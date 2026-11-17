// =====================================================================================
//  SHIFTERS - VisualsTestKit.cs   (VISUALS tests only)
//  * TestTeam(seed): varied, design-faithful random teams for visual review (used when the
//    real CharacterGenerator is still a stub that returns identical members).
//  * VisualsPreviewDriver: fakes motor values on a CharacterVisual (the stub motor never moves).
// =====================================================================================
using System.IO;
using UnityEngine;

namespace Shifters
{
    public static class VisualsTestKit
    {
        static readonly int[] Palette = { 0xFF5A5F, 0xFFB400, 0x7BC950, 0x3D9BE9, 0x9B5DE5, 0xF15BB5, 0x00BBF9, 0xFF8C42, 0xE63946, 0x43AA8B, 0xF9C74F, 0x577590 };
        static readonly int[] Pants = { 0x2B2D42, 0x3A4A6B, 0x5B3A29, 0x37474F, 0x4A5D8C, 0x6B5E45 };
        static readonly int[] Skins = { 0xFFDBC4, 0xF1C27D, 0xE0AC69, 0xC68642, 0x8D5524, 0x5C3A1E };
        static readonly int[] Hairs = { 0x1E1B18, 0x5A3825, 0xE8C872, 0xB5482B, 0x9EA3A8, 0x3D6FE0, 0xE85D9E };
        static readonly string[] Names = { "Gary", "Brenda", "Big Steve", "Dr. Toast", "Mildred", "Kevin", "Professor Noodle", "Zippy", "Bartholomew", "Doug", "Agnes", "Sprocket", "Tina Turbo", "Reginald", "Beans", "Wobbles McGee", "Chad", "Linda" };
        static readonly AccessoryType[] BruteAcc = { AccessoryType.Headband, AccessoryType.HardHat, AccessoryType.Mustache, AccessoryType.Crown, AccessoryType.None, AccessoryType.Beanie };
        static readonly AccessoryType[] SprintAcc = { AccessoryType.Visor, AccessoryType.Headband, AccessoryType.PropellerCap, AccessoryType.Headphones, AccessoryType.None, AccessoryType.Beanie };
        static readonly AccessoryType[] BrainAcc = { AccessoryType.Glasses, AccessoryType.BowTie, AccessoryType.Antenna, AccessoryType.Headphones, AccessoryType.PropellerCap, AccessoryType.Crown };

        public static Color Hex(int rgb) => UIColors.Hex(rgb);

        /// <summary>Real generator if it already produces varied members, else a local randomizer.</summary>
        public static TeamData Team(int seed)
        {
            var real = CharacterGenerator.GenerateTeam(seed);
            bool varied = real.Members[0] != null && real.Members[1] != null &&
                          (real.Members[0].PrimaryColor != real.Members[1].PrimaryColor || real.Members[0].Name != real.Members[1].Name);
            return varied ? real : TestTeam(seed);
        }

        public static TeamData TestTeam(int seed)
        {
            var rng = new System.Random(seed * 7919 + 3);
            var team = new TeamData { Seed = seed };
            Archetype[] order = { Archetype.Brute, Archetype.Sprinter, Archetype.Brainiac };
            for (int i = 2; i > 0; i--) { int j = rng.Next(i + 1); var t = order[i]; order[i] = order[j]; order[j] = t; }
            int c0 = rng.Next(Palette.Length);
            for (int i = 0; i < 3; i++)
            {
                var a = order[i];
                var p = new CharacterProfile { Archetype = a, Name = Names[rng.Next(Names.Length)] };
                p.PrimaryColor = Hex(Palette[(c0 + i * 4 + rng.Next(2)) % Palette.Length]);
                p.SecondaryColor = Hex(Pants[rng.Next(Pants.Length)]);
                p.Stats = new CharacterStats { Speed = a == Archetype.Sprinter ? 7 + rng.Next(4) : 1 + rng.Next(5), Strength = a == Archetype.Brute ? 7 + rng.Next(4) : 1 + rng.Next(5), IQ = a == Archetype.Brainiac ? 7 + rng.Next(4) : 1 + rng.Next(5) };
                var mr = Tuning.MassRange(a); p.Mass = Mathf.Lerp(mr.x, mr.y, (float)rng.NextDouble());
                var b = p.Body;
                b.Height = Tuning.BaseHeight(a) * (1f + ((float)rng.NextDouble() * 2f - 1f) * Tuning.HeightJitter);
                b.Radius = Tuning.CapsuleRadius(a);
                b.WidthScale = R(rng, 0.85f, 1.2f); b.HeadScale = R(rng, 0.85f, 1.2f); b.LimbScale = R(rng, 0.9f, 1.1f);
                b.BellyScale = R(rng, 0.9f, 1.3f); b.EyeScale = R(rng, 0.85f, 1.25f);
                var acc = a == Archetype.Brute ? BruteAcc : a == Archetype.Sprinter ? SprintAcc : BrainAcc;
                b.Accessory = acc[rng.Next(acc.Length)];
                b.HairStyle = rng.Next(5); b.MouthStyle = rng.Next(3);
                b.SkinTone = Hex(Skins[rng.Next(Skins.Length)]); b.HairColor = Hex(Hairs[rng.Next(Hairs.Length)]);
                team.Members[i] = p;
            }
            var w = team.Members[rng.Next(3)];
            w.Weapon = Weapon((WeaponType)rng.Next(5), (ExperimentalVariant)rng.Next(2));
            return team;
        }

        public static WeaponData Weapon(WeaponType t, ExperimentalVariant v)
        {
            var tints = new[] { 0xFF8A1F, 0x3DE0FF, 0xB07CFF, 0x7BC950, 0xFFC93C, 0xFF4D5E };
            return new WeaponData { Type = t, Variant = v, Name = t + " " + v, Tint = Hex(tints[((int)t * 2 + (int)v) % tints.Length]) };
        }

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        /// <summary>Save a RenderTexture to PNG. Returns the per-pixel luminance standard deviation (0 = blank).</summary>
        public static float SaveRT(Texture tex, string path)
        {
            var rt = tex as RenderTexture;
            if (rt == null) return 0f;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t2 = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t2.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t2.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, t2.EncodeToPNG());
            var px = t2.GetPixels32();
            double sum = 0, sq = 0;
            for (int i = 0; i < px.Length; i++) { double l = (px[i].r * 0.3 + px[i].g * 0.59 + px[i].b * 0.11) / 255.0; sum += l; sq += l * l; }
            Object.Destroy(t2);
            double mean = sum / px.Length;
            return (float)System.Math.Sqrt(System.Math.Max(0.0, sq / px.Length - mean * mean));
        }

        /// <summary>Side-by-side strip of several RenderTextures saved as one PNG.</summary>
        public static void SaveStrip(Texture[] texs, string path)
        {
            int w = 0, h = 0;
            foreach (var t in texs) if (t != null) { w += t.width; h = Mathf.Max(h, t.height); }
            if (w == 0) return;
            var strip = new Texture2D(w, h, TextureFormat.RGBA32, false);
            int x = 0;
            var prev = RenderTexture.active;
            foreach (var t in texs)
            {
                var rt = t as RenderTexture; if (rt == null) continue;
                RenderTexture.active = rt;
                strip.ReadPixels(new Rect(0, 0, rt.width, rt.height), x, 0);
                x += rt.width;
            }
            strip.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, strip.EncodeToPNG());
            Object.Destroy(strip);
        }
    }
}
