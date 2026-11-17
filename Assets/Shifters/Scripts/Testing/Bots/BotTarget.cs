// =====================================================================================
//  SHIFTERS - BotTarget.cs   [BOTS module]
//  "Somewhere / something" argument for every TeamBot action. Implicitly converts from
//  Vector3, Transform, any Component (characters, plates, doors...), GameObject and string
//  (a LevelMark id placed by the level builder via ctx.Mark(id, go)). Re-evaluated every frame,
//  so moving targets (teammates, platforms) work.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public readonly struct BotTarget
    {
        readonly Vector3 _point;
        readonly Transform _transform;
        readonly string _mark;
        readonly Vector3 _offset;
        readonly bool _hasPoint;

        BotTarget(Vector3 point, Transform t, string mark, Vector3 offset, bool hasPoint)
        { _point = point; _transform = t; _mark = mark; _offset = offset; _hasPoint = hasPoint; }

        public static implicit operator BotTarget(Vector3 p) => new BotTarget(p, null, null, Vector3.zero, true);
        public static implicit operator BotTarget(Transform t) => new BotTarget(default, t, null, Vector3.zero, false);
        public static implicit operator BotTarget(Component c) => new BotTarget(default, c != null ? c.transform : null, null, Vector3.zero, false);
        public static implicit operator BotTarget(GameObject g) => new BotTarget(default, g != null ? g.transform : null, null, Vector3.zero, false);
        public static implicit operator BotTarget(string markId) => new BotTarget(default, null, markId, Vector3.zero, false);

        /// <summary>Same target shifted by a world-space offset (e.g. "1 m in front of the door").</summary>
        public BotTarget Offset(Vector3 worldOffset) => new BotTarget(_point, _transform, _mark, _offset + worldOffset, _hasPoint);

        /// <summary>Resolved transform (null for plain points or unresolved marks).</summary>
        public Transform Transform => _transform != null ? _transform : (!string.IsNullOrEmpty(_mark) ? BotTargets.FindMark(_mark) : null);

        public bool IsValid => _hasPoint || Transform != null;

        public Vector3 Position
        {
            get
            {
                if (_hasPoint) return _point + _offset;
                var t = Transform;
                return (t != null ? t.position : Vector3.zero) + _offset;
            }
        }

        public string Describe()
        {
            if (_hasPoint) return BotFmt.V(_point + _offset);
            if (!string.IsNullOrEmpty(_mark)) return "'" + _mark + "'" + (Transform == null ? "(MISSING)" : "");
            if (_transform != null) return _transform.name;
            return "(null)";
        }
    }

    /// <summary>Target resolution helpers (LevelMark lookup, interactables, signals, aim points).</summary>
    public static class BotTargets
    {
        static readonly Dictionary<string, Transform> _marks = new Dictionary<string, Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _marks.Clear();

        /// <summary>LevelMark.Find(id) (builder framework) with a GameObject-name fallback. Cached per id.</summary>
        public static Transform FindMark(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_marks.TryGetValue(id, out var cached) && cached != null) return cached;
            Transform found = null;
            var markType = BotReflect.FindType("LevelMark");
            if (markType != null && BotReflect.TryCallStatic(markType, "Find", out var r, id) && r is Component comp) found = comp.transform;
            if (found == null)
            {
                foreach (var mb in BotReflect.FindAll("LevelMark"))
                    if (BotReflect.TryGet<string>(mb, "Id", out var mid) && mid == id) { found = mb.transform; break; }
            }
            if (found == null) { var go = GameObject.Find(id); if (go != null) found = go.transform; }
            if (found != null) _marks[id] = found;
            return found;
        }

        /// <summary>All marks whose id starts with prefix (LevelMark registry only).</summary>
        public static List<Transform> FindMarks(string prefix)
        {
            var list = new List<Transform>();
            foreach (var mb in BotReflect.FindAll("LevelMark"))
                if (BotReflect.TryGet<string>(mb, "Id", out var mid) && mid != null && mid.StartsWith(prefix)) list.Add(mb.transform);
            return list;
        }

        public static GameObject AsGameObject(object o)
        {
            switch (o)
            {
                case GameObject g: return g;
                case Component c: return c != null ? c.gameObject : null;
                case string id: { var t = FindMark(id); return t != null ? t.gameObject : null; }
                case BotTarget bt: { var t = bt.Transform; return t != null ? t.gameObject : null; }
                default: return null;
            }
        }

        public static Grabbable ResolveGrabbable(BotTarget t)
        {
            var tr = t.Transform;
            if (tr == null) return null;
            var g = tr.GetComponentInParent<Grabbable>();
            return g != null ? g : tr.GetComponentInChildren<Grabbable>();
        }

        public static IInteractable ResolveInteractable(BotTarget t)
        {
            var tr = t.Transform;
            if (tr == null) return null;
            var i = tr.GetComponent<IInteractable>();
            if (i == null) i = tr.GetComponentInChildren<IInteractable>();
            if (i == null) i = tr.GetComponentInParent<IInteractable>();
            return i;
        }

        public static SignalSource ResolveSource(object o)
        {
            if (o is SignalSource s) return s;
            var go = AsGameObject(o);
            return go != null ? go.GetComponentInChildren<SignalSource>() : null;
        }

        public static SignalReceiver ResolveReceiver(object o)
        {
            if (o is SignalReceiver r) return r;
            var go = AsGameObject(o);
            return go != null ? go.GetComponentInChildren<SignalReceiver>() : null;
        }

        /// <summary>Receiver.Powered, else Source.IsOn, else a bool member named Powered/IsOn/IsOpen/IsSolved/IsComplete.
        /// null when nothing readable was found.</summary>
        public static bool? ReadPowered(object o)
        {
            if (o is BotTarget bt) o = bt.Transform;
            var r = ResolveReceiver(o);
            if (r != null) return r.Powered;
            var s = ResolveSource(o);
            if (s != null) return s.IsOn;
            var go = AsGameObject(o);
            if (go == null) return null;
            string[] names = { "Powered", "IsOn", "IsOpen", "IsSolved", "IsComplete", "Completed" };
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>())
                foreach (var n in names)
                    if (BotReflect.TryGet<bool>(mb, n, out var b)) return b;
            return null;
        }

        /// <summary>Center of the target's colliders (or renderers / position).</summary>
        public static Vector3 AimPoint(BotTarget t)
        {
            var tr = t.Transform;
            if (tr == null) return t.Position;
            var col = tr.GetComponentInChildren<Collider>();
            if (col != null && col.enabled) return col.bounds.center;
            var ren = tr.GetComponentInChildren<Renderer>();
            return ren != null ? ren.bounds.center : tr.position;
        }

        /// <summary>Planar radius of an object (character radius or collider extents).</summary>
        public static float RadiusOf(Component c)
        {
            if (c == null) return 0.4f;
            var sc = c.GetComponentInParent<ShifterCharacter>();
            if (sc != null) return sc.Radius;
            var col = c.GetComponentInChildren<Collider>();
            if (col == null) return 0.4f;
            var e = col.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }
    }

    /// <summary>Tiny formatting helpers for logs.</summary>
    public static class BotFmt
    {
        public static string V(Vector3 v) => "(" + v.x.ToString("0.0") + "," + v.y.ToString("0.0") + "," + v.z.ToString("0.0") + ")";
        public static string F(float f) => f.ToString("0.00");
        public static string Name(ShifterCharacter c) => c != null ? c.DisplayName + "[" + c.Archetype + "]" : "(none)";
        public static string Obj(object o)
        {
            if (o == null) return "(null)";
            if (o is Component c) return c != null ? c.name + ":" + c.GetType().Name : "(destroyed)";
            return o.ToString();
        }
    }
}
