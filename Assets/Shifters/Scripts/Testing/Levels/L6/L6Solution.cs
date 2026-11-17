// =====================================================================================
//  SHIFTERS - L6Solution.cs   [CH03 PERFORMANCE REVIEW - TeamBot solution, no debug assists]
//  S0: the gun owner swaps the gun for the BIT-SHIFTER at the locker -> C1 -> into the arena (the fight begins).
//  P1: BIT-SHIFT crate A to HUGE (100 kg), the Brute carries it onto pad W and steps off; the press jams;
//      the tool holder shoots the heart twice (one bait parked ACTIVE in a break area the whole time).
//      Fallback (-l6Fallback 1, seed 1): no locker; the Brute pushes the 160 kg block onto pad E.
//  P2: the Sprinter starts the valve timer and TURBO-runs through V2 + V3 -> ARMOR OPEN -> one heart hit.
//  P3: the two inactive interns climb the review platform, SHIFT to one of them, the third joins (220-260 kg
//      >= 210) -> overload -> defeated -> north door -> break-room annex -> exit.
//  Run: run_level_test.sh <proj> 6 1,2,3,MIN,MAX   ·   fallback: run_level_test.sh <proj> 6 1 -l6Fallback 1
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L6Solution
    {
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, Options(), "L6");

        /// <summary>Iteration entry: builds the level at runtime (no scene) and runs seed 1 (or -botSeeds).</summary>
        public static IEnumerator RunRuntime(DevPlayContext ctx) => TeamBot.RunRuntimeLevel(ctx, 5, Solve, "L6rt", BotArgs.Seeds(1), Options());

        /// <summary>Boss extra (CAMPAIGN 22): Restart Checkpoint at each phase checkpoint (C2 / C3 / C4, a real scene reload via
        /// GameSession.RestartFromCheckpoint) -> the boss resumes in that phase -> the rest of the solution passes. Seed = session
        /// team. Run on the built scene: devplay.sh <proj> l6_restore -devScene Assets/Shifters/Scenes/L6_PerformanceReview.unity
        /// -devSetup Shifters.L6Solution.RunRestore -devSeconds 300</summary>
        public static IEnumerator RunRestore(DevPlayContext ctx)
        {
            for (int p = 0; p < 3; p++)
            {
                int phase = p;
                // p = 0: S0 -> P1 starts. p = 1: (restored P1) -> P2 starts. p = 2: (restored P2) -> P3 starts.
                yield return TeamBot.Run(ctx, b => Stages(b, phase == 0 ? 0 : phase - 1, phase, phase > 0), Options(), "L6restore" + (p + 1));
                if (GameSession.Instance == null) { ctx.Check(false, "no GameSession"); yield break; }
                var before = LevelMark.Find<L6DownsizerBoss>("boss");
                ctx.Check(before != null && before.Phase == phase && before.IsFighting, "reached phase " + (phase + 1) + " before the restart");
                GameSession.Instance.RestartFromCheckpoint();
                yield return ctx.Wait(1.5f);
                L6DownsizerBoss boss = null; float t = 0f;
                while (t < 20f) { boss = LevelMark.Find<L6DownsizerBoss>("boss"); if (boss != null && boss != before && boss.IsFighting) break; t += Time.deltaTime; yield return null; }
                int ck = CheckpointManager.Instance != null ? CheckpointManager.Instance.CurrentIndex : -1;
                ctx.Check(boss != null && boss.Phase == phase && boss.IsFighting, "Restart Checkpoint (C" + ck + ") resumes PHASE " + (phase + 1) + " (got " + (boss != null ? boss.Phase + 1 : -1) + ", " + (boss != null ? boss.State.ToString() : "none") + ")");
                ctx.Check(ck == 2 + phase, "checkpoint index after restart = " + ck + " (expected " + (2 + phase) + ")");
            }
            yield return TeamBot.Run(ctx, b => Stages(b, 2, 3, true), Options(), "L6restore4");
        }

        public static BotOptions Options() => new BotOptions { StrictPlayerThrows = true };

        public static bool Fallback
        {
            get
            {
                var a = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-l6Fallback") return a[i + 1] == "1";
                return false;
            }
        }

        static bool _cutscene;
        static void OnCutscene(bool on) => _cutscene = on;

        public static IEnumerator Solve(TeamBot bot) => Stages(bot, 0, 3, false);

        /// <summary>Stage 0 = S0 + P1, 1 = P2, 2 = P3 + exit. Runs stages from..to-1; when to &lt; 3 it stops as soon as the
        /// boss fights phase `to` (used by the checkpoint-restore scenario).</summary>
        public static IEnumerator Stages(TeamBot bot, int from, int to, bool restored)
        {
            GameEvents.CutsceneChanged -= OnCutscene; GameEvents.CutsceneChanged += OnCutscene; _cutscene = false;
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac; var gun = bot.GunOwner;
            var boss = LevelMark.Find<L6DownsizerBoss>("boss");
            if (boss == null) { bot.Fail("L6: boss mark missing"); yield break; }
            bool fallback = Fallback;
            bool shots = TeamBot.CurrentTeamKey == "1" && bot.Ctx != null;
            bot.Log("L6 team: " + bot.TeamSummary() + " gun=" + (gun != null ? gun.Archetype.ToString() : "none") + (fallback ? " FALLBACK (no tool)" : ""));
            if (gun == null) { bot.Fail("L6: no gun owner"); yield break; }
            // the bait: ACTIVE, parked in a break area while the others work (bosses only see who you control)
            var bait1 = brain != gun ? brain : spr;                   // P1: neither the carrier (Brute) nor the shooter
            var bait2 = brain != gun ? brain : brute;                 // P2: neither the runner (Sprinter) nor the shooter
            if (shots) BotDiagnostics.FocusShot(bot.Ctx, "l6_overview", new Vector3(0f, 0f, 24f), Vector3.forward, 24f, 24f);

            if (restored)
            {
                // after Restart Checkpoint the team stands in the phase's break area; the restored P1 uses the tool-less
                // block route (whether the tool survives a checkpoint reload is the weapons module's business)
                fallback = true;
                yield return bot.WaitUntil(() => boss.Phase == from && boss.IsFighting, 20f, "restored boss fights phase " + (from + 1));
                if (bot.Failed) yield break;
                yield return bot.Switch(from == 0 ? bait1 : bait2);
            }
            if (from <= 0)
            {
            if (!restored)
            {
            // ---------------- S0
            if (!fallback) yield return bot.Interact(gun, "s0.locker", "s0.locker.stand");
            if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.1");
            if (shots) bot.Snapshot("l6_s0_waiting_room");
            yield return bot.Switch(bait1);
            yield return bot.Parallel(Enter(bot, brute, bait1, gun, fallback), Enter(bot, spr, bait1, gun, fallback), Enter(bot, brain, bait1, gun, fallback));
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.State != BossState.Dormant, 60f, "the Downsizer wakes up");
            yield return bot.WaitUntil(() => boss.IsFighting, 15f, "phase 1 starts");
            if (bot.Failed) yield break;
            }
            if (to <= 0) yield break;

            // ---------------- P1 JAM THE PRESS
            for (int attempt = 0; attempt < 4 && InPhase(boss, 0) && !bot.Failed; attempt++)
            {
                if (fallback) yield return P1Block(bot, boss, brute);
                else yield return P1Crate(bot, boss, brute, gun);
                if (bot.Failed) yield break;
                yield return bot.WalkTo(gun, "s1.shoot");
                yield return bot.WaitUntil(() => boss.JammedPad >= 0 || boss.Phase > 0, 30f, "press jammed");
                if (bot.Failed) yield break;
                if (shots && attempt == 0) bot.Snapshot("l6_p1_jammed");
                yield return bot.ShootAt(gun, "boss.weak.heart", () => boss.Phase > 0 || !boss.IsVulnerable, 8, 9f);
            }
            yield return bot.ExpectBossPhase(boss, 1, 12f);
            if (bot.Failed) yield break;
            }
            if (to <= 1) { yield return bot.WaitUntil(() => boss.Phase == 1 && boss.IsFighting, 12f, "phase 2 fighting"); yield break; }
            if (from <= 1)
            {

            // ---------------- P2 DOWNSIZING PROTOCOL (valve run)
            yield return bot.Switch(bait2);
            if (bait2 != brute && brute != gun && brute.Feet.z > 16f) yield return bot.WalkTo(brute, Park(brute));
            if (gun.Feet.z > 30f || gun.Feet.x < -9f) yield return bot.WalkTo(gun, "s1.shoot");
            yield return bot.WaitUntil(() => boss.Phase == 1 && boss.IsFighting, 12f, "phase 2 fighting");
            for (int attempt = 0; attempt < 3 && InPhase(boss, 1) && !bot.Failed; attempt++)
            {
                if (bait2 != spr && boss.Arena != null && !boss.Arena.InSafeZone(bait2.Feet)) yield return bot.WalkTo(bait2, "boss.safe.1");
                // never sprint through the parked bait (a 55 kg Sprinter cannot shove a 115 kg Brute out of the lane)
                if (bait2 != spr && Vector3.Distance(spr.Feet, bait2.Feet) < 3f)
                    yield return bot.WalkTo(spr, spr.Feet + new Vector3(bait2.Feet.x > spr.Feet.x ? -1.8f : 1.8f, 0f, 2f));
                if (spr.Feet.z < 17f || (spr.Feet.x > -8f && spr.Feet.z < 24f)) yield return bot.WalkTo(spr, "s2.approach.s", true);
                yield return bot.WalkPath(spr, true, "s2.approach", "s2.v1.stand");
                yield return bot.Interact(spr, "s2.v1", "s2.v1.stand");
                yield return bot.WalkPath(spr, true, "s2.corner", "s2.v2", "s2.v3");
                if (bot.Failed) yield break;
                yield return bot.WaitUntil(() => boss.IsVulnerable || boss.Phase != 1, 2.5f, "armor open", false);
                if (!boss.IsVulnerable) { bot.Warn("L6: valve run missed (attempt " + (attempt + 1) + ")"); continue; }
                if (shots && attempt == 0) bot.Snapshot("l6_p2_armor_open");
                if (CharacterManager.Instance != null && CharacterManager.Instance.Active != bait2) yield return bot.Switch(bait2);
                yield return bot.ShootAt(gun, "boss.weak.heart", () => boss.Phase > 1 || !boss.IsVulnerable, 8, 9f);
            }
            yield return bot.ExpectBossPhase(boss, 2, 12f);
            if (bot.Failed) yield break;
            }
            if (to <= 2) { yield return bot.WaitUntil(() => boss.Phase == 2 && boss.IsFighting, 12f, "phase 3 fighting"); yield break; }

            // ---------------- P3 OVERLOAD (all three on the review scale)
            yield return bot.WaitUntil(() => boss.StampIsPlatform, 25f, "review platform down");
            if (bot.Failed) yield break;
            var active = CharacterManager.Instance != null ? CharacterManager.Instance.Active : bait2;
            ShifterCharacter first = null, second = null;
            foreach (var c in new[] { brute, spr, brain }) { if (c == active) continue; if (first == null) first = c; else second = c; }
            yield return bot.Parallel(Lane(bot, first, "a"), Lane(bot, second, "b"));
            if (bot.Failed) yield break;
            yield return bot.Expect(() => boss.OnStamp(first) && boss.OnStamp(second), "two interns on the review platform", 4f);
            yield return bot.Switch(first);                       // the active intern now stands above the shockwaves
            yield return Lane(bot, active, "c");
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.Overloading || boss.IsDefeated, 6f, "overload");
            if (shots) bot.Snapshot("l6_p3_overload");
            yield return bot.ExpectBossDefeated(boss, 10f);
            if (bot.Failed) yield break;
            yield return bot.Wait(2.2f);
            if (shots) BotDiagnostics.FocusShot(bot.Ctx, "l6_defeated", new Vector3(4f, 1.5f, 26f), Vector3.forward, 16f, 9f);

            // ---------------- S4 annex + exit
            yield return bot.Parallel(ToAnnex(bot, brute, -1.2f), ToAnnex(bot, spr, 0f), ToAnnex(bot, brain, 1.2f));
            yield return bot.GatherAt("s4.annex");
            yield return bot.WaitUntil(() => !_cutscene, 60f, "C04 finished");
            if (shots) bot.Snapshot("l6_annex");
            yield return bot.EnterExit();
            yield return bot.ExpectLevelComplete(15f);
            GameEvents.CutsceneChanged -= OnCutscene;
        }

        /// <summary>Into the arena (crossing z 11.2 wakes the boss): bait to break area SW, the tool holder to its spot.</summary>
        static IEnumerator Enter(TeamBot bot, ShifterCharacter c, ShifterCharacter bait, ShifterCharacter gun, bool fallback)
        {
            BotTarget to = c == gun && c != bait ? (BotTarget)(fallback ? "s1.shoot" : "s1.crate.shoot") : c == bait ? (BotTarget)"boss.safe.1" : Park(c);
            if (c != bait) yield return bot.Wait(c == gun ? 0.5f : 1.0f);
            yield return bot.WalkPath(c, false, "s1.enter", to);
        }

        /// <summary>Lane to the review platform: around the pads, over the step stool, onto spot a/b/c.</summary>
        /// <summary>Distinct parking spots in break area SW (never the bait's centre spot, never on the Sprinter's lane).</summary>
        static Vector3 Park(ShifterCharacter c) => LevelMark.Position("boss.safe.1") + (c != null && c.Archetype == Archetype.Sprinter ? new Vector3(-2.1f, 0f, -1.1f) : new Vector3(2.1f, 0f, -1.1f));

        static bool InPhase(BossBase b, int p) => b.Phase == p && b.State != BossState.PhaseTransition && !b.IsDefeated;

        static IEnumerator Lane(TeamBot bot, ShifterCharacter c, string spot)
        {
            if (c.Feet.x > 4f) yield return bot.WalkTo(c, "s3.lane.e");
            else if (c.Feet.x < -4f) yield return bot.WalkTo(c, "s3.lane.w");
            yield return bot.WalkTo(c, "s3.approach." + spot);
            yield return bot.WalkTo(c, "s3.plate." + spot, false, 0.5f, 8f);
        }

        /// <summary>Past the toppled press (west side) through the north door into the annex.</summary>
        static IEnumerator ToAnnex(TeamBot bot, ShifterCharacter c, float dx)
        {
            yield return bot.Wait(dx < 0f ? 0f : dx > 0f ? 1.0f : 0.5f);
            yield return bot.WalkPath(c, false, LevelMark.Position("s4.path.1") + new Vector3(dx, 0f, 0f), LevelMark.Position("s4.path.2") + new Vector3(dx, 0f, 0f),
                LevelMark.Position("s4.door") + new Vector3(dx * 0.8f, 0f, 0f), LevelMark.Position("s4.annex") + new Vector3(dx, 0f, 0f));
        }

        static IEnumerator P1Crate(TeamBot bot, L6DownsizerBoss boss, ShifterCharacter brute, ShifterCharacter gun)
        {
            var crate = LevelMark.Find<Shiftable>("s1.crateA");
            if (crate == null) { bot.Fail("L6: crate A is not Shiftable"); yield break; }
            yield return bot.WaitUntil(() => crate.Body != null && crate.Body.linearVelocity.sqrMagnitude < 0.05f, 5f, "crate settled", false);
            if (crate.SizeClass <= 0)
            {
                yield return bot.WalkTo(gun, "s1.crate.shoot");
                yield return bot.ShootAt(gun, "s1.crateA", () => crate.SizeClass > 0, 6, 12f);
            }
            if (crate.SizeClass <= 0) { bot.Fail("L6: BIT-SHIFTER did not make crate A HUGE"); yield break; }
            yield return bot.Grab(brute, "s1.crateA");
            yield return bot.WalkTo(brute, "s1.padW.drop");
            yield return bot.Face(brute, Vector3.right);
            yield return bot.Drop(brute);
            yield return bot.Wait(0.4f);
            yield return bot.WalkTo(brute, Park(brute), true);
        }

        static IEnumerator P1Block(TeamBot bot, L6DownsizerBoss boss, ShifterCharacter brute)
        {
            float top;
            if (boss.PropsOnPad(1, out top) >= L6DownsizerBoss.JamKg) yield break;
            yield return bot.WalkPath(brute, false, "s1.lane.e", "s1.block.behind");
            yield return bot.PushBlock(brute, "s1.block", "s1.padE.center", 0.4f, 60f);
            yield return bot.WalkTo(brute, "boss.safe.2", true);
        }
    }
}
