// =====================================================================================
//  SHIFTERS - CharacterVisual.cs   [CONTRACT API - implementation owned by the VISUALS agent]
//  Builds the procedural low-poly model for a character from its profile (archetype
//  silhouette + random proportions + accessory + face) and animates it procedurally from
//  the motor state (walk/run/jump/fall/land/tumble/carry/bridge/KO...). Expressive face.
//
//  Pose resolution each frame: explicit SetPose() wins; otherwise derived automatically from
//  State (Downed->KO, Captured->Captured), Condition (Carried, Bridging->Bridge, Flopped->Flop,
//  Seated), Grabber.IsHolding (Carrying) and Weapon.WantsAimPose (Aiming: RMB or the hip-aim window).
//  Expression resolution: SetExpression() (manual) > state > gesture > hurt flash > pose/condition
//  > motion (falling, sprinting) > Neutral. SetExpression(Neutral, 0) returns to automatic.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class CharacterVisual : MonoBehaviour, ICharacterModule
    {
        /// <summary>Fake motor values (preview drivers, cutscenes). When set, the motor is ignored.</summary>
        public struct VisualMotion
        {
            public Vector3 Velocity;
            public bool Grounded, Sprinting, Tumbling;
            public float AirTime;
            public Vector3 AngularVelocity;
        }

        public ShifterCharacter Character { get; protected set; }
        /// <summary>Root of the generated model (child "Model"). Rotations/scales of the model only - never the physics root.</summary>
        public Transform ModelRoot { get; protected set; }
        public Transform Head { get; protected set; }
        public Transform Torso { get; protected set; }
        public Transform HandL { get; protected set; }
        public Transform HandR { get; protected set; }
        /// <summary>Weapon attach point in the right hand (+Z = barrel direction).</summary>
        public Transform WeaponSocket { get; protected set; }
        public Renderer[] Renderers { get; protected set; } = new Renderer[0];
        public Expression CurrentExpression { get; protected set; }
        public PoseOverride CurrentPose { get; protected set; }

        // ------------------------------------------------------------------ extended API
        public CharacterRig Rig { get; protected set; }
        public CharacterAnimator Animator { get; protected set; }
        public FaceController Face => Rig != null ? Rig.Face : null;
        /// <summary>Pose actually shown this frame (explicit or derived from state/condition).</summary>
        public PoseOverride EffectivePose { get; protected set; }
        /// <summary>Test/cutscene hook: fake motor values (null = read the real CharacterMotor).</summary>
        public VisualMotion? MotionOverride { get; set; }
        /// <summary>World point the eyes track (null = active teammate / camera).</summary>
        public Vector3? LookTarget { get; set; }
        /// <summary>Aiming arm pitch in degrees (+ = up). Used by the Aiming pose.</summary>
        public float AimPitch { get; set; }
        /// <summary>Happy face + squash/stretch hop when this character becomes the active one (CharacterManager plays the wave).</summary>
        public bool AutoGreet = true;
        /// <summary>World-space head center (for speech bubbles, stars, portraits).</summary>
        public Vector3 HeadCenter => Rig != null ? Rig.HeadCenterWorld : transform.position + Vector3.up * 1.5f;

        Expression _manual; bool _manualOn; float _manualUntil = -1f;
        Expression _flash; float _flashUntil, _happyUntil;
        float _bridgeLen = Tuning.BridgeLength, _bridgeSag; bool _bridgeExplicit;
        Vector3 _prevFwd; bool _hasPrevFwd;
        Vector3 _prevPos; bool _hasPrevPos;
        float _starsTimer, _speedLinesTimer, _camRefresh;
        Transform _cam;
        CharacterMotor _motor;
        bool _subscribed;
        CharacterState _lastState = CharacterState.Alive;

        public virtual void OnCharacterInitialized(ShifterCharacter c)
        {
            Unsubscribe();
            Character = c;
            Rig?.Dispose();
            var profile = c != null && c.Profile != null ? c.Profile : new CharacterProfile();
            int seed = (c != null ? c.Slot * 7919 : 0) + (profile.Name ?? "").Length * 131 + 17;
            Rig = CharacterModelBuilder.Build(profile, transform, seed);
            Animator = new CharacterAnimator(Rig, seed);
            ModelRoot = Rig.Root; Head = Rig.Head; Torso = Rig.Spine;
            HandL = Rig.HandL; HandR = Rig.HandR; WeaponSocket = Rig.WeaponSocket;
            Renderers = Rig.Renderers;
            CurrentExpression = Expression.Neutral;
            Subscribe();
            CharacterDisguise.OnVisualBuilt(this);
        }

        // ------------------------------------------------------------------ campaign: SCARED + disguise
        /// <summary>null = automatic (ShifterCharacter.IsScared); true/false forces the SCARED look (tests, cutscenes).</summary>
        public bool? ScaredOverride { get; set; }
        /// <summary>SCARED look active this frame (tremble + Scared face).</summary>
        public bool IsScaredLook { get; private set; }
        /// <summary>Trembling amplitude (degrees) of the SCARED look.</summary>
        public static float ScaredTremble = 2.4f;
        public bool HasDisguise => CharacterDisguise.HasMustache(Rig);
        /// <summary>Heist fake mustache on/off.</summary>
        public void SetDisguise(bool on) { CharacterDisguise.SetMustache(Rig, on); Renderers = Rig != null ? Rig.Renderers : Renderers; }

        bool ResolveScared()
        {
            if (ScaredOverride.HasValue) return ScaredOverride.Value;
            var c = Character;
            return c != null && c.State == CharacterState.Alive && CharacterDisguise.ProbeScared(c);
        }

        // ------------------------------------------------------------------ contract API
        /// <summary>duration 0 = keep until changed; otherwise revert to the automatic expression afterwards.
        /// SetExpression(Neutral, 0) clears a manual expression.</summary>
        public virtual void SetExpression(Expression e, float duration = 0f)
        {
            if (e == Expression.Neutral && duration <= 0f) { _manualOn = false; return; }
            _manual = e; _manualOn = true; _manualUntil = duration > 0f ? Time.time + duration : -1f;
        }

        public virtual void Play(VisualAction action) { Animator?.Play(action); }
        public virtual void SetPose(PoseOverride pose) { CurrentPose = pose; }

        /// <summary>Bridge pose: stretch the body along +Z to this length (meters).</summary>
        public virtual void SetBridgeStretch(float length, float sag01)
        {
            _bridgeExplicit = true; _bridgeLen = Mathf.Max(1f, length); _bridgeSag = Mathf.Clamp01(sag01);
        }

        /// <summary>Rim/emission highlight (perspective, switcher selection). strength 0 = off.</summary>
        public virtual void SetHighlight(Color color, float strength) { Rig?.Mats.SetHighlight(color, strength); }

        /// <summary>Greyed-out look for KO/Lost/Captured (portraits & world).</summary>
        public virtual void SetDesaturated(bool on) { Rig?.Mats.SetDesaturated(on); }

        public void SetVisible(bool on) { Rig?.SetVisible(on); }

        /// <summary>Preview/cutscene helpers (the real motor events call these automatically).</summary>
        public void NotifyJumped() => Animator?.OnJump();
        public void NotifyLanded(float impactSpeed) => Animator?.OnLand(impactSpeed);
        public void NotifyTumbled() => Animator?.Kick(-1.5f);

        // ------------------------------------------------------------------ events
        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _motor = Character != null ? Character.Motor : null;
            if (_motor == null) _motor = GetComponent<CharacterMotor>();
            if (_motor != null) { _motor.Jumped += OnJumped; _motor.Landed += OnLanded; _motor.Tumbled += OnTumbled; _motor.GotUp += OnGotUp; }
            GameEvents.CharacterSwitched += OnSwitched;
            GameEvents.CharacterDamaged += OnDamaged;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (_motor != null) { _motor.Jumped -= OnJumped; _motor.Landed -= OnLanded; _motor.Tumbled -= OnTumbled; _motor.GotUp -= OnGotUp; }
            GameEvents.CharacterSwitched -= OnSwitched;
            GameEvents.CharacterDamaged -= OnDamaged;
        }

        void OnJumped() => Animator?.OnJump();
        void OnLanded(float s) => Animator?.OnLand(s);
        void OnTumbled() => Animator?.Kick(-1.5f);
        void OnGotUp() => Animator?.Kick(2.2f);

        void OnSwitched(ShifterCharacter prev, ShifterCharacter next)
        {
            // CharacterManager plays the Wave/FistPump gesture on camera arrival; here: happy face + a little hop
            // (pure squash & stretch, the physics body never moves, so pressure plates stay pressed).
            if (next == null || next != Character || !AutoGreet || Animator == null) return;
            if (EffectivePose != PoseOverride.None) return;
            Animator.Kick(2.6f);
            _happyUntil = Time.time + 1.8f;
        }

        void OnDamaged(ShifterCharacter c, float amount)
        {
            if (c == null || c != Character || Animator == null) return;
            _flash = amount >= 25f ? Expression.Angry : Expression.Surprised;
            CharacterDisguise.OnDamaged(this, amount);
            _flashUntil = Time.time + 0.5f;
            Animator.Kick(-1.6f);
        }

        protected virtual void OnDestroy()
        {
            Unsubscribe();
            Rig?.Mats?.Destroy();
        }

        // ------------------------------------------------------------------ per frame
        protected virtual void LateUpdate()
        {
            if (Rig == null || Animator == null) return;
            float dt = Time.deltaTime;
            var inp = BuildInput(dt);
            EffectivePose = inp.Pose;
            if (inp.Pose == PoseOverride.Bridge) Animator.SetBridge(_bridgeLen, _bridgeSag);
            IsScaredLook = ResolveScared();
            Animator.ExtraTremble = IsScaredLook && inp.Pose == PoseOverride.None ? ScaredTremble : 0f;
            Animator.Tick(dt, inp);
            SmoothSteps(dt, inp);
            var e = ResolveExpression(inp);
            CurrentExpression = e;
            Rig.Face.Set(e);
            Rig.Face.Tick(dt, ResolveLook());
            Juice(dt, inp);
        }

        AnimInput BuildInput(float dt)
        {
            var c = Character;
            var inp = AnimInput.Idle;
            Vector3 vel = Vector3.zero, angVel = Vector3.zero;
            bool grounded = true, sprint = false, tumbling = false; float air = 0f;
            if (MotionOverride.HasValue)
            {
                var m = MotionOverride.Value;
                vel = m.Velocity; grounded = m.Grounded; sprint = m.Sprinting; tumbling = m.Tumbling; air = m.AirTime; angVel = m.AngularVelocity;
            }
            else if (_motor != null)
            {
                vel = _motor.Velocity - _motor.GroundVelocity;
                var lp = _motor.LocalPlanarVelocity;
                if (lp.sqrMagnitude > 0.0001f || _motor.IsGrounded) { vel.x = lp.x; vel.z = lp.z; }
                // the stub/early motor may never report grounded: treat "no vertical motion, no air time" as grounded
                grounded = _motor.IsGrounded || (_motor.AirTime <= 0.0001f && Mathf.Abs(vel.y) < 1.5f);
                sprint = _motor.IsSprinting; tumbling = _motor.IsTumbling; air = _motor.AirTime;
                if (c != null && c.Body != null) angVel = c.Body.angularVelocity;
            }
            else if (dt > 0f)
            {
                var pos = transform.position;
                if (_hasPrevPos) vel = (pos - _prevPos) / dt;
                _prevPos = pos; _hasPrevPos = true;
            }
            if (c != null && (c.Condition == CharacterCondition.Thrown || c.Condition == CharacterCondition.Stunned)) tumbling = true;

            var planar = new Vector3(vel.x, 0f, vel.z);
            inp.Speed = planar.magnitude;
            inp.Forward = Vector3.Dot(planar, transform.forward) < -0.3f * inp.Speed ? -1f : 1f;
            inp.VerticalSpeed = vel.y;
            inp.Grounded = grounded; inp.Sprinting = sprint; inp.Tumbling = tumbling; inp.AirTime = air;
            inp.Turbo = sprint && Rig.Profile != null && Rig.Profile.HasSpeedPerk;
            inp.AngularVelocity = transform.InverseTransformDirection(angVel);
            inp.Upright = Vector3.Dot(transform.up, Vector3.up);
            var fwd = transform.forward; fwd.y = 0f;
            if (_hasPrevFwd && dt > 0f && fwd.sqrMagnitude > 0.01f) inp.TurnRate = Vector3.SignedAngle(_prevFwd, fwd, Vector3.up) / dt;
            if (fwd.sqrMagnitude > 0.01f) { _prevFwd = fwd; _hasPrevFwd = true; }
            inp.Stunned = c != null && c.Condition == CharacterCondition.Stunned;
            inp.Pose = ResolvePose(c);
            inp.AimPitch = AimPitch;
            if (inp.Pose == PoseOverride.Bridge)
            {
                if (!_bridgeExplicit && c != null && c.Bridge != null) { _bridgeLen = Tuning.BridgeLength; _bridgeSag = Mathf.Clamp01(c.Bridge.CurrentLoad / Mathf.Max(1f, c.Bridge.Capacity)); }
                inp.Strain01 = _bridgeSag;
            }
            else
            {
                _bridgeExplicit = false;
                if (c != null && c.Grabber != null && c.Grabber.IsHolding) inp.Strain01 = Mathf.Clamp01(c.Grabber.CarriedMass / Mathf.Max(1f, c.Grabber.Capacity));
            }
            return inp;
        }

        PoseOverride ResolvePose(ShifterCharacter c)
        {
            if (CurrentPose != PoseOverride.None) return CurrentPose;
            if (c == null) return PoseOverride.None;
            if (c.State == CharacterState.Downed) return PoseOverride.KO;
            if (c.State == CharacterState.Captured) return PoseOverride.Captured;
            switch (c.Condition)
            {
                case CharacterCondition.Carried: return PoseOverride.Carried;
                case CharacterCondition.Bridging: return PoseOverride.Bridge;
                case CharacterCondition.Flopped: return PoseOverride.Flop;
                case CharacterCondition.Seated: return PoseOverride.Seated;
            }
            if (c.Grabber != null && c.Grabber.IsHolding) return PoseOverride.Carrying;
            // RMB / scripted aim AND the short hip-aim window after every shot (WeaponHolder raises the gun arm
            // onto its stable shot line after this animator pass)
            if (c.Weapon != null && c.Weapon.WantsAimPose) return PoseOverride.Aiming;
            return PoseOverride.None;
        }

        Expression ResolveExpression(in AnimInput inp)
        {
            if (_manualOn)
            {
                if (_manualUntil < 0f || Time.time < _manualUntil) return _manual;
                _manualOn = false;
            }
            var c = Character;
            if (c != null)
            {
                if (c.State == CharacterState.Downed) return Expression.KO;
                if (c.State == CharacterState.Captured) return Expression.Scared;
                if (c.State == CharacterState.Lost) return Expression.Sad;
            }
            if (Animator.IsGesturing) return CharacterGestures.Face(Animator.CurrentGesture);
            if (Time.time < _flashUntil) return _flash;
            switch (inp.Pose)
            {
                case PoseOverride.Carried: return Mathf.Repeat(Time.time, 2.4f) < 1.6f ? Expression.Scared : Expression.Surprised;
                case PoseOverride.Bridge: return inp.Strain01 > 0.45f ? Expression.Strain : Expression.Determined;
                case PoseOverride.Hacking: case PoseOverride.Aiming: return Expression.Determined;
                case PoseOverride.Carrying: return inp.Strain01 > 0.6f ? Expression.Strain : Expression.Determined;
                case PoseOverride.Flop: return Expression.Dizzy;
                case PoseOverride.KO: return Expression.KO;
                case PoseOverride.Captured: return Expression.Scared;
            }
            if (c != null && c.Condition == CharacterCondition.Thrown) return inp.VerticalSpeed > 0f ? Expression.Happy : Expression.Scared;
            if (inp.Stunned) return Expression.Dizzy;
            if (inp.Tumbling) return Expression.Scared;
            if (!inp.Grounded && inp.AirTime > 0.45f && inp.VerticalSpeed < -9f) return Expression.Scared;
            if (IsScaredLook) return Expression.Scared;
            if (Time.time < _happyUntil) return Expression.Happy;
            if (inp.Sprinting && inp.Speed > 3f) return Expression.Determined;
            if (c != null && c.IsAlive && c.Health < 30f) return Expression.Sad;
            return Expression.Neutral;
        }

        float _stepOffset, _lastRootY; bool _hasRootY;

        /// <summary>The motor's step-up is a one-frame lift: absorb it into the model and ease it out (~0.1 s).</summary>
        void SmoothSteps(float dt, in AnimInput inp)
        {
            float y = transform.position.y;
            if (_hasRootY && inp.Grounded && inp.Pose == PoseOverride.None && !inp.Tumbling)
            {
                float dy = y - _lastRootY;
                if (_motor != null) dy -= _motor.GroundVelocity.y * dt;   // riding a lift is not a step
                if (dy > 0.06f && dy < 0.45f && inp.VerticalSpeed < 3f) _stepOffset -= dy;
            }
            else _stepOffset = 0f;
            _lastRootY = y; _hasRootY = true;
            _stepOffset = Mathf.MoveTowards(_stepOffset * Mathf.Exp(-14f * dt), 0f, dt * 0.3f);
            if (_stepOffset < -0.001f && Rig.Root != null) Rig.Root.localPosition += Vector3.up * _stepOffset;
        }

        Vector3? ResolveLook()
        {
            if (LookTarget.HasValue) return LookTarget;
            var c = Character;
            var mgr = CharacterManager.Instance;
            if (mgr != null && mgr.Active != null && mgr.Active != c) return mgr.Active.HeadTop;
            _camRefresh -= Time.unscaledDeltaTime;
            if (_cam == null || _camRefresh <= 0f) { var cm = Camera.main; _cam = cm != null ? cm.transform : null; _camRefresh = 1f; }
            return _cam != null ? _cam.position : (Vector3?)null;
        }

        void Juice(float dt, in AnimInput inp)
        {
            var c = Character;
            var state = c != null ? c.State : CharacterState.Alive;
            if (state != _lastState)
            {
                if (state == CharacterState.Lost) SetDesaturated(true);
                else if (_lastState == CharacterState.Lost) SetDesaturated(false);
                _lastState = state;
            }
            if (state == CharacterState.Alive && inp.Stunned && !inp.Tumbling)
            {
                _starsTimer -= dt;
                if (_starsTimer <= 0f) { Fx.Stars(Head, 2.5f); _starsTimer = 2.5f; }
            }
            else _starsTimer = 0f;
            if (inp.Turbo && inp.Grounded && inp.Speed > 8.5f)
            {
                _speedLinesTimer -= dt;
                if (_speedLinesTimer <= 0f) { Fx.SpeedLines(transform, 0.35f); _speedLinesTimer = 0.3f; }
            }
        }
    }
}
