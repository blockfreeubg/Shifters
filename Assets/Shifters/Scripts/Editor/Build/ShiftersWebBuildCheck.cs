// =====================================================================================
//  SHIFTERS - ShiftersWebBuildCheck.cs   (BUILDER module - WebGL output verification)
//  After ShiftersBuilder.BuildWebGL: checks that the output really is what itch.io needs (index.html at the root,
//  the SHIFTERS template with its pointer-lock guard placed BEFORE the Unity loader, loader/framework/data/wasm,
//  TemplateData fonts) and reads the Editor log to report how many variants of the critical passes survived
//  shader stripping: Shifters/StylizedLit ForwardLit and URP UberPost (0 = the invisible-3D / no-post regression).
// =====================================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class ShiftersWebBuildCheck
    {
        /// <summary>Marker attribute the SHIFTERS template puts on its guard script (index.html).</summary>
        public const string GuardMarker = "data-shifters-guard";
        /// <summary>Marker attribute the SHIFTERS template puts on its body (index.html).</summary>
        public const string TemplateMarker = "data-shifters-template";

        /// <summary>Problems found in a WebGL build folder (empty = OK). info receives a short human summary.</summary>
        public static List<string> Verify(string buildDir, bool expectShiftersTemplate, out string info)
        {
            var problems = new List<string>();
            var notes = new List<string>();
            string index = Path.Combine(buildDir, "index.html");
            if (!File.Exists(index)) { problems.Add("index.html missing at the build root"); info = ""; return problems; }
            string html = File.ReadAllText(index);
            string buildSub = Path.Combine(buildDir, "Build");
            string loader = FindOne(buildSub, "*.loader.js");
            if (loader == null) problems.Add("Build/*.loader.js missing");
            foreach (var pat in new[] { "*.framework.js*", "*.data*", "*.wasm*" })
                if (FindOne(buildSub, pat) == null) problems.Add("Build/" + pat + " missing");

            if (expectShiftersTemplate)
            {
                if (!html.Contains(TemplateMarker)) problems.Add("index.html is not the SHIFTERS template (" + TemplateMarker + " missing)");
                int guard = html.IndexOf(GuardMarker, System.StringComparison.Ordinal);
                int loaderRef = loader != null ? html.IndexOf(Path.GetFileName(loader), System.StringComparison.Ordinal) : -1;
                if (guard < 0) problems.Add("pointer-lock guard script missing from index.html");
                else if (loaderRef >= 0 && guard > loaderRef) problems.Add("pointer-lock guard is placed after the Unity loader reference");
                else notes.Add("pointer-lock guard before loader: yes");
                foreach (var f in new[] { "TemplateData/style.css", "TemplateData/fonts/Bungee-Regular.ttf", "TemplateData/fonts/BarlowCondensed-Bold.ttf" })
                    if (!File.Exists(Path.Combine(buildDir, f))) problems.Add(f + " missing");
                if (html.Contains("{{{") || html.Contains("}}}")) problems.Add("index.html still contains unprocessed template tags");
            }

            notes.Add(CriticalVariantSummary(problems));
            info = string.Join("; ", notes);
            return problems;
        }

        // shader, pass, what breaks when the pass has no variants in the player
        static readonly string[,] Critical =
        {
            { "Shifters/StylizedLit", "ForwardLit", "the game's main lit shader has no forward pass - the 3D world is invisible" },
            { "Hidden/Universal Render Pipeline/UberPost", "UberPost", "post-processing (tonemap/bloom/grading/switcher desaturation) cannot run" },
        };

        /// <summary>Variants kept after stripping for the critical shader passes (from this session's Editor log).
        /// Adds a problem for every pass that ended with 0 variants.</summary>
        public static string CriticalVariantSummary(List<string> problems)
        {
            if (!TryReadEditorLog(out string text, out string why)) return "shader variants: unknown (" + why + ")";
            var parts = new List<string>();
            for (int i = 0; i < Critical.GetLength(0); i++)
            {
                int n = VariantsAfterStripping(text, Critical[i, 0], Critical[i, 1]);
                parts.Add(Critical[i, 0] + "/" + Critical[i, 1] + " " + (n < 0 ? "not recompiled this session (cached; use -cleanBuild to re-verify)" : n.ToString()));
                if (n == 0) problems?.Add(Critical[i, 0] + " pass " + Critical[i, 1] + " has 0 variants after stripping: " + Critical[i, 2]);
            }
            return "shader variants kept: " + string.Join(", ", parts);
        }

        static bool TryReadEditorLog(out string text, out string why)
        {
            text = null; why = null;
            string log = Application.consoleLogPath;
            if (string.IsNullOrEmpty(log) || !File.Exists(log)) { why = "no editor log"; return false; }
            try
            {
                using (var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs)) text = sr.ReadToEnd();
                return true;
            }
            catch (IOException e) { why = e.Message; return false; }
        }

        static string FindOne(string dir, string pattern)
        {
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, pattern);
            return files.Length > 0 ? files[0] : null;
        }

        /// <summary>Variants left after scriptable stripping for one pass (or all passes when onlyPass is null) in the LAST
        /// compile block of shaderName in the log; -1 if the shader was not compiled in this session.</summary>
        static int VariantsAfterStripping(string log, string shaderName, string onlyPass)
        {
            string head = "Compiling shader \"" + shaderName + "\"";
            int start = log.LastIndexOf(head, System.StringComparison.Ordinal);
            if (start < 0) return -1;
            int end = log.IndexOf("Serialized binary data for shader", start, System.StringComparison.Ordinal);
            string block = end > start ? log.Substring(start, end - start) : log.Substring(start, Mathf.Min(6000, log.Length - start));
            int total = 0;
            string pass = null;
            foreach (var line in block.Split('\n'))
            {
                var pm = Regex.Match(line, "^\\s*Pass \"([^\"]*)\"");
                if (pm.Success) { pass = pm.Groups[1].Value; continue; }
                var m = Regex.Match(line, "After scriptable stripping:\\s*([0-9.,]+)");
                if (!m.Success) continue;
                if (onlyPass != null && pass != onlyPass) continue;
                if (int.TryParse(m.Groups[1].Value.Replace(".", "").Replace(",", ""), out int n)) total += n;
            }
            return total;
        }
    }
}
