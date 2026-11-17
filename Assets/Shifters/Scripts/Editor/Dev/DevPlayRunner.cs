// =====================================================================================
//  SHIFTERS - DevPlayRunner.cs   [TEST INFRASTRUCTURE - editor half, batchmode entry points]
//
//  Play Mode scenario (exits Unity when done; exit code 0 = no errors/exceptions/failed checks):
//    Unity -batchmode -projectPath P -executeMethod Shifters.EditorTools.DevPlayRunner.Play
//          [-devScene Assets/Shifters/Scenes/X.unity]   (omit = fresh empty scene with a sun + camera)
//          [-devSetup Shifters.MyTests.Setup]            (static void/IEnumerator [DevPlayContext])
//          [-devSeconds 6] [-devShots /abs/dir] [-devShotTimes 0.5,2,4] [-devSize 1280x720]
//          [-devResult /abs/result.json]
//  Edit-mode static camera shots of a scene (no play):
//    -executeMethod Shifters.EditorTools.DevPlayRunner.SceneShots -devScene ... -devShots dir
//          -devViews "name:px,py,pz:tx,ty,tz;name2:..."      (camera position : look-at target)
// =====================================================================================
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters.EditorTools
{
    public static class DevPlayRunner
    {
        static string Arg(string name, string def = "")
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return def;
        }

        public static void Play()
        {
            ShaderUtil.allowAsyncCompilation = false; // first-run screenshots must not show placeholder shaders
            var cfg = new DevPlayConfig
            {
                setup = Arg("-devSetup"),
                seconds = float.Parse(Arg("-devSeconds", "5"), System.Globalization.CultureInfo.InvariantCulture),
                shotsDir = Arg("-devShots"),
                resultPath = Arg("-devResult", Path.GetFullPath("Temp/devplay_result.json")),
            };
            var times = Arg("-devShotTimes");
            if (!string.IsNullOrEmpty(times))
                cfg.shotTimes = Array.ConvertAll(times.Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            var size = Arg("-devSize");
            if (!string.IsNullOrEmpty(size)) { var p = size.Split('x'); cfg.shotWidth = int.Parse(p[0]); cfg.shotHeight = int.Parse(p[1]); }

            string scene = Arg("-devScene");
            if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            else CreateDefaultScene();

            File.WriteAllText(Path.GetFullPath("Temp/devplay.json"), JsonUtility.ToJson(cfg));
            if (File.Exists(cfg.resultPath)) File.Delete(cfg.resultPath);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Debug.Log("[DEVPLAY] entering play mode");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            string resultPath = Arg("-devResult", Path.GetFullPath("Temp/devplay_result.json"));
            int code = 1;
            if (File.Exists(resultPath))
            {
                var r = JsonUtility.FromJson<DevPlayResult>(File.ReadAllText(resultPath));
                code = (r.errors + r.exceptions + r.checksFailed) == 0 && r.setupCompleted ? 0 : 2;
                Debug.Log("[DEVPLAY] RESULT code=" + code + "\n" + File.ReadAllText(resultPath));
            }
            else Debug.LogError("[DEVPLAY] no result file");
            EditorApplication.Exit(code);
        }

        /// <summary>Empty scene with a camera + directional light + ground (for isolated module tests).</summary>
        public static void CreateDefaultScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        public static void SceneShots()
        {
            ShaderUtil.allowAsyncCompilation = false;
            string scene = Arg("-devScene");
            if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            string dir = Arg("-devShots");
            Directory.CreateDirectory(dir);
            int w = 1280, h = 720;
            var size = Arg("-devSize");
            if (!string.IsNullOrEmpty(size)) { var p = size.Split('x'); w = int.Parse(p[0]); h = int.Parse(p[1]); }
            var camGo = new GameObject("DevShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f;
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            foreach (var view in Arg("-devViews").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = view.Split(':');
                Vector3 V(string s) { var c = s.Split(','); return new Vector3(F(c[0]), F(c[1]), F(c[2])); }
                cam.transform.position = V(parts[1]);
                cam.transform.LookAt(V(parts[2]));
                var rt = new RenderTexture(w, h, 24);
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, parts[0] + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex); rt.Release();
                Debug.Log("[DEVPLAY] scene shot " + parts[0]);
            }
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        static float F(string s) => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
