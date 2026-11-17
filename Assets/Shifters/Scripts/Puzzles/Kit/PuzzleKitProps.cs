using System.Globalization;
using UnityEngine;

namespace Shifters
{
    public static partial class PuzzleKit
    {
        // ================================================================== props (pos = bottom center on the floor)
        static Rigidbody PropBody(GameObject go, float mass)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.angularDamping = 0.3f;
            return rb;
        }

        static void PropCommon(GameObject go, Rigidbody rb, string display, SfxId impact, float pitch, Pushable push = null)
        {
            if (push == null)
            {
                var g = go.AddComponent<Grabbable>();
                g.DisplayName = display;
            }
            go.AddComponent<WeightBody>();
            go.AddComponent<Respawnable>();
            var imp = go.AddComponent<PropImpact>();
            imp.Sound = impact; imp.Pitch = pitch;
            var info = go.AddComponent<PropInfo>();
            info.Body = rb; info.Push = push;
            info.Reveal = Reveal(go, PerspectiveMask.Strength, Mathf.RoundToInt(rb.mass) + " KG", new Vector3(0f, 1.2f, 0f), push != null);
        }

        static Mesh CrateMesh(float size)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "pz_crate_{0:0.##}", size);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float h = size * 0.5f, t = size * 0.13f;
                var core = new Color(0.86f, 0.86f, 0.86f);
                var edge = Color.white;
                var plank = new Color(0.72f, 0.72f, 0.72f);
                b.AddBox(Vector3.zero, Vector3.one * (size * 0.94f), Quaternion.identity, core, 0.01f);
                for (int a = 0; a < 3; a++)
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            var c = Vector3.zero; var sz = Vector3.one * t;
                            int i1 = (a + 1) % 3, i2 = (a + 2) % 3;
                            c[i1] = s1 * (h - t * 0.5f); c[i2] = s2 * (h - t * 0.5f);
                            sz[a] = size;
                            b.AddBox(c, sz, Quaternion.identity, edge, 0.012f);
                        }
                // diagonal braces on the four side faces
                float diag = size * 1.18f;
                for (int f = 0; f < 4; f++)
                {
                    var rot = Quaternion.Euler(0f, f * 90f, 0f);
                    var c = rot * new Vector3(0f, 0f, h - t * 0.35f);
                    b.AddBox(c, new Vector3(diag, t * 0.75f, t * 0.6f), rot * Quaternion.Euler(0f, 0f, 45f), plank, 0.01f);
                }
                return b.ToMesh(key);
            });
        }

        /// <summary>Wooden crate: small 0.9 m / 25 kg (anyone carries it) or medium 1.2 m / 60 kg. Grabbable + WeightBody + Respawnable.</summary>
        public static Rigidbody Crate(Transform parent, Vector3 pos, Quaternion? rot = null, bool medium = false)
        {
            float size = medium ? 1.2f : 0.9f;
            var r = Rot(rot);
            var go = Kit.Make(medium ? "CrateMedium" : "CrateSmall", parent, pos + r * Vector3.up * (size * 0.5f), r);
            ProcMesh.AddRenderer(go, CrateMesh(size), M(MatKey.Wood));
            go.AddComponent<BoxCollider>().size = Vector3.one * size;
            var rb = PropBody(go, medium ? Tuning.CrateMediumKg : Tuning.CrateSmallKg);
            PropCommon(go, rb, medium ? "big crate" : "crate", SfxId.Bonk, medium ? 0.8f : 1.1f);
            return rb;
        }

        static Mesh BarrelMesh()
        {
            return ProcMesh.Cached("pz_barrel_v1", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(Vector3.zero, 0.4f, 1f, Quaternion.identity, Color.white, 16, true);
                var hoop = new Color(0.45f, 0.45f, 0.5f);
                foreach (var y in new[] { -0.36f, 0.36f })
                    b.AddCylinder(new Vector3(0f, y, 0f), 0.42f, 0.07f, Quaternion.identity, hoop, 16, true);
                b.AddCylinder(new Vector3(0f, 0.5f, 0f), 0.36f, 0.02f, Quaternion.identity, new Color(0.8f, 0.8f, 0.8f), 16, false);
                return b.ToMesh("pz_barrel_v1");
            });
        }

        /// <summary>Barrel (30 kg, ~0.84 x 1.0 m) that rolls when knocked over. Grabbable + WeightBody + Respawnable.</summary>
        public static Rigidbody Barrel(Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            var r = Rot(rot);
            var go = Kit.Make("Barrel", parent, pos + r * Vector3.up * 0.5f, r);
            ProcMesh.AddRenderer(go, BarrelMesh(), M(MatKey.Barrel));
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = ProcMesh.Cached("pz_barrel_col", () => ProcMesh.Cylinder(0.42f, 1f, 12, false));
            mc.convex = true;
            var rb = PropBody(go, Tuning.BarrelKg);
            PropCommon(go, rb, "barrel", SfxId.Bonk, 0.75f);
            return rb;
        }

        /// <summary>160 kg steel block (1.6 m) with orange HEAVY stripes. Pushable only by STR >= 7 (Tuning.HeavyPushStrength).</summary>
        public static Pushable HeavyBlock(Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            const float s = 1.6f;
            var r = Rot(rot);
            var go = Kit.Make("HeavyBlock", parent, pos + r * Vector3.up * (s * 0.5f), r);
            var t = go.transform;
            PuzzleVisuals.Box(t, "Core", Vector3.zero, Vector3.one * s, MatKey.CrateHeavy, 0.07f);
            foreach (var y in new[] { -0.5f, 0.5f })
                PuzzleVisuals.Box(t, "Band", new Vector3(0f, y, 0f), new Vector3(s + 0.03f, 0.2f, s + 0.03f), MatKey.SafetyOrange, 0.02f);
            for (int f = 0; f < 4; f++)
            {
                var dir = Quaternion.Euler(0f, f * 90f, 0f) * Vector3.forward;
                FaceText(t, "Heavy", dir * (s * 0.5f + 0.005f), dir, "HEAVY\n<size=55%>160 KG</size>", 3.2f, UIColors.AccentYellow, s * 0.9f, 0.7f);
            }
            go.AddComponent<BoxCollider>().size = Vector3.one * s;
            var rb = PropBody(go, Tuning.BlockHeavyKg);
            var push = go.AddComponent<Pushable>();
            push.RequiredStrength = Tuning.HeavyPushStrength;
            PropCommon(go, rb, "heavy block", SfxId.LandHeavy, 0.7f, push);
            return push;
        }

        /// <summary>Glass pane (size x height, 0.1 thick) in a light frame. Breaks on 1 shot or a thrown body; ON once broken.</summary>
        public static Breakable GlassPane(Transform parent, Vector3 pos, Quaternion? rot = null, Vector2? size = null)
        {
            var sz = size ?? new Vector2(3f, 3f);
            var root = Kit.Make("GlassPane", parent, pos, Rot(rot));
            var t = root.transform;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, sz.y * 0.5f, 0f); col.size = new Vector3(sz.x, sz.y, 0.14f);
            var intact = Kit.Make("Intact", t, Vector3.zero);
            var glass = PuzzleVisuals.Box(intact.transform, "Glass", new Vector3(0f, sz.y * 0.5f, 0f), new Vector3(sz.x, sz.y, 0.06f), MatKey.Glass, 0.01f, null, false);
            PuzzleVisuals.Box(intact.transform, "Glint", new Vector3(-sz.x * 0.2f, sz.y * 0.62f, 0.035f), new Vector3(0.08f, sz.y * 0.5f, 0.005f), MatKey.SignWhite, 0.002f, Quaternion.Euler(0f, 0f, -25f), false);
            for (int s = -1; s <= 1; s += 2)
                PuzzleVisuals.Box(t, "FrameSide", new Vector3(s * (sz.x * 0.5f + 0.06f), sz.y * 0.5f, 0f), new Vector3(0.12f, sz.y + 0.12f, 0.2f), MatKey.MetalLight);
            PuzzleVisuals.Box(t, "FrameTop", new Vector3(0f, sz.y + 0.06f, 0f), new Vector3(sz.x + 0.24f, 0.12f, 0.2f), MatKey.MetalLight);
            PuzzleVisuals.Box(t, "Sill", new Vector3(0f, 0.03f, 0f), new Vector3(sz.x, 0.06f, 0.22f), MatKey.MetalLight);
            var b = root.AddComponent<Breakable>();
            b.Kind = BreakableKind.Glass; b.ShotsToBreak = 1; b.Size = new Vector3(sz.x, sz.y, 0.1f);
            b.Intact = intact; b.Solid = col; b.ShardMaterial = M(MatKey.Glass);
            b.Reveal = Reveal(root, PerspectiveMask.Weapon, "SHOOT TO BREAK", new Vector3(0f, sz.y + 0.5f, 0f), true, false, new Renderer[] { glass });
            return b;
        }

        /// <summary>Cracked concrete wall: E-punch by STR >= 7, a thrown strong character, or shotsToBreak hits (0 = bullet-proof).</summary>
        public static Breakable CrackedWall(Transform parent, Vector3 pos, Quaternion? rot = null, Vector3? size = null, int shotsToBreak = 4)
        {
            var sz = size ?? new Vector3(3f, 3.6f, 0.6f);
            var root = Kit.Make("CrackedWall", parent, pos, Rot(rot));
            var t = root.transform;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, sz.y * 0.5f, 0f); col.size = sz;
            var intact = Kit.Make("Intact", t, Vector3.zero);
            var wall = PuzzleVisuals.Box(intact.transform, "Wall", new Vector3(0f, sz.y * 0.5f, 0f), sz, MatKey.Concrete, 0.05f);
            string key = string.Format(CultureInfo.InvariantCulture, "pz_cracks_{0:0.##}x{1:0.##}x{2:0.##}", sz.x, sz.y, sz.z);
            var cracks = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var rng = new System.Random(7);
                for (int f = -1; f <= 1; f += 2)
                    for (int k = 0; k < 3; k++)
                    {
                        var p = new Vector3((k - 1) * sz.x * 0.25f, sz.y * (0.25f + 0.2f * k), f * (sz.z * 0.5f + 0.004f));
                        for (int seg = 0; seg < 5; seg++)
                        {
                            float ang = (float)(rng.NextDouble() * 140.0 - 70.0) + (seg % 2 == 0 ? 60f : -60f);
                            float len = 0.25f + (float)rng.NextDouble() * 0.35f;
                            var dir = Quaternion.Euler(0f, 0f, ang) * Vector3.up;
                            b.AddBox(p + dir * len * 0.5f, new Vector3(0.035f, len, 0.01f), Quaternion.Euler(0f, 0f, ang), Color.white, 0f);
                            p += dir * len;
                        }
                    }
                return b.ToMesh(key);
            });
            ProcMesh.Part("Cracks", intact.transform, cracks, M(MatKey.Black), Vector3.zero, null, null, false);
            FaceText(intact.transform, "Graffiti", new Vector3(0f, sz.y * 0.45f, sz.z * 0.5f), Vector3.forward, "PUNCH\nHERE", 4f, UIColors.Danger, sz.x * 0.8f, 1.2f);
            var br = root.AddComponent<Breakable>();
            br.Kind = BreakableKind.CrackedWall; br.ShotsToBreak = shotsToBreak; br.RequiredStrength = Tuning.HeavyPushStrength;
            br.Size = sz; br.Intact = intact; br.Solid = col; br.ShardMaterial = M(MatKey.Concrete);
            br.Reveal = Reveal(root, PerspectiveMask.Strength, "CRACKED WALL", new Vector3(0f, sz.y + 0.4f, 0f), true, false, new Renderer[] { wall });
            return br;
        }
    }
}
