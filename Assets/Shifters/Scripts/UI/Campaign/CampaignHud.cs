// =====================================================================================
//  SHIFTERS - CampaignHud.cs   (ui_hud agent)
//  Root of the in-chapter campaign UI (CAMPAIGN §15.2). Self-creates at AfterSceneLoad
//  (DontDestroyOnLoad) and listens only to GameEvents, so story / bosses / campaign /
//  levels never need a reference to it. Canvases (ScreenSpaceOverlay):
//    "Campaign HUD Canvas"   order 15  : letterbox, boss bar, chase meter, objective memo,
//                                        WHO RULES TODAY, DialogueBox, exhibit toast, skip chip
//                                        (above ui's HUD 10, below menus/pause 20, switcher 30)
//    "Campaign PiP Canvas"   order 35  : ME-TV / radio PiP (above Level Complete for reaction
//                                        lines; hidden while paused)
//    "Campaign Card Canvas"  order 101 : chapter card (rides on ScreenFader's 100)
//  Heist role stamps / SCARED badges are children of ui's HUD cards (HUD canvas).
//  Story beats it understands: hud.roles.on|off, hud.ruler.on|off, hud.pip.off, hud.reset.
//  Announcement order (polish H1/H2): a chapter/act card raised during live gameplay waits until the
//  centre banner and the boss bar's defeat stamp are done (<= CardMaxWait s); ui's banners are held while
//  a card is up and replay after it (except the level-intro banner: a card that was up during the hold drops it,
//  polish 1b - the chapter card is the title); ui's tutorial card docks to the left column while the boss bar /
//  chase meter is up and ducks while a centre banner shows.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    [DefaultExecutionOrder(60)]
    public class CampaignHud : MonoBehaviour
    {
        public const int MainOrder = 15, PipOrder = 35, CardOrder = 101;
        public const float TopY = -18f, TimerDrop = 128f, StackGap = 10f;
        /// <summary>Top edge of ui's HudTutorial card (HudTutorial.HomePos.y, same CanvasScaler reference as this canvas).</summary>
        public const float TutorialTop = -150f;

        /// <summary>Bottom edge (top-anchored y) of the HudTutorial card while it shows; false when no card is up.</summary>
        public static bool TutorialCardBottom(UIManager ui, out float bottom)
        {
            bottom = 0f;
            var t = ui != null && ui.Hud != null ? ui.Hud.Tutorial : null;
            if (t == null || !t.Visible || !t.isActiveAndEnabled || t.Docked) return false;
            bottom = TutorialTop - ((RectTransform)t.transform).rect.height;
            return true;
        }

        public static CampaignHud Instance { get; private set; }
        /// <summary>Set false before the first scene loads to stop the AfterSceneLoad self-creation (tests).</summary>
        public static bool AutoCreate = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; AutoCreate = true; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (AutoCreate) Ensure(); }

        /// <summary>The live instance (created on demand).</summary>
        public static CampaignHud Ensure()
        {
            if (Instance != null) return Instance;
            if (!Application.isPlaying) return null;
            var go = new GameObject("[CampaignHud]");
            DontDestroyOnLoad(go);
            return go.AddComponent<CampaignHud>();
        }

        public Canvas MainCanvas { get; private set; }
        public Canvas PipCanvas { get; private set; }
        public Canvas CardCanvas { get; private set; }

        public CampaignLetterbox Letterbox { get; private set; }
        public CampaignBossBar BossBar { get; private set; }
        public CampaignChaseMeter Chase { get; private set; }
        public CampaignObjectiveMemo Memo { get; private set; }
        public CampaignRulerPrompt Ruler { get; private set; }
        public DialogueBox Dialogue { get; private set; }
        public CampaignExhibitToast Exhibit { get; private set; }
        public CampaignSkipChip Skip { get; private set; }
        public MeTvPip Pip { get; private set; }
        public CampaignChapterCard Card { get; private set; }
        public CampaignCardBadges Badges { get; private set; }

        /// <summary>A blocking story scene is running (GameEvents.CutsceneChanged).</summary>
        public bool Blocking { get; private set; }
        /// <summary>ui's gameplay HUD is up (we are in a level).</summary>
        public bool InLevel => UIManager.Instance != null && UIManager.Instance.HudVisible;
        public bool TimerVisible { get; private set; }

        CanvasGroup _gameplayLayer, _pipGroup, _tvBackdrop;
        bool _rolesExplicit;
        // ui's level-title / gameplay banners are held back while a blocking scene runs (fixes/ui_hud.md story #2)
        struct HeldBanner { public string Title, Subtitle; public BannerStyle Style; public bool Intro; }
        readonly List<HeldBanner> _heldBanners = new List<HeldBanner>(4);
        HudBanners _bannersHeld;
        // a chapter/act card was up (or waiting) during the current hold: held level-intro banners are dropped (polish 1b)
        bool _cardInHold;
        // the banner LevelManager.Begin raised for this scene (it may already be on screen when a blocking scene holds it)
        string _introTitle;

        /// <summary>A chapter/act card waits for the current announcements (banner / defeat stamp) to finish.</summary>
        public const float CardMaxWait = 6f;
        struct PendingCard { public bool Set; public string Act, Title; public float Since; }
        PendingCard _pendingCard;
        /// <summary>A chapter card request is waiting for the banner / defeat stamp (polish H1).</summary>
        public bool CardPending => _pendingCard.Set;

        /// <summary>True while ui's HudBanners are held back by a blocking scene (they replay when it ends).</summary>
        public bool BannersHeld => _bannersHeld != null;
        public int HeldBannerCount => _heldBanners.Count;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            MainCanvas = UIFactory.Canvas("Campaign HUD Canvas", transform, MainOrder, false);
            PipCanvas = UIFactory.Canvas("Campaign PiP Canvas", transform, PipOrder, false);
            CardCanvas = UIFactory.Canvas("Campaign Card Canvas", transform, CardOrder, false);

            var m = MainCanvas.transform;
            // full-screen ME-TV backdrop: dims the world under the big set, below the letterbox + DialogueBox
            var bd = UIFactory.Rect("TvBackdrop", m); UIFactory.Stretch(bd);
            _tvBackdrop = UIFactory.Group(bd.gameObject);
            _tvBackdrop.alpha = 0f; _tvBackdrop.blocksRaycasts = false; _tvBackdrop.interactable = false;
            var bdi = UIFactory.Fill(bd, "Dim", null, new Color(0.03f, 0.03f, 0.06f, 0.86f)); bdi.raycastTarget = false;
            var bdv = UIFactory.Fill(bd, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0f, 0.7f)); bdv.raycastTarget = false;
            var bdsRt = UIFactory.Rect("Scan", bd); UIFactory.Stretch(bdsRt);
            var bds = bdsRt.gameObject.AddComponent<RawImage>(); bds.raycastTarget = false; bds.texture = CampaignHudStyle.Scanlines;
            bds.uvRect = new Rect(0f, 0f, 1f, 180f); bds.color = new Color(1f, 1f, 1f, 0.05f);
            Letterbox = CampaignLetterbox.Create(m);
            var layer = UIFactory.Rect("GameplayLayer", m);
            UIFactory.Stretch(layer);
            _gameplayLayer = UIFactory.Group(layer.gameObject);
            _gameplayLayer.blocksRaycasts = false; _gameplayLayer.interactable = false;
            BossBar = CampaignBossBar.Create(layer);
            Chase = CampaignChaseMeter.Create(layer);
            Memo = CampaignObjectiveMemo.Create(layer);
            Ruler = CampaignRulerPrompt.Create(layer);
            Dialogue = DialogueBox.Create(m);
            Exhibit = CampaignExhibitToast.Create(m);
            Skip = CampaignSkipChip.Create(m);

            var pipRoot = UIFactory.Rect("PipRoot", PipCanvas.transform);
            UIFactory.Stretch(pipRoot);
            _pipGroup = UIFactory.Group(pipRoot.gameObject);
            _pipGroup.blocksRaycasts = false; _pipGroup.interactable = false;
            Pip = MeTvPip.Create(pipRoot);

            Card = CampaignChapterCard.Create(CardCanvas.transform);
            Badges = CampaignCardBadges.Create(transform);
        }

        void OnEnable()
        {
            GameEvents.StoryLineShown += OnLineShown;
            GameEvents.StoryLineHidden += OnLineHidden;
            GameEvents.CutsceneChanged += OnCutscene;
            GameEvents.BroadcastChanged += OnBroadcast;
            GameEvents.BossChanged += OnBoss;
            GameEvents.ChaseChanged += OnChase;
            GameEvents.ExhibitAcquired += OnExhibit;
            GameEvents.ChapterCard += OnChapterCard;
            GameEvents.Banner += OnAnyBanner;
            GameEvents.ObjectiveChanged += OnObjective;
            GameEvents.StoryBeat += OnBeat;
            GameEvents.TimerChanged += OnTimer;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.SwitcherOpenChanged += OnWheelOpen;
            GameEvents.SwitcherSelectionChanged += OnWheelSelection;
            GameEvents.CharacterSwitched += OnSwitched;
            SceneManager.sceneLoaded += OnSceneLoaded;
            StoryPlayer.Cue += OnCue;
        }

        void OnDisable()
        {
            GameEvents.StoryLineShown -= OnLineShown;
            GameEvents.StoryLineHidden -= OnLineHidden;
            GameEvents.CutsceneChanged -= OnCutscene;
            GameEvents.BroadcastChanged -= OnBroadcast;
            GameEvents.BossChanged -= OnBoss;
            GameEvents.ChaseChanged -= OnChase;
            GameEvents.ExhibitAcquired -= OnExhibit;
            GameEvents.ChapterCard -= OnChapterCard;
            GameEvents.Banner -= OnAnyBanner;
            GameEvents.ObjectiveChanged -= OnObjective;
            GameEvents.StoryBeat -= OnBeat;
            GameEvents.TimerChanged -= OnTimer;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.SwitcherOpenChanged -= OnWheelOpen;
            GameEvents.SwitcherSelectionChanged -= OnWheelSelection;
            GameEvents.CharacterSwitched -= OnSwitched;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StoryPlayer.Cue -= OnCue;
            ReleaseBanners(false);
        }

        // ------------------------------------------------------------------ public API
        /// <summary>Heist role stamps on the HUD cards (Act II). Overrides the automatic Bank-theme default until the next scene.</summary>
        public void SetRoleStamps(bool on) { _rolesExplicit = true; Badges.SetRoles(on); }

        /// <summary>C15 "WHO RULES TODAY? [RIGHT SHIFT]" (title null = default).</summary>
        public void ShowRuler(bool on, string title = null) { if (on) Ruler.Show(title); else Ruler.Hide(); }

        /// <summary>Show a chapter card directly (same as raising GameEvents.ChapterCard, with explicit lines).</summary>
        public void ShowChapterCard(string act, string title, string objective = null, string quip = null) => Card.Show(act, title, objective, quip);

        /// <summary>Hide every campaign widget (scene change, tests). The chapter card survives unless includeCard.</summary>
        public void ResetAll(bool includeCard = false)
        {
            Blocking = false;
            ReleaseBanners(false);
            Dialogue.Hide(true);
            Pip.Close(true);
            Letterbox.Set(false, true);
            Skip.Set(false);
            BossBar.Hide(); BossBar.SetSuppressed(false);
            Chase.Hide(); Chase.SetSuppressed(false);
            Exhibit.Hide(true);
            Ruler.Hide(true);
            Memo.Hide(true);
            TimerVisible = false;
            if (includeCard) Card.Close(true);
        }

        // ------------------------------------------------------------------ event handlers
        void OnLineShown(StoryLine l)
        {
            Dialogue.Show(l);
            if (l.Broadcast) Pip.ShowLine(l);
        }

        void OnLineHidden()
        {
            Dialogue.Hide();
            Pip.LineEnded();
        }

        void OnCutscene(bool blocking)
        {
            Blocking = blocking;
            Letterbox.Set(blocking);
            Skip.Set(blocking);
            BossBar.SetSuppressed(blocking);
            Chase.SetSuppressed(blocking);
            if (blocking) { Memo.Hide(); HoldBanners(); }
            else { Pip.SetFullScreen(false); if (!Card.Showing) ReleaseBanners(true); }   // else: Update releases them when the card closes
        }

        /// <summary>StoryPlayer.Cue: ("tv", "MAX", channel) grows the PiP to the full-screen ME-TV; arg "off" ends it.</summary>
        void OnCue(string kind, string target, string arg)
        {
            if (kind != "tv") return;
            bool off = string.IsNullOrEmpty(arg) || string.Equals(arg, "off", System.StringComparison.OrdinalIgnoreCase);
            Pip.SetFullScreen(!off, arg);
        }

        // ------------------------------------------------------------------ banner hold (blocking scenes)
        /// <param name="keepCurrent">Re-queue the banner on screen (blocking scenes). A chapter card does not: it only
        /// shows live after the banners are done, or over the load black (an old level's banner must not replay).</param>
        void HoldBanners(bool keepCurrent = true)
        {
            var b = UIManager.Instance != null ? UIManager.Instance.Banners : null;
            if (b == null || _bannersHeld != null || !b.gameObject.activeSelf) return;
            _heldBanners.Clear();
            _cardInHold = false;
            if (keepCurrent && b.Showing && !string.IsNullOrEmpty(b.CurrentTitle))
            {
                var sub = b.transform.Find("Subtitle");
                var subT = sub != null ? sub.GetComponent<TextMeshProUGUI>() : null;
                var band = b.transform.Find("Band");
                var bandImg = band != null ? band.GetComponent<Image>() : null;
                var style = BannerStyle.Info;
                if (bandImg != null)
                {
                    float best = 9f;
                    foreach (BannerStyle st in System.Enum.GetValues(typeof(BannerStyle)))
                    {
                        Color c = HudBanners.StyleColor(st), d = bandImg.color;
                        float e = Mathf.Abs(c.r - d.r) + Mathf.Abs(c.g - d.g) + Mathf.Abs(c.b - d.b);
                        if (e < best) { best = e; style = st; }
                    }
                }
                _heldBanners.Add(new HeldBanner { Title = b.CurrentTitle, Subtitle = subT != null && subT.gameObject.activeSelf ? subT.text : null, Style = style,
                                                  Intro = _introTitle != null && b.CurrentTitle == _introTitle });
            }
            b.Clear();
            b.gameObject.SetActive(false); // HudBanners unsubscribes from GameEvents.Banner while inactive
            _bannersHeld = b;
            GameEvents.Banner += BufferBanner;
        }

        void BufferBanner(string title, string subtitle, BannerStyle style)
        {
            if (string.IsNullOrEmpty(title)) return;
            if (_heldBanners.Count >= 4) _heldBanners.RemoveAt(0);
            _heldBanners.Add(new HeldBanner { Title = title, Subtitle = subtitle, Style = style, Intro = IsLevelIntro() });
        }

        /// <summary>LevelManager.Begin raises its level-intro (or checkpoint-restore) banner just before IsStarted turns true.</summary>
        static bool IsLevelIntro() { var lm = LevelManager.Instance; return lm != null && !lm.IsStarted; }
        void OnAnyBanner(string title, string subtitle, BannerStyle style) { if (IsLevelIntro()) _introTitle = title; }

        void ReleaseBanners(bool replay)
        {
            if (_bannersHeld == null) { _heldBanners.Clear(); return; }
            GameEvents.Banner -= BufferBanner;
            var b = _bannersHeld;
            _bannersHeld = null;
            b.gameObject.SetActive(true);
            if (replay) for (int i = 0; i < _heldBanners.Count; i++) if (!(_cardInHold && _heldBanners[i].Intro)) b.Enqueue(_heldBanners[i].Title, _heldBanners[i].Subtitle, _heldBanners[i].Style);
            _heldBanners.Clear();
            _cardInHold = false;
        }

        void OnBroadcast(bool on, string speakerId) => Pip.SetBroadcast(on, speakerId);
        void OnBoss(BossStatus s) => BossBar.Apply(s);
        void OnChase(ChaseStatus s) => Chase.Apply(s);
        void OnExhibit(string id) => Exhibit.Show(id);
        void OnChapterCard(string act, string title)
        {
            if (AnnouncementBusy()) { _pendingCard = new PendingCard { Set = true, Act = act, Title = title, Since = UITime.Now }; return; }
            _pendingCard.Set = false;
            Card.Show(act, title);
        }

        /// <summary>Live gameplay with a centre banner (showing or queued) or the boss defeat stamp still up: a chapter
        /// card would draw over them (CH11: ACT IV over "SCARE-O-MATIC 3000: UNPLUGGED" + TERMINATED). Over the load
        /// black (or while fading) the card shows at once.</summary>
        bool AnnouncementBusy()
        {
            if (ScreenFader.Alpha > 0.5f || ScreenFader.IsFading || !InLevel) return false;
            var b = UIManager.Instance != null ? UIManager.Instance.Banners : null;
            if (b != null && b.gameObject.activeSelf && (b.Showing || b.Queued > 0)) return true;
            return BossBar.DefeatStampUp;
        }

        static bool BannerOnScreen(UIManager ui)
        {
            var b = ui != null ? ui.Banners : null;
            return b != null && b.gameObject.activeSelf && b.Showing;
        }
        void OnObjective(string text) => Memo.OnObjective(text, InLevel && !Blocking);
        void OnTimer(float seconds) => TimerVisible = seconds > 0f;
        void OnLevelStarted() => Memo.OnLevelStarted();
        void OnWheelOpen(bool open) => Ruler.OnWheelOpen(open);
        void OnWheelSelection(int slot) => Ruler.OnWheelSelection(slot);
        void OnSwitched(ShifterCharacter from, ShifterCharacter to) => Ruler.OnSwitched(to);

        void OnBeat(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("hud.")) return;
            switch (id)
            {
                case "hud.roles.on": SetRoleStamps(true); break;
                case "hud.roles.off": SetRoleStamps(false); break;
                case "hud.ruler.on": ShowRuler(true); break;
                case "hud.ruler.off": ShowRuler(false); break;
                case "hud.pip.off": Pip.Close(); break;
                case "hud.reset": ResetAll(); break;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            ResetAll();
            _rolesExplicit = false;
            _introTitle = null;
            int idx = LevelCatalog.IndexOfScene(scene.name);
            var info = LevelCatalog.Get(idx);
            Badges.SetRoles(info != null && info.Theme == LevelTheme.Bank);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            CampaignHudCompat.Tick(UITime.Delta);
            var ui = UIManager.Instance;
            bool paused = ui != null && ui.IsPaused;
            bool menu = ui != null && ui.AnyMenuOpen;
            bool inLevel = ui != null && ui.HudVisible;

            // gameplay widgets only in a level and not under the pause screen
            float gl = inLevel && !paused ? 1f : 0f;
            if (!Mathf.Approximately(_gameplayLayer.alpha, gl)) _gameplayLayer.alpha = Mathf.MoveTowards(_gameplayLayer.alpha, gl, UITime.Delta * 8f);
            // PiP is above the menus (Level Complete reactions) but never above the pause screen
            float pa = paused ? 0f : 1f;
            if (!Mathf.Approximately(_pipGroup.alpha, pa)) _pipGroup.alpha = pa;
            Pip.SetCaptionVisible(menu || !inLevel);
            float tb = Pip.FullScreen && Pip.Visible && !paused ? 1f : 0f;
            if (!Mathf.Approximately(_tvBackdrop.alpha, tb)) _tvBackdrop.alpha = Mathf.MoveTowards(_tvBackdrop.alpha, tb, UITime.Delta * 3f);

            // announcement order: a waiting chapter card shows once the banner / defeat stamp are done; while a card is up
            // ui's banners are held (they replay after it, or after the blocking scene that is still running)
            if (_pendingCard.Set && (!AnnouncementBusy() || UITime.Now - _pendingCard.Since > CardMaxWait))
            {
                _pendingCard.Set = false;
                Card.Show(_pendingCard.Act, _pendingCard.Title);
            }
            if (Card.Showing) { if (_bannersHeld == null) HoldBanners(false); }
            else if (_bannersHeld != null && !Blocking) ReleaseBanners(true);
            if (_bannersHeld != null && (Card.Showing || _pendingCard.Set)) _cardInHold = true;   // the card is the title

            // ui's tutorial card: docks to the left column while the boss bar / chase meter owns the top centre,
            // ducks while a centre banner shows (polish H2)
            var tut = ui != null && ui.Hud != null ? ui.Hud.Tutorial : null;
            if (tut != null)
            {
                tut.SetDocked(BossBar.Shown || Chase.Shown);
                tut.SetDucked(BannerOnScreen(ui));
            }

            // top-centre stack: boss bar, then chase meter; both drop under the timed-door timer
            float y = TopY - (TimerVisible ? TimerDrop : 0f);
            // kitfix REQ level6-1: if the stack would cover ui's HudTutorial card, it moves below the card while it shows
            float stackH = (BossBar.Shown ? CampaignBossBar.H : 0f) + (Chase.Shown ? CampaignChaseMeter.H : 0f)
                         + (BossBar.Shown && Chase.Shown ? StackGap : 0f);
            if (stackH > 0f && TutorialCardBottom(ui, out float tutBottom) && y - stackH < TutorialTop - 0.5f)
                y = Mathf.Min(y, tutBottom - StackGap);
            BossBar.SetHome(new Vector2(0f, y));
            if (BossBar.Shown) y -= CampaignBossBar.H + StackGap;
            Chase.SetHome(new Vector2(0f, y));
        }
    }
}
