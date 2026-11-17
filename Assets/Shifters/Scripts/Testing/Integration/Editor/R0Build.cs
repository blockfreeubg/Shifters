// =====================================================================================
//  R0Build (integrator2) — builds the R0 "Story wrapper" project: MainMenu + L1..L5 (+ sandbox/gallery, never in
//  Build Settings) and removes every other catalog scene from THIS project so Build Settings list exactly the shipped
//  scenes and the campaign routes past unbuilt chapters (LevelCatalog.HasScene).
//  Batch: -executeMethod Shifters.EditorTools.R0Build.Run [-r0Levels 0,1,2,3,4] -quit
//  Settings only (no rebuild): -executeMethod Shifters.EditorTools.R0Build.PruneOnly -quit
// =====================================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Shifters.EditorTools
{
    public static class R0Build
    {
        static HashSet<int> Shipped()
        {
            var set = new HashSet<int> { 0, 1, 2, 3, 4 };
            string arg = ShiftersEditorSteps.Arg("-r0Levels");
            if (!string.IsNullOrEmpty(arg))
            {
                set.Clear();
                foreach (var p in arg.Split(',')) if (int.TryParse(p.Trim(), out int i)) set.Add(i);
            }
            return set;
        }

        public static void Run()
        {
            var sw = Stopwatch.StartNew();
            var shipped = Shipped();
            var sb = new StringBuilder("R0 BUILD REPORT (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n");
            int failures = 0;
            sb.Append("TMP Essentials: ").Append(ShiftersEditorSteps.EnsureTmpEssentials() ? "ok" : "MISSING").Append('\n');
            sb.Append("Fonts: ").Append(ShiftersEditorSteps.RunExternalStep("FontAssetBuilder", "BuildAll")).Append('\n');
            sb.Append("Materials: ").Append(ShiftersEditorSteps.RunExternalStep("MaterialBaker", "BakeAll")).Append('\n');
            AssetDatabase.SaveAssets();
            ShiftersEditorSteps.ResetRuntimeCaches();
            ShiftersGeneratedAssets.ResetStats();
            try
            {
                using (ShiftersGeneratedAssets.InstallHook())
                {
                    sb.Append("Prefabs:\n").Append(ShiftersPrefabBuilder.BuildAll(out _, out int pf));
                    if (pf > 0) sb.Append("  (prefab failures: ").Append(pf).Append(", kit issues, not fatal)\n");
                    var menu = ShiftersSceneBuilder.BuildMainMenu();
                    sb.Append("Scene MainMenu:\n").Append(menu.Report);
                    if (!menu.Saved) failures++;
                    var list = new List<ILevelBuilder>();
                    foreach (var b in LevelBuilderRegistry.All) if (!LevelBuilderRegistry.IsCatalogLevel(b)) list.Add(b);
                    foreach (var b in LevelBuilderRegistry.All)
                        if (LevelBuilderRegistry.IsCatalogLevel(b) && shipped.Contains(b.LevelIndex)) list.Add(b);
                    foreach (var b in list)
                    {
                        var r = ShiftersSceneBuilder.BuildLevelScene(b);
                        sb.Append("Scene ").Append(r.SceneName).Append(":\n").Append(r.Report);
                        if (!r.Saved) failures++;
                    }
                }
            }
            catch (Exception e) { failures++; sb.Append("FATAL: ").Append(e).Append('\n'); Debug.LogException(e); }
            sb.Append(Prune(shipped));
            sb.Append("Generated assets: ").Append(ShiftersGeneratedAssets.Stats).Append('\n');
            sb.Append("Done in ").Append(sw.Elapsed.TotalSeconds.ToString("0.0")).Append(" s, failures: ").Append(failures).Append('\n');
            Finish(sb.ToString(), failures);
        }

        public static void PruneOnly() => Finish(Prune(Shipped()), 0);

        /// <summary>Deletes catalog scenes that are not shipped in this project, then applies Build Settings.</summary>
        static string Prune(HashSet<int> shipped)
        {
            var sb = new StringBuilder();
            foreach (var info in LevelCatalog.Levels)
            {
                if (shipped.Contains(info.Index) || string.IsNullOrEmpty(info.SceneName)) continue;
                string path = LevelCatalog.ScenePath(info.SceneName);
                if (File.Exists(path)) { AssetDatabase.DeleteAsset(path); sb.Append("Removed unshipped scene ").Append(path).Append('\n'); }
            }
            AssetDatabase.Refresh();
            ShiftersBuildSettings.Apply();
            AssetDatabase.SaveAssets();
            ShiftersEditorSteps.ResetRuntimeCaches();
            var parts = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) parts.Add(Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : "(off)"));
            sb.Append("R0 BUILD SETTINGS: ").Append(string.Join(", ", parts)).Append('\n');
            for (int i = 0; i < LevelCatalog.Count; i++)
                sb.Append("  HasScene[").Append(i).Append("] ").Append(LevelCatalog.Get(i)?.SceneName).Append(" = ").Append(LevelCatalog.HasScene(i)).Append('\n');
            return sb.ToString();
        }

        static void Finish(string report, int failures)
        {
            ShiftersGeneratedAssets.Cleanup();
            try { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/R0BuildReport.txt", report); } catch { }
            if (failures > 0) Debug.LogError("[R0BUILD] " + report); else Debug.Log("[R0BUILD] " + report);
            if (failures > 0 && Application.isBatchMode) throw new Exception("R0 build finished with " + failures + " failure(s)");
        }
    }
}
#endif
