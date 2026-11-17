// =====================================================================================
//  SHIFTERS - L5Solution.cs   [LEVEL 5 - TeamBot solution run, no debug assists]
//  Plays TOTAL CHAOS start to finish with the full-team ending (two in the pod, one on the roof):
//    S1 rides the three shuttles (sweeper shuttle: back zone, side exit)
//    S2 times the cycling lasers / electric floors, the Sprinter sprints the crumbling catwalk and
//       pulls the LASER OVERRIDE
//    S3 shoots the glass shelf (barrel -> dock plate -> bot A sleeps), drops off the catwalk, Brute +
//       Brainiac latch the 150 kg plate
//    S4 Brute pushes the block into the basket right after a crusher slam, Sprinter joins it,
//       Brute + Brainiac ride A, DISPATCH
//    S5 Brute + Brainiac cross the deck laser into the pod (<= 200 kg); the Sprinter climbs the
//       maintenance shaft (3 lifts, 2 lasers) to the roof hatch -> level complete -> victory.
//  Run: run_level_test.sh <proj> 5 <seed>
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L5Solution
    {
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, null, "L5");

        static Vector3 P(string id) => LevelMark.Position(id);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>DEV ONLY (never in the final runs): start at section N (1..5) by debug-teleporting the team.</summary>
        public static int DevStartSection = 1;

        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("L5 team: " + bot.TeamSummary());
            if (DevStartSection > 1)
            {
                Vector3 c = DevStartSection == 2 ? V(-3f, 2f, 57.5f) : DevStartSection == 3 ? V(-1f, 2f, 105.8f) : V(0f, 0.4f, 149f);
                yield return bot.DebugTeleport(brute, c + V(-1.6f, 0.1f, 0f));
                yield return bot.DebugTeleport(spr, c + V(0f, 0.1f, 0f));
                yield return bot.DebugTeleport(brain, c + V(1.6f, 0.1f, 0f));
                yield return bot.Wait(0.5f);
            }
            if (DevStartSection <= 1) yield return S1(bot, brute, spr, brain);
            if (bot.Failed) yield break;
            if (DevStartSection <= 2) yield return S2(bot, brute, spr, brain);
            if (bot.Failed) yield break;
            if (DevStartSection <= 3) yield return S3(bot, brute, spr, brain);
            if (bot.Failed) yield break;
            yield return S4(bot, brute, spr, brain);
            if (bot.Failed) yield break;
            yield return S5(bot, brute, spr, brain);
        }

        // ------------------------------------------------------------------ sections
        static IEnumerator S1(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var riders = new[] { brute, spr, brain };
            yield return bot.Parallel(bot.WalkTo(brute, V(-1.3f, 0f, 9.3f)), bot.WalkTo(spr, V(0f, 0f, 9.6f)), bot.WalkTo(brain, V(1.3f, 0f, 9.3f)));
            if (bot.Failed) yield break;
            yield return Ride(bot, "S1.P1", riders,
                new[] { V(-1.3f, 0f, -0.3f), V(0f, 0f, 0.7f), V(1.3f, 0f, -0.3f) },
                new[] { V(-1.6f, 0f, 25.6f), V(0f, 0f, 26.6f), V(1.6f, 0f, 25.6f) }, "P1");
            if (bot.Failed) yield break;
            yield return bot.Parallel(bot.WalkTo(brute, V(-1.5f, 0f, 28.4f)), bot.WalkTo(spr, V(0f, 0f, 28.5f)), bot.WalkTo(brain, V(1.5f, 0f, 28.4f)));
            if (bot.Failed) yield break;
            // sweeper shuttle: stand in the back zone, leave sideways onto island B
            yield return Ride(bot, "S1.P2", riders,
                new[] { V(-1.5f, 0f, -3.6f), V(0f, 0f, -3.75f), V(1.5f, 0f, -3.6f) },
                new[] { V(-5.2f, 0f, 38.2f), V(-6.7f, 0f, 39.3f), V(-8.2f, 0f, 38.2f) }, "P2");
            if (bot.Failed) yield break;
            yield return bot.Parallel(bot.WalkTo(brute, V(-7.5f, 0f, 39.8f)), bot.WalkTo(spr, V(-6.25f, 0f, 40.0f)), bot.WalkTo(brain, V(-5.0f, 0f, 39.8f)));
            if (bot.Failed) yield break;
            yield return Ride(bot, "S1.P3", riders,
                new[] { V(-1.3f, 0f, -0.3f), V(0f, 0f, 0.7f), V(1.3f, 0f, -0.3f) },
                new[] { V(-7.6f, 2f, 55.0f), V(-6.0f, 2f, 56.0f), V(-4.4f, 2f, 55.0f) }, "P3");
            if (bot.Failed) yield break;
            yield return bot.GatherAt(V(-3f, 2f, 57.5f));
            yield return ExpectCheckpoint(bot, 1);
        }

        static IEnumerator S2(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // Brute + Brainiac cross the cycling first half, one hazard at a time
            yield return Pair(bot, brute, brain, P("S2.Safe0"));
            if (bot.Failed) yield break;
            string[] hazards = { "S2.L1", "S2.E1", "S2.L2", "S2.E2", "S2.L3" };
            for (int i = 0; i < hazards.Length; i++)
            {
                var h = LevelMark.Find<PuzzleHazard>(hazards[i]);
                if (h == null) { bot.Fail("missing hazard " + hazards[i]); yield break; }
                yield return bot.WaitUntil(() => !h.IsActive && h.TimeToSwitch > 1.9f, 15f, "safe window " + hazards[i]);
                if (bot.Failed) yield break;
                yield return Pair(bot, brute, brain, P("S2.Safe" + (i + 1)));
                if (bot.Failed) yield break;
            }
            yield return Pair(bot, brute, brain, P("S2.Safe6"));
            if (bot.Failed) yield break;

            // Sprinter: turbo sprint over the crumbling catwalk, LASER OVERRIDE
            yield return bot.WalkTo(spr, "S2.TurboStart", false, 0.4f);
            yield return bot.Face(spr, Vector3.forward);
            if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "S2.TurboEnd", true, 0.9f, 10f);
            if (bot.Failed) yield break;
            yield return bot.ExpectNear(spr, "S2.TurboEnd", 2.0f, 0f, 0.8f);
            yield return bot.Interact(spr, "S2.Lever");
            yield return bot.ExpectPowered("S2.LaserToggle", true, 1.5f);
            if (bot.Failed) yield break;
            yield return bot.Parallel(
                bot.WalkPath(spr, false, V(8.6f, 2f, 89.4f), V(8.6f, 2f, 91.4f), P("S2.LedgeExit"), V(4f, 2f, 103.6f)),
                bot.WalkPath(brute, false, V(-1.5f, 2f, 98.5f), V(-2.6f, 2f, 105.8f)),
                bot.WalkPath(brain, false, V(1.5f, 2f, 98.5f), V(0.6f, 2f, 105.8f)));
            yield return ExpectCheckpoint(bot, 2);
        }

        static IEnumerator S3(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // everyone onto the bot-proof catwalk
            // (around the C2 beacon pole at (-4.3, 106.2))
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(-3.2f, 2f, 103.4f), V(-10.3f, 2f, 104.2f), V(-10.3f, 2f, 114f), V(-10.3f, 2f, 121.6f)),
                bot.WalkPath(spr, false, V(-1.0f, 2f, 102.2f), V(-11.0f, 2f, 102.6f), V(-10.9f, 2f, 113.5f), V(-10.9f, 2f, 115.8f)),
                bot.WalkPath(brain, false, V(-2.0f, 2f, 104.6f), V(-9.7f, 2f, 106.0f), V(-9.7f, 2f, 113f), V(-9.7f, 2f, 119.6f)));
            if (bot.Failed) yield break;
            var gun = bot.GunOwner;
            if (gun == null) { bot.Fail("nobody holds the gun"); yield break; }
            yield return bot.WalkTo(gun, "S3.ShootSpot", false, 0.4f);
            if (bot.Failed) yield break;
            var glass = LevelMark.Find<Breakable>("S3.Glass");
            yield return bot.ShootAt(gun, "S3.GlassAim", () => glass != null && glass.IsBroken, 10, 15f);
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S3.DockPlate", true, 5f);
            var dock = LevelMark.Find<L5ChargingDock>("S3.Dock");
            yield return bot.WaitUntil(() => dock != null && dock.IsAsleep, 12f, "bot A asleep on its dock");
            if (bot.Failed) yield break;

            // drop off the far end of the catwalk (outside both bot arenas)
            var order = new[] { brute, brain, spr };
            Vector3[] drops = { V(-7.4f, 0.4f, 131.4f), V(-7.6f, 0.4f, 129.6f), V(-7.2f, 0.4f, 133.2f) };
            for (int i = 0; i < order.Length; i++)
            {
                yield return bot.WalkPath(order[i], false, V(-10.5f, 2f, 129.4f + i * 1.0f), drops[i]);
                if (bot.Failed) yield break;
                yield return bot.WaitSettled(order[i], 2f);
            }
            yield return bot.Parallel(bot.WalkTo(brute, "S3.PlateA", false, 0.3f), bot.WalkTo(brain, "S3.PlateB", false, 0.3f),
                                      bot.WalkTo(spr, V(-1.6f, 0.4f, 137.0f)));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S3.Plate", true, 3f);
            yield return bot.ExpectPowered("S3.Door", true, 4f);
            if (bot.Failed) yield break;
            yield return bot.Wait(0.8f);
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(-1.0f, 0.4f, 141.2f), V(-1.2f, 0.4f, 146.0f), V(-1.6f, 0.4f, 149f)),
                bot.WalkPath(brain, false, V(0.4f, 0.4f, 141.0f), V(0.2f, 0.4f, 146.0f), V(0f, 0.4f, 149f)),
                bot.WalkPath(spr, false, V(1.0f, 0.4f, 140.5f), V(1.2f, 0.4f, 146.0f), V(1.6f, 0.4f, 149f)));
            yield return ExpectCheckpoint(bot, 3);
        }

        static IEnumerator S4(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var crusher = LevelMark.Find<Crusher>("S4.Crusher");
            var lift = LevelMark.Find<PulleyLift>("S4.Lift");
            if (crusher == null || lift == null) { bot.Fail("S4 crusher/lift missing"); yield break; }
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(1.6f, 0.4f, 157.5f), P("S4.PushStart")),
                bot.WalkPath(brain, false, V(-2.6f, 0.4f, 158f), P("S4.A_Approach"), P("S4.A_Brainiac")),
                bot.WalkPath(spr, false, V(5.5f, 0.4f, 158f), P("S4.B_Approach")));
            if (bot.Failed) yield break;
            // push right after a slam (the next one is 3.5 s away)
            yield return WaitSlam(bot, crusher, false);
            if (bot.Failed) yield break;
            yield return bot.PushBlock(brute, "S4.Block", "S4.BlockGoal", 0.3f, 20f);
            if (bot.Failed) yield break;
            yield return bot.WalkPath(brute, false, V(1.0f, 0.4f, 164.8f), V(-3.6f, 0.4f, 164.9f), P("S4.A_Brute"));
            if (bot.Failed) yield break;
            // Sprinter joins the block in the basket while the head is rising
            yield return WaitSlam(bot, crusher, true);
            if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, false, V(4.35f, 0.4f, 166.6f), P("S4.B_North"));
            if (bot.Failed) yield break;
            yield return bot.WaitSettled(spr, 1.5f);
            bot.Log("pulley before dispatch: A " + Mathf.RoundToInt(lift.MassA) + " kg vs B " + Mathf.RoundToInt(lift.MassB) + " kg");
            yield return bot.Expect(() => lift.MassB > lift.MassA + lift.DiffThresholdKg, "basket outweighs passengers", 2f);
            if (bot.Failed) yield break;
            yield return bot.Interact(brute, "S4.Dispatch");
            yield return bot.ExpectPowered("S4.Brake", true, 1.5f);
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => lift.Offset >= lift.Travel * 0.5f - 0.02f, 10f, "passengers at the top");
            if (bot.Failed) yield break;
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(-3.4f, 3.4f, 172.0f), V(-5.2f, 3.4f, 176.4f)),
                bot.WalkPath(brain, false, V(-2.4f, 3.4f, 172.0f), V(-2.8f, 3.4f, 176.6f)));
            yield return ExpectCheckpoint(bot, 4);
        }

        static IEnumerator S5(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // Brute + Brainiac: deck laser, then into the pod (any two weigh < 200 kg)
            yield return Pair(bot, brute, brain, P("S5.DeckSafe0"));
            if (bot.Failed) yield break;
            var dl = LevelMark.Find<PuzzleHazard>("S5.DeckLaser");
            yield return bot.WaitUntil(() => dl != null && !dl.IsActive && dl.TimeToSwitch > 1.7f, 15f, "deck laser off");
            if (bot.Failed) yield break;
            yield return Pair(bot, brute, brain, P("S5.DeckSafe1"));
            if (bot.Failed) yield break;
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(-1.2f, 3.4f, 196.5f), P("S5.CabA")),
                bot.WalkPath(brain, false, V(1.0f, 3.4f, 196.5f), P("S5.CabB")));
            if (bot.Failed) yield break;
            var lim = LevelMark.Find<L5PodWeightLimit>("S5.Pod");
            yield return bot.Expect(() => lim == null || !lim.IsOverweight, "two interns fit in the pod", 2f);
            if (bot.Failed) yield break;

            // Sprinter: basket bottom -> maintenance shaft -> roof hatch
            yield return bot.WalkTo(spr, "S5.M0", false, 0.5f);
            if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "S5.M0Dock", false, 0.5f);
            yield return Ride(bot, "S5.V1", new[] { spr }, new[] { V(0f, 0f, 0f) }, new[] { P("S5.M1Wait") }, "V1");
            if (bot.Failed) yield break;
            var gl1 = LevelMark.Find<PuzzleHazard>("S5.GL1");
            yield return bot.WaitUntil(() => gl1 != null && !gl1.IsActive && gl1.TimeToSwitch > 1.2f, 15f, "shaft laser 1 off");
            yield return bot.WalkTo(spr, "S5.M1Safe", false, 0.5f);
            if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, V(7f, 1.4f, 185.4f), false, 0.4f);
            yield return Ride(bot, "S5.V2", new[] { spr }, new[] { V(0f, 0f, 0f) }, new[] { P("S5.M2Dock") }, "V2");
            if (bot.Failed) yield break;
            var gl2 = LevelMark.Find<PuzzleHazard>("S5.GL2");
            yield return bot.WaitUntil(() => gl2 != null && !gl2.IsActive && gl2.TimeToSwitch > 1.2f, 15f, "shaft laser 2 off");
            yield return bot.WalkTo(spr, "S5.M2Safe", false, 0.5f);
            if (bot.Failed) yield break;
            yield return Ride(bot, "S5.V3", new[] { spr }, new[] { V(0f, 0f, 0f) }, new[] { P("S5.M3") }, "V3");
            if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "S5.Roof", false, 0.5f);
            yield return bot.ExpectLevelComplete(15f);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Two characters walk side by side (1.5 m apart across X) to a spot.</summary>
        static IEnumerator Pair(TeamBot bot, ShifterCharacter a, ShifterCharacter b, Vector3 spot)
        {
            yield return bot.Parallel(bot.WalkTo(a, spot + V(-1.5f, 0f, 0f), false, 0.4f), bot.WalkTo(b, spot + V(1.5f, 0f, 0f), false, 0.4f));
        }

        /// <summary>Wait for a fresh near-dock arrival, board at body-local spots, ride, step off at the far dock.</summary>
        static IEnumerator Ride(TeamBot bot, string id, ShifterCharacter[] riders, Vector3[] localSpots, Vector3[] exits, string label)
        {
            var mp = LevelMark.Find<MovingPlatform>(id);
            if (mp == null || mp.Body == null) { bot.Fail("moving platform " + id + " missing"); yield break; }
            yield return bot.WaitUntil(() => !(mp.Waiting && mp.TargetIndex == 1), 40f, label + " leaves the near dock");
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => mp.Waiting && mp.TargetIndex == 1, 40f, label + " arrives at the near dock");
            if (bot.Failed) yield break;
            var body = mp.Body.transform;
            var walks = new List<IEnumerator>();
            for (int i = 0; i < riders.Length; i++)
                walks.Add(bot.WalkTo(riders[i], ((BotTarget)body).Offset(body.rotation * localSpots[i]), false, 0.35f, 6f));
            yield return bot.Parallel(walks.ToArray());
            if (bot.Failed) yield break;
            yield return bot.Expect(() => mp.Waiting || mp.TargetIndex == 0 || mp.TargetIndex == 1, label + " boarded", 0f);
            yield return bot.WaitUntil(() => mp.Waiting && mp.TargetIndex == 0, 40f, label + " arrives at the far dock");
            if (bot.Failed) yield break;
            walks.Clear();
            for (int i = 0; i < riders.Length; i++) walks.Add(bot.WalkTo(riders[i], exits[i], false, 0.5f, 8f));
            yield return bot.Parallel(walks.ToArray());
        }

        /// <summary>Wait until the crusher head has just hit bottom (rising=false) or has just started rising.</summary>
        static IEnumerator WaitSlam(TeamBot bot, Crusher c, bool rising)
        {
            yield return bot.WaitUntil(() => c.Extension < 0.5f, 8f, "crusher up");
            yield return bot.WaitUntil(() => c.Extension > 0.98f, 8f, "crusher slams");
            if (rising) yield return bot.WaitUntil(() => c.Extension < 0.95f, 4f, "crusher rising");
        }

        static IEnumerator ExpectCheckpoint(TeamBot bot, int index)
        {
            var cp = LevelMark.Find<Checkpoint>("checkpoint." + index);
            yield return bot.Expect(() => cp != null && cp.IsActivated, "checkpoint " + index + " activated", 6f);
        }
    }
}
