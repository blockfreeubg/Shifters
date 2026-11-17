// =====================================================================================
//  SHIFTERS - EnemyBridge.cs   [owned by the ENEMIES agent]
//  LightQuery: "is this point lit?" for stalkers / Bravery. Uses puzzles' LightRegistry.IsLit when that
//  type exists (resolved once by reflection so the enemies module compiles with or without it) OR a
//  local fallback registry (ILightSource pools + ILightCone flashlights with line of sight) that tests
//  and levels can also feed directly. Also bridges ShifterCharacter.SetScared(bool) (seams).
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class LightQuery
    {
        static readonly List<ILightSource> _sources = new List<ILightSource>();
        static readonly List<ILightCone> _cones = new List<ILightCone>();
        static readonly List<Func<Vector3, bool>> _extra = new List<Func<Vector3, bool>>();
        static readonly RaycastHit[] _hits = new RaycastHit[12];
        static bool _resolved;
        static Func<Vector3, bool> _registryIsLit;
        static MethodInfo _regSource, _regCone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _sources.Clear(); _cones.Clear(); _extra.Clear(); _resolved = false; _registryIsLit = null; _regSource = _regCone = null; }

        /// <summary>True when puzzles' LightRegistry was found.</summary>
        public static bool HasRegistry { get { Resolve(); return _registryIsLit != null; } }

        /// <summary>Register a pool locally AND with LightRegistry (if present).</summary>
        public static void Register(ILightSource s)
        {
            if (s == null) return;
            if (!_sources.Contains(s)) _sources.Add(s);
            Resolve();
            if (_regSource != null) { try { _regSource.Invoke(null, new object[] { s }); } catch (Exception) { } }
        }

        public static void Unregister(ILightSource s) { _sources.Remove(s); }

        /// <summary>Register a cone (flashlight) locally AND with LightRegistry (if present).</summary>
        public static void RegisterCone(ILightCone c)
        {
            if (c == null) return;
            if (!_cones.Contains(c)) _cones.Add(c);
            Resolve();
            if (_regCone != null) { try { _regCone.Invoke(null, new object[] { c }); } catch (Exception) { } }
        }

        public static void UnregisterCone(ILightCone c) { _cones.Remove(c); }

        /// <summary>Extra "counts as lit" rule (e.g. a level's timed hall window).</summary>
        public static void AddRule(Func<Vector3, bool> rule) { if (rule != null && !_extra.Contains(rule)) _extra.Add(rule); }
        public static void RemoveRule(Func<Vector3, bool> rule) { _extra.Remove(rule); }

        /// <summary>Lit = inside an ON pool, inside an ON cone (angle/range + clear line of sight), any extra rule,
        /// or LightRegistry.IsLit(p).</summary>
        public static bool IsLit(Vector3 p)
        {
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                if (s == null || (s is UnityEngine.Object o && o == null)) { _sources.RemoveAt(i); continue; }
                if (!s.LightOn) continue;
                Vector3 d = p - s.LightPosition; d.y = 0f;
                if (d.sqrMagnitude <= s.LightRadius * s.LightRadius && Mathf.Abs(p.y - s.LightPosition.y) < 4f) return true;
            }
            for (int i = _cones.Count - 1; i >= 0; i--)
            {
                var c = _cones[i];
                if (c == null || (c is UnityEngine.Object o && o == null)) { _cones.RemoveAt(i); continue; }
                if (InCone(c, p)) return true;
            }
            for (int i = 0; i < _extra.Count; i++) if (_extra[i] != null && _extra[i](p)) return true;
            Resolve();
            if (_registryIsLit != null) { try { return _registryIsLit(p); } catch (Exception) { _registryIsLit = null; } }
            return false;
        }

        /// <summary>Cone test: on, within range, within HalfAngle of Forward, clear line of sight (characters and triggers ignored).</summary>
        public static bool InCone(ILightCone c, Vector3 p)
        {
            if (c == null || !c.LightOn) return false;
            Vector3 to = p - c.Origin;
            float d = to.magnitude;
            if (d > c.Range) return false;
            if (d > 0.05f && Vector3.Angle(c.Forward, to) > c.HalfAngle) return false;
            return ClearLine(c.Origin, p, null);
        }

        /// <summary>True if nothing solid (non-trigger, non-character, not under ignoreRoot) blocks a..b.</summary>
        public static bool ClearLine(Vector3 a, Vector3 b, Transform ignoreRoot)
        {
            Vector3 dir = b - a;
            float dist = dir.magnitude;
            if (dist < 0.05f) return true;
            dir /= dist;
            int n = Physics.RaycastNonAlloc(a, dir, _hits, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i].collider;
                if (col == null) continue;
                if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) continue;
                if (col.GetComponentInParent<ShifterCharacter>() != null) continue;
                if (col.GetComponentInParent<StalkerBrain>() != null) continue;
                return false;
            }
            return true;
        }

        static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                var t = EnemyBridge.FindType("Shifters.LightRegistry");
                if (t == null) return;
                var m = t.GetMethod("IsLit", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Vector3) }, null);
                if (m != null && m.ReturnType == typeof(bool)) _registryIsLit = (Func<Vector3, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, bool>), m);
                _regSource = t.GetMethod("Register", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ILightSource) }, null);
                _regCone = t.GetMethod("RegisterCone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ILightCone) }, null);
            }
            catch (Exception e) { Debug.LogWarning("[Enemies] LightRegistry bridge failed: " + e.Message); }
        }
    }

    /// <summary>Null-safe reflection bridges to campaign seams that may not exist yet in a given build.</summary>
    public static class EnemyBridge
    {
        static bool _scaredResolved;
        static Action<ShifterCharacter, bool> _setScared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _scaredResolved = false; _setScared = null; }

        public static Type FindType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            if (t != null) return t;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>True when ShifterCharacter.SetScared(bool) exists (seams).</summary>
        public static bool HasSetScared { get { ResolveScared(); return _setScared != null; } }

        /// <summary>ShifterCharacter.SetScared(bool) if the seam exists; otherwise nothing.</summary>
        public static void SetScared(ShifterCharacter c, bool scared)
        {
            if (c == null) return;
            ResolveScared();
            if (_setScared == null) return;
            try { _setScared(c, scared); } catch (Exception) { }
        }

        static void ResolveScared()
        {
            if (_scaredResolved) return;
            _scaredResolved = true;
            try
            {
                var m = typeof(ShifterCharacter).GetMethod("SetScared", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                if (m != null) _setScared = (Action<ShifterCharacter, bool>)Delegate.CreateDelegate(typeof(Action<ShifterCharacter, bool>), m);
            }
            catch (Exception e) { Debug.LogWarning("[Enemies] SetScared bridge failed: " + e.Message); }
        }
    }
}
