// =====================================================================================
//  SHIFTERS - L6Checks.cs   [CH03 - boss extras + static checks + screenshots, DevPlay]
//  devplay.sh <proj> l6_checks -devSetup Shifters.L6Checks.Run -devSeconds 330
//  * Build report (0 warnings), marks + 4 checkpoints resolve, ValidateSpecs / ValidateSafeZones / ValidateNoKillZones,
//    SceneAudit budgets + sign audit.
//  * 120 s idle in break areas (phases 1 -> 2 -> 3 via RestorePhase, active intern switched every 17 s): nobody
//    Downed / Lost, health never below 100, max one attack active, telegraphs >= 1.5 / 1.2 s.
//  * Timeline hash: P1 idle 21 s, rebuild, again -> identical.
//  * Screenshots: S0, arena overview (P1 telegraph), P3 review platform, annex.
//  (Debug placement is allowed here - this is not the solution run.)
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L6Checks
    {
        static readonly string[] Marks = { "spawn", "checkpoint.1", "checkpoint.2", "checkpoint.3", "checkpoint.4", "exit", "s0.locker", "s0.locker.stand",
            "s1.enter", "s1.crateA", "s1.crateB", "s1.block", "s1.padN.center", "s1.padE.center", "s1.padS.center", "s1.padW.center", "s1.padW.drop",
            "s1.shoot", "s1.crate.shoot", "s1.lane.e", "s1.block.behind", "boss", "boss.weak.heart", "boss.safe.1", "boss.safe.2", "boss.safe.3", "boss.safe.4",
            "s2.v1", "s2.v1.stand", "s2.approach", "s2.corner", "s2.v2", "s2.v3", "s2.gate2", "s2.gate3", "s2.armor", "s3.scale", "s3.plate.a", "s3.plate.b",
            "s3.plate.c", "s3.approach.a", "s3.approach.b", "s3.approach.c", "s3.lane.e", "s3.lane.w", "s3.door", "s3.pipe", "s4.path.1", "s4.path.2",
            "s4.door", "s4.annex", "story.percy", "tutorial.ch03_tools", "tutorial.ch03_boss", "tutorial.ch03_bitshift", "hint.1", "hint.2", "hint.3" };

        public static IEnumerator Run(DevPlayContext ctx)
        {
            BossBase.ClearMercyMemory();
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(5);
            yield return ctx.Wait(2f);
            ctx.Check(lctx != null && !lctx.Failed, "L6 builds at runtime");
            if (lctx == null) yield break;
            ctx.Check(lctx.Warnings.Count == 0, "build warnings: " + lctx.Warnings.Count + (lctx.Warnings.Count > 0 ? " first: " + lctx.Warnings[0] : ""));
            ctx.Log(lctx.Report());
            int missing = 0;
            foreach (var m in Marks) if (!LevelMark.Exists(m)) { missing++; ctx.Log("MISSING MARK " + m); }
            ctx.Check(missing == 0, "all " + Marks.Length + " marks resolve (missing " + missing + ")");
            var boss = LevelMark.Find<L6DownsizerBoss>("boss");
            ctx.Check(boss != null && boss.Model != null && boss.Arena != null, "boss + model + arena wired");
            if (boss == null) yield break;
            string err;
            ctx.Check(boss.ValidateSpecs(out err), "ValidateSpecs " + err);
            ctx.Check(boss.Arena.ValidateSafeZones(out err), "ValidateSafeZones " + err);
            ctx.Check(boss.Arena.ValidateNoKillZones(out err), "ValidateNoKillZones " + err);
            ctx.Check(boss.Arena.Zones.Count == 4, "4 break areas (" + boss.Arena.Zones.Count + ")");
            ctx.Check(boss.Phases.Length == 3 && boss.Phases[0].CheckpointIndex == 2 && boss.Phases[1].CheckpointIndex == 3 && boss.Phases[2].CheckpointIndex == 4, "phase checkpoints 2/3/4");
            var budget = SceneAudit.MeasureBudgets();
            ctx.Log("BUDGET " + budget.Line());
            ctx.Check(budget.Ok, "SceneAudit budgets: " + string.Join("; ", budget.Breaches));
            ctx.Check(budget.Rigidbodies <= 25 && budget.Renderers <= 1600, "WebGL targets rb " + budget.Rigidbodies + " <= 25, renderers " + budget.Renderers + " <= 1600");
            var signs = SceneAudit.SignProblems();
            foreach (var i in signs.Issues) ctx.Log("SIGN " + i.Kind + " '" + i.Text + "' at " + i.Path + " " + i.Detail);
            foreach (var i in signs.Warnings) ctx.Log("SIGN WARN " + i.Kind + " '" + i.Text + "' at " + i.Path);
            ctx.Check(signs.Issues.Count == 0, "sign audit: " + signs.Issues.Count + " problems (" + signs.Judged + " judged)");

            // ---- screenshots of the static layout
            BotDiagnostics.FocusShot(ctx, "l6c_s0", new Vector3(0f, 1.2f, 2f), Vector3.forward, 9f, 3.5f);
            BotDiagnostics.FocusShot(ctx, "l6c_locker", new Vector3(5.6f, 1.3f, 0f), Vector3.right, 4.5f, 1.2f);
            BotDiagnostics.FocusShot(ctx, "l6c_annex", new Vector3(1f, 1.3f, 48f), Vector3.forward, 6.5f, 2.6f);

            // ---- idle survival 120 s in break areas, phases 1 -> 2 -> 3
            var team = new List<ShifterCharacter>(CharacterManager.Instance.Team);
            Park(team, boss);
            yield return ctx.Wait(0.5f);
            boss.Begin();
            float t = 0f, minHp = 999f, minWind0 = 99f, minWind = 99f; int maxActive = 0, downs = 0, nextSwitch = 1, telegraphs = 0, lastSerial = -1;
            bool shotP1 = false, shotP3 = false;
            while (t < 120f)
            {
                yield return null;
                t += Time.deltaTime;
                if (t > 40f && boss.Phase == 0 && boss.IsFighting) boss.RestorePhase(1);
                if (t > 80f && boss.Phase == 1 && boss.IsFighting) boss.RestorePhase(2);
                if (t > 17f * nextSwitch) { nextSwitch++; CharacterManager.Instance.SwitchToNextAvailable(); }
                foreach (var c in team) { minHp = Mathf.Min(minHp, c.Health); if (c.State != CharacterState.Alive) downs++; }
                maxActive = Mathf.Max(maxActive, boss.Attacks.Active.Count);
                var cur = boss.Attacks.Current;
                if (cur != null && cur.Serial != lastSerial)
                {
                    lastSerial = cur.Serial; telegraphs++;
                    if (cur.Phase == 0) minWind0 = Mathf.Min(minWind0, cur.WindupSeconds); else minWind = Mathf.Min(minWind, cur.WindupSeconds);
                }
                if (!shotP1 && boss.Phase == 0 && cur != null && cur.Stage == BossTelegraphStage.Windup && cur.Elapsed > 1.0f)
                { shotP1 = true; BotDiagnostics.FocusShot(ctx, "l6c_arena_p1_telegraph", new Vector3(0f, 0f, 25f), Vector3.forward, 21f, 17f); }
                if (!shotP3 && boss.Phase == 2 && boss.StampIsPlatform && boss.StateTime > 1f)
                { shotP3 = true; BotDiagnostics.FocusShot(ctx, "l6c_p3_review_platform", new Vector3(0f, 0.6f, 20f), Vector3.forward, 8f, 3.5f); }
            }
            ctx.Log("IDLE: telegraphs " + telegraphs + " minHp " + minHp + " maxActive " + maxActive + " minWindup P1 " + minWind0.ToString("0.00") + " later " + minWind.ToString("0.00") + " phase " + boss.Phase);
            ctx.Check(downs == 0, "120 s idle: nobody Downed/Lost (" + downs + " samples)");
            ctx.Check(minHp >= 100f, "120 s idle: health never below 100 (" + minHp + ")");
            ctx.Check(maxActive <= 1, "max one attack active (" + maxActive + ")");
            ctx.Check(telegraphs >= 20, "telegraphs seen " + telegraphs);
            ctx.Check(minWind0 >= 1.5f - 1e-3f && minWind >= 1.2f - 1e-3f, "telegraphs >= 1.5 / 1.2 s (" + minWind0 + " / " + minWind + ")");
            ctx.Check(boss.Phase == 2 && boss.StampIsPlatform, "P3 review platform reached (phase " + boss.Phase + ", platform " + boss.StampIsPlatform + ")");

            // ---- timeline hash (P1, 21 s) twice
            uint h1 = 0, h2 = 0;
            for (int run = 0; run < 2; run++)
            {
                BossBase.ClearMercyMemory();
                LevelRuntimeBuilder.BuildInCurrentScene(5);
                yield return ctx.Wait(2f);
                boss = LevelMark.Find<L6DownsizerBoss>("boss");
                team = new List<ShifterCharacter>(CharacterManager.Instance.Team);
                Park(team, boss);
                yield return ctx.Wait(0.5f);
                boss.Begin();
                yield return ctx.Wait(21f);
                if (run == 0) h1 = boss.Attacks.TimelineHash; else h2 = boss.Attacks.TimelineHash;
            }
            ctx.Log("TIMELINE HASH " + h1.ToString("X8") + " / " + h2.ToString("X8"));
            ctx.Check(h1 == h2 && h1 != 2166136261u, "attack timeline hash identical across rebuilds");
        }

        static void Park(List<ShifterCharacter> team, L6DownsizerBoss boss)
        {
            var spots = new[] { LevelMark.Position("boss.safe.1"), LevelMark.Position("boss.safe.2"), LevelMark.Position("boss.safe.4") };
            for (int i = 0; i < team.Count && i < spots.Length; i++)
            {
                var c = team[i];
                if (c == null || c.Body == null) continue;
                c.Body.position = spots[i] + Vector3.up * 0.05f;
                c.Body.linearVelocity = Vector3.zero;
                c.transform.position = spots[i] + Vector3.up * 0.05f;
            }
        }
    }
}
