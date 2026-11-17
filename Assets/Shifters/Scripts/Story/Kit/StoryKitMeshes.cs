// =====================================================================================
//  SHIFTERS - StoryKitMeshes.cs   [owned by STORY_KIT]
//  Cached procedural meshes for the story props (vertex colored; materials multiply vertex colors).
//  FACE SPACE: every mesh is built with its FRONT along +Z and its feet / base at y = 0 (StoryKit roots are built
//  with EnvKit.ViewToFace(rot), so root.forward = the front = the side the reader / viewer sees).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class StoryKitMeshes
    {
        public static readonly Color Suit = Hex(0x6E737B), SuitDark = Hex(0x5A5F66), Skin = Hex(0xF2C9A0), Gold = Hex(0xE8B83A),
            Ink = Hex(0x202226), HairGrey = Hex(0x8E9196), BrowGrey = Hex(0x4A4D52), Cape = Hex(0x666A72), DrumWhite = Hex(0xEDE6D6),
            Ruby = Hex(0xC8202E), PercyBrown = Hex(0x8B5A2B), Chrome = Hex(0xC9CED6), Cream = Hex(0xE6D3B3), Stone = Hex(0x8D9196),
            Moss = Hex(0x6F7F5A);

        public static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        static Color StoneShade(Color c, bool statue)
        {
            if (!statue) return c;
            float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            float v = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(l * 1.25f));
            return new Color(v, v, v, 1f);
        }

        /// <summary>Maximilian Me, 1.20 m body / 1.45 m with crown, front +Z, feet at y=0. statue = grey shades only (for a
        /// stone material); otherwise full colors (also used under the gold hologram material). sunglasses adds shades.</summary>
        public static Mesh MaxFigure(bool statue, bool sunglasses = false)
        {
            string key = "story_maxfig_" + (statue ? "s" : "c") + (sunglasses ? "_sg" : "");
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var I = Quaternion.identity;
                System.Func<Color, Color> S = c => StoneShade(c, statue);
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    b.AddBox(new Vector3(sx * 0.1f, 0.1f, 0f), new Vector3(0.13f, 0.2f, 0.16f), I, S(SuitDark), 0.02f);
                    b.AddBox(new Vector3(sx * 0.1f, 0.035f, 0.04f), new Vector3(0.15f, 0.07f, 0.25f), I, S(Ink), 0.02f);
                    b.AddCapsule(new Vector3(sx * 0.32f, 0.5f, 0.02f), 0.075f, 0.38f, Quaternion.Euler(0f, 0f, sx * 14f), S(Suit), 8, 3);
                    b.AddSphere(new Vector3(sx * 0.36f, 0.31f, 0.05f), 0.07f, I, S(Skin), 8, 6);
                    // eyes, brows, mustache halves + curls
                    b.AddSphere(new Vector3(sx * 0.08f, 1.03f, 0.205f), 0.032f, I, S(Ink), 8, 6);
                    b.AddBox(new Vector3(sx * 0.085f, 1.095f, 0.2f), new Vector3(0.13f, 0.04f, 0.05f), Quaternion.Euler(0f, 0f, -sx * 12f), S(BrowGrey), 0.01f);
                    b.AddSphere(new Vector3(sx * 0.075f, 0.915f, 0.215f), 0.07f, I, S(HairGrey), 10, 6, true, new Vector3(1.45f, 0.55f, 0.6f));
                    b.AddSphere(new Vector3(sx * 0.16f, 0.945f, 0.19f), 0.032f, I, S(HairGrey), 8, 6);
                    // crown spikes
                }
                b.AddSphere(new Vector3(0f, 0.47f, 0f), 0.3f, I, S(Suit), 16, 10, true, new Vector3(1f, 1.05f, 0.82f));
                for (int i = 0; i < 3; i++) b.AddSphere(new Vector3(0f, 0.36f + i * 0.11f, 0.245f - Mathf.Abs(i - 1) * 0.012f), 0.03f, I, S(Gold), 8, 6);
                b.AddBox(new Vector3(0f, 0.74f, 0.17f), new Vector3(0.14f, 0.1f, 0.06f), I, S(Color.white), 0.01f);
                b.AddSphere(new Vector3(0f, 0.98f, 0.02f), 0.23f, I, S(Skin), 16, 10, true, new Vector3(1f, 1.05f, 0.95f));
                b.AddSphere(new Vector3(0f, 0.835f, 0.1f), 0.15f, I, S(Skin), 12, 8, true, new Vector3(1.1f, 0.8f, 1f));
                b.AddSphere(new Vector3(0f, 0.975f, 0.24f), 0.05f, I, S(Hex(0xE8B28A)), 8, 6);
                // monocle + chain
                b.AddTorus(new Vector3(0.08f, 1.03f, 0.228f), 0.052f, 0.009f, Quaternion.Euler(90f, 0f, 0f), S(Gold), 14, 5);
                b.AddBox(new Vector3(0.13f, 0.93f, 0.215f), new Vector3(0.012f, 0.19f, 0.012f), Quaternion.Euler(0f, 0f, 14f), S(Gold), 0f);
                // crown, two sizes too big
                b.AddCylinder(new Vector3(0f, 1.27f, 0.02f), 0.215f, 0.15f, I, S(Gold), 16, true, true);
                for (int i = 0; i < 5; i++)
                {
                    float a = (i - 2) * 0.62f;
                    b.AddCone(new Vector3(Mathf.Sin(a) * 0.19f, 1.34f, 0.02f + Mathf.Cos(a) * 0.19f), 0.045f, 0.12f, I, S(Gold), 6);
                }
                b.AddSphere(new Vector3(0f, 1.27f, 0.235f), 0.028f, I, S(Ruby), 8, 6);
                // cape + gold collar
                b.AddBox(new Vector3(0f, 0.48f, -0.25f), new Vector3(0.64f, 0.6f, 0.04f), Quaternion.Euler(-8f, 0f, 0f), S(Cape), 0.01f);
                b.AddTorus(new Vector3(0f, 0.77f, 0f), 0.19f, 0.045f, I, S(Gold), 14, 6);
                // one-man band bass drum on the back
                b.AddCylinder(new Vector3(0f, 0.55f, -0.36f), 0.27f, 0.18f, Quaternion.Euler(90f, 0f, 0f), S(DrumWhite), 18, true, true);
                b.AddTorus(new Vector3(0f, 0.55f, -0.27f), 0.27f, 0.022f, Quaternion.Euler(90f, 0f, 0f), S(Gold), 18, 5);
                b.AddTorus(new Vector3(0f, 0.55f, -0.45f), 0.27f, 0.022f, Quaternion.Euler(90f, 0f, 0f), S(Gold), 18, 5);
                if (sunglasses)
                {
                    b.AddBox(new Vector3(0f, 1.035f, 0.225f), new Vector3(0.3f, 0.075f, 0.03f), I, Ink, 0.01f);
                    b.AddBox(new Vector3(0f, 1.065f, 0.22f), new Vector3(0.34f, 0.015f, 0.02f), I, Ink, 0f);
                }
                return b.ToMesh(key);
            });
        }

        /// <summary>Bedsheet ghost, ~1.5 m, front +Z.</summary>
        public static Mesh Ghost(Color sheet)
        {
            string key = "story_ghost_" + ColorUtility.ToHtmlStringRGB(sheet);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var I = Quaternion.identity;
                b.AddCone(new Vector3(0f, 0.12f, 0f), 0.47f, 1.18f, I, sheet, 16);
                b.AddSphere(new Vector3(0f, 1.2f, 0f), 0.3f, I, sheet, 14, 10);
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2f;
                    b.AddSphere(new Vector3(Mathf.Sin(a) * 0.43f, 0.13f, Mathf.Cos(a) * 0.43f), 0.11f, I, sheet, 8, 6);
                }
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    b.AddSphere(new Vector3(sx * 0.1f, 1.25f, 0.27f), 0.06f, I, Ink, 8, 6, true, new Vector3(1f, 1.4f, 0.45f));
                    b.AddSphere(new Vector3(sx * 0.33f, 0.85f, 0.18f), 0.1f, I, sheet, 8, 6);
                }
                b.AddSphere(new Vector3(0f, 1.11f, 0.285f), 0.04f, I, Ink, 8, 6, true, new Vector3(1f, 1.2f, 0.4f));
                return b.ToMesh(key);
            });
        }

        /// <summary>Rounded tombstone of width w and total height h (incl. a 0.15 m mossy base), text face +Z at z = 0.11.</summary>
        public static Mesh Tombstone(float w, float h)
        {
            string key = EnvMeshes.Key("story_tomb", w, h);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var I = Quaternion.identity;
                b.AddBox(new Vector3(0f, 0.075f, 0f), new Vector3(w + 0.22f, 0.15f, 0.5f), I, Moss, 0.03f);
                float body = Mathf.Max(0.2f, h - 0.15f - w * 0.5f);
                b.AddBox(new Vector3(0f, 0.15f + body * 0.5f, 0f), new Vector3(w, body, 0.22f), I, Stone, 0.02f);
                b.AddCylinder(new Vector3(0f, 0.15f + body, 0f), w * 0.5f, 0.22f, Quaternion.Euler(90f, 0f, 0f), Stone, 20, true, true);
                b.AddBox(new Vector3(-w * 0.3f, 0.2f, 0.12f), new Vector3(w * 0.25f, 0.08f, 0.03f), I, Moss, 0.02f);
                return b.ToMesh(key);
            });
        }

        /// <summary>One merged mesh of 'count' simple citizens scattered in a local XZ area (bounds center/size), facing -Z
        /// (toward a viewer on the -Z side, i.e. the 'front' of a stage that faces -Z) with +-25 deg jitter.</summary>
        public static Mesh Crowd(Bounds area, int count, int seed)
        {
            count = Mathf.Clamp(count, 1, 400);
            string key = EnvMeshes.Key("story_crowd", area.center, area.size, count, seed);
            return ProcMesh.Cached(key, () =>
            {
                var rng = new System.Random(seed);
                Color[] outfits = { Hex(0x8A8F99), Hex(0x6E737B), Hex(0x9DA3AD), Hex(0xB07CFF), Hex(0x2EC4B6), Hex(0xFF8A1F), Hex(0x3C7DD9), Hex(0xE8394B), Hex(0x3DDC97), Hex(0xFFC93C) };
                Color[] skins = { Hex(0xF2C9A0), Hex(0xD9A47A), Hex(0xA8714B), Hex(0x7A4E32), Hex(0xFFE0C2) };
                var b = new ProcMesh.Builder();
                for (int i = 0; i < count; i++)
                {
                    float x = area.center.x + ((float)rng.NextDouble() - 0.5f) * area.size.x;
                    float z = area.center.z + ((float)rng.NextDouble() - 0.5f) * area.size.z;
                    float y = area.center.y - area.size.y * 0.5f;
                    float s = 1.1f + (float)rng.NextDouble() * 0.32f;   // 1.45-1.85 m citizens (interns ~1.8 m)
                    var rot = Quaternion.Euler(0f, 180f + ((float)rng.NextDouble() - 0.5f) * 50f, 0f);
                    var o = outfits[rng.Next(outfits.Length)];
                    var sk = skins[rng.Next(skins.Length)];
                    var p = new Vector3(x, y, z);
                    b.AddCapsule(p + new Vector3(0f, 0.5f * s, 0f), 0.2f * s, 0.95f * s, rot, o, 8, 2);
                    b.AddSphere(p + new Vector3(0f, 1.12f * s, 0f), 0.17f * s, rot, sk, 8, 5);
                    if (rng.NextDouble() < 0.35) b.AddCapsule(p + rot * new Vector3(0.22f * s, 1.2f * s, 0f), 0.05f * s, 0.4f * s, rot, o, 6, 2); // a raised hand
                }
                return b.ToMesh(key);
            });
        }

        /// <summary>Percy the Percolator body (0.8 x 1.3 x 0.6 m), front +Z, feet at y = 0. Screen/lamp/carafe are separate.</summary>
        public static Mesh PercyBody()
        {
            const string key = "story_percy_body";
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var I = Quaternion.identity;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        b.AddCylinder(new Vector3(sx * 0.32f, 0.03f, sz * 0.22f), 0.04f, 0.06f, I, Chrome, 8, true, true);
                b.AddBox(new Vector3(0f, 0.64f, 0f), new Vector3(0.8f, 1.16f, 0.6f), I, Cream, 0.05f);
                b.AddBox(new Vector3(0f, 1.25f, 0f), new Vector3(0.84f, 0.1f, 0.64f), I, Chrome, 0.03f);
                b.AddBox(new Vector3(0f, 0.08f, 0f), new Vector3(0.84f, 0.06f, 0.64f), I, Chrome, 0.02f);
                b.AddBox(new Vector3(0f, 0.8f, 0.301f), new Vector3(0.82f, 0.04f, 0.02f), I, Chrome, 0f);
                b.AddBox(new Vector3(0f, 0.43f, 0.29f), new Vector3(0.52f, 0.42f, 0.04f), I, Hex(0x2A2420), 0.01f);
                b.AddBox(new Vector3(0f, 0.25f, 0.27f), new Vector3(0.4f, 0.03f, 0.14f), I, Chrome, 0.01f);
                b.AddBox(new Vector3(0f, 0.98f, 0.3f), new Vector3(0.44f, 0.3f, 0.03f), I, PercyBrown, 0.01f);
                b.AddBox(new Vector3(0f, 0.98f, 0.31f), new Vector3(0.38f, 0.24f, 0.02f), I, Ink, 0.005f);
                b.AddBox(new Vector3(0f, 0.66f, 0.305f), new Vector3(0.5f, 0.09f, 0.015f), I, Chrome, 0.005f);
                b.AddBox(new Vector3(0.41f, 0.95f, 0f), new Vector3(0.04f, 0.3f, 0.06f), I, Chrome, 0.01f);
                b.AddCylinder(new Vector3(-0.25f, 1.33f, -0.1f), 0.05f, 0.08f, I, Chrome, 10, true, true);  // steam whistle
                return b.ToMesh(key);
            });
        }

        /// <summary>Simple "toy block" box with bevel (uniform color; the letters are TMP).</summary>
        public static Mesh Block(Vector3 size) => ProcMesh.CachedBox(size, Mathf.Min(0.08f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.08f));
    }
}
