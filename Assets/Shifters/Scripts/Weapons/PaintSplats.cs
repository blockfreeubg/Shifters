// =====================================================================================
//  SHIFTERS - PaintSplats.cs   [owned by the WEAPONS agent]
//  PAINT BLASTER splat decals: a pool of at most 24 flat blobs (oldest reused), grey-immune
//  (Unlit materials skip the world-colour desaturation), cycling the 7 Reserve colors.
//  Splats on moving things (rigidbodies, characters) ride along as children.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class PaintSplats
    {
        public const int MaxSplats = 24;
        /// <summary>The 7 Reserve colors (red, orange, yellow, green, cyan, blue, violet).</summary>
        public static readonly Color[] Colors =
        {
            UIColors.Hex(0xE8394B), UIColors.Hex(0xFF8A1F), UIColors.Hex(0xFFD23C), UIColors.Hex(0x5BD66B),
            UIColors.Hex(0x3DE0FF), UIColors.Hex(0x4D6BFF), UIColors.Hex(0xB07CFF),
        };

        static readonly List<Transform> _pool = new List<Transform>(MaxSplats);
        static int _next;
        static Transform _root;
        static readonly List<Renderer> _rends = new List<Renderer>(4);

        /// <summary>Remove every splat (level reset / tests).</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < _pool.Count; i++) if (_pool[i] != null) Object.Destroy(_pool[i].gameObject);
            _pool.Clear(); _next = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pool.Clear(); _next = 0; _root = null; }

        /// <summary>Live splats (tests).</summary>
        public static int Count { get { Prune(); return _pool.Count; } }

        static void Prune() { for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i); if (_next > _pool.Count) _next = 0; }

        /// <summary>Stick a splat of 'color' on the surface at point/normal (hit collider optional).</summary>
        public static Transform Splat(Vector3 point, Vector3 normal, Collider on, Color color)
        {
            Prune();
            if (_root == null) _root = new GameObject("[PaintSplats]").transform;
            Transform t;
            if (_pool.Count < MaxSplats)
            {
                var go = new GameObject("PaintSplat");
                var mesh = ProcMesh.Cached("paint_splat_disc", () => ProcMesh.Cylinder(0.5f, 0.012f, 14, false));
                ProcMesh.AddRenderer(go, mesh, MaterialLibrary.Unlit(color, 1.05f), false);
                var blob = ProcMesh.Cached("paint_splat_drop", () => ProcMesh.Sphere(0.12f, 6, 4, true));
                ProcMesh.Part("Drop1", go.transform, blob, MaterialLibrary.Unlit(color, 1.05f), new Vector3(0.55f, 0f, 0.15f), null, new Vector3(1f, 0.1f, 1f), false);
                ProcMesh.Part("Drop2", go.transform, blob, MaterialLibrary.Unlit(color, 1.05f), new Vector3(-0.3f, 0f, -0.5f), null, new Vector3(0.7f, 0.1f, 0.7f), false);
                t = go.transform;
                _pool.Add(t);
            }
            else
            {
                t = _pool[_next % _pool.Count];
                _next = (_next + 1) % MaxSplats;
                var mat = MaterialLibrary.Unlit(color, 1.05f);
                t.GetComponentsInChildren(_rends); for (int i = 0; i < _rends.Count; i++) _rends[i].sharedMaterial = mat;
            }
            Transform parent = _root;
            if (on != null)
            {
                var rb = on.attachedRigidbody;
                Transform moving = rb != null ? rb.transform : on.GetComponentInParent<ShifterCharacter>() != null ? on.GetComponentInParent<ShifterCharacter>().transform : null;
                if (moving != null && IsUniform(moving.lossyScale)) parent = moving;
            }
            if (normal.sqrMagnitude < 1e-4f) normal = Vector3.up;
            t.SetParent(parent, true);
            t.position = point + normal.normalized * 0.012f;
            t.rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float size = Random.Range(0.32f, 0.55f);
            Vector3 ls = parent == _root ? Vector3.one : InverseScale(parent.lossyScale);
            t.localScale = Vector3.Scale(new Vector3(size, 1f, size * Random.Range(0.8f, 1.15f)), ls);
            t.gameObject.SetActive(true);
            return t;
        }

        static bool IsUniform(Vector3 s) => Mathf.Abs(s.x - s.y) < 0.01f && Mathf.Abs(s.y - s.z) < 0.01f && s.x > 0.01f;
        static Vector3 InverseScale(Vector3 s) => new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
    }
}
