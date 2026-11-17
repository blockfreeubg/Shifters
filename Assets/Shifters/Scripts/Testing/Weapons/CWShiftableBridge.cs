using UnityEngine;

namespace Shifters
{
    /// <summary>Calls puzzles' PuzzleKit.MakeShiftable (CAMPAIGN 18.5).</summary>
    public static class CWShiftableBridge
    {
        public static bool Make(Rigidbody rb) => PuzzleKit.MakeShiftable(rb) != null;
    }
}
