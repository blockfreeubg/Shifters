// =====================================================================================
//  SHIFTERS - AbilityFeedback.cs   [ABILITIES module]
//  Rate-limited "you can't do that" feedback shared by all abilities (denied sound, floating
//  text over the head, shrug/strain gesture, toast).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class AbilityFeedback
    {
        static string s_lastToast;
        static float s_lastToastTime = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_lastToast = null; s_lastToastTime = -99f; }

        /// <summary>Toast that won't repeat the same text more often than minInterval (unscaled seconds).</summary>
        public static void Toast(string text, Color color, float minInterval = 1.5f)
        {
            if (string.IsNullOrEmpty(text)) return;
            float now = Time.unscaledTime;
            if (text == s_lastToast && now - s_lastToastTime < minInterval) return;
            s_lastToast = text;
            s_lastToastTime = now;
            GameEvents.RaiseToast(text, color);
        }

        /// <summary>Full denial: buzz + floating text over the head + gesture + (rate-limited) toast.</summary>
        public static void Denied(ShifterCharacter c, string floatingText, string toast, VisualAction gesture = VisualAction.Shrug)
        {
            Sfx.Play(SfxId.Denied);
            if (c == null) { Toast(toast, UIColors.Danger); return; }
            if (!string.IsNullOrEmpty(floatingText)) Fx.FloatingText(c.HeadTop + Vector3.up * 0.45f, floatingText, UIColors.Danger, 1f);
            if (c.Visual != null) c.Visual.Play(gesture);
            Toast(toast, UIColors.Danger);
        }
    }
}
