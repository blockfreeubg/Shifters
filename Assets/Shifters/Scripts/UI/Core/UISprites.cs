// =====================================================================================
//  SHIFTERS - UISprites.cs   (UI agent)
//  Procedural, cached UI sprites (generated once at runtime, antialiased via SDFs):
//  9-slice rounded panels & outlines, soft shadows/glows, circles, rings, hazard stripes,
//  gradients, radial lines, vignette, triangle. 1 sprite pixel = 1 canvas unit.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class UISprites
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cache.Clear();

        /// <summary>Filled rounded rectangle, 9-sliced (radius in canvas units).</summary>
        public static Sprite Rounded(int radius = 14)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            string key = "rr" + radius;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            int b = radius + 2, size = b * 2 + 2;
            float half = size * 0.5f;
            var tex = Make(size, size, false, (x, y) => Mathf.Clamp01(0.5f - RoundBox(x - half, y - half, half - 1f, half - 1f, radius)));
            return Store(key, tex, new Vector4(b, b, b, b));
        }

        /// <summary>Rounded rectangle outline (border only), 9-sliced.</summary>
        public static Sprite RoundedOutline(int radius = 14, int thickness = 3)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            string key = "ro" + radius + "_" + thickness;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            int b = radius + thickness + 2, size = b * 2 + 2;
            float half = size * 0.5f;
            var tex = Make(size, size, false, (x, y) =>
            {
                float d = RoundBox(x - half, y - half, half - 1f, half - 1f, radius);
                float ring = Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f;
                return Mathf.Clamp01(0.5f - ring);
            });
            return Store(key, tex, new Vector4(b, b, b, b));
        }

        /// <summary>Soft blurred rounded rect used for drop shadows and glows. Expand the image
        /// rect by <paramref name="blur"/> units on each side of the casting panel.</summary>
        public static Sprite Shadow(int blur = 24, int radius = 14)
        {
            string key = "sh" + blur + "_" + radius;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            int b = blur + radius + 2, size = b * 2 + 2;
            float half = size * 0.5f;
            var tex = Make(size, size, false, (x, y) =>
            {
                float d = RoundBox(x - half, y - half, half - blur, half - blur, radius);
                float t = Mathf.Clamp01(1f - (d + blur * 0.15f) / blur);
                return t * t * (3f - 2f * t);
            });
            return Store(key, tex, new Vector4(b, b, b, b));
        }

        public static Sprite Circle => Cached("circle", () => Make(128, 128, true, (x, y) => Mathf.Clamp01(0.5f - (Len(x - 64f, y - 64f) - 62f))));

        /// <summary>Ring (circle outline). thickness in pixels of a 256 px texture.</summary>
        public static Sprite Ring(int thickness = 10)
        {
            string key = "ring" + thickness;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            float r = 126f - thickness * 0.5f;
            var tex = Make(256, 256, true, (x, y) => Mathf.Clamp01(0.5f - (Mathf.Abs(Len(x - 128f, y - 128f) - r) - thickness * 0.5f)));
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Radial soft dot: opaque center fading to transparent edge.</summary>
        public static Sprite SoftCircle => Cached("soft", () => Make(128, 128, true, (x, y) =>
        {
            float t = Mathf.Clamp01(1f - Len(x - 64f, y - 64f) / 63f);
            return t * t;
        }));

        /// <summary>Tileable 45 degree hazard stripes (white on transparent). Use Image.Type.Tiled.</summary>
        public static Sprite Stripes => Cached("stripes", () =>
        {
            var t = Make(64, 64, false, (x, y) =>
            {
                float u = Mathf.Repeat(x + y, 32f);
                float e = u < 16f ? Mathf.Min(u, 16f - u) : -Mathf.Min(u - 16f, 32f - u);
                return Mathf.Clamp01(0.5f + e * 0.7071f);
            });
            t.wrapMode = TextureWrapMode.Repeat;
            return t;
        });

        /// <summary>Vertical gradient: opaque at the top, transparent at the bottom.</summary>
        public static Sprite GradientV => Cached("gradv", () => Make(4, 128, false, (x, y) => y / 127f));
        /// <summary>Horizontal gradient: opaque on the left, transparent on the right.</summary>
        public static Sprite GradientH => Cached("gradh", () => Make(128, 4, false, (x, y) => 1f - x / 127f));

        /// <summary>Sunburst of radial rays fading in/out with radius.</summary>
        public static Sprite RadialLines(int count = 36)
        {
            string key = "rays" + count;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = Make(512, 512, true, (x, y) =>
            {
                float dx = x - 256f, dy = y - 256f;
                float r = Len(dx, dy) / 256f;
                float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * count;
                float f = Mathf.Abs(Mathf.Repeat(a, 1f) - 0.5f) * 2f;   // 0 at ray center .. 1 between rays
                float ray = Mathf.Clamp01((0.3f - f) * 6f + 0.5f);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.42f, r)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.99f, r)));
                return ray * fade;
            });
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Full-screen vignette: transparent center, opaque edges.</summary>
        public static Sprite Vignette => Cached("vignette", () => Make(256, 256, false, (x, y) =>
        {
            float dx = (x - 128f) / 128f, dy = (y - 128f) / 128f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, r));
        }));

        /// <summary>Isosceles triangle pointing up.</summary>
        public static Sprite Triangle => Cached("tri", () => Make(64, 64, true, (x, y) =>
            Mathf.Clamp01(0.5f - UIShapes.Polygon(x / 64f, y / 64f, TriPts) * 64f)));

        static readonly Vector2[] TriPts = { new Vector2(0.5f, 0.97f), new Vector2(0.97f, 0.03f), new Vector2(0.03f, 0.03f) };

        // ------------------------------------------------------------------ helpers
        static Sprite Cached(string key, Func<Texture2D> build)
        {
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            return Store(key, build(), Vector4.zero);
        }

        static Sprite Store(string key, Texture2D tex, Vector4 border)
        {
            tex.name = "UISprite_" + key;
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sp.name = tex.name;
            _cache[key] = sp;
            return sp;
        }

        /// <summary>Builds a white RGBA texture with the given per-pixel alpha (pixel centers passed).</summary>
        public static Texture2D Make(int w, int h, bool mips, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(mips, false);
            return tex;
        }

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float RoundBox(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - hx + r, qy = Mathf.Abs(py) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
