// =====================================================================================
//  SHIFTERS - VisualMeshes.cs   (VISUALS module)
//  Extra low-poly primitives for characters, weapons and bots, built on top of ProcMesh and
//  always cached through ProcMesh.Cached with keys that encode every parameter.
//  * Lathe: smooth surface of revolution with superellipse ("squircle") cross-sections -
//    torsos, heads, domes, hats, coats.
//  * Stroke / Filled: flat 2D shapes extruded along Z and bent onto a curved face - mouths,
//    eyebrows, X-eyes, spirals.
// =====================================================================================
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Shifters
{
    public static class VisualMeshes
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string F(float v) => v.ToString("0.###", Inv);
        public static string V(Vector3 v) => F(v.x) + "x" + F(v.y) + "x" + F(v.z);

        // ------------------------------------------------------------------ basic cached primitives
        public static Mesh Sphere(float r, int seg = 14, int rings = 10)
            => ProcMesh.Cached("v_sph_" + F(r) + "_" + seg + "_" + rings, () => ProcMesh.Sphere(r, seg, rings, true));

        public static Mesh Ellipsoid(Vector3 radii, int seg = 14, int rings = 10)
            => ProcMesh.Cached("v_ell_" + V(radii) + "_" + seg + "_" + rings,
                () => new ProcMesh.Builder().AddSphere(Vector3.zero, 1f, Quaternion.identity, Color.white, seg, rings, true, radii).ToMesh("Ellipsoid"));

        /// <summary>Limb capsule hanging DOWN from its pivot: joint at y=0, other joint at y=-len (caps overlap the joints).</summary>
        public static Mesh Limb(float r, float len, int seg = 10)
            => ProcMesh.Cached("v_limb_" + F(r) + "_" + F(len) + "_" + seg,
                () => new ProcMesh.Builder().AddCapsule(new Vector3(0f, -len * 0.5f, 0f), r, len + 2f * r, Quaternion.identity, Color.white, seg, 3).ToMesh("Limb"));

        /// <summary>Tapered limb hanging down from the pivot (radius r0 at the joint, r1 at the far end).</summary>
        public static Mesh TaperLimb(float r0, float r1, float len, int seg = 10)
            => ProcMesh.Cached("v_tlimb_" + F(r0) + "_" + F(r1) + "_" + F(len),
                () =>
                {
                    var rings = new List<Vector4>();
                    // y, rx, rz, zoff  (top pole -> bottom pole), rounded ends
                    rings.Add(new Vector4(r0 * 0.9f, 0f, 0f, 0f));
                    rings.Add(new Vector4(r0 * 0.6f, r0 * 0.75f, r0 * 0.75f, 0f));
                    rings.Add(new Vector4(0f, r0, r0, 0f));
                    rings.Add(new Vector4(-len * 0.55f, Mathf.Lerp(r0, r1, 0.7f), Mathf.Lerp(r0, r1, 0.7f), 0f));
                    rings.Add(new Vector4(-len, r1, r1, 0f));
                    rings.Add(new Vector4(-len - r1 * 0.6f, r1 * 0.75f, r1 * 0.75f, 0f));
                    rings.Add(new Vector4(-len - r1 * 0.9f, 0f, 0f, 0f));
                    rings.Reverse();
                    return BuildLathe(rings, 2f, seg, "TaperLimb");
                });

        /// <summary>Chamfered box (chunky toy look).</summary>
        public static Mesh Block(Vector3 size, float bevel) => ProcMesh.CachedBox(size, bevel);

        // ------------------------------------------------------------------ lathe
        /// <summary>Smooth surface of revolution around Y. rings = (y, radiusX, radiusZ, zOffset), bottom to top;
        /// radius 0 rings close the shape. n = superellipse exponent (2 = ellipse, 3-4 = squircle).</summary>
        public static Mesh Lathe(string key, IList<Vector4> rings, float n = 2f, int seg = 16)
            => ProcMesh.Cached("v_lathe_" + key, () => BuildLathe(rings, n, seg, key));

        public static Mesh BuildLathe(IList<Vector4> rings, float n, int seg, string name)
        {
            var verts = new List<Vector3>(rings.Count * seg);
            var tris = new List<int>(rings.Count * seg * 6);
            float e = 2f / Mathf.Max(0.5f, n);
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                for (int s = 0; s < seg; s++)
                {
                    float th = s / (float)seg * Mathf.PI * 2f;
                    float c = Mathf.Cos(th), sn = Mathf.Sin(th);
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * r.y;
                    float z = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e) * r.z + r.w;
                    verts.Add(new Vector3(x, r.x, z));
                }
            }
            for (int i = 0; i < rings.Count - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a = i * seg + s, b = i * seg + (s + 1) % seg, c = (i + 1) * seg + s, d = (i + 1) * seg + (s + 1) % seg;
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }
            return Finish(name, verts, tris);
        }

        static Mesh Finish(string name, List<Vector3> verts, List<int> tris)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            var cols = new Color[verts.Count];
            var uvs = new Vector2[verts.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            m.colors = cols;
            m.uv = uvs;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Dome (upper half of an ellipsoid-ish lathe) with a closed flat bottom at y=0.</summary>
        public static Mesh Dome(float rx, float h, float rz, float n = 2f, int seg = 16)
        {
            string key = "dome_" + F(rx) + "_" + F(h) + "_" + F(rz) + "_" + F(n);
            return Lathe(key, new[]
            {
                new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, rx, rz, 0f), new Vector4(h * 0.35f, rx * 0.96f, rz * 0.96f, 0f),
                new Vector4(h * 0.65f, rx * 0.8f, rz * 0.8f, 0f), new Vector4(h * 0.88f, rx * 0.5f, rz * 0.5f, 0f),
                new Vector4(h, 0f, 0f, 0f)
            }, n, seg);
        }

        // ------------------------------------------------------------------ 2D shapes (face features)
        /// <summary>Thick polyline in the XY plane facing +Z, bent back along a sphere of radius curv (0 = flat).</summary>
        public static void AddStroke(ProcMesh.Builder b, IList<Vector2> pts, float thick, float depth, float curv, Vector3 offset)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var p0 = pts[i]; var p1 = pts[i + 1];
                var d = p1 - p0; float len = d.magnitude; if (len < 1e-5f) continue;
                var mid = (p0 + p1) * 0.5f;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                var center = new Vector3(mid.x, mid.y, -Bend(mid.x, mid.y, curv)) + offset;
                b.AddBox(center, new Vector3(len + thick * 0.7f, thick, depth), Quaternion.Euler(0f, 0f, ang), Color.white, Mathf.Min(thick, depth) * 0.3f);
            }
        }

        /// <summary>Filled convex polygon (XY, facing +Z) extruded by depth and bent along curv.</summary>
        public static void AddFilled(ProcMesh.Builder b, IList<Vector2> outline, float depth, float curv, Vector3 offset)
        {
            var c = Vector2.zero; for (int i = 0; i < outline.Count; i++) c += outline[i]; c /= outline.Count;
            Vector3 Front(Vector2 p) => new Vector3(p.x, p.y, depth * 0.5f - Bend(p.x, p.y, curv)) + offset;
            Vector3 Back(Vector2 p) => new Vector3(p.x, p.y, -depth * 0.5f - Bend(p.x, p.y, curv)) + offset;
            for (int i = 0; i < outline.Count; i++)
            {
                var p0 = outline[i]; var p1 = outline[(i + 1) % outline.Count];
                b.Tri(Front(c), Front(p0), Front(p1), Color.white, Vector3.forward);
                var outward = new Vector3((p0 + p1).x * 0.5f - c.x, (p0 + p1).y * 0.5f - c.y, 0f);
                b.Quad(Front(p0), Front(p1), Back(p1), Back(p0), Color.white, outward);
            }
        }

        static float Bend(float x, float y, float curv)
        {
            if (curv <= 0.0001f) return 0f;
            float d2 = Mathf.Min(x * x + y * y, curv * curv * 0.95f);
            return curv - Mathf.Sqrt(curv * curv - d2);
        }

        static readonly List<Vector2> _pts = new List<Vector2>(64);

        /// <summary>Arc polyline: circle radius rad centered at (0, cy), from angle a0 to a1 (degrees, 0 = +X, 90 = up).</summary>
        public static List<Vector2> Arc(float rad, float cy, float a0, float a1, int steps, float sx = 1f)
        {
            _pts.Clear();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                _pts.Add(new Vector2(Mathf.Cos(a) * rad * sx, cy + Mathf.Sin(a) * rad));
            }
            return _pts;
        }

        public static List<Vector2> Ellipse(float rx, float ry, float cy, int steps, float a0 = 0f, float a1 = 360f, bool closeFlat = false)
        {
            _pts.Clear();
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)(closeFlat ? steps - 1 : steps)) * Mathf.Deg2Rad;
                _pts.Add(new Vector2(Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
            return _pts;
        }
    }
}
