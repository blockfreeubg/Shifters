// =====================================================================================
//  SHIFTERS - L1Builder.Final.cs   (LEVEL 1: S5 Weapon Range, S6 Sacrifice Lesson, S7 Graduation)
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public partial class L1Builder
    {
        public const float RangeZ = 161f, D5Z = 176f, FiringLineX = -5.9f;
        public const float D6Z = 196f;
        public const float GateZ = 246.5f, ExitZ = 250f;

        GameObject DuctWallX(Transform p, float x0, float x1, float z, float h = 3.6f) => EnvKit.Wall(p, V(x0, 0f, z), V(x1, 0f, z), h, 0.4f, MatKey.WallAlt, true);
        GameObject DuctWallZ(Transform p, float x, float z0, float z1, float h = 3.6f) => EnvKit.Wall(p, V(x, 0f, z0), V(x, 0f, z1), h, 0.4f, MatKey.WallAlt, true);

        // ================================================================== S5 WEAPON RANGE
        void BuildWeaponRange()
        {
            var ctx = _ctx;
            var s = ctx.Section("S5_WeaponRange", Vector3.zero);
            var g = ctx.Geometry;
            const float H = 4.6f;
            Floor(g, -7f, 7f, D4Z + 0.2f, D5Z);
            Floor(g, -25f, -7f, 152f, 170f, 0f, MatKey.FloorAlt);
            WallZ(g, -7f, D4Z, 152f, H); WallZ(g, -7f, 170f, D5Z, H);
            WallZ(g, 7f, D4Z, D5Z, H);
            WallZ(g, -25f, 152f, 170f, H);
            WallX(g, -25f, -7f, 152f, H); WallX(g, -25f, -7f, 170f, H);
            // The range threshold is PAINT ONLY - no railing / counter. Guns fire from the muzzle, and a gun hanging at
            // a short intern's hip (every first shot before the gun swings up) sits at 0..0.4 m: anything solid between
            // the firing line and the targets would eat those shots. Hazard band = range edge, red line = stand behind me.
            EnvKit.HazardStripe(s, V(-7f, 0.011f, RangeZ), V(0.35f, 0.02f, 17.6f));
            Kit.BoxVisual(s, "FiringLine", V(FiringLineX, 0.012f, RangeZ), V(0.14f, 0.02f, 16.8f), MatKey.WarningRed, 0.003f, null, false);

            // three latching targets (8-12 m from the firing line) -> shutter D5
            var t1 = PuzzleKit.ShootTarget(ctx.Puzzles, V(-14.5f, 0f, 155.5f), Yaw(90f), ShootTargetMode.Latch, 4f, true, "");
            var t2 = PuzzleKit.ShootTarget(ctx.Puzzles, V(-16.5f, 0f, RangeZ), Yaw(90f), ShootTargetMode.Latch, 4f, true, "");
            var t3 = PuzzleKit.ShootTarget(ctx.Puzzles, V(-14.5f, 0f, 166.5f), Yaw(90f), ShootTargetMode.Latch, 4f, true, "");
            ctx.Mark("S5.T1", t1.gameObject); ctx.Mark("S5.T2", t2.gameObject); ctx.Mark("S5.T3", t3.gameObject);
            var d5 = PuzzleKit.Door(ctx.Puzzles, V(0f, 0f, D5Z), null, DoorMode.SlideUp, 4f, 4.2f, t1, t2, t3);
            ctx.Mark("S5.D5", d5.gameObject);
            WallX(g, -9f, -2.5f, D5Z, 5f); WallX(g, 2.5f, 9f, D5Z, 5f);
            PuzzleKit.Cable(s, V(-14.5f, 0f, 156f), V(-1.2f, 0f, D5Z - 0.6f), t1);
            PuzzleKit.Cable(s, V(-16.5f, 0f, RangeZ + 0.5f), V(-0.4f, 0f, D5Z - 0.6f), t2);
            PuzzleKit.Cable(s, V(-14.5f, 0f, 167f), V(0.4f, 0f, D5Z - 0.6f), t3);
            ctx.MarkPoint("S5.ShootSpot", V(-5.2f, 0f, RangeZ));
            ctx.MarkPoint("S5.D5Near", V(0f, 0f, D5Z - 2.5f));
            ctx.MarkPoint("S5.D5Far", V(0f, 0f, D5Z + 2.5f));
            FloorText(s, V(FiringLineX + 0.65f, 0f, RangeZ), -90f, "FIRING LINE", 0.6f, UIColors.Weapon, 5f);
            FloorText(s, V(-11f, 0f, RangeZ), -90f, "TARGETS DO NOT SHOOT BACK. YET.", 0.42f, UIColors.AccentYellow, 8f);

            // bonus: teetering barrel on a shelf, shot onto a 25 kg plate behind it -> "NICE SHOT"
            Box(g, "BarrelShelf", V(-22.35f, 0f, RangeZ - 1.5f), V(-21.65f, 2.6f, RangeZ + 1.5f), MatKey.MetalLight);
            Kit.BoxVisual(s, "ShelfLip", V(-22f, 2.62f, RangeZ), V(0.8f, 0.05f, 3.1f), MatKey.SafetyOrange, 0.01f);
            var barrel = PuzzleKit.Barrel(ctx.Props, V(-22.15f, 2.65f, RangeZ));
            ctx.Mark("S5.Barrel", barrel.gameObject);
            var bonusPlate = PuzzleKit.PressurePlate(ctx.Puzzles, V(-23.8f, 0f, RangeZ), null, 25f, true, "");
            ctx.Mark("S5.BonusPlate", bonusPlate.gameObject);
            PerspectiveKit.ShootHint(ctx.Puzzles, V(-21.45f, 3.15f, RangeZ), 0.5f);
            var signGo = Sign(s, V(-24.75f, 3.9f, RangeZ), 90f, "NICE SHOT", 3.4f, 1.1f, SignStyle.Arrow);
            var lit = Kit.Make("Lit", signGo.transform, Vector3.zero);
            Kit.BoxVisual(lit.transform, "GlowTop", V(0f, 0.65f, -0.02f), V(3.6f, 0.1f, 0.06f), MatKey.Glow, 0.01f, null, false);
            Kit.BoxVisual(lit.transform, "GlowBottom", V(0f, -0.65f, -0.02f), V(3.6f, 0.1f, 0.06f), MatKey.Glow, 0.01f, null, false);
            lit.SetActive(false);
            var bonus = signGo.AddComponent<L1BonusSign>();
            bonus.Text = signGo.GetComponentInChildren<TextMeshPro>();
            bonus.LitParts = lit;
            PuzzleKit.Wire(bonus, bonusPlate);
            ctx.Mark("S5.BonusSign", signGo);
            Sign(s, V(-22f, 4.3f, RangeZ - 3.2f), 90f, "DO NOT SHOOT THE BARREL\n(PLEASE SHOOT THE BARREL)", 3f, 1f, SignStyle.Warning);

            ctx.Tutorial(V(0f, 1.5f, 155f), V(14f, 3.5f, 10f), "weapon", "WEAPON",
                "One of you has a gun - look for the weapon icon. {kb:RIGHT MOUSE}{pad:{AIM}} aims, {kb:LEFT MOUSE}{pad:{FIRE}} fires.", new[] { "RMB", "LMB" }, true);
            ctx.Hint(V(-4f, 2f, RangeZ), V(16f, 5f, 18f),
                "Switch to whoever carries the gun (weapon icon). Hold {kb:RIGHT MOUSE}{pad:{AIM}} to aim and {kb:LEFT MOUSE}{pad:{FIRE}} to hit all three bullseyes. Bonus: knock the barrel off its shelf.",
                "Pointy end toward the targets.");
            ctx.Checkpoint(V(0f, 0f, 181f), 0f);

            var d = ctx.Decor;
            EnvKit.Decor.TrainingDummy(d, V(-20.5f, 0f, 153.6f), Yaw(80f));
            EnvKit.Decor.TrainingDummy(d, V(-20.5f, 0f, 168.4f), Yaw(100f));
            Sign(s, V(6.75f, 2.4f, 158f), -90f, "AIM AWAY FROM FACE", 3f, 0.8f, SignStyle.Danger);
            Poster(d, V(6.75f, 2.1f, 167f), -90f, "SAFETY FIRST", "SECOND, THIRD AND FOURTH: ALSO SAFETY. FIFTH: GUNS");
            Poster(d, V(-24.75f, 2f, 156f), 90f, "TARGETS HAVE FEELINGS", "THEY DON'T. SHOOT THEM.");
            for (int i = 0; i < 4; i++) EnvKit.Decor.Cone(d, V(-6.4f, 0f, 152.6f + i * 0.6f));
            EnvKit.Decor.Barrel(d, V(5.8f, 0f, 148.2f), 1);
            EnvKit.Decor.Barrel(d, V(6.1f, 0f, 149.2f), 0);
            Panel(V(-16f, 4.1f, 152.25f), 0f, 4f, 0.45f, UIColors.Weapon);
            Panel(V(-16f, 4.1f, 169.75f), 180f, 4f, 0.45f, UIColors.Weapon);
        }

        // ================================================================== S6 SACRIFICE LESSON
        void BuildSacrificeLesson()
        {
            var ctx = _ctx;
            var s = ctx.Section("S6_SacrificeLesson", Vector3.zero);
            var g = ctx.Geometry;
            const float H = 5f;
            Floor(g, -9f, 9f, D5Z, 222f);
            WallZ(g, 9f, D5Z, 222f, H);
            WallZ(g, -9f, D5Z, 190.1f, H); WallZ(g, -9f, 193.9f, 212f, H); WallZ(g, -9f, 216f, 222f, H);
            WallX(g, -9f, -2.25f, D6Z, H); WallX(g, 2.25f, 9f, D6Z, H);

            // booth (x -9..-4, z 188..196) with plate P6; D6 is open only while P6 is pressed
            WallX(g, -9f, -4f, 188f, 3.6f);
            WallZ(g, -4f, 192.5f, D6Z, 3.6f);
            var p6 = PuzzleKit.PressurePlate(ctx.Puzzles, V(-6.8f, 0f, 193.8f), null, Tuning.PlateLight, false, "");
            ctx.Mark("S6.P6", p6.gameObject);
            var d6 = PuzzleKit.Door(ctx.Puzzles, V(0f, 0f, D6Z), null, DoorMode.SlideUp, 3.5f, 4.2f, p6);
            ctx.Mark("S6.D6", d6.gameObject);
            PuzzleKit.Cable(s, V(-5.7f, 0f, 194.4f), V(-1.4f, 0f, D6Z - 0.6f), p6);

            // vent "ALTERNATE ROUTE": latches open once D6 was opened AND closed again with someone in the booth
            var wasOpened = PuzzleKit.LogicGate(ctx.Puzzles, V(-8.2f, 0f, 188.65f), null, SignalLogic.Latch, false, p6);
            var plateOff = PuzzleKit.LogicGate(ctx.Puzzles, V(-7.1f, 0f, 188.65f), null, SignalLogic.All, true, p6);
            var booth = PuzzleKit.ZoneTrigger(ctx.Puzzles, V(-6.5f, 0f, 192f), null, V(4.6f, 3f, 7.6f), ZoneFilter.AnyCharacter, 1, false, "");
            ctx.Mark("S6.Booth", booth.gameObject);
            var vent = PuzzleKit.Door(ctx.Puzzles, V(-9f, 0f, 192f), Yaw(90f), DoorMode.SlideUp, 2.8f, 4f, wasOpened, plateOff, booth);
            vent.Logic = SignalLogic.Latch;
            ctx.Mark("S6.Vent", vent.gameObject);
            var recovery = Kit.Make("Tutorial_recovery", ctx.Gameplay, V(-8f, 1f, 192f)).AddComponent<L1SignalTutorial>();
            recovery.Message = new TutorialMessage { Id = "recovery", Title = "RECOVERY", Body = "Most sacrifices are recoverable. Most. Take the vent and catch up with your team.", Keys = new string[0], Important = true, Duration = 0f };
            recovery.Logic = SignalLogic.Latch;
            PuzzleKit.Wire(recovery, wasOpened, plateOff, booth);
            ctx.Mark("tutorial.recovery", recovery.gameObject);
            ctx.MarkPoint("S6.BoothEntry", V(-2.2f, 0f, 189.8f));
            ctx.MarkPoint("S6.BoothInside", V(-5.6f, 0f, 190.4f));
            ctx.MarkPoint("S6.BoothBack", V(-8.05f, 0f, 190.95f));
            ctx.MarkPoint("S6.D6Near", V(0f, 0f, D6Z - 2.2f));
            ctx.MarkPoint("S6.D6Far", V(0f, 0f, D6Z + 3f));

            Sign(s, V(-6.5f, 3.95f, 187.75f), 180f, "SACRIFICE BOOTH", 4.2f, 0.75f, SignStyle.Danger); // read on the way in
            Sign(s, V(-3.8f, 2.4f, 194.2f), 90f, "PLEASE HOLD.\nYOUR SACRIFICE IS\nIMPORTANT TO US.", 2.6f, 1.3f);
            Sign(s, V(-8.75f, 4.55f, 192f), 90f, "ALTERNATE ROUTE", 3.2f, 0.6f, SignStyle.Exit);
            IQNote(s, V(-6.5f, 2.5f, 195.75f), 0f, "PSST: THE VENT OPENS WHEN THE DOOR CLOSES ON YOU", 0.26f);
            ctx.Tutorial(V(0f, 1.5f, 186f), V(18f, 3.5f, 6f), "sacrifice", "SACRIFICE",
                "Sometimes the only way forward is to sacrifice someone. One intern holds the booth plate, the others go through.", new string[0], true);
            ctx.Hint(V(0f, 2f, 192f), V(18f, 5f, 14f),
                "Leave one intern on the booth plate while the others walk through D6. Then switch back, step off the plate (the door closes) and take the ALTERNATE ROUTE vent behind you.",
                "Heroes stay behind. Then they crawl through vents. It's in the contract.");

            // duct parkour: vent -> west -> north with two small foam jumps -> east -> rejoin the team
            Floor(g, -21f, -9f, 190f, 194f, 0f, MatKey.FloorAlt);
            Floor(g, -21f, -17f, 194f, 199f, 0f, MatKey.FloorAlt);
            Floor(g, -21f, -17f, 200.5f, 205f, 0f, MatKey.FloorAlt);
            Floor(g, -21f, -17f, 206.5f, 212f, 0f, MatKey.FloorAlt);
            Floor(g, -21f, -9f, 212f, 216f, 0f, MatKey.FloorAlt);
            DuctWallX(s, -21f, -9f, 190f); DuctWallX(s, -17f, -9f, 194f);
            DuctWallZ(s, -21f, 190f, 216f); DuctWallZ(s, -17f, 194f, 212f);
            DuctWallX(s, -17f, -9f, 212f); DuctWallX(s, -21f, -9f, 216f);
            FoamPit(s, -21f, -17f, 199f, 200.5f, V(-19f, 0f, 196.8f));
            FoamPit(s, -21f, -17f, 205f, 206.5f, V(-19f, 0f, 202.8f));
            ctx.MarkPoint("S6.VentIn", V(-10.6f, 0f, 192f));
            ctx.MarkPoint("S6.Duct1End", V(-19f, 0f, 192f));
            ctx.MarkPoint("S6.Duct2Start", V(-19f, 0f, 195.5f));
            ctx.MarkPoint("S6.Gap1Takeoff", V(-19f, 0f, 198.85f));
            ctx.MarkPoint("S6.Gap1Landing", V(-19f, 0f, 202f));
            ctx.MarkPoint("S6.Gap2Takeoff", V(-19f, 0f, 204.85f));
            ctx.MarkPoint("S6.Gap2Landing", V(-19f, 0f, 208f));
            ctx.MarkPoint("S6.Duct3Start", V(-19f, 0f, 214f));
            ctx.MarkPoint("S6.Rejoin", V(-6f, 0f, 214f));
            EnvKit.Pipe(s, new[] { V(-20.6f, 2.9f, 190.6f), V(-20.6f, 2.9f, 215.4f), V(-9.5f, 2.9f, 215.4f) }, 0.16f);
            EnvKit.Pipe(s, new[] { V(-9.5f, 3.2f, 190.5f), V(-20.5f, 3.2f, 190.5f) }, 0.1f, false, MatKey.SafetyOrange);
            Sign(s, V(-17.2f, 2.2f, 197f), -90f, "<ALTERNATE ROUTE\n(THE LONG WAY)", 2.6f, 1f, SignStyle.Arrow); // arrow points on (north)
            Sign(s, V(-17.2f, 2.2f, 209.5f), -90f, "YOU ARE A HERO\n(UNPAID)", 2.4f, 1f);
            Sign(s, V(-15f, 2.2f, 215.75f), 180f, "> TEAM THIS WAY", 3f, 0.7f, SignStyle.Arrow);
            FloorText(s, V(-19f, 0f, 197.6f), 0f, "HOP", 0.6f, UIColors.AccentYellow, 3f);
            FloorText(s, V(-19f, 0f, 203.6f), 0f, "HOP AGAIN", 0.5f, UIColors.AccentYellow, 3f);
            ctx.Hint(V(-17f, 2f, 203f), V(10f, 5f, 28f),
                "Follow the duct: two small hops over foam and you are back with your team.",
                "Ducts always lead somewhere. Usually.");

            ctx.Checkpoint(V(2f, 0f, 218f), 0f);

            var d = ctx.Decor;
            BenchFacing(d, V(8.2f, 0f, 184f), -90f, 2f);
            EnvKit.Decor.Plant(d, V(8.2f, 0f, 177.4f), 0);
            EnvKit.Decor.Plant(d, V(8.2f, 0f, 220.8f), 1);
            Poster(d, V(8.75f, 2.1f, 190f), -90f, "SACRIFICE", "IT'S ONLY WEIRD IF YOU MAKE IT WEIRD");
            Poster(d, V(8.75f, 2.1f, 205f), -90f, "NIGHT SHIFT ONLY", "DO NOT FEED THE ROBOTS");
            Board(d, V(6.5f, 0f, 210f), -110f, "TEAM BUILDING:\nLEAVE NO INTERN\nBEHIND (FOR LONG)");
            EnvKit.Decor.CrateStack(d, V(-20f, 0f, 210.6f), Yaw(0f), 2, 3);
            Panel(V(-8.78f, 3.4f, 186f), 90f, 2.4f, 0.4f, UIColors.Danger);
            Panel(V(-19f, 3.3f, 215.78f), 180f, 2.2f, 0.4f, UIColors.AccentYellow); // on the duct's north wall, lights the way out
        }

        // ================================================================== S7 GRADUATION
        void BuildGraduation()
        {
            var ctx = _ctx;
            var s = ctx.Section("S7_Graduation", Vector3.zero);
            var g = ctx.Geometry;
            const float H = 6f;
            Floor(g, -12f, 12f, 222f, 257f);
            WallZ(g, -12f, 222f, 257f, H); WallZ(g, 12f, 222f, 257f, H);
            WallX(g, -12f, 12f, 257f, H);
            WallX(g, -12f, -9f, 222f, 5f, 0f, false); WallX(g, 9f, 12f, 222f, 5f, 0f, false);

            var pa = PuzzleKit.PressurePlate(ctx.Puzzles, V(-8.5f, 0f, 229f), null, Tuning.PlateLight, false, "");
            var pb = PuzzleKit.PressurePlate(ctx.Puzzles, V(8.5f, 0f, 229f), null, Tuning.PlateLight, false, "");
            var pc = PuzzleKit.PressurePlate(ctx.Puzzles, V(0f, 0f, 239.5f), null, Tuning.PlateLight, false, "");
            ctx.Mark("S7.PA", pa.gameObject); ctx.Mark("S7.PB", pb.gameObject); ctx.Mark("S7.PC", pc.gameObject);
            var gate = PuzzleKit.Door(ctx.Puzzles, V(0f, 0f, GateZ), null, DoorMode.SlideUp, 5f, 5f, pa, pb, pc);
            gate.Logic = SignalLogic.Latch;
            ctx.Mark("S7.Gate", gate.gameObject);
            WallX(g, -12f, -3f, GateZ, 6.5f); WallX(g, 3f, 12f, GateZ, 6.5f);
            PuzzleKit.Cable(s, V(-7.4f, 0f, 229.4f), V(-1.8f, 0f, GateZ - 0.6f), pa);
            PuzzleKit.Cable(s, V(7.4f, 0f, 229.4f), V(1.8f, 0f, GateZ - 0.6f), pb);
            PuzzleKit.Cable(s, V(0.6f, 0f, 240.6f), V(0.6f, 0f, GateZ - 0.6f), pc);
            FloorText(s, V(-8.5f, 0f, 226.9f), 0f, "GRADUATE #1", 0.42f, UIColors.AccentYellow, 3f);
            FloorText(s, V(8.5f, 0f, 226.9f), 0f, "GRADUATE #2", 0.42f, UIColors.AccentYellow, 3f);
            FloorText(s, V(0f, 0f, 237.4f), 0f, "GRADUATE #3", 0.42f, UIColors.AccentYellow, 3f);
            ctx.MarkPoint("S7.GateNear", V(0f, 0f, GateZ - 3.2f));
            ctx.MarkPoint("S7.ExitInside", V(0f, 0.1f, ExitZ + 2.3f));

            ctx.Exit(V(0f, 0f, ExitZ), 0f);
            Sign(s, V(0f, 7.35f, ExitZ - 0.45f), 180f, "GRADUATION ELEVATOR - END OF SHIFT", 7f, 0.9f, SignStyle.Exit);
            Sign(s, V(0f, 5.85f, GateZ - 0.5f), 180f, "CONGRATULATIONS (PROVISIONALLY)", 7.5f, 0.8f, SignStyle.Info);

            ctx.Tutorial(V(0f, 1.5f, 225f), V(18f, 3.5f, 6f), "graduation", "GRADUATION",
                "Three plates, three interns. Stand on all of them at the same time to open the gate.", new[] { "1", "2", "3" });
            ctx.Tutorial(V(0f, 1.5f, 243f), V(10f, 3.5f, 5f), "exit", "EXIT",
                "Get everyone into the exit elevator.", new string[0], true);
            ctx.Hint(V(0f, 2f, 234f), V(24f, 5f, 24f),
                "Walk one intern onto each glowing plate (switch with {kb:1/2/3}{pad:{Switcher}} - the others stay put). When all three are pressed the gate latches open. Then everyone into the elevator.",
                "Three plates. You have exactly enough interns. Barely.");

            var d = ctx.Decor;
            for (int i = 0; i < 4; i++)
            {
                float z = 224f + i * 7f;
                EnvKit.Decor.Plant(d, V(-11.2f, 0f, z), i % 3);
                EnvKit.Decor.Plant(d, V(11.2f, 0f, z), (i + 1) % 3);
            }
            Poster(d, V(-11.75f, 2.2f, 236f), 90f, "YOU DID IT", "HR WILL BE IN TOUCH. HR IS ALWAYS IN TOUCH.");
            Poster(d, V(11.75f, 2.2f, 236f), -90f, "ALUMNI WALL OF FAME", "(UNDER CONSTRUCTION SINCE FOREVER)");
            Poster(d, V(-11.75f, 2.2f, 243f), 90f, "GRAVITY", "IT'S NOT JUST A GOOD IDEA. IT'S THE LAW.");
            BenchFacing(d, V(-6f, 0f, 255.8f), 180f, 2.2f);
            BenchFacing(d, V(6f, 0f, 255.8f), 180f, 2.2f);
            EnvKit.Decor.TrainingDummy(d, V(10.6f, 0f, 252.5f), Yaw(-150f));
            Panel(V(-11.78f, 4.6f, 230f), 90f, 4f, 0.5f, UIColors.AccentYellow);
            Panel(V(11.78f, 4.6f, 230f), -90f, 4f, 0.5f, UIColors.AccentYellow);
            Panel(V(0f, 6.4f, GateZ - 0.25f), 180f, 5f, 0.4f, UIColors.Accent, true, 2.2f, 12f);
        }
    }
}
