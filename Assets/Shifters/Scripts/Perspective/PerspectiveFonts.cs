// =====================================================================================
//  SHIFTERS - PerspectiveFonts.cs   [owned by the PERSPECTIVE agent]
//  Resolves the TMP font used by world-space labels/clues/signs: the UI module's generated
//  Barlow Condensed / Bungee assets in Resources/Fonts when present, else TMP's default font.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class PerspectiveFonts
    {
        static TMP_FontAsset _label, _display;
        static bool _labelTried, _displayTried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _label = _display = null; _labelTried = _displayTried = false; }

        /// <summary>Condensed bold label font (chips, clues, console screens).</summary>
        public static TMP_FontAsset Label
        {
            get
            {
                if (_label == null && !_labelTried) { _labelTried = true; _label = Load("Fonts/BarlowCondensed-Bold SDF", "Fonts/Barlow-Black SDF"); }
                return _label != null ? _label : TMP_Settings.defaultFontAsset;
            }
        }

        /// <summary>Display font (big EXIT sign, checkpoint numbers).</summary>
        public static TMP_FontAsset Display
        {
            get
            {
                if (_display == null && !_displayTried) { _displayTried = true; _display = Load("Fonts/Bungee-Regular SDF", "Fonts/Barlow-Black SDF"); }
                return _display != null ? _display : Label;
            }
        }

        static TMP_FontAsset Load(string a, string b)
        {
            var f = Resources.Load<TMP_FontAsset>(a);
            if (f == null) f = Resources.Load<TMP_FontAsset>(b);
            return f;
        }

        /// <summary>Creates a 3D TextMeshPro child (no wrapping, centered, no shadows). size ~ line height in meters.</summary>
        public static TextMeshPro MakeText(Transform parent, string name, string text, float size, Color color, Vector3 localPos, Quaternion localRot, bool display = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var t = go.AddComponent<TextMeshPro>();
            var font = display ? Display : Label;
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size * 10f;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, size * 8f), size * 1.6f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }
            return t;
        }
    }
}
