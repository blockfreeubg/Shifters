// =====================================================================================
//  SHIFTERS - AbilityPhysics.cs   [ABILITIES module]
//  Allocation-free physics probes used by the abilities (ground/gap probes that skip triggers,
//  the caller's own body and - optionally - every character).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class AbilityPhysics
    {
        static RaycastHit[] s_hits = new RaycastHit[32];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_hits = new RaycastHit[32];

        public static bool IsCharacterCollider(Collider c)
        {
            var rb = c.attachedRigidbody;
            return rb != null && rb.GetComponent<ShifterCharacter>() != null;
        }

        /// <summary>Nearest non-trigger hit, skipping colliders attached to 'ignore' and (optionally) any character.</summary>
        public static bool Raycast(Vector3 origin, Vector3 dir, float distance, out RaycastHit hit, Rigidbody ignore = null, bool ignoreCharacters = true)
        {
            hit = default;
            int n = Physics.RaycastNonAlloc(origin, dir, s_hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = s_hits[i];
                if (h.collider == null) continue;
                var rb = h.collider.attachedRigidbody;
                if (ignore != null && rb == ignore) continue;
                if (ignoreCharacters && rb != null && rb.GetComponent<ShifterCharacter>() != null) continue;
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }

        /// <summary>Like Raycast but a sphere sweep.</summary>
        public static bool SphereCast(Vector3 origin, float radius, Vector3 dir, float distance, out RaycastHit hit, Rigidbody ignore = null, bool ignoreCharacters = true)
        {
            hit = default;
            int n = Physics.SphereCastNonAlloc(origin, radius, dir, s_hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = s_hits[i];
                if (h.collider == null) continue;
                var rb = h.collider.attachedRigidbody;
                if (ignore != null && rb == ignore) continue;
                if (ignoreCharacters && rb != null && rb.GetComponent<ShifterCharacter>() != null) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // started overlapping: ignore
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }

        /// <summary>Walkable ground height below p (searching from p.y+up down to p.y-down).</summary>
        public static bool GroundBelow(Vector3 p, float up, float down, out RaycastHit hit, Rigidbody ignore = null)
        {
            if (Raycast(p + Vector3.up * up, Vector3.down, up + down, out hit, ignore, true)) return hit.normal.y > 0.55f;
            return false;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float EaseInQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
    }
}
