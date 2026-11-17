// =====================================================================================
//  SHIFTERS - LevelSceneAssembler.cs   (BUILDER module - runtime level framework)
//  The single code path that turns an ILevelBuilder into a level inside the CURRENT scene:
//  theme -> lighting -> roots -> builder.Build(ctx) -> spawn check -> LevelManager -> main camera.
//  Used at runtime (LevelRuntimeBuilder / DevPlay) and by the editor scene builder (which then saves).
// =====================================================================================
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shifters
{
    public static class LevelSceneAssembler
    {
        /// <summary>Builds the level into the active scene and returns the context (check ctx.Failed).</summary>
        public static LevelBuildContext Assemble(ILevelBuilder builder)
        {
            var info = LevelBuilderRegistry.InfoFor(builder);
            float t0 = Time.realtimeSinceStartup;
            Kit.Theme = info.Theme;
            ApplyLighting(info.Theme);
            var ctx = new LevelBuildContext(builder, info);
            try
            {
                builder.Build(ctx);
            }
            catch (Exception e)
            {
                ctx.Failed = true;
                Debug.LogError("[LevelBuild " + info.SceneName + "] " + builder.GetType().Name + ".Build threw: " + e);
            }
            ctx.FinishBuild();
            ConfigureLevelManager(ctx);
            EnsureMainCamera(ctx.SpawnPosition, ctx.SpawnYaw, true);
            ctx.BuildSeconds = Time.realtimeSinceStartup - t0;
            return ctx;
        }

        // ------------------------------------------------------------------ lighting
        /// <summary>LevelLighting.Apply(theme); if the rendering module returns no sun, guarantees a basic one.</summary>
        public static Light ApplyLighting(LevelTheme theme)
        {
            Light sun = null;
            try { sun = LevelLighting.Apply(theme); }
            catch (Exception e) { Debug.LogWarning("[LevelBuild] LevelLighting.Apply threw: " + e.Message); }
            if (sun != null) return sun;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { RenderSettings.sun = l; return l; }
            var go = new GameObject("Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            go.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.27f, 0.27f);
            return sun;
        }

        // ------------------------------------------------------------------ camera
        /// <summary>Finds or creates the main camera (tag MainCamera, AudioListener, URP post-processing on,
        /// optional ShiftersCamera) and places it behind focus facing yaw.</summary>
        public static Camera EnsureMainCamera(Vector3 focus, float yaw, bool withShiftersCamera)
        {
            Camera cam = Camera.main;
            if (cam == null)
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>())
                    if (c.targetTexture == null && c.gameObject.activeInHierarchy) { cam = c; break; }
            if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.gameObject.tag = "MainCamera";
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 60f;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
            if (withShiftersCamera && cam.GetComponent<ShiftersCamera>() == null) cam.gameObject.AddComponent<ShiftersCamera>();
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            cam.transform.position = focus - fwd * 6.5f + Vector3.up * 3.2f;
            cam.transform.LookAt(focus + Vector3.up * 1.2f);
            return cam;
        }

        // ------------------------------------------------------------------ level manager
        /// <summary>Creates/configures the scene's LevelManager (perspective_level module) by reflection:
        /// LevelIndex + SpawnPoints. Falls back to LevelBootstrapFallback while LevelManager doesn't exist.</summary>
        public static Component ConfigureLevelManager(LevelBuildContext ctx)
        {
            var type = KitCatalog.FindType("LevelManager");
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                var existing = UnityEngine.Object.FindAnyObjectByType(type) as Component;
                GameObject go = existing != null ? existing.gameObject : new GameObject("LevelManager");
                bool wasActive = go.activeSelf;
                go.SetActive(false); // configure before Awake/OnEnable run at runtime
                var lm = existing != null ? existing : go.AddComponent(type);
                bool okIndex = SetMember(lm, ctx.ManagerLevelIndex, "LevelIndex", "levelIndex", "Index", "_levelIndex");
                bool okSpawns = SetMember(lm, ctx.SpawnPoints, "SpawnPoints", "spawnPoints", "StartSpawns", "Spawns", "_spawnPoints");
                if (!okIndex) ctx.Warn("LevelManager has no settable LevelIndex member");
                if (!okSpawns) ctx.Warn("LevelManager has no settable SpawnPoints member");
                var fb = go.GetComponent<LevelBootstrapFallback>();
                if (fb != null) Kit.SafeDestroy(fb);
                go.SetActive(wasActive || existing == null);
                return lm;
            }
            ctx.Warn("LevelManager type not found - added LevelBootstrapFallback (rebuild once the Level module is published)");
            var fgo = GameObject.Find("LevelManager (fallback)") ?? new GameObject("LevelManager (fallback)");
            fgo.SetActive(false);
            var f = fgo.GetComponent<LevelBootstrapFallback>() ?? fgo.AddComponent<LevelBootstrapFallback>();
            f.LevelIndex = ctx.ManagerLevelIndex;
            f.SpawnPoints = ctx.SpawnPoints;
            fgo.SetActive(true);
            return f;
        }

        /// <summary>Sets the first existing field/property among names (public or private instance) to value
        /// (loosely converted, e.g. Transform[] -> List&lt;Transform&gt;). Returns true on success.</summary>
        public static bool SetMember(object target, object value, params string[] names)
        {
            if (target == null) return false;
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (var t = target.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var n in names)
                {
                    var f = t.GetField(n, F | BindingFlags.DeclaredOnly);
                    if (f != null && !f.IsInitOnly && KitCatalog.TryConvert(value, f.FieldType, out var v, out _)) { f.SetValue(target, v); return true; }
                    var p = t.GetProperty(n, F | BindingFlags.DeclaredOnly);
                    if (p != null && p.CanWrite && p.GetIndexParameters().Length == 0 && KitCatalog.TryConvert(value, p.PropertyType, out var pv, out _))
                    {
                        try { p.SetValue(target, pv); return true; } catch { /* private setter on base etc. */ }
                    }
                }
            }
            return false;
        }
    }
}
