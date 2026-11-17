// =====================================================================================
//  SHIFTERS - MinWeightAlarm.cs   [PUZZLES - campaign wave, LEVELS2 CH07 THE SWAP]
//  Heist pedestal. While the Guarded prop rests on top it is calm. The moment it leaves (lifted / knocked off),
//  the pedestal weighs what is left on it for CheckSeconds: load >= MinKg (a teammate standing on it; the lifter
//  and the lifted prop do NOT count) = SWAPPED (disarmed until the prop returns); otherwise ALARM: this source is
//  ON for AlarmSeconds (wire laser rings / guards to it; use OffDelay to stretch), klaxon, the prop is
//  force-released and respawned on the pedestal (anti-softlock: the alarm always resets).
// =====================================================================================
using System;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class MinWeightAlarm : SignalSource, IResettable
    {
        public enum AlarmState { Resting, Checking, Swapped, Alarm, Away }

        public float MinKg = 50f;
        public Grabbable Guarded;
        public WeightSensor Sensor;
        [Tooltip("Pedestal top surface height above this transform.")] public float TopHeight = 0.6f;
        [Tooltip("Half size of the top (x, z).")] public Vector2 TopHalf = new Vector2(1f, 1f);
        public float CheckSeconds = 0.3f;
        public float AlarmSeconds = 4f;
        public TextMeshPro Readout;
        public PuzzleLamp Lamp;

        public AlarmState State { get; private set; } = AlarmState.Resting;
        public int AlarmCount { get; private set; }
        public int SwapCount { get; private set; }
        /// <summary>Load on the pedestal that counts (kg): excludes the prop's lifter and the prop itself.</summary>
        public float LoadKg { get; private set; }
        public bool PropOnPedestal { get; private set; }
        public event Action<MinWeightAlarm> Alarmed;
        public event Action<MinWeightAlarm> Swapped;

        float _timer, _minSeen;
        int _shownKg = int.MinValue;
        AlarmState _shownState = (AlarmState)(-1);

        bool PropResting()
        {
            if (Guarded == null || Guarded.IsHeld) return false;
            var l = transform.InverseTransformPoint(Guarded.transform.position);
            return Mathf.Abs(l.x) <= TopHalf.x + 0.1f && Mathf.Abs(l.z) <= TopHalf.y + 0.1f && l.y >= TopHeight - 0.3f && l.y <= TopHeight + 2.5f;
        }

        float MeasureLoad()
        {
            if (Sensor == null) return 0f;
            float kg = Sensor.Measure();
            if (Guarded != null)
            {
                if (PropResting()) kg -= Guarded.Mass;
                var holder = Guarded.Holder != null ? Guarded.Holder.Character : null;
                if (holder != null && Sensor.Contains(holder)) kg -= holder.TotalMass;
            }
            return Mathf.Max(0f, kg);
        }

        void FixedUpdate()
        {
            PropOnPedestal = PropResting();
            LoadKg = MeasureLoad();
            switch (State)
            {
                case AlarmState.Resting:
                    if (!PropOnPedestal) { State = AlarmState.Checking; _timer = CheckSeconds; _minSeen = LoadKg; }
                    break;
                case AlarmState.Checking:
                    if (PropOnPedestal) { State = AlarmState.Resting; break; }
                    _minSeen = Mathf.Min(_minSeen, LoadKg);
                    if (_minSeen < MinKg - 0.01f) { TriggerAlarm(); break; }
                    _timer -= Time.fixedDeltaTime;
                    if (_timer <= 0f)
                    {
                        State = AlarmState.Swapped;
                        SwapCount++;
                        Sfx.At(SfxId.Whisper, transform.position, 0.8f);
                        Swapped?.Invoke(this);
                    }
                    break;
                case AlarmState.Swapped:
                case AlarmState.Away:
                    if (PropOnPedestal) State = AlarmState.Resting;
                    break;
                case AlarmState.Alarm:
                    _timer -= Time.fixedDeltaTime;
                    if (_timer <= 0f) { SetOn(false); State = PropOnPedestal ? AlarmState.Resting : AlarmState.Away; }
                    break;
            }
        }

        /// <summary>Sound the alarm now (also scripts/tests).</summary>
        public void TriggerAlarm()
        {
            State = AlarmState.Alarm;
            _timer = AlarmSeconds;
            AlarmCount++;
            SetOn(true);
            Sfx.At(SfxId.Klaxon, transform.position + Vector3.up * 2f);
            Sfx.At(SfxId.Alarm, transform.position + Vector3.up * 2f);
            Fx.FloatingText(transform.position + Vector3.up * 2.4f, "ALARM!", UIColors.Danger, 1.6f);
            GameEvents.RaiseToast("ALARM! THE PEDESTAL FEELS LIGHTER. NEEDS " + Mathf.RoundToInt(MinKg) + " KG ON IT WHEN YOU LIFT.", UIColors.Danger);
            if (Guarded != null)
            {
                if (Guarded.Holder != null) Guarded.Holder.ForceRelease();
                var r = Guarded.GetComponent<Respawnable>();
                if (r != null) r.Respawn();
                else
                {
                    var p = transform.TransformPoint(new Vector3(0f, TopHeight + 0.6f, 0f));
                    Guarded.transform.position = p;
                    if (Guarded.Body != null) { Guarded.Body.position = p; if (!Guarded.Body.isKinematic) Guarded.Body.linearVelocity = Vector3.zero; }
                }
            }
            Alarmed?.Invoke(this);
        }

        void Update()
        {
            if (Lamp != null)
            {
                if (State == AlarmState.Alarm) Lamp.Blink(PuzzleLampColor.Red, PuzzleLampColor.Off, 8f);
                else if (State != _shownState) Lamp.Set(State == AlarmState.Swapped || State == AlarmState.Away ? PuzzleLampColor.Green : State == AlarmState.Checking ? PuzzleLampColor.Yellow : PuzzleLampColor.Red);
            }
            if (Readout == null) return;
            int kg = Mathf.RoundToInt(LoadKg);
            if (kg == _shownKg && State == _shownState) return;
            _shownKg = kg; _shownState = State;
            string head = State == AlarmState.Alarm ? "<color=#FF5A5A>ALARM!</color>" : State == AlarmState.Swapped || State == AlarmState.Away ? "<color=#3DDC97>DISARMED</color>" : "MIN " + Mathf.RoundToInt(MinKg) + " KG";
            Readout.text = head + "\n<size=70%>LOAD " + kg + " KG</size>";
        }

        public void ResetState()
        {
            State = AlarmState.Resting;
            _timer = 0f;
            SetOn(false);
        }
    }
}
