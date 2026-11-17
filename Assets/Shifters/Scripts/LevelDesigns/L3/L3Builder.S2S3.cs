// =====================================================================================
//  SHIFTERS - L3Builder.S2S3.cs   S2 Timed Lab Door + S3 The Volunteer Chair
//  S2: TIMED button -> 27 m straight acid-flanked walkway (3.4 m wide) -> big shutter (Any: timer, or the
//      SHUTTER OVERRIDE lever in the control room behind it, which opens it for good).
//  S3: the volunteer sits (E) -> hard-light bridge over a 10 m acid pit; the two others cross and pull
//      RELEASE -> the bridge holds for TimedWindow(21 m) only. The maintenance tunnel (opens on release)
//      is the recovery route: two acid trench jumps, then a bot room crossed on a 1.6 m pipe catwalk.
//      Headcount gate (whole team, latching) closes the section.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L3Builder
    {
        void BuildS2(LevelBuildContext ctx)
        {
            var g = ctx.Section("S2_TimedLabDoor", Vector3.zero);
            // prep room
            Floor(g, -8f, 8f, 47f, 59f);
            Wall(g, -8f, 47f, -8f, 59f);
            Wall(g, 8f, 47f, 8f, 59f);
            EnvKit.Railing(g, new Vector3(-8f, 0f, 59f), new Vector3(-1.75f, 0f, 59f));
            EnvKit.Railing(g, new Vector3(1.75f, 0f, 59f), new Vector3(8f, 0f, 59f));
            ctx.Checkpoint(new Vector3(-4.5f, 0f, 51.5f), 0f);

            // acid room with the walkway
            Floor(g, -1.7f, 1.7f, 59f, 84.4f, 0f, MatKey.FloorAlt);
            AcidPit(g, -8f, -1.7f, 59f, 84.4f);
            AcidPit(g, 1.7f, 8f, 59f, 84.4f);
            Wall(g, -8f, 59f, -8f, 84.4f);
            Wall(g, 8f, 59f, 8f, 84.4f);
            WallWithGap(g, 84.4f, -8f, 8f, -2.2f, 2.2f, 5.2f);

            // control room (shared floor with the S3 near platform)
            Floor(g, -8f, 8f, 84.4f, 109f);
            Wall(g, 8f, 84.4f, 8f, 109f);
            Wall(g, -8f, 84.4f, -8f, 101f);
            Wall(g, -8f, 105f, -8f, 109f);

            var lever = PuzzleKit.Lever(ctx.Puzzles, new Vector3(4.6f, 0f, 87.4f), Yaw(-90f), 0, false, false, "SHUTTER OVERRIDE ENGAGED");
            ctx.Mark("S2.Lever", lever.gameObject);
            var route = new[] { new Vector3(0f, 0f, 58.4f), new Vector3(0f, 0f, 83.8f) };
            var button = PuzzleKit.TimedButton(ctx.Puzzles, new Vector3(0f, 0f, 57.2f), Yaw(180f), S2RouteLength, route, "");
            ctx.Mark("S2.Button", button.gameObject);
            var door = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 84.4f), null, DoorMode.SlideUp, 3.4f, 4.4f, button, lever);
            door.Logic = SignalLogic.Any;
            ctx.Mark("S2.Door", door.gameObject);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(4.2f, 0f, 87.4f), new Vector3(2.3f, 0f, 85.0f), lever);
            PerspectiveKit.SpeedRoute(ctx.Decor, new[] { new Vector3(0f, 0f, 58.6f), new Vector3(0f, 0f, 70f), new Vector3(0f, 0f, 83.6f) }, 1.4f, 1.4f);

            ctx.Checkpoint(new Vector3(-3.5f, 0f, 91.5f), 0f);
            ctx.MarkPoint("S2.Gather", new Vector3(-4.5f, 0f, 55.5f));
            ctx.MarkPoint("S2.Gather2", new Vector3(-2.5f, 0f, 55.5f));
            ctx.MarkPoint("S2.ButtonSide", new Vector3(1.2f, 0f, 57.6f));
            ctx.MarkPoint("S2.ButtonEast", new Vector3(1.0f, 0f, 56.0f));
            ctx.MarkPoint("S2.WalkApproachL", new Vector3(-0.85f, 0f, 58.05f));
            ctx.MarkPoint("S2.WalkStartL", new Vector3(-0.85f, 0f, 59.6f));
            ctx.MarkPoint("S2.WalkEndL", new Vector3(-0.85f, 0f, 83.4f));
            ctx.MarkPoint("S2.WalkStartR", new Vector3(0.85f, 0f, 59.6f));
            ctx.MarkPoint("S2.WalkEndR", new Vector3(0.85f, 0f, 83.4f));
            ctx.MarkPoint("S2.ControlRoom2", new Vector3(-1.2f, 0f, 92.4f));
            ctx.MarkPoint("S2.ButtonStand", new Vector3(0f, 0f, 58.35f));
            ctx.MarkPoint("S2.RunMid", new Vector3(0f, 0f, 71f));
            ctx.MarkPoint("S2.RunEnd", new Vector3(0f, 0f, 86.6f));
            ctx.MarkPoint("S2.LeverStand", new Vector3(3.4f, 0f, 87.4f));
            ctx.MarkPoint("S2.WalkStart", new Vector3(0f, 0f, 59.6f));
            ctx.MarkPoint("S2.WalkEnd", new Vector3(0f, 0f, 83.0f));
            ctx.MarkPoint("S2.ControlRoom", new Vector3(-3.2f, 0f, 90.8f));
            ctx.MarkPoint("S2.InRoomL", new Vector3(-0.85f, 0f, 86.2f));
            ctx.MarkPoint("S2.InRoomR", new Vector3(0.85f, 0f, 86.2f));

            ctx.Hint(new Vector3(0f, 2f, 56f), new Vector3(16f, 5f, 9f),
                "Only TURBO LEGS beat this timer. Sprinter: press TIMED, sprint (LSHIFT) straight down the walkway, then pull SHUTTER OVERRIDE inside so the others can stroll in.",
                "Fast feet. Straight line. Absolutely no sightseeing.");
            ctx.Tutorial(new Vector3(0f, 1.5f, 55f), new Vector3(10f, 3f, 6f), "l3_timed", "TIMED DOOR",
                "Fast characters beat timed doors. Switch to your Sprinter and hold LEFT SHIFT. The others can wait. They're good at that.", new[] { "LSHIFT" });

            // dressing
            Sign(ctx.Decor, new Vector3(0f, 3.5f, 59.2f), 180f, "TIMED DOOR: FAST INTERNS ONLY", 5.2f, 0.6f, SignStyle.Warning);
            Sign(ctx.Decor, new Vector3(-5.5f, 2.6f, 58.9f), 180f, "SLOW INTERNS: THOUGHTS AND PRAYERS", 4.0f, 0.5f, SignStyle.Info);
            Sign(ctx.Decor, new Vector3(5.2f, 2.6f, 58.9f), 180f, "SHIFT INTO TURBO. OR DON'T. WE'RE NOT YOUR MANAGER.", 4.4f, 0.5f, SignStyle.Info);
            Sign(ctx.Decor, new Vector3(7.75f, 2.4f, 72f), -90f, "LAB SAFETY: THE FLOOR IS MOSTLY NOT ACID", 4.4f, 0.55f, SignStyle.Danger);
            Sign(ctx.Decor, new Vector3(-7.75f, 2.4f, 66f), 90f, "ACID: DO NOT TASTE. AGAIN.", 3.6f, 0.55f, SignStyle.Danger);
            Sign(ctx.Decor, new Vector3(0f, 5.0f, 84.0f), 180f, "CONTROL ROOM", 3.0f, 0.5f, SignStyle.Exit);
            Sign(ctx.Decor, new Vector3(7.75f, 2.4f, 87.4f), -90f, "SHUTTER OVERRIDE", 2.6f, 0.5f, SignStyle.Arrow);
            Poster(ctx.Decor, new Vector3(-7.75f, 2.2f, 52f), 90f, "PERSONAL BEST", "BEAT THE DOOR. NOT YOUR COWORKERS.");
            Poster(ctx.Decor, new Vector3(7.75f, 2.2f, 92f), -90f, "NICE RUN", "NOW LET THE SLOWPOKES IN.");
            EnvKit.Decor.DeskWithMonitors(ctx.Decor, new Vector3(5.6f, 0f, 92.5f), Yaw(-90f));
            EnvKit.Decor.DeskWithMonitors(ctx.Decor, new Vector3(-6.6f, 0f, 87.5f), Yaw(90f));
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(-5f, -0.6f, 64f), 2.6f, 0.6f);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(5f, -0.6f, 77f), 2.6f, 0.6f);
            L3Kit.BrokenTank(ctx.Decor, new Vector3(-5.2f, -0.4f, 79f), 0.6f, 21);
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(0f, 0f, 66f), new Vector2(2.6f, 4f), 13);
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(0f, 0f, 78f), new Vector2(2.6f, 3f), 14);
            EnvKit.Railing(g, new Vector3(-1.75f, 0f, 61f), new Vector3(-1.75f, 0f, 63.5f));
            EnvKit.Railing(g, new Vector3(1.75f, 0f, 74f), new Vector3(1.75f, 0f, 75.6f));
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(-7.7f, 3.8f, 47.5f), new Vector3(-7.7f, 3.8f, 108.5f) }, 0.14f);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(7.7f, 4.1f, 59.5f), new Vector3(7.7f, 4.1f, 84f) }, 0.12f);
            Panel(ctx.Lights, new Vector3(-7.75f, 3.9f, 72f), 90f, LabGreen, true, true, 2.4f, 16f);
            Panel(ctx.Lights, new Vector3(7.75f, 3.9f, 64f), -90f, LabWhite, true);
            Panel(ctx.Lights, new Vector3(-7.75f, 3.9f, 53f), 90f, LabWhite, false);
            Panel(ctx.Lights, new Vector3(7.75f, 3.6f, 89f), -90f, LabWhite, true);
        }

        void BuildS3(LevelBuildContext ctx)
        {
            var g = ctx.Section("S3_VolunteerChair", Vector3.zero);
            WallWithGap(g, 97f, -8f, 8f, -2.2f, 2.2f);
            Sign(ctx.Decor, new Vector3(0f, 4.2f, 96.75f), 180f, "VOLUNTEER WING", 3.6f, 0.6f, SignStyle.Info);

            // the deal: RELEASE lever (far side) -> chair -> hard-light bridge
            var release = PuzzleKit.Lever(ctx.Puzzles, new Vector3(3f, 0f, 121.4f), Quaternion.identity, 0, false, false, "");
            ctx.Mark("S3.Release", release.gameObject);
            var chair = L3Kit.VolunteerChair(ctx.Puzzles, new Vector3(3f, 0f, 98.6f), Quaternion.identity, release, S3RouteLength);
            ctx.Mark("S3.Chair", chair.gameObject);
            var bridge = L3Kit.HardLightBridge(ctx.Puzzles, new Vector3(0f, 0f, 109f), Quaternion.identity, 10f, 3.2f, chair);
            ctx.Mark("S3.Bridge", bridge.gameObject);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(1.9f, 0f, 99.9f), new Vector3(1.9f, 0f, 108.5f), chair);

            AcidPit(g, -8f, 8f, 109f, 119f, 3f);
            Wall(g, -8f, 109f, -8f, 119f);
            Wall(g, 8f, 109f, 8f, 119f);

            // far platform
            Floor(g, -8f, 8f, 119f, 135f);
            Wall(g, 8f, 119f, 8f, 135f);
            Wall(g, -8f, 119f, -8f, 128.5f);
            Wall(g, -8f, 133f, -8f, 135f);
            WallWithGap(g, 135f, -10f, 10f, -2.2f, 2.2f, 5.2f);
            var zone = PuzzleKit.ZoneTrigger(ctx.Puzzles, new Vector3(0f, 0f, 127.4f), null, new Vector3(15.6f, 4f, 14.6f), ZoneFilter.WholeTeam, 1, true, "");
            ctx.Mark("S3.Headcount", zone.gameObject);
            var gate = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 135f), null, DoorMode.SlideUp, 3.4f, 4.4f, zone);
            gate.Logic = SignalLogic.Latch;
            ctx.Mark("S3.Gate", gate.gameObject);
            zone.SolvedMessage = "HEADCOUNT COMPLETE. NOBODY LEFT BEHIND (THIS TIME)";

            // maintenance tunnel (opens on release)
            var tunnelDoor = PuzzleKit.Door(ctx.Puzzles, new Vector3(-8f, 0f, 103f), Yaw(90f), DoorMode.SlideUp, 3f, 4.2f, chair.ReleasedSignal);
            ctx.Mark("S3.TunnelDoor", tunnelDoor.gameObject);
            Floor(g, -17f, -8f, 100f, 106f, 0f, MatKey.FloorAlt);
            Floor(g, -17f, -11f, 106f, 110.5f, 0f, MatKey.FloorAlt);
            Floor(g, -17f, -11f, 112.1f, 115.5f, 0.5f, MatKey.PuzzleBody, 1.1f);
            Floor(g, -17f, -11f, 117.1f, 137.6f, 0f, MatKey.FloorAlt);
            Floor(g, -11f, -8f, 128.5f, 133f, 0f, MatKey.FloorAlt);
            AcidPit(g, -17f, -11f, 110.5f, 112.1f, 2f);
            AcidPit(g, -17f, -11f, 115.5f, 117.1f, 2f);
            const float th = 4.2f;
            Wall(g, -17f, 100f, -8f, 100f, th);
            Wall(g, -11f, 106f, -8f, 106f, th);
            Wall(g, -17f, 100f, -17f, 137.6f, th);
            Wall(g, -11f, 106f, -11f, 128.5f, th);
            Wall(g, -11f, 133f, -11f, 137.6f, th);
            Wall(g, -17f, 137.6f, -11f, 137.6f, th);
            Wall(g, -11f, 128.5f, -8f, 128.5f, th);
            Wall(g, -11f, 133f, -8f, 133f, th);

            // bot room: pipe catwalk (1.6 m) the bot can't reach, steps outside its arena
            Block(g, -17f, -15.4f, 117.1f, 118.3f, 0.8f, 0f, MatKey.MetalDark);
            Block(g, -17f, -15.4f, 118.3f, 131.6f, 1.6f, 0f, MatKey.MetalDark);
            Block(g, -17f, -15.4f, 131.6f, 132.8f, 0.8f, 0f, MatKey.MetalDark);
            EnvKit.Railing(g, new Vector3(-15.45f, 1.6f, 119.2f), new Vector3(-15.45f, 1.6f, 131.4f));
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(-15.22f, 1.2f, 118.4f), new Vector3(-15.22f, 1.2f, 131.5f) }, 0.16f);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(-15.25f, 0.55f, 117.2f), new Vector3(-15.25f, 0.55f, 132.7f) }, 0.2f);
            var tbot = EnemyKit.SecurityBot(ctx.Enemies, new Vector3(-13f, 0f, 127f), Yaw(180f),
                new[] { new Vector3(-13f, 0f, 120.5f), new Vector3(-13f, 0f, 129.5f) }, "L3_cell_tunnel", S3ArenaCenter, S3ArenaRadius);
            ctx.Mark("S3.Bot", tbot.gameObject);
            var tcell = EnemyKit.HoldingCell(ctx.Gameplay, new Vector3(-13.2f, 0f, 135.3f), Yaw(180f), "L3_cell_tunnel");
            ctx.Mark("S3.Cell", tcell.gameObject);
            var tcellLever = PuzzleKit.Lever(ctx.Puzzles, new Vector3(-6.4f, 0f, 126.4f), Yaw(90f), 0, false, false, "");
            ctx.Mark("S3.CellLever", tcellLever.gameObject);
            PuzzleKit.Wire(tcell, tcellLever);
            Sign(ctx.Decor, new Vector3(-7.75f, 2.6f, 126.4f), 90f, "TUNNEL CELL RELEASE", 2.6f, 0.5f, SignStyle.Danger);

            // marks for the solution
            ctx.MarkPoint("S3.GapS", new Vector3(-1.0f, 0f, 95.8f));
            ctx.MarkPoint("S3.GapN", new Vector3(-1.0f, 0f, 98.4f));
            ctx.MarkPoint("S3.GapS2", new Vector3(0.6f, 0f, 95.8f));
            ctx.MarkPoint("S3.GapN2", new Vector3(0.6f, 0f, 98.4f));
            ctx.MarkPoint("S3.ChairApproach", new Vector3(0.6f, 0f, 101.6f));
            ctx.MarkPoint("S3.BridgeStartL", new Vector3(-0.7f, 0f, 107.8f));
            ctx.MarkPoint("S3.BridgeEndL", new Vector3(-0.7f, 0f, 120.4f));
            ctx.MarkPoint("S3.BridgeStartR", new Vector3(0.7f, 0f, 107.8f));
            ctx.MarkPoint("S3.BridgeEndR", new Vector3(0.7f, 0f, 120.4f));
            ctx.MarkPoint("S3.GateFront2", new Vector3(1.0f, 0f, 133.2f));
            ctx.MarkPoint("S3.GateFront3", new Vector3(-1.0f, 0f, 133.2f));
            ctx.MarkPoint("S4.GateIn", new Vector3(0f, 0f, 136.8f));
            ctx.MarkPoint("S4.GateIn2", new Vector3(1.0f, 0f, 136.8f));
            ctx.MarkPoint("S4.GateIn3", new Vector3(-1.0f, 0f, 136.8f));
            ctx.MarkPoint("S3.ChairStand", new Vector3(3f, 0f, 100.9f));
            ctx.MarkPoint("S3.BridgeStart", new Vector3(0f, 0f, 107.8f));
            ctx.MarkPoint("S3.BridgeEnd", new Vector3(0f, 0f, 120.4f));
            ctx.MarkPoint("S3.ReleaseStand", new Vector3(3f, 0f, 122.5f));
            ctx.MarkPoint("S3.FarWait", new Vector3(-2f, 0f, 125f));
            ctx.MarkPoint("S3.FarWait2", new Vector3(1.5f, 0f, 126f));
            ctx.MarkPoint("S3.TunnelIn", new Vector3(-6.4f, 0f, 103f));
            ctx.MarkPoint("S3.TunnelA", new Vector3(-14f, 0f, 103f));
            ctx.MarkPoint("S3.Jump1Takeoff", new Vector3(-14f, 0f, 110.35f));
            ctx.MarkPoint("S3.Jump1Landing", new Vector3(-14f, 0.5f, 113.4f));
            ctx.MarkPoint("S3.Jump2Takeoff", new Vector3(-14f, 0.5f, 115.35f));
            ctx.MarkPoint("S3.Jump2Landing", new Vector3(-14f, 0f, 118.4f));
            ctx.MarkPoint("S3.StepUp", new Vector3(-16.2f, 0.8f, 117.75f));
            ctx.MarkPoint("S3.CatwalkIn", new Vector3(-16.2f, 1.6f, 119.4f));
            ctx.MarkPoint("S3.CatwalkMid", new Vector3(-16.2f, 1.6f, 125f));
            ctx.MarkPoint("S3.CatwalkOut", new Vector3(-16.2f, 1.6f, 131.0f));
            ctx.MarkPoint("S3.StepDown", new Vector3(-16.2f, 0.8f, 132.2f));
            ctx.MarkPoint("S3.RoomExit", new Vector3(-14.6f, 0f, 131.4f));
            ctx.MarkPoint("S3.TunnelOut", new Vector3(-9.5f, 0f, 130.8f));
            ctx.MarkPoint("S3.Rejoin", new Vector3(-4f, 0f, 127f));
            ctx.MarkPoint("S3.CellExit", new Vector3(-13.2f, 0f, 132.6f));
            ctx.MarkPoint("S3.GateFront", new Vector3(0f, 0f, 133.2f));

            ctx.Hint(new Vector3(0f, 2f, 104f), new Vector3(16f, 5f, 12f),
                "One intern sits in the chair (E) to power the bridge. The other two cross and pull RELEASE. The freed volunteer takes the MAINTENANCE TUNNEL unless they have TURBO LEGS.",
                "Someone has to take one for the team. Temporarily. Probably.");
            ctx.Hint(new Vector3(-14f, 2f, 118f), new Vector3(6f, 5f, 36f),
                "Jump the two acid trenches, then climb the steps onto the pipe catwalk: the bot can't reach 1.6 m. Drop down past the far end.",
                "Up is safe. Down is detention.");
            ctx.Tutorial(new Vector3(0f, 1.5f, 102.5f), new Vector3(12f, 3f, 6f), "l3_sacrifice", "SACRIFICE",
                "Sometimes the only way forward is to sacrifice someone. Sit in the chair (E) to power the bridge.", new[] { "E" });
            ctx.Tutorial(new Vector3(-12.5f, 1.5f, 103f), new Vector3(8f, 3f, 5f), "l3_recovery", "RECOVERY",
                "Most sacrifices are recoverable. Most. Follow the tunnel back to your team.", null);
            PerspectiveKit.Clue(ctx.Decor, new Vector3(-7.78f, 2.4f, 107f), Yaw(-90f), "TUNNEL OPENS WHEN THE VOLUNTEER IS RELEASED", 0.28f);
            PerspectiveKit.Clue(ctx.Decor, new Vector3(-16.78f, 3.0f, 124f), Yaw(-90f), "BOT REACH 1.3 M. PIPES 1.6 M.", 0.3f);

            // dressing
            Sign(ctx.Decor, new Vector3(-4.5f, 3.2f, 97.25f), 0f, "YOUR SACRIFICE IS APPRECIATED (NOT COMPENSATED)", 5f, 0.5f, SignStyle.Info);
            Sign(ctx.Decor, new Vector3(-7.50f, 4.6f, 103f), 90f, "MAINTENANCE TUNNEL", 3.0f, 0.5f, SignStyle.Warning);
            Sign(ctx.Decor, new Vector3(3f, 2.4f, 134.75f), 180f, "HEADCOUNT GATE: ALL STAFF MUST BE PRESENT. YES, EVEN KEVIN.", 5.0f, 0.55f, SignStyle.Warning);
            Sign(ctx.Decor, new Vector3(3f, 2.2f, 119.6f), 180f, "RELEASE VOLUNTEER (BRIDGE WILL RETRACT. SORRY.)", 4.6f, 0.5f, SignStyle.Danger);
            Sign(ctx.Decor, new Vector3(-14f, 3.4f, 106.3f), 180f, "AUTHORIZED SURVIVORS ONLY", 3.6f, 0.5f, SignStyle.Info);
            Sign(ctx.Decor, new Vector3(-14f, 3.4f, 137.35f), 180f, "STAY ON THE PIPES. THE ROBOT IS NOT YOUR FRIEND.", 5f, 0.5f, SignStyle.Warning);
            Poster(ctx.Decor, new Vector3(7.75f, 2.2f, 100f), -90f, "HR NOTICE", "SACRIFICES MUST BE PRE-APPROVED. FORM 27-B.");
            Poster(ctx.Decor, new Vector3(7.75f, 2.2f, 129f), -90f, "YOUR CLONE IS VALUED", "YOUR ORIGINAL WAS ALSO VALUED");
            Sign(ctx.Decor, new Vector3(4.5f, 3.9f, 97.25f), 0f, "PLEASE DO NOT SHIFT THE BLAME (OR THE INTERNS)", 4.6f, 0.45f, SignStyle.Warning);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(-6.6f, 0f, 98.6f), 2.2f, 0.5f);
            EnvKit.Decor.Tank(ctx.Decor, new Vector3(6.8f, 0f, 104f), 2.2f, 0.5f);
            L3Kit.BrokenTank(ctx.Decor, new Vector3(6.4f, 0f, 133.4f), 0.55f, 31);
            EnvKit.Decor.Plant(ctx.Decor, new Vector3(-7.2f, 0f, 134.2f), 2);
            EnvKit.Decor.Barrel(ctx.Decor, new Vector3(-16.2f, 0f, 101f), 0);
            EnvKit.Decor.Barrel(ctx.Decor, new Vector3(-15.6f, 0f, 101.9f), 1);
            EnvKit.Decor.Debris(ctx.Decor, new Vector3(-12f, 0f, 135f), 1.0f, 8, 17);
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(-12.5f, 0f, 121f), new Vector2(2.5f, 3f), 18);
            EnvKit.Decor.BrokenTiles(ctx.Decor, new Vector3(3f, 0f, 129f), new Vector2(4f, 4f), 19);
            EnvKit.Pipe(ctx.Decor, new[] { new Vector3(7.7f, 3.9f, 97.5f), new Vector3(7.7f, 3.9f, 134.5f) }, 0.12f);
            EnvKit.CableTray(ctx.Decor, new Vector3(-7.6f, 4.3f, 97.5f), new Vector3(-7.6f, 4.3f, 108.5f), 0.4f);
            Panel(ctx.Lights, new Vector3(7.75f, 3.9f, 104f), -90f, LabGreen, true, true, 2.4f, 16f);
            Panel(ctx.Lights, new Vector3(-7.75f, 3.9f, 124f), 90f, LabWhite, true);
            Panel(ctx.Lights, new Vector3(-16.75f, 3.4f, 112f), 90f, LabGreen, true);
            Panel(ctx.Lights, new Vector3(-11.25f, 3.4f, 124f), -90f, AlarmRed, true);
            ctx.Checkpoint(new Vector3(4f, 0f, 140f), 0f);
        }
    }
}
