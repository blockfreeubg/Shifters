// =====================================================================================
//  SHIFTERS - L12Builder.cs   [SHIFT 11 - UNMASKED: BOSS #3 THE BOOGEYMAN, LevelIndex 11, scene L12_Unmasked]
//  The abandoned SHIFTERS INC. HQ atrium at 3 AM (spec: LEVELS2.md "SHIFT 11"). Level space = world space:
//    S0 THE CRYPT STAIRS   x -3..3,   z -10..0, floor y 2.5   spawn, PARTY POPPER locker
//    BALCONY (C1)          x -6..6,   z 0..5,   deck y 2.5    intro trigger, release lever, statue, W/E stairs
//    ARENA THE ATRIUM      x -15..15, z 0..30,  floor y 0     broom closet, break areas (C2 on BA-E), crank / race / fuse,
//                                                            P2 pads + loop
//    S1n BREAKER ALCOVE    x 15..19,  z 25..29                laser curtain + MAIN BREAKER
//    S3 FRONT LOBBY        x -5..5,   z 30..38                exit, Percy's van outside
//  Night rules (G10): no KillZone anywhere, glow tape on walkable edges, falls <= 2.5 m (balcony ends / stairs are railed).
//  Section files: L12Builder.cs (shared + S0 + balcony + lobby), L12Builder.Arena.cs (atrium, stations, boss, decor).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L12Builder : ILevelBuilder
    {
        public int LevelIndex => 11;

        // ---- shared metrics (solution / checks read these)
        public const float DeckY = 2.5f;
        public const float WallH = 6f, SouthWallH = 9f;
        public const float RaceRoute = 28f;
        public static readonly Vector3 RaceStand = new Vector3(-12.5f, 0f, 27f);
        public static readonly Vector3 LeverStand = new Vector3(17.5f, 0f, 27f);
        public static readonly Vector3 CrankStand = new Vector3(-13.3f, 0f, 15f);
        public static readonly Vector3 FuseStand = new Vector3(13.3f, 0f, 15f);

        static readonly Color NightBlue = new Color(0.55f, 0.62f, 0.95f);
        static readonly Color Purple = new Color(0.72f, 0.5f, 1f);
        static readonly Color Warm = new Color(1f, 0.9f, 0.66f);

        LevelBuildContext _ctx;
        L12BoogeymanBoss _boss;
        L12AtriumLights _lights;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            BuildS0(ctx);
            BuildBalcony(ctx);
            BuildArena(ctx);       // boss + stations (L12Builder.Arena.cs)
            BuildLobby(ctx);
            BuildUpperStoreys(ctx);
            BuildTutorialsAndHints(ctx);
            FlashlightRig.Ensure(ctx.Lights);
        }

        // ------------------------------------------------------------------ helpers
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);
        /// <summary>EnvKit convention: rot = the viewer's look direction.</summary>
        static Quaternion Look(Vector3 dir) => EnvKit.ViewerLooking(dir);
        static Quaternion FloorText(float yaw = 0f) => Quaternion.Euler(90f, yaw, 0f);

        BoxCollider Slab(Transform p, float x0, float x1, float z0, float z1, float top, float thick, MatKey mat, bool trim, string name)
            => _ctx.Platform(p, new Vector3((x0 + x1) * 0.5f, top, (z0 + z1) * 0.5f), new Vector3(x1 - x0, thick, z1 - z0), mat, trim, name);

        static void WallX(Transform p, float x0, float x1, float z, float h, float y = 0f, MatKey mat = MatKey.Wall)
            => EnvKit.Wall(p, new Vector3(x0, y, z), new Vector3(x1, y, z), h, 0.4f, mat, true);

        static void WallZ(Transform p, float x, float z0, float z1, float h, float y = 0f, MatKey mat = MatKey.WallAlt)
            => EnvKit.Wall(p, new Vector3(x, y, z0), new Vector3(x, y, z1), h, 0.4f, mat, true);

        static void GlowX(Transform p, float x0, float x1, float z, float y)
            => Kit.BoxVisual(p, "GlowTape", new Vector3((x0 + x1) * 0.5f, y + 0.012f, z), new Vector3(x1 - x0, 0.025f, 0.12f), MatKey.TrimGlow, 0f, null, false);

        static void GlowZ(Transform p, float x, float z0, float z1, float y)
            => Kit.BoxVisual(p, "GlowTape", new Vector3(x, y + 0.012f, (z0 + z1) * 0.5f), new Vector3(0.12f, 0.025f, z1 - z0), MatKey.TrimGlow, 0f, null, false);

        static void SlopeGlow(Transform p, Vector3 a, Vector3 b)
        {
            var d = b - a;
            Kit.BoxVisual(p, "GlowTapeSlope", (a + b) * 0.5f + Vector3.up * 0.03f, new Vector3(0.12f, 0.025f, d.magnitude), MatKey.TrimGlow, 0f, Quaternion.LookRotation(d.normalized), false);
        }

        static GameObject Sign(Transform p, Vector3 pos, Vector3 viewerLook, string text, float w, float h, SignStyle style = SignStyle.Info)
            => EnvKit.Sign(p, pos, Look(viewerLook), text, new Vector2(w, h), style);

        static MeshRenderer Holo(Transform p, string name, Vector3 center, Vector3 size, Color c, Quaternion? rot = null)
        {
            var r = Kit.BoxVisual(p, name, center, size, MatKey.Hologram, 0f, rot, false);
            if (r != null) r.sharedMaterial = MaterialLibrary.Hologram(c);
            return r;
        }

        Transform VolumeKey(Transform p, string name, Vector3 center) => Kit.Make("LitVolume_" + name, p, center).transform;

        // ================================================================== S0 THE CRYPT STAIRS
        void BuildS0(LevelBuildContext ctx)
        {
            var s0 = ctx.Section("S0_CryptStairs", Vector3.zero);
            Slab(s0, -3f, 3f, -10f, 0f, DeckY, DeckY + 0.6f, MatKey.Concrete, false, "S0Floor");
            WallZ(s0, -3.2f, -10.4f, -0.4f, 4.5f, DeckY);
            WallZ(s0, 3.2f, -10.4f, -0.4f, 4.5f, DeckY);
            // the crypt tunnel behind the spawn: decorative steps down into the dark (an invisible wall keeps everyone up here)
            Kit.SolidBox(s0, "TunnelStop", new Vector3(0f, DeckY + 2f, -10.25f), new Vector3(6f, 4f, 0.3f), MatKey.Black, 0f);
            var stopR = s0.Find("TunnelStop"); if (stopR != null) { var mr = stopR.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false; }
            for (int i = 0; i < 5; i++)
                Kit.BoxVisual(s0, "CryptStep" + i, new Vector3(0f, DeckY - 0.2f - i * 0.4f, -10.6f - i * 0.5f), new Vector3(5.2f, 0.4f, 0.5f), MatKey.Concrete, 0.02f);
            Kit.BoxVisual(s0, "TunnelDark", new Vector3(0f, DeckY + 1.5f, -13.4f), new Vector3(6f, 7f, 0.3f), MatKey.Black, 0f, null, false);
            Kit.BoxVisual(s0, "TunnelRoof", new Vector3(0f, DeckY + 4.6f, -11.8f), new Vector3(6.4f, 0.3f, 3.6f), MatKey.Concrete, 0.02f);
            Kit.BoxVisual(s0, "S0Roof", new Vector3(0f, DeckY + 4.65f, -5.2f), new Vector3(6.8f, 0.3f, 10.4f), MatKey.Ceiling, 0.02f, null, false);
            EnvKit.Decor.Night.Cobweb(s0, new Vector3(-3f, DeckY + 4.4f, -9.9f), null, 1.2f, false);
            EnvKit.Decor.Night.Cobweb(s0, new Vector3(3f, DeckY + 4.4f, -9.9f), null, 1.2f, true);
            // emergency strip lights (emissive; the box is a LightRegistry volume)
            for (int s = -1; s <= 1; s += 2)
                Kit.BoxVisual(s0, "EmergencyStrip", new Vector3(s * 2.95f, DeckY + 3.6f, -5f), new Vector3(0.08f, 0.1f, 8.5f), MatKey.LightRed, 0f, null, false);

            ctx.SetSpawn(new Vector3(0f, DeckY, -7f), 0f);
            Sign(s0, new Vector3(-1.6f, DeckY + 3.2f, -10.05f), Vector3.back, "CRYPT TUNNEL\n<size=70%>NO RETURNS, NO REFUNDS</size>", 2.6f, 1.0f, SignStyle.Warning);

            // TOOL LOCKER: PARTY POPPER (G11: the popper moves no props or interns -> no NO-TOOL zones)
            var lockerPos = new Vector3(2.55f, DeckY, -5f);
            var locker = WeaponKit.ToolLocker(ctx.Puzzles, lockerPos, EnvKit.FacingViewerAt(lockerPos, new Vector3(0f, DeckY, -5f)), ExperimentalVariant.PartyPopper);
            if (locker != null) ctx.Mark("s0.locker", locker.gameObject);
            ctx.Label(s0, new Vector3(2.97f, DeckY + 3.0f, -5f) /* kitfix: in front of the x 3.0 wall face, was buried */, Look(Vector3.right), "FOR CELEBRATIONS AND EMERGENCIES.\nTHIS IS BOTH.", 0.2f, Warm, 2.4f);

            // the always-open gate onto the balcony (4 x 4 m)
            Kit.SolidBox(s0, "GateLintel", new Vector3(0f, DeckY + 4.0f + 1.25f, -0.2f), new Vector3(4.0f, 2.5f, 0.4f), MatKey.WallAlt, 0.03f);
        }

        // ================================================================== BALCONY (C1) + stairs + the closet's lever
        void BuildBalcony(LevelBuildContext ctx)
        {
            var b = ctx.Section("Balcony", Vector3.zero);
            Slab(b, -6f, 6f, 0f, 5f, DeckY, DeckY + 0.6f, MatKey.Marble, false, "BalconyDeck");   // solid base: nothing walks under it
            Kit.BoxVisual(b, "BalconyCarpet", new Vector3(0f, DeckY + 0.01f, 2.5f), new Vector3(5f, 0.02f, 4.4f), MatKey.Velvet, 0f, null, false);
            ctx.Checkpoint(new Vector3(0f, DeckY, 2.5f), 0f);    // beacon pole at (-3.3, 2.5, 2.9): walk to the W stairs along the screen (z 4.4)
            ctx.MarkPoint("boss.safe.1", new Vector3(0f, DeckY, 2.5f));
            ctx.MarkPoint("s0.leverStand", new Vector3(0f, DeckY, 3.7f));

            // railings + glow tape on the ends (fall 2.5 m onto the open floor, allowed)
            for (int s = -1; s <= 1; s += 2)
            {
                EnvKit.Railing(b, new Vector3(s * 5.9f, DeckY, 0.1f), new Vector3(s * 5.9f, DeckY, 4.9f));
                GlowZ(b, s * 5.85f, 0f, 5f, DeckY);
            }
            GlowX(b, -3f, 3f, 4.9f, DeckY);
            Sign(b, new Vector3(4.2f, DeckY + 3.4f, 0.05f), Vector3.back, "MANAGERS' BALCONY\n<size=70%>LIT 24/7 (BY LAW)</size>", 3f, 1.0f);

            // 2.2 m glass screen along z 5 above the closet (apex 1.75 < 2.2: nobody lands on the closet roof)
            Kit.SolidBox(b, "GlassScreenDeck", new Vector3(0f, DeckY + 1.1f, 5.05f), new Vector3(6f, 2.2f, 0.1f), MatKey.Glass, 0f);
            Kit.BoxVisual(b, "GlassScreenRail", new Vector3(0f, DeckY + 2.22f, 5.05f), new Vector3(6.1f, 0.06f, 0.14f), MatKey.MetalLight, 0f);
            Sign(b, new Vector3(-2.1f, DeckY + 1.8f, 4.95f), Vector3.forward, "DO NOT JUMP\nON THE CLOSET", 1.5f, 0.6f, SignStyle.Warning);

            // BROOM CLOSET RELEASE LEVER = PushButton(3 s) wired into the HoldingCell (door open while ON)
            var btnPos = new Vector3(0f, DeckY, 4.5f);
            var btn = PuzzleKit.PushButton(ctx.Puzzles, btnPos, Yaw(180f), 3f, "CLOSET OPEN!");
            ctx.Mark("s1.closetLever", btn.gameObject);
            Sign(b, new Vector3(1.6f, DeckY + 1.5f, 4.95f), Vector3.forward, "PULL TO RELEASE COWORKER.\n<size=70%>NO QUESTIONS ASKED.</size>", 2.4f, 0.7f, SignStyle.Info);
            _closetButton = btn;

            // Max statue, vandal stage 2 (ghost sheet)
            var statue = StoryKit.VillainStatue(b, new Vector3(-5f, DeckY, 1.6f) /* kitfix: back graffiti clear of the south wall */, Yaw(150f), 2.2f, 2);
            if (statue != null) ctx.Mark("story.taunt.1", statue);
            ctx.Label(b, new Vector3(-5f, DeckY + 0.35f, 2.45f), Yaw(150f) * Quaternion.Euler(0f, 180f, 0f), "STATUE OF ME\n<size=70%>(HAUNTED EDITION)</size>", 0.18f, Warm, 1.6f);

            // whiteboard (all views) + the IQ-only UV clue
            EnvKit.Decor.Whiteboard(b, new Vector3(3.6f, DeckY, 0.7f), Look(Vector3.back),
                "NIGHT CHECKLIST\n1 CRANK (WEST)\n2 BREAKER RACE (NORTH)\n3 FUSE (EAST)\nTAKE A BUDDY");
            PerspectiveKit.Clue(b, new Vector3(-2.9f, DeckY + 2.2f, 0.03f), Look(Vector3.back), "SCARE-O-MATIC 3000 SERVICE MANUAL:\nREMOVE SHEET. HACK PROJECTOR.", 0.9f);

            // STAIRS W (x -6..-3) and E (x 3..6), z 5..10: 2.5 m rise over 5 m, 16 steps, railings + glow tape both sides
            for (int s = -1; s <= 1; s += 2)
            {
                float cx = s * 4.5f;
                EnvKit.Stairs(b, new Vector3(cx, 0f, 10f), Vector3.back, 3f, DeckY / 16f, 5f / 16f, 16, MatKey.Marble);   // rise / run are PER STEP
                float outer = s * 5.95f;
                EnvKit.Railing(b, new Vector3(outer, 0f, 10f), new Vector3(outer, DeckY, 5f));
                SlopeGlow(b, new Vector3(outer - s * 0.1f, 0f, 10f), new Vector3(outer - s * 0.1f, DeckY, 5f));
                SlopeGlow(b, new Vector3(s * 3.1f, 0f, 10f), new Vector3(s * 3.1f, DeckY, 5f));
                // inner side (x +-3): the same 2.2 m glass screen (nobody reaches the 3.2 m closet roof)
                Kit.SolidBox(b, "GlassScreenStair", new Vector3(s * 2.98f, 2.35f, 7.2f), new Vector3(0.1f, 4.7f, 4.4f), MatKey.Glass, 0f);
                GlowX(b, cx - 1.5f, cx + 1.5f, 10.05f, 0f);
                ctx.MarkPoint(s < 0 ? "s1.stairW.top" : "s1.stairE.top", new Vector3(cx, DeckY, 4.6f));
                ctx.MarkPoint(s < 0 ? "s1.stairW.bottom" : "s1.stairE.bottom", new Vector3(cx, 0f, 10.5f));
            }
        }

        PushButton _closetButton;

        // ================================================================== S3 FRONT LOBBY + Percy's van
        void BuildLobby(LevelBuildContext ctx)
        {
            var s3 = ctx.Section("S3_FrontLobby", Vector3.zero);
            Slab(s3, -5.2f, 5.2f, 30.4f, 38.4f, 0f, 0.6f, MatKey.Marble, false, "LobbyFloor");
            WallZ(s3, -5.2f, 30.4f, 38.4f, 5f);
            WallZ(s3, 5.2f, 30.4f, 38.4f, 5f);
            // glass front with the doors (the exit cab stands inside)
            WallX(s3, -5.2f, -2.5f, 38.4f, 5f);
            WallX(s3, 2.5f, 5.2f, 38.4f, 5f);
            Kit.SolidBox(s3, "FrontGlass", new Vector3(0f, 2.5f, 38.4f), new Vector3(5f, 5f, 0.1f), MatKey.Glass, 0f);
            Kit.BoxVisual(s3, "LobbyCeiling", new Vector3(0f, 5.1f, 34.4f), new Vector3(10.8f, 0.2f, 8.4f), MatKey.Ceiling, 0f, null, false);
            Kit.BoxVisual(s3, "LobbyLightStrip", new Vector3(0f, 4.98f, 34.4f), new Vector3(1.2f, 0.04f, 6f), MatKey.LightOn, 0f, null, false);
            EnvKit.Decor.DeskWithMonitors(s3, new Vector3(-3.9f, 0f, 33f), Yaw(90f));
            Sign(s3, new Vector3(-5f, 3.2f, 33f), Vector3.left, "RECEPTION\n<size=70%>CLOSED SINCE TEAMWORK WAS BANNED</size>", 2.8f, 1f);
            EnvKit.Decor.Plant(s3, new Vector3(4.3f, 0f, 31.2f), 1);
            EnvKit.Decor.Night.WaterCooler(s3, new Vector3(4.4f, 0f, 36.4f), Yaw(-90f), true, true);

            ctx.Exit(new Vector3(0f, 0f, 33.9f), 0f, s3);   // cab (4.35 m deep) ends at z 38.25, in front of the glass at 38.4
            Sign(s3, new Vector3(0f, 4.3f, 33.4f), Vector3.forward, "FRONT DOOR → THE VAN", 3.6f, 0.7f, SignStyle.Exit);
            ctx.MarkPoint("s3.lobby", new Vector3(0f, 0f, 31.6f));

            // outside: Percy's van (decor; C10 staging)
            var van = Kit.Make("PercysVan", s3, new Vector3(0f, 0f, 42.5f), Yaw(90f)).transform;
            Kit.BoxVisual(van, "Body", new Vector3(0f, 1.25f, 0f), new Vector3(5.2f, 2.1f, 2.3f), MatKey.White, 0.15f);
            Kit.BoxVisual(van, "Cab", new Vector3(2.9f, 1.0f, 0f), new Vector3(1.2f, 1.6f, 2.2f), MatKey.White, 0.2f);
            Kit.BoxVisual(van, "Windshield", new Vector3(3.45f, 1.35f, 0f), new Vector3(0.1f, 0.7f, 1.9f), MatKey.BlackGlass, 0f);
            Kit.BoxVisual(van, "Stripe", new Vector3(0f, 1.1f, -1.16f), new Vector3(5.2f, 0.25f, 0.02f), MatKey.SafetyOrange, 0f);
            for (int w = 0; w < 4; w++)
                PuzzleVisuals.Cyl(van, "Wheel", new Vector3(w < 2 ? -1.6f : 2.2f, 0.4f, w % 2 == 0 ? -1.05f : 1.05f), 0.4f, 0.3f, MatKey.Rubber, Quaternion.Euler(90f, 0f, 0f), 12, false);
            ctx.Label(van, new Vector3(0f, 1.6f, -1.17f), Quaternion.identity, "PERCY'S PERCOLATED DELIVERIES\n<size=60%>DEFINITELY NOT A GETAWAY VAN</size>", 0.32f, UIColors.Hex(0x5A3A1E), 4.8f);
            Kit.BoxVisual(s3, "Street", new Vector3(0f, -0.02f, 43f), new Vector3(16f, 0.04f, 8f), MatKey.Concrete, 0f, null, false);
        }

        // ================================================================== dead upper storeys + broken glass roof (visual only)
        void BuildUpperStoreys(LevelBuildContext ctx)
        {
            var u = ctx.Section("UpperStoreys", Vector3.zero);
            const float y0 = WallH, y1 = 14f;
            float h = y1 - y0, cy = (y0 + y1) * 0.5f;
            Kit.BoxVisual(u, "UpperW", new Vector3(-15.6f, cy, 15f), new Vector3(0.4f, h, 31f), MatKey.WallAlt, 0f, null, false);
            Kit.BoxVisual(u, "UpperE", new Vector3(15.6f, cy, 15f), new Vector3(0.4f, h, 31f), MatKey.WallAlt, 0f, null, false);
            Kit.BoxVisual(u, "UpperN", new Vector3(0f, cy, 30.6f), new Vector3(31.6f, h, 0.4f), MatKey.WallAlt, 0f, null, false);
            Kit.BoxVisual(u, "UpperS", new Vector3(0f, (SouthWallH + y1) * 0.5f, -0.4f), new Vector3(31.6f, y1 - SouthWallH, 0.4f), MatKey.WallAlt, 0f, null, false);
            // dead office windows (2 rows); a couple still glow blue (screensavers)
            int n = 0;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 6; i++)
                {
                    float z = 3f + i * 4.8f, y = 7.6f + row * 3.4f;
                    bool glow = (i + row * 2) % 5 == 1;
                    Kit.BoxVisual(u, "WinW", new Vector3(-15.38f, y, z), new Vector3(0.05f, 1.8f, 2.6f), glow ? MatKey.LightBlue : MatKey.BlackGlass, 0f, null, false);
                    Kit.BoxVisual(u, "WinE", new Vector3(15.38f, y, z), new Vector3(0.05f, 1.8f, 2.6f), (glow && n++ % 2 == 0) ? MatKey.LightBlue : MatKey.BlackGlass, 0f, null, false);
                }
            for (int i = 0; i < 6; i++)
                Kit.BoxVisual(u, "WinN", new Vector3(-12.5f + i * 5f, 9.4f, 30.38f), new Vector3(2.8f, 3.2f, 0.05f), MatKey.BlackGlass, 0f, null, false);
            // the broken glass roof at 14 m (frame + a few panes; the moon peeks through)
            for (int i = 0; i < 5; i++)
                Kit.BoxVisual(u, "RoofBeam", new Vector3(-12f + i * 6f, y1, 15f), new Vector3(0.3f, 0.3f, 31f), MatKey.MetalDark, 0f, null, false);
            Kit.BoxVisual(u, "RoofBeamX", new Vector3(0f, y1, 15f), new Vector3(31f, 0.3f, 0.3f), MatKey.MetalDark, 0f, null, false);
            Kit.BoxVisual(u, "RoofPaneA", new Vector3(-9f, y1 + 0.1f, 7f), new Vector3(5.8f, 0.05f, 13f), MatKey.BlackGlass, 0f, null, false);
            Kit.BoxVisual(u, "RoofPaneB", new Vector3(9f, y1 + 0.1f, 22f), new Vector3(5.8f, 0.05f, 13f), MatKey.BlackGlass, 0f, null, false);
            PuzzleVisuals.Cyl(u, "Moon", new Vector3(3f, 24f, 40f), 3f, 0.2f, MatKey.Moon, Quaternion.Euler(80f, 0f, 0f), 24, false);
        }

        // ================================================================== tutorials + hints
        void BuildTutorialsAndHints(LevelBuildContext ctx)
        {
            var g = ctx.Gameplay;
            ctx.Tutorial(new Vector3(0f, DeckY + 1.5f, 2.5f), new Vector3(12f, 3f, 4.5f), "ch11_lonely", "IT ONLY HUNTS THE LONELY",
                "THE BOOGEYMAN ONLY HUNTS THE LONELY. Alone in the dark = SCARED. Move in pairs. Park friends in the light.", null, true, g);
            ctx.Tutorial(new Vector3(1.6f, DeckY + 1.5f, -5f), new Vector3(2.6f, 3f, 2.6f), "ch11_flare", "PARTY POPPER",
                "PARTY POPPER: a flare makes a safe pool of light for 8 seconds. Park a friend in it.", new[] { "LMB" }, false, g);
            // event tutorials (fired by the boss; the zones sit far below the level so walking never triggers them)
            var t1 = ctx.Tutorial(new Vector3(0f, -60f, 0f), new Vector3(0.5f, 0.5f, 0.5f), "ch11_eyes", "EYES IN THE DARK",
                "EYES BEHIND SOMEONE? Shine your flashlight on them, get to a friend, or step into the light. Quick!", null, true, g);
            var t2 = ctx.Tutorial(new Vector3(0f, -60f, 4f), new Vector3(0.5f, 0.5f, 0.5f), "ch11_closet", "THE BROOM CLOSET",
                "Spooked interns go to the BROOM CLOSET. Pull the lever on the lit balcony to get them out.", null, false, g);
            var t3 = ctx.Tutorial(new Vector3(0f, -60f, 8f), new Vector3(0.5f, 0.5f, 0.5f), "ch11_unmask", "UNMASK IT",
                "It's a floor buffer in a bedsheet. Knock the sheet off (shoot it, throw a friend at it, or punch it while it charges), then HACK the projector.", null, true, g);
            if (_boss != null)
            {
                _boss.TutEyes = t1 != null ? t1.GetComponent<TutorialTrigger>() : null;
                _boss.TutCloset = t2 != null ? t2.GetComponent<TutorialTrigger>() : null;
                _boss.TutUnmask = t3 != null ? t3.GetComponent<TutorialTrigger>() : null;
            }

            var h1 = ctx.Hint(new Vector3(0f, 1.5f, 19f), new Vector3(30f, 3f, 18f),
                "Three lights: the Brute cranks west, the Sprinter races north, the Brainiac fixes the east fuse. Go in pairs; park the third in a break area.",
                "Buddy system. Night-lights help.", g);
            ctx.Hint(new Vector3(0f, 2f, 6f), new Vector3(12f, 5f, 10f),
                "The release lever is on the balcony, up the stairs. The stairs are always lit.", "Check the closet. Upstairs.", g);
            var h3 = ctx.Hint(new Vector3(0f, 1.5f, 19f), new Vector3(30f, 3f, 18f),
                "Knock the sheet off: shoot it, throw a teammate from a THROW pad, or punch it on a charging pad. Keep the Brainiac close to hack it. Lonely interns near a pad get BOO'd.",
                "Unmask it. Bring a friend.", g);
            if (h3 != null) h3.SetActive(false);
            if (_boss != null)
            {
                var p2 = new System.Collections.Generic.List<GameObject>(_boss.PhaseOnlyP2);
                if (h3 != null) p2.Add(h3);
                _boss.PhaseOnlyP2 = p2.ToArray();
                if (h1 != null) _boss.PhaseOnlyP1 = new[] { h1 };
            }
        }
    }
}
