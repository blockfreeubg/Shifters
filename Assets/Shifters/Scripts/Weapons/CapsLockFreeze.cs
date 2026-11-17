// =====================================================================================
//  SHIFTERS - CapsLockFreeze.cs   [owned by the WEAPONS agent]
//  CAPS-LOCK on a dynamic prop without IFreezable: kinematic for 6 s (velocity zeroed; a falling
//  crate becomes a step), then released. Also the padlock badge shown over any locked target.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class CapsLockFreeze : MonoBehaviour
    {
        public float Until { get; private set; }
        public bool IsLocked => Time.time < Until;
        public Rigidbody Body { get; private set; }
        bool _frozeBody, _wasKinematic;
        Transform _badge;
        Vector3 _badgeOffset;

        /// <summary>Freeze a dynamic body in place for seconds (re-locking extends).</summary>
        public static CapsLockFreeze Lock(Rigidbody rb, float seconds)
        {
            if (rb == null) return null;
            var f = rb.GetComponent<CapsLockFreeze>();
            if (f == null) f = rb.gameObject.AddComponent<CapsLockFreeze>();
            f.Body = rb;
            if (!f._frozeBody)
            {
                f._wasKinematic = rb.isKinematic;
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
                f._frozeBody = true;
            }
            f.Show(rb.worldCenterOfMass + Vector3.up * 0.2f, seconds);
            return f;
        }

        /// <summary>Badge only (the target froze itself via IFreezable).</summary>
        public static CapsLockFreeze Badge(Transform t, Vector3 at, float seconds)
        {
            if (t == null) return null;
            var f = t.GetComponent<CapsLockFreeze>();
            if (f == null) f = t.gameObject.AddComponent<CapsLockFreeze>();
            f.Show(at, seconds);
            return f;
        }

        void Show(Vector3 at, float seconds)
        {
            Until = Mathf.Max(Until, Time.time + seconds);
            if (_badge == null)
            {
                _badge = new GameObject("CapsLockBadge").transform;
                var blue = MaterialLibrary.Unlit(WeaponStyle.Tint(WeaponKind.CapsLock), 1.6f);
                var body = ProcMesh.Cached("caps_lock_body", () => ProcMesh.Box(new Vector3(0.34f, 0.28f, 0.12f), 0.03f));
                var shackle = ProcMesh.Cached("caps_lock_shackle", () => ProcMesh.Torus(0.12f, 0.03f, 12, 4));
                ProcMesh.Part("Body", _badge, body, blue, Vector3.zero, null, null, false);
                ProcMesh.Part("Shackle", _badge, shackle, blue, new Vector3(0f, 0.17f, 0f), Quaternion.Euler(90f, 0f, 0f), null, false);
            }
            _badge.gameObject.SetActive(true);
            _badgeOffset = at - transform.position + Vector3.up * 0.5f;
            enabled = true;
        }

        void Update()
        {
            if (_badge != null)
            {
                _badge.position = transform.position + _badgeOffset + Vector3.up * (0.05f * Mathf.Sin(Time.time * 3f));
                _badge.rotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            }
            if (IsLocked) return;
            if (_frozeBody && Body != null)
            {
                var g = Body.GetComponent<Grabbable>();
                if (g == null || !g.IsHeld) Body.isKinematic = _wasKinematic; // a holder manages its own body state
            }
            _frozeBody = false;
            if (_badge != null) _badge.gameObject.SetActive(false);
            Fx.FloatingText(transform.position + Vector3.up, "caps lock off", WeaponStyle.Tint(WeaponKind.CapsLock), 0.7f);
            enabled = false;
        }

        void OnDestroy() { if (_badge != null) Kit.SafeDestroy(_badge.gameObject); }
    }
}
