// =====================================================================================
//  SHIFTERS - CharacterMotor.cs   [CONTRACT API - implementation owned by the CORE agent]
//  Rigidbody-based third-person locomotion (NOT Unity's CharacterController: we need real
//  mass for plates, stacking, throwing and being stood upon).
//  Brains (PlayerBrain / CharacterAI / test bots) write a Command every Update; the motor
//  consumes it in FixedUpdate.
//
//  Behaviour summary (DESIGN §5):
//   * ground probe = SphereCast from the capsule center (r*0.9), walkable <= 50 deg, ignores own
//     colliders + triggers; GroundCollider / GroundBody / StandingOn (stacking).
//   * grounded: gravity off, velocity fully driven (accel / strong decel), follows slopes, snaps to
//     the ground, inherits the motion of whatever it stands on (kinematic platforms, teammates,
//     ISurfaceVelocity conveyors). Airborne: gravity + extra fall gravity, 55% air control.
//   * jump buffering + coyote time + variable height, step-up <= 0.35 m, fall damage, footsteps.
//   * tumble (rotation unfrozen, frictional material) with auto get-up for Thrown/Stunned.
//   * never writes velocity while the body is kinematic or Condition is Carried/Bridging/Seated,
//     and never while the character is not Alive (except limp tumbling physics while Downed).
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class CharacterMotor : MonoBehaviour, ICharacterModule
    {
        public struct Command
        {
            /// <summary>Desired planar direction in WORLD space, magnitude 0..1.</summary>
            public Vector3 Move;
            public bool Sprint;
            /// <summary>Jump was pressed this frame (motor buffers it for Tuning.JumpBufferTime).</summary>
            public bool Jump;
            public bool JumpHeld;
            /// <summary>Optional facing override (aiming). Zero = face movement direction.</summary>
            public Vector3 Face;
        }

        public ShifterCharacter Character { get; private set; }

        // ---------------------------------------------------------------- readable state
        public bool IsGrounded { get; protected set; }
        public Vector3 GroundNormal { get; protected set; } = Vector3.up;
        public Collider GroundCollider { get; protected set; }
        public Rigidbody GroundBody { get; protected set; }
        /// <summary>Teammate we are standing on (stacking), else null.</summary>
        public ShifterCharacter StandingOn { get; protected set; }
        public Vector3 Velocity => Character != null && Character.Body != null ? Character.Body.linearVelocity : Vector3.zero;
        public Vector3 PlanarVelocity { get { var v = Velocity; v.y = 0f; return v; } }
        /// <summary>Planar speed normalized by current max sprint speed (0..1) - for animation.</summary>
        public float Speed01 { get; protected set; }
        public bool IsSprinting { get; protected set; }
        public bool IsTumbling { get; protected set; }
        public float AirTime { get; protected set; }
        public Vector3 Facing => transform.forward;
        public float WalkSpeed => Character != null && Character.Stats != null ? Tuning.WalkSpeed(Character.Stats.Speed) : 5f;
        public float SprintSpeed => Character != null && Character.Stats != null ? Tuning.SprintSpeed(Character.Stats.Speed) : 7f;
        /// <summary>External multipliers (carrying heavy things, slow zones...). Reset to 1 by whoever set them.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float JumpMultiplier { get; set; } = 1f;
        public Command CurrentCommand { get; protected set; }

        // ---------------------------------------------------------------- extra readable state (added)
        /// <summary>Velocity of the ground under the feet (platform / teammate / conveyor), world space.</summary>
        public Vector3 GroundVelocity { get; protected set; }
        /// <summary>Planar velocity relative to the ground (what the legs are doing) - for animation.</summary>
        public Vector3 LocalPlanarVelocity { get; protected set; }
        /// <summary>True during the 0.35 s blend from tumbling back to upright.</summary>
        public bool IsGettingUp => _gettingUp;
        /// <summary>True while Hold(true) is active or Condition is Interacting.</summary>
        public bool IsHolding => _hold;
        /// <summary>Seconds left of a non-tumble stun (Condition.Stunned without tumbling).</summary>
        public float StunRemaining => _stunTimer;
        /// <summary>Max planar distance a running jump can cover landing at the same height (AI / level checks).</summary>
        public float MaxJumpDistance => EstimateJumpDistance(0f, SprintSpeed * Mathf.Max(0f, SpeedMultiplier));

        // ---------------------------------------------------------------- events (animation/audio/fx listen)
        public event Action Jumped;
        public event Action<float> Landed;      // impact speed (m/s, positive)
        public event Action<int> Footstep;      // 0 = left, 1 = right
        public event Action Tumbled;            // started tumbling (thrown / knocked / flopped)
        public event Action GotUp;             // finished getting up

        protected void InvokeJumped() => Jumped?.Invoke();
        protected void InvokeLanded(float s) => Landed?.Invoke(s);
        protected void InvokeFootstep(int f) => Footstep?.Invoke(f);
        protected void InvokeTumbled() => Tumbled?.Invoke();
        protected void InvokeGotUp() => GotUp?.Invoke();

        // ---------------------------------------------------------------- tuning (motor-local)
        public const float SlopeLimit = 50f;
        public const float StepHeight = 0.35f;
        const float SnapDistance = 0.4f;           // max distance the ground snap pulls down
        const float GroundedEpsilon = 0.07f;       // feet-to-ground gap still counted as grounded when airborne
        const float MaxSnapSpeed = 10f;
        const float DecelMultiplier = 1.8f;        // braking / reversing
        const float IdleDecelMultiplier = 2.6f;    // no input: plant the feet (hard to shove off a plate)
        const float TumbleImpulseThreshold = 9f;   // impulses bigger than this always tumble
        const float GetUpBlendTime = 0.35f;
        const float TumbleMaxTime = 4f;
        const float SettleTime = 0.4f;
        const float VariableJumpCut = 0.5f;
        const float ImpulseControlTime = 0.35f;
        const float MaxGroundVelocity = 20f;

        // ---------------------------------------------------------------- shared physics materials
        static PhysicsMaterial s_frictionless, s_tumble;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_frictionless = null; s_tumble = null; }

        /// <summary>Zero-friction, zero-bounce material for character capsules (no wall sticking).</summary>
        public static PhysicsMaterial FrictionlessMaterial
        {
            get
            {
                if (s_frictionless == null)
                    s_frictionless = new PhysicsMaterial("ShiftersCharacter")
                    {
                        dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
                    };
                return s_frictionless;
            }
        }

        /// <summary>Grippy material used while tumbling so limp bodies come to rest.</summary>
        public static PhysicsMaterial TumbleMaterial
        {
            get
            {
                if (s_tumble == null)
                    s_tumble = new PhysicsMaterial("ShiftersTumble")
                    {
                        dynamicFriction = 0.6f, staticFriction = 0.7f, bounciness = 0.08f,
                        frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Average
                    };
                return s_tumble;
            }
        }

        // ---------------------------------------------------------------- runtime fields
        Rigidbody _body;
        CapsuleCollider _capsule;
        bool _initialized;
        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly Collider[] _overlaps = new Collider[16];

        float _jumpPressedTime = -999f;
        float _lastGroundedTime = -999f;
        bool _jumpedSinceGrounded;
        bool _jumpRising;
        float _groundLockout;
        float _impulseTimer;
        float _stunTimer;
        float _airKeepSpeed;
        float _prevAirVy;
        float _groundDistance;
        bool _hold;
        float _stepDistance;
        int _foot;

        // ground motion tracking
        Transform _trackedGround;
        Rigidbody _trackedGroundBody;
        Vector3 _trackedLocalPoint;
        Vector3 _trackedWorldPoint;
        Collider _surfaceCol;
        ISurfaceVelocity _surface;
        Collider _standCol;
        ShifterCharacter _standChar;

        // tumble
        float _tumbleTime, _tumbleMin, _settleTimer, _lastTumbleLandTime;
        bool _gettingUp;
        float _getUpT;
        Quaternion _getUpFromRot, _getUpToRot;
        Vector3 _getUpFromPos, _getUpToPos;
        float _defaultAngularDamping = 0.05f;

        protected virtual void Awake() { Character = GetComponent<ShifterCharacter>(); }

        /// <summary>Called by CharacterFactory after ShifterCharacter.Initialize.</summary>
        public void OnCharacterInitialized(ShifterCharacter c) { Character = c; Init(); }

        void Init()
        {
            if (Character == null) Character = GetComponent<ShifterCharacter>();
            if (Character == null || Character.Body == null) return;
            _body = Character.Body;
            _capsule = Character.Capsule;
            if (_capsule != null) _capsule.sharedMaterial = FrictionlessMaterial;
            _body.linearDamping = 0f;
            _body.angularDamping = _defaultAngularDamping;
            _body.maxDepenetrationVelocity = 6f;
            _body.sleepThreshold = 0.001f;
            _initialized = true;
        }

        void OnEnable() { if (Character == null) Character = GetComponent<ShifterCharacter>(); if (Character != null) Character.ConditionChanged += OnConditionChanged; }
        void OnDisable() { if (Character != null) Character.ConditionChanged -= OnConditionChanged; }

        void OnConditionChanged(ShifterCharacter c)
        {
            // Somebody else took the body (carry / bridge / seat): drop our tumble bookkeeping, they own physics now.
            var cond = c.Condition;
            if (cond == CharacterCondition.Carried || cond == CharacterCondition.Bridging || cond == CharacterCondition.Seated)
            {
                _gettingUp = false;
                IsTumbling = false;
                _stunTimer = 0f;
                ClearGround();
                if (_capsule != null) _capsule.sharedMaterial = FrictionlessMaterial;
                if (_body != null) _body.angularDamping = _defaultAngularDamping;
            }
            else if (cond == CharacterCondition.Normal) _stunTimer = 0f;
            if (cond == CharacterCondition.Interacting) _hold = true;
            else if (cond == CharacterCondition.Normal) _hold = false;
        }

        // ---------------------------------------------------------------- API
        public void SetCommand(in Command cmd)
        {
            CurrentCommand = cmd;
            if (cmd.Jump) _jumpPressedTime = Time.time;
        }

        public void ClearCommand() { CurrentCommand = default; }

        /// <summary>Instantly move (checkpoint restore, revive). Zeroes velocity, clears tumble.</summary>
        public virtual void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!_initialized) Init();
            _gettingUp = false;
            IsTumbling = false;
            _stunTimer = 0f;
            _impulseTimer = 0f;
            var up = UprightYaw(rotation);
            if (Character != null && Character.Body != null)
            {
                var b = Character.Body;
                b.constraints = RigidbodyConstraints.FreezeRotation;
                b.angularDamping = _defaultAngularDamping;
                b.position = position;
                b.rotation = up;
                if (!b.isKinematic) { b.linearVelocity = Vector3.zero; b.angularVelocity = Vector3.zero; }
            }
            if (_capsule != null) _capsule.sharedMaterial = FrictionlessMaterial;
            transform.SetPositionAndRotation(position, up);
            ClearGround();
            AirTime = 0f;
        }

        /// <summary>Knockback / launch. velocityChange in m/s. tumble => ragdoll-style tumble then get up.
        /// stunSeconds > 0 => no control for that long (Condition.Stunned).</summary>
        public virtual void AddImpulse(Vector3 velocityChange, bool tumble = false, float stunSeconds = 0f)
        {
            if (!_initialized) Init();
            if (Character == null || _body == null || _body.isKinematic) return;
            var cond = Character.Condition;
            if (cond == CharacterCondition.Carried || cond == CharacterCondition.Bridging || cond == CharacterCondition.Seated) return;
            _body.AddForce(velocityChange, ForceMode.VelocityChange);
            _impulseTimer = ImpulseControlTime;
            if (velocityChange.y > 1.5f) { _groundLockout = 0.15f; IsGrounded = false; }
            var planar = _body.linearVelocity + velocityChange; planar.y = 0f;
            _airKeepSpeed = Mathf.Max(_airKeepSpeed, planar.magnitude);

            bool big = velocityChange.magnitude > TumbleImpulseThreshold;
            if (Character.IsAlive && (tumble || big))
            {
                if (cond == CharacterCondition.Normal || cond == CharacterCondition.Interacting) Character.SetCondition(CharacterCondition.Stunned);
                BeginTumble(Mathf.Max(0.5f, stunSeconds));
            }
            else if (Character.IsAlive && stunSeconds > 0f && !IsTumbling)
            {
                if (cond == CharacterCondition.Normal || cond == CharacterCondition.Interacting || cond == CharacterCondition.Stunned)
                {
                    Character.SetCondition(CharacterCondition.Stunned);
                    _stunTimer = Mathf.Max(_stunTimer, stunSeconds);
                }
            }
        }

        /// <summary>Start ragdoll-ish tumbling (rotation unfrozen). Used by Thrown/Flopped/Stunned.
        /// The motor auto-recovers (gets up) when settled unless condition is Flopped or the character is not Alive.</summary>
        public virtual void BeginTumble(float minDuration = 0.5f)
        {
            if (!_initialized) Init();
            if (Character == null || _body == null) return;
            bool wasTumbling = IsTumbling;
            IsTumbling = true;
            _gettingUp = false;
            _tumbleTime = 0f;
            _tumbleMin = Mathf.Max(0.2f, minDuration);
            _settleTimer = 0f;
            _stunTimer = 0f;
            ClearGround();
            _body.constraints = RigidbodyConstraints.None;
            _body.useGravity = true;
            _body.angularDamping = 1.2f;
            if (_capsule != null) _capsule.sharedMaterial = TumbleMaterial;
            if (Character.IsAlive && (Character.Condition == CharacterCondition.Normal || Character.Condition == CharacterCondition.Interacting))
                Character.SetCondition(CharacterCondition.Stunned);
            if (!_body.isKinematic)
            {
                var axis = Vector3.Cross(Vector3.up, PlanarVelocity.sqrMagnitude > 0.5f ? PlanarVelocity.normalized : transform.forward);
                _body.AddTorque(axis * UnityEngine.Random.Range(2.5f, 5f) + UnityEngine.Random.insideUnitSphere * 1.5f, ForceMode.VelocityChange);
            }
            if (!wasTumbling) InvokeTumbled();
        }

        /// <summary>Force an immediate get-up (re-freeze rotation, stand upright).</summary>
        public virtual void StandUp()
        {
            if (!_initialized) Init();
            if (_body == null) return;
            if (IsTumbling && !_gettingUp) PrepareGetUp();
            FinishGetUp(immediate: true);
        }

        /// <summary>Blended get-up (0.35 s) from a tumble/flop. At the end Thrown/Stunned/Flopped conditions return to
        /// Normal (if alive) and GotUp fires. CharacterFlop may call this for "X again".</summary>
        public void GetUp()
        {
            if (!_initialized) Init();
            if (_body == null || _gettingUp) return;
            if (!IsTumbling) { FinishGetUp(immediate: true); return; }
            PrepareGetUp();
        }

        /// <summary>Make the body hold perfectly still in place (Interacting). false releases.</summary>
        public virtual void Hold(bool hold) { _hold = hold; }

        // ---------------------------------------------------------------- simulation
        void FixedUpdate()
        {
            if (!_initialized) { Init(); if (!_initialized) return; }
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            if (_groundLockout > 0f) _groundLockout -= dt;
            if (_impulseTimer > 0f) _impulseTimer -= dt;

            if (_body.isKinematic) { ClearGround(); Speed01 = 0f; IsSprinting = false; return; }
            var cond = Character.Condition;
            if (cond == CharacterCondition.Carried || cond == CharacterCondition.Bridging || cond == CharacterCondition.Seated)
            { ClearGround(); return; }

            if (_gettingUp) { UpdateGetUp(dt); return; }
            if (IsTumbling) { UpdateTumble(dt); return; }

            var state = Character.State;
            if (state == CharacterState.Downed) { BeginTumble(1f); return; }
            if (state != CharacterState.Alive) { _body.useGravity = true; ClearGround(); return; } // captured: bots own us

            EnsureUpright();
            UpdateStun(dt);
            UpdateGround(dt);
            Move(dt);
        }

        void UpdateStun(float dt)
        {
            if (_stunTimer <= 0f) return;
            _stunTimer -= dt;
            if (_stunTimer <= 0f)
            {
                _stunTimer = 0f;
                if (Character.Condition == CharacterCondition.Stunned) Character.SetCondition(CharacterCondition.Normal);
            }
        }

        void Move(float dt)
        {
            var cmd = CurrentCommand;
            var cond = Character.Condition;
            bool control = cond == CharacterCondition.Normal && _stunTimer <= 0f;
            bool hold = _hold || cond == CharacterCondition.Interacting;

            Vector3 move = Vector3.zero;
            if (control && !hold)
            {
                move = cmd.Move; move.y = 0f;
                if (move.sqrMagnitude > 1f) move.Normalize();
            }
            bool hasInput = move.sqrMagnitude > 0.0004f;
            bool sprint = control && cmd.Sprint && hasInput;
            int spd = Character.Stats != null ? Character.Stats.Speed : 5;
            float mult = Mathf.Max(0f, SpeedMultiplier);
            float maxSpeed = (sprint ? SprintSpeed : WalkSpeed) * mult;
            float accel = Tuning.Acceleration(spd);
            Vector3 targetRel = move * maxSpeed;
            Vector3 v = _body.linearVelocity;

            // ---------- facing
            Vector3 face = cmd.Face; face.y = 0f;
            if (face.sqrMagnitude < 0.0001f) face = hasInput ? move : Vector3.zero;
            if (face.sqrMagnitude > 0.0001f && (control || hold && cmd.Face.sqrMagnitude > 0.0001f))
            {
                var target = Quaternion.LookRotation(face.normalized, Vector3.up);
                _body.MoveRotation(Quaternion.RotateTowards(_body.rotation, target, Tuning.TurnSpeedDegrees(spd) * dt));
            }

            bool wantJump = control && !hold && Time.time - _jumpPressedTime <= Tuning.JumpBufferTime;
            bool canJump = !_jumpedSinceGrounded && (IsGrounded || Time.time - _lastGroundedTime <= Tuning.CoyoteTime);

            if (IsGrounded)
            {
                if (hasInput && !wantJump) TryStepUp(move, dt);
                Vector3 gv = GroundVelocity;
                Vector3 rel = v - gv; rel.y = 0f;
                float a = accel;
                if (!hasInput) a *= IdleDecelMultiplier;
                else if (Vector3.Dot(rel, targetRel) < 0f || rel.sqrMagnitude > targetRel.sqrMagnitude + 0.01f) a *= DecelMultiplier;
                if (_impulseTimer > 0f) a *= 0.12f;
                if (hold) rel = Vector3.zero;
                else rel = Vector3.MoveTowards(rel, targetRel, a * dt);
                LocalPlanarVelocity = rel;

                Vector3 along = rel;
                if (GroundNormal.y < 0.999f && rel.sqrMagnitude > 1e-5f)
                {
                    var proj = Vector3.ProjectOnPlane(rel, GroundNormal);
                    if (proj.sqrMagnitude > 1e-6f) along = proj.normalized * rel.magnitude;
                }
                float vy = along.y + gv.y;
                if (_groundDistance > 0.012f) vy -= Mathf.Min(_groundDistance / dt, MaxSnapSpeed);
                else if (_groundDistance < -0.02f) vy += Mathf.Min(-_groundDistance / dt, 4f);

                Vector3 nv = new Vector3(along.x + gv.x, vy, along.z + gv.z);
                _body.useGravity = false;
                if (wantJump && canJump) nv = DoJump(nv, gv.y);
                _body.linearVelocity = nv;
                AccumulateFootsteps(rel.magnitude, dt);
            }
            else
            {
                _body.useGravity = true;
                Vector3 planar = new Vector3(v.x, 0f, v.z);
                if (control && hasInput)
                {
                    float cur = planar.magnitude;
                    float cap = Mathf.Max(maxSpeed, Mathf.Min(cur, _airKeepSpeed));
                    Vector3 desired = move * cap;
                    planar = Vector3.MoveTowards(planar, desired, accel * Tuning.AirControl * dt);
                }
                float vy = v.y;
                if (_jumpRising)
                {
                    if (vy <= 0f) _jumpRising = false;
                    else if (!cmd.JumpHeld) { vy *= VariableJumpCut; _jumpRising = false; }
                }
                Vector3 nv = new Vector3(planar.x, vy, planar.z);
                if (wantJump && canJump) nv = DoJump(nv, 0f);
                else if (nv.y < 0f) _body.AddForce(Vector3.down * (Tuning.Gravity * (Tuning.FallGravityMultiplier - 1f)), ForceMode.Acceleration);
                _body.linearVelocity = nv;
                LocalPlanarVelocity = planar;
                _prevAirVy = nv.y;
            }

            float sprintMax = Mathf.Max(0.1f, SprintSpeed * Mathf.Max(0.1f, mult));
            Speed01 = Mathf.Clamp01(LocalPlanarVelocity.magnitude / sprintMax);
            IsSprinting = sprint && LocalPlanarVelocity.magnitude > WalkSpeed * mult * 0.9f;
        }

        Vector3 DoJump(Vector3 nv, float groundVy)
        {
            float jv = Tuning.JumpVelocity * Mathf.Max(0f, JumpMultiplier);
            // half-step compensation so the discrete arc reaches Tuning.JumpApexHeight
            jv += 0.5f * Tuning.Gravity * Time.fixedDeltaTime;
            nv.y = Mathf.Max(groundVy, 0f) + jv;
            _jumpPressedTime = -999f;
            _jumpedSinceGrounded = true;
            _jumpRising = true;
            _groundLockout = 0.12f;
            var planar = new Vector3(nv.x, 0f, nv.z);
            _airKeepSpeed = planar.magnitude;
            IsGrounded = false;
            _body.useGravity = true;
            ClearGround();
            InvokeJumped();
            return nv;
        }

        void AccumulateFootsteps(float speed, float dt)
        {
            if (speed < 0.6f) { _stepDistance = Mathf.Min(_stepDistance, 0.2f); return; }
            _stepDistance += speed * dt;
            float h = Character.Height;
            float stepLen = 0.45f * h + 0.08f * speed; // ~0.55 x stride
            if (_stepDistance >= stepLen)
            {
                _stepDistance -= stepLen;
                InvokeFootstep(_foot);
                _foot ^= 1;
            }
        }

        // ---------------------------------------------------------------- ground
        void UpdateGround(float dt)
        {
            bool wasGrounded = IsGrounded;
            bool found = false;
            RaycastHit hit = default;
            float gap = 0f;
            if (_groundLockout <= 0f)
            {
                float extra = wasGrounded ? SnapDistance : GroundedEpsilon;
                found = ProbeGround(extra, out hit, out gap);
                if (found && !wasGrounded)
                {
                    // only land when not moving up relative to the ground
                    var gvProbe = MeasureGroundVelocity(hit.collider, hit.point, dt, peek: true);
                    if (_body.linearVelocity.y - gvProbe.y > 1.0f) found = false;
                }
            }

            if (found)
            {
                var col = hit.collider;
                GroundCollider = col;
                GroundBody = col.attachedRigidbody;
                GroundNormal = hit.normal;
                _groundDistance = gap;
                if (col != _standCol) { _standCol = col; _standChar = GroundBody != null ? GroundBody.GetComponent<ShifterCharacter>() : null; }
                StandingOn = _standChar != null && _standChar != Character ? _standChar : null;
                Vector3 gv = MeasureGroundVelocity(col, hit.point, dt, peek: false);
                if (col != _surfaceCol) { _surfaceCol = col; _surface = col.GetComponentInParent<ISurfaceVelocity>(); }
                if (_surface != null) gv += _surface.GetSurfaceVelocity(hit.point);
                if (gv.sqrMagnitude > MaxGroundVelocity * MaxGroundVelocity) gv = gv.normalized * MaxGroundVelocity;
                GroundVelocity = gv;

                if (!wasGrounded)
                {
                    float impact = Mathf.Max(0f, -(_prevAirVy - gv.y));
                    if (AirTime > 0.05f) OnLanded(impact);
                }
                IsGrounded = true;
                AirTime = 0f;
                _lastGroundedTime = Time.time;
                _jumpedSinceGrounded = false;
                _jumpRising = false;
            }
            else
            {
                if (wasGrounded)
                {
                    var p = _body.linearVelocity; p.y = 0f;
                    _airKeepSpeed = p.magnitude;
                    _prevAirVy = _body.linearVelocity.y;
                }
                IsGrounded = false;
                GroundCollider = null;
                GroundBody = null;
                StandingOn = null;
                GroundNormal = Vector3.up;
                _groundDistance = 0f;
                _trackedGround = null;
                AirTime += dt;
            }
        }

        void OnLanded(float impact)
        {
            InvokeLanded(impact);
            if (impact > Tuning.FallDamageMinSpeed && Character.Condition != CharacterCondition.Thrown)
                Character.Damage((impact - Tuning.FallDamageMinSpeed) * Tuning.FallDamagePerMps, Vector3.zero, "a nasty fall");
        }

        /// <summary>Sphere cast from the capsule center down. gap = distance from the feet to the ground (negative = feet below).</summary>
        bool ProbeGround(float extra, out RaycastHit best, out float gap)
        {
            float h = Character.Height, r = Character.Radius;
            float pr = r * 0.9f;
            Vector3 feet = _body.position;
            float half = h * 0.5f;
            Vector3 origin = feet + Vector3.up * half;
            float toFeet = half - pr;
            int n = Physics.SphereCastNonAlloc(origin, pr, Vector3.down, _hits, toFeet + extra, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            best = default;
            gap = 0f;
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var hit = _hits[i];
                var col = hit.collider;
                if (col == null || IsOwn(col)) continue;
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // started overlapping
                Vector3 normal = hit.normal;
                if (Vector3.Angle(normal, Vector3.up) > SlopeLimit)
                {
                    // sphere touched an edge: check the real face normal under the contact point
                    var top = new Vector3(hit.point.x, origin.y, hit.point.z);
                    if (col.Raycast(new Ray(top, Vector3.down), out var faceHit, half + extra + 0.1f) && Vector3.Angle(faceHit.normal, Vector3.up) <= SlopeLimit)
                        normal = faceHit.normal;
                    else continue;
                }
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    best = hit;
                    best.normal = normal;
                    found = true;
                }
            }
            if (found) gap = bestDist - toFeet;
            return found;
        }

        bool IsOwn(Collider col) => col.attachedRigidbody == _body || col.transform.IsChildOf(transform);

        Vector3 MeasureGroundVelocity(Collider col, Vector3 point, float dt, bool peek)
        {
            var rb = col.attachedRigidbody;
            Transform t = rb != null ? rb.transform : col.transform;
            Vector3 vel = Vector3.zero;
            if (rb != null && !rb.isKinematic)
            {
                vel = rb.GetPointVelocity(point);
                if (!peek) Track(t, rb, point);
                return vel;
            }
            if (_trackedGround == t && dt > 0f)
            {
                Vector3 now = rb != null ? rb.position + rb.rotation * _trackedLocalPoint : t.TransformPoint(_trackedLocalPoint);
                Vector3 d = now - _trackedWorldPoint;
                if (d.sqrMagnitude < 4f) vel = d / dt;
            }
            if (!peek) Track(t, rb, point);
            return vel;
        }

        void Track(Transform t, Rigidbody rb, Vector3 point)
        {
            _trackedGround = t;
            _trackedGroundBody = rb;
            _trackedWorldPoint = point;
            _trackedLocalPoint = rb != null ? Quaternion.Inverse(rb.rotation) * (point - rb.position) : t.InverseTransformPoint(point);
        }

        void ClearGround()
        {
            IsGrounded = false;
            GroundCollider = null;
            GroundBody = null;
            StandingOn = null;
            GroundNormal = Vector3.up;
            GroundVelocity = Vector3.zero;
            _trackedGround = null;
            _trackedGroundBody = null;
            _groundDistance = 0f;
        }

        void TryStepUp(Vector3 move, float dt)
        {
            float r = Character.Radius;
            Vector3 dir = move.normalized;
            Vector3 feet = _body.position;
            float reach = r + 0.12f + PlanarVelocity.magnitude * dt;
            if (!Physics.Raycast(feet + Vector3.up * 0.06f, dir, out var low, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (IsOwn(low.collider) || Vector3.Angle(low.normal, Vector3.up) <= SlopeLimit) return;
            if (low.collider.attachedRigidbody != null && low.collider.attachedRigidbody.GetComponent<ShifterCharacter>() != null) return;
            Vector3 topOrigin = low.point + dir * 0.06f; topOrigin.y = feet.y + StepHeight + 0.05f;
            if (!Physics.Raycast(topOrigin, Vector3.down, out var top, StepHeight + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (IsOwn(top.collider) || Vector3.Angle(top.normal, Vector3.up) > SlopeLimit) return;
            float rise = top.point.y - feet.y;
            if (rise < 0.03f || rise > StepHeight) return;
            // clearance for the capsule on top of the step
            float h = Character.Height;
            Vector3 np = feet + Vector3.up * (rise + 0.02f) + dir * 0.05f;
            Vector3 p0 = np + Vector3.up * (r + 0.02f), p1 = np + Vector3.up * Mathf.Max(r + 0.02f, h - r - 0.02f);
            int n = Physics.OverlapCapsuleNonAlloc(p0, p1, r * 0.95f, _overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!IsOwn(_overlaps[i])) return;
            _body.position = np;
            _groundDistance = 0f;
        }

        void EnsureUpright()
        {
            var rot = _body.rotation;
            if (Vector3.Angle(rot * Vector3.up, Vector3.up) > 0.5f)
            {
                _body.constraints = RigidbodyConstraints.FreezeRotation;
                _body.rotation = UprightYaw(rot);
                _body.angularVelocity = Vector3.zero;
            }
            else if (_body.constraints != RigidbodyConstraints.FreezeRotation) _body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        static Quaternion UprightYaw(Quaternion rot)
        {
            var f = rot * Vector3.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) { f = rot * Vector3.up; f.y = 0f; f = -f; }
            if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            return Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        // ---------------------------------------------------------------- tumble
        void UpdateTumble(float dt)
        {
            _tumbleTime += dt;
            _body.useGravity = true;
            var v = _body.linearVelocity;
            if (v.y < 0f) _body.AddForce(Vector3.down * (Tuning.Gravity * (Tuning.FallGravityMultiplier - 1f)), ForceMode.Acceleration);
            Speed01 = 0f;
            IsSprinting = false;
            AirTime += dt;

            bool still = v.sqrMagnitude < 0.35f * 0.35f && _body.angularVelocity.sqrMagnitude < 1.2f;
            _settleTimer = still ? _settleTimer + dt : 0f;
            bool nearGround = NearGroundWhileTumbling();

            var cond = Character.Condition;
            bool alive = Character.IsAlive;
            if (!alive)
            {
                if (cond == CharacterCondition.Thrown && _settleTimer > SettleTime) Character.SetCondition(CharacterCondition.Normal);
                return; // Downed: stay limp until revived
            }
            if (cond == CharacterCondition.Flopped) return; // CharacterFlop decides
            bool recoverable = cond == CharacterCondition.Thrown || cond == CharacterCondition.Stunned || cond == CharacterCondition.Normal;
            if (!recoverable) return;
            if (_tumbleTime < _tumbleMin && cond != CharacterCondition.Normal) return;
            if (_settleTimer >= SettleTime || (_tumbleTime >= TumbleMaxTime && nearGround) || _tumbleTime >= TumbleMaxTime * 2f || cond == CharacterCondition.Normal && nearGround)
                PrepareGetUp();
        }

        bool NearGroundWhileTumbling()
        {
            Vector3 c = _capsule != null ? transform.TransformPoint(_capsule.center) : _body.worldCenterOfMass;
            float r = Character.Radius;
            int n = Physics.SphereCastNonAlloc(c, r * 0.8f, Vector3.down, _hits, Character.Height * 0.5f + 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (_hits[i].collider != null && !IsOwn(_hits[i].collider) && !(_hits[i].distance <= 0f && _hits[i].point == Vector3.zero)) return true;
            return false;
        }

        void PrepareGetUp()
        {
            _gettingUp = true;
            _getUpT = 0f;
            _getUpFromRot = _body.rotation;
            _getUpFromPos = _body.position;
            _getUpToRot = UprightYaw(_body.rotation);
            Vector3 c = _capsule != null ? transform.TransformPoint(_capsule.center) : _body.worldCenterOfMass;
            Vector3 feet = c - Vector3.up * (Character.Height * 0.5f);
            int n = Physics.RaycastNonAlloc(c + Vector3.up * 0.2f, Vector3.down, _hits, Character.Height + 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider == null || IsOwn(_hits[i].collider)) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; feet = _hits[i].point + Vector3.up * 0.02f; }
            }
            _getUpToPos = feet;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            _body.useGravity = false;
            if (!_body.isKinematic) { _body.linearVelocity = Vector3.zero; _body.angularVelocity = Vector3.zero; }
            if (_capsule != null) _capsule.sharedMaterial = FrictionlessMaterial;
        }

        void UpdateGetUp(float dt)
        {
            _getUpT += dt / GetUpBlendTime;
            float t = Mathf.Clamp01(_getUpT);
            float e = t * t * (3f - 2f * t);
            _body.MoveRotation(Quaternion.Slerp(_getUpFromRot, _getUpToRot, e));
            _body.MovePosition(Vector3.Lerp(_getUpFromPos, _getUpToPos, e));
            _body.linearVelocity = Vector3.zero;
            if (_getUpT >= 1f) FinishGetUp(immediate: false);
        }

        void FinishGetUp(bool immediate)
        {
            bool wasDown = IsTumbling || _gettingUp;
            if (_gettingUp && immediate)
            {
                _body.position = _getUpToPos;
            }
            _gettingUp = false;
            IsTumbling = false;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            _body.rotation = UprightYaw(_body.rotation);
            _body.angularDamping = _defaultAngularDamping;
            _body.useGravity = true;
            if (!_body.isKinematic) { _body.angularVelocity = Vector3.zero; if (immediate) _body.linearVelocity = Vector3.zero; }
            if (_capsule != null) _capsule.sharedMaterial = FrictionlessMaterial;
            _settleTimer = 0f;
            AirTime = 0f;
            if (Character.IsAlive)
            {
                var cond = Character.Condition;
                if (cond == CharacterCondition.Thrown || cond == CharacterCondition.Stunned || cond == CharacterCondition.Flopped)
                    Character.SetCondition(CharacterCondition.Normal);
            }
            if (wasDown) InvokeGotUp();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsTumbling || _body == null) return;
            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.up));
            if (impact > 2.5f && Time.time - _lastTumbleLandTime > 0.25f)
            {
                _lastTumbleLandTime = Time.time;
                InvokeLanded(impact);
            }
        }

        // ---------------------------------------------------------------- estimates
        /// <summary>Planar distance covered by a jump at planarSpeed that lands dh meters higher (negative = lower).</summary>
        public float EstimateJumpDistance(float dh, float planarSpeed)
        {
            float v0 = Tuning.JumpVelocity * Mathf.Max(0f, JumpMultiplier);
            float g = Tuning.Gravity, gf = Tuning.Gravity * Tuning.FallGravityMultiplier;
            float apex = v0 * v0 / (2f * g);
            if (dh > apex - 0.05f) return 0f;
            float tUp = v0 / g;
            float tDown = Mathf.Sqrt(2f * (apex - dh) / gf);
            return planarSpeed * (tUp + tDown);
        }
    }
}
