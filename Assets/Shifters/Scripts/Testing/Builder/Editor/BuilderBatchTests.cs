// =====================================================================================
//  SHIFTERS - BuilderBatchTests.cs   (BUILDER module editor tests, batchmode)
//   -executeMethod Shifters.EditorTools.BuilderBatchTests.RebuildAndVerify
//  Snapshots the .meta GUIDs of everything the builder generates, runs ShiftersBuilder.BuildAll again and
//  verifies: no GUID changed or disappeared, expected scenes/prefabs/meshes exist, generated assets carry their
//  path-derived GUIDs, saved scenes embed no meshes, Build Settings order. Exits 0 (pass) / 1 (fail).
// =====================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class BuilderBatchTests
    {
        static readonly string[] Roots = { "Assets/Shifters/Scenes", "Assets/Shifters/Prefabs", ShiftersGeneratedAssets.MeshFolder };

        public static void RebuildAndVerify()
        {
            var log = new StringBuilder("[BUILDERTEST]\n");
            int fails = 0;
            void Check(bool ok, string what) { log.Append(ok ? "  PASS " : "  FAIL ").Append(what).Append('\n'); if (!ok) fails++; }

            var before = SnapshotGuids();
            log.Append("  generated .meta files before rebuild: ").Append(before.Count).Append('\n');
            // delete a few generated meshes: the rebuild must recreate them with the SAME GUID (path-derived)
            var deleted = new List<string>();
            if (Directory.Exists(ShiftersGeneratedAssets.MeshFolder))
                foreach (var f in Directory.GetFiles(ShiftersGeneratedAssets.MeshFolder, "box_*.asset"))
                {
                    if (deleted.Count >= 3) break;
                    var p = f.Replace('\\', '/');
                    File.Delete(p); File.Delete(p + ".meta"); deleted.Add(p);
                }
            AssetDatabase.Refresh();
            log.Append("  deleted for re-creation: ").Append(string.Join(", ", deleted.ConvertAll(Path.GetFileName))).Append('\n');
            Exception buildError = null;
            try { ShiftersBuilder.BuildAll(); } catch (Exception e) { buildError = e; }
            Check(buildError == null, "BuildAll (second run) finished without failures" + (buildError != null ? ": " + buildError.Message : ""));
            var after = SnapshotGuids();

            int changed = 0, missing = 0;
            foreach (var kv in before)
            {
                if (!after.TryGetValue(kv.Key, out var g)) { missing++; if (missing <= 5) log.Append("    missing after rebuild: ").Append(kv.Key).Append('\n'); }
                else if (g != kv.Value) { changed++; if (changed <= 5) log.Append("    GUID changed: ").Append(kv.Key).Append('\n'); }
            }
            int added = 0; foreach (var k in after.Keys) if (!before.ContainsKey(k)) added++;
            Check(before.Count > 0, "first build produced generated assets (" + before.Count + ")");
            Check(changed == 0, "no GUID churn (" + changed + " changed)");
            Check(missing == 0, "no generated asset disappeared (" + missing + " missing)");
            Check(added == 0, "second build created no new assets (" + added + " added)");
            log.Append("  ").Append(ShiftersGeneratedAssets.Stats).Append('\n');
            Check(ShiftersGeneratedAssets.Created == deleted.Count, "second build re-created exactly the " + deleted.Count + " deleted assets (created " + ShiftersGeneratedAssets.Created + ")");
            foreach (var d in deleted)
            {
                Check(after.TryGetValue(d, out var g) && before.TryGetValue(d, out var g0) && g == g0, "re-created " + Path.GetFileName(d) + " kept its GUID");
                Check(File.Exists(d) && File.ReadAllText(d).Contains("--- !u!43 &4300000"), "re-created " + Path.GetFileName(d) + " uses the canonical mesh fileID 4300000");
            }

            foreach (var scene in new[] { LevelCatalog.MainMenuScene, L0_Sandbox.SceneName, L0_KitGallery.SceneName })
                Check(File.Exists(LevelCatalog.ScenePath(scene)), "scene exists: " + scene);
            int meshes = Directory.Exists(ShiftersGeneratedAssets.MeshFolder) ? Directory.GetFiles(ShiftersGeneratedAssets.MeshFolder, "*.asset", SearchOption.AllDirectories).Length : 0;
            Check(meshes > 0, "mesh assets generated (" + meshes + ")");
            int prefabs = Directory.Exists(ShiftersPrefabBuilder.PrefabRoot) ? Directory.GetFiles(ShiftersPrefabBuilder.PrefabRoot, "*.prefab", SearchOption.AllDirectories).Length : 0;
            log.Append("  prefabs on disk: ").Append(prefabs).Append(" (kit factories found: ").Append(KitCatalog.Discover().Count).Append(")\n");
            Check(File.Exists(ShiftersPrefabBuilder.PrefabRoot + "/Characters/Character.prefab"), "Character.prefab exists");

            int wrongGuid = 0;
            foreach (var kv in after)
            {
                if (!kv.Key.StartsWith(ShiftersGeneratedAssets.MeshFolder) || !kv.Key.EndsWith(".asset")) continue;
                if (kv.Value != ShiftersGeneratedAssets.DeterministicGuid(kv.Key)) { wrongGuid++; if (wrongGuid <= 3) log.Append("    non-deterministic GUID: ").Append(kv.Key).Append('\n'); }
            }
            Check(wrongGuid == 0, "every generated mesh asset has its path-derived GUID");

            var allGuids = new HashSet<string>(after.Values);
            var refRx = new Regex("m_Mesh: \\{fileID: (-?\\d+), guid: ([0-9a-f]{32})");
            foreach (var f in Directory.GetFiles(LevelCatalog.ScenesFolder, "*.unity"))
            {
                int embedded = 0, refs = 0, broken = 0;
                foreach (var line in File.ReadLines(f))
                {
                    if (line.StartsWith("--- !u!43 ")) embedded++;
                    var m = refRx.Match(line);
                    if (!m.Success) continue;
                    refs++;
                    string guid = m.Groups[2].Value;
                    bool builtin = guid == "0000000000000000e000000000000000" || guid == "0000000000000000f000000000000000";
                    if (!builtin && !allGuids.Contains(guid) && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) broken++;
                }
                Check(embedded == 0, Path.GetFileName(f) + " embeds no meshes (" + embedded + ")");
                Check(broken == 0, Path.GetFileName(f) + ": all " + refs + " mesh references resolve (" + broken + " broken)");
            }

            var bs = EditorBuildSettings.scenes;
            Check(bs.Length > 0 && bs[0].path == LevelCatalog.ScenePath(LevelCatalog.MainMenuScene) && bs[0].enabled, "Build Settings: MainMenu first and enabled");
            bool sampleOff = false, sandboxIn = false;
            foreach (var s in bs) { if (s.path == ShiftersBuildSettings.SampleScenePath) sampleOff = !s.enabled; if (s.path.Contains("L0_")) sandboxIn = true; }
            Check(sampleOff, "Build Settings: SampleScene kept but disabled");
            Check(!sandboxIn, "Build Settings: no sandbox/gallery scenes");
            Check(ShiftersBuildSettings.IsCorrect(), "ShiftersBuildSettings.IsCorrect() after build");

            log.Append(fails == 0 ? "[BUILDERTEST] ALL PASSED\n" : "[BUILDERTEST] FAILURES: " + fails + "\n");
            Debug.Log(log.ToString());
            File.WriteAllText("Logs/BuilderBatchTests.txt", log.ToString());
            EditorApplication.Exit(fails == 0 ? 0 : 1);
        }

        static Dictionary<string, string> SnapshotGuids()
        {
            var map = new Dictionary<string, string>();
            var rx = new Regex("^guid: ([0-9a-fA-F]{32})", RegexOptions.Multiline);
            foreach (var root in Roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
                {
                    string asset = meta.Substring(0, meta.Length - 5).Replace('\\', '/');
                    if (Directory.Exists(asset)) continue; // folders
                    var m = rx.Match(File.ReadAllText(meta));
                    if (m.Success) map[asset] = m.Groups[1].Value.ToLowerInvariant();
                }
            }
            return map;
        }
    }
}
