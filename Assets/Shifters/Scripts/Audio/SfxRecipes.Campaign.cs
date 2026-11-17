// =====================================================================================
//  SHIFTERS - SfxRecipes.Campaign.cs   [owned by the AUDIO agent]
//  "THE GREAT SHIFT" campaign sounds (CAMPAIGN 19 SfxId groups): story (kazoo, gavel, crowd,
//  thunder, klaxon, Percy's gurgle, slide whistle, BOO!, TV static, dialogue blip, act card),
//  bosses (roar, hit confirm, stomp, charge, defeat, oink, stamp, drum slam, shockwave, tram
//  bell), heist (camera beep/spotted, vault spin/open, coin shower), scary (stinger, cardboard
//  creak, vacuum hum, stapler, heartbeat) and the new weapons/tools. Comedy first: every scary
//  sound stays at the normal SFX level and most carry their own punchline.
//  Only SynthPatch / AudioFx / SynthGen calls (recorded into the SynthPlan => time-sliced).
// =====================================================================================
namespace Shifters
{
    public static partial class SfxRecipes
    {
        static float[] DispatchCampaign(SfxId id, int v, ref AudioRng r)
        {
            switch (id)
            {
                // story
                case SfxId.Kazoo: return KazooFanfare(v, ref r);
                case SfxId.Cymbal: return CymbalCrash(v, ref r);
                case SfxId.Gavel: return Gavel(v, ref r);
                case SfxId.CrowdGasp: return CrowdGasp(ref r);
                case SfxId.CrowdCheer: return CrowdCheer(ref r);
                case SfxId.Thunder: return Thunder(v, ref r);
                case SfxId.Whisper: return WhisperSfx(v, ref r);
                case SfxId.Creak: return DoorCreak(v, ref r);
                case SfxId.Klaxon: return Klaxon(ref r);
                case SfxId.Gurgle: return Gurgle(v, ref r);
                case SfxId.PrinterWhir: return PrinterWhir(ref r);
                case SfxId.ColorBurst: return ColorBurstSfx(v, ref r);
                case SfxId.SlideWhistleDown: return SlideWhistleDown(v, ref r);
                case SfxId.Boo: return Boo(v, ref r);
                case SfxId.TVStatic: return TvStatic(v, ref r);
                case SfxId.DialogueBlip: return DialogueBlip(v, ref r);
                case SfxId.ActCard: return ActCard(ref r);
                // bosses
                case SfxId.BossRoar: return BossRoar(v, ref r);
                case SfxId.BossHit: return BossHit(v, ref r);
                case SfxId.BossStomp: return BossStomp(v, ref r);
                case SfxId.BossCharge: return BossCharge(ref r);
                case SfxId.BossDefeat: return BossDefeat(ref r);
                case SfxId.Oink: return Oink(v, ref r);
                case SfxId.Stamp: return Stamp(v, ref r);
                case SfxId.DrumSlam: return DrumSlam(v, ref r);
                case SfxId.Shockwave: return Shockwave(ref r);
                case SfxId.TramBell: return TramBell(ref r);
                // heist
                case SfxId.CameraBeep: return CameraBeep(v, ref r);
                case SfxId.CameraSpotted: return CameraSpotted(ref r);
                case SfxId.VaultSpin: return VaultSpin(v, ref r);
                case SfxId.VaultOpen: return VaultOpen(ref r);
                case SfxId.CoinShower: return CoinShower(v, ref r);
                // scary
                case SfxId.ScareStinger: return ScareStinger(v, ref r);
                case SfxId.CardboardCreak: return CardboardCreak(v, ref r);
                case SfxId.VacuumHum: return VacuumHum(ref r);
                case SfxId.StaplerSnap: return StaplerSnap(v, ref r);
                case SfxId.Heartbeat: return Heartbeat(ref r);
                // weapons / tools
                case SfxId.LeafBlower: return LeafBlower(v, ref r);
                case SfxId.TShirtPop: return TShirtPop(v, ref r);
                case SfxId.CameraFlash: return CameraFlash(v, ref r);
                case SfxId.PaintSplat: return PaintSplat(v, ref r);
                case SfxId.BitShift: return BitShift(v, ref r);
                case SfxId.SwapZap: return SwapZap(v, ref r);
                case SfxId.Popper: return Popper(v, ref r);
                case SfxId.CapsLock: return CapsLockSfx(v, ref r);
                default: return null;
            }
        }

        // ------------------------------------------------------------------ shared campaign helpers
        /// <summary>Buzzy kazoo note: saw + narrow pulse through a nasal band-pass, wobbly vibrato, slight drive.</summary>
        static void KazooNote(float[] b, float at, float hz, float len, float gain, ref AudioRng r, float bendTo = 0f)
        {
            var saw = P(SynthWave.Saw, hz, len + 0.08f, gain * 0.55f).Bp(1150f, 0.9f).Vib(5.8f, 0.3f).Env(0.015f, 0f).Rel(len, 0.05f).Sat(2.2f);
            var buzz = P(SynthWave.Pulse, hz, len + 0.08f, gain * 0.25f).Pwm(0.12f).Hp(1600f).Lp(5200f).Vib(5.8f, 0.3f).Env(0.015f, 0f).Rel(len, 0.05f);
            if (bendTo > 0f) { saw = saw.Sweep(bendTo, len, false); buzz = buzz.Sweep(bendTo, len, false); }
            A(b, at, saw, ref r);
            A(b, at, buzz, ref r);
        }

        /// <summary>Breathy "hhuh"/"aah" noise voice through two formant band-passes.</summary>
        static void BreathVowel(float[] b, float at, float dur, float f1, float f2, float gain, ref AudioRng r, float rise = 1f)
        {
            A(b, at, P(SynthWave.Noise, 0f, dur, gain).Bp(f1, 4f, f1 * rise).Env(dur * 0.25f, dur * 0.4f), ref r);
            A(b, at, P(SynthWave.Noise, 0f, dur, gain * 0.6f).Bp(f2, 5f, f2 * rise).Env(dur * 0.25f, dur * 0.4f), ref r);
        }

        /// <summary>Formant "voice" from a saw (vowel F1/F2), used for BOO! and crowd voices.</summary>
        static void SawVowel(float[] b, float at, float hz, float len, float f1, float f2, float gain, ref AudioRng r, float toHz = 0f, float vib = 0.15f)
        {
            var p1 = P(SynthWave.Saw, hz, len + 0.1f, gain).Bp(f1, 3f).Vib(5.2f, vib).Env(0.03f, 0f).Rel(len, 0.08f);
            var p2 = P(SynthWave.Saw, hz, len + 0.1f, gain * 0.45f).Bp(f2, 4f).Vib(5.2f, vib).Env(0.03f, 0f).Rel(len, 0.08f);
            if (toHz > 0f) { p1 = p1.Sweep(toHz, len); p2 = p2.Sweep(toHz, len); }
            A(b, at, p1, ref r);
            A(b, at, p2, ref r);
        }

        static void Thud(float[] b, float at, float hz, float gain, float decay, ref AudioRng r)
        {
            A(b, at, P(SynthWave.Sine, hz, decay * 4f, gain).PitchEnv(14f, 0.02f).Env(0.0008f, decay).Sat(1.4f), ref r);
            A(b, at, P(SynthWave.Noise, 0f, 0.12f, gain * 0.5f).Lp(900f).Env(0.0005f, 0.03f), ref r);
        }

        // ================================================================== STORY
        static float[] KazooFanfare(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.35f);
            if (v == 0)
            {   // Max's own fanfare: "da-da-da-DAAAA" (G minor, pompous)
                float[] hz = { 392f, 392f, 392f, 523.25f, 466.16f, 523.25f };
                float[] at = { 0f, 0.11f, 0.22f, 0.36f, 0.66f, 0.8f };
                float[] ln = { 0.08f, 0.08f, 0.1f, 0.26f, 0.1f, 0.42f };
                for (int i = 0; i < hz.Length; i++) KazooNote(b, at[i], hz[i], ln[i], 0.9f, ref r);
            }
            else
            {   // deflating kazoo ("pfffrrrt" down a fifth)
                KazooNote(b, 0f, 466.16f, 0.18f, 0.9f, ref r);
                KazooNote(b, 0.22f, 440f, 0.85f, 0.9f, ref r, 260f);
            }
            AudioFx.Reverb(b, 0.12f, 0.35f, 0.4f);
            return b;
        }

        static float[] CymbalCrash(int v, ref AudioRng r)
        {
            float len = v == 0 ? 1.7f : 0.9f;
            var b = Synth.Buffer(len);
            A(b, 0f, P(SynthWave.Noise, 0f, len, 0.6f).Hp(4200f).Env(0.0008f, len * 0.42f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, len, 0.3f).Bp(7800f, 0.8f).Env(0.0008f, len * 0.3f), ref r);
            A(b, 0f, P(SynthWave.Sine, 452f, len, 0.12f).Fm(3.7f, 7f, 0.5f).Env(0.0008f, len * 0.35f), ref r);
            A(b, 0f, P(SynthWave.Sine, 1263f, len, 0.06f).Fm(2.3f, 4f, 0.3f).Env(0.0008f, len * 0.25f), ref r);
            return b;
        }

        static float[] Gavel(int v, ref AudioRng r)
        {
            int knocks = v == 0 ? 1 : 3;
            var b = Synth.Buffer(0.25f + knocks * 0.2f + 0.3f);
            for (int k = 0; k < knocks; k++)
            {
                float at = k * 0.2f;
                float g = k == knocks - 1 ? 1f : 0.75f;
                A(b, at, P(SynthWave.Sine, 520f, 0.2f, 0.7f * g).PitchEnv(5f, 0.006f).Env(0.0004f, 0.035f), ref r);     // wood
                A(b, at, P(SynthWave.Sine, 1240f, 0.1f, 0.3f * g).Env(0.0004f, 0.015f), ref r);
                A(b, at, P(SynthWave.Noise, 0f, 0.05f, 0.6f * g).Bp(2400f, 2f).Env(0.0003f, 0.006f), ref r);
                A(b, at, P(SynthWave.Sine, 120f, 0.3f, 0.5f * g).PitchEnv(8f, 0.01f).Env(0.0008f, 0.07f), ref r);        // the bench
            }
            AudioFx.Reverb(b, 0.22f, 0.65f, 0.35f);
            return b;
        }

        static float[] CrowdGasp(ref AudioRng r)
        {
            var b = Synth.Buffer(1.0f);
            for (int k = 0; k < 9; k++)
            {
                float at = r.Range(0f, 0.08f), d = r.Range(0.28f, 0.45f);
                BreathVowel(b, at, d, r.Range(650f, 900f), r.Range(1200f, 1700f), 0.35f, ref r, 1.35f);
            }
            for (int k = 0; k < 4; k++)   // a few voiced "oh!"s
                SawVowel(b, r.Range(0.02f, 0.1f), r.Range(180f, 320f), 0.22f, 520f, 900f, 0.18f, ref r, r.Range(260f, 420f));
            AudioFx.Reverb(b, 0.25f, 0.7f, 0.4f);
            return b;
        }

        static float[] CrowdCheer(ref AudioRng r)
        {
            var b = Synth.Buffer(1.9f);
            for (int k = 0; k < 10; k++)   // "yaaaay" voices
            {
                float at = r.Range(0f, 0.2f), hz = r.Range(170f, 380f);
                SawVowel(b, at, hz, r.Range(1.0f, 1.35f), r.Range(700f, 850f), r.Range(1150f, 1400f), 0.12f, ref r, hz * r.Range(1.1f, 1.3f), 0.3f);
            }
            A(b, 0f, P(SynthWave.Pink, 0f, 1.8f, 0.35f).Bp(1200f, 0.7f).Env(0.15f, 0f).Rel(1.3f, 0.45f), ref r);      // crowd bed
            SynthGen.Grains(b, 0.1f, 1.5f, 60, 1800f, 3200f, 0.006f, 0.012f, 0.45f, true, ref r);                    // clapping
            A(b, 0.35f, P(SynthWave.Sine, 2000f, 0.5f, 0.25f).Sweep(2900f, 0.12f).Then(2300f, 0.3f).Env(0.02f, 0f).Rel(0.42f, 0.05f), ref r); // whistle
            AudioFx.Reverb(b, 0.2f, 0.7f, 0.4f);
            AudioFx.FadeOut(b, 0.4f);
            return b;
        }

        static float[] Thunder(int v, ref AudioRng r)
        {
            float len = v == 0 ? 3.0f : 2.4f;
            var b = Synth.Buffer(len);
            float crack = v == 0 ? 0f : 0.25f;
            A(b, crack, P(SynthWave.Noise, 0f, 0.3f, 0.9f).Hp(1200f).Env(0.0005f, 0.06f), ref r);                    // crack
            SynthGen.Grains(b, crack, 0.35f, 30, 300f, 3000f, 0.004f, 0.02f, 0.5f, true, ref r, 0.15f);
            A(b, crack + 0.05f, P(SynthWave.Pink, 0f, len, 0.9f).Lp(320f, 0.8f, 120f).Env(0.08f, len * 0.35f), ref r); // rumble
            A(b, crack + 0.1f, P(SynthWave.Sine, 42f, len, 0.5f).Vib(3f, 2f).Env(0.15f, len * 0.3f), ref r);
            A(b, crack + 0.9f, P(SynthWave.Pink, 0f, len - 1f, 0.5f).Lp(220f).Trem(2.5f, 0.6f).Env(0.2f, len * 0.25f), ref r); // roll
            AudioFx.Reverb(b, 0.3f, 0.9f, 0.5f);
            AudioFx.FadeOut(b, 0.5f);
            return b;
        }

        static float[] WhisperSfx(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.3f);
            int syl = 4 + v;
            float t = 0.02f;
            for (int i = 0; i < syl && t < 1.0f; i++)
            {
                float d = r.Range(0.1f, 0.2f);
                if (r.Chance(0.5f)) A(b, t, P(SynthWave.Noise, 0f, 0.08f, 0.35f).Bp(6200f, 3f).Env(0.02f, 0.03f), ref r); // "s"
                BreathVowel(b, t + 0.04f, d, r.Range(450f, 800f), r.Range(1100f, 2200f), 0.45f, ref r, r.Range(0.85f, 1.15f));
                t += d + r.Range(0.02f, 0.08f);
            }
            AudioFx.Reverb(b, 0.35f, 0.75f, 0.5f);
            AudioFx.FadeOut(b, 0.2f);
            return b;
        }

        static float[] DoorCreak(int v, ref AudioRng r)
        {
            float len = 0.7f + 0.2f * v;
            var b = Synth.Buffer(len + 0.25f);
            SynthGen.Creak(b, 0.02f, len, 40f + 20f * v, 140f, 500f + 150f * v, 1400f, 14f, 0.9f, ref r);
            SynthGen.Creak(b, 0.05f, len * 0.8f, 30f, 90f, 900f, 2600f, 18f, 0.4f, ref r);
            AudioFx.Reverb(b, 0.2f, 0.6f, 0.4f);
            return b;
        }

        static float[] Klaxon(ref AudioRng r)
        {
            var b = Synth.Buffer(1.1f);
            for (int k = 0; k < 2; k++)
            {
                float at = k * 0.48f;
                A(b, at, P(SynthWave.Saw, 220f, 0.42f, 0.5f).Sweep(330f, 0.1f).Lp(2400f, 1.5f).Env(0.01f, 0f).Rel(0.36f, 0.04f).Sat(3f), ref r);
                A(b, at, P(SynthWave.Square, 222f, 0.42f, 0.35f).Sweep(333f, 0.1f).Lp(1800f).Env(0.01f, 0f).Rel(0.36f, 0.04f), ref r);
            }
            AudioFx.Saturate(b, 1.6f);
            AudioFx.Reverb(b, 0.15f, 0.5f, 0.4f);
            return b;
        }

        static float[] Gurgle(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.95f);
            int n = 9 + v * 3;
            for (int k = 0; k < n; k++)   // rising bubble "bloops"
            {
                float at = r.Range(0f, 0.72f), hz = r.Range(240f, 700f);
                A(b, at, P(SynthWave.Sine, hz, 0.09f, r.Range(0.35f, 0.7f)).Sweep(hz * r.Range(1.6f, 2.4f), 0.05f).Env(0.004f, 0.03f), ref r);
            }
            A(b, 0f, P(SynthWave.Pink, 0f, 0.9f, 0.25f).Lp(700f).Trem(17f, 0.8f).Env(0.05f, 0f).Rel(0.7f, 0.15f), ref r); // boiling
            if (v == 2) A(b, 0.6f, P(SynthWave.Sine, 2100f, 0.3f, 0.35f).Vib(9f, 0.5f).Env(0.02f, 0f).Rel(0.22f, 0.05f), ref r); // steam "fweee"
            return b;
        }

        static float[] PrinterWhir(ref AudioRng r)
        {
            var b = Synth.Buffer(1.5f);
            for (int line = 0; line < 4; line++)
            {
                float at = line * 0.34f;
                A(b, at, P(SynthWave.Square, 1300f + line * 40f, 0.24f, 0.18f).Trem(55f, 0.9f).Lp(3500f).Env(0.005f, 0f).Rel(0.22f, 0.02f), ref r); // print head
                A(b, at, P(SynthWave.Noise, 0f, 0.24f, 0.25f).Bp(3000f, 2f).Trem(55f, 0.9f).Env(0.005f, 0f).Rel(0.22f, 0.02f), ref r);
                A(b, at + 0.25f, P(SynthWave.Saw, 90f, 0.09f, 0.3f).Lp(500f).Env(0.003f, 0.04f), ref r);            // paper feed clunk
                A(b, at + 0.25f, P(SynthWave.Noise, 0f, 0.06f, 0.3f).Bp(1500f, 2f).Env(0.002f, 0.02f), ref r);
            }
            return b;
        }

        static float[] ColorBurstSfx(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.4f);
            A(b, 0f, P(SynthWave.Sine, 70f, 0.5f, 0.8f).PitchEnv(18f, 0.04f).Env(0.001f, 0.18f), ref r);               // whoomp
            A(b, 0f, P(SynthWave.Noise, 0f, 0.6f, 0.4f).Bp(600f, 1f, 4000f).Env(0.01f, 0.2f), ref r);                // fwoosh
            float[] arp = v == 0 ? new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f } : new[] { 587.33f, 739.99f, 880f, 1174.7f, 1480f, 1760f };
            for (int i = 0; i < arp.Length; i++) Bell(b, 0.05f + i * 0.045f, arp[i], 0.35f, 0.3f, ref r, 2f, 1f);
            SynthGen.Grains(b, 0.1f, 0.9f, 40, 2500f, 7000f, 0.01f, 0.04f, 0.22f, false, ref r, 0.4f);               // sparkle
            AudioFx.Reverb(b, 0.3f, 0.7f, 0.35f);
            return b;
        }

        static float[] SlideWhistleDown(int v, ref AudioRng r)
        {
            float len = v == 0 ? 0.65f : 0.9f;
            var b = Synth.Buffer(len + 0.15f);
            var p = P(SynthWave.Sine, 1900f, len + 0.05f, 0.8f).Sweep(v == 0 ? 420f : 300f, len).Vib(6.5f, 0.35f).Env(0.02f, 0f).Rel(len, 0.04f);
            A(b, 0f, p, ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, len, 0.06f).Bp(2500f, 2f, 700f).Env(0.02f, 0f).Rel(len, 0.04f), ref r);  // breath
            if (v == 1) A(b, len - 0.05f, P(SynthWave.Sine, 180f, 0.25f, 0.5f).PitchEnv(6f, 0.01f).Env(0.001f, 0.06f), ref r); // tiny bonk
            return b;
        }

        static float[] Boo(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.15f);
            float hz = v == 0 ? 150f : 230f;
            SawVowel(b, 0f, hz * 0.85f, 0.55f, 330f, 850f, 0.7f, ref r, hz * 1.15f, 0.6f);   // "BOOOO" (U formants, wobbly)
            A(b, 0f, P(SynthWave.Noise, 0f, 0.07f, 0.4f).Bp(800f, 2f).Env(0.001f, 0.015f), ref r);                     // the B
            if (v == 1) A(b, 0.6f, P(SynthWave.Square, 300f, 0.18f, 0.25f).Sweep(600f, 0.12f).Lp(2000f).Env(0.005f, 0.06f), ref r); // polite "boop?"
            AudioFx.Reverb(b, 0.28f, 0.75f, 0.45f);
            return b;
        }

        static float[] TvStatic(int v, ref AudioRng r)
        {
            float len = v == 0 ? 0.5f : 0.9f;
            var b = Synth.Buffer(len);
            A(b, 0f, P(SynthWave.Noise, 0f, len, 0.6f).Hp(700f).Lp(7000f).Trem(v == 0 ? 33f : 21f, 0.5f).Env(0.003f, 0f).Rel(len - 0.06f, 0.05f).Bits(10f), ref r);
            SynthGen.Grains(b, 0f, len - 0.05f, 25, 800f, 6000f, 0.002f, 0.006f, 0.45f, true, ref r);              // crackles
            A(b, 0f, P(SynthWave.Sine, 15734f / 2f, len, 0.05f).Env(0.003f, 0f).Rel(len - 0.06f, 0.05f), ref r);    // CRT whine
            return b;
        }

        static float[] DialogueBlip(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.05f);
            float hz = v == 0 ? 880f : v == 1 ? 990f : 784f;
            A(b, 0f, P(SynthWave.Triangle, hz, 0.045f, 0.8f).PitchEnv(2f, 0.004f).Env(0.0008f, 0.012f), ref r);
            A(b, 0f, P(SynthWave.Sine, hz * 2f, 0.02f, 0.15f).Env(0.0004f, 0.005f), ref r);
            return b;
        }

        static float[] ActCard(ref AudioRng r)
        {
            var b = Synth.Buffer(2.1f);
            A(b, 0f, P(SynthWave.Sine, 65f, 1.5f, 0.9f).PitchEnv(3f, 0.05f).Env(0.002f, 0.6f), ref r);              // timpani hit
            A(b, 0f, P(SynthWave.Noise, 0f, 0.2f, 0.4f).Lp(600f).Env(0.001f, 0.05f), ref r);
            float[] chord = { 130.81f, 196f, 261.63f, 311.13f, 392f };                                              // C minor "BWAAM"
            foreach (float f in chord) Brass(b, 0.02f, f, 1.0f, 0.4f, ref r, 0.08f);
            A(b, 0f, P(SynthWave.Noise, 0f, 2.0f, 0.4f).Hp(4000f).Env(0.001f, 0.8f), ref r);                         // cymbal
            float[] up = { 392f, 523.25f, 622.25f, 783.99f };
            for (int i = 0; i < up.Length; i++) Bell(b, 1.15f + i * 0.07f, up[i] * 2f, 0.3f, 0.18f, ref r);           // shimmer tag
            AudioFx.Reverb(b, 0.25f, 0.8f, 0.4f);
            AudioFx.FadeOut(b, 0.4f);
            return b;
        }

        // ================================================================== BOSSES
        static float[] BossRoar(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(2.0f);
            float hz = v == 0 ? 82f : 65f;
            SawVowel(b, 0f, hz, 1.15f, 700f, 1150f, 0.75f, ref r, hz * 0.8f, 0.9f);                                   // "RRAAAWR"
            A(b, 0f, P(SynthWave.Square, hz * 0.5f, 1.2f, 0.35f).Lp(400f).Vib(12f, 1.2f).Env(0.04f, 0f).Rel(1.05f, 0.12f), ref r); // growl
            A(b, 0f, P(SynthWave.Noise, 0f, 1.2f, 0.4f).Bp(900f, 1.2f, 500f).Trem(23f, 0.6f).Env(0.04f, 0f).Rel(1.0f, 0.15f), ref r);
            A(b, 0.02f, P(SynthWave.Saw, 1800f, 0.6f, 0.1f).Sweep(900f, 0.6f).Lp(3500f).Env(0.02f, 0.2f), ref r);   // servo whine (it's a machine)
            AudioFx.Saturate(b, 2.2f);
            AudioFx.Reverb(b, 0.25f, 0.85f, 0.4f);
            return b;
        }

        static float[] BossHit(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.8f);
            Thud(b, 0f, 95f, 0.9f, 0.09f, ref r);
            A(b, 0f, P(SynthWave.Sine, 620f, 0.7f, 0.5f).Fm(1.41f, 4f, 0.1f).Env(0.0005f, 0.22f), ref r);             // KLANG
            A(b, 0f, P(SynthWave.Sine, 1475f, 0.5f, 0.25f).Fm(2.76f, 2f, 0.06f).Env(0.0005f, 0.12f), ref r);
            float hi = v == 0 ? 1046.5f : 1174.7f;                                                                     // "ding-DING!" confirm
            A(b, 0.07f, P(SynthWave.Square, hi, 0.08f, 0.22f).Lp(5000f).Env(0.002f, 0.05f, 0.4f).Rel(0.06f, 0.02f), ref r);
            A(b, 0.14f, P(SynthWave.Square, hi * 1.5f, 0.2f, 0.22f).Lp(5000f).Env(0.002f, 0.1f, 0.4f).Rel(0.14f, 0.05f), ref r);
            AudioFx.Reverb(b, 0.15f, 0.6f, 0.4f);
            return b;
        }

        static float[] BossStomp(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.0f);
            A(b, 0f, P(SynthWave.Sine, v == 0 ? 48f : 40f, 0.9f, 1f).PitchEnv(16f, 0.03f).Env(0.001f, 0.3f).Sat(1.8f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.3f, 0.7f).Lp(1200f, 0.8f, 200f).Env(0.0008f, 0.08f), ref r);
            SynthGen.Grains(b, 0.04f, 0.5f, 22, 400f, 2500f, 0.008f, 0.03f, 0.3f, true, ref r, 0.25f);               // debris
            A(b, 0f, P(SynthWave.Sine, 820f, 0.5f, 0.18f).Fm(2.4f, 3f, 0.1f).Env(0.0005f, 0.15f), ref r);             // metal foot
            AudioFx.Reverb(b, 0.2f, 0.8f, 0.45f);
            return b;
        }

        static float[] BossCharge(ref AudioRng r)
        {
            var b = Synth.Buffer(1.3f);
            A(b, 0f, P(SynthWave.Saw, 110f, 1.15f, 0.45f).Sweep(880f, 1.1f).LpEnv(400f, 3000f, 0.9f, 2f).Vib(9f, 0.4f).Env(0.08f, 0f).Rel(1.05f, 0.08f), ref r);
            A(b, 0f, P(SynthWave.Square, 220f, 1.15f, 0.2f).Sweep(1760f, 1.1f).Lp(4000f).Trem(14f, 0.7f).Env(0.08f, 0f).Rel(1.05f, 0.08f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 1.15f, 0.25f).Bp(800f, 1.5f, 5000f).Env(0.2f, 0f).Rel(1.05f, 0.08f), ref r);
            A(b, 1.12f, P(SynthWave.Sine, 2093f, 0.15f, 0.4f).Env(0.001f, 0.05f), ref r);                              // "ping!" = here it comes
            return b;
        }

        static float[] BossDefeat(ref AudioRng r)
        {
            var b = Synth.Buffer(3.2f);
            for (int k = 0; k < 5; k++)   // chain of comedic pops
            {
                float at = k * 0.16f + r.Range(0f, 0.04f);
                Thud(b, at, r.Range(60f, 110f), 0.7f, 0.12f, ref r);
                A(b, at, P(SynthWave.Noise, 0f, 0.4f, 0.45f).Lp(2500f, 0.8f, 300f).Env(0.001f, 0.12f), ref r);
            }
            float[] wah = { 311.13f, 293.66f, 277.18f, 261.63f };                                                       // "wah wah wah waaah"
            for (int i = 0; i < wah.Length; i++)
            {
                bool last = i == wah.Length - 1;
                var p = P(SynthWave.Saw, wah[i], last ? 1.1f : 0.32f, 0.35f).LpEnv(300f, 1400f, 0.15f, 1.5f).Env(0.02f, 0f).Rel(last ? 1.0f : 0.26f, 0.08f);
                if (last) p = p.Vib(5f, 0.6f);
                A(b, 0.95f + i * 0.38f, p, ref r);
            }
            A(b, 2.9f, P(SynthWave.Sine, 300f, 0.3f, 0.45f).Sweep(900f, 0.08f).Vib(18f, 2f, 0.1f).Env(0.002f, 0.12f), ref r); // sproing
            AudioFx.Reverb(b, 0.2f, 0.75f, 0.4f);
            return b;
        }

        static float[] Oink(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.45f);
            int n = v == 2 ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                float at = k * 0.17f, hz = (v == 1 ? 170f : 140f) * (k == 1 ? 1.15f : 1f);
                A(b, at, P(SynthWave.Pulse, hz, 0.16f, 0.6f).Pwm(0.18f).Sweep(hz * 1.4f, 0.05f).Then(hz * 0.9f, 0.1f).Bp(950f, 2.5f).Env(0.006f, 0.06f, 0.5f).Rel(0.12f, 0.03f).Sat(2.5f), ref r);
                A(b, at, P(SynthWave.Noise, 0f, 0.14f, 0.3f).Bp(1800f, 3f).Trem(60f, 0.9f).Env(0.006f, 0.06f), ref r);   // snort
            }
            return b;
        }

        static float[] Stamp(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.45f);
            Thud(b, 0f, 140f, 0.8f, 0.045f, ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.08f, 0.6f).Bp(1700f, 1.5f).Env(0.0004f, 0.012f), ref r);
            A(b, 0.09f, P(SynthWave.Noise, 0f, 0.06f, 0.25f).Bp(900f, 2f).Env(0.0004f, 0.01f), ref r);                   // "-ka" lift
            if (v == 1) A(b, 0.12f, P(SynthWave.Square, 196f, 0.2f, 0.25f).Lp(1200f).Env(0.003f, 0.07f), ref r);      // DENIED buzz
            return b;
        }

        static float[] DrumSlam(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.9f);
            A(b, 0f, P(SynthWave.Sine, v == 0 ? 55f : 62f, 0.8f, 1f).PitchEnv(12f, 0.03f).Env(0.001f, 0.25f).Sat(1.5f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.4f, 0.5f).Bp(2600f, 0.8f).Env(0.0008f, 0.12f), ref r);                 // snare rattle
            A(b, 0f, P(SynthWave.Noise, 0f, 0.8f, 0.3f).Hp(5000f).Env(0.001f, 0.3f), ref r);                          // cymbal splash
            AudioFx.Reverb(b, 0.22f, 0.8f, 0.4f);
            return b;
        }

        static float[] Shockwave(ref AudioRng r)
        {
            var b = Synth.Buffer(1.3f);
            A(b, 0f, P(SynthWave.Sine, 60f, 1.0f, 1f).PitchEnv(20f, 0.05f).Env(0.001f, 0.35f).Sat(1.6f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 1.1f, 0.55f).Bp(3000f, 1.2f, 250f).Env(0.002f, 0.4f), ref r);             // expanding air
            A(b, 0.02f, P(SynthWave.Sine, 900f, 1f, 0.15f).Sweep(200f, 0.8f).Vib(30f, 1.5f).Env(0.005f, 0.3f), ref r);
            AudioFx.Reverb(b, 0.25f, 0.85f, 0.45f);
            return b;
        }

        static float[] TramBell(ref AudioRng r)
        {
            var b = Synth.Buffer(1.0f);
            for (int k = 0; k < 2; k++)
            {
                float at = k * 0.22f;
                Bell(b, at, 1318.5f, 0.35f, 0.5f, ref r, 2.76f, 1.8f);
                Bell(b, at, 1975.5f, 0.2f, 0.2f, ref r, 1.41f, 1f);
                A(b, at, P(SynthWave.Noise, 0f, 0.02f, 0.3f).Bp(4000f, 3f).Env(0.0003f, 0.004f), ref r);
            }
            return b;
        }

        // ================================================================== HEIST
        static float[] CameraBeep(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.14f);
            A(b, 0f, P(SynthWave.Sine, v == 0 ? 2350f : 2100f, 0.1f, 0.6f).Env(0.002f, 0f).Rel(0.07f, 0.02f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.02f, 0.2f).Bp(5000f, 3f).Env(0.0003f, 0.004f), ref r);                   // servo tick
            return b;
        }

        static float[] CameraSpotted(ref AudioRng r)
        {
            var b = Synth.Buffer(0.85f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.15f, 0.4f).Bp(1500f, 1.5f, 400f).Env(0.002f, 0.05f), ref r);            // record scratch
            A(b, 0f, P(SynthWave.Saw, 600f, 0.12f, 0.3f).Sweep(250f, 0.1f).Lp(2500f).Env(0.002f, 0.05f), ref r);
            A(b, 0.12f, P(SynthWave.Square, 880f, 0.16f, 0.4f).Lp(4500f).Env(0.003f, 0f).Rel(0.13f, 0.02f), ref r);   // "!" two-tone stab
            A(b, 0.3f, P(SynthWave.Square, 1174.7f, 0.3f, 0.4f).Lp(4500f).Env(0.003f, 0f).Rel(0.26f, 0.04f), ref r);
            Brass(b, 0.12f, 233.08f, 0.45f, 0.35f, ref r, 0.05f);
            Brass(b, 0.12f, 349.23f, 0.45f, 0.3f, ref r, 0.05f);
            return b;
        }

        static float[] VaultSpin(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.85f);
            int clicks = 9 + v * 3;
            for (int k = 0; k < clicks; k++)
            {
                float u = (float)k / clicks;
                float at = 0.75f * (1f - (1f - u) * (1f - u));                                                       // ratchet slows down
                A(b, at, P(SynthWave.Noise, 0f, 0.02f, 0.5f).Bp(3800f + 300f * (k & 1), 5f).Env(0.0003f, 0.004f), ref r);
                A(b, at, P(SynthWave.Sine, 2900f, 0.02f, 0.2f).Env(0.0003f, 0.003f), ref r);
            }
            A(b, 0.78f, P(SynthWave.Sine, 980f, 0.06f, 0.4f).Env(0.0004f, 0.01f), ref r);                              // number lands
            return b;
        }

        static float[] VaultOpen(ref AudioRng r)
        {
            var b = Synth.Buffer(3.2f);
            for (int k = 0; k < 3; k++)                                                                                // three bolts
            {
                float at = k * 0.28f;
                Thud(b, at, 90f, 0.7f, 0.07f, ref r);
                A(b, at, P(SynthWave.Sine, 640f + 90f * k, 0.3f, 0.3f).Fm(1.41f, 3f, 0.05f).Env(0.0005f, 0.08f), ref r);
            }
            A(b, 0.95f, P(SynthWave.Noise, 0f, 0.6f, 0.5f).Hp(2500f).Env(0.01f, 0.25f), ref r);                       // pressure hiss
            A(b, 1.0f, P(SynthWave.Saw, 45f, 1.6f, 0.5f).Lp(220f, 1.5f).Vib(4f, 0.3f).Env(0.2f, 0f).Rel(1.3f, 0.2f), ref r); // heavy door swings
            SynthGen.Creak(b, 1.05f, 1.4f, 20f, 60f, 300f, 700f, 12f, 0.35f, ref r);
            float[] aah = { 261.63f, 329.63f, 392f, 523.25f };                                                         // the heavenly "aaah"
            foreach (float f in aah) SawVowel(b, 1.6f, f, 1.2f, 800f, 1200f, 0.12f, ref r, 0f, 0.1f);
            SynthGen.Grains(b, 1.7f, 1.2f, 30, 3000f, 8000f, 0.02f, 0.06f, 0.15f, false, ref r);                     // gold glints
            AudioFx.Reverb(b, 0.3f, 0.85f, 0.4f);
            AudioFx.FadeOut(b, 0.3f);
            return b;
        }

        static float[] CoinShower(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.6f);
            int n = 34 + v * 12;
            for (int k = 0; k < n; k++)
            {
                float at = r.Range(0f, 1.2f) * r.Range(0.4f, 1f), hz = r.Range(2600f, 5200f);
                A(b, at, P(SynthWave.Sine, hz, 0.3f, r.Range(0.15f, 0.35f)).Fm(1.37f, 1.2f, 0.05f).Env(0.0005f, r.Range(0.04f, 0.12f)), ref r);
            }
            A(b, 0f, P(SynthWave.Noise, 0f, 1.3f, 0.2f).Bp(6000f, 1.5f).Trem(30f, 0.7f).Env(0.02f, 0.5f), ref r);     // jingle bed
            return b;
        }

        // ================================================================== SCARY
        static float[] ScareStinger(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(1.5f);
            float root = v == 0 ? 233.08f : 207.65f;
            float[] cluster = { 1f, 1.0595f, 1.4142f, 2.0f, 2.1189f };                                                   // minor 2nds + tritone
            foreach (float m in cluster)
                A(b, 0f, P(SynthWave.Saw, root * m, 1.3f, 0.22f).LpEnv(800f, 5000f, 0.25f, 1.5f).Vib(7f, 0.25f).Env(0.003f, 0.5f, 0.2f).Rel(1f, 0.25f), ref r);
            A(b, 0f, P(SynthWave.Sine, 55f, 0.8f, 0.6f).PitchEnv(10f, 0.04f).Env(0.001f, 0.3f), ref r);              // soft impact
            A(b, 0f, P(SynthWave.Noise, 0f, 0.6f, 0.3f).Bp(2500f, 1f).Env(0.002f, 0.15f), ref r);
            AudioFx.Reverb(b, 0.35f, 0.9f, 0.5f);
            AudioFx.FadeOut(b, 0.3f);
            return b;
        }

        static float[] CardboardCreak(int v, ref AudioRng r)
        {
            float len = 0.45f + 0.1f * v;
            var b = Synth.Buffer(len + 0.15f);
            SynthGen.Creak(b, 0.01f, len, 60f, 25f, 700f, 450f, 5f, 0.8f, ref r);                                        // dull flex
            SynthGen.Grains(b, 0.02f, len, 18, 900f, 2600f, 0.003f, 0.012f, 0.4f, true, ref r);                       // crinkle
            A(b, 0f, P(SynthWave.Noise, 0f, len, 0.15f).Bp(1100f, 1.2f).Env(0.05f, len * 0.5f), ref r);
            return b;
        }

        static float[] VacuumHum(ref AudioRng r)
        {
            // seamless 2.0 s loop: every periodic component completes whole cycles in 2 s (0.5 Hz grid)
            var b = Synth.Buffer(2.0f);
            A(b, 0f, P(SynthWave.Saw, 120f, 2.0f, 0.4f).Lp(700f, 1.2f).Vib(0.5f, 0.06f).Env(0.0003f, 0f), ref r);
            A(b, 0f, P(SynthWave.Square, 240f, 2.0f, 0.12f).Lp(1500f).Env(0.0003f, 0f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 2.0f, 0.45f).Bp(1800f, 0.8f).Trem(4f, 0.15f).Env(0.0003f, 0f), ref r);   // airflow
            A(b, 0f, P(SynthWave.Sine, 2750f, 2.0f, 0.06f).Env(0.0003f, 0f), ref r);                                    // motor whine
            return b;
        }

        static float[] StaplerSnap(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.3f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.04f, 0.5f).Bp(2200f, 3f).Env(0.0004f, 0.008f), ref r);                    // "ka-"
            A(b, 0.05f, P(SynthWave.Sine, 1900f, 0.12f, 0.45f).Fm(1.41f, 3f, 0.02f).Env(0.0003f, 0.025f), ref r);       // "-CHUNK" spring
            A(b, 0.05f, P(SynthWave.Noise, 0f, 0.05f, 0.7f).Bp(4500f, 2f).Env(0.0003f, 0.006f), ref r);
            Thud(b, 0.05f, 180f, 0.4f, 0.025f, ref r);
            if (v == 1) A(b, 0.14f, P(SynthWave.Sine, 3000f, 0.1f, 0.15f).Vib(40f, 1f).Env(0.002f, 0.03f), ref r);       // boing of the spring
            return b;
        }

        static float[] Heartbeat(ref AudioRng r)
        {
            // one 70 BPM "lub-dub" cycle (0.857 s) so it loops seamlessly with Sfx.Loop
            var b = Synth.Buffer(0.857f);
            A(b, 0f, P(SynthWave.Sine, 52f, 0.3f, 1f).PitchEnv(6f, 0.02f).Env(0.004f, 0.07f).Lp(300f), ref r);
            A(b, 0.2f, P(SynthWave.Sine, 46f, 0.3f, 0.75f).PitchEnv(5f, 0.02f).Env(0.004f, 0.06f).Lp(300f), ref r);
            return b;
        }

        // ================================================================== WEAPONS / TOOLS
        static float[] LeafBlower(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.7f);
            A(b, 0f, P(SynthWave.Saw, v == 0 ? 95f : 105f, 0.6f, 0.45f).Lp(900f, 1.4f).Trem(38f, 0.6f).Env(0.02f, 0f).Rel(0.5f, 0.1f).Sat(2.5f), ref r); // 2-stroke
            A(b, 0f, P(SynthWave.Noise, 0f, 0.65f, 0.7f).Bp(900f, 0.7f, 2500f).Env(0.03f, 0f).Rel(0.5f, 0.12f), ref r);                                   // FWOOSH
            A(b, 0.4f, P(SynthWave.Noise, 0f, 0.25f, 0.25f).Bp(1800f, 3f, 800f).Env(0.01f, 0.08f), ref r);                                                // leaves rustle out
            return b;
        }

        static float[] TShirtPop(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.55f);
            Thud(b, 0f, v == 0 ? 120f : 140f, 0.8f, 0.05f, ref r);                                                      // FWUMP
            A(b, 0f, P(SynthWave.Noise, 0f, 0.25f, 0.6f).Bp(700f, 1.2f, 2000f).Env(0.001f, 0.06f), ref r);
            A(b, 0.08f, P(SynthWave.Noise, 0f, 0.35f, 0.3f).Bp(2500f, 2f).Trem(26f, 0.9f).Env(0.01f, 0.12f), ref r);  // shirt flapping
            A(b, 0f, P(SynthWave.Sine, 400f, 0.15f, 0.2f).Sweep(900f, 0.1f).Env(0.002f, 0.05f), ref r);
            return b;
        }

        static float[] CameraFlash(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.7f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.03f, 0.6f).Bp(3500f, 3f).Env(0.0003f, 0.005f), ref r);                    // shutter "chk-"
            A(b, 0.04f, P(SynthWave.Noise, 0f, 0.03f, 0.5f).Bp(2600f, 3f).Env(0.0003f, 0.005f), ref r);                // "-chk"
            A(b, 0.02f, P(SynthWave.Sine, 3800f, 0.15f, 0.25f).Env(0.0005f, 0.05f), ref r);                             // FLASH
            A(b, 0.1f, P(SynthWave.Sine, 1200f, 0.55f, 0.3f).Sweep(v == 0 ? 6000f : 5200f, 0.5f).Env(0.05f, 0f).Rel(0.45f, 0.05f), ref r); // recharge whine
            return b;
        }

        static float[] PaintSplat(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.45f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.25f, 0.8f).Lp(2200f, 1f, 500f).Env(0.0008f, 0.06f), ref r);              // SPLAT
            A(b, 0f, P(SynthWave.Sine, 220f + 30f * v, 0.15f, 0.5f).PitchEnv(-7f, 0.03f).Env(0.001f, 0.04f), ref r);
            SynthGen.Grains(b, 0.03f, 0.25f, 10 + v * 3, 600f, 2200f, 0.006f, 0.02f, 0.35f, false, ref r, 0.1f);       // drips
            return b;
        }

        static float[] BitShift(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.5f);
            float[] notes = v == 0 ? new[] { 523.25f, 1046.5f, 783.99f, 1568f, 2093f } : new[] { 2093f, 1568f, 1046.5f, 783.99f, 523.25f };
            for (int i = 0; i < notes.Length; i++)
                A(b, i * 0.045f, P(SynthWave.Square, notes[i], 0.06f, 0.4f).Env(0.001f, 0f).Rel(0.04f, 0.01f).Bits(6f), ref r);
            A(b, 0.22f, P(SynthWave.Noise, 0f, 0.2f, 0.3f).Bp(3000f, 1f, 600f).Env(0.002f, 0.08f).Bits(5f), ref r);
            AudioFx.Crush(b, 3, 16f);
            return b;
        }

        static float[] SwapZap(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.7f);
            float a0 = v == 0 ? 300f : 360f;
            A(b, 0f, P(SynthWave.Saw, a0, 0.25f, 0.4f).Sweep(a0 * 5f, 0.22f).Lp(5000f).Vib(25f, 1f).Env(0.003f, 0f).Rel(0.22f, 0.03f), ref r);       // vwOOP
            A(b, 0.25f, P(SynthWave.Saw, a0 * 5f, 0.3f, 0.4f).Sweep(a0, 0.25f).Lp(5000f).Vib(25f, 1f).Env(0.003f, 0f).Rel(0.25f, 0.04f), ref r);     // -vwip
            A(b, 0.22f, P(SynthWave.Sine, 2637f, 0.3f, 0.3f).Env(0.001f, 0.1f), ref r);                                                               // the swap
            SynthGen.Grains(b, 0.2f, 0.4f, 16, 3000f, 8000f, 0.008f, 0.02f, 0.2f, false, ref r, 0.2f);
            return b;
        }

        static float[] Popper(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.65f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.06f, 0.9f).Bp(2200f, 1f).Env(0.0003f, 0.012f), ref r);                    // POP
            Thud(b, 0f, 200f, 0.5f, 0.03f, ref r);
            SynthGen.Grains(b, 0.04f, 0.5f, 35, 3000f, 9000f, 0.003f, 0.01f, 0.3f, true, ref r, 0.25f);                // confetti
            KazooNote(b, 0.15f, v == 0 ? 698.46f : 783.99f, 0.14f, 0.35f, ref r);                                       // tiny "toot!"
            return b;
        }

        static float[] CapsLockSfx(int v, ref AudioRng r)
        {
            var b = Synth.Buffer(0.55f);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.05f, 0.8f).Bp(3200f, 2f).Env(0.0003f, 0.01f), ref r);                    // big key CLACK
            Thud(b, 0f, 160f, 0.6f, 0.03f, ref r);
            float f = v == 0 ? 116.54f : 130.81f;                                                                       // SHOUTY BRASS "BWAMP"
            Brass(b, 0.03f, f, 0.22f, 0.8f, ref r, 0.05f);
            Brass(b, 0.03f, f * 1.5f, 0.22f, 0.6f, ref r, 0.05f);
            Brass(b, 0.03f, f * 2f, 0.22f, 0.45f, ref r, 0.05f);
            AudioFx.Saturate(b, 1.8f);
            return b;
        }
    }
}
