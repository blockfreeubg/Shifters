using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Level 2 freight elevator brain ("MAX 150 KG"). A SignalSource that powers a kit <see cref="Elevator"/> and an
    /// IInteractable control panel riding on the car. Pressing the panel weighs the car (WeightSensor on the car floor):
    /// within MaxKg it goes up; overloaded it sounds the alarm and flashes "OVERWEIGHT: PLEASE REMOVE ONE (1) INTERN".
    /// Once the car is up and has been empty for ReturnDelay seconds it returns down by itself, so the rest of the team
    /// can ride. Built by L2Builder (all parts assigned there; nothing is constructed at runtime).
    /// </summary>
    public class L2FreightElevator : SignalSource, IInteractable, IResettable
    {
        public float MaxKg = 150f;
        [Tooltip("Seconds the car must be empty at the top before it returns down.")] public float ReturnDelay = 1.5f;

        [Header("Parts (built by L2Builder)")]
        public Elevator Car;
        public WeightSensor Sensor;
        public TextMeshPro LoadText;
        public TextMeshPro WarningText;
        public PuzzleLamp Lamp;

        /// <summary>How many times the elevator refused to move because it was overloaded.</summary>
        public int Refusals { get; private set; }
        public float LastRefusedKg { get; private set; }
        public int Trips { get; private set; }
        public bool AtTop => Car != null && Car.Position01 >= 0.999f;
        public bool AtBottom => Car != null && Car.Position01 <= 0.001f;
        public float LoadKg => Sensor != null ? Sensor.CurrentMass : 0f;

        float _emptyTimer, _flash = -1f, _lastDenied = -10f;
        int _shownKg = int.MinValue;
        bool _shownOver;

        static readonly string[] RefuseQuips =
        {
            "We need to talk about lunch.", "Who had the second sandwich?", "It's the hard hat. Definitely the hard hat.",
            "I'm big boned!", "Elevator: 1, Team: 0."
        };

        // ------------------------------------------------------------------ IInteractable
        public int InteractPriority => 2;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.0f;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || !who.IsAlive) return false;
            if (Car != null && Car.Moving) { blockedReason = "The elevator is moving. Patience, intern."; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => IsOn ? "Send elevator down" : "Go up (MAX " + Mathf.RoundToInt(MaxKg) + " KG)";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) return;
            if (IsOn)
            {
                SetOn(false);
                Sfx.At(SfxId.ButtonPress, transform.position);
                return;
            }
            float kg = Sensor != null ? Sensor.Measure() : 0f;
            if (kg > MaxKg + 0.5f) { Refuse(kg, who); return; }
            if (kg < 1f)
            {
                GameEvents.RaiseToast("The elevator refuses to travel empty. It has standards.", UIColors.TextMuted);
                return;
            }
            Trips++;
            _emptyTimer = 0f;
            SetOn(true);
            Sfx.At(SfxId.ButtonPress, transform.position);
            Sfx.At(SfxId.ElevatorHum, transform.position, 0.9f);
            GameEvents.RaiseToast("GOING UP: " + Mathf.RoundToInt(kg) + " / " + Mathf.RoundToInt(MaxKg) + " KG. KEEP ARMS INSIDE THE INTERN.", UIColors.Success);
        }

        void Refuse(float kg, ShifterCharacter who)
        {
            Refusals++;
            LastRefusedKg = kg;
            _flash = 3f;
            Sfx.At(SfxId.Alarm, transform.position + Vector3.up * 2f);
            if (Time.time - _lastDenied > 1f) { _lastDenied = Time.time; Sfx.At(SfxId.Denied, transform.position); }
            // polish 11-17: the pop rose from the panel (back corner of the car) into the car frame and the LOAD sign; it
            // now pops low in the front half of the car (in front of the riders, under the LOAD / warning board for its whole
            // 1.25 m rise). The toast no longer quotes the weighed kg: it stayed up
            // ~3 s after riders stepped off and contradicted the live LOAD sign (160 vs 102); the sign is the live number.
            var car = Car != null && Car.Body != null ? Car.Body.transform : transform.parent;
            Vector3 pop = car != null ? car.position + car.forward * -1.2f + Vector3.up * 1.6f : transform.position + Vector3.up * 2.2f;
            Fx.FloatingText(pop, "OVERWEIGHT!", UIColors.Danger, 1.0f);
            GameEvents.RaiseToast("OVERWEIGHT! MAX " + Mathf.RoundToInt(MaxKg) + " KG. PLEASE REMOVE ONE (1) INTERN.", UIColors.Danger);
            if (who != null) who.Say(RefuseQuips[Refusals % RefuseQuips.Length]);
        }

        // ------------------------------------------------------------------ runtime
        void Start()
        {
            if (WarningText != null) WarningText.enabled = false;
            RefreshDisplay(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsOn && AtTop)
            {
                if (LoadKg < 1f) _emptyTimer += dt; else _emptyTimer = 0f;
                if (_emptyTimer >= ReturnDelay)
                {
                    _emptyTimer = 0f;
                    SetOn(false);
                    Sfx.At(SfxId.ButtonPress, transform.position, 0.6f, 0.8f);
                }
            }
            else _emptyTimer = 0f;

            if (_flash > 0f)
            {
                _flash -= dt;
                bool on = _flash > 0f && Mathf.Repeat(_flash, 0.4f) > 0.17f;
                if (WarningText != null) WarningText.enabled = on;
                if (Lamp != null) Lamp.Set(on ? PuzzleLampColor.Red : PuzzleLampColor.Yellow);
                if (_flash <= 0f && Lamp != null) Lamp.Set(IsOn ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            }
            else if (Lamp != null) Lamp.Set(IsOn ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            RefreshDisplay(false);
        }

        void RefreshDisplay(bool force)
        {
            if (LoadText == null) return;
            int kg = Mathf.RoundToInt(LoadKg);
            bool over = LoadKg > MaxKg + 0.5f;
            if (!force && kg == _shownKg && over == _shownOver) return;
            _shownKg = kg; _shownOver = over;
            LoadText.text = "LOAD " + kg + " / " + Mathf.RoundToInt(MaxKg) + " KG";
            LoadText.color = over ? UIColors.Danger : kg > 0 ? UIColors.Success : UIColors.TextMuted;
        }

        public void ResetState()
        {
            _emptyTimer = 0f;
            _flash = -1f;
            SetOn(false);
            if (WarningText != null) WarningText.enabled = false;
        }
    }
}
