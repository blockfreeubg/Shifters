// =====================================================================================
//  SHIFTERS - SfxRecipes.Character.cs   [owned by the AUDIO agent]
//  Character sounds: rubbery footsteps, boing jumps, thuds, slide-whistle tumbles, KO bonk
//  + cartoon birds, falling-into-the-void whistle, revive sparkle, bridge creaks...
// =====================================================================================
namespace Shifters
{
    public static partial class SfxRecipes
    {
        static float[] Footstep(int v, ref AudioRng r, bool heavy)
        {
            float len = heavy ? 0.2f : 0.11f;
            var b = Synth.Buffer(len + 0.03f);
            float f = heavy ? r.Range(80f, 105f) : r.Range(170f, 240f);
            A(b, 0f, P(SynthWave.Sine, f, len).PitchEnv(heavy ? 9f : 7f, 0.012f).Env(0.001f, heavy ? 0.06f : 0.035f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, len * 0.6f, heavy ? 0.45f : 0.3f).Lp(heavy ? 600f : 1400f).Env(0.002f, heavy ? 0.03f : 0.016f), ref r);
            if (!heavy && (v & 1) == 1)
                A(b, 0.012f, P(SynthWave.Triangle, r.Range(700f, 900f), 0.05f, 0.16f).Sweep(r.Range(1000f, 1250f)).Env(0.003f, 0.015f), ref r);
            if (heavy)
            {
                A(b, 0f, P(SynthWave.Sine, f * 0.5f, len, 0.6f).Env(0.002f, 0.08f), ref r);
                AudioFx.Saturate(b, 1.4f);
            }
            return b;
        }

        static float[] Jump(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.45f);
            float f0 = r.Range(190f, 240f);
            // jaw-harp style "boing": sine glide + buzzy saw through a sweeping resonant band
            A(b, 0f, P(SynthWave.Sine, f0, 0.4f, 0.9f).Sweep(f0 * 2.4f, 0.07f).Vib(15f + v * 2f, 2.2f, 0.12f).Env(0.002f, 0.13f), ref r);
            A(b, 0f, P(SynthWave.Saw, f0 * 0.5f, 0.34f, 0.35f).Sweep(f0 * 1.2f, 0.07f).Bp(900f, 4f, 2400f).Vib(15f, 2f, 0.1f).Env(0.002f, 0.1f), ref r);
            return b;
        }

        static float[] Land(int v, ref AudioRng r, bool heavy)
        {
            float len = heavy ? 0.45f : 0.2f;
            var b = Synth.Buffer(len + 0.06f);
            float f = heavy ? r.Range(55f, 70f) : r.Range(95f, 125f);
            A(b, 0f, P(SynthWave.Sine, f, len).PitchEnv(12f, 0.018f).Env(0.001f, heavy ? 0.14f : 0.06f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, len * 0.7f, heavy ? 0.7f : 0.45f).Lp(heavy ? 450f : 900f).Env(0.001f, heavy ? 0.06f : 0.03f), ref r);
            A(b, 0.02f, P(SynthWave.Triangle, r.Range(300f, 380f), 0.09f, 0.15f).Sweep(r.Range(160f, 200f)).Env(0.004f, 0.03f), ref r);
            if (heavy)
            {
                SynthGen.Grains(b, 0.03f, 0.3f, 14, 700f, 3000f, 0.004f, 0.012f, 0.25f, true, ref r, 0.15f);
                AudioFx.Saturate(b, 1.6f);
            }
            return b;
        }

        static float[] Tumble(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.85f);
            float hi = r.Range(1100f, 1450f), lo = r.Range(430f, 600f);
            // slide whistle: dive down, wobble back up (+ breathy noise riding the same pitch)
            var whistle = P(SynthWave.Sine, hi, 0.8f, 0.8f).Sweep(lo, 0.42f).Then(hi * (v == 1 ? 1.1f : 0.8f), 0.3f)
                .Vib(7f, 0.5f).Env(0.02f, 0f).Rel(0.68f, 0.05f);
            A(b, 0f, whistle, ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.8f, 0.06f).Bp(hi, 6f, lo).Env(0.02f, 0f).Rel(0.6f, 0.08f), ref r);
            return b;
        }

        static float[] Grab(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.22f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.12f, 0.5f).Bp(900f, 1.5f, 2600f).Env(0.02f, 0.03f), ref r);        // "fwip"
            A(b, 0.05f, P(SynthWave.Sine, 150f, 0.12f, 0.7f).PitchEnv(6f, 0.01f).Env(0.001f, 0.04f), ref r);      // soft grip thump
            A(b, 0.06f, P(SynthWave.Triangle, r.Range(900f, 1100f), 0.07f, 0.2f).Sweep(r.Range(1300f, 1500f)).Env(0.004f, 0.02f), ref r); // glove squeak
            return b;
        }

        static float[] Whoosh(int v, ref AudioRng r, float len, float f0, float f1, float f2)
        {
            var b = Synth.Buffer(len + 0.05f);
            float s = r.Range(0.9f, 1.15f);
            A(b, 0f, P(SynthWave.Pink, 0f, len, 0.8f).Bp(f0 * s, 2.2f, f1 * s).Env(len * 0.45f, len * 0.22f), ref r);
            A(b, len * 0.3f, P(SynthWave.Noise, 0f, len * 0.7f, 0.25f).Bp(f1 * s, 1.5f, f2 * s).Env(0.03f, len * 0.2f), ref r);
            return b;
        }

        static float[] Flop(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.45f);
            A(b, 0f, P(SynthWave.Sine, r.Range(150f, 175f), 0.4f, 0.8f).Sweep(85f, 0.35f).Vib(26f, 3f, 0.15f).Env(0.003f, 0.12f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.2f, 0.5f).Lp(500f).Env(0.001f, 0.05f), ref r);
            A(b, 0.08f, P(SynthWave.Sine, 95f, 0.2f, 0.6f).PitchEnv(8f, 0.015f).Env(0.001f, 0.05f), ref r);
            return b;
        }

        static float[] GetUp(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.24f);
            A(b, 0f, P(SynthWave.Triangle, r.Range(260f, 300f), 0.18f, 0.7f).Sweep(r.Range(680f, 760f), 0.12f).Vib(14f, 1.2f, 0.06f).Env(0.004f, 0.06f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.1f, 0.25f).Bp(1500f, 1.2f).Env(0.01f, 0.03f), ref r);
            return b;
        }

        static float[] KO(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.7f);
            // coconut bonk
            A(b, 0f, P(SynthWave.Sine, 520f, 0.3f).PitchEnv(4f, 0.01f).Env(0.0005f, 0.06f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1370f, 0.15f, 0.4f).Env(0.0005f, 0.025f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.05f, 0.3f).Bp(2000f, 2f).Env(0.0003f, 0.006f), ref r);
            // cartoon birds circling the head
            float t = 0.18f;
            int chirps = 5 + v;
            for (int k = 0; k < chirps; k++)
            {
                float f = r.Range(2600f, 3800f);
                var c = P(SynthWave.Sine, f, 0.1f, 0.3f).Sweep(f * r.Range(1.2f, 1.45f), 0.04f).Then(f * 0.9f, 0.05f).Vib(34f, 1.4f).Env(0.005f, 0.04f);
                A(b, t, c, ref r);
                if (r.Chance(0.6f)) A(b, t + 0.09f, c, ref r);
                t += r.Range(0.15f, 0.24f);
            }
            A(b, 0.2f, P(SynthWave.Sine, 1800f, 1.2f, 0.08f).Trem(6f, 0.8f).Env(0.1f, 0.5f), ref r);
            AudioFx.Reverb(b, 0.18f, 0.5f, 0.4f, 0.8f);
            return b;
        }

        static float[] Lost(ref AudioRng r)
        {
            var b = Synth.Buffer(2.4f);
            // falling-away slide whistle that fades with distance ...
            A(b, 0f, P(SynthWave.Sine, 1500f, 1.6f, 0.8f).Sweep(210f, 1.5f).Vib(6f, 0.4f).Env(0.02f, 0.75f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 1.4f, 0.05f).Bp(1500f, 6f, 230f).Env(0.02f, 0.6f), ref r);
            // ... and a distant thud far below
            A(b, 1.68f, P(SynthWave.Sine, 70f, 0.35f, 0.45f).PitchEnv(8f, 0.02f).Lp(400f).Env(0.001f, 0.12f), ref r);
            A(b, 1.68f, P(SynthWave.Noise, 0f, 0.2f, 0.25f).Lp(300f).Env(0.001f, 0.05f), ref r);
            AudioFx.Reverb(b, 0.35f, 0.8f, 0.5f, 1.2f);
            return b;
        }

        static float[] Revive(ref AudioRng r)
        {
            var b = Synth.Buffer(1.3f);
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            for (int i = 0; i < notes.Length; i++) Bell(b, i * 0.07f, notes[i], 0.25f, 0.45f, ref r, 4f, 1.2f);
            SynthGen.Grains(b, 0.05f, 0.65f, 14, 3000f, 7000f, 0.01f, 0.03f, 0.18f, false, ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.6f, 0.12f).Hp(4000f).Env(0.3f, 0.15f), ref r);
            AudioFx.Reverb(b, 0.3f, 0.6f, 0.35f);
            return b;
        }

        static float[] Captured(ref AudioRng r)
        {
            var b = Synth.Buffer(0.8f);
            // clamp "chk-chk-CLANG"
            A(b, 0f, P(SynthWave.Noise, 0f, 0.04f, 0.6f).Bp(2500f, 3f).Env(0.0003f, 0.008f), ref r);
            A(b, 0.05f, P(SynthWave.Noise, 0f, 0.04f, 0.6f).Bp(2300f, 3f).Env(0.0003f, 0.008f), ref r);
            A(b, 0.08f, P(SynthWave.Sine, 620f, 0.5f, 0.5f).Fm(2.76f, 3f, 0.08f).Env(0.0005f, 0.15f), ref r);
            // robotic "got-cha!" two-tone
            A(b, 0.2f, P(SynthWave.Square, 784f, 0.13f, 0.3f).Lp(3000f).Env(0.003f, 0f).Rel(0.11f, 0.02f), ref r);
            A(b, 0.34f, P(SynthWave.Square, 523f, 0.3f, 0.3f).Lp(3000f).Env(0.003f, 0f).Rel(0.22f, 0.04f).Vib(6f, 0.3f), ref r);
            return b;
        }

        static float[] Hurt(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.2f);
            A(b, 0f, P(SynthWave.Sine, r.Range(130f, 160f), 0.18f).PitchEnv(10f, 0.01f).Env(0.0005f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.08f, 0.55f).Bp(1500f, 0.8f).Env(0.0005f, 0.02f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.15f, 0.4f).Lp(400f).Env(0.001f, 0.04f), ref r);
            AudioFx.Saturate(b, 1.3f);
            return b;
        }

        static float[] BridgeForm(ref AudioRng r)
        {
            var b = Synth.Buffer(0.85f);
            SynthGen.Creak(b, 0f, 0.55f, 40f, 120f, 700f, 1100f, 6f, 0.6f, ref r);                       // rubber stick-slip
            A(b, 0f, P(SynthWave.Saw, 70f, 0.62f, 0.35f).Sweep(210f, 0.55f).Lp(900f, 4f).Vib(9f, 0.3f).Env(0.05f, 0f).Rel(0.55f, 0.04f), ref r);
            A(b, 0.55f, P(SynthWave.Sine, 200f, 0.25f, 0.45f).Vib(14f, 2f, 0.1f).Env(0.002f, 0.06f), ref r); // settle boing
            return b;
        }

        static float[] BridgeCreak(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.55f);
            bool up = (v & 1) == 0;
            SynthGen.Creak(b, 0f, 0.5f, up ? 25f : 60f, up ? 60f : 25f, r.Range(480f, 620f), r.Range(750f, 950f), 8f, 0.8f, ref r);
            A(b, 0f, P(SynthWave.Saw, r.Range(80f, 100f), 0.5f, 0.12f).Lp(400f, 2f).Env(0.08f, 0.2f), ref r);
            return b;
        }

        static float[] BridgeCollapse(ref AudioRng r)
        {
            var b = Synth.Buffer(1.4f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.05f, 0.8f).Hp(2000f).Env(0.0003f, 0.01f), ref r);              // snap
            A(b, 0f, P(SynthWave.Sine, 900f, 0.08f, 0.4f).Env(0.0003f, 0.02f), ref r);
            SynthGen.Creak(b, 0.02f, 0.3f, 120f, 30f, 900f, 400f, 6f, 0.5f, ref r);
            A(b, 0.05f, P(SynthWave.Sine, 520f, 0.65f, 0.4f).Sweep(110f, 0.6f).Vib(12f, 1.5f).Env(0.01f, 0.3f), ref r); // "wheeoo" fall
            A(b, 0.58f, P(SynthWave.Noise, 0f, 0.5f, 0.9f).Lp(1200f).Env(0.001f, 0.15f), ref r);               // crash
            A(b, 0.58f, P(SynthWave.Sine, 60f, 0.5f, 0.8f).PitchEnv(10f, 0.02f).Env(0.001f, 0.15f), ref r);
            SynthGen.Grains(b, 0.6f, 0.45f, 16, 500f, 2500f, 0.005f, 0.02f, 0.3f, true, ref r, 0.25f);
            AudioFx.Saturate(b, 1.3f);
            AudioFx.Reverb(b, 0.15f, 0.5f, 0.4f);
            return b;
        }

        static float[] BridgeRecover(ref AudioRng r)
        {
            var b = Synth.Buffer(0.45f);
            A(b, 0f, P(SynthWave.Sine, 180f, 0.3f, 0.8f).Sweep(420f, 0.08f).Vib(15f, 2f, 0.1f).Env(0.002f, 0.12f), ref r);
            A(b, 0.2f, P(SynthWave.Noise, 0f, 0.04f, 0.7f).Hp(3000f).Env(0.0003f, 0.006f), ref r);           // snap
            A(b, 0.2f, P(SynthWave.Sine, 1600f, 0.05f, 0.3f).Env(0.0003f, 0.01f), ref r);
            return b;
        }

        static float[] Bonk(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.22f);
            float f = r.Range(470f, 640f);
            A(b, 0f, P(SynthWave.Sine, f, 0.2f).PitchEnv(5f, 0.008f).Env(0.0005f, 0.045f), ref r);
            A(b, 0f, P(SynthWave.Sine, f * 2.7f, 0.1f, 0.4f).Env(0.0005f, 0.02f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.03f, 0.3f).Bp(1800f, 2f).Env(0.0003f, 0.006f), ref r);
            return b;
        }
    }
}
