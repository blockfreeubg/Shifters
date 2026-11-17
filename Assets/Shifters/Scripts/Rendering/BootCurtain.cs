// =====================================================================================
//  SHIFTERS - BootCurtain.cs   (RENDERING)
//  Opaque black overlay over the very first rendered frames of the application. On a fresh launch the first
//  player-loop frame of the MainMenu rendered runtime-created materials with wrong constants (stage puppets dark, no
//  emission; the "first frames render _BaseColor-tinted materials white" artifact of the rendering report). Frame 2
//  onwards is correct, and level loads (behind the GameSession / LevelManager fade) were clean in every test, so only
//  the boot frames are exposed. The curtain hides them whatever the root cause: created before the first scene loads,
//  it stays up for the player-loop renders of the first CoveredFrames frames, then removes itself. Overlay canvas with
//  the highest sorting order and no raycaster: never blocks input. Play mode only (RuntimeInitializeOnLoadMethod).
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    /// <summary>Black overlay covering the first rendered frames after start-up (see file header). Self-destructs.</summary>
    public class BootCurtain : MonoBehaviour
    {
        /// <summary>Frames rendered under the curtain after start-up.</summary>
        public const int CoveredFrames = 2;

        /// <summary>True while the curtain is up.</summary>
        public static bool Covering => _instance != null && _instance._canvas != null && _instance._canvas.enabled;
        /// <summary>Time.frameCount of the LateUpdate that lowered the curtain (-1 while up / never created).</summary>
        public static int LoweredAtFrame { get; private set; } = -1;

        static BootCurtain _instance;
        Canvas _canvas;
        int _lateUpdates;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            LoweredAtFrame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Raise()
        {
            if (_instance != null) return;
            var go = new GameObject("[BootCurtain]");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var plate = new GameObject("Black", typeof(RectTransform)).AddComponent<RawImage>();
            plate.transform.SetParent(go.transform, false);
            plate.color = Color.black;
            plate.raycastTarget = false;
            var rt = plate.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _instance = go.AddComponent<BootCurtain>();
            _instance._canvas = canvas;
        }

        void LateUpdate()
        {
            // LateUpdate #n runs right before frame n renders: stay up for frames 1..CoveredFrames.
            if (++_lateUpdates <= CoveredFrames) return;
            if (_canvas != null) _canvas.enabled = false;
            LoweredAtFrame = Time.frameCount;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
