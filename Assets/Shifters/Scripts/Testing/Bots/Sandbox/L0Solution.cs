// =====================================================================================
//  SHIFTERS - L0Solution.cs   [BOTS module - framework validation on the builder's L0_Sandbox]
//  TeamBot solves the sandbox course with the REAL characters, abilities and kits:
//    walk -> 2 m gap (all three) -> STACK the Sprinter onto the Brainiac's head and up the 2.5 m ledge ->
//    the Brute THROWS the Brainiac onto the 3.5 m ledge -> the Brute becomes a 5.2 m living BRIDGE and the
//    other two cross -> PUSH the 160 kg block onto the 150 kg plate -> HACK the IQ 7 terminal -> SHOOT the
//    target -> everybody into the EXIT.
//  Characters are referenced by ARCHETYPE (never by slot), so the same script works for every seed.
//  Stations whose objects are missing (a scene built before the kits existed) are SKIPPED with the reason.
//    Built scene : $SP/tools/run_level_test.sh <project> 0 [seed | s1,s2,s3]     (Shifters.L0Solution.Run)
//    Runtime     : devplay.sh <project> l0rt -devSetup Shifters.L0Solution.RunRuntime -devSeconds 1 [-botSeeds 1,2,3]
//  A good template for L<n>Solution (see docs/reports/bots.md).
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L0Solution
    {
        /// <summary>Built scene Assets/Shifters/Scenes/L0_Sandbox.unity (-botSeed N / -botSeeds a,b,c).</summary>
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunLevel(ctx, Solve, "L0");

        /// <summary>Sandbox built at runtime by LevelRuntimeBuilder (always today's kits), once per seed
        /// (-botSeeds, default 11, 22, 33). Use without -devScene.</summary>
        public static IEnumerator RunRuntime(DevPlayContext ctx) =>
            TeamBot.RunRuntimeLevel(ctx, L0_Sandbox.Index, Solve, "L0rt", BotArgs.Seeds(11, 22, 33));

        static bool Has(string mark) => BotTargets.FindMark(mark) != null;

        /// <summary>Height of the plate's top surface above the plate root (the floor).</summary>
        static float PadStep(string plateMark)
        {
            var t = BotTargets.FindMark(plateMark);
            if (t == null) return 0f;
            var p = t.position;
            return Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y - p.y : 0f;
        }

        /// <summary>Block-centre goal just short of the plate's near edge (plate/block half sizes) along the dominant axis.</summary>
        static Vector3 EdgeGoal(string blockMark, string plateMark, float plateHalf, float blockHalf)
        {
            Vector3 b = BotTargets.FindMark(blockMark).position, p = BotTargets.FindMark(plateMark).position;
            Vector3 d = p - b; d.y = 0f;
            Vector3 axis = Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
            Vector3 g = p - axis * (plateHalf + blockHalf + 0.1f);
            g.y = p.y;
            return g;
        }

        // puzzle-floor columns (x) per archetype, see step 4
        const float ColBrute = -0.9f, ColSprinter = 0.4f, ColBrain = 1.6f;

        // Course (L0_Sandbox, along +Z): spawn z -3 | 2 m gap z 7..9 | ledges z 15..21: stack x 2..7 (2.5 m), throw x -7..-3
        // (3.5 m), corridor between | bridge gap z 21..26.2 | puzzle floor: block (-3,30) -> plate (-3,37), terminal (3,33)
        // facing -Z, target (5,2.5,46) | exit cab z 50..54.
        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac; var gun = bot.GunOwner;
            yield return bot.Expect(() => brute != null && spr != null && brain != null, "one Brute, one Sprinter and one Brainiac");
            if (bot.Failed) yield break;
            bot.Snapshot("l0_start");

            // ---- 1. everyone jumps the 2 m gap: three lanes, assigned by current x so nobody crosses paths
            BotTarget take = "gap2.takeoff", land = "gap2.landing";
            var team = bot.Others();
            team.Sort((a, b) => a.Feet.x.CompareTo(b.Feet.x));
            var jumps = new IEnumerator[team.Count];
            for (int i = 0; i < team.Count; i++)
            {
                var lane = Vector3.right * ((i - (team.Count - 1) * 0.5f) * 2f);
                jumps[i] = bot.JumpGap(team[i], take.Offset(lane), land.Offset(lane));
            }
            yield return bot.Parallel(jumps);

            // ---- 2. STACK: the Brainiac (1.5 m) stands 0.7 m from the 2.5 m ledge, the Sprinter hops onto its head
            //         and climbs up; then walks off the ledge's front edge (2.5 m drop, away from everyone)
            yield return bot.WalkTo(brain, "stack.base", false, 0.25f);
            yield return bot.Face(brain, Vector3.right);
            yield return bot.StackOnto(spr, brain);
            yield return bot.ClimbOnto(spr, "stack.ledge");
            yield return bot.ExpectNear(spr, "stack.ledge", 2.5f, 0.5f, 0.4f);
            bot.Snapshot("l0_stack_top", spr);
            yield return bot.WalkPath(spr, false, new Vector3(4.5f, L0_Sandbox.StackHeight, 15.7f), new Vector3(4.5f, 0f, 13.4f));

            // ---- 3. THROW: the Brute carries the Brainiac to throw.spot (4.4 m from the 4 x 6 m ledge) and lobs it up.
            //         From 4.4 m that is a ~80 deg lob - steeper than a player can aim (38..63 deg): TeamBot WARNs. The
            //         player-aimable 60 deg lob (from ~7-9 m) lands with ~8 m/s of horizontal speed and rolled off this
            //         4 m deep ledge on 2 of 3 seeds; even the steep lob can roll off sideways (seed 33), so the Brute
            //         picks the Brainiac up and re-throws, like a player. Then it walks off the inner edge (3.5 m drop).
            yield return bot.Grab(brute, brain);
            yield return bot.ThrowTo(brute, "throw.ledge", "throw.spot", 3f, 3);   // tumbling bodies can roll off: up to 3 throws
            yield return bot.Expect(() => brain.Feet.y > L0_Sandbox.ThrowHeight - 0.3f && brain.Motor != null && brain.Motor.IsGrounded,
                "the Brainiac landed on the 3.5 m ledge", 2f, brain);
            bot.Snapshot("l0_throw_landed", brain);
            yield return bot.WalkPath(brain, false, new Vector3(-3.6f, L0_Sandbox.ThrowHeight, 17.6f), new Vector3(-1.4f, 0f, 17.6f));

            // ---- 4. BRIDGE: the Brute lies across the 5.2 m gap, the others cross one at a time, the Brute recovers
            //         onto the far side. On the puzzle floor everyone keeps to a north-south column so straight walks
            //         never cross: Brute x -0.9 (west of the push lane x -3 is the block), Sprinter x 0.4, Brainiac x 1.6
            //         (west of the terminal at x 2.5..3.5).
            yield return bot.WalkTo(brute, "bridge.near", false, 0.2f);
            yield return bot.FormBridge(brute, Vector3.forward);
            bot.Snapshot("l0_bridge", brute);
            yield return bot.WalkPath(brain, false, new Vector3(0f, 0f, 19.4f), new Vector3(0f, 0f, 27.7f), new Vector3(ColBrain, 0f, 29.0f));
            yield return bot.WalkPath(spr, false, new Vector3(1.0f, 0f, 13.8f), new Vector3(0f, 0f, 19.4f), new Vector3(0f, 0f, 27.7f), new Vector3(ColSprinter, 0f, 30.4f));
            yield return bot.RecoverBridge(brute);
            yield return bot.Expect(() => brute.Feet.z > L0_Sandbox.BridgeEnd && brute.Feet.y > -0.3f, "the Brute recovered onto the far side", 1f, brute);

            // ---- 5. PUSH the 160 kg block onto the 150 kg plate (Brute = HEAVY LIFTER), then step into its column.
            //         KIT ISSUE: today's PressurePlate pad is raised +0.14 m and a Pushable can't climb it (the block
            //         stops dead at the plate's edge). While that holds, the block is parked at the edge and the plate
            //         check is SKIPPED with the reason; once the pad is flush the full push + plate check run.
            if (Has("push.block") && Has("push.plate"))
            {
                yield return bot.WalkTo(brute, "push.start", false, 0.4f);
                float step = PadStep("push.plate");
                if (step > 0.06f)
                {
                    yield return bot.PushBlock(brute, "push.block", EdgeGoal("push.block", "push.plate", 1.0f, 0.8f));
                    bot.Skip("ExpectPowered push.plate", "KIT ISSUE (puzzles): the PressurePlate pad is raised " + step.ToString("0.00")
                        + " m and a Pushable cannot climb it - block parked at the plate's edge (see docs/fixes/puzzles.md)");
                }
                else
                {
                    yield return bot.PushBlock(brute, "push.block", "push.plate");
                    yield return bot.ExpectPowered("push.plate", true, 2f);
                }
            }
            else bot.Skip("PushBlock", "push.block / push.plate are not in this scene (built before PuzzleKit) - rebuild L0_Sandbox");
            yield return bot.WalkTo(brute, new Vector3(ColBrute, 0f, Mathf.Max(brute.Feet.z, 28.6f)));

            // ---- 6. HACK the IQ 7 terminal (Brainiac = BIG BRAIN; Interact switches to it - the sensor only scans for
            //         the active character), then back into its column
            if (Has("hack.terminal"))
            {
                yield return bot.Hack(brain, "hack.terminal", "hack.spot");
                yield return bot.WalkTo(brain, new Vector3(ColBrain, 0f, 31.0f));
            }
            else bot.Skip("Hack", "hack.terminal is not in this scene (built before PuzzleKit) - rebuild L0_Sandbox");

            // ---- 7. SHOOT the target (whoever has the gun - random per seed): walk up its own column, fire from ~10 m
            if (Has("shoot.target") && gun != null)
            {
                yield return bot.WalkTo(gun, new Vector3(gun.Feet.x, 0f, 38f));
                yield return bot.ShootAt(gun, "shoot.target");
            }
            else bot.Skip("ShootAt", gun == null ? "nobody has a gun" : "shoot.target is not in this scene (built before PuzzleKit) - rebuild L0_Sandbox");

            // ---- 8. EXIT: everyone into the cab
            if (TeamBot.FindExit("exit") != null)
            {
                yield return bot.EnterExit("exit");
                yield return bot.ExpectLevelComplete(8f);
            }
            else bot.Skip("EnterExit", "no LevelExit in this scene (built before LevelKit) - rebuild L0_Sandbox");
            bot.Snapshot("l0_end");
        }
    }
}
