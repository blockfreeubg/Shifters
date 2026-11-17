// =====================================================================================
//  SHIFTERS - MusicInstruments.Campaign.cs   [owned by the AUDIO agent]
//  Campaign instruments: march band (tuba, hand cymbals, timpani, kazoo, glockenspiel),
//  heist jazz (brushes, harmon-muted trumpet), spooky waltz (music box), rock orchestra
//  (power guitar, strings, choir). Same rules as MusicInstruments.Render: patches only.
// =====================================================================================
using System;

namespace Shifters
{
    public static partial class MusicInstruments
    {
        static int CampaignBus(MusicInst i)
        {
            switch (i)
            {
                case MusicInst.Brush: case MusicInst.Cymbal: case MusicInst.Timpani: return 0;
                case MusicInst.Tuba: return 1;
                case MusicInst.Choir: case MusicInst.Strings: case MusicInst.PowerGuitar: return 2;
                default: return 3;   // Kazoo, MutedTrumpet, Glock, MusicBox
            }
        }

        static int RenderCampaign(MusicInst inst, float hz, float len, float v, ref AudioRng r, float[] o, float glideFromHz)
        {
            int n = 0;
            switch (inst)
            {
                case MusicInst.Tuba:   // "oom": round brassy low note with a little blat at the start
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz, len + 0.3f, 0.3f * v).PitchEnv(-1.2f, 0.025f).LpEnv(hz * 1.3f, hz * 3.5f, 0.07f, 1.1f)
                        .Env(0.018f, 0.2f, 0.65f).Rel(len, 0.08f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 0.3f, 0.42f * v).Env(0.015f, 0.25f, 0.7f).Rel(len, 0.08f), ref r));
                    break;
                case MusicInst.Brush:  // brush swish on the snare
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.3f, 0.22f * v).Bp(4200f, 0.6f).Env(0.025f, 0.09f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, 190f, 0.1f, 0.12f * v).Env(0.002f, 0.03f), ref r));
                    break;
                case MusicInst.Cymbal: // marching-band hand cymbals (shorter than Crash)
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 1.0f, 0.24f * v).Hp(4200f).Env(0.0008f, 0.32f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, 470f, 1.0f, 0.06f * v).Fm(3.3f, 5f, 0.4f).Env(0.0008f, 0.35f), ref r));
                    break;
                case MusicInst.Timpani:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 1.6f, 0.8f * v).PitchEnv(1.5f, 0.06f).Env(0.002f, 0.55f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 1.51f, 0.8f, 0.25f * v).Env(0.002f, 0.25f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.1f, 0.25f * v).Lp(700f).Env(0.0008f, 0.03f), ref r));
                    break;
                case MusicInst.Choir:  // "aah" (two formants over three detuned saws)
                    {
                        float[] det = { -0.1f, 0f, 0.12f };
                        foreach (float d in det)
                        {
                            float f = hz * Synth.Semis(d);
                            n = Math.Max(n, R(o, P(SynthWave.Saw, f, len + 0.8f, 0.13f * v).Bp(780f, 1.4f).Vib(5f, 0.12f).Env(0.18f, 0f).Rel(len, 0.45f), ref r));
                            n = Math.Max(n, R(o, P(SynthWave.Saw, f, len + 0.8f, 0.06f * v).Bp(1180f, 2f).Vib(5f, 0.12f).Env(0.18f, 0f).Rel(len, 0.45f), ref r));
                        }
                        break;
                    }
                case MusicInst.Strings:
                    {
                        float[] det = { -0.08f, 0.07f };
                        foreach (float d in det)
                            n = Math.Max(n, R(o, P(SynthWave.Saw, hz * Synth.Semis(d), len + 0.6f, 0.11f * v).Lp(2600f, 0.8f).Vib(5.5f, 0.09f)
                                .Env(0.09f, 0.6f, 0.85f).Rel(len, 0.3f), ref r));
                        break;
                    }
                case MusicInst.PowerGuitar:   // distorted power chord (root + fifth + octave below), palm-mutable via len
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz, len + 0.15f, 0.16f * v).Sat(6f).Lp(2600f, 1.1f).Env(0.002f, 0.25f, 0.65f).Rel(len, 0.05f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz * 1.4983f, len + 0.15f, 0.11f * v).Sat(6f).Lp(2600f, 1.1f).Env(0.002f, 0.25f, 0.65f).Rel(len, 0.05f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz * 0.5f, len + 0.15f, 0.12f * v).Sat(4f).Lp(1200f).Env(0.002f, 0.25f, 0.65f).Rel(len, 0.05f), ref r));
                    break;
                case MusicInst.Kazoo:  // Max's kazoo: buzzy, nasal, a little wobbly
                    {
                        var p = P(SynthWave.Saw, glideFromHz > 0f ? glideFromHz : hz, len + 0.12f, 0.42f * v).Bp(1150f, 0.9f).Vib(5.8f, 0.28f).Sat(2.2f).Env(0.02f, 0f).Rel(len, 0.05f);
                        var q = P(SynthWave.Pulse, glideFromHz > 0f ? glideFromHz : hz, len + 0.12f, 0.14f * v).Pwm(0.12f).Hp(1600f).Lp(5000f).Vib(5.8f, 0.28f).Env(0.02f, 0f).Rel(len, 0.05f);
                        if (glideFromHz > 0f) { p = p.Sweep(hz, 0.06f); q = q.Sweep(hz, 0.06f); }
                        n = Math.Max(n, R(o, p, ref r));
                        n = Math.Max(n, R(o, q, ref r));
                        break;
                    }
                case MusicInst.MutedTrumpet:   // harmon mute: thin square with a quick "wah" filter opening
                    n = Math.Max(n, R(o, P(SynthWave.Square, hz, len + 0.2f, 0.3f * v).Pwm(0.38f).Hp(450f).LpEnv(hz * 1.2f, hz * 5f, 0.09f, 2.4f)
                        .Vib(5.2f, 0.1f).Env(0.012f, 0.22f, 0.6f).Rel(len, 0.06f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.04f, 0.04f * v).Bp(2500f, 2f).Env(0.003f, 0.012f), ref r));
                    break;
                case MusicInst.Glock:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 1.2f, 0.3f * v).Env(0.0008f, 0.45f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 2.76f, 0.5f, 0.1f * v).Env(0.0008f, 0.12f), ref r));
                    break;
                case MusicInst.MusicBox:   // tine + slight warble (old wind-up box)
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 1.4f, 0.3f * v).Vib(4.5f, 0.06f).Env(0.0008f, 0.7f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 2f, 0.5f, 0.08f * v).Env(0.0008f, 0.2f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 5.4f, 0.08f, 0.05f * v).Env(0.0004f, 0.02f), ref r));
                    break;
            }
            return n;
        }
    }
}
