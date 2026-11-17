// SHIFTERS - Shifters/Acid : unmistakable toxic acid (MatKey.Acid). Opaque, self-illuminated saturated green that
// boils (two scrolling 3D value-noise layers + bright crest veins), procedural bubbles that swell and pop on top
// faces and rise on side faces (tank liquid, glow strips), a glowing fresnel edge (never reads as a matte floor from
// grazing angles) and a sun glint on a wobbling normal. HDR crests/bubbles feed bloom. WebGL2-safe: target 3.0,
// vertex/fragment only, no textures, fog. SRP Batcher compatible (single UnityPerMaterial CBUFFER in every pass).
Shader "Shifters/Acid"
{
    Properties
    {
        [MainColor] _BaseColor("Acid Color", Color) = (0.39, 0.9, 0.2, 1)
        _DeepColor("Deep Color", Color) = (0.09, 0.36, 0.02, 1)
        [HDR] _GlowColor("Crest Glow (HDR)", Color) = (0.59, 1.46, 0.28, 1)
        [HDR] _FoamColor("Bubble Rim (HDR)", Color) = (1.19, 1.43, 0.64, 1)
        _CellScale("Boil Pattern Scale (1/m)", Float) = 0.9
        _FlowSpeed("Boil Speed", Float) = 0.45
        _BubbleDensity("Bubbles per m", Float) = 1.5
        _BubbleRate("Bubble Rate", Float) = 0.5
        _RiseSpeed("Side Bubble Rise (m/s)", Float) = 0.35
        _Emission("Self Illumination", Range(0, 3)) = 1
        _FresnelGlow("Edge Glow", Range(0, 2)) = 0.7
        _Glint("Sun Glint", Range(0, 3)) = 1.2
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _DeepColor;
            float4 _GlowColor;
            float4 _FoamColor;
            float  _CellScale;
            float  _FlowSpeed;
            float  _BubbleDensity;
            float  _BubbleRate;
            float  _RiseSpeed;
            float  _Emission;
            float  _FresnelGlow;
            float  _Glint;
            float  _Cutoff;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex AcidVertex
            #pragma fragment AcidFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "ShiftersCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings AcidVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            // One bubble per cell (~50% of cells): swells, shows a bright rim + soft dome, pops at the end of its life.
            // aa = screen-space size of one pattern unit (from the continuous coordinate; fwidth of the per-cell distance
            // would spike on cell borders).
            float AcidBubbleLayer(float2 p, float t, float rate, float aa)
            {
                float2 cell = floor(p);
                float2 f = frac(p) - 0.5;
                float h = Shifters_Hash21(cell);
                float h2 = Shifters_Hash21(cell + 17.31);
                float life = frac(t * rate * (0.55 + h2 * 0.9) + h * 7.13);
                float2 c = (float2(h, h2) - 0.5) * 0.4;
                float r = (0.1 + 0.2 * h2) * smoothstep(0.0, 0.8, life);
                float d = length(f - c);
                float rim = 1.0 - smoothstep(0.0, aa + 0.022, abs(d - r));
                float dome = (1.0 - smoothstep(r * 0.5, r + aa, d)) * 0.3;
                float alive = smoothstep(0.0, 0.08, life) * (1.0 - smoothstep(0.82, 1.0, life));
                float exists = step(0.5, frac(h * 13.7));
                return (rim + dome) * alive * exists;
            }

            half4 AcidFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float t = _Time.y;
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float upness = saturate(abs(n.y) * 2.0 - 0.6);          // 1 on the pool surface, 0 on tank/strip sides
                float2 p = Shifters_PlanarCoord(input.positionWS, n);    // xz on top faces, (z|x, y) on side faces
                float rise = (1.0 - upness) * t * _RiseSpeed;

                // boiling body: two drifting noise layers + bright crests
                float3 q = float3(p.x * _CellScale, (p.y - rise) * _CellScale, t * _FlowSpeed);
                float n1 = Shifters_ValueNoise3D(q);
                float n2 = Shifters_ValueNoise3D(q * float3(2.3, 2.3, 1.7) + 5.2);
                float boil = n1 * 0.62 + n2 * 0.38;
                float crest = 1.0 - abs(n1 - 0.5) * 2.0;
                crest *= crest; crest *= crest; crest *= crest;          // ^8: thin bright veins

                // bubbles: two layers (big slow, small fast); on side faces they rise
                float2 pb = float2(p.x, p.y - rise * 1.6) * _BubbleDensity;
                float aa = max(fwidth(pb.x), fwidth(pb.y)) * 1.2 + 0.004;
                float bub = AcidBubbleLayer(pb, t, _BubbleRate, aa) + 0.45 * AcidBubbleLayer(pb * 2.1 + 3.7, t * 1.35, _BubbleRate, aa * 2.1);

                half3 col = lerp(_DeepColor.rgb, _BaseColor.rgb, smoothstep(0.18, 0.82, boil));
                col += _GlowColor.rgb * (crest * 0.5 + boil * boil * boil * 0.3);
                col = lerp(col, _FoamColor.rgb, saturate(bub) * 0.9);

                // glowing edge at grazing angles (the acid never reads as a matte floor)
                float ndv = saturate(dot(n, v));
                float fres = 1.0 - ndv;
                col += _GlowColor.rgb * (fres * fres * fres) * _FresnelGlow;
                col *= _Emission;

                // sun glint on a wobbling liquid normal
                Light sun = GetMainLight();
                float3 wn = normalize(n + float3(n1 - 0.5, 0.0, n2 - 0.5) * 0.7 * upness);
                float3 hv = SafeNormalize(sun.direction + v);
                float spec = pow(saturate(dot(wn, hv)), 96.0);
                col += sun.color * spec * _Glint;

                col = MixFog(col, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
                return half4(col, 1.0);
            }
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
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
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
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
