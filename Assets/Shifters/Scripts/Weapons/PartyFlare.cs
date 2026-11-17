// =====================================================================================
//  SHIFTERS - PartyFlare.cs   [owned by the WEAPONS agent]
//  PARTY POPPER landing: confetti, noise 14 m (priority 2: lures bots and dummies), a warm-pink
//  light pool r 3 m for 8 s with ONE unshadowed point light, registered as an ILightSource
//  (puzzles' LightRegistry). Pooled: exactly one flare object; a new landing moves it.
//  NoToolZone(PartyPopper) at the landing point: the flare fizzles ("HR SAYS NO.").
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class PartyFlare : MonoBehaviour, ILightSource
    {
        public const float Seconds = 8f, NoiseRadius = 14f, NoisePriority = 2f;
        public static readonly Color FlareColor = new Color(1f, 0.62f, 0.8f);

        /// <summary>The (single, pooled) flare; null before the first landing.</summary>
        public static PartyFlare Current { get; private set; }
        /// <summary>Raised when a flare lands and lights up (levels / lighting rules may listen).</summary>
        public static event System.Action<PartyFlare> Landed;
        public static int LandCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Landed = null; LandCount = 0; }

        public Vector3 LightPosition => transform.position + Vector3.up * 0.3f;
        public float LightRadius => Tuning.LightPoolRadius;
        public bool LightOn => isActiveAndEnabled && Time.time < _until;
        public float Remaining => Mathf.Max(0f, _until - Time.time);
        public Light PointLight { get; private set; }
        public ShifterCharacter Shooter { get; private set; }

        float _until, _sparkle;
        Transform _ring;

        /// <summary>Land a flare at point (called by the PARTY POPPER projectile). Returns the flare or null if it fizzled.</summary>
        public static PartyFlare Land(Vector3 point, Vector3 normal, ShifterCharacter shooter)
        {
            if (ToolZones.Blocks(point, ToolMask.PartyPopper))
            {
                WeaponTools.Refuse(shooter != null ? shooter.Weapon : null, point, ToolZones.Refusal, true);
                return null;
            }
            var f = Current;
            if (f == null) f = Create();
            Vector3 up = normal.y > 0.5f ? Vector3.up : normal.normalized;
            f.transform.SetPositionAndRotation(point + up * 0.05f, Quaternion.identity);
            f.gameObject.SetActive(true);
            f.Shooter = shooter;
            f._until = Time.time + Seconds;
            f._sparkle = 0f;
            if (f.PointLight != null) { f.PointLight.enabled = true; f.PointLight.intensity = 2.6f; }
            Fx.Confetti(point + Vector3.up * 0.3f);
            Fx.Sparks(point, up, 16, FlareColor);
            Sfx.At(SfxId.Popper, point, 1f);
            GameEvents.RaiseNoise(point, NoiseRadius, shooter, NoisePriority);
            LandCount++;
            RegisterLight(f);
            try { Landed?.Invoke(f); } catch (System.Exception e) { Debug.LogException(e); }
            return f;
        }

        /// <summary>Registers the flare with puzzles' LightRegistry (idempotent; LightOn goes false when it expires).</summary>
        static void RegisterLight(PartyFlare f) { LightRegistry.Register(f); }

        void OnDestroy() { LightRegistry.Unregister(this); if (Current == this) Current = null; }

        static PartyFlare Create()
        {
            var go = new GameObject("PartyFlare");
            var f = go.AddComponent<PartyFlare>();
            Current = f;
            var stick = ProcMesh.Cached("flare_stick", () => ProcMesh.Cylinder(0.03f, 0.3f, 6));
            ProcMesh.Part("Stick", go.transform, stick, MaterialLibrary.Colored(UIColors.Hex(0xFFD23C), 0.3f), new Vector3(0f, 0.15f, 0f), Quaternion.Euler(12f, 0f, 8f), null, false);
            var ball = ProcMesh.Cached("flare_glow", () => ProcMesh.Sphere(0.09f, 8, 6, true));
            ProcMesh.Part("Glow", go.transform, ball, MaterialLibrary.Unlit(FlareColor, 2.5f), new Vector3(0.02f, 0.32f, 0.03f), null, null, false);
            var ringMesh = ProcMesh.Cached("flare_pool_ring", () => ProcMesh.Torus(1f, 0.03f, 32, 4));
            var ring = ProcMesh.Part("PoolRing", go.transform, ringMesh, MaterialLibrary.Unlit(FlareColor, 1.4f), new Vector3(0f, 0.03f, 0f), null,
                new Vector3(Tuning.LightPoolRadius, 1f, Tuning.LightPoolRadius), false);
            f._ring = ring.transform;
            var lgo = new GameObject("FlareLight");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = FlareColor;
            l.range = Tuning.LightPoolRadius * 1.9f;
            l.intensity = 2.6f;
            l.shadows = LightShadows.None; // never shadowed point lights (WebGL budget)
            f.PointLight = l;
            return f;
        }

        void Update()
        {
            float left = _until - Time.time;
            if (left <= 0f)
            {
                if (PointLight != null) PointLight.enabled = false;
                gameObject.SetActive(false);
                return;
            }
            if (PointLight != null) PointLight.intensity = 2.6f * Mathf.Clamp01(left) * (0.92f + 0.08f * Mathf.Sin(Time.time * 17f));
            if (_ring != null) { float s = Tuning.LightPoolRadius * (0.97f + 0.03f * Mathf.Sin(Time.time * 4f)); _ring.localScale = new Vector3(s, 1f, s); }
            _sparkle -= Time.deltaTime;
            if (_sparkle <= 0f) { _sparkle = 0.6f; Fx.Sparks(transform.position + Vector3.up * 0.35f, Vector3.up, 4, FlareColor); }
        }

        /// <summary>Put the flare out early (level reset / tests).</summary>
        public void Extinguish() { _until = 0f; if (PointLight != null) PointLight.enabled = false; gameObject.SetActive(false); }
    }
}
