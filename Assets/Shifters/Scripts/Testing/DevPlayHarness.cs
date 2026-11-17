// =====================================================================================
//  SHIFTERS - DevPlayHarness.cs   [TEST INFRASTRUCTURE - runtime half]
//  Inert unless the Editor test runner (Shifters.EditorTools.DevPlayRunner) wrote
//  Temp/devplay.json before entering Play Mode. Then it:
//    * fixes the frame time (Time.captureDeltaTime = 1/60) so simulations are deterministic,
//    * invokes an optional static setup method:  void M()  |  IEnumerator M()  |  IEnumerator M(DevPlayContext)
//    * captures screenshots from Camera.main (UI included) at the requested times,
//    * counts errors/exceptions and writes Temp/devplay_result.json, then exits Play Mode.
//  Use DevPlayContext.Log / Check / Shot from setup coroutines to assert and capture.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    [Serializable]
    public class DevPlayConfig
    {
        public string setup = "";          // "Namespace.Class.Method" (static)
        public float seconds = 5f;         // simulated seconds before auto-exit (coroutine setups may end earlier/later: max of both)
        public string shotsDir = "";
        public float[] shotTimes = new float[0];
        public int shotWidth = 1280;
        public int shotHeight = 720;
        public string resultPath = "";
    }

    [Serializable]
    public class DevPlayResult
    {
        public int errors;
        public int exceptions;
        public int checksFailed;
        public int checksPassed;
        public List<string> messages = new List<string>();
        public List<string> shots = new List<string>();
        public bool setupCompleted;
    }

    public class DevPlayContext
    {
        public DevPlayResult Result;
        public DevPlayConfig Config;
        public void Log(string msg) { Debug.Log("[DEVPLAY] " + msg); Result.messages.Add(msg); }
        public bool Check(bool condition, string what)
        {
            if (condition) { Result.checksPassed++; Debug.Log("[DEVPLAY] PASS " + what); Result.messages.Add("PASS " + what); }
            else { Result.checksFailed++; Debug.LogWarning("[DEVPLAY] FAIL " + what); Result.messages.Add("FAIL " + what); }
            return condition;
        }
        /// <summary>Capture Camera.main (or the given camera) right now, UI included.</summary>
        public string Shot(string name, Camera cam = null) => DevPlayHarness.Capture(Config, Result, name, cam);
        /// <summary>Wait a number of simulated seconds (frames at 60fps).</summary>
        public IEnumerator Wait(float seconds) { int frames = Mathf.Max(1, Mathf.RoundToInt(seconds * 60f)); for (int i = 0; i < frames; i++) yield return null; }
    }

    public class DevPlayHarness : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static DevPlayContext Context { get; private set; }
        static DevPlayConfig _config;
        static DevPlayResult _result;

        public static string ConfigPath => Path.Combine(Application.dataPath, "../Temp/devplay.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; Context = null; _config = null; _result = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_EDITOR
            if (!File.Exists(ConfigPath)) return;
            try { _config = JsonUtility.FromJson<DevPlayConfig>(File.ReadAllText(ConfigPath)); }
            catch (Exception e) { Debug.LogError("[DEVPLAY] bad config: " + e.Message); return; }
            File.Delete(ConfigPath);
            Active = true;
            _result = new DevPlayResult();
            Context = new DevPlayContext { Result = _result, Config = _config };
            Application.logMessageReceived += OnLog;
            Time.captureDeltaTime = 1f / 60f;
            var go = new GameObject("[DevPlayHarness]");
            DontDestroyOnLoad(go);
            go.AddComponent<DevPlayHarness>();
#endif
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (_result == null) return;
            if (condition.Contains("SearchDatabase") || stack.Contains("UnityEditor.Search")) return; // known batchmode noise
            if (type == LogType.Exception) { _result.exceptions++; _result.messages.Add("EXCEPTION " + condition + " @ " + FirstLine(stack)); }
            else if (type == LogType.Error || type == LogType.Assert) { _result.errors++; _result.messages.Add("ERROR " + condition); }
        }

        static string FirstLine(string s) { if (string.IsNullOrEmpty(s)) return ""; int i = s.IndexOf('\n'); return i < 0 ? s : s.Substring(0, i); }

        IEnumerator Start()
        {
            Debug.Log("[DEVPLAY] harness started, setup=" + _config.setup);
            IEnumerator setupRoutine = null;
            if (!string.IsNullOrEmpty(_config.setup))
            {
                try { setupRoutine = InvokeSetup(_config.setup); }
                catch (Exception e) { Debug.LogException(e); }
            }
            float start = Time.time;
            int shotIndex = 0;
            bool setupDone = setupRoutine == null;
            if (setupRoutine != null) StartCoroutine(RunSetup(setupRoutine, () => setupDone = true));
            var times = new List<float>(_config.shotTimes ?? new float[0]);
            times.Sort();
            while (true)
            {
                float t = Time.time - start;
                while (shotIndex < times.Count && t >= times[shotIndex])
                {
                    Capture(_config, _result, "t" + times[shotIndex].ToString("0.0"), null);
                    shotIndex++;
                }
                if (t >= _config.seconds && setupDone && shotIndex >= times.Count) break;
                if (t > _config.seconds + 120f) { _result.messages.Add("TIMEOUT waiting for setup coroutine"); break; }
                yield return null;
            }
            _result.setupCompleted = setupDone;
            Finish();
        }

        IEnumerator RunSetup(IEnumerator routine, Action done)
        {
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) break; current = routine.Current; }
                catch (Exception e) { Debug.LogException(e); break; }
                yield return current;
            }
            done();
        }

        static IEnumerator InvokeSetup(string fullName)
        {
            int dot = fullName.LastIndexOf('.');
            string typeName = fullName.Substring(0, dot), methodName = fullName.Substring(dot + 1);
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { type = asm.GetType(typeName); if (type != null) break; }
            if (type == null) throw new Exception("[DEVPLAY] setup type not found: " + typeName);
            var m = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) throw new Exception("[DEVPLAY] setup method not found: " + fullName);
            var ps = m.GetParameters();
            object ret = ps.Length == 1 ? m.Invoke(null, new object[] { Context }) : m.Invoke(null, null);
            return ret as IEnumerator;
        }

        public static string Capture(DevPlayConfig cfg, DevPlayResult res, string name, Camera cam)
        {
            try
            {
                if (cam == null) cam = Camera.main;
                if (cam == null) { var all = FindObjectsByType<Camera>(); if (all.Length > 0) cam = all[0]; }
                if (cam == null || string.IsNullOrEmpty(cfg.shotsDir)) return null;
                Directory.CreateDirectory(cfg.shotsDir);
                int w = cfg.shotWidth, h = cfg.shotHeight;
                var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                // Overlay canvases are not rendered into RenderTextures: temporarily render them through the camera.
                var canvases = FindObjectsByType<Canvas>();
                var restore = new List<(Canvas c, RenderMode m, Camera wc, float pd)>();
                foreach (var c in canvases)
                {
                    if (!c.isRootCanvas || !c.isActiveAndEnabled || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                    c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.4f);
                }
                Canvas.ForceUpdateCanvases();
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;
                foreach (var r in restore) { r.c.renderMode = r.m; r.c.worldCamera = r.wc; r.c.planeDistance = r.pd; }
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                string safe = name.Replace(' ', '_').Replace('/', '_');
                string path = Path.Combine(cfg.shotsDir, safe + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Destroy(tex);
                res?.shots.Add(path);
                Debug.Log("[DEVPLAY] shot " + path);
                return path;
            }
            catch (Exception e) { Debug.LogException(e); return null; }
        }

        void Finish()
        {
            Application.logMessageReceived -= OnLog;
            Time.captureDeltaTime = 0f;
            try
            {
                if (!string.IsNullOrEmpty(_config.resultPath)) File.WriteAllText(_config.resultPath, JsonUtility.ToJson(_result, true));
            }
            catch (Exception e) { Debug.LogError(e); }
            Debug.Log("[DEVPLAY] done errors=" + _result.errors + " exceptions=" + _result.exceptions + " checksFailed=" + _result.checksFailed + " checksPassed=" + _result.checksPassed);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }
    }
}
