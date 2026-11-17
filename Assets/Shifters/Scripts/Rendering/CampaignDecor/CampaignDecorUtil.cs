// =====================================================================================
//  SHIFTERS - CampaignDecorUtil.cs   (RENDERING_DECOR)
//  Shared helpers for the campaign decor kits (EnvKit.Decor.Bank / Night / Tower). Internal.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    internal static class CampaignDecorUtil
    {
        public static Color Hx(int rgb) => UIColors.Hex(rgb);
        public static Quaternion R(Quaternion? r) => r ?? Quaternion.identity;
        public static Material White => Kit.Mat(MatKey.White);

        /// <summary>Prop root. mounted = the piece has a readable/front side: 'rot' is the VIEWER'S LOOK (EnvKit convention),
        /// the root is built in face space (root.forward = local +Z = outward normal of the front, towards the viewer).</summary>
        public static GameObject Root(string name, Transform parent, Vector3 pos, Quaternion? rot, bool mounted)
            => Kit.Make(name, parent, pos, mounted ? EnvKit.ViewToFace(R(rot)) : R(rot));

        /// <summary>Cached merged mesh rendered on a child (vertex colors x material).</summary>
        public static MeshRenderer Part(Transform t, string name, string key, System.Action<EnvParts> build, Material m, bool shadows = true, Vector3? localPos = null)
            => EnvMeshes.Render(t, name, EnvMeshes.Cached(key, build), m, localPos ?? Vector3.zero, null, shadows);

        public static BoxCollider BoxCol(GameObject go, Vector3 center, Vector3 size)
        {
            var b = go.AddComponent<BoxCollider>(); b.center = center; b.size = size; return b;
        }

        /// <summary>Opaque flat-color material with emission (cached by MaterialLibrary).</summary>
        public static Material Glow(int rgb, float emission = 1.6f, float smooth = 0.4f) => MaterialLibrary.Colored(Hx(rgb), smooth, emission);

        /// <summary>Grey-immune flat emissive color (rendering's WorldColor grade skips it): stored color, warm bulbs.</summary>
        public static Material ImmuneGlow(Color c, float emission = 1.6f, float smooth = 0.4f) => MaterialLibrary.ImmuneColored(c, smooth, emission);

        /// <summary>Sagging rope/chain between a and b (rod segments along a parabola).</summary>
        public static void Sag(EnvParts p, Vector3 a, Vector3 b, float sag, float radius, Color c, int segs = 8, int sides = 6)
        {
            Vector3 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                var q = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                p.Rod(prev, q, radius, c, sides);
                prev = q;
            }
        }

        /// <summary>Chain of alternating torus links from a to b (straight).</summary>
        public static void LinkChain(EnvParts p, Vector3 a, Vector3 b, float link, Color c)
        {
            var d = b - a; float len = d.magnitude; if (len < 1e-3f) return;
            var dir = d / len; var baseRot = Quaternion.FromToRotation(Vector3.up, dir);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / (link * 1.5f)));
            for (int i = 0; i < n; i++)
            {
                var center = a + dir * ((i + 0.5f) * len / n);
                var rot = baseRot * Quaternion.Euler(0f, (i % 2) * 90f, 0f) * Quaternion.Euler(0f, 0f, 90f);
                p.Torus(center, link * 0.5f, link * 0.12f, c, rot, 8, 4);
            }
        }

        /// <summary>Text in face space (readable from the parent's +Z side).</summary>
        public static TextMeshPro Text(Transform parent, string name, string text, Vector3 localPos, Vector2 area, Color color, float maxHeight, bool display = true)
        {
            var t = EnvKit.AddText(parent, text ?? "", localPos, area, color, display ? ShiftersFonts.Display : ShiftersFonts.Label, maxHeight);
            t.gameObject.name = name;
            return t;
        }

        /// <summary>Stable small hash of a position (default text / variant picks).</summary>
        public static int PosHash(Vector3 p) => Mathf.Abs(Mathf.RoundToInt(p.x * 7f + p.z * 13f + p.y * 3f));

        public static Light DecorPoint(Transform parent, string name, Vector3 localPos, Color c, float range, float intensity)
        {
            var go = Kit.Make(name, parent, localPos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            DecorLight.Mark(l);
            return l;
        }
    }
}
