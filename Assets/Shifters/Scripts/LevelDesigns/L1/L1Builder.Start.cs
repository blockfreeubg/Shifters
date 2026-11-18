// =====================================================================================
//  SHIFTERS - L1Builder.Start.cs   (LEVEL 1: S0 Arrival Bay, S1 Switching Room)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L1Builder
    {
        // S0 metrics (public for the solution / tests)
        public const float StepZ = 10f, StepTop = 0.8f;
        public const float Gap0Start = 16f, Gap0End = 17.5f;
        public const float HurdleZ0 = 21.2f, HurdleZ1 = 23.4f, HurdleTop = 1.8f;
        public const float RampTopZ = 28f, RampBottomZ = 34f;

        // ================================================================== S0 ARRIVAL BAY
        void BuildArrivalBay()
        {
            var ctx = _ctx;
            var s = ctx.Section("S0_ArrivalBay", Vector3.zero);
            var g = ctx.Geometry;

            // floors: bay (y 0) -> 0.8 m step -> 1.5 m foam gap -> hurdle -> ramp down
            Floor(g, -9f, 9f, -13f, StepZ);
            Floor(g, -9f, 9f, StepZ, Gap0Start, StepTop, MatKey.FloorAlt, true, 1.8f);
            Floor(g, -9f, 9f, Gap0End, RampTopZ, StepTop, MatKey.FloorAlt, true, 1.8f);
            FoamPit(s, -9f, 9f, Gap0Start, Gap0End, V(0f, StepTop, 13.5f), StepTop);
            Box(g, "Hurdle", V(-9f, StepTop, HurdleZ0), V(9f, HurdleTop, HurdleZ1), MatKey.WallAlt);
            EnvKit.HazardStripe(s, V(0f, HurdleTop + 0.01f, (HurdleZ0 + HurdleZ1) * 0.5f), V(18f, 0.02f, 0.3f));
            EnvKit.Ramp(g, V(0f, 0f, RampBottomZ), V(0f, StepTop, RampTopZ), 18f, MatKey.FloorAlt);
            // face trims on the step
            Kit.BoxVisual(s, "StepNose", V(0f, StepTop - 0.06f, StepZ - 0.01f), V(18f, 0.12f, 0.04f), MatKey.SafetyOrange, 0.01f, null, false);

            // spawn podium (0.25 m) with a friendly arch
            ctx.Platform(g, V(0f, 0.25f, -5f), V(8f, 0.25f, 4.4f), MatKey.FloorAlt, true, "SpawnPodium");
            Kit.BoxVisual(s, "PodiumRing", V(0f, 0.262f, -5f), V(6.6f, 0.02f, 0.12f), MatKey.TrimGlow, 0.003f, null, false);
            ctx.SetSpawn(V(0f, 0.27f, -5f), 0f);
            EnvKit.Pillar(s, V(-4.6f, 0f, -1.4f), 4.2f, 0.6f);
            EnvKit.Pillar(s, V(4.6f, 0f, -1.4f), 4.2f, 0.6f);
            Kit.BoxVisual(s, "ArchBeam", V(0f, 4.35f, -1.4f), V(9.8f, 0.4f, 0.5f), MatKey.SafetyOrange, 0.05f);
            Sign(s, V(0f, 3.6f, -1.7f), 180f, "NEW HIRES: PLEASE STAND STILL WHILE WE PRETEND TO ONBOARD YOU", 8.2f, 0.8f);

            // big welcome gantry
            Gantry(s, 6.5f, 7.2f, 5.6f, "SHIFTERS INC. - EMPLOYEE ORIENTATION", 12.5f, 1.9f, SignStyle.Exit);
            Sign(s, V(0f, 4.15f, 6.2f), 180f, "YOUR FIRST SHIFT HAS BEGUN", 7f, 0.6f);

            // warm-up markings
            DevicePromptText.Attach(FloorText(s, V(0f, 0f, 7.6f), 0f, InputPrompts.Format("STEP UP  ({JUMP})", false, false), 0.55f, UIColors.Accent), "STEP UP  ({JUMP})");
            FloorText(s, V(0f, StepTop, 14.6f), 0f, "MIND THE GAP. OR DON'T: IT'S FOAM.", 0.42f, UIColors.AccentYellow);
            FloorText(s, V(0f, StepTop, 20.2f), 0f, "HOP THE WALL", 0.55f, UIColors.Accent);
            FloorText(s, V(0f, StepTop, 26f), 0f, "GENTLE RAMP. YOU'RE WELCOME.", 0.42f, UIColors.Info);
            Sign(s, V(-6f, StepTop + 1.6f, 16.75f), 180f, "FOAM PIT\nNO INTERNS WERE HARMED", 2.8f, 1.1f, SignStyle.Warning);

            // open-air bay: railings instead of walls
            EnvKit.Railing(s, V(-8.8f, 0f, -12.8f), V(-8.8f, 0f, StepZ));
            EnvKit.Railing(s, V(8.8f, 0f, -12.8f), V(8.8f, 0f, StepZ));
            EnvKit.Railing(s, V(-8.8f, 0f, -12.8f), V(8.8f, 0f, -12.8f));
            EnvKit.Railing(s, V(-8.8f, StepTop, StepZ), V(-8.8f, StepTop, Gap0Start));
            EnvKit.Railing(s, V(8.8f, StepTop, StepZ), V(8.8f, StepTop, Gap0Start));
            EnvKit.Railing(s, V(-8.8f, StepTop, Gap0End), V(-8.8f, StepTop, RampTopZ));
            EnvKit.Railing(s, V(8.8f, StepTop, Gap0End), V(8.8f, StepTop, RampTopZ));

            // tutorials (fire in this order as the active intern walks forward)
            ctx.Tutorial(V(0f, 1.5f, -5f), V(18f, 3.5f, 10f), "welcome", "WELCOME TO SHIFTERS",
                "Welcome to SHIFTERS. You have three characters. Try not to lose them.", new[] { "Backspace" }, true);
            ctx.Tutorial(V(0f, 1.5f, 3.5f), V(18f, 3.5f, 6f), "movement", "MOVEMENT",
                "{Move} to walk, {LOOK} to look around, {JUMP} to jump, {kb:hold LEFT SHIFT}{pad:click {Sprint}} to sprint.", new[] { "WASD", "Mouse", "Space", "LShift" });
            ctx.Hint(V(0f, 2f, 16f), V(18f, 5f, 20f),
                "Jump up the step with {JUMP}, hop the foam gap, then jump onto the low wall and drop down the far side.",
                "Up, over, over again. Legs are involved.");

            // decor: friendly lobby
            var d = ctx.Decor;
            Board(d, V(-6.9f, 0f, -4.5f), 60f, "ORIENTATION AGENDA\n1. WALK\n2. JUMP\n3. ???\n4. GRADUATE");
            BenchFacing(d, V(6.8f, 0f, -2.5f), -90f, 2.2f);
            EnvKit.Decor.Plant(d, V(7.6f, 0f, -11.6f), 0);
            EnvKit.Decor.Plant(d, V(-7.6f, 0f, -11.6f), 1);
            EnvKit.Decor.Plant(d, V(7.8f, 0f, 1.2f), 2);
            EnvKit.Decor.TrainingDummy(d, V(-7.4f, 0f, 4.5f), Yaw(120f));
            Poster(d, V(-7.8f, 1.9f, -9f), 90f, "EMPLOYEE OF THE MONTH", "THE FLOOR. ALWAYS THERE FOR YOU.");
            Poster(d, V(7.8f, 1.9f, -9f), -90f, "TEAMWORK", "BECAUSE YOU CAN'T AFFORD A LAWYER");
            Poster(d, V(-7.8f, 1.9f, 1.5f), 90f, "DAYS WITHOUT A SHIFT ACCIDENT: 0", "PERSONAL BEST: 0");
            for (int i = 0; i < 6; i++) EnvKit.Decor.Cone(d, V(-8.2f + i * 0.45f, StepTop, Gap0Start - 0.5f + (i % 2) * 0.2f));
            for (int i = 0; i < 4; i++) EnvKit.Decor.Cone(d, V(6.6f + i * 0.5f, StepTop, Gap0End + 0.5f));
            WelcomeDesk(d, V(5.8f, 0f, 3.2f));
        }

        /// <summary>Reception desk with a bell and a "PLEASE WAIT" sign (decor, solid).</summary>
        void WelcomeDesk(Transform d, Vector3 pos)
        {
            Kit.SolidBox(d, "Desk", pos + V(0f, 0.55f, 0f), V(2.6f, 1.1f, 0.9f), MatKey.SafetyOrange, 0.06f);
            Kit.BoxVisual(d, "DeskTop", pos + V(0f, 1.13f, 0f), V(2.8f, 0.06f, 1.05f), MatKey.SignWhite, 0.02f);
            Kit.BoxVisual(d, "Bell", pos + V(0.7f, 1.2f, 0f), V(0.18f, 0.1f, 0.18f), MatKey.HazardYellow, 0.04f);
            Sign(d, pos + V(0f, 1.6f, -0.1f), 180f, "RECEPTION\n(UNSTAFFED SINCE 2019)", 1.8f, 0.6f);
        }

        // ================================================================== S1 SWITCHING ROOM
        public const float D1Z = 42f;

        void BuildSwitchingRoom()
        {
            var ctx = _ctx;
            var s = ctx.Section("S1_SwitchingRoom", Vector3.zero);
            var g = ctx.Geometry;
            Floor(g, -9f, 9f, RampTopZ, 52f);
            WallZ(g, -9f, RampTopZ, 52f, 4.7f);
            WallZ(g, 9f, RampTopZ, 52f, 4.7f);

            // plate P1 (40 kg, not latched) in an alcove left of door D1
            var p1 = PuzzleKit.PressurePlate(ctx.Puzzles, V(-5f, 0f, 40.3f), null, Tuning.PlateLight, false, "");
            ctx.Mark("S1.P1", p1.gameObject);
            WallZ(g, -7.1f, 38.6f, D1Z, 3.4f);
            WallZ(g, -2.9f, 38.6f, D1Z, 3.4f);
            FloorText(s, V(-5f, 0f, 38.4f), 0f, "STAND HERE\n(YES, YOU)", 0.36f, UIColors.AccentYellow, 3.4f);
            Sign(s, V(-5f, 3.0f, D1Z - 0.25f), 180f, "DOOR HOLDER\nPOSITION", 2.6f, 0.9f, SignStyle.Warning);

            // lever L1 beyond the door: latches D1 open for whoever is still holding the plate
            var l1 = PuzzleKit.Lever(ctx.Puzzles, V(7.6f, 0f, 47f), Yaw(-90f), 0, true, false, "MAINTENANCE OVERRIDE ENGAGED");
            ctx.Mark("S1.L1", l1.gameObject);
            Sign(s, V(8.74f, 2.4f, 47f), -90f, "MAINTENANCE OVERRIDE\n(DO NOT PULL)\n(OK, PULL)", 2.4f, 1.3f, SignStyle.Warning);

            // door D1 = P1 OR L1
            var d1 = PuzzleKit.Door(ctx.Puzzles, V(2f, 0f, D1Z), null, DoorMode.SlideUp, 3f, 4.2f, p1, l1);
            d1.Logic = SignalLogic.Any;
            ctx.Mark("S1.D1", d1.gameObject);
            WallX(g, -9f, 0f, D1Z, 4.7f);
            WallX(g, 4f, 9f, D1Z, 4.7f);
            PuzzleKit.Cable(s, V(-5f, 0f, 40.3f), V(0.4f, 0f, 41.3f), p1);
            PuzzleKit.Cable(s, V(7.6f, 0f, 47f), V(3.6f, 0f, 42.7f), l1);
            ctx.MarkPoint("S1.DoorNear", V(2f, 0f, 39.6f));
            ctx.MarkPoint("S1.DoorFar", V(2f, 0f, 44.6f));

            // tutorials: switching at the plate, team orders after the lever
            ctx.Tutorial(V(0f, 1.5f, 37f), V(18f, 3.5f, 9f), "switching", "SWITCHING",
                "Press {Switcher|RIGHT SHIFT} to open the switcher{kb:, or 1/2/3 to switch directly}. Teammates stay exactly where you leave them.",
                new[] { "RShift", "1", "2", "3" }, true);
            var orders = Kit.Make("Tutorial_team_orders", ctx.Gameplay, V(7.6f, 1f, 47f)).AddComponent<L1SignalTutorial>();
            orders.Message = new TutorialMessage { Id = "team_orders", Title = "TEAM ORDERS", Body = "Press {AIMode}: inactive teammates FOLLOW you or HOLD position.", Keys = new[] { "G" }, Duration = 9f };
            orders.Logic = SignalLogic.Latch;
            PuzzleKit.Wire(orders, l1);
            ctx.Mark("tutorial.team_orders", orders.gameObject);
            ctx.Hint(V(0f, 2f, 40f), V(18f, 5f, 14f),
                "Leave one intern standing on the plate in the alcove, switch to another and walk through. Pull the MAINTENANCE OVERRIDE lever to keep the door open.",
                "The door only stays open while someone is being useful.");

            ctx.Checkpoint(V(0f, 0f, 49f), 0f);

            // decor
            var d = ctx.Decor;
            DevicePromptText.AttachInChildren(Board(d, V(-7.2f, 0f, 31.5f), 90f, InputPrompts.Format(HowToShift, false, false)), HowToShift);
            Poster(d, V(8.75f, 2f, 33f), -90f, "FAILURE IS A LEARNING OPPORTUNITY", "(FOR US)");
            Poster(d, V(-8.75f, 2f, 47f), 90f, "PLEASE DO NOT SHIFT THE BLAME", "(OR THE INTERNS)");
            EnvKit.Decor.Plant(d, V(8f, 0f, 29.2f), 1);
            EnvKit.Decor.Cone(d, V(-2.3f, 0f, 41.2f));
            Panel(V(-8.78f, 3.6f, 38f), 90f, 2.4f, 0.5f, UIColors.AccentYellow);
            Panel(V(8.78f, 3.6f, 38f), -90f, 2.4f, 0.5f, UIColors.AccentYellow);
        }
    }
}
