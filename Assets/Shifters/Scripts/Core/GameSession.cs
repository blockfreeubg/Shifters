// =====================================================================================
//  SHIFTERS - GameSession.cs   [CONTRACT API - implementation owned by the CORE agent]
//  Persistent (DontDestroyOnLoad) run state: team, seed, level progress, checkpoint snapshot,
//  settings (PlayerPrefs), stats. Created by GameBootstrap before the first scene loads.
//
//  Scene flow:
//    LoadLevel(i): reset time + Snapshot, Context=Cutscene, ScreenFader.FadeOut(0.35, title),
//      async load LevelCatalog.Get(i).SceneName. The level's LevelManager then calls
//      NotifyLevelLoaded(i) (sets LevelIndex, Context=Gameplay, saves run progress) and
//      ScreenFader.FadeIn. If nobody notifies within 2 s (unscaled) GameSession does both itself.
//    RestartLevel / RestartFromCheckpoint: reload the current level (Snapshot cleared / kept).
//    CompleteLevel(result): records result, unlocks, re-clones LeftBehind members (LastRecloned).
//    NextLevel: next index, or after the last level ShowVictory=true + GoToMainMenu().
//    Pressing Play inside a level scene works: the scene name is detected on load (and
//    LevelManager's NotifyLevelLoaded confirms it); EnsureTeam() rolls a random team.
//  PlayerPrefs keys: see Core/Session/SessionPrefs.cs.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; _seedSalt = 0; NextLevelOverride = null; SkipClassicRunSave = false; }

        /// <summary>Campaign seam: consulted first by NextLevel (NEXT SHIFT / auto-advance). Return true = handled (NextLevel
        /// does nothing else), false = classic flow (L1..L5 chain, then victory / menu). Null = classic flow. Exceptions = classic.</summary>
        public static Func<bool> NextLevelOverride;
        /// <summary>Campaign seam: true = CompleteLevel neither saves nor deletes the classic SavedRun ("shifters_run")
        /// (the campaign keeps its own save). Completion bits and best times are still recorded. Default false.</summary>
        public static bool SkipClassicRunSave;

        public const float FadeOutSeconds = 0.35f;
        public const float FadeInSeconds = 0.35f;
        /// <summary>Unscaled seconds GameSession waits for NotifyLevelLoaded before fading in by itself.</summary>
        public const float FadeInTimeout = 2f;
        const float FaderTimeout = 4f;

        public TeamData Team { get; protected set; }
        public int Seed => Team != null ? Team.Seed : 0;
        public bool HasRun => Team != null;
        public int LevelIndex { get; protected set; } = -1;          // -1 = main menu
        public int CheckpointIndex => Snapshot != null ? Snapshot.CheckpointIndex : -1;
        public CheckpointSnapshot Snapshot { get; protected set; }   // null = start of level
        public RunStats Stats { get; protected set; } = new RunStats();
        public SettingsData Settings { get; protected set; } = new SettingsData();
        /// <summary>Time.time when the current level attempt started (reset on every (re)load).</summary>
        public float LevelStartTime { get; protected set; }

        // ---------------------------------------------------------------- additions (CORE_SESSION)
        /// <summary>Catalog entry of the current level (null in the menu / unknown scenes).</summary>
        public LevelInfo CurrentLevel => LevelCatalog.Get(LevelIndex);
        /// <summary>True from LoadLevel/Restart*/GoToMainMenu until the new scene finished loading.</summary>
        public bool IsLoading { get; private set; }
        /// <summary>Set by NextLevel after the final level; the main menu shows the victory screen first. UI clears it (or ConsumeVictory()).</summary>
        public bool ShowVictory { get; set; }
        /// <summary>Last CompleteLevel result (valid when HasLastResult).</summary>
        public LevelResult LastResult { get; protected set; }
        public bool HasLastResult { get; protected set; }
        /// <summary>True if the last CompleteLevel beat the stored best time.</summary>
        public bool LastResultWasRecord { get; protected set; }
        /// <summary>Display names of the members re-cloned by the last CompleteLevel ("Gary II").</summary>
        public string[] LastRecloned { get; protected set; } = new string[0];
        /// <summary>Unscaled gameplay seconds spent in the current level (pause/menus excluded, accumulates across restarts).</summary>
        public float LevelElapsed { get; protected set; }
        /// <summary>Restarts (level + checkpoint) of the current level.</summary>
        public int LevelRestarts { get; protected set; }
        /// <summary>Level index "Continue" resumes at (-1 when the run is finished).</summary>
        public int ResumeLevelIndex { get; protected set; }
        /// <summary>Last session error (missing scene...), also logged with Debug.LogError.</summary>
        public string LastError { get; protected set; }
        /// <summary>A saved run exists in PlayerPrefs (main menu CONTINUE button).</summary>
        public bool HasSavedRun => SessionPrefs.LoadRun() != null;

        static int _seedSalt;
        int _fullscreenApplied = -1;   // -1 = settings never applied (boot), else the Fullscreen value last applied / seen
        bool _pageFullscreen;          // WebGL: Screen.fullScreen last frame (page fullscreen changes are mirrored)
        bool _levelNotified;
        bool _pendingFreshLevel;
        int _attemptSerial;
        int _completedSerial = -1;
        int _routineSerial;

        // ================================================================ lifecycle
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Settings = SessionPrefs.LoadSettings();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected virtual void Start()
        {
            if (!IsLoading) DetectScene(SceneManager.GetActiveScene());
        }

        protected virtual void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        protected virtual void Update()
        {
            ShiftersInput.UpdateCursorLock();   // lock loss => pause, click => relock (WebGL), even in frames nobody polls input
            TrackPageFullscreen();
            if (LevelIndex < 0 || IsLoading || TimeController.IsPaused || ShiftersInput.Context == InputContext.Menu) return;
            float dt = Mathf.Min(TimeController.RealDeltaTime, 0.25f);
            Stats.PlayTime += dt;
            LevelElapsed += dt;
        }

        protected virtual void OnApplicationQuit()
        {
            SaveRunProgress();
        }

        // ================================================================ run
        /// <summary>Start a brand new run with a fresh random team (seed &lt;= 0 => random seed).</summary>
        public virtual void NewRun(int seed = 0)
        {
            if (seed <= 0) seed = RandomSeed(Team != null ? Team.Seed : 0);
            Team = CharacterGenerator.GenerateTeam(seed);
            Stats = new RunStats();
            Snapshot = null;
            HasLastResult = false;
            LastResult = default;
            LastResultWasRecord = false;
            LastRecloned = new string[0];
            ShowVictory = false;
            ResumeLevelIndex = 0;
        }

        /// <summary>Make sure a team exists (pressing Play directly inside a level scene in the Editor).</summary>
        public virtual void EnsureTeam() { if (Team == null) NewRun(); }

        /// <summary>Random 6-digit seed (never equal to <paramref name="avoid"/>).</summary>
        public static int RandomSeed(int avoid = 0)
        {
            var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks ^ (++_seedSalt * 0x5bd1e995) ^ Environment.TickCount));
            int s;
            do s = rng.Next(100000, 1000000); while (s == avoid);
            return s;
        }

        /// <summary>Restore the saved run from PlayerPrefs and load its level. Returns false if there is none.</summary>
        public virtual bool ContinueRun()
        {
            var run = SessionPrefs.LoadRun();
            if (run == null) { Debug.LogWarning("[GameSession] ContinueRun: no saved run."); return false; }
            Team = run.Team;
            Stats = run.Stats;
            Snapshot = null;
            HasLastResult = false;
            LastRecloned = new string[0];
            ShowVictory = false;
            ResumeLevelIndex = run.LevelIndex;
            LoadLevel(run.LevelIndex);
            return true;
        }

        /// <summary>The saved run (team, level, stats) without loading it, or null. For menu previews.</summary>
        public SavedRun PeekSavedRun() => SessionPrefs.LoadRun();

        /// <summary>Forget the saved run (e.g. "New Run" confirmation).</summary>
        public void DeleteSavedRun() => SessionPrefs.DeleteRun();

        /// <summary>Write team + resume level + stats to PlayerPrefs (no-op without a team or after the final level).</summary>
        public void SaveRunProgress()
        {
            if (Team == null || ResumeLevelIndex < 0) return;
            SessionPrefs.SaveRun(Team, ResumeLevelIndex, Stats);
        }

        // ================================================================ scene flow
        public virtual void LoadLevel(int index)
        {
            var info = LevelCatalog.Get(index);
            if (info == null) { Fail("LoadLevel(" + index + "): no such level (valid 0.." + (LevelCatalog.Count - 1) + ")."); return; }
            if (IsLoading) { Debug.LogWarning("[GameSession] LoadLevel(" + index + ") ignored: a scene load is already running."); return; }
            if (!SceneLoader.CanLoad(info.SceneName))
            {
                Fail("LoadLevel(" + index + ") \"" + info.Title + "\": scene '" + info.SceneName + "' is not in Build Settings (expected at " +
                     LevelCatalog.ScenePath(info.SceneName) + "). Build the level scenes first.");
                return;
            }
            EnsureTeam();
            Snapshot = null;
            ShowVictory = false;
            _pendingFreshLevel = true;
            StartCoroutine(LoadRoutine(info.SceneName, info.Title, index));
        }

        public virtual void RestartLevel()
        {
            if (!CanRestart("RestartLevel")) return;
            Stats.Restarts++;
            LevelRestarts++;
            Snapshot = null;
            Reload();
        }

        /// <summary>Reload the current level keeping the checkpoint Snapshot (start of level if none).</summary>
        public virtual void RestartFromCheckpoint()
        {
            if (!CanRestart("RestartFromCheckpoint")) return;
            Stats.Restarts++;
            LevelRestarts++;
            Reload();
        }

        /// <summary>"LET HR HANDLE IT" (Pause screen / assists): reloads the level at the NEXT checkpoint after the current
        /// one (lowest Checkpoint.Index above CheckpointIndex in the loaded scene), with every intern that is not Lost alive
        /// at full health (HR frees the Captured and revives the Downed), the same active slot and gun owner.
        /// Counts Stats.HRInterventions and a restart. Returns false (and does nothing) when there is no next checkpoint,
        /// no level is running, or a load is in progress.</summary>
        public virtual bool RestartAtNextCheckpoint()
        {
            if (!CanRestart("RestartAtNextCheckpoint")) return false;
            int cur = CheckpointIndex;
            var lm = LevelManager.Instance;
            if (lm != null && lm.IsCompleted) return false;
            int next = int.MaxValue;
            var all = Checkpoint.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Index > cur && all[i].Index < next) next = all[i].Index;
            if (next == int.MaxValue) { Debug.Log("[GameSession] RestartAtNextCheckpoint: no checkpoint after " + cur); return false; }
            var cpm = CheckpointManager.Instance;
            var snap = cpm != null ? cpm.BuildSnapshot(next) : new CheckpointSnapshot { CheckpointIndex = next };
            snap.CheckpointIndex = next;
            for (int i = 0; i < 3; i++)
            {
                if (snap.States[i] == CharacterState.Lost) continue;
                snap.States[i] = CharacterState.Alive;
                snap.Health[i] = Tuning.MaxHealth;
                snap.CapturedCellIds[i] = "";
            }
            Snapshot = snap;
            Stats.HRInterventions++;
            Stats.Restarts++;
            LevelRestarts++;
            Reload();
            return true;
        }

        public virtual void SaveCheckpoint(CheckpointSnapshot snapshot)
        {
            Snapshot = snapshot == null ? null : JsonUtility.FromJson<CheckpointSnapshot>(JsonUtility.ToJson(snapshot));
        }

        /// <summary>Called by LevelExit: records result, re-clones left-behind members, unlocks next level.</summary>
        public virtual void CompleteLevel(LevelResult result)
        {
            if (_completedSerial == _attemptSerial && HasLastResult)
            {
                Debug.LogWarning("[GameSession] CompleteLevel called twice for the same attempt - ignored.");
                return;
            }
            _completedSerial = _attemptSerial;
            int idx = result.LevelIndex >= 0 && LevelCatalog.Get(result.LevelIndex) != null ? result.LevelIndex : LevelIndex;
            result.LevelIndex = idx;
            if (result.Time <= 0f) result.Time = LevelElapsed;
            LastResult = result;
            HasLastResult = true;
            Stats.LevelsCompleted++;
            if (idx >= 0)
            {
                SessionPrefs.MarkCompleted(idx);
                LastResultWasRecord = SessionPrefs.SubmitTime(idx, result.Time);
            }
            LastRecloned = RecloneLeftBehind(result.LeftBehind, idx);
            Snapshot = null;
            int next = LevelCatalog.ClassicNext(idx);   // W0: classic flow stops after L5 (catalog has 22 entries)
            ResumeLevelIndex = next;
            if (SkipClassicRunSave) { }
            else if (ResumeLevelIndex >= 0) SaveRunProgress(); else SessionPrefs.DeleteRun();
        }

        public virtual void NextLevel()
        {
            if (IsLoading) return;
            if (NextLevelOverride != null)
            {
                bool handled = false;
                try { handled = NextLevelOverride(); }
                catch (Exception e) { Debug.LogException(e); handled = false; }
                if (handled) return;
            }
            int from = LevelIndex >= 0 ? LevelIndex : HasLastResult ? LastResult.LevelIndex : -1;
            int next = LevelCatalog.ClassicNext(from);   // W0: only L1..L5 chain; new chapters are routed by the campaign
            if (next < 0)
            {
                ShowVictory = from < LevelCatalog.MainGameCount;
                GoToMainMenu();
                return;
            }
            LoadLevel(next);
        }

        public virtual void GoToMainMenu()
        {
            if (IsLoading) return;
            Snapshot = null;
            SaveRunProgress();
            if (!SceneLoader.CanLoad(LevelCatalog.MainMenuScene))
            {
                Fail("GoToMainMenu: scene '" + LevelCatalog.MainMenuScene + "' is not in Build Settings (expected at " +
                     LevelCatalog.ScenePath(LevelCatalog.MainMenuScene) + ").");
                return;
            }
            StartCoroutine(LoadRoutine(LevelCatalog.MainMenuScene, null, -1));
        }

        /// <summary>Returns ShowVictory and clears it.</summary>
        public bool ConsumeVictory() { bool v = ShowVictory; ShowVictory = false; return v; }

        /// <summary>Called by LevelManager when its level scene is ready (also works when a level scene is played directly).
        /// Sets LevelIndex, ensures a team, resets per-level timers for a fresh level, sets Context = Gameplay and saves
        /// run progress. LevelManager fades in itself (ScreenFader.FadeIn); GameSession won't fade in once notified.</summary>
        public virtual void NotifyLevelLoaded(int index)
        {
            _levelNotified = true;
            if (LevelCatalog.Get(index) == null)
            {
                int byScene = LevelCatalog.IndexOfScene(SceneManager.GetActiveScene().name);
                if (byScene < 0) Debug.LogWarning("[GameSession] NotifyLevelLoaded(" + index + "): not a catalog level - treating as a test level.");
                else index = byScene;
            }
            BeginLevel(index);
        }

        // ================================================================ settings / progress
        public virtual void ApplySettings()
        {
            SessionPrefs.Sanitize(Settings);
            var s = Settings;
            try { AudioManager.Instance?.ApplyVolumes(s.MasterVolume, s.MusicVolume, s.SfxVolume); }
            catch (Exception e) { Debug.LogException(e); }
            ApplyFullscreen(s);
            try { PostFx.SetQuality(s.Quality); }
            catch (Exception e) { Debug.LogException(e); }
            GameEvents.RaiseSettingsChanged();
        }

        public virtual void SaveSettings() { SessionPrefs.SaveSettings(Settings); }

        /// <summary>Restore default settings (not saved until SaveSettings), then ApplySettings().
        /// WebGL keeps the current Fullscreen value: a reset never makes the page jump to fullscreen.</summary>
        public void ResetSettingsToDefaults()
        {
            bool fullscreenNow = Settings != null && Settings.Fullscreen;
            Settings = new SettingsData();
            if (Application.platform == RuntimePlatform.WebGLPlayer) Settings.Fullscreen = fullscreenNow;
            ApplySettings();
        }

        /// <summary>What <see cref="ApplySettings"/> does with SettingsData.Fullscreen (see <see cref="PlanFullscreen"/>).</summary>
        public struct FullscreenPlan
        {
            /// <summary>Change the window / page fullscreen state now (to <see cref="Fullscreen"/>).</summary>
            public bool Request;
            public bool Fullscreen;
            /// <summary>Value SettingsData.Fullscreen holds afterwards.</summary>
            public bool Setting;
        }

        /// <summary>Fullscreen policy (pure, unit-tested). applied = -1 on the first ApplySettings (boot), else the value last
        /// applied (0/1); wanted = SettingsData.Fullscreen; actual = Screen.fullScreen.
        /// Desktop: applied at boot and whenever the player changes the option (an OS-level change is not undone by
        /// unrelated settings changes). WebGL: NEVER at boot - browsers turn that into a requestFullscreen() on the player's
        /// first click (the page jumped to fullscreen on itch.io, which has its own fullscreen button, as does the template);
        /// the option mirrors the page and only an explicit change of the option requests fullscreen (that Settings click is
        /// the user gesture browsers require).</summary>
        public static FullscreenPlan PlanFullscreen(bool webPlayer, int applied, bool wanted, bool actual)
        {
            bool boot = applied < 0;
            bool changedByPlayer = !boot && wanted != (applied == 1);
            if (webPlayer)
            {
                if (boot) return new FullscreenPlan { Request = false, Fullscreen = actual, Setting = actual };
                if (changedByPlayer) return new FullscreenPlan { Request = wanted != actual, Fullscreen = wanted, Setting = wanted };
                return new FullscreenPlan { Request = false, Fullscreen = actual, Setting = wanted };
            }
            bool apply = boot || changedByPlayer;
            return new FullscreenPlan { Request = apply && wanted != actual, Fullscreen = wanted, Setting = wanted };
        }

        void ApplyFullscreen(SettingsData s)
        {
            if (Application.isEditor || Application.isBatchMode) return;   // never resize the Editor or a test run
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            bool boot = _fullscreenApplied < 0;
            bool actual = Screen.fullScreen;
            var plan = PlanFullscreen(web, _fullscreenApplied, s.Fullscreen, actual);
            if (plan.Request)
            {
                var mode = plan.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (web) Screen.fullScreen = plan.Fullscreen;           // Unity issues requestFullscreen() inside the player's click
                else if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
                if (web) Debug.Log("[GameSession] fullscreen " + (plan.Fullscreen ? "requested" : "exit requested") + " by the Settings option");
            }
            s.Fullscreen = plan.Setting;
            _fullscreenApplied = plan.Setting ? 1 : 0;
            if (web && boot) _pageFullscreen = actual;
        }

        /// <summary>Called by the SHIFTERS WebGL page (Assets/WebGLTemplates/Shifters) with
        /// unityInstance.SendMessage("[SHIFTERS]", "OnBrowserPointerLock", "1" | "0") on every pointerlockchange /
        /// pointerlockerror: whether the browser really holds the pointer lock (see ShiftersInput.NotifyBrowserPointerLock).</summary>
        public void OnBrowserPointerLock(string held) => ShiftersInput.NotifyBrowserPointerLock(held == "1");

        /// <summary>WebGL: the page left / entered fullscreen by itself (Esc, F11, itch.io or template button): mirror it in the
        /// setting so Settings shows the truth and nothing re-requests it later.</summary>
        void TrackPageFullscreen()
        {
            if (_fullscreenApplied < 0 || Application.platform != RuntimePlatform.WebGLPlayer) return;
            bool fs = Screen.fullScreen;
            if (fs == _pageFullscreen) return;
            _pageFullscreen = fs;
            if (Settings != null) Settings.Fullscreen = fs;
            _fullscreenApplied = fs ? 1 : 0;
        }

        public virtual bool IsLevelCompleted(int index) => SessionPrefs.IsCompleted(index);
        /// <summary>Best completion time of a level in seconds (-1 = never completed).</summary>
        public float BestTime(int index) => SessionPrefs.BestTime(index);

        public virtual void QuitGame()
        {
            SaveRunProgress();
            SessionPrefs.SaveSettings(Settings);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================================================================ internals
        void Fail(string message)
        {
            LastError = message;
            Debug.LogError("[GameSession] " + message);
        }

        bool CanRestart(string what)
        {
            if (IsLoading) { Debug.LogWarning("[GameSession] " + what + " ignored: a scene load is already running."); return false; }
            if (LevelIndex < 0 && SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene)
            {
                Fail(what + ": not in a level (main menu).");
                return false;
            }
            return true;
        }

        void Reload()
        {
            var info = CurrentLevel;
            if (info != null && SceneLoader.CanLoad(info.SceneName)) StartCoroutine(LoadRoutine(info.SceneName, info.Title, LevelIndex));
            else StartCoroutine(LoadRoutine(null, null, LevelIndex));
        }

        void BeginLevel(int index)
        {
            bool fresh = index != LevelIndex || _pendingFreshLevel;
            _pendingFreshLevel = false;
            LevelIndex = index;
            EnsureTeam();
            if (fresh)
            {
                LevelElapsed = 0f;
                LevelRestarts = 0;
                Snapshot = null; // a different level (or a fresh LoadLevel) never inherits a checkpoint
            }
            _attemptSerial++;
            LevelStartTime = Time.time;
            if (index >= 0) ResumeLevelIndex = index;
            ShiftersInput.Context = InputContext.Gameplay;
            if (index >= 0) SaveRunProgress();
        }

        void EnterMenuState()
        {
            LevelIndex = -1;
            Snapshot = null;
            ShiftersInput.Context = InputContext.Menu;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            _attemptSerial++;
            if (!IsLoading) DetectScene(scene);
        }

        void DetectScene(Scene scene)
        {
            if (scene.name == LevelCatalog.MainMenuScene) { EnterMenuState(); return; }
            int idx = LevelCatalog.IndexOfScene(scene.name);
            if (idx >= 0 && idx != LevelIndex) BeginLevel(idx);
        }

        string[] RecloneLeftBehind(string[] leftBehind, int levelIndex)
        {
            if (Team == null || Team.Members == null || leftBehind == null || leftBehind.Length == 0) return new string[0];
            var done = new bool[Team.Members.Length];
            var names = new List<string>(3);
            for (int n = 0; n < leftBehind.Length; n++)
            {
                int slot = FindMember(leftBehind[n], done);
                if (slot < 0) { Debug.LogWarning("[GameSession] CompleteLevel: left-behind '" + leftBehind[n] + "' is not on the team."); continue; }
                done[slot] = true;
                var clone = CharacterGenerator.Reclone(Team.Members[slot], unchecked(Team.Seed * 31 + levelIndex * 1009 + slot * 97));
                Team.Members[slot] = clone;
                names.Add(clone.DisplayName);
            }
            return names.ToArray();
        }

        int FindMember(string name, bool[] skip)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            var m = Team.Members;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < m.Length; i++)
                {
                    if (skip[i] || m[i] == null) continue;
                    string candidate = pass == 0 ? m[i].DisplayName : m[i].Name;
                    if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return i;
                }
            return -1;
        }

        /// <summary>sceneName == null => reload the active scene. targetIndex -1 => main menu.</summary>
        IEnumerator LoadRoutine(string sceneName, string caption, int targetIndex)
        {
            int serial = ++_routineSerial;
            IsLoading = true;
            _levelNotified = false;
            var previousContext = ShiftersInput.Context;
            TimeController.ResetAll();
            ShiftersInput.Context = InputContext.Cutscene;

            yield return Guarded(ScreenFader.FadeOut(FadeOutSeconds, caption));

            string error;
            AsyncOperation op = sceneName != null ? SceneLoader.Begin(sceneName, out error) : SceneLoader.ReloadActive(out error);
            if (op == null)
            {
                IsLoading = false;
                _pendingFreshLevel = false;
                ShiftersInput.Context = previousContext;
                Fail(error ?? "scene load failed");
                yield return Guarded(ScreenFader.FadeIn(FadeInSeconds));
                yield break;
            }
            while (!op.isDone) yield return null;
            if (serial != _routineSerial) yield break;
            IsLoading = false;

            if (targetIndex < 0 && sceneName == LevelCatalog.MainMenuScene)
            {
                EnterMenuState();
                yield return null; // let the UI build the menu (it listens to sceneLoaded)
                yield return Guarded(ScreenFader.FadeIn(FadeInSeconds));
                yield break;
            }

            float waited = 0f;
            while (!_levelNotified && waited < FadeInTimeout && serial == _routineSerial)
            {
                waited += Mathf.Min(TimeController.RealDeltaTime, 0.1f);
                yield return null;
            }
            if (serial != _routineSerial) yield break; // a newer load/restart took over during the wait
            if (!_levelNotified)
            {
                // no LevelManager in this scene (yet): still make it playable
                BeginLevel(targetIndex >= 0 ? targetIndex : LevelCatalog.IndexOfScene(SceneManager.GetActiveScene().name));
                yield return Guarded(ScreenFader.FadeIn(FadeInSeconds));
            }
        }

        /// <summary>Runs a fader coroutine but survives its exceptions and never waits longer than FaderTimeout.</summary>
        static IEnumerator Guarded(IEnumerator routine)
        {
            if (routine == null) yield break;
            float elapsed = 0f;
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception e) { Debug.LogException(e); yield break; }
                yield return current;
                elapsed += Mathf.Min(TimeController.RealDeltaTime, 0.25f);
                if (elapsed > FaderTimeout) { Debug.LogWarning("[GameSession] screen fade timed out."); yield break; }
            }
        }
    }
}
