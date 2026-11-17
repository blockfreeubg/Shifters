// =====================================================================================
//  SHIFTERS - L4Decor.cs   [owned by LEVEL 4]
//  Small edit-mode-safe helpers for "THE IQ TEST": exam symbols (triangle/circle/square/star),
//  symbol wall panels, hologram walkways with filled corners, floors with a plate recess,
//  room walls and Research-theme decor clusters. Everything is built inside the call
//  (cached meshes, MaterialLibrary materials), so it works in DevPlay and in the scene builder.
// =====================================================================================
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Shifters
{
    /// <summary>Exam symbols used by the Switch Sequence room.</summary>
    public enum L4Symbol { Triangle = 0, Circle = 1, Square = 2, Star = 3 }

    /// <summary>Level 4 construction helpers (symbols, hologram walkways, recessed plates, walls, decor).</summary>
    public static class L4Decor
    {
        /// <summary>Signature colours of the four exam symbols (readable for everyone).</summary>
        public static Color SymbolColor(L4Symbol s)
        {
            switch (s)
            {
                case L4Symbol.Triangle: return UIColors.Hex(0xFF8A3D);
                case L4Symbol.Circle: return UIColors.Hex(0x3DE0FF);
                case L4Symbol.Square: return UIColors.Hex(0x3DDC97);
                default: return UIColors.Hex(0xFFC93C);
            }
        }

        public static string SymbolName(L4Symbol s)
        {
            switch (s)
            {
                case L4Symbol.Triangle: return "TRIANGLE";
                case L4Symbol.Circle: return "CIRCLE";
                case L4Symbol.Square: return "SQUARE";
                default: return "STAR";
            }
        }

        // ------------------------------------------------------------------ symbol meshes
        /// <summary>Flat extruded symbol in the local XY plane (thickness along Z, centred), 'size' = outer diameter.</summary>
        public static Mesh SymbolMesh(L4Symbol s, float size, float thickness = 0.08f)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "l4_symbol_{0}_{1:0.##}_{2:0.###}", (int)s, size, thickness);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float r = size * 0.5f;
                var outline = new List<Vector2>();
                switch (s)
                {
                    case L4Symbol.Triangle:
                        for (int i = 0; i < 3; i++) { float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f; outline.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a) - 0.15f) * r); }
                        break;
                    case L4Symbol.Circle:
                        for (int i = 0; i < 28; i++) { float a = i * Mathf.PI * 2f / 28f; outline.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.9f); }
                        break;
                    case L4Symbol.Square:
                        float q = r * 0.75f;
                        outline.Add(new Vector2(q, q)); outline.Add(new Vector2(-q, q)); outline.Add(new Vector2(-q, -q)); outline.Add(new Vector2(q, -q));
                        break;
                    default:
                        for (int i = 0; i < 10; i++) { float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f; float rr = i % 2 == 0 ? r : r * 0.42f; outline.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr); }
                        break;
                }
                Prism(b, outline, thickness, Color.white);
                return b.ToMesh(key);
            });
        }

        /// <summary>Star-shaped (w.r.t. origin) polygon prism: fan front/back faces + side quads.</summary>
        static void Prism(ProcMesh.Builder b, List<Vector2> o, float t, Color c)
        {
            float h = t * 0.5f;
            int n = o.Count;
            var front = new Vector3(0f, 0f, -h);
            var back = new Vector3(0f, 0f, h);
            for (int i = 0; i < n; i++)
            {
                var p = o[i]; var q = o[(i + 1) % n];
                var pf = new Vector3(p.x, p.y, -h); var qf = new Vector3(q.x, q.y, -h);
                var pb = new Vector3(p.x, p.y, h); var qb = new Vector3(q.x, q.y, h);
                b.Tri(front, pf, qf, c, Vector3.back);
                b.Tri(back, pb, qb, c, Vector3.forward);
                var mid = (p + q) * 0.5f;
                b.Quad(pf, qf, qb, pb, c, new Vector3(mid.x, mid.y, 0f));
            }
        }

        /// <summary>Wall-mounted exam symbol panel (dark backing, glowing frame, big coloured symbol). The face points along
        /// rot * forward (towards the viewer). pos = panel centre.</summary>
        public static GameObject SymbolPanel(Transform parent, Vector3 pos, Quaternion rot, L4Symbol s, float size = 1.5f)
        {
            var root = Kit.Make("SymbolPanel_" + SymbolName(s), parent, pos, rot);
            var t = root.transform;
            var col = SymbolColor(s);
            Kit.BoxVisual(t, "Backing", Vector3.zero, new Vector3(size + 0.3f, size + 0.3f, 0.12f), MatKey.MetalDark, 0.03f);
            var frameMat = MaterialLibrary.Colored(col, 0.2f, 1.1f);
            float e = size * 0.5f + 0.1f;
            ProcMesh.Part("FrameT", t, ProcMesh.CachedBox(new Vector3(size + 0.3f, 0.07f, 0.05f), 0.01f), frameMat, new Vector3(0f, e + 0.04f, 0.07f), null, null, false);
            ProcMesh.Part("FrameB", t, ProcMesh.CachedBox(new Vector3(size + 0.3f, 0.07f, 0.05f), 0.01f), frameMat, new Vector3(0f, -e - 0.04f, 0.07f), null, null, false);
            ProcMesh.Part("FrameL", t, ProcMesh.CachedBox(new Vector3(0.07f, size + 0.3f, 0.05f), 0.01f), frameMat, new Vector3(-e - 0.04f, 0f, 0.07f), null, null, false);
            ProcMesh.Part("FrameR", t, ProcMesh.CachedBox(new Vector3(0.07f, size + 0.3f, 0.05f), 0.01f), frameMat, new Vector3(e + 0.04f, 0f, 0.07f), null, null, false);
            ProcMesh.Part("Symbol", t, SymbolMesh(s, size * 0.82f, 0.08f), MaterialLibrary.Colored(col, 0.3f, 0.9f), new Vector3(0f, 0f, 0.1f), null, null, false);
            return root;
        }

        // ------------------------------------------------------------------ hologram walkway
        /// <summary>Hologram walkway along axis-aligned (or any) corner points (LOCAL top-surface centreline). Unlike
        /// PerspectiveKit.HologramPath it fills every corner with a square tile so nobody falls through the outer corner.
        /// Tiles are linked as neighbours in walking order (footprints light up the tiles around the Brainiac).</summary>
        public static HiddenPathTile[] HoloWalkway(Transform parent, Vector3[] corners, float width = 2f, float tileLength = 2f)
        {
            var root = Kit.Make("HologramWalkway", parent, Vector3.zero);
            var tiles = new List<HiddenPathTile>();
            float th = PerspectiveKit.TileThickness;
            int n = corners.Length;
            for (int s = 0; s < n - 1; s++)
            {
                Vector3 a = corners[s], b = corners[s + 1];
                Vector3 d = b - a; d.y = 0f;
                float len = d.magnitude;
                if (len < 0.05f) continue;
                Vector3 dir = d / len;
                float s0 = s > 0 ? width * 0.5f : 0f;
                float e0 = s + 1 < n - 1 ? width * 0.5f : 0f;
                float usable = len - s0 - e0;
                if (usable > 0.2f)
                {
                    int k = Mathf.Max(1, Mathf.RoundToInt(usable / tileLength));
                    float step = usable / k;
                    var rot = Quaternion.LookRotation(dir, Vector3.up);
                    for (int i = 0; i < k; i++)
                    {
                        Vector3 top = a + dir * (s0 + (i + 0.5f) * step);
                        top.y = Mathf.Lerp(a.y, b.y, (s0 + (i + 0.5f) * step) / len);
                        var tile = PerspectiveKit.HologramTile(root.transform, top + Vector3.down * (th * 0.5f), new Vector3(width, th, step), rot);
                        tile.name = "HoloTile_" + tiles.Count;
                        tiles.Add(tile);
                    }
                }
                if (s + 1 < n - 1)
                {
                    var c = PerspectiveKit.HologramTile(root.transform, b + Vector3.down * (th * 0.5f), new Vector3(width, th, width), Quaternion.LookRotation(dir, Vector3.up));
                    c.name = "HoloCorner_" + tiles.Count;
                    tiles.Add(c);
                }
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                var nb = new List<HiddenPathTile>(2);
                if (i > 0) nb.Add(tiles[i - 1]);
                if (i < tiles.Count - 1) nb.Add(tiles[i + 1]);
                tiles[i].Neighbours = nb.ToArray();
            }
            if (tiles.Count > 0 && tiles[0].Reveal != null) tiles[0].Reveal.Label = "HIDDEN PATH - FOLLOW ME";
            return tiles.ToArray();
        }

        // ------------------------------------------------------------------ floors / walls
        /// <summary>Straight wall (EnvKit.Wall) between two floor points.</summary>
        public static GameObject Wall(Transform parent, float x0, float z0, float x1, float z1, float height = 5f, MatKey mat = MatKey.Wall, float thickness = 0.5f)
            => EnvKit.Wall(parent, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), height, thickness, mat, true);

        /// <summary>Wall along X at z with a doorway gap [gapX0, gapX1] (the door frame fills the gap).</summary>
        public static void WallWithGap(Transform parent, float z, float x0, float x1, float gapX0, float gapX1, float height = 5f, MatKey mat = MatKey.Wall)
        {
            if (gapX0 - x0 > 0.1f) Wall(parent, x0, z, gapX0, z, height, mat);
            if (x1 - gapX1 > 0.1f) Wall(parent, gapX1, z, x1, z, height, mat);
        }

        // ------------------------------------------------------------------ decor
        /// <summary>Ring of slowly spinning geometric shapes floating in the void (Research theme signature).</summary>
        public static void FloatingCluster(Transform parent, Vector3 center, float radius, int count, int seed, float minSize = 0.9f, float maxSize = 2.4f)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = radius * (0.6f + 0.4f * (float)rng.NextDouble());
                var p = center + new Vector3(Mathf.Cos(a) * r, ((float)rng.NextDouble() - 0.3f) * 8f, Mathf.Sin(a) * r);
                EnvKit.Decor.FloatingShape(parent, p, Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble()), rng.Next(4));
            }
        }

        /// <summary>EnvKit.Decor.BrainJar mounts its "IQ 9000" plate with the readable face pointing INTO the jar (the text
        /// ends up buried in the base). Turns the plate outward (wherever the kit put it) and spins the jar about Y so the
        /// plate faces the world direction 'towards' (XZ). The jar is rotationally symmetric, so nothing else changes.</summary>
        public static GameObject FaceBrainJarPlate(GameObject jar, Vector3 towards)
        {
            if (jar == null) return null;
            Transform plate = null;
            foreach (Transform c in jar.transform)
                if (c.GetComponentInChildren<TMPro.TextMeshPro>(true) != null) { plate = c; break; }
            var outward = plate != null ? new Vector3(plate.localPosition.x, 0f, plate.localPosition.z) : Vector3.zero;
            towards.y = 0f;
            if (outward.sqrMagnitude < 1e-4f || towards.sqrMagnitude < 1e-4f) return jar;
            outward.Normalize();
            plate.localRotation = Quaternion.LookRotation(outward, Vector3.up);   // EnvKit sign roots read along their +Z
            jar.transform.rotation = Quaternion.LookRotation(towards.normalized, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(outward, Vector3.up));
            return jar;
        }

        /// <summary>IQ-only floor writing (PerspectiveKit.Clue lying on the floor, readable walking along +Z rotated by yaw).</summary>
        public static ClueMarker FloorClue(Transform parent, Vector3 floorPos, float yaw, string text, float size = 0.5f)
            => PerspectiveKit.Clue(parent, floorPos + Vector3.up * 0.04f, Quaternion.Euler(90f, yaw, 0f), text, size);

        /// <summary>Visible floor lettering (TMP lying on the floor, readable walking along +Z rotated by yaw).</summary>
        public static void FloorText(LevelBuildContext ctx, Transform parent, Vector3 floorPos, float yaw, string text, float height, Color color)
            => ctx.Label(parent, floorPos + Vector3.up * 0.03f, Quaternion.Euler(90f, yaw, 0f), text, height, color, 14f);
    }
}
