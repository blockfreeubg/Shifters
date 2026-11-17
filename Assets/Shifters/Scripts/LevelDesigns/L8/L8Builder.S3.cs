// =====================================================================================
//  SHIFTERS - L8Builder.S3.cs   [S2b VESTIBULE (C2), S3 THE RESERVE: canisters, scare, the swap,
//  master valve -> C06, exit "GETAWAY TRAM - PLATFORM 1"]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L8Builder
    {
        void BuildS3(LevelBuildContext ctx)
        {
            var g = ctx.Section("S3_Reserve", Vector3.zero);
            var d = ctx.Decor;

            // ---- S2b VESTIBULE (C2)
            Floor(g, -5.2f, 5.2f, 86.2f, 94.2f, 0f, MatKey.Marble, true, "VestibuleFloor");
            Wall(g, -5.4f, DoorZ, -5.4f, 95.4f, 7f);
            Wall(g, 5.4f, DoorZ, 5.4f, 95.4f, 7f);
            Wall(g, -6.5f, DoorZ, -5.4f, DoorZ, 7f);
            Wall(g, 5.4f, DoorZ, 6.5f, DoorZ, 7f);
            ctx.Checkpoint(V(-0.6f, 0f, 89.6f), 0f);
            EnvKit.Decor.Bank.SafeDepositWall(d, V(-5.15f, 0f, 91.5f), Face(Vector3.right), 6, 7, 0.45f, true, "BOX 1: ME. BOX 2: ALSO ME.");
            Sign(d, V(5.15f, 3.4f, 91.2f), Vector3.left, "NOBODY TOUCHES\nMY STUFF", 2.0f, 1.0f, SignStyle.Danger);

            // ---- S3 THE RESERVE
            var c = ReserveCenter;
            Floor(g, -10.6f, 10.6f, 94.2f, 114.0f, 0f, MatKey.Marble, false, "ReserveFloor");
            RingWall(g, c, ReserveR + 0.2f, 24, 7f, m => Mathf.Abs(m + 90f) < 8f || Mathf.Abs(m - 90f) < 8f);
            ProcMesh.Part("ReserveInlay", d, L8Art.Inlay(4.2f, 0.07f), Kit.Mat(MatKey.Gold), c + V(0f, 0.012f, 0f), null, null, false);

            // 9 canisters around the wall (emissive, grey-immune)
            var names = new[] { "RED", "ORANGE", "YELLOW", "GREEN", "CYAN", "BLUE", "VIOLET", "MAUVE\n(NOBODY KNOWS)", "PLAID\n(EXPERIMENTAL)" };
            var cols = new[] { 0xFF3B3B, 0xFF8A1F, 0xFFE03A, 0x3BE36B, 0x3BE3E3, 0x3B6BFF, 0xA04BFF, 0xC98BB9, 0xE8E8E8 };
            var angles = new[] { -40f, -16f, 8f, 31f, 52f, 128f, 149f, 172f, 196f };
            for (int i = 0; i < 9; i++)
            {
                var p = OnCircle(c, 8.7f, angles[i]);
                var can = EnvKit.Decor.Bank.Canister(d, p, FaceAt(p, c), UIColors.Hex(cols[i]), names[i], 6f, 0.9f, 0.85f, true);
                if (i == 8) EnvKit.Decor.Bank.SetCanisterFill(can, 0.35f);
            }
            var sceneTrigger = StoryKit.ScriptTrigger(ctx.Gameplay, V(0f, 1.5f, 96.4f), V(5f, 3f, 2.4f), "ch07.scare");
            ctx.Mark("s3.scareTrigger", sceneTrigger);
            var scare = ScareKit.FlickerReveal(d, new[] { V(-6.4f, 0f, 109.5f), V(-5.2f, 0f, 106f), V(-4f, 0f, 102.4f), V(-3f, 0f, 99f) }, 0.5f, "ch07.scare");
            // polish 11-17: the punchline is built readable from +Z only, but the team enters the Reserve from -Z (it read
            // mirrored); turn it to the camera.
            var scareView = scare != null ? scare.GetComponent<FlickerRevealView>() : null;
            if (scareView != null && scareView.PunchText != null && scareView.PunchText.GetComponent<PuzzleBillboard>() == null)
                scareView.PunchText.gameObject.AddComponent<PuzzleBillboard>();
            StoryKit.HoloPoster(d, OnCircle(c, 9.3f, -65f), FaceAt(OnCircle(c, 9.3f, -65f), c), "COLOR IS A LUXURY.\nLUXURY IS FOR ME.", 2);
            Sign(d, OnCircle(c, 9.8f, -112f, 3.6f), (c - OnCircle(c, 1f, -112f)).normalized, "SMILE, YOU'RE\nON CAMERA", 1.9f, 0.9f);
            EnvKit.Decor.Bank.CoinPile(d, OnCircle(c, 6.5f, -120f), null, 0.9f, 0.45f, 11);
            EnvKit.Decor.Bank.CoinPile(d, OnCircle(c, 6.6f, -62f), null, 0.8f, 0.4f, 12);

            // ---- THE SWAP: pedestal (MinWeightAlarm 50 kg) + MASTER PRISM (60 kg) -> MASTER VALVE socket
            var prismRb = PuzzleKit.Crate(ctx.Props, V(0.35f, 0.6f, 100.35f), null, true);
            var bc = prismRb.GetComponent<BoxCollider>();
            if (bc != null) bc.size = V(0.8f, 1.2f, 0.8f);
            var prism = Reskin(prismRb, "master prism", L8Art.Prism(), MaterialLibrary.ImmuneColored(UIColors.Hex(0xFF5FD2), 0.9f, 1.6f), Vector3.zero);
            ctx.Label(prismRb.transform, V(0f, 1.0f, 0f), Quaternion.identity, "MASTER PRISM\n<size=60%>60 KG - HEAVY LIFTER</size>", 0.22f, Gold, 2f).gameObject.AddComponent<PuzzleBillboard>();
            ctx.Mark("s3.prism", prismRb.gameObject);
            var alarm = PuzzleKit.MinWeightAlarm(ctx.Puzzles, V(0f, 0f, 100f), null, 50f, prism);
            ctx.Mark("s3.pedestal", alarm);
            Solid(g, -1.65f, -1.0f, 99.0f, 101.0f, 0.3f, 0f, MatKey.Marble, "PedestalStep");
            ctx.MarkPoint("s3.pedestalTop", V(-0.45f, 0.6f, 99.55f));
            ctx.MarkPoint("s3.pedestalStep", V(-1.35f, 0.3f, 99.6f));
            Sign(d, V(0f, 0f, 97.9f) + V(0f, 1.6f, 0f), Vector3.back, "PRESSURE PEDESTAL\n<size=60%>LIFT ONLY WHILE SOMEONE\nPRICELESS STANDS ON IT</size>", 2.6f, 1.0f, SignStyle.Danger);   // polish 11-17: Warning = dark text, unreadable in the dim Reserve
            var socket = PuzzleKit.PropSocket(ctx.Puzzles, V(0f, 0f, 110f), Yaw(180f), prism);
            ctx.Mark("s3.socket", socket);
            ctx.MarkPoint("s3.socketDrop", V(0f, 0f, 108.6f), 0f);
            ctx.MarkPoint("story.valve", V(0f, 1.2f, 110f));
            Kit.SolidBox(d, "ValvePipe", V(0f, 3.6f, 110f), V(0.5f, 5.2f, 0.5f), MatKey.Gold, 0.05f);
            Sign(d, V(0f, 2.6f, 109.2f), Vector3.back, "MASTER VALVE", 2.0f, 0.5f);
            var valve = Node("s3.valve", SignalLogic.Latch, "ch07.valve", socket);
            valve.SolvedMessage = "COLOR: RELEASED";
            var alarmNode = Node("s3.alarm", SignalLogic.Any, "ch07.alarm", alarm);
            alarmNode.BeatEveryRise = true;
            // alarm laser ring around the socket (4 s alarm + 2 s = 6 s)
            var ring = new Behaviour[4];
            ring[0] = PuzzleKit.LaserBarrier(ctx.Hazards, V(0f, 0f, 108.1f), Quaternion.identity, 3.2f, 3);
            ring[1] = PuzzleKit.LaserBarrier(ctx.Hazards, V(0f, 0f, 111.9f), Quaternion.identity, 3.2f, 3);
            ring[2] = PuzzleKit.LaserBarrier(ctx.Hazards, V(-1.9f, 0f, 110f), Yaw(90f), 3.2f, 3);
            ring[3] = PuzzleKit.LaserBarrier(ctx.Hazards, V(1.9f, 0f, 110f), Yaw(90f), 3.2f, 3);
            var ringToggle = PuzzleKit.HazardToggle(ctx.Puzzles, ring, true, alarm);
            ringToggle.OffDelay = 2f;
            foreach (var r in ring) if (r is PuzzleHazard ph) ph.SetToggleEnabled(false);   // off until an alarm (runtime builds too)
            ctx.Mark("s3.alarmRing", ringToggle);
            ctx.Hint(V(0f, 1.5f, 101f), V(7f, 3f, 6f), "Someone light stays on the pedestal while the Brute carries the prism.", "It's a priceless artifact. So are you.");

            // vault glow light
            Panel(d, OnCircle(c, 9.95f, 90f + 25f, 5.4f), (c - OnCircle(c, 1f, 115f)).normalized, Gold, true, 3f, 16f, 2.4f, 0.4f);
            Panel(d, OnCircle(c, 9.95f, 65f, 5.4f), (c - OnCircle(c, 1f, 65f)).normalized, Gold, false, 3f, 16f, 2.4f, 0.4f);

            // ---- exit: north doors open on the valve -> GETAWAY TRAM - PLATFORM 1
            var exitDoor = PuzzleKit.Door(ctx.Puzzles, V(0f, 0f, 114.0f), Quaternion.identity, DoorMode.SlideSide, 4f, 4.2f, valve);
            ctx.Mark("s3.exitDoor", exitDoor);
            // kitfix 11-17: the platform end wall sits behind the exit cab (cab back = 117.6 + 4.5 = 122.1), so the cab's
            // BackSign faces open air; the GETAWAY TRAM sign moved from that wall to the left platform wall.
            Floor(g, -3f, 3f, 114.0f, 122.2f, 0f, MatKey.Marble, true, "PlatformFloor");
            Wall(g, -3.2f, 114.0f, -3.2f, 122.6f, 6f);
            Wall(g, 3.2f, 114.0f, 3.2f, 122.6f, 6f);
            Wall(g, -3.2f, 122.6f, 3.2f, 122.6f, 6f);
            ctx.Exit(V(0f, 0f, 117.6f), 0f);
            Sign(d, V(-2.95f, 3.0f, 115.9f), Vector3.right, "GETAWAY TRAM\n<size=60%>PLATFORM 1</size>", 2.8f, 1.0f, SignStyle.Exit);
            Sign(d, V(0f, 5.3f, 113.7f), Vector3.back, "EXIT  (EMPLOYEES OF THE MONTH ONLY)", 4.2f, 0.5f, SignStyle.Exit);
        }
    }
}
