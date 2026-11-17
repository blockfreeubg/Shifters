// =====================================================================================
//  SHIFTERS - L12BoogeymanBoss.cs   [SHIFT 11 UNMASKED - boss brain: THE BOOGEYMAN / SCARE-O-MATIC 3000]
//  LONELY targeting (BossTargetRule.Loneliest + IsTargetable: SCARED >= 1.0 s, never the last free intern).
//    P1 LIGHTS OUT  fixed 8 s cycle (runner: Spook windup 1.5 / active 0.5 / recover 6.0, first TELL at 5.0 s).
//                   TELL: door-sized eyes open 4.5 m behind the target (dark point), whisper. Lands iff the target
//                   is still SCARED, the eyes are unlit (flashlight / flare / pools) and nobody SHIFTed to it.
//                   Landed -> stunned, dragged into the BROOM CLOSET (HoldingCell). Lights: 3 Custom hits
//                   (L12AtriumLights). Transition 3 s: shadow shrinks -> the machine is revealed.
//    P2 UNMASK      kinematic floor buffer on a fixed route (3.5 m/s) with 5 s PAD PAUSEs (BOO tell 1.2 / BOO 0.3 /
//                   CHARGING 3.5). BOO hits only LONELY interns inside the 5 m ring. Unmask (shot on the sheet /
//                   thrown intern / Brute punch while charging) -> DIZZY 8 s -> HACK the projector (Hack hit).
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public enum L12UnmaskKind { Shot, Impact, Punch }

    public sealed class L12BoogeymanBoss : BossBase
    {
        // ---- wired by L12Builder
        public L12AtriumLights Lights;
        public HoldingCell Closet;
        public BoogeymanModel Shadow;          // the 6 m silhouette (transition reveal + sky-cam scare), visual only
        public ScareOMaticModel Machine;       // child of Body
        public L12BoogeyEyes Eyes;             // the TELL / intro eyes (trigger r 0.8, IShootable)
        public Collider[] MachineColliders = new Collider[0];
        public Transform BooRingW, BooRingE;   // ring decal roots (child "Fill" scales with the tell)
        public GameObject[] PhaseOnlyP2 = new GameObject[0];   // hint 3, pad labels: shown in P2
        public GameObject[] PhaseOnlyP1 = new GameObject[0];   // hint 1: hidden in P2
        public TutorialTrigger TutEyes, TutCloset, TutUnmask;

        // ---- layout / tuning (LEVELS2 SHIFT 11)
        public static readonly Vector3 Hidden = new Vector3(0f, -3f, 19f);
        public static readonly Vector3 Center = new Vector3(0f, 0f, 19f);
        public static readonly Vector3 PadW = new Vector3(-6f, 0f, 19f);
        public static readonly Vector3 PadE = new Vector3(6f, 0f, 19f);
        static readonly Vector3[] LegEtoW = { new Vector3(6f, 0f, 24f), new Vector3(-6f, 0f, 24f), PadW };
        static readonly Vector3[] LegWtoE = { new Vector3(-6f, 0f, 14f), new Vector3(6f, 0f, 14f), PadE };
        static readonly Vector3[] WhisperPoints =
        {
            new Vector3(-13f, 2.8f, 27f), new Vector3(13f, 2.8f, 22f), new Vector3(0f, 2.8f, 28.5f),
            new Vector3(-13f, 2.8f, 10f), new Vector3(13f, 2.8f, 10f), new Vector3(0f, 2.8f, 16f),
        };
        public const float Cycle = 8f, FirstTell = 5f, SpookTell = 1.5f;
        public const float EyesBack = 4.5f, EyesHeight = 2.8f;
        public const float DriveSpeed = 3.5f, PadPause = 5f, BooTell = 1.2f, BooActive = 0.3f, BooRadius = 5f, LonelyRadius = 6f;
        public const float BooDamage = 30f, DizzySeconds = 8f, CheeseBonus = 2f, ResheetSeconds = 1.5f;

        // ---- runtime state (public for bots / tests, B13 extras)
        public int SpooksLanded { get; private set; }
        public int Fizzles { get; private set; }
        public int Whispers { get; private set; }
        public int Boos { get; private set; }
        public int Unmasks { get; private set; }
        public int SkyCams { get; private set; }
        public Vector3 EyesPoint { get; private set; }
        public bool EyesOpen => _eyesOn;
        public bool IsDizzy => Phase == 1 && IsVulnerable;
        public bool IsCharging => Phase == 1 && State == BossState.Fighting && _mode == Mode.Pause && _resheet <= 0f && _pauseT >= BooEnd && _pauseT < PadPause;
        /// <summary>-1 none, 0 = PAD W, 1 = PAD E (the pad the machine is paused on).</summary>
        public int CurrentPad => Phase == 1 && _mode == Mode.Pause ? _pad : -1;
        public float BooTellNow => BooTell * TellScale;
        float BooEnd => BooTellNow + BooActive;
        public bool BooTelling => Phase == 1 && State == BossState.Fighting && _mode == Mode.Pause && _resheet <= 0f && _pauseT < BooTellNow;
        public float ScaredThreshold => MercyLevel > 0 ? 1.5f : 1.0f;
        public float FightClock { get; private set; }
        /// <summary>Boss extra (c): FNV hash of (phase, slot, kind, start FightClock to 0.01 s) of every P1 tell slot and
        /// every P2 pad pause; deterministic (FixedUpdate clock), so identical on every team. ResetTimeline() clears it.</summary>
        public uint TimelineHash { get; private set; } = 2166136261u;
        public int TimelineEvents { get; private set; }
        public string TimelineLog { get; private set; } = "";
        public int Rescues { get; private set; }
        public void ResetTimeline() { TimelineHash = 2166136261u; TimelineEvents = 0; TimelineLog = ""; }
        void MixTimeline(int kind)
        {
            int t = Mathf.RoundToInt(FightClock * 100f);
            unchecked
            {
                foreach (int v in new[] { Phase, TimelineEvents, kind, t }) TimelineHash = (TimelineHash ^ (uint)v) * 16777619u;
            }
            TimelineEvents++;
            if (TimelineLog.Length < 400) TimelineLog += "P" + (Phase + 1) + "#" + TimelineEvents + "@" + (t / 100f).ToString("0.00") + " ";
        }
        int _closetHad; string _lastPrisoner = "AN INTERN"; bool _firstRescue = true;

        enum Mode { Idle, Drive, Pause }
        Mode _mode = Mode.Idle;
        Vector3[] _path; int _pathIdx; int _pad = -1; float _pauseT, _resheet; bool _booResolved; int _nextLeg; // 0 = E->W, 1 = W->E
        readonly float[] _scaredFor = new float[8];
        bool _eyesOn, _slotWhisper, _shifted, _flashed, _tellRunning, _firstTell = true, _firstFizzle = true, _firstSpook = true, _firstBoo = true, _firstUnmask = true;
        float _blindUntil = -99f, _noTellUntil = -99f, _eyesScareAt = -99f;
        ShifterCharacter _tellTarget, _swPrev, _swNext;
        int _skyCheck;
        bool _triggered;

        public void Configure()
        {
            BossId = "boogeyman"; BossName = "THE BOOGEYMAN"; BossTitle = "DEPT. OF FEAR · IT ONLY HUNTS THE LONELY"; BarLabel = "LIGHTS ON 0/3";
            Rule = BossTargetRule.Loneliest;
            TurnToTarget = false; AutoStart = false; IntroSeconds = 2.5f; TransitionSeconds = 3f;
            DefeatBanner = "SCARE-O-MATIC 3000: UNPLUGGED";
            MercyBanner = "HR SENT A NIGHT-LIGHT";
            Phases = new[]
            {
                new BossPhaseSpec { Name = "PHASE 1: LIGHTS OUT", HitsRequired = 3, Accepts = BossHitKind.Custom, HitsOnlyWhenVulnerable = false,
                    CheckpointIndex = 1, IntroBeat = "ch11.p1",
                    Pattern = new[] { BossAttackSpec.Make(BossAttackKind.Spook, SpookTell, true, null, 1.2f, 0f, 0f, 0.5f, Cycle - SpookTell - 0.5f, 0f) } },
                new BossPhaseSpec { Name = "PHASE 2: UNMASK", HitsRequired = 1, Accepts = BossHitKind.Hack, HitsOnlyWhenVulnerable = true,
                    VulnerableSeconds = DizzySeconds, CheckpointIndex = 2, IntroBeat = "ch11.p2",
                    Pattern = new BossAttackSpec[0] },
            };
        }

        protected override void Awake()
        {
            if (Phases == null || Phases.Length == 0) Configure();
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameEvents.CharacterSwitched += OnSwitched;
            GameEvents.SwitchTransitionChanged += OnSwitchTransition;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameEvents.CharacterSwitched -= OnSwitched;
            GameEvents.SwitchTransitionChanged -= OnSwitchTransition;
        }

        protected override void Start()
        {
            base.Start();
            SetMachine(false);
            if (Shadow != null) Shadow.gameObject.SetActive(false);
            ShowRing(BooRingW, false, 0f); ShowRing(BooRingE, false, 0f);
        }

        // ================================================================== B13 extras
        public float ScaredFor(ShifterCharacter c) => c != null && c.Slot >= 0 && c.Slot < _scaredFor.Length ? _scaredFor[c.Slot] : 0f;

        public static bool IsFree(ShifterCharacter c) => c != null && c.isActiveAndEnabled && c.IsAlive;

        public int FreeCount()
        {
            int n = 0; var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++) if (IsFree(team[i])) n++;
            return n;
        }

        /// <summary>P1: a TELL targets an intern within 1 m of p, or p is unlit and a TELL starts within h.
        /// P2: p inside a pad ring whose BOO resolves within h.</summary>
        public bool IsThreatened(Vector3 p, float h) => NextThreatTime(p) <= h;

        public float NextThreatTime(Vector3 p)
        {
            if (Phase == 0)
            {
                if (_tellRunning && _tellTarget != null && BossFootprint.Planar(_tellTarget.Feet, p) <= 1f) return 0f;
                if (LightRegistry.IsLit(p + Vector3.up)) return float.PositiveInfinity;
                return State == BossState.Fighting ? Attacks.NextAttackIn : float.PositiveInfinity;
            }
            if (Phase == 1 && State == BossState.Fighting)
            {
                int pad = NearPad(p);
                if (pad < 0) return float.PositiveInfinity;
                if (_mode == Mode.Pause && _pad == pad && !_booResolved) return Mathf.Max(0f, BooTellNow - _pauseT);
                if (_mode == Mode.Drive && _path != null && _path.Length > 0 && NearPad(_path[_path.Length - 1]) == pad)
                    return RemainingPath() / DriveSpeed + BooTellNow;
            }
            return float.PositiveInfinity;
        }

        static int NearPad(Vector3 p)
        {
            if (BossFootprint.Planar(p, PadW) <= BooRadius) return 0;
            if (BossFootprint.Planar(p, PadE) <= BooRadius) return 1;
            return -1;
        }

        float RemainingPath()
        {
            if (_path == null || Body == null) return 0f;
            float d = 0f; var at = Body.position; at.y = 0f;
            for (int i = _pathIdx; i < _path.Length; i++) { d += BossFootprint.Planar(at, _path[i]); at = _path[i]; }
            return d;
        }

        // ================================================================== targeting (LONELY)
        protected override bool IsTargetable(ShifterCharacter c)
        {
            if (Phase != 0 || !IsFree(c)) return false;
            if (ScaredFor(c) < ScaredThreshold) return false;
            if (Closet != null && Closet.Contains(c.Feet)) return false;
            return FreeCount() >= 2;   // never the last free intern: "A BOOGEYMAN NEEDS AN AUDIENCE."
        }

        /// <summary>The hit rule, evaluated once at TELL end.</summary>
        public override bool CanSpook(ShifterCharacter c, float radius)
        {
            if (_slotWhisper || c == null || Phase != 0) return false;
            if (!IsFree(c) || !Bravery.IsScared(c)) return false;
            if (_shifted || _flashed) return false;
            if (EyesLit(EyesPoint)) return false;
            return true;
        }

        public override SfxId TellSfx(BossAttackKind k) => k == BossAttackKind.Spook ? (ScareDirector.Mild ? SfxId.UIPop : SfxId.Whisper) : base.TellSfx(k);
        public override string HitText(BossAttackKind k) => k == BossAttackKind.Spook ? "BOO!" : "AAAH!";
        public override void MoveBody(Vector3 pos) { }   // the machine drives itself (fixed route)

        // ================================================================== lifecycle hooks
        protected override void Update()
        {
            base.Update();
            if (State == BossState.Dormant && !_triggered) CheckIntroTrigger();
            if (_skyCheck > 0 && --_skyCheck == 0) TrySkyCam();
            WatchCloset();
        }

        /// <summary>ch11.rescue: the closet emptied (lever pulled) during P1 -> {RESCUED} token + beat (first time).</summary>
        void WatchCloset()
        {
            if (Closet == null) return;
            int n = Closet.PrisonerCount;
            if (n > 0) { var pr = Closet.Prisoners[n - 1]; if (pr != null) _lastPrisoner = pr.DisplayName; }
            else if (_closetHad > 0 && Phase == 0 && State != BossState.Defeated)
            {
                Rescues++;
                StoryText.SetToken("RESCUED", _lastPrisoner);
                if (_firstRescue) { _firstRescue = false; GameEvents.RaiseStoryBeat("ch11.rescue"); }
            }
            _closetHad = n;
        }

        void CheckIntroTrigger()
        {
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive) continue;
                var f = c.Feet;
                if (f.z >= 1f && f.z <= 6f && Mathf.Abs(f.x) <= 6.5f && f.y > 1.5f)
                {
                    _triggered = true;
                    GameEvents.RaiseStoryBeat("ch11.intro");
                    Begin();
                    return;
                }
            }
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            float dt = Time.fixedDeltaTime;
            if (IsFighting || State == BossState.PhaseTransition) FightClock += dt;
            // ScaredFor: continuous seconds SCARED (P1 only)
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || c.Slot < 0 || c.Slot >= _scaredFor.Length) continue;
                bool s = Phase == 0 && IsFighting && Bravery.IsScared(c);
                _scaredFor[c.Slot] = s ? _scaredFor[c.Slot] + dt : 0f;
            }
            if (Phase == 1) TickPhase2(dt);
        }

        protected override void OnIntro()
        {
            AudioManager.Instance?.PlayMusic(MusicId.Boss, 1.5f);
            if (ScareDirector.TryPlay("ch11.eyes")) { _eyesScareAt = Time.time; if (Eyes != null) Eyes.PlayIntro(new Vector3(0f, 3f, 29f)); }
            if (Lights != null) Lights.OnBossIntro();
        }

        protected override void OnPhaseBegin(int phase)
        {
            FightClock = 0f;
            EndVulnerableOnHit = true;
            if (phase == 0)
            {
                Attacks.Delay(FirstTell);
                BarLabel = Lights != null ? Lights.BarText : "LIGHTS ON 0/3";
                GameEvents.RaiseObjective("LIGHTS ON 0/3 — STICK TOGETHER");
                SetMachine(false);
            }
            else
            {
                if (Lights != null) Lights.ForceAllOn();
                if (Closet != null) Closet.ReleaseAll();
                if (FlashlightRig.Instance != null) FlashlightRig.Instance.On = false;
                BarLabel = "UNMASKED 0/1";
                GameEvents.RaiseObjective("UNMASK IT. THEN HACK THE PROJECTOR.");
                SetMachine(true);
                if (Body != null) Body.position = Center;
                if (Machine != null) { Machine.SetSheet(true); Machine.SetProjector(true); }
                if (Shadow != null) Shadow.gameObject.SetActive(false);
                for (int i = 0; i < PhaseOnlyP2.Length; i++) if (PhaseOnlyP2[i] != null) PhaseOnlyP2[i].SetActive(true);
                for (int i = 0; i < PhaseOnlyP1.Length; i++) if (PhaseOnlyP1[i] != null) PhaseOnlyP1[i].SetActive(false);
                StartLeg(new[] { PadE }, 0);
                if (TutUnmask != null) TutUnmask.Fire();
            }
            RaiseStatus();
        }

        protected override void OnPhaseEnd(int phase)
        {
            if (phase != 0) return;
            CloseEyes();
            ScareDirector.SetWindow("tell", false);
            StartCoroutine(RevealRoutine());
        }

        IEnumerator RevealRoutine()
        {
            Fx.FloatingText(new Vector3(0f, 6f, 19f), "LIGHTS ON!", UIColors.AccentYellow, 1.6f);
            if (Shadow != null)
            {
                Shadow.gameObject.SetActive(true);
                Shadow.transform.position = Center;
                Shadow.transform.localScale = Vector3.one;
                Shadow.SetEyesOpen(1f); Shadow.SetAngry(true);
            }
            float t = 0f;
            while (t < 1.2f) { t += Time.deltaTime; if (Shadow != null) Shadow.SetPose(Mathf.PingPong(t * 2f, 1f), Mathf.Sin(t * 9f) * 6f); yield return null; }
            Sfx.At(SfxId.Squeak, Center + Vector3.up * 2f, 1f, 0.7f);
            SetMachine(true);
            if (Body != null) Body.position = Center;
            if (Machine != null) { Machine.SetSheet(true); Machine.SetProjector(true); }
            t = 0f;
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1f, 0.05f, t / 1.5f);
                if (Shadow != null) Shadow.transform.localScale = new Vector3(s, s, s);
                yield return null;
            }
            if (Shadow != null) Shadow.gameObject.SetActive(false);
            Fx.FloatingText(Center + Vector3.up * 2.6f, "...boo?", Color.white, 1.4f);
        }

        protected override void OnHit(int hitsLeft)
        {
            if (Phase == 1) { BarLabel = "UNMASKED 1/1"; RaiseStatus(); }
        }

        protected override void OnDefeated()
        {
            _mode = Mode.Idle;
            ShowRing(BooRingW, false, 0f); ShowRing(BooRingE, false, 0f);
            var at = Body != null ? Body.position : Center;
            Fx.Sparks(at + Vector3.up * 1.8f, Vector3.up, 30, UIColors.AccentYellow);
            Sfx.At(SfxId.BossDefeat, at, 1f);
            if (Machine != null) { Machine.SetSheet(false); Machine.SetProjector(true); }
            Fx.FloatingText(at + Vector3.up * 3f, "MAX?! (UPSIDE DOWN)", UIColors.Danger, 2f);
            var exhibit = StoryKit.ExhibitProp(transform.parent != null ? transform.parent : transform, at + new Vector3(1.4f, 0f, -1.2f), "C");
            if (exhibit != null) exhibit.name = "ExhibitC_Bedsheet";
            WorldColor.AddBurst(at, 14f, 8f);
            GameEvents.RaiseObjective("WALK OUT THE FRONT DOOR");
            GameEvents.RaiseStoryBeat("ch11.outro");
        }

        protected override void OnVulnerableChanged(bool vulnerable)
        {
            if (Phase != 1) return;
            if (!vulnerable && State != BossState.Defeated)
            {
                _resheet = ResheetSeconds;
                if (Machine != null) { Machine.SetSheet(true); Machine.SetProjector(true); }
                Fx.FloatingText((Body != null ? Body.position : Center) + Vector3.up * 2.6f, "RE-SHEETING...", Color.white, 1.2f);
            }
        }

        // ================================================================== P1: the TELL
        protected override void OnAttackStart(BossTelegraph t)
        {
            if (t.Kind != BossAttackKind.Spook || Phase != 0) return;
            MixTimeline((int)t.Kind);
            _tellRunning = true; _shifted = false; _flashed = false;
            _tellTarget = t.Target;
            _slotWhisper = _tellTarget == null || Time.time < _blindUntil || Time.time < _noTellUntil;
            Vector3 eyes = Vector3.zero;
            if (!_slotWhisper && !ChooseEyes(_tellTarget, out eyes)) _slotWhisper = true;
            if (_slotWhisper)
            {
                Whispers++;
                if (WhisperPoint(out var wp)) { EyesPoint = wp; if (Eyes != null) Eyes.Blink(wp, 0.6f); }
                return;
            }
            EyesPoint = eyes;
            _eyesOn = true;
            if (Eyes != null) Eyes.OpenAt(eyes, _tellTarget.Feet, t.WindupSeconds);
            ScareDirector.SetWindow("tell", true);
            Sfx.At(TellSfx(t.Kind), eyes, 1f);
            Fx.FloatingText(eyes + Vector3.up * 1.4f, ScareDirector.Mild ? "...split... up... (gently)" : "...split... up...", new Color(0.78f, 0.6f, 1f), 1.3f);
            Fx.FloatingText(_tellTarget.HeadTop + Vector3.up * 0.6f, "SPOOK: " + _tellTarget.DisplayName.ToUpperInvariant() + " " + t.WindupSeconds.ToString("0.0") + " s", UIColors.Speed, 1.2f);
            if (_firstTell)
            {
                _firstTell = false;
                GameEvents.RaiseStoryBeat("ch11.tell");
                if (TutEyes != null) TutEyes.Fire();
            }
        }

        protected override void OnAttackEnd(BossTelegraph t)
        {
            if (t.Kind != BossAttackKind.Spook) return;
            _tellRunning = false;
            ScareDirector.SetWindow("tell", false);
        }

        protected override void OnSpook(ShifterCharacter c)
        {
            CloseEyes();
            SpooksLanded++;
            if (_firstSpook) { _firstSpook = false; GameEvents.RaiseStoryBeat("ch11.spook"); }
            Sfx.At(SfxId.ScareStinger, c.Center, 0.8f);
            Sfx.At(SfxId.SlideWhistleDown, c.Center, 1f);
            Fx.FloatingText(c.HeadTop + Vector3.up * 0.4f, "AAAAH!", UIColors.Danger, 1.1f);
            StartCoroutine(DragToCloset(c));
            bool wipe = FreeCount() <= 2 || SpooksLanded % 3 == 0;   // after the capture only one would be left
            if (wipe) RegisterWipe();
        }

        IEnumerator DragToCloset(ShifterCharacter c)
        {
            ScareDirector.SetWindow("capture", true);
            Fx.Puff(c.Center, 1.2f, new Color(0.08f, 0.04f, 0.12f));
            yield return new WaitForSeconds(0.4f);
            if (c != null && c.IsAlive && Closet != null) Closet.Imprison(c);
            ScareDirector.SetWindow("capture", false);
            if (TutCloset != null) TutCloset.Fire();
        }

        protected override void OnSpookMissed(ShifterCharacter c)
        {
            if (_slotWhisper) { _slotWhisper = false; return; }
            Fizzles++;
            Sfx.At(SfxId.Squeak, EyesPoint, 1f, 0.55f);
            Fx.FloatingText(EyesPoint + Vector3.up * 1.3f, _flashed ? "NO FLASH PHOTOGRAPHY!" : "...never mind...", new Color(0.85f, 0.75f, 1f), 1.3f);
            if (Eyes != null) Eyes.Squint();
            _eyesOn = false;
            if (_firstFizzle) { _firstFizzle = false; GameEvents.RaiseStoryBeat("ch11.fizzle"); }
        }

        void CloseEyes() { _eyesOn = false; if (Eyes != null) Eyes.Close(); }

        /// <summary>The eyes are door-sized (about 2.8 m down to 2.0 m): light on any part of them counts (a flashlight
        /// held at chest height and aimed level reaches the lower half from 4.5 m).</summary>
        public static bool EyesLit(Vector3 eyes) => LightRegistry.IsLit(eyes) || LightRegistry.IsLit(eyes + Vector3.down * 0.8f);

        bool ChooseEyes(ShifterCharacter c, out Vector3 point)
        {
            point = Vector3.zero;
            if (c == null) return false;
            Vector3 back;
            var cam = Camera.main;
            if (c.IsActive && cam != null) back = -cam.transform.forward; else back = -c.transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) back = Vector3.back;
            back.Normalize();
            for (int k = 0; k < 8; k++)
            {
                float deg = k == 0 ? 0f : ((k + 1) / 2) * 45f * (k % 2 == 1 ? 1f : -1f);
                var d = Quaternion.Euler(0f, deg, 0f) * back;
                var p = c.Feet + d * EyesBack; p.y = EyesHeight;
                if (p.x < -14f || p.x > 14f || p.z < 1f || p.z > 29f) continue;
                if (EyesLit(p)) continue;
                point = p; return true;
            }
            return false;
        }

        bool WhisperPoint(out Vector3 p)
        {
            var team = BossTeam.Members;
            for (int i = 0; i < WhisperPoints.Length; i++)
            {
                p = WhisperPoints[i];
                if (LightRegistry.IsLit(p)) continue;
                bool far = true;
                for (int k = 0; k < team.Count; k++) if (team[k] != null && BossFootprint.Planar(team[k].Feet, p) < 10f) { far = false; break; }
                if (far) return true;
            }
            p = Vector3.zero; return false;
        }

        /// <summary>SAY CHEESE flash on the eyes (L12BoogeyEyes): fizzles the running TELL, blinds 2 s.</summary>
        public void OnFlash()
        {
            if (Phase != 0) return;
            _blindUntil = Time.time + 2f;
            if (_tellRunning && !_slotWhisper) _flashed = true;
            if (Eyes != null) Eyes.Squint();
        }

        void OnSwitched(ShifterCharacter prev, ShifterCharacter next)
        {
            _swPrev = prev; _swNext = next;
            if (_tellRunning && next != null && next == _tellTarget) _shifted = true;
        }

        void OnSwitchTransition(bool running) { if (running) _skyCheck = 2; }

        // ================================================================== set piece 10 part 2: the sky-cam scare
        void TrySkyCam()
        {
            if (Phase != 0 || !IsFighting || _tellRunning || Shadow == null) return;
            var cam = ShiftersCamera.Instance;
            if (cam == null || !cam.LastTransitionWasSky || _swPrev == null || _swNext == null) return;
            if (Time.time - _eyesScareAt < 30f) return;
            if (!ScareDirector.TryPlay("ch11.skycam")) return;
            SkyCams++;
            StartCoroutine(SkyCamRoutine((_swPrev.Feet + _swNext.Feet) * 0.5f));
        }

        IEnumerator SkyCamRoutine(Vector3 mid)
        {
            mid.x = Mathf.Clamp(mid.x, -13f, 13f); mid.z = Mathf.Clamp(mid.z, 2f, 28f); mid.y = 0f;
            _noTellUntil = Time.time + 4.5f;
            Shadow.gameObject.SetActive(true);
            Shadow.transform.position = mid;
            Shadow.transform.localScale = Vector3.one;
            Shadow.SetAngry(false); Shadow.SetEyesOpen(1f);
            var cam = Camera.main;
            if (cam != null) { var d = cam.transform.position - mid; d.y = 0f; if (d.sqrMagnitude > 0.01f) Shadow.transform.rotation = Quaternion.LookRotation(d.normalized); }
            float t = 0f;
            while (t < 2.5f)
            {
                t += Time.deltaTime;
                Shadow.SetPose(Mathf.Clamp01(t / 1.0f), t > 1.4f ? Mathf.Sin(t * 14f) * 12f : 0f);   // reach... freeze... tiny wave
                if (t > 1.4f && t - Time.deltaTime <= 1.4f) { Sfx.At(SfxId.Squeak, mid + Vector3.up * 5f, 1f, 1.4f); Fx.FloatingText(mid + Vector3.up * 6.4f, "*tiny embarrassed wave*", Color.white, 1.2f); }
                yield return null;
            }
            Shadow.gameObject.SetActive(false);
        }

        // ================================================================== P2: the floor buffer
        void StartLeg(Vector3[] path, int nextLeg)
        {
            _path = path; _pathIdx = 0; _mode = Mode.Drive; _pad = -1; _nextLeg = nextLeg;
            ShowRing(BooRingW, false, 0f); ShowRing(BooRingE, false, 0f);
            if (path != null && path.Length > 0 && Body != null)
            {
                float len = 0f; Vector3 a = Body.position; a.y = 0f;
                for (int i = 0; i < path.Length; i++) { len += BossFootprint.Planar(a, path[i]); a = path[i]; }
                Fx.FloatingText(path[path.Length - 1] + Vector3.up * 2.2f, "NEXT PAD " + (len / DriveSpeed).ToString("0.0") + " s", UIColors.Speed, Mathf.Min(2.5f, len / DriveSpeed));
            }
        }

        void TickPhase2(float dt)
        {
            if (State != BossState.Fighting || Body == null) return;
            if (_resheet > 0f)
            {
                _resheet -= dt;
                if (_resheet <= 0f && _mode == Mode.Pause) NextLeg();
                return;
            }
            if (_mode == Mode.Drive)
            {
                var pos = Body.position;
                var goal = _path[_pathIdx];
                var np = Vector3.MoveTowards(pos, goal, DriveSpeed * dt);
                var dir = goal - pos; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f) Body.rotation = Quaternion.RotateTowards(Body.rotation, Quaternion.LookRotation(dir.normalized), 360f * dt);
                Body.position = np;
                if (Machine != null) Machine.SetBrushAngle(FightClock * 720f);
                NudgeBystanders(np);
                if ((np - goal).sqrMagnitude < 1e-4f)
                {
                    _pathIdx++;
                    if (_pathIdx >= _path.Length)
                    {
                        _mode = Mode.Pause; _pauseT = 0f; _booResolved = false;
                        _pad = BossFootprint.Planar(goal, PadW) < 0.5f ? 0 : 1;
                        MixTimeline(100 + _pad);
                        Sfx.At(SfxId.VacuumHum, goal, 1f, 1.2f);
                        Fx.FloatingText(goal + Vector3.up * 2.8f, ScareDirector.Mild ? "vwoomp?" : "VWOOOMP", new Color(0.8f, 0.6f, 1f), 1.0f);
                    }
                }
                return;
            }
            if (_mode != Mode.Pause) return;
            _pauseT += dt;
            var ring = _pad == 0 ? BooRingW : BooRingE;
            if (!_booResolved)
            {
                ShowRing(ring, true, Mathf.Clamp01(_pauseT / BooTellNow));
                if (_pauseT >= BooTellNow) ResolveBoo();
            }
            else ShowRing(ring, false, 0f);
            if (_pauseT >= PadPause) NextLeg();
        }

        void NextLeg()
        {
            if (_nextLeg == 0) StartLeg(LegEtoW, 1); else StartLeg(LegWtoE, 0);
        }

        void ResolveBoo()
        {
            _booResolved = true;
            var pad = _pad == 0 ? PadW : PadE;
            bool any = false, pairs = false;
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (!IsFree(c)) continue;
                var f = c.Feet;
                if (BossFootprint.Planar(f, pad) > BooRadius || f.y > 2f) continue;
                if (Arena != null && Arena.InSafeZone(f)) continue;
                if (Bravery.HasBuddy(c, LonelyRadius)) { pairs = true; continue; }
                any = true;
                Boos++;
                float dmg = BooDamage * DamageScale;
                c.Damage(dmg, Vector3.zero, BossName);
                var away = f - pad; away.y = 0f; if (away.sqrMagnitude < 0.01f) away = Vector3.back; away.Normalize();
                if (c.IsAlive && c.Motor != null) c.Motor.AddImpulse(away * 6f + Vector3.up * 3f, true, 2f);
                Sfx.At(SfxId.SlideWhistleDown, c.Center, 1f);
                Fx.FloatingText(c.HeadTop + Vector3.up * 0.4f, "AAAH!", UIColors.Danger, 1.1f);
                if (_firstBoo) { _firstBoo = false; StoryText.SetToken("VICTIM", c.DisplayName); GameEvents.RaiseStoryBeat("ch11.boo"); }
            }
            Sfx.At(SfxId.Boo, pad, 1f);
            if (!any) Fx.FloatingText(pad + Vector3.up * 2.6f, pairs ? "...boo?  (\"Cute.\")" : "...boo?", Color.white, 1.2f);
        }

        void NudgeBystanders(Vector3 at)
        {
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive || c.Motor == null) continue;
                var d = c.Feet - at; d.y = 0f;
                if (d.sqrMagnitude > 1.3f * 1.3f || c.Feet.y > 1.5f) continue;
                BossAttackRunner.SlideOff(c, at, Time.fixedDeltaTime);
            }
        }

        /// <summary>Knock the sheet off (shot / thrown intern / punch). Punch and impact only while CHARGING.</summary>
        public bool Unmask(L12UnmaskKind kind, ShifterCharacter by = null, bool cheese = false)
        {
            if (Phase != 1 || State != BossState.Fighting || _resheet > 0f) return false;
            if (kind != L12UnmaskKind.Shot && !IsCharging) return false;
            Unmasks++;
            if (Machine != null) { Machine.SetSheet(false); Machine.SetProjector(false); }
            var at = Body != null ? Body.position : Center;
            Sfx.At(SfxId.Bonk, at + Vector3.up, 1f);
            Fx.Puff(at + Vector3.up * 1.6f, 1.2f, Color.white);
            Fx.FloatingText(at + Vector3.up * 3.4f, (cheese ? "MY EYES! I DON'T EVEN HAVE EYES!" : "MY SHEET!") + "\nDIZZY: HACK THE PROJECTOR!", UIColors.Intellect, 1.6f);
            if (_firstUnmask) { _firstUnmask = false; GameEvents.RaiseStoryBeat("ch11.unmasked"); }
            if (_mode == Mode.Pause) _pauseT = PadPause;              // the pad pause is over: after re-sheeting it drives on
            _booResolved = true;
            ShowRing(BooRingW, false, 0f); ShowRing(BooRingE, false, 0f);
            SetVulnerable(DizzySeconds * TellScale + (cheese ? CheeseBonus : 0f));
            return true;
        }

        void SetMachine(bool on)
        {
            if (Machine != null) Machine.gameObject.SetActive(on);
            for (int i = 0; i < MachineColliders.Length; i++) if (MachineColliders[i] != null) MachineColliders[i].enabled = on;
            if (!on && Body != null) Body.position = Hidden;
        }

        static void ShowRing(Transform ring, bool on, float fill)
        {
            if (ring == null) return;
            if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
            if (!on) return;
            var f = ring.Find("Fill");
            if (f != null) { float s = Mathf.Max(0.05f, fill); f.localScale = new Vector3(s, 1f, s); }
        }
    }
}
