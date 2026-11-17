// =====================================================================================
//  SHIFTERS - AbilityInput.cs   [ABILITIES module]
//  Shared gate for ability key handling: only the ACTIVE, player-controlled character in the
//  Gameplay input context reacts to F/Q/B/X/E. Bots/tests call the public ability methods.
// =====================================================================================
namespace Shifters
{
    public static class AbilityInput
    {
        /// <summary>True when this character should read ability keys this frame.</summary>
        public static bool HasControl(ShifterCharacter c) =>
            c != null && c.IsActive && !c.ExternalControl && c.IsAlive &&
            ShiftersInput.Context == InputContext.Gameplay && !TimeController.IsPaused;

        /// <summary>Key cap text for prompts. Falls back to the default letter when the input module
        /// doesn't know the action (or is still a stub).</summary>
        public static string Key(string action, string fallback)
        {
            var s = ShiftersInput.KeyLabel(action);
            return string.IsNullOrEmpty(s) || s == action || s.Length > 8 ? fallback : s;
        }
    }
}
