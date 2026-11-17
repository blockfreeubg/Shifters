// =====================================================================================
//  SHIFTERS - EnvMeshes.cs   (RENDERING)
//  Helpers on top of ProcMesh.Builder for the environment kit: a fluent multi-part, vertex-colored
//  mesh builder ("EnvParts"), parameter keys for ProcMesh.Cached, and extra primitives (arrow, gear).
//  Vertex colors are authored in sRGB like material colors (Shifters shaders convert).
// =====================================================================================
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Shifters
{
    /// <summary>Fluent wrapper merging many colored primitives into one mesh (one draw call).</summary>
    public sealed class EnvParts
    {
        public readonly ProcMesh.Builder B = new ProcMesh.Builder();
        static Quaternion R(Quaternion? r) => r ?? Quaternion.identity;

        public EnvParts Box(Vector3 center, Vector3 size, Color c, float bevel = 0.02f, Quaternion? rot = null) { B.AddBox(center, size, R(rot), c, bevel); return this; }
        public EnvParts Cyl(Vector3 center, float radius, float height, Color c, int seg = 12, Quaternion? rot = null, bool smooth = true) { B.AddCylinder(center, radius, height, R(rot), c, seg, smooth); return this; }
        public EnvParts Cone(Vector3 baseCenter, float radius, float height, Color c, int seg = 12, Quaternion? rot = null) { B.AddCone(baseCenter, radius, height, R(rot), c, seg); return this; }
        public EnvParts Sphere(Vector3 center, float radius, Color c, Vector3? scale = null, int seg = 12, int rings = 8, bool smooth = true, Quaternion? rot = null) { B.AddSphere(center, radius, R(rot), c, seg, rings, smooth, scale ?? Vector3.one); return this; }
        public EnvParts Torus(Vector3 center, float major, float minor, Color c, Quaternion? rot = null, int majorSeg = 16, int minorSeg = 8) { B.AddTorus(center, major, minor, R(rot), c, majorSeg, minorSeg); return this; }
        public EnvParts Wedge(Vector3 baseCenter, Vector3 size, Color c, Quaternion? rot = null) { B.AddWedge(baseCenter, size, R(rot), c); return this; }
        public EnvParts Capsule(Vector3 center, float radius, float height, Color c, Quaternion? rot = null, int seg = 12) { B.AddCapsule(center, radius, height, R(rot), c, seg, 4); return this; }

        /// <summary>Cylinder spanning two points.</summary>
        public EnvParts Rod(Vector3 a, Vector3 b, float radius, Color c, int seg = 10, bool smooth = true)
        {
            var d = b - a; float len = d.magnitude;
            if (len < 1e-4f) return this;
            B.AddCylinder((a + b) * 0.5f, radius, len, Quaternion.FromToRotation(Vector3.up, d / len), c, seg, smooth);
            return this;
        }

        /// <summary>Flat arrow (shaft + head) in the XY plane pointing +X, extruded along Z.</summary>
        public EnvParts Arrow(Vector3 center, float length, float width, float depth, Color c, Quaternion? rot = null)
        {
            var r = R(rot);
            float shaftLen = length * 0.55f;
            B.AddBox(center + r * new Vector3(-length * 0.5f + shaftLen * 0.5f, 0f, 0f), new Vector3(shaftLen, width * 0.38f, depth), r, c, 0f);
            float hx = -length * 0.5f + shaftLen;
            var f = new Vector3(0, 0, -depth * 0.5f); var k = new Vector3(0, 0, depth * 0.5f);
            Vector3 P(float x, float y, Vector3 z) => center + r * (new Vector3(x, y, 0f) + z);
            var tip = length * 0.5f; var hw = width * 0.5f;
            B.Tri(P(hx, -hw, f), P(hx, hw, f), P(tip, 0, f), c, r * Vector3.back);
            B.Tri(P(hx, -hw, k), P(hx, hw, k), P(tip, 0, k), c, r * Vector3.forward);
            B.Quad(P(hx, hw, f), P(tip, 0, f), P(tip, 0, k), P(hx, hw, k), c, r * new Vector3(hw, tip - hx, 0).normalized);
            B.Quad(P(hx, -hw, f), P(tip, 0, f), P(tip, 0, k), P(hx, -hw, k), c, r * new Vector3(hw, -(tip - hx), 0).normalized);
            B.Quad(P(hx, -hw, f), P(hx, hw, f), P(hx, hw, k), P(hx, -hw, k), c, r * Vector3.left);
            return this;
        }

        /// <summary>Gear disc in the XZ plane (axis Y): hub cylinder + rectangular teeth.</summary>
        public EnvParts Gear(Vector3 center, float radius, float thickness, int teeth, Color c, Quaternion? rot = null)
        {
            var r = R(rot);
            B.AddCylinder(center, radius * 0.86f, thickness, r, c, Mathf.Max(12, teeth * 2), false);
            B.AddCylinder(center, radius * 0.3f, thickness * 1.6f, r, Palettes.Shade(c, 0.7f), 10, false);
            for (int i = 0; i < teeth; i++)
            {
                float a = i / (float)teeth * 360f;
                var q = r * Quaternion.Euler(0f, a, 0f);
                B.AddBox(center + q * new Vector3(radius * 0.92f, 0f, 0f), new Vector3(radius * 0.22f, thickness, radius * 0.2f), q, c, 0.01f);
            }
            return this;
        }

        public Mesh ToMesh(string name) => B.ToMesh(name);
    }

    public static class EnvMeshes
    {
        /// <summary>Builds a ProcMesh.Cached key from a prefix and values (floats rounded to cm).</summary>
        public static string Key(string prefix, params object[] values)
        {
            var sb = new StringBuilder(prefix);
            foreach (var v in values)
            {
                sb.Append('_');
                switch (v)
                {
                    case float f: sb.Append(f.ToString("0.##", CultureInfo.InvariantCulture)); break;
                    case Vector3 p: sb.Append(p.x.ToString("0.##", CultureInfo.InvariantCulture)).Append('x').Append(p.y.ToString("0.##", CultureInfo.InvariantCulture)).Append('x').Append(p.z.ToString("0.##", CultureInfo.InvariantCulture)); break;
                    case Vector2 q: sb.Append(q.x.ToString("0.##", CultureInfo.InvariantCulture)).Append('x').Append(q.y.ToString("0.##", CultureInfo.InvariantCulture)); break;
                    case Color c: sb.Append(ColorUtility.ToHtmlStringRGB(c)); break;
                    default: sb.Append(v); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Stable 32-bit hash for long parameter lists (pipes, scatter layouts).</summary>
        public static string Hash(params Vector3[] pts)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var p in pts)
                {
                    h = (h ^ (uint)Mathf.RoundToInt(p.x * 100f)) * 16777619;
                    h = (h ^ (uint)Mathf.RoundToInt(p.y * 100f)) * 16777619;
                    h = (h ^ (uint)Mathf.RoundToInt(p.z * 100f)) * 16777619;
                }
                return h.ToString("x8");
            }
        }

        /// <summary>Cached merged mesh built by 'build' (key must encode every parameter, theme included).</summary>
        public static Mesh Cached(string key, System.Action<EnvParts> build)
            => ProcMesh.Cached(key, () => { var p = new EnvParts(); build(p); return p.ToMesh(key); });

        /// <summary>Child renderer for a mesh (no collider).</summary>
        public static MeshRenderer Render(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, Quaternion? localRot = null, bool castShadows = true)
        {
            var go = Kit.Make(name, parent, localPos, localRot ?? Quaternion.identity);
            return ProcMesh.AddRenderer(go, mesh, mat, castShadows);
        }

        /// <summary>Theme palette color for vertex-colored meshes.</summary>
        public static Color C(MatKey key) => Palettes.Color(key, Kit.Theme);
    }
}
