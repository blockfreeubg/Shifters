// =====================================================================================
//  SHIFTERS - GamepadTestKit.cs   (gp_core, DevPlay test helper)
//  Switch the prompt device from a DevPlay coroutine with REAL Input System state events on
//  virtual devices (so ShiftersInput.UsingGamepad / DeviceChanged flip exactly as for a player):
//      yield return GamepadTestKit.UseGamepad();    // virtual pad: right stick flick (no gameplay effect)
//      yield return GamepadTestKit.UseKeyboard();   // virtual keyboard: ScrollLock tap (unbound key)
//      yield return GamepadTestKit.MoveMouse(new Vector2(40, 0));
//      GamepadTestKit.Cleanup();                     // remove the virtual devices, restore input settings
//  The pad flick and its release are queued in the same input update, so LookStick reads zero by the time
//  the camera polls it; the keyboard tap (down one frame, up the next) shows up as AnyKeyPressed for one frame only.
//  Static audit: InputPrompts.AuditRawKeys(text) / GamepadTestKit.AuditScene() (every TMP text + DevicePromptText).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Shifters
{
    public static class GamepadTestKit
    {
        static Gamepad _pad;
        static Keyboard _kb;
        static Mouse _mouse;
        static bool _saved;
        static InputSettings.BackgroundBehavior _oldBg;
        static InputSettings.EditorInputBehaviorInPlayMode _oldEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pad = null; _kb = null; _mouse = null; _saved = false; }

        public static Gamepad Pad { get { Ensure(); return _pad; } }
        public static Keyboard Keyboard { get { Ensure(); return _kb; } }
        public static Mouse Mouse { get { Ensure(); return _mouse; } }

        /// <summary>Create the virtual devices and let input reach the game view in batchmode / unfocused editors.</summary>
        public static void Ensure()
        {
            ShiftersInput.Initialize();
            var s = InputSystem.settings;
            if (!_saved)
            {
                _saved = true;
                _oldBg = s.backgroundBehavior;
                _oldEditor = s.editorInputBehaviorInPlayMode;
                s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            }
            if (_pad == null || !_pad.added) _pad = InputSystem.AddDevice<Gamepad>("GpTestPad");
            if (_kb == null || !_kb.added) _kb = InputSystem.AddDevice<Keyboard>("GpTestKeyboard");
            if (_mouse == null || !_mouse.added) _mouse = InputSystem.AddDevice<Mouse>("GpTestMouse");
        }

        /// <summary>Real pad input: a right-stick flick + release in one input update. Waits 2 frames.</summary>
        public static IEnumerator UseGamepad()
        {
            Ensure();
            InputSystem.QueueStateEvent(_pad, new GamepadState { rightStick = new Vector2(0f, 0.9f) });
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            yield return null;
        }

        /// <summary>Real pad button press + release (e.g. GamepadButton.South) in one input update. Note: the button's
        /// action reads WasPressedThisFrame for that frame (A = Jump in Gameplay).</summary>
        public static IEnumerator TapPad(GamepadButton button)
        {
            Ensure();
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(button));
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            yield return null;
        }

        /// <summary>Hold a pad state (stick drift tests) for the given frames, then release.</summary>
        public static IEnumerator HoldPad(GamepadState state, int frames = 3)
        {
            Ensure();
            InputSystem.QueueStateEvent(_pad, state);
            for (int i = 0; i < frames; i++) yield return null;
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        /// <summary>Real keyboard input: tap of an unbound key (default ScrollLock): down one frame, up the next.</summary>
        public static IEnumerator UseKeyboard(Key key = Key.ScrollLock)
        {
            Ensure();
            InputSystem.QueueStateEvent(_kb, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            yield return null;
        }

        /// <summary>Real mouse motion (one event) then rest. A 40 px move flips to keyboard/mouse; 2 px jitter does not.</summary>
        public static IEnumerator MoveMouse(Vector2 delta)
        {
            Ensure();
            InputSystem.QueueStateEvent(_mouse, new MouseState { delta = delta });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState());
            yield return null;
        }

        /// <summary>Remove the virtual devices, restore input settings and set the prompt device back to keyboard.</summary>
        public static void Cleanup()
        {
            if (_pad != null && _pad.added) InputSystem.RemoveDevice(_pad);
            if (_kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _pad = null; _kb = null; _mouse = null;
            if (_saved)
            {
                var s = InputSystem.settings;
                s.backgroundBehavior = _oldBg;
                s.editorInputBehaviorInPlayMode = _oldEditor;
                _saved = false;
            }
            ShiftersInput.SetUsingGamepad(false);
        }

        /// <summary>Audit every TMP text in the loaded scenes (incl. inactive) for raw keyboard key names; texts driven by a
        /// DevicePromptText are audited by their TEMPLATE, live ActionCaps (DeviceKeyCap) are skipped. Texts a HUD renders from
        /// templates (tutorial body, prompt) show their CURRENT output: audit their source data instead. Returns "path: finding, finding" lines (empty = clean).</summary>
        public static List<string> AuditScene()
        {
            var lines = new List<string>();
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t.GetComponentInParent<DeviceKeyCap>(true) != null) continue;   // live action caps are device-driven
                var dpt = t.GetComponent<DevicePromptText>();
                string src = dpt != null ? dpt.Template : t.text;
                var f = InputPrompts.AuditRawKeys(src);
                if (f.Count > 0) lines.Add(Path(t.transform) + ": " + string.Join(", ", f) + "   <- \"" + src.Replace("\n", "\\n") + "\"");
            }
            return lines;
        }

        static string Path(Transform t)
        {
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }
    }
}
