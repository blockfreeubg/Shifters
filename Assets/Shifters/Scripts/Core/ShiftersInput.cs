// =====================================================================================
//  SHIFTERS - ShiftersInput.cs   [CONTRACT API - implementation owned by the CORE agent]
//  The ONLY place that touches the Input System. Project uses the NEW Input System only:
//  UnityEngine.Input.* throws at runtime - never use it anywhere.
//  Actions are created in code (keyboard+mouse and gamepad). Consumers poll these properties
//  from Update (not FixedUpdate: "Pressed" getters are true for exactly one rendered frame).
//  Gameplay properties return false/zero unless Context == Gameplay.
//
//  Keyboard map: WASD move | Mouse look | Space jump | LShift sprint | E interact | F grab/drop
//  Q throw | RShift (alt Tab) switcher | 1/2/3 select | LMB fire | RMB aim | B living bridge | X flop
//  G team follow/stay | R perspective scan | H hint | T taunt | Esc pause/cancel | Enter confirm
//  Backspace skip tutorial | Mouse wheel zoom
//
//  Gamepad map: LS move | RS look | A jump/confirm | B flop/cancel | X interact | Y grab/drop
//  RB throw | LB living bridge | LT aim | RT fire | L3 sprint (click = latch while moving) | R3 scan
//  D-pad down switcher | D-pad left/up/right = team mode / hint / taunt (Gameplay) or slot 1/2/3
//  (Switcher) | Start pause (and cancel in menus) | Back/View skip tutorial
//
//  Presses are swallowed for the rest of the frame in which Context changes, so e.g. the Esc
//  that opened the pause menu is never also read as the pause menu's CancelPressed.
//
//  Cursor lock (WebGL = browser pointer lock): Menu frees the cursor, every other context locks it.
//  WebGLInput.stickyCursorLock is switched OFF at boot so Cursor.lockState follows the browser: when the
//  browser drops the lock in Gameplay (Esc, alt-tab, focus loss) PausePressed is true once (the pause menu
//  opens), and a click on the game in Gameplay without the lock requests it again (that click does not fire).
//  Unity still reads Locked after a request the browser REFUSED, so the SHIFTERS WebGL page reports the real
//  state (NotifyBrowserPointerLock via GameSession.OnBrowserPointerLock); CursorLocked combines both.
//  UpdateCursorLock() does this once per frame (GameSession.Update + the first input poll of the frame).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shifters
{
    public static class ShiftersInput
    {
        /// <summary>Mouse degrees per pixel of delta (before Settings.MouseSensitivity).</summary>
        public const float MouseDegreesPerPixel = 0.1f;
        /// <summary>Right-stick look speed at full deflection (before Settings.MouseSensitivity).</summary>
        public const float StickDegreesPerSecond = 220f;

        static InputContext _context = InputContext.Gameplay;
        static int _contextChangedFrame = -1;
        static int _consumedFrame = -1;
        static bool _initFailed;
        static bool _usingGamepad;
        static bool _focusHooked;
        static int _sprintLatchFrame = -1;
        static bool _sprintLatch;
        static bool _hadCursorLock;     // [integrator] gameplay cursor lock was granted (browser pointer lock on WebGL)
        static int _lockLostFrame = -1;
        static int _cursorFrame = -1;   // frame UpdateCursorLock last ran
        static int _relockFrame = -1;   // frame a click in Gameplay re-requested the lock
        static bool _fireSuppressed;    // the click that re-captures the pointer never fires (until LMB is released)
        static CursorLockMode _lastSeenLock = CursorLockMode.None;
        static bool _lockSimulated;     // tests: SimulateCursorLock
        static CursorLockMode _simulatedLock = CursorLockMode.None;
        static int _browserLock = -1;   // WebGL page report (NotifyBrowserPointerLock): -1 unknown, 0 not held, 1 held
        static float _relockAskedAt = -1f;   // realtime of the last click-to-relock request (refusal bookkeeping)
        static int _relockRefusals;     // click-to-relock requests the browser refused in a row

        static InputActionMap _map;
        static InputAction _move, _lookMouse, _lookStick, _jump, _sprint, _sprintPad, _interact, _grab, _throw, _fire, _aim,
            _bridge, _flop, _aiMode, _scan, _hint, _taunt, _pause, _zoom, _switcher, _select1, _select2, _select3,
            _padSelect1, _padSelect2, _padSelect3, _confirm, _cancel, _skip, _navigate, _anyKey, _advance;

        /// <summary>Seconds Backspace / gamepad Back must be held for SkipHeld01 to reach 1 (story "hold to skip").</summary>
        public const float SkipHoldSeconds = 1f;
        static float _skipDownAt = -1f;
        static InputDevice[] _deviceFilter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Shutdown();
            _context = InputContext.Gameplay;
            _contextChangedFrame = -1;
            _consumedFrame = -1;
            _initFailed = false;
            _usingGamepad = false;
            _sprintLatchFrame = -1;
            _sprintLatch = false;
            _hadCursorLock = false;
            _lockLostFrame = -1;
            _cursorFrame = -1;
            _relockFrame = -1;
            _fireSuppressed = false;
            _lastSeenLock = CursorLockMode.None;
            _lockSimulated = false;
            _simulatedLock = CursorLockMode.None;
            _browserLock = -1;
            _relockAskedAt = -1f;
            _relockRefusals = 0;
            SimulatedLockRequests = 0;
            SimulatedUnlockRequests = 0;
            SimulatedGrantOnRequest = true;
            _skipDownAt = -1f;
            _deviceFilter = null;
        }

        /// <summary>Setting this also updates cursor lock (Gameplay/Switcher/Cutscene = locked + hidden, Menu = visible + free).
        /// Pressed-getters return false for the rest of the frame in which the context changed.</summary>
        public static InputContext Context
        {
            get => _context;
            set
            {
                if (_context != value)
                {
                    _context = value;
                    _contextChangedFrame = Time.frameCount;
                    _sprintLatch = false;
                    _hadCursorLock = false; // lock is re-requested for the new context: only a LATER loss counts
                    if (_skipDownAt >= 0f) _skipDownAt = Time.unscaledTime; // a hold carried over restarts (no instant skip)
                }
                ApplyCursor();
            }
        }

        /// <summary>True once the action map exists and is enabled.</summary>
        public static bool IsInitialized => _map != null;

        /// <summary>Create + enable the action map. Idempotent; safe in batchmode / with no devices (then everything is neutral).</summary>
        public static void Initialize()
        {
            ConfigurePlatformCursorLock();
            if (_map != null) { if (!_map.enabled) _map.Enable(); ApplyCursor(); return; }
            try
            {
                BuildMap();
                _map.Enable();
                _initFailed = false;
            }
            catch (Exception e)
            {
                _initFailed = true;
                Debug.LogError("[ShiftersInput] failed to build input actions: " + e.Message);
                DisposeMap();
                return;
            }
            if (!_focusHooked) { Application.focusChanged += OnFocusChanged; _focusHooked = true; }
            ApplyCursor();
        }

        /// <summary>Disable and dispose all actions (play-mode exit / statics reset). Initialize() rebuilds them.</summary>
        public static void Shutdown()
        {
            if (_focusHooked) { Application.focusChanged -= OnFocusChanged; _focusHooked = false; }
            DisposeMap();
        }

        /// <summary>Swallow every pressed-getter for the rest of this frame (UI consumed the input).</summary>
        public static void ConsumePresses() => _consumedFrame = Time.frameCount;

        // ---------------------------------------------------------------- gameplay (Context == Gameplay)
        public static Vector2 Move => GameplayLive ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero;

        /// <summary>Camera look delta in DEGREES for this frame (sensitivity + invert applied).
        /// x &gt; 0 = turn right, y &gt; 0 = look UP (so a camera with "pitch down = positive" does pitch -= Look.y).</summary>
        public static Vector2 Look
        {
            get
            {
                if (!GameplayLive) return Vector2.zero;
                var s = CurrentSettings;
                float sens = s != null ? Mathf.Clamp(s.MouseSensitivity, 0.05f, 10f) : 1f;
                Vector2 v = _lookMouse.ReadValue<Vector2>() * (MouseDegreesPerPixel * sens)
                          + _lookStick.ReadValue<Vector2>() * (StickDegreesPerSecond * TimeController.RealDeltaTime * sens);
                if (s != null && s.InvertY) v.y = -v.y;
                return v;
            }
        }

        public static bool JumpPressed => GameplayPress(_jump);
        public static bool JumpHeld => GameplayLive && _jump.IsPressed();
        public static bool SprintHeld
        {
            get
            {
                if (!GameplayLive) return false;
                UpdateSprintLatch();
                return _sprint.IsPressed() || _sprintLatch;
            }
        }
        public static bool InteractPressed => GameplayPress(_interact);
        public static bool GrabPressed => GameplayPress(_grab);
        public static bool ThrowPressed => GameplayPress(_throw);
        public static bool FirePressed => GameplayPress(_fire) && !FireSuppressed;
        public static bool FireHeld => GameplayLive && _fire.IsPressed() && !FireSuppressed;
        public static bool AimHeld => GameplayLive && _aim.IsPressed();
        public static bool BridgePressed => GameplayPress(_bridge);
        public static bool FlopPressed => GameplayPress(_flop);
        public static bool AIModePressed => GameplayPress(_aiMode);
        public static bool ScanPressed => GameplayPress(_scan);
        public static bool HintPressed => GameplayPress(_hint);
        public static bool TauntPressed => GameplayPress(_taunt);
        /// <summary>Esc / P / gamepad Start - or the gameplay cursor lock was lost (browsers release pointer lock on Esc
        /// and on focus loss, often without delivering the key to the game), so the pause menu opens either way.</summary>
        public static bool PausePressed => GameplayPress(_pause) || CursorLockLost();

        /// <summary>True (once) on the frame a previously granted gameplay cursor lock disappears (Gameplay context only).</summary>
        static bool CursorLockLost()
        {
            UpdateCursorLock();
            return _context == InputContext.Gameplay && _lockLostFrame == Time.frameCount;
        }

        // ---------------------------------------------------------------- cursor lock (WebGL pointer lock)
        /// <summary>True while the cursor lock is held (WebGL: the browser granted pointer lock; lock simulation: SimulatedCursorLock).</summary>
        public static bool CursorLocked => LockState == CursorLockMode.Locked;
        /// <summary>True if this frame's left click re-requested the gameplay cursor lock (that click does not fire).</summary>
        public static bool RelockClickThisFrame { get { UpdateCursorLock(); return _relockFrame == Time.frameCount; } }

        /// <summary>Per-frame cursor-lock upkeep; idempotent within a frame (GameSession.Update calls it, and so does the first
        /// lock-dependent input poll). In Gameplay: a granted lock that disappears => <see cref="PausePressed"/> once; on WebGL a
        /// left click without the lock requests it again (browsers only grant pointer lock inside a user gesture, and with
        /// WebGLInput.stickyCursorLock off Unity no longer re-locks on click by itself). Menu/Switcher/Cutscene never relock here.</summary>
        public static void UpdateCursorLock()
        {
            int f = Time.frameCount;
            if (_cursorFrame == f) return;
            _cursorFrame = f;
            if (_fireSuppressed && !LeftButtonHeld) _fireSuppressed = false;
            if (!LockLogicActive) return;
            var state = LockState;
            if (state != _lastSeenLock)
            {
                if (LogLockChanges) Debug.Log("[ShiftersInput] cursor lock " + _lastSeenLock + " -> " + state + " (context " + _context + ")");
                _lastSeenLock = state;
            }
            if (_context != InputContext.Gameplay) return;
            if (state == CursorLockMode.Locked) { _hadCursorLock = true; return; }
            if (_hadCursorLock && f != _contextChangedFrame)
            {
                _hadCursorLock = false;
                _lockLostFrame = f;      // PausePressed this frame -> the pause menu opens (once)
                if (LogLockChanges) Debug.Log("[ShiftersInput] gameplay cursor lock lost -> pause");
                return;
            }
            if (RelockOnClick && f != _contextChangedFrame && f != _consumedFrame && LeftClickPressed)
            {
                _relockFrame = f;
                _relockAskedAt = Time.realtimeSinceStartup;
                // this click only captures the pointer - unless the browser keeps refusing (then let the player shoot)
                _fireSuppressed = _relockRefusals < 2;
                if (LogLockChanges) Debug.Log("[ShiftersInput] click in gameplay without the cursor lock -> lock requested");
                SetCursorLock(true, fresh: true);
            }
        }

        /// <summary>WebGL page -> game (Assets/WebGLTemplates/Shifters calls unityInstance.SendMessage("[SHIFTERS]",
        /// "OnBrowserPointerLock", "1"|"0") on every pointerlockchange / pointerlockerror; GameSession forwards it): the
        /// browser's REAL pointer-lock state. Needed because Unity's Cursor.lockState reads Locked as soon as a lock is
        /// requested and stays Locked when the browser REFUSES the request (only a granted lock that is lost goes back to
        /// None): without this the game would show no CLICK TO PLAY and never ask again. Pages that never call it: -1.</summary>
        public static void NotifyBrowserPointerLock(bool held)
        {
            int was = _browserLock;
            _browserLock = held ? 1 : 0;
            if (held) { _relockRefusals = 0; _relockAskedAt = -1f; }
            else if (_relockAskedAt >= 0f && Time.realtimeSinceStartup - _relockAskedAt < 3f) { _relockRefusals++; _relockAskedAt = -1f; }
            if (LogLockChanges && was != _browserLock) Debug.Log("[ShiftersInput] browser pointer lock " + (held ? "granted" : "not held"));
        }

        /// <summary>Browser pointer-lock state reported by the WebGL page: -1 unknown (no report yet / not WebGL), 0 not held, 1 held.</summary>
        public static int BrowserPointerLock => _browserLock;

        /// <summary>TESTS ONLY (batchmode has no cursor / pointer lock). While true, ShiftersInput reads and writes
        /// <see cref="SimulatedCursorLock"/> instead of UnityEngine.Cursor.lockState and runs exactly the lock logic of a WebGL
        /// player (lock loss => pause, click => relock, Menu => unlock). The test plays Unity + the browser: SimulatedCursorLock
        /// is what Unity's Cursor.lockState would read, NotifyBrowserPointerLock (or GameSession.OnBrowserPointerLock) what the
        /// page reports. Lock requests are counted (SimulatedLockRequests / SimulatedUnlockRequests).</summary>
        public static bool SimulateCursorLock
        {
            get => _lockSimulated;
            set
            {
                if (_lockSimulated == value) return;
                _lockSimulated = value;
                _simulatedLock = CursorLockMode.None;
                _lastSeenLock = CursorLockMode.None;
                _hadCursorLock = false;
                _cursorFrame = -1;
                _browserLock = -1;
                _relockAskedAt = -1f;
                _relockRefusals = 0;
                SimulatedLockRequests = 0;
                SimulatedUnlockRequests = 0;
            }
        }
        /// <summary>TESTS: the simulated platform lock state (see <see cref="SimulateCursorLock"/>).</summary>
        public static CursorLockMode SimulatedCursorLock { get => _simulatedLock; set => _simulatedLock = value; }
        /// <summary>TESTS: true (default) = a simulated lock request reads back Locked at once (Unity's requested state);
        /// false = it stays as it is until the test grants it.</summary>
        public static bool SimulatedGrantOnRequest { get; set; } = true;
        public static int SimulatedLockRequests { get; private set; }
        public static int SimulatedUnlockRequests { get; private set; }

        /// <summary>Scroll steps this frame (+ = zoom in). One wheel notch = 1.</summary>
        public static float Zoom
        {
            get
            {
                if (!GameplayLive) return 0f;
                float y = _zoom.ReadValue<Vector2>().y;
                if (Mathf.Abs(y) > 3f) y /= 120f; // platform-specific range (Windows) -> notches
                return Mathf.Clamp(y, -3f, 3f);
            }
        }

        // ---------------------------------------------------------------- switcher / menus
        /// <summary>Right Shift / Tab / gamepad D-pad down (valid in Gameplay and Switcher contexts).</summary>
        public static bool SwitcherPressed =>
            (_context == InputContext.Gameplay || _context == InputContext.Switcher) && PressLive(_switcher);

        /// <summary>0..2 when 1/2/3 pressed this frame (Gameplay or Switcher context), else -1.
        /// Gamepad D-pad left/up/right select slots only inside the Switcher.</summary>
        public static int SelectPressed
        {
            get
            {
                if (!(_context == InputContext.Gameplay || _context == InputContext.Switcher) || !CanPress) return -1;
                if (_select1.WasPressedThisFrame()) return 0;
                if (_select2.WasPressedThisFrame()) return 1;
                if (_select3.WasPressedThisFrame()) return 2;
                if (_context == InputContext.Switcher)
                {
                    if (_padSelect1.WasPressedThisFrame()) return 0;
                    if (_padSelect2.WasPressedThisFrame()) return 1;
                    if (_padSelect3.WasPressedThisFrame()) return 2;
                }
                return -1;
            }
        }
        public static bool ConfirmPressed => MenuLike && PressLive(_confirm);      // Enter / gamepad South (Switcher, Menu)
        public static bool CancelPressed => MenuLike && PressLive(_cancel);        // Escape / gamepad East or Start (Switcher, Menu)
        public static bool SkipTutorialPressed => PressLive(_skip);
        /// <summary>Story: tap Enter / Space / left mouse / gamepad A = advance a line. Cutscene and Menu contexts only
        /// (in Gameplay Space is Jump and LMB is Fire). True for one rendered frame.</summary>
        public static bool AdvancePressed => (_context == InputContext.Cutscene || _context == InputContext.Menu) && PressLive(_advance);
        /// <summary>Story: 0..1 hold progress of Backspace / gamepad Back (1 after SkipHoldSeconds; unscaled time, so it
        /// works while paused). Readable in every context (Cutscene included); 0 while released; a hold carried across a context change restarts from 0.</summary>
        public static float SkipHeld01
        {
            get
            {
                if (!Live || _skipDownAt < 0f || !_skip.IsPressed()) return 0f;
                return Mathf.Clamp01((Time.unscaledTime - _skipDownAt) / SkipHoldSeconds);
            }
        }
        /// <summary>Co-op seam: only these devices drive the action map (null = all devices). Applied to the live map
        /// immediately (InputActionMap.devices) and on rebuild; LeftClickPressed honours it too.</summary>
        public static InputDevice[] DeviceFilter
        {
            get => _deviceFilter;
            set
            {
                _deviceFilter = value != null && value.Length > 0 ? value : null;
                ApplyDeviceFilter();
            }
        }

        static void ApplyDeviceFilter()
        {
            if (_map == null) return;
            try
            {
                if (_deviceFilter == null) _map.devices = null;
                else _map.devices = new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(_deviceFilter);
            }
            catch (Exception e) { Debug.LogWarning("[ShiftersInput] DeviceFilter: " + e.Message); }
        }

        static bool DeviceAllowed(InputDevice d)
        {
            if (_deviceFilter == null) return true;
            if (d == null) return false;
            for (int i = 0; i < _deviceFilter.Length; i++) if (_deviceFilter[i] == d) return true;
            return false;
        }                // Backspace / gamepad Back (any context)
        /// <summary>Arrow keys / either gamepad stick (Switcher and Menu contexts), magnitude &lt;= 1.</summary>
        public static Vector2 NavigateStick => MenuLike && Live ? Vector2.ClampMagnitude(_navigate.ReadValue<Vector2>(), 1f) : Vector2.zero;
        /// <summary>Any keyboard key, mouse button or main gamepad button (face, shoulders, Start/Back, D-pad) pressed this frame (any context).</summary>
        public static bool AnyKeyPressed => PressLive(_anyKey);

        // ---------------------------------------------------------------- raw (any context)
        public static bool LeftClickPressed { get { var m = Mouse.current; return m != null && DeviceAllowed(m) && m.leftButton.wasPressedThisFrame; } }
        public static Vector2 PointerPosition { get { var p = Pointer.current; return p != null ? p.position.ReadValue() : Vector2.zero; } }
        public static Vector2 PointerDelta { get { var m = Mouse.current; return m != null ? m.delta.ReadValue() : Vector2.zero; } }
        /// <summary>True when the last meaningful input came from a gamepad (prompts should show pad labels).</summary>
        public static bool UsingGamepad => _usingGamepad;

        /// <summary>Human readable key for prompts: KeyLabel("Interact") => "E" (or gamepad text such as "X" when UsingGamepad).
        /// Unknown names are returned unchanged.</summary>
        public static string KeyLabel(string action) => _usingGamepad ? KeyLabels.Gamepad(action) : KeyLabels.Keyboard(action);
        /// <summary>Keyboard/mouse label regardless of the last used device.</summary>
        public static string KeyLabelKeyboard(string action) => KeyLabels.Keyboard(action);
        /// <summary>Gamepad label regardless of the last used device.</summary>
        public static string KeyLabelGamepad(string action) => KeyLabels.Gamepad(action);

        // ================================================================ internals
        static bool Live
        {
            get
            {
                if (_map != null) return true;
                if (_initFailed || !Application.isPlaying) return false;
                Initialize();
                return _map != null;
            }
        }

        static bool CanPress { get { int f = Time.frameCount; return f != _contextChangedFrame && f != _consumedFrame && Live; } }
        static bool GameplayLive => _context == InputContext.Gameplay && Live;
        static bool MenuLike => _context == InputContext.Switcher || _context == InputContext.Menu;
        static bool GameplayPress(InputAction a) => _context == InputContext.Gameplay && PressLive(a);
        static bool PressLive(InputAction a) => CanPress && a.WasPressedThisFrame();

        static SettingsData CurrentSettings { get { var gs = GameSession.Instance; return gs != null ? gs.Settings : null; } }

        static void UpdateSprintLatch()
        {
            int f = Time.frameCount;
            if (_sprintLatchFrame == f) return;
            _sprintLatchFrame = f;
            if (_sprintPad.WasPressedThisFrame()) _sprintLatch = !_sprintLatch;
            if (_move.ReadValue<Vector2>().sqrMagnitude < 0.04f) _sprintLatch = false;
        }

        // ---------------------------------------------------------------- cursor lock internals
        /// <summary>Real cursor (play mode, not batchmode) or the test simulation.</summary>
        static bool LockLogicActive => _lockSimulated || (Application.isPlaying && !Application.isBatchMode);
        /// <summary>What Unity's Cursor.lockState reads (requested state: Locked while a request is pending or was refused).</summary>
        static CursorLockMode PlatformLockState => _lockSimulated ? _simulatedLock : Cursor.lockState;
        /// <summary>The lock really held: Unity's state, corrected by the page's report when the browser does not hold it.</summary>
        static CursorLockMode LockState
        {
            get
            {
                var s = PlatformLockState;
                return s == CursorLockMode.Locked && _browserLock == 0 ? CursorLockMode.None : s;
            }
        }
        static bool IsWebPlayer => Application.platform == RuntimePlatform.WebGLPlayer;
        /// <summary>Click-to-relock runs where the platform can lose the lock behind the game's back: WebGL (and tests).</summary>
        static bool RelockOnClick => _lockSimulated || IsWebPlayer;
        static bool LogLockChanges => IsWebPlayer;   // one console line per change: the browser log explains lock issues
        static bool LeftButtonHeld { get { var m = Mouse.current; return m != null && m.leftButton.isPressed; } }
        static bool FireSuppressed { get { UpdateCursorLock(); return _fireSuppressed; } }

        static void ConfigurePlatformCursorLock()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Unity's default "sticky" lock keeps Cursor.lockState == Locked after the browser released pointer lock (Esc,
            // alt-tab, focus loss), so the game never noticed: no pause, no "CLICK TO PLAY". Non-sticky, lockState follows
            // the browser and UpdateCursorLock pauses on loss / relocks on the next click in gameplay.
            if (WebGLInput.stickyCursorLock)
            {
                WebGLInput.stickyCursorLock = false;
                Debug.Log("[ShiftersInput] WebGL: stickyCursorLock off - Cursor.lockState follows the browser pointer lock");
            }
#endif
        }

        /// <summary>Lock (Gameplay/Switcher/Cutscene) or free (Menu) the cursor. Only calls the platform when the state has
        /// to change (force: always, e.g. focus regained), so menus, the pause screen and the switcher never re-request a
        /// lock that is already held.</summary>
        static void ApplyCursor(bool force = false)
        {
            if (!LockLogicActive) return;
            bool locked = _context != InputContext.Menu;
            // Unity's requested state decides: a pending (or refused) request is left alone here - a click in gameplay
            // re-requests a lock the browser does not hold (UpdateCursorLock)
            if (!force && locked == (PlatformLockState == CursorLockMode.Locked))
            {
                if (!_lockSimulated && Cursor.visible == locked) Cursor.visible = !locked;
                return;
            }
            SetCursorLock(locked);
        }

        /// <summary>fresh: issue a NEW browser request even if Unity already reads Locked (a refused request leaves it Locked
        /// and Unity ignores Locked -> Locked).</summary>
        static void SetCursorLock(bool locked, bool fresh = false)
        {
            if (_lockSimulated)
            {
                if (locked) { SimulatedLockRequests++; if (SimulatedGrantOnRequest) _simulatedLock = CursorLockMode.Locked; }
                else { SimulatedUnlockRequests++; _simulatedLock = CursorLockMode.None; }
                return;
            }
            if (locked && fresh && Cursor.lockState == CursorLockMode.Locked) Cursor.lockState = CursorLockMode.None;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        static void OnFocusChanged(bool focused) { if (focused) ApplyCursor(true); }   // desktop / Editor: re-lock after alt-tab

        static void OnActionTriggered(InputAction.CallbackContext ctx)
        {
            if (ctx.action == _skip)
            {
                if (ctx.phase == InputActionPhase.Performed && _skipDownAt < 0f) _skipDownAt = Time.unscaledTime;
                else if (ctx.phase == InputActionPhase.Canceled) _skipDownAt = -1f;
            }
            var control = ctx.control;
            if (control == null) return;
            var device = control.device;
            if (device is Gamepad) { _usingGamepad = true; return; }
            if (device is Keyboard) { _usingGamepad = false; return; }
            if (device is Mouse)
            {
                // ignore tiny mouse jitter so a bumped desk doesn't flip prompts away from the pad
                if (ctx.action == _lookMouse && ctx.ReadValue<Vector2>().sqrMagnitude < 9f) return;
                _usingGamepad = false;
            }
        }

        static void DisposeMap()
        {
            if (_map == null) return;
            try
            {
                _map.actionTriggered -= OnActionTriggered;
                _map.Disable();
                _map.Dispose();
            }
            catch (Exception) { /* already torn down by the Input System on play-mode exit */ }
            _map = null;
            _move = _lookMouse = _lookStick = _jump = _sprint = _sprintPad = _interact = _grab = _throw = _fire = _aim = null;
            _bridge = _flop = _aiMode = _scan = _hint = _taunt = _pause = _zoom = _switcher = _select1 = _select2 = _select3 = null;
            _padSelect1 = _padSelect2 = _padSelect3 = _confirm = _cancel = _skip = _navigate = _anyKey = _advance = null;
            _skipDownAt = -1f;
        }

        static void BuildMap()
        {
            var map = new InputActionMap("Shifters");

            _move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _lookMouse = map.AddAction("LookMouse", InputActionType.PassThrough, "<Mouse>/delta", expectedControlLayout: "Vector2");
            _lookStick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");
            _zoom = map.AddAction("Zoom", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");

            _jump = Button(map, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            _sprint = Button(map, "Sprint", "<Keyboard>/leftShift");
            _sprintPad = Button(map, "SprintPad", "<Gamepad>/leftStickPress");
            _interact = Button(map, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            _grab = Button(map, "Grab", "<Keyboard>/f", "<Gamepad>/buttonNorth");
            _throw = Button(map, "Throw", "<Keyboard>/q", "<Gamepad>/rightShoulder");
            _fire = Button(map, "Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _aim = Button(map, "Aim", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            _bridge = Button(map, "Bridge", "<Keyboard>/b", "<Gamepad>/leftShoulder");
            _flop = Button(map, "Flop", "<Keyboard>/x", "<Gamepad>/buttonEast");
            _aiMode = Button(map, "AIMode", "<Keyboard>/g", "<Gamepad>/dpad/left");
            _scan = Button(map, "Scan", "<Keyboard>/r", "<Gamepad>/rightStickPress");
            _hint = Button(map, "Hint", "<Keyboard>/h", "<Gamepad>/dpad/up");
            _taunt = Button(map, "Taunt", "<Keyboard>/t", "<Gamepad>/dpad/right");
            _pause = Button(map, "Pause", "<Keyboard>/escape", "<Keyboard>/p", "<Gamepad>/start"); // P: browsers eat Esc

            _switcher = Button(map, "Switcher", "<Keyboard>/rightShift", "<Keyboard>/tab", "<Gamepad>/dpad/down");
            _select1 = Button(map, "Select1", "<Keyboard>/1", "<Keyboard>/numpad1");
            _select2 = Button(map, "Select2", "<Keyboard>/2", "<Keyboard>/numpad2");
            _select3 = Button(map, "Select3", "<Keyboard>/3", "<Keyboard>/numpad3");
            _padSelect1 = Button(map, "PadSelect1", "<Gamepad>/dpad/left");
            _padSelect2 = Button(map, "PadSelect2", "<Gamepad>/dpad/up");
            _padSelect3 = Button(map, "PadSelect3", "<Gamepad>/dpad/right");
            _confirm = Button(map, "Confirm", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            _cancel = Button(map, "Cancel", "<Keyboard>/escape", "<Keyboard>/p", "<Gamepad>/buttonEast", "<Gamepad>/start");
            _skip = Button(map, "SkipTutorial", "<Keyboard>/backspace", "<Gamepad>/select");

            _navigate = map.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            _navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _navigate.AddBinding("<Gamepad>/leftStick");
            _navigate.AddBinding("<Gamepad>/rightStick");

            _anyKey = Button(map, "AnyKey", "<Keyboard>/anyKey", "<Mouse>/leftButton", "<Mouse>/rightButton",
                "<Gamepad>/buttonSouth", "<Gamepad>/buttonEast", "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth",
                "<Gamepad>/start", "<Gamepad>/select", "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder",
                "<Gamepad>/dpad/up", "<Gamepad>/dpad/down", "<Gamepad>/dpad/left", "<Gamepad>/dpad/right");

            _advance = Button(map, "Advance", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Mouse>/leftButton", "<Gamepad>/buttonSouth");

            map.actionTriggered += OnActionTriggered;
            _map = map;
            ApplyDeviceFilter();
        }

        static InputAction Button(InputActionMap map, string name, params string[] paths)
        {
            var a = map.AddAction(name, InputActionType.Button);
            for (int i = 0; i < paths.Length; i++) a.AddBinding(paths[i]);
            return a;
        }
    }
}
