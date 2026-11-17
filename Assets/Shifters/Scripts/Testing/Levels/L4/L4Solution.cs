// =====================================================================================
//  SHIFTERS - L4Solution.cs   [owned by LEVEL 4]
//  Scripted proof (TeamBot) that "THE IQ TEST" is completable by ANY random team. Characters are
//  referenced by archetype only (the gun owner is whoever holds the gun). No debug teleports.
//  Run on the built scene:  run_level_test.sh <project> 4 <seed>
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>Level 4 solution run (TeamBot).</summary>
    public static class L4Solution
    {
        /// <summary>Entry point for run_level_test.sh: -botSeeds a,b,c runs every seed in one launch (scene reload per
        /// team), -botSeed n / nothing runs once.</summary>
        public static IEnumerator Run(DevPlayContext ctx)
        {
            if (!string.IsNullOrEmpty(BotArgs.Str("-botSeeds", "")))
                return TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, new BotOptions(), "L4");
            return TeamBot.Run(ctx, Solve, new BotOptions(), "L4");
        }

        /// <summary>The whole intended solution, section by section.</summary>
        public static IEnumerator Solve(TeamBot bot) => SolveFrom(bot, 1);

        /// <summary>DEV ONLY for section > 1: debug-teleports the team to that section's checkpoint first (needs
        /// BotOptions.AllowDebugAssists; such runs are never valid proofs). Section 1 = the real full solution.</summary>
        public static IEnumerator SolveFrom(TeamBot bot, int section)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("TEAM " + bot.TeamSummary());
            if (section > 1)
            {
                var c = LevelMark.Position("checkpoint." + (section - 1));
                yield return bot.DebugTeleport(brute, c + new Vector3(-1.6f, 0.1f, 0f), 0f);
                yield return bot.DebugTeleport(spr, c + new Vector3(0f, 0.1f, 0f), 0f);
                yield return bot.DebugTeleport(brain, c + new Vector3(1.6f, 0.1f, 0f), 0f);
                yield return bot.Wait(0.5f);
            }
            if (section <= 1) yield return S1EntranceExam(bot, brute, spr, brain);
            if (section <= 2) yield return S2HiddenPath(bot, brute, spr, brain);
            if (section <= 3) yield return S3SwitchSequence(bot, brute, spr, brain);
            if (section <= 4) yield return S4LogicGates(bot, brute, spr, brain);
            yield return S5FinalExam(bot, brute, spr, brain);
        }

        // ------------------------------------------------------------------ helpers
        static bool CheckpointActive(int n)
        {
            var cp = LevelMark.Find<Checkpoint>("checkpoint." + n);
            return cp != null && cp.IsActivated;
        }

        static IEnumerator ExpectCheckpoint(TeamBot bot, int n) => bot.Expect(() => CheckpointActive(n), "checkpoint " + n + " activated", 4f);

        static IEnumerator ExpectOpen(TeamBot bot, string doorId, float within = 4f)
        {
            var door = LevelMark.Find<Door>(doorId);
            return bot.Expect(() => door != null && door.IsOpen, doorId + " fully open", within);
        }

        static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

        // ------------------------------------------------------------------ S1 entrance exam
        static IEnumerator S1EntranceExam(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // the Brute lifts the Brainiac up to the 2.5 m ledge (throw; stacking on a teammate's head works too)
            yield return bot.Parallel(bot.WalkTo(brain, "S1.ThrowPrep"), bot.WalkTo(spr, V(-3f, 6f)));
            yield return bot.Grab(brute, brain);
            yield return bot.ThrowTo(brute, "S1.LedgeTop", "S1.ThrowSpot", 4.2f, 3);   // anywhere on the 6.8 x 3.75 m ledge is fine; a tumbling miss is picked up and re-thrown, like a player would
            yield return bot.Expect(() => brain.Feet.y > L4Builder.LedgeHeight - 0.3f, "Brainiac is up on the 2.5 m ledge", 3f, brain);
            yield return bot.Hack(brain, "S1.Terminal");
            yield return bot.ExpectPowered("S1.Gate", true, 2f);
            yield return ExpectOpen(bot, "S1.Gate");
            yield return bot.WalkPath(brain, false, "S1.LedgeExit", "S1.GateNear");
            yield return bot.WalkPath(brain, false, "S1.GateNear", V(-1.6f, 17.5f));
            yield return bot.WalkPath(spr, false, "S1.GateNear", V(1.6f, 17.5f));
            yield return bot.WalkPath(brute, false, "S1.GateNear", V(0f, 16.0f));
            yield return bot.GatherAt("checkpoint.1");
            yield return ExpectCheckpoint(bot, 1);
        }

        // ------------------------------------------------------------------ S2 hidden pathway
        static IEnumerator S2HiddenPath(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var path = new BotTarget[] { "S2.Start", "S2.P0", "S2.P1", "S2.P2", "S2.P3", "S2.P4", "S2.Arrive" };
            // the Brainiac goes first (only BIG BRAIN sees the tiles; its footprints stay lit for the others)
            yield return bot.Switch(brain);
            yield return bot.WalkPath(brain, path, false, 0.35f, 20f);
            yield return bot.WalkTo(brain, V(-2.0f, 44.2f));
            yield return bot.WalkPath(spr, path, false, 0.35f, 20f);
            yield return bot.WalkTo(spr, V(-2.0f, 40.8f));
            yield return bot.WalkPath(brute, path, false, 0.35f, 20f);
            yield return bot.Expect(() => brute.IsAlive && spr.IsAlive && brain.IsAlive, "nobody fell into the void", 0f);
            yield return bot.GatherAt("S2.Gather");
            yield return ExpectCheckpoint(bot, 2);
        }

        // ------------------------------------------------------------------ S3 switch sequence
        static IEnumerator S3SwitchSequence(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // characters move one at a time along lanes that never cross a teammate (capsules push each other)
            var gunner = bot.GunOwner;
            if (gunner != null)
            {
                // bonus: the gun owner shoots the hidden target (weapon perspective) -> bonus closet opens
                yield return bot.WalkPath(gunner, false, V(-4f, 48f), V(-4f, 52.5f), V(-8f, 57f));
                yield return bot.ShootAt(gunner, "S3.Target");
                yield return bot.ExpectPowered("S3.BonusDoor", true, 2f);
            }
            else bot.Skip("S3 bonus target", "nobody holds the gun");
            if (brain != gunner) yield return bot.WalkPath(brain, false, V(-4f, 48f), V(-4f, 53f));
            // the order is only visible in IQ view: CIRCLE, STAR, TRIANGLE, SQUARE
            foreach (var sym in L4Builder.SequenceOrder)
                yield return bot.Interact(brain, "S3.Btn." + L4Decor.SymbolName(sym));
            yield return bot.ExpectPowered("S3.Lock", true, 1f);
            yield return ExpectOpen(bot, "S3.Door");
            // out through the door: the Brainiac (east side), the parked gunner (west side), then whoever waited at C2
            yield return bot.WalkPath(brain, false, V(-4f, 63.5f), S3Spot(bot, brain));
            if (gunner != null && gunner != brain) yield return bot.WalkPath(gunner, false, V(-4f, 63.5f), S3Spot(bot, gunner));
            foreach (var c in new[] { spr, brute })
                if (c != gunner) yield return bot.WalkPath(c, false, V(-4f, 48f), V(-4f, 52.5f), V(-4f, 63.5f), S3Spot(bot, c));
            yield return bot.GatherAt("S3.Gather");
            yield return ExpectCheckpoint(bot, 3);
        }

        /// <summary>Distinct parking spot per archetype beyond the S3 door (walkers never push a teammate).</summary>
        static BotTarget S3Spot(TeamBot bot, ShifterCharacter c)
            => c == bot.Brainiac ? V(-5.8f, 70.2f) : c == bot.Sprinter ? V(-1.9f, 70.4f) : V(-4.6f, 68.2f);

        // ------------------------------------------------------------------ S4 logic gates (ALL THREE)
        static IEnumerator S4LogicGates(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // single file over the walkway into the lab
            foreach (var c in new[] { brute, brain, spr })
                yield return bot.WalkPath(c, false, V(-4f, 76f), V(-4f, 81f), "S4.Entry");
            // Brute alone on A (100 kg)
            yield return bot.WalkPath(brute, false, V(-7.5f, 86.5f));
            yield return bot.WalkTo(brute, "S4.APos", false, 0.25f);
            // Brainiac + Sprinter together on B (100 kg)
            yield return bot.WalkPath(brain, false, V(-0.6f, 86f));
            yield return bot.WalkTo(brain, "S4.BLeft", false, 0.2f);
            yield return bot.WalkPath(spr, false, V(3.0f, 86f), "S4.BApproachEast");
            yield return bot.WalkTo(spr, "S4.BRight", false, 0.2f);
            yield return bot.ExpectPowered("S4.A", true, 2f);
            yield return bot.ExpectPowered("S4.B", true, 2f);
            yield return bot.ExpectPowered("S4.GateFinal", true, 1f);
            var latch = LevelMark.Find<L4SecurityLatch>("S4.Latch");
            yield return bot.Expect(() => latch != null && latch.IsLatched, "SECURITY LATCH engaged (2 s with C empty)", 4f);
            yield return ExpectOpen(bot, "S4.Door");
            // now everybody may cross C
            yield return bot.WalkPath(brute, false, "S4.Vestibule", V(-4f, 102.8f), V(-5.6f, 104.8f));
            yield return bot.WalkPath(brain, false, "S4.Vestibule", V(-4f, 102.8f), V(-2.4f, 104.8f));
            yield return bot.WalkPath(spr, false, "S4.Vestibule", V(-4f, 103.6f));
            yield return bot.ExpectPowered("S4.Door", true, 0f);
            yield return bot.GatherAt("S4.Gather");
            yield return ExpectCheckpoint(bot, 4);
        }

        // ------------------------------------------------------------------ S5 final exam (ALL THREE)
        static IEnumerator S5FinalExam(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // stage 1 STRENGTH: the Brute pushes the 160 kg block onto the recessed 150 kg plate
            yield return bot.WalkPath(brute, false, V(-4f, 110f), V(-4f, 114f));
            yield return bot.PushBlock(brute, "S5.Block", "S5.PlateCenter", 0.15f);
            yield return bot.ExpectPowered("S5.Plate", true, 2f);
            yield return ExpectOpen(bot, "S5.DoorA");

            // stage 2 SPEED: only TURBO LEGS beats the timed door; the lever behind it lets the others in
            yield return bot.WalkPath(spr, false, V(-4f, 110f), V(-4f, 114f), V(-7.2f, 122f), V(-7.0f, 128.6f), V(-4.6f, 129.4f), "S5.Stage2Entry");
            yield return bot.Interact(spr, "S5.Timed");
            yield return bot.WalkTo(spr, "S5.BeyondB", true, 0.8f, 12f);
            yield return bot.Interact(spr, "S5.Lever");
            yield return bot.ExpectPowered("S5.DoorB", true, 1f);
            yield return ExpectOpen(bot, "S5.DoorB");

            // stage 3 INTELLECT: the Brainiac hacks the terminal
            yield return bot.WalkPath(brain, false, V(-4f, 110f), V(-4f, 114f), V(-1.0f, 122f), V(-1.2f, 128.6f), V(-3.4f, 129.4f),
                "S5.Stage2Entry", V(-4f, 156.5f), "S5.Stage3Entry");
            yield return bot.Hack(brain, "S5.Terminal");
            yield return bot.ExpectPowered("S5.FinalDoor", true, 1f);
            yield return ExpectOpen(bot, "S5.FinalDoor");

            // everyone to the exit
            yield return bot.WalkPath(brute, false, V(-1.0f, 124.2f), V(-1.0f, 128.6f), V(-3.4f, 129.4f), "S5.Stage2Entry", V(-4f, 156.5f), "S5.Stage3Entry", V(-4f, 170.0f));
            yield return bot.WalkPath(spr, false, V(-2.2f, 168.2f));
            yield return bot.WalkPath(brain, false, V(-5.8f, 168.2f));
            // into the 3.2 m wide exit elevator in single file, each to its own spot inside the cab
            yield return bot.WalkPath(brute, false, V(-4f, 174.4f), V(-4f, 176.9f), V(-5.0f, 179.2f));
            yield return bot.WalkPath(spr, false, V(-4f, 174.4f), V(-4f, 176.9f), V(-3.2f, 179.3f));
            yield return bot.WalkPath(brain, false, V(-4f, 174.4f), V(-4.3f, 177.4f));
            yield return bot.ExpectLevelComplete(15f);
        }
    }
}
