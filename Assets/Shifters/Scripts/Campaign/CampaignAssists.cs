// =====================================================================================
//  SHIFTERS - CampaignAssists.cs   [owned by the CAMPAIGN agent]
//  HR INTERVENTION + BOSS MERCY bookkeeping (CAMPAIGN.md §16, §11.1 B10).
//  * HR INTERVENTION: after 3 restarts of the same checkpoint, or 4 min inside one section,
//    the Pause screen offers "LET HR HANDLE IT" (ShouldOfferHR) -> UseHR() ->
//    GameSession.RestartAtNextCheckpoint(). Never in CH14. Settings.HRAssists gates it.
//  * BOSS MERCY: 2 team wipes in the same boss phase -> BossBase.MercyLevel = 1 (tells +30%,
//    damage -30%) on that boss, re-applied whenever the boss reports that phase again.
//  Fed by CampaignDirector (events); state is per level (cleared when another level loads).
// =====================================================================================
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Shifters
{
    public static class CampaignAssists
    {
        public const int RestartsForHR = 3;
        public const float SecondsForHR = 240f;
        public const int WipesForMercy = 2;
        public const int NoHRCatalogIndex = 13;   // CH14 ALL IN FAVOR?
        public const string MercyToast = "HR SENT A MEMO ABOUT WORKPLACE SAFETY";
        public const string HRButton = "LET HR HANDLE IT";

        static int _levelIndex = int.MinValue;
        static int _sectionCp = int.MinValue;
        static readonly Dictionary<string, int> _wipes = new Dictionary<string, int>();
        static BossStatus _boss;
        static bool _hasBoss;
        static string _mercyKey;
        static int WipesInPhaseKey(string k) => _wipes.TryGetValue(k, out int n) ? n : 0;

        /// <summary>Restarts of the current section (checkpoint) in a row.</summary>
        public static int RestartsAtCheckpoint { get; private set; }
        /// <summary>Unpaused seconds spent in the current section.</summary>
        public static float SectionSeconds { get; private set; }
        /// <summary>Test hook: force the offer on/off (null = rules).</summary>
        public static bool? ForceOffer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ResetAll(); ForceOffer = null; }

        public static void ResetAll()
        {
            _levelIndex = int.MinValue; _sectionCp = int.MinValue; RestartsAtCheckpoint = 0; SectionSeconds = 0f;
            _wipes.Clear(); _hasBoss = false; _boss = default; _mercyKey = null;
        }

        static bool Enabled
        {
            get { var gs = GameSession.Instance; return gs == null || gs.Settings == null || gs.Settings.HRAssists; }
        }

        /// <summary>Read by the Pause screen: show "LET HR HANDLE IT".</summary>
        public static bool ShouldOfferHR
        {
            get
            {
                if (ForceOffer.HasValue) return ForceOffer.Value;
                if (!Enabled) return false;
                var lm = LevelManager.Instance;
                if (lm == null || !lm.IsStarted || lm.IsCompleted || lm.LevelIndex == NoHRCatalogIndex) return false;
                return RestartsAtCheckpoint >= RestartsForHR || SectionSeconds >= SecondsForHR;
            }
        }

        /// <summary>Pause button: restart at the NEXT checkpoint (seams). Counted in Stats.HRInterventions by GameSession and
        /// in the campaign save. Returns false if not offered or there is no next checkpoint.</summary>
        public static bool UseHR()
        {
            if (!ShouldOfferHR) return false;
            if (!CampaignDeps.RestartAtNextCheckpoint()) return false;
            CampaignDirector.Instance?.NoteHRUsed();
            RestartsAtCheckpoint = 0;
            SectionSeconds = 0f;
            return true;
        }

        public static int WipesInPhase(string bossId, int phase)
            => _wipes.TryGetValue(Key(bossId, phase), out int n) ? n : 0;

        /// <summary>The mercy level a boss phase should run at (0 normal, 1 = B10 mercy).</summary>
        public static int MercyLevelFor(string bossId, int phase) => WipesInPhase(bossId, phase) >= WipesForMercy ? 1 : 0;

        static string Key(string bossId, int phase) => (bossId ?? "") + "#" + phase;

        // ---------------------------------------------------------------- fed by CampaignDirector
        internal static void NoteLevelStarted(int levelIndex, bool fromCheckpoint, int checkpointIndex)
        {
            int cp = fromCheckpoint ? checkpointIndex : -1;
            _mercyKey = null;   // the scene (re)loaded: bosses are fresh, re-apply mercy on the next BossChanged
            if (levelIndex != _levelIndex)
            {
                _levelIndex = levelIndex; _sectionCp = cp; RestartsAtCheckpoint = 0; SectionSeconds = 0f;
                _wipes.Clear(); _hasBoss = false;
                return;
            }
            if (cp == _sectionCp) RestartsAtCheckpoint++;
            else { _sectionCp = cp; RestartsAtCheckpoint = 0; SectionSeconds = 0f; }
        }

        internal static void NoteCheckpoint(int index)
        {
            if (index == _sectionCp) return;
            _sectionCp = index; RestartsAtCheckpoint = 0; SectionSeconds = 0f;
        }

        internal static void Tick(float dt) { SectionSeconds += dt; }

        internal static void NoteBoss(BossStatus s)
        {
            _boss = s; _hasBoss = s.Visible && s.State != BossState.Defeated && !string.IsNullOrEmpty(s.BossId);
            if (!_hasBoss) return;
            // mercy is per phase: (re)apply only when the boss/phase changes (BossChanged can fire often)
            string k = Key(s.BossId, s.Phase);
            if (k == _mercyKey) return;
            bool hadMercy = _mercyKey != null && _mercyKey.StartsWith(s.BossId + "#", StringComparison.Ordinal) && WipesInPhaseKey(_mercyKey) >= WipesForMercy;
            _mercyKey = k;
            int lvl = MercyLevelFor(s.BossId, s.Phase);
            if (lvl > 0 || hadMercy) CampaignDeps.SetBossMercy(s.BossId, lvl);
        }

        internal static void NoteWipe()
        {
            if (!_hasBoss) return;
            string k = Key(_boss.BossId, _boss.Phase);
            _wipes.TryGetValue(k, out int n);
            _wipes[k] = ++n;
            if (n == WipesForMercy)
            {
                CampaignDeps.SetBossMercy(_boss.BossId, 1);
                GameEvents.RaiseToast(MercyToast, UIColors.Text);
            }
        }
    }
}
