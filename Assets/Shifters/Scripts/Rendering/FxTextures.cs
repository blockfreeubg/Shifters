// =====================================================================================
//  SHIFTERS - FxTextures.cs   (RENDERING)
//  Procedural particle textures generated once at runtime (soft circle, cartoon puff, star, square,
//  streak, glow, ring, vertical gradient). Never saved (HideFlags.DontSave).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class FxTextures
    {
        static Texture2D _soft, _puff, _star, _square, _streak, _glow, _ring, _gradient;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _soft = _puff = _star = _square = _streak = _glow = _ring = _gradient = null; }

        public static Texture2D SoftCircle => _soft != null ? _soft : (_soft = Make("fx_soft", 64, 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);
            return new Color(1, 1, 1, a);
        }));

        /// <summary>Cartoon smoke ball: solid disc with a crisp edge and a soft top-left highlight.</summary>
        public static Texture2D Puff => _puff != null ? _puff : (_puff = Make("fx_puff", 64, 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01((0.92f - r) / 0.08f);
            float shade = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(0.55f + (v - u) * 0.45f));
            return new Color(shade, shade, shade, a);
        }));

        public static Texture2D Star => _star != null ? _star : (_star = Make("fx_star", 64, 64, (u, v) =>
        {
            float ang = Mathf.Atan2(v, u);
            float r = Mathf.Sqrt(u * u + v * v);
            float k = Mathf.Cos(ang * 5f) * 0.5f + 0.5f;            // 5 points
            float edge = Mathf.Lerp(0.42f, 0.95f, Mathf.Pow(k, 2.2f));
            float a = Mathf.Clamp01((edge - r) / 0.06f);
            float glow = Mathf.Clamp01(1f - r) * 0.35f;
            return new Color(1, 1, 1, Mathf.Max(a, glow));
        }));

        public static Texture2D Square => _square != null ? _square : (_square = Make("fx_square", 16, 16, (u, v) =>
        {
            float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
            return new Color(1, 1, 1, Mathf.Clamp01((0.98f - d) / 0.12f));
        }));

        /// <summary>Horizontal soft line (u along, v across) for tracers and speed lines.</summary>
        public static Texture2D Streak => _streak != null ? _streak : (_streak = Make("fx_streak", 64, 16, (u, v) =>
        {
            float across = Mathf.Clamp01(1f - Mathf.Abs(v)); across = across * across;
            float along = Mathf.Clamp01((1f - Mathf.Abs(u)) * 3f);
            return new Color(1, 1, 1, across * along);
        }));

        public static Texture2D Glow => _glow != null ? _glow : (_glow = Make("fx_glow", 64, 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Exp(-r * r * 5f) * Mathf.Clamp01((1f - r) * 4f);
            return new Color(1, 1, 1, a);
        }));

        public static Texture2D Ring => _ring != null ? _ring : (_ring = Make("fx_ring", 128, 128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float d = Mathf.Abs(r - 0.86f);
            float a = Mathf.Clamp01(1f - d / 0.1f); a *= a;
            return new Color(1, 1, 1, a);
        }));

        /// <summary>Vertical gradient: opaque at the bottom (v=0) fading to the top (scan pulse walls).</summary>
        public static Texture2D Gradient => _gradient != null ? _gradient : (_gradient = Make("fx_gradient", 4, 64, (u, v) =>
        {
            float t = (v + 1f) * 0.5f;
            float a = Mathf.Pow(1f - t, 1.6f) * Mathf.Clamp01(t * 12f + 0.3f);
            return new Color(1, 1, 1, a);
        }));

        static Texture2D Make(string name, int w, int h, System.Func<float, float, Color> f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    px[y * w + x] = f(u, v);
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
