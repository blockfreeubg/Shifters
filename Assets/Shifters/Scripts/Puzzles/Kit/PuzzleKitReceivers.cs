using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static partial class PuzzleKit
    {
        // ================================================================== receivers
        /// <summary>Test-chamber door. pos = bottom center of the doorway, opening spans local X (width), passage along Z.
        /// Frame adds 0.5 m pillars each side and a 0.5 m header. Defaults 3 x 4.2 m (min Tuning.DoorMinWidth/Height).</summary>
        public static Door Door(Transform parent, Vector3 pos, Quaternion? rot, DoorMode mode, float width, float height, params SignalSource[] inputs)
        {
            width = Mathf.Max(width, Tuning.DoorMinWidth);
            height = Mathf.Max(height, Tuning.DoorMinHeight);
            var root = Kit.Make("Door", parent, pos, Rot(rot));
            var t = root.transform;
            float hw = width * 0.5f;
            Kit.SolidBox(t, "PillarL", new Vector3(-hw - 0.25f, (height + 0.5f) * 0.5f, 0f), new Vector3(0.5f, height + 0.5f, 0.9f), MatKey.MetalDark);
            Kit.SolidBox(t, "PillarR", new Vector3(hw + 0.25f, (height + 0.5f) * 0.5f, 0f), new Vector3(0.5f, height + 0.5f, 0.9f), MatKey.MetalDark);
            Kit.SolidBox(t, "Header", new Vector3(0f, height + 0.25f, 0f), new Vector3(width + 1f, 0.5f, 0.9f), MatKey.MetalDark);
            for (int s = -1; s <= 1; s += 2)
                for (int f = -1; f <= 1; f += 2)
                    PuzzleVisuals.Box(t, "Trim", new Vector3(s * (hw + 0.04f), height * 0.5f, f * 0.455f), new Vector3(0.08f, height, 0.02f), MatKey.SafetyOrange, 0.005f, null, false);
            var strips = new Renderer[2];
            for (int f = 0; f < 2; f++)
                strips[f] = PuzzleVisuals.Box(t, "Strip", new Vector3(0f, height + 0.1f, f == 0 ? 0.462f : -0.462f), new Vector3(width - 0.2f, 0.09f, 0.03f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.01f, null, false);
            var stripLamp = PuzzleVisuals.LampFrom(Kit.Make("StripLamp", t, Vector3.zero), PuzzleLampColor.Red, strips);
            if (mode == DoorMode.SlideUp)
            {
                // Gantry: guide rails above the pillars + a cap beam, so the raised panel is visibly held up.
                float railH = height - 0.3f, railY = height + 0.5f + railH * 0.5f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Kit.SolidBox(t, "Rail", new Vector3(s * (hw + 0.14f), railY, 0f), new Vector3(0.28f, railH, 0.55f), MatKey.MetalDark, 0.03f);
                    PuzzleVisuals.Box(t, "RailTrim", new Vector3(s * (hw + 0.14f), railY, 0f), new Vector3(0.08f, railH, 0.57f), MatKey.SafetyOrange, 0.005f, null, false);
                }
                Kit.SolidBox(t, "Cap", new Vector3(0f, 2f * height + 0.33f, 0f), new Vector3(width + 0.56f, 0.26f, 0.6f), MatKey.MetalDark, 0.03f);
            }

            float thick = 0.3f;
            var panel = Kit.Make("Panel", t, new Vector3(0f, height * 0.5f, 0f));
            var mover = panel.AddComponent<MoverBody>();
            mover.Ensure();
            var pc = panel.AddComponent<BoxCollider>();
            pc.size = new Vector3(width, height, thick);
            PuzzleVisuals.Box(panel.transform, "Slab", Vector3.zero, new Vector3(width - 0.02f, height - 0.02f, thick), MatKey.MetalDark, 0.04f);
            PuzzleVisuals.Box(panel.transform, "Inset", Vector3.zero, new Vector3(width * 0.78f, height * 0.82f, thick + 0.03f), MatKey.MetalLight, 0.03f);
            var chev = PuzzleVisuals.ChevronColumnMesh(width * 0.55f, height * 0.62f, 4, 0.03f);
            var chevRot = mode == DoorMode.SlideUp ? Quaternion.identity : Quaternion.Euler(0f, 0f, -90f);
            var chevMat = M(MatKey.HazardYellow);
            if (mode == DoorMode.SlideSide) chev = PuzzleVisuals.ChevronColumnMesh(height * 0.5f, width * 0.6f, 3, 0.03f);
            ProcMesh.Part("ChevronsF", panel.transform, chev, chevMat, new Vector3(0f, 0.05f, thick * 0.5f + 0.03f), chevRot, null, false);
            ProcMesh.Part("ChevronsB", panel.transform, chev, chevMat, new Vector3(0f, 0.05f, -thick * 0.5f - 0.03f), chevRot, null, false);
            PuzzleVisuals.StripedBox(panel.transform, "BottomStripe", new Vector3(0f, -height * 0.5f + 0.12f, 0f), new Vector3(width - 0.04f, 0.2f, thick + 0.04f), 0, 0.3f);

            var door = root.AddComponent<Door>();
            door.Mode = mode; door.Width = width; door.Height = height; door.PanelThickness = thick;
            door.Body = mover;
            door.Axis = mode == DoorMode.SlideUp ? Vector3.up : Vector3.right;
            door.Travel = mode == DoorMode.SlideUp ? height + 0.1f : width + 0.15f;
            door.HomeLocal = new Vector3(0f, height * 0.5f, 0f);
            door.SpeedOn = 3.6f; door.SpeedOff = 2.4f;
            door.Strip = stripLamp;
            return Wire(door, inputs);
        }

        public static Door Door(Transform parent, Vector3 pos, Quaternion? rot = null, params SignalSource[] inputs)
            => Door(parent, pos, rot, DoorMode.SlideUp, 3f, 4.2f, inputs);

        static void BuildDoorRing(Door door)
        {
            var t = door.transform;
            // Two rings (one per side), mounted in front of / behind the header so a rising panel never passes through them.
            var ring = Kit.Make("TimerRing", t, new Vector3(0f, door.Height + 1.05f, 0f)).transform;
            const int n = 12;
            var dots = new Renderer[2, n];
            for (int side = 0; side < 2; side++)
            {
                float sz = side == 0 ? 1f : -1f, z = sz * 0.56f;
                PuzzleVisuals.Box(ring, "Neck", new Vector3(0f, -0.43f, sz * 0.38f), new Vector3(0.3f, 0.26f, 0.36f), MatKey.MetalDark);
                PuzzleVisuals.Cyl(ring, "Plate", new Vector3(0f, 0f, z), 0.62f, 0.14f, MatKey.MetalDark, Quaternion.Euler(90f, 0f, 0f), 24);
                for (int i = 0; i < n; i++)
                {
                    // clockwise from 12 o'clock as seen from this side (a viewer on +Z sees +X on their left)
                    float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / n;
                    var p = new Vector3(-sz * Mathf.Cos(a) * 0.45f, Mathf.Sin(a) * 0.45f, z + sz * 0.08f);
                    dots[side, i] = PuzzleVisuals.Box(ring, side == 0 ? "DotF" : "DotB", p, new Vector3(0.11f, 0.11f, 0.03f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Off), 0.02f, null, false);
                }
            }
            var lamps = new PuzzleLamp[n];
            for (int i = 0; i < n; i++)
                lamps[i] = PuzzleVisuals.LampFrom(Kit.Make("Dot" + i, ring, Vector3.zero), PuzzleLampColor.Off, dots[0, i], dots[1, i]);
            door.RingLamps = lamps;
        }

        /// <summary>Kinematic moving platform. pos = TOP center at the first point; points are offsets of the top center
        /// (local to the platform root; include Vector3.zero as the start). Always moves when no inputs are given.</summary>
        public static MovingPlatform MovingPlatform(Transform parent, Vector3 pos, Quaternion? rot, Vector3[] points, Vector3 size, float speed = 2.5f, float waitTime = 1f, PlatformLoop loop = PlatformLoop.PingPong, params SignalSource[] inputs)
        {
            if (size.sqrMagnitude < 0.01f) size = new Vector3(3f, 0.4f, 3f);
            var root = Kit.Make("MovingPlatform", parent, pos, Rot(rot));
            var body = Kit.Make("Platform", root.transform, points != null && points.Length > 0 ? points[0] : Vector3.zero);
            var mover = body.AddComponent<MoverBody>();
            mover.Ensure();
            var col = body.AddComponent<BoxCollider>();
            col.size = size; col.center = new Vector3(0f, -size.y * 0.5f, 0f);
            BuildSlab(body.transform, size, MatKey.PuzzleBody, true);
            var lamps = new List<Renderer>();
            for (int s = -1; s <= 1; s += 2)
                lamps.Add(PuzzleVisuals.Box(body.transform, "Lamp", new Vector3(s * (size.x * 0.5f + 0.01f), -size.y * 0.5f, 0f), new Vector3(0.04f, 0.12f, 0.5f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Green), 0.01f, null, false));
            var mp = root.AddComponent<MovingPlatform>();
            mp.Points = points != null && points.Length > 0 ? points : new[] { Vector3.zero, new Vector3(0f, 0f, 6f) };
            mp.Speed = speed; mp.WaitTime = waitTime; mp.Loop = loop;
            mp.Body = mover;
            mp.Lamp = PuzzleVisuals.LampFrom(body, PuzzleLampColor.Green, lamps.ToArray());
            mp.PoweredWithoutInputs = inputs == null || inputs.Length == 0;
            return Wire(mp, inputs);
        }

        /// <summary>Teal slab with darker underside + accent border; top surface at local y = 0.</summary>
        static void BuildSlab(Transform t, Vector3 size, MatKey top, bool stripes)
        {
            PuzzleVisuals.Box(t, "Top", new Vector3(0f, -size.y * 0.5f, 0f), size, top, 0.05f);
            PuzzleVisuals.Box(t, "Under", new Vector3(0f, -size.y - 0.08f, 0f), new Vector3(size.x - 0.3f, 0.16f, size.z - 0.3f), MatKey.MetalDark, 0.03f);
            float e = 0.12f;
            PuzzleVisuals.Box(t, "EdgeN", new Vector3(0f, 0.004f, size.z * 0.5f - e * 0.5f), new Vector3(size.x - 0.02f, 0.01f, e), MatKey.PuzzleAccent, 0.003f, null, false);
            PuzzleVisuals.Box(t, "EdgeS", new Vector3(0f, 0.004f, -size.z * 0.5f + e * 0.5f), new Vector3(size.x - 0.02f, 0.01f, e), MatKey.PuzzleAccent, 0.003f, null, false);
            PuzzleVisuals.Box(t, "EdgeE", new Vector3(size.x * 0.5f - e * 0.5f, 0.004f, 0f), new Vector3(e, 0.01f, size.z - 0.02f), MatKey.PuzzleAccent, 0.003f, null, false);
            PuzzleVisuals.Box(t, "EdgeW", new Vector3(-size.x * 0.5f + e * 0.5f, 0.004f, 0f), new Vector3(e, 0.01f, size.z - 0.02f), MatKey.PuzzleAccent, 0.003f, null, false);
            if (stripes)
            {
                PuzzleVisuals.StripedBox(t, "StripeN", new Vector3(0f, -size.y * 0.5f, size.z * 0.5f + 0.01f), new Vector3(size.x - 0.1f, size.y * 0.5f, 0.02f), 0, 0.25f, false);
                PuzzleVisuals.StripedBox(t, "StripeS", new Vector3(0f, -size.y * 0.5f, -size.z * 0.5f - 0.01f), new Vector3(size.x - 0.1f, size.y * 0.5f, 0.02f), 0, 0.25f, false);
            }
        }

        /// <summary>Weight pulley: two platforms 'separation' apart along local X (A at -X, B at +X). Tops move between
        /// floor level (0) and 'travel'. startOffset +travel/2 = A up, B down. The heavier side (by > 10 kg) sinks at 1.5 m/s.</summary>
        public static PulleyLift PulleyLift(Transform parent, Vector3 pos, Quaternion? rot = null, float separation = 6f, float travel = 3f, Vector2? platformSize = null, float? startOffset = null)
        {
            var ps = platformSize ?? new Vector2(2.6f, 2.6f);
            var size = new Vector3(ps.x, 0.3f, ps.y);
            var root = Kit.Make("PulleyLift", parent, pos, Rot(rot));
            var t = root.transform;
            float half = travel * 0.5f, attach = 2.4f;
            float beamY = travel + attach + 1.2f;
            var lift = root.AddComponent<PulleyLift>();
            lift.Travel = travel; lift.Speed = 1.5f; lift.DiffThresholdKg = 10f;
            lift.StartOffset = startOffset ?? half;
            lift.HomeA = new Vector3(-separation * 0.5f, half, 0f);
            lift.HomeB = new Vector3(separation * 0.5f, half, 0f);
            lift.RopeTopY = beamY - 0.2f;
            lift.RopeAttachY = attach;
            var rends = new List<Renderer>();
            MoverBody MakeSide(string n, Vector3 home, float sign, out WeightSensor sensor)
            {
                var p = Kit.Make(n, t, home + Vector3.up * (sign * lift.StartOffset));
                var mb = p.AddComponent<MoverBody>(); mb.Ensure();
                var col = p.AddComponent<BoxCollider>(); col.size = size; col.center = new Vector3(0f, -size.y * 0.5f, 0f);
                BuildSlab(p.transform, size, MatKey.PuzzleBody, true);
                rends.Add(PuzzleVisuals.Box(p.transform, "Yoke", new Vector3(0f, attach, 0f), new Vector3(0.14f, 0.14f, size.z - 0.1f), MatKey.MetalDark));
                for (int s = -1; s <= 1; s += 2)
                    PuzzleVisuals.Box(p.transform, "Rope", new Vector3(0f, attach * 0.5f, s * (size.z * 0.5f - 0.12f)), new Vector3(0.05f, attach, 0.05f), MatKey.Rubber, 0.01f, null, false);
                sensor = p.AddComponent<WeightSensor>();
                sensor.Surfaces = new Collider[] { col };
                return mb;
            }
            lift.PlatformA = MakeSide("PlatformA", lift.HomeA, 1f, out lift.SensorA);
            lift.PlatformB = MakeSide("PlatformB", lift.HomeB, -1f, out lift.SensorB);
            // frame
            Kit.SolidBox(t, "Mast", new Vector3(0f, beamY * 0.5f, 0f), new Vector3(0.4f, beamY, 0.4f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "MastFoot", new Vector3(0f, 0.08f, 0f), new Vector3(0.9f, 0.16f, 0.9f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "Beam", new Vector3(0f, beamY, 0f), new Vector3(separation + 0.8f, 0.35f, 0.35f), MatKey.MetalDark);
            for (int s = -1; s <= 1; s += 2)
                PuzzleVisuals.Cyl(t, "Sheave", new Vector3(s * separation * 0.5f, beamY - 0.05f, 0f), 0.32f, 0.2f, MatKey.MetalLight, Quaternion.Euler(90f, 0f, 0f), 18);
            var wheel = Kit.Make("Wheel", t, new Vector3(0f, beamY + 0.25f, 0f)).transform;
            PuzzleVisuals.Cyl(wheel, "Rim", Vector3.zero, 0.55f, 0.18f, MatKey.SafetyOrange, Quaternion.Euler(90f, 0f, 0f), 20);
            PuzzleVisuals.Box(wheel, "Spoke1", Vector3.zero, new Vector3(1f, 0.1f, 0.22f), MatKey.MetalDark);
            PuzzleVisuals.Box(wheel, "Spoke2", Vector3.zero, new Vector3(0.1f, 1f, 0.22f), MatKey.MetalDark);
            lift.Wheel = wheel;
            var ropeMesh = PuzzleVisuals.Cylinder(0.05f, 1f, 8);
            lift.RopeA = ProcMesh.Part("RopeA", t, ropeMesh, M(MatKey.Rubber), lift.HomeA, null, null, false).transform;
            lift.RopeB = ProcMesh.Part("RopeB", t, ropeMesh, M(MatKey.Rubber), lift.HomeB, null, null, false).transform;
            var dA = PuzzleVisuals.Display(t, "DisplayA", new Vector3(-separation * 0.5f, beamY + 0.75f, 0f), 1.3f, 0.45f);
            lift.ReadoutA = PuzzleVisuals.Text(dA, "Kg", Vector3.zero, Quaternion.identity, "0 KG", 2.8f, UIColors.Text, 1.2f, 0.4f);
            var dB = PuzzleVisuals.Display(t, "DisplayB", new Vector3(separation * 0.5f, beamY + 0.75f, 0f), 1.3f, 0.45f);
            lift.ReadoutB = PuzzleVisuals.Text(dB, "Kg", Vector3.zero, Quaternion.identity, "0 KG", 2.8f, UIColors.Text, 1.2f, 0.4f);
            lift.Reveal = Reveal(root, PerspectiveMask.Strength, "PULLEY", new Vector3(0f, 2f, 0f), true, false, rends.ToArray());
            return lift;
        }

        /// <summary>Elevator car (top at pos.y when down) rising 'height' m while powered. size = car footprint.</summary>
        public static Elevator Elevator(Transform parent, Vector3 pos, Quaternion? rot, float height, Vector2 size, params SignalSource[] inputs)
        {
            if (size.sqrMagnitude < 0.01f) size = new Vector2(3f, 3f);
            var root = Kit.Make("Elevator", parent, pos, Rot(rot));
            var t = root.transform;
            var car = Kit.Make("Car", t, Vector3.zero);
            var mover = car.AddComponent<MoverBody>(); mover.Ensure();
            var s3 = new Vector3(size.x, 0.35f, size.y);
            var col = car.AddComponent<BoxCollider>(); col.size = s3; col.center = new Vector3(0f, -s3.y * 0.5f, 0f);
            BuildSlab(car.transform, s3, MatKey.PuzzleBody, true);
            var lampRends = new List<Renderer>();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Kit.SolidBox(t, "Rail", new Vector3(sx * (size.x * 0.5f + 0.15f), (height + 1.4f) * 0.5f - 0.35f, sz * (size.y * 0.5f - 0.25f)), new Vector3(0.16f, height + 1.4f, 0.16f), MatKey.MetalDark);
                    lampRends.Add(PuzzleVisuals.Box(t, "RailLamp", new Vector3(sx * (size.x * 0.5f + 0.15f), height + 1.1f, sz * (size.y * 0.5f - 0.25f)), new Vector3(0.2f, 0.12f, 0.2f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.02f, null, false));
                }
            var e = root.AddComponent<Elevator>();
            e.Body = mover; e.Axis = Vector3.up; e.Travel = height; e.HomeLocal = Vector3.zero; e.SpeedOn = 2.2f; e.SpeedOff = 2.2f;
            e.Lamp = PuzzleVisuals.LampFrom(Kit.Make("Lamps", t, Vector3.zero), PuzzleLampColor.Red, lampRends.ToArray());
            return Wire(e, inputs);
        }

        /// <summary>Walkway that slides out 'length' m along local +Z from pos (the ledge edge, floor height) while
        /// powered; retracted it hides under the near floor (needs >= 0.3 m thick floor there).</summary>
        public static RetractableBridge RetractableBridge(Transform parent, Vector3 pos, Quaternion? rot, float length, float width, params SignalSource[] inputs)
        {
            var root = Kit.Make("RetractableBridge", parent, pos, Rot(rot));
            var t = root.transform;
            var walk = Kit.Make("Walkway", t, Vector3.zero);
            var mover = walk.AddComponent<MoverBody>(); mover.Ensure();
            var col = walk.AddComponent<BoxCollider>();
            col.size = new Vector3(width, 0.25f, length); col.center = new Vector3(0f, -0.125f, -length * 0.5f);
            PuzzleVisuals.Box(walk.transform, "Deck", new Vector3(0f, -0.125f, -length * 0.5f), new Vector3(width, 0.25f, length), MatKey.MetalLight, 0.04f);
            for (int s = -1; s <= 1; s += 2)
                PuzzleVisuals.Box(walk.transform, "Edge", new Vector3(s * (width * 0.5f - 0.08f), 0.005f, -length * 0.5f), new Vector3(0.12f, 0.012f, length - 0.05f), MatKey.PuzzleBody, 0.003f, null, false);
            PuzzleVisuals.StripedBox(walk.transform, "Tip", new Vector3(0f, -0.12f, -0.1f), new Vector3(width + 0.02f, 0.26f, 0.2f), 0, 0.25f);
            var lampR = new List<Renderer>();
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.Box(t, "Housing", new Vector3(s * (width * 0.5f + 0.2f), 0.15f, -0.2f), new Vector3(0.35f, 0.3f, 0.4f), MatKey.MetalDark);
                lampR.Add(PuzzleVisuals.Box(t, "Lamp", new Vector3(s * (width * 0.5f + 0.2f), 0.32f, -0.2f), new Vector3(0.2f, 0.05f, 0.2f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.01f, null, false));
            }
            var b = root.AddComponent<RetractableBridge>();
            b.Body = mover; b.Axis = Vector3.forward; b.Travel = length; b.HomeLocal = Vector3.zero; b.SpeedOn = 3f; b.SpeedOff = 3f;
            b.Lamp = PuzzleVisuals.LampFrom(Kit.Make("Lamps", t, Vector3.zero), PuzzleLampColor.Red, lampR.ToArray());
            return Wire(b, inputs);
        }

        /// <summary>Conveyor belt, pos = top center, runs along local +Z (speed &lt; 0 reverses). Always on without inputs.</summary>
        public static Conveyor Conveyor(Transform parent, Vector3 pos, Quaternion? rot, float length, float width, float speed, params SignalSource[] inputs)
        {
            var root = Kit.Make("Conveyor", parent, pos, Rot(rot));
            var t = root.transform;
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(width, 0.4f, length); col.center = new Vector3(0f, -0.2f, 0f);
            PuzzleVisuals.Box(t, "Belt", new Vector3(0f, -0.2f, 0f), new Vector3(width, 0.4f, length), MatKey.Rubber, 0.04f);
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.Box(t, "Rail", new Vector3(s * (width * 0.5f + 0.1f), -0.2f, 0f), new Vector3(0.2f, 0.5f, length + 0.2f), MatKey.MetalDark, 0.03f);
                PuzzleVisuals.StripedBox(t, "RailStripe", new Vector3(s * (width * 0.5f + 0.1f), 0.06f, 0f), new Vector3(0.21f, 0.03f, length + 0.2f), 2, 0.3f, false);
                PuzzleVisuals.Cyl(t, "Roller", new Vector3(0f, -0.2f, s * length * 0.5f), 0.2f, width + 0.1f, MatKey.MetalLight, Quaternion.Euler(0f, 0f, 90f), 14);
            }
            int n = Mathf.Max(2, Mathf.CeilToInt(length / 0.5f));
            float spacing = length / n;
            var slats = new Transform[n];
            for (int i = 0; i < n; i++)
                slats[i] = PuzzleVisuals.Box(t, "Slat", new Vector3(0f, 0.008f, -length * 0.5f + i * spacing), new Vector3(width - 0.12f, 0.02f, 0.1f), MatKey.MetalLight, 0.005f, null, false).transform;
            var arrowMesh = PuzzleVisuals.FloorChevronMesh(0.5f);
            var arrows = new List<Renderer>();
            float dir = speed < 0f ? -1f : 1f;
            for (int s = -1; s <= 1; s += 2)
                for (int k = -1; k <= 1; k++)
                    arrows.Add(ProcMesh.Part("Arrow", t, arrowMesh, M(MatKey.LightBlue), new Vector3(s * (width * 0.5f + 0.1f), 0.085f, k * length * 0.3f), Quaternion.Euler(0f, dir > 0f ? 0f : 180f, 0f), new Vector3(0.35f, 1f, 0.35f), false).GetComponent<Renderer>());
            var c = root.AddComponent<Conveyor>();
            c.Speed = Mathf.Abs(speed); c.Direction = dir; c.Length = length;
            c.Slats = slats; c.SlatSpacing = spacing;
            c.Lamp = PuzzleVisuals.LampFrom(Kit.Make("Arrows", t, Vector3.zero), PuzzleLampColor.Blue, arrows.ToArray());
            c.PoweredWithoutInputs = inputs == null || inputs.Length == 0;
            return Wire(c, inputs);
        }

        /// <summary>Logic-only receiver that switches hazards (default: powered = hazards OFF).</summary>
        public static HazardToggle HazardToggle(Transform parent, Behaviour[] targets, bool hazardOnWhenPowered, params SignalSource[] inputs)
        {
            var root = Kit.Make("HazardToggle", parent, Vector3.zero);
            var h = root.AddComponent<HazardToggle>();
            h.Targets = targets ?? new Behaviour[0];
            h.HazardOnWhenPowered = hazardOnWhenPowered;
            return Wire(h, inputs);
        }

        /// <summary>Status lamp on a 1.6 m post (or a wall plate facing +Z when wallMounted). Green when powered.</summary>
        public static IndicatorLight IndicatorLight(Transform parent, Vector3 pos, Quaternion? rot, bool wallMounted, params SignalSource[] inputs)
        {
            var root = Kit.Make("IndicatorLight", parent, pos, Rot(rot));
            var t = root.transform;
            Renderer[] bulbs;
            if (wallMounted)
            {
                PuzzleVisuals.Box(t, "Plate", Vector3.zero, new Vector3(0.4f, 0.4f, 0.06f), MatKey.MetalDark);
                bulbs = new Renderer[] { PuzzleVisuals.Box(t, "Bulb", new Vector3(0f, 0f, 0.06f), new Vector3(0.24f, 0.24f, 0.08f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.05f, null, false) };
            }
            else
            {
                PuzzleVisuals.Box(t, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(0.35f, 0.08f, 0.35f), MatKey.MetalDark);
                PuzzleVisuals.Cyl(t, "Post", new Vector3(0f, 0.8f, 0f), 0.04f, 1.6f, MatKey.MetalDark, null, 10);
                PuzzleVisuals.Box(t, "Head", new Vector3(0f, 1.7f, 0f), new Vector3(0.32f, 0.3f, 0.32f), MatKey.PuzzleBody);
                bulbs = new Renderer[] { PuzzleVisuals.Box(t, "Bulb", new Vector3(0f, 1.9f, 0f), new Vector3(0.24f, 0.16f, 0.24f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.05f, null, false) };
            }
            var il = root.AddComponent<IndicatorLight>();
            il.Lamp = PuzzleVisuals.LampFrom(Kit.Make("Lamp", t, Vector3.zero), PuzzleLampColor.Red, bulbs);
            return Wire(il, inputs);
        }
    }
}
