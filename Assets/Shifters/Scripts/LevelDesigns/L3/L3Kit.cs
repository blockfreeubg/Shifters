// =====================================================================================
//  SHIFTERS - L3Kit.cs   [LEVEL 3 - Sacrifice Zone]
//  Level-specific factories (Core/Kit.cs conventions: build everything in the factory, cached meshes,
//  Kit materials, works in edit mode and at runtime). Positions are LOCAL to parent; objects face +Z.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class L3Kit
    {
        static Material M(MatKey k) => Kit.Mat(k);

        // ------------------------------------------------------------------ security latch
        /// <summary>Latch display on a post (screen faces +Z) that turns ON once 'input' stayed on for 'seconds'.</summary>
        public static L3HoldLatch HoldLatch(Transform parent, Vector3 pos, Quaternion rot, SignalSource input, float seconds, string title = "SECURITY LATCH")
        {
            var root = Kit.Make("L3HoldLatch", parent, pos, rot);
            var t = root.transform;
            PuzzleVisuals.Box(t, "Foot", new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 0.5f), MatKey.MetalDark);
            PuzzleVisuals.Cyl(t, "Post", new Vector3(0f, 0.9f, 0f), 0.06f, 1.7f, MatKey.MetalDark);
            Kit.SolidBox(t, "Housing", new Vector3(0f, 2.0f, 0f), new Vector3(1.5f, 0.75f, 0.22f), MatKey.PuzzleBody, 0.04f);
            PuzzleVisuals.Box(t, "Glass", new Vector3(0f, 2.05f, 0.115f), new Vector3(1.3f, 0.42f, 0.02f), MatKey.Black, 0.01f, null, false);
            PuzzleVisuals.Text(t, "Title", new Vector3(0f, 2.44f, 0.12f), PuzzleVisuals.Facing(Vector3.forward), title, 1.6f, UIColors.AccentYellow, 1.4f, 0.2f);
            var disp = PuzzleVisuals.Text(t, "Display", new Vector3(0f, 2.06f, 0.13f), PuzzleVisuals.Facing(Vector3.forward), "HOLD " + seconds.ToString("0") + " S", 3.2f, UIColors.Text, 1.25f, 0.4f);
            var lamps = new PuzzleLamp[5];
            for (int i = 0; i < lamps.Length; i++)
                lamps[i] = PuzzleVisuals.Lamp(t, "Lamp" + i, new Vector3((i - 2) * 0.24f, 1.74f, 0.12f), Quaternion.identity, 0.11f, PuzzleLampColor.Off);
            var latch = root.AddComponent<L3HoldLatch>();
            latch.Input = input;
            latch.HoldSeconds = seconds;
            latch.Display = disp;
            latch.Lamps = lamps;
            latch.IdleText = "HOLD " + seconds.ToString("0") + " S";
            return latch;
        }

        // ------------------------------------------------------------------ volunteer chair
        /// <summary>The "INSERT INTERN TO CONTINUE" machine: dais, padded chair with lap bar and brain-scanner helmet,
        /// power coils and a screen. The volunteer sits facing +Z; the released one is put down 1.4 m in front.</summary>
        public static L3VolunteerChair VolunteerChair(Transform parent, Vector3 pos, Quaternion rot, SignalSource releaseInput, float routeLength)
        {
            var root = Kit.Make("L3VolunteerChair", parent, pos, rot);
            var t = root.transform;
            // dais
            Kit.SolidBox(t, "Dais", new Vector3(0f, 0.08f, -0.2f), new Vector3(3.0f, 0.16f, 2.4f), MatKey.MetalDark, 0.04f);
            PuzzleVisuals.StripedBox(t, "DaisEdge", new Vector3(0f, 0.17f, 0.99f), new Vector3(3.0f, 0.03f, 0.12f), 0, 0.3f, false);
            // chair (seat cushion at ~0.45 above the dais under the hips, back behind)
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
                // tesla coils
                var coil = Kit.Make("Coil", t, new Vector3(s * 1.2f, daisTop, -0.6f)).transform;
                PuzzleVisuals.Cyl(coil, "Base", new Vector3(0f, 0.15f, 0f), 0.22f, 0.3f, MatKey.MetalDark);
                PuzzleVisuals.Cyl(coil, "Rod", new Vector3(0f, 1.0f, 0f), 0.07f, 1.5f, MatKey.MetalLight);
                for (int k = 0; k < 4; k++) PuzzleVisuals.Cyl(coil, "Ring", new Vector3(0f, 0.6f + k * 0.28f, 0f), 0.16f, 0.06f, MatKey.Pipe);
                ProcMesh.Part("Orb", coil, PuzzleVisuals.Sphere(0.2f), M(MatKey.LightBlue), new Vector3(0f, 1.85f, 0f), null, null, false);
            }
            // brain-scanner helmet arm
            PuzzleVisuals.Box(t, "HelmetArm", new Vector3(0f, daisTop + 1.85f, -0.45f), new Vector3(0.12f, 0.12f, 0.5f), MatKey.MetalDark);
            var helmet = PuzzleVisuals.Cyl(t, "Helmet", new Vector3(0f, daisTop + 1.78f, -0.18f), 0.32f, 0.22f, MatKey.MetalLight, null, 18);
            helmet.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PuzzleVisuals.Cyl(t, "HelmetGlow", new Vector3(0f, daisTop + 1.66f, -0.18f), 0.28f, 0.03f, MatKey.Hologram, null, 18, false);
            // lap bar (pivot behind the seat, swings down over the lap)
            var bar = Kit.Make("Restraint", t, new Vector3(0f, daisTop + 0.95f, -0.55f), Quaternion.Euler(-70f, 0f, 0f)).transform;
            PuzzleVisuals.Box(bar, "BarL", new Vector3(-0.42f, -0.15f, 0.35f), new Vector3(0.06f, 0.06f, 0.75f), MatKey.HazardYellow, 0.01f, Quaternion.Euler(25f, 0f, 0f), false);
            PuzzleVisuals.Box(bar, "BarR", new Vector3(0.42f, -0.15f, 0.35f), new Vector3(0.06f, 0.06f, 0.75f), MatKey.HazardYellow, 0.01f, Quaternion.Euler(25f, 0f, 0f), false);
            PuzzleVisuals.Box(bar, "BarFront", new Vector3(0f, -0.3f, 0.68f), new Vector3(0.9f, 0.08f, 0.08f), MatKey.HazardYellow, 0.01f, null, false);
            // screen + sign on a gantry above
            Kit.SolidBox(t, "GantryL", new Vector3(-1.55f, 1.5f, -1.2f), new Vector3(0.16f, 3.0f, 0.16f), MatKey.MetalDark);
            Kit.SolidBox(t, "GantryR", new Vector3(1.55f, 1.5f, -1.2f), new Vector3(0.16f, 3.0f, 0.16f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "GantryTop", new Vector3(0f, 3.05f, -1.2f), new Vector3(3.3f, 0.18f, 0.2f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "ScreenBox", new Vector3(0f, 2.62f, -1.1f), new Vector3(1.6f, 0.62f, 0.14f), MatKey.MetalDark);
            PuzzleVisuals.Box(t, "ScreenGlass", new Vector3(0f, 2.62f, -1.02f), new Vector3(1.45f, 0.5f, 0.02f), MatKey.Screen, 0.005f, null, false);
            var screen = PuzzleVisuals.Text(t, "Screen", new Vector3(0f, 2.62f, -1.0f), PuzzleVisuals.Facing(Vector3.forward), "INSERT INTERN\nTO CONTINUE", 2.0f, UIColors.Info, 1.4f, 0.48f);
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(0f, 3.25f, -1.1f), Quaternion.identity, 0.2f);
            // interaction volume
            var vol = Kit.Trigger(t, "InteractVolume", new Vector3(0f, 1.0f, -0.1f), new Vector3(1.6f, 2.0f, 1.6f));
            vol.name = "InteractVolume";
            // anchors
            var seat = Kit.Make("Seat", t, new Vector3(0f, daisTop + 0.02f, -0.12f));
            var exit = Kit.Make("ExitPoint", t, new Vector3(0f, 0.05f, 1.6f));

            var chair = root.AddComponent<L3VolunteerChair>();
            chair.Seat = seat.transform;
            chair.ExitPoint = exit.transform;
            chair.ReleaseInput = releaseInput;
            chair.RouteLength = routeLength;
            chair.Screen = screen;
            chair.Lamp = lamp;
            chair.Restraint = bar;
            var released = Kit.Make("ReleasedSignal", t, Vector3.zero).AddComponent<L3Signal>();
            chair.ReleasedSignal = released;
            var rev = root.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Intellect;
            rev.Highlight = true;
            rev.Label = "VOLUNTEER CHAIR\nPOWERS THE BRIDGE";
            rev.LabelOffset = new Vector3(0f, 3.6f, 0f);
            rev.Renderers = new Renderer[0];
            return chair;
        }

        // ------------------------------------------------------------------ hard-light bridge
        /// <summary>PuzzleKit.RetractableBridge re-skinned as a hard-light walkway (hologram deck + glowing rails) with
        /// two emitter pylons at the edge. pos = ledge edge at floor height, extends along local +Z.</summary>
        public static RetractableBridge HardLightBridge(Transform parent, Vector3 pos, Quaternion rot, float length, float width, params SignalSource[] inputs)
        {
            var b = PuzzleKit.RetractableBridge(parent, pos, rot, length, width, inputs);
            b.name = "HardLightBridge";
            var walk = b.Body != null ? b.Body.transform : b.transform;
            var deck = walk.Find("Deck");
            if (deck != null)
            {
                var r = deck.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = M(MatKey.Hologram);
            }
            PuzzleVisuals.Box(walk, "Core", new Vector3(0f, -0.2f, -length * 0.5f), new Vector3(width * 0.5f, 0.04f, length - 0.2f), MatKey.Glow, 0.01f, null, false);
            for (int s = -1; s <= 1; s += 2)
                PuzzleVisuals.Box(walk, "Rail", new Vector3(s * (width * 0.5f - 0.05f), 0.04f, -length * 0.5f), new Vector3(0.08f, 0.08f, length - 0.1f), MatKey.TrimGlow, 0.01f, null, false);
            var t = b.transform;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.SolidBox(t, "Pylon", new Vector3(s * (width * 0.5f + 0.45f), 0.6f, -0.45f), new Vector3(0.4f, 1.2f, 0.4f), MatKey.PuzzleBody, 0.05f);
                ProcMesh.Part("PylonOrb", t, PuzzleVisuals.Sphere(0.22f), M(MatKey.LightBlue), new Vector3(s * (width * 0.5f + 0.45f), 1.4f, -0.45f), null, null, false);
            }
            return b;
        }

        // ------------------------------------------------------------------ decor
        /// <summary>Decontamination shower arch (two posts, overhead pipe with nozzles, green glow strip). No collider
        /// except the posts. Passage along local Z, 'width' wide.</summary>
        public static GameObject DeconArch(Transform parent, Vector3 pos, Quaternion rot, float width = 4f)
        {
            var root = Kit.Make("L3DeconArch", parent, pos, rot);
            var t = root.transform;
            float hw = width * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.SolidBox(t, "Post", new Vector3(s * (hw + 0.2f), 1.75f, 0f), new Vector3(0.32f, 3.5f, 0.5f), MatKey.WallAlt, 0.05f);
                PuzzleVisuals.Box(t, "PostGlow", new Vector3(s * (hw + 0.035f), 1.6f, 0f), new Vector3(0.03f, 2.6f, 0.2f), MatKey.Acid, 0f, null, false);
            }
            PuzzleVisuals.Box(t, "Header", new Vector3(0f, 3.6f, 0f), new Vector3(width + 0.8f, 0.3f, 0.6f), MatKey.MetalDark);
            PuzzleVisuals.Cyl(t, "SprayPipe", new Vector3(0f, 3.35f, 0f), 0.08f, width, MatKey.Pipe, Quaternion.Euler(0f, 0f, 90f), 10);
            for (int i = 0; i < 5; i++)
            {
                float x = Mathf.Lerp(-hw + 0.3f, hw - 0.3f, i / 4f);
                PuzzleVisuals.Cyl(t, "Nozzle", new Vector3(x, 3.22f, 0f), 0.06f, 0.14f, MatKey.MetalLight, null, 8, false);
            }
            PuzzleVisuals.StripedBox(t, "FloorStripe", new Vector3(0f, 0.01f, 0f), new Vector3(width, 0.02f, 0.5f), 0, 0.35f, false);
            return root;
        }

        /// <summary>Shattered specimen tank: base, jagged glass shards, an acid puddle and a sad rubber duck. Collider on the base.</summary>
        public static GameObject BrokenTank(Transform parent, Vector3 pos, float radius = 0.6f, int seed = 1)
        {
            var root = Kit.Make("L3BrokenTank", parent, pos);
            var t = root.transform;
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = radius + 0.12f; col.height = 1.0f; col.center = new Vector3(0f, 0.5f, 0f);
            PuzzleVisuals.Cyl(t, "Base", new Vector3(0f, 0.15f, 0f), radius + 0.12f, 0.3f, MatKey.MetalDark, null, 18);
            PuzzleVisuals.Cyl(t, "Ring", new Vector3(0f, 0.34f, 0f), radius + 0.05f, 0.08f, MatKey.MetalLight, null, 18);
            var rng = new System.Random(seed);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.3f;
                float h = 0.25f + (float)rng.NextDouble() * 0.9f;
                var p = new Vector3(Mathf.Cos(a) * radius, 0.38f + h * 0.5f, Mathf.Sin(a) * radius);
                var q = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 20f, -a * Mathf.Rad2Deg + 90f, ((float)rng.NextDouble() - 0.5f) * 25f);
                PuzzleVisuals.Box(t, "Shard", p, new Vector3(radius * 0.62f, h, 0.03f), MatKey.Glass, 0f, q, false);
            }
            PuzzleVisuals.Cyl(t, "Puddle", new Vector3(0.3f, 0.012f, radius + 0.55f), radius + 0.45f, 0.02f, MatKey.Acid, null, 18, false);
            var duck = Kit.Make("Duck", t, new Vector3(0.5f, 0.02f, radius + 0.75f), Quaternion.Euler(0f, 200f, 0f)).transform;
            ProcMesh.Part("Body", duck, PuzzleVisuals.Sphere(0.12f), MaterialLibrary.Colored(new Color(1f, 0.82f, 0.2f), 0.4f), new Vector3(0f, 0.1f, 0f), null, new Vector3(1.2f, 0.8f, 1f));
            ProcMesh.Part("Head", duck, PuzzleVisuals.Sphere(0.075f), MaterialLibrary.Colored(new Color(1f, 0.82f, 0.2f), 0.4f), new Vector3(0.1f, 0.22f, 0f));
            return root;
        }

        /// <summary>Wall-mounted funny label plate (TMP text readable along rot*forward). Visual only.</summary>
        public static TextMeshPro WallText(Transform parent, Vector3 pos, Quaternion rot, string text, float fontSize, Color color, float width = 4f, float height = 1f)
        {
            var go = Kit.Make("L3WallText", parent, pos, rot);
            return PuzzleVisuals.Text(go.transform, "Text", Vector3.zero, Quaternion.Euler(0f, 180f, 0f), text, fontSize, color, width, height);
        }
    }
}
