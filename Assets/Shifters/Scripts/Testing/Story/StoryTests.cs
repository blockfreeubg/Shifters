// =====================================================================================
//  SHIFTERS - StoryTests.cs   [story tests, CAMPAIGN §22 G8]
//  devplay.sh <clone> story_tests -devSetup Shifters.StoryTests.RunAll
//      -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSeconds 5
//  Checks: every token resolves; every line fits the dialogue box (<= 90 chars, <= 2 rows at 56, names
//  15 chars); every script plays at 4x with 0 exceptions; blocking time <= 30 s (Full; C15 <= 40 s);
//  skip restores context/camera/HUD in the same frame; watchdog fires; STORY OFF / AutoSkip play nothing;
//  SHORT keeps only star lines; barks (cooldown, Heckles NEVER, bark beats); Shiftocracy pick + decree.
//  Screenshots: C00 (TV + Arrival Bay) and an in-level broadcast (ch01.start) in L1.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class StoryTests
    {
        static int _shown, _hidden;
        static StoryLine _lastLine;
        static readonly List<string> _shownTexts = new List<string>();

        static void OnShown(StoryLine l) { _shown++; _lastLine = l; _shownTexts.Add(l.Text); }
        static void OnHidden() { _hidden++; }

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            var prefsBackup = PlayerPrefs.GetString("shifters_story_seen", null);
            GameEvents.StoryLineShown += OnShown;
            GameEvents.StoryLineHidden += OnHidden;
            float pickTimeout = ShiftocracyEnding.PickTimeout;
            try { StaticChecks(ctx); } catch (Exception e) { ctx.Check(false, "static checks threw: " + e); }

            // let the level spawn the team
            for (int i = 0; i < 240 && (CharacterManager.Instance == null || CharacterManager.Instance.Active == null); i++) yield return null;
            yield return ctx.Wait(1f);
            var sp = StoryPlayer.Ensure();
            ctx.Check(sp != null, "StoryPlayer exists");
            if (sp == null) yield break;
            sp.ClearSeen();
            StoryPlayer.AutoSkip = false;
            StoryPlayer.StoryModeOverride = 0;
            StoryPlayer.TimeScale = 1f;

            // ---- screenshots: C00 at 1x
            bool done = false;
            sp.Play("C00", () => done = true);
            yield return ctx.Wait(1.2f);
            ctx.Check(sp.IsBlocking, "C00 is blocking");
            ctx.Check(ShiftersInput.Context == InputContext.Cutscene, "C00: InputContext.Cutscene");
            ctx.Check(CountRenderingCameras() == 1, "C00: exactly one camera renders (" + CountRenderingCameras() + ")");
            ctx.Shot("story_c00_tv");
            for (int i = 0; i < 60 * 20 && !(_lastLine.SpeakerId == "KLAXON"); i++) yield return null;
            yield return ctx.Wait(0.6f);
            ctx.Shot("story_c00_bay");
            for (int i = 0; i < 60 * 30 && !done; i++) yield return null;
            ctx.Check(done, "C00 finished, onDone fired");
            ctx.Check(sp.BlockingElapsed <= 30.5f, "C00 blocking time " + sp.BlockingElapsed.ToString("0.0") + " s <= 30");
            CheckRestored(ctx, sp, "C00 end");
            yield return ctx.Wait(1f);
            done = false;
            sp.Play("ch01.start", () => done = true);
            yield return ctx.Wait(1.5f);
            ctx.Check(_lastLine.Broadcast && _lastLine.SpeakerId == "MAX", "ch01.start line is a MAX PiP broadcast");
            ctx.Shot("story_l1_broadcast");
            for (int i = 0; i < 60 * 15 && !done; i++) yield return null;
            ctx.Check(done, "ch01.start finished");

            // ---- every script at 4x
            StoryPlayer.TimeScale = 4f;
            ShiftocracyEnding.PickTimeout = 2f;
            var ids = new List<string>(StoryScripts.Ids);
            int played = 0;
            foreach (var id in ids)
            {
                var s = StoryScripts.Get(id);
                if (s == null) { ctx.Check(false, "script " + id + " builds"); continue; }
                sp.ClearSeen();
                bool fin = false;
                int before = _shown;
                sp.Play(id, () => fin = true);
                float maxReal = 80f;
                float t = 0f;
                while (!fin && t < maxReal) { t += 1f / 60f; yield return null; }
                ctx.Check(fin, "script " + id + " finished (" + (_shown - before) + " lines)");
                if (s.Blocking)
                {
                    float lim = id == "C15" ? 40.5f : 30.5f;
                    ctx.Check(sp.BlockingElapsed <= lim, id + " blocking time " + sp.BlockingElapsed.ToString("0.0") + " s <= " + lim);
                    CheckRestored(ctx, sp, id);
                }
                played++;
            }
            ctx.Log("played " + played + " scripts");
            ShiftocracyEnding.PickTimeout = pickTimeout;

            // ---- Shiftocracy pick happened during C15
            var se = ShiftocracyEnding.Instance;
            ctx.Check(se != null && !string.IsNullOrEmpty(se.Decree), "Shiftocracy decree chosen: " + (se != null ? se.Decree : "null"));
            ctx.Check(se != null && _shownTexts.Contains(se.Decree), "decree line was shown");

            // ---- skip restores within the frame
            done = false;
            sp.Play("C04", () => done = true);
            yield return ctx.Wait(1f);
            ctx.Check(sp.IsBlocking, "C04 blocking before skip");
            sp.Skip();
            ctx.Check(done && !sp.IsBlocking, "skip: onDone fired + not blocking (same frame)");
            CheckRestored(ctx, sp, "skip (same frame)");
            yield return null;

            // ---- watchdog
            StoryScripts.Define("test.watchdog", b => b.Blocking().Watchdog(2f).Line("MAX", "Watch me.").Wait(100f));
            done = false;
            sp.Play("test.watchdog", () => done = true);
            for (int i = 0; i < 120 && !done; i++) yield return null;
            ctx.Check(done && sp.LastWasWatchdog, "watchdog fired and ended the scene");
            CheckRestored(ctx, sp, "watchdog");

            // ---- SHORT keeps only star lines
            StoryPlayer.StoryModeOverride = 1;
            var c04 = StoryScripts.Get("C04");
            var lines = new List<StoryStep>(); c04.CollectLines(lines);
            int stars = 0; foreach (var l in lines) if (l.Star) stars++;
            int b0 = _shown; done = false;
            sp.Play("C04", () => done = true);
            for (int i = 0; i < 60 * 30 && !done; i++) yield return null;
            ctx.Check(done && _shown - b0 == stars, "SHORT C04 showed " + (_shown - b0) + " lines == " + stars + " stars");

            // ---- STORY OFF plays nothing
            StoryPlayer.StoryModeOverride = 2;
            b0 = _shown; int cut = 0;
            Action<bool> onCut = x => { if (x) cut++; };
            GameEvents.CutsceneChanged += onCut;
            int offDone = 0;
            foreach (var id in new[] { "C00", "C04", "C15", "ch04.alone", "ch14.vote" }) sp.Play(id, () => offDone++);
            yield return null;
            ctx.Check(offDone == 5 && _shown == b0 && cut == 0 && !sp.IsPlaying, "STORY OFF: 0 lines, 0 cutscenes, 5 callbacks");
            // AutoSkip
            StoryPlayer.StoryModeOverride = 0; StoryPlayer.AutoSkip = true;
            offDone = 0;
            foreach (var id in new[] { "C06", "C13", "card.II" }) sp.Play(id, () => offDone++);
            yield return null;
            ctx.Check(offDone == 3 && _shown == b0 && cut == 0, "AutoSkip: 0 lines, 0 cutscenes, 3 callbacks");
            GameEvents.CutsceneChanged -= onCut;
            StoryPlayer.AutoSkip = false;

            // ---- campaign gates (fallback to CampaignDirector when campaign has not wired the Funcs)
            {
                var d = CampaignDirector.Instance;
                bool fp = d != null && d.IsFreePlay, camp = d != null && d.IsCampaign;
                ctx.Check(StoryPlayer.BeatsAllowedNow() == !fp, "BeatsAllowedNow == !FreePlay (" + StoryPlayer.BeatsAllowedNow() + ")");
                ctx.Check(StoryPlayer.CampaignActiveNow() == camp, "CampaignActiveNow == IsCampaign (" + camp + ")");
            }
            // ---- beats
            b0 = _shown;
            GameEvents.RaiseStoryBeat("ch04.keycard");
            yield return ctx.Wait(0.2f);
            ctx.Check(_shown > b0 && sp.CurrentId == "ch04.keycard", "beat ch04.keycard plays its script");
            sp.StopAll();
            GameEvents.RaiseStoryBeat("ch03.outro");
            yield return null;
            ctx.Check(sp.IsBlocking && sp.CurrentId == "C04", "alias ch03.outro -> C04 (blocking)");
            sp.Skip();
            CheckRestored(ctx, sp, "alias skip");

            // ---- barks
            VillainBarks.ForceEnabled = true;
            var vb = VillainBarks.Instance;
            ctx.Check(vb != null, "VillainBarks exists");
            if (vb != null)
            {
                string fired = null;
                Action<string, string> onBark = (k, t2) => fired = k;
                VillainBarks.BarkFired += onBark;
                GameEvents.RaiseStoryBeat("bark.lost!");
                yield return null;
                ctx.Check(fired == "lost", "bark beat 'bark.lost!' fired (" + fired + ")");
                sp.StopAll();
                fired = null;
                bool second = vb.TryBark("puzzle");
                ctx.Check(!second && fired == null, "cooldown blocks a low-priority bark right after");
                var gs = GameSession.Instance;
                if (gs != null && gs.Settings != null)
                {
                    int h = gs.Settings.Heckles;
                    gs.Settings.Heckles = 2;
                    ctx.Check(!vb.TryBark("wiped", true), "Heckles NEVER blocks even high-priority barks");
                    gs.Settings.Heckles = h;
                }
                VillainBarks.BarkFired -= onBark;
            }
            VillainBarks.ForceEnabled = false;
            sp.StopAll();
            CheckRestored(ctx, sp, "final");

            StoryPlayer.TimeScale = 1f;
            StoryPlayer.StoryModeOverride = -1;
            GameEvents.StoryLineShown -= OnShown;
            GameEvents.StoryLineHidden -= OnHidden;
            if (prefsBackup != null) PlayerPrefs.SetString("shifters_story_seen", prefsBackup); else PlayerPrefs.DeleteKey("shifters_story_seen");
            ctx.Log("StoryTests done: lines shown " + _shown + ", hidden " + _hidden);
        }

        static int CountRenderingCameras()
        {
            int n = 0;
            foreach (var c in Camera.allCameras) if (c.enabled && c.targetTexture == null && c.gameObject.activeInHierarchy) n++;
            return n;
        }

        static void CheckRestored(DevPlayContext ctx, StoryPlayer sp, string what)
        {
            bool ok = !sp.IsBlocking && ShiftersInput.Context != InputContext.Cutscene
                      && (CutsceneCamera.Instance == null || !CutsceneCamera.Instance.Active);
            var gc = StoryBridge.GameplayCamera;
            bool camOk = gc == null || gc.enabled;
            var ui = UIManager.Instance;
            bool hudOk = ui == null || ui.HudCanvas == null || ui.HudCanvas.enabled;
            ctx.Check(ok && camOk && hudOk, what + ": restored (ctx " + ShiftersInput.Context + ", cam " + camOk + ", hud " + hudOk + ")");
        }

        static void StaticChecks(DevPlayContext ctx)
        {
            int lines = 0, bad = 0;
            StoryText.ForceInternName = "BARTHOLOMEW III";
            foreach (var id in StoryScripts.Ids)
            {
                var s = StoryScripts.Get(id);
                var ls = new List<StoryStep>();
                s.CollectLines(ls);
                foreach (var st in ls)
                {
                    lines++;
                    if (!StoryText.TokensResolve(st.Text, out var tok)) { bad++; ctx.Check(false, id + ": unknown token {" + tok + "} in \"" + st.Text + "\""); continue; }
                    var r = StoryText.Resolve(st.Text);
                    if (!StoryText.FitsBox(r)) { bad++; ctx.Check(false, id + ": line does not fit the box (" + r.Length + " chars, " + StoryText.BoxRowsFor(r) + " rows): " + r); }
                    if (r.IndexOf('{') >= 0) { bad++; ctx.Check(false, id + ": unresolved token in " + r); }
                }
            }
            foreach (var kv in StoryText.EventBarks) foreach (var t in kv.Value) { lines++; if (!StoryText.FitsBox(StoryText.Resolve(t))) { bad++; ctx.Check(false, "bark " + kv.Key + " too long: " + t); } }
            foreach (var kv in StoryText.CounterBarks) { lines++; if (!StoryText.FitsBox(kv.Value)) { bad++; ctx.Check(false, "counter-bark " + kv.Key + " too long"); } }
            foreach (var kv in StoryText.Taunts) { lines++; if (!StoryText.FitsBox(kv.Value)) { bad++; ctx.Check(false, "taunt " + kv.Key + " too long"); } }
            foreach (var bank in new[] { StoryText.DecreesBrute, StoryText.DecreesSprinter, StoryText.DecreesBrainiac, StoryText.PercyLines, StoryText.Catchphrases })
                foreach (var t in bank) { lines++; if (!StoryText.FitsBox(t)) { bad++; ctx.Check(false, "bank line too long: " + t); } }
            foreach (WeaponType wt in Enum.GetValues(typeof(WeaponType)))
                foreach (ExperimentalVariant v in Enum.GetValues(typeof(ExperimentalVariant)))
                {
                    var g = StoryText.GunDecree(new WeaponData { Type = wt, Variant = v });
                    if (string.IsNullOrEmpty(g) || !StoryText.FitsBox(g)) { bad++; ctx.Check(false, "gun decree " + wt + "/" + v); }
                }
            ctx.Check(StoryText.TokensResolve(StoryText.ReprintPrinterLine, out _), "printer line tokens");
            StoryText.ForceInternName = null;
            ctx.Check(bad == 0, "static text checks: " + lines + " lines, " + bad + " problems");
            int blocking = 0;
            foreach (var id in StoryScripts.Ids) if (StoryScripts.Get(id).Blocking) blocking++;
            ctx.Check(StoryScripts.Ids.Count >= 90, "scripts defined: " + StoryScripts.Ids.Count + " (" + blocking + " blocking)");
            foreach (var need in new[] { "C00", "C01", "C02", "C03", "C04", "C05", "C06", "C07", "C08", "C09", "C10", "C11", "C12", "C13", "C14", "C15", "C16" })
                if (!StoryScripts.Exists(need)) ctx.Check(false, "missing script " + need);
            foreach (var kv in StoryScripts.Aliases) ctx.Check(StoryScripts.Exists(kv.Value), "alias " + kv.Key + " -> " + kv.Value);
        }
    }
}
