// SHIFTERS - rendering test helpers (screenshot pixel checks, camera placement). File access only in the editor
// (DevPlay); player/WebGL builds compile it to no-ops.
using UnityEngine;

namespace Shifters
{
    public static class RenderTestUtil
    {
        /// <summary>Fraction of pure-magenta pixels (missing/error shader) in a PNG. -1 if unreadable.</summary>
        public static float MagentaFraction(string pngPath)
        {
#if !UNITY_EDITOR
            return -1f;
#else
            if (string.IsNullOrEmpty(pngPath) || !System.IO.File.Exists(pngPath)) return -1f;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(pngPath))) { Object.Destroy(tex); return -1f; }
            var px = tex.GetPixels32();
            int bad = 0;
            for (int i = 0; i < px.Length; i++) { var p = px[i]; if (p.r > 225 && p.g < 40 && p.b > 225) bad++; }
            Object.Destroy(tex);
            return bad / (float)Mathf.Max(1, px.Length);
#endif
        }

        /// <summary>Average luminance (0..1) and saturation-ish spread of a PNG, to catch black/blown-out frames.</summary>
        public static float MeanLuma(string pngPath)
        {
#if !UNITY_EDITOR
            return -1f;
#else
            if (string.IsNullOrEmpty(pngPath) || !System.IO.File.Exists(pngPath)) return -1f;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(pngPath))) { Object.Destroy(tex); return -1f; }
            var px = tex.GetPixels32();
            double sum = 0;
            for (int i = 0; i < px.Length; i += 7) { var p = px[i]; sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0; }
            Object.Destroy(tex);
            return (float)(sum / Mathf.Max(1, px.Length / 7));
#endif
        }

        /// <summary>Mean HSV saturation (0..1) of a PNG (every 7th pixel). -1 if unreadable.</summary>
        public static float MeanSaturation(string pngPath) => RegionSaturation(pngPath, new Rect(0f, 0f, 1f, 1f));

        /// <summary>Mean HSV saturation inside a viewport-space rect (0..1, origin bottom-left). -1 if unreadable.</summary>
        public static float RegionSaturation(string pngPath, Rect vp)
        {
#if !UNITY_EDITOR
            return -1f;
#else
            if (string.IsNullOrEmpty(pngPath) || !System.IO.File.Exists(pngPath)) return -1f;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(pngPath))) { Object.Destroy(tex); return -1f; }
            int w = tex.width, h = tex.height;
            int x0 = Mathf.Clamp((int)(vp.xMin * w), 0, w - 1), x1 = Mathf.Clamp((int)(vp.xMax * w), x0 + 1, w);
            int y0 = Mathf.Clamp((int)(vp.yMin * h), 0, h - 1), y1 = Mathf.Clamp((int)(vp.yMax * h), y0 + 1, h);
            var px = tex.GetPixels32();
            double sum = 0; int n = 0;
            int step = (x1 - x0) * (y1 - y0) > 40000 ? 3 : 1;
            for (int y = y0; y < y1; y += step)
                for (int x = x0; x < x1; x += step)
                {
                    var p = px[y * w + x];
                    int mx = Mathf.Max(p.r, Mathf.Max(p.g, p.b)), mn = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                    sum += mx > 0 ? (mx - mn) / (double)mx : 0.0; n++;
                }
            Object.Destroy(tex);
            return n > 0 ? (float)(sum / n) : -1f;
#endif
        }

        /// <summary>Writes a PNG with a (left) and b (right) side by side (both scaled to half width). Returns the path.</summary>
        public static string SideBySide(string a, string b, string outPath)
        {
#if !UNITY_EDITOR
            return null;
#else
            if (!System.IO.File.Exists(a) || !System.IO.File.Exists(b)) return null;
            var ta = new Texture2D(2, 2, TextureFormat.RGB24, false); ta.LoadImage(System.IO.File.ReadAllBytes(a));
            var tb = new Texture2D(2, 2, TextureFormat.RGB24, false); tb.LoadImage(System.IO.File.ReadAllBytes(b));
            int w = ta.width, h = ta.height;
            var o = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pa = ta.GetPixels32(); var pb = tb.GetPixels32(); var po = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool left = x < w / 2;
                    int sx = Mathf.Clamp(left ? x + w / 4 : x - w / 4, 0, w - 1);   // centre crop of each image
                    po[y * w + x] = left ? pa[y * w + sx] : (tb.width == w && tb.height == h ? pb[y * w + sx] : new Color32(0, 0, 0, 255));
                    if (x == w / 2 || x == w / 2 - 1) po[y * w + x] = new Color32(255, 255, 255, 255);
                }
            o.SetPixels32(po); o.Apply();
            System.IO.File.WriteAllBytes(outPath, o.EncodeToPNG());
            Object.Destroy(ta); Object.Destroy(tb); Object.Destroy(o);
            return outPath;
#endif
        }

        public static void Place(Camera cam, Vector3 pos, Vector3 target, float fov = 55f)
        {
            if (cam == null) return;
            cam.transform.position = pos;
            cam.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        public static bool Check(DevPlayContext ctx, bool cond, string what) => ctx == null ? cond : ctx.Check(cond, what);
    }
}
