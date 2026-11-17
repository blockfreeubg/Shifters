// =====================================================================================
//  SHIFTERS - LevelCompleteHold.cs   (UI agent)
//  Lets a reaction (Max's ME-TV PiP line after a chapter, §15.2) hold the Level Complete screen back for at
//  most MaxSeconds (3 s) after GameEvents.LevelCompleted. UIManager waits its usual 0.9 s, then keeps waiting
//  while a hold is active. Ignored in CampaignDirector.BotMode. Cleared on every scene load.
//    LevelCompleteHold.Request(2.5f);   // from a LevelCompleted handler (same frame or within 0.9 s)
//    LevelCompleteHold.Release();       // the line finished early
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class LevelCompleteHold
    {
        public const float MaxSeconds = 3f;
        static float _until = -1f, _first = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _until = -1f; _first = -1f; }

        /// <summary>True while the Level Complete screen is being held back.</summary>
        public static bool Active => _until > 0f && UITime.Now < _until;
        public static float Remaining => Active ? _until - UITime.Now : 0f;

        /// <summary>Hold the Level Complete screen for up to seconds (total hold per completion is capped at MaxSeconds).</summary>
        public static void Request(float seconds)
        {
            if (CampaignLink.BotMode || seconds <= 0f) return;
            float now = UITime.Now;
            if (!Active) _first = now;
            _until = Mathf.Min(Mathf.Max(_until, now + seconds), _first + MaxSeconds);
        }

        public static void Release() { _until = -1f; _first = -1f; }
    }
}

namespace Shifters
{
    /// <summary>Optional extras a level / the story can hand the Level Complete screen before (or within 0.9 s of)
    /// GameEvents.LevelCompleted. Cleared on every scene load.
    ///   LevelCompleteExtras.StealthGrade = "GHOST";   // CH04 Act Natural: GHOST / CASUAL / LOUD (by detections)
    ///   LevelCompleteExtras.MaxReaction = "...";     // Max's live ME-TV line (else a UI line by grade)</summary>
    public static class LevelCompleteExtras
    {
        public static string StealthGrade;
        public static int Detections = -1;
        public static string MaxReaction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() { StealthGrade = null; Detections = -1; MaxReaction = null; }

        /// <summary>GHOST (0 detections) / CASUAL (1-2) / LOUD (3+).</summary>
        public static string GradeFor(int detections) => detections <= 0 ? "GHOST" : detections <= 2 ? "CASUAL" : "LOUD";
    }
}
