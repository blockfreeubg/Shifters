// =====================================================================================
//  SHIFTERS - L10Solution.cs   [LEVEL 10 - LIGHTS OUT, TeamBot solution, no debug assists]
//  The safe pattern = pairs + parking in pools (dummies only hunt SCARED interns):
//    S0 gun owner swaps the gun for the PARTY POPPER (tool path; -l10NoTool skips it) and lobs one flare into the
//       farm entrance -> checkpoint 1
//    S1 team to lamp pool B; Brainiac parks lit and GUARDS (flashlight on the dummies) while Brute + Sprinter push
//       the battery onto the mailroom plate (BREAKER 1); Sprinter runs the motion hall (BREAKER 2, lit during the
//       window); Brute and Brainiac meet, walk the UV arrows as a pair and hack the IT terminal (BREAKER 3);
//       everyone through the ELEVATOR-LOBBY door -> checkpoint 2
//    S2 copy room: the phone is answered, the Brute walks past the paper shelf -> checkpoint 3
//    S3 Brute holds the FLOODLIGHT plate (E corridor lit), Sprinter calls the elevator, Brainiac holds the W corridor
//       with the flashlight for the 10 s wait, DING -> everyone into the cab -> POWER RESTORED -> complete.
//  Run: run_level_test.sh <proj> 10 1,2,3,MIN,MAX   (tool-less: ... 10 1 -l10NoTool 1)
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L10Solution
    {
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, Options(), "L10");

        public static BotOptions Options() => new BotOptions { StrictPlayerThrows = true };

        /// <summary>Tests: called after each section with its name (screenshots).</summary>
        public static Action<string> SectionDone;
        public static bool UseToolOverride = true;
        /// <summary>Capture-and-rescue proof (also -l10Hug 1): at C3 the Brainiac ends up in the S3 TIME-OUT cage (the hug's end
        /// state, HoldingCell.Imprison), the Sprinter pulls the release lever, both rejoin and S3 continues.</summary>
        public static bool HugOverride;
        /// <summary>Set when the hug proof ran: the prisoner was captured, then released Alive.</summary>
        public static bool HugCaptured, HugRescued;

        static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

        static bool NoToolArg()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++) if (a[i] == "-l10NoTool") return true;
            return false;
        }

        static bool HugArg()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++) if (a[i] == "-l10Hug") return true;
            return false;
        }

        public static IEnumerator Solve(TeamBot bot)
        {
            HugCaptured = HugRescued = false;
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bool useTool = UseToolOverride && !NoToolArg();
            bot.Log("L10 team: " + bot.TeamSummary() + (useTool ? " (tool path)" : " (TOOL-LESS fallback)"));
            yield return S0(bot, useTool);
            if (bot.Failed) yield break;
            SectionDone?.Invoke("s0");
            yield return S1(bot, brute, spr, brain);
            if (bot.Failed) yield break;
            SectionDone?.Invoke("s1");
            yield return S2(bot, brute, spr, brain);
            if (bot.Failed) yield break;
            SectionDone?.Invoke("s2");
            if ((HugOverride || HugArg()) && brain != null && spr != null && brain != spr)
            {
                yield return HugRescue(bot, spr, brain);
                if (bot.Failed) yield break;
            }
            yield return S3(bot, brute, spr, brain);
            SectionDone?.Invoke("s3");
        }

        // ------------------------------------------------------------------ S0
        static IEnumerator S0(TeamBot bot, bool useTool)
        {
            if (useTool)
            {
                var owner = bot.GunOwner;
                yield return bot.Interact(owner, "s0.locker", "s0.lockerStand");
                if (bot.Failed) yield break;
                yield return bot.Expect(() => owner.Weapon != null && owner.Weapon.HasWeapon && TeamBot.IsTool(owner.Weapon.Data), "gun owner holds the PARTY POPPER", 2f);
                if (bot.Failed) yield break;
                yield return bot.WalkTo(owner, V(1.2f, 2.5f));
                yield return bot.ShootWithGun(new Vector3(0f, 0.3f, 13f), () => PartyFlare.Current != null, 2, 6f);
                if (bot.Failed) yield break;
            }
            yield return bot.GatherAt("checkpoint.1");
            yield return ExpectCheckpoint(bot, 1);
        }

        // ------------------------------------------------------------------ S1
        static IEnumerator S1(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // the whole team to lamp pool B
            yield return Group(bot, V(0f, 12f), V(0f, 19.6f), V(-3.2f, 20.4f));
            if (bot.Failed) yield break;
            yield return bot.WalkTo(brain, V(-4.2f, 20.6f), false, 0.5f);      // around fuse box 1's pillar (it stands in pool B)
            if (bot.Failed) yield break;
            yield return bot.WalkTo(brain, "s1.pool.b", false, 0.5f);
            if (bot.Failed) yield break;

            // BREAKER 1: Brute + Sprinter (a pair: never scared) to the mailroom; the Brainiac guards from the pool
            yield return bot.WithGuard(Pair(bot, brute, spr, V(-4.5f, 20f), V(-8f, 20.2f), V(-8f, 29.5f), V(-11.5f, 29.5f), V(-12.2f, 24.2f)), brain);
            if (bot.Failed) yield break;
            yield return bot.WithGuard(bot.Parallel(
                bot.PushBlock(brute, "s1a.battery", "s1a.plate.center", 0.35f, 40f),
                bot.WalkTo(spr, V(-12f, 27.5f))), brain);
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1.breaker1", true, 2f);
            if (bot.Failed) yield break;
            SectionDone?.Invoke("s1a");
            yield return bot.WalkTo(brute, V(-13.4f, 30.6f), false, 0.6f);   // waits in the now-lit mailroom pool

            // BREAKER 2: the Sprinter runs the motion hall
            yield return bot.WithGuard(bot.WalkPath(spr, true, V(-11.5f, 29.5f), V(-8f, 29.5f), V(-8f, 20.2f), V(0f, 20f), V(0f, 13.5f), V(11.8f, 13.5f), "s1b.stand"), brain);
            if (bot.Failed) yield break;
            yield return bot.Interact(spr, "s1b.timed", "s1b.stand");
            if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "s1b.leverStand", true, 0.9f, 10f);
            if (bot.Failed) yield break;
            yield return bot.Interact(spr, "s1b.lever", "s1b.leverStand");
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1b.lever", true, 2f);
            if (bot.Failed) yield break;
            SectionDone?.Invoke("s1b");

            // BREAKER 3: Brute and Brainiac meet, then walk the UV arrows as a pair
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(-11.5f, 29.5f), V(-8.2f, 28.2f)),
                bot.WalkPath(brain, false, V(-5.6f, 21.0f), V(-8f, 21.2f), V(-8f, 26.6f)));
            if (bot.Failed) yield break;
            yield return Pair(bot, brain, brute, V(-8f, 30f), V(-1f, 30f), "s1c.uv.1", "s1c.uv.2", V(8.6f, 46.5f), "s1c.door", V(12.6f, 46.2f));
            if (bot.Failed) yield break;
            yield return bot.Hack(brain, "s1c.terminal", "s1c.termStand");
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1.lobbyDoor", true, 3f);
            if (bot.Failed) yield break;
            SectionDone?.Invoke("s1c");
            yield return bot.WalkPath(spr, false, V(12.6f, 41.4f), V(12.8f, 44.4f), V(14f, 45.6f));   // around the lever post
            if (bot.Failed) yield break;
            yield return Group(bot, V(11.2f, 46.5f), V(8.6f, 46.8f), V(0.4f, 47f), V(0f, 51.5f));
            if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.2");
            yield return ExpectCheckpoint(bot, 2);
        }

        // ------------------------------------------------------------------ S2
        static IEnumerator S2(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var phone = LevelMark.Find<PhoneCallView>("s2.phone");
            yield return bot.Parallel(
                bot.WalkPath(brute, false, "s2.lane.1", "s2.lane.2", "s2.shelf"),
                bot.WalkPath(brain, false, "s2.lane.1", "s2.lane.2"),
                bot.WalkTo(spr, "s2.phoneStand"));
            if (bot.Failed) yield break;
            if (phone != null)
            {
                yield return bot.WaitUntil(() => phone.State >= 1, 8f, "the phone rings");
                if (bot.Failed) yield break;
                if (phone.State == 1) yield return bot.Interact(spr, phone, "s2.phoneStand");
                if (bot.Failed) yield break;
            }
            var dir = L10Director.Instance;
            yield return bot.Expect(() => dir == null || dir.AvalanchePlayed, "paper avalanche on the Brute", 2f);
            if (bot.Failed) yield break;
            yield return Group(bot, V(0f, 63.5f), V(0f, 67.5f));
            if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.3");
            yield return ExpectCheckpoint(bot, 3);
        }

        // ------------------------------------------------------------------ S3
        static IEnumerator S3(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(2.2f, 72.5f), "s3.flood"),
                bot.WalkPath(brain, false, V(-2.4f, 72.5f), "s3.guardW"),
                bot.WalkTo(spr, "s3.callStand"));
            if (bot.Failed) yield break;
            var flood = LevelMark.Find<PressurePlate>("s3.flood");
            yield return bot.Expect(() => flood != null && flood.IsOn, "the Brute holds the FLOODLIGHT plate", 3f);
            if (bot.Failed) yield break;
            yield return bot.Interact(spr, "s3.call", "s3.callStand");
            if (bot.Failed) yield break;
            yield return bot.Switch(brain);      // the flashlight rides the ACTIVE intern (Interact switched to the Sprinter)
            if (bot.Failed) yield break;
            var dir = L10Director.Instance;
            yield return bot.Parallel(
                bot.FaceAndHold(brain, "s3.corridorW", 12f, () => dir != null && dir.Dinged),
                bot.WalkTo(spr, "s3.cabWait"),
                Moment(bot, 4f, "s3stand"));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s3.cabDoor", true, 3f);
            if (bot.Failed) yield break;
            yield return bot.EnterExit();
            if (bot.Failed) yield break;
            yield return bot.Expect(() => dir == null || dir.Revealed, "POWER RESTORED reveal", 3f);
            if (SectionDone != null)
            {
                yield return bot.Wait(0.7f); SectionDone("s3reveal");      // crowd + banners from the cinematic pose
                yield return bot.Wait(1.05f); SectionDone("s3gag");        // the cardboard hand + "OUCH. PAPER CUT."
            }
            yield return bot.ExpectLevelComplete(15f);
        }

        // ------------------------------------------------------------------ capture-and-rescue proof
        static IEnumerator HugRescue(TeamBot bot, ShifterCharacter rescuer, ShifterCharacter victim)
        {
            var cell = HoldingCell.Find(L10Builder.CellS3Id);
            yield return bot.Expect(() => cell != null, "S3 TIME-OUT cell exists", 1f);
            if (bot.Failed) yield break;
            bot.Log("L10 hug proof: " + victim.name + " is hugged into the TIME-OUT cage");
            cell.Imprison(victim);
            yield return bot.Expect(() => victim.State == CharacterState.Captured && cell.PrisonerCount > 0, "victim held in the TIME-OUT cage", 2f);
            if (bot.Failed) yield break;
            HugCaptured = true;
            yield return Moment(bot, 0.6f, "s3hug");
            yield return bot.WalkPath(rescuer, false, V(0.6f, 64f), V(-0.8f, 58.5f), "s3.cellLeverStand");
            if (bot.Failed) yield break;
            yield return bot.Interact(rescuer, "s3.cellLever", "s3.cellLeverStand");
            if (bot.Failed) yield break;
            yield return bot.Expect(() => victim.State == CharacterState.Alive, "victim rescued (Alive)", 3f);
            if (bot.Failed) yield break;
            HugRescued = true;
            yield return bot.Parallel(
                bot.WalkPath(victim, false, V(-1.6f, 53.4f), V(-0.6f, 58.5f), V(0f, 64f), V(-0.6f, 68.6f)),
                bot.WalkPath(rescuer, false, V(-0.4f, 60f), V(0.4f, 64.5f), V(0.8f, 68.6f)));
        }

        // ------------------------------------------------------------------ helpers
        static IEnumerator Moment(TeamBot bot, float after, string name)
        {
            if (SectionDone == null) yield break;
            yield return bot.Wait(after);
            SectionDone(name);
        }

        /// <summary>Two interns walk the same waypoints side by side (b 1.2 m behind a); they re-sync at every waypoint so
        /// they never drift more than ~4 m apart (buddies, never SCARED).</summary>
        static IEnumerator Pair(TeamBot bot, ShifterCharacter a, ShifterCharacter b, params BotTarget[] pts)
        {
            Vector3 prev = a.Feet;
            foreach (var t in pts)
            {
                var p = t.Position;
                var dir = p - prev; dir.y = 0f;
                var back = dir.sqrMagnitude > 0.01f ? -dir.normalized * 1.2f : Vector3.zero;
                var side = dir.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, dir.normalized) * 0.5f : Vector3.zero;
                yield return bot.Parallel(bot.WalkTo(a, t, false, 0.6f), bot.WalkTo(b, p + back + side, false, 0.9f));
                if (bot.Failed) yield break;
                prev = p;
            }
        }

        /// <summary>The whole team walks the waypoints together (lateral offsets 1.1 m).</summary>
        static IEnumerator Group(TeamBot bot, params Vector3[] pts)
        {
            var team = new[] { bot.Brute, bot.Sprinter, bot.Brainiac };
            Vector3 prev = bot.Sprinter.Feet;
            foreach (var p in pts)
            {
                var dir = p - prev; dir.y = 0f;
                var side = dir.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, dir.normalized) * 1.1f : Vector3.right * 1.1f;
                yield return bot.Parallel(bot.WalkTo(team[0], p - side, false, 0.8f), bot.WalkTo(team[1], p, false, 0.8f), bot.WalkTo(team[2], p + side, false, 0.8f));
                if (bot.Failed) yield break;
                prev = p;
            }
        }

        static IEnumerator ExpectCheckpoint(TeamBot bot, int index)
        {
            var cp = LevelMark.Find<Checkpoint>("checkpoint." + index);
            yield return bot.Expect(() => cp != null && cp.IsActivated, "checkpoint " + index + " activated", 6f);
        }
    }
}
