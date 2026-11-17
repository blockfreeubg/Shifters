// =====================================================================================
//  SHIFTERS - L5PodWeightLimit.cs   [LEVEL 5]
//  Escape-pod precondition for a LevelExit: a WeightSensor on the cab floor. While the floor
//  carries more than MaxKg the exit is disabled (it cannot complete), the pod sounds an alarm and
//  nags "OVERWEIGHT: PLEASE REMOVE ONE (1) INTERN". Characters that are inside the exit zone but
//  NOT on the floor sensor (the roof hatch) still count as escaped - that is the intended loophole.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class L5PodWeightLimit : MonoBehaviour
    {
        public LevelExit Exit;
        public WeightSensor Sensor;
        public float MaxKg = 200f;
        [Tooltip("Readout on the pod (\"138 / 200 KG\").")] public TextMeshPro Readout;
        [Tooltip("Lamps that flash red while overweight.")] public PuzzleLamp[] AlarmLamps = new PuzzleLamp[0];

        public bool IsOverweight { get; private set; }
        public float CurrentKg => Sensor != null ? Sensor.CurrentMass : 0f;
        public int OverweightCount { get; private set; }

        float _nag, _beep;
        int _shownKg = -1;
        bool _shownOver;

        static readonly string[] Nags =
        {
            "OVERWEIGHT: PLEASE REMOVE ONE (1) INTERN",
            "POD CAPACITY 200 KG. SOMEONE HAS TO GO.",
            "HAVE YOU CONSIDERED THE ROOF?",
            "THIS POD DOES NOT DO TEAM BUILDING"
        };

        void Update()
        {
            float kg = CurrentKg;
            bool over = kg > MaxKg + 0.5f;
            if (Exit != null && !Exit.IsCompleting && !Exit.IsCompleted && Exit.enabled == over) Exit.enabled = !over;
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
                    GameEvents.RaiseToast(Nags[(OverweightCount - 1 + Mathf.FloorToInt(Time.time / 4f)) % Nags.Length], UIColors.Danger);
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
