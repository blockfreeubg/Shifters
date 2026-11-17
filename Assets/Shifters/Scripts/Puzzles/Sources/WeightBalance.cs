using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Two weight pads. ON while both carry at least MinKg and |A - B| &lt;= ToleranceKg. A central scale beam tilts
    /// toward the heavier side and a readout shows both loads.
    /// </summary>
    public class WeightBalance : SignalSource, IResettable
    {
        public WeightSensor SensorA;
        public WeightSensor SensorB;
        public float ToleranceKg = 10f;
        [Tooltip("Each side needs at least this much (an empty scale is not 'balanced').")] public float MinKg = 40f;
        public float MaxTilt = 18f;
        public float ReleaseDelay = 0.25f;

        [Header("Parts")]
        public Transform Beam;
        public PuzzleLamp Lamp;
        public TextMeshPro Readout;
        public Revealable Reveal;

        public float MassA => SensorA != null ? SensorA.CurrentMass : 0f;
        public float MassB => SensorB != null ? SensorB.CurrentMass : 0f;
        public bool Balanced { get; private set; }

        float _tilt, _off;
        int _shownA = -1, _shownB = -1;

        void Start() { if (Reveal != null) Reveal.DynamicLabel = Label; }

        public bool Evaluate(float a, float b) => a >= MinKg && b >= MinKg && Mathf.Abs(a - b) <= ToleranceKg;

        void FixedUpdate()
        {
            bool bal = Evaluate(MassA, MassB);
            if (bal)
            {
                _off = 0f;
                if (!IsOn) { Sfx.At(SfxId.PlatePress, transform.position, 1f, 1.2f); Fx.Puff(transform.position + Vector3.up * 2f, 0.6f); SetOn(true); }
            }
            else if (IsOn)
            {
                _off += Time.fixedDeltaTime;
                if (_off >= ReleaseDelay) { Sfx.At(SfxId.PlateRelease, transform.position); SetOn(false); }
            }
            Balanced = IsOn;
        }

        void Update()
        {
            float a = MassA, b = MassB;
            float target = Mathf.Clamp((b - a) * 0.2f, -MaxTilt, MaxTilt);
            _tilt = Mathf.Lerp(_tilt, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (Beam != null) Beam.localRotation = Quaternion.Euler(0f, 0f, -_tilt);
            if (Lamp != null) Lamp.Set(IsOn ? PuzzleLampColor.Green : (a + b > 1f ? PuzzleLampColor.Yellow : PuzzleLampColor.Red));
            int ra = Mathf.RoundToInt(a), rb = Mathf.RoundToInt(b);
            if (Readout != null && (ra != _shownA || rb != _shownB))
            {
                _shownA = ra; _shownB = rb;
                Readout.text = ra + "  |  " + rb + " KG";
                Readout.color = IsOn ? UIColors.Success : UIColors.Text;
            }
        }

        string Label(ShifterCharacter viewer)
            => "BALANCE ±" + Mathf.RoundToInt(ToleranceKg) + " KG\n" + Mathf.RoundToInt(MassA) + " vs " + Mathf.RoundToInt(MassB) + " KG";

        public void ResetState() { _off = 0f; SetOn(false); }
    }
}
