// =====================================================================================
//  SHIFTERS - UITime.cs   (UI agent)
//  Real (unscaled) time for UI animation that also stays deterministic under the DevPlay
//  harness (Time.captureDeltaTime fixes frames at 1/60 while unscaledDeltaTime is wall-clock).
//  Same semantics as TimeController.RealDeltaTime / RealTime, without depending on it.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class UITime
    {
        static float _now;
        static int _frame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _now = 0f; _frame = -1; }

        /// <summary>Unscaled frame delta (capture delta when the harness fixes the frame time).</summary>
        public static float Delta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        /// <summary>Accumulated unscaled seconds (advances once per frame; ignores timeScale/pause).</summary>
        public static float Now
        {
            get
            {
                int f = Time.frameCount;
                if (f != _frame)
                {
                    // frames since the last query (queried every frame by the tween runner; robust if not)
                    int frames = _frame < 0 ? 1 : Mathf.Clamp(f - _frame, 1, 600);
                    _frame = f;
                    _now += Mathf.Min(Delta, 0.25f) * frames;
                }
                return _now;
            }
        }
    }
}
