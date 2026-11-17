// SHIFTERS - Shifters/StylizedLit forward pass: soft-stepped toon diffuse + URP shadows (cascades, soft),
// SSAO, Forward+ cluster light loop, fresnel rim, toon specular, world grid/noise, stripes, emission, reveal glow.
#ifndef SHIFTERS_STYLIZED_LIT_FORWARD_INCLUDED
#define SHIFTERS_STYLIZED_LIT_FORWARD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "ShiftersCommon.hlsl"
#include "ShiftersWorldColor.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 color      : COLOR;
    float2 texcoord   : TEXCOORD0;
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

Varyings StylizedLitVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = vp.positionCS;
    output.positionWS = vp.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.color = float4(Shifters_SRGBToLinear(saturate(input.color.rgb)), input.color.a);
    output.fogFactor = ComputeFogFactor(vp.positionCS.z);
    return output;
}

half3 Shifters_Trilight(half3 n)
{
    half3 c = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(n.y));
    return lerp(c, unity_AmbientGround.rgb, saturate(-n.y));
}

half3 Shifters_ShadeLight(Light light, half3 n, half3 v, half3 albedo, half specMask, out half lightAmount)
{
    half ndl = dot(n, light.direction);
    half wrapped = saturate((ndl + _WrapLighting) / (1.0 + _WrapLighting));
    half shade = Shifters_StepSoft(wrapped, _ShadeSteps, _ShadeSoftness);
    half shadow = smoothstep(0.0, 1.0, light.shadowAttenuation);
    lightAmount = shade * shadow;
    half3 h = SafeNormalize(light.direction + v);
    half gloss = exp2(_Smoothness * 10.0 + 1.0);
    half spec = smoothstep(0.42, 0.58, pow(saturate(dot(n, h)), gloss)) * _SpecularStrength * _Smoothness * specMask;
    half3 radiance = light.color * light.distanceAttenuation;
    return radiance * lightAmount * (albedo + spec);
}

half4 StylizedLitFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half4 vc = lerp(half4(1, 1, 1, 1), (half4)input.color, _VertexColorStrength);
    half3 albedo = tex.rgb * _BaseColor.rgb * vc.rgb;
    half alpha = tex.a * _BaseColor.a * vc.a;

    float3 positionWS = input.positionWS;
    half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

    // ---- surface variation: world noise, per-tile tint, grid lines, stripes (uniform branches)
    float2 gridCoord = Shifters_PlanarCoord(positionWS, normalWS) / max(_GridScale, 0.01);
    float2 gridFw = fwidth(gridCoord);
    float stripeCoord = (input.uv.x + input.uv.y) / max(_StripeWidth, 0.01);
    float stripeFw = fwidth(stripeCoord);
    half3 emission = _EmissionColor.rgb;

    if (_NoiseStrength > 0.0005)
    {
        float3 np = positionWS * _NoiseScale;
        float n = Shifters_ValueNoise3D(np) * 0.65 + Shifters_ValueNoise3D(np * 3.7 + 11.3) * 0.35;
        albedo *= 1.0 + (n - 0.5) * 2.0 * _NoiseStrength;
    }
    if (_GridStrength > 0.0005)
    {
        float2 d = abs(frac(gridCoord + 0.5) - 0.5);            // distance to nearest grid line (cell units)
        float2 w = _GridWidth.xx;
        float2 l = 1.0 - smoothstep(w - gridFw, w + gridFw, d);
        half gridLine = max(l.x, l.y);
        float tileRand = Shifters_Hash21(floor(gridCoord));
        albedo *= 1.0 + (tileRand - 0.5) * 0.12 * _GridStrength;
        albedo = lerp(albedo, _GridColor.rgb, gridLine * _GridStrength * _GridColor.a);
        emission += _GridColor.rgb * gridLine * _GridGlow;
    }
    if (_StripeStrength > 0.0005)
    {
        float tri = abs(frac(stripeCoord) - 0.5) * 2.0;
        half st = smoothstep(0.5 - stripeFw, 0.5 + stripeFw, tri);
        albedo = lerp(albedo, _StripeColor.rgb, st * _StripeStrength);
    }

    // perspective reveal also tints the surface a little (readable even in dark themes)
    albedo = lerp(albedo, albedo * 0.6 + _RevealColor.rgb * 0.4, saturate(_RevealStrength) * 0.35);

    // ---- URP inputs
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), input.fogFactor);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0);

    // ---- main light
    Light mainLight = GetMainLight(inputData, inputData.shadowMask, aoFactor);
    half mainAmount;
    half3 direct = Shifters_ShadeLight(mainLight, normalWS, viewDirWS, albedo, 1.0, mainAmount);

    // ---- additional lights (Forward+ cluster loop or per-object loop)
    half3 additional = 0;
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    half addAmount;
  #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        Light dirLight = GetAdditionalLight(dirIndex, inputData, inputData.shadowMask, aoFactor);
        additional += Shifters_ShadeLight(dirLight, normalWS, viewDirWS, albedo, 1.0, addAmount);
    }
  #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);
        additional += Shifters_ShadeLight(light, normalWS, viewDirWS, albedo, 1.0, addAmount);
    LIGHT_LOOP_END
#endif

    // ---- ambient (trilight) tinted in shadow, SSAO on indirect
    half3 ambient = Shifters_Trilight(normalWS) * aoFactor.indirectAmbientOcclusion;
    half3 shadowTint = lerp(_ShadowTint.rgb, half3(1, 1, 1), mainAmount);
    half3 indirect = albedo * ambient * shadowTint;
    half3 lit = direct + additional + indirect;

    // ---- rim + reveal
    half ndv = saturate(dot(normalWS, viewDirWS));
    half fresnel = 1.0 - ndv;
    half rim = pow(fresnel, _RimPower) * _RimStrength;

    // ---- CHARACTER FILL (interns only, _CharacterFill > 0): a camera-side fill that tops the light reaching the
    // surface up to a minimum level, so playable characters keep shape/color/face in dark rooms. It fades out as soon
    // as the scene lights them (bright scenes are unchanged) and never touches the environment (darkness stays).
    if (_CharacterFill > 0.0005)
    {
        const half3 lumaW = half3(0.2126, 0.7152, 0.0722);
        half litLevel = dot(lit, lumaW) / max(dot(albedo, lumaW), 0.02);
        half need = saturate(1.0 - litLevel / _CharacterFill);
        half3 fillDir = SafeNormalize(viewDirWS + half3(0.0, 0.55, 0.0));
        half wrapF = saturate(dot(normalWS, fillDir) * 0.5 + 0.5);
        half3 amb = unity_AmbientSky.rgb;
        half3 hue = amb / max(max(amb.r, max(amb.g, amb.b)), 0.001);
        half3 fillTint = lerp(half3(1.0, 1.0, 1.0), hue, 0.3);
        lit += albedo * fillTint * (_CharacterFill * need * (0.3 + 0.7 * wrapF * wrapF));
        rim *= 1.0 + need * 1.5;
    }
    half3 rimColor = _RimColor.rgb * rim * lerp(0.45, 1.0, mainAmount);

    half pulse = 0.72 + 0.28 * sin(_Time.y * _RevealPulse);
    half3 reveal = _RevealColor.rgb * _RevealStrength * (0.22 + 1.6 * fresnel * fresnel) * pulse;
    // WORLD COLOR (grey -> color SHIFT); the perspective reveal glow stays colored (gameplay language)
    half3 color = Shifters_ApplyWorldColor(lit + rimColor + emission, positionWS, _GreyImmune) + reveal;

#if defined(_SURFACE_TYPE_TRANSPARENT)
    alpha = saturate(alpha + rim * 0.5);
#else
    alpha = 1.0;
#endif
    color = MixFog(color, inputData.fogCoord);
    return half4(color, alpha);
}
#endif
