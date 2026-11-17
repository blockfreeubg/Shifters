// =====================================================================================
//  SHIFTERS - L6DownsizerBoss.cs   [CH03 PERFORMANCE REVIEW - boss brain]
//  THE DOWNSIZER: a 7 m hydraulic HR press (visuals_bosses DownsizerModel, a SIBLING of this object so that
//  standing on the stamp is not "riding the boss"). ME-VISION (ActiveOnly).
//    P1 JAM THE PRESS   fixed slams N -> E -> S -> W (tell 1.5 s, one every 3 s). A pad carrying >= 90 kg of PROPS
//                       when the stamp lands JAMS it for 7 s (heart shutter open). 2 heart hits (any gun / tool).
//    P2 DOWNSIZING      slams the pad nearest the ACTIVE intern (tell 1.2 s). Heart armored; the valve run
//                       (TimedButton + 2 WindowGates, TURBO only) latches ARMOR OPEN 8 s -> 1 heart hit.
//    P3 OVERLOAD        props removed ("HR PROPERTY"). REVIEW: the stamp rests on pad S as a 0.5 m platform
//                       with a 210 kg scale (latches after 2 s), shockwave rings from the column every 3 s
//                       (active intern only; standing on the platform is above them) -> LIFT -> 2 slams at the
//                       active intern's nearest pad -> back to REVIEW. The press never lifts while someone stands on it.
//                       Overload -> strain 3 s -> topples east into the PIGMENT MAIN (orange burst) -> defeated.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L6DownsizerBoss : BossBase
    {
        // ---- wired by L6Builder (serialized)
        public DownsizerModel Model;
        public Transform[] Pads = new Transform[0];          // N, E, S, W (world centres on the floor)
        public Renderer[] PadLampsOn = new Renderer[0];      // blink while that pad is telegraphed
        public Rigidbody[] Props = new Rigidbody[0];         // crate A, crate B, heavy block
        public GameObject[] PropHatches = new GameObject[0]; // shown in P3 (chute closed / bay hatch)
        public GameObject[] PhaseHints = new GameObject[0];  // one hint zone per phase
        public L6ValveLatch Valves;
        public L6ReviewScale Scale;
        public BossWeakPoint Heart;
        public Transform PipePoint;
        public TMPro.TextMeshPro ScaleLabel;

        public const float JamKg = 90f, JamSeconds = 7f, ArmorSeconds = 8f, PadHalf = 1.5f, RestBottom = 3.2f;
        public const int LiftIndex = 3;                      // P3 pattern: 0..2 rings, 3..4 slams at you, 5 back to review
        static readonly string[] PadNames = { "padN", "padE", "padS", "padW" };
        static readonly string[] PadWords = { "NORTH", "EAST", "SOUTH", "WEST" };

        // ---- runtime state (public for bots / tests)
        public int JammedPad { get; private set; } = -1;
        public int Jams { get; private set; }
        public int Fired { get; private set; }
        public bool Overloading => _strain > 0f;
        public bool StampIsPlatform { get; private set; }
        public bool Toppled { get; private set; }
        float _jamTop, _strain, _topple = -1f, _recoverHold;
        int _telegraphPad = -1;
        readonly bool[] _downed = new bool[3];

        public void Configure()
        {
            BossId = "downsizer"; BossName = "THE DOWNSIZER"; BossTitle = "Hydraulic Human Resources"; BarLabel = "QUOTA: FIRED 0/3";
            Rule = BossTargetRule.ActiveOnly;
            TurnToTarget = false; AutoStart = false; IntroSeconds = 2.5f;
            DefeatBanner = "THE DOWNSIZER HAS BEEN DOWNSIZED";
            Phases = new[]
            {
                new BossPhaseSpec { Name = "PHASE 1: JAM THE PRESS", HitsRequired = 2, Accepts = BossHitKind.Shoot, HitsOnlyWhenVulnerable = true,
                    VulnerableSeconds = JamSeconds, CheckpointIndex = 2, IntroBeat = "ch03.p1",
                    Pattern = new[] {
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.5f, false, "padN", 1.9f, 0f, 0f, 0.35f, 1.15f),
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.5f, false, "padE", 1.9f, 0f, 0f, 0.35f, 1.15f),
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.5f, false, "padS", 1.9f, 0f, 0f, 0.35f, 1.15f),
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.5f, false, "padW", 1.9f, 0f, 0f, 0.35f, 1.15f) } },
                new BossPhaseSpec { Name = "PHASE 2: DOWNSIZING PROTOCOL", HitsRequired = 1, Accepts = BossHitKind.Shoot, HitsOnlyWhenVulnerable = true,
                    VulnerableSeconds = ArmorSeconds, CheckpointIndex = 3, IntroBeat = "ch03.p2",
                    Pattern = new[] {
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.2f, false, "padW", 1.9f, 0f, 0f, 0.35f, 1.25f) } },
                new BossPhaseSpec { Name = "PHASE 3: OVERLOAD", HitsRequired = 1, Accepts = BossHitKind.Custom, HitsOnlyWhenVulnerable = false,
                    CheckpointIndex = 4, IntroBeat = "ch03.p3",
                    Pattern = new[] {
                        BossAttackSpec.Make(BossAttackKind.Shockwave, 1.2f, false, "Column", 0.4f, 10f, 8f, 0f, 0.55f),
                        BossAttackSpec.Make(BossAttackKind.Shockwave, 1.2f, false, "Column", 0.4f, 10f, 8f, 0f, 0.55f),
                        BossAttackSpec.Make(BossAttackKind.Shockwave, 1.2f, false, "Column", 0.4f, 10f, 8f, 0f, 0.55f),
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 2.0f, false, "padW", 1.9f, 0f, 0f, 0.35f, 0.9f),
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.2f, false, "padE", 1.9f, 0f, 0f, 0.35f, 0.9f),
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.5f, false, "padS", 1.9f, 0f, 0f, 0.35f, 1.0f) } },
            };
        }

        protected override void Awake()
        {
            if (Phases == null || Phases.Length == 0) Configure();
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            if (Model != null) { Model.SetArmYaw(0f); Model.SetStampBottom(RestBottom); Model.SetShutter(0f); Model.SetHeartExposed(false); }
            SetStampPlatform(false);
            ShowHints(-1);
            if (ScaleLabel != null) ScaleLabel.gameObject.SetActive(false);
        }

        // ================================================================== hooks
        protected override void OnIntro()
        {
            AudioManager.Instance?.PlayMusic(MusicId.Boss, 1.5f);
            Say("PERFORMANCE REVIEW IN PROGRESS: YOU", UIColors.Danger);
        }

        protected override void OnPhaseBegin(int phase)
        {
            EndVulnerableOnHit = phase != 0;          // P1: both heart hits may land in one jam
            ReleaseJam(false);
            ShowHints(phase);
            if (Valves != null) Valves.ResetLatch();
            if (phase == 1) Say("ARMOR PLATING: ON. HEART: CONFIDENTIAL.", UIColors.Danger);
            if (phase == 2) RemoveProps();
            UpdateBar();
        }

        protected override void OnPhaseEnd(int phase) { ReleaseJam(false); }

        protected override void OnAttackStart(BossTelegraph t)
        {
            _telegraphPad = t.Kind == BossAttackKind.SlamZone ? NearestPad(t.Aim) : -1;
            if (t.Kind == BossAttackKind.SlamZone && _telegraphPad >= 0 && Phase == 0)
                Fx.FloatingText(Pads[_telegraphPad].position + Vector3.up * 3.2f, "NEXT: " + PadWords[_telegraphPad], UIColors.Speed, 0.9f);
            if (t.Kind == BossAttackKind.SlamZone && StampIsPlatform) SetStampPlatform(false);
        }

        protected override void OnAttackImpact(BossTelegraph t)
        {
            if (t.Kind != BossAttackKind.SlamZone) return;
            int pad = NearestPad(t.Aim);
            _telegraphPad = -1;
            if (pad < 0) return;
            float top;
            float kg = PropsOnPad(pad, out top);
            if (Phase == 0 && kg >= JamKg)
            {
                JammedPad = pad; _jamTop = top; Jams++;
                Sfx.At(SfxId.MetalHit, Pads[pad].position, 1f, 0.6f);
                Fx.Sparks(Pads[pad].position + Vector3.up * top, Vector3.up, 30, UIColors.AccentYellow);
                Fx.FloatingText(Pads[pad].position + Vector3.up * (top + 1.6f), "PRESS JAMMED!", UIColors.AccentYellow, 1.6f);
                GameEvents.RaiseStoryBeat("ch03.jam");
                SetVulnerable(JamSeconds);
                return;
            }
            if (kg > 0f)
            {
                Fx.FloatingText(Pads[pad].position + Vector3.up * 2.5f, kg >= JamKg ? "HR PROPERTY RETURNED" : "TOO LIGHT TO JAM (" + Mathf.RoundToInt(kg) + " KG)", UIColors.TextMuted, 0.9f);
                if (Phase > 0 || kg < JamKg) RespawnPropsOnPad(pad);
            }
        }

        protected override void OnVulnerableChanged(bool v)
        {
            if (Model != null) Model.SetHeartExposed(v);
            if (v)
            {
                Say(Phase == 0 ? "MALFUNCTION. HEART EXPOSED. DO NOT LOOK." : "ARMOR OPEN. THIS IS A HUMAN RESOURCES EMERGENCY.", UIColors.AccentYellow);
                if (Phase == 1) Sfx.At(SfxId.Steam, transform.position + Vector3.up * 3f, 1f);
            }
            else
            {
                ReleaseJam(true);
                if (Phase == 1 && Valves != null) Valves.ResetLatch();
            }
        }

        protected override void OnHit(int hitsLeft)
        {
            UpdateBar();
            Say(hitsLeft > 0 ? "OW. THAT'S GOING IN YOUR FILE." : Phase == 0 ? "ERROR: QUOTA NOT MET." : "VALVES... VENTING... FEELINGS.", UIColors.Danger);
        }

        protected override void OnHitRejected(BossHitKind kind, Vector3 point)
        {
            Fx.FloatingText(point, Phase == 2 ? "THE HEART IS IN A MEETING" : "CLANG! (ARMORED)", Color.white, 0.8f);
            Sfx.At(SfxId.MetalHit, point, 0.6f);
        }

        protected override void OnDefeated()
        {
            BarLabel = "QUOTA: FIRED " + Fired + "/3 · REVIEW: FAILED";
            RaiseStatus();
            _topple = 0f;
            SetStampPlatform(false);
            ShowHints(-1);
            if (ScaleLabel != null) ScaleLabel.gameObject.SetActive(false);
            Say("I... SHOULD HAVE... DELEGATED.", UIColors.Danger);
        }

        public override SfxId ImpactSfx(BossAttackKind k) => k == BossAttackKind.SlamZone ? SfxId.Stamp : k == BossAttackKind.Shockwave ? SfxId.Shockwave : base.ImpactSfx(k);
        public override SfxId TellSfx(BossAttackKind k) => k == BossAttackKind.SlamZone ? SfxId.Klaxon : base.TellSfx(k);
        public override string HitText(BossAttackKind k) => k == BossAttackKind.SlamZone ? "YOU'RE FIRED!" : "WRITTEN UP!";

        /// <summary>Shockwave rings run along the floor: an intern standing on the 0.5 m review platform is above them.</summary>
        public override bool IsVictim(ShifterCharacter c, BossTelegraph t)
        {
            if (t != null && t.Kind == BossAttackKind.Shockwave && OnStamp(c)) return false;
            return base.IsVictim(c, t);
        }

        // ================================================================== loop
        protected override void FixedUpdate()
        {
            if (State == BossState.Fighting || State == BossState.Vulnerable) PreTick();
            base.FixedUpdate();
            TrackFired();
        }

        void PreTick()
        {
            // P2 / P3: slams aim at the pad nearest the ACTIVE intern (fixed pad list -> deterministic geometry)
            if (Phase >= 1)
            {
                var a = BossTeam.Active;
                if (a != null)
                {
                    int pad = NearestPad(a.Feet);
                    var pat = Phases[Phase].Pattern;
                    if (Phase == 1) pat[0].TargetMark = PadNames[pad];
                    else { pat[3].TargetMark = PadNames[pad]; pat[4].TargetMark = PadNames[pad]; }
                }
            }
            // P2: the valve latch opens the armor
            if (Phase == 1 && State == BossState.Fighting && Valves != null && Valves.IsOn) { SetVulnerable(ArmorSeconds); Sfx.At(SfxId.PuzzleSolved, transform.position, 1f); }
            if (Phase != 2) return;
            // P3: the press waits while somebody is under review
            if (_strain <= 0f && State == BossState.Fighting && !Attacks.Running && Attacks.NextIndex == LiftIndex && AnyoneOnStamp())
            {
                Attacks.Delay(0.25f);
                if (Time.time - _recoverHold > 6f) { _recoverHold = Time.time; Say(Scale != null && Scale.CurrentKg > 0f ? "REVIEW EXTENDED. ONE (1) OF YOU. NOT " + Mathf.Max(1, Scale.CountOn) + "." : "TAKE YOUR TIME.", UIColors.Danger); }
            }
            bool platform = StampAtReview();
            if (platform != StampIsPlatform) SetStampPlatform(platform);
            if (Scale != null) Scale.Armed = StampIsPlatform && _strain <= 0f;
            if (ScaleLabel != null) ScaleLabel.text = "REVIEW SCALE\n<size=70%>" + Mathf.RoundToInt(Scale != null ? Scale.CurrentKg : 0f) + " / " + Mathf.RoundToInt(Scale != null ? Scale.RequiredKg : 210f) + " KG · RATED FOR ONE (1)</size>";
            if (_strain <= 0f && Scale != null && Scale.IsOn) BeginOverload();
            if (_strain > 0f)
            {
                _strain += Time.fixedDeltaTime;
                if (_strain >= 3f) { _strain = 0f; Attacks.Paused = false; TryHit(BossHitKind.Custom, transform.position + Vector3.up * 4f, BossTeam.Active); }
            }
        }

        void BeginOverload()
        {
            _strain = 0.0001f;
            Attacks.Cancel();
            Attacks.Paused = true;
            GameEvents.RaiseStoryBeat("ch03.overload");
            Sfx.At(SfxId.Alarm, transform.position, 1f);
            Fx.FloatingText(transform.position + Vector3.up * 7.5f, "LOAD: THREE EMPLOYEES. RATED FOR: ONE.", UIColors.Danger, 1.6f);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.4f, 3f);
        }

        void TrackFired()
        {
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count && i < _downed.Length; i++)
            {
                var c = team[i];
                bool d = c != null && c.State == CharacterState.Downed;
                if (d && !_downed[i] && IsFighting) { Fired = Mathf.Min(3, Fired + 1); UpdateBar(); Say("ONE DOWN. QUOTA: " + Fired + "/3.", UIColors.Danger); }
                _downed[i] = d;
            }
        }

        protected override void Update()
        {
            base.Update();
            Animate(Time.deltaTime);
        }

        // ================================================================== press animation (kinematic, visual + stamp collider)
        void Animate(float dt)
        {
            if (Model == null) return;
            if (_topple >= 0f) { AnimateTopple(dt); return; }
            float yaw = Model.ArmYaw, bottom = RestBottom; bool fast = false;
            var cur = Attacks.Current;
            if (JammedPad >= 0 && State == BossState.Vulnerable) { yaw = PadYaw(JammedPad); bottom = _jamTop; fast = true; }
            else if (cur != null && cur.Kind == BossAttackKind.SlamZone && State == BossState.Fighting)
            {
                yaw = Model.YawToward(Model.transform.InverseTransformPoint(cur.Aim));
                float rec = cur.Elapsed - cur.WindupSeconds - cur.ActiveSeconds;
                if (cur.Stage == BossTelegraphStage.Windup) bottom = Model.MaxStampBottom;
                else if (cur.Stage == BossTelegraphStage.Active) { bottom = 0f; fast = true; }
                else bottom = (Phase == 2 && cur.PatternIndex == 5) || rec < 0.35f ? 0f : RestBottom;
            }
            else if (Phase == 2 && (State == BossState.Fighting || State == BossState.Vulnerable)) { yaw = 180f; bottom = 0f; }
            else if (State == BossState.PhaseTransition && Phase == 1 && StateTime > 0.8f) { yaw = 180f; bottom = StateTime > 2.2f ? 0f : 1.2f; }
            if (_strain > 0f) { yaw = 180f + Mathf.Sin(Time.time * 40f) * 2.5f; bottom = 0f; }
            Model.SetArmYaw(Mathf.MoveTowardsAngle(Model.ArmYaw, yaw, 260f * dt));
            float b = Model.StampBottom;
            Model.SetStampBottom(b > bottom ? Mathf.MoveTowards(b, bottom, (fast ? 40f : 6f) * dt) : Mathf.MoveTowards(b, bottom, 4.5f * dt));
            Model.SetShutter(Mathf.MoveTowards(Model.ShutterOpen, IsVulnerable ? 1f : 0f, 2.5f * dt));
            // telegraph lamp blink (pad lamps)
            int blink = cur != null && cur.Stage == BossTelegraphStage.Windup && cur.Kind == BossAttackKind.SlamZone ? NearestPad(cur.Aim) : -1;
            bool on = Mathf.Repeat(Time.time * 6f, 1f) < 0.55f;
            for (int i = 0; i < PadLampsOn.Length; i++) if (PadLampsOn[i] != null) PadLampsOn[i].enabled = (i == blink && on) || (i == JammedPad && State == BossState.Vulnerable);
        }

        void AnimateTopple(float dt)
        {
            _topple += dt;
            var root = Model.transform;
            float t = Mathf.Clamp01(_topple / 1.6f);
            float ang = Mathf.Lerp(0f, 72f, t * t);
            // pivot about the plinth's east bottom edge
            var pivot = HomePosition + new Vector3(2f, 0f, 0f);
            root.SetPositionAndRotation(pivot + Quaternion.Euler(0f, 0f, -ang) * (HomePosition - pivot), Quaternion.Euler(0f, 0f, -ang));
            if (t >= 1f && !Toppled)
            {
                Toppled = true;
                var p = PipePoint != null ? PipePoint.position : HomePosition + new Vector3(13f, 3f, 0f);
                Sfx.At(SfxId.Explosion, p, 1f, 0.8f);
                Sfx.At(SfxId.ColorBurst, p, 1f);
                Fx.ColorBurst(p, new Color(1f, 0.55f, 0.1f), 16f);
                Fx.ConfettiFlare(p + Vector3.up, new Color(1f, 0.55f, 0.1f), Vector3.left);
                WorldColor.AddBurst(p, 18f, 3f, new Color(1f, 0.55f, 0.1f), true);
                WorldColor.Wave(p, 3f, WorldColor.Base, Mathf.Min(1f, WorldColor.Base + 0.08f), 60f);
                GameEvents.RaisePuzzleSolvedAt("PIGMENT MAIN BURST: ORANGE IS BACK", p);
                if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.6f, 0.8f);
            }
        }

        // ================================================================== helpers
        float PadYaw(int i) => Model != null && i >= 0 && i < Pads.Length ? Model.YawToward(Model.transform.InverseTransformPoint(Pads[i].position)) : 0f;

        public int NearestPad(Vector3 p)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Pads.Length; i++)
            {
                if (Pads[i] == null) continue;
                float d = BossFootprint.Planar(p, Pads[i].position);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Mass of resting (not carried) props whose centre is over the pad; top = highest prop top.</summary>
        public float PropsOnPad(int pad, out float top)
        {
            top = 0f; float kg = 0f;
            if (pad < 0 || pad >= Pads.Length || Pads[pad] == null) return 0f;
            var c = Pads[pad].position;
            for (int i = 0; i < Props.Length; i++)
            {
                var rb = Props[i];
                if (rb == null || !rb.gameObject.activeInHierarchy || rb.isKinematic) continue;
                var b = PropBounds(rb);
                var p = b.center;
                if (Mathf.Abs(p.x - c.x) > PadHalf + 0.45f || Mathf.Abs(p.z - c.z) > PadHalf + 0.45f || b.min.y > c.y + 0.6f) continue;
                kg += rb.mass;
                top = Mathf.Max(top, b.max.y - c.y);
            }
            return kg;
        }

        static Bounds PropBounds(Rigidbody rb)
        {
            var col = rb.GetComponent<Collider>();
            return col != null ? col.bounds : new Bounds(rb.position, Vector3.one);
        }

        void RespawnPropsOnPad(int pad)
        {
            var c = Pads[pad].position;
            for (int i = 0; i < Props.Length; i++)
            {
                var rb = Props[i];
                if (rb == null || !rb.gameObject.activeInHierarchy || rb.isKinematic) continue;
                var p = PropBounds(rb).center;
                if (Mathf.Abs(p.x - c.x) > PadHalf + 0.45f || Mathf.Abs(p.z - c.z) > PadHalf + 0.45f) continue;
                Fling(rb);
            }
        }

        void Fling(Rigidbody rb)
        {
            Fx.Puff(rb.position, 1.4f, new Color(0.9f, 0.85f, 0.75f));
            var r = rb.GetComponent<Respawnable>();
            if (r != null) r.Respawn();
            var s = rb.GetComponent<Shiftable>();
            if (s != null) s.ResetState();
        }

        void ReleaseJam(bool fling)
        {
            if (JammedPad < 0) return;
            int pad = JammedPad;
            JammedPad = -1;
            if (fling && pad < Pads.Length)
            {
                Fx.FloatingText(Pads[pad].position + Vector3.up * 2.5f, "UNJAMMED. RETURNING HR PROPERTY.", UIColors.TextMuted, 1f);
                RespawnPropsOnPad(pad);
            }
        }

        void RemoveProps()
        {
            for (int i = 0; i < Props.Length; i++)
            {
                var rb = Props[i];
                if (rb == null || !rb.gameObject.activeSelf) continue;
                Fx.Puff(rb.position, 1.6f, new Color(0.9f, 0.85f, 0.75f));
                Fx.FloatingText(rb.position + Vector3.up * 1.5f, "HR PROPERTY", UIColors.Danger, 1f);
                var g = rb.GetComponent<Grabbable>();
                if (g != null && g.Holder != null) g.Holder.ForceRelease();
                rb.gameObject.SetActive(false);
            }
            for (int i = 0; i < PropHatches.Length; i++) if (PropHatches[i] != null) PropHatches[i].SetActive(true);
        }

        void ShowHints(int phase) { for (int i = 0; i < PhaseHints.Length; i++) if (PhaseHints[i] != null) PhaseHints[i].SetActive(i == phase); }

        public bool OnStamp(ShifterCharacter c)
        {
            if (c == null || Model == null || Model.Stamp == null || !StampIsPlatform) return false;
            var s = Model.Stamp.position; var f = c.Feet;
            float h = Model.StampSize * 0.5f + 0.1f;
            return Mathf.Abs(f.x - s.x) <= h && Mathf.Abs(f.z - s.z) <= h && f.y > s.y + Model.StampThickness - 0.2f && f.y < s.y + 2f;
        }

        public bool AnyoneOnStamp()
        {
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++) if (team[i] != null && team[i].IsAlive && OnStamp(team[i])) return true;
            return false;
        }

        bool StampAtReview()
        {
            if (Model == null || IsDefeated) return false;
            if (Model.StampBottom > 0.05f || Mathf.Abs(Mathf.DeltaAngle(Model.ArmYaw, 180f)) > 3f) return false;
            var cur = Attacks.Current;
            if (_strain > 0f) return true;
            return cur == null || cur.Kind == BossAttackKind.Shockwave || (cur.PatternIndex == 5 && cur.Stage == BossTelegraphStage.Recover);
        }

        void SetStampPlatform(bool on)
        {
            StampIsPlatform = on;
            if (Model != null && Model.StampCollider != null)
            {
                if (on && !Model.StampCollider.enabled) NudgeOutOfStamp();
                Model.StampCollider.enabled = on;
            }
            if (ScaleLabel != null) ScaleLabel.gameObject.SetActive(on && !IsDefeated);
            if (!on && Scale != null) Scale.Armed = false;
        }

        /// <summary>Before the stamp becomes solid: anyone standing inside its footprint steps out to the nearest edge.</summary>
        void NudgeOutOfStamp()
        {
            if (Model == null || Model.Stamp == null) return;
            var s = Model.Stamp.position;
            float h = Model.StampSize * 0.5f;
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive || c.Body == null) continue;
                var f = c.Feet; float r = c.Radius + 0.15f;
                float dx = f.x - s.x, dz = f.z - s.z;
                if (Mathf.Abs(dx) > h + r || Mathf.Abs(dz) > h + r || f.y > s.y + 0.4f) continue;
                Vector3 to = f;
                float ex = h + r - Mathf.Abs(dx), ez = h + r - Mathf.Abs(dz);
                if (ex < ez) to.x = s.x + Mathf.Sign(dx == 0f ? 1f : dx) * (h + r); else to.z = s.z + Mathf.Sign(dz == 0f ? -1f : dz) * (h + r);
                c.Body.position = to;
            }
        }

        void UpdateBar()
        {
            string tail = Phase == 0 ? " · JAMS " + Mathf.Min(Hits, 2) + "/2" : Phase == 1 ? " · VALVES" : " · OVERLOAD";
            BarLabel = "QUOTA: FIRED " + Fired + "/3" + tail;
            RaiseStatus();
        }

        void Say(string line, Color c) => Fx.FloatingText(HomePosition + Vector3.up * 8.2f, line, c, 1.25f);
    }
}
