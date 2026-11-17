// =====================================================================================
//  SHIFTERS - CampaignDeps.cs   [owned by the CAMPAIGN agent]
//  Tiny adapter to the campaign's cross-module dependencies (seams hooks, story's StoryPlayer,
//  story_kit's StoryOverlay, rendering's WorldColor, bosses' BossBase). Every call is
//  null-safe and tolerates the other module being absent or not yet published (resolved by
//  name once, cached). Tests can replace any call through the *Override delegates.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public static class CampaignDeps
    {
        // ---- test / bot overrides (null = real call)
        public static Func<string, Action, bool> PlayOverride;      // (scriptId, onDone) -> handled
        public static Action<string> OverlayOverride;
        public static Action<float> WorldColorOverride;
        public static Func<int, bool> LoadOverride;                 // (catalogIndex) -> handled (no scene load)
        public static Func<bool> MenuOverride;                      // GoToMainMenu -> handled
        public static Func<int, bool> SceneAvailableOverride;       // LevelCatalog.HasScene replacement

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PlayOverride = null; OverlayOverride = null; WorldColorOverride = null; LoadOverride = null; MenuOverride = null;
            SceneAvailableOverride = null;
        }

        // All dependencies (story, story_kit, rendering, bosses, ui) have published their v1 APIs:
        // direct, null-safe calls (the v1 reflection adapter missed WorldColor.SetBase(float, float)).
        public static bool HasStory => true;
        public static bool HasWorldColor => true;

        static StoryPlayer StoryInstance() { var sp = StoryPlayer.Instance; return sp != null ? sp : null; }

        // ---------------------------------------------------------------- seams
        /// <summary>Installs/clears GameSession.NextLevelOverride (seams).</summary>
        public static bool SetNextLevelOverride(Func<bool> f) { GameSession.NextLevelOverride = f; return true; }
        public static Func<bool> GetNextLevelOverride() => GameSession.NextLevelOverride;
        /// <summary>Installs/clears LevelManager.CampaignIntroHook (seams).</summary>
        public static bool SetIntroHook(Action<LevelManager> a) { LevelManager.CampaignIntroHook = a; return true; }
        public static Action<LevelManager> GetIntroHook() => LevelManager.CampaignIntroHook;

        /// <summary>GameSession.RestartAtNextCheckpoint() (seams). False if there is no next checkpoint / no session.</summary>
        public static bool RestartAtNextCheckpoint()
        {
            var gs = GameSession.Instance;
            if (gs == null) return false;
            try { return gs.RestartAtNextCheckpoint(); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }

        public static void LoadLevel(int index)
        {
            if (LoadOverride != null && LoadOverride(index)) return;
            GameSession.Instance?.LoadLevel(index);
        }

        public static void GoToMainMenu()
        {
            if (MenuOverride != null && MenuOverride()) return;
            GameSession.Instance?.GoToMainMenu();
        }

        public static bool SceneAvailable(int index)
        {
            if (SceneAvailableOverride != null) return SceneAvailableOverride(index);
            return LevelCatalog.HasScene(index);
        }

        // ---------------------------------------------------------------- story
        /// <summary>StoryPlayer.Instance.Play(id, onDone). Returns false (and calls onDone at once) if it can't play.</summary>
        public static bool PlayScript(string id, Action onDone = null)
        {
            if (string.IsNullOrEmpty(id)) { onDone?.Invoke(); return false; }
            if (PlayOverride != null) return PlayOverride(id, onDone);
            var sp = StoryInstance();
            if (sp == null || !Exists(id)) { onDone?.Invoke(); return false; }
            try { sp.Play(id, onDone); return true; }
            catch (Exception e) { Debug.LogException(e); onDone?.Invoke(); return false; }
        }

        public static bool Exists(string id)
        {
            try { return StoryScripts.Exists(id); } catch (Exception) { return false; }
        }

        public static bool StoryPlaying { get { var sp = StoryInstance(); return sp != null && sp.IsPlaying; } }
        public static bool StoryBlocking { get { var sp = StoryInstance(); return sp != null && sp.IsBlocking; } }
        public static void SkipStory() { var sp = StoryInstance(); try { if (sp != null) sp.Skip(); } catch (Exception) { } }
        public static void SetStoryAutoSkip(bool on) { StoryPlayer.AutoSkip = on; }

        public static void ApplyOverlay(string overlayId)
        {
            if (string.IsNullOrEmpty(overlayId)) return;
            if (OverlayOverride != null) { OverlayOverride(overlayId); return; }
            try { StoryOverlay.Apply(overlayId); } catch (Exception e) { Debug.LogException(e); }
        }

        // ---------------------------------------------------------------- rendering
        public static void SetWorldColorBase(float b, float seconds = 0f)
        {
            if (WorldColorOverride != null) { WorldColorOverride(b); return; }
            try { WorldColor.SetBase(b, seconds); } catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>rendering's WorldColor.Enabled (skipped while WorldColorOverride is set: tests mock rendering).</summary>
        public static void SetWorldColorEnabled(bool on)
        {
            if (WorldColorOverride != null) return;
            try { WorldColor.Enabled = on; } catch (Exception e) { Debug.LogException(e); }
        }

        public static void ClearWorldColorBursts()
        {
            if (WorldColorOverride != null) return;
            try { WorldColor.ClearBursts(); } catch (Exception) { }
        }

        // ---------------------------------------------------------------- ui
        /// <summary>UIManager.RequestCampaignEnd (queued for the next main menu when called in a level).</summary>
        public static void RequestCampaignEnd(bool placeholder)
        {
            if (MenuOverride != null) return;   // tests
            try { UIManager.RequestCampaignEnd(placeholder); } catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>LevelCompleteHold.Request (UI caps the total hold at 3 s, ignores it in BotMode).</summary>
        public static void HoldLevelComplete(float seconds)
        {
            try { LevelCompleteHold.Request(seconds); } catch (Exception) { }
        }

        // ---------------------------------------------------------------- bosses
        /// <summary>Sets BossBase.MercyLevel on every live boss whose BossId matches (null/empty id = all). Returns count.</summary>
        public static int SetBossMercy(string bossId, int level)
        {
            int n = 0;
            var all = UnityEngine.Object.FindObjectsByType<BossBase>();
            for (int i = 0; i < all.Length; i++)
            {
                var b = all[i];
                if (b == null) continue;
                if (!string.IsNullOrEmpty(bossId) && b.BossId != bossId) continue;
                if (b.MercyLevel != level) b.MercyLevel = level;
                n++;
            }
            return n;
        }
    }
}
