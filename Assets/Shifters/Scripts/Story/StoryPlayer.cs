// =====================================================================================
//  SHIFTERS - StoryPlayer.cs   [story]
//  Plays StoryScripts. One blocking scene at a time + one non-blocking lane (PiP / radio / subtitles)
//  with a small queue. Presentation is ui_hud's / visuals': this only raises GameEvents with StoryLine data.
//
//  BLOCKING: InputContext.Cutscene, HUD suppressed, CutsceneCamera renders (gameplay camera suppressed),
//    CutsceneChanged(true) + LetterboxChanged(true), team under ExternalControl. Tap Enter/Space/LMB
//    (ShiftersInput.AdvancePressed) = next line; hold Backspace / gamepad Back 0.5 s (SkipHeld01) = skip.
//    Every exit path (end, Skip, watchdog, Stop, disable, scene change) runs Restore() — idempotent — the
//    same frame. 45 s watchdog (StoryScript.Watchdog).
//  NON-BLOCKING: lines with Broadcast = true for Max/Percy (PiP), false for others (subtitle bar).
//  STORY setting (SettingsData.StoryMode): 0 FULL (seen blocking scenes collapse to SHORT), 1 SHORT
//    (only ★ lines + non-FullOnly steps), 2 OFF (nothing plays; Essential steps run instantly).
//  AutoSkip (bots) = like OFF for every script. Beats: GameEvents.StoryBeat(id) plays StoryScripts.Get(id).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public sealed class StoryPlayer : MonoBehaviour
    {
        public static StoryPlayer Instance { get; private set; }
        /// <summary>Bots / tests: every script is skipped (Essential steps still run). Reset at SubsystemRegistration.</summary>
        public static bool AutoSkip;
        /// <summary>Story clock multiplier (tests play at 4x).</summary>
        public static float TimeScale = 1f;
        /// <summary>Seconds a Prompt waits for CompletePrompt before auto-completing.</summary>
        public static float PromptTimeout = 25f;
        /// <summary>Campaign hook: returns false to mute beat-triggered scripts (Free Play). Null = allowed.</summary>
        public static Func<bool> BeatsAllowed;
        /// <summary>Campaign hook: true while a story campaign runs (VillainBarks only heckle then). Null = false.</summary>
        public static Func<bool> CampaignActive;
        /// <summary>Overrides SettingsData.StoryMode when >= 0 (tests).</summary>
        public static int StoryModeOverride = -1;

        /// <summary>Letterbox bars on/off (blocking scenes). ui_hud draws them.</summary>
        public static event Action<bool> LetterboxChanged;
        /// <summary>A Prompt step opened (text, actionId). Handler must call CompletePrompt(actionId).</summary>
        public static event Action<string, string> PromptRequested;
        /// <summary>A Prompt is being resolved without the player (skip / OFF / AutoSkip / timeout): handler picks a default.</summary>
        public static event Action<string> PromptAutoResolve;
        /// <summary>Generic presentation cue for other modules: (kind, target, arg). kinds: "tv" (channel / "off"),
        /// "puppet" (MAX, action), "face" (target, Expression name), "gesture" (target, VisualAction name), "scare" (cueId).</summary>
        public static event Action<string, string, string> Cue;
        public static event Action<string> ScriptStarted;
        /// <summary>(id, skipped)</summary>
        public static event Action<string, bool> ScriptFinished;

        public bool IsPlaying => _block != null || _nb != null;
        public bool IsBlocking => _block != null;
        public string CurrentId => _block != null ? _block.Script.Id : _nb != null ? _nb.Script.Id : null;
        /// <summary>Hold-to-skip progress 0..1 for the skip chip.</summary>
        public float SkipProgress01 { get; private set; }
        /// <summary>Story-clock seconds the current blocking scene has run (tests: blocking time).</summary>
        public float BlockingElapsed { get; private set; }
        public bool LastWasWatchdog { get; private set; }
        public int QueueCount => _queue.Count;

        // ------------------------------------------------------------------ runtime types
        sealed class Run
        {
            public StoryScript Script;
            public List<StoryStep> Steps;
            public bool Short;
            public bool Blocking;
            public int Index;
            public bool Started;
            public float T;
            public float Dur;
            public Run[] Lanes;
            public Action OnDone;
            public bool LineUp;
            public string PromptId;
            public bool Bark;
        }

        sealed class Pending { public StoryScript Script; public Action OnDone; public bool Bark; }

        Run _block, _nb;
        readonly List<Pending> _queue = new List<Pending>();
        readonly HashSet<string> _once = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly List<ShifterCharacter> _controlled = new List<ShifterCharacter>();
        readonly List<ShifterCharacter> _movers = new List<ShifterCharacter>();
        readonly List<Vector3> _moveTargets = new List<Vector3>();
        readonly List<float> _moveTimes = new List<float>();
        InputContext _prevContext = InputContext.Gameplay;
        bool _restoreNeeded, _broadcastOn, _fadeActive, _promptDone;
        string _promptWaiting;
        List<StoryStep> _promptInject;
        int _skipsInRow;
        const string SeenKey = "shifters_story_seen";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null; AutoSkip = false; TimeScale = 1f; PromptTimeout = 25f; BeatsAllowed = null; CampaignActive = null;
            StoryModeOverride = -1; LetterboxChanged = null; PromptRequested = null; PromptAutoResolve = null; Cue = null;
            ScriptStarted = null; ScriptFinished = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate() { Ensure(); }

        /// <summary>The persistent [STORY] root (StoryPlayer + VillainBarks + ShiftocracyEnding + CutsceneCamera).</summary>
        public static StoryPlayer Ensure()
        {
            if (Instance != null) return Instance;
            if (!Application.isPlaying) return null;
            var go = new GameObject("[STORY]");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<StoryPlayer>();
            go.AddComponent<VillainBarks>();
            go.AddComponent<ShiftocracyEnding>();
            CutsceneCamera.Ensure(go.transform);
            return p;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            LoadSeen();
        }

        void OnEnable()
        {
            GameEvents.StoryBeat += OnBeat;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.StoryBeat -= OnBeat;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopAll();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single) return;
            _once.Clear();
            StopAll();
        }

        /// <summary>BeatsAllowed if set; else campaign fallback: beats are muted only in Free Play (CampaignDirector.IsFreePlay).</summary>
        public static bool BeatsAllowedNow()
        {
            if (BeatsAllowed != null) return BeatsAllowed();
            var d = CampaignDirector.Instance;
            return d == null || !d.IsFreePlay;
        }

        /// <summary>CampaignActive if set; else CampaignDirector.Instance.IsCampaign (false when the campaign module is absent).</summary>
        public static bool CampaignActiveNow()
        {
            if (CampaignActive != null) return CampaignActive();
            var d = CampaignDirector.Instance;
            return d != null && d.IsCampaign;
        }

        void OnBeat(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!BeatsAllowedNow()) return;
            if (!StoryScripts.Exists(id)) return;
            var s = StoryScripts.Get(id);
            if (s == null || (CurrentId != null && string.Equals(CurrentId, s.Id, StringComparison.OrdinalIgnoreCase))) return;
            Play(s, null);
        }

        // ------------------------------------------------------------------ settings
        public static int StoryMode
        {
            get
            {
                if (StoryModeOverride >= 0) return Mathf.Clamp(StoryModeOverride, 0, 2);
                var gs = GameSession.Instance;
                return gs != null && gs.Settings != null ? Mathf.Clamp(gs.Settings.StoryMode, 0, 2) : 0;
            }
        }

        static float SubtitleSpeed
        {
            get { var gs = GameSession.Instance; return gs != null && gs.Settings != null && gs.Settings.SubtitleSpeed > 0f ? gs.Settings.SubtitleSpeed : 1f; }
        }

        public bool HasSeen(string id) => !string.IsNullOrEmpty(id) && _seen.Contains(id);

        void LoadSeen()
        {
            _seen.Clear();
            var s = PlayerPrefs.GetString(SeenKey, "");
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(',')) if (p.Length > 0) _seen.Add(p);
        }

        void MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id) || !_seen.Add(id)) return;
            PlayerPrefs.SetString(SeenKey, string.Join(",", _seen));
        }

        /// <summary>Forget seen scenes (tests / new campaign).</summary>
        public void ClearSeen() { _seen.Clear(); PlayerPrefs.DeleteKey(SeenKey); }

        // ------------------------------------------------------------------ public API
        public void Play(string id, Action onDone = null)
        {
            var s = StoryScripts.Get(id);
            if (s == null) { Debug.LogWarning("[Story] unknown script '" + id + "'"); onDone?.Invoke(); return; }
            Play(s, onDone);
        }

        public void Play(StoryScript s, Action onDone = null)
        {
            if (s == null) { onDone?.Invoke(); return; }
            if (s.Once && !_once.Add(s.Id)) { onDone?.Invoke(); return; }
            int mode = StoryMode;
            if (AutoSkip || mode == 2)
            {
                RunEssentials(s.Steps, 0);
                ScriptStarted?.Invoke(s.Id);
                ScriptFinished?.Invoke(s.Id, true);
                onDone?.Invoke();
                return;
            }
            if (s.Blocking)
            {
                if (_block != null) { _queue.Add(new Pending { Script = s, OnDone = onDone }); return; }
                if (_nb != null) AbortNonBlocking(true);
                StartBlocking(s, onDone, mode == 1 || HasSeen(s.Id));
                return;
            }
            if (_block == null && _nb == null) { _nb = NewRun(s, onDone, mode == 1, false); ScriptStarted?.Invoke(s.Id); return; }
            if (s.HighPriority && _block == null && _nb != null && (_nb.Bark || !_nb.Script.HighPriority))
            {
                AbortNonBlocking(false);
                _nb = NewRun(s, onDone, mode == 1, false); ScriptStarted?.Invoke(s.Id);
                return;
            }
            if (s.HighPriority) _queue.Insert(0, new Pending { Script = s, OnDone = onDone });
            else
            {
                if (_queue.Count >= 4) DropOldestLow();
                _queue.Add(new Pending { Script = s, OnDone = onDone });
            }
        }

        /// <summary>A bark: plays only if the lane is free (high = may interrupt a running bark / queue first).
        /// Returns true if it started or was queued.</summary>
        public bool PlayBark(StoryScript s, bool high)
        {
            if (s == null || AutoSkip || _block != null) return false;
            if (_nb == null && _queue.Count == 0) { _nb = NewRun(s, null, StoryMode == 1, false); _nb.Bark = true; ScriptStarted?.Invoke(s.Id); return true; }
            if (!high) return false;
            if (_nb != null && _nb.Bark) { AbortNonBlocking(false); _nb = NewRun(s, null, false, false); _nb.Bark = true; ScriptStarted?.Invoke(s.Id); return true; }
            _queue.Insert(0, new Pending { Script = s, Bark = true });
            return true;
        }

        /// <summary>Skip the blocking scene now (restores context/camera/HUD this frame).</summary>
        public void Skip()
        {
            if (_block == null) return;
            var r = _block;
            FinishRun(r, true);
            _skipsInRow++;
            if (_skipsInRow >= 2 && StoryModeOverride < 0)
            {
                var gs = GameSession.Instance;
                if (gs != null && gs.Settings != null && gs.Settings.StoryMode == 0)
                {
                    gs.Settings.StoryMode = 1;
                    try { SessionPrefs.SaveSettings(gs.Settings); } catch { }
                    GameEvents.RaiseSettingsChanged();
                    GameEvents.RaiseToast("Here for the puzzles? Story set to SHORT.");
                }
                _skipsInRow = 0;
            }
        }

        /// <summary>Stop everything (essentials of unplayed steps still run; callbacks fire).</summary>
        public void StopAll()
        {
            if (_block != null) FinishRun(_block, true);
            if (_nb != null) FinishRun(_nb, true);
            while (_queue.Count > 0)
            {
                var p = _queue[0]; _queue.RemoveAt(0);
                RunEssentials(p.Script.Steps, 0);
                p.OnDone?.Invoke();
            }
            RestoreBlocking();
        }

        /// <summary>Resolve an open Prompt step. inject (optional) adds steps that play right after the prompt.</summary>
        public void CompletePrompt(string actionId, Action<StoryScriptBuilder> inject = null)
        {
            if (_promptWaiting == null || !string.Equals(_promptWaiting, actionId, StringComparison.OrdinalIgnoreCase)) return;
            _promptDone = true;
            if (inject != null)
            {
                var tmp = StoryScripts.Inline("inject", inject);
                _promptInject = new List<StoryStep>(tmp.Steps);
            }
        }

        public bool PromptOpen(string actionId) => _promptWaiting != null && string.Equals(_promptWaiting, actionId, StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ run management
        Run NewRun(StoryScript s, Action onDone, bool shortMode, bool blocking)
        {
            return new Run { Script = s, Steps = new List<StoryStep>(s.Steps), Short = shortMode, Blocking = blocking, OnDone = onDone };
        }

        void StartBlocking(StoryScript s, Action onDone, bool shortMode)
        {
            _block = NewRun(s, onDone, shortMode, true);
            BlockingElapsed = 0f;
            LastWasWatchdog = false;
            SkipProgress01 = 0f;
            MarkSeen(s.Id);
            try
            {
                _restoreNeeded = true;
                var ctx = ShiftersInput.Context;
                _prevContext = ctx == InputContext.Cutscene ? InputContext.Gameplay : ctx;
                ShiftersInput.Context = InputContext.Cutscene;
                StoryBridge.SetHudSuppressed(true);
                var cc = CutsceneCamera.Ensure(transform);
                var from = StoryBridge.GameplayCamera;
                cc.Begin(from);
                StoryBridge.SuppressMainCamera(true, cc.Cam);
                if (from != null && from.enabled && from != cc.Cam) from.enabled = false; // one camera renders, always
                TakeControl();
                GameEvents.RaiseCutsceneChanged(true);
                if (s.Letterbox) LetterboxChanged?.Invoke(true);
            }
            catch (Exception e) { Debug.LogError("[Story] blocking start failed: " + e); }
            ScriptStarted?.Invoke(s.Id);
        }

        void TakeControl()
        {
            _controlled.Clear();
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c == null || c.ExternalControl) continue;
                c.ExternalControl = true;
                if (c.Motor != null) c.Motor.SetCommand(default(CharacterMotor.Command));
                _controlled.Add(c);
            }
        }

        void RestoreBlocking()
        {
            if (!_restoreNeeded) return;
            _restoreNeeded = false;
            try
            {
                for (int i = 0; i < _controlled.Count; i++)
                {
                    var c = _controlled[i];
                    if (c == null) continue;
                    if (c.Motor != null) c.Motor.SetCommand(default(CharacterMotor.Command));
                    c.ExternalControl = false;
                }
                _controlled.Clear();
                _movers.Clear(); _moveTargets.Clear(); _moveTimes.Clear();
                var cc = CutsceneCamera.Instance;
                if (cc != null) cc.End();
                StoryBridge.SuppressMainCamera(false);
                var gc = StoryBridge.GameplayCamera;
                if (gc != null && !gc.enabled) gc.enabled = true;
                StoryBridge.SetHudSuppressed(false);
                if (ShiftersInput.Context == InputContext.Cutscene) ShiftersInput.Context = _prevContext;
                if (_fadeActive) { _fadeActive = false; ScreenFader.Set(0f); }
                SkipProgress01 = 0f;
            }
            catch (Exception e) { Debug.LogError("[Story] restore failed: " + e); }
            GameEvents.RaiseCutsceneChanged(false);
            LetterboxChanged?.Invoke(false);
        }

        void AbortNonBlocking(bool requeue)
        {
            var r = _nb;
            if (r == null) return;
            if (requeue && !r.Bark)
            {
                // keep what has not been said yet: re-queue the remaining steps as a fresh run
                HideLine(r);
                _nb = null;
                if (r.Index < r.Steps.Count)
                {
                    var rest = new StoryScript { Id = r.Script.Id, HighPriority = r.Script.HighPriority };
                    for (int i = r.Index + (r.Started ? 1 : 0); i < r.Steps.Count; i++) rest.Steps.Add(r.Steps[i]);
                    _queue.Insert(0, new Pending { Script = rest, OnDone = r.OnDone });
                }
                else r.OnDone?.Invoke();
                SetBroadcast(false, null);
                return;
            }
            FinishRun(r, true);
        }

        void DropOldestLow()
        {
            for (int i = 0; i < _queue.Count; i++)
                if (!_queue[i].Script.HighPriority)
                {
                    var p = _queue[i]; _queue.RemoveAt(i);
                    RunEssentials(p.Script.Steps, 0);
                    p.OnDone?.Invoke();
                    return;
                }
        }

        void FinishRun(Run r, bool skipped)
        {
            if (r == null) return;
            if (skipped) SkipRemaining(r);
            HideLine(r);
            if (r == _block) { _block = null; RestoreBlocking(); if (!skipped) _skipsInRow = 0; }
            if (r == _nb) _nb = null;
            SetBroadcast(false, null);
            if (_promptWaiting != null && (r.PromptId == _promptWaiting)) { _promptWaiting = null; }
            ScriptFinished?.Invoke(r.Script.Id, skipped);
            try { r.OnDone?.Invoke(); }
            catch (Exception e) { Debug.LogError("[Story] onDone of " + r.Script.Id + " threw: " + e); }
        }

        void SkipRemaining(Run r)
        {
            // the running step: lanes / prompt
            if (r.Index < r.Steps.Count && r.Started)
            {
                var st = r.Steps[r.Index];
                if (st.Kind == StoryStepKind.Parallel && r.Lanes != null)
                    for (int i = 0; i < r.Lanes.Length; i++) if (r.Lanes[i] != null) { HideLine(r.Lanes[i]); SkipRemaining(r.Lanes[i]); }
                if (st.Kind == StoryStepKind.Prompt && !_promptDone) AutoResolvePrompt(st.Text2);
                if (st.Kind == StoryStepKind.Fade) { _fadeActive = false; ScreenFader.Set(0f); }
                r.Index++;
            }
            RunEssentials(r.Steps, r.Index);
            r.Index = r.Steps.Count;
        }

        void AutoResolvePrompt(string actionId)
        {
            _promptInject = null;
            try { PromptAutoResolve?.Invoke(actionId); } catch (Exception e) { Debug.LogError("[Story] prompt auto-resolve: " + e); }
            _promptWaiting = null;
            _promptDone = true;
            _promptInject = null; // injected lines are not played when skipping
        }

        void RunEssentials(List<StoryStep> steps, int from)
        {
            for (int i = from; i < steps.Count; i++)
            {
                var st = steps[i];
                if (st.Kind == StoryStepKind.Parallel && st.Lanes != null)
                {
                    for (int l = 0; l < st.Lanes.Length; l++) if (st.Lanes[l] != null) RunEssentials(st.Lanes[l], 0);
                    continue;
                }
                if (!st.Essential) continue;
                try
                {
                    switch (st.Kind)
                    {
                        case StoryStepKind.Music: AudioManager.Instance?.PlayMusic(st.Music); break;
                        case StoryStepKind.WorldWave: StoryBridge.WorldSetBase(st.C); break;
                        case StoryStepKind.Objective: GameEvents.RaiseObjective(StoryText.Resolve(st.Text)); break;
                        case StoryStepKind.Beat: GameEvents.RaiseStoryBeat(st.Text); break;
                        case StoryStepKind.Do: st.Callback?.Invoke(); break;
                        case StoryStepKind.Prompt: AutoResolvePrompt(st.Text2); break;
                    }
                }
                catch (Exception e) { Debug.LogError("[Story] essential step " + st.Kind + " threw: " + e); }
            }
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = ShiftersCamera.UnscaledDeltaTime * Mathf.Max(0.01f, TimeScale);
            if (_block != null)
            {
                BlockingElapsed += dt;
                float skip = StoryBridge.SkipHeld01;
                SkipProgress01 = Mathf.Clamp01(skip);
                if (skip >= 1f) { Skip(); }
                else if (BlockingElapsed >= _block.Script.Watchdog)
                {
                    Debug.LogWarning("[Story] watchdog: '" + _block.Script.Id + "' exceeded " + _block.Script.Watchdog + " s, forcing end");
                    LastWasWatchdog = true;
                    FinishRun(_block, true);
                }
                else
                {
                    bool advance = StoryBridge.AdvancePressed;
                    var r = _block;
                    bool done = false;
                    try { done = Tick(r, dt, advance); }
                    catch (Exception e) { Debug.LogError("[Story] '" + r.Script.Id + "' step threw: " + e); done = true; }
                    if (done && r == _block) FinishRun(r, false);
                    TickMovers(dt);
                }
            }
            else if (_nb != null)
            {
                var ui = UIManager.Instance;
                if (ui == null || !ui.IsPaused)
                {
                    var r = _nb;
                    bool done = false;
                    try { done = Tick(r, dt, false); }
                    catch (Exception e) { Debug.LogError("[Story] '" + r.Script.Id + "' step threw: " + e); done = true; }
                    if (done && r == _nb) FinishRun(r, false);
                }
            }
            if (_block == null && _nb == null && _queue.Count > 0)
            {
                var p = _queue[0]; _queue.RemoveAt(0);
                if (p.Script.Blocking) Play(p.Script, p.OnDone);
                else
                {
                    int mode = StoryMode;
                    if (AutoSkip || mode == 2) { RunEssentials(p.Script.Steps, 0); p.OnDone?.Invoke(); }
                    else { _nb = NewRun(p.Script, p.OnDone, mode == 1, false); _nb.Bark = p.Bark; ScriptStarted?.Invoke(p.Script.Id); }
                }
            }
        }

        bool Skipped(Run r, StoryStep st)
        {
            if (!r.Short) return false;
            if (st.FullOnly) return true;
            if ((st.Kind == StoryStepKind.Line || st.Kind == StoryStepKind.Broadcast) && !st.Star && r.Script.HasStars) return true;
            return false;
        }

        /// <summary>Advance a run; true when finished.</summary>
        bool Tick(Run r, float dt, bool advance)
        {
            int guard = 0;
            while (r.Index < r.Steps.Count)
            {
                if (++guard > 256) return false;
                var st = r.Steps[r.Index];
                if (!r.Started)
                {
                    if (Skipped(r, st)) { r.Index++; continue; }
                    r.Started = true; r.T = 0f;
                    if (!Begin(r, st)) { r.Started = false; r.Index++; continue; }
                    return false; // waiting steps start counting next frame
                }
                r.T += dt;
                if (!Waiting(r, st, advance)) { End(r, st); r.Started = false; r.Index++; continue; }
                return false;
            }
            return true;
        }

        /// <summary>Start a step. Returns true if it waits.</summary>
        bool Begin(Run r, StoryStep st)
        {
            switch (st.Kind)
            {
                case StoryStepKind.Line:
                case StoryStepKind.Broadcast:
                    ShowLine(r, st);
                    return true;
                case StoryStepKind.Wait:
                    r.Dur = st.A * (r.Short ? 0.6f : 1f);
                    return r.Dur > 0f;
                case StoryStepKind.Fade:
                    r.Dur = Mathf.Max(0.01f, st.A);
                    _fadeActive = true;
                    StartCoroutine(ScreenFader.FadeOut(r.Dur / Mathf.Max(0.01f, TimeScale)));
                    return true;
                case StoryStepKind.Prompt:
                    r.PromptId = st.Text2;
                    _promptWaiting = st.Text2;
                    _promptDone = false;
                    _promptInject = null;
                    r.Dur = PromptTimeout;
                    PromptRequested?.Invoke(StoryText.Resolve(st.Text), st.Text2);
                    return !_promptDone;
                case StoryStepKind.Parallel:
                    if (st.Lanes == null || st.Lanes.Length == 0) return false;
                    r.Lanes = new Run[st.Lanes.Length];
                    for (int i = 0; i < st.Lanes.Length; i++)
                        r.Lanes[i] = new Run { Script = r.Script, Steps = new List<StoryStep>(st.Lanes[i]), Short = r.Short, Blocking = r.Blocking };
                    return true;
                default:
                    Instant(r, st);
                    return false;
            }
        }

        bool Waiting(Run r, StoryStep st, bool advance)
        {
            switch (st.Kind)
            {
                case StoryStepKind.Line:
                case StoryStepKind.Broadcast:
                    if (advance && r.Blocking && r.T > 0.15f) return false;
                    return r.T < r.Dur;
                case StoryStepKind.Wait: return r.T < r.Dur;
                case StoryStepKind.Fade: return r.T < r.Dur;
                case StoryStepKind.Prompt:
                    if (_promptDone) return false;
                    if (r.T >= r.Dur) { AutoResolvePrompt(st.Text2); return false; }
                    return true;
                case StoryStepKind.Parallel:
                {
                    bool any = false;
                    for (int i = 0; i < r.Lanes.Length; i++)
                    {
                        var l = r.Lanes[i];
                        if (l == null) continue;
                        if (Tick(l, r.T > 0f ? Mathf.Min(r.T, 0.25f) : 0f, advance)) { HideLine(l); r.Lanes[i] = null; }
                        else any = true;
                    }
                    r.T = 0f;
                    return any;
                }
            }
            return false;
        }

        void End(Run r, StoryStep st)
        {
            switch (st.Kind)
            {
                case StoryStepKind.Line:
                case StoryStepKind.Broadcast:
                    HideLine(r);
                    break;
                case StoryStepKind.Fade:
                    StartCoroutine(ScreenFader.FadeIn(Mathf.Max(0.01f, st.B) / Mathf.Max(0.01f, TimeScale)));
                    _fadeActive = false;
                    break;
                case StoryStepKind.Prompt:
                    _promptWaiting = null;
                    if (_promptInject != null && _promptInject.Count > 0)
                    {
                        r.Steps.InsertRange(r.Index + 1, _promptInject);
                        _promptInject = null;
                    }
                    break;
                case StoryStepKind.Parallel:
                    r.Lanes = null;
                    break;
            }
        }

        // ------------------------------------------------------------------ lines
        void ShowLine(Run r, StoryStep st)
        {
            StoryText.Speaker(st.Speaker, out var id, out var name, out var color, out var who);
            var text = StoryText.Resolve(st.Text);
            bool pip = st.Kind == StoryStepKind.Broadcast || (!r.Blocking && StoryText.IsPipSpeaker(id));
            r.Dur = StoryText.LineSeconds(text, SubtitleSpeed) * (st.Kind == StoryStepKind.Broadcast ? 1f : 1f);
            var line = new StoryLine { SpeakerId = id, SpeakerName = name, Text = text, SpeakerColor = color, Duration = r.Dur, Broadcast = pip, Face = st.Face };
            if (pip) SetBroadcast(true, id); else SetBroadcast(false, null);
            if (who != null && who.Visual != null && st.Face != Expression.Neutral) who.Visual.SetExpression(st.Face, r.Dur);
            r.LineUp = true;
            GameEvents.RaiseStoryLineShown(line);
        }

        void HideLine(Run r)
        {
            if (r == null || !r.LineUp) return;
            r.LineUp = false;
            GameEvents.RaiseStoryLineHidden();
        }

        void SetBroadcast(bool on, string speaker)
        {
            if (on == _broadcastOn) return;
            _broadcastOn = on;
            GameEvents.RaiseBroadcastChanged(on, on ? speaker : null);
        }

        // ------------------------------------------------------------------ instant steps
        void Instant(Run r, StoryStep st)
        {
            switch (st.Kind)
            {
                case StoryStepKind.Music: AudioManager.Instance?.PlayMusic(st.Music); break;
                case StoryStepKind.Sfx: Shifters.Sfx.Play(st.Sfx); break;
                case StoryStepKind.Banner: GameEvents.RaiseBanner(StoryText.Resolve(st.Text), StoryText.Resolve(st.Text2), st.Style); break;
                case StoryStepKind.Toast: GameEvents.RaiseToast(StoryText.Resolve(st.Text), StoryText.MaxGold); break;
                case StoryStepKind.Card: GameEvents.RaiseChapterCard(StoryText.Resolve(st.Text), StoryText.Resolve(st.Text2)); break;
                case StoryStepKind.Objective: GameEvents.RaiseObjective(StoryText.Resolve(st.Text)); break;
                case StoryStepKind.Beat: GameEvents.RaiseStoryBeat(st.Text); break;
                case StoryStepKind.Do: st.Callback?.Invoke(); break;
                case StoryStepKind.Tv: Cue?.Invoke("tv", "MAX", st.Text); break;
                case StoryStepKind.Puppet: Cue?.Invoke("puppet", "MAX", st.Text); break;
                case StoryStepKind.Scare: Cue?.Invoke("scare", null, st.Text); GameEvents.RaiseStoryBeat(st.Text); break;
                case StoryStepKind.WorldWave:
                {
                    Vector3 o = Vector3.zero;
                    if (!string.IsNullOrEmpty(st.Text) && LevelMark.Exists(st.Text)) o = LevelMark.Position(st.Text);
                    else { var a = CharacterManager.Instance != null ? CharacterManager.Instance.Active : null; if (a != null) o = a.transform.position; }
                    StoryBridge.WorldWave(o, st.A / Mathf.Max(0.01f, TimeScale), st.B, st.C);
                    break;
                }
                case StoryStepKind.Face:
                case StoryStepKind.Gesture:
                {
                    var c = InternFor(st.Speaker);
                    if (c != null && c.Visual != null)
                    {
                        if (st.Kind == StoryStepKind.Face) c.Visual.SetExpression(st.Face, 2.5f);
                        else c.Visual.Play(st.Action);
                    }
                    else Cue?.Invoke(st.Kind == StoryStepKind.Face ? "face" : "gesture", st.Speaker,
                        st.Kind == StoryStepKind.Face ? st.Face.ToString() : st.Action.ToString());
                    break;
                }
                case StoryStepKind.Move:
                {
                    var c = StoryText.FindIntern(st.Who);
                    if (c != null && !string.IsNullOrEmpty(st.Text) && LevelMark.Exists(st.Text))
                    {
                        if (!c.ExternalControl) { c.ExternalControl = true; _controlled.Add(c); }
                        _movers.Add(c); _moveTargets.Add(LevelMark.Position(st.Text)); _moveTimes.Add(0f);
                    }
                    break;
                }
                case StoryStepKind.Shot:
                {
                    var cc = CutsceneCamera.Instance;
                    if (cc == null || !cc.Active) break;
                    if (st.Text == null) { cc.GlideTo(st.P0, st.P1, st.A, st.B); break; }
                    if (!LevelMark.Exists(st.Text)) break;
                    var pos = LevelMark.Position(st.Text);
                    var look = !string.IsNullOrEmpty(st.Text2) && LevelMark.Exists(st.Text2) ? LevelMark.Position(st.Text2) + Vector3.up * 1.2f
                                                                                             : pos + cc.transform.forward * 5f;
                    cc.GlideTo(pos, look, st.A, st.B);
                    break;
                }
                case StoryStepKind.ShotTeam:
                {
                    var cc = CutsceneCamera.Instance;
                    if (cc == null || !cc.Active) break;
                    if (TeamFrame(st.C, out var pos, out var look)) cc.GlideTo(pos, look, st.A, st.B);
                    break;
                }
                case StoryStepKind.CameraDrift:
                    if (CutsceneCamera.Instance != null) CutsceneCamera.Instance.Drift(st.A);
                    break;
            }
        }

        static ShifterCharacter InternFor(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            var t = token.Trim('{', '}').ToUpperInvariant();
            switch (t)
            {
                case "BRUTE": return StoryText.FindIntern(Archetype.Brute);
                case "SPRINTER": return StoryText.FindIntern(Archetype.Sprinter);
                case "BRAINIAC": return StoryText.FindIntern(Archetype.Brainiac);
                case "GUNNER": return StoryText.FindGunner();
                case "ACTIVE": return CharacterManager.Instance != null ? CharacterManager.Instance.Active : null;
            }
            return null;
        }

        /// <summary>Camera pose framing the living team from the front of the active intern.</summary>
        public static bool TeamFrame(float distance, out Vector3 pos, out Vector3 look)
        {
            pos = look = Vector3.zero;
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null || cm.Team.Count == 0) return false;
            Vector3 sum = Vector3.zero; int n = 0;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c == null || c.State == CharacterState.Lost) continue;
                sum += c.transform.position; n++;
            }
            if (n == 0) return false;
            var center = sum / n + Vector3.up * 1.0f;
            var a = cm.Active != null ? cm.Active : cm.Team[0];
            var fwd = a != null ? a.transform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            fwd.Normalize();
            look = center;
            pos = center + fwd * Mathf.Max(2f, distance) + Vector3.up * 0.8f;
            return true;
        }

        void TickMovers(float dt)
        {
            for (int i = _movers.Count - 1; i >= 0; i--)
            {
                var c = _movers[i];
                if (c == null || c.Motor == null) { _movers.RemoveAt(i); _moveTargets.RemoveAt(i); _moveTimes.RemoveAt(i); continue; }
                _moveTimes[i] += dt;
                var d = _moveTargets[i] - c.transform.position; d.y = 0f;
                if (d.magnitude < 0.6f || _moveTimes[i] > 4f)
                {
                    if (d.magnitude >= 0.6f) c.Motor.Teleport(_moveTargets[i], c.transform.rotation);
                    c.Motor.SetCommand(default(CharacterMotor.Command));
                    _movers.RemoveAt(i); _moveTargets.RemoveAt(i); _moveTimes.RemoveAt(i);
                    continue;
                }
                c.Motor.SetCommand(new CharacterMotor.Command { Move = d.normalized * 0.6f });
            }
        }
    }
}
