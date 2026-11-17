// =====================================================================================
//  SHIFTERS - BotStory.cs   [BOTS module - campaign wave]
//  Story / campaign switches for bot runs, by reflection so the bots compile and run while the story and campaign
//  modules are absent: StoryPlayer.AutoSkip and CampaignDirector.BotMode (CAMPAIGN §18.1/§18.2).
//  TeamBot.Run / RunSeeds call Apply(options) before the (re)load and after the team spawned, Restore() at the end.
//  -botStory 1 on the command line forces story ON (the §22 story-ON proof run).
// =====================================================================================
using System;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class BotStory
    {
        static bool _applied, _prevSkip, _prevBot;
        static int _depth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _applied = false; _depth = 0; }

        /// <summary>Story ON for this process (-botStory 1)?</summary>
        public static bool CommandLineStoryOn => BotArgs.Str("-botStory", "0") == "1";

        /// <summary>Current values (null when the module is absent).</summary>
        public static bool? AutoSkip => GetStaticBool("StoryPlayer", "AutoSkip");
        public static bool? BotMode => GetStaticBool("CampaignDirector", "BotMode");

        /// <summary>Set StoryPlayer.AutoSkip + CampaignDirector.BotMode from the options (SkipStory, -botStory overrides).
        /// Nested calls (RunSeeds -> Run) keep the first saved state; a playing blocking script is skipped.</summary>
        public static void Apply(BotOptions o)
        {
            bool skip = (o == null || o.SkipStory) && !CommandLineStoryOn;
            if (!_applied)
            {
                _prevSkip = AutoSkip ?? false;
                _prevBot = BotMode ?? false;
                _applied = true;
            }
            _depth++;
            Set(skip);
        }

        /// <summary>Command-line runs before any TeamBot.Run (scene load): skip unless -botStory 1.</summary>
        public static void ApplyCommandLine() => Set(!CommandLineStoryOn);

        static void Set(bool skip)
        {
            SetStaticBool("StoryPlayer", "AutoSkip", skip);
            SetStaticBool("CampaignDirector", "BotMode", skip);
            if (!skip) return;
            var sp = BotReflect.FindFirst("StoryPlayer");
            if (sp != null && BotReflect.TryGet(sp, "IsPlaying", out bool playing) && playing) BotReflect.TryCall(sp, "Skip", out _);
        }

        /// <summary>Undo one Apply; the outermost restores the values from before the first Apply.</summary>
        public static void Restore()
        {
            if (_depth > 0) _depth--;
            if (_depth > 0 || !_applied) return;
            SetStaticBool("StoryPlayer", "AutoSkip", _prevSkip);
            SetStaticBool("CampaignDirector", "BotMode", _prevBot);
            _applied = false;
        }

        // ---------------------------------------------------------------- reflection helpers
        const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static bool? GetStaticBool(string typeName, string member)
        {
            var t = BotReflect.FindType(typeName);
            if (t == null) return null;
            try
            {
                var f = t.GetField(member, Static);
                if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(null);
                var p = t.GetProperty(member, Static);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead) return (bool)p.GetValue(null);
            }
            catch (Exception e) { Debug.LogWarning("[BOT] BotStory get " + typeName + "." + member + ": " + e.Message); }
            return null;
        }

        public static bool SetStaticBool(string typeName, string member, bool value)
        {
            var t = BotReflect.FindType(typeName);
            if (t == null) return false;
            try
            {
                var f = t.GetField(member, Static);
                if (f != null && f.FieldType == typeof(bool) && !f.IsInitOnly && !f.IsLiteral) { f.SetValue(null, value); return true; }
                var p = t.GetProperty(member, Static);
                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite) { p.SetValue(null, value); return true; }
            }
            catch (Exception e) { Debug.LogWarning("[BOT] BotStory set " + typeName + "." + member + ": " + e.Message); }
            return false;
        }
    }
}
