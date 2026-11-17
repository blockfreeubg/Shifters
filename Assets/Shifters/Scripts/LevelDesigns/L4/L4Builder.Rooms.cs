// =====================================================================================
//  SHIFTERS - L4Builder.Rooms.cs   [owned by LEVEL 4]
//  S3 Switch Sequence, S4 Logic Gates, S5 Final Exam (+ exit). See L4Builder.cs for the map.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L4Builder
    {
        /// <summary>Correct button order of the Switch Sequence (shown by IQ-only wall numbers).</summary>
        public static readonly L4Symbol[] SequenceOrder = { L4Symbol.Circle, L4Symbol.Star, L4Symbol.Triangle, L4Symbol.Square };

        const float RoomWest = -11.75f, RoomEast = 3.75f;   // wall centre lines (inner faces -11.5 / 3.5)

        // ================================================================== S3 switch sequence
        void BuildS3Sequence(LevelBuildContext ctx)
        {
            var s = ctx.Section("S3_SwitchSequence", Vector3.zero);
            var g = ctx.Geometry;
            // room z 50..66 on island B
            L4Decor.WallWithGap(g, 50f, -12f, 4f, -6.5f, -1.5f, 5f);
            ctx.Block(g, new Vector3(-4f, 5.0f, 50f), new Vector3(5.4f, 0.6f, 0.6f), MatKey.MetalDark, "EntryHeader");
            L4Decor.Wall(g, RoomWest, 50f, RoomWest, 66f, 5f);
            L4Decor.Wall(g, RoomEast, 50f, RoomEast, 56.1f, 5f);
            L4Decor.Wall(g, RoomEast, 59.9f, RoomEast, 66f, 5f);
            L4Decor.WallWithGap(g, 66f, -12f, 4f, -6.5f, -1.5f, 5f);
            EnvKit.Sign(s, new Vector3(-4f, 5.0f, 49.66f), Quaternion.identity, "THE SWITCH SEQUENCE", new Vector2(5.0f, 0.55f), SignStyle.Info);

            // four big buttons with symbol panels; IQ-only order numbers above each panel
            var buttons = new PushButton[4];
            void Station(L4Symbol sym, bool west, float z)
            {
                float yaw = west ? 90f : -90f;
                float inner = west ? RoomWest + 0.25f : RoomEast - 0.25f;
                float inward = west ? 1f : -1f;
                var b = PuzzleKit.PushButton(ctx.Puzzles, new Vector3(inner + inward * 0.95f, 0f, z), Quaternion.Euler(0f, yaw, 0f), 3f);
                b.name = "Button_" + L4Decor.SymbolName(sym);
                buttons[(int)sym] = b;
                ctx.Mark("S3.Btn." + L4Decor.SymbolName(sym), b.gameObject);
                L4Decor.SymbolPanel(s, new Vector3(inner + inward * 0.06f, 2.55f, z), Quaternion.Euler(0f, yaw, 0f), sym, 1.5f);
                int order = System.Array.IndexOf(SequenceOrder, sym) + 1;
                PerspectiveKit.Clue(s, new Vector3(inner + inward * 0.03f, 4.15f, z), Quaternion.Euler(0f, west ? -90f : 90f, 0f), order.ToString(), 0.85f);
                L4Decor.FloorText(ctx, s, new Vector3(inner + inward * 2.2f, 0f, z), west ? -90f : 90f, L4Decor.SymbolName(sym), 0.42f, L4Decor.SymbolColor(sym));
            }
            Station(L4Symbol.Triangle, true, 54f);
            Station(L4Symbol.Circle, true, 62f);
            Station(L4Symbol.Square, false, 54f);
            Station(L4Symbol.Star, false, 62f);

            var order = new SignalSource[SequenceOrder.Length];
            for (int i = 0; i < order.Length; i++) order[i] = buttons[(int)SequenceOrder[i]];
            var seq = PuzzleKit.SequenceLock(ctx.Puzzles, new Vector3(-0.1f, 0f, 64.7f), Quaternion.Euler(0f, 180f, 0f), order, false, "SEQUENCE ACCEPTED. +2 IQ (REFUNDED).");
            seq.WrongToast = "INCORRECT. -2 IQ";
            ctx.Mark("S3.Lock", seq.gameObject);
            var door = PuzzleKit.Door(ctx.Puzzles, new Vector3(-4f, 0f, 66f), Quaternion.identity, DoorMode.SlideUp, 4f, 4.2f, seq);
            ctx.Mark("S3.Door", door.gameObject);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(-0.1f, 0f, 64.7f), new Vector3(-1.9f, 0f, 65.4f), seq);

            // gun-only hidden target -> bonus closet in the east wall
            var target = PuzzleKit.ShootTarget(ctx.Puzzles, new Vector3(1.9f, 4.05f, 65.6f), Quaternion.Euler(0f, 180f, 0f), ShootTargetMode.Latch, 4f, false,
                "BONUS ROOM UNLOCKED. WHY ARE YOU SHOOTING WALLS?");
            ctx.Mark("S3.Target", target.gameObject);
            PerspectiveKit.ShootHint(ctx.Puzzles, new Vector3(1.9f, 4.05f, 65.42f), 0.62f);
            EnvKit.Poster(s, new Vector3(1.9f, 2.25f, 65.73f), Quaternion.identity, "PLEASE DO NOT SHOOT", "THE WALL. ESPECIALLY THE TOP RIGHT BIT.", new Vector2(1.2f, 1.5f));
            var bonusDoor = PuzzleKit.Door(ctx.Puzzles, new Vector3(RoomEast, 0f, 58f), Quaternion.Euler(0f, 90f, 0f), DoorMode.SlideUp, 2.8f, 4.0f, target);
            ctx.Mark("S3.BonusDoor", bonusDoor.gameObject);
            BuildBonusCloset(ctx, s);

            // dressing
            EnvKit.Decor.GridPillar(s, new Vector3(-11.1f, 0f, 50.6f), 5f);
            EnvKit.Decor.GridPillar(s, new Vector3(3.1f, 0f, 50.6f), 5f);
            EnvKit.Decor.GridPillar(s, new Vector3(-11.1f, 0f, 65.4f), 5f);
            EnvKit.Sign(s, new Vector3(-4f, 4.95f, 65.7f), Quaternion.identity, "WRONG ANSWERS WILL BE DEDUCTED FROM YOUR IQ", new Vector2(6.0f, 0.42f), SignStyle.Warning);
            L4Decor.FloorText(ctx, s, new Vector3(-4f, 0f, 57.5f), 0f, "CHOOSE WISELY", 0.8f, UIColors.Intellect);
            PerspectiveKit.Clue(s, new Vector3(-8.6f, 3.3f, 65.72f), Quaternion.identity, "THE NUMBERS\nARE THE ORDER.\nYOU'RE WELCOME.", 0.28f);
            // light panels follow the mounted-piece convention: rot = the direction the viewer looks (into the wall)
            EnvKit.LightPanel(ctx.Lights, new Vector3(RoomWest + 0.27f, 4.3f, 58f), Quaternion.Euler(0f, -90f, 0f), new Vector2(3f, 0.35f), UIColors.Intellect, true, 2.4f, 14f);
            EnvKit.LightPanel(ctx.Lights, new Vector3(RoomEast - 0.27f, 4.3f, 63f), Quaternion.Euler(0f, 90f, 0f), new Vector2(2.4f, 0.35f), UIColors.Intellect, false);

            ctx.Hint(new Vector3(-4f, 2f, 58f), new Vector3(15f, 5f, 15.5f),
                "IQ view shows a number above each symbol. Press the buttons (E) in that order: CIRCLE, STAR, TRIANGLE, SQUARE. A wrong press resets the sequence.",
                "There's an order to things. The smart one can see it. The rest of you can guess. 24 times.");

            ctx.Checkpoint(new Vector3(-4f, 0f, 71f), 0f);
            ctx.MarkPoint("S3.Beyond", new Vector3(-4f, 0f, 68.5f));
            ctx.MarkPoint("S3.Gather", new Vector3(-4f, 0f, 71.2f));
            EnvKit.Decor.HoloPanel(s, new Vector3(1.2f, 0f, 72.5f), Quaternion.Euler(0f, 74f, 0f), new Vector2(2.4f, 1.3f), "FUN FACT:\nTHE BUTTONS WERE\nALL THE SAME BUTTON.");
            // walkway to island C
            ctx.Platform(g, new Vector3(-4f, 0f, 78.5f), new Vector3(5f, 0.5f, 3.6f), MatKey.Grate, true, "WalkwayBC");
            EnvKit.Railing(s, new Vector3(-6.4f, 0f, 76.9f), new Vector3(-6.4f, 0f, 80.1f));
            EnvKit.Railing(s, new Vector3(-1.6f, 0f, 76.9f), new Vector3(-1.6f, 0f, 80.1f));
        }

        void BuildBonusCloset(LevelBuildContext ctx, Transform s)
        {
            var g = ctx.Geometry;
            ctx.Platform(g, new Vector3(6.6f, 0f, 58f), new Vector3(5.2f, 1.2f, 6.4f), MatKey.FloorAlt, true, "BonusClosetFloor");
            L4Decor.Wall(g, 9.0f, 54.8f, 9.0f, 61.2f, 4.6f, MatKey.WallAlt);
            L4Decor.Wall(g, 4.0f, 55.0f, 9.2f, 55.0f, 4.6f, MatKey.WallAlt);
            L4Decor.Wall(g, 4.0f, 61.0f, 9.2f, 61.0f, 4.6f, MatKey.WallAlt);
            var trophy = EnvKit.Decor.FloatingShape(s, new Vector3(6.9f, 1.9f, 58f), 0.9f, 3);
            trophy.name = "GoldenTorusOfMerit";
            EnvKit.Decor.Barrel(s, new Vector3(8.2f, 0f, 56.2f), 1);
            EnvKit.Sign(s, new Vector3(8.72f, 2.9f, 58f), Quaternion.Euler(0f, 90f, 0f), "BONUS ROOM!\nTHE PRIZE IS THIS SIGN.", new Vector2(3.2f, 1.1f), SignStyle.Exit);
            ctx.MarkPoint("S3.BonusInside", new Vector3(6.4f, 0f, 58f));
        }

        // ================================================================== S4 logic gates
        void BuildS4Logic(LevelBuildContext ctx)
        {
            var s = ctx.Section("S4_LogicGates", Vector3.zero);
            var g = ctx.Geometry;
            EnvKit.Platform(g, new Vector3(-4f, 0f, 95f), new Vector3(16f, 2.4f, 30f), true);   // island C z 80..110
            L4Decor.WallWithGap(g, 82f, -12f, 4f, -6.5f, -1.5f, 5f);
            ctx.Block(g, new Vector3(-4f, 5.0f, 82f), new Vector3(5.4f, 0.6f, 0.6f), MatKey.MetalDark, "EntryHeader");
            L4Decor.Wall(g, RoomWest, 82f, RoomWest, 102f, 5f);
            L4Decor.Wall(g, RoomEast, 82f, RoomEast, 102f, 5f);
            L4Decor.WallWithGap(g, 102f, -12f, 4f, -6.5f, -1.5f, 5f);
            // vestibule: the only way to the door crosses plate C
            L4Decor.Wall(g, -6.25f, 98.2f, -6.25f, 101.55f, 4.6f, MatKey.WallAlt);
            L4Decor.Wall(g, -1.75f, 98.2f, -1.75f, 101.55f, 4.6f, MatKey.WallAlt);
            EnvKit.Sign(s, new Vector3(-4f, 5.0f, 81.66f), Quaternion.identity, "LOGIC LAB - CHECK EMOTIONS AT THE DOOR", new Vector2(6.2f, 0.55f), SignStyle.Info);

            var p = ctx.Puzzles;
            var A = PuzzleKit.PressurePlate(p, new Vector3(-9.3f, 0f, 89f), null, Tuning.PlateMedium, false);
            var B = PuzzleKit.PressurePlate(p, new Vector3(1.3f, 0f, 89f), null, Tuning.PlateMedium, false);
            var D = PuzzleKit.PressurePlate(p, new Vector3(-4f, 0f, 86.2f), null, Tuning.PlateLight, false);
            var C1 = PuzzleKit.PressurePlate(p, new Vector3(-5f, 0f, 99.6f), null, Tuning.PlateLight, false);
            var C2 = PuzzleKit.PressurePlate(p, new Vector3(-3f, 0f, 99.6f), null, Tuning.PlateLight, false);
            A.name = "Plate_A"; B.name = "Plate_B"; D.name = "Plate_D_Decoy"; C1.name = "Plate_C_Left"; C2.name = "Plate_C_Right";
            ctx.Mark("S4.A", A.gameObject); ctx.Mark("S4.B", B.gameObject); ctx.Mark("S4.D", D.gameObject);
            ctx.Mark("S4.C1", C1.gameObject); ctx.Mark("S4.C2", C2.gameObject);

            var gAB = PuzzleKit.LogicGate(p, new Vector3(-4f, 0f, 93.2f), Quaternion.Euler(0f, 180f, 0f), SignalLogic.All, false, A, B);
            var gNotC = PuzzleKit.LogicGate(p, new Vector3(-9.2f, 0f, 96.6f), Quaternion.Euler(0f, 180f, 0f), SignalLogic.Any, true, C1, C2);
            if (gNotC.Symbol != null) gNotC.Symbol.text = "NOT C";
            var gFinal = PuzzleKit.LogicGate(p, new Vector3(1.0f, 0f, 96.6f), Quaternion.Euler(0f, 180f, 0f), SignalLogic.All, false, gAB, gNotC);
            gAB.name = "Gate_A_AND_B"; gNotC.name = "Gate_NOT_C"; gFinal.name = "Gate_FINAL_AND";
            ctx.Mark("S4.GateAB", gAB.gameObject); ctx.Mark("S4.GateNotC", gNotC.gameObject); ctx.Mark("S4.GateFinal", gFinal.gameObject);
            var latch = L4SecurityLatch.Create(p, new Vector3(1.4f, 2.75f, 101.62f), Quaternion.Euler(0f, 180f, 0f), gFinal, 2f);
            ctx.Mark("S4.Latch", latch.gameObject);
            var door = PuzzleKit.Door(p, new Vector3(-4f, 0f, 102f), Quaternion.identity, DoorMode.SlideUp, 4f, 4.2f, latch);
            ctx.Mark("S4.Door", door.gameObject);

            // glowing cables (readability: what powers what)
            PuzzleKit.Cable(p, new Vector3(-9.3f, 0f, 89f), new Vector3(-4.35f, 0f, 93.0f), A);
            PuzzleKit.Cable(p, new Vector3(1.3f, 0f, 89f), new Vector3(-3.65f, 0f, 93.0f), B);
            PuzzleKit.Cable(p, new Vector3(-4f, 0f, 93.2f), new Vector3(0.8f, 0f, 96.4f), gAB);
            PuzzleKit.Cable(p, new Vector3(-5f, 0f, 99.6f), new Vector3(-5f, 0f, 97.75f), C1);
            PuzzleKit.Cable(p, new Vector3(-5f, 0f, 97.75f), new Vector3(-9.05f, 0f, 96.75f), C1);
            PuzzleKit.Cable(p, new Vector3(-3f, 0f, 99.6f), new Vector3(-3f, 0f, 97.45f), C2);
            PuzzleKit.Cable(p, new Vector3(-3f, 0f, 97.45f), new Vector3(-9.35f, 0f, 96.45f), C2);
            PuzzleKit.Cable(p, new Vector3(-9.2f, 0f, 96.6f), new Vector3(1.2f, 0f, 96.75f), gNotC);
            PuzzleKit.Cable(p, new Vector3(1.0f, 0f, 96.6f), new Vector3(1.4f, 2.27f, 101.45f), gFinal);
            PuzzleKit.Cable(p, new Vector3(1.4f, 0f, 101.35f), new Vector3(-1.6f, 0f, 101.35f), latch);
            // the decoy's cable goes nowhere
            PuzzleKit.Cable(p, new Vector3(-4f, 0f, 86.2f), new Vector3(-11.25f, 0f, 84.2f), D);
            Kit.BoxVisual(s, "NowhereBox", new Vector3(-11.3f, 0.35f, 84.2f), new Vector3(0.35f, 0.7f, 0.6f), MatKey.PuzzleBody, 0.03f);
            PerspectiveKit.Clue(s, new Vector3(-11.47f, 1.8f, 84.2f), Quaternion.Euler(0f, -90f, 0f), "THIS CABLE\nGOES NOWHERE.", 0.22f);   // above the wall's accent strip

            // labels (everyone) + logic (IQ only)
            EnvKit.Sign(s, new Vector3(RoomWest + 0.29f, 3.3f, 89f), Quaternion.Euler(0f, -90f, 0f), "A", new Vector2(1.1f, 1.1f), SignStyle.Info);
            EnvKit.Sign(s, new Vector3(RoomEast - 0.29f, 3.3f, 89f), Quaternion.Euler(0f, 90f, 0f), "B", new Vector2(1.1f, 1.1f), SignStyle.Info);
            L4Decor.FloorText(ctx, s, new Vector3(-9.3f, 0f, 87.35f), 0f, "A", 0.9f, UIColors.Text);
            L4Decor.FloorText(ctx, s, new Vector3(1.3f, 0f, 87.35f), 0f, "B", 0.9f, UIColors.Text);
            L4Decor.FloorText(ctx, s, new Vector3(-4f, 0f, 84.75f), 0f, "D", 0.9f, UIColors.TextMuted);
            L4Decor.FloorText(ctx, s, new Vector3(-4f, 0f, 97.75f), 0f, "C", 0.9f, UIColors.Danger);
            L4Decor.FloorClue(s, new Vector3(-4f, 0f, 84.05f), 0f, "D: DOES NOTHING. LIKE GARY.", 0.3f);
            L4Decor.FloorClue(s, new Vector3(-4f, 0f, 97.0f), 0f, "C IS THE 'NOT'. KEEP OFF UNTIL LATCHED.", 0.26f);
            PerspectiveKit.Clue(s, new Vector3(-8.6f, 3.35f, 101.73f), Quaternion.identity, "DOOR = (A AND B)\nAND NOT C.\nOPEN 2 S =\nLATCHED FOREVER", 0.3f);   // clear of the corner pillar
            EnvKit.Poster(s, new Vector3(RoomWest + 0.27f, 2.4f, 94f), Quaternion.Euler(0f, -90f, 0f), "LOGIC", "IT'S LIKE FEELINGS, BUT CORRECT");
            EnvKit.Poster(s, new Vector3(RoomEast - 0.27f, 2.4f, 94f), Quaternion.Euler(0f, 90f, 0f), "TWO HEADS ARE BETTER", "THREE ARE A SAFETY VIOLATION");
            EnvKit.Poster(s, new Vector3(RoomWest + 0.27f, 2.4f, 98.5f), Quaternion.Euler(0f, -90f, 0f), "DO NOT SHIFT THE BLAME", "(OR THE INTERNS)");
            EnvKit.LightPanel(ctx.Lights, new Vector3(RoomWest + 0.27f, 4.3f, 91f), Quaternion.Euler(0f, -90f, 0f), new Vector2(3f, 0.35f), UIColors.Intellect, false);
            EnvKit.Decor.GridPillar(s, new Vector3(-11.1f, 0f, 101.3f), 5f);
            EnvKit.Decor.GridPillar(s, new Vector3(3.1f, 0f, 101.3f), 5f);

            ctx.MarkPoint("S4.APos", new Vector3(-9.3f, 0f, 89f));
            ctx.MarkPoint("S4.BLeft", new Vector3(0.85f, 0f, 89f));
            ctx.MarkPoint("S4.BRight", new Vector3(1.78f, 0f, 89f));
            ctx.MarkPoint("S4.BApproachEast", new Vector3(2.9f, 0f, 89f));
            ctx.MarkPoint("S4.Entry", new Vector3(-4f, 0f, 83.5f));
            ctx.MarkPoint("S4.Vestibule", new Vector3(-4f, 0f, 97.9f));
            ctx.MarkPoint("S4.Beyond", new Vector3(-4f, 0f, 104f));

            ctx.Tutorial(new Vector3(-4f, 1.5f, 84.4f), new Vector3(9f, 3f, 4f), "l4_logic", "LOGIC GATES",
                "Glowing cables show what powers what. Gate boxes combine their inputs. Smart interns can read the whole formula.", null, false);
            ctx.Hint(new Vector3(-4f, 2f, 92f), new Vector3(15f, 5f, 19f),
                "DOOR = (A AND B) AND NOT C. A and B each need 100 kg: the Brute on one, the Sprinter and Brainiac together on the other. Keep everyone off C until the SECURITY LATCH engages (2 s), then walk through.",
                "Two plates want company. One plate wants to be left alone. One plate is just there for emotional support.");

            ctx.Checkpoint(new Vector3(-4f, 0f, 106.5f), 0f);
            ctx.MarkPoint("S4.Gather", new Vector3(-4f, 0f, 106.7f));
            ctx.Platform(g, new Vector3(-4f, 0f, 111.5f), new Vector3(5f, 0.5f, 3.6f), MatKey.Grate, true, "WalkwayCD");
            EnvKit.Railing(s, new Vector3(-6.4f, 0f, 109.9f), new Vector3(-6.4f, 0f, 113.1f));
            EnvKit.Railing(s, new Vector3(-1.6f, 0f, 109.9f), new Vector3(-1.6f, 0f, 113.1f));
        }

        // ================================================================== S5 final exam
        void BuildS5FinalExam(LevelBuildContext ctx)
        {
            var s = ctx.Section("S5_FinalExam", Vector3.zero);
            var g = ctx.Geometry;
            var p = ctx.Puzzles;
            const float plateZ = 126.25f;
            // one continuous collider under the whole push lane (tiled colliders make sliding blocks snag on seams)
            EnvKit.Platform(g, new Vector3(-4f, 0f, 147.5f), new Vector3(16f, 2.4f, 69f), true);   // island D z 113..182

            // ---------------- stage 1: STRENGTH (z 113..130)
            L4Decor.Wall(g, RoomWest, 113.2f, RoomWest, 130f, 5f);
            L4Decor.Wall(g, RoomEast, 113.2f, RoomEast, 130f, 5f);
            L4Decor.WallWithGap(g, 130f, -12f, 4f, -6.5f, -1.5f, 5f);
            var block = PuzzleKit.HeavyBlock(ctx.Props, new Vector3(-4f, 0f, 118.5f));
            ctx.Mark("S5.Block", block.gameObject);
            var plate = PuzzleKit.PressurePlate(p, new Vector3(-4f, 0f, plateZ), null, Tuning.PlateHeavy, true, "STAGE 1 PASSED: STRENGTH");
            plate.name = "Plate_Stage1_150";
            ctx.Mark("S5.Plate", plate.gameObject);
            ctx.MarkPoint("S5.PlateCenter", new Vector3(-4f, 0f, plateZ + 0.12f));
            var doorA = PuzzleKit.Door(p, new Vector3(-4f, 0f, 130f), Quaternion.identity, DoorMode.SlideUp, 4f, 4.2f, plate);
            ctx.Mark("S5.DoorA", doorA.gameObject);
            PuzzleKit.Cable(p, new Vector3(-4f, 0f, plateZ), new Vector3(-6.9f, 0f, 129.4f), plate);
            PuzzleKit.IndicatorLight(p, new Vector3(-8.6f, 3.3f, 129.68f), Quaternion.Euler(0f, 180f, 0f), true, plate);
            EnvKit.Sign(s, new Vector3(-8.6f, 4.3f, 129.7f), Quaternion.identity, "STAGE 1: STRENGTH", new Vector2(3.4f, 0.55f), SignStyle.Info);
            EnvKit.Sign(s, new Vector3(0.6f, 3.4f, 129.7f), Quaternion.identity, "PUT THE HEAVY THING\nON THE HEAVY PLATE.\nTHAT'S THE QUESTION.", new Vector2(3.6f, 1.3f), SignStyle.Warning);
            EnvKit.Sign(s, new Vector3(-4f, 3.2f, 113.15f), Quaternion.identity, "FINAL EXAM - 3 STAGES - NO CALCULATORS", new Vector2(6.0f, 0.6f), SignStyle.Exit);
            for (int side = -1; side <= 1; side += 2)
                Kit.BoxVisual(s, "BannerPost", new Vector3(-4f + side * 2.82f, 1.46f, 113.22f), new Vector3(0.12f, 2.92f, 0.12f), MatKey.MetalDark, 0.01f);
            for (int side = -1; side <= 1; side += 2)
                Kit.BoxVisual(s, "PushGuide", new Vector3(-4f + side * 1.05f, 0.012f, 121.3f), new Vector3(0.1f, 0.02f, 6.6f), MatKey.TrimGlow, 0f, null, false);
            L4Decor.FloorText(ctx, s, new Vector3(-4f, 0f, 116.3f), 0f, "PUSH", 0.7f, UIColors.Strength);
            EnvKit.Decor.CrateStack(s, new Vector3(-10.2f, 0f, 115.2f), Quaternion.Euler(0f, 20f, 0f), 3, 4);
            EnvKit.Decor.Machine(s, new Vector3(2.2f, 0f, 116.5f), Quaternion.Euler(0f, -90f, 0f), new Vector3(1.6f, 2.0f, 1.4f));
            ctx.Hint(new Vector3(-4f, 2f, 121f), new Vector3(15f, 5f, 15f),
                "Only the Brute (STR 7+) can push the 160 kg block. Push it straight forward onto the 150 kg plate.",
                "Heavy thing. Heavy plate. Connect the dots.");

            // ---------------- stage 2: SPEED (z 130..158)
            L4Decor.Wall(g, RoomWest, 130f, RoomWest, 158f, 5f);
            L4Decor.Wall(g, RoomEast, 130f, RoomEast, 158f, 5f);
            L4Decor.WallWithGap(g, 158f, -12f, 4f, -6.5f, -1.5f, 5f);
            var route = new[] { new Vector3(-6.9f, 0f, 132.8f), new Vector3(-4.6f, 0f, 136f), new Vector3(-4f, 0f, 157.5f) };
            var timed = PuzzleKit.TimedButton(p, new Vector3(-8.0f, 0f, 132.8f), Quaternion.Euler(0f, 90f, 0f), 26f, route);
            ctx.Mark("S5.Timed", timed.gameObject);
            var lever = PuzzleKit.Lever(p, new Vector3(-1.2f, 0f, 160.6f), Quaternion.Euler(0f, 180f, 0f), 0, true, false, "STAGE 2 PASSED: SPEED");
            ctx.Mark("S5.Lever", lever.gameObject);
            var doorB = PuzzleKit.Door(p, new Vector3(-4f, 0f, 158f), Quaternion.identity, DoorMode.SlideUp, 4f, 4.2f, timed, lever);
            doorB.Logic = SignalLogic.Any;
            doorB.SpeedOff = 6f;
            ctx.Mark("S5.DoorB", doorB.gameObject);
            PuzzleKit.Cable(p, new Vector3(-1.2f, 0f, 160.6f), new Vector3(-1.7f, 0f, 158.6f), lever);
            PuzzleKit.IndicatorLight(p, new Vector3(0.4f, 0f, 161.4f), Quaternion.Euler(0f, 180f, 0f), false, lever);
            for (int lane = -1; lane <= 1; lane++)
                Kit.BoxVisual(s, "TrackLine", new Vector3(-4f + lane * 2.1f, 0.012f, 146f), new Vector3(0.12f, 0.02f, 22f), MatKey.SignWhite, 0f, null, false);
            L4Decor.FloorText(ctx, s, new Vector3(-4f, 0f, 135.4f), 0f, "START", 0.8f, UIColors.Speed);
            L4Decor.FloorText(ctx, s, new Vector3(-4f, 0f, 156.6f), 0f, "FINISH", 0.8f, UIColors.Speed);
            EnvKit.Sign(s, new Vector3(-8.6f, 3.9f, 157.68f), Quaternion.identity, "STAGE 2: SPEED\nTHE DOOR WAITS FOR NO INTERN", new Vector2(3.8f, 1.0f), SignStyle.Info);
            EnvKit.Sign(s, new Vector3(0.8f, 3.9f, 157.68f), Quaternion.identity, "TOO SLOW? TRY BEING FASTER.", new Vector2(3.8f, 0.6f), SignStyle.Warning);
            EnvKit.Sign(s, new Vector3(RoomWest + 0.29f, 2.6f, 134.2f), Quaternion.Euler(0f, -90f, 0f), "PRESS. RUN.\nDON'T THINK.", new Vector2(2.2f, 0.9f), SignStyle.Arrow);
            EnvKit.Poster(s, new Vector3(RoomEast - 0.27f, 2.4f, 140f), Quaternion.Euler(0f, 90f, 0f), "SPEED", "IS JUST IMPATIENCE WITH CARDIO");
            EnvKit.Poster(s, new Vector3(RoomWest + 0.27f, 2.4f, 147f), Quaternion.Euler(0f, -90f, 0f), "THE FINISH LINE", "IS A SOCIAL CONSTRUCT. RUN ANYWAY.");
            ctx.MarkPoint("S5.RunPrep", new Vector3(-6.3f, 0f, 131.6f));
            ctx.MarkPoint("S5.BeyondB", new Vector3(-4f, 0f, 160.2f));
            ctx.MarkPoint("S5.Stage2Entry", new Vector3(-4f, 0f, 132.2f));
            ctx.Hint(new Vector3(-4f, 2f, 143f), new Vector3(15f, 5f, 25f),
                "The timed door only stays open long enough for TURBO LEGS: press the orange button with the Sprinter and sprint (LSHIFT) straight to the door. Pull the lever behind it to let the others in.",
                "Run. No, faster than that.");

            // ---------------- stage 3: INTELLECT (z 158..172)
            L4Decor.Wall(g, RoomWest, 158f, RoomWest, 172f, 5f);
            L4Decor.Wall(g, RoomEast, 158f, RoomEast, 172f, 5f);
            L4Decor.WallWithGap(g, 172f, -12f, 4f, -6.6f, -1.4f, 7f);
            var term = PuzzleKit.Terminal(p, new Vector3(-9.6f, 0f, 165.2f), Quaternion.Euler(0f, 90f, 0f), Tuning.TerminalIQ, true, 6f, "STAGE 3 PASSED: INTELLECT");
            ctx.Mark("S5.Terminal", term.gameObject);
            var final = PuzzleKit.Door(p, new Vector3(-4f, 0f, 172f), Quaternion.identity, DoorMode.SlideUp, 4.2f, 4.4f, plate, lever, term);
            ctx.Mark("S5.FinalDoor", final.gameObject);
            PuzzleKit.Cable(p, new Vector3(-9.6f, 0f, 165.2f), new Vector3(-6.9f, 0f, 171.4f), term);
            var lamps = new SignalSource[] { plate, lever, term };
            string[] names = { "STR", "SPD", "IQ" };
            Color[] cols = { UIColors.Strength, UIColors.Speed, UIColors.Intellect };
            for (int i = 0; i < 3; i++)
            {
                float x = -6.2f + i * 2.2f;
                PuzzleKit.IndicatorLight(p, new Vector3(x, 5.7f, 171.68f), Quaternion.Euler(0f, 180f, 0f), true, lamps[i]);
                ctx.Label(s, new Vector3(x, 5.15f, 171.66f), Quaternion.identity, names[i], 0.42f, cols[i], 2f);
            }
            EnvKit.Sign(s, new Vector3(-4f, 6.55f, 171.68f), Quaternion.identity, "FINAL EXAM", new Vector2(4.6f, 0.62f), SignStyle.Exit);
            // status board between the door's gantry rails: the lamps / labels / sign above were floating in the open gap
            Kit.BoxVisual(s, "StatusBoard", new Vector3(-4f, 5.95f, 171.76f), new Vector3(4.2f, 2.1f, 0.08f), MatKey.MetalDark, 0.02f);
            L4Decor.FaceBrainJarPlate(EnvKit.Decor.BrainJar(s, new Vector3(1.4f, 0f, 165.5f), 1.7f), Vector3.left);   // "IQ 9000" faces the path
            EnvKit.Sign(s, new Vector3(1.4f, 0.55f, 163.95f), Quaternion.identity, "FORMER INTERN OF THE MONTH", new Vector2(2.6f, 0.4f), SignStyle.Info);
            EnvKit.Sign(s, new Vector3(RoomWest + 0.29f, 3.3f, 165.2f), Quaternion.Euler(0f, -90f, 0f), "STAGE 3: INTELLECT\nPLEASE HACK RESPONSIBLY", new Vector2(3.6f, 1.0f), SignStyle.Info);
            PerspectiveKit.Clue(s, new Vector3(RoomEast - 0.27f, 3.2f, 161.5f), Quaternion.Euler(0f, 90f, 0f), "THE ANSWER IS 42.\nTHE QUESTION IS CLASSIFIED.", 0.28f);
            EnvKit.LightPanel(ctx.Lights, new Vector3(-4f, 6.1f, 171.7f), Quaternion.identity, new Vector2(5.4f, 0.3f), UIColors.Intellect, true, 2.6f, 16f);
            EnvKit.Decor.GridPillar(s, new Vector3(-11.1f, 0f, 171.3f), 6.5f);
            EnvKit.Decor.GridPillar(s, new Vector3(3.1f, 0f, 171.3f), 6.5f);
            ctx.MarkPoint("S5.Stage3Entry", new Vector3(-4f, 0f, 161.5f));
            ctx.MarkPoint("S5.BeforeFinal", new Vector3(-4f, 0f, 169.5f));
            ctx.Hint(new Vector3(-4f, 2f, 165f), new Vector3(15f, 5f, 12f),
                "The Brainiac hacks the terminal (IQ 7). When all three stage lamps are lit, the final door opens.",
                "Something in here wants to be hacked by someone who can spell 'hack'.");

            // ---------------- exit
            ctx.Exit(new Vector3(-4f, 0f, 176f), 0f);
            ctx.MarkPoint("S5.ExitInside", new Vector3(-4f, 0.1f, 178.1f));
            EnvKit.Sign(s, new Vector3(-4f, 7.45f, 175.75f), Quaternion.identity, "GRADUATION... AGAIN", new Vector2(6.4f, 0.9f), SignStyle.Info);
            EnvKit.Decor.HoloPanel(s, new Vector3(1.6f, 0f, 175.6f), Quaternion.Euler(0f, 45f, 0f), new Vector2(2.4f, 1.3f), "SHIFT COMPLETE?\nRESULTS: PENDING.\nSHIFT CHANGE IN: NEVER");
        }
    }
}
