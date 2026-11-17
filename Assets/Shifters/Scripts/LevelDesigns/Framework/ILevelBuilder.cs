// =====================================================================================
//  SHIFTERS - ILevelBuilder.cs   (BUILDER module - runtime level framework)
//  One class per level (Scripts/LevelDesigns/L<n>/L<n>Builder.cs) builds the whole level in code.
//  The SAME code runs at runtime (LevelRuntimeBuilder, DevPlay tests) and in the editor
//  (ShiftersBuilder saves the result as Assets/Shifters/Scenes/<SceneName>.unity).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Code-defined level. Needs a public parameterless constructor. LevelIndex = LevelCatalog index (0 = L1).</summary>
    public interface ILevelBuilder
    {
        int LevelIndex { get; }
        void Build(LevelBuildContext ctx);
    }

    /// <summary>Optional, for scenes that are NOT in LevelCatalog (sandbox, galleries): describes the scene
    /// (SceneName, Theme, titles). Such scenes are never added to Build Settings.</summary>
    public interface ILevelSceneInfo
    {
        LevelInfo SceneInfo { get; }
    }

    /// <summary>Finds ILevelBuilder implementations by reflection (cached).</summary>
    public static class LevelBuilderRegistry
    {
        static List<ILevelBuilder> _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all = null;

        /// <summary>Every builder in the loaded assemblies, sorted by LevelIndex.</summary>
        public static IReadOnlyList<ILevelBuilder> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<ILevelBuilder>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || t.IsInterface || !typeof(ILevelBuilder).IsAssignableFrom(t)) continue;
                        if (t.GetConstructor(Type.EmptyTypes) == null) { Debug.LogWarning("[LevelBuilder] " + t.Name + " needs a public parameterless constructor"); continue; }
                        try { _all.Add((ILevelBuilder)Activator.CreateInstance(t)); }
                        catch (Exception e) { Debug.LogWarning("[LevelBuilder] cannot create " + t.Name + ": " + e.Message); }
                    }
                }
                _all.Sort((a, b) => a.LevelIndex.CompareTo(b.LevelIndex));
                return _all;
            }
        }

        /// <summary>Builder for a level index (LevelCatalog index or a special index such as L0_Sandbox.Index), or null.</summary>
        public static ILevelBuilder Find(int levelIndex)
        {
            ILevelBuilder found = null;
            foreach (var b in All)
            {
                if (b.LevelIndex != levelIndex) continue;
                if (found != null) Debug.LogWarning("[LevelBuilder] two builders for index " + levelIndex + ": " + found.GetType().Name + " and " + b.GetType().Name + " (using the first)");
                else found = b;
            }
            return found;
        }

        /// <summary>LevelInfo for a builder: its ILevelSceneInfo, else the LevelCatalog entry, else a generic one.</summary>
        public static LevelInfo InfoFor(ILevelBuilder b)
        {
            if (b is ILevelSceneInfo si && si.SceneInfo != null) return si.SceneInfo;
            var info = LevelCatalog.Get(b.LevelIndex);
            if (info != null) return info;
            return new LevelInfo(b.LevelIndex, "L_" + b.GetType().Name, b.GetType().Name.ToUpperInvariant(), "Custom level", "", LevelTheme.Training, MusicId.Training, "Reach the exit.");
        }

        /// <summary>True if the builder's index is a LevelCatalog level (goes to Build Settings).</summary>
        public static bool IsCatalogLevel(ILevelBuilder b) => !(b is ILevelSceneInfo) && LevelCatalog.Get(b.LevelIndex) != null;
    }
}
