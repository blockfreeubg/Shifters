// =====================================================================================
//  SHIFTERS - MusicSongs.cs   [owned by the AUDIO agent]
//  The soundtrack, composed in code (real scales & progressions, 8-bar seamless loops):
//    Menu      100 BPM  D dorian lounge (Dm9-G9 vamp, Bbmaj7-A7b9), EP + funk bass + vibes
//    Training  124 BPM  F major elevator jazz, I-vi-ii-V, walking upright bass, swung ride, vibes
//    Factory   108 BPM  E phrygian clank groove, distorted riff, metal percussion, steam, organ stabs
//    Lab        96 BPM  A harmonic minor, heartbeat kick, pumping pulse bass, pad, gliding theremin
//    Research  132 BPM  7/8 (3+2+2) D lydian arpeggios, woodblocks, whole-tone bell runs
//    Chaos     140 BPM  C minor four-on-the-floor, "wee-oo" alarm lead, frantic arps, stabs
//    Victory   116 BPM  C major brass fanfare + celebration groove, march snare, tom fills
// =====================================================================================
using System;
using System.Collections.Generic;

namespace Shifters
{
    public static partial class MusicSongs
    {
        public static SongSpec Build(MusicId id)
        {
            switch (id)
            {
                case MusicId.Menu: return Menu();
                case MusicId.Training: return Training();
                case MusicId.Factory: return Factory();
                case MusicId.Lab: return Lab();
                case MusicId.Research: return Research();
                case MusicId.Chaos: return Chaos();
                case MusicId.Victory: return Victory();
                default: return BuildCampaign(id);   // MusicSongs.Campaign.cs
            }
        }

        // ------------------------------------------------------------------ theory helpers
        struct Chord { public int Root, Bass; public int[] Iv; }
        static Chord C(int root, int[] iv, int bass = -1) => new Chord { Root = root, Iv = iv, Bass = bass < 0 ? root : bass };

        static readonly int[] Maj = { 0, 4, 7 }, Min = { 0, 3, 7 }, Maj7 = { 0, 4, 7, 11 }, Min7 = { 0, 3, 7, 10 }, Dom7 = { 0, 4, 7, 10 };
        static readonly int[] Min9 = { 0, 3, 7, 10, 14 }, Dom9 = { 0, 4, 7, 10, 14 }, Dom7b9 = { 0, 4, 7, 10, 13 }, Sus47 = { 0, 5, 7, 10 };
        static readonly int[] Lyd = { 0, 4, 7, 18 }, Maj7s11 = { 0, 4, 7, 11, 18 };

        static int Place(int note, int lo) => lo + (((note - lo) % 12) + 12) % 12;

        static int[] Voicing(Chord c, int center)
        {
            var list = new List<int>(6);
            foreach (int iv in c.Iv)
            {
                int nn = c.Root + iv;
                while (nn < center - 6) nn += 12;
                while (nn >= center + 6) nn -= 12;
                if (!list.Contains(nn)) list.Add(nn);
            }
            list.Sort();
            return list.ToArray();
        }

        static float Vel(char ch) => ch == 'X' ? 1f : ch == 'x' ? 0.85f : ch == 'o' ? 0.55f : ch == 'g' ? 0.3f : 0f;

        static int SustainLen(string p, int at, int barEnd)
        {
            int len = 1;
            while (at + len < barEnd && p[at + len] == '-') len++;
            return len;
        }

        /// <summary>Drum pattern: 'X' accent, 'x' hit, 'o' soft, 'g' ghost, '.' rest. Pattern may span several bars.</summary>
        static void Drum(SongSpec s, MusicInst inst, string pat, float midi, float vel, float pan = 0f, int from = 0, int to = -1)
        {
            int spb = s.StepsPerBar;
            if (to < 0) to = s.Bars;
            int pBars = Math.Max(1, pat.Length / spb);
            for (int bar = from; bar < to; bar++)
            {
                int seg = ((bar - from) % pBars) * spb;
                for (int i = 0; i < spb && seg + i < pat.Length; i++)
                {
                    float v = Vel(pat[seg + i]);
                    if (v > 0f) s.Add(bar * spb + i, 1f, inst, midi, v * vel, pan);
                }
            }
        }

        /// <summary>Bass tokens per step: R root, g ghost root, 3/5/7 chord tones, 8 octave, b/u chromatic approach to the
        /// next bar's root from below/above, p minor 2nd, f tritone, h +2, 4 +5, 6 +9, '-' sustain, '.' rest.</summary>
        static void Bass(SongSpec s, MusicInst inst, Chord[] ch, string pat, int lo, float vel, float gate = 0.85f, int from = 0, int to = -1)
        {
            int spb = s.StepsPerBar;
            if (to < 0) to = s.Bars;
            int pBars = Math.Max(1, pat.Length / spb);
            for (int bar = from; bar < to; bar++)
            {
                var c = ch[bar % ch.Length];
                var nx = ch[(bar + 1) % ch.Length];
                int root = Place(c.Bass, lo), nroot = Place(nx.Bass, lo);
                int third = c.Iv.Length > 1 ? c.Iv[1] : 4, fifth = c.Iv.Length > 2 ? c.Iv[2] : 7, sev = c.Iv.Length > 3 ? c.Iv[3] : 10;
                int seg = ((bar - from) % pBars) * spb;
                for (int i = 0; i < spb && seg + i < pat.Length; i++)
                {
                    char t = pat[seg + i];
                    int midi;
                    switch (t)
                    {
                        case 'R': case 'g': midi = root; break;
                        case '3': midi = root + third; break;
                        case '5': midi = root + fifth; break;
                        case '7': midi = root + sev; break;
                        case '8': midi = root + 12; break;
                        case 'b': midi = nroot - 1; break;
                        case 'u': midi = nroot + 1; break;
                        case 'p': midi = root + 1; break;
                        case 'f': midi = root + 6; break;
                        case 'h': midi = root + 2; break;
                        case '4': midi = root + 5; break;
                        case '6': midi = root + 9; break;
                        default: continue;
                    }
                    int len = SustainLen(pat, seg + i, seg + spb);
                    s.Add(bar * spb + i, len * gate, inst, midi, t == 'g' ? vel * 0.4f : vel, 0f);
                }
            }
        }

        /// <summary>Chord comping: rhythm 'x'/'X' starts the bar's chord, '-' sustains.</summary>
        static void Comp(SongSpec s, MusicInst inst, Chord[] ch, string rhythm, int center, float vel, float gate = 0.95f, float spread = 0.6f, int from = 0, int to = -1)
        {
            int spb = s.StepsPerBar;
            if (to < 0) to = s.Bars;
            int pBars = Math.Max(1, rhythm.Length / spb);
            for (int bar = from; bar < to; bar++)
            {
                var notes = Voicing(ch[bar % ch.Length], center);
                int seg = ((bar - from) % pBars) * spb;
                for (int i = 0; i < spb && seg + i < rhythm.Length; i++)
                {
                    float v = Vel(rhythm[seg + i]);
                    if (v <= 0f) continue;
                    int len = SustainLen(rhythm, seg + i, seg + spb);
                    for (int k = 0; k < notes.Length; k++)
                    {
                        float pan = notes.Length > 1 ? Synth.Lerp(-spread * 0.5f, spread * 0.5f, (float)k / (notes.Length - 1)) : 0f;
                        s.Add(bar * spb + i, len * gate, inst, notes[k], v * vel, pan);
                    }
                }
            }
        }

        /// <summary>Arpeggio over the bar's chord (count notes upward from center); index restarts at groupStarts.</summary>
        static void Arp(SongSpec s, MusicInst inst, Chord[] ch, string rhythm, int center, int count, float vel, float gate, float pan,
                        int from, int to, int[] groupStarts = null, bool upDown = false)
        {
            int spb = s.StepsPerBar;
            var arp = new int[count];
            for (int bar = from; bar < to; bar++)
            {
                var basev = Voicing(ch[bar % ch.Length], center);
                for (int i = 0; i < count; i++) arp[i] = basev[i % basev.Length] + 12 * (i / basev.Length);
                int k = 0;
                for (int i = 0; i < spb && i < rhythm.Length; i++)
                {
                    if (groupStarts != null && Array.IndexOf(groupStarts, i) >= 0) k = 0;
                    float v = Vel(rhythm[i]);
                    if (v <= 0f) continue;
                    int idx = k % count;
                    if (upDown)
                    {
                        int period = Math.Max(1, count * 2 - 2);
                        int m = k % period;
                        idx = m < count ? m : period - m;
                    }
                    bool accent = groupStarts != null && Array.IndexOf(groupStarts, i) >= 0;
                    s.Add(bar * spb + i, gate, inst, arp[idx], v * vel * (accent ? 1.15f : 1f), (k & 1) == 0 ? pan : -pan);
                    k++;
                }
            }
        }

        /// <summary>Explicit melody: quadruples (bar, step, lengthSteps, midi). glide => each note slides from the previous.</summary>
        static void Mel(SongSpec s, MusicInst inst, float vel, float pan, bool glide, params float[] q)
        {
            for (int i = 0; i + 3 < q.Length; i += 4)
            {
                float step = q[i] * s.StepsPerBar + q[i + 1];
                float from = glide && i >= 4 ? q[i - 1] : 0f;
                s.Add(step, q[i + 2], inst, q[i + 3], vel, pan, from);
            }
        }

        /// <summary>Jazz walking bass in quarters: root, chord tones, chromatic approach into the next bar.</summary>
        static void Walk(SongSpec s, MusicInst inst, Chord[] ch, int lo, float vel)
        {
            int spb = s.StepsPerBar;
            for (int bar = 0; bar < s.Bars; bar++)
            {
                var c = ch[bar % ch.Length];
                var nx = ch[(bar + 1) % ch.Length];
                int root = Place(c.Bass, lo), nroot = Place(nx.Bass, lo);
                int third = root + c.Iv[1], fifth = root + c.Iv[2];
                int sixth = root + (c.Iv.Length > 3 ? c.Iv[3] : 9);
                int[] q = (bar & 1) == 0 ? new[] { root, third, fifth, nroot - 1 } : new[] { root, fifth, sixth, nroot + 1 };
                for (int b = 0; b < 4; b++) s.Add(bar * spb + b * 4, 3.4f, inst, q[b], b == 0 ? vel : vel * 0.85f, 0f);
            }
        }

        // ------------------------------------------------------------------ songs
        static SongSpec Menu()
        {
            var s = new SongSpec { Name = "Menu - Lounge Lizard", Bpm = 100f, Swing = 0.14f, SwingGrid = 1, Seed = 11, DuckDepth = 0.18f };
            s.Sends = new[] { 0.06f, 0f, 0.3f, 0.35f };
            var ch = new[] { C(50, Min9), C(55, Dom9), C(50, Min9), C(55, Dom9), C(58, Maj7), C(57, Dom7b9), C(50, Min9), C(57, Dom7) };
            Drum(s, MusicInst.Kick, "X.....x...x.....X.....x..x......", 36, 0.85f);
            Drum(s, MusicInst.Snare, "....x..g.g..x..g....x..g....x.gg", 38, 0.6f);
            Drum(s, MusicInst.HatC, "xoxoxoxoxoxoxoxo", 42, 0.5f, 0.25f);
            Drum(s, MusicInst.HatO, "..............o.", 46, 0.45f, 0.25f);
            Drum(s, MusicInst.Shaker, "gggggggggggggggg", 70, 0.6f, -0.3f);
            Comp(s, MusicInst.EPiano, ch, "x-----x---x-----..x---x-....x---", 64, 0.7f);
            Bass(s, MusicInst.BassSynth, ch, "R-.R..8.5-.R..b.R-.R..8.5-.7.8.b", 36, 0.9f, 0.8f);
            Mel(s, MusicInst.Vibes, 0.6f, -0.2f, false,
                4, 0, 3, 74, 4, 3, 3, 77, 4, 6, 2, 81, 4, 8, 6, 79,
                5, 0, 4, 77, 5, 4, 2, 76, 5, 6, 6, 74,
                6, 0, 3, 72, 6, 3, 3, 74, 6, 6, 2, 77, 6, 8, 8, 76,
                7, 0, 4, 73, 7, 4, 4, 76, 7, 8, 8, 74);
            return s;
        }

        static SongSpec Training()
        {
            var s = new SongSpec { Name = "Training - Elevator Orientation", Bpm = 124f, Swing = 0.33f, SwingGrid = 2, Seed = 22, DuckDepth = 0.1f };
            s.Sends = new[] { 0.08f, 0f, 0.25f, 0.3f };
            var ch = new[] { C(53, Maj7), C(50, Min7), C(55, Min7), C(48, Dom7), C(53, Maj7), C(50, Min7), C(55, Min7), C(48, Dom7) };
            Drum(s, MusicInst.Ride, "X...x.x.X...x.x.", 60, 0.8f, 0.3f);
            Drum(s, MusicInst.HatC, "....x.......x...", 42, 0.6f, -0.2f);
            Drum(s, MusicInst.Kick, "o.......o.......", 36, 0.7f);
            Drum(s, MusicInst.Rim, "....x.......x..g", 84, 0.5f, -0.1f);
            Walk(s, MusicInst.BassUpright, ch, 36, 0.95f);
            Comp(s, MusicInst.EPiano, ch, "x-....x-..........x-....x-....x-", 62, 0.6f);
            Mel(s, MusicInst.Vibes, 0.65f, 0.15f, false,
                0, 0, 2, 69, 0, 2, 2, 72, 0, 4, 2, 76, 0, 6, 4, 77, 0, 10, 2, 76, 0, 12, 4, 72,
                1, 0, 3, 74, 1, 4, 2, 77, 1, 6, 4, 81, 1, 12, 4, 79,
                2, 0, 2, 70, 2, 2, 2, 74, 2, 4, 2, 77, 2, 6, 6, 81, 2, 12, 2, 79, 2, 14, 2, 77,
                3, 0, 4, 76, 3, 4, 2, 79, 3, 6, 4, 82, 3, 10, 2, 81, 3, 12, 4, 79,
                4, 0, 6, 81, 4, 6, 2, 79, 4, 8, 4, 77, 4, 12, 4, 76,
                5, 0, 2, 77, 5, 2, 2, 76, 5, 4, 4, 74, 5, 8, 4, 69, 5, 12, 4, 72,
                6, 0, 4, 74, 6, 4, 2, 70, 6, 6, 2, 74, 6, 8, 4, 77, 6, 12, 2, 76, 6, 14, 2, 74,
                7, 0, 6, 72, 7, 6, 2, 76, 7, 8, 4, 79, 7, 12, 4, 76);
            return s;
        }

        static SongSpec Factory()
        {
            var s = new SongSpec { Name = "Factory - Clank Shift", Bpm = 108f, Swing = 0.06f, SwingGrid = 1, Seed = 33, DuckDepth = 0.4f };
            s.Sends = new[] { 0.07f, 0f, 0.2f, 0.25f };
            var ch = new[] { C(52, Min), C(52, Min), C(48, Maj), C(50, Maj), C(52, Min), C(52, Min), C(48, Maj), C(47, Maj) };
            Drum(s, MusicInst.Kick, "X..x..x.X..x....", 36, 1f);
            Drum(s, MusicInst.Clap, "....X.......X...", 39, 0.8f, 0.05f);
            Drum(s, MusicInst.Snare, "....x.......x..g", 38, 0.5f);
            Drum(s, MusicInst.Clank, "..x...x...x.x..x", 79, 0.6f, 0.3f);
            Drum(s, MusicInst.Anvil, "......x.......x.", 88, 0.45f, -0.35f);
            Drum(s, MusicInst.Shaker, "xgxgxgxgxgxgxgxg", 70, 0.8f, -0.35f);
            Drum(s, MusicInst.HatO, "..o...o...o...o.", 46, 0.4f, 0.2f);
            s.Add(3 * 16 + 12, 4, MusicInst.Steam, 60, 0.7f, 0.4f);
            s.Add(7 * 16 + 12, 4, MusicInst.Steam, 60, 0.7f, -0.4f);
            s.Add(0, 16, MusicInst.Crash, 60, 0.6f);
            Bass(s, MusicInst.BassDist, ch, "R.R8.R.p.R.5.R.b", 40, 0.9f, 0.7f);
            Comp(s, MusicInst.Organ, ch, "..x...x...x...x.", 64, 0.5f, 1.2f, 0.5f);
            Mel(s, MusicInst.Lead, 0.6f, -0.15f, false,
                4, 0, 2, 76, 4, 2, 2, 79, 4, 4, 2, 77, 4, 6, 2, 76, 4, 8, 4, 71, 4, 12, 4, 74,
                5, 0, 2, 76, 5, 2, 2, 79, 5, 4, 2, 81, 5, 6, 2, 82, 5, 8, 4, 81, 5, 12, 4, 79,
                6, 0, 2, 79, 6, 2, 2, 76, 6, 4, 4, 72, 6, 8, 2, 76, 6, 10, 2, 79, 6, 12, 4, 84,
                7, 0, 4, 83, 7, 4, 2, 81, 7, 6, 2, 78, 7, 8, 8, 75);
            return s;
        }

        static SongSpec Lab()
        {
            var s = new SongSpec { Name = "Lab - Something Is Leaking", Bpm = 96f, Seed = 44, DuckDepth = 0.45f, DuckRelease = 0.2f, ReverbSize = 0.85f, ReverbReturn = 0.7f };
            s.Sends = new[] { 0.1f, 0f, 0.35f, 0.45f };
            s.EchoSteps = 3f; s.EchoFeedback = 0.45f; s.EchoSend = 0.22f;
            var ch = new[] { C(57, Min), C(57, Min), C(53, Maj), C(53, Maj), C(50, Min), C(50, Min), C(52, Maj), C(52, Maj) };
            Drum(s, MusicInst.Kick, "X..o....X..o....", 36, 0.85f);
            Drum(s, MusicInst.Rim, "x.......x.......", 86, 0.35f, 0.4f);
            Drum(s, MusicInst.Rim, "....x.......x...", 81, 0.3f, -0.4f);
            Drum(s, MusicInst.Shaker, "..g...g...g...g.", 70, 0.8f, -0.4f);
            Drum(s, MusicInst.HatC, "..............g.", 42, 0.6f, 0.3f);
            Bass(s, MusicInst.BassPulse, ch, "R.R.R.R.R.R.R.R.", 40, 0.8f, 0.6f);
            Comp(s, MusicInst.Pad, ch, "x---------------", 60, 0.6f, 1.0f, 0.8f);
            Mel(s, MusicInst.Theremin, 0.7f, 0.1f, true,
                0, 0, 12, 76, 0, 12, 4, 74, 1, 0, 8, 72, 1, 8, 8, 71, 2, 0, 12, 69, 2, 12, 4, 72, 3, 0, 16, 77,
                4, 0, 8, 76, 4, 8, 4, 77, 4, 12, 4, 76, 5, 0, 16, 74, 6, 0, 8, 71, 6, 8, 8, 68, 7, 0, 12, 76);
            var r = new AudioRng(4242);
            for (int bar = 0; bar < s.Bars; bar++)
                for (int k = 0; k < 2; k++)
                {
                    var v = Voicing(ch[bar], 84);
                    s.Add(bar * 16 + r.Range(0, 8) * 2, 2, MusicInst.Bell, v[r.Range(0, v.Length)], 0.35f, r.Range(-0.7f, 0.7f));
                }
            return s;
        }

        static SongSpec Research()
        {
            var s = new SongSpec { Name = "Research - Odd Signals", Bpm = 132f, StepsPerBar = 14, Seed = 55, DuckDepth = 0.15f, ReverbSize = 0.7f };
            s.Sends = new[] { 0.08f, 0f, 0.3f, 0.35f };
            s.EchoSteps = 3f; s.EchoSend = 0.2f;
            var ch = new[] { C(50, Lyd), C(52, Maj, 50), C(50, Lyd), C(52, Maj, 50), C(47, Min9), C(55, Maj7s11), C(52, Min9), C(57, Sus47) };
            Drum(s, MusicInst.Kick, "X.........x...", 36, 0.8f);
            Drum(s, MusicInst.Clap, "......x.......", 39, 0.55f, 0.1f);
            Drum(s, MusicInst.Woodblock, "X.....x...x...", 79, 0.55f, 0.35f);
            Drum(s, MusicInst.Shaker, "x.x.x.x.x.x.x.", 70, 0.7f, -0.3f);
            Drum(s, MusicInst.HatC, "..g...g...g..g", 42, 0.7f, 0.2f);
            Bass(s, MusicInst.BassSynth, ch, "R-....5-..8-..", 38, 0.8f, 0.85f);
            Arp(s, MusicInst.Pluck, ch, "xxxxxxxxxxxxxx", 62, 7, 0.5f, 1.2f, 0.35f, 0, s.Bars, new[] { 0, 6, 10 });
            Comp(s, MusicInst.Pad, ch, "x-------------", 62, 0.35f, 1f, 0.8f);
            float[] wt = { 74, 76, 78, 80, 82, 84 };
            for (int i = 0; i < 6; i++) s.Add(3 * 14 + 8 + i, 1, MusicInst.Bell, wt[i], 0.45f, 0.3f);
            for (int i = 0; i < 6; i++) s.Add(7 * 14 + 8 + i, 1, MusicInst.Bell, wt[5 - i], 0.45f, -0.3f);
            Mel(s, MusicInst.Lead, 0.45f, -0.1f, false,
                4, 0, 3, 74, 4, 3, 3, 73, 4, 6, 4, 71, 4, 10, 4, 66,
                5, 0, 6, 74, 5, 6, 4, 78, 5, 10, 4, 79,
                6, 0, 3, 79, 6, 3, 3, 78, 6, 6, 8, 74);
            return s;
        }

        static SongSpec Chaos()
        {
            var s = new SongSpec { Name = "Chaos - Meltdown Protocol", Bpm = 140f, Seed = 66, DuckDepth = 0.35f, ReverbSize = 0.4f };
            s.Sends = new[] { 0.05f, 0f, 0.15f, 0.2f };
            s.EchoSteps = 3f; s.EchoSend = 0.12f;
            var ch = new[] { C(48, Min), C(48, Min), C(56, Maj), C(58, Maj), C(48, Min), C(48, Min), C(49, Maj), C(55, Maj) };
            Drum(s, MusicInst.Kick, "X...X...X...X...", 36, 1f);
            Drum(s, MusicInst.Snare, "....X.......X...", 38, 0.9f, 0f, 0, 7);
            Drum(s, MusicInst.Snare, "....X...X.X.XXXX", 38, 0.85f, 0f, 7, 8);
            Drum(s, MusicInst.Clap, "....x.......x...", 39, 0.5f);
            Drum(s, MusicInst.HatC, "xgxgxgxgxgxgxgxg", 42, 0.7f, 0.2f);
            Drum(s, MusicInst.HatO, "..o...o...o...o.", 46, 0.5f, -0.2f);
            s.Add(0, 16, MusicInst.Crash, 60, 0.7f);
            s.Add(64, 16, MusicInst.Crash, 60, 0.6f);
            Bass(s, MusicInst.BassSub, ch, "R.R.8.R.R.R.8.R.", 36, 0.9f, 0.8f);
            int[] hi = { 79, 79, 80, 82, 79, 79, 80, 83 };
            int[] lo = { 75, 75, 75, 77, 75, 75, 77, 79 };
            for (int bar = 0; bar < 8; bar++)
                for (int h = 0; h < (bar < 4 ? 2 : 1); h++)
                {
                    int st = bar * 16 + h * 8;
                    s.Add(st, 4, MusicInst.SirenLead, hi[bar], 0.7f, -0.15f);
                    s.Add(st + 4, 4, MusicInst.SirenLead, lo[bar], 0.6f, -0.15f);
                }
            Arp(s, MusicInst.Pluck, ch, "xxxxxxxxxxxxxxxx", 72, 6, 0.4f, 0.9f, 0.4f, 4, 8, null, true);
            Comp(s, MusicInst.Stab, ch, "..x...x...x...x.", 60, 0.55f, 1.5f, 0.6f, 4, 8);
            return s;
        }

        static SongSpec Victory()
        {
            var s = new SongSpec { Name = "Victory - Employee Of The Month", Bpm = 116f, Swing = 0.1f, SwingGrid = 1, Seed = 77, DuckDepth = 0.15f };
            s.Sends = new[] { 0.08f, 0f, 0.25f, 0.3f };
            s.EchoSend = 0.08f;
            var ch = new[] { C(48, Maj), C(53, Maj), C(55, Maj), C(48, Maj), C(57, Min), C(53, Maj), C(55, Dom7), C(48, Maj) };
            Drum(s, MusicInst.Kick, "X.......X.......", 36, 0.95f);
            Drum(s, MusicInst.Snare, "....X.......X..x", 38, 0.8f, 0f, 0, 7);
            Drum(s, MusicInst.Snare, "....X...X.XxXXXX", 38, 0.8f, 0f, 7, 8);
            Drum(s, MusicInst.HatC, "x.x.x.x.x.x.x.x.", 42, 0.5f, 0.2f);
            s.Add(0, 16, MusicInst.Crash, 60, 0.7f);
            s.Add(64, 16, MusicInst.Crash, 60, 0.6f);
            s.Add(0, 1, MusicInst.Tom, 45, 0.9f);
            s.Add(3, 1, MusicInst.Tom, 45, 0.7f);
            s.Add(6, 1, MusicInst.Tom, 45, 0.8f);
            float[] fill = { 50, 47, 45, 43 };
            for (int i = 0; i < 4; i++) s.Add(7 * 16 + 8 + i * 2, 1, MusicInst.Tom, fill[i], 0.8f, (i - 1.5f) * 0.2f);
            Bass(s, MusicInst.BassSynth, ch, "R...5...R...5.8.", 36, 0.85f, 0.8f);
            Comp(s, MusicInst.Organ, ch, "..x...x...x...x.", 64, 0.45f, 1.5f, 0.5f);
            Comp(s, MusicInst.Brass, ch, "x-.x..x-x-------", 64, 0.75f, 0.95f, 0.6f, 0, 1);
            Comp(s, MusicInst.Brass, ch, "x-.x..x-x-------", 64, 0.65f, 0.95f, 0.6f, 4, 5);
            Mel(s, MusicInst.Horn, 0.75f, 0.1f, false,
                0, 0, 2, 67, 0, 3, 1, 72, 0, 6, 2, 76, 0, 8, 8, 79,
                1, 0, 4, 81, 1, 4, 2, 79, 1, 6, 2, 77, 1, 8, 6, 81, 1, 14, 2, 79,
                2, 0, 3, 79, 2, 3, 1, 77, 2, 4, 2, 76, 2, 6, 2, 74, 2, 8, 4, 71, 2, 12, 4, 74,
                3, 0, 8, 72, 3, 8, 2, 76, 3, 10, 2, 79, 3, 12, 4, 84,
                4, 0, 4, 76, 4, 4, 2, 72, 4, 6, 2, 76, 4, 8, 8, 81,
                5, 0, 2, 81, 5, 2, 2, 79, 5, 4, 4, 77, 5, 8, 4, 72, 5, 12, 4, 77,
                6, 0, 4, 79, 6, 4, 2, 81, 6, 6, 2, 83, 6, 8, 4, 86, 6, 12, 4, 83,
                7, 0, 12, 84, 7, 12, 2, 79, 7, 14, 2, 76);
            Arp(s, MusicInst.Bell, ch, "x.x.x.x.x.x.x.x.", 84, 4, 0.3f, 1f, 0.5f, 4, 8);
            return s;
        }
    }
}
