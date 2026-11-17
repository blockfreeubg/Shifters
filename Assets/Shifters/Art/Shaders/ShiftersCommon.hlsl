// SHIFTERS - shared shader helpers (hash / value noise / color). Owned by RENDERING.
#ifndef SHIFTERS_COMMON_INCLUDED
#define SHIFTERS_COMMON_INCLUDED

float Shifters_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float Shifters_Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// Smooth 3D value noise in [0,1].
float Shifters_ValueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = Shifters_Hash31(i);
    float n100 = Shifters_Hash31(i + float3(1, 0, 0));
    float n010 = Shifters_Hash31(i + float3(0, 1, 0));
    float n110 = Shifters_Hash31(i + float3(1, 1, 0));
    float n001 = Shifters_Hash31(i + float3(0, 0, 1));
    float n101 = Shifters_Hash31(i + float3(1, 0, 1));
    float n011 = Shifters_Hash31(i + float3(0, 1, 1));
    float n111 = Shifters_Hash31(i + float3(1, 1, 1));
    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

// Cheap sRGB -> linear (vertex colors are authored in sRGB like material colors).
float3 Shifters_SRGBToLinear(float3 c)
{
    return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
}

// Soft-stepped toon ramp: 'steps' bands, 'softness' 0 (hard) .. 1 (smooth).
float Shifters_StepSoft(float x, float steps, float softness)
{
    steps = max(steps, 1.0);
    float s = saturate(x) * steps;
    float i = floor(s);
    float f = s - i;
    float w = max(softness, 0.001) * 0.5;
    return saturate((i + smoothstep(0.5 - w, 0.5 + w, f)) / steps);
}

// Picks a 2D world coordinate on the plane most aligned with the normal (for grids / tiles).
float2 Shifters_PlanarCoord(float3 positionWS, float3 normalWS)
{
    float3 an = abs(normalWS);
    if (an.y >= an.x && an.y >= an.z) return positionWS.xz;
    if (an.x >= an.z) return positionWS.zy;
    return positionWS.xy;
}
#endif
