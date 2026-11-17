// =====================================================================================
//  SHIFTERS - CharacterSwitcher.cs   [CONTRACT API - logic owned by CORE, visuals = SwitcherWheelUI (UI agent)]
//  RIGHT SHIFT opens the GTA-inspired wheel (time slows to Tuning.SwitcherTimeScale).
//  1/2/3 or mouse direction highlight, Enter/LMB confirm, Esc or RIGHT SHIFT close.
//  With the wheel closed, 1/2/3 switch directly. Raises GameEvents.SwitcherOpenChanged /
//  SwitcherSelectionChanged; the UI only renders what this class decides.
//  Mouse: PointerDelta accumulates into a virtual stick (clamped radius, dead zone); its angle
//  (0 = up, clockwise) picks the nearest SlotAngles entry. Everything runs on unscaled time.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class CharacterSwitcher : MonoBehaviour
    {
        public static CharacterSwitcher Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public bool IsOpen { get; protected set; }
        /// <summary>Highlighted slot while open (-1 none).</summary>
        public int Highlighted { get; protected set; } = -1;
        /// <summary>Wheel slot angles in degrees (0 = up, clockwise) used by mouse selection and the UI layout.</summary>
        public static readonly float[] SlotAngles = { 0f, 120f, 240f };

        /// <summary>0..1 eased open amount (drives PostFx switcher blend and the music muffle). UI may read it.</summary>
        public float Blend { get; protected set; }
        /// <summary>Virtual stick from mouse motion (pixels, clamped to StickRadius) - UI can draw a pointer with it.</summary>
        public Vector2 VirtualStick => _stick;
        public const float StickRadius = 140f;
        const float StickDeadZone = 28f;
        const float PadDeadZone = 0.5f;

        Vector2 _stick;
        float _appliedBlend = -1f;

        protected virtual void Awake() { Instance = this; }
        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) RestoreWorld();
        }

        protected virtual void OnDisable() { if (IsOpen) Close(false); }

        /// <summary>True when the wheel may open now (not paused, no menu, team present, no transition running).</summary>
        public bool CanOpen
        {
            get
            {
                var mgr = CharacterManager.Instance;
                if (mgr == null || mgr.Active == null || mgr.Team.Count == 0) return false;
                if (TimeController.IsPaused) return false;
                if (UIManager.Instance != null && UIManager.Instance.AnyMenuOpen) return false;
                if (ShiftersInput.Context != InputContext.Gameplay) return false;
                return true;
            }
        }

        public virtual void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _stick = Vector2.zero;
            TimeController.SetSlowMo(true);
            ShiftersInput.Context = InputContext.Switcher;
            if (PortraitStudio.Instance != null) PortraitStudio.Instance.SetLive(true);
            Sfx.Play(SfxId.SwitcherOpen);
            var mgr = CharacterManager.Instance;
            Highlighted = -2; // force the event below
            GameEvents.RaiseSwitcherOpenChanged(true);
            Highlight(mgr != null ? mgr.ActiveIndex : 0, silent: true);
        }

        public virtual void Close(bool confirm)
        {
            if (!IsOpen) return;
            var mgr = CharacterManager.Instance;
            int slot = Highlighted;
            if (confirm && mgr != null && slot != mgr.ActiveIndex && !mgr.CanSwitchTo(slot))
            {
                Sfx.Play(SfxId.Denied); // unavailable: error buzz, wheel stays open
                return;
            }
            IsOpen = false;
            RestoreWorld();
            Sfx.Play(SfxId.SwitcherClose);
            GameEvents.RaiseSwitcherOpenChanged(false);
            if (confirm && mgr != null && slot != mgr.ActiveIndex) mgr.SwitchTo(slot);
        }

        void RestoreWorld()
        {
            TimeController.SetSlowMo(false);
            if (ShiftersInput.Context == InputContext.Switcher) ShiftersInput.Context = InputContext.Gameplay;
            if (PortraitStudio.Instance != null) PortraitStudio.Instance.SetLive(false);
        }

        public virtual void Highlight(int slot) => Highlight(slot, false);

        void Highlight(int slot, bool silent)
        {
            if (slot == Highlighted) return;
            Highlighted = slot;
            if (!silent && slot >= 0) Sfx.Play(SfxId.SwitcherTick);
            GameEvents.RaiseSwitcherSelectionChanged(slot);
        }

        void Update()
        {
            var mgr = CharacterManager.Instance;
            if (!IsOpen)
            {
                if (ShiftersInput.SwitcherPressed && CanOpen) Open();
                else if (mgr != null && ShiftersInput.Context == InputContext.Gameplay && !TimeController.IsPaused)
                {
                    int s = ShiftersInput.SelectPressed;
                    if (s >= 0 && s != mgr.ActiveIndex) mgr.SwitchTo(s);
                }
            }
            else UpdateOpen(mgr);
            UpdateBlend();
        }

        void UpdateOpen(CharacterManager mgr)
        {
            if (mgr == null || TimeController.IsPaused || (UIManager.Instance != null && UIManager.Instance.AnyMenuOpen)) { Close(false); return; }

            // virtual stick from the mouse
            _stick += ShiftersInput.PointerDelta;
            if (_stick.sqrMagnitude > StickRadius * StickRadius) _stick = _stick.normalized * StickRadius;
            if (_stick.sqrMagnitude > StickDeadZone * StickDeadZone) Highlight(SlotForDirection(_stick));
            var pad = ShiftersInput.NavigateStick;
            if (pad.sqrMagnitude > PadDeadZone * PadDeadZone) Highlight(SlotForDirection(pad));

            int sel = ShiftersInput.SelectPressed;
            if (sel >= 0)
            {
                if (sel == Highlighted) { Close(true); return; }
                Highlight(sel);
                _stick = Vector2.zero;
            }
            if (ShiftersInput.ConfirmPressed || ShiftersInput.LeftClickPressed) { Close(true); return; }
            if (ShiftersInput.CancelPressed || ShiftersInput.SwitcherPressed) Close(false);
        }

        /// <summary>Nearest slot for a screen direction (x right, y up).</summary>
        public static int SlotForDirection(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; // 0 = up, clockwise
            if (angle < 0f) angle += 360f;
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < SlotAngles.Length; i++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(angle, SlotAngles[i]));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void UpdateBlend()
        {
            float target = IsOpen ? 1f : 0f;
            Blend = Mathf.MoveTowards(Blend, target, ShiftersCamera.UnscaledDeltaTime / Mathf.Max(0.01f, Tuning.SwitcherRampTime));
            if (Mathf.Approximately(Blend, _appliedBlend)) return;
            _appliedBlend = Blend;
            float e = Blend * Blend * (3f - 2f * Blend);
            PostFx.SetSwitcherBlend(e);
            if (AudioManager.Instance != null) AudioManager.Instance.SetSlowMoAmount(e);
        }
    }
}
