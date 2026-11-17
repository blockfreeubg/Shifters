// =====================================================================================
//  SHIFTERS - CampaignCatalog.cs   [owned by the CAMPAIGN agent]
//  The story order of "THE GREAT SHIFT" (CAMPAIGN.md §5.2, §5.3, §6, §6.1, §7): 14 main
//  chapters CH01..CH14 and 8 OVERTIME levels O1..O8. Story order != catalog index order
//  (the heist interleaves L3/L4). Data only; CampaignDirector does the routing.
// =====================================================================================
using System.Collections.Generic;

namespace Shifters
{
    public static class CampaignCatalog
    {
        /// <summary>World-color meter after the sunrise (CH14 epilogue, oversaturated ending party).</summary>
        public const float EpilogueMeter = 1.15f;
        /// <summary>Script played after the credits (C16 post-credits; the ui's credits screen plays it).</summary>
        public const string PostCreditsScript = "C16";
        public const string PrologueScript = "C00";

        static ChapterDef M(string id, int num, string act, ChapterKind kind, int idx, string title, string sub, string obj,
                            float m0, float m1, string intro, string outro, string exhibit, MusicId music, string overlay,
                            float botS, float humanMin)
            => new ChapterDef
            {
                Id = id, Number = num, Act = act, Kind = kind, CatalogIndex = idx, Title = title, Subtitle = sub, Objective = obj,
                MeterStart = m0, MeterEnd = m1, IntroScript = intro, OutroScript = outro, ExhibitId = exhibit,
                MusicOverride = music, OverlayId = overlay, BotBudgetSeconds = botS, HumanMinutes = humanMin, Optional = false,
            };

        static ChapterDef O(string id, int idx, string title, string sub, string obj, float humanMin)
            => new ChapterDef
            {
                Id = id, Number = 0, Act = "OVERTIME", Kind = ChapterKind.Overtime, CatalogIndex = idx, Title = title, Subtitle = sub,
                Objective = obj, MeterStart = 1f, MeterEnd = 1f, MusicOverride = MusicId.None,
                BotBudgetSeconds = UnityEngine.Mathf.Round((humanMin * 60f - 30f) / 2.2f), HumanMinutes = humanMin, Optional = true,
            };

        /// <summary>Main path in story order (§5.2). Intro/outro = scripts the DIRECTOR plays at chapter start / completion;
        /// scripts fired by a level's own beats (C03, C06, C09, C13, C15) are not listed (C04/C07/C10 are listed as outros
        /// and are skipped when the level already played them via its beat).</summary>
        public static readonly ChapterDef[] Chapters =
        {
            M("CH01", 1, "I", ChapterKind.Level, 0, "ORIENTATION DAY", "ME Industries Employee Orientation",
              "Survive orientation. Ignore Rule #1. Reach the exit elevator together.",
              0.00f, 0.10f, "C00", "C01", null, MusicId.None, "L1", 177f, 7.0f),
            M("CH02", 2, "I", ChapterKind.Level, 1, "THE GREY PAINT FACTORY", "Punishment Transfer",
              "Quota: 1000 statues of ME. Ship yourselves out of the grey paint factory instead.",
              0.10f, 0.15f, "C02", null, null, MusicId.None, "L2", 133f, 4.5f),
            M("CH03", 3, "I", ChapterKind.Boss, 5, "PERFORMANCE REVIEW", "The Downsizer",
              "Jam the press, vent its valves, then overload its review plate. All three of you.",
              0.15f, 0.20f, null, "C04", null, MusicId.None, null, 65f, 3.4f),
            M("CH04", 4, "II", ChapterKind.Level, 6, "ACT NATURAL", "The Bank Lobby",
              "Walk in like strangers. Only cooperate in the cameras' blind spots.",
              0.20f, 0.25f, "C05", null, null, MusicId.None, null, 45f, 2.2f),
            M("CH05", 5, "II", ChapterKind.Level, 2, "THE BACK WAY IN", "The Color Extraction Lab",
              "Sneak through the lab under the bank. Find out where the color went.",
              0.25f, 0.30f, null, null, "A", MusicId.Heist, "L3", 140f, 5.5f),
            M("CH06", 6, "II", ChapterKind.Level, 3, "THE BANKER'S EXAM", "Designed for One (1) Genius",
              "Pass the vault's security exam. It takes three geniuses. Luckily you have three.",
              0.30f, 0.35f, null, null, null, MusicId.Heist, "L4", 154f, 5.5f),
            M("CH07", 7, "II", ChapterKind.Level, 7, "THE FEDERAL COLOR RESERVE", "The Vault",
              "Crack the three-lock vault and swap the MASTER PRISM into the valve.",
              0.35f, 0.55f, null, null, null, MusicId.None, null, 70f, 3.1f),
            M("CH08", 8, "II", ChapterKind.Boss, 8, "THE GETAWAY", "Porkchop",
              "Flee on the coin tram, then make Porkchop cough up the Books.",
              0.55f, 0.60f, null, "C07", "B", MusicId.None, null, 80f, 4.0f),
            M("CH09", 9, "III", ChapterKind.Level, 9, "LIGHTS OUT", "The Old Headquarters",
              "Restore three breakers. Stay in the light. Don't split up.",
              0.60f, 0.63f, "C08", null, null, MusicId.None, null, 70f, 3.1f),
            M("CH10", 10, "III", ChapterKind.Level, 10, "THE ME-MORIAL PARK", "The Cemetery",
              "Cross the cemetery as a team and open the mausoleum.",
              0.63f, 0.66f, null, null, null, MusicId.None, null, 30f, 1.2f),
            M("CH11", 11, "III", ChapterKind.Boss, 11, "UNMASKED", "The Boogeyman",
              "Light the atrium in pairs. Then unmask whatever is under that bedsheet.",
              0.66f, 0.72f, null, "C10", "C", MusicId.None, null, 50f, 2.7f),
            M("CH12", 12, "IV", ChapterKind.Level, 4, "DEMOLITION DAY", "ME Tower Is Self-Destructing",
              "Teamwork detected. Self-destruct armed. Reach the executive escape pod.",
              0.72f, 0.78f, "C11", null, null, MusicId.None, "L5", 167f, 6.5f),
            M("CH13", 13, "IV", ChapterKind.Boss, 12, "HOSTILE TAKEOVER", "The One-Man Band",
              "Break the ME-CHA's fist, skates and brain dome. Then capture Max.",
              0.78f, 0.85f, "C12", null, null, MusicId.None, null, 80f, 3.7f),
            M("CH14", 14, "FINALE", ChapterKind.Finale, 13, "ALL IN FAVOR?", "The Impeachment",
              "Present the evidence. Cast a unanimous vote. Catch him when he falls.",
              0.85f, 1.00f, "C14", null, null, MusicId.None, null, 45f, 1.8f),
        };

        /// <summary>OVERTIME side levels (§5.3), optional, unlocked from the start, full color, no story.</summary>
        public static readonly ChapterDef[] Overtime =
        {
            O("O1", 14, "CASING THE JOINT", "Pure Stealth", "Case the bank without being seen. Go for the GHOST grade.", 3f),
            O("O2", 15, "SWAP MEET", "The SWAP-O-MATIC", "Chain swaps to get everyone up. PUT THE BRUTE UP THERE.", 3f),
            O("O3", 16, "CAPS LOCK CAPERS", "The CAPS-LOCK Cannon", "Freeze tiles, crushers and crates mid-air. Reach the exit.", 3f),
            O("O4", 17, "BIT BY BIT", "The BIT-SHIFTER", "Shrink blocks for the vents, grow them for the steps.", 3f),
            O("O5", 18, "THE BREAK ROOM", "The Coffee Is Brewing", "Survive 120 seconds while the coffee brews. Don't look away.", 3f),
            O("O6", 19, "TEAM-BUILDING RETREAT", "Trust Falls", "Run the trust-fall course. Beat the par times.", 4f),
            O("O7", 20, "QUARTERLY REVIEW", "Boss Rush", "Downsizer, Porkchop, One-Man Band. Back to back. With modifiers.", 8f),
            O("O8", 21, "THE TOWER OF ONE", "One (1) Passenger", "Get three interns up a tower built for one.", 4f),
        };

        static CampaignCatalog() { Index(); }

        static Dictionary<string, ChapterDef> _byId;
        static Dictionary<int, ChapterDef> _byIndex;

        static void Index()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, ChapterDef>();
            _byIndex = new Dictionary<int, ChapterDef>();
            for (int i = 0; i < Chapters.Length; i++) { Chapters[i].Order = i; _byId[Chapters[i].Id] = Chapters[i]; _byIndex[Chapters[i].CatalogIndex] = Chapters[i]; }
            for (int i = 0; i < Overtime.Length; i++) { Overtime[i].Order = Chapters.Length + i; _byId[Overtime[i].Id] = Overtime[i]; _byIndex[Overtime[i].CatalogIndex] = Overtime[i]; }
        }

        public static int Count => Chapters.Length;
        public static ChapterDef First { get { Index(); return Chapters[0]; } }
        public static ChapterDef Last { get { Index(); return Chapters[Chapters.Length - 1]; } }

        /// <summary>"CH07" / "O3" (case-insensitive; also accepts "7" / "07" for CH07). Null if unknown.</summary>
        public static ChapterDef Find(string id)
        {
            Index();
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId.TryGetValue(id, out var c)) return c;
            string up = id.Trim().ToUpperInvariant();
            if (_byId.TryGetValue(up, out c)) return c;
            if (int.TryParse(up, out int n) && n >= 1 && n <= Chapters.Length) return Chapters[n - 1];
            return null;
        }

        /// <summary>The chapter (main or OVERTIME) played in that LevelCatalog index; null if not a chapter.</summary>
        public static ChapterDef ForCatalogIndex(int catalogIndex) { Index(); return _byIndex.TryGetValue(catalogIndex, out var c) ? c : null; }

        /// <summary>Next chapter in story order (main path: null after CH14; OVERTIME: next side level, null after O8).
        /// Does NOT check whether the scene is built: see CampaignDirector.NextPlayable.</summary>
        public static ChapterDef Next(ChapterDef c)
        {
            Index();
            if (c == null) return null;
            if (c.IsMain) return c.Order + 1 < Chapters.Length ? Chapters[c.Order + 1] : null;
            int o = c.Order - Chapters.Length;
            return o + 1 < Overtime.Length ? Overtime[o + 1] : null;
        }

        /// <summary>0..13 for CH01..CH14, 14..21 for O1..O8, -1 unknown.</summary>
        public static int StoryOrderOf(string id) { var c = Find(id); return c != null ? c.Order : -1; }

        /// <summary>Main-path chapters of one act ("I".."IV", "FINALE"), story order.</summary>
        public static List<ChapterDef> InAct(string act)
        {
            Index();
            var list = new List<ChapterDef>();
            foreach (var c in Chapters) if (c.Act == act) list.Add(c);
            return list;
        }

        public static readonly string[] Acts = { "I", "II", "III", "IV", "FINALE" };

        public static string ActName(string act)
        {
            switch (act)
            {
                case "I": return "DAY SHIFT";
                case "II": return "SWING SHIFT";
                case "III": return "GRAVEYARD SHIFT";
                case "IV": return "OVERTIME";
                case "FINALE": return "SHIFT CHANGE";
                case "OVERTIME": return "UNPAID";
                default: return "";
            }
        }

        /// <summary>"ACT II · SWING SHIFT", "FINALE · SHIFT CHANGE", "OVERTIME · UNPAID".</summary>
        public static string ActLabel(string act)
        {
            if (act == "FINALE") return "FINALE · SHIFT CHANGE";
            if (act == "OVERTIME") return "OVERTIME · UNPAID";
            return "ACT " + act + " · " + ActName(act);
        }

        /// <summary>The act's tagline (§6 headings).</summary>
        public static string ActTagline(string act)
        {
            switch (act)
            {
                case "I": return "You're hired. Alone.";
                case "II": return "The Great Color Heist.";
                case "III": return "Don't split up.";
                case "IV": return "Storm the Tower.";
                case "FINALE": return "All in favor?";
                default: return "";
            }
        }

        /// <summary>One-line "STORY SO FAR" per act for the SHIFT SCHEDULE (§15.1).</summary>
        public static string StorySoFar(string act)
        {
            switch (act)
            {
                case "I": return "Three interns got hired by a man who hates teams. They became one anyway.";
                case "II": return "To impeach him, they need evidence. And color. Both are in his bank.";
                case "III": return "He cut the power. He wants them to split up. They won't.";
                case "IV": return "He took Percy. They're going up.";
                case "FINALE": return "All in favor?";
                default: return "";
            }
        }

        public static readonly string[] ExhibitIds = { "A", "B", "C" };

        /// <summary>"THE GREY MEMO" / "THE BOOKS" / "THE BEDSHEET".</summary>
        public static string ExhibitName(string id)
        {
            switch (id)
            {
                case "A": return "THE GREY MEMO";
                case "B": return "THE BOOKS";
                case "C": return "THE BEDSHEET";
                default: return "";
            }
        }

        /// <summary>"EXHIBIT B ACQUIRED: THE BOOKS".</summary>
        public static string ExhibitToast(string id) => "EXHIBIT " + id + " ACQUIRED: " + ExhibitName(id);

        /// <summary>The chapter that awards an exhibit (null if unknown).</summary>
        public static ChapterDef ChapterOfExhibit(string id)
        {
            foreach (var c in Chapters) if (c.ExhibitId == id) return c;
            return null;
        }

        /// <summary>Story beats fired by levels that play a script the director also lists (dedupe) or that the
        /// campaign records as seen. beat id -> script id.</summary>
        public static string ScriptForBeat(string beat)
        {
            switch (beat)
            {
                case "ch03.intro": return "C03";
                case "ch03.outro": return "C04";
                case "ch07.valve": return "C06";
                case "ch08.outro": return "C07";
                case "ch10.mausoleum": return "C09";
                case "ch11.outro": return "C10";
                case "ch13.caught": return "C13";
                case "ch14.bench": return "C15";
                default: return null;
            }
        }

        /// <summary>HR's verdict on a violation count (§15.2 "HR (ALSO ME) SAYS:").</summary>
        public static string HRVerdict(int violations)
        {
            if (violations <= 5) return "Barely cooperative. Promising.";
            if (violations <= 20) return "Excessive cooperation. See me.";
            return "This is a TEAM. I'm calling HR. I AM HR.";
        }
    }
}
