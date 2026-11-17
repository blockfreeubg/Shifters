// =====================================================================================
//  SHIFTERS - L9PorkchopBoss.cs   [LEVEL 9 - THE GETAWAY]  BOSS #2: PORKCHOP (Dept. of Finance)
//  ME-VISION (BossTargetRule.ActiveOnly): it only sees whoever you control. One attack, the HAM CHARGE:
//    TELL (paws scrape, eyes red, rising OINK, lane decal + SPD chip "CHARGE IN 0.8 s") while it turns to follow
//    the active intern; LockLead s before the end of the tell the lane LOCKS ("LOCKED ON!"); then a straight
//    charge at 10 m/s. A REINFORCED PILLAR in the lane stops it -> BONK -> DIZZY 5 s (P1, P2) / 6 s (P3) = the
//    weak-point window. Walls / bays: it shakes it off (recover) and trots back to the centre.
//    It ignores interns on a loading bay (safe zone) or its stairs (pigs can't climb > 1.3 m) and needs
//    NoticeDelay s to notice you when you step down.
//  P1 BREAK THE BANK  belly plug (any gun, WeakPointShoot) · P2 SNOUT PUNCH (Brute, WeakPointPunch STR 7)
//  P3 EAR HACK (Brainiac, the EAR PORT terminal beside its right ear, WeakPointHack). 1 hit each. Bar "SAVINGS n/3".
//  Phase = checkpoint 4 / 5 / 6 (the SW / SE / NE loading bays).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L9PorkchopBoss : BossBase
    {
        public PorkchopModel Model;
        [Tooltip("The model root (hidden while Dormant).")] public Transform ModelRoot;
        public Vector3 Center;
        public Vector3[] Pillars = new Vector3[0];
        public float PillarHalf = 1f, FrontReach = 2.0f, HitTolerance = 0.9f, BounceBack = 1.8f;
        public float NoticeDelay = 1.0f, LockLead = 0.6f, HomeSpeed = 6f;
        public BossWeakPoint Plug, Snout, Ear;
        public Transform EarTerminal;
        public GameObject Exhibit, WestBreach;
        public Vector3 BurstFrom;
        public L9Van Van;
        public float OutroDelay = 2.4f, VanDelay = 3.4f;

        // ---- bots / tests
        public bool ChargeLocked { get; private set; }
        public bool IsTelling { get { var c = Attacks.Current; return State == BossState.Fighting && c != null && c.Stage == BossTelegraphStage.Windup; } }
        public int PillarHits { get; private set; }
        public int LastPillar { get; private set; } = -1;
        public int Charges { get; private set; }
        public bool WalkingHome => _walkHome;
        public bool OutroRaised => _outroRaised;
        /// <summary>Where the Brute stands to punch the snout (in front of it, between snout and pillar).</summary>
        public Vector3 PunchStandPoint { get { var b = BodyT; var p = b.position + BodyForward * (FrontReach + 0.95f); p.y = b.position.y; return p; } }
        /// <summary>A punch spot beside the snout (on the side nearest from), clear of the pillar the pig is facing while DIZZY.</summary>
        public Vector3 PunchStandFor(Vector3 from)
        {
            var b = BodyT; var f = BodyForward; f.y = 0f; f.Normalize();
            var r = Vector3.Cross(Vector3.up, f);
            float side = Vector3.Dot(from - b.position, r) >= 0f ? 1f : -1f;
            var p = b.position + f * (FrontReach + 0.15f) + r * side * 1.45f; p.y = b.position.y;
            return p;
        }
        /// <summary>Where the Brainiac stands to use the EAR PORT terminal.</summary>
        public Vector3 HackStandPoint { get { if (EarTerminal == null) return BodyT.position; var p = EarTerminal.TransformPoint(new Vector3(0f, 0f, 0.95f)); p.y = BodyT.position.y; return p; } }
        public Vector3 PlugPoint => Plug != null ? Plug.transform.position : BodyT.position + Vector3.up * 1.2f;

        Transform BodyT => Body != null ? Body : transform;
        int _pillarHit = -1, _lockSerial = -1, _chargeSerial = -1;
        Vector3 _hitDir;
        bool _walkHome, _outroRaised, _baitHidden;
        Quaternion _rot0 = Quaternion.identity;
        float _baitSeenAt = -1f;
        float _notice, _introT = -1f, _defeatT = -1f, _wobble, _gait, _coinT;

        // ------------------------------------------------------------------ setup
        public void Configure()
        {
            BossId = "porkchop"; BossName = "PORKCHOP"; BossTitle = "DEPT. OF FINANCE"; BarLabel = "SAVINGS 3/3";
            DefeatBanner = "THE BANK IS BROKEN";
            Rule = BossTargetRule.ActiveOnly;
            IntroSeconds = 2.6f;
            Phases = new[]
            {
                new BossPhaseSpec { Name = "PHASE 1: BREAK THE BANK", HitsRequired = 1, Accepts = BossHitKind.Shoot, HitsOnlyWhenVulnerable = true,
                    VulnerableSeconds = 5f, CheckpointIndex = 4, Pattern = new[] { Charge(1.5f) } },
                new BossPhaseSpec { Name = "PHASE 2: SNOUT PUNCH", HitsRequired = 1, Accepts = BossHitKind.Punch, HitsOnlyWhenVulnerable = true,
                    VulnerableSeconds = 5f, CheckpointIndex = 5, Pattern = new[] { Charge(1.3f) } },
                new BossPhaseSpec { Name = "PHASE 3: EAR HACK", HitsRequired = 1, Accepts = BossHitKind.Hack, HitsOnlyWhenVulnerable = true,
                    VulnerableSeconds = 6f, CheckpointIndex = 6, Pattern = new[] { Charge(1.2f) } },
            };
        }

        static BossAttackSpec Charge(float windup) => BossAttackSpec.Make(BossAttackKind.Charge, windup, true, null, 1.3f, 24f, 10f, 0f, 1.2f);

        protected override void Awake()
        {
            if (Phases == null || Phases.Length == 0) Configure();
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            _rot0 = HomeRotation;
            SetHome(Center, _rot0);
            if (ModelRoot != null && State == BossState.Dormant) ModelRoot.gameObject.SetActive(false);
            if (Exhibit != null) Exhibit.SetActive(false);
        }

        // ------------------------------------------------------------------ targeting
        /// <summary>The active intern can be charged: inside the arena, not on a loading bay or its stairs.</summary>
        public bool ValidTarget()
        {
            var a = BossTeam.Active;
            if (a == null || !a.IsAlive) return false;
            var f = a.Feet;
            if (Arena != null && (!Arena.Contains(f) || Arena.InSafeZone(f))) return false;
            return f.y < 1.0f;
        }

        int PillarAt(Vector3 front)
        {
            float tol2 = HitTolerance * HitTolerance;
            for (int i = 0; i < Pillars.Length; i++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(front.x - Pillars[i].x) - PillarHalf);
                float dz = Mathf.Max(0f, Mathf.Abs(front.z - Pillars[i].z) - PillarHalf);
                if (dx * dx + dz * dz < tol2) return i;
            }
            return -1;
        }

        /// <summary>Re-aim the (windup) charge lane at aim; the lane ends where the snout would meet a pillar.</summary>
        void Retarget(BossTelegraph t, Vector3 aim)
        {
            aim.y = t.Origin.y;
            var d = aim - t.Origin; d.y = 0f;
            if (d.sqrMagnitude < 0.04f) return;
            d.Normalize();
            t.Aim = aim;
            t.End = Arena != null ? BossAttackRunner.ClampCharge(Arena, t.Origin, t.Origin + d * t.Range, t.Radius, t.Range) : t.Origin + d * t.Range;
            float len = BossFootprint.Planar(t.Origin, t.End);
            for (float s = 0.25f; s <= len; s += 0.25f)
            {
                var q = t.Origin + d * s;
                if (PillarAt(q + d * FrontReach) >= 0) { t.End = q; break; }
            }
            t.ActiveSeconds = Mathf.Max(0.2f, BossFootprint.Planar(t.Origin, t.End) / Mathf.Max(0.5f, t.Speed));
        }

        void TrackAim()
        {
            var t = Attacks.Current;
            if (t == null || t.Kind != BossAttackKind.Charge) { ChargeLocked = false; return; }
            if (t.Serial != _chargeSerial) { _chargeSerial = t.Serial; Charges++; }
            if (t.Stage != BossTelegraphStage.Windup) { ChargeLocked = true; return; }
            if (t.Elapsed < t.WindupSeconds - LockLead)
            {
                ChargeLocked = false;
                var a = BossTeam.Active;
                if (a != null && ValidTarget()) Retarget(t, a.Feet);
                return;
            }
            ChargeLocked = true;
            if (_lockSerial != t.Serial)
            {
                _lockSerial = t.Serial;
                Sfx.At(SfxId.Oink, BodyT.position, 1f, 1.5f);
                Fx.FloatingText(BodyT.position + Vector3.up * 4.3f, "LOCKED ON!", UIColors.Danger, 1.1f);
            }
        }

        // ------------------------------------------------------------------ loop
        protected override void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (State == BossState.Fighting)
            {
                var cur = Attacks.Current;
                if (_walkHome) { if (cur == null) Attacks.Delay(0.3f); }
                else if (!ValidTarget())
                {
                    _notice = NoticeDelay;
                    if (cur == null) Attacks.Delay(0.3f);
                    else if (cur.Stage == BossTelegraphStage.Windup)
                    {
                        Attacks.Cancel();
                        Fx.FloatingText(BodyT.position + Vector3.up * 4.2f, "HMPH. WHERE'D YOU GO?", Color.white, 0.9f);
                    }
                }
                else if (_notice > 0f) { _notice -= dt; if (cur == null) Attacks.Delay(_notice + 0.02f); }
            }
            base.FixedUpdate();
            if (State == BossState.Fighting) TrackAim(); else ChargeLocked = false;
            if (_pillarHit >= 0) { if (State == BossState.Fighting) StartDizzy(); else _pillarHit = -1; }
            TurnToTarget = !_walkHome;
            if (_walkHome && (State == BossState.Fighting || State == BossState.PhaseTransition)) WalkHome(dt);
        }

        public override void MoveBody(Vector3 pos)
        {
            var cur = Attacks.Current;
            bool charging = State == BossState.Fighting && cur != null && cur.Kind == BossAttackKind.Charge && cur.Stage == BossTelegraphStage.Active;
            if (charging)
            {
                if (_pillarHit >= 0) return;                       // nose-first in a pillar until the dizzy starts
                var b = BodyT;
                var from = b.position;
                var dir = cur.End - cur.Origin; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    dir.Normalize();
                    AimBody(dir);
                    var step = pos - from; step.y = 0f;
                    int n = Mathf.Max(1, Mathf.CeilToInt(step.magnitude / 0.2f));
                    for (int s = 1; s <= n; s++)
                    {
                        var q = from + step * (s / (float)n);
                        int hit = PillarAt(q + dir * FrontReach);
                        if (hit >= 0)
                        {
                            _pillarHit = hit; _hitDir = dir;
                            base.MoveBody(from + step * ((s - 1) / (float)n));
                            return;
                        }
                    }
                }
            }
            base.MoveBody(pos);
        }

        void StartDizzy()
        {
            int hit = _pillarHit;
            _pillarHit = -1;
            var b = BodyT;
            var bounce = b.position - _hitDir * BounceBack;
            if (Arena != null)
            {
                var bb = Arena.Bounds;
                bounce.x = Mathf.Clamp(bounce.x, bb.min.x + 2.5f, bb.max.x - 2.5f);
                bounce.z = Mathf.Clamp(bounce.z, bb.min.z + 2.5f, bb.max.z - 2.5f);
            }
            bounce.y = b.position.y;
            var contact = b.position + _hitDir * (FrontReach + 0.4f) + Vector3.up * 1.8f;
            // keep the snout in the pillar while DIZZY (BossBase turns a Vulnerable boss to HomeRotation):
            // the plug, snout and ear port stay where the player saw them at the BONK
            SetHome(bounce, Quaternion.LookRotation(_hitDir, Vector3.up));
            LastPillar = hit; PillarHits++;
            Fx.Sparks(contact, -_hitDir, 30, UIColors.AccentYellow);
            Fx.CoinShower(contact, 30, 1f);
            Sfx.At(SfxId.MetalHit, contact, 1f, 0.6f);
            Sfx.At(SfxId.BossStomp, contact, 0.9f);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.45f, 0.35f);
            Fx.FloatingText(b.position + Vector3.up * 4.6f, "BONK! DIZZY!", UIColors.AccentYellow, 1.6f);
            SetVulnerable(Phase >= 2 ? 6f : 5f);
        }

        void WalkHome(float dt)
        {
            var b = BodyT;
            var to = Center - b.position; to.y = 0f;
            float d = to.magnitude;
            if (d < 0.05f)
            {
                _walkHome = false;
                base.MoveBody(Center);
                SetHome(Center, _rot0);
                return;
            }
            base.MoveBody(b.position + to / d * Mathf.Min(d, HomeSpeed * dt));
            AimBody(to);
            SetHome(HomePosition, Quaternion.LookRotation(to / d, Vector3.up));   // PhaseTransition turns toward HomeRotation too
        }

        // ------------------------------------------------------------------ visuals
        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            if (!_baitHidden) BaitCardTick();
            if (_introT >= 0f) IntroTick(dt);
            if (Model == null) return;
            var cur = Attacks.Current;
            bool tell = State == BossState.Fighting && cur != null && cur.Stage == BossTelegraphStage.Windup;
            bool charge = State == BossState.Fighting && cur != null && cur.Stage == BossTelegraphStage.Active;
            if (_defeatT < 0f)
            {
                Model.SetEyesRed(tell || charge);
                Model.SetScrape(tell ? Mathf.Repeat(cur.Elapsed * 3.2f, 1f) : 0f);
            }
            if (charge || _walkHome || _introT >= 0f) { _gait += dt * (charge ? 16f : 10f); Model.SetGait(_gait, charge ? 32f : 20f); }
            else Model.SetGait(0f, 0f);
            if (ModelRoot == null) return;
            if (_defeatT >= 0f) DefeatTick(dt);
            else if (State == BossState.Vulnerable)
            {
                _wobble += dt;
                ModelRoot.localRotation = Quaternion.Euler(0f, Mathf.Sin(_wobble * 5f) * 9f, Mathf.Sin(_wobble * 7.3f) * 7f);
                Model.SetEars(-40f + Mathf.Sin(_wobble * 9f) * 15f);
            }
            else { _wobble = 0f; ModelRoot.localRotation = Quaternion.identity; Model.SetEars(tell ? 25f : 0f); }
        }

        void IntroTick(float dt)
        {
            _introT += dt;
            float k = Mathf.Clamp01(_introT / (IntroSeconds * 0.8f));
            var p = Vector3.Lerp(BurstFrom, Center, PuzzleUtil.Ease(k));
            base.MoveBody(p);
            AimBody(Center - BurstFrom);
            if (k >= 1f) { _introT = -1f; base.MoveBody(Center); BodyT.rotation = _rot0; }
        }

        void DefeatTick(float dt)
        {
            _defeatT += dt;
            float k = Mathf.Clamp01(_defeatT / 1.1f);
            ModelRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 82f, PuzzleUtil.Ease(k)));
            _coinT -= dt;
            if (_defeatT < 3.2f && _coinT <= 0f && Model != null)
            {
                _coinT = 0.35f;
                var slot = Model.Part("CoinSlot");
                Fx.CoinShower((slot != null ? slot.position : BodyT.position + Vector3.up * 2f) + Vector3.up * 0.4f, 45, 1.6f);
                Sfx.At(SfxId.CoinShower, BodyT.position, 0.9f);
            }
            if (!_outroRaised && _defeatT >= OutroDelay) { _outroRaised = true; GameEvents.RaiseStoryBeat("ch08.outro"); }
            if (_defeatT >= VanDelay && Van != null) Van.Go();
        }

        // ------------------------------------------------------------------ hooks
        protected override void OnIntro()
        {
            if (ModelRoot != null) ModelRoot.gameObject.SetActive(true);
            if (WestBreach != null && WestBreach.activeSelf)
            {
                WestBreach.SetActive(false);
                if (Phase > 0) { UpdateBar(); return; }
                var w = WestBreach.transform.position + Vector3.up * 2.5f;
                Fx.PaperBurst(w, 30, 1.6f);
                Fx.CoinShower(w, 60, 2.5f);
                Sfx.At(SfxId.Explosion, w, 1f, 0.8f);
                if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.6f, 0.5f);
            }
            if (Phase == 0)
            {
                GameEvents.RaiseStoryBeat("ch08.boss");
                _introT = 0f;
                base.MoveBody(BurstFrom);
            }
            UpdateBar();
        }

        /// <summary>The ch08_bait card sits mid-screen: drop it once it has been read (3 s) and the first tell starts, or at the first charge.</summary>
        void BaitCardTick()
        {
            var tm = TutorialManager.Instance;
            if (tm == null || tm.Current == null || tm.Current.Id != "ch08_bait") { if (_baitSeenAt >= 0f) _baitHidden = true; return; }
            if (_baitSeenAt < 0f) _baitSeenAt = Time.unscaledTime;
            if ((IsTelling && Time.unscaledTime - _baitSeenAt >= 3f) || (Charges > 0 && !IsTelling)) { tm.Hide(); _baitHidden = true; }
        }

        protected override void OnPhaseBegin(int phase)
        {
            var off = BodyT.position - Center; off.y = 0f;
            _walkHome = off.sqrMagnitude > 0.04f;
            _notice = NoticeDelay;
            UpdateBar();
            Fx.FloatingText(BodyT.position + Vector3.up * 4.8f, phase == 0 ? "OINK. INSUFFICIENT FUNDS." : phase == 1 ? "OINK! NOW I'M ANGRY-ER!" : "FINAL OFFER: OINK.", UIColors.Danger, 1.2f);
        }

        protected override void OnVulnerableChanged(bool vulnerable)
        {
            if (vulnerable) return;
            _walkHome = true;
            Fx.FloatingText(BodyT.position + Vector3.up * 4.4f, "*shakes it off*", Color.white, 1f);
        }

        protected override void OnHit(int hitsLeft)
        {
            GameEvents.RaiseStoryBeat(Phase == 0 ? "ch08.p1" : Phase == 1 ? "ch08.p2" : "ch08.p3");
            BarLabel = "SAVINGS " + Mathf.Max(0, PhaseCount - Phase - 1) + "/" + PhaseCount;
            RaiseStatus();
        }

        protected override void OnDefeated()
        {
            BarLabel = "SAVINGS 0/" + PhaseCount;
            RaiseStatus();
            _defeatT = 0f;
            if (Model != null) Model.SetEyesRed(false);
            if (Exhibit != null)
            {
                var b = BodyT;
                var p = b.position + BodyForward * 3.2f;
                if (Arena != null) { var bb = Arena.Bounds; p.x = Mathf.Clamp(p.x, bb.min.x + 3f, bb.max.x - 3f); p.z = Mathf.Clamp(p.z, bb.min.z + 3f, bb.max.z - 3f); }
                p.y = b.position.y;
                Exhibit.transform.position = p;
                Exhibit.SetActive(true);
                Fx.Poof(p + Vector3.up, UIColors.AccentYellow, 1.6f);
                Fx.FloatingText(p + Vector3.up * 2.6f, "EXHIBIT B: THE BOOKS", UIColors.AccentYellow, 1.4f);
            }
        }

        public override SfxId TellSfx(BossAttackKind k) => SfxId.Oink;
        public override SfxId ImpactSfx(BossAttackKind k) => SfxId.BossCharge;
        public override string HitText(BossAttackKind k) => "HAM'D!";

        void UpdateBar() { BarLabel = "SAVINGS " + Mathf.Max(0, PhaseCount - Phase) + "/" + PhaseCount; RaiseStatus(); }
    }
}
