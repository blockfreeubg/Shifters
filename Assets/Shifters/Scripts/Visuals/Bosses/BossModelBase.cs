// =====================================================================================
//  SHIFTERS - BossModelBase.cs   [owned by VISUALS_BOSSES]
//  Base of every boss / set-piece model component (DownsizerModel, PorkchopModel, ...). Models are KINEMATIC named
//  hierarchies: no rigidbodies, colliders only where the chapter spec asks for them; the boss brains (BossBase
//  subclasses in the level folders) animate the named parts. Weak points are added by BossKit at the named anchors.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public abstract class BossModelBase : MonoBehaviour
    {
        /// <summary>Model id ("Downsizer", "Porkchop", ...).</summary>
        public string ModelId;
        /// <summary>The named parts (same order as RequiredParts), resolved by the factory.</summary>
        public Transform[] Parts = new Transform[0];

        /// <summary>Names of the parts the factory guarantees (tests assert them; brains may look them up).</summary>
        public abstract string[] RequiredParts { get; }

        /// <summary>Named part (any depth). Null if missing.</summary>
        public Transform Part(string partName)
        {
            if (string.IsNullOrEmpty(partName)) return null;
            for (int i = 0; i < Parts.Length; i++) if (Parts[i] != null && Parts[i].name == partName) return Parts[i];
            return FindDeep(transform, partName);
        }

        /// <summary>True if every RequiredParts entry exists; missing lists the absent ones.</summary>
        public bool HasAllParts(out string missing)
        {
            missing = "";
            foreach (var n in RequiredParts) if (FindDeep(transform, n) == null) missing += (missing.Length > 0 ? ", " : "") + n;
            return missing.Length == 0;
        }

        /// <summary>Called by the factories at the end of the build.</summary>
        public void CollectParts()
        {
            var req = RequiredParts;
            var list = new List<Transform>(req.Length);
            foreach (var n in req) { var t = FindDeep(transform, n); if (t != null) list.Add(t); }
            Parts = list.ToArray();
        }

        public int RendererCount => GetComponentsInChildren<Renderer>(true).Length;

        /// <summary>World bounds of all renderers (for tests / camera framing).</summary>
        public Bounds RenderBounds()
        {
            var rs = GetComponentsInChildren<Renderer>(false);
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in rs) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        public static Transform FindDeep(Transform t, string n)
        {
            if (t == null) return null;
            if (t.name == n) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), n); if (r != null) return r; }
            return null;
        }
    }
}
