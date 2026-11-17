// =====================================================================================
//  SHIFTERS - L10Builder.cs   [SHIFT 09 - LIGHTS OUT, LevelIndex 9, scene L10_LightsOut]
//  The abandoned SHIFTERS INC. HQ at night (Act III opener, Spooky). Sections along +Z:
//    S0 Parking Garage (spawn, PARTY POPPER locker, lamp rows die)      z -10 ..  8   -> C1 (0,0,5.6)
//    S1 Cubicle Farm + Mailroom (STR) + Motion Hall (SPD) + IT Closet    z   8 .. 50
//       (IQ): three breakers -> ELEVATOR-LOBBY door = AND(1,2,3); two Team-Building Dummies
//    S2 Copy Room (atmosphere: copier flash, phone, paper avalanche)    z  50 .. 66   -> C2 (0,0,54)
//    S3 Executive Elevator lobby (call, 10 s stand, floodlight plate,  z  66 .. 83   -> C3 (0,0,70), EXIT
//       two impatient dummies, POWER RESTORED reveal)
//  Spec: docs/campaign/LEVELS2.md "SHIFT 09"; rules CAMPAIGN 10 (no Lost hazards, glow edges, <= 3 real pool lights).
//  Section files: L10Builder.S0S1.cs, L10Builder.S2S3.cs. Runtime logic: L10Director.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L10Builder : ILevelBuilder
    {
        public int LevelIndex => 9;

        public const float WallH = 4.6f;
        public const float FloorT = 0.6f;

        // ---- shared metrics (the solution and the director read these)
        public static readonly Vector3 SpawnPos = new Vector3(0f, 0f, -7f);
        public static readonly Vector3 DWArena = new Vector3(-8f, 0f, 30f);
        public const float DWRadius = 11f;
        public static readonly Vector3 DEArena = new Vector3(5f, 0f, 32f);
        public const float DERadius = 7f;
        public static readonly Vector3 DW2Arena = new Vector3(-14f, 0f, 74f);
        public static readonly Vector3 DE2Arena = new Vector3(14f, 0f, 74f);
        public const float S3Radius = 11f;
        public const float HallRoute = 28f;
        public const string CellId = "l10.trust";
        /// <summary>S3 intake: the copy-room TIME-OUT cage (the impatient dummies bring hugged interns here, ~20 m from the lobby).</summary>
        public const string CellS3Id = "l10.timeout";
        public static readonly Vector3 CellS3Pos = new Vector3(-5.6f, 0f, 52.8f);

        LevelBuildContext _ctx;
        L10Director _dir;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            var dgo = Kit.Make("L10Director", ctx.Gameplay, Vector3.zero);
            _dir = dgo.AddComponent<L10Director>();
            FlashlightRig.Ensure(ctx.Lights);

            BuildS0(ctx);
            BuildS1(ctx);
            BuildS2(ctx);
            BuildS3(ctx);
        }

        // ------------------------------------------------------------------ helpers
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Floor slab x0..x1, z0..z1, top at y (glowing trim edges in the Night theme).</summary>
        GameObject Floor(Transform p, float x0, float x1, float z0, float z1, float y = 0f, MatKey mat = MatKey.Floor, bool trim = true)
            => EnvKit.Floor(p, new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(x1 - x0, FloorT, z1 - z0), mat, trim);

        GameObject Wall(Transform p, float x0, float z0, float x1, float z1, float h = WallH, MatKey mat = MatKey.Wall)
            => EnvKit.Wall(p, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), h, 0.4f, mat, true);

        /// <summary>Wall along X at z from x0..x1 with gaps (pairs gx0,gx1 ascending).</summary>
        void WallX(Transform p, float z, float x0, float x1, params float[] gaps)
        {
            float a = x0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                if (gaps[i] - a > 0.05f) Wall(p, a, z, gaps[i], z);
                a = gaps[i + 1];
            }
            if (x1 - a > 0.05f) Wall(p, a, z, x1, z);
        }

        /// <summary>Wall along Z at x from z0..z1 with gaps (pairs gz0,gz1 ascending).</summary>
        void WallZ(Transform p, float x, float z0, float z1, params float[] gaps)
        {
            float a = z0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                if (gaps[i] - a > 0.05f) Wall(p, x, a, x, gaps[i]);
                a = gaps[i + 1];
            }
            if (z1 - a > 0.05f) Wall(p, x, a, x, z1);
        }

        /// <summary>Wall-mounted sign whose text is read by a viewer standing on the 'towardViewer' side.</summary>
        GameObject Sign(Transform p, Vector3 pos, Vector3 towardViewer, string text, float w, float h, SignStyle style = SignStyle.Info)
            => EnvKit.Sign(p, pos, EnvKit.WallMountFacing(towardViewer), text, new Vector2(w, h), style);

        GameObject Poster(Transform p, Vector3 pos, Vector3 towardViewer, string title, string sub, float w = 1.3f, float h = 1.7f)
            => EnvKit.Poster(p, pos, EnvKit.WallMountFacing(towardViewer), title, sub, new Vector2(w, h));

        /// <summary>Floor label lying flat, text up = +Z (readable walking north).</summary>
        void FloorLabel(Transform p, Vector3 pos, string text, float h, Color c, float yaw = 0f, float maxW = 6f)
            => _ctx.Label(p, pos + Vector3.up * 0.02f, Quaternion.Euler(90f, yaw, 0f), text, h, c, maxW);

        /// <summary>Ceiling-style emissive lamp panel hanging at y, glowing downward (no real light).</summary>
        GameObject HangingLamp(Transform p, Vector3 pos, Color c, float w = 1.8f)
        {
            var go = EnvKit.LightPanel(p, pos, EnvKit.ViewerLooking(Vector3.up, Vector3.forward), new Vector2(w, 0.35f), c, false);
            Kit.BoxVisual(go.transform, "Hanger", new Vector3(0f, 0f, 0.6f), new Vector3(0.05f, 0.05f, 1.1f), MatKey.MetalDark, 0f, null, false);
            return go;
        }

        /// <summary>Cubicles in a grid inside the rectangle; skips cells within 'clear' of any of the given points.</summary>
        void CubicleBlock(Transform p, float x0, float x1, float z0, float z1, float openYaw, int seed, Vector3[] keepClear = null, float clear = 3.4f)
        {
            const float cs = 2.5f;
            int nx = Mathf.Max(1, Mathf.FloorToInt((x1 - x0) / cs));
            int nz = Mathf.Max(1, Mathf.FloorToInt((z1 - z0) / cs));
            float ox = x0 + ((x1 - x0) - nx * cs) * 0.5f + cs * 0.5f;
            float oz = z0 + ((z1 - z0) - nz * cs) * 0.5f + cs * 0.5f;
            int k = seed;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    var c = new Vector3(ox + ix * cs, 0f, oz + iz * cs);
                    bool skip = false;
                    if (keepClear != null)
                        foreach (var q in keepClear)
                            if (new Vector2(c.x - q.x, c.z - q.z).magnitude < clear) { skip = true; break; }
                    if (skip) continue;
                    k = (k * 7 + 3) % 6;
                    bool lamp = (k == 1);
                    // the open side faces the nearest aisle (openYaw = direction a viewer standing in the aisle looks)
                    EnvKit.Decor.Night.Cubicle(p, c, Yaw(openYaw), 2.4f, 2.4f, 1.8f, k, true, lamp);
                }
        }

        static Color Teal => UIColors.Hex(0x4FE3D2);
        static Color Warm => UIColors.Hex(0xFFD58A);
        static Color Pink => UIColors.Hex(0xFF7AC8);
        static Color Danger => UIColors.Hex(0xFF5A6A);
    }
}
