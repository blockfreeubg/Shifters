// SHIFTERS - Shifters/Hologram : transparent (alpha or additive) hologram with fresnel edge glow, animated
// scanlines, sweep band, flicker and optional world grid. ZWrite off. Holograms, IQ paths, scan pulse, shields.
Shader "Shifters/Hologram"
{
    Properties
    {
        [MainColor][HDR] _BaseColor("Color", Color) = (0.69, 0.49, 1, 0.6)
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        _Alpha("Alpha", Range(0, 1)) = 1
        _VertexColorStrength("Vertex Color Strength", Range(0, 1)) = 1
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 2.2
        _FresnelStrength("Fresnel Strength", Range(0, 4)) = 1.4
        _ScanlineDensity("Scanline Density (lines/m)", Float) = 9
        _ScanlineSpeed("Scanline Speed", Float) = 1.2
        _ScanlineStrength("Scanline Strength", Range(0, 1)) = 0.55
        _SweepStrength("Sweep Strength", Range(0, 3)) = 0.8
        _FlickerStrength("Flicker Strength", Range(0, 1)) = 0.25
        _FlickerSpeed("Flicker Speed", Float) = 14
        _GridScale("Grid Scale (m)", Float) = 1
        _GridStrength("Grid Strength", Range(0, 2)) = 0
        _MinAlpha("Body Alpha", Range(0, 1)) = 0.35
        [HideInInspector] _SrcBlend("__src", Float) = 5
        [HideInInspector] _DstBlend("__dst", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HoloVertex
            #pragma fragment HoloFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ShiftersCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Alpha;
                float  _VertexColorStrength;
                float  _FresnelPower;
                float  _FresnelStrength;
                float  _ScanlineDensity;
                float  _ScanlineSpeed;
                float  _ScanlineStrength;
                float  _SweepStrength;
                float  _FlickerStrength;
                float  _FlickerSpeed;
                float  _GridScale;
                float  _GridStrength;
                float  _MinAlpha;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HoloVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = float4(Shifters_SRGBToLinear(saturate(input.color.rgb)), input.color.a);
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            half4 HoloFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndv = abs(dot(n, v));
                float fres = pow(1.0 - ndv, _FresnelPower) * _FresnelStrength;

                float scan = frac(input.positionWS.y * _ScanlineDensity - _Time.y * _ScanlineSpeed);
                float scanBand = smoothstep(0.0, 0.2, scan) * (1.0 - smoothstep(0.45, 0.65, scan));
                float scanTerm = lerp(1.0, 0.45 + 0.9 * scanBand, _ScanlineStrength);
                float sweep = pow(frac(input.positionWS.y * 0.12 - _Time.y * 0.3), 24.0) * _SweepStrength;

                float flickerRand = Shifters_Hash21(float2(floor(_Time.y * _FlickerSpeed), 7.13));
                float flicker = 1.0 - _FlickerStrength * step(0.8, flickerRand) * 0.7;

                float2 gc = Shifters_PlanarCoord(input.positionWS, n) / max(_GridScale, 0.01);
                float2 gfw = fwidth(gc);
                float2 gd = abs(frac(gc + 0.5) - 0.5);
                float2 gl = 1.0 - smoothstep(0.03 - gfw, 0.03 + gfw, gd);
                float grid = max(gl.x, gl.y) * _GridStrength;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 vc = lerp(half4(1, 1, 1, 1), (half4)input.color, _VertexColorStrength);
                half3 col = _BaseColor.rgb * tex.rgb * vc.rgb;
                half alpha = _BaseColor.a * _Alpha * tex.a * vc.a;

                half glow = (_MinAlpha * scanTerm + fres + sweep + grid);
                half3 color = col * (0.6 + fres * 1.4 + sweep * 1.5 + grid * 1.5) * scanTerm * flicker;
                alpha = saturate(alpha * glow * flicker);
                color = MixFog(color, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
