// =====================================================================================
//  SHIFTERS - ShiftersProjectSetup.cs   (BUILDER module - editor pipeline)
//  [InitializeOnLoad]: when the editor loads and the SHIFTERS scenes exist, Build Settings are corrected
//  (MainMenu + L1..L5, SampleScene disabled). Cheap and silent when already correct.
// =====================================================================================
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shifters.EditorTools
{
    /// <summary>On editor load: if the SHIFTERS scenes exist, make sure Build Settings are correct (silent when they are).</summary>
    [InitializeOnLoad]
    public static class ShiftersProjectSetup
    {
        static ShiftersProjectSetup() => EditorApplication.delayCall += Run;

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(LevelCatalog.ScenePath(LevelCatalog.MainMenuScene))) return;
            try { ShiftersBuildSettings.Apply(); }
            catch (System.Exception e) { Debug.LogWarning("[SHIFTERS] could not apply Build Settings: " + e.Message); }
        }
    }
}
