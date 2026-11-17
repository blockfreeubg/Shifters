// =====================================================================================
//  SHIFTERS - MusicSynth.cs   [owned by the AUDIO agent]
//  Tiny step sequencer that renders a SongSpec into a seamless stereo loop:
//  notes -> instrument patches (MusicInstruments) -> 4 buses (drums / bass / keys / lead)
//  with wrap-around note tails, kick-driven sidechain ducking on bass+keys, circular
//  reverb + ping-pong echo (so tails wrap seamlessly), master soft-knee limiter and
//  loudness/peak normalization (~-14 dBFS RMS feel, peak <= -1 dBFS).
//  Loop length is EXACTLY Bars * BarFrames samples. Pure math: run it on a worker thread.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;

namespace Shifters
{
    public struct MusicNote
    {
        public float Step, Len, Midi, Vel, Pan, GlideFrom;
        public MusicInst MusicInst;
    }

    /// <summary>Song description (built by MusicSongs).</summary>
    public sealed class SongSpec
    {
        public string Name = "Song";
        public float Bpm = 120f;
        public int StepsPerBeat = 4;     // 16th-note grid
        public int StepsPerBar = 16;     // 14 => 7/8
        public int Bars = 8;
        public float Swing;              // fraction of the swing unit
        public int SwingGrid = 1;        // 1 = 16th swing (odd steps), 2 = 8th swing (steps % 4 == 2)
        public int Seed = 1;
        public float DuckDepth = 0.3f, DuckRelease = 0.15f;
        public float ReverbSize = 0.55f, ReverbDamp = 0.4f, ReverbReturn = 0.55f;
        public float[] Sends = { 0.05f, 0f, 0.25f, 0.3f };     // reverb sends: drums, bass, keys, lead
        public float[] BusGain = { 0.9f, 0.8f, 0.62f, 0.55f };
        public float EchoSteps = 3f, EchoFeedback = 0.35f, EchoSend = 0.18f; // lead bus ping-pong
        public readonly List<MusicNote> Notes = new List<MusicNote>(768);

        public int TotalSteps => StepsPerBar * Bars;

        public void Add(float step, float len, MusicInst inst, float midi, float vel = 1f, float pan = 0f, float glideFrom = 0f)
        {
            Notes.Add(new MusicNote { Step = step, Len = len, MusicInst = inst, Midi = midi, Vel = vel, Pan = pan, GlideFrom = glideFrom });
        }
    }

    /// <summary>Rendered loop (interleaved stereo).</summary>
    public sealed class MusicData
    {
        public string Name;
        public float[] Interleaved;
        public int Frames, BarFrames, Bars, StepFrames;
        public float Bpm, Peak, Rms;
        public float[] BusRms = new float[4];
        public double RenderMs;
    }

    public static class MusicSynth
    {
        public const float TargetRms = 0.19f;

        /// <summary>Renders a MusicId (null for None / unknown).</summary>
        public static MusicData Render(MusicId id)
        {
            var spec = MusicSongs.Build(id);
            return spec == null ? null : Render(spec);
        }

        public static MusicData Render(SongSpec s)
        {
            var md = new MusicData();
            var e = RenderSteps(s, md);
            while (e.MoveNext()) { }
            return md;
        }

        /// <summary>Frames of buffer work between two yields of RenderSteps (well under 2 ms on desktop .NET).</summary>
        public const int SliceFrames = 16384;

        /// <summary>Incremental render into md: every MoveNext does a small slice (a few notes or SliceFrames of
        /// mixing/effects), so single-threaded platforms (WebGL) can spread a track over frames.
        /// md.Interleaved is set when the enumerator finishes.</summary>
        public static IEnumerator RenderSteps(SongSpec s, MusicData md)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int stepFrames = (int)Math.Round(Synth.SampleRate * 60.0 / s.Bpm / s.StepsPerBeat);
            int barFrames = stepFrames * s.StepsPerBar;
            int n = barFrames * s.Bars;
            var L = new float[4][];
            var R = new float[4][];
            for (int i = 0; i < 4; i++)
            {
                L[i] = new float[n]; sw.Stop(); yield return null; sw.Start();   // big zeroed buffers: one per slice
                R[i] = new float[n]; sw.Stop(); yield return null; sw.Start();
            }
            var scratch = new float[Synth.Frames(6f)];
            var kicks = new List<int>(64);
            var rng = new AudioRng(s.Seed);
            int used = 0, work = 0;

            for (int ni = 0; ni < s.Notes.Count; ni++)
            {
                var note = s.Notes[ni];
                int istep = (int)Math.Floor(note.Step);
                double swing = 0;
                if (s.Swing > 0f && Math.Abs(note.Step - istep) < 1e-4f)
                {
                    if (s.SwingGrid == 2 && (istep & 3) == 2) swing = s.Swing * 2.0 * stepFrames;
                    else if (s.SwingGrid == 1 && (istep & 1) == 1) swing = s.Swing * stepFrames;
                }
                int start = (int)(note.Step * stepFrames + swing);
                start %= n;
                if (start < 0) start += n;
                float lenSec = Math.Max(0.02f, note.Len * stepFrames / (float)Synth.SampleRate);
                Array.Clear(scratch, 0, used);
                float hz = Synth.MidiToHz(note.Midi);
                float glide = note.GlideFrom > 0f ? Synth.MidiToHz(note.GlideFrom) : 0f;
                // the note is recorded as a plan and executed in slices (a long pad/brass note is several ms of DSP)
                var notePlan = SynthPlan.Begin();
                try { used = MusicInstruments.Render(note.MusicInst, hz, lenSec, note.Vel, ref rng, scratch, glide); }
                finally { notePlan.End(); }
                while (!notePlan.Step(SliceFrames)) { sw.Stop(); yield return null; sw.Start(); }
                int bus = MusicInstruments.BusOf(note.MusicInst);
                float pan = Synth.Clamp(note.Pan, -1f, 1f);
                float ang = (pan + 1f) * 0.25f * (float)Math.PI;
                float gl = (float)Math.Cos(ang) * 1.4142f, gr = (float)Math.Sin(ang) * 1.4142f;
                var bl = L[bus]; var br = R[bus];
                int idx = start;
                for (int i = 0; i < used; i++)
                {
                    float v = scratch[i];
                    bl[idx] += v * gl;
                    br[idx] += v * gr;
                    if (++idx >= n) idx = 0;
                }
                if (note.MusicInst == MusicInst.Kick) kicks.Add(start);
                work += used;
                if (work >= SliceFrames) { work = 0; sw.Stop(); yield return null; sw.Start(); }
            }

            // sidechain-ish ducking of bass + keys from kick hits
            if (s.DuckDepth > 0f && kicks.Count > 0)
            {
                var duck = new float[n];
                sw.Stop(); yield return null; sw.Start();
                for (int i = 0; i < n; i++)
                {
                    duck[i] = 1f;
                    if ((i & (SliceFrames * 16 - 1)) == SliceFrames * 16 - 1) { sw.Stop(); yield return null; sw.Start(); }
                }
                int att = Synth.Frames(0.006f), rel = Synth.Frames(s.DuckRelease);
                int span = att + rel * 5;
                var env = new float[span];
                for (int i = 0; i < span; i++) env[i] = 1f - s.DuckDepth * (i < att ? (float)i / att : (float)Math.Exp(-(i - att) / (double)rel));
                sw.Stop(); yield return null; sw.Start();
                int duckWork = 0;
                foreach (int k0 in kicks)
                {
                    for (int i = 0; i < span; i++)
                    {
                        int j = (k0 + i) % n;
                        if (env[i] < duck[j]) duck[j] = env[i];
                    }
                    duckWork += span;
                    if (duckWork >= SliceFrames * 4) { duckWork = 0; sw.Stop(); yield return null; sw.Start(); }
                }
                sw.Stop(); yield return null; sw.Start();
                for (int i = 0; i < n; i++)
                {
                    float d = duck[i];
                    L[1][i] *= d; R[1][i] *= d; L[2][i] *= d; R[2][i] *= d;
                    if ((i & (SliceFrames * 2 - 1)) == 0 && i > 0) { sw.Stop(); yield return null; sw.Start(); }
                }
            }

            var outL = new float[n];
            sw.Stop(); yield return null; sw.Start();
            var outR = new float[n];
            sw.Stop(); yield return null; sw.Start();
            md.Name = s.Name; md.Bars = s.Bars; md.BarFrames = barFrames; md.Frames = n; md.StepFrames = stepFrames; md.Bpm = s.Bpm;
            for (int b = 0; b < 4; b++)
            {
                float g = s.BusGain[b];
                double acc = 0;
                for (int i = 0; i < n; i++)
                {
                    float l = L[b][i] * g, r = R[b][i] * g;
                    outL[i] += l; outR[i] += r;
                    acc += l * l + r * r;
                    if ((i & (SliceFrames * 8 - 1)) == SliceFrames * 8 - 1) { sw.Stop(); yield return null; sw.Start(); }
                }
                md.BusRms[b] = (float)Math.Sqrt(acc / (2.0 * n));
                sw.Stop(); yield return null; sw.Start();
            }

            // effects (circular so tails wrap around the loop point)
            var send = new float[n];
            sw.Stop(); yield return null; sw.Start();
            var sendR = new float[n];
            sw.Stop(); yield return null; sw.Start();
            for (int b = 0; b < 4; b++)
            {
                float sg = s.Sends[b] * s.BusGain[b];
                if (sg <= 0f) continue;
                for (int i = 0; i < n; i++)
                {
                    send[i] += L[b][i] * sg; sendR[i] += R[b][i] * sg;
                    if ((i & (SliceFrames * 8 - 1)) == SliceFrames * 8 - 1) { sw.Stop(); yield return null; sw.Start(); }
                }
            }
            sw.Stop(); yield return null; sw.Start();
            var rv = AudioFx.ReverbCircularSteps(send, sendR, outL, outR, s.ReverbReturn, s.ReverbSize, s.ReverbDamp);
            while (rv.MoveNext()) { sw.Stop(); yield return null; sw.Start(); }
            if (s.EchoSend > 0f)
            {
                var pp = AudioFx.PingPongCircularSteps(L[3], R[3], outL, outR, (int)(s.EchoSteps * stepFrames), s.EchoFeedback, s.EchoSend * s.BusGain[3]);
                while (pp.MoveNext()) { sw.Stop(); yield return null; sw.Start(); }
            }

            // master: loudness to target RMS, soft-knee limiter, then peak <= -1 dBFS
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += outL[i] * outL[i] + outR[i] * outR[i];
                if ((i & (SliceFrames * 8 - 1)) == SliceFrames * 8 - 1) { sw.Stop(); yield return null; sw.Start(); }
            }
            sw.Stop(); yield return null; sw.Start();
            float rms = (float)Math.Sqrt(sum / (2.0 * n));
            float gain = rms > 1e-6f ? TargetRms / rms : 1f;
            var data = new float[n * 2];
            sw.Stop(); yield return null; sw.Start();
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float l = Limit(outL[i] * gain), r = Limit(outR[i] * gain);
                data[2 * i] = l; data[2 * i + 1] = r;
                float al = Math.Abs(l), ar = Math.Abs(r);
                if (al > peak) peak = al;
                if (ar > peak) peak = ar;
                if ((i & (SliceFrames * 2 - 1)) == 0 && i > 0) { sw.Stop(); yield return null; sw.Start(); }
            }
            if (peak > Synth.PeakTarget)
            {
                sw.Stop(); yield return null; sw.Start();
                float k = Synth.PeakTarget / peak;
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] *= k;
                    if ((i & (SliceFrames * 16 - 1)) == SliceFrames * 16 - 1) { sw.Stop(); yield return null; sw.Start(); }
                }
                peak = Synth.PeakTarget;
            }
            sw.Stop(); yield return null; sw.Start();
            md.Peak = peak;
            double rs = 0;
            for (int i = 0; i < data.Length; i++)
            {
                rs += data[i] * data[i];
                if ((i & (SliceFrames * 16 - 1)) == SliceFrames * 16 - 1) { sw.Stop(); yield return null; sw.Start(); }
            }
            md.Rms = data.Length == 0 ? 0f : (float)Math.Sqrt(rs / data.Length);
            md.RenderMs = sw.Elapsed.TotalMilliseconds;
            md.Interleaved = data;
        }

        static float Limit(float x)
        {
            const float T = 0.6f;
            float a = x < 0f ? -x : x;
            if (a <= T) return x;
            float y = T + (1f - T) * Synth.Tanh((a - T) / (1f - T));
            return x < 0f ? -y : y;
        }
    }
}
