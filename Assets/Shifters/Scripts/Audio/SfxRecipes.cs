// =====================================================================================
//  SHIFTERS - SfxRecipes.cs   [owned by the AUDIO agent]
//  Procedural generator for EVERY SfxId (comedy first: boings, slide whistles, squeaks,
//  honks). Each recipe layers Patches (see SynthPatch.cs) and returns a mono float[] that is
//  DC-blocked and normalized to -1 dBFS. Deterministic per (id, variant). Thread-safe.
//  Plan(id, v) records the recipe as a SynthPlan that can be executed in small slices (no
//  threads on WebGL); Render(id, v) is the same plan executed at once (identical samples).
//  Recipes are split by category: SfxRecipes.Character / .Interface / .Puzzle / .Combat.
// =====================================================================================
namespace Shifters
{
    /// <summary>Extra stingers that have no SfxId (played by AudioManager itself).</summary>
    public enum SfxSpecial { SadTrombone }

    public static partial class SfxRecipes
    {
        static readonly System.Action<float[], float, float> RemoveMeanOp = (b, _, __) => RemoveMean(b);

        /// <summary>Renders one variation of a sound effect (mono, 44.1 kHz, peak -1 dBFS).</summary>
        public static float[] Render(SfxId id, int variant) => Plan(id, variant).RunToEnd();

        public static float[] RenderSpecial(SfxSpecial s) => PlanSpecial(s).RunToEnd();

        /// <summary>Records one variation as a deferred plan (cheap: recipe logic only). Execute it with
        /// plan.Step(frames) - e.g. via SynthPlanTask - or plan.RunToEnd().</summary>
        public static SynthPlan Plan(SfxId id, int variant)
        {
            var plan = SynthPlan.Begin();
            try
            {
                var r = new AudioRng(AudioRng.Hash((int)id + 1, variant, 7919));
                float[] b = Dispatch(id, variant, ref r) ?? Fallback(ref r);
                bool loop = SfxTable.Get(id).Loopable;
                if (loop) RemoveMean(b);          // keeps the seam intact
                else AudioFx.DcBlock(b);
                AudioFx.Normalize(b);
                if (!loop) AudioFx.FadeOut(b, 0.006f);   // never end on a click
                plan.Result = b;
            }
            finally { plan.End(); }
            return plan;
        }

        public static SynthPlan PlanSpecial(SfxSpecial s)
        {
            var plan = SynthPlan.Begin();
            try
            {
                var r = new AudioRng(AudioRng.Hash(1000 + (int)s, 0, 31));
                float[] b = s == SfxSpecial.SadTrombone ? SadTrombone(ref r) : Fallback(ref r);
                AudioFx.DcBlock(b);
                AudioFx.Normalize(b);
                AudioFx.FadeOut(b, 0.006f);
                plan.Result = b;
            }
            finally { plan.End(); }
            return plan;
        }

        static void RemoveMean(float[] b)
        {
            if (SynthPlan.Recording != null) { SynthPlan.Recording.Defer(b, RemoveMeanOp); return; }
            double m = 0;
            for (int i = 0; i < b.Length; i++) m += b[i];
            float k = (float)(m / System.Math.Max(1, b.Length));
            for (int i = 0; i < b.Length; i++) b[i] -= k;
        }

        static float[] Dispatch(SfxId id, int v, ref AudioRng r)
        {
            switch (id)
            {
                // characters
                case SfxId.Footstep: return Footstep(v, ref r, false);
                case SfxId.FootstepHeavy: return Footstep(v, ref r, true);
                case SfxId.Jump: return Jump(v, ref r);
                case SfxId.Land: return Land(v, ref r, false);
                case SfxId.LandHeavy: return Land(v, ref r, true);
                case SfxId.Tumble: return Tumble(v, ref r);
                case SfxId.Grab: return Grab(v, ref r);
                case SfxId.Throw: return Whoosh(v, ref r, 0.36f, 500f, 2600f, 900f);
                case SfxId.Whoosh: return Whoosh(v, ref r, 0.5f, 300f, 1800f, 600f);
                case SfxId.Flop: return Flop(v, ref r);
                case SfxId.GetUp: return GetUp(v, ref r);
                case SfxId.KO: return KO(v, ref r);
                case SfxId.Lost: return Lost(ref r);
                case SfxId.Revive: return Revive(ref r);
                case SfxId.Captured: return Captured(ref r);
                case SfxId.Hurt: return Hurt(v, ref r);
                case SfxId.BridgeForm: return BridgeForm(ref r);
                case SfxId.BridgeCreak: return BridgeCreak(v, ref r);
                case SfxId.BridgeCollapse: return BridgeCollapse(ref r);
                case SfxId.BridgeRecover: return BridgeRecover(ref r);
                case SfxId.Bonk: return Bonk(v, ref r);
                // switching / UI
                case SfxId.SwitcherOpen: return SwitcherOpen(ref r);
                case SfxId.SwitcherClose: return SwitcherClose(ref r);
                case SfxId.SwitcherTick: return Tick(ref r, 2200f, 0.7f);
                case SfxId.SwitchWhoosh: return SwitchWhoosh(ref r);
                case SfxId.SwitchLand: return SwitchLand(ref r);
                case SfxId.ScanPulse: return ScanPulse(ref r);
                case SfxId.Denied: return Denied(ref r);
                case SfxId.UIHover: return Tick(ref r, v == 0 ? 1900f : 2100f, 0.6f);
                case SfxId.UIClick: return UIClick(ref r);
                case SfxId.UIBack: return UIBack(ref r);
                case SfxId.UIError: return UIError(ref r);
                case SfxId.UIPop: return UIPop(ref r);
                case SfxId.Banner: return Banner(ref r);
                case SfxId.TutorialPop: return TutorialPop(ref r);
                // puzzles
                case SfxId.PlatePress: return PlatePress(ref r);
                case SfxId.PlateRelease: return PlateRelease(ref r);
                case SfxId.Lever: return Lever(ref r);
                case SfxId.ButtonPress: return ButtonPress(ref r);
                case SfxId.TimerTick: return Tick(ref r, v == 0 ? 2000f : 1500f, 0.8f);
                case SfxId.TimerEnd: return TimerEnd(ref r);
                case SfxId.DoorOpen: return DoorOpen(ref r);
                case SfxId.DoorClose: return DoorClose(ref r);
                case SfxId.DoorSlam: return DoorSlam(ref r);
                case SfxId.Terminal: return Terminal(v, ref r);
                case SfxId.HackSuccess: return HackSuccess(ref r);
                case SfxId.HackFail: return HackFail(ref r);
                case SfxId.PuzzleSolved: return PuzzleSolved(ref r);
                case SfxId.Checkpoint: return Checkpoint(ref r);
                case SfxId.LevelComplete: return LevelComplete(ref r);
                case SfxId.ElevatorHum: return ElevatorHum(ref r);
                case SfxId.Steam: return Steam(v, ref r);
                case SfxId.Crusher: return Crusher(ref r);
                case SfxId.LaserZap: return LaserZap(v, ref r);
                case SfxId.Bounce: return Bounce(v, ref r);
                case SfxId.Conveyor: return Conveyor(ref r);
                case SfxId.Break: return GlassBreak(v, ref r);
                case SfxId.Crumble: return Crumble(v, ref r);
                case SfxId.Wobble: return Wobble(v, ref r);
                // weapons
                case SfxId.Pistol: return Pistol(v, ref r);
                case SfxId.Shotgun: return Shotgun(v, ref r);
                case SfxId.Laser: return LaserGun(v, ref r);
                case SfxId.Slingshot: return Slingshot(v, ref r);
                case SfxId.Chicken: return Chicken(v, ref r);
                case SfxId.Plunger: return Plunger(v, ref r);
                case SfxId.Impact: return Impact(v, ref r);
                case SfxId.MetalHit: return MetalHit(v, ref r);
                case SfxId.Ricochet: return Ricochet(v, ref r);
                case SfxId.Pickup: return Pickup(ref r);
                case SfxId.Empty: return EmptyClick(ref r);
                // enemies
                case SfxId.EnemyBeep: return EnemyBeep(v, ref r);
                case SfxId.EnemyAlert: return EnemyAlert(ref r);
                case SfxId.EnemyStun: return EnemyStun(ref r);
                case SfxId.Siren: return Siren(ref r);
                case SfxId.EnemyGrab: return EnemyGrab(ref r);
                // misc
                case SfxId.Explosion: return Explosion(v, ref r);
                case SfxId.Squeak: return Squeak(v, ref r);
                case SfxId.Splash: return Splash(v, ref r);
                case SfxId.Sizzle: return Sizzle(v, ref r);
                case SfxId.Alarm: return Alarm(ref r);
                default: return DispatchCampaign(id, v, ref r);   // SfxRecipes.Campaign.cs
            }
        }

        // ------------------------------------------------------------------ shared shortcuts
        static SynthPatch P(SynthWave w, float hz, float dur, float gain = 1f) => SynthPatch.Make(w, hz, dur, gain);
        static void A(float[] b, float at, in SynthPatch p, ref AudioRng r) => SynthGen.At(b, at, p, ref r);

        /// <summary>Generic soft blip for unknown/new ids so nothing is ever silent or crashes.</summary>
        static float[] Fallback(ref AudioRng r)
        {
            var b = Synth.Buffer(0.15f);
            A(b, 0f, P(SynthWave.Sine, 880f, 0.12f).Sweep(660f).Env(0.002f, 0.05f), ref r);
            return b;
        }

        static float[] Tick(ref AudioRng r, float hz, float gain)
        {
            var b = Synth.Buffer(0.06f);
            A(b, 0f, P(SynthWave.Sine, hz, 0.05f, gain).PitchEnv(3f, 0.003f).Env(0.0005f, 0.01f), ref r);
            A(b, 0f, P(SynthWave.Noise, 0f, 0.01f, 0.25f).Hp(5000f).Env(0.0003f, 0.002f), ref r);
            return b;
        }

        /// <summary>Two-operator FM bell note.</summary>
        static void Bell(float[] b, float at, float hz, float decay, float gain, ref AudioRng r, float ratio = 3.5f, float index = 1.4f)
        {
            A(b, at, P(SynthWave.Sine, hz, decay * 5f, gain).Fm(ratio, index, decay * 0.4f).Env(0.001f, decay), ref r);
        }

        /// <summary>Simple synthetic brass note ("bwaa"): two detuned saws through an opening/closing low-pass.</summary>
        static void Brass(float[] b, float at, float hz, float len, float gain, ref AudioRng r, float vib = 0.12f)
        {
            for (int k = 0; k < 2; k++)
            {
                var p = P(SynthWave.Saw, hz * (k == 0 ? 0.997f : 1.004f), len + 0.45f, gain * 0.5f)
                    .LpEnv(hz * 1.6f, hz * 4f, 0.22f, 1.1f).Env(0.025f, 0.3f, 0.75f).Rel(len, 0.1f).Vib(5.5f, vib);
                A(b, at, p, ref r);
            }
        }
    }
}
