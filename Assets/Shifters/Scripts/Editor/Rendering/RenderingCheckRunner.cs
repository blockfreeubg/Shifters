// =====================================================================================
//  SHIFTERS - RenderingCheckRunner.cs   (RENDERING - editor)
//  One-Unity-run helpers for the rendering module's tests:
//    BakeThenPlay : MaterialBaker.BakeAll -> VerifyBaked -> WebGL (GLES3) shader compile check -> DevPlayRunner.Play
//                   devplay-style:  unity_batch.sh <clone> <log> -executeMethod Shifters.EditorTools.RenderingCheckRunner.BakeThenPlay
//                                   -devSetup ... -devScene ... -devShots ... -devResult ...
//    WebGLShaderCheck : compiles every pass of every Assets/Shifters/Art/Shaders shader for ShaderCompilerPlatform.GLES3x /
//                   BuildTarget.WebGL (no keywords + a representative URP keyword set) and logs
//                   "[SHIFTERS/WebGLShaders] RESULT ok=<n> failed=<m> keywords=<k>" (k = declared keyword count per shader).
// =====================================================================================
using System;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shifters.EditorTools
{
    public static class RenderingCheckRunner
    {
        public static void BakeThenPlay()
        {
            try { MaterialBaker.BakeAll(); MaterialBaker.VerifyBaked(); }
            catch (Exception e) { Debug.LogError("[SHIFTERS/Materials] bake failed: " + e); }
            try { WebGLShaderCheck(); }
            catch (Exception e) { Debug.LogError("[SHIFTERS/WebGLShaders] check threw: " + e); }
            DevPlayRunner.Play();
        }

        static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT" },
            new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "INSTANCING_ON" },
        };

        [MenuItem("SHIFTERS/Check WebGL Shaders", false, 31)]
        public static int WebGLShaderCheck()
        {
            int ok = 0, failed = 0;
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shifters/Art/Shaders" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;
                bool hasErr = ShaderUtil.ShaderHasError(shader);
                int kw = (int)shader.keywordSpace.keywordCount;
                sb.Append("  ").Append(shader.name).Append(" keywords=").Append(kw).Append(hasErr ? " HAS_ERROR" : "").Append('\n');
                if (hasErr) failed++;
                var data = ShaderUtil.GetShaderData(shader);
                if (data == null) continue;
                for (int s = 0; s < data.SubshaderCount; s++)
                {
                    var sub = data.GetSubshader(s);
                    for (int p = 0; p < sub.PassCount; p++)
                    {
                        var pass = sub.GetPass(p);
                        if (!pass.HasShaderStage(ShaderType.Vertex)) continue;
                        foreach (var set in KeywordSets)
                            foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                            {
                                if (!pass.HasShaderStage(stage)) continue;
                                try
                                {
                                    var info = pass.CompileVariant(stage, set, ShaderCompilerPlatform.GLES3x, BuildTarget.WebGL);
                                    if (info.Success) ok++;
                                    else
                                    {
                                        failed++;
                                        sb.Append("  FAIL ").Append(shader.name).Append(" sub").Append(s).Append(" pass '").Append(pass.Name)
                                          .Append("' ").Append(stage).Append(" [").Append(string.Join(" ", set)).Append("]\n");
                                        if (info.Messages != null)
                                            foreach (var m in info.Messages)
                                                if (m.severity == ShaderCompilerMessageSeverity.Error) sb.Append("     ").Append(m.message).Append('\n');
                                    }
                                }
                                catch (Exception e)
                                {
                                    sb.Append("  SKIP ").Append(shader.name).Append(" pass '").Append(pass.Name).Append("': ").Append(e.Message).Append('\n');
                                }
                            }
                    }
                }
            }
            Debug.Log("[SHIFTERS/WebGLShaders] RESULT ok=" + ok + " failed=" + failed + "\n" + sb);
            return failed;
        }
    }
}
