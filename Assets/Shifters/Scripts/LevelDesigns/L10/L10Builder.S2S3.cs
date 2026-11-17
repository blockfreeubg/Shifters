// =====================================================================================
//  SHIFTERS - L10Builder.S2S3.cs   [LEVEL 10 - S2 Copy Room, S3 Executive Elevator + reveal]
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shifters
{
    public partial class L10Builder
    {
        // ================================================================== S2 THE COPY ROOM  x -8..8, z 50..66
        void BuildS2(LevelBuildContext ctx)
        {
            var g = ctx.Section("S2_CopyRoom", Vector3.zero);
            var d = ctx.Section("S2_Decor", Vector3.zero);
            Floor(g, -8f, 8f, 50f, 66f, 0f, MatKey.FloorAlt);
            WallZ(g, -8f, 50f, 66f);
            WallZ(g, 8f, 50f, 66f);
            WallX(g, 66f, -8f, 8f, -2.5f, 2.5f);          // door at z 66: always open
            PuzzleKit.LightPool(ctx.Puzzles, V(0f, 0f, 54f), 3f, false);
            ctx.Checkpoint(V(0f, 0f, 54.2f), 0f);

            // copiers along both side walls; the flash bars are toggled by the director
            var c1 = EnvKit.Decor.Night.CopierStack(d, V(-7.1f, 0f, 58.5f), EnvKit.FacingViewer(Vector3.right), 2, true);
            var c2 = EnvKit.Decor.Night.CopierStack(d, V(7.1f, 0f, 58.5f), EnvKit.FacingViewer(Vector3.left), 2, true);
            var flashes = new List<GameObject>();
            foreach (var c in new[] { c1, c2 })
            {
                var f = c.transform.Find("Flash");
                if (f != null) { flashes.Add(f.gameObject); f.gameObject.SetActive(false); }
            }
            _dir.CopierFlashes = flashes.ToArray();
            // desks (side lanes x +-3..+-6 stay walkable around them)
            EnvKit.Decor.DeskWithMonitors(d, V(-4.6f, 0f, 57.6f), Yaw(0f));
            EnvKit.Decor.DeskWithMonitors(d, V(4.6f, 0f, 57.6f), Yaw(0f));
            EnvKit.Decor.DeskWithMonitors(d, V(-4.6f, 0f, 60.4f), Yaw(180f));
            EnvKit.Decor.DeskWithMonitors(d, V(5.8f, 0f, 61.2f), Yaw(180f));
            // six dummies among the desks: visible ONLY during the first copier flash
            var six = Kit.Make("CopierDummies", d, Vector3.zero);
            Vector3[] sp = { V(-6.2f, 0f, 56.2f), V(-2.6f, 0f, 59f), V(-6.4f, 0f, 62.4f), V(2.8f, 0f, 56.4f), V(6.4f, 0f, 59.4f), V(3f, 0f, 63.4f) };
            foreach (var p in sp) DummyModel.Create(six.transform, p, Quaternion.LookRotation(V(0f, 0f, 54f) - p), false);
            six.SetActive(false);
            _dir.CopierDummies = six;
            // printed pages (HELP / HELP / TEAM PHOTO (UNAUTHORIZED)) pop out of the left copier
            string[] pages = { "HELP", "HELP", "TEAM PHOTO\n(UNAUTHORIZED)" };
            var pageGos = new GameObject[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                var pg = Kit.Make("Page_" + i, d, V(-5.9f + i * 0.05f, 1.35f + i * 0.5f, 57.9f + i * 0.4f), EnvKit.FacingViewer(V(1f, 0f, -0.6f)));
                Kit.BoxVisual(pg.transform, "Paper", V(0f, 0f, 0.01f), V(i == 2 ? 1.3f : 0.8f, i == 2 ? 0.9f : 1.0f, 0.01f), MatKey.SignWhite, 0f, null, false);
                ctx.Label(pg.transform, Vector3.zero, Quaternion.identity, pages[i], i == 2 ? 0.2f : 0.34f, UIColors.Hex(0x222222), 1.2f);
                pg.SetActive(false);
                pageGos[i] = pg;
            }
            _dir.CopierPages = pageGos;
            // drifts of old printouts + two dead desk lamps hanging over the room
            var rnd = new System.Random(909);
            for (int i = 0; i < 18; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (5.6f + (float)rnd.NextDouble() * 1.6f);
                float z = 55f + (float)rnd.NextDouble() * 9f;
                Kit.BoxVisual(d, "Printout", V(x, 0.012f, z), V(0.3f, 0.01f, 0.42f), MatKey.SignWhite, 0f, Yaw((float)rnd.NextDouble() * 360f), false);
            }
            HangingLamp(d, V(-3.5f, 4.0f, 59f), UIColors.Hex(0x9FB4FF), 2f);
            HangingLamp(d, V(3.5f, 4.0f, 59f), UIColors.Hex(0x9FB4FF), 2f);
            Sign(d, V(7.78f, 3.3f, 56f), Vector3.left, "HELP", 1.4f, 0.7f, SignStyle.Danger);
            StoryKit.ScriptTrigger(ctx.Gameplay, V(0f, 1.5f, 56.6f), V(16f, 3f, 2f), "l10.copier");
            _dir.CopyRoomCenter = V(0f, 0f, 58f);

            // the ringing phone (E = answer)
            var phone = ScareKit.PhoneCall(d, V(5f, 0f, 60f), "ch09.phone");
            ctx.Mark("s2.phone", phone);
            _dir.Phone = phone != null ? phone.GetComponentInChildren<PhoneCallView>(true) : null;

            // "THIS IS FINE" + the paper avalanche on the Brute
            var shelf = V(-5.2f, 0f, 64.9f);
            Kit.SolidBox(g, "PaperShelf", shelf + V(0f, 1.3f, 0f), V(3.2f, 2.6f, 1.2f), MatKey.MetalLight, 0.03f);
            for (int i = 0; i < 5; i++)
                Kit.BoxVisual(d, "Ream", shelf + V(-1.2f + i * 0.6f, 2.72f, 0f), V(0.45f, 0.24f, 0.32f), MatKey.SignWhite, 0.02f, null, false);
            Sign(d, V(-5.2f, 3.5f, 65.78f), Vector3.back, "THIS IS FINE", 2.6f, 0.7f, SignStyle.Info);
            HangingLamp(d, V(-5.2f, 4.3f, 65.1f), Warm, 2.6f);          // lit so the sign reads from the door
            _dir.ShelfPoint = shelf + V(0f, 2.8f, -0.6f);
            _dir.ShelfZoneCenter = V(-4.4f, 0f, 63.2f);
            ctx.MarkPoint("s2.shelf", V(-4.2f, 0f, 63.1f));
            ctx.MarkPoint("s2.lane.1", V(-1f, 0f, 56.5f));
            ctx.MarkPoint("s2.lane.2", V(-2.6f, 0f, 62.6f));
            ctx.MarkPoint("s2.lane.3", V(0f, 0f, 65.4f));
            ctx.MarkPoint("s2.phoneStand", V(4.1f, 0f, 59.0f));

            // the S3 intake: TEAM-BUILDING TIME-OUT cage in the copy room's NW corner + release lever in the checkpoint pool
            var tcell = EnemyKit.HoldingCell(ctx.Enemies, CellS3Pos, Yaw(90f), CellS3Id);
            var trel = PuzzleKit.Lever(ctx.Puzzles, V(-2.4f, 0f, 56.0f), Yaw(90f), 0, false, false, "");
            PuzzleKit.Wire(tcell, trel);
            ctx.Mark("s3.cellLever", trel.gameObject);
            ctx.MarkPoint("s3.cellLeverStand", V(-1.5f, 0f, 56.0f), -90f);
            Sign(d, V(-7.78f, 3.9f, 56.4f), Vector3.right, "TEAM-BUILDING TIME-OUT", 2.6f, 0.6f, SignStyle.Danger);
            FloorLabel(d, V(-1.5f, 0f, 57.3f), "RELEASE\n(PULL TO FORGIVE)", 0.26f, Teal);

            Sign(d, V(0f, 3.4f, 50.25f), Vector3.forward, "COPY ROOM\n(NO COPIES OF ME ALLOWED. THERE IS ONLY ONE.)", 4.6f, 1f, SignStyle.Info);
            Sign(d, V(-7.78f, 2.6f, 62.5f), Vector3.right, "TEAM PHOTO DAY:\nCANCELLED", 2.4f, 0.9f, SignStyle.Danger);
            Poster(d, V(7.78f, 2.4f, 63.4f), Vector3.left, "COPIES = TEAMWORK", "(not authorized)");
            StoryKit.HoloPoster(d, V(6.4f, 0f, 53.2f), EnvKit.FacingViewer(V(-1f, 0f, 0f)), StoryKitLines.Poster(9), 3);
            EnvKit.Decor.Night.Cobweb(d, V(7.8f, 4.5f, 65.8f), EnvKit.FacingViewer(V(-1f, 0f, -1f)), 1.2f, true);
            ctx.Hint(V(0f, 2f, 60f), V(16f, 4f, 12f), "Nothing in here can hurt you. Probably. Answer the phone if you're brave.", "It's just paper.");
        }

        // ================================================================== S3 THE EXECUTIVE ELEVATOR  lobby x -5..5, z 66..79
        void BuildS3(LevelBuildContext ctx)
        {
            var g = ctx.Section("S3_ElevatorLobby", Vector3.zero);
            var d = ctx.Section("S3_Decor", Vector3.zero);
            Floor(g, -5f, 5f, 66f, 79f, 0f, MatKey.Floor);
            Floor(g, -25f, -5f, 72f, 76f, 0f, MatKey.FloorAlt);
            Floor(g, 5f, 25f, 72f, 76f, 0f, MatKey.FloorAlt);
            Floor(g, -2.6f, 2.6f, 79f, 84f, 0f, MatKey.MetalDark);
            // walls: lobby sides open onto the corridors (z 72..76), corridors end at x +-25
            WallZ(g, -5f, 66f, 79f, 72f, 76f);
            WallZ(g, 5f, 66f, 79f, 72f, 76f);
            WallX(g, 72f, -25f, -5f);
            WallX(g, 76f, -25f, -5f);
            WallX(g, 72f, 5f, 25f);
            WallX(g, 76f, 5f, 25f);
            WallZ(g, -25f, 72f, 76f);
            WallZ(g, 25f, 72f, 76f);
            WallX(g, 79f, -5f, 5f, -2.6f, 2.6f);
            // the cab (walls + lit volume)
            WallZ(g, -2.6f, 79f, 84f);
            WallZ(g, 2.6f, 79f, 84f);
            WallX(g, 84f, -2.6f, 2.6f);
            ctx.Checkpoint(V(0f, 0f, 69.6f), 0f);

            // lever "CALL ELEVATOR (ONE MOMENT PLEASE)" -> HoldLatch(10 s) -> cab door
            var call = PuzzleKit.Lever(ctx.Puzzles, V(-3f, 0f, 77.8f), Yaw(180f), 0, true, false, "");
            ctx.Mark("s3.call", call.gameObject);
            ctx.MarkPoint("s3.callStand", V(-3f, 0f, 77.0f), 0f);
            var latch = PuzzleKit.HoldLatch(ctx.Puzzles, V(3.8f, 2.6f, 78.75f), EnvKit.FacingViewer(Vector3.back), call, 10f, "ELEVATOR ARRIVING", HoldLatchMode.HoldThenLatch);
            latch.SolvedMessage = "DING!";
            var cabDoor = PuzzleKit.Door(ctx.Puzzles, V(0f, 0f, 79f), Quaternion.identity, DoorMode.SlideSide, 4f, 3.2f, latch);
            ctx.Mark("s3.cabDoor", cabDoor.gameObject);
            var exitGo = ctx.Exit(V(0f, 0f, 79.6f), 0f);
            var lx = exitGo != null ? exitGo.GetComponentInChildren<LevelExit>() : null;
            if (lx != null) lx.SettleTime = 3.2f;   // hold the doors open: the POWER RESTORED crowd + paper-cut gag need their beat
            ctx.MarkPoint("s3.cabWait", V(0.6f, 0f, 77.4f));
            Sign(d, V(-3.2f, 3.0f, 78.78f), Vector3.back, "CALL ELEVATOR\n(ONE MOMENT PLEASE)", 2.4f, 0.9f, SignStyle.Info);
            // mounted on the cab door frame header (Tuning.DoorMinHeight 4.0 -> header y 4.0..4.5, front face z 78.55), not inside it
            Sign(d, V(0f, 4.25f, 78.53f), Vector3.back, "EXECUTIVE ELEVATOR - TOP FLOOR: ME", 4.8f, 0.44f, SignStyle.Exit);
            var cabVol = Kit.Make("CabLitVolume", ctx.Gameplay, V(0f, 2f, 81.5f));
            _dir.CabVolume = cabVol.transform;
            _dir.CabVolumeSize = V(5f, 4f, 5f);

            // FLOODLIGHT plate (100 kg, not latching): lights the whole E corridor while pressed
            var flood = PuzzleKit.PressurePlate(ctx.Puzzles, V(3.8f, 0f, 74f), Quaternion.identity, 100f, false, "");
            ctx.Mark("s3.flood", flood.gameObject);
            var fvol = Kit.Make("FloodLitVolume", ctx.Gameplay, V(14.2f, 2f, 74f));
            var fglow = Kit.Make("FloodGlow", d, Vector3.zero);
            for (int i = 0; i < 4; i++) HangingLamp(fglow.transform, V(8f + i * 5f, 4.0f, 74f), UIColors.Hex(0xFFF4D0), 3f);
            Kit.BoxVisual(fglow.transform, "Spill", V(15f, 0.015f, 74f), V(20f, 0.01f, 3.8f), MatKey.Glow, 0f, null, false);
            fglow.SetActive(false);
            Kit.BoxVisual(d, "FloodLamp", V(5.3f, 3.9f, 75.6f), V(0.8f, 0.5f, 0.5f), MatKey.MetalLight, 0.05f, null, false);
            _dir.FloodPlate = flood;
            _dir.FloodVolume = fvol.transform;
            _dir.FloodVolumeSize = V(23f, 4f, 4f);
            _dir.FloodGlow = fglow;
            Sign(d, V(4.78f, 2.7f, 70.6f), Vector3.left, "FLOODLIGHT\n100 KG TO HOLD", 2.2f, 0.9f, SignStyle.Info);
            ctx.MarkPoint("s3.corridorW", V(-16f, 1.1f, 74f));
            ctx.MarkPoint("s3.guardW", V(-3.2f, 0f, 74f), -90f);

            // the LOBBY POOL (real light #3): cuts out 1 s every 4 s (0.5 s buzz tell)
            var lobbySig = PuzzleKit.Signal(ctx.Puzzles, "LobbyPoolCut");
            var lobby = PuzzleKit.LightPool(ctx.Puzzles, V(0f, 0f, 76f), 3f, true, lobbySig);
            lobby.Invert = true;
            _dir.LobbyCut = lobbySig;
            _dir.LobbyPool = lobby;

            // the impatient dummies (dormant until the call)
            var dw2 = EnemyKit.Stalker(ctx.Enemies, V(-24f, 0f, 74f), Yaw(90f), CellS3Id, DW2Arena, S3Radius, StalkerSkin.TeamBuildingDummy);
            var de2 = EnemyKit.Stalker(ctx.Enemies, V(24f, 0f, 74f), Yaw(-90f), CellS3Id, DE2Arena, S3Radius, StalkerSkin.TeamBuildingDummy);
            dw2.Dormant = true; de2.Dormant = true;
            ctx.Mark("s3.dummyW", dw2.gameObject);
            ctx.Mark("s3.dummyE", de2.gameObject);
            PerspectiveKit.ThreatLabel(dw2.transform, "THREAT: {m} M");
            PerspectiveKit.ThreatLabel(de2.transform, "THREAT: {m} M");
            _dir.S3Dummies = new[] { dw2, de2 };
            _dir.CallLever = call;
            _dir.CabLatch = latch;

            Sign(d, V(-24.78f, 2.6f, 74f), Vector3.right, "HR: TEAM-BUILDING STORAGE", 2.4f, 0.8f, SignStyle.Info);
            Sign(d, V(24.78f, 2.6f, 74f), Vector3.left, "HR: TEAM-BUILDING OVERFLOW", 2.4f, 0.8f, SignStyle.Info);
            Sign(d, V(-4.78f, 2.8f, 69f), Vector3.right, "THE DUMMIES ARE IMPATIENT.\nLIGHT BOTH HALLWAYS.", 2.6f, 0.9f, SignStyle.Warning);
            StoryKit.HoloPoster(d, V(-4.2f, 0f, 67.2f), EnvKit.FacingViewer(V(1f, 0f, 1f)), StoryKitLines.Poster(11), 3);
            ctx.Hint(V(0f, 2f, 73f), V(10f, 4f, 12f), "Someone heavy holds the floodlight plate; you hold the other corridor with your flashlight. Hugged? Your friend waits in the copy-room TIME-OUT cage: pull its lever.", "Light both hallways.");

            // POWER RESTORED: forty dummies behind the team (one merged static mesh), hidden until the reveal
            var crowd = BuildCrowd(d, 40);
            crowd.SetActive(false);
            _dir.RevealCrowd = crowd;
            // the cardboard Max hand that slips between the closing doors
            var hand = Kit.Make("CardboardMaxHand", d, V(2.9f, 1.5f, 79.3f));
            Kit.BoxVisual(hand.transform, "Palm", Vector3.zero, V(0.6f, 0.7f, 0.04f), MatKey.Cardboard, 0.02f, null, false);
            for (int i = 0; i < 4; i++)
                Kit.BoxVisual(hand.transform, "Finger", V(-0.22f + i * 0.15f, 0.55f, 0f), V(0.11f, 0.4f, 0.04f), MatKey.Cardboard, 0.02f, null, false);
            Kit.BoxVisual(hand.transform, "Cuff", V(0f, -0.5f, 0f), V(0.7f, 0.3f, 0.05f), MatKey.Gold, 0.02f, null, false);
            hand.SetActive(false);
            _dir.MaxHand = hand;
        }

        /// <summary>count Team-Building Dummies in rows behind the lobby (z 67..73), merged into one mesh per material,
        /// plus a few "WELCOME TO THE TEAM!" banners.</summary>
        GameObject BuildCrowd(Transform parent, int count)
        {
            var root = Kit.Make("RevealCrowd", parent, Vector3.zero);
            var tmp = Kit.Make("tmp", null, Vector3.zero);
            int perRow = 8;
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow, col = i % perRow;
                float x = -4.2f + col * 1.2f + (row % 2) * 0.5f;
                float z = 72.6f - row * 1.25f;
                if (row >= 3) { x = (col < 4 ? -8f - col * 1.6f : 8f + (col - 4) * 1.6f); z = 74f + ((row % 2) - 0.5f) * 1.6f; }
                var p = V(x, 0f, z);
                DummyModel.Create(tmp.transform, p, Quaternion.LookRotation(V(0f, 0f, 79f) - p), false);
            }
            // merge every renderer by material
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var mf in tmp.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || !mr.enabled) continue;
                var mat = mr.sharedMaterial;
                if (mat == null) continue;
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<CombineInstance>();
                for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++)
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sm, transform = mf.transform.localToWorldMatrix });
            }
            int n = 0;
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "L10_RevealCrowd_" + (n++), indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var part = Kit.Make("Crowd_" + n, root.transform, Vector3.zero);
                ProcMesh.AddRenderer(part, mesh, kv.Key, false);
            }
            Kit.SafeDestroy(tmp);
            // banners held up by the front row
            for (int i = 0; i < 3; i++)
            {
                var b = Kit.Make("WelcomeBanner", root.transform, V(-3.2f + i * 3.2f, 2.35f, 73.2f), EnvKit.FacingViewer(Vector3.forward));
                Kit.BoxVisual(b.transform, "Cloth", V(0f, 0f, 0.02f), V(2.8f, 0.7f, 0.03f), MatKey.SignWhite, 0f, null, false);
                _ctx.Label(b.transform, Vector3.zero, Quaternion.identity, "WELCOME TO THE TEAM!", 0.3f, UIColors.Hex(0xE0306A), 2.7f);
            }
            return root;
        }
    }
}
