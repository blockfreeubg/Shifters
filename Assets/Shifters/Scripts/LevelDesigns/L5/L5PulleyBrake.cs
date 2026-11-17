// =====================================================================================
//  SHIFTERS - L5PulleyBrake.cs   [LEVEL 5]
//  Holds a PulleyLift still (component disabled) until its inputs power it ("DISPATCH" lever),
//  so both sides can be loaded first. While braked it keeps the kg readouts alive and shows
//  the verdict ("A RISES" / "A SINKS" / "TOO EVEN") on a status display.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class L5PulleyBrake : SignalReceiver
    {
        public PulleyLift Lift;
        public TextMeshPro Status;

        public bool Released => Powered;

        int _a = -1, _b = -1;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (Lift != null) Lift.enabled = powered;
            if (!initial && powered)
            {
                Sfx.At(SfxId.Lever, transform.position, 0.9f, 0.8f);
                GameEvents.RaiseToast("BRAKE RELEASED. HOLD ON TO SOMETHING.", UIColors.AccentYellow);
            }
            Refresh(true);
        }

        protected override void Update()
        {
            base.Update();
            Refresh(false);
        }

        void Refresh(bool force)
        {
            if (Lift == null) return;
            int a = Mathf.RoundToInt(Lift.MassA), b = Mathf.RoundToInt(Lift.MassB);
            if (!force && a == _a && b == _b) return;
            _a = a; _b = b;
            if (!Powered)
            {
                if (Lift.ReadoutA != null) Lift.ReadoutA.text = a + " KG";
                if (Lift.ReadoutB != null) Lift.ReadoutB.text = b + " KG";
            }
            if (Status == null) return;
            float d = Lift.MassA - Lift.MassB;
            string verdict = Mathf.Abs(d) <= Lift.DiffThresholdKg ? "TOO EVEN - NOTHING MOVES"
                : d < 0f ? "<color=#3DDC97>PASSENGERS RISE</color>" : "<color=#FF4D5E>PASSENGERS SINK</color>";
            Status.text = (Powered ? "BRAKE OFF" : "BRAKE ON") + "\n<size=70%>" + verdict + "</size>";
        }
    }
}
