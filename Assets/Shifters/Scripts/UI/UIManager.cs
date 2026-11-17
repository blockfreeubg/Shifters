// =====================================================================================
//  SHIFTERS - UIManager.cs   [CONTRACT API - implementation owned by the UI agent]
//  Persistent (created by GameBootstrap). Builds every screen at runtime with uGUI + TextMeshPro:
//  main menu, team reveal, level select, settings, credits, HUD, switcher wheel, pause, tutorial
//  popups, banners, toasts, speech bubbles, level complete, team wiped. Listens to GameEvents.
//  Owns an EventSystem with InputSystemUIInputModule (NOT StandaloneInputModule).
//
//  Canvases (CanvasScaler 1920x1080, match 0.5): HUD 10, menus 20, switcher 30, popups 40
//  (banners/toasts), ScreenFader 100 (own canvas, see ScreenFader.cs).
//  Menus are a stack of UIScreens (Push/Pop/ReplaceAll). AnyMenuOpen == stack not empty.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; PendingCampaignEnd = false; }

        /// <summary>True when any blocking menu (pause, settings, level complete...) is open.</summary>
        public bool AnyMenuOpen { get; protected set; }

        // ------------------------------------------------------------------ canvases & roots
        public Canvas HudCanvas { get; private set; }
        public Canvas MenuCanvas { get; private set; }
        public Canvas SwitcherCanvas { get; private set; }
        public Canvas PopupCanvas { get; private set; }
        public EventSystem Events { get; private set; }

        public HudRoot Hud { get; private set; }
        public SwitcherWheelUI Wheel { get; private set; }
        public HudBanners Banners { get; private set; }
        public HudToasts Toasts { get; private set; }

        // ------------------------------------------------------------------ screens
        public MainMenuScreen MainMenu { get; private set; }
        public TeamRevealScreen TeamReveal { get; private set; }
        public LevelSelectScreen LevelSelect { get; private set; }
        public SettingsScreen Settings { get; private set; }
        public CreditsScreen Credits { get; private set; }
        public PauseScreen Pause { get; private set; }
        public LevelCompleteScreen LevelComplete { get; private set; }
        public TeamWipedScreen TeamWipedScreen { get; private set; }
        public VictoryScreen Victory { get; private set; }
        // campaign wave (§15)
        public ShiftScheduleScreen ShiftSchedule { get; private set; }
        public FreePlayScreen FreePlay { get; private set; }
        public ConfirmScreen Confirm { get; private set; }
        public CaseFileScreen CaseFile { get; private set; }
        public CampaignEndScreen CampaignEnd { get; private set; }

        /// <summary>Set (by the campaign after CH14, or RequestCampaignEnd) to open the campaign end screen with the next main menu.</summary>
        public static bool PendingCampaignEnd;
        /// <summary>True while UIManager.SetHudSuppressed(true) hides the gameplay HUD (cutscenes / letterbox).</summary>
        public bool HudSuppressed { get; private set; }

        /// <summary>True while the in-game HUD is the base layer (level scenes).</summary>
        public bool HudVisible { get; private set; }
        /// <summary>True while the pause menu (or a screen opened from it) is up and time is paused by the UI.</summary>
        public bool IsPaused { get; private set; }
        public UIScreen Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public int StackCount => _stack.Count;

        readonly List<UIScreen> _stack = new List<UIScreen>();
        float _lastStackChange;
        bool _built;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildRoots();
        }

        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        protected virtual void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.TeamWiped += OnTeamWiped;
        }

        protected virtual void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.TeamWiped -= OnTeamWiped;
        }

        void BuildRoots()
        {
            if (_built) return;
            _built = true;
            EnsureEventSystem();
            HudCanvas = UIFactory.Canvas("HUD Canvas", transform, 10, false);
            MenuCanvas = UIFactory.Canvas("Menu Canvas", transform, 20, true);
            SwitcherCanvas = UIFactory.Canvas("Switcher Canvas", transform, 30, false);
            PopupCanvas = UIFactory.Canvas("Popup Canvas", transform, 40, false);

            Hud = HudRoot.Create(HudCanvas.transform);
            HudCanvas.enabled = false;
            Wheel = SwitcherWheelUI.Create(SwitcherCanvas.transform);
            Banners = HudBanners.Create(PopupCanvas.transform);
            Toasts = HudToasts.Create(PopupCanvas.transform);

            var m = MenuCanvas.transform;
            MainMenu = UIScreen.Create<MainMenuScreen>(m, "MainMenu");
            TeamReveal = UIScreen.Create<TeamRevealScreen>(m, "TeamReveal");
            LevelSelect = UIScreen.Create<LevelSelectScreen>(m, "LevelSelect");
            Settings = UIScreen.Create<SettingsScreen>(m, "Settings");
            Credits = UIScreen.Create<CreditsScreen>(m, "Credits");
            Pause = UIScreen.Create<PauseScreen>(m, "Pause");
            LevelComplete = UIScreen.Create<LevelCompleteScreen>(m, "LevelComplete");
            TeamWipedScreen = UIScreen.Create<TeamWipedScreen>(m, "TeamWiped");
            Victory = UIScreen.Create<VictoryScreen>(m, "Victory");
            ShiftSchedule = UIScreen.Create<ShiftScheduleScreen>(m, "ShiftSchedule");
            FreePlay = UIScreen.Create<FreePlayScreen>(m, "FreePlay");
            CaseFile = UIScreen.Create<CaseFileScreen>(m, "CaseFile");
            CampaignEnd = UIScreen.Create<CampaignEndScreen>(m, "CampaignEnd");
            Confirm = UIScreen.Create<ConfirmScreen>(m, "Confirm");
        }

        /// <summary>Creates the single persistent EventSystem (InputSystemUIInputModule) if none is ours yet.</summary>
        public void EnsureEventSystem()
        {
            if (Events != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            Events = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            DisableForeignEventSystems();
        }

        void DisableForeignEventSystems()
        {
            foreach (var es in FindObjectsByType<EventSystem>())
            {
                if (es == null || es == Events || !es.enabled) continue;
                Debug.LogWarning("[UI] Disabling extra EventSystem '" + es.name + "' (UIManager owns the only one).");
                foreach (var mod in es.GetComponents<BaseInputModule>()) mod.enabled = false;
                es.enabled = false;
            }
            if (Events != null && !Events.enabled) Events.enabled = true;
        }

        // ------------------------------------------------------------------ scene flow
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            DisableForeignEventSystems();
            if (IsPaused) SetPausedInternal(false);
            CloseAll(true);
            LevelCompleteHold.Release();
            LevelCompleteExtras.Clear();
            SetHudSuppressed(false);
            Hud?.ResetState();
            Wheel?.ForceClose();
            if (scene.name == LevelCatalog.MainMenuScene) ShowMainMenu();
            else if (LevelCatalog.IndexOfScene(scene.name) >= 0) ShowGameplayHUD();
        }

        public virtual void ShowMainMenu()
        {
            BuildRoots();
            if (IsPaused) SetPausedInternal(false);
            HudVisible = false;
            if (HudCanvas != null) HudCanvas.enabled = false;
            Wheel?.ForceClose();
            CloseAll(true);
            MenuStage.Ensure(SessionActions.Team);
            AudioManager.Instance?.PlayMusic(MusicId.Menu);
            PostFx.SetTheme(LevelTheme.Menu);
            Push(MainMenu);
            bool directorEnd = CampaignLink.ConsumeCampaignEnd(out bool placeholderEnd);
            if (directorEnd) CampaignEnd.Placeholder = placeholderEnd;
            if (PendingCampaignEnd || directorEnd)
            {
                PendingCampaignEnd = false;
                SessionActions.ShowVictory = false;
                Push(CampaignEnd);
            }
            else if (SessionActions.ShowVictory)
            {
                SessionActions.ShowVictory = false;
                Push(Victory);
            }
            ShiftersInput.Context = InputContext.Menu;
        }

        public virtual void ShowGameplayHUD()
        {
            BuildRoots();
            if (IsPaused) SetPausedInternal(false);
            CloseAll(true);
            MenuStage.DestroyStage();
            HudVisible = true;
            if (HudCanvas != null) HudCanvas.enabled = true;
            Hud?.OnShown();
            if (ShiftersInput.Context == InputContext.Menu) ShiftersInput.Context = InputContext.Gameplay;
        }

        public virtual void OpenPause()
        {
            if (IsPaused || !HudVisible) return;
            if (CharacterSwitcher.Instance != null && CharacterSwitcher.Instance.IsOpen) CharacterSwitcher.Instance.Close(false);
            SetPausedInternal(true);
            Push(Pause);
        }

        public virtual void ClosePause()
        {
            if (!IsPaused) return;
            CloseAll(false);
            SetPausedInternal(false);
            if (HudVisible) ShiftersInput.Context = InputContext.Gameplay;
        }

        void SetPausedInternal(bool p)
        {
            IsPaused = p;
            TimeController.SetPaused(p);   // TimeController is the single source of GameEvents.PauseChanged
        }

        // ------------------------------------------------------------------ screen stack
        /// <summary>Open a screen on top of the current one (the current one is hidden until popped).</summary>
        public void Push(UIScreen s)
        {
            if (s == null) return;
            if (Top == s) { s.SelectDefault(); return; }
            _stack.Remove(s);
            Top?.Hide();
            _stack.Add(s);
            s.Show();
            OnStackChanged();
        }

        /// <summary>Close the top screen and return to the previous one.</summary>
        public void Pop()
        {
            if (_stack.Count == 0) return;
            var top = Top;
            _stack.RemoveAt(_stack.Count - 1);
            top.Hide();
            if (_stack.Count == 0 && IsPaused) { SetPausedInternal(false); }
            Top?.Show();
            OnStackChanged();
        }

        /// <summary>Close everything and open s as the only screen.</summary>
        public void ReplaceAll(UIScreen s) { CloseAll(false); Push(s); }

        public void CloseAll(bool instant = false)
        {
            for (int i = _stack.Count - 1; i >= 0; i--) if (_stack[i] != null) _stack[i].Hide(instant);
            _stack.Clear();
            OnStackChanged();
        }

        void OnStackChanged()
        {
            _lastStackChange = UITime.Now;
            AnyMenuOpen = _stack.Count > 0;
            if (AnyMenuOpen) ShiftersInput.Context = InputContext.Menu;
            else if (HudVisible && ShiftersInput.Context == InputContext.Menu) ShiftersInput.Context = InputContext.Gameplay;
            if (Hud != null) Hud.SetMenuOpen(AnyMenuOpen);
        }

        public void OpenSettings() => Push(Settings);
        public void OpenCredits() => Push(Credits);

        /// <summary>Level complete screen (also raised automatically on GameEvents.LevelCompleted).</summary>
        public void ShowLevelComplete(LevelResult result)
        {
            if (IsPaused) SetPausedInternal(false);
            Wheel?.ForceClose();
            LevelComplete.SetResult(result);
            ReplaceAll(LevelComplete);
        }

        public void ShowTeamWiped()
        {
            if (IsPaused) SetPausedInternal(false);
            Wheel?.ForceClose();
            ReplaceAll(TeamWipedScreen);
        }

        public void ShowVictory() => Push(Victory);

        // ------------------------------------------------------------------ campaign wave hooks (§15)
        /// <summary>Hide / show the gameplay HUD (letterbox, blocking cutscenes). Widgets that ui_hud parents to HudCanvas stay visible.</summary>
        public void SetHudSuppressed(bool suppressed)
        {
            HudSuppressed = suppressed;
            Hud?.SetSuppressed(suppressed);
        }

        /// <summary>Open SHIFT SCHEDULE (chapter select).</summary>
        public void OpenShiftSchedule() => Push(ShiftSchedule);
        /// <summary>Open the FREE PLAY grid.</summary>
        public void OpenFreePlay() => Push(FreePlay);
        /// <summary>Open the CASE FILE (pause / menus).</summary>
        public void OpenCaseFile() => Push(CaseFile);

        /// <summary>Campaign end ("THE GREAT SHIFT - COMPLETE" + HR FILE). placeholder = R0 card "MAX REMAINS IN POWER... FOR NOW.".
        /// In a level scene it is queued for the next main menu (PendingCampaignEnd); in the menu it opens now.</summary>
        public void ShowCampaignEnd(bool placeholder = false)
        {
            CampaignEnd.Placeholder = placeholder;
            if (HudVisible) { PendingCampaignEnd = true; return; }
            if (IsPaused) SetPausedInternal(false);
            Push(CampaignEnd);
        }

        /// <summary>Static form for modules without a UIManager reference: queue/open the campaign end screen.</summary>
        public static void RequestCampaignEnd(bool placeholder = false)
        {
            if (Instance != null) Instance.ShowCampaignEnd(placeholder);
            else PendingCampaignEnd = true;
        }

        /// <summary>Modal yes/no (keyboard: left/right, Enter, Esc = no).</summary>
        public void AskConfirm(string title, string body, string yes, string no, System.Action onYes, System.Action onNo = null, bool defaultYes = true)
        {
            Confirm.Setup(title, body, yes, no, onYes, onNo, defaultYes);
            Push(Confirm);
        }

        void OnLevelCompleted(LevelResult r) => QueueLevelComplete(r);

        /// <summary>What GameEvents.LevelCompleted does: wait 0.9 s, then while LevelCompleteHold is active (<= 3 s), then open Level Complete.</summary>
        public void QueueLevelComplete(LevelResult r) => UITween.Delay(0.9f, () => ShowLevelCompleteWhenFree(r, 0), this);

        void ShowLevelCompleteWhenFree(LevelResult r, int waited)
        {
            if (this == null) return;
            if (LevelCompleteHold.Active && waited < 40) { UITween.Delay(0.1f, () => ShowLevelCompleteWhenFree(r, waited + 1), this); return; }
            LevelCompleteHold.Release();
            ShowLevelComplete(r);
        }
        void OnTeamWiped() => UITween.Delay(0.4f, () => { if (this != null && HudVisible) ShowTeamWiped(); }, this);

        // ------------------------------------------------------------------ input
        protected virtual void Update()
        {
            if (_stack.Count == 0)
            {
                bool switcherOpen = CharacterSwitcher.Instance != null && CharacterSwitcher.Instance.IsOpen;
                if (HudVisible && !switcherOpen && ShiftersInput.Context == InputContext.Gameplay && ShiftersInput.PausePressed) OpenPause();
                return;
            }
            if (UITime.Now - _lastStackChange > 0.08f && ShiftersInput.CancelPressed)
            {
                Top?.OnBack();
                return;
            }
            var es = Events != null ? Events : EventSystem.current;
            if (es != null)
            {
                var sel = es.currentSelectedGameObject;
                if ((sel == null || !sel.activeInHierarchy) && ShiftersInput.NavigateStick.sqrMagnitude > 0.25f) Top?.SelectDefault();
            }
        }
    }
}
