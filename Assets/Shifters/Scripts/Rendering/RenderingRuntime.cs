// =====================================================================================
//  SHIFTERS - RenderingRuntime.cs   (RENDERING)
//  Hidden persistent runner for the campaign-wave rendering systems: animates WorldColor (bursts, waves, fades) and the
//  LevelLighting override, captures each scene's look on load, and wires the GameEvents hooks (LevelStarted,
//  PuzzleSolvedAt, SettingsChanged). Created automatically in play mode; never saved in scenes.
// =====================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    [AddComponentMenu("")]
    public sealed class RenderingRuntime : MonoBehaviour
    {
        static RenderingRuntime _instance;
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; _hooked = false; }

        // GameEvents.ClearAll runs at SubsystemRegistration: subscribe afterwards.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => Ensure();

        public static RenderingRuntime Ensure()
        {
            if (!Application.isPlaying) return null;
            Hook();
            if (_instance != null) return _instance;
            var go = new GameObject("[RenderingRuntime]") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RenderingRuntime>();
            return _instance;
        }

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.LevelStarted += WorldColor.OnLevelStarted;
            GameEvents.PuzzleSolvedAt += WorldColor.OnPuzzleSolvedAt;
            GameEvents.SettingsChanged += WorldColor.OnSettingsChanged;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            LevelLighting.OnSceneLoaded();
            WorldColor.OnSceneLoaded(scene, mode);
        }

        void Update()
        {
            // unscaled (pause menus don't freeze a burst mid-ring); a fixed capture step (recording / DevPlay) wins
            float dt = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            if (dt > 0.1f) dt = 0.1f;
            WorldColor.Tick(dt);
            LevelLighting.Tick(dt);
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
