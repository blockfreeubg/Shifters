// =====================================================================================
//  SHIFTERS - StoryKitLate.cs   [owned by STORY_KIT]
//  Null-safe bridges to campaign-wave APIs of other modules (CAMPAIGN §18): VillainStudio's render texture,
//  CampaignDirector.LostLog, LevelLighting.SetOverride/ClearOverride, StalkerBrain.FreezeAll, CharacterPuppet.SetGold.
//  (Direct calls since every API is published; v1 used reflection while they were still in progress.)
//  Also: StoryKitSay (non-blocking StoryLine show/hide that never hides someone else's line) and StoryAttemptMemory
//  (ids seen during the current level attempt; survives checkpoint restarts / scene reloads of the same level).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class StoryKitLate
    {
        /// <summary>VillainStudio's broadcast texture (Max's face) when a studio exists (never creates one), else null.</summary>
        public static Texture VillainTexture()
        {
            var st = VillainStudio.Instance;
            return st != null ? st.Texture : null;
        }

        /// <summary>CampaignDirector.Instance.LostLog as plain records (empty when the campaign is absent / not running).</summary>
        public static List<LostEntry> LostLog()
        {
            var list = new List<LostEntry>();
            var cd = CampaignDirector.Instance;
            if (cd == null) return list;
            try
            {
                var log = cd.LostLog;
                if (log == null) return list;
                for (int i = 0; i < log.Count; i++)
                    list.Add(new LostEntry { Name = log[i].Name, Cause = log[i].Cause, ChapterId = log[i].ChapterId, CloneName = log[i].CloneName });
            }
            catch (Exception e) { Debug.LogWarning("[StoryKit] LostLog: " + e.Message); }
            return list;
        }

        /// <summary>LevelLighting.SetOverride(intensity, ambient, seconds).</summary>
        public static bool TrySetLightingOverride(float intensity, float ambient, float seconds)
        {
            try { LevelLighting.SetOverride(intensity, ambient, seconds); return true; }
            catch (Exception e) { Debug.LogWarning("[StoryKit] SetOverride: " + e.Message); return false; }
        }

        public static bool TryClearLightingOverride()
        {
            try { LevelLighting.ClearOverride(0.15f); return true; } catch (Exception) { return false; }
        }

        /// <summary>StalkerBrain.FreezeAll(seconds).</summary>
        public static bool TryFreezeAllStalkers(float seconds)
        {
            try { StalkerBrain.FreezeAll(seconds); return true; } catch (Exception e) { Debug.LogWarning("[StoryKit] FreezeAll: " + e.Message); return false; }
        }

        public static bool HasLightingOverride => true;

        /// <summary>CharacterPuppet.SetGold(on); false only when p is null or it threw (caller applies a gold material itself).</summary>
        public static bool TrySetGold(CharacterPuppet p, bool on)
        {
            if (p == null) return false;
            try { p.SetGold(on); return true; } catch (Exception) { return false; }
        }
    }

    public struct LostEntry { public string Name, Cause, ChapterId, CloneName; }

    /// <summary>Non-blocking StoryLine helper for props (Percy, taunts, phone): Hide(token) only hides the line if no
    /// other system has shown a newer one meanwhile.</summary>
    public static class StoryKitSay
    {
        static int _next, _current;
        static bool _raising, _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _next = 0; _current = 0; _raising = false; _hooked = false; }

        static void OnShown(StoryLine l) { if (!_raising) _current = 0; }

        public static int Say(string speakerId, string speakerName, Color color, string text, float seconds, Expression face = Expression.Neutral, bool broadcast = false)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (!_hooked) { GameEvents.StoryLineShown += OnShown; _hooked = true; }
            _current = ++_next;
            _raising = true;
            try
            {
                GameEvents.RaiseStoryLineShown(new StoryLine
                {
                    SpeakerId = speakerId, SpeakerName = speakerName, Text = text, SpeakerColor = color, Duration = seconds,
                    Broadcast = broadcast, Face = face,
                });
            }
            finally { _raising = false; }
            return _current;
        }

        public static int Max(string text, float seconds = 0f, Expression face = Expression.Smug) =>
            Say(StoryKitLines.MaxId, StoryKitLines.MaxName, StoryKitLines.MaxColor, text, seconds > 0f ? seconds : AutoSeconds(text), face);

        public static int Percy(string text, float seconds = 0f, bool radio = false) =>
            Say(StoryKitLines.PercyId, StoryKitLines.PercyName, StoryKitLines.PercyColor, text, seconds > 0f ? seconds : AutoSeconds(text), Expression.Happy, radio);

        public static float AutoSeconds(string text) => Mathf.Clamp(1.6f + (text != null ? text.Length : 0) * 0.055f, 2.5f, 6f);

        public static bool IsCurrent(int token) => token != 0 && token == _current;

        public static void Hide(int token)
        {
            if (!IsCurrent(token)) return;
            _current = 0;
            GameEvents.RaiseStoryLineHidden();
        }
    }

    /// <summary>Ids seen during the current level attempt (LevelAttempt.LevelIndex). Reset when another level (or a menu)
    /// starts; survives checkpoint restarts. Used by taunt spots and the ScareDirector.</summary>
    public static class StoryAttemptMemory
    {
        static readonly HashSet<string> _seen = new HashSet<string>();
        static int _attemptLevel = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _seen.Clear(); _attemptLevel = int.MinValue; }

        /// <summary>Clears the memory when the attempt changed. Returns true when it was cleared.</summary>
        public static bool Sync()
        {
            int lvl = LevelAttempt.LevelIndex;
            if (lvl == _attemptLevel) return false;
            _attemptLevel = lvl;
            _seen.Clear();
            return true;
        }

        public static bool Seen(string id) { Sync(); return !string.IsNullOrEmpty(id) && _seen.Contains(id); }
        public static void Mark(string id) { Sync(); if (!string.IsNullOrEmpty(id)) _seen.Add(id); }
        public static void Clear() { _seen.Clear(); }
    }
}
