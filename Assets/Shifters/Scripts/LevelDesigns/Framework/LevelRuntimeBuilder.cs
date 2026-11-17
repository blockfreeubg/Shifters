// =====================================================================================
//  SHIFTERS - LevelRuntimeBuilder.cs   (BUILDER module - runtime level framework)
//  Builds a code-defined level into the current scene at runtime - the exact same code path as the
//  editor scene builder. Typical DevPlay use (no -devScene => empty default scene):
//      var ctx = LevelRuntimeBuilder.BuildInCurrentScene(0);   // L1
//      yield return ctx2.Wait(1.5f);                           // LevelManager.Start spawns the team
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class LevelRuntimeBuilder
    {
        /// <summary>Context of the most recent runtime build (null if none).</summary>
        public static LevelBuildContext LastContext { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => LastContext = null;

        /// <summary>Finds the ILevelBuilder for levelIndex (LevelCatalog index, or L0_Sandbox.Index / L0_KitGallery.Index)
        /// and builds it into the active scene. Replaces a previous build of the same level. Returns null if no builder.</summary>
        public static LevelBuildContext BuildInCurrentScene(int levelIndex)
        {
            var b = LevelBuilderRegistry.Find(levelIndex);
            if (b == null)
            {
                Debug.LogError("[LevelRuntimeBuilder] no ILevelBuilder with LevelIndex " + levelIndex);
                return null;
            }
            return BuildInCurrentScene(b);
        }

        /// <summary>Builds the given builder into the active scene.</summary>
        public static LevelBuildContext BuildInCurrentScene(ILevelBuilder builder)
        {
            if (builder == null) return null;
            var info = LevelBuilderRegistry.InfoFor(builder);
            var old = GameObject.Find(LevelBuildContext.RootName(info));
            if (old != null) Object.DestroyImmediate(old); // immediate: old LevelMarks must unregister before new ones register
            LastContext = LevelSceneAssembler.Assemble(builder);
            Debug.Log("[LevelRuntimeBuilder] built " + LastContext.Report());
            return LastContext;
        }
    }
}
