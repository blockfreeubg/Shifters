// =====================================================================================
//  SHIFTERS - SfxRecipes.Puzzle.cs   [owned by the AUDIO agent]
//  Puzzle & level sounds: plates, levers, buttons, timers, doors, terminals, jingles,
//  fanfare, machinery loops (elevator hum, conveyor), hazards, breakables, sad trombone.
// =====================================================================================
namespace Shifters
{
    public static partial class SfxRecipes
    {
        static float[] PlatePress(ref AudioRng r)
        {
            var b = Synth.Buffer(0.28f);
            A(b, 0f, P(SynthWave.Sine, 180f, 0.15f, 0.8f).Sweep(120f, 0.05f).Env(0.001f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.08f, 0.55f).Bp(900f, 1.5f).Env(0.0005f, 0.02f), ref r);
            A(b, 0.005f, P(SynthWave.Sine, 1450f, 0.05f, 0.2f).Env(0.0005f, 0.01f), ref r);
            A(b, 0.06f, P(SynthWave.Sine, 140f, 0.15f, 0.45f).PitchEnv(5f, 0.01f).Env(0.001f, 0.04f), ref r);
            return b;
        }

        static float[] PlateRelease(ref AudioRng r)
        {
            var b = Synth.Buffer(0.22f);
            A(b, 0f, P(SynthWave.Sine, 140f, 0.14f, 0.6f).Sweep(210f, 0.04f).Env(0.001f, 0.04f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.06f, 0.4f).Bp(1200f, 1.5f).Env(0.0005f, 0.015f), ref r);
            A(b, 0.03f, P(SynthWave.Sine, 300f, 0.15f, 0.2f).Vib(20f, 1.5f, 0.08f).Env(0.002f, 0.05f), ref r);
            return b;
        }

        static float[] Lever(ref AudioRng r)
        {
            var b = Synth.Buffer(0.75f);
            for (int k = 0; k < 3; k++)
            {
                A(b, k * 0.05f, P(SynthWave.Noise, 0f, 0.03f, 0.45f).Bp(3000f + k * 200f, 4f).Env(0.0003f, 0.004f), ref r);
                A(b, k * 0.05f, P(SynthWave.Sine, 2400f, 0.03f, 0.2f).Env(0.0003f, 0.003f), ref r);
            }
            A(b, 0.16f, P(SynthWave.Sine, 330f, 0.5f, 0.8f).Fm(2.76f, 4f, 0.06f).Env(0.0005f, 0.18f), ref r);
            A(b, 0.16f, P(SynthWave.Sine, 911f, 0.3f, 0.3f).Fm(1.41f, 2f, 0.05f).Env(0.0005f, 0.1f), ref r);
            A(b, 0.16f, P(SynthWave.Noise, 0f, 0.1f, 0.4f).Lp(800f).Env(0.0005f, 0.03f), ref r);
            AudioFx.Reverb(b, 0.12f, 0.4f, 0.4f, 0.7f);
            return b;
        }

        static float[] ButtonPress(ref AudioRng r)
        {
            var b = Synth.Buffer(0.11f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.02f, 0.55f).Bp(3500f, 3f).Env(0.0003f, 0.003f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1800f, 0.04f, 0.4f).Env(0.0003f, 0.006f), ref r);
            A(b, 0f, P(SynthWave.Sine, 300f, 0.06f, 0.3f).Env(0.0005f, 0.02f), ref r);
            A(b, 0.035f, P(SynthWave.Sine, 1500f, 0.04f, 0.3f).Env(0.0003f, 0.005f), ref r);
            return b;
        }

        static float[] TimerEnd(ref AudioRng r)
        {
            var b = Synth.Buffer(0.85f);
            A(b, 0f, P(SynthWave.Saw, 220f, 0.8f, 0.45f).Lp(1800f, 1f).Env(0.003f, 0f).Rel(0.7f, 0.03f), ref r);
            A(b, 0f, P(SynthWave.Saw, 233f, 0.8f, 0.45f).Lp(1800f, 1f).Env(0.003f, 0f).Rel(0.7f, 0.03f), ref r);
            A(b, 0f, P(SynthWave.Square, 110f, 0.8f, 0.25f).Lp(900f).Env(0.003f, 0f).Rel(0.7f, 0.03f), ref r);
            AudioFx.Saturate(b, 1.5f);
            return b;
        }

        static float[] DoorOpen(ref AudioRng r)
        {
            var b = Synth.Buffer(1.45f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.5f, 0.55f).Hp(2500f).Env(0.01f, 0.25f), ref r);                   // pneumatic hiss
            A(b, 0.05f, P(SynthWave.Saw, 48f, 1.25f, 0.5f).Lp(220f, 1.5f).Vib(5f, 0.2f).Env(0.15f, 0f).Rel(1.0f, 0.12f), ref r); // motor rumble
            A(b, 0.05f, P(SynthWave.Pink, 0f, 1.25f, 0.4f).Lp(300f).Env(0.2f, 0f).Rel(1.0f, 0.12f), ref r);
            A(b, 1.12f, P(SynthWave.Sine, 110f, 0.25f, 0.7f).PitchEnv(8f, 0.015f).Env(0.0005f, 0.06f), ref r);  // end clunk
            A(b, 1.12f, P(SynthWave.Noise, 0f, 0.1f, 0.4f).Lp(700f).Env(0.0005f, 0.02f), ref r);
            return b;
        }

        static float[] DoorClose(ref AudioRng r)
        {
            var b = Synth.Buffer(1.05f);
            A(b, 0f, P(SynthWave.Saw, 52f, 0.8f, 0.45f).Lp(240f, 1.5f).Env(0.1f, 0f).Rel(0.62f, 0.08f), ref r);
            A(b, 0f, P(SynthWave.Pink, 0f, 0.8f, 0.35f).Lp(300f).Env(0.1f, 0f).Rel(0.62f, 0.08f), ref r);
            A(b, 0.7f, P(SynthWave.Sine, 90f, 0.3f, 0.9f).PitchEnv(10f, 0.02f).Env(0.0005f, 0.1f), ref r);
            A(b, 0.7f, P(SynthWave.Noise, 0f, 0.15f, 0.5f).Lp(600f).Env(0.0005f, 0.04f), ref r);
            return b;
        }

        static float[] DoorSlam(ref AudioRng r)
        {
            var b = Synth.Buffer(1.2f);
            A(b, 0f, P(SynthWave.Sine, 70f, 0.5f).PitchEnv(12f, 0.02f).Env(0.0005f, 0.15f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.25f, 0.8f).Lp(1500f).Env(0.0005f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Sine, 410f, 0.8f, 0.3f).Fm(2.4f, 3f, 0.1f).Env(0.0005f, 0.35f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1170f, 0.5f, 0.12f).Env(0.0005f, 0.2f), ref r);
            AudioFx.Saturate(b, 1.5f);
            AudioFx.Reverb(b, 0.25f, 0.7f, 0.4f);
            return b;
        }

        static float[] Terminal(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.4f);
            float[] scale = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f };
            int count = 3 + v % 3;
            for (int k = 0; k < count; k++)
            {
                float f = scale[r.Range(0, scale.Length)];
                A(b, k * 0.065f, P(SynthWave.Pulse, f, 0.05f, 0.35f).Pwm(0.25f).Lp(4000f).Env(0.002f, 0.03f, 0.6f).Bits(12f), ref r);
            }
            return b;
        }

        static float[] HackSuccess(ref AudioRng r)
        {
            var b = Synth.Buffer(0.95f);
            float[] arp = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
            for (int i = 0; i < arp.Length; i++)
                A(b, i * 0.06f, P(SynthWave.Pulse, arp[i], 0.1f, 0.35f).Pwm(0.3f).Lp(5000f).Env(0.002f, 0.05f, 0.5f), ref r);
            float[] chord = { 1046.5f, 1318.5f, 1568f };
            foreach (float f in chord) A(b, 0.32f, P(SynthWave.Pulse, f, 0.5f, 0.2f).Pwm(0.3f).Lp(5000f).Env(0.002f, 0.25f), ref r);
            Bell(b, 0.32f, 2093f, 0.3f, 0.25f, ref r);
            AudioFx.Crush(b, 2, 20f);
            return b;
        }

        static float[] HackFail(ref AudioRng r)
        {
            var b = Synth.Buffer(1.0f);
            float[] notes = { 392f, 370f, 349.2f, 329.6f };
            for (int i = 0; i < notes.Length; i++)
            {
                bool last = i == notes.Length - 1;
                var p = P(SynthWave.Square, notes[i], last ? 0.5f : 0.15f, 0.4f).Lp(1600f).Env(0.004f, 0f).Rel(last ? 0.4f : 0.12f, 0.04f);
                if (last) p = p.Vib(6f, 0.6f);
                A(b, i * 0.15f, p, ref r);
            }
            return b;
        }

        static float[] PuzzleSolved(ref AudioRng r)
        {
            var b = Synth.Buffer(1.6f);
            float[] notes = { 783.99f, 1046.5f, 1318.5f, 1568f };
            for (int i = 0; i < notes.Length; i++)
                A(b, i * 0.08f, P(SynthWave.Sine, notes[i], 0.6f, 0.5f).Fm(4f, 1f, 0.05f).Env(0.001f, 0.25f), ref r);
            float[] chord = { 1046.5f, 1318.5f, 1568f, 2093f };
            foreach (float f in chord) A(b, 0.32f, P(SynthWave.Sine, f, 1.0f, 0.28f).Fm(1f, 1.3f, 0.3f).Env(0.002f, 0.6f), ref r);
            SynthGen.Grains(b, 0.32f, 0.7f, 12, 3500f, 8000f, 0.01f, 0.03f, 0.12f, false, ref r);
            AudioFx.Reverb(b, 0.3f, 0.6f, 0.35f);
            return b;
        }

        static float[] Checkpoint(ref AudioRng r)
        {
            var b = Synth.Buffer(1.5f);
            float[] notes = { 783.99f, 987.77f, 1174.66f };
            for (int i = 0; i < notes.Length; i++) Bell(b, i * 0.1f, notes[i], 0.4f, 0.45f, ref r, 3.5f, 1.5f);
            Bell(b, 0.3f, 1174.66f, 0.6f, 0.3f, ref r);
            Bell(b, 0.3f, 1568f, 0.6f, 0.3f, ref r);
            A(b, 0.3f, P(SynthWave.Sine, 2349f, 0.9f, 0.08f).Trem(7f, 0.7f).Env(0.05f, 0.4f), ref r);
            AudioFx.Reverb(b, 0.35f, 0.65f, 0.35f);
            return b;
        }

        static float[] LevelComplete(ref AudioRng r)
        {
            var b = Synth.Buffer(3.0f);
            // "ta-ta-ta-TAAAA"
            float[] pickup = { 392f, 523.25f, 659.25f };
            for (int i = 0; i < pickup.Length; i++) Brass(b, i * 0.14f, pickup[i], 0.1f, 0.6f, ref r, 0f);
            Brass(b, 0.42f, 783.99f, 1.6f, 0.7f, ref r, 0.18f);
            float[] chord = { 261.63f, 329.63f, 392f };
            foreach (float f in chord) Brass(b, 0.42f, f, 1.6f, 0.4f, ref r, 0.1f);
            for (int k = 0; k < 3; k++)
                A(b, k * 0.14f, P(SynthWave.Sine, 98f, 0.3f, 0.4f).PitchEnv(5f, 0.03f).Env(0.001f, 0.08f), ref r);   // timpani pickups
            A(b, 0.42f, P(SynthWave.Sine, 98f, 0.9f, 0.8f).PitchEnv(5f, 0.04f).Env(0.001f, 0.3f), ref r);
            A(b, 0.42f, P(SynthWave.Noise, 0f, 2.2f, 0.3f).Hp(4000f).Env(0.001f, 0.8f), ref r);                       // cymbal
            AudioFx.Reverb(b, 0.3f, 0.7f, 0.4f);
            return b;
        }

        static float[] ElevatorHum(ref AudioRng r)
        {
            var b = Synth.Buffer(2.25f);
            A(b, 0f, P(SynthWave.Sine, 60f, 2.25f, 0.5f).Trem(0.5f, 0.2f), ref r);
            A(b, 0f, P(SynthWave.Sine, 120f, 2.25f, 0.3f), ref r);
            A(b, 0f, P(SynthWave.Sine, 180f, 2.25f, 0.15f).Trem(1f, 0.3f), ref r);
            A(b, 0f, P(SynthWave.Pink, 0f, 2.25f, 0.15f).Lp(400f), ref r);
            return AudioFx.MakeLoop(b, 0.25f);
        }

        static float[] Steam(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.1f);
            A(b, 0f, P(SynthWave.Noise, 0f, 1.0f, 0.7f).Hp(1800f).Env(0.015f, 0.25f, 0f, v == 0 ? 0.4f : 0.2f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.12f, 0.4f).Bp(5000f, 2f).Env(0.001f, 0.05f), ref r);
            A(b, 0f, P(SynthWave.Pink, 0f, 0.9f, 0.2f).Lp(800f).Env(0.02f, 0.3f), ref r);
            return b;
        }

        static float[] Crusher(ref AudioRng r)
        {
            var b = Synth.Buffer(1.3f);
            A(b, 0f, P(SynthWave.Sine, 50f, 0.7f).PitchEnv(14f, 0.03f).Env(0.0005f, 0.25f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.35f, 0.9f).Lp(2000f).Env(0.0005f, 0.08f), ref r);
            A(b, 0f, P(SynthWave.Sine, 230f, 0.9f, 0.35f).Fm(2.76f, 5f, 0.1f).Env(0.0005f, 0.4f), ref r);
            SynthGen.Grains(b, 0.02f, 0.5f, 18, 400f, 2500f, 0.005f, 0.02f, 0.3f, true, ref r, 0.2f);
            AudioFx.Saturate(b, 2f);
            AudioFx.Reverb(b, 0.2f, 0.6f, 0.4f);
            return b;
        }

        static float[] LaserZap(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.3f);
            float s = v == 0 ? 1f : 1.25f;
            A(b, 0f, P(SynthWave.Saw, 2800f * s, 0.25f, 0.5f).Sweep(180f, 0.18f).Lp(6000f).Env(0.001f, 0.08f), ref r);
            A(b, 0f, P(SynthWave.Square, 1400f * s, 0.25f, 0.25f).Sweep(90f, 0.2f).Env(0.001f, 0.08f), ref r);
            A(b, 0f, P(SynthWave.Sine, 900f, 0.2f, 0.3f).Fm(1.5f, 3f).Env(0.001f, 0.08f), ref r);
            AudioFx.Crush(b, 1, 10f);
            return b;
        }

        static float[] Bounce(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.7f);
            float s = v == 0 ? 1f : 0.85f;
            A(b, 0f, P(SynthWave.Sine, 140f * s, 0.65f, 0.9f).Sweep(620f * s, 0.12f).Then(520f * s, 0.2f).Vib(13f, 2.5f, 0.25f).Env(0.002f, 0.25f), ref r);
            A(b, 0f, P(SynthWave.Saw, 70f * s, 0.4f, 0.25f).Sweep(300f * s, 0.12f).Bp(600f, 3f, 1800f).Env(0.002f, 0.15f), ref r);
            return b;
        }

        static float[] Conveyor(ref AudioRng r)
        {
            var b = Synth.Buffer(2.25f);
            A(b, 0f, P(SynthWave.Saw, 45f, 2.25f, 0.4f).Lp(160f), ref r);
            A(b, 0f, P(SynthWave.Pink, 0f, 2.25f, 0.3f).Lp(250f), ref r);
            for (int k = 0; k < 14; k++)
            {
                float at = k / 6f;
                A(b, at, P(SynthWave.Noise, 0f, 0.04f, 0.35f).Bp(1800f, 3f).Env(0.0005f, 0.008f), ref r);
                A(b, at, P(SynthWave.Sine, 900f, 0.05f, 0.15f).Env(0.0005f, 0.01f), ref r);
            }
            return AudioFx.MakeLoop(b, 0.25f);
        }

        static float[] GlassBreak(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.15f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.1f, 0.8f).Hp(2000f).Env(0.0003f, 0.02f), ref r);
            A(b, 0f, P(SynthWave.Sine, 2600f, 0.1f, 0.4f).Env(0.0003f, 0.02f), ref r);
            SynthGen.Grains(b, 0.01f, 0.7f, 28 + v * 6, 2500f, 8000f, 0.01f, 0.05f, 0.45f, false, ref r, 0.25f);
            SynthGen.Grains(b, 0.01f, 0.5f, 18, 3000f, 9000f, 0.002f, 0.008f, 0.3f, true, ref r, 0.2f);
            AudioFx.Reverb(b, 0.2f, 0.5f, 0.3f);
            return b;
        }

        static float[] Crumble(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.2f);
            SynthGen.Grains(b, 0f, 0.9f, 40, 300f, 1500f, 0.01f, 0.04f, 0.6f, true, ref r, 0.5f);
            A(b, 0f, P(SynthWave.Pink, 0f, 1.0f, 0.6f).Lp(200f).Env(0.05f, 0.4f), ref r);
            for (int k = 0; k < 3; k++)
                A(b, r.Range(0f, 0.6f), P(SynthWave.Sine, r.Range(80f, 120f), 0.25f, 0.5f).PitchEnv(8f, 0.015f).Env(0.0005f, 0.06f), ref r);
            return b;
        }

        static float[] Wobble(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.75f);
            float f = v == 0 ? 210f : 180f;
            A(b, 0f, P(SynthWave.Sine, f, 0.7f, 0.8f).Vib(6.5f, 3f).Trem(6.5f, 0.5f).Env(0.01f, 0.35f), ref r);
            A(b, 0f, P(SynthWave.Triangle, f * 0.5f, 0.7f, 0.3f).Vib(6.5f, 3f).Env(0.01f, 0.35f), ref r);
            return b;
        }

        static float[] SadTrombone(ref AudioRng r)
        {
            var b = Synth.Buffer(3.2f);
            float[] notes = { 233.08f, 220f, 207.65f, 196f };
            float t = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                bool last = i == notes.Length - 1;
                float len = last ? 1.5f : 0.4f;
                for (int k = 0; k < 2; k++)
                {
                    var p = P(SynthWave.Saw, notes[i] * (k == 0 ? 0.997f : 1.003f), len + 0.3f, 0.4f)
                        .Lp(320f, 1.5f, last ? 1100f : 1500f).Env(0.03f, 0.25f, 0.8f).Rel(len, 0.12f);
                    if (last) p = p.Vib(5f, 0.55f).Trem(5f, 0.25f);
                    A(b, t, p, ref r);
                }
                t += 0.46f;
            }
            AudioFx.Reverb(b, 0.2f, 0.5f, 0.4f);
            return b;
        }
    }
}
