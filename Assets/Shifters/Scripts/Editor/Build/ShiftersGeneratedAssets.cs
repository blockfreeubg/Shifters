// =====================================================================================
//  SHIFTERS - ShiftersGeneratedAssets.cs   (BUILDER module - editor pipeline)
//  Persists generated objects as assets with DETERMINISTIC GUIDs (md5 of the asset path), so the same mesh
//  built in any agent's clone gets the same GUID -> scenes from different clones stay valid after rsync.
//    * ProcMesh.PersistHook (InstallHook): every ProcMesh.Cached key -> Art/Meshes/Generated/<key>.asset
//      (overwritten in place when content changed, untouched when identical, deduped by key).
//    * PersistEmbedded: walks every serialized object reference under some roots and persists in-memory
//      Meshes (always) and Materials/Textures/PhysicsMaterials/other assets (when includeAll, e.g. prefabs)
//      into Generated/_extra|_materials|_textures|_misc, named <name>_<contentHash>.
//    * SaveAsset: public helper other editor steps can use for GUID-stable generated assets.
// =====================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shifters.EditorTools
{
    public static class ShiftersGeneratedAssets
    {
        public const string MeshFolder = "Assets/Shifters/Art/Meshes/Generated";
        public const string ExtraMeshFolder = MeshFolder + "/_extra";
        public const string MaterialFolder = MeshFolder + "/_materials";
        public const string TextureFolder = MeshFolder + "/_textures";
        public const string MiscFolder = MeshFolder + "/_misc";
        const string TempFolder = MeshFolder + "/__tmp";

        public static int Created, Updated, Unchanged;
        static readonly Dictionary<string, Mesh> _hookMeshes = new Dictionary<string, Mesh>();

        public static void ResetStats() { Created = Updated = Unchanged = 0; _hookMeshes.Clear(); }

        /// <summary>Removes the temporary folder used while creating assets (call at the end of a build).</summary>
        public static void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }
        public static string Stats => "assets created " + Created + ", updated " + Updated + ", unchanged " + Unchanged;

        // ------------------------------------------------------------------ hook
        sealed class HookScope : IDisposable
        {
            public void Dispose() { ProcMesh.PersistHook = null; }
        }

        /// <summary>Installs ProcMesh.PersistHook until the returned scope is disposed.</summary>
        public static IDisposable InstallHook()
        {
            ProcMesh.PersistHook = PersistCachedMesh;
            return new HookScope();
        }

        /// <summary>ProcMesh.PersistHook implementation.</summary>
        public static Mesh PersistCachedMesh(string key, Mesh mesh)
        {
            if (mesh == null || string.IsNullOrEmpty(key)) return mesh;
            string path = MeshFolder + "/" + SanitizeFileName(key) + ".asset";
            if (_hookMeshes.TryGetValue(path, out var done) && done != null)
            {
                if (done != mesh && !EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
                return done;
            }
            mesh.name = key;
            var saved = SaveAsset(mesh, path);
            if (saved != null && saved != mesh && !EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
            if (saved != null) _hookMeshes[path] = saved;
            return saved != null ? saved : mesh;
        }

        // ------------------------------------------------------------------ deterministic asset writing
        /// <summary>GUID derived from the asset path (lowercase md5 hex) - identical in every project copy.</summary>
        public static string DeterministicGuid(string assetPath)
            => Md5Hex("shifters-asset:" + assetPath.Replace('\\', '/').ToLowerInvariant());

        /// <summary>Saves obj at path (the input object is never consumed). Existing asset: content compared and
        /// overwritten in place (GUID kept). New asset: written with DeterministicGuid(path). Returns the asset.</summary>
        public static T SaveAsset<T>(T obj, string path) where T : Object
        {
            if (obj == null) return null;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                if (existing == obj) return existing;
                if (ContentHash(existing) == ContentHash(obj)) { Unchanged++; return existing; }
                string keepName = existing.name;
                EditorUtility.CopySerialized(obj, existing);
                existing.name = keepName;
                EditorUtility.SetDirty(existing);
                Updated++;
                return existing;
            }
            var fast = TryWriteDirect(obj, path);
            if (fast != null) { Created++; return fast; }
            // fallback: create under a temp folder with the SAME file name (CreateAsset names the object after the
            // file), then copy the file next to a .meta carrying the deterministic GUID and import it there
            EnsureFolder(TempFolder);
            string tmp = TempFolder + "/" + Path.GetFileName(path);
            if (AssetDatabase.LoadMainAssetAtPath(tmp) != null || File.Exists(tmp)) AssetDatabase.DeleteAsset(tmp);
            var copy = Object.Instantiate(obj);
            copy.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, tmp);
            string meta = File.ReadAllText(tmp + ".meta");
            meta = Regex.Replace(meta, "guid: [0-9a-fA-F]{32}", "guid: " + DeterministicGuid(path));
            File.Copy(tmp, path, true);
            File.WriteAllText(path + ".meta", meta);
            AssetDatabase.DeleteAsset(tmp);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<T>(path);
            if (loaded == null) Debug.LogError("[SHIFTERS/Build] failed to persist " + path);
            else Created++;
            return loaded;
        }

        static readonly Regex HeaderRx = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Multiline);

        /// <summary>Fast path (one import instead of three refreshes): serialize a copy straight to path as YAML,
        /// normalize the main object's fileID to classID*100000 (what CreateAsset uses, so assets written either way
        /// are interchangeable), write a NativeFormatImporter .meta with the deterministic GUID, import once.
        /// Returns null (and cleans up) if anything is unexpected; the caller then uses the CreateAsset path.</summary>
        static T TryWriteDirect<T>(T obj, string path) where T : Object
        {
            string ext = Path.GetExtension(path);
            if (ext != ".asset" && ext != ".mat") return null;
            Object copy = null;
            try
            {
                copy = Object.Instantiate(obj);
                copy.name = Path.GetFileNameWithoutExtension(path);
                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[] { copy }, path, true);
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path);
                var headers = HeaderRx.Matches(text);
                if (!text.StartsWith("%YAML") || headers.Count != 1) { File.Delete(path); return null; }
                string classId = headers[0].Groups[1].Value, oldId = headers[0].Groups[2].Value;
                long mainId = long.Parse(classId) * 100000L;
                if (oldId != mainId.ToString())
                {
                    text = text.Replace("--- !u!" + classId + " &" + oldId, "--- !u!" + classId + " &" + mainId)
                               .Replace("{fileID: " + oldId + "}", "{fileID: " + mainId + "}");
                    File.WriteAllText(path, text);
                }
                File.WriteAllText(path + ".meta",
                    "fileFormatVersion: 2\nguid: " + DeterministicGuid(path) + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: "
                    + mainId + "\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var loaded = AssetDatabase.LoadAssetAtPath<T>(path);
                if (loaded != null) return loaded;
                Debug.LogWarning("[SHIFTERS/Build] direct write of " + path + " did not import - using CreateAsset");
            }
            catch (Exception e) { Debug.LogWarning("[SHIFTERS/Build] direct write of " + path + " failed: " + e.Message); }
            finally { if (copy != null) Object.DestroyImmediate(copy); }
            AssetDatabase.DeleteAsset(path);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
            return null;
        }

        // ------------------------------------------------------------------ embedded objects
        /// <summary>Persists in-memory objects referenced by any component under roots and re-points the references.
        /// Meshes always; materials/textures/physics materials/scriptable objects only when includeAll (prefabs).
        /// Skips DontSave objects (e.g. TextMeshPro's runtime mesh). Returns the number of references re-pointed.</summary>
        public static int PersistEmbedded(IEnumerable<GameObject> roots, bool includeAll)
        {
            var map = new Dictionary<Object, Object>();
            int repointed = 0;
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null || comp is Transform) continue;
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var o = it.objectReferenceValue;
                        if (o == null || EditorUtility.IsPersistent(o) || (o.hideFlags & HideFlags.DontSave) != 0) continue;
                        if (o is GameObject || o is Component) continue; // scene objects
                        if (!map.TryGetValue(o, out var rep)) { rep = PersistObject(o, includeAll); map[o] = rep; }
                        if (rep != null && rep != o) { it.objectReferenceValue = rep; changed = true; repointed++; }
                    }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            return repointed;
        }

        static Object PersistObject(Object o, bool includeAll)
        {
            string baseName = SanitizeFileName(string.IsNullOrEmpty(o.name) ? o.GetType().Name : o.name, 60);
            switch (o)
            {
                case Mesh m:
                    return SaveAsset(m, ExtraMeshFolder + "/" + baseName + "_" + ContentHash(m).Substring(0, 10) + ".asset");
                case Material mat:
                    if (!includeAll) return o;
                    var clone = new Material(mat) { name = mat.name };
                    foreach (var prop in mat.GetTexturePropertyNames())
                    {
                        var tex = mat.GetTexture(prop) as Texture2D;
                        if (tex != null && !EditorUtility.IsPersistent(tex) && (tex.hideFlags & HideFlags.DontSave) == 0)
                            clone.SetTexture(prop, (Texture2D)PersistObject(tex, true));
                    }
                    var savedMat = SaveAsset(clone, MaterialFolder + "/" + baseName + "_" + ContentHash(clone).Substring(0, 10) + ".mat");
                    Object.DestroyImmediate(clone);
                    return savedMat;
                case Texture2D tex2:
                    if (!includeAll) return o;
                    if (!tex2.isReadable) { Debug.LogWarning("[SHIFTERS/Build] cannot persist unreadable texture " + tex2.name); return o; }
                    return SaveAsset(tex2, TextureFolder + "/" + baseName + "_" + ContentHash(tex2).Substring(0, 10) + ".asset");
                case PhysicsMaterial pm:
                    if (!includeAll) return o;
                    return SaveAsset(pm, MiscFolder + "/" + baseName + "_" + ContentHash(pm).Substring(0, 10) + ".asset");
                case ScriptableObject so:
                    if (!includeAll) return o;
                    return SaveAsset(so, MiscFolder + "/" + baseName + "_" + ContentHash(so).Substring(0, 10) + ".asset");
                default:
                    return o;
            }
        }

        // ------------------------------------------------------------------ hashing
        /// <summary>Deterministic content hash (mesh geometry, material shader/properties, texture pixels, else JSON).</summary>
        public static string ContentHash(Object o)
        {
            switch (o)
            {
                case Mesh m: return MeshHash(m);
                case Material mat: return MaterialHash(mat);
                case Texture2D t: return TextureHash(t);
                default: return Md5Hex(o.GetType().Name + EditorJsonUtility.ToJson(o));
            }
        }

        static string MeshHash(Mesh m)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(m.vertexCount); w.Write(m.subMeshCount); w.Write((int)m.indexFormat);
                foreach (var v in m.vertices) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
                foreach (var v in m.normals) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
                foreach (var c in m.colors32) { w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a); }
                foreach (var u in m.uv) { w.Write(u.x); w.Write(u.y); }
                for (int s = 0; s < m.subMeshCount; s++) foreach (var i in m.GetTriangles(s)) w.Write(i);
                w.Flush();
                return Md5Hex(ms.ToArray());
            }
        }

        static string MaterialHash(Material mat)
        {
            var sb = new StringBuilder();
            sb.Append(mat.shader != null ? mat.shader.name : "null").Append('|').Append(mat.renderQueue).Append('|');
            var kw = mat.shaderKeywords; Array.Sort(kw, StringComparer.Ordinal); sb.Append(string.Join(",", kw)).Append('|');
            var sh = mat.shader;
            if (sh != null)
            {
                for (int i = 0; i < sh.GetPropertyCount(); i++)
                {
                    string n = sh.GetPropertyName(i);
                    sb.Append(n).Append('=');
                    switch (sh.GetPropertyType(i))
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Color: sb.Append(mat.GetColor(n)); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector: sb.Append(mat.GetVector(n)); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                        case UnityEngine.Rendering.ShaderPropertyType.Range: sb.Append(mat.GetFloat(n).ToString("R", System.Globalization.CultureInfo.InvariantCulture)); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Int: sb.Append(mat.GetInteger(n)); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                            var t = mat.GetTexture(n);
                            sb.Append(t == null ? "none" : EditorUtility.IsPersistent(t) ? AssetDatabase.GetAssetPath(t) + "#" + t.name : "mem:" + (t is Texture2D t2 ? TextureHash(t2) : t.name));
                            sb.Append(mat.GetTextureScale(n)).Append(mat.GetTextureOffset(n));
                            break;
                    }
                    sb.Append(';');
                }
            }
            return Md5Hex(sb.ToString());
        }

        static string TextureHash(Texture2D t)
        {
            var head = t.width + "x" + t.height + "_" + t.format + "_" + t.mipmapCount + "_" + t.filterMode + "_" + t.wrapMode;
            if (!t.isReadable) return Md5Hex(head + t.name);
            var data = t.GetRawTextureData();
            var bytes = new byte[data.Length + head.Length];
            Buffer.BlockCopy(data, 0, bytes, 0, data.Length);
            Encoding.ASCII.GetBytes(head, 0, head.Length, bytes, data.Length);
            return Md5Hex(bytes);
        }

        static string Md5Hex(string s) => Md5Hex(Encoding.UTF8.GetBytes(s));

        static string Md5Hex(byte[] bytes)
        {
            using (var md5 = MD5.Create())
            {
                var h = md5.ComputeHash(bytes);
                var sb = new StringBuilder(32);
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ paths
        /// <summary>Filesystem-safe name: [A-Za-z0-9._-] kept, others '_'; if altered or longer than max, a short
        /// hash of the original is appended so distinct keys never collide.</summary>
        public static string SanitizeFileName(string key, int max = 110)
        {
            var sb = new StringBuilder(key.Length);
            foreach (char c in key) sb.Append(char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '-' || c == '.' ? c : '_');
            string s = sb.ToString().Trim('.');
            bool altered = s != key;
            if (s.Length > max) { s = s.Substring(0, max); altered = true; }
            if (s.Length == 0) { s = "asset"; altered = true; }
            if (altered) s += "_" + Md5Hex(key).Substring(0, 8);
            return s;
        }

        /// <summary>Creates a folder chain under Assets (AssetDatabase-aware).</summary>
        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
