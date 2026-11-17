// =====================================================================================
//  SHIFTERS - Grabbable.cs   [CONTRACT API - implementation owned by the ABILITIES agent]
//  Marks a rigidbody (prop or character) that a CharacterGrabber can pick up (F) and throw (Q).
//  Put it on the SAME GameObject as the Rigidbody. Props: Rigidbody + collider(s) + Grabbable
//  (+ WeightBody so it counts on plates). Characters get one from CharacterFactory.
//
//  While held (CharacterGrabber owns it):
//    * Rigidbody kinematic, interpolation off, CCD speculative, velocity zeroed before;
//    * every collider that was enabled is DISABLED (restored exactly on release);
//    * the WeightBody (if any) is disabled - the carrier's TotalMass carries the weight;
//    * parented to the carrier's CarryPoint (props: bottom-centre of their collider bounds
//      sits on the carry point; characters: feet on the carry point, upright root).
//  After a throw it is "InFlight" until its first impact (Landed event + noise for characters).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class Grabbable : MonoBehaviour
    {
        [Tooltip("Shown in prompts, e.g. 'crate'.")] public string DisplayName = "object";
        public bool CanBeGrabbed = true;
        [Tooltip("Extra multiplier on the launch speed of this object (1 = normal).")] public float ThrowSpeedMultiplier = 1f;

        public Rigidbody Body { get; protected set; }
        public ShifterCharacter Character { get; protected set; }   // non-null when this is a teammate
        public CharacterGrabber Holder { get; protected set; }
        public bool IsHeld => Holder != null;
        public float Mass => Character != null ? Character.Mass : (Body != null ? Body.mass : 0f);
        /// <summary>Name for prompts/toasts ("Brenda", "crate").</summary>
        public string PromptName => Character != null ? Character.DisplayName : DisplayName;

        // ---------------------------------------------------------------- flight (after a throw)
        /// <summary>True from a throw until the first solid impact (max 5 s). Enemies: a body InFlight hitting
        /// you faster than ~6 m/s is a "bowling" hit.</summary>
        public bool InFlight { get; protected set; }
        public CharacterGrabber LastThrower { get; protected set; }
        public float LastThrowTime { get; protected set; } = -999f;
        public float LastReleaseTime { get; protected set; } = -999f;

        public event Action<CharacterGrabber> Grabbed;
        public event Action<Vector3, bool> Released;               // velocity, wasThrown
        /// <summary>First impact after a throw: (this, contact point, impact speed).</summary>
        public event Action<Grabbable, Vector3, float> Landed;

        // ---------------------------------------------------------------- saved physics (CharacterGrabber)
        readonly List<Collider> _disabledColliders = new List<Collider>();
        public IReadOnlyList<Collider> DisabledColliders => _disabledColliders;
        bool _savedKinematic;
        RigidbodyInterpolation _savedInterpolation;
        CollisionDetectionMode _savedCcd;
        Transform _savedParent;
        bool _hadParent;
        WeightBody _weight;
        bool _savedWeightEnabled;
        bool _boundsValid;
        Bounds _localBounds;
        int _wriggles;
        float _lastWriggleTime = -99f;

        static readonly string[] WriggleLines = { "Hey!", "Put me DOWN!", "Unhand me!", "I bite!", "HR will hear about this!" };

        protected virtual void Awake() { Body = GetComponent<Rigidbody>(); Character = GetComponent<ShifterCharacter>(); _weight = GetComponent<WeightBody>(); }

        // Called only by CharacterGrabber:
        public virtual void NotifyGrabbed(CharacterGrabber by) { Holder = by; InFlight = false; _wriggles = 0; Grabbed?.Invoke(by); }
        public virtual void NotifyReleased(Vector3 velocity, bool thrown) { Holder = null; LastReleaseTime = Time.time; Released?.Invoke(velocity, thrown); }

        /// <summary>Collider bounds in this transform's local (unscaled) space. Props are carried/placed by it.</summary>
        public Bounds LocalBounds
        {
            get
            {
                if (!_boundsValid) { _localBounds = ComputeLocalBounds(); _boundsValid = true; }
                return _localBounds;
            }
        }

        /// <summary>Bottom-centre of LocalBounds scaled by localScale (offset from pivot to the resting point).</summary>
        public Vector3 BottomOffset
        {
            get
            {
                var b = LocalBounds;
                return Vector3.Scale(transform.localScale, new Vector3(b.center.x, b.min.y, b.center.z));
            }
        }

        /// <summary>Half the horizontal footprint (m) - used for placement in front of the carrier.</summary>
        public float FootprintRadius
        {
            get
            {
                if (Character != null) return Character.Radius;
                var e = Vector3.Scale(transform.localScale, LocalBounds.extents);
                return Mathf.Max(Mathf.Abs(e.x), Mathf.Abs(e.z));
            }
        }

        // ---------------------------------------------------------------- hold/release physics (CharacterGrabber)
        /// <summary>CharacterGrabber only: make kinematic, disable colliders + WeightBody, remember everything.</summary>
        public void BeginHoldPhysics()
        {
            if (Character == null) _boundsValid = false; // recompute fresh (cheap, rare)
            _hadParent = transform.parent != null;
            _savedParent = transform.parent;
            _disabledColliders.Clear();
            GetComponentsInChildren(false, s_colliderScratch);
            for (int i = 0; i < s_colliderScratch.Count; i++)
            {
                var c = s_colliderScratch[i];
                if (c != null && c.enabled) { c.enabled = false; _disabledColliders.Add(c); }
            }
            s_colliderScratch.Clear();
            if (Body != null)
            {
                _savedKinematic = Body.isKinematic;
                _savedInterpolation = Body.interpolation;
                _savedCcd = Body.collisionDetectionMode;
                if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
                if (Body.collisionDetectionMode == CollisionDetectionMode.Continuous || Body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
                    Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Body.isKinematic = true;
                Body.interpolation = RigidbodyInterpolation.None;
            }
            if (_weight == null) _weight = GetComponent<WeightBody>();
            if (_weight != null) { _savedWeightEnabled = _weight.enabled; _weight.enabled = false; }
        }

        /// <summary>CharacterGrabber only: unparent at the given pose and restore physics. Returns the restored body
        /// (non-kinematic unless it was kinematic before).</summary>
        public void EndHoldPhysics(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            var parent = _hadParent && _savedParent != null ? _savedParent : null;
            transform.SetParent(parent, true);
            transform.SetPositionAndRotation(position, rotation);
            for (int i = 0; i < _disabledColliders.Count; i++) if (_disabledColliders[i] != null) _disabledColliders[i].enabled = true;
            if (Body != null)
            {
                Body.position = position;
                Body.rotation = rotation;
                Body.isKinematic = _savedKinematic;
                Body.interpolation = _savedInterpolation;
                if (!Body.isKinematic)
                {
                    Body.collisionDetectionMode = _savedCcd;
                    Body.linearVelocity = velocity;
                    Body.angularVelocity = angularVelocity;
                    Body.WakeUp();
                }
            }
            if (_weight != null) _weight.enabled = _savedWeightEnabled;
        }

        static readonly List<Collider> s_colliderScratch = new List<Collider>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_colliderScratch.Clear();

        // ---------------------------------------------------------------- flight tracking
        /// <summary>CharacterGrabber only: start tracking the throw for the landing event/noise.</summary>
        public void BeginFlight(CharacterGrabber thrower)
        {
            InFlight = true;
            LastThrower = thrower;
            LastThrowTime = Time.time;
        }

        protected virtual void FixedUpdate()
        {
            if (!InFlight) return;
            float t = Time.time - LastThrowTime;
            if (t > 5f) { InFlight = false; return; }
            // characters: motor ground probe also counts as landing (soft landings may not raise OnCollisionEnter)
            if (Character != null && t > 0.2f && Character.Motor != null && Character.Motor.IsGrounded && Body != null && Body.linearVelocity.y <= 0.5f)
                Land(Character.Feet, Mathf.Abs(Body.linearVelocity.y));
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if (!InFlight) return;
            float t = Time.time - LastThrowTime;
            if (t < 0.06f) return;
            var rb = collision.rigidbody;
            if (LastThrower != null && LastThrower.Character != null && rb != null && rb == LastThrower.Character.Body && t < 0.4f) return;
            Vector3 p = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Land(p, collision.relativeVelocity.magnitude);
        }

        void Land(Vector3 point, float impactSpeed)
        {
            if (!InFlight) return;
            InFlight = false;
            if (Character != null)
            {
                GameEvents.RaiseNoise(point, Tuning.NoiseThrowLanding, Character, 2f);
                Sfx.At(SfxId.LandHeavy, point, Mathf.Clamp01(0.5f + impactSpeed / 20f));
                Fx.Puff(point, 1.1f);
            }
            else
            {
                GameEvents.RaiseNoise(point, Tuning.NoiseThrowLanding * 0.6f, null, 1f);
                Sfx.At(impactSpeed > 8f ? SfxId.Bonk : SfxId.Land, point, Mathf.Clamp01(0.4f + impactSpeed / 20f));
                Fx.Puff(point, 0.7f);
            }
            Landed?.Invoke(this, point, impactSpeed);
        }

        // ---------------------------------------------------------------- carried teammate struggles
        protected virtual void Update()
        {
            if (Holder == null || Character == null) return;
            if (AbilityInput.HasControl(Character) && ShiftersInput.JumpPressed) Wriggle();
        }

        /// <summary>Carried character struggles (Jump while carried). Three wriggles within 2 s break free.</summary>
        public void Wriggle()
        {
            if (Holder == null || Character == null) return;
            if (Time.time - _lastWriggleTime > 2f) _wriggles = 0;
            _lastWriggleTime = Time.time;
            _wriggles++;
            Sfx.At(SfxId.Squeak, transform.position, 0.8f, 0.9f + 0.15f * _wriggles);
            if (Character.Visual != null) Character.Visual.Play(VisualAction.Strain);
            Character.Say(WriggleLines[UnityEngine.Random.Range(0, WriggleLines.Length)]);
            if (_wriggles >= 3)
            {
                var h = Holder;
                _wriggles = 0;
                h.Drop();
                if (h.Character != null) h.Character.Say("Fine! Walk then!");
            }
        }

        protected virtual void OnDisable()
        {
            if (Holder != null) Holder.ForceRelease();
            InFlight = false;
        }

        // ---------------------------------------------------------------- bounds
        Bounds ComputeLocalBounds()
        {
            bool any = false;
            var result = new Bounds(Vector3.zero, Vector3.zero);
            var w2l = transform.worldToLocalMatrix;
            var cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c.isTrigger) continue;
                if (!TryColliderLocalBounds(c, out var lb)) continue;
                var m = w2l * c.transform.localToWorldMatrix;
                Encapsulate(ref result, ref any, m, lb);
            }
            if (!any)
            {
                var filters = GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i].sharedMesh == null) continue;
                    Encapsulate(ref result, ref any, w2l * filters[i].transform.localToWorldMatrix, filters[i].sharedMesh.bounds);
                }
            }
            if (!any) result = new Bounds(Vector3.zero, Vector3.one * 0.5f);
            return result;
        }

        static void Encapsulate(ref Bounds result, ref bool any, Matrix4x4 m, Bounds lb)
        {
            var c = lb.center; var e = lb.extents;
            for (int k = 0; k < 8; k++)
            {
                var corner = c + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else result.Encapsulate(p);
            }
        }

        static bool TryColliderLocalBounds(Collider c, out Bounds b)
        {
            switch (c)
            {
                case BoxCollider box: b = new Bounds(box.center, box.size); return true;
                case SphereCollider s: b = new Bounds(s.center, Vector3.one * (s.radius * 2f)); return true;
                case CapsuleCollider cap:
                {
                    var size = Vector3.one * (cap.radius * 2f);
                    size[Mathf.Clamp(cap.direction, 0, 2)] = Mathf.Max(cap.height, cap.radius * 2f);
                    b = new Bounds(cap.center, size); return true;
                }
                case MeshCollider mc when mc.sharedMesh != null: b = mc.sharedMesh.bounds; return true;
            }
            b = default;
            return false;
        }
    }
}
