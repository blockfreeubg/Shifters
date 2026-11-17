// =====================================================================================
//  SHIFTERS - LevelKit.cs   [owned by the LEVEL agent]
//  Factories for level-flow objects (Core/Kit.cs conventions; work at runtime and in edit mode).
//  Positions are LOCAL to parent (null = world). Every object faces its local +Z.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class LevelKit
    {
        public const float SpawnSpacing = 1.6f;

        /// <summary>Three spawn transforms in a row (local X: -1.6, 0, +1.6), facing rot's +Z.</summary>
        public static Transform[] SpawnPoints(Transform parent, Vector3 pos, Quaternion rot)
        {
            var root = Kit.Make("SpawnPoints", parent, pos, rot);
            var arr = new Transform[3];
            for (int i = 0; i < 3; i++) arr[i] = Kit.Make("Spawn_" + i, root.transform, new Vector3((i - 1) * SpawnSpacing, 0f, 0f)).transform;
            return arr;
        }

        /// <summary>Creates and configures the scene's LevelManager (levelIndex into LevelCatalog, 3 start spawns).</summary>
        public static global::Shifters.LevelManager LevelManager(Transform parent, int levelIndex, Transform[] spawnPoints)
        {
            var go = Kit.Make("LevelManager", parent, Vector3.zero);
            var lm = go.AddComponent<global::Shifters.LevelManager>();
            lm.LevelIndex = levelIndex;
            lm.SpawnPoints = spawnPoints ?? new Transform[3];
            return lm;
        }

        // ------------------------------------------------------------------ checkpoint
        /// <summary>Checkpoint at pos (floor level). Spawn row across local X, team faces +Z, beacon pole on the left.</summary>
        public static global::Shifters.Checkpoint Checkpoint(Transform parent, Vector3 pos, Quaternion rot, int index)
        {
            var root = Kit.Make("Checkpoint_" + index, parent, pos, rot);
            var cp = root.AddComponent<global::Shifters.Checkpoint>();
            cp.Index = index;
            var t = root.transform;

            // ground: dark base disc + glowing status ring
            ProcMesh.AddRenderer(Kit.Make("Base", t, new Vector3(0f, 0.03f, 0f)), ProcMesh.Cached("lk_cp_base", () => ProcMesh.Cylinder(2.9f, 0.06f, 32)), Kit.Mat(MatKey.MetalDark));
            var ring = ProcMesh.AddRenderer(Kit.Make("Ring", t, new Vector3(0f, 0.07f, 0f)), ProcMesh.Cached("lk_cp_ring", () => ProcMesh.Torus(2.7f, 0.07f, 40, 6)), Kit.Mat(MatKey.LightBlue), false);

            // beacon pole + lamp + holo flag
            var pole = Kit.Make("Beacon", t, new Vector3(-3.3f, 0f, 0.4f));
            ProcMesh.AddRenderer(Kit.Make("Foot", pole.transform, new Vector3(0f, 0.12f, 0f)), ProcMesh.Cached("lk_cp_foot", () => ProcMesh.Cylinder(0.32f, 0.24f, 16)), Kit.Mat(MatKey.MetalDark));
            var poleR = Kit.Make("Pole", pole.transform, new Vector3(0f, 1.75f, 0f));
            ProcMesh.AddRenderer(poleR, ProcMesh.Cached("lk_cp_pole", () => ProcMesh.Cylinder(0.075f, 3.5f, 12, true)), Kit.Mat(MatKey.MetalLight));
            var col = poleR.AddComponent<CapsuleCollider>();
            col.radius = 0.12f; col.height = 3.5f;
            var lamp = ProcMesh.AddRenderer(Kit.Make("Lamp", pole.transform, new Vector3(0f, 3.62f, 0f)), ProcMesh.Cached("lk_cp_lamp", () => ProcMesh.Sphere(0.2f, 14, 10)), Kit.Mat(MatKey.LightBlue), false);
            var flag = Kit.Make("Flag", pole.transform, new Vector3(0f, cp.FlagLowY, 0f));
            var flagR = ProcMesh.AddRenderer(Kit.Make("Cloth", flag.transform, new Vector3(0.5f, 0f, 0f)), ProcMesh.CachedBox(new Vector3(0.9f, 0.55f, 0.03f), 0.01f), Kit.Mat(MatKey.LightBlue), false);
            ProcMesh.AddRenderer(Kit.Make("Holo", flag.transform, new Vector3(0.5f, 0f, 0f)), ProcMesh.CachedBox(new Vector3(1.0f, 0.65f, 0.06f), 0.02f), Kit.Mat(MatKey.Hologram), false);
            string label = global::Shifters.Checkpoint.DefaultNumber(index).ToString();
            var num = PerspectiveFonts.MakeText(flag.transform, "Number", label, 0.42f, UIColors.Background, new Vector3(0.5f, 0f, -0.03f), Quaternion.identity, true);
            num.fontStyle = FontStyles.Bold;
            var numBack = PerspectiveFonts.MakeText(flag.transform, "NumberBack", label, 0.42f, UIColors.Background, new Vector3(0.5f, 0f, 0.03f), Quaternion.Euler(0f, 180f, 0f), true);
            numBack.fontStyle = FontStyles.Bold;
            cp.NumberTexts = new TMP_Text[] { num, numBack };
            var lightGo = Kit.Make("BeaconLight", pole.transform, new Vector3(0f, 3.4f, 0f));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.intensity = 0.6f;
            light.color = UIColors.Info;
            light.shadows = LightShadows.None;
            light.enabled = false; // lit only while this is the current checkpoint (CheckpointManager)

            cp.StatusRenderers = new Renderer[] { ring, lamp, flagR };
            cp.Flag = flag.transform;
            cp.Lamp = light;

            // spawn points (slot order) inside the ring
            cp.SpawnPoints = new Transform[3];
            for (int i = 0; i < 3; i++) cp.SpawnPoints[i] = Kit.Make("Spawn_" + i, t, new Vector3((i - 1) * SpawnSpacing, 0.05f, 0f)).transform;
            return cp;
        }

        // ------------------------------------------------------------------ exit
        /// <summary>Elevator exit: opening (3.2 x 4.2 m) at local z=0 facing -Z, cab extends to +Z (4 m deep).
        /// Walk in along +Z. Floor top at pos.y + 0.1 (small step).</summary>
        public static LevelExit Exit(Transform parent, Vector3 pos, Quaternion rot)
        {
            var root = Kit.Make("LevelExit", parent, pos, rot);
            var exit = root.AddComponent<LevelExit>();
            var t = root.transform;
            const float W = 4.4f, D = 4.2f, H = 4.6f, Wall = 0.3f;

            Kit.SolidBox(t, "CabFloor", new Vector3(0f, 0.05f, D * 0.5f), new Vector3(W, 0.1f, D), MatKey.MetalLight, 0.02f);
            Kit.SolidBox(t, "CabBack", new Vector3(0f, H * 0.5f, D + Wall * 0.5f), new Vector3(W + Wall * 2f, H, Wall), MatKey.MetalDark);
            Kit.SolidBox(t, "CabLeft", new Vector3(-W * 0.5f - Wall * 0.5f, H * 0.5f, D * 0.5f), new Vector3(Wall, H, D), MatKey.MetalDark);
            Kit.SolidBox(t, "CabRight", new Vector3(W * 0.5f + Wall * 0.5f, H * 0.5f, D * 0.5f), new Vector3(Wall, H, D), MatKey.MetalDark);
            Kit.SolidBox(t, "CabRoof", new Vector3(0f, H + 0.15f, D * 0.5f), new Vector3(W + Wall * 2f, 0.3f, D + Wall), MatKey.MetalDark);
            // door frame (orange trim) around the 3.2 m opening
            Kit.SolidBox(t, "FrameL", new Vector3(-1.9f, H * 0.5f, -0.15f), new Vector3(0.6f, H, 0.3f), MatKey.SafetyOrange);
            Kit.SolidBox(t, "FrameR", new Vector3(1.9f, H * 0.5f, -0.15f), new Vector3(0.6f, H, 0.3f), MatKey.SafetyOrange);
            Kit.SolidBox(t, "FrameTop", new Vector3(0f, 4.2f + (H - 4.2f) * 0.5f + 0.15f, -0.15f), new Vector3(4.4f, H - 4.2f + 0.3f, 0.3f), MatKey.SafetyOrange);
            Kit.BoxVisual(t, "Threshold", new Vector3(0f, 0.11f, 0f), new Vector3(3.2f, 0.03f, 0.25f), MatKey.HazardYellow, 0f);

            // sliding doors (visual only; open at start, close on completion)
            var dl = Kit.BoxVisual(t, "DoorL", new Vector3(-0.8f - exit.DoorOpenOffset, 2.1f, 0.12f), new Vector3(1.6f, 4.1f, 0.1f), MatKey.MetalLight, 0.02f);
            var dr = Kit.BoxVisual(t, "DoorR", new Vector3(0.8f + exit.DoorOpenOffset, 2.1f, 0.12f), new Vector3(1.6f, 4.1f, 0.1f), MatKey.MetalLight, 0.02f);
            Kit.BoxVisual(dl.transform, "StripeL", new Vector3(0.75f, 0f, -0.06f), new Vector3(0.06f, 4.0f, 0.02f), MatKey.SafetyOrange, 0f);
            Kit.BoxVisual(dr.transform, "StripeR", new Vector3(-0.75f, 0f, -0.06f), new Vector3(0.06f, 4.0f, 0.02f), MatKey.SafetyOrange, 0f);
            exit.DoorLeft = dl.transform;
            exit.DoorRight = dr.transform;

            // huge EXIT sign
            var sign = Kit.Make("ExitSign", t, new Vector3(0f, H + 1.2f, -0.2f));
            Kit.BoxVisual(sign.transform, "SignBoard", Vector3.zero, new Vector3(4.6f, 1.5f, 0.25f), MatKey.SafetyOrange, 0.06f);
            Kit.BoxVisual(sign.transform, "SignGlow", new Vector3(0f, -0.82f, 0f), new Vector3(4.2f, 0.08f, 0.2f), MatKey.Glow, 0f, null, false);
            var txt = PerspectiveFonts.MakeText(sign.transform, "ExitText", "EXIT", 1.25f, UIColors.Background, new Vector3(0f, 0.02f, -0.14f), Quaternion.identity, true);
            txt.fontStyle = FontStyles.Bold;
            Kit.BoxVisual(sign.transform, "PostL", new Vector3(-1.6f, -0.95f, 0.05f), new Vector3(0.12f, 0.5f, 0.12f), MatKey.MetalDark, 0f);
            Kit.BoxVisual(sign.transform, "PostR", new Vector3(1.6f, -0.95f, 0.05f), new Vector3(0.12f, 0.5f, 0.12f), MatKey.MetalDark, 0f);

            // inside: floor light strip + joke sign on the back wall
            Kit.BoxVisual(t, "FloorStrip", new Vector3(0f, 0.11f, D - 0.3f), new Vector3(W - 0.4f, 0.02f, 0.12f), MatKey.LightOn, 0f, null, false);
            PerspectiveFonts.MakeText(t, "BackSign", "THANK YOU FOR YOUR SHIFT\n<size=60%>PLEASE RETURN YOUR BADGE AND ANY REMAINING COWORKERS</size>", 0.32f,
                UIColors.Text, new Vector3(0f, 3.2f, D - 0.02f), Quaternion.identity);

            // console (inside the cab, right wall)
            var consoleGo = Kit.Make("ExitConsole", t, new Vector3(1.75f, 0f, 1.6f), Quaternion.Euler(0f, 90f, 0f));
            var body = Kit.SolidBox(consoleGo.transform, "Pedestal", new Vector3(0f, 0.6f, 0f), new Vector3(0.9f, 1.2f, 0.5f), MatKey.PuzzleBody, 0.05f);
            Kit.BoxVisual(consoleGo.transform, "Screen", new Vector3(0f, 1.25f, -0.05f), new Vector3(0.8f, 0.45f, 0.06f), MatKey.Screen, 0.02f, Quaternion.Euler(-20f, 0f, 0f));
            Kit.BoxVisual(consoleGo.transform, "Button", new Vector3(0f, 1.05f, -0.27f), new Vector3(0.22f, 0.22f, 0.08f), MatKey.LightRed, 0.03f);
            PerspectiveFonts.MakeText(consoleGo.transform, "ScreenText", "LEAVE\n<size=70%>WITHOUT THEM?</size>", 0.12f, UIColors.Danger, new Vector3(0f, 1.27f, -0.09f), Quaternion.Euler(-20f, 0f, 0f));
            var console = body.gameObject.AddComponent<ExitConsole>();
            console.Exit = exit;
            exit.Console = console;

            exit.ZoneCenter = new Vector3(0f, 2f, D * 0.5f + 0.1f);
            exit.ZoneSize = new Vector3(W - 0.2f, 4f, D - 0.2f);
            return exit;
        }

        // ------------------------------------------------------------------ tutorials & hints
        /// <summary>Tutorial zone (center/size local to parent). keys: key caps, e.g. { "W", "A", "S", "D" }. important = stays until skipped.</summary>
        public static TutorialTrigger Tutorial(Transform parent, Vector3 center, Vector3 size, string id, string title, string body, string[] keys = null, bool important = false)
        {
            var go = Kit.Make("Tutorial_" + id, parent, center);
            var tt = go.AddComponent<TutorialTrigger>();
            tt.ZoneCenter = Vector3.zero;
            tt.ZoneSize = size;
            tt.Message = new TutorialMessage { Id = id, Title = title ?? "", Body = body ?? "", Keys = keys ?? new string[0], Important = important, Duration = important ? 0f : 7f };
            return tt;
        }

        /// <summary>Hint volume around a puzzle (center/size local to parent).</summary>
        public static global::Shifters.HintZone HintZone(Transform parent, Vector3 center, Vector3 size, string realHint, string vagueHint)
        {
            var go = Kit.Make("HintZone", parent, center);
            var hz = go.AddComponent<global::Shifters.HintZone>();
            hz.ZoneCenter = Vector3.zero;
            hz.ZoneSize = size;
            hz.RealHint = realHint ?? "";
            hz.VagueHint = vagueHint ?? "";
            return hz;
        }

        // ------------------------------------------------------------------ campaign wave (promoted by SEAMS)
        /// <summary>Weight limit on an exit (copy of L5PodWeightLimit, on LevelExit.Precondition): a WeightSensor on the
        /// cab's "CabFloor", a "CAPACITY" board with a live "kg / max KG" readout left of the door and two alarm lamps.
        /// While the floor carries more than maxKg the exit refuses to complete. Riders inside the zone but off the floor
        /// (a roof seat) still count as escaped.</summary>
        public static ExitWeightGate ExitWeightLimit(LevelExit exit, float maxKg)
        {
            if (exit == null) return null;
            var t = exit.transform;
            var floorT = t.Find("CabFloor");
            var sensor = exit.GetComponent<WeightSensor>();
            if (sensor == null) sensor = exit.gameObject.AddComponent<WeightSensor>();
            sensor.Surfaces = floorT != null && floorT.GetComponent<Collider>() != null ? new[] { floorT.GetComponent<Collider>() } : new Collider[0];
            var gate = exit.gameObject.AddComponent<ExitWeightGate>();
            gate.Exit = exit; gate.Sensor = sensor; gate.MaxKg = maxKg;
            var board = Kit.Make("ExitCapacity", t, new Vector3(-3.6f, 2.6f, -0.35f), Quaternion.identity);
            Kit.BoxVisual(board.transform, "Board", Vector3.zero, new Vector3(2.4f, 1.5f, 0.12f), MatKey.MetalDark, 0.03f);
            PuzzleVisuals.Text(board.transform, "Title", new Vector3(0f, 0.45f, -0.07f), Quaternion.identity, "CAPACITY", 3f, UIColors.AccentYellow, 2.2f, 0.4f);
            gate.Readout = PuzzleVisuals.Text(board.transform, "Readout", new Vector3(0f, -0.2f, -0.07f), Quaternion.identity,
                "0 / " + Mathf.RoundToInt(maxKg) + " KG", 3.4f, UIColors.Text, 2.2f, 0.8f);
            Kit.BoxVisual(board.transform, "Post", new Vector3(0f, -1.65f, 0.1f), new Vector3(0.15f, 1.9f, 0.15f), MatKey.MetalDark, 0.01f);
            gate.AlarmLamps = new[]
            {
                PuzzleVisuals.Lamp(t, "CapLampL", new Vector3(-2.25f, 4.4f, -0.32f), Quaternion.identity, 0.22f, PuzzleLampColor.Green),
                PuzzleVisuals.Lamp(t, "CapLampR", new Vector3(2.25f, 4.4f, -0.32f), Quaternion.identity, 0.22f, PuzzleLampColor.Green)
            };
            return gate;
        }

        /// <summary>Invisible catch volume (copy of L1SafetyNet): center/size local to p; characters entering it are put
        /// back at respawnWorld (WORLD point) facing yaw. Layer IgnoreRaycast, trigger only.</summary>
        public static LevelSafetyNet SafetyNet(Transform p, Vector3 center, Vector3 size, Vector3 respawnWorld, float yaw)
        {
            var go = Kit.Make("SafetyNet", p, center);
            go.layer = 2;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            var net = go.AddComponent<LevelSafetyNet>();
            net.RespawnPoint = respawnWorld;
            net.RespawnYaw = yaw;
            return net;
        }

        /// <summary>THE INTERN PRINTER kiosk (CAMPAIGN §16): a big office printer facing +Z with a screen, paper tray and
        /// "REPRINT LOST INTERNS" sign. [E] while an intern is Lost: 10 s PAPERWORK, then CharacterManager.Reprint on the
        /// output tray 1.8 m in front, and the current checkpoint is re-saved. Footprint ~1.8 x 1.2 m; keep 2.5 m clear in front.</summary>
        public static InternPrinterKiosk ReprintKiosk(Transform p, Vector3 pos, Quaternion? rot)
        {
            var root = Kit.Make("InternPrinter", p, pos, rot ?? Quaternion.identity);
            var t = root.transform;
            var body = Kit.SolidBox(t, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 1.3f, 1.2f), MatKey.MetalLight, 0.08f);
            Kit.BoxVisual(t, "Lid", new Vector3(0f, 1.36f, -0.05f), new Vector3(1.7f, 0.12f, 1.05f), MatKey.MetalDark, 0.04f);
            Kit.BoxVisual(t, "Stripe", new Vector3(0f, 0.95f, 0.61f), new Vector3(1.82f, 0.08f, 0.02f), MatKey.SafetyOrange, 0f, null, false);
            Kit.BoxVisual(t, "Slot", new Vector3(0f, 0.75f, 0.61f), new Vector3(1.3f, 0.06f, 0.03f), MatKey.MetalDark, 0f, null, false);
            Kit.BoxVisual(t, "Tray", new Vector3(0f, 0.6f, 0.85f), new Vector3(1.2f, 0.05f, 0.5f), MatKey.MetalDark, 0.01f);
            var paper = Kit.BoxVisual(t, "Paper", new Vector3(0f, 0.66f, 0.55f), new Vector3(0.8f, 0.02f, 0.5f), MatKey.Wall, 0f, null, false);
            Kit.BoxVisual(t, "ScreenBezel", new Vector3(0.45f, 1.6f, 0.2f), new Vector3(0.9f, 0.55f, 0.1f), MatKey.MetalDark, 0.02f, Quaternion.Euler(-25f, 0f, 0f));
            Kit.BoxVisual(t, "Screen", new Vector3(0.45f, 1.6f, 0.245f), new Vector3(0.8f, 0.45f, 0.02f), MatKey.Screen, 0f, Quaternion.Euler(-25f, 0f, 0f), false);
            var screen = PerspectiveFonts.MakeText(t, "ScreenText", "INTERN\nPRINTER", 0.13f, UIColors.AccentYellow, new Vector3(0.45f, 1.61f, 0.265f),
                Quaternion.Euler(25f, 180f, 0f));
            PuzzleVisuals.Lamp(t, "Lamp", new Vector3(-0.6f, 1.48f, 0.3f), Quaternion.identity, 0.14f, PuzzleLampColor.Green);
            var sign = Kit.Make("Sign", t, new Vector3(0f, 2.55f, 0f));
            Kit.BoxVisual(sign.transform, "SignBoard", Vector3.zero, new Vector3(3.0f, 0.7f, 0.1f), MatKey.SafetyOrange, 0.03f);
            var txt = PerspectiveFonts.MakeText(sign.transform, "SignText", "INTERN PRINTER\n<size=50%>REPRINT LOST INTERNS HERE</size>", 0.22f, Color.white,
                new Vector3(0f, 0f, 0.06f), Quaternion.Euler(0f, 180f, 0f), true);
            txt.fontStyle = FontStyles.Bold;
            Kit.BoxVisual(sign.transform, "Post", new Vector3(0f, -0.55f, -0.05f), new Vector3(0.1f, 0.6f, 0.1f), MatKey.MetalDark, 0f);
            var kiosk = body.gameObject.AddComponent<InternPrinterKiosk>();
            kiosk.Root = t;
            kiosk.Screen = screen;
            kiosk.Paper = paper.transform;
            kiosk.OutputLocal = new Vector3(0f, 0.05f, 1.8f);
            return kiosk;
        }
    }
}
