// =====================================================================================
//  SHIFTERS - TimeController.cs   [CONTRACT API - implementation owned by the CORE agent]
//  Owns Time.timeScale (and scales Time.fixedDeltaTime with it). Nobody else writes timeScale.
//  Layers: pause (0) > hit-stop > slow-mo (switcher) > normal. Ramps use unscaled time.
//  The effective scale is the SLOWEST active layer (a hit-stop during the switcher never speeds
//  time up). Physics keeps ~60 steps per real second in slow-mo (fixedDeltaTime = 1/60 * scale,
//  min 1/600). Everything resets on scene load (no stuck slow-mo / pause after a restart).
//  "Real" time = Time.captureDeltaTime when a capture/test harness fixes the frame time (DevPlay:
//  1/60), else Time.unscaledDeltaTime. Use TimeController.RealDeltaTime / RealTime for UI tweens
//  and ramps that must ignore timeScale and stay deterministic in tests and video capture.
// =====================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public class TimeController : MonoBehaviour
    {
        public static TimeController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; _baseScale = 1f; _realTime = 0f; _realFrame = -1; }

        /// <summary>Seconds (unscaled) for a hit-stop to blend back to normal after its hold time.</summary>
        public const float HitStopRecoverTime = 0.12f;
        /// <summary>Smallest fixedDeltaTime we ever set (keeps slow-mo physics cheap).</summary>
        public const float MinFixedDeltaTime = 1f / 600f;

        public static bool IsPaused => Instance != null && Instance.paused;
        public static bool IsSlowMo => Instance != null && Instance.slowMo;
        /// <summary>True while a hit-stop is holding or recovering.</summary>
        public static bool IsHitStop => Instance != null && Instance._hitActive;
        /// <summary>Current effective time scale (what Time.timeScale is set to).</summary>
        public static float CurrentScale => Instance != null ? Instance._current : Time.timeScale;
        /// <summary>0..1 eased slow-mo blend (1 = fully in switcher slow-mo). Handy for UI/audio that want to follow the ramp.</summary>
        public static float SlowMoBlend => Instance != null ? Instance.Eased(Instance._slowT) : 0f;

        static float _realTime;
        static int _realFrame = -1;

        /// <summary>Unscaled seconds of this frame: Time.captureDeltaTime when set (DevPlay / recording), else Time.unscaledDeltaTime.</summary>
        public static float RealDeltaTime => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        /// <summary>Accumulated <see cref="RealDeltaTime"/> (seconds, advances once per frame, ignores timeScale/pause).</summary>
        public static float RealTime { get { Tick(); return _realTime; } }

        static void Tick()
        {
            int f = Time.frameCount;
            if (f == _realFrame) return;
            _realFrame = f;
            _realTime += Mathf.Min(RealDeltaTime, 0.25f);
        }

        static float _baseScale = 1f;
        /// <summary>Global "normal speed" multiplier (default 1). Tests/bots may fast-forward with e.g. 3; never write Time.timeScale directly.</summary>
        public static float BaseScale { get => _baseScale; set => _baseScale = Mathf.Clamp(value, 0.05f, 20f); }

        protected bool paused, slowMo;
        float _slowT;              // 0..1 linear ramp position (eased when applied)
        bool _hitActive;
        float _hitScale = 1f;
        float _hitHoldUntil;       // unscaled time when the hold ends (then blends out)
        float _current = 1f;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResetLayers();
            Apply(true);
        }

        protected virtual void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance != this) return;
            Instance = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Tuning.FixedTimestep;
        }

        public static void SetPaused(bool p) { if (Instance != null) Instance.ApplyPaused(p); else Time.timeScale = p ? 0f : 1f; }
        public static void SetSlowMo(bool on) { if (Instance != null) Instance.ApplySlowMo(on); }
        /// <summary>Brief dramatic slow-down (KO, collapse...). seconds in unscaled time. Snaps in, holds, blends out over
        /// <see cref="HitStopRecoverTime"/>. Overlapping calls keep the slowest scale and the latest end.</summary>
        public static void HitStop(float scale, float seconds) { if (Instance != null) Instance.ApplyHitStop(scale, seconds); }
        /// <summary>Clear pause, slow-mo and hit-stop instantly (scene loads do this automatically).</summary>
        public static void ResetAll() { if (Instance != null) Instance.ResetInstant(); else { Time.timeScale = 1f; Time.fixedDeltaTime = Tuning.FixedTimestep; } }

        protected virtual void ApplyPaused(bool p)
        {
            if (paused == p) return;
            paused = p;
            Apply(true);
            GameEvents.RaisePauseChanged(p);
        }

        protected virtual void ApplySlowMo(bool on)
        {
            slowMo = on;
            Apply(false);
        }

        protected virtual void ApplyHitStop(float scale, float seconds)
        {
            scale = Mathf.Clamp(scale, 0f, 1f);
            seconds = Mathf.Max(0f, seconds);
            float now = RealTime;
            bool holding = _hitActive && now < _hitHoldUntil;
            _hitScale = holding ? Mathf.Min(_hitScale, scale) : scale;
            _hitHoldUntil = holding ? Mathf.Max(_hitHoldUntil, now + seconds) : now + seconds;
            _hitActive = true;
            Apply(true);
        }

        void ResetInstant()
        {
            bool wasPaused = paused;
            ResetLayers();
            Apply(true);
            if (wasPaused) GameEvents.RaisePauseChanged(false);
        }

        void ResetLayers()
        {
            paused = false;
            slowMo = false;
            _slowT = 0f;
            _hitActive = false;
            _hitScale = 1f;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) ResetInstant();
        }

        void Update()
        {
            Tick();
            float dt = Mathf.Min(RealDeltaTime, 0.1f);
            float target = slowMo ? 1f : 0f;
            if (_slowT != target)
                _slowT = Mathf.MoveTowards(_slowT, target, dt / Mathf.Max(0.01f, Tuning.SwitcherRampTime));
            Apply(false);
        }

        float Eased(float t) => t * t * (3f - 2f * t);

        float HitFactor()
        {
            if (!_hitActive) return 1f;
            float now = RealTime;
            if (now < _hitHoldUntil) return _hitScale;
            float k = Mathf.Clamp01((now - _hitHoldUntil) / HitStopRecoverTime);
            if (k >= 1f) { _hitActive = false; return 1f; }
            return Mathf.Lerp(_hitScale, 1f, Eased(k));
        }

        /// <summary>Compute the layered scale and write Time.timeScale / fixedDeltaTime when they differ.</summary>
        void Apply(bool force)
        {
            float scale;
            if (paused) scale = 0f;
            else
            {
                float slow = Mathf.Lerp(1f, Tuning.SwitcherTimeScale, Eased(_slowT));
                scale = Mathf.Min(slow, HitFactor()) * _baseScale;
            }
            if (!force && Mathf.Approximately(scale, _current) && Mathf.Approximately(Time.timeScale, scale)) return;
            _current = scale;
            Time.timeScale = scale;
            Time.fixedDeltaTime = Mathf.Max(Tuning.FixedTimestep * Mathf.Min(scale, 1f), MinFixedDeltaTime);
        }
    }
}
