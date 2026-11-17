// =====================================================================================
//  SHIFTERS - ShiftersEditorSteps.cs   (BUILDER module - editor pipeline)
//  Pipeline steps owned by other modules, invoked by reflection so the builder compiles and runs while
//  those modules are still in progress: TMP Essentials check/import, FontAssetBuilder.BuildAll (UI),
//  MaterialBaker.BakeAll (RENDERING). Also clears runtime caches that may hold non-persisted objects.
// =====================================================================================
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class ShiftersEditorSteps
    {
        public const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        public const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        /// <summary>True if TMP Essentials are present. If missing, imports the package (synchronous in batchmode;
        /// in the interactive Editor the import may finish after this call - run Build Everything again).</summary>
        public static bool EnsureTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath)) return true;
            Debug.Log("[SHIFTERS/Build] TMP Essentials missing - importing " + TmpEssentialsPackage);
            try { UnityEditor.AssetPackage.Package.Import(TmpEssentialsPackage, false); AssetDatabase.Refresh(); }
            catch (Exception e) { Debug.LogWarning("[SHIFTERS/Build] TMP import failed: " + e.Message); }
            bool ok = File.Exists(TmpSettingsPath);
            if (!ok) Debug.LogWarning("[SHIFTERS/Build] TMP Essentials import pending (async). Run SHIFTERS/Build Everything again when it finishes.");
            return ok;
        }

        /// <summary>Invokes Shifters.EditorTools.&lt;typeName&gt;.&lt;method&gt;() if it exists. Returns "ok", "missing" or "failed: ...".</summary>
        public static string RunExternalStep(string typeName, string method)
        {
            var t = FindEditorType("Shifters.EditorTools." + typeName) ?? FindEditorType("Shifters." + typeName);
            if (t == null) return "missing (" + typeName + " not published yet)";
            var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m == null) return "missing (" + typeName + "." + method + "() not found)";
            try { m.Invoke(null, null); return "ok"; }
            catch (TargetInvocationException e)
            {
                var inner = e.InnerException ?? e;
                Debug.LogWarning("[SHIFTERS/Build] " + typeName + "." + method + " threw: " + inner);
                return "failed: " + inner.GetType().Name + ": " + inner.Message;
            }
        }

        public static Type FindEditorType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Clears static caches of ProcMesh and MaterialLibrary (they may hold in-memory objects from earlier
        /// edit-mode work; materials must be re-resolved from freshly baked assets).</summary>
        public static void ResetRuntimeCaches()
        {
            ClearStaticDictionary(typeof(ProcMesh), "_cache");
            ClearStaticDictionary(typeof(MaterialLibrary), "_cache");
            var kitReset = typeof(KitCatalog).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            kitReset?.Invoke(null, null);
            var regReset = typeof(LevelBuilderRegistry).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            regReset?.Invoke(null, null);
        }

        static void ClearStaticDictionary(Type t, string field)
        {
            var f = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Static);
            if (f?.GetValue(null) is System.Collections.IDictionary d) d.Clear();
            else Debug.LogWarning("[SHIFTERS/Build] could not clear " + t.Name + "." + field);
        }

        /// <summary>Command line value after flag (null if absent).</summary>
        public static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == flag) return args[i + 1];
            return null;
        }
    }
}
