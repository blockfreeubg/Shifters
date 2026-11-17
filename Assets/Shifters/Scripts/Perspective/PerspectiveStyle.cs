// =====================================================================================
//  SHIFTERS - PerspectiveStyle.cs   [owned by the PERSPECTIVE agent]
//  Colors, short tags and names for perspective masks, plus the blended/alternating tint used
//  for viewers with several perks (e.g. a Brute holding the gun sees STR + GUN).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class PerspectiveStyle
    {
        /// <summary>Single perspectives in display order.</summary>
        public static readonly PerspectiveMask[] Order = { PerspectiveMask.Strength, PerspectiveMask.Speed, PerspectiveMask.Intellect, PerspectiveMask.Weapon };

        /// <summary>Seconds each color holds before blending to the next one (multi-perk viewers).</summary>
        public const float AlternatePeriod = 2.2f;

        public static Color ColorOf(PerspectiveMask m) => UIColors.ForPerspective(m);

        /// <summary>Short chip tag: STR / SPD / IQ / GUN.</summary>
        public static string Tag(PerspectiveMask single)
        {
            if ((single & PerspectiveMask.Strength) != 0) return "STR";
            if ((single & PerspectiveMask.Speed) != 0) return "SPD";
            if ((single & PerspectiveMask.Intellect) != 0) return "IQ";
            if ((single & PerspectiveMask.Weapon) != 0) return "GUN";
            return "";
        }

        /// <summary>"STRENGTH VIEW", "TURBO VIEW", "BIG BRAIN VIEW", "TRIGGER VIEW".</summary>
        public static string ViewName(PerspectiveMask single)
        {
            if ((single & PerspectiveMask.Strength) != 0) return "STRENGTH VIEW";
            if ((single & PerspectiveMask.Speed) != 0) return "TURBO VIEW";
            if ((single & PerspectiveMask.Intellect) != 0) return "BIG BRAIN VIEW";
            if ((single & PerspectiveMask.Weapon) != 0) return "TRIGGER VIEW";
            return "NORMAL VIEW";
        }

        public static int Count(PerspectiveMask m)
        {
            int n = 0;
            for (int i = 0; i < Order.Length; i++) if ((m & Order[i]) != 0) n++;
            return n;
        }

        /// <summary>n-th set perspective of the mask in display order (None if out of range).</summary>
        public static PerspectiveMask Nth(PerspectiveMask m, int n)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                if ((m & Order[i]) == 0) continue;
                if (n == 0) return Order[i];
                n--;
            }
            return PerspectiveMask.None;
        }

        /// <summary>First perspective shared by both masks (what a viewer "sees this thing as").</summary>
        public static PerspectiveMask FirstShared(PerspectiveMask a, PerspectiveMask b) => Nth(a & b, 0);

        /// <summary>Tint for a mask at time t: a single color, or alternating smooth blends between all set colors.</summary>
        public static Color Tint(PerspectiveMask m, float time)
        {
            int n = Count(m);
            if (n == 0) return Color.white;
            if (n == 1) return ColorOf(m);
            float f = time / AlternatePeriod;
            int i = Mathf.FloorToInt(f);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - i - 0.7f) / 0.3f)); // hold 70%, blend 30%
            var a = ColorOf(Nth(m, ((i % n) + n) % n));
            var b = ColorOf(Nth(m, (((i + 1) % n) + n) % n));
            return Color.Lerp(a, b, t);
        }

        /// <summary>Hex string (RRGGBB) for rich text, cached per perspective.</summary>
        public static string Hex(PerspectiveMask single)
        {
            if ((single & PerspectiveMask.Strength) != 0) return _hexStr ??= ColorUtility.ToHtmlStringRGB(UIColors.Strength);
            if ((single & PerspectiveMask.Speed) != 0) return _hexSpd ??= ColorUtility.ToHtmlStringRGB(UIColors.Speed);
            if ((single & PerspectiveMask.Intellect) != 0) return _hexIq ??= ColorUtility.ToHtmlStringRGB(UIColors.Intellect);
            if ((single & PerspectiveMask.Weapon) != 0) return _hexGun ??= ColorUtility.ToHtmlStringRGB(UIColors.Weapon);
            return "F2F4F8";
        }
        static string _hexStr, _hexSpd, _hexIq, _hexGun;

        /// <summary>Unscaled-ish delta: the DevPlay harness' fixed capture step when active, else unscaled delta
        /// (UI-like timers keep running during the switcher slow-mo).</summary>
        public static float UiDelta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
    }
}
