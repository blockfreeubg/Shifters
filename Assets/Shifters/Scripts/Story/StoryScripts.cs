// =====================================================================================
//  SHIFTERS - StoryScripts.cs   [story]
//  Registry of story scripts by id. The built-in campaign scripts (C00..C16, act cards, the §7.2 beat
//  lines, L1-L5 broadcasts) are defined in StoryText.*.cs and registered lazily on first use.
//  Other modules may Define their own (or override one) at any time; Alias maps a beat id to a script
//  id (e.g. "ch03.outro" -> "C04"). StoryPlayer plays a beat when Exists(beatId).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class StoryScripts
    {
        static readonly Dictionary<string, Action<StoryScriptBuilder>> _defs = new Dictionary<string, Action<StoryScriptBuilder>>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, StoryScript> _built = new Dictionary<string, StoryScript>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> _alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> _order = new List<string>();
        static bool _defaults;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _defs.Clear(); _built.Clear(); _alias.Clear(); _order.Clear(); _defaults = false; }

        static void EnsureDefaults()
        {
            if (_defaults) return;
            _defaults = true;
            try { StoryText.RegisterAll(); }
            catch (Exception e) { Debug.LogError("[Story] default scripts failed: " + e); }
        }

        /// <summary>Define (or replace) a script. The builder runs lazily the first time the script is needed.</summary>
        public static void Define(string id, Action<StoryScriptBuilder> build)
        {
            if (string.IsNullOrEmpty(id) || build == null) return;
            EnsureDefaults();
            if (!_defs.ContainsKey(id)) _order.Add(id);
            _defs[id] = build;
            _built.Remove(id);
        }

        /// <summary>Map a beat id onto a script id ("ch03.outro" -> "C04").</summary>
        public static void Alias(string beatId, string scriptId)
        {
            if (string.IsNullOrEmpty(beatId) || string.IsNullOrEmpty(scriptId)) return;
            EnsureDefaults();
            _alias[beatId] = scriptId;
        }

        public static string Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            EnsureDefaults();
            return _alias.TryGetValue(id, out var to) ? to : id;
        }

        public static bool Exists(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureDefaults();
            return _defs.ContainsKey(Resolve(id));
        }

        /// <summary>The built script (aliases resolved), or null.</summary>
        public static StoryScript Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureDefaults();
            id = Resolve(id);
            if (_built.TryGetValue(id, out var s)) return s;
            if (!_defs.TryGetValue(id, out var def)) return null;
            s = new StoryScript { Id = id };
            try { def(new StoryScriptBuilder(s)); }
            catch (Exception e) { Debug.LogError("[Story] building script " + id + " failed: " + e); }
            _built[id] = s;
            return s;
        }

        /// <summary>Every defined script id in definition order (tests).</summary>
        public static IReadOnlyList<string> Ids { get { EnsureDefaults(); return _order; } }
        public static IEnumerable<KeyValuePair<string, string>> Aliases { get { EnsureDefaults(); return _alias; } }

        /// <summary>A one-off script built now (not registered): barks, decrees.</summary>
        public static StoryScript Inline(string id, Action<StoryScriptBuilder> build)
        {
            var s = new StoryScript { Id = id };
            build?.Invoke(new StoryScriptBuilder(s));
            return s;
        }
    }
}
