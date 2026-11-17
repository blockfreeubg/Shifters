// =====================================================================================
//  SHIFTERS - SpeedRoute.cs   [owned by the PERSPECTIVE agent]
//  TURBO VIEW floor chevrons: glowing arrows along a path, only visible to SPD-perk viewers
//  (Revealable Speed + ShowOnlyWhenRevealed on the same GameObject), with a scrolling glow wave
//  running in the travel direction. Build with PerspectiveKit.SpeedRoute.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class SpeedRoute : MonoBehaviour
    {
        public Renderer[] Chevrons = new Renderer[0];
        [Tooltip("Glow wave speed (chevrons per second).")] public float ScrollSpeed = 7f;
        public Color GlowColor = new Color(0.239f, 0.878f, 1f);
        public float Length;
        public Revealable Reveal;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock _mpb;

        void Start() { if (Reveal == null) Reveal = GetComponent<Revealable>(); }

        void Update()
        {
            if (Reveal == null || !Reveal.IsRevealed || Chevrons.Length == 0) return;
            _mpb ??= new MaterialPropertyBlock();
            float head = Time.time * ScrollSpeed;
            int n = Chevrons.Length;
            float period = Mathf.Max(8f, n * 0.6f);
            for (int i = 0; i < n; i++)
            {
                var r = Chevrons[i];
                if (r == null) continue;
                float phase = Mathf.Repeat(head - i, period) / period; // 0 = wave front on this chevron
                float wave = Mathf.Pow(1f - phase, 6f);
                float k = 0.45f + 2.6f * wave;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, GlowColor * k);
                _mpb.SetColor(BaseColorId, Color.Lerp(GlowColor * 0.6f, Color.white, wave * 0.6f));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
