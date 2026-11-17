// =====================================================================================
//  SHIFTERS - L1Builder.Skills.cs   (LEVEL 1: S3 Bridge Training, S4 Stack & Throw)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L1Builder
    {
        public const float BridgeGapStart = 90f, BridgeGapEnd = 95.2f;           // 5.2 m (living bridge 6.5 m)
        public const float PlateauZ0 = 113f, PlateauZ1 = 121f, PlateauTop = 2.5f; // stack ledge 2.5 m
        public const float StairsBottomZ = 127.5f;
        public const float BalconyX0 = 0f, BalconyX1 = 7f, BalconyZ0 = 140.5f, BalconyZ1 = 146f, BalconyTop = 3.6f;
        public const float D4Z = 146.2f, D4X = -3.5f;
        // THROW FROM HERE pad (where the thrower's feet go). From anywhere on it, a player throw (Tuning.ThrowBaseAngle
        // + 0..25 deg look-up = 38..63 deg) lands on the balcony for every possible Brute (STR 7..10) and flier (55..75 kg):
        // the back edge keeps the weakest steep lob past the balcony lip, the 6.2 m wall behind the balcony stops the
        // strongest flat throw (checked by arithmetic and in Unity, see L1QAChecks.HumanThrows).
        public const float ThrowPadX = 4.6f, ThrowPadZ0 = 135.5f, ThrowPadZ1 = 137f, ThrowPadW = 2.6f;
        public const float ThrowPadZ = (ThrowPadZ0 + ThrowPadZ1) * 0.5f;

        // ================================================================== S3 BRIDGE TRAINING
        void BuildBridgeTraining()
        {
            var ctx = _ctx;
            var s = ctx.Section("S3_BridgeTraining", Vector3.zero);
            var g = ctx.Geometry;
            Floor(g, -6f, 6f, HubZ1, BridgeGapStart, 0f, MatKey.Floor, true, 1.6f);
            Floor(g, -6f, 6f, BridgeGapEnd, 106f, 0f, MatKey.Floor, true, 1.6f);
            FoamPit(s, -6f, 6f, BridgeGapStart, BridgeGapEnd, V(0f, 0f, 86.5f));
            WallZ(g, -6f, HubZ1, 106f, 4f);
            WallZ(g, 6f, HubZ1, 106f, 4f);
            // fill the hub wall shoulders between the 18 m hub and the 12 m corridor
            ctx.MarkPoint("S3.BridgeSpot", V(0f, 0f, BridgeGapStart - 0.4f));
            ctx.MarkPoint("S3.Approach", V(0f, 0f, BridgeGapStart - 2.2f));
            ctx.MarkPoint("S3.Far", V(0f, 0f, BridgeGapEnd + 3.6f));
            ctx.MarkPoint("S3.FarEdge", V(0f, 0f, BridgeGapEnd + 0.6f));

            Gantry(s, BridgeGapEnd + 1.2f, 5.4f, 5.3f, "BRIDGE TRAINING: PLEASE DO NOT STEP ON THE INTERN", 10.2f, 1.2f, SignStyle.Warning);
            Sign(s, V(-5.75f, 1.9f, 87f), 90f, "GAP: 5.2 M\nINTERN: 6.5 M\nMATH: CHECKS OUT", 2.2f, 1.2f);
            FloorText(s, V(0f, 0f, 88.4f), 0f, "LIE DOWN HERE  [B]", 0.45f, UIColors.AccentYellow, 6f);
            Kit.BoxVisual(s, "BridgeMark", V(0f, 0.01f, BridgeGapStart - 0.45f), V(1.4f, 0.02f, 0.9f), MatKey.HazardStripes, 0.003f, null, false);
            ctx.Tutorial(V(0f, 1.5f, 85.5f), V(12f, 3.5f, 6f), "bridge", "LIVING BRIDGE",
                "Need a bridge? Try using your teammate. Stand at the edge and press B.", new[] { "B" }, true);
            var follow = Kit.Make("Tutorial_bridge_cross", ctx.Gameplay, V(0f, 1f, 92f)).AddComponent<L1BridgeTutorial>();
            follow.ZoneSize = V(12f, 5f, 14f);
            follow.Message = new TutorialMessage
            {
                Id = "bridge_cross", Title = "CROSSING",
                Body = "Switch and walk across. As the bridge, press B again to recover - you'll snap to the far side.",
                Keys = new[] { "RShift", "B" }, Important = true, Duration = 0f
            };
            ctx.Mark("tutorial.bridge_cross", follow.gameObject);
            ctx.Hint(V(0f, 2f, 92f), V(12f, 5f, 16f),
                "Walk one intern to the striped mark at the edge and press B to become a 6.5 m plank. Switch to the others and walk across one at a time. Then switch back and press B to snap to the far side.",
                "The gap is exactly one intern long. Ish.");
            ctx.Checkpoint(V(0f, 0f, 102f), 0f);

            var d = ctx.Decor;
            EnvKit.Decor.TrainingDummy(d, V(-4.8f, 0f, 81f), Yaw(30f));
            EnvKit.Decor.TrainingDummy(d, V(4.8f, 0f, 100f), Yaw(-60f));
            Poster(d, V(5.75f, 2.1f, 83f), -90f, "INTERN-GRADE PLANKING", "LOAD RATING: ONE (1) COWORKER");
            Poster(d, V(-5.75f, 2.1f, 99f), 90f, "SAFETY FIRST", "(SAFETY RESULTS NOT GUARANTEED)");
            for (int i = 0; i < 3; i++) EnvKit.Decor.Cone(d, V(-5.2f + i * 0.5f, 0f, BridgeGapStart - 0.5f));
            for (int i = 0; i < 3; i++) EnvKit.Decor.Cone(d, V(4.2f + i * 0.5f, 0f, BridgeGapStart - 0.5f));
            Panel(V(-5.78f, 3.3f, 92.6f), 90f, 3f, 0.45f, UIColors.AccentYellow);
            Panel(V(5.78f, 3.3f, 92.6f), -90f, 3f, 0.45f, UIColors.AccentYellow);
        }

        // ================================================================== S4 STACK & THROW
        void BuildStackAndThrow()
        {
            var ctx = _ctx;
            var s = ctx.Section("S4_StackAndThrow", Vector3.zero);
            var g = ctx.Geometry;
            const float H = 6.2f;
            Floor(g, -7f, 7f, 106f, D4Z + 0.2f);
            WallZ(g, -7f, 106f, D4Z, H);
            WallZ(g, 7f, 106f, D4Z, H);
            // shoulders where the 12 m bridge corridor meets the 14 m stack hall
            WallX(g, -7f, -6f, 106f, 4f, 0f, false);
            WallX(g, 6f, 7f, 106f, 4f, 0f, false);

            // ---- (a) 2.5 m plateau: stack to climb, button lowers the ramp for everyone
            EnvKit.Floor(g, V(0f, PlateauTop, (PlateauZ0 + PlateauZ1) * 0.5f), V(14f, PlateauTop, PlateauZ1 - PlateauZ0), MatKey.FloorAlt, true);
            EnvKit.Stairs(g, V(0f, 0f, StairsBottomZ), Vector3.back, 6f, 0.25f, 0.65f, 10, MatKey.FloorAlt);
            EnvKit.Railing(s, V(-6.6f, PlateauTop, PlateauZ1 - 0.15f), V(-3.1f, PlateauTop, PlateauZ1 - 0.15f));
            EnvKit.Railing(s, V(3.1f, PlateauTop, PlateauZ1 - 0.15f), V(6.6f, PlateauTop, PlateauZ1 - 0.15f));
            var button = PuzzleKit.PushButton(ctx.Puzzles, V(3.5f, PlateauTop, PlateauZ0 + 3f), Yaw(180f), 2f, "RAMP DEPLOYED. ACCESSIBILITY ACHIEVED.");
            ctx.Mark("S4.Button", button.gameObject);
            var ramp = L1FoldingRamp.Build(ctx.Puzzles, V(-4f, PlateauTop, PlateauZ0), 180f, 3.4f, 6.5f, PlateauTop, false, "RAMP\n(FOLDED FOR YOUR INCONVENIENCE)\n\nBUTTON IS UP THERE", button);
            ctx.Mark("S4.Ramp", ramp.gameObject);
            PuzzleKit.Cable(s, V(3.5f, PlateauTop, PlateauZ0 + 3.6f), V(-2f, PlateauTop, PlateauZ0 + 0.5f), button);
            ctx.MarkPoint("S4.StackBottom", V(3.5f, 0f, PlateauZ0 - 0.62f));
            ctx.MarkPoint("S4.StackTop", V(3.5f, PlateauTop, PlateauZ0 + 1.3f));
            ctx.MarkPoint("S4.RampFoot", V(-4f, 0f, PlateauZ0 - 7.4f));
            ctx.MarkPoint("S4.RampTop", V(-4f, PlateauTop, PlateauZ0 + 1.2f));
            ctx.MarkPoint("S4.PlateauBack", V(0f, PlateauTop, PlateauZ1 - 0.8f));
            ctx.MarkPoint("S4.StairsFoot", V(0f, 0f, StairsBottomZ + 1.6f));
            Kit.BoxVisual(s, "StackPad", V(3.5f, 0.012f, PlateauZ0 - 0.75f), V(1.6f, 0.02f, 1.3f), MatKey.HazardStripes, 0.003f, null, false);
            FloorText(s, V(3.5f, 0f, PlateauZ0 - 2.1f), 0f, "STACK HERE", 0.55f, UIColors.AccentYellow, 4f);
            Sign(s, V(3.5f, 1.6f, PlateauZ0 - 0.04f), 180f, "2.5 M\nJUMP ON A COLLEAGUE", 2.2f, 1f, SignStyle.Warning);
            IQNote(s, V(0.5f, 1.8f, PlateauZ0 - 0.03f), 0f, "THE BUTTON IS UP THERE. THINK VERTICALLY.", 0.28f);
            ctx.Tutorial(V(0f, 1.5f, 109.5f), V(14f, 3.5f, 7f), "stacking", "STACKING",
                "Jump onto a teammate's head to reach higher ledges. Up there, press the button.", new[] { "Space", "E" }, true);
            ctx.Hint(V(0f, 2f, 110f), V(14f, 5f, 9f),
                "Park one intern on the STACK HERE stripes, switch to another, jump onto their head and then onto the ledge. Press the button to lower the ramp for everyone.",
                "Two interns are taller than one. Usually.");

            // ---- (b) 3.6 m throw balcony with lever L4 (opens D4 + unfolds the stairs)
            EnvKit.Floor(g, V((BalconyX0 + BalconyX1) * 0.5f, BalconyTop, (BalconyZ0 + BalconyZ1) * 0.5f), V(BalconyX1 - BalconyX0, BalconyTop, BalconyZ1 - BalconyZ0), MatKey.FloorAlt, true);
            EnvKit.Railing(s, V(BalconyX0 + 0.15f, BalconyTop, BalconyZ0 + 0.1f), V(BalconyX0 + 0.15f, BalconyTop, BalconyZ1 - 0.2f));
            var l4 = PuzzleKit.Lever(ctx.Puzzles, V(5.2f, BalconyTop, BalconyZ1 - 0.75f), Yaw(180f), 0, true, false, "AIRMAIL DELIVERED. DOOR OPEN.");
            ctx.Mark("S4.L4", l4.gameObject);
            var stairs = L1FoldingRamp.Build(ctx.Puzzles, V(1.4f, BalconyTop, BalconyZ0), 180f, 2.4f, 7.4f, BalconyTop, true, "STAIRS\n(FOLDED)\n\nLEVER IS UP THERE.\nGOOD LUCK.", l4);
            ctx.Mark("S4.Stairs", stairs.gameObject);
            var d4 = PuzzleKit.Door(ctx.Puzzles, V(D4X, 0f, D4Z), null, DoorMode.SlideUp, 3.5f, 4.2f, l4);
            ctx.Mark("S4.D4", d4.gameObject);
            WallX(g, -7f, D4X - 2.25f, D4Z, H);
            WallX(g, D4X + 2.25f, 7f, D4Z, H);
            PuzzleKit.Cable(s, V(BalconyX0 - 0.06f, 0f, D4Z - 0.7f), V(BalconyX0 - 0.06f, BalconyTop, D4Z - 0.7f), l4);
            PuzzleKit.Cable(s, V(BalconyX0 - 0.06f, 0f, D4Z - 0.7f), V(D4X + 1.8f, 0f, D4Z - 0.7f), l4);
            ctx.MarkPoint("S4.ThrowLanding", V(4.6f, BalconyTop, 143.2f));
            ctx.MarkPoint("S4.ThrowApproach", V(ThrowPadX, 0f, ThrowPadZ0 - 1f));
            ctx.MarkPoint("S4.PickupSpot", V(4.6f, 0f, 132.6f));
            ctx.MarkPoint("S4.ThrowPad", V(ThrowPadX, 0f, ThrowPadZ));
            ctx.MarkPoint("S4.ThrowPadBack", V(ThrowPadX, 0f, ThrowPadZ0));
            ctx.MarkPoint("S4.ThrowPadFront", V(ThrowPadX, 0f, ThrowPadZ1));
            ctx.MarkPoint("S4.StairsTop", V(1.4f, BalconyTop, BalconyZ0 + 1f));
            ctx.MarkPoint("S4.StairsBottom", V(1.4f, 0f, BalconyZ0 - 8.6f));
            ctx.MarkPoint("S4.D4Near", V(D4X, 0f, D4Z - 2.4f));
            ctx.MarkPoint("S4.D4Far", V(D4X, 0f, D4Z + 3f));
            // THROW FROM HERE: striped pad with a strength-orange border, the words just in front of it (read while walking
            // up to it), chevrons pointing at the balcony. Flat paint only: nothing to trip over.
            Kit.BoxVisual(s, "ThrowPad", V(ThrowPadX, 0.012f, ThrowPadZ), V(ThrowPadW, 0.02f, ThrowPadZ1 - ThrowPadZ0), MatKey.HazardStripes, 0.003f, null, false);
            var padEdge = MaterialLibrary.Colored(UIColors.Strength, 0.3f, 0.6f);
            foreach (float ez in new[] { ThrowPadZ0 - 0.06f, ThrowPadZ1 + 0.06f })
                ProcMesh.AddRenderer(Kit.Make("ThrowPadEdge", s, V(ThrowPadX, 0.014f, ez)), ProcMesh.CachedBox(V(ThrowPadW + 0.24f, 0.022f, 0.12f), 0f), padEdge, false);
            foreach (float ex in new[] { ThrowPadX - ThrowPadW * 0.5f - 0.06f, ThrowPadX + ThrowPadW * 0.5f + 0.06f })
                ProcMesh.AddRenderer(Kit.Make("ThrowPadEdge", s, V(ex, 0.014f, ThrowPadZ)), ProcMesh.CachedBox(V(0.12f, 0.022f, ThrowPadZ1 - ThrowPadZ0), 0f), padEdge, false);
            FloorText(s, V(ThrowPadX, 0f, ThrowPadZ0 - 0.55f), 0f, "THROW FROM HERE", 0.5f, UIColors.Strength, 4f);
            FloorText(s, V(ThrowPadX, 0f, ThrowPadZ1 + 0.75f), 0f, "^   ^   ^", 0.6f, UIColors.Strength, 3f);
            Sign(s, V(4.6f, 2.3f, BalconyZ0 - 0.04f), 180f, "3.6 M\nAIRMAIL ONLY", 2.4f, 1f, SignStyle.Warning);
            Sign(s, V(4.6f, BalconyTop + 2.4f, BalconyZ1 - 0.15f), 180f, "LANDING ZONE\nPLEASE TUCK AND ROLL", 3.4f, 1.1f);
            IQNote(s, V(-6.75f, 2.4f, 138f), -90f, "STRONG INTERNS CAN THROW SMALL INTERNS. LEGALLY GREY.", 0.26f);
            ctx.Tutorial(V(2f, 1.5f, 135f), V(12f, 3.5f, 8f), "throwing", "THROWING",
                "Strong characters can pick up (F) and throw (Q) teammates. Stand on THROW FROM HERE, face the balcony, look up a little.", new[] { "F", "Q" }, true);
            ctx.Hint(V(2f, 2f, 138f), V(14f, 6f, 14f),
                "Switch to the HEAVY LIFTER, pick up a teammate with F, stand on the THROW FROM HERE stripes facing the balcony and throw with Q (look up a little). Up there, pull the lever.",
                "Some interns are meant to fly. Briefly.");

            // decor
            var d = ctx.Decor;
            Poster(d, V(-6.75f, 2.1f, 110f), 90f, "WE TRUST YOU WITH YOUR COWORKERS", "WE SHOULDN'T");
            Poster(d, V(6.75f, 2.1f, 131f), -90f, "SHIFT CHANGE IN: NEVER", "YOUR CLONE WILL COVER FOR YOU");
            Board(d, V(-6f, 0f, 130f), 80f, "THROWING 101:\n1. LIFT\n2. YEET\n3. APOLOGIZE");
            EnvKit.Decor.TrainingDummy(d, V(6.2f, PlateauTop, 119.6f), Yaw(-140f));
            EnvKit.Decor.CrateStack(d, V(-6f, 0f, 124.5f), Yaw(5f), 3, 9);
            EnvKit.Decor.Plant(d, V(6.3f, 0f, 107.2f), 2);
            EnvKit.Decor.Cone(d, V(ThrowPadX + ThrowPadW * 0.5f + 0.45f, 0f, ThrowPadZ1 + 0.35f));
            EnvKit.Decor.Cone(d, V(ThrowPadX - ThrowPadW * 0.5f - 0.45f, 0f, ThrowPadZ1 + 0.35f));
            Panel(V(-6.78f, 4.6f, 118f), 90f, 3f, 0.45f, UIColors.AccentYellow);
            Panel(V(6.78f, 5.4f, 143f), -90f, 2.5f, 0.45f, UIColors.Strength);
        }
    }
}
