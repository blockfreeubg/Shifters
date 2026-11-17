// =====================================================================================
//  SHIFTERS - CampaignHudCompat.cs   (ui_hud agent)
//  Thin bridges to the campaign-wave APIs other modules own (CAMPAIGN §18.7/§18.8):
//  ShiftersInput.SkipHeld01 (seams), ShifterCharacter.IsScared (seams), VillainStudio's
//  render texture (visuals). v1 resolved these by reflection while they were landing;
//  they are published now, so the calls are direct (null-safe). Test hooks stay.
// =====================================================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shifters
{
    public static class CampaignHudCompat
    {
        /// <summary>Kept for API compatibility (v1): hold seconds of the old local fallback.</summary>
        public const float FallbackSkipHoldSeconds = 0.8f;

        /// <summary>Test hook: when &gt;= 0 overrides SkipHeld01 (DevPlay drives the chip without a keyboard).</summary>
        public static float SkipOverride = -1f;
        /// <summary>Test hook: characters reported SCARED regardless of the real state (null = none).</summary>
        public static System.Collections.Generic.HashSet<ShifterCharacter> ScaredOverride;
        /// <summary>Test / integration hook: explicit texture for the PiP (wins over VillainStudio).</summary>
        public static Texture PipTextureOverride;
        /// <summary>When true (default) the PiP calls VillainStudio.Ensure() on a Max/Percy broadcast so the RT exists.</summary>
        public static bool EnsureStudio = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SkipOverride = -1f; ScaredOverride = null; PipTextureOverride = null; EnsureStudio = true;
        }

        /// <summary>True: the real ShiftersInput.SkipHeld01 is used.</summary>
        public static bool HasRealSkip => true;
        public static bool HasRealScared => true;

        /// <summary>v1 API (no-op now: ShiftersInput tracks the hold itself).</summary>
        public static void Tick(float dt) { }

        /// <summary>0..1 skip hold progress (ShiftersInput.SkipHeld01: Backspace / gamepad Back).</summary>
        public static float SkipHeld01()
        {
            if (SkipOverride >= 0f) return Mathf.Clamp01(SkipOverride);
            return Mathf.Clamp01(ShiftersInput.SkipHeld01);
        }

        /// <summary>Any "advance / skip" key pressed this frame (card skip): Enter, Space, Escape, Backspace, gamepad South/Start/East, LMB.</summary>
        public static bool AnyAdvancePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame
                || kb.backspaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame)) return true;
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        /// <summary>SCARED state of an intern (ShifterCharacter.IsScared, or the test override).</summary>
        public static bool IsScared(ShifterCharacter c)
        {
            if (c == null) return false;
            if (ScaredOverride != null && ScaredOverride.Contains(c)) return true;
            return c.IsScared;
        }

        /// <summary>The VillainStudio render texture (320x240), creating the studio if needed when <paramref name="ensure"/>.</summary>
        public static Texture VillainTexture(bool ensure)
        {
            if (PipTextureOverride != null) return PipTextureOverride;
            var s = VillainStudio.Instance;
            if (s == null && ensure && EnsureStudio && Application.isPlaying) s = VillainStudio.Ensure();
            return s != null ? s.Texture : null;
        }

        /// <summary>v1 overload: texture if the studio already exists (no creation).</summary>
        public static Texture VillainTexture() => VillainTexture(false);
    }
}
