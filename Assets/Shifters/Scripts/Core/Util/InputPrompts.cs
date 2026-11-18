// =====================================================================================
//  SHIFTERS - InputPrompts.cs   (gp_core: device-aware prompt text)
//  ONE formatter for every player-facing control hint. Templates carry tokens instead of key names:
//    {Jump}            -> "Space" (keyboard) / "A" (gamepad)          (names + aliases: KeyLabels.Ids)
//    {JUMP}            -> "SPACE" / "A"     (ALL-CAPS token name => upper-cased label)
//    {Switcher|RIGHT SHIFT} -> "RIGHT SHIFT" (keyboard: the literal after '|') / "D-DOWN" (gamepad; caps follow the literal)
//    {kb:, or 1/2/3 to switch directly}  -> text only on keyboard/mouse (may contain tokens)
//    {pad:, then D-LEFT/UP/RIGHT}        -> text only on gamepad (may contain tokens)
//    {{ / }}           -> literal braces
//  Unknown tokens stay visible ("{Frobnicate}") and are logged once. Results are cached per
//  (template, device, rich); a template without '{' is returned as-is (no allocation).
//  rich = TMP rich text: gamepad face buttons get their Xbox colour (A green, B red, X blue, Y yellow),
//  other pad labels bold. Keyboard output is NEVER styled (keyboard players see exactly the old text).
//  Also: AuditRawKeys() - finds raw keyboard key names outside tokens (migration / integrator check).
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public static class InputPrompts
    {
        public static readonly Color PadA = new Color32(0x5C, 0xC2, 0x3C, 0xFF);
        public static readonly Color PadB = new Color32(0xE5, 0x48, 0x4D, 0xFF);
        public static readonly Color PadX = new Color32(0x3A, 0x8D, 0xDE, 0xFF);
        public static readonly Color PadY = new Color32(0xF2, 0xC2, 0x30, 0xFF);
        const string HexA = "#5CC23C", HexB = "#E5484D", HexX = "#3A8DDE", HexY = "#F2C230";

        static readonly Dictionary<string, string>[] Cache =
        {
            new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal),
        };
        static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.Ordinal);
        static readonly StringBuilder Sb = new StringBuilder(256);
        const int CacheLimit = 2048;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < Cache.Length; i++) Cache[i].Clear();
            Logged.Clear();
        }

        // ------------------------------------------------------------------ labels
        /// <summary>Label of an action for the CURRENT device ("Space" / "A"); "" when the pad has no binding.</summary>
        public static string Label(string action) => KeyLabels.For(action, ShiftersInput.UsingGamepad);
        public static string Label(string action, bool gamepad) => KeyLabels.For(action, gamepad);

        /// <summary>True for the four gamepad face buttons (round coloured caps).</summary>
        public static bool IsFaceButton(string padLabel, out Color color)
        {
            color = Color.white;
            if (padLabel == null || padLabel.Length != 1) return false;
            switch (padLabel[0])
            {
                case 'A': case 'a': color = PadA; return true;
                case 'B': case 'b': color = PadB; return true;
                case 'X': case 'x': color = PadX; return true;
                case 'Y': case 'y': color = PadY; return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ formatting
        /// <summary>Format for the current device, plain text (no colour tags).</summary>
        public static string Format(string template) => Format(template, ShiftersInput.UsingGamepad, false);
        /// <summary>Format for the current device with TMP rich styling of pad labels (keyboard output unchanged).</summary>
        public static string FormatRich(string template) => Format(template, ShiftersInput.UsingGamepad, true);

        /// <summary>Format a template for the given device. Cached; safe at edit time (builders bake the keyboard text).</summary>
        public static string Format(string template, bool gamepad, bool rich)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0 && template.IndexOf('}') < 0) return template ?? "";
            var cache = Cache[(gamepad ? 1 : 0) + (rich ? 2 : 0)];
            if (cache.TryGetValue(template, out var hit)) return hit;
            Sb.Length = 0;
            Append(Sb, template, 0, template.Length, gamepad, rich, template);
            string s = Sb.ToString();
            if (cache.Count >= CacheLimit) cache.Clear();
            cache[template] = s;
            return s;
        }

        /// <summary>True when the text contains a {token} or device segment.</summary>
        public static bool HasTokens(string s) => !string.IsNullOrEmpty(s) && s.IndexOf('{') >= 0;

        static void Append(StringBuilder sb, string t, int start, int end, bool gamepad, bool rich, string whole)
        {
            int i = start;
            while (i < end)
            {
                char c = t[i];
                if (c == '{')
                {
                    if (i + 1 < end && t[i + 1] == '{') { sb.Append('{'); i += 2; continue; }
                    int close = Match(t, i, end);
                    if (close < 0) { sb.Append(t, i, end - i); return; }   // unclosed: literal
                    Token(sb, t, i + 1, close, gamepad, rich, whole);
                    i = close + 1;
                    continue;
                }
                if (c == '}' && i + 1 < end && t[i + 1] == '}') { sb.Append('}'); i += 2; continue; }
                sb.Append(c);
                i++;
            }
        }

        /// <summary>Index of the '}' closing the '{' at open (nesting aware), or -1.</summary>
        static int Match(string t, int open, int end)
        {
            int depth = 0;
            for (int j = open; j < end; j++)
            {
                if (t[j] == '{') depth++;
                else if (t[j] == '}' && --depth == 0) return j;
            }
            return -1;
        }

        static bool StartsWithCi(string t, int at, int end, string prefix)
        {
            if (end - at < prefix.Length) return false;
            return string.Compare(t, at, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        static void Token(StringBuilder sb, string t, int s, int e, bool gamepad, bool rich, string whole)
        {
            if (StartsWithCi(t, s, e, "kb:")) { if (!gamepad) Append(sb, t, s + 3, e, false, rich, whole); return; }
            if (StartsWithCi(t, s, e, "pad:")) { if (gamepad) Append(sb, t, s + 4, e, true, rich, whole); return; }
            int bar = t.IndexOf('|', s, e - s);
            string name = t.Substring(s, (bar < 0 ? e : bar) - s).Trim();
            string kbOverride = bar < 0 ? null : t.Substring(bar + 1, e - bar - 1);
            if (!KeyLabels.IsAction(name))
            {
                sb.Append('{').Append(t, s, e - s).Append('}');
                LogOnce("unknown token {" + name + "} in \"" + whole + "\"");
                return;
            }
            bool upper = AllCaps(kbOverride ?? name);
            if (!gamepad)
            {
                string kb = kbOverride ?? KeyLabels.Keyboard(name);
                sb.Append(upper ? kb.ToUpperInvariant() : kb);
                return;
            }
            string pad = KeyLabels.Gamepad(name);
            if (string.IsNullOrEmpty(pad)) { LogOnce("token {" + name + "} has no gamepad binding (wrap it in {kb:...}) in \"" + whole + "\""); return; }
            if (upper) pad = pad.ToUpperInvariant();
            if (!rich) { sb.Append(pad); return; }
            string hex = FaceHex(pad);
            if (hex != null) sb.Append("<b><color=").Append(hex).Append('>').Append(pad).Append("</color></b>");
            else sb.Append("<b>").Append(pad).Append("</b>");
        }

        static string FaceHex(string pad)
        {
            if (pad.Length != 1) return null;
            switch (pad[0]) { case 'A': return HexA; case 'B': return HexB; case 'X': return HexX; case 'Y': return HexY; }
            return null;
        }

        static bool AllCaps(string s)
        {
            bool letter = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLower(c)) return false;
                if (char.IsUpper(c)) letter = true;
            }
            return letter;
        }

        static void LogOnce(string msg)
        {
            if (Logged.Add(msg)) Debug.LogWarning("[InputPrompts] " + msg);
        }

        // ------------------------------------------------------------------ audit
        // multi-letter keyboard names, any case, whole word
        static readonly string[] AnyCase = { "WASD", "LSHIFT", "RSHIFT", "LMB", "RMB", "BACKSPACE", "SPACEBAR", "ESC", "LEFT SHIFT", "RIGHT SHIFT",
            "LEFT CLICK", "RIGHT CLICK", "MOUSE WHEEL", "SCROLL WHEEL", "LEFT MOUSE", "RIGHT MOUSE", "ARROW KEYS", "NUMPAD" };
        // ordinary words that are key names only when written in CAPS (or after press/hold/tap/hit)
        static readonly string[] CapsOnly = { "SPACE", "TAB", "MOUSE", "CLICK", "CTRL", "ARROWS" };
        static readonly string[] Verbs = { "press", "hold", "tap", "hit", "pressing", "holding", "tapping" };

        /// <summary>Raw keyboard key names found OUTSIDE tokens / {kb:} segments / rich tags (empty = clean).
        /// Heuristic: any-case WASD/LSHIFT/LMB/BACKSPACE/ESC..., CAPS SPACE/TAB/MOUSE/CLICK/ENTER/SHIFT..., [E]-style
        /// bracket caps, and "press X" / "hold Shift" verb phrases. "SHIFT" as the game's theme word (THE GREAT SHIFT,
        /// SHIFTERS) is only flagged after a verb ("HOLD SHIFT"), never on its own.</summary>
        public static List<string> AuditRawKeys(string text)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            string s = StripForAudit(text);
            string up = s.ToUpperInvariant();
            for (int k = 0; k < AnyCase.Length; k++) FindWords(up, AnyCase[k], found, null);
            for (int k = 0; k < CapsOnly.Length; k++) FindWords(s, CapsOnly[k], found, null);
            Bracketed(s, '[', ']', found, true);    // [E] [1] [SPACE] [RIGHT SHIFT] ...
            Bracketed(s, '(', ')', found, false);   // (SPACE) (E) (ENTER)
            // "press E", "hold SHIFT", "tap space"
            var words = s.Split(new[] { ' ', '\t', '\n', ',', '.', ':', ';', '!', '?', '(', ')', '"' }, StringSplitOptions.RemoveEmptyEntries);
            for (int w = 0; w + 1 < words.Length; w++)
            {
                if (Array.IndexOf(Verbs, words[w].ToLowerInvariant()) < 0) continue;
                string nx = words[w + 1];
                bool single = nx.Length == 1 && (char.IsUpper(nx[0]) || char.IsDigit(nx[0]));
                string nu = nx.ToUpperInvariant();
                bool named = nu == "SPACE" || nu == "SHIFT" || nu == "TAB" || nu == "ENTER" || nu == "ESC" || nu == "ESCAPE" || nu == "BACKSPACE" || nu == "CTRL"
                    || nu == "RETURN" || nu == "LMB" || nu == "RMB" || nu == "LSHIFT" || nu == "RSHIFT";
                if (single || named) found.Add(words[w] + " " + nx);
            }
            return found;
        }

        /// <summary>True when <see cref="AuditRawKeys"/> finds nothing.</summary>
        public static bool IsClean(string text) => AuditRawKeys(text).Count == 0;

        static void Bracketed(string s, char open, char close, List<string> found, bool anyShort)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != open) continue;
                int j = s.IndexOf(close, i + 1);
                if (j < 0) break;
                string inner = s.Substring(i + 1, j - i - 1).Trim();
                bool shortCap = anyShort ? inner.Length >= 1 && inner.Length <= 2 : inner.Length == 1 && char.IsUpper(inner[0]);
                if (shortCap || inner.Length <= 12 && KeyLabels.IsKeyName(inner) && (anyShort || IsCapsWord(inner)))
                    found.Add(open + inner + close);
                i = j;
            }
        }

        static bool IsCapsWord(string s)
        {
            for (int i = 0; i < s.Length; i++) if (!(char.IsUpper(s[i]) || s[i] == ' ')) return false;
            return true;
        }

        static void FindWords(string hay, string word, List<string> found, string _)
        {
            int at = 0;
            while ((at = hay.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
            {
                bool l = at == 0 || !char.IsLetterOrDigit(hay[at - 1]);
                int end = at + word.Length;
                bool r = end >= hay.Length || !char.IsLetterOrDigit(hay[end]);
                if (l && r) { found.Add(word); return; }
                at = end;
            }
        }

        /// <summary>Text with tokens, {kb:...} segments and rich tags removed ({pad:...} content kept: it must be pad-clean).</summary>
        static string StripForAudit(string t)
        {
            var sb = new StringBuilder(t.Length);
            int i = 0;
            while (i < t.Length)
            {
                char c = t[i];
                if (c == '{')
                {
                    int close = Match(t, i, t.Length);
                    if (close < 0) { sb.Append(t, i, t.Length - i); break; }
                    if (StartsWithCi(t, i + 1, close, "pad:")) sb.Append(' ').Append(StripForAudit(t.Substring(i + 5, close - i - 5))).Append(' ');
                    else sb.Append(' ');
                    i = close + 1;
                    continue;
                }
                if (c == '<')
                {
                    int close = t.IndexOf('>', i + 1);
                    if (close > i && close - i < 40) { i = close + 1; continue; }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
