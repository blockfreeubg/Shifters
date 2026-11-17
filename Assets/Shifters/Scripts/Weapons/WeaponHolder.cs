// =====================================================================================
//  SHIFTERS - WeaponHolder.cs   [CONTRACT API - implementation owned by the WEAPONS agent]
//  On every character. Only one character owns the gun at a time. LMB fire / RMB aim when the
//  owner is active. Drops a WeaponPickup when the owner goes Downed/Captured/Lost (respawning
//  at the last checkpoint if it fell into the void) so the team can never lose the gun for good.
//  Keeps Profile.Weapon in sync (null when not holding).
//
//  Shots never leave from wherever the animated gun happens to hang: every shot (hip-fire, aimed,
//  FireAt, the first shot of a burst) travels along the STABLE line fire pivot -> target
//  (WeaponAim: 0.8 x body height, gun side), and the muzzle sits on that line. Firing opens a short
//  "hip-aim" window: the gun snaps up onto the line the same frame (WeaponArmPose, after the
//  CharacterAnimator - hence the late execution order), the active player's body turns toward the
//  shot, the visual shows the Aiming pose. RMB additionally zooms the over-the-shoulder camera.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    [DefaultExecutionOrder(100)] // Update after PlayerBrain (shot facing), LateUpdate after CharacterVisual (arm pose)
    public class WeaponHolder : MonoBehaviour, ICharacterModule
    {
        public ShifterCharacter Character { get; protected set; }
        public WeaponData Data { get; protected set; }
        public bool HasWeapon => Data != null;
        public bool IsAiming { get; protected set; }

        // ------------------------------------------------------------------ additions (WEAPONS agent)
        /// <summary>Raised after Equip / Drop / TakeWeapon on any holder (HUD, switcher, perspective listen).</summary>
        public static event Action<WeaponHolder> WeaponChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => WeaponChanged = null;

        /// <summary>Raised on every shot with the aim point (animation/fx hooks).</summary>
        public event Action<WeaponHolder, Vector3> Fired;

        /// <summary>Hip-aim window after a shot: at least this long (s)...</summary>
        public const float HipAimMinHold = 0.6f;
        /// <summary>...and at most this long (slow guns keep the gun up until the next shot is ready).</summary>
        public const float HipAimMaxHold = 1.6f;
        /// <summary>RMB raise time and the lower-back time of the gun arm (s). Shots snap it up instantly.</summary>
        public const float AimRaiseTime = 0.12f, AimLowerTime = 0.25f;
        /// <summary>Relaxed "low ready" carry: the barrel is tilted this far up from the hanging arm (points forward-down
        /// instead of into the floor). Blends out while the gun is raised.</summary>
        public const float CarryTilt = 65f;

        public WeaponKind Kind => WeaponStyle.KindOf(Data);
        /// <summary>The equipped model (child of the hand socket) or null.</summary>
        public GameObject Model { get; protected set; }
        /// <summary>The visible gun's muzzle transform (child of the model) or null.</summary>
        public Transform Muzzle { get; protected set; }
        /// <summary>Where a shot toward the current aim leaves from: the muzzle point on the stable shot line
        /// (pivot -> AimPoint while aiming / just fired / controlled, else straight ahead). Use this - not the hanging
        /// model's muzzle - for line-of-fire reasoning. The visible gun's muzzle is GunMuzzlePosition.</summary>
        public Vector3 MuzzlePosition => Character != null ? MuzzlePoint(CurrentAimTarget()) : (Muzzle != null ? Muzzle.position : transform.position);
        /// <summary>World position of the visible gun's muzzle (== MuzzlePosition while the gun is raised).</summary>
        public Vector3 GunMuzzlePosition => Muzzle != null ? Muzzle.position : MuzzlePosition;
        public float LastFireTime { get; protected set; } = -999f;
        public int ShotsFired { get; protected set; }
        public bool IsReady => HasWeapon && Time.time >= _nextFireTime;
        /// <summary>Crosshair point of the last frame the owner was controlled (or the last FireAt / AimAt target).</summary>
        public Vector3 AimPoint { get; protected set; }

        /// <summary>True during the short hip-aim window after a shot (gun up on the shot line, no camera zoom).</summary>
        public bool IsHipAiming => HasWeapon && Time.time < _hipAimUntil;
        /// <summary>The owner shows the Aiming pose (RMB / scripted aim or the hip-aim window). CharacterVisual reads this.</summary>
        public bool WantsAimPose => HasWeapon && (IsAiming || IsHipAiming) && Character != null && Character.IsAlive
                                    && Character.Condition == CharacterCondition.Normal;
        /// <summary>0..1 blend of the raised gun arm actually applied this frame (tests/debug).</summary>
        public float AimPoseWeight => _aimW;
        /// <summary>Stable fire geometry of this owner + equipped gun (recomputed on Equip).</summary>
        public WeaponFireGeometry FireGeometry { get { EnsureGeometry(); return _geo; } }
        /// <summary>Shot line of the last shot: pivot (inside the body), muzzle point (on the line) and direction.</summary>
        public Vector3 LastShotPivot { get; protected set; }
        public Vector3 LastShotMuzzle { get; protected set; }
        public Vector3 LastShotDirection { get; protected set; } = Vector3.forward;

        /// <summary>The holder that currently owns the gun (null if it lies on the floor as a pickup).</summary>
        public static WeaponHolder CurrentOwner
        {
            get
            {
                var all = ShifterCharacter.All;
                for (int i = 0; i < all.Count; i++) { var c = all[i]; if (c != null && c.Weapon != null && c.Weapon.HasWeapon) return c.Weapon; }
                return null;
            }
        }

        float _nextFireTime, _noiseCooldown, _recoil, _safeProbeTimer, _hipAimUntil, _aimW, _aimPointTime = -999f, _geoHeight;
        int _shotFrame = -1;
        Vector3 _modelBasePos;
        Quaternion _modelBaseRot = Quaternion.identity;
        bool _crosshairShown, _cameraAiming, _subscribed, _scriptedAim, _armPosed, _geoValid, _inHandSocket;
        WeaponFireGeometry _geo;
        System.Random _rng = new System.Random(1337);
        readonly Vector3[] _safeHistory = new Vector3[4];
        int _safeCount, _safeNext;
        static readonly string[] PickupLines = { "Ooh, shiny!", "I am the captain now.", "Finally, a promotion.", "Is this... loaded?", "Mine now." };

        // ------------------------------------------------------------------ lifecycle
        public virtual void OnCharacterInitialized(ShifterCharacter c)
        {
            Character = c;
            _rng = new System.Random(4242 + (c != null ? c.Slot * 97 : 0));
            if (c != null && !_subscribed) { c.StateChanged += OnOwnerStateChanged; c.ConditionChanged += OnOwnerConditionChanged; _subscribed = true; }
            _geoValid = false;
            if (c != null && c.Profile != null && c.Profile.HasWeapon) Equip(WeaponCustody.SanitizeProfileGun(c.Profile.Weapon));
        }

        protected virtual void OnDestroy()
        {
            if (Character != null && _subscribed) { Character.StateChanged -= OnOwnerStateChanged; Character.ConditionChanged -= OnOwnerConditionChanged; }
            _subscribed = false;
            ReleaseAimAndCrosshair();
        }

        protected virtual void OnDisable() => ReleaseAimAndCrosshair();

        /// <summary>After a (re)spawn: if the team lost its gun (dropped before a reload), hand it back.</summary>
        protected virtual void Start() => WeaponCustody.EnsureTeamArmed();

        // ------------------------------------------------------------------ equip / drop
        public virtual void Equip(WeaponData data)
        {
            if (data == null) { TakeWeapon(); return; }
            ClearModel();
            Data = data;
            if (Character != null && Character.Profile != null) Character.Profile.Weapon = data;
            WeaponCustody.Remember(data);
            WeaponPickup.RemovePickupsOf(data); // never two copies of the same gun
            BuildModel();
            _nextFireTime = Time.time + 0.15f;
            WeaponChanged?.Invoke(this);
        }

        /// <summary>Equip with pickup feedback (sound, poof, toast, quip).</summary>
        public void EquipFromPickup(WeaponData data)
        {
            Equip(data);
            if (Character == null || data == null) return;
            Sfx.At(SfxId.Pickup, Character.Center);
            Fx.Poof(GunMuzzlePosition, data.Tint, 0.5f);
            Character.Say(PickupLines[(Character.Slot + ShotsFired + 3) % PickupLines.Length]);
            GameEvents.RaiseToast(Character.DisplayName + " got the " + data.Name + "!", UIColors.Weapon);
        }

        /// <summary>Drop as a world pickup at the character's position. Returns the dropped data.</summary>
        public virtual WeaponData Drop()
        {
            var d = Data;
            if (d == null) return null;
            bool lost = Character != null && Character.State == CharacterState.Lost;
            Vector3 at = Character != null ? Character.Center : transform.position;
            Vector3? safe = lost ? SafeRespawnPoint() : (Vector3?)null;
            ReleaseAimAndCrosshair();
            ClearModel();
            Data = null;
            if (Character != null && Character.Profile != null) Character.Profile.Weapon = null;
            WeaponPickup.SpawnDropped(d, at, safe);
            WeaponChanged?.Invoke(this);
            return d;
        }

        /// <summary>Remove the weapon WITHOUT spawning a pickup (hand-overs, checkpoint restore). Returns it.</summary>
        public virtual WeaponData TakeWeapon()
        {
            var d = Data;
            ReleaseAimAndCrosshair();
            ClearModel();
            Data = null;
            if (Character != null && Character.Profile != null) Character.Profile.Weapon = null;
            if (d != null) WeaponChanged?.Invoke(this);
            return d;
        }

        /// <summary>Programmatic fire toward a world point (test bots). Respects fire rate. Leaves from the stable shot
        /// line and raises the gun (hip-aim window) exactly like a player shot.</summary>
        public virtual bool FireAt(Vector3 worldTarget) => TryFire(worldTarget);

        /// <summary>Scripted aim for bots / tests / cutscenes: hold the aim pose toward a world point (null = stop).
        /// Like holding RMB (aim pose; the aim camera only if this is the active character). FireAt still fires.</summary>
        public void AimAt(Vector3? worldPoint)
        {
            _scriptedAim = worldPoint.HasValue && HasWeapon;
            if (worldPoint.HasValue) SetAimPoint(worldPoint.Value);
        }

        // ------------------------------------------------------------------ shot geometry (public: bots, level QA)
        /// <summary>Start of the shot line toward target: a stable point inside the body (0.8 x height, gun side).</summary>
        public Vector3 FirePivot(Vector3 target)
        {
            if (Character == null) return transform.position;
            EnsureGeometry();
            if (!IsUpright) return Character.Center;
            WeaponAim.AimFrame(Character.Feet, transform.forward, target, out _, out var right);
            return WeaponAim.Pivot(_geo, Character.Feet, right);
        }

        /// <summary>Muzzle point of a shot toward target (on the shot line, MuzzleDistance in front of the pivot): where
        /// projectiles spawn and tracers / muzzle flashes start.</summary>
        public Vector3 MuzzlePoint(Vector3 target)
        {
            Vector3 pivot = FirePivot(target);
            return pivot + WeaponAim.Direction(pivot, target, transform.forward) * (IsUpright ? _geo.MuzzleDistance : 0.5f);
        }

        /// <summary>Centre-line trace of a shot toward target exactly as fired (no spread). True if something is hit within
        /// range; hit = first non-trigger collider (own body skipped). Line-of-fire checks for bots and level QA.</summary>
        public bool TraceShot(Vector3 target, out RaycastHit hit)
        {
            Vector3 pivot = FirePivot(target);
            float range = (Data != null ? Data.Range : 30f) + _geo.MuzzleDistance;
            return WeaponBallistics.Raycast(pivot, WeaponAim.Direction(pivot, target, transform.forward), range, transform, out hit);
        }

        bool IsUpright => Vector3.Dot(transform.up, Vector3.up) > 0.75f;

        void EnsureGeometry()
        {
            float h = Character != null ? Character.Height : 0f;
            if (_geoValid && Mathf.Approximately(h, _geoHeight)) return;
            _geo = WeaponAim.Measure(Character, Muzzle != null ? MuzzleInSocket() : WeaponAim.DefaultMuzzleInSocket);
            _geoHeight = h;
            _geoValid = true;
        }

        /// <summary>Muzzle of the built model in socket space (rest pose, no recoil).</summary>
        Vector3 MuzzleInSocket()
        {
            if (Model == null || Muzzle == null) return WeaponAim.DefaultMuzzleInSocket;
            var mt = Model.transform;
            Vector3 local = Vector3.Scale(mt.InverseTransformPoint(Muzzle.position), mt.localScale);
            return _modelBasePos + _modelBaseRot * local;
        }

        Vector3 CurrentAimTarget()
        {
            if (IsAiming || IsHipAiming || Time.time - _aimPointTime < 0.5f) return AimPoint;
            EnsureGeometry();
            return Character.Feet + transform.forward * 20f + Vector3.up * _geo.PivotHeight;
        }

        void SetAimPoint(Vector3 p) { AimPoint = p; _aimPointTime = Time.time; }

        // ------------------------------------------------------------------ state reactions
        void OnOwnerStateChanged(ShifterCharacter c)
        {
            if (c == null || c.IsAlive) return;
            if (HasWeapon) Drop(); else ReleaseAimAndCrosshair();
        }

        void OnOwnerConditionChanged(ShifterCharacter c)
        {
            if (c != null && c.Condition != CharacterCondition.Normal) { SetAiming(false); SyncCameraAim(); _hipAimUntil = 0f; }
        }

        // ------------------------------------------------------------------ per-frame
        protected virtual void Update()
        {
            if (Character == null) return;
            float dt = Time.deltaTime;
            _noiseCooldown -= dt;
            SampleSafeGround(dt);

            bool able = HasWeapon && Character.IsAlive && Character.Condition == CharacterCondition.Normal
                        && (Character.Grabber == null || !Character.Grabber.IsHolding);
            bool controlling = HasWeapon && Character.IsActive && !Character.ExternalControl && Character.IsAlive
                               && ShiftersInput.Context == InputContext.Gameplay && !TimeController.IsPaused;
            bool canUse = controlling && able;
            if (!HasWeapon) _scriptedAim = false;

            SetCrosshair(controlling);
            SetAiming((canUse && ShiftersInput.AimHeld) || (_scriptedAim && able));
            SyncCameraAim();
            if (controlling) SetAimPoint(ComputeAimPoint());
            if (canUse && (ShiftersInput.FireHeld || ShiftersInput.FirePressed))
            {
                TryFire(AimPoint);
                if (IsHipAiming) _hipAimUntil = Mathf.Max(_hipAimUntil, Time.time + 0.25f); // trigger held: keep the gun up
            }
            else if (controlling && !canUse && ShiftersInput.FirePressed) Sfx.At(SfxId.Empty, MuzzlePosition, 0.6f);
            if (canUse && !IsAiming && IsHipAiming) FaceShotDirection();
            if (WantsAimPose && Character.Visual != null) Character.Visual.AimPitch = PitchOf(AimPoint);
        }

        protected virtual void LateUpdate()
        {
            float dt = Time.deltaTime;
            // the shot frame renders the gun exactly where the muzzle flash / tracer start; the kick follows next frame
            bool shotFrame = _shotFrame == Time.frameCount;
            if (!shotFrame) _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 6f);
            float recoil = shotFrame ? 0f : _recoil;
            UpdateArmPose(dt);
            if (Model == null) return;
            var t = Model.transform;
            t.localPosition = _modelBasePos + new Vector3(0f, 0f, -WeaponStyle.Recoil(Kind) * recoil);
            Quaternion kick = Quaternion.Euler(-14f * recoil, 0f, 0f);
            // relaxed: low-ready carry (barrel forward-down); raised: the hand already points the barrel along the shot line
            Quaternion carry = _inHandSocket ? Quaternion.Euler(-CarryTilt * (1f - _aimW), 0f, 0f) : Quaternion.identity;
            if (_armPosed) { t.localRotation = _modelBaseRot * carry * kick; return; }
            if (WantsAimPose && t.parent != null)
            {
                // no rig to pose (fallback socket): at least point the barrel along the shot line
                Vector3 to = AimPoint - t.position;
                if (to.sqrMagnitude > 0.25f)
                {
                    Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up) * kick;
                    t.rotation = Quaternion.Slerp(t.rotation, want, 1f - Mathf.Exp(-25f * dt));
                    return;
                }
            }
            t.localRotation = Quaternion.Slerp(t.localRotation, _modelBaseRot * carry * kick, 1f - Mathf.Exp(-15f * dt));
        }

        /// <summary>Raise / lower the gun arm onto the shot line toward AimPoint (blended; shots snap it up).</summary>
        void UpdateArmPose(float dt)
        {
            _armPosed = false;
            var v = Character != null ? Character.Visual : null;
            var rig = v != null ? v.Rig : null;
            if (rig == null || Model == null) { _aimW = 0f; return; }
            var pose = v.EffectivePose;
            if (pose != PoseOverride.Aiming && pose != PoseOverride.None) { _aimW = 0f; return; } // carry, KO, bridge...
            float target = WantsAimPose && IsUpright ? 1f : 0f;
            _aimW = Mathf.MoveTowards(_aimW, target, dt / (target > _aimW ? AimRaiseTime : AimLowerTime));
            if (_aimW <= 0.001f) { _aimW = 0f; return; }
            EnsureGeometry();
            Vector3 feet = Character.Feet, aim = AimPoint;
            WeaponAim.AimFrame(feet, transform.forward, aim, out var fwd, out var right);
            Vector3 pivot = WeaponAim.Pivot(_geo, feet, right);
            WeaponAim.SocketPose(_geo, pivot, WeaponAim.Direction(pivot, aim, fwd), out var socketPos, out var socketRot);
            _armPosed = WeaponArmPose.Apply(rig, transform.forward, fwd, right, socketPos, socketRot, _aimW);
        }

        float PitchOf(Vector3 target)
        {
            Vector3 d = WeaponAim.Direction(FirePivot(target), target, transform.forward);
            return Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Active player hip-firing: turn the body toward the shot (PlayerBrain only faces the camera while aiming).</summary>
        void FaceShotDirection()
        {
            var motor = Character.Motor;
            if (motor == null) return;
            Vector3 f = AimPoint - Character.Feet; f.y = 0f;
            if (f.sqrMagnitude < 2.25f) { var cam = ShiftersCamera.Instance; f = cam != null ? cam.FlatForward : transform.forward; f.y = 0f; }
            if (f.sqrMagnitude < 1e-4f) return;
            var cmd = motor.CurrentCommand;
            cmd.Face = f.normalized;
            cmd.Jump = false; // PlayerBrain already registered this frame's jump press
            motor.SetCommand(cmd);
        }

        // ------------------------------------------------------------------ aiming / crosshair
        void SetAiming(bool on)
        {
            if (IsAiming == on) return;
            IsAiming = on;
            if (on && Character != null && Character.IsActive && !Character.ExternalControl) GameEvents.RaiseCrosshair(true);
        }

        /// <summary>The over-the-shoulder aim camera follows IsAiming of the ACTIVE character only.</summary>
        void SyncCameraAim()
        {
            bool want = IsAiming && Character != null && Character.IsActive;
            if (want == _cameraAiming) return;
            var cam = ShiftersCamera.Instance;
            if (cam != null) cam.IsAiming = want;
            _cameraAiming = want && cam != null;
        }

        void SetCrosshair(bool visible)
        {
            if (_crosshairShown == visible) return;
            _crosshairShown = visible;
            GameEvents.RaiseCrosshair(visible);
        }

        void ReleaseAimAndCrosshair()
        {
            _scriptedAim = false;
            _hipAimUntil = 0f;
            SetAiming(false);
            SyncCameraAim();
            SetCrosshair(false);
        }

        Vector3 ComputeAimPoint()
        {
            float range = Data != null ? Data.Range : 30f;
            var cam = ShiftersCamera.Instance;
            if (cam != null)
            {
                Ray r = cam.AimRay;
                float skip = Mathf.Max(0f, Vector3.Dot(Character.Center - r.origin, r.direction) - 0.4f);
                float maxD = range + skip + 2f;
                if (WeaponBallistics.Raycast(r.origin, r.direction, maxD, transform, out var hit, skip)) return hit.point;
                return r.GetPoint(maxD);
            }
            return Character.Feet + Vector3.up * FireGeometry.PivotHeight + transform.forward * range; // straight ahead
        }

        // ------------------------------------------------------------------ firing
        protected virtual bool TryFire(Vector3 target)
        {
            if (!HasWeapon || Time.time < _nextFireTime) return false;
            if (Character != null && !Character.IsAlive) return false;
            var d = Data;
            var kind = Kind;
            _nextFireTime = Time.time + 1f / Mathf.Max(0.1f, d.FireRate);
            LastFireTime = Time.time;
            ShotsFired++;
            SetAimPoint(target);
            _hipAimUntil = Mathf.Max(_hipAimUntil, Time.time + Mathf.Clamp(1f / Mathf.Max(0.1f, d.FireRate) + 0.25f, HipAimMinHold, HipAimMaxHold));
            _aimW = 1f;    // snap the gun up: this frame's render already shows it on the shot line
            _recoil = 1f;
            _shotFrame = Time.frameCount;

            // the stable shot line: pivot (inside the body) -> target; the muzzle sits on it
            EnsureGeometry();
            Vector3 pivot = FirePivot(target);
            Vector3 dir = WeaponAim.Direction(pivot, target, transform.forward);
            float muzzleDist = IsUpright ? _geo.MuzzleDistance : 0.5f;
            Vector3 muzzle = pivot + dir * muzzleDist;
            LastShotPivot = pivot; LastShotMuzzle = muzzle; LastShotDirection = dir;
            if (Character != null && Character.Visual != null) Character.Visual.AimPitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

            bool continuous = WeaponStyle.IsContinuous(kind);
            try
            {
                if (WeaponStyle.IsProjectile(kind)) FireProjectile(kind, d, pivot, dir, muzzleDist, target);
                else if (WeaponStyle.IsCampaignKind(kind)) WeaponTools.Fire(this, kind, d, pivot, dir, muzzle, muzzleDist);
                else FireHitscan(kind, d, pivot, dir, muzzle, muzzleDist);
            }
            catch (Exception e) { Debug.LogException(e); }

            if (!continuous) Fx.MuzzleFlash(muzzle, dir, d.Tint);
            if (!continuous || ShotsFired % 2 == 1) Sfx.At(WeaponStyle.FireSfx(kind), muzzle, continuous ? 0.75f : 1f, 0.94f + (float)_rng.NextDouble() * 0.12f);
            if (Character != null && Character.IsActive) ShiftersCamera.Instance?.Shake(WeaponStyle.ShakeAmplitude(kind), WeaponStyle.ShakeDuration(kind));
            float noise = WeaponStyle.NoiseRadius(kind);
            if (_noiseCooldown <= 0f && noise > 0f) { GameEvents.RaiseNoise(muzzle, noise, Character, 1f); _noiseCooldown = 0.3f; }
            var session = GameSession.Instance;
            if (session != null && session.Stats != null) session.Stats.ShotsFired++;
            Fired?.Invoke(this, target);
            return true;
        }

        /// <summary>Rays start at the pivot inside the body (point-blank targets and walls right in front are hit), the
        /// tracer starts at the muzzle.</summary>
        void FireHitscan(WeaponKind kind, WeaponData d, Vector3 pivot, Vector3 dir, Vector3 muzzle, float muzzleDist)
        {
            int pellets = kind == WeaponKind.Shotgun ? Mathf.Max(1, d.Pellets) : 1;
            float share = 1f / pellets;
            float range = d.Range + muzzleDist;
            Color tracer = WeaponStyle.TracerColor(kind);
            for (int p = 0; p < pellets; p++)
            {
                Vector3 pd = WeaponBallistics.Spread(dir, d.SpreadDegrees, _rng);
                bool hit = WeaponBallistics.Raycast(pivot, pd, range, transform, out var h);
                Vector3 end = hit ? h.point : pivot + pd * range;
                if (!hit || h.distance > muzzleDist + 0.05f) Fx.Tracer(muzzle, end, tracer, WeaponStyle.TracerWidth(kind), WeaponStyle.TracerDuration(kind));
                if (!hit) continue;
                var shot = new ShotInfo
                {
                    Point = h.point, Normal = h.normal, Direction = pd, Damage = d.Damage, Knockback = d.Knockback * share,
                    Weapon = d.Type, Variant = d.Variant, Shooter = Character, Collider = h.collider,
                };
                WeaponBallistics.ApplyHit(shot, kind == WeaponKind.Plunger, share, d.FireRate);
                WeaponBallistics.ImpactFx(h.point, h.normal, h.collider, kind, d.Tint);
            }
        }

        /// <summary>Projectiles spawn at the muzzle point (or just before whatever blocks pivot -> muzzle) and arc onto
        /// the target from there.</summary>
        void FireProjectile(WeaponKind kind, WeaponData d, Vector3 pivot, Vector3 dir, float muzzleDist, Vector3 target)
        {
            float radius = WeaponProjectile.RadiusOf(kind);
            Vector3 spawn = pivot + dir * muzzleDist;
            if (WeaponBallistics.Raycast(pivot, dir, muzzleDist + radius, transform, out var h))
                spawn = pivot + dir * Mathf.Max(0.05f, h.distance - radius - 0.03f);
            float g = Mathf.Max(1f, -Physics.gravity.y);
            float speed = Mathf.Sqrt(Mathf.Max(WeaponGenerator.MinRange, d.Range) * g); // 45-degree reach == Range (lobbed kinds: sqrt(Range*g))
            Vector3 v = WeaponBallistics.BallisticVelocity(spawn, target, speed, g);
            v = WeaponBallistics.Spread(v, d.SpreadDegrees, _rng).normalized * speed;
            WeaponProjectile.Launch(kind, spawn, v, d, Character);
        }

        // ------------------------------------------------------------------ model
        void BuildModel()
        {
            Transform socket = ResolveSocket();
            _inHandSocket = Character != null && Character.Visual != null && socket == Character.Visual.WeaponSocket; // barrel hangs along the arm
            GameObject model = null;
            try { model = WeaponModelBuilder.Build(Data, socket); }
            catch (Exception e) { Debug.LogException(e); }
            if (model == null) { model = new GameObject("Weapon_" + Data.Type); model.transform.SetParent(socket, false); }
            if (model.GetComponentInChildren<Renderer>() == null) WeaponFallbackModel.Build(Data, model.transform);
            foreach (var col in model.GetComponentsInChildren<Collider>()) Kit.SafeDestroy(col); // models are visual only
            Model = model;
            _modelBasePos = model.transform.localPosition;
            _modelBaseRot = model.transform.localRotation;
            Muzzle = FindDeep(model.transform, "Muzzle");
            if (Muzzle == null)
            {
                Muzzle = new GameObject("Muzzle").transform;
                Muzzle.SetParent(model.transform, false);
                Muzzle.localPosition = new Vector3(0f, 0f, 0.4f);
            }
            _geoValid = false;
            EnsureGeometry();
        }

        void ClearModel()
        {
            if (Model != null) Kit.SafeDestroy(Model);
            Model = null;
            Muzzle = null;
            _aimW = 0f;
            _armPosed = false;
            _geoValid = false;
        }

        Transform ResolveSocket()
        {
            var v = Character != null ? Character.Visual : null;
            if (v != null && v.WeaponSocket != null) return v.WeaponSocket;
            if (v != null && v.HandR != null) return v.HandR;
            var existing = transform.Find("WeaponSocketFallback");
            if (existing != null) return existing;
            var s = new GameObject("WeaponSocketFallback").transform;
            s.SetParent(transform, false);
            float h = Character != null ? Character.Height : 1.8f, r = Character != null ? Character.Radius : 0.4f;
            s.localPosition = new Vector3(r + 0.12f, h * WeaponAim.PivotHeightFraction, 0.25f);
            return s;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        // ------------------------------------------------------------------ safe ground memory (void respawn)
        void SampleSafeGround(float dt)
        {
            _safeProbeTimer -= dt;
            if (_safeProbeTimer > 0f) return;
            _safeProbeTimer = 0.5f;
            if (!Character.IsAlive || Character.Condition != CharacterCondition.Normal) return;
            Vector3 o = Character.Feet + Vector3.up * 0.4f;
            if (WeaponBallistics.Raycast(o, Vector3.down, 0.9f, transform, out var h) && h.collider.GetComponentInParent<ShifterCharacter>() == null && h.normal.y > 0.7f)
            {
                _safeHistory[_safeNext] = h.point;
                _safeNext = (_safeNext + 1) % _safeHistory.Length;
                _safeCount = Mathf.Min(_safeCount + 1, _safeHistory.Length);
            }
        }

        /// <summary>Where a gun lost to the void should reappear: checkpoint fallback, else ground the owner stood on ~2 s ago.</summary>
        Vector3? SafeRespawnPoint()
        {
            if (WeaponPickup.FallbackSpawn.HasValue) return WeaponPickup.FallbackSpawn.Value;
            if (_safeCount == 0) return null;
            int oldest = _safeCount < _safeHistory.Length ? 0 : _safeNext;
            return _safeHistory[oldest];
        }
    }
}
