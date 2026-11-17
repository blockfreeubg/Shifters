// =====================================================================================
//  SHIFTERS - LevelLighting.cs   [CONTRACT API - implementation owned by the RENDERING agent]
//  Applies a theme's look: sun light (color/angle/intensity/soft shadows), ambient trilight,
//  fog, background/sky gradient. Used by the editor builder (baked into scenes) and at runtime.
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Shifters
{
    public static partial class LevelLighting
    {
        /// <summary>Theme applied by the last Apply() call.</summary>
        public static LevelTheme Current { get; private set; } = LevelTheme.Training;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = LevelTheme.Training;

        /// <summary>Configure RenderSettings + find-or-create the directional sun for this theme. Returns the sun.
        /// Works in edit mode (scene builder: settings are saved with the active scene) and at runtime.</summary>
        public static Light Apply(LevelTheme theme)
        {
            var look = ThemeLooks.Get(theme);
            Current = theme;

            var sun = FindOrCreateSun();
            sun.type = LightType.Directional;
            sun.color = look.SunColor;
            sun.intensity = look.SunIntensity;
            sun.transform.rotation = Quaternion.Euler(look.SunPitch, look.SunYaw, 0f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = look.ShadowStrength;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.renderMode = LightRenderMode.ForcePixel;
#if UNITY_EDITOR
            sun.lightmapBakeType = LightmapBakeType.Realtime;   // editor-only API (scene builder); players are realtime anyway
#endif
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = look.FogDensity;
            RenderSettings.fogColor = look.FogColor;

            RenderSettings.skybox = SkyMaterial(theme);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = look.Reflection;
            RenderSettings.subtractiveShadowColor = look.AmbientGround;
            if (Application.isPlaying) CaptureLook();   // campaign wave: world color / override work from these values
            return sun;
        }

        /// <summary>True when the WebGL/Mobile real-time light budget applies (WebGL player, Mobile quality level, Low
        /// graphics setting): decorative lights (DecorLight / EnvKit panels and beacons) are switched off at scene start.</summary>
        public static bool LowEndLighting => LightBudget.IsLowEnd;

        /// <summary>Runs the light budget pass now (it also runs by itself whenever a scene loads). See LightBudget.</summary>
        public static void ApplyLightBudget() => LightBudget.Apply();

        public static Color FogColor(LevelTheme theme) => ThemeLooks.Get(theme).FogColor;

        /// <summary>Theme accent color (trim / glow strips / UI-matching highlights).</summary>
        public static Color AccentColor(LevelTheme theme) => ThemeLooks.Get(theme).Accent;

        /// <summary>Baked Resources/Materials/Sky_&lt;Theme&gt; or a runtime-built GradientSky material.</summary>
        public static Material SkyMaterial(LevelTheme theme)
        {
            var m = Resources.Load<Material>("Materials/Sky_" + theme);
            if (m != null) return m;
            if (_runtimeSky[(int)theme] == null) _runtimeSky[(int)theme] = CreateSkyMaterial(theme);
            return _runtimeSky[(int)theme];
        }

        static readonly Material[] _runtimeSky = new Material[System.Enum.GetValues(typeof(LevelTheme)).Length];   // W0: themes appended

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSky() { for (int i = 0; i < _runtimeSky.Length; i++) _runtimeSky[i] = null; }

        /// <summary>Creates a fresh GradientSky material for the theme (used by the MaterialBaker).</summary>
        public static Material CreateSkyMaterial(LevelTheme theme)
        {
            var shader = MaterialLibrary.SkyShader;
            var m = new Material(shader) { name = "Sky_" + theme };
            ApplySky(m, theme);
            return m;
        }

        /// <summary>Writes the theme's sky gradient/sun/nebula parameters into a GradientSky material.</summary>
        public static void ApplySky(Material m, LevelTheme theme)
        {
            var l = ThemeLooks.Get(theme);
            m.SetColor("_TopColor", l.SkyTop);
            m.SetColor("_HorizonColor", l.SkyHorizon);
            m.SetColor("_BottomColor", l.SkyBottom);
            m.SetFloat("_HorizonSharpness", l.HorizonSharpness);
            m.SetColor("_SunColor", l.SunGlowColor);
            var dir = Quaternion.Euler(l.SunPitch, l.SunYaw, 0f) * Vector3.back; // towards the sun
            m.SetVector("_SunDirection", new Vector4(dir.x, dir.y, dir.z, 0f));
            m.SetFloat("_SunSize", l.SunSize);
            m.SetFloat("_SunGlow", l.SunGlow);
            m.SetColor("_NebulaColorA", l.NebulaA);
            m.SetColor("_NebulaColorB", l.NebulaB);
            m.SetFloat("_NebulaStrength", l.NebulaStrength);
            m.SetFloat("_ShaftStrength", l.Shafts);
            m.SetFloat("_StarStrength", l.Stars);
            m.SetFloat("_Seed", l.Seed);
        }

        static Light FindOrCreateSun()
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional && sun.gameObject.scene.IsValid()) return sun;
            sun = null;
            var lights = Object.FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || l.type != LightType.Directional) continue;
                if (l.name == "Sun") { sun = l; break; }
                if (sun == null) sun = l;
            }
            if (sun == null)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.name = "Sun";
            return sun;
        }
    }
}
