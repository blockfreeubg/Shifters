// =====================================================================================
//  SHIFTERS - L3Builder.cs   [LEVEL 3 - SACRIFICE ZONE, LevelIndex 2, scene L3_SacrificeZone]
//  An abandoned lab floating in a sickly green void. Sections along +Z:
//    S0 Decontamination (spawn, bot tutorial)            z  -8 ..   9
//    S1 Bot Hall (1 bot, taunt gallery, cell, catwalk)   z   9 ..  47   -> C1
//    S2 Timed Lab Door (27 m acid walkway, override)     z  47 ..  97   -> C2
//    S3 The Volunteer Chair (sacrifice + tunnel)         z  97 .. 135   -> C3
//    S4 Distraction Gauntlet (2 bots, 100 kg x 3 s)      z 135 .. 190   -> EXIT
//  Bot arenas are circles; every safe zone (entries, stairs, landings, holding cells) lies outside
//  them, and every "high ground" (galleries, taunt prows, pipe catwalk) is > 1.3 m above the bot floor.
//  Section files: L3Builder.S0S1.cs, L3Builder.S2S3.cs, L3Builder.S4.cs.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L3Builder : ILevelBuilder
    {
        public int LevelIndex => 2;

        public const float WallH = 4.6f;
        public const float FloorT = 0.6f;

        // shared metrics used by the solution (keep in sync with the layout)
        public static readonly Vector3 S1ArenaCenter = new Vector3(0f, 0f, 31f);
        public const float S1ArenaRadius = 11f;
        public static readonly Vector3 S3ArenaCenter = new Vector3(-14f, 0f, 125f);
        public const float S3ArenaRadius = 5.5f;
        public static readonly Vector3 S4ArenaCenter = new Vector3(0f, 0f, 162f);
        public const float S4ArenaRadius = 8f;   // bots grab within r + 0.5: the whole 100 kg plate (9.1 m+ out) is safe - lure to CROSS, then hold
        public const float S2RouteLength = 27f;
        public const float S3RouteLength = 21f;

        LevelBuildContext _ctx;
        int _lights;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            _lights = 0;
            BuildS0(ctx);
            BuildS1(ctx);
            BuildS2(ctx);
            BuildS3(ctx);
            BuildS4(ctx);
            EnvKit.VoidBackdrop(ctx.Decor, LevelTheme.Lab, new Bounds(new Vector3(0f, 0f, 90f), new Vector3(50f, 20f, 210f)), 33);
        }

        // ------------------------------------------------------------------ helpers
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);

        /// <summary>Floor slab from x0..x1, z0..z1 with its top at y.</summary>
        GameObject Floor(Transform p, float x0, float x1, float z0, float z1, float y = 0f, MatKey mat = MatKey.Floor, float thick = FloorT, bool trim = true)
            => EnvKit.Floor(p, new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(x1 - x0, thick, z1 - z0), mat, trim);

        /// <summary>Solid raised block (gallery, pulpit, catwalk): top at 'top', bottom at 'bottom'.</summary>
        GameObject Block(Transform p, float x0, float x1, float z0, float z1, float top, float bottom = 0f, MatKey mat = MatKey.FloorAlt)
            => EnvKit.Floor(p, new Vector3((x0 + x1) * 0.5f, top, (z0 + z1) * 0.5f), new Vector3(x1 - x0, top - bottom, z1 - z0), mat, true);

        GameObject Wall(Transform p, float x0, float z0, float x1, float z1, float h = WallH, float y = 0f, MatKey mat = MatKey.Wall)
            => EnvKit.Wall(p, new Vector3(x0, y, z0), new Vector3(x1, y, z1), h, 0.4f, mat, true);

        /// <summary>Wall across X at z with a gap [gx0, gx1].</summary>
        void WallWithGap(Transform p, float z, float x0, float x1, float gx0, float gx1, float h = WallH)
        {
            if (gx0 - x0 > 0.05f) Wall(p, x0, z, gx0, z, h);
            if (x1 - gx1 > 0.05f) Wall(p, gx1, z, x1, z, h);
        }

        // L3's mounted pieces take 'yaw' = the direction the readable / lit FACE points (towards the player). EnvKit's
        // mounted-piece convention (Sign, Poster, LightPanel) takes the direction the VIEWER looks (into the wall), hence
        // MountYaw = yaw + 180. (Without it every L3 sign, poster and light panel showed its blank back to the player.)
        static Quaternion MountYaw(float faceYaw) => Yaw(faceYaw + 180f);

        /// <summary>Sign whose text faces along 'yaw' (e.g. -90 on an east wall: readable from the west).</summary>
        GameObject Sign(Transform p, Vector3 pos, float yaw, string text, float w, float h, SignStyle style = SignStyle.Info)
            => EnvKit.Sign(p, pos, MountYaw(yaw), text, new Vector2(w, h), style);

        /// <summary>Poster whose print faces along 'yaw'.</summary>
        GameObject Poster(Transform p, Vector3 pos, float yaw, string title, string sub)
            => EnvKit.Poster(p, pos, MountYaw(yaw), title, sub, new Vector2(1.2f, 1.6f));

        /// <summary>Emissive light panel on a wall, lit face along 'yaw'. Real point light only while the budget allows (max 6).</summary>
        GameObject Panel(Transform p, Vector3 pos, float yaw, Color c, bool flicker = false, bool wantLight = false, float intensity = 2.2f, float range = 12f)
        {
            bool light = wantLight && _lights < 6;
            if (light) _lights++;
            var go = EnvKit.LightPanel(p, pos, MountYaw(yaw), new Vector2(1.6f, 0.4f), c, light, intensity, range);
            if (flicker) EnvKit.AddFlicker(go, 0.6f);
            return go;
        }

        /// <summary>KillZone acid (top below the floor) + the EnvKit acid pool visual over x0..x1, z0..z1.</summary>
        void AcidPit(Transform p, float x0, float x1, float z0, float z1, float depth = 2.5f)
        {
            var c = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            EnvKit.AcidPool(p, c, new Vector3(x1 - x0, depth, z1 - z0));
            PuzzleKit.KillZone(_ctx.Hazards, c + Vector3.down * 0.55f, null, new Vector3(x1 - x0, 6f, z1 - z0), KillZoneType.Acid, null, "dissolved in acid");
        }

        /// <summary>Taunt pulpit shaped as a PROW: a 45-degree-rotated square (top 2 m, half-diagonal 2.1 m) whose tip points along
        /// +x*dir at 'tip' and whose back half merges into the gallery. Returns the lure stand point (0.5 m behind the tip).
        /// Why a prow: a BONK-BOT waiting below a high target parks ~1.3-1.75 m from it and needs line of sight over the
        /// ledge edge. Behind a tip the lure is within ~0.5 m of the edge in every frontal direction, so up to three bots can
        /// park around the tip, stand still and keep watching (measured with L3Diag: lure lost 0-2% of the time). A straight
        /// face (or a curb that keeps bots away) leaves the second bot pushing at a corner, where its steering detours keep
        /// turning the lure out of view - and any crosser in view gets grabbed.</summary>
        Vector3 LureProw(Transform g, Transform decor, Vector3 tip, float dir, string label)
        {
            const float hd = 2.1f;
            float side = hd * Mathf.Sqrt(2f);
            var c = new Vector3(tip.x - dir * hd, 1f, tip.z);
            var rot = Quaternion.Euler(0f, 45f, 0f);
            Kit.SolidBox(g, "LureProw", c, new Vector3(side, 2f, side), MatKey.PuzzleBody, 0.03f, rot);
            // hazard stripes along both front edges (just inside the top edge)
            for (int s = -1; s <= 1; s += 2)
            {
                var a = new Vector3(tip.x, 2.006f, tip.z);
                var b = new Vector3(tip.x - dir * hd, 2.006f, tip.z + s * hd);
                Vector3 inward = new Vector3(-dir, 0f, -s).normalized * 0.12f;
                Kit.BoxVisual(decor, "ProwEdgeStripe", (a + b) * 0.5f + inward, new Vector3(0.24f, 0.012f, side - 0.25f),
                              MatKey.HazardStripes, 0.01f, Quaternion.LookRotation(b - a), false);
            }
            var stand = new Vector3(tip.x - dir * 0.5f, 2f, tip.z);
            PuzzleVisuals.Cyl(decor, "TauntSpot", stand + Vector3.up * 0.008f, 0.36f, 0.016f, MatKey.HazardYellow, null, 20, false);
            var lureLabel = _ctx.Label(decor, new Vector3(tip.x - dir * 1.25f, 2.02f, tip.z), Quaternion.Euler(90f, dir > 0f ? 90f : -90f, 0f), InputPrompts.Format(label, false, false), 0.3f, UIColors.AccentYellow, 1.9f);
            if (label.IndexOf('{') >= 0) DevicePromptText.Attach(lureLabel, label);   // gamepad prompts: taunt key follows the device, live
            return stand;
        }

        /// <summary>Gallery railing along x = edgeX from z0 to z1, leaving the gap where a LureProw (tip, dir) meets the gallery.</summary>
        void RailingAroundProw(Transform g, float edgeX, float z0, float z1, Vector3 tip, float dir)
        {
            float half = 2.1f - Mathf.Abs(edgeX - (tip.x - dir * 2.1f)) + 0.02f; // prow half-width where it crosses the gallery edge
            EnvKit.Railing(g, new Vector3(edgeX, 2f, z0), new Vector3(edgeX, 2f, tip.z - half));
            EnvKit.Railing(g, new Vector3(edgeX, 2f, tip.z + half), new Vector3(edgeX, 2f, z1));
        }

        static Color LabGreen => UIColors.Hex(0x9BFF6A);
        static Color LabWhite => UIColors.Hex(0xE6FFE9);
        static Color AlarmRed => UIColors.Hex(0xFF4D5E);
    }
}
