// =====================================================================================
//  SHIFTERS - L14Hearing.cs   [LEVEL 14 - ALL IN FAVOR? the level director (a BossBase-style phase owner, no boss)]
//  Phase = Charges -> Defense -> Filibuster -> Vote -> Catch -> Bench -> Sunrise -> Done.
//  Nothing in this chapter can hurt anyone. Restores from CheckpointManager.CurrentIndex (C2 = 2, C3 = 3).
//  TeamBot reads: Phase, WordLanded(i|name), HumbleDown, Caught, Catcher, CatcherCount, Missed, VistaShown, TimelineHash.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Shifters
{
    public class L14Hearing : MonoBehaviour
    {
        public enum HearingPhase { Charges, Defense, Filibuster, Vote, Catch, Bench, Sunrise, Done }
        public static L14Hearing Instance { get; private set; }
        /// <summary>One timeline hash per run in this launch (TeamBot compares them across seeds).</summary>
        public static readonly List<string> Hashes = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Hashes.Clear(); }

        public static readonly string[] WordNames = { "I", "AM", "VERY", "POPULAR", "AND1", "TALL", "AND2", "HUMBLE" };

        // ---------------- wiring (set by L14Builder, serialized in the scene)
        [Header("S1")] public Pushable Cabinet; public L14PropScale Scale; public Rigidbody Books; public PropSocket Tray; public Terminal Locker;
        public Transform PedestalA, PedestalC; public GameObject PendingA, PendingC; public Transform LockerDoor, SheetStart, SheetEnd, BooksHome;
        [Header("S2")] public L14Word[] Words = new L14Word[8]; public PushButton Mute; public PuzzleSignal FoldSignal; public FoldingRamp Fold;
        public GameObject FoldDecal; public Light DockSpot;
        [Header("S3")] public PushButton[] Aye = new PushButton[3]; public Transform[] Plates = new Transform[3]; public L14Envelope[] Envelopes = new L14Envelope[3];
        public PuzzleSignal VoteSignal; public L14NayButton Nay; public GameObject TallyBoard; public GameObject StickyNote; public TextMeshPro ArticlePoster;
        [Header("S4")] public Transform Cage, Gavel, SideSeatL, SideSeatR; public TextMeshPro ThroneBack, Plaque, ThronePosterSticker;
        public L14BenchSeat[] Seats = new L14BenchSeat[3]; public PuzzleSignal BenchSignal; public Transform MaxHome, DefenseSpot, WitnessSpot, MaxSpot, CatchPoint;
        public GameObject CatchRing; public GameObject MutedSign; public GameObject DockProps;
        [Header("S5")] public PuzzleSignal DoorSignal; public LevelExit Exit; public TextMeshPro Neon; public GameObject[] NorthGlass = new GameObject[0];
        public GameObject[] VistaSets = new GameObject[3]; public Transform MaxStatue, StatueRiser; public Transform PercySpot, Ghosts, Detector;

        // ---------------- state
        public HearingPhase Phase { get; private set; } = HearingPhase.Charges;
        public float PhaseTime => _t;
        public bool ExhibitA { get; private set; }
        public bool ExhibitB { get; private set; }
        public bool ExhibitC { get; private set; }
        public int Evidence => (ExhibitA ? 1 : 0) + (ExhibitB ? 1 : 0) + (ExhibitC ? 1 : 0);
        public bool Muted { get; private set; }
        public bool HumbleDown { get; private set; }
        public bool Caught { get; private set; }
        public bool Missed { get; private set; }
        public ShifterCharacter Catcher { get; private set; }
        public int CatcherCount { get; private set; }
        public Archetype? VistaShown { get; private set; }
        public bool BribeActive { get; private set; }
        public int Bribes { get; private set; }
        public bool Touchdown { get; private set; }
        public float D { get; private set; } = 6f;
        public string TimelineHash { get; private set; } = "";
        public VillainPuppet Max { get; private set; }
        public bool WordLanded(int i) => Words != null && i >= 0 && i < Words.Length && Words[i] != null && Words[i].Landed;
        public bool WordLanded(string name) => WordLanded(System.Array.IndexOf(WordNames, name));

        bool _init, _musicSet, _beeped, _cStarted, _filibuster, _sunriseStarted, _signDone, _statueDone, _balconyDone, _c15Seen, _flying, _foldDone;
        float _t, _benchHold, _booksOut, _sunT, _monologue;
        bool[] _ayePrev = new bool[3];
        readonly bool[] _telegraphed = new bool[8], _dropped = new bool[8];
        readonly List<string> _log = new List<string>();
        readonly List<ShifterCharacter> _ctlHeld = new List<ShifterCharacter>();
        Vector3 _flightFrom, _flightTo;
        float _flightT;
        TallyBoardView _tally;
        PercyPuppet _percy;
        Vector3[] _cpZone = new Vector3[4];
        Checkpoint[] _cps = new Checkpoint[4];
        const float FlightSeconds = 2.6f;
        /// <summary>Catch rule: planar distance to L <= CatchRadius + capsule radius (= the gold ring the player sees, r 1.85).</summary>
        public const float CatchRadius = 1.85f;
        static bool Fast => StoryPlayer.AutoSkip;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable() { GameEvents.StoryBeat += OnBeat; StoryPlayer.ScriptFinished += OnScriptFinished; }
        void OnDisable() { GameEvents.StoryBeat -= OnBeat; StoryPlayer.ScriptFinished -= OnScriptFinished; }

        void Start()
        {
            if (TallyBoard != null) _tally = TallyBoard.GetComponentInChildren<TallyBoardView>(true);
            if (PercySpot != null)
            {
                var pv = PercySpot.GetComponentInChildren<PercyMachineView>(true);
                _percy = pv != null ? pv.Puppet : PercySpot.GetComponentInChildren<PercyPuppet>(true);
                if (_percy != null) _percy.SetWig(true);
            }
            if (MaxHome != null)
            {
                Max = VillainPuppet.Create(transform, MaxHome.position, MaxHome.rotation);
                Max.SetPose(PoseOverride.Seated);
                Max.SetHat(VillainHat.Crown);
            }
            if (Exit != null) Exit.Precondition = () => Phase == HearingPhase.Done ? null : "WAIT FOR THE SUNRISE (IT'S WORTH IT).";
            for (int i = 0; i < Words.Length; i++) if (Words[i] != null) Words[i].ResetToCloud();
            foreach (var cp in Checkpoint.All)
                if (cp != null && cp.Index >= 2 && cp.Index <= 3) { _cps[cp.Index] = cp; _cpZone[cp.Index] = cp.ZoneSize; cp.ZoneSize = Vector3.zero; }
            if (MutedSign != null) MutedSign.SetActive(false);
            if (CatchRing != null) CatchRing.SetActive(false);
            if (FoldDecal != null) FoldDecal.SetActive(false);
            if (DockSpot != null) DockSpot.enabled = false;
            for (int i = 0; i < VistaSets.Length; i++) if (VistaSets[i] != null) VistaSets[i].SetActive(false);
            for (int i = 0; i < Envelopes.Length; i++) if (Envelopes[i] != null) Envelopes[i].ResetState();
            for (int i = 0; i < Seats.Length; i++) if (Seats[i] != null) Seats[i].Open = false;
        }

        // ================================================================== init / restore
        void Init()
        {
            _init = true;
            D = (StoryPlayer.AutoSkip || StoryPlayer.StoryMode == 2) ? 1f : 6f;
            var lm = LevelManager.Instance;
            int idx = lm != null && lm.StartedFromCheckpoint && CheckpointManager.Instance != null ? CheckpointManager.Instance.CurrentIndex : 1;
            LogEvent("init.cp" + idx);
            if (idx >= 3) { RestoreCharges(); RestoreWordsLanded(); OnMuted(true); }
            else if (idx == 2) { RestoreCharges(); StartDefense(); }
            else
            {
                Phase = HearingPhase.Charges;
                SetObjective("IMPEACH MAX · EVIDENCE 0/3");
                var cd = CampaignDirector.Instance;
                if (cd == null || !cd.IsCampaign) GameEvents.RaiseStoryBeat("ch14.start");
            }
        }

        void RestoreCharges()
        {
            if (Cabinet != null && Cabinet.Body != null && Scale != null)
            {
                Vector3 p = Scale.transform.position + Vector3.up * 0.8f;
                Cabinet.Body.position = p; Cabinet.transform.position = p; Cabinet.Body.linearVelocity = Vector3.zero;
            }
            if (Scale != null) Scale.ForceLatch();
            if (Books != null && Tray != null) { LockBooks(); Books.position = Tray.transform.position + Vector3.up * 0.5f; Books.transform.position = Books.position; }
            if (LockerDoor != null) LockerDoor.localRotation = Quaternion.Euler(0f, -105f, 0f);
            ShowExhibit("A", PedestalA, PendingA, false);
            ShowExhibit("C", PedestalC, PendingC, false);
            ExhibitA = ExhibitB = ExhibitC = true; _cStarted = true;
            _tally?.SetTally(0, 1, "EVIDENCE 3/3");
        }

        void RestoreWordsLanded()
        {
            _filibuster = true;
            for (int i = 0; i < Words.Length; i++) { _telegraphed[i] = _dropped[i] = true; if (Words[i] != null) Words[i].LandNow(); }
        }

        // ================================================================== update
        void Update()
        {
            if (!_init)
            {
                var lm = LevelManager.Instance;
                if (lm == null || lm.IsStarted) Init();
                return;
            }
            float dt = Time.deltaTime;
            _t += dt;
            var sp = StoryPlayer.Instance;
            bool blocking = sp != null && sp.IsBlocking;
            if (!_musicSet && !blocking && Phase < HearingPhase.Catch) { _musicSet = true; AudioManager.Instance?.PlayMusic(MusicId.Villain, 1.5f); }
            Detect();
            LockAyes();
            switch (Phase)
            {
                case HearingPhase.Charges: UpdateCharges(dt); break;
                case HearingPhase.Defense: UpdateDefense(); break;
                case HearingPhase.Vote: UpdateVote(); break;
                case HearingPhase.Catch: UpdateCatch(dt); break;
                case HearingPhase.Bench: UpdateBench(dt); break;
                case HearingPhase.Sunrise: UpdateSunrise(dt); break;
            }
            if (_filibuster) UpdateWords();
            if (Phase == HearingPhase.Filibuster && !Muted)
            {
                _monologue -= dt;
                if (_monologue <= 0f && Max != null) { _monologue = 3.3f; Max.Play(VillainGesture.Monologue); }
                if (Mute != null && Mute.IsOn) OnMuted(false);
            }
        }

        void Detect()
        {
            if (_beeped || Detector == null) return;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive) continue;
                Vector3 p = c.transform.position - Detector.position;
                if (Mathf.Abs(p.x) < 2.2f && Mathf.Abs(p.z) < 0.5f) { _beeped = true; StartCoroutine(Beeps()); return; }
            }
        }

        IEnumerator Beeps()
        {
            for (int i = 0; i < 3; i++) { Sfx.At(SfxId.CameraBeep, Detector.position + Vector3.up * 2f, 0.9f, 1.2f + i * 0.15f); yield return new WaitForSeconds(0.22f); }
            StoryKitSay.Say("PA", "PA", UIColors.Info, "TEAM DETECTED. HEARING IN PROGRESS. CARRY ON.", 3f);
        }

        void LockAyes()
        {
            for (int i = 0; i < Aye.Length; i++)
            {
                var b = Aye[i];
                if (b == null) continue;
                bool on = b.IsOn;
                if (on && !_ayePrev[i])
                {
                    if (Phase != HearingPhase.Vote) { b.ResetState(); on = false; Sfx.At(SfxId.Denied, b.transform.position); GameEvents.RaiseToast("THE FLOOR IS NOT OPEN FOR VOTING.", UIColors.AccentYellow); }
                    else
                    {
                        LogEvent("aye" + (i + 1));
                        Nay?.Honk();
                        if (Max != null) { Max.Play(VillainGesture.Rage); }
                    }
                }
                _ayePrev[i] = on;
            }
            if (Phase == HearingPhase.Vote && _tally != null)
            {
                int ayes = 0;
                for (int i = 0; i < Aye.Length; i++) if (Aye[i] != null && Aye[i].IsOn) ayes++;
                if (ayes != _tally.Aye) _tally.SetTally(ayes, 1, ayes > 0 ? "NAY: STILL 1. ONE VOTE PER PERSON. HE IS ONE PERSON." : "ALL IN FAVOR?");
            }
        }

        // ================================================================== S1 THE CHARGES
        void UpdateCharges(float dt)
        {
            if (!ExhibitA && Scale != null && Scale.IsOn) SolveA();
            if (!ExhibitB && Tray != null && Tray.IsOn && Tray.Seated != null && Books != null && Tray.Seated.Body == Books) SolveB();
            if (!_cStarted && Locker != null && Locker.IsOn) { _cStarted = true; StartCoroutine(SolveC()); }
            if (!ExhibitB && Books != null)
            {
                Vector3 p = Books.position;
                bool outside = p.y < -1f || Mathf.Abs(p.x) > 17f || p.z < -9f || p.z > 45f;
                _booksOut = outside ? _booksOut + dt : 0f;
                if (_booksOut > 3f && BooksHome != null)
                {
                    _booksOut = 0f;
                    var g = Books.GetComponent<Grabbable>();
                    if (g == null || !g.IsHeld) { Books.linearVelocity = Vector3.zero; Books.position = BooksHome.position; Books.transform.position = BooksHome.position; Fx.Poof(BooksHome.position, UIColors.AccentYellow); }
                }
            }
            if (ExhibitA && ExhibitB && ExhibitC) StartDefense();
        }

        void Solved(string msg, Vector3 at, string beat)
        {
            GameEvents.RaisePuzzleSolved(msg);
            GameEvents.RaisePuzzleSolvedAt(msg, at);
            GameEvents.RaiseStoryBeat(beat);
            Sfx.At(SfxId.CrowdGasp, at, 1f);
            Sfx.At(SfxId.Gavel, PercySpot != null ? PercySpot.position : at, 0.9f);
            _percy?.Kick(0.6f);
            LogEvent(beat);
            _tally?.SetTally(0, 1, msg + " · EVIDENCE " + Evidence + "/3");
            SetObjective("IMPEACH MAX · EVIDENCE " + Evidence + "/3");
        }

        void SolveA()
        {
            ExhibitA = true;
            if (Cabinet != null) Fx.PaperBurst(Cabinet.transform.position + Vector3.up * 1.0f, 24, 1f);
            ShowExhibit("A", PedestalA, PendingA, true);
            Solved("EXHIBIT A: THE GREY MEMO", PedestalA != null ? PedestalA.position : transform.position, "ch14.exhibitA");
        }

        void SolveB()
        {
            ExhibitB = true;
            LockBooks();
            Solved("EXHIBIT B: THE BOOKS", Tray.transform.position, "ch14.exhibitB");
        }

        void LockBooks()
        {
            if (Books == null) return;
            var g = Books.GetComponent<Grabbable>();
            if (g != null) { g.CanBeGrabbed = false; }
            Books.isKinematic = true;
        }

        IEnumerator SolveC()
        {
            float k = Fast ? 0.3f : 1f;
            if (LockerDoor != null)
                for (float t = 0f; t < 0.3f; t += Time.deltaTime) { LockerDoor.localRotation = Quaternion.Euler(0f, -105f * (t / 0.3f), 0f); yield return null; }
            ScareDirector.TryPlay("ch14.sheet");
            GhostSheet sheet = null;
            if (SheetStart != null)
            {
                sheet = GhostSheet.Create(transform, SheetStart.position, SheetStart.rotation, "LAUNDRY");
                sheet.Boo();
                Sfx.At(SfxId.Boo, SheetStart.position, 1f);
                StoryKitSay.Say("SHEET", "BEDSHEET", Color.white, "BOO!", 1.2f);
            }
            yield return new WaitForSeconds(0.9f * k);
            if (sheet != null && SheetEnd != null)
            {
                Sfx.At(SfxId.SlideWhistleDown, SheetStart.position, 0.9f);
                Vector3 a = sheet.transform.position, b = SheetEnd.position + Vector3.up * 1.2f;
                float dur = 2.0f * k;
                for (float t = 0f; t < dur; t += Time.deltaTime)
                {
                    float u = t / dur;
                    sheet.transform.position = Vector3.Lerp(a, b, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.3f;
                    sheet.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, u);
                    yield return null;
                }
                Destroy(sheet.gameObject);
            }
            GameEvents.RaiseToast("FOLDED.", UIColors.Text);
            ShowExhibit("C", PedestalC, PendingC, true);
            ExhibitC = true;
            Solved("EXHIBIT C: THE BEDSHEET", PedestalC != null ? PedestalC.position : transform.position, "ch14.exhibitC");
        }

        void ShowExhibit(string id, Transform at, GameObject pending, bool animate)
        {
            if (at == null || at.childCount > 0) return;
            if (pending != null) pending.SetActive(false);
            var go = StoryKit.ExhibitProp(at, Vector3.zero, id);
            if (go != null && animate) StartCoroutine(Pop(go.transform));
        }

        IEnumerator Pop(Transform t)
        {
            for (float u = 0f; u < 0.45f; u += Time.deltaTime) { t.localScale = Vector3.one * Mathf.SmoothStep(0.1f, 1f, u / 0.45f) * (1f + 0.15f * Mathf.Sin(u * 14f)); yield return null; }
            t.localScale = Vector3.one;
        }

        // ================================================================== S2 THE DEFENSE + THE FILIBUSTER
        void StartDefense()
        {
            Phase = HearingPhase.Defense; _t = 0f;
            ActivateCp(2);
            SetObjective("SILENCE THE FILIBUSTER");
            for (int i = 0; i < Words.Length; i++) { _telegraphed[i] = _dropped[i] = false; if (Words[i] != null) Words[i].ResetToCloud(); }
            GameEvents.RaiseStoryBeat("ch14.defense");
            LogEvent("defense");
            StartCoroutine(DefenseShow());
        }

        IEnumerator DefenseShow()
        {
            float k = D / 6f;
            if (Max != null) { Max.SetHat(VillainHat.Judge); Max.SetPose(PoseOverride.None); yield return Hop(DefenseSpot, 0.45f * k); }
            yield return new WaitForSeconds(1.5f * k - 0.45f * k);
            if (Max != null) yield return Hop(WitnessSpot, 0.45f * k);
            yield return new WaitForSeconds(1.5f * k - 0.45f * k);
            if (Max != null) { Max.SetHat(VillainHat.Crown); Max.SetExpression(Expression.Happy, 1.5f); }
            yield return new WaitForSeconds(1.0f * k);
            if (_percy != null) { _percy.SetFace(PercyFace.Upset, 2f); _percy.Puff(); }
            yield return new WaitForSeconds(1.0f * k);
            if (Ghosts != null) { foreach (var g in Ghosts.GetComponentsInChildren<GhostSheet>(true)) g.Boo(); Sfx.At(SfxId.Boo, Ghosts.position, 0.9f); }
            if (Max != null) yield return Hop(MaxHome, 0.3f * k);
        }

        IEnumerator Hop(Transform to, float dur)
        {
            if (Max == null || to == null) yield break;
            Vector3 a = Max.transform.position, b = to.position;
            Quaternion ra = Max.transform.rotation;
            dur = Mathf.Max(0.05f, dur);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                Max.transform.position = Vector3.Lerp(a, b, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.6f;
                Max.transform.rotation = Quaternion.Slerp(ra, to.rotation, u);
                yield return null;
            }
            Max.transform.SetPositionAndRotation(b, to.rotation);
        }

        void UpdateDefense()
        {
            if (_t >= D) StartFilibuster();
        }

        void StartFilibuster()
        {
            Phase = HearingPhase.Filibuster; _t = D;
            _filibuster = true;
            GameEvents.RaiseStoryBeat("ch14.filibuster");
            LogEvent("filibuster");
            LevelLighting.SetOverride(0.7f, 0.7f, 0.6f);
            if (DockSpot != null) DockSpot.enabled = true;
            ShowTutorial("ch14_filibuster", "THE FILIBUSTER", "His words are falling from the ceiling. Climb them and hit MUTE.", false);
            if (Max != null) { Max.SetPose(PoseOverride.None); Max.Play(VillainGesture.Monologue); _monologue = 3.3f; }
        }

        void UpdateWords()
        {
            // the word timeline keeps running after MUTE (POPULAR and HUMBLE finish their fall)
            for (int i = 0; i < Words.Length; i++)
            {
                var w = Words[i];
                if (w == null) continue;
                float tTel = D + 0.5f * i, tDrop = D + 1.0f + 0.5f * i;   // drop starts 0.5 s before the landing at D + 1.5 + 0.5 i
                if (!_telegraphed[i] && (_t >= tTel || Phase > HearingPhase.Filibuster)) { _telegraphed[i] = true; w.BeginTelegraph(); LogEvent("tel." + WordNames[i]); }
                if (!_dropped[i] && (_t >= tDrop || Phase > HearingPhase.Filibuster)) { _dropped[i] = true; w.BeginDrop(); LogEvent("drop." + WordNames[i]); }
            }
        }

        public void OnWordLanded(L14Word w)
        {
            if (w == null) return;
            LogEvent("land." + (w.Index >= 0 && w.Index < WordNames.Length ? WordNames[w.Index] : w.Word));
            if (w.Index == 3) StoryKitSay.Say("PA", "PA", UIColors.Info, "NOT EVEN HIS LIES FIT ON THE STAIRS.", 3f);
        }

        void OnMuted(bool restore)
        {
            if (Muted) return;
            Muted = true;
            Phase = HearingPhase.Vote; _t = 0f;
            if (!restore)
            {
                GameEvents.RaiseStoryBeat("ch14.muted");
                GameEvents.RaisePuzzleSolved("MUTED");
                GameEvents.RaisePuzzleSolvedAt("MUTED", Mute != null ? Mute.transform.position : transform.position);
            }
            LogEvent("muted");
            LevelLighting.ClearOverride(0.6f);
            if (DockSpot != null) DockSpot.enabled = false;
            if (MutedSign != null) MutedSign.SetActive(true);
            if (Max != null) { Max.Talking = true; Max.SetPose(PoseOverride.Seated); Max.transform.SetPositionAndRotation(MaxHome.position, MaxHome.rotation); }
            if (Fold != null) Fold.Duration = Fast ? 0.6f : 2.0f;
            if (FoldDecal != null) { FoldDecal.SetActive(true); StartCoroutine(HideLater(FoldDecal, 3f)); }
            FoldSignal?.Set(true);
            ActivateCp(3);
            GameEvents.RaiseStoryBeat("ch14.vote");
            _tally?.SetTally(0, 1, "ALL IN FAVOR?");
            ShowTutorial("ch14_vote", "UNANIMOUS VOTE", "UNANIMOUS VOTE: one intern per plate, then AYE with all three within 5 seconds. SHIFT fast.", true);
            SetObjective("ALL IN FAVOR?");
            for (int i = 0; i < Envelopes.Length; i++) if (Envelopes[i] != null) StartCoroutine(SlideEnvelope(Envelopes[i]));
        }

        IEnumerator HideLater(GameObject go, float s) { yield return new WaitForSeconds(s); if (go != null) go.SetActive(false); }

        IEnumerator SlideEnvelope(L14Envelope e)
        {
            e.gameObject.SetActive(true); e.Available = true;
            Vector3 b = e.Home, a = b + Vector3.up * 1.2f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { e.transform.position = Vector3.Lerp(a, b, t / 0.5f); yield return null; }
            e.transform.position = b;
        }

        public void ShootHumble()
        {
            var w = Words.Length > 7 ? Words[7] : null;
            if (HumbleDown || w == null || !w.Landed) return;
            HumbleDown = true;
            w.KnockDown(new Vector3(8.5f, 0f, 29f));
            foreach (var tmp in w.GetComponentsInChildren<TMP_Text>(true)) tmp.text = "HUMBLE\n<size=45%>(FOUND ON FLOOR)</size>";
            GameEvents.RaiseStoryBeat("ch14.humble");
            GameEvents.RaisePuzzleSolvedAt("OBJECTION SUSTAINED", w.transform.position);
            GameEvents.RaiseToast("HUMBLE (FOUND ON FLOOR)", VillainLook.Gold);
            LogEvent("humble");
        }

        // ================================================================== S3 THE VOTE
        public bool PlateOccupied(int n, out ShifterCharacter who)
        {
            who = null;
            if (Plates == null || n < 0 || n >= Plates.Length || Plates[n] == null) return false;
            Vector3 c = Plates[n].position;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var ch = all[i];
                if (ch == null || !ch.IsAlive) continue;
                Vector3 p = ch.transform.position;
                if (Mathf.Abs(p.x - c.x) <= 1.0f && Mathf.Abs(p.z - c.z) <= 1.0f && p.y < c.y + 0.8f) { who = ch; return true; }
            }
            return false;
        }

        public bool KeyTurned(int n) => PlateOccupied(n, out _) && Aye[n] != null && Aye[n].IsOn;

        void UpdateVote()
        {
            if (BribeActive || Phase != HearingPhase.Vote) return;
            if (KeyTurned(0) && KeyTurned(1) && KeyTurned(2)) Impeach();
        }

        public void Bribe(ShifterCharacter who, L14Envelope env)
        {
            if (BribeActive || Phase != HearingPhase.Vote || who == null) return;
            StartCoroutine(BribeBeat(who, env));
        }

        IEnumerator BribeBeat(ShifterCharacter who, L14Envelope env)
        {
            BribeActive = true; Bribes++;
            LogEvent("bribe");
            bool prev = who.ExternalControl;
            who.ExternalControl = true;
            StoryText.SetToken("NAME", who.DisplayName.ToUpperInvariant());
            GameEvents.RaiseStoryBeat("ch14.bribe");
            env.gameObject.SetActive(false);
            var crown = BuildCrown(who.transform);
            Vector3 a = who.transform.position, b = new Vector3(a.x, a.y, 23.5f);
            for (float t = 0f; t < 0.7f; t += Time.fixedDeltaTime)
            {
                Vector3 p = Vector3.Lerp(a, b, t / 0.7f);
                if (who.Body != null) who.Body.MovePosition(p); else who.transform.position = p;
                yield return new WaitForFixedUpdate();
            }
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i] != who && all[i].Visual != null) all[i].Visual.Play(VisualAction.Facepalm);
            yield return new WaitForSeconds(2.3f);
            if (crown != null) { crown.SetParent(transform, true); crown.position = env.Home + new Vector3(0.1f, 0.05f, 0f); crown.rotation = Quaternion.Euler(0f, 20f, 8f); }
            who.ExternalControl = prev;
            BribeActive = false;
        }

        Transform BuildCrown(Transform head)
        {
            var root = Kit.Make("PaperCrown", head, new Vector3(0f, 1.95f, 0f)).transform;
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Kit.BoxVisual(root, "Point" + i, new Vector3(Mathf.Sin(a) * 0.17f, 0.07f, Mathf.Cos(a) * 0.17f), new Vector3(0.1f, 0.18f, 0.02f), MatKey.Gold, 0.005f, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), false);
            }
            Kit.BoxVisual(root, "Band", new Vector3(0f, 0f, 0f), new Vector3(0.38f, 0.06f, 0.38f), MatKey.Gold, 0.005f, null, false);
            Sfx.At(SfxId.Popper, head.position + Vector3.up * 2f, 0.8f);
            GameEvents.RaiseToast("VICE PRESIDENT (OF SANDWICHES)", VillainLook.Gold);
            return root;
        }

        void Impeach()
        {
            VoteSignal?.Set(true);
            Phase = HearingPhase.Catch; _t = 0f;
            LogEvent("impeached");
            GameEvents.RaiseStoryBeat("ch14.impeached");
            _tally?.SetTally(3, 1, "MOTION CARRIES. MAXIMILIAN ME IS HEREBY... IMPEACHED.");
            if (StickyNote != null) StartCoroutine(DropNote());
            WorldColor.Wave(TallyBoard != null ? TallyBoard.transform.position : transform.position, Fast ? 0.5f : 3f, 0.85f, 1.00f);
            Sfx.Play(SfxId.CrowdCheer, 1f);
            AudioManager.Instance?.PlayMusic(MusicId.Finale, 1f);
            SetObjective("CATCH HIM!");
            for (int i = 0; i < Envelopes.Length; i++) if (Envelopes[i] != null) Envelopes[i].Available = false;
            if (MutedSign != null) MutedSign.SetActive(false);
            if (Max != null) { Max.Talking = false; Max.SetExpression(Expression.Shocked, 2f); }
            StoryKitSay.Max("...uh-oh.", 1.6f, Expression.Shocked);
        }

        IEnumerator DropNote()
        {
            Vector3 a = StickyNote.transform.position;
            if (ArticlePoster != null) ArticlePoster.text = "ARTICLE 3\nEXISTS.";
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                StickyNote.transform.position = a + new Vector3(Mathf.Sin(t * 9f) * 0.25f, -t * 2.2f, -0.1f);
                StickyNote.transform.Rotate(0f, 0f, 260f * Time.deltaTime);
                yield return null;
            }
            StickyNote.SetActive(false);
        }

        // ================================================================== S4 THE CATCH
        void UpdateCatch(float dt)
        {
            if (Cage != null && _t <= 0.7f) Cage.localPosition = new Vector3(0f, Mathf.SmoothStep(0f, 4f, Mathf.Clamp01(_t / 0.6f)), 0f);
            if (Gavel != null && !_flying && !Touchdown)
            {
                if (_t >= 0.3f && _t < 1.5f) Gavel.localRotation = Quaternion.Euler(Mathf.Lerp(-65f, -100f, Mathf.SmoothStep(0f, 1f, (_t - 0.3f) / 1.2f)), 0f, 0f);
            }
            if (!_flying && !Touchdown && _t >= 1.5f) Launch();
            if (_flying)
            {
                _flightT += dt;
                float u = Mathf.Clamp01(_flightT / FlightSeconds);
                if (Gavel != null) Gavel.localRotation = Quaternion.Euler(Mathf.Lerp(20f, -65f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_flightT - 0.4f) / 1.5f))), 0f, 0f);
                if (Max != null)
                {
                    Vector3 p = Vector3.Lerp(_flightFrom, _flightTo, u) + Vector3.up * (4f * 7.5f * u * (1f - u));
                    Max.transform.position = p;
                    Max.transform.rotation = Quaternion.Euler(-360f * u, 180f, 0f);
                }
                if (u >= 1f) DoTouchdown();
            }
        }

        void Launch()
        {
            _flying = true; _flightT = 0f;
            LogEvent("launch");
            if (Gavel != null) Gavel.localRotation = Quaternion.Euler(20f, 0f, 0f);
            Sfx.At(SfxId.Gavel, MaxHome.position, 1f, 0.55f);
            Sfx.At(SfxId.Whoosh, MaxHome.position, 1f, 0.8f);
            Fx.ShockwaveRing(MaxHome.position, 4f, VillainLook.Gold, 0.6f);
            GameEvents.RaiseToast("BONG.", UIColors.AccentYellow);   // no centre banner: the ch14_catch tutorial carries "CATCH HIM!" and the flight stays visible during the slow-mo
            _flightFrom = MaxHome.position + Vector3.up * 0.6f;
            _flightTo = CatchPoint.position + Vector3.up * 0.6f;
            if (Max != null) { Max.SetPose(PoseOverride.Flop); Max.SetExpression(Expression.Shocked, 3f); }
            if (CatchRing != null) CatchRing.SetActive(true);
            ShowTutorial("ch14_catch", "CATCH HIM!", "CATCH HIM! Get anyone into the gold ring.", true);
            TimeController.HitStop(0.3f, 3.0f);
        }

        void DoTouchdown()
        {
            _flying = false; Touchdown = true; _t = 0f;
            Vector3 L = CatchPoint.position;
            var all = ShifterCharacter.All;
            ShifterCharacter nearest = null, nearestAny = null; float best = 999f, bestAny = 999f;
            CatcherCount = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive) continue;
                Vector3 p = c.transform.position;
                float d = new Vector2(p.x - L.x, p.z - L.z).magnitude;
                if (d < bestAny) { bestAny = d; nearestAny = c; }
                if (d <= CatchRadius + c.Radius && Mathf.Abs(p.y - L.y) <= 2.0f)
                {
                    CatcherCount++;
                    if (c.Visual != null) c.Visual.Play(VisualAction.Catch);
                    if (d < best) { best = d; nearest = c; }
                }
            }
            if (CatchRing != null) CatchRing.SetActive(false);
            if (nearest != null)
            {
                Caught = true; Catcher = nearest;
                LogEvent("caught");
                if (Max != null)
                {
                    var cp = nearest.CarryPoint != null ? nearest.CarryPoint : nearest.transform;
                    Max.transform.SetParent(cp, false);
                    Max.transform.localPosition = Vector3.zero; Max.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    Max.SetPose(PoseOverride.Carried);
                    Max.SetExpression(Expression.Sad, 3f);
                }
                if (nearest.Archetype != Archetype.Brute) nearest.Say("OOF.");
                StartCoroutine(HoldCatcher(nearest));
                if (CatcherCount >= 3) GameEvents.RaiseToast("TRUST FALL · CAUGHT BY 3 (OF 3)", VillainLook.Gold);
                else if (CatcherCount == 2) GameEvents.RaiseToast("TEAM CATCH · CAUGHT BY 2", VillainLook.Gold);
                GameEvents.RaiseStoryBeat("ch14.caught");
            }
            else
            {
                Missed = true; Catcher = nearestAny;
                LogEvent("landed");
                if (Max != null) { Max.transform.position = new Vector3(L.x, L.y, L.z); Max.transform.rotation = Quaternion.Euler(-90f, 180f, 0f); Max.SetPose(PoseOverride.Flop); }
                Sfx.At(SfxId.Flop, L, 1f);
                if (nearestAny != null && nearestAny.Visual != null) nearestAny.Visual.Play(VisualAction.Catch);
                GameEvents.RaiseStoryBeat("ch14.landed");
                StartCoroutine(MaxToSpotLater(1.5f));
            }
            StoryText.Catcher = Catcher != null ? Catcher.DisplayName.ToUpperInvariant() : "THE TEAM";
            SetObjective("TAKE A SEAT. ALL OF YOU.");
            StartCoroutine(FoldThrone());
        }

        IEnumerator HoldCatcher(ShifterCharacter c)
        {
            bool prev = c.ExternalControl;
            c.ExternalControl = true;
            yield return new WaitForSeconds(1.5f);
            c.ExternalControl = prev;
            yield return MaxToSpot();
        }

        IEnumerator MaxToSpotLater(float s) { yield return new WaitForSeconds(s); yield return MaxToSpot(); }

        IEnumerator MaxToSpot()
        {
            if (Max == null || MaxSpot == null) yield break;
            Max.transform.SetParent(transform, true);
            Max.SetPose(PoseOverride.None);
            Max.transform.rotation = MaxSpot.rotation;
            yield return Hop(MaxSpot, 0.8f);
            Max.SetExpression(Expression.Pout, 3f);
        }

        IEnumerator FoldThrone()
        {
            yield return new WaitForSeconds(0.5f);
            LogEvent("fold");
            Sfx.At(SfxId.Creak, MaxHome.position, 1f);
            float dur = Fast ? 0.4f : 1.5f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / dur);
                if (SideSeatL != null) SideSeatL.localPosition = new Vector3(-1.6f * u, 0f, 0f);
                if (SideSeatR != null) SideSeatR.localPosition = new Vector3(1.6f * u, 0f, 0f);
                yield return null;
            }
            if (SideSeatL != null) SideSeatL.localPosition = new Vector3(-1.6f, 0f, 0f);
            if (SideSeatR != null) SideSeatR.localPosition = new Vector3(1.6f, 0f, 0f);
            if (ThroneBack != null) ThroneBack.text = "THREE";
            if (Plaque != null) Plaque.text = "BENCH FOR THREE\n<size=70%>(NO SAVING SEATS)</size>";
            if (ThronePosterSticker != null) ThronePosterSticker.gameObject.SetActive(true);
            if (DockProps != null) DockProps.SetActive(false);
            Fx.ConfettiFlare(MaxHome.position + Vector3.up * 1.5f, VillainLook.Gold);
            _foldDone = true;
            float wait = Mathf.Max(0f, 2.0f - 0.5f - dur);
            yield return new WaitForSeconds(wait);
            Phase = HearingPhase.Bench; _t = 0f;
            LogEvent("bench.open");
            for (int i = 0; i < Seats.Length; i++) if (Seats[i] != null) Seats[i].Open = true;
            ShowTutorial("ch14_bench", "BENCH FOR THREE", "BENCH FOR THREE: everybody sit down (E). Rulers sit together.", false);
        }

        // ================================================================== S4 THE BENCH -> C15
        public bool BenchFull
        {
            get
            {
                for (int i = 0; i < Seats.Length; i++) if (Seats[i] == null || !Seats[i].IsOccupied) return false;
                return true;
            }
        }

        void UpdateBench(float dt)
        {
            _benchHold = BenchFull ? _benchHold + dt : 0f;
            if (_benchHold >= 0.5f) StartSunrise();
        }

        void StartSunrise()
        {
            Phase = HearingPhase.Sunrise; _t = 0f; _sunT = 0f;
            _sunriseStarted = true;
            BenchSignal?.Set(true);
            LogEvent("bench");
            var team = ShifterCharacter.All;
            _ctlHeld.Clear();
            for (int i = 0; i < team.Count; i++) if (team[i] != null && !team[i].ExternalControl) { team[i].ExternalControl = true; _ctlHeld.Add(team[i]); }
            if (Caught && Max != null && Catcher != null)
            {
                for (int i = 0; i < Seats.Length; i++)
                    if (Seats[i] != null && Seats[i].Occupant == Catcher && Seats[i].Seat != null)
                    {
                        Max.transform.SetParent(transform, true);
                        Max.transform.SetPositionAndRotation(Seats[i].Seat.position + Seats[i].Seat.rotation * new Vector3(0f, 0.55f, 0.3f), Seats[i].Seat.rotation);
                        Max.SetPose(PoseOverride.Seated);
                        Max.SetExpression(Expression.Sad, 4f);
                    }
            }
            GameEvents.RaiseStoryBeat("ch14.bench");
        }

        void UpdateSunrise(float dt)
        {
            _sunT += Time.unscaledDeltaTime;
            var sp = StoryPlayer.Instance;
            bool c15 = sp != null && sp.IsPlaying && sp.CurrentId == "C15";
            if (c15) _c15Seen = true;
            // fallback when the story module is absent / muted: run the set changes ourselves
            if (!_c15Seen && _sunT > 1.5f && !_balconyDone) StartCoroutine(Fallback());
        }

        IEnumerator Fallback()
        {
            _c15Seen = true;
            LogEvent("c15.fallback");
            OnBeat("c15.sign");
            yield return new WaitForSeconds(Fast ? 0.3f : 2f);
            var cm = CharacterManager.Instance;
            var pick = cm != null ? cm.Active : null;
            if (pick != null) { StoryText.President = pick.DisplayName.ToUpperInvariant(); ShowVista(pick.Archetype); }
            ShiftocracyEnding.Instance?.SpawnFoundersStatue();
            OnBeat("c15.statue");
            yield return new WaitForSeconds(Fast ? 0.3f : 2f);
            OnBeat("c15.balcony");
        }

        void OnScriptFinished(string id, bool skipped)
        {
            if (id != "C15" || !_sunriseStarted) return;
            _c15Seen = true;
            if (!_signDone) OnBeat("c15.sign");
            var end = ShiftocracyEnding.Instance;
            if (VistaShown == null)
            {
                if (end != null && end.President != null) ShowVista(end.PresidentArchetype);
                else { var cm = CharacterManager.Instance; if (cm != null && cm.Active != null) ShowVista(cm.Active.Archetype); }
            }
            if (!_statueDone) OnBeat("c15.statue");
            if (!_balconyDone) OnBeat("c15.balcony");
        }

        void OnBeat(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("c15.")) return;
            if (!_sunriseStarted) return;
            if (id == "c15.sign" && !_signDone) { _signDone = true; LogEvent("sign"); StartCoroutine(Sign()); }
            else if (id.StartsWith("c15.vista."))
            {
                string a = id.Substring(10);
                int dot = a.IndexOf('.'); if (dot >= 0) a = a.Substring(0, dot);
                if (a == "brute") ShowVista(Archetype.Brute); else if (a == "sprinter") ShowVista(Archetype.Sprinter); else if (a == "brainiac") ShowVista(Archetype.Brainiac);
            }
            else if (id == "c15.statue" && !_statueDone) { _statueDone = true; LogEvent("statue"); StartCoroutine(Statue()); }
            else if (id == "c15.balcony" && !_balconyDone) { _balconyDone = true; LogEvent("balcony"); Balcony(); }
        }

        IEnumerator Sign()
        {
            float k = Fast ? 0.2f : 1f;
            if (Neon != null)
            {
                for (int i = 0; i < 4; i++) { Neon.enabled = !Neon.enabled; Sfx.At(SfxId.Sizzle, Neon.transform.position, 0.5f); yield return new WaitForSeconds(0.12f * k); }
                Neon.enabled = true;
                Neon.text = "SHIFT CHANGE: NOW";
                Neon.color = UIColors.Success;
            }
            for (int i = 0; i < NorthGlass.Length; i++) if (NorthGlass[i] != null) NorthGlass[i].SetActive(false);
            LevelLighting.SetOverride(1.5f, 1.7f, Fast ? 0.5f : 6f);   // lift the dais too, not only the backdrop
            if (!StoryPlayer.AutoSkip && StoryPlayer.Instance == null) WorldColor.Wave(new Vector3(0f, 6f, 60f), 6f, 1.00f, 1.15f);
        }

        void ShowVista(Archetype a)
        {
            if (VistaShown == a) return;
            int i = a == Archetype.Brute ? 0 : a == Archetype.Sprinter ? 1 : 2;
            for (int k = 0; k < VistaSets.Length; k++) if (VistaSets[k] != null && k != i) VistaSets[k].SetActive(false);
            VistaShown = a;
            LogEvent("vista");
            if (i < VistaSets.Length && VistaSets[i] != null) StartCoroutine(Rise(VistaSets[i].transform, Fast ? 0.3f : 1.5f, 30f));
        }

        IEnumerator Rise(Transform t, float dur, float depth)
        {
            t.gameObject.SetActive(true);
            Vector3 home = t.localPosition, a = home - Vector3.up * depth;
            for (float u = 0f; u < dur; u += Time.deltaTime) { t.localPosition = Vector3.Lerp(a, home, Mathf.SmoothStep(0f, 1f, u / dur)); yield return null; }
            t.localPosition = home;
            Sfx.Play(SfxId.ColorBurst, 0.8f);
        }

        IEnumerator Statue()
        {
            float k = Fast ? 0.4f : 1f;
            ShiftocracyEnding.Instance?.SpawnFoundersStatue();
            if (MaxStatue != null)
            {
                Quaternion a = MaxStatue.localRotation, b = a * Quaternion.Euler(95f, 0f, 0f);
                for (float t = 0f; t < 1.2f * k; t += Time.deltaTime) { float u = t / (1.2f * k); MaxStatue.localRotation = Quaternion.Slerp(a, b, u * u); yield return null; }
                MaxStatue.localRotation = b;
                Sfx.Play(SfxId.Explosion, 0.6f);
                Sfx.Play(SfxId.CrowdGasp, 0.9f);
            }
            if (StatueRiser != null) { FixStatueText(); yield return Rise(StatueRiser, 2.0f * k, 18f); }
        }

        /// <summary>The vista copy of the founders' statue is scaled x5: StoryKit's auto-sized plaque texts wrap per
        /// letter at that scale (a column of giant dark letters in front of the statue). Fixed size, no wrap.</summary>
        void FixStatueText()
        {
            var tmps = StatueRiser.GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                var t = tmps[i]; if (t == null) continue;
                t.enableAutoSizing = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.fontSize = Mathf.Min(1.4f, Mathf.Max(0.8f, t.fontSizeMax * 0.9f));
            }
        }

        void Balcony()
        {
            Phase = HearingPhase.Done;
            LogEvent("done");
            if (Max != null && MaxSpot != null)
            {
                Max.transform.SetParent(transform, true);
                Max.transform.SetPositionAndRotation(MaxSpot.position, MaxSpot.rotation);
                Max.SetPose(PoseOverride.None);
                Max.SetHat(VillainHat.EmployeeOfTheMonth);
                Max.SetExpression(Expression.Happy, 3f);
                var badge = Kit.Make("Lanyard", Max.transform, new Vector3(0f, 0.75f, 0.2f)).transform;
                Kit.BoxVisual(badge, "Card", Vector3.zero, new Vector3(0.22f, 0.3f, 0.02f), MatKey.SignWhite, 0.01f, null, false);
                L14Builder.Text(badge, new Vector3(0f, 0f, 0.015f), Quaternion.Euler(0f, 180f, 0f), "INTERN\n#4", 0.07f, Color.black, 0.2f);
            }
            // meeting adjourned: stand everyone up AND close the bench (a seat reopened after Done was a softlock:
            // L14BenchSeat holds its sitter until Release() and the exit needs all three interns)
            for (int i = 0; i < Seats.Length; i++) if (Seats[i] != null) { Seats[i].Release(); Seats[i].Open = false; Seats[i].ClosedReason = "MEETING ADJOURNED. TO THE BALCONY."; }
            for (int i = 0; i < _ctlHeld.Count; i++) if (_ctlHeld[i] != null) _ctlHeld[i].ExternalControl = false;
            _ctlHeld.Clear();
            DoorSignal?.Set(true);
            SetObjective("TO THE BALCONY");
            ComputeHash();
        }

        // ================================================================== helpers
        void ActivateCp(int idx)
        {
            if (idx < _cps.Length && _cps[idx] != null && _cpZone[idx] != Vector3.zero) _cps[idx].ZoneSize = _cpZone[idx];
            CheckpointManager.ActivateFromScript(idx);
            LogEvent("cp" + idx);
        }

        void SetObjective(string text)
        {
            if (LevelManager.Instance != null) LevelManager.Instance.SetObjective(text);
            else GameEvents.RaiseObjective(text);
        }

        static void ShowTutorial(string id, string title, string body, bool important)
        {
            var tm = TutorialManager.Instance;
            if (tm != null && tm.Current != null && tm.Current.Important) tm.Hide();
            var m = new TutorialMessage { Id = id, Title = title, Body = body, Important = important, Duration = important ? 0f : 7f };
            if (tm != null) tm.Show(m); else GameEvents.RaiseTutorialShow(m);
        }

        public void LogEvent(string name)
        {
            // phase-relative scaled time (0.1 s) + name: the deterministic part of the timeline
            _log.Add(Phase + ":" + name + "@" + Mathf.Round(_t * 10f) / 10f);
        }

        void ComputeHash()
        {
            uint h = 2166136261;
            for (int i = 0; i < _log.Count; i++)
            {
                if (_log[i].Contains("hover") || _log[i].Contains("land.") || _log[i].Contains("bribe") || _log[i].Contains("humble") || _log[i].Contains("aye") || _log[i].Contains("init") || _log[i].Contains("landed") || _log[i].Contains("caught") || _log[i].Contains("vista")) continue;
                if (!(_log[i].StartsWith("Defense") || _log[i].StartsWith("Filibuster") || _log[i].StartsWith("Catch"))) continue;
                string s = _log[i];
                for (int k = 0; k < s.Length; k++) { h ^= s[k]; h *= 16777619; }
            }
            TimelineHash = h.ToString("x8");
            Hashes.Add(TimelineHash);
            Debug.Log("[L14] TIMELINE hash=" + TimelineHash + " events=" + _log.Count + " :: " + string.Join(" | ", _log));
        }
    }
}
