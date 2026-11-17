// =====================================================================================
//  SHIFTERS - ShiftersBuildSettings.cs   (BUILDER module - editor pipeline)
//  Build Settings = MainMenu, L1..L5 (LevelCatalog order, scenes that exist), then the pre-existing
//  Assets/Scenes/SampleScene.unity DISABLED, then any other user entries unchanged.
//  Sandbox / gallery scenes are never added.
// =====================================================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class ShiftersBuildSettings
    {
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        /// <summary>The desired scene list given the scenes that currently exist on disk.</summary>
        public static List<EditorBuildSettingsScene> Desired()
        {
            var list = new List<EditorBuildSettingsScene>();
            var ours = new HashSet<string>();
            void Add(string path, bool enabled)
            {
                ours.Add(path);
                if (File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, enabled));
            }
            Add(LevelCatalog.ScenePath(LevelCatalog.MainMenuScene), true);
            foreach (var l in LevelCatalog.Levels) Add(LevelCatalog.ScenePath(l.SceneName), true);
            Add(SampleScenePath, false);
            foreach (var s in EditorBuildSettings.scenes)
                if (!ours.Contains(s.path) && !list.Exists(x => x.path == s.path)) list.Add(new EditorBuildSettingsScene(s.path, s.enabled));
            return list;
        }

        /// <summary>True if Build Settings already match Desired() (paths + enabled flags, in order).</summary>
        public static bool IsCorrect()
        {
            var want = Desired();
            var have = EditorBuildSettings.scenes;
            if (want.Count != have.Length) return false;
            for (int i = 0; i < want.Count; i++)
                if (want[i].path != have[i].path || want[i].enabled != have[i].enabled) return false;
            return true;
        }

        /// <summary>Applies Desired() if needed. Returns true if Build Settings changed.</summary>
        public static bool Apply()
        {
            if (IsCorrect()) return false;
            var want = Desired();
            EditorBuildSettings.scenes = want.ToArray();
            var names = new List<string>();
            foreach (var s in want) names.Add(Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : " (off)"));
            Debug.Log("[SHIFTERS/Build] Build Settings: " + string.Join(", ", names));
            return true;
        }
    }
}
