// =====================================================================================
//  SHIFTERS - L13OneManBandBoss.cs   [LEVEL 13 - THE ONE-MAN BAND (final boss)]   BossBase brain.
//  "I'm the strong one, the fast one AND the smart one." ME-VISION (ActiveOnly) in P1-P3.
//    P1 THE FIST    SlamZone r 2.5 (tell 1.5 s) aimed at the active intern; a MISS gets the fist STUCK 4 s
//                   (6 s if a >= 90 kg prop jams it) -> the Brute punches a knuckle (E) x3 -> ArmR drops off.
//    P2 THE SKATES  SkateLoop CCW round the lane (r 10.5, 9 m/s, forever) until the presser-only BUMP RUN
//                   raises the bumps: B1 BUMP, B2 BUMP, B3 = cartoon flight home -> legs off.
//    P3 BRAIN DOME  kazoo Sweep x2 then a 5 s YAWN; hack both latching FIREWALL terminals (W/E shield halves),
//                   then shoot the EGO ANTENNA once (any gun / tool).
//    P4 CAPTURE     no attacks: the SOLO POD (FleeBrain) flees round the figure-8; stun + Brute carry +
//                   INTAKE hack -> DETENTION -> DETAINED = defeat (north door, C5, C13).
//  HOLD ZONE: P1/P3 tells wait while the active intern is within 3 m of a break area or outside the arena.
//  Phase = checkpoint (C1..C4); C5 restore = Defeated with the pod domed.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L13OneManBandBoss : BossBase
    {
        // ---------------- wiring (L13Builder)
        public OneManBandModel Model;
        public Collider BodyBlock;
        public Transform KnuckleRoot;
        public BossWeakPoint[] Knuckles = new BossWeakPoint[0];
        public BossWeakPoint Antenna;
        public Terminal TerminalW, TerminalE;
        public GameObject CableW, CableE, DomeLight;
        public L13Signal FirewallDown;
        public Transform[] Bumps = new Transform[0];
        public Transform[] Incoming = new Transform[0];
        public L13BumpRun BumpRun;
        public FleeBrain Pod;
        public L13Detention Detention;
        public Transform CageBars;
        public ScriptTrigger WreckTrigger;
        public MeTvScreenView ArenaTv;
        public GameObject TutFirewall, TutCapture;
        public float HoldMargin = 3f, LeglessDrop = 2.2f;
        public int FinalCheckpoint = 5;
        public Vector3 ArmRestPos = new Vector3(-7.5f, 0.7f, 47.2f);

        // ---------------- bot / test API (B13 + level extras)
        public bool FistStuck => Phase == 0 && State == BossState.Vulnerable;
        public Vector3 FistCentre { get; private set; }
        public float JammedUntil { get; private set; }
        public bool BumpsUp => BumpRun != null && BumpRun.IsOn;
        public bool ShieldW { get; private set; } = true;
        public bool ShieldE { get; private set; } = true;
        public bool PodTiny => _shrink != null && _shrink.IsTiny;
        public bool Detained => Detention != null && Detention.IsOn;
        public int Sweeps { get; private set; }
        public int Yawns { get; private set; }
        public bool Yawning => Time.time < _yawnUntil;
        public bool Tripping => _tripT >= 0f;
        public int Trips { get; private set; }
        public int Stucks { get; private set; }
        public int Holds { get; private set; }
        public bool AntennaLive => _antennaLive;
        public bool IsThreatened(Vector3 p, float horizon) => Attacks.IsThreatened(p, horizon);

        enum FistMode { Home, Hover, Slam, Stuck, Return }
        FistMode _fist = FistMode.Home;
        bool _victim, _decided, _jammed, _antennaLive, _yawned, _stuckSeen, _armDropped, _legless, _restoredDefeated, _restoreChecked, _wasCarried, _wasArguing;
        float _armT = -1f, _tripT = -1f, _ejectT = -1f, _yawnUntil, _lastUnion = -99f, _holdT, _zzzT;
        int _frames, _punches;
        bool[] _bumpHit = new bool[3];
        Vector3 _tripFrom, _armFrom, _ejectFrom;
        Quaternion _armFromRot;
        Vector3 _fistLocal = new Vector3(0f, -4.6f, 0f);
        L13PodShrink _shrink;

        static readonly string[] PunchLines = { "KNUCKLE INTEGRITY: 66%.", "KNUCKLE INTEGRITY: 33%.", "OW! MUSCLES, THAT'S ASSAULT ON A FIST!" };

        // ------------------------------------------------------------------ config
        public void Configure()
        {
            BossId = "onemanband"; BossName = "THE ONE-MAN BAND"; BossTitle = "MAX ME, IN A 9 M GOLD ME-CHA"; BarLabel = "APPROVAL RATING: 100%";
            Rule = BossTargetRule.ActiveOnly;
            AutoStart = true; EndVulnerableOnHit = true; TurnToTarget = true;
            DefeatBanner = "HOSTILE TAKEOVER: REVERSED";
            Phases = new[]
            {
                new BossPhaseSpec { Name = "PHASE 1: THE FIST", HitsRequired = 3, Accepts = BossHitKind.Punch, HitsOnlyWhenVulnerable = true, VulnerableSeconds = 4f,
                    CheckpointIndex = 1, IntroBeat = "ch13.p1",
                    Pattern = new[] { BossAttackSpec.Make(BossAttackKind.SlamZone, 1.5f, true, null, 2.5f, 0f, 0f, 0.3f, 1.5f, 30f) } },
                new BossPhaseSpec { Name = "PHASE 2: THE SKATES", HitsRequired = 1, Accepts = BossHitKind.Custom, HitsOnlyWhenVulnerable = false,
                    CheckpointIndex = 2, IntroBeat = "ch13.p2", HazardGroupsOn = new[] { "s2.lane" },
                    Pattern = new[] { BossAttackSpec.Make(BossAttackKind.SkateLoop, 1.2f, false, "s2.loop", 2f, 0f, 9f, 9999f, 0.5f, 30f) } },
                new BossPhaseSpec { Name = "PHASE 3: THE BRAIN DOME", HitsRequired = 3, Accepts = BossHitKind.Hack | BossHitKind.Shoot, HitsOnlyWhenVulnerable = false,
                    CheckpointIndex = 3, IntroBeat = "ch13.p3", HazardGroupsOn = new[] { "s3.cables" },
                    Pattern = new[] { BossAttackSpec.Make(BossAttackKind.Sweep, 1.2f, true, null, 0.6f, 24f, 103f, 1.75f, 1.0f, 30f),
                                      BossAttackSpec.Make(BossAttackKind.Sweep, 1.2f, true, null, 0.6f, 24f, 103f, 1.75f, 1.0f, 30f) } },
                new BossPhaseSpec { Name = "PHASE 4: THE CAPTURE", HitsRequired = 1, Accepts = BossHitKind.Custom, HitsOnlyWhenVulnerable = false,
                    CheckpointIndex = 4, IntroBeat = "ch13.eject", HazardGroupsOn = new[] { "s4.rail" },
                    Pattern = new BossAttackSpec[0] },
            };
        }

        protected override void Awake()
        {
            Configure();
            base.Awake();
            if (Pod != null) _shrink = Pod.GetComponent<L13PodShrink>();
            if (Model != null && Model.Fist != null) _fistLocal = Model.Fist.localPosition;
        }

        protected override void Start()
        {
            base.Start();
            if (Pod != null) { Pod.Stunned += OnPodStunned; Pod.Monologue += OnPodMonologue; }
            if (WreckTrigger != null) WreckTrigger.Fired += OnWreck;
            GameEvents.StoryBeat += OnBeatRelay;
            if (Detention == null || !Detention.IsOn) ShowPod(false);
        }

        protected override void OnDestroy()
        {
            if (Pod != null) { Pod.Stunned -= OnPodStunned; Pod.Monologue -= OnPodMonologue; }
            if (WreckTrigger != null) WreckTrigger.Fired -= OnWreck;
            GameEvents.StoryBeat -= OnBeatRelay;
            base.OnDestroy();
        }

        // ------------------------------------------------------------------ per frame
        protected override void Update()
        {
            _frames++;
            if (_wreckPendingUntil > 0f)
            {
                if (Time.time > _wreckPendingUntil) _wreckPendingUntil = 0f;
                else if (ScareDirector.CanPlay("ch13.wreck")) { _wreckPendingUntil = 0f; PlayWreck(); }
            }
            if (_frames == 2 && !_restoreChecked)
            {
                _restoreChecked = true;
                var mgr = CheckpointManager.Instance;
                if (mgr != null && mgr.CurrentIndex >= FinalCheckpoint) RestoreDefeated();
            }
            base.Update();
            float dt = Time.deltaTime;
            TickHold(dt);
            TickFist(dt);
            TickArm(dt);
            TickSkates(dt);
            TickDome(dt);
            TickPod(dt);
        }

        // ---------------- HOLD ZONE
        public bool InHold(Vector3 p)
        {
            if (Arena == null) return false;
            if (!Arena.Contains(p)) return true;
            var zs = Arena.Zones;
            for (int i = 0; i < zs.Count; i++) if (zs[i] != null && zs[i].Contains(p, HoldMargin)) return true;
            return false;
        }

        void TickHold(float dt)
        {
            if (State != BossState.Fighting || (Phase != 0 && Phase != 2) || Attacks.Running || Yawning) { _holdT = 0f; return; }
            var a = BossTeam.Active;
            if (a == null || !a.IsAlive || !InHold(a.Feet)) { _holdT = 0f; return; }
            Attacks.Delay(0.25f);
            if (_holdT == 0f) Holds++;
            _holdT += dt;
            if (_holdT > 1.5f && Time.time - _lastUnion > 30f)
            {
                _lastUnion = Time.time;
                Beat("ch13.unionbreak");
                Say("ON BREAK? FINE. UNION RULES. ...I WROTE THOSE TOO.");
            }
        }

        // ---------------- P1 THE FIST
        Vector3 ClampFist(Vector3 aim)
        {
            const float r = 2.5f;
            aim.x = Mathf.Clamp(aim.x, -19f + r, 19f - r);
            aim.z = Mathf.Clamp(aim.z, 8f + r, 52f - r);
            for (int it = 0; it < 3; it++)
            {
                aim = PushOut(aim, -2.5f, 2.5f, 40.5f, 44.5f, r + 1.5f);          // mech feet
                aim = PushOut(aim, -19f, -15f, 18.5f, 31f, r + 1.5f);            // W balcony + ramp
                aim = PushOut(aim, 15f, 19f, 18.5f, 31f, r + 1.5f);              // E balcony + ramp
                aim = PushOut(aim, 15f, 19f, 34f, 46f, r + 1.5f);                // Percy pedestal + DETENTION wall + INTAKE
                aim.x = Mathf.Clamp(aim.x, -19f + r, 19f - r);
                aim.z = Mathf.Clamp(aim.z, 8f + r, 52f - r);
                if (Arena != null) aim = Arena.PushOutOfSafeZones(aim, r + 0.5f);
            }
            return aim;
        }

        static Vector3 PushOut(Vector3 p, float x0, float x1, float z0, float z1, float m)
        {
            x0 -= m; x1 += m; z0 -= m; z1 += m;
            if (p.x <= x0 || p.x >= x1 || p.z <= z0 || p.z >= z1) return p;
            float dl = p.x - x0, dr = x1 - p.x, db = p.z - z0, dt = z1 - p.z;
            float best = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
            if (best == dl) p.x = x0; else if (best == dr) p.x = x1; else if (best == db) p.z = z0; else p.z = z1;
            return p;
        }

        protected override void OnAttackStart(BossTelegraph t)
        {
            if (Phase == 0 && t.Kind == BossAttackKind.SlamZone)
            {
                t.Aim = ClampFist(t.Aim); t.End = t.Aim;
                FistCentre = t.Aim;
                _victim = false; _decided = false; _jammed = false;
                _fist = FistMode.Hover;
                if (Model != null) Model.SetFistDetached(true);
                Sfx.At(SfxId.DrumSlam, t.Aim, 0.6f, 1.4f);
            }
            else if (Phase == 2 && t.Kind == BossAttackKind.Sweep)
            {
                if (Model != null) Model.SetKazooArm(-70f, 0f);
                Sfx.At(SfxId.Kazoo, HomePosition, 1f, 0.6f);
            }
        }

        protected override void OnAttackImpact(BossTelegraph t)
        {
            if (Phase != 0 || t.Kind != BossAttackKind.SlamZone) return;
            _fist = FistMode.Slam;
            // JAM: a prop >= 90 kg (BIT-SHIFTER HUGE paperweight) inside the circle at impact
            var hits = Physics.OverlapSphere(t.Aim + Vector3.up * 0.6f, t.Radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic || rb.GetComponent<ShifterCharacter>() != null || rb.mass < 90f) continue;
                if (BossFootprint.Planar(rb.position, t.Aim) > t.Radius) continue;
                _jammed = true;
                rb.AddForce(((rb.position - t.Aim).normalized + Vector3.up) * 6f, ForceMode.VelocityChange);
                break;
            }
        }

        protected override void OnVictimHit(ShifterCharacter c, BossTelegraph t) { if (Phase == 0) _victim = true; }

        protected override void OnAttackEnd(BossTelegraph t)
        {
            if (Phase == 0 && t.Kind == BossAttackKind.SlamZone && _fist != FistMode.Stuck) _fist = FistMode.Return;
            if (Phase == 2 && t.Kind == BossAttackKind.Sweep)
            {
                Sweeps++;
                if (Model != null) Model.SetKazooArm(0f, 0f);
                if (Sweeps % 2 == 0)
                {
                    Attacks.Delay(5f);
                    _yawnUntil = Time.time + 5f; _zzzT = 0f; Yawns++;
                    if (!_yawned) { _yawned = true; Beat("ch13.yawn"); }
                    Say("*YAWN* ...FIVE MORE MINUTES.");
                }
            }
        }

        void TickFist(float dt)
        {
            if (Model == null || Model.Fist == null) return;
            var fist = Model.Fist;
            if (Phase == 0 && State == BossState.Fighting)
            {
                var cur = Attacks.Current;
                if (cur != null && cur.Kind == BossAttackKind.SlamZone && cur.Stage == BossTelegraphStage.Recover && !_decided)
                {
                    _decided = true;
                    if (!_victim)
                    {
                        _fist = FistMode.Stuck; Stucks++;
                        float secs = _jammed ? 6f : 4f;
                        if (_jammed) { JammedUntil = Time.time + secs; GameEvents.RaiseToast("FIST JAMMED!", UIColors.AccentYellow); }
                        SetVulnerable(secs);
                        Fx.Sparks(FistCentre + Vector3.up * 0.3f, Vector3.up, 24, UIColors.AccentYellow);
                        Fx.FloatingText(FistCentre + Vector3.up * 2.6f, "STUCK!", UIColors.AccentYellow, 1.5f);
                        if (!_stuckSeen) { _stuckSeen = true; Beat("ch13.stuck"); Say("WHO INSTALLED SOFT MARBLE?! ...ME. FOR THE AMBIENCE."); }
                    }
                }
            }
            bool knucklesOn = Phase == 0 && State == BossState.Vulnerable && _fist == FistMode.Stuck;
            if (KnuckleRoot != null)
            {
                if (knucklesOn) KnuckleRoot.position = FistCentre + Vector3.up * 0.6f;
                if (KnuckleRoot.gameObject.activeSelf != knucklesOn) KnuckleRoot.gameObject.SetActive(knucklesOn);
            }
            if (_armDropped && _fist != FistMode.Home) { _fist = FistMode.Home; Model.SetFistDetached(false); fist.localPosition = _fistLocal; fist.localRotation = Quaternion.identity; }
            switch (_fist)
            {
                case FistMode.Hover:
                    fist.position = Vector3.MoveTowards(fist.position, FistCentre + Vector3.up * 6f, 30f * dt);
                    fist.rotation = Quaternion.identity;
                    break;
                case FistMode.Slam:
                {
                    var cur = Attacks.Current;
                    float e = cur != null && cur.ActiveSeconds > 0f ? Mathf.Clamp01(cur.ActiveElapsed / cur.ActiveSeconds) : 1f;
                    fist.position = FistCentre + Vector3.up * Mathf.Lerp(6f, 0.6f, e * e);
                    fist.rotation = Quaternion.identity;
                    if (cur == null || cur.Stage == BossTelegraphStage.Recover) fist.position = FistCentre + Vector3.up * 0.6f;
                    break;
                }
                case FistMode.Stuck:
                    fist.position = FistCentre + Vector3.up * 0.6f + new Vector3(Mathf.Sin(Time.time * 40f), 0f, Mathf.Cos(Time.time * 37f)) * 0.03f;
                    fist.rotation = Quaternion.identity;
                    if (State != BossState.Vulnerable) _fist = FistMode.Return;
                    break;
                case FistMode.Return:
                {
                    var home = fist.parent != null ? fist.parent.TransformPoint(_fistLocal) : fist.position;
                    fist.position = Vector3.MoveTowards(fist.position, home, 14f * dt);
                    if ((fist.position - home).sqrMagnitude < 0.04f) { _fist = FistMode.Home; Model.SetFistDetached(false); fist.localPosition = _fistLocal; fist.localRotation = Quaternion.identity; }
                    break;
                }
            }
        }

        protected override void OnVulnerableChanged(bool v)
        {
            if (Phase == 0 && !v && _fist == FistMode.Stuck) { _fist = FistMode.Return; Say("YANK!"); }
        }

        protected override void OnHit(int hitsLeft)
        {
            if (Phase == 0)
            {
                Beat("ch13.punch");
                Say(PunchLines[Mathf.Clamp(_punches++, 0, PunchLines.Length - 1)]);
                _fist = FistMode.Return;
                BarLabel = "APPROVAL RATING: " + (100 - 8 * (3 - hitsLeft)) + "%";
            }
            else if (Phase == 2) BarLabel = "APPROVAL RATING: " + (50 - 8 * (3 - hitsLeft)) + "%";
            RaiseStatus();
        }

        protected override void OnHitRejected(BossHitKind kind, Vector3 point)
        {
            if (kind == BossHitKind.Punch) { Fx.FloatingText(point, "WAIT FOR IT TO GET STUCK!", Color.white, 0.9f); return; }
            base.OnHitRejected(kind, point);
        }

        // ---------------- arm drop (P1 -> P2)
        void StartArmDrop()
        {
            if (_armDropped || Model == null || Model.ArmR == null) { _armDropped = true; return; }
            Model.SetFistDetached(false);
            if (Model.Fist != null) { Model.Fist.localPosition = _fistLocal; Model.Fist.localRotation = Quaternion.identity; }
            Model.ArmR.SetParent(transform.parent, true);
            _armFrom = Model.ArmR.position; _armFromRot = Model.ArmR.rotation;
            _armT = 0f; _armDropped = true; _fist = FistMode.Home;
            Sfx.At(SfxId.MetalHit, _armFrom, 1f, 0.6f);
        }

        void SnapArmDropped()
        {
            if (_armDropped && _armT < 0f) return;
            if (!_armDropped) StartArmDrop();
            _armT = 2f;
            TickArm(0f);
        }

        void TickArm(float dt)
        {
            if (_armT < 0f || Model == null || Model.ArmR == null) return;
            _armT += dt;
            float e = Mathf.Clamp01(_armT / 2f);
            var arm = Model.ArmR;
            var pos = Vector3.Lerp(_armFrom, ArmRestPos, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 2.5f;
            arm.position = pos;
            arm.rotation = Quaternion.Slerp(_armFromRot, Quaternion.Euler(0f, 35f, 90f), e);
            if (e >= 1f)
            {
                _armT = -1f;
                Fx.Puff(ArmRestPos, 2.5f, new Color(0.9f, 0.85f, 0.75f));
                Sfx.At(SfxId.LandHeavy, ArmRestPos, 1f, 0.7f);
            }
        }

        // ---------------- P2 THE SKATES
        void TickSkates(float dt)
        {
            if (Phase != 1) return;
            var p = transform.position;
            if (_tripT >= 0f)
            {
                _tripT += dt;
                float e = Mathf.Clamp01(_tripT / 2.5f);
                var pos = Vector3.Lerp(_tripFrom, HomePosition, PuzzleUtil.Ease(e)) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 7f;
                transform.position = pos;
                transform.rotation = HomeRotation * Quaternion.Euler(-e * 360f, 0f, Mathf.Sin(e * Mathf.PI * 3f) * 20f);
                if (e >= 1f) FinishTrip();
                return;
            }
            var cur = Attacks.Current;
            bool skating = State == BossState.Fighting && cur != null && cur.Kind == BossAttackKind.SkateLoop && cur.Stage == BossTelegraphStage.Active;
            float th = Mathf.Atan2(-p.x, p.z - L13Builder.LaneCenter.z);
            for (int i = 0; i < Incoming.Length; i++)
            {
                var s = Incoming[i];
                if (s == null) continue;
                if (s.gameObject.activeSelf != skating) s.gameObject.SetActive(skating);
                if (!skating) continue;
                float ti = th + (1.4f + i * 1.9f) / L13Builder.LaneR;
                s.position = L13Builder.LanePoint(ti) + Vector3.up * 0.03f;
                s.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(ti), 0f, -Mathf.Sin(ti)));
            }
            if (!skating) return;
            transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(th), 0f, -Mathf.Sin(th)));
            if (Model != null) Model.SetStride(Time.time * 7f, 18f);
            if (p.z > L13Builder.LaneCenter.z + 4f) { _bumpHit[0] = _bumpHit[1] = _bumpHit[2] = false; }
            if (!BumpsUp) return;
            for (int i = 0; i < Bumps.Length && i < 3; i++)
            {
                if (Bumps[i] == null || _bumpHit[i]) continue;
                if (BossFootprint.Planar(p, Bumps[i].position) > 1.5f) continue;
                _bumpHit[i] = true;
                Sfx.At(SfxId.Bounce, Bumps[i].position, 1f, 0.8f);
                Fx.FloatingText(Bumps[i].position + Vector3.up * 3f, "BUMP!", UIColors.AccentYellow, 1.4f);
                if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.2f, 0.2f);
                if (i == 1) Fx.Sparks(p + Vector3.up * 0.5f, Vector3.up, 20, UIColors.AccentYellow);
                if (i == 2 && (_bumpHit[0] || _bumpHit[1])) { StartTrip(); return; }
            }
        }

        void StartTrip()
        {
            _tripFrom = transform.position;
            Attacks.Cancel();
            Attacks.Paused = true;
            transform.position = _tripFrom;
            _tripT = 0f; Trips++;
            for (int i = 0; i < Incoming.Length; i++) if (Incoming[i] != null) Incoming[i].gameObject.SetActive(false);
            if (Model != null) Model.SetBoost(false);
            Sfx.At(SfxId.SlideWhistleDown, _tripFrom, 1f, 1.3f);
            Say("WHOA-OA-OA! WHO PUT SPEED BUMPS ON MY SKATE LANE?!");
        }

        void FinishTrip()
        {
            _tripT = -1f;
            transform.position = HomePosition; transform.rotation = HomeRotation;
            SetLegless(true);
            Fx.Puff(HomePosition, 4f, new Color(0.9f, 0.85f, 0.75f));
            Sfx.At(SfxId.LandHeavy, HomePosition, 1f, 0.6f);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.45f, 0.4f);
            Attacks.Paused = false;
            Beat("ch13.legs");
            TryHit(BossHitKind.Custom, HomePosition + Vector3.up * 3f, null);
        }

        void SetLegless(bool on)
        {
            _legless = on;
            if (Model == null) return;
            foreach (var t in new[] { Model.LegL, Model.LegR, Model.Pelvis }) if (t != null) t.gameObject.SetActive(!on);
            Model.transform.localPosition = on ? Vector3.down * LeglessDrop : Vector3.zero;
            Model.SetBoost(false);
        }

        // ---------------- P3 THE BRAIN DOME
        void TickDome(float dt)
        {
            if (Phase != 2 || (State != BossState.Fighting && State != BossState.Vulnerable)) return;
            if (ShieldW && TerminalW != null && TerminalW.IsOn && TryHit(BossHitKind.Hack, TerminalW.transform.position + Vector3.up * 1.5f, TerminalW.Hacker))
                DropShield(-1);
            if (ShieldE && TerminalE != null && TerminalE.IsOn && TryHit(BossHitKind.Hack, TerminalE.transform.position + Vector3.up * 1.5f, TerminalE.Hacker))
                DropShield(1);
            if (!ShieldW && !ShieldE && !_antennaLive && Phase == 2)
            {
                _antennaLive = true;
                if (Antenna != null) { if (Model != null && Model.AntennaTip != null) Antenna.transform.position = Model.AntennaTip.position; Antenna.gameObject.SetActive(true); }
                if (FirewallDown != null) FirewallDown.Set(true);
                Beat("ch13.firewall");
                Say("GLASSES PERSON! THAT FIREWALL WAS ALSO ME!");
                GameEvents.RaiseToast("EGO ANTENNA EXPOSED: ANY GUN!", UIColors.AccentYellow);
            }
            if (Yawning)
            {
                _zzzT -= dt;
                if (_zzzT <= 0f) { _zzzT = 1.2f; Fx.FloatingText(HomePosition + Vector3.up * 7.5f, "(-_-)zzz", UIColors.Text, 1.2f); }
            }
        }

        void DropShield(int side)
        {
            if (side < 0) { ShieldW = false; if (CableW != null) CableW.SetActive(false); }
            else { ShieldE = false; if (CableE != null) CableE.SetActive(false); }
            if (Model != null) Model.SetShieldHalf(side, false);
            GameEvents.RaiseToast(side < 0 ? "FIREWALL W: OFFLINE" : "FIREWALL E: OFFLINE", UIColors.AccentYellow);
            Sfx.At(SfxId.HackSuccess, HomePosition + Vector3.up * 5f, 1f);
        }

        // ---------------- P4 THE CAPTURE
        void ShowPod(bool on)
        {
            if (Pod == null) return;
            foreach (var r in Pod.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            foreach (var c in Pod.GetComponentsInChildren<Collider>(true)) c.enabled = on;
            foreach (var rv in Pod.GetComponentsInChildren<Revealable>(true)) rv.enabled = on;
        }

        void StartEject()
        {
            if (Pod == null) return;
            ShowPod(true);
            if (Model != null && Model.Max != null) Model.Max.SetActive(false);
            _ejectFrom = Model != null && Model.Cockpit != null ? Model.Cockpit.position : HomePosition + Vector3.up * 5f;
            Pod.transform.position = _ejectFrom;
            Pod.enabled = false;
            _ejectT = 0f;
            Sfx.At(SfxId.Whoosh, _ejectFrom, 1f, 0.8f);
            Say("EJECT! EJECT! I'M TAKING MY CHAIR!");
        }

        void TickPod(float dt)
        {
            if (Pod == null) return;
            if (_ejectT >= 0f)
            {
                _ejectT += dt;
                float e = Mathf.Clamp01(_ejectT / 1.5f);
                var to = L13Builder.PodHome + Vector3.up * Pod.HoverHeight;
                Pod.transform.position = Vector3.Lerp(_ejectFrom, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 4f;
                if (_ejectT >= 2f)
                {
                    _ejectT = -1f;
                    Pod.transform.position = to;
                    Pod.enabled = true;
                    Beat("ch13.chase");
                    Fx.FloatingText(to + Vector3.up * 2.5f, "CATCH ME? NOBODY HAS EVER CAUGHT ME.", UIColors.Danger, 1.2f);
                }
            }
            if (Phase == 3 && Pod.enabled)
            {
                if (Pod.IsCarried && !_wasCarried) Beat("ch13.carried");
                if (Pod.IsDistracted && !_wasArguing) Beat("ch13.argue");
                _wasCarried = Pod.IsCarried; _wasArguing = Pod.IsDistracted;
            }
            if (Phase == 3 && IsFighting && Detained) TryHit(BossHitKind.Custom, L13Builder.CellSeat + Vector3.up, null);
        }

        void OnPodStunned(FleeBrain fb, string cause) { Beat(cause == "tag" ? "ch13.tag" : "ch13.dizzy"); }
        void OnPodMonologue(FleeBrain fb, string line) { Beat("ch13.monologue"); }

        // ------------------------------------------------------------------ phases
        protected override void OnPhaseBegin(int phase)
        {
            if (Phase < 1) { if (BodyBlock != null) BodyBlock.enabled = true; }
            if (phase >= 1) SnapArmDropped();
            if (phase >= 2) SetLegless(true);
            switch (phase)
            {
                case 0:
                    BarLabel = "APPROVAL RATING: 100%";
                    Headline("APPROVAL RATING: 100% (POLLED: ME)");
                    break;
                case 1:
                    BarLabel = "APPROVAL RATING: 75%";
                    Headline("APPROVAL RATING: 75% (RECOUNT PENDING)");
                    if (BodyBlock != null) BodyBlock.enabled = false;
                    if (Model != null) Model.SetBoost(true);
                    _tripT = -1f; Attacks.Paused = false;
                    _bumpHit[0] = _bumpHit[1] = _bumpHit[2] = false;
                    Attacks.Delay(1.8f);
                    Say("ROCKET SKATES! I'M ALSO THE FAST ONE!");
                    break;
                case 2:
                    BarLabel = "APPROVAL RATING: 50%";
                    Headline("APPROVAL RATING: 50% (DECAF DETECTED)");
                    if (BodyBlock != null) BodyBlock.enabled = true;
                    if (BumpRun != null) BumpRun.Lower();
                    ShieldW = ShieldE = true; _antennaLive = false; Sweeps = 0;
                    if (Model != null) { Model.SetShieldHalf(-1, true); Model.SetShieldHalf(1, true); }
                    if (TerminalW != null) TerminalW.ResetState();
                    if (TerminalE != null) TerminalE.ResetState();
                    if (CableW != null) CableW.SetActive(true);
                    if (CableE != null) CableE.SetActive(true);
                    if (FirewallDown != null) FirewallDown.Set(false);
                    if (Antenna != null) Antenna.gameObject.SetActive(false);
                    if (DomeLight != null) DomeLight.SetActive(true);
                    if (TutFirewall != null) TutFirewall.SetActive(true);
                    Attacks.Delay(0.8f);
                    Say("BRAIN DOME ONLINE. I'M ALSO THE SMART ONE. *SIP* ...DECAF?!");
                    break;
                case 3:
                    BarLabel = "APPROVAL RATING: 25%";
                    Headline("APPROVAL RATING: 25% (FLEEING)");
                    if (BodyBlock != null) BodyBlock.enabled = true;
                    if (Model != null) { Model.SetShieldHalf(-1, false); Model.SetShieldHalf(1, false); }
                    if (Antenna != null) Antenna.gameObject.SetActive(false);
                    if (DomeLight != null) DomeLight.SetActive(false);
                    if (TutFirewall != null) TutFirewall.SetActive(false);
                    if (TutCapture != null) TutCapture.SetActive(true);
                    if (Detention != null && !Detention.IsOn) Detention.ResetState();
                    StartEject();
                    break;
            }
            RaiseStatus();
        }

        protected override void OnPhaseEnd(int phase)
        {
            if (phase == 0) { Beat("ch13.arm"); Say("MY ARM! THAT WAS MY GOOD ARM!"); StartArmDrop(); }
            if (phase == 2) { if (Antenna != null) Antenna.gameObject.SetActive(false); if (DomeLight != null) DomeLight.SetActive(false); }
        }

        protected override void OnDefeated()
        {
            BarLabel = "APPROVAL RATING: 1% (ME)";
            Headline("APPROVAL RATING: 1% (ME)");
            if (TutCapture != null) TutCapture.SetActive(false);
            if (CageBars != null) CageBars.localPosition = Vector3.up * 2.6f;
            if (WreckTrigger != null) WreckTrigger.enabled = true;
            if (!_restoredDefeated)
            {
                BossCompat.ActivateCheckpoint(FinalCheckpoint);
                Beat("ch13.caught");
                Sfx.At(SfxId.ColorBurst, L13Builder.CellSeat, 1f);
                Fx.Confetti(L13Builder.CellSeat + Vector3.up * 3f);
                Fx.Confetti(HomePosition + Vector3.up * 5f);
                GameEvents.RaiseToast("PERCY IS FREE! THE NORTH DOOR IS OPEN.", UIColors.AccentYellow);
            }
            RaiseStatus();
        }

        void RestoreDefeated()
        {
            _restoredDefeated = true;
            AutoBeginOnRestore = false; AutoStart = false;
            SnapArmDropped();
            SetLegless(true);
            if (Model != null) { Model.SetShieldHalf(-1, false); Model.SetShieldHalf(1, false); if (Model.Max != null) Model.Max.SetActive(false); }
            ShowPod(true);
            if (Detention != null) Detention.SnapCaptured();
            ForceDefeat();
        }

        float _wreckPendingUntil;

        /// <summary>The cutout JumpScare commits the cue itself (scare.ch13.cutout); its lines are the story beat ch13.cutout.</summary>
        void OnBeatRelay(string id) { if (id == "scare.ch13.cutout") Beat("ch13.cutout"); }

        /// <summary>The wreck trigger fires on the walk to the exit; if a blocking window is open (C13 still running) the scare
        /// waits up to 30 s for it to close instead of being lost.</summary>
        void OnWreck(ScriptTrigger t)
        {
            if (ScareDirector.CanPlay("ch13.wreck")) PlayWreck();
            else if (ScareDirector.LastVerdict == ScareVerdict.Blocked || ScareDirector.LastVerdict == ScareVerdict.Cooldown) _wreckPendingUntil = Time.time + 30f;
        }

        void PlayWreck()
        {
            if (!ScareDirector.TryPlay("ch13.wreck")) return;
            Beat("ch13.wreck");
            var at = HomePosition + Vector3.up * (5.2f + (_legless ? 0f : LeglessDrop));
            Fx.Sparks(at, Vector3.up, 30, UIColors.Danger);
            Fx.FloatingText(at + Vector3.up * 1.2f, "...PLEASE RATE YOUR EXPERIENCE.  *", UIColors.Danger, 1.3f);
            Sfx.At(SfxId.Creak, at, 1f, 0.5f);
        }

        // ------------------------------------------------------------------ flavour
        public override SfxId TellSfx(BossAttackKind k) => k == BossAttackKind.SlamZone ? SfxId.Whoosh : k == BossAttackKind.Sweep ? SfxId.Kazoo : k == BossAttackKind.SkateLoop ? SfxId.BossCharge : base.TellSfx(k);
        public override SfxId ImpactSfx(BossAttackKind k) => k == BossAttackKind.SlamZone ? SfxId.DrumSlam : k == BossAttackKind.Sweep ? SfxId.Kazoo : k == BossAttackKind.SkateLoop ? SfxId.BossStomp : base.ImpactSfx(k);
        public override string HitText(BossAttackKind k) => k == BossAttackKind.SlamZone ? "FISTED BY MANAGEMENT!" : k == BossAttackKind.SkateLoop ? "SKATED ON!" : k == BossAttackKind.Sweep ? "KAZOO'D!" : base.HitText(k);

        protected override void OnMercy() { Say("HR SENT A MEMO. FINE. I'LL TELEGRAPH HARDER."); }

        void Headline(string text) { if (ArenaTv != null) ArenaTv.SetHeadline(text); }
        static void Beat(string id) => GameEvents.RaiseStoryBeat(id);
        void Say(string line)
        {
            float h = _legless ? 8f : 10.2f;
            Fx.FloatingText(transform.position + Vector3.up * h, line, UIColors.Danger, 1.25f);
        }
    }
}
