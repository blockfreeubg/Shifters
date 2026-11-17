// SHIFTERS - Shifters/StylizedLit : the game's main lit shader (URP 17.6, Forward+ ready, SRP Batcher compatible).
// Soft-stepped toon diffuse, cool shadow tint, toon specular, fresnel rim, vertex color multiply, emission,
// perspective reveal glow (_RevealColor/_RevealStrength), world grid lines, noise variation, UV stripes.
Shader "Shifters/StylizedLit"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _VertexColorStrength("Vertex Color Strength", Range(0, 1)) = 1

        [Header(Toon Shading)]
        _ShadeSteps("Shade Steps", Range(1, 6)) = 3
        _ShadeSoftness("Shade Softness", Range(0.001, 1)) = 0.45
        _WrapLighting("Wrap (half-lambert)", Range(0, 1)) = 0.3
        _ShadowTint("Shadow Tint", Color) = (0.78, 0.82, 1, 1)

        [Header(Specular and Rim)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.3
        _SpecularStrength("Specular Strength", Range(0, 2)) = 0.5
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength("Rim Strength", Range(0, 2)) = 0.2

        [Header(Emission and Reveal)]
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        [HDR] _RevealColor("Reveal Color", Color) = (0.69, 0.49, 1, 1)
        _RevealStrength("Reveal Strength", Range(0, 1)) = 0
        _RevealPulse("Reveal Pulse Speed", Float) = 5

        [Header(World Grid)]
        _GridScale("Grid Scale (m)", Float) = 1
        _GridStrength("Grid Strength", Range(0, 1)) = 0
        [HDR] _GridColor("Grid Color", Color) = (0, 0, 0, 0.6)
        _GridWidth("Grid Line Width", Range(0.001, 0.25)) = 0.02
        _GridGlow("Grid Glow", Range(0, 4)) = 0

        [Header(Variation)]
        _NoiseStrength("Noise Strength", Range(0, 0.5)) = 0.05
        _NoiseScale("Noise Scale", Float) = 0.35

        [Header(Stripes (UV space))]
        _StripeColor("Stripe Color", Color) = (0.1, 0.1, 0.12, 1)
        _StripeWidth("Stripe Width (m)", Float) = 0.3
        _StripeStrength("Stripe Strength", Range(0, 1)) = 0

        [Header(World Color)]
        _GreyImmune("Grey Immune (keeps color in the grey world)", Range(0, 1)) = 0

        [Header(Character Fill)]
        _CharacterFill("Character Fill (min light level in dark scenes; interns only)", Range(0, 1)) = 0

        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface("__surface", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StylizedLitVertex
            #pragma fragment StylizedLitFragment

            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "StylizedLitInput.hlsl"
            #include "StylizedLitForward.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "StylizedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "StylizedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "StylizedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
