// =====================================================================================
//  SHIFTERS - MusicInstruments.cs   [owned by the AUDIO agent]
//  The procedural band: synth drums (kick/snare/clap/hats/ride/rim/shaker/woodblock/toms/
//  crash + industrial clank/anvil/steam), basses (funk synth, upright, distorted, pulse,
//  sub), keys (FM electric piano, drawbar organ, detuned pad, stabs) and leads (vibes,
//  brass, horn, pluck, FM bell, square lead, theremin, siren lead). All built from Patches.
// =====================================================================================
using System;

namespace Shifters
{
    public enum MusicInst
    {
        Kick, Snare, Clap, HatC, HatO, Ride, Rim, Shaker, Woodblock, Clank, Anvil, Tom, Crash, Steam,
        BassSynth, BassUpright, BassDist, BassPulse, BassSub,
        EPiano, Organ, Pad, Stab, Brass,
        Vibes, Horn, Pluck, Bell, Lead, Theremin, SirenLead,
        // ---- campaign (MusicInstruments.Campaign.cs); bus assignment is explicit in BusOf
        Tuba, Brush, Cymbal, Timpani, Choir, Strings, PowerGuitar, Kazoo, MutedTrumpet, Glock, MusicBox
    }

    public static partial class MusicInstruments
    {
        /// <summary>0 drums, 1 bass, 2 keys, 3 lead.</summary>
        public static int BusOf(MusicInst i)
        {
            if (i >= MusicInst.Tuba) return CampaignBus(i);
            if (i <= MusicInst.Steam) return 0;
            if (i <= MusicInst.BassSub) return 1;
            if (i <= MusicInst.Brass) return 2;
            return 3;
        }

        static int R(float[] o, in SynthPatch p, ref AudioRng r) => SynthGen.Render(o, 0, p, ref r);
        static int R(float[] o, float at, in SynthPatch p, ref AudioRng r) => SynthGen.Render(o, Synth.ToFrames(at), p, ref r) + Synth.ToFrames(at);
        static SynthPatch P(SynthWave w, float hz, float dur, float gain) => SynthPatch.Make(w, hz, Math.Min(dur, 5.9f), gain);

        /// <summary>Renders one note into o (cleared by the caller). Returns frames used.</summary>
        public static int Render(MusicInst inst, float hz, float len, float vel, ref AudioRng r, float[] o, float glideFromHz = 0f)
        {
            int n = 0;
            float v = vel;
            switch (inst)
            {
                // ------------------------------------------------------------------ drums
                case MusicInst.Kick:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, 50f, 0.5f, 1f * v).PitchEnv(22f, 0.03f).Env(0.001f, 0.2f).Sat(1.3f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.02f, 0.25f * v).Hp(2500f).Env(0.0005f, 0.004f), ref r));
                    break;
                case MusicInst.Snare:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, 185f, 0.3f, 0.45f * v).PitchEnv(5f, 0.02f).Env(0.001f, 0.06f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.35f, 0.32f * v).Bp(3000f, 0.7f).Env(0.001f, 0.11f), ref r));
                    break;
                case MusicInst.Clap:
                    for (int k = 0; k < 3; k++)
                        n = Math.Max(n, R(o, k * 0.009f, P(SynthWave.Noise, 0f, 0.02f, 0.3f * v).Bp(1300f, 1.5f).Env(0.0005f, 0.006f), ref r));
                    n = Math.Max(n, R(o, 0.02f, P(SynthWave.Noise, 0f, 0.3f, 0.28f * v).Bp(1300f, 1.2f).Env(0.001f, 0.1f), ref r));
                    break;
                case MusicInst.HatC:
                    n = R(o, P(SynthWave.Noise, 0f, 0.1f, 0.3f * v).Hp(7500f).Env(0.0005f, 0.028f), ref r);
                    break;
                case MusicInst.HatO:
                    n = R(o, P(SynthWave.Noise, 0f, 0.5f, 0.26f * v).Hp(6500f).Env(0.001f, 0.2f), ref r);
                    break;
                case MusicInst.Ride:
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 1.0f, 0.1f * v).Hp(5500f).Env(0.001f, 0.45f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, 620f, 1.0f, 0.12f * v).Fm(2.71f, 2.5f, 0.6f).Env(0.001f, 0.6f), ref r));
                    break;
                case MusicInst.Rim:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 0.06f, 0.45f * v).Env(0.0005f, 0.012f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Triangle, hz * 0.5f, 0.06f, 0.3f * v).Env(0.0005f, 0.012f), ref r));
                    break;
                case MusicInst.Shaker:
                    n = R(o, P(SynthWave.Noise, 0f, 0.12f, 0.14f * v).Bp(7000f, 1.2f).Env(0.012f, 0.03f), ref r);
                    break;
                case MusicInst.Woodblock:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 0.1f, 0.55f * v).PitchEnv(3f, 0.008f).Env(0.0005f, 0.025f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 2.4f, 0.06f, 0.2f * v).Env(0.0005f, 0.012f), ref r));
                    break;
                case MusicInst.Clank:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 0.5f, 0.4f * v).Fm(1.414f, 4f, 0.05f).Env(0.0005f, 0.16f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 1.73f, 0.35f, 0.2f * v).Fm(2.76f, 2f, 0.04f).Env(0.0005f, 0.09f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.02f, 0.2f * v).Bp(3000f, 2f).Env(0.0003f, 0.004f), ref r));
                    break;
                case MusicInst.Anvil:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 1.0f, 0.3f * v).Fm(3.3f, 2f, 0.2f).Env(0.0005f, 0.35f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 2.41f, 0.6f, 0.12f * v).Env(0.0005f, 0.2f), ref r));
                    break;
                case MusicInst.Tom:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, 0.6f, 0.8f * v).PitchEnv(7f, 0.05f).Env(0.001f, 0.22f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.05f, 0.15f * v).Lp(1200f).Env(0.0005f, 0.01f), ref r));
                    break;
                case MusicInst.Crash:
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 2.2f, 0.28f * v).Hp(3500f).Env(0.001f, 0.9f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, 340f, 2.2f, 0.07f * v).Fm(3.7f, 6f, 1f).Env(0.001f, 1f), ref r));
                    break;
                case MusicInst.Steam:
                    n = R(o, P(SynthWave.Noise, 0f, len + 0.4f, 0.25f * v).Hp(3000f).Env(0.03f, 0f).Rel(len, 0.1f), ref r);
                    break;

                // ------------------------------------------------------------------ basses
                case MusicInst.BassSynth:
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz, len + 0.3f, 0.42f * v).LpEnv(180f, 1500f * v, 0.08f, 1.8f).Env(0.003f, 0.25f, 0.6f).Rel(len, 0.04f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 0.3f, 0.45f * v).Env(0.003f, 0.3f, 0.7f).Rel(len, 0.04f), ref r));
                    break;
                case MusicInst.BassUpright:
                    n = Math.Max(n, R(o, P(SynthWave.Triangle, hz, len + 0.5f, 0.55f * v).PitchEnv(0.4f, 0.02f).Lp(1000f).Env(0.003f, 0.35f, 0.25f).Rel(len, 0.08f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 0.5f, 0.5f * v).Env(0.003f, 0.4f, 0.3f).Rel(len, 0.08f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.05f, 0.12f * v).Lp(400f).Env(0.001f, 0.015f), ref r));
                    break;
                case MusicInst.BassDist:
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz, len + 0.25f, 0.35f * v).LpEnv(400f, 900f, 0.1f, 2f).Sat(3f).Env(0.002f, 0.2f, 0.8f).Rel(len, 0.04f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 0.25f, 0.3f * v).Env(0.002f, 0f).Rel(len, 0.04f), ref r));
                    break;
                case MusicInst.BassPulse:
                    n = Math.Max(n, R(o, P(SynthWave.Pulse, hz, len + 0.2f, 0.35f * v).Pwm(0.3f).LpEnv(250f, 600f, 0.06f, 1.4f).Env(0.002f, 0.12f, 0.4f).Rel(len, 0.04f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 0.2f, 0.35f * v).Env(0.002f, 0.15f, 0.5f).Rel(len, 0.04f), ref r));
                    break;
                case MusicInst.BassSub:
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz, len + 0.2f, 0.3f * v).LpEnv(300f, 1800f * v, 0.07f, 2.5f).Sat(1.5f).Env(0.002f, 0.15f, 0.7f).Rel(len, 0.04f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 0.2f, 0.5f * v).Env(0.002f, 0f).Rel(len, 0.05f), ref r));
                    break;

                // ------------------------------------------------------------------ keys
                case MusicInst.EPiano:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 1.2f, 0.32f * v).Fm(1f, 0.4f + 1.4f * v, 0.5f).Env(0.002f, 1.2f).Rel(len, 0.3f).Trem(4.5f, 0.2f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 4f, 0.3f, 0.04f * v).Env(0.001f, 0.04f), ref r));
                    break;
                case MusicInst.Organ:
                    {
                        float[] mul = { 0.5f, 1f, 2f, 3f };
                        float[] g = { 0.22f, 0.4f, 0.22f, 0.12f };
                        for (int k = 0; k < 4; k++)
                            n = Math.Max(n, R(o, P(SynthWave.Sine, hz * mul[k], len + 0.3f, g[k] * v).Env(0.004f, 0f).Rel(len, 0.05f).Vib(6.2f, 0.06f), ref r));
                        n = Math.Max(n, R(o, P(SynthWave.Noise, 0f, 0.02f, 0.05f * v).Hp(2000f).Env(0.0005f, 0.004f), ref r));
                        break;
                    }
                case MusicInst.Pad:
                    {
                        float[] det = { -0.12f, 0f, 0.1f };
                        foreach (float d in det)
                            n = Math.Max(n, R(o, P(SynthWave.Saw, hz * Synth.Semis(d), len + 2.4f, 0.12f * v).Lp(1100f, 0.8f).Env(0.35f, 1.5f, 0.75f).Rel(len, 0.6f), ref r));
                        break;
                    }
                case MusicInst.Stab:
                    n = Math.Max(n, R(o, P(SynthWave.Square, hz, len + 0.3f, 0.14f * v).Lp(2200f, 1f).Env(0.002f, 0.12f, 0.25f).Rel(len, 0.06f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Saw, hz * 1.005f, len + 0.3f, 0.12f * v).Lp(2200f, 1f).Env(0.002f, 0.12f, 0.25f).Rel(len, 0.06f), ref r));
                    break;
                case MusicInst.Brass:
                case MusicInst.Horn:
                    for (int k = 0; k < 2; k++)
                        n = Math.Max(n, R(o, P(SynthWave.Saw, hz * (k == 0 ? 0.996f : 1.004f), len + 0.5f, 0.16f * v)
                            .LpEnv(hz * 1.5f, hz * 4f, 0.25f, 1.2f).Env(0.03f, 0.3f, 0.7f).Rel(len, 0.1f).Vib(5.5f, 0.12f), ref r));
                    break;

                // ------------------------------------------------------------------ leads
                case MusicInst.Vibes:
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz, len + 1.2f, 0.45f * v).Env(0.002f, 1.0f).Rel(len + 0.3f, 0.4f).Trem(5.5f, 0.35f), ref r));
                    n = Math.Max(n, R(o, P(SynthWave.Sine, hz * 4f, 0.5f, 0.1f * v).Env(0.001f, 0.12f), ref r));
                    break;
                case MusicInst.Pluck:
                    n = R(o, P(SynthWave.Saw, hz, 0.6f, 0.3f * v).LpEnv(250f, 4500f * v, 0.05f, 1.2f).Env(0.001f, 0.22f), ref r);
                    break;
                case MusicInst.Bell:
                    n = R(o, P(SynthWave.Sine, hz, 1.6f, 0.3f * v).Fm(3.5f, 2.2f, 0.4f).Env(0.001f, 0.7f), ref r);
                    break;
                case MusicInst.Lead:
                    n = R(o, P(SynthWave.Pulse, hz, len + 0.4f, 0.2f * v).Pwm(0.5f, 0.8f, 0.15f).Lp(3000f, 0.9f).Vib(5.5f, 0.15f).Env(0.008f, 0.25f, 0.7f).Rel(len, 0.08f), ref r);
                    break;
                case MusicInst.Theremin:
                    {
                        var p = P(SynthWave.Sine, glideFromHz > 0f ? glideFromHz : hz, len + 1f, 0.3f * v).Vib(6f, 0.3f).Env(0.08f, 0f).Rel(len, 0.25f);
                        if (glideFromHz > 0f) p = p.Sweep(hz, 0.09f);
                        n = Math.Max(n, R(o, p, ref r));
                        var t = P(SynthWave.Triangle, glideFromHz > 0f ? glideFromHz : hz, len + 1f, 0.08f * v).Vib(6f, 0.3f).Env(0.08f, 0f).Rel(len, 0.25f);
                        if (glideFromHz > 0f) t = t.Sweep(hz, 0.09f);
                        n = Math.Max(n, R(o, t, ref r));
                        break;
                    }
                case MusicInst.SirenLead:
                    n = R(o, P(SynthWave.Saw, hz * Synth.Semis(-1.5f), len + 0.2f, 0.18f * v).Sweep(hz, 0.06f).Then(hz * Synth.Semis(-0.7f), len)
                        .Lp(2600f, 1f).Vib(7f, 0.25f).Env(0.01f, 0.2f, 0.8f).Rel(len, 0.06f).Sat(1.2f), ref r);
                    break;
                default:
                    n = RenderCampaign(inst, hz, len, v, ref r, o, glideFromHz);
                    break;
            }
            return Math.Min(n, o.Length);
        }
    }
}
