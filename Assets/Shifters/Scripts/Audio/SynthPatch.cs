// =====================================================================================
//  SHIFTERS - SynthPatch.cs   [owned by the AUDIO agent]
//  A tiny "patch" synthesizer: one SynthPatch = oscillator (+FM / PWM / vibrato / pitch sweeps
//  & transient pitch drop) -> filters (SVF LP with sweep/envelope, BP, one-pole HP) ->
//  saturation -> amplitude envelope (attack/hold/decay/sustain + gated release, tremolo).
//  SynthGen.Render adds a patch into a float buffer (via SynthPatchCursor, which can also render it
//  in slices). SFX, music instruments and stingers are all built by layering patches.
//  Worker-thread safe (pure math).
// =====================================================================================
using System;

namespace Shifters
{
    public enum SynthWave { Sine, Triangle, Saw, Square, Pulse, Noise, Pink }

    /// <summary>Immutable-style synth voice description. Build with SynthPatch.Make(...) and the fluent modifiers.</summary>
    public struct SynthPatch
    {
        public SynthWave SynthWave;
        public float Freq, FreqEnd, SweepTime, Freq2, Sweep2Time;
        public bool LinearSweep;
        public float PitchEnvSemis, PitchEnvTau;
        public float VibHz, VibSemis, VibTau;
        public float FmRatio, FmIndex, FmTau;
        public float Pw, PwmHz, PwmDepth;
        public float Attack, Hold, Decay, Sustain, Gate, Release;
        public float Duration, Gain;
        public float LpHz, LpEndHz, LpEnvHz, LpEnvTau, LpQ;
        public float BpHz, BpEndHz, BpQ;
        public float HpHz;
        public float Drive;
        public float TremHz, TremDepth;
        public float Crush;

        public static SynthPatch Make(SynthWave w, float hz, float duration, float gain = 1f) =>
            new SynthPatch { SynthWave = w, Freq = hz, Duration = duration, Gain = gain, Attack = 0.002f, Pw = 0.5f, LpQ = 0.707f, BpQ = 1f };

        /// <summary>Glide to toHz over time seconds (0 = whole duration), exponential unless linear.</summary>
        public SynthPatch Sweep(float toHz, float time = 0f, bool linear = false) { var p = this; p.FreqEnd = toHz; p.SweepTime = time; p.LinearSweep = linear; return p; }
        /// <summary>After the first sweep, glide on to hz over time seconds.</summary>
        public SynthPatch Then(float hz, float time) { var p = this; p.Freq2 = hz; p.Sweep2Time = time; return p; }
        /// <summary>Transient pitch drop: starts +semis above and decays with tau (kicks, bonks, toms).</summary>
        public SynthPatch PitchEnv(float semis, float tau) { var p = this; p.PitchEnvSemis = semis; p.PitchEnvTau = tau; return p; }
        public SynthPatch Vib(float hz, float semis, float decayTau = 0f) { var p = this; p.VibHz = hz; p.VibSemis = semis; p.VibTau = decayTau; return p; }
        /// <summary>Sine-modulator phase modulation (only for SynthWave.Sine). tau > 0 => index decays.</summary>
        public SynthPatch Fm(float ratio, float index, float tau = 0f) { var p = this; p.FmRatio = ratio; p.FmIndex = index; p.FmTau = tau; return p; }
        /// <summary>Amplitude: linear attack, optional hold, exponential decay (tau) toward sustain. decay 0 = flat.</summary>
        public SynthPatch Env(float attack, float decay, float sustain = 0f, float hold = 0f) { var p = this; p.Attack = Math.Max(0.0003f, attack); p.Decay = decay; p.Sustain = sustain; p.Hold = hold; return p; }
        /// <summary>Gated release: after gate seconds the amplitude decays with tau release.</summary>
        public SynthPatch Rel(float gate, float release) { var p = this; p.Gate = Math.Max(0.001f, gate); p.Release = release; return p; }
        public SynthPatch Lp(float hz, float q = 0.707f, float endHz = 0f) { var p = this; p.LpHz = hz; p.LpQ = q; p.LpEndHz = endHz; return p; }
        /// <summary>Low-pass with a decaying cutoff envelope: cutoff = baseHz + envHz * exp(-t/tau).</summary>
        public SynthPatch LpEnv(float baseHz, float envHz, float tau, float q = 0.707f) { var p = this; p.LpHz = baseHz; p.LpEnvHz = envHz; p.LpEnvTau = tau; p.LpQ = q; return p; }
        public SynthPatch Bp(float hz, float q = 1f, float endHz = 0f) { var p = this; p.BpHz = hz; p.BpQ = q; p.BpEndHz = endHz; return p; }
        public SynthPatch Hp(float hz) { var p = this; p.HpHz = hz; return p; }
        public SynthPatch Sat(float drive) { var p = this; p.Drive = drive; return p; }
        public SynthPatch Trem(float hz, float depth) { var p = this; p.TremHz = hz; p.TremDepth = depth; return p; }
        public SynthPatch Pwm(float pw, float hz = 0f, float depth = 0f) { var p = this; p.Pw = pw; p.PwmHz = hz; p.PwmDepth = depth; return p; }
        /// <summary>Amplitude quantization (levels per unit, e.g. 12 = crunchy).</summary>
        public SynthPatch Bits(float levels) { var p = this; p.Crush = levels; return p; }
    }

    /// <summary>Resumable rendering of one patch into a buffer. All per-sample state (oscillators, filters, noise,
    /// RNG, sample index) lives here, so rendering in slices (Advance(frames) repeatedly - SynthPlan, time-sliced
    /// on WebGL) gives exactly the same samples as rendering in one go (SynthGen.Render).</summary>
    public struct SynthPatchCursor
    {
        public readonly float[] Dst;
        /// <summary>Frames this patch writes (0 = nothing to do).</summary>
        public readonly int Max;
        readonly SynthPatch _p;
        readonly int _offset, _n, _fadeN;
        readonly float _hpC, _sweepT, _logR1, _logR2, _lpQ, _bpQ, _gain, _pitchTau, _lpEnvTau, _relTau, _invDur;
        readonly bool _sweep, _then, _lpOn, _bpOn;
        int _i;
        AudioRng _rng;
        AudioOsc _osc, _mod;
        AudioSvf _lp, _bp;
        AudioOnePole _hp;
        AudioPinkNoise _pink;

        public SynthPatchCursor(float[] dst, int offset, in SynthPatch p, AudioRng rng)
        {
            Dst = dst;
            _p = p;
            _rng = rng;
            _i = 0;
            _osc = default; _mod = default; _lp = default; _bp = default; _hp = default; _pink = default;
            _n = Synth.Frames(p.Duration);
            if (offset < 0) offset = 0;
            _offset = offset;
            Max = dst == null ? 0 : Math.Max(0, Math.Min(_n, dst.Length - offset));
            _hpC = p.HpHz > 0f ? AudioOnePole.Coef(p.HpHz) : 0f;
            _fadeN = Math.Max(1, Math.Min(_n / 4, 220));
            _sweep = p.FreqEnd > 0f && p.Freq > 0f;
            _sweepT = p.SweepTime > 0f ? p.SweepTime : p.Duration;
            _logR1 = _sweep && !p.LinearSweep ? (float)Math.Log(p.FreqEnd / p.Freq) : 0f;
            _then = _sweep && p.Freq2 > 0f;
            _logR2 = _then ? (float)Math.Log(p.Freq2 / p.FreqEnd) : 0f;
            _lpOn = p.LpHz > 0f; _bpOn = p.BpHz > 0f;
            _lpQ = p.LpQ > 0f ? p.LpQ : 0.707f; _bpQ = p.BpQ > 0f ? p.BpQ : 1f;
            float gain = p.Gain;
            if (p.SynthWave == SynthWave.Noise || p.SynthWave == SynthWave.Pink)
            {
                // keep filtered noise roughly as loud (RMS) as an unfiltered tone of the same gain
                if (_bpOn) gain *= Math.Min(12f, (float)Math.Sqrt(22050f * _bpQ / Math.Max(50f, p.BpHz)));
                if (_lpOn) gain *= Math.Min(8f, (float)Math.Sqrt(22050f / Math.Max(50f, p.LpHz + p.LpEnvHz * 0.5f)));
            }
            _gain = gain;
            _pitchTau = Math.Max(1e-4f, p.PitchEnvTau); _lpEnvTau = Math.Max(1e-4f, p.LpEnvTau); _relTau = Math.Max(1e-3f, p.Release);
            _invDur = 1f / Math.Max(1e-4f, p.Duration);
        }

        public bool Done => _i >= Max;
        public int Remaining => Max - _i;
        /// <summary>RNG state after the frames rendered so far (noise waves draw one value per frame).</summary>
        public AudioRng Rng => _rng;
        /// <summary>True for noise waves: they draw exactly one RNG value per rendered frame.</summary>
        public bool Noisy => _p.SynthWave == SynthWave.Noise || _p.SynthWave == SynthWave.Pink;

        /// <summary>Adds the next (up to) frames samples into Dst. Returns the frames written.</summary>
        public int Advance(int frames)
        {
            int count = Math.Min(frames, Max - _i);
            if (count <= 0) return 0;
            int begin = _i, end = begin + count;
            float[] dst = Dst;
            int offset = _offset, n = _n, fadeN = _fadeN;
            float hpC = _hpC, sweepT = _sweepT, logR1 = _logR1, logR2 = _logR2, lpQ = _lpQ, bpQ = _bpQ, gain = _gain;
            float pitchTau = _pitchTau, lpEnvTau = _lpEnvTau, relTau = _relTau, invDur = _invDur;
            bool sweep = _sweep, then = _then, lpOn = _lpOn, bpOn = _bpOn;
            AudioOsc osc = _osc, mod = _mod;
            AudioSvf lp = _lp, bp = _bp;
            AudioOnePole hp = _hp;
            AudioPinkNoise pink = _pink;
            AudioRng rng = _rng;
            ref readonly SynthPatch p = ref _p;

            for (int i = begin; i < end; i++)
            {
                float t = i * Synth.Dt;
                float f = p.Freq;
                if (sweep)
                {
                    if (then && t > sweepT)
                    {
                        float u2 = (t - sweepT) / Math.Max(1e-4f, p.Sweep2Time);
                        if (u2 > 1f) u2 = 1f;
                        f = p.LinearSweep ? p.FreqEnd + (p.Freq2 - p.FreqEnd) * u2 : p.FreqEnd * (float)Math.Exp(logR2 * u2);
                    }
                    else
                    {
                        float u = t / sweepT;
                        if (u > 1f) u = 1f;
                        f = p.LinearSweep ? p.Freq + (p.FreqEnd - p.Freq) * u : p.Freq * (float)Math.Exp(logR1 * u);
                    }
                }
                if (p.PitchEnvSemis != 0f) f *= (float)Math.Pow(2.0, p.PitchEnvSemis * Math.Exp(-t / pitchTau) / 12.0);
                if (p.VibSemis != 0f)
                {
                    float d = p.VibSemis;
                    if (p.VibTau > 0f) d *= (float)Math.Exp(-t / p.VibTau);
                    f *= 1f + 0.05946f * d * (float)Math.Sin(Synth.TwoPi * p.VibHz * t);
                }

                float v;
                switch (p.SynthWave)
                {
                    case SynthWave.Sine:
                        if (p.FmIndex > 0f)
                        {
                            float idx = p.FmIndex * (p.FmTau > 0f ? (float)Math.Exp(-t / p.FmTau) : 1f);
                            v = osc.SinePM(f, idx * mod.Sine(f * p.FmRatio));
                        }
                        else v = osc.Sine(f);
                        break;
                    case SynthWave.Triangle: v = osc.Tri(f); break;
                    case SynthWave.Saw: v = osc.Saw(f); break;
                    case SynthWave.Square: v = osc.Square(f, p.Pw > 0f ? p.Pw : 0.5f); break;
                    case SynthWave.Pulse:
                        {
                            float pw = (p.Pw > 0f ? p.Pw : 0.5f) + p.PwmDepth * (float)Math.Sin(Synth.TwoPi * p.PwmHz * t);
                            v = osc.Square(f, Synth.Clamp(pw, 0.05f, 0.95f));
                            break;
                        }
                    case SynthWave.Pink: v = pink.Next(ref rng) * 2.5f; break;
                    default: v = rng.Signed(); break;
                }

                if (lpOn)
                {
                    float c = p.LpHz;
                    if (p.LpEndHz > 0f) c = Synth.ExpLerp(p.LpHz, p.LpEndHz, t * invDur);
                    if (p.LpEnvHz > 0f) c += p.LpEnvHz * (float)Math.Exp(-t / lpEnvTau);
                    lp.Set(c, lpQ);
                    v = lp.Low(v);
                }
                if (bpOn)
                {
                    bp.Set(p.BpEndHz > 0f ? Synth.ExpLerp(p.BpHz, p.BpEndHz, t * invDur) : p.BpHz, bpQ);
                    v = bp.Band(v);
                }
                if (hpC > 0f) v = hp.HP(v, hpC);
                if (p.Drive > 0f) v = Synth.SoftClip(v * p.Drive);

                float a;
                if (t < p.Attack) a = t / p.Attack;
                else
                {
                    float td = t - p.Attack - p.Hold;
                    a = td <= 0f || p.Decay <= 0f ? 1f : p.Sustain + (1f - p.Sustain) * (float)Math.Exp(-td / p.Decay);
                }
                if (p.Gate > 0f && t > p.Gate) a *= (float)Math.Exp(-(t - p.Gate) / relTau);
                if (p.TremDepth > 0f) a *= 1f - p.TremDepth * (0.5f + 0.5f * (float)Math.Sin(Synth.TwoPi * p.TremHz * t));
                if (i > n - fadeN) a *= (float)(n - i) / fadeN;

                v *= a * gain;
                if (p.Crush > 0f) v = (float)Math.Round(v * p.Crush) / p.Crush;
                dst[offset + i] += v;
            }
            _osc = osc; _mod = mod; _lp = lp; _bp = bp; _hp = hp; _pink = pink; _rng = rng;
            _i = end;
            return count;
        }
    }

    /// <summary>Renders patches into buffers + a few special generators (grains, creaks, layering).
    /// While a SynthPlan records (SfxRecipes.Plan), Render and Layer are deferred into the plan instead.</summary>
    public static class SynthGen
    {
        /// <summary>Adds the patch into dst starting at frame offset. Returns frames written.</summary>
        public static int Render(float[] dst, int offset, in SynthPatch p, ref AudioRng rng)
        {
            var plan = SynthPlan.Recording;
            if (plan != null) return plan.AddPatch(dst, offset, p, ref rng);
            var c = new SynthPatchCursor(dst, offset, p, rng);
            if (c.Max <= 0) return 0;
            c.Advance(c.Max);
            rng = c.Rng;
            return c.Max;
        }

        /// <summary>Adds the patch at a time in seconds.</summary>
        public static int At(float[] dst, float seconds, in SynthPatch p, ref AudioRng rng) => Render(dst, Synth.ToFrames(seconds), p, ref rng);

        /// <summary>Random short pings/grains (glass shards, crackles, debris, sparkles).</summary>
        public static void Grains(float[] b, float start, float dur, int count, float fMin, float fMax, float tauMin, float tauMax,
                                  float gain, bool noisy, ref AudioRng r, float fadeTau = 0f)
        {
            for (int k = 0; k < count; k++)
            {
                float rel = r.Value() * dur;
                float g = gain * (0.4f + 0.6f * r.Value()) * (fadeTau > 0f ? (float)Math.Exp(-rel / fadeTau) : 1f);
                float tau = r.Range(tauMin, tauMax);
                float f = Synth.ExpLerp(fMin, fMax, r.Value());
                var p = noisy ? SynthPatch.Make(SynthWave.Noise, 0f, tau * 6f + 0.005f, g).Bp(f, 3f).Env(0.0005f, tau)
                              : SynthPatch.Make(SynthWave.Sine, f, tau * 6f + 0.005f, g).Env(0.0005f, tau);
                At(b, start + rel, p, ref r);
            }
        }

        /// <summary>Stick-slip friction creak (wood, rubber bands, hinges): jittered impulse train into resonators.</summary>
        public static void Creak(float[] b, float start, float dur, float rate0, float rate1, float res0, float res1, float q, float gain, ref AudioRng r)
        {
            int n = Synth.Frames(dur);
            var tmp = new float[n];
            AudioSvf f1 = default, f2 = default;
            float next = 0f, env = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i * Synth.Dt, u = t / dur;
                float x = 0f;
                if (t >= next)
                {
                    x = 0.5f + 0.5f * r.Value();
                    float rate = Synth.Lerp(rate0, rate1, u);
                    next = t + (0.6f + 0.8f * r.Value()) / Math.Max(1f, rate);
                }
                if ((i & 15) == 0)                     // resonance + envelope move slowly: update every 16 samples (cost)
                {
                    float res = Synth.ExpLerp(res0, res1, u);
                    f1.Set(res, q);
                    f2.Set(res * 2.37f, q);
                    env = (float)Math.Sqrt(Math.Max(0.0, Math.Sin(Math.PI * Math.Min(1f, u))));
                }
                float y = f1.Band(x) + 0.5f * f2.Band(x);
                tmp[i] = y * env;
            }
            Layer(b, start, tmp, gain);
        }

        /// <summary>Mixes a separately rendered layer, scaled so its peak equals peakGain.</summary>
        public static void Layer(float[] b, float start, float[] layer, float peakGain)
        {
            var plan = SynthPlan.Recording;
            if (plan != null) { plan.AddLayer(b, start, layer, peakGain); return; }
            float pk = AudioFx.Peak(layer);
            if (pk < 1e-9f) return;
            float g = peakGain / pk;
            int o = Synth.ToFrames(start);
            for (int i = 0; i < layer.Length && o + i < b.Length; i++) b[o + i] += layer[i] * g;
        }
    }
}
