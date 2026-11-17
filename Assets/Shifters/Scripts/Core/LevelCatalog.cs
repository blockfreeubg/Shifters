// =====================================================================================
//  SHIFTERS - LevelCatalog.cs   [CONTRACT FILE]
//  The ordered list of playable levels. Scene files live in Assets/Shifters/Scenes/<SceneName>.unity
//  and are added to Build Settings (MainMenu first) by the editor builder.
//  Campaign wave (W0, 2026-11-15): entries 5..21 = the new chapters (CAMPAIGN §5.2, idx 5..13, story order differs from
//  index order - see CampaignCatalog) and OVERTIME (§5.3, idx 14..21). L<n> = index + 1. A cut chapter keeps its entry.
//  RULE for every consumer: never assume every entry has a scene. The classic (non-campaign) flow - LEVEL SELECT,
//  NEXT SHIFT, victory, "LEVELS x/N", Continue - walks only 0..MainGameCount-1 (ClassicNext); anything that lists or
//  loads arbitrary entries checks HasScene(i). The campaign routes chapters itself (GameSession.NextLevelOverride).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class LevelCatalog
    {
        public const string MainMenuScene = "MainMenu";
        public const string ScenesFolder = "Assets/Shifters/Scenes";

        public static readonly LevelInfo[] Levels =
        {
            new LevelInfo(0, "L1_TrainingFacility", "THE TRAINING FACILITY", "Level 1 - Orientation Day",
                "Learn to move, switch, stack, bridge and occasionally lose your coworkers.",
                LevelTheme.Training, MusicId.Training, "Complete orientation and reach the exit elevator."),
            new LevelInfo(1, "L2_WeightProblem", "THE WEIGHT PROBLEM", "Level 2 - Industrial Factory",
                "Heavy objects, pressure plates, counterweights and questionable stacking.",
                LevelTheme.Factory, MusicId.Factory, "Weigh your options. Reach the shipping dock."),
            new LevelInfo(2, "L3_SacrificeZone", "SACRIFICE ZONE", "Level 3 - Abandoned Laboratory",
                "Security bots, timed doors and the occasional heroic sacrifice.",
                LevelTheme.Lab, MusicId.Lab, "Escape the lab. Bring back whoever you can."),
            new LevelInfo(3, "L4_IQTest", "THE IQ TEST", "Level 4 - Strange Research Facility",
                "Logic, hidden pathways, switch sequences and different points of view.",
                LevelTheme.Research, MusicId.Research, "Pass the exam. Think. Then think harder."),
            new LevelInfo(4, "L5_TotalChaos", "TOTAL CHAOS", "Level 5 - Unstable Industrial Complex",
                "Everything at once. The building is falling apart and so is your team.",
                LevelTheme.Chaos, MusicId.Chaos, "Survive the meltdown. Reach the escape elevator."),

            // ---- campaign wave (W0): main-path chapters (CAMPAIGN §5.2). Subtitle = story shift number.
            new LevelInfo(5, "L6_PerformanceReview", "PERFORMANCE REVIEW", "Shift 03 - The Downsizer",
                "Boss fight on the factory roof: a hydraulic press that wants you gone. Jam it, vent it, overload it.",
                LevelTheme.Factory, MusicId.Villain, "Survive your performance review. Overload THE DOWNSIZER."),
            new LevelInfo(6, "L7_ActNatural", "ACT NATURAL", "Shift 04 - The Bank Lobby",
                "The cameras are trained to spot one thing: teamwork. Stay apart. Look busy.",
                LevelTheme.Bank, MusicId.Heist, "Cross the lobby without being caught working together."),
            new LevelInfo(7, "L8_ColorReserve", "THE FEDERAL COLOR RESERVE", "Shift 07 - The Vault",
                "A laser curtain, three locks and the most saturated gold in the world. Steal the color back.",
                LevelTheme.Bank, MusicId.Heist, "Crack the vault. Take back the color."),
            new LevelInfo(8, "L9_TheGetaway", "THE GETAWAY", "Shift 08 - Porkchop",
                "A tram chase through the coin-sorting plant, then a very large, very angry piggy bank.",
                LevelTheme.Bank, MusicId.Heist, "Outrun PORKCHOP. Then break the bank."),
            new LevelInfo(9, "L10_LightsOut", "LIGHTS OUT", "Shift 09 - The Old Headquarters",
                "The abandoned HQ at night. Your flashlight is the only color. Something moves when it is dark.",
                LevelTheme.Night, MusicId.Spooky, "Restore power to the old HQ. Stay in the light."),
            new LevelInfo(10, "L11_MeMorialPark", "THE ME-MORIAL PARK", "Shift 10 - The Cemetery",
                "Fog, moonlight and a tombstone for every intern you ever lost. Stick together.",
                LevelTheme.Night, MusicId.Spooky, "Cross the cemetery. Nobody walks alone."),
            new LevelInfo(11, "L12_Unmasked", "UNMASKED", "Shift 11 - The Boogeyman",
                "Boss: it only hunts the loneliest intern. Stay together and pull off the mask.",
                LevelTheme.Night, MusicId.Boss, "Unmask THE BOOGEYMAN."),
            new LevelInfo(12, "L13_HostileTakeover", "HOSTILE TAKEOVER", "Shift 13 - The One-Man Band",
                "Final boss on top of ME TOWER: a one-man mech that hates teams. Fist, skates, brain dome.",
                LevelTheme.Tower, MusicId.FinalBoss, "Dismantle THE ONE-MAN BAND. Catch Max."),
            new LevelInfo(13, "L14_AllInFavor", "ALL IN FAVOR?", "Shift 14 - The Impeachment",
                "Evidence, a filibuster, one very important vote - and a catch.",
                LevelTheme.Tower, MusicId.Finale, "Present the evidence. Call the vote."),

            // ---- campaign wave (W0): OVERTIME side levels (CAMPAIGN §5.3, optional, "UNPAID").
            new LevelInfo(14, "L15_CasingTheJoint", "CASING THE JOINT", "Overtime - Pure Stealth",
                "Guard cones, cameras and photo spots. Nobody sees you, nobody gets hurt. Go for the GHOST grade.",
                LevelTheme.Bank, MusicId.Heist, "Case the bank without being spotted."),
            new LevelInfo(15, "L16_SwapMeet", "SWAP MEET", "Overtime - The SWAP-O-MATIC",
                "Swap chains in the research wing. Yes, you can put the Brute up there.",
                LevelTheme.Research, MusicId.Research, "Swap your way to the exit."),
            new LevelInfo(16, "L17_CapsLockCapers", "CAPS LOCK CAPERS", "Overtime - The CAPS-LOCK Cannon",
                "Freeze tiles, crushers and crates in mid-air. LOUD AND CLEAR.",
                LevelTheme.Chaos, MusicId.Chaos, "FREEZE THINGS. REACH THE EXIT."),
            new LevelInfo(17, "L18_BitByBit", "BIT BY BIT", "Overtime - The BIT-SHIFTER",
                "Tiny blocks in vents, huge blocks for stairs. Size matters.",
                LevelTheme.Factory, MusicId.Factory, "Resize your way through the factory."),
            new LevelInfo(18, "L19_TheBreakRoom", "THE BREAK ROOM", "Overtime - The Coffee Is Brewing",
                "Survive 120 seconds against three team-building dummies while the coffee brews.",
                LevelTheme.Night, MusicId.Spooky, "Survive until the coffee is ready."),
            new LevelInfo(19, "L20_TeamBuildingRetreat", "TEAM-BUILDING RETREAT", "Overtime - Trust Falls",
                "A trust-fall course with par times. Attendance is mandatory. Fun is mandatory.",
                LevelTheme.Training, MusicId.Training, "Finish the course under par."),
            new LevelInfo(20, "L21_QuarterlyReview", "QUARTERLY REVIEW", "Overtime - Boss Rush",
                "The Downsizer, Porkchop and the One-Man Band, back to back, with modifiers.",
                LevelTheme.Tower, MusicId.Boss, "Survive the quarterly review: three bosses in a row."),
            new LevelInfo(21, "L22_TowerOfOne", "THE TOWER OF ONE", "Overtime - One (1) Passenger",
                "A lift for one, a hall of a thousand Maxes and a scale that only moves for a quorum.",
                LevelTheme.Tower, MusicId.Villain, "Reach the top of the Tower of One."),
        };

        /// <summary>Entries 0..MainGameCount-1 are the shipped 5-level game (L1..L5). The classic (non-campaign) flow -
        /// LEVEL SELECT, NEXT SHIFT, the victory screen, "LEVELS x/N", Continue - only walks these, so it never reaches an
        /// entry whose scene is not built. The campaign module routes the chapters itself.</summary>
        public const int MainGameCount = 5;

        /// <summary>Classic-flow successor of a catalog index: index + 1 inside the shipped game (-1 -> 0, 0..3 -> +1),
        /// otherwise -1 (= run over: after L5 the victory screen; a new chapter played outside the campaign returns to the menu).</summary>
        public static int ClassicNext(int index) => index >= -1 && index + 1 < MainGameCount ? index + 1 : -1;

        /// <summary>True if the entry exists and its scene can be loaded (in Build Settings; in the Editor also any scene file
        /// on disk, like SceneLoader.CanLoad). Use it before listing / loading entries beyond MainGameCount.</summary>
        public static bool HasScene(int index)
        {
            var info = Get(index);
            if (info == null || string.IsNullOrEmpty(info.SceneName)) return false;
            if (Application.CanStreamedLevelBeLoaded(info.SceneName)) return true;
#if UNITY_EDITOR
            return System.IO.File.Exists(ScenePath(info.SceneName));
#else
            return false;
#endif
        }

        public static int Count => Levels.Length;
        public static LevelInfo Get(int index) => index >= 0 && index < Levels.Length ? Levels[index] : null;

        public static int IndexOfScene(string sceneName)
        {
            for (int i = 0; i < Levels.Length; i++)
                if (Levels[i].SceneName == sceneName) return i;
            return -1;
        }

        public static string ScenePath(string sceneName) => ScenesFolder + "/" + sceneName + ".unity";
    }
}
