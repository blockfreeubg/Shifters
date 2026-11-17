using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Runtime/edit-mode factories for every puzzle element, prop and hazard (Kit conventions, see Core/Kit.cs).
    /// Conventions: pos/rot are LOCAL to parent (world if parent is null); pos is the element's floor contact point
    /// (bottom center) unless noted; interactables face local +Z (stand on their +Z side). Receivers take their
    /// inputs as the trailing params; use Wire() to rewire later and Cable() to draw glowing power conduits.
    /// </summary>
    public static partial class PuzzleKit
    {
        static Material M(MatKey k) => Kit.Mat(k);
        static Quaternion Rot(Quaternion? r) => r ?? Quaternion.identity;

        // ================================================================== wiring
        /// <summary>Set a receiver's inputs (re-subscribes at runtime; safe in edit mode). A Door fed by a TimedButton
        /// gets a countdown ring.</summary>
        public static T Wire<T>(T receiver, params SignalSource[] inputs) where T : SignalReceiver
        {
            if (receiver == null) return null;
            bool was = receiver.enabled;
            receiver.enabled = false;
            receiver.Inputs = inputs ?? new SignalSource[0];
            if (receiver is Door door)
                foreach (var s in receiver.Inputs)
                    if (s is TimedButton tb) { door.Timer = tb; if (door.RingLamps.Length == 0) BuildDoorRing(door); break; }
            receiver.enabled = was;
            return receiver;
        }

        /// <summary>Append one more input to a receiver.</summary>
        public static T Connect<T>(SignalSource source, T receiver) where T : SignalReceiver
        {
            if (receiver == null || source == null) return receiver;
            var list = new List<SignalSource>(receiver.Inputs ?? new SignalSource[0]);
            if (!list.Contains(source)) list.Add(source);
            return Wire(receiver, list.ToArray());
        }

        /// <summary>Glowing floor conduit between a source and any target transform (both at their floor points).</summary>
        public static PowerCable CableBetween(Transform parent, SignalSource source, Transform target, float sideOffset = 0f)
        {
            if (source == null || target == null) return null;
            Vector3 a = source.transform.position, b = target.position;
            if (sideOffset != 0f) { a += source.transform.right * sideOffset; }
            if (parent != null) { a = parent.InverseTransformPoint(a); b = parent.InverseTransformPoint(b); }
            return Cable(parent, a, b, source);
        }

        /// <summary>L-shaped floor conduit from 'from' to 'to' (parent space; along X first, then Z, then up if 'to' is
        /// higher) that glows LightBlue with a running pulse while 'source' is on.</summary>
        public static PowerCable Cable(Transform parent, Vector3 from, Vector3 to, SignalSource source)
        {
            var root = Kit.Make("PowerCable", parent, Vector3.zero);
            const float lift = 0.03f;
            var pts = new List<Vector3> { from + Vector3.up * lift };
            var corner = new Vector3(to.x, from.y + lift, from.z);
            var end = new Vector3(to.x, from.y + lift, to.z);
            if ((corner - pts[pts.Count - 1]).sqrMagnitude > 0.0025f) pts.Add(corner);
            if ((end - pts[pts.Count - 1]).sqrMagnitude > 0.0025f) pts.Add(end);
            if (to.y - from.y > 0.1f) pts.Add(new Vector3(to.x, to.y, to.z));
            if (pts.Count < 2) pts.Add(pts[0] + Vector3.forward * 0.1f);

            var cores = new List<Renderer>();
            var offMat = M(MatKey.Rubber);
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i];
                var d = b - a; float len = d.magnitude;
                if (len < 0.01f) continue;
                var dir = d / len;
                var rot = Mathf.Abs(dir.y) > 0.9f ? Quaternion.LookRotation(dir, Vector3.forward) : Quaternion.LookRotation(dir, Vector3.up);
                var mid = (a + b) * 0.5f;
                PuzzleVisuals.Box(root.transform, "Conduit", mid, new Vector3(0.16f, 0.06f, len + 0.08f), MatKey.MetalDark, 0.02f, rot, false);
                cores.Add(PuzzleVisuals.Box(root.transform, "Core", mid + rot * new Vector3(0f, 0.032f, 0f), new Vector3(0.06f, 0.02f, len + 0.04f), offMat, 0.005f, rot, false));
                if (i < pts.Count - 1) PuzzleVisuals.Box(root.transform, "Junction", b, new Vector3(0.22f, 0.09f, 0.22f), MatKey.MetalDark, 0.02f, null, false);
            }
            var pulse = ProcMesh.Part("Pulse", root.transform, PuzzleVisuals.Sphere(0.07f), M(MatKey.LightOn), pts[0] + Vector3.up * 0.05f, null, null, false);
            var path = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) path[i] = pts[i] + Vector3.up * 0.05f;
            pulse.SetActive(false);
            var cable = root.AddComponent<PowerCable>();
            cable.Cores = cores.ToArray();
            cable.OnMaterial = M(MatKey.LightBlue);
            cable.OffMaterial = offMat;
            cable.Path = path;
            cable.Pulse = pulse.transform;
            cable.SetSource(source);
            return cable;
        }

        // ================================================================== shared builders
        static Revealable Reveal(GameObject go, PerspectiveMask cat, string label, Vector3 labelOffset, bool highlight = true, bool showOnly = false, Renderer[] renderers = null)
        {
            var r = go.AddComponent<Revealable>();
            r.Category = cat;
            r.Highlight = highlight;
            r.ShowOnlyWhenRevealed = showOnly;
            r.Label = label ?? "";
            r.LabelOffset = labelOffset;
            r.Renderers = renderers ?? new Renderer[0];
            return r;
        }

        static TextMeshPro FaceText(Transform parent, string name, Vector3 pos, Vector3 outward, string text, float size, Color color, float w = 1.5f, float h = 0.5f)
            => PuzzleVisuals.Text(parent, name, pos + outward.normalized * 0.012f, PuzzleVisuals.Facing(outward), text, size, color, w, h);

        /// <summary>Plate frame + sinking pad. Returns pad transform and collider.</summary>
        static Transform PlateBody(Transform root, Vector2 size, out BoxCollider padCollider, out PuzzleLamp lamp, List<Renderer> rends)
        {
            float fx = size.x, fz = size.y, rim = 0.16f, h = 0.14f;
            rends.Add(PuzzleVisuals.Box(root, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(fx, 0.04f, fz), MatKey.MetalDark, 0.01f));
            rends.Add(PuzzleVisuals.Box(root, "RimN", new Vector3(0f, h * 0.5f, fz * 0.5f - rim * 0.5f), new Vector3(fx, h, rim), MatKey.MetalDark, 0.03f));
            rends.Add(PuzzleVisuals.Box(root, "RimS", new Vector3(0f, h * 0.5f, -fz * 0.5f + rim * 0.5f), new Vector3(fx, h, rim), MatKey.MetalDark, 0.03f));
            rends.Add(PuzzleVisuals.Box(root, "RimE", new Vector3(fx * 0.5f - rim * 0.5f, h * 0.5f, 0f), new Vector3(rim, h, fz - rim * 2f), MatKey.MetalDark, 0.03f));
            rends.Add(PuzzleVisuals.Box(root, "RimW", new Vector3(-fx * 0.5f + rim * 0.5f, h * 0.5f, 0f), new Vector3(rim, h, fz - rim * 2f), MatKey.MetalDark, 0.03f));
            var bulbs = new List<Renderer>();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    bulbs.Add(PuzzleVisuals.Box(root, "Lamp", new Vector3(sx * (fx * 0.5f - rim * 0.5f), h + 0.015f, sz * (fz * 0.5f - rim * 0.5f)), new Vector3(0.1f, 0.04f, 0.1f), PuzzleLamp.DefaultMaterial(PuzzleLampColor.Red), 0.015f, null, false));
            var pad = Kit.Make("Pad", root, new Vector3(0f, h - 0.06f, 0f));
            rends.Add(PuzzleVisuals.Box(pad.transform, "PadTop", Vector3.zero, new Vector3(fx - rim * 2f - 0.04f, 0.12f, fz - rim * 2f - 0.04f), MatKey.PuzzleBody, 0.03f));
            rends.Add(PuzzleVisuals.Box(pad.transform, "PadInset", new Vector3(0f, 0.062f, 0f), new Vector3(fx * 0.5f, 0.006f, fz * 0.5f), MatKey.PuzzleAccent, 0.002f, null, false));
            padCollider = pad.AddComponent<BoxCollider>();
            padCollider.size = new Vector3(fx - 0.02f, 0.12f, fz - 0.02f);
            lamp = PuzzleVisuals.LampFrom(root.gameObject, PuzzleLampColor.Red, bulbs.ToArray());
            return pad.transform;
        }

        static T BuildPlate<T>(string name, Transform parent, Vector3 pos, Quaternion rot, float requiredKg, bool latching, string solvedMessage) where T : PressurePlate
        {
            var root = Kit.Make(name, parent, pos, rot);
            var rends = new List<Renderer>();
            var pad = PlateBody(root.transform, new Vector2(2f, 2f), out var padCol, out var lamp, rends);
            var sensor = root.AddComponent<WeightSensor>();
            sensor.Surfaces = new Collider[] { padCol };
            var plate = root.AddComponent<T>();
            plate.RequiredKg = requiredKg;
            plate.Latching = latching;
            plate.SolvedMessage = solvedMessage ?? "";
            plate.Sensor = sensor;
            plate.Pad = pad;
            plate.Lamp = lamp;
            PuzzleVisuals.TwoLineDisplay(root.transform, new Vector3(0f, 2.75f, 0f), 1.7f, "", "0 KG", out plate.RequirementText, out plate.LoadText);
            plate.Reveal = Reveal(root, PerspectiveMask.Strength, "PLATE", new Vector3(0f, 1.4f, 0f), true, false, rends.ToArray());
            return plate;
        }

        // ================================================================== sources
        /// <summary>2x2 m weight plate (top at +0.14). ON while >= requiredKg rests on it (stacks count). Tiers: Tuning.PlateLight/Medium/Heavy/Team.</summary>
        public static PressurePlate PressurePlate(Transform parent, Vector3 pos, Quaternion? rot = null, float requiredKg = Tuning.PlateHeavy, bool latching = false, string solvedMessage = "")
        {
            var p = BuildPlate<PressurePlate>("PressurePlate", parent, pos, Rot(rot), requiredKg, latching, solvedMessage);
            p.RequirementText.text = p.RequirementString();
            return p;
        }

        /// <summary>2x2 m plate that needs minKg..maxKg ("200-270 KG"); too heavy blinks + toasts.</summary>
        public static WeightRangePlate WeightRangePlate(Transform parent, Vector3 pos, Quaternion? rot = null, float minKg = 200f, float maxKg = 270f, bool latching = false, string solvedMessage = "")
        {
            var p = BuildPlate<WeightRangePlate>("WeightRangePlate", parent, pos, Rot(rot), minKg, latching, solvedMessage);
            p.MaxKg = maxKg;
            p.RequirementText.text = p.RequirementString();
            return p;
        }

        /// <summary>Two 2x2 pads 'separation' apart along local X (A = -X, B = +X) with a tilting scale between them.
        /// ON while both carry >= minKg and differ by &lt;= toleranceKg.</summary>
        public static WeightBalance WeightBalance(Transform parent, Vector3 pos, Quaternion? rot = null, float separation = 5f, float toleranceKg = 10f, float minKg = 40f)
        {
            var root = Kit.Make("WeightBalance", parent, pos, Rot(rot));
            var rends = new List<Renderer>();
            WeightSensor MakePad(string n, float x)
            {
                var g = Kit.Make(n, root.transform, new Vector3(x, 0f, 0f));
                PlateBody(g.transform, new Vector2(2f, 2f), out var col, out var l, rends);
                l.Set(PuzzleLampColor.Blue);
                var s = g.AddComponent<WeightSensor>();
                s.Surfaces = new Collider[] { col };
                return s;
            }
            var a = MakePad("PadA", -separation * 0.5f);
            var b = MakePad("PadB", separation * 0.5f);
            var t = root.transform;
            rends.Add(PuzzleVisuals.Box(t, "ScaleBase", new Vector3(0f, 0.06f, 0f), new Vector3(0.9f, 0.12f, 0.9f), MatKey.MetalDark));
            Kit.SolidBox(t, "Pillar", new Vector3(0f, 1f, 0f), new Vector3(0.3f, 1.9f, 0.3f), MatKey.PuzzleBody);
            var beam = Kit.Make("Beam", t, new Vector3(0f, 2.05f, 0f)).transform;
            rends.Add(PuzzleVisuals.Box(beam, "Bar", Vector3.zero, new Vector3(2.6f, 0.12f, 0.16f), MatKey.PuzzleAccent));
            for (int s = -1; s <= 1; s += 2)
            {
                rends.Add(PuzzleVisuals.Box(beam, "String", new Vector3(s * 1.2f, -0.25f, 0f), new Vector3(0.03f, 0.5f, 0.03f), MatKey.MetalDark, 0.005f));
                rends.Add(PuzzleVisuals.Cyl(beam, "Pan", new Vector3(s * 1.2f, -0.52f, 0f), 0.3f, 0.06f, MatKey.MetalLight));
            }
            rends.Add(PuzzleVisuals.Cyl(t, "Pivot", new Vector3(0f, 2.05f, 0f), 0.12f, 0.3f, MatKey.MetalDark, Quaternion.Euler(90f, 0f, 0f)));
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(0f, 1.4f, 0.16f), Quaternion.identity, 0.16f);
            var disp = PuzzleVisuals.Display(t, "Display", new Vector3(0f, 2.95f, 0f), 2.1f, 0.62f);
            var title = PuzzleVisuals.Text(disp, "Title", new Vector3(0f, 0.14f, 0f), Quaternion.identity, "BALANCE ±" + Mathf.RoundToInt(toleranceKg) + " KG", 2.4f, UIColors.AccentYellow, 2f, 0.3f);
            var readout = PuzzleVisuals.Text(disp, "Readout", new Vector3(0f, -0.14f, 0f), Quaternion.identity, "0  |  0 KG", 2.6f, UIColors.Text, 2f, 0.3f);
            var wb = root.AddComponent<WeightBalance>();
            wb.SensorA = a; wb.SensorB = b; wb.ToleranceKg = toleranceKg; wb.MinKg = minKg;
            wb.Beam = beam; wb.Lamp = lamp; wb.Readout = readout;
            wb.Reveal = Reveal(root, PerspectiveMask.Strength, "BALANCE", new Vector3(0f, 1.2f, 0f), true, false, rends.ToArray());
            return wb;
        }

        /// <summary>Floor lever (~1.2 m). requiredStrength > 0 (use Tuning.RustyLeverStrength) = rusty lever.</summary>
        public static Lever Lever(Transform parent, Vector3 pos, Quaternion? rot = null, int requiredStrength = 0, bool oneShot = false, bool startsOn = false, string solvedMessage = "")
        {
            bool rusty = requiredStrength > 0;
            var root = Kit.Make(rusty ? "RustyLever" : "Lever", parent, pos, Rot(rot));
            var t = root.transform;
            var body = rusty ? MatKey.MetalRust : MatKey.PuzzleBody;
            var rends = new List<Renderer>();
            rends.Add(Kit.SolidBox(t, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.55f), body).GetComponent<Renderer>());
            rends.Add(PuzzleVisuals.Box(t, "Top", new Vector3(0f, 0.415f, 0f), new Vector3(0.62f, 0.04f, 0.47f), rusty ? MatKey.MetalDark : MatKey.PuzzleAccent));
            PuzzleVisuals.Box(t, "Slot", new Vector3(0f, 0.437f, 0f), new Vector3(0.12f, 0.01f, 0.42f), MatKey.Black, 0.003f, null, false);
            var handle = Kit.Make("Handle", t, new Vector3(0f, 0.42f, 0f)).transform;
            rends.Add(PuzzleVisuals.Cyl(handle, "Shaft", new Vector3(0f, 0.4f, 0f), 0.045f, 0.8f, rusty ? MatKey.MetalRust : MatKey.MetalLight));
            rends.Add(ProcMesh.Part("Knob", handle, PuzzleVisuals.Sphere(0.11f), M(rusty ? MatKey.SafetyOrange : MatKey.WarningRed), new Vector3(0f, 0.82f, 0f)).GetComponent<Renderer>());
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(0f, rusty ? 0.3f : 0.22f, 0.28f), Quaternion.identity, 0.13f);
            if (rusty) FaceText(t, "StrText", new Vector3(0f, 0.12f, 0.275f), Vector3.forward, "STR " + requiredStrength, 1.6f, UIColors.Strength, 0.6f, 0.2f);
            var lever = root.AddComponent<Lever>();
            lever.RequiredStrength = requiredStrength;
            lever.OneShot = oneShot;
            lever.StartsOn = startsOn;
            lever.SolvedMessage = solvedMessage ?? "";
            lever.Handle = handle;
            lever.Lamp = lamp;
            if (rusty) lever.Reveal = Reveal(root, PerspectiveMask.Strength, "RUSTY LEVER - STR " + requiredStrength, new Vector3(0f, 1.6f, 0f), true, false, rends.ToArray());
            handle.localRotation = Quaternion.Euler(startsOn ? lever.OnAngle : lever.OffAngle, 0f, 0f);
            return lever;
        }

        static Transform Pedestal(Transform t, MatKey capMat, bool timed, out Transform cap, List<Renderer> rends)
        {
            rends.Add(Kit.SolidBox(t, "Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.5f), MatKey.PuzzleBody).GetComponent<Renderer>());
            rends.Add(PuzzleVisuals.Box(t, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(0.66f, 0.08f, 0.66f), MatKey.MetalDark));
            if (timed) PuzzleVisuals.StripedBox(t, "Band", new Vector3(0f, 0.82f, 0f), new Vector3(0.53f, 0.1f, 0.53f), 0, 0.09f);
            else rends.Add(PuzzleVisuals.Box(t, "Band", new Vector3(0f, 0.82f, 0f), new Vector3(0.53f, 0.07f, 0.53f), MatKey.PuzzleAccent));
            rends.Add(PuzzleVisuals.Box(t, "TopPlate", new Vector3(0f, 1.02f, 0f), new Vector3(0.6f, 0.05f, 0.6f), MatKey.MetalDark));
            cap = Kit.Make("Cap", t, new Vector3(0f, 1.09f, 0f)).transform;
            rends.Add(PuzzleVisuals.Cyl(cap, "Button", Vector3.zero, 0.17f, 0.1f, capMat, null, 18));
            rends.Add(PuzzleVisuals.Cyl(t, "Collar", new Vector3(0f, 1.055f, 0f), 0.21f, 0.03f, MatKey.HazardBlack, null, 18));
            return t;
        }

        /// <summary>Pedestal push button (~1.1 m). Momentary: ON for 'duration' seconds per press.</summary>
        public static PushButton PushButton(Transform parent, Vector3 pos, Quaternion? rot = null, float duration = 3f, string solvedMessage = "")
        {
            var root = Kit.Make("PushButton", parent, pos, Rot(rot));
            var rends = new List<Renderer>();
            Pedestal(root.transform, MatKey.WarningRed, false, out var cap, rends);
            var lamp = PuzzleVisuals.Lamp(root.transform, "Lamp", new Vector3(0f, 0.62f, 0.255f), Quaternion.identity, 0.14f);
            var b = root.AddComponent<PushButton>();
            b.Duration = duration; b.Cap = cap; b.Lamp = lamp; b.SolvedMessage = solvedMessage ?? "";
            return b;
        }

        /// <summary>Timed button: window = Tuning.TimedWindow(routeLength, fastest sprint). Optional 'route' (parent-space
        /// floor points) draws Speed-perspective turbo chevrons; routeLength &lt;= 0 = measured from the route.</summary>
        public static TimedButton TimedButton(Transform parent, Vector3 pos, Quaternion? rot = null, float routeLength = 24f, Vector3[] route = null, string solvedMessage = "")
        {
            var root = Kit.Make("TimedButton", parent, pos, Rot(rot));
            var rends = new List<Renderer>();
            Pedestal(root.transform, MatKey.SafetyOrange, true, out var cap, rends);
            var lamp = PuzzleVisuals.Lamp(root.transform, "Lamp", new Vector3(0f, 0.62f, 0.255f), Quaternion.identity, 0.14f);
            FaceText(root.transform, "Label", new Vector3(0f, 0.38f, 0.25f), Vector3.forward, "TIMED", 1.3f, UIColors.Speed, 0.5f, 0.2f);
            if (route != null && route.Length > 1 && routeLength <= 0f)
            {
                routeLength = 0f;
                for (int i = 1; i < route.Length; i++) routeLength += Vector3.Distance(route[i - 1], route[i]);
            }
            var b = root.AddComponent<TimedButton>();
            b.RouteLength = Mathf.Max(1f, routeLength);
            b.Cap = cap; b.Lamp = lamp; b.SolvedMessage = solvedMessage ?? "";
            b.Reveal = Reveal(root, PerspectiveMask.Speed, "TURBO RUN", new Vector3(0f, 1.9f, 0f), true, false, rends.ToArray());
            if (route != null && route.Length > 1) TurboRoute(parent, route);
            return b;
        }

        /// <summary>Speed-perspective floor chevrons along a polyline (parent-space points). Only visible to TURBO LEGS.</summary>
        public static Revealable TurboRoute(Transform parent, Vector3[] points, float spacing = 1.4f)
        {
            var root = Kit.Make("TurboRoute", parent, Vector3.zero);
            var mesh = PuzzleVisuals.FloorChevronMesh(0.9f);
            var mat = M(MatKey.LightBlue);
            var rends = new List<Renderer>();
            float carry = 0.6f;
            for (int i = 1; i < points.Length; i++)
            {
                var a = points[i - 1]; var b = points[i];
                var d = b - a; d.y = 0f;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                var dir = d / len;
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                for (float s = carry; s < len; s += spacing)
                    rends.Add(ProcMesh.Part("Chevron", root.transform, mesh, mat, Vector3.Lerp(a, b, s / len) + Vector3.up * 0.03f, rot, null, false).GetComponent<Renderer>());
                carry = spacing - ((len - carry) % spacing);
            }
            return Reveal(root, PerspectiveMask.Speed, "", Vector3.up, true, true, rends.ToArray());
        }

        /// <summary>Bullseye remote switch (IShootable). onPost: 1.75 m post; else wall-mounted (disc centered on pos, facing +Z).</summary>
        public static ShootTarget ShootTarget(Transform parent, Vector3 pos, Quaternion? rot = null, ShootTargetMode mode = ShootTargetMode.Latch, float momentarySeconds = 4f, bool onPost = true, string solvedMessage = "")
        {
            var root = Kit.Make("ShootTarget", parent, pos, Rot(rot));
            var t = root.transform;
            float discY = onPost ? 1.75f : 0f;
            if (onPost)
            {
                PuzzleVisuals.Box(t, "Foot", new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.1f, 0.6f), MatKey.MetalDark);
                PuzzleVisuals.Cyl(t, "Post", new Vector3(0f, 0.85f, 0f), 0.06f, 1.6f, MatKey.MetalDark);
            }
            var disc = Kit.Make("Disc", t, new Vector3(0f, discY, 0f)).transform;
            var face = Quaternion.Euler(90f, 0f, 0f);
            var rends = new List<Renderer>
            {
                PuzzleVisuals.Cyl(disc, "Back", new Vector3(0f, 0f, -0.03f), 0.5f, 0.06f, MatKey.MetalDark, face, 24),
                PuzzleVisuals.Cyl(disc, "Ring1", new Vector3(0f, 0f, 0f), 0.45f, 0.07f, MatKey.WarningRed, face, 24),
                PuzzleVisuals.Cyl(disc, "Ring2", new Vector3(0f, 0f, 0.01f), 0.33f, 0.08f, MatKey.SignWhite, face, 24),
                PuzzleVisuals.Cyl(disc, "Ring3", new Vector3(0f, 0f, 0.02f), 0.21f, 0.09f, MatKey.WarningRed, face, 20),
                PuzzleVisuals.Cyl(disc, "Bull", new Vector3(0f, 0f, 0.03f), 0.08f, 0.1f, MatKey.HazardYellow, face, 16)
            };
            var col = disc.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(0.95f, 0.95f, 0.16f);
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", onPost ? new Vector3(0f, 1.12f, 0.07f) : new Vector3(0f, -0.66f, 0.03f), Quaternion.identity, 0.12f);
            var st = root.AddComponent<ShootTarget>();
            st.Mode = mode; st.MomentarySeconds = momentarySeconds; st.Disc = disc; st.Lamp = lamp; st.SolvedMessage = solvedMessage ?? "";
            st.Reveal = Reveal(root, PerspectiveMask.Weapon, "SHOOT", new Vector3(0f, discY + 0.8f, 0f), true, false, rends.ToArray());
            return st;
        }

        /// <summary>Hackable terminal console (~1.4 m), screen faces +Z. Needs IQ >= requiredIQ (default Tuning.TerminalIQ).</summary>
        public static Terminal Terminal(Transform parent, Vector3 pos, Quaternion? rot = null, int requiredIQ = Tuning.TerminalIQ, bool latching = true, float onSeconds = 6f, string solvedMessage = "")
        {
            var root = Kit.Make("Terminal", parent, pos, Rot(rot));
            var t = root.transform;
            var rends = new List<Renderer>();
            rends.Add(Kit.SolidBox(t, "Cabinet", new Vector3(0f, 0.43f, -0.05f), new Vector3(0.9f, 0.86f, 0.55f), MatKey.PuzzleBody).GetComponent<Renderer>());
            rends.Add(PuzzleVisuals.Box(t, "Kick", new Vector3(0f, 0.05f, -0.03f), new Vector3(0.94f, 0.1f, 0.58f), MatKey.MetalDark));
            rends.Add(PuzzleVisuals.Box(t, "Desk", new Vector3(0f, 0.89f, 0.04f), new Vector3(1f, 0.06f, 0.72f), MatKey.MetalDark));
            rends.Add(PuzzleVisuals.Box(t, "Keyboard", new Vector3(0f, 0.935f, 0.2f), new Vector3(0.6f, 0.03f, 0.2f), MatKey.Black, 0.01f, Quaternion.Euler(-6f, 0f, 0f)));
            var mon = Kit.Make("Monitor", t, new Vector3(0f, 1.15f, -0.12f), Quaternion.Euler(-12f, 0f, 0f)).transform;
            rends.Add(PuzzleVisuals.Box(mon, "Housing", Vector3.zero, new Vector3(0.82f, 0.5f, 0.12f), MatKey.MetalDark));
            PuzzleVisuals.Box(mon, "Glass", new Vector3(0f, 0f, 0.062f), new Vector3(0.72f, 0.4f, 0.01f), MatKey.Screen, 0.004f, null, false);
            var screen = PuzzleVisuals.Text(mon, "ScreenText", new Vector3(0f, 0f, 0.072f), PuzzleVisuals.Facing(Vector3.forward), "LOCKED", 1.25f, UIColors.Info, 0.68f, 0.38f);
            screen.textWrappingMode = TextWrappingModes.Normal;
            PuzzleVisuals.Cyl(t, "Antenna", new Vector3(0.32f, 1.55f, -0.16f), 0.015f, 0.36f, MatKey.MetalDark, null, 8);
            var lamp = PuzzleVisuals.Lamp(mon, "Lamp", new Vector3(0.3f, 0.29f, 0.02f), Quaternion.Euler(-90f, 0f, 0f), 0.08f);
            FaceText(t, "IQText", new Vector3(0f, 0.62f, 0.226f), Vector3.forward, "IQ " + requiredIQ, 2f, UIColors.Intellect, 0.7f, 0.25f);
            var term = root.AddComponent<Terminal>();
            term.RequiredIQ = requiredIQ; term.Latching = latching; term.OnSeconds = onSeconds; term.SolvedMessage = solvedMessage ?? "";
            term.Screen = screen; term.Lamp = lamp;
            term.Reveal = Reveal(root, PerspectiveMask.Intellect, "TERMINAL - IQ " + requiredIQ, new Vector3(0f, 1.9f, 0f), true, false, rends.ToArray());
            return term;
        }

        /// <summary>Logic box on a post (~1.6 m): combines inputs (All=AND, Any=OR, Xor, Toggle, Latch; not = invert).</summary>
        public static LogicGate LogicGate(Transform parent, Vector3 pos, Quaternion? rot, SignalLogic logic, bool not, params SignalSource[] inputs)
        {
            var root = Kit.Make("LogicGate", parent, pos, Rot(rot));
            var t = root.transform;
            var rends = new List<Renderer>();
            PuzzleVisuals.Cyl(t, "Post", new Vector3(0f, 0.55f, 0f), 0.05f, 1.1f, MatKey.MetalDark);
            PuzzleVisuals.Box(t, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(0.4f, 0.08f, 0.4f), MatKey.MetalDark);
            rends.Add(Kit.SolidBox(t, "Box", new Vector3(0f, 1.32f, 0f), new Vector3(0.8f, 0.5f, 0.3f), MatKey.PuzzleBody).GetComponent<Renderer>());
            PuzzleVisuals.Box(t, "Face", new Vector3(0f, 1.3f, 0.151f), new Vector3(0.66f, 0.3f, 0.01f), MatKey.Black, 0.004f, null, false);
            int n = inputs != null ? inputs.Length : 0;
            PuzzleVisuals.Box(t, "FaceBack", new Vector3(0f, 1.3f, -0.151f), new Vector3(0.66f, 0.3f, 0.01f), MatKey.Black, 0.004f, null, false);
            var sym = FaceText(t, "Symbol", new Vector3(0f, 1.3f, 0.157f), Vector3.forward, Shifters.LogicGate.SymbolFor(logic, not, n), 2.4f, UIColors.Intellect, 0.62f, 0.28f);
            var symBack = FaceText(t, "SymbolBack", new Vector3(0f, 1.3f, -0.157f), Vector3.back, Shifters.LogicGate.SymbolFor(logic, not, n), 2.4f, UIColors.Intellect, 0.62f, 0.28f);
            var inLamps = new PuzzleLamp[Mathf.Min(n, 6)];
            for (int i = 0; i < inLamps.Length; i++)
                inLamps[i] = PuzzleVisuals.Lamp(t, "In" + i, new Vector3((i - (inLamps.Length - 1) * 0.5f) * 0.12f, 1.6f, 0.08f), Quaternion.Euler(-90f, 0f, 0f), 0.07f, PuzzleLampColor.Off);
            var outLamp = PuzzleVisuals.Lamp(t, "Out", new Vector3(0.405f, 1.32f, 0f), Quaternion.Euler(0f, 90f, 0f), 0.12f);
            var g = root.AddComponent<LogicGate>();
            g.Logic = logic; g.Not = not; g.Symbol = sym; g.SymbolBack = symBack; g.OutputLamp = outLamp; g.InputLamps = inLamps;
            g.Reveal = Reveal(root, PerspectiveMask.Intellect, "LOGIC", new Vector3(0f, 2f, 0f), true, false, rends.ToArray());
            g.SetInputs(inputs);
            return g;
        }

        /// <summary>Sequence lock panel on a post with one progress lamp per step. showClues: IQ-only floating numbers
        /// over each input ("1", "2", "1 3" for repeated inputs).</summary>
        public static SequenceLock SequenceLock(Transform parent, Vector3 pos, Quaternion? rot, SignalSource[] order, bool showClues = true, string solvedMessage = "")
        {
            order = order ?? new SignalSource[0];
            var root = Kit.Make("SequenceLock", parent, pos, Rot(rot));
            var t = root.transform;
            int n = order.Length;
            float w = Mathf.Max(0.8f, 0.3f + n * 0.24f);
            var rends = new List<Renderer>();
            PuzzleVisuals.Cyl(t, "Post", new Vector3(0f, 0.6f, 0f), 0.05f, 1.2f, MatKey.MetalDark);
            PuzzleVisuals.Box(t, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(0.4f, 0.08f, 0.4f), MatKey.MetalDark);
            rends.Add(Kit.SolidBox(t, "Panel", new Vector3(0f, 1.4f, 0f), new Vector3(w, 0.5f, 0.2f), MatKey.PuzzleBody).GetComponent<Renderer>());
            FaceText(t, "Title", new Vector3(0f, 1.55f, 0.1f), Vector3.forward, "SEQUENCE", 1.5f, UIColors.Text, w, 0.2f);
            var lamps = new PuzzleLamp[n];
            for (int i = 0; i < n; i++)
                lamps[i] = PuzzleVisuals.Lamp(t, "Step" + i, new Vector3((i - (n - 1) * 0.5f) * 0.24f, 1.33f, 0.1f), Quaternion.identity, 0.12f, PuzzleLampColor.Off);
            var sl = root.AddComponent<SequenceLock>();
            sl.ProgressLamps = lamps;
            sl.SolvedMessage = solvedMessage ?? "";
            sl.Reveal = Reveal(root, PerspectiveMask.Intellect, "SEQUENCE", new Vector3(0f, 2f, 0f), true, false, rends.ToArray());
            sl.SetOrder(order);
            if (showClues) SequenceClues(t, order);
            return sl;
        }

        static void SequenceClues(Transform lockRoot, SignalSource[] order)
        {
            var map = new Dictionary<SignalSource, string>();
            var seen = new List<SignalSource>();
            for (int i = 0; i < order.Length; i++)
            {
                var s = order[i];
                if (s == null) continue;
                if (map.TryGetValue(s, out var str)) map[s] = str + " " + (i + 1);
                else { map[s] = (i + 1).ToString(); seen.Add(s); }
            }
            foreach (var s in seen)
            {
                var world = s.transform.position + Vector3.up * 2.1f;
                var clue = Kit.Make("Clue", lockRoot, lockRoot.InverseTransformPoint(world));
                clue.transform.rotation = Quaternion.identity;
                clue.AddComponent<PuzzleBillboard>();
                var disc = PuzzleVisuals.Cyl(clue.transform, "Holo", new Vector3(0f, 0f, 0.03f), 0.3f, 0.02f, MatKey.Hologram, Quaternion.Euler(90f, 0f, 0f), 20, false);
                var txt = PuzzleVisuals.Text(clue.transform, "Number", Vector3.zero, Quaternion.identity, map[s], 4.5f, UIColors.Intellect, 1f, 0.6f);
                Reveal(clue, PerspectiveMask.Intellect, "", Vector3.up, true, true, new Renderer[] { disc, txt.GetComponent<Renderer>() });
            }
        }

        /// <summary>Invisible character zone (box 'size', bottom at pos). visible: teal floor corner brackets + label.</summary>
        public static ZoneTrigger ZoneTrigger(Transform parent, Vector3 pos, Quaternion? rot, Vector3 size, ZoneFilter filter = ZoneFilter.AnyCharacter, int requiredCount = 1, bool visible = true, string solvedMessage = "")
        {
            var root = Kit.Make("ZoneTrigger", parent, pos, Rot(rot));
            var z = root.AddComponent<ZoneTrigger>();
            z.Size = size; z.Filter = filter; z.RequiredCount = requiredCount; z.SolvedMessage = solvedMessage ?? "";
            if (visible)
            {
                PuzzleVisuals.FloorCorners(root.transform, Vector3.zero, new Vector2(size.x, size.z), MatKey.PuzzleBody);
                string label = filter == ZoneFilter.WholeTeam ? "WHOLE TEAM" : filter == ZoneFilter.AnyCharacter ? (requiredCount > 1 ? requiredCount + "+ INTERNS" : "STAND HERE") : "SPECIALISTS ONLY";
                PuzzleVisuals.Text(root.transform, "FloorLabel", new Vector3(0f, 0.02f, -size.z * 0.5f + 0.35f), PuzzleVisuals.Facing(Vector3.up), label, 3f, UIColors.Accent, size.x, 0.5f);
            }
            return z;
        }
    }
}
