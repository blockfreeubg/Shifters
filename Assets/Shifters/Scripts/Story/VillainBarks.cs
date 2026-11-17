// =====================================================================================
//  SHIFTERS - VillainBarks.cs   [story]
//  Max heckles the team over the ME-TV PiP (CAMPAIGN §8.2 / §8.4). Lives on the [STORY] root.
//  Rules: <= 1 bark per 40 s (Heckles 0 OFTEN) / 80 s (1 SOMETIMES) / none (2 NEVER); <= 8 per level
//  (6 in L1; halved on SOMETIMES); never during a blocking scene or while a tutorial popup is up
//  (counter-barks fire when the matching tutorial hides, or 6 s after it shows); high-priority events
//  (Lost, team wipe, "bark.*!" beats) skip the cooldown/cap. Only active while StoryPlayer.CampaignActive
//  returns true (or ForceEnabled for tests). Other modules can request a bark with
//  GameEvents.RaiseStoryBeat("bark.<key>") (key from StoryText.EventBarks; "bark.<key>!" = high priority).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class VillainBarks : MonoBehaviour
    {
        public static VillainBarks Instance { get; private set; }
        /// <summary>Tests: heckle even without a campaign.</summary>
        public static bool ForceEnabled;
        public static event Action<string, string> BarkFired;   // (key, text)

        public const float CooldownOften = 40f, CooldownSometimes = 80f, CounterBarkDelay = 6f, IdleSeconds = 90f;

        public int BarksThisLevel { get; private set; }
        public string LastKey { get; private set; }
        public float Clock => _clock;

        float _clock, _lastBark = -999f, _tutShownAt, _idleT, _levelStartAt;
        bool _tutVisible, _firstPuzzle, _checkpointBarked, _idleBarked, _reprintPending;
        string _tutId, _pendingCounter;
        float _pendingExitAt = -1f; string _pendingExitKey;
        readonly HashSet<string> _counterDone = new HashSet<string>();
        readonly List<float> _switchTimes = new List<float>();
        readonly Dictionary<string, int> _rot = new Dictionary<string, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; ForceEnabled = false; BarkFired = null; }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            GameEvents.PuzzleSolved += OnPuzzle;
            GameEvents.CharacterSwitched += OnSwitched;
            GameEvents.CharacterStateChanged += OnState;
            GameEvents.CharacterConditionChanged += OnCondition;
            GameEvents.TeamWiped += OnWiped;
            GameEvents.LevelCompleted += OnCompleted;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.TutorialShow += OnTutShow;
            GameEvents.TutorialHide += OnTutHide;
            GameEvents.StoryBeat += OnBeat;
        }

        void OnDisable()
        {
            GameEvents.PuzzleSolved -= OnPuzzle;
            GameEvents.CharacterSwitched -= OnSwitched;
            GameEvents.CharacterStateChanged -= OnState;
            GameEvents.CharacterConditionChanged -= OnCondition;
            GameEvents.TeamWiped -= OnWiped;
            GameEvents.LevelCompleted -= OnCompleted;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.TutorialShow -= OnTutShow;
            GameEvents.TutorialHide -= OnTutHide;
            GameEvents.StoryBeat -= OnBeat;
        }

        // ------------------------------------------------------------------ settings
        public static bool Active
        {
            get
            {
                if (ForceEnabled) return true;
                return StoryPlayer.CampaignActiveNow();
            }
        }

        public static int Heckles
        {
            get { var gs = GameSession.Instance; return gs != null && gs.Settings != null ? Mathf.Clamp(gs.Settings.Heckles, 0, 2) : 0; }
        }

        public static float Cooldown => Heckles == 1 ? CooldownSometimes : CooldownOften;

        static int LevelIndex => LevelManager.Instance != null ? LevelManager.Instance.LevelIndex : -1;

        public static int LevelCap
        {
            get { int cap = LevelIndex == 0 ? 6 : 8; return Heckles == 1 ? cap / 2 : cap; }
        }

        // ------------------------------------------------------------------ core
        /// <summary>Try to bark a line from StoryText.EventBarks[key]. Returns true if it played/queued.</summary>
        public bool TryBark(string key, bool high = false)
        {
            if (!StoryText.EventBarks.TryGetValue(key ?? "", out var bank) || bank.Length == 0) return false;
            _rot.TryGetValue(key, out int i);
            _rot[key] = i + 1;
            return TryBarkText(key, bank[i % bank.Length], high);
        }

        public bool TryBarkText(string key, string text, bool high, string speaker = "MAX")
        {
            if (!Active || Heckles >= 2 || string.IsNullOrEmpty(text)) return false;
            var sp = StoryPlayer.Instance;
            if (sp == null || sp.IsBlocking) return false;
            if (!high)
            {
                if (_tutVisible) return false;
                if (_clock - _lastBark < Cooldown) return false;
                if (BarksThisLevel >= LevelCap) return false;
                if (sp.IsPlaying) return false;
            }
            return TryBarkScript(key, StoryScripts.Inline("bark." + key, b => b.Line(speaker, text)), high, text);
        }

        bool TryBarkScript(string key, StoryScript s, bool high, string text)
        {
            if (!Active || Heckles >= 2 || s == null) return false;
            var sp = StoryPlayer.Instance;
            if (sp == null || sp.IsBlocking) return false;
            if (!high && (_tutVisible || _clock - _lastBark < Cooldown || BarksThisLevel >= LevelCap || sp.IsPlaying)) return false;
            if (!sp.PlayBark(s, high)) return false;
            _lastBark = _clock;
            BarksThisLevel++;
            LastKey = key;
            BarkFired?.Invoke(key, text);
            return true;
        }

        void Update()
        {
            float dt = ShiftersCamera.UnscaledDeltaTime * Mathf.Max(0.01f, StoryPlayer.TimeScale);
            _clock += dt;
            if (!Active) return;

            // counter-bark: 6 s after the tutorial showed (or when it hid)
            if (_pendingCounter != null && (!_tutVisible || _clock - _tutShownAt >= CounterBarkDelay))
            {
                var id = _pendingCounter;
                bool wasVisible = _tutVisible;
                _tutVisible = false; // the 6 s rule overrides the popup
                if (TryCounter(id)) _pendingCounter = null;
                else if (_clock - _tutShownAt > CounterBarkDelay + 20f) _pendingCounter = null; // give up
                _tutVisible = wasVisible;
            }

            if (_pendingExitAt >= 0f && _clock >= _pendingExitAt)
            {
                _pendingExitAt = -1f;
                var sp = StoryPlayer.Instance;
                if (sp != null && !sp.IsPlaying) TryBark(_pendingExitKey, true);
            }

            if (_reprintPending && _clock - _levelStartAt > 4f)
            {
                _reprintPending = false;
                var bank = StoryText.EventBarks["reprint"];
                TryBarkScript("reprint", StoryScripts.Inline("bark.reprint", b => b.Sfx(SfxId.PrinterWhir)
                    .Line("PRINTER", StoryText.ReprintPrinterLine).Line("MAX", bank[0])), true, bank[0]);
            }

            // idle 90 s (gameplay context, no movement input)
            if (!_idleBarked && ShiftersInput.Context == InputContext.Gameplay && LevelManager.Instance != null && !LevelManager.Instance.IsCompleted)
            {
                if (ShiftersInput.Move.sqrMagnitude < 0.01f && !ShiftersInput.JumpHeld) _idleT += dt; else _idleT = 0f;
                if (_idleT >= IdleSeconds && TryBark("idle")) _idleBarked = true;
            }
        }

        bool TryCounter(string tutId)
        {
            if (string.IsNullOrEmpty(tutId) || _counterDone.Contains(tutId)) return true;
            if (!StoryText.CounterBarks.TryGetValue(tutId, out var text)) return true;
            if (LevelIndex == 0 && Array.IndexOf(StoryText.L1CounterBarkIds, tutId) < 0) return true;
            if (!TryBarkText("tutorial." + tutId, text, false)) return false;
            _counterDone.Add(tutId);
            return true;
        }

        // ------------------------------------------------------------------ events
        void OnLevelStarted()
        {
            BarksThisLevel = 0;
            _firstPuzzle = false; _checkpointBarked = false; _idleBarked = false; _idleT = 0f;
            _counterDone.Clear(); _switchTimes.Clear(); _pendingCounter = null; _tutVisible = false; _pendingExitAt = -1f;
            _levelStartAt = _clock;
            _lastBark = _clock - Cooldown + 12f; // first heckle no earlier than ~12 s in
            var gs = GameSession.Instance;
            _reprintPending = gs != null && gs.LastRecloned != null && gs.LastRecloned.Length > 0;
            if (_reprintPending) StoryText.CloneName = gs.LastRecloned[0];
        }

        void OnTutShow(TutorialMessage m)
        {
            _tutVisible = true;
            _tutShownAt = _clock;
            _tutId = m != null ? m.Id : null;
            if (_tutId != null && StoryText.CounterBarks.ContainsKey(_tutId) && !_counterDone.Contains(_tutId)) _pendingCounter = _tutId;
        }

        void OnTutHide() { _tutVisible = false; }

        void OnPuzzle(string msg)
        {
            if (_firstPuzzle) return;
            if (TryBark("puzzle")) _firstPuzzle = true;
        }

        void OnSwitched(ShifterCharacter from, ShifterCharacter to)
        {
            _switchTimes.Add(_clock);
            while (_switchTimes.Count > 0 && _clock - _switchTimes[0] > 30f) _switchTimes.RemoveAt(0);
            if (_switchTimes.Count >= 5 && TryBark("switching")) _switchTimes.Clear();
        }

        void OnState(ShifterCharacter c)
        {
            if (c == null || c.IsRestoringState) return;
            switch (c.State)
            {
                case CharacterState.Lost: TryBark("lost", true); break;
                case CharacterState.Captured: TryBark("captured"); break;
                case CharacterState.Alive:
                    if (c.PreviousState == CharacterState.Captured || c.PreviousState == CharacterState.Downed) TryBark("rescued");
                    break;
            }
        }

        void OnCondition(ShifterCharacter c)
        {
            if (c == null) return;
            if (c.Condition == CharacterCondition.Flopped) TryBark("flop");
            else if (c.Condition == CharacterCondition.Thrown) TryBark("thrown");
        }

        void OnWiped() { TryBark("wiped", true); }

        void OnCompleted(LevelResult r)
        {
            _pendingExitKey = r.LeftBehind != null && r.LeftBehind.Length > 0 ? "leftBehind" : "exitAll";
            _pendingExitAt = _clock + 0.4f;   // the campaign's outro (if any) starts first and wins
        }

        void OnCheckpoint(int n)
        {
            if (!Active) return;
            var sp = StoryPlayer.Instance;
            int li = LevelIndex;
            if (sp != null && li == 0 && n == 5 && StoryScripts.Exists("l1.percy")) { sp.Play("l1.percy"); return; }
            if (sp != null && li == 1 && n == 1 && StoryScripts.Exists("c02.percy")) { sp.Play("c02.percy"); return; }
            if (!_checkpointBarked && n >= 2 && TryBark("checkpoint")) _checkpointBarked = true;
        }

        void OnBeat(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id.StartsWith("bark.", StringComparison.OrdinalIgnoreCase))
            {
                bool high = id.EndsWith("!");
                var key = id.Substring(5).TrimEnd('!');
                TryBark(key, high);
                return;
            }
            // any scripted story beat counts as a Max appearance: push the next heckle back
            if (StoryScripts.Exists(id)) _lastBark = Mathf.Max(_lastBark, _clock - Cooldown * 0.5f);
        }
    }
}
