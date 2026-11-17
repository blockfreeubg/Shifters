// =====================================================================================
//  SHIFTERS - CharacterMaterials.cs   (VISUALS module)
//  Per-character material INSTANCES (so highlight / desaturate never leak to other characters).
//  Works with Shifters/StylizedLit (_RevealColor/_RevealStrength) and falls back to URP/Lit
//  emission when the stylized shader is not available.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class CharacterMaterials
    {
        public Material Primary, Secondary, Skin, Hair, Brow, Shoe, Accent, EyeWhite, Pupil, Glint, Mouth, Coat, Dark, Gold, Glow;
        public Color AccentColor { get; private set; }

        readonly List<Material> _body = new List<Material>();     // highlight + desaturate
        readonly List<Color> _bodyBase = new List<Color>();
        readonly List<Material> _all = new List<Material>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int RevealColorId = Shader.PropertyToID("_RevealColor");
        static readonly int RevealStrengthId = Shader.PropertyToID("_RevealStrength");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int CharacterFillId = Shader.PropertyToID("_CharacterFill");
        /// <summary>(polish) Minimum light level of every intern material in dark rooms (StylizedLit _CharacterFill):
        /// a camera-side fill that only kicks in where the scene leaves the character darker than this.</summary>
        public const float DarkFill = 0.42f;

        public bool Desaturated { get; private set; }
        public float HighlightStrength { get; private set; }
        public Color HighlightColor { get; private set; }

        public CharacterMaterials(CharacterProfile p)
        {
            var b = p.Body;
            AccentColor = PickAccent(p.PrimaryColor);
            Primary = Body(p.PrimaryColor, 0.3f);
            Secondary = Body(p.SecondaryColor, 0.25f);
            Skin = Body(b.SkinTone, 0.35f);
            Hair = Body(b.HairColor, 0.3f);
            Brow = Body(b.HairColor * 0.55f + new Color(0.02f, 0.02f, 0.02f, 0.45f), 0.2f);
            Color shoe = p.Archetype == Archetype.Sprinter ? AccentColor
                       : p.Archetype == Archetype.Brute ? new Color(0.2f, 0.15f, 0.12f) : new Color(0.38f, 0.24f, 0.16f);
            Shoe = Body(shoe, 0.35f);
            Accent = Body(AccentColor, 0.4f);
            Coat = Body(new Color(0.95f, 0.96f, 0.98f), 0.3f);
            Dark = Body(new Color(0.13f, 0.13f, 0.16f), 0.5f);
            Gold = Body(new Color(1f, 0.77f, 0.2f), 0.7f);
            EyeWhite = Plain(new Color(0.98f, 0.98f, 0.97f), 0.7f);
            Pupil = Plain(new Color(0.06f, 0.06f, 0.08f), 0.8f);
            Mouth = Plain(new Color(0.17f, 0.035f, 0.05f), 0.35f);   // near-black red: reads on every skin tone
            Glint = Plain(Color.white, 0.9f); SetEmission(Glint, Color.white * 0.8f);
            Glow = Plain(AccentColor, 0.6f); SetEmission(Glow, AccentColor * 1.6f);
        }

        /// <summary>(campaign) Extra per-character material instance, destroyed with the others. body = takes part in
        /// highlight/desaturate.</summary>
        public Material Extra(Color c, float smooth, bool body = true) => body ? Body(c, smooth) : Plain(c, smooth);
        /// <summary>(campaign) Register an externally created material so Destroy() frees it.</summary>
        public void Track(Material m) { if (m != null && !_all.Contains(m)) _all.Add(m); }

        Material Body(Color c, float smooth) { c.a = 1f; var m = Fill(MaterialLibrary.Instance(c, smooth)); _body.Add(m); _bodyBase.Add(c); _all.Add(m); return m; }
        Material Plain(Color c, float smooth) { var m = Fill(MaterialLibrary.Instance(c, smooth)); _all.Add(m); return m; }
        static Material Fill(Material m) { if (m != null && m.HasProperty(CharacterFillId)) m.SetFloat(CharacterFillId, DarkFill); return m; }

        public static void SetEmission(Material m, Color c)
        {
            if (m == null || !m.HasProperty(EmissionId)) return;
            m.EnableKeyword("_EMISSION");
            m.SetColor(EmissionId, c);
        }

        /// <summary>Bright accent that separates from the primary color (hue complement, controlled saturation).</summary>
        public static Color PickAccent(Color primary)
        {
            Color.RGBToHSV(primary, out float h, out float s, out float v);
            if (s < 0.25f) return new Color(1f, 0.8f, 0.2f);
            float h2 = (h + 0.5f) % 1f;
            // avoid muddy complements (dark blues/purples) - nudge toward friendlier hues
            if (h2 > 0.62f && h2 < 0.8f) h2 = 0.56f;
            return Color.HSVToRGB(h2, 0.72f, 0.98f);
        }

        /// <summary>Greys out every body material (KO / Lost / Captured).</summary>
        public void SetDesaturated(bool on)
        {
            Desaturated = on;
            for (int i = 0; i < _body.Count; i++)
            {
                var c = _bodyBase[i];
                if (on)
                {
                    float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    var g = new Color(l * 0.92f, l * 0.95f, l * 1.02f, 1f);
                    c = Color.Lerp(c, g, 0.88f) * 0.78f; c.a = 1f;
                }
                if (_body[i] != null) _body[i].SetColor(BaseColorId, c);
            }
        }

        /// <summary>Rim/emission highlight on all body materials (strength 0 = off).</summary>
        public void SetHighlight(Color color, float strength)
        {
            strength = Mathf.Max(0f, strength);
            HighlightColor = color; HighlightStrength = strength;
            for (int i = 0; i < _body.Count; i++)
            {
                var m = _body[i]; if (m == null) continue;
                if (m.HasProperty(RevealStrengthId))
                {
                    m.SetColor(RevealColorId, color);
                    m.SetFloat(RevealStrengthId, strength);
                }
                else if (m.HasProperty(EmissionId))
                {
                    if (strength > 0.001f) { m.EnableKeyword("_EMISSION"); m.SetColor(EmissionId, color * (0.45f * strength)); }
                    else { m.SetColor(EmissionId, Color.black); m.DisableKeyword("_EMISSION"); }
                }
            }
        }

        public void Destroy()
        {
            for (int i = 0; i < _all.Count; i++) if (_all[i] != null) Kit.SafeDestroy(_all[i]);
            _all.Clear(); _body.Clear(); _bodyBase.Clear();
        }
    }
}
