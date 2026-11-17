// =====================================================================================
//  SHIFTERS - SfxRecipes.Interface.cs   [owned by the AUDIO agent]
//  Switcher wheel, GTA-ish switch whoosh/landing, perspective scan pulse and UI sounds.
// =====================================================================================
namespace Shifters
{
    public static partial class SfxRecipes
    {
        static float[] SwitcherOpen(ref AudioRng r)
        {
            var b = Synth.Buffer(1.25f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.45f, 0.7f).Bp(3500f, 2f, 350f).Env(0.03f, 0.22f), ref r);       // swoosh down
            A(b, 0f, P(SynthWave.Saw, 55f, 1.1f, 0.35f).Lp(300f, 1f).Env(0.25f, 0.6f), ref r);                 // low drone
            A(b, 0f, P(SynthWave.Sine, 110f, 1.1f, 0.25f).Trem(3f, 0.4f).Env(0.3f, 0.7f), ref r);
            AudioFx.Reverb(b, 0.2f, 0.6f, 0.4f);
            return b;
        }

        static float[] SwitcherClose(ref AudioRng r)
        {
            var b = Synth.Buffer(0.4f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.3f, 0.6f).Bp(400f, 2f, 3000f).Env(0.1f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Sine, 300f, 0.25f, 0.3f).Sweep(900f, 0.15f).Env(0.05f, 0.05f), ref r);
            A(b, 0.22f, P(SynthWave.Sine, 900f, 0.08f, 0.4f).Sweep(1400f, 0.03f).Env(0.001f, 0.02f), ref r);  // pop
            return b;
        }

        static float[] SwitchWhoosh(ref AudioRng r)
        {
            var b = Synth.Buffer(0.9f);
            A(b, 0f, P(SynthWave.Pink, 0f, 0.8f, 0.8f).Bp(250f, 1.5f, 2200f).Env(0.28f, 0.2f), ref r);
            A(b, 0f, P(SynthWave.Saw, 80f, 0.8f, 0.25f).Sweep(160f, 0.35f).Then(70f, 0.4f).Lp(600f).Env(0.2f, 0.3f), ref r);
            A(b, 0.1f, P(SynthWave.Noise, 0f, 0.6f, 0.15f).Hp(6000f).Env(0.2f, 0.15f), ref r);
            return b;
        }

        static float[] SwitchLand(ref AudioRng r)
        {
            var b = Synth.Buffer(0.8f);
            A(b, 0f, P(SynthWave.Sine, 75f, 0.5f).PitchEnv(14f, 0.025f).Env(0.001f, 0.15f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.2f, 0.5f).Lp(500f).Env(0.001f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Sine, 40f, 0.6f, 0.4f).Env(0.002f, 0.25f), ref r);
            AudioFx.Reverb(b, 0.15f, 0.5f, 0.5f);
            return b;
        }

        static float[] ScanPulse(ref AudioRng r)
        {
            var b = Synth.Buffer(1.3f);
            A(b, 0f, P(SynthWave.Sine, 160f, 0.9f, 0.7f).Sweep(340f, 0.5f).Fm(1.5f, 2f, 0.3f).Env(0.04f, 0.35f), ref r);   // "vwoom"
            A(b, 0f, P(SynthWave.Saw, 80f, 0.8f, 0.3f).Sweep(170f, 0.5f).Lp(300f, 1.5f, 1600f).Env(0.05f, 0.3f), ref r);
            A(b, 0.3f, P(SynthWave.Sine, 680f, 0.7f, 0.15f).Trem(9f, 0.5f).Env(0.01f, 0.3f), ref r);
            AudioFx.Reverb(b, 0.35f, 0.6f, 0.4f);
            return b;
        }

        static float[] Denied(ref AudioRng r)
        {
            var b = Synth.Buffer(0.36f);
            for (int k = 0; k < 2; k++)
            {
                float at = k * 0.17f;
                A(b, at, P(SynthWave.Square, 120f, 0.13f, 0.5f).Lp(1200f).Env(0.003f, 0.1f, 0.7f), ref r);
                A(b, at, P(SynthWave.Square, 127f, 0.13f, 0.4f).Lp(1200f).Env(0.003f, 0.1f, 0.7f), ref r);
            }
            return b;
        }

        static float[] UIClick(ref AudioRng r)
        {
            var b = Synth.Buffer(0.08f);
            A(b, 0f, P(SynthWave.Sine, 1300f, 0.07f, 0.8f).Sweep(900f, 0.03f).Env(0.0005f, 0.018f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.02f, 0.35f).Bp(4000f, 2f).Env(0.0003f, 0.003f), ref r);
            return b;
        }

        static float[] UIBack(ref AudioRng r)
        {
            var b = Synth.Buffer(0.16f);
            A(b, 0f, P(SynthWave.Sine, 900f, 0.06f, 0.6f).Env(0.001f, 0.03f), ref r);
            A(b, 0.055f, P(SynthWave.Sine, 620f, 0.09f, 0.6f).Env(0.001f, 0.04f), ref r);
            return b;
        }

        static float[] UIError(ref AudioRng r)
        {
            var b = Synth.Buffer(0.34f);
            for (int k = 0; k < 2; k++)
            {
                A(b, k * 0.16f, P(SynthWave.Square, 150f, 0.12f, 0.5f).Lp(1500f).Env(0.002f, 0.1f, 0.8f), ref r);
                A(b, k * 0.16f, P(SynthWave.Square, 159f, 0.12f, 0.35f).Lp(1500f).Env(0.002f, 0.1f, 0.8f), ref r);
            }
            return b;
        }

        static float[] UIPop(ref AudioRng r)
        {
            var b = Synth.Buffer(0.1f);
            A(b, 0f, P(SynthWave.Sine, 380f, 0.09f, 0.9f).Sweep(1300f, 0.04f).Env(0.001f, 0.03f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.02f, 0.2f).Hp(3000f).Env(0.0003f, 0.004f), ref r);
            return b;
        }

        static float[] Banner(ref AudioRng r)
        {
            var b = Synth.Buffer(1.1f);
            float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f };
            foreach (float f in chord) A(b, 0.03f, P(SynthWave.Sine, f, 0.8f, 0.3f).Fm(1f, 1.5f, 0.2f).Env(0.002f, 0.35f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.25f, 0.3f).Bp(800f, 1.5f, 4000f).Env(0.08f, 0.08f), ref r);
            A(b, 0.03f, P(SynthWave.Sine, 130f, 0.3f, 0.5f).PitchEnv(7f, 0.02f).Env(0.001f, 0.1f), ref r);
            AudioFx.Reverb(b, 0.25f, 0.6f, 0.4f);
            return b;
        }

        static float[] TutorialPop(ref AudioRng r)
        {
            var b = Synth.Buffer(1.0f);
            Bell(b, 0f, 1318.5f, 0.3f, 0.5f, ref r);
            Bell(b, 0.12f, 1760f, 0.35f, 0.5f, ref r);
            AudioFx.Reverb(b, 0.3f, 0.6f, 0.35f);
            return b;
        }
    }
}
