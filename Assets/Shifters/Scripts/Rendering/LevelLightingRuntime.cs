// =====================================================================================
//  SHIFTERS - LevelLightingRuntime.cs   (RENDERING, campaign wave)
//  Runtime half of LevelLighting: the scene's look is captured on load (sun color/intensity, ambient trilight, fog),
//  then recomposed whenever the world color or the lighting override changes:
//    sun.color / fog  = WorldColor.Desaturate(original, WorldColor.Current)       (CPU half of CAMPAIGN 14)
//    sun.intensity    = original * override intensity;  ambient = original * override ambient  (SetOverride)
//  Nothing is written while both systems are idle, so levels that tweak RenderSettings themselves keep working.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class LevelLighting
    {
        static bool _captured, _modified;
        static Light _capSun;
        static Color _capSunColor, _capFog, _capAmbSky, _capAmbEq, _capAmbGround;
        static float _capSunIntensity, _capAmbIntensity;

        static float _ovI = 1f, _ovA = 1f, _ovFromI = 1f, _ovFromA = 1f, _ovToI = 1f, _ovToA = 1f, _ovAge, _ovDur;
        static bool _ovActive, _ovAnimating;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime()
        {
            _captured = _modified = false; _capSun = null;
            _ovI = _ovA = _ovFromI = _ovFromA = _ovToI = _ovToA = 1f; _ovAge = _ovDur = 0f; _ovActive = _ovAnimating = false;
        }

        /// <summary>True while a SetOverride is in effect (or fading back).</summary>
        public static bool OverrideActive => _ovActive || _ovAnimating;
        /// <summary>Current override multipliers (1 = none).</summary>
        public static float OverrideIntensity => _ovI;
        public static float OverrideAmbient => _ovA;
        /// <summary>(polish) Where the override is heading (SetOverride's targets; 1/1 while none or fading back).
        /// Short effects (scare blackouts) save these and restore them afterwards.</summary>
        public static bool OverrideHeld => _ovActive;
        public static float OverrideTargetIntensity => _ovToI;
        public static float OverrideTargetAmbient => _ovToA;

        /// <summary>Dims (or boosts) the scene: sun intensity x intensity, ambient trilight x ambient, blended over
        /// seconds. Blackouts: SetOverride(0.05f, 0.12f, 0.2f). Emissive puzzle parts keep glowing (readability floor).
        /// Cleared by ClearOverride or the next scene load.</summary>
        public static void SetOverride(float intensity, float ambient, float seconds)
        {
            if (!Application.isPlaying) return;
            RenderingRuntime.Ensure();
            if (!_captured) CaptureLook();
            _ovFromI = _ovI; _ovFromA = _ovA;
            _ovToI = Mathf.Max(0f, intensity); _ovToA = Mathf.Max(0f, ambient);
            _ovAge = 0f; _ovDur = Mathf.Max(0f, seconds);
            _ovActive = true;
            _ovAnimating = _ovDur > 0f;
            if (!_ovAnimating) { _ovI = _ovToI; _ovA = _ovToA; }
            RefreshLook();
        }

        /// <summary>Returns to the scene's own lighting over seconds.</summary>
        public static void ClearOverride(float seconds = 0.5f)
        {
            if (!_ovActive && !_ovAnimating) return;
            _ovFromI = _ovI; _ovFromA = _ovA; _ovToI = 1f; _ovToA = 1f; _ovAge = 0f; _ovDur = Mathf.Max(0f, seconds);
            _ovActive = false;
            _ovAnimating = _ovDur > 0f;
            if (!_ovAnimating) { _ovI = 1f; _ovA = 1f; }
            RefreshLook();
        }

        /// <summary>Re-reads the scene's current sun/ambient/fog as the "original" look (call after changing
        /// RenderSettings yourself while the world color or an override is active).</summary>
        public static void CaptureLook()
        {
            _capSun = RenderSettings.sun;
            if (_capSun == null)
            {
                var lights = Object.FindObjectsByType<Light>();
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i] != null && lights[i].type == LightType.Directional) { _capSun = lights[i]; break; }
            }
            _capSunColor = _capSun != null ? _capSun.color : Color.white;
            _capSunIntensity = _capSun != null ? _capSun.intensity : 1f;
            _capFog = RenderSettings.fogColor;
            _capAmbSky = RenderSettings.ambientSkyColor;
            _capAmbEq = RenderSettings.ambientEquatorColor;
            _capAmbGround = RenderSettings.ambientGroundColor;
            _capAmbIntensity = RenderSettings.ambientIntensity;
            _captured = true;
            _modified = false;
        }

        /// <summary>The captured (un-greyed, un-dimmed) fog color of the current scene.</summary>
        public static Color OriginalFogColor => _captured ? _capFog : RenderSettings.fogColor;
        public static Color OriginalSunColor => _captured ? _capSunColor : (RenderSettings.sun != null ? RenderSettings.sun.color : Color.white);

        /// <summary>Recomposes sun/fog/ambient from the captured look, the world color and the override.</summary>
        public static void RefreshLook()
        {
            if (!Application.isPlaying) return;
            bool wc = WorldColor.EffectActive;
            bool ov = _ovActive || _ovAnimating || _ovI != 1f || _ovA != 1f;
            if (!wc && !ov && !_modified) return;
            if (!_captured) CaptureLook();
            float sat = wc ? WorldColor.Current : 1f;
            bool grey = wc && Mathf.Abs(sat - 1f) > 0.0005f;
            if (_capSun != null)
            {
                _capSun.color = grey ? WorldColor.Desaturate(_capSunColor, sat) : _capSunColor;
                _capSun.intensity = _capSunIntensity * _ovI;
            }
            var fog = grey ? WorldColor.Desaturate(_capFog, sat) : _capFog;
            // a blackout also darkens the fog a little so distant geometry does not glow grey
            RenderSettings.fogColor = Color.Lerp(fog * 0.25f, fog, Mathf.Clamp01(0.35f + 0.65f * _ovA));
            RenderSettings.ambientSkyColor = _capAmbSky * _ovA;
            RenderSettings.ambientEquatorColor = _capAmbEq * _ovA;
            RenderSettings.ambientGroundColor = _capAmbGround * _ovA;
            _modified = grey || ov;
        }

        internal static void OnSceneLoaded()
        {
            _ovI = _ovA = _ovToI = _ovToA = 1f; _ovActive = _ovAnimating = false;
            CaptureLook();
        }

        internal static void Tick(float dt)
        {
            if (!_ovAnimating) return;
            _ovAge += dt;
            float t = _ovDur > 0f ? Mathf.Clamp01(_ovAge / _ovDur) : 1f;
            float e = t * t * (3f - 2f * t);
            _ovI = Mathf.Lerp(_ovFromI, _ovToI, e);
            _ovA = Mathf.Lerp(_ovFromA, _ovToA, e);
            if (t >= 1f) _ovAnimating = false;
            RefreshLook();
        }
    }
}
