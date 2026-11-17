// =====================================================================================
//  SHIFTERS - WeaponStyle.cs   [owned by the WEAPONS agent]
//  Per-kind presentation & behaviour table (tint, sounds, tracer look, shake, hitscan vs
//  physical projectile). WeaponKind flattens WeaponType + ExperimentalVariant.
//  Campaign kinds (CAMPAIGN 13): pool guns LeafBlower, TShirtCannon, SayCheese, PaintBlaster;
//  tools BitShifter, SwapOMatic, PartyPopper, CapsLock (lockers / armory only, fixed stats).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>The concrete weapon flavours (WeaponType + ExperimentalVariant flattened). Campaign kinds share their
    /// ExperimentalVariant's name (CAMPAIGN 18.4).</summary>
    public enum WeaponKind
    {
        Pistol, Shotgun, Laser, Slingshot, Chicken, Plunger,
        // ---- campaign appends (W0): pool guns, then tools
        LeafBlower, TShirtCannon, SayCheese, PaintBlaster, BitShifter, SwapOMatic, PartyPopper, CapsLock
    }

    public static class WeaponStyle
    {
        public const int KindCount = 14;

        public static WeaponKind KindOf(WeaponType type, ExperimentalVariant variant)
        {
            switch (type)
            {
                case WeaponType.Pistol: return WeaponKind.Pistol;
                case WeaponType.Shotgun: return WeaponKind.Shotgun;
                case WeaponType.LaserGun: return WeaponKind.Laser;
                case WeaponType.Slingshot: return WeaponKind.Slingshot;
            }
            switch (variant)
            {
                case ExperimentalVariant.GravityPlunger: return WeaponKind.Plunger;
                case ExperimentalVariant.LeafBlower: return WeaponKind.LeafBlower;
                case ExperimentalVariant.TShirtCannon: return WeaponKind.TShirtCannon;
                case ExperimentalVariant.SayCheese: return WeaponKind.SayCheese;
                case ExperimentalVariant.PaintBlaster: return WeaponKind.PaintBlaster;
                case ExperimentalVariant.BitShifter: return WeaponKind.BitShifter;
                case ExperimentalVariant.SwapOMatic: return WeaponKind.SwapOMatic;
                case ExperimentalVariant.PartyPopper: return WeaponKind.PartyPopper;
                case ExperimentalVariant.CapsLock: return WeaponKind.CapsLock;
                default: return WeaponKind.Chicken; // RubberChickenLauncher + reserved stretch values
            }
        }

        public static WeaponKind KindOf(WeaponData d) => d == null ? WeaponKind.Pistol : KindOf(d.Type, d.Variant);

        public static WeaponType TypeOf(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Pistol: return WeaponType.Pistol;
                case WeaponKind.Shotgun: return WeaponType.Shotgun;
                case WeaponKind.Laser: return WeaponType.LaserGun;
                case WeaponKind.Slingshot: return WeaponType.Slingshot;
                default: return WeaponType.Experimental;
            }
        }

        public static ExperimentalVariant VariantOf(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Plunger: return ExperimentalVariant.GravityPlunger;
                case WeaponKind.LeafBlower: return ExperimentalVariant.LeafBlower;
                case WeaponKind.TShirtCannon: return ExperimentalVariant.TShirtCannon;
                case WeaponKind.SayCheese: return ExperimentalVariant.SayCheese;
                case WeaponKind.PaintBlaster: return ExperimentalVariant.PaintBlaster;
                case WeaponKind.BitShifter: return ExperimentalVariant.BitShifter;
                case WeaponKind.SwapOMatic: return ExperimentalVariant.SwapOMatic;
                case WeaponKind.PartyPopper: return ExperimentalVariant.PartyPopper;
                case WeaponKind.CapsLock: return ExperimentalVariant.CapsLock;
                default: return ExperimentalVariant.RubberChickenLauncher;
            }
        }

        /// <summary>Kind of an ExperimentalVariant (tools, lockers).</summary>
        public static WeaponKind KindOf(ExperimentalVariant v) => KindOf(WeaponType.Experimental, v);

        /// <summary>Physical pooled projectile (pebble / chicken / T-shirt / paint blob / party flare) instead of a hitscan ray.</summary>
        public static bool IsProjectile(WeaponKind k) => k == WeaponKind.Slingshot || k == WeaponKind.Chicken || k == WeaponKind.TShirtCannon
                                                        || k == WeaponKind.PaintBlaster || k == WeaponKind.PartyPopper;

        /// <summary>Campaign TOOL (lockers / CH13 armory only; never rolled by the generator; fixed stats).</summary>
        public static bool IsTool(WeaponKind k) => k == WeaponKind.BitShifter || k == WeaponKind.SwapOMatic || k == WeaponKind.PartyPopper || k == WeaponKind.CapsLock;
        public static bool IsTool(WeaponData d) => d != null && d.Type == WeaponType.Experimental && IsTool(KindOf(d));

        /// <summary>Continuous fire while LMB is held (LEAF BLOWER): FireRate = ticks per second.</summary>
        public static bool IsContinuous(WeaponKind k) => k == WeaponKind.LeafBlower;

        /// <summary>Kinds handled by WeaponTools (everything appended in the campaign wave).</summary>
        public static bool IsCampaignKind(WeaponKind k) => (int)k >= (int)WeaponKind.LeafBlower;

        /// <summary>NoToolZone flag of a kind (None for guns that ignore the zones).</summary>
        public static ToolMask ToolMaskOf(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.SwapOMatic: return ToolMask.SwapOMatic;
                case WeaponKind.BitShifter: return ToolMask.BitShifter;
                case WeaponKind.CapsLock: return ToolMask.CapsLock;
                case WeaponKind.PartyPopper: return ToolMask.PartyPopper;
                default: return ToolMask.None;
            }
        }

        public static Color Tint(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Pistol: return UIColors.Hex(0xFF8A1F);
                case WeaponKind.Shotgun: return UIColors.Hex(0xE8394B);
                case WeaponKind.Laser: return UIColors.Hex(0x3DE0FF);
                case WeaponKind.Slingshot: return UIColors.Hex(0x7BD389);
                case WeaponKind.Chicken: return UIColors.Hex(0xFFD23C);
                case WeaponKind.LeafBlower: return UIColors.Hex(0x9BE15D);
                case WeaponKind.TShirtCannon: return UIColors.Hex(0xFF4D9A);
                case WeaponKind.SayCheese: return UIColors.Hex(0xF2F2F2);
                case WeaponKind.PaintBlaster: return UIColors.Hex(0x2EC4B6);
                case WeaponKind.BitShifter: return UIColors.Hex(0x39FF88);
                case WeaponKind.SwapOMatic: return UIColors.Hex(0x7A5CFF);
                case WeaponKind.PartyPopper: return UIColors.Hex(0xFF7AC8);
                case WeaponKind.CapsLock: return UIColors.Hex(0x4DA3FF);
                default: return UIColors.Hex(0xC05CFF);
            }
        }

        public static SfxId FireSfx(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Pistol: return SfxId.Pistol;
                case WeaponKind.Shotgun: return SfxId.Shotgun;
                case WeaponKind.Laser: return SfxId.Laser;
                case WeaponKind.Slingshot: return SfxId.Slingshot;
                case WeaponKind.Chicken: return SfxId.Chicken;
                case WeaponKind.LeafBlower: return SfxId.LeafBlower;
                case WeaponKind.TShirtCannon: return SfxId.TShirtPop;
                case WeaponKind.SayCheese: return SfxId.CameraFlash;
                case WeaponKind.PaintBlaster: return SfxId.PaintSplat;
                case WeaponKind.BitShifter: return SfxId.BitShift;
                case WeaponKind.SwapOMatic: return SfxId.SwapZap;
                case WeaponKind.PartyPopper: return SfxId.Popper;
                case WeaponKind.CapsLock: return SfxId.CapsLock;
                default: return SfxId.Plunger;
            }
        }

        /// <summary>Short UI label ("SHOTGUN") for badges.</summary>
        public static string Label(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Pistol: return "PISTOL";
                case WeaponKind.Shotgun: return "SHOTGUN";
                case WeaponKind.Laser: return "LASER";
                case WeaponKind.Slingshot: return "SLINGSHOT";
                case WeaponKind.Chicken: return "CHICKEN LAUNCHER";
                case WeaponKind.LeafBlower: return "LEAF BLOWER";
                case WeaponKind.TShirtCannon: return "T-SHIRT CANNON";
                case WeaponKind.SayCheese: return "SAY CHEESE 3000";
                case WeaponKind.PaintBlaster: return "PAINT BLASTER";
                case WeaponKind.BitShifter: return "BIT-SHIFTER";
                case WeaponKind.SwapOMatic: return "SWAP-O-MATIC";
                case WeaponKind.PartyPopper: return "PARTY POPPER";
                case WeaponKind.CapsLock: return "CAPS-LOCK CANNON";
                default: return "GRAVITY PLUNGER";
            }
        }

        /// <summary>One-line "what it does" for HUD / locker prompts / armory placards.</summary>
        public static string Blurb(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.LeafBlower: return "Hold to blow light props and bots around";
                case WeaponKind.TShirtCannon: return "Free merch! Wraps a bot's head for 4 s";
                case WeaponKind.SayCheese: return "Flash: stuns bots, blinds cameras";
                case WeaponKind.PaintBlaster: return "Paints the grey world";
                case WeaponKind.BitShifter: return "NORMAL > HUGE > TINY. Not interns.";
                case WeaponKind.SwapOMatic: return "Swap places with props, buddies, guards";
                case WeaponKind.PartyPopper: return "Confetti flare: light + noise lure";
                case WeaponKind.CapsLock: return "LOCKS a target for 6 s";
                default: return "";
            }
        }

        public static Color TracerColor(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Laser: return UIColors.Hex(0x7FF4FF);
                case WeaponKind.Plunger: return UIColors.Hex(0xD9A066);
                case WeaponKind.Shotgun: return UIColors.Hex(0xFFD27A);
                case WeaponKind.SayCheese: return UIColors.Hex(0xFFFFFF);
                case WeaponKind.BitShifter: return UIColors.Hex(0x39FF88);
                case WeaponKind.SwapOMatic: return UIColors.Hex(0xA98BFF);
                case WeaponKind.CapsLock: return UIColors.Hex(0x7FC1FF);
                case WeaponKind.LeafBlower: return UIColors.Hex(0xD8F7C0);
                default: return UIColors.Hex(0xFFE08A);
            }
        }

        public static float TracerWidth(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Laser: return 0.09f;
                case WeaponKind.Plunger: return 0.05f;
                case WeaponKind.Shotgun: return 0.035f;
                case WeaponKind.BitShifter: return 0.08f;
                case WeaponKind.SwapOMatic: return 0.11f;
                case WeaponKind.CapsLock: return 0.1f;
                case WeaponKind.SayCheese: return 0.03f;
                default: return 0.05f;
            }
        }

        public static float TracerDuration(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Laser: return 0.06f;
                case WeaponKind.Plunger: return 0.22f;
                case WeaponKind.BitShifter: case WeaponKind.CapsLock: return 0.18f;
                case WeaponKind.SwapOMatic: return 0.2f;
                default: return 0.08f;
            }
        }

        public static float ShakeAmplitude(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Shotgun: return 0.35f;
                case WeaponKind.Laser: return 0.04f;
                case WeaponKind.Plunger: return 0.18f;
                case WeaponKind.LeafBlower: return 0.03f;
                case WeaponKind.TShirtCannon: return 0.2f;
                case WeaponKind.SwapOMatic: return 0.25f;
                case WeaponKind.PartyPopper: return 0.15f;
                default: return 0.12f;
            }
        }

        /// <summary>Noise radius of a trigger pull (bots hear it). SWAP / PARTY POPPER raise their own noise at the target.</summary>
        public static float NoiseRadius(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.LeafBlower: return 8f;
                case WeaponKind.TShirtCannon: return 10f;
                case WeaponKind.SayCheese: return 6f;
                case WeaponKind.BitShifter: case WeaponKind.CapsLock: return 6f;
                case WeaponKind.SwapOMatic: return 0f;
                case WeaponKind.PartyPopper: return 4f;
                default: return Tuning.NoiseGunshot; // includes PAINT BLASTER ("noise like a gunshot")
            }
        }

        public static float ShakeDuration(WeaponKind k) => k == WeaponKind.Shotgun ? 0.22f : k == WeaponKind.LeafBlower ? 0.25f : 0.1f;
        public static float Recoil(WeaponKind k) => k == WeaponKind.Shotgun ? 0.16f : k == WeaponKind.Laser ? 0.02f : k == WeaponKind.LeafBlower ? 0.03f
                                                    : k == WeaponKind.TShirtCannon ? 0.14f : 0.08f;
    }
}
