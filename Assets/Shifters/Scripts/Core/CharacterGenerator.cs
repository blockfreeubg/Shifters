// =====================================================================================
//  SHIFTERS - CharacterGenerator.cs   [CONTRACT API - implementation owned by the CORE agent]
//  Deterministic (System.Random(seed)) team generation. See Tuning.cs for the archetype
//  guarantees that keep every puzzle solvable:
//    * exactly one Brute / Sprinter / Brainiac, slot order shuffled
//    * primary stat 7..10 (always the perk), secondaries 1..5 (never a perk), total 12..17
//    * unique names + hue-separated unique colors inside a team
//    * exactly ONE member carries the weapon (WeaponGenerator.Generate(rng), rolled LAST so the
//      rest of the team never depends on the weapon module's RNG usage)
//  Flavour data lives in Core/Session/CharacterPools.cs.
// =====================================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public static class CharacterGenerator
    {
        public const int TotalStatMin = 12;
        public const int TotalStatMax = 17;
        /// <summary>Minimum hue distance (0..1 hue units, 0.12 = ~43 degrees) between teammates' colors.</summary>
        public const float MinHueSeparation = 0.12f;

        static readonly Archetype[] Archetypes = { Archetype.Brute, Archetype.Sprinter, Archetype.Brainiac };

        /// <summary>Generate a full team of three (one Brute, one Sprinter, one Brainiac, shuffled slots),
        /// unique names/colors, randomized stats/bodies, and exactly ONE weapon on a random member.</summary>
        public static TeamData GenerateTeam(int seed)
        {
            var team = new TeamData { Seed = seed };
            var rng = new System.Random(seed);

            var order = (Archetype[])Archetypes.Clone();
            for (int i = order.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = order[i]; order[i] = order[j]; order[j] = t; }

            var usedNames = new HashSet<string>();
            var usedColors = new List<int>(3);
            for (int i = 0; i < 3; i++) team.Members[i] = GenerateMember(order[i], rng, usedNames, usedColors);

            int armed = rng.Next(3);
            var weapon = SafeWeapon(rng);
            team.Members[armed].Weapon = weapon;
            return team;
        }

        /// <summary>Re-clone a lost character for the next level: CloneGeneration+1 and -1 to a random
        /// SECONDARY stat (min 1). The primary (perk) stat never drops below Tuning.PrimaryStatMin.
        /// Adds a funny reprint suffix to the personality and fades the color a little ("printer low on ink").</summary>
        public static CharacterProfile Reclone(CharacterProfile lost, int seed)
        {
            if (lost == null) return null;
            var p = lost.Clone();
            p.CloneGeneration = Mathf.Max(1, p.CloneGeneration) + 1;
            var rng = new System.Random(unchecked(seed * 486187739 + p.CloneGeneration * 7919 + (int)p.Archetype));

            var s = p.Stats;
            GetSecondaries(p.Archetype, out var first, out var second);
            int a = s.Get(first), b = s.Get(second);
            bool canA = a > Tuning.StatMin, canB = b > Tuning.StatMin;
            if (canA && canB) { if (rng.Next(2) == 0) SetStat(s, first, a - 1); else SetStat(s, second, b - 1); }
            else if (canA) SetStat(s, first, a - 1);
            else if (canB) SetStat(s, second, b - 1);
            var primary = PrimaryOf(p.Archetype);
            SetStat(s, primary, Mathf.Clamp(s.Get(primary), Tuning.PrimaryStatMin, Tuning.StatMax));
            SetStat(s, first, Mathf.Clamp(s.Get(first), Tuning.StatMin, Tuning.SecondaryStatMax));
            SetStat(s, second, Mathf.Clamp(s.Get(second), Tuning.StatMin, Tuning.SecondaryStatMax));

            p.Personality = StripRecloneSuffix(p.Personality) + " " + Pick(rng, CharacterPools.RecloneSuffixes);
            Color.RGBToHSV(p.PrimaryColor, out float h, out float sat, out float v);
            p.PrimaryColor = Color.HSVToRGB(h, Mathf.Max(0.35f, sat * 0.9f), v);
            p.SecondaryColor = DarkShade(p.PrimaryColor);
            return p;
        }

        /// <summary>Primary (perk) stat of an archetype.</summary>
        public static PerspectiveMask PrimaryOf(Archetype a) =>
            a == Archetype.Brute ? PerspectiveMask.Strength : a == Archetype.Sprinter ? PerspectiveMask.Speed : PerspectiveMask.Intellect;

        /// <summary>One-line-per-member human readable summary (logs, tests, debug overlays).</summary>
        public static string Describe(TeamData team)
        {
            if (team == null || team.Members == null) return "(no team)";
            var sb = new StringBuilder();
            sb.Append("TEAM seed=").Append(team.Seed).Append('\n');
            for (int i = 0; i < team.Members.Length; i++)
            {
                var m = team.Members[i];
                if (m == null) { sb.Append("  [").Append(i + 1).Append("] (empty)\n"); continue; }
                sb.Append("  [").Append(i + 1).Append("] ").Append(m.DisplayName).Append(" - ").Append(m.Title)
                  .Append(" (").Append(m.Archetype).Append(") SPD ").Append(m.Stats.Speed).Append(" STR ").Append(m.Stats.Strength)
                  .Append(" IQ ").Append(m.Stats.IQ).Append(" | ").Append(m.Mass.ToString("0.0")).Append(" kg, ")
                  .Append(m.Body.Height.ToString("0.00")).Append(" m, ").Append(m.Body.Accessory).Append(", #")
                  .Append(ColorUtility.ToHtmlStringRGB(m.PrimaryColor)).Append(" | perks: ").Append(PerkList(m))
                  .Append(" | \"").Append(m.Personality).Append("\" / \"").Append(m.Catchphrase).Append("\"");
                if (m.HasWeapon)
                    sb.Append(" | WEAPON ").Append(m.Weapon.Name).Append(" (").Append(m.Weapon.Type)
                      .Append(m.Weapon.Type == WeaponType.Experimental ? "/" + m.Weapon.Variant : "").Append(")");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>"HEAVY LIFTER, GUN NAME" style perk list.</summary>
        public static string PerkList(CharacterProfile m)
        {
            var parts = new List<string>(4);
            if (m.HasStrengthPerk) parts.Add("HEAVY LIFTER");
            if (m.HasSpeedPerk) parts.Add("TURBO LEGS");
            if (m.HasIQPerk) parts.Add("BIG BRAIN");
            if (m.HasWeapon) parts.Add(m.Weapon.Name.ToUpperInvariant());
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        // ================================================================ member
        static CharacterProfile GenerateMember(Archetype a, System.Random rng, HashSet<string> usedNames, List<int> usedColors)
        {
            var p = new CharacterProfile { Archetype = a };
            p.Name = PickName(a, rng, usedNames);
            p.Title = Pick(rng, a == Archetype.Brute ? CharacterPools.BruteTitles : a == Archetype.Sprinter ? CharacterPools.SprinterTitles : CharacterPools.BrainiacTitles);
            p.Personality = Pick(rng, a == Archetype.Brute ? CharacterPools.BrutePersonalities : a == Archetype.Sprinter ? CharacterPools.SprinterPersonalities : CharacterPools.BrainiacPersonalities);
            p.Catchphrase = Pick(rng, a == Archetype.Brute ? CharacterPools.BruteCatchphrases : a == Archetype.Sprinter ? CharacterPools.SprinterCatchphrases : CharacterPools.BrainiacCatchphrases);

            int ci = PickColor(rng, usedColors);
            p.PrimaryColor = CharacterPools.Palette[ci];
            p.SecondaryColor = DarkShade(p.PrimaryColor);

            p.Stats = RollStats(a, rng);

            var mr = Tuning.MassRange(a);
            float massT = (float)rng.NextDouble();
            p.Mass = Mathf.Round(Mathf.Lerp(mr.x, mr.y, massT) * 10f) / 10f;

            p.Body = RollBody(a, rng, massT);
            p.VoicePitch = Mathf.Round(Range(rng, VoiceRange(a)) * 100f) / 100f;
            p.CloneGeneration = 1;
            p.Weapon = null;
            return p;
        }

        static CharacterStats RollStats(Archetype a, System.Random rng)
        {
            // primary 7..10 weighted toward 8 (7:25% 8:35% 9:25% 10:15%)
            int r = rng.Next(100);
            int primary = r < 25 ? 7 : r < 60 ? 8 : r < 85 ? 9 : 10;
            int s1 = 1, s2 = 1;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                s1 = rng.Next(Tuning.StatMin, Tuning.SecondaryStatMax + 1);
                s2 = rng.Next(Tuning.StatMin, Tuning.SecondaryStatMax + 1);
                int total = primary + s1 + s2;
                if (total >= TotalStatMin && total <= TotalStatMax) break;
            }
            // deterministic repair if the band still isn't met
            while (primary + s1 + s2 > TotalStatMax) { if (s1 >= s2 && s1 > Tuning.StatMin) s1--; else if (s2 > Tuning.StatMin) s2--; else break; }
            while (primary + s1 + s2 < TotalStatMin) { if (s1 <= s2 && s1 < Tuning.SecondaryStatMax) s1++; else if (s2 < Tuning.SecondaryStatMax) s2++; else break; }

            var st = new CharacterStats();
            GetSecondaries(a, out var first, out var second);
            SetStat(st, PrimaryOf(a), primary);
            SetStat(st, first, s1);
            SetStat(st, second, s2);
            return st;
        }

        static BodyShape RollBody(Archetype a, System.Random rng, float massT)
        {
            var b = new BodyShape();
            b.Height = Round3(Tuning.BaseHeight(a) * (1f + Range(rng, -Tuning.HeightJitter, Tuning.HeightJitter)));
            b.Radius = Round3(Tuning.CapsuleRadius(a) * (1f + Range(rng, -0.03f, 0.03f)));
            // heavier rolls look heavier: mass biases width & belly
            b.WidthScale = Round3(Mathf.Clamp(Range(rng, 0.88f, 1.08f) + massT * 0.12f, 0.85f, 1.2f));
            b.BellyScale = Round3(Mathf.Clamp(Range(rng, 0.9f, 1.15f) + massT * 0.15f, 0.9f, 1.3f));
            b.HeadScale = Round3(Range(rng, 0.85f, 1.2f));
            b.LimbScale = Round3(Range(rng, 0.9f, 1.1f));
            b.EyeScale = Round3(Range(rng, 0.85f, 1.25f));
            b.MouthStyle = rng.Next(3);
            b.Accessory = PickAccessory(rng, a == Archetype.Brute ? CharacterPools.BruteAccessories : a == Archetype.Sprinter ? CharacterPools.SprinterAccessories : CharacterPools.BrainiacAccessories);
            // hair: brutes are often bald, brainiacs often have wild hair
            int bald = a == Archetype.Brute ? 40 : a == Archetype.Sprinter ? 10 : 15;
            b.HairStyle = rng.Next(100) < bald ? 0 : rng.Next(1, 5);
            b.SkinTone = rng.Next(100) < 4 ? Pick(rng, CharacterPools.NoveltySkins) : Pick(rng, CharacterPools.SkinTones);
            b.HairColor = rng.Next(100) < 12 ? Pick(rng, CharacterPools.DyedHair) : Pick(rng, CharacterPools.HairColors);
            return b;
        }

        static Vector2 VoiceRange(Archetype a) => a == Archetype.Brute ? new Vector2(0.75f, 0.9f) : a == Archetype.Sprinter ? new Vector2(1.15f, 1.3f) : new Vector2(1.0f, 1.15f);

        static WeaponData SafeWeapon(System.Random rng)
        {
            WeaponData w = null;
            try { w = WeaponGenerator.Generate(rng); }
            catch (System.Exception e) { Debug.LogWarning("[CharacterGenerator] WeaponGenerator failed: " + e.Message); }
            if (w == null) w = new WeaponData();
            if (string.IsNullOrEmpty(w.Name)) w.Name = "Mystery Blaster";
            return w;
        }

        // ================================================================ picking helpers
        static string PickName(Archetype a, System.Random rng, HashSet<string> used)
        {
            var themed = a == Archetype.Brute ? CharacterPools.BruteNames : a == Archetype.Sprinter ? CharacterPools.SprinterNames : CharacterPools.BrainiacNames;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var pool = rng.Next(100) < 40 ? themed : CharacterPools.SharedNames;
                var n = pool[rng.Next(pool.Length)];
                if (used.Add(n)) return n;
            }
            for (int i = 0; i < CharacterPools.SharedNames.Length; i++)
                if (used.Add(CharacterPools.SharedNames[i])) return CharacterPools.SharedNames[i];
            var fallback = "Intern #" + (used.Count + 1);
            used.Add(fallback);
            return fallback;
        }

        static int PickColor(System.Random rng, List<int> used)
        {
            var pal = CharacterPools.Palette;
            int start = rng.Next(pal.Length);
            for (int pass = 0; pass < 3; pass++)
            {
                float minSep = pass == 0 ? MinHueSeparation : pass == 1 ? MinHueSeparation * 0.5f : 0f;
                for (int k = 0; k < pal.Length; k++)
                {
                    int ci = (start + k * 5) % pal.Length; // stride 5 (coprime with 12) walks the whole palette
                    if (used.Contains(ci)) continue;
                    if (MinHueDistance(ci, used) < minSep) continue;
                    used.Add(ci);
                    return ci;
                }
            }
            used.Add(start);
            return start;
        }

        static float MinHueDistance(int ci, List<int> used)
        {
            Color.RGBToHSV(CharacterPools.Palette[ci], out float h, out _, out _);
            float best = 1f;
            for (int i = 0; i < used.Count; i++)
            {
                Color.RGBToHSV(CharacterPools.Palette[used[i]], out float h2, out _, out _);
                float d = Mathf.Abs(h - h2);
                d = Mathf.Min(d, 1f - d);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Circular hue distance (0..0.5) between two colors.</summary>
        public static float HueDistance(Color a, Color b)
        {
            Color.RGBToHSV(a, out float h1, out _, out _);
            Color.RGBToHSV(b, out float h2, out _, out _);
            float d = Mathf.Abs(h1 - h2);
            return Mathf.Min(d, 1f - d);
        }

        static AccessoryType PickAccessory(System.Random rng, AccessoryWeight[] table)
        {
            int total = 0;
            for (int i = 0; i < table.Length; i++) total += table[i].Weight;
            int r = rng.Next(total);
            for (int i = 0; i < table.Length; i++) { r -= table[i].Weight; if (r < 0) return table[i].Type; }
            return AccessoryType.None;
        }

        static Color DarkShade(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 0.85f), v * 0.42f);
        }

        static string StripRecloneSuffix(string personality)
        {
            if (string.IsNullOrEmpty(personality)) return "";
            var sfx = CharacterPools.RecloneSuffixes;
            for (int i = 0; i < sfx.Length; i++)
            {
                string tail = " " + sfx[i];
                if (personality.EndsWith(tail)) return personality.Substring(0, personality.Length - tail.Length);
            }
            return personality;
        }

        static void GetSecondaries(Archetype a, out PerspectiveMask first, out PerspectiveMask second)
        {
            switch (a)
            {
                case Archetype.Brute: first = PerspectiveMask.Speed; second = PerspectiveMask.Intellect; break;
                case Archetype.Sprinter: first = PerspectiveMask.Strength; second = PerspectiveMask.Intellect; break;
                default: first = PerspectiveMask.Speed; second = PerspectiveMask.Strength; break;
            }
        }

        static void SetStat(CharacterStats s, PerspectiveMask stat, int value)
        {
            if (stat == PerspectiveMask.Speed) s.Speed = value;
            else if (stat == PerspectiveMask.Strength) s.Strength = value;
            else if (stat == PerspectiveMask.Intellect) s.IQ = value;
        }

        static T Pick<T>(System.Random rng, T[] arr) => arr[rng.Next(arr.Length)];
        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static float Range(System.Random rng, Vector2 r) => Range(rng, r.x, r.y);
        static float Round3(float v) => Mathf.Round(v * 1000f) / 1000f;
    }
}
