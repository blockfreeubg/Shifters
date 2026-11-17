// =====================================================================================
//  SHIFTERS - IntegrationFlow.cs   [INTEGRATION - the real game flow through scenes]
//  devplay.sh <proj> int_flow -devScene Assets/Shifters/Scenes/MainMenu.unity -devSetup Shifters.IntegrationFlow.MenuToLevel -devSeconds 1
//  MainMenu scene -> NEW RUN -> Team Reveal (3 cards, portraits) -> START SHIFT (LoadLevel 0 = L1; when L1 is not
//  built yet the sandbox scene is loaded through SceneLoader instead) -> level starts (team, HUD, Gameplay
//  context) -> pause -> MAIN MENU button -> back in the menu with time running and the Menu context.
//  Buttons are clicked through the UI's own UIButton.Click (reflection: the UI module owns those types).
// =====================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class IntegrationFlow
    {
        public static IEnumerator MenuToLevel(DevPlayContext ctx)
        {
            E2E.Ctx = ctx;
            var prefs = E2EPrefsBackup.Capture();
            E2E.Subscribe();
            try
            {
                var ui = UIManager.Instance;
                yield return E2E.Until(() => (ui = UIManager.Instance) != null && Top(ui) == "MainMenuScreen", 5f);
                ctx.Check(Top(ui) == "MainMenuScreen", "main menu shown in the MainMenu scene (" + Top(ui) + ")");
                ctx.Check(ShiftersInput.Context == InputContext.Menu, "context Menu in the main menu");
                ctx.Check(Mathf.Abs(Time.timeScale - 1f) < 0.01f, "time runs in the menu");
                yield return ctx.Wait(1.5f);
                ctx.Shot("m1_main_menu");

                // NEW RUN -> team reveal
                ctx.Check(Click(ui, "MainMenu", "NEW RUN"), "clicked NEW RUN");
                yield return E2E.Until(() => Top(ui) == "TeamRevealScreen", 3f);
                ctx.Check(Top(ui) == "TeamRevealScreen", "team reveal shown (" + Top(ui) + ")");
                var gs = GameSession.Instance;
                ctx.Check(gs != null && gs.Team != null && gs.Team.Members != null && gs.Team.Members.Length == 3, "NEW RUN generated a team");
                var ps = PortraitStudio.Instance;
                yield return ctx.Wait(1.5f);
                for (int i = 0; i < 3; i++) ctx.Check(ps != null && ps.GetPortrait(i) != null, "team reveal portrait " + i);
                ctx.Shot("m2_team_reveal");

                // START SHIFT -> level 1 (or the sandbox while L1 is not built)
                var l1 = LevelCatalog.Get(0);
                bool haveL1 = l1 != null && SceneLoader.CanLoad(l1.SceneName);
                string expectScene = haveL1 ? l1.SceneName : "L0_Sandbox";
                if (haveL1) ctx.Check(Click(ui, "TeamReveal", "START SHIFT"), "clicked START SHIFT");
                else
                {
                    ctx.Log("L1 scene not built yet - START is replaced by loading L0_Sandbox (SceneLoader)");
                    ctx.Check(E2E.Truthy(E2E.Member(Screen(ui, "TeamReveal"), "StartButton")), "START SHIFT button exists");
                    var op = SceneLoader.Begin("L0_Sandbox", out string err);
                    ctx.Check(op != null, "sandbox scene load started (" + err + ")");
                }
                yield return E2E.Until(() => SceneManager.GetActiveScene().name == expectScene, 20f);
                ctx.Check(SceneManager.GetActiveScene().name == expectScene, "scene " + expectScene + " loaded (" + SceneManager.GetActiveScene().name + ")");
                yield return E2E.Until(() => LevelManager.Instance != null && LevelManager.Instance.IsStarted && CharacterManager.Instance != null && CharacterManager.Instance.Team.Count == 3, 10f);
                ctx.Check(E2E.LastUntilOk, "LevelManager started and spawned the team");
                yield return E2E.Until(() => ShiftersInput.Context == InputContext.Gameplay, 4f);
                ctx.Check(ShiftersInput.Context == InputContext.Gameplay, "context Gameplay in the level (" + ShiftersInput.Context + ")");
                ui = UIManager.Instance;
                var hud = E2E.Member(ui, "HudCanvas") as Canvas;
                ctx.Check(hud != null && hud.enabled && !ui.AnyMenuOpen, "HUD visible, no menu open");
                ctx.Check(haveL1 ? gs.LevelIndex == 0 : true, "session level index 0 (" + gs.LevelIndex + ")");
                ctx.Check(Mathf.Abs(Time.timeScale - 1f) < 0.01f, "time scale 1 in the level");
                yield return ctx.Wait(2.5f);
                ctx.Shot("m3_level_start");

                // pause -> MAIN MENU button
                ui.OpenPause();
                yield return ctx.Wait(0.5f);
                ctx.Check(Top(ui) == "PauseScreen" && TimeController.IsPaused, "pause screen open, paused");
                ctx.Shot("m4_pause");
                ctx.Check(Click(ui, "Pause", "MAIN MENU"), "clicked MAIN MENU in the pause screen");
                yield return E2E.Until(() => SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene && Top(UIManager.Instance) == "MainMenuScreen", 20f);
                ui = UIManager.Instance;
                ctx.Check(SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene, "back in the MainMenu scene");
                ctx.Check(Top(ui) == "MainMenuScreen", "main menu screen on top (" + Top(ui) + ")");
                ctx.Check(!TimeController.IsPaused && Mathf.Abs(Time.timeScale - 1f) < 0.01f, "time not left paused after quitting to the menu (" + Time.timeScale + ")");
                ctx.Check(ShiftersInput.Context == InputContext.Menu, "context Menu after quitting (" + ShiftersInput.Context + ")");
                ctx.Check(CharacterManager.Instance == null && LevelManager.Instance == null, "level singletons gone with the level scene");
                yield return ctx.Wait(1.5f);
                ctx.Shot("m5_back_in_menu");
            }
            finally
            {
                E2E.Unsubscribe();
                prefs.Restore();
            }
        }

        static string Top(UIManager ui) => ui == null ? "null" : E2E.TypeName(E2E.Member(ui, "Top"));

        static Component Screen(UIManager ui, string screenProperty) => E2E.Member(ui, screenProperty) as Component;

        /// <summary>Clicks the UIButton named "Btn_&lt;label&gt;" under the given UIManager screen (UIButton.Click).</summary>
        static bool Click(UIManager ui, string screenProperty, string label)
        {
            var screen = Screen(ui, screenProperty);
            if (screen == null) return false;
            foreach (var t in screen.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Btn_" + label) continue;
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb == null || mb.GetType().Name != "UIButton") continue;
                    var m = mb.GetType().GetMethod("Click");
                    if (m == null) return false;
                    m.Invoke(mb, null);
                    return true;
                }
            }
            return false;
        }
    }
}
