// =====================================================================================
//  SHIFTERS - L3Builder.S0S1.cs   S0 Decontamination + S1 Bot Hall
//  S1: 20 x 38 m hall, one BONK-BOT patrolling a circular arena (center z 31, r 11). The entry
//  (z < 19.5) and the far end (z > 42.5) are outside the arena. East: SECURITY GALLERY (2 m high,
//  stairs from the entry) with a TAUNT PROW pointing into the arena and the CELL RELEASE lever; the
//  holding cell itself stands in the safe entry zone. Far end: 40 kg plate -> 1 s SECURITY LATCH ->
//  exit door. A retracted catwalk links the gallery to the far landing; its lever is on the landing
//  (reached by far-side stairs) so the taunter can rejoin the team once somebody made it across.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L3Builder
    {
        void BuildS0(LevelBuildContext ctx)
        {
            var s = ctx.Section("S0_Decontamination", Vector3.zero);
            var g = s;
            Floor(g, -7f, 7f, -8f, 9f, 0f, MatKey.FloorAlt);
            Wall(g, -7f, -8f, -7f, 9f);
            Wall(g, 7f, -8f, 7f, 9f);
            Wall(g, -7f, -8f, 7f, -8f);
            WallWithGap(g, 9f, -7f, 7f, -3f, 3f);
            Kit.SolidBox(g, "Lintel", new Vector3(0f, 4.45f, 9f), new Vector3(6.4f, 0.3f, 0.5f), MatKey.MetalDark, 0.04f);

            ctx.SetSpawn(new Vector3(0f, 0f, -4.5f), 0f);
            ctx.Tutorial(new Vector3(0f, 1.5f, -3f), new Vector3(13f, 3f, 9f), "l3_bots", "SECURITY BOTS",
                "Security bots chase the closest intern. Press {Taunt} to taunt and distract them. Or throw a teammate at them.",
                new[] { "T", "F", "Q" }, true);

            // decontamination showers + flavour
            L3Kit.DeconArch(ctx.Decor, new Vector3(0f, 0f, 1.5f), Quaternion.identity, 5f);
            L3Kit.DeconArch(ctx.Decor, new Vector3(0f, 0f, 5.5f), Quaternion.identity, 5f);
            Sign(ctx.Decor, new Vector3(0f, 3.75f, 8.7f), 180f, "DECONTAMINATION COMPLETE. PROBABLY.", 6.2f, 0.75f, SignStyle.Warning);
            Poster(ctx.Decor, new Vector3(-2.2f, 2.1f, -7.75f), 0f, "HR NOTICE", "SACRIFICES MUST BE PRE-APPROVED");
            Poster(ctx.Decor, new Vector3(2.2f, 2.1f, -7.75f), 0f, "NIGHT SHIFT ONLY", "DO NOT FEED THE ROBOTS. THEY BITE. LITIGIOUSLY.");
            Sign(ctx.Decor, new Vector3(0f, 4.2f, -7.75f), 0f, "YOUR SHIFT HAS BEGUN", 4.2f, 0.7f, SignStyle.Exit);
            Sign(ctx.Decor, new Vector3(6.75f, 2.6f, -1f), -90f, "QUARANTINE LEVEL: SPICY", 3.2f, 0.6f, SignStyle.Danger);
            Sign(ctx.Decor, new Vector3(-6.75f, 2.6f, 2f), 90f, "LAB 3: ABANDONED SINCE THE INCIDENT", 3.6f, 0.55f, SignStyle.Info);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(-5.6f, 0f, -6.2f));
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(-5.6f, 0f, -3.9f), 2.0f, 0.5f);
            L3Kit.BrokenTank(ctx.Decor, new Vector3(5.5f, 0f, -6f), 0.55f, 4);
            EnvKit.Decor.Bench(ctx.Decor, new Vector3(5.9f, 0f, 2.5f), Yaw(-90f), 2f);
            EnvKit.Decor.Plant(ctx.Decor, new Vector3(-6.2f, 0f, 7.9f), 1);
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(2.5f, 0f, -2f), new Vector2(3f, 3f), 7);
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(-3f, 0f, 6.5f), new Vector2(2.5f, 2f), 8);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(-6.7f, 4.0f, -7.7f), new Vector3(-6.7f, 4.0f, 8.6f) }, 0.14f);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(6.7f, 3.6f, -7.7f), new Vector3(6.7f, 3.6f, 4f), new Vector3(6.7f, 0.4f, 4f) }, 0.1f);
            Panel(ctx.Lights, new Vector3(0f, 3.6f, -7.75f), 0f, LabGreen, false, true, 2.6f, 14f);
            Panel(ctx.Lights, new Vector3(-6.75f, 3.7f, -3f), 90f, LabWhite, true);
            Panel(ctx.Lights, new Vector3(6.75f, 3.7f, 5f), -90f, LabWhite, true);
            ctx.Label(ctx.Decor, new Vector3(0f, 0.02f, 3.5f), Quaternion.Euler(90f, 0f, 0f), "STAND STILL. THINK CLEAN THOUGHTS.", 0.35f, LabGreen, 6f);
        }

        void BuildS1(LevelBuildContext ctx)
        {
            var s = ctx.Section("S1_BotHall", Vector3.zero);
            var g = s;
            // ---------------------------------------------------------------- structure
            Floor(g, -10f, 10f, 9f, 47f);
            Wall(g, -10f, 9f, -10f, 47f);
            Wall(g, 10f, 9f, 10f, 47f);
            Wall(g, -10f, 9f, -7f, 9f);
            Wall(g, 7f, 9f, 10f, 9f);
            WallWithGap(g, 47f, -10f, 10f, -4.2f, 0.2f, 5.2f);

            // security gallery (east, 2 m high) + stairs from the safe entry + taunt pulpit
            Block(g, 6.5f, 10f, 14.1f, 37f, 2f);
            EnvKit.Stairs(g, new Vector3(8.25f, 0f, 10.3f), Vector3.forward, 3.5f, 0.2f, 0.38f, 10);
            var prow1 = new Vector3(3.5f, 0f, 27f);
            LureProw(g, ctx.Decor, prow1, -1f, "TAUNT\nHERE ({TAUNT})");
            RailingAroundProw(g, 6.5f, 14.1f, 37f, prow1, -1f);
            ctx.Label(ctx.Decor, new Vector3(1.0f, 0.02f, 27f), Quaternion.Euler(90f, 90f, 0f), "BONK-BOT QUEUE", 0.32f, UIColors.AccentYellow, 5f);
            Sign(ctx.Decor, new Vector3(4.51f, 1.0f, 25.91f), -135f, "ROBOTS CAN'T CLIMB (WE CHECKED)", 2.6f, 0.5f, SignStyle.Warning);
            Sign(ctx.Decor, new Vector3(6.44f, 1.3f, 16f), -90f, "SECURITY GALLERY", 3.2f, 0.6f, SignStyle.Arrow);

            // far landing + stairs down (outside the arena) + retracted catwalk
            Block(g, 6.5f, 10f, 44.5f, 47f, 2f);
            EnvKit.Stairs(g, new Vector3(2.7f, 0f, 45.75f), Vector3.right, 2.5f, 0.2f, 0.38f, 10);
            var catLever = PuzzleKit.Lever(ctx.Puzzles, new Vector3(9.3f, 2f, 45.75f), Yaw(-90f), 0, false, false, "CATWALK EXTENDED");
            ctx.Mark("S1.CatwalkLever", catLever.gameObject);
            var catwalk = PuzzleKit.RetractableBridge(ctx.Puzzles, new Vector3(8.25f, 2f, 37f), Quaternion.identity, 7.5f, 2.4f, catLever);
            ctx.Mark("S1.Catwalk", catwalk.gameObject);
            Sign(ctx.Decor, new Vector3(9.78f, 3.4f, 45.75f), -90f, "GALLERY CATWALK", 2.2f, 0.45f, SignStyle.Info);
            Sign(ctx.Decor, new Vector3(8.25f, 3.5f, 36.9f), 180f, "CATWALK RETRACTED (BUDGET)", 2.8f, 0.45f, SignStyle.Warning);
            EnvKit.CableTray(ctx.Decor, new Vector3(9.8f, 3.2f, 37.2f), new Vector3(9.8f, 3.2f, 44.6f), 0.4f);

            // ---------------------------------------------------------------- bot + cell
            // The cell sits in the safe entry zone, OUTSIDE the bot arena (13+ m from its center): a released intern starts
            // somewhere safe, and a bot handing over a prisoner can't re-grab the one it just released through the open door.
            var cell = EnemyKit.HoldingCell(ctx.Gameplay, new Vector3(-7.9f, 0f, 17f), Yaw(90f), "L3_cell_hall");
            ctx.Mark("S1.Cell", cell.gameObject);
            var cellLever = PuzzleKit.Lever(ctx.Puzzles, new Vector3(9.3f, 2f, 15.4f), Yaw(180f), 0, false, false, "");
            ctx.Mark("S1.CellLever", cellLever.gameObject);
            PuzzleKit.Wire(cell, cellLever);
            Sign(ctx.Decor, new Vector3(9.78f, 3.2f, 15.4f), -90f, "CELL RELEASE\n(EMERGENCIES & MISTAKES)", 2.4f, 0.8f, SignStyle.Danger);
            L3Kit.WallText(ctx.Decor, new Vector3(-5.6f, 4.35f, 17f), Yaw(90f), "RELEASE LEVER: EAST GALLERY, TOP OF THE STAIRS", 2.2f, UIColors.AccentYellow, 5f, 0.5f);

            var bot = EnemyKit.SecurityBot(ctx.Enemies, new Vector3(0f, 0f, 33f), Yaw(180f),
                new[] { new Vector3(-5f, 0f, 24f), new Vector3(-5f, 0f, 38f), new Vector3(1f, 0f, 38f), new Vector3(1f, 0f, 23.6f) },
                "L3_cell_hall", S1ArenaCenter, S1ArenaRadius);
            ctx.Mark("S1.Bot", bot.gameObject);
            ctx.Label(ctx.Decor, new Vector3(0f, 0.02f, 20f), Quaternion.Euler(90f, 0f, 0f), "BONK-BOT PATROL ZONE", 0.7f, AlarmRed, 9f);
            PuzzleVisuals.StripedBox(ctx.Decor, "ArenaLine", new Vector3(-3.5f, 0.006f, 19.6f), new Vector3(12f, 0.012f, 0.3f), 0, 0.4f, false);

            // ---------------------------------------------------------------- exit: plate -> latch -> door
            var plate = PuzzleKit.PressurePlate(ctx.Puzzles, new Vector3(-6.5f, 0f, 44.4f), null, Tuning.PlateLight, false, "");
            ctx.Mark("S1.Plate", plate.gameObject);
            var latch = L3Kit.HoldLatch(ctx.Puzzles, new Vector3(-9.0f, 0f, 46.2f), Yaw(180f), plate, 1f);
            ctx.Mark("S1.Latch", latch.gameObject);
            latch.SolvedMessage = "BOT HALL DOOR UNLOCKED";
            var door = PuzzleKit.Door(ctx.Puzzles, new Vector3(-2f, 0f, 47f), null, DoorMode.SlideUp, 3.4f, 4.4f, latch);
            ctx.Mark("S1.Door", door.gameObject);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(-6.5f, 0f, 45.4f), new Vector3(-9.0f, 0f, 46.0f), plate);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(-8.5f, 0f, 46.4f), new Vector3(-4.3f, 0f, 46.5f), latch);

            // ---------------------------------------------------------------- bot marks
            ctx.MarkPoint("S1.Entry", new Vector3(-4.6f, 0f, 12.8f));
            ctx.MarkPoint("S1.CellExit", new Vector3(-4.6f, 0f, 17.2f));
            ctx.MarkPoint("S1.CellStage", new Vector3(8.4f, 2f, 17.2f));
            ctx.MarkPoint("S1.Entry2", new Vector3(-4f, 0f, 12.5f));
            ctx.MarkPoint("S1.StairBottom", new Vector3(8.2f, 0f, 9.85f));
            ctx.MarkPoint("S1.StairApproach", new Vector3(5.6f, 0f, 9.95f));
            ctx.MarkPoint("S1.InDoor", new Vector3(1.5f, 0f, 10.4f));
            ctx.MarkPoint("S1.InDoorW", new Vector3(-1.5f, 0f, 10.6f));
            ctx.MarkPoint("S1.InDoorW2", new Vector3(-0.4f, 0f, 11.2f));
            ctx.MarkPoint("S1.GalleryBend", new Vector3(7.9f, 2f, 30f));
            ctx.MarkPoint("S1.FarMid", new Vector3(-1.0f, 0f, 43.4f));
            ctx.MarkPoint("S1.StairTop", new Vector3(8.1f, 2f, 14.7f));
            ctx.MarkPoint("S1.GalleryMid", new Vector3(7.9f, 2f, 22f));
            ctx.MarkPoint("S1.PulpitEntry", new Vector3(6.0f, 2f, 27f));
            ctx.MarkPoint("S1.GalleryBendS", new Vector3(7.9f, 2f, 26.2f));
            ctx.MarkPoint("S1.Pulpit", new Vector3(4.0f, 2f, 27f));
            ctx.MarkPoint("S1.ProwRoot", new Vector3(7.2f, 2f, 27.1f));
            ctx.MarkPoint("S1.GalleryEnd", new Vector3(8.1f, 2f, 36.3f));
            ctx.MarkPoint("S1.CatwalkMid", new Vector3(8.25f, 2f, 41f));
            ctx.MarkPoint("S1.Landing", new Vector3(8.0f, 2f, 45.75f));
            ctx.MarkPoint("S1.FarStairsBottom", new Vector3(2.1f, 0f, 45.75f));
            ctx.MarkPoint("S1.Cross1", new Vector3(-5f, 0f, 20f));
            ctx.MarkPoint("S1.WallS", new Vector3(-7.2f, 0f, 25.5f));
            ctx.MarkPoint("S1.WallN", new Vector3(-7.2f, 0f, 38.5f));
            ctx.MarkPoint("S1.Cross2", new Vector3(-3.4f, 0f, 26f));
            ctx.MarkPoint("S1.Cross3", new Vector3(-3.4f, 0f, 38.5f));
            ctx.MarkPoint("S1.Cross4", new Vector3(-5.5f, 0f, 42.6f));
            ctx.MarkPoint("S1.PlateSpot", new Vector3(-6.5f, 0f, 44.4f));
            ctx.MarkPoint("S1.PlateSide", new Vector3(-4.3f, 0f, 44.0f));
            ctx.MarkPoint("S1.DoorFront", new Vector3(-2f, 0f, 45.4f));
            ctx.MarkPoint("S1.DoorFrontL", new Vector3(-2.8f, 0f, 45.4f));
            ctx.MarkPoint("S1.DoorFrontR", new Vector3(-1.2f, 0f, 45.4f));
            ctx.MarkPoint("S1.Through", new Vector3(-2f, 0f, 48.6f));
            ctx.MarkPoint("S1.ThroughL", new Vector3(-2.8f, 0f, 48.6f));
            ctx.MarkPoint("S1.ThroughR", new Vector3(-1.2f, 0f, 48.6f));
            ctx.MarkPoint("S1.Beyond", new Vector3(-5.5f, 0f, 51.5f));
            ctx.MarkPoint("S1.Beyond2", new Vector3(0.8f, 0f, 51.5f));
            ctx.MarkPoint("S1.Beyond3", new Vector3(-3f, 0f, 50.6f));

            // ---------------------------------------------------------------- hints / tutorials / perspective
            ctx.Hint(new Vector3(0f, 2f, 20f), new Vector3(20f, 5f, 22f),
                "One intern climbs the SECURITY GALLERY and taunts ({Taunt}) from the pulpit: the bot can't climb, so it waits below while the others cross to the 40 KG plate. Then extend the catwalk from the far landing.",
                "Robots can't climb. Someone should stand somewhere high and be extremely annoying.");
            ctx.Hint(new Vector3(0f, 2f, 40f), new Vector3(20f, 5f, 12f),
                "Stand on the plate for 1 second to latch the door. Captured? Pull CELL RELEASE on the gallery.",
                "Plates like company. Cells like levers.");
            ctx.Tutorial(new Vector3(8.25f, 3f, 17f), new Vector3(3.5f, 3f, 5f), "l3_highground", "HIGH GROUND",
                "Bots can't climb. Taunt ({Taunt}) from the pulpit and they'll wait below, beeping furiously.", new[] { "T" });
            ctx.Tutorial(new Vector3(-5f, 1.5f, 17f), new Vector3(3f, 3f, 5f), "l3_cells", "DETENTION",
                "Captured interns go to a holding cell. A free teammate can pull its release lever. Captures are temporary. Probably.", new[] { "E" });
            PerspectiveKit.Clue(ctx.Decor, new Vector3(-9.78f, 2.6f, 24f), Yaw(-90f), "BOTS REACH 1.3 M UP. GALLERY: 2.0 M. MATH.", 0.32f);
            PerspectiveKit.Clue(ctx.Decor, new Vector3(9.78f, 3.2f, 41f), Yaw(90f), "CATWALK LEVER: FAR LANDING", 0.3f);

            // ---------------------------------------------------------------- dressing
            Poster(ctx.Decor, new Vector3(-9.75f, 2.2f, 20.5f), 90f, "BONK-BOT 3000", "NOW WITH 40% LESS EMPATHY");
            Poster(ctx.Decor, new Vector3(-9.75f, 2.2f, 42f), 90f, "DAYS WITHOUT A SHIFT ACCIDENT: 0", "PERSONAL BEST: 0");
            Sign(ctx.Decor, new Vector3(-2f, 5.0f, 46.7f), 180f, "LAB WING B", 3.4f, 0.5f, SignStyle.Exit);
            Sign(ctx.Decor, new Vector3(-6.5f, 2.6f, 46.75f), 180f, "PLATE HOLD = DOOR. WE TRIED A BUTTON ONCE.", 4.2f, 0.45f, SignStyle.Info);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(-9f, 0f, 11f), 2.3f, 0.55f);
            L3Kit.BrokenTank(ctx.Decor, new Vector3(-8.8f, 0f, 39.5f), 0.6f, 9);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(-8.9f, 0f, 26.5f), 2.2f, 0.55f);
            EnvKit.Decor.DeskWithMonitors(ctx.Decor, new Vector3(9.4f, 2f, 20f), Yaw(-90f));
            EnvKit.Decor.DeskWithMonitors(ctx.Decor, new Vector3(9.4f, 2f, 31f), Yaw(-90f));
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(-1f, 0f, 30f), new Vector2(6f, 6f), 11);
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(3f, 0f, 40f), new Vector2(4f, 4f), 12);
            EnvKit.Decor.Debris(ctx.Decor, new Vector3(-1.5f, 0f, 35.5f), 1.4f, 10, 4);
            EnvKit.Decor.Barrel(ctx.Decor, new Vector3(9.2f, 0f, 41.5f), 0);
            EnvKit.Decor.Barrel(ctx.Decor, new Vector3(8.4f, 0f, 42.6f), 1);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(-9.7f, 4.2f, 9.5f), new Vector3(-9.7f, 4.2f, 46.5f) }, 0.15f);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(9.7f, 4.4f, 9.5f), new Vector3(9.7f, 4.4f, 46.5f) }, 0.12f);
            Panel(ctx.Lights, new Vector3(-9.75f, 3.9f, 22f), 90f, LabWhite, true, true, 2.4f, 14f);
            Panel(ctx.Lights, new Vector3(-9.75f, 3.9f, 36f), 90f, LabGreen, false, true, 2.2f, 14f);
            Panel(ctx.Lights, new Vector3(9.75f, 4.0f, 27f), -90f, LabWhite, true);
            Panel(ctx.Lights, new Vector3(-6f, 4.2f, 46.75f), 180f, AlarmRed, true);
        }
    }
}
