// =====================================================================================
//  SHIFTERS - Kit.cs   [CONTRACT FILE - conventions for runtime-capable object factories]
//  Every module that places things in levels exposes a static "<Module>Kit" class with factory
//  methods (e.g. PuzzleKit.PressurePlate(parent, pos, rot, requiredKg)). Factories:
//    * build complete GameObjects in code (visual meshes via ProcMesh.Cached/CachedBox + MaterialLibrary,
//      colliders, components configured through PUBLIC fields), and return the main component;
//    * work identically at runtime (DevPlay tests) and in the Editor (the scene builder calls them in
//      edit mode and saves the scene) -> NO Start-time construction: build everything inside the factory;
//    * use Kit.Theme for theme-dependent materials;
//    * never require custom tags or layers.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class Kit
    {
        /// <summary>Theme used by factories for materials. The level builder sets it before building a level.</summary>
        public static LevelTheme Theme = LevelTheme.Training;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Theme = LevelTheme.Training;

        public static Material Mat(MatKey key) => MaterialLibrary.Get(key, Theme);

        public static GameObject Make(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            return go;
        }

        public static GameObject Make(string name, Transform parent, Vector3 position) => Make(name, parent, position, Quaternion.identity);

        /// <summary>Child renderer using a cached beveled box mesh. center is local to parent.</summary>
        public static MeshRenderer BoxVisual(Transform parent, string name, Vector3 center, Vector3 size, MatKey mat, float bevel = 0.04f, Quaternion? rot = null, bool castShadows = true)
        {
            var go = Make(name, parent, center, rot ?? Quaternion.identity);
            return ProcMesh.AddRenderer(go, ProcMesh.CachedBox(size, bevel), Mat(mat), castShadows);
        }

        /// <summary>Solid static box: visual + BoxCollider on the same child.</summary>
        public static BoxCollider SolidBox(Transform parent, string name, Vector3 center, Vector3 size, MatKey mat, float bevel = 0.04f, Quaternion? rot = null)
        {
            var r = BoxVisual(parent, name, center, size, mat, bevel, rot);
            var col = r.gameObject.AddComponent<BoxCollider>();
            col.size = size;
            return col;
        }

        /// <summary>Trigger volume child (no renderer). Uses the built-in "Ignore Raycast" layer so camera/ground probes skip it.</summary>
        public static BoxCollider Trigger(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = Make(name, parent, center);
            go.layer = 2; // Ignore Raycast (built-in)
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            return col;
        }

        /// <summary>Destroy that works in edit mode and play mode.</summary>
        public static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
