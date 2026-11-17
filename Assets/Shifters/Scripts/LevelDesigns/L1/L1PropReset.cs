// =====================================================================================
//  SHIFTERS - L1PropReset.cs   (LEVEL 1)
//  "OOPS" button receiver: poofs props back home (anti-softlock for the heavy block lane).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>On every rising edge of its inputs, respawns every <see cref="Targets"/> prop at its home spot.</summary>
    public class L1PropReset : SignalReceiver
    {
        public Respawnable[] Targets = new Respawnable[0];
        public string Toast = "Block reset. Please try to be less creative.";

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (!powered || initial) return;
            foreach (var r in Targets) if (r != null) r.Respawn();
            if (!string.IsNullOrEmpty(Toast)) GameEvents.RaiseToast(Toast, UIColors.Info);
        }
    }
}
