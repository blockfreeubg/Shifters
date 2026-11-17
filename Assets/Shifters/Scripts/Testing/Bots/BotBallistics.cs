// =====================================================================================
//  SHIFTERS - BotBallistics.cs   [BOTS module]
//  Throw solver for TeamBot.ThrowTo. Fixed launch speed (Tuning.ThrowSpeed(STR)), so the
//  free variables are the elevation and the throw spot. Supports a different gravity on the way
//  down (the motor may apply Tuning.FallGravityMultiplier to falling bodies).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class BotBallistics
    {
        /// <summary>Horizontal distance at which a launch (speed, elevation) from height 0 comes back DOWN through
        /// height dh. NaN if the apex is below dh.</summary>
        public static float RangeForHeight(float speed, float elevationDeg, float dh, float gUp, float gDown)
        {
            float th = elevationDeg * Mathf.Deg2Rad;
            float vx = speed * Mathf.Cos(th), vy = speed * Mathf.Sin(th);
            if (vy <= 0f) return float.NaN;
            float tUp = vy / gUp;
            float apex = vy * vy / (2f * gUp);
            if (apex < dh) return float.NaN;
            float tDown = Mathf.Sqrt(2f * (apex - dh) / gDown);
            return vx * (tUp + tDown);
        }

        /// <summary>Shortest / longest touchdown distance (descending through dh) over elevations [minDeg, maxDeg].
        /// false if no elevation in the range reaches dh.</summary>
        public static bool RangeSpan(float speed, float minDeg, float maxDeg, float dh, float gUp, float gDown, out float near, out float far)
        {
            near = float.MaxValue; far = 0f;
            for (float a = minDeg; a <= maxDeg + 1e-3f; a += 0.5f)
            {
                float r = RangeForHeight(speed, a, dh, gUp, gDown);
                if (float.IsNaN(r)) continue;
                near = Mathf.Min(near, r); far = Mathf.Max(far, r);
            }
            return far > 0f;
        }

        /// <summary>Flight time until coming down through dh (NaN if never).</summary>
        public static float FlightTime(float speed, float elevationDeg, float dh, float gUp, float gDown)
        {
            float vy = speed * Mathf.Sin(elevationDeg * Mathf.Deg2Rad);
            float apex = vy * vy / (2f * gUp);
            if (vy <= 0f || apex < dh) return float.NaN;
            return vy / gUp + Mathf.Sqrt(2f * (apex - dh) / gDown);
        }

        /// <summary>Height of the trajectory at horizontal distance x (relative to launch).</summary>
        public static float HeightAt(float speed, float elevationDeg, float x, float gUp, float gDown)
        {
            float th = elevationDeg * Mathf.Deg2Rad;
            float vx = Mathf.Max(0.01f, speed * Mathf.Cos(th)), vy = speed * Mathf.Sin(th);
            float t = x / vx, tUp = vy / gUp;
            if (t <= tUp) return vy * t - 0.5f * gUp * t * t;
            float apex = vy * vy / (2f * gUp), td = t - tUp;
            return apex - 0.5f * gDown * td * td;
        }

        /// <summary>Elevation in [minDeg, maxDeg] whose descending branch passes through (d, dh) within 5 cm. There are
        /// usually two (flat and lob): returns the one closest to preferDeg (NaN = the lowest). false if none.</summary>
        public static bool SolveElevation(float speed, float d, float dh, float gUp, float gDown, float minDeg, float maxDeg, out float elevationDeg, float preferDeg = float.NaN)
        {
            elevationDeg = float.NaN;
            float prevA = minDeg, prevErr = float.NaN;
            for (float a = minDeg; a <= maxDeg + 1e-3f; a += 0.5f)
            {
                float r = RangeForHeight(speed, a, dh, gUp, gDown);
                float err = float.IsNaN(r) ? float.NaN : r - d;
                float root = float.NaN;
                if (!float.IsNaN(err) && Mathf.Abs(err) < 0.05f) root = a;
                else if (!float.IsNaN(err) && !float.IsNaN(prevErr) && Mathf.Sign(err) != Mathf.Sign(prevErr))
                {
                    float lo = prevA, hi = a, elo = prevErr;
                    for (int i = 0; i < 30; i++)
                    {
                        float mid = 0.5f * (lo + hi);
                        float em = RangeForHeight(speed, mid, dh, gUp, gDown) - d;
                        if (float.IsNaN(em)) { lo = mid; continue; }
                        if (Mathf.Sign(em) == Mathf.Sign(elo)) { lo = mid; elo = em; } else hi = mid;
                    }
                    float m2 = 0.5f * (lo + hi);
                    if (Mathf.Abs(RangeForHeight(speed, m2, dh, gUp, gDown) - d) < 0.05f) root = m2;
                }
                if (!float.IsNaN(root))
                {
                    if (float.IsNaN(preferDeg)) { elevationDeg = root; return true; }
                    if (float.IsNaN(elevationDeg) || Mathf.Abs(root - preferDeg) < Mathf.Abs(elevationDeg - preferDeg)) elevationDeg = root;
                }
                prevA = a; prevErr = err;
            }
            return !float.IsNaN(elevationDeg);
        }

        /// <summary>Launch direction from a planar heading and an elevation.</summary>
        public static Vector3 Direction(Vector3 planarHeading, float elevationDeg)
        {
            planarHeading.y = 0f;
            if (planarHeading.sqrMagnitude < 1e-6f) planarHeading = Vector3.forward;
            planarHeading.Normalize();
            float th = elevationDeg * Mathf.Deg2Rad;
            return (planarHeading * Mathf.Cos(th) + Vector3.up * Mathf.Sin(th)).normalized;
        }

        /// <summary>Numerical check (unit tests): integrate and return the point where the body comes down through targetY.</summary>
        public static Vector3 Simulate(Vector3 start, Vector3 velocity, float gUp, float gDown, float targetY, float dt = 1f / 600f)
        {
            Vector3 p = start, v = velocity;
            for (int i = 0; i < 60000; i++)
            {
                float g = v.y > 0f ? gUp : gDown;
                v.y -= g * dt;
                Vector3 next = p + v * dt;
                if (v.y < 0f && next.y <= targetY && p.y >= targetY)
                {
                    float k = (p.y - targetY) / Mathf.Max(1e-6f, p.y - next.y);
                    return Vector3.Lerp(p, next, k);
                }
                p = next;
                if (p.y < targetY - 100f) break;
            }
            return p;
        }
    }
}
