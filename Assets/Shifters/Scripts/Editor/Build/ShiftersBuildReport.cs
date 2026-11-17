// =====================================================================================
//  SHIFTERS - ShiftersBuildReport.cs   (BUILDER module - editor pipeline)
//  Scans built scenes/prefabs: object/renderer/mesh/material/light/rigidbody counts and warnings for renderers
//  with missing/default/magenta/non-Shifters materials, missing meshes and embedded (non-asset) meshes.
// =====================================================================================
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class ShiftersBuildReport
    {
        public class Stats
        {
            public string Name;
            public int Objects, Renderers, Lights, ShadowPointLights, Rigidbodies, Colliders, Marks, TextObjects;
            public int MeshesUnique, MeshAssets, MeshEmbedded, MaterialsUnique, MaterialAssets, MaterialEmbedded;
            public int BadMaterials, NonShiftersShaders;
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Errors = new List<string>();
        }

        /// <summary>True for shaders the game may use: Shifters/*, URP particles, TextMeshPro.</summary>
        public static bool IsAllowedShader(Shader s)
        {
            if (s == null) return false;
            string n = s.name;
            return n.StartsWith("Shifters/") || n.StartsWith("Universal Render Pipeline/Particles") || n.StartsWith("TextMeshPro") || n.StartsWith("TMP");
        }

        public static bool ShiftersShadersPresent => Shader.Find("Shifters/StylizedLit") != null;

        public static Stats Scan(string name, IEnumerable<GameObject> roots)
        {
            var st = new Stats { Name = name };
            var meshes = new HashSet<Mesh>();
            var mats = new HashSet<Material>();
            bool strictShaders = ShiftersShadersPresent;
            foreach (var root in roots)
            {
                if (root == null) continue;
                st.Objects += root.GetComponentsInChildren<Transform>(true).Length;
                st.Rigidbodies += root.GetComponentsInChildren<Rigidbody>(true).Length;
                st.Colliders += root.GetComponentsInChildren<Collider>(true).Length;
                st.Marks += root.GetComponentsInChildren<LevelMark>(true).Length;
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    st.Lights++;
                    if (l.type == LightType.Point && l.shadows != LightShadows.None) st.ShadowPointLights++;
                }
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    bool isText = r.GetComponent<TMP_Text>() != null;
                    if (isText) st.TextObjects++;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) { }
                    st.Renderers++;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && !isText)
                    {
                        var m = mf.sharedMesh;
                        if (m == null) Warn(st, "missing mesh: " + Path(r.transform));
                        else if (meshes.Add(m))
                        {
                            if (EditorUtility.IsPersistent(m)) st.MeshAssets++;
                            else { st.MeshEmbedded++; Warn(st, "embedded (non-asset) mesh '" + m.name + "' on " + Path(r.transform)); }
                        }
                    }
                    var shared = r.sharedMaterials;
                    if (shared.Length == 0) { st.BadMaterials++; Error(st, "renderer without materials: " + Path(r.transform)); }
                    foreach (var mat in shared)
                    {
                        if (mat == null) { st.BadMaterials++; Error(st, "null material slot: " + Path(r.transform)); continue; }
                        if (mats.Add(mat)) { if (EditorUtility.IsPersistent(mat)) st.MaterialAssets++; else st.MaterialEmbedded++; }
                        var sh = mat.shader;
                        if (sh == null || sh.name == "Hidden/InternalErrorShader") { st.BadMaterials++; Error(st, "magenta/missing shader on " + Path(r.transform)); }
                        else if (mat.name == "Default-Material" || mat.name == "Default-Diffuse") { st.BadMaterials++; Error(st, "Unity default material on " + Path(r.transform)); }
                        else if (!IsAllowedShader(sh))
                        {
                            st.NonShiftersShaders++;
                            if (strictShaders) Warn(st, "non-Shifters shader '" + sh.name + "' (" + mat.name + ") on " + Path(r.transform));
                        }
                    }
                }
            }
            st.MeshesUnique = meshes.Count;
            st.MaterialsUnique = mats.Count;
            if (!strictShaders && st.NonShiftersShaders > 0)
                st.Warnings.Add(st.NonShiftersShaders + " material slots use fallback shaders (Shifters shaders not in this project yet - rendering module unpublished)");
            if (st.ShadowPointLights > 4) Warn(st, st.ShadowPointLights + " shadow-casting point lights (keep <= 4)");
            if (st.Rigidbodies > 80) Warn(st, st.Rigidbodies + " rigidbodies (keep < ~80 active)");
            return st;
        }

        const int MaxListed = 25;

        static void Warn(Stats st, string msg) { if (st.Warnings.Count < MaxListed) st.Warnings.Add(msg); else if (st.Warnings.Count == MaxListed) st.Warnings.Add("... more warnings omitted"); }
        static void Error(Stats st, string msg) { if (st.Errors.Count < MaxListed) st.Errors.Add(msg); else if (st.Errors.Count == MaxListed) st.Errors.Add("... more errors omitted"); }

        static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null && sb.Length < 120; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        public static string Format(Stats s)
        {
            var sb = new StringBuilder();
            sb.Append("  [").Append(s.Name).Append("] objects ").Append(s.Objects).Append(", renderers ").Append(s.Renderers)
              .Append(" (text ").Append(s.TextObjects).Append("), meshes ").Append(s.MeshesUnique).Append(" (assets ").Append(s.MeshAssets)
              .Append(", embedded ").Append(s.MeshEmbedded).Append("), materials ").Append(s.MaterialsUnique).Append(" (assets ").Append(s.MaterialAssets)
              .Append(", in-scene ").Append(s.MaterialEmbedded).Append("), lights ").Append(s.Lights).Append(", rigidbodies ").Append(s.Rigidbodies)
              .Append(", colliders ").Append(s.Colliders).Append(", marks ").Append(s.Marks).Append('\n');
            foreach (var e in s.Errors) sb.Append("    ERROR: ").Append(e).Append('\n');
            foreach (var w in s.Warnings) sb.Append("    warning: ").Append(w).Append('\n');
            return sb.ToString();
        }
    }
}
