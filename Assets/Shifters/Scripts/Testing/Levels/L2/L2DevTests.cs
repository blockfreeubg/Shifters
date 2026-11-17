using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Level 2 development scenarios.
    ///   devplay.sh &lt;proj&gt; l2_rt -devSetup Shifters.L2DevTests.RuntimeSolve -devSeconds 1   (runtime build + shots + solution)
    ///   devplay.sh &lt;proj&gt; l2_shots -devScene Assets/Shifters/Scenes/L2_WeightProblem.unity -devSetup Shifters.L2DevTests.SceneShots
    /// </summary>
    public static class L2DevTests
    {
        public static IEnumerator RuntimeSolve(DevPlayContext ctx)
        {
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(1);
            ctx.Check(lctx != null && !lctx.Failed, "L2 builds at runtime");
            if (lctx != null)
            {
                ctx.Log(lctx.Report());
                ctx.Check(lctx.CheckpointCount == 3, "3 checkpoints (" + lctx.CheckpointCount + ")");
                ctx.Check(lctx.ExitCount == 1, "1 exit");
            }
            yield return ctx.Wait(5f);   // level-start banner gone
            Shots(ctx);
            yield return TeamBot.Run(ctx, L2Solution.Solve, null, "L2rt");
        }

        public static IEnumerator SceneShots(DevPlayContext ctx)
        {
            yield return ctx.Wait(5f);
            Shots(ctx);
            ctx.Check(MaterialAudit(ctx), "no default/magenta materials");
        }

        static void Shots(DevPlayContext ctx)
        {
            BotDiagnostics.ShotFrom(ctx, "l2_01_spawn", new Vector3(0.5f, 3.2f, -4.5f), new Vector3(0f, 1.2f, 12f));
            BotDiagnostics.ShotFrom(ctx, "l2_02_heavygate", new Vector3(6.5f, 7.5f, 7f), new Vector3(-1f, 0f, 21f));
            BotDiagnostics.ShotFrom(ctx, "l2_03_elevator", new Vector3(-4.5f, 4.5f, 29f), new Vector3(0f, 3f, 41f));
            BotDiagnostics.ShotFrom(ctx, "l2_04_catwalk", new Vector3(10f, 12f, 43f), new Vector3(1f, 4f, 56f));
            BotDiagnostics.ShotFrom(ctx, "l2_05_pulley", new Vector3(11f, 14.5f, 64f), new Vector3(0f, 7f, 79f));
            BotDiagnostics.ShotFrom(ctx, "l2_06_upper", new Vector3(-9f, 19f, 79f), new Vector3(2f, 12f, 91f));
            BotDiagnostics.ShotFrom(ctx, "l2_07_scale", new Vector3(-8.5f, 18f, 96f), new Vector3(1f, 12f, 110f));
            BotDiagnostics.ShotFrom(ctx, "l2_08_exit", new Vector3(8f, 19f, 108f), new Vector3(2.5f, 12f, 123f));
            BotDiagnostics.ShotFrom(ctx, "l2_09_overview", new Vector3(-48f, 46f, 18f), new Vector3(0f, 4f, 66f));
        }

        /// <summary>True if no renderer under the level root uses a missing, default or error material.</summary>
        public static bool MaterialAudit(DevPlayContext ctx)
        {
            var root = GameObject.Find(LevelBuildContext.RootName(LevelCatalog.Get(1)));
            if (root == null) { ctx.Log("material audit: level root not found"); return false; }
            int bad = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    bool broken = m == null || m.shader == null || m.shader.name.Contains("InternalErrorShader") || m.name.StartsWith("Default-") || m.name == "Lit";
                    if (broken && r.GetComponent<TMPro.TextMeshPro>() == null)
                    {
                        if (bad < 8) ctx.Log("bad material on " + r.name + ": " + (m == null ? "null" : m.name + " / " + (m.shader != null ? m.shader.name : "no shader")));
                        bad++;
                    }
                }
            }
            ctx.Log("material audit: " + bad + " bad");
            return bad == 0;
        }
    }
}
