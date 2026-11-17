// =====================================================================================
//  SHIFTERS - BossAttackRunner.cs   [BOSSES module - CAMPAIGN §11.1 B2/B3/B4/B6/B12]
//  Executes a phase's FIXED attack loop on the fixed timestep (scaled time, no RNG): for each spec
//  WINDUP (telegraph: SFX + floor decal fill + SPD chip) -> ACTIVE (the hit volume) -> RECOVER.
//  Exactly one attack exists at a time (B3). Targets follow the boss's rule (B4): only valid victims are
//  damaged (30 dmg + tumble/stun, scaled by mercy); everybody else overlapping a volume is pushed
//  aside at <= 2 m/s and never over a ledge. Aim points are clamped out of the break areas and
//  projectiles fizzle at their edges (B6). Volley projectiles: pooled, raycast-stepped, <= 10 (B12).
//  Bots read IsThreatened / NextThreatTime / Active / NextAttackIn.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class BossAttackRunner
    {
        public const int MaxProjectiles = 10;
        public const float PushSpeed = 2f;
        const float StartDelay = 1.2f;

        readonly BossBase _boss;
        BossAttackSpec[] _pattern = new BossAttackSpec[0];
        int _phase, _next;
        float _gap;
        bool _running;
        readonly BossTelegraph _cur = new BossTelegraph();
        readonly BossTelegraph _pred = new BossTelegraph();
        readonly List<BossTelegraph> _active = new List<BossTelegraph>(1);
        int _hitMask, _shots;
        bool _impacted;
        float _chargeDist;              // Charge: distance actually travelled (stops before bystanders, B4)
        Vector3 _chargeReached;
        BossTelegraphView _view;
        GameObject _ring;
        readonly Projectile[] _proj = new Projectile[MaxProjectiles];
        static readonly RaycastHit[] s_hits = new RaycastHit[12];

        sealed class Projectile { public GameObject Go; public Vector3 Pos, Dir; public float Travel; public bool Live; }

        public BossAttackRunner(BossBase boss) { _boss = boss; }

        // ------------------------------------------------------------------ public API (B13)
        public IReadOnlyList<BossTelegraph> Active => _active;
        public BossTelegraph Current => _running ? _cur : null;
        public bool Running => _running;
        public bool Paused { get; set; }
        /// <summary>Fixed-step seconds since SetPattern.</summary>
        public float Clock { get; private set; }
        /// <summary>Attacks started since the boss was created.</summary>
        public int Serial { get; private set; }
        /// <summary>FNV-1a over (phase, pattern index, kind, start tick) of every attack: identical across seeds (B2).</summary>
        public uint TimelineHash { get; private set; } = 2166136261u;
        public int PatternLength => _pattern.Length;
        public int NextIndex => _next;
        public int LiveProjectiles { get { int n = 0; for (int i = 0; i < _proj.Length; i++) if (_proj[i] != null && _proj[i].Live) n++; return n; } }
        /// <summary>Raised on the attack's windup start, impact (active start) and end (after recover).</summary>
        public event Action<BossTelegraph> Started, Impact, Ended;

        public static float MinTelegraph(int phase) => phase <= 0 ? Tuning.BossTelegraphFirst : Tuning.BossTelegraphMin;

        public void ResetHash() { TimelineHash = 2166136261u; Serial = 0; }

        public void SetPattern(BossAttackSpec[] pattern, int phase, float startDelay = StartDelay)
        {
            Cancel();
            _pattern = pattern ?? new BossAttackSpec[0];
            _phase = phase;
            _next = 0;
            _gap = startDelay;
            Clock = 0f;
        }

        /// <summary>Delay the next attack start (e.g. after a vulnerable window).</summary>
        public void Delay(float seconds) { if (!_running) _gap = Mathf.Max(_gap, seconds); }

        public void Cancel()
        {
            if (_running)
            {
                _running = false;
                EndVisuals();
                if (_cur.Kind == BossAttackKind.Charge || _cur.Kind == BossAttackKind.SkateLoop) _boss.MoveBody(_boss.HomePosition);
            }
            _active.Clear();
        }

        /// <summary>Seconds until the next impact (0 while an attack is active).</summary>
        public float NextAttackIn
        {
            get
            {
                if (_pattern.Length == 0) return float.PositiveInfinity;
                if (_running)
                {
                    if (_cur.Stage == BossTelegraphStage.Windup) return _cur.TimeToActive;
                    if (_cur.Stage == BossTelegraphStage.Active) return 0f;
                    return _cur.Remaining + WindupOf(_pattern[_next]);
                }
                return Mathf.Max(0f, _gap) + WindupOf(_pattern[_next]);
            }
        }

        public bool IsThreatened(Vector3 p, float horizonSeconds) => NextThreatTime(p) <= horizonSeconds;

        /// <summary>Seconds until an attack volume could cover p (0 = now; +inf = no known threat). Uses the current
        /// telegraph and, for fixed-footprint kinds, the predicted next attack of the loop.</summary>
        public float NextThreatTime(Vector3 p)
        {
            float best = float.PositiveInfinity;
            if (_boss.Arena != null && _boss.Arena.InSafeZone(p)) return best;
            if (_running && _cur.Stage != BossTelegraphStage.Recover && _cur.Covers(p)) best = _cur.Stage == BossTelegraphStage.Active ? 0f : _cur.TimeToActive;
            if (_pattern.Length > 0 && !Paused)
            {
                var s = _pattern[_next];
                if (BossAttackDefaults.FixedFootprint(s.Kind))
                {
                    Fill(_pred, s, _next, false);
                    if (_pred.Covers(p))
                    {
                        float t = (_running ? _cur.Remaining : Mathf.Max(0f, _gap)) + _pred.WindupSeconds;
                        best = Mathf.Min(best, t);
                    }
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ timing
        float WindupOf(in BossAttackSpec s) => Mathf.Max(s.Windup, MinTelegraph(_phase)) * _boss.TellScale;

        public void Tick(float dt)
        {
            if (Paused || dt <= 0f) return;
            Clock += dt;
            if (!_running)
            {
                if (_pattern.Length == 0) return;
                _gap -= dt;
                if (_gap > 0f) return;
                StartAttack(_next);
                _next = (_next + 1) % _pattern.Length;
                return;
            }
            _cur.Elapsed += dt;
            if (_cur.Stage == BossTelegraphStage.Windup && _cur.Elapsed >= _cur.WindupSeconds) BeginActive();
            if (_cur.Stage == BossTelegraphStage.Active)
            {
                TickActive(dt);
                if (_cur.Elapsed >= _cur.WindupSeconds + _cur.ActiveSeconds) EndActive();
            }
            if (_cur.Stage == BossTelegraphStage.Recover)
            {
                if (_cur.Kind == BossAttackKind.Charge || _cur.Kind == BossAttackKind.SkateLoop)
                {
                    float t = _cur.RecoverSeconds > 0f ? Mathf.Clamp01((_cur.Elapsed - _cur.WindupSeconds - _cur.ActiveSeconds) / _cur.RecoverSeconds) : 1f;
                    var from = _cur.Kind == BossAttackKind.Charge ? _chargeReached : (_cur.Path != null && _cur.Path.Length > 0 ? _cur.Path[0] : _cur.Origin);
                    _boss.MoveBody(Vector3.Lerp(from, _boss.HomePosition, PuzzleUtil.Ease(t)));
                }
                if (_cur.Elapsed >= _cur.WindupSeconds + _cur.ActiveSeconds + _cur.RecoverSeconds) Finish();
            }
        }

        void Hash(int v) { unchecked { TimelineHash = (TimelineHash ^ (uint)v) * 16777619u; } }

        void StartAttack(int index)
        {
            Fill(_cur, _pattern[index], index, true);
            _cur.Serial = ++Serial;
            _cur.Elapsed = 0f;
            _cur.Stage = BossTelegraphStage.Windup;
            _hitMask = 0; _shots = 0; _impacted = false;
            _chargeDist = 0f; _chargeReached = _cur.Origin;
            _running = true;
            _active.Clear();
            _active.Add(_cur);
            Hash(_phase); Hash(index); Hash((int)_cur.Kind); Hash(Mathf.RoundToInt(Clock * 50f));
            EnsureView();
            if (_view != null) _view.Show(_cur);
            Sfx.At(_boss.TellSfx(_cur.Kind), _cur.Kind == BossAttackKind.SlamZone ? _cur.Aim : _boss.HomePosition, 0.9f);
            _boss.NotifyAttack(_cur, 0);
            Started?.Invoke(_cur);
        }

        /// <summary>Resolve geometry + timing of spec into t. lockTarget = false for predictions (no target).</summary>
        void Fill(BossTelegraph t, in BossAttackSpec s, int index, bool lockTarget)
        {
            var arena = _boss.Arena;
            t.Kind = s.Kind; t.Spec = s; t.Phase = _phase; t.PatternIndex = index;
            t.Radius = BossAttackDefaults.Radius(s); t.Range = BossAttackDefaults.Range(s); t.Speed = BossAttackDefaults.Speed(s);
            t.Damage = BossAttackDefaults.Damage(s);
            t.WindupSeconds = WindupOf(s);
            t.RecoverSeconds = BossAttackDefaults.Recover(s);
            t.Path = null;
            t.Origin = StaticOrigin(_boss, s);
            var mark = arena != null ? arena.Mark(s.TargetMark) : null;
            ShifterCharacter target = null;
            if (lockTarget && (s.TargetActive || s.Kind == BossAttackKind.Spook)) target = _boss.Target;
            if (target != null && arena != null && arena.InSafeZone(target.Feet)) target = null;
            t.Target = target;
            Vector3 aim = target != null ? target.Feet : mark != null ? mark.position : _boss.FallbackAim();
            aim.y = t.Origin.y;
            switch (s.Kind)
            {
                case BossAttackKind.SlamZone:
                    if (arena != null) aim = arena.PushOutOfSafeZones(aim, t.Radius);
                    t.ActiveSeconds = s.Active > 0f ? s.Active : 0.35f;
                    break;
                case BossAttackKind.Shockwave:
                    t.ActiveSeconds = s.Active > 0f ? s.Active : t.Range / Mathf.Max(0.5f, t.Speed);
                    break;
                case BossAttackKind.Charge:
                {
                    var d = aim - t.Origin; d.y = 0f;
                    if (d.sqrMagnitude < 0.01f) d = _boss.BodyForward;
                    t.End = arena != null ? ClampCharge(arena, t.Origin, t.Origin + d.normalized * t.Range, t.Radius, t.Range) : t.Origin + d.normalized * t.Range;
                    t.ActiveSeconds = s.Active > 0f ? s.Active : Mathf.Max(0.2f, BossFootprint.Planar(t.Origin, t.End) / Mathf.Max(0.5f, t.Speed));
                    break;
                }
                case BossAttackKind.Sweep:
                    t.ActiveSeconds = s.Active > 0f ? s.Active : 180f / Mathf.Max(10f, t.Speed);
                    break;
                case BossAttackKind.Volley:
                {
                    var d = aim - t.Origin; d.y = 0f;
                    if (d.sqrMagnitude < 0.01f) d = _boss.BodyForward;
                    t.End = t.Origin + d.normalized * t.Range;
                    t.ActiveSeconds = s.Active > 0f ? s.Active : 1.4f;
                    break;
                }
                case BossAttackKind.Spook:
                    t.ActiveSeconds = s.Active > 0f ? s.Active : 0.3f;
                    break;
                case BossAttackKind.SkateLoop:
                {
                    t.Path = LoopPath(_boss, s);
                    float len = 0f;
                    if (t.Path != null) for (int i = 0; i < t.Path.Length; i++) len += BossFootprint.Planar(t.Path[i], t.Path[(i + 1) % t.Path.Length]);
                    t.ActiveSeconds = s.Active > 0f ? s.Active : Mathf.Max(0.5f, len / Mathf.Max(0.5f, t.Speed));
                    if (t.Path != null && t.Path.Length > 0) t.Origin = t.Path[0];
                    break;
                }
                default:
                    t.ActiveSeconds = s.Active > 0f ? s.Active : 0.5f;
                    break;
            }
            t.Aim = aim;
            if (s.Kind != BossAttackKind.Charge && s.Kind != BossAttackKind.Volley) t.End = aim;
        }

        void BeginActive()
        {
            _cur.Stage = BossTelegraphStage.Active;
            _impacted = true;
            var k = _cur.Kind;
            Sfx.At(_boss.ImpactSfx(k), k == BossAttackKind.SlamZone ? _cur.Aim : _boss.HomePosition, 1f);
            switch (k)
            {
                case BossAttackKind.SlamZone:
                    Fx.Puff(_cur.Aim + Vector3.up * 0.2f, _cur.Radius * 0.9f, new Color(0.9f, 0.85f, 0.75f));
                    Fx.Sparks(_cur.Aim + Vector3.up * 0.1f, Vector3.up, 16, UIColors.AccentYellow);
                    Shake(0.35f, 0.3f);
                    break;
                case BossAttackKind.Shockwave:
                    EnsureRing();
                    if (_ring != null) { _ring.SetActive(true); _ring.transform.position = _cur.Origin + Vector3.up * 0.15f; _ring.transform.localScale = new Vector3(0.1f, 1f, 0.1f); }
                    Shake(0.25f, 0.25f);
                    break;
                case BossAttackKind.Spook:
                    var tg = _cur.Target;
                    if (tg != null && tg.IsAlive && !InSafe(tg.Feet) && _boss.CanSpook(tg, _cur.Radius))
                    {
                        tg.Motor?.AddImpulse((tg.Feet - _boss.HomePosition).normalized * 1.5f + Vector3.up * 1f, false, 2f);
                        _boss.SpookLanded(tg);
                    }
                    else _boss.SpookMissed(tg);
                    break;
                case BossAttackKind.Summon:
                    _boss.Summon(Mathf.Clamp(Mathf.RoundToInt(_cur.Range > 0f ? _cur.Range : 2f), 1, 2), _cur.Aim);
                    break;
            }
            if (_view != null && k == BossAttackKind.Shockwave) _view.Hide();
            _boss.NotifyAttack(_cur, 1);
            Impact?.Invoke(_cur);
        }

        void EndActive()
        {
            _cur.Stage = BossTelegraphStage.Recover;
            EndVisuals();
            if (_cur.Kind == BossAttackKind.Charge) Shake(0.2f, 0.2f);
        }

        void Finish()
        {
            _running = false;
            _active.Clear();
            _gap = 0f;
            _boss.NotifyAttack(_cur, 2);
            Ended?.Invoke(_cur);
        }

        void EndVisuals()
        {
            if (_view != null) _view.Hide();
            if (_ring != null) _ring.SetActive(false);
            for (int i = 0; i < _proj.Length; i++) if (_proj[i] != null && _proj[i].Live) Despawn(_proj[i], false);
        }

        // ------------------------------------------------------------------ active volumes
        void TickActive(float dt)
        {
            float te = _cur.ActiveElapsed;
            var team = BossTeam.Members;
            switch (_cur.Kind)
            {
                case BossAttackKind.SlamZone:
                    for (int i = 0; i < team.Count; i++)
                    {
                        var c = team[i];
                        if (c == null || !c.IsAlive) continue;
                        if (BossFootprint.Planar(c.Feet, _cur.Aim) > _cur.Radius + c.Radius || Mathf.Abs(c.Feet.y - _cur.Aim.y) > 2.5f) continue;
                        Affect(c, _cur.Aim, dt, Away(c.Feet, _cur.Aim));
                    }
                    break;
                case BossAttackKind.Shockwave:
                {
                    float r = Mathf.Min(_cur.Range, _cur.Speed * te);
                    if (_ring != null) _ring.transform.localScale = new Vector3(Mathf.Max(0.1f, r), 1f, Mathf.Max(0.1f, r));
                    for (int i = 0; i < team.Count; i++)
                    {
                        var c = team[i];
                        if (c == null || !c.IsAlive) continue;
                        float d = BossFootprint.Planar(c.Feet, _cur.Origin);
                        if (Mathf.Abs(d - r) > _cur.Radius + c.Radius) continue;
                        if (c.Feet.y - _cur.Origin.y > 0.8f) continue;                       // jumped over it
                        Affect(c, _cur.Origin, dt, Away(c.Feet, _cur.Origin));
                    }
                    break;
                }
                case BossAttackKind.Charge:
                {
                    float len = BossFootprint.Planar(_cur.Origin, _cur.End);
                    float t = _cur.ActiveSeconds > 0f ? Mathf.Clamp01(te / _cur.ActiveSeconds) : 1f;
                    var dir = len > 0.01f ? (_cur.End - _cur.Origin) / len : _boss.BodyForward;
                    dir.y = 0f;
                    // B4: the charge never runs over a bystander (non-victim): it brakes in front of them while they are
                    // pushed sideways out of the lane at <= 2 m/s (no tumble, never launched onto the boss).
                    float want = len * t, limit = want;
                    for (int i = 0; i < team.Count; i++)
                    {
                        var c = team[i];
                        if (c == null || !c.IsAlive || _boss.IsVictim(c, _cur) || InSafe(c.Feet)) continue;
                        var rel = c.Feet - _cur.Origin; rel.y = 0f;
                        float along = Vector3.Dot(rel, dir);
                        float lat = (rel - dir * along).magnitude;
                        float clear = _cur.Radius + c.Radius + 0.3f;
                        if (lat < clear && along > _chargeDist - 0.2f) limit = Mathf.Min(limit, along - clear);
                    }
                    _chargeDist = Mathf.Max(_chargeDist, Mathf.Min(want, limit));
                    var pos = _cur.Origin + dir * _chargeDist;
                    pos.y = _cur.Origin.y;
                    _chargeReached = pos;
                    _boss.MoveBody(pos);
                    for (int i = 0; i < team.Count; i++)
                    {
                        var c = team[i];
                        if (c == null || !c.IsAlive) continue;
                        float reach = _cur.Radius + c.Radius + (_boss.IsVictim(c, _cur) ? 0f : 0.7f);
                        if (BossFootprint.Planar(c.Feet, pos) > reach) continue;
                        var side = Vector3.Cross(Vector3.up, dir); if (Vector3.Dot(c.Feet - pos, side) < 0f) side = -side;
                        Affect(c, pos - dir, dt, side);
                    }
                    break;
                }
                case BossAttackKind.Sweep:
                {
                    var fwd = _cur.Aim - _cur.Origin; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = _boss.BodyForward;
                    float a0 = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg - 90f;
                    float ang = a0 + _cur.Speed * te;
                    var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                    var tip = _cur.Origin + dir * _cur.Range;
                    if (_ring != null) _ring.SetActive(false);
                    _boss.AimBody(dir);
                    for (int i = 0; i < team.Count; i++)
                    {
                        var c = team[i];
                        if (c == null || !c.IsAlive) continue;
                        if (BossFootprint.SegmentDistance(c.Feet, _cur.Origin, tip) > _cur.Radius + c.Radius) continue;
                        if (c.Feet.y - _cur.Origin.y > 0.8f) continue;
                        Affect(c, _cur.Origin, dt, Quaternion.Euler(0f, ang + 90f, 0f) * Vector3.forward);
                    }
                    break;
                }
                case BossAttackKind.Volley:
                {
                    const int count = 3;
                    float every = _cur.ActiveSeconds / (count + 0.5f);
                    while (_shots < count && te >= _shots * every) { Fire(_shots, count); _shots++; }
                    StepProjectiles(dt, team);
                    break;
                }
                case BossAttackKind.SkateLoop:
                {
                    var path = _cur.Path;
                    if (path == null || path.Length < 2) break;
                    var pos = PointOnLoop(path, _cur.Speed * te);
                    _boss.MoveBody(pos);
                    for (int i = 0; i < team.Count; i++)
                    {
                        var c = team[i];
                        if (c == null || !c.IsAlive) continue;
                        if (BossFootprint.Planar(c.Feet, pos) > _cur.Radius + c.Radius) continue;
                        Affect(c, pos, dt, Away(c.Feet, pos));
                    }
                    break;
                }
            }
        }

        static Vector3 Away(Vector3 p, Vector3 from) { var d = p - from; d.y = 0f; return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward; }

        static Vector3 PointOnLoop(Vector3[] path, float s)
        {
            float total = 0f;
            for (int i = 0; i < path.Length; i++) total += BossFootprint.Planar(path[i], path[(i + 1) % path.Length]);
            if (total <= 0.01f) return path[0];
            s = Mathf.Repeat(s, total);
            for (int i = 0; i < path.Length; i++)
            {
                var a = path[i]; var b = path[(i + 1) % path.Length];
                float l = BossFootprint.Planar(a, b);
                if (s <= l) return Vector3.Lerp(a, b, l > 0f ? s / l : 0f);
                s -= l;
            }
            return path[0];
        }

        bool InSafe(Vector3 p) => _boss.Arena != null && _boss.Arena.InSafeZone(p);

        /// <summary>B4: damage a valid victim once per attack; push anybody else gently aside.</summary>
        void Affect(ShifterCharacter c, Vector3 from, float dt, Vector3 pushDir)
        {
            if (InSafe(c.Feet)) return;
            if (_boss.IsVictim(c, _cur)) Damage(c, from);
            else Push(c, pushDir, dt);
        }

        void Damage(ShifterCharacter c, Vector3 from)
        {
            int bit = 1 << Mathf.Clamp(c.Slot, 0, 30);
            if ((_hitMask & bit) != 0) return;
            _hitMask |= bit;
            float dmg = _cur.Damage * _boss.DamageScale;
            var dir = Away(c.Feet, from);
            if (dmg > 0f) c.Damage(dmg, Vector3.zero, _boss.BossName);
            if (c.IsAlive && c.Motor != null) c.Motor.AddImpulse(dir * 6.5f + Vector3.up * 3.5f, true, 1.5f);
            Sfx.At(SfxId.BossHit, c.Center, 1f);
            Fx.FloatingText(c.HeadTop + Vector3.up * 0.3f, _boss.HitText(_cur.Kind), UIColors.Danger, 1.1f);
            if (c.IsActive) Shake(0.3f, 0.25f);
            _boss.NotifyVictim(c, _cur);
        }

        static readonly RaycastHit[] s_pushHits = new RaycastHit[8];

        /// <summary>B4 push: &lt;= 2 m/s, no tumble, never off a ledge, never up onto / into anything (walls, the boss).</summary>
        static void Push(ShifterCharacter c, Vector3 dir, float dt)
        {
            if (c.Body == null || c.Body.isKinematic || c.Condition != CharacterCondition.Normal) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            var delta = dir * (PushSpeed * dt);
            var next = c.Body.position + delta;
            // ground under the next position, at (about) the same height: no ledge drop, no climbing onto the boss
            if (!Physics.Raycast(next + Vector3.up * 0.5f, Vector3.down, out var g, 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (Mathf.Abs(g.point.y - c.Feet.y) > 0.3f) return;
            if (g.collider != null && g.collider.attachedRigidbody != null && g.collider.attachedRigidbody.isKinematic && g.collider.GetComponentInParent<BossBase>() != null) return;
            float probe = c.Radius + PushSpeed * dt + 0.08f;
            if (Blocked(c, c.Feet + Vector3.up * 0.45f, dir, probe) || Blocked(c, c.Feet + Vector3.up * 1.2f, dir, probe)) return;
            c.Body.MovePosition(next);
        }

        static bool Blocked(ShifterCharacter c, Vector3 from, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(from, dir, s_pushHits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = s_pushHits[i].collider;
                if (col == null) continue;
                if (c.Body != null && col.attachedRigidbody == c.Body) continue;
                if (col.transform.IsChildOf(c.transform)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Safety net: an intern standing ON the boss (popped up by physics) slides off it at PushSpeed.</summary>
        public static void SlideOff(ShifterCharacter c, Vector3 bossCenter, float dt)
        {
            if (c == null || c.Body == null || c.Body.isKinematic || c.Condition != CharacterCondition.Normal) return;
            var d = c.Feet - bossCenter; d.y = 0f;
            if (d.sqrMagnitude < 1e-3f) d = Vector3.forward;
            c.Body.MovePosition(c.Body.position + d.normalized * (PushSpeed * dt));
        }

        // ------------------------------------------------------------------ projectiles (B12)
        void Fire(int i, int count)
        {
            Projectile p = null;
            for (int k = 0; k < _proj.Length; k++)
            {
                if (_proj[k] == null) _proj[k] = new Projectile();
                if (!_proj[k].Live) { p = _proj[k]; break; }
            }
            if (p == null) return;
            if (p.Go == null) p.Go = BossKit.Projectile(_boss.transform, _cur.Radius);
            var d = _cur.End - _cur.Origin; d.y = 0f; d = d.sqrMagnitude > 1e-4f ? d.normalized : _boss.BodyForward;
            float spread = count > 1 ? (i - (count - 1) * 0.5f) * 9f : 0f;
            p.Dir = Quaternion.Euler(0f, spread, 0f) * d;
            p.Pos = _cur.Origin + Vector3.up * 1.2f + p.Dir * 1.2f;
            p.Travel = 0f;
            p.Live = true;
            p.Go.transform.SetParent(null, true);
            p.Go.transform.position = p.Pos;
            p.Go.SetActive(true);
            Sfx.At(SfxId.Popper, p.Pos, 0.7f, 0.8f);
        }

        void StepProjectiles(float dt, IReadOnlyList<ShifterCharacter> team)
        {
            for (int k = 0; k < _proj.Length; k++)
            {
                var p = _proj[k];
                if (p == null || !p.Live) continue;
                float step = _cur.Speed * dt;
                var next = p.Pos + p.Dir * step;
                // characters: math (ME-VISION: only victims are hit; projectiles pass through others)
                bool done = false;
                for (int i = 0; i < team.Count && !done; i++)
                {
                    var c = team[i];
                    if (c == null || !c.IsAlive || !_boss.IsVictim(c, _cur) || InSafe(c.Feet)) continue;
                    var cc = c.Center; float half = c.Height * 0.5f;
                    var q = ClosestOnSegment(cc, p.Pos, next);
                    var dq = q - cc; float dy = Mathf.Max(0f, Mathf.Abs(dq.y) - half); dq.y = 0f;
                    if (dq.magnitude <= c.Radius + _cur.Radius && dy <= _cur.Radius) { Damage(c, p.Pos - p.Dir); done = true; }
                }
                if (!done && HitsWorld(p.Pos, p.Dir, step)) done = true;
                p.Travel += step;
                if (!done && (p.Travel >= _cur.Range || (_boss.Arena != null && _boss.Arena.InSafeZone(next, _cur.Radius)))) done = true;
                if (done) { Despawn(p, true); continue; }
                p.Pos = next;
                p.Go.transform.position = next;
            }
        }

        static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a; float l2 = ab.sqrMagnitude;
            return l2 > 1e-8f ? a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : a;
        }

        bool HitsWorld(Vector3 from, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(from, dir, s_hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = s_hits[i].collider;
                if (col == null) continue;
                if (col.GetComponentInParent<ShifterCharacter>() != null) continue;
                if (col.transform.IsChildOf(_boss.transform)) continue;
                return true;
            }
            return false;
        }

        void Despawn(Projectile p, bool fx)
        {
            p.Live = false;
            if (p.Go == null) return;
            if (fx) Fx.Puff(p.Pos, 0.6f, new Color(1f, 0.5f, 0.3f));
            p.Go.SetActive(false);
        }

        // ------------------------------------------------------------------ visuals / helpers
        void EnsureView()
        {
            if (_view != null) return;
            var go = BossKit.TelegraphDecal(null, _boss.HomePosition, 1f);
            _view = go != null ? go.GetComponent<BossTelegraphView>() : null;
            if (_view != null) _view.Hide();
        }

        void EnsureRing()
        {
            if (_ring != null) return;
            _ring = BossKit.ShockwaveRing(null, _boss.HomePosition);
            if (_ring != null) _ring.SetActive(false);
        }

        static void Shake(float amp, float dur) { if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(amp, dur); }

        /// <summary>Destroys pooled visuals (boss destroyed).</summary>
        public void DisposeVisuals()
        {
            if (_view != null) Kit.SafeDestroy(_view.gameObject);
            if (_ring != null) Kit.SafeDestroy(_ring);
            for (int i = 0; i < _proj.Length; i++) if (_proj[i] != null && _proj[i].Go != null) Kit.SafeDestroy(_proj[i].Go);
            _view = null; _ring = null;
        }

        // ------------------------------------------------------------------ static geometry (also used by BossArena validation)
        public static Vector3 StaticOrigin(BossBase boss, in BossAttackSpec s)
        {
            var arena = boss.Arena;
            if (s.Kind == BossAttackKind.SkateLoop) { var path = LoopPath(boss, s); if (path != null && path.Length > 0) return path[0]; }
            var mark = arena != null ? arena.Mark(s.TargetMark) : null;
            if (mark != null && !s.TargetActive && BossAttackDefaults.FromOrigin(s.Kind)) return mark.position;
            return boss.HomePosition;
        }

        public static Vector3[] LoopPath(BossBase boss, in BossAttackSpec s)
        {
            var arena = boss.Arena;
            var root = arena != null ? arena.Mark(s.TargetMark) : null;
            if (root == null || root.childCount < 2) return null;
            var pts = new Vector3[root.childCount];
            for (int i = 0; i < pts.Length; i++) pts[i] = root.GetChild(i).position;
            return pts;
        }

        /// <summary>Charge end point: from origin toward aim (&lt;= range), stopping before any break area (radius r) or the bounds.</summary>
        public static Vector3 ClampCharge(BossArena arena, Vector3 origin, Vector3 aim, float r, float range)
        {
            var d = aim - origin; d.y = 0f;
            float len = Mathf.Min(range, d.magnitude);
            if (len < 0.01f) return origin;
            d /= d.magnitude;
            var last = origin;
            for (float s = 0.25f; s <= len + 1e-4f; s += 0.25f)
            {
                var q = origin + d * s;
                if (arena.SafeZoneDistance(q) < r + 0.05f) break;
                var b = arena.Bounds;
                if (q.x < b.min.x + r || q.x > b.max.x - r || q.z < b.min.z + r || q.z > b.max.z - r) break;
                last = q;
            }
            return last;
        }
    }
}
