using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Shared helpers for puzzle elements: character lookup, cheap box tests, overlap buffers, formatting.</summary>
    public static class PuzzleUtil
    {
        static readonly Collider[] _hits = new Collider[64];

        /// <summary>The character owning this collider (capsule, head platform...) via its attached rigidbody, else null.
        /// Props carried by a character are NOT resolved to the carrier (they have their own rigidbody).</summary>
        public static ShifterCharacter CharacterFrom(Collider c)
        {
            if (c == null) return null;
            var rb = c.attachedRigidbody;
            if (rb == null) return null;
            return rb.TryGetComponent(out ShifterCharacter sc) ? sc : null;
        }

        /// <summary>World point inside a box given in t's local space (center + half extents)?</summary>
        public static bool InBox(Transform t, Vector3 localCenter, Vector3 half, Vector3 world)
        {
            var p = t.InverseTransformPoint(world) - localCenter;
            return p.x >= -half.x && p.x <= half.x && p.y >= -half.y && p.y <= half.y && p.z >= -half.z && p.z <= half.z;
        }

        /// <summary>Approximate capsule-vs-box test using the character's lower body and center points.</summary>
        public static bool CharacterInBox(Transform t, Vector3 localCenter, Vector3 half, ShifterCharacter c)
        {
            if (c == null) return false;
            float r = c.Radius * 0.6f;
            var h = new Vector3(half.x + r, half.y, half.z + r);
            return InBox(t, localCenter, h, c.Feet + Vector3.up * 0.2f) || InBox(t, localCenter, h, c.Center);
        }

        /// <summary>Distinct rigidbodies overlapping a world-space box (non-trigger colliders). Allocation-free.</summary>
        public static int OverlapBodies(Vector3 center, Vector3 half, Quaternion rot, List<Rigidbody> results, Rigidbody ignore = null)
        {
            results.Clear();
            int n = Physics.OverlapBoxNonAlloc(center, half, _hits, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var rb = _hits[i].attachedRigidbody;
                if (rb == null || rb == ignore || results.Contains(rb)) continue;
                results.Add(rb);
            }
            return results.Count;
        }

        public static string Kg(float kg) => Mathf.RoundToInt(kg).ToString() + " KG";

        /// <summary>Returns true at most once per interval (rate-limited feedback).</summary>
        public static bool RateLimit(ref float last, float interval)
        {
            if (Time.time - last < interval && last > 0f) return false;
            last = Time.time;
            return true;
        }

        /// <summary>Smoothstep easing.</summary>
        public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>Plays a visual gesture if the character has a visual (null-safe).</summary>
        public static void Gesture(ShifterCharacter c, VisualAction a)
        {
            if (c != null && c.Visual != null) c.Visual.Play(a);
        }

        public static void Pose(ShifterCharacter c, PoseOverride p)
        {
            if (c != null && c.Visual != null) c.Visual.SetPose(p);
        }

        public static void Express(ShifterCharacter c, Expression e, float seconds)
        {
            if (c != null && c.Visual != null) c.Visual.SetExpression(e, seconds);
        }

        public static string StrBlockedReason(int required, ShifterCharacter who)
            => "Requires STR " + required + " - " + (who != null ? who.DisplayName : "you") + " has STR " + (who != null && who.Stats != null ? who.Stats.Strength : 0);
    }

    /// <summary>Per-object cooldown for repeated hazard hits (no per-frame allocations after warm-up).</summary>
    public class PuzzleCooldown
    {
        readonly Dictionary<Object, float> _next = new Dictionary<Object, float>();
        public float Interval;
        public PuzzleCooldown(float interval) { Interval = interval; }

        public bool Ready(Object o)
        {
            if (o == null) return false;
            float now = Time.time;
            if (_next.TryGetValue(o, out var t) && now < t) return false;
            _next[o] = now + Interval;
            return true;
        }

        public void Clear() => _next.Clear();
    }
}
