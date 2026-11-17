// =====================================================================================
//  SHIFTERS - ThemeLook.cs   (RENDERING)
//  Per-theme lighting / sky / fog / post-processing presets used by LevelLighting and PostFx.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>All look parameters of one LevelTheme (sun, ambient, fog, sky, color grade).</summary>
    public sealed class ThemeLook
    {
        // sun
        public Color SunColor; public float SunIntensity; public float SunPitch; public float SunYaw; public float ShadowStrength = 0.85f;
        // ambient trilight + reflections
        public Color AmbientSky, AmbientEquator, AmbientGround; public float Reflection = 0.4f;
        // fog (exponential squared) - distant void fades to the sky horizon color
        public Color FogColor; public float FogDensity;
        // sky (Shifters/GradientSky)
        public Color SkyTop, SkyHorizon, SkyBottom; public float HorizonSharpness = 3.5f;
        public Color SunGlowColor; public float SunGlow = 0.4f; public float SunSize = 0.15f;
        public Color NebulaA, NebulaB; public float NebulaStrength = 0.2f; public float Shafts = 0.15f; public float Stars; public float Seed = 3.7f;
        // StylizedLit shadow tint for this theme's materials
        public Color ShadowTint;
        // post grade
        public float Exposure, Contrast, Saturation, Temperature, Tint;
        public float BloomIntensity = 0.6f, BloomThreshold = 1.05f, BloomScatter = 0.65f; public Color BloomTint = Color.white;
        public float Vignette = 0.22f; public Color VignetteColor = Color.black;
        public Vector4 Shadows = new Vector4(1, 1, 1, 0), Midtones = new Vector4(1, 1, 1, 0), Highlights = new Vector4(1, 1, 1, 0);
        // gameplay-facing accent (trim / glow strips)
        public Color Accent;
    }

    /// <summary>Theme presets (DESIGN §6). Training bright clean, Factory amber haze, Lab sickly green, Research violet
    /// dreamy, Chaos red emergency, Menu dark stage; campaign: Bank golden dusk, Night midnight blue (rain/fog), Tower
    /// red pre-dawn behind black glass + gold.</summary>
    public static class ThemeLooks
    {
        static ThemeLook[] _looks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _looks = null;

        static Color H(int rgb) => UIColors.Hex(rgb);

        public static ThemeLook Get(LevelTheme theme)
        {
            if (_looks == null) Build();
            int i = (int)theme;
            return i >= 0 && i < _looks.Length ? _looks[i] : _looks[(int)LevelTheme.Training];
        }

        static void Build()
        {
            _looks = new ThemeLook[System.Enum.GetValues(typeof(LevelTheme)).Length];
            _looks[(int)LevelTheme.Training] = new ThemeLook
            {
                SunColor = H(0xFFF0D6), SunIntensity = 1.1f, SunPitch = 48f, SunYaw = -38f, ShadowStrength = 0.8f,
                AmbientSky = H(0xB6CDEB), AmbientEquator = H(0x97A8BE), AmbientGround = H(0x6D6863), Reflection = 0.45f,
                FogColor = H(0xDCE6F1), FogDensity = 0.0052f,
                SkyTop = H(0x6FA3E0), SkyHorizon = H(0xDCE6F1), SkyBottom = H(0x93A7C2), HorizonSharpness = 3.2f,
                SunGlowColor = H(0xFFF1D8), SunGlow = 0.45f, NebulaA = H(0xFFFFFF), NebulaB = H(0xBFD8FF), NebulaStrength = 0.22f, Shafts = 0.12f, Seed = 2.1f,
                ShadowTint = H(0xBAC6FF),
                Exposure = 0f, Contrast = 8f, Saturation = 8f, Temperature = 2f,
                BloomIntensity = 0.3f, BloomThreshold = 1.5f, BloomScatter = 0.5f, Vignette = 0.2f, VignetteColor = H(0x0B1424),
                Shadows = new Vector4(0.96f, 0.98f, 1.06f, 0f), Highlights = new Vector4(1.03f, 1.01f, 0.97f, 0f),
                Accent = H(0xFF8A1F)
            };
            _looks[(int)LevelTheme.Factory] = new ThemeLook
            {
                SunColor = H(0xFFBE78), SunIntensity = 1.35f, SunPitch = 30f, SunYaw = 125f, ShadowStrength = 0.85f,
                AmbientSky = H(0x9C7A62), AmbientEquator = H(0x6E5242), AmbientGround = H(0x2C211B), Reflection = 0.35f,
                FogColor = H(0xC57844), FogDensity = 0.0082f,
                SkyTop = H(0x2A1912), SkyHorizon = H(0xC57844), SkyBottom = H(0x22140E), HorizonSharpness = 2.6f,
                SunGlowColor = H(0xFFB063), SunGlow = 0.7f, SunSize = 0.25f, NebulaA = H(0xFF9A4A), NebulaB = H(0x6E3A22), NebulaStrength = 0.3f, Shafts = 0.3f, Seed = 5.3f,
                ShadowTint = H(0xC2A2B6),
                Exposure = 0f, Contrast = 16f, Saturation = 10f, Temperature = 10f,
                BloomIntensity = 0.55f, BloomThreshold = 1.2f, BloomScatter = 0.55f, BloomTint = H(0xFFD2A8), Vignette = 0.28f, VignetteColor = H(0x1A0A04),
                Shadows = new Vector4(0.98f, 0.95f, 1.04f, -0.02f), Highlights = new Vector4(1.05f, 1.0f, 0.92f, 0f),
                Accent = H(0xFFB21F)
            };
            _looks[(int)LevelTheme.Lab] = new ThemeLook
            {
                SunColor = H(0xE6FFE4), SunIntensity = 1.0f, SunPitch = 62f, SunYaw = 22f, ShadowStrength = 0.8f,
                AmbientSky = H(0x88AA93), AmbientEquator = H(0x5A7864), AmbientGround = H(0x1E2A22), Reflection = 0.4f,
                FogColor = H(0x5E8A6B), FogDensity = 0.0085f,
                SkyTop = H(0x0C1812), SkyHorizon = H(0x5E8A6B), SkyBottom = H(0x07100B), HorizonSharpness = 2.8f,
                SunGlowColor = H(0xC8FFB8), SunGlow = 0.3f, NebulaA = H(0x7CFF4D), NebulaB = H(0x2E6A50), NebulaStrength = 0.25f, Shafts = 0.25f, Seed = 8.9f,
                ShadowTint = H(0x9CC6B0),
                Exposure = 0f, Contrast = 18f, Saturation = -6f, Temperature = -4f, Tint = -8f,
                BloomIntensity = 0.6f, BloomThreshold = 1.15f, BloomScatter = 0.55f, BloomTint = H(0xC8FFC8), Vignette = 0.32f, VignetteColor = H(0x03120A),
                Shadows = new Vector4(0.95f, 1.04f, 0.98f, -0.02f), Midtones = new Vector4(0.98f, 1.03f, 0.98f, 0f),
                Accent = H(0xB8D94A)
            };
            _looks[(int)LevelTheme.Research] = new ThemeLook
            {
                SunColor = H(0xFFF3FF), SunIntensity = 1.15f, SunPitch = 46f, SunYaw = -62f, ShadowStrength = 0.75f,
                AmbientSky = H(0xC4B6F5), AmbientEquator = H(0x9B8CD2), AmbientGround = H(0x41366B), Reflection = 0.5f,
                FogColor = H(0xC9B6F8), FogDensity = 0.005f,
                SkyTop = H(0x2B1D63), SkyHorizon = H(0xC9B6F8), SkyBottom = H(0x1A1140), HorizonSharpness = 2.4f,
                SunGlowColor = H(0xFFE6FF), SunGlow = 0.5f, NebulaA = H(0xFF9BF0), NebulaB = H(0x7C5CFF), NebulaStrength = 0.45f, Shafts = 0.2f, Stars = 0.8f, Seed = 11.4f,
                ShadowTint = H(0xB4A3FF),
                Exposure = 0f, Contrast = 6f, Saturation = 10f, Temperature = -2f, Tint = 6f,
                BloomIntensity = 0.5f, BloomThreshold = 1.45f, BloomScatter = 0.55f, BloomTint = H(0xE6D6FF), Vignette = 0.18f, VignetteColor = H(0x140A36),
                Shadows = new Vector4(1.0f, 0.96f, 1.08f, 0f), Highlights = new Vector4(1.02f, 1.0f, 1.04f, 0f),
                Accent = H(0x9B6BFF)
            };
            _looks[(int)LevelTheme.Chaos] = new ThemeLook
            {
                SunColor = H(0xFF7652), SunIntensity = 1.3f, SunPitch = 24f, SunYaw = 205f, ShadowStrength = 0.92f,
                AmbientSky = H(0x5E3038), AmbientEquator = H(0x40232A), AmbientGround = H(0x130909), Reflection = 0.3f,
                FogColor = H(0x5C1A13), FogDensity = 0.008f,
                SkyTop = H(0x090304), SkyHorizon = H(0x5C1A13), SkyBottom = H(0x050203), HorizonSharpness = 2.2f,
                SunGlowColor = H(0xFF5A2A), SunGlow = 0.8f, SunSize = 0.3f, NebulaA = H(0xFF3A1A), NebulaB = H(0xFF8A1F), NebulaStrength = 0.35f, Shafts = 0.35f, Stars = 0.3f, Seed = 13.7f,
                ShadowTint = H(0x9A7AA6),
                Exposure = 0f, Contrast = 24f, Saturation = 8f, Temperature = 6f,
                BloomIntensity = 0.75f, BloomThreshold = 1.05f, BloomScatter = 0.6f, BloomTint = H(0xFFB49A), Vignette = 0.36f, VignetteColor = H(0x1E0202),
                Shadows = new Vector4(1.06f, 0.94f, 0.96f, -0.03f), Highlights = new Vector4(1.05f, 0.98f, 0.94f, 0f),
                Accent = H(0xFF5A1F)
            };
            _looks[(int)LevelTheme.Menu] = new ThemeLook
            {
                SunColor = H(0xFFE2C2), SunIntensity = 0.75f, SunPitch = 55f, SunYaw = -30f, ShadowStrength = 0.9f,
                AmbientSky = H(0x3C465A), AmbientEquator = H(0x272D3A), AmbientGround = H(0x0E1015), Reflection = 0.3f,
                FogColor = H(0x161B26), FogDensity = 0.012f,
                SkyTop = H(0x05060A), SkyHorizon = H(0x1A2030), SkyBottom = H(0x040507), HorizonSharpness = 3f,
                SunGlowColor = H(0xFF9A4A), SunGlow = 0.2f, NebulaA = H(0xFF8A1F), NebulaB = H(0x3D5AFF), NebulaStrength = 0.25f, Shafts = 0.1f, Stars = 0.6f, Seed = 1.3f,
                ShadowTint = H(0x8E9AC8),
                Exposure = 0f, Contrast = 12f, Saturation = 6f,
                BloomIntensity = 0.7f, BloomThreshold = 1.05f, Vignette = 0.36f, VignetteColor = H(0x000000),
                Accent = H(0xFF8A1F)
            };
            _looks[(int)LevelTheme.Bank] = new ThemeLook
            {
                SunColor = H(0xFFC27A), SunIntensity = 1.25f, SunPitch = 22f, SunYaw = 112f, ShadowStrength = 0.8f,
                AmbientSky = H(0xD0A88A), AmbientEquator = H(0x9A765E), AmbientGround = H(0x45322A), Reflection = 0.5f,
                FogColor = H(0xE3A877), FogDensity = 0.0058f,
                SkyTop = H(0x3A2C66), SkyHorizon = H(0xFFB878), SkyBottom = H(0x5E3C46), HorizonSharpness = 2.4f,
                SunGlowColor = H(0xFFC27A), SunGlow = 0.85f, SunSize = 0.32f, NebulaA = H(0xFFD08A), NebulaB = H(0xC77AA8), NebulaStrength = 0.32f, Shafts = 0.28f, Stars = 0.15f, Seed = 6.2f,
                ShadowTint = H(0xB89ACB),
                Exposure = 0f, Contrast = 12f, Saturation = 10f, Temperature = 12f, Tint = 2f,
                BloomIntensity = 0.65f, BloomThreshold = 1.1f, BloomScatter = 0.65f, BloomTint = H(0xFFDCA0), Vignette = 0.26f, VignetteColor = H(0x1C0E08),
                Shadows = new Vector4(1.0f, 0.95f, 1.06f, -0.02f), Highlights = new Vector4(1.06f, 1.01f, 0.9f, 0.01f),
                Accent = H(0xFFC24A)
            };
            _looks[(int)LevelTheme.Night] = new ThemeLook
            {
                SunColor = H(0xA8C0FF), SunIntensity = 0.55f, SunPitch = 55f, SunYaw = -140f, ShadowStrength = 0.75f,
                AmbientSky = H(0x4A5C90), AmbientEquator = H(0x2C385A), AmbientGround = H(0x10141F), Reflection = 0.45f,
                FogColor = H(0x1C2846), FogDensity = 0.017f,
                SkyTop = H(0x03050E), SkyHorizon = H(0x223056), SkyBottom = H(0x05070F), HorizonSharpness = 2.2f,
                SunGlowColor = H(0xC8D8FF), SunGlow = 0.55f, SunSize = 0.28f, NebulaA = H(0x3D5AFF), NebulaB = H(0x7A4CFF), NebulaStrength = 0.18f, Shafts = 0.08f, Stars = 1.2f, Seed = 9.6f,
                ShadowTint = H(0x7C8CD0),
                Exposure = 0.1f, Contrast = 14f, Saturation = -2f, Temperature = -14f, Tint = 2f,
                BloomIntensity = 0.85f, BloomThreshold = 1.0f, BloomScatter = 0.7f, BloomTint = H(0xB8D0FF), Vignette = 0.4f, VignetteColor = H(0x02040C),
                Shadows = new Vector4(0.94f, 0.98f, 1.12f, -0.02f), Highlights = new Vector4(0.98f, 1.0f, 1.06f, 0f),
                Accent = H(0x7FD8FF)
            };
            _looks[(int)LevelTheme.Tower] = new ThemeLook
            {
                SunColor = H(0xFF7650), SunIntensity = 1.2f, SunPitch = 14f, SunYaw = 160f, ShadowStrength = 0.9f,
                AmbientSky = H(0x74404E), AmbientEquator = H(0x4A2A36), AmbientGround = H(0x150B10), Reflection = 0.55f,
                FogColor = H(0x5A2430), FogDensity = 0.0068f,
                SkyTop = H(0x0B0612), SkyHorizon = H(0xD24E30), SkyBottom = H(0x170709), HorizonSharpness = 2.0f,
                SunGlowColor = H(0xFF7A40), SunGlow = 0.95f, SunSize = 0.38f, NebulaA = H(0xFF4A2A), NebulaB = H(0x8A2A5A), NebulaStrength = 0.3f, Shafts = 0.3f, Stars = 0.45f, Seed = 15.2f,
                ShadowTint = H(0x8E78B0),
                Exposure = 0f, Contrast = 20f, Saturation = 6f, Temperature = 6f, Tint = 3f,
                BloomIntensity = 0.75f, BloomThreshold = 1.05f, BloomScatter = 0.65f, BloomTint = H(0xFFC08A), Vignette = 0.34f, VignetteColor = H(0x160206),
                Shadows = new Vector4(1.04f, 0.94f, 1.0f, -0.03f), Highlights = new Vector4(1.06f, 0.99f, 0.9f, 0f),
                Accent = H(0xFFB830)
            };
        }
    }
}
