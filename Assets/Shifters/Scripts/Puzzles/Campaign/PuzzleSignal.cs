// =====================================================================================
//  SHIFTERS - PuzzleSignal.cs   [PUZZLES - campaign wave; promoted copy of L3Signal]
//  Plain script-driven signal source ("chair released" -> maintenance tunnel door).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Plain settable signal source (script-driven: "chair released", "tunnel open"...).</summary>
    public class PuzzleSignal : SignalSource, IResettable
    {
        public bool StartsOn;
        void Awake() { if (StartsOn) SetOn(true); }
        /// <summary>Turn the signal on/off.</summary>
        public void Set(bool on) => SetOn(on);
        public void ResetState() => SetOn(StartsOn);
    }
}
