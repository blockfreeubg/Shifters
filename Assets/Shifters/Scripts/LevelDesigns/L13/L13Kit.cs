// =====================================================================================
//  SHIFTERS - L13Kit.cs   [LEVEL 13 - HOSTILE TAKEOVER]  small mesh helpers (edit-mode safe)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class L13Kit
    {
        /// <summary>Flat ring in the XZ plane (normals up), centred on the origin.</summary>
        public static Mesh Annulus(float rIn, float rOut, int segments)
        {
            var m = new Mesh { name = "l13_annulus" };
            var v = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[v.Length];
            var n = new Vector3[v.Length];
            var tri = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i * 2] = new Vector3(c * rIn, 0f, s * rIn);
                v[i * 2 + 1] = new Vector3(c * rOut, 0f, s * rOut);
                uv[i * 2] = new Vector2(i / (float)segments * 8f, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments * 8f, 1f);
                n[i * 2] = n[i * 2 + 1] = Vector3.up;
            }
            for (int i = 0; i < segments; i++)
            {
                int b = i * 6, k = i * 2;
                tri[b] = k; tri[b + 1] = k + 2; tri[b + 2] = k + 1;
                tri[b + 3] = k + 1; tri[b + 4] = k + 2; tri[b + 5] = k + 3;
            }
            m.vertices = v; m.uv = uv; m.normals = n; m.triangles = tri;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Thin box from a to b (visual only).</summary>
        public static GameObject Beam(Transform parent, Vector3 a, Vector3 b, float thickness, MatKey mat, string name)
        {
            var d = b - a;
            var go = Kit.Make(name, parent, (a + b) * 0.5f, d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized) : Quaternion.identity);
            Kit.BoxVisual(go.transform, "Beam", Vector3.zero, new Vector3(thickness, thickness, d.magnitude), mat, 0f, null, false);
            return go;
        }
    }
}
