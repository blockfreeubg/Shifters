// =====================================================================================
//  SHIFTERS - CampaignUITests.cs   (UI agent, DevPlay)
//  Screenshot tour + assertions of every campaign-wave screen at 1920x1080 AND 1280x720 (shot size switched
//  per shot): main menu (no save / with a campaign save -> CONTINUE "SHIFT 07 - ...", CAMPAIGN default,
//  QUIT vs WebGL), START OVER confirm, YOUR NEW HIRES (TEAM CODE, ENTER CODE), SHIFT SCHEDULE (rows, PUNCHED,
//  nav, scroll, confirm -> StartAt), FREE PLAY grid (nav, card -> StartFreePlay), SETTINGS story tab
//  (+ persistence through SessionPrefs), CREDITS, CASE FILE, Pause (CASE FILE + HR), Level Complete campaign
//  strip + LevelCompleteHold, campaign end (placeholder + full HR FILE).
//  Works without the campaign module: CampaignLink.SimulatePresent + a fake "shifters_campaign" save.
//  Scene-changing buttons run with SessionActions.DryRun. Every shifters_* pref it touches is backed up/restored.
//  Run: devplay.sh <clone> ui_campaign -devSetup Shifters.CampaignUITests.RunAll
//         -devScene Assets/Shifters/Scenes/MainMenu.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Shifters
{
    public static class CampaignUITests
    {
        static readonly string[] PrefKeys = { "shifters_settings", "shifters_run", "shifters_completed", "shifters_campaign" };

        const string FakeSave = "{\"v\":1,\"chapter\":\"CH07\",\"done\":[\"CH01\",\"CH02\",\"CH03\",\"CH04\",\"CH05\",\"CH06\"],\"seen\":[\"C00\"],\"meter\":0.35," +
                                "\"exhibits\":[\"A\"],\"lost\":[{\"name\":\"Gary\",\"cause\":\"Fell into the void\",\"chapter\":\"CH02\",\"clone\":\"Gary II\"}," +
                                "{\"name\":\"Brendaline\",\"cause\":\"Captured\",\"chapter\":\"CH05\",\"clone\":\"\"}],\"violations\":12,\"time\":1234.5,\"completed\":false}";

        /// <summary>Classic UITests.RunAll, then this tour, in one Unity launch.</summary>
        public static IEnumerator RunBoth(DevPlayContext ctx)
        {
            yield return FirstBoot(ctx);
            yield return UITests.RunAll(ctx);
            UIManager.Instance?.ShowMainMenu();
            yield return ctx.Wait(0.5f);
            yield return RunAll(ctx);
        }

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            var backup = Backup();
            SessionActions.DryRun = true;
            try
            {
                ScreenFader.Set(0f);
                yield return ctx.Wait(1.2f);
                ScreenFader.Set(0f);
                var ui = UIManager.Instance;
                ctx.Check(ui != null, "UIManager exists");
                if (ui == null) yield break;
                ctx.Log("campaign module installed: " + CampaignLink.ModuleInstalled + ", director: " + (CampaignLink.Director != null));
                yield return Menu(ctx, ui);
                yield return NewHires(ctx, ui);
                yield return Schedule(ctx, ui);
                yield return FreePlay(ctx, ui);
                yield return Settings(ctx, ui);
                yield return CaseFileAndEnd(ctx, ui);
                yield return InGame(ctx, ui);
                ctx.Log("SessionActions log: " + string.Join(", ", SessionActions.Log));
            }
            finally
            {
                SessionActions.DryRun = false;
                CampaignLink.SimulatePresent = false; CampaignLink.SimulatedCurrent = null; CampaignLink.SimulateHR = false; CampaignLink.ForceAbsent = false;
                LevelCompleteExtras.Clear();
                Restore(backup);
            }
        }

        // ------------------------------------------------------------------ first boot (verifier fix, 2026-11-16)
        // The main menu is opened from UIManager.OnSceneLoaded BEFORE CampaignDirector attaches (AfterSceneLoad). With the REAL
        // director and no Simulate flags the menu must still read CAMPAIGN and take CONTINUE from shifters_campaign.
        static IEnumerator FirstBoot(DevPlayContext ctx)
        {
            CampaignLink.ForceAbsent = false; CampaignLink.SimulatePresent = false;
            yield return null; yield return null;
            var ui = UIManager.Instance;
            var mm = ui != null ? ui.Top as MainMenuScreen : null;
            ctx.Log("first boot: top=" + (ui != null && ui.Top != null ? ui.Top.GetType().Name : "none") + " module=" + CampaignLink.ModuleInstalled +
                    " director=" + (CampaignLink.Director != null) + " label=" + (mm != null ? mm.CampaignButton.Label.text : "-") +
                    " continue=" + (mm != null && mm.ContinueButton.gameObject.activeSelf) + " hasSave=" + CampaignLink.HasSave +
                    " refreshes=" + (mm != null ? mm.PresenceRefreshes : -1));
            ctx.Check(mm != null, "first boot: main menu is on top");
            if (mm == null) yield break;
            if (!CampaignLink.ModuleInstalled) { ctx.Log("first boot: campaign module not installed - classic menu expected"); ctx.Check(mm.CampaignButton.Label.text == "PLAY", "first boot (no module): PLAY"); yield break; }
            ctx.Check(CampaignLink.Director != null, "first boot: the real CampaignDirector is attached (no Simulate flags)");
            ctx.Check(mm.CampaignButton.Label.text == "CAMPAIGN", "first boot: label CAMPAIGN without re-opening the menu (" + mm.CampaignButton.Label.text + ")");
            ctx.Check(mm.ContinueButton.gameObject.activeSelf == CampaignLink.HasSave, "first boot: CONTINUE follows the campaign save (visible " + mm.ContinueButton.gameObject.activeSelf + ", save " + CampaignLink.HasSave + ")");
            if (CampaignLink.HasSave && CampaignLink.SavedChapter != null)
                ctx.Check(mm.ContinueLabel == CampaignLink.SavedChapter.MenuLabel, "first boot: CONTINUE subline from shifters_campaign (" + mm.ContinueLabel + ")");

            // deterministic replay of the ordering: menu laid out while the module looks absent, then the director "attaches"
            int before = mm.PresenceRefreshes;
            CampaignLink.ForceAbsent = true;
            ui.ShowMainMenu();
            yield return null;
            ctx.Check(mm.CampaignButton.Label.text == "PLAY", "module absent while opening -> PLAY");
            CampaignLink.ForceAbsent = false;   // Present flips true while the menu stays open
            yield return null; yield return null;
            ctx.Check(mm.CampaignButton.Label.text == "CAMPAIGN" && mm.PresenceRefreshes > before, "Present flips while open -> CAMPAIGN on the next frame, no re-open (refreshes " + mm.PresenceRefreshes + ")");
            ctx.Check(mm.ContinueButton.gameObject.activeSelf == CampaignLink.HasSave, "after the flip CONTINUE follows the campaign save");
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            ctx.Check(sel != null && sel.activeInHierarchy, "a visible button stays selected after the flip (" + N(sel) + ")");
            yield return ctx.Wait(0.6f);
            yield return Shots(ctx, "c00_first_boot");
        }

        // two resolutions per screen (the canvas scaler re-lays out for the render target size)
        static IEnumerator Shots(DevPlayContext ctx, string name)
        {
            int w = ctx.Config.shotWidth, h = ctx.Config.shotHeight;
            ctx.Config.shotWidth = 1920; ctx.Config.shotHeight = 1080;
            ctx.Shot(name + "_1080");
            yield return null;
            ctx.Config.shotWidth = 1280; ctx.Config.shotHeight = 720;
            ctx.Shot(name + "_720");
            ctx.Config.shotWidth = w; ctx.Config.shotHeight = h;
            yield return null;
        }

        static GameObject Sel => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        static string N(GameObject g) => g != null ? g.name : "none";

        // ------------------------------------------------------------------ main menu
        static IEnumerator Menu(DevPlayContext ctx, UIManager ui)
        {
            // A: no campaign save, module absent -> classic labels, no CONTINUE without a run
            PlayerPrefs.DeleteKey(CampaignLink.SaveKey); CampaignLink.ReloadSave();
            CampaignLink.ForceAbsent = true;
            ui.ReplaceAll(ui.MainMenu);
            yield return ctx.Wait(0.8f);
            var mm = ui.MainMenu;
            ctx.Check(mm.Buttons.Count == 7, "7 menu buttons");
            ctx.Check(Sel == mm.CampaignButton.gameObject, "absent: default selection is CAMPAIGN/PLAY (" + N(Sel) + ")");
            ctx.Check(mm.CampaignButton.Label.text == "PLAY", "absent: CAMPAIGN reads PLAY");
            ctx.Check(mm.QuitButton.gameObject.activeSelf == !MainMenuScreen.IsWebGL, "QUIT visible exactly when not WebGL (editor: visible)");
            ctx.Check(mm.ContinueButton.gameObject.activeSelf == SessionActions.CanContinue, "absent: CONTINUE follows the classic saved run");
            ctx.Check(CampaignLink.ShiftLabelFor(6) == "SHIFT 07" && CampaignLink.ShiftLabelFor(0) == "SHIFT 01", "absent: classic shift labels (idx 6 -> '" + CampaignLink.ShiftLabelFor(6) + "')");

            // B: campaign present (simulated) without a save -> no CONTINUE
            CampaignLink.ForceAbsent = false;
            CampaignLink.SimulatePresent = true;
            ui.ReplaceAll(ui.MainMenu);
            yield return ctx.Wait(0.6f);
            ctx.Check(!mm.ContinueButton.gameObject.activeSelf, "campaign without a save: CONTINUE hidden");
            ctx.Check(mm.CampaignButton.Label.text == "CAMPAIGN", "campaign present: CAMPAIGN label");
            ctx.Check(CampaignLink.ShiftLabelFor(6) == "SHIFT 04" && CampaignLink.ShiftLabelFor(9) == "SHIFT 09" && CampaignLink.ShiftLabelFor(0) == "SHIFT 01" && CampaignLink.ShiftLabelFor(-1) == "",
                "campaign: chapter shift labels by catalog index (6 -> '" + CampaignLink.ShiftLabelFor(6) + "', 9 -> '" + CampaignLink.ShiftLabelFor(9) + "')");
            ctx.Check(Sel == mm.CampaignButton.gameObject, "default selection CAMPAIGN (" + N(Sel) + ")");
            yield return Shots(ctx, "c01_menu_nosave");

            // C: with a save -> CONTINUE "SHIFT 07 - THE FEDERAL COLOR RESERVE"
            PlayerPrefs.SetString(CampaignLink.SaveKey, FakeSave); CampaignLink.ReloadSave();
            ui.ReplaceAll(ui.MainMenu);
            yield return ctx.Wait(2.6f);   // ME-TV types a line
            ctx.Check(mm.ContinueButton.gameObject.activeSelf, "campaign save: CONTINUE visible");
            ctx.Check(mm.ContinueLabel == "SHIFT 07 - THE FEDERAL COLOR RESERVE", "CONTINUE label '" + mm.ContinueLabel + "'");
            ctx.Check(Sel == mm.CampaignButton.gameObject, "with a save the default is still CAMPAIGN (" + N(Sel) + ")");
            ctx.Check(!string.IsNullOrEmpty(mm.HeckleText), "ME-TV heckles: '" + mm.HeckleText + "'");
            yield return Shots(ctx, "c02_menu_continue");
            // navigation: Up from CAMPAIGN -> CONTINUE, Down x2 -> SHIFT SCHEDULE -> FREE PLAY
            var es = EventSystem.current;
            Move(es, MoveDirection.Up); yield return null;
            ctx.Check(Sel == mm.ContinueButton.gameObject, "Up from CAMPAIGN selects CONTINUE (" + N(Sel) + ")");
            Move(es, MoveDirection.Down); Move(es, MoveDirection.Down); Move(es, MoveDirection.Down); yield return null;
            ctx.Check(Sel == mm.FreePlayButton.gameObject, "Down x3 selects FREE PLAY (" + N(Sel) + ")");
            mm.ContinueButton.Click();
            ctx.Check(SessionActions.Last == "CampaignContinue", "CONTINUE -> CampaignDirector.Continue (" + SessionActions.Last + ")");
            // VILLAIN HECKLES: NEVER -> off air
            int heck = SessionActions.Settings.Heckles;
            SessionActions.Settings.Heckles = 2;
            yield return ctx.Wait(0.2f);
            ctx.Check(mm.HeckleText == "PLEASE STAND BY", "HECKLES NEVER: ME-TV off air ('" + mm.HeckleText + "')");
            SessionActions.Settings.Heckles = heck;
        }

        // ------------------------------------------------------------------ START OVER + YOUR NEW HIRES
        static IEnumerator NewHires(DevPlayContext ctx, UIManager ui)
        {
            var mm = ui.MainMenu;
            mm.CampaignButton.Click();
            yield return ctx.Wait(0.5f);
            ctx.Check(ui.Top == ui.Confirm && ui.Confirm.TitleText == "START OVER?", "CAMPAIGN with a save asks START OVER? (" + (ui.Top != null ? ui.Top.name : "none") + ")");
            ctx.Check(Sel == ui.Confirm.NoButton.gameObject, "START OVER defaults to the safe answer (" + N(Sel) + ")");
            yield return Shots(ctx, "c03_start_over");
            ui.Confirm.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.MainMenu, "Esc on START OVER returns to the menu");
            mm.CampaignButton.Click();
            yield return ctx.Wait(0.3f);
            ui.Confirm.Answer(true);
            yield return ctx.Wait(1.6f);
            var tr = ui.TeamReveal;
            ctx.Check(ui.Top == tr && tr.CurrentMode == TeamRevealScreen.Mode.Campaign, "START OVER -> YOUR NEW HIRES");
            ctx.Check(tr.TitleText.text == "YOUR NEW HIRES", "title '" + tr.TitleText.text + "'");
            ctx.Check(SessionActions.Team != null && tr.TeamCodeText.text == SessionActions.Team.Seed.ToString(), "TEAM CODE shows the seed (" + tr.TeamCodeText.text + ")");
            ctx.Check(Sel == tr.StartButton.gameObject, "START preselected");
            ctx.Check(tr.PartyRow.gameObject.activeSelf == TeamRevealScreen.CoopInstalled, "PARTY toggle only with the coop module (" + tr.PartyRow.gameObject.activeSelf + ")");
            yield return Shots(ctx, "c04_new_hires");
            Move(EventSystem.current, MoveDirection.Up); yield return null;
            ctx.Check(Sel == tr.EnterCodeButton.gameObject, "Up from START reaches ENTER CODE (" + N(Sel) + ")");
            ctx.Check(!tr.ApplyCode("abc"), "a non-numeric code is rejected");
            ctx.Check(tr.ApplyCode("482913") && SessionActions.Team.Seed == 482913 && tr.TeamCodeText.text == "482913", "ENTER CODE 482913 -> that team");
            var m0 = SessionActions.Team.Members[0].Name;
            tr.ApplyCode("482913");
            ctx.Check(SessionActions.Team.Members[0].Name == m0, "same code = same team (" + m0 + ")");
            yield return ctx.Wait(1.2f);
            yield return Shots(ctx, "c05_new_hires_code");
            tr.StartButton.Click();
            ctx.Check(SessionActions.Last == "CampaignNew:482913", "START -> CampaignDirector.StartNew(seed) (" + SessionActions.Last + ")");
            tr.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.MainMenu, "BACK from new hires");
        }

        // ------------------------------------------------------------------ SHIFT SCHEDULE
        static IEnumerator Schedule(DevPlayContext ctx, UIManager ui)
        {
            ui.MainMenu.ScheduleButton.Click();
            yield return ctx.Wait(0.9f);
            var sc = ui.ShiftSchedule;
            ctx.Check(ui.Top == sc, "SHIFT SCHEDULE opens");
            ctx.Check(ui.Toasts == null || ui.Toasts.ActiveCount == 0, "no stale TEAM CODE toasts over the SHIFT SCHEDULE header (" + (ui.Toasts != null ? ui.Toasts.ActiveCount : 0) + ")");
            ctx.Check(sc.Rows.Count == CampaignLink.Chapters.Count + CampaignLink.Overtime.Count && CampaignLink.Chapters.Count == 14, "rows: " + sc.Rows.Count + " (14 chapters + OVERTIME)");
            var r1 = sc.Find("CH01");
            ctx.Check(r1 != null && r1.Punched.text == "PUNCHED", "CH01 is PUNCHED from the save");
            var r7 = sc.Find("CH07");
            ctx.Check(r7 != null && (r7.Chapter.Playable || r7.Punched.text.Contains("NOT BUILT")), "CH07 row: playable or NOT BUILT");
            var ex5 = sc.Find("CH05");
            ctx.Check(ex5 != null && ex5.Exhibit != null && ex5.Exhibit.color == UIColors.AccentYellow, "CH05 shows exhibit A acquired");
            ctx.Check(sc.Find("O1") != null && sc.Find("O1").ColorText.text == "UNPAID", "OVERTIME rows say UNPAID");
            int playable = 0; foreach (var r in sc.Rows) if (r.Chapter.Playable) playable++;
            ctx.Check(Sel != null && (playable == 0 || (Sel.name.StartsWith("Row_"))), "a playable row is preselected (" + N(Sel) + ")");
            yield return Shots(ctx, "c06_schedule");
            // walk down every playable row: the list scrolls
            var es = EventSystem.current;
            for (int i = 0; i < playable + 2; i++) { Move(es, MoveDirection.Down); yield return ctx.Wait(0.05f); }
            yield return ctx.Wait(0.6f);
            ctx.Check(Sel == sc.BackButton.gameObject, "Down past the last row reaches BACK (" + N(Sel) + ")");
            Move(es, MoveDirection.Up); yield return ctx.Wait(0.6f);
            ctx.Check(sc.ContentHeight <= 650f || sc.Scroll > 1f, "the timesheet scrolled to keep the selection visible (scroll " + sc.Scroll.ToString("0") + " / " + sc.ContentHeight.ToString("0") + ")");
            yield return Shots(ctx, "c07_schedule_scrolled");
            if (r1 != null && r1.Chapter.Playable)
            {
                r1.Button.Click();
                yield return ctx.Wait(0.4f);
                ctx.Check(ui.Top == ui.Confirm && ui.Confirm.TitleText.StartsWith("CONTINUE THE CAMPAIGN"), "row with a team asks 'Continue the campaign from here?'");
                yield return Shots(ctx, "c08_schedule_confirm");
                ui.Confirm.Answer(true);
                ctx.Check(SessionActions.Last == "CampaignAt:CH01", "confirm -> StartAt(CH01) (" + SessionActions.Last + ")");
                yield return ctx.Wait(0.3f);
            }
            var unbuilt = (ShiftScheduleScreen.Row)null;
            foreach (var r in sc.Rows) if (!r.Chapter.Playable) { unbuilt = r; break; }
            if (unbuilt != null)
            {
                ctx.Check(!unbuilt.Button.Interactable, "unbuilt chapter row is disabled (" + unbuilt.Chapter.Id + ")");
                string last = SessionActions.Last;
                unbuilt.Button.Click();
                ctx.Check(SessionActions.Last == last && ui.Top == sc, "clicking an unbuilt row does nothing");
            }
            sc.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.MainMenu, "BACK from the schedule");
        }

        // ------------------------------------------------------------------ FREE PLAY
        static IEnumerator FreePlay(DevPlayContext ctx, UIManager ui)
        {
            ui.MainMenu.FreePlayButton.Click();
            yield return ctx.Wait(1.0f);
            var fp = ui.FreePlay;
            int built = 0; for (int i = 0; i < LevelCatalog.Count; i++) if (LevelCatalog.HasScene(i)) built++;
            ctx.Check(ui.Top == fp && fp.Cards.Count == built, "FREE PLAY shows every built level (" + fp.Cards.Count + "/" + built + ")");
            ctx.Check(fp.Cards.Count == 0 || Sel == fp.Cards[0].gameObject, "first card preselected");
            yield return Shots(ctx, "c09_free_play");
            if (fp.Cards.Count > 1)
            {
                Move(EventSystem.current, MoveDirection.Right); yield return null;
                ctx.Check(Sel == fp.Cards[1].gameObject, "Right moves to the next card");
                fp.Cards[1].Click();
                ctx.Check(SessionActions.Last == "FreePlay:" + fp.CardLevels[1], "card -> StartFreePlay (" + SessionActions.Last + ")");
            }
            fp.OnBack();
            yield return ctx.Wait(0.3f);
        }

        // ------------------------------------------------------------------ SETTINGS (story tab + persistence)
        static IEnumerator Settings(DevPlayContext ctx, UIManager ui)
        {
            ui.MainMenu.SettingsButton.Click();
            yield return ctx.Wait(0.6f);
            var st = ui.Settings;
            ctx.Check(ui.Top == st && st.Tab == 0, "settings open on the GAME tab");
            Move(EventSystem.current, MoveDirection.Up); yield return null;
            ctx.Check(Sel == st.GameTab.gameObject, "Up from the first row reaches the tabs (" + N(Sel) + ")");
            Move(EventSystem.current, MoveDirection.Right); yield return null; yield return null;
            ctx.Check(Sel == st.StoryTab.gameObject && st.Tab == 1, "Right on the tabs shows STORY & ASSISTS");
            string[] rows = { "STORY", "VILLAIN HECKLES", "SPOOKY", "COLOR SHIFT EFFECT", "HR ASSISTS", "SUBTITLE SPEED" };
            foreach (var r in rows) ctx.Check(st.Row(r) != null, "row " + r);
            var s = SessionActions.Settings;
            int story0 = s.StoryMode, heck0 = s.Heckles; bool spooky0 = s.SpookyMild, fx0 = s.ColorShiftFx, hr0 = s.HRAssists; float sub0 = s.SubtitleSpeed;
            st.Row("STORY").SetIndex(0); st.Row("STORY").Step(1);
            ctx.Check(s.StoryMode == 1 && st.Row("STORY").ValueText.text == "SHORT", "STORY -> SHORT");
            st.Row("VILLAIN HECKLES").SetIndex(1); st.Row("VILLAIN HECKLES").Step(1);
            ctx.Check(s.Heckles == 2, "VILLAIN HECKLES -> NEVER");
            st.Row("SPOOKY").SetIndex(0); st.Row("SPOOKY").Step(1);
            ctx.Check(s.SpookyMild, "SPOOKY -> MILD");
            st.Row("HR ASSISTS").SetIndex(1); st.Row("HR ASSISTS").Step(1);
            ctx.Check(!s.HRAssists, "HR ASSISTS -> OFF");
            st.Row("SUBTITLE SPEED").Slider.value = 1.5f;
            ctx.Check(Mathf.Abs(s.SubtitleSpeed - 1.5f) < 0.01f, "SUBTITLE SPEED -> 1.5x");
            yield return ctx.Wait(0.3f);
            yield return Shots(ctx, "c10_settings_story");
            st.OnBack();   // OnClosed -> SaveSettings
            yield return ctx.Wait(0.3f);
            var loaded = SessionPrefs.LoadSettings();
            ctx.Check(loaded.StoryMode == 1 && loaded.Heckles == 2 && loaded.SpookyMild && !loaded.HRAssists && Mathf.Abs(loaded.SubtitleSpeed - 1.5f) < 0.01f,
                "story settings persist (prefs: story " + loaded.StoryMode + " heckles " + loaded.Heckles + " mild " + loaded.SpookyMild + " hr " + loaded.HRAssists + " sub " + loaded.SubtitleSpeed + ")");
            s.StoryMode = story0; s.Heckles = heck0; s.SpookyMild = spooky0; s.ColorShiftFx = fx0; s.HRAssists = hr0; s.SubtitleSpeed = sub0;
            SessionActions.ApplySettings();
            ui.MainMenu.CreditsButton.Click();
            yield return ctx.Wait(4f);
            yield return Shots(ctx, "c11_credits");
            ui.Credits.OnBack();
            yield return ctx.Wait(0.3f);
        }

        // ------------------------------------------------------------------ CASE FILE + campaign end
        static IEnumerator CaseFileAndEnd(DevPlayContext ctx, UIManager ui)
        {
            ui.OpenCaseFile();
            yield return ctx.Wait(1.2f);
            var cf = ui.CaseFile;
            ctx.Check(ui.Top == cf, "CASE FILE opens");
            ctx.Check(cf.ShownExhibits.Count == 1 && cf.ShownExhibits[0] == "A", "exhibit A acquired, B/C missing (" + string.Join(",", cf.ShownExhibits) + ")");
            ctx.Check(cf.LostText.Contains("GARY") && cf.LostText.Contains("GARY II"), "lost log lists Gary -> Gary II");
            ctx.Check(cf.ViolationsText == "12", "violations 12 (" + cf.ViolationsText + ")");
            yield return Shots(ctx, "c12_case_file");
            cf.OnBack();
            yield return ctx.Wait(0.3f);

            ui.ShowCampaignEnd(true);
            yield return ctx.Wait(1.4f);
            ctx.Check(ui.Top == ui.CampaignEnd && ui.CampaignEnd.TitleText == "MAX REMAINS IN POWER... FOR NOW.", "R0 placeholder end card ('" + ui.CampaignEnd.TitleText + "')");
            yield return Shots(ctx, "c13_end_placeholder");
            ui.CampaignEnd.OnBack();
            yield return ctx.Wait(0.3f);
            ui.ShowCampaignEnd(false);
            yield return ctx.Wait(1.6f);
            var ce = ui.CampaignEnd;
            ctx.Check(ce.TitleText.Contains("COMPLETE") && ce.GradeText.StartsWith("TEAMWORK:") && ce.FileText.Contains("TEAM CODE") && ce.FileText.Contains("HIGHEST CLONE"),
                "full end + HR FILE (" + ce.GradeText + ")");
            ctx.Check(Sel == ce.MenuButton.gameObject, "MAIN MENU preselected on the end screen");
            yield return Shots(ctx, "c14_end_hr_file");
            ce.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.MainMenu, "end screen -> main menu");
            // queued path: RequestCampaignEnd while in a level is shown with the next main menu
            UIManager.PendingCampaignEnd = true;
            ui.ShowMainMenu();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.CampaignEnd && !UIManager.PendingCampaignEnd, "PendingCampaignEnd opens the end screen with the main menu");
            ui.CampaignEnd.OnBack();
            yield return ctx.Wait(0.3f);
        }

        // ------------------------------------------------------------------ pause + level complete (HUD on the menu scene)
        static IEnumerator InGame(DevPlayContext ctx, UIManager ui)
        {
            CampaignLink.SimulatedCurrent = CampaignLink.Find("CH05");
            CampaignLink.SimulateHR = true;
            bool hr0 = SessionActions.Settings.HRAssists;
            SessionActions.Settings.HRAssists = true;
            ui.ShowGameplayHUD();
            yield return ctx.Wait(0.3f);
            var hudObj = UnityEngine.Object.FindAnyObjectByType<HudObjective>();
            CampaignLink.SimulatedCurrent = CampaignLink.Find("CH01");
            if (hudObj != null) hudObj.RefreshLevel();
            ctx.Check(hudObj != null && hudObj.LevelText == "SHIFT 01  /  ORIENTATION DAY", "HUD objective header CH01 = ORIENTATION DAY (" + (hudObj != null ? hudObj.LevelText : "no HudObjective") + ")");
            yield return Shots(ctx, "c15a_hud_campaign_ch01");
            CampaignLink.SimulatedCurrent = CampaignLink.Find("CH05");
            if (hudObj != null) hudObj.RefreshLevel();
            ctx.Check(hudObj != null && hudObj.LevelText == "SHIFT 05  /  THE BACK WAY IN", "HUD objective header = chapter title in campaign (" + (hudObj != null ? hudObj.LevelText : "no HudObjective") + ")");
            ui.OpenPause();
            yield return ctx.Wait(0.8f);
            var p = ui.Pause;
            ctx.Check(p.StatsText.Contains("SHIFTS</color>  ") && p.StatsText.Contains("/14"), "pause RUN STATS: SHIFTS n/14 in campaign");
            ctx.Check(ui.Top == p && Sel == p.Find("RESUME").gameObject, "pause: RESUME preselected (" + N(Sel) + ")");
            ctx.Check(p.HRButton.gameObject.activeSelf && p.CaseFileButton.gameObject.activeSelf, "pause offers LET HR HANDLE IT + CASE FILE");
            p.ReleaseResumeGuard();   // WebPolish guard keeps RESUME until a real key press
            Move(EventSystem.current, MoveDirection.Down); yield return null;
            var navDown = p.Find("RESUME").Selectable.navigation.selectOnDown;
            ctx.Check(navDown != null && navDown.gameObject == p.Find("RESTART CHECKPOINT").gameObject, "nav: RESUME.down = RESTART CHECKPOINT (" + (navDown != null ? navDown.name : "none") + ")");
            ctx.Check(Sel == p.Find("RESTART CHECKPOINT").gameObject, "Down from RESUME = RESTART CHECKPOINT (WebPolish order kept) (sel " + N(Sel) + ")");
            yield return Shots(ctx, "c15_pause_campaign");
            p.CaseFileButton.Click();
            yield return ctx.Wait(0.5f);
            ctx.Check(ui.Top == ui.CaseFile && ui.IsPaused, "CASE FILE from pause (still paused)");
            ui.CaseFile.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == p, "back to pause");
            p.HRButton.Click();
            ctx.Check(SessionActions.Last == "HRIntervention" && !ui.IsPaused, "LET HR HANDLE IT -> RestartAtNextCheckpoint, pause closed (" + SessionActions.Last + ")");
            SessionActions.Settings.HRAssists = false;
            ui.OpenPause();
            yield return ctx.Wait(0.4f);
            ctx.Check(!p.HRButton.gameObject.activeSelf, "HR ASSISTS off: no HR button");
            ui.ClosePause();
            SessionActions.Settings.HRAssists = hr0;
            CampaignLink.SimulateHR = false;

            // level complete with the hold + campaign strip
            LevelCompleteExtras.StealthGrade = "GHOST";
            LevelCompleteExtras.Detections = 0;
            var r = new LevelResult { LevelIndex = 2, Time = 201f, Survivors = 3, LeftBehind = new string[0], Switches = 14, Restarts = 0 };
            LevelCompleteHold.Request(1.6f);
            ui.QueueLevelComplete(r);
            yield return ctx.Wait(1.3f);
            ctx.Check(ui.Top != ui.LevelComplete, "LevelCompleteHold keeps the screen back");
            yield return ctx.Wait(1.2f);
            var lc = ui.LevelComplete;
            ctx.Check(ui.Top == lc, "Level Complete opens after the hold");
            yield return ctx.Wait(1.8f);
            ctx.Check(lc.CampaignStripShown && lc.StealthText.Contains("GHOST") && lc.ExhibitText.Contains("EXHIBIT A"), "campaign strip + stealth grade + exhibit stamp");
            ctx.Check(lc.ReactionText.Length > 2, "Max's live reaction: " + lc.ReactionText);
            ctx.Check(lc.NextButton.Label.text == "NEXT SHIFT", "NEXT SHIFT label (" + lc.NextButton.Label.text + ")");
            // a story line still running when the screen is up goes to the ME-TV box, the HUD DialogueBox is hidden under the screen
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "MAX", SpeakerName = "MAX ME", Text = "...hi.", Face = Expression.Happy });
            yield return ctx.Wait(0.6f);
            ctx.Check(lc.MirroredLine == "...hi." && lc.ReactionText.Contains("...hi."), "story line mirrored into the ME-TV box (" + lc.ReactionText + ")");
            ctx.Check(CampaignHud.Instance == null || (lc.CampaignHudHidden && !CampaignHud.Instance.MainCanvas.enabled), "campaign HUD (DialogueBox, skip chip) hidden under Level Complete");
            yield return Shots(ctx, "c16_level_complete_campaign");
            lc.NextButton.Click();
            GameEvents.RaiseStoryLineHidden();
            ctx.Check(SessionActions.Last == "NextLevel", "NEXT SHIFT -> GameSession.NextLevel (campaign routes it)");
            // the cap: a hold longer than 3 s is clipped
            LevelCompleteHold.Request(10f);
            ctx.Check(LevelCompleteHold.Remaining <= LevelCompleteHold.MaxSeconds + 0.01f, "hold capped at 3 s (" + LevelCompleteHold.Remaining.ToString("0.00") + ")");
            LevelCompleteHold.Release();
            // suppression hook
            ui.CloseAll(true);
            ctx.Check(CampaignHud.Instance == null || (!lc.CampaignHudHidden && CampaignHud.Instance.MainCanvas.enabled), "campaign HUD canvas restored when Level Complete closes");
            ui.SetHudSuppressed(true);
            ctx.Check(ui.HudSuppressed, "SetHudSuppressed(true)");
            ui.SetHudSuppressed(false);
            CampaignLink.SimulatedCurrent = null;
            LevelCompleteExtras.Clear();
            ui.ShowMainMenu();
            yield return ctx.Wait(0.4f);
        }

        // ------------------------------------------------------------------ helpers
        static void Move(EventSystem es, MoveDirection dir)
        {
            if (es == null || es.currentSelectedGameObject == null) return;
            var data = new AxisEventData(es) { moveDir = dir, moveVector = dir == MoveDirection.Down ? Vector2.down : dir == MoveDirection.Up ? Vector2.up : dir == MoveDirection.Left ? Vector2.left : Vector2.right };
            ExecuteEvents.Execute(es.currentSelectedGameObject, data, ExecuteEvents.moveHandler);
        }

        static Dictionary<string, string> Backup()
        {
            var d = new Dictionary<string, string>();
            foreach (var k in PrefKeys) if (PlayerPrefs.HasKey(k)) d[k] = k == "shifters_completed" ? "i:" + PlayerPrefs.GetInt(k) : "s:" + PlayerPrefs.GetString(k);
            return d;
        }

        static void Restore(Dictionary<string, string> d)
        {
            foreach (var k in PrefKeys)
            {
                if (!d.TryGetValue(k, out var v)) { PlayerPrefs.DeleteKey(k); continue; }
                if (v.StartsWith("i:")) PlayerPrefs.SetInt(k, int.Parse(v.Substring(2))); else PlayerPrefs.SetString(k, v.Substring(2));
            }
            PlayerPrefs.Save();
            CampaignLink.ReloadSave();
            var gs = GameSession.Instance;
            if (gs != null && gs.Settings != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(SessionPrefs.LoadSettings()), gs.Settings);
        }
    }
}
