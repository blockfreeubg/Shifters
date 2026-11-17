// =====================================================================================
//  SHIFTERS - SessionPrefs.cs   [owned by the CORE_SESSION agent]
//  PlayerPrefs persistence for GameSession: settings JSON, run JSON, level completion bitmask
//  and best times. All reads are defensive (corrupt/missing data => defaults, never throws).
//
//  Keys:  "shifters_settings"  SettingsData JSON
//         "shifters_run"       SavedRun JSON (team + next level + stats)
//         "shifters_completed" int bitmask (bit i = level i completed)
//         "shifters_best_<i>"  float best completion time (seconds) of level i
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    /// <summary>Serialized run (JsonUtility). JsonUtility can't store null class fields, so weapon ownership is kept
    /// in <see cref="WeaponSlot"/> and every other member's Weapon is nulled again on load.</summary>
    [Serializable]
    public class SavedRun
    {
        public int Version = 1;
        public TeamData Team;
        public int LevelIndex;          // level to (re)start when continuing
        public int WeaponSlot = -1;     // member that owns the gun (-1 none)
        public RunStats Stats = new RunStats();
        public string SavedAtUtc = "";
    }

    public static class SessionPrefs
    {
        public const string SettingsKey = "shifters_settings";
        public const string RunKey = "shifters_run";
        public const string CompletedKey = "shifters_completed";
        public const string BestTimePrefix = "shifters_best_";

        // ------------------------------------------------------------------ settings
        public static SettingsData LoadSettings()
        {
            var s = new SettingsData();
            string json = PlayerPrefs.GetString(SettingsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, s); }
                catch (Exception e) { Debug.LogWarning("[GameSession] settings JSON unreadable, using defaults: " + e.Message); s = new SettingsData(); }
            }
            Sanitize(s);
            return s;
        }

        public static void SaveSettings(SettingsData s)
        {
            if (s == null) return;
            Sanitize(s);
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        /// <summary>Clamp every setting into its valid range (also fixes NaN from hand-edited prefs).</summary>
        public static void Sanitize(SettingsData s)
        {
            s.MasterVolume = Clamp01Safe(s.MasterVolume, 0.8f);
            s.MusicVolume = Clamp01Safe(s.MusicVolume, 0.55f);
            s.SfxVolume = Clamp01Safe(s.SfxVolume, 0.8f);
            s.MouseSensitivity = float.IsNaN(s.MouseSensitivity) ? 1f : Mathf.Clamp(s.MouseSensitivity, 0.2f, 3f);
            s.Fov = float.IsNaN(s.Fov) ? 60f : Mathf.Clamp(s.Fov, 50f, 90f);
            s.Quality = Mathf.Clamp(s.Quality, 0, 1);
            // campaign wave (W0) settings; old JSON without them keeps the SettingsData defaults (FromJsonOverwrite)
            s.StoryMode = Mathf.Clamp(s.StoryMode, 0, 2);
            s.Heckles = Mathf.Clamp(s.Heckles, 0, 2);
            s.SubtitleSpeed = float.IsNaN(s.SubtitleSpeed) ? 1f : Mathf.Clamp(s.SubtitleSpeed, 0.5f, 2f);
            s.PartyPlayers = Mathf.Clamp(s.PartyPlayers, 1, 3);
        }

        static float Clamp01Safe(float v, float fallback) => float.IsNaN(v) ? fallback : Mathf.Clamp01(v);

        // ------------------------------------------------------------------ run
        public static bool HasRun => PlayerPrefs.HasKey(RunKey);

        public static void SaveRun(TeamData team, int levelIndex, RunStats stats)
        {
            if (team == null || team.Members == null) return;
            var run = new SavedRun
            {
                Team = team,
                LevelIndex = Mathf.Max(0, levelIndex),
                Stats = stats ?? new RunStats(),
                SavedAtUtc = DateTime.UtcNow.ToString("u"),
            };
            for (int i = 0; i < team.Members.Length; i++)
                if (team.Members[i] != null && team.Members[i].HasWeapon) { run.WeaponSlot = i; break; }
            PlayerPrefs.SetString(RunKey, JsonUtility.ToJson(run));
            PlayerPrefs.Save();
        }

        /// <summary>Parse the saved run. Returns null if missing or corrupt (corrupt data is deleted).</summary>
        public static SavedRun LoadRun()
        {
            string json = PlayerPrefs.GetString(RunKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            SavedRun run = null;
            try { run = JsonUtility.FromJson<SavedRun>(json); }
            catch (Exception e) { Debug.LogWarning("[GameSession] saved run unreadable, discarding: " + e.Message); }
            if (run == null || run.Team == null || run.Team.Members == null || run.Team.Members.Length != 3 || Array.IndexOf(run.Team.Members, null) >= 0)
            {
                if (run != null) Debug.LogWarning("[GameSession] saved run incomplete, discarding.");
                DeleteRun();
                return null;
            }
            for (int i = 0; i < run.Team.Members.Length; i++)
                if (i != run.WeaponSlot) run.Team.Members[i].Weapon = null;
            if (run.Stats == null) run.Stats = new RunStats();
            run.LevelIndex = Mathf.Clamp(run.LevelIndex, 0, Mathf.Max(0, LevelCatalog.Count - 1));
            return run;
        }

        public static void DeleteRun()
        {
            PlayerPrefs.DeleteKey(RunKey);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ completion / best times
        public static bool IsCompleted(int index) => index >= 0 && index < 31 && (PlayerPrefs.GetInt(CompletedKey, 0) & (1 << index)) != 0;

        public static void MarkCompleted(int index)
        {
            if (index < 0 || index >= 31) return;
            PlayerPrefs.SetInt(CompletedKey, PlayerPrefs.GetInt(CompletedKey, 0) | (1 << index));
            PlayerPrefs.Save();
        }

        /// <summary>Best completion time in seconds, or -1 if never completed.</summary>
        public static float BestTime(int index) => PlayerPrefs.GetFloat(BestTimePrefix + index, -1f);

        /// <summary>Records the time if it beats the stored best. Returns true for a new record.</summary>
        public static bool SubmitTime(int index, float seconds)
        {
            if (index < 0 || seconds <= 0f || float.IsNaN(seconds)) return false;
            float best = BestTime(index);
            if (best > 0f && best <= seconds) return false;
            PlayerPrefs.SetFloat(BestTimePrefix + index, seconds);
            PlayerPrefs.Save();
            return true;
        }

        // ------------------------------------------------------------------ test backup (seams, campaign wave)
        // PlayerPrefs cannot enumerate keys: BackupAll covers the keys above (best times for every catalog index + 8),
        // a few reserved campaign-wave names, and every key registered with RegisterBackupKey (campaign / story / ui
        // modules register theirs once, e.g. from a static constructor or RuntimeInitializeOnLoadMethod).
        static readonly System.Collections.Generic.List<string> _extraKeys = new System.Collections.Generic.List<string>
        {
            "shifters_campaign", "shifters_campaign_save", "shifters_campaign_settings", "shifters_exhibits", "shifters_story_seen",
            "shifters_tutorials", "shifters_freeplay", "shifters_party", "shifters_teamcodes",
        };
        enum PrefKind { String, Int, Float }
        struct PrefValue { public string Key; public PrefKind Kind; public string S; public int I; public float F; }
        static System.Collections.Generic.List<PrefValue> _backup;
        static System.Collections.Generic.List<string> _backupKeys;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBackupStatics() { _backup = null; _backupKeys = null; }   // the key registry is config and survives

        /// <summary>Adds a PlayerPrefs key that BackupAll/RestoreAll must cover (idempotent).</summary>
        public static void RegisterBackupKey(string key)
        {
            if (!string.IsNullOrEmpty(key) && !_extraKeys.Contains(key)) _extraKeys.Add(key);
        }

        /// <summary>Every key BackupAll covers.</summary>
        public static System.Collections.Generic.List<string> BackupKeys()
        {
            var keys = new System.Collections.Generic.List<string> { SettingsKey, RunKey, CompletedKey };
            for (int i = 0; i < LevelCatalog.Count + 8; i++) keys.Add(BestTimePrefix + i);
            for (int i = 0; i < _extraKeys.Count; i++) if (!keys.Contains(_extraKeys[i])) keys.Add(_extraKeys[i]);
            return keys;
        }

        /// <summary>True between BackupAll and RestoreAll.</summary>
        public static bool HasBackup => _backup != null;

        /// <summary>Tests: snapshot every SHIFTERS PlayerPrefs key (value + type; absent keys are remembered as absent).
        /// A second call while a backup is held is ignored (the first, untouched state wins). Returns the number of present keys.</summary>
        public static int BackupAll()
        {
            if (_backup != null) return _backup.Count;
            _backup = new System.Collections.Generic.List<PrefValue>();
            _backupKeys = BackupKeys();
            const string sentinel = "\u0001__shifters_none__";
            const int intSentinel = int.MinValue + 7;
            foreach (var k in _backupKeys)
            {
                if (!PlayerPrefs.HasKey(k)) continue;
                var v = new PrefValue { Key = k };
                string s = PlayerPrefs.GetString(k, sentinel);
                if (s != sentinel) { v.Kind = PrefKind.String; v.S = s; }
                else
                {
                    int iv = PlayerPrefs.GetInt(k, intSentinel);
                    if (iv != intSentinel) { v.Kind = PrefKind.Int; v.I = iv; }
                    else { v.Kind = PrefKind.Float; v.F = PlayerPrefs.GetFloat(k, 0f); }
                }
                _backup.Add(v);
            }
            return _backup.Count;
        }

        /// <summary>Tests: restores the BackupAll snapshot exactly (keys that did not exist are deleted again) and drops it.
        /// No backup held = no-op (returns false).</summary>
        public static bool RestoreAll()
        {
            if (_backup == null) return false;
            foreach (var k in _backupKeys) PlayerPrefs.DeleteKey(k);
            foreach (var v in _backup)
            {
                switch (v.Kind)
                {
                    case PrefKind.String: PlayerPrefs.SetString(v.Key, v.S); break;
                    case PrefKind.Int: PlayerPrefs.SetInt(v.Key, v.I); break;
                    default: PlayerPrefs.SetFloat(v.Key, v.F); break;
                }
            }
            PlayerPrefs.Save();
            _backup = null;
            _backupKeys = null;
            return true;
        }

        /// <summary>Wipe all SHIFTERS progress (completion, best times, run). Settings are kept.</summary>
        public static void ClearProgress()
        {
            PlayerPrefs.DeleteKey(CompletedKey);
            PlayerPrefs.DeleteKey(RunKey);
            for (int i = 0; i < LevelCatalog.Count; i++) PlayerPrefs.DeleteKey(BestTimePrefix + i);
            PlayerPrefs.Save();
        }
    }
}
