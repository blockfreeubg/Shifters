// =====================================================================================
//  SHIFTERS - L6ValveLatch.cs   [CH03 - P2 "ARMOR" latch]
//  Latches ON the moment every input is ON together (V1 TimedButton window + V2/V3 WindowGates).
//  The boss consumes it (ARMOR OPEN 8 s) and calls ResetLatch() when the window closes, so a missed
//  shot can simply be retried with another valve run.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L6ValveLatch : SignalSource, IResettable
    {
        public SignalSource[] Inputs = new SignalSource[0];
        public Renderer[] LitWhenOn = new Renderer[0];

        void Update()
        {
            if (IsOn || Inputs == null || Inputs.Length == 0) return;
            for (int i = 0; i < Inputs.Length; i++) if (Inputs[i] == null || !Inputs[i].IsOn) return;
            SetOn(true);
            for (int i = 0; i < LitWhenOn.Length; i++) if (LitWhenOn[i] != null) LitWhenOn[i].enabled = true;
            Fx.FloatingText(transform.position + Vector3.up * 2.5f, "ALL VALVES VENTED!", UIColors.Speed, 1.4f);
        }

        public void ResetLatch()
        {
            if (IsOn) SetOn(false);
            for (int i = 0; i < LitWhenOn.Length; i++) if (LitWhenOn[i] != null) LitWhenOn[i].enabled = false;
        }

        public void ResetState() => ResetLatch();
    }
}
