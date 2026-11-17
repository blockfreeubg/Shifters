// SHIFTERS - WORLD COLOR (CAMPAIGN 14): the grey-to-color SHIFT. Owned by RENDERING.
// Global uniforms (set by WorldColor.cs with Shader.SetGlobal*; NOT per material, so no SRP-Batcher impact).
// Every global left unset reads 0, and _ShiftersWorldParams.x == 0 means OFF: the world renders in full color.
//   _ShiftersWorldColor   base saturation (0 grey .. 1 color, > 1 oversaturated party)
//   _ShiftersBursts[8]    xyz centre, w radius (unused slots: w = -1000)
//   _ShiftersWave         xyz origin, w radius (w < 0 = no wave)
//   _ShiftersWorldParams  x on (0/1), y saturation inside the wave, z ink-edge width (m), w unused
// sat = max(lerp(base, waveTo, insideWave), insideAnyBurst); color = lerp(luma(c) * (0.93, 0.96, 1.04), c, sat).
// Uniform branches only (no keywords, no variants). WebGL2 safe.
#ifndef SHIFTERS_WORLD_COLOR_INCLUDED
#define SHIFTERS_WORLD_COLOR_INCLUDED

float  _ShiftersWorldColor;
float4 _ShiftersBursts[8];
float4 _ShiftersWave;
float4 _ShiftersWorldParams;

// cheap smooth ink-edge noise in [-1, 1] (sums of nested sines: no hashing)
float Shifters_InkNoise(float3 p)
{
    return sin(p.x * 1.31 + sin(p.z * 0.73 + p.y * 0.4) * 2.1) * sin(p.z * 1.17 + sin(p.x * 0.61 + p.y * 0.9) * 1.7);
}

// 0 outside .. 1 inside a noisy-edged sphere (edge softness ~0.6 m, wobble up to 0.9 m)
float Shifters_InkInside(float3 p, float4 sphere, float n)
{
    float d = distance(p, sphere.xyz);
    float edge = sphere.w + n * clamp(sphere.w * 0.2, 0.0, 0.9);
    return saturate((edge - d) * 1.6 + 0.5);
}

// Saturation at a world position (CPU mirror: WorldColor.SaturationAt).
float Shifters_WorldSaturation(float3 positionWS)
{
    float n = Shifters_InkNoise(positionWS);
    float sat = _ShiftersWorldColor;
    UNITY_BRANCH
    if (_ShiftersWave.w >= 0.0)
        sat = lerp(sat, _ShiftersWorldParams.y, Shifters_InkInside(positionWS, _ShiftersWave, n));
    float burst = 0.0;
    float rim = 0.0;
    [unroll] for (int i = 0; i < 8; i++)
    {
        float k = Shifters_InkInside(positionWS, _ShiftersBursts[i], n);
        burst = max(burst, k);
        rim = max(rim, k * (1.0 - k));
    }
    // a thin oversaturated ink rim at the frontier of every burst
    return max(sat, burst + rim * 0.9);
}

half3 Shifters_ApplyWorldColor(half3 c, float3 positionWS, float immune)
{
    UNITY_BRANCH
    if (_ShiftersWorldParams.x > 0.5 && immune < 0.5)
    {
        half sat = (half)Shifters_WorldSaturation(positionWS);
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = max(lerp(l * half3(0.93, 0.96, 1.04), c, sat), 0.0);
    }
    return c;
}
#endif
