using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Shifters
{
    public static partial class PuzzleKit
    {
        static T HazardSetup<T>(T h, float cycleOn, float cycleOff, float phase, SignalSource[] inputs) where T : PuzzleHazard
        {
            h.CycleOn = cycleOn; h.CycleOff = cycleOff; h.Phase = phase;
            h.PoweredWithoutInputs = true;
            return Wire(h, inputs);
        }

        // ================================================================== kill zones
        /// <summary>Kill volume. pos = TOP center of the box (it extends size.y downward). Acid/Incinerator/FoamPit draw
        /// their surface; Void is invisible. Characters are Lost (FoamPit: bounced to respawnPoint, parent space).</summary>
        public static KillZone KillZone(Transform parent, Vector3 pos, Quaternion? rot, Vector3 size, KillZoneType type = KillZoneType.Void, Vector3? respawnPoint = null, string cause = null)
        {
            var root = Kit.Make("KillZone_" + type, parent, pos, Rot(rot));
            root.layer = 2;
            var t = root.transform;
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true; col.size = size; col.center = new Vector3(0f, -size.y * 0.5f, 0f);
            switch (type)
            {
                case KillZoneType.Acid:
                    PuzzleVisuals.Box(t, "AcidSurface", new Vector3(0f, -0.35f, 0f), new Vector3(size.x, 0.1f, size.z), MatKey.Acid, 0.02f, null, false);
                    var rng = new System.Random(3);
                    for (int i = 0; i < 7; i++)
                    {
                        var p = new Vector3(((float)rng.NextDouble() - 0.5f) * size.x * 0.8f, -0.3f, ((float)rng.NextDouble() - 0.5f) * size.z * 0.8f);
                        ProcMesh.Part("Bubble", t, PuzzleVisuals.Sphere(0.08f + (float)rng.NextDouble() * 0.1f), M(MatKey.Acid), p, null, new Vector3(1f, 0.6f, 1f), false);
                    }
                    break;
                case KillZoneType.Incinerator:
                    var glow = MaterialLibrary.Colored(new Color(1f, 0.45f, 0.1f), 0.1f, 3f);
                    PuzzleVisuals.Box(t, "Embers", new Vector3(0f, -size.y + 0.1f, 0f), new Vector3(size.x, 0.2f, size.z), glow, 0.02f, null, false);
                    for (float x = -size.x * 0.5f + 0.25f; x < size.x * 0.5f; x += 0.5f)
                        PuzzleVisuals.Box(t, "Grill", new Vector3(x, -0.6f, 0f), new Vector3(0.08f, 0.08f, size.z), MatKey.MetalDark, 0.01f, null, false);
                    break;
                case KillZoneType.FoamPit:
                    string key = string.Format(CultureInfo.InvariantCulture, "pz_foam_{0:0.#}x{1:0.#}", size.x, size.z);
                    var foam = ProcMesh.Cached(key, () =>
                    {
                        var b = new ProcMesh.Builder();
                        var r2 = new System.Random(11);
                        Color[] pastel = { new Color(1f, 0.6f, 0.75f), new Color(0.55f, 0.8f, 1f), new Color(1f, 0.9f, 0.45f), new Color(0.6f, 1f, 0.7f), new Color(0.8f, 0.65f, 1f) };
                        int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / 0.45f)), nz = Mathf.Max(1, Mathf.RoundToInt(size.z / 0.45f));
                        for (int ix = 0; ix < nx; ix++)
                            for (int iz = 0; iz < nz; iz++)
                                for (int layer = 0; layer < 2; layer++)
                                {
                                    var c = new Vector3((ix + 0.5f) / nx - 0.5f, 0f, (iz + 0.5f) / nz - 0.5f);
                                    c.x *= size.x; c.z *= size.z;
                                    c += new Vector3(((float)r2.NextDouble() - 0.5f) * 0.15f, layer * 0.32f + (float)r2.NextDouble() * 0.12f, ((float)r2.NextDouble() - 0.5f) * 0.15f);
                                    var rr = Quaternion.Euler((float)r2.NextDouble() * 40f, (float)r2.NextDouble() * 90f, (float)r2.NextDouble() * 40f);
                                    b.AddBox(c, Vector3.one * 0.4f, rr, pastel[r2.Next(pastel.Length)], 0.06f);
                                }
                        return b.ToMesh(key);
                    });
                    ProcMesh.Part("Foam", t, foam, MaterialLibrary.Colored(Color.white, 0.05f), new Vector3(0f, -Mathf.Min(size.y, 2.5f) + 0.2f, 0f), null, null, false);
                    break;
            }
            var kz = root.AddComponent<KillZone>();
            kz.Type = type;
            kz.Cause = cause ?? "";
            var rp = respawnPoint ?? Vector3.zero;
            kz.RespawnPoint = parent != null ? parent.TransformPoint(rp) : rp;
            return kz;
        }

        /// <summary>Tutorial foam pit: characters falling in are bounced back to respawnPoint (parent space).</summary>
        public static KillZone FoamPit(Transform parent, Vector3 pos, Quaternion? rot, Vector3 size, Vector3 respawnPoint)
            => KillZone(parent, pos, rot, size, KillZoneType.FoamPit, respawnPoint);

        // ================================================================== lasers
        /// <summary>Laser fence 'width' wide (local X) with 'beams' beams (0.45 m, then every 0.6 m). Always on without inputs;
        /// cycleOn/cycleOff > 0 = cycling. Wire a plate with Invert (or HazardToggle) to switch it off.</summary>
        public static LaserBarrier LaserBarrier(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 3f, int beams = 2, float cycleOn = 0f, float cycleOff = 0f, float phase = 0f, params SignalSource[] inputs)
        {
            beams = Mathf.Clamp(beams, 1, 4);
            var root = Kit.Make("LaserBarrier", parent, pos, Rot(rot));
            root.layer = 2;
            var t = root.transform;
            float top = 0.45f + (beams - 1) * 0.6f;
            float postH = top + 0.45f;
            var zone = root.AddComponent<BoxCollider>();
            zone.isTrigger = true; zone.center = new Vector3(0f, top * 0.5f + 0.1f, 0f); zone.size = new Vector3(width, top + 0.4f, 0.3f);
            var lamps = new List<PuzzleLamp>();
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (width * 0.5f + 0.15f);
                Kit.SolidBox(t, "Emitter", new Vector3(x, postH * 0.5f, 0f), new Vector3(0.3f, postH, 0.3f), MatKey.MetalDark);
                PuzzleVisuals.StripedBox(t, "Foot", new Vector3(x, 0.08f, 0f), new Vector3(0.36f, 0.16f, 0.36f), 1, 0.04f);
                for (int i = 0; i < beams; i++)
                    PuzzleVisuals.Box(t, "Lens", new Vector3(x - s * 0.16f, 0.45f + i * 0.6f, 0f), new Vector3(0.06f, 0.14f, 0.14f), MatKey.WarningRed, 0.02f, null, false);
                lamps.Add(PuzzleVisuals.Lamp(t, "Lamp", new Vector3(x, postH + 0.03f, 0f), Quaternion.Euler(-90f, 0f, 0f), 0.14f));
            }
            var beamMesh = PuzzleVisuals.Cylinder(0.035f, 1f, 10);
            var beamRends = new Renderer[beams];
            for (int i = 0; i < beams; i++)
                beamRends[i] = ProcMesh.Part("Beam", t, beamMesh, M(MatKey.Laser), new Vector3(0f, 0.45f + i * 0.6f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(1f, width, 1f), false).GetComponent<Renderer>();
            var lb = root.AddComponent<LaserBarrier>();
            lb.Width = width; lb.Beams = beamRends; lb.Zone = zone; lb.EmitterLamps = lamps.ToArray();
            lb.Reveal = Reveal(root, PerspectiveMask.Speed, "LASER", new Vector3(0f, postH + 0.6f, 0f), false, false, beamRends);
            return HazardSetup(lb, cycleOn, cycleOff, phase, inputs);
        }

        // ================================================================== crusher
        /// <summary>Cycling crusher over a footprint (local XZ) centered on pos (floor). drop = head travel (m).</summary>
        public static Crusher Crusher(Transform parent, Vector3 pos, Quaternion? rot = null, Vector2? footprint = null, float drop = 2.6f, float upTime = 1.8f, float phase = 0f, params SignalSource[] inputs)
        {
            var fp = footprint ?? new Vector2(2f, 2f);
            var root = Kit.Make("Crusher", parent, pos, Rot(rot));
            var t = root.transform;
            float topY = drop + 1.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.SolidBox(t, "Pillar", new Vector3(s * (fp.x * 0.5f + 0.3f), topY * 0.5f, 0f), new Vector3(0.4f, topY, 0.6f), MatKey.MetalDark);
                PuzzleVisuals.StripedBox(t, "FloorStripe", new Vector3(0f, 0.01f, s * (fp.y * 0.5f + 0.12f)), new Vector3(fp.x + 0.6f, 0.02f, 0.2f), 0, 0.25f, false);
            }
            PuzzleVisuals.Box(t, "TopBeam", new Vector3(0f, topY + 0.25f, 0f), new Vector3(fp.x + 1f, 0.5f, 0.7f), MatKey.MetalDark);
            PuzzleVisuals.Cyl(t, "Housing", new Vector3(0f, topY - 0.25f, 0f), 0.28f, 0.5f, MatKey.MetalLight, null, 14);
            var rod = ProcMesh.Part("Rod", t, PuzzleVisuals.Cylinder(0.14f, 1f, 12), M(MatKey.MetalLight), new Vector3(0f, topY - 0.5f, 0f)).transform;
            const float hh = 0.4f;
            var upLocal = new Vector3(0f, drop + hh + 0.02f, 0f);
            var head = Kit.Make("Head", t, upLocal);
            var mover = head.AddComponent<MoverBody>(); mover.Ensure();
            head.AddComponent<BoxCollider>().size = new Vector3(fp.x, hh * 2f, fp.y);
            PuzzleVisuals.Box(head.transform, "Block", Vector3.zero, new Vector3(fp.x, hh * 2f, fp.y), MatKey.MetalDark, 0.05f);
            PuzzleVisuals.StripedBox(head.transform, "Teeth", new Vector3(0f, -hh + 0.1f, 0f), new Vector3(fp.x + 0.03f, 0.2f, fp.y + 0.03f), 0, 0.25f);
            var lamps = new List<PuzzleLamp>();
            for (int s = -1; s <= 1; s += 2)
                for (int f = -1; f <= 1; f += 2)
                    lamps.Add(PuzzleVisuals.Lamp(t, "Warn", new Vector3(s * (fp.x * 0.5f + 0.3f), topY - 0.4f, f * 0.31f), f > 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f), 0.16f, PuzzleLampColor.Off));
            var c = root.AddComponent<Crusher>();
            c.Drop = drop; c.UpTime = upTime; c.Footprint = fp; c.Head = mover; c.HeadUpLocal = upLocal; c.HeadHalfHeight = hh;
            c.WarningLamps = lamps.ToArray(); c.Rod = rod; c.RodTopY = topY - 0.25f;
            c.Reveal = Reveal(root, PerspectiveMask.Speed, "CRUSHER", new Vector3(0f, topY + 0.9f, 0f), false);
            return HazardSetup(c, 0f, 0f, phase, inputs);
        }

        // ================================================================== sweeper
        /// <summary>Rotating hazard bar centered on pos (floor), 'length' total, bar at barHeight (jumpable at 0.6 m).</summary>
        public static SweeperArm SweeperArm(Transform parent, Vector3 pos, Quaternion? rot = null, float length = 6f, float degreesPerSecond = 90f, float barHeight = 0.6f, params SignalSource[] inputs)
        {
            var root = Kit.Make("SweeperArm", parent, pos, Rot(rot));
            var t = root.transform;
            Kit.SolidBox(t, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.3f, 0.9f), MatKey.MetalDark);
            var arm = Kit.Make("Arm", t, Vector3.zero);
            var mover = arm.AddComponent<MoverBody>(); mover.Ensure();
            const float thick = 0.45f;
            PuzzleVisuals.Cyl(arm.transform, "Hub", new Vector3(0f, barHeight * 0.5f + 0.2f, 0f), 0.32f, barHeight + 0.4f, MatKey.MetalLight, null, 16);
            PuzzleVisuals.StripedBox(arm.transform, "Bar", new Vector3(0f, barHeight, 0f), new Vector3(length, thick, thick), 0, 0.35f);
            var bc = Kit.Make("BarCollider", arm.transform, new Vector3(0f, barHeight, 0f)).AddComponent<BoxCollider>();
            bc.size = new Vector3(length, thick, thick);
            var sw = root.AddComponent<SweeperArm>();
            sw.Length = length; sw.DegreesPerSecond = degreesPerSecond; sw.BarHeight = barHeight; sw.BarThickness = thick; sw.Arm = mover;
            Reveal(root, PerspectiveMask.Speed, "SWEEPER - JUMP IT", new Vector3(0f, barHeight + 1.2f, 0f), false);
            return HazardSetup(sw, 0f, 0f, 0f, inputs);
        }

        // ================================================================== launchers
        /// <summary>Bounce pad (1.8 m) on the floor at pos. launchHeight = apex above the pad; forwardSpeed along local +Z.</summary>
        public static BouncePad BouncePad(Transform parent, Vector3 pos, Quaternion? rot = null, float launchHeight = 5f, float forwardSpeed = 0f)
        {
            var root = Kit.Make("BouncePad", parent, pos, Rot(rot));
            root.layer = 2;
            var t = root.transform;
            var trig = root.AddComponent<BoxCollider>();
            trig.isTrigger = true; trig.center = new Vector3(0f, 0.5f, 0f); trig.size = new Vector3(1.5f, 0.6f, 1.5f);
            Kit.SolidBox(t, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(1.8f, 0.2f, 1.8f), MatKey.MetalDark);
            PuzzleVisuals.Cyl(t, "Ring", new Vector3(0f, 0.21f, 0f), 0.86f, 0.03f, MatKey.HazardYellow, null, 20, false);
            var pad = Kit.Make("Pad", t, new Vector3(0f, 0.2f, 0f)).transform;
            var rends = new List<Renderer>
            {
                PuzzleVisuals.Cyl(pad, "Spring", new Vector3(0f, 0.04f, 0f), 0.6f, 0.08f, MatKey.HazardBlack, null, 20),
                PuzzleVisuals.Cyl(pad, "Top", new Vector3(0f, 0.11f, 0f), 0.74f, 0.07f, MatKey.SafetyOrange, null, 20)
            };
            var arrow = PuzzleVisuals.FloorChevronMesh(0.6f);
            for (int i = 0; i < 2; i++)
                ProcMesh.Part("Arrow", pad, arrow, M(MatKey.HazardYellow), new Vector3(0f, 0.15f, -0.12f + i * 0.28f), Quaternion.identity, null, false);
            var bp = root.AddComponent<BouncePad>();
            bp.LaunchHeight = launchHeight; bp.ForwardSpeed = forwardSpeed; bp.Pad = pad;
            Reveal(root, PerspectiveMask.Speed, "LAUNCH " + launchHeight.ToString("0.#", CultureInfo.InvariantCulture) + " M", new Vector3(0f, 1.3f, 0f), true, false, rends.ToArray());
            return bp;
        }

        /// <summary>Steam vent grate (1.6 m) cycling on/off; venting launches characters AND loose props (crates, barrels) to launchHeight.</summary>
        public static SteamVent SteamVent(Transform parent, Vector3 pos, Quaternion? rot = null, float launchHeight = 4.5f, float cycleOn = 2f, float cycleOff = 2.5f, float phase = 0f, params SignalSource[] inputs)
        {
            var root = Kit.Make("SteamVent", parent, pos, Rot(rot));
            var t = root.transform;
            Kit.SolidBox(t, "Grate", new Vector3(0f, 0.06f, 0f), new Vector3(1.6f, 0.12f, 1.6f), MatKey.Grate);
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.StripedBox(t, "RimX", new Vector3(0f, 0.07f, s * 0.86f), new Vector3(1.84f, 0.14f, 0.12f), 0, 0.2f);
                PuzzleVisuals.StripedBox(t, "RimZ", new Vector3(s * 0.86f, 0.07f, 0f), new Vector3(0.12f, 0.14f, 1.6f), 2, 0.2f);
            }
            for (float x = -0.6f; x <= 0.61f; x += 0.3f)
                PuzzleVisuals.Box(t, "Slot", new Vector3(x, 0.125f, 0f), new Vector3(0.12f, 0.01f, 1.3f), MatKey.Black, 0.002f, null, false);
            var puffMat = MaterialLibrary.Transparent(new Color(1f, 1f, 1f, 0.45f));
            var puffs = new Transform[8];
            var puffMesh = PuzzleVisuals.Sphere(0.45f);
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i] = ProcMesh.Part("Steam", t, puffMesh, puffMat, new Vector3(0f, 0.5f + i * 0.4f, 0f), null, null, false).transform;
                puffs[i].gameObject.SetActive(false);
            }
            var v = root.AddComponent<SteamVent>();
            v.LaunchHeight = launchHeight; v.Puffs = puffs;
            v.Reveal = Reveal(root, PerspectiveMask.Speed, "STEAM", new Vector3(0f, 1.2f, 0f), false);
            return HazardSetup(v, cycleOn, cycleOff, phase, inputs);
        }

        // ================================================================== platforms
        /// <summary>Crumbling slab, pos = top center. Falls ShakeTime (0.6 s) after a character touches it, back after 4 s.</summary>
        public static CrumblingPlatform CrumblingPlatform(Transform parent, Vector3 pos, Quaternion? rot = null, Vector3? size = null)
        {
            var sz = size ?? new Vector3(2.5f, 0.5f, 2.5f);
            var r = Rot(rot);
            var root = Kit.Make("CrumblingPlatform", parent, pos + r * Vector3.down * (sz.y * 0.5f), r);
            var col = root.AddComponent<BoxCollider>(); col.size = sz;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 200f; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var vis = Kit.Make("Visual", root.transform, Vector3.zero).transform;
            var rends = new List<Renderer> { PuzzleVisuals.Box(vis, "Slab", Vector3.zero, sz, MatKey.FloorAlt, 0.05f) };
            string key = string.Format(CultureInfo.InvariantCulture, "pz_crumble_cracks_{0:0.##}x{1:0.##}", sz.x, sz.z);
            var cracks = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var rng = new System.Random(5);
                for (int k = 0; k < 4; k++)
                {
                    var p = new Vector3(((float)rng.NextDouble() - 0.5f) * sz.x * 0.4f, sz.y * 0.5f + 0.004f, ((float)rng.NextDouble() - 0.5f) * sz.z * 0.4f);
                    float ang = (float)rng.NextDouble() * 360f;
                    for (int s = 0; s < 4; s++)
                    {
                        ang += (float)(rng.NextDouble() * 80.0 - 40.0);
                        float len = 0.2f + (float)rng.NextDouble() * 0.3f;
                        var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                        b.AddBox(p + dir * len * 0.5f, new Vector3(0.035f, 0.008f, len), Quaternion.Euler(0f, ang, 0f), Color.white, 0f);
                        p += dir * len;
                    }
                }
                return b.ToMesh(key);
            });
            ProcMesh.Part("Cracks", vis, cracks, M(MatKey.Black), Vector3.zero, null, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.Box(vis, "WarnX", new Vector3(0f, sz.y * 0.5f + 0.004f, s * (sz.z * 0.5f - 0.06f)), new Vector3(sz.x - 0.04f, 0.01f, 0.1f), MatKey.HazardYellow, 0.002f, null, false);
                PuzzleVisuals.Box(vis, "WarnZ", new Vector3(s * (sz.x * 0.5f - 0.06f), sz.y * 0.5f + 0.004f, 0f), new Vector3(0.1f, 0.01f, sz.z - 0.04f), MatKey.HazardYellow, 0.002f, null, false);
            }
            var cp = root.AddComponent<CrumblingPlatform>();
            cp.Visual = vis;
            cp.Reveal = Reveal(root, PerspectiveMask.Speed, "CRUMBLES", new Vector3(0f, 1.3f, 0f), true, false, rends.ToArray());
            return cp;
        }

        /// <summary>Seesaw slab, pos = top center; tilts around local X (weight toward +Z / -Z tips that end down).</summary>
        public static WobblyPlatform WobblyPlatform(Transform parent, Vector3 pos, Quaternion? rot = null, Vector3? size = null)
        {
            var sz = size ?? new Vector3(2.5f, 0.35f, 4.5f);
            var root = Kit.Make("WobblyPlatform", parent, pos, Rot(rot));
            var t = root.transform;
            PuzzleVisuals.Box(t, "Support", new Vector3(0f, -sz.y - 0.75f, 0f), new Vector3(0.45f, 1.5f, 0.45f), MatKey.MetalDark);
            PuzzleVisuals.Cyl(t, "Axle", new Vector3(0f, -sz.y * 0.5f, 0f), 0.13f, sz.x + 0.4f, MatKey.MetalLight, Quaternion.Euler(0f, 0f, 90f), 12);
            var slab = Kit.Make("Slab", t, Vector3.zero);
            var mover = slab.AddComponent<MoverBody>(); mover.Ensure();
            var col = slab.AddComponent<BoxCollider>(); col.size = sz; col.center = new Vector3(0f, -sz.y * 0.5f, 0f);
            PuzzleVisuals.Box(slab.transform, "Deck", new Vector3(0f, -sz.y * 0.5f, 0f), sz, MatKey.MetalLight, 0.05f);
            for (int s = -1; s <= 1; s += 2)
                PuzzleVisuals.StripedBox(slab.transform, "EndStripe", new Vector3(0f, 0.006f, s * (sz.z * 0.5f - 0.15f)), new Vector3(sz.x - 0.05f, 0.012f, 0.3f), 0, 0.25f, false);
            var sensor = slab.AddComponent<WeightSensor>();
            sensor.Surfaces = new Collider[] { col };
            var w = root.AddComponent<WobblyPlatform>();
            w.Slab = mover; w.Sensor = sensor;
            return w;
        }

        // ================================================================== floors
        /// <summary>Electric floor tile, pos = top center (surface at pos.y). Cycles live/safe; anyone standing on it while live gets zapped.</summary>
        public static ElectricFloor ElectricFloor(Transform parent, Vector3 pos, Quaternion? rot = null, Vector2? size = null, float cycleOn = 2f, float cycleOff = 2.5f, float phase = 0f, params SignalSource[] inputs)
        {
            var sz = size ?? new Vector2(4f, 4f);
            var root = Kit.Make("ElectricFloor", parent, pos, Rot(rot));
            var t = root.transform;
            Kit.SolidBox(t, "Tile", new Vector3(0f, -0.1f, 0f), new Vector3(sz.x, 0.2f, sz.y), MatKey.MetalDark);
            string key = string.Format(CultureInfo.InvariantCulture, "pz_egrid_{0:0.##}x{1:0.##}", sz.x, sz.y);
            var grid = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                for (float x = -sz.x * 0.5f + 0.5f; x < sz.x * 0.5f - 0.01f; x += 0.5f) b.AddBox(new Vector3(x, 0.004f, 0f), new Vector3(0.05f, 0.012f, sz.y - 0.3f), Quaternion.identity, Color.white, 0f);
                for (float z = -sz.y * 0.5f + 0.5f; z < sz.y * 0.5f - 0.01f; z += 0.5f) b.AddBox(new Vector3(0f, 0.004f, z), new Vector3(sz.x - 0.3f, 0.012f, 0.05f), Quaternion.identity, Color.white, 0f);
                return b.ToMesh(key);
            });
            var off = M(MatKey.Rubber);
            var lines = ProcMesh.Part("Grid", t, grid, off, Vector3.zero, null, null, false).GetComponent<Renderer>();
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.StripedBox(t, "BorderX", new Vector3(0f, 0.005f, s * (sz.y * 0.5f - 0.07f)), new Vector3(sz.x, 0.012f, 0.14f), 0, 0.25f, false);
                PuzzleVisuals.StripedBox(t, "BorderZ", new Vector3(s * (sz.x * 0.5f - 0.07f), 0.005f, 0f), new Vector3(0.14f, 0.012f, sz.y - 0.28f), 2, 0.25f, false);
            }
            var ef = root.AddComponent<ElectricFloor>();
            ef.Size = sz; ef.Lines = new[] { lines };
            ef.OffMaterial = off; ef.WarnMaterial = MaterialLibrary.Colored(UIColors.AccentYellow, 0.2f, 1.5f); ef.LiveMaterial = M(MatKey.LightBlue);
            ef.Reveal = Reveal(root, PerspectiveMask.Speed, "ELECTRIC FLOOR", new Vector3(0f, 1.2f, 0f), false);
            return HazardSetup(ef, cycleOn, cycleOff, phase, inputs);
        }

        /// <summary>Spike bed on the floor at pos. cycleOn/cycleOff > 0 = retracting spikes; else always out.</summary>
        public static Spikes Spikes(Transform parent, Vector3 pos, Quaternion? rot = null, Vector2? size = null, float cycleOn = 0f, float cycleOff = 0f, float phase = 0f, params SignalSource[] inputs)
        {
            var sz = size ?? new Vector2(2f, 2f);
            var root = Kit.Make("Spikes", parent, pos, Rot(rot));
            var t = root.transform;
            Kit.SolidBox(t, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(sz.x, 0.1f, sz.y), MatKey.MetalDark);
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.StripedBox(t, "RimX", new Vector3(0f, 0.06f, s * (sz.y * 0.5f + 0.06f)), new Vector3(sz.x + 0.24f, 0.12f, 0.12f), 0, 0.2f);
                PuzzleVisuals.StripedBox(t, "RimZ", new Vector3(s * (sz.x * 0.5f + 0.06f), 0.06f, 0f), new Vector3(0.12f, 0.12f, sz.y), 2, 0.2f);
            }
            const float h = 0.45f;
            string key = string.Format(CultureInfo.InvariantCulture, "pz_spikes_{0:0.##}x{1:0.##}", sz.x, sz.y);
            var mesh = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                for (float x = -sz.x * 0.5f + 0.17f; x < sz.x * 0.5f; x += 0.33f)
                    for (float z = -sz.y * 0.5f + 0.17f; z < sz.y * 0.5f; z += 0.33f)
                        b.AddCone(new Vector3(x, 0.1f, z), 0.1f, h, Quaternion.identity, Color.white, 6);
                return b.ToMesh(key);
            });
            var spikeRoot = Kit.Make("SpikeRoot", t, Vector3.zero).transform;
            ProcMesh.Part("Spikes", spikeRoot, mesh, M(MatKey.MetalLight), Vector3.zero);
            var sp = root.AddComponent<Spikes>();
            sp.Size = sz; sp.SpikeHeight = h; sp.SpikeRoot = spikeRoot;
            sp.Reveal = Reveal(root, PerspectiveMask.Speed, "SPIKES", new Vector3(0f, 1.1f, 0f), false);
            return HazardSetup(sp, cycleOn, cycleOff, phase, inputs);
        }
    }
}
