// =====================================================================================
//  SHIFTERS - VoicePresets.cs   [owned by the AUDIO agent]
//  Gibberish voice presets for the campaign's non-intern speakers (CAMPAIGN 2.1/2.2/18.8):
//    Villain    Max: pitch 0.62 = deep pompous megaphone voice (rise-and-fall contour, slow
//               vibrato, band-passed + driven). Pitch 1.45 = his REAL voice when the ME-CHA's
//               modulator breaks: squeaky, fast, nervous, no megaphone. Anything in between blends.
//    Percy      the coffee machine: gurgly bubbling voice + a steam-whistle "fweee" at the end.
//    Ghost      polite bedsheet ghosts: breathy whisper with a big reverb tail.
//    Announcer  ME INDUSTRIES PA ("UNAUTHORIZED WAVE DETECTED"): flat, band-passed, echo.
//    Robot      guard bots / ME-TV computers: monotone, bit-crushed.
//  Rendering is a VoiceRenderTask: one syllable (or one 8k-frame post-fx chunk) per MoveNext,
//  so a 3 s line never costs more than ~1 ms in one go (WebGL: no threads). Pure math.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;

namespace Shifters
{
    /// <summary>GhostMild = the Ghost under SPOOKY: MILD (soft voiced "ooo" instead of a whisper; AudioManager swaps it).</summary>
    public enum VoicePreset { Villain, Percy, Ghost, Announcer, Robot, GhostMild }

    public static class VoicePresets
    {
        public const float VillainDeep = 0.62f, VillainSqueaky = 1.45f;
        /// <summary>Longest preset babble (story lines are up to 90 characters).</summary>
        public const float MaxSeconds = 3.2f;
        public static readonly int Count = Enum.GetValues(typeof(VoicePreset)).Length;

        public static float DefaultPitch(VoicePreset p) => p == VoicePreset.Villain ? VillainDeep : 1f;

        /// <summary>Speaker id / name -> preset (case-insensitive). "max", "villain", "me-cha", "boss" -> Villain 0.62;
        /// add "squeak", "real", "unmasked" or "broken" -> Villain 1.45; "percy"/"coffee" -> Percy; "ghost"/"sheet" ->
        /// Ghost; "pa", "announcer", "klaxon", "hr", "intercom", "speaker" -> Announcer; "robot", "guard", "bot", "computer",
        /// "me-tv" -> Robot. Anything else (interns, tokens like {BRUTE}) returns false.</summary>
        public static bool TryResolveSpeaker(string speakerId, out VoicePreset preset, out float pitch)
        {
            preset = VoicePreset.Villain; pitch = 1f;
            if (string.IsNullOrEmpty(speakerId)) return false;
            string s = speakerId.ToLowerInvariant();
            if (Has(s, "percy") || Has(s, "coffee") || Has(s, "percolator")) { preset = VoicePreset.Percy; return true; }
            if (Has(s, "ghost") || Has(s, "sheet")) { preset = VoicePreset.Ghost; return true; }
            if (s == "max" || s.StartsWith("max_") || s.StartsWith("max ") || s.StartsWith("max-") || Has(s, "villain") || Has(s, "me-cha") || Has(s, "mecha")
                || Has(s, "maximilian") || s == "boss" || Has(s, "ceo"))
            {
                preset = VoicePreset.Villain;
                pitch = Has(s, "squeak") || Has(s, "real") || Has(s, "unmask") || Has(s, "broken") || Has(s, "small") ? VillainSqueaky : VillainDeep;
                return true;
            }
            if (s == "pa" || s == "hr" || Has(s, "announcer") || Has(s, "klaxon") || Has(s, "intercom") || Has(s, "speaker") || Has(s, "narrator"))
            { preset = VoicePreset.Announcer; return true; }
            if (Has(s, "robot") || Has(s, "guard") || s.EndsWith("bot") || Has(s, "computer") || Has(s, "me-tv") || Has(s, "metv"))
            { preset = VoicePreset.Robot; return true; }
            return false;
        }

        static bool Has(string s, string part) => s.IndexOf(part, StringComparison.Ordinal) >= 0;

        public static VoiceSynth.VoiceParams Params(VoicePreset p, float pitch)
        {
            if (pitch <= 0f) pitch = DefaultPitch(p);
            pitch = Synth.Clamp(pitch, 0.4f, 2f);
            switch (p)
            {
                case VoicePreset.Villain:
                    {
                        float t = Synth.Clamp01((pitch - VillainDeep) / (VillainSqueaky - VillainDeep));   // 0 deep .. 1 squeaky
                        return new VoiceSynth.VoiceParams
                        {
                            F0 = 160f * pitch, FormantScale = Synth.Lerp(0.9f, 1.22f, t), Breath = 0.05f, Jitter = Synth.Lerp(0.012f, 0.03f, t),
                            Rate = Synth.Lerp(0.82f, 1.45f, t), Drive = Synth.Lerp(1.5f, 0f, t), Sub = Synth.Lerp(0.18f, 0f, t), Q = Synth.Lerp(8f, 11f, t),
                            Nasal = Synth.Lerp(0.1f, 0.7f, t), Wobble = Synth.Lerp(0.35f, 0.2f, t)
                        };
                    }
                case VoicePreset.Percy:
                    return new VoiceSynth.VoiceParams { F0 = 135f * pitch, FormantScale = 0.86f, Breath = 0.07f, Jitter = 0.02f, Rate = 1.1f, Nasal = 0.35f, Q = 5f, Gurgle = 0.85f };
                case VoicePreset.Ghost:
                    return new VoiceSynth.VoiceParams { F0 = 190f * pitch, FormantScale = 1.05f, Breath = 0.25f, Jitter = 0.01f, Rate = 0.72f, Q = 9f, Whisper = 0.88f };
                case VoicePreset.GhostMild:
                    return new VoiceSynth.VoiceParams { F0 = 220f * pitch, FormantScale = 1.08f, Breath = 0.12f, Jitter = 0.01f, Rate = 0.8f, Q = 9f, Whisper = 0.25f, Nasal = 0.3f };
                case VoicePreset.Announcer:
                    return new VoiceSynth.VoiceParams { F0 = 118f * pitch, FormantScale = 0.97f, Breath = 0.03f, Jitter = 0.004f, Rate = 0.95f, Drive = 1.2f, Q = 9f };
                default:
                    return new VoiceSynth.VoiceParams { F0 = 110f * pitch, FormantScale = 1f, Breath = 0.01f, Jitter = 0f, Rate = 1.05f, Q = 13f, Nasal = 0.3f };
            }
        }

        /// <summary>Preset babble syllables for a text length (≈ one syllable per 3.3 characters, 2..30).</summary>
        public static int SyllablesForText(string text)
        {
            int len = string.IsNullOrEmpty(text) ? 6 : text.Length;
            return Math.Max(2, Math.Min(30, (int)Math.Round(len / 3.3f)));
        }

        /// <summary>Is the villain pitch in the "megaphone" range (deep voice through the ME-CHA modulator)?</summary>
        public static bool Megaphone(VoicePreset p, float pitch) => p == VoicePreset.Villain && (pitch <= 0f ? VillainDeep : pitch) < 1f;

        static readonly VoiceSynth.Cons[] VillainCons = { VoiceSynth.Cons.M, VoiceSynth.Cons.B, VoiceSynth.Cons.D, VoiceSynth.Cons.H, VoiceSynth.Cons.W, VoiceSynth.Cons.N, VoiceSynth.Cons.G, VoiceSynth.Cons.None };
        static readonly VoiceSynth.Cons[] PercyCons = { VoiceSynth.Cons.B, VoiceSynth.Cons.P, VoiceSynth.Cons.L, VoiceSynth.Cons.G, VoiceSynth.Cons.W, VoiceSynth.Cons.None };
        static readonly VoiceSynth.Cons[] GhostCons = { VoiceSynth.Cons.H, VoiceSynth.Cons.W, VoiceSynth.Cons.Sh, VoiceSynth.Cons.S, VoiceSynth.Cons.F, VoiceSynth.Cons.None };
        static readonly VoiceSynth.Cons[] FlatCons = { VoiceSynth.Cons.T, VoiceSynth.Cons.D, VoiceSynth.Cons.K, VoiceSynth.Cons.N, VoiceSynth.Cons.S, VoiceSynth.Cons.B, VoiceSynth.Cons.P };

        static VoiceSynth.Syllable Syl(VoiceSynth.Cons on, VoiceSynth.Vowel v0, VoiceSynth.Vowel v1, float dur, float p0, float p1, float gain = 1f, float gap = 0.02f)
            => new VoiceSynth.Syllable { Onset = on, V0 = v0, V1 = v1, Dur = dur, P0 = p0, P1 = p1, Gain = gain, Gap = gap };

        /// <summary>Gibberish for a story line. ending: 0 statement, 1 question, 2 exclamation (VoiceSynth.EndingForText).</summary>
        public static VoiceRenderTask Chatter(VoicePreset p, float pitch, int syllables, int variant, int ending)
        {
            if (pitch <= 0f) pitch = DefaultPitch(p);
            var vp = Params(p, pitch);
            var r = new AudioRng(AudioRng.Hash(7000 + (int)p * 131 + syllables, variant, ending * 11 + (int)(pitch * 1000f)));
            var syl = new List<VoiceSynth.Syllable>(syllables + 1);
            float baseDur = 0.1f / Math.Max(0.3f, vp.Rate), total = 0f;
            for (int i = 0; i < syllables; i++)
            {
                float u = syllables <= 1 ? 1f : (float)i / (syllables - 1);
                bool last = i == syllables - 1;
                VoiceSynth.Syllable s;
                var v0 = (VoiceSynth.Vowel)r.Range(0, 6);
                var v1 = r.Chance(0.3f) ? (VoiceSynth.Vowel)r.Range(0, 6) : v0;
                switch (p)
                {
                    case VoicePreset.Villain:
                        {   // pompous arc over the phrase; every third syllable is a stressed, held, vibrato-ed "MAAAH"
                            bool stress = i % 3 == 0;
                            float arc = 1f + 0.22f * (float)Math.Sin(Math.PI * u) - 0.08f * u;
                            float pp = arc * (stress ? 1.12f : 0.97f) * (1f + r.Range(-0.04f, 0.04f));
                            s = Syl(VillainCons[r.Range(0, VillainCons.Length)], stress ? VoiceSynth.Vowel.A : v0, stress ? VoiceSynth.Vowel.O : v1,
                                    baseDur * (stress ? 1.7f : r.Range(0.85f, 1.1f)), pp, pp * (stress ? 0.94f : 1.0f), stress ? 1.05f : 0.9f, stress ? 0.04f : 0.015f);
                            if (stress) { s.PMid = pp * 1.08f; s.Vib = 0.25f; }
                            break;
                        }
                    case VoicePreset.Percy:
                        {   // jumpy little "bloop" syllables
                            float pp = r.Range(0.88f, 1.3f);
                            var vv = r.Chance(0.6f) ? (r.Chance(0.5f) ? VoiceSynth.Vowel.O : VoiceSynth.Vowel.U) : v0;
                            s = Syl(PercyCons[r.Range(0, PercyCons.Length)], vv, vv, baseDur * r.Range(0.7f, 1.15f), pp, pp * r.Range(1.05f, 1.25f), 0.95f, r.Chance(0.3f) ? 0.05f : 0.015f);
                            break;
                        }
                    case VoicePreset.Ghost:
                    case VoicePreset.GhostMild:
                        {   // long, drifting, falling "whooo"
                            float pp = Synth.Lerp(1.08f, 0.9f, u) * (1f + r.Range(-0.05f, 0.05f));
                            var vv = r.Chance(0.55f) ? VoiceSynth.Vowel.U : v0;
                            s = Syl(GhostCons[r.Range(0, GhostCons.Length)], vv, r.Chance(0.4f) ? VoiceSynth.Vowel.O : vv, baseDur * r.Range(1.1f, 1.6f), pp, pp * 0.95f, 0.9f, 0.04f);
                            break;
                        }
                    case VoicePreset.Announcer:
                        {
                            float pp = Synth.Lerp(1.04f, 0.96f, u);
                            s = Syl(FlatCons[r.Range(0, FlatCons.Length)], v0, v1, baseDur * r.Range(0.9f, 1.1f), pp, pp, 1f, i % 4 == 3 ? 0.06f : 0.015f);
                            break;
                        }
                    default:
                        s = Syl(FlatCons[r.Range(0, FlatCons.Length)], v0, v0, baseDur, 1f, 1f, 1f, 0.025f);
                        break;
                }
                if (last)
                {
                    s.Dur *= p == VoicePreset.Villain ? 2.0f : 1.5f;
                    if (ending == 1) { s.P0 = 1f; s.P1 = 1.35f; s.PMid = 0f; }
                    else if (ending == 2) { s.P0 = 1.3f; s.P1 = 1.12f; s.Gain = 1.1f; s.PMid = 0f; }
                    else { s.P0 = 1f; s.P1 = p == VoicePreset.Robot ? 1f : 0.82f; s.PMid = 0f; }
                    if (p == VoicePreset.Villain) s.Vib = 0.35f;
                }
                if (total + s.Dur + s.Gap > MaxSeconds - 0.1f) break;
                total += s.Dur + s.Gap;
                syl.Add(s);
            }
            return new VoiceRenderTask(p, pitch, vp, syl, r);
        }

        /// <summary>A canned VoiceLine (Laugh, Gasp, Denied, Victory, Taunt...) in a preset's voice.</summary>
        public static VoiceRenderTask Line(VoicePreset p, float pitch, VoiceLine line, int variant)
        {
            if (pitch <= 0f) pitch = DefaultPitch(p);
            var vp = Params(p, pitch);
            var r = new AudioRng(AudioRng.Hash(9000 + (int)p * 31 + (int)line, variant, (int)(pitch * 1000f)));
            var syl = new List<VoiceSynth.Syllable>(8);
            VoiceSynth.LineSyllables(line, ref r, syl, vp);
            return new VoiceRenderTask(p, pitch, vp, syl, r);
        }

        public static float[] RenderChatter(VoicePreset p, float pitch, int syllables, int variant, int ending) => Chatter(p, pitch, syllables, variant, ending).RunToEnd();
        public static float[] RenderLine(VoicePreset p, float pitch, VoiceLine line, int variant) => Line(p, pitch, line, variant).RunToEnd();
    }

    /// <summary>Incremental preset-voice render: 1st MoveNext allocates, then one syllable per MoveNext, then the
    /// preset's post effects in 8192-frame chunks, then normalization. Result is set when MoveNext returns false.</summary>
    public sealed class VoiceRenderTask : IEnumerator
    {
        public const int PostChunk = 8192;
        readonly VoicePreset _preset;
        readonly float _pitch;
        readonly VoiceSynth.VoiceParams _vp;
        readonly List<VoiceSynth.Syllable> _syl;
        AudioRng _r;
        VoiceSynth.VoiceState _st;
        float[] _b;
        int _next = -1, _pos, _post;
        bool _megaphone;
        AudioOnePole _hp1, _hp2, _lp1, _lp2;
        AudioReverb _rv;
        AudioEchoState _echo;
        float _hpC, _lpC;

        public float[] Result { get; private set; }
        public int Slices { get; private set; }
        public object Current => null;

        public VoiceRenderTask(VoicePreset preset, float pitch, in VoiceSynth.VoiceParams vp, List<VoiceSynth.Syllable> syl, AudioRng r)
        {
            _preset = preset; _pitch = pitch; _vp = vp; _syl = syl; _r = r;
        }

        float TailSeconds => _preset == VoicePreset.Ghost || _preset == VoicePreset.GhostMild ? 0.7f : _preset == VoicePreset.Percy ? 0.4f : _preset == VoicePreset.Announcer ? 0.5f : 0.05f;

        public bool MoveNext()
        {
            if (Result != null) return false;
            Slices++;
            if (_next < 0)
            {
                _b = new float[VoiceSynth.FramesFor(_syl, TailSeconds)];
                _st = VoiceSynth.VoiceState.Make(_r.Range(0f, 6.2832f));
                _pos = Synth.Frames(0.01f);
                _next = 0;
                return true;
            }
            if (_next < _syl.Count)
            {
                _pos = VoiceSynth.RenderSyllable(_vp, _syl[_next], _b, _pos, ref _r, ref _st);
                _next++;
                if (_next == _syl.Count && _preset == VoicePreset.Percy) Whistle();
                return true;
            }
            if (_post == 0) BeginPost();
            if (_post < _b.Length)
            {
                int to = Math.Min(_b.Length, _post + PostChunk);
                PostRange(_post, to);
                _post = to;
                return true;
            }
            if (_preset == VoicePreset.Robot) AudioFx.Crush(_b, 2, 14f);
            VoiceSynth.Finish(_b);
            Result = _b;
            return false;
        }

        public float[] RunToEnd() { while (MoveNext()) { } return Result; }

        public void Reset() { }

        void Whistle()
        {   // Percy's steam-whistle punctuation
            float at = Math.Min(_pos * Synth.Dt, _b.Length * Synth.Dt - 0.36f);
            var w = SynthPatch.Make(SynthWave.Sine, 1950f, 0.34f, 0.45f).Sweep(2350f, 0.05f).Vib(11f, 0.5f).Env(0.015f, 0f).Rel(0.26f, 0.06f);
            SynthGen.At(_b, Math.Max(0f, at), w, ref _r);
            var n = SynthPatch.Make(SynthWave.Noise, 0f, 0.34f, 0.2f).Bp(2600f, 2f).Env(0.015f, 0f).Rel(0.26f, 0.06f);
            SynthGen.At(_b, Math.Max(0f, at), n, ref _r);
        }

        void BeginPost()
        {
            _megaphone = VoicePresets.Megaphone(_preset, _pitch) || _preset == VoicePreset.Announcer;
            _hpC = AudioOnePole.Coef(_preset == VoicePreset.Announcer ? 450f : 330f);
            _lpC = AudioOnePole.Coef(_preset == VoicePreset.Announcer ? 3000f : 3600f);
            if (_preset == VoicePreset.Ghost || _preset == VoicePreset.GhostMild) _rv = new AudioReverb(0.88f, 0.5f, 1f, 0);
            if (_preset == VoicePreset.Announcer) _echo = new AudioEchoState(0.13f, 0.32f, 0.35f);
        }

        void PostRange(int from, int to)
        {
            if (_megaphone)
            {
                for (int i = from; i < to; i++)
                {
                    float x = _b[i];
                    x = _hp1.HP(x, _hpC); x = _hp2.HP(x, _hpC);
                    x = _lp1.LP(x, _lpC); x = _lp2.LP(x, _lpC);
                    _b[i] = Synth.SoftClip(x * 2.2f);
                }
            }
            if (_rv != null) AudioFx.ReverbRange(_b, _rv, 0.55f, from, to);
            if (_preset == VoicePreset.Announcer) _echo.Process(_b, from, to);
        }
    }
}
