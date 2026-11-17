// =====================================================================================
//  SHIFTERS - L1Builder.Tests.cs   (LEVEL 1: S2 Aptitude Tests - needs ALL THREE)
//  Hub x -9..9 / z 52..72 with door D2 (Latch-AND of the three lane results) in its north wall.
//    STR lane (west, z 54..62): push the 160 kg block 13 m onto the recessed 150 kg plate (latching).
//    IQ  lane (west, z 64..71): IQ 7 terminal (latching).
//    SPD lane (east, z 54..62): timed button -> 28 m sprint -> door D_spd -> 40 kg latching plate.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L1Builder
    {
        public const float HubZ0 = 52f, HubZ1 = 72f;
        public const float StrPlateX = -27f, LaneZ = 58f, BlockX = -14f;
        public const float SpdButtonX = 11.5f, SpdDoorX = 39.5f, SpdPlateX = 42.5f, SpdRoute = 28f;
        public const float IqZ = 67.5f, IqTerminalX = -19.5f;

        void BuildAptitudeTests()
        {
            var ctx = _ctx;
            var s = ctx.Section("S2_AptitudeTests", Vector3.zero);
            var g = ctx.Geometry;
            const float H = 5.5f;

            // ---- hub
            Floor(g, -9f, 9f, HubZ0, HubZ1);
            WallZ(g, -9f, HubZ0, 54f, H); WallZ(g, -9f, 62f, 64f, H); WallZ(g, -9f, 71f, HubZ1, H);
            WallZ(g, 9f, HubZ0, 54f, H); WallZ(g, 9f, 62f, HubZ1, H);
            WallX(g, -9f, -2.75f, HubZ1, H); WallX(g, 2.75f, 9f, HubZ1, H);

            // ---- STR lane: heavy block -> recessed 150 kg plate
            Floor(g, -33f, -9f, 54f, 62f, 0f, MatKey.FloorAlt);
            WallX(g, -33f, -9f, 54f, H); WallX(g, -33f, -21f, 62f, H);
            Box(g, "LaneDivider", V(-21f, 0f, 62f), V(-9f, H, 64f), MatKey.WallAlt);
            WallZ(g, -33f, 54f, 62f, H);
            var block = PuzzleKit.HeavyBlock(ctx.Props, V(BlockX, 0f, LaneZ));
            ctx.Mark("S2.STR.Block", block.gameObject);
            var strPlate = PuzzleKit.PressurePlate(ctx.Puzzles, V(StrPlateX, 0f, LaneZ), null, Tuning.PlateHeavy, true, "STRENGTH TEST: PASSED (THE BLOCK DID MOST OF IT)");
            ctx.Mark("S2.STR.Plate", strPlate.gameObject);
            ctx.MarkPoint("S2.STR.Goal", V(StrPlateX, 0f, LaneZ));
            ctx.MarkPoint("S2.STR.Entry", V(-8f, 0f, LaneZ));
            ctx.MarkPoint("S2.STR.PlateEdge", V(StrPlateX + 1.85f, 0f, LaneZ));
            for (float x = -16.5f; x > StrPlateX + 1.5f; x -= 2.5f)
                FloorText(s, V(x, 0f, LaneZ + 2.2f), -90f, "<<<", 0.5f, UIColors.Strength, 2f);
            FloorText(s, V(StrPlateX + 2.4f, 0f, LaneZ), -90f, "BLOCK GOES HERE", 0.4f, UIColors.Strength, 4f);
            // OOPS button: respawns the block (anti-softlock)
            var oops = PuzzleKit.PushButton(ctx.Puzzles, V(-10.6f, 0f, 55.2f), Yaw(0f), 1f, "");
            var reset = Kit.Make("L1PropReset", ctx.Puzzles, V(-10.6f, 0f, 55.2f)).AddComponent<L1PropReset>();
            reset.Targets = new[] { block.GetComponent<Respawnable>() };
            PuzzleKit.Wire(reset, oops);
            ctx.Mark("S2.STR.Oops", oops.gameObject);
            Sign(s, V(-10.6f, 2.0f, 54.25f), 0f, "OOPS BUTTON\n(RESETS BLOCK)", 1.7f, 0.7f, SignStyle.Warning);
            ctx.Tutorial(V(-12.5f, 1.5f, LaneZ), V(7f, 3.5f, 8f), "strength", "STRENGTH",
                "Your strength determines what you can move. Only a HEAVY LIFTER can push this block.", new[] { "WASD" });
            ctx.Hint(V(-21f, 2f, LaneZ), V(24f, 5f, 8f),
                "Switch to your HEAVY LIFTER and walk into the block to push it straight down the lane onto the plate. Messed up? Press the OOPS button.",
                "That block needs a big friend. Possibly a big, sweaty friend.");

            // ---- IQ lane: terminal
            Floor(g, -21f, -9f, 64f, 71f, 0f, MatKey.FloorAlt);
            WallX(g, -21f, -9f, 71f, H);
            WallZ(g, -21f, 64f, 71f, H);
            var term = PuzzleKit.Terminal(ctx.Puzzles, V(IqTerminalX, 0f, IqZ), Yaw(90f), Tuning.TerminalIQ, true, 6f, "INTELLIGENCE TEST: PASSED (WE ARE AS SURPRISED AS YOU)");
            ctx.Mark("S2.IQ.Terminal", term.gameObject);
            ctx.MarkPoint("S2.IQ.Entry", V(-7.5f, 0f, IqZ));
            IQNote(s, V(-20.75f, 2.6f, 65.5f), -90f, "THE PASSWORD IS 'PASSWORD'", 0.32f);
            IQNote(s, V(-15f, 2.3f, 70.75f), 0f, "SMART PEOPLE READ WALLS", 0.3f);
            ctx.Tutorial(V(-12.5f, 1.5f, IqZ), V(7f, 3.5f, 7f), "intellect", "INTELLECT",
                "Smart characters hack terminals and see hidden clues. Walk up and press E.", new[] { "E" });
            ctx.Hint(V(-15f, 2f, IqZ), V(12f, 5f, 7f),
                "Switch to your BIG BRAIN (IQ 7+), walk up to the terminal and press E. Hold still while it hacks.",
                "The computer only talks to nerds.");

            // ---- SPD lane: timed button -> 28 m straight run -> D_spd -> latching plate
            Floor(g, 9f, 45f, 54f, 62f, 0f, MatKey.FloorAlt);
            WallX(g, 9f, 45f, 54f, H); WallX(g, 9f, 45f, 62f, H);
            WallZ(g, 45f, 54f, 62f, H);
            var route = new[] { V(SpdButtonX + 1f, 0f, LaneZ), V(SpdDoorX + 1f, 0f, LaneZ) };
            var timed = PuzzleKit.TimedButton(ctx.Puzzles, V(SpdButtonX, 0f, 55.8f), Yaw(0f), SpdRoute, route, "");
            ctx.Mark("S2.SPD.Button", timed.gameObject);
            var spdPlate = PuzzleKit.PressurePlate(ctx.Puzzles, V(SpdPlateX, 0f, LaneZ), null, Tuning.PlateLight, true, "SPEED TEST: PASSED (SUSPICIOUSLY FAST)");
            ctx.Mark("S2.SPD.Plate", spdPlate.gameObject);
            var dSpd = PuzzleKit.Door(ctx.Puzzles, V(SpdDoorX, 0f, LaneZ), Yaw(90f), DoorMode.SlideUp, 3.6f, 4.2f, timed, spdPlate);
            dSpd.Logic = SignalLogic.Any;
            dSpd.SpeedOff = 7.5f; // slams shut: non-turbo interns arrive just in time to watch it close
            ctx.Mark("S2.SPD.Door", dSpd.gameObject);
            WallZ(g, SpdDoorX, 54f, LaneZ - 2.3f, H); WallZ(g, SpdDoorX, LaneZ + 2.3f, 62f, H);
            ctx.MarkPoint("S2.SPD.Entry", V(8f, 0f, LaneZ));
            ctx.MarkPoint("S2.SPD.Back", V(SpdDoorX - 3f, 0f, LaneZ));
            FloorText(s, V(SpdButtonX + 3.2f, 0f, LaneZ), 90f, "START", 0.8f, UIColors.Speed, 3f);
            FloorText(s, V(SpdDoorX - 2.2f, 0f, LaneZ), 90f, "FINISH (MAYBE)", 0.5f, UIColors.Speed, 4f);
            for (float x = SpdButtonX + 5f; x < SpdDoorX - 3f; x += 3f)
                Kit.BoxVisual(s, "LaneLine", V(x, 0.012f, LaneZ), V(1.2f, 0.02f, 0.12f), MatKey.SignWhite, 0.003f, null, false);
            ctx.Tutorial(V(12.5f, 1.5f, LaneZ), V(7f, 3.5f, 8f), "speed", "SPEED",
                "Fast characters beat timed doors. Press E on the timed button, then hold LEFT SHIFT and run.", new[] { "E", "LShift" });
            ctx.Hint(V(26f, 2f, LaneZ), V(36f, 5f, 8f),
                "Only TURBO LEGS can beat this door. Switch to your sprinter, press the timed button and sprint (LEFT SHIFT) straight down the lane onto the plate.",
                "Run. Like your performance review depends on it. It does.");

            // ---- door D2: latch-AND of the three lane results
            var d2 = PuzzleKit.Door(ctx.Puzzles, V(0f, 0f, HubZ1), null, DoorMode.SlideUp, 4.5f, 5f, strPlate, spdPlate, term);
            d2.Logic = SignalLogic.Latch;
            ctx.Mark("S2.D2", d2.gameObject);
            ctx.MarkPoint("S2.D2Near", V(0f, 0f, HubZ1 - 2.5f));
            ctx.MarkPoint("S2.D2Far", V(0f, 0f, HubZ1 + 2.5f));
            LaneLamp(s, V(-4.6f, 3.4f, HubZ1 - 0.22f), "STR", UIColors.Strength, strPlate);
            LaneLamp(s, V(-6.6f, 3.4f, HubZ1 - 0.22f), "IQ", UIColors.Intellect, term);
            LaneLamp(s, V(4.6f, 3.4f, HubZ1 - 0.22f), "SPD", UIColors.Speed, spdPlate);
            PuzzleKit.Cable(s, V(StrPlateX + 1.1f, 0f, LaneZ + 0.6f), V(-1.6f, 0f, HubZ1 - 0.6f), strPlate);
            PuzzleKit.Cable(s, V(IqTerminalX + 0.5f, 0f, IqZ - 0.9f), V(-0.6f, 0f, HubZ1 - 0.6f), term);
            PuzzleKit.Cable(s, V(SpdPlateX - 1.1f, 0f, LaneZ - 0.6f), V(1.6f, 0f, HubZ1 - 0.6f), spdPlate);

            // ---- lane portals (readable from the hub, where you pick a lane) + hub tutorials
            Sign(s, V(-8.75f, 4.6f, LaneZ), 90f, "STRENGTH TEST", 4.5f, 0.9f, SignStyle.Warning);
            Sign(s, V(-8.75f, 4.6f, IqZ), 90f, "INTELLIGENCE TEST", 4.5f, 0.9f, SignStyle.Info);
            Sign(s, V(8.75f, 4.6f, LaneZ), -90f, "SPEED TEST", 4.5f, 0.9f, SignStyle.Danger);
            Sign(s, V(0f, 5.85f, HubZ1 - 0.3f), 180f, "APTITUDE TESTS: ALL THREE OR NOTHING", 9f, 0.85f, SignStyle.Exit);
            ctx.Tutorial(V(0f, 1.5f, 55f), V(18f, 3.5f, 5f), "perspective", "SHIFT PERSPECTIVE",
                "Each character sees the world differently. Switch and look for highlights. Press R to scan.", new[] { "R", "RShift" });
            ctx.Tutorial(V(0f, 1.5f, 62f), V(18f, 3.5f, 6f), "stats", "STATS",
                "Some characters are better suited for certain puzzles. Check stats in the switcher.", new[] { "RShift" });
            ctx.Hint(V(0f, 2f, 62f), V(18f, 5f, 20f),
                "Each lane tests one stat: the HEAVY LIFTER pushes the block, the TURBO LEGS beats the timed door, the BIG BRAIN hacks the terminal. Door D2 opens when all three lamps are green.",
                "Three lanes, three interns. Coincidence? HR thinks not.");

            ctx.Checkpoint(V(0f, 0f, 77f), 0f);

            // ---- decor
            var d = ctx.Decor;
            Board(d, V(6.2f, 0f, 69.6f), 200f, "TODAY'S SHIFT SCORES:\nSTR  ???\nSPD  ???\nIQ   ???\nVIBES: GREAT");
            Poster(d, V(-8.78f, 2.1f, 63f), 90f, "HAVE YOU TRIED", "BEING SMARTER?"); // on the lane divider (z 62..64)
            Poster(d, V(8.75f, 2.1f, 68f), -90f, "SHIFT HAPPENS", "ESPECIALLY WHEN YOU PRESS RIGHT SHIFT");
            Poster(d, V(-32.75f, 2.2f, LaneZ - 2.5f), 90f, "LIFT WITH YOUR LEGS", "OR YOUR COWORKERS");
            EnvKit.Decor.CrateStack(d, V(-31.2f, 0f, 60.4f), Yaw(10f), 3, 4);
            EnvKit.Decor.TrainingDummy(d, V(-31.5f, 0f, 55.4f), Yaw(60f));
            EnvKit.Decor.DeskWithMonitors(d, V(-16f, 0f, 70.2f), Yaw(180f));
            EnvKit.Decor.Plant(d, V(-20.2f, 0f, 70.2f), 0);
            for (int i = 0; i < 5; i++) EnvKit.Decor.Cone(d, V(SpdButtonX + 6f + i * 5f, 0f, 61.2f));
            for (int i = 0; i < 5; i++) EnvKit.Decor.Cone(d, V(SpdButtonX + 6f + i * 5f, 0f, 54.8f));
            BenchFacing(d, V(44.1f, 0f, 60.6f), -90f, 1.6f);
            Poster(d, V(44.75f, 2.2f, 56f), -90f, "PERSONAL BEST", "SET BY THE DOOR. IT WON.");
            Panel(V(0f, 5.2f, HubZ0 + 0.3f), 180f, 5f, 0.4f, UIColors.AccentYellow);   // free-hanging hub entrance strip:
            Panel(V(0f, 5.2f, HubZ0 + 0.3f), 0f, 5f, 0.4f, UIColors.AccentYellow);     // lit on both sides (back to back)
            Panel(V(-20f, 4.6f, 54.2f), 0f, 3f, 0.4f, UIColors.Strength);
            Panel(V(-15f, 4.6f, 70.8f), 180f, 3f, 0.4f, UIColors.Intellect);
            Panel(V(27f, 4.6f, 61.8f), 180f, 4f, 0.4f, UIColors.Speed);
        }

        /// <summary>Wall-mounted status lamp with a colored label, green when the lane result is in.</summary>
        void LaneLamp(Transform s, Vector3 pos, string label, Color color, SignalSource src)
        {
            PuzzleKit.IndicatorLight(s, pos, Yaw(180f), true, src);
            _ctx.Label(s, pos + V(0f, 0.55f, -0.06f), Quaternion.identity, label, 0.4f, color, 2f);
        }
    }
}
