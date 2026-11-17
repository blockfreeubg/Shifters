// =====================================================================================
//  SHIFTERS - HiddenPathTile.cs   [owned by the PERSPECTIVE agent]
//  An invisible-but-solid hologram platform. The BoxCollider (on this GameObject) is ALWAYS
//  solid; the hologram renderers are only visible in IQ view (BIG BRAIN). When a character with
//  the IQ perk stands on a tile, that tile and its neighbours stay faintly visible to EVERYONE
//  for FootprintDuration seconds ("footprints"), so teammates can follow the Brainiac.
//  Build with PerspectiveKit.HologramPath / PerspectiveKit.HologramTile.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class HiddenPathTile : MonoBehaviour
    {
        public static readonly List<HiddenPathTile> All = new List<HiddenPathTile>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public const float FootprintDuration = 8f;
        const float FootprintFade = 1.5f, CheckInterval = 0.1f;

        [Tooltip("Collider/visual size (m). The transform is the box center.")] public Vector3 Size = new Vector3(1.6f, 0.25f, 1.6f);
        [Tooltip("Tiles that light up together with this one (path neighbours).")] public HiddenPathTile[] Neighbours = new HiddenPathTile[0];
        [Tooltip("Hologram renderers (hidden unless IQ view or footprinted).")] public Renderer[] Visuals = new Renderer[0];
        [Tooltip("Visibility of footprinted tiles for non-IQ viewers (0..1).")] public float FootprintVisibility = 0.4f;
        public Revealable Reveal;

        /// <summary>Time.time until which this tile is footprinted.</summary>
        public float FootprintUntil { get; private set; } = -1f;
        public bool IsFootprinted => Time.time < FootprintUntil;
        /// <summary>Animated 0..1 visibility (0 = renderers disabled).</summary>
        public float Visibility { get; private set; }
        /// <summary>True when any hologram renderer is currently drawn.</summary>
        public bool IsVisible { get; private set; } = true;

        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static MaterialPropertyBlock _mpb;
        Color[] _baseColors = new Color[0];
        Color[] _baseEmission = new Color[0];
        float[] _baseAlpha = new float[0];
        float _check;
        bool _started;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            if (Reveal == null) Reveal = GetComponent<Revealable>();
            _baseColors = new Color[Visuals.Length];
            _baseAlpha = new float[Visuals.Length];
            _baseEmission = new Color[Visuals.Length];
            for (int i = 0; i < Visuals.Length; i++)
            {
                var m = Visuals[i] != null ? Visuals[i].sharedMaterial : null;
                _baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : new Color(0.69f, 0.49f, 1f, 0.5f);
                _baseAlpha[i] = m != null && m.HasProperty(AlphaId) ? m.GetFloat(AlphaId) : 1f;
                _baseEmission[i] = m != null && m.HasProperty(EmissionId) && m.IsKeywordEnabled("_EMISSION") ? m.GetColor(EmissionId) : new Color(0f, 0f, 0f, -1f);
            }
            _check = Random.value * CheckInterval;
            _started = true;
            Visibility = Target();
            Apply(true);
        }

        /// <summary>Mark this tile (and neighbours) as stepped on by a BIG BRAIN character.</summary>
        public void Footprint(bool includeNeighbours = true)
        {
            FootprintUntil = Time.time + FootprintDuration;
            if (!includeNeighbours) return;
            for (int i = 0; i < Neighbours.Length; i++) if (Neighbours[i] != null) Neighbours[i].Footprint(false);
        }

        /// <summary>True if the character's feet rest on top of this tile.</summary>
        public bool IsStandingOn(ShifterCharacter c)
        {
            if (c == null) return false;
            var p = transform.InverseTransformPoint(c.Feet);
            float hx = Size.x * 0.5f + 0.15f, hz = Size.z * 0.5f + 0.15f, top = Size.y * 0.5f;
            return Mathf.Abs(p.x) <= hx && Mathf.Abs(p.z) <= hz && p.y >= top - 0.35f && p.y <= top + 0.6f;
        }

        float Target()
        {
            if (Reveal != null && Reveal.IsRevealed) return 1f;
            float left = FootprintUntil - Time.time;
            if (left <= 0f) return 0f;
            return FootprintVisibility * Mathf.Clamp01(left / FootprintFade);
        }

        void Update()
        {
            if (!_started) return;
            _check -= Time.deltaTime;
            if (_check <= 0f)
            {
                _check = CheckInterval;
                var all = ShifterCharacter.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c != null && c.IsAlive && c.Profile != null && c.Profile.HasIQPerk && IsStandingOn(c)) { Footprint(true); break; }
                }
            }
            float target = Target();
            if (Mathf.Approximately(target, Visibility) && IsVisible == (Visibility > 0.005f)) return;
            Visibility = Mathf.MoveTowards(Visibility, target, PerspectiveStyle.UiDelta * 4f);
            Apply(false);
        }

        void Apply(bool force)
        {
            bool on = Visibility > 0.005f;
            if (force || on != IsVisible)
            {
                IsVisible = on;
                for (int i = 0; i < Visuals.Length; i++) if (Visuals[i] != null) Visuals[i].enabled = on;
            }
            if (!on) return;
            _mpb ??= new MaterialPropertyBlock();
            // revealed = full hologram; footprints = dim ghost (darker + more transparent so opaque fallbacks still read as faint)
            float v = Visibility;
            for (int i = 0; i < Visuals.Length; i++)
            {
                var r = Visuals[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                var c = _baseColors[i];
                var dim = Color.Lerp(c * 0.35f, c, v);
                dim.a = c.a * v;
                _mpb.SetColor(BaseColorId, dim);
                _mpb.SetFloat(AlphaId, _baseAlpha[i] * v);
                if (_baseEmission[i].a >= 0f) _mpb.SetColor(EmissionId, _baseEmission[i] * Mathf.Lerp(0.25f, 1f, v));
                r.SetPropertyBlock(_mpb);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.69f, 0.49f, 1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Size);
        }
    }
}
