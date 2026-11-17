// =====================================================================================
//  SHIFTERS - SfxRecipes.Combat.cs   [owned by the AUDIO agent]
//  Cartoon weapons ("pew", "boomf", "bzzew", "thwang", rubber chicken, plunger "thwop"),
//  impacts, enemy bot beeps/alert/siren, explosions, squeaks, splashes, sizzles, alarm.
// =====================================================================================
using System;

namespace Shifters
{
    public static partial class SfxRecipes
    {
        static float[] Pistol(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.2f);
            float s = r.Range(0.9f, 1.1f);
            A(b, 0f, P(SynthWave.Square, 1700f * s, 0.16f, 0.5f).Pwm(0.35f).Sweep(260f * s, 0.13f).Lp(5000f).Env(0.0005f, 0.06f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1200f * s, 0.14f, 0.5f).Sweep(200f, 0.1f).Env(0.0005f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.03f, 0.4f).Hp(2500f).Env(0.0003f, 0.006f), ref r);
            return b;
        }

        static float[] Shotgun(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.75f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.4f, 1f).Lp(2500f, 0.707f, 300f).Env(0.0005f, 0.12f), ref r);
            A(b, 0f, P(SynthWave.Sine, 85f, 0.4f).PitchEnv(10f, 0.02f).Env(0.0005f, 0.12f), ref r);
            A(b, 0f, P(SynthWave.Pink, 0f, 0.4f, 0.4f).Bp(800f, 1f).Env(0.001f, 0.15f), ref r);
            AudioFx.Saturate(b, 2f);
            // cartoon pump "chk-chk"
            A(b, 0.38f + v * 0.04f, P(SynthWave.Noise, 0f, 0.03f, 0.25f).Bp(2200f, 3f).Env(0.0003f, 0.006f), ref r);
            A(b, 0.47f + v * 0.04f, P(SynthWave.Noise, 0f, 0.03f, 0.25f).Bp(1700f, 3f).Env(0.0003f, 0.006f), ref r);
            AudioFx.Reverb(b, 0.12f, 0.5f, 0.4f, 0.8f);
            return b;
        }

        static float[] LaserGun(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.32f);
            float s = v == 0 ? 1f : 1.2f;
            A(b, 0f, P(SynthWave.Saw, 2600f * s, 0.28f, 0.4f).Sweep(300f, 0.22f).Lp(5000f).Env(0.001f, 0.1f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1800f * s, 0.28f, 0.4f).Sweep(400f, 0.22f).Fm(0.5f, 4f, 0.15f).Env(0.001f, 0.1f), ref r);
            A(b, 0f, P(SynthWave.Pulse, 220f, 0.25f, 0.2f).Sweep(110f).Pwm(0.5f, 30f, 0.3f).Env(0.001f, 0.08f), ref r);
            AudioFx.Crush(b, 1, 16f);
            return b;
        }

        static float[] Slingshot(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.5f);
            float f = v == 0 ? 120f : 140f;
            A(b, 0f, P(SynthWave.Saw, f, 0.45f, 0.6f).Sweep(f * 0.8f, 0.3f).LpEnv(300f, 3000f, 0.05f, 2f).Vib(9f, 0.7f, 0.2f).Env(0.001f, 0.18f), ref r); // twang
            A(b, 0f, P(SynthWave.Noise, 0f, 0.06f, 0.4f).Bp(1200f, 1f).Env(0.0005f, 0.02f), ref r);
            A(b, 0f, P(SynthWave.Sine, 400f, 0.08f, 0.4f).Sweep(150f, 0.06f).Env(0.0005f, 0.04f), ref r);
            return b;
        }

        static float[] Chicken(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.5f);
            float a, peak, end;
            if (v == 1) { a = 420f; peak = 620f; end = 380f; }      // honk
            else { a = 650f; peak = 1250f + v * 120f; end = 520f; } // squawk
            A(b, 0f, P(SynthWave.Saw, a, 0.42f, 0.7f).Sweep(peak, 0.06f).Then(end, 0.25f).Bp(1500f, 3f).Vib(28f, 0.8f).Env(0.004f, 0.2f, 0.3f), ref r);
            A(b, 0f, P(SynthWave.Square, a * 0.5f, 0.4f, 0.3f).Sweep(peak * 0.5f, 0.06f).Then(end * 0.5f, 0.25f).Lp(2000f).Env(0.004f, 0.2f, 0.3f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.3f, 0.2f).Bp(2500f, 2f).Env(0.004f, 0.1f), ref r);
            if (v == 2) A(b, 0.22f, P(SynthWave.Saw, a * 1.1f, 0.2f, 0.5f).Sweep(peak, 0.04f).Then(end, 0.1f).Bp(1600f, 3f).Env(0.003f, 0.08f), ref r);
            return b;
        }

        static float[] Plunger(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.25f);
            float s = v == 0 ? 1f : 0.85f;
            A(b, 0f, P(SynthWave.Sine, 140f * s, 0.2f, 0.9f).Sweep(650f * s, 0.05f).Env(0.001f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.1f, 0.4f).Lp(900f).Env(0.0005f, 0.03f), ref r);
            A(b, 0.045f, P(SynthWave.Sine, 900f, 0.05f, 0.4f).Sweep(400f, 0.02f).Env(0.0005f, 0.012f), ref r);
            return b;
        }

        static float[] Impact(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.22f);
            A(b, 0f, P(SynthWave.Sine, r.Range(140f, 190f), 0.2f, 0.9f).PitchEnv(9f, 0.01f).Env(0.0005f, 0.04f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.1f, 0.7f).Bp(r.Range(1000f, 1500f), 0.7f).Env(0.0005f, 0.03f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.18f, 0.4f).Lp(400f).Env(0.0005f, 0.06f), ref r);
            AudioFx.Saturate(b, 1.3f);
            return b;
        }

        static float[] MetalHit(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.8f);
            float f = r.Range(380f, 520f);
            A(b, 0f, P(SynthWave.Sine, f, 0.8f, 0.6f).Fm(1.414f, 3.5f, 0.08f).Env(0.0005f, 0.3f), ref r);
            A(b, 0f, P(SynthWave.Sine, f * 2.76f, 0.5f, 0.3f).Fm(1f, 1f, 0.05f).Env(0.0005f, 0.15f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.05f, 0.4f).Hp(3000f).Env(0.0003f, 0.01f), ref r);
            AudioFx.Reverb(b, 0.1f, 0.5f, 0.3f, 0.7f);
            return b;
        }

        static float[] Ricochet(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.7f);
            float f = r.Range(3200f, 4000f);
            A(b, 0f, P(SynthWave.Sine, f, 0.55f, 0.7f).Sweep(f * 0.36f, 0.45f).Vib(28f, 0.3f).Env(0.001f, 0.25f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.03f, 0.5f).Hp(4000f).Env(0.0003f, 0.006f), ref r);
            A(b, 0f, P(SynthWave.Sine, 5200f, 0.08f, 0.2f).Env(0.0003f, 0.02f), ref r);
            AudioFx.Reverb(b, 0.15f, 0.5f, 0.3f);
            return b;
        }

        static float[] Pickup(ref AudioRng r)
        {
            var b = Synth.Buffer(0.6f);
            A(b, 0f, P(SynthWave.Pulse, 880f, 0.08f, 0.35f).Pwm(0.25f).Lp(5000f).Env(0.002f, 0f).Rel(0.06f, 0.02f), ref r);
            A(b, 0.07f, P(SynthWave.Pulse, 1320f, 0.08f, 0.35f).Pwm(0.25f).Lp(5000f).Env(0.002f, 0f).Rel(0.06f, 0.02f), ref r);
            Bell(b, 0.14f, 1760f, 0.15f, 0.4f, ref r);
            return b;
        }

        static float[] EmptyClick(ref AudioRng r)
        {
            var b = Synth.Buffer(0.08f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.02f, 0.7f).Bp(2500f, 2f).Env(0.0003f, 0.003f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1100f, 0.03f, 0.3f).Env(0.0003f, 0.008f), ref r);
            A(b, 0.02f, P(SynthWave.Sine, 2600f, 0.03f, 0.2f).Env(0.0003f, 0.005f), ref r);
            return b;
        }

        static float[] EnemyBeep(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.26f);
            float[] choices = { 880f, 987.77f, 1174.66f };
            float f1 = choices[v % 3], f2 = f1 * ((v & 1) == 0 ? 1.25f : 0.8f);
            A(b, 0f, P(SynthWave.Pulse, f1, 0.07f, 0.35f).Pwm(0.3f).Lp(3500f).Env(0.002f, 0f).Rel(0.06f, 0.01f).Bits(12f), ref r);
            A(b, 0.1f, P(SynthWave.Pulse, f2, 0.09f, 0.35f).Pwm(0.3f).Lp(3500f).Env(0.002f, 0f).Rel(0.08f, 0.01f).Bits(12f), ref r);
            return b;
        }

        static float[] EnemyAlert(ref AudioRng r)
        {
            var b = Synth.Buffer(0.35f);
            A(b, 0f, P(SynthWave.Square, 600f, 0.12f, 0.4f).Sweep(1800f, 0.06f).Lp(6000f).Env(0.001f, 0f).Rel(0.1f, 0.01f), ref r);
            A(b, 0.13f, P(SynthWave.Sine, 1800f, 0.15f, 0.5f).Env(0.001f, 0.06f), ref r);
            A(b, 0.13f, P(SynthWave.Square, 1800f, 0.12f, 0.2f).Lp(5000f).Env(0.001f, 0.05f), ref r);
            return b;
        }

        static float[] EnemyStun(ref AudioRng r)
        {
            var b = Synth.Buffer(1.15f);
            A(b, 0f, P(SynthWave.Sine, 900f, 1.1f, 0.5f).Sweep(500f).Vib(7f, 4f).Trem(9f, 0.4f).Env(0.01f, 0.6f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1350f, 1.1f, 0.25f).Sweep(700f).Vib(5.3f, 3f).Env(0.01f, 0.5f), ref r);
            for (int k = 0; k < 2; k++)
            {
                float f = r.Range(2800f, 3600f);
                A(b, 0.2f + k * 0.35f, P(SynthWave.Sine, f, 0.1f, 0.25f).Sweep(f * 1.3f, 0.04f).Then(f * 0.9f, 0.05f).Vib(34f, 1.4f).Env(0.005f, 0.04f), ref r);
            }
            return b;
        }

        /// <summary>Seamless "wee-oo" loop: frequencies are scaled so the loop holds a whole number of cycles.</summary>
        static float[] Siren(ref AudioRng r)
        {
            int n = Synth.Frames(1.2f);
            var f = new float[n];
            double cycles = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i * Synth.Dt;
                bool first = t < 0.6f;
                float u = Synth.Clamp01((first ? t : t - 0.6f) / 0.12f);
                u = u * u * (3f - 2f * u);
                f[i] = first ? Synth.Lerp(760f, 960f, u) : Synth.Lerp(960f, 760f, u);
                cycles += f[i] * Synth.Dt;
            }
            double k = Math.Round(cycles) / cycles;
            var b = new float[n];
            AudioOsc o1 = default, o2 = default;
            AudioSvf lp = default;
            lp.Set(3000f, 0.8f);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float hz = (float)(f[i] * k);
                    float v = lp.Low(o1.Square(hz) * 0.45f + o2.Sine(hz) * 0.6f);
                    if (pass == 1) b[i] = v;
                }
            return b;
        }

        static float[] EnemyGrab(ref AudioRng r)
        {
            var b = Synth.Buffer(0.55f);
            A(b, 0f, P(SynthWave.Saw, 220f, 0.22f, 0.3f).Sweep(440f, 0.2f).Lp(1500f).Vib(30f, 0.3f).Env(0.01f, 0f).Rel(0.2f, 0.02f), ref r);
            A(b, 0f, P(SynthWave.Square, 110f, 0.22f, 0.15f).Sweep(220f, 0.2f).Lp(1200f).Env(0.01f, 0f).Rel(0.2f, 0.02f), ref r);
            A(b, 0.22f, P(SynthWave.Noise, 0f, 0.05f, 0.6f).Bp(2000f, 3f).Env(0.0003f, 0.01f), ref r);
            A(b, 0.22f, P(SynthWave.Sine, 500f, 0.3f, 0.5f).Fm(2.76f, 3f, 0.06f).Env(0.0005f, 0.15f), ref r);
            return b;
        }

        static float[] Explosion(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(2.2f);
            A(b, 0f, P(SynthWave.Noise, 0f, 1.5f, 1f).Lp(4000f, 0.707f, 200f).Env(0.002f, 0.35f), ref r);
            A(b, 0f, P(SynthWave.Sine, 55f, 1.2f).PitchEnv(12f, 0.05f).Env(0.001f, 0.4f), ref r);
            A(b, 0f, P(SynthWave.Pink, 0f, 1.5f, 0.5f).Lp(600f).Env(0.005f, 0.6f), ref r);
            SynthGen.Grains(b, 0.1f, 0.9f, 30 + v * 8, 1000f, 4000f, 0.003f, 0.012f, 0.3f, true, ref r, 0.5f);
            AudioFx.Saturate(b, 2.5f);
            AudioFx.Crush(b, 1, 24f);
            AudioFx.Reverb(b, 0.25f, 0.8f, 0.45f);
            return b;
        }

        static float[] Squeak(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.22f);
            float s = 1f + v * 0.12f;
            A(b, 0f, P(SynthWave.Sine, 1200f * s, 0.2f, 0.7f).Sweep(1700f * s, 0.05f).Then(1100f * s, 0.1f).Env(0.003f, 0.08f, 0.6f).Rel(0.14f, 0.02f), ref r);
            A(b, 0f, P(SynthWave.Saw, 1200f * s, 0.2f, 0.3f).Sweep(1700f * s, 0.05f).Then(1100f * s, 0.1f).Bp(2400f, 4f).Env(0.003f, 0.08f, 0.6f).Rel(0.14f, 0.02f), ref r);
            return b;
        }

        static float[] Splash(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.0f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.5f, 0.8f).Bp(900f, 1f, 2500f).Env(0.005f, 0.12f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.8f, 0.4f).Lp(1500f).Env(0.005f, 0.3f), ref r);
            for (int k = 0; k < 10 + v * 3; k++)
            {
                float f = r.Range(500f, 1400f);
                A(b, r.Range(0.05f, 0.6f), P(SynthWave.Sine, f, 0.06f, 0.3f).Sweep(f * 1.6f, 0.03f).Env(0.002f, 0.02f), ref r); // bubbles
            }
            return b;
        }

        static float[] Sizzle(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.05f);
            A(b, 0f, P(SynthWave.Noise, 0f, 1.0f, 0.5f).Hp(3000f).Env(0.05f, 0.6f, 0.4f), ref r);
            SynthGen.Grains(b, 0f, 0.9f, 60, 2000f, 6000f, 0.002f, 0.006f, 0.5f, true, ref r);
            return b;
        }

        static float[] Alarm(ref AudioRng r)
        {
            var b = Synth.Buffer(1.0f);
            float[] tones = { 880f, 660f, 880f, 660f };   // whole cycles per 0.25 s segment => seamless
            for (int i = 0; i < 4; i++)
                A(b, i * 0.25f, P(SynthWave.Square, tones[i], 0.2f, 0.4f).Lp(3500f).Env(0.004f, 0f), ref r);
            return b;
        }
    }
}
