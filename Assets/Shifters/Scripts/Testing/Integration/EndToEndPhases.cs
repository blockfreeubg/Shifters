// =====================================================================================
//  SHIFTERS - EndToEndPhases.cs   [INTEGRATION - phases 1-5: spawn, presentation, movement + checkpoint,
//  switching (direct + wheel + perspective), time scale / input contexts (pause, settings, switcher)]
//  Abilities phases: EndToEndPhases.Abilities.cs. World phases (puzzles, gun, bot, exit): EndToEndPhases.World.cs.
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static partial class EndToEndPhases
    {
        // ------------------------------------------------------------------ 1. spawn through LevelManager
        public static IEnumerator Spawn(DevPlayContext ctx)
        {
            yield return E2E.Until(() => CharacterManager.Instance != null && CharacterManager.Instance.Team.Count == 3
                                         && LevelManager.Instance != null && LevelManager.Instance.IsStarted, 6f);
            var mgr = CharacterManager.Instance;
            ctx.Check(mgr != null && mgr.Team.Count == 3, "LevelManager spawned a team of 3 (" + (mgr != null ? mgr.Team.Count : 0) + ")");
            if (mgr == null || mgr.Team.Count != 3) yield break;
            var lm = LevelManager.Instance;
            ctx.Check(lm != null && lm.IsStarted && E2E.LevelStarts >= 1, "LevelManager started and raised LevelStarted");
            ctx.Check(E2E.TeamSpawns >= 1, "TeamSpawned raised");
            ctx.Check(CharacterSwitcher.Instance != null, "CharacterSwitcher created by LevelManager");
            ctx.Check(CheckpointManager.Instance != null, "CheckpointManager created by LevelManager");
            ctx.Check(ShiftersCamera.Instance != null && ShiftersCamera.Instance.Target == mgr.Active, "ShiftersCamera follows the active character");
            ctx.Check(E2E.Brute != null && E2E.Sprinter != null && E2E.Brainiac != null, "team has one Brute, one Sprinter, one Brainiac");
            ctx.Check(E2E.GunOwner != null, "exactly one member carries a weapon (" + E2E.Who(E2E.GunOwner) + ")");
            int guns = 0; foreach (var c in mgr.Team) if (c.Weapon != null && c.Weapon.HasWeapon) guns++;
            ctx.Check(guns == 1, "one gun in the team (" + guns + ")");
            ctx.Check(ShiftersInput.Context == InputContext.Gameplay, "input context is Gameplay after the level start (" + ShiftersInput.Context + ")");
            foreach (var c in mgr.Team)
                ctx.Log("member " + c.Slot + ": " + E2E.Who(c) + " SPD " + c.Stats.Speed + " STR " + c.Stats.Strength + " IQ " + c.Stats.IQ
                        + " " + c.Mass.ToString("0") + " kg" + (c.Weapon != null && c.Weapon.HasWeapon ? " GUN " + c.Weapon.Data.Name : ""));
            // let the intro (fade, banner, music) play, then take control of everyone
            yield return ctx.Wait(1.2f);
            E2E.TakeControl();
        }

        // ------------------------------------------------------------------ 2. presentation (HUD, portraits, audio, perspective, materials)
        public static IEnumerator Presentation(DevPlayContext ctx)
        {
            var mgr = CharacterManager.Instance;
            var ui = UIManager.Instance;
            ctx.Check(ui != null, "UIManager exists");
            if (ui != null)
            {
                foreach (var n in new[] { "HudCanvas", "MenuCanvas", "SwitcherCanvas", "PopupCanvas" })
                {
                    var cv = E2E.Member(ui, n) as Canvas;
                    ctx.Check(cv != null, "UI canvas " + n + " exists");
                }
                var hud = E2E.Member(ui, "HudCanvas") as Canvas;
                ctx.Check(hud != null && hud.enabled && hud.gameObject.activeInHierarchy, "HUD canvas visible during gameplay");
                ctx.Check(E2E.Truthy(E2E.Member(ui, "HudVisible")), "UIManager.HudVisible");
                ctx.Check(!ui.AnyMenuOpen, "no menu open at level start");
                ctx.Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length >= 1, "an EventSystem exists");
            }
            var ps = PortraitStudio.Instance;
            ctx.Check(ps != null, "PortraitStudio exists");
            for (int i = 0; i < 3; i++) ctx.Check(ps != null && ps.GetPortrait(i) != null, "portrait " + i + " is non-null");

            var am = AudioManager.Instance;
            ctx.Check(am != null, "AudioManager exists");
            object playedBefore = E2E.Member(am, "PlayedCount");
            Sfx.Play(SfxId.UIClick);
            Sfx.At(SfxId.Bonk, mgr.Active.Center);
            Sfx.Voice(mgr.Active, VoiceLine.Greeting);
            yield return ctx.Wait(0.2f);
            object playedAfter = E2E.Member(am, "PlayedCount");
            if (playedBefore is int pb && playedAfter is int pa) ctx.Check(pa > pb, "AudioManager played the test sounds (" + pb + " -> " + pa + ")");

            var pm = PerspectiveManager.Instance;
            ctx.Check(pm != null && pm.Viewer == mgr.Active, "PerspectiveManager viewer is the active character");
            ctx.Check(pm != null && pm.Current == mgr.Active.Perspectives, "perspective = active's perks (" + (pm != null ? pm.Current.ToString() : "-") + " vs " + mgr.Active.Perspectives + ")");
            ctx.Check(E2E.Objectives >= 1 || (LevelManager.Instance != null && LevelManager.Instance.Objective.Length > 0), "objective set (" + (LevelManager.Instance != null ? LevelManager.Instance.Objective : "") + ")");
            ctx.Check(E2E.Banners >= 1, "intro banner raised (" + (E2E.BannerLog.Count > 0 ? E2E.BannerLog[0] : "-") + ")");

            int bad = E2E.CountShaderProblems(out string first);
            ctx.Check(bad == 0, "no magenta/default materials in the level (" + bad + (first != null ? ": " + first : "") + ")");
            foreach (var c in mgr.Team)
            {
                int rends = c.GetComponentsInChildren<Renderer>().Length;
                ctx.Check(rends > 3, E2E.Who(c) + " has a real model (" + rends + " renderers)");
            }
            E2E.Frame(0f, 16f);
            yield return ctx.Wait(0.3f);
            ctx.Shot("01_hud_level_start");
        }

        // ------------------------------------------------------------------ 3. walking, jumping a 2 m gap, checkpoint activation
        public static IEnumerator Movement(DevPlayContext ctx)
        {
            var mgr = CharacterManager.Instance;
            var c = mgr.Active;
            Vector3 start = c.Feet;
            int before = E2E.Checkpoints; // the gap landing may already be inside the checkpoint zone
            ctx.Check(c.Motor.IsGrounded, E2E.Who(c) + " is grounded at the spawn");
            var takeoff = LevelMark.Position("gap2.takeoff");
            var landing = LevelMark.Position("gap2.landing");
            yield return E2E.WalkTo(c, new Vector3(0f, 0f, 1.5f), false, 0.4f, 6f);
            ctx.Check(E2E.LastWalkOk && E2E.Planar(c.Feet, start) > 2.5f, "walked " + E2E.Planar(c.Feet, start).ToString("0.0") + " m (camera-independent motor commands)");
            ctx.Check(c.Motor.IsGrounded && c.Feet.y > -0.2f, "still on the floor after walking");

            // standing jump height
            float y0 = c.Feet.y, apex = y0;
            E2E.Drive(c, Vector3.zero, false, true, true);
            for (int i = 0; i < 50; i++) { yield return null; apex = Mathf.Max(apex, c.Feet.y); if (i == 2) E2E.Drive(c, Vector3.zero, false, false, true); }
            E2E.Stop(c);
            ctx.Check(apex - y0 > 1.2f && apex - y0 < 2.4f, "standing jump apex " + (apex - y0).ToString("0.00") + " m");
            yield return E2E.Until(() => c.Motor.IsGrounded, 2f);

            // run-up + jump over the 2 m gap
            yield return E2E.RunAndJump(c, takeoff, landing + Vector3.forward * 0.5f, 6f);
            ctx.Check(c.Feet.z > L0_Sandbox.Gap2End && c.Feet.y > -0.3f, "jumped the 2 m gap (feet " + c.Feet.ToString("0.0") + ")");
            ctx.Shot("02_after_gap_jump");

            // checkpoint 1
            var cp = LevelMark.Position("checkpoint.1");
            yield return E2E.WalkTo(c, cp, false, 0.5f, 6f);
            yield return E2E.Until(() => E2E.Checkpoints > before, 2f);
            ctx.Check(E2E.Checkpoints > before, "CheckpointReached raised by walking into checkpoint 1");
            var cm = CheckpointManager.Instance;
            ctx.Check(cm != null && cm.CurrentIndex == 1, "CheckpointManager.CurrentIndex = 1 (" + (cm != null ? cm.CurrentIndex : -9) + ")");
            ctx.Check(GameSession.Instance != null && GameSession.Instance.Snapshot != null && GameSession.Instance.Snapshot.CheckpointIndex == 1,
                "GameSession holds the checkpoint snapshot");
            ctx.Check(E2E.BannerContains("CHECKPOINT"), "CHECKPOINT banner shown");
            ctx.Check(WeaponPickup.FallbackSpawn.HasValue, "checkpoint set the weapon fallback spawn");
            E2E.Frame(180f, 20f);
            yield return ctx.Wait(0.5f);
            ctx.Shot("03_checkpoint_banner");
            E2E.Frame(0f, 16f);
        }

        // ------------------------------------------------------------------ 4. switching: direct (sky cam), wheel open/close, perspective
        public static IEnumerator Switching(DevPlayContext ctx)
        {
            var mgr = CharacterManager.Instance;
            var cam = ShiftersCamera.Instance;
            var first = mgr.Active;
            ShifterCharacter next = null;
            foreach (var c in mgr.Team) if (c != first) { next = c; break; }
            int switchedBefore = E2E.Switched, perspBefore = E2E.PerspectiveChanges;
            float dist = E2E.Planar(first.Feet, next.Feet);
            yield return E2E.SwitchTo(next);
            ctx.Check(E2E.Switched > switchedBefore, "CharacterSwitched raised");
            if (cam != null)
            {
                ctx.Check(cam.Target == next, "camera target is the new character");
                ctx.Check(cam.LastTransitionWasSky == (dist >= 10f && GameSession.Instance.Settings.CinematicSwitch), "sky cam used for a " + dist.ToString("0") + " m switch (" + cam.LastTransitionWasSky + ")");
                ctx.Check(cam.LastTransitionDuration > 0.2f && cam.LastTransitionDuration <= 1.3f, "transition took " + cam.LastTransitionDuration.ToString("0.00") + " s");
            }
            ctx.Check(!first.IsActive && first.ExternalControl, "previous character is inactive (and stays where it was)");
            yield return ctx.Wait(0.4f);
            var pm = PerspectiveManager.Instance;
            ctx.Check(pm != null && pm.Viewer == next && pm.Current == next.Perspectives, "perspective follows the switch (" + (pm != null ? pm.Current.ToString() : "-") + ")");
            if (first.Perspectives != next.Perspectives) ctx.Check(E2E.PerspectiveChanges > perspBefore, "PerspectiveChanged raised on switch");
            ctx.Shot("04_switched_direct");

            // ---- the wheel (CharacterSwitcher API)
            var sw = CharacterSwitcher.Instance;
            ctx.Check(sw != null && sw.CanOpen, "switcher can open in gameplay");
            if (sw == null) yield break;
            int opens = E2E.SwitcherOpens;
            sw.Open();
            ctx.Check(sw.IsOpen && E2E.SwitcherOpens > opens, "wheel opened (SwitcherOpenChanged)");
            ctx.Check(ShiftersInput.Context == InputContext.Switcher, "input context = Switcher while the wheel is open");
            ctx.Check(TimeController.IsSlowMo, "slow-mo requested");
            yield return ctx.Wait(0.35f);
            ctx.Check(Time.timeScale < 0.1f && Time.timeScale > 0.05f, "time scale ramped to ~0.08 (" + Time.timeScale.ToString("0.000") + ")");
            ctx.Check(sw.Blend > 0.9f, "switcher blend ~1 (" + sw.Blend.ToString("0.00") + ")");
            var wheel = E2E.Member(UIManager.Instance, "Wheel") as Component;
            ctx.Check(wheel != null && wheel.gameObject.activeInHierarchy, "switcher wheel UI present");
            ShifterCharacter target = null;
            foreach (var c in mgr.Team) if (c != next && c != first) target = c;
            sw.Highlight(target.Slot);
            ctx.Check(sw.Highlighted == target.Slot, "wheel highlight follows Highlight(slot)");
            yield return ctx.Wait(0.1f);
            ctx.Shot("05_switcher_wheel");
            sw.Close(true);
            ctx.Check(!sw.IsOpen, "wheel closed on confirm");
            yield return E2E.Until(() => mgr.Active == target && !mgr.IsTransitioning, 4f);
            ctx.Check(mgr.Active == target, "wheel confirm switched to " + E2E.Who(target));
            ctx.Check(ShiftersInput.Context == InputContext.Gameplay, "context back to Gameplay after the wheel (" + ShiftersInput.Context + ")");
            yield return ctx.Wait(0.4f);
            ctx.Check(Mathf.Abs(Time.timeScale - 1f) < 0.01f, "time scale back to 1 (" + Time.timeScale.ToString("0.000") + ")");
            foreach (var c in mgr.Team) c.ExternalControl = true;

            // cancel path: open + Close(false) keeps the active character
            sw.Open();
            yield return ctx.Wait(0.1f);
            sw.Highlight(first.Slot);
            sw.Close(false);
            yield return ctx.Wait(0.3f);
            ctx.Check(mgr.Active == target && ShiftersInput.Context == InputContext.Gameplay, "wheel cancel keeps the active character");
            ctx.Check(Mathf.Abs(Time.timeScale - 1f) < 0.01f, "time scale 1 after a cancelled wheel");
        }

        // ------------------------------------------------------------------ 5. pause / settings / switcher contexts and time scale interplay
        public static IEnumerator TimeAndContexts(DevPlayContext ctx)
        {
            var ui = UIManager.Instance;
            var sw = CharacterSwitcher.Instance;
            if (ui == null) { ctx.Check(false, "UIManager missing - context tests skipped"); yield break; }
            int pauses = E2E.Pauses;
            ui.OpenPause();
            yield return null;
            ctx.Check(TimeController.IsPaused && Time.timeScale == 0f, "pause: TimeController paused, timeScale 0");
            ctx.Check(E2E.Pauses == pauses + 1, "PauseChanged raised exactly once (" + (E2E.Pauses - pauses) + ")");
            ctx.Check(ShiftersInput.Context == InputContext.Menu && ui.AnyMenuOpen, "pause: context Menu, AnyMenuOpen");
            ctx.Check(E2E.TypeName(E2E.Member(ui, "Top")) == "PauseScreen", "pause screen on top (" + E2E.TypeName(E2E.Member(ui, "Top")) + ")");
            ctx.Check(sw == null || !sw.CanOpen, "switcher refuses to open while paused");
            yield return ctx.Wait(0.4f);
            ctx.Shot("06_pause_menu");
            ctx.Check(E2E.Invoke(ui, "OpenSettings"), "UIManager.OpenSettings()");
            yield return ctx.Wait(0.4f);
            ctx.Check(E2E.TypeName(E2E.Member(ui, "Top")) == "SettingsScreen", "settings screen pushed over pause");
            ctx.Check(TimeController.IsPaused && ShiftersInput.Context == InputContext.Menu, "still paused in settings");
            ctx.Shot("07_settings");
            E2E.Invoke(ui, "Pop");
            yield return null;
            ctx.Check(E2E.TypeName(E2E.Member(ui, "Top")) == "PauseScreen" && TimeController.IsPaused, "back to the pause screen, still paused");
            ui.ClosePause();
            yield return null;
            ctx.Check(!TimeController.IsPaused && !ui.AnyMenuOpen, "resume: unpaused, no menu");
            ctx.Check(ShiftersInput.Context == InputContext.Gameplay, "resume: context Gameplay (" + ShiftersInput.Context + ")");
            yield return ctx.Wait(0.1f);
            ctx.Check(Mathf.Abs(Time.timeScale - 1f) < 0.01f, "resume: timeScale 1 (" + Time.timeScale + ")");

            // pause while the wheel is open: wheel closes, slow-mo must not stick after resuming
            if (sw != null)
            {
                sw.Open();
                yield return ctx.Wait(0.3f);
                ctx.Check(Time.timeScale < 0.1f, "wheel slow-mo active before pausing (" + Time.timeScale.ToString("0.000") + ")");
                ui.OpenPause();
                yield return null;
                ctx.Check(!sw.IsOpen, "opening pause closes the wheel");
                ctx.Check(TimeController.IsPaused && Time.timeScale == 0f, "paused over slow-mo: timeScale 0");
                ctx.Check(ShiftersInput.Context == InputContext.Menu, "context Menu (not Switcher) while paused");
                yield return ctx.Wait(0.3f);
                ui.ClosePause();
                yield return ctx.Wait(0.4f);
                ctx.Check(!TimeController.IsSlowMo && Mathf.Abs(Time.timeScale - 1f) < 0.01f, "after resume: no slow-mo left (" + Time.timeScale.ToString("0.000") + ")");
                ctx.Check(ShiftersInput.Context == InputContext.Gameplay, "after resume: context Gameplay");
            }
            // hit-stop + pause: pause wins, resume returns to normal speed
            // level music (synthesized asynchronously - give it time)
            var am = AudioManager.Instance;
            if (E2E.HasMember(am, "CurrentMusic"))
            {
                // the loop renders on a worker thread (wall-clock bound) while DevPlay frames are simulated: wait in REAL time
                float t0 = Time.realtimeSinceStartup;
                while (E2E.Member(am, "CurrentMusic").ToString() != MusicId.Training.ToString() && Time.realtimeSinceStartup - t0 < 30f) yield return null;
                var factory = E2E.Member(am, "Factory");
                ctx.Log("music wait " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s real; factory pending=" + E2E.Member(factory, "Pending")
                        + " threads=" + E2E.Member(factory, "ThreadsAvailable") + " renderMs=" + E2E.Member(factory, "TotalRenderMs"));
                ctx.Check(E2E.Member(am, "CurrentMusic").ToString() == MusicId.Training.ToString(), "level music playing (" + E2E.Member(am, "CurrentMusic") + ")");
            }
            TimeController.HitStop(0.2f, 0.1f);
            yield return null;
            ui.OpenPause();
            yield return null;
            ctx.Check(Time.timeScale == 0f, "pause during a hit-stop: timeScale 0");
            ui.ClosePause();
            yield return E2E.Until(() => Mathf.Abs(Time.timeScale - 1f) < 0.01f, 3f);
            ctx.Check(E2E.LastUntilOk, "hit-stop recovers to timeScale 1 after resume (" + Time.timeScale.ToString("0.000") + ")");
            foreach (var c in CharacterManager.Instance.Team) c.ExternalControl = true;
        }
    }
}
