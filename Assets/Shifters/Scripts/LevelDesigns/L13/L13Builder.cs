// =====================================================================================
//  SHIFTERS - L13Builder.cs   [LEVEL 13 - SHIFT 13: HOSTILE TAKEOVER (final boss: THE ONE-MAN BAND)]
//  ME TOWER penthouse roof, floor 296 ("THE TOP OF THE TOP"). LEVELS2.md SHIFT 13.
//    S0 THE EXECUTIVE LANDING (x -8..8, z -10..8): crashed escape pod, THE ARMORY, cardboard-Max closet, C1.
//    ARENA (x -19..19, z 8..52, one floor collider): P1 FIST, P2 SKATES + BUMP RUN, P3 BRAIN DOME (balconies),
//          P4 CAPTURE (figure-8 SOLO POD rail) + DETENTION (VIP) alcove (x 19..25, z 37..43).
//    S5 THE LOBBY OF ONE (x -5..5, z 52..62): C5 + EXPRESS ELEVATOR exit.
//  Every object comes from code (BuildAll regenerates the scene). Partial: L13Builder.Arena.cs.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L13Builder : ILevelBuilder
    {
        public int LevelIndex => 12;

        public static readonly Vector3 ArenaCenter = new Vector3(0f, 0f, 30f);
        public static readonly Vector3 Home = new Vector3(0f, 0f, 42.5f);
        public static readonly Vector3 LaneCenter = new Vector3(0f, 0f, 32f);
        public const float LaneR = 10.5f, LaneIn = 8f, LaneOut = 13f;
        public const float RouteLength = 26f;
        public static readonly Vector3 PodHome = new Vector3(0f, 0f, 31f);
        public static readonly Vector3 CellSeat = new Vector3(22f, 0f, 40f);

        LevelBuildContext _ctx;
        int _lights;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            _lights = 0;
            BuildShell(ctx);
            BuildS0(ctx);
            BuildArena(ctx);
            BuildS5(ctx);
            BuildBackdrop(ctx);
        }

        // ------------------------------------------------------------------ helpers
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);
        /// <summary>Floor text readable by a viewer looking along yaw.</summary>
        static Quaternion FloorRot(float yaw = 0f) => Quaternion.Euler(90f, yaw, 0f);

        GameObject Wall(Transform p, float x0, float z0, float x1, float z1, float h, MatKey mat = MatKey.BlackGlass)
            => EnvKit.Wall(p, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), h, 0.5f, mat, true);

        /// <summary>Wall sign; yaw = the viewer's look direction (west wall -90, east wall +90, north wall 0, south wall 180).</summary>
        GameObject Sign(Transform p, Vector3 pos, float yaw, string text, float w, float h, SignStyle style = SignStyle.Info)
            => EnvKit.Sign(p, pos, Yaw(yaw), text, new Vector2(w, h), style);

        void FloorText(Transform p, Vector3 pos, float yaw, string text, float height, Color color, float width = 10f)
            => _ctx.Label(p, pos + Vector3.up * 0.03f, FloorRot(yaw), text, height, color, width);

        GameObject GoldLight(Transform p, Vector3 pos, Color c, float intensity, float range)
        {
            if (_lights >= 3) return null;
            _lights++;
            var go = Kit.Make("L13Light", p, pos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            DecorLight.Mark(l);
            return go;
        }

        static Color Gold => UIColors.Hex(0xFFD24A);
        static Color Ruby => UIColors.Hex(0xFF4D5E);
        static Color Ink => UIColors.Hex(0xF4F1E8);

        // ------------------------------------------------------------------ shell: floor + walls
        void BuildShell(LevelBuildContext ctx)
        {
            var g = ctx.Section("Shell", Vector3.zero);
            // ONE floor collider for S0, the arena, DETENTION and S5 (x -19..25, z -10..62)
            ctx.Platform(ctx.Geometry, new Vector3(3f, 0f, 27f), new Vector3(44f, 1f, 74f), MatKey.Floor, false, "PenthouseFloor");
            // visual overlays (no colliders): black glass landing, soft marble arena with gold tile seams
            Kit.BoxVisual(g, "S0Tiles", new Vector3(0f, 0.004f, -1f), new Vector3(16f, 0.008f, 18f), MatKey.Marble, 0f, null, false);
            Kit.BoxVisual(g, "ArenaMarble", new Vector3(0f, 0.004f, 30f), new Vector3(38f, 0.008f, 44f), MatKey.Marble, 0f, null, false);
            for (int i = -3; i <= 3; i++)
            {
                Kit.BoxVisual(g, "GoldSeamX", new Vector3(i * 5.4f, 0.01f, 30f), new Vector3(0.08f, 0.01f, 44f), MatKey.Gold, 0f, null, false);
            }
            for (int i = 0; i < 8; i++)
                Kit.BoxVisual(g, "GoldSeamZ", new Vector3(0f, 0.01f, 10.5f + i * 5.5f), new Vector3(38f, 0.01f, 0.08f), MatKey.Gold, 0f, null, false);
            Kit.BoxVisual(g, "S5Carpet", new Vector3(0f, 0.004f, 58f), new Vector3(10f, 0.008f, 12f), MatKey.Velvet, 0f, null, false);
            Kit.BoxVisual(g, "CellFloor", new Vector3(22f, 0.004f, 40f), new Vector3(6f, 0.008f, 6f), MatKey.HazardBlack, 0f, null, false);

            var w = ctx.Geometry;
            // S0 (walls 5 m)
            Wall(w, -8f, -10f, -8f, 8f, 5f);
            Wall(w, 8f, -10f, 8f, 8f, 5f);
            Wall(w, -8f, -10f, 8f, -10f, 5f);
            // arena glass shell (6 m): south wall with the 12 m portal x -6..6
            Wall(w, -19f, 8f, -6f, 8f, 6f);
            Wall(w, 6f, 8f, 19f, 8f, 6f);
            Kit.BoxVisual(g, "PortalLintel", new Vector3(0f, 6.25f, 8f), new Vector3(12.6f, 0.5f, 0.6f), MatKey.Gold, 0.05f);
            Wall(w, -19f, 8f, -19f, 52f, 6f);
            Wall(w, 19f, 8f, 19f, 37.5f, 6f);                                     // east wall, DETENTION door gap z 37.5..42.5
            Wall(w, 19f, 42.5f, 19f, 52f, 6f);
            Kit.SolidBox(w, "CellLintel", new Vector3(19f, 5.25f, 40f), new Vector3(0.5f, 1.5f, 5f), MatKey.BlackGlass, 0.03f);
            Wall(w, -19f, 52f, -2.6f, 52f, 6f);                                   // north wall, 4 m door x -2..2 (+ pillars)
            Wall(w, 2.6f, 52f, 19f, 52f, 6f);
            Kit.SolidBox(w, "NorthLintel", new Vector3(0f, 5.5f, 52f), new Vector3(5.2f, 1.0f, 0.5f), MatKey.BlackGlass, 0.03f);
            // DETENTION alcove (x 19..25, z 37..43, 5 m ceiling)
            Wall(w, 25f, 37f, 25f, 43f, 5f, MatKey.MetalDark);
            Wall(w, 19f, 37f, 25f, 37f, 5f, MatKey.MetalDark);
            Wall(w, 19f, 43f, 25f, 43f, 5f, MatKey.MetalDark);
            Kit.SolidBox(w, "CellCeiling", new Vector3(22f, 5.25f, 40f), new Vector3(6.5f, 0.5f, 6.5f), MatKey.MetalDark, 0.03f);
            // S5 lobby (walls 5 m)
            Wall(w, -5f, 52f, -5f, 64f, 5f);
            Wall(w, 5f, 52f, 5f, 64f, 5f);
            Wall(w, -5f, 64f, 5f, 64f, 5f);
        }

        // ------------------------------------------------------------------ S0 THE EXECUTIVE LANDING
        void BuildS0(LevelBuildContext ctx)
        {
            var s0 = ctx.Section("S0_ExecutiveLanding", Vector3.zero);
            ctx.SetSpawn(new Vector3(0f, 0f, -6f), 0f);
            // crashed EXECUTIVE ESCAPE POD on a toppled Max statue
            StoryKit.VillainStatue(s0, new Vector3(-2.5f, 0f, -8.6f), Yaw(180f), 1.45f, 3); // plinth texts face +z (spawn) / -z (0.75 m to the south wall): clear of PodBlock + ThroneDais (smoke sign audit)
            var pod = Kit.Make("CrashedEscapePod", s0, new Vector3(0f, 0f, -8.5f), Yaw(18f)).transform;
            ProcMesh.AddRenderer(Kit.Make("Shell", pod, new Vector3(0f, 1.1f, 0f), Quaternion.Euler(0f, 0f, 78f)),
                ProcMesh.Cached("l13_escpod", () => ProcMesh.Cylinder(1.0f, 3.0f, 18, true)), Kit.Mat(MatKey.MetalLight), true);
            Kit.BoxVisual(pod, "Fin", new Vector3(1.6f, 1.5f, 0f), new Vector3(0.9f, 1.4f, 0.12f), MatKey.WarningRed, 0.03f);
            Kit.BoxVisual(pod, "FallenDoor", new Vector3(-0.4f, 0.06f, 1.9f), new Vector3(1.4f, 0.1f, 1.6f), MatKey.MetalDark, 0.03f, Yaw(-25f));
            Kit.SolidBox(pod, "PodBlock", new Vector3(0f, 1.0f, 0f), new Vector3(3.2f, 2.0f, 2.0f), MatKey.Shadow, 0f).GetComponent<Renderer>().enabled = false;
            ctx.Label(s0, new Vector3(0f, 2.6f, -7.2f), Quaternion.identity, "EXECUTIVE ESCAPE POD\n<size=60%>LANDED BADLY</size>", 0.42f, Ink, 5f);

            // THE ARMORY on the east wall (slots x 7.0, facing west; backboard +0.7 = flush with the wall face x 7.75); slots 1.6 m apart along z
            var tools = new[] { ExperimentalVariant.BitShifter, ExperimentalVariant.SwapOMatic, ExperimentalVariant.PartyPopper, ExperimentalVariant.CapsLock };
            var armory = WeaponKit.Armory(s0, new Vector3(7.0f, 0f, -1.2f), Yaw(90f), tools);
            if (armory != null && armory.Slots != null)
            {
                string[] ids = { "bitshifter", "swap", "popper", "capslock" };
                for (int i = 0; i < armory.Slots.Length && i < ids.Length; i++)
                    if (armory.Slots[i] != null) ctx.Mark("s0.armory." + ids[i], armory.Slots[i].gameObject);
                ctx.Mark("s0.armory", armory.gameObject);
            }
            Sign(s0, new Vector3(7.7f, 4.15f, -1.2f), 90f, "THE ARMORY\n<size=55%>CONFISCATED TEAM EQUIPMENT</size>", 4.6f, 1.2f, SignStyle.Info);

            // supply closet + cardboard-Max jump scare
            Kit.SolidBox(s0, "Closet", new Vector3(7.25f, 1.4f, -6.2f), new Vector3(1.4f, 2.8f, 2.2f), MatKey.Wood, 0.04f);
            Sign(s0, new Vector3(6.5f, 3.2f, -6.2f), 90f, "SUPPLY CLOSET\n<size=55%>(DEFINITELY NOTHING IN HERE)</size>", 2.2f, 0.8f, SignStyle.Info);
            ScareKit.JumpScare(s0, new Vector3(5.2f, 1.2f, -5.5f), new Vector3(3.2f, 2.4f, 3.0f), new Vector3(6.2f, 0f, -5.5f), ScareKind.CardboardMax, "ch13.cutout");

            // 2 EXECUTIVE PAPERWEIGHTS (25 kg, BIT-SHIFTER-able)
            for (int i = 0; i < 2; i++)
            {
                var rb = PuzzleKit.Crate(ctx.Props, new Vector3(-6f, 0.45f, i == 0 ? -3f : -1.5f));
                if (rb == null) continue;
                rb.name = "ExecutivePaperweight" + (i + 1);
                PuzzleKit.MakeShiftable(rb);
                PerspectiveKit.Reveal(rb.gameObject, PerspectiveMask.Strength, "PAPERWEIGHT 25 KG\n(HUGE: 100 KG)", true, false, new Vector3(0f, 1.1f, 0f));
                ctx.Mark("s0.paperweight." + (i + 1), rb.gameObject);
            }
            ctx.Label(s0, new Vector3(-7.75f, 2.2f, -2.25f), Yaw(-90f), "EXECUTIVE PAPERWEIGHTS\n<size=60%>WEIGHT: SYMBOLIC</size>", 0.32f, Ink, 3f);

            // INTERN PRINTER kiosk on the west wall facing east
            var kiosk = LevelKit.ReprintKiosk(s0, new Vector3(-7.2f, 0f, 2f), Yaw(90f));
            if (kiosk != null) ctx.Mark("s0.kiosk", kiosk.gameObject);

            // ME-TV + propaganda
            var tv = StoryKit.MeTvScreen(s0, new Vector3(-7.7f, 3f, -4f), Yaw(-90f), 3.2f);
            if (tv != null) ctx.Mark("story.metv", tv);
            // polish 11-17: Yaw(180) showed the slogans to the arena and their mirrored backs to the team walking in from the spawn (z -6)
            StoryKit.HoloPoster(s0, new Vector3(-4.5f, 0f, 6.6f), Yaw(0f), "ONE WORLD. ONE RULER. ONE CHAIR.", 4);
            StoryKit.HoloPoster(s0, new Vector3(4.5f, 0f, 6.6f), Yaw(0f), "THINK FOR YOURSELF. ONLY YOURSELF.", 4);
            Sign(s0, new Vector3(0f, 3.8f, -9.7f), 180f, "FLOOR 296: PENTHOUSE OF ONE\n<size=55%>(ALL MINE)</size>", 6.5f, 1.4f, SignStyle.Info);
            Sign(s0, new Vector3(-7.7f, 3.8f, 5.5f), -90f, "BOARD MEETING: CANCELLED\n<size=55%>(BOARD: FIRED)</size>", 3.4f, 1.0f, SignStyle.Warning);
            EnvKit.Decor.Tower.ThroneDais(s0, new Vector3(-4.8f, 0f, -7.6f), Yaw(180f), 3.0f, 2.0f, 2, 0.2f, true, true);

            // C1 + portal
            ctx.Checkpoint(new Vector3(0f, 0f, 3f), 0f);
            PuzzleKit.NoToolZone(ctx.Puzzles, new Vector3(0f, 2f, 8.25f), new Vector3(12f, 4f, 2.5f),
                ToolMask.SwapOMatic | ToolMask.BitShifter | ToolMask.CapsLock | ToolMask.PartyPopper);
            ctx.Label(s0, new Vector3(0f, 6.9f, 7.6f), Yaw(0f), "THE TOP OF THE TOP", 0.8f, Gold, 12f);
            FloorText(s0, new Vector3(0f, 0f, 6.3f), 0f, "SHATTERPROOF GLASS. LIKE MY EGO.", 0.42f, Ink, 9f);

            ctx.Tutorial(new Vector3(5.5f, 1.5f, -1.2f), new Vector3(5f, 3f, 8f), "ch13_armory", "THE ARMORY",
                "THE ARMORY: swap your gun for any tool you've unlocked. Or keep the gun. Every phase works with it.", new[] { "E" }, false);
            ctx.Tutorial(new Vector3(0f, 1.5f, 6.5f), new Vector3(12f, 3f, 3f), "ch13_vision", "ME-VISION",
                "ME-VISION: he only attacks who you control. Park the others in BREAK AREAS. He waits while you're on break.", new[] { "RShift" }, true);

            // story staging (C12)
            ctx.MarkPoint("story.c12.cam", new Vector3(0f, 5f, 2f));
            ctx.MarkPoint("story.c12.desk", new Vector3(3f, 4f, 40f));
        }

        // ------------------------------------------------------------------ S5 THE LOBBY OF ONE
        void BuildS5(LevelBuildContext ctx)
        {
            var s5 = ctx.Section("S5_LobbyOfOne", Vector3.zero);
            ctx.Checkpoint(new Vector3(0f, 0f, 55.5f), 0f);
            ctx.Exit(new Vector3(0f, 0f, 59.5f), 0f);
            Sign(s5, new Vector3(0f, 4.2f, 63.7f), 0f, "EXPRESS ELEVATOR\n<size=55%>-> THE HALL OF ONE</size>", 4.5f, 1.2f, SignStyle.Exit);
            Sign(s5, new Vector3(-4.7f, 3.2f, 56f), -90f, "LOBBY OF ONE\n<size=55%>SEATING CAPACITY: 1</size>", 3f, 1f);
            EnvKit.Decor.Tower.PropagandaScreen(s5, new Vector3(4.7f, 3.0f, 56.5f), Yaw(90f), 3.2f, 1.8f, "VOTE ME\n(NO OTHER OPTIONS)", false, 2f);
            ctx.MarkPoint("s5.lobby", new Vector3(0f, 0f, 55.5f));
        }

        // ------------------------------------------------------------------ backdrop
        void BuildBackdrop(LevelBuildContext ctx)
        {
            var d = ctx.Decor;
            EnvKit.Decor.Tower.CitySkyline(d, new Vector3(0f, -30f, 95f), Yaw(0f), 170f, 13, true, 1.2f, null);
            EnvKit.Decor.Tower.CitySkyline(d, new Vector3(-62f, -30f, 26f), Yaw(-90f), 150f, 17, false, 1.1f, null);
            EnvKit.Decor.Tower.CitySkyline(d, new Vector3(68f, -30f, 26f), Yaw(90f), 150f, 23, false, 1.1f, null);
            EnvKit.Decor.Tower.CitySkyline(d, new Vector3(0f, -30f, -48f), Yaw(180f), 170f, 29, false, 1.1f, null);
        }
    }
}
