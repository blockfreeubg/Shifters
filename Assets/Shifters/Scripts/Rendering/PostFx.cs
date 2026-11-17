// =====================================================================================
//  SHIFTERS - PostFx.cs   [CONTRACT API - implementation owned by the RENDERING agent]
//  Runtime URP Volume stack: theme grade + perspective tint + switcher desaturation + flashes.
//  Creates its own persistent global Volume lazily. Safe to call when nothing exists yet.
//  All calls are no-ops in edit mode (scenes never get a runtime profile baked in).
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shifters
{
    public static class PostFx
    {
        static Volume _volume;
        static VolumeProfile _profile;
        static Tonemapping _tone;
        static Bloom _bloom;
        static ColorAdjustments _color;
        static Vignette _vignette;
        static ShadowsMidtonesHighlights _smh;
        static WhiteBalance _wb;
        static ChromaticAberration _ca;
        static DepthOfField _dof;
        static PostFxDriver _driver;

        static LevelTheme _theme = LevelTheme.Training;
        static Color _tintColor = Color.white;
        static float _tintWeight;
        static float _switcher;
        static float _burst;
        static int _quality = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _volume = null; _profile = null; _tone = null; _bloom = null; _color = null; _vignette = null; _smh = null;
            _wb = null; _ca = null; _dof = null; _driver = null;
            _theme = LevelTheme.Training; _tintColor = Color.white; _tintWeight = 0f; _switcher = 0f; _burst = 0f; _quality = 2;
        }

        /// <summary>The persistent global volume (null in edit mode / before first use).</summary>
        public static Volume Volume => _volume;
        public static LevelTheme Theme => _theme;
        public static float SwitcherBlend => _switcher;
        public static int Quality => _quality;

        public static void SetTheme(LevelTheme theme)
        {
            _theme = theme;
            if (!Ensure()) return;
            Refresh();
            EnsureCameraPost(Camera.main);
        }

        /// <summary>Perspective color grading overlay. weight 0..1.</summary>
        public static void SetPerspectiveTint(Color color, float weight)
        {
            _tintColor = color;
            _tintWeight = Mathf.Clamp01(weight);
            if (!Ensure()) return;
            Refresh();
        }

        /// <summary>Switcher open blend 0..1 (desaturate, vignette, slight blur feel).</summary>
        public static void SetSwitcherBlend(float t01)
        {
            _switcher = Mathf.Clamp01(t01);
            if (!Ensure()) return;
            Refresh();
        }

        /// <summary>Full-screen color flash (damage red, checkpoint green...).</summary>
        public static void Flash(Color color, float duration = 0.25f)
        {
            if (!Ensure()) return;
            _driver.Flash(color, Mathf.Max(0.02f, duration));
        }

        /// <summary>0 = low (no bloom / chromatic aberration / vignette; decorative real-time lights off via LightBudget),
        /// 1 = medium, 2 = high.</summary>
        public static void SetQuality(int quality)
        {
            int q = Mathf.Clamp(quality, 0, 2);
            bool lowChanged = (q == 0) != (_quality == 0);
            _quality = q;
            if (!Ensure()) return;
            Refresh();
            if (lowChanged) LightBudget.Refresh();
        }

        /// <summary>Short chromatic-aberration kick (turbo sprint, big impacts). Decays over 'duration' seconds (unscaled).</summary>
        public static void Burst(float strength = 0.6f, float duration = 0.35f)
        {
            if (!Ensure()) return;
            _driver.Burst(Mathf.Clamp01(strength), Mathf.Max(0.02f, duration));
        }

        /// <summary>Makes sure a camera renders post-processing (URP camera data).</summary>
        public static void EnsureCameraPost(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
        }

        internal static void SetBurstValue(float v)
        {
            if (Mathf.Approximately(v, _burst)) return;
            _burst = v;
            if (_volume != null) Refresh();
        }

        static bool Ensure()
        {
            if (!Application.isPlaying) return false;
            if (_volume != null && _profile != null) return true;
            var go = new GameObject("[PostFx]");
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 1f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "ShiftersRuntimeProfile";
            _tone = _profile.Add<Tonemapping>(true);
            _bloom = _profile.Add<Bloom>(true);
            _color = _profile.Add<ColorAdjustments>(true);
            _vignette = _profile.Add<Vignette>(true);
            _smh = _profile.Add<ShadowsMidtonesHighlights>(true);
            _wb = _profile.Add<WhiteBalance>(true);
            _ca = _profile.Add<ChromaticAberration>(true);
            _dof = _profile.Add<DepthOfField>(true);
            _volume.sharedProfile = _profile;
            _driver = go.AddComponent<PostFxDriver>();
            Refresh();
            return true;
        }

        static void Refresh()
        {
            if (_profile == null) return;
            var l = ThemeLooks.Get(_theme);
            float sw = _switcher, tw = _tintWeight;
            bool fancy = _quality > 0;

            _tone.mode.Override(TonemappingMode.Neutral);

            _bloom.active = fancy;
            _bloom.intensity.Override(l.BloomIntensity * (_quality >= 2 ? 1f : 0.8f));
            _bloom.threshold.Override(l.BloomThreshold);
            _bloom.scatter.Override(l.BloomScatter);
            _bloom.tint.Override(l.BloomTint);
            _bloom.highQualityFiltering.Override(_quality >= 2);

            _color.postExposure.Override(l.Exposure - 0.25f * sw);
            _color.contrast.Override(Mathf.Clamp(l.Contrast + 12f * sw, -100f, 100f));
            _color.saturation.Override(Mathf.Lerp(l.Saturation, -70f, sw));
            var tint = Color.Lerp(Color.white, LightenTint(_tintColor), tw * 0.5f);
            _color.colorFilter.Override(tint);

            _smh.shadows.Override(l.Shadows);
            _smh.midtones.Override(l.Midtones);
            _smh.highlights.Override(l.Highlights);

            _wb.temperature.Override(l.Temperature);
            _wb.tint.Override(l.Tint);

            _vignette.active = fancy;
            float vig = Mathf.Lerp(l.Vignette + 0.1f * tw, 0.45f, sw);
            _vignette.intensity.Override(vig);
            _vignette.smoothness.Override(0.42f);
            _vignette.rounded.Override(false);
            _vignette.color.Override(Color.Lerp(l.VignetteColor, _tintColor * 0.55f, tw * 0.8f));

            _ca.active = fancy;
            _ca.intensity.Override(Mathf.Clamp01(Mathf.Max(0.22f * sw, _burst)));

            _dof.mode.Override(DepthOfFieldMode.Off);
        }

        static Color LightenTint(Color c)
        {
            // keep the filter bright so it tints instead of darkening the frame
            float max = Mathf.Max(0.001f, c.maxColorComponent);
            var n = c / max;
            return Color.Lerp(n, Color.white, 0.35f);
        }
    }
}
