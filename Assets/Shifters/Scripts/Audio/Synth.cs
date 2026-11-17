// =====================================================================================
//  SHIFTERS - Synth.cs   [owned by the AUDIO agent]
//  Core DSP primitives of the fully procedural audio: constants, deterministic RNG,
//  band-limited oscillators (polyBLEP saw/square, sine, triangle), pink noise.
//  Plain math only (no Unity objects) => safe to run on worker threads.
// =====================================================================================
using System;

namespace Shifters
{
    /// <summary>Global synthesis constants and stateless helpers.</summary>
    public static class Synth
    {
        public const int SampleRate = 44100;
        public const float Dt = 1f / SampleRate;
        public const float TwoPi = 6.28318530718f;
        /// <summary>-1 dBFS: every generated clip is normalized to this peak.</summary>
        public const float PeakTarget = 0.891f;

        public static int Frames(float seconds) => Math.Max(1, (int)Math.Ceiling(seconds * SampleRate));
        public static float[] Buffer(float seconds) => new float[Frames(seconds)];
        public static int ToFrames(float seconds) => (int)(seconds * SampleRate);
        public static float MidiToHz(float midi) => 440f * (float)Math.Pow(2.0, (midi - 69f) / 12.0);
        public static float Semis(float semitones) => (float)Math.Pow(2.0, semitones / 12.0);
        public static float DbToGain(float db) => (float)Math.Pow(10.0, db / 20.0);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        public static float Clamp(float x, float lo, float hi) => x < lo ? lo : (x > hi ? hi : x);
        /// <summary>Log-space interpolation (for frequencies).</summary>
        public static float ExpLerp(float a, float b, float t) => a * (float)Math.Pow(b / a, Clamp01(t));
        public static float Sine(float phase) => (float)Math.Sin(phase * TwoPi);
        public static float Tri(float p) { p -= (float)Math.Floor(p); return p < 0.5f ? 4f * p - 1f : 3f - 4f * p; }
        public static float Tanh(float x) => (float)Math.Tanh(x);

        /// <summary>Cheap tanh-like saturator (Pade), output in [-1, 1].</summary>
        public static float SoftClip(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        /// <summary>polyBLEP residual for band-limited discontinuities.</summary>
        public static float PolyBlep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }
    }

    /// <summary>Phase-accumulating oscillator. Mutable struct: keep it in a local/field and call methods on it.</summary>
    public struct AudioOsc
    {
        public float Phase;

        public AudioOsc(float phase) { Phase = phase; }

        void Step(float hz)
        {
            Phase += hz * Synth.Dt;
            if (Phase >= 1f || Phase < 0f) Phase -= (float)Math.Floor(Phase);
        }

        public float Sine(float hz) { float v = Synth.Sine(Phase); Step(hz); return v; }
        /// <summary>Phase-modulated sine (FM synthesis). pm in radians.</summary>
        public float SinePM(float hz, float pm) { float v = (float)Math.Sin(Phase * Synth.TwoPi + pm); Step(hz); return v; }
        public float Tri(float hz) { float v = Synth.Tri(Phase); Step(hz); return v; }

        public float Saw(float hz)
        {
            float dt = Math.Min(0.5f, Math.Abs(hz) * Synth.Dt);
            float v = 2f * Phase - 1f - Synth.PolyBlep(Phase, dt);
            Step(hz);
            return v;
        }

        public float Square(float hz, float pw = 0.5f)
        {
            float dt = Math.Min(0.5f, Math.Abs(hz) * Synth.Dt);
            float v = Phase < pw ? 1f : -1f;
            v += Synth.PolyBlep(Phase, dt);
            float p2 = Phase - pw;
            if (p2 < 0f) p2 += 1f;
            v -= Synth.PolyBlep(p2, dt);
            Step(hz);
            return v;
        }
    }

    /// <summary>Deterministic xorshift RNG (thread-safe as a value type; never touches UnityEngine.Random).</summary>
    public struct AudioRng
    {
        uint _s;

        public AudioRng(int seed)
        {
            unchecked { _s = (uint)seed * 2654435761u + 0x6D2B79F5u; }
            if (_s == 0) _s = 0x9E3779B9u;
            Next(); Next();
        }

        public uint Next() { uint x = _s; x ^= x << 13; x ^= x >> 17; x ^= x << 5; _s = x; return x; }
        /// <summary>[0, 1)</summary>
        public float Value() => (Next() >> 8) * (1f / 16777216f);
        /// <summary>[-1, 1)</summary>
        public float Signed() => Value() * 2f - 1f;
        public float Range(float a, float b) => a + (b - a) * Value();
        public int Range(int a, int bExclusive) => bExclusive <= a ? a : a + (int)(Next() % (uint)(bExclusive - a));
        public bool Chance(float p) => Value() < p;

        public static int Hash(int a, int b, int c = 0)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 15;
                return (int)h;
            }
        }
    }

    /// <summary>Paul Kellet's economy pink noise filter over white noise.</summary>
    public struct AudioPinkNoise
    {
        float _b0, _b1, _b2;

        public float Next(ref AudioRng rng)
        {
            float w = rng.Signed();
            _b0 = 0.99765f * _b0 + w * 0.0990460f;
            _b1 = 0.96300f * _b1 + w * 0.2965164f;
            _b2 = 0.57000f * _b2 + w * 1.0526913f;
            return (_b0 + _b1 + _b2 + w * 0.1848f) * 0.25f;
        }
    }
}
