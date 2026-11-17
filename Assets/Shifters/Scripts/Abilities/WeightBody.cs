// =====================================================================================
//  SHIFTERS - WeightBody.cs   [CONTRACT FILE - weight/support graph, implemented by the architect]
//  On every body that should weigh on plates/bridges/lifts (characters, crates, barrels, blocks).
//  Tracks what it rests on (SupportCollider/SupportBody) from the character motor's ground probe
//  (characters) and from upward-facing collision contacts (props, tumbling characters).
//  WeightSensor walks this support graph so stacked weight counts: a character on a crate on a
//  plate = both masses.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class WeightBody : MonoBehaviour
    {
        public static readonly List<WeightBody> All = new List<WeightBody>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [Tooltip("Optional override (kg). <= 0 uses Rigidbody.mass / character mass.")] public float MassOverride = 0f;

        public Rigidbody Body { get; protected set; }
        public ShifterCharacter Character { get; protected set; }
        /// <summary>Own mass + carried mass (characters).</summary>
        public virtual float EffectiveMass =>
            MassOverride > 0f ? MassOverride : Character != null ? Character.TotalMass : (Body != null ? Body.mass : 0f);
        public Collider SupportCollider { get; protected set; }
        public Rigidbody SupportBody => SupportCollider != null ? SupportCollider.attachedRigidbody : null;
        /// <summary>False while carried (the carrier already includes it), bridging, or not alive-and-present.</summary>
        public virtual bool Counts
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                if (Character == null) return Body == null || Body.gameObject.activeInHierarchy;
                if (Character.State == CharacterState.Lost || Character.State == CharacterState.Captured) return false;
                return Character.Condition != CharacterCondition.Carried && Character.Condition != CharacterCondition.Bridging;
            }
        }

        Collider _pendingSupport;
        float _pendingNormalY;
        float _lastSupportTime = -1f;
        const float SupportGrace = 0.12f;

        protected virtual void Awake() { Body = GetComponent<Rigidbody>(); Character = GetComponent<ShifterCharacter>(); }
        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); SupportCollider = null; }

        protected virtual void FixedUpdate()
        {
            // 1) characters standing normally: trust the motor's ground probe
            Collider support = null;
            if (Character != null && Character.Motor != null && Character.Motor.IsGrounded && Character.Motor.GroundCollider != null)
                support = Character.Motor.GroundCollider;
            // 2) otherwise: best upward contact collected during the last physics step
            if (support == null && _pendingSupport != null) support = _pendingSupport;

            if (support != null) { SupportCollider = support; _lastSupportTime = Time.time; }
            else if (Body != null && !Body.isKinematic && Body.IsSleeping() && SupportCollider != null && SupportCollider.enabled)
                _lastSupportTime = Time.time; // sleeping bodies get no OnCollisionStay: keep the last support
            else if (Time.time - _lastSupportTime > SupportGrace) SupportCollider = null;

            _pendingSupport = null;
            _pendingNormalY = 0f;
        }

        protected virtual void OnCollisionStay(Collision collision)
        {
            int n = collision.contactCount;
            for (int i = 0; i < n; i++)
            {
                var cp = collision.GetContact(i);
                // normal points from the other collider toward this body; > 0.5 = we rest on top of it
                if (cp.normal.y > 0.5f && cp.normal.y > _pendingNormalY)
                {
                    _pendingNormalY = cp.normal.y;
                    _pendingSupport = collision.collider;
                }
            }
        }

        /// <summary>True if this body rests on any of the given colliders, or on a rigidbody in 'bodies'.</summary>
        public bool RestsOn(Collider[] surfaces, HashSet<Rigidbody> bodies)
        {
            var sc = SupportCollider;
            if (sc == null) return false;
            for (int i = 0; i < surfaces.Length; i++) if (surfaces[i] == sc) return true;
            var sb = sc.attachedRigidbody;
            return sb != null && bodies.Contains(sb);
        }
    }
}
