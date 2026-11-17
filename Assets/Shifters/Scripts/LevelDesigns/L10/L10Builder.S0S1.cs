// =====================================================================================
//  SHIFTERS - L10Builder.S0S1.cs   [LEVEL 10 - S0 Parking Garage, S1 Cubicle Farm + three breakers]
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public partial class L10Builder
    {
        // ================================================================== S0 PARKING GARAGE  x -10..10, z -10..8
        void BuildS0(LevelBuildContext ctx)
        {
            var g = ctx.Section("S0_ParkingGarage", Vector3.zero);
            var d = ctx.Section("S0_Decor", Vector3.zero);
            Floor(g, -10f, 10f, -10f, 8f, 0f, MatKey.Concrete);
            WallX(g, -10f, -10f, 10f);
            WallZ(g, -10f, -10f, 8f);
            WallZ(g, 10f, -10f, 8f);
            // painted parking bays + driving lane
            for (int i = 0; i < 4; i++)
            {
                float z = -8f + i * 4f;
                Kit.BoxVisual(d, "BayLineW", V(-6.5f, 0.012f, z), V(6f, 0.02f, 0.12f), MatKey.SignWhite, 0f, null, false);
                Kit.BoxVisual(d, "BayLineE", V(7f, 0.012f, z), V(6f, 0.02f, 0.12f), MatKey.SignWhite, 0f, null, false);
            }
            FloorLabel(d, V(0f, 0f, -3f), "STAFF ONLY\n(STAFF: NOBODY)", 0.45f, Warm);
            // ceiling beams + four lamp rows (they die row by row on ch09.garage)
            var rows = new GameObject[4];
            var kills = new PuzzleSignal[3];
            float[] rowZ = { -6f, -2f, 2f, 6f };
            for (int r = 0; r < 4; r++)
            {
                Kit.BoxVisual(d, "Beam", V(0f, 4.55f, rowZ[r]), V(20f, 0.3f, 0.35f), MatKey.Concrete, 0.02f, null, false);
                var row = Kit.Make("GarageRow_" + r, d, Vector3.zero);
                for (int k = -1; k <= 1; k++) HangingLamp(row.transform, V(k * 6f, 4.05f, rowZ[r]), Warm, 2.2f);
                rows[r] = row;
                if (r < 3)
                {
                    kills[r] = PuzzleKit.Signal(ctx.Puzzles, "GarageKill_" + r);
                    var pool = PuzzleKit.LightPool(ctx.Puzzles, V(0f, 0f, rowZ[r]), 3f, false, kills[r]);
                    pool.Invert = true;       // lit until the director kills the row
                }
            }
            // the LOBBY POOL stays on (real light #1)
            PuzzleKit.LightPool(ctx.Puzzles, V(0f, 0f, 6f), 3f, true);
            ctx.Checkpoint(V(0f, 0f, 5.4f), 0f);
            FloorLabel(d, V(0f, 0f, 3.75f), "IF YOU CAN READ THIS, YOU'RE STANDING\nIN THE LIGHT. STAY THERE.", 0.3f, Warm, 0f, 5.6f);
            // two abandoned cars + a van
            Car(g, d, V(-6.6f, 0f, -0.2f), 0f, MatKey.MetalLight);
            Car(g, d, V(7f, 0f, 3.8f), 180f, MatKey.PuzzleAccent);
            Car(g, d, V(-6.6f, 0f, -7.8f), 0f, MatKey.MetalRust);
            EnvKit.Pillar(g, V(-3.8f, 0f, 0f), WallH, 0.7f);
            EnvKit.Pillar(g, V(3.8f, 0f, 0f), WallH, 0.7f);

            ctx.SetSpawn(SpawnPos, 0f);
            ctx.Tutorial(V(0f, 1.5f, -7f), V(9f, 3f, 5f), "ch09_dark", "LIGHTS OUT",
                "The intern you control carries the flashlight. Teammates keep glowsticks. Alone in the dark = SCARED.", null, true);

            // TOOL LOCKER: PARTY POPPER (east side, faces the driving lane)
            var lockerPos = V(6f, 0f, -4f);
            var locker = WeaponKit.ToolLocker(ctx.Props, lockerPos, EnvKit.FacingViewerAt(lockerPos, V(3f, 0f, -4f)), ExperimentalVariant.PartyPopper);
            ctx.Mark("s0.locker", locker.gameObject);
            ctx.MarkPoint("s0.lockerStand", V(4.6f, 0f, -4f), -90f);
            Kit.SolidBox(g, "LockerBackWall", V(6.75f, 1.5f, -4f), V(0.3f, 3f, 2.6f), MatKey.WallAlt, 0.03f);
            Sign(d, V(6.58f, 3.3f, -4f), Vector3.left, "FOR CELEBRATIONS AND EMERGENCIES.\nMOSTLY EMERGENCIES.", 2.4f, 0.7f, SignStyle.Warning);
            ctx.Tutorial(V(4.5f, 1.5f, -4f), V(4f, 3f, 4f), "ch09_flare", "PARTY POPPER",
                "PARTY POPPER: a confetti flare makes a pool of light for 8 seconds. It's also loud.", new[] { "LMB" }, false);

            // signage + story dressing
            Sign(d, V(-5.6f, 3.0f, 7.78f), Vector3.back, "NIGHT SHIFT:\nYOU ARE NOT ALONE\n(MAX IS, THOUGH)", 3.2f, 1.3f, SignStyle.Danger);
            Sign(d, V(4.4f, 2.9f, 7.78f), Vector3.back, "THIS WAY TO SAFETY\n(PROBABLY)", 2.6f, 0.9f, SignStyle.Arrow);
            StoryKit.MeTvScreen(d, V(-9.75f, 3.0f, 1.5f), EnvKit.WallMountFacing(Vector3.right), 3.6f);
            ctx.MarkPoint("story.metv", V(-9.5f, 0f, 1.5f));
            Sign(d, V(0f, 3.6f, -9.78f), Vector3.forward, "SHIFTERS INC. HQ - VISITOR PARKING\n\"CLOSED SINCE THE INCIDENT\"", 4.6f, 1f, SignStyle.Info);
            EnvKit.Decor.Night.Cobweb(d, V(-9.8f, 4.5f, -9.8f), EnvKit.FacingViewer(V(1f, 0f, 1f)), 1.4f);
            EnvKit.Decor.Night.Cobweb(d, V(9.8f, 4.5f, -9.8f), EnvKit.FacingViewer(V(-1f, 0f, 1f)), 1.2f, true);
            EnvKit.Decor.Night.WaterCooler(d, V(-9.2f, 0f, 6.8f), EnvKit.FacingViewer(Vector3.right), true, true);

            StoryKit.ScriptTrigger(ctx.Gameplay, V(0f, 1.5f, -4f), V(20f, 3f, 1.4f), "ch09.garage");

            _dir.GarageRows = rows;
            _dir.GarageKills = kills;
        }

        void Car(Transform g, Transform d, Vector3 pos, float yaw, MatKey paint)
        {
            var root = Kit.Make("Car", g, pos, Yaw(yaw)).transform;
            Kit.SolidBox(root, "Body", V(0f, 0.65f, 0f), V(1.9f, 0.9f, 4.3f), paint, 0.12f);
            Kit.SolidBox(root, "Cabin", V(0f, 1.4f, -0.3f), V(1.7f, 0.65f, 2.2f), MatKey.Glass, 0.1f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Kit.BoxVisual(root, "Wheel", V(sx * 0.95f, 0.35f, sz * 1.35f), V(0.3f, 0.7f, 0.7f), MatKey.Rubber, 0.1f, null, false);
            Kit.BoxVisual(root, "Dust", V(0f, 1.74f, -0.3f), V(1.6f, 0.02f, 2.1f), MatKey.Concrete, 0f, null, false);
        }

        // ================================================================== S1 CUBICLE FARM  x -18..18, z 8..50
        void BuildS1(LevelBuildContext ctx)
        {
            var g = ctx.Section("S1_CubicleFarm", Vector3.zero);
            var d = ctx.Section("S1_Decor", Vector3.zero);
            Floor(g, -18f, 18f, 8f, 50f, 0f, MatKey.Floor);
            // outer walls (the z 8 wall is shared with the garage; doors are >= 4 m)
            WallX(g, 8f, -18f, 18f, -2f, 2f);
            WallZ(g, -18f, 8f, 50f);
            WallZ(g, 18f, 8f, 50f);
            WallX(g, 50f, -18f, 18f, -2.5f, 2.5f);
            // hall / closet block (x 10..18)
            WallZ(g, 10f, 8f, 50f, 11.5f, 15.5f, 44.5f, 48.5f);
            WallX(g, 44f, 10f, 18f, 11.5f, 16.5f);
            // mailroom (x -18..-10, z 22..38)
            WallZ(g, -10f, 22f, 38f, 27.5f, 31.5f);
            WallX(g, 22f, -18f, -10f);
            WallX(g, 38f, -18f, -10f);

            // ---- light pools (emissive desk lamps, r 3)
            var poolB = V(-4f, 0f, 22f);
            PuzzleKit.LightPool(ctx.Puzzles, V(-12f, 0f, 15f), 3f, false);
            PuzzleKit.LightPool(ctx.Puzzles, poolB, 3f, false);
            PuzzleKit.LightPool(ctx.Puzzles, V(6f, 0f, 22f), 3f, false);
            PuzzleKit.LightPool(ctx.Puzzles, V(-12f, 0f, 44f), 3f, false);
            ctx.MarkPoint("s1.pool.b", poolB);
            FloorLabel(d, poolB + V(0f, 0f, 1.9f), "SAFE-ISH ZONE", 0.32f, Warm);

            // ---- cubicles (aisles: N-S x -8 and x 0, E-W z 20 / 30 / 40, each >= 3 m)
            var vend = V(-2.2f, 0f, 35f);
            var cellPos = V(-6f, 0f, 46f);
            CubicleBlock(d, -6.6f, -1.6f, 9.4f, 18.4f, -90f, 1);
            CubicleBlock(d, -18f, -9.6f, 9.4f, 18.4f, 90f, 2, new[] { V(-12f, 0f, 15f) }, 3.2f);
            CubicleBlock(d, -6.6f, -1.6f, 21.6f, 28.4f, -90f, 3, new[] { poolB }, 3.2f);
            CubicleBlock(d, -6.6f, -1.6f, 31.6f, 38.4f, -90f, 4, new[] { vend }, 2f);
            CubicleBlock(d, 1.6f, 10f, 21.6f, 28.4f, 90f, 5, new[] { V(6f, 0f, 22f) }, 3.2f);
            CubicleBlock(d, 1.6f, 10f, 31.6f, 38.4f, 90f, 6);
            CubicleBlock(d, -18f, -9.6f, 41.6f, 49.6f, 90f, 7, new[] { V(-12f, 0f, 44f), V(-10.3f, 0f, 44f) }, 3.2f);
            // reception (south-east): open, a few desks so the hall door is easy to reach
            EnvKit.Decor.DeskWithMonitors(d, V(6f, 0f, 17.4f), Yaw(180f));
            EnvKit.Decor.DeskWithMonitors(d, V(3f, 0f, 17.4f), Yaw(180f));
            EnvKit.Decor.Night.WaterCooler(d, V(9.4f, 0f, 9.2f), EnvKit.FacingViewer(Vector3.left), true, true);
            EnvKit.Decor.Night.WaterCooler(d, V(-1.0f, 0f, 49.3f), EnvKit.FacingViewer(Vector3.back), true, true);
            EnvKit.Decor.Night.CopierStack(d, V(7.5f, 0f, 49f), EnvKit.FacingViewer(Vector3.back), 1, true);

            // ---- the two S1 dummies (dormant outside their arenas)
            var dw = EnemyKit.Stalker(ctx.Enemies, V(-12f, 0f, 40f), Yaw(180f), CellId, DWArena, DWRadius, StalkerSkin.TeamBuildingDummy);
            var de = EnemyKit.Stalker(ctx.Enemies, V(8.5f, 0f, 30f), Yaw(-90f), CellId, DEArena, DERadius, StalkerSkin.TeamBuildingDummy);
            ctx.Mark("s1.dummyW", dw.gameObject);
            ctx.Mark("s1.dummyE", de.gameObject);
            PerspectiveKit.ThreatLabel(dw.transform, "THREAT: {m} M");
            PerspectiveKit.ThreatLabel(de.transform, "THREAT: {m} M");
            _dir.S1Dummies = new[] { dw, de };
            ctx.Tutorial(V(0f, 1.5f, 11f), V(4f, 3f, 5f), "ch09_dummies", "TEAM-BUILDING DUMMIES",
                "They move when you don't look. Light stops them. Don't leave friends alone in the dark.", null, true);
            ctx.Hint(V(-2f, 2f, 26f), V(22f, 4f, 22f),
                "Work in pairs: nobody within 6 m of a friend is scared, and dummies only chase scared interns. Park the third in a lamp pool.",
                "Buddy system.");

            // ---- THE TRUST EXERCISE ROOM (holding cell) + release lever in a real-light pool (#2)
            var cell = EnemyKit.HoldingCell(ctx.Enemies, cellPos, Yaw(180f), CellId);
            var release = PuzzleKit.Lever(ctx.Puzzles, V(-2.5f, 0f, 47f), Yaw(90f), 0, false, false, "");
            PuzzleKit.Wire(cell, release);
            PuzzleKit.LightPool(ctx.Puzzles, V(-2.4f, 0f, 46.6f), 3f, true);
            ctx.Mark("s1.cellLever", release.gameObject);
            ctx.MarkPoint("s1.cellLeverStand", V(-1.6f, 0f, 47f), -90f);
            Sign(d, V(-6f, 3.75f, 43.9f), Vector3.back, "THE TRUST EXERCISE ROOM", 3.4f, 0.6f, SignStyle.Warning);
            Sign(d, V(-5.2f, 2.4f, 49.78f), Vector3.back, "LOST & FOUND:\n3 INTERNS (UNCLAIMED)", 2.2f, 0.9f, SignStyle.Info);
            FloorLabel(d, V(-1.6f, 0f, 45.1f), "RELEASE\n(PULL TO TRUST)", 0.28f, Teal);

            // ---- the vending machine scare (ch09.vending): a dummy at the end of the z 30 aisle, then gone
            VendingMachine(g, d, vend);
            ScareKit.FlickerReveal(d, new[] { V(-9.2f, 0f, 30f), V(-7.2f, 0f, 30f) }, 0.6f, "ch09.vending", null, null, ScareKind.Dummy);
            StoryKit.ScriptTrigger(ctx.Gameplay, V(0f, 1.5f, 33.5f), V(3f, 3f, 2f), "ch09.vending");

            // ---- optional helpers: 3 FUSE BOXES (shoot = a pool at the base for 15 s)
            FuseBox(ctx, g, d, V(-3f, 3f, 21.6f), 180f, V(-3f, 0f, 18.9f));
            FuseBox(ctx, g, d, V(3f, 3f, 41.6f), 180f, V(3f, 0f, 38.9f));
            FuseBox(ctx, g, d, V(-10.6f, 3f, 44f), 90f, V(-8f, 0f, 44f));

            BuildS1a(ctx, g, d);
            BuildS1b(ctx, g, d);
            BuildS1c(ctx, g, d);

            // ---- story dressing
            Sign(d, V(-17.78f, 2.6f, 32f), Vector3.right, "TEAM SPIRIT 1999", 5.5f, 1.2f, SignStyle.Info);
            Poster(d, V(-17.78f, 2.2f, 15f), Vector3.right, "TEAM PHOTO 1999", "(every face scribbled out except one)");
            Sign(d, V(-9f, 3.1f, 49.78f), Vector3.back, "<s>TOGETHER</s> ALONE\nWE ACHIEVE MORE", 3.4f, 1.1f, SignStyle.Danger);
            // polish 11-17: faced north-east, the team walks in from the south and saw the translucent slogan mirrored
            StoryKit.HoloPoster(d, V(-1.9f, 0f, 9.2f), EnvKit.FacingViewer(V(1f, 0f, -1f)), StoryKitLines.Poster(4), 3);
            StoryKit.HoloPoster(d, V(9.3f, 0f, 41.6f), EnvKit.FacingViewer(V(-1f, 0f, -1f)), StoryKitLines.Poster(7), 3);
            EnvKit.Decor.Night.MoonWindow(d, V(17.78f, 1.0f, 30f), EnvKit.WallMountFacing(Vector3.left), 1.6f, 2.8f, true, false);
            EnvKit.Decor.Night.MoonWindow(d, V(-17.78f, 1.0f, 22f), EnvKit.WallMountFacing(Vector3.right), 1.6f, 2.8f, false, false);
            EnvKit.Decor.Night.Cobweb(d, V(-17.8f, 4.5f, 49.8f), EnvKit.FacingViewer(V(1f, 0f, -1f)), 1.5f);
            EnvKit.Decor.Night.Cobweb(d, V(9.8f, 4.5f, 8.2f), EnvKit.FacingViewer(V(-1f, 0f, 1f)), 1.2f, true);

            // ---- ELEVATOR-LOBBY door = AND(BREAKER 1, 2, 3) + status lamps + board
            var lobbyDoor = PuzzleKit.Door(ctx.Puzzles, V(0f, 0f, 50f), Quaternion.identity, DoorMode.SlideUp, 4f, 3.4f, _breakers);
            ctx.Mark("s1.lobbyDoor", lobbyDoor.gameObject);
            for (int i = 0; i < 3; i++)
                PuzzleKit.IndicatorLight(ctx.Puzzles, V(-3.6f + i * 0.9f, 3.2f, 49.76f), Yaw(180f), true, _breakers[i]);
            _dir.PowerBoard = ctx.Label(d, V(4.4f, 3.3f, 49.76f), Quaternion.identity, "POWER: 0/3 BREAKERS", 0.36f, Warm, 3.4f);
            _dir.Breakers = _breakers;
        }

        SignalSource[] _breakers = new SignalSource[3];

        // ---- S1a MAILROOM (STR breaker): battery 160 kg pushed 8 m onto a latching 150 kg plate
        void BuildS1a(LevelBuildContext ctx, Transform g, Transform d)
        {
            var battery = PuzzleKit.HeavyBlock(ctx.Props, V(-14f, 0f, 25f));
            ctx.Mark("s1a.battery", battery.gameObject);
            var plate = PuzzleKit.PressurePlate(ctx.Puzzles, V(-14f, 0f, 33f), Quaternion.identity, 150f, true, "BREAKER 1/3: MAILROOM");
            ctx.Mark("s1.breaker1", plate.gameObject);
            ctx.MarkPoint("s1a.plate.center", V(-14f, 0f, 33f));
            ctx.MarkPoint("s1a.pushStart", V(-14f, 0f, 23.2f));
            _breakers[0] = plate;
            PuzzleKit.LightPool(ctx.Puzzles, V(-14f, 0f, 31f), 3f, false, plate);
            ctx.Label(battery.transform, V(0f, 1.95f, 0f), EnvKit.FacingViewer(Vector3.right), "SPARE BATTERY\n160 KG", 0.3f, Teal, 2f);
            ctx.Label(battery.transform, V(0f, 1.95f, 0f), EnvKit.FacingViewer(Vector3.back), "SPARE BATTERY\n160 KG", 0.3f, Teal, 2f);
            FloorLabel(d, V(-14f, 0f, 28.6f), "PUSH ^\n(STR)", 0.4f, Teal);
            Sign(d, V(-9.78f, 3.6f, 29.5f), Vector3.right, "MAILROOM\n(BREAKER 1)", 2.6f, 0.9f, SignStyle.Info);
            Sign(d, V(-14f, 2.8f, 37.78f), Vector3.back, "RETURN TO SENDER:\nTEAMWORK", 2.8f, 0.9f, SignStyle.Info);
            // shelving along the west wall (out of the push lane)
            for (int i = 0; i < 4; i++)
                Kit.SolidBox(g, "MailShelf", V(-17.2f, 1.1f, 24.5f + i * 3.4f), V(1.1f, 2.2f, 2.6f), MatKey.Wood, 0.03f);
            for (int i = 0; i < 6; i++)
                Kit.BoxVisual(d, "Parcel", V(-17.2f, 2.35f, 23.6f + i * 2.2f), V(0.7f, 0.3f, 0.6f), MatKey.Cardboard, 0.03f, null, false);
            ctx.Hint(V(-14f, 2f, 30f), V(8f, 4f, 16f), "The Brute pushes the SPARE BATTERY onto the plate. Bring a buddy so nobody gets scared.", "Heavy thing, heavy plate.");
        }

        // ---- S1b MOTION-SENSOR HALL (SPD breaker): 28 m turbo run through a 3-beam laser curtain
        void BuildS1b(LevelBuildContext ctx, Transform g, Transform d)
        {
            var stand = V(14f, 0f, 12.7f);
            var route = new[] { stand, V(14f, 0f, 26f), V(14f, 0f, 40.7f) };
            var timed = PuzzleKit.TimedButton(ctx.Puzzles, V(14f, 0f, 12f), Quaternion.identity, HallRoute, route, "");
            ctx.Mark("s1b.timed", timed.gameObject);
            ctx.MarkPoint("s1b.stand", stand, 0f);
            var lever = PuzzleKit.Lever(ctx.Puzzles, V(14f, 0f, 42f), Yaw(180f), 0, true, false, "BREAKER 2/3: MOTION HALL");
            ctx.Mark("s1b.lever", lever.gameObject);
            ctx.MarkPoint("s1b.leverStand", V(14f, 0f, 41.2f), 0f);
            _breakers[1] = lever;
            var laser = PuzzleKit.LaserBarrier(ctx.Hazards, V(14f, 0f, 40f), Quaternion.identity, 8f, 3, 0f, 0f, 0f, timed, lever);
            laser.Logic = SignalLogic.Any;
            laser.Invert = true;      // ON unless (window running OR breaker pulled)
            ctx.Mark("s1b.laser", laser.gameObject);
            PuzzleKit.LightPool(ctx.Puzzles, V(14f, 0f, 41.6f), 3f, false, lever);
            var inner = PuzzleKit.Door(ctx.Puzzles, V(14f, 0f, 44f), Quaternion.identity, DoorMode.SlideUp, 4f, 3.4f, lever);
            ctx.Mark("s1b.closetDoor", inner.gameObject);

            // "the lights only notice fast people": the hall counts as lit while the window runs
            var vol = Kit.Make("HallLitVolume", ctx.Gameplay, V(14f, 2f, 26f));
            var glow = Kit.Make("HallGlow", d, Vector3.zero);
            for (int i = 0; i < 6; i++) HangingLamp(glow.transform, V(14f, 4.1f, 13f + i * 5.5f), Teal, 3f);
            glow.SetActive(false);
            _dir.HallTimer = timed;
            _dir.HallVolume = vol.transform;
            _dir.HallVolumeSize = V(8f, 4f, 34f);
            _dir.HallGlow = glow;

            Sign(d, V(9.78f, 3.5f, 13.5f), Vector3.left, "MOTION-SENSOR HALL\nTHE LIGHTS ONLY NOTICE FAST PEOPLE", 3.6f, 1.0f, SignStyle.Warning);
            FloorLabel(d, V(14f, 0f, 15.2f), "TURBO RUN 28 M\nLIGHTS OUT IN 1.2 S", 0.45f, Teal);
            Sign(d, V(17.78f, 2.6f, 36f), Vector3.left, "SMILE! YOU'RE BEING DETECTED", 2.8f, 0.7f, SignStyle.Info);
            for (int i = 0; i < 3; i++)
                Kit.BoxVisual(d, "Sensor", V(17.7f, 3.2f, 18f + i * 8f), V(0.2f, 0.3f, 0.3f), MatKey.LightRed, 0.03f, null, false);
            ctx.Hint(V(14f, 2f, 22f), V(8f, 4f, 20f), "Only TURBO LEGS beats the laser grid. The hall is lit while the timer runs.", "Gotta go fast.");
        }

        // ---- S1c IT CLOSET (IQ breaker): UV arrows (IQ only) to the FACILITIES TERMINAL
        void BuildS1c(LevelBuildContext ctx, Transform g, Transform d)
        {
            var term = PuzzleKit.Terminal(ctx.Puzzles, V(15f, 0f, 47f), Yaw(-90f), 7, true, 6f, "BREAKER 3/3: IT CLOSET");
            ctx.Mark("s1c.terminal", term.gameObject);
            ctx.MarkPoint("s1c.termStand", V(13.9f, 0f, 47f), 90f);
            _breakers[2] = term;
            PuzzleKit.LightPool(ctx.Puzzles, V(13.6f, 0f, 47f), 3f, false, term);
            // UV ink: east along the z 40 aisle, then north to the closet door
            var east = Quaternion.Euler(90f, 0f, 0f);
            var north = Quaternion.Euler(90f, -90f, 0f);
            for (int i = 0; i < 4; i++) PerspectiveKit.Clue(d, V(1.6f + i * 1.9f, 0.03f, 40f), east, ">>", 0.6f);
            PerspectiveKit.Clue(d, V(8.6f, 0.03f, 42.4f), north, ">>", 0.6f);
            PerspectiveKit.Clue(d, V(8.6f, 0.03f, 44.6f), east, "IT >>", 0.6f);
            ctx.MarkPoint("s1c.uv.1", V(0f, 0f, 40f));
            ctx.MarkPoint("s1c.uv.2", V(8.6f, 0f, 40f));
            ctx.MarkPoint("s1c.door", V(11.2f, 0f, 46.5f));
            Sign(d, V(9.78f, 3.6f, 46.5f), Vector3.left, "IT DEPARTMENT\n(ALSO ME)", 2.6f, 0.9f, SignStyle.Info);
            Sign(d, V(17.78f, 2.4f, 47f), Vector3.left, "HAVE YOU TRIED\nTURNING HIM OFF AND ON AGAIN?", 2.8f, 0.9f, SignStyle.Info);
            Kit.SolidBox(g, "ServerRack", V(17.3f, 1.1f, 45.2f), V(0.9f, 2.2f, 1.6f), MatKey.MetalDark, 0.03f);
            Kit.BoxVisual(d, "RackLeds", V(16.83f, 1.4f, 45.2f), V(0.02f, 1.2f, 1.2f), MatKey.Screen, 0f, null, false);
            ctx.Hint(V(5f, 2f, 43f), V(12f, 4f, 7f), "Follow the UV arrows (smart eyes only) to the IT closet.", "Read the floor. With your brain.");
        }

        void FuseBox(LevelBuildContext ctx, Transform g, Transform d, Vector3 targetPos, float yaw, Vector3 poolPos)
        {
            var back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;      // into the pillar
            var pillarBase = new Vector3(targetPos.x, 0f, targetPos.z) + back * 0.42f;
            EnvKit.Pillar(g, pillarBase, WallH, 0.7f);
            var t = PuzzleKit.ShootTarget(ctx.Puzzles, targetPos, Yaw(yaw), ShootTargetMode.Momentary, 15f, false, "");
            PuzzleKit.LightPool(ctx.Puzzles, poolPos, 3f, false, t);
            var face = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            ctx.Label(d, targetPos + Vector3.down * 0.75f + face * 0.02f, EnvKit.FacingViewer(face), "FUSE BOX\nSHOOT: LIGHT 15 S", 0.2f, Warm, 1.6f);
        }

        void VendingMachine(Transform g, Transform d, Vector3 pos)
        {
            var root = Kit.Make("VendingMachine", g, pos, Yaw(90f)).transform;     // front faces +X (the main aisle)
            Kit.SolidBox(root, "Body", V(0f, 1.0f, 0f), V(1.1f, 2.0f, 0.85f), MatKey.WarningRed, 0.04f);
            Kit.BoxVisual(root, "Window", V(-0.12f, 1.25f, 0.43f), V(0.7f, 1.2f, 0.02f), MatKey.Screen, 0f, null, false);
            Kit.BoxVisual(root, "Tray", V(0f, 0.3f, 0.43f), V(0.8f, 0.2f, 0.03f), MatKey.Black, 0f, null, false);
            _ctx.Label(root, V(0f, 2.15f, 0.44f), Quaternion.Euler(0f, 180f, 0f), "SNAX FOR ONE", 0.18f, Warm, 1.1f);
            var can = Kit.Make("LonelinessCan", root, V(0.15f, 0.35f, 0.62f));
            ProcMesh.AddRenderer(can, ProcMesh.Cached("l10_can", () => ProcMesh.Cylinder(0.06f, 0.2f, 12, true)), Kit.Mat(MatKey.LightBlue), false);
            can.SetActive(false);
            _dir.VendingCan = can;
        }
    }
}
