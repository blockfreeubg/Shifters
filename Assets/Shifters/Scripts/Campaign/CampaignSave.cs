// =====================================================================================
//  SHIFTERS - CampaignSave.cs   [owned by the CAMPAIGN agent]
//  Campaign save (CAMPAIGN.md §15.1): JSON v1 under PlayerPrefs key "shifters_campaign",
//  written through an ICampaignStore (PlayerPrefsStore calls PlayerPrefs.Save() after every
//  write for the WebGL IndexedDB flush; MemoryStore for tests). A version mismatch / corrupt
//  JSON discards the save ("HR LOST YOUR FILE. (THEY DO THAT.)"). The team itself stays in
//  "shifters_run" (SessionPrefs).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public interface ICampaignStore { string Load(); void Save(string json); void Delete(); }

    public sealed class PlayerPrefsStore : ICampaignStore
    {
        public readonly string Key;
        public PlayerPrefsStore(string key = CampaignSave.Key) { Key = key; }
        public string Load() => PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        public void Save(string json) { PlayerPrefs.SetString(Key, json ?? ""); PlayerPrefs.Save(); }
        public void Delete() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }

    public sealed class MemoryStore : ICampaignStore
    {
        public string Json;
        public int Saves, Loads, Deletes;
        public MemoryStore(string json = null) { Json = json; }
        public string Load() { Loads++; return Json; }
        public void Save(string json) { Saves++; Json = json; }
        public void Delete() { Deletes++; Json = null; }
    }

    [Serializable]
    public class CampaignLostEntry { public string name, cause, chapter, clone; }

    /// <summary>JSON v1 (§15.1): {v, chapter, done[], seen[], meter, exhibits[], lost[{name, cause, chapter, clone}],
    /// violations, time, completed} + additive: hr (HR interventions), started (chapter starts), lastSeed.</summary>
    [Serializable]
    public class CampaignSaveData
    {
        public int v = CampaignSave.Version;
        public string chapter = "";             // chapter to CONTINUE at ("" = nothing to continue)
        public string[] done = new string[0];   // completed chapter ids (story order not guaranteed)
        public string[] seen = new string[0];   // story script ids seen
        public float meter;                     // world-color meter at the start of `chapter`
        public string[] exhibits = new string[0];
        public CampaignLostEntry[] lost = new CampaignLostEntry[0];
        public int violations;
        public float time;                      // campaign play seconds
        public bool completed;                  // the campaign has been completed at least once (rainbow trim)
        public int hr;                          // HR INTERVENTIONs used
        public int lastSeed;                    // team seed of this campaign (info)
        public string[] thrownNames = new string[0];   // HR FILE "MOST THROWN" (parallel to thrownCounts)
        public int[] thrownCounts = new int[0];
        public string president = "";           // HR FILE "FIRST PRESIDENT" (set by CH14)

        public bool HasChapter => !string.IsNullOrEmpty(chapter);
        public bool IsDone(string id) => Array.IndexOf(done, id) >= 0;
        public bool HasExhibit(string id) => Array.IndexOf(exhibits, id) >= 0;
        public bool HasSeen(string id) => Array.IndexOf(seen, id) >= 0;

        public void Sanitize()
        {
            if (chapter == null || (chapter.Length > 0 && CampaignCatalog.Find(chapter) == null)) chapter = "";
            done = done ?? new string[0];
            seen = seen ?? new string[0];
            exhibits = exhibits ?? new string[0];
            lost = lost ?? new CampaignLostEntry[0];
            thrownNames = thrownNames ?? new string[0];
            thrownCounts = thrownCounts ?? new int[0];
            if (thrownCounts.Length != thrownNames.Length) { var nc = new int[thrownNames.Length]; Array.Copy(thrownCounts, nc, Mathf.Min(thrownCounts.Length, nc.Length)); thrownCounts = nc; }
            president = president ?? "";
            if (float.IsNaN(meter) || float.IsInfinity(meter)) meter = 0f;
            meter = Mathf.Clamp(meter, 0f, CampaignCatalog.EpilogueMeter);
            violations = Mathf.Max(0, violations);
            hr = Mathf.Max(0, hr);
            if (float.IsNaN(time) || time < 0f) time = 0f;
        }

        public CampaignSaveData Clone() => JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(this));
    }

    public static class CampaignSave
    {
        public const int Version = 1;
        public const string Key = "shifters_campaign";
        public const string LostFileToast = "HR LOST YOUR FILE. (THEY DO THAT.)";
        /// <summary>Lost entries kept in the save (oldest dropped; keeps the JSON well under 4 KB).</summary>
        public const int MaxLost = 24;
        public const int MaxSeen = 64;

        static ICampaignStore _store;
        /// <summary>The store used by CampaignDirector. Default PlayerPrefsStore; tests set a MemoryStore.</summary>
        public static ICampaignStore Store { get => _store ?? (_store = new PlayerPrefsStore()); set => _store = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _store = null; SessionPrefs.RegisterBackupKey(Key); }

        public static string ToJson(CampaignSaveData d)
        {
            if (d == null) return null;
            d.v = Version;
            return JsonUtility.ToJson(d);
        }

        /// <summary>Parse JSON. Null for empty input. versionOk false (and null) for corrupt JSON or v != Version.</summary>
        public static CampaignSaveData FromJson(string json, out bool versionOk)
        {
            versionOk = true;
            if (string.IsNullOrEmpty(json)) return null;
            CampaignSaveData d;
            try { d = JsonUtility.FromJson<CampaignSaveData>(json); }
            catch (Exception) { versionOk = false; return null; }
            if (d == null || d.v != Version) { versionOk = false; return null; }
            d.Sanitize();
            return d;
        }

        /// <summary>Load from a store. Null if none. A version mismatch / corrupt save is DELETED and discarded = true.</summary>
        public static CampaignSaveData Load(ICampaignStore store, out bool discarded)
        {
            discarded = false;
            if (store == null) return null;
            string json;
            try { json = store.Load(); }
            catch (Exception e) { Debug.LogWarning("[CampaignSave] load failed: " + e.Message); return null; }
            var d = FromJson(json, out bool ok);
            if (!ok)
            {
                discarded = true;
                Debug.LogWarning("[CampaignSave] discarding campaign save (version mismatch or corrupt): " + Trunc(json));
                try { store.Delete(); } catch (Exception) { }
            }
            return d;
        }

        public static CampaignSaveData Load(out bool discarded) => Load(Store, out discarded);
        public static CampaignSaveData Load() => Load(Store, out _);

        public static void Save(ICampaignStore store, CampaignSaveData d)
        {
            if (store == null || d == null) return;
            Trim(d);
            try { store.Save(ToJson(d)); }
            catch (Exception e) { Debug.LogWarning("[CampaignSave] save failed: " + e.Message); }
        }

        public static void Save(CampaignSaveData d) => Save(Store, d);

        public static void Delete() { try { Store.Delete(); } catch (Exception) { } }

        /// <summary>True if the store holds a valid v1 save with a chapter to continue.</summary>
        public static bool Exists(ICampaignStore store)
        {
            try { var d = FromJson(store?.Load(), out _); return d != null && d.HasChapter; }
            catch (Exception) { return false; }
        }

        static void Trim(CampaignSaveData d)
        {
            if (d.lost != null && d.lost.Length > MaxLost)
            {
                var l = new CampaignLostEntry[MaxLost];
                Array.Copy(d.lost, d.lost.Length - MaxLost, l, 0, MaxLost);
                d.lost = l;
            }
            if (d.seen != null && d.seen.Length > MaxSeen)
            {
                var s = new string[MaxSeen];
                Array.Copy(d.seen, d.seen.Length - MaxSeen, s, 0, MaxSeen);
                d.seen = s;
            }
        }

        internal static string[] Add(string[] arr, string item)
        {
            if (string.IsNullOrEmpty(item)) return arr ?? new string[0];
            arr = arr ?? new string[0];
            if (Array.IndexOf(arr, item) >= 0) return arr;
            var n = new string[arr.Length + 1];
            Array.Copy(arr, n, arr.Length);
            n[arr.Length] = item;
            return n;
        }

        internal static CampaignLostEntry[] Add(CampaignLostEntry[] arr, CampaignLostEntry e)
        {
            var list = new List<CampaignLostEntry>(arr ?? new CampaignLostEntry[0]) { e };
            return list.ToArray();
        }

        static string Trunc(string s) => s == null ? "null" : s.Length > 120 ? s.Substring(0, 120) + "..." : s;
    }
}
