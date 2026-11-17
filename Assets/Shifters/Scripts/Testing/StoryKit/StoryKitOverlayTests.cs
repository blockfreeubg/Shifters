// SHIFTERS - DevPlay: StoryOverlay L1-L5 (CAMPAIGN 8.3).
//   devplay.sh <clone> storykit_overlays -devSetup Shifters.StoryKitOverlayTests.Run -devSeconds 5
// For each of L1..L5: load the built scene, let the level start (no overlay outside campaign mode), Apply the overlay,
// assert it added no collider / light / rigidbody anywhere, no missing material, SceneAudit sign checks under the
// overlay root pass, then contact-sheet every placed piece (4 x 3 tiles) + one player-eye view from the spawn mark.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class StoryKitOverlayTests
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            for (int n = 1; n <= 5; n++) yield return Level(ctx, n);
            StoryOverlay.Clear();
        }

        static IEnumerator Load(string sceneName)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Shifters/Scenes/" + sceneName + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(sceneName);
#endif
            yield return null;
            yield return null;
        }

        static int Count<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include).Length;

        static IEnumerator Level(DevPlayContext ctx, int n)
        {
            var info = LevelCatalog.Get(n - 1);
            string scene = info != null ? info.SceneName : null;
            ctx.Check(!string.IsNullOrEmpty(scene), "L" + n + ": catalog scene");
            if (string.IsNullOrEmpty(scene)) yield break;
            yield return Load(scene);
            yield return ctx.Wait(2.5f);
            ctx.Check(SceneManager.GetActiveScene().name == scene, "L" + n + ": scene " + scene + " loaded");
            if (!StoryOverlay.AutoApplyOnLevelStart)
                ctx.Check(StoryOverlay.Current == null && GameObject.Find("StoryOverlay_L" + n) == null, "L" + n + ": no overlay outside campaign mode");

            int col0 = Count<Collider>(), light0 = Count<Light>(), rb0 = Count<Rigidbody>();
            var root = StoryOverlay.ApplyAndGet("L" + n);
            int col1 = Count<Collider>(), light1 = Count<Light>(), rb1 = Count<Rigidbody>();
            ctx.Check(root != null && StoryOverlay.CurrentLevel == n, "L" + n + ": overlay applied");
            if (root == null) yield break;
            var pieces = new List<KeyValuePair<string, GameObject>>(StoryLayoutUtil.LastPieces);
            ctx.Log("L" + n + " overlay: " + pieces.Count + " pieces");
            ctx.Check(pieces.Count >= 5, "L" + n + ": at least 5 placements (" + pieces.Count + ")");
            ctx.Check(root.GetComponentsInChildren<Collider>(true).Length == 0 && col1 == col0, "L" + n + ": overlay added no colliders (" + col0 + " -> " + col1 + ")");
            ctx.Check(root.GetComponentsInChildren<Light>(true).Length == 0 && light1 == light0, "L" + n + ": overlay added no lights");
            ctx.Check(rb1 == rb0, "L" + n + ": overlay added no rigidbodies");
            int bad = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (m == null || m.shader == null || m.shader.name.Contains("InternalError") || m.name.StartsWith("Default-")) bad++;
            ctx.Check(bad == 0, "L" + n + ": overlay materials ok (" + bad + " bad)");
            yield return null;

            // sign audit of the overlay's texts only
            var moving = new HashSet<Transform>();
            yield return SceneAudit.CollectMovingTexts(moving);
            var res = SceneAudit.SignProblems(moving, root.transform);
            foreach (var i in res.Issues) ctx.Log("  SIGN " + i.Kind + " " + i.What + " '" + i.Text + "' " + i.Path + " " + i.Detail);
            ctx.Check(res.Issues.Count == 0, "L" + n + ": overlay sign audit clean (" + res.Summary + ")");

            // screenshots: every placement + the player's first view from the spawn
            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
                var gos = new List<GameObject>(); var dirs = new List<Vector3>();
                foreach (var kv in pieces)
                {
                    if (kv.Value == null) continue;
                    StoryKitTests.Bounds(kv.Value, out int rc); if (rc == 0) continue;   // logic-only (line spots)
                    kv.Value.name = kv.Value.name + "[" + kv.Key + "]";
                    gos.Add(kv.Value);
                    var f = kv.Value.transform.forward; f.y = 0f;
                    dirs.Add((f.sqrMagnitude > 0.01f ? f.normalized : Vector3.back) + Vector3.up * 0.22f);
                }
                for (int s = 0; s * 12 < gos.Count; s++)
                    StoryKitTests.Sheet(ctx, cam, "overlay_L" + n + "_" + (s + 1), gos.GetRange(s * 12, Mathf.Min(12, gos.Count - s * 12)), Vector3.back, 4, 3, dirs.GetRange(s * 12, Mathf.Min(12, gos.Count - s * 12)));
                var spawn = StoryLayoutUtil.Mark("spawn");
                if (spawn != null)
                {
                    var pos = spawn.position + Vector3.up * 2.2f - StoryLayoutUtil.Flat(spawn.forward) * 3f;
                    var prevP = cam.transform.position; var prevR = cam.transform.rotation; float prevF = cam.fieldOfView;
                    StoryKitTests.Pose(cam, pos, pos + StoryLayoutUtil.Flat(spawn.forward) * 10f + Vector3.up * 1.5f, 70f);
                    ctx.Shot("overlay_L" + n + "_spawnview", cam);
                    cam.transform.SetPositionAndRotation(prevP, prevR); cam.fieldOfView = prevF;
                }
            }
            yield return ctx.Wait(0.5f);
        }
    }
}
