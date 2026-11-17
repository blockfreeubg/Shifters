// =====================================================================================
//  SHIFTERS - BotBatch.cs   [BOTS module - editor batch entry points]
//  Shifters.EditorTools.BotBatch.SmokeAllScenes
//      Runs Shifters.SmokeTests.Run in Play Mode on MainMenu + every level scene that exists
//      (LevelCatalog order, then any other scene in Assets/Shifters/Scenes) in ONE Unity launch,
//      prints "[SMOKE] SUMMARY ..." lines and exits (0 = all scenes clean).
//      Args: -devShots <dir> (screenshots per scene), -smokeScenes A,B (optional subset by scene name).
//  Shifters.EditorTools.BotBatchSelfTest.Run proves this runner on two throwaway scenes.
//  Use via $SP/tools/run_smoke.sh <project>.
// =====================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class BotBatch
    {
        static readonly List<string> _scenes = new List<string>();
        static readonly List<string> _summary = new List<string>();
        static int _index, _failures;
        static string _shotsBase = "";

        static string Arg(string name, string def = "")
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return def;
        }

        /// <summary>Called right before the editor exits with the summary code (self-test cleanup).</summary>
        public static Action Finished;

        public static void SmokeAllScenes() => RunScenes(CollectScenes(Arg("-smokeScenes", "")).ToArray());

        /// <summary>Smoke-test exactly these scene paths (one Play Mode session each), then exit.</summary>
        public static void RunScenes(string[] scenePaths)
        {
            ShaderUtil.allowAsyncCompilation = false;
            _scenes.Clear(); _summary.Clear(); _index = 0; _failures = 0;
            _shotsBase = Arg("-devShots", Path.GetFullPath("Temp/smoke_shots"));
            _scenes.AddRange(scenePaths);
            Debug.Log("[SMOKE] scenes to test: " + (_scenes.Count == 0 ? "(none)" : string.Join(", ", _scenes)));
            if (_scenes.Count == 0)
            {
                Debug.Log("[SMOKE] SUMMARY no scene files under " + LevelCatalog.ScenesFolder + " yet - nothing to test");
                Finished?.Invoke();
                EditorApplication.Exit(3);
                return;
            }
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            StartNext();
        }

        /// <summary>MainMenu, L1..L5 (catalog order), then any other scene in the folder. Optional name filter.</summary>
        public static List<string> CollectScenes(string filterCsv)
        {
            var ordered = new List<string>();
            void AddIf(string sceneName)
            {
                string p = LevelCatalog.ScenePath(sceneName);
                if (File.Exists(p) && !ordered.Contains(p)) ordered.Add(p);
            }
            AddIf(LevelCatalog.MainMenuScene);
            foreach (var l in LevelCatalog.Levels) AddIf(l.SceneName);
            if (Directory.Exists(LevelCatalog.ScenesFolder))
            {
                var extra = Directory.GetFiles(LevelCatalog.ScenesFolder, "*.unity", SearchOption.AllDirectories);
                Array.Sort(extra, StringComparer.Ordinal);
                foreach (var f in extra) { var p = f.Replace('\\', '/'); if (!ordered.Contains(p)) ordered.Add(p); }
            }
            if (string.IsNullOrEmpty(filterCsv)) return ordered;
            var keep = new List<string>();
            var names = new HashSet<string>(filterCsv.Split(','));
            foreach (var p in ordered) if (names.Contains(Path.GetFileNameWithoutExtension(p))) keep.Add(p);
            return keep;
        }

        static string ResultPath => Path.GetFullPath("Temp/smoke_result.json");

        static void StartNext()
        {
            if (_index >= _scenes.Count) { Finish(); return; }
            string path = _scenes[_index];
            string name = Path.GetFileNameWithoutExtension(path);
            try { EditorSceneManager.OpenScene(path, OpenSceneMode.Single); }
            catch (Exception e)
            {
                Record(name, false, "could not open scene: " + e.Message);
                _index++;
                EditorApplication.delayCall += StartNext;
                return;
            }
            var cfg = new DevPlayConfig
            {
                setup = "Shifters.SmokeTests.Run",
                seconds = 4f,
                shotsDir = Path.Combine(_shotsBase, name),
                resultPath = ResultPath,
            };
            Directory.CreateDirectory(cfg.shotsDir);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            File.WriteAllText(Path.GetFullPath("Temp/devplay.json"), JsonUtility.ToJson(cfg));
            Debug.Log("[SMOKE] >>> " + path);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            string name = Path.GetFileNameWithoutExtension(_scenes[_index]);
            if (!File.Exists(ResultPath)) Record(name, false, "no result file (harness did not finish)");
            else
            {
                var r = JsonUtility.FromJson<DevPlayResult>(File.ReadAllText(ResultPath));
                bool ok = r.errors + r.exceptions + r.checksFailed == 0 && r.setupCompleted;
                var bad = new List<string>();
                foreach (var m in r.messages)
                    if (m.StartsWith("FAIL") || m.StartsWith("ERROR") || m.StartsWith("EXCEPTION")) { if (bad.Count < 6) bad.Add(m); }
                Record(name, ok, "errors=" + r.errors + " exceptions=" + r.exceptions + " checks " + r.checksPassed + " ok / " + r.checksFailed + " failed"
                                 + (bad.Count > 0 ? " | " + string.Join(" | ", bad) : ""));
            }
            _index++;
            EditorApplication.delayCall += StartNext;
        }

        static void Record(string scene, bool ok, string detail)
        {
            if (!ok) _failures++;
            string line = "[SMOKE] SUMMARY " + scene + ": " + (ok ? "PASS" : "FAIL") + " - " + detail;
            _summary.Add(line);
            Debug.Log(line);
        }

        static void Finish()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("[SMOKE] SUMMARY TOTAL " + (_scenes.Count - _failures) + "/" + _scenes.Count + " scenes clean");
            try { File.WriteAllText(Path.Combine(_shotsBase, "smoke_summary.txt"), string.Join("\n", _summary)); } catch (Exception) { }
            try { Finished?.Invoke(); } catch (Exception e) { Debug.LogWarning("[SMOKE] finish hook: " + e.Message); }
            EditorApplication.Exit(_failures == 0 ? 0 : 2);
        }
    }
}
