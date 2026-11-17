// =====================================================================================
//  SHIFTERS - ProcMesh.cs   [CONTRACT FILE - shared low-poly mesh library]
//  Stylized low-poly primitives with optional bevels, flat or smooth shading and VERTEX COLORS
//  (the Shifters/StylizedLit shader multiplies _BaseColor by vertex color, so one material can
//  render multi-colored props). Every primitive is centered at the origin unless noted.
//  Use ProcMesh.Builder to merge several primitives (with transforms + colors) into one mesh.
//  Need a new primitive? Add it in your own file (e.g. EnvMeshes.cs) on top of Builder.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class ProcMesh
    {
        // ------------------------------------------------------------------ public one-shot helpers
        public static Mesh Box(Vector3 size, float bevel = 0f, Color? color = null)
            => new Builder().AddBox(Vector3.zero, size, Quaternion.identity, color ?? Color.white, bevel).ToMesh("Box");

        /// <summary>Cylinder along Y, centered at origin (y from -height/2 to +height/2).</summary>
        public static Mesh Cylinder(float radius, float height, int segments = 12, bool smooth = false, Color? color = null)
            => new Builder().AddCylinder(Vector3.zero, radius, height, Quaternion.identity, color ?? Color.white, segments, smooth).ToMesh("Cylinder");

        /// <summary>Cone along +Y, base at y=0, tip at y=height.</summary>
        public static Mesh Cone(float radius, float height, int segments = 12, Color? color = null)
            => new Builder().AddCone(Vector3.zero, radius, height, Quaternion.identity, color ?? Color.white, segments).ToMesh("Cone");

        public static Mesh Sphere(float radius, int segments = 12, int rings = 8, bool smooth = true, Color? color = null)
            => new Builder().AddSphere(Vector3.zero, radius, Quaternion.identity, color ?? Color.white, segments, rings, smooth, Vector3.one).ToMesh("Sphere");

        /// <summary>Capsule along Y, centered at origin, total height includes the caps.</summary>
        public static Mesh Capsule(float radius, float height, int segments = 12, int capRings = 4, Color? color = null)
            => new Builder().AddCapsule(Vector3.zero, radius, height, Quaternion.identity, color ?? Color.white, segments, capRings).ToMesh("Capsule");

        /// <summary>Torus in the XZ plane.</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments = 16, int minorSegments = 8, Color? color = null)
            => new Builder().AddTorus(Vector3.zero, majorRadius, minorRadius, Quaternion.identity, color ?? Color.white, majorSegments, minorSegments).ToMesh("Torus");

        /// <summary>Ramp: footprint size.x * size.z, rising from y=0 at -z to y=size.y at +z. Centered on footprint, base at y=0.</summary>
        public static Mesh Wedge(Vector3 size, Color? color = null)
            => new Builder().AddWedge(Vector3.zero, size, Quaternion.identity, color ?? Color.white).ToMesh("Wedge");

        // ------------------------------------------------------------------ GameObject helpers
        /// <summary>Adds MeshFilter + MeshRenderer to go. Returns the renderer.</summary>
        public static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Material mat, bool castShadows = true)
        {
            var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        /// <summary>Creates a child GameObject with a mesh renderer (no collider).</summary>
        public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Quaternion? localRot = null, Vector3? localScale = null, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale ?? Vector3.one;
            AddRenderer(go, mesh, mat, castShadows);
            return go;
        }

        // ------------------------------------------------------------------ cache for shared unit meshes
        static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cache.Clear();

        /// <summary>Editor-only hook installed by the scene builder: persists a newly created cached mesh as an
        /// asset (Assets/Shifters/Art/Meshes/Generated/&lt;key&gt;.asset) and returns the persisted mesh.
        /// Null at runtime (meshes stay in memory).</summary>
        public static System.Func<string, Mesh, Mesh> PersistHook;

        /// <summary>Returns a cached mesh for key, creating it with factory on first use. ALWAYS use this (with a key
        /// that encodes every parameter, e.g. "box_2x0.5x4_b0.05") for meshes placed by Kit factories, so the editor
        /// builder can save them as reusable assets instead of embedding them in scenes.</summary>
        public static Mesh Cached(string key, System.Func<Mesh> factory)
        {
            if (_cache.TryGetValue(key, out var m) && m != null) return m;
            m = factory();
            m.name = key;
            if (PersistHook != null) m = PersistHook(key, m) ?? m;
            _cache[key] = m;
            return m;
        }

        /// <summary>Cached beveled box keyed by its dimensions (rounded to cm).</summary>
        public static Mesh CachedBox(Vector3 size, float bevel = 0.04f, Color? color = null)
        {
            var c = color ?? Color.white;
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture, "box_{0:0.##}x{1:0.##}x{2:0.##}_b{3:0.###}_{4}",
                size.x, size.y, size.z, bevel, ColorUtility.ToHtmlStringRGB(c));
            return Cached(key, () => Box(size, bevel, c));
        }

        // ==================================================================== Builder
        public class Builder
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<Vector3> _n = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<Color> _c = new List<Color>();
            readonly List<int> _t = new List<int>();

            public int VertexCount => _v.Count;

            public Mesh ToMesh(string name = "ProcMesh")
            {
                var m = new Mesh { name = name };
                if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(_v);
                m.SetNormals(_n);
                m.SetUVs(0, _uv);
                m.SetColors(_c);
                m.SetTriangles(_t, 0);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }

            // ---------------------------------------------------------------- low level
            /// <summary>Flat triangle; flips winding if needed so the face points along 'outward' (when non-zero).</summary>
            public Builder Tri(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 outward = default)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return this;
                if (outward != Vector3.zero && Vector3.Dot(n, outward) < 0f) { var tmp = b; b = c; c = tmp; n = -n; }
                n.Normalize();
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _n.Add(n); _n.Add(n); _n.Add(n);
                _uv.Add(PlanarUV(a, n)); _uv.Add(PlanarUV(b, n)); _uv.Add(PlanarUV(c, n));
                _c.Add(col); _c.Add(col); _c.Add(col);
                _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
                return this;
            }

            /// <summary>Flat quad a-b-c-d (any winding; oriented by 'outward' when non-zero).</summary>
            public Builder Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 outward = default)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
                if (outward != Vector3.zero && Vector3.Dot(n, outward) < 0f) { var tmp = b; b = d; d = tmp; n = -n; }
                n.Normalize();
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
                for (int k = 0; k < 4; k++) { _n.Add(n); _c.Add(col); }
                _uv.Add(PlanarUV(a, n)); _uv.Add(PlanarUV(b, n)); _uv.Add(PlanarUV(c, n)); _uv.Add(PlanarUV(d, n));
                _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
                _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
                return this;
            }

            /// <summary>Smooth-shaded vertex (explicit normal). Returns its index.</summary>
            public int Vertex(Vector3 p, Vector3 n, Color col, Vector2 uv)
            {
                _v.Add(p); _n.Add(n.normalized); _c.Add(col); _uv.Add(uv);
                return _v.Count - 1;
            }

            public Builder Triangle(int a, int b, int c) { _t.Add(a); _t.Add(b); _t.Add(c); return this; }

            static Vector2 PlanarUV(Vector3 p, Vector3 n)
            {
                var an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                if (an.y >= an.x && an.y >= an.z) return new Vector2(p.x, p.z);
                if (an.x >= an.z) return new Vector2(p.z, p.y);
                return new Vector2(p.x, p.y);
            }

            /// <summary>Append an existing mesh transformed by m, tinted by col (multiplies its vertex colors).</summary>
            public Builder AddMesh(Mesh mesh, Matrix4x4 m, Color col)
            {
                var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv; var cols = mesh.colors; var tris = mesh.triangles;
                int baseIndex = _v.Count;
                var nm = m.inverse.transpose;
                for (int i = 0; i < verts.Length; i++)
                {
                    _v.Add(m.MultiplyPoint3x4(verts[i]));
                    _n.Add(norms.Length == verts.Length ? nm.MultiplyVector(norms[i]).normalized : Vector3.up);
                    _uv.Add(uvs.Length == verts.Length ? uvs[i] : Vector2.zero);
                    _c.Add(cols.Length == verts.Length ? cols[i] * col : col);
                }
                for (int i = 0; i < tris.Length; i++) _t.Add(baseIndex + tris[i]);
                return this;
            }

            // ---------------------------------------------------------------- box (optionally chamfered)
            public Builder AddBox(Vector3 center, Vector3 size, Quaternion rot, Color col, float bevel = 0f)
            {
                var h = size * 0.5f;
                float b = Mathf.Clamp(bevel, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
                var hb = new Vector3(h.x - b, h.y - b, h.z - b);

                // inset point on face 'axis' (0=x,1=y,2=z) for corner signs s
                Vector3 P(int axis, Vector3 s)
                {
                    var p = new Vector3(s.x * hb.x, s.y * hb.y, s.z * hb.z);
                    p[axis] = s[axis] * h[axis];
                    return center + rot * p;
                }

                // 6 faces
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        int u = (axis + 1) % 3, v = (axis + 2) % 3;
                        var s = Vector3.zero; s[axis] = sign;
                        Vector3 C(int su, int sv) { var t = s; t[u] = su; t[v] = sv; return P(axis, t); }
                        var outward = rot * (s);
                        Quad(C(-1, -1), C(1, -1), C(1, 1), C(-1, 1), col, outward);
                    }
                }
                if (b <= 1e-4f) return this;
                // 12 edge chamfers
                for (int a1 = 0; a1 < 3; a1++)
                {
                    int a2 = (a1 + 1) % 3, d = (a1 + 2) % 3;
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            Vector3 S(int sd) { var t = Vector3.zero; t[a1] = s1; t[a2] = s2; t[d] = sd; return t; }
                            var outward = Vector3.zero; outward[a1] = s1; outward[a2] = s2;
                            Quad(P(a1, S(-1)), P(a1, S(1)), P(a2, S(1)), P(a2, S(-1)), col, rot * outward);
                        }
                }
                // 8 corner triangles
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var s = new Vector3(sx, sy, sz);
                            Tri(P(0, s), P(1, s), P(2, s), col, rot * s);
                        }
                return this;
            }

            // ---------------------------------------------------------------- cylinder / cone
            public Builder AddCylinder(Vector3 center, float radius, float height, Quaternion rot, Color col, int segments = 12, bool smooth = false, bool caps = true)
            {
                segments = Mathf.Max(3, segments);
                float hh = height * 0.5f;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    var b0 = center + rot * (d0 * radius + Vector3.down * hh);
                    var b1 = center + rot * (d1 * radius + Vector3.down * hh);
                    var t0 = center + rot * (d0 * radius + Vector3.up * hh);
                    var t1 = center + rot * (d1 * radius + Vector3.up * hh);
                    if (smooth)
                    {
                        int i0 = Vertex(b0, rot * d0, col, new Vector2(i / (float)segments, 0f));
                        int i1 = Vertex(b1, rot * d1, col, new Vector2((i + 1) / (float)segments, 0f));
                        int i2 = Vertex(t1, rot * d1, col, new Vector2((i + 1) / (float)segments, 1f));
                        int i3 = Vertex(t0, rot * d0, col, new Vector2(i / (float)segments, 1f));
                        // outward winding (clockwise when viewed from outside in Unity's left-handed system)
                        Triangle(i0, i2, i1); Triangle(i0, i3, i2);
                    }
                    else
                    {
                        Quad(b0, b1, t1, t0, col, rot * ((d0 + d1) * 0.5f));
                    }
                    if (caps)
                    {
                        Tri(center + rot * (Vector3.up * hh), t0, t1, col, rot * Vector3.up);
                        Tri(center + rot * (Vector3.down * hh), b0, b1, col, rot * Vector3.down);
                    }
                }
                return this;
            }

            public Builder AddCone(Vector3 baseCenter, float radius, float height, Quaternion rot, Color col, int segments = 12)
            {
                segments = Mathf.Max(3, segments);
                var tip = baseCenter + rot * (Vector3.up * height);
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    var p0 = baseCenter + rot * (d0 * radius);
                    var p1 = baseCenter + rot * (d1 * radius);
                    Tri(p0, p1, tip, col, rot * ((d0 + d1) * 0.5f + Vector3.up * (radius / Mathf.Max(0.001f, height))));
                    Tri(baseCenter, p0, p1, col, rot * Vector3.down);
                }
                return this;
            }

            // ---------------------------------------------------------------- sphere / ellipsoid
            public Builder AddSphere(Vector3 center, float radius, Quaternion rot, Color col, int segments = 12, int rings = 8, bool smooth = true, Vector3? scale = null)
            {
                segments = Mathf.Max(3, segments); rings = Mathf.Max(2, rings);
                var sc = scale ?? Vector3.one;
                Vector3 Pt(int r, int s)
                {
                    float phi = Mathf.PI * r / rings;               // 0..pi from top
                    float th = 2f * Mathf.PI * s / segments;
                    return new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                }
                if (smooth)
                {
                    int start = _v.Count;
                    for (int r = 0; r <= rings; r++)
                        for (int s = 0; s <= segments; s++)
                        {
                            var u = Pt(r, s);
                            var p = Vector3.Scale(u * radius, sc);
                            var n = new Vector3(u.x / Mathf.Max(sc.x, 1e-4f), u.y / Mathf.Max(sc.y, 1e-4f), u.z / Mathf.Max(sc.z, 1e-4f));
                            Vertex(center + rot * p, rot * n, col, new Vector2(s / (float)segments, 1f - r / (float)rings));
                        }
                    int row = segments + 1;
                    for (int r = 0; r < rings; r++)
                        for (int s = 0; s < segments; s++)
                        {
                            int a = start + r * row + s, b = a + 1, c = a + row, d = c + 1;
                            if (r != 0) Triangle(a, b, c);
                            if (r != rings - 1) Triangle(b, d, c);
                        }
                }
                else
                {
                    for (int r = 0; r < rings; r++)
                        for (int s = 0; s < segments; s++)
                        {
                            Vector3 W(int rr, int ss) => center + rot * Vector3.Scale(Pt(rr, ss) * radius, sc);
                            var mid = rot * Vector3.Scale(Pt(r, s) + Pt(r + 1, s + 1), sc);
                            if (r == 0) Tri(W(0, 0), W(1, s), W(1, s + 1), col, mid);
                            else if (r == rings - 1) Tri(W(r, s), W(r, s + 1), W(rings, 0), col, mid);
                            else Quad(W(r, s), W(r, s + 1), W(r + 1, s + 1), W(r + 1, s), col, mid);
                        }
                }
                return this;
            }

            // ---------------------------------------------------------------- capsule (smooth)
            public Builder AddCapsule(Vector3 center, float radius, float height, Quaternion rot, Color col, int segments = 12, int capRings = 4)
            {
                segments = Mathf.Max(3, segments); capRings = Mathf.Max(1, capRings);
                float half = Mathf.Max(0f, height * 0.5f - radius);
                int start = _v.Count;
                int rings = capRings * 2 + 1; // rows of vertices = rings + 1
                for (int r = 0; r <= rings; r++)
                {
                    float phi; float yOff;
                    if (r <= capRings) { phi = Mathf.PI * 0.5f * r / capRings; yOff = half; }
                    else { phi = Mathf.PI * 0.5f + Mathf.PI * 0.5f * (r - capRings - 1) / capRings; yOff = -half; }
                    for (int s = 0; s <= segments; s++)
                    {
                        float th = 2f * Mathf.PI * s / segments;
                        var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        var p = n * radius + Vector3.up * yOff;
                        Vertex(center + rot * p, rot * n, col, new Vector2(s / (float)segments, r / (float)rings));
                    }
                }
                int row = segments + 1;
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < segments; s++)
                    {
                        int a = start + r * row + s, b = a + 1, c = a + row, d = c + 1;
                        Triangle(a, b, c); Triangle(b, d, c);
                    }
                return this;
            }

            // ---------------------------------------------------------------- torus (smooth, XZ plane)
            public Builder AddTorus(Vector3 center, float major, float minor, Quaternion rot, Color col, int majorSeg = 16, int minorSeg = 8)
            {
                int start = _v.Count;
                for (int i = 0; i <= majorSeg; i++)
                {
                    float a = 2f * Mathf.PI * i / majorSeg;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int j = 0; j <= minorSeg; j++)
                    {
                        float b = 2f * Mathf.PI * j / minorSeg;
                        var n = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        Vertex(center + rot * (dir * major + n * minor), rot * n, col, new Vector2(i / (float)majorSeg, j / (float)minorSeg));
                    }
                }
                int row = minorSeg + 1;
                for (int i = 0; i < majorSeg; i++)
                    for (int j = 0; j < minorSeg; j++)
                    {
                        int a = start + i * row + j, b = a + row, c = a + 1, d = b + 1;
                        Triangle(a, c, b); Triangle(c, d, b);
                    }
                return this;
            }

            // ---------------------------------------------------------------- wedge / ramp
            public Builder AddWedge(Vector3 baseCenter, Vector3 size, Quaternion rot, Color col)
            {
                var h = new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
                Vector3 W(float x, float y, float z) => baseCenter + rot * new Vector3(x, y, z);
                var bl = W(-h.x, 0, -h.z); var br = W(h.x, 0, -h.z); var fl = W(-h.x, 0, h.z); var fr = W(h.x, 0, h.z);
                var tl = W(-h.x, h.y, h.z); var tr = W(h.x, h.y, h.z);
                Quad(bl, br, fr, fl, col, rot * Vector3.down);              // bottom
                Quad(fl, fr, tr, tl, col, rot * Vector3.forward);           // back wall (+z)
                Quad(bl, br, tr, tl, col, rot * new Vector3(0f, size.z, -size.y)); // slope
                Tri(bl, fl, tl, col, rot * Vector3.left);
                Tri(br, fr, tr, col, rot * Vector3.right);
                return this;
            }
        }
    }
}
