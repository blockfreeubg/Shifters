using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Enables/disables hazard components from signals. Default: when POWERED the targets are switched OFF
    /// (press the plate to kill the lasers); set HazardOnWhenPowered for the opposite. PuzzleHazard targets are gated
    /// with SetToggleEnabled (keeps their visuals/cycles consistent); any other Behaviour gets .enabled.
    /// </summary>
    public class HazardToggle : SignalReceiver
    {
        public Behaviour[] Targets = new Behaviour[0];
        public bool HazardOnWhenPowered;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            bool on = HazardOnWhenPowered ? powered : !powered;
            foreach (var b in Targets)
            {
                if (b == null) continue;
                if (b is PuzzleHazard h) h.SetToggleEnabled(on);
                else b.enabled = on;
            }
        }
    }
}
