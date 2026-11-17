// =====================================================================================
//  SHIFTERS - SmokeTests.cs   [BOTS module - smoke tests]
//  Shifters.SmokeTests.Run : DevPlay scenario for ANY scene (-devScene X). Waits 3 s, then checks
//  no errors/exceptions, LevelManager + spawned team (levels) / UI present (main menu), camera
//  renders, no missing/default/magenta materials, rigidbodies < 120, samples frame time
//  (batchmode timing is indicative only), takes a screenshot, and runs the generic SIGN AUDIT
//  (SmokeTests.Signs -> SceneAudit.SignProblems: no world text / sign / poster / light panel faces a wall,
//  is buried, upside down or mirrored; one "[SMOKE]   SIGN #n ..." line per issue with object path and
//  position, <shots>/sign_audit.txt, numbered close-ups <shots>/sign_issues_N.png). Sign issues fail the
//  shipping scenes (MainMenu + LevelCatalog levels); in dev scenes (L0_*) they are reported only.
//  All scenes in one Unity run: Shifters.EditorTools.BotBatch.SmokeAllScenes ($SP/tools/run_smoke.sh).
//  Shifters.SmokeTests.CodeBuiltCourse : same checks on the bots' code-built mini course
//  (proves the audit itself works while no scenes are built yet).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class SmokeTests
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            BotPrefsGuard.Push(ctx);
            BotStory.Apply(null);
            yield return RunCore(ctx);
            BotStory.Restore();
            BotPrefsGuard.Pop(ctx);
        }

        static IEnumerator RunCore(DevPlayContext ctx)
        {
            yield return ctx.Wait(3f);
            var scene = SceneManager.GetActiveScene();
            string sn = scene.name;
            bool isMenu = sn == LevelCatalog.MainMenuScene;
            var lm = BotReflect.FindFirst("LevelManager");
            bool isLevel = !isMenu && (LevelCatalog.IndexOfScene(sn) >= 0 || lm != null || sn.StartsWith("L0"));
            ctx.Log("[SMOKE] scene '" + sn + "' (" + scene.path + ") menu=" + isMenu + " level=" + isLevel);
            ctx.Check(ctx.Result.exceptions == 0, "[SMOKE] no exceptions while loading (" + ctx.Result.exceptions + ")");
            ctx.Check(ctx.Result.errors == 0, "[SMOKE] no errors while loading (" + ctx.Result.errors + ")");
            if (isLevel) CheckLevel(ctx, lm, sn);
            if (isMenu) CheckMenu(ctx);
            yield return CommonChecks(ctx, "smoke_" + sn);
            yield return Signs(ctx, sn, null, isMenu || LevelCatalog.IndexOfScene(sn) >= 0);
        }

        /// <summary>Generic sign audit on whatever is loaded (any scene, or after a runtime level build): every world-space
        /// text, EnvKit sign / poster / whiteboard / holo panel and light panel must show its readable / lit side to open
        /// space (nothing solid within SceneAudit.SignClearance = 0.4 m), not be buried in geometry, upside down, sideways or
        /// mirrored. Billboards / animated texts are found by moving the camera (SceneAudit.CollectMovingTexts) and skipped.
        /// Logs the summary and one "[SMOKE]   SIGN #n KIND piece 'text' at (x,y,z) facing (x,y,z) | path | why" line per
        /// issue (CLIPPED texts as "SIGN-WARN" lines), writes &lt;shots&gt;/sign_audit.txt and a numbered close-up sheet,
        /// and records ONE check - failing on issues unless failOnIssues is false (dev scenes: reported only).</summary>
        public static IEnumerator Signs(DevPlayContext ctx, string label = null, Transform onlyUnder = null, bool failOnIssues = true)
        {
            label = string.IsNullOrEmpty(label) ? SceneManager.GetActiveScene().name : label;
            var moving = new HashSet<Transform>();
            yield return SceneAudit.CollectMovingTexts(moving);
            var res = SceneAudit.SignProblems(moving, onlyUnder);
            ctx.Log("[SMOKE] sign audit " + label + ": " + res.Summary);
            for (int i = 0; i < res.Issues.Count; i++) ctx.Log("[SMOKE]   SIGN #" + i + " " + res.Issues[i]);
            for (int i = 0; i < res.Warnings.Count; i++) ctx.Log("[SMOKE]   SIGN-WARN #" + (res.Issues.Count + i) + " " + res.Warnings[i]);
            WriteSignReport(ctx, label, res);
            var first = new System.Text.StringBuilder();
            for (int i = 0; i < res.Issues.Count && i < 3; i++)
                first.Append(i == 0 ? " - " : " ; ").Append(res.Issues[i].Kind).Append(" '").Append(res.Issues[i].Text).Append("' @")
                     .Append(SceneAudit.Fmt(res.Issues[i].Position)).Append(' ').Append(res.Issues[i].Path);
            ctx.Check(res.Issues.Count == 0 || !failOnIssues, "[SMOKE] signs readable: no text / sign / poster / light panel faces a wall, is buried, upside down or mirrored ("
                                             + res.Issues.Count + " issue(s), " + res.Warnings.Count + " warning(s), " + res.Judged + " judged"
                                             + (failOnIssues ? "" : "; dev scene: reported, not failing") + ")" + first);
            if (res.Issues.Count + res.Warnings.Count > 0)
            {
                var all = new List<SceneAudit.SignIssue>(res.Issues);
                all.AddRange(res.Warnings);
                yield return SceneAudit.SignIssueSheet(ctx, all, "sign_issues");
            }
        }

        static void WriteSignReport(DevPlayContext ctx, string label, SceneAudit.SignAuditResult res)
        {
#if UNITY_EDITOR
            if (ctx?.Config == null || string.IsNullOrEmpty(ctx.Config.shotsDir)) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("SIGN AUDIT ").Append(label).Append('\n').Append(res.Summary).Append('\n');
                for (int i = 0; i < res.Issues.Count; i++) sb.Append('#').Append(i).Append(' ').Append(res.Issues[i]).Append('\n');
                for (int i = 0; i < res.Warnings.Count; i++) sb.Append("WARN #").Append(res.Issues.Count + i).Append(' ').Append(res.Warnings[i]).Append('\n');
                System.IO.Directory.CreateDirectory(ctx.Config.shotsDir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(ctx.Config.shotsDir, "sign_audit.txt"), sb.ToString());
            }
            catch (System.Exception e) { ctx.Log("[SMOKE] could not write sign_audit.txt: " + e.Message); }
#endif
        }

        /// <summary>Smoke checks on the bots' code-built course (no scene files needed).</summary>
        public static IEnumerator CodeBuiltCourse(DevPlayContext ctx)
        {
            TeamBot.ForceTeam(4242);
            var course = BotMiniCourse.Build(BotCapabilities.Probe());
            course.SpawnTeam();
            yield return ctx.Wait(3f);
            var cm = CharacterManager.Instance;
            ctx.Check(cm != null && cm.Team.Count == 3, "[SMOKE] course team spawned (" + (cm != null ? cm.Team.Count : 0) + ")");
            yield return CommonChecks(ctx, "smoke_code_course");
        }

        /// <summary>Camera / materials / rigidbodies / frame-time checks + screenshot on whatever is loaded now.</summary>
        public static IEnumerator Audit(DevPlayContext ctx, string shotName) => CommonChecks(ctx, shotName);

        /// <summary>CAMPAIGN §21 WebGL budgets on whatever is loaded: one "[SMOKE] BUDGET ..." line + one check per hard
        /// cap (rigidbodies, lights after the WebGL/Mobile LightBudget pass, point/spot within 25 m, renderers, active
        /// enemies, screen cameras). Dev scenes (L0_*) are reported, not failed.</summary>
        public static IEnumerator Budgets(DevPlayContext ctx, string label = null)
        {
            yield return null;
            string sn = string.IsNullOrEmpty(label) ? SceneManager.GetActiveScene().name : label;
            bool dev = sn.StartsWith("L0") || sn.StartsWith("smoke_code");
            var r = SceneAudit.MeasureBudgets(sn);
            ctx.Log("[SMOKE] " + r.Line());
            ctx.Log("[SMOKE]   LightBudget low-end pass: " + r.LowEndInfo);
            void C(bool ok, string what) => ctx.Check(ok || dev, "[SMOKE] budget " + what + (dev && !ok ? " (dev scene: reported, not failing)" : ""));
            C(r.Rigidbodies <= SceneAudit.CapRigidbodies, "active rigidbodies <= " + SceneAudit.CapRigidbodies + " (" + r.Rigidbodies + ")");
            C(r.LightsLowEnd <= SceneAudit.CapLights, "non-sun lights on WebGL/Mobile <= " + SceneAudit.CapLights + " (" + r.LightsLowEnd + "; raw " + r.LightsRaw + ")");
            C(r.NearLightsLowEnd <= SceneAudit.CapNearLights, "point/spot within 25 m <= " + SceneAudit.CapNearLights + " (" + r.NearLightsLowEnd + " @" + r.NearWorstAt + ")");
            C(r.Renderers <= SceneAudit.CapRenderers, "renderers <= " + SceneAudit.CapRenderers + " (" + r.Renderers + ")");
            C(r.Enemies <= SceneAudit.CapEnemies, "active enemies <= " + SceneAudit.CapEnemies + " (" + r.Enemies + ")");
            C(r.Cameras <= SceneAudit.CapCameras, "cameras rendering to screen <= " + SceneAudit.CapCameras + " (" + r.Cameras + ")");
        }

        static void CheckLevel(DevPlayContext ctx, MonoBehaviour lm, string sceneName)
        {
            // non-shipping L0 scenes built before the Level module existed use the builder's bootstrap fallback
            var fallback = lm == null ? BotReflect.FindFirst("LevelBootstrapFallback") : null;
            if (fallback != null && sceneName.StartsWith("L0"))
                ctx.Log("[SMOKE] NOTE: " + sceneName + " uses LevelBootstrapFallback (built before the Level module) - rebuild it with ShiftersBuilder.BuildLevel");
            else ctx.Check(lm != null, "[SMOKE] LevelManager present");
            var cm = CharacterManager.Instance;
            int n = cm != null ? cm.Team.Count : 0, alive = 0;
            if (cm != null) foreach (var c in cm.Team) if (c != null && c.IsAlive) alive++;
            ctx.Check(n == 3, "[SMOKE] team spawned (" + n + " characters, " + alive + " alive)");
            ctx.Check(alive == n && n > 0, "[SMOKE] whole team alive after 3 s");
            if (cm != null) ctx.Check(cm.Active != null, "[SMOKE] an active character is selected (" + BotFmt.Name(cm.Active) + ")");
        }

        static void CheckMenu(DevPlayContext ctx)
        {
            ctx.Check(UIManager.Instance != null, "[SMOKE] UIManager present");
            int ui = 0;
            foreach (var cr in Object.FindObjectsByType<CanvasRenderer>()) if (cr != null && cr.gameObject.activeInHierarchy) ui++;
            ctx.Check(ui > 0, "[SMOKE] main menu UI present (" + ui + " active UI elements)");
        }

        static IEnumerator CommonChecks(DevPlayContext ctx, string shotName)
        {
            var cam = Camera.main;
            ctx.Check(cam != null && cam.isActiveAndEnabled, "[SMOKE] main camera present");
            if (cam != null) ctx.Check(SceneAudit.CameraRendersSomething(cam, out string info), "[SMOKE] camera renders (" + info + ")");
            var bad = SceneAudit.MaterialProblems();
            ctx.Check(bad.Count == 0, "[SMOKE] no missing/default/magenta materials (" + bad.Count + " problem renderers)");
            for (int i = 0; i < bad.Count && i < 15; i++) ctx.Log("[SMOKE]   material: " + bad[i]);
            var foreign = SceneAudit.ForeignShaders();
            if (foreign.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in foreign) parts.Add(kv.Key + " x" + kv.Value);
                ctx.Log("[SMOKE] note: non-Shifters shaders in use: " + string.Join(", ", parts));
            }
            yield return Budgets(ctx);
            // frame time sample
            var samples = new List<float>(120);
            double prev = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                samples.Add((float)((now - prev) * 1000.0));
                prev = now;
            }
            samples.Sort();
            float sum = 0f;
            foreach (var s in samples) sum += s;
            ctx.Log("[SMOKE] frame time over 120 frames (batchmode, indicative only): avg " + (sum / samples.Count).ToString("0.0")
                    + " ms, p95 " + samples[Mathf.Min(samples.Count - 1, (int)(samples.Count * 0.95f))].ToString("0.0")
                    + " ms, max " + samples[samples.Count - 1].ToString("0.0") + " ms");
            ctx.Shot(shotName);
        }
    }
}
