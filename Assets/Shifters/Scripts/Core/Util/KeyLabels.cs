// =====================================================================================
//  SHIFTERS - KeyLabels.cs   [owned by the CORE_SESSION agent]
//  Display strings for key caps / prompts. Used by ShiftersInput.KeyLabel. Lookup is a
//  case-insensitive dictionary of constant strings (no per-call allocations).
// =====================================================================================
using System;
using System.Collections.Generic;

namespace Shifters
{
    /// <summary>Action-name -> key cap text (keyboard + gamepad). Prefer <see cref="ShiftersInput.KeyLabel"/>.</summary>
    public static class KeyLabels
    {
        // action ids
        const int Move = 0, Look = 1, Jump = 2, Sprint = 3, Interact = 4, Grab = 5, Throw = 6, Fire = 7, Aim = 8, Bridge = 9,
            Flop = 10, AIMode = 11, Scan = 12, Hint = 13, Taunt = 14, Pause = 15, Zoom = 16, Switcher = 17, Select1 = 18,
            Select2 = 19, Select3 = 20, Confirm = 21, Cancel = 22, SkipTutorial = 23, Navigate = 24;

        static readonly string[] KeyboardLabels =
        {
            "WASD", "Mouse", "Space", "LShift", "E", "F", "Q", "LMB", "RMB", "B",
            "X", "G", "R", "H", "T", "Esc", "Wheel", "RShift", "1",
            "2", "3", "Enter", "Esc", "Backspace", "Arrows",
        };

        static readonly string[] GamepadLabels =
        {
            "LS", "RS", "A", "L3", "X", "Y", "RB", "RT", "LT", "LB",
            "B", "D-Left", "R3", "D-Up", "D-Right", "Start", "-", "D-Down", "D-Left",
            "D-Up", "D-Right", "A", "B", "Back", "LS",
        };

        static readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Move", Move }, { "Walk", Move },
            { "Look", Look }, { "Camera", Look },
            { "Jump", Jump },
            { "Sprint", Sprint }, { "Run", Sprint },
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
            { "Select1", Select1 }, { "1", Select1 },
            { "Select2", Select2 }, { "2", Select2 },
            { "Select3", Select3 }, { "3", Select3 },
            { "Confirm", Confirm }, { "Submit", Confirm }, { "Accept", Confirm }, { "Enter", Confirm },
            { "Cancel", Cancel }, { "Back", Cancel }, { "Close", Cancel },
            { "SkipTutorial", SkipTutorial }, { "Skip", SkipTutorial },
            { "Navigate", Navigate },
        };

        /// <summary>Keyboard/mouse label ("E", "RShift", "LMB", "Space"...). Unknown names are returned unchanged.</summary>
        public static string Keyboard(string action) => Lookup(action, KeyboardLabels);
        /// <summary>Gamepad label ("A", "X", "RB", "D-Down"...). Unknown names are returned unchanged.</summary>
        public static string Gamepad(string action) => Lookup(action, GamepadLabels);

        static string Lookup(string action, string[] table)
        {
            if (string.IsNullOrEmpty(action)) return "";
            return Ids.TryGetValue(action, out int id) ? table[id] : action;
        }
    }
}
