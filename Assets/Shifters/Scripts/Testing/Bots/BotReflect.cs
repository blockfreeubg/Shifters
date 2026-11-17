// =====================================================================================
//  SHIFTERS - BotReflect.cs   [BOTS module]
//  Duck-typed access to modules that have no contract yet (LevelMark, Pushable, Terminal,
//  LevelExit, PuzzleKit, LevelKit...) plus stub detection (contract stubs have tiny IL bodies),
//  so the bots degrade gracefully while other modules are still being written.
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class BotReflect
    {
        const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Stat = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _types.Clear();

        /// <summary>Find a type by full or short name ("LevelMark" => "Shifters.LevelMark"). Cached; null if absent.</summary>
        public static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_types.TryGetValue(name, out var t)) return t;
            string full = name.Contains(".") ? name : "Shifters." + name;
            t = typeof(BotReflect).Assembly.GetType(full, false); // all SHIFTERS runtime code is in Assembly-CSharp (no asmdefs)
            if (t == null) t = Type.GetType(full + ", Assembly-CSharp", false);
            _types[name] = t;
            return t;
        }

        public static bool HasType(string name) => FindType(name) != null;

        /// <summary>Read a property or field (any visibility). null if missing.</summary>
        public static object Get(object o, string member)
        {
            if (o == null) return null;
            var t = o.GetType();
            var p = t.GetProperty(member, Inst);
            if (p != null && p.GetIndexParameters().Length == 0) { try { return p.GetValue(o); } catch (Exception) { return null; } }
            var f = t.GetField(member, Inst);
            return f != null ? f.GetValue(o) : null;
        }

        public static bool TryGet<T>(object o, string member, out T value)
        {
            var v = Get(o, member);
            if (v is T tv) { value = tv; return true; }
            value = default;
            return false;
        }

        /// <summary>Call an instance method by name with matching argument count. Returns (found, result).</summary>
        public static bool TryCall(object o, string method, out object result, params object[] args)
        {
            result = null;
            if (o == null) return false;
            foreach (var m in o.GetType().GetMethods(Inst))
            {
                if (m.Name != method || m.GetParameters().Length != args.Length) continue;
                try { result = m.Invoke(o, args); return true; }
                catch (TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); return true; }
                catch (ArgumentException) { /* try next overload */ }
            }
            return false;
        }

        /// <summary>Static method call by name (first overload whose parameters accept the args).</summary>
        public static bool TryCallStatic(Type t, string method, out object result, params object[] args)
        {
            result = null;
            if (t == null) return false;
            foreach (var m in t.GetMethods(Stat))
            {
                if (m.Name != method || m.GetParameters().Length != args.Length) continue;
                try { result = m.Invoke(null, args); return true; }
                catch (TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); return true; }
                catch (ArgumentException) { /* try next overload */ }
            }
            return false;
        }

        /// <summary>True when the method exists and its IL body is tiny without calling anything (a contract stub like
        /// "=> false" or "{ }"). Missing method => true.
        /// Unknown (no body info, e.g. IL2CPP) => false.</summary>
        public static bool IsStub(Type t, string method, params Type[] signature)
        {
            if (t == null) return true;
            MethodInfo m = null;
            try { m = signature != null && signature.Length > 0 ? t.GetMethod(method, Inst, null, signature, null) : t.GetMethod(method, Inst); }
            catch (AmbiguousMatchException)
            {
                foreach (var mm in t.GetMethods(Inst)) if (mm.Name == method) { m = mm; break; }
            }
            if (m == null) return true;
            try
            {
                var il = m.GetMethodBody()?.GetILAsByteArray();
                if (il == null || il.Length > 8) return false;
                // tiny but forwarding (e.g. "=> TryFire(p)": call / callvirt) is real code, not a stub
                foreach (var b in il) if (b == 0x28 || b == 0x6F) return false;
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Does the type itself declare the method (e.g. CharacterMotor.FixedUpdate exists only in the real motor)?</summary>
        public static bool Declares(Type t, string method) =>
            t != null && t.GetMethod(method, Inst | BindingFlags.DeclaredOnly) != null;

        /// <summary>First component of the named type on the object or its children.</summary>
        public static Component FindComponent(GameObject go, string typeName)
        {
            var t = FindType(typeName);
            if (go == null || t == null) return null;
            var c = go.GetComponent(t);
            return c != null ? c : go.GetComponentInChildren(t, true);
        }

        /// <summary>Component of the named type on the object or a parent (null when the type is absent).</summary>
        public static Component FindComponentInParent(Component c, string typeName)
        {
            var t = FindType(typeName);
            if (c == null || t == null) return null;
            return c.GetComponentInParent(t, true);
        }

        /// <summary>All scene objects of a named MonoBehaviour type (test-time only: allocates).</summary>
        public static List<MonoBehaviour> FindAll(string typeName)
        {
            var list = new List<MonoBehaviour>();
            var t = FindType(typeName);
            if (t == null) return list;
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
                if (mb != null && t.IsInstanceOfType(mb)) list.Add(mb);
            return list;
        }

        public static MonoBehaviour FindFirst(string typeName)
        {
            var l = FindAll(typeName);
            return l.Count > 0 ? l[0] : null;
        }

        /// <summary>Call a Kit factory "Kit.Method(Transform parent, Vector3 pos, Quaternion rot, ...)" by reflection.
        /// Remaining parameters are filled from <paramref name="extras"/> by type (in order), then defaults.
        /// Returns the created GameObject (null if the kit/method doesn't exist or threw).</summary>
        public static GameObject CallKit(string kitType, string method, Transform parent, Vector3 pos, Quaternion rot, params object[] extras)
        {
            var t = FindType(kitType);
            if (t == null) return null;
            foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (m.Name != method) continue;
                var ps = m.GetParameters();
                if (ps.Length < 2 || ps[0].ParameterType != typeof(Transform) || ps[1].ParameterType != typeof(Vector3)) continue;
                var args = new object[ps.Length];
                args[0] = parent; args[1] = pos;
                var used = new bool[extras.Length];
                for (int i = 2; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    if (i == 2 && (pt == typeof(Quaternion) || pt == typeof(Quaternion?))) { args[i] = rot; continue; }
                    bool filled = false;
                    for (int k = 0; k < extras.Length; k++)
                    {
                        if (used[k] || extras[k] == null) continue;
                        if (pt.IsInstanceOfType(extras[k])) { args[i] = extras[k]; used[k] = true; filled = true; break; }
                        if (pt == typeof(float) && extras[k] is int iv) { args[i] = (float)iv; used[k] = true; filled = true; break; }
                    }
                    if (filled) continue;
                    if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                    else args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
                }
                try
                {
                    var r = m.Invoke(null, args);
                    if (r is GameObject go) return go;
                    if (r is Component c) return c.gameObject;
                    return null;
                }
                catch (Exception e) { Debug.LogWarning("[BOT] kit " + kitType + "." + method + " threw: " + (e.InnerException ?? e).Message); return null; }
            }
            return null;
        }
    }
}
