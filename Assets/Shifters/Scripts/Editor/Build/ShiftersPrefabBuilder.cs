// =====================================================================================
//  SHIFTERS - ShiftersPrefabBuilder.cs   (BUILDER module - editor pipeline)
//  One default instance of every kit factory (KitCatalog.Discover) saved as
//  Assets/Shifters/Prefabs/<Category>/<Method>.prefab (categories Puzzles, Props, Hazards, Environment, Enemies,
//  Weapons, Level), plus Prefabs/Characters/Character.prefab built by CharacterFactory from a fixed seed
//  (inspection only - the game spawns characters from code). Prefabs are overwritten in place (GUIDs kept);
//  every in-memory mesh/material/texture they reference is persisted first (prefabs cannot embed them).
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class ShiftersPrefabBuilder
    {
        public const string PrefabRoot = "Assets/Shifters/Prefabs";
        public const int CharacterPreviewSeed = 2026;

        /// <summary>Builds all prefabs in a temporary (unsaved) scene. Returns a report; failures are listed, not thrown.</summary>
        public static string BuildAll(out int saved, out int failed)
        {
            saved = 0; failed = 0;
            var sb = new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Kit.Theme = LevelTheme.Training;
            var used = new HashSet<string>();
            var factories = KitCatalog.Discover();
            foreach (var f in factories)
            {
                var slot = new GameObject(f.Name);
                KitCatalog.SpawnDefault(f, slot.transform, out string error);
                if (error != null)
                {
                    failed++;
                    sb.Append("    prefab FAILED ").Append(f.Id).Append(": ").Append(error).Append('\n');
                    UnityEngine.Object.DestroyImmediate(slot);
                    continue;
                }
                string path = PrefabRoot + "/" + f.Category + "/" + f.Name + ".prefab";
                if (!used.Add(path)) { path = PrefabRoot + "/" + f.Category + "/" + f.Kit.Replace('.', '_') + "_" + f.Name + ".prefab"; used.Add(path); }
                if (Save(slot, path)) saved++; else { failed++; sb.Append("    prefab SAVE FAILED ").Append(path).Append('\n'); }
            }
            if (BuildCharacterPreview(sb)) saved++; else failed++;
            sb.Insert(0, "  prefabs: " + saved + " saved, " + failed + " failed (" + factories.Count + " kit factories)\n");
            return sb.ToString();
        }

        static bool BuildCharacterPreview(StringBuilder sb)
        {
            GameObject go = null;
            try
            {
                var team = CharacterGenerator.GenerateTeam(CharacterPreviewSeed);
                var c = CharacterFactory.Create(team.Members[0], 0, Vector3.zero, Quaternion.identity);
                go = c.gameObject;
                go.name = "Character";
                return Save(go, PrefabRoot + "/Characters/Character.prefab");
            }
            catch (Exception e)
            {
                sb.Append("    Character.prefab FAILED: ").Append(e.GetType().Name).Append(": ").Append(e.Message).Append('\n');
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                return false;
            }
        }

        static bool Save(GameObject go, string path)
        {
            ShiftersGeneratedAssets.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            ShiftersGeneratedAssets.PersistEmbedded(new[] { go }, true);
            bool ok;
            try { PrefabUtility.SaveAsPrefabAsset(go, path, out ok); }
            catch (Exception e) { Debug.LogWarning("[SHIFTERS/Build] prefab " + path + ": " + e.Message); ok = false; }
            UnityEngine.Object.DestroyImmediate(go);
            return ok;
        }
    }
}
