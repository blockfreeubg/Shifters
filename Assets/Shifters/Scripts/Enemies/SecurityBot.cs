// =====================================================================================
//  SHIFTERS - SecurityBot.cs   [owned by the ENEMIES agent]
//  Campaign: IEnemyReactions (Blind/WRAPPED, Shove, Lure), IFreezable (CAPS-LOCK), ISwappable (SWAP-O-MATIC),
//  IResizable (BIT-SHIFTER: TINY 8 s), OnBreak (GuardBot coffee break), pool-gun OnShot dispatch.
//  "BONK-BOT 3000": patrols waypoints inside a circular arena, investigates noises, chases
//  what it sees, grabs a character (Capture), carries it to its HoldingCell with a siren and
//  locks it up. Stunned by gunfire (IShootable), thrown characters/props (>6 m/s) or launches.
//  Never damages anyone. Build with EnemyKit.SecurityBot(...).
//  States: Patrol -> (noise) Investigate -> (sight) Chase -> (grab) Capture -> Return -> Patrol;
//          any -> Stunned (drops a carried prisoner: rescued!).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public enum BotState { Patrol, Investigate, Chase, Capture, Return, Stunned }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class SecurityBot : MonoBehaviour, IShootable, IEnemyReactions, ISwappable, IResizable
    {
        /// <summary>All bots in the scene (registry).</summary>
        public static readonly List<SecurityBot> All = new List<SecurityBot>();
        static PhysicsMaterial _frictionless;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); _frictionless = null; BlowerShovesItself = true; }

        public const float SenseInterval = 0.1f;
        public const float Mass = 150f;

        [Header("Wiring (EnemyKit / level builders) - points are in the PARENT's local space")]
        public Vector3[] Waypoints = new Vector3[0];
        [Tooltip("HoldingCell.Id this bot delivers prisoners to. Empty/missing => captures become a KO at home.")]
        public string CellId = "";
        public Vector3 ArenaCenter;
        [Tooltip("Bot never leaves this circle while hunting (<= 0: unlimited).")] public float ArenaRadius;

        [Header("Tuning")]
        public float PatrolSpeed = Tuning.BotPatrolSpeed;
        public float ChaseSpeed = Tuning.BotChaseSpeed;
        public float SightRange = Tuning.BotSightRange;
        public float Fov = Tuning.BotFov;
        public float CaptureRadius = Tuning.BotCaptureRadius;
        public float StunDuration = Tuning.BotStunTime;
        public float LoseInterestTime = Tuning.BotLoseInterestTime;
        public float InvestigateLookTime = 3f;
        public float WaypointPause = 0.8f;
        [Tooltip("Thrown characters / props hitting the bot faster than this stun it.")] public float ImpactStunSpeed = 6f;
        [Tooltip("Targets standing this much higher than the bot are unreachable: it waits below, beeping angrily.")] public float UnreachableHeight = 1.3f;
        public float AlertPause = 0.35f;

        [Header("Anchors (EnemyKit)")]
        public Transform GrabPoint;
        public Transform EyePoint;
        public Transform HeadAnchor;
        public Transform ModelRoot;
        public Color Accent = new Color(1f, 0.54f, 0.12f);

        // ------------------------------------------------------------------ readable state
        public BotState State { get; private set; } = BotState.Patrol;
        public ShifterCharacter Target { get; private set; }
        public ShifterCharacter Carried { get; private set; }
        public bool IsStunned => State == BotState.Stunned;
        public bool IsAlerted => State == BotState.Chase || State == BotState.Capture;
        public bool IsWaitingBelow { get; private set; }
        public bool InAlertPause => _alertTimer > 0f;
        public float StunRemaining => State == BotState.Stunned ? Mathf.Max(0f, _stunTimer) : 0f;
        public float StateTime => _stateTime;
        public Vector3 Home { get; private set; }
        public Vector3 InvestigatePoint => _investigatePoint;
        public Rigidbody Body { get; private set; }
        public BotSenses Senses { get; private set; }
        public int CaptureCount { get; private set; }
        public int WaypointsReached { get; private set; }
        public Vector3 ArenaCenterWorld => _arenaCenterW;
        public Vector3 EyePosition => EyePoint != null ? EyePoint.position : transform.position + Vector3.up * 1.25f + transform.forward * 0.45f;
        // campaign reactions
        public bool IsBlind => _blindTimer > 0f;
        public float BlindRemaining => Mathf.Max(0f, _blindTimer);
        public bool IsFrozen => _freezeTimer > 0f;
        public float FrozenRemaining => Mathf.Max(0f, _freezeTimer);
        public bool IsTiny => _tinyTimer > 0f;
        public int SizeClass => IsTiny ? -1 : 0;
        /// <summary>GuardBot coffee break: patrol/return hold still and sense nothing.</summary>
        public bool OnBreak { get; set; }
        public Vector3 SwapPoint => transform.position;
        public HoldingCell Cell
        {
            get { if (_cell == null && !string.IsNullOrEmpty(CellId)) _cell = HoldingCell.Find(CellId); return _cell; }
        }

        public event Action<SecurityBot, BotState> StateChanged;

        // ------------------------------------------------------------------ internals
        BotSteering _steer;
        HoldingCell _cell;
        Vector3[] _wp = new Vector3[0];
        Vector3 _arenaCenterW, _homeForward, _lastSeen, _investigatePoint, _lookBase;
        int _wpIndex;
        bool _started, _investigateLooking;
        float _stateTime, _senseTimer, _pauseTimer, _alertTimer, _unseen, _stunTimer, _lookStart, _investigatePriority;
        float _beepTimer = 3f, _sirenTimer, _angryTimer, _struggleTimer, _lastPlanarSpeed;

        static readonly string[] StruggleLines = { "PUT ME DOWN!", "I have RIGHTS!", "HELP!", "I'd like to speak to your manager!", "This is NOT in my job description!" };
        static readonly string[] GrabbedLines = { "HEY!", "NOOO!", "Unhand me, toaster!", "Not again!" };
        static readonly string[] AngryTexts = { "!!", "#@%!", "COME DOWN!", "BEEP BOOP >:(" };
        static readonly RaycastHit[] _dropHits = new RaycastHit[8];
        int _lineIndex;
        float _blindTimer, _freezeTimer, _tinyTimer, _blowerReady, _ledgeWatch, _ledgeY;
        bool _frozenKinematic;
        Vector3 _normalScale = Vector3.one;
        float _normalMass = Mass;
        GameObject _shirt;
        Grabbable _tinyGrab;

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Senses = new BotSenses(this);
            if (_frictionless == null)
                _frictionless = new PhysicsMaterial("BotFrictionless") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };
            foreach (var c in GetComponents<Collider>()) if (!c.isTrigger) c.sharedMaterial = _frictionless;
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            GameEvents.Noise += OnNoise;
        }

        void OnDisable()
        {
            All.Remove(this);
            GameEvents.Noise -= OnNoise;
        }

        void Start()
        {
            Body.mass = Mass;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.isKinematic = false;
            _steer = new BotSteering(Body, transform);
            Home = transform.position;
            _homeForward = transform.forward;
            RefreshWaypoints();
            _started = true;
            SetState(BotState.Patrol);
        }

        /// <summary>Re-read Waypoints/ArenaCenter (parent-local) into world space. Call after changing them at runtime.</summary>
        public void RefreshWaypoints()
        {
            var p = transform.parent;
            int n = Waypoints != null ? Waypoints.Length : 0;
            _wp = new Vector3[n];
            for (int i = 0; i < n; i++) _wp[i] = p != null ? p.TransformPoint(Waypoints[i]) : Waypoints[i];
            _arenaCenterW = p != null ? p.TransformPoint(ArenaCenter) : ArenaCenter;
            _wpIndex = n > 0 ? NearestWaypoint() : 0;
        }

        // ------------------------------------------------------------------ public API
        public bool CanSee(ShifterCharacter c) => Senses != null && !IsBlind && Senses.CanSee(c);

        /// <summary>Is the point inside the bot's arena circle (+margin)? Always true without an arena.</summary>
        public bool InArena(Vector3 p, float margin = 0f)
        {
            if (ArenaRadius <= 0f) return true;
            Vector3 d = p - (_started ? _arenaCenterW : ArenaCenter); d.y = 0f;
            return d.magnitude <= ArenaRadius + margin;
        }

        /// <summary>Stun (spin + stars). Drops a carried prisoner (rescued!). Re-stunning extends the timer.</summary>
        public void Stun(float seconds = -1f, string text = null)
        {
            if (seconds < 0f) seconds = StunDuration;
            if (seconds <= 0f || !_started) return;
            if (State == BotState.Stunned) { _stunTimer = Mathf.Max(_stunTimer, seconds); return; }
            if (Carried != null) ReleaseCarried();
            Target = null; IsWaitingBelow = false; _alertTimer = 0f;
            SetState(BotState.Stunned);
            _stunTimer = seconds;
            _steer.Stop();
            Sfx.At(SfxId.EnemyStun, transform.position + Vector3.up);
            Fx.Stars(HeadAnchor != null ? HeadAnchor : transform, seconds);
            Fx.Sparks(EyePosition, Vector3.up, 14, UIColors.AccentYellow);
            Fx.FloatingText(EyePosition + Vector3.up * 0.9f, text ?? "BZZT!", UIColors.AccentYellow, 1.3f);
        }

        /// <summary>Make the bot investigate a point as if it heard something (levels, tests).</summary>
        public void HearNoise(Vector3 position, float priority = 1f)
        {
            if (!_started) return;
            if (State == BotState.Patrol || State == BotState.Return || (State == BotState.Investigate && (priority >= _investigatePriority || _investigateLooking)))
                BeginInvestigate(position, priority, false);
        }

        /// <summary>IShootable: any weapon hit stuns the bot.</summary>
        public void OnShot(in ShotInfo shot)
        {
            switch (EnemyReact.Classify(shot))
            {
                case EnemyHit.LeafBlower:
                    // weapons' LeafBlowerStream shoves + stuns the bot itself (fixes/enemies.md #1): no double shove here.
                    if (!BlowerShovesItself && Time.time >= _blowerReady)
                    {
                        _blowerReady = Time.time + EnemyReact.BlowerStunCooldown;
                        Shove(EnemyReact.PushDir(shot) * EnemyReact.BlowerShoveSpeed, EnemyReact.BlowerStunSeconds);
                        _ledgeWatch = 2f; _ledgeY = transform.position.y;
                    }
                    return;
                case EnemyHit.TShirt:
                    // weapons' projectile adds its own TShirtWrap visual + "WRAPPED!" text right after OnShot:
                    // blind silently and only build our shirt if theirs has not appeared one tick later.
                    Fx.Puff(shot.Point, 0.6f, Color.white); BlindCore(EnemyReact.WrapBlindSeconds, null, false); _shirtCheck = 0.12f; return;
                case EnemyHit.SayCheese: Stun(EnemyReact.CheeseBotStunSeconds, "SAY CHEESE!"); return;
                case EnemyHit.PartyPopper: Fx.Confetti(EyePosition); Stun(EnemyReact.PopperDanceSeconds, "PARTY!"); return;
                case EnemyHit.ToolOnly: Fx.Sparks(shot.Point, shot.Normal, 6, UIColors.Info); return;
            }
            Fx.Sparks(shot.Point, shot.Normal, 12, UIColors.AccentYellow);
            bool chicken = shot.Weapon == WeaponType.Experimental && shot.Variant == ExperimentalVariant.RubberChickenLauncher;
            Stun(StunDuration, chicken ? "BAWK?!" : "BZZT!");
        }

        // ------------------------------------------------------------------ campaign reactions (IEnemyReactions & co.)
        /// <summary>WRAPPED / flashed: sight 0 for seconds; a chasing bot loses its target and wanders.</summary>
        public void Blind(float seconds, string label = null) => BlindCore(seconds, label ?? "WRAPPED!", true);

        /// <summary>When true (default) OnShot(LeafBlower) does nothing: weapons' LeafBlowerStream shoves + stuns the bot
        /// directly (Shove/Stun). Set false for a caller that only delivers OnShot and wants the shove from enemies.</summary>
        public static bool BlowerShovesItself = true;
        float _shirtCheck;

        void BlindCore(float seconds, string label, bool shirt)
        {
            if (seconds <= 0f || !_started) return;
            bool was = IsBlind;
            _blindTimer = Mathf.Max(_blindTimer, seconds);
            if (State == BotState.Chase) { Target = null; IsWaitingBelow = false; BeginInvestigate(transform.position + transform.forward * 3f, 0.2f, false); }
            if (!was)
            {
                if (label != null) Fx.FloatingText(EyePosition + Vector3.up * 0.9f, label, UIColors.Text, 1.3f);
                Sfx.At(SfxId.TShirtPop, EyePosition, 0.7f);
                if (shirt) ShowShirt(true);
            }
        }

        /// <summary>Add velocity (m/s); optional stun.</summary>
        public void Shove(Vector3 velocity, float stunSeconds = 0f)
        {
            if (!_started || Body == null || Body.isKinematic) return;
            Body.linearVelocity += velocity;
            if (stunSeconds > 0f) Stun(stunSeconds, "WHOOSH!");
        }

        /// <summary>Walk over to a noise point.</summary>
        public void Lure(Vector3 point, float priority = 1f) => HearNoise(point, priority);

        /// <summary>CAPS-LOCK: frozen solid (kinematic) for seconds; a carried prisoner stays carried.</summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f || !_started) return;
            bool was = IsFrozen;
            _freezeTimer = Mathf.Max(_freezeTimer, seconds);
            if (!was)
            {
                _steer.Stop();
                if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.isKinematic = true; _frozenKinematic = true; }
                Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "LOCKED", UIColors.Info, 1.3f);
                Sfx.At(SfxId.CapsLock, EyePosition, 0.8f);
            }
        }

        /// <summary>SWAP-O-MATIC: refuses only while frozen.</summary>
        public bool CanSwap(ShifterCharacter by, out string reason)
        {
            reason = null;
            if (!_started || !isActiveAndEnabled) { reason = "NOTHING TO SWAP WITH"; return false; }
            if (IsFrozen) { reason = "IT'S LOCKED"; return false; }
            return true;
        }

        /// <summary>SWAP-O-MATIC: teleport (feet to pos), drop a prisoner, then stunned 1.5 s "WHERE AM I?".</summary>
        public void SwapTo(Vector3 pos, Quaternion rot)
        {
            if (!_started) return;
            if (Carried != null) ReleaseCarried();
            Vector3 f = rot * Vector3.forward; f.y = 0f;
            Quaternion yaw = f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f) : transform.rotation;
            Body.position = pos; transform.SetPositionAndRotation(pos, yaw); Body.rotation = yaw;
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Stun(EnemyReact.SwapStunSeconds, "WHERE AM I?");
        }

        /// <summary>BIT-SHIFTER: a bot goes TINY for 8 s ("BONK-BOT 3": 15 kg, carryable, can't capture). Re-hit refreshes.</summary>
        public bool TryCycleSize(ShifterCharacter by, out string reason)
        {
            reason = null;
            if (!_started) { reason = "NOT NOW"; return false; }
            SetTiny(EnemyReact.TinySeconds);
            return true;
        }

        /// <summary>Make the bot tiny for seconds (0 = restore now).</summary>
        public void SetTiny(float seconds)
        {
            if (seconds > 0f)
            {
                bool was = IsTiny;
                _tinyTimer = Mathf.Max(_tinyTimer, seconds);
                if (was) return;
                if (Carried != null) ReleaseCarried();
                Target = null; IsWaitingBelow = false;
                _normalScale = transform.localScale;
                _normalMass = Body.mass;
                transform.localScale = _normalScale * Tuning.TinyScale;
                Body.mass = EnemyReact.TinyMass;
                if (_tinyGrab == null) { _tinyGrab = GetComponent<Grabbable>(); if (_tinyGrab == null) _tinyGrab = gameObject.AddComponent<Grabbable>(); _tinyGrab.DisplayName = "tiny BONK-BOT"; }
                _tinyGrab.CanBeGrabbed = true;
                Fx.Poof(transform.position + Vector3.up * 0.5f, UIColors.Info, 0.8f);
                Fx.FloatingText(EyePosition + Vector3.up * 0.5f, "BONK-BOT 3", UIColors.Info, 1.2f);
                Sfx.At(SfxId.BitShift, transform.position, 0.8f, 1.4f);
            }
            else if (IsTiny)
            {
                _tinyTimer = 0f;
                transform.localScale = _normalScale;
                Body.mass = _normalMass;
                if (_tinyGrab != null)
                {
                    _tinyGrab.CanBeGrabbed = false;
                    if (_tinyGrab.Holder != null) _tinyGrab.Holder.ForceRelease();
                }
                Fx.Poof(transform.position + Vector3.up * 0.8f, UIColors.Info, 1f);
                Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "BONK-BOT 3000 IS BACK", UIColors.AccentYellow, 1.1f);
                Sfx.At(SfxId.BitShift, transform.position, 0.8f, 0.8f);
            }
        }

        /// <summary>Timers for blind / freeze / tiny / ledge. Returns true when the brain must skip this tick.</summary>
        bool TickReactions(float dt)
        {
            if (_blindTimer > 0f) { _blindTimer -= dt; if (_blindTimer <= 0f) ShowShirt(false); }
            if (_shirtCheck > 0f)
            {
                _shirtCheck -= dt;
                if (_shirtCheck <= 0f && _blindTimer > 0f && !HasWeaponsWrap()) { ShowShirt(true); Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "WRAPPED!", UIColors.Text, 1.3f); }
            }
            if (_ledgeWatch > 0f)
            {
                _ledgeWatch -= dt;
                if (_ledgeY - transform.position.y >= EnemyReact.BlowerLedgeDrop) { _ledgeWatch = 0f; Stun(EnemyReact.BlowerLedgeStunSeconds, "OOF! (LEDGE)"); }
            }
            if (_tinyTimer > 0f)
            {
                _tinyTimer -= dt;
                if (_tinyTimer <= 0f) { _tinyTimer = 0.0001f; SetTiny(0f); }
                else if (_tinyGrab != null && (_tinyGrab.IsHeld || _tinyGrab.InFlight)) return true;   // carried around: brain off
            }
            if (_freezeTimer > 0f)
            {
                _freezeTimer -= dt;
                if (Carried != null) CaptiveBody.Maintain(Carried);
                if (_freezeTimer > 0f) return true;
                if (_frozenKinematic) { Body.isKinematic = false; _frozenKinematic = false; }
            }
            return false;
        }

        bool HasWeaponsWrap()
        {
            Transform at = HeadAnchor != null ? HeadAnchor : transform;
            var w = at.Find("WeaponsWrap") ?? transform.Find("WeaponsWrap");
            return w != null && w.gameObject.activeInHierarchy;
        }

        void ShowShirt(bool on)
        {
            if (on && _shirt == null)
            {
                Transform at = HeadAnchor != null ? HeadAnchor : transform;
                _shirt = Kit.BoxVisual(at, "WrappedShirt", new Vector3(0f, -0.55f, 0.1f), new Vector3(1.25f, 0.75f, 1.25f), MatKey.SignWhite, 0.12f, null, false).gameObject;
                Kit.BoxVisual(_shirt.transform, "Logo", new Vector3(0f, 0f, 0.64f), new Vector3(0.5f, 0.3f, 0.02f), MatKey.SafetyOrange, 0f, null, false);
            }
            if (_shirt != null) _shirt.SetActive(on);
        }

        // ------------------------------------------------------------------ brain
        void FixedUpdate()
        {
            if (!_started) return;
            float dt = Time.fixedDeltaTime;
            _stateTime += dt;
            _senseTimer -= dt;
            bool sense = false;
            if (_senseTimer <= 0f) { _senseTimer = SenseInterval; sense = true; }

            if (transform.position.y < Tuning.LethalFallY) { FellIntoVoid(); return; }
            if (TickReactions(dt)) return;
            if (State != BotState.Stunned && Body.linearVelocity.y > 8f) Stun(StunDuration, "WHEEE!");

            switch (State)
            {
                case BotState.Patrol: TickPatrol(dt, sense); break;
                case BotState.Investigate: TickInvestigate(dt, sense); break;
                case BotState.Chase: TickChase(dt, sense); break;
                case BotState.Capture: TickCarry(dt); break;
                case BotState.Return: TickReturn(dt, sense); break;
                case BotState.Stunned: TickStunned(dt); break;
            }
            var v = Body.linearVelocity;
            _lastPlanarSpeed = new Vector2(v.x, v.z).magnitude;
        }

        void TickPatrol(float dt, bool sense)
        {
            if (OnBreak) { _steer.Hold(transform.forward, dt, 0f); return; }
            if (sense && TrySpot()) return;
            IdleBeep(dt);
            Vector3 pos = transform.position;
            if (_wp.Length == 0)
            {
                if (PlanarDist(Home) > 0.6f) _steer.MoveTo(Home, PatrolSpeed, 0.2f, false, dt);
                else _steer.Hold(Quaternion.Euler(0f, Mathf.Sin(_stateTime * 0.6f) * 60f, 0f) * _homeForward, dt, 60f);
                return;
            }
            Vector3 goal = _wp[_wpIndex];
            if (PlanarDist(goal) < 0.7f)
            {
                _pauseTimer += dt;
                Vector3 next = _wp[(_wpIndex + 1) % _wp.Length];
                _steer.Hold(next - pos, dt, 120f);
                if (_pauseTimer >= WaypointPause) { _pauseTimer = 0f; _wpIndex = (_wpIndex + 1) % _wp.Length; WaypointsReached++; }
            }
            else
            {
                _steer.MoveTo(goal, PatrolSpeed, 0.3f, false, dt);
                if (_steer.StuckTime > 4f) _wpIndex = (_wpIndex + 1) % _wp.Length; // blocked: try the next one
            }
        }

        void TickInvestigate(float dt, bool sense)
        {
            if (sense && TrySpot()) return;
            if (!_investigateLooking)
            {
                _steer.MoveTo(_investigatePoint, PatrolSpeed * 1.4f, 0.9f, false, dt);
                if (PlanarDist(_investigatePoint) < 1.3f || _stateTime > 8f || _steer.StuckTime > 2.5f)
                { _investigateLooking = true; _lookStart = _stateTime; _lookBase = transform.forward; }
                return;
            }
            float t = _stateTime - _lookStart;
            _steer.Hold(Quaternion.Euler(0f, Mathf.Sin(t * 2.2f) * 75f, 0f) * _lookBase, dt, 220f);
            if (t >= InvestigateLookTime) { Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "...", UIColors.TextMuted, 1f); BeginReturn(); }
        }

        void TickChase(float dt, bool sense)
        {
            var t = Target;
            if (!BotSenses.IsCapturable(t))
            {
                Target = null; IsWaitingBelow = false;
                if (!TrySpot()) BeginInvestigate(_lastSeen, 0.5f, true);
                return;
            }
            Vector3 pos = transform.position;
            if (_alertTimer > 0f) { _alertTimer -= dt; _steer.Hold(t.Feet - pos, dt, 420f); return; }
            if (sense)
            {
                if (Senses.CanSee(t)) { _lastSeen = t.Feet; _unseen = 0f; }
                else
                {
                    _unseen += SenseInterval;
                    var other = Senses.FindTarget();
                    if (other != null) { Target = t = other; _lastSeen = other.Feet; _unseen = 0f; }
                }
            }
            if (_unseen > LoseInterestTime) { Target = null; IsWaitingBelow = false; Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "?", UIColors.AccentYellow, 1.3f); BeginInvestigate(_lastSeen, 0.5f, true); return; }
            if (!InArena(t.Feet, 0.5f)) { Target = null; IsWaitingBelow = false; Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "HMPH.", UIColors.TextMuted, 1.1f); BeginReturn(); return; }

            Vector3 goal = _unseen > 0f ? _lastSeen : t.Feet;
            float height = t.Feet.y - pos.y;
            IsWaitingBelow = _unseen <= 0f && height > UnreachableHeight && PlanarDist(goal) < 4.5f;
            if (IsWaitingBelow)
            {
                Vector3 away = pos - goal; away.y = 0f;
                away = away.sqrMagnitude > 1e-3f ? away.normalized : -transform.forward;
                Vector3 stand = goal + away * 1.3f; stand.y = pos.y;
                if (PlanarDist(stand) > 0.45f) _steer.MoveTo(stand, ChaseSpeed * 0.6f, 0.2f, true, dt);
                else _steer.Hold(goal - pos, dt, 300f);
                AngryBeep(dt);
                return;
            }
            if (_unseen <= 0f && CanGrab(t)) { Grab(t); return; }
            _steer.MoveTo(ClampToArena(goal, 0.6f), ChaseSpeed, 0f, true, dt);
            _sirenTimer -= dt;
            if (_sirenTimer <= 0f) { _sirenTimer = 0.9f; Sfx.At(SfxId.EnemyBeep, pos, 0.55f, 1.6f); }
        }

        void TickCarry(float dt)
        {
            var c = Carried;
            if (c == null) { BeginReturn(); return; }
            if (c.State != CharacterState.Captured)
            {
                // someone else changed its state (checkpoint revive, cutscene): let go
                Carried = null;
                CaptiveBody.Free(c, DropSpot(), transform.rotation);
                BeginReturn();
                return;
            }
            CaptiveBody.Maintain(c);
            var cell = Cell;
            Vector3 goal = cell != null ? cell.IntakePosition : Home;
            _sirenTimer -= dt;
            if (_sirenTimer <= 0f) { _sirenTimer = 1.1f; Sfx.At(SfxId.Siren, transform.position + Vector3.up, 0.9f); }
            _struggleTimer -= dt;
            if (_struggleTimer <= 0f) { _struggleTimer = 2.6f; c.Say(StruggleLines[_lineIndex++ % StruggleLines.Length]); }
            if (PlanarDist(goal) < 1.0f || _stateTime > 30f || _steer.StuckTime > 6f) { Deliver(); return; }
            _steer.MoveTo(goal, ChaseSpeed, 0.4f, true, dt);
        }

        void TickReturn(float dt, bool sense)
        {
            if (OnBreak) { _steer.Hold(transform.forward, dt, 0f); return; }
            if (sense && TrySpot()) return;
            Vector3 goal = _wp.Length > 0 ? _wp[_wpIndex] : Home;
            if (PlanarDist(goal) < 0.8f) { SetState(BotState.Patrol); return; }
            _steer.MoveTo(goal, PatrolSpeed * 1.2f, 0.3f, false, dt);
            if (_steer.StuckTime > 4f && _wp.Length > 1) _wpIndex = (_wpIndex + 1) % _wp.Length;
        }

        void TickStunned(float dt)
        {
            _stunTimer -= dt;
            _steer.Hold(transform.forward, dt, 0f);
            if (_stunTimer <= 0f) { Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "...REBOOTING", UIColors.TextMuted, 0.9f); BeginInvestigate(transform.position, 0f, true); }
        }

        // ------------------------------------------------------------------ transitions
        void SetState(BotState s)
        {
            State = s;
            _stateTime = 0f;
            _pauseTimer = 0f;
            StateChanged?.Invoke(this, s);
        }

        bool TrySpot()
        {
            var t = Senses.FindTarget();
            if (t == null) return false;
            BeginChase(t);
            return true;
        }

        void BeginChase(ShifterCharacter t)
        {
            Target = t;
            _lastSeen = t.Feet;
            _unseen = 0f;
            _alertTimer = AlertPause;
            _sirenTimer = 0f;
            IsWaitingBelow = false;
            SetState(BotState.Chase);
            Sfx.At(SfxId.EnemyAlert, transform.position + Vector3.up);
            Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "!", UIColors.Danger, 1.8f);
        }

        void BeginInvestigate(Vector3 point, float priority, bool lookOnly)
        {
            _investigatePoint = ClampToArena(point, 0.8f);
            _investigatePriority = priority;
            SetState(BotState.Investigate);
            _investigateLooking = lookOnly;
            _lookStart = 0f;
            _lookBase = transform.forward;
            if (!lookOnly)
            {
                Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "?", UIColors.AccentYellow, 1.5f);
                Sfx.At(SfxId.EnemyBeep, transform.position + Vector3.up, 0.7f, 1.35f);
            }
        }

        void BeginReturn()
        {
            Target = null;
            IsWaitingBelow = false;
            if (_wp.Length > 0) _wpIndex = NearestWaypoint();
            SetState(BotState.Return);
        }

        // ------------------------------------------------------------------ capture & delivery
        bool CanGrab(ShifterCharacter t)
        {
            if (IsTiny || IsBlind) return false;
            Vector3 d = t.Feet - transform.position;
            float h = d.y; d.y = 0f;
            if (h < -1f || h > 1.25f) return false;
            if (t.Condition == CharacterCondition.Thrown && t.Body != null && t.Body.linearVelocity.magnitude > ImpactStunSpeed) return false; // incoming bowling ball!
            return d.magnitude <= CaptureRadius + t.Radius * 0.3f + 0.15f;
        }

        void Grab(ShifterCharacter c)
        {
            Target = null; IsWaitingBelow = false;
            var cell = Cell;
            c.Capture(cell != null ? cell.Id : "", "was captured by a security bot");
            if (c.State != CharacterState.Captured) { BeginReturn(); return; }
            Carried = c;
            CaptureCount++;
            Transform holder = GrabPoint != null ? GrabPoint : transform;
            Vector3 local = new Vector3(0f, -c.Height * 0.5f, 0f);
            if (GrabPoint == null) local += new Vector3(0f, 1.55f, 0.95f);
            CaptiveBody.Seize(c, holder, local, Quaternion.Euler(0f, 180f, 0f));
            Sfx.At(SfxId.EnemyGrab, c.Center);
            Sfx.Voice(c, VoiceLine.Scream);
            c.Say(GrabbedLines[_lineIndex++ % GrabbedLines.Length]);
            Fx.FloatingText(EyePosition + Vector3.up * 1.0f, "GOTCHA!", UIColors.Danger, 1.2f);
            // the "<NAME> HAS BEEN CAPTURED" banner is raised by CharacterManager on the state change
            var s = GameSession.Instance;
            if (s != null && s.Stats != null) s.Stats.Captures++;
            _struggleTimer = 1.2f;
            _sirenTimer = 0f;
            SetState(BotState.Capture);
        }

        void Deliver()
        {
            var c = Carried;
            Carried = null;
            if (c != null)
            {
                var cell = Cell;
                if (cell != null)
                {
                    cell.Imprison(c);
                    Fx.FloatingText(EyePosition + Vector3.up * 0.9f, "DETAINED!", UIColors.Danger, 1.1f);
                }
                else
                {
                    CaptiveBody.Free(c, DropSpot(), Quaternion.LookRotation(-transform.forward));
                    c.CapturedCellId = "";
                    c.KnockOut("was bonked by a security bot");
                    Sfx.At(SfxId.Bonk, c.Center);
                    Fx.Stars(c.transform, 2f);
                }
            }
            BeginReturn();
        }

        /// <summary>Stunned while carrying: drop the prisoner, who is free again.</summary>
        void ReleaseCarried()
        {
            var c = Carried;
            Carried = null;
            if (c == null) return;
            Vector3 spot = DropSpot();
            Vector3 face = transform.position - spot; face.y = 0f;
            CaptiveBody.Free(c, spot, face.sqrMagnitude > 1e-3f ? Quaternion.LookRotation(face) : transform.rotation);
            c.ReleaseFromCapture();
            if (c.Visual != null) c.Visual.SetExpression(Expression.Happy, 1.5f);
            // the "<NAME> HAS BEEN RESCUED" banner is raised by CharacterManager on the Captured -> Alive change
            Fx.Confetti(c.Center + Vector3.up * 0.5f);
            Sfx.At(SfxId.Revive, c.Center);
            c.Say("I'M FREE!");
        }

        /// <summary>A free spot next to the bot (front, back, left, right) at its feet height.</summary>
        Vector3 DropSpot()
        {
            Vector3 pos = transform.position;
            Vector3 f = transform.forward; f.y = 0f; f = f.sqrMagnitude > 1e-3f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3[] dirs = { f, -f, r, -r };
            for (int i = 0; i < dirs.Length; i++)
            {
                if (!Blocked(pos + Vector3.up * 0.9f, dirs[i], 1.5f)) return pos + dirs[i] * 1.3f + Vector3.up * 0.05f;
            }
            return pos + Vector3.up * 0.1f;
        }

        bool Blocked(Vector3 origin, Vector3 dir, float len)
        {
            int n = Physics.SphereCastNonAlloc(origin, 0.35f, dir, _dropHits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _dropHits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<ShifterCharacter>() != null) continue;
                return true;
            }
            return false;
        }

        void FellIntoVoid()
        {
            if (Carried != null)
            {
                var c = Carried; Carried = null;
                var cell = Cell;
                if (cell != null) cell.Imprison(c);
                else { CaptiveBody.Free(c, Home + Vector3.up * 0.2f, Quaternion.identity); c.ReleaseFromCapture(); }
            }
            GameEvents.RaiseToast("A BONK-BOT has been decommissioned. Nobody liked it anyway.", UIColors.Success);
            Sfx.At(SfxId.Explosion, transform.position, 0.5f);
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ hearing & impacts
        void OnNoise(NoiseEvent e)
        {
            if (!_started || !isActiveAndEnabled) return;
            if ((e.Position - transform.position).magnitude > e.Radius) return;
            Senses.Remember(e);
            if (e.Source != null && CaptiveBody.IsSeized(e.Source)) return;
            HearNoise(e.Position, e.Priority);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!_started || State == BotState.Stunned) return;
            var rb = collision.rigidbody;
            if (rb == null || rb.isKinematic || rb == Body) return;
            if (rb.GetComponent<WeaponProjectile>() != null) return; // projectiles stun via OnShot
            var ch = rb.GetComponent<ShifterCharacter>();
            if (ch != null && ch.Condition != CharacterCondition.Thrown) return;
            if (ch == null && rb.mass < 8f) return;
            if (rb.GetComponent<SecurityBot>() != null) return;
            float rel = collision.relativeVelocity.magnitude;
            if (rel >= ImpactStunSpeed && rel > _lastPlanarSpeed * 1.5f)
            {
                Sfx.At(SfxId.Bonk, transform.position + Vector3.up);
                Stun(StunDuration, ch != null ? "STRIKE!" : "BONK!");
                if (ch != null) ch.Say("STRIKE!");
            }
        }

        // ------------------------------------------------------------------ audio flavour
        void IdleBeep(float dt)
        {
            _beepTimer -= dt;
            if (_beepTimer > 0f) return;
            _beepTimer = 4f + Mathf.PerlinNoise(transform.position.x * 0.3f, _stateTime) * 3f;
            Sfx.At(SfxId.EnemyBeep, transform.position + Vector3.up, 0.3f, 0.9f + Mathf.PerlinNoise(_stateTime, 1.7f) * 0.3f);
        }

        void AngryBeep(float dt)
        {
            _angryTimer -= dt;
            if (_angryTimer > 0f) return;
            _angryTimer = 0.45f;
            _lineIndex++;
            Sfx.At(SfxId.EnemyBeep, transform.position + Vector3.up, 0.8f, (_lineIndex & 1) == 0 ? 2f : 1.7f);
            if (_lineIndex % 4 == 0) Fx.FloatingText(EyePosition + Vector3.up * 0.9f, AngryTexts[(_lineIndex / 4) % AngryTexts.Length], UIColors.Danger, 1.1f);
        }

        // ------------------------------------------------------------------ helpers
        float PlanarDist(Vector3 p) { Vector3 d = p - transform.position; d.y = 0f; return d.magnitude; }

        Vector3 ClampToArena(Vector3 p, float margin)
        {
            if (ArenaRadius <= 0f) return p;
            Vector3 d = p - _arenaCenterW; d.y = 0f;
            float max = Mathf.Max(0.5f, ArenaRadius - margin);
            if (d.magnitude <= max) return p;
            Vector3 q = _arenaCenterW + d.normalized * max;
            q.y = p.y;
            return q;
        }

        int NearestWaypoint()
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < _wp.Length; i++) { float d = PlanarDist(_wp[i]); if (d < bd) { bd = d; best = i; } }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var p = transform.parent;
            if (Waypoints != null)
                for (int i = 0; i < Waypoints.Length; i++)
                {
                    Vector3 a = p != null ? p.TransformPoint(Waypoints[i]) : Waypoints[i];
                    Vector3 b = p != null ? p.TransformPoint(Waypoints[(i + 1) % Waypoints.Length]) : Waypoints[(i + 1) % Waypoints.Length];
                    Gizmos.DrawWireSphere(a, 0.3f); Gizmos.DrawLine(a, b);
                }
            if (ArenaRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.3f);
                Vector3 c = p != null ? p.TransformPoint(ArenaCenter) : ArenaCenter;
                for (int i = 0; i < 32; i++)
                {
                    float a0 = i / 32f * Mathf.PI * 2f, a1 = (i + 1) / 32f * Mathf.PI * 2f;
                    Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * ArenaRadius, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * ArenaRadius);
                }
            }
        }
    }
}
