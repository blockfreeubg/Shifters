// =====================================================================================
//  SHIFTERS - EnemyConeVisual.cs   [owned by the ENEMIES agent]
//  Hologram floor wedge (vision cone) for security cameras and guard bots: apex at the transform,
//  opens along local +Z, radius Range, half-angle HalfAngle. Color via MaterialPropertyBlock
//  (white idle -> orange spotting -> red detected). Mesh cached per (range, angle).
// =====================================================================================
using System.Globalization;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class EnemyConeVisual : MonoBehaviour
    {
        public static readonly Color Idle = new Color(0.92f, 0.95f, 1f, 0.32f);
        public static readonly Color Spotting = new Color(1f, 0.55f, 0.1f, 0.5f);
        public static readonly Color Alert = new Color(1f, 0.12f, 0.1f, 0.62f);
        public static readonly Color Off = new Color(0.4f, 0.42f, 0.48f, 0.12f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public MeshRenderer Renderer;
        public float Range = 12f;
        public float HalfAngle = 20f;

        MaterialPropertyBlock _mpb;
        Color _color = new Color(-1f, 0f, 0f, 0f);

        /// <summary>Rendering's wall-clipped hologram fan (preferred look). When present our own wedge stays hidden and
        /// colours map to its states: Idle / Spotting (any in-between tint) / Alarm / Off (grey).</summary>
        public VisionConeMesh Fan;
        static readonly Color FanOff = new Color(0.45f, 0.47f, 0.52f, 0.12f);
        static readonly Color FanSpotting = new Color(1f, 0.55f, 0.06f, 0.55f);
        static readonly Color FanAlarm = new Color(1f, 0.08f, 0.06f, 0.75f);
        int _fanState = -1;
        bool _visible = true;
        float _fanFloorY = float.NaN;

        public Color CurrentColor => _color;
        public bool Visible => Fan != null ? _visible : Renderer != null && Renderer.enabled;

        void LateUpdate()
        {
            if (Fan == null) return;
            float y = transform.position.y - 0.03f;
            if (float.IsNaN(_fanFloorY) || Mathf.Abs(y - _fanFloorY) > 0.01f) { _fanFloorY = y; Fan.FloorY = y; Fan.Rebuild(); }
        }

        void ApplyFan(Color c)
        {
            int st = c == Alert ? 2 : c == Off ? 3 : c == Idle ? 0 : 1;
            if (st == _fanState) return;
            _fanState = st;
            switch (st)
            {
                case 0: Fan.SetState(VisionConeMesh.State.Idle); break;
                case 1: Fan.SetColor(FanSpotting); break;   // stronger than the defaults: must read on bright floors
                case 2: Fan.SetColor(FanAlarm); break;
                default: Fan.SetColor(FanOff); break;
            }
        }

        public void SetColor(Color c)
        {
            if (Renderer == null || c == _color) return;
            _color = c;
            if (Fan != null) { ApplyFan(c); return; }
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            Renderer.SetPropertyBlock(_mpb);
        }

        public void SetVisible(bool v)
        {
            if (Fan != null) { if (_visible != v) { _visible = v; Fan.gameObject.SetActive(v); } return; }
            if (Renderer != null && Renderer.enabled != v) Renderer.enabled = v;
        }

        /// <summary>Builds the wedge as a child of parent at localPos (local yaw = parent's).</summary>
        public static EnemyConeVisual Create(Transform parent, string name, Vector3 localPos, float range, float halfAngle, Color color)
        {
            var go = Kit.Make(name, parent, localPos);
            var mr = ProcMesh.AddRenderer(go, Wedge(range, halfAngle), Kit.Mat(MatKey.Hologram), false);
            mr.receiveShadows = false;
            var v = go.AddComponent<EnemyConeVisual>();
            v.Renderer = mr; v.Range = range; v.HalfAngle = halfAngle;
            try
            {
                var fan = VisionConeMesh.Create(go.transform, Vector3.zero, null, range, halfAngle * 2f, VisionConeMesh.Style.FloorFan);
                if (fan != null)
                {
                    fan.FloorLift = 0.03f;
                    fan.FloorY = go.transform.position.y - 0.03f;
                    fan.Segments = Mathf.Clamp(Mathf.CeilToInt(halfAngle * 2f / 4f), 10, 28);
                    fan.Rebuild();
                    v.Fan = fan;
                    mr.enabled = false;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Enemies] VisionConeMesh unavailable, using the plain wedge: " + e.Message); }
            v.SetColor(color);
            return v;
        }

        /// <summary>Flat fan (XZ plane, +Z forward) with a brighter rim band; vertex alpha fades toward the apex.</summary>
        public static Mesh Wedge(float range, float halfAngle)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "enemy_cone_{0:0.##}_{1:0.#}", range, halfAngle);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                int seg = Mathf.Clamp(Mathf.CeilToInt(halfAngle * 2f / 4f), 6, 40);
                float inner = Mathf.Max(0.2f, range - 0.35f);
                Color apex = new Color(1f, 1f, 1f, 0.35f), mid = new Color(1f, 1f, 1f, 0.7f), rim = Color.white;
                int a = b.Vertex(Vector3.zero, Vector3.up, apex, new Vector2(0.5f, 0f));
                for (int i = 0; i < seg; i++)
                {
                    float t0 = Mathf.Lerp(-halfAngle, halfAngle, i / (float)seg) * Mathf.Deg2Rad;
                    float t1 = Mathf.Lerp(-halfAngle, halfAngle, (i + 1) / (float)seg) * Mathf.Deg2Rad;
                    Vector3 d0 = new Vector3(Mathf.Sin(t0), 0f, Mathf.Cos(t0)), d1 = new Vector3(Mathf.Sin(t1), 0f, Mathf.Cos(t1));
                    int m0 = b.Vertex(d0 * inner, Vector3.up, mid, new Vector2(0f, 0.9f));
                    int m1 = b.Vertex(d1 * inner, Vector3.up, mid, new Vector2(1f, 0.9f));
                    int r0 = b.Vertex(d0 * range, Vector3.up, rim, new Vector2(0f, 1f));
                    int r1 = b.Vertex(d1 * range, Vector3.up, rim, new Vector2(1f, 1f));
                    // clockwise seen from above (Unity front faces)
                    b.Triangle(a, m0, m1);
                    b.Triangle(m0, r0, r1);
                    b.Triangle(m0, r1, m1);
                }
                // side edges (thin bright strips)
                for (int s = -1; s <= 1; s += 2)
                {
                    float t = s * halfAngle * Mathf.Deg2Rad;
                    Vector3 d = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t));
                    Vector3 n = new Vector3(d.z, 0f, -d.x) * (0.06f * s);
                    int e0 = b.Vertex(Vector3.zero, Vector3.up, rim, new Vector2(0f, 0f));
                    int e1 = b.Vertex(d * range, Vector3.up, rim, new Vector2(0f, 1f));
                    int e2 = b.Vertex(d * range - n, Vector3.up, rim, new Vector2(1f, 1f));
                    int e3 = b.Vertex(-n * 0.3f, Vector3.up, rim, new Vector2(1f, 0f));
                    b.Triangle(e0, e1, e2); b.Triangle(e0, e2, e3);   // both windings: always visible from above
                    b.Triangle(e0, e2, e1); b.Triangle(e0, e3, e2);
                }
                return b.ToMesh(key);
            });
        }
    }
}
