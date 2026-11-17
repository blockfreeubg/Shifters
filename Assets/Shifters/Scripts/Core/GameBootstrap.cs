// =====================================================================================
//  SHIFTERS - GameBootstrap.cs   [owned by the CORE agent]
//  Runs before the first scene loads (any scene - so pressing Play inside a level works).
//  Creates the persistent "[SHIFTERS]" root with the global managers, applies physics/time
//  settings (gravity 25, 60Hz), initializes input, applies saved settings, and resets statics
//  (domain reload is DISABLED in this project's Enter Play Mode settings!).
//  Every manager is added in isolation: one module throwing in Awake never stops the others.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public static class GameBootstrap
    {
        public const int TargetFrameRate = 120;

        public static GameObject Root { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Root = null; GameEvents.ClearAll(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Root != null) return;
            Physics.gravity = new Vector3(0f, -Tuning.Gravity, 0f);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Tuning.FixedTimestep;
            Application.runInBackground = true;
            if (!Application.isBatchMode)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = TargetFrameRate;
            }

            Root = new GameObject("[SHIFTERS]");
            UnityEngine.Object.DontDestroyOnLoad(Root);
            Add<GameSession>();
            Add<TimeController>();
            Add<AudioManager>();
            Add<PerspectiveManager>();
            Add<UIManager>();

            try { ShiftersInput.Initialize(); }
            catch (Exception e) { Debug.LogException(e); }
            try { GameSession.Instance?.ApplySettings(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Add<T>() where T : Component
        {
            try { if (Root.GetComponent<T>() == null) Root.AddComponent<T>(); }
            catch (Exception e) { Debug.LogError("[GameBootstrap] failed to create " + typeof(T).Name + ": " + e); }
        }
    }
}
