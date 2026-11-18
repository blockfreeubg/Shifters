// =====================================================================================
//  SHIFTERS - GamepadPromptTests.cs   (gp_core, DevPlay)
//  G1 KeyLabels == the real InputAction bindings (every action, keyboard + gamepad, switcher context for slots)
//  G2 InputPrompts.Format: tokens, aliases, ALL-CAPS, {Action|kb literal}, {kb:} / {pad:} segments, escapes,
//     unknown tokens, no-pad actions, rich pad styling, caching + zero allocations when cached
//  G3 InputPrompts.AuditRawKeys: raw keyboard names found, tokenised text clean, theme word SHIFT not flagged
//  G4 DeviceChanged with virtual devices: pad drift / small triggers / mouse jitter never flip; pad flick,
//     pad button, key tap, mouse move flip (one event per flip)
//  G5 L1 HUD: tutorial card (converted MOVEMENT body + legacy Keys), interaction prompt, skip chip and a world
//     sign (DevicePromptText) in keyboard mode and pad mode, switching LIVE on real input. Shots kb_* / pad_*.
//  Run: devplay.sh <clone> gp_core -devSetup Shifters.GamepadPromptTests.RunAll
//         -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Shifters
{
    public static class GamepadPromptTests
    {
        static DevPlayContext _ctx;

        public const string MovementBody = "{Move} to walk, {LOOK} to look around, {JUMP} to jump, {kb:hold LEFT SHIFT}{pad:click {Sprint}} to sprint.";
        public const string MovementKb = "WASD to walk, MOUSE to look around, SPACE to jump, hold LEFT SHIFT to sprint.";

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            _ctx = ctx;
            StoryPlayer.StoryModeOverride = 2;   // story OFF: no intro scene over the HUD
            ShiftersInput.SetUsingGamepad(false);
            Labels();
            Formatter();
            Audit();
            yield return Flips();
            yield return Hud();
            GamepadTestKit.Cleanup();
            StoryPlayer.StoryModeOverride = -1;
        }

        // ------------------------------------------------------------------ G1
        static readonly Dictionary<string, string> PathLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "<Keyboard>/space", "Space" }, { "<Keyboard>/leftShift", "LShift" }, { "<Keyboard>/rightShift", "RShift" },
            { "<Keyboard>/e", "E" }, { "<Keyboard>/f", "F" }, { "<Keyboard>/q", "Q" }, { "<Keyboard>/b", "B" }, { "<Keyboard>/x", "X" },
            { "<Keyboard>/g", "G" }, { "<Keyboard>/r", "R" }, { "<Keyboard>/h", "H" }, { "<Keyboard>/t", "T" },
            { "<Keyboard>/escape", "Esc" }, { "<Keyboard>/enter", "Enter" }, { "<Keyboard>/backspace", "Backspace" },
            { "<Keyboard>/1", "1" }, { "<Keyboard>/2", "2" }, { "<Keyboard>/3", "3" },
            { "<Mouse>/leftButton", "LMB" }, { "<Mouse>/rightButton", "RMB" }, { "<Mouse>/delta", "Mouse" }, { "<Mouse>/scroll", "Wheel" },
            { "<Gamepad>/buttonSouth", "A" }, { "<Gamepad>/buttonEast", "B" }, { "<Gamepad>/buttonWest", "X" }, { "<Gamepad>/buttonNorth", "Y" },
            { "<Gamepad>/leftShoulder", "LB" }, { "<Gamepad>/rightShoulder", "RB" }, { "<Gamepad>/leftTrigger", "LT" }, { "<Gamepad>/rightTrigger", "RT" },
            { "<Gamepad>/leftStick", "LS" }, { "<Gamepad>/rightStick", "RS" }, { "<Gamepad>/leftStickPress", "L3" }, { "<Gamepad>/rightStickPress", "R3" },
            { "<Gamepad>/dpad/up", "D-UP" }, { "<Gamepad>/dpad/down", "D-DOWN" }, { "<Gamepad>/dpad/left", "D-LEFT" }, { "<Gamepad>/dpad/right", "D-RIGHT" },
            { "<Gamepad>/start", "Start" }, { "<Gamepad>/select", "Back" },
        };

        // canonical action -> (keyboard map action, gamepad map action)
        static readonly string[,] ActionMap =
        {
            { "Move", "Move", "Move" }, { "Look", "LookMouse", "LookStick" }, { "Jump", "Jump", "Jump" }, { "Sprint", "Sprint", "SprintPad" },
            { "Interact", "Interact", "Interact" }, { "Grab", "Grab", "Grab" }, { "Throw", "Throw", "Throw" }, { "Fire", "Fire", "Fire" },
            { "Aim", "Aim", "Aim" }, { "Bridge", "Bridge", "Bridge" }, { "Flop", "Flop", "Flop" }, { "AIMode", "AIMode", "AIMode" },
            { "Scan", "Scan", "Scan" }, { "Hint", "Hint", "Hint" }, { "Taunt", "Taunt", "Taunt" }, { "Pause", "Pause", "Pause" },
            { "Zoom", "Zoom", "Zoom" }, { "Switcher", "Switcher", "Switcher" }, { "Select1", "Select1", "PadSelect1" },
            { "Select2", "Select2", "PadSelect2" }, { "Select3", "Select3", "PadSelect3" }, { "Confirm", "Confirm", "Confirm" },
            { "Cancel", "Cancel", "Cancel" }, { "SkipTutorial", "SkipTutorial", "SkipTutorial" }, { "Navigate", "Navigate", "Navigate" },
            { "Advance", "Advance", "Advance" },
        };

        static string FirstLabel(InputAction a, bool pad)
        {
            if (a == null) return null;
            var b = a.bindings;
            for (int i = 0; i < b.Count; i++)
            {
                if (b[i].isPartOfComposite) continue;
                if (b[i].isComposite)
                {
                    var parts = new List<string>();
                    for (int j = i + 1; j < b.Count && b[j].isPartOfComposite; j++) parts.Add(b[j].path.ToLowerInvariant());
                    if (pad) continue;
                    if (parts.Contains("<keyboard>/w") && parts.Contains("<keyboard>/a") && parts.Contains("<keyboard>/s") && parts.Contains("<keyboard>/d")) return "WASD";
                    if (parts.Contains("<keyboard>/uparrow") && parts.Contains("<keyboard>/leftarrow")) return "Arrows";
                    return "?composite";
                }
                string path = b[i].path;
                bool isPad = path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
                bool isKb = path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase);
                if (pad ? !isPad : !isKb) continue;
                return PathLabels.TryGetValue(path, out var l) ? l : "?" + path;
            }
            return "";
        }

        static void Labels()
        {
            var map = ShiftersInput.ActionMapForTests;
            if (!_ctx.Check(map != null, "G1 action map available")) return;
            var covered = new HashSet<string>();
            int bad = 0;
            for (int i = 0; i < ActionMap.GetLength(0); i++)
            {
                string act = ActionMap[i, 0];
                var ka = map.FindAction(ActionMap[i, 1]); var pa = map.FindAction(ActionMap[i, 2]);
                covered.Add(ActionMap[i, 1]); covered.Add(ActionMap[i, 2]);
                string ek = FirstLabel(ka, false), ep = FirstLabel(pa, true);
                string lk = KeyLabels.Keyboard(act), lp = KeyLabels.Gamepad(act);
                bool ok = ka != null && pa != null && ek == lk && ep == lp;
                if (!ok) bad++;
                _ctx.Check(ok, "G1 " + act + ": KeyLabels kb '" + lk + "' pad '" + lp + "' == bindings kb '" + ek + "' pad '" + ep + "'");
            }
            // Slots = Select1..3 / PadSelect1..3
            bool slots = KeyLabels.Keyboard("Slots") == FirstLabel(map.FindAction("Select1"), false) + "/" + FirstLabel(map.FindAction("Select2"), false) + "/" + FirstLabel(map.FindAction("Select3"), false)
                         && FirstLabel(map.FindAction("PadSelect1"), true) == "D-LEFT" && FirstLabel(map.FindAction("PadSelect2"), true) == "D-UP"
                         && FirstLabel(map.FindAction("PadSelect3"), true) == "D-RIGHT" && KeyLabels.Gamepad("Slots") == "D-PAD";
            _ctx.Check(slots, "G1 Slots: '" + KeyLabels.Keyboard("Slots") + "' / '" + KeyLabels.Gamepad("Slots") + "' match Select1-3 / PadSelect1-3 (D-LEFT/UP/RIGHT)");
            var missing = new List<string>();
            foreach (var a in map.actions) if (!covered.Contains(a.name) && a.name != "AnyKey") missing.Add(a.name);
            _ctx.Check(missing.Count == 0, "G1 every InputAction has a KeyLabels entry (missing: " + string.Join(",", missing) + ")");
            foreach (var a in KeyLabels.Actions) if (!KeyLabels.IsAction(a)) missing.Add(a);
            _ctx.Check(KeyLabels.PadSwitcherOnly("Select1") && KeyLabels.PadSwitcherOnly("Slots") && !KeyLabels.PadSwitcherOnly("Jump"), "G1 slot selects flagged switcher-only on pad");
            _ctx.Check(KeyLabels.ActionForKey("WASD") == "Move" && KeyLabels.ActionForKey("TAB") == "Switcher" && KeyLabels.ActionForKey("LShift") == "Sprint"
                       && KeyLabels.ActionForKey("Mouse") == "Look" && KeyLabels.ActionForKey("LMB") == "Fire" && KeyLabels.ActionForKey("Backspace") == "SkipTutorial"
                       && KeyLabels.ActionForKey("Jump") == "Jump" && KeyLabels.ActionForKey("{Grab}") == "Grab" && KeyLabels.ActionForKey("Q / E") == null,
                       "G1 legacy key names map to actions");
        }

        // ------------------------------------------------------------------ G2
        static void Eq(string template, bool pad, bool rich, string expected)
        {
            string got = InputPrompts.Format(template, pad, rich);
            _ctx.Check(got == expected, "G2 " + (pad ? "pad" : "kb ") + (rich ? " rich" : "") + " '" + template + "' => '" + got + "'" + (got == expected ? "" : " (expected '" + expected + "')"));
        }

        static void Formatter()
        {
            Eq("{Jump} to jump", false, false, "Space to jump");
            Eq("{Jump} to jump", true, false, "A to jump");
            Eq("STEP UP  ({JUMP})", false, false, "STEP UP  (SPACE)");
            Eq("STEP UP  ({JUMP})", true, false, "STEP UP  (A)");
            Eq(MovementBody, false, true, MovementKb);
            Eq(MovementBody, true, false, "LS to walk, RS to look around, A to jump, click L3 to sprint.");
            Eq("Press {Switcher|RIGHT SHIFT} to open the switcher{kb:, or 1/2/3 to switch directly}.", false, false, "Press RIGHT SHIFT to open the switcher, or 1/2/3 to switch directly.");
            Eq("Press {Switcher|RIGHT SHIFT} to open the switcher{kb:, or 1/2/3 to switch directly}.", true, false, "Press D-DOWN to open the switcher.");
            Eq("{pad:In the switcher, {Slots} picks a slot.}{kb:Keys {Slots} pick a slot.}", true, false, "In the switcher, D-PAD picks a slot.");
            Eq("{pad:In the switcher, {Slots} picks a slot.}{kb:Keys {Slots} pick a slot.}", false, false, "Keys 1/2/3 pick a slot.");
            Eq("Throw Brenda    [{Grab}] Put down", false, false, "Throw Brenda    [F] Put down");
            Eq("Throw Brenda    [{Grab}] Put down", true, true, "Throw Brenda    [<b><color=#F2C230>Y</color></b>] Put down");
            Eq("{Jump}", true, true, "<b><color=#5CC23C>A</color></b>");
            Eq("{Jump}", false, true, "Space");
            Eq("{Bridge} bridge", true, true, "<b>LB</b> bridge");
            Eq("{Use} / {Team} / {interact}", false, false, "E / G / E");
            Eq("{Use} / {Team} / {interact}", true, false, "X / D-LEFT / X");
            Eq("HOLD {SKIP} TO SKIP", true, false, "HOLD BACK TO SKIP");
            Eq("HOLD {SKIP} TO SKIP", false, false, "HOLD BACKSPACE TO SKIP");
            Eq("{Frobnicate} stays", true, false, "{Frobnicate} stays");
            Eq("{{literal}} and {Jump}", false, false, "{literal} and Space");
            Eq("open {Jump", false, false, "open {Jump");
            Eq("zoom {Zoom}", true, false, "zoom ");
            Eq("{KB:only keyboard}{PAD:only pad}", true, false, "only pad");
            string plain = "No tokens here.";
            _ctx.Check(ReferenceEquals(InputPrompts.Format(plain, true, true), plain), "G2 token-free text returned as-is");
            string a = InputPrompts.Format(MovementBody, true, true), b = InputPrompts.Format(MovementBody, true, true);
            _ctx.Check(ReferenceEquals(a, b), "G2 cached result reused");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) { InputPrompts.Format(MovementBody, (i & 1) == 0, (i & 2) == 0); InputPrompts.Label("Jump"); }
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            _ctx.Check(alloc < 1024, "G2 cached Format/Label allocate nothing (" + alloc + " bytes / 2000 calls)");
        }

        // ------------------------------------------------------------------ G3
        static void AuditCase(string text, bool expectClean)
        {
            var f = InputPrompts.AuditRawKeys(text);
            _ctx.Check((f.Count == 0) == expectClean, "G3 audit '" + text + "' => " + (f.Count == 0 ? "clean" : string.Join(", ", f)));
        }

        static void Audit()
        {
            AuditCase(MovementKb, false);
            AuditCase(MovementBody, true);
            AuditCase("STEP UP  (SPACE)", false);
            AuditCase("STEP UP  ({JUMP})", true);
            AuditCase("[F] Pick up Brenda", false);
            AuditCase("[{Grab}] Pick up Brenda", true);
            AuditCase("Press G: inactive teammates FOLLOW you or HOLD position.", false);
            AuditCase("HOLD [BACKSPACE] TO SKIP", false);
            AuditCase("LIE DOWN HERE  [B]", false);
            AuditCase("Click LMB to fire", false);
            AuditCase("Press TAB to open the wheel", false);
            AuditCase("Welcome to SHIFTERS. THE GREAT SHIFT begins. You have three characters.", true);
            AuditCase("MIND THE GAP. OR DON'T: IT'S FOAM.", true);
            AuditCase("STAND HERE\n(YES, YOU)", true);
            AuditCase("Press {Switcher|RIGHT SHIFT}{kb:, or 1/2/3 to switch directly}.", true);
            AuditCase("<b>GRADUATE #1</b> walks into space.", true);
            AuditCase("LOAD RATING: ONE (1) COWORKER", true);
            AuditCase("STEP UP (E)", false);
        }

        // ------------------------------------------------------------------ G4
        static IEnumerator Flips()
        {
            int events = 0; bool lastArg = false;
            Action<bool> h = g => { events++; lastArg = g; };
            ShiftersInput.DeviceChanged += h;
            ShiftersInput.Context = InputContext.Gameplay;
            GamepadTestKit.Ensure();
            yield return null;
            ShiftersInput.SetUsingGamepad(false); events = 0;

            yield return GamepadTestKit.HoldPad(new GamepadState { leftStick = new Vector2(0.3f, 0.1f) });
            yield return GamepadTestKit.HoldPad(new GamepadState { rightStick = new Vector2(0f, -0.4f) });
            yield return GamepadTestKit.HoldPad(new GamepadState { leftTrigger = 0.3f, rightTrigger = 0.2f });
            _ctx.Check(!ShiftersInput.UsingGamepad && events == 0, "G4 pad drift (LS 0.32, RS 0.4, triggers 0.3) does NOT flip to pad (events " + events + ")");

            int v0 = ShiftersInput.DeviceVersion;
            yield return GamepadTestKit.UseGamepad();
            _ctx.Check(ShiftersInput.UsingGamepad && events == 1 && lastArg && ShiftersInput.DeviceVersion == v0 + 1, "G4 right-stick flick flips to pad, one DeviceChanged(true) (events " + events + ")");
            yield return GamepadTestKit.UseGamepad();
            _ctx.Check(events == 1, "G4 more pad input: no extra event");

            for (int i = 0; i < 4; i++) yield return GamepadTestKit.MoveMouse(new Vector2(2f, 1f));
            _ctx.Check(ShiftersInput.UsingGamepad && events == 1, "G4 mouse jitter (4 x 2 px) does NOT flip back (events " + events + ")");
            yield return GamepadTestKit.MoveMouse(new Vector2(40f, 0f));
            _ctx.Check(!ShiftersInput.UsingGamepad && events == 2 && !lastArg, "G4 a real 40 px mouse move flips to keyboard/mouse (events " + events + ")");

            yield return GamepadTestKit.TapPad(GamepadButton.RightStick);
            _ctx.Check(ShiftersInput.UsingGamepad && events == 3, "G4 pad button (R3) flips to pad");
            yield return GamepadTestKit.UseKeyboard();
            _ctx.Check(!ShiftersInput.UsingGamepad && events == 4, "G4 key tap flips to keyboard");
            yield return GamepadTestKit.HoldPad(new GamepadState { leftStick = new Vector2(0f, 0.8f) }, 2);
            _ctx.Check(ShiftersInput.UsingGamepad && events == 5, "G4 full left-stick push flips to pad");
            yield return GamepadTestKit.UseKeyboard();
            ShiftersInput.DeviceChanged -= h;
            _ctx.Check(!ShiftersInput.UsingGamepad, "G4 back on keyboard");
        }

        // ------------------------------------------------------------------ G5
        static IEnumerator Hud()
        {
            float t0 = Time.realtimeSinceStartup;
            while ((CharacterManager.Instance == null || CharacterManager.Instance.Team == null || CharacterManager.Instance.Team.Count < 3
                    || UIManager.Instance == null || !UIManager.Instance.HudVisible) && Time.realtimeSinceStartup - t0 < 40f)
                yield return null;
            yield return _ctx.Wait(1.5f);
            ScreenFader.Set(0f);
            GameEvents.RaiseTutorialHide();
            var chud = CampaignHud.Instance;
            if (chud != null && chud.Card.Showing) chud.Card.Close(true);
            if (CharacterManager.Instance != null) foreach (var c in CharacterManager.Instance.Team) if (c != null) c.ExternalControl = true;
            yield return _ctx.Wait(0.5f);
            var tut = UnityEngine.Object.FindAnyObjectByType<HudTutorial>(FindObjectsInactive.Include);
            var prompt = UnityEngine.Object.FindAnyObjectByType<HudPrompt>(FindObjectsInactive.Include);
            if (!_ctx.Check(tut != null && prompt != null && chud != null, "G5 L1 HUD up (tutorial " + (tut != null) + ", prompt " + (prompt != null) + ", campaign hud " + (chud != null) + ")")) yield break;

            // world sign with a live template
            var cam = Camera.main;
            var signGo = new GameObject("GpTestSign");
            var sign = signGo.AddComponent<TextMeshPro>();
            sign.fontSize = 3f; sign.alignment = TextAlignmentOptions.Center; sign.color = UIColors.Accent;
            sign.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            if (cam != null) { signGo.transform.position = cam.transform.position + cam.transform.forward * 5f + Vector3.up * 0.4f; signGo.transform.rotation = Quaternion.LookRotation(cam.transform.forward); }
            var dpt = DevicePromptText.Attach(sign, "STEP UP  ({JUMP})");

            ShiftersInput.SetUsingGamepad(false);
            GameEvents.RaiseTutorialShow(new TutorialMessage { Id = "gp_movement", Title = "MOVEMENT", Body = MovementBody, Keys = new[] { "WASD", "Mouse", "Space", "LShift" }, Important = true, Duration = 0f });
            GameEvents.RaisePrompt(PromptInfo.Make("{Throw}", "Throw Brenda    [{Grab}] Put down"));
            chud.Skip.Set(true);
            yield return _ctx.Wait(1.5f);
            _ctx.Check(tut.BodyText == MovementKb, "G5 kb: card body is exactly the old keyboard text ('" + tut.BodyText + "')");
            _ctx.Check(Keys(tut) == "WASD,MOUSE,SPACE,LSHIFT", "G5 kb: card keys " + Keys(tut));
            _ctx.Check(prompt.ShownKey == "Q" && prompt.ShownText == "Throw Brenda    [F] Put down", "G5 kb: prompt [" + prompt.ShownKey + "] " + prompt.ShownText);
            _ctx.Check(chud.Skip.KeyText == "BACKSPACE", "G5 kb: skip chip key " + chud.Skip.KeyText);
            _ctx.Check(sign.text == "STEP UP  (SPACE)", "G5 kb: world sign '" + sign.text + "'");
            _ctx.Shot("kb_card_prompt_chip");

            yield return GamepadTestKit.UseGamepad();   // real pad input -> live switch of everything on screen
            yield return _ctx.Wait(0.6f);
            _ctx.Check(ShiftersInput.UsingGamepad, "G5 pad: UsingGamepad after a real pad flick");
            _ctx.Check(tut.Visible && tut.BodyText == InputPrompts.Format(MovementBody, true, true) && tut.BodyText.Contains("click <b>L3</b> to sprint"), "G5 pad: visible card re-rendered live ('" + tut.BodyText + "')");
            _ctx.Check(Keys(tut) == "LS,RS,A,L3", "G5 pad: card keys " + Keys(tut));
            _ctx.Check(prompt.ShownKey == "RB" && prompt.ShownText.Contains("<color=#F2C230>Y</color>") && prompt.ShownText.StartsWith("Throw Brenda"), "G5 pad: prompt [" + prompt.ShownKey + "] " + prompt.ShownText);
            _ctx.Check(chud.Skip.KeyText == "BACK", "G5 pad: skip chip key " + chud.Skip.KeyText);
            _ctx.Check(sign.text.Contains(">A<") && sign.text.StartsWith("STEP UP  ("), "G5 pad: world sign '" + sign.text + "'");
            _ctx.Shot("pad_card_prompt_chip");

            // legacy Keys: W,A,S,D -> one LS; '1' (no pad direct select outside the switcher) -> Switcher (dedup with TAB), Wheel (no pad zoom) dropped
            GameEvents.RaiseTutorialShow(new TutorialMessage { Id = "gp_legacy", Title = "SWITCHING", Body = "Press {Switcher|RIGHT SHIFT} to open the switcher{kb:, or 1/2/3 to switch directly}.", Keys = new[] { "W", "A", "S", "D", "1", "Wheel", "TAB" }, Important = true, Duration = 0f });
            yield return _ctx.Wait(0.8f);
            _ctx.Check(Keys(tut) == "LS,D-DOWN" && tut.BodyText == "Press <b>D-DOWN</b> to open the switcher.", "G5 pad: legacy keys " + Keys(tut) + " / body '" + tut.BodyText + "'");
            _ctx.Shot("pad_switch_card");
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.6f);
            _ctx.Check(Keys(tut) == "W,A,S,D,1,WHEEL,TAB" && tut.BodyText == "Press RIGHT SHIFT to open the switcher, or 1/2/3 to switch directly.", "G5 kb: legacy keys " + Keys(tut) + " / body '" + tut.BodyText + "'");
            _ctx.Check(prompt.ShownKey == "Q" && chud.Skip.KeyText == "BACKSPACE" && sign.text == "STEP UP  (SPACE)", "G5 kb: prompt / chip / sign back to keyboard");
            _ctx.Shot("kb_switch_card");

            // literal (unconverted) prompt keys keep the classic keyboard cap
            GameEvents.RaisePrompt(PromptInfo.Make("RIGHT SHIFT", "WHO RULES TODAY?"));
            yield return GamepadTestKit.UseGamepad();
            _ctx.Check(prompt.ShownKey == "RIGHT SHIFT", "G5 literal prompt key unchanged on pad (convert it to a token): " + prompt.ShownKey);
            yield return GamepadTestKit.UseKeyboard();

            GameEvents.RaisePrompt(null);
            GameEvents.RaiseTutorialHide();
            chud.Skip.Set(false);
            UnityEngine.Object.Destroy(signGo);
            _ctx.Check(dpt != null, "G5 DevicePromptText attached");
            var audit = GamepadTestKit.AuditScene();
            _ctx.Log("G5 L1 scene audit (informational, for the migration agents): " + audit.Count + " texts with raw keyboard names");
            for (int i = 0; i < audit.Count && i < 40; i++) _ctx.Log("  audit: " + audit[i]);
        }

        static string Keys(HudTutorial t)
        {
            var l = new List<string>();
            for (int i = 0; i < t.KeyCount; i++) l.Add(t.KeyText(i));
            return string.Join(",", l);
        }
    }
}
