// =====================================================================================
//  SHIFTERS - UIHudTests.cs   (ui_hud agent, DevPlay)
//  Drives every campaign in-chapter widget with fake GameEvents over the real L1 level
//  (HUD visible) and screenshots each state at 1920x1080 and 1280x720. Asserts: widgets
//  react, no overlap with ui's HUD elements or the WebGL template toast corner, layering
//  (main 15 above HUD 10 / below menus 20; PiP 35 hidden while paused; card 101 above
//  the fader 100), skip chip progress, chapter card timing on the fade.
//  Run: devplay.sh <clone> uihud -devSetup Shifters.UIHudTests.RunAll
//         -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class UIHudTests
    {
        static DevPlayContext _ctx;

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            _ctx = ctx;
            // wait for the level to spawn the team and show the HUD
            float t0 = Time.realtimeSinceStartup;
            while ((CharacterManager.Instance == null || CharacterManager.Instance.Team == null || CharacterManager.Instance.Team.Count < 3
                    || UIManager.Instance == null || !UIManager.Instance.HudVisible) && Time.realtimeSinceStartup - t0 < 30f)
                yield return null;
            yield return ctx.Wait(1.5f);
            ScreenFader.Set(0f);
            GameEvents.RaiseTutorialHide();
            var ui = UIManager.Instance;
            var hud = CampaignHud.Instance;
            ctx.Check(ui != null && ui.HudVisible, "L1 running with the gameplay HUD visible");
            ctx.Check(hud != null, "CampaignHud self-created at AfterSceneLoad");
            if (ui == null || hud == null) yield break;
            var team = CharacterManager.Instance.Team;
            foreach (var c in team) if (c != null) c.ExternalControl = true;

            // ---------------------------------------------------------------- layering
            ctx.Check(hud.MainCanvas.sortingOrder > ui.HudCanvas.sortingOrder && hud.MainCanvas.sortingOrder < ui.MenuCanvas.sortingOrder,
                "main campaign canvas above HUD (" + ui.HudCanvas.sortingOrder + ") and below menus (" + ui.MenuCanvas.sortingOrder + ")");
            ctx.Check(hud.PipCanvas.sortingOrder > ui.MenuCanvas.sortingOrder, "PiP canvas above Level Complete (menu canvas)");
            ctx.Check(hud.CardCanvas.sortingOrder > 100, "chapter card canvas above the ScreenFader (100)");

            // ---------------------------------------------------------------- R0: dialogue (intern)
            GameEvents.RaiseObjective("Get everyone to the EXIT elevator");
            yield return ctx.Wait(0.5f);
            var brute = Find(team, Archetype.Brute);
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "BRUTE", SpeakerName = brute != null ? brute.DisplayName : "BRUTE",
                Text = "Can I punch the government? Asking for a friend. The friend is me." });
            yield return ctx.Wait(0.4f);
            ctx.Check(hud.Dialogue.Visible && hud.Dialogue.Typing, "DialogueBox visible and typing");
            yield return ctx.Wait(1.6f);
            ctx.Check(!hud.Dialogue.Typing, "typewriter finished a 66-char line within 2 s (45 chars/s)");
            CheckNoHudOverlap("DialogueBox", hud.Dialogue.Rt, ui);
            yield return Shots("01_dialogue_intern");
            GameEvents.RaiseStoryLineHidden();
            yield return ctx.Wait(0.4f);
            ctx.Check(!hud.Dialogue.Visible, "StoryLineHidden hides the DialogueBox");

            // auto-hide when no Duration and no Hidden
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "SPRINTER", Text = "Already done!" });
            yield return ctx.Wait(0.2f);
            float dur = DialogueBox.DurationFor(hud.Dialogue.Current);
            ctx.Check(Mathf.Abs(dur - 2.5f / DialogueBox.SubtitleSpeed) < 0.01f, "DurationFor short line = 2.5 s");
            yield return ctx.Wait(dur + 0.8f);
            ctx.Check(!hud.Dialogue.Visible, "line without Duration auto-hides");

            // ---------------------------------------------------------------- R0: ME-TV PiP (Max) + radio (Percy)
            GameEvents.RaiseBroadcastChanged(true, "MAX");
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "MAX", SpeakerName = "MAX ME", Broadcast = true, Face = Expression.Smug,
                Text = "Teamwork is just three people being wrong together." });
            yield return ctx.Wait(1.4f);
            ctx.Check(hud.Pip.Visible && !hud.Pip.RadioSkin, "Max broadcast opens the ME-TV PiP");
            ctx.Check(hud.Pip.HasLiveTexture && VillainStudio.Instance != null && VillainStudio.Instance.IsLive, "PiP shows the live VillainStudio render texture");
            CheckNoHudOverlap("MeTvPip", hud.Pip.Rt, ui);
            yield return Shots("02_pip_max");
            GameEvents.RaiseStoryLineHidden();
            GameEvents.RaiseBroadcastChanged(false, "MAX");
            yield return ctx.Wait(1.2f);
            ctx.Check(!hud.Pip.Visible, "broadcast off closes the PiP");
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "PERCY", SpeakerName = "PERCY", Broadcast = true,
                Text = "Psst. Radio check. The vents smell like old espresso. Go left." });
            yield return ctx.Wait(1.4f);
            ctx.Check(hud.Pip.Visible && hud.Pip.RadioSkin, "Percy broadcast uses the radio skin");
            yield return Shots("03_pip_percy_radio");
            GameEvents.RaiseStoryLineHidden();
            yield return ctx.Wait(1.2f);
            ctx.Check(!hud.Pip.Visible, "PiP closes after the last line without a BroadcastChanged(on)");

            // ---------------------------------------------------------------- R0: letterbox + skip chip (blocking)
            GameEvents.RaiseCutsceneChanged(true);
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "MAX", SpeakerName = "MAX ME", Face = Expression.Angry,
                Text = "You were supposed to SPLIT UP! That's how horror WORKS!" });
            CampaignHudCompat.SkipOverride = 0.55f;
            yield return ctx.Wait(1.6f);
            ctx.Check(hud.Letterbox.Amount01 > 0.99f, "letterbox bars fully in");
            ctx.Check(hud.Skip.Shown && Mathf.Abs(hud.Skip.Progress01 - 0.55f) < 0.01f, "skip chip shows hold progress");
            CheckNoOverlap("SkipChip vs DialogueBox", hud.Skip.Rt, hud.Dialogue.Rt);
            yield return Shots("04_letterbox_skip");
            CampaignHudCompat.SkipOverride = -1f;
            ctx.Check(CampaignHudCompat.HasRealSkip || CampaignHudCompat.SkipHeld01() == 0f, "SkipHeld01 bridge resolves (real seam: " + CampaignHudCompat.HasRealSkip + ")");
            GameEvents.RaiseStoryLineHidden();
            GameEvents.RaiseCutsceneChanged(false);
            yield return ctx.Wait(0.6f);
            ctx.Check(hud.Letterbox.Amount01 < 0.01f && !hud.Skip.Shown, "letterbox + chip leave after the scene");

            // ---------------------------------------------------------------- fixes: full-screen ME-TV (StoryPlayer.Cue tv) + banner hold
            GameEvents.RaiseBanner("THE TRAINING FACILITY", "Level 1 - Orientation Day", BannerStyle.Info);
            yield return ctx.Wait(0.3f);
            GameEvents.RaiseCutsceneChanged(true);
            yield return null;
            ctx.Check(hud.BannersHeld && ui.Banners != null && !ui.Banners.gameObject.activeSelf && hud.HeldBannerCount == 1,
                "level-title banner held back during the blocking scene (held " + hud.HeldBannerCount + ")");
            GameEvents.RaiseBanner("CHECKPOINT", null, BannerStyle.Checkpoint);
            ctx.Check(hud.HeldBannerCount == 2, "banners raised during the scene are buffered");
            InvokeCue("tv", "MAX", "ME NEWS");
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "MAX", SpeakerName = "MAX ME", Broadcast = true, Face = Expression.Smug,
                Text = "Good morning, citizens of ME. Today's forecast: grey, with a chance of me." });
            yield return ctx.Wait(1.4f);
            ctx.Check(hud.Pip.FullScreen && hud.Pip.Visible && hud.Pip.Channel == "ME NEWS" && hud.Pip.Rt.localScale.x > 2.5f,
                "Cue(tv, MAX, ME NEWS) grows the PiP to full screen (scale " + hud.Pip.Rt.localScale.x.ToString("0.00") + ")");
            CheckNoOverlap("FullScreen TV vs DialogueBox", hud.Pip.Rt, hud.Dialogue.Rt);
            CheckInsideScreen("FullScreen TV", hud.Pip.Rt);
            yield return Shots("04b_fullscreen_tv");
            InvokeCue("tv", "MAX", "ME WEATHER");
            yield return ctx.Wait(0.5f);
            ctx.Check(hud.Pip.FullScreen && hud.Pip.Channel == "ME WEATHER", "channel change while full screen");
            InvokeCue("tv", "MAX", "off");
            GameEvents.RaiseStoryLineHidden();
            yield return ctx.Wait(0.8f);
            ctx.Check(!hud.Pip.FullScreen, "Cue(tv, off) ends the full-screen TV");
            GameEvents.RaiseCutsceneChanged(false);
            yield return ctx.Wait(0.5f);
            ctx.Check(!hud.BannersHeld && ui.Banners.gameObject.activeSelf && ui.Banners.Showing && ui.Banners.CurrentTitle == "THE TRAINING FACILITY",
                "held banner replays after the scene (" + ui.Banners.CurrentTitle + ")");
            yield return Shots("04c_banner_after_scene");
            float tb = Time.realtimeSinceStartup;
            while ((ui.Banners.Showing || ui.Banners.Queued > 0) && Time.realtimeSinceStartup - tb < 10f) yield return null;
            ctx.Check(!hud.Pip.Visible, "PiP closed after the full-screen broadcast");
            // CutsceneChanged(false) also ends a full-screen TV that never got its "off"
            GameEvents.RaiseCutsceneChanged(true);
            InvokeCue("tv", "MAX", "ME SPORTS");
            yield return ctx.Wait(0.6f);
            GameEvents.RaiseCutsceneChanged(false);
            yield return ctx.Wait(1.2f);
            ctx.Check(!hud.Pip.FullScreen && !hud.Pip.Visible, "scene end closes a full-screen TV left on");

            // ---------------------------------------------------------------- R1: boss bar
            var boss = new BossStatus { BossId = "downsizer", Name = "The Downsizer", Title = "Human Resources", BarLabel = "QUOTA: FIRED 0/3",
                Phase = 0, PhaseCount = 3, Hits = 0, HitsRequired = 2, State = BossState.Fighting, Visible = true };
            GameEvents.RaiseBossChanged(boss);
            yield return ctx.Wait(0.7f);
            ctx.Check(hud.BossBar.Shown, "BossChanged(Visible) shows the boss bar");
            CheckNoHudOverlap("BossBar", hud.BossBar.Rt, ui);
            boss.Hits = 1; boss.State = BossState.Vulnerable;
            GameEvents.RaiseBossChanged(boss);
            yield return ctx.Wait(0.5f);
            yield return Shots("05_boss_vulnerable");
            GameEvents.RaiseCutsceneChanged(true);
            yield return ctx.Wait(0.4f);
            ctx.Check(!hud.BossBar.Shown, "boss bar hides during a blocking scene");
            GameEvents.RaiseCutsceneChanged(false);
            yield return ctx.Wait(0.4f);
            ctx.Check(hud.BossBar.Shown, "boss bar returns after the scene");
            boss.Phase = 1; boss.Hits = 0; boss.HitsRequired = 3; boss.State = BossState.Fighting; boss.BarLabel = "QUOTA: FIRED 1/3";
            GameEvents.RaiseBossChanged(boss);
            yield return ctx.Wait(0.5f);
            // timed door at the same time: the bar drops under the timer
            GameEvents.RaiseTimer(9.5f);
            yield return ctx.Wait(0.5f);
            CheckNoOverlap("BossBar vs HudTimer", hud.BossBar.Rt, ui.Hud.Timer.transform as RectTransform);
            yield return Shots("06_boss_phase2_timer");
            // kitfix REQ level6-1: a tutorial card during the fight pushes the dropped boss bar below the card
            GameEvents.RaiseTutorialShow(new TutorialMessage { Id = "uihud_boss_tut", Title = "BOSS FIGHT",
                Body = "BOSSES ONLY SEE WHO YOU'RE CONTROLLING. Park the others in BREAK AREAS. Open the wheel to slow time.",
                Keys = new[] { "RSHIFT" }, Duration = 0f });
            yield return ctx.Wait(0.8f);
            var tutRt = ui.Hud.Tutorial.transform as RectTransform;
            ctx.Check(ui.Hud.Tutorial.Visible, "tutorial card visible during the boss fight");
            CheckNoOverlap("BossBar vs HudTutorial (timer up)", hud.BossBar.Rt, tutRt);
            CheckNoOverlap("BossBar vs HudTimer (tutorial up)", hud.BossBar.Rt, ui.Hud.Timer.transform as RectTransform);
            CheckInsideScreen("BossBar under the tutorial card", hud.BossBar.Rt);
            yield return Shots("06b_boss_timer_tutorial");
            GameEvents.RaiseTimer(0f);
            yield return ctx.Wait(0.6f);
            ctx.Check(Mathf.Abs(hud.BossBar.Rt.anchoredPosition.y - CampaignHud.TopY) < 1f, "no timer: boss bar stays at the top above the tutorial card (y " + hud.BossBar.Rt.anchoredPosition.y.ToString("0") + ")");
            CheckNoOverlap("BossBar vs HudTutorial (no timer)", hud.BossBar.Rt, tutRt);
            yield return Shots("06c_boss_tutorial_notimer");
            GameEvents.RaiseTimer(9.5f);
            GameEvents.RaiseTutorialHide();
            yield return ctx.Wait(0.7f);
            ctx.Check(Mathf.Abs(hud.BossBar.Rt.anchoredPosition.y - (CampaignHud.TopY - CampaignHud.TimerDrop)) < 1f, "tutorial hidden: boss bar back under the timer (y " + hud.BossBar.Rt.anchoredPosition.y.ToString("0") + ")");
            GameEvents.RaiseTimer(0f);

            // ---------------------------------------------------------------- R1: chase meter (stacked under the boss bar)
            var chase = new ChaseStatus { Gap = 40f, MaxGap = 45f, Station = 0, StationCount = 3, Visible = true };
            GameEvents.RaiseChaseChanged(chase);
            yield return ctx.Wait(0.6f);
            ctx.Check(hud.Chase.Shown && hud.Chase.LabelText == "PORKCHOP: 40 M", "chase meter shows 'PORKCHOP: 40 M'");
            CheckNoOverlap("ChaseMeter vs BossBar", hud.Chase.Rt, hud.BossBar.Rt);
            CheckNoHudOverlap("ChaseMeter", hud.Chase.Rt, ui);
            chase.Gap = 7f; chase.Station = 1;
            GameEvents.RaiseChaseChanged(chase);
            yield return ctx.Wait(0.3f);
            yield return Shots("07_boss_and_chase_close");
            boss.State = BossState.Defeated; boss.Phase = 2; boss.Hits = 3;
            GameEvents.RaiseBossChanged(boss);
            chase.Busted = true;
            GameEvents.RaiseChaseChanged(chase);
            yield return ctx.Wait(0.5f);
            yield return Shots("08_boss_defeated_busted");
            yield return ctx.Wait(2.6f);
            ctx.Check(!hud.BossBar.Shown, "defeated boss bar hides itself");
            chase.Visible = false; GameEvents.RaiseChaseChanged(chase);
            yield return ctx.Wait(0.4f);
            ctx.Check(!hud.Chase.Shown, "ChaseStatus.Visible=false hides the meter");

            // ---------------------------------------------------------------- R1: exhibit toast
            GameEvents.RaiseExhibitAcquired("B");
            yield return ctx.Wait(1.0f);
            ctx.Check(hud.Exhibit.Shown && hud.Exhibit.HeadlineText == "EXHIBIT B ACQUIRED", "exhibit toast: EXHIBIT B ACQUIRED");
            CheckNoHudOverlap("ExhibitToast", hud.Exhibit.Rt, ui);
            yield return Shots("09_exhibit_b");
            yield return ctx.Wait(2.8f);
            ctx.Check(!hud.Exhibit.Shown, "exhibit toast leaves on its own");

            // ---------------------------------------------------------------- R1: role stamps + SCARED + objective memo
            GameEvents.RaiseStoryBeat("hud.roles.on");
            CampaignHudCompat.ScaredOverride = new HashSet<ShifterCharacter>();
            var spr = Find(team, Archetype.Sprinter);
            if (spr != null) CampaignHudCompat.ScaredOverride.Add(spr);
            GameEvents.RaiseObjective("Crack the vault. Act natural.");
            yield return ctx.Wait(0.8f);
            int bruteSlot = brute != null ? brute.Slot : 0, sprSlot = spr != null ? spr.Slot : 1;
            ctx.Check(hud.Badges.RoleShown(bruteSlot) == "THE MUSCLE", "Brute card stamped THE MUSCLE (got " + hud.Badges.RoleShown(bruteSlot) + ")");
            ctx.Check(hud.Badges.IsScaredShown(sprSlot), "SCARED badge on the Sprinter's card");
            ctx.Check(hud.Memo.Shown, "mid-level objective change stamps NEW ORDERS");
            for (int i = 0; i < 3; i++)
            {
                var rr = hud.Badges.RoleRect(i);
                if (rr != null && rr.gameObject.activeInHierarchy) CheckInsideScreen("role stamp " + i, rr);
            }
            yield return Shots("10_roles_scared_memo");
            CampaignHudCompat.ScaredOverride = null;
            GameEvents.RaiseStoryBeat("hud.roles.off");
            yield return ctx.Wait(0.4f);
            ctx.Check(hud.Badges.RoleShown(bruteSlot) == null && !hud.Badges.IsScaredShown(sprSlot), "stamps + badge clear");

            // ---------------------------------------------------------------- R2: WHO RULES TODAY?
            GameEvents.RaiseStoryBeat("hud.ruler.on");
            yield return ctx.Wait(0.6f);
            ctx.Check(hud.Ruler.Active, "hud.ruler.on shows WHO RULES TODAY?");
            CheckNoHudOverlap("RulerPrompt", hud.Ruler.Rt, ui);
            yield return Shots("11_ruler_prompt");
            ShifterCharacter elected = null;
            System.Action<ShifterCharacter> onPick = c => elected = c;
            CampaignRulerPrompt.RulerChosen += onPick;
            GameEvents.RaiseSwitcherOpenChanged(true);
            GameEvents.RaiseSwitcherSelectionChanged(sprSlot);
            yield return ctx.Wait(0.3f);
            ctx.Check(hud.Ruler.SubText.StartsWith("VOTE: "), "wheel hover reads VOTE: <NAME>");
            GameEvents.RaiseSwitcherOpenChanged(false);
            yield return ctx.Wait(0.6f);
            CampaignRulerPrompt.RulerChosen -= onPick;
            ctx.Check(elected != null && elected == hud.Ruler.Chosen, "closing the wheel on a slot elects that intern (RulerChosen raised)");
            yield return Shots("12_ruler_elected");
            GameEvents.RaiseStoryBeat("hud.ruler.off");

            // ---------------------------------------------------------------- R0: chapter card on the load fade
            CampaignChapterCard.PendingQuip = "Act natural. Like me. I'm always natural.";
            bool fadeDone = false;
            hud.StartCoroutine(Run(ScreenFader.FadeOut(0.35f, "THE FEDERAL COLOR RESERVE"), () => fadeDone = true));
            GameEvents.RaiseChapterCard("ACT II · SWING SHIFT", "SHIFT 07 · THE FEDERAL COLOR RESERVE");
            yield return ctx.Wait(1.0f);
            ctx.Check(hud.Card.Showing && hud.Card.QuipText.Length > 0, "chapter card shows act/title/quip");
            ctx.Check(fadeDone, "fader finished its fade-out under the card");
            yield return Shots("13_chapter_card");
            yield return ctx.Wait(2.6f);
            ctx.Check(hud.Card.Showing, "card holds while the screen is still black (loading)");
            hud.StartCoroutine(ScreenFader.FadeIn(0.35f));
            yield return ctx.Wait(0.8f);
            ctx.Check(!hud.Card.Showing, "card leaves when the fade-in starts");
            ScreenFader.Set(0f);
            hud.ShowChapterCard("ACT III · GRAVEYARD SHIFT", "SHIFT 10 · LIGHTS OUT", "Stick together. Find the switch.", null);
            yield return ctx.Wait(0.6f);
            yield return Shots("14_chapter_card_over_gameplay_noquip");
            hud.Card.Skip();
            yield return ctx.Wait(0.5f);
            ctx.Check(!hud.Card.Showing, "card skip works");

            // ---------------------------------------------------------------- layering vs pause
            GameEvents.RaiseBossChanged(new BossStatus { BossId = "porkchop", Name = "Porkchop", Title = "Federal Reserve Security", BarLabel = "OINK METER",
                Phase = 0, PhaseCount = 2, HitsRequired = 3, State = BossState.Fighting, Visible = true });
            GameEvents.RaiseBroadcastChanged(true, "MAX");
            GameEvents.RaiseStoryLineShown(new StoryLine { SpeakerId = "MAX", Broadcast = true, Text = "Pausing? Quitters pause." });
            yield return ctx.Wait(0.8f);
            ui.OpenPause();
            yield return ctx.Wait(0.6f);
            var pipGroup = hud.Pip.transform.parent.GetComponent<CanvasGroup>();
            ctx.Check(ui.IsPaused && pipGroup != null && pipGroup.alpha < 0.01f, "PiP hidden under the pause screen");
            yield return Shots("15_pause_layering");
            ui.ClosePause();
            yield return ctx.Wait(0.4f);
            ctx.Check(pipGroup != null && pipGroup.alpha > 0.99f, "PiP back after unpause");
            hud.ResetAll(true);
            yield return ctx.Wait(0.3f);
            ctx.Check(!hud.Pip.Visible && !hud.BossBar.Shown && !hud.Dialogue.Visible, "ResetAll clears every widget");
            ctx.Log("UIHudTests finished: " + ctx.Result.checksPassed + " passed, " + ctx.Result.checksFailed + " failed");
        }

        // ------------------------------------------------------------------ helpers
        static void InvokeCue(string kind, string target, string arg)
        {
            var f = typeof(StoryPlayer).GetField("Cue", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var d = f != null ? f.GetValue(null) as System.Action<string, string, string> : null;
            _ctx.Check(d != null, "StoryPlayer.Cue has subscribers");
            d?.Invoke(kind, target, arg);
        }

        static IEnumerator Run(IEnumerator e, System.Action done) { yield return e; done(); }

        static ShifterCharacter Find(IReadOnlyList<ShifterCharacter> team, Archetype a)
        {
            for (int i = 0; i < team.Count; i++) if (team[i] != null && team[i].Archetype == a) return team[i];
            return null;
        }

        // Layout checks are deferred to Shots(): batch Unity's Screen is 640x480 (4:3), which is not a target, so every
        // overlap / inside-screen check runs with the canvases laid out exactly like the 1920x1080 and 1280x720 shots.
        static readonly List<System.Action> _pending = new List<System.Action>();
        static Camera _cam;
        static int _sw, _sh;
        static string _at = "";

        static IEnumerator Shots(string name)
        {
            var cfg = _ctx.Config;
            int w = cfg.shotWidth, h = cfg.shotHeight;
            yield return null;
            RunPendingAt(1920, 1080);
            cfg.shotWidth = 1920; cfg.shotHeight = 1080; _ctx.Shot(name + "_1080");
            RunPendingAt(1280, 720);
            cfg.shotWidth = 1280; cfg.shotHeight = 720; _ctx.Shot(name + "_720");
            cfg.shotWidth = w; cfg.shotHeight = h;
            _pending.Clear();
            CampaignHud.Instance?.Pip.Relayout(true);
        }

        static void RunPendingAt(int w, int h)
        {
            var cam = Camera.main;
            if (cam == null) { var all = Object.FindObjectsByType<Camera>(); if (all.Length > 0) cam = all[0]; }
            var hud = CampaignHud.Instance;
            if (cam == null) { foreach (var a in _pending) a(); return; }
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var restore = new List<(Canvas c, RenderMode m, Camera wc, float pd)>();
            foreach (var c in Object.FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || !c.isActiveAndEnabled || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.4f);
            }
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            if (hud != null) { hud.Pip.Relayout(true); Canvas.ForceUpdateCanvases(); }
            _cam = cam; _sw = w; _sh = h; _at = " @" + w + "x" + h;
            if (hud != null && _pending.Count > 0)
            {
                var pr = hud.MainCanvas.transform as RectTransform;
                _ctx.Log("layout" + _at + ": campaign canvas " + pr.rect.width.ToString("0") + "x" + pr.rect.height.ToString("0") + " units, scale " + hud.MainCanvas.scaleFactor.ToString("0.000"));
            }
            try { foreach (var a in _pending) a(); }
            finally
            {
                _cam = null; _at = "";
                cam.targetTexture = prev;
                foreach (var r in restore) { r.c.renderMode = r.m; r.c.worldCamera = r.wc; r.c.planeDistance = r.pd; }
                RenderTexture.ReleaseTemporary(rt);
                Canvas.ForceUpdateCanvases();
            }
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            if (_cam != null) for (int i = 0; i < 4; i++) c[i] = RectTransformUtility.WorldToScreenPoint(_cam, c[i]);
            return Rect.MinMaxRect(Mathf.Min(c[0].x, c[2].x), Mathf.Min(c[0].y, c[2].y), Mathf.Max(c[0].x, c[2].x), Mathf.Max(c[0].y, c[2].y));
        }

        static int SW => _cam != null ? _sw : Screen.width;
        static int SH => _cam != null ? _sh : Screen.height;

        static void CheckNoOverlap(string what, RectTransform a, RectTransform b) => _pending.Add(() => CheckNoOverlapNow(what, a, b));
        static void CheckInsideScreen(string what, RectTransform a) => _pending.Add(() => CheckInsideScreenNow(what, a));
        static void CheckNoHudOverlap(string what, RectTransform w, UIManager ui) => _pending.Add(() => CheckNoHudOverlapNow(what, w, ui));

        static void CheckNoOverlapNow(string what, RectTransform a, RectTransform b)
        {
            if (a == null || b == null) { _ctx.Check(false, what + ": missing rect"); return; }
            var ra = ScreenRect(a); var rb = ScreenRect(b);
            _ctx.Check(!ra.Overlaps(rb), what + " do not overlap" + _at + " (" + Fmt(ra) + " / " + Fmt(rb) + ")");
        }

        static void CheckInsideScreenNow(string what, RectTransform a)
        {
            var r = ScreenRect(a);
            _ctx.Check(r.xMin >= -1f && r.yMin >= -1f && r.xMax <= SW + 1f && r.yMax <= SH + 1f, what + " inside the screen" + _at + " " + Fmt(r));
        }

        /// <summary>Widget vs the HUD's fixed elements (objective, perspective chips, timer, prompt, hint, team cards)
        /// and the WebGL template's toast corner (top-right 26% x 10%).</summary>
        static void CheckNoHudOverlapNow(string what, RectTransform w, UIManager ui)
        {
            var r = ScreenRect(w);
            var h = ui.Hud;
            var list = new List<(string, RectTransform)>
            {
                ("Objective", h.Objective != null ? (RectTransform)h.Objective.transform : null),
                ("Perspective", h.Perspective != null ? (RectTransform)h.Perspective.transform : null),
                ("Prompt", h.Prompt != null ? (RectTransform)h.Prompt.transform : null),
                ("Hint", h.Hint != null ? (RectTransform)h.Hint.transform : null),
            };
            for (int i = 0; i < 3; i++) { var card = h.Team != null ? h.Team.Card(i) : null; if (card != null && card.gameObject.activeInHierarchy) list.Add(("Card" + i, card.Rt)); }
            bool ok = true; string hit = "";
            foreach (var (n, rt) in list)
            {
                if (rt == null) continue;
                var o = ScreenRect(rt);
                if (n == "Prompt") o = Rect.MinMaxRect(o.xMin, o.yMax - 58f * o.height / 110f, o.xMax, o.yMax); // the visible pill
                if (r.Overlaps(o)) { ok = false; hit += n + Fmt(o) + " "; }
            }
            var toast = Rect.MinMaxRect(SW * 0.74f, SH * 0.9f, SW, SH);
            if (r.Overlaps(toast)) { ok = false; hit += "WebGLToastArea "; }
            _ctx.Check(ok, what + _at + " " + Fmt(r) + " clear of HUD elements" + (ok ? "" : " — overlaps " + hit));
        }

        static string Fmt(Rect r) => "[" + r.xMin.ToString("0") + "," + r.yMin.ToString("0") + " " + r.width.ToString("0") + "x" + r.height.ToString("0") + "]";
    }
}
