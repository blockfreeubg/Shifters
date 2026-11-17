// =====================================================================================
//  SHIFTERS - ShiftersWebBuildTarget.cs   (BUILDER module - build target consistency)
//  URP's ShaderBuildPreprocessor decides which shader variants to keep from the quality levels of the ACTIVE
//  build target, while Unity's keyword prefiltering uses the platform actually being built. A WebGL build made
//  from a macOS-active editor therefore stripped against PC_RPAsset (Forward+, fast sRGB off) while prefiltering
//  for Mobile_RPAsset (plain Forward, fast sRGB on): Shifters/StylizedLit "ForwardLit" and URP UberPost were left
//  with 0 variants - the 3D world was invisible in the browser and post-processing could not run.
//  EnsureActive() switches the active platform before building (ShiftersBuilder.BuildWebGL does this); the build
//  guard (ShiftersWebPostFxStripping.cs) refuses any player build whose target is not the active platform.
// =====================================================================================
using UnityEditor;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class ShiftersWebBuildTarget
    {
        /// <summary>Makes target the active build target (a platform switch re-imports platform-dependent assets).
        /// Returns false if the switch failed. note describes what happened.</summary>
        public static bool EnsureActive(BuildTarget target, out string note)
        {
            var from = EditorUserBuildSettings.activeBuildTarget;
            if (from == target) { note = "active platform: " + target; return true; }
            var group = BuildPipeline.GetBuildTargetGroup(target);
            Debug.Log("[SHIFTERS/Build] switching the active platform " + from + " -> " + target
                      + " (URP strips shader variants for the ACTIVE platform's quality levels / render pipeline asset)");
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target) && EditorUserBuildSettings.activeBuildTarget == target;
            note = ok ? "active platform switched " + from + " -> " + target + " (stays " + target + " after the build)"
                      : "FAILED to switch the active platform " + from + " -> " + target;
            return ok;
        }

        /// <summary>Null when a player build for buildTarget strips shaders consistently, else the reason it would not.</summary>
        public static string MismatchProblem(BuildTarget buildTarget)
        {
            var active = EditorUserBuildSettings.activeBuildTarget;
            if (buildTarget == active) return null;
            return "this " + buildTarget + " player build runs while the ACTIVE platform is " + active + ": URP would strip shader "
                   + "variants for the wrong render pipeline asset (Shifters/StylizedLit ForwardLit and UberPost end up with 0 variants = "
                   + "invisible 3D, no post-processing). Switch the platform to " + buildTarget + " first (ShiftersBuilder.BuildWebGL does it; "
                   + "scripts: ShiftersWebBuildTarget.EnsureActive) or start Unity with -buildTarget.";
        }
    }
}
