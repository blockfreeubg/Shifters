// =====================================================================================
//  SHIFTERS - SynthFx.cs   [owned by the AUDIO agent]
//  Whole-buffer processing: measurement (peak/RMS/finite), gain & normalization to -1 dBFS,
//  saturation, bitcrush, fades, DC blocking, echo, small-room reverb, seamless-loop
//  crossfades and circular (wrap-around) reverb/ping-pong for perfectly looping music.
//  While a SynthPlan records (SfxRecipes.Plan), the whole-buffer effects are deferred into the
//  plan (executed later, in order, in slices) and the measurements first flush the plan.
// =====================================================================================
using System;

namespace Shifters
{
    public static class AudioFx
    {
        // deferred forms (no captures => allocated once)
        static readonly System.Action<float[], float, float> GainOp = (b, g, _) => Gain(b, g);
        static readonly System.Action<float[], float, float> NormalizeOp = (b, pk, _) => Normalize(b, pk);
        static readonly System.Action<float[], float, float> SaturateOp = (b, d, _) => Saturate(b, d);
        static readonly System.Action<float[], float, float> CrushOp = (b, h, l) => Crush(b, (int)h, l);
        static readonly System.Action<float[], float, float> FadeInOp = (b, sec, _) => FadeIn(b, sec);
        static readonly System.Action<float[], float, float> FadeOutOp = (b, sec, _) => FadeOut(b, sec);
        static readonly System.Action<float[], float, float> DcBlockOp = (b, _, __) => DcBlock(b);
        static readonly System.Action<float[], float, float> LowPassOp = (b, hz, _) => LowPass(b, hz);

        /// <summary>Measurements need the real samples: a recording plan executes what it has so far first.</summary>
        static void FlushPlan() => SynthPlan.Recording?.Flush();

        public static float Peak(float[] b)
        {
            FlushPlan();
            float m = 0f;
            for (int i = 0; i < b.Length; i++) { float a = b[i] < 0f ? -b[i] : b[i]; if (a > m) m = a; }
            return m;
        }

        public static float Rms(float[] b)
        {
            FlushPlan();
            if (b.Length == 0) return 0f;
            double s = 0;
            for (int i = 0; i < b.Length; i++) s += b[i] * b[i];
            return (float)Math.Sqrt(s / b.Length);
        }

        public static bool AllFinite(float[] b)
        {
            FlushPlan();
            for (int i = 0; i < b.Length; i++) if (float.IsNaN(b[i]) || float.IsInfinity(b[i])) return false;
            return true;
        }

        public static void Gain(float[] b, float g)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, GainOp, g); return; }
            for (int i = 0; i < b.Length; i++) b[i] *= g;
        }

        /// <summary>Scales so the absolute peak equals peak (default -1 dBFS). Replaces NaN/Inf with 0 first.</summary>
        public static void Normalize(float[] b, float peak = Synth.PeakTarget)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, NormalizeOp, peak); return; }
            for (int i = 0; i < b.Length; i++) if (float.IsNaN(b[i]) || float.IsInfinity(b[i])) b[i] = 0f;
            float pk = Peak(b);
            if (pk < 1e-7f) return;
            Gain(b, peak / pk);
        }

        public static void Saturate(float[] b, float drive)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, SaturateOp, drive); return; }
            for (int i = 0; i < b.Length; i++) b[i] = Synth.SoftClip(b[i] * drive);
        }

        /// <summary>Sample-rate reduction (hold N samples) + amplitude quantization.</summary>
        public static void Crush(float[] b, int hold, float levels)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, CrushOp, hold, levels); return; }
            float h = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                if (hold <= 1 || i % hold == 0) h = levels > 0f ? (float)Math.Round(b[i] * levels) / levels : b[i];
                b[i] = h;
            }
        }

        public static void FadeIn(float[] b, float seconds)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, FadeInOp, seconds); return; }
            int n = Math.Min(b.Length, Synth.Frames(seconds));
            for (int i = 0; i < n; i++) b[i] *= (float)i / n;
        }

        public static void FadeOut(float[] b, float seconds)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, FadeOutOp, seconds); return; }
            int n = Math.Min(b.Length, Synth.Frames(seconds));
            for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= (float)i / n;
        }

        /// <summary>Removes DC offset (one-pole high pass at ~18 Hz).</summary>
        public static void DcBlock(float[] b)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, DcBlockOp); return; }
            float x1 = 0f, y1 = 0f;
            const float R = 0.9975f;
            for (int i = 0; i < b.Length; i++)
            {
                float x = b[i];
                float y = x - x1 + R * y1;
                x1 = x; y1 = y;
                b[i] = y;
            }
        }

        public static void LowPass(float[] b, float hz)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, LowPassOp, hz); return; }
            var f = new AudioOnePole();
            float c = AudioOnePole.Coef(hz);
            for (int i = 0; i < b.Length; i++) b[i] = f.LP(b[i], c);
        }

        /// <summary>In-place feedback echo; leave silence at the end of the buffer for the tail.</summary>
        public static void Echo(float[] b, float delaySeconds, float feedback, float mix)
        {
            var e = new AudioEchoState(delaySeconds, feedback, mix);
            if (SynthPlan.Recording != null) { SynthPlan.Recording.AddEcho(b, e); return; }
            e.Process(b, 0, b.Length);
        }

        /// <summary>In-place small room reverb (wet added on top of dry). Leave silence at the end for the tail.</summary>
        public static void Reverb(float[] b, float mix, float size = 0.5f, float damp = 0.4f, float scale = 1f)
        {
            var rv = new AudioReverb(size, damp, scale, 0);
            if (SynthPlan.Recording != null) { SynthPlan.Recording.AddReverb(b, rv, mix); return; }
            ReverbRange(b, rv, mix, 0, b.Length);
        }

        /// <summary>Reverb of frames [from, to) with persistent state (slices of one Reverb call).</summary>
        public static void ReverbRange(float[] b, AudioReverb rv, float mix, int from, int to)
        {
            for (int i = from; i < to; i++) { float x = b[i]; b[i] = x + mix * rv.Process(x); }
        }

        /// <summary>Turns a buffer rendered with xfadeSeconds of extra material at the end into a seamless loop.</summary>
        public static float[] MakeLoop(float[] b, float xfadeSeconds)
        {
            int x = Math.Min(Synth.Frames(xfadeSeconds), b.Length / 3);
            int n = b.Length - x;
            var o = new float[n];
            if (SynthPlan.Recording != null) { SynthPlan.Recording.AddLoop(b, o, x); return o; }
            MakeLoopInto(b, o, x);
            return o;
        }

        /// <summary>Fills o (length b.Length - x) with the seamless loop of b (x crossfade frames).</summary>
        public static void MakeLoopInto(float[] b, float[] o, int x)
        {
            int n = o.Length;
            Array.Copy(b, o, n);
            for (int i = 0; i < x; i++)
            {
                float u = (float)i / x;
                float gin = (float)Math.Sqrt(u), gout = (float)Math.Sqrt(1f - u);
                o[i] = b[i] * gin + b[n + i] * gout;
            }
        }

        /// <summary>Stereo reverb of a looping signal: runs twice around the loop so the tail wraps seamlessly.</summary>
        public static void ReverbCircular(float[] inL, float[] inR, float[] outL, float[] outR, float send, float size, float damp, float scale = 1f)
        {
            var e = ReverbCircularSteps(inL, inR, outL, outR, send, size, damp, scale);
            while (e.MoveNext()) { }
        }

        /// <summary>Incremental ReverbCircular (yields every MusicSynth.SliceFrames frames).</summary>
        public static System.Collections.IEnumerator ReverbCircularSteps(float[] inL, float[] inR, float[] outL, float[] outR, float send, float size, float damp, float scale = 1f)
        {
            var l = new AudioReverb(size, damp, scale, 0);
            var r = new AudioReverb(size, damp, scale, 23);
            int n = inL.Length;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float x = (inL[i] + inR[i]) * 0.5f * send;
                    float yl = l.Process(x), yr = r.Process(x);
                    if (pass == 1) { outL[i] += yl; outR[i] += yr; }
                    if ((i & (MusicSynth.SliceFrames - 1)) == MusicSynth.SliceFrames - 1) yield return null;
                }
        }

        /// <summary>Ping-pong echo of a looping signal (wraps seamlessly).</summary>
        public static void PingPongCircular(float[] inL, float[] inR, float[] outL, float[] outR, int delayFrames, float feedback, float send)
        {
            var e = PingPongCircularSteps(inL, inR, outL, outR, delayFrames, feedback, send);
            while (e.MoveNext()) { }
        }

        /// <summary>Incremental PingPongCircular (yields every 4 x MusicSynth.SliceFrames frames).</summary>
        public static System.Collections.IEnumerator PingPongCircularSteps(float[] inL, float[] inR, float[] outL, float[] outR, int delayFrames, float feedback, float send)
        {
            delayFrames = Math.Max(1, delayFrames);
            var dl = new float[delayFrames];
            var dr = new float[delayFrames];
            int idx = 0, n = inL.Length;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float x = (inL[i] + inR[i]) * 0.5f * send;
                    float yl = dl[idx], yr = dr[idx];
                    dl[idx] = x + yr * feedback;
                    dr[idx] = yl * feedback;
                    if (pass == 1) { outL[i] += yl; outR[i] += yr; }
                    if (++idx >= delayFrames) idx = 0;
                    if ((i & (MusicSynth.SliceFrames * 4 - 1)) == MusicSynth.SliceFrames * 4 - 1) yield return null;
                }
        }
    }

    /// <summary>Feedback echo state (AudioFx.Echo), resumable so a plan can run it in slices.</summary>
    public sealed class AudioEchoState
    {
        readonly float[] _line;
        readonly float _feedback, _mix;
        int _idx;

        public AudioEchoState(float delaySeconds, float feedback, float mix)
        {
            _line = new float[Math.Max(1, Synth.ToFrames(delaySeconds))];
            _feedback = feedback;
            _mix = mix;
        }

        /// <summary>Processes frames [from, to) of b in place.</summary>
        public void Process(float[] b, int from, int to)
        {
            int d = _line.Length;
            for (int i = from; i < to; i++)
            {
                float y = _line[_idx];
                _line[_idx] = b[i] + y * _feedback;
                b[i] += y * _mix;
                if (++_idx >= d) _idx = 0;
            }
        }
    }
}
