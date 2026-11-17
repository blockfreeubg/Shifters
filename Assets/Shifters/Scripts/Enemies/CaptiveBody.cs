// =====================================================================================
//  SHIFTERS - CaptiveBody.cs   [owned by the WEAPONS_ENEMIES agent]
//  Physical take-over of a captured character's body while a security bot carries it:
//  kinematic + all colliders disabled + parented to the bot's grab point. Free() restores
//  everything (parent, colliders, physics) at a world pose. State is NOT touched here.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class CaptiveBody
    {
        struct Saved
        {
            public Collider[] Colliders;
            public bool[] Enabled;
            public Transform Parent;
            public RigidbodyInterpolation Interpolation;
        }

        static readonly Dictionary<ShifterCharacter, Saved> _saved = new Dictionary<ShifterCharacter, Saved>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _saved.Clear();

        /// <summary>True while a bot physically holds this character.</summary>
        public static bool IsSeized(ShifterCharacter c) => c != null && _saved.ContainsKey(c);

        /// <summary>Take over the body: kinematic, colliders off, parented to holder at the given local pose.</summary>
        public static void Seize(ShifterCharacter c, Transform holder, Vector3 localPos, Quaternion localRot)
        {
            if (c == null || holder == null) return;
            if (!_saved.ContainsKey(c))
            {
                if (c.Grabber != null && c.Grabber.IsHolding) c.Grabber.ForceRelease(); // drops whatever it carried
                if (c.Motor != null) c.Motor.StandUp();
                var cols = c.GetComponentsInChildren<Collider>(true);
                var en = new bool[cols.Length];
                for (int i = 0; i < cols.Length; i++) { en[i] = cols[i].enabled; cols[i].enabled = false; }
                _saved[c] = new Saved
                {
                    Colliders = cols, Enabled = en, Parent = c.transform.parent,
                    Interpolation = c.Body != null ? c.Body.interpolation : RigidbodyInterpolation.Interpolate,
                };
            }
            var rb = c.Body;
            if (rb != null)
            {
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            c.transform.SetParent(holder, true);
            c.transform.localPosition = localPos;
            c.transform.localRotation = localRot;
            if (c.Visual != null) { c.Visual.SetPose(PoseOverride.Captured); c.Visual.SetExpression(Expression.Scared); }
        }

        /// <summary>Give the body back (parent, colliders, dynamic physics) at a world pose.</summary>
        public static void Free(ShifterCharacter c, Vector3 position, Quaternion rotation)
        {
            if (c == null) return;
            if (_saved.TryGetValue(c, out var s))
            {
                _saved.Remove(c);
                c.transform.SetParent(s.Parent, true);
                for (int i = 0; i < s.Colliders.Length; i++) if (s.Colliders[i] != null) s.Colliders[i].enabled = s.Enabled[i];
                if (c.Body != null) { c.Body.isKinematic = false; c.Body.interpolation = s.Interpolation; }
            }
            Vector3 fwd = rotation * Vector3.forward; fwd.y = 0f;
            Quaternion upright = fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd.normalized) : Quaternion.identity;
            if (c.Motor != null) { c.Motor.Teleport(position, upright); c.Motor.StandUp(); }
            else
            {
                c.transform.SetPositionAndRotation(position, upright);
                if (c.Body != null && !c.Body.isKinematic) { c.Body.position = position; c.Body.rotation = upright; c.Body.linearVelocity = Vector3.zero; }
            }
            if (c.Visual != null && c.Visual.CurrentPose == PoseOverride.Captured) c.Visual.SetPose(PoseOverride.None);
        }

        /// <summary>Re-assert kinematic while held (in case another system touched the body).</summary>
        public static void Maintain(ShifterCharacter c)
        {
            if (c == null || c.Body == null || !IsSeized(c)) return;
            if (!c.Body.isKinematic) { c.Body.linearVelocity = Vector3.zero; c.Body.isKinematic = true; }
        }
    }
}
