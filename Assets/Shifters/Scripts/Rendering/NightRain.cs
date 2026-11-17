// =====================================================================================
//  SHIFTERS - NightRain.cs   (RENDERING, campaign wave: the Night theme's rain/fog feel)
//  One pooled ParticleSystem of stretched rain streaks in a box around the main camera (follows it, world-space
//  particles), plus faint ground splashes. Cheap on WebGL (<= 700 particles, unlit additive). Outdoor night chapters
//  call NightRain.Set(1f); indoors leave it off (or 0.3 for a leaky roof). NightRain.Set(0) stops it.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [AddComponentMenu("")]
    public sealed class NightRain : MonoBehaviour
    {
        static NightRain _instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        ParticleSystem _ps;
        float _intensity;

        /// <summary>Current rain intensity 0..1 (0 = off).</summary>
        public static float Intensity => _instance != null ? _instance._intensity : 0f;

        /// <summary>Starts/stops the rain (0..1). Scene-scoped: a scene load removes it.</summary>
        public static void Set(float intensity01)
        {
            if (!Application.isPlaying) return;
            intensity01 = Mathf.Clamp01(intensity01);
            if (_instance == null)
            {
                if (intensity01 <= 0f) return;
                var go = new GameObject("[NightRain]");
                _instance = go.AddComponent<NightRain>();
                _instance.Build();
            }
            _instance._intensity = intensity01;
            var em = _instance._ps.emission;
            em.rateOverTime = 650f * intensity01;
            if (intensity01 > 0f && !_instance._ps.isPlaying) _instance._ps.Play();
            if (intensity01 <= 0f) _instance._ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void Build()
        {
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.7f, 0.82f, 1f, 0.35f);
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = _ps.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-1.2f); vel.y = new ParticleSystem.MinMaxCurve(-16f); vel.z = new ParticleSystem.MinMaxCurve(-0.6f);
            var shape = _ps.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(26f, 0.5f, 26f);
            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f; r.lengthScale = 1f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var mat = new Material(MaterialLibrary.ParticleShader) { name = "fx_rain" };
            if (mat.HasProperty(ShaderIds.SrcBlend)) { mat.SetFloat(ShaderIds.SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha); mat.SetFloat(ShaderIds.DstBlend, (float)UnityEngine.Rendering.BlendMode.One); }
            if (mat.HasProperty(ShaderIds.BaseMap)) mat.SetTexture(ShaderIds.BaseMap, FxTextures.Streak);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            r.sharedMaterial = mat;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.position = cam.transform.position + Vector3.up * 9f + cam.transform.forward * 6f;
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
