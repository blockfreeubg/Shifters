// =====================================================================================
//  SHIFTERS - MaterialBaker.cs   (RENDERING - editor)
//  Bakes the whole material library into GUID-stable assets:
//    Assets/Shifters/Resources/Materials/<Theme>_<Key>.mat   (every LevelTheme x MatKey)
//    Assets/Shifters/Resources/Materials/Sky_<Theme>.mat     (Shifters/GradientSky per theme)
//    Assets/Shifters/Resources/Materials/Fx_*.mat            (unlit glow / particle helpers; keeps the Fx shaders
//                                                             in player builds)
//  Assets are written through ShiftersGeneratedAssets.SaveAsset (BUILDER), so every clone produces the SAME
//  GUIDs (md5 of the path) and re-baking updates files in place (idempotent, no GUID churn).
//  Menu: SHIFTERS/Bake Materials.   Batch: -executeMethod Shifters.EditorTools.MaterialBaker.BakeAll -quit
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shifters.EditorTools
{
    public static class MaterialBaker
    {
        public const string Folder = "Assets/Shifters/Resources/Materials";

        /// <summary>Number of assets written/checked by the last BakeAll (themed + sky + fx).</summary>
        public static int LastCount { get; private set; }

        [MenuItem("SHIFTERS/Bake Materials", false, 30)]
        static void BakeMenu()
        {
            BakeAll();
            EditorUtility.DisplayDialog("SHIFTERS", "Baked " + LastCount + " materials into " + Folder, "OK");
        }

        /// <summary>Bakes every themed material, the theme skies and the Fx helper materials. Safe to call repeatedly.</summary>
        public static void BakeAll()
        {
            float t0 = Time.realtimeSinceStartup;
            int failed = 0, count = 0;
            var paths = new List<string>(300);
            MaterialLibrary.ClearCache();
            try
            {
                EnsureFolder(Folder);
                var lit = Shader.Find(MaterialLibrary.LitShaderName);
                if (lit == null) Debug.LogWarning("[SHIFTERS/Materials] Shifters/StylizedLit not found - baking with fallbacks");

                foreach (LevelTheme theme in Enum.GetValues(typeof(LevelTheme)))
                {
                    foreach (MatKey key in Enum.GetValues(typeof(MatKey)))
                    {
                        string path = Folder + "/" + theme + "_" + key + ".mat";
                        if (Save(MaterialLibrary.Build(key, theme), path)) { count++; paths.Add(path); } else failed++;
                    }
                    string sky = Folder + "/Sky_" + theme + ".mat";
                    if (Save(LevelLighting.CreateSkyMaterial(theme), sky)) { count++; paths.Add(sky); } else failed++;
                }
                foreach (var id in MaterialLibrary.SpecialIds)
                {
                    string path = Folder + "/" + id + ".mat";
                    if (Save(MaterialLibrary.BuildSpecial(id), path)) { count++; paths.Add(path); } else failed++;
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                MaterialLibrary.ClearCache();
            }
            LastCount = count;
            Debug.Log("[SHIFTERS/Materials] baked " + count + " materials (" + failed + " failed) in " +
                      (Time.realtimeSinceStartup - t0).ToString("0.0") + " s -> " + Folder + "  (" + ShiftersGeneratedAssets.Stats + ")");
            if (failed > 0) Debug.LogError("[SHIFTERS/Materials] " + failed + " materials failed to bake");
        }

        /// <summary>Persists a freshly built material at path (deterministic GUID) and destroys the temporary.</summary>
        static bool Save(Material m, string path)
        {
            if (m == null) { Debug.LogError("[SHIFTERS/Materials] null material for " + path); return false; }
            try
            {
                m.name = System.IO.Path.GetFileNameWithoutExtension(path);
                var saved = ShiftersGeneratedAssets.SaveAsset(m, path);
                if (saved == null) { Debug.LogError("[SHIFTERS/Materials] could not save " + path); return false; }
                if (saved.shader == null || saved.shader.name.StartsWith("Hidden/InternalErrorShader"))
                {
                    Debug.LogError("[SHIFTERS/Materials] " + path + " has no valid shader");
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[SHIFTERS/Materials] " + path + ": " + e.Message);
                return false;
            }
            finally
            {
                if (!EditorUtility.IsPersistent(m)) Object.DestroyImmediate(m);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        /// <summary>Batch check used by tests: every baked material loads from Resources with a supported shader.</summary>
        public static int VerifyBaked()
        {
            int ok = 0;
            foreach (LevelTheme theme in Enum.GetValues(typeof(LevelTheme)))
                foreach (MatKey key in Enum.GetValues(typeof(MatKey)))
                {
                    var m = Resources.Load<Material>("Materials/" + theme + "_" + key);
                    if (m != null && m.shader != null && m.shader.isSupported) ok++;
                    else Debug.LogWarning("[SHIFTERS/Materials] missing/invalid baked material " + theme + "_" + key);
                }
            Debug.Log("[SHIFTERS/Materials] verify: " + ok + " valid baked theme materials");
            return ok;
        }
    }
}
