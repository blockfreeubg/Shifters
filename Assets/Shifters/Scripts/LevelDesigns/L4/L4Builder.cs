// =====================================================================================
//  SHIFTERS - L4Builder.cs   [owned by LEVEL 4]
//  LEVEL 4 - "THE IQ TEST" (Research theme). Floating white/violet exam chambers in a void.
//    S0 Spawn ............ z -8..10   tip: switch to your smartest intern
//    S1 Entrance Exam .... z 10..26   IQ-7 terminal on a 2.5 m ledge (stack/throw the Brainiac) -> gate   C1
//    S2 Hidden Pathway ... z 26..42   32 m invisible hologram path, 3 turns, IQ view only              C2
//    S3 Switch Sequence .. z 50..66   4 symbol buttons, IQ-only order numbers, gun-only bonus target    C3
//    S4 Logic Gates ...... z 82..102  (A AND B) AND NOT C, 100 kg plates (ALL THREE), security latch   C4
//    S5 Final Exam ....... z 113..182 STR block->150 plate, SPD timed door, IQ terminal -> EXIT
//  Coordinates are world space (sections are grouping transforms at the origin).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Code-defined Level 4 ("THE IQ TEST", LevelCatalog index 3, scene L4_IQTest).</summary>
    public partial class L4Builder : ILevelBuilder
    {
        // gamepad prompts (integrator2, gamepad1): holo hint whose key follows the device (DevicePromptText)
        const string BrainHolo = "HINT: THE BRUTE\nCAN LIFT A BRAIN.\n({GRAB} TO PICK UP)";

        public int LevelIndex => 3;

        // ------------------------------------------------------------------ shared metrics (also used by L4Solution)
        public const float LedgeHeight = 2.5f;
        /// <summary>Hidden path corner points (top surface centreline).</summary>
        public static readonly Vector3[] PathCorners =
        {
            new Vector3(0f, 0f, 26f), new Vector3(0f, 0f, 30f), new Vector3(10f, 0f, 30f),
            new Vector3(10f, 0f, 42f), new Vector3(4f, 0f, 42f)
        };
        public const float PathWidth = 2f;

        LevelBuildContext _ctx;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            BuildVoid(ctx);
            BuildS0Spawn(ctx);
            BuildS1Entrance(ctx);
            BuildS2HiddenPath(ctx);
            BuildS3Sequence(ctx);
            BuildS4Logic(ctx);
            BuildS5FinalExam(ctx);
            BuildAmbience(ctx);
            _ctx = null;
        }

        // ================================================================== void + backdrop
        void BuildVoid(LevelBuildContext ctx)
        {
            var kz = PuzzleKit.KillZone(ctx.Hazards, new Vector3(-2f, -14f, 85f), Quaternion.identity, new Vector3(280f, 40f, 420f),
                KillZoneType.Void, null, "fell into the void of unanswered questions");
            ctx.Mark("Void", kz.gameObject);
            EnvKit.VoidBackdrop(ctx.Decor, LevelTheme.Research, new Bounds(new Vector3(-3f, 0f, 87f), new Vector3(44f, 24f, 200f)), 4);
        }

        // ================================================================== S0 spawn
        void BuildS0Spawn(LevelBuildContext ctx)
        {
            var s = ctx.Section("S0_Spawn", Vector3.zero);
            // Island A: spawn + entrance exam (x -10..10, z -8..26)
            EnvKit.Platform(ctx.Geometry, new Vector3(0f, 0f, 9f), new Vector3(20f, 2.4f, 34f), true);
            ctx.SetSpawn(new Vector3(0f, 0f, -3.5f), 0f);

            ctx.Tutorial(new Vector3(0f, 1.5f, -2.5f), new Vector3(12f, 3f, 8f), "l4_smart", "THE IQ TEST",
                "SHIFT to your smartest intern to see what others can't. Press {Scan} to scan the room with whoever you are.",
                new[] { "1", "2", "3", "R" }, false);

            // welcome arch
            EnvKit.Decor.GridPillar(s, new Vector3(-4.6f, 0f, 0.6f), 5.6f);
            EnvKit.Decor.GridPillar(s, new Vector3(4.6f, 0f, 0.6f), 5.6f);
            EnvKit.Sign(s, new Vector3(0f, 5.0f, 0.6f), Quaternion.identity, "WELCOME TO THE IQ TEST", new Vector2(8.6f, 1.15f), SignStyle.Info);
            EnvKit.Sign(s, new Vector3(0f, 3.95f, 0.6f), Quaternion.identity, "YOUR EXAM SHIFT HAS BEGUN", new Vector2(5.6f, 0.55f), SignStyle.Arrow);

            // the control group
            L4Decor.FaceBrainJarPlate(EnvKit.Decor.BrainJar(s, new Vector3(-6.6f, 0f, 3.2f), 1.35f), Vector3.right);   // "IQ 9000" faces the path
            EnvKit.Sign(s, new Vector3(-6.6f, 0.45f, 1.95f), Quaternion.identity, "PLEASE DO NOT LICK THE BRAIN", new Vector2(2.4f, 0.42f), SignStyle.Warning);
            EnvKit.Decor.HoloPanel(s, new Vector3(-6.8f, 0f, -4.6f), Quaternion.Euler(0f, 255f, 0f), new Vector2(2.6f, 1.4f), "YOUR IQ HAS BEEN NOTED.\nAND FILED UNDER\n'ADORABLE'.");
            EnvKit.Decor.DeskWithMonitors(s, new Vector3(8.0f, 0f, -4.8f), Quaternion.Euler(0f, -90f, 0f));
            EnvKit.Decor.Whiteboard(s, new Vector3(8.4f, 0f, -0.8f), Quaternion.Euler(0f, 70f, 0f), "IQ TEST Q1:\n2 + 2 = ?\nA) 4  B) FISH  C) YES\n(CORRECT: B)");
            EnvKit.Decor.Plant(s, new Vector3(8.6f, 0f, -6.6f), 1);
            EnvKit.Decor.Plant(s, new Vector3(-8.6f, 0f, -6.6f), 2);
            EnvKit.Sign(s, new Vector3(-8.9f, 2.3f, 7.8f), Quaternion.Euler(0f, -72f, 0f), "EXAM IN PROGRESS: SILENCE", new Vector2(3.6f, 0.6f), SignStyle.Danger);
            EnvKit.Pillar(s, new Vector3(-8.9f, 0f, 7.8f), 2.0f, 0.18f);
            EnvKit.Decor.HoloPanel(s, new Vector3(-8.4f, 0f, -6.2f), Quaternion.Euler(0f, 252f, 0f), new Vector2(2.4f, 1.3f), "DAYS WITHOUT A\nSHIFT ACCIDENT: 0\nRECORD: 0");

            // IQ-only teaser: proves the perspective right at the start
            L4Decor.FloorClue(s, new Vector3(0f, 0f, 1.6f), 0f, "PSST. ONLY SMART EYES CAN READ THIS.\nTHE REST OF THEM SEE A FLOOR.", 0.34f);
            L4Decor.FloorText(ctx, s, new Vector3(0f, 0f, -6.2f), 0f, "INTERN LINE-UP", 0.55f, UIColors.Intellect);
        }

        // ================================================================== S1 entrance exam
        void BuildS1Entrance(LevelBuildContext ctx)
        {
            var s = ctx.Section("S1_EntranceExam", Vector3.zero);
            // gate wall across the island (z = 14), doorway x -2.5..2.5 (door frame fills it)
            L4Decor.WallWithGap(ctx.Geometry, 14f, -10.3f, 10.3f, -2.5f, 2.5f, 5.6f);

            // 2.5 m ledge (balcony) against the wall, right of the gate: x 3..9, z 10..13.75
            ctx.Platform(ctx.Geometry, new Vector3(6.4f, LedgeHeight, 11.875f), new Vector3(6.8f, LedgeHeight, 3.75f), MatKey.FloorAlt, true, "ExamLedge");
            EnvKit.HazardStripe(s, new Vector3(6.4f, LedgeHeight - 0.12f, 9.985f), new Vector3(6.8f, 0.2f, 0.04f));
            var term = PuzzleKit.Terminal(ctx.Puzzles, new Vector3(8.7f, LedgeHeight, 13.05f), Quaternion.Euler(0f, 180f, 0f), Tuning.TerminalIQ, true, 6f,
                "ENTRANCE EXAM PASSED. PROBABLY CHEATED.");
            ctx.Mark("S1.Terminal", term.gameObject);
            var gate = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 14f), Quaternion.identity, DoorMode.SlideUp, 4f, 4.2f, term);
            ctx.Mark("S1.Gate", gate.gameObject);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(8.7f, LedgeHeight, 13.4f), new Vector3(3.1f, LedgeHeight, 13.4f), term);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(2.9f, 0f, 9.7f), new Vector3(2.6f, 0f, 13.4f), term);

            ctx.MarkPoint("S1.LedgeTop", new Vector3(5.0f, LedgeHeight, 11.4f));
            ctx.MarkPoint("S1.StackSpot", new Vector3(5.0f, 0f, 9.35f));
            ctx.MarkPoint("S1.ThrowPrep", new Vector3(5.2f, 0f, 3.9f));
            ctx.MarkPoint("S1.ThrowSpot", new Vector3(5.2f, 0f, 2.7f));
            ctx.MarkPoint("S1.LedgeExit", new Vector3(3.6f, LedgeHeight, 11.6f));
            ctx.MarkPoint("S1.GateNear", new Vector3(0f, 0f, 11.5f));
            ctx.MarkPoint("S1.GateFar", new Vector3(0f, 0f, 17f));

            // signage
            EnvKit.Sign(s, new Vector3(6.4f, 1.35f, 9.94f), Quaternion.identity, "ENTRANCE EXAM\nQ1: HOW DO YOU GET UP HERE?", new Vector2(3.8f, 1.15f), SignStyle.Info);
            EnvKit.Sign(s, new Vector3(0f, 5.15f, 13.7f), Quaternion.identity, "ENTRANCE EXAM", new Vector2(4.2f, 0.75f), SignStyle.Exit);
            PerspectiveKit.Clue(s, new Vector3(6.6f, 4.25f, 13.72f), Quaternion.identity, "TERMINAL UP HERE.\nNEEDS A BRAIN + A BOOST.", 0.3f);
            EnvKit.Poster(s, new Vector3(-7.4f, 2.6f, 13.72f), Quaternion.identity, "THINK HARDER", "THEN THINK HARDER AGAIN");
            EnvKit.Poster(s, new Vector3(-4.6f, 2.6f, 13.72f), Quaternion.identity, "STANDING ON COWORKERS", "IS ENCOURAGED. FOR SCIENCE.");
            // EnvKit mounted pieces (Sign, Poster, LightPanel, HoloPanel...): rot = the direction the VIEWER looks
            EnvKit.LightPanel(ctx.Lights, new Vector3(-6f, 4.6f, 13.7f), Quaternion.identity, new Vector2(2.4f, 0.35f), UIColors.Intellect, false);
            DevicePromptText.AttachInChildren(EnvKit.Decor.HoloPanel(s, new Vector3(-6.5f, 0f, 9.5f), Quaternion.Euler(0f, -55f, 0f), new Vector2(2.6f, 1.3f), InputPrompts.Format(BrainHolo, false, false)), BrainHolo);

            ctx.Hint(new Vector3(3f, 2f, 8.5f), new Vector3(18f, 5f, 11f),
                "The terminal needs IQ 7 and sits on a 2.5 m ledge. Have the Brute pick up the Brainiac ({Grab}) and put them down on a teammate standing by the ledge - or just throw them up ({Throw}). Then hack ({Interact}).",
                "Some minds need elevating. Literally.");

            // C1 beyond the gate
            ctx.Checkpoint(new Vector3(0f, 0f, 20f), 0f);
            EnvKit.Decor.Bench(s, new Vector3(-6.5f, 0f, 19f), Quaternion.Euler(0f, 90f, 0f), 2f);
            EnvKit.Decor.HoloPanel(s, new Vector3(6.6f, 0f, 21.5f), Quaternion.Euler(0f, 56f, 0f), new Vector2(2.4f, 1.3f), "CONGRATULATIONS.\nYOU OPENED A DOOR.\nMOST INTERNS DON'T.");
        }

        // ================================================================== S2 hidden pathway
        void BuildS2HiddenPath(LevelBuildContext ctx)
        {
            var s = ctx.Section("S2_HiddenPathway", Vector3.zero);
            var tiles = L4Decor.HoloWalkway(ctx.Puzzles, PathCorners, PathWidth, 2f);
            if (tiles.Length > 0) ctx.Mark("S2.Path", tiles[0].transform.parent.gameObject);
            for (int i = 0; i < PathCorners.Length; i++) ctx.MarkPoint("S2.P" + i, PathCorners[i]);
            ctx.MarkPoint("S2.Start", new Vector3(0f, 0f, 24.6f));
            ctx.MarkPoint("S2.Arrive", new Vector3(1.6f, 0f, 42f));

            // entry: a cheerful lack of bridge
            EnvKit.Sign(s, new Vector3(-3.4f, 1.9f, 25.4f), Quaternion.identity, "BRIDGE (PROBABLY)", new Vector2(3.0f, 0.6f), SignStyle.Warning);
            EnvKit.Pillar(s, new Vector3(-3.4f, 0f, 25.45f), 1.55f, 0.18f);
            EnvKit.Sign(s, new Vector3(3.4f, 1.9f, 25.4f), Quaternion.identity, "SHIFT TO A SMART INTERN FIRST", new Vector2(3.6f, 0.6f), SignStyle.Danger);
            EnvKit.Pillar(s, new Vector3(3.4f, 0f, 25.45f), 1.55f, 0.18f);
            L4Decor.FloorClue(s, new Vector3(0f, 0f, 24.4f), 0f, "START HERE. TRUST THE TILES.", 0.34f);
            L4Decor.FloorText(ctx, s, new Vector3(0f, 0f, 22.9f), 0f, "FALLING IS NOT A VALID ANSWER", 0.36f, UIColors.Danger);
            // far side greeting (readable from the path)
            EnvKit.Sign(s, new Vector3(-9.2f, 2.9f, 49.68f), Quaternion.identity, "YOU MADE IT.\nWE ARE AS SURPRISED AS YOU.", new Vector2(4.4f, 1.0f), SignStyle.Info);

            ctx.Tutorial(new Vector3(0f, 1.5f, 23.2f), new Vector3(10f, 3f, 5f), "l4_path", "HIDDEN PATHWAY",
                "Only BIG BRAIN interns can see hologram tiles. Tiles they step on stay visible to everyone for 8 seconds. Press {AIMode} and the others FOLLOW your steps.",
                new[] { "G" }, false);
            ctx.Hint(new Vector3(2f, 2f, 24f), new Vector3(22f, 5f, 8f),
                "Switch to the Brainiac: the hologram path starts at the middle of the edge, goes 4 m forward, 10 m RIGHT, 12 m forward, then 6 m LEFT. Walk it first, then have the others FOLLOW ({AIMode}) while the tiles glow.",
                "The bridge is there. Probably. Ask someone smart to look at it.");

            // C2 on the far island (x -12..4, z 39.5..77)
            EnvKit.Platform(ctx.Geometry, new Vector3(-4f, 0f, 58.25f), new Vector3(16f, 2.4f, 37.5f), true);
            ctx.Checkpoint(new Vector3(-4f, 0f, 45f), 0f);
            ctx.MarkPoint("S2.Gather", new Vector3(-4f, 0f, 45.2f));
            EnvKit.Decor.HoloPanel(s, new Vector3(-10f, 0f, 42.5f), Quaternion.Euler(0f, -112f, 0f), new Vector2(2.4f, 1.3f), "HOLOGRAM PATH:\n100% SOLID*\n*SOLIDITY NOT GUARANTEED");
            EnvKit.Decor.Plant(s, new Vector3(1.8f, 0f, 47.8f), 0);
            EnvKit.Decor.Bench(s, new Vector3(-10.4f, 0f, 46.5f), Quaternion.Euler(0f, 90f, 0f), 2f);
        }

        // ================================================================== ambience
        void BuildAmbience(LevelBuildContext ctx)
        {
            var d = ctx.Section("Ambience", Vector3.zero);
            L4Decor.FloatingCluster(d, new Vector3(0f, 4f, 6f), 21f, 9, 11, 1.2f, 2.8f);
            L4Decor.FloatingCluster(d, new Vector3(22f, 2f, 40f), 9f, 6, 12, 1.0f, 2.2f);
            L4Decor.FloatingCluster(d, new Vector3(-24f, 3f, 60f), 10f, 7, 13, 1.2f, 3.0f);
            L4Decor.FloatingCluster(d, new Vector3(16f, 5f, 90f), 9f, 6, 14, 1.2f, 2.6f);
            L4Decor.FloatingCluster(d, new Vector3(-22f, 4f, 120f), 10f, 7, 15, 1.2f, 3.0f);
            L4Decor.FloatingCluster(d, new Vector3(16f, 3f, 155f), 10f, 7, 16, 1.2f, 3.0f);
            L4Decor.FloatingCluster(d, new Vector3(-4f, 12f, 190f), 12f, 6, 17, 2.0f, 4.0f);
            // a few giant hero shapes high up for silhouette
            EnvKit.Decor.FloatingShape(d, new Vector3(-20f, 14f, 20f), 6f, 3);
            EnvKit.Decor.FloatingShape(d, new Vector3(26f, 16f, 70f), 7f, 2);
            EnvKit.Decor.FloatingShape(d, new Vector3(-26f, 18f, 140f), 8f, 0);
        }
    }
}
