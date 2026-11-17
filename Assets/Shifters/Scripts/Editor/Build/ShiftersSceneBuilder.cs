// =====================================================================================
//  SHIFTERS - ShiftersSceneBuilder.cs   (BUILDER module - editor pipeline)
//  Builds scene files: level scenes from ILevelBuilders (via the runtime LevelSceneAssembler - identical to
//  LevelRuntimeBuilder) and the MainMenu scene. Realtime lighting only (shared LightingSettings asset with
//  baked + realtime GI off; nothing is baked). Scenes are saved in place (GUIDs kept).
// =====================================================================================
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters.EditorTools
{
    public static class ShiftersSceneBuilder
    {
        public const string LightingSettingsPath = "Assets/Shifters/Scenes/ShiftersRealtimeLighting.lighting";
        public const string MenuStageMarker = "MenuStage";

        public class Result
        {
            public string SceneName, Path, Report;
            public bool Saved;
            public LevelBuildContext Context;
            public ShiftersBuildReport.Stats Stats;
        }

        /// <summary>Builds and saves the scene of one ILevelBuilder. Never saves a scene whose Build threw.</summary>
        public static Result BuildLevelScene(ILevelBuilder builder)
        {
            var info = LevelBuilderRegistry.InfoFor(builder);
            var res = new Result { SceneName = info.SceneName, Path = LevelCatalog.ScenePath(info.SceneName) };
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplyLightingSettings();
            res.Context = LevelSceneAssembler.Assemble(builder);
            if (res.Context.Failed)
            {
                res.Report = "  [" + info.SceneName + "] BUILD FAILED (builder threw) - scene NOT saved\n" + res.Context.Report();
                return res;
            }
            ShiftersGeneratedAssets.PersistEmbedded(scene.GetRootGameObjects(), false);
            res.Saved = Save(scene, res.Path);
            res.Stats = ShiftersBuildReport.Scan(info.SceneName, scene.GetRootGameObjects());
            res.Report = res.Context.Report() + ShiftersBuildReport.Format(res.Stats) + (res.Saved ? "" : "  SAVE FAILED: " + res.Path + "\n");
            return res;
        }

        /// <summary>MainMenu: Menu-theme lighting, main camera (no ShiftersCamera; the UI drives the menu camera/stage),
        /// and an empty "MenuStage" marker at the origin where the UI builds its runtime stage.</summary>
        public static Result BuildMainMenu()
        {
            var res = new Result { SceneName = LevelCatalog.MainMenuScene, Path = LevelCatalog.ScenePath(LevelCatalog.MainMenuScene) };
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplyLightingSettings();
            Kit.Theme = LevelTheme.Menu;
            LevelSceneAssembler.ApplyLighting(LevelTheme.Menu);
            var cam = LevelSceneAssembler.EnsureMainCamera(Vector3.zero, 0f, false);
            cam.transform.position = new Vector3(0f, 1.7f, -6.5f);
            cam.transform.LookAt(new Vector3(0f, 1.1f, 0f));
            cam.fieldOfView = 40f;
            new GameObject(MenuStageMarker);
            ShiftersGeneratedAssets.PersistEmbedded(scene.GetRootGameObjects(), false);
            res.Saved = Save(scene, res.Path);
            res.Stats = ShiftersBuildReport.Scan(res.SceneName, scene.GetRootGameObjects());
            res.Report = ShiftersBuildReport.Format(res.Stats) + (res.Saved ? "" : "  SAVE FAILED: " + res.Path + "\n");
            Kit.Theme = LevelTheme.Training;
            return res;
        }

        static bool Save(Scene scene, string path)
        {
            ShiftersGeneratedAssets.EnsureFolder(LevelCatalog.ScenesFolder);
            try { return EditorSceneManager.SaveScene(scene, path); }
            catch (Exception e) { Debug.LogError("[SHIFTERS/Build] saving " + path + " failed: " + e); return false; }
        }

        /// <summary>Assigns the shared realtime-only LightingSettings asset (created once, deterministic GUID).</summary>
        public static void ApplyLightingSettings()
        {
            try
            {
                var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
                if (ls == null)
                {
                    var fresh = new LightingSettings { name = "ShiftersRealtimeLighting", bakedGI = false, realtimeGI = false };
                    ls = ShiftersGeneratedAssets.SaveAsset(fresh, LightingSettingsPath);
                    UnityEngine.Object.DestroyImmediate(fresh);
                }
                if (ls != null) Lightmapping.lightingSettings = ls;
            }
            catch (Exception e) { Debug.LogWarning("[SHIFTERS/Build] lighting settings: " + e.Message); }
        }
    }
}
