// =====================================================================================
//  SHIFTERS - BotPrefsGuard.cs   [BOTS module - campaign wave, CAMPAIGN §22 test hygiene]
//  Every runner (TeamBot.Run / RunSeeds, SmokeTests, CampaignBot) backs up every shifters_* PlayerPrefs key at its
//  outermost start and restores it at the end, then ASSERTS the prefs equal the backup (ctx.Check).
//  PlayerPrefs cannot enumerate keys, so the key set = every const/static string in the Shifters assembly that starts
//  with "shifters_" (SessionPrefs.SettingsKey/RunKey/CompletedKey, the campaign's save key ...). A constant ending in
//  '_' is a PREFIX ("shifters_best_"): keys prefix+0..63 are covered. Extra keys: BotPrefsGuard.ExtraKeys.Add(...).
//  Nested Push/Pop (RunSeeds -> Run) only act at depth 0.
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class BotPrefsGuard
    {
        /// <summary>Keys guarded in addition to the discovered ones.</summary>
        public static readonly List<string> ExtraKeys = new List<string> { "shifters_campaign", "shifters_settings", "shifters_run", "shifters_completed" };
        public const int PrefixRange = 64;

        enum Kind { None, Str, Int, Flt }
        struct Entry { public Kind Kind; public string S; public int I; public float F; }

        static Dictionary<string, Entry> _backup;
        static int _depth;
        static List<string> _keys;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _backup = null; _depth = 0; _keys = null; }

        public static bool Active => _depth > 0;

        /// <summary>Enter a runner (every Push needs exactly one Pop). Returns true for the outermost.</summary>
        public static bool Push(DevPlayContext ctx = null)
        {
            _depth++;
            if (_depth > 1) return false;
            _backup = Capture();
            int present = 0;
            foreach (var kv in _backup) if (kv.Value.Kind != Kind.None) present++;
            Debug.Log("[DEVPLAY] [BOT] PlayerPrefs backed up: " + Keys().Count + " shifters_* keys checked, " + present + " present");
            return true;
        }

        /// <summary>Leave a runner: the outermost restores the backup and asserts the prefs match it.</summary>
        public static bool Pop(DevPlayContext ctx = null)
        {
            if (_depth <= 0) return true;
            _depth--;
            if (_depth > 0 || _backup == null) return true;
            int changed = 0;
            var now = Capture();
            foreach (var kv in _backup) if (!Same(kv.Value, now[kv.Key])) changed++;
            Restore(_backup);
            var after = Capture();
            var bad = new List<string>();
            foreach (var kv in _backup) if (!Same(kv.Value, after[kv.Key])) bad.Add(kv.Key);
            _backup = null;
            string msg = "[BOT] PlayerPrefs restored: shifters_* unchanged after the run (" + changed + " key(s) the run had written were put back"
                         + (bad.Count > 0 ? "; STILL DIFFERENT: " + string.Join(", ", bad) : "") + ")";
            if (ctx != null) ctx.Check(bad.Count == 0, msg);
            else if (bad.Count > 0) Debug.LogError("[DEVPLAY] " + msg); else Debug.Log("[DEVPLAY] " + msg);
            return bad.Count == 0;
        }

        /// <summary>The guarded key list (discovered once).</summary>
        public static List<string> Keys()
        {
            if (_keys != null) return _keys;
            var set = new HashSet<string>();
            var prefixes = new HashSet<string> { "shifters_best_" };
            foreach (var k in ExtraKeys) if (!string.IsNullOrEmpty(k)) set.Add(k);
            try
            {
                foreach (var t in typeof(BotPrefsGuard).Assembly.GetTypes())
                {
                    if (t.Namespace == null || !t.Namespace.StartsWith("Shifters")) continue;
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType != typeof(string) || t.ContainsGenericParameters) continue;
                        if (!f.IsLiteral && !f.IsInitOnly) continue;
                        string v = null;
                        try { v = f.IsLiteral ? f.GetRawConstantValue() as string : f.GetValue(null) as string; } catch (Exception) { }
                        if (string.IsNullOrEmpty(v) || !v.StartsWith("shifters_") || v.IndexOf(' ') >= 0 || v.Length > 64) continue;
                        if (v.EndsWith("_")) prefixes.Add(v); else set.Add(v);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[BOT] BotPrefsGuard key discovery: " + e.Message); }
            foreach (var p in prefixes) for (int i = 0; i < PrefixRange; i++) set.Add(p + i);
            _keys = new List<string>(set);
            _keys.Sort(StringComparer.Ordinal);
            return _keys;
        }

        static Dictionary<string, Entry> Capture()
        {
            var d = new Dictionary<string, Entry>();
            foreach (var k in Keys()) d[k] = Read(k);
            return d;
        }

        const string StrSentinel = "\u0001__no__";
        const int IntSentinel = int.MinValue + 7;

        static Entry Read(string k)
        {
            if (!PlayerPrefs.HasKey(k)) return new Entry { Kind = Kind.None };
            string s = PlayerPrefs.GetString(k, StrSentinel);
            if (s != StrSentinel) return new Entry { Kind = Kind.Str, S = s };
            int i = PlayerPrefs.GetInt(k, IntSentinel);
            if (i != IntSentinel) return new Entry { Kind = Kind.Int, I = i };
            return new Entry { Kind = Kind.Flt, F = PlayerPrefs.GetFloat(k, 0f) };
        }

        static bool Same(Entry a, Entry b) =>
            a.Kind == b.Kind && (a.Kind == Kind.None || (a.Kind == Kind.Str && a.S == b.S) || (a.Kind == Kind.Int && a.I == b.I)
                                 || (a.Kind == Kind.Flt && a.F.Equals(b.F)));

        static void Restore(Dictionary<string, Entry> d)
        {
            foreach (var kv in d)
            {
                var e = kv.Value;
                switch (e.Kind)
                {
                    case Kind.None: if (PlayerPrefs.HasKey(kv.Key)) PlayerPrefs.DeleteKey(kv.Key); break;
                    case Kind.Str: PlayerPrefs.SetString(kv.Key, e.S); break;
                    case Kind.Int: PlayerPrefs.SetInt(kv.Key, e.I); break;
                    case Kind.Flt: PlayerPrefs.SetFloat(kv.Key, e.F); break;
                }
            }
            PlayerPrefs.Save();
        }
    }
}
