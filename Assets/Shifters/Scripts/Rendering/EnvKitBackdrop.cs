// =====================================================================================
//  SHIFTERS - EnvKitBackdrop.cs   (RENDERING)
//  Void backdrop (huge distant silhouettes for depth: giant pillars, floating facility chunks, colossal
//  "SHIFTERS INC." signs) and hazard visuals (AcidPool, VoidPit - visual only; puzzles add KillZones).
//  The backdrop is 2 merged meshes (lit structures + unlit glowing windows) + a few TMP signs.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class EnvKit
    {
        readonly struct BackdropBox
        {
            public readonly Vector3 Center, Size; public readonly Color Color; public readonly float Bevel; public readonly Quaternion Rot;
            public BackdropBox(Vector3 c, Vector3 s, Color col, float bevel, Quaternion rot) { Center = c; Size = s; Color = col; Bevel = bevel; Rot = rot; }
        }

        static readonly string[] BackdropSigns = { "SHIFTERS INC.", "TEST CHAMBER", "SAFETY THIRD", "HR IS WATCHING", "NO REFUNDS" };

        /// <summary>Giant distant silhouettes around 'bounds' (the playable level volume). Placed 60-200 m outside the
        /// bounds so the theme fog fades them into the void. No colliders, no shadows.</summary>
        public static GameObject VoidBackdrop(Transform parent, LevelTheme theme, Bounds bounds, int seed = 7)
        {
            var root = Kit.Make("VoidBackdrop", parent, Vector3.zero);
            var t = root.transform;
            var prev = Kit.Theme; Kit.Theme = theme;
            var look = ThemeLooks.Get(theme);
            var c = bounds.center;
            float baseR = Mathf.Max(bounds.extents.x, bounds.extents.z) + 60f;
            string key = EnvMeshes.Key("env_backdrop", theme, c, bounds.size, (float)seed);
            var dark = Color.Lerp(Palettes.Color(MatKey.MetalDark, theme), look.FogColor, 0.15f);
            var mid = Color.Lerp(Palettes.Color(MatKey.WallAlt, theme), look.FogColor, 0.2f);
            var accent = look.Accent;
            var cool = UIColors.Hex(0x3DE0FF);

            // one deterministic layout pass shared by the lit structure mesh and the unlit glow mesh (glow bands wrap
            // their pillar with the pillar's own rotation)
            var solid = new System.Collections.Generic.List<BackdropBox>(64);
            var glow = new System.Collections.Generic.List<BackdropBox>(64);
            {
                var rng = new System.Random(seed);
                float R() => (float)rng.NextDouble();
                for (int i = 0; i < 11; i++)
                {
                    float a = i / 11f * Mathf.PI * 2f + R() * 0.35f, r = baseR + R() * 110f;
                    var pos = new Vector3(c.x + Mathf.Cos(a) * r, 0f, c.z + Mathf.Sin(a) * r);
                    float w = 8f + R() * 14f, top = c.y + 15f + R() * 85f, bottom = c.y - 260f;
                    var col = Color.Lerp(dark, mid, R() * 0.6f);
                    var q = Quaternion.Euler(0f, R() * 90f, 0f);
                    solid.Add(new BackdropBox(new Vector3(pos.x, (top + bottom) * 0.5f, pos.z), new Vector3(w, top - bottom, w * 0.8f), col, 0.4f, q));
                    solid.Add(new BackdropBox(new Vector3(pos.x, top + 2f, pos.z), new Vector3(w + 3f, 4f, w * 0.8f + 3f), dark, 0.3f, q));
                    for (int k = 0; k < 4; k++)
                        glow.Add(new BackdropBox(new Vector3(pos.x, top - 6f - k * 13f, pos.z), new Vector3(w + 0.5f, 0.7f, w * 0.8f + 0.5f), k % 2 == 0 ? accent : cool, 0f, q));
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = (i + 0.5f) / 8f * Mathf.PI * 2f + R() * 0.4f, r = baseR + 25f + R() * 120f;
                    var pos = new Vector3(c.x + Mathf.Cos(a) * r, c.y - 35f + R() * 75f, c.z + Mathf.Sin(a) * r);
                    var size = new Vector3(18f + R() * 28f, 5f + R() * 7f, 12f + R() * 20f);
                    var q = Quaternion.Euler(0f, R() * 180f, 0f);
                    solid.Add(new BackdropBox(pos, size, mid, 0.5f, q));
                    solid.Add(new BackdropBox(pos + Vector3.down * (size.y * 0.5f + 3f), new Vector3(size.x * 0.6f, 6f, size.z * 0.6f), dark, 0.5f, q));
                    glow.Add(new BackdropBox(pos + Vector3.down * (size.y * 0.5f + 6.2f), new Vector3(size.x * 0.4f, 0.6f, size.z * 0.4f), cool, 0f, q));
                    for (int k = 0; k < 3; k++)
                    {
                        float bh = 5f + R() * 16f, bw = 4f + R() * 7f;
                        var off = q * new Vector3((R() - 0.5f) * size.x * 0.7f, size.y * 0.5f + bh * 0.5f, (R() - 0.5f) * size.z * 0.7f);
                        solid.Add(new BackdropBox(pos + off, new Vector3(bw, bh, bw * (0.7f + R() * 0.6f)), Color.Lerp(dark, mid, R()), 0.3f, q));
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = R() * Mathf.PI * 2f, r = R() * baseR * 0.8f;
                    solid.Add(new BackdropBox(new Vector3(c.x + Mathf.Cos(a) * r, c.y - 70f - R() * 60f, c.z + Mathf.Sin(a) * r), new Vector3(40f + R() * 50f, 8f, 40f + R() * 50f), dark, 0.5f, Quaternion.identity));
                }
            }
            var structures = EnvMeshes.Cached(key + "_s", p => { foreach (var b in solid) p.Box(b.Center, b.Size, b.Color, b.Bevel, b.Rot); });
            EnvMeshes.Render(t, "Structures", structures, Kit.Mat(MatKey.White), Vector3.zero, null, false);
            var glows = EnvMeshes.Cached(key + "_g2", p => { foreach (var b in glow) p.Box(b.Center, b.Size, b.Color, b.Bevel, b.Rot); });
            EnvMeshes.Render(t, "Glows", glows, MaterialLibrary.UnlitGlowMaterial, Vector3.zero, null, false);

            // colossal signs facing the level
            var srng = new System.Random(seed * 31 + 3);
            for (int i = 0; i < 3; i++)
            {
                float a = (i / 3f + (float)srng.NextDouble() * 0.15f) * Mathf.PI * 2f, r = baseR + 40f + (float)srng.NextDouble() * 40f;
                var pos = new Vector3(c.x + Mathf.Cos(a) * r, c.y + 18f + (float)srng.NextDouble() * 25f, c.z + Mathf.Sin(a) * r);
                var face = Quaternion.LookRotation(new Vector3(c.x - pos.x, 0f, c.z - pos.z).normalized);
                var sgo = Kit.Make("BigSign" + i, t, pos, face);
                string txt = i == 0 ? BackdropSigns[0] : BackdropSigns[1 + srng.Next(BackdropSigns.Length - 1)];
                float w = 34f + txt.Length * 1.6f;
                Kit.BoxVisual(sgo.transform, "Board", Vector3.zero, new Vector3(w, 9f, 1f), MatKey.MetalDark, 0.3f, null, false);
                Kit.BoxVisual(sgo.transform, "Edge", new Vector3(0f, -5f, 0f), new Vector3(w + 1f, 0.8f, 1.4f), MatKey.TrimGlow, 0f, null, false);
                var tmp = AddText(sgo.transform, txt, new Vector3(0f, 0f, 0.55f), new Vector2(w * 0.92f, 7.5f), look.Accent * 1.6f, ShiftersFonts.Display, 6.5f);
                tmp.fontStyle = TMPro.FontStyles.Bold;
                Kit.BoxVisual(sgo.transform, "Strut", new Vector3(0f, -60f, 0f), new Vector3(2.5f, 110f, 2.5f), MatKey.MetalDark, 0.2f, null, false);
            }
            Kit.Theme = prev;
            return root;
        }

        /// <summary>Acid pool visual: hazard-striped rim, dark basin walls, glowing translucent acid surface, fumes.
        /// center = rim top center (floor level); size = (x, depth, z). The floor must leave a hole here. Visual only.</summary>
        public static GameObject AcidPool(Transform parent, Vector3 center, Vector3 size)
        {
            size = new Vector3(Mathf.Max(0.6f, size.x), Mathf.Max(0.3f, size.y), Mathf.Max(0.6f, size.z));
            var root = Kit.Make("AcidPool", parent, center);
            var t = root.transform;
            float rim = 0.25f;
            RimFrame(t, size, rim, MatKey.HazardStripes);
            BasinWalls(t, size, MatKey.MetalDark, size.y);
            EnvMeshes.Render(t, "Acid", ProcMesh.CachedBox(new Vector3(size.x, 0.12f, size.z), 0f), Kit.Mat(MatKey.Acid), new Vector3(0f, -0.3f, 0f), null, false);
            Kit.BoxVisual(t, "Bottom", new Vector3(0f, -size.y, 0f), new Vector3(size.x, 0.1f, size.z), MatKey.Void, 0f, null, false);
            var fumes = root.AddComponent<SteamPuffer>();
            fumes.LocalPoint = new Vector3(0f, -0.2f, 0f); fumes.Interval = 0.5f; fumes.Size = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.35f, 0.4f, 1.4f);
            fumes.Color = new Color(0.55f, 1f, 0.35f, 0.35f);
            return root;
        }

        /// <summary>Bottomless void pit visual: hazard rim, walls that sink into darkness, red warning light strip.
        /// center = rim top center (floor level); size = (x, visual depth, z). The floor must leave a hole. Visual only.</summary>
        public static GameObject VoidPit(Transform parent, Vector3 center, Vector3 size)
        {
            size = new Vector3(Mathf.Max(0.6f, size.x), Mathf.Max(2f, size.y), Mathf.Max(0.6f, size.z));
            var root = Kit.Make("VoidPit", parent, center);
            var t = root.transform;
            RimFrame(t, size, 0.3f, MatKey.HazardStripes);
            BasinWalls(t, new Vector3(size.x, 1.2f, size.z), MatKey.MetalDark, 1.2f);
            var deep = Kit.Make("Deep", t, new Vector3(0f, -1.2f, 0f));
            BasinWalls(deep.transform, new Vector3(size.x, size.y, size.z), MatKey.Void, size.y);
            var strip = EnvMeshes.Cached(EnvMeshes.Key("env_pit_strip", size.x, size.z), p =>
            {
                var c = Color.white; float y = -0.45f, w = 0.05f;
                p.Box(new Vector3(0, y, size.z * 0.5f - w), new Vector3(size.x - 0.1f, 0.06f, 0.04f), c, 0f);
                p.Box(new Vector3(0, y, -size.z * 0.5f + w), new Vector3(size.x - 0.1f, 0.06f, 0.04f), c, 0f);
                p.Box(new Vector3(size.x * 0.5f - w, y, 0), new Vector3(0.04f, 0.06f, size.z - 0.1f), c, 0f);
                p.Box(new Vector3(-size.x * 0.5f + w, y, 0), new Vector3(0.04f, 0.06f, size.z - 0.1f), c, 0f);
            });
            EnvMeshes.Render(t, "WarningStrip", strip, Kit.Mat(MatKey.LightRed), Vector3.zero, null, false);
            return root;
        }

        static void RimFrame(Transform t, Vector3 size, float rim, MatKey mat)
        {
            var key = EnvMeshes.Key("env_rim", size.x, size.z, rim);
            var mesh = EnvMeshes.Cached(key, p =>
            {
                var c = Color.white; float h = 0.06f, y = 0.03f - 0.025f;
                p.Box(new Vector3(0, y, size.z * 0.5f + rim * 0.5f), new Vector3(size.x + rim * 2f, h, rim), c, 0.01f);
                p.Box(new Vector3(0, y, -size.z * 0.5f - rim * 0.5f), new Vector3(size.x + rim * 2f, h, rim), c, 0.01f);
                p.Box(new Vector3(size.x * 0.5f + rim * 0.5f, y, 0), new Vector3(rim, h, size.z), c, 0.01f);
                p.Box(new Vector3(-size.x * 0.5f - rim * 0.5f, y, 0), new Vector3(rim, h, size.z), c, 0.01f);
            });
            EnvMeshes.Render(t, "Rim", mesh, Kit.Mat(mat), Vector3.zero, null, false);
        }

        static void BasinWalls(Transform t, Vector3 size, MatKey mat, float depth)
        {
            var key = EnvMeshes.Key("env_basin", size.x, size.z, depth);
            var mesh = EnvMeshes.Cached(key, p =>
            {
                var c = Color.white; const float th = 0.1f; float y = -depth * 0.5f;
                p.Box(new Vector3(0, y, size.z * 0.5f + th * 0.5f), new Vector3(size.x + th * 2f, depth, th), c, 0f);
                p.Box(new Vector3(0, y, -size.z * 0.5f - th * 0.5f), new Vector3(size.x + th * 2f, depth, th), c, 0f);
                p.Box(new Vector3(size.x * 0.5f + th * 0.5f, y, 0), new Vector3(th, depth, size.z), c, 0f);
                p.Box(new Vector3(-size.x * 0.5f - th * 0.5f, y, 0), new Vector3(th, depth, size.z), c, 0f);
            });
            EnvMeshes.Render(t, "Walls", mesh, Kit.Mat(mat), Vector3.zero, null, false);
        }
    }
}
