// =====================================================================================
//  SHIFTERS - L1Solution.cs   (LEVEL 1 solution run, TeamBot)
//  Plays the intended solution of THE TRAINING FACILITY with any random team (characters are
//  referenced by archetype only; the gun by GunOwner). No debug teleports.
//    run_level_test.sh <project> 1 [seed | s1,s2,s3]
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L1Solution
    {
        /// <summary>Entry point (DevPlay -devSetup Shifters.L1Solution.Run). -botSeeds a,b,c runs several teams.</summary>
        public static IEnumerator Run(DevPlayContext ctx)
        {
            var seeds = BotArgs.Seeds();
            if (seeds != null && seeds.Length > 1) return TeamBot.RunSeeds(ctx, seeds, Solve, null, Options(), "L1");
            return TeamBot.Run(ctx, Solve, Options(), "L1");
        }

        /// <summary>DEV ONLY (L1DevTests): push the block to the plate's edge and force D2 open, for kits whose Pushable
        /// can't climb a plate pad yet. Never set by Run (final solution runs).</summary>
        public static bool DevAssistPushPlate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => DevAssistPushPlate = false;

        /// <summary>StrictPlayerThrows: a throw that needs an elevation a player can't aim (outside 38..63 deg) FAILS the run.</summary>
        public static BotOptions Options() => new BotOptions { DiagnosticShots = true, Verbose = true, RunTimeout = 900f, StrictPlayerThrows = true };

        static Vector3 P(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>The whole level, section by section.</summary>
        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("L1 team: " + bot.TeamSummary());
            yield return ArrivalBay(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return SwitchingRoom(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return AptitudeTests(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return BridgeTraining(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return StackAndThrow(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return WeaponRange(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return SacrificeLesson(bot, brute, spr, brain); if (bot.Failed) yield break;
            yield return Graduation(bot, brute, spr, brain); if (bot.Failed) yield break;
            FinalChecks(bot);
        }

        static IEnumerator Seq(TeamBot bot, params IEnumerator[] steps) { foreach (var s in steps) { if (bot.Failed) yield break; yield return s; } }

        static IEnumerator Checkpoint(TeamBot bot, int index)
        {
            yield return bot.GatherAt("checkpoint." + index);
            if (bot.Failed) yield break;
            yield return bot.Expect(() => IsCheckpointActive(index), "checkpoint " + index + " activated", 2f);
        }

        static bool IsCheckpointActive(int index)
        {
            foreach (var c in Shifters.Checkpoint.All) if (c != null && c.Index == index) return c.IsActivated;
            return false;
        }

        // ------------------------------------------------------------------ S0
        static IEnumerator ArrivalBay(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // one lane per intern, by current x so nobody crosses paths
            var team = new List<ShifterCharacter> { brute, spr, brain };
            team.Sort((a, b) => a.Feet.x.CompareTo(b.Feet.x));
            float[] lanes = { -3f, 0f, 3f };
            var r = new IEnumerator[3];
            for (int i = 0; i < 3; i++) r[i] = bot.WalkTo(team[i], P(lanes[i], 0f, 7.2f), false, 0.4f);
            yield return bot.Parallel(r); if (bot.Failed) yield break;
            for (int i = 0; i < 3; i++) r[i] = bot.ClimbOnto(team[i], P(lanes[i], L1Builder.StepTop, 11.8f));
            yield return bot.Parallel(r); if (bot.Failed) yield break;
            for (int i = 0; i < 3; i++) r[i] = bot.JumpGap(team[i], P(lanes[i], L1Builder.StepTop, L1Builder.Gap0Start - 0.15f), P(lanes[i], L1Builder.StepTop, L1Builder.Gap0End + 1.7f));
            yield return bot.Parallel(r); if (bot.Failed) yield break;
            for (int i = 0; i < 3; i++) r[i] = Seq(bot, bot.ClimbOnto(team[i], P(lanes[i], L1Builder.HurdleTop, (L1Builder.HurdleZ0 + L1Builder.HurdleZ1) * 0.5f)),
                                                   bot.WalkTo(team[i], P(lanes[i], L1Builder.StepTop, 26.2f), false, 0.5f));
            yield return bot.Parallel(r); if (bot.Failed) yield break;
            for (int i = 0; i < 3; i++) r[i] = bot.WalkTo(team[i], P(lanes[i], 0f, 36f), false, 0.5f);
            yield return bot.Parallel(r);
        }

        // ------------------------------------------------------------------ S1
        static IEnumerator SwitchingRoom(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.WalkPath(brain, false, P(brain.Feet.x, 0f, 37.8f), P(-5f, 0f, 37.8f), "S1.P1"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S1.D1", true, 3f); if (bot.Failed) yield break;
            yield return bot.Wait(0.6f);
            float bx = brute.Feet.x < spr.Feet.x ? 1.2f : 2.8f, sx = bx < 2f ? 2.8f : 1.2f;
            yield return bot.Parallel(bot.WalkPath(brute, false, P(bx, 0f, 39.4f), P(bx, 0f, 44.8f)),
                                      bot.WalkPath(spr, false, P(sx, 0f, 39.4f), P(sx, 0f, 44.8f)));
            if (bot.Failed) yield break;
            yield return bot.Interact(brute, "S1.L1"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S1.L1", true, 1f); if (bot.Failed) yield break;
            yield return bot.WalkPath(brain, false, P(-5f, 0f, 37.3f), P(-2f, 0f, 37.3f), "S1.DoorNear", "S1.DoorFar"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S1.D1", true, 0.5f); if (bot.Failed) yield break;
            yield return Checkpoint(bot, 1);
        }

        // ------------------------------------------------------------------ S2
        static IEnumerator AptitudeTests(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var push = Seq(bot, bot.WalkPath(brute, false, P(-3f, 0f, 55.5f), "S2.STR.Entry"),
                           bot.PushBlock(brute, "S2.STR.Block", DevAssistPushPlate ? "S2.STR.PlateEdge" : "S2.STR.Goal"),
                           DevAssistPushPlate ? bot.Wait(0.1f) : bot.ExpectPowered("S2.STR.Plate", true, 3f),
                           bot.WalkPath(brute, false, P(-12f, 0f, 60.5f), P(-6f, 0f, 60.5f), P(-3f, 0f, 66f)));
            var others = Seq(bot, bot.Wait(1.6f),
                             bot.WalkPath(brain, false, P(-2f, 0f, 63f), "S2.IQ.Entry"),
                             bot.Hack(brain, "S2.IQ.Terminal"),
                             bot.WalkPath(brain, false, "S2.IQ.Entry", P(-1f, 0f, 67f)),
                             bot.WalkPath(spr, false, P(5f, 0f, 58.2f), P(L1Builder.SpdButtonX, 0f, 58.2f)),
                             bot.Interact(spr, "S2.SPD.Button", P(L1Builder.SpdButtonX, 0f, 56.95f)),
                             bot.WalkTo(spr, "S2.SPD.Plate", true, 0.5f, 12f),
                             bot.ExpectPowered("S2.SPD.Plate", true, 2f),
                             bot.Wait(1.3f),
                             bot.WalkPath(spr, false, "S2.SPD.Back", "S2.SPD.Entry", P(2f, 0f, 66f)));
            yield return bot.Parallel(push, others); if (bot.Failed) yield break;
            if (DevAssistPushPlate && !TeamBot.IsPowered("S2.D2"))
            {
                bot.Warn("!!! DEV ASSIST: forcing D2 open (block parked at the plate edge) - not a valid solution run");
                var d2 = LevelMark.Find<Door>("S2.D2");
                if (d2 != null) d2.ForcePower(true);
            }
            yield return bot.ExpectPowered("S2.D2", true, 3f); if (bot.Failed) yield break;
            yield return bot.Wait(1.2f);
            yield return bot.GatherAt("S2.D2Near"); if (bot.Failed) yield break;
            yield return Checkpoint(bot, 2);
        }

        // ------------------------------------------------------------------ S3
        static IEnumerator BridgeTraining(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.WalkTo(brute, "S3.BridgeSpot", false, 0.2f); if (bot.Failed) yield break;
            yield return bot.FormBridge(brute, Vector3.forward); if (bot.Failed) yield break;
            bot.Snapshot("s3_bridge_formed", brute);
            yield return bot.WalkPath(brain, false, "S3.Approach", "S3.Far"); if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, false, "S3.Approach", P(1.2f, 0f, L1Builder.BridgeGapEnd + 4.6f)); if (bot.Failed) yield break;
            yield return bot.RecoverBridge(brute); if (bot.Failed) yield break;
            yield return bot.Expect(() => brute.Feet.z > L1Builder.BridgeGapEnd, "the bridge snapped to the far side", 1f, brute); if (bot.Failed) yield break;
            yield return Checkpoint(bot, 3);
        }

        // ------------------------------------------------------------------ S4
        static IEnumerator StackAndThrow(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // (a) stack: the Sprinter jumps on the Brainiac's head, climbs the 2.5 m ledge, presses the button
            yield return bot.WalkTo(brain, "S4.StackBottom", false, 0.2f); if (bot.Failed) yield break;
            yield return bot.Parallel(bot.WalkTo(spr, P(3.5f, 0f, L1Builder.PlateauZ0 - 4.6f), false, 0.4f),
                                      bot.WalkTo(brute, P(-1f, 0f, 105.5f), false, 0.5f));
            if (bot.Failed) yield break;
            yield return bot.Face(brain, Vector3.forward);
            yield return bot.StackOnto(spr, brain); if (bot.Failed) yield break;
            bot.Snapshot("s4_stacked", spr);
            yield return bot.ClimbOnto(spr, "S4.StackTop"); if (bot.Failed) yield break;
            yield return bot.Interact(spr, "S4.Button"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S4.Ramp", true, 2f); if (bot.Failed) yield break;
            var ramp = LevelMark.Find<L1FoldingRamp>("S4.Ramp");
            yield return bot.WaitUntil(() => ramp == null || ramp.IsLowered, 4f, "ramp lowered"); if (bot.Failed) yield break;
            yield return bot.WalkPath(brute, false, P(-4f, 0f, 104.8f), "S4.RampFoot", "S4.RampTop", P(1.5f, L1Builder.PlateauTop, 119.6f)); if (bot.Failed) yield break;
            yield return bot.WalkPath(brain, false, P(3.5f, 0f, 109f), P(-4f, 0f, 104.8f), "S4.RampFoot", "S4.RampTop", P(-1.5f, L1Builder.PlateauTop, 119.6f)); if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, P(0f, L1Builder.PlateauTop, 119.2f), false, 0.5f); if (bot.Failed) yield break;
            yield return bot.Parallel(bot.WalkPath(brute, false, P(1.5f, L1Builder.PlateauTop, 120.6f), P(1.5f, 0f, 129.2f)),
                                      bot.WalkPath(brain, false, P(-1.5f, L1Builder.PlateauTop, 120.6f), P(-1.5f, 0f, 129.2f), P(-4.5f, 0f, 131f)),
                                      bot.WalkPath(spr, false, P(0f, L1Builder.PlateauTop, 120.6f), P(0f, 0f, 130.8f)));
            if (bot.Failed) yield break;

            // (b) throw: the Brute throws the Sprinter onto the 3.6 m balcony; lever L4 opens D4 and unfolds the stairs
            yield return bot.Parallel(bot.WalkTo(spr, "S4.PickupSpot", false, 0.4f), bot.WalkTo(brute, P(4.6f, 0f, 130.6f), false, 0.4f));
            if (bot.Failed) yield break;
            yield return bot.Grab(brute, spr); if (bot.Failed) yield break;
            // throw like a player: carry the flier onto the THROW FROM HERE pad, face the balcony, look up a little (player
            // throws are Tuning.ThrowBaseAngle + 0..25 deg). Options.StrictPlayerThrows fails the run if that is not enough.
            // Planned on the pad, i.e. long after the 0.2 s lift, with the real carried height.
            yield return bot.WalkTo(brute, "S4.ThrowPad", false, 0.15f); if (bot.Failed) yield break;
            PlanThrow(brute, spr, out var throwFrom, out var landing, out float elevation);
            bot.Options.ThrowElevation = elevation;
            bot.Log("S4 throw plan: v " + brute.Grabber.ThrowSpeedFor(spr.Grabbable).ToString("0.00") + " m/s, carried centre "
                    + (spr.Center.y - brute.Feet.y).ToString("0.00") + " m up, stand at " + BotFmt.V(throwFrom) + " (pad z " + L1Builder.ThrowPadZ0 + ".."
                    + L1Builder.ThrowPadZ1 + "), look up " + (elevation - Tuning.ThrowBaseAngle).ToString("0") + " deg = " + elevation.ToString("0")
                    + " deg throw, expected touchdown z " + (landing.z - bot.Options.ThrowSlideAllowance).ToString("0.0"));
            yield return bot.WalkTo(brute, throwFrom, false, 0.15f); if (bot.Failed) yield break; // feet on the pad
            yield return bot.ThrowTo(brute, landing, throwFrom, 8f); if (bot.Failed) yield break;
            yield return bot.Expect(() => OnBalcony(spr), spr.DisplayName + " is on the balcony", 1f, spr); if (bot.Failed) yield break;
            bot.Snapshot("s4_thrown", spr);
            yield return bot.Interact(spr, "S4.L4"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S4.D4", true, 2f); if (bot.Failed) yield break;
            var stairs = LevelMark.Find<L1FoldingRamp>("S4.Stairs");
            yield return bot.WaitUntil(() => stairs == null || stairs.IsLowered, 4f, "stairs unfolded"); if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, false, "S4.StairsTop", "S4.StairsBottom", P(-1.5f, 0f, 130.4f)); if (bot.Failed) yield break;
            yield return bot.WalkPath(brute, false, P(4.6f, 0f, 131.5f), P(-0.8f, 0f, 133.2f), "S4.D4Near", "S4.D4Far", P(3f, 0f, 156f)); if (bot.Failed) yield break;
            yield return bot.WalkPath(brain, false, "S4.D4Near", "S4.D4Far", P(1.5f, 0f, 158f)); if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, false, P(-1.5f, 0f, 133f), "S4.D4Near", "S4.D4Far", P(4.5f, 0f, 159.5f));
        }

        /// <summary>Look-up angles a player would use (38 deg = camera level, +25 = max look-up), steep branch only.</summary>
        static readonly float[] NaturalThrowAngles = { 55f, 58f, 52f, 60f, 62f };

        /// <summary>Where on the THROW FROM HERE pad to stand and how far to look up: the most natural angle (then the pad
        /// middle, back, front) whose arc (same model as TeamBot.ThrowTo: BotBallistics, motor fall gravity for the flier)
        /// comes down on the balcony at least 1 m past its lip and short of the back wall. Angles whose reach is within
        /// 0.35 m of the throw's maximum reach are skipped: there a few cm of foot placement would leave no player angle.
        /// landing = that touchdown plus ThrowTo's slide allowance (ThrowTo aims the touchdown that much short of it).
        /// Call with the flier already carried overhead (the lift takes CharacterGrabber.LiftTime).</summary>
        static void PlanThrow(ShifterCharacter thrower, ShifterCharacter flier, out Vector3 from, out Vector3 landing, out float elevation)
        {
            float gUp = Mathf.Abs(Physics.gravity.y), gDown = gUp * Tuning.FallGravityMultiplier;
            float v = thrower.Grabber.ThrowSpeedFor(flier.Grabbable);
            float releaseUp = flier.Center.y - thrower.Feet.y;                     // carried overhead
            float dh = L1Builder.BalconyTop + flier.Radius + 0.1f - releaseUp;     // ThrowTo aims the centre this high
            const float lead = 0.1f;                                                // the carried body sits ~0.1 m ahead of the feet
            const float slide = 1f;                                                 // BotOptions.ThrowSlideAllowance default
            float minTd = L1Builder.BalconyZ0 + 1f, maxTd = L1Builder.BalconyZ1 - 0.5f;
            float rMax = 0f;
            for (float a = Tuning.ThrowBaseAngle; a <= Tuning.ThrowBaseAngle + 25f + 1e-3f; a += 0.5f)
            {
                float r = BotBallistics.RangeForHeight(v, a, dh, gUp, gDown);
                if (!float.IsNaN(r)) rMax = Mathf.Max(rMax, r);
            }
            float[] spots = { L1Builder.ThrowPadZ, L1Builder.ThrowPadZ0 + 0.15f, L1Builder.ThrowPadZ1 - 0.15f };
            foreach (float a in NaturalThrowAngles)
                foreach (float z in spots)
                {
                    float r = BotBallistics.RangeForHeight(v, a, dh, gUp, gDown);
                    if (float.IsNaN(r) || r > rMax - 0.35f) continue;
                    float td = z + lead + r;
                    if (td < minTd || td > maxTd) continue;
                    from = P(L1Builder.ThrowPadX, 0f, z);
                    landing = P(L1Builder.ThrowPadX, L1Builder.BalconyTop, td + slide);
                    elevation = a;
                    return;
                }
            // not expected for any team (see L1Builder.ThrowPad*): pad middle to the balcony middle; ThrowTo reports the angle
            from = P(L1Builder.ThrowPadX, 0f, L1Builder.ThrowPadZ);
            landing = P(L1Builder.ThrowPadX, L1Builder.BalconyTop, (L1Builder.BalconyZ0 + L1Builder.BalconyZ1) * 0.5f + slide);
            elevation = 50f;
        }

        static bool OnBalcony(ShifterCharacter c)
        {
            var f = c.Feet;
            return c.IsAlive && f.y > L1Builder.BalconyTop - 0.3f && f.x > L1Builder.BalconyX0 - 0.1f && f.x < L1Builder.BalconyX1 + 0.1f
                   && f.z > L1Builder.BalconyZ0 - 0.1f && f.z < L1Builder.D4Z;
        }

        // ------------------------------------------------------------------ S5
        static IEnumerator WeaponRange(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var gun = bot.GunOwner;
            if (gun == null) { bot.Fail("nobody holds the gun"); yield break; }
            bot.Log("gun owner: " + gun.DisplayName + " (" + gun.Archetype + ")");
            yield return bot.WalkTo(gun, "S5.ShootSpot", false, 0.4f); if (bot.Failed) yield break;
            foreach (var t in new[] { "S5.T1", "S5.T2", "S5.T3" })
            {
                yield return bot.ShootAt(gun, t, null, 24, 25f);
                if (bot.Failed) yield break;
            }
            yield return bot.ExpectPowered("S5.D5", true, 3f); if (bot.Failed) yield break;
            yield return BonusShot(bot, gun);
            yield return bot.Wait(0.8f);
            yield return bot.GatherAt("S5.D5Near"); if (bot.Failed) yield break;
            yield return Checkpoint(bot, 4);
        }

        /// <summary>Optional bonus: knock the barrel off its shelf onto the 25 kg plate. Logged, never fails the run.</summary>
        static IEnumerator BonusShot(TeamBot bot, ShifterCharacter gun)
        {
            var barrel = LevelMark.Find("S5.Barrel");
            var plate = LevelMark.Find<PressurePlate>("S5.BonusPlate");
            if (barrel == null || plate == null || gun.Weapon == null) yield break;
            yield return bot.FaceTarget(gun, barrel.transform);
            int shots = 0;
            float t0 = bot.Now;
            while (bot.Now - t0 < 8f && !plate.IsOn && shots < 8)
            {
                if (barrel.transform.position.y > 2.2f && gun.Weapon.FireAt(barrel.transform.position + Vector3.up * 0.3f)) shots++;
                yield return null;
            }
            float t1 = bot.Now;
            while (bot.Now - t1 < 3f && !plate.IsOn) yield return null;
            bot.Log("bonus barrel: " + (plate.IsOn ? "NICE SHOT lit" : "missed (optional)") + " after " + shots + " shot(s)");
        }

        // ------------------------------------------------------------------ S6
        static IEnumerator SacrificeLesson(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.WalkPath(brute, false, "S6.BoothEntry", "S6.BoothInside", "S6.P6"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S6.D6", true, 3f); if (bot.Failed) yield break;
            yield return bot.Wait(0.8f);
            var left = spr.Feet.x < brain.Feet.x ? spr : brain; var right = left == spr ? brain : spr;
            yield return bot.Parallel(bot.WalkPath(left, false, P(-0.75f, 0f, 193.4f), P(-0.75f, 0f, 199.6f), P(1.2f, 0f, 205f)),
                                      bot.WalkPath(right, false, P(0.8f, 0f, 193.4f), P(0.8f, 0f, 199.6f), P(3.4f, 0f, 206f)));
            if (bot.Failed) yield break;
            yield return bot.WalkTo(brute, "S6.BoothBack", false, 0.4f); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S6.D6", false, 5f); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S6.Vent", true, 3f); if (bot.Failed) yield break;
            yield return bot.Wait(1.3f);
            yield return bot.WalkPath(brute, false, "S6.VentIn", "S6.Duct1End", "S6.Duct2Start"); if (bot.Failed) yield break;
            yield return bot.JumpGap(brute, "S6.Gap1Takeoff", "S6.Gap1Landing"); if (bot.Failed) yield break;
            yield return bot.JumpGap(brute, "S6.Gap2Takeoff", "S6.Gap2Landing"); if (bot.Failed) yield break;
            yield return bot.WalkPath(brute, false, "S6.Duct3Start", "S6.Rejoin"); if (bot.Failed) yield break;
            yield return Checkpoint(bot, 5);
        }

        // ------------------------------------------------------------------ S7
        static IEnumerator Graduation(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var byX = new List<ShifterCharacter> { brute, spr, brain };
            byX.Sort((a, b) => a.Feet.x.CompareTo(b.Feet.x));
            yield return bot.Parallel(bot.WalkTo(byX[0], "S7.PA", false, 0.4f), bot.WalkTo(byX[1], "S7.PC", false, 0.4f), bot.WalkTo(byX[2], "S7.PB", false, 0.4f));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("S7.Gate", true, 3f); if (bot.Failed) yield break;
            yield return bot.Wait(1.4f);
            yield return bot.GatherAt("S7.GateNear"); if (bot.Failed) yield break;
            // single file through the 3.2 m elevator door, then spread out inside the cab (console on the right wall)
            var line = new List<ShifterCharacter> { brute, spr, brain };
            line.Sort((a, b) => Mathf.Abs(a.Feet.x).CompareTo(Mathf.Abs(b.Feet.x)));
            Vector3[] spots = { P(-1.1f, 0.1f, L1Builder.ExitZ + 3.3f), P(0.9f, 0.1f, L1Builder.ExitZ + 3.4f), P(-0.4f, 0.1f, L1Builder.ExitZ + 1.4f) };
            for (int i = 0; i < line.Count; i++)
            {
                yield return bot.WalkPath(line[i], false, P(0f, 0f, L1Builder.ExitZ - 1.4f), P(0f, 0.1f, L1Builder.ExitZ + 0.7f), spots[i]);
                if (bot.Failed) yield break;
            }
            yield return bot.ExpectLevelComplete(15f);
        }

        // ------------------------------------------------------------------ final assertions
        static readonly string[] MustFire = { "welcome", "movement", "switching", "team_orders", "bridge", "bridge_cross", "stacking", "throwing", "weapon", "sacrifice", "recovery" };

        static void FinalChecks(TeamBot bot)
        {
            int cps = 0, active = 0;
            foreach (var c in Shifters.Checkpoint.All) { if (c == null) continue; cps++; if (c.IsActivated) active++; }
            bot.Ctx.Check(cps == 5 && active == cps, "[L1] every checkpoint activated (" + active + "/" + cps + ")");
            var fired = new List<string>();
            var missed = new List<string>();
            foreach (var id in new[] { "welcome", "movement", "switching", "team_orders", "perspective", "stats", "strength", "intellect", "speed", "bridge", "bridge_cross", "stacking", "throwing", "weapon", "sacrifice", "recovery", "graduation", "exit" })
                (TutorialFired(id) ? fired : missed).Add(id);
            bot.Log("tutorials fired: " + string.Join(", ", fired) + (missed.Count > 0 ? "  | not triggered by the bot's active intern: " + string.Join(", ", missed) : ""));
            foreach (var id in MustFire) bot.Ctx.Check(TutorialFired(id), "[L1] tutorial '" + id + "' fired");
            var mats = SceneAudit.MaterialProblems(20);
            bot.Ctx.Check(mats.Count == 0, "[L1] no default/magenta/missing materials (" + mats.Count + (mats.Count > 0 ? ": " + mats[0] : "") + ")");
        }

        static bool TutorialFired(string id)
        {
            var go = LevelMark.Find("tutorial." + id);
            if (go == null) return false;
            var t = go.GetComponent<TutorialTrigger>(); if (t != null) return t.Fired;
            var s = go.GetComponent<L1SignalTutorial>(); if (s != null) return s.Fired;
            var b = go.GetComponent<L1BridgeTutorial>(); if (b != null) return b.Fired;
            return false;
        }
    }
}
