using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Kinematic rigidbody driven by a puzzle element (doors, platforms, lifts, crushers, sweepers) through
    /// MovePosition/MoveRotation in FixedUpdate. Tracks its own velocity so riders can inherit it:
    /// CharacterMotor reads GroundBody velocity; if PhysX reports zero velocity for kinematic bodies the motor can
    /// still use ISurfaceVelocity (enable <see cref="ReportSurfaceVelocity"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class MoverBody : MonoBehaviour, ISurfaceVelocity, IFreezable
    {
        /// <summary>When true, GetSurfaceVelocity returns this mover's velocity (in addition to whatever the motor reads
        /// from GroundBody). Default false: PhysX already reports kinematic MovePosition velocity to riders.</summary>
        public static bool ReportSurfaceVelocity = DefaultReportSurfaceVelocity;
        public const bool DefaultReportSurfaceVelocity = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ReportSurfaceVelocity = DefaultReportSurfaceVelocity;

        public Rigidbody Body { get; private set; }
        /// <summary>Linear velocity of the last move step (m/s).</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>Angular velocity of the last rotation step (rad/s, world).</summary>
        public Vector3 AngularVelocity { get; private set; }

        void Awake() => Ensure();

        /// <summary>Makes sure a kinematic, interpolated, gravity-free rigidbody exists.</summary>
        public Rigidbody Ensure()
        {
            if (Body != null) return Body;
            Body = GetComponent<Rigidbody>();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
            Body.isKinematic = true;
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            return Body;
        }

        /// <summary>Move to a world position this physics step.</summary>
        public void MoveTo(Vector3 world)
        {
            var b = Ensure();
            if (Time.time < _frozenUntil) { Velocity = Vector3.zero; return; }
            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : Tuning.FixedTimestep;
            Velocity = (world - b.position) / dt;
            b.MovePosition(world);
        }

        /// <summary>Rotate to a world rotation this physics step.</summary>
        public void RotateTo(Quaternion world)
        {
            var b = Ensure();
            if (Time.time < _frozenUntil) { AngularVelocity = Vector3.zero; return; }
            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : Tuning.FixedTimestep;
            var delta = world * Quaternion.Inverse(b.rotation);
            delta.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            AngularVelocity = float.IsNaN(axis.x) || Mathf.Abs(ang) < 1e-4f ? Vector3.zero : axis * (ang * Mathf.Deg2Rad / dt);
            b.MoveRotation(world);
        }

        /// <summary>Snap instantly (initial placement / reset). Not for per-frame use.</summary>
        public void Snap(Vector3 world, Quaternion? rot = null)
        {
            var b = Ensure();
            b.position = world;
            transform.position = world;
            if (rot.HasValue) { b.rotation = rot.Value; transform.rotation = rot.Value; }
            Velocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
        }

        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            var b = Body != null ? Body : Ensure();
            return Velocity + Vector3.Cross(AngularVelocity, worldPoint - b.worldCenterOfMass);
        }

        public Vector3 GetSurfaceVelocity(Vector3 worldPoint) => ReportSurfaceVelocity ? PointVelocity(worldPoint) : Vector3.zero;

        void OnDisable() { Velocity = Vector3.zero; AngularVelocity = Vector3.zero; }

        // ------------------------------------------------------------------ IFreezable (CAPS-LOCK cannon)
        float _frozenUntil = -1f;
        PuzzleHazard _hz;
        bool _hzInit;

        /// <summary>Frozen itself, or its hazard owner is frozen.</summary>
        public bool IsFrozen
        {
            get
            {
                if (Time.time < _frozenUntil) return true;
                if (!_hzInit) { _hzInit = true; _hz = GetComponentInParent<PuzzleHazard>(); }
                return _hz != null && _hz.IsFrozen;
            }
        }

        /// <summary>Doors, elevators, bridges, ramps and other receivers are CAPS-PROOF (only MovingPlatform and
        /// PuzzleHazard owners, or owner-less movers like WobblyPlatform, can be locked).</summary>
        public bool CapsProof
        {
            get
            {
                var r = GetComponentInParent<SignalReceiver>();
                return r != null && !(r is MovingPlatform) && !(r is PuzzleHazard);
            }
        }

        /// <summary>CAPS-LOCK: a hazard's mover forwards to the hazard; CAPS-PROOF movers ignore it (IsFrozen stays false).</summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;
            var hz = GetComponentInParent<PuzzleHazard>();
            if (hz != null) { hz.Freeze(seconds); return; }
            if (CapsProof) return;
            bool was = Time.time < _frozenUntil;
            _frozenUntil = Mathf.Max(_frozenUntil, Time.time + seconds);
            Velocity = Vector3.zero; AngularVelocity = Vector3.zero;
            if (!was)
            {
                Fx.FloatingText(transform.position + Vector3.up * 1.5f, "CAPS LOCK", UIColors.Info, 1.2f);
                Sfx.At(SfxId.CapsLock, transform.position);
            }
        }
    }
}
