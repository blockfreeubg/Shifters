// =====================================================================================
//  SHIFTERS - TutorialManager.cs   [owned by the LEVEL agent]
//  Scene singleton (created by LevelManager). Queues TutorialMessages and drives the UI popup via
//  GameEvents.RaiseTutorialShow/Hide. Respects Settings.Tutorials. Each id is shown once per level
//  attempt (cleared when the level starts from the beginning, kept on checkpoint restarts).
//  Backspace (ShiftersInput.SkipTutorialPressed) hides the current message. Timers are unscaled.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        static readonly Dictionary<int, HashSet<string>> _seen = new Dictionary<int, HashSet<string>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; _seen.Clear(); }

        /// <summary>Gap between two messages so the popup can animate out/in.</summary>
        public const float Gap = 0.35f;
        /// <summary>A visible non-important message yields to a queued one after this long.</summary>
        public const float MinVisible = 2.5f;
        /// <summary>An important message yields to a queued one after this long.</summary>
        public const float ImportantMinVisible = 10f;

        public int LevelIndex = -1;
        public TutorialMessage Current { get; private set; }
        public int QueueCount => _queue.Count;
        /// <summary>Messages shown since this manager was created.</summary>
        public int ShownCount { get; private set; }

        readonly List<TutorialMessage> _queue = new List<TutorialMessage>();
        float _visibleFor, _gapLeft;

        public static bool TutorialsEnabled
        {
            get
            {
                var s = GameSession.Instance;
                return s == null || s.Settings == null || s.Settings.Tutorials;
            }
        }

        protected virtual void Awake() { Instance = this; }
        protected virtual void OnEnable() { GameEvents.SettingsChanged += OnSettingsChanged; }
        protected virtual void OnDisable() { GameEvents.SettingsChanged -= OnSettingsChanged; }
        protected virtual void OnDestroy()
        {
            if (Current != null) GameEvents.RaiseTutorialHide();
            if (Instance == this) Instance = null;
        }

        public static TutorialManager Ensure() => Instance != null ? Instance : new GameObject("TutorialManager").AddComponent<TutorialManager>();

        HashSet<string> SeenSet(bool create)
        {
            int key = LevelIndex >= 0 ? LevelIndex : (LevelManager.Instance != null ? LevelManager.Instance.LevelIndex : -1);
            if (!_seen.TryGetValue(key, out var set) && create) { set = new HashSet<string>(); _seen[key] = set; }
            return set;
        }

        public bool HasSeen(string id) { var s = SeenSet(false); return s != null && !string.IsNullOrEmpty(id) && s.Contains(id); }

        /// <summary>Queue a message. Returns false when suppressed (tutorials off, already seen, already queued/showing).</summary>
        public bool Show(TutorialMessage m)
        {
            if (m == null || !TutorialsEnabled) return false;
            if (HasSeen(m.Id)) return false;
            if (Current != null && Current.Id == m.Id) return false;
            for (int i = 0; i < _queue.Count; i++) if (_queue[i].Id == m.Id) return false;
            _queue.Add(m);
            if (Current == null && _gapLeft <= 0f) ShowNext();
            return true;
        }

        /// <summary>Hide the current message (skip). The next queued one follows after a short gap.</summary>
        public void Hide()
        {
            if (Current == null) return;
            Current = null;
            _gapLeft = Gap;
            GameEvents.RaiseTutorialHide();
        }

        /// <summary>Clear the queue and hide everything.</summary>
        public void Clear() { _queue.Clear(); Hide(); }

        void ShowNext()
        {
            if (_queue.Count == 0) return;
            var m = _queue[0];
            _queue.RemoveAt(0);
            Current = m;
            _visibleFor = 0f;
            ShownCount++;
            if (!string.IsNullOrEmpty(m.Id)) SeenSet(true).Add(m.Id);
            Sfx.Play(SfxId.TutorialPop, 0.7f);
            GameEvents.RaiseTutorialShow(m);
        }

        protected virtual void Update()
        {
            float dt = PerspectiveStyle.UiDelta;
            if (Current != null)
            {
                if (ShiftersInput.SkipTutorialPressed) { Hide(); return; }
                _visibleFor += dt;
                bool timedOut = !Current.Important && Current.Duration > 0f && _visibleFor >= Current.Duration;
                bool yield = _queue.Count > 0 && _visibleFor >= (Current.Important ? ImportantMinVisible : MinVisible);
                if (timedOut || yield) Hide();
            }
            else if (_queue.Count > 0)
            {
                _gapLeft -= dt;
                if (_gapLeft <= 0f) ShowNext();
            }
            else if (_gapLeft > 0f) _gapLeft -= dt;
        }

        void OnSettingsChanged() { if (!TutorialsEnabled) Clear(); }

        /// <summary>Forget which tutorials were shown for a level (fresh start of that level).</summary>
        public static void ClearSeen(int levelIndex) { if (_seen.TryGetValue(levelIndex, out var s)) s.Clear(); }

        /// <summary>Pause menu "Restart Tutorial": clear seen ids, enable tutorials, load Level 1.</summary>
        public static void RestartTutorial()
        {
            foreach (var kv in _seen) kv.Value.Clear();
            LevelAttempt.Close();
            var s = GameSession.Instance;
            if (s == null) return;
            if (s.Settings != null) s.Settings.Tutorials = true;
            s.SaveSettings();
            s.ApplySettings();
            s.LoadLevel(0);
        }
    }
}
