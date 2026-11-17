// =====================================================================================
//  SHIFTERS - SynthFilters.cs   [owned by the AUDIO agent]
//  Filters & reverb used by the procedural audio: one-pole LP/HP, a resonant
//  state-variable filter (Simper/TPT topology - stable under fast modulation) and a
//  small Freeverb-style room (4 combs + 2 allpasses). Plain math, worker-thread safe.
// =====================================================================================
using System;

namespace Shifters
{
    /// <summary>One-pole low/high pass. Mutable struct.</summary>
    public struct AudioOnePole
    {
        public float Z;
        public static float Coef(float hz) => 1f - (float)Math.Exp(-Synth.TwoPi * Math.Min(Math.Max(hz, 1f), 20000f) * Synth.Dt);
        public float LP(float x, float coef) { Z += coef * (x - Z); return Z; }
        public float HP(float x, float coef) { Z += coef * (x - Z); return x - Z; }
    }

    /// <summary>Resonant state-variable filter (low / band / high outputs). Call Set before processing.</summary>
    public struct AudioSvf
    {
        float _ic1, _ic2, _a1, _a2, _a3, _k, _hz, _q;

        public void Set(float hz, float q)
        {
            if (hz == _hz && q == _q && _a1 != 0f) return;
            _hz = hz; _q = q;
            float f = Math.Max(10f, Math.Min(hz, Synth.SampleRate * 0.45f));
            float g = (float)Math.Tan(Math.PI * f / Synth.SampleRate);
            _k = 1f / Math.Max(0.1f, q);
            _a1 = 1f / (1f + g * (g + _k));
            _a2 = g * _a1;
            _a3 = g * _a2;
        }

        public float Low(float x) { Tick(x, out _, out float v2); return v2; }
        /// <summary>Band-pass normalized to unity gain at the center frequency.</summary>
        public float Band(float x) { Tick(x, out float v1, out _); return v1 * _k; }
        public float High(float x) { Tick(x, out float v1, out float v2); return x - _k * v1 - v2; }

        void Tick(float v0, out float v1, out float v2)
        {
            float v3 = v0 - _ic2;
            v1 = _a1 * _ic1 + _a2 * v3;
            v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2f * v1 - _ic1;
            _ic2 = 2f * v2 - _ic2;
        }
    }

    /// <summary>Small Freeverb-style reverb (mono in, mono out). Use two with different spread for stereo.</summary>
    public sealed class AudioReverb
    {
        static readonly int[] CombLengths = { 1116, 1188, 1277, 1356 };
        static readonly int[] AllpassLengths = { 556, 441 };

        readonly float[][] _comb = new float[4][];
        readonly int[] _ci = new int[4];
        readonly float[] _cs = new float[4];
        readonly float[][] _ap = new float[2][];
        readonly int[] _ai = new int[2];
        readonly float _fb, _damp;

        /// <param name="size">0..1 room size (feedback)</param>
        /// <param name="damp">0..1 high-frequency damping</param>
        /// <param name="scale">delay-length scale (0.5 = small box, 1.5 = hall)</param>
        /// <param name="spread">extra samples for a decorrelated right channel (23 in Freeverb)</param>
        public AudioReverb(float size = 0.5f, float damp = 0.4f, float scale = 1f, int spread = 0)
        {
            _fb = 0.7f + 0.28f * Synth.Clamp01(size);
            _damp = Synth.Clamp01(damp);
            for (int i = 0; i < 4; i++) _comb[i] = new float[Math.Max(8, (int)((CombLengths[i] + spread) * scale))];
            for (int i = 0; i < 2; i++) _ap[i] = new float[Math.Max(8, (int)((AllpassLengths[i] + spread) * scale))];
        }

        public float Process(float x)
        {
            float acc = 0f;
            for (int c = 0; c < 4; c++)
            {
                var buf = _comb[c];
                int idx = _ci[c];
                float y = buf[idx];
                _cs[c] = y * (1f - _damp) + _cs[c] * _damp;
                buf[idx] = x + _cs[c] * _fb;
                if (++idx >= buf.Length) idx = 0;
                _ci[c] = idx;
                acc += y;
            }
            for (int a = 0; a < 2; a++)
            {
                var buf = _ap[a];
                int idx = _ai[a];
                float bo = buf[idx];
                float y = -acc + bo;
                buf[idx] = acc + bo * 0.5f;
                if (++idx >= buf.Length) idx = 0;
                _ai[a] = idx;
                acc = y;
            }
            return acc * 0.25f;
        }
    }
}
