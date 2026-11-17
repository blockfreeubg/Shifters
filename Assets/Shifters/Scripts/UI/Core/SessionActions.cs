// =====================================================================================
//  SHIFTERS - SessionActions.cs   (UI agent)
//  The single place where UI buttons call into GameSession (and optional APIs that may or
//  may not exist yet: ContinueRun, HasSavedRun, ShowVictory, TutorialManager.RestartTutorial).
//  Every action is recorded in Log (test hook). DryRun = true records scene-changing actions
//  without executing them (used by UI tests so buttons can be verified without loading scenes).
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class SessionActions
    {
        /// <summary>Tests: record scene-changing actions without executing them.</summary>
        public static bool DryRun;
        /// <summary>Recorded actions ("NewRun", "LoadLevel:2", "RestartLevel"...), newest last.</summary>
        public static readonly List<string> Log = new List<string>();
        static readonly SettingsData _fallbackSettings = new SettingsData();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { DryRun = false; Log.Clear(); }

        public static string Last => Log.Count > 0 ? Log[Log.Count - 1] : "";
        public static bool Logged(string action) => Log.Contains(action);

        static GameSession S => GameSession.Instance;

        static void Record(string a)
        {
            Log.Add(a);
            if (Log.Count > 128) Log.RemoveAt(0);
        }

        // ------------------------------------------------------------------ actions
        /// <summary>Fresh random team (not a scene change: always executed).</summary>
        public static void NewRun() { Record("NewRun"); S?.NewRun(0); }
        public static void EnsureTeam() { S?.EnsureTeam(); }

        public static void LoadLevel(int index)
        {
            Record("LoadLevel:" + index);
            if (DryRun || S == null) return;
            S.EnsureTeam();
            S.LoadLevel(index);
        }

        // ------------------------------------------------------------------ campaign actions (CampaignLink; fall back to the classic flow)
        /// <summary>CAMPAIGN -> YOUR NEW HIRES -> START: CampaignDirector.StartNew(seed), else the classic LoadLevel(0).</summary>
        public static void CampaignNew(int seed)
        {
            Record("CampaignNew:" + seed);
            if (DryRun) return;
            if (CampaignLink.StartNew(-1)) return;   // -1 = keep the team YOUR NEW HIRES shows (seed / TEAM CODE already applied)
            if (S == null) return;
            S.EnsureTeam();
            S.LoadLevel(0);
        }

        /// <summary>SHIFT SCHEDULE row: CampaignDirector.StartAt(id), else load the chapter's level directly.</summary>
        public static void CampaignAt(ChapterView c)
        {
            if (c == null) return;
            Record("CampaignAt:" + c.Id);
            if (DryRun) return;
            if (CampaignLink.StartAt(c.Id)) return;
            if (S == null || !c.Playable) return;
            S.EnsureTeam();
            S.LoadLevel(c.CatalogIndex);
        }

        /// <summary>FREE PLAY card: CampaignDirector.StartFreePlay(idx) (no story, full color), else LoadLevel(idx).</summary>
        public static void FreePlay(int catalogIndex)
        {
            Record("FreePlay:" + catalogIndex);
            if (DryRun) return;
            if (S != null && !S.HasRun) S.NewRun(0);
            if (CampaignLink.StartFreePlay(catalogIndex)) return;
            if (S == null) return;
            S.EnsureTeam();
            S.LoadLevel(catalogIndex);
        }

        /// <summary>CONTINUE with a campaign save: CampaignDirector.Continue(), else the classic Continue().</summary>
        public static void CampaignContinue()
        {
            Record("CampaignContinue");
            if (DryRun) return;
            if (CampaignLink.Continue()) return;
            Log.RemoveAt(Log.Count - 1);
            Continue();
        }

        /// <summary>Pause -> LET HR HANDLE IT: GameSession.RestartAtNextCheckpoint() (seams), else restart the checkpoint.</summary>
        public static void HRIntervention()
        {
            Record("HRIntervention");
            if (DryRun) return;
            if (CampaignLink.UseHR()) return;
            if (InvokeOptional("RestartAtNextCheckpoint")) return;
            S?.RestartFromCheckpoint();
        }

        /// <summary>New run with an explicit seed (TEAM CODE). 0 = random.</summary>
        public static void NewRun(int seed) { Record("NewRun:" + seed); S?.NewRun(seed); }

        public static void RestartLevel() { Record("RestartLevel"); if (!DryRun) S?.RestartLevel(); }
        public static void RestartCheckpoint() { Record("RestartCheckpoint"); if (!DryRun) S?.RestartFromCheckpoint(); }
        public static void NextLevel() { Record("NextLevel"); if (!DryRun) S?.NextLevel(); }
        public static void MainMenu() { Record("MainMenu"); if (!DryRun) S?.GoToMainMenu(); }
        public static void Quit() { Record("Quit"); if (!DryRun) S?.QuitGame(); }

        /// <summary>CONTINUE: GameSession.ContinueRun() when available, else load the first incomplete level.</summary>
        public static void Continue()
        {
            Record("Continue");
            if (DryRun || S == null) return;
            if (InvokeOptional("ContinueRun")) return;
            S.EnsureTeam();
            S.LoadLevel(FirstIncompleteLevel());
        }

        /// <summary>TutorialManager.RestartTutorial() if that module exists, else re-enable tutorials and load level 1.</summary>
        public static void RestartTutorial()
        {
            Record("RestartTutorial");
            if (DryRun) return;
            try
            {
                var t = Type.GetType("Shifters.TutorialManager");
                var m = t?.GetMethod("RestartTutorial", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (m != null) { m.Invoke(null, null); return; }
            }
            catch (Exception e) { Debug.LogWarning("[UI] RestartTutorial via TutorialManager failed: " + e.Message); }
            if (S == null) return;
            S.Settings.Tutorials = true;
            S.ApplySettings();
            S.SaveSettings();
            S.EnsureTeam();
            S.LoadLevel(0);
        }

        // ------------------------------------------------------------------ queries
        public static SettingsData Settings => S != null && S.Settings != null ? S.Settings : _fallbackSettings;
        public static TeamData Team => S != null ? S.Team : null;
        public static bool HasRun => S != null && S.HasRun;
        public static bool CanContinue => S != null && (S.HasRun || GetBool("HasSavedRun"));

        /// <summary>GameSession.ShowVictory (set by NextLevel after the last level). Writable if the session allows.</summary>
        public static bool ShowVictory
        {
            get => GetBool("ShowVictory");
            set => SetBool("ShowVictory", value);
        }

        public static bool IsLevelCompleted(int i) => S != null && S.IsLevelCompleted(i);

        public static int FirstIncompleteLevel()
        {
            for (int i = 0; i < LevelCatalog.MainGameCount; i++) if (!IsLevelCompleted(i)) return i;
            return 0;
        }

        public static void ApplySettings() { if (S != null) S.ApplySettings(); else GameEvents.RaiseSettingsChanged(); }
        public static void SaveSettings() { S?.SaveSettings(); }

        // ------------------------------------------------------------------ optional members (reflection, menu-time only)
        static bool GetBool(string member)
        {
            var s = S;
            if (s == null) return false;
            try
            {
                var t = s.GetType();
                var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(bool)) return (bool)p.GetValue(s);
                var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(s);
            }
            catch (Exception) { }
            return false;
        }

        static void SetBool(string member, bool value)
        {
            var s = S;
            if (s == null) return;
            try
            {
                var t = s.GetType();
                var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite && p.GetSetMethod() != null) { p.SetValue(s, value); return; }
                var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(bool)) f.SetValue(s, value);
            }
            catch (Exception) { }
        }

        static bool InvokeOptional(string method)
        {
            var s = S;
            if (s == null) return false;
            try
            {
                var m = s.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m == null) return false;
                m.Invoke(s, null);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[UI] " + method + " failed: " + e.Message); return false; }
        }
    }
}
