// =====================================================================================
//  SHIFTERS - LightPool.cs   [PUZZLES - campaign wave, CAMPAIGN §10.2]
//  A 3 m emissive floor ring that counts as LIT for LightRegistry while powered (always on without inputs).
//  Optional real point light (no shadows; the factory allows at most PuzzleKit.MaxRealPoolLights per scene).
//  Switching flickers the ring for 0.45 s (old fluorescent tube feel). Built by PuzzleKit.LightPool.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class LightPool : SignalReceiver, ILightSource
    {
        public float Radius = Tuning.LightPoolRadius;
        [Tooltip("Emissive ring / disc renderers (swapped between OnMaterial and OffMaterial).")] public Renderer[] Glow = new Renderer[0];
        public Material OnMaterial;
        public Material OffMaterial;
        [Tooltip("Faint pool disc (hidden while off).")] public Renderer Disc;
        [Tooltip("Optional real point light (<= 3 per scene).")] public Light RealLight;
        public float LightIntensity = 2.2f;

        public Vector3 LightPosition => transform.position;
        public float LightRadius => Radius;
        public bool LightOn => Powered && isActiveAndEnabled;

        float _flicker;
        bool _shown, _shownInit;

        protected override void OnEnable()
        {
            base.OnEnable();
            LightRegistry.Register(this);
            Apply(Powered, true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            LightRegistry.Unregister(this);
        }

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (initial) { Apply(powered, true); return; }
            _flicker = 0.45f;
            Sfx.At(SfxId.ButtonPress, transform.position, 0.5f, powered ? 0.6f : 0.45f);
            if (powered) Fx.Sparks(transform.position + Vector3.up * 0.1f, Vector3.up, 6, new Color(1f, 0.9f, 0.6f));
        }

        protected override void Update()
        {
            base.Update();
            if (_flicker > 0f)
            {
                _flicker -= Time.deltaTime;
                bool on = _flicker <= 0f ? Powered : (Mathf.Repeat(_flicker, 0.12f) > 0.05f) == Powered;
                Apply(on, false);
            }
            else if (_shown != Powered) Apply(Powered, false);
        }

        void Apply(bool on, bool force)
        {
            if (!force && _shownInit && _shown == on) return;
            _shownInit = true;
            _shown = on;
            var m = on ? OnMaterial : OffMaterial;
            if (m != null && Glow != null) foreach (var r in Glow) if (r != null) r.sharedMaterial = m;
            if (Disc != null) Disc.enabled = on;
            if (RealLight != null) { RealLight.enabled = on; RealLight.intensity = LightIntensity; }
        }
    }
}
