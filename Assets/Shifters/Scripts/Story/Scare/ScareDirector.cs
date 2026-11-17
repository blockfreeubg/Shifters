// =====================================================================================
//  SHIFTERS - ScareDirector.cs   [owned by STORY_KIT]
//  Enforces CAMPAIGN §10.1 for every ScareKit cue (and anything else that asks):
//    CanPlay(cueId) / TryPlay(cueId)  - once per attempt (seen ids survive checkpoint restarts), <= 1 per 30 s,
//                                       <= 6 per chapter, never during a blocking window;
//    automatic windows                - timed doors (GameEvents.TimerChanged > 0), captured interns, recent damage
//                                       (hazard, 3 s), boss fights (BossChanged Visible+Fighting), chases (ChaseChanged
//                                       Visible), blocking cutscenes (CutsceneChanged);
//    SetWindow(id, on) / BlockFor(s)  - level-specific hazards / timed sections;
//    on a successful TryPlay          - the stalkers are frozen for 2 s (StalkerBrain.FreezeAll) and
//                                       StoryBeat "scare.<cueId>" is raised;
//    Mild                             - SettingsData.SpookyMild (MildOverride for tests): ScareKit halves flickers,
//                                       swaps painted smiles for stickers, drops breathing/whispers, keeps lights 30%
//                                       brighter, never punches the camera in.
//  The singleton auto-creates on first use (no scene setup needed); state lives in statics keyed to the attempt.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class ScareDirector : MonoBehaviour
    {
        static ScareDirector _instance;
        static readonly ScareRules _rules = new ScareRules();
        static int _attemptLevel = int.MinValue;
        static bool _quitting;

        /// <summary>Test / settings override for the Spooky setting (null = SettingsData.SpookyMild).</summary>
        public static bool? MildOverride;
        /// <summary>Last refusal reason of CanPlay / TryPlay (for logs and tests).</summary>
        public static ScareVerdict LastVerdict { get; private set; }
        public static string LastCue { get; private set; }
        public static int StalkersFrozenLast { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null; _rules.Reset(); _rules.Enabled = true; _attemptLevel = int.MinValue; _quitting = false;
            MildOverride = null; LastVerdict = ScareVerdict.Ok; LastCue = null; StalkersFrozenLast = 0;
        }

        public static ScareRules Rules => _rules;

        public static bool Mild
        {
            get
            {
                if (MildOverride.HasValue) return MildOverride.Value;
                var gs = GameSession.Instance;
                return gs != null && gs.Settings != null && gs.Settings.SpookyMild;
            }
        }

        public static ScareDirector Ensure()
        {
            if (_instance != null || _quitting || !Application.isPlaying) return _instance;
            _instance = new GameObject("ScareDirector").AddComponent<ScareDirector>();
            return _instance;
        }

        static void SyncAttempt()
        {
            int lvl = LevelAttempt.LevelIndex;
            if (lvl == _attemptLevel) return;
            _attemptLevel = lvl;
            _rules.Reset();
        }

        /// <summary>CAMPAIGN §18.2 contract: may this cue play right now? (no side effects)</summary>
        public static bool CanPlay(string cueId)
        {
            Ensure();
            SyncAttempt();
            var v = _rules.Check(cueId, Time.time);
            LastVerdict = v; LastCue = cueId;
            return v == ScareVerdict.Ok;
        }

        /// <summary>CanPlay + commit (seen, counted, cooldown), freezes stalkers 2 s and raises StoryBeat "scare.&lt;id&gt;".</summary>
        public static bool TryPlay(string cueId)
        {
            Ensure();
            SyncAttempt();
            var v = _rules.TryBegin(cueId, Time.time);
            LastVerdict = v; LastCue = cueId;
            if (v != ScareVerdict.Ok) { Debug.Log("[ScareDirector] '" + cueId + "' skipped: " + v); return false; }
            StalkersFrozenLast = FreezeStalkers(ScareRules.StalkerFreezeSeconds);
            GameEvents.RaiseStoryBeat("scare." + cueId);
            return true;
        }

        public static bool HasSeen(string cueId) { SyncAttempt(); return _rules.HasSeen(cueId); }
        public static void SetWindow(string id, bool on) { SyncAttempt(); _rules.SetWindow(id, on); }
        public static void BlockFor(float seconds, string id = "block") { SyncAttempt(); _rules.BlockUntil(id, Time.time + Mathf.Max(0f, seconds)); }
        public static bool IsBlocked(out string by) { SyncAttempt(); return _rules.IsBlocked(Time.time, out by); }
        public static int CountThisAttempt { get { SyncAttempt(); return _rules.Count; } }
        /// <summary>Forget everything (new attempt). Normally automatic.</summary>
        public static void ResetAttempt() { _rules.Reset(); _attemptLevel = LevelAttempt.LevelIndex; }

        static readonly List<IFreezable> _freezables = new List<IFreezable>();
        /// <summary>Freezes the stalkers (StalkerBrain.FreezeAll when enemies has it, else every IFreezable whose type
        /// name contains "Stalker"); security bots / cameras are NOT frozen. Returns the number frozen (-1 = FreezeAll).</summary>
        public static int FreezeStalkers(float seconds)
        {
            if (StoryKitLate.TryFreezeAllStalkers(seconds)) return -1;
            _freezables.Clear();
            foreach (var mb in FindObjectsByType<MonoBehaviour>())
                if (mb is IFreezable f && mb.isActiveAndEnabled && mb.GetType().Name.IndexOf("Stalker", System.StringComparison.Ordinal) >= 0) _freezables.Add(f);
            for (int i = 0; i < _freezables.Count; i++)
            {
                try { _freezables[i].Freeze(seconds); } catch (System.Exception e) { Debug.LogWarning("[ScareDirector] freeze: " + e.Message); }
            }
            int n = _freezables.Count;
            _freezables.Clear();
            return n;
        }

        // ---------------------------------------------------------------- automatic windows
        float _poll;

        void OnEnable()
        {
            GameEvents.TimerChanged += OnTimer;
            GameEvents.CharacterDamaged += OnDamaged;
            GameEvents.BossChanged += OnBoss;
            GameEvents.ChaseChanged += OnChase;
            GameEvents.CutsceneChanged += OnCutscene;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void OnDisable()
        {
            GameEvents.TimerChanged -= OnTimer;
            GameEvents.CharacterDamaged -= OnDamaged;
            GameEvents.BossChanged -= OnBoss;
            GameEvents.ChaseChanged -= OnChase;
            GameEvents.CutsceneChanged -= OnCutscene;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
        void OnApplicationQuit() { _quitting = true; }

        static void OnSceneChanged(UnityEngine.SceneManagement.Scene a, UnityEngine.SceneManagement.Scene b) { _rules.ClearWindows(); }
        static void OnTimer(float s) => _rules.SetWindow("timer", s > 0f);
        static void OnDamaged(ShifterCharacter c, float amount) { if (amount > 0f) _rules.BlockUntil("hazard", Time.time + 3f); }
        static void OnBoss(BossStatus b) => _rules.SetWindow("boss", b.Visible && b.State != BossState.Defeated && b.State != BossState.Dormant);
        static void OnChase(ChaseStatus c) => _rules.SetWindow("chase", c.Visible && !c.Busted);
        static void OnCutscene(bool blocking) => _rules.SetWindow("cutscene", blocking);

        void Update()
        {
            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0f) return;
            _poll = 0.25f;
            SyncAttempt();
            bool captured = false;
            var cm = CharacterManager.Instance;
            if (cm != null && cm.Team != null)
                for (int i = 0; i < cm.Team.Count; i++)
                {
                    var c = cm.Team[i];
                    if (c != null && (c.State == CharacterState.Captured || c.State == CharacterState.Downed)) { captured = true; break; }
                }
            _rules.SetWindow("capture", captured);
        }
    }
}
