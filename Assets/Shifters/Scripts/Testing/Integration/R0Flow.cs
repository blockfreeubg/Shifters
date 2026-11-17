// =====================================================================================
//  SHIFTERS - R0Flow.cs   [INTEGRATION - R0 "Story wrapper" campaign flow through real scenes + UI buttons]
//  devplay.sh <proj> r0_flow -devScene Assets/Shifters/Scenes/MainMenu.unity -devSetup Shifters.R0Flow.Run -devSeconds 1
//  MainMenu -> CAMPAIGN -> YOUR NEW HIRES (team T) -> START SHIFT -> L1 (CH01, prologue skipped) -> pause MAIN MENU ->
//  menu shows CONTINUE + team T -> CONTINUE -> L1 again with team T -> MAIN MENU -> SHIFT SCHEDULE (unbuilt rows greyed).
//  Watches GameSession.Team every frame and logs when (scene / top screen) the team object is replaced.
//  PlayerPrefs: BotPrefsGuard (all shifters_* keys incl. campaign + run) restored at the end.
// =====================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class R0Flow
    {
        static TeamData _watched;
        static int _replacements;

        static string Names(TeamData t)
        {
            if (t == null || t.Members == null) return "(none)";
            var s = "";
            foreach (var m in t.Members) s += (s.Length > 0 ? ", " : "") + (m != null ? m.DisplayName : "?");
            return s + " #" + t.Seed;
        }

        static string Top(UIManager ui) => ui == null ? "null" : E2E.TypeName(E2E.Member(ui, "Top"));

        static bool Click(UIManager ui, string screenProperty, string label)
        {
            var screen = E2E.Member(ui, screenProperty) as Component;
            if (screen == null) return false;
            foreach (var t in screen.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Btn_" + label) continue;
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                {
                    var m = mb.GetType().GetMethod("Click", System.Type.EmptyTypes);
                    if (m != null) { m.Invoke(mb, null); return true; }
                }
            }
            return false;
        }

        static IEnumerator Watch(DevPlayContext ctx, Ctl c)
        {
            while (!c.Stop)
            {
                var gs = GameSession.Instance;
                var t = gs != null ? gs.Team : null;
                if (!ReferenceEquals(t, _watched))
                {
                    _replacements++;
                    ctx.Log("TEAMWATCH f" + Time.frameCount + " scene=" + SceneManager.GetActiveScene().name + " top=" + Top(UIManager.Instance)
                            + " team " + Names(_watched) + " -> " + Names(t) + " (gs id " + (gs != null ? gs.GetHashCode() : 0) + ")");
                    _watched = t;
                }
                yield return null;
            }
        }

        class Ctl { public bool Stop; }

        static IEnumerator WaitLevel(DevPlayContext ctx, string label, float skipAfter = 1.5f)
        {
            yield return E2E.Until(() => SceneManager.GetActiveScene().name == LevelCatalog.Get(0).SceneName
                                         && LevelManager.Instance != null && LevelManager.Instance.IsStarted, 30f);
            ctx.Check(E2E.LastUntilOk, label + ": L1 loaded and started");
            yield return ctx.Wait(skipAfter);
            var sp = StoryPlayer.Instance;
            ctx.Log(label + ": story blocking=" + (sp != null && sp.IsBlocking));
            for (int i = 0; i < 6 && sp != null && sp.IsBlocking; i++) { sp.Skip(); yield return ctx.Wait(0.5f); }
            yield return ctx.Wait(1f);
        }

        public static IEnumerator Run(DevPlayContext ctx)
        {
            E2E.Ctx = ctx;
            BotPrefsGuard.Push(ctx);
            PlayerPrefs.DeleteKey("shifters_campaign"); PlayerPrefs.DeleteKey("shifters_run");
            CampaignDirector.Instance?.ReloadSave();
            var ctl = new Ctl();
            _watched = GameSession.Instance != null ? GameSession.Instance.Team : null; _replacements = 0;
            var runner = new GameObject("R0FlowWatch").AddComponent<R0FlowRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Watch(ctx, ctl));
            UIManager ui = null;
            yield return E2E.Until(() => (ui = UIManager.Instance) != null && Top(ui) == "MainMenuScreen", 8f);
            ctx.Check(Top(ui) == "MainMenuScreen", "main menu shown (" + Top(ui) + ")");
            yield return ctx.Wait(1f);
            ctx.Check(Click(ui, "MainMenu", "CAMPAIGN"), "clicked CAMPAIGN");
            yield return E2E.Until(() => Top(ui) == "TeamRevealScreen", 4f);
            ctx.Check(Top(ui) == "TeamRevealScreen", "YOUR NEW HIRES shown (" + Top(ui) + ")");
            var gs = GameSession.Instance;
            var hired = gs != null ? gs.Team : null;
            int hiredSeed = hired != null ? hired.Seed : -1;
            ctx.Log("hired team: " + Names(hired));
            yield return ctx.Wait(1f);
            ctx.Shot("r0_01_hires");
            ctx.Check(Click(ui, "TeamReveal", "START SHIFT"), "clicked START SHIFT");
            yield return WaitLevel(ctx, "new campaign");
            gs = GameSession.Instance;
            ctx.Check(gs != null && gs.Seed == hiredSeed, "L1 plays the hired team (" + Names(gs?.Team) + ")");
            ctx.Check(CampaignDirector.Instance != null && CampaignDirector.Instance.IsCampaign && CampaignDirector.Instance.Current?.Id == "CH01", "campaign CH01 running");
            ctx.Log("WORLDCOLOR campaign L1: Enabled=" + WorldColor.Enabled + " SettingAllows=" + WorldColor.SettingAllows + " EffectActive=" + WorldColor.EffectActive
                    + " Base=" + WorldColor.Base + " Current=" + WorldColor.Current + " _ShiftersWorldParams=" + Shader.GetGlobalVector("_ShiftersWorldParams")
                    + " _ShiftersWorldColor=" + Shader.GetGlobalFloat("_ShiftersWorldColor") + " meter=" + CampaignDirector.Instance?.Meter);
            ctx.Check(WorldColor.EffectActive && WorldColor.Current < 0.6f, "campaign CH01 greys the world (R0 'world color on L1-L5'): EffectActive=" + WorldColor.EffectActive + ", Current=" + WorldColor.Current);
            ctx.Shot("r0_02_l1");
            // pause -> MAIN MENU (the pause button's action)
            ui.OpenPause();
            yield return ctx.Wait(0.5f);
            ctx.Log("pause top=" + Top(ui));
            ctx.Check(Click(ui, "Pause", "MAIN MENU"), "clicked MAIN MENU in the pause screen");
            yield return E2E.Until(() => SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene && Top(ui) == "MainMenuScreen", 20f);
            ctx.Check(E2E.LastUntilOk, "back in the main menu");
            yield return ctx.Wait(1.5f);
            gs = GameSession.Instance;
            ctx.Check(gs != null && gs.Seed == hiredSeed, "menu after quitting keeps the hired team (now " + Names(gs?.Team) + ", hired #" + hiredSeed + ")");
            ctx.Check(CampaignDirector.Instance != null && CampaignDirector.Instance.HasSave, "campaign save exists -> CONTINUE");
            ctx.Shot("r0_03_menu_continue");
            ctx.Check(Click(ui, "MainMenu", "CONTINUE"), "clicked CONTINUE");
            yield return WaitLevel(ctx, "continue");
            gs = GameSession.Instance;
            ctx.Check(gs != null && gs.Seed == hiredSeed, "CONTINUE plays the hired team (" + Names(gs?.Team) + ", hired #" + hiredSeed + ")");
            ctx.Check(CampaignDirector.Instance != null && CampaignDirector.Instance.Current?.Id == "CH01", "CONTINUE resumed CH01");
            ctx.Shot("r0_04_continue_l1");
            ui.OpenPause();
            yield return ctx.Wait(0.5f);
            Click(ui, "Pause", "MAIN MENU");
            yield return E2E.Until(() => SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene && Top(ui) == "MainMenuScreen", 20f);
            yield return ctx.Wait(1f);
            ctx.Check(Click(ui, "MainMenu", "SHIFT SCHEDULE"), "clicked SHIFT SCHEDULE");
            yield return E2E.Until(() => Top(ui) == "ShiftScheduleScreen", 4f);
            ctx.Check(Top(ui) == "ShiftScheduleScreen", "SHIFT SCHEDULE shown (" + Top(ui) + ")");
            yield return ctx.Wait(1f);
            ctx.Shot("r0_05_schedule");
            ctx.Log("team replacements seen: " + _replacements);
            ctl.Stop = true;
            Object.Destroy(runner.gameObject);
            BotPrefsGuard.Pop(ctx);
        }
    }
}
