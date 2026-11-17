// =====================================================================================
//  SHIFTERS - ExitWeightGate.cs   [perspective_level kit, promoted by SEAMS from L5PodWeightLimit]
//  Weight precondition for a LevelExit: a WeightSensor on the cab floor. While the floor carries more
//  than MaxKg, LevelExit.Precondition refuses ("OVERWEIGHT: ...") so the exit cannot complete, the cab
//  sounds an alarm, lamps blink red and a readout shows "138 / 200 KG". Characters inside the exit zone
//  but NOT on the floor sensor (a roof seat) still count as escaped. Build with LevelKit.ExitWeightLimit.
// =====================================================================================
using System;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class ExitWeightGate : MonoBehaviour
    {
        public LevelExit Exit;
        public WeightSensor Sensor;
        public float MaxKg = 200f;
        [Tooltip("Readout (\"138 / 200 KG\").")] public TextMeshPro Readout;
        [Tooltip("Lamps that flash red while overweight.")] public PuzzleLamp[] AlarmLamps = new PuzzleLamp[0];

        public bool IsOverweight { get; private set; }
        public float CurrentKg => Sensor != null ? Sensor.CurrentMass : 0f;
        public int OverweightCount { get; private set; }

        float _nag, _beep;
        int _shownKg = -1;
        bool _shownOver, _hooked;
        Func<string> _previous;

        static readonly string[] Nags =
        {
            "OVERWEIGHT: PLEASE REMOVE ONE (1) INTERN",
            "CAPACITY {0} KG. SOMEONE HAS TO GO.",
            "THIS ELEVATOR DOES NOT DO TEAM BUILDING",
            "WEIGHT LIMIT EXCEEDED. HAVE YOU TRIED BEING LIGHTER?",
        };

        void OnEnable() => Hook();
        void Start() => Hook();

        void Hook()
        {
            if (_hooked || Exit == null) return;
            _hooked = true;
            _previous = Exit.Precondition;
            Exit.Precondition = Check;
        }

        void OnDisable()
        {
            if (!_hooked || Exit == null) return;
            if (Exit.Precondition == (Func<string>)Check) Exit.Precondition = _previous;
            _hooked = false;
        }

        string Check()
        {
            if (_previous != null) { var r = _previous(); if (!string.IsNullOrEmpty(r)) return r; }
            if (CurrentKg > MaxKg + 0.5f) return NagText(Mathf.Max(0, OverweightCount - 1));
            return null;
        }

        string NagText(int i)
        {
            var s = Nags[i % Nags.Length];
            return s.IndexOf('{') >= 0 ? string.Format(s, Mathf.RoundToInt(MaxKg)) : s;
        }

        void Update()
        {
            float kg = CurrentKg;
            bool over = kg > MaxKg + 0.5f;
            if (over && !IsOverweight)
            {
                OverweightCount++;
                _nag = 0f;
                Sfx.At(SfxId.Alarm, transform.position + Vector3.up * 2f, 0.9f);
            }
            IsOverweight = over;

            if (over)
            {
                _nag -= Time.deltaTime;
                if (_nag <= 0f)
                {
                    _nag = 4f;
                    // the exit's own refusal toast covers the "everyone inside" case: no double toast
                    bool exitJustRefused = Exit != null && Time.unscaledTime - Exit.LastRefusalTime < 1.5f;
                    if (!exitJustRefused) GameEvents.RaiseToast(NagText(OverweightCount - 1 + Mathf.FloorToInt(Time.time / 4f)), UIColors.Danger);
                    Fx.FloatingText(transform.position + Vector3.up * 3.2f + transform.forward * 2f, "OVERWEIGHT!", UIColors.Danger, 1.3f);
                }
                _beep -= Time.deltaTime;
                if (_beep <= 0f) { _beep = 1.1f; Sfx.At(SfxId.EnemyBeep, transform.position + Vector3.up * 2f, 0.5f, 1.7f); }
                foreach (var l in AlarmLamps) if (l != null) l.Blink(PuzzleLampColor.Red, PuzzleLampColor.Off, 3f);
            }
            else
            {
                foreach (var l in AlarmLamps) if (l != null && l.Current != PuzzleLampColor.Green) l.Set(PuzzleLampColor.Green);
            }

            int k = Mathf.RoundToInt(kg);
            if (Readout != null && (k != _shownKg || over != _shownOver))
            {
                _shownKg = k; _shownOver = over;
                Readout.text = (over ? "<color=#FF4D5E>" : "") + k + " / " + Mathf.RoundToInt(MaxKg) + " KG" + (over ? "</color>\n<size=60%>OVERWEIGHT</size>" : "\n<size=60%>MAX LOAD</size>");
            }
        }
    }
}
