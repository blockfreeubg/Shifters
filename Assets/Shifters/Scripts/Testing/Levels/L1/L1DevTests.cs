// =====================================================================================
//  SHIFTERS - L1DevTests.cs   (LEVEL 1 DevPlay scenarios)
//    devplay.sh <clone> l1_rt -devSetup Shifters.L1DevTests.RuntimeBuildAndSolve -devSeconds 1
//      builds L1 at runtime, takes section review shots, then plays L1Solution on the current team.
//    devplay.sh <clone> l1_shots -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSetup Shifters.L1DevTests.SceneShots
// =====================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shifters
{
    public static class L1DevTests
    {
        static readonly (string name, Vector3 pos, Vector3 target)[] Views =
        {
            ("s0_bay", new Vector3(13f, 9f, -15f), new Vector3(0f, 1f, 10f)),
            ("s0_warmup", new Vector3(-12f, 7f, 8f), new Vector3(0f, 1f, 20f)),
            ("s1_switching", new Vector3(10f, 10f, 27f), new Vector3(0f, 0f, 43f)),
            ("s2_hub", new Vector3(0f, 14f, 44f), new Vector3(0f, 0f, 64f)),
            ("s2_lanes", new Vector3(4f, 42f, 26f), new Vector3(4f, 0f, 60f)),
            ("s3_bridge", new Vector3(9f, 9f, 78f), new Vector3(0f, 0f, 93f)),
            ("s4_stack", new Vector3(-6f, 8f, 99f), new Vector3(1f, 2f, 114f)),
            ("s4_throw", new Vector3(-6f, 9f, 126f), new Vector3(3f, 3f, 142f)),
            ("s5_range", new Vector3(5f, 9f, 148f), new Vector3(-13f, 1f, 162f)),
            ("s6_booth", new Vector3(8f, 10f, 179f), new Vector3(-6f, 0f, 194f)),
            ("s6_duct", new Vector3(-6f, 16f, 192f), new Vector3(-17f, 0f, 205f)),
            ("s7_graduation", new Vector3(0f, 10f, 221f), new Vector3(0f, 1f, 246f)),
            ("overview", new Vector3(90f, 140f, 40f), new Vector3(0f, 0f, 125f)),
            // close-ups of everything that faces a player (signs, posters, whiteboards, light strips, the throw pad, the range)
            ("c_s0_whiteboard", new Vector3(-1.5f, 2.2f, -8.5f), new Vector3(-6.9f, 1.3f, -4.5f)),
            ("c_s1_panels", new Vector3(0f, 2.2f, 31f), new Vector3(-9f, 3.4f, 38f)),
            ("c_s2_portals_west", new Vector3(6f, 3f, 60f), new Vector3(-9f, 4f, 63f)),
            ("c_s2_portal_spd", new Vector3(-5f, 3f, 58f), new Vector3(9f, 4.3f, 58f)),
            ("c_s4_throw_pad", new Vector3(4.6f, 4.2f, 129.5f), new Vector3(4.6f, 1.6f, 140f)),
            ("c_s5_firing_line", new Vector3(2.5f, 2.4f, 161f), new Vector3(-15f, 1f, 161f)),
            ("c_s6_booth_front", new Vector3(-2f, 2.6f, 179.5f), new Vector3(-6.5f, 3.2f, 188f)),
            ("c_s6_duct_arrow", new Vector3(-19.6f, 1.8f, 192.6f), new Vector3(-17.2f, 2.1f, 197.5f)),
            ("c_s6_duct_exit", new Vector3(-19f, 1.8f, 205.5f), new Vector3(-17f, 2.6f, 215.8f)),
            ("c_s7_panels", new Vector3(0f, 2.6f, 224f), new Vector3(-12f, 4.4f, 231f)),
            ("c_s7_benches", new Vector3(-6.5f, 2f, 249f), new Vector3(-6f, 0.6f, 256f)),
            ("c_s2_hub_back", new Vector3(0f, 3f, 66f), new Vector3(0f, 4.6f, 52f)),
        };

        /// <summary>Runtime build + review shots + the full solution with the session's team.</summary>
        public static IEnumerator RuntimeBuildAndSolve(DevPlayContext ctx)
        {
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(0);
            ctx.Check(lctx != null && !lctx.Failed, "L1 runtime build succeeded");
            if (lctx != null) ctx.Log(lctx.Report());
            yield return ctx.Wait(2f);
            ctx.Shot("spawn_view");
            yield return ReviewShots(ctx);
            L1Solution.DevAssistPushPlate = !PushOntoPlateWorks();
            ctx.Log("DevAssistPushPlate=" + L1Solution.DevAssistPushPlate);
            yield return TeamBot.Run(ctx, L1Solution.Solve, L1Solution.Options(), "L1rt");
            L1Solution.DevAssistPushPlate = false;
            ctx.Shot("final");
        }

        /// <summary>Review shots of a built scene (no solution).</summary>
        public static IEnumerator SceneShots(DevPlayContext ctx)
        {
            yield return ctx.Wait(2f);
            ctx.Shot("spawn_view");
            yield return ReviewShots(ctx);
        }

        /// <summary>Does the published Pushable climb small steps (architect's step-up fix)? Detected by member name.</summary>
        static bool PushOntoPlateWorks()
        {
            foreach (var m in typeof(Pushable).GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                if (m.Name.IndexOf("Step", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>Section overviews + close-ups (UI canvases hidden) from a free camera; works on any L1 build.</summary>
        public static IEnumerator ReviewShots(DevPlayContext ctx)
        {
            var go = new GameObject("L1ReviewCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            var canvases = Object.FindObjectsByType<Canvas>();
            foreach (var c in canvases) if (c != null) c.enabled = false;
            foreach (var v in Views)
            {
                go.transform.position = v.pos;
                go.transform.rotation = Quaternion.LookRotation(v.target - v.pos, Vector3.up);
                yield return null;
                ctx.Shot(v.name, cam);
            }
            foreach (var c in canvases) if (c != null) c.enabled = true;
            Object.Destroy(go);
        }
    }
}
