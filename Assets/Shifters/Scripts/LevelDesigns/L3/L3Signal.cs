// =====================================================================================
//  SHIFTERS - L3Signal.cs   [LEVEL 3 - Sacrifice Zone]
//  Plain script-driven signal source ("chair released" -> maintenance tunnel door).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Plain settable signal source (script-driven: "chair released", "tunnel open"...).</summary>
    public class L3Signal : SignalSource, IResettable
    {
        public bool StartsOn;
        void Awake() { if (StartsOn) SetOn(true); }
        /// <summary>Turn the signal on/off.</summary>
        public void Set(bool on) => SetOn(on);
        public void ResetState() => SetOn(StartsOn);
    }
}
