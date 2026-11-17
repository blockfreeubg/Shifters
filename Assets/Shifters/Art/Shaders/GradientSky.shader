// SHIFTERS - Shifters/GradientSky : stylized void skybox. Top / horizon / bottom gradient with horizon sharpness,
// soft sun glow and a few big soft nebula blobs + light shafts for depth. Fog color should match _HorizonColor.
Shader "Shifters/GradientSky"
{
    Properties
    {
        _TopColor("Top", Color) = (0.36, 0.55, 0.85, 1)
        _HorizonColor("Horizon", Color) = (0.86, 0.9, 0.95, 1)
        _BottomColor("Bottom", Color) = (0.55, 0.6, 0.7, 1)
        _HorizonSharpness("Horizon Sharpness", Range(0.3, 16)) = 3.5
        _HorizonOffset("Horizon Offset", Range(-0.5, 0.5)) = 0
        [HDR] _SunColor("Sun Glow Color", Color) = (1, 0.92, 0.8, 1)
        _SunDirection("Sun Direction (towards sun)", Vector) = (0.3, 0.45, 0.6, 0)
        _SunSize("Sun Disc Size", Range(0, 1)) = 0.15
        _SunGlow("Sun Glow", Range(0, 2)) = 0.4
        _NebulaColorA("Nebula A", Color) = (1, 1, 1, 1)
        _NebulaColorB("Nebula B", Color) = (1, 1, 1, 1)
        _NebulaStrength("Nebula Strength", Range(0, 1)) = 0.2
        _ShaftStrength("Light Shafts", Range(0, 1)) = 0.15
        _StarStrength("Stars", Range(0, 2)) = 0
        _Seed("Seed", Float) = 3.7
        [HideInInspector] _GreyImmune("Grey Immune", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ShiftersCommon.hlsl"
            #include "ShiftersWorldColor.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _HorizonColor;
                float4 _BottomColor;
                float  _HorizonSharpness;
                float  _HorizonOffset;
                float4 _SunColor;
                float4 _SunDirection;
                float  _SunSize;
                float  _SunGlow;
                float4 _NebulaColorA;
                float4 _NebulaColorB;
                float  _NebulaStrength;
                float  _ShaftStrength;
                float  _StarStrength;
                float  _Seed;
                float  _GreyImmune;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings SkyVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 SkyFragment(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                float y = d.y - _HorizonOffset;
                float t = 1.0 - exp(-abs(y) * _HorizonSharpness);
                float3 col = y >= 0.0 ? lerp(_HorizonColor.rgb, _TopColor.rgb, t) : lerp(_HorizonColor.rgb, _BottomColor.rgb, t);

                // light shafts rising around the horizon
                float2 h = normalize(d.xz + 1e-5);
                float shaftNoise = Shifters_ValueNoise3D(float3(h * 7.0, _Seed)) * 0.6 + Shifters_ValueNoise3D(float3(h * 17.0, _Seed + 4.0)) * 0.4;
                float shaft = smoothstep(0.55, 0.95, shaftNoise) * saturate(1.0 - abs(y) * 2.2);
                col += _HorizonColor.rgb * shaft * _ShaftStrength;

                // big soft nebula blobs
                float neb = 0.0;
                float3 nebCol = 0;
                [unroll] for (int i = 0; i < 5; i++)
                {
                    float fi = (float)i;
                    float a = _Seed * 1.7 + fi * 2.399;
                    float3 bd = normalize(float3(cos(a), sin(_Seed + fi * 1.31) * 0.45 + 0.08, sin(a)));
                    float k = 5.0 + 4.0 * frac(fi * 0.618 + _Seed);
                    float w = exp((dot(d, bd) - 1.0) * k);
                    w *= 0.55 + 0.45 * Shifters_ValueNoise3D(d * 4.0 + fi * 3.1 + _Seed);
                    nebCol += lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, frac(fi * 0.5)) * w;
                    neb += w;
                }
                col = lerp(col, col + nebCol, _NebulaStrength);

                // stars (tiny, only above the horizon)
                float3 sp = d * 180.0;
                float star = step(0.9965, Shifters_Hash31(floor(sp)));
                float3 fp = frac(sp) - 0.5;
                star *= saturate(1.0 - length(fp) * 2.6) * saturate(y * 4.0);
                col += star * _StarStrength;

                // sun glow
                float3 sd = normalize(_SunDirection.xyz + 1e-5);
                float sdot = saturate(dot(d, sd));
                float disc = smoothstep(1.0 - _SunSize * 0.012, 1.0 - _SunSize * 0.006, sdot);
                float glow = pow(sdot, 12.0) * 0.6 + pow(sdot, 90.0) * 0.8;
                col += _SunColor.rgb * (glow * _SunGlow + disc * _SunGlow * 2.0);
                // WORLD COLOR: the sky reads as a far shell 150 m around the camera (waves sweep over it)
                col = Shifters_ApplyWorldColor(col, _WorldSpaceCameraPos + d * 150.0, _GreyImmune);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
