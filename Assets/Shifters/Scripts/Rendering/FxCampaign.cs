// =====================================================================================
//  SHIFTERS - FxCampaign.cs   (RENDERING, campaign wave Fx)
//  ColorBurst (ink ring of a world-color burst), CoinShower (heist), PaintSplat (pooled grey-immune decals),
//  ShockwaveRing (boss stomps), PaperBurst (paperwork explosions), ConfettiFlare (PARTY POPPER), GlitchSwap
//  (SWAP-O-MATIC). Fire-and-forget, safe to spam, no-ops in edit mode / batch without FxSystem.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static partial class Fx
    {
        static readonly Color GoldColor = new Color(1f, 0.78f, 0.2f, 1f);
        static readonly Color PaperColor = new Color(0.97f, 0.96f, 0.92f, 1f);

        /// <summary>World-color burst: a ground ink ring in color racing out to radius, paint droplets and sparkles.
        /// (WorldColor.AddBurst calls this; the saturation itself is the shader's.)</summary>
        public static void ColorBurst(Vector3 pos, Color color, float radius = 11f)
        {
            var s = S; if (s == null) return;
            radius = Mathf.Max(1f, radius);
            var c = color; c.a = 1f;
            s.Emit(s.ShockPs, pos + Vector3.up * 0.08f, Vector3.zero, radius * 2f, 1.4f, new Color(c.r, c.g, c.b, 0.9f));
            s.Emit(s.ShockPs, pos + Vector3.up * 0.1f, Vector3.zero, radius * 1.2f, 0.9f, new Color(1f, 1f, 1f, 0.6f));
            for (int i = 0; i < 26; i++)
            {
                var d = RandXZ(2.5f, 7f);
                var tint = i % 3 == 0 ? (Color)FxSystem.RandomConfettiColor() : c;
                s.Emit(s.DropsPs, pos + Vector3.up * 0.6f, d + Vector3.up * Random.Range(3f, 7.5f), Random.Range(0.14f, 0.3f), Random.Range(0.7f, 1.2f), tint);
            }
            for (int i = 0; i < 14; i++)
                s.Emit(s.SparklePs, pos + RandXZ(0.2f, radius * 0.45f) + Vector3.up * Random.Range(0.3f, 2.2f), Vector3.up * Random.Range(0.4f, 1.2f),
                    Random.Range(0.25f, 0.5f), Random.Range(0.6f, 1.1f), Color.Lerp(c, Color.white, 0.35f), Random.value * 360f);
            s.Emit(s.GlowPs, pos + Vector3.up * 0.8f, Vector3.zero, 4.5f, 0.45f, new Color(c.r, c.g, c.b, 0.8f));
        }

        /// <summary>Gold coins fountain up and rain down (vault, jackpot, Porkchop hits). count ~ 20..120.</summary>
        public static void CoinShower(Vector3 pos, int count = 50, float spread = 1f)
        {
            var s = S; if (s == null) return;
            count = Mathf.Clamp(count, 1, 200);
            spread = Mathf.Max(0.2f, spread);
            for (int i = 0; i < count; i++)
            {
                var v = RandXZ(0.5f, 3.2f) * spread + Vector3.up * Random.Range(5f, 9.5f);
                var g = Color.Lerp(GoldColor, new Color(1f, 0.92f, 0.55f, 1f), Random.value * 0.5f);
                s.EmitConfetti(pos + Vector3.up * 0.4f, v, Random.Range(0.16f, 0.26f), Random.Range(1.6f, 2.6f), g);
            }
            for (int i = 0; i < 10; i++)
                s.Emit(s.SparklePs, pos + RandXZ(0.1f, 1.5f * spread) + Vector3.up * Random.Range(0.5f, 2.5f), Vector3.up * 0.8f,
                    Random.Range(0.3f, 0.55f), Random.Range(0.5f, 1f), new Color(1f, 0.9f, 0.5f, 1f), Random.value * 360f);
            s.Emit(s.GlowPs, pos + Vector3.up * 0.8f, Vector3.zero, 3f, 0.35f, new Color(1f, 0.8f, 0.3f, 0.8f));
        }

        /// <summary>Paint splat decal on a surface (PAINT BLASTER, burst stamps). Pooled (oldest recycled, max 48),
        /// grey-immune, cleared on scene load. size = diameter in metres.</summary>
        public static GameObject PaintSplat(Vector3 pos, Vector3 normal, Color color, float size = 0.9f)
        {
            var s = S; if (s == null) return null;
            var go = SplatPool.Place(s.transform, pos, normal, color, size);
            for (int i = 0; i < 7; i++)
            {
                var d = Vector3.ProjectOnPlane(Random.onUnitSphere, normal).normalized * Random.Range(1f, 3f) + normal * Random.Range(0.5f, 2f);
                s.Emit(s.DropsPs, pos + normal * 0.1f, d, Random.Range(0.08f, 0.16f), Random.Range(0.3f, 0.6f), color);
            }
            return go;
        }

        /// <summary>Number of live paint splats (tests).</summary>
        public static int PaintSplatCount => SplatPool.Live;

        /// <summary>Removes every paint splat.</summary>
        public static void ClearPaintSplats() => SplatPool.Clear();

        /// <summary>Boss stomp / slam: a bright ground ring expanding to radius over seconds + dust + sparks.</summary>
        public static void ShockwaveRing(Vector3 pos, float radius = 6f, Color? color = null, float seconds = 0.55f)
        {
            var s = S; if (s == null) return;
            radius = Mathf.Max(0.5f, radius);
            var c = color ?? new Color(1f, 0.85f, 0.6f, 1f);
            s.Emit(s.ShockPs, pos + Vector3.up * 0.12f, Vector3.zero, radius * 2f, seconds, new Color(c.r, c.g, c.b, 0.95f));
            s.Emit(s.ShockPs, pos + Vector3.up * 0.14f, Vector3.zero, radius * 1.4f, seconds * 0.8f, new Color(1f, 1f, 1f, 0.7f));
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * 3f), 8, 28);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                s.Emit(s.Dust, pos + d * 0.6f + Vector3.up * 0.1f, d * (radius / Mathf.Max(0.1f, seconds)) * 0.55f + Vector3.up * 0.3f,
                    Random.Range(0.5f, 0.9f), Random.Range(0.5f, 0.8f), new Color(0.82f, 0.78f, 0.72f, 0.75f), Random.value * 360f);
            }
            Sparks(pos + Vector3.up * 0.2f, Vector3.up, 10, c);
        }

        /// <summary>Paperwork explosion: sheets flutter up and drift down (printers, filing cabinets, HR forms).</summary>
        public static void PaperBurst(Vector3 pos, int count = 24, float power = 1f)
        {
            var s = S; if (s == null) return;
            count = Mathf.Clamp(count, 1, 120);
            for (int i = 0; i < count; i++)
            {
                var v = RandXZ(0.6f, 2.8f) * power + Vector3.up * Random.Range(2.5f, 5.5f) * power;
                var c = i % 7 == 0 ? new Color(1f, 0.93f, 0.55f, 1f) : (i % 5 == 0 ? new Color(0.78f, 0.9f, 1f, 1f) : PaperColor);
                s.EmitConfetti(pos + Vector3.up * 0.3f, v, Random.Range(0.28f, 0.42f), Random.Range(2.2f, 3.4f), c);
            }
            Puff(pos, 0.8f);
        }

        /// <summary>PARTY POPPER: streak up, confetti blast and a short colored flash (visual only - the flare's
        /// gameplay light is the weapon's ILightSource).</summary>
        public static void ConfettiFlare(Vector3 pos, Color color, Vector3? dir = null)
        {
            var s = S; if (s == null) return;
            var d = (dir ?? Vector3.up).normalized;
            for (int i = 0; i < 40; i++)
            {
                var v = (d * Random.Range(4f, 8f) + Random.insideUnitSphere * 3.5f);
                s.EmitConfetti(pos, v, Random.Range(0.1f, 0.2f), Random.Range(1.4f, 2.4f), FxSystem.RandomConfettiColor());
            }
            for (int i = 0; i < 8; i++)
                s.Emit(s.StreakPs, pos, d * Random.Range(8f, 14f) + Random.insideUnitSphere * 2f, Random.Range(0.12f, 0.2f), Random.Range(0.2f, 0.35f), Color.Lerp(color, Color.white, 0.3f));
            s.Emit(s.GlowPs, pos, Vector3.zero, 2.6f, 0.4f, new Color(color.r, color.g, color.b, 0.9f));
            s.FlashLight(pos + d * 0.5f, color, 8f, 7f, 0.35f);
        }

        /// <summary>SWAP-O-MATIC: a digital glitch at both ends (pixel squares, cyan/magenta split) and a zig-zag bolt.</summary>
        public static void GlitchSwap(Vector3 a, Vector3 b, Color? color = null)
        {
            var s = S; if (s == null) return;
            var c = color ?? new Color(0.25f, 1f, 0.95f, 1f);
            GlitchAt(s, a, c);
            GlitchAt(s, b, c);
            var prev = a + Vector3.up;
            const int legs = 5;
            for (int i = 1; i <= legs; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)legs) + Vector3.up + (i < legs ? Random.insideUnitSphere * 0.35f : Vector3.zero);
                s.Tracer(prev, p, i % 2 == 0 ? c : new Color(1f, 0.3f, 0.85f, 1f), 0.07f, 0.22f);
                prev = p;
            }
        }

        static void GlitchAt(FxSystem s, Vector3 p, Color c)
        {
            for (int i = 0; i < 22; i++)
            {
                var off = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(0.1f, 2f), Random.Range(-0.45f, 0.45f));
                var col = (i & 1) == 0 ? c : new Color(1f, 0.3f, 0.85f, 1f);
                var ep = new ParticleSystem.EmitParams { position = p + off, velocity = new Vector3(Random.Range(-1.5f, 1.5f), 0f, 0f), startSize = Random.Range(0.08f, 0.2f), startLifetime = Random.Range(0.25f, 0.5f), startColor = col, rotation3D = Vector3.zero };
                if (s.ConfettiPs != null) s.ConfettiPs.Emit(ep, 1);
            }
            s.Emit(s.GlowPs, p + Vector3.up, Vector3.zero, 2.2f, 0.3f, new Color(c.r, c.g, c.b, 0.8f));
            s.ScanPulse(p, c, 2.5f, 0.35f);
        }

        // ------------------------------------------------------------------ paint splat pool
        static class SplatPool
        {
            const int Max = 48;
            const int Variants = 6;
            static readonly List<GameObject> _items = new List<GameObject>(Max);
            static int _cursor;
            static Mesh[] _meshes;
            static bool _hooked;

            public static int Live
            {
                get { int n = 0; for (int i = 0; i < _items.Count; i++) if (_items[i] != null && _items[i].activeSelf) n++; return n; }
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void ResetStatics() { _items.Clear(); _cursor = 0; _meshes = null; _hooked = false; }

            public static void Clear() { for (int i = 0; i < _items.Count; i++) if (_items[i] != null) _items[i].SetActive(false); }

            public static GameObject Place(Transform root, Vector3 pos, Vector3 normal, Color color, float size)
            {
                if (!_hooked) { _hooked = true; SceneManager.sceneLoaded += (sc, mode) => { if (mode == LoadSceneMode.Single) Clear(); }; }
                if (_meshes == null) BuildMeshes();
                GameObject go;
                if (_items.Count < Max)
                {
                    go = new GameObject("PaintSplat");
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    _items.Add(go);
                }
                else
                {
                    go = _items[_cursor];
                    _cursor = (_cursor + 1) % Max;
                    if (go == null) { _items.RemoveAll(x => x == null); return Place(root, pos, normal, color, size); }
                }
                go.SetActive(true);
                var n = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up;
                go.transform.position = pos + n * 0.012f;
                go.transform.rotation = Quaternion.LookRotation(n) * Quaternion.Euler(0f, 0f, Random.value * 360f);
                go.transform.localScale = Vector3.one * Mathf.Max(0.05f, size) * Random.Range(0.85f, 1.15f);
                go.GetComponent<MeshFilter>().sharedMesh = _meshes[Random.Range(0, Variants)];
                var c = color; c.a = 1f;
                go.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.ImmuneColored(c, 0.75f, 0.12f);
                return go;
            }

            static void BuildMeshes()
            {
                _meshes = new Mesh[Variants];
                var rng = new System.Random(4242);
                for (int v = 0; v < Variants; v++) _meshes[v] = BuildBlob(rng, v);
            }

            // irregular blob (diameter ~1, front face +Z so LookRotation(normal) shows it on the +normal side) + a few satellite drops
            static Mesh BuildBlob(System.Random rng, int v)
            {
                var verts = new List<Vector3>(80); var tris = new List<int>(240);
                const int seg = 18;
                AddDisc(verts, tris, Vector3.zero, 0.5f, seg, rng, 0.32f);
                int drops = 3 + v % 3;
                for (int d = 0; d < drops; d++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.55f + (float)rng.NextDouble() * 0.25f;
                    AddDisc(verts, tris, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f), 0.05f + (float)rng.NextDouble() * 0.07f, 8, rng, 0.1f);
                }
                var m = new Mesh { name = "PaintSplat_" + v };
                m.SetVertices(verts);
                m.SetTriangles(tris, 0);
                var normals = new Vector3[verts.Count];
                for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;
                m.normals = normals;
                m.RecalculateBounds();
                return m;
            }

            static void AddDisc(List<Vector3> verts, List<int> tris, Vector3 c, float r, int seg, System.Random rng, float wobble)
            {
                int b = verts.Count;
                verts.Add(c);
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float rr = r * (1f - wobble * 0.5f + wobble * (float)rng.NextDouble());
                    verts.Add(c + new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0f));
                }
                for (int i = 0; i < seg; i++) { tris.Add(b); tris.Add(b + 1 + i); tris.Add(b + 1 + (i + 1) % seg); }
            }
        }
    }
}
