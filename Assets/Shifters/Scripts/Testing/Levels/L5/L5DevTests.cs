// =====================================================================================
//  SHIFTERS - L5DevTests.cs   [LEVEL 5 - DevPlay scenarios]
//  Shifters.L5DevTests.Shots     : runtime-build L5, overview screenshots of every section + checks
//  Shifters.L5DevTests.BuildAndSolve : runtime-build L5 and play L5Solution on it (dev iteration)
//  Shifters.L5DevTests.SceneShots : screenshots on the generated scene (-devScene ...L5_TotalChaos.unity)
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L5DevTests
    {
        static readonly (string name, Vector3 pos, Vector3 look)[] Views =
        {
            ("01_spawn", new Vector3(0f, 4.5f, -1.5f), new Vector3(0f, 0.8f, 14f)),
            ("02_commute", new Vector3(9f, 9f, 14f), new Vector3(-2f, 0f, 36f)),
            ("03_sweeper", new Vector3(6f, 5f, 26f), new Vector3(0f, 0f, 36f)),
            ("04_laserhall", new Vector3(0.5f, 7.5f, 56f), new Vector3(1f, 2f, 82f)),
            ("05_turbo", new Vector3(13f, 7f, 62f), new Vector3(7f, 2f, 82f)),
            ("06_arena", new Vector3(-6f, 10f, 108f), new Vector3(2f, 0.4f, 130f)),
            ("07_cage", new Vector3(-11f, 5f, 121f), new Vector3(-7.6f, 2f, 127f)),
            ("08_pulley", new Vector3(-2f, 6.5f, 152f), new Vector3(1f, 1f, 168f)),
            ("09_pod", new Vector3(-8f, 8.5f, 182f), new Vector3(1f, 4f, 200f)),
            ("10_shaft", new Vector3(16f, 6f, 172f), new Vector3(6f, 2f, 190f)),
            ("11_overview", new Vector3(34f, 45f, 60f), new Vector3(0f, 0f, 110f)),
        };

        public static IEnumerator Shots(DevPlayContext ctx)
        {
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(4);
            ctx.Check(lctx != null && !lctx.Failed, "L5 runtime build");
            if (lctx != null) ctx.Log(lctx.Report());
            yield return ctx.Wait(2.0f);
            yield return Checks(ctx);
            yield return TakeViews(ctx);
        }

        public static IEnumerator SceneShots(DevPlayContext ctx)
        {
            yield return ctx.Wait(7.0f);
            yield return Checks(ctx);
            yield return TakeViews(ctx);
        }

        public static IEnumerator BuildAndSolve(DevPlayContext ctx)
        {
            int seed = BotArgs.Int("-botSeed", 0);
            if (seed != 0) TeamBot.ForceTeam(seed);
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(4);
            ctx.Check(lctx != null && !lctx.Failed, "L5 runtime build");
            yield return ctx.Wait(1.5f);
            yield return TeamBot.Run(ctx, L5Solution.Solve, null, "L5rt");
        }

        /// <summary>Runtime build + checks + overview shots, then the full solution (one dev iteration = one Unity run).</summary>
        public static IEnumerator All(DevPlayContext ctx)
        {
            int seed = BotArgs.Int("-botSeed", 0);
            if (seed != 0) TeamBot.ForceTeam(seed);
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(4);
            ctx.Check(lctx != null && !lctx.Failed, "L5 runtime build");
            if (lctx != null) ctx.Log(lctx.Report());
            yield return ctx.Wait(1.5f);
            if (BotArgs.Int("-l5Shots", 1) != 0) { yield return Checks(ctx); yield return TakeViews(ctx); }
            L5Solution.DevStartSection = BotArgs.Int("-l5From", 1);
            var opts = new BotOptions { AllowDebugAssists = L5Solution.DevStartSection > 1 };
            yield return TeamBot.Run(ctx, L5Solution.Solve, opts, "L5rt");
            L5Solution.DevStartSection = 1;
        }

        static IEnumerator Checks(DevPlayContext ctx)
        {
            string[] marks = { "spawn", "S1.P1", "S1.P2", "S1.P3", "S1.Sweeper", "S2.Lever", "S2.LaserToggle", "S3.Glass", "S3.Barrel",
                               "S3.DockPlate", "S3.Dock", "S3.BotA", "S3.BotB", "S3.Cell", "S3.Plate", "S3.Door", "S4.Lift", "S4.Block",
                               "S4.Crusher", "S4.Dispatch", "S4.Brake", "S5.Pod", "S5.V1", "S5.V2", "S5.V3", "exit",
                               "checkpoint.1", "checkpoint.2", "checkpoint.3", "checkpoint.4" };
            foreach (var m in marks) ctx.Check(LevelMark.Exists(m), "mark " + m);
            var lift = LevelMark.Find<PulleyLift>("S4.Lift");
            ctx.Check(lift != null && !lift.enabled, "pulley starts braked");
            var cr = LevelMark.Find<Crusher>("S4.Crusher");
            ctx.Check(cr != null && cr.IsActive, "basket crusher cycling while braked");
            var hz = LevelMark.Find<PuzzleHazard>("S2.H1");
            ctx.Check(hz != null && hz.IsActive, "second-half laser on before the override");
            var exit = LevelMark.Find<LevelExit>("exit");
            ctx.Check(exit != null && exit.enabled, "exit enabled while the pod is empty");
            int bad = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name.Contains("InternalErrorShader") || m.name.StartsWith("Default-Material") || m.name == "Lit")
                { if (bad < 5) ctx.Log("bad material on " + r.name); bad++; }
            }
            ctx.Check(bad == 0, "no default/magenta materials (" + bad + ")");
            yield return null;
        }

        static IEnumerator TakeViews(DevPlayContext ctx)
        {
            var go = new GameObject("L5ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 600f;
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            foreach (var v in Views)
            {
                go.transform.position = v.pos;
                go.transform.rotation = Quaternion.LookRotation(v.look - v.pos, Vector3.up);
                yield return null;
                ctx.Shot(v.name, cam);
            }
            Object.Destroy(go);
            if (main != null) ctx.Shot("12_gameplay", main);
        }
    }
}
