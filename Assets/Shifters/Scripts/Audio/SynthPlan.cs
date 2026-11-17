// =====================================================================================
//  SHIFTERS - SynthPlan.cs   [owned by the AUDIO agent]
//  Deferred, time-sliceable rendering of SFX recipes (WebGL has no threads: a 3 s fanfare
//  rendered in one go is a 50-90 ms hitch there).
//  SfxRecipes.Plan runs a recipe while a SynthPlan RECORDS: SynthGen.Render / Layer and the
//  AudioFx whole-buffer effects then only append ops (patch cursor + RNG snapshot, effect +
//  its arguments) and return at once; recipe-level logic and RNG draws run normally (a noise
//  patch advances the caller's RNG by exactly the draws its rendering will make). Step()
//  later executes the ops in order, about N frames of work per call, and the result is
//  bit-identical to rendering directly. AudioFx measurements (Peak/Rms/AllFinite) flush the
//  plan first, so a recipe that reads its buffer still gets the real samples.
//  Rule for recipe code: touch buffer contents only through SynthGen / AudioFx.
//  Pure math, no UnityEngine: safe on worker threads (the recording flag is per thread).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;

namespace Shifters
{
    public sealed class SynthPlan
    {
        [ThreadStatic] static SynthPlan _recording;

        /// <summary>The plan recording on this thread (null = SynthGen/AudioFx execute immediately).</summary>
        public static SynthPlan Recording => _recording;

        /// <summary>Main thread, play-mode start (domain reload is off): never leave a stale recording behind.</summary>
        public static void ResetRecording() => _recording = null;

        readonly List<SynthPlanOp> _ops = new List<SynthPlanOp>(32);
        SynthPlan _outer;
        bool _isRecording;
        int _next;

        /// <summary>The buffer the recipe returned (complete once Finished).</summary>
        public float[] Result;
        /// <summary>Frames of work recorded / executed so far (patch frames + effect frames).</summary>
        public long TotalWork { get; private set; }
        public long DoneWork { get; private set; }
        public int OpCount => _ops.Count;
        public bool Finished => !_isRecording && _next >= _ops.Count;

        /// <summary>Starts recording on this thread (nestable). Always pair with End() in a finally block.</summary>
        public static SynthPlan Begin()
        {
            var p = new SynthPlan { _outer = _recording, _isRecording = true };
            _recording = p;
            return p;
        }

        public void End()
        {
            if (!_isRecording) return;
            _isRecording = false;
            if (_recording == this) _recording = _outer;
            _outer = null;
        }

        // ------------------------------------------------------------------ recording (called by SynthGen / AudioFx)

        internal int AddPatch(float[] dst, int offset, in SynthPatch p, ref AudioRng rng)
        {
            var c = new SynthPatchCursor(dst, offset, p, rng);
            if (c.Max <= 0) return 0;
            if (c.Noisy) for (int i = 0; i < c.Max; i++) rng.Next();   // the render draws one value per frame
            Add(new PatchOp { Cursor = c }, c.Max);
            return c.Max;
        }

        internal void Defer(float[] b, Action<float[], float, float> fx, float a = 0f, float c = 0f) =>
            Add(new BufferOp { Buffer = b, Fx = fx, A = a, C = c }, b != null ? b.Length : 1);

        internal void AddReverb(float[] b, AudioReverb rv, float mix) => Add(new ReverbOp { Buffer = b, Reverb = rv, Mix = mix }, b.Length);

        internal void AddEcho(float[] b, AudioEchoState e) => Add(new EchoOp { Buffer = b, Echo = e }, b.Length);

        internal void AddLayer(float[] b, float start, float[] layer, float peakGain) =>
            Add(new LayerOp { Buffer = b, Start = start, Layer = layer, PeakGain = peakGain }, layer != null ? layer.Length : 1);

        internal void AddLoop(float[] src, float[] dst, int xfadeFrames) => Add(new LoopOp { Src = src, Dst = dst, Xfade = xfadeFrames }, dst.Length);

        void Add(SynthPlanOp op, int work)
        {
            op.Work = Math.Max(1, work);
            TotalWork += op.Work;
            _ops.Add(op);
        }

        /// <summary>Executes everything recorded so far (a measurement inside the recipe needs real samples).</summary>
        public void Flush()
        {
            while (_next < _ops.Count) Step(int.MaxValue);
        }

        // ------------------------------------------------------------------ execution

        /// <summary>Executes about frames frames of work (patches/reverbs in slices; cheap whole-buffer effects
        /// count as their length but run in one go). Returns true once everything is done.</summary>
        public bool Step(int frames)
        {
            var saved = _recording;
            _recording = null;            // ops run the immediate SynthGen/AudioFx paths
            try
            {
                int budget = Math.Max(1, frames);
                while (_next < _ops.Count && budget > 0)
                {
                    var op = _ops[_next];
                    int w = Math.Max(1, op.Run(budget));
                    budget -= w;
                    DoneWork += w;
                    if (op.Done) { _ops[_next] = null; _next++; }   // let finished ops go
                }
            }
            finally { _recording = saved; }
            return Finished;
        }

        /// <summary>Executes the whole plan and returns Result.</summary>
        public float[] RunToEnd()
        {
            End();
            while (!Step(int.MaxValue)) { }
            return Result;
        }

        // ------------------------------------------------------------------ ops

        abstract class SynthPlanOp
        {
            public int Work;
            public bool Done;
            /// <summary>Does up to budget frames of work; returns the frames done.</summary>
            public abstract int Run(int budget);
        }

        sealed class PatchOp : SynthPlanOp
        {
            public SynthPatchCursor Cursor;
            public override int Run(int budget)
            {
                int w = Cursor.Advance(budget);
                Done = Cursor.Done;
                return w;
            }
        }

        /// <summary>Cheap whole-buffer effect (gain, normalize, saturate, crush, fades, DC block, low-pass).</summary>
        sealed class BufferOp : SynthPlanOp
        {
            public float[] Buffer;
            public Action<float[], float, float> Fx;
            public float A, C;
            public override int Run(int budget) { Fx(Buffer, A, C); Done = true; return Work; }
        }

        sealed class ReverbOp : SynthPlanOp
        {
            public float[] Buffer;
            public AudioReverb Reverb;
            public float Mix;
            int _i;
            public override int Run(int budget)
            {
                int to = (int)Math.Min((long)_i + budget, Buffer.Length);
                AudioFx.ReverbRange(Buffer, Reverb, Mix, _i, to);
                int w = to - _i;
                _i = to;
                Done = _i >= Buffer.Length;
                return w;
            }
        }

        sealed class EchoOp : SynthPlanOp
        {
            public float[] Buffer;
            public AudioEchoState Echo;
            int _i;
            public override int Run(int budget)
            {
                int to = (int)Math.Min((long)_i + budget, Buffer.Length);
                Echo.Process(Buffer, _i, to);
                int w = to - _i;
                _i = to;
                Done = _i >= Buffer.Length;
                return w;
            }
        }

        sealed class LayerOp : SynthPlanOp
        {
            public float[] Buffer, Layer;
            public float Start, PeakGain;
            public override int Run(int budget) { SynthGen.Layer(Buffer, Start, Layer, PeakGain); Done = true; return Work; }
        }

        sealed class LoopOp : SynthPlanOp
        {
            public float[] Src, Dst;
            public int Xfade;
            public override int Run(int budget) { AudioFx.MakeLoopInto(Src, Dst, Xfade); Done = true; return Work; }
        }
    }

    /// <summary>ClipFactory.Job.Steps for a render that is planned lazily: the first MoveNext records the plan
    /// (cheap: recipe logic only), every further MoveNext executes about SliceFrames frames of it (well under
    /// a millisecond on desktop). Result holds the samples once MoveNext returned false.</summary>
    public sealed class SynthPlanTask : IEnumerator
    {
        /// <summary>Frames of work per MoveNext (~0.05-0.4 ms of DSP on desktop .NET, a few times more in Mono/WebGL).</summary>
        public const int SliceFrames = 8192;

        readonly Func<SynthPlan> _makePlan;
        SynthPlan _plan;

        public float[] Result { get; private set; }
        public int Slices { get; private set; }
        public object Current => null;

        public SynthPlanTask(Func<SynthPlan> makePlan) { _makePlan = makePlan; }

        public bool MoveNext()
        {
            if (Result != null) return false;
            Slices++;
            if (_plan == null)
            {
                _plan = _makePlan != null ? _makePlan() : null;
                if (_plan == null) return false;
                _plan.End();
                return !_plan.Finished || Finish();
            }
            return !_plan.Step(SliceFrames) || Finish();
        }

        bool Finish()
        {
            Result = _plan.Result;
            _plan = null;
            return false;
        }

        public void Reset() { }
    }
}
