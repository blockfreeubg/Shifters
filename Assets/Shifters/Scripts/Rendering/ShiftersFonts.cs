// =====================================================================================
//  SHIFTERS - ShiftersFonts.cs   (RENDERING)
//  TMP font lookup for world-space text (signs, posters, floating text). Uses the UI agent's font
//  assets in Resources/Fonts when present, else TMP's default font. Never returns null if TMP is set up.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class ShiftersFonts
    {
        static TMP_FontAsset _display, _label, _numbers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _display = _label = _numbers = null; }

        /// <summary>Chunky header font (Bungee) - signs, logos, floating text.</summary>
        public static TMP_FontAsset Display => _display != null ? _display : (_display = Load("Fonts/Bungee-Regular SDF"));
        /// <summary>Condensed bold caps (Barlow Condensed Bold) - labels, poster body text.</summary>
        public static TMP_FontAsset Label => _label != null ? _label : (_label = Load("Fonts/BarlowCondensed-Bold SDF"));
        /// <summary>Heavy numbers (Barlow Black).</summary>
        public static TMP_FontAsset Numbers => _numbers != null ? _numbers : (_numbers = Load("Fonts/Barlow-Black SDF"));

        static TMP_FontAsset Load(string path)
        {
            var f = Resources.Load<TMP_FontAsset>(path);
            if (f == null) f = TMP_Settings.defaultFontAsset;
            if (f == null) f = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return f;
        }
    }
}
