using UnityEngine;

namespace Shifters
{
    public enum PuzzleLampColor { Off, Red, Green, Blue, Yellow }

    /// <summary>Indicator lamp: swaps its renderers' shared material between the standard light materials
    /// (red off / green on / blue / yellow warning). Materials are captured at build time (Palette) so lamps keep the
    /// theme they were built with.</summary>
    public class PuzzleLamp : MonoBehaviour
    {
        public Renderer[] Renderers = new Renderer[0];
        [Tooltip("Indexed by PuzzleLampColor. Filled by PuzzleVisuals.Lamp.")] public Material[] Palette = new Material[0];
        public PuzzleLampColor Current = PuzzleLampColor.Off;
        [System.NonSerialized] bool _applied;

        public void Set(PuzzleLampColor c)
        {
            if (_applied && c == Current) return;
            Current = c;
            _applied = true;
            var m = Get(c);
            for (int i = 0; i < Renderers.Length; i++) if (Renderers[i] != null) Renderers[i].sharedMaterial = m;
        }

        /// <summary>Green when on, red when off.</summary>
        public void Set(bool on) => Set(on ? PuzzleLampColor.Green : PuzzleLampColor.Red);

        /// <summary>Alternates between two colors (call every frame while blinking).</summary>
        public void Blink(PuzzleLampColor a, PuzzleLampColor b, float hz = 4f) => Set(Mathf.Repeat(Time.time * hz, 1f) < 0.5f ? a : b);

        Material Get(PuzzleLampColor c)
        {
            int i = (int)c;
            if (Palette != null && i < Palette.Length && Palette[i] != null) return Palette[i];
            return DefaultMaterial(c);
        }

        public static Material DefaultMaterial(PuzzleLampColor c)
        {
            switch (c)
            {
                case PuzzleLampColor.Red: return Kit.Mat(MatKey.LightRed);
                case PuzzleLampColor.Green: return Kit.Mat(MatKey.LightOn);
                case PuzzleLampColor.Blue: return Kit.Mat(MatKey.LightBlue);
                case PuzzleLampColor.Yellow: return MaterialLibrary.Colored(UIColors.AccentYellow, 0.3f, 2f);
                default: return Kit.Mat(MatKey.LightOff);
            }
        }

        public static Material[] DefaultPalette()
            => new[] { DefaultMaterial(PuzzleLampColor.Off), DefaultMaterial(PuzzleLampColor.Red), DefaultMaterial(PuzzleLampColor.Green), DefaultMaterial(PuzzleLampColor.Blue), DefaultMaterial(PuzzleLampColor.Yellow) };
    }
}
