// =====================================================================================
//  SHIFTERS - L2Builder.cs   (LEVEL 2 - THE WEIGHT PROBLEM, Factory theme)
//  Code-defined level: everything in the scene is built here (runtime + editor scene builder).
//
//  Layout (progress along +Z, x in [-8, 8]):
//    y=0   S0 Loading Dock (spawn z=0) -> S1 Heavy Gate (160 kg block -> 150 kg plate holds the shutter open)
//          -> C1 -> S2 Freight Elevator "MAX 150 KG" (car z 37.9..41.9, rises 6 m)
//    y=6   S3 Wobbly Catwalk over the incinerator (z 48..62, turbo express lane on the left) -> C2
//          -> S4 Counterweight Lift: PEOPLE platform A + CARGO basket B (block shoved into B's loading pit)
//    y=12  S4 upper deck: freight-stairs lever, 2.5 m stack ledge with the crusher's OFF lever -> C3
//          -> S5 Scale of Justice (200-270 kg, latching) -> crusher corridor -> SHIPPING DOCK exit (z 125)
//
//  WEIGHT ARITHMETIC (Brute 100-120, Sprinter 55-65, Brainiac 65-75, heavy block 160, small crate 25):
//    S1 plate >= 150: block 160 OK | Brute+anyone 155-195 OK (but then they stay behind) | Spr+Brain 120-140 NO.
//    S2 car <= 150:   Brute alone 100-120 OK | Spr+Brain 120-140 OK | Brute+anyone 155-195 NO | all 220-260 NO.
//    S4 pulley (B must beat A by > 10 kg): block 160 vs Spr+Brain 120-140 -> diff 20-40 OK (they rise).
//                     Alternative: Brute in B (100-120) vs Sprinter alone (55-65) -> diff >= 35 OK.
//                     Brute on A with block in B: 160-120 = 40 OK too. Brute + anyone on A (>= 155) NO.
//    S5 scale 200-270: all three 220-260 OK for every team | Brute+Spr+2 crates 205-235 OK |
//                     Brute+Brain+2 crates 215-245 OK | Spr+Brain+2 crates 170-190 NO | all three + crate 245-285 maybe TOO HEAVY.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Level 2 "THE WEIGHT PROBLEM" builder (LevelCatalog index 1, scene L2_WeightProblem).</summary>
    public class L2Builder : ILevelBuilder
    {
        public int LevelIndex => 1;

        // ------------------------------------------------------------------ key metrics (shared with L2Solution)
        public const float UpperY = 6f;           // S3 / S4 lower deck
        public const float TopY = 12f;            // S4 upper deck / S5
        public const float CarZ = 39.9f;          // freight elevator car center
        public const float CarSize = 4f;
        public const float ElevatorMaxKg = 150f;
        public const float WobblyX = 2.5f;
        public static readonly float[] WobblyZ = { 50.35f, 55f, 59.65f };
        public const float PulleyX = 1.5f, PulleyZ = 79f, PulleySeparation = 6f, PulleyTravel = 6f, PulleyPlatform = 3f, CargoPitDepth = 1.6f;
        public const float StackLedgeTop = TopY + 2.5f;
        public const float ScaleMinKg = 200f, ScaleMaxKg = 270f;
        /// <summary>S1 plate sunk 2 cm below the floor so the pushed block slides onto it. Set false once the kit's
        /// Pushable can step up onto the +0.14 m pad (architect fix): then the plate sits on the floor like everywhere else.</summary>
        public static readonly bool RecessHeavyGatePlate = false;

        static readonly Color Warm = new Color(1f, 0.72f, 0.42f);
        static readonly Color Ember = new Color(1f, 0.45f, 0.15f);
        static readonly Color SignInk = new Color(1f, 0.93f, 0.8f);

        public void Build(LevelBuildContext ctx)
        {
            BuildS0(ctx);
            BuildS1(ctx);
            BuildS2(ctx);
            BuildS3(ctx);
            BuildS4(ctx);
            BuildS5(ctx);
            BuildBackdrop(ctx);
        }

        // ================================================================== helpers
        static Quaternion Yaw(float y) => Quaternion.Euler(0f, y, 0f);

        /// <summary>EnvKit mounted pieces (Sign, Poster, LightPanel...) take the direction the VIEWER looks (rot*forward
        /// points into the wall). This level authors them by the direction their readable / lit face points (towards the
        /// reader), so every such rotation goes through here. Face(Yaw(180)) = readable by someone walking +Z.</summary>
        static Quaternion Face(Quaternion faceRot) => faceRot * Quaternion.Euler(0f, 180f, 0f);

        /// <summary>Sign whose readable face points along 'yaw' (180 = towards a player walking +Z).</summary>
        static GameObject Sign(Transform p, Vector3 pos, float yaw, string text, float w, float h, SignStyle style = SignStyle.Info)
            => EnvKit.Sign(p, pos, Face(Yaw(yaw)), text, new Vector2(w, h), style);

        /// <summary>Free-standing sign (see Sign) on thin posts hidden behind the panel, down to the floor at floorY.</summary>
        static GameObject PostSign(Transform p, Vector3 pos, float yaw, string text, float w, float h, SignStyle style, float floorY, bool twoPosts = false)
        {
            var go = Sign(p, pos, yaw, text, w, h, style);
            var rot = Yaw(yaw);
            float bottom = pos.y - h * 0.5f + 0.02f, len = bottom - floorY;
            if (len > 0.05f)
            {
                var behind = rot * Vector3.back * 0.07f;
                var side = rot * Vector3.right * (w * 0.5f - 0.18f);
                int n = twoPosts ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    var x = twoPosts ? (i == 0 ? side : -side) : Vector3.zero;
                    Kit.BoxVisual(go.transform, "Post" + i, go.transform.InverseTransformPoint(new Vector3(pos.x, floorY + len * 0.5f, pos.z) + behind + x),
                        new Vector3(0.1f, len, 0.1f), MatKey.MetalDark, 0.01f, Quaternion.Inverse(go.transform.rotation) * rot);
                }
            }
            return go;
        }

        static void Rail(Transform p, float x0, float z0, float x1, float z1, float y)
            => EnvKit.Railing(p, new Vector3(x0, y, z0), new Vector3(x1, y, z1));

        static void Wall(Transform p, float x0, float z0, float x1, float z1, float y, float h, MatKey mat = MatKey.Wall)
            => EnvKit.Wall(p, new Vector3(x0, y, z0), new Vector3(x1, y, z1), h, 0.4f, mat);

        // ================================================================== S0 loading dock
        void BuildS0(LevelBuildContext ctx)
        {
            var g = ctx.Section("S0_LoadingDock", Vector3.zero);
            L2Parts.Rect(g, -8f, 8f, -6f, 10f, 0f, 0.6f, MatKey.Floor);
            Wall(g, -8.4f, -6.2f, 8.4f, -6.2f, 0f, 5f);
            Wall(g, -8.2f, -6f, -8.2f, 42f, 0f, 5f);
            Wall(g, 8.2f, -6f, 8.2f, 42f, 0f, 5f);
            ctx.SetSpawn(new Vector3(0f, 0f, 0f), 0f);

            ctx.Tutorial(new Vector3(0f, 1.5f, 1.5f), new Vector3(12f, 3f, 8f), "l2_weights", "WEIGHT MATTERS",
                "Plates show how much weight they need. Your strong character sees weights - switch to them and press R to scan.",
                new[] { "1", "2", "3", "R" }, false);
            ctx.Hint(new Vector3(0f, 2f, 2f), new Vector3(16f, 4f, 14f),
                "Everything here has a weight. Plates and lifts list exactly what they need. Do the math before you do the jumping.",
                "Heavy things are heavy. Light things are not. You're welcome.");

            var d = ctx.Decor;
            Sign(d, new Vector3(0f, 3.6f, -5.95f), 0f, "SHIFTERS INC. - SHIPPING & RECEIVING\n<size=60%>YOUR SHIFT HAS BEGUN. IT DOES NOT END.</size>", 7.5f, 1.4f, SignStyle.Info);
            EnvKit.Poster(d, new Vector3(-8.0f, 2.4f, 2f), Face(Yaw(90f)), "QUOTA: 0 / 1000", "SHIFT HARDER. PRODUCTIVITY IS EVERYONE'S PROBLEM.");
            EnvKit.Poster(d, new Vector3(8.0f, 2.4f, 4f), Face(Yaw(-90f)), "LIFT WITH YOUR LEGS", "OR YOUR COWORKERS.");
            Sign(d, new Vector3(-5.65f, 3.9f, -5.95f), 0f, "DAYS WITHOUT A SHIFT ACCIDENT: 0", 3.6f, 0.7f, SignStyle.Warning);
            EnvKit.LightPanel(ctx.Lights, new Vector3(0f, 4.6f, -5.9f), Face(Yaw(0f) * Quaternion.Euler(25f, 0f, 0f)), new Vector2(3f, 0.5f), Warm, true, 2.4f, 16f);

            // dock clutter: crates, barrels, a forklift-ish machine, pallets of nonsense
            EnvKit.Decor.CrateStack(d, new Vector3(-6.4f, 0f, -4.4f), Yaw(10f), 4, 2);
            EnvKit.Decor.CrateStack(d, new Vector3(6.3f, 0f, -4.2f), Yaw(-20f), 3, 5);
            EnvKit.Decor.Barrel(d, new Vector3(-6.8f, 0f, 1.5f), 1);
            EnvKit.Decor.Barrel(d, new Vector3(-6.9f, 0f, 2.6f), 2);
            EnvKit.Decor.Barrel(d, new Vector3(-6.1f, 0f, 2.1f), 1);
            EnvKit.Decor.Machine(d, new Vector3(6.2f, 0f, 1.5f), Yaw(-90f), new Vector3(2.2f, 2.4f, 1.6f));
            EnvKit.Decor.Gear(d, new Vector3(7.9f, 3.4f, 1.5f), Quaternion.Euler(0f, 0f, 90f), 0.9f, 30f);
            EnvKit.Decor.Cone(d, new Vector3(-2.8f, 0f, 6.5f));
            EnvKit.Decor.Cone(d, new Vector3(2.9f, 0f, 6.2f));
            EnvKit.Decor.SteamStack(d, new Vector3(-7.2f, 0f, -2.2f), 3.4f);
            PuzzleKit.SteamVent(ctx.Hazards, new Vector3(-6.2f, 0f, 7.2f), null, 4f, 2f, 3f, 0.5f);
            PostSign(d, new Vector3(-6.2f, 1.4f, 8.6f), 180f, "FREE STEAM. NO REFUNDS.", 2.4f, 0.5f, SignStyle.Warning, 0f);
            EnvKit.Pipe(d, new[] { new Vector3(-7.8f, 4.2f, -6f), new Vector3(-7.8f, 4.2f, 20f), new Vector3(-7.8f, 2.6f, 20f) }, 0.16f);
            EnvKit.CableTray(d, new Vector3(7.7f, 4.4f, -5.5f), new Vector3(7.7f, 4.4f, 26f));
        }

        // ================================================================== S1 heavy gate
        void BuildS1(LevelBuildContext ctx)
        {
            var g = ctx.Section("S1_HeavyGate", Vector3.zero);
            // floor with a 3x3 recess for the plate (pad flush with the floor) and the incinerator hole
            L2Parts.Rect(g, -8f, -5.5f, 10f, 27f, 0f);
            if (RecessHeavyGatePlate)
            {
                L2Parts.Rect(g, -5.5f, -2.5f, 10f, 21.5f, 0f);
                L2Parts.Rect(g, -5.5f, -2.5f, 24.5f, 27f, 0f);
            }
            else L2Parts.Rect(g, -5.5f, -2.5f, 10f, 27f, 0f);
            L2Parts.Rect(g, -2.5f, 4.5f, 10f, 27f, 0f);
            L2Parts.Rect(g, 4.5f, 8f, 10f, 14.5f, 0f);
            L2Parts.Rect(g, 4.5f, 8f, 18.5f, 27f, 0f);
            // stripes around the recess so it reads as "put something heavy here"
            EnvKit.HazardStripe(ctx.Decor, new Vector3(-4f, 0.006f, 21.3f), new Vector3(3.4f, 0.012f, 0.3f));
            EnvKit.HazardStripe(ctx.Decor, new Vector3(-4f, 0.006f, 24.7f), new Vector3(3.4f, 0.012f, 0.3f));

            // the gate wall + shutter
            Wall(g, -8f, 27f, -0.5f, 27f, 0f, 5f, MatKey.WallAlt);
            Wall(g, 4.5f, 27f, 8f, 27f, 0f, 5f, MatKey.WallAlt);
            var plate = PuzzleKit.PressurePlate(ctx.Puzzles, new Vector3(-4f, RecessHeavyGatePlate ? -L2Parts.PlateTop - 0.02f : 0f, 23f), null, Tuning.PlateHeavy, false, "HEAVY GATE OPEN");
            L2Parts.Enlarge(plate, 1.5f);
            ctx.Mark("S1.Plate", plate);
            var gate = PuzzleKit.Door(ctx.Puzzles, new Vector3(2f, 0f, 27f), null, DoorMode.SlideUp, 4f, 4.5f, plate);
            ctx.Mark("S1.Gate", gate);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(-2.4f, 0f, 23f), new Vector3(-0.2f, 0f, 26.4f), plate);
            PuzzleKit.IndicatorLight(ctx.Puzzles, new Vector3(-1.2f, 2.2f, 26.75f), Yaw(180f), true, plate);

            var block = PuzzleKit.HeavyBlock(ctx.Props, new Vector3(-4f, 0f, 13f));
            ctx.Mark("S1.Block", block);
            ctx.MarkPoint("S1.PlateGoal", new Vector3(-4f, 0f, 23f));
            ctx.MarkPoint("S1.BruteClear", new Vector3(-1.2f, 0f, 20.6f));
            ctx.MarkPoint("S1.GateIn", new Vector3(2f, 0f, 25.2f));
            ctx.MarkPoint("S1.GateOut", new Vector3(2f, 0f, 29.2f));
            ctx.MarkPoint("S1.LaneL", new Vector3(-1.2f, 0f, 12f));
            ctx.MarkPoint("S1.LaneL2", new Vector3(-1.2f, 0f, 20.2f));

            ctx.Hint(new Vector3(0f, 2f, 19f), new Vector3(16f, 4f, 16f),
                "The shutter stays open only while 150 kg sit on the plate. The steel block weighs 160 kg: have your Brute push it into the recess, then everyone walks through.",
                "Two of you could stand on the plate... and then live there forever. Find something heavier and less needy.");

            // ragebait production line: crates ride a conveyor into the incinerator and respawn forever
            var conv = PuzzleKit.Conveyor(ctx.Puzzles, new Vector3(2.25f, 0.45f, 16.5f), Yaw(90f), 4.5f, 1.4f, 1.3f);
            ctx.Mark("S1.Conveyor", conv);
            for (int i = 0; i < 2; i++) PuzzleKit.Crate(ctx.Props, new Vector3(0.8f + i * 2f, 0.45f, 16.5f));
            PuzzleKit.KillZone(ctx.Hazards, new Vector3(6.25f, -0.15f, 16.5f), null, new Vector3(3.4f, 2.6f, 3.8f), KillZoneType.Incinerator, null, "was shipped to the incinerator");
            L2Parts.Box(ctx.Geometry, "IncineratorPitN", 4.5f, 8f, -3f, -0.05f, 18.5f, 18.7f, MatKey.MetalDark);
            L2Parts.Box(ctx.Geometry, "IncineratorPitS", 4.5f, 8f, -3f, -0.05f, 14.3f, 14.5f, MatKey.MetalDark);
            L2Parts.Box(ctx.Geometry, "IncineratorPitB", 4.5f, 8f, -3.2f, -3f, 14.3f, 18.7f, MatKey.MetalDark);
            Rail(g, 4.6f, 14.45f, 8f, 14.45f, 0f);
            Rail(g, 4.6f, 18.55f, 8f, 18.55f, 0f);
            Sign(ctx.Decor, new Vector3(7.95f, 2.6f, 16.5f), -90f, "INCINERATOR\n<size=60%>NOT AN EXIT. WE CHECKED.</size>", 2.8f, 1.2f, SignStyle.Danger);
            Sign(ctx.Decor, new Vector3(2.25f, 1.25f, 15.65f), 180f, "PLEASE DO NOT RIDE THE CONVEYOR", 3.2f, 0.45f, SignStyle.Warning);
            Kit.BoxVisual(ctx.Decor, "ConveyorSignPost", new Vector3(2.25f, 0.5f, 15.68f), new Vector3(0.1f, 0.9f, 0.1f), MatKey.MetalDark);

            var d = ctx.Decor;
            Sign(d, new Vector3(2f, 5.45f, 26.69f), 180f, "HEAVY GATE - 150 KG TO OPEN", 4.4f, 0.55f, SignStyle.Info);   // on the gate gantry
            EnvKit.Poster(d, new Vector3(-8.0f, 2.4f, 13f), Face(Yaw(90f)), "EMPLOYEE OF THE MONTH", "THE 160 KG BLOCK. NEVER COMPLAINS.");
            EnvKit.Decor.CrateStack(d, new Vector3(-6.8f, 0f, 25.6f), Yaw(5f), 3, 9);
            EnvKit.Decor.Barrel(d, new Vector3(6.9f, 0f, 25.7f), 2);
            EnvKit.Decor.Chain(d, new Vector3(-4f, 5.5f, 18f), 2.6f);
            EnvKit.Decor.Chain(d, new Vector3(4f, 5.5f, 22f), 3.2f);
            PuzzleKit.SteamVent(ctx.Hazards, new Vector3(6.4f, 0f, 22.5f), null, 4f, 2.2f, 2.8f, 1.7f);
            EnvKit.LightPanel(ctx.Lights, new Vector3(-7.95f, 4.2f, 20f), Face(Yaw(90f) * Quaternion.Euler(20f, 0f, 0f)), new Vector2(2.6f, 0.45f), Warm, true, 2.2f, 15f);

            // C1 hall (between the gate and the elevator)
            L2Parts.Rect(g, -8f, 8f, 27f, 37.9f, 0f);
            ctx.Checkpoint(new Vector3(2f, 0f, 33f), 0f);
            ctx.MarkPoint("C1.A", new Vector3(0.4f, 0f, 33f));
            ctx.MarkPoint("C1.B", new Vector3(2f, 0f, 33.7f));
            ctx.MarkPoint("C1.C", new Vector3(3.6f, 0f, 33f));
            EnvKit.Decor.Bench(d, new Vector3(-6.5f, 0f, 31f), Yaw(90f), 2f);
            EnvKit.Poster(d, new Vector3(-8.0f, 2.4f, 34f), Face(Yaw(90f)), "SHIFT CHANGE IN: NEVER", "BREAKS ARE A PRIVILEGE. THIS IS THE ROOM.");
        }

        // ================================================================== S2 freight elevator
        void BuildS2(LevelBuildContext ctx)
        {
            var g = ctx.Section("S2_FreightElevator", Vector3.zero);
            float c0 = CarZ - CarSize * 0.5f;
            // floor beside the car, and the 6 m loading tower whose top is the S3 deck
            L2Parts.Rect(g, -8f, -2.35f, c0, 42f, 0f);
            L2Parts.Rect(g, 2.35f, 8f, c0, 42f, 0f);
            L2Parts.Box(ctx.Geometry, "LoadingTower", -8f, 8f, -1f, UpperY - 0.6f, 42f, 48f, MatKey.Concrete);
            L2Parts.Rect(g, -8f, 8f, 42f, 48f, UpperY, 0.6f, MatKey.FloorAlt);

            var fe = L2Parts.FreightElevator(ctx.Puzzles, new Vector3(0f, 0f, CarZ), UpperY, CarSize, ElevatorMaxKg, new Vector3(1.45f, 0f, 1.45f));
            ctx.Mark("S2.Panel", fe);
            ctx.Mark("S2.Elevator", fe.Car);
            // load display + the flashing overweight sign on the tower face (readable from the hall)
            var face = ctx.Decor;
            Sign(face, new Vector3(0f, 4.75f, 41.95f), 180f, "FREIGHT ELEVATOR\n<size=70%>MAX 150 KG</size>", 4.2f, 1.3f, SignStyle.Warning);
            fe.LoadText = L2Parts.Text(ctx, face, new Vector3(0f, 3.75f, 41.93f), 0f, "LOAD 0 / 150 KG", 0.34f, UIColors.TextMuted, 4f);
            fe.WarningText = L2Parts.Text(ctx, face, new Vector3(0f, 3.15f, 41.93f), 0f, "OVERWEIGHT: PLEASE REMOVE ONE (1) INTERN", 0.3f, UIColors.Danger, 7f);
            fe.WarningText.enabled = false;
            Kit.BoxVisual(face, "WarnBack", new Vector3(0f, 3.45f, 41.97f), new Vector3(7.4f, 1.3f, 0.04f), MatKey.Black, 0.01f, null, false);
            EnvKit.Decor.WarningBeacon(face, new Vector3(-3.4f, 5.5f, 41.8f), false);
            EnvKit.Decor.WarningBeacon(face, new Vector3(3.4f, 5.5f, 41.8f), false);
            EnvKit.HazardStripe(ctx.Decor, new Vector3(0f, 0.006f, c0 - 0.25f), new Vector3(4.6f, 0.012f, 0.4f));

            ctx.MarkPoint("S2.Front", new Vector3(0f, 0f, 36.4f));
            ctx.MarkPoint("S2.CarA", new Vector3(1.25f, 0f, CarZ + 0.35f));   // panel stand spot
            ctx.MarkPoint("S2.CarB", new Vector3(-1.0f, 0f, CarZ - 0.9f));
            ctx.MarkPoint("S2.CarC", new Vector3(-1.0f, 0f, CarZ + 0.9f));
            ctx.MarkPoint("S2.CarExit", new Vector3(-0.2f, UpperY, 41.3f));
            ctx.MarkPoint("S2.Wait", new Vector3(-3.6f, 0f, 35.6f));
            ctx.MarkPoint("S2.Top1", new Vector3(1.2f, UpperY, 45.2f));
            ctx.MarkPoint("S2.Top2", new Vector3(-1.2f, UpperY, 44.6f));
            ctx.MarkPoint("S2.Top3", new Vector3(0f, UpperY, 46.4f));

            ctx.Hint(new Vector3(0f, 2f, 38f), new Vector3(14f, 4f, 9f),
                "MAX 150 KG. The Brute (100-120) rides alone. The car comes back down by itself, then the Sprinter and the Brainiac (120-140 together) ride up.",
                "The elevator is on a diet. Maybe don't all get in at once.");
            var d = ctx.Decor;
            EnvKit.Poster(d, new Vector3(8.0f, 2.4f, 31f), Face(Yaw(-90f)), "PLEASE DO NOT SHIFT THE BLAME", "(OR THE INTERNS. THE INTERNS ARE LOAD-BEARING.)");
            EnvKit.Decor.Machine(d, new Vector3(-5.6f, 0f, 40f), Yaw(90f), new Vector3(2f, 2.6f, 2.2f));
            EnvKit.Poster(d, new Vector3(-8.0f, 2.4f, 37.2f), Face(Yaw(90f)), "THE WEIGHT PROBLEM", "IT'S NOT YOU. IT'S YOUR MASS.");
            EnvKit.Decor.CrateStack(d, new Vector3(5.6f, 0f, 40.4f), Yaw(-12f), 3, 4);
            EnvKit.LightPanel(ctx.Lights, new Vector3(-5f, 5.6f, 41.9f), Face(Yaw(180f) * Quaternion.Euler(25f, 0f, 0f)), new Vector2(2.4f, 0.45f), Warm, true, 2.2f, 14f);
            // tower top railings (front edge leaves the car opening, back edge leaves the catwalk + express lane)
            Rail(g, -8f, 42.1f, -2.35f, 42.1f, UpperY);
            Rail(g, 2.35f, 42.1f, 8f, 42.1f, UpperY);
            Rail(g, -7.9f, 42.1f, -7.9f, 47.9f, UpperY);
            Rail(g, 7.9f, 42.1f, 7.9f, 47.9f, UpperY);
        }

        // ================================================================== S3 wobbly catwalk
        void BuildS3(LevelBuildContext ctx)
        {
            var g = ctx.Section("S3_WobblyCatwalk", Vector3.zero);
            Rail(g, -7.9f, 47.9f, -6.6f, 47.9f, UpperY);
            Rail(g, -4.4f, 47.9f, 0.9f, 47.9f, UpperY);
            Rail(g, 4.1f, 47.9f, 7.9f, 47.9f, UpperY);

            // incinerator trench (deadly) between the tower and deck B
            PuzzleKit.KillZone(ctx.Hazards, new Vector3(0f, 2.5f, 55f), null, new Vector3(16f, 3f, 14f), KillZoneType.Incinerator, null, "was incinerated (catwalk safety training: failed)");
            L2Parts.Box(ctx.Geometry, "TrenchFloor", -8f, 8f, -1.2f, -0.6f, 48f, 62f, MatKey.MetalDark);
            L2Parts.Box(ctx.Geometry, "TrenchWallW", -8.6f, -8f, -1.2f, UpperY, 48f, 62f, MatKey.MetalRust);
            L2Parts.Box(ctx.Geometry, "TrenchWallE", 8f, 8.6f, -1.2f, UpperY, 48f, 62f, MatKey.MetalRust);
            L2Parts.Box(ctx.Geometry, "TrenchWallN", -8f, 8f, -1.2f, UpperY - 1f, 62f, 62.6f, MatKey.MetalRust);
            EnvKit.LightPanel(ctx.Lights, new Vector3(0f, 0.2f, 55f), Face(Quaternion.Euler(-90f, 0f, 0f)), new Vector2(10f, 6f), Ember, true, 3.2f, 14f);

            // the wobbly catwalk: three seesaw slabs tilting SIDEWAYS (axle along +Z) - walk the yellow line
            for (int i = 0; i < WobblyZ.Length; i++)
            {
                var w = PuzzleKit.WobblyPlatform(ctx.Puzzles, new Vector3(WobblyX, UpperY, WobblyZ[i]), Yaw(90f), new Vector3(4.5f, 0.35f, 3f));
                ctx.Mark("S3.Wobbly" + (i + 1), w);
                if (w.Slab != null)
                    Kit.BoxVisual(w.Slab.transform, "CenterLine", new Vector3(0f, 0.008f, 0f), new Vector3(4.3f, 0.014f, 0.16f), MatKey.HazardYellow, 0.003f, null, false);
                Kit.BoxVisual(ctx.Geometry, "WobblyPost" + i, new Vector3(WobblyX, 2.0f, WobblyZ[i]), new Vector3(0.5f, 4.3f, 0.5f), MatKey.MetalDark);
            }
            ctx.MarkPoint("S3.Cat0", new Vector3(WobblyX, UpperY, 46.8f));
            ctx.MarkPoint("S3.Cat1", new Vector3(WobblyX, UpperY, 53.2f));
            ctx.MarkPoint("S3.Cat2", new Vector3(WobblyX, UpperY, 58f));
            ctx.MarkPoint("S3.Cat3", new Vector3(WobblyX, UpperY, 63.6f));
            PostSign(ctx.Decor, new Vector3(WobblyX, UpperY + 3.7f, 47.75f), 180f, "WOBBLY CATWALK\n<size=60%>STAY ON THE LINE. THE LINE IS YOUR FRIEND.</size>", 3.4f, 1.1f, SignStyle.Warning, UpperY, true);

            // turbo express lane: 5.7 m gap, only TURBO LEGS make it
            L2Parts.Rect(g, -6.5f, -4.5f, 48f, 51.5f, UpperY, 0.4f, MatKey.Grate);
            L2Parts.Rect(g, -6.5f, -4.5f, 57.2f, 62f, UpperY, 0.4f, MatKey.Grate);
            PerspectiveKit.SpeedRoute(ctx.Puzzles, new[] { new Vector3(-5.5f, UpperY, 44f), new Vector3(-5.5f, UpperY, 51.4f), new Vector3(-5.5f, UpperY, 61.5f) });
            PostSign(ctx.Decor, new Vector3(-5.5f, UpperY + 3.7f, 47.75f), 180f, "TURBO EXPRESS LANE\n<size=60%>LEGS REQUIRED. 5.7 M OF NOPE.</size>", 3f, 1.1f, SignStyle.Danger, UpperY, true);
            ctx.MarkPoint("S3.TurboTakeoff", new Vector3(-5.5f, UpperY, 51.4f));
            ctx.MarkPoint("S3.TurboLanding", new Vector3(-5.5f, UpperY, 58.6f));

            ctx.Hint(new Vector3(0f, UpperY + 2f, 55f), new Vector3(16f, 5f, 18f),
                "The catwalk slabs tilt toward whoever stands off-center (the Brute tilts them the most). Walk the yellow line one at a time. The Sprinter can sprint-jump the express lane.",
                "Balance. Like a career, but hotter.");

            // deck B (S3 landing + S4 lower deck) - built in S4; checkpoint 2 here
            ctx.Checkpoint(new Vector3(0f, UpperY, 67f), 0f);
            ctx.MarkPoint("C2.A", new Vector3(-1.6f, UpperY, 67f));
            ctx.MarkPoint("C2.B", new Vector3(0f, UpperY, 67.7f));
            ctx.MarkPoint("C2.C", new Vector3(1.6f, UpperY, 67f));
            var d = ctx.Decor;
            EnvKit.Decor.SteamStack(d, new Vector3(-7.4f, UpperY, 45f), 3f);
            Sign(d, new Vector3(6.2f, UpperY + 1.62f, 47.82f), 180f, "SHIFT YOUR WEIGHT\n<size=60%>CAREFULLY. THE FLOOR IS LAVA. LITERALLY.</size>", 2.8f, 1.0f, SignStyle.Warning);
            EnvKit.Decor.Gear(d, new Vector3(8.5f, 3.5f, 52f), Quaternion.Euler(0f, 0f, 90f), 1.4f, -20f);
            EnvKit.Decor.Gear(d, new Vector3(8.5f, 2.2f, 56f), Quaternion.Euler(0f, 0f, 90f), 0.9f, 32f);
            EnvKit.Decor.Chain(d, new Vector3(-1.5f, UpperY + 5f, 55f), 3.5f);
            EnvKit.Decor.Chain(d, new Vector3(6f, UpperY + 5f, 50f), 2.8f);
        }

        // ================================================================== S4 counterweight lift
        void BuildS4(LevelBuildContext ctx)
        {
            var g = ctx.Section("S4_Counterweight", Vector3.zero);
            float y = UpperY;
            float ax0 = PulleyX - PulleySeparation * 0.5f - PulleyPlatform * 0.5f - 0.05f; // -3.05
            float ax1 = ax0 + PulleyPlatform + 0.1f;                                         //  0.05
            float bx0 = PulleyX + PulleySeparation * 0.5f - PulleyPlatform * 0.5f - 0.05f;  //  2.95
            float bx1 = bx0 + PulleyPlatform + 0.1f;                                         //  6.05
            float pz0 = PulleyZ - PulleyPlatform * 0.5f - 0.05f, pz1 = PulleyZ + PulleyPlatform * 0.5f + 0.05f; // 77.45..80.55

            // lower deck with a notch for PEOPLE platform A and the CARGO loading pit for B
            L2Parts.Rect(g, -8f, 8f, 62f, pz0, y, 1f, MatKey.FloorAlt);
            L2Parts.Rect(g, -8f, ax0, pz0, pz1, y, 1f, MatKey.FloorAlt);
            L2Parts.Rect(g, ax1, bx0, pz0, pz1, y, 1f, MatKey.FloorAlt);
            L2Parts.Rect(g, bx1, 8f, pz0, pz1, y, 1f, MatKey.FloorAlt);
            L2Parts.Rect(g, -8f, 8f, pz1, 84f, y, 1f, MatKey.FloorAlt);
            // loading pit liner walls (contain the falling block)
            L2Parts.Box(ctx.Geometry, "PitLinerW", bx0 - 0.2f, bx0, 1f, y - 1f, pz0, pz1, MatKey.MetalRust);
            L2Parts.Box(ctx.Geometry, "PitLinerE", bx1, bx1 + 0.2f, 1f, y - 1f, pz0, pz1, MatKey.MetalRust);
            L2Parts.Box(ctx.Geometry, "PitLinerS", bx0 - 0.2f, bx1 + 0.2f, 1f, y - 1f, pz0 - 0.2f, pz0, MatKey.MetalRust);
            L2Parts.Box(ctx.Geometry, "PitLinerN", bx0 - 0.2f, bx1 + 0.2f, 1f, y - 1f, pz1, pz1 + 0.2f, MatKey.MetalRust);
            EnvKit.HazardStripe(ctx.Decor, new Vector3((bx0 + bx1) * 0.5f, y + 0.006f, pz0 - 0.25f), new Vector3(3.2f, 0.012f, 0.4f));
            for (float px = -7f; px <= 7f; px += 7f)
                EnvKit.Pillar(ctx.Geometry, new Vector3(px, -2.5f, 72f), y - 1f + 2.5f, 0.9f);

            var lift = L2Parts.CounterweightLift(ctx.Puzzles, new Vector3(PulleyX, y, PulleyZ), PulleySeparation, PulleyTravel, PulleyPlatform, CargoPitDepth);
            ctx.Mark("S4.Pulley", lift);
            PostSign(ctx.Decor, new Vector3(PulleyX - PulleySeparation * 0.5f, y + 2.6f, pz0 - 0.15f), 180f, "PEOPLE", 2.8f, 0.6f, SignStyle.Info, y, true);   // arch over the way onto A
            PostSign(ctx.Decor, new Vector3(PulleyX + PulleySeparation * 0.5f, y + 2.6f, pz0 - 0.15f), 180f, "CARGO\n<size=60%>(THE HEAVIER SIDE GOES DOWN)</size>", 2.6f, 0.9f, SignStyle.Warning, y, true);
            Sign(ctx.Decor, new Vector3(PulleyX, y + 3.8f, PulleyZ - 0.25f), 180f, "LOAD SHIFTING\n<size=60%>IN PROGRESS</size>", 1.8f, 0.8f, SignStyle.Info);

            // the counterweight
            var block = PuzzleKit.HeavyBlock(ctx.Props, new Vector3(PulleyX + PulleySeparation * 0.5f, y, 71f));
            ctx.Mark("S4.Block", block);
            ctx.MarkPoint("S4.BlockNear", new Vector3(PulleyX + PulleySeparation * 0.5f, y, pz0 - 1.85f));
            ctx.MarkPoint("S4.PitEdge", new Vector3(PulleyX + PulleySeparation * 0.5f, y, pz0));
            ctx.MarkPoint("S4.BruteBack", new Vector3(PulleyX + PulleySeparation * 0.5f, y, pz0 - 3.4f));
            ctx.MarkPoint("S4.A1", new Vector3(PulleyX - PulleySeparation * 0.5f - 0.7f, y, PulleyZ - 0.6f));
            ctx.MarkPoint("S4.A2", new Vector3(PulleyX - PulleySeparation * 0.5f + 0.7f, y, PulleyZ + 0.6f));
            ctx.MarkPoint("S4.TopA1", new Vector3(-2.2f, TopY, 82.6f));
            ctx.MarkPoint("S4.TopA2", new Vector3(-0.4f, TopY, 83.3f));

            // basement under the cargo basket (anyone who rides B down walks back up the maintenance stairs)
            float by = y - CargoPitDepth - PulleyTravel; // -1.6
            L2Parts.Rect(g, bx1, 11.5f, 72f, 86f, by, 0.6f, MatKey.Concrete);
            L2Parts.Rect(g, 2f, bx1, 72f, pz0, by, 0.6f, MatKey.Concrete);
            L2Parts.Rect(g, 2f, bx1, pz1, 86f, by, 0.6f, MatKey.Concrete);
            L2Parts.Rect(g, 2f, bx0, pz0, pz1, by, 0.6f, MatKey.Concrete);
            EnvKit.Stairs(ctx.Geometry, new Vector3(9.75f, by, 84.5f), Vector3.back, 2.5f, (y - by) / 25f, 0.45f, 25, MatKey.Concrete);
            L2Parts.Rect(g, 8f, 11.5f, 69f, 84.5f - 25f * 0.45f, y, 1f, MatKey.FloorAlt);
            PostSign(ctx.Decor, new Vector3(9.75f, by + 2.6f, 85.9f), 180f, "BASEMENT\n<size=60%>HOW DID YOU GET DOWN HERE? STAIRS ->UP</size>", 3.2f, 1f, SignStyle.Info, by);
            Rail(g, 7.9f, 62.1f, 7.9f, 69f, y);
            Rail(g, 7.9f, 84.5f - 25f * 0.45f, 7.9f, 83.9f, y);
            Rail(g, -7.9f, 62.1f, -7.9f, 66f, y);
            Rail(g, -7.9f, 76f, -7.9f, 83.9f, y);
            Rail(g, -7.9f, 62.1f, -6.6f, 62.1f, y);
            Rail(g, -4.4f, 62.1f, 0.9f, 62.1f, y);
            Rail(g, 4.1f, 62.1f, 7.9f, 62.1f, y);

            // FREIGHT STAIRS (locked shutter at the bottom, opened by the lever upstairs)
            var stairsLever = PuzzleKit.Lever(ctx.Puzzles, new Vector3(-4f, TopY, 84.5f), null, 0, true, false, "FREIGHT STAIRS UNLOCKED");
            ctx.Mark("S4.StairsLever", stairsLever);
            ctx.MarkPoint("S4.StairsLeverStand", new Vector3(-4f, TopY, 85.7f));
            var stairsDoor = PuzzleKit.Door(ctx.Puzzles, new Vector3(-6.5f, y, 66.5f), null, DoorMode.SlideUp, 3f, 4.2f, stairsLever);
            ctx.Mark("S4.StairsDoor", stairsDoor);
            EnvKit.Stairs(ctx.Geometry, new Vector3(-6.5f, y, 67f), Vector3.forward, 3f, 0.3f, 0.45f, 20, MatKey.MetalLight);
            Wall(g, -4.6f, 66.95f, -4.6f, 76f, y, 6.5f, MatKey.WallAlt);
            L2Parts.Rect(g, -8f, -5f, 76f, 80.6f, TopY, 0.6f, MatKey.Grate);
            Rail(g, -4.9f, 76f, -4.9f, 80.6f, TopY);
            Rail(g, -7.9f, 76f, -7.9f, 80.6f, TopY);
            Sign(ctx.Decor, new Vector3(-6.5f, y + 5.3f, 66.19f), 180f, "FREIGHT STAIRS\n<size=60%>LOCKED FROM ABOVE. FOR SAFETY. (WHOSE? UNCLEAR.)</size>", 3.4f, 1.1f, SignStyle.Info);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(-4.1f, y, 66.2f), new Vector3(-4.1f, TopY, 84.3f), stairsLever);
            ctx.MarkPoint("S4.BruteRoute1", new Vector3(-3.4f, y, 65.2f));
            ctx.MarkPoint("S4.DoorFront", new Vector3(-6.5f, y, 65.0f));
            ctx.MarkPoint("S4.StairsBottom", new Vector3(-6.5f, y, 67.7f));
            ctx.MarkPoint("S4.StairsTop", new Vector3(-6.5f, TopY, 76.7f));
            ctx.MarkPoint("S4.WalkwayEnd", new Vector3(-6.5f, TopY, 80.2f));
            ctx.MarkPoint("S4.UpperArrive", new Vector3(-6.4f, TopY, 86.2f));

            ctx.Hint(new Vector3(0f, y + 2f, 74f), new Vector3(16f, 5f, 22f),
                "Heavier side sinks. Put the Sprinter and the Brainiac on PEOPLE (120-140 kg), then the Brute shoves the 160 kg block into the CARGO pit. Upstairs, a lever unlocks the freight stairs for the Brute.",
                "Somebody goes up, something goes down. That's physics. Or office politics.");

            // ---------------- upper deck (y = 12)
            L2Parts.Rect(g, -8f, 8f, 80.6f, 110f, TopY, 1f, MatKey.Floor);
            Rail(g, 0.1f, 80.7f, 7.9f, 80.7f, TopY);
            Rail(g, -4.95f, 80.7f, -3.1f, 80.7f, TopY);
            Rail(g, -7.9f, 80.7f, -7.9f, 109.9f, TopY);
            Rail(g, 7.9f, 80.7f, 7.9f, 109.9f, TopY);
            EnvKit.Pillar(ctx.Geometry, new Vector3(-7.2f, y, 83.3f), TopY - 1f - y, 0.8f);
            EnvKit.Pillar(ctx.Geometry, new Vector3(7.2f, y, 83.3f), TopY - 1f - y, 0.8f);

            // crusher gate + 2.5 m stack ledge with the crusher's OFF lever
            Wall(g, -8f, 91f, -2.2f, 91f, TopY, 4.5f, MatKey.WallAlt);
            Wall(g, 2.2f, 91f, 8f, 91f, TopY, 4.5f, MatKey.WallAlt);
            var ledge = L2Parts.Box(ctx.Geometry, "StackLedge", 2.6f, 6.6f, TopY - 0.5f, StackLedgeTop, 86.6f, 90.8f, MatKey.MetalRust);
            Kit.BoxVisual(ledge.transform, "LedgeTrim", new Vector3(0f, (StackLedgeTop - TopY + 0.5f) * 0.5f - 0.03f, -2.1f), new Vector3(4.05f, 0.08f, 0.06f), MatKey.HazardYellow, 0.01f, null, false);
            var crusherLever = PuzzleKit.Lever(ctx.Puzzles, new Vector3(4.6f, StackLedgeTop, 89.6f), Yaw(180f), 0, true, false, "CRUSHER OFF. MOSTLY.");
            ctx.Mark("S4.CrusherLever", crusherLever);
            ctx.MarkPoint("S4.CrusherLeverStand", new Vector3(4.6f, StackLedgeTop, 88.4f));
            ctx.MarkPoint("S4.LedgeTop", new Vector3(4.6f, StackLedgeTop, 88.1f));
            ctx.MarkPoint("S4.StackBase", new Vector3(4.6f, TopY, 86.15f));
            ctx.MarkPoint("S4.StackRunup", new Vector3(4.6f, TopY, 83.2f));
            ctx.MarkPoint("S4.StackApproach", new Vector3(2.0f, TopY, 85.0f));
            ctx.MarkPoint("S4.LedgeDrop", new Vector3(1.4f, TopY, 88.0f));
            var crusher = PuzzleKit.Crusher(ctx.Hazards, new Vector3(0f, TopY, 91f), null, new Vector2(3.6f, 2.4f), 3.6f, 1.8f, 0f);
            crusher.Invert = true;
            PuzzleKit.Wire(crusher, crusherLever);
            ctx.Mark("S4.Crusher", crusher);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(2.5f, TopY, 89.6f), new Vector3(2.0f, TopY, 90.6f), crusherLever);
            Sign(ctx.Decor, new Vector3(4.6f, TopY + 1.6f, 86.55f), 180f, "STACK HERE\n<size=60%>CRUSHER OFF-SWITCH UPSTAIRS</size>", 2.8f, 0.9f, SignStyle.Warning);
            Sign(ctx.Decor, new Vector3(-4.5f, TopY + 3.2f, 90.75f), 180f, "CRUSHER\n<size=60%>TIMING IS A SOFT SKILL</size>", 3f, 1f, SignStyle.Danger);
            ctx.MarkPoint("S4.CrBefore", new Vector3(0f, TopY, 88.4f));
            ctx.MarkPoint("S4.CrAfter", new Vector3(0f, TopY, 93.6f));
            ctx.Hint(new Vector3(2f, TopY + 2f, 87f), new Vector3(12f, 5f, 10f),
                "The crusher's off-switch is on the 2.5 m ledge. Park the Brainiac against the ledge, jump onto their head, then onto the ledge, and pull the lever.",
                "Stand on the shoulders of giants. Or the nearest Brainiac.");

            var d = ctx.Decor;
            EnvKit.Decor.Machine(d, new Vector3(-6.4f, TopY, 88.6f), Yaw(90f), new Vector3(2f, 2.4f, 2.6f));
            EnvKit.Decor.Gear(d, new Vector3(-7.95f, TopY + 3f, 95f), Quaternion.Euler(0f, 0f, 90f), 1.1f, 24f);
            EnvKit.Decor.SteamStack(d, new Vector3(7.2f, TopY, 95f), 3.2f);
            EnvKit.Decor.CrateStack(d, new Vector3(-6.6f, y, 81.8f), Yaw(8f), 2, 3);
            EnvKit.Poster(d, new Vector3(-3.0f, TopY + 1.7f, 90.78f), Face(Yaw(180f)), "WEIGHT SHIFTS. PEOPLE RISE.", "COUNTERWEIGHTS: BECAUSE SOMEONE HAS TO GO DOWN.");
            EnvKit.LightPanel(ctx.Lights, new Vector3(0f, y + 10.2f, 84f), Face(Yaw(180f) * Quaternion.Euler(35f, 0f, 0f)), new Vector2(3f, 0.5f), Warm, true, 2.6f, 18f);

            ctx.Checkpoint(new Vector3(0f, TopY, 97f), 0f);
            ctx.MarkPoint("C3.A", new Vector3(-1.6f, TopY, 97f));
            ctx.MarkPoint("C3.B", new Vector3(0f, TopY, 97.7f));
            ctx.MarkPoint("C3.C", new Vector3(1.6f, TopY, 97f));
        }

        // ================================================================== S5 scale of justice
        void BuildS5(LevelBuildContext ctx)
        {
            var g = ctx.Section("S5_ScaleOfJustice", Vector3.zero);
            float y = TopY;
            var scale = PuzzleKit.WeightRangePlate(ctx.Puzzles, new Vector3(-3.5f, y, 104f), null, ScaleMinKg, ScaleMaxKg, true, "SHIPMENT ACCEPTED");
            L2Parts.Enlarge(scale, 1.6f);
            ctx.Mark("S5.Scale", scale);
            ctx.MarkPoint("S5.ScaleB", new Vector3(-4.3f, y, 103.3f));
            ctx.MarkPoint("S5.ScaleS", new Vector3(-2.7f, y, 103.3f));
            ctx.MarkPoint("S5.ScaleI", new Vector3(-3.5f, y, 104.75f));
            ctx.Mark("S5.Crate1", PuzzleKit.Crate(ctx.Props, new Vector3(-7f, y, 101.4f)));
            ctx.Mark("S5.Crate2", PuzzleKit.Crate(ctx.Props, new Vector3(-7f, y, 102.8f), Yaw(20f)));
            Sign(ctx.Decor, new Vector3(-3.5f, y + 4.2f, 107.6f), 180f, "SHIPPING SCALE: 200-270 KG\n<size=62%>NOT ONE KILO MORE.</size>", 5f, 1.3f, SignStyle.Warning);
            Kit.BoxVisual(ctx.Decor, "ScaleSignPost", new Vector3(-3.5f, y + 1.8f, 107.7f), new Vector3(0.15f, 3.6f, 0.15f), MatKey.MetalDark);
            PerspectiveKit.Clue(ctx.Decor, new Vector3(-3.5f, y + 0.04f, 101.5f), Quaternion.Euler(90f, 0f, 0f), "YOU THREE: 220-260 KG\nTWO + CRATES: MAYBE", 0.45f);   // painted in front of the scale

            // exit gate (opens when the scale latches)
            Wall(g, -8f, 110f, 0f, 110f, y, 5f, MatKey.WallAlt);
            Wall(g, 5f, 110f, 8f, 110f, y, 5f, MatKey.WallAlt);
            var gate = PuzzleKit.Door(ctx.Puzzles, new Vector3(2.5f, y, 110f), null, DoorMode.SlideUp, 4f, 4.5f, scale);
            ctx.Mark("S5.Gate", gate);
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(-1.85f, y, 104f), new Vector3(0.2f, y, 109.4f), scale);
            Sign(ctx.Decor, new Vector3(2.5f, y + 5.4f, 109.69f), 180f, "SHIPPING DOCK ->", 4.6f, 0.55f, SignStyle.Exit);   // on the gate gantry
            ctx.MarkPoint("S5.GateFront", new Vector3(2.5f, y, 107.5f));
            ctx.Hint(new Vector3(-1f, y + 2f, 104f), new Vector3(14f, 5f, 12f),
                "The scale wants 200-270 kg and latches: all three of you (220-260 kg) squeeze on together. Two of you plus both crates may also work.",
                "Group photo on the scale. Everybody squeeze in. Hold your breath.");

            // crusher corridor (timed hazard) to the SHIPPING DOCK
            L2Parts.Rect(g, 0f, 5f, 110f, 124f, y, 1f, MatKey.Grate);
            L2Parts.Rect(g, -1.5f, 6.5f, 124f, 131f, y, 1f, MatKey.Floor);
            Wall(g, 0.3f, 110.45f, 0.3f, 124f, y, 4.5f, MatKey.Wall);
            Wall(g, 4.7f, 110.45f, 4.7f, 124f, y, 4.5f, MatKey.Wall);
            var c1 = PuzzleKit.Crusher(ctx.Hazards, new Vector3(2.5f, y, 114.5f), null, new Vector2(3f, 2f), 3.6f, 1.8f, 0f);
            var c2 = PuzzleKit.Crusher(ctx.Hazards, new Vector3(2.5f, y, 119.5f), null, new Vector2(3f, 2f), 3.6f, 1.8f, 1.83f);
            ctx.Mark("S5.Crusher1", c1);
            ctx.Mark("S5.Crusher2", c2);
            ctx.MarkPoint("S5.Cr1Before", new Vector3(2.5f, y, 112f));
            ctx.MarkPoint("S5.Mid", new Vector3(2.5f, y, 117f));
            ctx.MarkPoint("S5.Cr2After", new Vector3(2.5f, y, 122f));
            ctx.Hint(new Vector3(2.5f, y + 2f, 117f), new Vector3(5f, 5f, 14f),
                "Wait until a crusher has finished rising, then go - one crusher at a time. The gap between them is safe.",
                "Timing is everything. So is having a skull.");
            Rail(g, -1.4f, 130.9f, 6.4f, 130.9f, y);
            Rail(g, -1.4f, 124.1f, -1.4f, 130.9f, y);
            Rail(g, 6.4f, 124.1f, 6.4f, 130.9f, y);

            ctx.Exit(new Vector3(2.5f, y, 125f), 0f);
            ctx.MarkPoint("S5.Exit1", new Vector3(1.4f, y + 0.1f, 127.1f));
            ctx.MarkPoint("S5.Exit2", new Vector3(2.5f, y + 0.1f, 128.3f));
            ctx.MarkPoint("S5.Exit3", new Vector3(3.6f, y + 0.1f, 127.1f));

            var d = ctx.Decor;
            EnvKit.Poster(d, new Vector3(-6.2f, y + 2.3f, 109.78f), Face(Yaw(180f)), "NOT ONE KILO MORE", "WE WEIGH THE INTERNS. WE ALWAYS WEIGH THE INTERNS.");
            EnvKit.Decor.Machine(d, new Vector3(6.4f, y, 105.5f), Yaw(-90f), new Vector3(2f, 2.2f, 2.4f));
            EnvKit.Decor.Barrel(d, new Vector3(-7f, y, 106.5f), 1);
            EnvKit.Decor.Barrel(d, new Vector3(-6.2f, y, 107.2f), 0);
            EnvKit.Decor.WarningBeacon(d, new Vector3(0.3f, y + 4.7f, 117f), false);
            EnvKit.LightPanel(ctx.Lights, new Vector3(0f, y + 4.6f, 100f), Face(Yaw(0f) * Quaternion.Euler(30f, 0f, 0f)), new Vector2(3f, 0.5f), Warm, true, 2.4f, 16f);
        }

        // ================================================================== backdrop
        void BuildBackdrop(LevelBuildContext ctx)
        {
            EnvKit.VoidBackdrop(ctx.Decor, LevelTheme.Factory, new Bounds(new Vector3(0f, 6f, 62f), new Vector3(24f, 26f, 150f)), 22);
            var d = ctx.Decor;
            EnvKit.Decor.SteamStack(d, new Vector3(-12f, -2f, 30f), 6f);
            EnvKit.Decor.SteamStack(d, new Vector3(12.5f, 0f, 60f), 7f);
            EnvKit.Decor.SteamStack(d, new Vector3(-12.5f, 4f, 100f), 6f);
            EnvKit.Pipe(d, new[] { new Vector3(10f, -2f, 20f), new Vector3(10f, 9f, 20f), new Vector3(10f, 9f, 90f), new Vector3(10f, 15f, 90f) }, 0.3f);
            EnvKit.Pipe(d, new[] { new Vector3(-10.5f, 0f, 50f), new Vector3(-10.5f, 13f, 50f), new Vector3(-10.5f, 13f, 120f) }, 0.25f);
        }
    }
}
