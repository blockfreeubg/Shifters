// =====================================================================================
//  SHIFTERS - L14Solution.cs   [SHIFT 14 - ALL IN FAVOR? TeamBot solution, no debug assists]
//  S0 C1 -> S1 three exhibits in parallel (Brute pushes the cabinet; Sprinter stacks on the Brainiac, takes the Books
//  to tray B; the Brainiac hacks the evidence locker) -> C2 -> S2 the Sprinter climbs the falling words and MUTEs
//  while the others take voter plates -> C3 -> S3 AYE, SHIFT, AYE, SHIFT, AYE -> S4 the catch (seed 2: nobody moves,
//  MAX: team catch) -> the bench for three -> C15 + WHO RULES TODAY (AutoSkip: seeded pick; story ON: a real Switch)
//  -> the balcony. Seed 1 also shoots HUMBLE and takes the screenshots.
//  Run: run_level_test.sh <proj> 14 1,2,3,MIN,MAX     (story ON: BOT_STORY=1 run_level_test.sh <proj> 14 1)
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L14Solution
    {
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, Options(), "L14");

        public static BotOptions Options() => new BotOptions { StrictPlayerThrows = true };

        static L14Hearing H => L14Hearing.Instance;

        static IEnumerator Seq(TeamBot bot, params IEnumerator[] steps)
        {
            foreach (var s in steps) { if (bot.Failed) yield break; yield return s; }
        }

        static void Shot(TeamBot bot, string name) { if (TeamBot.IsCurrentTeam(1)) bot.Ctx?.Shot("l14_" + name); }

        static void Overview(TeamBot bot, string name, Vector3 from, Vector3 at)
        {
            if (!TeamBot.IsCurrentTeam(1) || bot.Ctx == null) return;
            var go = new GameObject("L14ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 600f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            cam.enabled = false;
            bot.Ctx.Shot("l14_" + name, cam);
            Object.Destroy(go);
        }

        public static IEnumerator Solve(TeamBot bot) => Stages(bot, 1);

        /// <summary>L14Extras: > 0 = the first intern sits alone on the bench this long before the others join (idle test).</summary>
        public static float BenchIdleSeconds;
        /// <summary>L14Extras: after Done also seat an intern by script on the closed bench (must be released at once).</summary>
        public static bool ScriptedSeatCheck;
        /// <summary>-l14PickTimeout 1 (story ON): nobody switches at WHO RULES TODAY?; the 15 s timeout must pick the active intern.</summary>
        static bool PickTimeoutRun => BotArgs.Str("-l14PickTimeout", "0") == "1";

        /// <summary>from = 1 (C1, the whole chapter), 2 (team restored at C2: the filibuster onwards) or 3 (restored at
        /// C3: the vote onwards). L14Extras uses 2 and 3 for the Restart Checkpoint runs.</summary>
        public static IEnumerator Stages(TeamBot bot, int from)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("L14 team: " + bot.TeamSummary());
            bool story = !StoryPlayer.AutoSkip;
            yield return bot.WaitUntil(() => H != null, 10f, "L14Hearing present");
            if (story)
            {
                yield return bot.WaitUntil(() => StoryPlayer.Instance == null || !StoryPlayer.Instance.IsBlocking, 60f, "C14 finished");
                // C14 grabbed the team before the bot attached; its RestoreBlocking cleared ExternalControl: take it back
                foreach (var c in bot.Team) bot.TakeControl(c);
            }
            if (bot.Failed) yield break;
            Overview(bot, "1_hall", new Vector3(0f, 8.5f, 1.2f), new Vector3(0f, 1.5f, 30f));

            if (from <= 1)
            {
            // ---- 1. S0
            yield return bot.GatherAt("checkpoint.1");
            yield return bot.Expect(() => CheckpointManager.Instance != null && CheckpointManager.Instance.CurrentIndex >= 1, "C1 active", 3f);
            if (bot.Failed) yield break;

            // ---- 2. S1 THE CHARGES (three exhibits in parallel)
            var laneA = Seq(bot,
                bot.WalkPath(brute, true, "s0.doorE", "s1.laneE", "s1a.pushStart"),
                bot.PushBlock(brute, "s1a.cabinet", "s1a.scale.center"),
                bot.ExpectPowered("s1a.scale", true, 2f),
                // seed 3 (polite-word run): the Brute is the only intern free before the filibuster: it parks on POPULAR's footprint
                TeamBot.IsCurrentTeam(3) ? bot.WalkPath(brute, true, "s3.laneE1", "s3.laneE2", "s4.parkLane", "s4.popularPark") : bot.Wait(0f));
            var laneBC = Seq(bot,
                bot.Parallel(bot.WalkPath(brain, true, "s0.doorW", "s1.laneW", "s1b.stack"), bot.WalkPath(spr, true, "s0.doorC", "s1.laneW2", "s1b.approach")),
                bot.StackOnto(spr, brain),
                bot.ClimbOnto(spr, "s1b.shelfTop"),
                bot.Grab(spr, "s1b.books"),
                bot.Parallel(
                    bot.Hack(brain, "s1c.terminal", "s1c.stand"),
                    Seq(bot, bot.WalkPath(spr, false, "s1b.shelfEdge", "s1b.offShelf", "s1b.trayStand"), bot.Drop(spr), bot.ExpectPowered("s1b.tray", true, 3f))));
            yield return bot.Parallel(laneA, laneBC);
            if (bot.Failed) yield break;
            Shot(bot, "2_charges");
            yield return bot.WaitUntil(() => H.Phase >= L14Hearing.HearingPhase.Defense, 10f, "charges done -> Defense");
            yield return bot.Expect(() => CheckpointManager.Instance.CurrentIndex == 2, "C2 force-activated", 2f);
            if (bot.Failed) yield break;
            }

            // ---- 3. S2 THE FILIBUSTER: the Sprinter climbs his words; the others take voter plates
            if (from <= 2)
            {
            var climb = Seq(bot,
                from <= 1 ? bot.WalkPath(spr, true, "s2.laneS", "s2.laneS2", "s2.stairFoot") : bot.WalkPath(spr, true, "s2.laneS2", "s2.stairFoot"),
                bot.WaitUntil(() => H.WordLanded("I"), 12f, "word I landed"), bot.ClimbOnto(spr, "s2.step.I"),
                bot.WaitUntil(() => H.WordLanded("AM"), 8f, "word AM landed"), bot.ClimbOnto(spr, "s2.step.AM"),
                bot.WaitUntil(() => H.WordLanded("VERY"), 8f, "word VERY landed"), bot.ClimbOnto(spr, "s2.step.VERY"),
                ShotWords(bot),
                bot.WaitUntil(() => H.WordLanded("AND1"), 8f, "word AND landed"),
                bot.JumpGap(spr, "s2.pause.takeoff", "s2.pause.landing", true, 1.5f),
                bot.WaitUntil(() => H.WordLanded("TALL"), 8f, "word TALL landed"), bot.ClimbOnto(spr, "s2.step.TALL"),
                bot.WaitUntil(() => H.WordLanded("AND2"), 8f, "word AND2 landed"), bot.ClimbOnto(spr, "s2.step.AND2"),
                bot.WalkTo(spr, "s2.muteStand"),
                bot.Interact(spr, "s2.mute"));
            yield return bot.Parallel(climb,
                TeamBot.IsCurrentTeam(3) && from <= 1 ? PoliteWord(bot, brute) : from <= 1 ? bot.WalkPath(brute, false, "s3.laneE1", "s3.laneE2", "s3.v3.stand") : bot.WalkPath(brute, false, "s3.laneE2", "s3.v3.stand"),
                bot.WalkPath(brain, false, "s3.laneW", "s3.v1.stand"));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s2.fold", true, 3f);
            yield return bot.Expect(() => CheckpointManager.Instance.CurrentIndex == 3, "C3 force-activated", 2f);
            if (bot.Failed) yield break;
            if (TeamBot.IsCurrentTeam(1))
            {
                yield return bot.WaitUntil(() => H.WordLanded("HUMBLE"), 6f, "HUMBLE landed");
                yield return bot.ShootWithGun("s2.humble", () => H.HumbleDown, 8, 12f);
                yield return bot.Expect(() => H.HumbleDown, "HUMBLE shot down (optional gag)", 2f);
                if (bot.Failed) yield break;
            }

            }

            // ---- 4. S3 THE VOTE
            yield return bot.WaitUntil(() => H.WordLanded("POPULAR"), 6f, "POPULAR landed (catch mat)");
            if (from <= 2) yield return bot.WalkPath(spr, false, "s2.foldTop", "s2.foldFoot", "s3.v2.approach", "s3.v2.stand");
            else yield return bot.Parallel(bot.WalkPath(spr, false, "s3.v2.approach", "s3.v2.stand"), bot.WalkTo(brain, "s3.v1.stand"), bot.WalkPath(brute, false, "s3.v3.stand"));
            if (bot.Failed) yield break;
            Shot(bot, "3_vote");
            if (story)
            {
                // the bribe (optional gag): the Sprinter opens envelope 2, steps off its plate, gets a paper crown, steps back on
                yield return bot.Interact(spr, "s3.bribe2", "s3.bribe2.stand");
                yield return bot.Expect(() => H.Bribes == 1 && !H.PlateOccupied(1, out _), "bribe: the Sprinter stepped off plate 2", 2f);
                yield return bot.WaitUntil(() => !H.BribeActive, 6f, "bribe beat over");
                yield return bot.WalkTo(spr, "s3.v2.stand");
                if (bot.Failed) yield break;
            }
            yield return bot.Interact(brain, "s3.aye1", "s3.v1.stand");
            yield return bot.Interact(spr, "s3.aye2", "s3.v2.stand");
            yield return bot.Interact(brute, "s3.aye3", "s3.v3.stand");
            yield return bot.ExpectPowered("s3.vote", true, 1f);
            if (bot.Failed) yield break;

            // ---- 5. THE CATCH
            yield return bot.WaitUntil(() => H.Phase == L14Hearing.HearingPhase.Catch, 3f, "Phase Catch");
            if (TeamBot.IsCurrentTeam(2))
            {
                yield return bot.Expect(() => H.Missed && H.Touchdown, "nobody moved: Max landed on POPULAR (ch14.landed)", 9f);
            }
            else if (TeamBot.CurrentTeamKey == "MAX")
            {
                yield return bot.Parallel(Seq(bot, bot.WalkPath(spr, true, "s4.catch.lane"), bot.WalkTo(spr, "s4.catch.S", true, 0.3f)), bot.WalkTo(brain, "s4.catch.SW", true, 0.3f), bot.WalkTo(brute, "s4.catch.SE", true, 0.3f));
                yield return bot.Expect(() => H.Touchdown && H.Caught && H.CatcherCount == 3, "TEAM CATCH by 3", 9f);
            }
            else
            {
                yield return bot.WalkPath(spr, true, "s4.catch.lane");
                yield return bot.WalkTo(spr, "s4.catch.S", true, 0.3f);
                yield return ShotCatch(bot);
                yield return bot.Expect(() => H.Touchdown && H.Caught && H.Catcher == spr, "the Sprinter caught Max", 9f);
            }
            if (bot.Failed) yield break;

            // ---- 6. THE BENCH FOR THREE
            yield return bot.WaitUntil(() => H.Phase == L14Hearing.HearingPhase.Bench, 8f, "Phase Bench (throne folded)");
            yield return bot.Parallel(
                bot.WalkPath(brain, false, "s4.lane.W0", "s4.lane.W1", "s4.lane.W2", "s4.seatStand1"),
                bot.WalkPath(spr, false, "s4.lane.E", "s4.ramp.C", "s4.seatStand2"),
                bot.WalkPath(brute, false, "s4.lane.E2", "s4.ramp.E", "s4.seatStand3"));
            if (bot.Failed) yield break;
            var sitters = TeamBot.IsCurrentTeam(3) ? new[] { brute, spr, brain } : new[] { brain, spr, brute };
            var chairs = TeamBot.IsCurrentTeam(3) ? new[] { "s4.seat3", "s4.seat2", "s4.seat1" } : new[] { "s4.seat1", "s4.seat2", "s4.seat3" };
            for (int i = 0; i < 3; i++)
            {
                yield return bot.SitIn(sitters[i], chairs[i]);
                if (bot.Failed) yield break;
                if (i == 0 && BenchIdleSeconds > 0f)
                {
                    var first = sitters[0];
                    bot.Log("[L14] bench idle: " + first.DisplayName + " sits alone for " + BenchIdleSeconds + " s");
                    yield return bot.Wait(BenchIdleSeconds);
                    yield return bot.Expect(() => first.Condition == CharacterCondition.Seated && first.IsAlive && H.Phase == L14Hearing.HearingPhase.Bench && !H.BenchFull,
                        "bench idle " + BenchIdleSeconds + " s: " + first.DisplayName + " still seated, phase still Bench (no timer, no auto-release, nothing breaks)", 0f);
                    foreach (var c in bot.Team)
                        yield return bot.Expect(() => c.IsAlive && c.State == CharacterState.Alive && c.Health >= c.MaxHealth - 0.01f, c.DisplayName + " unharmed after the bench idle", 0f);
                    if (bot.Failed) yield break;
                }
            }
            yield return bot.ExpectPowered("s4.bench", true, 1.5f);
            if (bot.Failed) yield break;
            Shot(bot, "5_bench");

            // ---- 7. WHO RULES TODAY?
            var cm = CharacterManager.Instance;
            if (!story)
            {
                yield return bot.WaitUntil(() => ShiftocracyEnding.Instance != null && ShiftocracyEnding.Instance.President != null, 10f, "President picked (AutoSkip)");
                if (bot.Failed) yield break;
                int seed = GameSession.Instance != null ? GameSession.Instance.Seed : 0, n = cm.Team.Count;
                var expected = cm.Team[((seed % n) + n) % n];
                yield return bot.Expect(() => ShiftocracyEnding.Instance.President == expected && H.VistaShown == ShiftocracyEnding.Instance.PresidentArchetype,
                    "seeded pick " + expected.DisplayName + " + its vista set", 2f);
            }
            else
            {
                yield return bot.WaitUntil(() => ShiftocracyEnding.Instance != null && ShiftocracyEnding.Instance.Picking, 60f, "WHO RULES TODAY? prompt open");
                if (bot.Failed) yield break;
                if (PickTimeoutRun)
                {
                    var active = cm.Active; float took = 0f;
                    bot.Log("[L14] pick-timeout run: no input at WHO RULES TODAY? (active " + active.DisplayName + ")");
                    // same clock as ShiftocracyEnding's timer (Time.unscaledTime does not advance in the bot's sim stepping)
                    while (ShiftocracyEnding.Instance.President == null && took < ShiftocracyEnding.PickTimeout + 15f) { took += ShiftersCamera.UnscaledDeltaTime * Mathf.Max(0.01f, StoryPlayer.TimeScale); yield return null; }
                    var pres = ShiftocracyEnding.Instance.President;
                    bot.Log("[L14] pick timeout fired after " + took.ToString("0.0") + " s -> " + (pres != null ? pres.DisplayName : "nobody"));
                    yield return bot.Expect(() => pres == active, "pick timeout (no input) -> the active intern " + active.DisplayName + " rules", 0f);
                    yield return bot.Expect(() => took >= ShiftocracyEnding.PickTimeout * 0.8f, "the timeout waited ~" + ShiftocracyEnding.PickTimeout + " s (" + took.ToString("0.0") + " s)", 0f);
                    yield return bot.Expect(() => H.VistaShown == ShiftocracyEnding.Instance.PresidentArchetype, "the president's vista set rose (" + H.VistaShown + ")", 30f);
                }
                else
                {
                    ShifterCharacter pick = null;
                    foreach (var c in cm.Team) if (c != null && c != cm.Active) { pick = c; break; }
                    yield return bot.Switch(pick);
                    yield return bot.Expect(() => ShiftocracyEnding.Instance.President == pick, "real pick by Switch -> " + pick.DisplayName, 4f);
                }
            }
            yield return bot.ExpectPowered("s5.balconyDoor", true, story ? 60f : 8f);
            yield return bot.WaitUntil(() => H.Phase == L14Hearing.HearingPhase.Done, story ? 60f : 8f, "Phase Done");
            if (bot.Failed) yield break;
            if (story)
            {
                yield return bot.WaitUntil(() => StoryPlayer.Instance == null || !StoryPlayer.Instance.IsBlocking, 60f, "C15 finished");
                foreach (var c in bot.Team) bot.TakeControl(c);
            }
            yield return bot.Wait(story ? 0.5f : 2.0f);
            // the adjourned bench is closed: E on it does nothing (it was a seated softlock: the exit needs all three)
            for (int s = 0; s < H.Seats.Length; s++)
            {
                var seat = H.Seats[s];
                if (seat == null) continue;
                seat.Interact(bot.Team[s % bot.Team.Count]);   // what E does (one toast per seat: "MEETING ADJOURNED...")
                yield return bot.Wait(0.1f);
                yield return bot.Expect(() => !seat.Open && !seat.IsOccupied && bot.Team.TrueForAll(c => c.Condition == CharacterCondition.Normal && !seat.CanInteract(c, out _)),
                    "after Done bench seat " + (s + 1) + " is closed: E does not seat anyone", 0f);
                if (ScriptedSeatCheck)
                {
                    var who = bot.Team[s % bot.Team.Count];
                    seat.SeatCharacter(who);
                    yield return bot.Expect(() => !seat.IsOccupied && who.Condition == CharacterCondition.Normal, "a scripted sit on closed seat " + (s + 1) + " is released at once", 1f);
                }
            }
            if (bot.Failed) yield break;
            Overview(bot, "5b_bench_sunrise", new Vector3(0f, 3.4f, 33.2f), new Vector3(0f, 1.6f, 39.5f));
            Overview(bot, "6_vista", new Vector3(-7f, 7.5f, 50.5f), new Vector3(0f, -3f, 84f));

            // ---- 8. THE BALCONY OF THREE
            yield return bot.Parallel(
                bot.WalkPath(brain, false, "s5.lane.W", "s5.lane.W2", "s5.front.W", "s5.door.W"),
                bot.WalkPath(brute, false, "s5.lane.E", "s5.lane.E2", "s5.front.E", "s5.door.E"),
                Seq(bot, bot.Wait(0.8f), bot.WalkPath(spr, false, "s5.lane.E", "s5.lane.E2", "s5.front.C", "s5.door.C")));
            if (bot.Failed) yield break;
            yield return bot.EnterExit();
            yield return bot.ExpectLevelComplete();
            if (bot.Failed) yield break;
            bot.Log("[L14] timeline hash " + H?.TimelineHash + " (all runs: " + string.Join(",", L14Hearing.Hashes) + ")");
            if (from <= 1 && L14Hearing.Hashes.Count > 1)   // restored runs (L14Extras) start mid-timeline
                yield return bot.Expect(() => L14Hearing.Hashes.TrueForAll(h => h == L14Hearing.Hashes[0]), "timeline hash identical across seeds", 0f);
        }

        /// <summary>Seed 3: the Brute parks on POPULAR's footprint before the filibuster: the word hovers ("...ahem."),
        /// nudges it out after 10 s, lands; then the Brute takes voter plate 3.</summary>
        static IEnumerator PoliteWord(TeamBot bot, ShifterCharacter who)
        {
            var w = H.Words[3];
            yield return bot.WalkTo(who, "s4.popularPark", true, 0.3f);
            yield return bot.WaitUntil(() => w.HoverCount > 0, 12f, "POPULAR hovers over the parked " + who.DisplayName);
            yield return bot.WaitUntil(() => w.Landed, 16f, "POPULAR nudged " + who.DisplayName + " out after 10 s and landed");
            yield return bot.Expect(() => w.TotalHover >= 9.5f && who.IsAlive && who.Condition == CharacterCondition.Normal, "polite word: hovered >= 10 s, nobody hurt", 0f);
            yield return bot.WalkPath(who, false, "s4.parkLane", "s3.v3.stand");
        }

        static IEnumerator ShotWords(TeamBot bot) { Overview(bot, "4_filibuster", new Vector3(5.5f, 5.5f, 12.5f), new Vector3(14f, 2.2f, 22.5f)); yield break; }

        static IEnumerator ShotCatch(TeamBot bot)
        {
            if (TeamBot.IsCurrentTeam(1))
            {
                float t0 = Time.time;
                while (H != null && !H.Touchdown && Time.time - t0 < 0.9f) yield return null;
                Overview(bot, "4b_catch", new Vector3(-6.5f, 3.2f, 25.5f), new Vector3(-2f, 2.2f, 32f));
                Overview(bot, "4c_catch_wide", new Vector3(5f, 4f, 24f), new Vector3(-0.7f, 6.5f, 36.5f));
            }
        }
    }
}
