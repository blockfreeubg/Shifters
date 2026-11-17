// =====================================================================================
//  SHIFTERS - FlashlightRig.cs   (RENDERING, CAMPAIGN 10.2)
//  Exactly ONE real-time spot light (no shadows) carried by the ACTIVE intern: 50 deg visual cone, 14 m, tinted 20%
//  toward the intern's color. Aims along the camera forward for the human player and along the character's facing
//  under ExternalControl (bots: deterministic). On SHIFT it fades out on the old intern and in on the new one over
//  0.3 s total, so there is always exactly one light. Registers itself as an ILightCone with puzzles' LightRegistry
//  (the GEOMETRIC lit rule uses Tuning.FlashlightHalfAngle 25 deg / FlashlightRange 14 m, not the visual cone).
//  Inactive alive interns hold GLOWSTICKS (emissive stick + additive halo in their color, no Light).
//  Usage: FlashlightRig.Ensure() in a night scene (idempotent); FlashlightRig.Remove() to take it away.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public sealed class FlashlightRig : MonoBehaviour, ILightCone
    {
        public const float VisualConeDeg = 50f;
        public const float FadeSeconds = 0.3f;
        public const float TintTowardIntern = 0.2f;
        public static readonly Color BaseColor = new Color(1f, 0.95f, 0.84f);

        public static FlashlightRig Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [Tooltip("Flashlight switched on (players cannot toggle it; scripts may, e.g. a blackout cutscene).")]
        public bool On = true;
        [Tooltip("Inactive alive interns carry glowsticks.")]
        public bool Glowsticks = true;
        public float Intensity = 9f;
        [Tooltip("Height of the lamp above the intern's feet, as a fraction of the body height.")]
        public float HeightFraction = 0.72f;
        [Tooltip("Pitch added to the facing direction under ExternalControl (degrees down).")]
        public float BotPitchDown = 10f;

        Light _light;
        ShifterCharacter _target, _pending;
        float _fade = 1f;          // 0..1 multiplier
        int _fadeDir;              // -1 fading out (then switch), +1 fading in, 0 idle
        bool _registered;
        readonly Dictionary<ShifterCharacter, GameObject> _sticks = new Dictionary<ShifterCharacter, GameObject>();
        readonly List<ShifterCharacter> _tmpChars = new List<ShifterCharacter>(4);
        float _stickRefresh;

        // ------------------------------------------------------------------ ILightCone
        public Vector3 Origin => _light != null ? _light.transform.position : transform.position;
        public Vector3 Forward => _light != null ? _light.transform.forward : transform.forward;
        public float Range => Tuning.FlashlightRange;
        public float HalfAngle => Tuning.FlashlightHalfAngle;
        /// <summary>Geometric "on": switched on, carried by an alive intern and not mid-switch (fade above half).</summary>
        public bool LightOn => On && isActiveAndEnabled && _target != null && _target.IsAlive && _fade > 0.5f;

        /// <summary>The intern currently carrying the light (null if none).</summary>
        public ShifterCharacter Target => _target;
        /// <summary>The single Light component (spot, no shadows).</summary>
        public Light Light => _light;
        /// <summary>0..1 fade (1 = full).</summary>
        public float Fade => _fade;

        // ------------------------------------------------------------------ static API
        /// <summary>Finds or creates the scene's flashlight (idempotent). Play mode and edit mode (scene builders).</summary>
        public static FlashlightRig Ensure(Transform parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<FlashlightRig>();
            if (existing != null) { existing.Init(); return existing; }
            var go = new GameObject("FlashlightRig");
            if (parent != null) go.transform.SetParent(parent, false);
            var rig = go.AddComponent<FlashlightRig>();
            rig.Init();
            return rig;
        }

        /// <summary>Destroys the scene's flashlight (and its glowsticks).</summary>
        public static void Remove()
        {
            if (Instance == null) return;
            if (Application.isPlaying) Destroy(Instance.gameObject); else DestroyImmediate(Instance.gameObject);
            Instance = null;
        }

        /// <summary>Color the light takes for an intern color (20% toward it).</summary>
        public static Color TintFor(Color intern) => Color.Lerp(BaseColor, intern, TintTowardIntern);

        // ------------------------------------------------------------------ lifecycle
        void Awake() => Init();

        void Init()
        {
            if (Instance != null && Instance != this) { if (Application.isPlaying) Destroy(gameObject); return; }
            Instance = this;
            if (_light == null)
            {
                var t = transform.Find("Flashlight");
                if (t == null) { t = new GameObject("Flashlight").transform; t.SetParent(transform, false); }
                _light = t.GetComponent<Light>();
                if (_light == null) _light = t.gameObject.AddComponent<Light>();
            }
            _light.type = LightType.Spot;
            _light.shadows = LightShadows.None;
            _light.spotAngle = VisualConeDeg;
            _light.innerSpotAngle = VisualConeDeg * 0.55f;
            _light.range = Tuning.FlashlightRange;
            _light.intensity = Intensity;
            _light.color = BaseColor;
            _light.renderMode = LightRenderMode.ForcePixel;
            DecorLight.Mark(_light, true);   // the WebGL light budget must keep it
        }

        void OnEnable()
        {
            if (Application.isPlaying) GameEvents.CharacterSwitched += OnSwitched;
            Register(true);
        }

        void OnDisable()
        {
            if (Application.isPlaying) GameEvents.CharacterSwitched -= OnSwitched;
            Register(false);
        }

        void OnDestroy()
        {
            Register(false);
            foreach (var kv in _sticks) if (kv.Value != null) Destroy(kv.Value);
            _sticks.Clear();
            if (Instance == this) Instance = null;
        }

        void Register(bool on)
        {
            if (on == _registered) return;
            _registered = on;
            if (on) LightRegistryBridge.RegisterCone(this); else LightRegistryBridge.UnregisterCone(this);
        }

        void OnSwitched(ShifterCharacter from, ShifterCharacter to)
        {
            if (to == _target) return;
            _pending = to;
            if (_target == null || _fade <= 0.001f) { _target = to; _pending = null; _fade = 0f; _fadeDir = 1; }
            else _fadeDir = -1;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            var cm = CharacterManager.Instance;
            var active = cm != null ? cm.Active : null;
            if (_fadeDir == 0 && active != _target) OnSwitched(_target, active);

            float step = Time.deltaTime / (FadeSeconds * 0.5f);
            if (_fadeDir < 0)
            {
                _fade -= step;
                if (_fade <= 0f) { _fade = 0f; _target = _pending; _pending = null; _fadeDir = _target != null ? 1 : 0; }
            }
            else if (_fadeDir > 0)
            {
                _fade += step;
                if (_fade >= 1f) { _fade = 1f; _fadeDir = 0; }
            }

            bool show = On && _target != null && _target.IsAlive && !_target.IsHidden;
            if (_light != null)
            {
                _light.enabled = show && _fade > 0.001f;
                if (show) Aim(_target);
                _light.intensity = Intensity * _fade * _fade;
            }

            _stickRefresh -= Time.deltaTime;
            if (_stickRefresh <= 0f) { _stickRefresh = 0.25f; RefreshGlowsticks(cm); }
        }

        void Aim(ShifterCharacter c)
        {
            var ct = c.transform;
            var lt = _light.transform;
            var fwd = ct.forward;
            Vector3 dir;
            var cam = ShiftersCamera.Instance != null && ShiftersCamera.Instance.Cam != null ? ShiftersCamera.Instance.Cam.transform : (Camera.main != null ? Camera.main.transform : null);
            if (!c.ExternalControl && cam != null) dir = cam.forward;
            else dir = Quaternion.AngleAxis(BotPitchDown, Vector3.Cross(Vector3.up, fwd).normalized) * fwd;
            if (dir.sqrMagnitude < 1e-4f) dir = fwd;
            lt.position = ct.position + Vector3.up * (c.Height * HeightFraction) + fwd * (c.Radius + 0.05f);
            lt.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            _light.color = TintFor(c.Color);
        }

        // ------------------------------------------------------------------ glowsticks (no lights: emissive + halo)
        void RefreshGlowsticks(CharacterManager cm)
        {
            _tmpChars.Clear();
            if (cm != null && Glowsticks)
                for (int i = 0; i < 8; i++)
                {
                    var c = cm.Get(i);
                    if (c != null) _tmpChars.Add(c);
                }
            // remove sticks of interns that are active / gone / not alive
            List<ShifterCharacter> drop = null;
            foreach (var kv in _sticks)
            {
                var c = kv.Key;
                bool keep = c != null && _tmpChars.Contains(c) && c != _target && c.IsAlive;
                if (!keep) { (drop ??= new List<ShifterCharacter>()).Add(c); if (kv.Value != null) Destroy(kv.Value); }
            }
            if (drop != null) foreach (var c in drop) _sticks.Remove(c);
            for (int i = 0; i < _tmpChars.Count; i++)
            {
                var c = _tmpChars[i];
                if (c == _target || !c.IsAlive || _sticks.ContainsKey(c)) continue;
                _sticks[c] = BuildGlowstick(c);
            }
        }

        static GameObject BuildGlowstick(ShifterCharacter c)
        {
            var col = c.Color;
            var root = new GameObject("Glowstick");
            root.transform.SetParent(c.transform, false);
            root.transform.localPosition = new Vector3(c.Radius + 0.12f, c.Height * 0.48f, 0.12f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            stick.name = "Stick";
            var sc = stick.GetComponent<Collider>(); if (sc != null) Destroy(sc);
            stick.transform.SetParent(root.transform, false);
            stick.transform.localScale = new Vector3(0.06f, 0.11f, 0.06f);
            var mr = stick.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Unlit(col, 3f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Halo";
            var hc = halo.GetComponent<Collider>(); if (hc != null) Destroy(hc);
            halo.transform.SetParent(root.transform, false);
            halo.transform.localScale = Vector3.one * 0.75f;
            var hr = halo.GetComponent<MeshRenderer>();
            hr.sharedMaterial = MaterialLibrary.Hologram(new Color(col.r, col.g, col.b, 0.22f), true);
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }
    }
}
