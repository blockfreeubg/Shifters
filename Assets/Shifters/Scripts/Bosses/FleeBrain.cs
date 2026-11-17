// =====================================================================================
//  SHIFTERS - FleeBrain.cs   [BOSSES module - CH13 P4 SOLO POD, CAMPAIGN §12.1]
//  Max's 105 kg gold hover-chair. Hovers along a fixed Loop (figure-8; points LOCAL to the pod's parent)
//  at Speed (7 m/s: faster than any non-turbo sprint, slower than turbo), always choosing the loop
//  direction that runs away from the nearest intern. Every MonologueEvery s it stops for MonologueFor s
//  to boast. STUN routes (DizzyFor s; the pod drops to the floor and becomes grabbable):
//    Sprinter TAG at >= TagSpeed . touch during a MONOLOGUE . any gun/tool hit (IShootable) .
//    a thrown teammate . TAUNT within TauntRadius -> he zooms over to ARGUE (hovers ArgueDistance away
//    for ArgueFor s, grabbable; touching him then also stuns).
//  Carrying (Grabbable, Brute only at 105 kg) keeps him dizzy; released he stays dizzy >= 1.5 s, then
//  rises and rejoins the loop. Capture() (DETENTION) ends the brain for good.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public sealed class FleeBrain : MonoBehaviour, IShootable, IResettable
    {
        [Tooltip("Loop points, LOCAL to this object's parent (world if none). Closed loop.")] public Vector3[] Loop = new Vector3[0];
        public float Speed = 7f, MonologueEvery = 7f, MonologueFor = 3f, DizzyFor = 3f;
        public float HoverHeight = 0.6f, TagSpeed = 8f, TauntRadius = 9f, ArgueDistance = 1.7f, ArgueFor = 2.5f, TouchRadius = 1.4f;
        public string[] MonologueLines = { "I RULE ALONE!", "TEAMWORK IS A SCAM!", "ONE MAN. ONE VOTE. MINE.", "BEHOLD: ME!", "I DELEGATE TO MYSELF!" };
        public string[] ArgueLines = { "EXCUSE ME?!", "HOW DARE YOU!", "SAY THAT TO MY FACE!" };

        [Tooltip("World-space box (x/z used); size zero = off. Taunters outside are ignored ('NOT WORTH MY TIME.') and the argue hover is clamped inside.")]
        public Bounds ArgueArea;
        [Tooltip("At most one loop-direction reversal per this many seconds (no jitter between two flanking interns)...")]
        public float MinReverseInterval = 1f;
        [Tooltip("...except an immediate one when an intern stands this close ahead along the loop.")]
        public float PanicReverseDistance = 1.5f;
        public Rigidbody Body;
        public Grabbable Grab;

        public bool IsDizzy => _dizzy > 0f || IsCarried;
        public bool IsMonologuing { get; private set; }
        /// <summary>Arguing with a taunter (hovering in place, grabbable).</summary>
        public bool IsDistracted { get; private set; }
        public bool IsCarried => Grab != null && Grab.IsHeld;
        public bool IsCaptured { get; private set; }
        public bool IsFleeing => !IsCaptured && !IsDizzy && !IsMonologuing && !IsDistracted;
        public float DizzyLeft => Mathf.Max(0f, _dizzy);
        public int StunCount { get; private set; }
        public int MonologueCount { get; private set; }
        /// <summary>Loop-direction reversals so far (tests: &lt;= 1 per MinReverseInterval unless panicking).</summary>
        public int Reversals { get; private set; }
        public bool ArgueAreaOn => ArgueArea.size.x > 0f && ArgueArea.size.z > 0f;
        public bool InArgueArea(Vector3 p) => !ArgueAreaOn || (p.x >= ArgueArea.min.x && p.x <= ArgueArea.max.x && p.z >= ArgueArea.min.z && p.z <= ArgueArea.max.z);
        public string LastStunCause { get; private set; } = "";
        public ShifterCharacter Taunter { get; private set; }

        public event Action<FleeBrain, string> Stunned;
        public event Action<FleeBrain, string> Monologue;

        float _clock, _lastReverse = -99f, _snubbedAt = -99f;
        float _s, _total, _dir = 1f, _dirTimer, _mono, _monoLeft, _dizzy, _argue, _rejoin;
        float[] _cum;
        Vector3[] _world;
        int _line;
        bool _airborneDrop;

        void Awake()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (Grab == null) Grab = GetComponent<Grabbable>();
            BuildLoop();
            if (Body != null) { Body.isKinematic = true; Body.mass = Tuning.SoloPodKg; }
            if (Grab != null) { Grab.CanBeGrabbed = false; Grab.Released += OnReleased; Grab.Grabbed += OnGrabbed; }
            _s = NearestParam(transform.position);
        }

        // Factories (BossKit.SoloPod) set Loop after AddComponent (= after Awake): rebuild the loop here.
        void Start()
        {
            BuildLoop();
            _s = NearestParam(transform.position);
        }

        void OnDestroy() { if (Grab != null) { Grab.Released -= OnReleased; Grab.Grabbed -= OnGrabbed; } }
        void OnEnable() { GameEvents.Noise += OnNoise; }
        void OnDisable() { GameEvents.Noise -= OnNoise; }

        void BuildLoop()
        {
            int n = Loop != null ? Loop.Length : 0;
            _world = new Vector3[n];
            _cum = new float[n + 1];
            var parent = transform.parent;
            for (int i = 0; i < n; i++) _world[i] = parent != null ? parent.TransformPoint(Loop[i]) : Loop[i];
            for (int i = 0; i < n; i++) _cum[i + 1] = _cum[i] + Vector3.Distance(_world[i], _world[(i + 1) % n]);
            _total = n > 1 ? _cum[n] : 0f;
        }

        Vector3 LoopPoint(float s)
        {
            int n = _world.Length;
            if (n == 0) return transform.position;
            if (n == 1 || _total <= 0f) return _world[0];
            s = Mathf.Repeat(s, _total);
            for (int i = 0; i < n; i++)
                if (s <= _cum[i + 1]) { float l = _cum[i + 1] - _cum[i]; return Vector3.Lerp(_world[i], _world[(i + 1) % n], l > 1e-4f ? (s - _cum[i]) / l : 0f); }
            return _world[0];
        }

        float NearestParam(Vector3 p)
        {
            if (_world == null || _world.Length < 2) return 0f;
            float best = float.PositiveInfinity, bs = 0f;
            for (float s = 0f; s < _total; s += 0.5f) { float d = BossFootprint.Planar(LoopPoint(s), p); if (d < best) { best = d; bs = s; } }
            return bs;
        }

        // ------------------------------------------------------------------ API
        public void Stun(float seconds) => StunInternal(seconds, "stun");

        void StunInternal(float seconds, string cause)
        {
            if (IsCaptured || IsCarried) return;
            bool was = _dizzy > 0f;
            _dizzy = Mathf.Max(_dizzy, seconds);
            IsMonologuing = false; IsDistracted = false; Taunter = null;
            if (!was)
            {
                StunCount++;
                LastStunCause = cause;
                if (Body != null) { Body.isKinematic = false; Body.useGravity = true; Body.linearVelocity = Vector3.down * 1f; }
                if (Grab != null) Grab.CanBeGrabbed = true;
                Fx.Stars(transform, Mathf.Max(1f, seconds));
                Sfx.At(SfxId.Bonk, transform.position, 1f, 0.7f);
                Fx.FloatingText(transform.position + Vector3.up * 2f, "DIZZY!", UIColors.AccentYellow, 1.3f);
                Stunned?.Invoke(this, cause);
            }
        }

        /// <summary>TAUNT reaction: zoom to ArgueDistance from the taunter and argue for ArgueFor s (grabbable).</summary>
        public void Argue(ShifterCharacter taunter)
        {
            if (taunter == null || IsCaptured || IsDizzy) return;
            Taunter = taunter;
            IsDistracted = true;
            IsMonologuing = false;
            _argue = ArgueFor;
            if (Grab != null) Grab.CanBeGrabbed = true;
            Fx.FloatingText(transform.position + Vector3.up * 2.2f, ArgueLines[(_line++) % ArgueLines.Length], UIColors.Danger, 1.2f);
            Sfx.At(SfxId.Kazoo, transform.position, 0.8f, 0.8f);
        }

        /// <summary>DETENTION: stop for good (the pod stays where it is, not grabbable).</summary>
        public void Capture()
        {
            IsCaptured = true;
            IsDistracted = IsMonologuing = false;
            _dizzy = 0f;
            if (Grab != null) { if (Grab.Holder != null) Grab.Holder.ForceRelease(); Grab.CanBeGrabbed = false; }
            if (Body != null) { Body.isKinematic = false; Body.useGravity = true; }
        }

        public void ResetState()
        {
            IsCaptured = false; IsDistracted = false; IsMonologuing = false; _dizzy = 0f; _mono = 0f; _rejoin = 0f;
            if (Grab != null) { if (Grab.Holder != null) Grab.Holder.ForceRelease(); Grab.CanBeGrabbed = false; }
            if (Body != null) { Body.isKinematic = true; Body.useGravity = false; }
            _s = 0f;
            SetPose(LoopPoint(0f) + Vector3.up * HoverHeight, transform.rotation);
        }

        public void OnShot(in ShotInfo shot) => StunInternal(DizzyFor, "shot");

        void OnGrabbed(CharacterGrabber by) { IsDistracted = false; IsMonologuing = false; _dizzy = Mathf.Max(_dizzy, DizzyFor); }

        void OnReleased(Vector3 v, bool thrown)
        {
            if (IsCaptured) return;
            _dizzy = Mathf.Max(_dizzy, 1.5f);
            if (Body != null) { Body.isKinematic = false; Body.useGravity = true; }
            if (Grab != null) Grab.CanBeGrabbed = true;
        }

        void OnNoise(NoiseEvent e)
        {
            if (e.Source == null || !e.Source.IsAlive || IsCaptured || IsDizzy) return;
            if (Mathf.Abs(e.Radius - Tuning.NoiseTaunt) > 0.01f) return;                     // only taunts
            if (BossFootprint.Planar(e.Source.Feet, transform.position) > TauntRadius) return;
            if (!InArgueArea(e.Source.Feet))
            {
                if (_clock - _snubbedAt > 1.5f) { _snubbedAt = _clock; Fx.FloatingText(transform.position + Vector3.up * 2.2f, "NOT WORTH MY TIME.", UIColors.Danger, 1.1f); }
                return;
            }
            Argue(e.Source);
        }

        // ------------------------------------------------------------------ loop
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _clock += dt;
            if (IsCaptured) return;
            if (IsCarried) return;
            if (_dizzy > 0f)
            {
                _dizzy -= dt;
                if (_dizzy <= 0f) Recover();
                return;
            }
            CheckContacts();
            if (_dizzy > 0f) return;
            if (IsDistracted)
            {
                _argue -= dt;
                var t = Taunter;
                if (t != null && t.IsAlive)
                {
                    var away = transform.position - t.Feet; away.y = 0f;
                    var goal = t.Feet + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * ArgueDistance + Vector3.up * HoverHeight;
                    if (ArgueAreaOn)
                    {
                        goal.x = Mathf.Clamp(goal.x, ArgueArea.min.x, ArgueArea.max.x);
                        goal.z = Mathf.Clamp(goal.z, ArgueArea.min.z, ArgueArea.max.z);
                        goal.y = (_world != null && _world.Length > 0 ? _world[0].y : GroundY(goal)) + HoverHeight;
                    }
                    else goal.y = GroundY(goal) + HoverHeight;
                    // zoom in to argue, but never back away from the taunter: walking up to him touches/grabs him
                    bool far = BossFootprint.Planar(transform.position, t.Feet) > ArgueDistance + 0.3f;
                    var p = far ? Vector3.MoveTowards(transform.position, goal, Speed * 1.4f * dt) : transform.position;
                    var look = t.Feet - p; look.y = 0f;
                    SetPose(p, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look.normalized) : transform.rotation);
                }
                if (_argue <= 0f || t == null) { IsDistracted = false; Taunter = null; if (Grab != null) Grab.CanBeGrabbed = false; _rejoin = 1f; }
                return;
            }
            if (IsMonologuing)
            {
                _monoLeft -= dt;
                if (_monoLeft <= 0f) { IsMonologuing = false; if (Grab != null) Grab.CanBeGrabbed = false; }
                return;
            }
            _mono += dt;
            if (_mono >= MonologueEvery)
            {
                _mono = 0f;
                IsMonologuing = true;
                _monoLeft = MonologueFor;
                MonologueCount++;
                string line = MonologueLines.Length > 0 ? MonologueLines[(MonologueCount - 1) % MonologueLines.Length] : "ME!";
                Fx.FloatingText(transform.position + Vector3.up * 2.4f, line, UIColors.Danger, 1.4f);
                Sfx.At(SfxId.Cymbal, transform.position, 0.6f);
                Monologue?.Invoke(this, line);
                return;
            }
            Flee(dt);
        }

        void Recover()
        {
            _dizzy = 0f;
            if (Grab != null) Grab.CanBeGrabbed = false;
            if (Body != null) { Body.isKinematic = true; Body.useGravity = false; }
            _rejoin = 1f;
            _mono = 0f;
            Fx.FloatingText(transform.position + Vector3.up * 2f, "I'M BACK!", UIColors.Danger, 1.1f);
        }

        void Flee(float dt)
        {
            if (_world.Length < 2) return;
            var threat = NearestIntern(out float threatDist);
            _dirTimer -= dt;
            if (_clock - _lastReverse >= 0.3f && InternAhead()) Reverse();   // panic: never fly into an intern
            else if (_dirTimer <= 0f && threat != null)
            {
                _dirTimer = 0.25f;
                float fwd = BossFootprint.Planar(LoopPoint(_s + 2.5f), threat.Feet), back = BossFootprint.Planar(LoopPoint(_s - 2.5f), threat.Feet);
                if (Mathf.Abs(fwd - back) > 0.3f)
                {
                    float want = fwd >= back ? 1f : -1f;
                    if (want != _dir && _clock - _lastReverse >= MinReverseInterval) Reverse();
                }
            }
            if (_rejoin > 0f)
            {
                // fly back to the loop after a stun/argue: straight to the nearest loop point
                _s = NearestParam(transform.position);
                var tp = LoopPoint(_s) + Vector3.up * HoverHeight;
                var p = Vector3.MoveTowards(transform.position, tp, Speed * dt);
                SetPose(p, Face(tp - transform.position));
                if ((p - tp).sqrMagnitude < 0.04f) _rejoin = 0f;
                return;
            }
            _s += _dir * Speed * dt;
            var pos = LoopPoint(_s) + Vector3.up * HoverHeight;
            SetPose(pos, Face(LoopPoint(_s + _dir) - LoopPoint(_s)));
        }

        Quaternion Face(Vector3 d) { d.y = 0f; return d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized) : transform.rotation; }

        void SetPose(Vector3 p, Quaternion r)
        {
            if (Body != null && Body.isKinematic) { Body.MovePosition(p); Body.MoveRotation(r); }
            else transform.SetPositionAndRotation(p, r);
        }

        float GroundY(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
                return hit.point.y;
            return p.y - HoverHeight;
        }

        void Reverse() { _dir = -_dir; _lastReverse = _clock; Reversals++; }

        bool InternAhead()
        {
            if (PanicReverseDistance <= 0f) return false;
            var fwd = LoopPoint(_s + _dir * 0.5f) - LoopPoint(_s); fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return false;
            fwd.Normalize();
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive) continue;
                var d = c.Feet - transform.position; d.y = 0f;
                if (d.magnitude <= PanicReverseDistance && Vector3.Dot(d, fwd) > 0.2f) return true;
            }
            return false;
        }

        ShifterCharacter NearestIntern(out float dist)
        {
            dist = float.PositiveInfinity; ShifterCharacter best = null;
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive) continue;
                float d = BossFootprint.Planar(c.Feet, transform.position);
                if (d < dist) { dist = d; best = c; }
            }
            return best;
        }

        void CheckContacts()
        {
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive) continue;
                float d = BossFootprint.Planar(c.Feet, transform.position);
                if (d > TouchRadius + c.Radius) continue;
                if (Mathf.Abs(c.Feet.y - transform.position.y) > 2.2f) continue;
                if (c.Condition == CharacterCondition.Thrown) { StunInternal(DizzyFor, "thrown teammate"); return; }
                var v = c.Motor != null ? c.Motor.PlanarVelocity : Vector3.zero;
                if (v.magnitude >= TagSpeed) { StunInternal(DizzyFor, "tag"); return; }
                if (IsMonologuing) { StunInternal(DizzyFor, "monologue touch"); return; }
                if (IsDistracted) { StunInternal(DizzyFor, "argue touch"); return; }
            }
        }
    }
}
