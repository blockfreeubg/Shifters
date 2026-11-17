// =====================================================================================
//  SHIFTERS - PolishHudTests.cs   (polish round "hud", DevPlay)
//  H1 act card waits for the boss-defeat banner + TERMINATED stamp (real L12 boss, story OFF) and banners
//     raised under a card replay after it.
//  H2 tutorial card docks to the left column during boss fights (real L6 / L13 bosses) and ducks under the
//     defeat banner.
//  H3 Percy radio antenna clear of the objective panel (L1).
//  H4 CONTINUE THE CAMPAIGN memo keeps CLOCK IN focused when it pops up under a resting mouse.
//  H5 BenchOverlay: off by default, SHOW FPS setting, ?bench=1 parsing, settings row fits the panel.
//  1b (RunTitle) real campaign chapter starts: the chapter card is the title, the level-intro banner raised under it
//     is dropped (never visible during the card or the 12 s after it); a non-intro banner raised under the card still
//     replays; FREE PLAY (no card) still shows the intro banner. Frames every 0.5 s (t_<chapter>_NN).
//  Run: devplay.sh <clone> polish_hud -devSetup Shifters.PolishHudTests.RunAll
//         -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSize 1920x1080 -devSeconds 1
//       devplay.sh <clone> polish_title -devSetup Shifters.PolishHudTests.RunTitle
//         -devScene Assets/Shifters/Scenes/MainMenu.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class PolishHudTests
    {
        static DevPlayContext _ctx;

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            _ctx = ctx;
            StoryPlayer.StoryModeOverride = 2;   // story OFF (the CH11 evidence run)
            yield return L1Part();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-polishL1Only") >= 0) { StoryPlayer.StoryModeOverride = -1; yield break; }
            yield return L6Part();
            yield return L12Part();
            yield return L13Part();
            StoryPlayer.StoryModeOverride = -1;
        }

        // ------------------------------------------------------------------ 1b: chapter card = title (no intro banner after it)
        public static IEnumerator RunTitle(DevPlayContext ctx)
        {
            _ctx = ctx;
            yield return null;
            SessionPrefs.BackupAll();
            var d = CampaignDirector.Instance; var gs = GameSession.Instance;
            if (d == null || gs == null) { ctx.Check(false, "1b director/session present"); yield break; }
            d.UseStore(new MemoryStore());
            if (!gs.HasRun) gs.NewRun(1);
            string introRaised = null;   // what LevelManager.Begin raised (before IsStarted)
            System.Action<string, string, BannerStyle> onBanner = (t, sub, st) =>
            {
                var lm = LevelManager.Instance;
                if (lm != null && !lm.IsStarted) introRaised = t;
            };
            GameEvents.Banner += onBanner;
            try
            {
                StoryPlayer.StoryModeOverride = 2;   // OFF: + a gameplay banner raised under the card must still replay
                introRaised = null;
                yield return ChapterStart(d, "CH02", 1, true, () => introRaised);
                StoryPlayer.StoryModeOverride = 0;   // FULL (the default)
                introRaised = null;
                yield return ChapterStart(d, "CH07", 7, false, () => introRaised);
                introRaised = null;
                yield return ChapterStart(d, "CH13", 12, false, () => introRaised);

                // FREE PLAY L1: no chapter card, the intro banner shows as before
                StoryPlayer.StoryModeOverride = 2;
                introRaised = null;
                d.StartFreePlay(0);
                var ui = UIManager.Instance; var hud = CampaignHud.Instance;
                float t0 = Time.realtimeSinceStartup;
                while ((LevelManager.Instance == null || !LevelManager.Instance.IsStarted || LevelManager.Instance.LevelIndex != 0) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
                bool seen = false, card = false; string shown = "";
                for (int f = 0; f < 360; f++)
                {
                    if (hud.Card.Showing) card = true;
                    if (introRaised != null && ui.Banners.Showing && BannerDrawn(ui) == introRaised.ToUpperInvariant()) seen = true;
                    if (ui.Banners.Showing) shown = ui.Banners.CurrentTitle;
                    if (f % 30 == 0) Frame("t_free_l1_" + (f / 30).ToString("00"));
                    yield return null;
                }
                _ctx.Check(!d.IsCampaign && !card, "1b FREE PLAY L1: no chapter card (campaign " + d.IsCampaign + ", card " + card + ")");
                _ctx.Check(introRaised != null && seen, "1b FREE PLAY L1: intro banner '" + introRaised + "' shows (last banner '" + shown + "')");
            }
            finally
            {
                GameEvents.Banner -= onBanner;
                StoryPlayer.StoryModeOverride = -1;
                CampaignSave.Store = null;
                CampaignDirector.Instance?.UseStore(CampaignSave.Store);
                SessionPrefs.RestoreAll();
            }
            ctx.Log("PolishHudTests.RunTitle finished");
        }

        static IEnumerator ChapterStart(CampaignDirector d, string chapter, int levelIndex, bool extraBanner, System.Func<string> intro)
        {
            var ui = UIManager.Instance; var hud = CampaignHud.Instance;
            string tag = chapter.ToLowerInvariant();
            d.StartAt(chapter);
            _ctx.Check(d.IsCampaign && d.Current != null && d.Current.Id == chapter && hud.Card.Showing, "1b " + chapter + ": campaign chapter start shows the card ('" + hud.Card.ActText + " / " + hud.Card.TitleText + "')");
            string introTitle = d.Current != null ? d.Current.Title : "?";
            bool introVisible = false, extraRaised = false, extraSeen = false, started = false;
            int frame = 0, shot = 0, afterCard = -1, afterScene = -1;
            var banners = new List<string>();
            string state = "";
            // the card (load + 3 s), then 12 s of play (and 8 s after a blocking intro scene, story FULL)
            while (frame < 60 * 90 && (afterCard < 0 || frame - afterCard < 720 || afterScene < 0 || frame - afterScene < 480))
            {
                var lm = LevelManager.Instance;
                if (!started && lm != null && lm.IsStarted && lm.LevelIndex == levelIndex) { started = true; _ctx.Log(chapter + " level started at frame " + frame + " (card " + hud.Card.Showing + ")"); }
                if (afterCard < 0 && started && !hud.Card.Showing) afterCard = frame;
                if (afterScene < 0 && afterCard >= 0 && !hud.Blocking) afterScene = frame;
                // what is drawn (alpha + text), not only HudBanners.Showing: a cleared banner must not stay on screen
                string drawn = BannerDrawn(ui);
                string st = "card " + hud.Card.Showing + " blocking " + hud.Blocking + " held " + hud.BannersHeld + "/" + hud.HeldBannerCount
                          + " banner " + (ui.Banners.gameObject.activeSelf && ui.Banners.Showing ? ui.Banners.CurrentTitle : "-") + " drawn " + (drawn ?? "-");
                if (st != state) { state = st; _ctx.Log(chapter + " f" + frame + ": " + st); }
                if (drawn != null && !hud.Card.Showing && (banners.Count == 0 || banners[banners.Count - 1] != drawn)) banners.Add(drawn);
                if (drawn != null && !hud.Card.Showing && drawn == introTitle.ToUpperInvariant()) introVisible = true;
                if (extraBanner && started && !extraRaised && hud.Card.Showing)
                {
                    extraRaised = true;
                    GameEvents.RaiseBanner("POLISH 1B GAMEPLAY BANNER", "raised under the chapter card", BannerStyle.Checkpoint);
                }
                if (drawn == "POLISH 1B GAMEPLAY BANNER" && !hud.Card.Showing) extraSeen = true;
                if (frame % 30 == 0) Frame("t_" + tag + "_" + (shot++).ToString("00"));
                frame++;
                yield return null;
            }
            _ctx.Check(started && afterCard > 0, "1b " + chapter + ": level started and the card closed (after " + (afterCard / 60f).ToString("0.0") + " s; play after " + (afterScene / 60f).ToString("0.0") + " s)");
            _ctx.Check(intro() == introTitle, "1b " + chapter + ": LevelManager raised the intro banner '" + intro() + "' (expected '" + introTitle + "')");
            _ctx.Check(!introVisible, "1b " + chapter + ": intro banner '" + introTitle + "' dropped: never drawn after the card (through the intro scene + 12 s; banners drawn seen: " + string.Join(" | ", banners) + ")");
            if (extraBanner) _ctx.Check(extraRaised && extraSeen && !hud.Card.Showing, "1b " + chapter + ": a gameplay banner raised under the card replays after it (raised " + extraRaised + ", seen " + extraSeen + ")");
        }

        /// <summary>The centre banner's title if it is actually drawn (active, alpha > 0.05), else null.</summary>
        static string BannerDrawn(UIManager ui)
        {
            var b = ui != null ? ui.Banners : null;
            if (b == null || !b.gameObject.activeInHierarchy) return null;
            var g = b.GetComponent<CanvasGroup>();
            var t = b.transform.Find("Title");
            var tm = t != null ? t.GetComponent<TMPro.TextMeshProUGUI>() : null;
            return g != null && g.alpha > 0.05f && tm != null ? tm.text : null;
        }

        static void Frame(string name)
        {
            var cfg = _ctx.Config;
            cfg.shotWidth = 960; cfg.shotHeight = 540;
            _ctx.Shot(name);
        }

        // ------------------------------------------------------------------ H3 / H4 / H5 on L1
        static IEnumerator L1Part()
        {
            yield return WaitLevel("L1");
            var ui = UIManager.Instance; var hud = CampaignHud.Instance;
            if (ui == null || hud == null) { _ctx.Check(false, "L1 HUD up"); yield break; }

            // H3: Percy radio under a 3-line objective
            GameEvents.RaiseObjective("Survive orientation. Ignore Rule #1. Reach the exit elevator together.");
            GameEvents.RaiseBroadcastChanged(true, "PERCY");
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "PERCY", SpeakerName = "PERCY", Broadcast = true, Text = "Psst. Nice teamwork. I wrote those tutorials. Don't tell him." });
            yield return _ctx.Wait(1.6f);
            _ctx.Check(hud.Pip.Visible && hud.Pip.RadioSkin, "Percy radio PiP up");
            var antenna = hud.Pip.Rt.Find("Antenna") as RectTransform;
            var knob = antenna != null ? antenna.Find("Knob") as RectTransform : null;
            var obj = ui.Hud.Objective != null ? (RectTransform)ui.Hud.Objective.transform : null;
            yield return Layout(() =>
            {
                var ro = ScreenRect(obj); var ra = ScreenRect(antenna); var rk = ScreenRect(knob); var rp = ScreenRect(hud.Pip.Rt);
                _ctx.Check(!ro.Overlaps(ra) && !ro.Overlaps(rk), "H3 antenna + knob clear of the objective panel (obj " + Fmt(ro) + " antenna " + Fmt(ra) + " knob " + Fmt(rk) + ")");
                _ctx.Check(rk.yMin >= rp.yMin - 1f && rk.yMax <= rp.yMax + 1f, "H3 knob within the set's height " + Fmt(rk) + " / set " + Fmt(rp));
            });
            Shot("h3_l1_percy_radio");
            GameEvents.RaiseStoryLineHidden();
            GameEvents.RaiseBroadcastChanged(false, "PERCY");
            yield return _ctx.Wait(1.0f);

            // H5: BenchOverlay
            _ctx.Check(BenchOverlay.Instance != null, "H5 BenchOverlay self-created");
            _ctx.Check(!SessionActions.Settings.ShowFps && BenchOverlay.Instance != null && !BenchOverlay.Instance.Visible, "H5 overlay off by default (ShowFps=" + SessionActions.Settings.ShowFps + ", bench=" + BenchOverlay.BenchMode + ")");
            _ctx.Check(BenchOverlay.UrlHasBench("https://x.itch.zone/html/1/index.html?bench=1") && BenchOverlay.UrlHasBench("https://x/i.html?dpr=1&bench=1")
                       && BenchOverlay.UrlHasBench("https://x/i.html?bench") && !BenchOverlay.UrlHasBench("https://x/i.html?bench=0")
                       && !BenchOverlay.UrlHasBench("https://x/i.html?benchmark=1") && !BenchOverlay.UrlHasBench("https://x/i.html#bench=1")
                       && !BenchOverlay.UrlHasBench("") && !BenchOverlay.UrlHasBench(null), "H5 ?bench=1 URL parsing");
            SessionActions.Settings.ShowFps = true;
            yield return _ctx.Wait(1.5f);
            var bo = BenchOverlay.Instance;
            _ctx.Check(bo != null && bo.Visible && bo.Text.Contains("FPS") && bo.Text.Contains("1% LOW") && bo.AvgFps > 0f && bo.Low1Fps > 0f && bo.FrameMs > 0f,
                "H5 SHOW FPS on: '" + (bo != null ? bo.Text : "") + "'");
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "PERCY", SpeakerName = "PERCY", Text = "Thirty frames is a feeling. Sixty is a lifestyle." });
            yield return _ctx.Wait(1.2f);
            yield return Layout(() =>
            {
                var rb = ScreenRect(bo.transform.GetChild(0).GetChild(0) as RectTransform);
                var rd = ScreenRect(hud.Dialogue.Rt);
                _ctx.Check(!rb.Overlaps(rd), "H5 FPS pill " + Fmt(rb) + " clear of the DialogueBox " + Fmt(rd));
            });
            Shot("h5_showfps_overlay");
            GameEvents.RaiseStoryLineHidden();
            ui.OpenSettings();
            yield return _ctx.Wait(0.8f);
            var row = ui.Settings.Row("SHOW FPS");
            _ctx.Check(row != null && row.OptionIndex == 1, "H5 settings GAME tab has SHOW FPS (ON)");
            if (row != null)
            {
                var panel = row.transform.parent as RectTransform;
                while (panel != null && !panel.name.StartsWith("Panel_")) panel = panel.parent as RectTransform;
                yield return Layout(() =>
                {
                    var rp = ScreenRect(panel); var rr = ScreenRect(row.transform as RectTransform);
                    _ctx.Check(rp.Contains(new Vector2(rr.xMin + 2f, rr.yMin)) && rp.Contains(new Vector2(rr.xMax - 2f, rr.yMax)), "H5 SHOW FPS row " + Fmt(rr) + " inside the settings panel " + Fmt(rp));
                });
            }
            Shot("h5_settings_showfps");
            row?.SetIndex(0);
            SessionActions.Settings.ShowFps = false;
            ui.Pop();
            yield return _ctx.Wait(0.8f);
            _ctx.Check(bo != null && !bo.Visible, "H5 SHOW FPS off hides the overlay");

            // H4: the memo pops up under a resting mouse that sits where NOT TODAY appears
            yield return MemoGuard(ui);
        }

        static IEnumerator MemoGuard(UIManager ui)
        {
            Mouse mouse = null;
            var settings = InputSystem.settings;
            var oldBg = settings.backgroundBehavior;
            var oldEditor = settings.editorInputBehaviorInPlayMode;
            // batchmode has no focused game view: let the virtual mouse's events through (as WebPolishTests does)
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            try { mouse = InputSystem.AddDevice<Mouse>("PolishHudMouse"); } catch (System.Exception e) { _ctx.Log("no virtual mouse: " + e.Message); }
            bool yes = false, no = false;
            ui.AskConfirm("CONTINUE THE CAMPAIGN FROM HERE?", "SHIFT 05 · THE SACRIFICE ZONE", "CLOCK IN", "NOT TODAY", () => yes = true, () => no = true);
            yield return _ctx.Wait(0.6f);
            var c = ui.Confirm;
            Vector2 noPos = RectTransformUtility.WorldToScreenPoint(null, c.NoButton.Rt.TransformPoint(c.NoButton.Rt.rect.center));
            c.Answer(false);
            no = false;
            yield return _ctx.Wait(0.3f);
            if (mouse != null) InputSystem.QueueStateEvent(mouse, new MouseState { position = noPos });
            yield return _ctx.Wait(0.1f);
            ui.AskConfirm("CONTINUE THE CAMPAIGN FROM HERE?", "SHIFT 05 · THE SACRIFICE ZONE\n<size=75%>Your current interns clock in. The campaign continues from this shift.</size>", "CLOCK IN", "NOT TODAY", () => yes = true, () => no = true);
            yield return _ctx.Wait(0.5f);
            var es = EventSystem.current;
            ExecuteEvents.Execute(c.NoButton.gameObject, new PointerEventData(es) { position = noPos }, ExecuteEvents.pointerEnterHandler);
            bool hoverTook = es.currentSelectedGameObject == c.NoButton.gameObject;
            yield return null; yield return null;
            _ctx.Check(es.currentSelectedGameObject == c.YesButton.gameObject && c.DefaultGuardActive,
                "H4 (pointer " + ShiftersInput.PointerPosition + ", NOT TODAY at " + noPos + ") resting-pointer hover on NOT TODAY undone: CLOCK IN focused (hover had " + (hoverTook ? "taken" : "not taken") + " the focus, reverts " + c.DefaultGuardReverts + ")");
            Shot("h4_memo_clockin_focused");
            ExecuteEvents.Execute(es.currentSelectedGameObject, new BaseEventData(es), ExecuteEvents.submitHandler);   // Enter
            yield return _ctx.Wait(0.3f);
            _ctx.Check(yes && !no, "H4 Enter on the memo accepts (CLOCK IN)");
            // a real mouse move hands hover back to the player
            yes = no = false;
            ui.AskConfirm("WORK OVERTIME?", "OVERTIME 1", "CLOCK IN", "NOT TODAY", () => yes = true, () => no = true);
            yield return _ctx.Wait(0.5f);
            if (mouse != null) InputSystem.QueueStateEvent(mouse, new MouseState { position = noPos + new Vector2(30f, 4f) });
            yield return null; yield return null;
            ExecuteEvents.Execute(c.NoButton.gameObject, new PointerEventData(es) { position = noPos + new Vector2(30f, 4f) }, ExecuteEvents.pointerEnterHandler);
            yield return _ctx.Wait(0.1f);
            _ctx.Check(mouse == null || (es.currentSelectedGameObject == c.NoButton.gameObject && !c.DefaultGuardActive), "H4 a moved mouse can still hover NOT TODAY (selected "
                + (es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "none") + ", guard " + c.DefaultGuardActive + ", pointer " + ShiftersInput.PointerPosition + " no " + noPos
                + ", current " + (Pointer.current != null ? Pointer.current.name : "null") + ")");
            c.Answer(false);
            yield return _ctx.Wait(0.3f);
            _ctx.Check(no && !yes, "H4 NOT TODAY answers no");
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            settings.backgroundBehavior = oldBg;
            settings.editorInputBehaviorInPlayMode = oldEditor;
        }

        // ------------------------------------------------------------------ H2 (+H1 stamp) on L6
        static IEnumerator L6Part()
        {
            yield return LoadLevel("L6_PerformanceReview");
            var ui = UIManager.Instance; var hud = CampaignHud.Instance;
            var boss = Object.FindAnyObjectByType<BossBase>();
            _ctx.Check(boss != null, "L6 boss found");
            if (boss == null || ui == null || hud == null) yield break;
            boss.Begin();
            yield return WaitBannersDone(ui, 8f);
            _ctx.Check(hud.BossBar.Shown, "L6 boss bar up");
            GameEvents.RaiseTutorialShow(new TutorialMessage { Id = "polish_ch03_boss", Title = "BOSS FIGHT", Important = true, Keys = new[] { "RShift" },
                Body = "BOSSES ONLY SEE WHO YOU'RE CONTROLLING. Park the others in BREAK AREAS. Open the wheel to slow time." });
            yield return _ctx.Wait(1.0f);
            var tut = ui.Hud.Tutorial;
            _ctx.Check(tut.Visible && tut.Docked, "H2 tutorial docked to the left column while the boss bar is up");
            yield return CheckTutorialClear("L6 fight", ui, hud);
            Shot("h2_l6_boss_tutorial_docked");
            GameEvents.RaiseTimer(9.5f);
            yield return _ctx.Wait(0.7f);
            yield return CheckTutorialClear("L6 fight + timer", ui, hud);
            Shot("h2_l6_boss_tutorial_timer");
            GameEvents.RaiseTimer(0f);
            yield return _ctx.Wait(0.5f);

            boss.ForceDefeat();
            yield return _ctx.Wait(0.7f);
            _ctx.Check(ui.Banners.Showing && hud.BossBar.DefeatStampUp, "L6 defeat banner '" + ui.Banners.CurrentTitle + "' + stamp up");
            _ctx.Check(tut.Ducked && tut.GetComponent<CanvasGroup>().alpha < 0.05f, "H2 tutorial ducked under the defeat banner (alpha " + tut.GetComponent<CanvasGroup>().alpha.ToString("0.00") + ")");
            Shot("h2_l6_defeated");
            yield return WaitBannersDone(ui, 8f);
            yield return _ctx.Wait(0.8f);
            _ctx.Check(tut.Visible && !tut.Ducked && tut.GetComponent<CanvasGroup>().alpha > 0.95f, "H2 tutorial back after the banner");
            _ctx.Check(!hud.BossBar.Shown && !tut.Docked, "H2 boss bar gone: tutorial back at the top centre");
            Shot("h2_l6_after_banner");
            GameEvents.RaiseTutorialHide();
            yield return _ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ H1 on L12 (story OFF, real boss + real outro)
        static IEnumerator L12Part()
        {
            yield return LoadLevel("L12_Unmasked");
            var ui = UIManager.Instance; var hud = CampaignHud.Instance;
            var boss = Object.FindAnyObjectByType<BossBase>();
            _ctx.Check(boss != null, "L12 boss found");
            if (boss == null || ui == null || hud == null) yield break;
            boss.Begin();
            yield return WaitBannersDone(ui, 8f);
            _ctx.Check(hud.BossBar.Shown, "L12 boss bar up");
            boss.ForceDefeat();
            yield return _ctx.Wait(0.5f);
            if (!hud.CardPending && !hud.Card.Showing)
            {
                _ctx.Log("ch11.outro raised no card in this context: raising the act card like StoryText.ActCardNow(3)");
                GameEvents.RaiseChapterCard("ACT IV · OVERTIME", "STORM THE TOWER.");
            }
            _ctx.Check(ui.Banners.Showing && hud.CardPending && !hud.Card.Showing,
                "H1 act card waits while '" + ui.Banners.CurrentTitle + "' shows (pending " + hud.CardPending + ", card " + hud.Card.Showing + ", stamp " + hud.BossBar.DefeatStampUp + ")");
            Shot("h1_l12_defeat_banner");
            float t0 = Time.realtimeSinceStartup, waited = 0f;
            bool overlap = false;
            while (!hud.Card.Showing && Time.realtimeSinceStartup - t0 < 12f) { yield return null; waited += Time.unscaledDeltaTime; }
            for (int i = 0; i < 30 && hud.Card.Showing; i++)
            {
                if (ui.Banners.Showing && ui.Banners.gameObject.activeSelf) overlap = true;
                if (hud.BossBar.DefeatStampUp) overlap = true;
                yield return null;
            }
            _ctx.Check(hud.Card.Showing && !overlap, "H1 act card '" + hud.Card.ActText + " / " + hud.Card.TitleText + "' after the banner + stamp (waited ~" + waited.ToString("0.0") + " s, overlap " + overlap + ")");
            Shot("h1_l12_actcard");
            GameEvents.RaiseBanner("CHECKPOINT", null, BannerStyle.Checkpoint);
            yield return _ctx.Wait(0.3f);
            _ctx.Check(hud.Card.Showing && !(ui.Banners.gameObject.activeSelf && ui.Banners.Showing), "H1 a banner raised under the card is held");
            hud.Card.Skip();
            yield return _ctx.Wait(0.6f);
            _ctx.Check(ui.Banners.gameObject.activeSelf && ui.Banners.Showing && ui.Banners.CurrentTitle == "CHECKPOINT", "H1 held banner replays after the card (" + ui.Banners.CurrentTitle + ")");
            yield return WaitBannersDone(ui, 6f);
        }

        // ------------------------------------------------------------------ H2 on L13 (THE CAPTURE)
        static IEnumerator L13Part()
        {
            yield return LoadLevel("L13_HostileTakeover");
            var ui = UIManager.Instance; var hud = CampaignHud.Instance;
            var boss = Object.FindAnyObjectByType<BossBase>();
            _ctx.Check(boss != null, "L13 boss found");
            if (boss == null || ui == null || hud == null) yield break;
            boss.Begin();
            yield return WaitBannersDone(ui, 8f);
            _ctx.Check(hud.BossBar.Shown, "L13 boss bar up");
            GameEvents.RaiseTutorialShow(new TutorialMessage { Id = "polish_ch13_capture", Title = "THE CAPTURE", Important = true, Keys = new[] { "T", "F" },
                Body = "Stun him: bump him mid-speech, shoot him, TAUNT him (T) or TURBO-tag him. Then the Brute grabs the pod (F)." });
            yield return _ctx.Wait(1.0f);
            var tut = ui.Hud.Tutorial;
            _ctx.Check(tut.Visible && tut.Docked, "H2 THE CAPTURE docked while the boss bar is up");
            yield return CheckTutorialClear("L13 capture", ui, hud);
            // look at the boss with the call-out zone (pop text / bubbles over its head) in the upper middle
            var body = boss.Body != null ? boss.Body : boss.transform;
            Fx.FloatingText(body.position + Vector3.up * 4f, "DIZZY", UIColors.AccentYellow, 3f);
            yield return _ctx.Wait(0.4f);
            var cam = Camera.main;
            if (cam != null)
            {
                var p0 = cam.transform.position; var r0 = cam.transform.rotation;
                Vector3 fwd = body.position - p0; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();
                cam.transform.position = body.position - fwd * 13f + Vector3.up * 4.5f;
                cam.transform.rotation = Quaternion.LookRotation(body.position + Vector3.up * 2f - cam.transform.position);
                Shot("h2_l13_capture_docked", cam);
                cam.transform.SetPositionAndRotation(p0, r0);
            }
            GameEvents.RaiseTutorialHide();
            yield return _ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ helpers
        static IEnumerator CheckTutorialClear(string tag, UIManager ui, CampaignHud hud)
        {
            var tut = (RectTransform)ui.Hud.Tutorial.transform;
            var obj = ui.Hud.Objective != null ? (RectTransform)ui.Hud.Objective.transform : null;
            var timer = ui.Hud.Timer != null && ui.Hud.Timer.gameObject.activeInHierarchy ? (RectTransform)ui.Hud.Timer.transform : null;
            yield return Layout(() =>
            {
                var rt = ScreenRect(tut);
                _ctx.Check(!rt.Overlaps(ScreenRect(hud.BossBar.Rt)), "H2 " + tag + ": tutorial " + Fmt(rt) + " clear of the boss bar " + Fmt(ScreenRect(hud.BossBar.Rt)));
                if (obj != null) _ctx.Check(!rt.Overlaps(ScreenRect(obj)), "H2 " + tag + ": tutorial clear of the objective " + Fmt(ScreenRect(obj)));
                // PiP home slot (Max / Percy can broadcast mid-fight)
                var pipHome = ScreenRect(hud.Pip.Rt);   // at its home slot (hidden or not) outside full-screen TV
                _ctx.Check(!rt.Overlaps(pipHome), "H2 " + tag + ": tutorial clear of the PiP slot " + Fmt(pipHome));
                if (timer != null) _ctx.Check(!rt.Overlaps(ScreenRect(timer)), "H2 " + tag + ": tutorial clear of the timer");
                for (int i = 0; i < 3; i++)
                {
                    var card = ui.Hud.Team != null ? ui.Hud.Team.Card(i) : null;
                    if (card != null && card.gameObject.activeInHierarchy) _ctx.Check(!rt.Overlaps(ScreenRect(card.Rt)), "H2 " + tag + ": tutorial clear of team card " + i + " " + Fmt(ScreenRect(card.Rt)));
                }
                _ctx.Check(rt.xMin >= 0f && rt.yMin >= 0f && rt.xMax <= _sw && rt.yMax <= _sh, "H2 " + tag + ": tutorial inside the screen");
            });
        }

        static IEnumerator WaitLevel(string tag)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((CharacterManager.Instance == null || CharacterManager.Instance.Team == null || CharacterManager.Instance.Team.Count < 3
                    || UIManager.Instance == null || !UIManager.Instance.HudVisible) && Time.realtimeSinceStartup - t0 < 40f)
                yield return null;
            yield return _ctx.Wait(1.5f);
            ScreenFader.Set(0f);
            GameEvents.RaiseTutorialHide();
            if (CampaignHud.Instance != null && CampaignHud.Instance.Card.Showing) CampaignHud.Instance.Card.Close(true);
            if (CharacterManager.Instance != null) foreach (var c in CharacterManager.Instance.Team) if (c != null) c.ExternalControl = true;
            _ctx.Log(tag + " ready (" + SceneManager.GetActiveScene().name + ")");
        }

        static IEnumerator LoadLevel(string scene)
        {
            GameEvents.RaiseTutorialHide();
            SceneManager.LoadScene(scene);
            yield return null; yield return null;
            yield return WaitLevel(scene);
        }

        static IEnumerator WaitBannersDone(UIManager ui, float max)
        {
            float t0 = Time.realtimeSinceStartup;
            yield return _ctx.Wait(0.3f);
            while (ui.Banners != null && (ui.Banners.Showing || ui.Banners.Queued > 0) && Time.realtimeSinceStartup - t0 < max) yield return null;
            yield return _ctx.Wait(0.5f);
        }

        static void Shot(string name, Camera cam = null)
        {
            var cfg = _ctx.Config;
            cfg.shotWidth = 1920; cfg.shotHeight = 1080;
            _ctx.Shot(name, cam);
        }

        // layout checks run with the overlay canvases laid out at 1920x1080 (batch Screen is 640x480)
        static Camera _cam;
        static int _sw = 1920, _sh = 1080;

        static IEnumerator Layout(System.Action checks)
        {
            yield return null;
            var cam = Camera.main;
            if (cam == null) { var all = Object.FindObjectsByType<Camera>(); if (all.Length > 0) cam = all[0]; }
            if (cam == null) { checks(); yield break; }
            var rt = RenderTexture.GetTemporary(_sw, _sh, 24, RenderTextureFormat.ARGB32);
            var restore = new List<(Canvas c, RenderMode m, Camera wc, float pd)>();
            foreach (var c in Object.FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || !c.isActiveAndEnabled || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.4f);
            }
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            Rescale(restore);
            _cam = cam;
            try { checks(); }
            catch (System.Exception e) { _ctx.Check(false, "layout check threw: " + e.Message); }
            finally
            {
                _cam = null;
                cam.targetTexture = prev;
                foreach (var r in restore) { r.c.renderMode = r.m; r.c.worldCamera = r.wc; r.c.planeDistance = r.pd; }
                RenderTexture.ReleaseTemporary(rt);
                Rescale(restore);
            }
        }

        // CanvasScaler only re-evaluates in its Update/OnEnable: toggle it so the 1920x1080 target is used right now
        static void Rescale(List<(Canvas c, RenderMode m, Camera wc, float pd)> canvases)
        {
            foreach (var r in canvases) { var cs = r.c.GetComponent<UnityEngine.UI.CanvasScaler>(); if (cs != null && cs.enabled) { cs.enabled = false; cs.enabled = true; } }
            Canvas.ForceUpdateCanvases();
        }

        static Rect ScreenRect(RectTransform rt)
        {
            if (rt == null) return new Rect(-9999f, -9999f, 0f, 0f);
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            if (_cam != null) for (int i = 0; i < 4; i++) c[i] = RectTransformUtility.WorldToScreenPoint(_cam, c[i]);
            float x0 = Mathf.Min(Mathf.Min(c[0].x, c[1].x), Mathf.Min(c[2].x, c[3].x)), x1 = Mathf.Max(Mathf.Max(c[0].x, c[1].x), Mathf.Max(c[2].x, c[3].x));
            float y0 = Mathf.Min(Mathf.Min(c[0].y, c[1].y), Mathf.Min(c[2].y, c[3].y)), y1 = Mathf.Max(Mathf.Max(c[0].y, c[1].y), Mathf.Max(c[2].y, c[3].y));
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static string Fmt(Rect r) => "[" + r.xMin.ToString("0") + "," + r.yMin.ToString("0") + " " + r.width.ToString("0") + "x" + r.height.ToString("0") + "]";
    }
}
