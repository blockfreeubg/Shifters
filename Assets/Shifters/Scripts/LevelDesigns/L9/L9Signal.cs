// SHIFTERS - L9Signal.cs   [LEVEL 9 - THE GETAWAY]  plain settable signal (checkpoint restores: "toll already paid")
using UnityEngine;

namespace Shifters
{
    /// <summary>Script-driven signal source (CH08: the toll gate stays open after a restore past ST2).</summary>
    public class L9Signal : SignalSource, IResettable
    {
        public bool StartsOn;
        void Awake() { if (StartsOn) SetOn(true); }
        public void Set(bool on) => SetOn(on);
        public void ResetState() => SetOn(StartsOn);
    }
}
