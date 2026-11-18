// =====================================================================================
//  SHIFTERS - L8Builder.S2.cs   [S2 THE VAULT DOOR ROTUNDA: Lock A tumblers, Lock B three-key turn,
//  Lock C shot code, the three-lock vault door]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L8Builder
    {
        void BuildS2(LevelBuildContext ctx)
        {
            var g = ctx.Section("S2_Rotunda", Vector3.zero);
            var d = ctx.Decor;
            var c = RotundaCenter;
            Floor(g, -16.5f, 16.5f, 54.0f, 86.2f, 0f, MatKey.Marble, false, "RotundaFloor");
            RingWall(g, c, RotundaR + 0.2f, 32, 7f, m => Mathf.Abs(m + 90f) < 6f || (m > 56f && m < 124f));
            Wall(g, -2.4f, 54.6f, -3.2f, 54.08f, 5f);
            Wall(g, 2.4f, 54.6f, 3.2f, 54.08f, 5f);
            Wall(g, -9.05f, 83.45f, -6.5f, 85.2f, 11.2f);
            Wall(g, 9.05f, 83.45f, 6.5f, 85.2f, 11.2f);
            Wall(g, -6.5f, DoorZ, -2.6f, DoorZ, 11.2f);
            Wall(g, 2.6f, DoorZ, 6.5f, DoorZ, 11.2f);
            ctx.Block(g, V(0f, 8.6f, DoorZ), V(5.4f, 5.2f, 0.4f), MatKey.Wall, "DoorLintel");
            ProcMesh.Part("Inlay6", d, L8Art.Inlay(6f, 0.07f), Kit.Mat(MatKey.Gold), c + V(0f, 0.012f, 0f), null, null, false);
            ProcMesh.Part("Inlay11", d, L8Art.Inlay(11.5f, 0.07f), Kit.Mat(MatKey.Gold), c + V(0f, 0.012f, 0f), null, null, false);
            foreach (var a in new[] { 0f, -30f, 150f, 180f, -45f + 0.01f })
                EnvKit.Decor.Bank.MarbleColumn(g, OnCircle(c, 14.9f, a), null, 7f, 1.1f, true, true);

            // ================= LOCK A - THE TUMBLERS (west)
            var pallet = PuzzleKit.HeavyBlock(ctx.Props, V(-8.5f, 0f, 59.5f));
            for (int i = 0; i < 3; i++)
                Kit.BoxVisual(pallet.transform, "Bullion", V(-0.45f + i * 0.45f, 0.92f, 0f), V(0.36f, 0.22f, 1.1f), MatKey.Gold, 0.03f);
            ctx.Label(pallet.transform, V(0f, 1.25f, 0f), Quaternion.identity, "GOLD BULLION", 0.22f, Gold, 1.6f).gameObject.AddComponent<PuzzleBillboard>();
            ctx.Mark("s2.pallet", pallet);
            var pI = PuzzleKit.WeightRangePlate(ctx.Puzzles, V(-8.5f, 0f, 66f), null, 150f, 180f);
            var pII = PuzzleKit.WeightRangePlate(ctx.Puzzles, V(-11.5f, 0f, 70f), null, 20f, 35f);
            var pIII = PuzzleKit.WeightRangePlate(ctx.Puzzles, V(-9f, 0f, 74f), null, 55f, 80f);
            ctx.Mark("s2.plateI", pI); ctx.Mark("s2.plateII", pII); ctx.Mark("s2.plateIII", pIII);
            ctx.MarkPoint("s2.plateI.center", V(-8.5f, 0f, 66f));
            ctx.MarkPoint("s2.plateII.drop", V(-11.5f, 0f, 70f));
            ctx.MarkPoint("s2.plateIII.center", V(-9f, 0f, 74f));
            FloorLabel(d, V(-6.9f, 0f, 66f), "I", 0.9f, Gold);
            FloorLabel(d, V(-11.5f, 0f, 68.3f), "II", 0.9f, Gold);
            FloorLabel(d, V(-7.4f, 0f, 74f), "III", 0.9f, Gold);
            var crateA = PuzzleKit.Crate(ctx.Props, V(-12.5f, 0f, 63f));
            var crateB = PuzzleKit.Crate(ctx.Props, V(-12.5f, 0f, 64.5f));
            var barrel = PuzzleKit.Barrel(ctx.Props, V(-13f, 0f, 66.5f));
            ctx.Mark("s2.crateA", crateA.gameObject); ctx.Mark("s2.crateB", crateB.gameObject); ctx.Mark("s2.barrel", barrel.gameObject);
            var tumblers = Node("s2.tumblers", SignalLogic.All, null, pI, pII, pIII);
            var holdPos = V(-5.4f, 0f, 70f);
            var holdA = PuzzleKit.HoldLatch(ctx.Puzzles, holdPos, FaceAt(holdPos, V(-10f, 0f, 70f)), tumblers, 2f, "TUMBLERS", HoldLatchMode.HoldThenLatch);
            ctx.Mark("s2.holdA", holdA);
            var lockA = Node("s2.lockA", SignalLogic.Latch, "ch07.lockA", holdA);
            lockA.SolvedMessage = "TUMBLERS: BALANCED";
            var manPos = OnCircle(c, 15.75f, 187f, 2.7f);
            Sign(d, manPos, -(manPos - V(0f, manPos.y, c.z)).normalized, "TUMBLER MANUAL\n<size=62%>I: ONE PALLET   II: ONE BOX\nIII: ONE INTERN (NOT THE BIG ONE)</size>", 3.4f, 1.4f);
            EnvKit.Decor.Bank.CoinPile(d, V(-10.8f, 0f, 57.6f), null, 0.8f, 0.4f, 9);
            ctx.Hint(V(-10f, 1.5f, 66f), V(8f, 3f, 14f), "Pallet on I, one box on II, one light intern on III.", "Count the kilos.");

            // ================= LOCK B - THE THREE-KEY TURN
            var keyPads = new[] { V(6f, 0f, 59f), V(-3f, 0f, 81f), V(10f, 3f, 73.5f) };
            var keyYaw = new[] { 180f, 180f, -90f };
            var keys = new SignalSource[3];
            for (int i = 0; i < 3; i++)
            {
                string id = "s2.K" + (i + 1);
                var pad = PuzzleKit.ZoneTrigger(ctx.Puzzles, keyPads[i], null, V(3f, 2.5f, 3f), ZoneFilter.AnyCharacter, 1, true);
                var sw = PuzzleKit.PushButton(ctx.Puzzles, keyPads[i], Yaw(keyYaw[i]), KeySeconds);
                ctx.Mark(id + ".pad", pad); ctx.Mark(id + ".key", sw);
                ctx.MarkPoint(id, keyPads[i] + Yaw(keyYaw[i]) * V(0f, 0f, 0.95f), keyYaw[i] + 180f);
                var k = Node(id + ".on", SignalLogic.All, null, pad, sw);
                keys[i] = k;
                PuzzleKit.IndicatorLight(ctx.Puzzles, keyPads[i] + Yaw(keyYaw[i]) * V(1.25f, 0f, -1.25f), null, false, k);
                FloorLabel(d, keyPads[i] + Yaw(keyYaw[i]) * V(0f, 0f, 1.95f), "KEY " + (i + 1), 0.5f, Gold, keyYaw[i] + 180f);
            }
            var lockB = Node("s2.lockB", SignalLogic.Latch, "ch07.lockB", keys);
            lockB.SolvedMessage = "KEYS: TURNED";
            // K3 access: ramp + 3.0 m lip, 5.4 m gap (turbo / living bridge) or a Brute throw from the south
            ctx.Ramp(g, V(0.8f, 0f, 59f), V(0.8f, 3f, 71f), 1.6f, 0.4f, MatKey.FloorAlt, "LipRamp");
            Solid(g, -1.5f, 1.6f, 71f, 74f, 3f, 0f, MatKey.FloorAlt, "RampLip");
            Solid(g, 7f, 12.5f, 70f, 77f, 3f, 0f, MatKey.Marble, "TimeLockGallery");
            ctx.Block(g, V(9.75f, 3.75f, 77.15f), V(5.5f, 1.5f, 0.3f), MatKey.Wall, "GalleryBackWall");
            PuzzleVisuals.StripedBox(d, "LipEdge", V(1.45f, 3.01f, 72.5f), V(0.3f, 0.02f, 3f), 2, 0.35f, false);
            PuzzleVisuals.StripedBox(d, "GalleryEdge", V(7.15f, 3.01f, 73.5f), V(0.3f, 0.02f, 7f), 2, 0.35f, false);
            ctx.MarkPoint("s2.rampBottom", V(0.8f, 0f, 57.6f));
            ctx.MarkPoint("s2.rampTop", V(0.8f, 3f, 71.8f));
            ctx.MarkPoint("s2.rampLip", V(0f, 3f, 72.5f));
            ctx.MarkPoint("s2.lipEdge", V(1.45f, 3f, 72.5f), 90f);
            ctx.MarkPoint("s2.galleryLanding", V(8.3f, 3f, 72.5f), 90f);
            ctx.MarkPoint("s2.throwSpot", V(10f, 0f, 64.5f), 0f);
            Disc(d, V(10f, 0f, 64.5f), 0.7f, MatKey.HazardYellow, "ThrowSpot");
            FloorLabel(d, V(10f, 0f, 63.3f), "THROW FROM HERE", 0.32f, Gold, 0f, 3f);
            Sign(d, V(9.75f, 3.95f, 76.98f), Vector3.back, "TIME-LOCK GALLERY  -  KEY 3", 4.2f, 0.55f);
            FloorLabel(d, V(0f, 3f, 73.4f), "TURBO JUMP OR BRIDGE  >>", 0.28f, Teal, -90f, 3f);
            ctx.Tutorial(V(0f, 1.5f, 57.8f), V(9f, 3f, 6f), "ch07_keys", "THE THREE-KEY TURN",
                         "Like in the movies: all three keys within 5 seconds. You are three people. Act like it.", new[] { "G", "E", "RShift" }, true);
            ctx.Hint(V(5f, 1.5f, 62f), V(8f, 3f, 7f), "Park everyone on a key pad, then {kb:press {Interact}, SHIFT, {Interact}, SHIFT, {Interact}.}{pad:press {Interact} on each pad in turn, SHIFTing between them with {Switcher}.}", "Three keys, three people.");
            var teamPos = OnCircle(c, 15.75f, -62f, 3.0f);
            var toward = -(teamPos - V(0f, teamPos.y, c.z)).normalized;
            Sign(d, teamPos, toward, "THE COMBINATION IS A TEAM EFFORT", 3.6f, 0.8f);
            Kit.BoxVisual(d, "Strike", teamPos + toward * 0.07f, V(3.5f, 0.14f, 0.03f), MatKey.WarningRed, 0f, Face(toward) * Quaternion.Euler(0f, 0f, 6f), false);
            ctx.Label(d, teamPos + V(0f, -0.75f, 0f) + toward * 0.07f, Face(toward), "(NOT ANYMORE.  - MAX)", 0.3f, VaultRed, 3.4f);

            // ================= LOCK C - THE CODE (dials on the door, podium, portrait clue)
            Solid(g, -5.5f, -3.5f, 71f, 73f, 1.0f, 0f, MatKey.Marble, "TellersPodium");
            Solid(g, -5.5f, -3.5f, 70.4f, 71f, 0.67f, 0f, MatKey.Marble, "PodiumStep2");
            Solid(g, -5.5f, -3.5f, 69.8f, 70.4f, 0.34f, 0f, MatKey.Marble, "PodiumStep1");
            Kit.BoxVisual(d, "PodiumTrim", V(-4.5f, 1.0f, 73f), V(2.04f, 0.08f, 0.06f), MatKey.Gold, 0.01f);
            ctx.MarkPoint("s2.podium", V(-4.5f, 1.0f, 72.3f), 0f);
            FloorLabel(d, V(-4.5f, 1.0f, 71.4f), "TELLER'S PODIUM", 0.22f, Gold, 0f, 1.9f);

            var vault = EnvKit.Decor.Bank.VaultDoorDecor(ctx.Puzzles, V(0f, DoorCenterY, DoorZ - 0.35f), Face(Vector3.back), DoorDiameter, 0f, true, "");
            var frame = vault.transform.Find("Frame");
            if (frame != null) Kit.SafeDestroy(frame.gameObject);
            ProcMesh.Part("VaultRing", vault.transform, L8Art.Ring(4.3f, 0.42f), Kit.Mat(MatKey.Gold), V(0f, 0f, 0.05f), null, null, true);
            var hinge = vault.transform.Find("Hinge");
            var doorT = vault.transform.Find("Hinge/Door");
            var wheel = vault.transform.Find("Hinge/Door/Wheel");
            var symbols = new[] { L8Symbol.Drum, L8Symbol.Crown, L8Symbol.Mustache, L8Symbol.Monocle };
            var dials = new ShootTarget[4];
            var symMat = MaterialLibrary.ImmuneColored(Gold, 0.6f, 0.9f);
            for (int i = 0; i < 4; i++)
            {
                float wx = -3f + 2f * i;
                var lp = V(-wx, 2.0f, 0.44f);
                var st = PuzzleKit.ShootTarget(doorT != null ? doorT : vault.transform, lp, Quaternion.identity, ShootTargetMode.Momentary, 2f, false, "");
                st.transform.localScale = Vector3.one * 1.2f;
                if (st.Disc != null) ProcMesh.Part("Symbol", st.Disc, L8Art.Symbol(symbols[i], 0.6f, 0.08f), symMat, V(0f, 0.02f, 0.13f), null, null, false);
                ctx.Label(st.transform, V(0f, -0.92f, 0.04f), Yaw(180f), L8Art.Name(symbols[i]), 0.22f, Cream, 1.4f);
                dials[i] = st;
                ctx.Mark("s2.dial." + L8Art.Name(symbols[i]), st);
            }
            var seqPos = V(2.9f, 0f, 82.3f);
            var seq = PuzzleKit.SequenceLock(ctx.Puzzles, seqPos, Yaw(180f), new SignalSource[] { dials[1], dials[3], dials[2], dials[0] }, true, "");
            ctx.Mark("s2.sequence", seq);
            var lockC = Node("s2.lockC", SignalLogic.Latch, "ch07.lockC", seq);
            lockC.SolvedMessage = "CODE: CORRECT";
            ctx.Hint(V(-4.5f, 1.5f, 75.5f), V(5f, 3f, 5f), "Crown, monocle, mustache, drum.", "Top to bottom.");

            // Max's portrait (west wall): the clue, top to bottom
            var porPos = OnCircle(c, 15.72f, 163.125f, 3.9f);
            var porToward = -(porPos - V(0f, porPos.y, c.z)).normalized;
            var portrait = Kit.Make("MaxPortrait", d, porPos, Face(porToward)).transform;
            Kit.BoxVisual(portrait, "Frame", Vector3.zero, V(2.6f, 4.4f, 0.12f), MatKey.Gold, 0.04f);
            Kit.BoxVisual(portrait, "Canvas", V(0f, 0f, -0.07f), V(2.25f, 4.05f, 0.03f), MatKey.Velvet, 0.01f, null, false);
            var porMat = MaterialLibrary.ImmuneColored(Gold, 0.6f, 0.6f);
            var order = new[] { L8Symbol.Crown, L8Symbol.Monocle, L8Symbol.Mustache, L8Symbol.Drum };
            for (int i = 0; i < 4; i++)
                ProcMesh.Part("Feature" + i, portrait, L8Art.Symbol(order[i], 0.8f, 0.08f), porMat, V(0f, 1.45f - i * 0.95f, -0.12f), null, null, false);
            ctx.Label(portrait, V(0f, 2.5f, -0.08f), Quaternion.identity, "MAXIMILIAN ME", 0.3f, Gold, 2.6f);
            ctx.Label(portrait, V(0f, -2.55f, -0.08f), Quaternion.identity, "\"MY BEST FEATURES,\nTOP TO BOTTOM.\"", 0.24f, Cream, 2.8f);

            // lock lamps over the door: TUMBLERS / KEYS / CODE
            var lampHolder = Kit.Make("LockLamps", d, V(0f, 0f, DoorZ - 0.24f), Yaw(180f)).transform;
            var locks = new[] { lockA, lockB, lockC };
            var lampNames = new[] { "TUMBLERS", "KEYS", "CODE" };
            for (int i = 0; i < 3; i++)
            {
                float wx = -2.1f + 2.1f * i;
                Kit.BoxVisual(lampHolder, "LampPlate", V(-wx, 9.3f, -0.02f), V(1.9f, 1.2f, 0.06f), MatKey.MetalDark, 0.02f);
                var lamp = PuzzleVisuals.Lamp(lampHolder, "LockLamp" + i, V(-wx, 9.5f, 0.03f), Quaternion.identity, 0.42f, PuzzleLampColor.Red);
                locks[i].Lamps = new[] { lamp };
                ctx.Label(lampHolder, V(-wx, 8.9f, 0.03f), Yaw(180f), lampNames[i], 0.28f, Cream, 1.8f);
            }
            Sign(d, V(0f, 10.55f, DoorZ - 0.25f), Vector3.back, "THE FEDERAL COLOR RESERVE  -  VAULT 1 OF 1", 6.4f, 0.75f);

            // the door itself: AND(A, B, C) -> swings into the vestibule with a gold glow
            var glow = Kit.Make("VaultGlow", d, V(0f, 0f, 0f));
            PuzzleVisuals.Cyl(glow.transform, "GlowPool", V(0f, 0.015f, DoorZ + 3.6f), 2.6f, 0.02f, MatKey.Gold, null, 32, false);
            Panel(glow.transform, V(0f, 6.4f, DoorZ + 0.6f), Vector3.back, Gold, true, 3f, 16f, 2.4f, 0.4f);
            glow.SetActive(false);
            var vd = vault.AddComponent<L8VaultDoor>();
            vd.enabled = false;   // OnEnable subscribes: re-enable after wiring (runtime builds)
            vd.Inputs = new SignalSource[] { lockA, lockB, lockC };
            vd.Logic = SignalLogic.All;
            vd.Hinge = hinge; vd.Wheel = wheel; vd.Glow = glow;
            vd.enabled = true;
            ctx.Mark("s2.vaultDoor", vd);
            PuzzleKit.NoToolZone(ctx.Puzzles, V(0f, 3f, DoorZ + 0.6f), V(10.4f, 6f, 0.8f), ToolMask.SwapOMatic);

            // story dressing
            var tauntPos = V(-3.2f, 0f, 58.2f);
            StoryKit.HologramTauntSpot(d, tauntPos, FaceAt(tauntPos, V(0f, 0f, 55f)), "Three locks. Three people? Adorable. I open it ALONE. With MONEY.");
            ctx.MarkPoint("story.taunt.1", tauntPos);
            StoryKit.HoloPoster(d, OnCircle(c, 14.6f, -15f), FaceAt(OnCircle(c, 14.6f, -15f), c), "VAULT 1 OF 1\nBECAUSE THERE IS\nONLY ONE OF ME.", 2);
            Panel(d, OnCircle(c, 15.75f, 0f, 5.2f), Vector3.left, Cream, true, 2.6f, 18f, 2.4f, 0.4f);
            Panel(d, OnCircle(c, 15.75f, 180f, 5.6f), Vector3.right, Cream, false, 2.4f, 14f, 2.4f, 0.4f);
            Panel(d, OnCircle(c, 15.75f, -112f, 5.2f), (c - OnCircle(c, 1f, -112f)).normalized, Gold, false);
            Panel(d, OnCircle(c, 15.75f, -68f, 5.2f), (c - OnCircle(c, 1f, -68f)).normalized, Gold, false);
        }
    }
}
