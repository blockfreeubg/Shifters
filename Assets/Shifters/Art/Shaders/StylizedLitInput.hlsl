// SHIFTERS - Shifters/StylizedLit material inputs (SRP Batcher: every property lives in UnityPerMaterial).
#ifndef SHIFTERS_STYLIZED_LIT_INPUT_INCLUDED
#define SHIFTERS_STYLIZED_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float  _VertexColorStrength;
    float  _ShadeSteps;
    float  _ShadeSoftness;
    float  _WrapLighting;
    float4 _ShadowTint;
    float  _Smoothness;
    float  _SpecularStrength;
    float  _RimPower;
    float  _RimStrength;
    float4 _RimColor;
    float4 _EmissionColor;
    float4 _RevealColor;
    float  _RevealStrength;
    float  _RevealPulse;
    float  _GridScale;
    float  _GridStrength;
    float4 _GridColor;
    float  _GridWidth;
    float  _GridGlow;
    float  _NoiseStrength;
    float  _NoiseScale;
    float4 _StripeColor;
    float  _StripeWidth;
    float  _StripeStrength;
    float  _Cutoff;
    float  _Surface;
    float  _GreyImmune;
    float  _CharacterFill;
CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

#endif
