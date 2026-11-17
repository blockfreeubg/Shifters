// =====================================================================================
//  SHIFTERS - L3Builder.S4.cs   S4 Distraction Gauntlet + exit
//  Two BONK-BOTs patrol around a 100 kg plate in front of the exit door (arena center z 162, r 8).
//  The plate must be held 3 s (SECURITY LATCH) - Brute alone, or Sprinter + Brainiac together.
//  West: LURE GALLERY (2 m, stairs from the safe entry) with a PROW pulpit pointing into the arena and
//  the cell release lever. The holding cell is by the entry, outside the arena, so a capture costs a
//  walk back, never a loop. The gallery runs to a STAFF HATCH in the far wall that opens with the same
//  latch, so the lure rejoins the team in the exit lobby. Distractions: taunt from the prow, gunshots
//  (noise + stun), or a thrown teammate (bowling!).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L3Builder
    {
        void BuildS4(LevelBuildContext ctx)
        {
            var g = ctx.Section("S4_DistractionGauntlet", Vector3.zero);
            Floor(g, -10f, 10f, 135f, 175f);
            Wall(g, -10f, 135f, -10f, 175f);
            Wall(g, 10f, 135f, 10f, 175f);
            // far bulkhead: main door (x -2.2..2.2) + staff hatch on the gallery (x -10.15..-6.35, y 2..6.5)
            Wall(g, -6.35f, 175f, -2.2f, 175f, 6.5f);
            Wall(g, 2.2f, 175f, 10f, 175f, 6.5f);
            Kit.SolidBox(g, "DoorTop", new Vector3(0f, 5.7f, 175f), new Vector3(4.4f, 1.6f, 0.4f), MatKey.Wall, 0.02f);

            // lure gallery + stairs + pulpit
            Block(g, -10f, -6.5f, 140.8f, 175f, 2f);
            EnvKit.Stairs(g, new Vector3(-8.25f, 0f, 137f), Vector3.forward, 3.5f, 0.2f, 0.38f, 10);
            var prow4 = new Vector3(-3.5f, 0f, 159.5f);
            LureProw(g, ctx.Decor, prow4, 1f, "LURE\nHERE (T)");
            RailingAroundProw(g, -6.5f, 140.8f, 174.6f, prow4, 1f);
            Sign(ctx.Decor, new Vector3(-4.51f, 1.2f, 158.41f), 135f, "BOTS: QUEUE AT THE TIP. NO CLIMBING.", 2.6f, 0.5f, SignStyle.Warning);
            Sign(ctx.Decor, new Vector3(-6.44f, 1.3f, 142f), 90f, "<LURE GALLERY", 3f, 0.55f, SignStyle.Arrow);

            // cell (east wall, next to the safe entry, outside the arena) + release lever on the gallery
            var cell = EnemyKit.HoldingCell(ctx.Gameplay, new Vector3(8f, 0f, 147.5f), Yaw(-90f), "L3_cell_gauntlet");
            ctx.Mark("S4.Cell", cell.gameObject);
            var cellLever = PuzzleKit.Lever(ctx.Puzzles, new Vector3(-9.3f, 2f, 143.2f), Yaw(180f), 0, false, false, "");
            ctx.Mark("S4.CellLever", cellLever.gameObject);
            PuzzleKit.Wire(cell, cellLever);
            Sign(ctx.Decor, new Vector3(-9.78f, 3.2f, 143.2f), 90f, "CELL RELEASE", 2.2f, 0.5f, SignStyle.Danger);

            // charging dock against the east wall: closes the strip outside the arena circle, so nobody can stroll
            // past the bots along the wall without a lure (it used to be the cell's job)
            Kit.SolidBox(g, "ChargingDock", new Vector3(7.975f, 1.5f, 160f), new Vector3(3.65f, 3f, 4.2f), MatKey.MetalDark, 0.05f);
            Kit.BoxVisual(ctx.Decor, "DockStripe", new Vector3(6.14f, 0.45f, 160f), new Vector3(0.04f, 0.5f, 4.1f), MatKey.HazardStripes, 0.01f);
            Kit.BoxVisual(ctx.Decor, "DockPad", new Vector3(6.6f, 3.03f, 160f), new Vector3(0.8f, 0.06f, 3.6f), MatKey.LightBlue, 0.02f);
            Sign(ctx.Decor, new Vector3(6.1f, 2.1f, 160f), -90f, "BONK-BOT CHARGING DOCK. DO NOT LICK.", 3.6f, 0.55f, SignStyle.Warning);

            // two bots, same loop, opposite phase
            var loop = new[] { new Vector3(-0.8f, 0f, 155.5f), new Vector3(4f, 0f, 155.5f), new Vector3(4f, 0f, 168f), new Vector3(-0.8f, 0f, 168f) };
            var botA = EnemyKit.SecurityBot(ctx.Enemies, new Vector3(-0.8f, 0f, 160f), Yaw(0f), loop, "L3_cell_gauntlet", S4ArenaCenter, S4ArenaRadius);
            ctx.Mark("S4.BotA", botA.gameObject);
            var loopB = new[] { loop[2], loop[3], loop[0], loop[1] };
            var botB = EnemyKit.SecurityBot(ctx.Enemies, new Vector3(4f, 0f, 165f), Yaw(180f), loopB, "L3_cell_gauntlet", S4ArenaCenter, S4ArenaRadius);
            ctx.Mark("S4.BotB", botB.gameObject);
            ctx.Label(ctx.Decor, new Vector3(1f, 0.02f, 151.6f), Quaternion.Euler(90f, 0f, 0f), "DOUBLE BONK ZONE", 0.7f, AlarmRed, 9f);
            PuzzleVisuals.StripedBox(ctx.Decor, "ArenaLine", new Vector3(1.5f, 0.006f, 151f), new Vector3(10f, 0.012f, 0.3f), 0, 0.4f, false);

            // 100 kg plate -> 3 s latch -> exit door + staff hatch
            // the whole 2x2 m plate lies outside the bots' reach (nearest point 9.1 m from the arena center; they grab within 8.5)
            var plate = PuzzleKit.PressurePlate(ctx.Puzzles, new Vector3(2.5f, 0f, 172f), null, Tuning.PlateMedium, false, "");
            ctx.Mark("S4.Plate", plate.gameObject);
            var latch = L3Kit.HoldLatch(ctx.Puzzles, new Vector3(5.2f, 0f, 173.6f), Yaw(180f), plate, 3f);
            latch.SolvedMessage = "GAUNTLET DOOR LATCHED OPEN";
            ctx.Mark("S4.Latch", latch.gameObject);
            var door = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 175f), null, DoorMode.SlideUp, 3.4f, 4.4f, latch);
            ctx.Mark("S4.Door", door.gameObject);
            var hatch = PuzzleKit.Door(ctx.Puzzles, new Vector3(-8.25f, 2f, 175f), null, DoorMode.SlideUp, 2.8f, 4f, latch);
            ctx.Mark("S4.Hatch", hatch.gameObject);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(3.5f, 0f, 172.6f), new Vector3(4.7f, 0f, 173.4f), plate);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(4.7f, 0f, 174.2f), new Vector3(2.3f, 0f, 174.4f), latch);

            // exit lobby: landing behind the hatch, stairs down, the exit elevator
            Floor(g, -10f, 10f, 175f, 190f, 0f, MatKey.FloorAlt);
            Wall(g, -10f, 175f, -10f, 190f);
            Wall(g, 10f, 175f, 10f, 190f);
            Wall(g, -10f, 190f, 10f, 190f);
            Block(g, -10f, -6.5f, 175.2f, 178.5f, 2f);
            EnvKit.Stairs(g, new Vector3(-8.25f, 0f, 182.3f), Vector3.back, 3.5f, 0.2f, 0.38f, 10);
            EnvKit.Railing(g, new Vector3(-6.5f, 2f, 175.4f), new Vector3(-6.5f, 2f, 178.5f));
            ctx.Exit(new Vector3(3f, 0f, 182f), 0f);

            // marks
            ctx.MarkPoint("S4.Entry", new Vector3(3f, 0f, 143.5f));
            ctx.MarkPoint("S4.Entry2", new Vector3(5f, 0f, 144.5f));
            ctx.MarkPoint("S4.StairBottom", new Vector3(-8.2f, 0f, 136.35f));
            ctx.MarkPoint("S4.StairApproach", new Vector3(-5.6f, 0f, 136.45f));
            ctx.MarkPoint("S4.StairTop", new Vector3(-8.1f, 2f, 141.6f));
            ctx.MarkPoint("S4.GalleryMid", new Vector3(-8.1f, 2f, 152f));
            ctx.MarkPoint("S4.PulpitEntry", new Vector3(-6f, 2f, 159.5f));
            ctx.MarkPoint("S4.GalleryBendS", new Vector3(-8.0f, 2f, 158.4f));
            ctx.MarkPoint("S4.GalleryBendN", new Vector3(-8.0f, 2f, 160.6f));
            ctx.MarkPoint("S4.Pulpit", new Vector3(-4.0f, 2f, 159.5f));
            ctx.MarkPoint("S4.GalleryNorth", new Vector3(-8.1f, 2f, 168f));
            ctx.MarkPoint("S4.HatchFront", new Vector3(-8.25f, 2f, 173.8f));
            ctx.MarkPoint("S4.HatchBeyond", new Vector3(-8.25f, 2f, 177.3f));
            ctx.MarkPoint("S4.LobbyStairsBottom", new Vector3(-8.25f, 0f, 183f));
            ctx.MarkPoint("S4.Cross1", new Vector3(4.6f, 0f, 150f));
            ctx.MarkPoint("S4.LaneS", new Vector3(5.4f, 0f, 152.5f));
            ctx.MarkPoint("S4.LaneMid", new Vector3(5.2f, 0f, 160f));
            ctx.MarkPoint("S4.LaneN", new Vector3(5.5f, 0f, 166.4f));
            ctx.MarkPoint("S4.PlateApproach", new Vector3(4.6f, 0f, 170.4f));
            ctx.MarkPoint("S4.NorthOfPlate", new Vector3(3.9f, 0f, 173.9f));
            ctx.MarkPoint("S4.CellExit", new Vector3(5.2f, 0f, 147.5f));
            ctx.MarkPoint("S4.CellStage", new Vector3(-8.4f, 2f, 145.6f));
            ctx.MarkPoint("S4.Cross2", new Vector3(2.6f, 0f, 168.2f));
            ctx.MarkPoint("S4.PlateSpot", new Vector3(2.5f, 0f, 172f));
            ctx.MarkPoint("S4.PlateSide", new Vector3(5.1f, 0f, 171.4f));
            ctx.MarkPoint("S4.DoorFront", new Vector3(0f, 0f, 173.4f));
            ctx.MarkPoint("S4.Lobby", new Vector3(0f, 0f, 178f));
            ctx.MarkPoint("S4.ExitInside", new Vector3(3f, 0.1f, 184.2f));
            ctx.MarkPoint("S4.Cross2b", new Vector3(0.6f, 0f, 167.0f));
            ctx.MarkPoint("S4.LobbyWait", new Vector3(4.5f, 0f, 179.0f));
            ctx.MarkPoint("S4.LobbyWait2", new Vector3(7.0f, 0f, 178.5f));
            ctx.MarkPoint("S4.CabFront", new Vector3(3f, 0f, 180.6f));
            ctx.MarkPoint("S4.CabFrontL", new Vector3(2.0f, 0f, 180.6f));
            ctx.MarkPoint("S4.CabFrontR", new Vector3(4.0f, 0f, 180.6f));
            ctx.MarkPoint("S4.CabL", new Vector3(2.0f, 0.1f, 184.2f));
            ctx.MarkPoint("S4.CabC", new Vector3(3.0f, 0.1f, 185.0f));
            ctx.MarkPoint("S4.CabR", new Vector3(4.0f, 0.1f, 184.2f));

            ctx.Hint(new Vector3(0f, 2f, 150f), new Vector3(20f, 5f, 26f),
                "Lure both bots: taunt (T) from the gallery pulpit. While they wait below, the Brute (or Sprinter + Brainiac together) holds the 100 KG plate for 3 s. The latch also opens the staff hatch at the end of the gallery for the lure.",
                "Two bots, one plate, zero dignity. Somebody be loud somewhere high.");
            ctx.Tutorial(new Vector3(3f, 1.5f, 145f), new Vector3(10f, 3f, 6f), "l3_distract", "DISTRACTION",
                "Bots investigate noises: taunts, gunshots, thrown teammates landing. A teammate thrown INTO a bot knocks it over. Bowling!", new[] { "T", "LMB", "Q" });
            PerspectiveKit.Clue(ctx.Decor, new Vector3(9.78f, 2.8f, 152.5f), Yaw(90f), "PLATE 100 KG. LATCH 3 S. STAFF HATCH OPENS TOO.", 0.3f);

            // dressing
            Sign(ctx.Decor, new Vector3(0f, 5.0f, 174.75f), 180f, "EXIT: THANK YOU FOR SURVIVING (STATISTICALLY UNLIKELY)", 7.0f, 0.6f, SignStyle.Exit);
            Sign(ctx.Decor, new Vector3(-8.25f, 6.8f, 174.75f), 180f, "STAFF HATCH", 2.6f, 0.45f, SignStyle.Info);
            Sign(ctx.Decor, new Vector3(9.75f, 2.6f, 164.6f), -90f, "THE ROBOTS ARE FRIENDLY. THE ROBOTS ARE FRIENDLY. THE ROBOTS ARE", 4.8f, 0.6f, SignStyle.Warning);
            Poster(ctx.Decor, new Vector3(9.75f, 2.2f, 168f), -90f, "SECURITY TIP", "IF A ROBOT CHASES YOU, RUN. ALSO SCREAM.");
            Poster(ctx.Decor, new Vector3(-9.75f, 4.3f, 165f), 90f, "SPECIMEN 42", "DO NOT MAKE EYE CONTACT");
            Poster(ctx.Decor, new Vector3(-6f, 2.2f, 189.75f), 180f, "CONGRATULATIONS", "YOU HAVE BEEN PROMOTED TO SURVIVOR");
            Sign(ctx.Decor, new Vector3(6.5f, 3.6f, 189.75f), 180f, "SHIFT CHANGE IN: NEVER", 3.6f, 0.6f, SignStyle.Warning);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(9f, 0f, 137f), 2.3f, 0.55f);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(-4.6f, 0f, 173.8f), 2.0f, 0.5f);
            L3Kit.BrokenTank(ctx.Decor, new Vector3(8.6f, 0f, 171f), 0.6f, 41);
            EnvKit.Decor.DeskWithMonitors(ctx.Decor, new Vector3(-9.3f, 2f, 150f), Yaw(90f));
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(1f, 0f, 162f), new Vector2(6f, 6f), 23);
            EnvKit.Decor.Debris(ctx.Decor, new Vector3(6f, 0f, 152f), 1.2f, 9, 24);
            EnvKit.Decor.Plant(ctx.Decor, new Vector3(8.6f, 0f, 188.6f), 0);
            EnvKit.Decor.Plant(ctx.Decor, new Vector3(-2f, 0f, 188.8f), 2);
            EnvKit.Decor.Bench(ctx.Decor, new Vector3(-3f, 0f, 186.5f), Yaw(90f), 2f);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(9.7f, 4.2f, 135.5f), new Vector3(9.7f, 4.2f, 174.5f) }, 0.15f);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(-9.7f, 5.5f, 140f), new Vector3(-9.7f, 5.5f, 174.5f) }, 0.12f);
            Panel(ctx.Lights, new Vector3(9.75f, 3.9f, 158f), -90f, LabWhite, true, true, 2.4f, 16f);
            Panel(ctx.Lights, new Vector3(9.75f, 3.9f, 141f), -90f, LabGreen, true);
            Panel(ctx.Lights, new Vector3(-9.75f, 4.4f, 156f), 90f, AlarmRed, true);
            Panel(ctx.Lights, new Vector3(0f, 4.0f, 189.75f), 180f, LabWhite, false, true, 2.2f, 14f);
            EnvKit.Decor.WarningBeacon(ctx.Decor, new Vector3(1.95f, 4.9f, 175f), false);
            EnvKit.Decor.WarningBeacon(ctx.Decor, new Vector3(-1.95f, 4.9f, 175f), false);
        }
    }
}
