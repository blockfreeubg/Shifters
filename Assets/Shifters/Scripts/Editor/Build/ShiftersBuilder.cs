// =====================================================================================
//  SHIFTERS - ShiftersBuilder.cs   (BUILDER module - editor pipeline entry points)
//  Menu "SHIFTERS/Build Everything" or batchmode:
//    unity -batchmode -projectPath P -executeMethod Shifters.EditorTools.ShiftersBuilder.BuildAll -quit
//  Order: TMP Essentials -> FontAssetBuilder.BuildAll -> MaterialBaker.BakeAll -> clear caches -> install
//  ProcMesh.PersistHook -> prefabs -> MainMenu -> sandbox + gallery -> every catalog level with an ILevelBuilder
//  -> Build Settings -> remove hook -> SaveAssets -> report (console + Logs/ShiftersBuildReport.txt).
//  Single level:  -executeMethod Shifters.EditorTools.ShiftersBuilder.BuildLevel -devLevel <index|sandbox|gallery|all>
//  Idempotent: scenes/prefabs/assets are overwritten in place; generated assets get path-derived GUIDs.
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Shifters.EditorTools
{
    public static partial class ShiftersBuilder
    {
        public const string ReportPath = "Logs/ShiftersBuildReport.txt";

        /// <summary>Result of the last BuildAll/BuildLevel run (for tests).</summary>
        public static string LastReport { get; private set; } = "";
        public static int LastFailures { get; private set; }

        // ------------------------------------------------------------------ menus
        [MenuItem("SHIFTERS/Build Everything", false, 0)]
        static void BuildAllMenu() => Interactive(BuildAll);

        [MenuItem("SHIFTERS/Build Level/L1 Training Facility", false, 20)] static void MenuL1() => Interactive(() => BuildLevelIndex(0));
        [MenuItem("SHIFTERS/Build Level/L2 Weight Problem", false, 21)] static void MenuL2() => Interactive(() => BuildLevelIndex(1));
        [MenuItem("SHIFTERS/Build Level/L3 Sacrifice Zone", false, 22)] static void MenuL3() => Interactive(() => BuildLevelIndex(2));
        [MenuItem("SHIFTERS/Build Level/L4 IQ Test", false, 23)] static void MenuL4() => Interactive(() => BuildLevelIndex(3));
        [MenuItem("SHIFTERS/Build Level/L5 Total Chaos", false, 24)] static void MenuL5() => Interactive(() => BuildLevelIndex(4));
        [MenuItem("SHIFTERS/Build Level/L0 Sandbox", false, 40)] static void MenuSandbox() => Interactive(() => BuildLevelIndex(L0_Sandbox.Index));
        [MenuItem("SHIFTERS/Build Level/L0 Kit Gallery", false, 41)] static void MenuGallery() => Interactive(() => BuildLevelIndex(L0_KitGallery.Index));
        [MenuItem("SHIFTERS/Build Main Menu Scene", false, 50)] static void MenuMainMenu() => Interactive(BuildMainMenu);
        [MenuItem("SHIFTERS/Build Prefabs", false, 51)] static void MenuPrefabs() => Interactive(BuildPrefabs);
        [MenuItem("SHIFTERS/Apply Build Settings", false, 52)] static void MenuBuildSettings() => ShiftersBuildSettings.Apply();
        [MenuItem("SHIFTERS/Build Player (macOS)", false, 70)] static void MenuPlayer() => BuildPlayer();

        /// <summary>Menu wrapper: asks to save modified scenes, runs, then reopens the scene that was open.</summary>
        static void Interactive(Action action)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;
            try { action(); }
            finally
            {
                if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
        }

        // ------------------------------------------------------------------ full build
        /// <summary>Builds everything (see file header). Throws in batchmode if anything failed (non-zero exit).</summary>
        public static void BuildAll()
        {
            var sw = Stopwatch.StartNew();
            var sb = new StringBuilder("SHIFTERS BUILD REPORT  (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n");
            int failures = 0;
            sb.Append("TMP Essentials: ").Append(ShiftersEditorSteps.EnsureTmpEssentials() ? "ok" : "MISSING (import pending)").Append('\n');
            sb.Append("Fonts (FontAssetBuilder.BuildAll): ").Append(ShiftersEditorSteps.RunExternalStep("FontAssetBuilder", "BuildAll")).Append('\n');
            sb.Append("Materials (MaterialBaker.BakeAll): ").Append(ShiftersEditorSteps.RunExternalStep("MaterialBaker", "BakeAll")).Append('\n');
            AssetDatabase.SaveAssets();
            ShiftersEditorSteps.ResetRuntimeCaches();
            ShiftersGeneratedAssets.ResetStats();
            try
            {
                using (ShiftersGeneratedAssets.InstallHook())
                {
                    sb.Append("Prefabs:\n").Append(ShiftersPrefabBuilder.BuildAll(out _, out int prefabFails));
                    if (prefabFails > 0) sb.Append("  (prefab failures are kit issues, not fatal)\n");
                    var menu = ShiftersSceneBuilder.BuildMainMenu();
                    sb.Append("Scene MainMenu:\n").Append(menu.Report);
                    if (!menu.Saved) failures++;
                    foreach (var b in BuildersInOrder())
                    {
                        var r = ShiftersSceneBuilder.BuildLevelScene(b);
                        sb.Append("Scene ").Append(r.SceneName).Append(":\n").Append(r.Report);
                        if (!r.Saved) failures++;
                    }
                    foreach (var info in LevelCatalog.Levels)
                        if (LevelBuilderRegistry.Find(info.Index) == null)
                            sb.Append("Scene ").Append(info.SceneName).Append(": skipped (no ILevelBuilder with LevelIndex ").Append(info.Index).Append(" yet)\n");
                }
            }
            catch (Exception e) { failures++; sb.Append("FATAL: ").Append(e).Append('\n'); Debug.LogException(e); }
            ShiftersBuildSettings.Apply();
            AssetDatabase.SaveAssets();
            ShiftersEditorSteps.ResetRuntimeCaches();
            sb.Append("Generated assets: ").Append(ShiftersGeneratedAssets.Stats).Append('\n');
            sb.Append("Build Settings: ").Append(DescribeBuildSettings()).Append('\n');
            sb.Append("Done in ").Append(sw.Elapsed.TotalSeconds.ToString("0.0")).Append(" s, failures: ").Append(failures).Append('\n');
            Finish(sb.ToString(), failures);
        }

        /// <summary>Sandbox + gallery first, then catalog levels by index.</summary>
        static List<ILevelBuilder> BuildersInOrder()
        {
            var list = new List<ILevelBuilder>();
            foreach (var b in LevelBuilderRegistry.All) if (!LevelBuilderRegistry.IsCatalogLevel(b)) list.Add(b);
            foreach (var b in LevelBuilderRegistry.All) if (LevelBuilderRegistry.IsCatalogLevel(b)) list.Add(b);
            return list;
        }

        // ------------------------------------------------------------------ single steps
        /// <summary>Batchmode entry: -devLevel &lt;catalog index | 100/sandbox | 101/gallery | scene name | all&gt; (also -level).</summary>
        public static void BuildLevel()
        {
            string arg = ShiftersEditorSteps.Arg("-devLevel") ?? ShiftersEditorSteps.Arg("-level") ?? "";
            arg = arg.Trim();
            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase)) { BuildAll(); return; }
            int index = ParseLevelArg(arg);
            if (index == int.MinValue)
            {
                Finish("BuildLevel: unknown -devLevel '" + arg + "' (use a catalog index 0-" + (LevelCatalog.Count - 1) + ", L<n>, sandbox, gallery, a scene name, or all)\n", 1);
                return;
            }
            BuildLevelIndex(index);
        }

        static int ParseLevelArg(string arg)
        {
            if (int.TryParse(arg, out int i)) return i;
            if (arg.Equals("sandbox", StringComparison.OrdinalIgnoreCase) || arg == L0_Sandbox.SceneName) return L0_Sandbox.Index;
            if (arg.Equals("gallery", StringComparison.OrdinalIgnoreCase) || arg == L0_KitGallery.SceneName) return L0_KitGallery.Index;
            int idx = LevelCatalog.IndexOfScene(arg);
            if (idx >= 0) return idx;
            // L<n> (any n >= 1, e.g. L1, L6, L12): level n = catalog index n - 1
            if (arg.Length >= 2 && (arg[0] == 'L' || arg[0] == 'l') && int.TryParse(arg.Substring(1), out int n) && n >= 1) return n - 1;
            // L<n>_Anything that is not (yet) in the catalog under that exact name: still level n
            int us = arg.IndexOf('_');
            if (us >= 2 && (arg[0] == 'L' || arg[0] == 'l') && int.TryParse(arg.Substring(1, us - 1), out int m) && m >= 1) return m - 1;
            return int.MinValue;
        }

        /// <summary>Builds one level scene (catalog index 0-4, or L0_Sandbox.Index / L0_KitGallery.Index) with the mesh
        /// persist hook, then refreshes Build Settings. Bakes materials/fonts only if none exist yet. Returns success.</summary>
        public static bool BuildLevelIndex(int index)
        {
            var sw = Stopwatch.StartNew();
            var sb = new StringBuilder("SHIFTERS BUILD LEVEL " + index + "\n");
            var builder = LevelBuilderRegistry.Find(index);
            if (builder == null)
            {
                Finish("No ILevelBuilder with LevelIndex " + index + " (found: " + DescribeBuilders() + ")\n", 1);
                return false;
            }
            EnsureBaseAssets(sb);
            ShiftersEditorSteps.ResetRuntimeCaches();
            ShiftersGeneratedAssets.ResetStats();
            builder = LevelBuilderRegistry.Find(index); // fresh instance after the registry reset
            ShiftersSceneBuilder.Result r;
            using (ShiftersGeneratedAssets.InstallHook()) r = ShiftersSceneBuilder.BuildLevelScene(builder);
            sb.Append(r.Report);
            ShiftersBuildSettings.Apply();
            AssetDatabase.SaveAssets();
            ShiftersEditorSteps.ResetRuntimeCaches();
            sb.Append("Generated assets: ").Append(ShiftersGeneratedAssets.Stats).Append('\n');
            sb.Append("Done in ").Append(sw.Elapsed.TotalSeconds.ToString("0.0")).Append(" s -> ").Append(r.Saved ? r.Path : "NOT SAVED").Append('\n');
            Finish(sb.ToString(), r.Saved ? 0 : 1);
            return r.Saved;
        }

        /// <summary>Menu/CLI: rebuild only the MainMenu scene.</summary>
        public static void BuildMainMenu()
        {
            var sb = new StringBuilder("SHIFTERS BUILD MAIN MENU\n");
            EnsureBaseAssets(sb);
            ShiftersEditorSteps.ResetRuntimeCaches();
            ShiftersSceneBuilder.Result r;
            using (ShiftersGeneratedAssets.InstallHook()) r = ShiftersSceneBuilder.BuildMainMenu();
            sb.Append(r.Report);
            ShiftersBuildSettings.Apply();
            AssetDatabase.SaveAssets();
            Finish(sb.ToString(), r.Saved ? 0 : 1);
        }

        /// <summary>Menu/CLI: regenerate all prefabs.</summary>
        public static void BuildPrefabs()
        {
            var sb = new StringBuilder("SHIFTERS BUILD PREFABS\n");
            EnsureBaseAssets(sb);
            ShiftersEditorSteps.ResetRuntimeCaches();
            using (ShiftersGeneratedAssets.InstallHook()) sb.Append(ShiftersPrefabBuilder.BuildAll(out _, out _));
            AssetDatabase.SaveAssets();
            Finish(sb.ToString(), 0);
        }

        /// <summary>Bakes materials/fonts only when their output folders are empty (keeps GUIDs of existing bakes).</summary>
        static void EnsureBaseAssets(StringBuilder sb)
        {
            ShiftersEditorSteps.EnsureTmpEssentials();
            if (AssetDatabase.FindAssets("t:Material", new[] { "Assets/Shifters/Resources/Materials" }).Length == 0 && AssetDatabase.IsValidFolder("Assets/Shifters/Resources"))
                sb.Append("Materials: ").Append(ShiftersEditorSteps.RunExternalStep("MaterialBaker", "BakeAll")).Append('\n');
            if (!AssetDatabase.IsValidFolder("Assets/Shifters/Resources/Fonts") || AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Shifters/Resources/Fonts" }).Length == 0)
                sb.Append("Fonts: ").Append(ShiftersEditorSteps.RunExternalStep("FontAssetBuilder", "BuildAll")).Append('\n');
        }

        // ------------------------------------------------------------------ player
        /// <summary>macOS standalone build of the enabled Build Settings scenes (development off).
        /// Output: -buildPath &lt;path&gt; or env SHIFTERS_BUILD_PATH, default &lt;project&gt;/Builds/SHIFTERS.app.
        /// Makes StandaloneOSX the active platform first (e.g. after BuildWebGL): URP strips shader variants for the ACTIVE
        /// platform, and the build guard (ShiftersWebPostFxStripping) refuses player builds whose target is not active.</summary>
        public static void BuildPlayer()
        {
            ShiftersBuildSettings.Apply();
            string path = ShiftersEditorSteps.Arg("-buildPath") ?? Environment.GetEnvironmentVariable("SHIFTERS_BUILD_PATH")
                          ?? Path.GetFullPath("Builds/SHIFTERS.app");
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled && File.Exists(s.path)) scenes.Add(s.path);
            if (scenes.Count == 0) { Finish("BuildPlayer: no enabled scenes - run Build Everything first\n", 1); return; }
            if (!ShiftersWebBuildTarget.EnsureActive(BuildTarget.StandaloneOSX, out string targetNote))
            { Finish("BuildPlayer: " + targetNote + "\n", 1); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(), locationPathName = path, target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            string text = "SHIFTERS PLAYER BUILD: " + sum.result + " -> " + path + " (" + scenes.Count + " scenes, " + (sum.totalSize / (1024 * 1024)) + " MB, "
                          + sum.totalErrors + " errors, " + sum.totalWarnings + " warnings, " + sum.totalTime.TotalSeconds.ToString("0") + " s)\n"
                          + "Platform: " + targetNote + "\n";
            Finish(text, sum.result == BuildResult.Succeeded ? 0 : 1);
        }

        // ------------------------------------------------------------------ helpers
        static void Finish(string report, int failures)
        {
            LastReport = report;
            LastFailures = failures;
            ShiftersGeneratedAssets.Cleanup();
            try { Directory.CreateDirectory("Logs"); File.WriteAllText(ReportPath, report); } catch { }
            if (failures > 0) Debug.LogError("[SHIFTERS/Build] " + report);
            else Debug.Log("[SHIFTERS/Build] " + report);
            if (failures > 0 && Application.isBatchMode) throw new Exception("SHIFTERS build finished with " + failures + " failure(s) - see " + ReportPath);
        }

        static string DescribeBuildSettings()
        {
            var parts = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) parts.Add(Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : "(off)"));
            return string.Join(", ", parts);
        }

        static string DescribeBuilders()
        {
            var parts = new List<string>();
            foreach (var b in LevelBuilderRegistry.All) parts.Add(b.GetType().Name + "=" + b.LevelIndex);
            return parts.Count > 0 ? string.Join(", ", parts) : "none";
        }
    }
}
