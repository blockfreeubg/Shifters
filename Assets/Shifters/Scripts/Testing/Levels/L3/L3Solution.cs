// =====================================================================================
//  SHIFTERS - L3Solution.cs   [LEVEL 3 - SACRIFICE ZONE]  TeamBot solution (no debug assists)
//  Characters are referenced by ARCHETYPE only. The bot plays the way a careful player does:
//    * a lure counts only once every bot is parked below the taunt prow, watching the taunter;
//    * crossers sprint through bot arenas on the route the parked bots can see least of;
//    * if a bot grabs somebody anyway, the lure walks the gallery to the CELL RELEASE lever, pulls it,
//      walks back and re-lures, and the freed intern crosses again from the cell (L3's cells stand by the
//      section entries, outside the arenas). Same for the tunnel cell in S3.
//    S1  Brainiac climbs the security gallery and taunts from the prow (the bot parks below);
//        Sprinter crosses and extends the catwalk from the far landing; Brute holds the 40 kg plate
//        (1 s latch) -> door; the Brainiac walks the gallery + catwalk back to the team.
//    S2  Sprinter presses TIMED and sprints the 27 m acid walkway, pulls SHUTTER OVERRIDE; others walk.
//    S3  Brainiac volunteers (chair -> hard-light bridge); Brute + Sprinter cross; Sprinter pulls RELEASE;
//        the bridge retracts; the Brainiac recovers through the maintenance tunnel (2 trench jumps + the
//        pipe catwalk over the tunnel bot); headcount gate latches.
//    S4  Brainiac lures BOTH bots to the gallery prow; Brute holds the 100 kg plate 3 s; the latch opens
//        door + staff hatch; everyone meets in the exit elevator.
//  Run: $SP/tools/run_level_test.sh <project> 3 <seed|a,b,c>
//       add -l3ForceCapture 1 to make the Brute walk into a patrolling bot in S1 and S4 first (proves the
//       rescue path: lever, re-lure, cross again from the cell).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L3Solution
    {
        /// <summary>Built scene (run_level_test.sh): -botSeed N / -botSeeds a,b,c handled by TeamBot.RunLevel.</summary>
        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            L3Checks.SceneSanity(ctx);
            yield return TeamBot.RunLevel(ctx, Solve, "L3");
        }

        /// <summary>Same solution on the level built at runtime by L3Builder (seeds from -botSeeds, else 11, 22, 33).</summary>
        public static IEnumerator RunRuntime(DevPlayContext ctx) => TeamBot.RunRuntimeLevel(ctx, 2, Solve, "L3rt");

        /// <summary>-l3ForceCapture 1: the Brute walks into a patrolling bot in S1 and S4 before the lure starts.</summary>
        static bool ForceCapture => BotArgs.Int("-l3ForceCapture", 0) != 0;

        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("team: " + bot.TeamSummary() + (ForceCapture ? "  [FORCED CAPTURES: rescue-path test]" : ""));
            yield return S1(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return S2(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return S3(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return S4(bot, brute, spr, brain);
        }

        // ================================================================== arenas
        /// <summary>One bot arena: its bots, where the lure stands, and the way out of its holding cell.</summary>
        sealed class Arena
        {
            public string Tag, Perch, Cell, Lever;
            public string[] BotMarks;
            public Vector3 Face;
            /// <summary>Gallery walks between the perch and the cell lever (high ground: bots can't reach them).</summary>
            public BotTarget[] PerchToLever, LeverToPerch;
            public int Captures;
            public float LastTaunt = -99f;
            /// <summary>The lure is busy (rescue / re-lure): the background taunting pauses.</summary>
            public bool LureBusy;
            public readonly List<SecurityBot> Bots = new List<SecurityBot>();

            public bool Resolve(TeamBot bot)
            {
                Bots.Clear();
                foreach (var m in BotMarks)
                {
                    var b = LevelMark.Find<SecurityBot>(m);
                    if (b == null) { bot.Fail("bot mark missing: " + m); return false; }
                    Bots.Add(b);
                }
                return true;
            }
        }

        static Arena S1Arena() => new Arena
        {
            Tag = "S1", Perch = "S1.Pulpit", Cell = "S1.Cell", Lever = "S1.CellLever", BotMarks = new[] { "S1.Bot" }, Face = Vector3.left,
            PerchToLever = new BotTarget[] { "S1.PulpitEntry", "S1.GalleryBendS", "S1.GalleryMid", "S1.CellStage" },
            LeverToPerch = new BotTarget[] { "S1.GalleryMid", "S1.GalleryBendS", "S1.PulpitEntry", "S1.Pulpit" },
        };

        static Arena S4Arena() => new Arena
        {
            Tag = "S4", Perch = "S4.Pulpit", Cell = "S4.Cell", Lever = "S4.CellLever", BotMarks = new[] { "S4.BotA", "S4.BotB" }, Face = Vector3.right,
            PerchToLever = new BotTarget[] { "S4.PulpitEntry", "S4.GalleryBendS", "S4.GalleryMid", "S4.CellStage" },
            LeverToPerch = new BotTarget[] { "S4.GalleryMid", "S4.GalleryBendS", "S4.PulpitEntry", "S4.Pulpit" },
        };

        // ================================================================== helpers
        static IEnumerator After(TeamBot bot, float seconds, IEnumerator r)
        {
            yield return bot.Wait(seconds);
            yield return r;
        }

        static IEnumerator Then(IEnumerator r, System.Action after)
        {
            yield return r;
            after();
        }

        static float Planar(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static IEnumerator ExpectCheckpoint(TeamBot bot, int index, float within)
        {
            var cp = LevelMark.Find<Checkpoint>("checkpoint." + index);
            if (cp == null) { bot.Fail("checkpoint." + index + " not found"); yield break; }
            yield return bot.Expect(() => cp.IsActivated, "checkpoint C" + index + " activated", within);
        }

        static bool Active(SecurityBot b) => b != null && b.isActiveAndEnabled;

        /// <summary>Every (active) bot of the arena is chasing the lure.</summary>
        static bool LureHolds(Arena a, ShifterCharacter lure)
        {
            foreach (var b in a.Bots)
                if (Active(b) && !(b.State == BotState.Chase && b.Target == lure)) return false;
            return true;
        }

        static string BotStates(Arena a, Vector3 perch)
        {
            string st = "";
            foreach (var b in a.Bots)
                st += b.name + "=" + b.State + "(" + (b.Target != null ? b.Target.DisplayName : "-") + (b.IsWaitingBelow ? ", parked" : "")
                      + ", d=" + Planar(b.transform.position, perch).ToString("0.0") + ") ";
            return st;
        }

        // ================================================================== luring
        /// <summary>Taunt, keeping the 2 s taunt cooldown between ALL taunts of this lure (background + lure loop).</summary>
        static IEnumerator Taunt(TeamBot bot, ShifterCharacter lure, Arena a)
        {
            float wait = a.LastTaunt + TauntGap - bot.Now;
            if (wait > 0f) yield return bot.Wait(wait);
            a.LastTaunt = bot.Now;
            yield return bot.Taunt(lure);
        }

        const float TauntGap = 2.25f;

        /// <summary>Taunt from the perch until every bot is PARKED below it: chasing the lure, waiting below, seeing it and
        /// standing still, for 1.5 s in a row. If they lock on but never park, go ahead after 'timeout' (rescue covers the
        /// rest); if they never take the bait at all, fail.</summary>
        static IEnumerator LureBots(TeamBot bot, ShifterCharacter lure, Arena a, float timeout)
        {
            bool wasBusy = a.LureBusy;
            a.LureBusy = true;
            Vector3 perch = LevelMark.Position(a.Perch);
            float t0 = bot.Now, parkedSince = -1f;
            while (true)
            {
                if (bot.Failed) yield break;
                bool locked = true, parked = true;
                foreach (var b in a.Bots)
                {
                    if (!Active(b)) continue;
                    bool on = b.State == BotState.Chase && b.Target == lure && Planar(b.transform.position, perch) < 5.5f;
                    if (!on) { locked = false; parked = false; break; }
                    var v = b.Body != null ? b.Body.linearVelocity : Vector3.zero;
                    if (!(b.IsWaitingBelow && b.CanSee(lure) && new Vector2(v.x, v.z).magnitude < 0.35f)) parked = false;
                }
                if (parked)
                {
                    if (parkedSince < 0f) parkedSince = bot.Now;
                    if (bot.Now - parkedSince >= 1.5f) break;
                }
                else parkedSince = -1f;
                if (bot.Now - t0 > timeout)
                {
                    if (locked) { bot.Warn("lure: bots locked on but not parked after " + timeout + " s - going anyway: " + BotStates(a, perch)); break; }
                    bot.Fail("bots never took the bait: " + BotStates(a, perch), lure);
                    yield break;
                }
                if (bot.Now - a.LastTaunt > TauntGap)
                {
                    yield return Taunt(bot, lure, a);
                    if (bot.Failed) yield break;
                    continue;
                }
                yield return null;
            }
            a.LureBusy = wasBusy;
            bot.Log("lure holds " + a.Bots.Count + " bot(s) parked below " + a.Perch + " (" + (bot.Now - t0).ToString("0.0") + " s)");
        }

        /// <summary>Re-taunt every ~2.2 s until done() (keeps the noise interest on the lure while others work).
        /// Pauses while the lure is busy with a rescue.</summary>
        static IEnumerator KeepTaunting(TeamBot bot, ShifterCharacter lure, Arena a, System.Func<bool> done, float max)
        {
            float t0 = bot.Now;
            while (!done() && !bot.Failed && bot.Now - t0 < max)
            {
                if (a.LureBusy || bot.Now - a.LastTaunt < TauntGap) { yield return null; continue; }
                yield return Taunt(bot, lure, a);
            }
        }

        // ================================================================== capture-aware walking
        sealed class WalkResult { public bool Captured; }

        /// <summary>Walk through waypoints with motor commands only (like TeamBot.WalkPath). If a security bot grabs c the
        /// walk stops quietly (r.Captured) so the solution can rescue c; anything else (stuck, timeout, lost) fails the run.</summary>
        static IEnumerator SafeWalk(TeamBot bot, ShifterCharacter c, IList<BotTarget> pts, bool sprint, float tolerance, WalkResult r, float timeoutPerPoint = 20f)
        {
            r.Captured = false;
            string title = c.DisplayName + " " + pts.Count + " pts" + (sprint ? " (sprint)" : "");
            bot.Log(">> SafeWalk " + title);
            float tStart = bot.Now;
            for (int i = 0; i < pts.Count; i++)
            {
                bool last = i == pts.Count - 1;
                float tol = last ? tolerance : Mathf.Max(0.8f, tolerance);
                if (!pts[i].IsValid) { bot.Fail("SafeWalk " + title + ": waypoint " + i + " not found: " + pts[i].Describe(), c); yield break; }
                float t0 = bot.Now, lastProgress = bot.Now, best = float.MaxValue, recovering = 0f;
                int hops = 0;
                while (true)
                {
                    if (bot.Failed) { bot.Driver.Stop(c); yield break; }
                    if (c.State == CharacterState.Captured || CaptiveBody.IsSeized(c))
                    {
                        bot.Driver.Stop(c);
                        r.Captured = true;
                        bot.Warn("CAPTURED during SafeWalk " + title + " at " + BotFmt.V(c.Feet) + " (" + c.LastCause + ")");
                        yield break;
                    }
                    if (!c.IsAlive) { bot.Driver.Stop(c); bot.Fail("SafeWalk " + title + ": " + c.DisplayName + " is " + c.State + " (" + c.LastCause + ")", c); yield break; }
                    var cond = c.Condition;
                    bool tumbling = c.Motor != null && c.Motor.IsTumbling;
                    if (cond == CharacterCondition.Thrown || cond == CharacterCondition.Stunned || (cond == CharacterCondition.Normal && tumbling))
                    {
                        bot.Driver.Stop(c);
                        recovering += 1f / 60f;
                        if (recovering > 6f) { bot.Fail("SafeWalk " + title + ": did not recover from " + cond + " in 6 s", c); yield break; }
                        lastProgress = bot.Now;
                        yield return null;
                        continue;
                    }
                    if (cond != CharacterCondition.Normal) { bot.Driver.Stop(c); bot.Fail("SafeWalk " + title + ": cannot move while " + cond, c); yield break; }
                    recovering = 0f;
                    Vector3 to = Flat(pts[i].Position - c.Feet);
                    float d = to.magnitude;
                    if (d <= tol) break;
                    if (d < best - 0.25f) { best = d; lastProgress = bot.Now; }
                    else if (bot.Now - lastProgress > bot.Options.StuckSeconds)
                    {
                        if (hops < 1)
                        {
                            hops++;
                            bot.Warn(c.DisplayName + " stuck " + BotFmt.F(d) + " m from " + pts[i].Describe() + " - hopping");
                            bot.Driver.PressJump(c);
                            lastProgress = bot.Now;
                        }
                        else
                        {
                            bot.Driver.Stop(c);
                            bot.Fail("SafeWalk " + title + ": STUCK " + BotFmt.F(d) + " m from " + pts[i].Describe() + " at " + BotFmt.V(c.Feet), c);
                            yield break;
                        }
                    }
                    if (bot.Now - t0 > timeoutPerPoint)
                    {
                        bot.Driver.Stop(c);
                        bot.Fail("SafeWalk " + title + ": timeout " + timeoutPerPoint + " s, still " + BotFmt.F(d) + " m from " + pts[i].Describe(), c);
                        yield break;
                    }
                    float mag = Mathf.Clamp(d / 1.2f, 0.35f, 1f);
                    bot.Driver.SetMove(c, to / d * mag, sprint && d > 2.5f);
                    bot.Driver.SetJumpHeld(c, hops > 0 && bot.Now - lastProgress < 0.4f);
                    yield return null;
                }
            }
            bot.Driver.Stop(c);
            float t1 = bot.Now;
            while (bot.Now - t1 < 0.5f && c.Motor != null && c.Motor.PlanarVelocity.magnitude > 0.4f) yield return null;
            bot.Log("OK   SafeWalk " + title + "  " + (bot.Now - tStart).ToString("0.00") + "s  at " + BotFmt.V(c.Feet));
        }

        /// <summary>The route whose in-arena stretch the bots can see least of right now (their current facing).</summary>
        static BotTarget[] PickRoute(TeamBot bot, Arena a, BotTarget[][] routes)
        {
            if (routes.Length == 1) return routes[0];
            int best = 0, bestScore = int.MaxValue;
            string s = "";
            for (int i = 0; i < routes.Length; i++)
            {
                int score = Exposure(a, routes[i]);
                s += (i > 0 ? ", " : "") + "route" + i + "=" + score;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            bot.Log("route exposure (" + a.Tag + "): " + s + " -> route" + best);
            return routes[best];
        }

        /// <summary>Sample points (every 1.5 m) of a route that are inside the arena AND visible to some bot.</summary>
        static int Exposure(Arena a, BotTarget[] route)
        {
            int n = 0;
            for (int i = 0; i + 1 < route.Length; i++)
            {
                Vector3 p0 = route[i].Position, p1 = route[i + 1].Position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Planar(p0, p1) / 1.5f));
                for (int k = 0; k <= steps; k++)
                {
                    Vector3 p = Vector3.Lerp(p0, p1, k / (float)steps);
                    foreach (var b in a.Bots)
                        if (Active(b) && b.InArena(p, 0.5f) && L3BotSight.WouldSee(b, p)) { n++; break; }
                }
            }
            return n;
        }

        /// <summary>Cross a bot arena: walk to its edge ('approach', or 'fromCell' when c starts in the holding cell), make
        /// sure the lure holds, then sprint the least visible route. Captured? Rescue and go again from the cell.</summary>
        static IEnumerator Cross(TeamBot bot, Arena a, ShifterCharacter lure, ShifterCharacter c, BotTarget[] approach, BotTarget[] fromCell,
                                 BotTarget[][] routes, float tolerance)
        {
            var r = new WalkResult();
            var cell = LevelMark.Find<HoldingCell>(a.Cell);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                bool inCell = cell != null && cell.Contains(c.Feet);
                yield return SafeWalk(bot, c, inCell ? fromCell : approach, false, 0.6f, r);
                if (bot.Failed) yield break;
                if (!r.Captured)
                {
                    if (!LureHolds(a, lure)) { yield return LureBots(bot, lure, a, 30f); if (bot.Failed) yield break; }
                    yield return SafeWalk(bot, c, PickRoute(bot, a, routes), true, tolerance, r);
                    if (bot.Failed || !r.Captured) yield break;
                }
                yield return Rescue(bot, a, lure, c);
                if (bot.Failed) yield break;
            }
            bot.Fail(a.Tag + ": " + c.DisplayName + " still not across after 5 attempts", c);
        }

        /// <summary>A bot grabbed 'prisoner': the lure walks the gallery to the CELL RELEASE lever and pulls it (the cell then
        /// frees everybody inside, including whoever is still being carried there), walks back, waits for the prisoner to
        /// walk free and re-lures. Fails after 4 captures in one arena.</summary>
        static IEnumerator Rescue(TeamBot bot, Arena a, ShifterCharacter lure, ShifterCharacter prisoner)
        {
            a.Captures++;
            bot.Warn("RESCUE #" + a.Captures + " in " + a.Tag + ": " + BotFmt.Name(prisoner) + " was captured - " + BotFmt.Name(lure) + " goes for " + a.Lever);
            if (a.Captures > 4) { bot.Fail(a.Tag + ": " + a.Captures + " captures - the lure is not holding the bots", prisoner); yield break; }
            if (!lure.IsAlive) { bot.Fail(a.Tag + ": the lure is " + lure.State + " too - nobody left to pull " + a.Lever, lure); yield break; }
            a.LureBusy = true;
            var cell = LevelMark.Find<HoldingCell>(a.Cell);
            if (cell == null) { bot.Fail("cell missing: " + a.Cell); yield break; }
            if (!cell.IsOpen)
            {
                yield return bot.WalkPath(lure, a.PerchToLever, false, 0.4f);
                if (bot.Failed) yield break;
                yield return bot.Interact(lure, a.Lever);
                if (bot.Failed) yield break;
                yield return bot.ExpectPowered(a.Cell, true, 2f);
                if (bot.Failed) yield break;
                yield return bot.WalkPath(lure, a.LeverToPerch, false, 0.25f);
                if (bot.Failed) yield break;
                yield return bot.Face(lure, a.Face);
                if (bot.Failed) yield break;
            }
            yield return bot.WaitUntil(() => prisoner.State == CharacterState.Alive && !CaptiveBody.IsSeized(prisoner), 40f,
                                       prisoner.DisplayName + " walks free from " + a.Cell);
            if (bot.Failed) yield break;
            yield return LureBots(bot, lure, a, 45f);
            a.LureBusy = false;
        }

        /// <summary>-l3ForceCapture: c walks straight into a patrolling bot of the arena (no lure yet) until it is grabbed,
        /// then the normal rescue runs.</summary>
        static IEnumerator ForcedCapture(TeamBot bot, Arena a, ShifterCharacter lure, ShifterCharacter c)
        {
            var target = a.Bots[0];
            bot.Warn("FORCED CAPTURE TEST (" + a.Tag + "): " + BotFmt.Name(c) + " walks into " + target.name);
            float t0 = bot.Now;
            while (bot.Now - t0 < 25f)
            {
                if (c.State == CharacterState.Captured || CaptiveBody.IsSeized(c)) break;
                if (bot.Failed || !c.IsAlive) { bot.Driver.Stop(c); yield break; }
                Vector3 to = Flat(target.transform.position - c.Feet);
                bot.Driver.SetMove(c, to.sqrMagnitude > 0.01f ? to.normalized : Vector3.zero, false);
                yield return null;
            }
            bot.Driver.Stop(c);
            if (c.State != CharacterState.Captured) { bot.Warn("FORCED CAPTURE (" + a.Tag + ") did not happen in 25 s"); yield break; }
            yield return Rescue(bot, a, lure, c);
        }

        // ================================================================== S1 bot hall
        static IEnumerator S1(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var a = S1Arena();
            if (!a.Resolve(bot)) yield break;
            // leave decontamination without crossing paths: exits sorted by spawn x
            var byX = new List<ShifterCharacter> { brute, spr, brain };
            byX.Sort((p, q) => p.Feet.x.CompareTo(q.Feet.x));
            BotTarget ExitOf(ShifterCharacter c) => new Vector3(-1.5f + 1.5f * byX.IndexOf(c), 0f, 7.6f);

            yield return bot.Parallel(
                bot.WalkPath(brain, new List<BotTarget> { ExitOf(brain), "S1.InDoor", "S1.StairApproach", "S1.StairBottom", "S1.StairTop", "S1.GalleryMid", "S1.GalleryBendS", "S1.PulpitEntry", "S1.Pulpit" }, false, 0.25f),
                After(bot, 2.5f, bot.WalkPath(brute, false, ExitOf(brute), "S1.InDoorW", "S1.Entry")),
                After(bot, 4.0f, bot.WalkPath(spr, false, ExitOf(spr), "S1.InDoorW2", "S1.Entry2")));
            if (bot.Failed) yield break;
            yield return bot.Face(brain, a.Face);
            if (ForceCapture) { yield return ForcedCapture(bot, a, brain, brute); if (bot.Failed) yield break; }
            yield return LureBots(bot, brain, a, 40f);
            if (bot.Failed) yield break;

            bool s1Done = false;
            yield return bot.Parallel(Then(CrossS1(bot, a, brain, brute, spr), () => s1Done = true), KeepTaunting(bot, brain, a, () => s1Done, 400f));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S1.Latch", true, 1f);
            yield return bot.ExpectPowered("S1.Door", true, 3f);
            if (bot.Failed) yield break;
            yield return S1Exit(bot, brute, spr, brain);
        }

        static IEnumerator CrossS1(TeamBot bot, Arena a, ShifterCharacter lure, ShifterCharacter brute, ShifterCharacter spr)
        {
            var fromCell = new BotTarget[] { "S1.CellExit", "S1.Cross1" };
            // Sprinter crosses first and extends the catwalk from the far landing
            yield return Cross(bot, a, lure, spr, new BotTarget[] { "S1.Cross1" }, fromCell, new[]
            {
                new BotTarget[] { "S1.Cross1", "S1.Cross2", "S1.Cross3", "S1.FarMid" },
                new BotTarget[] { "S1.Cross1", "S1.WallS", "S1.WallN", "S1.FarMid" },
            }, 0.8f);
            if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, false, "S1.FarStairsBottom", "S1.Landing");
            if (bot.Failed) yield break;
            yield return bot.Interact(spr, "S1.CatwalkLever");
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S1.Catwalk", true, 2f);
            var catwalk = LevelMark.Find<RetractableBridge>("S1.Catwalk");
            yield return bot.WaitUntil(() => catwalk != null && catwalk.Position01 > 0.98f, 6f, "catwalk extended");
            if (bot.Failed) yield break;

            // Brute crosses and holds the plate (1 s latch)
            yield return Cross(bot, a, lure, brute, new BotTarget[] { "S1.Cross1" }, fromCell, new[]
            {
                new BotTarget[] { "S1.Cross1", "S1.Cross2", "S1.Cross3", "S1.Cross4", "S1.PlateSpot" },
                new BotTarget[] { "S1.Cross1", "S1.WallS", "S1.WallN", "S1.Cross4", "S1.PlateSpot" },
            }, 0.3f);
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => TeamBot.IsPowered("S1.Latch"), 4f, "40 kg plate held 1 s (latch)");
        }

        static IEnumerator S1Exit(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // everyone through the door; the lure walks the gallery + catwalk (bot follows below, then gives up)
            yield return bot.WalkPath(brute, false, "S1.PlateSide", "S1.DoorFrontL", "S1.ThroughL", "S1.Beyond");
            if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, false, "S1.FarStairsBottom", "S1.DoorFrontR", "S1.ThroughR", "S1.Beyond2");
            if (bot.Failed) yield break;
            yield return bot.WalkPath(brain, false, "S1.PulpitEntry", "S1.ProwRoot", "S1.GalleryBend", "S1.GalleryEnd", "S1.CatwalkMid", "S1.Landing", "S1.FarStairsBottom", "S1.DoorFront", "S1.Through", "S1.Beyond3");
            if (bot.Failed) yield break;
            yield return ExpectCheckpoint(bot, 1, 4f);
        }

        // ================================================================== S2 timed lab door
        static IEnumerator S2(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.Parallel(
                bot.WalkPath(spr, false, "S2.ButtonSide", "S2.ButtonStand"),
                bot.WalkTo(brute, "S2.Gather"),
                bot.WalkTo(brain, "S2.Gather2"));
            if (bot.Failed) yield break;
            yield return bot.Interact(spr, "S2.Button", "S2.ButtonStand");
            if (bot.Failed) yield break;
            float pressed = bot.Now;
            yield return bot.WalkPath(spr, new List<BotTarget> { "S2.RunMid", "S2.RunEnd" }, true, 0.8f);
            if (bot.Failed) yield break;
            bot.Log("timed run: " + (bot.Now - pressed).ToString("0.00") + " s (window " + LevelMark.Find<TimedButton>("S2.Button").Window.ToString("0.00") + " s)");
            yield return bot.Interact(spr, "S2.Lever", "S2.LeverStand");
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S2.Door", true, 3f);
            yield return bot.Parallel(
                bot.WalkPath(brute, false, "S2.WalkApproachL", "S2.WalkStartL", "S2.WalkEndL", "S2.InRoomL", "S2.ControlRoom"),
                After(bot, 0.6f, bot.WalkPath(brain, false, "S2.ButtonEast", "S2.WalkStartR", "S2.WalkEndR", "S2.InRoomR", "S2.ControlRoom2")));
            if (bot.Failed) yield break;
            yield return ExpectCheckpoint(bot, 2, 4f);
        }

        // ================================================================== S3 the volunteer chair
        static IEnumerator S3(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.WalkPath(brain, false, "S3.GapS", "S3.GapN", "S3.ChairApproach", "S3.ChairStand");
            if (bot.Failed) yield break;
            yield return bot.Interact(brain, "S3.Chair", "S3.ChairStand", false);
            if (bot.Failed) yield break;
            yield return bot.ExpectCondition(brain, CharacterCondition.Seated, 2f);
            yield return bot.ExpectPowered("S3.Bridge", true, 2f);
            var bridge = LevelMark.Find<RetractableBridge>("S3.Bridge");
            yield return bot.WaitUntil(() => bridge != null && bridge.Position01 > 0.98f, 6f, "hard-light bridge extended");
            if (bot.Failed) yield break;

            yield return bot.Parallel(
                bot.WalkPath(brute, false, "S3.GapS", "S3.GapN", "S3.BridgeStartL", "S3.BridgeEndL", "S3.FarWait"),
                After(bot, 1.8f, bot.WalkPath(spr, false, "S3.GapS2", "S3.GapN2", "S3.BridgeStartR", "S3.BridgeEndR", "S3.ReleaseStand")));
            if (bot.Failed) yield break;
            yield return bot.Interact(spr, "S3.Release", "S3.ReleaseStand");
            if (bot.Failed) yield break;
            yield return bot.ExpectCondition(brain, CharacterCondition.Normal, 2f);
            yield return bot.ExpectPowered("S3.TunnelDoor", true, 3f);
            // the sacrifice: the bridge only holds for the TURBO window - the Brainiac doesn't even try
            yield return bot.WaitUntil(() => !TeamBot.IsPowered("S3.Bridge"), 8f, "bridge retracted after the grace window");
            if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, false, "S3.FarWait2");
            if (bot.Failed) yield break;

            // recovery through the maintenance tunnel
            yield return bot.WalkPath(brain, false, "S3.TunnelIn", "S3.TunnelA");
            if (bot.Failed) yield break;
            yield return bot.JumpGap(brain, "S3.Jump1Takeoff", "S3.Jump1Landing", true, 3.0f);
            if (bot.Failed) yield break;
            yield return bot.JumpGap(brain, "S3.Jump2Takeoff", "S3.Jump2Landing", true, 2.5f);
            if (bot.Failed) yield break;
            yield return bot.ClimbOnto(brain, "S3.StepUp");
            if (bot.Failed) yield break;
            yield return bot.ClimbOnto(brain, "S3.CatwalkIn");
            if (bot.Failed) yield break;
            // the pipe catwalk is out of the tunnel bot's reach; if the Brainiac gets grabbed anyway, the Sprinter frees it
            var tr = new WalkResult();
            yield return SafeWalk(bot, brain, new BotTarget[] { "S3.CatwalkMid", "S3.CatwalkOut", "S3.StepDown", "S3.RoomExit", "S3.TunnelOut", "S3.Rejoin" }, false, 0.6f, tr);
            if (bot.Failed) yield break;
            if (tr.Captured)
            {
                bot.Warn("RESCUE in S3: " + BotFmt.Name(brain) + " was captured in the tunnel - " + BotFmt.Name(spr) + " pulls S3.CellLever");
                yield return bot.Interact(spr, "S3.CellLever");
                if (bot.Failed) yield break;
                yield return bot.ExpectPowered("S3.Cell", true, 2f);
                yield return bot.WaitUntil(() => brain.State == CharacterState.Alive && !CaptiveBody.IsSeized(brain), 40f, brain.DisplayName + " walks free from S3.Cell");
                if (bot.Failed) yield break;
                yield return SafeWalk(bot, brain, new BotTarget[] { "S3.CellExit", "S3.TunnelOut", "S3.Rejoin" }, false, 0.6f, tr);
                if (bot.Failed) yield break;
                if (tr.Captured) { bot.Fail("S3: captured again on the way out of the tunnel cell", brain); yield break; }
                yield return bot.WalkPath(spr, false, "S3.FarWait2");
                if (bot.Failed) yield break;
            }
            yield return bot.ExpectPowered("S3.Gate", true, 4f);
            if (bot.Failed) yield break;
            yield return bot.Parallel(
                bot.WalkPath(brain, false, "S3.GateFront3", "S4.GateIn3"),
                After(bot, 1.2f, bot.WalkPath(brute, false, "S3.GateFront", "S4.GateIn", "S4.Entry")),
                After(bot, 2.6f, bot.WalkPath(spr, false, "S3.GateFront2", "S4.GateIn2", "S4.Entry2")));
            if (bot.Failed) yield break;
            yield return ExpectCheckpoint(bot, 3, 4f);
        }

        // ================================================================== S4 distraction gauntlet
        static IEnumerator S4(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var a = S4Arena();
            if (!a.Resolve(bot)) yield break;
            yield return bot.WalkPath(brain, new List<BotTarget> { "S4.StairApproach", "S4.StairBottom", "S4.StairTop", "S4.GalleryMid", "S4.GalleryBendS", "S4.PulpitEntry", "S4.Pulpit" }, false, 0.25f);
            if (bot.Failed) yield break;
            yield return bot.Face(brain, a.Face);
            if (ForceCapture) { yield return ForcedCapture(bot, a, brain, brute); if (bot.Failed) yield break; }
            yield return LureBots(bot, brain, a, 45f);
            if (bot.Failed) yield break;

            bool s4Done = false;
            yield return bot.Parallel(Then(PlateTeamS4(bot, a, brain, brute, spr), () => s4Done = true), KeepTaunting(bot, brain, a, () => s4Done, 400f));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S4.Latch", true, 1f);
            yield return bot.ExpectPowered("S4.Door", true, 3f);
            yield return bot.ExpectPowered("S4.Hatch", true, 1f);
            if (bot.Failed) yield break;

            yield return bot.WalkPath(spr, false, "S4.NorthOfPlate", "S4.DoorFront", "S4.Lobby", "S4.LobbyWait2");
            if (bot.Failed) yield break;
            yield return bot.WalkPath(brute, false, "S4.DoorFront", "S4.Lobby", "S4.LobbyWait");
            if (bot.Failed) yield break;
            yield return bot.WalkPath(brain, false, "S4.PulpitEntry", "S4.GalleryBendN", "S4.GalleryNorth", "S4.HatchFront", "S4.HatchBeyond", "S4.LobbyStairsBottom", "S4.CabFront");
            if (bot.Failed) yield break;

            yield return bot.Parallel(
                bot.WalkPath(brain, false, "S4.CabC"),
                After(bot, 0.6f, bot.WalkPath(brute, false, "S4.CabFrontL", "S4.CabL")),
                After(bot, 1.6f, bot.WalkPath(spr, false, "S4.CabFrontR", "S4.CabR")));
            if (bot.Failed) yield break;
            yield return bot.ExpectLevelComplete(15f);
        }

        static IEnumerator PlateTeamS4(TeamBot bot, Arena a, ShifterCharacter lure, ShifterCharacter brute, ShifterCharacter spr)
        {
            var fromCell = new BotTarget[] { "S4.CellExit", "S4.Cross1" };
            // Brute to the 100 kg plate (the whole plate is out of the bots' reach), then the Sprinter next to it
            yield return Cross(bot, a, lure, brute, new BotTarget[] { "S4.Cross1" }, fromCell, new[]
            {
                new BotTarget[] { "S4.Cross1", "S4.LaneS", "S4.LaneMid", "S4.LaneN", "S4.PlateApproach", "S4.PlateSpot" },
                new BotTarget[] { "S4.Cross1", "S4.Cross2", "S4.PlateSpot" },
            }, 0.3f);
            if (bot.Failed) yield break;
            yield return Cross(bot, a, lure, spr, new BotTarget[] { "S4.Cross1" }, fromCell, new[]
            {
                new BotTarget[] { "S4.Cross1", "S4.LaneS", "S4.LaneMid", "S4.LaneN", "S4.PlateApproach", "S4.PlateSide" },
                new BotTarget[] { "S4.Cross1", "S4.Cross2b", "S4.PlateSide" },
            }, 0.6f);
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => TeamBot.IsPowered("S4.Latch"), 8f, "100 kg plate held 3 s (latch)");
        }
    }
}
