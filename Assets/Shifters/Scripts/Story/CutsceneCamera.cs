// =====================================================================================
//  SHIFTERS - CutsceneCamera.cs   [story]
//  The one camera that renders during blocking story scenes. StoryPlayer enables it and suppresses the
//  gameplay camera (StoryBridge.SuppressMainCamera), so exactly one camera draws. Starts at the gameplay
//  camera's pose/settings, then glides between Shot targets and drifts slowly for life.
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shifters
{
    public sealed class CutsceneCamera : MonoBehaviour
    {
        public static CutsceneCamera Instance { get; private set; }
        public Camera Cam { get; private set; }
        public bool Active => Cam != null && Cam.enabled;

        Vector3 _fromPos, _toPos, _lookFrom, _lookTo;
        float _fovFrom, _fovTo, _t, _dur;
        bool _hasLook;
        float _drift;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            Instance = this;
            Cam = GetComponent<Camera>();
            if (Cam == null) Cam = gameObject.AddComponent<Camera>();
            Cam.enabled = false;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static CutsceneCamera Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[CutsceneCamera]");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<CutsceneCamera>();
        }

        /// <summary>Take over from the gameplay camera (copies pose, fov, clear flags, culling, post-processing).</summary>
        public void Begin(Camera from)
        {
            if (Cam == null) Awake();
            if (from != null)
            {
                Cam.CopyFrom(from);
                transform.SetPositionAndRotation(from.transform.position, from.transform.rotation);
                try
                {
                    var src = from.GetUniversalAdditionalCameraData();
                    var dst = Cam.GetUniversalAdditionalCameraData();
                    if (src != null && dst != null) { dst.renderPostProcessing = src.renderPostProcessing; dst.antialiasing = src.antialiasing; }
                }
                catch { }
            }
            Cam.targetTexture = null;
            Cam.depth = 10f;
            _dur = 0f; _t = 0f; _hasLook = false; _drift = 0f;
            _fromPos = _toPos = transform.position;
            _fovFrom = _fovTo = Cam.fieldOfView;
            Cam.enabled = true;
        }

        public void End()
        {
            if (Cam != null) Cam.enabled = false;
            _drift = 0f;
        }

        /// <summary>Glide (seconds > 0) or cut (0) to a pose looking at a point.</summary>
        public void GlideTo(Vector3 pos, Vector3 lookAt, float fov, float seconds)
        {
            if (Cam == null) return;
            _fromPos = transform.position;
            _lookFrom = transform.position + transform.forward * Mathf.Max(1f, Vector3.Distance(pos, lookAt));
            _toPos = pos;
            _lookTo = lookAt;
            _hasLook = true;
            _fovFrom = Cam.fieldOfView;
            _fovTo = fov > 1f ? fov : Cam.fieldOfView;
            _dur = Mathf.Max(0f, seconds);
            _t = 0f;
            _drift = 0f;
            if (_dur <= 0f) Apply(1f);
        }

        public void Drift(float metersPerSecond) { _drift = metersPerSecond; }

        void Apply(float k)
        {
            float e = k * k * (3f - 2f * k);
            var p = Vector3.Lerp(_fromPos, _toPos, e);
            transform.position = p;
            if (_hasLook)
            {
                var look = Vector3.Lerp(_lookFrom, _lookTo, e);
                var d = look - p;
                if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            Cam.fieldOfView = Mathf.Lerp(_fovFrom, _fovTo, e);
        }

        void LateUpdate()
        {
            if (Cam == null || !Cam.enabled) return;
            float dt = ShiftersCamera.UnscaledDeltaTime * Mathf.Max(0.01f, StoryPlayer.TimeScale);
            if (_dur > 0f && _t < _dur)
            {
                _t += dt;
                Apply(Mathf.Clamp01(_t / _dur));
            }
            else if (_drift != 0f)
            {
                transform.position += transform.forward * (_drift * dt);
            }
        }
    }
}
