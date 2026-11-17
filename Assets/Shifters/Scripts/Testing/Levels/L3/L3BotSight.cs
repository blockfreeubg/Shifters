// =====================================================================================
//  SHIFTERS - L3BotSight.cs   [LEVEL 3]  "could this security bot see an intern standing there?"
//  A copy of BotSenses.CanSee for an empty spot (range, 110-degree FOV or touch radius, line of sight to
//  the center or head of a 1.85 m intern; the bot's own colliders and characters are ignored). Used by the
//  L3 solution to pick the crossing route the parked bots can see least of, and by L3Diag.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class L3BotSight
    {
        static readonly RaycastHit[] _hits = new RaycastHit[16];

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Would bot b see an intern whose feet are at 'feet' (its current facing)?</summary>
        public static bool WouldSee(SecurityBot b, Vector3 feet)
        {
            if (b == null) return false;
            Vector3 eye = b.EyePosition;
            Vector3 center = feet + Vector3.up * 0.93f, head = feet + Vector3.up * 1.73f;
            Vector3 to = center - eye;
            if (to.magnitude > b.SightRange) return false;
            Vector3 flat = Flat(to);
            bool touching = flat.magnitude < BotSenses.TouchRadius;
            if (!touching && flat.sqrMagnitude > 1e-4f && Vector3.Angle(Flat(b.transform.forward), flat) > b.Fov * 0.5f) return false;
            return Clear(b, eye, center) || Clear(b, eye, head);
        }

        static bool Clear(SecurityBot b, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.05f) return true;
            d /= dist;
            int n = Physics.RaycastNonAlloc(from, d, _hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(b.transform) || c.GetComponentInParent<ShifterCharacter>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
