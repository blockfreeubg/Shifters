// =====================================================================================
//  SHIFTERS - StoryText.cs   [story]
//  Canonical story text (CAMPAIGN §7, §7.1, §7.2, §8.2, §8.4, §12, §24). Partial per act:
//    StoryText.cs          tokens, speakers, colors, line timing, box limits
//    StoryText.Act1.cs     prologue C00, act cards, C01..C04, L1/L2 broadcasts, end card
//    StoryText.Act2.cs     heist C05..C07 + ch04..ch08 beats
//    StoryText.Act3.cs     night C08..C10 + ch09..ch11 beats
//    StoryText.Act4.cs     tower + finale C11..C16 + ch13/ch14 beats, decrees
//    StoryText.Banks.cs    barks, counter-barks, ME-TV channels, wrong names, Percy, posters, tombstones, taunts
//  Tokens: {BRUTE} {SPRINTER} {BRAINIAC} {GUNNER} {CATCHER} {CLONE} {ACTIVE} {PRESIDENT} = interns' DisplayNames;
//  {NAME} = one of Max's WRONG names (§24.1, rotates); {CHAPTER} = "SHIFT 05"; any custom token via SetToken.
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public static partial class StoryText
    {
        // ---- dialogue-box limits (enforced by StoryTests)
        /// <summary>Max characters of one resolved subtitle (§7 rules).</summary>
        public const int MaxChars = 90;
        /// <summary>DialogueBox wraps at this many characters per row...</summary>
        public const int BoxCharsPerRow = 56;
        /// <summary>...and shows at most this many rows.</summary>
        public const int BoxRows = 2;
        /// <summary>Longest intern DisplayName the box budget assumes ("BARTHOLOMEW III").</summary>
        public const int MaxNameChars = 15;

        public static readonly Color MaxGold = new Color(0.91f, 0.72f, 0.23f);        // #E8B83A
        public static readonly Color PercyBrown = new Color(0.545f, 0.353f, 0.169f);  // #8B5A2B
        public static readonly Color MachineGrey = new Color(0.62f, 0.66f, 0.71f);
        public static readonly Color AlarmRed = new Color(0.93f, 0.33f, 0.30f);
        public static readonly Color GhostBlue = new Color(0.78f, 0.90f, 1f);

        static readonly Dictionary<string, string> _tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly StringBuilder _sb = new StringBuilder(128);
        static int _wrongIdx;

        /// <summary>Chapter label for {CHAPTER} (campaign sets "SHIFT 05").</summary>
        public static string Chapter = "SHIFT 01";
        /// <summary>Intern who caught Max (C15). Null = the Brute.</summary>
        public static string Catcher;
        /// <summary>Reprinted intern for {CLONE}. Null = GameSession.LastRecloned[0] or the active intern.</summary>
        public static string CloneName;
        /// <summary>Today's president for {PRESIDENT} (ShiftocracyEnding sets it).</summary>
        public static string President;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _tokens.Clear(); _wrongIdx = 0; Chapter = "SHIFT 01"; Catcher = null; CloneName = null; President = null; }

        public static void SetToken(string token, string value)
        {
            if (string.IsNullOrEmpty(token)) return;
            token = token.Trim('{', '}');
            if (value == null) _tokens.Remove(token); else _tokens[token] = value;
        }

        /// <summary>Next of Max's wrong names (§24.1), rotating. Max never says a real name before C15.</summary>
        public static string WrongName()
        {
            var n = WrongNames[_wrongIdx % WrongNames.Length];
            _wrongIdx++;
            return n;
        }

        /// <summary>Test hook: when set, every intern token resolves to this (box-fit worst case).</summary>
        public static string ForceInternName;

        // ---- characters
        public static ShifterCharacter FindIntern(Archetype a)
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return null;
            for (int i = 0; i < cm.Team.Count; i++) { var c = cm.Team[i]; if (c != null && c.Archetype == a) return c; }
            return null;
        }

        static CharacterProfile FindProfile(Archetype a)
        {
            var gs = GameSession.Instance;
            var t = gs != null ? gs.Team : null;
            if (t == null || t.Members == null) return null;
            for (int i = 0; i < t.Members.Length; i++) if (t.Members[i] != null && t.Members[i].Archetype == a) return t.Members[i];
            return null;
        }

        public static ShifterCharacter FindGunner()
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return null;
            for (int i = 0; i < cm.Team.Count; i++) { var c = cm.Team[i]; if (c != null && c.Profile != null && c.Profile.HasWeapon) return c; }
            return null;
        }

        public static string InternName(Archetype a)
        {
            if (!string.IsNullOrEmpty(ForceInternName)) return ForceInternName;
            var c = FindIntern(a);
            if (c != null) return c.DisplayName.ToUpperInvariant();
            var p = FindProfile(a);
            if (p != null) return p.DisplayName.ToUpperInvariant();
            return a == Archetype.Brute ? "THE BIG ONE" : a == Archetype.Sprinter ? "THE FAST ONE" : "THE SMART ONE";
        }

        public static Color InternColor(Archetype a)
        {
            var c = FindIntern(a);
            if (c != null) return c.Color;
            var p = FindProfile(a);
            if (p != null) return p.PrimaryColor;
            return a == Archetype.Brute ? new Color(0.9f, 0.35f, 0.3f) : a == Archetype.Sprinter ? new Color(0.3f, 0.75f, 0.95f) : new Color(0.55f, 0.85f, 0.4f);
        }

        static bool TryArchetype(string token, out Archetype a)
        {
            switch (token)
            {
                case "BRUTE": a = Archetype.Brute; return true;
                case "SPRINTER": a = Archetype.Sprinter; return true;
                case "BRAINIAC": a = Archetype.Brainiac; return true;
            }
            a = Archetype.Brute; return false;
        }

        static string TokenValue(string token)
        {
            if (_tokens.TryGetValue(token, out var custom)) return custom;
            if (TryArchetype(token, out var a)) return InternName(a);
            switch (token)
            {
                case "NAME": return WrongName();
                case "CHAPTER": return Chapter ?? "";
                case "GUNNER":
                {
                    if (!string.IsNullOrEmpty(ForceInternName)) return ForceInternName;
                    var g = FindGunner();
                    return g != null ? g.DisplayName.ToUpperInvariant() : InternName(Archetype.Brainiac);
                }
                case "CATCHER":
                    if (!string.IsNullOrEmpty(ForceInternName)) return ForceInternName;
                    return !string.IsNullOrEmpty(Catcher) ? Catcher.ToUpperInvariant() : InternName(Archetype.Brute);
                case "PRESIDENT":
                    if (!string.IsNullOrEmpty(ForceInternName)) return ForceInternName;
                    return !string.IsNullOrEmpty(President) ? President.ToUpperInvariant() : InternName(Archetype.Brainiac);
                case "CLONE":
                {
                    if (!string.IsNullOrEmpty(ForceInternName)) return ForceInternName;
                    if (!string.IsNullOrEmpty(CloneName)) return CloneName.ToUpperInvariant();
                    var gs = GameSession.Instance;
                    if (gs != null && gs.LastRecloned != null && gs.LastRecloned.Length > 0 && !string.IsNullOrEmpty(gs.LastRecloned[0]))
                        return gs.LastRecloned[0].ToUpperInvariant();
                    return ActiveName();
                }
                case "ACTIVE": return ActiveName();
                case "RESCUED": case "VICTIM": return ActiveName();   // levels SetToken the real intern before the beat
            }
            return null;
        }

        static string ActiveName()
        {
            if (!string.IsNullOrEmpty(ForceInternName)) return ForceInternName;
            var cm = CharacterManager.Instance;
            if (cm != null && cm.Active != null) return cm.Active.DisplayName.ToUpperInvariant();
            return InternName(Archetype.Brute);
        }

        /// <summary>Replace every {TOKEN}. Unknown tokens stay as written (StoryTests fails on them).</summary>
        public static string Resolve(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text ?? "";
            _sb.Length = 0;
            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];
                if (ch == '{')
                {
                    int end = text.IndexOf('}', i + 1);
                    if (end > i)
                    {
                        var tok = text.Substring(i + 1, end - i - 1);
                        var v = TokenValue(tok.ToUpperInvariant());
                        if (v != null) { _sb.Append(v); i = end + 1; continue; }
                    }
                }
                _sb.Append(ch);
                i++;
            }
            return _sb.ToString();
        }

        /// <summary>True when every token in the text is known (tests).</summary>
        public static bool TokensResolve(string text, out string bad)
        {
            bad = null;
            if (string.IsNullOrEmpty(text)) return true;
            int i = 0;
            while ((i = text.IndexOf('{', i)) >= 0)
            {
                int end = text.IndexOf('}', i + 1);
                if (end < 0) { bad = text.Substring(i); return false; }
                var tok = text.Substring(i + 1, end - i - 1).ToUpperInvariant();
                if (!_tokens.ContainsKey(tok) && !IsKnownToken(tok)) { bad = tok; return false; }
                i = end + 1;
            }
            return true;
        }

        static bool IsKnownToken(string t)
        {
            switch (t)
            {
                case "BRUTE": case "SPRINTER": case "BRAINIAC": case "GUNNER": case "CATCHER": case "CLONE":
                case "ACTIVE": case "PRESIDENT": case "NAME": case "CHAPTER": case "RESCUED": case "VICTIM": return true;
            }
            return IsInputPromptToken(t);
        }

        /// <summary>Device-aware control tokens (InputPrompts, gp_ui): "{Switcher|RIGHT SHIFT}", "{JUMP}", "{kb:...}", "{pad:...}".
        /// Resolve leaves them as written; the HUD (HudPrompt / DevicePromptText) formats them for the player's device.</summary>
        static bool IsInputPromptToken(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            if (t.StartsWith("KB:", StringComparison.OrdinalIgnoreCase) || t.StartsWith("PAD:", StringComparison.OrdinalIgnoreCase)) return true;
            int bar = t.IndexOf('|');
            return KeyLabels.Canonical(bar >= 0 ? t.Substring(0, bar) : t) != null;
        }

        // ---- speakers
        /// <summary>Resolve a speaker token to the StoryLine plate data. character = the intern (null for others).</summary>
        public static void Speaker(string token, out string id, out string name, out Color color, out ShifterCharacter character)
        {
            character = null;
            token = string.IsNullOrEmpty(token) ? "MAX" : token;
            var t = token.Trim('{', '}').ToUpperInvariant();
            if (TryArchetype(t, out var a) || t == "GUNNER" || t == "CATCHER" || t == "CLONE" || t == "ACTIVE" || t == "PRESIDENT" || t == "RESCUED" || t == "VICTIM" || _tokens.ContainsKey(t))
            {
                if (t == "GUNNER") { character = FindGunner(); if (character != null) a = character.Archetype; else a = Archetype.Brainiac; }
                else if (t == "CATCHER" || t == "CLONE" || t == "ACTIVE" || t == "PRESIDENT" || t == "RESCUED" || t == "VICTIM" || _tokens.ContainsKey(t))
                {
                    var nm = TokenValue(t);
                    character = FindByName(nm);
                    a = character != null ? character.Archetype : Archetype.Brute;
                    id = character != null ? character.Archetype.ToString().ToUpperInvariant() : t;
                    name = nm; color = character != null ? character.Color : InternColor(a);
                    return;
                }
                else character = FindIntern(a);
                id = a.ToString().ToUpperInvariant();
                name = t == "GUNNER" ? TokenValue("GUNNER") : InternName(a);
                color = character != null ? character.Color : InternColor(a);
                return;
            }
            id = t;
            switch (t)
            {
                case "MAX": case "TV": id = "MAX"; name = "MAX ME"; color = MaxGold; return;
                case "CARDBOARD MAX": name = t; color = MaxGold; return;
                case "PERCY": name = "PERCY"; color = PercyBrown; return;
                case "GHOST": case "GHOST TRIO": name = t; color = GhostBlue; return;
                case "KLAXON": case "ALARM": case "BOOGEYMAN": name = t; color = AlarmRed; return;
                case "DOWNSIZER": case "PORKCHOP": case "ME-CHA": case "SCARE-O-MATIC": name = t; color = new Color(0.95f, 0.55f, 0.25f); return;
                case "CARD": case "TITLE": name = ""; color = Color.white; return;
            }
            name = t; color = MachineGrey;
        }

        static ShifterCharacter FindByName(string upperName)
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null || string.IsNullOrEmpty(upperName)) return null;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c != null && string.Equals(c.DisplayName, upperName, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
        }

        /// <summary>Speakers whose lines go to the ME-TV / radio PiP in non-blocking scripts.</summary>
        public static bool IsPipSpeaker(string id) => id == "MAX" || id == "PERCY";

        /// <summary>Seconds a line stays up before auto-advance (shorter than §15.2's chars/15 so blocking scenes fit 30 s).</summary>
        public static float LineSeconds(string resolvedText, float subtitleSpeed = 1f)
        {
            int n = resolvedText != null ? resolvedText.Length : 0;
            float s = Mathf.Clamp(0.9f + 0.042f * n, 1.8f, 5f);
            return s / Mathf.Clamp(subtitleSpeed <= 0f ? 1f : subtitleSpeed, 0.5f, 2f);
        }

        /// <summary>Rows the DialogueBox needs for this text with greedy word wrap at BoxCharsPerRow.</summary>
        public static int BoxRowsFor(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int rows = 1, col = 0;
            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                int w = words[i].Length;
                if (col == 0) col = w;
                else if (col + 1 + w <= BoxCharsPerRow) col += 1 + w;
                else { rows++; col = w; }
                while (col > BoxCharsPerRow) { rows++; col -= BoxCharsPerRow; }
            }
            return rows;
        }

        public static bool FitsBox(string resolved) => resolved != null && resolved.Length <= MaxChars && BoxRowsFor(resolved) <= BoxRows;

        // ---- registration of every built-in script (StoryScripts calls this once)
        internal static void RegisterAll()
        {
            RegisterAct1();
            RegisterAct2();
            RegisterAct3();
            RegisterAct4();
        }

        // tiny helpers for the act files
        static string Pick(string[] bank, ref int idx) { var s = bank[idx % bank.Length]; idx++; return s; }
    }
}
