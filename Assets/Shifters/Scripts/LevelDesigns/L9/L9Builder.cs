// =====================================================================================
//  SHIFTERS - L9Builder.cs   [SHIFT 08 - THE GETAWAY: BOSS #2 PORKCHOP, LevelIndex 8, scene L9_TheGetaway]
//  The coin-sorting plant under the vault. Alarms. Max: "PORKCHOP! FETCH!"  Layout along +Z (world = level space):
//    S0  TRAM PLATFORM      x -8..8, z -10..2 (+ boarding wings beside the docked tram z 2..9)   spawn, C1
//    TRACK                  tram deck (4 x 7) on the coin river, deck-top points z 5.5 -> 30 -> 59 -> 87 -> 118
//    ST1 ROADBLOCK          stop z 30; VAULT BRICKS wall z 35; side platform x -6..-2, z 28..36      (STR / GUN)
//    ST2 TOLL GATE          stop z 59; TOLL gate z 64; deck CONSOLE (IQ); station platform + C2      (IQ)
//    ST3 DRAWBRIDGE         stop z 87; gorge z 92..102; PYLON x 7.6..10.6 z 86..89 +0.5; C3           (SPD / bridge)
//    DOCK                   track end z 118; loading-dock wings x +-(2..8)
//    ARENA                  x -15..15, z 122..152, 6 m walls, 4 pillars, 4 loading bays (C4 SW, C5 SE, C6 NE) -> exit z 153
//  Section files: L9Builder.Chase.cs (S0 + track + stations), L9Builder.Arena.cs (Porkchop arena, van, exit).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L9Builder : ILevelBuilder
    {
        public int LevelIndex => 8;

        public const float WallH = 8f, ArenaWallH = 6f, RiverY = -2.4f, BedTop = -0.8f;
        // track (deck-top centre points) and stops
        public const float DockZ = 5.5f, St1Z = 30f, St2Z = 59f, St3Z = 87f, EndZ = 118f;
        public static readonly Vector3 ArenaCenter = new Vector3(0f, 0f, 137f);
        public static readonly Vector3[] PillarPos = { new Vector3(-7f, 0f, 130f), new Vector3(7f, 0f, 130f), new Vector3(7f, 0f, 144f), new Vector3(-7f, 0f, 144f) };
        /// <summary>Bays SW, SE, NE, NW (centres, top 1.6 m).</summary>
        public static readonly Vector3[] BayPos = { new Vector3(-11.5f, 1.6f, 124.5f), new Vector3(11.5f, 1.6f, 124.5f), new Vector3(11.5f, 1.6f, 149.5f), new Vector3(-11.5f, 1.6f, 149.5f) };
        public const float BayTop = 1.6f;

        LevelBuildContext _ctx;
        int _lights;
        ChaseTrack _track;
        Transform _deck;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            _lights = 0;
            BuildHall(ctx);
            BuildS0(ctx);
            BuildTrack(ctx);
            BuildArena(ctx);
        }

        // ------------------------------------------------------------------ helpers
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);
        /// <summary>Rotation for a piece read by a viewer looking along 'look' (EnvKit convention).</summary>
        static Quaternion Look(Vector3 look) => EnvKit.ViewerLooking(look);

        GameObject Floor(Transform p, float x0, float x1, float z0, float z1, float y = 0f, MatKey mat = MatKey.Floor, float thick = 0.6f, bool trim = true)
            => EnvKit.Floor(p, new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(x1 - x0, thick, z1 - z0), mat, trim);

        /// <summary>Station platform: a floor slab on a concrete pier down into the coin river.</summary>
        GameObject Pier(Transform p, float x0, float x1, float z0, float z1, float top = 0f, MatKey mat = MatKey.Floor)
        {
            var f = Floor(p, x0, x1, z0, z1, top, mat);
            Kit.BoxVisual(_ctx.Decor, "Pier", new Vector3((x0 + x1) * 0.5f, (top - 0.6f + RiverY - 0.2f) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0 - 0.3f, top - 0.6f - RiverY + 0.2f, z1 - z0 - 0.3f), MatKey.Concrete, 0.04f);
            return f;
        }

        GameObject Wall(Transform p, float x0, float z0, float x1, float z1, float h, float y = 0f, MatKey mat = MatKey.Wall)
            => EnvKit.Wall(p, new Vector3(x0, y, z0), new Vector3(x1, y, z1), h, 0.4f, mat, true);

        GameObject Sign(Transform p, Vector3 pos, Vector3 look, string text, float w, float h, SignStyle style = SignStyle.Info)
            => EnvKit.Sign(p, pos, Look(look), text, new Vector2(w, h), style);

        /// <summary>Sign on a post (post from the floor at y0 up to the sign).</summary>
        GameObject PostSign(Transform p, Vector3 pos, Vector3 look, string text, float w, float h, float y0 = 0f, SignStyle style = SignStyle.Info)
        {
            Kit.BoxVisual(_ctx.Decor, "SignPost", new Vector3(pos.x, (y0 + pos.y - h * 0.5f) * 0.5f, pos.z) + look.normalized * 0.08f,
                new Vector3(0.12f, Mathf.Max(0.1f, pos.y - h * 0.5f - y0), 0.12f), MatKey.MetalDark, 0.02f);
            return Sign(p, pos, look, text, w, h, style);
        }

        GameObject Panel(Transform p, Vector3 pos, Vector3 look, Color c, bool wantLight = false, float intensity = 2.4f, float range = 14f)
        {
            bool light = wantLight && _lights < 3;
            if (light) _lights++;
            return EnvKit.LightPanel(p, pos, Look(look), new Vector2(1.8f, 0.45f), c, light, intensity, range);
        }

        /// <summary>Perspective-only label (STR / SPD / IQ / GUN view) on an empty anchor.</summary>
        GameObject Reveal(Transform p, Vector3 pos, PerspectiveMask mask, string text)
        {
            var go = Kit.Make("Reveal_" + mask, p, pos);
            PerspectiveKit.Reveal(go, mask, text, false, true, Vector3.zero);
            return go;
        }

        static Color Gold => UIColors.Hex(0xFFD25A);
        static Color AlarmRed => UIColors.Hex(0xFF4D5E);
        static Color VaultTeal => UIColors.Hex(0x7FE7D9);
        static Color Ink => UIColors.Hex(0x1A1A1A);
    }
}
