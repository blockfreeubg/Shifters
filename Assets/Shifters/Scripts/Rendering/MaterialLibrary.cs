// =====================================================================================
//  SHIFTERS - MaterialLibrary.cs   [CONTRACT API - implementation owned by the RENDERING agent]
//  Central access to every material. Editor builder bakes them as assets into
//  Assets/Shifters/Resources/Materials/<Theme>_<Key>.mat; at runtime Get() loads from Resources
//  and falls back to creating a material from the Shifters/StylizedLit shader (or URP/Lit).
//  NEVER leave a renderer with Unity's default material.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shifters
{
    public enum MatKey
    {
        // structure
        Floor, FloorAlt, Wall, WallAlt, Trim, Ceiling, Concrete, MetalDark, MetalLight, MetalRust, Grate, Pipe,
        // signage / hazard
        HazardYellow, HazardBlack, SafetyOrange, WarningRed, SignWhite, Poster,
        // puzzle language (consistent in every level)
        PuzzleBody, PuzzleAccent, LightOff, LightOn, LightRed, LightBlue, Screen,
        // props
        Wood, CrateHeavy, Barrel, Rubber, Glass, Plastic, Cardboard,
        // fx / special
        Hologram, Acid, Laser, Void, Glow, White, Black, Shadow,
        // (rendering additions - appended) yellow/black diagonal stripes, emissive theme accent strip, plants
        HazardStripes, TrimGlow, Foliage,
        // (campaign appends, W0) bank marble, grey-immune gold, velvet ropes, ghost hologram, ME TOWER glass, the moon.
        Marble, Gold, Velvet, GhostGlow, BlackGlass, Moon
    }

    /// <summary>Shader property ids shared by the rendering module (and anyone driving our shaders).</summary>
    public static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int RevealColor = Shader.PropertyToID("_RevealColor");
        public static readonly int RevealStrength = Shader.PropertyToID("_RevealStrength");
        public static readonly int RevealPulse = Shader.PropertyToID("_RevealPulse");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int SpecularStrength = Shader.PropertyToID("_SpecularStrength");
        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int RimStrength = Shader.PropertyToID("_RimStrength");
        public static readonly int ShadowTint = Shader.PropertyToID("_ShadowTint");
        public static readonly int ShadeSteps = Shader.PropertyToID("_ShadeSteps");
        public static readonly int ShadeSoftness = Shader.PropertyToID("_ShadeSoftness");
        public static readonly int WrapLighting = Shader.PropertyToID("_WrapLighting");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int GridColor = Shader.PropertyToID("_GridColor");
        public static readonly int GridScale = Shader.PropertyToID("_GridScale");
        public static readonly int GridStrength = Shader.PropertyToID("_GridStrength");
        public static readonly int GridWidth = Shader.PropertyToID("_GridWidth");
        public static readonly int GridGlow = Shader.PropertyToID("_GridGlow");
        public static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");
        public static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        public static readonly int StripeColor = Shader.PropertyToID("_StripeColor");
        public static readonly int StripeWidth = Shader.PropertyToID("_StripeWidth");
        public static readonly int StripeStrength = Shader.PropertyToID("_StripeStrength");
        public static readonly int Surface = Shader.PropertyToID("_Surface");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        public static readonly int Cull = Shader.PropertyToID("_Cull");
        public static readonly int Alpha = Shader.PropertyToID("_Alpha");
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int Glow = Shader.PropertyToID("_Glow");
        public static readonly int ScanlineDensity = Shader.PropertyToID("_ScanlineDensity");
        public static readonly int FresnelStrength = Shader.PropertyToID("_FresnelStrength");
        public static readonly int FlickerStrength = Shader.PropertyToID("_FlickerStrength");
        public static readonly int MinAlpha = Shader.PropertyToID("_MinAlpha");
        /// <summary>WORLD COLOR per-material immunity (1 = keeps full color in the grey world). See WorldColor.</summary>
        public static readonly int GreyImmune = Shader.PropertyToID("_GreyImmune");
    }

    public static class MaterialLibrary
    {
        public const string LitShaderName = "Shifters/StylizedLit";
        public const string HologramShaderName = "Shifters/Hologram";
        public const string SkyShaderName = "Shifters/GradientSky";
        public const string ParticleShaderName = "Shifters/FxParticle";
        public const string UnlitShaderName = "Shifters/Unlit";
        public const string AcidShaderName = "Shifters/Acid";

        static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Shader> _shaders = new Dictionary<string, Shader>();
        static readonly HashSet<Material> _sharedIds = new HashSet<Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _cache.Clear(); _shaders.Clear(); _sharedIds.Clear(); }

        /// <summary>Drops every cached material (the baker calls this after writing assets).</summary>
        public static void ClearCache() { _cache.Clear(); _sharedIds.Clear(); }

        /// <summary>True for materials shared by many renderers: MaterialLibrary's cache or a persistent asset
        /// (WorldColor.MakeImmune swaps those for immune copies instead of editing them).</summary>
        public static bool IsShared(Material m)
        {
            if (m == null) return false;
            if (_sharedIds.Contains(m)) return true;
            if (m.name.StartsWith("inst_")) return false;
            foreach (var kv in _cache) if (kv.Value == m) { _sharedIds.Add(m); return true; }
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(m)) return true;
#endif
            return Resources.Load<Material>("Materials/" + m.name) == m;
        }

        /// <summary>True when the key keeps full color in the grey world (puzzle language, hazards, lights, Gold...).</summary>
        public static bool IsGreyImmune(MatKey key) => Palettes.IsGreyImmune(key);

        /// <summary>Lit stylized shader (Shifters/StylizedLit) with URP/Lit fallback.</summary>
        public static Shader LitShader
        {
            get
            {
                var s = FindShader(LitShaderName, "Materials/Training_White");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
                return s;
            }
        }

        public static Shader HologramShader => Or(FindShader(HologramShaderName, "Materials/Training_Hologram"), "Universal Render Pipeline/Unlit");
        public static Shader SkyShader => Or(FindShader(SkyShaderName, "Materials/Sky_Training"), "Skybox/Procedural");
        public static Shader ParticleShader => Or(FindShader(ParticleShaderName, "Materials/Fx_ParticleAlpha"), "Universal Render Pipeline/Particles/Unlit");
        public static Shader UnlitShader => Or(FindShader(UnlitShaderName, "Materials/Fx_Unlit"), "Universal Render Pipeline/Unlit");
        /// <summary>Shifters/Acid (null if missing: MatKey.Acid then falls back to an emissive StylizedLit material).</summary>
        public static Shader AcidShader => FindShader(AcidShaderName, "Materials/Lab_Acid");

        static Shader Or(Shader s, string fallbackName) => s != null ? s : Shader.Find(fallbackName);

        /// <summary>Shader.Find with a player-build-safe fallback: the shader of a baked Resources material.</summary>
        static Shader FindShader(string name, string fallbackMaterial)
        {
            if (_shaders.TryGetValue(name, out var s) && s != null) return s;
            s = Shader.Find(name);
            if (s == null && fallbackMaterial != null)
            {
                var m = Resources.Load<Material>(fallbackMaterial);
                if (m != null && m.shader != null && m.shader.name == name) s = m.shader;
            }
            if (s != null) _shaders[name] = s;
            return s;
        }

        /// <summary>Themed material for a key: cache, then Resources/Materials/&lt;Theme&gt;_&lt;Key&gt;, then built at runtime.</summary>
        public static Material Get(MatKey key, LevelTheme theme = LevelTheme.Training)
        {
            string id = theme + "_" + key;
            if (_cache.TryGetValue(id, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + id);
            if (m == null) m = Build(key, theme);
            _cache[id] = m;
            return m;
        }

        /// <summary>Builds a fresh (uncached) material for key/theme from the palette spec. Used by the baker.</summary>
        public static Material Build(MatKey key, LevelTheme theme)
        {
            var spec = Palettes.Spec(key, theme);
            Material m;
            if (spec.Surface == MatSurface.Hologram)
            {
                m = new Material(HologramShader) { name = theme + "_" + key };
                ConfigureHologram(m, spec.Color, false);
            }
            else if (spec.Surface == MatSurface.Unlit)
            {
                m = new Material(UnlitShader) { name = theme + "_" + key };
                m.SetColor(ShaderIds.BaseColor, spec.Color);
            }
            else if (spec.Surface == MatSurface.Acid && AcidShader != null)
            {
                m = new Material(AcidShader) { name = theme + "_" + key };
                ConfigureAcid(m, spec.Color);
            }
            else
            {
                m = new Material(LitShader) { name = theme + "_" + key };
                ApplySpec(m, spec);
            }
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Palette color of a key in a theme (handy for vertex-colored merged meshes).</summary>
        public static Color ColorOf(MatKey key, LevelTheme theme) => Palettes.Color(key, theme);

        /// <summary>Writes a MatSpec into a Shifters/StylizedLit material (safe on other shaders: only sets existing props).</summary>
        public static void ApplySpec(Material m, MatSpec s)
        {
            SetC(m, ShaderIds.BaseColor, s.Color);
            SetF(m, ShaderIds.Smoothness, s.Smoothness);
            SetF(m, ShaderIds.SpecularStrength, s.Specular);
            SetF(m, ShaderIds.RimStrength, s.RimStrength);
            SetF(m, ShaderIds.RimPower, s.RimPower);
            SetC(m, ShaderIds.RimColor, s.RimColor);
            SetF(m, ShaderIds.ShadeSteps, s.ShadeSteps);
            SetF(m, ShaderIds.ShadeSoftness, s.ShadeSoftness);
            SetF(m, ShaderIds.WrapLighting, s.Wrap);
            SetC(m, ShaderIds.ShadowTint, s.ShadowTint);
            SetF(m, ShaderIds.GridStrength, s.GridStrength);
            SetF(m, ShaderIds.GridScale, s.GridScale);
            SetF(m, ShaderIds.GridWidth, s.GridWidth);
            SetF(m, ShaderIds.GridGlow, s.GridGlow);
            SetC(m, ShaderIds.GridColor, s.GridColor);
            SetF(m, ShaderIds.NoiseStrength, s.Noise);
            SetF(m, ShaderIds.NoiseScale, s.NoiseScale);
            SetF(m, ShaderIds.StripeStrength, s.StripeStrength);
            SetF(m, ShaderIds.StripeWidth, s.StripeWidth);
            SetC(m, ShaderIds.StripeColor, s.StripeColor);
            SetEmission(m, s.Emission);
            SetF(m, ShaderIds.GreyImmune, s.GreyImmune ? 1f : 0f);
            SetTransparent(m, s.Surface == MatSurface.Transparent);
            if (!s.CastShadows) m.SetShaderPassEnabled("ShadowCaster", false);
        }

        /// <summary>Sets HDR emission on StylizedLit (no keyword needed) and URP/Lit (_EMISSION keyword).</summary>
        public static void SetEmission(Material m, Color hdr)
        {
            if (m == null || !m.HasProperty(ShaderIds.EmissionColor)) return;
            m.SetColor(ShaderIds.EmissionColor, hdr);
            bool on = hdr.maxColorComponent > 0.0001f;
            if (on) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = on ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        /// <summary>Switches a StylizedLit material between opaque and alpha-blended transparent.</summary>
        public static void SetTransparent(Material m, bool transparent)
        {
            if (m == null || !m.HasProperty(ShaderIds.SrcBlend)) return;
            if (transparent)
            {
                m.SetFloat(ShaderIds.Surface, 1f);
                m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.SrcAlpha);
                m.SetFloat(ShaderIds.DstBlend, (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat(ShaderIds.ZWrite, 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("DepthNormals", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                m.SetFloat(ShaderIds.Surface, 0f);
                m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.One);
                m.SetFloat(ShaderIds.DstBlend, (float)BlendMode.Zero);
                m.SetFloat(ShaderIds.ZWrite, 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = -1;
                m.SetShaderPassEnabled("DepthOnly", true);
                m.SetShaderPassEnabled("DepthNormals", true);
                m.SetShaderPassEnabled("ShadowCaster", true);
            }
        }

        /// <summary>Cached opaque lit material of a flat color (characters, one-off props).</summary>
        public static Material Colored(Color color, float smoothness = 0.25f, float emission = 0f)
        {
            string id = "c_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + smoothness.ToString("0.00") + "_" + emission.ToString("0.0");
            if (_cache.TryGetValue(id, out var m) && m != null) return m;
            m = NewLit(id, color, smoothness);
            if (emission > 0f) SetEmission(m, color * emission);
            _cache[id] = m;
            return m;
        }

        /// <summary>Unique (non-shared) instance for per-character tinting/highlighting.</summary>
        /// <remarks>Campaign wave: instances are GREY-IMMUNE by default (the interns stay vivid in the grey world);
        /// pass greyImmune false for villain-owned things.</remarks>
        public static Material Instance(Color color, float smoothness = 0.25f) => Instance(color, smoothness, true);

        public static Material Instance(Color color, float smoothness, bool greyImmune)
        {
            var m = NewLit("inst_" + ColorUtility.ToHtmlStringRGB(color), color, smoothness);
            SetF(m, ShaderIds.GreyImmune, greyImmune ? 1f : 0f);
            return m;
        }

        /// <summary>Cached GREY-IMMUNE opaque lit material of a flat color (Reserve canisters, paint, gold props).</summary>
        public static Material ImmuneColored(Color color, float smoothness = 0.25f, float emission = 0f)
        {
            string id = "ci_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + smoothness.ToString("0.00") + "_" + emission.ToString("0.0");
            if (_cache.TryGetValue(id, out var m) && m != null) return m;
            m = NewLit(id, color, smoothness);
            if (emission > 0f) SetEmission(m, color * emission);
            SetF(m, ShaderIds.GreyImmune, 1f);
            _cache[id] = m;
            return m;
        }

        /// <summary>Simple transparent material (glass, holograms, scan pulses): StylizedLit alpha-blended with a
        /// fresnel edge. Alpha comes from color.a. Cached per color.</summary>
        public static Material Transparent(Color color)
        {
            string id = "t_" + ColorUtility.ToHtmlStringRGBA(color);
            if (_cache.TryGetValue(id, out var m) && m != null) return m;
            m = NewLit(id, color, 0.85f);
            if (m.HasProperty(ShaderIds.RimStrength)) { m.SetFloat(ShaderIds.RimStrength, 0.7f); m.SetFloat(ShaderIds.RimPower, 2.5f); }
            SetTransparent(m, true);
            _cache[id] = m;
            return m;
        }

        /// <summary>Cached Shifters/Hologram material (alpha or additive). color.a = opacity.</summary>
        public static Material Hologram(Color color, bool additive = false)
        {
            string id = "h_" + ColorUtility.ToHtmlStringRGBA(color) + (additive ? "_add" : "");
            if (_cache.TryGetValue(id, out var m) && m != null) return m;
            m = new Material(HologramShader) { name = id };
            ConfigureHologram(m, color, additive);
            _cache[id] = m;
            return m;
        }

        /// <summary>Cached Shifters/Unlit material: flat color (x glow for HDR bloom), vertex colors multiply.</summary>
        public static Material Unlit(Color color, float glow = 1f)
        {
            string id = "u_" + ColorUtility.ToHtmlStringRGB(color) + "_" + glow.ToString("0.0");
            if (_cache.TryGetValue(id, out var m) && m != null) return m;
            m = new Material(UnlitShader) { name = id, enableInstancing = true };
            m.SetColor(ShaderIds.BaseColor, color);
            if (m.HasProperty(ShaderIds.Glow)) m.SetFloat(ShaderIds.Glow, glow);
            _cache[id] = m;
            return m;
        }

        /// <summary>Shared unlit vertex-color glow material (backdrop windows, emissive strips). Baked as
        /// Resources/Materials/Fx_UnlitGlow so scenes built in the editor reference an asset.</summary>
        public static Material UnlitGlowMaterial
        {
            get
            {
                const string id = "Fx_UnlitGlow";
                if (_cache.TryGetValue(id, out var m) && m != null) return m;
                m = Resources.Load<Material>("Materials/" + id);
                if (m == null) m = BuildSpecial(id);
                _cache[id] = m;
                return m;
            }
        }

        /// <summary>Builds the non-themed helper materials baked next to the theme set (Fx_*): they also make sure the
        /// FxParticle / Unlit shaders are included in player builds.</summary>
        public static Material BuildSpecial(string id)
        {
            Material m;
            switch (id)
            {
                case "Fx_UnlitGlow":
                    m = new Material(UnlitShader) { name = id, enableInstancing = true };
                    m.SetColor(ShaderIds.BaseColor, Color.white);
                    if (m.HasProperty(ShaderIds.Glow)) m.SetFloat(ShaderIds.Glow, 2.2f);
                    return m;
                case "Fx_Unlit":
                    m = new Material(UnlitShader) { name = id, enableInstancing = true };
                    m.SetColor(ShaderIds.BaseColor, Color.white);
                    return m;
                case "Fx_ParticleAdd":
                case "Fx_ParticleAlpha":
                    m = new Material(ParticleShader) { name = id };
                    if (m.HasProperty(ShaderIds.SrcBlend))
                    {
                        m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.SrcAlpha);
                        m.SetFloat(ShaderIds.DstBlend, id == "Fx_ParticleAdd" ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                    }
                    return m;
                default:
                    return new Material(LitShader) { name = id };
            }
        }

        /// <summary>Ids of the special (non-themed) baked materials.</summary>
        public static readonly string[] SpecialIds = { "Fx_UnlitGlow", "Fx_Unlit", "Fx_ParticleAdd", "Fx_ParticleAlpha" };

        /// <summary>Sets up a Shifters/Acid material from a palette color (sRGB): saturated body, dark troughs, HDR crest
        /// glow and bubble rims derived from its hue (HDR values authored in linear space, so bloom stays controlled).</summary>
        public static void ConfigureAcid(Material m, Color c)
        {
            if (m == null) return;
            Color.RGBToHSV(c, out float h, out float s, out _);
            SetC(m, ShaderIds.BaseColor, Color.HSVToRGB(h, Mathf.Max(s, 0.78f), 0.9f));
            SetC(m, AcidIds.DeepColor, Color.HSVToRGB(Mathf.Repeat(h + 0.02f, 1f), 0.95f, 0.36f));
            SetC(m, AcidIds.GlowColor, LinearHdr(Color.HSVToRGB(h, 0.82f, 1f), 2.3f));
            SetC(m, AcidIds.FoamColor, LinearHdr(Color.HSVToRGB(Mathf.Repeat(h - 0.07f, 1f), 0.55f, 1f), 2.2f));
            SetF(m, AcidIds.CellScale, 0.9f);
            SetF(m, AcidIds.FlowSpeed, 0.45f);
            SetF(m, AcidIds.BubbleDensity, 1.5f);
            SetF(m, AcidIds.BubbleRate, 0.5f);
            SetF(m, AcidIds.RiseSpeed, 0.35f);
            SetF(m, AcidIds.Emission, 1f);
            SetF(m, AcidIds.FresnelGlow, 0.7f);
            SetF(m, AcidIds.Glint, 1.2f);
            m.renderQueue = -1;
            m.enableInstancing = true;
        }

        /// <summary>sRGB color times a LINEAR intensity, returned in the gamma space SetColor expects.</summary>
        static Color LinearHdr(Color srgb, float intensity)
        {
            var lin = srgb.linear * intensity;
            lin.a = 1f;
            return lin.gamma;
        }

        static class AcidIds
        {
            public static readonly int DeepColor = Shader.PropertyToID("_DeepColor");
            public static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
            public static readonly int FoamColor = Shader.PropertyToID("_FoamColor");
            public static readonly int CellScale = Shader.PropertyToID("_CellScale");
            public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
            public static readonly int BubbleDensity = Shader.PropertyToID("_BubbleDensity");
            public static readonly int BubbleRate = Shader.PropertyToID("_BubbleRate");
            public static readonly int RiseSpeed = Shader.PropertyToID("_RiseSpeed");
            public static readonly int Emission = Shader.PropertyToID("_Emission");
            public static readonly int FresnelGlow = Shader.PropertyToID("_FresnelGlow");
            public static readonly int Glint = Shader.PropertyToID("_Glint");
        }

        /// <summary>Sets up a Shifters/Hologram material.</summary>
        public static void ConfigureHologram(Material m, Color color, bool additive)
        {
            m.SetColor(ShaderIds.BaseColor, color);
            if (m.HasProperty(ShaderIds.SrcBlend))
            {
                m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.SrcAlpha);
                m.SetFloat(ShaderIds.DstBlend, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            }
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Material NewLit(string name, Color color, float smoothness)
        {
            var m = new Material(LitShader) { name = name, enableInstancing = true };
            var spec = MatSpec.Default(color);
            spec.Smoothness = smoothness;
            spec.RimStrength = 0.22f;
            spec.Noise = 0f;
            ApplySpec(m, spec);
            if (color.a < 0.999f) SetTransparent(m, true);
            return m;
        }

        static void SetC(Material m, int id, Color c) { if (m.HasProperty(id)) m.SetColor(id, c); }
        static void SetF(Material m, int id, float f) { if (m.HasProperty(id)) m.SetFloat(id, f); }

        public static Color DefaultColor(MatKey key)
        {
            switch (key)
            {
                case MatKey.Floor: return UIColors.Hex(0xC9CED6);
                case MatKey.FloorAlt: return UIColors.Hex(0xAEB6C2);
                case MatKey.Wall: return UIColors.Hex(0xE4E7EC);
                case MatKey.WallAlt: return UIColors.Hex(0x8E97A4);
                case MatKey.Trim: return UIColors.Hex(0xFF8A1F);
                case MatKey.Ceiling: return UIColors.Hex(0xD5D9DF);
                case MatKey.Concrete: return UIColors.Hex(0x9AA0A8);
                case MatKey.MetalDark: return UIColors.Hex(0x3A414C);
                case MatKey.MetalLight: return UIColors.Hex(0x8C96A3);
                case MatKey.MetalRust: return UIColors.Hex(0xA4583A);
                case MatKey.Grate: return UIColors.Hex(0x59616C);
                case MatKey.Pipe: return UIColors.Hex(0x8FA3B8);
                case MatKey.HazardYellow: return UIColors.Hex(0xFFC93C);
                case MatKey.HazardBlack: return UIColors.Hex(0x1E1F24);
                case MatKey.SafetyOrange: return UIColors.Hex(0xFF8A1F);
                case MatKey.WarningRed: return UIColors.Hex(0xE8394B);
                case MatKey.SignWhite: return UIColors.Hex(0xF4F6F8);
                case MatKey.Poster: return UIColors.Hex(0xF2EEE6);
                case MatKey.PuzzleBody: return UIColors.Hex(0x2EC4B6);
                case MatKey.PuzzleAccent: return UIColors.Hex(0x1B8C83);
                case MatKey.LightOff: return UIColors.Hex(0x5A1E24);
                case MatKey.LightOn: return UIColors.Hex(0x3DDC97);
                case MatKey.LightRed: return UIColors.Hex(0xFF4D5E);
                case MatKey.LightBlue: return UIColors.Hex(0x3DE0FF);
                case MatKey.Screen: return UIColors.Hex(0x1D3B4F);
                case MatKey.Wood: return UIColors.Hex(0xC08A55);
                case MatKey.CrateHeavy: return UIColors.Hex(0x56627A);
                case MatKey.Barrel: return UIColors.Hex(0x3C7DD9);
                case MatKey.Rubber: return UIColors.Hex(0x2A2A2E);
                case MatKey.Glass: return new Color(0.7f, 0.9f, 1f, 0.3f);
                case MatKey.Plastic: return UIColors.Hex(0xE9ECEF);
                case MatKey.Cardboard: return UIColors.Hex(0xB98D5D);
                case MatKey.Hologram: return new Color(0.69f, 0.49f, 1f, 0.5f);
                case MatKey.Acid: return UIColors.Hex(0x7CFF4D);
                case MatKey.Laser: return UIColors.Hex(0xFF2D3D);
                case MatKey.Void: return UIColors.Hex(0x07080B);
                case MatKey.Glow: return UIColors.Hex(0xFFF6E0);
                case MatKey.Black: return UIColors.Hex(0x15161A);
                case MatKey.Shadow: return new Color(0f, 0f, 0f, 0.45f);
                case MatKey.HazardStripes: return UIColors.Hex(0xFFC93C);
                case MatKey.TrimGlow: return UIColors.Hex(0xFF8A1F);
                case MatKey.Foliage: return UIColors.Hex(0x5DAA4A);
                // campaign appends (W0 interim looks; rendering refines them)
                case MatKey.Marble: return UIColors.Hex(0xE6DFD2);
                case MatKey.Gold: return UIColors.Hex(0xF5B82E);
                case MatKey.Velvet: return UIColors.Hex(0xA3183C);
                case MatKey.GhostGlow: return new Color(0.72f, 0.96f, 1f, 0.42f);
                case MatKey.BlackGlass: return UIColors.Hex(0x0F1218);
                case MatKey.Moon: return UIColors.Hex(0xF4F0DC);
                default: return Color.white;
            }
        }
    }
}
