// =====================================================================================
//  SHIFTERS - BuilderTests.cs   (BUILDER module DevPlay scenarios)
//   RuntimeSandbox : no -devScene. LevelRuntimeBuilder builds L0_Sandbox into the empty scene; checks geometry
//                    metrics by raycast, marks, team spawn, camera, materials; screenshots.
//   BuiltScenes    : -devScene Assets/Shifters/Scenes/L0_Sandbox.unity (made by ShiftersBuilder.BuildAll); same
//                    checks on the saved scene, then loads MainMenu and checks/screenshots it.
// =====================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class BuilderTests
    {
        public static IEnumerator RuntimeSandbox(DevPlayContext ctx)
        {
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(L0_Sandbox.Index);
            ctx.Check(lctx != null && !lctx.Failed, "runtime build of L0_Sandbox succeeded");
            if (lctx == null) yield break;
            ctx.Log(lctx.Report());
            ctx.Check(lctx.SpawnPoints.Length == 3, "3 spawn points");
            ctx.Check(lctx.Root != null && lctx.Geometry.childCount > 0, "roots created and geometry built");
            ctx.Check(Kit.Theme == LevelTheme.Training, "Kit.Theme set to the level theme");
            yield return VerifySandbox(ctx, "rt");
            // rebuild in place must replace the old level, not duplicate it
            var again = LevelRuntimeBuilder.BuildInCurrentScene(L0_Sandbox.Index);
            yield return null;
            int roots = 0;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == LevelBuildContext.RootName(again.Info)) roots++;
            ctx.Check(roots == 1, "rebuild replaces the previous level root (roots=" + roots + ")");
            ctx.Check(LevelMark.Find("bridge.far") != null && LevelMark.Find("bridge.far").transform.IsChildOf(again.Root), "marks resolve to the rebuilt level");
        }

        public static IEnumerator BuiltScenes(DevPlayContext ctx)
        {
            ctx.Check(SceneManager.GetActiveScene().name == L0_Sandbox.SceneName, "opened scene " + SceneManager.GetActiveScene().name);
            var lmType = KitCatalog.FindType("LevelManager");
            bool hasManager = lmType != null ? Object.FindAnyObjectByType(lmType) != null : Object.FindAnyObjectByType<LevelBootstrapFallback>() != null;
            ctx.Check(hasManager, "scene contains " + (lmType != null ? "LevelManager" : "LevelBootstrapFallback"));
            yield return VerifySandbox(ctx, "scene");

            // ---- main menu
            string path = LevelCatalog.ScenePath(LevelCatalog.MainMenuScene);
            if (SceneUtility.GetBuildIndexByScenePath(path) >= 0) SceneManager.LoadScene(LevelCatalog.MainMenuScene);
            else
            {
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            }
            ctx.Check(SceneUtility.GetBuildIndexByScenePath(path) == 0, "MainMenu is Build Settings scene 0");
            yield return ctx.Wait(2.5f);
            ctx.Check(SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene, "MainMenu scene loaded");
            ctx.Check(Camera.main != null, "MainMenu has a main camera");
            ctx.Check(GameObject.Find("MenuStage") != null, "MainMenu has the MenuStage marker");
            ctx.Log("UIManager present: " + (UIManager.Instance != null) + ", AnyMenuOpen: " + (UIManager.Instance != null && UIManager.Instance.AnyMenuOpen));
            ScanRenderers(ctx, "menu");
            ctx.Shot("mainmenu");
        }

        /// <summary>Shared checks on a built sandbox (runtime or loaded scene).</summary>
        static IEnumerator VerifySandbox(DevPlayContext ctx, string prefix)
        {
            yield return null;
            // marks
            foreach (var id in new[] { "spawn", "checkpoint.1", "exit", "gap2.takeoff", "gap2.landing", "stack.base", "stack.ledge", "throw.spot", "throw.ledge", "bridge.near", "bridge.far", "push.start", "hack.spot", "shoot.spot" })
                ctx.Check(LevelMark.Find(id) != null, prefix + ": mark '" + id + "'");
            ctx.Check(LevelMark.FindAll("bridge.").Count == 2, prefix + ": FindAll(\"bridge.\") returns 2");
            // geometry metrics by raycast
            Probe(ctx, prefix, "spawn floor", new Vector3(3.5f, 0f, 3f), 0f);
            Probe(ctx, prefix, "2 m gap (catch floor)", new Vector3(0f, 0f, 8f), L0_Sandbox.CatchY);
            Probe(ctx, prefix, "stack ledge 2.5 m", LevelMark.Position("stack.ledge"), L0_Sandbox.StackHeight);
            Probe(ctx, prefix, "throw ledge 3.5 m", LevelMark.Position("throw.ledge"), L0_Sandbox.ThrowHeight);
            Probe(ctx, prefix, "bridge gap (catch floor)", new Vector3(0f, 0f, 23.6f), L0_Sandbox.CatchY);
            Probe(ctx, prefix, "far side of the bridge", LevelMark.Position("bridge.far"), 0f);
            ctx.Check(Mathf.Abs(L0_Sandbox.BridgeEnd - L0_Sandbox.BridgeStart - 5.2f) < 0.01f && Mathf.Abs(L0_Sandbox.Gap2End - L0_Sandbox.Gap2Start - 2f) < 0.01f, prefix + ": gap metrics 2.0 m / 5.2 m");

            yield return ctx.Wait(1.5f);
            var cm = CharacterManager.Instance;
            ctx.Check(cm != null && cm.Team.Count == 3, prefix + ": team of 3 spawned (" + (cm != null ? cm.Team.Count : 0) + ")");
            ctx.Check(cm != null && cm.Active != null, prefix + ": an active character");
            var spawn = LevelMark.Position("spawn");
            if (cm != null)
                foreach (var c in cm.Team)
                    ctx.Check(c != null && Vector3.Distance(c.transform.position, spawn) < 5f && c.transform.position.y > -1f,
                        prefix + ": " + (c != null ? c.name : "null") + " stands at the spawn (" + (c != null ? c.transform.position.ToString("F1") : "") + ")");
            var cam = Camera.main;
            ctx.Check(cam != null && cam.GetComponent<ShiftersCamera>() != null, prefix + ": main camera with ShiftersCamera");
            ctx.Check(cam != null && cam.GetComponent<AudioListener>() != null, prefix + ": main camera has an AudioListener");
            ScanRenderers(ctx, prefix);
            ctx.Shot(prefix + "_spawn");
            var ov = new GameObject("OverviewCam").AddComponent<Camera>();
            ov.fieldOfView = 55f; ov.farClipPlane = 600f;
            if (cam != null) { ov.clearFlags = cam.clearFlags; ov.backgroundColor = cam.backgroundColor; }
            ov.transform.position = new Vector3(26f, 22f, -14f);
            ov.transform.LookAt(new Vector3(0f, 0f, 22f));
            ctx.Shot(prefix + "_overview", ov);
            ov.transform.position = new Vector3(-12f, 9f, 9f);
            ov.transform.LookAt(new Vector3(1f, 1.5f, 24f));
            ctx.Shot(prefix + "_stations", ov);
            Object.Destroy(ov.gameObject);
        }

        static void Probe(DevPlayContext ctx, string prefix, string what, Vector3 at, float expectedY)
        {
            bool hit = Physics.Raycast(new Vector3(at.x, 12f, at.z), Vector3.down, out var h, 40f, ~0, QueryTriggerInteraction.Ignore);
            ctx.Check(hit && Mathf.Abs(h.point.y - expectedY) < 0.05f, prefix + ": " + what + " surface at y=" + expectedY + " (hit " + (hit ? h.point.y.ToString("F2") : "none") + ")");
        }

        /// <summary>No renderer may use a missing/magenta/default material; with Shifters shaders present, all must be Shifters/URP-particles/TMP.</summary>
        public static void ScanRenderers(DevPlayContext ctx, string prefix)
        {
            bool strict = Shader.Find("Shifters/StylizedLit") != null;
            int total = 0, bad = 0, foreign = 0;
            string firstBad = null, firstForeign = null;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                total++;
                foreach (var m in r.sharedMaterials)
                {
                    string n = m != null && m.shader != null ? m.shader.name : null;
                    if (m == null || n == null || n == "Hidden/InternalErrorShader" || m.name.StartsWith("Default-"))
                    { bad++; if (firstBad == null) firstBad = r.name + " (" + (m != null ? m.name : "null") + ")"; continue; }
                    if (!(n.StartsWith("Shifters/") || n.StartsWith("Universal Render Pipeline/Particles") || n.StartsWith("TextMeshPro") || n.StartsWith("TMP")))
                    { foreign++; if (firstForeign == null) firstForeign = r.name + " (" + n + ")"; }
                }
            }
            ctx.Check(bad == 0, prefix + ": no missing/magenta/default materials on " + total + " renderers" + (firstBad != null ? " - first: " + firstBad : ""));
            if (strict) ctx.Check(foreign == 0, prefix + ": every material uses a Shifters/particles/TMP shader" + (firstForeign != null ? " - first: " + firstForeign : ""));
            else ctx.Log(prefix + ": Shifters shaders not in project yet; " + foreign + " material slots use fallback shaders");
        }
    }
}
