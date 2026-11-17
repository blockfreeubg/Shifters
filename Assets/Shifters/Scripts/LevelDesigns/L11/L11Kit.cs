// =====================================================================================
//  SHIFTERS - L11Kit.cs   [SHIFT 10 - THE ME-MORIAL PARK]  level-local factories (no MonoBehaviours here)
//  Ghost bridge (RetractableBridge reskin, L3 HardLightBridge pattern), lamp posts, iron arches, ghost handshake
//  hands (Spikes reskin), the 1999 trust-fall tableau, the big walk-in mausoleum (4.6 m doorway: the decor kit's
//  Mausoleum has a fixed 2 m door, too narrow for G1), dead trees and the moon. Every mesh is ProcMesh-cached.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class L11Kit
    {
        static Material M(MatKey k) => Kit.Mat(k);
        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        static Mesh Sph(float r) => ProcMesh.Cached("l11_sph_" + F(r), () => ProcMesh.Sphere(r, 12, 8, true));
        static Mesh Caps(float r, float h) => ProcMesh.Cached("l11_caps_" + F(r) + "_" + F(h), () => ProcMesh.Capsule(r, h, 12, 4));
        static Mesh Cyl(float r, float h) => ProcMesh.Cached("l11_cyl_" + F(r) + "_" + F(h), () => ProcMesh.Cylinder(r, h, 12, true));
        static Mesh Cone(float r, float h) => ProcMesh.Cached("l11_cone_" + F(r) + "_" + F(h), () => ProcMesh.Cone(r, h, 12));

        public static readonly Color SoilColor = new Color(0.23f, 0.16f, 0.11f);
        public static readonly Color GhostBlue = new Color(0.55f, 0.85f, 1f);
        /// <summary>Moonlit marble: faintly self-lit so the C09 tableau reads in the dark hall without a real light.</summary>
        public static Material MoonMarble => MaterialLibrary.Colored(new Color(0.8f, 0.82f, 0.9f), 0.35f, 0.35f);

        // ------------------------------------------------------------------ ghost bridge
        /// <summary>RetractableBridge with a ghost-glow deck and glowing rails. pos = the near edge (deck home is
        /// hidden inside the near bank, it slides +Z by 'length'). Lantern posts are visual only (no colliders).</summary>
        public static RetractableBridge GhostBridge(Transform parent, Vector3 pos, float length, float width, params SignalSource[] inputs)
        {
            var b = PuzzleKit.RetractableBridge(parent, pos, Quaternion.identity, length, width, inputs);
            b.name = "GhostBridge";
            b.SpeedOn = 3.2f; b.SpeedOff = 2.2f;
            var walk = b.Body != null ? b.Body.transform : b.transform;
            var deck = walk.Find("Deck");
            if (deck != null) { var r = deck.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = M(MatKey.GhostGlow); }
            PuzzleVisuals.Box(walk, "Core", new Vector3(0f, -0.2f, -length * 0.5f), new Vector3(width * 0.5f, 0.04f, length - 0.2f), MatKey.Glow, 0.01f, null, false);
            for (int s = -1; s <= 1; s += 2)
                PuzzleVisuals.Box(walk, "Rail", new Vector3(s * (width * 0.5f - 0.05f), 0.03f, -length * 0.5f), new Vector3(0.08f, 0.06f, length - 0.1f), MatKey.TrimGlow, 0.01f, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                PuzzleVisuals.Box(b.transform, "LanternPost", new Vector3(s * (width * 0.5f + 0.3f), 0.8f, -0.35f), new Vector3(0.14f, 1.6f, 0.14f), MatKey.MetalDark, 0.02f);
                ProcMesh.Part("LanternOrb", b.transform, Sph(0.2f), M(MatKey.GhostGlow), new Vector3(s * (width * 0.5f + 0.3f), 1.75f, -0.35f), null, null, false);
            }
            return b;
        }

        // ------------------------------------------------------------------ lamp post
        /// <summary>Victorian park lamp (3.6 m): pole, arm and an emissive lantern. No Light (the level adds at most one
        /// real pool light). pos = floor point. Collider on the pole only.</summary>
        public static GameObject LampPost(Transform parent, Vector3 pos, float height = 3.6f)
        {
            var root = Kit.Make("L11LampPost", parent, pos, Quaternion.identity);
            var t = root.transform;
            Kit.SolidBox(t, "Pole", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.14f, height, 0.14f), MatKey.MetalDark, 0.02f);
            Kit.BoxVisual(t, "Foot", new Vector3(0f, 0.15f, 0f), new Vector3(0.34f, 0.3f, 0.34f), MatKey.MetalDark, 0.03f);
            Kit.BoxVisual(t, "Cap", new Vector3(0f, height + 0.05f, 0f), new Vector3(0.42f, 0.1f, 0.42f), MatKey.MetalDark, 0.02f);
            ProcMesh.Part("Lantern", t, Sph(0.22f), M(MatKey.LightOn), new Vector3(0f, height - 0.22f, 0f), null, new Vector3(1f, 1.3f, 1f), false);
            ProcMesh.Part("Roof", t, Cone(0.3f, 0.3f), M(MatKey.MetalDark), new Vector3(0f, height + 0.1f, 0f), null, null, false);
            return root;
        }

        // ------------------------------------------------------------------ iron arch with a sign
        /// <summary>Wrought-iron arch across the path at 'pos' (floor point, passage along +Z), posts at +-halfWidth
        /// (visual only), sign board readable from -Z. Returns the sign.</summary>
        public static GameObject Arch(Transform parent, Vector3 pos, float halfWidth, float height, string text, Vector2 signSize, SignStyle style = SignStyle.Info)
        {
            var root = Kit.Make("L11Arch", parent, pos, Quaternion.identity);
            var t = root.transform;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.BoxVisual(t, "Post", new Vector3(s * halfWidth, height * 0.5f, 0f), new Vector3(0.18f, height, 0.18f), MatKey.MetalDark, 0.02f);
                ProcMesh.Part("Finial", t, Cone(0.14f, 0.35f), M(MatKey.MetalDark), new Vector3(s * halfWidth, height + 0.05f, 0f), null, null, false);
            }
            Kit.BoxVisual(t, "Bar", new Vector3(0f, height - 0.1f, 0f), new Vector3(halfWidth * 2f, 0.12f, 0.12f), MatKey.MetalDark, 0.02f);
            Kit.BoxVisual(t, "Curl", new Vector3(0f, height + 0.35f, 0f), new Vector3(halfWidth * 1.2f, 0.06f, 0.06f), MatKey.MetalDark, 0.01f);
            Kit.BoxVisual(t, "Glow", new Vector3(0f, height - 0.2f, -0.07f), new Vector3(halfWidth * 2f - 0.3f, 0.04f, 0.02f), MatKey.TrimGlow, 0f, null, false);
            return EnvKit.Sign(t, new Vector3(0f, height - 0.2f - signSize.y * 0.5f - 0.05f, -0.08f), Quaternion.identity, text, signSize, style);
        }

        // ------------------------------------------------------------------ ghost handshake hands
        /// <summary>Re-skins a Spikes strip as a row of glowing ghost hands reaching up ("FIRM HANDSHAKE!"): the spike
        /// meshes are hidden, the hands ride the SpikeRoot so they pop up with the hazard. Strip runs along local Z.</summary>
        public static void HandshakeSkin(Spikes sp, float length)
        {
            if (sp == null || sp.SpikeRoot == null) return;
            foreach (var r in sp.SpikeRoot.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            int n = Mathf.Max(2, Mathf.RoundToInt(length / 0.9f));
            var mat = M(MatKey.GhostGlow);
            for (int i = 0; i < n; i++)
            {
                float z = Mathf.Lerp(-length * 0.5f + 0.4f, length * 0.5f - 0.4f, n == 1 ? 0.5f : i / (float)(n - 1));
                var hand = Kit.Make("Hand", sp.SpikeRoot, new Vector3(0f, 0f, z), Quaternion.Euler(0f, i % 2 == 0 ? 15f : -15f, 0f)).transform;
                ProcMesh.Part("Palm", hand, ProcMesh.CachedBox(new Vector3(0.12f, 0.32f, 0.26f), 0.04f), mat, new Vector3(0f, 0.2f, 0f), null, null, false);
                ProcMesh.Part("Fingers", hand, ProcMesh.CachedBox(new Vector3(0.1f, 0.22f, 0.24f), 0.04f), mat, new Vector3(0f, 0.46f, 0.01f), null, null, false);
                ProcMesh.Part("Thumb", hand, ProcMesh.CachedBox(new Vector3(0.08f, 0.16f, 0.07f), 0.03f), mat, new Vector3(0.03f, 0.3f, 0.16f), Quaternion.Euler(-30f, 0f, 0f), null, false);
            }
        }

        // ------------------------------------------------------------------ statues
        /// <summary>Marble intern statue (~1.75 m) holding a coffee mug; faces local +Z (rot = plain rotation).</summary>
        public static GameObject InternStatue(Transform parent, Vector3 pos, Quaternion rot, float scale = 1f)
        {
            var root = Kit.Make("L11InternStatue", parent, pos, rot);
            var t = root.transform;
            var stone = MoonMarble;
            Kit.BoxVisual(t, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(0.7f, 0.2f, 0.7f), MatKey.Concrete, 0.02f);
            ProcMesh.Part("Body", t, Caps(0.23f * scale, 1.15f * scale), stone, new Vector3(0f, 0.2f + 0.6f * scale, 0f), null, null, true);
            ProcMesh.Part("Head", t, Sph(0.17f * scale), stone, new Vector3(0f, 0.2f + 1.38f * scale, 0f), null, null, true);
            ProcMesh.Part("Arm", t, Caps(0.07f * scale, 0.5f * scale), stone, new Vector3(0.2f * scale, 0.2f + 0.95f * scale, 0.14f * scale), Quaternion.Euler(70f, 0f, 0f), null, false);
            ProcMesh.Part("Mug", t, Cyl(0.06f * scale, 0.11f * scale), stone, new Vector3(0.2f * scale, 0.2f + 1.0f * scale, 0.36f * scale), null, null, false);
            return root;
        }

        /// <summary>Marble coffee machine statue (the 1999 Percy, honestly just brewing). Faces local +Z.</summary>
        public static GameObject CoffeeStatue(Transform parent, Vector3 pos, Quaternion rot)
        {
            var root = Kit.Make("L11CoffeeStatue", parent, pos, rot);
            var t = root.transform;
            var stone = MoonMarble;
            Kit.BoxVisual(t, "Base", new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.8f), MatKey.Concrete, 0.03f);
            ProcMesh.Part("Body", t, ProcMesh.CachedBox(new Vector3(0.62f, 0.95f, 0.55f), 0.05f), stone, new Vector3(0f, 0.98f, 0f), null, null, true);
            ProcMesh.Part("Carafe", t, Cyl(0.13f, 0.26f), stone, new Vector3(0f, 0.75f, 0.24f), null, null, false);
            ProcMesh.Part("Steam", t, Sph(0.1f), M(MatKey.GhostGlow), new Vector3(0f, 1.62f, 0.05f), null, new Vector3(1f, 1.6f, 1f), false);
            return root;
        }

        // ------------------------------------------------------------------ the mausoleum
        /// <summary>Walk-in mausoleum, front wall at z0 (doorway 4.6 x 3.6 m centred on x=0), hall to z0+depth, back
        /// wall with a 5.0 x 4.9 m opening for the exit cab. pos = floor point of the doorway centre. Colliders on walls
        /// and roof. Returns the root; children "DoorL"/"DoorR" (hinges, rotate localRotation about Y to open).</summary>
        public static GameObject Mausoleum(Transform parent, Vector3 pos, float halfWidth, float depth, float height, string inscription)
        {
            var root = Kit.Make("L11Mausoleum", parent, pos, Quaternion.identity);
            var t = root.transform;
            const float wt = 0.5f, dHalf = 2.3f, dH = 3.6f, backHalf = 2.5f, backH = 4.9f;
            // front wall (two side pieces + lintel)
            float sideW = halfWidth - dHalf;
            for (int s = -1; s <= 1; s += 2)
                Kit.SolidBox(t, "Front", new Vector3(s * (dHalf + sideW * 0.5f), height * 0.5f, 0f), new Vector3(sideW, height, wt), MatKey.WallAlt, 0.04f);
            Kit.SolidBox(t, "Lintel", new Vector3(0f, dH + (height - dH) * 0.5f, 0f), new Vector3(dHalf * 2f, height - dH, wt), MatKey.WallAlt, 0.04f);
            // side walls
            for (int s = -1; s <= 1; s += 2)
                Kit.SolidBox(t, "Side", new Vector3(s * (halfWidth - wt * 0.5f), height * 0.5f, depth * 0.5f), new Vector3(wt, height, depth), MatKey.WallAlt, 0.04f);
            // back wall with the cab opening
            float bw = halfWidth - backHalf;
            for (int s = -1; s <= 1; s += 2)
                Kit.SolidBox(t, "Back", new Vector3(s * (backHalf + bw * 0.5f), height * 0.5f, depth), new Vector3(bw, height, wt), MatKey.WallAlt, 0.04f);
            Kit.SolidBox(t, "BackLintel", new Vector3(0f, backH + (height - backH) * 0.5f, depth), new Vector3(backHalf * 2f, height - backH, wt), MatKey.WallAlt, 0.04f);
            // roof + pediment
            Kit.SolidBox(t, "Roof", new Vector3(0f, height + 0.2f, depth * 0.5f), new Vector3(halfWidth * 2f + 0.8f, 0.4f, depth + 1.0f), MatKey.Concrete, 0.05f);
            Kit.BoxVisual(t, "Cornice", new Vector3(0f, height + 0.5f, -0.35f), new Vector3(halfWidth * 2f + 1.0f, 0.2f, 0.4f), MatKey.Concrete, 0.04f);
            for (int s = -1; s <= 1; s += 2)
                Kit.BoxVisual(t, "Pediment", new Vector3(s * halfWidth * 0.48f, height + 0.95f, -0.3f), new Vector3(halfWidth * 1.15f, 0.3f, 0.35f), MatKey.Concrete, 0.03f, Quaternion.Euler(0f, 0f, -s * 14f));
            // columns
            foreach (float x in new[] { -halfWidth + 0.45f, -dHalf - 0.45f, dHalf + 0.45f, halfWidth - 0.45f })
            {
                ProcMesh.Part("Column", t, Cyl(0.32f, height - 0.3f), M(MatKey.Marble), new Vector3(x, (height - 0.3f) * 0.5f, -0.5f), null, null, true);
                Kit.BoxVisual(t, "ColumnCap", new Vector3(x, height - 0.15f, -0.5f), new Vector3(0.85f, 0.3f, 0.85f), MatKey.Concrete, 0.03f);
                Kit.BoxVisual(t, "ColumnFoot", new Vector3(x, 0.12f, -0.5f), new Vector3(0.85f, 0.24f, 0.85f), MatKey.Concrete, 0.03f);
            }
            // glowing threshold + doorway trim (walkable edge glow)
            Kit.BoxVisual(t, "Threshold", new Vector3(0f, 0.01f, -0.3f), new Vector3(dHalf * 2f, 0.02f, 0.12f), MatKey.TrimGlow, 0f, null, false);
            for (int s = -1; s <= 1; s += 2)
                Kit.BoxVisual(t, "DoorTrim", new Vector3(s * (dHalf - 0.04f), dH * 0.5f, -0.27f), new Vector3(0.06f, dH, 0.04f), MatKey.TrimGlow, 0f, null, false);
            // inscription
            var ins = PerspectiveFonts.MakeText(t, "Inscription", inscription, 0.75f, new Color(0.86f, 0.88f, 0.92f), new Vector3(0f, dH + 0.75f, -0.27f), Quaternion.identity, true);
            if (ins != null) ins.fontStyle = TMPro.FontStyles.Bold;
            // doors (visual only; L11MausoleumDoors swings them open)
            for (int s = -1; s <= 1; s += 2)
            {
                var hinge = Kit.Make(s < 0 ? "DoorL" : "DoorR", t, new Vector3(s * dHalf, 0f, -0.3f), Quaternion.identity).transform;
                Kit.BoxVisual(hinge, "Panel", new Vector3(-s * dHalf * 0.5f, dH * 0.5f, 0f), new Vector3(dHalf - 0.05f, dH - 0.05f, 0.1f), MatKey.MetalRust, 0.02f);
                Kit.BoxVisual(hinge, "Bars", new Vector3(-s * dHalf * 0.5f, dH * 0.6f, -0.06f), new Vector3(dHalf - 0.5f, 0.08f, 0.03f), MatKey.MetalDark, 0f);
            }
            // interior floor
            Kit.BoxVisual(t, "HallFloor", new Vector3(0f, 0.005f, depth * 0.5f), new Vector3(halfWidth * 2f - 1f, 0.01f, depth - 0.6f), MatKey.Marble, 0f, null, false);
            return root;
        }

        // ------------------------------------------------------------------ backdrop
        /// <summary>Leafless tree, pos = floor point. Visual only.</summary>
        public static GameObject DeadTree(Transform parent, Vector3 pos, float height, int seed)
        {
            var root = Kit.Make("L11DeadTree", parent, pos, Quaternion.Euler(0f, seed * 47f % 360f, 0f));
            var t = root.transform;
            var bark = M(MatKey.Black);
            ProcMesh.Part("Trunk", t, Cyl(0.22f, height), bark, new Vector3(0f, height * 0.5f, 0f), null, null, false);
            for (int i = 0; i < 3; i++)
            {
                float y = height * (0.55f + 0.15f * i);
                float a = (i * 120f + seed * 31f) % 360f;
                ProcMesh.Part("Branch", t, Cyl(0.07f, height * 0.45f), bark, Quaternion.Euler(0f, a, 0f) * new Vector3(0.45f, y, 0f), Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(0f, 0f, -50f), null, false);
            }
            return root;
        }

        /// <summary>The moon: a big emissive disc far away (grey-immune Moon material).</summary>
        public static GameObject Moon(Transform parent, Vector3 pos, float radius)
            => ProcMesh.Part("L11Moon", parent, Sph(radius), M(MatKey.Moon), pos, null, null, false);
    }
}
