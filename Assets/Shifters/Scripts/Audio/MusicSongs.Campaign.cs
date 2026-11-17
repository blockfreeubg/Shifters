// =====================================================================================
//  SHIFTERS - MusicSongs.Campaign.cs   [owned by the AUDIO agent]
//  "THE GREAT SHIFT" soundtrack (seamless loops, composed in code):
//    Villain    "HAIL TO ME"         104 BPM  G minor pompous march: tuba oom-pah, snare rolls, a cymbal
//                                             on every downbeat, a kazoo lead that is always slightly late
//    Heist      "SWING SHIFT"        138 BPM  F minor swing: walking bass, brushed ride, harmon-muted trumpet
//    Spooky     "NIGHT SHIFT WALTZ"  3/4      D minor music-box waltz, ticking clock, theremin, midnight chime
//    Boss       "PERFORMANCE REVIEW" 150 BPM  E minor rock: power-chord chugs, brass stabs, choir
//    FinalBoss  "HAIL TO ME (ROCK)"  132 BPM  the villain theme as a rock orchestra (guitars, strings, timpani)
//    Finale     "SHIFT CHANGE"       132 BPM  the same motif in C MAJOR for the full band, key change up to D
// =====================================================================================
using System;

namespace Shifters
{
    public static partial class MusicSongs
    {
        static SongSpec BuildCampaign(MusicId id)
        {
            switch (id)
            {
                case MusicId.Villain: return Villain();
                case MusicId.Heist: return Heist();
                case MusicId.Spooky: return Spooky();
                case MusicId.Boss: return Boss();
                case MusicId.FinalBoss: return FinalBoss();
                case MusicId.Finale: return Finale();
                default: return null;
            }
        }

        // The "HAIL TO ME" motif (G minor), quadruples (bar, step, len, midi) - shared by Villain / FinalBoss / Finale.
        static readonly float[] HailToMe =
        {
            0, 0, 3, 67, 0, 3, 1, 67, 0, 4, 3, 70, 0, 7, 1, 69, 0, 8, 4, 67, 0, 12, 4, 74,
            1, 0, 3, 72, 1, 3, 1, 70, 1, 4, 4, 69, 1, 8, 4, 66, 1, 12, 4, 62,
            2, 0, 3, 67, 2, 3, 1, 69, 2, 4, 3, 70, 2, 7, 1, 72, 2, 8, 6, 74, 2, 14, 2, 75,
            3, 0, 3, 74, 3, 3, 1, 72, 3, 4, 3, 70, 3, 7, 1, 69, 3, 8, 8, 67,
            4, 0, 3, 70, 4, 3, 1, 70, 4, 4, 3, 74, 4, 7, 1, 72, 4, 8, 4, 70, 4, 12, 4, 79,
            5, 0, 3, 79, 5, 3, 1, 77, 5, 4, 4, 75, 5, 8, 4, 74, 5, 12, 4, 70,
            6, 0, 3, 72, 6, 3, 1, 75, 6, 4, 3, 74, 6, 7, 1, 72, 6, 8, 4, 70, 6, 12, 4, 69,
            7, 0, 4, 69, 7, 4, 2, 70, 7, 6, 2, 69, 7, 8, 4, 66, 7, 12, 2, 62, 7, 14, 2, 62,
        };

        /// <summary>Melody from quadruples with a transposition and a per-note "late" offset (lateMax steps, random).</summary>
        static void MelLoose(SongSpec s, MusicInst inst, float vel, float pan, float[] q, int semis, float lateMax, ref AudioRng r, int fromBar = 0, int toBar = 99)
        {
            for (int i = 0; i + 3 < q.Length; i += 4)
            {
                if (q[i] < fromBar || q[i] >= toBar) continue;
                float late = lateMax > 0f ? r.Range(lateMax * 0.25f, lateMax) : 0f;
                s.Add(q[i] * s.StepsPerBar + q[i + 1] + late, q[i + 2] - late * 0.5f, inst, q[i + 3] + semis, vel * (q[i + 1] == 0 ? 1f : 0.9f), pan);
            }
        }

        // ------------------------------------------------------------------ VILLAIN
        static SongSpec Villain()
        {
            var s = new SongSpec { Name = "Villain - HAIL TO ME", Bpm = 104f, Seed = 104, DuckDepth = 0.15f, ReverbSize = 0.7f };
            s.Sends = new[] { 0.1f, 0f, 0.25f, 0.22f };
            s.EchoSend = 0.06f;
            var ch = new[] { C(55, Min), C(50, Dom7), C(55, Min), C(48, Min), C(55, Min, 50), C(51, Maj), C(48, Min), C(50, Dom7) };
            Drum(s, MusicInst.Kick, "X.......x.......", 36, 0.7f);                                   // bass drum (he wears it)
            Drum(s, MusicInst.Snare, "..g.X..g..g.X.g...g.X..g.gggXggg", 38, 0.55f, 0.05f);          // march + rolls
            Drum(s, MusicInst.Snare, "................................................gggggggXgXgXXXXX", 38, 0.6f, 0.05f, 0, 8);
            Drum(s, MusicInst.Cymbal, "x...............", 60, 0.55f, 0.3f);                          // EVERY downbeat. He insists.
            Drum(s, MusicInst.Cymbal, "........o.......", 60, 0.3f, -0.3f, 4, 8);
            Bass(s, MusicInst.Tuba, ch, "R---....5---....", 40, 0.8f, 0.85f);                         // oom ... pah
            Comp(s, MusicInst.Brass, ch, "....x-......x-..", 62, 0.4f, 0.9f, 0.5f, 2, 8);             // pompous stabs
            Comp(s, MusicInst.Organ, ch, "x---------------", 60, 0.25f, 1f, 0.6f, 4, 8);
            var r = new AudioRng(1040);
            MelLoose(s, MusicInst.Kazoo, 0.8f, -0.1f, HailToMe, 0, 0.35f, ref r);                      // kazoo, always a bit late
            MelLoose(s, MusicInst.Glock, 0.35f, 0.35f, HailToMe, 12, 0f, ref r, 4, 8);                 // glockenspiel doubling
            return s;
        }

        // ------------------------------------------------------------------ HEIST
        static SongSpec Heist()
        {
            var s = new SongSpec { Name = "Heist - SWING SHIFT", Bpm = 138f, Swing = 0.33f, SwingGrid = 2, Seed = 138, DuckDepth = 0.08f, ReverbSize = 0.5f };
            s.Sends = new[] { 0.08f, 0f, 0.22f, 0.3f };
            s.EchoSteps = 3f; s.EchoSend = 0.1f;
            var ch = new[] { C(53, Min7), C(53, Min7), C(58, Min7), C(58, Min7), C(49, Dom7), C(48, Dom7b9), C(53, Min7), C(48, Dom7) };
            Drum(s, MusicInst.Ride, "X...x.x.X...x.x.", 60, 0.7f, 0.3f);                              // ding, ding-a ding
            Drum(s, MusicInst.Brush, "g.g.x.g.g.g.x.g.", 38, 0.8f, -0.15f);                           // brushes
            Drum(s, MusicInst.HatC, "....x.......x...", 42, 0.5f, -0.25f);                            // foot hat on 2 & 4
            Drum(s, MusicInst.Kick, "o.......o.......", 36, 0.45f);                                   // feathered
            Drum(s, MusicInst.Rim, "..............g.......x.......g.", 84, 0.35f, 0.1f);
            Walk(s, MusicInst.BassUpright, ch, 36, 0.72f);
            Comp(s, MusicInst.EPiano, ch, "..x-......x-......x-....x-..x-..", 60, 0.45f);
            Mel(s, MusicInst.MutedTrumpet, 0.75f, 0.12f, false,
                0, 2, 2, 72, 0, 4, 2, 75, 0, 6, 4, 77, 0, 10, 2, 75, 0, 12, 4, 72,
                1, 0, 2, 68, 1, 2, 2, 70, 1, 4, 2, 71, 1, 6, 6, 72,
                2, 0, 2, 73, 2, 2, 2, 77, 2, 4, 4, 80, 2, 8, 2, 79, 2, 10, 2, 77, 2, 12, 4, 73,
                3, 0, 4, 76, 3, 4, 2, 79, 3, 6, 4, 82, 3, 10, 2, 79, 3, 12, 4, 76,
                4, 0, 2, 77, 4, 2, 2, 80, 4, 4, 4, 84, 4, 8, 2, 83, 4, 10, 2, 82, 4, 12, 4, 80,
                5, 0, 3, 80, 5, 3, 1, 79, 5, 4, 2, 77, 5, 6, 2, 75, 5, 8, 4, 73, 5, 12, 4, 77,
                6, 0, 2, 76, 6, 2, 2, 77, 6, 4, 2, 79, 6, 6, 2, 80, 6, 8, 4, 82, 6, 12, 2, 85, 6, 14, 2, 84,
                7, 0, 6, 84, 7, 6, 2, 82, 7, 8, 2, 79, 7, 10, 2, 76, 7, 12, 4, 72);
            Mel(s, MusicInst.Vibes, 0.3f, -0.35f, false, 3, 14, 2, 84, 7, 14, 2, 89);                  // little "ding" tags
            return s;
        }

        // ------------------------------------------------------------------ SPOOKY (3/4)
        static SongSpec Spooky()
        {
            var s = new SongSpec { Name = "Spooky - NIGHT SHIFT WALTZ", Bpm = 138f, StepsPerBar = 12, Bars = 12, Seed = 13, DuckDepth = 0.05f,
                                   ReverbSize = 0.9f, ReverbReturn = 0.7f };
            s.Sends = new[] { 0.15f, 0f, 0.4f, 0.45f };
            s.EchoSteps = 6f; s.EchoFeedback = 0.4f; s.EchoSend = 0.16f;
            var ch = new[] { C(50, Min), C(50, Min, 45), C(55, Min), C(50, Min), C(58, Maj), C(55, Min), C(57, Dom7), C(57, Dom7),
                             C(50, Min), C(55, Min), C(51, Maj), C(57, Dom7) };
            Drum(s, MusicInst.Woodblock, "x...g...g...", 96, 0.22f, 0.4f);                            // the clock ticks
            Drum(s, MusicInst.Woodblock, "..g...g...g.", 91, 0.12f, -0.4f);
            Bass(s, MusicInst.BassUpright, ch, "R...........", 36, 0.85f, 0.35f);                      // oom
            Comp(s, MusicInst.Pluck, ch, "....x...x...", 62, 0.3f, 0.4f, 0.5f);                         // pah pah
            Comp(s, MusicInst.Choir, ch, "x-----------", 57, 0.32f, 1f, 0.7f);
            Mel(s, MusicInst.MusicBox, 0.8f, 0.1f, false,
                0, 0, 4, 69, 0, 4, 4, 74, 0, 8, 4, 77,
                1, 0, 8, 81, 1, 8, 2, 79, 1, 10, 2, 77,
                2, 0, 4, 79, 2, 4, 4, 82, 2, 8, 4, 74,
                3, 0, 8, 77, 3, 8, 2, 76, 3, 10, 2, 74,
                4, 0, 4, 74, 4, 4, 4, 77, 4, 8, 4, 82,
                5, 0, 6, 82, 5, 6, 2, 81, 5, 8, 4, 79,
                6, 0, 4, 76, 6, 4, 4, 79, 6, 8, 4, 73,
                7, 0, 12, 76,
                8, 0, 4, 74, 8, 4, 4, 77, 8, 8, 4, 81,
                9, 0, 4, 82, 9, 4, 2, 81, 9, 6, 2, 79, 9, 8, 4, 74,
                10, 0, 4, 79, 10, 4, 4, 82, 10, 8, 4, 87,
                11, 0, 4, 81, 11, 4, 2, 79, 11, 6, 2, 76, 11, 8, 4, 73);
            Mel(s, MusicInst.Theremin, 0.45f, -0.2f, true,
                2, 0, 24, 62, 4, 0, 12, 65, 5, 0, 12, 62, 6, 0, 24, 61, 9, 0, 12, 67, 10, 0, 12, 70, 11, 0, 12, 69);
            s.Add(7 * 12, 6, MusicInst.Bell, 62, 0.4f, 0.3f);                                          // midnight chimes
            s.Add(7 * 12 + 6, 6, MusicInst.Bell, 57, 0.35f, -0.3f);
            s.Add(11 * 12 + 8, 4, MusicInst.Bell, 69, 0.3f, 0.3f);
            return s;
        }

        // ------------------------------------------------------------------ BOSS
        static SongSpec Boss()
        {
            var s = new SongSpec { Name = "Boss - PERFORMANCE REVIEW", Bpm = 150f, Seed = 150, DuckDepth = 0.35f, ReverbSize = 0.45f };
            s.Sends = new[] { 0.06f, 0f, 0.15f, 0.22f };
            s.EchoSend = 0.1f;
            var ch = new[] { C(52, Min), C(52, Min), C(48, Maj), C(50, Maj), C(52, Min), C(52, Min), C(48, Maj), C(47, Dom7) };
            Drum(s, MusicInst.Kick, "X..xX...X..xX.x.", 36, 0.8f);
            Drum(s, MusicInst.Snare, "....X.......X...", 38, 0.9f, 0f, 0, 7);
            Drum(s, MusicInst.Snare, "....X.......XxXX", 38, 0.9f, 0f, 7, 8);
            Drum(s, MusicInst.HatC, "x.x.x.x.x.x.x.x.", 42, 0.6f, 0.2f);
            s.Add(0, 16, MusicInst.Crash, 60, 0.7f);
            s.Add(64, 16, MusicInst.Crash, 60, 0.6f);
            s.Add(0, 4, MusicInst.Timpani, 40, 0.9f);
            s.Add(32, 4, MusicInst.Timpani, 36, 0.8f);
            Bass(s, MusicInst.BassDist, ch, "R.R.R.R.R.R.R.8.", 40, 0.85f, 0.6f);
            Bass(s, MusicInst.PowerGuitar, ch, "R.RRR.R.RRR.R5R.", 52, 0.8f, 0.45f);                   // chugga-chugga
            Comp(s, MusicInst.Brass, ch, "x..x..x.........", 64, 0.55f, 0.8f, 0.6f, 4, 8);
            Comp(s, MusicInst.Choir, ch, "x---------------", 64, 0.3f, 1f, 0.7f);
            Mel(s, MusicInst.Lead, 1f, -0.1f, false,
                4, 0, 2, 76, 4, 2, 2, 76, 4, 4, 2, 79, 4, 6, 2, 76, 4, 8, 4, 83, 4, 12, 4, 81,
                5, 0, 2, 79, 5, 2, 2, 78, 5, 4, 4, 76, 5, 8, 4, 74, 5, 12, 4, 78,
                6, 0, 2, 76, 6, 2, 2, 79, 6, 4, 4, 84, 6, 8, 4, 83, 6, 12, 4, 79,
                7, 0, 4, 78, 7, 4, 4, 81, 7, 8, 8, 83);
            Arp(s, MusicInst.Pluck, ch, "x.x.x.x.x.x.x.x.", 72, 4, 0.35f, 0.8f, 0.4f, 0, 4);
            return s;
        }

        // ------------------------------------------------------------------ FINAL BOSS (villain theme, rock orchestra)
        static SongSpec FinalBoss()
        {
            var s = new SongSpec { Name = "FinalBoss - HAIL TO ME (ROCK ORCHESTRA)", Bpm = 132f, Seed = 132, DuckDepth = 0.3f, ReverbSize = 0.65f };
            s.Sends = new[] { 0.07f, 0f, 0.2f, 0.25f };
            s.EchoSend = 0.08f;
            var ch = new[] { C(55, Min), C(50, Dom7), C(55, Min), C(48, Min), C(55, Min, 50), C(51, Maj), C(48, Min), C(50, Dom7) };
            Drum(s, MusicInst.Kick, "X.xxX.x.X.xxX.x.", 36, 0.8f);
            Drum(s, MusicInst.Snare, "....X.......X...", 38, 0.9f, 0f, 0, 3);
            Drum(s, MusicInst.Snare, "....X.......X.gg....X.......X...", 38, 0.9f, 0f, 3, 7);
            Drum(s, MusicInst.Snare, "....X...X.X.XXXX", 38, 0.9f, 0f, 7, 8);
            Drum(s, MusicInst.HatC, "x.x.x.x.x.x.x.x.", 42, 0.55f, 0.2f);
            Drum(s, MusicInst.Timpani, "X.......x.......", 31, 0.7f, -0.2f);
            Drum(s, MusicInst.Cymbal, "x...............", 60, 0.4f, 0.35f);                          // still on every downbeat
            s.Add(0, 16, MusicInst.Crash, 60, 0.7f);
            s.Add(64, 16, MusicInst.Crash, 60, 0.65f);
            Bass(s, MusicInst.BassDist, ch, "R.R.R.R.R.R.5.R.", 40, 0.85f, 0.6f);
            Bass(s, MusicInst.PowerGuitar, ch, "R.R.R.RRR.R.R.RR", 50, 0.75f, 0.45f);
            Comp(s, MusicInst.Strings, ch, "x-------x-------", 62, 0.45f, 1f, 0.8f);
            Comp(s, MusicInst.Choir, ch, "x---------------", 57, 0.3f, 1f, 0.7f, 4, 8);
            var r = new AudioRng(1320);
            MelLoose(s, MusicInst.Horn, 1.1f, 0.05f, HailToMe, 0, 0f, ref r);
            MelLoose(s, MusicInst.Strings, 0.5f, 0.3f, HailToMe, 12, 0f, ref r);
            MelLoose(s, MusicInst.Kazoo, 0.35f, -0.35f, HailToMe, 12, 0.25f, ref r, 4, 8);            // he still brought the kazoo
            return s;
        }

        // ------------------------------------------------------------------ FINALE (full band, key change)
        static SongSpec Finale()
        {
            var s = new SongSpec { Name = "Finale - SHIFT CHANGE", Bpm = 132f, Bars = 12, Swing = 0.06f, SwingGrid = 1, Seed = 2026, DuckDepth = 0.15f, ReverbSize = 0.6f };
            s.Sends = new[] { 0.08f, 0f, 0.25f, 0.28f };
            s.EchoSend = 0.08f;
            // C major for 7 bars, Bb pivot, D major for 3 bars, G7 back home
            var ch = new[] { C(48, Maj), C(57, Min), C(53, Maj), C(55, Maj), C(48, Maj), C(57, Min), C(53, Maj), C(58, Maj),
                             C(50, Maj), C(59, Min), C(52, Min), C(55, Dom7) };
            Drum(s, MusicInst.Kick, "X.......X.x.....", 36, 0.95f);
            Drum(s, MusicInst.Snare, "....X.......X..g", 38, 0.8f, 0f, 0, 7);
            Drum(s, MusicInst.Snare, "....X...X.X.XXXX", 38, 0.85f, 0f, 7, 8);
            Drum(s, MusicInst.Snare, "....X.......X..g", 38, 0.8f, 0f, 8, 12);
            Drum(s, MusicInst.Clap, "....x.......x...", 39, 0.45f, 0.1f, 8, 12);
            Drum(s, MusicInst.HatC, "x.x.x.x.x.x.x.x.", 42, 0.5f, 0.2f);
            Drum(s, MusicInst.Shaker, "gggggggggggggggg", 70, 0.6f, -0.3f);
            s.Add(0, 16, MusicInst.Crash, 60, 0.6f);
            s.Add(8 * 16, 16, MusicInst.Crash, 60, 0.8f);                                              // the key change
            s.Add(8 * 16, 4, MusicInst.Timpani, 38, 0.9f);
            Bass(s, MusicInst.BassSynth, ch, "R...5.R.R...5.8.", 36, 0.85f, 0.8f);
            Comp(s, MusicInst.EPiano, ch, "x-..x-..x-..x-..", 64, 0.35f);
            Comp(s, MusicInst.Organ, ch, "..x...x...x...x.", 60, 0.3f, 1.4f, 0.5f, 4, 12);
            Comp(s, MusicInst.Strings, ch, "x---------------", 67, 0.35f, 1f, 0.8f, 4, 12);
            Comp(s, MusicInst.Choir, ch, "x---------------", 60, 0.3f, 1f, 0.7f, 8, 12);
            Comp(s, MusicInst.Brass, ch, "x-.x..x-........", 64, 0.55f, 0.95f, 0.6f, 7, 9);
            Mel(s, MusicInst.Horn, 0.75f, 0.08f, false,
                0, 0, 3, 72, 0, 3, 1, 72, 0, 4, 3, 76, 0, 7, 1, 74, 0, 8, 4, 72, 0, 12, 4, 79,
                1, 0, 3, 81, 1, 3, 1, 79, 1, 4, 4, 76, 1, 8, 4, 72, 1, 12, 4, 69,
                2, 0, 3, 77, 2, 3, 1, 79, 2, 4, 3, 81, 2, 7, 1, 84, 2, 8, 6, 81, 2, 14, 2, 79,
                3, 0, 3, 79, 3, 3, 1, 77, 3, 4, 3, 74, 3, 7, 1, 71, 3, 8, 4, 67, 3, 12, 4, 71,
                4, 0, 3, 76, 4, 3, 1, 76, 4, 4, 3, 79, 4, 7, 1, 77, 4, 8, 4, 76, 4, 12, 4, 84,
                5, 0, 3, 84, 5, 3, 1, 83, 5, 4, 4, 81, 5, 8, 4, 76, 5, 12, 4, 72,
                6, 0, 3, 77, 6, 3, 1, 81, 6, 4, 3, 81, 6, 7, 1, 84, 6, 8, 4, 84, 6, 12, 4, 81,
                7, 0, 4, 82, 7, 4, 4, 86, 7, 8, 4, 89, 7, 12, 4, 86,
                8, 0, 3, 74, 8, 3, 1, 74, 8, 4, 3, 78, 8, 7, 1, 76, 8, 8, 4, 74, 8, 12, 4, 81,
                9, 0, 3, 83, 9, 3, 1, 81, 9, 4, 4, 78, 9, 8, 4, 74, 9, 12, 4, 71,
                10, 0, 3, 79, 10, 3, 1, 81, 10, 4, 3, 83, 10, 7, 1, 86, 10, 8, 6, 83, 10, 14, 2, 81,
                11, 0, 4, 79, 11, 4, 4, 83, 11, 8, 4, 86, 11, 12, 4, 74);
            Mel(s, MusicInst.Kazoo, 0.35f, -0.4f, false,                                               // Max joins the band (in time, finally)
                4, 0, 3, 67, 4, 4, 3, 72, 4, 8, 4, 67, 5, 0, 3, 72, 5, 4, 4, 69, 6, 0, 3, 69, 6, 4, 3, 72,
                8, 0, 3, 69, 8, 4, 3, 74, 8, 8, 4, 69, 9, 0, 3, 74, 9, 4, 4, 71, 10, 0, 3, 71, 10, 4, 3, 74);
            Arp(s, MusicInst.Glock, ch, "x.x.x.x.x.x.x.x.", 84, 4, 0.25f, 1f, 0.5f, 8, 12);
            Arp(s, MusicInst.Vibes, ch, "x...x...x...x...", 79, 4, 0.3f, 2f, 0.4f, 0, 8);
            return s;
        }
    }
}
