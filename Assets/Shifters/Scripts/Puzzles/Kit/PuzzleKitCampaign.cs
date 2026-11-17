// =====================================================================================
//  SHIFTERS - PuzzleKitCampaign.cs   [PUZZLES - campaign wave, CAMPAIGN §18.5]
//  Factories for the campaign parts: promoted copies of level-local parts (HoldLatch, Seat, CallElevator,
//  FoldingRamp, Signal) and new parts (WindowGate, OneAtATimeDoor, MinWeightAlarm, PropSocket, LightPool,
//  MakeShiftable, NoToolZone). Kit conventions: built entirely here, edit-mode safe, cached meshes, positions
//  LOCAL to parent, objects face +Z unless noted.
// =====================================================================================
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static partial class PuzzleKit
    {
        /// <summary>Most LightPools with a real point light per scene (the factory refuses more).</summary>
        public const int MaxRealPoolLights = 3;
        public static readonly Color PoolColor = new Color(1f, 0.86f, 0.55f);
        static readonly CultureInfo Inv2 = CultureInfo.InvariantCulture;

        // ================================================================== HOLD LATCH (copy of L3HoldLatch / L4SecurityLatch)
        /// <summary>Latch display on a post. Readable face toward local -Z (identity = read by a viewer looking +Z, the
        /// EnvKit convention: pass EnvKit.FacingViewer(...)). HoldThenLatch: ON once 'input' stayed
        /// on for 'seconds'. OpenThenLatch: passes 'input' through and latches after 'seconds'.</summary>
        public static HoldLatch HoldLatch(Transform parent, Vector3 pos, Quaternion? rot, SignalSource input, float seconds, string title,
                                          HoldLatchMode mode = HoldLatchMode.HoldThenLatch)
        {
            var root = Kit.Make(mode == HoldLatchMode.OpenThenLatch ? "SecurityLatch" : "HoldLatch", parent, pos, Rot(rot));
            var t = root.transform;
            PuzzleVisuals.Box(t, "Foot", new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 0.5f), MatKey.MetalDark);
            PuzzleVisuals.Cyl(t, "Post", new Vector3(0f, 0.9f, 0f), 0.06f, 1.7f, MatKey.MetalDark);
            Kit.SolidBox(t, "Housing", new Vector3(0f, 2.0f, 0f), new Vector3(1.5f, 0.75f, 0.22f), MatKey.PuzzleBody, 0.04f);
            PuzzleVisuals.Box(t, "Glass", new Vector3(0f, 2.05f, -0.115f), new Vector3(1.3f, 0.42f, 0.02f), MatKey.Black, 0.01f, null, false);
            PuzzleVisuals.Text(t, "Title", new Vector3(0f, 2.44f, -0.12f), PuzzleVisuals.Facing(Vector3.back), string.IsNullOrEmpty(title) ? "SECURITY LATCH" : title, 1.6f, UIColors.AccentYellow, 1.4f, 0.2f);
            string idle = mode == HoldLatchMode.OpenThenLatch ? "STANDBY" : "HOLD " + seconds.ToString("0.#", Inv2) + " S";
            var disp = PuzzleVisuals.Text(t, "Display", new Vector3(0f, 2.06f, -0.13f), PuzzleVisuals.Facing(Vector3.back), idle, 3.2f, UIColors.Text, 1.25f, 0.4f);
            var lamps = new PuzzleLamp[5];
            for (int i = 0; i < lamps.Length; i++)
                lamps[i] = PuzzleVisuals.Lamp(t, "Lamp" + i, new Vector3((i - 2) * 0.24f, 1.74f, -0.12f), Quaternion.Euler(0f, 180f, 0f), 0.11f, PuzzleLampColor.Off);
            var latch = root.AddComponent<HoldLatch>();
            latch.Input = input;
            latch.HoldSeconds = Mathf.Max(0.05f, seconds);
            latch.Mode = mode;
            latch.Display = disp;
            latch.Lamps = lamps;
            Reveal(root, PerspectiveMask.Intellect, (mode == HoldLatchMode.OpenThenLatch ? "OPEN " : "HOLD ") + seconds.ToString("0.#", Inv2) + " S = STAYS OPEN", new Vector3(0f, 2.9f, 0f), true, false);
            return latch;
        }

        // ================================================================== SEAT (copy of L3VolunteerChair)
        /// <summary>"INSERT INTERN TO CONTINUE" chair: E to sit (locked in, this source ON while occupied). Any change of
        /// releaseInput frees the volunteer; the signal then stays ON for Tuning.TimedWindow(routeLength, fastest).
        /// The occupant faces +Z; the released one is put down 1.6 m in front. label = screen text while empty.
        /// seat.ReleasedSignal latches ON at the first release (recovery route).</summary>
        public static PuzzleSeat Seat(Transform parent, Vector3 pos, Quaternion? rot, string label, SignalSource releaseInput = null, float routeLength = 21f)
        {
            var root = Kit.Make("PuzzleSeat", parent, pos, Rot(rot));
            var t = root.transform;
            Kit.SolidBox(t, "Dais", new Vector3(0f, 0.08f, -0.2f), new Vector3(3.0f, 0.16f, 2.4f), MatKey.MetalDark, 0.04f);
            PuzzleVisuals.StripedBox(t, "DaisEdge", new Vector3(0f, 0.17f, 0.99f), new Vector3(3.0f, 0.03f, 0.12f), 0, 0.3f, false);
            const float daisTop = 0.16f;
            PuzzleVisuals.Box(t, "Pedestal", new Vector3(0f, daisTop + 0.15f, -0.25f), new Vector3(0.35f, 0.3f, 0.35f), MatKey.MetalLight);
            Kit.SolidBox(t, "Cushion", new Vector3(0f, daisTop + 0.36f, -0.18f), new Vector3(0.9f, 0.14f, 0.7f), MatKey.PuzzleBody, 0.05f);
            Kit.SolidBox(t, "Back", new Vector3(0f, daisTop + 1.05f, -0.62f), new Vector3(0.95f, 1.3f, 0.18f), MatKey.PuzzleBody, 0.06f);
            PuzzleVisuals.Box(t, "BackPad", new Vector3(0f, daisTop + 1.0f, -0.52f), new Vector3(0.7f, 1.0f, 0.05f), MatKey.PuzzleAccent, 0.03f);
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.Box(t, "Arm", new Vector3(s * 0.52f, daisTop + 0.62f, -0.2f), new Vector3(0.12f, 0.08f, 0.8f), MatKey.MetalDark);
                PuzzleVisuals.Box(t, "ArmPost", new Vector3(s * 0.52f, daisTop + 0.45f, 0.12f), new Vector3(0.08f, 0.3f, 0.08f), MatKey.MetalDark);
                PuzzleVisuals.Box(t, "Strap", new Vector3(s * 0.52f, daisTop + 0.67f, -0.05f), new Vector3(0.16f, 0.04f, 0.14f), MatKey.WarningRed, 0.01f, null, false);
                var coil = Kit.Make("Coil", t, new Vector3(s * 1.2f, daisTop, -0.6f)).transform;
                PuzzleVisuals.Cyl(coil, "Base", new Vector3(0f, 0.15f, 0f), 0.22f, 0.3f, MatKey.MetalDark);
                PuzzleVisuals.Cyl(coil, "Rod", new Vector3(0f, 1.0f, 0f), 0.07f, 1.5f, MatKey.MetalLight);
                for (int k = 0; k < 4; k++) PuzzleVisuals.Cyl(coil, "Ring", new Vector3(0f, 0.6f + k * 0.28f, 0f), 0.16f, 0.06f, MatKey.Pipe);
                ProcMesh.Part("Orb", coil, PuzzleVisuals.Sphere(0.2f), M(MatKey.LightBlue), new Vector3(0f, 1.85f, 0f), null, null, false);
            }
            PuzzleVisuals.Box(t, "HelmetArm", new Vector3(0f, daisTop + 1.85f, -0.45f), new Vector3(0.12f, 0.12f, 0.5f), MatKey.MetalDark);
            var helmet = PuzzleVisuals.Cyl(t, "Helmet", new Vector3(0f, daisTop + 1.78f, -0.18f), 0.32f, 0.22f, MatKey.MetalLight, null, 18);
            helmet.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PuzzleVisuals.Cyl(t, "HelmetGlow", new Vector3(0f, daisTop + 1.66f, -0.18f), 0.28f, 0.03f, MatKey.Hologram, null, 18, false);
            var bar = Kit.Make("Restraint", t, new Vector3(0f, daisTop + 0.95f, -0.55f), Quaternion.Euler(-70f, 0f, 0f)).transform;
            PuzzleVisuals.Box(bar, "BarL", new Vector3(-0.42f, -0.15f, 0.35f), new Vector3(0.06f, 0.06f, 0.75f), MatKey.HazardYellow, 0.01f, Quaternion.Euler(25f, 0f, 0f), false);
            PuzzleVisuals.Box(bar, "BarR", new Vector3(0.42f, -0.15f, 0.35f), new Vector3(0.06f, 0.06f, 0.75f), MatKey.HazardYellow, 0.01f, Quaternion.Euler(25f, 0f, 0f), false);
            PuzzleVisuals.Box(bar, "BarFront", new Vector3(0f, -0.3f, 0.68f), new Vector3(0.9f, 0.08f, 0.08f), MatKey.HazardYellow, 0.01f, null, false);
            Kit.SolidBox(t, "GantryL", new Vector3(-1.55f, 1.5f, -1.2f), new Vector3(0.16f, 3.0f, 0.16f), MatKey.MetalDark);
            Kit.SolidBox(t, "GantryR", new Vector3(1.55f, 1.5f, -1.2f), new Vector3(0.16f, 3.0f, 0.16f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "GantryTop", new Vector3(0f, 3.05f, -1.2f), new Vector3(3.3f, 0.18f, 0.2f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "ScreenBox", new Vector3(0f, 2.62f, -1.1f), new Vector3(1.6f, 0.62f, 0.14f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "ScreenGlass", new Vector3(0f, 2.62f, -1.02f), new Vector3(1.45f, 0.5f, 0.02f), MatKey.Screen, 0.005f, null, false);
            string idle = string.IsNullOrEmpty(label) ? "INSERT INTERN\nTO CONTINUE" : label;
            var screen = PuzzleVisuals.Text(t, "Screen", new Vector3(0f, 2.62f, -1.0f), PuzzleVisuals.Facing(Vector3.forward), idle, 2.0f, UIColors.Info, 1.4f, 0.48f);
            screen.enableAutoSizing = true; screen.fontSizeMin = 0.8f; screen.fontSizeMax = 2.0f;
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(0f, 3.25f, -1.1f), Quaternion.identity, 0.2f);
            Kit.Trigger(t, "InteractVolume", new Vector3(0f, 1.0f, -0.1f), new Vector3(1.6f, 2.0f, 1.6f));
            var seat = Kit.Make("Seat", t, new Vector3(0f, daisTop + 0.02f, -0.12f));
            var exit = Kit.Make("ExitPoint", t, new Vector3(0f, 0.05f, 1.6f));

            var chair = root.AddComponent<PuzzleSeat>();
            chair.Seat = seat.transform;
            chair.ExitPoint = exit.transform;
            chair.ReleaseInput = releaseInput;
            chair.RouteLength = routeLength;
            chair.Screen = screen;
            chair.IdleText = idle;
            chair.Lamp = lamp;
            chair.Restraint = bar;
            chair.ReleasedSignal = Kit.Make("ReleasedSignal", t, Vector3.zero).AddComponent<PuzzleSignal>();
            Reveal(root, PerspectiveMask.Intellect, idle.Replace('\n', ' ') + "\nSIT = POWER ON", new Vector3(0f, 3.6f, 0f), true, false, new Renderer[0]);
            return chair;
        }

        // ================================================================== CALL ELEVATOR (copy of L2FreightElevator)
        /// <summary>Elevator car (size.x x size.y, top at pos.y when down) rising 'height' m, with a control pedestal on
        /// the car (default: the +X/+Z corner, facing -Z), a car WeightSensor and a load display. maxKg caps the load,
        /// maxCount (> 0) caps the interns ("ONE (1) PASSENGER"). Empty at the top for 1.5 s = returns down.</summary>
        public static CallElevator CallElevator(Transform parent, Vector3 pos, float height, Vector2 size, float maxKg, int maxCount, Vector3? panelLocal = null)
        {
            if (size.sqrMagnitude < 0.01f) size = new Vector2(3f, 3f);
            var elev = Elevator(parent, pos, Quaternion.identity, height, size);
            var car = elev.Body != null ? elev.Body.gameObject : elev.gameObject;
            var carCol = car.GetComponent<BoxCollider>();
            var sensor = car.AddComponent<WeightSensor>();
            sensor.Surfaces = carCol != null ? new Collider[] { carCol } : new Collider[0];

            var pl = panelLocal ?? new Vector3(size.x * 0.5f - 0.45f, 0f, size.y * 0.5f - 0.4f);
            var panelGo = Kit.Make("ControlPanel", car.transform, pl, Quaternion.Euler(0f, 180f, 0f));
            var pt = panelGo.transform;
            Kit.SolidBox(pt, "Pedestal", new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 1.1f, 0.45f), MatKey.PuzzleBody);
            Kit.BoxVisual(pt, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(0.7f, 0.08f, 0.6f), MatKey.MetalDark);
            Kit.BoxVisual(pt, "Face", new Vector3(0f, 1.0f, 0.2f), new Vector3(0.5f, 0.35f, 0.08f), MatKey.Screen, 0.02f, Quaternion.Euler(-25f, 0f, 0f), false);
            Kit.BoxVisual(pt, "BigButton", new Vector3(0f, 1.14f, 0.05f), new Vector3(0.22f, 0.08f, 0.22f), MatKey.WarningRed, 0.03f);
            var lamp = PuzzleVisuals.Lamp(pt, "Lamp", new Vector3(0f, 0.45f, 0.235f), Quaternion.identity, 0.14f);
            var up = PuzzleVisuals.Text(pt, "UpLabel", new Vector3(0f, 0.8f, 0.236f), Quaternion.Euler(0f, 180f, 0f), "UP", 2.2f, UIColors.AccentYellow, 0.45f, 0.25f);
            up.alignment = TextAlignmentOptions.Center;
            // capacity sign on the panel's back (readable from the car centre side)
            string cap = (maxCount > 0 ? (maxCount == 1 ? "ONE (1) PASSENGER" : "MAX " + maxCount + " INTERNS") + "\n" : "") + "MAX " + Mathf.RoundToInt(maxKg) + " KG";
            var capText = PuzzleVisuals.Text(pt, "Capacity", new Vector3(0f, 0.75f, -0.236f), Quaternion.identity, cap, 1.4f, UIColors.AccentYellow, 0.55f, 0.4f);
            capText.enableAutoSizing = true; capText.fontSizeMin = 0.5f; capText.fontSizeMax = 1.4f;
            var load = PuzzleVisuals.Text(pt, "Load", new Vector3(0f, 1.32f, 0f), PuzzleVisuals.Facing(Vector3.forward), "LOAD 0 KG", 1.2f, UIColors.TextMuted, 0.9f, 0.2f);
            load.gameObject.AddComponent<PuzzleBillboard>();

            var fe = panelGo.AddComponent<CallElevator>();
            fe.MaxKg = maxKg;
            fe.MaxCount = Mathf.Max(0, maxCount);
            fe.Car = elev;
            fe.Sensor = sensor;
            fe.Lamp = lamp;
            fe.LoadText = load;
            Wire(elev, fe);
            return fe;
        }

        /// <summary>Landing call button pedestal (E) for a CallElevator; top = the upper landing. Readable from local -Z.</summary>
        public static CallElevatorButton ElevatorCallButton(Transform parent, Vector3 pos, Quaternion? rot, CallElevator elevator, bool top)
        {
            var root = Kit.Make(top ? "CallButtonTop" : "CallButtonBottom", parent, pos, Rot(rot));
            var t = root.transform;
            Kit.SolidBox(t, "Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 1.0f, 0.3f), MatKey.PuzzleBody);
            Kit.BoxVisual(t, "Button", new Vector3(0f, 1.04f, 0f), new Vector3(0.2f, 0.08f, 0.2f), MatKey.WarningRed, 0.03f);
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(0f, 0.75f, -0.16f), Quaternion.Euler(0f, 180f, 0f), 0.12f);
            PuzzleVisuals.Text(t, "Label", new Vector3(0f, 0.45f, -0.16f), PuzzleVisuals.Facing(Vector3.back), top ? "CALL\n(UP HERE)" : "CALL", 1.2f, UIColors.AccentYellow, 0.38f, 0.3f);
            var b = root.AddComponent<CallElevatorButton>();
            b.Elevator = elevator; b.Top = top; b.Lamp = lamp;
            return b;
        }

        // ================================================================== FOLDING RAMP (copy of L1FoldingRamp)
        /// <summary>Hinged ramp / folding stairs: hinge = top edge center of the ledge (local to parent); yaw = direction
        /// the ramp runs DOWN toward (0 = +Z). Raised until powered (Latch logic: one-way unfold).</summary>
        public static FoldingRamp FoldingRamp(Transform parent, Vector3 hinge, float yaw, float width, float run, float height, bool stairs,
                                              string foldedLabel, params SignalSource[] inputs)
            => Shifters.FoldingRamp.Build(parent, hinge, yaw, width, run, height, stairs, foldedLabel, inputs);

        // ================================================================== SIGNAL (copy of L3Signal)
        /// <summary>Plain script-settable source: signal.Set(true/false). StartsOn for inverted beats.</summary>
        public static PuzzleSignal Signal(Transform parent, string name)
            => Kit.Make(string.IsNullOrEmpty(name) ? "Signal" : name, parent, Vector3.zero).AddComponent<PuzzleSignal>();

        // ================================================================== WINDOW GATE
        /// <summary>Run-through gate (pos = bottom center, size = passage box, passage along local Z): ON from an Alive
        /// intern's entry while 'window' runs until the window ends. Arch posts stand outside size.x.</summary>
        public static WindowGate WindowGate(Transform parent, Vector3 pos, Quaternion? rot, Vector3 size, TimedButton window)
        {
            if (size.sqrMagnitude < 0.01f) size = new Vector3(3f, 3f, 1f);
            size = new Vector3(Mathf.Max(1f, size.x), Mathf.Max(2.2f, size.y), Mathf.Max(0.6f, size.z));
            var root = Kit.Make("WindowGate", parent, pos, Rot(rot));
            var t = root.transform;
            float hw = size.x * 0.5f, h = size.y;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.SolidBox(t, "Post", new Vector3(s * (hw + 0.15f), (h + 0.4f) * 0.5f, 0f), new Vector3(0.3f, h + 0.4f, 0.4f), MatKey.MetalDark);
                PuzzleVisuals.Box(t, "PostTrim", new Vector3(s * (hw + 0.15f), (h + 0.4f) * 0.5f, 0f), new Vector3(0.08f, h + 0.4f, 0.42f), MatKey.SafetyOrange, 0.005f, null, false);
            }
            PuzzleVisuals.Box(t, "Header", new Vector3(0f, h + 0.25f, 0f), new Vector3(size.x + 0.6f, 0.3f, 0.4f), MatKey.MetalDark);
            var strips = new List<Renderer>();
            for (int f = -1; f <= 1; f += 2)
                strips.Add(PuzzleVisuals.Box(t, "Strip", new Vector3(0f, h + 0.25f, f * 0.21f), new Vector3(size.x - 0.1f, 0.1f, 0.02f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.01f, null, false));
            for (int s = -1; s <= 1; s += 2)
                strips.Add(PuzzleVisuals.Box(t, "PostLamp", new Vector3(s * (hw + 0.15f), h + 0.5f, 0f), new Vector3(0.22f, 0.12f, 0.22f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.02f, null, false));
            var lamp = PuzzleVisuals.LampFrom(Kit.Make("Lamps", t, Vector3.zero), PuzzleLampColor.Red, strips.ToArray());
            // floor threshold with chevrons pointing along +Z
            PuzzleVisuals.StripedBox(t, "Threshold", new Vector3(0f, 0.015f, 0f), new Vector3(size.x, 0.03f, 0.25f), 0, 0.3f, false);
            var labels = new TextMeshPro[2];
            labels[0] = PuzzleVisuals.Text(t, "LabelBack", new Vector3(0f, h + 0.72f, -0.05f), PuzzleVisuals.Facing(Vector3.back), "WINDOW GATE", 2.4f, UIColors.Text, size.x + 0.6f, 0.4f);
            labels[1] = PuzzleVisuals.Text(t, "LabelFront", new Vector3(0f, h + 0.72f, 0.05f), PuzzleVisuals.Facing(Vector3.forward), "WINDOW GATE", 2.4f, UIColors.Text, size.x + 0.6f, 0.4f);
            PuzzleVisuals.Box(t, "SignBoard", new Vector3(0f, h + 0.72f, 0f), new Vector3(size.x * 0.75f, 0.44f, 0.06f), MatKey.Black, 0.02f, null, false);
            var g = root.AddComponent<WindowGate>();
            g.Window = window; g.Size = size; g.Lamp = lamp; g.Labels = labels;
            Reveal(root, PerspectiveMask.Speed, "WINDOW GATE\nPASS IT BEFORE THE TIMER ENDS", new Vector3(0f, h + 1.3f, 0f), true, false);
            return g;
        }

        // ================================================================== ONE AT A TIME DOOR
        /// <summary>Kit door (3 x 4.2 m, pos = bottom center of the doorway, passage along Z) that opens only while its
        /// inputs are on AND exactly one Alive intern stands in the approach zone (zoneSize, in front on local -Z).</summary>
        public static OneAtATimeDoor OneAtATimeDoor(Transform parent, Vector3 pos, Quaternion? rot, Vector3 zoneSize, params SignalSource[] inputs)
        {
            if (zoneSize.sqrMagnitude < 0.01f) zoneSize = new Vector3(3f, 3f, 3f);
            var door = Door(parent, pos, rot, DoorMode.SlideUp, 3f, 4.2f);
            var t = door.transform;
            var sensorGo = Kit.Make("OneAtATimeSensor", t, Vector3.zero);
            var o = sensorGo.AddComponent<OneAtATimeDoor>();
            o.Door = door;
            o.ZoneSize = zoneSize;
            o.ZoneCenter = new Vector3(0f, zoneSize.y * 0.5f, -(zoneSize.z * 0.5f + 0.2f));
            var all = new List<SignalSource>();
            if (inputs != null) foreach (var s in inputs) if (s != null) all.Add(s);
            all.Add(o);
            door.Logic = SignalLogic.All;
            Wire(door, all.ToArray());
            // approach zone: floor brackets + "1" footprints
            PuzzleVisuals.FloorCorners(t, new Vector3(0f, 0f, o.ZoneCenter.z), new Vector2(zoneSize.x, zoneSize.z), MatKey.PuzzleBody);
            PuzzleVisuals.Text(t, "FloorLabel", new Vector3(0f, 0.02f, o.ZoneCenter.z), PuzzleVisuals.Facing(Vector3.up), "1", 9f, UIColors.Accent, zoneSize.x, zoneSize.z);
            // sign above the door, readable from the approach side (-Z)
            float sy = door.Height + 1.2f;
            PuzzleVisuals.Box(t, "SignBoard", new Vector3(0f, sy, -0.5f), new Vector3(3.6f, 0.9f, 0.08f), MatKey.Black, 0.02f, null, false);
            o.Sign = PuzzleVisuals.Text(t, "Sign", new Vector3(0f, sy, -0.56f), PuzzleVisuals.Facing(Vector3.back), "ONE (1) AT A TIME", 2.6f, UIColors.AccentYellow, 3.4f, 0.8f);
            o.Lamp = PuzzleVisuals.Lamp(t, "SoloLamp", new Vector3(2.1f, sy, -0.56f), Quaternion.Euler(0f, 180f, 0f), 0.2f, PuzzleLampColor.Yellow);
            Reveal(sensorGo, PerspectiveMask.Intellect, "SOLO DOOR: EXACTLY 1 INTERN IN THE ZONE", new Vector3(0f, 2.6f, o.ZoneCenter.z), true, false);
            return o;
        }

        // ================================================================== MIN WEIGHT ALARM (heist pedestal)
        /// <summary>2 x 2 m pedestal, top 0.6 m above pos (put the guarded prop on top: y = pos.y + 0.6). Lifting the prop
        /// while less than minKg (not counting the lifter) stays on the pedestal = ALARM (this source ON 4 s, prop respawns).</summary>
        public static MinWeightAlarm MinWeightAlarm(Transform parent, Vector3 pos, Quaternion? rot, float minKg, Grabbable guarded)
        {
            const float top = 0.6f;
            var root = Kit.Make("MinWeightAlarm", parent, pos, Rot(rot));
            var t = root.transform;
            var col = Kit.SolidBox(t, "Pedestal", new Vector3(0f, top * 0.5f, 0f), new Vector3(2f, top, 2f), MatKey.Marble, 0.05f);
            PuzzleVisuals.Box(t, "TrimTop", new Vector3(0f, top - 0.04f, 0f), new Vector3(2.06f, 0.08f, 2.06f), MatKey.Gold, 0.02f);
            PuzzleVisuals.Box(t, "TrimFoot", new Vector3(0f, 0.04f, 0f), new Vector3(2.1f, 0.08f, 2.1f), MatKey.Gold, 0.02f);
            PuzzleVisuals.Box(t, "Pad", new Vector3(0f, top + 0.01f, 0f), new Vector3(1.7f, 0.02f, 1.7f), MatKey.Velvet, 0.005f, null, false);
            var sensor = col.gameObject.AddComponent<WeightSensor>();
            sensor.Surfaces = new Collider[] { col };
            PuzzleVisuals.Box(t, "Screen", new Vector3(0f, top * 0.5f, -1.01f), new Vector3(1.3f, 0.4f, 0.02f), MatKey.Black, 0.01f, null, false);
            var readout = PuzzleVisuals.Text(t, "Readout", new Vector3(0f, top * 0.5f, -1.03f), PuzzleVisuals.Facing(Vector3.back), "MIN " + Mathf.RoundToInt(minKg) + " KG", 1.5f, UIColors.Text, 1.25f, 0.38f);
            readout.richText = true;
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(0.82f, top * 0.5f, -1.02f), Quaternion.Euler(0f, 180f, 0f), 0.14f);
            var a = root.AddComponent<MinWeightAlarm>();
            a.MinKg = minKg; a.Guarded = guarded; a.Sensor = sensor; a.TopHeight = top; a.TopHalf = new Vector2(1f, 1f);
            a.Readout = readout; a.Lamp = lamp;
            Reveal(root, PerspectiveMask.Strength, "PRESSURE PEDESTAL\nKEEP " + Mathf.RoundToInt(minKg) + " KG ON IT", new Vector3(0f, 2.4f, 0f), true, false);
            return a;
        }

        // ================================================================== PROP SOCKET
        /// <summary>Cradle (pos = floor, seat 0.5 m up) that is ON while 'accepted' (null = any prop) rests in it.
        /// Released props within 1.4 m snap in.</summary>
        public static PropSocket PropSocket(Transform parent, Vector3 pos, Quaternion? rot, Grabbable accepted)
        {
            const float seatY = 0.5f;
            var root = Kit.Make("PropSocket", parent, pos, Rot(rot));
            var t = root.transform;
            Kit.SolidBox(t, "Base", new Vector3(0f, seatY * 0.5f, 0f), new Vector3(1.4f, seatY, 1.4f), MatKey.MetalDark, 0.06f);
            PuzzleVisuals.Cyl(t, "Ring", new Vector3(0f, seatY + 0.02f, 0f), 0.62f, 0.04f, MatKey.PuzzleBody, null, 24, false);
            PuzzleVisuals.Cyl(t, "Core", new Vector3(0f, seatY + 0.025f, 0f), 0.45f, 0.04f, MatKey.Hologram, null, 24, false);
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0f, 0.62f);
                Kit.SolidBox(t, "Claw", new Vector3(d.x, seatY + 0.15f, d.z), new Vector3(0.12f, 0.3f, 0.12f), MatKey.SafetyOrange, 0.02f);
            }
            var anchor = Kit.Make("Anchor", t, new Vector3(0f, seatY, 0f));
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(0f, seatY * 0.5f, -0.71f), Quaternion.Euler(0f, 180f, 0f), 0.16f);
            string what = accepted != null ? accepted.DisplayName.ToUpperInvariant() : "PART";
            var label = PuzzleVisuals.Text(t, "Label", new Vector3(0f, seatY + 1.6f, 0f), Quaternion.identity, "INSERT " + what, 2.2f, UIColors.AccentYellow, 2.4f, 0.4f);
            label.gameObject.AddComponent<PuzzleBillboard>();
            var s = root.AddComponent<PropSocket>();
            s.Accepted = accepted; s.Anchor = anchor.transform; s.Lamp = lamp; s.Label = label; s.IdleText = "INSERT " + what;
            Reveal(root, PerspectiveMask.Intellect, "SOCKET: " + what, new Vector3(0f, 2.6f, 0f), true, false);
            return s;
        }

        // ================================================================== LIGHT POOL
        /// <summary>Emissive floor ring of 'radius' (pos = floor center) that counts as LIT (LightRegistry) while powered;
        /// always on without inputs. realLight adds one unshadowed point light (max MaxRealPoolLights per scene).</summary>
        public static LightPool LightPool(Transform parent, Vector3 pos, float radius = 3f, bool realLight = false, params SignalSource[] inputs)
        {
            radius = Mathf.Max(0.5f, radius);
            var root = Kit.Make("LightPool", parent, pos, Quaternion.identity);
            var t = root.transform;
            var on = MaterialLibrary.Colored(PoolColor, 0.3f, 2.2f);
            var off = MaterialLibrary.Colored(new Color(0.22f, 0.21f, 0.2f), 0.2f, 0f);
            var ring = ProcMesh.Part("Ring", t, RingMesh(radius, 0.16f), on, new Vector3(0f, 0.025f, 0f), null, null, false).GetComponent<Renderer>();
            var inner = ProcMesh.Part("InnerRing", t, RingMesh(radius * 0.5f, 0.05f), on, new Vector3(0f, 0.022f, 0f), null, null, false).GetComponent<Renderer>();
            var disc = ProcMesh.Part("Pool", t, DiscMesh(radius - 0.1f), MaterialLibrary.Transparent(new Color(PoolColor.r, PoolColor.g, PoolColor.b, 0.13f)), new Vector3(0f, 0.018f, 0f), null, null, false).GetComponent<Renderer>();
            var glow = new List<Renderer> { ring, inner };
            for (int i = 0; i < 8; i++)
            {
                var d = Quaternion.Euler(0f, i * 45f, 0f) * new Vector3(0f, 0f, radius + 0.12f);
                glow.Add(PuzzleVisuals.Box(t, "Stud", new Vector3(d.x, 0.03f, d.z), new Vector3(0.16f, 0.06f, 0.16f), on, 0.02f, Quaternion.Euler(0f, i * 45f, 0f), false));
            }
            var pool = root.AddComponent<LightPool>();
            pool.Radius = radius; pool.Glow = glow.ToArray(); pool.OnMaterial = on; pool.OffMaterial = off; pool.Disc = disc;
            pool.PoweredWithoutInputs = true;
            pool.Logic = SignalLogic.All;
            if (realLight)
            {
                int existing = 0;
                foreach (var lp in Object.FindObjectsByType<LightPool>(FindObjectsInactive.Exclude))
                    if (lp != null && lp != pool && lp.RealLight != null && lp.gameObject.scene == root.scene) existing++;
                if (existing >= MaxRealPoolLights) Debug.LogWarning("[PuzzleKit] LightPool: already " + existing + " real pool lights in this scene; this one is emissive only.");
                else
                {
                    var lg = Kit.Make("PoolLight", t, new Vector3(0f, 2.6f, 0f));
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Point; l.range = radius * 2.2f; l.intensity = pool.LightIntensity; l.color = PoolColor;
                    l.shadows = LightShadows.None;
                    pool.RealLight = l;
                }
            }
            return Wire(pool, inputs);
        }

        static Mesh RingMesh(float radius, float width)
        {
            string key = string.Format(Inv2, "pz_pool_ring_{0:0.##}_{1:0.###}", radius, width);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                int seg = Mathf.Clamp(Mathf.RoundToInt(radius * 14f), 24, 72);
                float r0 = radius - width, r1 = radius;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)); var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                    b.Quad(d0 * r0, d0 * r1, d1 * r1, d1 * r0, Color.white, Vector3.up);
                }
                return b.ToMesh(key);
            });
        }

        static Mesh DiscMesh(float radius)
        {
            string key = string.Format(Inv2, "pz_pool_disc_{0:0.##}", radius);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                int seg = Mathf.Clamp(Mathf.RoundToInt(radius * 12f), 20, 64);
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    b.Tri(Vector3.zero, new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius, new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius, Color.white, Vector3.up);
                }
                return b.ToMesh(key);
            });
        }

        // ================================================================== SHIFTABLE (BIT-SHIFTER)
        /// <summary>Make a dynamic prop BIT-SHIFTER-able (Shiftable: NORMAL -> HUGE -> TINY). Pushable blocks get a
        /// (disabled) Grabbable so TINY blocks can be carried. Call after the prop is fully built.</summary>
        public static Shiftable MakeShiftable(Rigidbody rb)
        {
            if (rb == null) return null;
            var go = rb.gameObject;
            var s = go.GetComponent<Shiftable>();
            if (s != null) return s;
            var push = go.GetComponent<Pushable>();
            if (push != null && go.GetComponent<Grabbable>() == null)
            {
                var g = go.AddComponent<Grabbable>();
                g.DisplayName = string.IsNullOrEmpty(push.DisplayName) ? "block" : push.DisplayName;
                g.CanBeGrabbed = false;
            }
            s = go.AddComponent<Shiftable>();
            s.Capture();
            var info = go.GetComponent<PropInfo>();
            if (info != null && info.Reveal != null) info.Reveal.Label = info.Reveal.Label + "\n<< >> SHIFTABLE";
            return s;
        }

        // ================================================================== NO-TOOL ZONE
        /// <summary>Purple-striped NO-SWAP / NO-SIZE volume: center/size local to parent (box, axis-aligned to parent).
        /// Tools query Shifters.NoToolZone.Blocks / BlocksSegment and call NoToolZone.Fizzle.</summary>
        public static NoToolZone NoToolZone(Transform parent, Vector3 center, Vector3 size, ToolMask mask)
        {
            if (mask == ToolMask.None) mask = ToolMask.All;
            var root = Kit.Make("NoToolZone", parent, new Vector3(center.x, center.y - size.y * 0.5f, center.z));
            var t = root.transform;
            var z = root.AddComponent<NoToolZone>();
            z.Center = new Vector3(0f, size.y * 0.5f, 0f); z.Size = size; z.Mask = mask;
            var purple = MaterialLibrary.Colored(Shifters.NoToolZone.Purple, 0.3f, 0.5f);
            float hx = size.x * 0.5f, hz = size.z * 0.5f;
            PurpleStripes(t, "EdgeS", new Vector3(0f, 0.02f, -hz), new Vector3(size.x, 0.04f, 0.3f), 0, purple);
            PurpleStripes(t, "EdgeN", new Vector3(0f, 0.02f, hz), new Vector3(size.x, 0.04f, 0.3f), 0, purple);
            PurpleStripes(t, "EdgeW", new Vector3(-hx, 0.02f, 0f), new Vector3(0.3f, 0.04f, size.z), 2, purple);
            PurpleStripes(t, "EdgeE", new Vector3(hx, 0.02f, 0f), new Vector3(0.3f, 0.04f, size.z), 2, purple);
            // faint purple curtain walls so the volume reads from afar
            var veil = MaterialLibrary.Transparent(new Color(0.62f, 0.32f, 1f, 0.07f));
            float vh = Mathf.Min(size.y, 3f);
            PuzzleVisuals.Box(t, "VeilS", new Vector3(0f, vh * 0.5f, -hz), new Vector3(size.x, vh, 0.02f), veil, 0f, null, false);
            PuzzleVisuals.Box(t, "VeilN", new Vector3(0f, vh * 0.5f, hz), new Vector3(size.x, vh, 0.02f), veil, 0f, null, false);
            PuzzleVisuals.Box(t, "VeilW", new Vector3(-hx, vh * 0.5f, 0f), new Vector3(0.02f, vh, size.z), veil, 0f, null, false);
            PuzzleVisuals.Box(t, "VeilE", new Vector3(hx, vh * 0.5f, 0f), new Vector3(0.02f, vh, size.z), veil, 0f, null, false);
            string label = Shifters.NoToolZone.LabelFor(mask);
            var tl = PuzzleVisuals.Text(t, "FloorLabel", new Vector3(0f, 0.045f, -hz + 0.6f), PuzzleVisuals.Facing(Vector3.up), label + "\n<size=60%>HR SAYS NO.</size>", 3f, Shifters.NoToolZone.Purple, Mathf.Max(1f, size.x - 0.6f), 1f);
            tl.enableAutoSizing = true; tl.fontSizeMin = 1f; tl.fontSizeMax = 3f;
            return z;
        }

        static void PurpleStripes(Transform parent, string name, Vector3 center, Vector3 size, int axis, Material purple)
        {
            var root = Kit.Make(name, parent, center);
            PuzzleVisuals.Box(root.transform, "Dark", Vector3.zero, size, MatKey.HazardBlack, 0.01f, null, false);
            const float band = 0.35f;
            int count = Mathf.Max(1, Mathf.FloorToInt(size[axis] / (band * 2f)));
            var bandSize = size + new Vector3(0.01f, 0.01f, 0.01f);
            bandSize[axis] = band;
            string key = string.Format(Inv2, "pz_purple_{0:0.##}x{1:0.##}x{2:0.##}_{3}", size.x, size.y, size.z, axis);
            var mesh = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float start = -count * band + band * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    var c = Vector3.zero;
                    c[axis] = start + i * band * 2f;
                    b.AddBox(c, bandSize, Quaternion.Euler(0f, 0f, 0f), Color.white, 0.005f);
                }
                return b.ToMesh(key);
            });
            ProcMesh.Part("Purple", root.transform, mesh, purple, Vector3.zero, null, null, false);
        }
    }
}
