// =====================================================================================
//  SHIFTERS - FontLibrary.cs   (UI agent)
//  Runtime access to the generated TMP font assets in Resources/Fonts (built by
//  Editor/UI/FontAssetBuilder). Falls back to TMP_Settings.defaultFontAsset.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>The four SHIFTERS typefaces: Display (Bungee), Body (Barlow SemiBold), Heavy (Barlow Black), Label (Barlow Condensed Bold).</summary>
    public static class FontLibrary
    {
        public const string DisplayPath = "Fonts/Bungee-Regular SDF";
        public const string BodyPath = "Fonts/Barlow-SemiBold SDF";
        public const string HeavyPath = "Fonts/Barlow-Black SDF";
        public const string LabelPath = "Fonts/BarlowCondensed-Bold SDF";
        public const string LogoMaterialPath = "Fonts/Bungee-Regular SDF Logo";
        public const string ShadowMaterialPath = "Fonts/Bungee-Regular SDF Shadow";

        static TMP_FontAsset _display, _body, _heavy, _label;
        static Material _logo, _displayShadow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _display = _body = _heavy = _label = null; _logo = _displayShadow = null; }

        /// <summary>Bungee: logo, headers, banners.</summary>
        public static TMP_FontAsset Display => Load(ref _display, DisplayPath);
        /// <summary>Barlow SemiBold: body text.</summary>
        public static TMP_FontAsset Body => Load(ref _body, BodyPath);
        /// <summary>Barlow Black: numbers, names.</summary>
        public static TMP_FontAsset Heavy => Load(ref _heavy, HeavyPath);
        /// <summary>Barlow Condensed Bold: labels / caps.</summary>
        public static TMP_FontAsset Label => Load(ref _label, LabelPath);

        /// <summary>True when the generated SHIFTERS fonts are present (false = TMP default fallback).</summary>
        public static bool HasCustomFonts => Display != null && Display != TMP_Settings.defaultFontAsset;

        /// <summary>Outlined + drop-shadow material for the SHIFTERS logo (Bungee).</summary>
        public static Material LogoMaterial
        {
            get
            {
                if (_logo != null) return _logo;
                _logo = Resources.Load<Material>(LogoMaterialPath);
                if (_logo == null && Display != null && Display.material != null)
                {
                    _logo = new Material(Display.material) { name = "Logo (runtime)" };
                    _logo.EnableKeyword("OUTLINE_ON");
                    _logo.SetFloat("_OutlineWidth", 0.16f);
                    _logo.SetColor("_OutlineColor", new Color(0.09f, 0.05f, 0.02f, 1f));
                    _logo.EnableKeyword("UNDERLAY_ON");
                    _logo.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
                    _logo.SetFloat("_UnderlayOffsetX", 0.55f);
                    _logo.SetFloat("_UnderlayOffsetY", -0.65f);
                    _logo.SetFloat("_UnderlaySoftness", 0.25f);
                }
                return _logo;
            }
        }

        /// <summary>Display font material with a soft drop shadow (banners, big titles).</summary>
        public static Material DisplayShadow
        {
            get
            {
                if (_displayShadow != null) return _displayShadow;
                _displayShadow = Resources.Load<Material>(ShadowMaterialPath);
                if (_displayShadow != null) return _displayShadow;
                if (Display == null || Display.material == null) return null;
                _displayShadow = new Material(Display.material) { name = "DisplayShadow (runtime)" };
                _displayShadow.EnableKeyword("UNDERLAY_ON");
                _displayShadow.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
                _displayShadow.SetFloat("_UnderlayOffsetX", 0.4f);
                _displayShadow.SetFloat("_UnderlayOffsetY", -0.5f);
                _displayShadow.SetFloat("_UnderlaySoftness", 0.35f);
                return _displayShadow;
            }
        }

        static TMP_FontAsset Load(ref TMP_FontAsset slot, string path)
        {
            if (slot != null) return slot;
            slot = Resources.Load<TMP_FontAsset>(path);
            if (slot == null) slot = TMP_Settings.defaultFontAsset;
            return slot;
        }
    }
}
