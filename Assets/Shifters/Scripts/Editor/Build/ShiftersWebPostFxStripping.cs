// =====================================================================================
//  SHIFTERS - ShiftersWebPostFxStripping.cs   (BUILDER module - player builds keep post-processing)
//  SHIFTERS creates its post-processing Volume profile at RUNTIME (PostFx), so URP's global option
//  "Strip Unused Post Processing Variants And Resources" finds no VolumeProfile asset using it and strips
//  every UberPost variant (+ Bloom/DoF/... resources). In the player UberPostProcessPass is then invalid and
//  URP skips it - and since that pass is the one that writes the camera to the screen, the 3D view vanished
//  in the WebGL build (only the UI was visible) and tonemapping/bloom/grading/vignette and the switch-wheel
//  desaturation / perspective tints were lost.
//  Fix: every player build runs with that option OFF (URP settings API, in memory only) and the original
//  value is restored right after the build. Assets/Settings is never edited (if the build itself saved the
//  settings asset meanwhile, its original bytes are written back).
//  * ShiftersBuilder.BuildWebGL wraps BuildPipeline.BuildPlayer in Begin/Restore explicitly.
//  * ShiftersWebPostFxBuildGuard does the same for every other player build (ShiftersBuilder.BuildPlayer,
//    File > Build Profiles...), ordered before URP's own build preprocessor, and fails builds whose target is not
//    the active platform (URP would strip for the wrong pipeline asset - see ShiftersWebBuildTarget.cs).
// =====================================================================================
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shifters.EditorTools
{
    /// <summary>Turns URP's VolumeProfile-based post-processing stripping off for the duration of a player build.</summary>
    public static class ShiftersWebPostFxStripping
    {
        const string SessionKey = "Shifters.PostFxStripping.RestorePending";

        static bool _active;        // Begin() ran and Restore() has not yet
        static bool _flipped;       // we changed true -> false and must put it back
        static string _assetPath;   // URP global settings asset
        static byte[] _assetBytes;  // its bytes before the build

        /// <summary>True while a build runs with the stripping suspended.</summary>
        public static bool Active => _active;

        /// <summary>Current value of URP's "Strip Unused Post Processing Variants" (false if URP settings are missing).</summary>
        public static bool StrippingEnabled => TryGetSetting(out var s) && s.stripUnusedPostProcessingVariants;

        /// <summary>Disable the stripping for the next build. Idempotent; pair with <see cref="Restore"/>.</summary>
        public static void Begin(string reason)
        {
            if (_active) return;
            _active = true;
            _flipped = false;
            if (!TryGetSetting(out var s))
            {
                Debug.LogWarning("[SHIFTERS/Build] " + reason + ": URPShaderStrippingSetting not found - post-processing shaders may be stripped from the player.");
                return;
            }
            if (!s.stripUnusedPostProcessingVariants)
            {
                Debug.Log("[SHIFTERS/Build] " + reason + ": URP post-processing stripping is already off - UberPost/Bloom shaders stay in the player.");
                return;
            }
            _assetPath = GlobalSettingsPath();
            _assetBytes = !string.IsNullOrEmpty(_assetPath) && File.Exists(_assetPath) ? File.ReadAllBytes(_assetPath) : null;
            s.stripUnusedPostProcessingVariants = false;
            _flipped = true;
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[SHIFTERS/Build] " + reason + ": URP 'Strip Unused Post Processing Variants' is OFF for this build "
                      + "(SHIFTERS builds its Volume profiles at runtime; the stripper would remove UberPost and the 3D view).");
        }

        /// <summary>Put the original setting back (no-op when nothing is suspended).</summary>
        public static void Restore()
        {
            if (!_active && !SessionState.GetBool(SessionKey, false)) return;
            bool flipped = _flipped || SessionState.GetBool(SessionKey, false);
            _active = false;
            _flipped = false;
            SessionState.EraseBool(SessionKey);
            if (!flipped) return;
            if (TryGetSetting(out var s) && !s.stripUnusedPostProcessingVariants) s.stripUnusedPostProcessingVariants = true;
            RestoreAssetBytes();
            Debug.Log("[SHIFTERS/Build] URP 'Strip Unused Post Processing Variants' restored to ON (project setting unchanged).");
        }

        static void RestoreAssetBytes()
        {
            string path = _assetPath;
            byte[] original = _assetBytes;
            _assetPath = null;
            _assetBytes = null;
            if (string.IsNullOrEmpty(path) || original == null || !File.Exists(path)) return;
            byte[] now = File.ReadAllBytes(path);
            if (SameBytes(now, original)) return;
            // the build saved the in-memory (stripping off) state to disk: write the original back and reload it
            File.WriteAllBytes(path, original);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log("[SHIFTERS/Build] restored " + path + " on disk (the build had saved the temporary value).");
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static bool TryGetSetting(out URPShaderStrippingSetting setting)
        {
            setting = null;
            return GraphicsSettings.TryGetRenderPipelineSettings(out setting) && setting != null;
        }

        static string GlobalSettingsPath()
        {
            var gs = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            return gs != null ? AssetDatabase.GetAssetPath(gs) : null;
        }

        // A build that failed never reaches the post-process callback: restore on the first editor tick after it.
        internal static void RestoreWhenBuildEnds()
        {
            EditorApplication.update -= PollBuildEnd;
            EditorApplication.update += PollBuildEnd;
        }

        static void PollBuildEnd()
        {
            if (BuildPipeline.isBuildingPlayer) return;
            EditorApplication.update -= PollBuildEnd;
            Restore();
        }

        [InitializeOnLoadMethod]
        static void RestoreAfterDomainReload()
        {
            // a build interrupted by a script reload leaves the session flag behind: finish the restore
            if (SessionState.GetBool(SessionKey, false)) EditorApplication.delayCall += Restore;
        }
    }

    /// <summary>Applies <see cref="ShiftersWebPostFxStripping"/> to every player build (standalone too) and refuses builds
    /// whose target is not the active platform (see ShiftersWebBuildTarget). Runs before URP's ShaderBuildPreprocessor
    /// (callbackOrder 0), which reads the setting when it gathers the build features.</summary>
    sealed class ShiftersWebPostFxBuildGuard : IPreprocessBuildWithContext, IPostprocessBuildWithContext
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildCallbackContext ctx)
        {
            string mismatch = ShiftersWebBuildTarget.MismatchProblem(ctx.Report.summary.platform);
            if (mismatch != null)
            {
                ShiftersWebPostFxStripping.Restore();
                throw new BuildFailedException("[SHIFTERS/Build] " + mismatch);
            }
            if (ShiftersWebPostFxStripping.Active) return;   // ShiftersBuilder.BuildWebGL already handles it
            ShiftersWebPostFxStripping.Begin("Player build (" + ctx.Report.summary.platform + ")");
            ShiftersWebPostFxStripping.RestoreWhenBuildEnds();
        }

        public void OnPostprocessBuild(BuildCallbackContext ctx) => ShiftersWebPostFxStripping.Restore();
    }
}
