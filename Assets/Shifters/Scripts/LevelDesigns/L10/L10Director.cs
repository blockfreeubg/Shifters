// =====================================================================================
//  SHIFTERS - L10Director.cs   [LEVEL 10 - LIGHTS OUT runtime logic; references set by L10Builder]
//  Garage lamp rows die on ch09.garage; the motion hall counts as lit while its window runs; the floodlight
//  plate lights the E corridor; the S3 lobby pool cuts out 1 s every 4 s; the elevator call wakes the two
//  impatient dummies (HuntAll) until DING; first dummy sighting (ch09.dummy); vending can; copier flash
//  (gated by ScareDirector); phone beat; paper avalanche on the Brute; POWER RESTORED reveal + paper cut.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class L10Director : MonoBehaviour
    {
        public static L10Director Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [Header("S0")] public GameObject[] GarageRows = new GameObject[0];
        public PuzzleSignal[] GarageKills = new PuzzleSignal[0];
        [Header("S1")] public StalkerBrain[] S1Dummies = new StalkerBrain[0];
        public SignalSource[] Breakers = new SignalSource[0];
        public TextMeshPro PowerBoard;
        public TimedButton HallTimer;
        public Transform HallVolume;
        public Vector3 HallVolumeSize = new Vector3(8f, 4f, 34f);
        public GameObject HallGlow;
        public GameObject VendingCan;
        [Header("S2")] public GameObject[] CopierFlashes = new GameObject[0];
        public GameObject CopierDummies;
        public GameObject[] CopierPages = new GameObject[0];
        public Vector3 CopyRoomCenter;
        public PhoneCallView Phone;
        public Vector3 ShelfPoint, ShelfZoneCenter;
        [Header("S3")] public StalkerBrain[] S3Dummies = new StalkerBrain[0];
        public Lever CallLever;
        public HoldLatch CabLatch;
        public PressurePlate FloodPlate;
        public Transform FloodVolume;
        public Vector3 FloodVolumeSize = new Vector3(23f, 4f, 4f);
        public GameObject FloodGlow;
        public PuzzleSignal LobbyCut;
        public LightPool LobbyPool;
        public Transform CabVolume;
        public Vector3 CabVolumeSize = new Vector3(5f, 4f, 5f);
        public GameObject RevealCrowd;
        public GameObject MaxHand;
        [Tooltip("Reveal cinematic: camera at the back of the cab, looking out over the team at the crowd.")]
        public Vector3 RevealCamPos = new Vector3(0f, 3.1f, 83.45f);
        public Vector3 RevealCamLook = new Vector3(0f, 1.5f, 71f);
        [Tooltip("Interns closer than this (planar) to the reveal camera are hidden for the cinematic (no face close-ups).")]
        public float RevealHideRadius = 1.7f;
        [Tooltip("Seconds the reveal cinematic holds the camera if the level has not completed (safety release).")]
        public float RevealCamMax = 9f;

        // ---- state (tests read these)
        public bool GarageDark { get; private set; }
        public bool HallLit { get; private set; }
        public bool FloodLit { get; private set; }
        public bool StandActive { get; private set; }
        public bool Dinged { get; private set; }
        public bool Revealed { get; private set; }
        public bool CopierPlayed { get; private set; }
        public bool AvalanchePlayed { get; private set; }
        public bool DummySighted { get; private set; }
        /// <summary>True while the reveal cinematic owns the gameplay camera.</summary>
        public bool RevealCamActive { get; private set; }
        /// <summary>Visible (not hidden) ThreatLabels right now (tests).</summary>
        public int ThreatLabelsShown { get; private set; }
        public int BreakersOn { get; private set; } = -1;

        float _garageT = -1f, _copierT = -1f, _copierRetry, _copierPendingSince = -1f, _vendingT = -1f, _revealT = -1f, _poll;
        int _garageStep, _copierStep, _handStep;
        bool _callSeen, _phoneRaised, _buzzed;
        Vector3 _handStart;
        GameObject[] _s1Labels = new GameObject[0], _s3Labels = new GameObject[0];
        ShiftersCamera _revealCam;
        Transform _revealCamT;
        Vector3 _camFromPos;
        Quaternion _camFromRot;
        readonly System.Collections.Generic.List<Renderer> _hiddenTeam = new System.Collections.Generic.List<Renderer>();

        void Awake() { Instance = this; }

        void Start()
        {
            FlashlightRig.Ensure();
            GameEvents.StoryBeat += OnBeat;
            if (CabVolume != null) LightRegistry.RegisterVolume(CabVolume, Vector3.zero, CabVolumeSize);
            if (Phone != null) Phone.Punchline = "...also, wrong number. Probably.";
            if (MaxHand != null) _handStart = MaxHand.transform.position;
            _s1Labels = LabelsOf(S1Dummies);
            _s3Labels = LabelsOf(S3Dummies);
            var lm = LevelManager.Instance;
            if (lm != null && lm.StartedFromCheckpoint) KillGarage(true);
        }

        void OnDestroy()
        {
            GameEvents.StoryBeat -= OnBeat;
            EndRevealCam();
            if (CabVolume != null) LightRegistry.UnregisterVolume(CabVolume);
            if (HallVolume != null) LightRegistry.UnregisterVolume(HallVolume);
            if (FloodVolume != null) LightRegistry.UnregisterVolume(FloodVolume);
            if (Instance == this) Instance = null;
        }

        void OnBeat(string id)
        {
            switch (id)
            {
                case "ch09.garage":
                    if (!GarageDark && _garageT < 0f) { ScareDirector.TryPlay("ch09.garage"); _garageT = 0f; _garageStep = 0; }
                    break;
                case "scare.ch09.vending":
                    if (_vendingT < 0f) _vendingT = 0f;
                    break;
                case "l10.copier":
                    if (!CopierPlayed && _copierPendingSince < 0f) _copierPendingSince = Time.time;
                    break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TickGarage(dt);
            TickHall();
            TickFlood();
            TickLobbyPool();
            TickStand();
            TickCopier(dt);
            TickReveal(dt);
            TickLabels();
            if (_vendingT >= 0f && _vendingT < 99f)
            {
                _vendingT += dt;
                if (_vendingT >= 2.2f)
                {
                    _vendingT = 100f;
                    if (VendingCan != null)
                    {
                        VendingCan.SetActive(true);
                        Fx.FloatingText(VendingCan.transform.position + Vector3.up * 1.1f, "LONELINESS (SUGAR-FREE)", UIColors.Hex(0x9FD8FF), 0.9f);
                        Sfx.At(SfxId.Squeak, VendingCan.transform.position, 0.8f, 0.8f);
                    }
                }
            }
            _poll -= dt;
            if (_poll > 0f) return;
            _poll = 0.25f;
            UpdatePowerBoard();
            CheckSighting();
            CheckPhone();
            CheckAvalanche();
        }

        // ------------------------------------------------------------------ S0 garage
        void TickGarage(float dt)
        {
            if (_garageT < 0f || GarageDark) return;
            _garageT += dt;
            // far row (z 6 lamps) -> z 2 -> z -6 -> the row next to the team (z -2): click... click... CLACK
            float[] at = { 0.15f, 0.85f, 1.55f, 2.4f };
            int[] order = { 3, 2, 0, 1 };
            while (_garageStep < 4 && _garageT >= at[_garageStep])
            {
                int r = order[_garageStep];
                KillRow(r);
                Sfx.Play(_garageStep < 3 ? SfxId.Lever : SfxId.DoorSlam, _garageStep < 3 ? 0.7f : 0.9f, _garageStep < 3 ? 1.6f : 0.7f);
                _garageStep++;
            }
            if (_garageStep >= 4) { GarageDark = true; LevelLighting.SetOverride(0.6f, 0.65f, 1.2f); }
        }

        void KillRow(int r)
        {
            if (r >= 0 && r < GarageRows.Length && GarageRows[r] != null) GarageRows[r].SetActive(false);
            if (r >= 0 && r < GarageKills.Length && GarageKills[r] != null) GarageKills[r].Set(true);
        }

        void KillGarage(bool instant)
        {
            for (int r = 0; r < 4; r++) KillRow(r);
            GarageDark = true;
            LevelLighting.SetOverride(0.6f, 0.65f, instant ? 0f : 1.2f);
        }

        // ------------------------------------------------------------------ S1
        void TickHall()
        {
            bool on = HallTimer != null && HallTimer.IsOn;
            if (on == HallLit) return;
            HallLit = on;
            if (HallVolume != null)
            {
                if (on) LightRegistry.RegisterVolume(HallVolume, Vector3.zero, HallVolumeSize);
                else LightRegistry.UnregisterVolume(HallVolume);
            }
            if (HallGlow != null) HallGlow.SetActive(on);
        }

        void UpdatePowerBoard()
        {
            int n = 0;
            for (int i = 0; i < Breakers.Length; i++) if (Breakers[i] != null && Breakers[i].IsOn) n++;
            if (n == BreakersOn) return;
            BreakersOn = n;
            if (PowerBoard != null) PowerBoard.text = n >= 3 ? "POWER: 3/3 - LOBBY OPEN" : "POWER: " + n + "/3 BREAKERS";
        }

        void CheckSighting()
        {
            if (DummySighted) return;
            var cm = CharacterManager.Instance;
            var a = cm != null ? cm.Active : null;
            if (a == null || !a.IsAlive) return;
            foreach (var s in S1Dummies)
            {
                if (s == null || s.IsAsleep) continue;
                if ((s.transform.position - a.transform.position).sqrMagnitude < 13f * 13f)
                {
                    DummySighted = true;
                    GameEvents.RaiseStoryBeat("ch09.dummy");
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ S2
        void TickCopier(float dt)
        {
            if (!CopierPlayed && _copierPendingSince >= 0f)
            {
                _copierRetry -= dt;
                if (_copierRetry <= 0f)
                {
                    _copierRetry = 2f;   // TryPlay logs every refusal: poll gently
                    if (ScareDirector.HasSeen("ch09.copier")) { CopierPlayed = true; ShowPages(); }
                    else if (ScareDirector.TryPlay("ch09.copier") || Time.time - _copierPendingSince > 25f)
                    {
                        CopierPlayed = true; _copierT = 0f; _copierStep = 0;
                        GameEvents.RaiseStoryBeat("ch09.copier");
                    }
                }
            }
            if (_copierT < 0f) return;
            _copierT += dt;
            // 0.00 flash + six dummies | 0.22 dark | 0.40 HELP | 0.90 flash (gone) | 1.10 HELP | 1.12 dark | 2.30 TEAM PHOTO
            float[] at = { 0f, 0.22f, 0.4f, 0.9f, 1.1f, 1.12f, 2.3f };
            while (_copierStep < at.Length && _copierT >= at[_copierStep])
            {
                switch (_copierStep)
                {
                    case 0: SetFlash(true); if (CopierDummies != null) CopierDummies.SetActive(true); Sfx.Play(SfxId.CameraFlash, 0.8f, 0.8f); break;
                    case 1: SetFlash(false); if (CopierDummies != null) CopierDummies.SetActive(false); break;
                    case 2: Page(0); Sfx.Play(SfxId.PrinterWhir, 0.8f); break;
                    case 3: SetFlash(true); Sfx.Play(SfxId.CameraFlash, 0.6f, 1.1f); break;
                    case 4: Page(1); break;
                    case 5: SetFlash(false); break;
                    case 6: Page(2); Sfx.Play(SfxId.SlideWhistleDown, 0.8f); break;
                }
                _copierStep++;
            }
            if (_copierStep >= at.Length) _copierT = -1f;
        }

        void SetFlash(bool on) { foreach (var f in CopierFlashes) if (f != null) f.SetActive(on); }
        void Page(int i)
        {
            if (i < 0 || i >= CopierPages.Length || CopierPages[i] == null) return;
            CopierPages[i].SetActive(true);
            Fx.PaperBurst(CopierPages[i].transform.position, 4, 0.5f);
        }
        void ShowPages() { for (int i = 0; i < CopierPages.Length; i++) if (CopierPages[i] != null) CopierPages[i].SetActive(true); }

        void CheckPhone()
        {
            if (_phoneRaised || Phone == null) return;
            if (Phone.State >= 2) { _phoneRaised = true; GameEvents.RaiseStoryBeat("ch09.phone"); }
        }

        void CheckAvalanche()
        {
            if (AvalanchePlayed) return;
            var cm = CharacterManager.Instance;
            if (cm == null) return;
            foreach (var c in cm.Team)
            {
                if (c == null || !c.IsAlive || c.Archetype != Archetype.Brute) continue;
                var p = c.transform.position;
                if (new Vector2(p.x - ShelfZoneCenter.x, p.z - ShelfZoneCenter.z).sqrMagnitude > 2.2f * 2.2f) continue;
                AvalanchePlayed = true;
                Fx.PaperBurst(ShelfPoint, 12, 1.2f);
                Sfx.At(SfxId.Crumble, ShelfPoint, 0.8f, 1.4f);
                Fx.FloatingText(c.Center + Vector3.up * 1.3f, "I'M OK", UIColors.Hex(0xFFD58A), 1.1f);
                return;
            }
        }

        // ------------------------------------------------------------------ S3
        void TickFlood()
        {
            bool on = (FloodPlate != null && FloodPlate.IsOn) || Revealed;
            if (on == FloodLit) return;
            FloodLit = on;
            if (FloodVolume != null)
            {
                if (on) LightRegistry.RegisterVolume(FloodVolume, Vector3.zero, FloodVolumeSize);
                else LightRegistry.UnregisterVolume(FloodVolume);
            }
            if (FloodGlow != null) FloodGlow.SetActive(on);
        }

        void TickLobbyPool()
        {
            if (LobbyCut == null) return;
            if (Revealed) { if (LobbyCut.IsOn) LobbyCut.Set(false); return; }
            float t = Mathf.Repeat(Time.time, 4f);
            bool cut = t >= 3f;
            if (t >= 2.5f && t < 3f)
            {
                if (!_buzzed) { _buzzed = true; if (TeamNear(LobbyPool != null ? LobbyPool.transform.position : transform.position, 14f)) Sfx.At(SfxId.Sizzle, LobbyPool.transform.position, 0.45f, 0.6f); }
            }
            else _buzzed = false;
            if (LobbyCut.IsOn != cut) LobbyCut.Set(cut);
        }

        void TickStand()
        {
            bool called = CallLever != null && CallLever.IsOn;
            if (called && !_callSeen)
            {
                _callSeen = true;
                StandActive = true;
                foreach (var s in S3Dummies) if (s != null) { s.HuntAll = true; s.SetDormant(false); }
                GameEvents.RaiseStoryBeat("ch09.elevator");
                ScareDirector.SetWindow("l10.stand", true);
                Sfx.Play(SfxId.ElevatorHum, 0.6f);
            }
            if (!Dinged && CabLatch != null && CabLatch.IsLatched)
            {
                Dinged = true;
                StandActive = false;
                foreach (var s in S3Dummies) if (s != null) { s.HuntAll = false; s.SetDormant(true); }
                ScareDirector.SetWindow("l10.stand", false);
                Sfx.Play(SfxId.Banner, 0.9f, 1.3f);
                Fx.FloatingText(CabLatch.transform.position + Vector3.down * 0.6f, "DING!", UIColors.Hex(0xFFE27A), 1.4f);
            }
        }

        void TickReveal(float dt)
        {
            if (Dinged && !Revealed && CabVolume != null)
            {
                var cm = CharacterManager.Instance;
                if (cm != null)
                {
                    int alive = 0, inside = 0;
                    foreach (var c in cm.Team)
                    {
                        if (c == null || !c.IsAlive) continue;
                        alive++;
                        if (InBox(c.transform.position, CabVolume.position, CabVolumeSize)) inside++;
                    }
                    if (alive > 0 && inside == alive) Reveal();     // the whole (alive) team is in the cab: lights on
                }
            }
            if (_revealT < 0f) return;
            _revealT += dt;
            if (MaxHand == null) return;
            if (_handStep == 0 && _revealT >= 0.9f) { _handStep = 1; MaxHand.SetActive(true); Sfx.At(SfxId.CardboardCreak, _handStart, 0.8f); }
            if (_handStep == 1)
            {
                float k = Mathf.Clamp01((_revealT - 0.9f) / 0.6f);
                MaxHand.transform.position = _handStart + Vector3.left * (2.0f * k);   // out from behind the open exit-door panel (|x| < 1.3 is in the reveal frame)
                if (_revealT >= 1.5f)
                {
                    _handStep = 2;
                    Sfx.At(SfxId.Bonk, MaxHand.transform.position, 1f);
                    // the text sits inside the reveal frame (between the hand and the doorway centre)
                    Vector3 hp = MaxHand.transform.position;
                    Fx.FloatingText(new Vector3(-0.1f, hp.y + 0.7f, hp.z - 0.8f), "OUCH. PAPER CUT.", UIColors.Hex(0xFFD58A), 0.75f);
                }
            }
            if (_handStep == 2)
            {
                MaxHand.transform.position += new Vector3(0.6f, 1.6f, 0f) * dt;   // yanked up out of the doorway (stays in frame)
                MaxHand.transform.Rotate(0f, 0f, 360f * dt);
                if (_revealT >= 2.9f) { _handStep = 3; MaxHand.SetActive(false); }
            }
        }

        void Reveal()
        {
            Revealed = true;
            _revealT = 0f;
            if (RevealCrowd != null) RevealCrowd.SetActive(true);
            foreach (var s in S3Dummies) if (s != null) s.gameObject.SetActive(false);
            LevelLighting.SetOverride(1.9f, 2.2f, 0.35f);
            if (CabLatch != null)                             // the IQ "HOLD 10 S = STAYS OPEN" tag is moot now (kept out of the shot)
            {
                var rv = CabLatch.GetComponentInChildren<Revealable>(true);
                if (rv == null) rv = CabLatch.GetComponentInParent<Revealable>();
                if (rv != null) { rv.Label = ""; rv.DynamicLabel = null; }
            }
            BeginRevealCam();                                // turn around: they are RIGHT BEHIND YOU
            TickLabels();
            for (int i = 0; i < 4; i++) Fx.Confetti(new Vector3(-3f + i * 2f, 2.6f, 71.5f));
            Sfx.Play(SfxId.CrowdCheer, 0.9f);
            GameEvents.RaiseStoryBeat("ch09.reveal");
            GameEvents.RaiseToast("POWER RESTORED", UIColors.Hex(0xFFE27A));
        }

        // ------------------------------------------------------------------ reveal cinematic (fixed pose)
        void BeginRevealCam()
        {
            var sc = ShiftersCamera.Instance;
            if (sc == null) return;
            _revealCam = sc;
            _revealCamT = sc.Cam != null ? sc.Cam.transform : sc.transform;
            _camFromPos = _revealCamT.position;
            _camFromRot = _revealCamT.rotation;
            sc.enabled = false;                               // the follow rig stops; this director poses the camera
            RevealCamActive = true;
            _hiddenTeam.Clear();
            var cm = CharacterManager.Instance;
            if (cm != null)
                foreach (var c in cm.Team)
                {
                    if (c == null) continue;
                    Vector3 d = c.transform.position - RevealCamPos; d.y = 0f;
                    if (d.magnitude >= RevealHideRadius) continue;
                    foreach (var r in c.GetComponentsInChildren<Renderer>())
                        if (r != null && r.enabled) { r.enabled = false; _hiddenTeam.Add(r); }
                }
        }

        void EndRevealCam()
        {
            if (!RevealCamActive) return;
            RevealCamActive = false;
            foreach (var r in _hiddenTeam) if (r != null) r.enabled = true;
            _hiddenTeam.Clear();
            if (_revealCam != null) _revealCam.enabled = true;
            _revealCam = null;
        }

        void LateUpdate()
        {
            if (!RevealCamActive) return;
            if (_revealCam == null || _revealCamT == null) { RevealCamActive = false; return; }
            var lm = LevelManager.Instance;
            bool done = lm != null && lm.IsCompleted;
            if (_revealT > RevealCamMax && !done) { EndRevealCam(); return; }   // safety: never keep the player's camera
            float k = Mathf.Clamp01(_revealT / 0.45f);
            k = k * k * (3f - 2f * k);
            Vector3 pos = RevealCamPos + Vector3.back * (0.25f * Mathf.Clamp01(_revealT / 3f));   // a slow push toward the crowd
            Quaternion rot = Quaternion.LookRotation(RevealCamLook - RevealCamPos, Vector3.up);
            _revealCamT.SetPositionAndRotation(Vector3.Lerp(_camFromPos, pos, k), Quaternion.Slerp(_camFromRot, rot, k));
        }

        // ------------------------------------------------------------------ IQ threat labels
        static GameObject[] LabelsOf(StalkerBrain[] dummies)
        {
            var arr = new GameObject[dummies.Length];       // parallel to dummies (null = no label)
            for (int i = 0; i < dummies.Length; i++)
            {
                var m = dummies[i] != null ? dummies[i].GetComponentInChildren<ThreatMarker>(true) : null;
                arr[i] = m != null ? m.gameObject : null;
            }
            return arr;
        }

        /// <summary>A label shows only for an awake dummy within 20 m of the active intern; the S1 labels go off once the
        /// team is past the cubicle farm (checkpoint 2); every label goes off for the reveal.</summary>
        void TickLabels()
        {
            var cm = CharacterManager.Instance;
            var a = cm != null ? cm.Active : null;
            Vector3 ap = a != null ? a.transform.position : Vector3.zero;
            int shown = 0;
            shown += TickLabelSet(S1Dummies, _s1Labels, a != null && ap.z < 50f, ap);
            shown += TickLabelSet(S3Dummies, _s3Labels, a != null, ap);
            ThreatLabelsShown = shown;
        }

        int TickLabelSet(StalkerBrain[] dummies, GameObject[] labels, bool sectionOn, Vector3 ap)
        {
            int shown = 0;
            for (int i = 0; i < labels.Length && i < dummies.Length; i++)
            {
                var go = labels[i];
                if (go == null) continue;
                var s = dummies[i];
                bool on = sectionOn && !Revealed && s != null && !s.IsAsleep && (s.transform.position - ap).sqrMagnitude <= 400f;
                if (go.activeSelf != on) go.SetActive(on);
                if (on) shown++;
            }
            return shown;
        }

        // ------------------------------------------------------------------ helpers
        static bool InBox(Vector3 p, Vector3 c, Vector3 size)
            => Mathf.Abs(p.x - c.x) <= size.x * 0.5f && Mathf.Abs(p.z - c.z) <= size.z * 0.5f && p.y > c.y - size.y * 0.5f - 0.5f && p.y < c.y + size.y * 0.5f;

        static bool TeamNear(Vector3 p, float r)
        {
            var cm = CharacterManager.Instance;
            if (cm == null) return false;
            foreach (var c in cm.Team) if (c != null && c.IsAlive && (c.transform.position - p).sqrMagnitude < r * r) return true;
            return false;
        }
    }
}
