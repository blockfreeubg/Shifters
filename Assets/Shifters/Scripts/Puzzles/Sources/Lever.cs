using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Floor lever (E). Toggle (default) or OneShot. RequiredStrength &gt; 0 makes it a rusty lever: blocked prompt
    /// "Requires STR 7 - Kevin has STR 3" for weaklings, rusty creak + strain for the Brute.
    /// </summary>
    public class Lever : SignalSource, IInteractable, IResettable
    {
        public int RequiredStrength = 0;
        public bool OneShot;
        public bool StartsOn;
        public float OnAngle = 38f;
        public float OffAngle = -38f;

        [Header("Parts")]
        public Transform Handle;
        public PuzzleLamp Lamp;
        public Revealable Reveal;

        public bool IsRusty => RequiredStrength > 0;
        public bool Used { get; private set; }

        float _angle;
        bool _angleInit;
        float _lastDenied;

        void Start()
        {
            if (StartsOn) SetOn(true);
            if (Reveal != null && IsRusty) Reveal.DynamicLabel = v => v != null && v.Stats != null && v.Stats.Strength >= RequiredStrength
                ? "RUSTY LEVER - YOU CAN PULL IT" : "RUSTY LEVER - NEEDS STR " + RequiredStrength;
        }

        // ------------------------------------------------------------------ IInteractable
        public int InteractPriority => 1;
        public Vector3 InteractPoint => transform.position + Vector3.up * 0.9f;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || (OneShot && Used)) return false;
            if (IsRusty && (who.Stats == null || who.Stats.Strength < RequiredStrength))
            {
                blockedReason = PuzzleUtil.StrBlockedReason(RequiredStrength, who);
                return false;
            }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => IsRusty ? "Yank rusty lever" : (IsOn ? "Push lever back" : "Pull lever");

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out var reason))
            {
                if (reason != null && PuzzleUtil.RateLimit(ref _lastDenied, 1.5f))
                {
                    Sfx.At(SfxId.Denied, transform.position);
                    PuzzleUtil.Gesture(who, VisualAction.Strain);
                    if (who != null) who.Say("It won't budge!");
                }
                return;
            }
            Used = true;
            Flip(!IsOn, who);
        }

        /// <summary>Set the lever state directly (scripts/tests), with sound.</summary>
        public void Flip(bool on, ShifterCharacter who = null)
        {
            if (IsOn == on) return;
            SetOn(on);
            Sfx.At(SfxId.Lever, transform.position, 1f, IsRusty ? 0.7f : 1f);
            if (IsRusty)
            {
                Sfx.At(SfxId.BridgeCreak, transform.position, 0.8f, 0.8f);
                Fx.Puff(transform.position + Vector3.up * 0.6f, 0.6f, MaterialLibrary.DefaultColor(MatKey.MetalRust));
                PuzzleUtil.Gesture(who, VisualAction.Strain);
                PuzzleUtil.Express(who, Expression.Strain, 0.8f);
            }
            else PuzzleUtil.Gesture(who, VisualAction.Interact);
        }

        void Update()
        {
            float target = IsOn ? OnAngle : OffAngle;
            if (!_angleInit) { _angle = target; _angleInit = true; }
            _angle = Mathf.MoveTowards(_angle, target, Time.deltaTime * (IsRusty ? 160f : 420f));
            if (Handle != null) Handle.localRotation = Quaternion.Euler(_angle, 0f, 0f);
            if (Lamp != null) Lamp.Set(IsOn);
        }

        public void ResetState() { Used = false; SetOn(StartsOn); }
    }
}
