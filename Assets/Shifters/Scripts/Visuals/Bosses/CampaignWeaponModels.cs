// =====================================================================================
//  SHIFTERS - CampaignWeaponModels.cs   [owned by VISUALS_BOSSES]
//  Models of the 8 campaign weapons / tools (WeaponKind.LeafBlower .. CapsLock, CAMPAIGN §13). Called by
//  WeaponModelBuilder.Build (visuals) for WeaponType.Experimental with a campaign ExperimentalVariant.
//  Same convention as WeaponModelBuilder: grip at the origin, barrel along +Z, top = +Y, sizes ~0.4-0.7 m, meshes via
//  ProcMesh.Cached ("wpn_c_*"), shared materials (MaterialLibrary.Colored; gold = MatKey.Gold, grey-immune). Build returns
//  the LOCAL muzzle position: WeaponModelBuilder adds the child named "Muzzle" there (so every model has a Muzzle child).
//  Every model has parts named "Body" (tinted) and "Grip" (dark); the joke of each name is in the silhouette:
//    LeafBlower   "Hot Air 2000 (CEO Edition)"   engine pack + long gold-banded blower tube
//    TShirtCannon "Free Merch Launcher"           fat barrel with a rolled red T-shirt in the muzzle, CO2 tank
//    SayCheese    "SAY CHEESE 3000"               boxy retro camera, big lens, flash dish on a stalk
//    PaintBlaster "PAINT BLASTER"                 paint bucket hopper, splats in the primaries, nozzle
//    BitShifter   "BIT-SHIFTER << >>"            sleek emitter with glowing << / >> chevrons and 0/1 coils
//    SwapOMatic   "SWAP-O-MATIC 3000"             twin prongs with a blue and an orange orb, swap ring
//    PartyPopper  "PARTY POPPER"                  giant striped popper cone, confetti at the mouth, pull string
//    CapsLock     "CAPS-LOCK CANNON"              a giant CAPS LOCK keycap (arrow + lamp) with a gold padlock
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class CampaignWeaponModels
    {
        static readonly Color Dark = new Color(0.16f, 0.17f, 0.2f);
        static readonly Color Light = new Color(0.86f, 0.87f, 0.9f);
        static readonly Quaternion AlongZ = Quaternion.Euler(90f, 0f, 0f);
        static readonly Quaternion AlongX = Quaternion.Euler(0f, 0f, 90f);
        static readonly Color W = Color.white;

        /// <summary>Campaign kind of an ExperimentalVariant (same names). false for RubberChickenLauncher / GravityPlunger
        /// (WeaponModelBuilder builds those) and for the reserved stretch variants.</summary>
        public static bool TryKind(ExperimentalVariant variant, out WeaponKind kind)
        {
            switch (variant)
            {
                case ExperimentalVariant.LeafBlower: kind = WeaponKind.LeafBlower; return true;
                case ExperimentalVariant.TShirtCannon: kind = WeaponKind.TShirtCannon; return true;
                case ExperimentalVariant.SayCheese: kind = WeaponKind.SayCheese; return true;
                case ExperimentalVariant.PaintBlaster: kind = WeaponKind.PaintBlaster; return true;
                case ExperimentalVariant.BitShifter: kind = WeaponKind.BitShifter; return true;
                case ExperimentalVariant.SwapOMatic: kind = WeaponKind.SwapOMatic; return true;
                case ExperimentalVariant.PartyPopper: kind = WeaponKind.PartyPopper; return true;
                case ExperimentalVariant.CapsLock: kind = WeaponKind.CapsLock; return true;
                default: kind = WeaponKind.Chicken; return false;
            }
        }

        /// <summary>True for the campaign kinds (WeaponKind.LeafBlower .. CapsLock).</summary>
        public static bool Handles(WeaponKind kind) => kind >= WeaponKind.LeafBlower && kind <= WeaponKind.CapsLock;

        /// <summary>Builds the model parts of a campaign weapon under root (tint = WeaponData.Tint, alpha ignored) and returns
        /// the local muzzle position (always in front of the grip, z &gt; 0.2).</summary>
        public static Vector3 Build(WeaponKind kind, Transform root, Color tint)
        {
            tint.a = 1f;
            switch (kind)
            {
                case WeaponKind.LeafBlower: return LeafBlower(root, tint);
                case WeaponKind.TShirtCannon: return TShirtCannon(root, tint);
                case WeaponKind.SayCheese: return SayCheese(root, tint);
                case WeaponKind.PaintBlaster: return PaintBlaster(root, tint);
                case WeaponKind.BitShifter: return BitShifter(root, tint);
                case WeaponKind.SwapOMatic: return SwapOMatic(root, tint);
                case WeaponKind.PartyPopper: return PartyPopper(root, tint);
                case WeaponKind.CapsLock: return CapsLock(root, tint);
                default: return Generic(root, tint);
            }
        }

        // ------------------------------------------------------------------ helpers
        static void Part(Transform root, string name, string key, System.Action<ProcMesh.Builder> build, Material mat)
        {
            var mesh = ProcMesh.Cached("wpn_c_" + key, () => { var b = new ProcMesh.Builder(); build(b); return b.ToMesh("wpn_c_" + key); });
            ProcMesh.Part(name, root, mesh, mat, Vector3.zero);
        }
        static Material Mat(Color c, float smooth = 0.45f, float emission = 0f) => MaterialLibrary.Colored(c, smooth, emission);
        static Material Gold => Kit.Mat(MatKey.Gold);
        static Material Glow(Color c, float g = 2f) => MaterialLibrary.Unlit(c, g);

        /// <summary>Standard angled pistol grip + trigger guard at the origin.</summary>
        static void Grip(ProcMesh.Builder b, float z = -0.02f, float y = 0f)
        {
            b.AddBox(new Vector3(0f, y - 0.045f, z), new Vector3(0.065f, 0.15f, 0.075f), Quaternion.Euler(14f, 0f, 0f), W, 0.02f);
            b.AddBox(new Vector3(0f, y + 0.0f, z + 0.065f), new Vector3(0.02f, 0.045f, 0.065f), Quaternion.identity, W, 0.006f);
        }

        static Vector3 Generic(Transform r, Color tint)
        {
            Part(r, "Body", "gen_body", b => b.AddBox(new Vector3(0f, 0.07f, 0.16f), new Vector3(0.12f, 0.13f, 0.42f), Quaternion.identity, W, 0.03f), Mat(tint));
            Part(r, "Grip", "gen_grip", b => Grip(b), Mat(Dark, 0.4f));
            return new Vector3(0f, 0.07f, 0.42f);
        }

        // ------------------------------------------------------------------ HOT AIR 2000 (CEO EDITION) leaf blower
        static Vector3 LeafBlower(Transform r, Color tint)
        {
            Part(r, "Body", "blower_body", b =>
            {
                b.AddBox(new Vector3(0f, 0.11f, -0.07f), new Vector3(0.17f, 0.17f, 0.2f), Quaternion.identity, W, 0.045f);   // engine
                b.AddSphere(new Vector3(0f, 0.2f, -0.1f), 0.075f, Quaternion.identity, W, 12, 8, true, new Vector3(1.1f, 0.7f, 1f)); // tank
                b.AddCylinder(new Vector3(0f, 0.075f, 0.11f), 0.062f, 0.16f, AlongZ, W, 14, true);                          // fan housing
            }, Mat(tint, 0.5f));
            Part(r, "Tube", "blower_tube", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.07f, 0.36f), 0.042f, 0.36f, AlongZ, W, 14, true);
                b.AddCylinder(new Vector3(0f, 0.068f, 0.58f), 0.05f, 0.1f, AlongZ, W, 14, true);                               // flared nozzle
                b.AddCone(new Vector3(0f, 0.068f, 0.53f), 0.056f, 0.06f, Quaternion.Euler(-90f, 0f, 0f), W, 14);
            }, Mat(Light, 0.6f));
            Part(r, "Grip", "blower_dark", b =>
            {
                Grip(b);
                for (int i = 0; i < 4; i++) b.AddBox(new Vector3(0.088f, 0.11f, -0.14f + i * 0.045f), new Vector3(0.015f, 0.14f, 0.02f), Quaternion.identity, W, 0.004f); // fins
                b.AddTorus(new Vector3(-0.1f, 0.12f, -0.07f), 0.03f, 0.008f, AlongX, W, 10, 4);                               // pull start
                b.AddBox(new Vector3(0f, 0.215f, 0.03f), new Vector3(0.03f, 0.03f, 0.16f), Quaternion.identity, W, 0.008f);    // top handle
                b.AddBox(new Vector3(0f, 0.18f, 0.1f), new Vector3(0.03f, 0.08f, 0.03f), Quaternion.identity, W, 0.008f);
            }, Mat(Dark, 0.4f));
            Part(r, "Gold", "blower_gold", b =>
            {
                b.AddTorus(new Vector3(0f, 0.07f, 0.24f), 0.045f, 0.01f, AlongZ, W, 14, 5);
                b.AddTorus(new Vector3(0f, 0.07f, 0.42f), 0.045f, 0.01f, AlongZ, W, 14, 5);
                b.AddTorus(new Vector3(0f, 0.068f, 0.63f), 0.052f, 0.012f, AlongZ, W, 14, 5);
                b.AddBox(new Vector3(0f, 0.11f, -0.172f), new Vector3(0.1f, 0.06f, 0.012f), Quaternion.identity, W, 0.004f);  // CEO plate
            }, Gold);
            return new Vector3(0f, 0.068f, 0.66f);
        }

        // ------------------------------------------------------------------ FREE MERCH LAUNCHER (T-shirt cannon)
        static Vector3 TShirtCannon(Transform r, Color tint)
        {
            Part(r, "Body", "tshirt_body", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.1f, 0.2f), 0.085f, 0.5f, AlongZ, W, 16, true);
                b.AddSphere(new Vector3(0f, 0.1f, -0.05f), 0.085f, Quaternion.identity, W, 14, 8, true, new Vector3(1f, 1f, 0.6f));
            }, Mat(tint, 0.5f));
            Part(r, "Grip", "tshirt_dark", b =>
            {
                Grip(b);
                b.AddTorus(new Vector3(0f, 0.1f, 0.45f), 0.086f, 0.016f, AlongZ, W, 16, 6);                                     // muzzle ring
                b.AddTorus(new Vector3(0f, 0.1f, 0.06f), 0.086f, 0.012f, AlongZ, W, 16, 5);
                b.AddCylinder(new Vector3(0f, -0.005f, 0.2f), 0.035f, 0.26f, AlongZ, W, 10, true);                           // CO2 tank
                b.AddBox(new Vector3(0f, 0.025f, 0.12f), new Vector3(0.025f, 0.05f, 0.03f), Quaternion.identity, W, 0.006f);
                b.AddBox(new Vector3(0f, 0.04f, 0.34f), new Vector3(0.04f, 0.09f, 0.05f), Quaternion.Euler(-12f, 0f, 0f), W, 0.01f); // front grip
            }, Mat(Dark, 0.4f));
            Part(r, "Shirt", "tshirt_shirt", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.1f, 0.425f), 0.068f, 0.08f, AlongZ, W, 14, true);                             // rolled shirt
                b.AddCylinder(new Vector3(0.0f, 0.115f, 0.468f), 0.03f, 0.012f, AlongZ, W, 10, true);
            }, Mat(new Color(0.92f, 0.16f, 0.2f), 0.2f));
            Part(r, "Gauge", "tshirt_gauge", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.2f, 0.06f), 0.03f, 0.02f, Quaternion.identity, W, 12, true);
                b.AddBox(new Vector3(0.07f, 0.13f, 0.22f), new Vector3(0.012f, 0.05f, 0.16f), Quaternion.identity, W, 0.004f); // FREE stripe
                b.AddBox(new Vector3(-0.07f, 0.13f, 0.22f), new Vector3(0.012f, 0.05f, 0.16f), Quaternion.identity, W, 0.004f);
            }, Mat(new Color(0.97f, 0.97f, 0.95f), 0.4f));
            return new Vector3(0f, 0.1f, 0.5f);
        }

        // ------------------------------------------------------------------ SAY CHEESE 3000 (camera flash)
        static Vector3 SayCheese(Transform r, Color tint)
        {
            Part(r, "Body", "cheese_body2", b =>
            {
                b.AddBox(new Vector3(0f, 0.095f, 0.08f), new Vector3(0.3f, 0.17f, 0.16f), Quaternion.identity, W, 0.03f);
                b.AddBox(new Vector3(-0.09f, 0.195f, 0.07f), new Vector3(0.08f, 0.045f, 0.09f), Quaternion.identity, W, 0.012f);  // viewfinder hump
            }, Mat(tint, 0.45f));
            Part(r, "Grip", "cheese_dark2", b =>
            {
                Grip(b, 0.02f);
                b.AddCylinder(new Vector3(0f, 0.095f, 0.21f), 0.072f, 0.1f, AlongZ, W, 16, true);                              // lens barrel
                b.AddCylinder(new Vector3(0f, 0.095f, 0.3f), 0.06f, 0.08f, AlongZ, W, 16, true);                               // zoom ring
                b.AddTorus(new Vector3(0f, 0.095f, 0.34f), 0.06f, 0.012f, AlongZ, W, 16, 5);
                b.AddCylinder(new Vector3(0.1f, 0.25f, 0.06f), 0.012f, 0.1f, Quaternion.identity, W, 6, true);                  // flash stalk
                b.AddBox(new Vector3(0f, 0.095f, -0.005f), new Vector3(0.24f, 0.12f, 0.012f), Quaternion.identity, W, 0.004f);  // back plate
            }, Mat(Dark, 0.4f));
            Part(r, "Lens", "cheese_lens2", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.095f, 0.345f), 0.048f, 0.01f, AlongZ, W, 16, true);
            }, Mat(new Color(0.35f, 0.6f, 0.95f), 0.95f));
            Part(r, "Flash", "cheese_flash", b =>
            {
                b.AddBox(new Vector3(0.1f, 0.33f, 0.075f), new Vector3(0.12f, 0.08f, 0.05f), Quaternion.identity, W, 0.015f);   // flash head
                b.AddCone(new Vector3(0.1f, 0.33f, 0.13f), 0.07f, 0.05f, Quaternion.Euler(-90f, 0f, 0f), W, 14);
            }, Mat(new Color(0.8f, 0.82f, 0.86f), 0.85f));
            Part(r, "Bulb", "cheese_bulb2", b =>
            {
                b.AddSphere(new Vector3(0.1f, 0.33f, 0.105f), 0.03f, Quaternion.identity, W, 10, 6, true, null);
                b.AddCylinder(new Vector3(0.08f, 0.19f, 0.11f), 0.014f, 0.012f, Quaternion.identity, W, 8, true);                // red shutter button
            }, Glow(new Color(1f, 0.97f, 0.8f), 2.5f));
            return new Vector3(0f, 0.095f, 0.36f);
        }

        // ------------------------------------------------------------------ PAINT BLASTER
        static Vector3 PaintBlaster(Transform r, Color tint)
        {
            Part(r, "Body", "paint_body", b =>
            {
                b.AddBox(new Vector3(0f, 0.08f, 0.1f), new Vector3(0.12f, 0.12f, 0.3f), Quaternion.identity, W, 0.04f);
                b.AddCylinder(new Vector3(0f, 0.08f, 0.3f), 0.045f, 0.14f, AlongZ, W, 14, true);
            }, Mat(tint, 0.5f));
            Part(r, "Grip", "paint_dark", b =>
            {
                Grip(b);
                b.AddCone(new Vector3(0f, 0.08f, 0.37f), 0.05f, 0.09f, AlongZ, W, 14);                                          // nozzle
                b.AddBox(new Vector3(0f, 0.15f, 0.06f), new Vector3(0.03f, 0.04f, 0.04f), Quaternion.identity, W, 0.008f);       // feed neck
            }, Mat(Dark, 0.4f));
            Part(r, "Bucket", "paint_bucket", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.225f, 0.05f), 0.072f, 0.11f, Quaternion.identity, W, 16, true);
                b.AddTorus(new Vector3(0f, 0.282f, 0.05f), 0.07f, 0.008f, Quaternion.identity, W, 16, 4);
            }, Mat(new Color(0.93f, 0.93f, 0.92f), 0.35f));
            Part(r, "PaintRed", "paint_red", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.282f, 0.05f), 0.064f, 0.01f, Quaternion.identity, W, 16, true);                // paint surface
                b.AddCapsule(new Vector3(0.071f, 0.25f, 0.05f), 0.012f, 0.06f, Quaternion.identity, W, 6, 2);                   // drip
                b.AddSphere(new Vector3(0.062f, 0.09f, 0.17f), 0.026f, Quaternion.identity, W, 8, 5, true, new Vector3(0.4f, 1f, 1.2f));
            }, Mat(new Color(0.9f, 0.14f, 0.16f), 0.7f));
            Part(r, "PaintYellow", "paint_yellow", b =>
            {
                b.AddSphere(new Vector3(-0.062f, 0.11f, 0.02f), 0.028f, Quaternion.identity, W, 8, 5, true, new Vector3(0.4f, 1.1f, 1f));
                b.AddSphere(new Vector3(0f, 0.045f, 0.42f), 0.018f, Quaternion.identity, W, 8, 5, true, null);                  // blob at the tip
            }, Mat(new Color(1f, 0.85f, 0.15f), 0.7f));
            Part(r, "PaintBlue", "paint_blue", b =>
            {
                b.AddSphere(new Vector3(0.062f, 0.05f, -0.01f), 0.022f, Quaternion.identity, W, 8, 5, true, new Vector3(0.4f, 1f, 1.3f));
                b.AddSphere(new Vector3(-0.062f, 0.06f, 0.22f), 0.02f, Quaternion.identity, W, 8, 5, true, new Vector3(0.4f, 1.2f, 1f));
            }, Mat(new Color(0.15f, 0.42f, 0.95f), 0.7f));
            return new Vector3(0f, 0.08f, 0.47f);
        }

        // ------------------------------------------------------------------ BIT-SHIFTER << >>
        static Vector3 BitShifter(Transform r, Color tint)
        {
            var green = new Color(0.3f, 1f, 0.45f);
            Part(r, "Body", "bit_body", b =>
            {
                b.AddBox(new Vector3(0f, 0.08f, 0.06f), new Vector3(0.11f, 0.12f, 0.26f), Quaternion.identity, W, 0.035f);
                b.AddBox(new Vector3(0f, 0.15f, 0.02f), new Vector3(0.06f, 0.04f, 0.18f), Quaternion.identity, W, 0.015f);
            }, Mat(tint, 0.6f));
            Part(r, "Grip", "bit_dark", b =>
            {
                Grip(b);
                b.AddCylinder(new Vector3(0f, 0.08f, 0.3f), 0.032f, 0.24f, AlongZ, W, 12, true);                                // barrel core
                b.AddCylinder(new Vector3(0f, 0.08f, 0.43f), 0.045f, 0.03f, AlongZ, W, 12, true);                               // emitter
            }, Mat(Dark, 0.5f));
            Part(r, "Bits", "bit_glow", b =>
            {
                // "0" coils and "1" bars alternating along the barrel
                for (int i = 0; i < 4; i++)
                {
                    float z = 0.22f + i * 0.05f;
                    if (i % 2 == 0) b.AddTorus(new Vector3(0f, 0.08f, z), 0.042f, 0.009f, AlongZ, W, 12, 4);
                    else b.AddBox(new Vector3(0f, 0.08f, z), new Vector3(0.014f, 0.1f, 0.014f), Quaternion.identity, W, 0.003f);
                }
                b.AddCylinder(new Vector3(0f, 0.08f, 0.447f), 0.03f, 0.008f, AlongZ, W, 12, true);
                // chevrons: "<<" on the left side, ">>" on the right side (in the YZ plane, pointing -Z / +Z)
                for (int side = -1; side <= 1; side += 2)
                    for (int k = 0; k < 2; k++)
                    {
                        float zc = 0.04f + k * 0.07f;
                        float x = side * 0.058f;
                        float dir = side;   // right side points forward (>>), left side back (<<)
                        b.AddBox(new Vector3(x, 0.1f, zc), new Vector3(0.008f, 0.012f, 0.06f), Quaternion.Euler(-40f * dir, 0f, 0f), W, 0.002f);
                        b.AddBox(new Vector3(x, 0.06f, zc), new Vector3(0.008f, 0.012f, 0.06f), Quaternion.Euler(40f * dir, 0f, 0f), W, 0.002f);
                    }
            }, Glow(green, 2.4f));
            return new Vector3(0f, 0.08f, 0.47f);
        }

        // ------------------------------------------------------------------ SWAP-O-MATIC 3000
        static Vector3 SwapOMatic(Transform r, Color tint)
        {
            Part(r, "Body", "swap_body", b =>
            {
                b.AddBox(new Vector3(0f, 0.08f, 0.05f), new Vector3(0.13f, 0.13f, 0.22f), Quaternion.identity, W, 0.04f);
                b.AddCylinder(new Vector3(0f, 0.08f, 0.19f), 0.075f, 0.06f, AlongZ, W, 16, true);                               // dish
            }, Mat(tint, 0.55f));
            Part(r, "Grip", "swap_dark", b =>
            {
                Grip(b);
                b.AddCylinder(new Vector3(-0.045f, 0.08f, 0.3f), 0.012f, 0.18f, AlongZ, W, 8, true);                            // prongs
                b.AddCylinder(new Vector3(0.045f, 0.08f, 0.3f), 0.012f, 0.18f, AlongZ, W, 8, true);
                b.AddBox(new Vector3(0f, 0.16f, 0.02f), new Vector3(0.05f, 0.03f, 0.1f), Quaternion.identity, W, 0.01f);
            }, Mat(Dark, 0.45f));
            Part(r, "OrbBlue", "swap_blue", b => b.AddSphere(new Vector3(-0.045f, 0.08f, 0.4f), 0.03f, Quaternion.identity, W, 12, 8, true, null), Glow(new Color(0.25f, 0.6f, 1f), 2.6f));
            Part(r, "OrbOrange", "swap_orange", b => b.AddSphere(new Vector3(0.045f, 0.08f, 0.4f), 0.03f, Quaternion.identity, W, 12, 8, true, null), Glow(new Color(1f, 0.55f, 0.12f), 2.6f));
            Part(r, "SwapRing", "swap_ring", b =>
            {
                b.AddTorus(new Vector3(0f, 0.08f, 0.32f), 0.07f, 0.008f, AlongZ, W, 18, 4);
                // arrow heads on the ring (one up-left, one down-right): the "change places" glyph
                b.AddCone(new Vector3(-0.06f, 0.12f, 0.32f), 0.016f, 0.03f, Quaternion.Euler(0f, 0f, 60f), W, 6);
                b.AddCone(new Vector3(0.06f, 0.04f, 0.32f), 0.016f, 0.03f, Quaternion.Euler(0f, 0f, 240f), W, 6);
            }, Gold);
            return new Vector3(0f, 0.08f, 0.44f);
        }

        // ------------------------------------------------------------------ PARTY POPPER
        static Vector3 PartyPopper(Transform r, Color tint)
        {
            Part(r, "Body", "popper_cone", b =>
            {
                b.AddCone(new Vector3(0f, 0.09f, 0.5f), 0.12f, 0.48f, Quaternion.Euler(-90f, 0f, 0f), W, 18);                  // apex points back
            }, Mat(tint, 0.4f));
            Part(r, "Stripes", "popper_stripes", b =>
            {
                for (int i = 0; i < 4; i++)
                {
                    float z = 0.12f + i * 0.1f;
                    float rad = 0.12f * (z - 0.02f) / 0.48f;
                    b.AddTorus(new Vector3(0f, 0.09f, z), rad + 0.004f, 0.01f, AlongZ, W, 16, 4);
                }
            }, Mat(new Color(1f, 0.85f, 0.2f), 0.4f));
            Part(r, "Grip", "popper_dark", b =>
            {
                Grip(b, -0.0f, 0.02f);
                b.AddCylinder(new Vector3(0f, 0.09f, 0.0f), 0.006f, 0.12f, AlongZ, W, 6, true);                                 // pull string
                b.AddTorus(new Vector3(0f, 0.09f, -0.07f), 0.022f, 0.006f, AlongX, W, 10, 4);
            }, Mat(new Color(0.95f, 0.95f, 0.95f), 0.3f));
            Part(r, "Confetti", "popper_confetti", b =>
            {
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.4f;
                    var p = new Vector3(Mathf.Cos(a) * 0.08f * (0.4f + (i % 3) * 0.3f), 0.09f + Mathf.Sin(a) * 0.08f * (0.4f + (i % 3) * 0.3f), 0.5f + (i % 2) * 0.012f);
                    b.AddBox(p, new Vector3(0.022f, 0.012f, 0.006f), Quaternion.Euler(i * 37f, i * 53f, i * 71f), W, 0f);
                }
            }, Mat(new Color(0.3f, 0.85f, 0.95f), 0.3f, 0.6f));
            Part(r, "ConfettiPink", "popper_confetti2", b =>
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 2.4f + 1.2f;
                    var p = new Vector3(Mathf.Cos(a) * 0.07f * (0.5f + (i % 2) * 0.4f), 0.09f + Mathf.Sin(a) * 0.07f * (0.5f + (i % 2) * 0.4f), 0.505f);
                    b.AddBox(p, new Vector3(0.02f, 0.012f, 0.006f), Quaternion.Euler(i * 29f, i * 61f, i * 43f), W, 0f);
                }
            }, Mat(new Color(1f, 0.4f, 0.7f), 0.3f, 0.6f));
            return new Vector3(0f, 0.09f, 0.54f);
        }

        // ------------------------------------------------------------------ CAPS-LOCK CANNON
        static Vector3 CapsLock(Transform r, Color tint)
        {
            Part(r, "Body", "caps_key", b =>
            {
                b.AddBox(new Vector3(0f, 0.09f, 0.07f), new Vector3(0.2f, 0.12f, 0.26f), Quaternion.identity, W, 0.035f);       // keycap
                b.AddBox(new Vector3(0f, 0.155f, 0.07f), new Vector3(0.17f, 0.02f, 0.22f), Quaternion.identity, W, 0.01f);      // dished top
            }, Mat(tint, 0.4f));
            Part(r, "Grip", "caps_dark", b =>
            {
                Grip(b);
                b.AddCylinder(new Vector3(0f, 0.08f, 0.3f), 0.04f, 0.22f, AlongZ, W, 14, true);                                 // barrel
                // the CAPS LOCK glyph on the top face: up arrow (two diagonal strokes + stem) and the bar under it
                b.AddBox(new Vector3(-0.022f, 0.168f, 0.12f), new Vector3(0.012f, 0.006f, 0.06f), Quaternion.Euler(0f, -40f, 0f), W, 0.002f);
                b.AddBox(new Vector3(0.022f, 0.168f, 0.12f), new Vector3(0.012f, 0.006f, 0.06f), Quaternion.Euler(0f, 40f, 0f), W, 0.002f);
                b.AddBox(new Vector3(0f, 0.168f, 0.085f), new Vector3(0.012f, 0.006f, 0.06f), Quaternion.identity, W, 0.002f);
                b.AddBox(new Vector3(0f, 0.168f, 0.035f), new Vector3(0.06f, 0.006f, 0.012f), Quaternion.identity, W, 0.002f);
                b.AddTorus(new Vector3(0.055f, 0.21f, 0.0f), 0.026f, 0.007f, AlongX, W, 12, 4);                                // padlock shackle
            }, Mat(Dark, 0.45f));
            Part(r, "Padlock", "caps_lock", b =>
            {
                b.AddBox(new Vector3(0.055f, 0.185f, 0.0f), new Vector3(0.022f, 0.05f, 0.06f), Quaternion.identity, W, 0.008f);
                b.AddTorus(new Vector3(0f, 0.08f, 0.41f), 0.045f, 0.012f, AlongZ, W, 14, 5);                                    // muzzle ring
            }, Gold);
            Part(r, "Lamp", "caps_lamp", b =>
            {
                b.AddBox(new Vector3(-0.06f, 0.168f, 0.15f), new Vector3(0.022f, 0.008f, 0.012f), Quaternion.identity, W, 0.002f);
                b.AddCylinder(new Vector3(0f, 0.08f, 0.418f), 0.014f, 0.01f, AlongZ, W, 8, true);                                // keyhole glow
            }, Glow(new Color(0.35f, 1f, 0.5f), 2.5f));
            return new Vector3(0f, 0.08f, 0.44f);
        }
    }
}
