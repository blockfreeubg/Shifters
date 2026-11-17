// =====================================================================================
//  SHIFTERS - LevelAttempt.cs   [owned by the LEVEL agent]
//  Bookkeeping that must survive scene reloads within ONE attempt at a level:
//  elapsed time and switch count (kept across "Restart Checkpoint", reset by "Restart Level"),
//  and the number of restarts. An attempt ends when the level completes or when a scene
//  without a LevelManager (menu) is loaded.
// =====================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class LevelAttempt
    {
        public static int LevelIndex { get; private set; } = -1;
        public static int Restarts { get; private set; }
        static float _bankedTime;
        static int _bankedSwitches;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LevelIndex = -1; Restarts = 0; _bankedTime = 0f; _bankedSwitches = 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>Called by LevelManager.Start. Returns true when this is a reload of the attempt in progress.</summary>
        public static bool Begin(int levelIndex, bool fromCheckpoint, out float time, out int switches)
        {
            bool continuing = LevelIndex == levelIndex;
            if (continuing) Restarts++;
            else { LevelIndex = levelIndex; Restarts = 0; _bankedTime = 0f; _bankedSwitches = 0; }
            if (continuing && fromCheckpoint) { time = _bankedTime; switches = _bankedSwitches; }
            else { time = 0f; switches = 0; }
            return continuing;
        }

        /// <summary>LevelManager.OnDestroy: remember progress in case the same level reloads (checkpoint restart).</summary>
        public static void Bank(int levelIndex, float time, int switches)
        {
            if (levelIndex != LevelIndex) return;
            _bankedTime = time;
            _bankedSwitches = switches;
        }

        /// <summary>The attempt is over (level completed, main menu, restart tutorial).</summary>
        public static void Close() { LevelIndex = -1; Restarts = 0; _bankedTime = 0f; _bankedSwitches = 0; }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            var lm = LevelManager.Instance;
            if (lm == null || lm.gameObject.scene != scene) Close();
        }
    }
}
