// =====================================================================================
//  SHIFTERS - L14PropScale.cs   [LEVEL 14 - THE SCALE OF JUSTICE: weighs PROPS, never interns]
//  Sums the masses of the registered non-character rigidbodies whose centre is over the pad (the CH03 JamSensor
//  rule). Latches ON at >= RequiredKg held for HoldSeconds. Only the 160 kg cabinet can ever solve it.
// =====================================================================================
using UnityEngine;
using TMPro;

namespace Shifters
{
    public class L14PropScale : SignalSource
    {
        public Rigidbody[] Props = new Rigidbody[0];
        public Vector2 PadSize = new Vector2(2.6f, 2.6f);
        public float RequiredKg = 150f;
        public float HoldSeconds = 0.5f;
        public TextMeshPro Readout;
        public Transform PanL, PanR;

        public float CurrentKg { get; private set; }
        float _hold, _shown = -1f, _poll;

        public void ForceLatch() { SetOn(true); Refresh(); }

        void Update()
        {
            _poll -= Time.deltaTime;
            if (_poll > 0f) return;
            _poll = 0.1f;
            float kg = 0f;
            Vector3 c = transform.position;
            for (int i = 0; i < Props.Length; i++)
            {
                var rb = Props[i];
                if (rb == null) continue;
                Vector3 p = rb.worldCenterOfMass;
                if (Mathf.Abs(p.x - c.x) <= PadSize.x * 0.5f && Mathf.Abs(p.z - c.z) <= PadSize.y * 0.5f && p.y < c.y + 2.2f && p.y > c.y - 0.5f)
                    kg += rb.mass;
            }
            CurrentKg = kg;
            if (!IsOn)
            {
                _hold = kg >= RequiredKg ? _hold + 0.1f : 0f;
                if (_hold >= HoldSeconds) SetOn(true);
            }
            Refresh();
        }

        void Refresh()
        {
            float shown = IsOn ? Mathf.Max(CurrentKg, RequiredKg) : CurrentKg;
            if (Readout != null && !Mathf.Approximately(shown, _shown))
            {
                _shown = shown;
                Readout.text = IsOn ? "GUILTY (" + Mathf.RoundToInt(shown) + " KG)" : Mathf.RoundToInt(shown) + " / " + Mathf.RoundToInt(RequiredKg) + " KG\n<size=60%>WEIGHS EVIDENCE, NOT INTERNS</size>";
            }
            float tilt = Mathf.Clamp01(shown / RequiredKg) * 0.35f;
            if (PanL != null) PanL.localPosition = new Vector3(PanL.localPosition.x, 2.0f - tilt, PanL.localPosition.z);
            if (PanR != null) PanR.localPosition = new Vector3(PanR.localPosition.x, 2.0f + tilt, PanR.localPosition.z);
        }
    }
}
