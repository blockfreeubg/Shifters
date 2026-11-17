// =====================================================================================
//  SHIFTERS - StalkerBrain.cs   [owned by the ENEMIES agent]
//  Team-Building Dummy / cardboard Max (CH09-CH11, O5). Moves ONLY while UNLIT (its own centre is not in a
//  pool / lit rule / the active flashlight cone with line of sight - LightQuery), stop-motion glide at
//  Tuning.StalkerSpeed (visual hops at HopHz), never steps into light. Targets the nearest SCARED intern
//  (Bravery.IsScared; inactive interns count InactiveBonus m closer); HuntAll = any Alive intern.
//  Capture at CaptureRadius = "TEAM-BUILDING HUG" -> HoldingCell CellId (Imprison) or a KO without a cell,
//  then it poofs home and rests. Freeze(s) (SAY CHEESE 3 s counts as lit, CAPS-LOCK, scripted scares via
//  FreezeAll), 1.5 s grace after every SHIFT, noise lures, dormant outside its arena (no Alive intern within
//  ArenaRadius + WakeMargin) or while Dormant is set. Never hurts anyone.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public enum StalkerState { Dormant, Lit, Frozen, Idle, Hunting, Lured, Resting }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class StalkerBrain : MonoBehaviour, IShootable, IEnemyReactions, IResettable
    {
        public static readonly List<StalkerBrain> All = new List<StalkerBrain>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); _frictionless = null; }
        static PhysicsMaterial _frictionless;

        public const float SenseInterval = 0.1f;
        public const float Mass = 200f;

        [Header("Wiring (EnemyKit.Stalker) - points are in the PARENT's local space")]
        public string CellId = "";
        public Vector3 ArenaCenter;
        [Tooltip("Never leaves this circle (<= 0: unlimited).")] public float ArenaRadius = 10f;
        public StalkerSkin Skin = StalkerSkin.TeamBuildingDummy;

        [Header("Tuning")]
        public float Speed = Tuning.StalkerSpeed;
        public float CaptureRadius = 1.1f;
        public float HopHz = 9f;
        public float InactiveBonus = 3f;
        [Tooltip("Awake only while an Alive intern is within ArenaRadius + WakeMargin.")] public float WakeMargin = 4f;
        public float RestAfterHug = 4f;
        [Tooltip("Look-ahead used to stop at the edge of light.")] public float EdgeLookAhead = 0.7f;
        [Tooltip("Hunt ANY Alive intern, scared or not (CH09 S3 'the dummies are impatient').")] public bool HuntAll;
        [Tooltip("Manual sleep (levels wake sections).")] public bool Dormant;

        [Header("Parts (EnemyKit)")]
        public Transform ModelRoot;
        /// <summary>visuals' IStalkerVisual (DummyModel / CardboardMaxModel) under ModelRoot, or null (placeholder model).</summary>
        public Component Visual;
        IStalkerVisual _vis; bool _visInit, _visMoving, _visLit; int _visPose = -1;
        public Transform HeadAnchor;
        public float ModelHeight = 2f;

        // ------------------------------------------------------------------ readable state
        public StalkerState State { get; private set; } = StalkerState.Idle;
        public bool IsLit { get; private set; }
        public bool IsMoving { get; private set; }
        public ShifterCharacter Target { get; private set; }
        public bool IsFrozen => _freeze > 0f;
        public float FrozenRemaining => Mathf.Max(0f, _freeze);
        public bool IsBlind => _blind > 0f;
        public bool IsAsleep => State == StalkerState.Dormant;
        public int CaptureCount { get; private set; }
        public Vector3 Home { get; private set; }
        public Vector3 ArenaCenterWorld => _arenaW;
        public Vector3 Center => transform.position + Vector3.up * (ModelHeight * 0.5f);
        public Rigidbody Body { get; private set; }
        /// <summary>Meters moved while unlit (tests: must not grow while lit).</summary>
        public float DistanceMoved { get; private set; }
        public float DistanceMovedLit { get; private set; }

        public event Action<StalkerBrain, ShifterCharacter> Hugged;

        BotSteering _steer;
        Vector3 _arenaW, _lure, _lastPos, _heldModelPos;
        Quaternion _heldModelRot;
        float _freeze, _blind, _sense, _lureTimer, _hop, _rest, _creak, _stuckLook;
        bool _started, _wasMoving;
        int _hum;
        static readonly string[] HugLines = { "TEAM-BUILDING HUG!", "TRUST FALL!", "SYNERGY!", "WELCOME TO THE TEAM!" };
        static readonly string[] VictimLines = { "NOT THE TRUST EXERCISE!", "IT'S SO... WOODEN!", "PERSONAL SPACE!", "I'M NOT A TEAM PLAYER!" };
        int _line;

        // ------------------------------------------------------------------ lifecycle
        void Awake() { Body = GetComponent<Rigidbody>(); }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            GameEvents.Noise += OnNoise;
            GameEvents.CharacterSwitched += OnSwitched;
            BraveryTicker.Ensure();
        }

        void OnDisable()
        {
            All.Remove(this);
            GameEvents.Noise -= OnNoise;
            GameEvents.CharacterSwitched -= OnSwitched;
            if (_hum > 0) { Sfx.StopLoop(_hum, 0.2f); _hum = 0; }
        }

        void Start()
        {
            Body.mass = Mass;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.isKinematic = false;
            // no ground friction: the steering drives the velocity directly (a 2 m capsule with default friction barely creeps)
            if (_frictionless == null)
                _frictionless = new PhysicsMaterial("StalkerFrictionless") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };
            foreach (var c in GetComponents<Collider>()) if (!c.isTrigger) c.sharedMaterial = _frictionless;
            _steer = new BotSteering(Body, transform) { TurnRate = 400f, ChaseTurnRate = 600f };
            Home = transform.position;
            var p = transform.parent;
            _arenaW = p != null ? p.TransformPoint(ArenaCenter) : ArenaCenter;
            _lastPos = transform.position;
            if (ModelRoot != null) { _heldModelPos = ModelRoot.position; _heldModelRot = ModelRoot.rotation; }
            _started = true;
        }

        // ------------------------------------------------------------------ public API
        /// <summary>Freeze for seconds (SAY CHEESE, CAPS-LOCK, scripted scare). Re-freezing extends.</summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;
            _freeze = Mathf.Max(_freeze, seconds);
            StopNow();
        }

        /// <summary>Freeze every stalker (ScareDirector: 2 s after a scripted scare).</summary>
        public static void FreezeAll(float seconds)
        {
            for (int i = 0; i < All.Count; i++) if (All[i] != null) All[i].Freeze(seconds);
        }

        /// <summary>Manual dormancy (levels: wake a section's dummies).</summary>
        public void SetDormant(bool dormant) { Dormant = dormant; if (dormant) StopNow(); }

        public static void SetAllDormant(bool dormant) { for (int i = 0; i < All.Count; i++) if (All[i] != null) All[i].SetDormant(dormant); }

        /// <summary>Nearest stalker to p (HUD vignette pulse / IQ threat vision), or null.</summary>
        public static StalkerBrain Nearest(Vector3 p, out float distance)
        {
            StalkerBrain best = null; distance = float.PositiveInfinity;
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s == null || !s.isActiveAndEnabled) continue;
                float d = Vector3.Distance(p, s.transform.position);
                if (d < distance) { distance = d; best = s; }
            }
            return best;
        }

        public void Blind(float seconds, string label = null)
        {
            if (seconds <= 0f) return;
            if (_blind <= 0f) Fx.FloatingText(Center + Vector3.up * 1.2f, label ?? "WRAPPED!", UIColors.Text, 1.2f);
            _blind = Mathf.Max(_blind, seconds);
            Target = null;
        }

        /// <summary>Dummies don't get dizzy: a stun is a freeze.</summary>
        public void Stun(float seconds, string label = null)
        {
            if (seconds <= 0f) return;
            if (!string.IsNullOrEmpty(label)) Fx.FloatingText(Center + Vector3.up * 1.2f, label, UIColors.AccentYellow, 1.2f);
            Freeze(seconds);
        }

        public void Shove(Vector3 velocity, float stunSeconds = 0f)
        {
            if (Body != null && !Body.isKinematic) Body.linearVelocity += velocity;
            if (stunSeconds > 0f) Freeze(stunSeconds);
        }

        public void Lure(Vector3 point, float priority = 1f)
        {
            if (!InArena(point, 0.5f)) return;
            _lure = point; _lureTimer = 6f;
        }

        public void OnShot(in ShotInfo shot)
        {
            switch (EnemyReact.Classify(shot))
            {
                case EnemyHit.SayCheese:
                    Fx.FloatingText(Center + Vector3.up * 1.2f, "SAY CHEESE!", UIColors.AccentYellow, 1.2f);
                    Freeze(EnemyReact.CheeseFreezeSeconds); break;
                case EnemyHit.TShirt: Blind(EnemyReact.WrapBlindSeconds, "WRAPPED!"); break;
                case EnemyHit.PartyPopper: Fx.Confetti(Center); Stun(EnemyReact.PopperDanceSeconds, "PARTY!"); break;
                case EnemyHit.LeafBlower: Shove(EnemyReact.PushDir(shot) * EnemyReact.BlowerShoveSpeed, 0.5f); break;
                case EnemyHit.ToolOnly: Fx.Sparks(shot.Point, shot.Normal, 6, UIColors.Info); break;
                default:
                    Fx.Puff(shot.Point, 0.4f, new Color(0.8f, 0.65f, 0.45f));
                    Fx.FloatingText(Center + Vector3.up * 1.2f, Skin == StalkerSkin.CardboardMax ? "*rip*" : "*thunk*", UIColors.TextMuted, 1f);
                    Freeze(0.6f); break;
            }
        }

        public void ResetState()
        {
            Target = null; _freeze = 0f; _blind = 0f; _lureTimer = 0f; _rest = 0f;
            if (_started) Warp(Home);
        }

        public bool InArena(Vector3 p, float margin = 0f)
        {
            if (ArenaRadius <= 0f) return true;
            Vector3 c = _started ? _arenaW : (transform.parent != null ? transform.parent.TransformPoint(ArenaCenter) : ArenaCenter);
            Vector3 d = p - c; d.y = 0f;
            return d.magnitude <= ArenaRadius + margin;
        }

        // ------------------------------------------------------------------ brain
        void FixedUpdate()
        {
            if (!_started) return;
            float dt = Time.fixedDeltaTime;
            if (_freeze > 0f) _freeze -= dt;
            if (_blind > 0f) _blind -= dt;
            if (_lureTimer > 0f) _lureTimer -= dt;
            if (_rest > 0f) _rest -= dt;
            if (transform.position.y < Tuning.LethalFallY) { Warp(Home); return; }

            _sense -= dt;
            bool sense = _sense <= 0f;
            if (sense)
            {
                _sense = SenseInterval;
                IsLit = LightQuery.IsLit(Center) || LightQuery.IsLit(transform.position + Vector3.up * 0.3f);
            }

            bool moved = false;
            if (Dormant || !AnyInternNear()) { SetState(StalkerState.Dormant); Target = null; StopNow(); }
            else if (IsLit) { SetState(StalkerState.Lit); StopNow(); }
            else if (_freeze > 0f) { SetState(StalkerState.Frozen); StopNow(); }
            else if (_rest > 0f) { SetState(StalkerState.Resting); StopNow(); }
            else
            {
                if (sense) Target = IsBlind ? null : FindTarget();
                var t = Target;
                if (t != null && t.IsAlive)
                {
                    SetState(StalkerState.Hunting);
                    if (CanHug(t)) { Hug(t); StopNow(); }
                    else moved = Step(t.Feet, dt);
                }
                else if (_lureTimer > 0f)
                {
                    SetState(StalkerState.Lured);
                    if (Planar(_lure) < 1.2f) { _lureTimer = 0f; StopNow(); }
                    else moved = Step(_lure, dt);
                }
                else
                {
                    SetState(StalkerState.Idle);
                    if (!InArena(transform.position, 0.5f)) moved = Step(Home, dt);
                    else StopNow();
                }
            }

            IsMoving = moved;
            Vector3 pos = transform.position;
            float d = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_lastPos.x, 0f, _lastPos.z));
            if (IsLit) DistanceMovedLit += d; else DistanceMoved += d;
            _lastPos = pos;
            Tells(dt);
        }

        bool AnyInternNear()
        {
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.isActiveAndEnabled && c.IsAlive && InArena(c.Feet, WakeMargin)) return true;
            }
            return false;
        }

        ShifterCharacter FindTarget()
        {
            var all = ShifterCharacter.All;
            ShifterCharacter best = null;
            float bestScore = float.MaxValue;
            Vector3 pos = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!BotSenses.IsCapturable(c) || !InArena(c.Feet, 0.5f)) continue;
                if (!HuntAll && !Bravery.IsScared(c)) continue;
                float score = Vector3.Distance(pos, c.Feet) - (c.IsActive ? 0f : InactiveBonus);
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        /// <summary>One movement tick toward goal; refuses to step into light or out of the arena. True if moving.</summary>
        bool Step(Vector3 goal, float dt)
        {
            Vector3 pos = transform.position;
            Vector3 to = goal - pos; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) { StopNow(); return false; }
            Vector3 dir = to.normalized;
            Vector3 ahead = pos + dir * EdgeLookAhead;
            if (LightQuery.IsLit(ahead + Vector3.up * (ModelHeight * 0.5f)) || !InArena(ahead, 0f))
            {
                // edge of the light / arena: wait there, facing the target (creepy)
                _steer.Hold(dir, dt, 300f);
                StopNow();
                return false;
            }
            _steer.MoveTo(goal, Speed, 0.05f, true, dt);
            var v = Body.linearVelocity;
            return new Vector2(v.x, v.z).sqrMagnitude > 0.04f;
        }

        bool CanHug(ShifterCharacter t)
        {
            Vector3 d = t.Feet - transform.position;
            float h = d.y; d.y = 0f;
            if (h < -1f || h > 1.3f) return false;
            return d.magnitude <= CaptureRadius + t.Radius * 0.5f;
        }

        void Hug(ShifterCharacter c)
        {
            Target = null;
            CaptureCount++;
            Fx.FloatingText(Center + Vector3.up * 1.3f, HugLines[_line % HugLines.Length], UIColors.Danger, 1.4f);
            c.Say(VictimLines[_line++ % VictimLines.Length]);
            Sfx.At(SfxId.CardboardCreak, c.Center, 1f, 0.8f);
            Sfx.Voice(c, VoiceLine.Scream);
            var cell = string.IsNullOrEmpty(CellId) ? null : HoldingCell.Find(CellId);
            if (cell != null)
            {
                c.Capture(cell.Id, "got a team-building hug");
                cell.Imprison(c);
            }
            else c.KnockOut("got a team-building hug");
            var s = GameSession.Instance;
            if (s != null && s.Stats != null) s.Stats.Captures++;
            Hugged?.Invoke(this, c);
            // poof home and rest
            Fx.Poof(Center, new Color(0.85f, 0.7f, 0.5f), 1.2f);
            Warp(Home);
            Fx.Poof(Home + Vector3.up, new Color(0.85f, 0.7f, 0.5f), 1.2f);
            _rest = RestAfterHug;
            SetState(StalkerState.Resting);
        }

        /// <summary>Seconds left of the post-hug rest (0 when not resting).</summary>
        public float RestRemaining => Mathf.Max(0f, _rest);

        void Warp(Vector3 p)
        {
            StopNow();
            Body.position = p; transform.position = p;
            _lastPos = p;
            if (ModelRoot != null) { _heldModelPos = ModelRoot.parent != null ? ModelRoot.parent.TransformPoint(Vector3.zero) : p; }
        }

        void StopNow()
        {
            if (Body == null || Body.isKinematic) return;
            var v = Body.linearVelocity;
            Body.linearVelocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
        }

        void SetState(StalkerState s) { State = s; }

        void OnSwitched(ShifterCharacter from, ShifterCharacter to) { if (_freeze < Tuning.StalkerGraceAfterSwitch) _freeze = Tuning.StalkerGraceAfterSwitch; }

        void OnNoise(NoiseEvent e)
        {
            if (!_started || !isActiveAndEnabled) return;
            if ((e.Position - transform.position).magnitude > e.Radius) return;
            if (e.Source != null && CaptiveBody.IsSeized(e.Source)) return;
            Lure(e.Position, e.Priority);
        }

        float Planar(Vector3 p) { Vector3 d = p - transform.position; d.y = 0f; return d.magnitude; }

        // ------------------------------------------------------------------ stop-motion visual + audio tells
        void DriveVisual()
        {
            if (!_visInit) { _visInit = true; _vis = Visual as IStalkerVisual; _visMoving = false; _visLit = false; }
            if (_vis == null) return;
            bool lit = IsLit || IsFrozen;
            if (lit != _visLit) { _visLit = lit; _vis.SetLit(lit); }
            bool mv = IsMoving && !lit;
            if (mv != _visMoving) { _visMoving = mv; _vis.SetMoving(mv, Tuning.StalkerSpeed); }
            var dm = _vis as DummyModel;
            if (dm != null)
            {
                var tg = Target;
                dm.LookTarget = tg != null && State != StalkerState.Dormant ? tg.HeadTop : (Vector3?)null;
                int pose = State == StalkerState.Dormant || State == StalkerState.Resting ? 1 : tg != null && Planar(tg.Feet) < 3f ? 2 : 0;
                if (pose != _visPose)
                {
                    _visPose = pose;
                    dm.SetPose(pose == 2 ? DummyPose.Reach : pose == 1 ? DummyPose.Slump : DummyPose.Stand, 0.3f);
                }
            }
        }

        void LateUpdate()
        {
            if (_started) DriveVisual();
            if (ModelRoot == null || !_started) return;
            Transform root = ModelRoot.parent != null ? ModelRoot.parent : transform;
            if (!IsMoving)
            {
                _heldModelPos = root.position; _heldModelRot = root.rotation;
                ModelRoot.SetPositionAndRotation(_heldModelPos, _heldModelRot);
                _hop = 0f;
                return;
            }
            _hop -= Time.deltaTime;
            if (_hop <= 0f)
            {
                _hop += 1f / Mathf.Max(1f, HopHz);
                if (_hop < 0f) _hop = 0f;
                _heldModelPos = root.position + Vector3.up * 0.04f;
                _heldModelRot = root.rotation;
            }
            ModelRoot.SetPositionAndRotation(_heldModelPos, _heldModelRot);
        }

        void Tells(float dt)
        {
            if (IsMoving && !_wasMoving)
            {
                if (_hum == 0) _hum = Sfx.Loop(SfxId.VacuumHum, transform.position, 0.35f, transform);
                else Sfx.LoopVolume(_hum, 0.35f);
            }
            else if (!IsMoving && _wasMoving && _hum > 0) Sfx.LoopVolume(_hum, 0f);
            _wasMoving = IsMoving;
            if (!IsMoving) return;
            _creak -= dt;
            if (_creak <= 0f)
            {
                _creak = 0.55f + Mathf.PerlinNoise(transform.position.x, Time.time) * 0.5f;
                Sfx.At(SfxId.CardboardCreak, transform.position + Vector3.up, 0.45f, 0.8f + Mathf.PerlinNoise(Time.time, 3.1f) * 0.4f);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (ArenaRadius <= 0f) return;
            Gizmos.color = new Color(1f, 0.5f, 0.9f);
            Vector3 c = transform.parent != null ? transform.parent.TransformPoint(ArenaCenter) : ArenaCenter;
            for (int i = 0; i < 32; i++)
            {
                float a0 = i / 32f * Mathf.PI * 2f, a1 = (i + 1) / 32f * Mathf.PI * 2f;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * ArenaRadius, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * ArenaRadius);
            }
        }
    }
}
