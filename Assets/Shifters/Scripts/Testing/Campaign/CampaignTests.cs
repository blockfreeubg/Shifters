// =====================================================================================
//  SHIFTERS - CampaignTests.cs   [owned by the CAMPAIGN agent]
//  DevPlay suites for the campaign module (CAMPAIGN.md §22 G8 "CampaignTests (MemoryStore)").
//    RunAll  : scene-less (no level loads): catalog data, save JSON v1 + version check, StartNew,
//              forced completes through all 14 chapters in story order (routing, meter, world
//              color, exhibits, saves, intro/outro/overlay calls), Continue, StartAt, unbuilt
//              chapters skipped + placeholder end, Free Play, COLOR SHIFT off, LostLog,
//              violations, BotMode, assists bookkeeping.
//    RealL1  : MainMenu -> StartNew -> real L1 load: title/subtitle/objective override, banner,
//              world-color base 0.40, save written (MemoryStore); forced complete -> NEXT SHIFT
//              routes to L2 with its story title and base 0.46.
//  Run: devplay.sh <clone> campaign_all -devSetup Shifters.CampaignTests.RunAll -devSeconds 1
//       devplay.sh <clone> campaign_l1 -devSetup Shifters.CampaignTests.RealL1 -devScene Assets/Shifters/Scenes/MainMenu.unity -devSeconds 1
//  Every run backs up and restores all shifters_* PlayerPrefs (SessionPrefs.BackupAll/RestoreAll).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class CampaignTests
    {
        sealed class Rec
        {
            public readonly List<string> Plays = new List<string>(), Overlays = new List<string>(), Cards = new List<string>(), Exhibits = new List<string>();
            public readonly List<int> Loads = new List<int>();
            public readonly List<float> Colors = new List<float>();
            public int Menus;
            public void Clear() { Plays.Clear(); Overlays.Clear(); Cards.Clear(); Exhibits.Clear(); Loads.Clear(); Colors.Clear(); Menus = 0; }
            public int LastLoad => Loads.Count > 0 ? Loads[Loads.Count - 1] : -99;
            public float LastColor => Colors.Count > 0 ? Colors[Colors.Count - 1] : -1f;
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            yield return null;
            SessionPrefs.BackupAll();
            var rec = new Rec();
            Action<string, string> onCard = (a, t) => rec.Cards.Add(a + "|" + t);
            Action<string> onExhibit = id => rec.Exhibits.Add(id);
            GameEvents.ChapterCard += onCard;
            GameEvents.ExhibitAcquired += onExhibit;
            try
            {
                CatalogChecks(ctx);
                SaveChecks(ctx);
                DirectorChecks(ctx, rec);
                HRFileChecks(ctx);
            }
            catch (Exception e) { ctx.Check(false, "exception: " + e); }
            finally
            {
                GameEvents.ChapterCard -= onCard;
                GameEvents.ExhibitAcquired -= onExhibit;
                ClearOverrides();
                CampaignDirector.BotMode = false;
                CampaignSave.Store = null;
                CampaignDirector.Instance?.UseStore(CampaignSave.Store);
                SessionPrefs.RestoreAll();
            }
            ctx.Log("CampaignTests.RunAll finished");
        }

        /// <summary>HR FILE members (MostThrown / President) + additive save fields (old saves load without them).</summary>
        static void HRFileChecks(DevPlayContext ctx)
        {
            var d = CampaignDirector.Instance;
            if (d == null) { ctx.Check(false, "HRFile: director present"); return; }
            var old = new MemoryStore("{\"v\":1,\"chapter\":\"CH03\",\"meter\":0.2}");
            d.UseStore(old);
            ctx.Check(d.HasSave && d.MostThrown == null && d.President == null && d.SaveData.thrownNames != null && d.SaveData.thrownCounts != null,
                "HRFile: v1 save without thrown/president fields loads clean (MostThrown/President null)");
            var mem = new MemoryStore("{\"v\":1,\"chapter\":\"CH05\",\"thrownNames\":[\"Gus\",\"Rita\"],\"thrownCounts\":[2,7]}");
            d.UseStore(mem);
            ctx.Check(d.MostThrown == "RITA (7x)", "HRFile: MostThrown = most-thrown intern (" + d.MostThrown + ")");
            int saves = mem.Saves;
            d.President = "Rita";
            var back = CampaignSave.FromJson(mem.Json, out _);
            ctx.Check(d.President == "Rita" && mem.Saves == saves + 1 && back != null && back.president == "Rita" && back.thrownCounts.Length == 2,
                "HRFile: President persists in the save");
            var bad = new MemoryStore("{\"v\":1,\"chapter\":\"CH05\",\"thrownNames\":[\"A\",\"B\",\"C\"],\"thrownCounts\":[1]}");
            d.UseStore(bad);
            ctx.Check(d.SaveData.thrownCounts.Length == 3 && d.MostThrown == "A (1x)", "HRFile: mismatched thrown arrays sanitized");
            d.UseStore(new MemoryStore());
        }

        static void ClearOverrides()
        {
            CampaignDeps.PlayOverride = null; CampaignDeps.OverlayOverride = null; CampaignDeps.WorldColorOverride = null;
            CampaignDeps.LoadOverride = null; CampaignDeps.MenuOverride = null; CampaignDeps.SceneAvailableOverride = null;
            CampaignAssists.ForceOffer = null;
        }

        // ------------------------------------------------------------------ catalog
        static void CatalogChecks(DevPlayContext ctx)
        {
            var ch = CampaignCatalog.Chapters;
            ctx.Check(ch.Length == 14 && CampaignCatalog.Overtime.Length == 8, "catalog: 14 chapters + 8 OVERTIME");
            int[] idx = { 0, 1, 5, 6, 2, 3, 7, 8, 9, 10, 11, 4, 12, 13 };
            float[] m0 = { 0f, .10f, .15f, .20f, .25f, .30f, .35f, .55f, .60f, .63f, .66f, .72f, .78f, .85f };
            bool idsOk = true, idxOk = true, meterOk = true, numOk = true, sceneOk = true;
            for (int i = 0; i < ch.Length; i++)
            {
                idsOk &= ch[i].Id == "CH" + (i + 1).ToString("00") && CampaignCatalog.Find(ch[i].Id) == ch[i] && CampaignCatalog.StoryOrderOf(ch[i].Id) == i;
                idxOk &= ch[i].CatalogIndex == idx[i] && CampaignCatalog.ForCatalogIndex(idx[i]) == ch[i];
                numOk &= ch[i].Number == i + 1 && !ch[i].Optional && ch[i].Order == i;
                meterOk &= Near(ch[i].MeterStart, m0[i]) && Near(ch[i].MeterEnd, i + 1 < ch.Length ? m0[i + 1] : 1f);
                sceneOk &= LevelCatalog.Get(ch[i].CatalogIndex) != null && !string.IsNullOrEmpty(ch[i].SceneName);
                if (i + 1 < ch.Length) idsOk &= CampaignCatalog.Next(ch[i]) == ch[i + 1];
            }
            ctx.Check(idsOk, "catalog: ids CH01..CH14, Find, StoryOrderOf, Next in story order");
            ctx.Check(idxOk, "catalog: catalog indices per §5.2 (0,1,5,6,2,3,7,8,9,10,11,4,12,13) + ForCatalogIndex");
            ctx.Check(numOk, "catalog: numbers 1..14, not optional");
            ctx.Check(meterOk, "catalog: meter chain §6.1 (start = previous end; CH07 .35->.55; CH14 -> 1.00)");
            ctx.Check(sceneOk, "catalog: every chapter has a LevelCatalog entry");
            ctx.Check(CampaignCatalog.Next(CampaignCatalog.Find("CH14")) == null && CampaignCatalog.Next(CampaignCatalog.Find("O8")) == null, "catalog: Next(CH14) = Next(O8) = null");
            ctx.Check(CampaignCatalog.Find("CH05").ExhibitId == "A" && CampaignCatalog.Find("CH08").ExhibitId == "B" && CampaignCatalog.Find("CH11").ExhibitId == "C", "catalog: exhibits A/B/C on CH05/CH08/CH11");
            ctx.Check(CampaignCatalog.Find("CH01").OverlayId == "L1" && CampaignCatalog.Find("CH02").OverlayId == "L2" && CampaignCatalog.Find("CH05").OverlayId == "L3"
                      && CampaignCatalog.Find("CH06").OverlayId == "L4" && CampaignCatalog.Find("CH12").OverlayId == "L5" && CampaignCatalog.Find("CH07").OverlayId == null, "catalog: overlays L1..L5 on the re-framed chapters only");
            ctx.Check(CampaignCatalog.Find("CH01").IntroScript == "C00" && CampaignCatalog.Find("CH03").OutroScript == "C04" && CampaignCatalog.Find("CH09").IntroScript == "C08", "catalog: script ids (C00, C04, C08)");
            ctx.Check(CampaignCatalog.Find("CH03").Kind == ChapterKind.Boss && CampaignCatalog.Find("CH13").Kind == ChapterKind.Boss && CampaignCatalog.Find("CH14").Kind == ChapterKind.Finale, "catalog: kinds");
            float human = 0f; foreach (var c in ch) human += c.HumanMinutes;
            ctx.Check(Mathf.Abs(human - 54.2f) < 0.05f, "catalog: human minutes sum 54.2 (" + human.ToString("0.0") + ")");
            bool otOk = true;
            for (int i = 0; i < 8; i++)
            {
                var o = CampaignCatalog.Overtime[i];
                otOk &= o.Id == "O" + (i + 1) && o.CatalogIndex == 14 + i && o.Optional && o.Kind == ChapterKind.Overtime && o.Number == 0 && o.Act == "OVERTIME" && LevelCatalog.Get(o.CatalogIndex) != null;
            }
            ctx.Check(otOk, "catalog: OVERTIME O1..O8 at indices 14..21, optional");
            ctx.Check(CampaignCatalog.Find("CH07").CardTitle == "SHIFT 07 · THE FEDERAL COLOR RESERVE" && CampaignCatalog.Find("CH07").ActLabel == "ACT II · SWING SHIFT", "catalog: card strings ('" + CampaignCatalog.Find("CH07").ActLabel + "' / '" + CampaignCatalog.Find("CH07").CardTitle + "')");
            ctx.Check(Near(WorldColorDriver.BaseFor(0f), 0.40f) && Near(WorldColorDriver.BaseFor(1f), 1f) && Near(WorldColorDriver.BaseFor(0.5f), 0.70f), "world color: BaseFor = 0.40 + 0.60 * meter");
        }

        // ------------------------------------------------------------------ save
        static void SaveChecks(DevPlayContext ctx)
        {
            var mem = new MemoryStore();
            var d = new CampaignSaveData { chapter = "CH07", meter = 0.35f, done = new[] { "CH01", "CH02" }, exhibits = new[] { "A" }, violations = 9, time = 123.5f,
                                           lost = new[] { new CampaignLostEntry { name = "GARY", cause = "fell into the void", chapter = "CH05", clone = "GARY II" } } };
            CampaignSave.Save(mem, d);
            ctx.Check(mem.Json != null && mem.Json.Contains("\"v\":1") && mem.Json.Length < 4096, "save: JSON v1 written (" + (mem.Json != null ? mem.Json.Length : 0) + " bytes)");
            var back = CampaignSave.Load(mem, out bool disc);
            ctx.Check(!disc && back != null && back.chapter == "CH07" && Near(back.meter, 0.35f) && back.done.Length == 2 && back.HasExhibit("A") && back.violations == 9
                      && back.lost.Length == 1 && back.lost[0].clone == "GARY II", "save: round trip");
            var bad = new MemoryStore(mem.Json.Replace("\"v\":1", "\"v\":2"));
            var r2 = CampaignSave.Load(bad, out bool disc2);
            ctx.Check(r2 == null && disc2 && bad.Json == null && bad.Deletes == 1, "save: version mismatch -> discarded + deleted");
            var corrupt = new MemoryStore("{not json");
            var r3 = CampaignSave.Load(corrupt, out bool disc3);
            ctx.Check(r3 == null && disc3 && corrupt.Json == null, "save: corrupt JSON -> discarded");
            var none = CampaignSave.Load(new MemoryStore(), out bool disc4);
            ctx.Check(none == null && !disc4, "save: empty store -> null, not discarded");
            var unknown = CampaignSave.Load(new MemoryStore("{\"v\":1,\"chapter\":\"CH99\"}"), out _);
            ctx.Check(unknown != null && !unknown.HasChapter, "save: unknown chapter id sanitized");
            const string testKey = "shifters_campaign_selftest";
            var pp = new PlayerPrefsStore(testKey);
            pp.Save("{\"v\":1}");
            ctx.Check(PlayerPrefs.GetString(testKey) == "{\"v\":1}" && pp.Load() == "{\"v\":1}", "save: PlayerPrefsStore writes/reads");
            pp.Delete();
            ctx.Check(!PlayerPrefs.HasKey(testKey), "save: PlayerPrefsStore deletes");
        }

        // ------------------------------------------------------------------ director
        static void Install(Rec rec, Func<int, bool> available)
        {
            var gs = GameSession.Instance;
            CampaignDeps.PlayOverride = (id, done) => { rec.Plays.Add(id); done?.Invoke(); return true; };
            CampaignDeps.OverlayOverride = id => rec.Overlays.Add(id);
            CampaignDeps.WorldColorOverride = b => rec.Colors.Add(b);
            CampaignDeps.LoadOverride = i => { rec.Loads.Add(i); if (gs != null) gs.NotifyLevelLoaded(i); return true; };
            CampaignDeps.MenuOverride = () => { rec.Menus++; return true; };
            CampaignDeps.SceneAvailableOverride = available;
        }

        static void DirectorChecks(DevPlayContext ctx, Rec rec)
        {
            var d = CampaignDirector.Instance;
            var gs = GameSession.Instance;
            ctx.Check(d != null && d.gameObject == GameBootstrap.Root, "director: self-attached to the [SHIFTERS] root");
            ctx.Check(WorldColorDriver.Instance != null, "director: WorldColorDriver attached");
            if (d == null || gs == null) return;
            ctx.Check(GameSession.NextLevelOverride != null && LevelManager.CampaignIntroHook != null, "director: NextLevelOverride + CampaignIntroHook installed");
            bool fxWas = gs.Settings.ColorShiftFx;
            gs.Settings.ColorShiftFx = true;

            // ================= full story chain, everything built
            var mem = new MemoryStore();
            d.UseStore(mem);
            Install(rec, i => true);
            ctx.Check(!d.HasSave && !d.IsCampaign && d.Mode == CampaignMode.None, "director: fresh store -> no save, not campaign");
            d.StartNew(424242);
            ctx.Check(gs.Seed == 424242, "StartNew(seed): NewRun with that seed");
            ctx.Check(d.IsCampaign && d.Current != null && d.Current.Id == "CH01" && rec.LastLoad == 0, "StartNew -> CH01, loads catalog 0");
            ctx.Check(rec.Cards.Count == 1 && rec.Cards[0] == "ACT I · DAY SHIFT|SHIFT 01 · ORIENTATION DAY", "StartNew: ChapterCard raised (" + (rec.Cards.Count > 0 ? rec.Cards[0] : "none") + ")");
            ctx.Check(mem.Saves >= 1 && CampaignSave.FromJson(mem.Json, out _)?.chapter == "CH01", "StartNew: save written at chapter start");
            ChapterDef started = null; Action<ChapterDef> onStart = c => started = c;
            d.ChapterStarted += onStart;
            d.HandleLevelStarted(0, false);
            ctx.Check(started != null && started.Id == "CH01", "LevelStarted: ChapterStarted(CH01)");
            ctx.Check(rec.Plays.Count == 1 && rec.Plays[0] == "C00", "LevelStarted: intro C00 played on the fresh chapter start");
            ctx.Check(rec.Overlays.Count == 1 && rec.Overlays[0] == "L1", "LevelStarted: StoryOverlay L1 applied");
            ctx.Check(Near(rec.LastColor, 0.40f) && Near(WorldColorDriver.TargetBase(), 0.40f), "world color: CH01 base 0.40 (" + rec.LastColor + ")");
            ctx.Check(WorldColorDriver.Instance.EffectEnabled, "world color: CH01 enables the effect (WorldColor.Enabled)");
            d.HandleLevelStarted(0, true, 3);
            d.HandleLevelStarted(0, false);   // Restart Level
            ctx.Check(rec.Plays.Count == 1, "intro NOT replayed on checkpoint restore / restart level");
            ctx.Check(rec.Overlays.Count == 3, "overlay re-applied on every level start (scene reloads)");
            d.AddViolation(); d.AddViolation();
            ctx.Check(d.TeamworkViolations == 2 && gs.Stats.TeamworkViolations >= 2, "violations: counted in campaign + RunStats");
            d.ChapterStarted -= onStart;

            string midSave = null;
            bool chainOk = true, meterOk = true, colorOk = true, saveOk = true, loadOk = true;
            string chainErr = "";
            var exhibitsAt = new List<string>();
            for (int i = 0; i < CampaignCatalog.Chapters.Length; i++)
            {
                var c = CampaignCatalog.Chapters[i];
                if (d.Current != c) { chainOk = false; chainErr += " expected " + c.Id + " got " + (d.Current != null ? d.Current.Id : "null"); break; }
                loadOk &= rec.LastLoad == c.CatalogIndex;
                if (i > 0) d.HandleLevelStarted(c.CatalogIndex, false);
                colorOk &= Near(rec.LastColor, WorldColorDriver.BaseFor(c.MeterStart));
                meterOk &= Near(d.Meter, c.MeterStart);
                string[] lb = c.Id == "CH05" ? new[] { gs.Team.Members[0].DisplayName } : null;
                string lostName = lb != null ? lb[0] : null;
                d.ForceCompleteCurrent(lb, 10f + i);
                meterOk &= Near(d.Meter, c.MeterEnd) && Near(d.LastMeterTo, c.MeterEnd) && Near(d.LastMeterFrom, c.MeterStart);
                if (!string.IsNullOrEmpty(c.ExhibitId)) exhibitsAt.Add(c.Id + ":" + (d.HasExhibit(c.ExhibitId) ? c.ExhibitId : "-"));
                var saved = CampaignSave.FromJson(mem.Json, out _);
                var next = CampaignCatalog.Next(c);
                saveOk &= saved != null && saved.IsDone(c.Id) && (next != null ? saved.chapter == next.Id && Near(saved.meter, next.MeterStart) : !saved.HasChapter && saved.completed);
                if (c.Id == "CH05")
                {
                    ctx.Check(d.LostLog.Count == 1 && d.LostLog[0].Name == lostName && d.LostLog[0].ChapterId == "CH05" && d.LostLog[0].Cause == "left behind"
                              && !string.IsNullOrEmpty(d.LostLog[0].CloneName) && gs.LastRecloned.Length == 1 && d.LostLog[0].CloneName == gs.LastRecloned[0],
                              "LostLog: CH05 left-behind recorded with its reprint (" + (d.LostLog.Count > 0 ? d.LostLog[0].ToString() : "empty") + ")");
                    ctx.Check(saved != null && saved.lost.Length == 1 && saved.lost[0].chapter == "CH05", "LostLog: saved");
                }
                if (c.Id == "CH06") midSave = mem.Json;
                if (c.Id == "CH03") ctx.Check(rec.Plays.Contains("C04"), "outro C04 played by the director when the level did not fire ch03.outro");
                if (c.Id == "CH08") { GameEvents.RaiseStoryBeat("noop"); }
                bool routed = d.RouteNext();
                if (!routed) { chainOk = false; chainErr += " RouteNext false at " + c.Id; break; }
            }
            ctx.Check(chainOk, "chain: forced completes walk CH01..CH14 in story order" + chainErr);
            ctx.Check(loadOk, "chain: each chapter loads its catalog index");
            ctx.Check(meterOk, "chain: meter = MeterStart while playing, MeterEnd after completion");
            ctx.Check(colorOk, "chain: world-color base = BaseFor(meter) on every chapter start");
            ctx.Check(saveOk, "chain: saved after every completion (done[], next chapter, meter)");
            ctx.Check(string.Join(",", exhibitsAt) == "CH05:A,CH08:B,CH11:C" && rec.Exhibits.Count == 3 && rec.Exhibits[0] == "A" && rec.Exhibits[2] == "C", "exhibits: A/B/C awarded + ExhibitAcquired (" + string.Join(",", exhibitsAt) + ")");
            ctx.Check(rec.Plays.Contains("C08") && rec.Plays.Contains("C14") && rec.Plays.Contains("C12") && rec.Plays.Contains("C07") && rec.Plays.Contains("C10"), "chain: intro/outro scripts played (" + string.Join(",", rec.Plays) + ")");
            ctx.Check(d.PendingCampaignEnd && !d.CampaignEndIsPlaceholder && rec.Menus == 1 && !gs.ShowVictory, "after CH14: campaign end (complete) -> main menu, no Victory");
            ctx.Check(d.CampaignCompleted && !d.HasSave, "after CH14: completed flag, nothing to continue");
            ctx.Check(d.ConsumeCampaignEnd() && !d.PendingCampaignEnd, "ConsumeCampaignEnd clears the flag");

            // ================= level fired the outro via its beat -> director skips it
            rec.Clear();
            d.UseStore(new MemoryStore());
            d.StartAt("CH03");
            d.HandleLevelStarted(5, false);
            GameEvents.RaiseStoryBeat("ch03.outro");
            d.ForceCompleteCurrent(null, 5f);
            ctx.Check(!rec.Plays.Contains("C04") && d.HasSeen("C04"), "outro dedupe: ch03.outro beat -> director does not replay C04 (seen recorded)");

            // ================= Continue
            rec.Clear();
            var cont = new MemoryStore(midSave);
            d.UseStore(cont);
            ctx.Check(d.HasSave && d.SavedChapter != null && d.SavedChapter.Id == "CH07" && d.SavedChapter.CardTitle == "SHIFT 07 · THE FEDERAL COLOR RESERVE", "Continue: save after CH06 -> CONTINUE at CH07");
            ctx.Check(d.HasExhibit("A") && d.LostLog.Count == 1 && d.TeamworkViolations == 2, "Continue: exhibits / LostLog / violations restored from the save");
            bool ok = d.Continue();
            ctx.Check(ok && d.IsCampaign && d.Current.Id == "CH07" && Near(d.Meter, 0.35f) && rec.LastLoad == 7, "Continue(): loads CH07 at meter 0.35");

            // ================= StartAt
            rec.Clear();
            d.UseStore(new MemoryStore());
            d.StartAt("CH09");
            ctx.Check(d.Current.Id == "CH09" && Near(d.Meter, 0.60f) && rec.LastLoad == 9 && d.HasExhibit("A") && d.HasExhibit("B") && !d.HasExhibit("C"), "StartAt(CH09): table meter 0.60, exhibits A+B granted, loads 9");
            d.HandleLevelStarted(9, false);
            ctx.Check(rec.Plays.Count == 1 && rec.Plays[0] == "C08", "StartAt(CH09): intro C08");
            ctx.Check(rec.Exhibits.Count == 0, "StartAt: earlier exhibits granted silently (no ExhibitAcquired)");

            // ================= unbuilt chapters skipped (only L1..L5 built)
            rec.Clear();
            Install(rec, i => i < LevelCatalog.MainGameCount);
            d.UseStore(new MemoryStore());
            d.StartNew(-1);
            var order = new List<string>();
            for (int guard = 0; guard < 20 && d.IsCampaign; guard++)
            {
                order.Add(d.Current.Id);
                d.HandleLevelStarted(d.Current.CatalogIndex, false);
                d.ForceCompleteCurrent(null, 3f);
                d.RouteNext();
            }
            ctx.Check(string.Join(",", order) == "CH01,CH02,CH05,CH06,CH12", "unbuilt skipped: L1-L5 only -> " + string.Join(",", order));
            ctx.Check(d.PendingCampaignEnd && d.CampaignEndIsPlaceholder && !d.CampaignCompleted, "unbuilt: path runs out after CH12 -> placeholder end ('" + CampaignDirector.PlaceholderEndText + "')");
            ctx.Check(Near(CampaignSave.FromJson(((MemoryStore)CampaignSave.Store).Json, out _).meter, 0.78f), "unbuilt: meter after CH12 = 0.78");
            d.ConsumeCampaignEnd();
            rec.Clear();
            d.StartAt("CH03");
            ctx.Check(d.Current != null && d.Current.Id == "CH05" && rec.LastLoad == 2, "unbuilt: StartAt(CH03) forwards to the next built chapter CH05");

            // ================= Free Play
            rec.Clear();
            d.StartFreePlay(1);
            ctx.Check(!d.IsCampaign && d.IsFreePlay && d.Current.Id == "CH02" && rec.LastLoad == 1, "Free Play: IsCampaign false, loads idx 1");
            d.HandleLevelStarted(1, false);
            ctx.Check(rec.Plays.Count == 0 && rec.Overlays.Count == 0, "Free Play: no story, no overlay");
            ctx.Check(Near(rec.LastColor, 1f) && Near(WorldColorDriver.TargetBase(), 1f), "Free Play: world color sat 1");
            ctx.Check(!WorldColorDriver.Instance.EffectEnabled, "Free Play: world-color effect off");
            d.RouteNext();
            ctx.Check(d.IsFreePlay && d.Current.Id == "CH05" && rec.LastLoad == 2, "Free Play: NEXT -> next built chapter in story order (CH05)");
            d.StartFreePlay(4);
            gs.ShowVictory = false;
            d.RouteNext();
            ctx.Check(d.Mode == CampaignMode.None && gs.ShowVictory && rec.Menus == 1, "Free Play: after the last built chapter -> Victory + main menu");
            gs.ShowVictory = false;
            d.StartFreePlay(14);
            ctx.Check(!d.IsCampaign && d.Current != null && d.Current.Id == "O1", "OVERTIME via StartFreePlay: IsCampaign false (O1)");

            // ================= COLOR SHIFT EFFECT off
            d.StartAt("CH01");
            d.HandleLevelStarted(0, false);
            ctx.Check(Near(WorldColorDriver.TargetBase(), 0.40f), "COLOR SHIFT ON: campaign base 0.40");
            gs.Settings.ColorShiftFx = false;
            WorldColorDriver.Instance.Apply();
            ctx.Check(Near(rec.LastColor, 1f), "COLOR SHIFT OFF: sat 1 in the campaign");
            ctx.Check(!WorldColorDriver.Instance.EffectEnabled, "COLOR SHIFT OFF: effect off");
            gs.Settings.ColorShiftFx = true;

            // ================= BotMode
            rec.Clear();
            CampaignDirector.BotMode = true;
            d.UseStore(new MemoryStore());
            d.StartNew(-1);
            d.HandleLevelStarted(0, false);
            d.ForceCompleteCurrent(null, 1f);
            ctx.Check(rec.Plays.Count == 0 && rec.Overlays.Count == 1 && d.Meter > 0.09f, "BotMode: no story played (overlay still applied), chapter completes");
            CampaignDirector.BotMode = false;

            // ================= assists bookkeeping
            CampaignAssists.ResetAll();
            CampaignAssists.NoteLevelStarted(5, false, -1);
            CampaignAssists.NoteCheckpoint(1);
            for (int i = 0; i < 3; i++) CampaignAssists.NoteLevelStarted(5, true, 1);
            ctx.Check(CampaignAssists.RestartsAtCheckpoint == 3, "HR: 3 restarts of the same checkpoint counted (" + CampaignAssists.RestartsAtCheckpoint + ")");
            CampaignAssists.NoteCheckpoint(2);
            ctx.Check(CampaignAssists.RestartsAtCheckpoint == 0 && CampaignAssists.SectionSeconds == 0f, "HR: a new checkpoint resets the section");
            CampaignAssists.Tick(241f);
            ctx.Check(CampaignAssists.SectionSeconds >= CampaignAssists.SecondsForHR, "HR: 4 min in a section counted");
            ctx.Check(!CampaignAssists.ShouldOfferHR, "HR: not offered without a running level");
            var bs = new BossStatus { BossId = "downsizer", Phase = 1, PhaseCount = 3, Visible = true, State = BossState.Fighting };
            CampaignAssists.NoteBoss(bs);
            CampaignAssists.NoteWipe();
            ctx.Check(CampaignAssists.WipesInPhase("downsizer", 1) == 1 && CampaignAssists.MercyLevelFor("downsizer", 1) == 0, "MERCY: 1 wipe -> no mercy");
            CampaignAssists.NoteWipe();
            ctx.Check(CampaignAssists.WipesInPhase("downsizer", 1) == 2 && CampaignAssists.MercyLevelFor("downsizer", 1) == 1 && CampaignAssists.MercyLevelFor("downsizer", 2) == 0, "MERCY: 2 wipes in a phase -> MercyLevel 1 for that phase only");
            CampaignAssists.ResetAll();

            gs.Settings.ColorShiftFx = fxWas;
            ShiftersInput.Context = InputContext.Menu;
        }

        /// <summary>One Unity run: scene-less RunAll, then the real MainMenu -> L1 -> L2 flow (-devScene MainMenu).</summary>
        public static IEnumerator All(DevPlayContext ctx)
        {
            yield return RunAll(ctx);
            // RunAll leaves scene-less session state behind (LevelIndex set by overridden loads, story queue):
            // return to a real main menu first, exactly like a player would.
            StoryPlayer.Instance?.StopAll();
            var gs = GameSession.Instance;
            if (gs != null) gs.GoToMainMenu();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 30f && (gs == null || gs.LevelIndex >= 0 || gs.IsLoading
                   || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != LevelCatalog.MainMenuScene)) yield return null;
            yield return ctx.Wait(0.5f);
            yield return RealL1(ctx);
        }

        // ------------------------------------------------------------------ real L1 (MainMenu scene)
        public static IEnumerator RealL1(DevPlayContext ctx)
        {
            yield return null;
            SessionPrefs.BackupAll();
            var d = CampaignDirector.Instance;
            var gs = GameSession.Instance;
            var mem = new MemoryStore();
            string banner = null, card = null;
            var cards = new List<string>();
            Action<string, string, BannerStyle> onBanner = (t, s, st) => { if (st == BannerStyle.Info) banner = t + " / " + s; };
            Action<string, string> onCard = (a, t) => { card = a + " / " + t; cards.Add(card); };
            float lastColorEvt = -1f;
            Action<float> onColor = b => lastColorEvt = b;
            GameEvents.Banner += onBanner; GameEvents.ChapterCard += onCard; GameEvents.WorldColorChanged += onColor;
            if (d == null || gs == null) { ctx.Check(false, "director/session present"); yield break; }
            d.UseStore(mem);
            gs.Settings.ColorShiftFx = true;
            ctx.Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene, "RealL1: starts in MainMenu");
            ctx.Check(Near(WorldColorDriver.TargetBase(), 0.40f), "menu: grey stage base 0.40 with no campaign progress (" + WorldColorDriver.TargetBase() + ")");
            ctx.Shot("menu");

            d.StartNew(0);
            float t0 = Time.realtimeSinceStartup;
            while ((LevelManager.Instance == null || !LevelManager.Instance.IsStarted || LevelManager.Instance.LevelIndex != 0) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            var lm = LevelManager.Instance;
            ctx.Check(lm != null && lm.IsStarted && lm.LevelIndex == 0, "RealL1: L1 loaded and started");
            if (lm != null)
            {
                ctx.Check(lm.IntroTitle == "ORIENTATION DAY" && lm.IntroSubtitle == "SHIFT 01 · ME Industries Employee Orientation", "RealL1: intro title/subtitle overridden ('" + lm.IntroTitle + "' / '" + lm.IntroSubtitle + "')");
                ctx.Check(lm.Objective == CampaignCatalog.First.Objective, "RealL1: objective overridden ('" + lm.Objective + "')");
                ctx.Check(banner == "ORIENTATION DAY / SHIFT 01 · ME Industries Employee Orientation", "RealL1: intro banner shows the story title (" + banner + ")");
            }
            ctx.Check(cards.Contains("ACT I · DAY SHIFT / SHIFT 01 · ORIENTATION DAY"), "RealL1: chapter card raised (" + string.Join(" | ", cards) + ")");
            ctx.Check(d.IsCampaign && d.Current.Id == "CH01", "RealL1: IsCampaign, CH01");
            ctx.Check(WorldColorDriver.Instance != null && Near(WorldColorDriver.Instance.CurrentBase, 0.40f) && (lastColorEvt < 0f || Near(lastColorEvt, 0.40f)),
                "RealL1: world-color driver base 0.40 (" + (WorldColorDriver.Instance != null ? WorldColorDriver.Instance.CurrentBase : -1f) + ")");
            ctx.Check(Near(WorldColor.Base, 0.40f), "RealL1: rendering WorldColor.Base driven to 0.40 (" + WorldColor.Base + ")");
            ctx.Check(WorldColor.Enabled && WorldColor.EffectActive && WorldColor.Current < 0.6f, "RealL1: campaign CH01 greys the world (Enabled=" + WorldColor.Enabled + " EffectActive=" + WorldColor.EffectActive + " Current=" + WorldColor.Current + ")");
            if (gs != null && gs.Settings != null)
            {
                gs.Settings.ColorShiftFx = false; WorldColorDriver.Instance.Apply();
                ctx.Check(!WorldColor.Enabled && !WorldColor.EffectActive && Near(WorldColor.Current, 1f), "RealL1: COLOR SHIFT EFFECT off -> full color (Enabled=" + WorldColor.Enabled + ")");
                gs.Settings.ColorShiftFx = true; WorldColorDriver.Instance.Apply();
                ctx.Check(WorldColor.EffectActive, "RealL1: COLOR SHIFT EFFECT back on -> grey again");
            }
            var saved = CampaignSave.FromJson(mem.Json, out _);
            ctx.Check(saved != null && saved.chapter == "CH01" && mem.Saves >= 1, "RealL1: campaign save written (MemoryStore)");
            yield return ctx.Wait(2.5f);
            ctx.Shot("l1_campaign");
            yield return ctx.Wait(5f);
            ctx.Shot("l1_play");

            // complete -> NEXT SHIFT -> L2
            d.ForceCompleteCurrent(null, 100f);
            saved = CampaignSave.FromJson(mem.Json, out _);
            ctx.Check(saved != null && saved.chapter == "CH02" && saved.IsDone("CH01") && Near(saved.meter, 0.10f), "RealL1: completion saved (next CH02, meter 0.10)");
            ctx.Check(SessionPrefs.LoadRun() != null && SessionPrefs.LoadRun().LevelIndex == 1, "RealL1: team saved to shifters_run pointing at L2");
            yield return ctx.Wait(0.5f);
            gs.NextLevel();
            t0 = Time.realtimeSinceStartup;
            while ((LevelManager.Instance == null || !LevelManager.Instance.IsStarted || LevelManager.Instance.LevelIndex != 1) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            lm = LevelManager.Instance;
            ctx.Check(lm != null && lm.LevelIndex == 1 && lm.IntroTitle == "THE GREY PAINT FACTORY", "RealL1: NEXT SHIFT routed to L2 with its story title (" + (lm != null ? lm.IntroTitle : "null") + ")");
            ctx.Check(d.Current != null && d.Current.Id == "CH02" && Near(WorldColorDriver.Instance.CurrentBase, 0.46f), "RealL1: CH02 world-color base 0.46 (" + WorldColorDriver.Instance.CurrentBase + ")");
            ctx.Check(Near(WorldColor.Base, 0.46f) && Near(lastColorEvt, 0.46f), "RealL1: rendering WorldColor.Base 0.46 + WorldColorChanged (" + WorldColor.Base + ")");
            yield return ctx.Wait(2.5f);
            ctx.Shot("l2_campaign");

            GameEvents.Banner -= onBanner; GameEvents.ChapterCard -= onCard; GameEvents.WorldColorChanged -= onColor;
            CampaignSave.Store = null;
            d.UseStore(CampaignSave.Store);
            SessionPrefs.RestoreAll();
            ctx.Log("CampaignTests.RealL1 finished");
        }
    }
}
