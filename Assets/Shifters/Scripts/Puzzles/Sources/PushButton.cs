using UnityEngine;

namespace Shifters
{
    /// <summary>Big red pedestal button (E). Momentary: ON for Duration seconds after each press (re-press refreshes).</summary>
    public class PushButton : SignalSource, IInteractable, IResettable
    {
        public float Duration = 3f;

        [Header("Parts")]
        public Transform Cap;
        public PuzzleLamp Lamp;
        public Revealable Reveal;

        /// <summary>Seconds left while on.</summary>
        public float Remaining { get; protected set; }
        /// <summary>Remaining / window (1 right after the press).</summary>
        public float Remaining01 => CurrentWindow > 0f ? Mathf.Clamp01(Remaining / CurrentWindow) : 0f;
        public float CurrentWindow { get; protected set; }

        Vector3 _capHome;
        bool _capInit;
        float _capPress;

        public virtual int InteractPriority => 1;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.05f;

        public virtual bool CanInteract(ShifterCharacter who, out string blockedReason) { blockedReason = null; return who != null; }
        public virtual string GetPrompt(ShifterCharacter who) => IsOn ? "Press again" : "Press button";

        public virtual void Interact(ShifterCharacter who) => Press(who);

        /// <summary>Press the button (scripts/tests too).</summary>
        public virtual void Press(ShifterCharacter who = null)
        {
            CurrentWindow = WindowSeconds();
            Remaining = CurrentWindow;
            _capPress = 1f;
            Sfx.At(SfxId.ButtonPress, transform.position);
            PuzzleUtil.Gesture(who, VisualAction.Interact);
            SetOn(true);
            OnPressed(who);
        }

        protected virtual float WindowSeconds() => Duration;
        protected virtual void OnPressed(ShifterCharacter who) { }
        protected virtual void OnRunning(float remaining) { }
        protected virtual void OnExpired() { Sfx.At(SfxId.ButtonPress, transform.position, 0.7f, 0.75f); }

        protected virtual void Update()
        {
            if (!_capInit && Cap != null) { _capHome = Cap.localPosition; _capInit = true; }
            if (IsOn)
            {
                Remaining -= Time.deltaTime;
                if (Remaining <= 0f) { Remaining = 0f; SetOn(false); OnExpired(); }
                else OnRunning(Remaining);
            }
            _capPress = Mathf.MoveTowards(_capPress, IsOn ? 0.5f : 0f, Time.deltaTime * 4f);
            if (Cap != null && _capInit) Cap.localPosition = _capHome + Vector3.down * (0.06f * _capPress);
            if (Lamp != null) Lamp.Set(IsOn);
        }

        public virtual void ResetState() { Remaining = 0f; SetOn(false); }
    }
}
