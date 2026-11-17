// =====================================================================================
//  SHIFTERS - KitCatalog.cs   (BUILDER module - runtime level framework)
//  Reflection bridge to the module kits (EnvKit, PuzzleKit, LevelKit, PerspectiveKit, EnemyKit,
//  WeaponKit + their public nested static classes such as EnvKit.Decor).
//    * TryInvoke / Spawn: call a kit factory by name with loosely-typed arguments (int->float, yaw->Quaternion,
//      GameObject->Component, ...). Missing trailing parameters use the factory's own defaults or sensible
//      synthesized values. Lets framework code (sandbox, passthroughs) compile before a kit exists.
//    * Discover / SpawnDefault: enumerate every placeable factory and build one default instance
//      (used by the kit gallery scene and the prefab generator).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class KitCatalog
    {
        /// <summary>Kit classes scanned by <see cref="Discover"/> (namespace Shifters).</summary>
        public static readonly string[] KitNames = { "EnvKit", "PuzzleKit", "LevelKit", "PerspectiveKit", "EnemyKit", "WeaponKit" };

        /// <summary>One placeable kit factory method.</summary>
        public sealed class Factory
        {
            public string Kit;        // e.g. "PuzzleKit" or "EnvKit.Decor"
            public string Name;       // method name
            public MethodInfo Method;
            public string Category;   // Puzzles, Props, Hazards, Environment, Enemies, Weapons, Level
            public string Id => Kit + "." + Name;
        }

        static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();
        static readonly HashSet<string> _warned = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _types.Clear(); _warned.Clear(); }

        // ------------------------------------------------------------------ type lookup
        /// <summary>Finds a type by short name ("PuzzleKit" = Shifters.PuzzleKit, "EnvKit.Decor" = nested) or full name.</summary>
        public static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_types.TryGetValue(name, out var cached)) return cached;
            Type found = null;
            string full = name.StartsWith("Shifters.", StringComparison.Ordinal) ? name : "Shifters." + name;
            string[] candidates = { full, full.Replace('.', '+').Replace("Shifters+", "Shifters."), name };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var c in candidates)
                {
                    try { found = asm.GetType(c, false); } catch { found = null; }
                    if (found != null) break;
                }
                if (found != null) break;
            }
            _types[name] = found;
            return found;
        }

        public static bool HasKit(string kit) => FindType(kit) != null;

        public static bool HasFactory(string kit, string method)
        {
            var t = FindType(kit);
            if (t == null) return false;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static)) if (m.Name == method) return true;
            return false;
        }

        /// <summary>Logs a warning once per key (missing kits are expected while modules are in progress).</summary>
        public static void WarnOnce(string key, string message)
        {
            if (_warned.Add(key)) Debug.LogWarning("[SHIFTERS/Builder] " + message);
        }

        // ------------------------------------------------------------------ flexible invocation
        /// <summary>Invokes kit.method with loosely-typed args. Picks the best matching overload. Returns false if the
        /// kit/method is missing, no overload binds, or the factory threw (error logged as a warning).</summary>
        public static bool TryInvoke(string kit, string method, out object result, params object[] args)
        {
            result = null;
            var t = FindType(kit);
            if (t == null) { WarnOnce(kit, kit + " not found (module not published yet?) - using fallbacks."); return false; }
            args = args ?? new object[0];
            MethodInfo best = null; object[] bestArgs = null; int bestScore = int.MinValue;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != method || m.IsGenericMethodDefinition) continue;
                if (TryBind(m, args, out var bound, out int score) && score > bestScore) { best = m; bestArgs = bound; bestScore = score; }
            }
            if (best == null) { WarnOnce(kit + "." + method + "/" + args.Length, kit + "." + method + " has no overload matching (" + Describe(args) + ")."); return false; }
            try { result = best.Invoke(null, bestArgs); return true; }
            catch (TargetInvocationException e)
            {
                var inner = e.InnerException ?? e;
                Debug.LogWarning("[SHIFTERS/Builder] " + kit + "." + method + " threw: " + inner.GetType().Name + ": " + inner.Message + "\n" + inner.StackTrace);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SHIFTERS/Builder] " + kit + "." + method + " invoke failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Invokes a factory and returns the GameObject it built (or null).</summary>
        public static GameObject Spawn(string kit, string method, params object[] args)
            => TryInvoke(kit, method, out var r, args) ? AsGameObject(r) : null;

        public static GameObject AsGameObject(object o)
        {
            if (o is GameObject g) return g;
            if (o is Component c) return c != null ? c.gameObject : null;
            return null;
        }

        static string Describe(object[] args)
        {
            var parts = new string[args.Length];
            for (int i = 0; i < args.Length; i++) parts[i] = args[i] == null ? "null" : args[i].GetType().Name;
            return string.Join(", ", parts);
        }

        static bool TryBind(MethodInfo m, object[] args, out object[] bound, out int score)
        {
            var ps = m.GetParameters();
            bound = null; score = 0;
            if (args.Length > ps.Length) return false;
            var result = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (i < args.Length)
                {
                    if (!TryConvert(args[i], p.ParameterType, out var v, out bool exact)) return false;
                    result[i] = v; score += exact ? 3 : 1;
                }
                else if (p.HasDefaultValue || p.IsOptional)
                {
                    var dv = p.DefaultValue;
                    result[i] = dv == DBNull.Value || dv == Type.Missing ? DefaultOf(p.ParameterType) : dv;
                }
                else if (p.GetCustomAttribute<ParamArrayAttribute>() != null)
                {
                    result[i] = Array.CreateInstance(p.ParameterType.GetElementType(), 0);
                }
                else
                {
                    // missing required trailing parameter: synthesize a sensible value, penalize the score
                    result[i] = Synthesize(p, m.Name, null);
                    score -= 4;
                }
            }
            score -= (ps.Length - args.Length); // prefer overloads that need fewer extra parameters
            bound = result;
            return true;
        }

        static object DefaultOf(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

        /// <summary>Loose conversion used by the binder.</summary>
        public static bool TryConvert(object a, Type t, out object v, out bool exact)
        {
            v = null; exact = false;
            var under = Nullable.GetUnderlyingType(t);
            if (a == null) { exact = true; return !t.IsValueType || under != null; }
            var target = under ?? t;
            if (target.IsInstanceOfType(a)) { v = a; exact = true; return true; }
            // numeric
            if (IsNumeric(a.GetType()) && IsNumeric(target) && !target.IsEnum)
            {
                if ((target == typeof(int) || target == typeof(long) || target == typeof(short)) && (a is float || a is double))
                {
                    double d = Convert.ToDouble(a);
                    if (Math.Abs(d - Math.Round(d)) > 1e-6) return false;
                }
                try { v = Convert.ChangeType(a, target, System.Globalization.CultureInfo.InvariantCulture); return true; } catch { return false; }
            }
            if (target.IsEnum)
            {
                if (a is string s) { try { v = Enum.Parse(target, s, true); return true; } catch { return false; } }
                if (IsNumeric(a.GetType())) { v = Enum.ToObject(target, Convert.ToInt32(a)); return true; }
                return false;
            }
            if (target == typeof(Quaternion) && IsNumeric(a.GetType())) { v = Quaternion.Euler(0f, Convert.ToSingle(a), 0f); return true; }
            if (target == typeof(Vector2) && a is Vector3 v3) { v = new Vector2(v3.x, v3.y); return true; }
            if (target == typeof(Vector3) && a is Vector2 v2) { v = (Vector3)v2; return true; }
            if (target == typeof(GameObject) && a is Component ca) { v = ca.gameObject; return true; }
            if (target == typeof(Transform) && a is GameObject ga) { v = ga.transform; return true; }
            if (target == typeof(Transform) && a is Component ct) { v = ct.transform; return true; }
            if (typeof(Component).IsAssignableFrom(target))
            {
                Component c = a is GameObject g ? g.GetComponent(target) : a is Component cc ? cc.GetComponent(target) : null;
                if (c != null) { v = c; return true; }
                return false;
            }
            if (target.IsArray && a is IList list && !(a is string))
            {
                var et = target.GetElementType();
                var arr = Array.CreateInstance(et, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    if (!TryConvert(list[i], et, out var e, out _)) return false;
                    arr.SetValue(e, i);
                }
                v = arr; return true;
            }
            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>) && a is IList src)
            {
                var et = target.GetGenericArguments()[0];
                var dst = (IList)Activator.CreateInstance(target);
                foreach (var item in src) { if (!TryConvert(item, et, out var e, out _)) return false; dst.Add(e); }
                v = dst; return true;
            }
            if (target == typeof(string)) { v = a.ToString(); return true; }
            return false;
        }

        static bool IsNumeric(Type t)
            => t == typeof(int) || t == typeof(float) || t == typeof(double) || t == typeof(long) || t == typeof(short) || t == typeof(byte);

        // ------------------------------------------------------------------ discovery (gallery + prefabs)
        static readonly string[] ExcludedWords =
        {
            "Manager", "Setup", "Apply", "Configure", "Register", "Init", "Wire", "Connect", "Link", "Find", "Get",
            "Clear", "Reset", "Destroy", "Remove", "Theme", "Mat", "Mesh", "Bind", "Attach", "Hook", "Rebuild", "Refresh",
            "Between" // [integrator] helpers that join two existing objects (PuzzleKit.CableBetween) are not placeable
        };

        /// <summary>All placeable factories: public static, first parameter Transform, returns GameObject/Component.</summary>
        public static List<Factory> Discover()
        {
            var list = new List<Factory>();
            foreach (var kit in KitNames)
            {
                var t = FindType(kit);
                if (t == null) continue;
                Collect(t, kit, list);
                foreach (var nested in t.GetNestedTypes(BindingFlags.Public))
                    if (nested.IsAbstract && nested.IsSealed) Collect(nested, kit + "." + nested.Name, list); // static class
            }
            return list;
        }

        static void Collect(Type t, string kitName, List<Factory> list)
        {
            var byName = new Dictionary<string, MethodInfo>();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName || m.IsGenericMethodDefinition) continue;
                if (!(m.ReturnType == typeof(GameObject) || typeof(Component).IsAssignableFrom(m.ReturnType))) continue;
                var ps = m.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(Transform)) continue;
                if (IsExcluded(m.Name)) continue;
                if (byName.TryGetValue(m.Name, out var prev) && RequiredCount(prev) <= RequiredCount(m)) continue;
                byName[m.Name] = m;
            }
            var names = new List<string>(byName.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (var n in names)
                list.Add(new Factory { Kit = kitName, Name = n, Method = byName[n], Category = CategoryFor(kitName, n) });
        }

        static int RequiredCount(MethodInfo m)
        {
            int n = 0;
            foreach (var p in m.GetParameters()) if (!p.HasDefaultValue && !p.IsOptional) n++;
            return n;
        }

        static bool IsExcluded(string name)
        {
            foreach (var w in ExcludedWords) if (name.StartsWith(w, StringComparison.Ordinal) || name.EndsWith(w, StringComparison.Ordinal)) return true;
            return false;
        }

        static readonly string[] HazardWords = { "Kill", "Laser", "Crusher", "Sweeper", "Spike", "Steam", "Vent", "Crumbl", "Wobbl", "Electric", "Acid", "Pit", "Hazard", "Bounce", "Foam", "Saw", "Fire", "Incinerat" };
        static readonly string[] PropWords = { "Crate", "Barrel", "Block", "Prop", "Breakable", "Ball", "Bucket", "Debris", "Box" };

        /// <summary>Prefab category for a factory (Puzzles, Props, Hazards, Environment, Enemies, Weapons, Level).</summary>
        public static string CategoryFor(string kit, string method)
        {
            if (kit.StartsWith("EnemyKit", StringComparison.Ordinal)) return "Enemies";
            if (kit.StartsWith("WeaponKit", StringComparison.Ordinal)) return "Weapons";
            if (kit.StartsWith("LevelKit", StringComparison.Ordinal) || kit.StartsWith("PerspectiveKit", StringComparison.Ordinal)) return "Level";
            if (ContainsAny(method, HazardWords) && !method.StartsWith("HazardStripe", StringComparison.Ordinal)) return "Hazards";
            if (kit.StartsWith("PuzzleKit", StringComparison.Ordinal)) return ContainsAny(method, PropWords) ? "Props" : "Puzzles";
            return "Environment";
        }

        static bool ContainsAny(string s, string[] words)
        {
            foreach (var w in words) if (s.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>Builds one default instance of the factory under parent (local origin). Returns the built object,
        /// or null with an error message.</summary>
        public static GameObject SpawnDefault(Factory f, Transform parent, out string error)
        {
            error = null;
            var ps = f.Method.GetParameters();
            var args = new object[ps.Length];
            args[0] = parent;
            for (int i = 1; i < ps.Length; i++) args[i] = Synthesize(ps[i], f.Name, parent);
            try
            {
                var r = f.Method.Invoke(null, args);
                var go = AsGameObject(r);
                if (go == null && parent.childCount == 0) error = "factory returned nothing";
                return go;
            }
            catch (TargetInvocationException e) { var inner = e.InnerException ?? e; error = inner.GetType().Name + ": " + inner.Message; }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
            return null;
        }

        /// <summary>Synthesizes a plausible argument for a factory parameter from its type and name.</summary>
        public static object Synthesize(ParameterInfo p, string method, Transform parent)
        {
            var t = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
            string n = (p.Name ?? "").ToLowerInvariant();
            string mn = method ?? "";
            bool hasDefault = p.HasDefaultValue && p.DefaultValue != null && p.DefaultValue != DBNull.Value && p.DefaultValue != Type.Missing;
            if (hasDefault) return p.DefaultValue;

            if (t == typeof(Transform)) return parent;
            if (t == typeof(Vector3))
            {
                if (Has(n, "size", "extent", "dimension", "scale", "bounds"))
                {
                    if (Has(mn, "Wall")) return new Vector3(4f, 3f, 0.3f);
                    if (Has(mn, "Floor", "Platform", "Slab", "Tile")) return new Vector3(4f, 0.5f, 4f);
                    if (Has(mn, "Zone", "Trigger", "Volume", "Pit", "Hint", "Tutorial", "Area", "Acid", "Void")) return new Vector3(4f, 3f, 4f);
                    if (Has(mn, "Sign", "Poster", "Panel")) return new Vector3(2f, 1f, 0.1f);
                    return new Vector3(2f, 1f, 2f);
                }
                if (Has(n, "to", "end", "target", "dest", "b")) return new Vector3(0f, 0f, 4f);
                if (Has(n, "dir", "direction", "axis", "forward", "normal")) return Vector3.forward;
                if (Has(n, "offset", "travel", "delta", "move", "rise", "lift")) return new Vector3(0f, 2f, 0f);
                return Vector3.zero;
            }
            if (t == typeof(Vector2)) return Has(n, "size") ? new Vector2(2f, 1f) : Vector2.zero;
            if (t == typeof(Quaternion)) return Quaternion.identity;
            if (t == typeof(float) || t == typeof(double))
            {
                float f = 1f;
                if (Has(n, "max")) f = 120f;
                else if (Has(n, "kg", "mass", "weight", "required", "min")) f = 40f;
                else if (Has(n, "width")) f = 2.6f;
                else if (Has(n, "height", "rise", "lift")) f = 2f;
                else if (Has(n, "length", "distance", "span", "route", "run")) f = 6f;
                else if (Has(n, "radius")) f = 4f;
                else if (Has(n, "thick")) f = 0.3f;
                else if (Has(n, "speed")) f = 2f;
                else if (Has(n, "time", "duration", "seconds", "window", "delay", "period", "interval", "cycle")) f = 4f;
                else if (Has(n, "angle", "yaw", "pitch")) f = 0f;
                else if (Has(n, "damage")) f = 10f;
                else if (Has(n, "iq", "str", "strength", "spd")) f = 7f;
                return t == typeof(double) ? (object)(double)f : f;
            }
            if (t == typeof(int))
            {
                if (Has(n, "iq", "str", "strength", "spd", "level")) return 7;
                if (Has(n, "kg", "mass", "weight")) return 40;
                if (Has(n, "count", "segments", "steps", "num")) return 4;
                return 1;
            }
            if (t == typeof(bool)) return false;
            if (t == typeof(string))
            {
                if (Has(n, "id", "key", "cell")) return "gallery_" + mn.ToLowerInvariant();
                if (Has(n, "title", "header")) return "GALLERY";
                if (Has(n, "real")) return "This is the real hint.";
                if (Has(n, "vague")) return "This is a vague hint.";
                return mn.ToUpperInvariant();
            }
            if (t == typeof(string[])) return Has(n, "key") ? new[] { "E" } : new[] { "SAMPLE" };
            if (t == typeof(Vector3[]))
            {
                if (Has(n, "waypoint", "patrol"))
                    return new[] { new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f), new Vector3(2f, 0f, 2f), new Vector3(-2f, 0f, 2f) };
                return new[] { new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, 1f), new Vector3(3f, 0f, 3f) };
            }
            if (t == typeof(List<Vector3>)) return new List<Vector3> { new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, 3f) };
            if (t == typeof(Color)) return new Color(1f, 0.54f, 0.12f);
            if (t == typeof(LevelTheme)) return Kit.Theme;
            if (t.IsEnum)
            {
                if (p.HasDefaultValue && p.DefaultValue != null && p.DefaultValue != DBNull.Value) return p.DefaultValue;
                var values = Enum.GetValues(t);
                return values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(t);
            }
            if (t == typeof(WeaponData)) return WeaponGenerator.Generate(new System.Random(7));
            if (t.IsValueType) return Activator.CreateInstance(t);
            return null;
        }

        static bool Has(string s, params string[] words)
        {
            string l = s.ToLowerInvariant();
            foreach (var w in words)
            {
                if (w.Length <= 2) { if (l == w) return true; }
                else if (l.Contains(w)) return true;
            }
            return false;
        }
    }
}
