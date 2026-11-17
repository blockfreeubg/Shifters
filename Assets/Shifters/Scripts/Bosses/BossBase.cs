// =====================================================================================
//  SHIFTERS - BossBase.cs   [BOSSES module - CAMPAIGN §11.1 / §18.3]
//  Base of every boss brain (DownsizerBoss, PorkchopBoss, BoogeymanBoss, OneManBandBoss live in
//  their level folders). A SignalSource that turns ON when defeated (B8: wire the exit doors to it).
//    Dormant -> Intro -> Fighting <-> Vulnerable -> PhaseTransition -> ... -> Defeated
//  * Phases: fixed attack loop (BossAttackRunner), N discrete hits (B1, <= 3) filtered by phase kind,
//    state and (optionally) the vulnerable window. Weak points call Hit().
//  * B7: each phase start force-activates its checkpoint (revives Downed); after Restart Checkpoint the
//    boss restores the phase from CheckpointManager.CurrentIndex and auto-begins. Later phase checkpoints
//    are disabled until their phase starts (walking through them early must not skip a phase).
//  * B10 mercy: 2 team wipes in one phase -> tells +30 %, damage -30 % (persists across reloads).
//  * B13 bot API: Phase, State, IsVulnerable, NextAttackIn, Target, Attacks.IsThreatened/NextThreatTime.
//  A brain usually only fills Phases in Awake/the factory and overrides OnPhaseBegin + OnAttackEnd
//  (e.g. "after the shockwave the heart is exposed": SetVulnerable(4)).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public abstract class BossBase : SignalSource, IResettable
    {
        [Header("Identity")]
        public string BossId = "boss";
        public string BossName = "BOSS", BossTitle = "", BarLabel = "";
        public BossTargetRule Rule = BossTargetRule.ActiveOnly;
        public BossPhaseSpec[] Phases = new BossPhaseSpec[0];
        public BossArena Arena;

        [Header("Flow")]
        [Tooltip("Moved by Charge/SkateLoop and turned toward the target (default: this transform).")] public Transform Body;
        [Tooltip("Begin when the active intern enters the arena bounds.")] public bool AutoStart = true;
        [Tooltip("After Restart Checkpoint inside the fight: begin at the restored phase automatically.")] public bool AutoBeginOnRestore = true;
        public float IntroSeconds = 2.5f, RestoreIntroSeconds = 1.5f, TransitionSeconds = 3f, DefeatedHideSeconds = 4f;
        [Tooltip("A landed hit ends the vulnerable window (one specialist action per window).")] public bool EndVulnerableOnHit = true;
        public float DefaultVulnerableSeconds = 4f;
        [Tooltip("Discrete hits: further hits within this many seconds are ignored.")] public float HitCooldown = 0.5f;
        public bool TurnToTarget = true;
        public float TurnSpeed = 140f;
        public string DefeatBanner = "BOSS DEFEATED";
        public string MercyBanner = "HR SENT A MEMO ABOUT WORKPLACE SAFETY";
        public int MaxMinions = 2;

        // ------------------------------------------------------------------ state (B13)
        public int Phase { get; private set; }
        public int Hits { get; private set; }
        public BossState State { get; private set; } = BossState.Dormant;
        public float StateTime { get; private set; }
        public bool IsVulnerable => State == BossState.Vulnerable;
        public float VulnerableLeft => State == BossState.Vulnerable ? Mathf.Max(0f, _vulnLeft) : 0f;
        public float NextAttackIn => State == BossState.Fighting ? Attacks.NextAttackIn : float.PositiveInfinity;
        public ShifterCharacter Target { get; private set; }
        public int MercyLevel { get; set; }
        public int WipesThisPhase { get; private set; }
        public BossAttackRunner Attacks => _attacks ??= new BossAttackRunner(this);
        public int PhaseCount => Phases != null ? Phases.Length : 0;
        public BossPhaseSpec CurrentPhase => Phases != null && Phase >= 0 && Phase < Phases.Length ? Phases[Phase] : default;
        public int HitsRequired => Mathf.Clamp(CurrentPhase.HitsRequired <= 0 ? 1 : CurrentPhase.HitsRequired, 1, 3);
        public bool IsDefeated => State == BossState.Defeated;
        public bool IsFighting => State == BossState.Fighting || State == BossState.Vulnerable;
        public Vector3 HomePosition { get; private set; }
        /// <summary>Facing at fight start (captured in Start): the body turns back to it during vulnerable windows so the
        /// weak points face the team (e.g. after a sweep spun it around).</summary>
        public Quaternion HomeRotation { get; private set; } = Quaternion.identity;
        public Vector3 BodyForward { get { var f = (Body != null ? Body : transform).forward; f.y = 0f; return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward; } }
        public float TellScale => MercyLevel > 0 ? 1.3f : 1f;
        public float DamageScale => MercyLevel > 0 ? 0.7f : 1f;
        public int RejectedHits { get; private set; }
        public IReadOnlyList<GameObject> Minions => _minions;

        /// <summary>Boss status changed (hides SignalSource.Changed on purpose; receivers wired to the boss as a
        /// SignalSource still get the ON edge through the base event).</summary>
        public new event Action<BossBase> Changed;

        BossAttackRunner _attacks;
        float _vulnLeft, _introDuration, _lastHitTime = -99f, _targetTimer;
        int _startFrames = 2;
        bool _hiddenAfterDefeat, _homeSet, _homeRotSet;
        readonly List<GameObject> _minions = new List<GameObject>(2);
        readonly HashSet<string> _managedGroups = new HashSet<string>();
        // later-phase checkpoints disabled at level start (a disabled Checkpoint leaves Checkpoint.All, so Find() can't see it)
        readonly List<Checkpoint> _heldCheckpoints = new List<Checkpoint>(4);

        // wipes / mercy survive Restart Checkpoint (scene reload); keyed by BossId
        static readonly Dictionary<string, int> s_wipes = new Dictionary<string, int>();
        static readonly Dictionary<string, int> s_mercy = new Dictionary<string, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_wipes.Clear(); s_mercy.Clear(); }

        /// <summary>Forget wipes/mercy of every boss (tests, new run).</summary>
        public static void ClearMercyMemory() { s_wipes.Clear(); s_mercy.Clear(); }

        // ------------------------------------------------------------------ brain hooks
        protected abstract void OnPhaseBegin(int phase);
        protected virtual void OnHit(int hitsLeft) { }
        protected virtual void OnDefeated() { }
        protected virtual void OnIntro() { }
        protected virtual void OnPhaseEnd(int phase) { }
        protected virtual void OnVulnerableChanged(bool vulnerable) { }
        protected virtual void OnHitRejected(BossHitKind kind, Vector3 point) { Fx.FloatingText(point, "CLANG!", Color.white, 0.8f); Sfx.At(SfxId.MetalHit, point, 0.6f, 0.8f); }
        protected virtual void OnAttackStart(BossTelegraph t) { }
        protected virtual void OnAttackImpact(BossTelegraph t) { }
        protected virtual void OnAttackEnd(BossTelegraph t) { }
        protected virtual void OnVictimHit(ShifterCharacter c, BossTelegraph t) { }
        /// <summary>Spook landed on c (already stunned 2 s): Boogeyman drags it to the broom closet here.</summary>
        protected virtual void OnSpook(ShifterCharacter c) { }
        protected virtual void OnSpookMissed(ShifterCharacter c) { }
        /// <summary>Summon attack: build minion i at pos (return null for none). The base keeps at most MaxMinions alive.</summary>
        protected virtual GameObject SpawnMinion(int index, Vector3 pos) => null;
        protected virtual void OnMercy() { }
        /// <summary>Extra target filter (Boogeyman: not lit). Default: everyone.</summary>
        protected virtual bool IsTargetable(ShifterCharacter c) => true;

        public virtual SfxId TellSfx(BossAttackKind k)
        {
            switch (k)
            {
                case BossAttackKind.Charge: return SfxId.BossCharge;
                case BossAttackKind.Sweep: return SfxId.Kazoo;
                case BossAttackKind.Spook: return SfxId.Whisper;
                case BossAttackKind.Summon: return SfxId.Klaxon;
                case BossAttackKind.SkateLoop: return SfxId.Klaxon;
                default: return SfxId.BossRoar;
            }
        }

        public virtual SfxId ImpactSfx(BossAttackKind k)
        {
            switch (k)
            {
                case BossAttackKind.SlamZone: return SfxId.BossStomp;
                case BossAttackKind.Shockwave: return SfxId.Shockwave;
                case BossAttackKind.Charge: return SfxId.BossCharge;
                case BossAttackKind.Sweep: return SfxId.Kazoo;
                case BossAttackKind.Spook: return SfxId.Boo;
                case BossAttackKind.Volley: return SfxId.Popper;
                default: return SfxId.BossStomp;
            }
        }

        public virtual string HitText(BossAttackKind k) => k == BossAttackKind.Spook ? "BOO!" : k == BossAttackKind.Charge ? "WHAM!" : "BONK!";

        /// <summary>Aim point when there is no target and no mark (default: 6 m in front of home).</summary>
        public virtual Vector3 FallbackAim()
        {
            var c = Arena != null ? Arena.Mark("Center") : null;
            return c != null ? c.position : HomePosition + BodyForward * 6f;
        }

        public virtual void MoveBody(Vector3 pos)
        {
            var b = Body != null ? Body : transform;
            pos.y = b.position.y;
            b.position = pos;
        }

        public virtual void AimBody(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            var b = Body != null ? Body : transform;
            b.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>B4: who an attack may damage. ActiveOnly: the active intern. Loneliest: the attack's locked target.</summary>
        public virtual bool IsVictim(ShifterCharacter c, BossTelegraph t)
        {
            if (c == null || !c.IsAlive) return false;
            if (Rule == BossTargetRule.ActiveOnly) return c.IsActive;
            var tg = t != null && t.Target != null ? t.Target : Target;
            return c == tg;
        }

        /// <summary>Spook succeeds when nobody else stands within radius of c (teamwork beats fear).</summary>
        public virtual bool CanSpook(ShifterCharacter c, float radius)
        {
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var o = team[i];
                if (o == null || o == c || !o.IsAlive) continue;
                if (BossFootprint.Planar(o.Feet, c.Feet) <= radius) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ runner callbacks (internal plumbing)
        internal void NotifyAttack(BossTelegraph t, int stage)
        {
            if (stage == 0) OnAttackStart(t); else if (stage == 1) OnAttackImpact(t); else OnAttackEnd(t);
        }
        internal void NotifyVictim(ShifterCharacter c, BossTelegraph t) => OnVictimHit(c, t);
        internal void SpookLanded(ShifterCharacter c) => OnSpook(c);
        internal void SpookMissed(ShifterCharacter c) => OnSpookMissed(c);

        internal void Summon(int count, Vector3 at)
        {
            _minions.RemoveAll(m => m == null);
            for (int i = 0; i < count && _minions.Count < Mathf.Min(2, MaxMinions); i++)
            {
                var pos = at + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 2f;
                if (Arena != null) pos = Arena.PushOutOfSafeZones(pos, 1f);
                var m = SpawnMinion(_minions.Count, pos);
                if (m != null) _minions.Add(m);
            }
        }

        // ------------------------------------------------------------------ lifecycle
        protected virtual void Awake()
        {
            if (Body == null) Body = transform;
            CaptureHome();
            if (Arena != null && Arena.Boss == null) Arena.Boss = this;
        }

        void CaptureHome() { if (_homeSet) return; _homeSet = true; HomePosition = (Body != null ? Body : transform).position; }

        /// <summary>Re-capture the home position (call after moving the boss in a builder at runtime).</summary>
        public void SetHome(Vector3 world) { _homeSet = true; HomePosition = world; }
        /// <summary>Re-capture home position + facing (builders that move/turn the boss at runtime).</summary>
        public void SetHome(Vector3 world, Quaternion rotation) { SetHome(world); _homeRotSet = true; HomeRotation = rotation; }

        protected virtual void OnEnable() { GameEvents.TeamWiped += OnTeamWiped; }
        protected virtual void OnDisable() { GameEvents.TeamWiped -= OnTeamWiped; }
        protected virtual void OnDestroy() { _attacks?.DisposeVisuals(); }

        protected virtual void Start()
        {
            if (!_homeRotSet) { _homeRotSet = true; HomeRotation = (Body != null ? Body : transform).rotation; }
            if (Arena != null) Arena.Boss = this;
            CollectManagedGroups();
            ApplyHazardGroups(-1);
        }

        protected virtual void Update()
        {
            if (_startFrames > 0 && --_startFrames == 0) AfterLevelStart();
            if (State == BossState.Dormant && AutoStart && _startFrames == 0)
            {
                var a = BossTeam.Active;
                if (a != null && a.IsAlive && (Arena == null || Arena.Contains(a.Feet))) Begin();
            }
            if (TurnToTarget && State == BossState.Fighting) TurnBody();
            else if (State == BossState.Vulnerable || State == BossState.PhaseTransition)
            {
                var b = Body != null ? Body : transform;
                b.rotation = Quaternion.RotateTowards(b.rotation, HomeRotation, TurnSpeed * 2f * Time.deltaTime);
            }
        }

        protected virtual void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            StateTime += dt;
            _targetTimer -= dt;
            if (_targetTimer <= 0f) { _targetTimer = 0.2f; UpdateTarget(); }
            if (State != BossState.Dormant) SlideRidersOff(dt);
            switch (State)
            {
                case BossState.Intro:
                    if (StateTime >= _introDuration) StartPhase(Phase, false);
                    break;
                case BossState.Fighting:
                    Attacks.Tick(dt);
                    break;
                case BossState.Vulnerable:
                    _vulnLeft -= dt;
                    if (_vulnLeft <= 0f) EndVulnerable();
                    break;
                case BossState.PhaseTransition:
                    if (StateTime >= TransitionSeconds) StartPhase(Phase + 1, true);
                    break;
                case BossState.Defeated:
                    if (!_hiddenAfterDefeat && StateTime >= DefeatedHideSeconds) { _hiddenAfterDefeat = true; RaiseStatus(); }
                    break;
            }
        }

        void SlideRidersOff(float dt)
        {
            var team = BossTeam.Members;
            var root = Body != null ? Body : transform;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive || c.Motor == null) continue;
                var g = c.Motor.GroundCollider;
                if (g == null || !(g.transform == root || g.transform.IsChildOf(root) || g.transform.IsChildOf(transform))) continue;
                BossAttackRunner.SlideOff(c, root.position, dt);
            }
        }

        void TurnBody()
        {
            var cur = Attacks.Current;
            if (cur != null && cur.Stage != BossTelegraphStage.Windup) return;   // charges/sweeps steer the body themselves
            Vector3 look;
            if (cur != null) look = (cur.Kind == BossAttackKind.SlamZone || cur.Kind == BossAttackKind.Spook ? cur.Aim : cur.End) - (Body != null ? Body.position : transform.position);
            else if (Target != null) look = Target.Feet - (Body != null ? Body.position : transform.position);
            else return;
            look.y = 0f;
            if (look.sqrMagnitude < 0.25f) return;
            var b = Body != null ? Body : transform;
            b.rotation = Quaternion.RotateTowards(b.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), TurnSpeed * Time.deltaTime);
        }

        void AfterLevelStart()
        {
            DisableLaterPhaseCheckpoints();
            if (State == BossState.Dormant && AutoBeginOnRestore && FightReached()) Begin();
        }

        // ------------------------------------------------------------------ public API
        /// <summary>Start the fight (intro, then the phase restored from the checkpoint index). No-op unless Dormant.</summary>
        public void Begin()
        {
            if (State != BossState.Dormant) return;
            CaptureHome();
            int p = PhaseFromCheckpoint();
            Phase = p;
            Hits = 0;
            LoadMercy();
            _introDuration = p > 0 ? RestoreIntroSeconds : IntroSeconds;
            SetState(BossState.Intro);
            if (p == 0) GameEvents.RaiseBanner(BossName, BossTitle, BannerStyle.Boss);
            else GameEvents.RaiseBanner(BossName, "BACK FOR MORE: " + PhaseLabel(p), BannerStyle.Boss);
            Sfx.At(SfxId.BossRoar, HomePosition, 1f);
            OnIntro();
            if (_introDuration <= 0f) StartPhase(p, false);
        }

        /// <summary>B7: jump to a phase (checkpoint restore, tests). Short intro, then the phase starts.</summary>
        public void RestorePhase(int phase)
        {
            phase = Mathf.Clamp(phase, 0, Mathf.Max(0, PhaseCount - 1));
            Attacks.Cancel();
            MoveBody(HomePosition);
            SetOn(false);
            Phase = phase;
            Hits = 0;
            LoadMercy();
            _introDuration = RestoreIntroSeconds;
            SetState(BossState.Intro);
            if (_introDuration <= 0f) StartPhase(phase, false);
        }

        /// <summary>Open the weak-point window for seconds (&lt;= 0: the phase's VulnerableSeconds, else DefaultVulnerableSeconds).</summary>
        public void SetVulnerable(float seconds)
        {
            if (State != BossState.Fighting && State != BossState.Vulnerable) return;
            if (seconds <= 0f) seconds = CurrentPhase.VulnerableSeconds > 0f ? CurrentPhase.VulnerableSeconds : DefaultVulnerableSeconds;
            Attacks.Cancel();
            MoveBody(HomePosition);
            _vulnLeft = seconds;
            bool was = State == BossState.Vulnerable;
            SetState(BossState.Vulnerable);
            if (!was)
            {
                Sfx.At(SfxId.Gurgle, HomePosition, 0.9f);
                Fx.FloatingText(HomePosition + Vector3.up * 4f, "OPENING!", UIColors.AccentYellow, 1.4f);
                OnVulnerableChanged(true);
            }
        }

        public void EndVulnerable()
        {
            if (State != BossState.Vulnerable) return;
            _vulnLeft = 0f;
            SetState(BossState.Fighting);
            Attacks.Delay(0.8f);
            OnVulnerableChanged(false);
        }

        /// <summary>Would a hit of this kind count right now? (weak points light up / bots check this).</summary>
        public bool CanAccept(BossHitKind kind)
        {
            if (State != BossState.Fighting && State != BossState.Vulnerable) return false;
            var ps = CurrentPhase;
            if ((ps.Accepts & kind) == 0) return false;
            if (ps.HitsOnlyWhenVulnerable && State != BossState.Vulnerable) return false;
            return true;
        }

        public void Hit(BossHitKind kind, Vector3 point, ShifterCharacter by) => TryHit(kind, point, by);

        /// <summary>Hit() returning whether it counted (filtered by phase kinds, state, the window and HitCooldown).</summary>
        public bool TryHit(BossHitKind kind, Vector3 point, ShifterCharacter by)
        {
            if (!CanAccept(kind))
            {
                if (State != BossState.Defeated && State != BossState.Dormant) { RejectedHits++; OnHitRejected(kind, point); }
                return false;
            }
            if (Time.time - _lastHitTime < HitCooldown) return false;
            _lastHitTime = Time.time;
            Hits++;
            int left = HitsRequired - Hits;
            Sfx.At(SfxId.BossHit, point, 1f, 0.9f);
            Fx.Sparks(point, Vector3.up, 20, UIColors.AccentYellow);
            Fx.FloatingText(point + Vector3.up * 0.6f, left > 0 ? "HIT! " + left + " TO GO" : "PHASE CLEAR!", UIColors.AccentYellow, 1.3f);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.25f, 0.25f);
            if (by != null) by.Say(left > 0 ? "Gotcha!" : "TEAMWORK!");
            OnHit(left);
            if (left <= 0) { CompletePhase(); return true; }
            if (EndVulnerableOnHit && State == BossState.Vulnerable) EndVulnerable();
            RaiseStatus();
            return true;
        }

        public void ResetState()
        {
            Attacks.Cancel();
            for (int i = 0; i < _minions.Count; i++) if (_minions[i] != null) Kit.SafeDestroy(_minions[i]);
            _minions.Clear();
            MoveBody(HomePosition);
            Phase = 0; Hits = 0; _vulnLeft = 0f; _hiddenAfterDefeat = false;
            SetOn(false);
            SetState(BossState.Dormant);
            ApplyHazardGroups(-1);
        }

        /// <summary>Force the defeat (cutscene skip, tests).</summary>
        public void ForceDefeat() { if (State != BossState.Defeated) Defeat(); }

        public string PhaseLabel(int p) => Phases != null && p >= 0 && p < Phases.Length && !string.IsNullOrEmpty(Phases[p].Name) ? Phases[p].Name : "PHASE " + (p + 1);

        public BossStatus BuildStatus() => new BossStatus
        {
            BossId = BossId, Name = BossName, Title = BossTitle, BarLabel = BarLabel,
            Phase = Phase, PhaseCount = PhaseCount, Hits = Hits, HitsRequired = HitsRequired,
            State = State,
            Visible = State != BossState.Dormant && !(State == BossState.Defeated && _hiddenAfterDefeat)
        };

        /// <summary>Raise Changed + GameEvents.BossChanged (brains call it after changing BarLabel).</summary>
        public void RaiseStatus()
        {
            try { Changed?.Invoke(this); } catch (Exception e) { Debug.LogException(e); }
            GameEvents.RaiseBossChanged(BuildStatus());
        }

        // ------------------------------------------------------------------ validation (B1/B3), also used by tests
        /// <summary>Static B1/B3 check of every phase/attack spec: windups &gt;= 1.5 s (phase 0) / 1.2 s, hits 1..3, accepts set.</summary>
        public bool ValidateSpecs(out string error)
        {
            var sb = new System.Text.StringBuilder();
            if (Phases == null || Phases.Length == 0) sb.Append("no phases; ");
            else for (int p = 0; p < Phases.Length; p++)
            {
                var ps = Phases[p];
                if (ps.HitsRequired > 3) sb.Append("P" + p + " needs " + ps.HitsRequired + " hits (B1 max 3); ");
                if (ps.Accepts == BossHitKind.None) sb.Append("P" + p + " accepts no hit kind; ");
                if (ps.Pattern == null) continue;
                float min = BossAttackRunner.MinTelegraph(p);
                for (int i = 0; i < ps.Pattern.Length; i++)
                    if (ps.Pattern[i].Windup < min - 1e-4f) sb.Append("P" + p + "#" + i + " " + ps.Pattern[i].Kind + " windup " + ps.Pattern[i].Windup + " s < " + min + " s; ");
                int ck = ps.CheckpointIndex;
                if (p > 0 && ck > 0 && Phases[p - 1].CheckpointIndex >= ck) sb.Append("P" + p + " checkpoint " + ck + " not after P" + (p - 1) + "'s; ");
            }
            error = sb.ToString();
            return error.Length == 0;
        }

        // ------------------------------------------------------------------ internals
        void SetState(BossState s)
        {
            State = s;
            StateTime = 0f;
            RaiseStatus();
        }

        void StartPhase(int p, bool announce)
        {
            if (Phases == null || Phases.Length == 0) { Defeat(); return; }
            p = Mathf.Clamp(p, 0, Phases.Length - 1);
            Phase = p;
            Hits = 0;
            _vulnLeft = 0f;
            LoadMercy();
            var ps = Phases[p];
            if (ps.CheckpointIndex > 0) { ReleaseHeldCheckpoint(ps.CheckpointIndex); BossCompat.ActivateCheckpoint(ps.CheckpointIndex); }
            ReviveDownedAtPhase(ps.CheckpointIndex);
            ApplyHazardGroups(p);
            if (!string.IsNullOrEmpty(ps.IntroBeat)) GameEvents.RaiseStoryBeat(ps.IntroBeat);
            if (announce) GameEvents.RaiseBanner(PhaseLabel(p), BossName, BannerStyle.Boss);
            Attacks.SetPattern(ps.Pattern, p);
            SetState(BossState.Fighting);
            OnPhaseBegin(p);
            RaiseStatus();
        }

        static void ReviveDownedAtPhase(int checkpointIndex)
        {
            // ActivateFromScript/TryActivate revive Downed teammates; if the checkpoint was already active, do it here.
            var mgr = CheckpointManager.Instance;
            var cm = CharacterManager.Instance;
            if (mgr == null || cm == null) return;
            var cp = checkpointIndex > 0 ? CheckpointManager.Find(checkpointIndex) : mgr.Current;
            if (cp == null) cp = mgr.Current;
            if (cp == null) return;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c != null && c.State == CharacterState.Downed) mgr.ReviveAt(cp, c);
            }
        }

        void CompletePhase()
        {
            Attacks.Cancel();
            MoveBody(HomePosition);
            s_wipes.Remove(WipeKey(Phase));
            WipesThisPhase = 0;
            OnPhaseEnd(Phase);
            if (Phase >= PhaseCount - 1) { Defeat(); return; }
            Sfx.At(SfxId.BossRoar, HomePosition, 1f, 0.8f);
            SetState(BossState.PhaseTransition);
        }

        void Defeat()
        {
            Attacks.Cancel();
            MoveBody(HomePosition);
            _hiddenAfterDefeat = false;
            for (int p = 0; p < PhaseCount; p++) s_wipes.Remove(WipeKey(p));
            s_mercy.Remove(BossId ?? "");
            SetState(BossState.Defeated);
            ApplyHazardGroups(-1);
            Sfx.At(SfxId.BossDefeat, HomePosition, 1f);
            Fx.Confetti(HomePosition + Vector3.up * 3f);
            GameEvents.RaiseBanner(DefeatBanner, BossName, BannerStyle.Boss);
            SetOn(true);
            OnDefeated();
            RaiseStatus();
        }

        string WipeKey(int p) => (BossId ?? "") + "#" + p;

        void LoadMercy()
        {
            WipesThisPhase = s_wipes.TryGetValue(WipeKey(Phase), out int w) ? w : 0;
            if (s_mercy.TryGetValue(BossId ?? "", out int m)) MercyLevel = Mathf.Max(MercyLevel, m);
        }

        void OnTeamWiped() => RegisterWipe();

        /// <summary>Count one team wipe in the current phase (GameEvents.TeamWiped calls it; tests may too). 2 wipes = mercy (B10).</summary>
        public void RegisterWipe()
        {
            if (State == BossState.Dormant || State == BossState.Defeated) return;
            string key = WipeKey(Phase);
            s_wipes.TryGetValue(key, out int w);
            s_wipes[key] = ++w;
            WipesThisPhase = w;
            if (w >= 2 && MercyLevel == 0 && HRAssistsOn())
            {
                MercyLevel = 1;
                s_mercy[BossId ?? ""] = 1;
                GameEvents.RaiseBanner(MercyBanner, "Longer warnings. Softer bonks.", BannerStyle.Boss);
                var gs = GameSession.Instance;
                if (gs != null && gs.Stats != null) gs.Stats.HRInterventions++;
                OnMercy();
            }
            RaiseStatus();
        }

        static bool HRAssistsOn()
        {
            var gs = GameSession.Instance;
            return gs == null || gs.Settings == null || gs.Settings.HRAssists;
        }

        void UpdateTarget()
        {
            ShifterCharacter t = null;
            if (Rule == BossTargetRule.ActiveOnly)
            {
                var a = BossTeam.Active;
                if (Valid(a)) t = a;
            }
            else
            {
                var team = BossTeam.Members;
                float best = -1f;
                for (int i = 0; i < team.Count; i++)
                {
                    var c = team[i];
                    if (!Valid(c) || !BossCompat.IsScared(c)) continue;
                    float iso = float.PositiveInfinity;
                    for (int k = 0; k < team.Count; k++)
                    {
                        var o = team[k];
                        if (o == null || o == c || !o.IsAlive) continue;
                        iso = Mathf.Min(iso, BossFootprint.Planar(o.Feet, c.Feet));
                    }
                    if (float.IsPositiveInfinity(iso)) iso = 9999f;
                    if (iso > best + 0.01f) { best = iso; t = c; }
                }
            }
            if (t != Target) { Target = t; }
        }

        bool Valid(ShifterCharacter c)
        {
            if (c == null || !c.IsAlive) return false;
            if (c.Condition == CharacterCondition.Carried || c.Condition == CharacterCondition.Seated) return false;
            if (Arena != null && (!Arena.Contains(c.Feet, 2f) || Arena.InSafeZone(c.Feet))) return false;
            return IsTargetable(c);
        }

        int PhaseFromCheckpoint()
        {
            var mgr = CheckpointManager.Instance;
            int idx = mgr != null ? mgr.CurrentIndex : -1;
            int p = 0;
            if (Phases != null)
                for (int i = 0; i < Phases.Length; i++)
                {
                    int ck = Phases[i].CheckpointIndex;
                    if (ck > 0 && ck <= idx) p = i;
                }
            return p;
        }

        bool FightReached()
        {
            var mgr = CheckpointManager.Instance;
            if (mgr == null || Phases == null) return false;
            for (int i = 0; i < Phases.Length; i++) { int ck = Phases[i].CheckpointIndex; if (ck > 0 && ck <= mgr.CurrentIndex) return true; }
            return false;
        }

        void DisableLaterPhaseCheckpoints()
        {
            if (Phases == null) return;
            var mgr = CheckpointManager.Instance;
            int cur = mgr != null ? mgr.CurrentIndex : -1;
            for (int i = 1; i < Phases.Length; i++)
            {
                int ck = Phases[i].CheckpointIndex;
                if (ck <= 0 || ck <= cur) continue;
                var cp = CheckpointManager.Find(ck);
                if (cp == null && Arena != null && Arena.PhaseCheckpoints != null)
                    foreach (var a in Arena.PhaseCheckpoints) if (a != null && a.Index == ck) cp = a;
                if (cp != null) { cp.enabled = false; if (!_heldCheckpoints.Contains(cp)) _heldCheckpoints.Add(cp); }
            }
        }

        /// <summary>Re-enable a phase checkpoint held back at level start (so CheckpointManager.Find sees it again).</summary>
        void ReleaseHeldCheckpoint(int index)
        {
            for (int i = _heldCheckpoints.Count - 1; i >= 0; i--)
            {
                var cp = _heldCheckpoints[i];
                if (cp == null) { _heldCheckpoints.RemoveAt(i); continue; }
                if (cp.Index != index) continue;
                cp.enabled = true;
                _heldCheckpoints.RemoveAt(i);
            }
            if (CheckpointManager.Find(index) == null && Arena != null && Arena.PhaseCheckpoints != null)
                foreach (var a in Arena.PhaseCheckpoints) if (a != null && a.Index == index && !a.enabled) a.enabled = true;
        }

        void CollectManagedGroups()
        {
            _managedGroups.Clear();
            if (Phases == null) return;
            foreach (var ps in Phases) if (ps.HazardGroupsOn != null) foreach (var g in ps.HazardGroupsOn) if (!string.IsNullOrEmpty(g)) _managedGroups.Add(g);
        }

        void ApplyHazardGroups(int phase)
        {
            if (Arena == null || _managedGroups.Count == 0) return;
            string[] on = Phases != null && phase >= 0 && phase < Phases.Length ? Phases[phase].HazardGroupsOn : null;
            foreach (var g in Arena.HazardGroups())
            {
                if (!_managedGroups.Contains(g.name)) continue;
                bool active = on != null && Array.IndexOf(on, g.name) >= 0;
                if (g.gameObject.activeSelf != active) g.gameObject.SetActive(active);
            }
        }
    }
}
