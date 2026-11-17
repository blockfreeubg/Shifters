using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Scripted TeamBot playthrough of Level 2 "THE WEIGHT PROBLEM" (intended solution, characters by ARCHETYPE only,
    /// motor commands and public ability/interact APIs only - no debug teleports).
    /// Built scene: run_level_test.sh &lt;proj&gt; 2 &lt;seed&gt;  or  run_level_test.sh &lt;proj&gt; 2 1,2,3  (all seeds in one launch).
    /// </summary>
    public static class L2Solution
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            var seeds = BotArgs.Seeds();
            if (seeds == null || seeds.Length <= 1) return TeamBot.Run(ctx, Solve, null, "L2");
            return TeamBot.RunSeeds(ctx, seeds, Solve, null, null, "L2");
        }

        static T Find<T>(string id) where T : Component => LevelMark.Find<T>(id);

        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("L2 team: " + bot.TeamSummary());
            bot.Log("L2 masses: brute " + brute.TotalMass.ToString("0.0") + " kg, sprinter " + spr.TotalMass.ToString("0.0") + " kg, brainiac " + brain.TotalMass.ToString("0.0") + " kg");

            // ---------------------------------------------------------------- S1 heavy gate: block -> 150 kg plate
            yield return bot.PushBlock(brute, "S1.Block", "S1.PlateGoal", 0.3f);
            yield return bot.ExpectPowered("S1.Plate", true, 2f);
            yield return bot.ExpectPowered("S1.Gate", true, 4f);
            bot.Snapshot("S1_block_on_plate", brute);
            yield return bot.WalkTo(brute, "S1.BruteClear");
            yield return bot.WalkPath(brute, false, "S1.GateIn", "S1.GateOut", "C1.A");
            yield return bot.WalkPath(spr, false, "S1.LaneL", "S1.LaneL2", "S1.GateIn", "S1.GateOut", "C1.B");
            yield return bot.WalkPath(brain, false, "S1.LaneL", "S1.LaneL2", "S1.GateIn", "S1.GateOut", "C1.C");
            yield return ExpectCheckpoint(bot, 1);

            // ---------------------------------------------------------------- S2 freight elevator (MAX 150 KG)
            var fe = Find<L2FreightElevator>("S2.Panel");
            yield return bot.Expect(() => fe != null, "freight elevator panel found");
            if (bot.Failed) yield break;
            yield return bot.WalkPath(brute, false, "S2.Front", "S2.CarA");
            yield return bot.WalkPath(spr, false, "S2.Front", "S2.CarB");
            yield return bot.Wait(0.4f);
            yield return bot.Interact(brute, "S2.Panel", (BotTarget)"S2.CarA");
            yield return bot.Expect(() => fe.Refusals >= 1 && !fe.IsOn, "elevator refuses Brute + Sprinter (" + fe.LastRefusedKg.ToString("0") + " kg > 150)", 1f);
            bot.Snapshot("S2_overweight", brute);
            yield return bot.WalkTo(spr, "S2.Wait");
            yield return bot.Wait(0.4f);
            yield return bot.Interact(brute, "S2.Panel", (BotTarget)"S2.CarA");
            yield return bot.Expect(() => fe.IsOn, "elevator accepts the Brute alone", 1f);
            yield return bot.WaitUntil(() => fe.AtTop, 10f, "car at the top (Brute)");
            yield return bot.WalkPath(brute, false, "S2.CarExit", "S2.Top1");
            yield return bot.WaitUntil(() => fe.AtBottom && !fe.IsOn, 12f, "empty car returned down");
            yield return bot.WalkTo(spr, "S2.CarA");
            yield return bot.WalkPath(brain, false, "S2.Front", "S2.CarB");
            yield return bot.Wait(0.4f);
            yield return bot.Interact(spr, "S2.Panel", (BotTarget)"S2.CarA");
            yield return bot.Expect(() => fe.IsOn, "elevator accepts Sprinter + Brainiac", 1f);
            yield return bot.WaitUntil(() => fe.AtTop, 10f, "car at the top (Sprinter + Brainiac)");
            yield return bot.WalkPath(spr, false, "S2.CarExit", "S2.Top2");
            yield return bot.WalkTo(brain, "S2.Top3");

            // ---------------------------------------------------------------- S3 wobbly catwalk (single file, on the line)
            foreach (var c in new[] { brute, spr, brain })
            {
                yield return bot.WalkPath(c, false, "S3.Cat0", "S3.Cat1", "S3.Cat2", "S3.Cat3");
                if (c == brute) bot.Snapshot("S3_catwalk_brute", brute);
            }
            yield return bot.WalkTo(brute, "C2.A");
            yield return bot.WalkTo(spr, "C2.B");
            yield return bot.WalkTo(brain, "C2.C");
            yield return ExpectCheckpoint(bot, 2);

            // ---------------------------------------------------------------- S4 counterweight lift
            var lift = Find<PulleyLift>("S4.Pulley");
            yield return bot.Expect(() => lift != null, "pulley lift found");
            if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "S4.A1", false, 0.35f);
            yield return bot.WalkTo(brain, "S4.A2", false, 0.35f);
            yield return bot.Expect(() => lift.MassA > 100f && lift.Offset <= -lift.Travel * 0.5f + 0.05f, "Sprinter + Brainiac wait on PEOPLE (" + lift.MassA.ToString("0") + " kg)", 2f);
            yield return bot.PushBlock(brute, "S4.Block", "S4.BlockNear", 0.3f);
            var block = LevelMark.Find("S4.Block");
            var pitEdge = LevelMark.Find("S4.PitEdge");
            float stopAlong = (pitEdge != null ? pitEdge.transform.position.z : 77.45f) + 0.85f;   // trailing face 5 cm past the edge
            yield return Shove(bot, brute, block != null ? block.transform : null, Vector3.forward, stopAlong, 8f);
            yield return bot.WalkTo(brute, "S4.BruteBack");
            yield return bot.WaitUntil(() => lift.Offset >= lift.Travel * 0.5f - 0.02f, 12f, "PEOPLE platform reached the upper deck");
            bot.Snapshot("S4_lift_up", spr);
            yield return bot.WalkTo(spr, "S4.TopA1");
            yield return bot.WalkTo(brain, "S4.TopA2");
            yield return bot.Interact(brain, "S4.StairsLever", (BotTarget)"S4.StairsLeverStand");
            yield return bot.ExpectPowered("S4.StairsDoor", true, 4f);
            yield return bot.WalkPath(brute, false, "S4.BruteRoute1", "S4.DoorFront", "S4.StairsBottom", "S4.StairsTop", "S4.WalkwayEnd", "S4.UpperArrive");

            // stacking sub-puzzle: Sprinter on the Brainiac's head -> 2.5 m ledge -> crusher OFF lever
            yield return bot.WalkPath(brain, false, "S4.StackApproach");
            yield return bot.WalkTo(brain, "S4.StackBase", false, 0.2f);
            yield return bot.Face(brain, Vector3.forward);
            yield return bot.WalkTo(spr, "S4.StackRunup", false, 0.3f);
            yield return bot.StackOnto(spr, brain);
            yield return bot.ClimbOnto(spr, "S4.LedgeTop");
            yield return bot.Interact(spr, "S4.CrusherLever", (BotTarget)"S4.CrusherLeverStand");
            var crusher = Find<Crusher>("S4.Crusher");
            yield return bot.Expect(() => crusher != null && !crusher.IsActive && crusher.Extension < 0.01f, "S4 crusher parked by the lever", 4f);
            bot.Snapshot("S4_crusher_off", spr);
            yield return bot.WalkTo(spr, "S4.LedgeDrop");
            yield return bot.WaitSettled(spr);
            yield return bot.WalkPath(brute, false, "S4.CrBefore", "S4.CrAfter", "C3.A");
            yield return bot.WalkPath(spr, false, "S4.CrBefore", "S4.CrAfter", "C3.B");
            yield return bot.WalkPath(brain, false, "S4.StackApproach", "S4.CrBefore", "S4.CrAfter", "C3.C");
            yield return ExpectCheckpoint(bot, 3);

            // ---------------------------------------------------------------- S5 scale of justice (all three) + crushers
            yield return bot.WalkTo(brain, "S5.ScaleI", false, 0.35f);
            yield return bot.WalkTo(spr, "S5.ScaleS", false, 0.35f);
            yield return bot.WalkTo(brute, "S5.ScaleB", false, 0.35f);
            var scale = Find<WeightRangePlate>("S5.Scale");
            yield return bot.ExpectPowered("S5.Scale", true, 3f);
            bot.Log("L2 scale reading: " + (scale != null ? scale.CurrentKg.ToString("0.0") : "?") + " kg (200-270)");
            bot.Snapshot("S5_scale", brute);
            yield return bot.ExpectPowered("S5.Gate", true, 4f);
            var c1 = Find<Crusher>("S5.Crusher1");
            var c2 = Find<Crusher>("S5.Crusher2");
            var order = new[] { spr, brain, brute };
            var exits = new[] { "S5.Exit1", "S5.Exit2", "S5.Exit3" };
            for (int i = 0; i < order.Length; i++)
            {
                var c = order[i];
                yield return bot.WalkPath(c, false, "S5.GateFront", "S5.Cr1Before");
                yield return PassCrusher(bot, c, c1, "S5.Mid");
                yield return PassCrusher(bot, c, c2, "S5.Cr2After");
                yield return bot.WalkTo(c, exits[i], false, 0.5f);
            }
            yield return bot.ExpectLevelComplete(15f);
        }

        // ---------------------------------------------------------------- helpers
        static IEnumerator ExpectCheckpoint(TeamBot bot, int n)
        {
            var cp = LevelMark.Find<Checkpoint>("checkpoint." + n);
            yield return bot.Expect(() => cp != null && cp.IsActivated, "checkpoint " + n + " activated", 3f);
        }

        /// <summary>Final shove of a heavy block over a pit edge: keep pushing along dir (motor commands only, centred on
        /// the block) until its trailing face clears the edge (or it starts dropping), then stop at once so the pusher
        /// stays on the deck, and verify the block fell.</summary>
        static IEnumerator Shove(TeamBot bot, ShifterCharacter c, Transform block, Vector3 dir, float stopAlong, float timeout)
        {
            if (block == null) { bot.Fail("Shove: block not found", c); yield break; }
            float y0 = block.position.y, t0 = bot.Now;
            var side = Vector3.Cross(Vector3.up, dir);
            while (block.position.y > y0 - 0.03f && Vector3.Dot(block.position, dir) < stopAlong && bot.Now - t0 < timeout)
            {
                float off = Vector3.Dot(c.transform.position - block.position, side);
                var move = dir + side * Mathf.Clamp(-off * 1.5f, -0.5f, 0.5f);
                bot.Driver.SetMove(c, move.normalized * 0.5f, false);
                yield return null;
            }
            bot.Driver.Stop(c);
            yield return bot.Expect(() => block.position.y < y0 - 1.0f, "block shoved into the CARGO pit", 2f, c);
            yield return bot.Wait(0.3f);
        }

        /// <summary>Wait for the crusher to slam and fully rise again (start of its 1.8 s up phase), then sprint past it.</summary>
        static IEnumerator PassCrusher(TeamBot bot, ShifterCharacter c, Crusher cr, string after)
        {
            if (cr == null) { bot.Fail("crusher not found", c); yield break; }
            yield return bot.WaitUntil(() => cr.Extension > 0.5f, 8f, "crusher slams");
            yield return bot.WaitUntil(() => cr.Extension < 0.001f, 4f, "crusher back up");
            yield return bot.WalkTo(c, after, true, 0.6f);
        }
    }
}
