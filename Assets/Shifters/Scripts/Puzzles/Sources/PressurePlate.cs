using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// 2x2 m weight plate. ON while the WeightSensor measures >= RequiredKg (stacked weight counts).
    /// The teal pad sinks PressDepth when on; a floating display shows the requirement and the live load;
    /// STR perspective reveals requirement + current load. Optional Latching (stays on forever once pressed).
    /// </summary>
    public class PressurePlate : SignalSource, IResettable
    {
        public float RequiredKg = Tuning.PlateHeavy;
        public bool Latching;
        [Tooltip("Seconds the weight must be missing before releasing (filters physics jitter).")] public float ReleaseDelay = 0.2f;
        public float PressDepth = 0.06f;

        [Header("Parts (built by PuzzleKit)")]
        public WeightSensor Sensor;
        public Transform Pad;
        public PuzzleLamp Lamp;
        public TextMeshPro RequirementText;
        public TextMeshPro LoadText;
        public Revealable Reveal;

        public float CurrentKg => Sensor != null ? Sensor.CurrentMass : 0f;
        /// <summary>Weight condition satisfied right now (IsOn may stay true when latched).</summary>
        public bool Satisfied { get; private set; }

        Vector3 _padHome;
        bool _homeSet;
        float _below, _depth;
        int _shownKg = int.MinValue;
        int _shownMode = -1;

        protected virtual void Start()
        {
            CaptureHome();
            if (Reveal != null) Reveal.DynamicLabel = RevealLabel;
            if (RequirementText != null) RequirementText.text = RequirementString();
            RefreshDisplay(true);
        }

        void CaptureHome()
        {
            if (_homeSet || Pad == null) return;
            _padHome = Pad.localPosition;
            _homeSet = true;
        }

        /// <summary>Weight test (override for ranges).</summary>
        protected virtual bool Evaluate(float kg) => kg >= RequiredKg - 0.5f;
        public virtual string RequirementString() => "≥" + Mathf.RoundToInt(RequiredKg) + " KG";

        protected virtual void FixedUpdate()
        {
            if (Sensor == null) return;
            bool sat = Evaluate(Sensor.CurrentMass);
            if (sat)
            {
                _below = 0f;
                if (!Satisfied) { Satisfied = true; OnPressed(); }
            }
            else if (Satisfied)
            {
                _below += Time.fixedDeltaTime;
                if (_below >= ReleaseDelay) { Satisfied = false; OnReleased(); }
            }
        }

        void OnPressed()
        {
            if (!IsOn) Sfx.At(SfxId.PlatePress, transform.position);
            Fx.Puff(transform.position + Vector3.up * 0.1f, 0.5f);
            SetOn(true);
        }

        void OnReleased()
        {
            if (Latching) return;
            Sfx.At(SfxId.PlateRelease, transform.position);
            SetOn(false);
        }

        protected virtual void Update()
        {
            CaptureHome();
            float target = IsOn ? PressDepth : 0f;
            if (Pad != null && !Mathf.Approximately(_depth, target))
            {
                _depth = Mathf.MoveTowards(_depth, target, Time.deltaTime * 0.6f);
                Pad.localPosition = _padHome + Vector3.down * _depth;
            }
            RefreshDisplay(false);
        }

        /// <summary>0 = under, 1 = satisfied, 2 = too heavy (range plates).</summary>
        protected virtual int LoadMode(float kg) => Evaluate(kg) ? 1 : 0;

        void RefreshDisplay(bool force)
        {
            float kg = CurrentKg;
            int rounded = Mathf.RoundToInt(kg);
            int mode = LoadMode(kg);
            if (Lamp != null)
            {
                if (mode == 2) Lamp.Blink(PuzzleLampColor.Yellow, PuzzleLampColor.Red, 5f);
                else Lamp.Set(IsOn ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            }
            if (!force && rounded == _shownKg && mode == _shownMode) return;
            _shownKg = rounded;
            _shownMode = mode;
            if (LoadText != null)
            {
                LoadText.text = mode == 2 ? "TOO HEAVY: " + rounded + " KG" : rounded + " KG";
                LoadText.color = mode == 1 ? UIColors.Success : mode == 2 ? UIColors.Accent : (rounded > 0 ? UIColors.Danger : UIColors.TextMuted);
            }
            OnLoadModeShown(mode);
        }

        protected virtual void OnLoadModeShown(int mode) { }

        string RevealLabel(ShifterCharacter viewer)
        {
            float kg = CurrentKg;
            string req = RequirementString();
            return "PLATE " + req + "\nON IT: " + PuzzleUtil.Kg(kg) + (Evaluate(kg) ? "  OK" : "");
        }

        public virtual void ResetState()
        {
            Satisfied = false;
            _below = 0f;
            SetOn(false);
        }
    }
}
