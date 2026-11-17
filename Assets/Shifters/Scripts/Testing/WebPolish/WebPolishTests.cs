// =====================================================================================
//  SHIFTERS - WebPolishTests.cs   [FIX_WEBPOLISH - DevPlay proof of the WebGL polish fixes]
//  devplay.sh <proj> webpolish -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity
//      -devSetup Shifters.WebPolishTests.RunAll -devSeconds 1
//  1. Cursor lock: batchmode has no cursor / pointer lock, so ShiftersInput.SimulateCursorLock makes ShiftersInput run
//     exactly the lock logic of a WebGL player (WebGLInput.stickyCursorLock off => Cursor.lockState follows the browser):
//     lock lost in gameplay => pause once; resume => lock requested; ungranted lock => no pause loop; click in gameplay
//     => relock (that click does not fire); pause menu / switcher never issue lock or unlock requests.
//  2. Pause menu: after Tab / P / P with a mouse pointer parked over RESTART CHECKPOINT, RESUME stays preselected
//     (a hover from a pointer that never moved is undone); a real key press / mouse move hands control back.
//  3. Fullscreen policy (GameSession.PlanFullscreen): WebGL never requests fullscreen at boot, only on an explicit
//     change of the option; desktop applies at boot and on change, and does not re-force it on unrelated changes.
//  PlayerPrefs (shifters_* keys) are backed up and restored.
// =====================================================================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Shifters
{
    public static class WebPolishTests
    {
        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            var prefs = E2EPrefsBackup.Capture();
            var settings = InputSystem.settings;
            var oldBg = settings.backgroundBehavior;
            var oldEditor = settings.editorInputBehaviorInPlayMode;
            Keyboard kb = null;
            Mouse mouse = null;
            GameObject probeGo = null;
            int pauseEvents = 0;
            Action<bool> onPause = _ => pauseEvents++;
            GameEvents.PauseChanged += onPause;
            try
            {
                FullscreenPolicyChecks(ctx);

                // ------------------------------------------------ the level (real L1 scene, HUD, Gameplay)
                yield return Until(() => LevelManager.Instance != null && LevelManager.Instance.IsStarted && CharacterManager.Instance != null
                                         && CharacterManager.Instance.Team.Count == 3, 25f);
                yield return Until(() => ShiftersInput.Context == InputContext.Gameplay && UIManager.Instance != null && UIManager.Instance.HudVisible, 8f);
                var ui = UIManager.Instance;
                ctx.Check(ui != null && ui.HudVisible && ShiftersInput.Context == InputContext.Gameplay, "level running: HUD visible, Gameplay context");
                if (ui == null) yield break;
                yield return ctx.Wait(1f);

                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                kb = InputSystem.AddDevice<Keyboard>("WebPolishKeyboard");
                mouse = InputSystem.AddDevice<Mouse>("WebPolishMouse");
                probeGo = new GameObject("[WebPolishSelectionProbe]");
                var probe = probeGo.AddComponent<WebPolishSelectionProbe>();
                var center = new Vector2(Screen.width * 0.62f, Screen.height * 0.5f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                yield return Frames(2);

                // ------------------------------------------------ 1. cursor lock (simulated browser pointer lock)
                ShiftersInput.SimulateCursorLock = true;
                ShiftersInput.SimulatedGrantOnRequest = true;
                ShiftersInput.Context = InputContext.Gameplay;   // re-applies the cursor: Gameplay wants the lock
                ctx.Check(ShiftersInput.SimulatedLockRequests == 1 && ShiftersInput.CursorLocked, "lock: Gameplay requests the cursor lock (granted)");
                yield return Frames(3);
                ctx.Check(!ui.IsPaused, "lock: lock held => no pause");
                int requests = ShiftersInput.SimulatedLockRequests;

                pauseEvents = 0;
                ShiftersInput.SimulatedCursorLock = CursorLockMode.None;   // the browser dropped it (Esc / alt-tab / focus loss)
                yield return Frames(2);
                ctx.Check(ui.IsPaused && ui.Top == ui.Pause && ShiftersInput.Context == InputContext.Menu, "lock lost in gameplay => pause menu open, Menu context");
                yield return ctx.Wait(0.6f);
                var resume = ui.Pause.Find("RESUME");
                var restart = ui.Pause.Find("RESTART CHECKPOINT");
                ctx.Check(resume != null && restart != null, "pause buttons RESUME / RESTART CHECKPOINT exist");
                if (resume == null || restart == null) yield break;
                ctx.Check(pauseEvents == 1 && ui.IsPaused, "lock lost => exactly ONE PauseChanged, menu stays open (" + pauseEvents + ")");
                ctx.Check(probe.Last == resume.gameObject, "pause opened by the lock loss preselects RESUME (" + Name(probe.Last) + ")");
                ctx.Check(ShiftersInput.SimulatedLockRequests == requests && ShiftersInput.SimulatedUnlockRequests == 0,
                          "pause menu does not fight the lock (lock requests " + ShiftersInput.SimulatedLockRequests + ", unlock " + ShiftersInput.SimulatedUnlockRequests + ")");
                ctx.Shot("wp1_pause_after_lock_loss");
                var restartScreen = ScreenCenter(restart.Rt);   // final position (the slide-in is over)
                var homes = new Vector2[ui.Pause.Buttons.Count];
                for (int i = 0; i < homes.Length; i++) homes[i] = ui.Pause.Buttons[i].Rt.anchoredPosition;

                yield return Click(mouse, new Vector2(Screen.width * 0.62f, Screen.height * 0.9f));
                ctx.Check(ShiftersInput.SimulatedLockRequests == requests && ui.IsPaused, "click inside the pause menu => no lock request");

                ui.ClosePause();
                yield return Frames(2);
                ctx.Check(!ui.IsPaused && ShiftersInput.Context == InputContext.Gameplay, "RESUME => Gameplay");
                ctx.Check(ShiftersInput.SimulatedLockRequests == requests + 1 && ShiftersInput.CursorLocked, "resume requests the lock again (" + ShiftersInput.SimulatedLockRequests + ")");
                yield return ctx.Wait(0.4f);
                ctx.Check(!ui.IsPaused && pauseEvents == 2, "no second pause once the lock is back (" + pauseEvents + " PauseChanged)");

                // a lock request the browser does not grant (no user gesture / refused right after Esc)
                ShiftersInput.SimulatedGrantOnRequest = false;
                ShiftersInput.SimulatedCursorLock = CursorLockMode.None;
                yield return Frames(2);
                ctx.Check(ui.IsPaused && pauseEvents == 3, "second lock loss => pause again (" + pauseEvents + ")");
                ui.ClosePause();
                yield return ctx.Wait(0.5f);
                ctx.Check(!ui.IsPaused && !ShiftersInput.CursorLocked && pauseEvents == 4, "resume without a granted lock: gameplay continues unlocked, no pause loop (" + pauseEvents + ")");

                int beforeClick = ShiftersInput.SimulatedLockRequests;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }.WithButton(MouseButton.Left));
                yield return null;
                bool clicked = ShiftersInput.LeftClickPressed, relock = ShiftersInput.RelockClickThisFrame;
                bool fired = ShiftersInput.FirePressed, fireHeld = ShiftersInput.FireHeld;
                ctx.Check(clicked && relock && ShiftersInput.SimulatedLockRequests == beforeClick + 1, "click in gameplay without the lock => lock requested (" + ShiftersInput.SimulatedLockRequests + ")");
                ctx.Check(!fired && !fireHeld, "the click that re-captures the pointer does not fire");
                ShiftersInput.SimulatedCursorLock = CursorLockMode.Locked;   // the browser grants it
                yield return null;
                ctx.Check(!ShiftersInput.FireHeld && !ui.IsPaused, "fire stays suppressed while that click is held");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                yield return Frames(2);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }.WithButton(MouseButton.Left));
                yield return null;
                bool fireLocked = ShiftersInput.FirePressed, relockLocked = ShiftersInput.RelockClickThisFrame;
                ctx.Check(fireLocked && !relockLocked && ShiftersInput.SimulatedLockRequests == beforeClick + 1, "with the lock held a click fires normally, no extra lock request");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                yield return Frames(2);
                ShiftersInput.SimulatedGrantOnRequest = true;

                // ------------------------------------------------ 1b. the page's report (a real WebGL build showed it: Unity's
                // Cursor.lockState reads Locked right after a request and STAYS Locked when the browser refuses it)
                var root = GameSession.Instance != null ? GameSession.Instance.gameObject : null;
                ctx.Check(root != null && root.name == "[SHIFTERS]", "page report target: GameSession on the [SHIFTERS] root (what the template's SendMessage addresses)");
                if (root == null) yield break;
                root.SendMessage("OnBrowserPointerLock", "1");   // exactly what the template sends on pointerlockchange
                yield return Frames(2);
                ctx.Check(ShiftersInput.BrowserPointerLock == 1 && ShiftersInput.CursorLocked && !ui.IsPaused, "page: lock held => CursorLocked, no pause");
                pauseEvents = 0;
                root.SendMessage("OnBrowserPointerLock", "0");   // the browser dropped it ...
                ShiftersInput.SimulatedCursorLock = CursorLockMode.None;   // ... and non-sticky Unity reads None
                yield return Frames(2);
                ctx.Check(ui.IsPaused && pauseEvents == 1, "page: lock dropped => pause once (" + pauseEvents + ")");
                ui.ClosePause();                                 // resume: Unity reads Locked at once (requested) ...
                yield return null;
                root.SendMessage("OnBrowserPointerLock", "0");   // ... but the browser REFUSES (pointerlockerror)
                yield return Frames(3);
                ctx.Check(ShiftersInput.SimulatedCursorLock == CursorLockMode.Locked && !ShiftersInput.CursorLocked && !ui.IsPaused,
                          "refused request: Unity reads Locked, page says not held => CursorLocked false (CLICK TO PLAY), no pause loop");
                int r0 = ShiftersInput.SimulatedLockRequests;
                for (int k = 0; k < 3; k++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = center }.WithButton(MouseButton.Left));
                    yield return null;
                    bool rl = ShiftersInput.RelockClickThisFrame, fp = ShiftersInput.FirePressed;
                    ctx.Check(rl && ShiftersInput.SimulatedLockRequests == r0 + k + 1 && fp == (k == 2),
                              "refused lock, click " + (k + 1) + ": fresh lock request" + (k == 2 ? ", and it fires (refused twice in a row: stop eating shots)" : ", shot not fired")
                              + " (requests " + (ShiftersInput.SimulatedLockRequests - r0) + ", fired " + fp + ")");
                    root.SendMessage("OnBrowserPointerLock", "0");   // refused again
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                    yield return Frames(2);
                }
                root.SendMessage("OnBrowserPointerLock", "1");   // granted at last
                yield return Frames(2);
                ctx.Check(ShiftersInput.CursorLocked && !ui.IsPaused && ShiftersInput.Context == InputContext.Gameplay, "page: lock granted at last => locked, still in gameplay");

                // ------------------------------------------------ 2. Tab / P / P with the pointer parked over RESTART CHECKPOINT
                int req0 = ShiftersInput.SimulatedLockRequests, un0 = ShiftersInput.SimulatedUnlockRequests;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = restartScreen });   // e.g. where PLAY was clicked (226,280)
                yield return Frames(2);
                var sw = CharacterSwitcher.Instance;
                yield return Tap(kb, Key.Tab);
                ctx.Check(sw != null && sw.IsOpen && ShiftersInput.Context == InputContext.Switcher, "Tab opens the switcher wheel");
                ctx.Check(ShiftersInput.SimulatedLockRequests == req0 && ShiftersInput.SimulatedUnlockRequests == un0 && ShiftersInput.CursorLocked,
                          "switcher keeps the lock: no lock / unlock requests");
                yield return ctx.Wait(0.3f);
                yield return Tap(kb, Key.P);
                yield return Frames(2);
                ctx.Check(sw != null && !sw.IsOpen && ShiftersInput.Context == InputContext.Gameplay && !ui.IsPaused, "P closes the wheel (Cancel), no pause yet");
                ctx.Check(ShiftersInput.SimulatedLockRequests == req0 && ShiftersInput.SimulatedUnlockRequests == un0, "closing the wheel: no lock churn");
                yield return ctx.Wait(0.3f);
                probe.Begin(resume.gameObject, ui.Pause);   // every frame's final selection while the pause menu is on top
                int reverts0 = ui.Pause.ResumeGuardReverts;
                yield return Tap(kb, Key.P);
                ctx.Check(ui.IsPaused && ui.Top == ui.Pause, "P opens the pause menu");
                yield return Frames(50);   // ~0.8 s: covers the slide-in of the buttons under the parked pointer
                probe.End();
                ctx.Check(probe.Frames >= 45 && probe.Mismatches == 0, "Tab/P/P: RESUME selected at the end of every frame of the opening (" + probe.Frames
                          + " frames, " + probe.Mismatches + " others" + (probe.Mismatches > 0 ? ", first " + probe.FirstMismatch : "") + ")");
                ctx.Check(ui.Pause.ResumeGuardActive, "RESUME guard still active (the player did nothing)");
                float maxOffset = 0f;
                for (int i = 0; i < homes.Length && i < ui.Pause.Buttons.Count; i++)
                    maxOffset = Mathf.Max(maxOffset, (ui.Pause.Buttons[i].Rt.anchoredPosition - homes[i]).magnitude);
                ctx.Check(maxOffset < 1f, "pause buttons back at their home positions although an earlier pause was closed mid-slide (max offset "
                          + maxOffset.ToString("0.0") + ")");
                ctx.Log("opening under the parked pointer: the guard undid " + (ui.Pause.ResumeGuardReverts - reverts0)
                        + " hover selection(s) made by the EventSystem (0 = the batchmode UI module produced no hover)");
                ctx.Shot("wp2_pause_after_tab_p_p");

                // the hover a stationary pointer produces (what the EventSystem does when a button appears under it)
                var es = EventSystem.current;
                ExecuteEvents.Execute(restart.gameObject, new PointerEventData(es) { position = restartScreen }, ExecuteEvents.pointerEnterHandler);
                bool hoverSelected = Selected() == restart.gameObject;
                yield return null;   // LateUpdate of that frame ran (guard), the probe recorded the frame's final selection
                ctx.Check(hoverSelected && probe.Last == resume.gameObject && Selected() == resume.gameObject,
                          "hover by a pointer that never moved is undone: RESUME selected again (hover had selected " + (hoverSelected ? "RESTART CHECKPOINT" : "nothing") + ", frame ended on " + Name(probe.Last) + ")");

                // a NEW key press hands control back: Down arrow moves to RESTART CHECKPOINT and stays there
                yield return Tap(kb, Key.DownArrow);
                bool moduleNavigated = Selected() == restart.gameObject;
                if (Selected() == resume.gameObject)   // batchmode UI module did not navigate: deliver the move event it would send
                    ExecuteEvents.Execute(resume.gameObject, new AxisEventData(es) { moveDir = MoveDirection.Down, moveVector = Vector2.down }, ExecuteEvents.moveHandler);
                yield return Frames(4);
                ctx.Check(!ui.Pause.ResumeGuardActive && probe.Last == restart.gameObject, "a new key press ends the guard: Down selects RESTART CHECKPOINT and it stays ("
                          + Name(probe.Last) + (moduleNavigated ? ", navigated by the UI module" : ", move event delivered by the test") + ")");

                // reopen with the parked pointer, then really move the mouse: hover selection is the player's again
                ui.ClosePause();
                yield return Frames(3);
                ui.OpenPause();
                yield return ctx.Wait(0.6f);
                ctx.Check(probe.Last == resume.gameObject && ui.Pause.ResumeGuardActive, "reopened pause: RESUME preselected again (" + Name(probe.Last) + ")");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = restartScreen + new Vector2(40f, 3f) });
                yield return Frames(2);
                ExecuteEvents.Execute(restart.gameObject, new PointerEventData(es) { position = restartScreen + new Vector2(40f, 3f) }, ExecuteEvents.pointerEnterHandler);
                yield return Frames(3);
                ctx.Check(!ui.Pause.ResumeGuardActive && probe.Last == restart.gameObject, "a real mouse move ends the guard: hovering RESTART CHECKPOINT selects it (" + Name(probe.Last) + ")");
                ui.ClosePause();
                yield return Frames(3);
                ctx.Check(!ui.IsPaused && ShiftersInput.Context == InputContext.Gameplay && ShiftersInput.CursorLocked, "back in gameplay with the lock");
            }
            finally
            {
                GameEvents.PauseChanged -= onPause;
                ShiftersInput.SimulateCursorLock = false;
                if (UIManager.Instance != null && UIManager.Instance.IsPaused) UIManager.Instance.ClosePause();
                if (kb != null) InputSystem.RemoveDevice(kb);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (probeGo != null) UnityEngine.Object.Destroy(probeGo);
                settings.backgroundBehavior = oldBg;
                settings.editorInputBehaviorInPlayMode = oldEditor;
                prefs.Restore();
            }
            ctx.Log("web polish tests finished");
        }

        // ================================================================ fullscreen policy
        static void FullscreenPolicyChecks(DevPlayContext ctx)
        {
            // WebGL
            var p = GameSession.PlanFullscreen(true, -1, true, false);
            ctx.Check(!p.Request && !p.Setting, "fullscreen/web boot: saved Fullscreen=ON is NOT requested (no jump on the first click); setting mirrors the page (OFF)");
            p = GameSession.PlanFullscreen(true, -1, false, true);
            ctx.Check(!p.Request && p.Setting, "fullscreen/web boot inside an already fullscreen page: no request, setting ON");
            p = GameSession.PlanFullscreen(true, 0, false, false);
            ctx.Check(!p.Request && !p.Setting, "fullscreen/web: unrelated settings change (volume) requests nothing");
            p = GameSession.PlanFullscreen(true, 0, true, false);
            ctx.Check(p.Request && p.Fullscreen && p.Setting, "fullscreen/web: player switches the option ON => fullscreen requested");
            p = GameSession.PlanFullscreen(true, 1, false, true);
            ctx.Check(p.Request && !p.Fullscreen && !p.Setting, "fullscreen/web: player switches the option OFF => exit requested");
            p = GameSession.PlanFullscreen(true, 1, true, false);
            ctx.Check(!p.Request && p.Setting, "fullscreen/web: a pending / refused request is not repeated by unrelated changes");
            // desktop
            p = GameSession.PlanFullscreen(false, -1, true, false);
            ctx.Check(p.Request && p.Fullscreen && p.Setting, "fullscreen/desktop boot: saved ON => fullscreen window");
            p = GameSession.PlanFullscreen(false, -1, false, true);
            ctx.Check(p.Request && !p.Fullscreen, "fullscreen/desktop boot: saved OFF => windowed");
            p = GameSession.PlanFullscreen(false, -1, true, true);
            ctx.Check(!p.Request, "fullscreen/desktop boot: already in the saved mode => no change");
            p = GameSession.PlanFullscreen(false, 1, true, false);
            ctx.Check(!p.Request && p.Setting, "fullscreen/desktop: unrelated change does not re-force fullscreen the OS left");
            p = GameSession.PlanFullscreen(false, 1, false, true);
            ctx.Check(p.Request && !p.Fullscreen && !p.Setting, "fullscreen/desktop: player switches OFF => windowed");
            var gs = GameSession.Instance;
            if (gs != null)
            {
                bool before = gs.Settings.Fullscreen;
                gs.ApplySettings();   // Editor: the window is never touched, the value is kept
                ctx.Check(gs.Settings.Fullscreen == before, "fullscreen/editor: ApplySettings keeps the option and never resizes the Editor");
            }
        }

        // ================================================================ helpers
        static GameObject Selected() { var es = EventSystem.current; return es != null ? es.currentSelectedGameObject : null; }
        static string Name(GameObject go) => go != null ? go.name : "nothing";

        static Vector2 ScreenCenter(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        }

        static IEnumerator Tap(Keyboard kb, Key key)
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null;   // the press frame: consumers run in Update
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        static IEnumerator Click(Mouse mouse, Vector2 pos)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
            yield return null;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static IEnumerator Until(Func<bool> cond, float seconds)
        {
            int frames = Mathf.CeilToInt(seconds * 60f);
            for (int i = 0; i < frames && !cond(); i++) yield return null;
        }
    }

    /// <summary>Test helper: records the EventSystem selection at the very end of every frame (after all default-order
    /// LateUpdates, e.g. PauseScreen's RESUME guard). Batchmode never resumes WaitForEndOfFrame, so tests read this instead.</summary>
    [DefaultExecutionOrder(32000)]
    public class WebPolishSelectionProbe : MonoBehaviour
    {
        public GameObject Last;
        public int Frames, Mismatches;
        public string FirstMismatch = "";
        GameObject _watch;
        UIScreen _screen;

        public void Begin(GameObject expected, UIScreen whileOnTop) { _watch = expected; _screen = whileOnTop; Frames = 0; Mismatches = 0; FirstMismatch = ""; }
        public void End() { _watch = null; _screen = null; }

        void LateUpdate()
        {
            var es = EventSystem.current;
            Last = es != null ? es.currentSelectedGameObject : null;
            if (_watch == null) return;
            var ui = UIManager.Instance;
            if (_screen != null && (ui == null || ui.Top != _screen)) return;
            Frames++;
            if (Last != _watch) { Mismatches++; if (FirstMismatch == "") FirstMismatch = Last != null ? Last.name : "nothing"; }
        }
    }
}
