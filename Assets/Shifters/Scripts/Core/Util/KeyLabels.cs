// =====================================================================================
//  SHIFTERS - KeyLabels.cs   [owned by the CORE_SESSION agent; gamepad prompts: gp_core]
//  Display strings for key caps / prompts. Used by ShiftersInput.KeyLabel and InputPrompts.
//  Lookup is a case-insensitive dictionary of constant strings (no per-call allocations).
//  Every label must match the real binding in ShiftersInput.BuildMap (GamepadPromptTests checks it).
//  Select1/2/3 + Slots gamepad labels are the SWITCHER-context bindings (D-pad left/up/right pick a
//  slot only while the switcher is open; in Gameplay those D-pad directions are team mode / hint / taunt).
// =====================================================================================
using System;
using System.Collections.Generic;

namespace Shifters
{
    /// <summary>Action-name -> key cap text (keyboard + gamepad). Prefer <see cref="ShiftersInput.KeyLabel"/> / <see cref="InputPrompts"/>.</summary>
    public static class KeyLabels
    {
        // action ids
        const int Move = 0, Look = 1, Jump = 2, Sprint = 3, Interact = 4, Grab = 5, Throw = 6, Fire = 7, Aim = 8, Bridge = 9,
            Flop = 10, AIMode = 11, Scan = 12, Hint = 13, Taunt = 14, Pause = 15, Zoom = 16, Switcher = 17, Select1 = 18,
            Select2 = 19, Select3 = 20, Confirm = 21, Cancel = 22, SkipTutorial = 23, Navigate = 24, Advance = 25, Slots = 26;

        /// <summary>Canonical action names (index = id). Token names / test tables use these.</summary>
        public static readonly string[] Actions =
        {
            "Move", "Look", "Jump", "Sprint", "Interact", "Grab", "Throw", "Fire", "Aim", "Bridge",
            "Flop", "AIMode", "Scan", "Hint", "Taunt", "Pause", "Zoom", "Switcher", "Select1",
            "Select2", "Select3", "Confirm", "Cancel", "SkipTutorial", "Navigate", "Advance", "Slots",
        };

        static readonly string[] KeyboardLabels =
        {
            "WASD", "Mouse", "Space", "LShift", "E", "F", "Q", "LMB", "RMB", "B",
            "X", "G", "R", "H", "T", "Esc", "Wheel", "RShift", "1",
            "2", "3", "Enter", "Esc", "Backspace", "Arrows", "Enter", "1/2/3",
        };

        /// <summary>"" = no gamepad equivalent (Zoom: the camera distance is mouse-wheel only).</summary>
        static readonly string[] GamepadLabels =
        {
            "LS", "RS", "A", "L3", "X", "Y", "RB", "RT", "LT", "LB",
            "B", "D-LEFT", "R3", "D-UP", "D-RIGHT", "Start", "", "D-DOWN", "D-LEFT",
            "D-UP", "D-RIGHT", "A", "B", "Back", "LS", "A", "D-PAD",
        };

        static readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Move", Move }, { "Walk", Move },
            { "Look", Look }, { "Camera", Look }, { "LookMouse", Look }, { "LookStick", Look },
            { "Jump", Jump },
            { "Sprint", Sprint }, { "Run", Sprint }, { "SprintPad", Sprint },
            { "Interact", Interact }, { "Use", Interact },
            { "Grab", Grab }, { "Drop", Grab }, { "PutDown", Grab }, { "Carry", Grab }, { "Pickup", Grab },
            { "Throw", Throw },
            { "Fire", Fire }, { "Shoot", Fire },
            { "Aim", Aim },
            { "Bridge", Bridge }, { "LivingBridge", Bridge },
            { "Flop", Flop },
            { "AIMode", AIMode }, { "Team", AIMode }, { "TeamMode", AIMode }, { "Follow", AIMode }, { "Stay", AIMode },
            { "Scan", Scan }, { "Perspective", Scan }, { "Pulse", Scan },
            { "Hint", Hint },
            { "Taunt", Taunt },
            { "Pause", Pause }, { "Menu", Pause },
            { "Zoom", Zoom },
            { "Switcher", Switcher }, { "Switch", Switcher }, { "Wheel", Switcher }, { "Shift", Switcher },
            { "Select1", Select1 }, { "1", Select1 }, { "PadSelect1", Select1 },
            { "Select2", Select2 }, { "2", Select2 }, { "PadSelect2", Select2 },
            { "Select3", Select3 }, { "3", Select3 }, { "PadSelect3", Select3 },
            { "Slots", Slots }, { "SelectSlot", Slots }, { "Select", Slots },
            { "Confirm", Confirm }, { "Submit", Confirm }, { "Accept", Confirm }, { "Enter", Confirm },
            { "Cancel", Cancel }, { "Back", Cancel }, { "Close", Cancel },
            { "SkipTutorial", SkipTutorial }, { "Skip", SkipTutorial },
            { "Navigate", Navigate },
            { "Advance", Advance }, { "Continue", Advance },
        };

        /// <summary>Legacy KEYBOARD key names (as written in old TutorialMessage.Keys arrays / KeyCap calls) -> action id.
        /// Checked before the action names, so "Wheel" here is the mouse wheel (Zoom) and "Shift" is sprint.</summary>
        static readonly Dictionary<string, int> KeyNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "WASD", Move }, { "W", Move }, { "A", Move }, { "S", Move }, { "D", Move },
            { "Mouse", Look },
            { "Space", Jump }, { "Spacebar", Jump },
            { "LShift", Sprint }, { "Left Shift", Sprint }, { "LeftShift", Sprint }, { "Shift", Sprint },
            { "E", Interact }, { "F", Grab }, { "Q", Throw },
            { "LMB", Fire }, { "Click", Fire }, { "Left Click", Fire },
            { "RMB", Aim }, { "Right Click", Aim },
            { "B", Bridge }, { "X", Flop }, { "G", AIMode }, { "R", Scan }, { "H", Hint }, { "T", Taunt },
            { "Esc", Pause }, { "Escape", Pause }, { "P", Pause },
            { "Wheel", Zoom }, { "Mouse Wheel", Zoom }, { "Scroll", Zoom },
            { "RShift", Switcher }, { "Right Shift", Switcher }, { "RightShift", Switcher }, { "Tab", Switcher },
            { "1", Select1 }, { "2", Select2 }, { "3", Select3 }, { "1/2/3", Slots }, { "123", Slots },
            { "Enter", Confirm }, { "Return", Confirm },
            { "Backspace", SkipTutorial },
            { "Arrows", Navigate },
        };

        /// <summary>Keyboard/mouse label ("E", "RShift", "LMB", "Space"...). Unknown names are returned unchanged.</summary>
        public static string Keyboard(string action) => Lookup(action, KeyboardLabels);
        /// <summary>Gamepad label ("A", "X", "RB", "D-DOWN"...; "" when the action has no pad binding). Unknown names are returned unchanged.</summary>
        public static string Gamepad(string action) => Lookup(action, GamepadLabels);
        /// <summary>Label for the given device. Unknown names are returned unchanged.</summary>
        public static string For(string action, bool gamepad) => Lookup(action, gamepad ? GamepadLabels : KeyboardLabels);

        /// <summary>True when the name is an action (or alias) this table knows.</summary>
        public static bool IsAction(string action) => !string.IsNullOrEmpty(action) && Ids.ContainsKey(action);

        /// <summary>Canonical action name for an action name/alias ("Use" -> "Interact"), or null.</summary>
        public static string Canonical(string action) => !string.IsNullOrEmpty(action) && Ids.TryGetValue(action, out int id) ? Actions[id] : null;

        /// <summary>Action for a LEGACY keyboard key name ("Space" -> "Jump", "RShift"/"TAB" -> "Switcher", "LMB" -> "Fire") or,
        /// failing that, an action name/alias ("Jump"). Null when unknown. Used to migrate old Keys arrays.</summary>
        public static string ActionForKey(string keyOrAction)
        {
            if (string.IsNullOrEmpty(keyOrAction)) return null;
            string k = keyOrAction.Trim();
            if (k.Length > 2 && k[0] == '{' && k[k.Length - 1] == '}') k = k.Substring(1, k.Length - 2);
            if (KeyNames.TryGetValue(k, out int id)) return Actions[id];
            return Ids.TryGetValue(k, out id) ? Actions[id] : null;
        }

        /// <summary>True when the name is a LEGACY keyboard key name ("Space", "LShift", "E", "Tab", "1"...).</summary>
        public static bool IsKeyName(string key) => !string.IsNullOrEmpty(key) && KeyNames.ContainsKey(key.Trim());

        /// <summary>True when the action's gamepad binding only exists while the switcher is open (Select1/2/3, Slots).</summary>
        public static bool PadSwitcherOnly(string action)
            => !string.IsNullOrEmpty(action) && Ids.TryGetValue(action, out int id) && (id == Select1 || id == Select2 || id == Select3 || id == Slots);

        static string Lookup(string action, string[] table)
        {
            if (string.IsNullOrEmpty(action)) return "";
            return Ids.TryGetValue(action, out int id) ? table[id] : action;
        }
    }
}
