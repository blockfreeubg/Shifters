// =====================================================================================
//  SHIFTERS - UITests.cs   (UI agent, DevPlay)
//  One batched scenario that walks the whole UI in the real MainMenu scene:
//    main menu (+ letter shift) -> keyboard nav -> PLAY -> team reveal (REROLL / START) ->
//    level select -> settings (live) -> credits -> HUD with every element -> switcher wheel
//    (each slot, KO'd + LOST cards, deny) -> pause (+ settings from pause) -> level complete ->
//    team wiped -> victory via the main-menu path.
//  Scene-changing buttons run with SessionActions.DryRun (recorded, not executed).
//  PlayerPrefs (shared with the real project's Editor!) are backed up and restored.
//  Run: devplay.sh <clone> ui_all -devSetup Shifters.UITests.RunAll
//         -devScene Assets/Shifters/Scenes/MainMenu.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Shifters
{
    public static class UITests
    {
        static readonly string[] PrefKeys =
        {
            "shifters_settings", "shifters_run", "shifters_completed",
            "shifters_best_0", "shifters_best_1", "shifters_best_2", "shifters_best_3", "shifters_best_4",
        };

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            var backup = BackupPrefs();
            SessionActions.DryRun = true;
            CampaignLink.ForceAbsent = true;   // the classic flow (campaign module absent); CampaignUITests covers the campaign flow
            try
            {
                ScreenFader.Set(0f);
                yield return ctx.Wait(1.2f);
                ScreenFader.Set(0f);
                var ui = UIManager.Instance;
                ctx.Check(ui != null, "UIManager exists");
                if (ui == null) yield break;

                yield return MainMenu(ctx, ui);
                yield return TeamReveal(ctx, ui);
                yield return LevelSelect(ctx, ui);
                yield return Settings(ctx, ui);
                yield return Credits(ctx, ui);
                yield return Hud(ctx, ui);
                yield return Switcher(ctx, ui);
                yield return Pause(ctx, ui);
                yield return EndScreens(ctx, ui);
                ctx.Log("SessionActions log: " + string.Join(", ", SessionActions.Log));
            }
            finally
            {
                SessionActions.DryRun = false;
                CampaignLink.ForceAbsent = false;
                RestorePrefs(backup);
            }
        }

        // ------------------------------------------------------------------ main menu
        static IEnumerator MainMenu(DevPlayContext ctx, UIManager ui)
        {
            ctx.Check(ui.Top == ui.MainMenu, "MainMenu scene opens the main menu (top=" + (ui.Top != null ? ui.Top.name : "none") + ")");
            ctx.Check(ui.AnyMenuOpen && ShiftersInput.Context == InputContext.Menu, "menu context while the main menu is open");
            ctx.Check(FontLibrary.HasCustomFonts && FontLibrary.Display.name.Contains("Bungee"), "generated fonts load at runtime (" + (FontLibrary.Display != null ? FontLibrary.Display.name : "null") + ")");
            ctx.Check(FontLibrary.Label != null && FontLibrary.Label.name.Contains("Condensed") && FontLibrary.LogoMaterial != null, "label font + logo material load");
            int enabledEs = 0;
            foreach (var es in Object.FindObjectsByType<EventSystem>()) if (es.enabled) enabledEs++;
            ctx.Check(enabledEs == 1 && ui.Events != null && ui.Events.GetComponent<InputSystemUIInputModule>() != null, "exactly one EventSystem, with InputSystemUIInputModule (" + enabledEs + ")");
            var stage = MenuStage.Instance;
            ctx.Check(stage != null && stage.Puppets.Length == 3, "menu stage with 3 puppets");
            var marker = GameObject.Find(MenuStage.SceneMarkerName);
            ctx.Check(marker == null || (stage != null && stage.transform.parent == marker.transform), "menu stage lives under the scene's MenuStage marker");
            ui.ReplaceAll(ui.MainMenu);   // re-open with ForceAbsent applied (labels / CONTINUE visibility)
            yield return ctx.Wait(0.6f);
            ctx.Check(ui.MainMenu.Buttons.Count == 7, "main menu has 7 buttons (CONTINUE CAMPAIGN SCHEDULE FREEPLAY SETTINGS CREDITS QUIT)");
            ctx.Shot("01_main_menu");

            ui.MainMenu.Logo.ForceShift();
            yield return ctx.Wait(0.22f);
            ctx.Check(ui.MainMenu.Logo.IsShifting, "logo letters shifting");
            ctx.Shot("02_main_menu_letter_shift");
            yield return ctx.Wait(1.2f);

            // keyboard / gamepad navigation through the EventSystem (no devices in batchmode)
            var esys = EventSystem.current;
            var first = esys != null ? esys.currentSelectedGameObject : null;
            var play = ui.MainMenu.Buttons[1];
            ctx.Check(first == play.gameObject, "default selection is CAMPAIGN/PLAY (got " + (first != null ? first.name : "none") + ")");
            ctx.Check(play.Label.text == "PLAY", "without the campaign module the CAMPAIGN button reads PLAY ('" + play.Label.text + "')");
            esys.SetSelectedGameObject(play.gameObject);
            Move(esys, MoveDirection.Down);
            yield return null;
            ctx.Check(esys.currentSelectedGameObject == ui.MainMenu.Buttons[2].gameObject, "arrow down selects SHIFT SCHEDULE");
            Move(esys, MoveDirection.Up);
            Move(esys, MoveDirection.Up);
            yield return null;
            var top = esys.currentSelectedGameObject;
            UIButton lastActive = null; // [integrator] with no saved run CONTINUE is hidden: PLAY is first and Up wraps to the last button
            foreach (var b in ui.MainMenu.Buttons) if (b != null && b.gameObject.activeSelf) lastActive = b;
            ctx.Check(top == ui.MainMenu.Buttons[0].gameObject || top == play.gameObject || (lastActive != null && top == lastActive.gameObject),
                "arrow up wraps/stops at the top without throwing (" + (top != null ? top.name : "none") + ")");
            esys.SetSelectedGameObject(ui.MainMenu.Buttons[2].gameObject);
            Submit(esys);
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.ShiftSchedule, "submit on SHIFT SCHEDULE opens the schedule");
            ui.Top.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.MainMenu, "back returns to the main menu");

            ui.MainMenu.Find("QUIT").Click();
            ctx.Check(SessionActions.Last == "Quit", "QUIT calls GameSession.QuitGame (dry run)");
        }

        // ------------------------------------------------------------------ team reveal
        static IEnumerator TeamReveal(DevPlayContext ctx, UIManager ui)
        {
            ui.MainMenu.Buttons[1].Click();
            yield return ctx.Wait(0.25f);
            ctx.Check(ui.Top == ui.TeamReveal, "PLAY opens the team reveal");
            ctx.Check(SessionActions.Logged("NewRun") && SessionActions.Team != null, "PLAY starts a new run");
            int seed = SessionActions.Team != null ? SessionActions.Team.Seed : 0;
            ctx.Shot("03a_team_reveal_flip");
            yield return ctx.Wait(1.3f);
            var c0 = ui.TeamReveal.Card(0);
            ctx.Check(c0 != null && c0.gameObject.activeSelf && c0.Profile == SessionActions.Team.Members[0], "card 1 bound to member 1");
            ctx.Check(PortraitStudio.Instance != null, "portrait studio available in the menu");
            ctx.Shot("03_team_reveal");
            ui.TeamReveal.RerollButton.Click();
            yield return ctx.Wait(1.4f);
            ctx.Check(SessionActions.Team != null && SessionActions.Team.Seed != seed, "REROLL makes a new team (" + seed + " -> " + (SessionActions.Team != null ? SessionActions.Team.Seed : 0) + ")");
            ctx.Check(ui.TeamReveal.Card(1).Profile == SessionActions.Team.Members[1], "cards rebound after reroll");
            ctx.Check(MenuStage.Instance != null && MenuStage.Instance.Team == SessionActions.Team, "stage shows the rerolled team");
            ctx.Shot("03b_team_reveal_reroll");
            ui.TeamReveal.StartButton.Click();
            ctx.Check(SessionActions.Last == "LoadLevel:0", "START loads level 1 (" + SessionActions.Last + ")");
            ui.TeamReveal.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.MainMenu, "BACK from team reveal");
        }

        // ------------------------------------------------------------------ level select
        static IEnumerator LevelSelect(DevPlayContext ctx, UIManager ui)
        {
            ui.Push(ui.LevelSelect);   // the classic 5-card select is no longer on the menu (FREE PLAY replaced it); still built
            yield return ctx.Wait(0.9f);
            ctx.Check(ui.Top == ui.LevelSelect && ui.LevelSelect.Cards.Count == LevelCatalog.MainGameCount, "level select shows " + LevelCatalog.MainGameCount + " cards");
            ctx.Shot("04_level_select");
            ui.LevelSelect.Cards[2].Click();
            ctx.Check(SessionActions.Last == "LoadLevel:2", "level card 3 loads level index 2 (" + SessionActions.Last + ")");
            ui.LevelSelect.OnBack();
            yield return ctx.Wait(0.3f);
        }

        // ------------------------------------------------------------------ settings
        static IEnumerator Settings(DevPlayContext ctx, UIManager ui)
        {
            int changed = 0;
            System.Action onChanged = () => changed++;
            GameEvents.SettingsChanged += onChanged;
            ui.MainMenu.Find("SETTINGS").Click();
            yield return ctx.Wait(0.6f);
            ctx.Check(ui.Top == ui.Settings && ui.Settings.Rows.Count >= 11, "settings screen with " + ui.Settings.Rows.Count + " rows");
            var s = SessionActions.Settings;
            var vol = ui.Settings.Row("MASTER VOLUME");
            ctx.Check(vol != null && vol.Slider != null, "master volume slider exists");
            if (vol != null && vol.Slider != null)
            {
                vol.Slider.value = vol.Slider.maxValue > 1.5f ? 30f : 0.3f;
                ctx.Check(Mathf.Abs(s.MasterVolume - 0.3f) < 0.02f, "master volume writes Settings (" + s.MasterVolume.ToString("0.00") + ")");
            }
            var cam = ui.Settings.Row("SWITCH CAMERA");
            bool before = s.CinematicSwitch;
            if (cam != null) cam.Step(1);
            ctx.Check(cam != null && s.CinematicSwitch != before, "switch camera option toggles CinematicSwitch");
            var fov = ui.Settings.Row("FIELD OF VIEW");
            if (fov != null && fov.Slider != null) fov.Slider.value = 75f;
            ctx.Check(fov != null && Mathf.Abs(s.Fov - 75f) < 0.6f, "FOV slider writes Settings.Fov (" + s.Fov + ")");
            ctx.Check(changed > 0, "SettingsChanged raised for live preview (" + changed + ")");
            ctx.Shot("05_settings");
            ui.Settings.OnBack();
            yield return ctx.Wait(0.3f);
            GameEvents.SettingsChanged -= onChanged;
            ctx.Check(ui.Top == ui.MainMenu, "settings BACK returns to the main menu");
        }

        // ------------------------------------------------------------------ credits
        static IEnumerator Credits(DevPlayContext ctx, UIManager ui)
        {
            ui.MainMenu.Find("CREDITS").Click();
            yield return ctx.Wait(0.5f);
            float s0 = ui.Credits.Scroll;
            yield return ctx.Wait(2.0f);
            ctx.Check(ui.Top == ui.Credits && ui.Credits.Scroll > s0 + 20f, "credits scroll (" + s0.ToString("0") + " -> " + ui.Credits.Scroll.ToString("0") + ")");
            ctx.Shot("06_credits");
            ui.Credits.OnBack();
            yield return ctx.Wait(0.3f);
        }

        // ------------------------------------------------------------------ HUD
        static CharacterManager _cm;

        static IEnumerator Hud(DevPlayContext ctx, UIManager ui)
        {
            ui.ShowGameplayHUD();
            ctx.Check(MenuStage.Instance == null, "menu stage removed for gameplay");
            ctx.Check(ui.HudVisible && !ui.AnyMenuOpen && ShiftersInput.Context == InputContext.Gameplay, "HUD mode: gameplay context, no menu");

            LevelLighting.Apply(LevelTheme.Training);
            Kit.SolidBox(null, "UITestGround", new Vector3(0f, -0.5f, 6f), new Vector3(30f, 1f, 30f), MatKey.Floor);
            Kit.SolidBox(null, "UITestWall", new Vector3(0f, 1.5f, 14f), new Vector3(30f, 4f, 1f), MatKey.Wall);
            var go = new GameObject("CharacterManager");
            _cm = go.AddComponent<CharacterManager>();
            go.AddComponent<CharacterSwitcher>();
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<ShiftersCamera>() == null) cam.gameObject.AddComponent<ShiftersCamera>();
            var team = SessionActions.Team ?? CharacterGenerator.GenerateTeam(4242);
            var poses = new List<Pose>
            {
                new Pose(new Vector3(-1.8f, 0f, 4f), Quaternion.identity),
                new Pose(new Vector3(0f, 0f, 5.5f), Quaternion.identity),
                new Pose(new Vector3(1.8f, 0f, 4f), Quaternion.identity),
            };
            _cm.SpawnTeam(team, poses, 0);
            foreach (var c in _cm.Team) c.ExternalControl = true;
            yield return ctx.Wait(1.0f);
            ctx.Check(_cm.Team.Count == 3 && ui.Hud.Team.Card(0) != null && ui.Hud.Team.Card(0).gameObject.activeSelf, "HUD team cards bound");

            var active = _cm.Active;
            GameEvents.RaiseObjective("Get everyone to the EXIT elevator");
            GameEvents.RaisePrompt(PromptInfo.Make("E", "Pull lever"));
            GameEvents.RaiseCrosshair(true);
            GameEvents.RaisePerspectiveChanged(PerspectiveMask.Strength | PerspectiveMask.Weapon, active);
            GameEvents.RaiseTimer(2.4f);
            GameEvents.RaiseProgress(0.62f, "HACKING TERMINAL");
            GameEvents.RaiseBanner("CHECKPOINT", "HR has saved your progress. Probably.", BannerStyle.Checkpoint);
            GameEvents.RaiseToast("TEAM: FOLLOW ME", UIColors.Accent);
            GameEvents.RaiseToast("Needs STR 7 — Dr. Kettle has STR 1", UIColors.Danger);
            GameEvents.RaiseHint("Something heavy on that plate might help. Something... teammate-shaped.");
            GameEvents.RaiseTutorialShow(new TutorialMessage
            {
                Id = "uitest_switch", Title = "SWITCH CHARACTERS",
                Body = "Hold RIGHT SHIFT to open the switch wheel, or press 1 / 2 / 3 to shift instantly.",
                Keys = new[] { "RShift", "1", "2", "3" }, Duration = 8f,
            });
            _cm.SetAIMode(TeamAIMode.Follow);
            if (_cm.Get(1) != null) GameEvents.RaiseSpeech(_cm.Get(1), "Finally, some respect.");
            if (_cm.Get(2) != null) GameEvents.RaiseSpeech(_cm.Get(2), "I have a theory.");
            if (active != null) { active.Damage(28f, Vector3.zero, "uitest"); }
            yield return ctx.Wait(0.75f);
            ctx.Check(ui.Hud.Tutorial.Visible, "tutorial popup visible");
            ctx.Check(ui.Banners.Showing && ui.Banners.CurrentTitle == "CHECKPOINT", "banner showing");
            ctx.Check(ui.Toasts.ActiveCount >= 2, "toasts stacked (" + ui.Toasts.ActiveCount + ")");
            ctx.Check(ui.Hud.Speech.ActiveCount == 2, "two speech bubbles (" + ui.Hud.Speech.ActiveCount + ")");
            ctx.Shot("07_hud_everything");

            GameEvents.RaisePrompt(PromptInfo.MakeBlocked("F", "Pick up Big Steve", "TOO HEAVY! Needs STR 7 - yours: 3"));
            GameEvents.RaiseTimer(8f);
            yield return ctx.Wait(0.4f);
            ctx.Shot("08_hud_blocked_prompt");

            ui.Hud.Tutorial.Hide();
            yield return ctx.Wait(0.5f);
            ctx.Check(!ui.Hud.Tutorial.Visible, "tutorial popup hides (skip)");
            GameEvents.RaiseTimer(0f);
            GameEvents.RaiseProgress(-1f, null);
            GameEvents.RaisePrompt(null);
            GameEvents.RaiseCrosshair(false);
            ui.Banners.Clear();
            ui.Toasts.Clear();
            yield return ctx.Wait(0.2f);
        }

        // ------------------------------------------------------------------ switcher wheel
        static IEnumerator Switcher(DevPlayContext ctx, UIManager ui)
        {
            var sw = CharacterSwitcher.Instance;
            ctx.Check(sw != null, "CharacterSwitcher present");
            if (sw == null || _cm == null) yield break;
            sw.Open();
            yield return ctx.Wait(0.08f);
            ctx.Check(ui.Wheel.IsOpen, "wheel opens with the switcher");
            ctx.Shot("09a_switcher_flyout");
            yield return ctx.Wait(0.45f);
            ctx.Check(ui.Wheel.Highlight == _cm.ActiveIndex, "wheel highlights the active slot first");
            ctx.Shot("09_switcher_slot1");
            for (int s = 1; s < 3; s++)
            {
                sw.Highlight(s);
                yield return ctx.Wait(0.3f);
                ctx.Check(ui.Wheel.Highlight == s && ui.Wheel.Card(s).Highlighted, "wheel highlight follows slot " + (s + 1));
                ctx.Shot("09_switcher_slot" + (s + 1));
            }
            sw.Close(false);
            yield return ctx.Wait(0.4f);
            ctx.Check(!ui.Wheel.IsOpen, "wheel closes on cancel");

            var ko = _cm.Get(1);
            var lost = _cm.Get(2);
            if (ko != null) ko.KnockOut("uitest bonk");
            if (lost != null) lost.Lose("the void");
            yield return ctx.Wait(0.5f);
            ui.Banners.Clear();
            sw.Open();
            yield return ctx.Wait(0.5f);
            ctx.Check(!ui.Wheel.Card(1).Available && !ui.Wheel.Card(2).Available, "KO'd and LOST cards are unavailable");
            sw.Highlight(1);
            yield return ctx.Wait(0.12f);
            sw.Close(true);
            yield return ctx.Wait(0.12f);
            ctx.Check(sw.IsOpen && ui.Wheel.IsOpen, "confirming an unavailable card keeps the wheel open");
            ctx.Shot("10_switcher_ko_lost_deny");
            sw.Close(false);
            yield return ctx.Wait(0.4f);
            if (ko != null) ko.Revive();
            yield return ctx.Wait(0.3f);
            ui.Banners.Clear();
        }

        // ------------------------------------------------------------------ pause
        static IEnumerator Pause(DevPlayContext ctx, UIManager ui)
        {
            ui.OpenPause();
            yield return ctx.Wait(0.5f);
            ctx.Check(ui.IsPaused && TimeController.IsPaused && ui.Top == ui.Pause, "pause opens and pauses time");
            ctx.Check(ShiftersInput.Context == InputContext.Menu, "menu context while paused");
            ctx.Shot("11_pause");
            ui.Pause.Find("SETTINGS").Click();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.Settings && TimeController.IsPaused, "settings from pause keeps the game paused");
            ui.Settings.OnBack();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.Pause, "settings BACK returns to pause");
            ui.Pause.Find("RESTART CHECKPOINT").Click();
            ctx.Check(SessionActions.Last == "RestartCheckpoint" && !ui.IsPaused && !TimeController.IsPaused, "RESTART CHECKPOINT unpauses and restarts (dry run)");
            ui.OpenPause();
            yield return ctx.Wait(0.2f);
            ui.Pause.Find("RESTART TUTORIAL").Click();
            ctx.Check(SessionActions.Last == "RestartTutorial", "RESTART TUTORIAL wired");
            ui.OpenPause();
            yield return ctx.Wait(0.2f);
            ui.Pause.Find("RESUME").Click();
            yield return ctx.Wait(0.2f);
            ctx.Check(!ui.IsPaused && !TimeController.IsPaused && ShiftersInput.Context == InputContext.Gameplay && !ui.AnyMenuOpen, "RESUME restores gameplay");
        }

        // ------------------------------------------------------------------ level complete / wiped / victory
        static IEnumerator EndScreens(DevPlayContext ctx, UIManager ui)
        {
            var r = new LevelResult
            {
                LevelIndex = 0, Time = 187.4f, Survivors = 2,
                LeftBehind = new[] { _cm != null && _cm.Get(2) != null ? _cm.Get(2).DisplayName : "Gary" },
                Switches = 14, Restarts = 3,
            };
            GameEvents.RaiseLevelCompleted(r);
            yield return ctx.Wait(1.9f);
            ctx.Check(ui.Top == ui.LevelComplete && ui.LevelComplete.Result.Survivors == 2, "level complete screen on LevelCompleted");
            ctx.Check(!string.IsNullOrEmpty(ui.LevelComplete.Review), "HR review line: " + ui.LevelComplete.Review);
            ctx.Shot("12_level_complete");
            ui.LevelComplete.NextButton.Click();
            ctx.Check(SessionActions.Last == "NextLevel", "NEXT LEVEL wired");
            ui.LevelComplete.RetryButton.Click();
            ctx.Check(SessionActions.Last == "RestartLevel", "RETRY wired");

            ui.ShowGameplayHUD();
            GameEvents.RaiseTeamWiped();
            yield return ctx.Wait(1.3f);
            ctx.Check(ui.Top == ui.TeamWipedScreen, "team wiped screen on TeamWiped");
            ctx.Shot("13_team_wiped");
            ui.TeamWipedScreen.CheckpointButton.Click();
            ctx.Check(SessionActions.Last == "RestartCheckpoint", "team wiped RESTART CHECKPOINT wired");
            ui.TeamWipedScreen.MenuButton.Click();
            ctx.Check(SessionActions.Last == "MainMenu", "team wiped MAIN MENU wired");

            // victory through the real main-menu path (GameSession.ShowVictory after L5)
            if (_cm != null) Object.Destroy(_cm.gameObject);
            var cam = Camera.main;
            var scam = cam != null ? cam.GetComponent<ShiftersCamera>() : null;
            if (scam != null) Object.Destroy(scam);
            yield return null;
            SessionActions.ShowVictory = true;
            ui.ShowMainMenu();
            yield return ctx.Wait(1.6f);
            ctx.Check(ui.Top == ui.Victory, "victory screen opens over the main menu when ShowVictory is set");
            ctx.Check(!SessionActions.ShowVictory, "ShowVictory consumed");
            ctx.Shot("14_victory");
            ui.Victory.CreditsButton.Click();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.Credits, "victory CREDITS opens credits");
            ui.Credits.OnBack();
            yield return ctx.Wait(0.3f);
            ui.Victory.MenuButton.Click();
            yield return ctx.Wait(0.3f);
            ctx.Check(ui.Top == ui.MainMenu, "victory MAIN MENU returns to the main menu");
        }

        // ------------------------------------------------------------------ helpers
        static void Move(EventSystem es, MoveDirection dir)
        {
            if (es == null || es.currentSelectedGameObject == null) return;
            var data = new AxisEventData(es) { moveDir = dir, moveVector = dir == MoveDirection.Down ? Vector2.down : dir == MoveDirection.Up ? Vector2.up : dir == MoveDirection.Left ? Vector2.left : Vector2.right };
            ExecuteEvents.Execute(es.currentSelectedGameObject, data, ExecuteEvents.moveHandler);
        }

        static void Submit(EventSystem es)
        {
            if (es == null || es.currentSelectedGameObject == null) return;
            ExecuteEvents.Execute(es.currentSelectedGameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
        }

        static Dictionary<string, string> BackupPrefs()
        {
            var d = new Dictionary<string, string>();
            foreach (var k in PrefKeys)
            {
                if (!PlayerPrefs.HasKey(k)) continue;
                if (k.StartsWith("shifters_best_")) d[k] = "f:" + PlayerPrefs.GetFloat(k).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                else if (k == "shifters_completed") d[k] = "i:" + PlayerPrefs.GetInt(k);
                else d[k] = "s:" + PlayerPrefs.GetString(k);
            }
            return d;
        }

        static void RestorePrefs(Dictionary<string, string> d)
        {
            foreach (var k in PrefKeys)
            {
                if (!d.TryGetValue(k, out var v)) { PlayerPrefs.DeleteKey(k); continue; }
                string body = v.Substring(2);
                if (v.StartsWith("f:")) PlayerPrefs.SetFloat(k, float.Parse(body, System.Globalization.CultureInfo.InvariantCulture));
                else if (v.StartsWith("i:")) PlayerPrefs.SetInt(k, int.Parse(body));
                else PlayerPrefs.SetString(k, body);
            }
            PlayerPrefs.Save();
        }
    }
}
