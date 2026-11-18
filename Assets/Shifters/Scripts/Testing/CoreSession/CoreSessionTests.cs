// =====================================================================================
//  SHIFTERS - CoreSessionTests.cs   [CORE_SESSION DevPlay scenarios]
//  One batched scenario covering CharacterGenerator, Reclone, ShiftersInput (virtual devices),
//  TimeController, GameSession (prefs/run/complete/missing scenes) and the real scene-reload flow.
//  Run:  devplay.sh <clone> core_session_all -devSetup Shifters.CoreSessionTests.RunAll
//          -devScene Assets/Scenes/SampleScene.unity -devSeconds 1
//  (SampleScene is only used because it is in Build Settings: the reload tests reload it.)
//  The user's PlayerPrefs for SHIFTERS keys are backed up and restored.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class CoreSessionTests
    {
        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            var prefs = PrefsBackup.Capture();
            SessionPrefs.ClearProgress();
            try
            {
                BootstrapChecks(ctx);
                GeneratorChecks(ctx);
                RecloneChecks(ctx);
            }
            catch (Exception e) { ctx.Check(false, "sync section threw: " + e); }

            yield return InputChecks(ctx);
            yield return TimeChecks(ctx);

            try { SessionChecks(ctx); }
            catch (Exception e) { ctx.Check(false, "session section threw: " + e); }

            yield return PlayTimeChecks(ctx);
            yield return SceneFlowChecks(ctx);

            prefs.Restore();
            ctx.Log("core session tests finished");
        }

        // ================================================================ bootstrap
        static void BootstrapChecks(DevPlayContext ctx)
        {
            var root = GameBootstrap.Root;
            ctx.Check(root != null, "bootstrap: [SHIFTERS] root exists");
            if (root != null)
            {
                ctx.Check(root.GetComponent<GameSession>() != null && root.GetComponent<TimeController>() != null &&
                          root.GetComponent<AudioManager>() != null && root.GetComponent<PerspectiveManager>() != null &&
                          root.GetComponent<UIManager>() != null, "bootstrap: all 5 managers on the root");
            }
            ctx.Check(GameSession.Instance != null && TimeController.Instance != null, "bootstrap: singletons set");
            ctx.Check(Mathf.Approximately(Physics.gravity.y, -Tuning.Gravity), "bootstrap: gravity 25");
            ctx.Check(Application.runInBackground, "bootstrap: runInBackground");
            ctx.Check(ShiftersInput.IsInitialized, "bootstrap: input initialized");
        }

        // ================================================================ generator
        static void GeneratorChecks(DevPlayContext ctx)
        {
            int badArch = 0, badPrimary = 0, badSecondary = 0, badTotal = 0, badWeapon = 0, dupName = 0, dupColor = 0, badMass = 0,
                badHeight = 0, badVoice = 0, badBody = 0, badDeterminism = 0, badText = 0;
            float minHue = 1f;
            var allNames = new HashSet<string>();
            var accessoryCounts = new Dictionary<Archetype, Dictionary<AccessoryType, int>>();
            for (int seed = 1; seed <= 500; seed++)
            {
                var team = CharacterGenerator.GenerateTeam(seed);
                var m = team.Members;
                int brutes = 0, sprinters = 0, brains = 0, weapons = 0;
                for (int i = 0; i < 3; i++)
                {
                    var p = m[i];
                    if (p.Archetype == Archetype.Brute) brutes++; else if (p.Archetype == Archetype.Sprinter) sprinters++; else brains++;
                    if (p.HasWeapon) weapons++;
                    var prim = CharacterGenerator.PrimaryOf(p.Archetype);
                    int pv = p.Stats.Get(prim);
                    if (pv < 7 || pv > 10) badPrimary++;
                    foreach (var st in new[] { PerspectiveMask.Speed, PerspectiveMask.Strength, PerspectiveMask.Intellect })
                    {
                        if (st == prim) continue;
                        int v = p.Stats.Get(st);
                        if (v < 1 || v > 5) badSecondary++;
                    }
                    int total = p.Stats.Speed + p.Stats.Strength + p.Stats.IQ;
                    if (total < CharacterGenerator.TotalStatMin || total > CharacterGenerator.TotalStatMax) badTotal++;
                    var mr = Tuning.MassRange(p.Archetype);
                    if (p.Mass < mr.x || p.Mass > mr.y) badMass++;
                    float h0 = Tuning.BaseHeight(p.Archetype);
                    if (Mathf.Abs(p.Body.Height - h0) > h0 * Tuning.HeightJitter + 0.002f) badHeight++;
                    var vr = p.Archetype == Archetype.Brute ? new Vector2(0.75f, 0.9f) : p.Archetype == Archetype.Sprinter ? new Vector2(1.15f, 1.3f) : new Vector2(1f, 1.15f);
                    if (p.VoicePitch < vr.x - 0.001f || p.VoicePitch > vr.y + 0.001f) badVoice++;
                    var b = p.Body;
                    if (b.WidthScale < 0.85f || b.WidthScale > 1.2f || b.HeadScale < 0.85f || b.HeadScale > 1.2f || b.LimbScale < 0.9f || b.LimbScale > 1.1f ||
                        b.BellyScale < 0.9f || b.BellyScale > 1.3f || b.EyeScale < 0.85f || b.EyeScale > 1.25f || b.HairStyle < 0 || b.HairStyle > 4 ||
                        b.MouthStyle < 0 || b.MouthStyle > 2 || Mathf.Abs(b.Radius - Tuning.CapsuleRadius(p.Archetype)) > 0.031f * Tuning.CapsuleRadius(p.Archetype)) badBody++;
                    if (string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.Title) || string.IsNullOrEmpty(p.Personality) || string.IsNullOrEmpty(p.Catchphrase) || p.CloneGeneration != 1) badText++;
                    allNames.Add(p.Name);
                    if (!accessoryCounts.TryGetValue(p.Archetype, out var dict)) accessoryCounts[p.Archetype] = dict = new Dictionary<AccessoryType, int>();
                    dict.TryGetValue(b.Accessory, out int c); dict[b.Accessory] = c + 1;
                }
                if (brutes != 1 || sprinters != 1 || brains != 1) badArch++;
                if (weapons != 1) badWeapon++;
                if (m[0].Name == m[1].Name || m[0].Name == m[2].Name || m[1].Name == m[2].Name) dupName++;
                if (m[0].PrimaryColor == m[1].PrimaryColor || m[0].PrimaryColor == m[2].PrimaryColor || m[1].PrimaryColor == m[2].PrimaryColor) dupColor++;
                minHue = Mathf.Min(minHue, CharacterGenerator.HueDistance(m[0].PrimaryColor, m[1].PrimaryColor),
                    CharacterGenerator.HueDistance(m[0].PrimaryColor, m[2].PrimaryColor), CharacterGenerator.HueDistance(m[1].PrimaryColor, m[2].PrimaryColor));
                if (seed % 25 == 0 && JsonUtility.ToJson(team) != JsonUtility.ToJson(CharacterGenerator.GenerateTeam(seed))) badDeterminism++;
            }
            ctx.Check(badArch == 0, "gen500: exactly one Brute/Sprinter/Brainiac per team (bad=" + badArch + ")");
            ctx.Check(badPrimary == 0, "gen500: primary stat 7..10 (bad=" + badPrimary + ")");
            ctx.Check(badSecondary == 0, "gen500: secondary stats 1..5 (bad=" + badSecondary + ")");
            ctx.Check(badTotal == 0, "gen500: stat total within 12..17 (bad=" + badTotal + ")");
            ctx.Check(badWeapon == 0, "gen500: exactly one weapon per team (bad=" + badWeapon + ")");
            ctx.Check(dupName == 0, "gen500: unique names within team (bad=" + dupName + ")");
            ctx.Check(dupColor == 0, "gen500: unique colors within team (bad=" + dupColor + ")");
            ctx.Check(minHue >= CharacterGenerator.MinHueSeparation - 0.0001f, "gen500: teammate hue separation >= " + CharacterGenerator.MinHueSeparation + " (min=" + minHue.ToString("0.000") + ")");
            ctx.Check(badMass == 0, "gen500: masses within archetype ranges (bad=" + badMass + ")");
            ctx.Check(badHeight == 0, "gen500: heights within +-4% of base (bad=" + badHeight + ")");
            ctx.Check(badVoice == 0, "gen500: voice pitch within archetype ranges (bad=" + badVoice + ")");
            ctx.Check(badBody == 0, "gen500: body multipliers/radius in range (bad=" + badBody + ")");
            ctx.Check(badText == 0, "gen500: flavour text present, CloneGeneration 1 (bad=" + badText + ")");
            ctx.Check(badDeterminism == 0, "gen500: same seed => identical JSON (bad=" + badDeterminism + ")");
            ctx.Check(JsonUtility.ToJson(CharacterGenerator.GenerateTeam(7)) != JsonUtility.ToJson(CharacterGenerator.GenerateTeam(8)), "gen: different seeds differ");
            ctx.Check(allNames.Count >= 45, "gen500: >= 45 distinct names seen (" + allNames.Count + ")");
            int namePool = CharacterPools.SharedNames.Length + CharacterPools.BruteNames.Length + CharacterPools.SprinterNames.Length + CharacterPools.BrainiacNames.Length;
            ctx.Check(namePool >= 45 && CharacterPools.Palette.Length >= 10, "pools: names " + namePool + " >= 45, palette " + CharacterPools.Palette.Length + " >= 10");
            ctx.Check(CharacterPools.BrutePersonalities.Length >= 12 && CharacterPools.SprinterPersonalities.Length >= 12 && CharacterPools.BrainiacPersonalities.Length >= 12, "pools: >= 12 personalities per archetype");
            // accessory bias sanity: signature accessories dominate
            int bruteSig = Count(accessoryCounts, Archetype.Brute, AccessoryType.HardHat, AccessoryType.Headband, AccessoryType.Mustache);
            int sprSig = Count(accessoryCounts, Archetype.Sprinter, AccessoryType.Visor, AccessoryType.Headband, AccessoryType.PropellerCap);
            int brSig = Count(accessoryCounts, Archetype.Brainiac, AccessoryType.Glasses, AccessoryType.BowTie, AccessoryType.Antenna);
            ctx.Check(bruteSig > 300 && sprSig > 300 && brSig > 300, "gen500: archetype-biased accessories (" + bruteSig + "/" + sprSig + "/" + brSig + " of 500)");

            foreach (int s in new[] { 42, 1337, 2026 }) ctx.Log(CharacterGenerator.Describe(CharacterGenerator.GenerateTeam(s)));
        }

        static int Count(Dictionary<Archetype, Dictionary<AccessoryType, int>> d, Archetype a, params AccessoryType[] types)
        {
            int n = 0;
            if (!d.TryGetValue(a, out var inner)) return 0;
            foreach (var t in types) if (inner.TryGetValue(t, out int c)) n += c;
            return n;
        }

        // ================================================================ reclone
        static void RecloneChecks(DevPlayContext ctx)
        {
            int bad = 0, badSuffix = 0, badIsolation = 0, badWeapon = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var team = CharacterGenerator.GenerateTeam(seed);
                foreach (var m in team.Members)
                {
                    string before = JsonUtility.ToJson(m);
                    var r = CharacterGenerator.Reclone(m, seed);
                    var prim = CharacterGenerator.PrimaryOf(m.Archetype);
                    int secBefore = m.Stats.Speed + m.Stats.Strength + m.Stats.IQ - m.Stats.Get(prim);
                    int secAfter = r.Stats.Speed + r.Stats.Strength + r.Stats.IQ - r.Stats.Get(prim);
                    bool bothMin = secBefore == 2;
                    if (r.CloneGeneration != 2 || r.Stats.Get(prim) != m.Stats.Get(prim) || r.Stats.Get(prim) < 7 ||
                        secAfter != (bothMin ? secBefore : secBefore - 1) || r.Stats.Speed < 1 || r.Stats.Strength < 1 || r.Stats.IQ < 1 ||
                        r.DisplayName != m.Name + " II" || r.Archetype != m.Archetype) bad++;
                    var r2 = CharacterGenerator.Reclone(r, seed + 1);
                    if (r2.DisplayName != m.Name + " III" || SuffixCount(r2.Personality) != 1 || SuffixCount(r.Personality) != 1 ||
                        !r2.Personality.StartsWith(m.Personality)) badSuffix++;
                    if (JsonUtility.ToJson(m) != before) badIsolation++;
                    if (r.HasWeapon != m.HasWeapon || (m.HasWeapon && ReferenceEquals(r.Weapon, m.Weapon))) badWeapon++;
                }
            }
            ctx.Check(bad == 0, "reclone x600: gen+1, -1 secondary (min 1), primary kept >= 7, 'Name II' (bad=" + bad + ")");
            ctx.Check(badSuffix == 0, "reclone: 'III' + exactly one reprint suffix, never accumulates (bad=" + badSuffix + ")");
            ctx.Check(badIsolation == 0, "reclone: original profile untouched (bad=" + badIsolation + ")");
            ctx.Check(badWeapon == 0, "reclone: weapon kept as a deep copy (bad=" + badWeapon + ")");

            var floor = CharacterGenerator.GenerateTeam(5).Members[0].Clone();
            var p0 = CharacterGenerator.PrimaryOf(floor.Archetype);
            floor.Stats = new CharacterStats { Speed = 1, Strength = 1, IQ = 1 };
            if (p0 == PerspectiveMask.Speed) floor.Stats.Speed = 4; else if (p0 == PerspectiveMask.Strength) floor.Stats.Strength = 4; else floor.Stats.IQ = 4;
            var rf = CharacterGenerator.Reclone(floor, 3);
            ctx.Check(rf.Stats.Get(p0) == 7 && rf.Stats.Speed >= 1 && rf.Stats.Strength >= 1 && rf.Stats.IQ >= 1, "reclone: secondaries floor at 1, corrupted primary repaired to 7");
            ctx.Log("reclone sample: " + CharacterGenerator.Describe(new TeamData { Seed = 42, Members = new[] {
                CharacterGenerator.Reclone(CharacterGenerator.GenerateTeam(42).Members[0], 42),
                CharacterGenerator.Reclone(CharacterGenerator.Reclone(CharacterGenerator.GenerateTeam(42).Members[1], 42), 43),
                CharacterGenerator.GenerateTeam(42).Members[2] } }));
        }

        static int SuffixCount(string s)
        {
            int n = 0;
            foreach (var sfx in CharacterPools.RecloneSuffixes) { int i = 0; while ((i = s.IndexOf(sfx, i, StringComparison.Ordinal)) >= 0) { n++; i += sfx.Length; } }
            return n;
        }

        // ================================================================ input
        static IEnumerator InputChecks(DevPlayContext ctx)
        {
            ctx.Log("devices before test: " + InputSystem.devices.Count + " (keyboard=" + (Keyboard.current != null) + ", mouse=" + (Mouse.current != null) + ", pad=" + (Gamepad.current != null) + ")");
            ShiftersInput.Initialize(); // idempotent
            ShiftersInput.Initialize();
            ctx.Check(ShiftersInput.IsInitialized, "input: actions built + enabled (Initialize idempotent)");

            // neutral with no input
            bool threw = false;
            try
            {
                foreach (InputContext c in Enum.GetValues(typeof(InputContext)))
                {
                    ShiftersInput.Context = c;
                    var _ = ShiftersInput.Move + ShiftersInput.Look + ShiftersInput.NavigateStick + ShiftersInput.PointerDelta + ShiftersInput.PointerPosition;
                    bool any = ShiftersInput.JumpPressed | ShiftersInput.JumpHeld | ShiftersInput.SprintHeld | ShiftersInput.InteractPressed | ShiftersInput.GrabPressed |
                               ShiftersInput.ThrowPressed | ShiftersInput.FirePressed | ShiftersInput.FireHeld | ShiftersInput.AimHeld | ShiftersInput.BridgePressed |
                               ShiftersInput.FlopPressed | ShiftersInput.AIModePressed | ShiftersInput.ScanPressed | ShiftersInput.HintPressed | ShiftersInput.TauntPressed |
                               ShiftersInput.PausePressed | ShiftersInput.SwitcherPressed | ShiftersInput.ConfirmPressed | ShiftersInput.CancelPressed |
                               ShiftersInput.SkipTutorialPressed | ShiftersInput.AnyKeyPressed | ShiftersInput.LeftClickPressed;
                    if (any || ShiftersInput.SelectPressed != -1 || ShiftersInput.Zoom != 0f || ShiftersInput.Move != Vector2.zero || ShiftersInput.Look != Vector2.zero)
                        ctx.Check(false, "input: neutral in context " + c);
                }
            }
            catch (Exception e) { threw = true; ctx.Check(false, "input: getters threw " + e.Message); }
            ctx.Check(!threw, "input: all getters neutral + no throw in every context");

            ctx.Check(ShiftersInput.KeyLabelKeyboard("Interact") == "E" && ShiftersInput.KeyLabelKeyboard("grab") == "F" && ShiftersInput.KeyLabelKeyboard("Throw") == "Q" &&
                      ShiftersInput.KeyLabelKeyboard("Switcher") == "RShift" && ShiftersInput.KeyLabelKeyboard("Fire") == "LMB" && ShiftersInput.KeyLabelKeyboard("Jump") == "Space" &&
                      ShiftersInput.KeyLabelKeyboard("SkipTutorial") == "Backspace" && ShiftersInput.KeyLabelKeyboard("Frobnicate") == "Frobnicate", "input: keyboard KeyLabels");
            ctx.Check(ShiftersInput.KeyLabelGamepad("Interact") == "X" && ShiftersInput.KeyLabelGamepad("Jump") == "A" && ShiftersInput.KeyLabelGamepad("Switcher") == "D-DOWN" &&
                      ShiftersInput.KeyLabelGamepad("Fire") == "RT" && ShiftersInput.KeyLabelGamepad("Pause") == "Start", "input: gamepad KeyLabels");

            // allocation check: 2000 frames worth of polling
            ShiftersInput.Context = InputContext.Gameplay;
            yield return null;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) PollAll();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            ctx.Check(allocated < 1024, "input: polling every getter + KeyLabel allocates nothing (" + allocated + " bytes / 2000 polls)");

            // ---- virtual devices: real bindings end-to-end
            var settings = InputSystem.settings;
            var oldBg = settings.backgroundBehavior;
            var oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var kb = InputSystem.AddDevice<Keyboard>("CoreTestKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>("CoreTestMouse");
            var pad = InputSystem.AddDevice<Gamepad>("CoreTestPad");
            var gs = GameSession.Instance;
            float oldSens = gs.Settings.MouseSensitivity; bool oldInvert = gs.Settings.InvertY;
            gs.Settings.MouseSensitivity = 1f; gs.Settings.InvertY = false;
            yield return null;

            ShiftersInput.Context = InputContext.Gameplay;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.E));
            yield return null;
            ctx.Check(ShiftersInput.InteractPressed, "input/kb: E => InteractPressed (Gameplay)");
            ctx.Check(!ShiftersInput.UsingGamepad && ShiftersInput.KeyLabel("Interact") == "E", "input/kb: UsingGamepad false, KeyLabel E");
            yield return Release(kb);

            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W, Key.D, Key.LeftShift, Key.Space));
            yield return null;
            var mv = ShiftersInput.Move;
            ctx.Check(mv.x > 0.6f && mv.y > 0.6f && mv.magnitude <= 1.001f, "input/kb: W+D => diagonal Move " + mv);
            ctx.Check(ShiftersInput.SprintHeld && ShiftersInput.JumpHeld && ShiftersInput.JumpPressed, "input/kb: LShift sprint held, Space jump pressed+held");
            yield return Release(kb);

            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Digit2));
            yield return null;
            ctx.Check(ShiftersInput.SelectPressed == 1, "input/kb: '2' => SelectPressed 1 in Gameplay");
            yield return Release(kb);

            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(50f, 20f) });
            yield return null;
            var look = ShiftersInput.Look;
            ctx.Check(Mathf.Abs(look.x - 5f) < 0.01f && Mathf.Abs(look.y - 2f) < 0.01f, "input/mouse: delta (50,20) => Look (5,2) degrees, got " + look);
            gs.Settings.InvertY = true; gs.Settings.MouseSensitivity = 2f;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(50f, 20f) });
            yield return null;
            look = ShiftersInput.Look;
            ctx.Check(Mathf.Abs(look.x - 10f) < 0.01f && Mathf.Abs(look.y + 4f) < 0.01f, "input/mouse: sensitivity 2 + InvertY => (10,-4), got " + look);
            gs.Settings.InvertY = false; gs.Settings.MouseSensitivity = 1f;
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 1f) });
            yield return null;
            float zoom = ShiftersInput.Zoom;
            ctx.Check(zoom > 0.5f && zoom <= 1.01f, "input/mouse: wheel up => Zoom +1 (" + zoom + ")");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            ctx.Check(ShiftersInput.FirePressed && ShiftersInput.LeftClickPressed, "input/mouse: LMB => FirePressed + LeftClickPressed");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;

            // context gating + same-frame swallow
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape));
            yield return null;
            bool pause = ShiftersInput.PausePressed;
            ShiftersInput.Context = InputContext.Menu;      // what the UI does when Esc opens the pause menu
            bool cancelSameFrame = ShiftersInput.CancelPressed;
            ctx.Check(pause && !cancelSameFrame, "input: Esc opens pause, the same Esc is NOT also read as Cancel (pause=" + pause + ")");
            yield return Release(kb);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.E, Key.Enter));
            yield return null;
            ctx.Check(!ShiftersInput.InteractPressed && ShiftersInput.ConfirmPressed && ShiftersInput.Move == Vector2.zero, "input: Menu context => gameplay neutral, Enter => Confirm");
            yield return Release(kb);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape));
            yield return null;
            ctx.Check(ShiftersInput.CancelPressed && !ShiftersInput.PausePressed, "input: Esc in Menu => Cancel (not Pause)");
            yield return Release(kb);

            ShiftersInput.Context = InputContext.Cutscene;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Backspace, Key.K));
            yield return null;
            ctx.Check(ShiftersInput.SkipTutorialPressed && ShiftersInput.AnyKeyPressed && !ShiftersInput.ConfirmPressed, "input: Backspace skip + AnyKey work in Cutscene");
            yield return Release(kb);

            // gamepad
            ShiftersInput.Context = InputContext.Gameplay;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0f, 1f) }.WithButton(GamepadButton.West));
            yield return null;
            ctx.Check(ShiftersInput.Move.y > 0.9f && ShiftersInput.InteractPressed, "input/pad: left stick => Move, X => Interact");
            ctx.Check(ShiftersInput.UsingGamepad && ShiftersInput.KeyLabel("Interact") == "X", "input/pad: UsingGamepad true, KeyLabel X");
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0f, 1f) }.WithButton(GamepadButton.LeftStick));
            yield return null;
            ctx.Check(ShiftersInput.SprintHeld, "input/pad: L3 click latches sprint while moving");
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0f, 1f) });
            yield return null;
            ctx.Check(ShiftersInput.SprintHeld, "input/pad: sprint latch stays after releasing L3");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            ctx.Check(!ShiftersInput.SprintHeld, "input/pad: sprint latch clears when the stick is released");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadLeft));
            yield return null;
            ctx.Check(ShiftersInput.AIModePressed && ShiftersInput.SelectPressed == -1, "input/pad: D-left => team mode in Gameplay (no slot select)");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
            yield return null;
            ctx.Check(ShiftersInput.SwitcherPressed, "input/pad: D-down => SwitcherPressed");
            ShiftersInput.Context = InputContext.Switcher;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(-1f, 0f) }.WithButton(GamepadButton.DpadRight));
            yield return null;
            var nav = ShiftersInput.NavigateStick;
            ctx.Check(ShiftersInput.SelectPressed == 2 && !ShiftersInput.TauntPressed && nav.x < -0.9f, "input/pad: Switcher => D-right selects slot 3, right stick navigates " + nav);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            yield return null;
            ctx.Check(ShiftersInput.CancelPressed && !ShiftersInput.FlopPressed, "input/pad: B => Cancel in Switcher (not Flop)");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            ctx.Check(ShiftersInput.Look == Vector2.zero, "input: Look neutral in Switcher context");

            // cleanup
            ShiftersInput.Context = InputContext.Gameplay;
            gs.Settings.MouseSensitivity = oldSens; gs.Settings.InvertY = oldInvert;
            InputSystem.RemoveDevice(kb); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(pad);
            settings.backgroundBehavior = oldBg;
            settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            ShiftersInput.Shutdown();
            ctx.Check(!ShiftersInput.IsInitialized, "input: Shutdown disposes the map");
            ShiftersInput.Initialize();
            ctx.Check(ShiftersInput.IsInitialized && ShiftersInput.Move == Vector2.zero, "input: re-Initialize after Shutdown works");
        }

        static IEnumerator Release(Keyboard kb) { InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null; }

        static void PollAll()
        {
            var v = ShiftersInput.Move + ShiftersInput.Look + ShiftersInput.NavigateStick + ShiftersInput.PointerDelta + ShiftersInput.PointerPosition;
            bool b = ShiftersInput.JumpPressed | ShiftersInput.JumpHeld | ShiftersInput.SprintHeld | ShiftersInput.InteractPressed | ShiftersInput.GrabPressed |
                     ShiftersInput.ThrowPressed | ShiftersInput.FirePressed | ShiftersInput.FireHeld | ShiftersInput.AimHeld | ShiftersInput.BridgePressed |
                     ShiftersInput.FlopPressed | ShiftersInput.AIModePressed | ShiftersInput.ScanPressed | ShiftersInput.HintPressed | ShiftersInput.TauntPressed |
                     ShiftersInput.PausePressed | ShiftersInput.SwitcherPressed | ShiftersInput.ConfirmPressed | ShiftersInput.CancelPressed |
                     ShiftersInput.SkipTutorialPressed | ShiftersInput.AnyKeyPressed | ShiftersInput.LeftClickPressed | ShiftersInput.UsingGamepad;
            int s = ShiftersInput.SelectPressed;
            float z = ShiftersInput.Zoom;
            string l = ShiftersInput.KeyLabel("Interact");
            if (b && s > 5 && z > 99f && v.x > 1e9f && l == null) Debug.Log("unreachable");
        }

        // ================================================================ time
        static IEnumerator TimeChecks(DevPlayContext ctx)
        {
            int pauseEvents = 0;
            Action<bool> onPause = _ => pauseEvents++;
            GameEvents.PauseChanged += onPause;
            float u0 = TimeController.RealTime;
            TimeController.SetSlowMo(true);
            yield return WaitUntil(() => Time.timeScale <= Tuning.SwitcherTimeScale + 0.0005f, 600);
            float rampTook = TimeController.RealTime - u0;
            ctx.Check(Mathf.Abs(Time.timeScale - Tuning.SwitcherTimeScale) < 0.001f && TimeController.IsSlowMo, "time: slow-mo reaches 0.08 (" + Time.timeScale + ")");
            ctx.Check(rampTook > 0.1f && rampTook < 0.4f, "time: slow-mo ramp ~" + Tuning.SwitcherRampTime + "s unscaled (took " + rampTook.ToString("0.000") + ")");
            ctx.Check(Mathf.Abs(Time.fixedDeltaTime - TimeController.MinFixedDeltaTime) < 1e-6f, "time: fixedDeltaTime scaled + clamped to 1/600 in slow-mo (" + Time.fixedDeltaTime + ")");
            TimeController.SetPaused(true);
            ctx.Check(Time.timeScale == 0f && TimeController.IsPaused && pauseEvents == 1, "time: pause => 0 (PauseChanged raised)");
            yield return ctx.Wait(0.2f);
            ctx.Check(Time.timeScale == 0f, "time: stays paused over frames");
            TimeController.SetPaused(true);
            ctx.Check(pauseEvents == 1, "time: redundant SetPaused raises no event");
            TimeController.SetPaused(false);
            ctx.Check(Mathf.Abs(Time.timeScale - Tuning.SwitcherTimeScale) < 0.001f && pauseEvents == 2, "time: unpause returns to slow-mo (pause stacks with slow-mo)");
            TimeController.HitStop(0.3f, 0.2f);
            ctx.Check(Mathf.Abs(Time.timeScale - Tuning.SwitcherTimeScale) < 0.001f, "time: hit-stop during slow-mo never speeds time up");
            TimeController.SetSlowMo(false);
            yield return WaitUntil(() => Time.timeScale >= 0.999f, 600);
            ctx.Check(Mathf.Approximately(Time.timeScale, 1f) && Mathf.Approximately(Time.fixedDeltaTime, Tuning.FixedTimestep), "time: slow-mo off returns to 1 and fixedDeltaTime 1/60");
            TimeController.HitStop(0.3f, 0.25f);
            ctx.Check(Mathf.Abs(Time.timeScale - 0.3f) < 0.001f && TimeController.IsHitStop, "time: HitStop(0.3) snaps to 0.3");
            float h0 = TimeController.RealTime;
            yield return WaitUntil(() => !TimeController.IsHitStop, 600);
            float hitTook = TimeController.RealTime - h0;
            ctx.Check(Mathf.Approximately(Time.timeScale, 1f) && hitTook > 0.25f && hitTook < 0.6f, "time: hit-stop holds then recovers to 1 (took " + hitTook.ToString("0.000") + "s)");
            TimeController.BaseScale = 2f;
            yield return null;
            ctx.Check(Mathf.Approximately(Time.timeScale, 2f) && Mathf.Approximately(Time.fixedDeltaTime, Tuning.FixedTimestep), "time: BaseScale fast-forward 2x keeps 1/60 physics");
            TimeController.BaseScale = 1f;
            TimeController.SetSlowMo(true);
            TimeController.SetPaused(true);
            TimeController.ResetAll();
            ctx.Check(Time.timeScale == 1f && !TimeController.IsPaused && !TimeController.IsSlowMo && pauseEvents == 4, "time: ResetAll clears pause + slow-mo (raises unpause)");
            GameEvents.PauseChanged -= onPause;
        }

        static IEnumerator WaitUntil(Func<bool> cond, int maxFrames)
        {
            for (int i = 0; i < maxFrames && !cond(); i++) yield return null;
        }

        // ================================================================ session (sync)
        static void SessionChecks(DevPlayContext ctx)
        {
            var gs = GameSession.Instance;

            // settings round-trip
            int changed = 0;
            Action onSettings = () => changed++;
            GameEvents.SettingsChanged += onSettings;
            var s = gs.Settings;
            s.MasterVolume = 0.33f; s.MusicVolume = 0.21f; s.SfxVolume = 0.77f; s.MouseSensitivity = 2.5f; s.InvertY = true; s.Fov = 72f;
            s.CinematicSwitch = false; s.Tutorials = false; s.Quality = 0; s.Fullscreen = false; s.ScreenShake = false;
            string expected = JsonUtility.ToJson(s);
            gs.SaveSettings();
            ctx.Check(PlayerPrefs.HasKey(SessionPrefs.SettingsKey), "settings: saved under 'shifters_settings'");
            var loaded = SessionPrefs.LoadSettings();
            ctx.Check(JsonUtility.ToJson(loaded) == expected, "settings: PlayerPrefs JSON round-trip identical");
            gs.ApplySettings();
            ctx.Check(changed == 1, "settings: ApplySettings raises SettingsChanged");
            PlayerPrefs.SetString(SessionPrefs.SettingsKey, "{\"MouseSensitivity\":99,\"Fov\":10,\"MasterVolume\":-3}");
            var clamped = SessionPrefs.LoadSettings();
            ctx.Check(clamped.MouseSensitivity == 3f && clamped.Fov == 50f && clamped.MasterVolume == 0f && clamped.SfxVolume == 0.8f, "settings: out-of-range values sanitized, missing fields default");
            PlayerPrefs.SetString(SessionPrefs.SettingsKey, "not json {{{");
            ctx.Check(SessionPrefs.LoadSettings() != null, "settings: corrupt JSON => defaults, no throw");
            gs.ResetSettingsToDefaults();
            ctx.Check(JsonUtility.ToJson(gs.Settings) == JsonUtility.ToJson(new SettingsData()) && changed == 2, "settings: ResetSettingsToDefaults");
            GameEvents.SettingsChanged -= onSettings;

            // NewRun / EnsureTeam
            gs.NewRun(0);
            int rnd = gs.Seed;
            ctx.Check(gs.HasRun && rnd >= 100000 && rnd <= 999999, "session: NewRun(0) => random 6-digit seed " + rnd);
            gs.NewRun(-5);
            ctx.Check(gs.Seed != rnd, "session: re-roll gives a new seed (" + gs.Seed + ")");
            gs.NewRun(4242);
            ctx.Check(gs.Seed == 4242 && JsonUtility.ToJson(gs.Team) == JsonUtility.ToJson(CharacterGenerator.GenerateTeam(4242)) && gs.Stats.Restarts == 0, "session: NewRun(seed) deterministic team, fresh stats");
            var teamRef = gs.Team;
            gs.EnsureTeam();
            ctx.Check(ReferenceEquals(gs.Team, teamRef), "session: EnsureTeam keeps an existing team");

            // run save/load round-trip
            gs.DeleteSavedRun();
            ctx.Check(!gs.HasSavedRun, "run: no saved run after delete");
            gs.Stats.Throws = 7; gs.Stats.PlayTime = 12.5f;
            SessionPrefs.SaveRun(gs.Team, 2, gs.Stats);
            var run = gs.PeekSavedRun();
            ctx.Check(gs.HasSavedRun && run != null && run.LevelIndex == 2 && run.Stats.Throws == 7 && Mathf.Approximately(run.Stats.PlayTime, 12.5f), "run: saved under 'shifters_run' with level + stats");
            ctx.Check(run != null && JsonUtility.ToJson(run.Team) == JsonUtility.ToJson(gs.Team), "run: team JSON round-trip identical");
            int armed = 0; if (run != null) foreach (var m in run.Team.Members) if (m.HasWeapon) armed++;
            ctx.Check(armed == 1, "run: exactly one armed member after load (JsonUtility null-weapon fix), got " + armed);
            PlayerPrefs.SetString(SessionPrefs.RunKey, "{\"Team\":{\"Members\":[]}}");
            ctx.Check(!gs.HasSavedRun && !PlayerPrefs.HasKey(SessionPrefs.RunKey), "run: incomplete saved run discarded");

            // missing scenes => clear error, no throw. [integrator] In the merged project MainMenu / level scenes exist and
            // would REALLY load (leaving this test scene): only the still-missing ones are exercised here; the real
            // menu -> level -> menu flow is covered by Shifters.IntegrationFlow.MenuToLevel.
            bool menuExists = SceneLoader.CanLoad(LevelCatalog.MainMenuScene);
            bool l1Exists = SceneLoader.CanLoad(LevelCatalog.Get(0).SceneName);
            bool l4Exists = SceneLoader.CanLoad(LevelCatalog.Get(3).SceneName);
            if (menuExists || l1Exists || l4Exists) ctx.Log("scene-missing checks limited: MainMenu=" + menuExists + " L1=" + l1Exists + " L4=" + l4Exists + " exist");
            var cap = LogCapture.Begin();
            bool threw = false;
            try { if (!l1Exists) gs.LoadLevel(0); gs.LoadLevel(99); if (!menuExists) gs.GoToMainMenu(); }
            catch (Exception e) { threw = true; ctx.Log("threw: " + e); }
            cap.End();
            ctx.Check(!threw && !gs.IsLoading, "scenes: LoadLevel/GoToMainMenu with missing scenes don't throw or start loading");
            if (!l1Exists) ctx.Check(cap.ErrorContaining("L1_TrainingFacility") && cap.ErrorContaining("Build Settings"), "scenes: missing L1 logs a clear error: " + cap.FirstError);
            ctx.Check(cap.ErrorContaining("no such level"), "scenes: LoadLevel(99) logs 'no such level'");
            if (!menuExists) ctx.Check(cap.ErrorContaining("MainMenu"), "scenes: GoToMainMenu without MainMenu logs a clear error");

            // CompleteLevel => re-clone lost / left-behind members
            gs.NewRun(4242);
            gs.NotifyLevelLoaded(0);
            ctx.Check(gs.LevelIndex == 0 && ShiftersInput.Context == InputContext.Gameplay && gs.ResumeLevelIndex == 0, "session: NotifyLevelLoaded(0) sets level, Gameplay context, resume point");
            ctx.Check(gs.HasSavedRun && gs.PeekSavedRun().LevelIndex == 0, "session: entering a level saves run progress");
            var victim = gs.Team.Members[1];
            string victimName = victim.DisplayName;
            int victimPrimary = victim.Stats.Get(CharacterGenerator.PrimaryOf(victim.Archetype));
            var keep0 = gs.Team.Members[0];
            int completedBefore = gs.Stats.LevelsCompleted;
            gs.CompleteLevel(new LevelResult { LevelIndex = 0, Time = 42f, Survivors = 2, LeftBehind = new[] { victimName }, Switches = 3, Restarts = 1 });
            var clone = gs.Team.Members[1];
            ctx.Check(clone.CloneGeneration == 2 && clone.DisplayName == victim.Name + " II" && clone.Stats.Get(CharacterGenerator.PrimaryOf(clone.Archetype)) == victimPrimary,
                "complete: left-behind '" + victimName + "' re-cloned as '" + clone.DisplayName + "'");
            ctx.Check(ReferenceEquals(gs.Team.Members[0], keep0) && gs.Team.Members[0].CloneGeneration == 1, "complete: survivors untouched");
            ctx.Check(gs.LastRecloned.Length == 1 && gs.LastRecloned[0] == clone.DisplayName, "complete: LastRecloned lists the new clone");
            ctx.Check(gs.HasLastResult && gs.LastResult.Time == 42f && gs.Stats.LevelsCompleted == completedBefore + 1 && gs.IsLevelCompleted(0), "complete: result stored, stats++, level 0 marked completed");
            ctx.Check(gs.ResumeLevelIndex == 1 && gs.PeekSavedRun().LevelIndex == 1 && Mathf.Approximately(gs.BestTime(0), 42f), "complete: saved run resumes at level 1, best time stored");
            int armedAfter = 0; foreach (var m in gs.PeekSavedRun().Team.Members) if (m.HasWeapon) armedAfter++;
            ctx.Check(armedAfter == 1, "complete: saved team still has exactly one weapon");
            gs.CompleteLevel(new LevelResult { LevelIndex = 0, Time = 40f, LeftBehind = new[] { clone.DisplayName } });
            ctx.Check(gs.Team.Members[1].CloneGeneration == 2 && gs.Stats.LevelsCompleted == completedBefore + 1, "complete: duplicate CompleteLevel for the same attempt ignored");
            gs.NotifyLevelLoaded(1);
            gs.CompleteLevel(new LevelResult { LevelIndex = 1, Time = 50f, LeftBehind = new[] { clone.DisplayName, gs.Team.Members[2].DisplayName, "Nobody McGhost" } });
            ctx.Check(gs.Team.Members[1].DisplayName == victim.Name + " III" && gs.Team.Members[2].CloneGeneration == 2 && gs.LastRecloned.Length == 2,
                "complete: second loss => 'III', several left behind, unknown names ignored");
            gs.NotifyLevelLoaded(LevelCatalog.MainGameCount - 1);   // W0: last level of the classic flow (L5)
            gs.CompleteLevel(new LevelResult { LevelIndex = LevelCatalog.MainGameCount - 1, Time = 99f, LeftBehind = new string[0] });
            ctx.Check(gs.ResumeLevelIndex == -1 && !gs.HasSavedRun, "complete: finishing the last level ends the run (saved run deleted)");
            if (!menuExists)
            {
                cap = LogCapture.Begin();
                gs.NextLevel();
                cap.End();
                ctx.Check(gs.ShowVictory && cap.ErrorContaining("MainMenu"), "flow: NextLevel after the last level sets ShowVictory and goes to MainMenu");
                ctx.Check(gs.ConsumeVictory() && !gs.ShowVictory, "flow: ConsumeVictory clears the flag");
            }

            // ContinueRun
            gs.NewRun(99);
            SessionPrefs.SaveRun(gs.Team, 3, gs.Stats);
            string savedTeam = JsonUtility.ToJson(gs.Team);
            gs.NewRun(100);
            if (!l4Exists)
            {
                cap = LogCapture.Begin();
                bool cont = gs.ContinueRun();
                cap.End();
                ctx.Check(cont && JsonUtility.ToJson(gs.Team) == savedTeam && gs.ResumeLevelIndex == 3 && cap.ErrorContaining("L4_IQTest"), "run: ContinueRun restores the team and loads saved level 4 (scene missing => clear error)");
            }

            // checkpoint snapshot is copied
            var snap = new CheckpointSnapshot { CheckpointIndex = 2, ActiveSlot = 1 };
            gs.SaveCheckpoint(snap);
            snap.ActiveSlot = 2;
            ctx.Check(gs.CheckpointIndex == 2 && gs.Snapshot.ActiveSlot == 1, "checkpoint: SaveCheckpoint stores a copy");
            ctx.Log(CharacterGenerator.Describe(gs.Team));
        }

        static IEnumerator PlayTimeChecks(DevPlayContext ctx)
        {
            var gs = GameSession.Instance;
            gs.NotifyLevelLoaded(2);
            ShiftersInput.Context = InputContext.Gameplay;
            float pt = gs.Stats.PlayTime, le = gs.LevelElapsed, u = TimeController.RealTime;
            yield return ctx.Wait(1f);
            float dt = TimeController.RealTime - u;
            ctx.Check(Mathf.Abs(gs.Stats.PlayTime - pt - dt) < 0.05f && Mathf.Abs(gs.LevelElapsed - le - dt) < 0.05f && dt > 0.9f, "playtime: PlayTime/LevelElapsed track unscaled gameplay time (+" + (gs.Stats.PlayTime - pt).ToString("0.00") + "s over " + dt.ToString("0.00") + "s)");
            TimeController.SetPaused(true);
            pt = gs.Stats.PlayTime;
            yield return ctx.Wait(0.5f);
            ctx.Check(Mathf.Approximately(gs.Stats.PlayTime, pt), "playtime: not counted while paused");
            TimeController.SetPaused(false);
            ShiftersInput.Context = InputContext.Menu;
            pt = gs.Stats.PlayTime;
            yield return ctx.Wait(0.5f);
            ctx.Check(Mathf.Approximately(gs.Stats.PlayTime, pt), "playtime: not counted in menus");
            ShiftersInput.Context = InputContext.Gameplay;
        }

        // ================================================================ real scene reloads (needs a scene in Build Settings)
        static IEnumerator SceneFlowChecks(DevPlayContext ctx)
        {
            var gs = GameSession.Instance;
            var active = SceneManager.GetActiveScene();
            if (active.buildIndex < 0) { ctx.Log("scene flow skipped: run with -devScene Assets/Scenes/SampleScene.unity"); yield break; }
            gs.NotifyLevelLoaded(-1); // not a catalog level: dev/test scene
            ctx.Check(gs.LevelIndex == -1, "flow: NotifyLevelLoaded(-1) in a non-catalog scene => test level");
            gs.SaveCheckpoint(new CheckpointSnapshot { CheckpointIndex = 1, ActiveSlot = 2 });
            TimeController.SetSlowMo(true);
            yield return ctx.Wait(0.3f);
            int restarts = gs.Stats.Restarts;
            var marker = new GameObject("CoreSessionMarker");
            gs.RestartFromCheckpoint();
            ctx.Check(gs.IsLoading && ShiftersInput.Context == InputContext.Cutscene && Mathf.Approximately(Time.timeScale, 1f) && !TimeController.IsSlowMo,
                "flow: restart => loading, Cutscene context, time reset immediately");
            gs.RestartLevel(); // ignored while loading
            ctx.Check(gs.Stats.Restarts == restarts + 1, "flow: second restart while loading is ignored");
            float u0 = TimeController.RealTime;
            yield return WaitUntil(() => !gs.IsLoading, 1200);
            ctx.Check(!gs.IsLoading && marker == null, "flow: scene actually reloaded (" + (TimeController.RealTime - u0).ToString("0.00") + "s)");
            ctx.Check(gs.Snapshot != null && gs.CheckpointIndex == 1 && gs.Snapshot.ActiveSlot == 2, "flow: RestartFromCheckpoint keeps the snapshot");
            float waitStart = TimeController.RealTime;
            yield return WaitUntil(() => ShiftersInput.Context == InputContext.Gameplay, 600);
            float waited = TimeController.RealTime - waitStart;
            ctx.Check(ShiftersInput.Context == InputContext.Gameplay && waited > 1.5f, "flow: without a LevelManager the session takes over after the 2 s timeout (" + waited.ToString("0.00") + "s)");

            gs.RestartLevel();
            yield return null;
            yield return WaitUntil(() => !gs.IsLoading, 1200);
            ctx.Check(gs.Snapshot == null && gs.Stats.Restarts == restarts + 2 && gs.LevelRestarts >= 2, "flow: RestartLevel clears the snapshot, counts restarts");
            // LevelManager-style notification ends the wait early
            yield return null;
            gs.NotifyLevelLoaded(-1);
            ctx.Check(ShiftersInput.Context == InputContext.Gameplay, "flow: NotifyLevelLoaded during the wait hands control to gameplay at once");
            yield return ctx.Wait(0.2f);
            ctx.Check(Camera.main != null, "flow: reloaded scene has its camera (harness screenshots keep working)");
            ctx.Shot("after_reload");
        }

        // ================================================================ helpers
        /// <summary>Captures log output (so expected errors don't fail the harness) and lets tests inspect it.</summary>
        sealed class LogCapture : ILogHandler
        {
            readonly ILogHandler _prev;
            public readonly List<string> Errors = new List<string>();
            LogCapture(ILogHandler prev) { _prev = prev; }
            public static LogCapture Begin() { var c = new LogCapture(Debug.unityLogger.logHandler); Debug.unityLogger.logHandler = c; return c; }
            public void End() => Debug.unityLogger.logHandler = _prev;
            public string FirstError => Errors.Count > 0 ? Errors[0] : "(none)";
            public bool ErrorContaining(string s) { foreach (var e in Errors) if (e.Contains(s)) return true; return false; }
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                string msg = args != null && args.Length > 0 ? string.Format(format, args) : format;
                if (logType == LogType.Error || logType == LogType.Assert || logType == LogType.Exception) Errors.Add(msg);
                else _prev.LogFormat(logType, context, format, args);
            }
            public void LogException(Exception exception, UnityEngine.Object context) => Errors.Add("EXCEPTION " + exception.Message);
        }

        /// <summary>Back up the SHIFTERS PlayerPrefs keys (the Editor shares them with the real project) and restore them.</summary>
        sealed class PrefsBackup
        {
            readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
            readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
            readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            static string[] StringKeys => new[] { SessionPrefs.SettingsKey, SessionPrefs.RunKey };

            public static PrefsBackup Capture()
            {
                var b = new PrefsBackup();
                foreach (var k in StringKeys) if (PlayerPrefs.HasKey(k)) b._strings[k] = PlayerPrefs.GetString(k);
                if (PlayerPrefs.HasKey(SessionPrefs.CompletedKey)) b._ints[SessionPrefs.CompletedKey] = PlayerPrefs.GetInt(SessionPrefs.CompletedKey);
                for (int i = 0; i < LevelCatalog.Count; i++) { string k = SessionPrefs.BestTimePrefix + i; if (PlayerPrefs.HasKey(k)) b._floats[k] = PlayerPrefs.GetFloat(k); }
                return b;
            }

            public void Restore()
            {
                foreach (var k in StringKeys) { if (_strings.TryGetValue(k, out var v)) PlayerPrefs.SetString(k, v); else PlayerPrefs.DeleteKey(k); }
                if (_ints.TryGetValue(SessionPrefs.CompletedKey, out int c)) PlayerPrefs.SetInt(SessionPrefs.CompletedKey, c); else PlayerPrefs.DeleteKey(SessionPrefs.CompletedKey);
                for (int i = 0; i < LevelCatalog.Count; i++)
                {
                    string k = SessionPrefs.BestTimePrefix + i;
                    if (_floats.TryGetValue(k, out float f)) PlayerPrefs.SetFloat(k, f); else PlayerPrefs.DeleteKey(k);
                }
                PlayerPrefs.Save();
            }
        }
    }
}
