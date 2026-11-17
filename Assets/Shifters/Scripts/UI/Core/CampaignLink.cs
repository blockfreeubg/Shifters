// =====================================================================================
//  SHIFTERS - CampaignLink.cs   (UI agent)
//  The UI's only door into the campaign module (CampaignDirector / CampaignCatalog / CampaignAssists,
//  CAMPAIGN.md §18.1). Every call is null-safe and works when the campaign module is ABSENT (the old
//  PLAY -> reveal -> L1 flow keeps working): members are looked up by name at menu time, so the UI
//  compiles and runs with or without Scripts/Campaign. ChapterView is the UI's copy of a ChapterDef;
//  without the campaign module a fallback table (§5.2 / §5.3) feeds SHIFT SCHEDULE and FREE PLAY.
//  The campaign save (PlayerPrefs "shifters_campaign", JSON v1, §15.1) is read-only here (PUNCHED stamps,
//  exhibits, CONTINUE label) when the director does not expose the data itself.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    /// <summary>UI-side copy of a campaign ChapterDef.</summary>
    public sealed class ChapterView
    {
        public string Id = "", Act = "", Title = "", Subtitle = "", Objective = "", ExhibitId, Kind = "Level";
        public int Number, CatalogIndex = -1;
        public float MeterStart, MeterEnd;
        public bool Optional;
        public bool IsOvertime => Kind == "Overtime" || Act == "OVERTIME" || Number <= 0;
        /// <summary>"SHIFT 07" or "OVERTIME".</summary>
        public string ShiftLabel => IsOvertime ? "OVERTIME" : "SHIFT " + Number.ToString("00");
        /// <summary>"SHIFT 07 - THE FEDERAL COLOR RESERVE".</summary>
        public string MenuLabel => ShiftLabel + " - " + (Title ?? "").ToUpperInvariant();
        public bool Playable => LevelCatalog.HasScene(CatalogIndex);
        public bool IsBoss => Kind == "Boss";
    }

    /// <summary>UI-side copy of a campaign LostRecord.</summary>
    public sealed class LostView { public string Name = "", Cause = "", ChapterId = "", CloneName = ""; }

    /// <summary>Read-only view of the "shifters_campaign" save JSON (v1, §15.1).</summary>
    [Serializable]
    public class CampaignSaveView
    {
        [Serializable] public class Lost { public string name, cause, chapter, clone; }
        public int v;
        public string chapter;
        public string[] done, seen, exhibits;
        public float meter;
        public Lost[] lost;
        public int violations;
        public float time;
        public bool completed;
        public int hr;
        public bool HasChapter => !string.IsNullOrEmpty(chapter);
        public bool Done(string id) => done != null && Array.IndexOf(done, id) >= 0;
        public bool HasExhibit(string id) => exhibits != null && Array.IndexOf(exhibits, id) >= 0;
    }

    public static class CampaignLink
    {
        public const string SaveKey = "shifters_campaign";

        /// <summary>Tests: pretend the campaign module is absent (old flow) even when it exists.</summary>
        public static bool ForceAbsent;
        /// <summary>Tests: treat the saved JSON as the campaign state while no director exists (CONTINUE / PUNCHED checks).</summary>
        public static bool SimulatePresent;
        /// <summary>Tests (with SimulatePresent): the chapter being "played" (IsCampaign = true, Current = this).</summary>
        public static ChapterView SimulatedCurrent;
        /// <summary>Tests (with SimulatePresent): CampaignAssists.ShouldOfferHR.</summary>
        public static bool SimulateHR;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ForceAbsent = false; SimulatePresent = false; SimulatedCurrent = null; SimulateHR = false; _chapters = null; _overtime = null; _catalogFromModule = false; }

        // ------------------------------------------------------------------ types
        const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
        const BindingFlags PI = BindingFlags.Public | BindingFlags.Instance;
        static Type _dirT, _catT, _assT;
        static bool _looked;

        static void Look()
        {
            if (_looked) return;
            _looked = true;
            _dirT = FindType("Shifters.CampaignDirector");
            _catT = FindType("Shifters.CampaignCatalog");
            _assT = FindType("Shifters.CampaignAssists");
        }

        static Type FindType(string name)
        {
            try
            {
                var t = Type.GetType(name);
                if (t != null) return t;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = a.GetType(name, false);
                    if (t != null) return t;
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>True when the campaign module's code is in the build.</summary>
        public static bool ModuleInstalled { get { Look(); return !ForceAbsent && _dirT != null; } }

        /// <summary>CampaignDirector.Instance (null when absent).</summary>
        public static object Director
        {
            get
            {
                if (!ModuleInstalled) return null;
                try { return _dirT.GetProperty("Instance", PS)?.GetValue(null) ?? _dirT.GetField("Instance", PS)?.GetValue(null); }
                catch (Exception) { return null; }
            }
        }

        /// <summary>The campaign flow is available (a live director, or the test simulation).</summary>
        public static bool Present => Director != null || (SimulatePresent && !ForceAbsent);

        // ------------------------------------------------------------------ state queries
        public static bool IsCampaign
        {
            get
            {
                if (SimActive) return true;
                var d = Director;
                if (d != null) return Get<bool>(d, "IsCampaign");
                return false;
            }
        }

        static bool Simulating => SimulatePresent && !ForceAbsent;
        /// <summary>Test seam: a simulated chapter overrides the live director's in-level state (IsCampaign/Current/HR/last-*).</summary>
        static bool SimActive => Simulating && SimulatedCurrent != null;
        public static bool HasSave
        {
            get
            {
                var d = Director;
                if (d != null) return Get<bool>(d, "HasSave");
                var s = SimulatePresent && !ForceAbsent ? Save : null;
                return s != null && s.HasChapter;
            }
        }
        public static float Meter
        {
            get
            {
                var d = Director;
                if (d != null)
                {
                    // in a chapter: the live meter; in the menus: the saved one (the live meter is 0 until a chapter starts)
                    if (Get<bool>(d, "IsCampaign")) return Get<float>(d, "Meter");
                    var sm = GetObj(d, "SavedMeter");
                    return sm is float f ? Mathf.Max(f, Get<float>(d, "Meter")) : Get<float>(d, "Meter");
                }
                var s = Save; return s != null ? s.meter : 0f;
            }
        }
        public static int TeamworkViolations
        {
            get
            {
                var d = Director;
                if (d != null) return Get<int>(d, "TeamworkViolations");
                var s = Save; return s != null ? s.violations : 0;
            }
        }
        public static bool BotMode
        {
            get
            {
                if (!ModuleInstalled) return false;
                try { var f = _dirT.GetField("BotMode", PS); if (f != null) return (bool)f.GetValue(null); var p = _dirT.GetProperty("BotMode", PS); return p != null && (bool)p.GetValue(null); }
                catch (Exception) { return false; }
            }
        }

        /// <summary>The chapter being played (null in the menu / classic play).</summary>
        public static ChapterView Current
        {
            get
            {
                if (SimActive) return SimulatedCurrent;
                var d = Director;
                if (d != null) return FromDef(GetObj(d, "Current"));
                return null;
            }
        }

        /// <summary>The chapter CONTINUE resumes (director's SavedChapter / SavedChapterId if it has one, else the save JSON).</summary>
        public static ChapterView SavedChapter
        {
            get
            {
                var d = Director;
                var def = GetObj(d, "SavedChapter");
                if (def != null) return FromDef(def);
                var id = GetObj(d, "SavedChapterId") as string;
                if (!string.IsNullOrEmpty(id)) return Find(id);
                var s = Save;
                return s != null && !string.IsNullOrEmpty(s.chapter) ? Find(s.chapter) : null;
            }
        }

        public static bool HasExhibit(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var d = Director;
            if (d != null)
            {
                try { var m = d.GetType().GetMethod("HasExhibit", PI, null, new[] { typeof(string) }, null); if (m != null) return (bool)m.Invoke(d, new object[] { id }); }
                catch (Exception) { }
            }
            var s = Save;
            return s != null && s.HasExhibit(id);
        }

        /// <summary>True if the chapter was completed in the campaign save (PUNCHED).</summary>
        public static bool IsPunched(string id)
        {
            var s = Save;
            return s != null && s.Done(id);
        }

        public static List<LostView> LostLog
        {
            get
            {
                var list = new List<LostView>();
                var d = Director;
                var log = GetObj(d, "LostLog") as IEnumerable;
                if (log != null)
                {
                    foreach (var r in log)
                    {
                        if (r == null) continue;
                        list.Add(new LostView { Name = Str(r, "Name"), Cause = Str(r, "Cause"), ChapterId = Str(r, "ChapterId"), CloneName = Str(r, "CloneName") });
                    }
                    return list;
                }
                var s = Save;
                if (s != null && s.lost != null)
                    foreach (var l in s.lost) if (l != null) list.Add(new LostView { Name = l.name ?? "", Cause = l.cause ?? "", ChapterId = l.chapter ?? "", CloneName = l.clone ?? "" });
                return list;
            }
        }

        /// <summary>CampaignAssists.ShouldOfferHR (false without the module, in CH14 and with HR ASSISTS off).</summary>
        public static bool ShouldOfferHR
        {
            get
            {
                if (!SessionActions.Settings.HRAssists) return false;
                if (SimActive || (Simulating && Director == null)) return SimulateHR;
                Look();
                if (ForceAbsent || _assT == null) return false;
                try { var p = _assT.GetProperty("ShouldOfferHR", PS); if (p != null) return (bool)p.GetValue(null); var f = _assT.GetField("ShouldOfferHR", PS); return f != null && (bool)f.GetValue(null); }
                catch (Exception) { return false; }
            }
        }

        /// <summary>The campaign save: the director's SaveData (same JSON field names) or the parsed PlayerPrefs JSON; null if none.</summary>
        public static CampaignSaveView Save
        {
            get
            {
                try
                {
                    var data = GetObj(Director, "SaveData");
                    if (data != null) return JsonUtility.FromJson<CampaignSaveView>(JsonUtility.ToJson(data));
                    if (!PlayerPrefs.HasKey(SaveKey)) return null;
                    var json = PlayerPrefs.GetString(SaveKey, "");
                    if (string.IsNullOrEmpty(json)) return null;
                    return JsonUtility.FromJson<CampaignSaveView>(json);
                }
                catch (Exception) { return null; }
            }
        }

        /// <summary>World color before/after the last completed chapter (director LastMeterFrom/To), or -1 when unknown.</summary>
        public static float LastMeterFrom => !SimActive && GetObj(Director, "LastMeterFrom") is float f ? f : -1f;
        public static float LastMeterTo => !SimActive && GetObj(Director, "LastMeterTo") is float f ? f : -1f;
        /// <summary>Exhibit awarded by the last completed chapter (director LastExhibit), else null.</summary>
        public static string LastExhibit => SimActive ? null : NullIfEmpty(GetObj(Director, "LastExhibit") as string);

        /// <summary>Main menu: the director finished the campaign (after CH14) -> returns true once; placeholder = R0 end card.</summary>
        public static bool ConsumeCampaignEnd(out bool placeholder)
        {
            placeholder = false;
            var d = Director;
            if (d == null || !Get<bool>(d, "PendingCampaignEnd")) return false;
            placeholder = Get<bool>(d, "CampaignEndIsPlaceholder");
            try { var m = d.GetType().GetMethod("ConsumeCampaignEnd", PI, null, Type.EmptyTypes, null); m?.Invoke(d, null); }
            catch (Exception e) { Debug.LogWarning("[UI] ConsumeCampaignEnd failed: " + e.Message); }
            return true;
        }

        /// <summary>After the campaign end + credits: CampaignDirector.PlayPostCredits(onDone) (C16). Calls onDone itself if absent.</summary>
        public static void PlayPostCredits(Action onDone)
        {
            var d = Director;
            try
            {
                var m = d?.GetType().GetMethod("PlayPostCredits", PI, null, new[] { typeof(Action) }, null);
                if (m != null) { m.Invoke(d, new object[] { onDone }); return; }
            }
            catch (Exception e) { Debug.LogWarning("[UI] PlayPostCredits failed: " + e.Message); }
            onDone?.Invoke();
        }

        /// <summary>Pause "LET HR HANDLE IT": CampaignAssists.UseHR() (counts it + restarts at the next checkpoint). False if absent.</summary>
        public static bool UseHR()
        {
            Look();
            if (ForceAbsent || _assT == null) return false;
            try
            {
                var m = _assT.GetMethod("UseHR", PS, null, Type.EmptyTypes, null);
                if (m == null) return false;
                var r = m.Invoke(null, null);
                return !(r is bool b) || b;
            }
            catch (Exception e) { Debug.LogWarning("[UI] CampaignAssists.UseHR failed: " + e.Message); return false; }
        }

        /// <summary>Re-reads the campaign save from PlayerPrefs (CampaignDirector.ReloadSave). Call after editing the save key
        /// directly (tests, a debug "wipe save"); the director otherwise caches the save it loaded at boot. False if absent.</summary>
        public static bool ReloadSave()
        {
            var d = Director;
            if (d == null) return false;
            try
            {
                var m = d.GetType().GetMethod("ReloadSave", PI, null, Type.EmptyTypes, null);
                if (m == null) return false;
                m.Invoke(d, null);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[UI] ReloadSave failed: " + e.Message); return false; }
        }

        // ------------------------------------------------------------------ actions (return false when not handled)
        public static bool StartNew(int seed) => Invoke(Director, "StartNew", seed);
        public static bool StartAt(string chapterId) => Invoke(Director, "StartAt", chapterId);
        public static bool Continue()
        {
            var d = Director;
            if (d == null) return false;
            try
            {
                var m = d.GetType().GetMethod("Continue", PI, null, Type.EmptyTypes, null);
                if (m == null) return false;
                var r = m.Invoke(d, null);
                return !(r is bool b) || b;
            }
            catch (Exception e) { Debug.LogWarning("[UI] CampaignDirector.Continue failed: " + e.Message); return false; }
        }
        public static bool StartFreePlay(int catalogIndex) => Invoke(Director, "StartFreePlay", catalogIndex);

        // ------------------------------------------------------------------ catalog
        static List<ChapterView> _chapters, _overtime;
        static bool _catalogFromModule;

        public static IReadOnlyList<ChapterView> Chapters { get { LoadCatalog(); return _chapters; } }
        public static IReadOnlyList<ChapterView> Overtime { get { LoadCatalog(); return _overtime; } }

        public static ChapterView Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            LoadCatalog();
            foreach (var c in _chapters) if (c.Id == id) return c;
            foreach (var c in _overtime) if (c.Id == id) return c;
            return null;
        }

        /// <summary>Shift label for a LevelCatalog index: the chapter's ShiftLabel ("SHIFT 04" for L7_ActNatural, "OVERTIME")
        /// when the campaign module is installed (or simulated) and the index is a chapter; otherwise the classic
        /// "SHIFT " + (idx + 1) label. Null-safe; "" for idx &lt; 0.</summary>
        public static string ShiftLabelFor(int idx)
        {
            if (idx < 0) return "";
            ChapterView ch = null;
            try { if (ModuleInstalled || Present) ch = ForCatalogIndex(idx); } catch { ch = null; }
            return ch != null ? ch.ShiftLabel : "SHIFT " + (idx + 1).ToString("00");
        }

        public static ChapterView ForCatalogIndex(int idx)
        {
            LoadCatalog();
            foreach (var c in _chapters) if (c.CatalogIndex == idx) return c;
            foreach (var c in _overtime) if (c.CatalogIndex == idx) return c;
            return null;
        }

        /// <summary>The next main-path chapter in story order (null after CH14 / for OVERTIME).</summary>
        public static ChapterView Next(ChapterView c)
        {
            if (c == null) return null;
            LoadCatalog();
            int i = _chapters.IndexOf(c);
            if (i < 0) for (int k = 0; k < _chapters.Count; k++) if (_chapters[k].Id == c.Id) i = k;
            return i >= 0 && i + 1 < _chapters.Count ? _chapters[i + 1] : null;
        }

        static void LoadCatalog()
        {
            Look();
            bool module = !ForceAbsent && _catT != null;
            if (_chapters != null && module == _catalogFromModule) return;
            _catalogFromModule = module;
            _chapters = new List<ChapterView>();
            _overtime = new List<ChapterView>();
            if (module)
            {
                ReadArray("Chapters", _chapters);
                ReadArray("Overtime", _overtime);
                if (_chapters.Count > 0) return;
            }
            Fallback(_chapters, _overtime);
        }

        static void ReadArray(string member, List<ChapterView> into)
        {
            try
            {
                var v = (_catT.GetField(member, PS)?.GetValue(null) ?? _catT.GetProperty(member, PS)?.GetValue(null)) as IEnumerable;
                if (v == null) return;
                foreach (var d in v) { var c = FromDef(d); if (c != null) into.Add(c); }
            }
            catch (Exception e) { Debug.LogWarning("[UI] CampaignCatalog." + member + ": " + e.Message); }
        }

        static ChapterView FromDef(object d)
        {
            if (d == null) return null;
            if (d is ChapterView cv) return cv;
            // prefer the cached instance with the same id (stable references for IndexOf)
            string id = Str(d, "Id");
            if (_chapters != null && _catalogFromModule)
            {
                foreach (var c in _chapters) if (c.Id == id) return c;
                foreach (var c in _overtime) if (c.Id == id) return c;
            }
            return new ChapterView
            {
                Id = id, Number = Get<int>(d, "Number"), Act = Str(d, "Act"), Title = Str(d, "Title"), Subtitle = Str(d, "Subtitle"),
                Objective = Str(d, "Objective"), CatalogIndex = GetObj(d, "CatalogIndex") is int ci ? ci : -1, Kind = Str(d, "Kind"),
                MeterStart = Get<float>(d, "MeterStart"), MeterEnd = Get<float>(d, "MeterEnd"), ExhibitId = NullIfEmpty(Str(d, "ExhibitId")),
                Optional = Get<bool>(d, "Optional"),
            };
        }

        /// <summary>§5.2 / §5.3 copy used when the campaign module is absent (ids "CH01".."CH14", "O1".."O8").</summary>
        static void Fallback(List<ChapterView> ch, List<ChapterView> ot)
        {
            void C(int n, string act, string title, int idx, string kind, float m0, float m1, string ex = null) =>
                ch.Add(new ChapterView { Id = "CH" + n.ToString("00"), Number = n, Act = act, Title = title, CatalogIndex = idx, Kind = kind, MeterStart = m0, MeterEnd = m1, ExhibitId = ex });
            C(1, "I", "Orientation Day", 0, "Level", 0.00f, 0.10f);
            C(2, "I", "The Grey Paint Factory", 1, "Level", 0.10f, 0.15f);
            C(3, "I", "Performance Review", 5, "Boss", 0.15f, 0.20f);
            C(4, "II", "Act Natural", 6, "Level", 0.20f, 0.25f);
            C(5, "II", "The Back Way In", 2, "Level", 0.25f, 0.30f, "A");
            C(6, "II", "The Banker's Exam", 3, "Level", 0.30f, 0.35f);
            C(7, "II", "The Federal Color Reserve", 7, "Level", 0.35f, 0.55f);
            C(8, "II", "The Getaway", 8, "Boss", 0.55f, 0.60f, "B");
            C(9, "III", "Lights Out", 9, "Level", 0.60f, 0.63f);
            C(10, "III", "The ME-morial Park", 10, "Level", 0.63f, 0.66f);
            C(11, "III", "Unmasked", 11, "Boss", 0.66f, 0.72f, "C");
            C(12, "IV", "Demolition Day", 4, "Level", 0.72f, 0.78f);
            C(13, "IV", "Hostile Takeover", 12, "Boss", 0.78f, 0.85f);
            C(14, "FINALE", "All in Favor?", 13, "Finale", 0.85f, 1.00f);
            string[] ots = { "Casing the Joint", "Swap Meet", "Caps Lock Capers", "Bit by Bit", "The Break Room", "Team-Building Retreat", "Quarterly Review", "The Tower of One" };
            for (int i = 0; i < ots.Length; i++)
                ot.Add(new ChapterView { Id = "O" + (i + 1), Number = 0, Act = "OVERTIME", Title = ots[i], CatalogIndex = 14 + i, Kind = "Overtime", Optional = true });
        }

        // ------------------------------------------------------------------ text helpers
        /// <summary>"I" -> "DAY SHIFT" ... (§8 acts).</summary>
        public static string ActName(string act)
        {
            switch (act)
            {
                case "I": return "DAY SHIFT";
                case "II": return "SWING SHIFT";
                case "III": return "GRAVEYARD SHIFT";
                case "IV": return "OVERTIME";
                case "FINALE": return "SHIFT CHANGE";
                default: return "UNPAID";
            }
        }

        /// <summary>"ACT II - SWING SHIFT" / "FINALE - SHIFT CHANGE".</summary>
        public static string ActHeader(string act) => act == "FINALE" ? "FINALE - SHIFT CHANGE" : act == "OVERTIME" ? "OVERTIME - UNPAID" : "ACT " + act + " - " + ActName(act);

        /// <summary>One-line "STORY SO FAR" per act (§15.1).</summary>
        public static string StorySoFar(string act)
        {
            switch (act)
            {
                case "I": return "Three interns got hired by a man who hates teams. They became one anyway.";
                case "II": return "To impeach him, they need evidence. And color. Both are in his bank.";
                case "III": return "He cut the power. He wants them to split up. They won't.";
                case "IV": return "He took Percy. They're going up.";
                case "FINALE": return "All in favor?";
                default: return "Side shifts. No story. No pay. No refunds.";
            }
        }

        public static string ExhibitName(string id)
        {
            switch (id)
            {
                case "A": return "THE GREY MEMO";
                case "B": return "THE BOOKS";
                case "C": return "THE BEDSHEET";
                default: return "EVIDENCE";
            }
        }

        // ------------------------------------------------------------------ reflection helpers
        static object GetObj(object o, string member)
        {
            if (o == null) return null;
            try
            {
                var t = o.GetType();
                var p = t.GetProperty(member, PI);
                if (p != null) return p.GetValue(o);
                var f = t.GetField(member, PI);
                return f != null ? f.GetValue(o) : null;
            }
            catch (Exception) { return null; }
        }

        static T Get<T>(object o, string member)
        {
            var v = GetObj(o, member);
            if (v is T t) return t;
            try { if (v != null && v.GetType().IsEnum && typeof(T) == typeof(int)) return (T)(object)Convert.ToInt32(v); } catch (Exception) { }
            return default;
        }

        static string Str(object o, string member)
        {
            var v = GetObj(o, member);
            return v != null ? v.ToString() : "";
        }

        static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        static bool Invoke(object o, string method, object arg)
        {
            if (o == null) return false;
            try
            {
                foreach (var m in o.GetType().GetMethods(PI))
                {
                    if (m.Name != method) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1 || !ps[0].ParameterType.IsInstanceOfType(arg)) continue;
                    m.Invoke(o, new[] { arg });
                    return true;
                }
            }
            catch (Exception e) { Debug.LogWarning("[UI] CampaignDirector." + method + " failed: " + (e.InnerException ?? e).Message); return true; }
            return false;
        }
    }
}
