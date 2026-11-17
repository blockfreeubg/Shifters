// =====================================================================================
//  SHIFTERS - LevelManager.cs   [owned by the LEVEL agent]
//  The scene component that turns a level scene into a playable level. A level scene needs:
//    * one LevelManager (LevelIndex + 3 SpawnPoints; use LevelKit.LevelManager / LevelKit.SpawnPoints),
//    * Checkpoints with ascending indices (LevelKit.Checkpoint), one LevelExit (LevelKit.Exit),
//    * optional TutorialTriggers / HintZones (LevelKit.Tutorial / LevelKit.HintZone).
//  At Start it creates (as children) CharacterManager, CharacterSwitcher, TutorialManager, HintSystem and
//  CheckpointManager, ensures a main Camera with ShiftersCamera, applies LevelLighting (only if the scene
//  has no directional light) + PostFx theme, spawns the team (start spawns or the saved checkpoint, applying
//  the CheckpointSnapshot), shows the HUD, plays music, raises objective + intro banner, fades in and
//  raises GameEvents.LevelStarted. Tracks level time / switches for the LevelResult.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        /// <summary>Optional override for restoring captives. Called after spawning for every character that was Captured
        /// in the restored snapshot (character, cellId); return true when handled. When unassigned (or it returns false)
        /// LevelManager does RestoreState(Captured) + HoldingCell.Find(cellId).Imprison(c) itself; with no such cell the
        /// character restores as Downed (revived at the next checkpoint).</summary>
        public static Func<ShifterCharacter, string, bool> CaptureRestoreHook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; CaptureRestoreHook = null; CampaignIntroHook = null; }

        /// <summary>Campaign seam: invoked in Start (after the team spawned, before the HUD, music, objective and intro
        /// banner) so the campaign can set IntroTitle / IntroSubtitle / ObjectiveOverride / ShowIntroBanner / PlayMusic.
        /// Null = classic behaviour. Exceptions are logged and ignored.</summary>
        public static Action<LevelManager> CampaignIntroHook;

        [Header("Level")]
        [Tooltip("Index into LevelCatalog.Levels.")] public int LevelIndex;
        [Tooltip("Start spawn points (slot 0..2). Missing entries are generated in a row next to the first one.")]
        public Transform[] SpawnPoints = new Transform[3];
        [TextArea] public string ObjectiveOverride = "";
        [Tooltip("Empty = LevelCatalog title/subtitle.")] public string IntroTitle = "", IntroSubtitle = "";

        [Header("Options")]
        [Tooltip("Apply LevelLighting at runtime even if the scene already has a directional light.")] public bool ForceRuntimeLighting;
        public bool PlayMusic = true;
        public bool ShowIntroBanner = true;
        public float FadeInSeconds = 0.6f;
        [Tooltip("Slot that starts active on a fresh start.")] public int StartActiveSlot;

        public LevelInfo Info => LevelCatalog.Get(LevelIndex) ?? (_fallbackInfo ??= new LevelInfo(LevelIndex, gameObject.scene.name, "TEST CHAMBER", "Unlisted Facility",
            "", Kit.Theme, MusicId.Training, "Reach the exit."));
        public LevelTheme Theme => Info.Theme;
        /// <summary>Gameplay seconds in this attempt (continues across checkpoint restarts).</summary>
        public float ElapsedTime { get; private set; }
        /// <summary>Character switches in this attempt.</summary>
        public int SwitchCount { get; private set; }
        /// <summary>Restarts of this level (GameSession.LevelRestarts; LevelAttempt's reload count without a session).</summary>
        public int RestartCount { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool StartedFromCheckpoint { get; private set; }
        public string Objective { get; private set; } = "";
        public CharacterManager Characters { get; private set; }
        public TeamData Team { get; private set; }

        LevelInfo _fallbackInfo;

        static readonly string[] RestoreQuips =
        {
            "Restored from backup. Mostly.", "Let's pretend that didn't happen.", "HR has been notified. Again.",
            "Previously, on SHIFTERS...", "Time is a flat circle. So is the floor.", "Insurance claim #4417 filed.",
            "Same shift. Fewer excuses.", "Re-clocking you in...",
        };

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) Debug.LogWarning("[SHIFTERS] Two LevelManagers in the scene - using the newest.");
            Instance = this;
        }

        protected virtual void Start() => Begin();

        protected virtual void OnDestroy()
        {
            GameEvents.CharacterSwitched -= OnSwitched;
            if (IsStarted && !IsCompleted) LevelAttempt.Bank(LevelIndex, ElapsedTime, SwitchCount);
            if (Instance == this) Instance = null;
        }

        protected virtual void Update()
        {
            if (IsStarted && !IsCompleted) ElapsedTime += Time.deltaTime;
        }

        // ------------------------------------------------------------------ public API
        public void SetObjective(string text)
        {
            Objective = text ?? "";
            GameEvents.RaiseObjective(Objective);
        }

        public void RestartCheckpoint() { if (GameSession.Instance != null) GameSession.Instance.RestartFromCheckpoint(); }
        public void RestartLevel() { if (GameSession.Instance != null) GameSession.Instance.RestartLevel(); }

        /// <summary>LevelExit: stop the clock and close the attempt.</summary>
        public void MarkCompleted()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            LevelAttempt.Close();
        }

        public LevelResult BuildResult(int survivors, string[] leftBehind) => new LevelResult
        {
            LevelIndex = LevelIndex,
            Time = ElapsedTime,
            Survivors = survivors,
            LeftBehind = leftBehind ?? new string[0],
            Switches = SwitchCount,
            Restarts = RestartCount,
        };

        /// <summary>Start spawn poses (slot order).</summary>
        public List<Pose> StartPoses()
        {
            var list = new List<Pose>(3);
            Transform first = null;
            for (int i = 0; i < SpawnPoints.Length; i++) if (SpawnPoints[i] != null) { first = SpawnPoints[i]; break; }
            var basePose = first != null ? new Pose(first.position, first.rotation) : new Pose(transform.position, transform.rotation);
            for (int i = 0; i < 3; i++)
            {
                var t = i < SpawnPoints.Length ? SpawnPoints[i] : null;
                list.Add(t != null ? new Pose(t.position, t.rotation)
                                   : new Pose(basePose.position + basePose.rotation * Vector3.right * ((i - 1) * LevelKit.SpawnSpacing), basePose.rotation));
            }
            return list;
        }

        // ------------------------------------------------------------------ startup
        void Begin()
        {
            var session = GameSession.Instance;
            if (session != null) { session.EnsureTeam(); Team = session.Team; }
            if (Team == null) Team = CharacterGenerator.GenerateTeam(Environment.TickCount & 0x7FFFFFFF | 1);
            NotifySession(session);

            var snap = session != null ? session.Snapshot : null;
            if (snap != null && (snap.CheckpointIndex < 0 || CheckpointManager.Find(snap.CheckpointIndex) == null)) snap = null;
            StartedFromCheckpoint = snap != null;
            LevelAttempt.Begin(LevelIndex, StartedFromCheckpoint, out float t, out int sw);
            ElapsedTime = t;
            SwitchCount = sw;
            // GameSession is the authority (counts RestartLevel/RestartFromCheckpoint, reset by a fresh LoadLevel);
            // LevelAttempt's reload count is only the fallback when there is no session.
            RestartCount = session != null ? session.LevelRestarts : LevelAttempt.Restarts;
            if (!StartedFromCheckpoint) TutorialManager.ClearSeen(LevelIndex);

            var theme = Theme;
            Kit.Theme = theme;
            if (ForceRuntimeLighting || !SceneHasSun()) LevelLighting.Apply(theme);
            PostFx.SetTheme(theme);
            EnsureCamera();
            EnsureSystems();

            // ---- team
            ApplyWeaponOwner(snap);
            var poses = StartedFromCheckpoint ? CheckpointManager.Find(snap.CheckpointIndex).GetSpawnPoses() : StartPoses();
            CheckpointManager.SetItemFallback(poses[1].position); // weapons: a gun lost in the void reappears here
            int activeSlot = PickActiveSlot(snap);
            if (Characters.Team.Count == 0) Characters.SpawnTeam(Team, poses, activeSlot);
            if (snap != null)
            {
                ApplySnapshotStates(snap);
                CheckpointManager.Instance.RestoreIndex(snap.CheckpointIndex);
            }
            if (Characters.Active == null || !Characters.Active.IsSelectable) Characters.SwitchToNextAvailable();

            var cam = ShiftersCamera.Instance;
            if (cam != null && Characters.Active != null) cam.SetTarget(Characters.Active, true);

            // ---- presentation
            if (CampaignIntroHook != null)
            {
                try { CampaignIntroHook(this); } catch (Exception e) { Debug.LogException(e); }
            }
            if (UIManager.Instance != null) UIManager.Instance.ShowGameplayHUD();
            if (PlayMusic && AudioManager.Instance != null) AudioManager.Instance.PlayMusic(Info.Music);
            SetObjective(string.IsNullOrEmpty(ObjectiveOverride) ? Info.DefaultObjective : ObjectiveOverride);
            if (ShowIntroBanner)
            {
                if (StartedFromCheckpoint)
                    GameEvents.RaiseBanner("CHECKPOINT " + Checkpoint.RankOf(snap.CheckpointIndex), RestoreQuips[UnityEngine.Random.Range(0, RestoreQuips.Length)], BannerStyle.Checkpoint);
                else
                    GameEvents.RaiseBanner(string.IsNullOrEmpty(IntroTitle) ? Info.Title : IntroTitle,
                                           string.IsNullOrEmpty(IntroSubtitle) ? Info.Subtitle : IntroSubtitle, BannerStyle.Info);
            }
            StartCoroutine(ScreenFader.FadeIn(FadeInSeconds));
            GameEvents.CharacterSwitched -= OnSwitched;
            GameEvents.CharacterSwitched += OnSwitched;
            IsStarted = true;
            GameEvents.RaiseLevelStarted();
        }

        void OnSwitched(ShifterCharacter prev, ShifterCharacter next) { if (prev != null && IsStarted && !IsCompleted) SwitchCount++; }

        void NotifySession(GameSession session)
        {
            // Tells the session which level this is (sets LevelIndex, Context = Gameplay; a different level clears the Snapshot).
            if (session == null) return;
            try { session.NotifyLevelLoaded(LevelIndex); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static bool SceneHasSun()
        {
            var lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].type == LightType.Directional && lights[i].isActiveAndEnabled) return true;
            return false;
        }

        void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cams = FindObjectsByType<Camera>();
                for (int i = 0; i < cams.Length; i++) if (cams[i].isActiveAndEnabled && cams[i].targetTexture == null) { cam = cams[i]; break; }
            }
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                cam.fieldOfView = Tuning.CamDefaultFov;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 600f;
            }
            if (cam.GetComponent<ShiftersCamera>() == null) cam.gameObject.AddComponent<ShiftersCamera>();
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        }

        void EnsureSystems()
        {
            Characters = CharacterManager.Instance != null ? CharacterManager.Instance : Child<CharacterManager>("CharacterManager");
            if (CharacterSwitcher.Instance == null) Child<CharacterSwitcher>("CharacterSwitcher");
            var tm = TutorialManager.Instance != null ? TutorialManager.Instance : Child<TutorialManager>("TutorialManager");
            tm.LevelIndex = LevelIndex;
            if (HintSystem.Instance == null) Child<HintSystem>("HintSystem");
            if (CheckpointManager.Instance == null) Child<CheckpointManager>("CheckpointManager");
        }

        T Child<T>(string n) where T : Component
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        int PickActiveSlot(CheckpointSnapshot snap)
        {
            if (snap == null) return Mathf.Clamp(StartActiveSlot, 0, 2);
            int s = Mathf.Clamp(snap.ActiveSlot, 0, 2);
            if (StateAt(snap, s) == CharacterState.Alive) return s;
            for (int i = 0; i < 3; i++) if (StateAt(snap, i) == CharacterState.Alive) return i;
            return 0;
        }

        static CharacterState StateAt(CheckpointSnapshot snap, int i) => snap.States != null && i < snap.States.Length ? snap.States[i] : CharacterState.Alive;

        void ApplyWeaponOwner(CheckpointSnapshot snap)
        {
            if (snap == null || Team == null || Team.Members == null) return;
            int owner = snap.WeaponOwnerSlot;
            if (owner < 0 || owner >= Team.Members.Length) return; // dropped at checkpoint time: the weapons module respawns its pickup
            WeaponData gun = null;
            for (int i = 0; i < Team.Members.Length; i++) if (Team.Members[i] != null && Team.Members[i].HasWeapon) { gun = Team.Members[i].Weapon; break; }
            if (gun == null) return;
            for (int i = 0; i < Team.Members.Length; i++) if (Team.Members[i] != null) Team.Members[i].Weapon = i == owner ? gun : null;
        }

        void ApplySnapshotStates(CheckpointSnapshot snap)
        {
            for (int i = 0; i < Characters.Team.Count; i++)
            {
                var c = Characters.Team[i];
                if (c == null) continue;
                var state = StateAt(snap, i);
                float hp = snap.Health != null && i < snap.Health.Length ? snap.Health[i] : Tuning.MaxHealth;
                string cell = snap.CapturedCellIds != null && i < snap.CapturedCellIds.Length ? snap.CapturedCellIds[i] : "";
                switch (state)
                {
                    case CharacterState.Alive:
                        if (hp < Tuning.MaxHealth - 0.01f) c.RestoreState(CharacterState.Alive, Mathf.Max(1f, hp), "");
                        break;
                    case CharacterState.Downed:
                        c.RestoreState(CharacterState.Downed, 0f, "");
                        break;
                    case CharacterState.Captured:
                        bool handled = false;
                        if (CaptureRestoreHook != null)
                        {
                            try { handled = CaptureRestoreHook(c, cell); }
                            catch (Exception e) { Debug.LogException(e); }
                        }
                        if (!handled) handled = RestoreCaptive(c, Mathf.Max(1f, hp), cell);
                        if (!handled) c.RestoreState(CharacterState.Downed, 0f, "");
                        break;
                    case CharacterState.Lost:
                        c.RestoreState(CharacterState.Lost, 0f, ""); // ShifterCharacter hides the body (IsHidden); stays in the team list
                        break;
                }
            }
        }

        /// <summary>Lock a restored captive back into its HoldingCell (enemies module). False if the cell doesn't exist.</summary>
        static bool RestoreCaptive(ShifterCharacter c, float hp, string cellId)
        {
            var cell = HoldingCell.Find(cellId);
            if (cell == null) return false;
            try
            {
                c.RestoreState(CharacterState.Captured, hp, cellId);
                cell.Imprison(c);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}
