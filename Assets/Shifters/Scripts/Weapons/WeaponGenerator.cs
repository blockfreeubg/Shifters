// =====================================================================================
//  SHIFTERS - WeaponGenerator.cs   [CONTRACT API - implementation owned by the WEAPONS agent]
//  Random weapon kind + stats within balanced ranges (see DESIGN). Deterministic per rng.
//  Campaign W4: the kind is uniform over PoolKinds (the shipped random kinds: 6 classic + LEAF BLOWER,
//  T-SHIRT CANNON, SAY CHEESE, PAINT BLASTER). Stream-length rule: exactly two draws (kind index, then
//  one rng.Next(2) as before) precede the stat rolls, so a seed keeps its team; only the gun may change.
//  Tools (BIT-SHIFTER, SWAP-O-MATIC, PARTY POPPER, CAPS-LOCK) are never rolled: Tool(variant).
//  Range is ALWAYS >= 20 m (puzzle targets are placed within 16 m).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class WeaponGenerator
    {
        /// <summary>Every generated weapon reaches at least this far (puzzle targets are within 16 m).</summary>
        public const float MinRange = 20f;

        // Names: base noun per kind + optional funny prefix/suffix. Index = (int)WeaponKind.
        static readonly string[][] Bases =
        {
            new[] { "Pew-Pew", "Blaster", "Zapper", "Peashooter", "Popgun", "Hand Cannon", "Snap Pistol" },
            new[] { "Boomstick", "Scattergun", "Blunderbuss", "Spray-n-Pray", "Thunder Tube", "Kablammer" },
            new[] { "Lazor Pointer", "Photon Tickler", "Beam Machine", "Ray Gun", "Disco Zapper", "Pew Beam" },
            new[] { "Pebble Flinger", "Rock Yeeter", "Pocket Catapult", "Gravel Gun", "Slingshot", "Stone Tosser" },
            new[] { "Rubber Chicken Launcher", "Cluck Cannon", "Poultry Cannon", "Bawk-zooka", "Squeak Thrower" },
            new[] { "Gravity Plunger", "Suction Cannon", "Plunge-o-Matic", "Yank Plunger", "Grabby Plunger" },
            new[] { "Leaf Blower", "Hot Air 2000 (CEO Edition)", "Gust Buster", "Breeze Cannon", "Wind Bag", "Blower" },
            new[] { "T-Shirt Cannon", "Free Merch Launcher", "Swag Cannon", "Tee Bazooka", "Merch Mortar" },
            new[] { "Say Cheese", "Flash Camera", "Selfie Cannon", "Paparazzi Pal", "Photo Op" },
            new[] { "Paint Blaster", "The Brush-Off", "Primary Concern", "Splat Gun", "Color Cannon" },
            new[] { "BIT-SHIFTER << >>" },
            new[] { "SWAP-O-MATIC 3000" },
            new[] { "PARTY POPPER" },
            new[] { "CAPS-LOCK CANNON" },
        };

        /// <summary>The shipped random kinds (W4): Generate(rng) draws uniformly from this list. Never contains tools.</summary>
        public static readonly WeaponKind[] PoolKinds =
        {
            WeaponKind.Pistol, WeaponKind.Shotgun, WeaponKind.Laser, WeaponKind.Slingshot, WeaponKind.Chicken, WeaponKind.Plunger,
            WeaponKind.LeafBlower, WeaponKind.TShirtCannon, WeaponKind.SayCheese, WeaponKind.PaintBlaster,
        };

        /// <summary>Tests / QA: when set, Generate(rng) returns this kind (same rng draws, so the team is unchanged).
        /// Also read once from the command line: <c>-forceWeapon LeafBlower</c> (editor / standalone only).</summary>
        public static WeaponKind? ForcedPoolKind
        {
            get { if (!_forcedRead) ReadForcedArg(); return _forced; }
            set { _forced = value; _forcedRead = true; }
        }
        static WeaponKind? _forced;
        static bool _forcedRead;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _forced = null; _forcedRead = false; }

        static void ReadForcedArg()
        {
            _forcedRead = true;
#if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == "-forceWeapon" && System.Enum.TryParse(args[i + 1], true, out WeaponKind k)) { _forced = k; Debug.Log("[WeaponGenerator] forced pool kind: " + k); }
            }
            catch (System.Exception) { }
#endif
        }

        /// <summary>True if the kind can be rolled by Generate(rng).</summary>
        public static bool IsPoolKind(WeaponKind k) { for (int i = 0; i < PoolKinds.Length; i++) if (PoolKinds[i] == k) return true; return false; }

        static readonly string[] Suffixes =
        {
            "3000", "2000", "9000", "Deluxe", "Supreme", "Mk II", "XL", "Jr.", "Pro Max", "Classic",
            "of Mild Inconvenience", "of Questionable Legality", "of Unpaid Overtime",
        };

        static readonly string[] Prefixes =
        {
            "Slightly Used", "Mostly Legal", "Discount", "Bootleg", "Gold-Plated", "Haunted", "Budget", "Totally Safe", "Refurbished",
        };

        const int MaxNameLength = 34;

        /// <summary>Random weapon: kind uniform over PoolKinds, stats inside the per-kind ranges.</summary>
        public static WeaponData Generate(System.Random rng)
        {
            if (rng == null) rng = new System.Random();
            int index = rng.Next(PoolKinds.Length);
            rng.Next(2); // always consumed (the old variant draw) so the stream length before the stats is stable
            var kind = PoolKinds[index];
            var forced = ForcedPoolKind;
            if (forced.HasValue) kind = forced.Value;
            return Generate(kind, rng);
        }

        /// <summary>Fixed-stat TOOL (or a deterministic mid-range pool gun for pool variants). Never random.</summary>
        public static WeaponData Tool(ExperimentalVariant tool)
        {
            var kind = WeaponStyle.KindOf(tool);
            if (!WeaponStyle.IsTool(kind)) return Generate(kind, new System.Random(4100 + (int)kind));
            var d = new WeaponData { Type = WeaponType.Experimental, Variant = tool, Pellets = 1, Damage = 0f, Accuracy = 1f, Knockback = 0f };
            switch (kind)
            {
                case WeaponKind.BitShifter: d.FireRate = 1.5f; d.Range = 25f; break;
                case WeaponKind.SwapOMatic: d.FireRate = 1f / 1.5f; d.Range = 25f; break;
                case WeaponKind.PartyPopper: d.FireRate = 0.5f; d.Range = 20f; d.Knockback = 4f; break;
                default: d.FireRate = 0.5f; d.Range = 25f; break; // CapsLock: 2 s cooldown
            }
            d.Range = Mathf.Max(MinRange, d.Range);
            d.Tint = WeaponStyle.Tint(kind);
            d.Name = Bases[(int)kind][0];
            return d;
        }

        /// <summary>Random weapon of a specific kind (levels, tests, debug).</summary>
        public static WeaponData Generate(WeaponKind kind, System.Random rng)
        {
            if (WeaponStyle.IsTool(kind)) return Tool(WeaponStyle.VariantOf(kind));
            if (rng == null) rng = new System.Random();
            var d = new WeaponData { Type = WeaponStyle.TypeOf(kind), Variant = WeaponStyle.VariantOf(kind), Pellets = 1 };
            switch (kind)
            {
                case WeaponKind.Pistol:
                    d.Damage = R1(rng, 15f, 25f); d.FireRate = R2(rng, 2.5f, 4f); d.Accuracy = R2(rng, 0.75f, 0.95f);
                    d.Range = R0(rng, 35f, 50f); d.Knockback = R1(rng, 6f, 12f);
                    break;
                case WeaponKind.Shotgun:
                    d.Damage = R1(rng, 8f, 12f); d.Pellets = rng.Next(6, 9); d.FireRate = R2(rng, 0.8f, 1.3f);
                    d.Accuracy = R2(rng, 0.4f, 0.6f); d.Range = R0(rng, 20f, 28f); d.Knockback = R1(rng, 20f, 35f); // total over all pellets
                    break;
                case WeaponKind.Laser:
                    d.Damage = R1(rng, 8f, 14f); d.FireRate = R2(rng, 6f, 10f); d.Accuracy = R2(rng, 0.95f, 1f);
                    d.Range = R0(rng, 45f, 60f); d.Knockback = R1(rng, 2f, 5f);
                    break;
                case WeaponKind.Slingshot:
                    d.Damage = R1(rng, 20f, 35f); d.FireRate = R2(rng, 1f, 1.6f); d.Accuracy = R2(rng, 0.8f, 0.95f);
                    d.Range = R0(rng, 25f, 40f); d.Knockback = R1(rng, 15f, 25f);
                    break;
                case WeaponKind.Chicken:
                    d.Damage = R1(rng, 10f, 15f); d.FireRate = R2(rng, 1.2f, 2f); d.Accuracy = R2(rng, 0.7f, 0.85f);
                    d.Range = R0(rng, 25f, 35f); d.Knockback = R1(rng, 18f, 30f);
                    break;
                case WeaponKind.LeafBlower: // continuous cone; FireRate = ticks/s; Knockback = push speed (m/s)
                    d.Damage = 0f; d.FireRate = 4f; d.Accuracy = 1f;
                    d.Range = R0(rng, 20f, 24f); d.Knockback = R1(rng, 4.0f, 6.4f);
                    break;
                case WeaponKind.TShirtCannon:
                    d.Damage = R1(rng, 5f, 10f); d.FireRate = R2(rng, 1.0f, 1.4f); d.Accuracy = R2(rng, 0.8f, 0.9f);
                    d.Range = R0(rng, 25f, 32f); d.Knockback = R1(rng, 18f, 28f);
                    break;
                case WeaponKind.SayCheese:
                    d.Damage = 0f; d.FireRate = R2(rng, 0.6f, 0.8f); d.Accuracy = 1f;
                    d.Range = R0(rng, 20f, 25f); d.Knockback = 0f;
                    break;
                case WeaponKind.PaintBlaster:
                    d.Damage = R1(rng, 10f, 16f); d.FireRate = R2(rng, 1.5f, 2.5f); d.Accuracy = R2(rng, 0.8f, 0.95f);
                    d.Range = R0(rng, 25f, 35f); d.Knockback = R1(rng, 8f, 14f);
                    break;
                default: // Plunger: hitscan PULL. Knockback = pull strength.
                    d.Damage = 0f; d.FireRate = R2(rng, 0.8f, 1.2f); d.Accuracy = R2(rng, 0.9f, 1f);
                    d.Range = R0(rng, 20f, 30f); d.Knockback = R1(rng, 12f, 20f);
                    break;
            }
            d.Range = Mathf.Max(MinRange, d.Range);
            d.Tint = WeaponStyle.Tint(kind);
            d.Name = MakeName(kind, rng);
            return d;
        }

        static string MakeName(WeaponKind kind, System.Random rng)
        {
            var bases = Bases[(int)kind];
            string b = bases[rng.Next(bases.Length)];
            string suffix = rng.NextDouble() < 0.6 ? Suffixes[rng.Next(Suffixes.Length)] : null;
            string prefix = rng.NextDouble() < 0.3 ? Prefixes[rng.Next(Prefixes.Length)] : null;
            string name = Compose(prefix, b, suffix);
            if (name.Length > MaxNameLength) name = Compose(null, b, suffix);
            if (name.Length > MaxNameLength) name = b;
            return name;
        }

        static string Compose(string prefix, string b, string suffix)
        {
            string s = string.IsNullOrEmpty(prefix) ? b : prefix + " " + b;
            return string.IsNullOrEmpty(suffix) ? s : s + " " + suffix;
        }

        static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static float R0(System.Random rng, float min, float max) => Mathf.Clamp(Mathf.Round(Rand(rng, min, max)), min, max);
        static float R1(System.Random rng, float min, float max) => Mathf.Clamp(Mathf.Round(Rand(rng, min, max) * 10f) / 10f, min, max);
        static float R2(System.Random rng, float min, float max) => Mathf.Clamp(Mathf.Round(Rand(rng, min, max) * 100f) / 100f, min, max);
    }
}
