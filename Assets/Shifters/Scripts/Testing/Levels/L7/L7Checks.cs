// =====================================================================================
//  SHIFTERS - L7Checks.cs   [SHIFT 04 - ACT NATURAL]  DevPlay checks + screenshots on the BUILT scene.
//  devplay.sh <proj> l7_checks -devScene Assets/Shifters/Scenes/L7_ActNatural.unity
//             -devSetup Shifters.L7Checks.Run -devSeconds 60 -devSize 1600x900
//  Static: every mark/beat the solution uses resolves, budgets, sign audit, materials. Geometry: the statue's
//  blind spot really hides the stack spot / gate approach from T1 (Physics LOS), T1 does see the open
//  counter, spawn and waits lie outside Q0/T2 range. Live: a deliberate pair in Q1's cone gets detected and
//  escorted. Screenshots: street, lobby, detection, statue + IQ blind spot, teller area, corridor/exit.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L7Checks
    {
        static readonly string[] Marks = {
            "spawn", "checkpoint.1", "checkpoint.2", "exit", "story.metv", "story.director",
            "s0.Q0", "s0.alarm", "s0.alarm.escort", "s0.return.0", "s1.entry.0", "s1.entry.1", "s1.entry.2",
            "s1.P1", "s1.L1", "s1.G1", "s1.Q1", "s1.Q2", "s1.alarm", "s1.alarm.escort", "s1.gate", "s1.approach",
            "s1.beyond", "s1.beyond2", "s1.beyond3", "s1.plateRoute", "s1.togetherA", "s1.togetherB", "s1.return.2",
            "s2.T1", "s2.T2", "s2.alarm", "s2.alarmB", "s2.statue", "s2.blindSpot", "s2.tellerGate", "s2.gateBtn",
            "s2.printer", "s2.staffDoor", "s2.doorZone", "s2.hallIn", "s2.westRun", "s2.stackSpot", "s2.stackRun",
            "s2.gateWait", "s2.counterTop", "s2.tellerDrop", "s2.gateOut", "s2.gateIn", "s2.inside.a",
            "s2.wait.0", "s2.wait.1", "s2.wait.2", "s3.corridor.0", "s3.corridor.1", "s3.corridor.2",
            "tutorial.ch04_actnatural", "tutorial.ch04_onedoor", "hint.1", "hint.2", "hint.3" };

        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(1.0f);
            TutorialManager.Instance?.Clear();
            var team = new List<ShifterCharacter>(ShifterCharacter.All);
            ctx.Check(team.Count == 3, "team of 3 spawned");

            // ---------------- static
            foreach (var m in Marks) ctx.Check(LevelMark.Exists(m), "mark " + m);
            L7Director.DefineLines();
            foreach (var id in L7Solution.BeatIds) ctx.Check(StoryScripts.Exists(id), "story script exists: " + id);
            ctx.Check(StoryScripts.Exists("C05") && StoryScripts.Exists("react.CH04"), "C05 + react.CH04 exist");
            var budget = SceneAudit.MeasureBudgets();
            ctx.Log("budget: " + budget.Line());
            ctx.Check(budget.Ok, "SceneAudit budgets (no hard-cap breach)");
            ctx.Check(budget.Renderers <= 1600, "renderers <= 1600 target (" + budget.Renderers + ")");
            var signs = SceneAudit.SignProblems();
            foreach (var i in signs.Issues) ctx.Log("SIGN " + i.Kind + ": " + i.What);
            foreach (var w in signs.Warnings) ctx.Log("SIGN warn " + w.Kind + ": " + w.What);
            ctx.Check(signs.Issues.Count == 0, "sign audit clean (" + signs.Issues.Count + " issues, " + signs.Warnings.Count + " warnings)");
            var mats = SceneAudit.MaterialProblems();
            foreach (var m in mats) ctx.Log("MAT " + m);
            ctx.Check(mats.Count == 0, "no default/magenta materials");
            int cps = 0; foreach (var cp in Checkpoint.All) if (cp != null) cps++;
            ctx.Check(cps == 2, "2 checkpoints");

            // ---------------- geometry (Physics LOS)
            var t1 = LevelMark.Find<SecurityCamera>("s2.T1");
            var t2 = LevelMark.Find<SecurityCamera>("s2.T2");
            var q0 = LevelMark.Find<SecurityCamera>("s0.Q0");
            if (t1 != null)
            {
                foreach (var id in new[] { "s2.stackSpot", "s2.gateWait", "s2.gateOut", "s2.stackRun" })
                {
                    var p = LevelMark.Position(id);
                    bool c = t1.LineOfSight(p + Vector3.up * 0.9f), h = t1.LineOfSight(p + Vector3.up * 1.7f);
                    ctx.Check(!c && !h, "T1 blind at " + id + " (centre " + c + ", head " + h + ")");
                }
                var top = LevelMark.Position("s2.stackSpot") + Vector3.up * 3.3f;
                ctx.Check(!t1.LineOfSight(top), "T1 blind to the stacked top's head");
                var open = new Vector3(1f, 1.2f, 50.5f);
                ctx.Check(t1.LineOfSight(open), "T1 sees the open counter front (1, 1.2, 50.5)");
                float dOpen = Vector3.Distance(new Vector3(t1.transform.position.x, 0f, t1.transform.position.z), new Vector3(1f, 0f, 50.5f));
                ctx.Check(dOpen < t1.Range, "open counter within T1 range");
            }
            if (t2 != null)
                for (int i = 0; i < 3; i++)
                {
                    var p = LevelMark.Position("s2.wait." + i); p.y = 0f;
                    var c = t2.transform.position; c.y = 0f;
                    ctx.Check(Vector3.Distance(p, c) > t2.Range + 0.5f, "s2.wait." + i + " outside T2 range");
                }
            if (q0 != null)
            {
                var c = q0.transform.position; c.y = 0f;
                ctx.Check(Vector3.Distance(c, L7Builder.SpawnPos) > q0.Range + 1f, "spawn outside Q0 range");
            }

            // ---------------- shots (after the level banner)
            yield return ctx.Wait(4.5f);
            TutorialManager.Instance?.Clear();
            BotDiagnostics.ShotFrom(ctx, "l7_01_street", new Vector3(0f, 3.6f, -9.9f), new Vector3(0f, 3.6f, 4f));
            yield return ctx.Wait(0.2f);
            BotDiagnostics.ShotFrom(ctx, "l7_02_lobby", new Vector3(10f, 6.5f, 7f), new Vector3(-3f, 1f, 25f));
            yield return ctx.Wait(0.2f);

            // live: deliberate pair inside Q1's cone -> detected -> escorted back
            var esc = LevelMark.Find<EscortBack>("s1.alarm.escort");
            ShifterCharacter a = team.Count > 0 ? team[0] : null, b = team.Count > 1 ? team[1] : null;
            if (esc != null && a != null && b != null)
            {
                int before = esc.EscortCount;
                a.Motor.Teleport(LevelMark.Position("s1.togetherA"), Quaternion.identity);
                b.Motor.Teleport(LevelMark.Position("s1.togetherB"), Quaternion.identity);
                float t0 = Time.time; bool shot = false;
                while (Time.time - t0 < 12f && esc.EscortCount == before)
                {
                    if (!shot && esc.IsEscorting) { BotDiagnostics.ShotFrom(ctx, "l7_03_detected", new Vector3(4f, 6f, 9f), new Vector3(-4f, 1f, 21f)); shot = true; }
                    yield return null;
                }
                ctx.Check(esc.EscortCount > before, "pair in Q1's cone -> TEAMWORK DETECTED -> escorted");
                ctx.Check(a.transform.position.z < 9f && b.transform.position.z < 9f, "escorted to the S1 return marks (z " + a.transform.position.z.ToString("0.0") + ")");
                var dir = LevelMark.Find<L7Director>("story.director");
                ctx.Check(dir != null && dir.Detections == 1 && LevelCompleteExtras.StealthGrade == "CASUAL", "stealth grade counts it (CASUAL)");
            }
            yield return ctx.Wait(3.5f);

            // statue + IQ blind spot: Brainiac active, standing in the hall
            var brain = Find(team, Archetype.Brainiac);
            if (brain != null && CharacterManager.Instance != null)
            {
                brain.Motor.Teleport(new Vector3(-1f, 0f, 44f), Quaternion.LookRotation(new Vector3(-0.6f, 0f, 1f)));
                CharacterManager.Instance.SwitchTo(brain.Slot, true);
                yield return ctx.Wait(1.2f);
                PerspectiveManager.Instance?.Refresh(true);
                PerspectiveManager.Instance?.Pulse();
                yield return ctx.Wait(1.2f);
                var hatch = LevelMark.Find<Revealable>("s2.blindSpot");
                ctx.Check(hatch != null && hatch.IsRevealed, "IQ view reveals the BLIND SPOT hatching");
                BotDiagnostics.ShotFrom(ctx, "l7_04_statue_iq", new Vector3(4f, 7.5f, 41f), new Vector3(-8f, 1f, 51f));
                var main = Camera.main; if (main != null) ctx.Shot("l7_04b_gameplay_iq", main);
            }
            yield return ctx.Wait(0.3f);
            BotDiagnostics.ShotFrom(ctx, "l7_05_teller", new Vector3(-9f, 6.5f, 54.5f), new Vector3(4f, 1f, 63f));
            yield return ctx.Wait(0.2f);
            BotDiagnostics.ShotFrom(ctx, "l7_06_exit", new Vector3(0f, 3.4f, 65.2f), new Vector3(0f, 1.6f, 76f));
            yield return ctx.Wait(0.2f);
            BotDiagnostics.ShotFrom(ctx, "l7_07_counter", new Vector3(-1f, 4f, 43f), new Vector3(-6f, 3f, 52f));
            yield return ctx.Wait(0.5f);
        }

        static ShifterCharacter Find(List<ShifterCharacter> team, Archetype a)
        {
            foreach (var c in team) if (c != null && c.Archetype == a) return c;
            return null;
        }
    }
}
