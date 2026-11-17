// =====================================================================================
//  SHIFTERS - VoiceSynth.cs   [owned by the AUDIO agent]
//  Gibberish speech a la Animal Crossing / Banjo-Kazooie: syllables = optional consonant
//  (noise bursts, nasal murmurs, glides) + vowel (3 band-pass formants over a glottal saw/
//  pulse source with a pitch contour). Per-character pitch (Profile.VoicePitch) and timbre
//  by archetype (Brute gruff/low, Sprinter quick/high, Brainiac nasal). Per VoiceLine
//  contours ("wheee" rises, "nooo" falls, grunts strain...). Thread-safe, deterministic.
// =====================================================================================
using System;
using System.Collections.Generic;

namespace Shifters
{
    public static class VoiceSynth
    {
        /// <summary>Hard cap for speech-bubble chatter.</summary>
        public const float MaxChatterSeconds = 1.5f;

        public enum Vowel { A, E, I, O, U, Uh }
        public enum Cons { None, B, D, G, P, T, K, S, Sh, F, H, M, N, L, W, Y }

        // F1, F2, F3 (Hz) for A E I O U Uh
        static readonly float[,] Formants =
        {
            { 800f, 1200f, 2500f }, { 520f, 1750f, 2450f }, { 300f, 2250f, 3000f },
            { 500f, 900f, 2400f }, { 330f, 850f, 2250f }, { 620f, 1250f, 2400f }
        };

        public struct Syllable
        {
            public Cons Onset, Coda;
            public Vowel V0, V1;            // vowel glides V0 -> V1
            public float Dur;               // seconds (consonants included)
            public float P0, P1, PMid;      // pitch multipliers (PMid > 0 => rise-fall curve)
            public float Gain, Gap;         // gap = silence after the syllable
            public float Vib;               // vibrato depth (semitones)
        }

        /// <summary>Per-character voice parameters.</summary>
        public struct VoiceParams
        {
            public float F0, FormantScale, Breath, Jitter, Nasal, Rate, Drive, Sub, Q;
            // campaign presets (VoicePresets.cs): 0 = off for the interns
            public float Whisper;   // 0..1 voiced -> pure breath
            public float Gurgle;    // 0..1 bubbly amplitude flutter + pitch wobble (Percy)
            public float Wobble;    // semitones of slow pompous vibrato on every syllable (Max)
        }

        public static VoiceParams ForCharacter(Archetype a, float pitch)
        {
            pitch = Synth.Clamp(pitch <= 0f ? 1f : pitch, 0.5f, 2f);
            switch (a)
            {
                case Archetype.Brute:
                    return new VoiceParams { F0 = 128f * pitch, FormantScale = 0.9f, Breath = 0.12f, Jitter = 0.03f, Rate = 0.88f, Drive = 1.8f, Sub = 0.3f, Q = 6f };
                case Archetype.Sprinter:
                    return new VoiceParams { F0 = 196f * pitch, FormantScale = 1.12f, Breath = 0.05f, Jitter = 0.01f, Rate = 1.3f, Q = 7f };
                default:
                    return new VoiceParams { F0 = 160f * pitch, FormantScale = 1.04f, Breath = 0.04f, Jitter = 0.008f, Rate = 1.05f, Nasal = 0.6f, Q = 11f };
            }
        }

        // ------------------------------------------------------------------ public renderers

        /// <summary>A canned voice line (Jump "hup", Thrown "wheee", Lost "nooo"...).</summary>
        public static float[] RenderLine(Archetype a, float pitch, VoiceLine line, int variant)
        {
            var vp = ForCharacter(a, pitch);
            var r = new AudioRng(AudioRng.Hash((int)a * 31 + (int)line, variant, (int)(pitch * 1000f)));
            var syl = new List<Syllable>(8);
            BuildLine(line, ref r, syl, vp);
            return Render(vp, syl, ref r);
        }

        /// <summary>Speech-bubble gibberish: syllable count from text length, capped at MaxChatterSeconds.
        /// ending: 0 statement (falls), 1 question (rises), 2 exclamation (high).</summary>
        public static float[] RenderChatter(Archetype a, float pitch, int syllables, int variant, int ending)
        {
            var vp = ForCharacter(a, pitch);
            var r = new AudioRng(AudioRng.Hash((int)a * 97 + syllables, variant, ending * 7 + (int)(pitch * 1000f)));
            var syl = new List<Syllable>(syllables + 1);
            float total = 0f;
            for (int i = 0; i < syllables; i++)
            {
                float u = syllables <= 1 ? 1f : (float)i / (syllables - 1);
                bool last = i == syllables - 1;
                var s = RandomSyllable(ref r, 0.095f / vp.Rate);
                float baseP = Synth.Lerp(1.1f, 0.95f, u) * (1f + r.Range(-0.09f, 0.09f));
                s.P0 = baseP; s.P1 = baseP * r.Range(0.96f, 1.04f);
                if (last)
                {
                    s.Dur *= 1.5f;
                    if (ending == 1) { s.P0 = 1f; s.P1 = 1.32f; }
                    else if (ending == 2) { s.P0 = 1.25f; s.P1 = 1.15f; s.Gain = 1.1f; }
                    else { s.P0 = 1f; s.P1 = 0.84f; }
                }
                if (total + s.Dur + s.Gap > MaxChatterSeconds - 0.08f) break;
                total += s.Dur + s.Gap;
                syl.Add(s);
            }
            return Render(vp, syl, ref r);
        }

        /// <summary>Speech-bubble text -> syllable count (≈ one syllable per 3.5 characters, 2..14).</summary>
        public static int SyllablesForText(string text)
        {
            int len = string.IsNullOrEmpty(text) ? 4 : text.Length;
            return Math.Max(2, Math.Min(14, (int)Math.Round(len / 3.5f)));
        }

        public static int EndingForText(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == '?') return 1;
                if (c == '!') return 2;
                if (char.IsLetterOrDigit(c)) return 0;
            }
            return 0;
        }

        // ------------------------------------------------------------------ line definitions

        static Syllable S(Cons on, Vowel v0, Vowel v1, float dur, float p0, float p1, Cons coda = Cons.None, float gain = 1f, float gap = 0.02f)
            => new Syllable { Onset = on, V0 = v0, V1 = v1, Dur = dur, P0 = p0, P1 = p1, Coda = coda, Gain = gain, Gap = gap };

        static readonly Cons[] Friendly = { Cons.H, Cons.W, Cons.Y, Cons.L, Cons.M, Cons.B, Cons.N, Cons.D };
        static readonly Cons[] AnyCons = { Cons.None, Cons.B, Cons.D, Cons.G, Cons.P, Cons.T, Cons.K, Cons.S, Cons.Sh, Cons.F, Cons.H, Cons.M, Cons.N, Cons.L, Cons.W, Cons.Y, Cons.B, Cons.M, Cons.L };
        static readonly Vowel[] Bright = { Vowel.E, Vowel.O, Vowel.A, Vowel.I };

        static Syllable RandomSyllable(ref AudioRng r, float dur)
        {
            var c = AnyCons[r.Range(0, AnyCons.Length)];
            var v = (Vowel)r.Range(0, 6);
            var v1 = r.Chance(0.3f) ? (Vowel)r.Range(0, 6) : v;
            var s = S(c, v, v1, dur * r.Range(0.85f, 1.2f), 1f, 1f, r.Chance(0.15f) ? Cons.N : Cons.None, r.Range(0.8f, 1f), r.Chance(0.25f) ? 0.05f : 0.015f);
            return s;
        }

        /// <summary>The syllables of a canned line for any voice (used by VoicePresets).</summary>
        public static void LineSyllables(VoiceLine line, ref AudioRng r, List<Syllable> o, in VoiceParams vp) => BuildLine(line, ref r, o, vp);

        static void BuildLine(VoiceLine line, ref AudioRng r, List<Syllable> o, in VoiceParams vp)
        {
            float k = 1f / vp.Rate;   // duration scale
            switch (line)
            {
                case VoiceLine.Greeting:
                    {
                        int n = 3 + r.Range(0, 2);
                        float[] contour = { 1.0f, 1.12f, 1.2f, 1.1f };
                        for (int i = 0; i < n; i++)
                        {
                            bool last = i == n - 1;
                            float p = contour[Math.Min(i, contour.Length - 1)];
                            o.Add(S(Friendly[r.Range(0, Friendly.Length)], Bright[r.Range(0, Bright.Length)], Bright[r.Range(0, Bright.Length)],
                                    (last ? 0.2f : 0.12f) * k, p, last ? p * 0.88f : p * 1.05f));
                        }
                        break;
                    }
                case VoiceLine.Selected:
                    {
                        int n = 2 + r.Range(0, 2);
                        for (int i = 0; i < n; i++)
                        {
                            bool last = i == n - 1;
                            float p = 1.05f + 0.15f * i;
                            o.Add(S(i == 0 ? Cons.Y : Friendly[r.Range(0, Friendly.Length)], i == 0 ? Vowel.E : Vowel.A, last ? Vowel.O : Vowel.A,
                                    (last ? 0.18f : 0.1f) * k, p, last ? p * 1.1f : p));
                        }
                        break;
                    }
                case VoiceLine.Jump:
                    o.Add(S(Cons.H, Vowel.Uh, Vowel.U, 0.11f * k, 1.1f, 1.25f, Cons.P, 1f, 0.01f));
                    break;
                case VoiceLine.Hurt:
                    o.Add(S(r.Chance(0.5f) ? Cons.None : Cons.W, Vowel.O, Vowel.U, 0.2f * k, 1.18f, 0.85f, Cons.F));
                    break;
                case VoiceLine.Scream:
                    {
                        var s = S(r.Chance(0.5f) ? Cons.None : Cons.W, Vowel.A, Vowel.O, 1.15f, 1.7f, 0.9f);
                        s.Vib = 0.45f; s.PMid = 1.75f;
                        o.Add(s);
                        break;
                    }
                case VoiceLine.Strain:
                    o.Add(S(Cons.N, Vowel.Uh, Vowel.Uh, 0.25f, 0.8f, 0.84f, Cons.None, 0.8f, 0.03f));
                    o.Add(S(Cons.N, Vowel.Uh, Vowel.A, 0.38f, 0.82f, 0.9f, Cons.H, 1f));
                    break;
                case VoiceLine.Thrown:
                    {
                        var s = S(Cons.W, Vowel.I, Vowel.I, 0.85f, 1.0f, 1.9f);
                        s.Vib = 0.3f;
                        o.Add(s);
                        break;
                    }
                case VoiceLine.KO:
                    o.Add(S(Cons.B, Vowel.E, Vowel.Uh, 0.36f * k, 1.0f, 0.7f, Cons.H));
                    break;
                case VoiceLine.Victory:
                    {
                        o.Add(S(Cons.Y, Vowel.A, Vowel.A, 0.13f * k, 1.2f, 1.3f, Cons.None, 1f, 0.01f));
                        var s = S(Cons.H, Vowel.U, Vowel.U, 0.45f, 1.5f, 1.3f);
                        s.PMid = 1.75f;
                        o.Add(s);
                        break;
                    }
                case VoiceLine.Taunt:
                    {
                        float[] p = { 1.2f, 1.0f, 1.2f, 1.0f };
                        float[] d = { 0.18f, 0.14f, 0.12f, 0.2f };
                        for (int i = 0; i < 4; i++) o.Add(S(Cons.N, Vowel.I, Vowel.E, d[i] * k, p[i], p[i], Cons.None, 1f, 0.03f));
                        break;
                    }
                case VoiceLine.Denied:
                    o.Add(S(Cons.N, Vowel.Uh, Vowel.Uh, 0.16f * k, 1.2f, 1.2f, Cons.None, 1f, 0.05f));
                    o.Add(S(Cons.None, Vowel.Uh, Vowel.Uh, 0.22f * k, 1.0f, 0.88f));
                    break;
                case VoiceLine.Idle:
                    {
                        int n = 3 + r.Range(0, 3);
                        Cons[] mum = { Cons.M, Cons.N, Cons.B, Cons.H };
                        Vowel[] mv = { Vowel.Uh, Vowel.U, Vowel.O };
                        for (int i = 0; i < n; i++)
                        {
                            float p = 0.95f + r.Range(-0.04f, 0.04f);
                            o.Add(S(mum[r.Range(0, mum.Length)], mv[r.Range(0, mv.Length)], mv[r.Range(0, mv.Length)], 0.13f * k, p, p * 0.98f, Cons.None, 0.7f, 0.03f));
                        }
                        break;
                    }
                case VoiceLine.Laugh:
                    {
                        int n = 4 + r.Range(0, 2);
                        for (int i = 0; i < n; i++)
                        {
                            float p = 1.3f - 0.07f * i;
                            o.Add(S(Cons.H, Vowel.A, Vowel.A, 0.1f * k, p, p * 0.96f, Cons.None, 1f - 0.08f * i, 0.035f));
                        }
                        break;
                    }
                case VoiceLine.Lost:
                    {
                        var s = S(Cons.N, Vowel.O, Vowel.U, 1.45f, 1.3f, 0.7f);
                        s.Vib = 0.4f;
                        o.Add(s);
                        break;
                    }
                case VoiceLine.Gasp:   // sharp frightened intake: "HHAH!" rising
                    {
                        var g = S(Cons.H, Vowel.A, Vowel.Uh, 0.2f * k, 1.25f, 1.5f, Cons.None, 0.9f, 0.01f);
                        g.Vib = 0.2f;
                        o.Add(g);
                        break;
                    }
                default:
                    o.Add(RandomSyllable(ref r, 0.1f * k));
                    break;
            }
        }

        // ------------------------------------------------------------------ synthesis core

        static float ConsDuration(Cons c)
        {
            switch (c)
            {
                case Cons.None: return 0f;
                case Cons.S: case Cons.Sh: return 0.06f;
                case Cons.F: case Cons.H: return 0.045f;
                case Cons.M: case Cons.N: case Cons.L: case Cons.W: case Cons.Y: return 0.04f;
                default: return 0.03f; // plosives
            }
        }

        static float BurstHz(Cons c)
        {
            switch (c)
            {
                case Cons.P: case Cons.B: return 800f;
                case Cons.T: case Cons.D: return 3800f;
                case Cons.K: case Cons.G: return 2100f;
                case Cons.S: return 6000f;
                case Cons.Sh: return 2800f;
                case Cons.F: return 1600f;
                default: return 1500f;
            }
        }

        static bool Voiceless(Cons c) => c == Cons.P || c == Cons.T || c == Cons.K || c == Cons.S || c == Cons.Sh || c == Cons.F || c == Cons.H;
        static bool Plosive(Cons c) => c == Cons.P || c == Cons.T || c == Cons.K || c == Cons.B || c == Cons.D || c == Cons.G;

        /// <summary>Oscillator/filter state carried across syllables.</summary>
        public struct VoiceState
        {
            public AudioOsc Src, Sub;
            public AudioOnePole Tilt;
            public float TiltC, Jitter, Phase, GurglePhase;
            public static VoiceState Make(float phase = 0f) => new VoiceState { TiltC = AudioOnePole.Coef(2800f), Phase = phase };
        }

        /// <summary>Frames needed for a syllable list (plus extra tail seconds).</summary>
        public static int FramesFor(List<Syllable> syl, float extraSeconds = 0f)
        {
            float total = 0.06f + extraSeconds;
            for (int i = 0; i < syl.Count; i++) total += syl[i].Dur + syl[i].Gap;
            return Synth.Frames(total);
        }

        /// <summary>Renders one syllable at pos (adds into b); returns the next position.</summary>
        public static int RenderSyllable(in VoiceParams vp, in Syllable s, float[] b, int pos, ref AudioRng r, ref VoiceState st)
        {
            int n = Synth.Frames(s.Dur);
            float cd = Math.Min(ConsDuration(s.Onset), s.Dur * 0.4f);
            float coda = s.Coda == Cons.None ? 0f : Math.Min(0.05f, s.Dur * 0.3f);
            AudioSvf f1 = default, f2 = default, f3 = default, nas = default, cons = default;
            f1.Set(500f, vp.Q); f2.Set(1500f, vp.Q); f3.Set(2500f, vp.Q);
            nas.Set(260f, 2f);
            cons.Set(BurstHz(s.Onset), 2f);
            AudioSvf codaF = default;
            codaF.Set(BurstHz(s.Coda), 1.5f);
            float gl0x = 0f, gl1x = 0f, gl2x = 0f; // glide-in formant offsets for L/W/Y
            if (s.Onset == Cons.W) { gl0x = 330f; gl1x = 800f; gl2x = 2250f; }
            else if (s.Onset == Cons.Y) { gl0x = 280f; gl1x = 2300f; gl2x = 3000f; }
            else if (s.Onset == Cons.L) { gl0x = 380f; gl1x = 1300f; gl2x = 2700f; }
            for (int i = 0; i < n && pos + i < b.Length; i++)
            {
                float t = i * Synth.Dt;
                float u = t / s.Dur;
                // pitch contour
                float pm;
                if (s.PMid > 0f)
                {
                    float a = 1f - u;
                    pm = a * a * s.P0 + 2f * a * u * (2f * s.PMid - 0.5f * (s.P0 + s.P1)) + u * u * s.P1;
                }
                else pm = Synth.Lerp(s.P0, s.P1, u * u * (3f - 2f * u));
                if ((i & 63) == 0) st.Jitter = r.Signed() * vp.Jitter;
                float f0 = vp.F0 * pm * (1f + st.Jitter);
                if (vp.Wobble > 0f) f0 *= 1f + 0.0595f * vp.Wobble * (float)Math.Sin(Synth.TwoPi * 4.2f * t + st.Phase);
                if (vp.Gurgle > 0f)
                {
                    f0 *= 1f + 0.07f * vp.Gurgle * (float)Math.Sin(Synth.TwoPi * 9f * t + st.Phase);
                    st.GurglePhase += Synth.TwoPi * (17f + 6f * (float)Math.Sin(st.Phase + t * 3f)) * Synth.Dt;
                }
                if (s.Vib > 0f) f0 *= 1f + 0.0595f * s.Vib * (float)Math.Sin(Synth.TwoPi * 6f * t) * Synth.Clamp01(t * 3f);

                // glottal source: saw tilted down + breath (+ sub-octave growl for gruff voices)
                float g = st.Tilt.LP(st.Src.Saw(f0), st.TiltC) * 1.6f;
                if (vp.Sub > 0f) g += st.Sub.Square(f0 * 0.5f, 0.4f) * vp.Sub * 0.5f;
                float noise = r.Signed();
                g += noise * vp.Breath;
                if (vp.Drive > 0f) g = Synth.SoftClip(g * vp.Drive) * 0.8f;
                if (vp.Whisper > 0f) g = g * (1f - vp.Whisper) + noise * vp.Whisper * 1.3f;

                // formants (update every 16 samples)
                if ((i & 15) == 0)
                {
                    float vu = Synth.Clamp01((t - cd) / Math.Max(0.02f, s.Dur - cd));
                    int a0 = (int)s.V0, a1 = (int)s.V1;
                    float F1 = Synth.Lerp(Formants[a0, 0], Formants[a1, 0], vu);
                    float F2 = Synth.Lerp(Formants[a0, 1], Formants[a1, 1], vu);
                    float F3 = Synth.Lerp(Formants[a0, 2], Formants[a1, 2], vu);
                    if (gl0x > 0f && t < cd + 0.05f)
                    {
                        float gw = 1f - Synth.Clamp01(t / (cd + 0.05f));
                        F1 = Synth.Lerp(F1, gl0x, gw); F2 = Synth.Lerp(F2, gl1x, gw); F3 = Synth.Lerp(F3, gl2x, gw);
                    }
                    float sc = vp.FormantScale;
                    f1.Set(F1 * sc, vp.Q * 0.7f);
                    f2.Set(F2 * sc, vp.Q);
                    f3.Set(F3 * sc, vp.Q * 1.2f);
                }
                float voiced = f1.Band(g) * 1.0f + f2.Band(g) * 0.6f + f3.Band(g) * 0.28f;
                if (vp.Nasal > 0f) voiced = voiced * (1f - vp.Nasal * 0.35f) + nas.Band(g) * vp.Nasal * 0.6f + f3.Band(g) * vp.Nasal * 0.25f;

                // amplitude: consonant region, vowel, coda
                float amp;
                float vowelStart = cd;
                if (s.Onset == Cons.None) amp = Synth.Clamp01(t / 0.012f);
                else if (Plosive(s.Onset)) amp = t < cd * 0.5f ? 0f : Synth.Clamp01((t - cd * 0.5f) / 0.01f);
                else if (s.Onset == Cons.M || s.Onset == Cons.N) amp = t < cd ? 0.45f * Synth.Clamp01(t / 0.01f) : 1f;
                else if (Voiceless(s.Onset)) amp = Synth.Clamp01((t - cd) / 0.012f);
                else amp = Synth.Clamp01(t / 0.015f);
                float tEnd = s.Dur - coda;
                if (t > tEnd - 0.03f) amp *= Synth.Clamp01((s.Dur - t - coda * 0.3f) / 0.03f + (s.Coda == Cons.None ? 0f : 0f));
                if (s.Coda == Cons.P && t > tEnd) amp = 0f;

                if (vp.Gurgle > 0f) amp *= 1f - vp.Gurgle * 0.55f * (0.5f + 0.5f * (float)Math.Sin(st.GurglePhase));
                float y;
                if ((s.Onset == Cons.M || s.Onset == Cons.N) && t < vowelStart) y = nas.Band(g) * amp * 1.2f;
                else y = voiced * amp;

                // consonant noise
                if (s.Onset != Cons.None && Voiceless(s.Onset) && t < cd + 0.015f)
                {
                    float ce = s.Onset == Cons.H ? 0.5f : 1f;
                    float shape = Plosive(s.Onset) ? (float)Math.Exp(-Math.Max(0f, t - cd * 0.5f) / 0.008f) * (t >= cd * 0.5f ? 1f : 0f)
                                                   : (float)Math.Sin(Math.PI * Synth.Clamp01(t / (cd + 0.015f)));
                    y += (s.Onset == Cons.H ? f2.Band(noise) * 2f : cons.Band(noise) * 2.2f) * shape * ce * 0.5f;
                }
                else if (Plosive(s.Onset) && t >= cd * 0.5f && t < cd * 0.5f + 0.012f)
                    y += cons.Band(noise) * 1.2f * (float)Math.Exp(-(t - cd * 0.5f) / 0.004f);

                if (s.Coda != Cons.None && t > tEnd)
                {
                    float ct = (t - tEnd) / Math.Max(0.005f, coda);
                    if (s.Coda == Cons.F || s.Coda == Cons.H || s.Coda == Cons.S)
                        y += codaF.Band(noise) * 0.9f * (1f - ct);
                    else if (s.Coda == Cons.N || s.Coda == Cons.M)
                        y += nas.Band(g) * (1f - ct);
                    else if (s.Coda == Cons.P && ct < 0.4f && ct > 0.25f)
                        y += codaF.Band(noise) * 0.8f;
                }
                b[pos + i] += y * s.Gain;
            }
            return pos + n + Synth.Frames(s.Gap);
        }

        /// <summary>Final clean-up of a rendered voice buffer.</summary>
        public static void Finish(float[] b)
        {
            AudioFx.DcBlock(b);
            AudioFx.FadeOut(b, 0.01f);
            AudioFx.Normalize(b);
        }

        static float[] Render(in VoiceParams vp, List<Syllable> syl, ref AudioRng r)
        {
            var b = new float[FramesFor(syl)];
            var st = VoiceState.Make();
            int pos = Synth.Frames(0.01f);
            for (int si = 0; si < syl.Count; si++) pos = RenderSyllable(vp, syl[si], b, pos, ref r, ref st);
            Finish(b);
            return b;
        }
    }
}
