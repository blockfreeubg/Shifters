// =====================================================================================
//  SHIFTERS - CampaignTypes.cs   [owned by the CAMPAIGN agent]
//  Data types of the story campaign "THE GREAT SHIFT" (CAMPAIGN.md §18.1): ChapterKind,
//  ChapterDef (immutable chapter data, built by CampaignCatalog), LostRecord, CampaignMode.
// =====================================================================================
using System;

namespace Shifters
{
    public enum ChapterKind { Level, Boss, Finale, Overtime }

    /// <summary>What the CampaignDirector is currently running. None = classic flow / main menu / a scene played directly.</summary>
    public enum CampaignMode { None, Campaign, FreePlay }

    /// <summary>Immutable data, one per chapter (main path CH01..CH14, OVERTIME O1..O8). Built only by CampaignCatalog.</summary>
    public sealed class ChapterDef
    {
        public string Id;            // "CH07" / "O3"
        public int Number;           // 7 (UI "SHIFT 07"); 0 for OVERTIME
        public string Act;           // "I".."IV", "FINALE", "OVERTIME"
        public string Title, Subtitle, Objective;
        public int CatalogIndex;     // LevelCatalog index (L<n> = index + 1)
        public ChapterKind Kind;
        public float MeterStart, MeterEnd;       // §6.1 world-color meter
        public string IntroScript, OutroScript;  // story ids played by the director (null = none)
        public string ExhibitId;                 // "A"/"B"/"C" awarded on completion (null = none)
        public MusicId MusicOverride;            // None = catalog music
        public string OverlayId;                 // "L1".."L5" for re-framed levels, null for new
        public float BotBudgetSeconds, HumanMinutes;
        public bool Optional;

        // ---- additive helpers (v1)
        /// <summary>Story order position: 0..13 main path, 14..21 OVERTIME.</summary>
        public int Order;
        public bool IsMain => Kind != ChapterKind.Overtime;
        public bool IsBoss => Kind == ChapterKind.Boss;
        /// <summary>"SHIFT 07" / "OVERTIME O3".</summary>
        public string ShiftLabel => Number > 0 ? "SHIFT " + Number.ToString("00") : "OVERTIME " + Id;
        /// <summary>"ACT II · SWING SHIFT" (chapter card top line).</summary>
        public string ActLabel => CampaignCatalog.ActLabel(Act);
        /// <summary>"SHIFT 07 · THE FEDERAL COLOR RESERVE" (chapter card / CONTINUE button).</summary>
        public string CardTitle => ShiftLabel + " · " + Title;
        /// <summary>Banner subtitle in campaign mode: "SHIFT 07 · The Vault".</summary>
        public string BannerSubtitle => ShiftLabel + " · " + Subtitle;
        public string SceneName => LevelCatalog.Get(CatalogIndex)?.SceneName;
        public override string ToString() => Id + " " + Title + " (idx " + CatalogIndex + ")";
    }

    /// <summary>An intern lost (left behind) in a campaign chapter and the clone that replaced them.</summary>
    [Serializable]
    public struct LostRecord
    {
        public string Name;       // display name of the lost intern ("GARY")
        public string Cause;      // ShifterCharacter.LastCause ("fell into the void") or "left behind"
        public string ChapterId;  // "CH05"
        public string CloneName;  // the reprint that replaced them ("GARY II"), may be empty
        public LostRecord(string name, string cause, string chapterId, string cloneName)
        { Name = name; Cause = cause; ChapterId = chapterId; CloneName = cloneName; }
        public override string ToString() => Name + " (" + Cause + ", " + ChapterId + ") -> " + CloneName;
    }
}
