// =====================================================================================
//  SHIFTERS - LevelMark.cs   (BUILDER module - runtime level framework)
//  Named handle on a level object so solution bots / tests can find it: LevelMark.Find("s2.heavyBlock").
//  Added by LevelBuildContext.Mark(id, go) / MarkPoint(id, pos). A GameObject may carry several marks.
//  Ids are free-form; convention: "<section>.<thing>" (lowercase section, camelCase thing).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class LevelMark : MonoBehaviour
    {
        /// <summary>Unique id within the level, e.g. "s3.bridgeFar".</summary>
        public string Id;

        static readonly Dictionary<string, LevelMark> _byId = new Dictionary<string, LevelMark>();
        static readonly List<LevelMark> _all = new List<LevelMark>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _byId.Clear(); _all.Clear(); }

        /// <summary>All registered marks (objects that have been active at least once).</summary>
        public static IReadOnlyList<LevelMark> All => _all;

        void Awake() => Register(this);
        void OnDestroy() => Unregister(this);

        static void Register(LevelMark m)
        {
            if (m == null || string.IsNullOrEmpty(m.Id)) return;
            if (!_all.Contains(m)) _all.Add(m);
            if (_byId.TryGetValue(m.Id, out var existing) && existing != null && existing != m)
            {
                Debug.LogWarning("[LevelMark] duplicate id '" + m.Id + "' on " + m.name + " (keeping " + existing.name + ")");
                return;
            }
            _byId[m.Id] = m;
        }

        static void Unregister(LevelMark m)
        {
            _all.Remove(m);
            if (m != null && !string.IsNullOrEmpty(m.Id) && _byId.TryGetValue(m.Id, out var existing) && existing == m) _byId.Remove(m.Id);
        }

        /// <summary>The GameObject marked with id (also finds never-activated / edit-mode objects via a scan). Null if none.</summary>
        public static GameObject Find(string id)
        {
            var m = FindMark(id);
            return m != null ? m.gameObject : null;
        }

        /// <summary>Component T on (or under) the object marked with id.</summary>
        public static T Find<T>(string id) where T : Component
        {
            var go = Find(id);
            if (go == null) return null;
            var c = go.GetComponent<T>();
            return c != null ? c : go.GetComponentInChildren<T>(true);
        }

        /// <summary>World position of the marked object (Vector3.zero + warning if missing).</summary>
        public static Vector3 Position(string id)
        {
            var go = Find(id);
            if (go == null) { Debug.LogWarning("[LevelMark] no mark '" + id + "'"); return Vector3.zero; }
            return go.transform.position;
        }

        public static bool Exists(string id) => FindMark(id) != null;

        public static LevelMark FindMark(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId.TryGetValue(id, out var m) && m != null) return m;
            // fallback scan: inactive objects never ran Awake; edit mode has no registry
            foreach (var mark in Object.FindObjectsByType<LevelMark>(FindObjectsInactive.Include))
            {
                if (mark.Id != id) continue;
                if (Application.isPlaying) Register(mark);
                return mark;
            }
            return null;
        }

        /// <summary>All marks whose id starts with prefix (e.g. "s2."), sorted by id. Allocates - not for per-frame use.</summary>
        public static List<LevelMark> FindAll(string prefix)
        {
            var result = new List<LevelMark>();
            FindAll(prefix, result);
            return result;
        }

        public static void FindAll(string prefix, List<LevelMark> results)
        {
            results.Clear();
            foreach (var mark in Object.FindObjectsByType<LevelMark>(FindObjectsInactive.Include))
                if (mark.Id != null && (string.IsNullOrEmpty(prefix) || mark.Id.StartsWith(prefix, System.StringComparison.Ordinal))) results.Add(mark);
            results.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        /// <summary>Adds a mark to go (reuses an existing mark with the same id).</summary>
        public static LevelMark Add(GameObject go, string id)
        {
            if (go == null || string.IsNullOrEmpty(id)) return null;
            foreach (var existing in go.GetComponents<LevelMark>()) if (existing.Id == id) return existing;
            var m = go.AddComponent<LevelMark>();
            m.Id = id;
            if (Application.isPlaying) Register(m); // Awake ran before Id was set
            return m;
        }
    }
}
