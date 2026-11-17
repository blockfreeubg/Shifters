// =====================================================================================
//  SHIFTERS - BotFrameworkTests.cs   [BOTS module - DevPlay scenarios]
//    Shifters.BotFrameworkTests.All      unit checks + the builder's L0_Sandbox built at runtime with the real kits,
//                                        solved by L0Solution on 3 seeds (-botSeeds to override) + smoke audit of it
//    Shifters.BotFrameworkTests.Unit     framework logic only (no characters needed)
//    Shifters.BotFrameworkTests.MiniRun  the bots' own code-built course (fallback while no sandbox exists): walk, 2 m gap,
//        stack to 2.5 m, throw onto 3.5 m, push 160 kg onto a 150 kg plate, hack, shoot, 5.2 m bridge, exit.
//  Sections whose module is still a contract stub are SKIPPED (logged), never faked.
//  Seeds: -botSeeds 1,2,3 (default 101,202,303).
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class BotFrameworkTests
    {
        public static IEnumerator All(DevPlayContext ctx)
        {
            yield return Unit(ctx);
            ctx.Log(BotCapabilities.Probe().Describe());
            yield return L0Solution.RunRuntime(ctx);
            yield return SmokeTests.Audit(ctx, "smoke_l0_runtime");
        }

        // ------------------------------------------------------------------ unit checks
        public static IEnumerator Unit(DevPlayContext ctx)
        {
            // BotRoutine: nesting, frame counting, exception capture
            int frames = 0;
            var r = new BotRoutine(Outer());
            while (r.Step()) frames++;
            ctx.Check(r.Error == null && frames == 4, "[BOTUNIT] BotRoutine nested enumerators step frame-by-frame (" + frames + " frames)");
            var bad = new BotRoutine(Throws());
            while (bad.Step()) { }
            ctx.Check(bad.Error != null && bad.Error.Message == "boom", "[BOTUNIT] BotRoutine captures exceptions instead of killing the run");

            // ballistics: solve -> simulate round trip
            int ok = 0, total = 0;
            foreach (float v in new[] { 15.9f, 17.4f, 19f })
            foreach (float dh in new[] { -1f, 0f, 1.3f })
            foreach (float gDown in new[] { 25f, 40f })
            {
                total++;
                float d = BotBallistics.RangeForHeight(v, 60f, dh, 25f, gDown);
                bool solved = BotBallistics.SolveElevation(v, d, dh, 25f, gDown, 15f, 75f, out float elev, 60f);
                var land = BotBallistics.Simulate(Vector3.zero, BotBallistics.Direction(Vector3.forward, elev) * v, 25f, gDown, dh);
                bool lowOk = BotBallistics.SolveElevation(v, d, dh, 25f, gDown, 15f, 75f, out float low) && low < 50f;
                if (solved && Mathf.Abs(elev - 60f) < 0.6f && lowOk && Mathf.Abs(land.z - d) < 0.1f) ok++;
                else ctx.Log("[BOTUNIT] ballistics miss v=" + v + " dh=" + dh + " g=" + gDown + " d=" + d + " elev=" + elev + " sim=" + land.z);
            }
            ctx.Check(ok == total, "[BOTUNIT] throw solver finds the flat arc and the preferred 60 deg lob, simulation lands within 10 cm (" + ok + "/" + total + ")");

            // targets: points, offsets, mark lookup by name fallback
            var go = new GameObject("BOT_TEST_MARK");
            go.transform.position = new Vector3(3f, 1f, -2f);
            BotTarget byName = "BOT_TEST_MARK";
            BotTarget point = new Vector3(1f, 2f, 3f);
            ctx.Check(byName.IsValid && (byName.Position - go.transform.position).sqrMagnitude < 1e-6f, "[BOTUNIT] mark id resolves (GameObject-name fallback, LevelMark type present=" + BotReflect.HasType("LevelMark") + ")");
            ctx.Check((point.Offset(Vector3.up).Position - new Vector3(1f, 3f, 3f)).sqrMagnitude < 1e-6f, "[BOTUNIT] BotTarget offset");
            BotTarget missing = "NO_SUCH_MARK_123";
            ctx.Check(!missing.IsValid && missing.Describe().Contains("MISSING"), "[BOTUNIT] missing mark is reported (" + missing.Describe() + ")");
            Object.Destroy(go);

            // failure path on a private context: failed check + dump file, later actions skipped
            var loose = new DevPlayContext { Result = new DevPlayResult(), Config = new DevPlayConfig { shotsDir = ctx.Config != null ? ctx.Config.shotsDir : "" } };
            var bot = TeamBot.Attach(loose, new BotOptions { DiagnosticShots = true });
            bot.Label = "unit_failure_probe";
            yield return bot.WalkTo(null, Vector3.zero);
            int stepsAfterFail = bot.Steps.Count;
            yield return bot.WalkTo(null, Vector3.one);
            bool dumpWritten = string.IsNullOrEmpty(loose.Config.shotsDir) || System.IO.File.Exists(System.IO.Path.Combine(loose.Config.shotsDir, "bot_unit_failure_probe_FAIL01.txt"));
            ctx.Check(bot.Failed && loose.Result.checksFailed == 1 && bot.Steps.Count == stepsAfterFail && dumpWritten,
                "[BOTUNIT] failure => failed check + dump file, later actions skipped (" + bot.FailReason + ")");
            bot.Dispose();
            ctx.Check(TeamBot.Current == null, "[BOTUNIT] Dispose detaches");

            // debug assists are refused unless explicitly allowed
            var loose2 = new DevPlayContext { Result = new DevPlayResult(), Config = new DevPlayConfig() };
            var bot2 = TeamBot.Attach(loose2, new BotOptions { DiagnosticShots = false });
            var dummy = new GameObject("BOT_DUMMY");
            yield return bot2.WalkTo(null, dummy.transform);
            ctx.Check(bot2.Failed && !bot2.UsedDebugAssists, "[BOTUNIT] actions on a missing character fail cleanly");
            bot2.Dispose();
            Object.Destroy(dummy);
            ctx.Log("[BOTUNIT] done");
        }

        static IEnumerator Outer() { yield return null; yield return Inner(); yield return null; }
        static IEnumerator Inner() { yield return null; yield return null; }
        static IEnumerator Throws() { yield return null; throw new System.Exception("boom"); }

        // ------------------------------------------------------------------ mini course
        public static IEnumerator MiniRun(DevPlayContext ctx)
        {
            var caps = BotCapabilities.Probe();
            ctx.Log(caps.Describe());
            BotMiniCourse course = null;
            int[] seeds = BotArgs.Seeds(101, 202, 303);
            IEnumerator Prepare(int seed)
            {
                if (course != null) course.Destroy();
                yield return null;
                yield return null;
                TeamBot.ForceTeam(seed);
                course = BotMiniCourse.Build(caps);
                course.SpawnTeam();
                yield return null;
            }
            int first = TeamBot.Results.Count;
            yield return TeamBot.RunSeeds(ctx, seeds, bot => Course(bot, course, caps), Prepare, new BotOptions(), "MiniCourse");
            for (int i = first; i < TeamBot.Results.Count; i++)
            {
                var res = TeamBot.Results[i];
                int skipped = 0;
                foreach (var s in res.Steps) if (s.Outcome == "SKIP") skipped++;
                ctx.Check(res.Passed, "[BOT] mini course " + res.Label + " passed (" + res.Seconds.ToString("0.0") + " s, " + skipped + " skipped)");
            }
            if (course != null) course.Destroy();
        }

        static IEnumerator Course(TeamBot bot, BotMiniCourse k, BotCapabilities caps)
        {
            var brute = bot.Brute; var sprinter = bot.Sprinter; var brain = bot.Brainiac; var gun = bot.GunOwner;
            yield return bot.Expect(() => brute != null && sprinter != null && brain != null && gun != null, "one Brute, one Sprinter, one Brainiac and one gun owner");
            bot.Snapshot("course_start");
            if (!caps.Motor) { bot.Skip("course", "CharacterMotor is still a contract stub - nothing can move yet"); yield break; }

            // 1. walk (all three at once)
            yield return bot.Parallel(
                bot.WalkTo(brute, new Vector3(-1.6f, 0f, -1.5f)),
                bot.WalkTo(sprinter, new Vector3(0f, 0f, -1.5f), true),
                bot.WalkTo(brain, new Vector3(1.6f, 0f, -1.5f)));

            // 2. hack the terminal (Brainiac, IQ >= 7)
            yield return bot.Hack(brain, k.Terminal);

            // 3. shoot the target (whoever has the gun)
            if (caps.Weapon) yield return bot.ShootAt(gun, k.Target);
            else bot.Skip("ShootAt", "WeaponHolder.FireAt is still a stub");

            // 4. push the 160 kg block onto the 150 kg plate (Brute)
            if (caps.Push && k.Block != null && k.Plate != null)
            {
                yield return bot.PushBlock(brute, k.Block, BotMiniCourse.PlatePos);
                yield return bot.ExpectPowered(k.Plate, true, 2f);
            }
            else bot.Skip("PushBlock", "Pushable / PuzzleKit.HeavyBlock / PuzzleKit.PressurePlate not published");

            // 5. everyone jumps the 2 m gap (three lanes, in parallel)
            yield return bot.Parallel(
                bot.JumpGap(brute, new Vector3(-2f, 0f, BotMiniCourse.GapAStart), new Vector3(-2f, 0f, BotMiniCourse.GapAEnd + 1.2f)),
                bot.JumpGap(sprinter, new Vector3(0f, 0f, BotMiniCourse.GapAStart), new Vector3(0f, 0f, BotMiniCourse.GapAEnd + 1.2f)),
                bot.JumpGap(brain, new Vector3(2f, 0f, BotMiniCourse.GapAStart), new Vector3(2f, 0f, BotMiniCourse.GapAEnd + 1.2f)));

            // 6. stack: Sprinter onto the Brainiac's head, then up the 2.5 m ledge
            yield return bot.WalkTo(brain, BotMiniCourse.StackBottom, false, 0.25f);
            yield return bot.Face(brain, Vector3.forward);
            yield return bot.StackOnto(sprinter, brain);
            yield return bot.ClimbOnto(sprinter, BotMiniCourse.StackTop);
            yield return bot.ExpectNear(sprinter, BotMiniCourse.StackTop, 3f, 0.5f, 0.4f);
            bot.Snapshot("stack_top", sprinter);
            yield return bot.WalkPath(sprinter, false, BotMiniCourse.StackBack, BotMiniCourse.StackDown);

            // 7. throw: the Brute throws the Brainiac onto the 3.5 m ledge
            if (caps.Grab)
            {
                yield return bot.Grab(brute, brain);
                yield return bot.WalkTo(brute, BotMiniCourse.ThrowStage);
                yield return bot.ThrowTo(brute, BotMiniCourse.ThrowLanding);
                yield return bot.ExpectNear(brain, BotMiniCourse.ThrowLanding, 3f, 1f, 0.5f);
                bot.Snapshot("throw_landed", brain);
                yield return bot.WalkPath(brain, false, BotMiniCourse.ThrowBack, BotMiniCourse.ThrowDown);
            }
            else bot.Skip("Grab+ThrowTo", "CharacterGrabber is still a stub");

            // 8. living bridge over the 5.2 m gap (Brute), the others cross, Brute recovers to the far side
            yield return bot.WalkTo(brute, BotMiniCourse.MidLane, false, 0.8f);
            yield return bot.WalkTo(brute, BotMiniCourse.BridgeSpot, false, 0.2f);
            if (caps.Bridge)
            {
                yield return bot.FormBridge(brute, Vector3.forward);
                bot.Snapshot("bridge_formed", brute);
                yield return bot.WalkPath(brain, false, BotMiniCourse.BridgeApproach, BotMiniCourse.BridgeFar, BotMiniCourse.BridgeFar + new Vector3(-1.5f, 0f, 1.5f));
                yield return bot.WalkPath(sprinter, false, BotMiniCourse.BridgeApproach, BotMiniCourse.BridgeFar, BotMiniCourse.BridgeFar + new Vector3(1.5f, 0f, 1.5f));
                yield return bot.RecoverBridge(brute);
                yield return bot.Expect(() => brute.Feet.z > BotMiniCourse.GapBEnd && brute.Feet.y > -0.3f, "bridge recovered onto the far side", 1f, brute);
            }
            else
            {
                bot.Skip("FormBridge", "CharacterBridge is still a stub - Sprinter turbo-jumps, the others use a DEBUG ASSIST");
                yield return bot.JumpGap(sprinter, new Vector3(1.5f, 0f, BotMiniCourse.GapBStart), new Vector3(1.5f, 0f, BotMiniCourse.GapBEnd + 2f));
                bot.Options.AllowDebugAssists = true;
                yield return bot.DebugTeleport(brute, new Vector3(-1.5f, 0f, BotMiniCourse.GapBEnd + 2f));
                yield return bot.DebugTeleport(brain, new Vector3(0f, 0f, BotMiniCourse.GapBEnd + 2f));
                bot.Options.AllowDebugAssists = false;
            }

            // 9. exit
            yield return bot.GatherAt(BotMiniCourse.ExitCenter);
            yield return bot.ExpectLevelComplete(8f);
            bot.Snapshot("course_end", sprinter);
        }
    }
}
