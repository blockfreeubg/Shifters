// =====================================================================================
//  SHIFTERS - ShiftersBuilder.WebGL.cs   (BUILDER module - WebGL / itch.io build)
//  Menu "SHIFTERS/Build WebGL (itch.io)" or batchmode:
//    unity -batchmode -projectPath P -executeMethod Shifters.EditorTools.ShiftersBuilder.BuildWebGL -quit [-buildPath dir] [-cleanBuild]
//  Builds the enabled Build Settings scenes for WebGL into <project>/Builds/WebGL (or -buildPath / env
//  SHIFTERS_WEBGL_PATH), ready to zip for itch.io:
//   * template PROJECT:Shifters (Assets/WebGLTemplates/Shifters): SHIFTERS-branded loader, 16:9 canvas that fits
//     the itch.io 1280x720 viewport and its fullscreen button, focus on click, and a guard (registered before the
//     Unity loader) that turns browser pointer-lock refusals into console warnings instead of Unity's fatal alert;
//   * WebGL is made the ACTIVE platform first (ShiftersWebBuildTarget): URP strips shader variants for the active
//     platform's render pipeline asset - building from a macOS-active editor stripped against PC_RPAsset and left the
//     game's StylizedLit ForwardLit pass and UberPost with 0 variants (invisible 3D, no post-processing);
//   * URP "Strip Unused Post Processing Variants" OFF during the build (ShiftersWebPostFxStripping): PostFx builds
//     its Volume profile at runtime, so the stripper would drop the bloom/tonemap/CA variants PostFx uses;
//   * Gzip + decompression fallback (works without Content-Encoding headers), data caching, 1280x720 canvas.
//  Player settings are changed ONLY when this method runs. The output is verified (ShiftersWebBuildCheck);
//  -cleanBuild forces a clean build cache so the log proves the shader variant counts (release builds).
// =====================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Shifters.EditorTools
{
    public static partial class ShiftersBuilder
    {
        public const int WebGLCanvasWidth = 1280, WebGLCanvasHeight = 720;
        /// <summary>The SHIFTERS WebGL template (folder Assets/WebGLTemplates/Shifters).</summary>
        public const string WebGLTemplate = "PROJECT:Shifters";
        /// <summary>index.html of <see cref="WebGLTemplate"/> (project relative).</summary>
        public const string WebGLTemplateIndex = "Assets/WebGLTemplates/Shifters/index.html";

        [MenuItem("SHIFTERS/Build WebGL (itch.io)", false, 71)] static void MenuWebGL() => BuildWebGL();

        /// <summary>WebGL build of the enabled Build Settings scenes for itch.io (zip the output folder and upload it).</summary>
        public static void BuildWebGL()
        {
            ShiftersBuildSettings.Apply();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Finish("BuildWebGL: the WebGL Build Support module is not installed for this Unity version\n", 1);
                return;
            }
            string path = ShiftersEditorSteps.Arg("-buildPath") ?? Environment.GetEnvironmentVariable("SHIFTERS_WEBGL_PATH")
                          ?? Path.GetFullPath("Builds/WebGL");
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled && File.Exists(s.path)) scenes.Add(s.path);
            if (scenes.Count == 0) { Finish("BuildWebGL: no enabled scenes - run Build Everything first\n", 1); return; }

            // URP strips shader variants for the ACTIVE platform: build WebGL with WebGL active (ShiftersWebBuildTarget.cs)
            if (!ShiftersWebBuildTarget.EnsureActive(BuildTarget.WebGL, out string targetNote))
            {
                Finish("BuildWebGL: " + targetNote + " - a WebGL build from another active platform strips the game's shaders\n", 1);
                return;
            }
            bool ownTemplate = ApplyWebGLPlayerSettings(out string templateNote);
            Directory.CreateDirectory(path);
            bool clean = Array.IndexOf(Environment.GetCommandLineArgs(), "-cleanBuild") >= 0;
            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = clean ? BuildOptions.CleanBuildCache : BuildOptions.None,
            };

            BuildReport report;
            ShiftersWebPostFxStripping.Begin("BuildWebGL");
            try { report = BuildPipeline.BuildPlayer(opts); }
            finally { ShiftersWebPostFxStripping.Restore(); }

            var sum = report.summary;
            bool ok = sum.result == BuildResult.Succeeded;
            string text = "SHIFTERS WEBGL BUILD: " + sum.result + " -> " + path + " (" + scenes.Count + " scenes, " + (sum.totalSize / (1024 * 1024)) + " MB, "
                          + sum.totalErrors + " errors, " + sum.totalWarnings + " warnings, " + sum.totalTime.TotalSeconds.ToString("0") + " s)\n"
                          + "Template: " + PlayerSettings.WebGL.template + templateNote + "; " + targetNote + (clean ? "; clean build cache" : "") + "\n";
            if (ok)
            {
                var problems = ShiftersWebBuildCheck.Verify(path, ownTemplate, out string info);
                text += "Verified: " + info + "\n";
                foreach (var p in problems) text += "PROBLEM: " + p + "\n";
                ok = problems.Count == 0;
            }
            text += "Upload: zip the CONTENTS of " + path + " (index.html at the zip root), itch.io kind HTML, viewport "
                    + WebGLCanvasWidth + "x" + WebGLCanvasHeight + ", enable 'fullscreen button'.\n";
            Finish(text, ok ? 0 : 1);
        }

        /// <summary>itch.io-friendly WebGL player settings (called only by BuildWebGL). Returns true when the SHIFTERS
        /// template is used (falls back to Unity's default template if the folder is missing).</summary>
        static bool ApplyWebGLPlayerSettings(out string templateNote)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // works without Content-Encoding headers
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.defaultWebScreenWidth = WebGLCanvasWidth;
            PlayerSettings.defaultWebScreenHeight = WebGLCanvasHeight;
            bool own = File.Exists(WebGLTemplateIndex);
            PlayerSettings.WebGL.template = own ? WebGLTemplate : "APPLICATION:Default";
            templateNote = own ? "" : " (WARNING: " + WebGLTemplateIndex + " missing - Unity default template used, no pointer-lock guard)";
            return own;
        }
    }
}
