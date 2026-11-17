// =====================================================================================
//  SHIFTERS - BenchOverlay.cs   (polish hud, CAMPAIGN §21 BenchmarkMode / gate G10)
//  Small performance read-out for human playtesters: "FPS 60 · AVG 59.8 · 1% LOW 52 · 16.7 MS" in a
//  dark pill at the bottom-right corner. OFF by default. On when:
//    * the WebGL page URL has ?bench=1 (Application.absoluteURL, like the template's ?dpr=), or the
//      player was started with -bench (desktop / editor), or
//    * Settings > GAME > SHOW FPS is ON (SettingsData.ShowFps, saved with the other settings).
//  Window: the last 1024 frames (≈ 17 s at 60 FPS), unscaled time. AVG = frames / time, 1% LOW = the
//  mean of the slowest 1 % of those frames. The text refreshes twice a second (no per-frame allocations).
//  With ?bench=1 it also logs one "[Bench]" line every 10 s to the browser console (scene + numbers).
//  Own canvas (sort order 140): above menus, the chapter card and the fader; never blocks raycasts.
// =====================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class BenchOverlay : MonoBehaviour
    {
        public const int SortOrder = 140, Window = 1024;
        /// <summary>Widest pill: stays right of the DialogueBox (which ends at x 1480 of 1920) and clear of the chapter card's skip hint.</summary>
        const float MaxW = 410f;
        const float RefreshSeconds = 0.5f, LogSeconds = 10f;

        public static BenchOverlay Instance { get; private set; }
        /// <summary>?bench=1 / -bench was given (BenchmarkMode).</summary>
        public static bool BenchMode { get; private set; }
        /// <summary>Forces the overlay on/off regardless of settings (tests). Null = automatic.</summary>
        public static bool? ForceVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; BenchMode = false; ForceVisible = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            BenchMode = UrlHasBench(Application.absoluteURL) || HasBenchArg();
            if (Instance != null || !Application.isPlaying) return;
            var go = new GameObject("[BenchOverlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<BenchOverlay>();
        }

        /// <summary>True when the URL query has bench=1 (also bench, bench=true / yes / on).</summary>
        public static bool UrlHasBench(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            int q = url.IndexOf('?');
            if (q < 0) return false;
            int hash = url.IndexOf('#', q);
            string query = hash > q ? url.Substring(q + 1, hash - q - 1) : url.Substring(q + 1);
            foreach (var part in query.Split('&'))
            {
                int eq = part.IndexOf('=');
                string key = eq >= 0 ? part.Substring(0, eq) : part;
                if (!string.Equals(key, "bench", System.StringComparison.OrdinalIgnoreCase)) continue;
                string v = eq >= 0 ? part.Substring(eq + 1).ToLowerInvariant() : "1";
                return v == "1" || v == "true" || v == "yes" || v == "on" || v.Length == 0;
            }
            return false;
        }

        static bool HasBenchArg()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            try { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-bench") return true; }
            catch (System.Exception) { }
            return false;
#endif
        }

        /// <summary>The overlay is drawn right now.</summary>
        public bool Visible { get; private set; }
        public float Fps { get; private set; }
        public float AvgFps { get; private set; }
        public float Low1Fps { get; private set; }
        public float FrameMs { get; private set; }
        public string Text => _text != null ? _text.text : "";

        Canvas _canvas;
        RectTransform _pill;
        TextMeshProUGUI _text;
        readonly float[] _dt = new float[Window];
        readonly float[] _sorted = new float[Window];
        int _count, _head;
        float _refreshT, _logT, _shortSum;
        int _shortFrames;

        public static bool WantVisible()
        {
            if (ForceVisible.HasValue) return ForceVisible.Value;
            return BenchMode || SessionActions.Settings.ShowFps;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvas = UIFactory.Canvas("Bench Canvas", transform, SortOrder, false);
            _pill = UIFactory.Rect("Bench", _canvas.transform, new Vector2(1f, 0f), new Vector2(-14f, 4f), new Vector2(MaxW, 26f), new Vector2(1f, 0f));
            var bg = UIFactory.Fill(_pill, "Bg", UISprites.Rounded(8), new Color(0.03f, 0.04f, 0.06f, 0.72f));
            bg.raycastTarget = false;
            _text = UIFactory.Text(_pill, "Text", "", FontLibrary.Label, 17f, UIColors.Text, TextAlignmentOptions.Right);
            UIFactory.Stretch(_text.rectTransform, 10f, 10f, 0f, 0f);
            _text.characterSpacing = 1f;
            UIFactory.AutoSize(_text, 12f, 17f);
            _canvas.gameObject.SetActive(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            bool want = WantVisible();
            if (want != Visible)
            {
                Visible = want;
                _canvas.gameObject.SetActive(want);
                _count = 0; _head = 0; _refreshT = 0f; _logT = 0f; _shortSum = 0f; _shortFrames = 0;
                if (want) _text.text = "FPS --";
            }
            if (!Visible) return;

            float dt = UITime.Delta;   // unscaled (pause / slow-mo don't count); the fixed capture step in tests
            if (dt <= 0f || dt > 1f) return;   // first frame / tab in the background: not a rendered frame
            _dt[_head] = dt; _head = (_head + 1) % Window; if (_count < Window) _count++;
            _shortSum += dt; _shortFrames++;
            _refreshT += dt; _logT += dt;
            if (_refreshT < RefreshSeconds) return;

            Fps = _shortFrames / Mathf.Max(1e-4f, _shortSum);
            FrameMs = 1000f * _shortSum / Mathf.Max(1, _shortFrames);
            _refreshT = 0f; _shortSum = 0f; _shortFrames = 0;
            float sum = 0f;
            for (int i = 0; i < _count; i++) { _sorted[i] = _dt[i]; sum += _dt[i]; }
            AvgFps = _count / Mathf.Max(1e-4f, sum);
            System.Array.Sort(_sorted, 0, _count);
            int worst = Mathf.Max(1, _count / 100);
            float wsum = 0f;
            for (int i = _count - worst; i < _count; i++) wsum += _sorted[i];
            Low1Fps = worst / Mathf.Max(1e-4f, wsum);

            _text.text = "FPS <color=" + Hex(Fps) + ">" + Mathf.RoundToInt(Fps) + "</color>  ·  AVG " + AvgFps.ToString("0.0")
                       + "  ·  1% LOW <color=" + Hex(Low1Fps) + ">" + Mathf.RoundToInt(Low1Fps) + "</color>  ·  " + FrameMs.ToString("0.0") + " MS";
            _pill.sizeDelta = new Vector2(Mathf.Min(MaxW, _text.GetPreferredValues(_text.text, 9999f, 26f).x + 24f), 26f);
            if (BenchMode && _logT >= LogSeconds)
            {
                _logT = 0f;
                Debug.Log("[Bench] scene=" + SceneManager.GetActiveScene().name + " fps=" + Fps.ToString("0.0") + " avg=" + AvgFps.ToString("0.0")
                          + " low1=" + Low1Fps.ToString("0.0") + " ms=" + FrameMs.ToString("0.0") + " frames=" + _count
                          + " screen=" + Screen.width + "x" + Screen.height);
            }
        }

        static string Hex(float fps) => fps >= 55f ? "#7CFFB2" : fps >= 30f ? "#FFD34D" : "#FF4D5E";
    }
}
