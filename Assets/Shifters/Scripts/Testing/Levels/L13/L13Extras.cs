// =====================================================================================
//  SHIFTERS - L13Extras.cs   [LEVEL 13 - HOSTILE TAKEOVER]  boss extras (CAMPAIGN 22 "Bosses additionally").
//  RunRestore: real Restart Checkpoint (scene reload) after the first punch (C1) and at C2 / C3 / C4 / C5, each
//              followed by the rest of the solution; P4 uses the Sprinter TAG capture route (Run B).
//  RunExtras : 120 s idle in break areas (40 s each in P1/P2/P3 via RestorePhase) + the attack-timeline hash
//              (patterns re-aimed at boss.hashTarget, active intern on the W balcony) for seeds 1,2,3,MIN,MAX.
//  RunTools  : Run C (BIT-SHIFTER, seeds 1, 3, MAX): armory pick in S0, P1-P3 as Run A (antenna by the tool),
//              P4 TINY: the tool shrinks the pod near s4.tinyWait, the Sprinter carries it into DETENTION.
//  devplay.sh <proj> l13_tools   -devScene Assets/Shifters/Scenes/L13_HostileTakeover.unity -devSetup Shifters.L13Extras.RunTools   -devSeconds 600
//  devplay.sh <proj> l13_restore -devScene Assets/Shifters/Scenes/L13_HostileTakeover.unity -devSetup Shifters.L13Extras.RunRestore -devSeconds 400
//  devplay.sh <proj> l13_extras  -devScene Assets/Shifters/Scenes/L13_HostileTakeover.unity -devSetup Shifters.L13Extras.RunExtras  -devSeconds 600
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L13Extras
    {
        static L13OneManBandBoss Boss => Object.FindAnyObjectByType<L13OneManBandBoss>();
        static int CI => CheckpointManager.Instance != null ? CheckpointManager.Instance.CurrentIndex : -1;

        static IEnumerator Restart(DevPlayContext ctx, L13OneManBandBoss before)
        {
            GameSession.Instance.RestartFromCheckpoint();
            yield return ctx.Wait(1.5f);
            float t = 0f;
            while (t < 20f) { var b = Boss; if (b != null && b != before && (b.IsFighting || b.IsDefeated || (CI <= 1 && b.State == BossState.Dormant))) break; t += Time.deltaTime; yield return null; }
            yield return ctx.Wait(0.5f);
        }

        public static IEnumerator RunRestore(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            var opts = L13Solution.Options();
            // C1: one punch, restart -> P1 again with 0/3
            yield return TeamBot.Run(ctx, b => L13Solution.Stages(b, 0, 1, false, 1), opts, "L13restoreC1");
            var before = Boss;
            ctx.Check(before != null && before.Phase == 0 && before.Hits >= 1, "C1: one punch landed before the restart");
            yield return Restart(ctx, before);
            var boss = Boss;
            ctx.Check(boss != null && boss != before && boss.Phase == 0 && boss.Hits == 0 && CI == 1, "C1 restore: P1 again, 0/3 (CI " + CI + ")");
            yield return TeamBot.Run(ctx, b => L13Solution.Stages(b, 0, 1), opts, "L13restoreP1");
            // C2 / C3 / C4
            for (int p = 1; p <= 3; p++)
            {
                // the previous stage ends in the phase transition: wait until phase p fights and C(p+1) is active
                for (float w = 0f; w < 12f; w += Time.deltaTime)
                {
                    var bb = Boss; if (bb != null && bb.Phase == p && bb.IsFighting && CI == p + 1) break;
                    yield return null;
                }
                yield return ctx.Wait(1f);
                before = Boss;
                ctx.Check(before != null && before.Phase == p && before.IsFighting, "reached phase " + (p + 1) + " before the restart");
                yield return Restart(ctx, before);
                boss = Boss;
                ctx.Check(boss != null && boss != before && boss.Phase == p && boss.IsFighting && CI == p + 1, "Restart Checkpoint C" + CI + " resumes PHASE " + (p + 1) + " (got " + (boss != null ? (boss.Phase + 1) + " " + boss.State : "none") + ")");
                if (boss == null) yield break;
                if (p == 1) ctx.Check(!boss.BumpsUp && boss.Hits == 0, "C2 restore: bumps down, 0 hits");
                if (p == 2) ctx.Check(boss.ShieldW && boss.ShieldE && !boss.AntennaLive && boss.TerminalW != null && !boss.TerminalW.IsOn && !boss.TerminalE.IsOn, "C3 restore: both shields up, terminals unlatched, antenna dark");
                if (p == 3)
                {
                    yield return ctx.Wait(2.5f);
                    ctx.Check(boss.Pod != null && boss.Pod.enabled && !boss.Detained && boss.Detention != null && !boss.Detention.DoorOpen, "C4 restore: pod launched on the rail, door shut, not detained");
                }
                yield return TeamBot.Run(ctx, b => L13Solution.Stages(b, p + 1, p == 3 ? 4 : p + 1, p == 3), opts, "L13restoreP" + (p + 1));
            }
            // C5
            before = Boss;
            ctx.Check(before != null && before.IsDefeated && CI == 5, "defeated + C5 before the last restart");
            yield return Restart(ctx, before);
            boss = Boss;
            var north = LevelMark.Find<Door>("s5.northDoor");
            ctx.Check(boss != null && boss.IsDefeated && boss.IsOn && boss.Detained && CI == 5, "C5 restore: boss defeated, pod DETAINED under the dome, north door powered");
            if (boss != null && boss.Pod != null) ctx.Check(Vector3.Distance(boss.Pod.transform.position, L13Builder.CellSeat) < 0.8f, "C5 restore: pod on the DETENTION seat");
            yield return TeamBot.Run(ctx, b => L13Solution.Stages(b, 5, 5), opts, "L13restoreExit");
        }

        // ------------------------------------------------------------------ idle + timeline hash
        static readonly List<string> s_hashes = new List<string>();

        public static IEnumerator RunExtras(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            yield return TeamBot.Run(ctx, Idle, L13Solution.Options(), "L13idle");
            s_hashes.Clear();
            yield return TeamBot.RunSeeds(ctx, new[] { 1, 2, 3, TeamBot.SeedMin, TeamBot.SeedMax }, Hash, null, L13Solution.Options(), "L13hash");
            for (int p = 0; p < 3; p++)
            {
                string first = null; bool same = true; var sb = new System.Text.StringBuilder();
                foreach (var h in s_hashes)
                {
                    if (!h.StartsWith("P" + p + "=")) continue;
                    sb.Append(h).Append(' ');
                    if (first == null) first = h; else if (h != first) same = false;
                }
                ctx.Check(first != null && same, "timeline hash P" + (p + 1) + " identical across seeds: " + sb);
            }
        }

        /// <summary>120 s: Sprinter active in break area SW, Brute in N, Brainiac in SE; 40 s each in P1, P2, P3.</summary>
        static IEnumerator Idle(TeamBot bot)
        {
            var boss = Boss; var spr = bot.Sprinter; var brute = bot.Brute; var brain = bot.Brainiac;
            var portalW = new Vector3(-4f, 0f, 10.5f); var portalE = new Vector3(4f, 0f, 10.5f);
            yield return bot.Switch(spr);
            yield return bot.Parallel(bot.WalkPath(spr, false, portalW, "boss.safe.1"), bot.WalkPath(brute, false, portalE, new Vector3(9f, 0f, 46f), "boss.safe.5"), bot.WalkPath(brain, false, portalE, "boss.safe.2"));
            if (bot.Failed) yield break;
            if (boss.State == BossState.Dormant) boss.Begin();
            yield return bot.WaitBoss(boss, BossState.Fighting, 10f);
            int laps0 = 0;
            for (int p = 0; p < 3; p++)
            {
                if (p > 0) { boss.RestorePhase(p); yield return bot.WaitBoss(boss, BossState.Fighting, 6f); }
                int serial0 = boss.Attacks.Serial, holds0 = boss.Holds;
                float t0 = Time.time; laps0 = 0; Vector3 last = boss.transform.position; float travelled = 0f;
                while (Time.time - t0 < 40f)
                {
                    travelled += Vector3.Distance(last, boss.transform.position); last = boss.transform.position;
                    yield return null;
                }
                foreach (var c in bot.Team)
                    yield return bot.Expect(() => c.IsAlive && c.Condition == CharacterCondition.Normal && c.State == CharacterState.Alive, c.DisplayName + " unharmed after 40 s idle in P" + (p + 1), 0f);
                if (p == 1) { laps0 = Mathf.FloorToInt(travelled / 65.97f); yield return bot.Expect(() => laps0 >= 5, "P2 skates >= 5 laps while everyone is on break (" + laps0 + ")", 0f); }
                else yield return bot.Expect(() => boss.Attacks.Serial == serial0, "P" + (p + 1) + ": no tell started while the team is on break (HOLD, " + (boss.Holds - holds0) + " holds)", 0f);
                if (bot.Failed) yield break;
            }
            foreach (var c in bot.Team) yield return bot.Expect(() => c.Health >= c.MaxHealth - 0.01f, c.DisplayName + " health full after 120 s", 0f);
        }

        /// <summary>Per seed: patterns re-aimed at boss.hashTarget (TargetActive off), the Brainiac active on the W balcony,
        /// RestorePhase(p) then 20 s -> Attacks.TimelineHash.</summary>
        static IEnumerator Hash(TeamBot bot)
        {
            var boss = Boss; var brain = bot.Brainiac;
            for (int p = 0; p < 3; p++)
            {
                var pat = boss.Phases[p].Pattern;
                for (int i = 0; i < pat.Length; i++) { pat[i].TargetActive = false; if (p != 1) pat[i].TargetMark = "boss.hashTarget"; }
            }
            yield return bot.Switch(brain);
            yield return bot.WalkPath(brain, false, new Vector3(-4f, 0f, 10.5f), "s3.stairsW.foot", "s3.stairsW.top", "s3.balconyW.stand");
            if (bot.Failed) yield break;
            if (boss.State == BossState.Dormant) boss.Begin();
            yield return bot.WaitBoss(boss, BossState.Fighting, 10f);
            for (int p = 0; p < 3; p++)
            {
                boss.RestorePhase(p);
                yield return bot.WaitBoss(boss, BossState.Fighting, 6f);
                boss.Attacks.ResetHash();
                yield return bot.Wait(20f);
                s_hashes.Add("P" + p + "=" + boss.Attacks.TimelineHash.ToString("X8"));
                bot.Log("HASH P" + (p + 1) + " team " + TeamBot.CurrentTeamKey + " = " + boss.Attacks.TimelineHash.ToString("X8") + " (" + boss.Attacks.Serial + " attacks)");
            }
        }
    
        // ------------------------------------------------------------------ Run C: BIT-SHIFTER tool path
        public static IEnumerator RunTools(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            yield return TeamBot.RunSeeds(ctx, new[] { 1, 3, TeamBot.SeedMax }, Tools, null, L13Solution.Options(), "L13tools");
        }

        static IEnumerator Tools(TeamBot bot)
        {
            var boss = Boss; var gun = bot.GunOwner; var spr = bot.Sprinter; var brain = bot.Brainiac; var brute = bot.Brute;
            if (gun == null) { bot.Fail("no GunOwner"); yield break; }
            yield return bot.Switch(gun);
            yield return bot.Interact(gun, "s0.armory.bitshifter");
            if (bot.Failed) yield break;
            var armory = Object.FindAnyObjectByType<WeaponArmory>();
            yield return bot.Expect(() => armory != null && armory.Current != null && armory.Current.Tool == ExperimentalVariant.BitShifter, "armory: BIT-SHIFTER picked by " + gun.DisplayName, 2f);
            if (bot.Failed) yield break;
            yield return L13Solution.Stages(bot, 0, 3);
            if (bot.Failed) yield break;
            // P4 TINY
            yield return bot.ExpectBossPhase(boss, 3, 8f);
            if (bot.Failed) yield break;
            var pod = boss.Pod; var shrink = pod.GetComponent<L13PodShrink>();
            bool brainUp = Vector3.Distance(brain.Feet, new Vector3(17.5f, 2.5f, 28f)) < 3f;
            var brainGo = brainUp ? bot.WalkPath(brain, false, "s3.stairsE.top", "s3.stairsE.foot", "s3.cross.1", "s3.east.1", "s4.intake.stand")
                                  : bot.WalkPath(brain, false, "s5.path.1", "s4.intake.stand");
            var sprGo = bot.WalkTo(spr, new Vector3(10.5f, 0f, 38f), true);   // off the rail (2.8 m), so the pod does not panic-reverse
            var gunGo = gun != spr && gun != brain ? bot.WalkTo(gun, new Vector3(4.5f, 0f, 38.5f), true) : bot.Wait(0.1f);
            yield return bot.Parallel(brainGo, sprGo, gunGo);
            if (bot.Failed) yield break;
            yield return bot.Switch(gun);
            for (int tries = 0; tries < 3 && !boss.Detained; tries++)
            {
                yield return bot.WaitUntil(() => pod.enabled && !pod.IsCarried && !pod.IsDizzy && Vector3.Distance(gun.Feet, pod.transform.position) <= 18f, 25f, "pod within 18 m of the tool");
                if (bot.Failed) { bot.Log("pod at " + pod.transform.position + " carried " + pod.IsCarried + " dizzy " + pod.IsDizzy); yield break; }
                if (bot.Failed) yield break;
                yield return bot.ShootAt(gun, pod, () => shrink.IsTiny, 4, 4f);
                yield return bot.Expect(() => shrink.IsTiny, "BIT-SHIFTER: the SOLO POD is TINY", 0.5f);
                if (bot.Failed) yield break;
                Shot(bot, "l13_tool_tiny");
                if (Vector3.Distance(spr.Feet, pod.transform.position) > 2f) yield return bot.WalkTo(spr, pod, true, 1.6f, 4f);
                yield return bot.Grab(spr, pod, 3f);
                if (bot.Failed) yield break;
                yield return bot.Parallel(bot.WalkTo(spr, "s4.cellFront", true, 0.6f, 6f), bot.Hack(brain, "s4.intake", (BotTarget)"s4.intake.stand"));
                yield return bot.WaitUntil(() => boss.Detention.DoorOpen, 3f, "DETENTION door open", false);
                yield return bot.WalkTo(spr, "s4.cell.drop", false, 0.5f, 4f);
                yield return bot.Drop(spr);
                yield return bot.WalkTo(spr, "s4.cellFront", false, 0.6f, 4f);
                yield return bot.WaitUntil(() => boss.Detained, 4f, "DETAINED (tool path)", false);
                if (!boss.Detained) { bot.Log("TINY try " + (tries + 1) + " missed; retrying"); yield return bot.WalkTo(spr, new Vector3(10.5f, 0f, 38f), true); }
            }
            yield return bot.ExpectPowered("s4.detained", true, 1f);
            yield return bot.ExpectBossDefeated(boss, 5f);
            if (bot.Failed) yield break;
            yield return L13Solution.Stages(bot, 5, 5);
        }

        static void Shot(TeamBot bot, string name) { if (TeamBot.CurrentTeamKey == "1" && bot.Ctx != null) bot.Ctx.Shot(name); }
    }
}
