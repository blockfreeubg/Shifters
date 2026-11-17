// =====================================================================================
//  SHIFTERS - Palettes.cs   (RENDERING)
//  Per-theme color palettes and per-MatKey material parameters (MatSpec). MaterialLibrary and the
//  MaterialBaker build every <Theme>_<Key> material from these specs. The puzzle language
//  (PuzzleBody teal, indicator lights, hazards, props) is identical in every theme on purpose.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Material family. Acid = Shifters/Acid (self-lit boiling toxic liquid with bubbles).</summary>
    public enum MatSurface { Opaque, Transparent, Hologram, Unlit, Acid }

    /// <summary>Everything needed to build one material.</summary>
    public struct MatSpec
    {
        public MatSurface Surface;
        public Color Color;
        public float Smoothness, Specular, RimStrength, RimPower;
        public Color RimColor;
        public Color Emission;               // HDR emission (black = none)
        public float GridStrength, GridScale, GridWidth, GridGlow; public Color GridColor;
        public float Noise, NoiseScale;
        public float StripeStrength, StripeWidth; public Color StripeColor;
        public float ShadeSteps, ShadeSoftness, Wrap;
        public Color ShadowTint;
        public bool CastShadows;
        /// <summary>WORLD COLOR: keeps full color in the grey world (_GreyImmune = 1). Set from Palettes.IsGreyImmune.</summary>
        public bool GreyImmune;

        public static MatSpec Default(Color c) => new MatSpec
        {
            Surface = MatSurface.Opaque, Color = c, Smoothness = 0.3f, Specular = 0.5f, RimStrength = 0.18f, RimPower = 3.5f,
            RimColor = Color.white, Emission = Color.black, GridScale = 1f, GridWidth = 0.02f, GridColor = new Color(0, 0, 0, 0.6f),
            Noise = 0.04f, NoiseScale = 0.35f, StripeWidth = 0.3f, StripeColor = UIColors.Hex(0x1E1F24),
            ShadeSteps = 3f, ShadeSoftness = 0.45f, Wrap = 0.3f, ShadowTint = UIColors.Hex(0xC7D0FF), CastShadows = true
        };
    }

    public static class Palettes
    {
        static Dictionary<int, Color>[] _theme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _theme = null;

        static Color H(int rgb) => UIColors.Hex(rgb);

        /// <summary>Palette color of a key in a theme (theme override, else MaterialLibrary.DefaultColor).</summary>
        public static Color Color(MatKey key, LevelTheme theme)
        {
            if (_theme == null) Build();
            var d = _theme[(int)theme];
            if (d != null && d.TryGetValue((int)key, out var c)) return c;
            return MaterialLibrary.DefaultColor(key);
        }

        /// <summary>Full material spec for a key in a theme.</summary>
        public static MatSpec Spec(MatKey key, LevelTheme theme)
        {
            var look = ThemeLooks.Get(theme);
            var c = Color(key, theme);
            var s = MatSpec.Default(c);
            s.ShadowTint = look.ShadowTint;
            s.GreyImmune = IsGreyImmune(key);
            switch (key)
            {
                case MatKey.Floor:
                    s.Smoothness = 0.25f; s.Specular = 0.35f; s.RimStrength = 0.08f; s.Noise = 0.05f;
                    s.GridStrength = 0.55f; s.GridScale = 1f; s.GridWidth = 0.022f; s.GridColor = Shade(c, 0.62f, 1f);
                    if (theme == LevelTheme.Night) { s.Smoothness = 0.55f; s.Specular = 0.7f; s.GridColor = new Color(0.36f, 0.62f, 1f, 0.8f); s.GridGlow = 0.55f; } // wet tiles, glowing seams
                    else if (theme == LevelTheme.Tower) { s.Smoothness = 0.7f; s.Specular = 0.9f; s.GridColor = new Color(0.86f, 0.62f, 0.2f, 0.9f); s.GridGlow = 0.5f; }
                    else if (theme == LevelTheme.Bank) { s.Smoothness = 0.7f; s.Specular = 0.85f; s.GridScale = 1.5f; s.GridColor = new Color(0.8f, 0.6f, 0.25f, 0.85f); s.GridGlow = 0.15f; s.Noise = 0.09f; s.NoiseScale = 1.9f; }
                    break;
                case MatKey.FloorAlt:
                    s.Smoothness = 0.3f; s.Specular = 0.35f; s.RimStrength = 0.08f; s.Noise = 0.05f;
                    s.GridStrength = 0.5f; s.GridScale = 0.5f; s.GridWidth = 0.03f; s.GridColor = Shade(c, 0.65f, 1f);
                    break;
                case MatKey.Wall:
                    s.Smoothness = 0.2f; s.RimStrength = 0.12f; s.Noise = 0.035f;
                    s.GridStrength = 0.4f; s.GridScale = 2f; s.GridWidth = 0.008f; s.GridColor = Shade(c, 0.7f, 1f);
                    break;
                case MatKey.WallAlt:
                    s.Smoothness = 0.25f; s.RimStrength = 0.12f; s.Noise = 0.04f;
                    s.GridStrength = 0.35f; s.GridScale = 1f; s.GridWidth = 0.012f; s.GridColor = Shade(c, 0.72f, 1f);
                    break;
                case MatKey.Ceiling: s.Smoothness = 0.15f; s.RimStrength = 0.05f; break;
                case MatKey.Trim:
                    s.Smoothness = 0.45f; s.Specular = 0.6f; s.RimStrength = 0.25f; s.Noise = 0.02f;
                    if (theme == LevelTheme.Research || theme == LevelTheme.Chaos || theme == LevelTheme.Night) s.Emission = c * 0.6f;
                    else if (theme == LevelTheme.Bank || theme == LevelTheme.Tower) { s.Smoothness = 0.8f; s.Specular = 1.3f; s.RimColor = H(0xFFE9A8); s.Emission = c * 0.25f; }
                    break;
                case MatKey.TrimGlow:
                    s.Smoothness = 0.4f; s.Emission = c * (theme == LevelTheme.Night ? 2.3f : 1.7f); s.RimStrength = 0f; s.CastShadows = false; break;
                case MatKey.Concrete: s.Smoothness = 0.1f; s.Specular = 0.15f; s.Noise = 0.13f; s.NoiseScale = 0.9f; break;
                case MatKey.MetalDark:
                case MatKey.MetalLight:
                    s.Smoothness = 0.6f; s.Specular = 0.7f; s.RimStrength = 0.3f; s.Noise = 0.03f; break;
                case MatKey.MetalRust: s.Smoothness = 0.22f; s.Specular = 0.3f; s.Noise = 0.3f; s.NoiseScale = 1.6f; break;
                case MatKey.Grate:
                    s.Smoothness = 0.5f; s.Specular = 0.6f; s.GridStrength = 0.85f; s.GridScale = 0.2f; s.GridWidth = 0.14f;
                    s.GridColor = Shade(c, 0.35f, 1f); break;
                case MatKey.Pipe: s.Smoothness = 0.55f; s.Specular = 0.75f; s.RimStrength = 0.25f; break;
                case MatKey.HazardYellow:
                case MatKey.HazardBlack: s.Smoothness = 0.35f; s.Noise = 0.03f; break;
                case MatKey.HazardStripes:
                    s.Smoothness = 0.35f; s.StripeStrength = 1f; s.StripeWidth = 0.32f; s.StripeColor = H(0x1E1F24); s.Noise = 0.03f; break;
                case MatKey.SafetyOrange: s.Smoothness = 0.4f; s.RimStrength = 0.22f; break;
                case MatKey.WarningRed: s.Smoothness = 0.4f; s.Emission = c * 0.12f; break;
                case MatKey.SignWhite: s.Smoothness = 0.2f; s.Emission = c * (theme == LevelTheme.Chaos || theme == LevelTheme.Menu ? 0.25f : 0.08f); s.Noise = 0f; break;
                case MatKey.Poster: s.Smoothness = 0.1f; s.Specular = 0.1f; s.Noise = 0f; break;
                case MatKey.PuzzleBody:
                case MatKey.PuzzleAccent:
                    s.Smoothness = 0.45f; s.Specular = 0.6f; s.RimStrength = 0.4f; s.RimColor = H(0xBFFFF6); s.Noise = 0.02f; s.Emission = c * 0.14f; s.ShadowTint = H(0xD8F0FF); break;
                case MatKey.LightOff: s.Smoothness = 0.65f; s.Emission = c * 0.35f; s.CastShadows = false; break;
                case MatKey.LightOn:
                case MatKey.LightRed: s.Smoothness = 0.6f; s.Emission = c * 1.9f; s.RimStrength = 0f; s.CastShadows = false; break;
                case MatKey.LightBlue: s.Smoothness = 0.6f; s.Emission = c * 1.8f; s.RimStrength = 0f; s.CastShadows = false; break;
                case MatKey.Screen: s.Smoothness = 0.85f; s.Specular = 0.8f; s.Emission = H(0x2E7FB8) * 0.9f; s.Noise = 0f; break;
                case MatKey.Wood: s.Smoothness = 0.15f; s.Specular = 0.2f; s.Noise = 0.16f; s.NoiseScale = 2.6f; break;
                case MatKey.CrateHeavy: s.Smoothness = 0.5f; s.Specular = 0.65f; s.RimStrength = 0.25f; break;
                case MatKey.Barrel: s.Smoothness = 0.45f; s.Specular = 0.6f; break;
                case MatKey.Rubber: s.Smoothness = 0.08f; s.Specular = 0.1f; s.RimStrength = 0.1f; break;
                case MatKey.Glass:
                    s.Surface = MatSurface.Transparent; s.Smoothness = 0.95f; s.Specular = 1.2f; s.RimStrength = 0.9f; s.RimPower = 2.5f;
                    s.RimColor = H(0xDDF6FF); s.Noise = 0f; s.CastShadows = false; break;
                case MatKey.Plastic: s.Smoothness = 0.55f; s.Specular = 0.6f; break;
                case MatKey.Cardboard: s.Smoothness = 0.05f; s.Specular = 0.05f; s.Noise = 0.1f; s.NoiseScale = 3f; break;
                case MatKey.GhostGlow:   // bedsheet ghosts, ghost trio, haunted hologram signs (Shifters/Hologram, pale cyan)
                case MatKey.Hologram: s.Surface = MatSurface.Hologram; s.CastShadows = false; break;
                case MatKey.Acid:
                    // Shifters/Acid (MaterialLibrary.ConfigureAcid derives its colors from c); the rest is the opaque
                    // emissive StylizedLit fallback used only if that shader is missing.
                    s.Surface = MatSurface.Acid; s.Smoothness = 0.9f; s.Specular = 1f;
                    s.Emission = c * 1.4f; s.RimStrength = 0.6f; s.RimColor = H(0xE6FFB0); s.Noise = 0.15f; s.NoiseScale = 1.4f; s.CastShadows = false; break;
                case MatKey.Laser: s.Emission = c * 3.2f; s.RimStrength = 0f; s.Smoothness = 0f; s.CastShadows = false; break;
                case MatKey.Void: s.Smoothness = 0f; s.Specular = 0f; s.RimStrength = 0f; s.Noise = 0f; break;
                case MatKey.Moon:        // the big moon disc / moon windows: cool cream glow with soft surface variation
                    s.Emission = c * 1.6f; s.RimStrength = 0.6f; s.RimPower = 2f; s.RimColor = H(0xBFD4FF); s.Noise = 0.18f; s.NoiseScale = 1.3f;
                    s.Smoothness = 0f; s.Specular = 0f; s.CastShadows = false; break;
                case MatKey.Glow: s.Emission = c * 2f; s.RimStrength = 0f; s.CastShadows = false; break;
                case MatKey.Marble:      // polished bank marble: big tiles, soft veining noise, glossy
                    s.Smoothness = 0.82f; s.Specular = 0.95f; s.RimStrength = 0.2f; s.Noise = 0.11f; s.NoiseScale = 1.9f;
                    s.GridStrength = 0.35f; s.GridScale = 1.5f; s.GridWidth = 0.012f; s.GridColor = Shade(c, 0.7f, 1f);
                    if (theme == LevelTheme.Bank) { s.GridColor = new Color(0.85f, 0.62f, 0.2f, 0.9f); s.GridGlow = 0.25f; }
                    break;
                case MatKey.Gold:        // GREY-IMMUNE glowing gold (the villain's hoard, coins, crowns, statue)
                    s.Smoothness = 0.88f; s.Specular = 1.5f; s.RimStrength = 0.6f; s.RimPower = 2.6f; s.RimColor = H(0xFFE9A8);
                    s.Emission = c * 0.32f; s.Noise = 0.03f; s.ShadeSteps = 4f; s.ShadowTint = H(0xE0B070); break;
                case MatKey.Velvet:      // velvet ropes / curtains: matte with a bright sheen at grazing angles
                    s.Smoothness = 0.05f; s.Specular = 0.05f; s.RimStrength = 0.75f; s.RimPower = 1.7f; s.RimColor = H(0xFF8AA6);
                    s.Wrap = 0.5f; s.Noise = 0.06f; s.NoiseScale = 4f; break;
                case MatKey.BlackGlass:  // ME TOWER: black glass with thin gold mullions
                    s.Smoothness = 0.96f; s.Specular = 1.6f; s.RimStrength = 0.55f; s.RimPower = 4f; s.RimColor = H(0x9FB4FF); s.Noise = 0f;
                    s.GridStrength = 0.6f; s.GridScale = 1.5f; s.GridWidth = 0.012f; s.GridColor = new Color(0.86f, 0.64f, 0.2f, 0.85f); s.GridGlow = 0.35f; break;
                case MatKey.White: s.Smoothness = 0.3f; s.Noise = 0.03f; break;
                case MatKey.Black: s.Smoothness = 0.35f; break;
                case MatKey.Shadow:
                    s.Surface = MatSurface.Transparent; s.Color = new Color(0, 0, 0, 0.45f); s.Smoothness = 0f; s.Specular = 0f;
                    s.RimStrength = 0f; s.Noise = 0f; s.CastShadows = false; break;
                case MatKey.Foliage: s.Smoothness = 0.2f; s.Wrap = 0.55f; s.Noise = 0.1f; s.NoiseScale = 2f; s.RimStrength = 0.25f; s.RimColor = H(0xE8FFB0); break;
            }
            return s;
        }

        /// <summary>CAMPAIGN 14 grey-immune keys: the puzzle language (teal parts, indicator lights, lasers, acid,
        /// holograms, screens, hazards, warning colors, glow trim) and Gold. Everything else greys with the world.</summary>
        public static bool IsGreyImmune(MatKey key)
        {
            switch (key)
            {
                case MatKey.PuzzleBody: case MatKey.PuzzleAccent:
                case MatKey.LightOff: case MatKey.LightOn: case MatKey.LightRed: case MatKey.LightBlue:
                case MatKey.Laser: case MatKey.Acid: case MatKey.Hologram: case MatKey.Glow: case MatKey.Screen:
                case MatKey.HazardYellow: case MatKey.HazardBlack: case MatKey.HazardStripes:
                case MatKey.WarningRed: case MatKey.SafetyOrange: case MatKey.TrimGlow:
                case MatKey.Gold: case MatKey.GhostGlow: case MatKey.Moon:
                    return true;
                default: return false;
            }
        }

        /// <summary>Multiplies rgb by k, sets alpha a.</summary>
        public static Color Shade(Color c, float k, float a = 1f) => new Color(c.r * k, c.g * k, c.b * k, a);

        static void Build()
        {
            _theme = new Dictionary<int, Color>[System.Enum.GetValues(typeof(LevelTheme)).Length];   // W0: themes appended (Bank/Night/Tower)
            _theme[(int)LevelTheme.Training] = Map(
                (MatKey.Floor, 0xAEB9C8), (MatKey.FloorAlt, 0x97A4B6), (MatKey.Wall, 0xE4E8EE), (MatKey.WallAlt, 0xC7CFDA),
                (MatKey.Trim, 0xFF8A1F), (MatKey.TrimGlow, 0xFF8A1F), (MatKey.Ceiling, 0xDDE2E8), (MatKey.Concrete, 0xB3B8BF),
                (MatKey.MetalDark, 0x3D4552), (MatKey.MetalLight, 0xA7B1BE), (MatKey.Grate, 0x5D6673), (MatKey.Pipe, 0x8FB0D6));
            _theme[(int)LevelTheme.Factory] = Map(
                (MatKey.Floor, 0x6E655B), (MatKey.FloorAlt, 0x5B534A), (MatKey.Wall, 0x9A7F66), (MatKey.WallAlt, 0x6B5546),
                (MatKey.Trim, 0xFFB21F), (MatKey.TrimGlow, 0xFFA21F), (MatKey.Ceiling, 0x4A3E36), (MatKey.Concrete, 0x857A6E),
                (MatKey.MetalDark, 0x35312E), (MatKey.MetalLight, 0x968B7E), (MatKey.MetalRust, 0xB0582C), (MatKey.Grate, 0x3F3A35),
                (MatKey.Pipe, 0xB86A3A));
            _theme[(int)LevelTheme.Lab] = Map(
                (MatKey.Floor, 0xB3C2AE), (MatKey.FloorAlt, 0x94A792), (MatKey.Wall, 0xD8E3D0), (MatKey.WallAlt, 0x8EA08F),
                (MatKey.Trim, 0xB8D94A), (MatKey.TrimGlow, 0x9CFF5A), (MatKey.Ceiling, 0xC4CFBE), (MatKey.Concrete, 0x9EA897),
                (MatKey.MetalDark, 0x323B36), (MatKey.MetalLight, 0x95A39A), (MatKey.MetalRust, 0x8A6A3A), (MatKey.Grate, 0x46524A),
                (MatKey.Pipe, 0x7E9C8A));
            _theme[(int)LevelTheme.Research] = Map(
                (MatKey.Floor, 0xE1DDF0), (MatKey.FloorAlt, 0xC9C0E6), (MatKey.Wall, 0xF5F3FB), (MatKey.WallAlt, 0xBBAFE0),
                (MatKey.Trim, 0x9B6BFF), (MatKey.TrimGlow, 0xA77BFF), (MatKey.Ceiling, 0xE9E5F5), (MatKey.Concrete, 0xC9C4D8),
                (MatKey.MetalDark, 0x3A3552), (MatKey.MetalLight, 0xB9B3D1), (MatKey.MetalRust, 0x9A6A8A), (MatKey.Grate, 0x6A6290),
                (MatKey.Pipe, 0xC9BFEA));
            _theme[(int)LevelTheme.Chaos] = Map(
                (MatKey.Floor, 0x40434A), (MatKey.FloorAlt, 0x33363C), (MatKey.Wall, 0x52565E), (MatKey.WallAlt, 0x2F3137),
                (MatKey.Trim, 0xFF5A1F), (MatKey.TrimGlow, 0xFF4A1A), (MatKey.Ceiling, 0x2A2C31), (MatKey.Concrete, 0x56595F),
                (MatKey.MetalDark, 0x25272C), (MatKey.MetalLight, 0x6E737C), (MatKey.MetalRust, 0x8E3F22), (MatKey.Grate, 0x2B2D32),
                (MatKey.Pipe, 0x5A5F68));
            _theme[(int)LevelTheme.Menu] = Map(
                (MatKey.Floor, 0x2C313B), (MatKey.FloorAlt, 0x242933), (MatKey.Wall, 0x383E4A), (MatKey.WallAlt, 0x272C36),
                (MatKey.Trim, 0xFF8A1F), (MatKey.TrimGlow, 0xFF8A1F), (MatKey.Ceiling, 0x1E222A), (MatKey.Concrete, 0x3E434C),
                (MatKey.MetalDark, 0x22262E), (MatKey.MetalLight, 0x6C7480), (MatKey.Grate, 0x262A31), (MatKey.Pipe, 0x59616E));
            // campaign themes: Bank golden-dusk marble + glowing gold, Night midnight blue, Tower black glass + gold
            _theme[(int)LevelTheme.Bank] = Map(
                (MatKey.Floor, 0xE2D5BC), (MatKey.FloorAlt, 0xB9A27E), (MatKey.Wall, 0xF0E6D2), (MatKey.WallAlt, 0x7E5A3E),
                (MatKey.Trim, 0xE0A92E), (MatKey.TrimGlow, 0xFFC24A), (MatKey.Ceiling, 0xEDE2CC), (MatKey.Concrete, 0xC2B49C),
                (MatKey.MetalDark, 0x3B3026), (MatKey.MetalLight, 0xC4B192), (MatKey.MetalRust, 0x9A5E32), (MatKey.Grate, 0x4E4236),
                (MatKey.Pipe, 0xC9A35A), (MatKey.Marble, 0xEDE6D8), (MatKey.Velvet, 0xA3183C), (MatKey.Wood, 0x8E5A34));
            _theme[(int)LevelTheme.Night] = Map(
                (MatKey.Floor, 0x46506A), (MatKey.FloorAlt, 0x363E54), (MatKey.Wall, 0x58627E), (MatKey.WallAlt, 0x323A50),
                (MatKey.Trim, 0x6FA8FF), (MatKey.TrimGlow, 0x7FD8FF), (MatKey.Ceiling, 0x262C40), (MatKey.Concrete, 0x58607A),
                (MatKey.MetalDark, 0x1E2434), (MatKey.MetalLight, 0x7682A0), (MatKey.MetalRust, 0x6A4A46), (MatKey.Grate, 0x2A3044),
                (MatKey.Pipe, 0x55628A), (MatKey.Marble, 0x9AA2B8), (MatKey.Foliage, 0x3E6A5A), (MatKey.Wood, 0x6E5644));
            _theme[(int)LevelTheme.Tower] = Map(
                (MatKey.Floor, 0x2A2C34), (MatKey.FloorAlt, 0x1C1D23), (MatKey.Wall, 0x30323B), (MatKey.WallAlt, 0x16171C),
                (MatKey.Trim, 0xE0A92E), (MatKey.TrimGlow, 0xFFB830), (MatKey.Ceiling, 0x141519), (MatKey.Concrete, 0x44464F),
                (MatKey.MetalDark, 0x121317), (MatKey.MetalLight, 0x6A6D78), (MatKey.MetalRust, 0x7A3A2A), (MatKey.Grate, 0x1E1F25),
                (MatKey.Pipe, 0x464A58), (MatKey.BlackGlass, 0x0D1016), (MatKey.Marble, 0x2E2C33), (MatKey.Velvet, 0x8E1030));
        }

        static Dictionary<int, Color> Map(params (MatKey key, int rgb)[] entries)
        {
            var d = new Dictionary<int, Color>(entries.Length);
            foreach (var e in entries) d[(int)e.key] = H(e.rgb);
            return d;
        }
    }
}
