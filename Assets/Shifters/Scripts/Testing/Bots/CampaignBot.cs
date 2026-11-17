// =====================================================================================
//  SHIFTERS - CampaignBot.cs   [BOTS module - campaign chains, CAMPAIGN §22 G6/G7]
//  Shifters.CampaignBot.NavChain   (G6) forced completes through EVERY main chapter in story order:
//      routing (the director loads the next chapter's scene; chapters without a scene must be routed past), the save
//      (chapter at start, done[] after completion), CONTINUE (menu -> CampaignDirector.Continue after CH04 resumes at the
//      next chapter), chapter cards (GameEvents.ChapterCard per chapter), the world-color meter (MeterStart on arrival,
//      MeterEnd after completion), exhibits (ExhibitAcquired + HasExhibit), the LostLog (one intern is Lost in CH02),
//      and no early victory (GameSession.ShowVictory stays false; the campaign is not "completed" before CH14).
//  Shifters.CampaignBot.FullChain  (G7) -campaignAct I|II|III|IV|FINALE: the REAL level solutions (L<n>Solution.Solve)
//      in story order with ONE persistent team (no ForceTeam between chapters: re-clones carry over), NEXT SHIFT routing.
//  Run: $SP/tools/run_level_test.sh <proj> NAV        |  $SP/tools/run_level_test.sh <proj> ACT:II
//  The campaign / story modules are reached by reflection (CampaignDirector, CampaignCatalog, ChapterDef), so this file
//  compiles while they are absent (the runs then fail with "campaign module absent"). PlayerPrefs are guarded
//  (BotPrefsGuard): the chains start from a clean shifters_campaign / shifters_run and put the originals back.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class CampaignBot
    {
        /// <summary>Reflection view of one ChapterDef.</summary>
        public sealed class Chapter
        {
            public object Def;
            public string Id, Act, Title, ExhibitId;
            public int CatalogIndex, Order;
            public float MeterStart, MeterEnd;
            public string Kind;
            public bool HasScene => LevelCatalog.HasScene(CatalogIndex);
            public override string ToString() => Id + " " + Title + " (idx " + CatalogIndex + (HasScene ? "" : ", NO SCENE") + ")";
        }

        static Type T(string n) => BotReflect.FindType(n);
        static object Director => T("CampaignDirector")?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        public static bool CampaignPresent => T("CampaignDirector") != null && T("CampaignCatalog") != null;

        /// <summary>Main-path chapters in story order (CampaignCatalog.Chapters).</summary>
        public static List<Chapter> MainChapters()
        {
            var list = new List<Chapter>();
            var f = T("CampaignCatalog")?.GetField("Chapters", BindingFlags.Public | BindingFlags.Static);
            if (f == null || !(f.GetValue(null) is IEnumerable arr)) return list;
            int order = 0;
            foreach (var d in arr) if (d != null) list.Add(Wrap(d, order++));
            return list;
        }

        static Chapter Wrap(object d, int order)
        {
            if (d == null) return null;
            var c = new Chapter { Def = d, Order = order };
            c.Id = BotReflect.Get(d, "Id") as string;
            c.Act = BotReflect.Get(d, "Act") as string;
            c.Title = BotReflect.Get(d, "Title") as string;
            c.ExhibitId = BotReflect.Get(d, "ExhibitId") as string;
            c.CatalogIndex = BotReflect.TryGet(d, "CatalogIndex", out int ci) ? ci : -1;
            c.MeterStart = BotReflect.TryGet(d, "MeterStart", out float ms) ? ms : 0f;
            c.MeterEnd = BotReflect.TryGet(d, "MeterEnd", out float me) ? me : 0f;
            c.Kind = BotReflect.Get(d, "Kind")?.ToString();
            return c;
        }

        static string CurrentId() { var cur = BotReflect.Get(Director, "Current"); return cur != null ? BotReflect.Get(cur, "Id") as string : null; }
        static float Meter() => BotReflect.TryGet(Director, "Meter", out float m) ? m : float.NaN;
        static bool IsCampaign() => BotReflect.TryGet(Director, "IsCampaign", out bool b) && b;
        static bool Call(string method, params object[] args) => BotReflect.TryCall(Director, method, out _, args);
        static bool HasExhibit(string id) => BotReflect.TryCall(Director, "HasExhibit", out var r, id) && r is bool b && b;
        static int LostCount() => BotReflect.Get(Director, "LostLog") is ICollection col ? col.Count : (BotReflect.Get(Director, "LostLog") is IEnumerable e ? Count(e) : -1);
        static int Count(IEnumerable e) { int n = 0; foreach (var _ in e) n++; return n; }

        /// <summary>The campaign save as stored (shifters_campaign), parsed into a loose view (null if none).</summary>
        sealed class SaveView { public string chapter = ""; public string[] done = new string[0]; public string[] exhibits = new string[0]; public float meter; public bool completed; public CampaignLostView[] lost = new CampaignLostView[0]; }
        [Serializable] sealed class CampaignLostView { public string name, cause, chapter, clone; }
        static SaveView ReadSave()
        {
            string key = T("CampaignSave")?.GetField("Key", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string ?? "shifters_campaign";
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<SaveView>(json); } catch (Exception) { return null; }
        }

        static bool Contains(string[] a, string id) => a != null && Array.IndexOf(a, id) >= 0;

        // ------------------------------------------------------------------ waits
        static bool InLevel(int catalogIndex)
        {
            var gs = GameSession.Instance;
            if (gs == null || gs.IsLoading || gs.LevelIndex != catalogIndex) return false;
            if (SceneManager.GetActiveScene().name != LevelCatalog.Get(catalogIndex)?.SceneName) return false;
            var cm = CharacterManager.Instance;
            return cm != null && cm.Team.Count >= 3;
        }

        static IEnumerator WaitFor(Func<bool> cond, float timeout, Action<bool> done)
        {
            var driver = BotDriver.Ensure();
            float t0 = driver.Now;
            while (!cond() && driver.Now - t0 < timeout) yield return null;
            done(cond());
        }

        // ------------------------------------------------------------------ NavChain (G6)
        public static IEnumerator NavChain(DevPlayContext ctx)
        {
            var driver = BotDriver.Ensure();
            driver.HoldHarness(ctx);
            BotPrefsGuard.Push(ctx);
            BotStory.Apply(null);
            int cards = 0; string lastCard = "";
            var exhibitsSeen = new List<string>();
            Action<string, string> onCard = (a, t) => { cards++; lastCard = a + " / " + t; };
            Action<string> onExhibit = id => exhibitsSeen.Add(id);
            GameEvents.ChapterCard += onCard;
            GameEvents.ExhibitAcquired += onExhibit;
            try { yield return NavCore(ctx, () => cards, () => lastCard, exhibitsSeen); }
            finally
            {
                GameEvents.ChapterCard -= onCard;
                GameEvents.ExhibitAcquired -= onExhibit;
            }
            BotStory.Restore();
            BotPrefsGuard.Pop(ctx);
            driver.ReleaseHarness(ctx);
        }

        static IEnumerator NavCore(DevPlayContext ctx, Func<int> cards, Func<string> lastCard, List<string> exhibitsSeen)
        {
            if (!CampaignPresent) { ctx.Check(false, "[CAMPAIGN] campaign module absent (CampaignDirector / CampaignCatalog not found)"); yield break; }
            bool ok = false;
            yield return WaitFor(() => Director != null && GameSession.Instance != null, 8f, r => ok = r);
            if (!ctx.Check(ok, "[CAMPAIGN] CampaignDirector.Instance + GameSession present")) yield break;
            var chapters = MainChapters();
            ctx.Check(chapters.Count >= 14, "[CAMPAIGN] CampaignCatalog.Chapters has the 14 main chapters (" + chapters.Count + ")");
            var withScene = chapters.FindAll(c => c.HasScene);
            ctx.Log("[CAMPAIGN] NavChain over " + chapters.Count + " chapters, " + withScene.Count + " with a scene: " + string.Join(", ", withScene.ConvertAll(c => c.Id)));
            PlayerPrefs.DeleteKey("shifters_campaign"); PlayerPrefs.DeleteKey(SessionPrefs.RunKey);
            var driver = BotDriver.Ensure();
            float t0 = driver.Now;
            int cardsBefore = cards();   // the card is raised right before the chapter's LoadLevel: count from each routing call
            if (!Call("StartNew", 1)) Call("StartNew", -1);
            int lostBefore = 0, routed = 0, skipped = 0;
            bool continueDone = false;
            Chapter prevDone = null;
            for (int i = 0; i < chapters.Count; i++)
            {
                var ch = chapters[i];
                if (!ch.HasScene)
                {
                    skipped++;
                    ctx.Log("[CAMPAIGN] " + ch.Id + " has no scene yet - the director must route past it");
                    continue;
                }
                yield return WaitFor(() => InLevel(ch.CatalogIndex), 25f, r => ok = r);
                var gs = GameSession.Instance;
                if (!ctx.Check(ok, "[CAMPAIGN] routing: " + (prevDone != null ? prevDone.Id + " -> " : "start -> ") + ch.Id + " loads " + ch + " (now idx " + (gs != null ? gs.LevelIndex : -9) + ", director at " + CurrentId() + ")"))
                    yield break;
                routed++;
                yield return Settle(1.0f);
                ctx.Check(CurrentId() == ch.Id, "[CAMPAIGN] " + ch.Id + ": director.Current = " + CurrentId());
                ctx.Check(IsCampaign(), "[CAMPAIGN] " + ch.Id + ": IsCampaign");
                float m = Meter();
                ctx.Check(!float.IsNaN(m) && Mathf.Abs(m - ch.MeterStart) < 0.021f, "[CAMPAIGN] " + ch.Id + ": meter at start " + Fmt(m) + " ~ MeterStart " + Fmt(ch.MeterStart));
                ctx.Check(cards() > cardsBefore, "[CAMPAIGN] " + ch.Id + ": chapter card raised (" + lastCard() + ")");
                var sv = ReadSave();
                ctx.Check(sv != null && sv.chapter == ch.Id, "[CAMPAIGN] " + ch.Id + ": saved at chapter start (save.chapter = " + (sv != null ? sv.chapter : "NO SAVE") + ")");
                ctx.Check(!gs.ShowVictory, "[CAMPAIGN] " + ch.Id + ": no victory flag before the end");

                // ---- forced complete (what LevelExit does), one intern Lost in the 2nd chapter
                var team = CharacterManager.Instance.Team;
                var left = new List<string>();
                bool loseOne = routed == 2;
                if (loseOne)
                {
                    lostBefore = LostCount();
                    var victim = team[team.Count - 1];
                    left.Add(victim.DisplayName);
                    victim.Lose("NavChain: fell into the test void");
                    yield return Settle(0.5f);
                }
                var res = new LevelResult { LevelIndex = ch.CatalogIndex, Time = 30f + i, Survivors = 3 - left.Count, LeftBehind = left.ToArray(), Switches = 2, Restarts = 0 };
                gs.CompleteLevel(res);
                GameEvents.RaiseLevelCompleted(res);
                yield return Settle(2.0f);
                bool last = i == chapters.Count - 1 || chapters.FindIndex(i + 1, c => c.HasScene) < 0;
                sv = ReadSave();
                ctx.Check(sv != null && Contains(sv.done, ch.Id), "[CAMPAIGN] " + ch.Id + ": save.done has it after completion");
                m = Meter();
                ctx.Check(!float.IsNaN(m) && Mathf.Abs(m - ch.MeterEnd) < 0.021f, "[CAMPAIGN] " + ch.Id + ": meter after completion " + Fmt(m) + " ~ MeterEnd " + Fmt(ch.MeterEnd));
                if (!string.IsNullOrEmpty(ch.ExhibitId))
                    ctx.Check(HasExhibit(ch.ExhibitId) && exhibitsSeen.Contains(ch.ExhibitId) && sv != null && Contains(sv.exhibits, ch.ExhibitId),
                              "[CAMPAIGN] " + ch.Id + ": exhibit " + ch.ExhibitId + " awarded (event + director + save)");
                if (loseOne)
                {
                    int lc = LostCount();
                    ctx.Check(lc > lostBefore, "[CAMPAIGN] " + ch.Id + ": LostLog recorded the lost intern (" + lostBefore + " -> " + lc + ")");
                    ctx.Check(gs.LastRecloned != null && gs.LastRecloned.Length > 0, "[CAMPAIGN] " + ch.Id + ": the lost intern was re-cloned (" + string.Join(",", gs.LastRecloned ?? new string[0]) + ")");
                }
                if (!last) ctx.Check(!gs.ShowVictory && (sv == null || !sv.completed), "[CAMPAIGN] " + ch.Id + ": no early victory / completion");
                prevDone = ch;
                if (last)
                {
                    gs.NextLevel();
                    yield return Settle(4f);
                    sv = ReadSave();
                    bool isFinal = i == chapters.Count - 1;
                    if (isFinal) ctx.Check(sv != null && sv.completed, "[CAMPAIGN] after " + ch.Id + ": campaign marked completed in the save");
                    ctx.Log("[CAMPAIGN] after the last built chapter " + ch.Id + ": scene '" + SceneManager.GetActiveScene().name + "' victory=" + gs.ShowVictory + " completed=" + (sv != null && sv.completed));
                    break;
                }
                // ---- CONTINUE once (after the 4th routed chapter): menu, then CampaignDirector.Continue()
                if (!continueDone && routed >= 4)
                {
                    continueDone = true;
                    gs.GoToMainMenu();
                    yield return WaitFor(() => SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene && !GameSession.Instance.IsLoading, 15f, r => ok = r);
                    ctx.Check(ok, "[CAMPAIGN] continue: back in the main menu");
                    yield return Settle(0.5f);
                    sv = ReadSave();
                    bool hasSave = BotReflect.TryGet(Director, "HasSave", out bool hs) && hs;
                    ctx.Check(hasSave && sv != null && !string.IsNullOrEmpty(sv.chapter), "[CAMPAIGN] continue: HasSave with chapter '" + (sv != null ? sv.chapter : "") + "'");
                    cardsBefore = cards();
                    bool called = BotReflect.TryCall(Director, "Continue", out var cr) && (!(cr is bool cb) || cb);
                    ctx.Check(called, "[CAMPAIGN] continue: CampaignDirector.Continue() accepted");
                    ctx.Log("[CAMPAIGN] CONTINUE -> expecting the next chapter with a scene");
                    continue;
                }
                cardsBefore = cards();
                gs.NextLevel();
            }
            ctx.Check(routed == withScene.Count, "[CAMPAIGN] NavChain routed " + routed + "/" + withScene.Count + " built chapters (" + skipped + " without a scene) in " + Fmt(driver.Now - t0) + " s");
            ctx.Check(continueDone || withScene.Count < 5, "[CAMPAIGN] CONTINUE was exercised");
        }

        static IEnumerator Settle(float s)
        {
            var d = BotDriver.Ensure();
            float t0 = d.Now;
            while (d.Now - t0 < s) yield return null;
        }

        static string Fmt(float f) => f.ToString("0.00");

        // ------------------------------------------------------------------ FullChain (G7)
        /// <summary>-campaignAct I|II|III|IV|FINALE (default I).</summary>
        public static IEnumerator FullChain(DevPlayContext ctx) => FullChainAct(ctx, BotArgs.Str("-campaignAct", "I"));

        /// <summary>FullChain for one act from code (a separate name: DevPlay resolves -devSetup by method name, so FullChain must not be overloaded).</summary>

        public static IEnumerator FullChainAct(DevPlayContext ctx, string act)
        {
            var driver = BotDriver.Ensure();
            driver.HoldHarness(ctx);
            BotPrefsGuard.Push(ctx);
            yield return FullCore(ctx, act);
            BotPrefsGuard.Pop(ctx);
            driver.ReleaseHarness(ctx);
        }

        /// <summary>Static Solve(TeamBot) of Shifters.L&lt;n&gt;Solution (n = catalog index + 1), plus its Options() if any.</summary>
        public static Func<TeamBot, IEnumerator> FindSolution(int catalogIndex, out BotOptions options)
        {
            options = null;
            var t = T("L" + (catalogIndex + 1) + "Solution");
            if (t == null) return null;
            var m = t.GetMethod("Solve", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(TeamBot) }, null);
            if (m == null || m.ReturnType != typeof(IEnumerator)) return null;
            var om = t.GetMethod("Options", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (om != null && om.ReturnType == typeof(BotOptions)) { try { options = om.Invoke(null, null) as BotOptions; } catch (Exception) { } }
            return (Func<TeamBot, IEnumerator>)Delegate.CreateDelegate(typeof(Func<TeamBot, IEnumerator>), m);
        }

        static IEnumerator FullCore(DevPlayContext ctx, string act)
        {
            if (!CampaignPresent) { ctx.Check(false, "[CAMPAIGN] campaign module absent"); yield break; }
            bool ok = false;
            yield return WaitFor(() => Director != null && GameSession.Instance != null, 8f, r => ok = r);
            if (!ctx.Check(ok, "[CAMPAIGN] CampaignDirector.Instance present")) yield break;
            var chapters = MainChapters().FindAll(c => string.Equals(c.Act, act, StringComparison.OrdinalIgnoreCase));
            if (!ctx.Check(chapters.Count > 0, "[CAMPAIGN] act " + act + " has chapters (" + chapters.Count + ")")) yield break;
            var playable = chapters.FindAll(c => c.HasScene && FindSolution(c.CatalogIndex, out _) != null);
            ctx.Log("[CAMPAIGN] FullChain act " + act + ": " + string.Join(", ", chapters.ConvertAll(c => c.Id + (c.HasScene ? "" : "(no scene)") + (FindSolution(c.CatalogIndex, out _) != null ? "" : "(no solution)"))));
            if (!ctx.Check(playable.Count > 0, "[CAMPAIGN] act " + act + ": at least one chapter with scene + solution")) yield break;
            var driver = BotDriver.Ensure();
            float t0 = driver.Now;
            if (act == "I") { PlayerPrefs.DeleteKey("shifters_campaign"); PlayerPrefs.DeleteKey(SessionPrefs.RunKey); if (!Call("StartNew", 1)) Call("StartNew", -1); }
            else
            {
                if (GameSession.Instance.Team == null) TeamBot.ForceTeam(1);
                Call("StartAt", playable[0].Id);
            }
            string teamAtStart = null;
            int passed = 0;
            float botSeconds = 0f;
            for (int i = 0; i < playable.Count; i++)
            {
                var ch = playable[i];
                yield return WaitFor(() => InLevel(ch.CatalogIndex), 30f, r => ok = r);
                if (!ctx.Check(ok, "[CAMPAIGN] " + ch.Id + " loaded (director at " + CurrentId() + ")")) yield break;
                yield return Settle(0.5f);
                string team = TeamBot.DescribeTeam(GameSession.Instance.Team);
                if (teamAtStart == null) teamAtStart = team;
                ctx.Log("[CAMPAIGN] " + ch.Id + " team: " + team);
                var solve = FindSolution(ch.CatalogIndex, out var opts);
                opts = opts ?? new BotOptions();
                opts.GuardPrefs = false;   // the chain guards the prefs; the saves of this chain must survive between chapters
                int before = TeamBot.Results.Count;
                yield return TeamBot.Run(ctx, solve, opts, "CHAIN-" + ch.Id);
                var r = TeamBot.Results.Count > before ? TeamBot.Results[TeamBot.Results.Count - 1] : null;
                if (!ctx.Check(r != null && r.Passed, "[CAMPAIGN] " + ch.Id + " solved with the persistent team" + (r != null ? " in " + Fmt(r.Seconds) + " s" : ""))) yield break;
                passed++;
                botSeconds += r.Seconds;
                yield return WaitFor(() => GameSession.Instance.HasLastResult && GameSession.Instance.LastResult.LevelIndex == ch.CatalogIndex, 10f, x => ok = x);
                ctx.Check(ok, "[CAMPAIGN] " + ch.Id + ": the real exit completed the level");
                yield return Settle(2f);
                var sv = ReadSave();
                ctx.Check(sv != null && Contains(sv.done, ch.Id), "[CAMPAIGN] " + ch.Id + ": saved as done");
                if (i < playable.Count - 1) GameSession.Instance.NextLevel();
            }
            ctx.Log("[CAMPAIGN] FullChain act " + act + ": " + passed + "/" + playable.Count + " chapters, bot time " + Fmt(botSeconds) + " s, wall-sim " + Fmt(driver.Now - t0) + " s; team at start: " + teamAtStart);
            ctx.Check(passed == playable.Count, "[CAMPAIGN] FullChain act " + act + " green (" + passed + "/" + playable.Count + ")");
        }
    }
}
