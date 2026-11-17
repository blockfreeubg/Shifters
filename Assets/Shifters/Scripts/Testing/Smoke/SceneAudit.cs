// =====================================================================================
//  SHIFTERS - SceneAudit.cs   [BOTS module - smoke tests]
//  Scene health probes: missing / default / magenta materials, foreign shaders, rigidbody count,
//  "does the camera render anything", frame-time sampling. The generic sign audit (texts / signs / posters /
//  light panels that face a wall, are upside down or mirrored) lives in SceneAudit.Signs.cs.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static partial class SceneAudit
    {
        static readonly string[] AllowedShaderPrefixes =
        {
            "Shifters/", "Universal Render Pipeline/Particles", "TextMeshPro", "UI/", "Sprites/", "Hidden/", "Skybox/"
        };

        /// <summary>Renderers with no/missing/default/magenta materials (active renderers only).</summary>
        public static List<string> MaterialProblems(int max = 60)
        {
            var list = new List<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) { Add(list, max, Path(r) + ": no material"); continue; }
                foreach (var m in mats)
                {
                    if (m == null) { Add(list, max, Path(r) + ": NULL material (renders magenta)"); continue; }
                    var sh = m.shader;
                    if (sh == null || sh.name == "Hidden/InternalErrorShader" || !sh.isSupported)
                        Add(list, max, Path(r) + ": MAGENTA material '" + m.name + "' (shader " + (sh != null ? sh.name : "null") + ")");
                    else if (IsDefaultMaterial(m))
                        Add(list, max, Path(r) + ": DEFAULT material '" + m.name + "'");
                }
            }
            return list;
        }

        static void Add(List<string> l, int max, string s) { if (l.Count < max) l.Add(s); }

        public static bool IsDefaultMaterial(Material m) =>
            m.name.StartsWith("Default-") || m.name == "Default Material" || m.name == "Lit" || m.shader.name == "Standard";

        /// <summary>Shader names in use that are not Shifters/TMP/URP-particle/UI shaders (informational).</summary>
        public static Dictionary<string, int> ForeignShaders()
        {
            var d = new Dictionary<string, int>();
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null) continue;
                    string n = m.shader.name;
                    bool ok = false;
                    foreach (var p in AllowedShaderPrefixes) if (n.StartsWith(p)) { ok = true; break; }
                    if (ok) continue;
                    d.TryGetValue(n, out int c);
                    d[n] = c + 1;
                }
            }
            return d;
        }

        public static int ActiveRigidbodies()
        {
            int n = 0;
            foreach (var rb in Object.FindObjectsByType<Rigidbody>()) if (rb != null && rb.gameObject.activeInHierarchy) n++;
            return n;
        }

        /// <summary>Render the camera into a small texture: something visible = luminance range > 0.04 and not mostly magenta.</summary>
        public static bool CameraRendersSomething(Camera cam, out string info)
        {
            info = "no camera";
            if (cam == null) return false;
            const int w = 96, h = 54;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
            var px = tex.GetPixels32();
            Object.Destroy(tex);
            float lo = 1f, hi = 0f;
            int magenta = 0;
            foreach (var p in px)
            {
                float l = (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
                lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l);
                if (p.r > 230 && p.b > 230 && p.g < 40) magenta++;
            }
            float mag01 = magenta / (float)px.Length;
            info = "luma " + lo.ToString("0.00") + ".." + hi.ToString("0.00") + ", magenta " + (mag01 * 100f).ToString("0") + "%";
            return hi - lo > 0.04f && mag01 < 0.05f;
        }

        public static string Path(Component c)
        {
            if (c == null) return "(null)";
            var t = c.transform;
            string p = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
