// =====================================================================================
//  SHIFTERS - SeamsTests.cs   [SEAMS DevPlay suite, campaign wave]
//  Proves every campaign hook seams added to core_session / core_character / perspective_level / builder.
//  Run (one Unity launch, starts in SampleScene, loads L1 through NextLevel):
//    $SP/tools/devplay.sh <proj> seams -devSetup Shifters.SeamsTests.RunAll -devScene Assets/Scenes/SampleScene.unity -devSeconds 200
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Shifters
{
    public static class SeamsTests
    {
        static readonly List<string> _banners = new List<string>();
        static readonly List<string> _toasts = new List<string>();
        static void OnBanner(string t, string s, BannerStyle st) => _banners.Add(t + "|" + s);
        static void OnToast(string t, Color c) => _toasts.Add(t);

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            int present = SessionPrefs.BackupAll();
            ctx.Log("prefs backed up (" + present + " keys present)");
            GameEvents.Banner += OnBanner;
            GameEvents.Toast += OnToast;
            try { BuilderChecks(ctx); } catch (Exception e) { ctx.Check(false, "builder section threw: " + e); }
            yield return InputChecks(ctx);
            yield return LevelChecks(ctx);
            GameEvents.Banner -= OnBanner;
            GameEvents.Toast -= OnToast;
            GameSession.NextLevelOverride = null;
            LevelManager.CampaignIntroHook = null;
            LevelExit.GlobalPrecondition = null;
            ShiftersInput.DeviceFilter = null;
            PrefsChecks(ctx); // last: also restores the backup taken above
            ctx.Log("seams tests finished");
        }

        // ================================================================ builder (editor assembly via reflection)
        static void BuilderChecks(DevPlayContext ctx)
        {
            Type b = null, bs = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (b == null) b = asm.GetType("Shifters.EditorTools.ShiftersBuilder");
                if (bs == null) bs = asm.GetType("Shifters.EditorTools.ShiftersBuildSettings");
            }
            if (b == null) { ctx.Log("builder checks skipped (no editor assembly)"); return; }
            var parse = b.GetMethod("ParseLevelArg", BindingFlags.NonPublic | BindingFlags.Static);
            ctx.Check(parse != null, "builder: ParseLevelArg exists");
            if (parse != null)
            {
                Func<string, int> P = a => (int)parse.Invoke(null, new object[] { a });
                ctx.Check(P("0") == 0 && P("4") == 4 && P("13") == 13 && P("21") == 21, "builder: -devLevel accepts any catalog index (0, 4, 13, 21)");
                ctx.Check(P("L1") == 0 && P("L5") == 4 && P("l6") == 5 && P("L10") == 9 && P("L14") == 13 && P("L22") == 21, "builder: -devLevel L<n> incl. n >= 10 (L10 -> 9, L22 -> 21)");
                ctx.Check(P("L6_PerformanceReview") == 5 && P("L12_Unmasked") == 11 && P("L1_TrainingFacility") == 0, "builder: -devLevel scene names (catalog)");
                ctx.Check(P("L30_NotInCatalog") == 29, "builder: L<n>_Name outside the catalog still maps to level n");
                ctx.Check(P("garbage") == int.MinValue && P("L") == int.MinValue && P("L0x") == int.MinValue, "builder: garbage rejected");
                ctx.Check(P("sandbox") == L0_Sandbox.Index && P("gallery") == L0_KitGallery.Index, "builder: sandbox / gallery still work");
            }
            if (bs != null)
            {
                var desired = bs.GetMethod("Desired", BindingFlags.Public | BindingFlags.Static);
                var list = desired != null ? desired.Invoke(null, null) as System.Collections.IList : null;
                if (list != null)
                {
                    var paths = new List<string>();
                    foreach (var o in list) paths.Add((string)o.GetType().GetProperty("path").GetValue(o));
                    int last = -1; bool ordered = true, allExist = true;
                    foreach (var p in paths)
                    {
                        int idx = LevelCatalog.IndexOfScene(System.IO.Path.GetFileNameWithoutExtension(p));
                        if (idx < 0) continue;
                        if (idx < last) ordered = false;
                        last = idx;
                        if (!System.IO.File.Exists(p)) allExist = false;
                    }
                    ctx.Check(paths.Count > 0 && paths[0].EndsWith(LevelCatalog.MainMenuScene + ".unity"), "build settings: MainMenu first");
                    ctx.Check(ordered && allExist, "build settings: catalog scenes in catalog order, only existing files (" + paths.Count + " entries)");
                }
            }
        }

        // ================================================================ prefs backup
        static void PrefsChecks(DevPlayContext ctx)
        {
            // the suite's own backup (taken at start) is still held: nested BackupAll is ignored
            ctx.Check(SessionPrefs.HasBackup, "prefs: backup held during the suite");
            SessionPrefs.RestoreAll();
            ctx.Check(!SessionPrefs.HasBackup, "prefs: RestoreAll drops the backup");

            const string extra = "shifters_seams_test";
            SessionPrefs.RegisterBackupKey(extra);
            PlayerPrefs.SetString(SessionPrefs.SettingsKey, "{\"MasterVolume\":0.31}");
            PlayerPrefs.SetInt(SessionPrefs.CompletedKey, 5);
            PlayerPrefs.SetFloat(SessionPrefs.BestTimePrefix + 2, 77.5f);
            PlayerPrefs.DeleteKey(SessionPrefs.BestTimePrefix + 13);
            PlayerPrefs.DeleteKey(extra);
            var origRun = PlayerPrefs.GetString(SessionPrefs.RunKey, null);
            int n = SessionPrefs.BackupAll();
            ctx.Check(n >= 3 && SessionPrefs.BackupKeys().Contains(extra) && SessionPrefs.BackupKeys().Contains(SessionPrefs.BestTimePrefix + 21),
                "prefs: BackupAll covers settings/completed/best_0..21+/registered keys (" + n + " present)");
            PlayerPrefs.SetString(SessionPrefs.SettingsKey, "garbage");
            PlayerPrefs.SetInt(SessionPrefs.CompletedKey, 31);
            PlayerPrefs.DeleteKey(SessionPrefs.BestTimePrefix + 2);
            PlayerPrefs.SetFloat(SessionPrefs.BestTimePrefix + 13, 1f);
            PlayerPrefs.SetString(extra, "x");
            SessionPrefs.RestoreAll();
            ctx.Check(PlayerPrefs.GetString(SessionPrefs.SettingsKey, "") == "{\"MasterVolume\":0.31}", "prefs: string restored");
            ctx.Check(PlayerPrefs.GetInt(SessionPrefs.CompletedKey, -1) == 5, "prefs: int restored (type kept)");
            ctx.Check(Mathf.Approximately(PlayerPrefs.GetFloat(SessionPrefs.BestTimePrefix + 2, -1f), 77.5f), "prefs: float restored (type kept)");
            ctx.Check(!PlayerPrefs.HasKey(SessionPrefs.BestTimePrefix + 13) && !PlayerPrefs.HasKey(extra), "prefs: keys absent before the backup are deleted again");
            ctx.Check(PlayerPrefs.GetString(SessionPrefs.RunKey, null) == origRun, "prefs: untouched key unchanged");
            ctx.Check(!SessionPrefs.RestoreAll(), "prefs: RestoreAll without a backup = no-op");
            // put the real values back (this block changed them after the suite's restore)
            PlayerPrefs.DeleteKey(SessionPrefs.SettingsKey); PlayerPrefs.DeleteKey(SessionPrefs.CompletedKey); PlayerPrefs.DeleteKey(SessionPrefs.BestTimePrefix + 2);
            if (_settingsAtStart != null) PlayerPrefs.SetString(SessionPrefs.SettingsKey, _settingsAtStart);
            if (_completedAtStart != int.MinValue) PlayerPrefs.SetInt(SessionPrefs.CompletedKey, _completedAtStart);
            if (_best2AtStart > -1e8f) PlayerPrefs.SetFloat(SessionPrefs.BestTimePrefix + 2, _best2AtStart);
            PlayerPrefs.Save();
        }
        static string _settingsAtStart;
        static int _completedAtStart = int.MinValue;
        static float _best2AtStart = -1e9f;

        // ================================================================ input
        static IEnumerator InputChecks(DevPlayContext ctx)
        {
            _settingsAtStart = PlayerPrefs.HasKey(SessionPrefs.SettingsKey) ? PlayerPrefs.GetString(SessionPrefs.SettingsKey) : null;
            _completedAtStart = PlayerPrefs.HasKey(SessionPrefs.CompletedKey) ? PlayerPrefs.GetInt(SessionPrefs.CompletedKey) : int.MinValue;
            _best2AtStart = PlayerPrefs.HasKey(SessionPrefs.BestTimePrefix + 2) ? PlayerPrefs.GetFloat(SessionPrefs.BestTimePrefix + 2) : -1e9f;

            var settings = InputSystem.settings;
            var oldBg = settings.backgroundBehavior;
            var oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var kb = InputSystem.AddDevice<Keyboard>("SeamsKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>("SeamsMouse");
            var pad = InputSystem.AddDevice<Gamepad>("SeamsPad");
            yield return null;

            ShiftersInput.Context = InputContext.Cutscene;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space));
            yield return null;
            ctx.Check(ShiftersInput.AdvancePressed, "input: Space => AdvancePressed (Cutscene)");
            yield return null;
            ctx.Check(!ShiftersInput.AdvancePressed, "input: AdvancePressed lasts one frame");
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Enter)); yield return null;
            ctx.Check(ShiftersInput.AdvancePressed, "input: Enter => AdvancePressed");
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
            ctx.Check(ShiftersInput.AdvancePressed, "input: left click => AdvancePressed");
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            ctx.Check(ShiftersInput.AdvancePressed, "input: gamepad A => AdvancePressed");
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;

            ShiftersInput.Context = InputContext.Gameplay;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space)); yield return null;
            ctx.Check(!ShiftersInput.AdvancePressed && ShiftersInput.JumpPressed, "input: in Gameplay Space is Jump, not Advance");
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null;

            // ---- skip hold
            ShiftersInput.Context = InputContext.Cutscene;
            yield return null;
            ctx.Check(ShiftersInput.SkipHeld01 == 0f, "input: SkipHeld01 = 0 while released");
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Backspace)); yield return null;
            float t0 = Time.unscaledTime;
            float early = ShiftersInput.SkipHeld01;
            while (Time.unscaledTime - t0 < 0.5f) yield return null;
            float mid = ShiftersInput.SkipHeld01;
            while (Time.unscaledTime - t0 < 1.15f) yield return null;
            float full = ShiftersInput.SkipHeld01;
            ctx.Check(early < 0.2f && mid > 0.3f && mid < 0.8f && full >= 1f,
                "input: Backspace hold => SkipHeld01 rises 0 -> 1 over SkipHoldSeconds (" + early.ToString("0.00") + ", " + mid.ToString("0.00") + ", " + full.ToString("0.00") + ")");
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null;
            ctx.Check(ShiftersInput.SkipHeld01 == 0f, "input: release => SkipHeld01 back to 0");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Select)); yield return null;
            float p0 = Time.unscaledTime;
            while (Time.unscaledTime - p0 < 0.6f) yield return null;
            float padHold = ShiftersInput.SkipHeld01;
            ShiftersInput.Context = InputContext.Menu;   // context change restarts a running hold
            float afterSwitch = ShiftersInput.SkipHeld01;
            ctx.Check(padHold > 0.4f && afterSwitch < 0.1f, "input: gamepad Back hold works; a context change restarts it (" + padHold.ToString("0.00") + " -> " + afterSwitch.ToString("0.00") + ")");
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;

            // ---- device filter
            ShiftersInput.Context = InputContext.Cutscene;
            yield return null;
            ShiftersInput.DeviceFilter = new InputDevice[] { pad };
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space)); yield return null;
            bool kbBlocked = !ShiftersInput.AdvancePressed;
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
            bool mouseBlocked = !ShiftersInput.AdvancePressed && !ShiftersInput.LeftClickPressed;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            bool padWorks = ShiftersInput.AdvancePressed;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
            ctx.Check(kbBlocked && mouseBlocked && padWorks, "input: DeviceFilter = {pad} blocks keyboard + mouse, pad still works");
            ShiftersInput.DeviceFilter = null;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space)); yield return null;
            ctx.Check(ShiftersInput.AdvancePressed && ShiftersInput.DeviceFilter == null, "input: DeviceFilter = null => all devices again");
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null;

            ShiftersInput.Context = InputContext.Gameplay;
            InputSystem.RemoveDevice(kb); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(pad);
            settings.backgroundBehavior = oldBg;
            settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
        }

        // ================================================================ level hooks (loads L1)
        static IEnumerator WaitUntil(Func<bool> cond, float seconds)
        {
            float t = Time.unscaledTime;
            while (!cond() && Time.unscaledTime - t < seconds) yield return null;
        }

        static IEnumerator LevelChecks(DevPlayContext ctx)
        {
            var gs = GameSession.Instance;
            if (gs == null) { ctx.Check(false, "level: no GameSession"); yield break; }
            gs.NewRun(4242);
            gs.ShowVictory = false;

            // ---- NextLevelOverride: true = handled
            int calls = 0;
            GameSession.NextLevelOverride = () => { calls++; return true; };
            gs.NextLevel();
            ctx.Check(calls == 1 && !gs.IsLoading, "session: NextLevelOverride true => NextLevel does nothing else");
            GameSession.NextLevelOverride = () => { calls++; return false; };
            int lmCalls = 0; LevelManager seen = null;
            LevelManager.CampaignIntroHook = lm => { lmCalls++; seen = lm; lm.IntroTitle = "SEAMS TITLE"; lm.IntroSubtitle = "Shift 99 - Hooks"; lm.ObjectiveOverride = "SEAMS OBJECTIVE"; };
            _banners.Clear();
            // false = classic flow (from the SampleScene, LevelIndex -1 -> L1)
            gs.NextLevel();
            ctx.Check(calls == 2 && gs.IsLoading, "session: NextLevelOverride false => classic flow (loads L1)");
            yield return WaitUntil(() => LevelManager.Instance != null && LevelManager.Instance.IsStarted, 30f);
            var lmi = LevelManager.Instance;
            ctx.Check(lmi != null && lmi.IsStarted && lmi.LevelIndex == 0, "level: L1 started");
            if (lmi == null) yield break;
            ctx.Check(lmCalls == 1 && seen == lmi, "level: CampaignIntroHook invoked once with the LevelManager");
            ctx.Check(lmi.Objective == "SEAMS OBJECTIVE", "level: hook's ObjectiveOverride applied (" + lmi.Objective + ")");
            ctx.Check(_banners.Contains("SEAMS TITLE|Shift 99 - Hooks"), "level: hook's IntroTitle/IntroSubtitle used for the start banner");
            LevelManager.CampaignIntroHook = null;
            GameSession.NextLevelOverride = null;
            yield return ctx.Wait(1.2f);
            var cm = CharacterManager.Instance;
            ctx.Check(cm != null && cm.Team.Count == 3, "level: team spawned");
            if (cm == null || cm.Team.Count < 3) yield break;

            // ---- checkpoints
            var cps = new List<Checkpoint>(Checkpoint.All);
            cps.Sort((a, b) => a.Index.CompareTo(b.Index));
            ctx.Check(cps.Count >= 3, "level: L1 has >= 3 checkpoints (" + cps.Count + ")");
            var cpm = CheckpointManager.Instance;
            int activations = 0;
            Action<Checkpoint> onAct = c => activations++;
            CheckpointManager.CheckpointActivated += onAct;
            int i0 = cps[0].Index;
            bool ok = CheckpointManager.ActivateFromScript(i0);
            ctx.Check(ok && CheckpointManager.Instance.CurrentIndex == i0 && gs.CheckpointIndex == i0 && activations == 1,
                "checkpoint: ActivateFromScript(first) activates + saves the snapshot + raises CheckpointActivated");
            var victim = cm.Get(1);
            victim.KnockOut("seams test");
            yield return null;
            ok = CheckpointManager.ActivateFromScript(i0);
            yield return null;
            ctx.Check(ok && victim.State == CharacterState.Alive && activations == 1 && gs.Snapshot.States[1] == CharacterState.Alive,
                "checkpoint: ActivateFromScript(current) revives the Downed and re-saves quietly");
            ok = CheckpointManager.ActivateFromScript(9999);
            ctx.Check(!ok, "checkpoint: unknown index => false");
            CheckpointManager.CheckpointActivated -= onAct;

            // ---- scared flag
            var c0 = cm.Get(0);
            int ev = 0, anyEv = 0;
            Action<ShifterCharacter> h1 = c => ev++, h2 = c => anyEv++;
            c0.ScaredChanged += h1; ShifterCharacter.AnyScaredChanged += h2;
            float hp = c0.Health; var st = c0.Stats != null ? c0.Stats.Speed : -1;
            c0.SetScared(true); c0.SetScared(true);
            ctx.Check(c0.IsScared && ev == 1 && anyEv == 1, "scared: SetScared(true) sets IsScared, raises once (no repeat)");
            ctx.Check(Mathf.Approximately(c0.Health, hp) && (c0.Stats == null || c0.Stats.Speed == st), "scared: no stat change");
            c0.SetScared(false);
            ctx.Check(!c0.IsScared && ev == 2 && anyEv == 2, "scared: SetScared(false) clears + raises");
            var c2 = cm.Get(2);
            c2.SetScared(true);
            c2.KnockOut("seams scare");
            yield return null;
            bool clearedOnKo = !c2.IsScared;
            c2.SetScared(true);
            ctx.Check(clearedOnKo && !c2.IsScared, "scared: cleared when Downed; cannot be scared while not Alive");
            c2.Revive();
            c0.ScaredChanged -= h1; ShifterCharacter.AnyScaredChanged -= h2;

            // ---- camera suppression
            var sc = ShiftersCamera.Instance;
            ctx.Check(sc != null && sc.Cam != null, "camera: ShiftersCamera present");
            if (sc != null)
            {
                var repGo = new GameObject("SeamsCutsceneCam");
                var rep = repGo.AddComponent<Camera>();
                rep.enabled = false;
                var lp = cm.Active.transform.position;
                repGo.transform.position = lp + new Vector3(0f, 12f, -14f);
                repGo.transform.LookAt(lp);
                sc.SuppressMainCamera(true, rep);
                yield return null;
                int enabledCams = 0;
                foreach (var cam in Camera.allCameras) if (cam.enabled && cam.gameObject.activeInHierarchy && cam.targetTexture == null) enabledCams++;
                ctx.Check(!sc.Cam.enabled && ShiftersCamera.MainCameraSuppressed && Camera.main == rep && rep.enabled,
                    "camera: SuppressMainCamera(true, cutscene) => main off, replacement is Camera.main");
                ctx.Check(enabledCams == 1, "camera: exactly one screen camera renders (" + enabledCams + ")");
                ctx.Shot("camera_suppressed_wide", rep);
                sc.SuppressMainCamera(false);
                yield return null;
                ctx.Check(sc.Cam.enabled && !ShiftersCamera.MainCameraSuppressed && Camera.main == sc.Cam && !rep.CompareTag("MainCamera"),
                    "camera: SuppressMainCamera(false) restores the gameplay camera, untags the replacement");
                UnityEngine.Object.Destroy(repGo);
            }

            // ---- reprint + kiosk
            var lostOne = cm.Get(2);
            int gen = lostOne.Profile.CloneGeneration;
            string oldName = lostOne.DisplayName;
            ctx.Check(cm.Reprint(2, Vector3.zero, Quaternion.identity) == null && !cm.CanReprint(2), "reprint: refused while the intern is not Lost");
            lostOne.Lose("seams test");
            yield return ctx.Wait(0.3f);
            ctx.Check(cm.CanReprint(2), "reprint: CanReprint once Lost");
            var act = cm.Active;
            Vector3 kpos = act.transform.position + act.transform.forward * 3f;
            var kiosk = LevelKit.ReprintKiosk(null, kpos, Quaternion.LookRotation(-act.transform.forward));
            kiosk.PrintSeconds = 1f;
            ShifterCharacter printed = null;
            Action<ShifterCharacter> onRep = c => printed = c;
            CharacterManager.Reprinted += onRep;
            int reprintsBefore = gs.Stats.Reprints;
            bool can = kiosk.CanInteract(act, out _);
            string prompt = kiosk.GetPrompt(act);
            ctx.Check(can && prompt == "REPRINT " + oldName.ToUpperInvariant(), "kiosk: offers \"" + prompt + "\" while an intern is Lost");
            kiosk.Interact(act);
            yield return ctx.Wait(0.5f);
            bool busyCan = kiosk.CanInteract(act, out string busy);
            ctx.Check(kiosk.IsPrinting && !busyCan && busy != null && busy.StartsWith("PRINTING"), "kiosk: PAPERWORK runs (busy prompt: " + busy + ")");
            ctx.Shot("kiosk_printing");
            yield return ctx.Wait(1.0f);
            var fresh = cm.Get(2);
            ctx.Check(fresh != null && fresh != lostOne && fresh.IsAlive && printed == fresh && kiosk.ReprintCount == 1, "reprint: slot 2 replaced by a new Alive intern (Reprinted raised)");
            if (fresh != null)
            {
                ctx.Check(fresh.Profile.CloneGeneration == Mathf.Max(1, gen) + 1, "reprint: CloneGeneration + 1 (" + gen + " -> " + fresh.Profile.CloneGeneration + ")");
                ctx.Check(gs.Team.Members[2] == fresh.Profile && gs.Stats.Reprints == reprintsBefore + 1, "reprint: session team + Stats.Reprints updated");
                ctx.Check(Vector3.Distance(fresh.transform.position, kiosk.OutputPose.position) < 1.5f, "reprint: appears on the output tray");
                ctx.Check(gs.Snapshot != null && gs.Snapshot.States[2] == CharacterState.Alive, "kiosk: checkpoint re-saved with the reprinted intern alive");
                int weapons = 0; for (int i = 0; i < 3; i++) if (cm.Get(i).Weapon != null && cm.Get(i).Weapon.HasWeapon) weapons++;
                ctx.Check(weapons <= 1, "reprint: gun not duplicated (" + weapons + " holders)");
            }
            CharacterManager.Reprinted -= onRep;
            bool noneCan = kiosk.CanInteract(cm.Active, out string none);
            ctx.Check(!noneCan && none == null, "kiosk: no prompt when nobody is Lost");
            yield return ctx.Wait(0.6f);
            ctx.Shot("kiosk_after");

            // ---- promoted copies: safety net, holo walkway, threat label
            var basePos = cm.Active.transform.position;
            var net = LevelKit.SafetyNet(null, basePos + new Vector3(0f, -60f, 0f), new Vector3(30f, 6f, 30f), basePos + Vector3.up * 0.2f, 90f);
            var faller = cm.Get(1);
            Vector3 fallerHome = faller.transform.position;
            faller.Motor.Teleport(basePos + new Vector3(0f, -58f, 0f), Quaternion.identity);
            yield return ctx.Wait(0.5f);
            ctx.Check(net.Catches >= 1 && Vector3.Distance(faller.transform.position, basePos) < 3f, "safety net: catches a falling intern and returns them (" + net.Catches + ")");
            faller.Motor.Teleport(fallerHome, Quaternion.identity);

            var walkRoot = new GameObject("SeamsWalkway").transform;
            walkRoot.position = basePos + new Vector3(0f, 0.3f, 6f);
            var tiles = PerspectiveKit.HoloWalkway(walkRoot, new[] { Vector3.zero, new Vector3(0f, 0f, 8f), new Vector3(6.4f, 0f, 8f) });
            int corners = 0; bool linked = true;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i].name.StartsWith("HoloCorner")) corners++;
                int want = (i > 0 ? 1 : 0) + (i < tiles.Length - 1 ? 1 : 0);
                if (tiles[i].Neighbours == null || tiles[i].Neighbours.Length != want) linked = false;
            }
            ctx.Check(tiles.Length >= 7 && corners == 1 && linked, "holo walkway: " + tiles.Length + " tiles, 1 corner, neighbours linked (default width 1.6)");

            // threat label on a cube behind a wall
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "SeamsThreat";
            UnityEngine.Object.Destroy(target.GetComponent<Collider>());
            target.transform.position = basePos + cm.Active.transform.forward * 9f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "SeamsWall";
            wall.transform.position = basePos + cm.Active.transform.forward * 5f + Vector3.up * 2f;
            wall.transform.rotation = cm.Active.transform.rotation;
            wall.transform.localScale = new Vector3(8f, 6f, 0.4f);
            var marker = PerspectiveKit.ThreatLabel(target.transform, "THREAT: {m} M");
            marker.Height = 1.2f;
            int brainSlot = -1, otherSlot = -1;
            for (int i = 0; i < 3; i++)
            {
                var c = cm.Get(i);
                if (c == null || !c.IsAlive) continue;
                bool iq = (c.Perspectives & PerspectiveMask.Intellect) != 0;
                if (iq && brainSlot < 0) brainSlot = i; else if (!iq && otherSlot < 0) otherSlot = i;
            }
            ctx.Check(brainSlot >= 0 && otherSlot >= 0, "threat: team has an IQ viewer and a non-IQ viewer");
            if (brainSlot >= 0 && otherSlot >= 0)
            {
                cm.SwitchTo(otherSlot, true);
                yield return ctx.Wait(0.6f);
                ctx.Check(!marker.IsShown, "threat: hidden for a non-IQ intern");
                cm.SwitchTo(brainSlot, true);
                yield return ctx.Wait(0.8f);
                int m = Mathf.RoundToInt(Vector3.Distance(cm.Active.transform.position, target.transform.position));
                ctx.Check(marker.IsShown && marker.ShownText == "THREAT: " + m + " M", "threat: shown for the Brainiac with live meters (\"" + marker.ShownText + "\")");
                ctx.Shot("threat_through_wall");
            }

            // ---- exit precondition
            var exit = LevelExit.All.Count > 0 ? LevelExit.All[0] : null;
            ctx.Check(exit != null, "exit: L1 has a LevelExit");
            if (exit != null)
            {
                UnityEngine.Object.Destroy(wall);
                exit.Precondition = () => "BOSS FIRST";
                _toasts.Clear();
                PutTeamInExit(cm, exit);
                yield return ctx.Wait(1.2f);
                ctx.Check(!exit.IsCompleting && exit.RefusalCount > 0 && exit.LastRefusal == "BOSS FIRST" && _toasts.Contains("BOSS FIRST"),
                    "exit: Precondition refusal keeps the exit open and toasts the reason");
                exit.LeaveWithoutThem();
                ctx.Check(!exit.IsCompleting, "exit: LeaveWithoutThem refused too");
                exit.Precondition = null;
                LevelExit.GlobalPrecondition = () => "GLOBAL NO";
                yield return ctx.Wait(0.5f);
                ctx.Check(!exit.IsCompleting && exit.LastRefusal == "GLOBAL NO", "exit: GlobalPrecondition refuses as well");
                LevelExit.GlobalPrecondition = null;
                var gate = LevelKit.ExitWeightLimit(exit, 60f);
                yield return ctx.Wait(1.2f);
                ctx.Check(gate.IsOverweight && !exit.IsCompleting && exit.LastRefusal != null && exit.LastRefusal.Contains("KG") || gate.IsOverweight && !exit.IsCompleting && exit.LastRefusal != null && exit.LastRefusal.Contains("INTERN"),
                    "exit weight limit: " + gate.CurrentKg.ToString("0") + " kg > 60 => refused (\"" + exit.LastRefusal + "\")");
                var cam = ShiftersCamera.Instance;
                if (cam != null) cam.FocusOn(exit.transform.position + Vector3.up * 2f, 2f);
                yield return ctx.Wait(0.4f);
                ctx.Shot("exit_weight_gate");
            }

            // ---- RestartAtNextCheckpoint (reloads the scene)
            int hrBefore = gs.Stats.HRInterventions;
            int cur = gs.CheckpointIndex;
            int expectNext = -1;
            foreach (var c in cps) if (c.Index > cur) { expectNext = c.Index; break; }
            cm.Get(0).KnockOut("seams hr");
            bool hr = gs.RestartAtNextCheckpoint();
            ctx.Check(hr && gs.IsLoading && gs.Stats.HRInterventions == hrBefore + 1, "session: RestartAtNextCheckpoint reloads, counts HRInterventions");
            yield return WaitUntil(() => !gs.IsLoading && LevelManager.Instance != null && LevelManager.Instance.IsStarted, 30f);
            yield return ctx.Wait(1f);
            lmi = LevelManager.Instance; cm = CharacterManager.Instance;
            ctx.Check(lmi != null && lmi.StartedFromCheckpoint && gs.CheckpointIndex == expectNext && CheckpointManager.Instance.CurrentIndex == expectNext,
                "session: restarted at the NEXT checkpoint (" + cur + " -> " + gs.CheckpointIndex + ")");
            if (cm != null)
            {
                int alive = 0; for (int i = 0; i < cm.Team.Count; i++) if (cm.Get(i).IsAlive) alive++;
                var cpNext = CheckpointManager.Find(expectNext);
                float d = cpNext != null && cm.Active != null ? Vector3.Distance(cm.Active.transform.position, cpNext.GetSpawnPose(cm.Active.Slot).position) : 99f;
                ctx.Check(alive == 3 && d < 3f, "session: everyone alive at the next checkpoint's spawns (" + alive + " alive, " + d.ToString("0.0") + " m)");
            }
            ctx.Shot("hr_next_checkpoint");
            // walk to the last checkpoint, then it refuses
            int guard = 0;
            while (guard++ < 10)
            {
                int c = gs.CheckpointIndex;
                bool any = false; foreach (var cp in Checkpoint.All) if (cp != null && cp.Index > c) any = true;
                if (!any) break;
                gs.RestartAtNextCheckpoint();
                yield return WaitUntil(() => !gs.IsLoading && LevelManager.Instance != null && LevelManager.Instance.IsStarted, 30f);
                yield return ctx.Wait(0.3f);
            }
            bool lastRefused = !gs.RestartAtNextCheckpoint() && !gs.IsLoading;
            ctx.Check(lastRefused, "session: RestartAtNextCheckpoint at the last checkpoint => false, no reload");

            // ---- exit completes without a precondition; NextLevelOverride routes afterwards
            exit = LevelExit.All.Count > 0 ? LevelExit.All[0] : null;
            cm = CharacterManager.Instance;
            if (exit != null && cm != null)
            {
                int routed = 0;
                GameSession.NextLevelOverride = () => { routed++; return true; };
                GameSession.SkipClassicRunSave = true;
                string runBefore = PlayerPrefs.GetString(SessionPrefs.RunKey, "<none>");
                bool done = false; Action<LevelResult> onDone = r => done = true;
                GameEvents.LevelCompleted += onDone;
                PutTeamInExit(cm, exit);
                yield return WaitUntil(() => done, 8f);
                GameEvents.LevelCompleted -= onDone;
                ctx.Check(done && exit.IsCompleted, "exit: completes when the precondition is null");
                ctx.Check(PlayerPrefs.GetString(SessionPrefs.RunKey, "<none>") == runBefore, "session: SkipClassicRunSave leaves the classic SavedRun untouched");
                gs.NextLevel();
                ctx.Check(routed == 1 && !gs.IsLoading, "session: NEXT SHIFT after completion routed by NextLevelOverride");
                GameSession.NextLevelOverride = null;
                GameSession.SkipClassicRunSave = false;
            }
        }

        // ================================================================ visual pass (screenshots of the new kit pieces)
        //  devplay.sh <proj> seams_vis -devSetup Shifters.SeamsTests.Visuals -devScene Assets/Scenes/SampleScene.unity -devSeconds 60
        public static IEnumerator Visuals(DevPlayContext ctx)
        {
            SessionPrefs.BackupAll();
            var gs = GameSession.Instance;
            gs.NewRun(777);
            gs.LoadLevel(0);
            yield return WaitUntil(() => LevelManager.Instance != null && LevelManager.Instance.IsStarted, 30f);
            yield return ctx.Wait(1.5f);
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Active == null) { ctx.Check(false, "vis: L1 team"); SessionPrefs.RestoreAll(); yield break; }
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
            foreach (var cv in canvases) if (cv.isRootCanvas) cv.enabled = false;

            var camGo = new GameObject("SeamsShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            ShiftersCamera.Instance.SuppressMainCamera(true, cam);

            var a = cm.Active;
            Vector3 fwd = a.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 basePos = a.transform.position;

            // kiosk with a Lost intern (printing)
            var kpos = basePos + fwd * 4f + right * 3f;
            var kiosk = LevelKit.ReprintKiosk(null, kpos, Quaternion.LookRotation(-fwd));
            kiosk.PrintSeconds = 30f;
            cm.Get(2).Lose("vis");
            yield return ctx.Wait(0.3f);
            camGo.transform.position = kpos - fwd * 5.5f + Vector3.up * 2.4f + right * 1.5f;
            camGo.transform.LookAt(kpos + Vector3.up * 1.2f);
            yield return null;
            ctx.Shot("vis_kiosk_idle", cam);
            kiosk.StartPrint(2);
            yield return ctx.Wait(1.5f);
            ctx.Shot("vis_kiosk_printing", cam);
            kiosk.PrintSeconds = 1.6f;
            yield return ctx.Wait(1.0f);
            ctx.Check(cm.Get(2).IsAlive, "vis: reprinted");
            ctx.Shot("vis_kiosk_printed", cam);

            // threat label behind a wall (Brainiac active)
            for (int i = 0; i < 3; i++) if (cm.Get(i) != null && cm.Get(i).IsAlive && (cm.Get(i).Perspectives & PerspectiveMask.Intellect) != 0) { cm.SwitchTo(i, true); break; }
            var tpos = basePos - right * 4f + fwd * 9f;
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.Destroy(target.GetComponent<Collider>());
            target.transform.position = tpos + Vector3.up;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = tpos - fwd * 3f + Vector3.up * 2f;
            wall.transform.rotation = Quaternion.LookRotation(fwd);
            wall.transform.localScale = new Vector3(6f, 4f, 0.4f);
            var marker = PerspectiveKit.ThreatLabel(target.transform, "THREAT: {m} M");
            camGo.transform.position = tpos - fwd * 10f + Vector3.up * 1.6f;
            camGo.transform.LookAt(tpos + Vector3.up * 2f);
            yield return ctx.Wait(0.8f);
            ctx.Check(marker.IsShown, "vis: threat label shown (" + marker.ShownText + ")");
            ctx.Shot("vis_threat_through_wall", cam);

            // exit with a weight gate, team inside
            var exit = LevelExit.All.Count > 0 ? LevelExit.All[0] : null;
            if (exit != null)
            {
                exit.Precondition = null;
                var gate = LevelKit.ExitWeightLimit(exit, 150f);
                PutTeamInExit(cm, exit);
                var et = exit.transform;
                camGo.transform.position = et.TransformPoint(new Vector3(-1.2f, 2.6f, -5.5f));
                camGo.transform.LookAt(et.TransformPoint(new Vector3(-1.8f, 2.2f, 1f)));
                yield return ctx.Wait(1.2f);
                ctx.Check(gate.IsOverweight && !exit.IsCompleting, "vis: weight gate holds the exit (" + gate.CurrentKg.ToString("0") + " kg)");
                ctx.Shot("vis_exit_weight_gate", cam);
            }
            ShiftersCamera.Instance.SuppressMainCamera(false);
            foreach (var cv in canvases) if (cv != null) cv.enabled = true;
            SessionPrefs.RestoreAll();
        }

        static void PutTeamInExit(CharacterManager cm, LevelExit exit)
        {
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Get(i);
                if (c == null || !c.IsAlive) continue;
                var local = exit.ZoneCenter + new Vector3((i - 1) * 1.2f, -exit.ZoneSize.y * 0.5f + 0.15f, 0f);
                c.Motor.Teleport(exit.transform.TransformPoint(local), exit.transform.rotation);
            }
        }
    }
}
