// =====================================================================================
//  SHIFTERS - StoryOverlay.cs   [owned by STORY_KIT]
//  Runtime story dressing of the existing L1-L5 scenes (CAMPAIGN §8.3): StoryOverlay.Apply(overlayId) builds
//  StoryLayout_L<n> under a scene-root object "StoryOverlay_L<n>" - collider-free, light-free, anchored to the scene's
//  LevelMarks (ME-TV billboards in the void, holo-posters beside checkpoints, Max statue, Percy, hologram taunt spots,
//  level extras). The campaign calls it on GameEvents.LevelStarted in campaign mode only. Idempotent (re-applying
//  rebuilds). Accepted ids: "L1".."L5", "1".."5", "StoryLayout_L1", or a scene name ("L1_TrainingFacility").
//  Tests / tools: -storyOverlays on the command line (or AutoApplyOnLevelStart = true) applies the overlay of the active
//  scene on every LevelStarted in ANY mode (used to run the L1-L5 solutions with overlays on).
// =====================================================================================
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class StoryOverlay
    {
        public static bool AutoApplyOnLevelStart;
        public static GameObject Current { get; private set; }
        public static int CurrentLevel { get; private set; }
        public static event Action<GameObject> Applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null; CurrentLevel = 0; Applied = null;
            AutoApplyOnLevelStart = false;
            try { foreach (var a in Environment.GetCommandLineArgs()) if (a == "-storyOverlays") AutoApplyOnLevelStart = true; } catch (Exception) { }
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelStarted += OnLevelStarted;
        }

        static void OnLevelStarted()
        {
            if (!AutoApplyOnLevelStart) return;
            Apply(SceneManager.GetActiveScene().name);
        }

        /// <summary>Level number 1..5 for an overlay id / scene name, 0 if none.</summary>
        public static int LevelOf(string overlayId)
        {
            if (string.IsNullOrEmpty(overlayId)) return 0;
            string s = overlayId.Trim();
            if (s.StartsWith("StoryLayout_", StringComparison.OrdinalIgnoreCase)) s = s.Substring(12);
            if (s.StartsWith("L", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            int n = 0, i = 0;
            while (i < s.Length && char.IsDigit(s[i])) { n = n * 10 + (s[i] - '0'); i++; }
            if (i == 0) return 0;
            if (i < s.Length && s[i] != '_') return 0;
            return n >= 1 && n <= 5 ? n : 0;
        }

        public static bool Has(string overlayId) => LevelOf(overlayId) > 0;

        /// <summary>CAMPAIGN §18.2: build the overlay (no-op + warning for unknown ids).</summary>
        public static void Apply(string overlayId) { ApplyAndGet(overlayId); }

        /// <summary>(additive) Apply and return the overlay root (null for unknown ids).</summary>
        public static GameObject ApplyAndGet(string overlayId)
        {
            int n = LevelOf(overlayId);
            if (n == 0) { Debug.LogWarning("[StoryOverlay] no overlay for '" + overlayId + "'"); return null; }
            Clear();
            var root = new GameObject("StoryOverlay_L" + n);
            var prevTheme = Kit.Theme;
            Kit.Theme = LevelLighting.Current;
            var u = new StoryLayoutUtil(root.transform);
            try
            {
                switch (n)
                {
                    case 1: StoryLayout_L1.Build(u); break;
                    case 2: StoryLayout_L2.Build(u); break;
                    case 3: StoryLayout_L3.Build(u); break;
                    case 4: StoryLayout_L4.Build(u); break;
                    case 5: StoryLayout_L5.Build(u); break;
                }
            }
            catch (Exception e) { Debug.LogError("[StoryOverlay] L" + n + " failed: " + e); }
            finally { Kit.Theme = prevTheme; }
            Current = root; CurrentLevel = n;
            Debug.Log("[StoryOverlay] L" + n + " applied: " + u.Placed + " pieces (" + u.Skipped + " skipped: " + u.SkipLog + ")");
            Applied?.Invoke(root);
            return root;
        }

        public static void Clear()
        {
            if (Current != null) Kit.SafeDestroy(Current);
            Current = null; CurrentLevel = 0;
        }
    }
}
