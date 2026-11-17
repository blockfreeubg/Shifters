// =====================================================================================
//  SHIFTERS - LightFlicker.cs   (RENDERING)
//  Flickering light + emissive face for Lab "flicker-ready" lighting and broken panels (EnvKit.AddFlicker).
//  Own file named after the class so scene/prefab-saved instances bind (see SlowSpin.cs). The emissive face keeps
//  flickering when the WebGL/Mobile light budget switched the real Light off (LightBudget / DecorLight).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Flickering light + emissive face (Lab "flicker-ready" lighting, broken panels).</summary>
    public class LightFlicker : MonoBehaviour
    {
        public Light Target;
        public Renderer Glow;
        public float Amount = 0.7f;
        public float Speed = 1f;
        float _baseIntensity = -1f, _seed, _blackoutUntil, _nextBlackout;
        Color _glowColor;
        MaterialPropertyBlock _mpb;

        void OnEnable()
        {
            _seed = Random.value * 100f;
            if (Target != null && _baseIntensity < 0f) _baseIntensity = Target.intensity;
            if (Glow != null)
            {
                _mpb = new MaterialPropertyBlock();
                var m = Glow.sharedMaterial;
                _glowColor = m != null && m.HasProperty(ShaderIds.EmissionColor) ? m.GetColor(ShaderIds.EmissionColor) : Color.white;
            }
            _nextBlackout = Time.time + Random.Range(1.5f, 5f);
        }

        void Update()
        {
            float t = Time.time * Speed;
            float n = Mathf.PerlinNoise(_seed, t * 6f);
            float k = 1f - Amount * Mathf.Clamp01((0.55f - n) * 3f);
            if (Time.time >= _nextBlackout) { _blackoutUntil = Time.time + Random.Range(0.05f, 0.25f); _nextBlackout = Time.time + Random.Range(1.5f, 6f); }
            if (Time.time < _blackoutUntil) k = 1f - Amount;
            if (Target != null && Target.enabled) Target.intensity = _baseIntensity * k;
            if (Glow != null && _mpb != null)
            {
                _mpb.SetColor(ShaderIds.EmissionColor, _glowColor * k);
                Glow.SetPropertyBlock(_mpb);
            }
        }
    }
}
