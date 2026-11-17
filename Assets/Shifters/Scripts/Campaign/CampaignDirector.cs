// =====================================================================================
//  SHIFTERS - CampaignDirector.cs   [owned by the CAMPAIGN agent]
//  Runs the story campaign "THE GREAT SHIFT" (CAMPAIGN.md §15, §18.1). Self-attaches to the
//  persistent [SHIFTERS] root (AfterSceneLoad).
//  * StartNew / StartAt / Continue / StartFreePlay / BeginChapter start chapters (chapter card
//    via GameEvents.ChapterCard, then GameSession.LoadLevel of the chapter's catalog index).
//  * Before a campaign level's Start (sceneLoaded + LevelManager.CampaignIntroHook) it sets the
//    level's IntroTitle / IntroSubtitle / ObjectiveOverride (+ music override).
//  * LevelStarted: story overlay (re-framed L1..L5), world-color base, intro script (only on a
//    fresh chapter start, never on checkpoint restores / restarts), assists bookkeeping.
//  * LevelCompleted: LostLog, exhibit, meter, saves (campaign + team), outro script.
//  * NEXT SHIFT: GameSession.NextLevelOverride -> next BUILT chapter in story order (unbuilt
//    chapters are skipped), campaign end after CH14 (placeholder end if the path runs out).
//  Free Play: no story, full color, NEXT = next built chapter, Victory after the last.
//  IsCampaign is false in Free Play / OVERTIME / classic flow / main menu.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public sealed class CampaignDirector : MonoBehaviour
    {
        public static CampaignDirector Instance { get; private set; }
        /// <summary>Tests/bots: no story, no PiP holds, auto-pick in C15 (reset at SubsystemRegistration).</summary>
        public static bool BotMode;
        /// <summary>Apply StoryOverlay on re-framed levels (G2 regression runs set false).</summary>
        public static bool OverlaysEnabled = true;
        /// <summary>Seconds between TEAMWORK VIOLATION toasts (Acts I-II only).</summary>
        public const float ViolationToastCooldown = 15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; BotMode = false; OverlaysEnabled = true; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            var root = GameBootstrap.Root;
            if (root == null) return;
            try
            {
                if (root.GetComponent<CampaignDirector>() == null) root.AddComponent<CampaignDirector>();
                if (root.GetComponent<WorldColorDriver>() == null) root.AddComponent<WorldColorDriver>();
            }
            catch (Exception e) { Debug.LogError("[CampaignDirector] attach failed: " + e); }
        }

        // ================================================================ state
        public CampaignMode Mode { get; private set; }
        public bool IsCampaign => Mode == CampaignMode.Campaign;
        public bool IsFreePlay => Mode == CampaignMode.FreePlay;
        public ChapterDef Current { get; private set; }
        /// <summary>World-color meter of the running campaign chapter (§6.1); after a completion = the chapter's MeterEnd.</summary>
        public float Meter { get; private set; }
        public bool HasSave => _data != null && _data.HasChapter;
        /// <summary>The chapter CONTINUE resumes at (null if none): "SHIFT 07 · THE FEDERAL COLOR RESERVE" = SavedChapter.CardTitle.</summary>
        public ChapterDef SavedChapter => HasSave ? CampaignCatalog.Find(_data.chapter) : null;
        public float SavedMeter => _data != null ? _data.meter : 0f;
        /// <summary>The campaign has been completed at least once (menu rainbow trim "THE WORLD REMEMBERS").</summary>
        public bool CampaignCompleted => _data != null && _data.completed;
        /// <summary>Read-only view for the UI (HR FILE, SHIFT SCHEDULE). Do not modify.</summary>
        public CampaignSaveData SaveData => _data;
        public bool SaveWasDiscarded { get; private set; }

        public IReadOnlyList<LostRecord> LostLog => _lost;
        public int TeamworkViolations => _data != null ? _data.violations : 0;
        public int HRInterventions => _data != null ? _data.hr : 0;
        public float CampaignTime => _data != null ? _data.time : 0f;
        /// <summary>HR FILE "MOST THROWN": the intern thrown most this campaign (null if nobody was thrown).</summary>
        public string MostThrown
        {
            get
            {
                if (_data == null || _data.thrownNames == null || _data.thrownCounts == null) return null;
                int best = -1; string name = null;
                for (int i = 0; i < _data.thrownNames.Length && i < _data.thrownCounts.Length; i++)
                    if (_data.thrownCounts[i] > best && !string.IsNullOrEmpty(_data.thrownNames[i])) { best = _data.thrownCounts[i]; name = _data.thrownNames[i]; }
                return name != null ? name.ToUpperInvariant() + " (" + best + "x)" : null;
            }
        }
        /// <summary>HR FILE "FIRST PRESIDENT": set by CH14 / the epilogue (null = the UI picks one).</summary>
        public string President
        {
            get => _data != null && !string.IsNullOrEmpty(_data.president) ? _data.president : null;
            set { if (_data == null) return; _data.president = value ?? ""; Save(); }
        }
        public bool IsChapterDone(string id) => _data != null && _data.IsDone(id);
        public bool HasExhibit(string id) => _data != null && _data.HasExhibit(id);
        public bool HasSeen(string scriptId) => _data != null && _data.HasSeen(scriptId);

        /// <summary>Meter before/after the last campaign completion (Level Complete "WORLD COLOR" delta).</summary>
        public float LastMeterFrom { get; private set; }
        public float LastMeterTo { get; private set; }
        /// <summary>The exhibit awarded by the last completion (null if none).</summary>
        public string LastExhibit { get; private set; }

        /// <summary>Set when the campaign ended (after CH14 = complete, or the built path ran out = placeholder end
        /// "MAX REMAINS IN POWER... FOR NOW."). The main menu shows the campaign end screen + credits and clears it.</summary>
        public bool PendingCampaignEnd { get; private set; }
        public bool CampaignEndIsPlaceholder { get; private set; }
        public const string PlaceholderEndText = "MAX REMAINS IN POWER... FOR NOW.";

        public event Action<ChapterDef> ChapterStarted;
        public event Action<ChapterDef, LevelResult> ChapterCompleted;
        /// <summary>true = THE GREAT SHIFT · COMPLETE, false = placeholder end.</summary>
        public event Action<bool> CampaignEnded;
        public event Action<int> ViolationCommitted;

        CampaignSaveData _data = new CampaignSaveData();
        readonly List<LostRecord> _lost = new List<LostRecord>();
        readonly HashSet<string> _playedThisChapter = new HashSet<string>();
        readonly Dictionary<string, string> _lostCause = new Dictionary<string, string>();
        bool _pendingStart;        // the chapter's first LevelStarted is still to come
        bool _introDone;           // intro played (or skipped) for this chapter start
        ChapterDef _completedFor;  // completion already handled for this chapter start
        float _lastViolationToast = -999f;
        bool _hooksInstalled;
        Func<bool> _nextHandler;
        Action<LevelManager> _introHandler;
        float _hookCheck;

        // ================================================================ lifecycle
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _nextHandler = RouteNext;
            _introHandler = ApplyIntro;
            LoadSave();
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.CharacterStateChanged += OnStateChanged;
            GameEvents.CharacterConditionChanged += OnConditionChanged;
            GameEvents.StoryBeat += OnStoryBeat;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.TeamWiped += OnTeamWiped;
            GameEvents.BossChanged += OnBossChanged;
            InstallHooks();
        }

        void Start()
        {
            if (SaveWasDiscarded) GameEvents.RaiseToast(CampaignSave.LostFileToast, UIColors.AccentYellow);
            var s = SceneManager.GetActiveScene();
            if (s.name == LevelCatalog.MainMenuScene) EnterMenu();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.CharacterStateChanged -= OnStateChanged;
            GameEvents.CharacterConditionChanged -= OnConditionChanged;
            GameEvents.StoryBeat -= OnStoryBeat;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.TeamWiped -= OnTeamWiped;
            GameEvents.BossChanged -= OnBossChanged;
            if (_hooksInstalled)
            {
                if (CampaignDeps.GetNextLevelOverride() == _nextHandler) CampaignDeps.SetNextLevelOverride(null);
                if (CampaignDeps.GetIntroHook() == _introHandler) CampaignDeps.SetIntroHook(null);
            }
            if (Instance == this) Instance = null;
        }

        void InstallHooks()
        {
            bool a = CampaignDeps.SetNextLevelOverride(_nextHandler);
            bool b = CampaignDeps.SetIntroHook(_introHandler);
            _hooksInstalled = a || b;
            if (!a) Debug.Log("[CampaignDirector] GameSession.NextLevelOverride not available yet - NEXT SHIFT uses the classic flow.");
        }

        void Update()
        {
            // re-install if someone (a test, a static reset) cleared the seams
            if (Time.unscaledTime >= _hookCheck)
            {
                _hookCheck = Time.unscaledTime + 1f;
                if (CampaignDeps.GetNextLevelOverride() == null) CampaignDeps.SetNextLevelOverride(_nextHandler);
                if (CampaignDeps.GetIntroHook() == null) CampaignDeps.SetIntroHook(_introHandler);
            }
            var gs = GameSession.Instance;
            if (gs == null || gs.LevelIndex < 0 || gs.IsLoading || TimeController.IsPaused || ShiftersInput.Context == InputContext.Menu) return;
            float dt = Mathf.Min(TimeController.RealDeltaTime, 0.25f);
            var lm = LevelManager.Instance;
            if (lm != null && lm.IsStarted && !lm.IsCompleted) CampaignAssists.Tick(dt);
            if (IsCampaign && _data != null) _data.time += dt;
        }

        // ================================================================ save
        void LoadSave()
        {
            _data = CampaignSave.Load(CampaignSave.Store, out bool discarded) ?? new CampaignSaveData();
            SaveWasDiscarded = discarded;
            _lost.Clear();
            foreach (var e in _data.lost)
                if (e != null) _lost.Add(new LostRecord(e.name, e.cause, e.chapter, e.clone));
        }

        void Save() => CampaignSave.Save(CampaignSave.Store, _data);

        /// <summary>Tests: swap the store (e.g. a MemoryStore) and reload from it.</summary>
        public void UseStore(ICampaignStore store)
        {
            CampaignSave.Store = store;
            LoadSave();
            Mode = CampaignMode.None; Current = null; Meter = 0f; PendingCampaignEnd = false; CampaignEndIsPlaceholder = false;
            _pendingStart = false; _introDone = false; _completedFor = null; _playedThisChapter.Clear(); _lostCause.Clear();
        }

        /// <summary>Re-read the save from the store (e.g. after another tab wrote it).</summary>
        public void ReloadSave() => LoadSave();

        /// <summary>Delete the campaign save (keeps the "completed once" flag).</summary>
        public void DeleteSave()
        {
            bool done = CampaignCompleted;
            CampaignSave.Delete();
            _data = new CampaignSaveData { completed = done };
            _lost.Clear();
            if (done) Save();
        }

        // ================================================================ starting chapters
        /// <summary>New campaign. seed &gt; 0: NewRun(seed) (TEAM CODE); seed == 0: NewRun with a random team;
        /// seed &lt; 0 (default): keep the session's current team (the one YOUR NEW HIRES just rolled), NewRun if none.
        /// Resets campaign progress (keeps the "completed once" flag) and begins CH01.</summary>
        public void StartNew(int seed = -1)
        {
            var gs = GameSession.Instance;
            if (gs != null)
            {
                if (seed > 0) { if (!gs.HasRun || gs.Seed != seed) gs.NewRun(seed); }   // same seed = same team: no reroll
                else if (seed == 0 || !gs.HasRun) gs.NewRun(0);
            }
            bool everCompleted = CampaignCompleted;
            _data = new CampaignSaveData { completed = everCompleted, lastSeed = gs != null ? gs.Seed : 0 };
            _lost.Clear();
            PendingCampaignEnd = false;
            BeginChapter(CampaignCatalog.First, null, false);
        }

        /// <summary>SHIFT SCHEDULE: start (or replay) a chapter with the saved team; the campaign continues from there.
        /// Meter = the chapter's table value; exhibits of earlier chapters are granted silently.</summary>
        public void StartAt(string chapterId)
        {
            var c = CampaignCatalog.Find(chapterId);
            if (c == null) { Debug.LogWarning("[CampaignDirector] StartAt: unknown chapter '" + chapterId + "'"); return; }
            if (!c.IsMain) { StartFreePlay(c.CatalogIndex); return; }
            if (_data == null) _data = new CampaignSaveData();
            for (int i = 0; i < c.Order; i++)
            {
                var e = CampaignCatalog.Chapters[i].ExhibitId;
                if (!string.IsNullOrEmpty(e)) _data.exhibits = CampaignSave.Add(_data.exhibits, e);
            }
            PendingCampaignEnd = false;
            BeginChapter(c, null, true);
        }

        /// <summary>CONTINUE: loads the start of the saved chapter (no cross-session checkpoint resume).</summary>
        public bool Continue()
        {
            if (!HasSave) return false;
            var c = CampaignCatalog.Find(_data.chapter);
            if (c == null) return false;
            PendingCampaignEnd = false;
            BeginChapter(c, _data.meter, true);
            return true;
        }

        /// <summary>FREE PLAY / OVERTIME: no story, full color; NEXT = next built chapter in story order.</summary>
        public void StartFreePlay(int catalogIndex)
        {
            Mode = CampaignMode.FreePlay;
            Current = CampaignCatalog.ForCatalogIndex(catalogIndex);
            PendingCampaignEnd = false;
            _pendingStart = false;
            WorldColorDriver.Instance?.Apply();
            LoadChapterLevel(catalogIndex, true);
        }

        /// <summary>Begin a campaign chapter by id (unbuilt chapters forward to the next built one).</summary>
        public void BeginChapter(string chapterId) => BeginChapter(CampaignCatalog.Find(chapterId), null, true);

        void BeginChapter(ChapterDef def, float? meter, bool preferSavedTeam)
        {
            var c = FirstPlayableFrom(def);
            if (c == null)
            {
                Debug.LogWarning("[CampaignDirector] no built chapter from " + (def != null ? def.Id : "null") + " on - campaign end.");
                EndCampaign(IsChapterDone(CampaignCatalog.Last.Id));
                return;
            }
            Mode = CampaignMode.Campaign;
            Current = c;
            Meter = c == def && meter.HasValue ? meter.Value : c.MeterStart;
            _pendingStart = true;
            _introDone = false;
            _completedFor = null;
            _playedThisChapter.Clear();
            _lostCause.Clear();
            if (_data == null) _data = new CampaignSaveData();
            _data.chapter = c.Id;
            _data.meter = Meter;
            var gs = GameSession.Instance;
            if (gs != null && gs.HasRun) _data.lastSeed = gs.Seed;
            Save();
            GameEvents.RaiseChapterCard(c.ActLabel, c.CardTitle);
            LoadChapterLevel(c.CatalogIndex, preferSavedTeam);
        }

        void LoadChapterLevel(int index, bool preferSavedTeam)
        {
            if (CampaignDeps.LoadOverride != null && CampaignDeps.LoadOverride(index)) return;
            var gs = GameSession.Instance;
            if (gs == null) return;
            if (!gs.HasRun && preferSavedTeam)
            {
                var run = SessionPrefs.LoadRun();
                if (run != null && run.Team != null)
                {
                    // the team lives in "shifters_run": point it at this chapter and resume it (loads the level)
                    SessionPrefs.SaveRun(run.Team, index, run.Stats);
                    if (gs.ContinueRun()) return;
                }
            }
            gs.LoadLevel(index);
        }

        // ================================================================ routing
        public static bool IsPlayable(ChapterDef c) => c != null && CampaignDeps.SceneAvailable(c.CatalogIndex);

        /// <summary>c itself if its scene is built, else the next built chapter in story order (null if none).</summary>
        public static ChapterDef FirstPlayableFrom(ChapterDef c)
        {
            while (c != null && !IsPlayable(c)) c = CampaignCatalog.Next(c);
            return c;
        }

        /// <summary>Next BUILT chapter after c in story order (main path or OVERTIME chain), null if none.</summary>
        public static ChapterDef NextPlayable(ChapterDef c) => FirstPlayableFrom(CampaignCatalog.Next(c));

        /// <summary>GameSession.NextLevelOverride handler (NEXT SHIFT). Public for bots/tests. True = handled.</summary>
        public bool RouteNext()
        {
            if (Mode == CampaignMode.None || Current == null) return false;
            var gs = GameSession.Instance;
            if (IsCampaign)
            {
                var next = NextPlayable(Current);
                if (next != null) { BeginChapter(next, null, true); return true; }
                EndCampaign(IsChapterDone(CampaignCatalog.Last.Id));
                return true;
            }
            // Free Play
            var n = NextPlayable(Current);
            if (n != null) { StartFreePlay(n.CatalogIndex); return true; }
            bool mainPath = Current.IsMain;
            Mode = CampaignMode.None;
            Current = null;
            if (gs != null) gs.ShowVictory = mainPath;
            CampaignDeps.GoToMainMenu();
            return true;
        }

        void EndCampaign(bool complete)
        {
            PendingCampaignEnd = true;
            CampaignEndIsPlaceholder = !complete;
            Mode = CampaignMode.None;
            var gs = GameSession.Instance;
            if (gs != null) gs.ShowVictory = false;
            try { CampaignEnded?.Invoke(complete); } catch (Exception e) { Debug.LogException(e); }
            CampaignDeps.RequestCampaignEnd(!complete);
            CampaignDeps.GoToMainMenu();
        }

        /// <summary>Main menu: returns PendingCampaignEnd and clears it (show the campaign end screen + credits, then
        /// CampaignCatalog.PostCreditsScript when complete).</summary>
        public bool ConsumeCampaignEnd() { bool p = PendingCampaignEnd; PendingCampaignEnd = false; return p; }

        /// <summary>Credits handoff: plays C16 (post-credits) unless BotMode; onDone always runs.</summary>
        public void PlayPostCredits(Action onDone = null)
        {
            if (BotMode) { onDone?.Invoke(); return; }
            PlayStory(CampaignCatalog.PostCreditsScript, onDone);
        }

        // ================================================================ scene / level events
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            if (scene.name == LevelCatalog.MainMenuScene) { EnterMenu(); return; }
            var lm = LevelManager.Instance;
            if (lm != null && lm.gameObject.scene == scene) ApplyIntro(lm);
        }

        void EnterMenu()
        {
            if (Mode != CampaignMode.None) { Mode = CampaignMode.None; Current = null; }
            _pendingStart = false;
            WorldColorDriver.Instance?.Apply();
        }

        /// <summary>LevelManager.CampaignIntroHook (+ sceneLoaded fallback): runs before the level's Start banner.</summary>
        void ApplyIntro(LevelManager lm)
        {
            if (lm == null || !CheckLevel(lm.LevelIndex)) return;
            if (!IsCampaign) return;
            lm.IntroTitle = Current.Title;
            lm.IntroSubtitle = Current.BannerSubtitle;
            lm.ObjectiveOverride = Current.Objective;
            if (Current.MusicOverride != MusicId.None && lm.PlayMusic)
            {
                lm.PlayMusic = false;
                AudioManager.Instance?.PlayMusic(Current.MusicOverride);
            }
        }

        /// <summary>A level loaded: is it the one we are running? A different level ends campaign/free-play mode
        /// (e.g. classic LEVEL SELECT); in Free Play an unexpected chapter level just becomes Current.</summary>
        bool CheckLevel(int levelIndex)
        {
            if (Mode == CampaignMode.None) return false;
            if (Current != null && Current.CatalogIndex == levelIndex) return true;
            if (IsFreePlay) { Current = CampaignCatalog.ForCatalogIndex(levelIndex); return Current != null; }
            Debug.Log("[CampaignDirector] level " + levelIndex + " is not chapter " + (Current != null ? Current.Id : "-") + " - leaving campaign mode.");
            Mode = CampaignMode.None; Current = null; _pendingStart = false;
            return false;
        }

        void OnLevelStarted()
        {
            var lm = LevelManager.Instance;
            var gs = GameSession.Instance;
            int idx = lm != null ? lm.LevelIndex : gs != null ? gs.LevelIndex : -1;
            bool fromCp = lm != null && lm.StartedFromCheckpoint;
            HandleLevelStarted(idx, fromCp, gs != null ? gs.CheckpointIndex : -1);
        }

        /// <summary>LevelStarted logic (public for tests/bots that simulate a level start without a scene).</summary>
        public void HandleLevelStarted(int levelIndex, bool fromCheckpoint, int checkpointIndex = -1)
        {
            CampaignAssists.NoteLevelStarted(levelIndex, fromCheckpoint, checkpointIndex);
            bool ours = CheckLevel(levelIndex);
            if (!fromCheckpoint) CampaignDeps.ClearWorldColorBursts();
            WorldColorDriver.Instance?.Apply();
            if (!ours || !IsCampaign) return;
            if (OverlaysEnabled && !string.IsNullOrEmpty(Current.OverlayId)) CampaignDeps.ApplyOverlay(Current.OverlayId);
            if (_pendingStart)
            {
                _pendingStart = false;
                try { ChapterStarted?.Invoke(Current); } catch (Exception e) { Debug.LogException(e); }
            }
            if (!_introDone)
            {
                _introDone = true;
                // fresh chapter start only: never on checkpoint restores (a restore also means the intro already ran)
                if (!fromCheckpoint && !string.IsNullOrEmpty(Current.IntroScript)) PlayStory(Current.IntroScript, null);
            }
        }

        void OnLevelCompleted(LevelResult r) => HandleLevelCompleted(r);

        /// <summary>LevelCompleted logic (public for tests/bots).</summary>
        public void HandleLevelCompleted(LevelResult r)
        {
            if (!IsCampaign || Current == null || r.LevelIndex != Current.CatalogIndex) return;
            if (_completedFor == Current) return;
            var c = Current;
            _completedFor = c;
            var gs = GameSession.Instance;

            // ---- Lost log (left-behind interns and their reprints)
            var lb = r.LeftBehind ?? new string[0];
            var rc = gs != null && gs.LastRecloned != null ? gs.LastRecloned : new string[0];
            for (int i = 0; i < lb.Length; i++)
            {
                if (string.IsNullOrEmpty(lb[i])) continue;
                string cause = _lostCause.TryGetValue(lb[i], out var cs) && !string.IsNullOrEmpty(cs) ? cs : "left behind";
                var rec = new LostRecord(lb[i], cause, c.Id, i < rc.Length ? rc[i] : "");
                _lost.Add(rec);
                _data.lost = CampaignSave.Add(_data.lost, new CampaignLostEntry { name = rec.Name, cause = rec.Cause, chapter = rec.ChapterId, clone = rec.CloneName });
            }
            if (_lost.Count > CampaignSave.MaxLost) _lost.RemoveRange(0, _lost.Count - CampaignSave.MaxLost);

            // ---- progress
            _data.done = CampaignSave.Add(_data.done, c.Id);
            LastExhibit = null;
            if (!string.IsNullOrEmpty(c.ExhibitId) && !HasExhibit(c.ExhibitId)) { AwardExhibit(c.ExhibitId); LastExhibit = c.ExhibitId; }
            LastMeterFrom = Meter;
            Meter = c.MeterEnd;
            LastMeterTo = Meter;
            var next = NextPlayable(c);
            bool finale = c == CampaignCatalog.Last;
            if (finale) { _data.completed = true; _data.chapter = ""; }
            else if (next != null) _data.chapter = next.Id;
            else _data.chapter = "";
            _data.meter = next != null && !finale ? next.MeterStart : Meter;
            Save();
            if (gs != null && gs.Team != null && next != null && !finale) SessionPrefs.SaveRun(gs.Team, next.CatalogIndex, gs.Stats);
            WorldColorDriver.Instance?.Apply();

            // ---- outro (skipped when the level already played it through its beat)
            bool outro = !string.IsNullOrEmpty(c.OutroScript) && !_playedThisChapter.Contains(c.OutroScript);
            if (outro) PlayStory(c.OutroScript, null);
            // let the WORLD COLOR delta / exhibit stamp / outro breathe before Level Complete (UI caps at 3 s)
            if (!BotMode && (outro || LastExhibit != null)) CampaignDeps.HoldLevelComplete(outro ? 2.5f : 1.2f);
            try { ChapterCompleted?.Invoke(c, r); } catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Bots/tests: complete the current chapter like a LevelExit would (GameSession.CompleteLevel +
        /// GameEvents.LevelCompleted). leftBehind = display names. False if no chapter is running.</summary>
        public bool ForceCompleteCurrent(string[] leftBehind = null, float time = 1f)
        {
            if (Current == null) return false;
            var lb = leftBehind ?? new string[0];
            var r = new LevelResult { LevelIndex = Current.CatalogIndex, Time = time, Survivors = Mathf.Max(0, 3 - lb.Length), LeftBehind = lb };
            var lm = LevelManager.Instance;
            if (lm != null && lm.LevelIndex == Current.CatalogIndex) lm.MarkCompleted();
            GameSession.Instance?.CompleteLevel(r);
            GameEvents.RaiseLevelCompleted(r);
            return true;
        }

        // ================================================================ exhibits / story
        public void AwardExhibit(string id)
        {
            if (string.IsNullOrEmpty(id) || HasExhibit(id)) return;
            _data.exhibits = CampaignSave.Add(_data.exhibits, id);
            GameEvents.RaiseExhibitAcquired(id);
        }

        public void MarkSeen(string scriptId)
        {
            if (string.IsNullOrEmpty(scriptId) || _data == null) return;
            _data.seen = CampaignSave.Add(_data.seen, scriptId);
        }

        bool PlayStory(string id, Action onDone)
        {
            _playedThisChapter.Add(id);
            MarkSeen(id);
            if (BotMode) { onDone?.Invoke(); return false; }
            return CampaignDeps.PlayScript(id, onDone);
        }

        void OnStoryBeat(string beat)
        {
            if (string.IsNullOrEmpty(beat)) return;
            string script = CampaignCatalog.ScriptForBeat(beat);
            if (script == null && beat.Length == 3 && beat[0] == 'C' && char.IsDigit(beat[1]) && char.IsDigit(beat[2])) script = beat;
            if (script == null) return;
            _playedThisChapter.Add(script);
            MarkSeen(script);
        }

        // ================================================================ lost log / violations / assists
        static bool LevelRunning { get { var lm = LevelManager.Instance; return lm == null || (lm.IsStarted && !lm.IsCompleted); } }

        void OnStateChanged(ShifterCharacter c)
        {
            if (c == null || !IsCampaign) return;
            if (c.State == CharacterState.Lost)
                _lostCause[c.DisplayName] = string.IsNullOrEmpty(c.LastCause) ? "lost" : c.LastCause;
            else if (c.State == CharacterState.Alive && !c.IsRestoringState && LevelRunning &&
                     (c.PreviousState == CharacterState.Downed || c.PreviousState == CharacterState.Captured))
                AddViolation();   // rescue / revive
        }

        void OnConditionChanged(ShifterCharacter c)
        {
            if (c == null || !IsCampaign || c.IsRestoringState || !LevelRunning) return;
            var k = c.Condition;
            if (k == CharacterCondition.Thrown) NoteThrown(c.DisplayName);
            if (k == CharacterCondition.Carried || k == CharacterCondition.Thrown || k == CharacterCondition.Bridging) AddViolation();
        }

        /// <summary>+1 TEAMWORK VIOLATION (campaign only). Public for level scripts (stack &gt;= 1 s, all-three plate).</summary>
        public void AddViolation()
        {
            if (!IsCampaign || _data == null) return;
            _data.violations++;
            var gs = GameSession.Instance;
            if (gs != null && gs.Stats != null) gs.Stats.TeamworkViolations++;
            if (Current != null && (Current.Act == "I" || Current.Act == "II") && Time.unscaledTime - _lastViolationToast >= ViolationToastCooldown)
            {
                _lastViolationToast = Time.unscaledTime;
                GameEvents.RaiseToast("TEAMWORK VIOLATION #" + _data.violations, UIColors.AccentYellow);
            }
            try { ViolationCommitted?.Invoke(_data.violations); } catch (Exception e) { Debug.LogException(e); }
        }

        void NoteThrown(string name)
        {
            if (string.IsNullOrEmpty(name) || _data == null) return;
            int i = Array.IndexOf(_data.thrownNames, name);
            if (i < 0)
            {
                _data.thrownNames = CampaignSave.Add(_data.thrownNames, name);
                i = Array.IndexOf(_data.thrownNames, name);
                var nc = new int[_data.thrownNames.Length];
                Array.Copy(_data.thrownCounts, nc, Mathf.Min(_data.thrownCounts.Length, nc.Length));
                _data.thrownCounts = nc;
            }
            if (i >= 0 && i < _data.thrownCounts.Length) _data.thrownCounts[i]++;
        }

        internal void NoteHRUsed() { if (_data != null) _data.hr++; }

        void OnCheckpoint(int index) => CampaignAssists.NoteCheckpoint(index);
        void OnTeamWiped() => CampaignAssists.NoteWipe();
        void OnBossChanged(BossStatus s) => CampaignAssists.NoteBoss(s);
    }
}
