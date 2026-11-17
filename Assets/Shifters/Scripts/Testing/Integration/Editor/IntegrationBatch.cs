// =====================================================================================
//  SHIFTERS - IntegrationBatch.cs   [INTEGRATION - editor batch entry point]
//  Runs the same DevPlay scenario in N CONSECUTIVE Play Mode sessions inside ONE editor launch (domain reload is
//  disabled, so this proves that every static is reset on SubsystemRegistration and that the game boots twice):
//    unity_batch.sh <proj> int_twice -executeMethod Shifters.EditorTools.IntegrationBatch.Repeat
//        [-devSetup Shifters.EndToEnd.Run] [-devScene path] [-devRounds 2] [-devShots dir]
//  Prints "[INTEGRATION] round N: ..." per session and exits 0 only if every round was clean.
// =====================================================================================
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class IntegrationBatch
    {
        static int _round, _rounds, _failures;
        static string _setup, _scene, _shots;
        static readonly StringBuilder _summary = new StringBuilder();

        static string Arg(string name, string def = "")
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return def;
        }

        public static void Repeat()
        {
            ShaderUtil.allowAsyncCompilation = false;
            _setup = Arg("-devSetup", "Shifters.EndToEnd.Run");
            _scene = Arg("-devScene", "");
            _shots = Arg("-devShots", Path.GetFullPath("Temp/int_shots"));
            _rounds = Mathf.Max(1, int.Parse(Arg("-devRounds", "2")));
            _round = 0; _failures = 0; _summary.Length = 0;
            EditorApplication.playModeStateChanged -= OnChanged;
            EditorApplication.playModeStateChanged += OnChanged;
            StartRound();
        }

        static string ResultPath(int round) => Path.GetFullPath("Temp/int_round" + round + ".json");

        static void StartRound()
        {
            _round++;
            var cfg = new DevPlayConfig { setup = _setup, seconds = 1f, shotsDir = Path.Combine(_shots, "round" + _round), resultPath = ResultPath(_round) };
            if (!string.IsNullOrEmpty(_scene)) EditorSceneManager.OpenScene(_scene, OpenSceneMode.Single);
            else DevPlayRunner.CreateDefaultScene();
            File.WriteAllText(Path.GetFullPath("Temp/devplay.json"), JsonUtility.ToJson(cfg));
            if (File.Exists(cfg.resultPath)) File.Delete(cfg.resultPath);
            Debug.Log("[INTEGRATION] round " + _round + "/" + _rounds + ": entering play mode (" + _setup + ")");
            EditorApplication.EnterPlaymode();
        }

        static void OnChanged(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            string line;
            var path = ResultPath(_round);
            if (File.Exists(path))
            {
                var r = JsonUtility.FromJson<DevPlayResult>(File.ReadAllText(path));
                bool ok = r.errors + r.exceptions + r.checksFailed == 0 && r.setupCompleted;
                if (!ok) _failures++;
                line = "round " + _round + ": " + (ok ? "CLEAN" : "FAILED") + " errors=" + r.errors + " exceptions=" + r.exceptions
                       + " checksFailed=" + r.checksFailed + " checksPassed=" + r.checksPassed + " setupCompleted=" + r.setupCompleted;
                foreach (var m in r.messages) if (m.StartsWith("FAIL") || m.StartsWith("ERROR") || m.StartsWith("EXCEPTION")) line += "\n    " + m;
            }
            else { _failures++; line = "round " + _round + ": NO RESULT FILE"; }
            _summary.AppendLine(line);
            Debug.Log("[INTEGRATION] " + line);
            if (_round < _rounds) { EditorApplication.delayCall += StartRound; return; }
            EditorApplication.playModeStateChanged -= OnChanged;
            Debug.Log("[INTEGRATION] SUMMARY " + (_failures == 0 ? "ALL ROUNDS CLEAN" : _failures + " round(s) failed") + "\n" + _summary);
            EditorApplication.Exit(_failures == 0 ? 0 : 2);
        }
    }
}
