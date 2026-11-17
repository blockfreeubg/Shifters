// =====================================================================================
//  SHIFTERS - L12Builder.Arena.cs   [SHIFT 11 - the atrium: walls, closet, break areas, the three stations,
//  the breaker alcove, the P2 pads + loop, the boss (eyes, shadow, SCARE-O-MATIC 3000) and the night decor]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L12Builder
    {
        void BuildArena(LevelBuildContext ctx)
        {
            var a = ctx.Section("Atrium", Vector3.zero);
            var deco = ctx.Section("AtriumDecor", Vector3.zero);

            // ---------------- shell: ONE floor collider, 6 m walls (south 9 m), openings for the gate, the alcove and the exit
            Slab(a, -15.4f, 15.4f, -0.4f, 30.4f, 0f, 0.6f, MatKey.Marble, false, "AtriumFloor");
            WallZ(a, -15.2f, -0.4f, 30.4f, WallH);
            WallZ(a, 15.2f, -0.4f, 24.8f, WallH);
            WallZ(a, 15.2f, 29.2f, 30.4f, WallH);
            Kit.SolidBox(a, "AlcoveLintel", new Vector3(15.2f, 4.3f, 27f), new Vector3(0.4f, 3.4f, 4.4f), MatKey.WallAlt, 0.03f);
            WallX(a, -15.2f, -2.5f, 30.2f, WallH);
            WallX(a, 2.5f, 15.2f, 30.2f, WallH);
            Kit.SolidBox(a, "ExitLintel", new Vector3(0f, 5.25f, 30.2f), new Vector3(5f, 1.5f, 0.4f), MatKey.Wall, 0.03f);
            WallX(a, -15.2f, -2f, -0.2f, SouthWallH);
            WallX(a, 2f, 15.2f, -0.2f, SouthWallH);
            Kit.SolidBox(a, "GateHeader", new Vector3(0f, (DeckY + 4f + SouthWallH) * 0.5f, -0.2f), new Vector3(4f, SouthWallH - DeckY - 4f, 0.4f), MatKey.Wall, 0.03f);
            // glow tape along the foot of every wall (walkable edges glow, G10)
            GlowZ(deco, -14.95f, 0f, 30f, 0f); GlowZ(deco, 14.95f, 0f, 25f, 0f); GlowX(deco, -15f, -2.5f, 29.95f, 0f); GlowX(deco, 2.5f, 15f, 29.95f, 0f);

            // ---------------- the arena (bounds only; our own walls have the openings)
            var arena = BossKit.Arena(ctx.Gameplay, new Vector3(0f, 0f, 15f), new Vector2(30f, 30f), 0f, false);
            ctx.Mark("boss.arena", arena.gameObject);
            var baW = BossKit.BreakArea(arena.transform, new Vector3(-11.5f, 0f, -11.5f), new Vector2(7f, 5f));
            var baE = BossKit.BreakArea(arena.transform, new Vector3(11.5f, 0f, -11.5f), new Vector2(7f, 5f));
            ctx.MarkPoint("boss.safe.2", new Vector3(-11.5f, 0f, 3.5f));
            ctx.MarkPoint("boss.safe.3", new Vector3(11.5f, 0f, 3.5f));
            ctx.Checkpoint(new Vector3(11.5f, 0f, 3.5f), 0f);
            for (int s = -1; s <= 1; s += 2)
                Sign(deco, new Vector3(s * 11.5f, 3.4f, 0.02f), Vector3.back, "BREAK AREA\n<size=70%>MONSTERS MUST CLOCK OUT</size>", 3.6f, 1.1f, SignStyle.Warning);

            // ---------------- BROOM CLOSET (holding cell) + fillers
            var cell = EnemyKit.HoldingCell(ctx.Enemies, new Vector3(0f, 0f, 7.2f), Quaternion.identity, "closet");
            if (_closetButton != null) cell.SetInputs(_closetButton);
            ctx.Mark("s1.closet", cell.gameObject);
            if (cell.Lamp != null) DecorLight.Mark(cell.Lamp);
            Kit.SolidBox(a, "ClosetFillerW", new Vector3(-2.525f, 1.6f, 7.2f), new Vector3(0.95f, 3.2f, 4.4f), MatKey.WallAlt, 0.03f);
            Kit.SolidBox(a, "ClosetFillerE", new Vector3(2.525f, 1.6f, 7.2f), new Vector3(0.95f, 3.2f, 4.4f), MatKey.WallAlt, 0.03f);
            Sign(deco, new Vector3(0f, 4.4f, 9.55f), Vector3.back, "BROOM CLOSET — JANITORIAL\n<size=70%>(ALSO ME)</size>", 3.4f, 0.8f, SignStyle.Info);
            EnvKit.Decor.Night.Cobweb(deco, new Vector3(-2f, 3.2f, 9.4f), null, 0.7f, false);

            // ---------------- lights controller + lit zones
            var lightsGo = Kit.Make("L12AtriumLights", ctx.Gameplay, Vector3.zero);
            _lights = lightsGo.AddComponent<L12AtriumLights>();
            var vt = lightsGo.transform;
            _lights.AlwaysKeys = new[]
            {
                VolumeKey(vt, "S0", new Vector3(0f, DeckY + 2.5f, -5f)),
                VolumeKey(vt, "Balcony", new Vector3(0f, 2.25f, 5.75f)),
                VolumeKey(vt, "BAW", new Vector3(-11.5f, 2.25f, 3.5f)),
                VolumeKey(vt, "BAE", new Vector3(11.5f, 2.25f, 3.5f)),
                VolumeKey(vt, "Lobby", new Vector3(0f, 2.25f, 34.4f)),
            };
            _lights.AlwaysSizes = new[] { new Vector3(6f, 6f, 10f), new Vector3(12f, 5.5f, 11.5f), new Vector3(7f, 5.5f, 5f), new Vector3(7f, 5.5f, 5f), new Vector3(10f, 5.5f, 8f) };
            _lights.KeyW = VolumeKey(vt, "FloodW", new Vector3(-10f, 2.25f, 15f));
            _lights.KeyN = VolumeKey(vt, "FloodN", new Vector3(0f, 2.25f, 15f));
            _lights.KeyE = VolumeKey(vt, "FloodE", new Vector3(10f, 2.25f, 15f));
            _lights.KeyRace = VolumeKey(vt, "Race", new Vector3(1f, 2.25f, 27.5f));
            _lights.KeyAlcove = VolumeKey(vt, "Alcove", new Vector3(17f, 2.25f, 27f));
            // walkable "always lit" islands read as lit: warm floor glows
            Holo(deco, "BalconyGlow", new Vector3(0f, 0.03f, 10.6f), new Vector3(12f, 0.02f, 1.6f), new Color(1f, 0.9f, 0.6f, 0.25f));

            BuildStations(ctx, a, deco);
            BuildAlcove(ctx, deco);
            BuildBoss(ctx, arena, cell, deco);
            BuildP2Floor(ctx, deco);
            BuildDecor(ctx, deco);
        }

        // ================================================================== the three stations + flood fixtures
        void BuildStations(LevelBuildContext ctx, Transform a, Transform deco)
        {
            // W GENERATOR CRANK (STR 7)
            var crankPos = new Vector3(-14.4f, 0f, 15f);
            var crank = PuzzleKit.Lever(ctx.Puzzles, crankPos, Yaw(90f), 7, true, false, "GENERATOR ONLINE");
            ctx.Mark("s1.crankW", crank.gameObject);
            ctx.MarkPoint("s1.crankW.stand", CrankStand);
            Kit.BoxVisual(deco, "Generator", new Vector3(-14.55f, 0.9f, 16.9f), new Vector3(1.0f, 1.8f, 2.0f), MatKey.MetalRust, 0.08f);
            Sign(deco, new Vector3(-15f, 2.9f, 15f), Vector3.left, "EMERGENCY CRANK\n<size=70%>THIS IS AN EMERGENCY.</size>", 3f, 1f, SignStyle.Warning);
            ctx.Label(deco, new Vector3(-13.8f, 0.03f, 15f), FloorText(90f), "RUSTY · STR 7", 0.3f, UIColors.Strength, 2.4f);

            // E FUSE BOX (IQ 7, latching)
            var fusePos = new Vector3(14.4f, 0f, 15f);
            var fuse = PuzzleKit.Terminal(ctx.Puzzles, fusePos, Yaw(-90f), 7, true, 6f, "FUSE REPLACED");
            ctx.Mark("s1.fuseE", fuse.gameObject);
            ctx.MarkPoint("s1.fuseE.stand", FuseStand);
            Kit.BoxVisual(deco, "FuseCabinet", new Vector3(14.75f, 1.6f, 16.6f), new Vector3(0.5f, 2.2f, 1.4f), MatKey.MetalDark, 0.04f);
            Sign(deco, new Vector3(15f, 2.9f, 15f), Vector3.right, "FUSE BOX\n<size=70%>DO NOT HACK (PLEASE HACK)</size>", 3f, 1f, SignStyle.Info);
            ctx.Label(deco, new Vector3(13.8f, 0.03f, 15f), FloorText(-90f), "FUSE · IQ 7", 0.3f, UIColors.Intellect, 2.4f);

            // N BREAKER RACE: TimedButton(28 m) at the start pool, curtain at x 15.5, MAIN BREAKER 2 m past it
            var race = PuzzleKit.TimedButton(ctx.Puzzles, new Vector3(-12f, 0f, 26.3f), Quaternion.identity, RaceRoute,
                new[] { RaceStand, new Vector3(15.5f, 0f, 27f) }, "");
            ctx.Mark("s1.race.button", race.gameObject);
            ctx.MarkPoint("s1.race.stand", RaceStand);
            PuzzleKit.LightPool(ctx.Puzzles, new Vector3(-12f, 0f, 27f), 3f, false);
            ctx.Label(deco, new Vector3(-12f, 0.03f, 28.6f), FloorText(), "ON YOUR MARKS", 0.42f, UIColors.AccentYellow, 4f);
            ctx.Label(deco, new Vector3(-6f, 0.03f, 27f), FloorText(90f), "TURBO RUN 28 M  >>>", 0.5f, UIColors.Speed, 6f);
            Sign(deco, new Vector3(-12f, 3.4f, 30f), Vector3.forward, "THE LIGHTS ARE SLOW.\n<size=70%>LIKE MANAGEMENT.</size>", 3.2f, 1f, SignStyle.Info);

            var breaker = PuzzleKit.Lever(ctx.Puzzles, new Vector3(18.6f, 0f, 27f), Yaw(-90f), 0, true, false, "BREAKER RESET");
            ctx.Mark("s1.race.lever", breaker.gameObject);
            ctx.MarkPoint("s1.race.leverStand", LeverStand);
            var laser = PuzzleKit.LaserBarrier(ctx.Hazards, new Vector3(15.5f, 0f, 27f), Yaw(90f), 4f, 3);
            ctx.Mark("s1.race.curtain", laser.gameObject);
            var gate = PuzzleKit.LogicGate(ctx.Puzzles, new Vector3(15.5f, 0f, 24.4f), null, SignalLogic.Any, false, race, breaker);
            PuzzleKit.HazardToggle(ctx.Puzzles, new Behaviour[] { laser }, false, gate);

            // flood signals (latched; P2 / RestorePhase set them)
            var fW = PuzzleKit.Signal(ctx.Puzzles, "FloodW"); var fN = PuzzleKit.Signal(ctx.Puzzles, "FloodN"); var fE = PuzzleKit.Signal(ctx.Puzzles, "FloodE");
            ctx.Mark("s1.floodW", fW.gameObject); ctx.Mark("s1.floodN", fN.gameObject); ctx.Mark("s1.floodE", fE.gameObject);
            _lights.Crank = crank; _lights.Breaker = breaker; _lights.Fuse = fuse; _lights.Race = race;
            _lights.FloodW = fW; _lights.FloodN = fN; _lights.FloodE = fE;

            _lights.VisW = Flood(deco, "FloodW", new Vector3(-14.8f, 5f, 15f), new Vector3(-10f, 0f, 15f), 10f);
            _lights.VisN = Flood(deco, "FloodN", new Vector3(0f, 5.5f, 29.8f), new Vector3(0f, 0f, 15f), 10f);
            _lights.VisE = Flood(deco, "FloodE", new Vector3(14.8f, 5f, 15f), new Vector3(10f, 0f, 15f), 10f);

            var rv = Kit.Make("RaceLights", deco, Vector3.zero).transform;
            Holo(rv, "RaceWash", new Vector3(1f, 0.04f, 27.5f), new Vector3(28f, 0.02f, 4f), new Color(1f, 0.95f, 0.7f, 0.22f));
            for (int i = 0; i < 6; i++) Kit.BoxVisual(rv, "RaceLamp", new Vector3(-11f + i * 5f, 4.6f, 29.95f), new Vector3(1.4f, 0.25f, 0.1f), MatKey.LightOn, 0f, null, false);
            _lights.VisRace = rv.gameObject;
            for (int i = 0; i < 6; i++) Kit.BoxVisual(deco, "RaceLampOff", new Vector3(-11f + i * 5f, 4.6f, 30.0f), new Vector3(1.4f, 0.25f, 0.06f), MatKey.LightOff, 0f, null, false);

            ctx.MarkPoint("s1.legW", new Vector3(-9f, 0f, 13f));
            ctx.MarkPoint("s1.lane.1", new Vector3(-7f, 0f, 15f));
            ctx.MarkPoint("s1.lane.2", new Vector3(0f, 0f, 15f));
            ctx.MarkPoint("s1.lane.3", new Vector3(7f, 0f, 15f));
            ctx.MarkPoint("s1.darkSpot", new Vector3(0f, 0f, 20f));
        }

        /// <summary>Flood fixture (always visible, dark lens) + the ON visuals (lens, beam, floor wash) returned for toggling.</summary>
        GameObject Flood(Transform deco, string name, Vector3 fixture, Vector3 target, float washWidth)
        {
            var dir = target - fixture;
            var fix = Kit.Make(name + "_Fixture", deco, fixture, Quaternion.LookRotation(dir.normalized)).transform;
            Kit.BoxVisual(fix, "Housing", Vector3.zero, new Vector3(1.4f, 0.9f, 0.7f), MatKey.MetalDark, 0.05f);
            Kit.BoxVisual(fix, "LensOff", new Vector3(0f, 0f, 0.36f), new Vector3(1.2f, 0.7f, 0.04f), MatKey.LightOff, 0f, null, false);
            var on = Kit.Make(name + "_On", deco, Vector3.zero).transform;
            Kit.BoxVisual(on, "LensOn", fixture + fix.forward * 0.4f, new Vector3(1.2f, 0.7f, 0.04f), MatKey.LightOn, 0f, fix.rotation, false);
            float len = dir.magnitude;
            Holo(on, "Beam", (fixture + target) * 0.5f, new Vector3(2.4f, 0.6f, len), new Color(1f, 0.95f, 0.75f, 0.10f), Quaternion.LookRotation(dir.normalized));
            Holo(on, "Wash", new Vector3(target.x, 0.035f, 15f), new Vector3(washWidth, 0.02f, 30f), new Color(1f, 0.93f, 0.7f, 0.16f));
            return on.gameObject;
        }

        // ================================================================== S1n BREAKER ALCOVE
        void BuildAlcove(LevelBuildContext ctx, Transform deco)
        {
            var s = ctx.Section("S1n_BreakerAlcove", Vector3.zero);
            Slab(s, 15f, 19.4f, 24.6f, 29.4f, 0f, 0.6f, MatKey.Concrete, false, "AlcoveFloor");
            WallX(s, 15.2f, 19.4f, 24.8f, 3.4f);
            WallX(s, 15.2f, 19.4f, 29.2f, 3.4f);
            WallZ(s, 19.2f, 24.6f, 29.4f, 3.4f);
            Sign(deco, new Vector3(19f, 2.4f, 27f), Vector3.right, "MAIN BREAKER\n<size=70%>RUNNERS ONLY</size>", 2.4f, 0.9f, SignStyle.Warning);
            var on = Kit.Make("AlcoveLamp_On", deco, Vector3.zero).transform;
            Kit.BoxVisual(on, "AlcoveLamp", new Vector3(17f, 3.3f, 27f), new Vector3(1.6f, 0.08f, 0.5f), MatKey.LightOn, 0f, null, false);
            Holo(on, "AlcoveWash", new Vector3(17f, 0.04f, 27f), new Vector3(4f, 0.02f, 4f), new Color(1f, 0.95f, 0.7f, 0.25f));
            _lights.VisAlcove = on.gameObject;
            Kit.BoxVisual(deco, "AlcoveLampOff", new Vector3(17f, 3.32f, 27f), new Vector3(1.6f, 0.06f, 0.5f), MatKey.LightOff, 0f, null, false);
        }

        // ================================================================== the boss
        void BuildBoss(LevelBuildContext ctx, BossArena arena, HoldingCell cell, Transform deco)
        {
            var bossGo = Kit.Make("L12Boogeyman", ctx.Enemies, L12BoogeymanBoss.Center);
            var boss = bossGo.AddComponent<L12BoogeymanBoss>();
            boss.Configure();
            boss.Arena = arena;
            boss.Lights = _lights; _lights.Boss = boss;
            boss.Closet = cell;
            ctx.Mark("boss", bossGo);
            _boss = boss;

            // body: the SCARE-O-MATIC 3000 (kinematic root; waits collider-off under the floor in P1)
            var body = Kit.Make("ScareOMatic3000", ctx.Enemies, L12BoogeymanBoss.Hidden);
            var rb = body.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;
            boss.Body = body.transform;
            boss.Machine = BossModels.ScareOMatic(body.transform, Vector3.zero, Quaternion.identity);
            var sheetGo = Kit.Make("SheetCollider", body.transform, Vector3.zero);
            var sheetCol = sheetGo.AddComponent<BoxCollider>(); sheetCol.center = new Vector3(0f, 1.0f, 0f); sheetCol.size = new Vector3(1.4f, 2.0f, 1.4f);
            sheetGo.AddComponent<L12BoogeySheet>().Boss = boss;
            var sheetMark = Kit.Make("SheetTarget", body.transform, new Vector3(0f, 1.3f, 0f));
            ctx.Mark("boss.weak.sheet", sheetMark);
            var punchGo = Kit.Make("ChargePunch", body.transform, new Vector3(0f, 1.0f, 0f));
            var pc = punchGo.AddComponent<SphereCollider>(); pc.isTrigger = true; pc.radius = 1.05f;
            punchGo.AddComponent<L12ChargePunch>().Boss = boss;
            ctx.Mark("boss.weak.punch", punchGo);
            var impact = Kit.Trigger(body.transform, "SheetImpact", new Vector3(0f, 1.1f, -0.35f), new Vector3(1.6f, 2.2f, 2.3f));   // also covers the projector cabinet on the back (only THROWN interns count)
            impact.gameObject.AddComponent<L12SheetImpact>().Boss = boss;
            ctx.Mark("boss.weak.impact", impact.gameObject);
            var hack = BossKit.WeakPointHack(body.transform, new Vector3(0f, 0f, -1.05f), Quaternion.Euler(0f, 180f, 0f), boss, 1 << 1);
            ctx.Mark("boss.weak.projector", hack.Source != null ? hack.Source.gameObject : hack.gameObject);   // the Terminal (the root's own IInteractable is the Punch path)
            ctx.Label(hack.transform, new Vector3(0f, 1.75f, 0f), Quaternion.Euler(0f, 180f, 0f), "PROJECTOR: HACK WHEN DIZZY (8 s)", 0.16f, UIColors.Intellect, 1.8f);
            boss.MachineColliders = new Collider[] { sheetCol, pc, impact };
            foreach (var c in hack.GetComponentsInChildren<Collider>(true))
            {
                var list = new System.Collections.Generic.List<Collider>(boss.MachineColliders) { c };
                boss.MachineColliders = list.ToArray();
            }
            PerspectiveKit.Reveal(sheetMark, PerspectiveMask.Weapon, "SHOOT THE SHEET", true, false, new Vector3(0f, 1.0f, 0f));
            PerspectiveKit.Reveal(punchGo, PerspectiveMask.Strength, "PUNCH WHILE CHARGING · STR 7", true, false, new Vector3(0f, 1.4f, 0f));

            // the 6 m silhouette (visual only) and the door-sized eyes
            boss.Shadow = BossModels.Boogeyman(ctx.Enemies, L12BoogeymanBoss.Center, Quaternion.Euler(0f, 180f, 0f));
            boss.Eyes = BuildEyes(ctx, boss);

            // BOO rings (r 5) on both pads
            boss.BooRingW = BooRing(ctx.Enemies, "BooRingW", L12BoogeymanBoss.PadW);
            boss.BooRingE = BooRing(ctx.Enemies, "BooRingE", L12BoogeymanBoss.PadE);

            // the north exit door is wired to the boss (B8: Defeated = ON)
            var door = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 30.2f), Quaternion.identity, DoorMode.SlideUp, 4f, 4f, boss);
            ctx.Mark("s3.door", door.gameObject);
            Sign(deco, new Vector3(0f, 4.9f, 30.0f), Vector3.forward, "LOBBY → EXIT\n<size=70%>LOCKED BY ORDER OF THE BOOGEYMAN</size>", 3.6f, 0.8f, SignStyle.Exit);
        }

        L12BoogeyEyes BuildEyes(LevelBuildContext ctx, L12BoogeymanBoss boss)
        {
            var root = Kit.Make("L12BoogeyEyes", ctx.Enemies, new Vector3(0f, 2.8f, 29f));
            var eyes = root.AddComponent<L12BoogeyEyes>();
            eyes.Boss = boss;
            var mesh = ProcMesh.Cached("l12_eye_unit", () => ProcMesh.Sphere(0.5f, 16, 12));
            var glow = MaterialLibrary.Unlit(new Color(1f, 0.93f, 0.55f), 2.2f);
            var black = Kit.Mat(MatKey.Black);
            Transform Eye(string n, float x)
            {
                var pivot = Kit.Make(n, root.transform, new Vector3(x, 0f, 0f)).transform;
                var oval = Kit.Make("Oval", pivot, Vector3.zero); oval.transform.localScale = new Vector3(1f, 2f, 0.3f);
                ProcMesh.AddRenderer(oval, mesh, glow, false);
                var pupil = Kit.Make("Pupil", pivot, new Vector3(0f, -0.2f, 0.14f)); pupil.transform.localScale = new Vector3(0.36f, 0.6f, 0.08f);
                ProcMesh.AddRenderer(pupil, mesh, black, false);
                return pivot;
            }
            eyes.EyeL = Eye("EyeL", -0.75f);
            eyes.EyeR = Eye("EyeR", 0.75f);
            // Spooky MILD: smiley-sticker pupils
            Transform Smile(string n, float x)
            {
                var s = Kit.Make(n, root.transform, new Vector3(x, -0.25f, 0.17f)); s.transform.localScale = new Vector3(0.42f, 0.42f, 0.05f);
                ProcMesh.AddRenderer(s, mesh, Kit.Mat(MatKey.HazardYellow), false);
                return s.transform;
            }
            eyes.SmileL = Smile("SmileL", -0.75f); eyes.SmileR = Smile("SmileR", 0.75f);
            var trig = root.AddComponent<SphereCollider>(); trig.isTrigger = true; trig.radius = 0.8f;
            eyes.Trigger = trig;
            PerspectiveKit.Reveal(root, PerspectiveMask.Weapon, "IMMUNE TO BULLETS (TRY A FLASH)", true, false, new Vector3(0f, 1.6f, 0f));
            return eyes;
        }

        Transform BooRing(Transform p, string name, Vector3 pad)
        {
            var root = Kit.Make(name, p, pad + Vector3.up * 0.05f).transform;
            var rim = Kit.Make("Rim", root, Vector3.zero, Quaternion.identity);
            ProcMesh.AddRenderer(rim, ProcMesh.Cached("l12_boo_rim", () => ProcMesh.Torus(L12BoogeymanBoss.BooRadius, 0.08f, 48, 4)), MaterialLibrary.Unlit(Purple, 1.6f), false);
            rim.transform.localScale = new Vector3(1f, 0.25f, 1f);
            var fill = Kit.Make("Fill", root, Vector3.zero);
            ProcMesh.AddRenderer(fill, ProcMesh.Cached("l12_boo_fill", () => ProcMesh.Sphere(L12BoogeymanBoss.BooRadius, 32, 4)), MaterialLibrary.Hologram(new Color(Purple.r, Purple.g, Purple.b, 0.22f)), false);
            fill.transform.localScale = new Vector3(0.05f, 0.004f, 0.05f);
            var lbl = _ctx.Label(root, new Vector3(0f, 0.04f, -L12BoogeymanBoss.BooRadius + 0.6f), FloorText(), "BOO IN 1.2 s — BRING A FRIEND", 0.34f, Purple, 5f);
            root.gameObject.SetActive(false);
            return root;
        }

        // ================================================================== P2 floor: charging pads, buffing loop, throw pads
        void BuildP2Floor(LevelBuildContext ctx, Transform deco)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var pad = new Vector3(s * 6f, 0f, 19f);
                PuzzleVisuals.Cyl(deco, "ChargingPad", pad + Vector3.up * 0.012f, 1.5f, 0.02f, MatKey.LightBlue, null, 24, false);
                PuzzleVisuals.Cyl(deco, "ChargingPadRim", pad + Vector3.up * 0.008f, 1.7f, 0.016f, MatKey.HazardStripes, null, 24, false);
                ctx.Label(deco, pad + new Vector3(0f, 0.04f, -2.1f), FloorText(), "CHARGING STATION\n<size=70%>DO NOT DISTURB</size>", 0.3f, UIColors.Info, 3.4f);
                ctx.MarkPoint(s < 0 ? "s2.padW" : "s2.padE", pad);
                var tp = new Vector3(s * 14f, 0f, 23.2f);   // 9 m diagonal: a STR 9-10 Brute's shortest aimable lob is ~8.8 m (the E/W walls stop an 8 m+ straight line)
                PuzzleVisuals.Cyl(deco, "ThrowPad", tp + Vector3.up * 0.012f, 0.9f, 0.02f, MatKey.HazardYellow, null, 20, false);
                ctx.Label(deco, tp + new Vector3(-s * 0.2f, 0.04f, -1.3f), FloorText(), "THROW FROM HERE · 9 M", 0.24f, UIColors.Strength, 2.6f);
                ctx.MarkPoint(s < 0 ? "s2.throwW" : "s2.throwE", tp, s < 0 ? 117.7f : -117.7f);
                ctx.MarkPoint(s < 0 ? "s2.shootW" : "s2.shootE", new Vector3(s * 12f, 0f, 24f));
            }
            ctx.MarkPoint("s2.padE.punchStand", new Vector3(7.6f, 0f, 19f));
            ctx.MarkPoint("s2.padE.pairSpot", new Vector3(9.5f, 0f, 21f));
            // the buffing loop (faint wax shine) + its sign
            Holo(deco, "WaxN", new Vector3(0f, 0.02f, 24f), new Vector3(13.4f, 0.01f, 1.4f), new Color(0.75f, 0.85f, 1f, 0.12f));
            Holo(deco, "WaxS", new Vector3(0f, 0.02f, 14f), new Vector3(13.4f, 0.01f, 1.4f), new Color(0.75f, 0.85f, 1f, 0.12f));
            Holo(deco, "WaxW", new Vector3(-6f, 0.02f, 19f), new Vector3(1.4f, 0.01f, 8.6f), new Color(0.75f, 0.85f, 1f, 0.12f));
            Holo(deco, "WaxE", new Vector3(6f, 0.02f, 19f), new Vector3(1.4f, 0.01f, 8.6f), new Color(0.75f, 0.85f, 1f, 0.12f));
            EnvKit.Decor.Cone(deco, new Vector3(0f, 0f, 21.8f));
            ctx.Label(deco, new Vector3(0f, 1.25f, 21.8f), Quaternion.identity, "CAUTION: WET FLOOR.\n<size=70%>ALSO HAUNTED.</size>", 0.22f, UIColors.AccentYellow, 2.6f);
        }

        // ================================================================== night dressing (story signage, mural, posters)
        void BuildDecor(LevelBuildContext ctx, Transform deco)
        {
            // ME-TV
            var tvPos = new Vector3(-6.5f, 4.4f, 29.9f);
            var tv = StoryKit.MeTvScreen(deco, tvPos, Look(Vector3.forward), 3.2f);
            if (tv != null) ctx.Mark("story.metv", tv);
            ctx.Label(deco, new Vector3(-6.5f, 2.55f, 29.92f), Look(Vector3.forward), "SPLIT UP! IT'S FASTER! — ME", 0.26f, UIColors.Danger, 3.4f);

            // "TEAM SPIRIT 1999" mural on the south wall (somebody stuck googly eyes on every face)
            var m = Kit.Make("Mural", deco, new Vector3(-9f, 6.6f, 0.02f), Quaternion.identity).transform;
            Kit.BoxVisual(m, "Canvas", Vector3.zero, new Vector3(9f, 3.2f, 0.04f), MatKey.Poster, 0f, null, false);
            ctx.Label(m, new Vector3(0f, 1.0f, 0.05f), Look(Vector3.back), "TEAM SPIRIT 1999", 0.62f, UIColors.Hex(0xF2C14E), 8.6f);
            var faceMat = MaterialLibrary.Colored(new Color(0.93f, 0.74f, 0.58f));
            var eyeMesh = ProcMesh.Cached("l12_googly", () => ProcMesh.Sphere(0.12f, 10, 6));
            var faceMesh = ProcMesh.Cached("l12_face", () => ProcMesh.Sphere(0.42f, 12, 8));
            for (int i = 0; i < 6; i++)
            {
                float x = -3.4f + i * 1.36f;
                var face = Kit.Make("Face", m, new Vector3(x, -0.4f, 0.06f)); face.transform.localScale = new Vector3(1f, 1.15f, 0.2f);
                ProcMesh.AddRenderer(face, faceMesh, faceMat, false);
                for (int e = -1; e <= 1; e += 2)
                {
                    var ge = Kit.Make("GooglyEye", m, new Vector3(x + e * 0.14f, -0.3f, 0.16f)); ge.transform.localScale = new Vector3(1f, 1f, 0.4f);
                    ProcMesh.AddRenderer(ge, eyeMesh, Kit.Mat(MatKey.White), false);
                    var pu = Kit.Make("GooglyPupil", m, new Vector3(x + e * 0.14f + ((i + e) % 3 - 1) * 0.04f, -0.34f, 0.2f)); pu.transform.localScale = new Vector3(0.45f, 0.45f, 0.2f);
                    ProcMesh.AddRenderer(pu, eyeMesh, Kit.Mat(MatKey.Black), false);
                }
            }
            ctx.Label(m, new Vector3(0f, -1.35f, 0.05f), Look(Vector3.back), "(back when we were allowed to hold hands on trust falls)", 0.17f, UIColors.Hex(0xE4E7EC), 8f);

            // propaganda posters (§24.4) on the side walls
            var posters = new[] { ("TRUST NO ONE.", "ESPECIALLY ON TRUST FALLS."), ("TWO'S A CROWD.", "THREE'S A CRIME."), ("LUNCH: EAT ALONE.", "DIGEST ALONE."), ("THINK FOR YOURSELF.", "ONLY YOURSELF.") };
            EnvKit.Poster(deco, new Vector3(-14.95f, 2.3f, 8f), Look(Vector3.left), posters[0].Item1, posters[0].Item2, new Vector2(1.4f, 1.9f));
            EnvKit.Poster(deco, new Vector3(-14.95f, 2.3f, 20.6f)   /* kitfix: clear of the z 22.5 column (smoke sign audit) */, Look(Vector3.left), posters[1].Item1, posters[1].Item2, new Vector2(1.4f, 1.9f));
            EnvKit.Poster(deco, new Vector3(14.95f, 2.3f, 8f), Look(Vector3.right), posters[2].Item1, posters[2].Item2, new Vector2(1.4f, 1.9f));
            EnvKit.Poster(deco, new Vector3(14.95f, 2.3f, 21.5f), Look(Vector3.right), posters[3].Item1, posters[3].Item2, new Vector2(1.4f, 1.9f));

            // marble columns, dead water coolers, cobwebs, cubicle ruins in the far corners
            for (int s = -1; s <= 1; s += 2)
            {
                EnvKit.Decor.Bank.MarbleColumn(deco, new Vector3(s * 14.4f, 0f, 9.5f), null, 6f, 1.0f, true, true);
                EnvKit.Decor.Bank.MarbleColumn(deco, new Vector3(s * 14.4f, 0f, 22.5f), null, 6f, 1.0f, true, true);
                EnvKit.Decor.Night.Cobweb(deco, new Vector3(s * 15f, 6f, 30f), null, 1.4f, s > 0);
                EnvKit.Decor.Night.Cobweb(deco, new Vector3(s * 15f, 6f, 0f), null, 1.2f, s < 0);
            }
            EnvKit.Decor.Night.WaterCooler(deco, new Vector3(-14.4f, 0f, 29.3f), Yaw(90f), true, true);
        }
    }
}
