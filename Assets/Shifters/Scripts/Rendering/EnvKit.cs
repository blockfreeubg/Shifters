// =====================================================================================
//  SHIFTERS - EnvKit.cs   (RENDERING)
//  Environment kit factories for level builders (Kit conventions: build everything inside the factory,
//  ProcMesh.Cached meshes, Kit.Mat(MatKey) materials for Kit.Theme, works in edit mode and at runtime).
//  CONVENTIONS
//    * Floor / Platform / AcidPool / VoidPit: 'center' is the TOP-surface center (= walk height).
//    * Wall / Railing / CableTray: 'from'/'to' are points on the base line (wall bottom = from.y).
//    * Mounted pieces (Sign, Poster, LightPanel, Decor.Whiteboard/MotivationalPoster/HoloPanel/SparkingPanel):
//      rot = the direction the VIEWER looks (rot*forward points into the wall); the readable / lit face is on the
//      -forward side (same as TextMeshPro and LevelBuildContext.Sign/Label). Wall in front of a viewer looking +Z:
//      Quaternion.identity.
//    * Everything walkable or bumpable has colliders (floors, platforms, walls, pillars, ramps, stairs,
//      railings); decor is collider-less unless large. Returned GameObject = the root of the piece.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class EnvKit
    {
        static Material M(MatKey k) => Kit.Mat(k);
        static Color C(MatKey k) => EnvMeshes.C(k);

        // ================================================================== floors & platforms
        /// <summary>Beveled floor slab with trim edge bands. center = TOP surface center, size = (x, thickness, z).
        /// Collider: BoxCollider on child "Slab".</summary>
        public static GameObject Floor(Transform parent, Vector3 center, Vector3 size, MatKey mat = MatKey.Floor, bool trim = true)
        {
            size = new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.05f, size.y), Mathf.Max(0.1f, size.z));
            var root = Kit.Make("Floor", parent, center);
            Kit.SolidBox(root.transform, "Slab", new Vector3(0f, -size.y * 0.5f, 0f), size, mat, 0.03f);
            if (trim) TrimBand(root.transform, size, MatKey.Trim, Mathf.Min(0.12f, size.y * 0.45f));
            return root;
        }

        /// <summary>Floating platform: deck + inset hull + designed underside (ribs, thruster glow), optional
        /// yellow/black hazard stripes on the top perimeter. center = TOP surface center. Colliders: deck + hull boxes.</summary>
        public static GameObject Platform(Transform parent, Vector3 center, Vector3 size, bool hazardEdges = true)
        {
            size = new Vector3(Mathf.Max(0.5f, size.x), Mathf.Max(0.2f, size.y), Mathf.Max(0.5f, size.z));
            var root = Kit.Make("Platform", parent, center);
            var t = root.transform;
            float deck = Mathf.Min(size.y, 0.32f);
            Kit.SolidBox(t, "Deck", new Vector3(0f, -deck * 0.5f, 0f), new Vector3(size.x, deck, size.z), MatKey.FloorAlt, 0.04f);
            float hullH = size.y - deck;
            float inset = Mathf.Min(0.35f, Mathf.Min(size.x, size.z) * 0.12f);
            if (hullH > 0.04f)
                Kit.SolidBox(t, "Hull", new Vector3(0f, -deck - hullH * 0.5f, 0f), new Vector3(size.x - inset * 2f, hullH, size.z - inset * 2f), MatKey.MetalDark, 0.05f);
            TrimBand(t, new Vector3(size.x, deck, size.z), MatKey.Trim, Mathf.Min(0.1f, deck * 0.4f));

            // underside: ribs + thruster pod (merged, vertex colored)
            float bottom = -size.y;
            var key = EnvMeshes.Key("env_platform_under", Kit.Theme, size);
            var under = EnvMeshes.Cached(key, p =>
            {
                var rib = C(MatKey.MetalLight);
                var dark = Palettes.Shade(C(MatKey.MetalDark), 0.8f);
                int ribs = Mathf.Max(2, Mathf.RoundToInt(size.x / 1.6f));
                for (int i = 0; i < ribs; i++)
                {
                    float x = Mathf.Lerp(-size.x * 0.5f + inset + 0.2f, size.x * 0.5f - inset - 0.2f, ribs == 1 ? 0.5f : i / (float)(ribs - 1));
                    p.Box(new Vector3(x, bottom - 0.06f, 0f), new Vector3(0.12f, 0.12f, size.z - inset * 2f - 0.1f), rib, 0.02f);
                }
                float pod = Mathf.Min(size.x, size.z) * 0.22f;
                p.Cyl(new Vector3(0f, bottom - 0.2f, 0f), pod, 0.3f, dark, 16, null, false);
                p.Cone(new Vector3(0f, bottom - 0.35f, 0f), pod * 0.75f, 0.35f, rib, 16, Quaternion.Euler(180f, 0f, 0f));
            });
            EnvMeshes.Render(t, "Underside", under, M(MatKey.White), Vector3.zero);
            float podR = Mathf.Min(size.x, size.z) * 0.22f;
            EnvMeshes.Render(t, "Thruster", ProcMesh.Cached(EnvMeshes.Key("env_thruster", podR), () => ProcMesh.Cylinder(podR * 0.55f, 0.06f, 16)), M(MatKey.LightBlue),
                new Vector3(0f, bottom - 0.72f, 0f), null, false);

            if (hazardEdges)
            {
                float w = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.1f, 0.12f, 0.3f);
                var hk = EnvMeshes.Key("env_hazard_edge", size.x, size.z, w);
                var edge = EnvMeshes.Cached(hk, p =>
                {
                    var c = Color.white; float h = 0.012f, y = 0.006f;
                    p.Box(new Vector3(0f, y, size.z * 0.5f - w * 0.5f), new Vector3(size.x, h, w), c, 0f);
                    p.Box(new Vector3(0f, y, -size.z * 0.5f + w * 0.5f), new Vector3(size.x, h, w), c, 0f);
                    p.Box(new Vector3(size.x * 0.5f - w * 0.5f, y, 0f), new Vector3(w, h, size.z - w * 2f), c, 0f);
                    p.Box(new Vector3(-size.x * 0.5f + w * 0.5f, y, 0f), new Vector3(w, h, size.z - w * 2f), c, 0f);
                });
                EnvMeshes.Render(t, "HazardEdges", edge, M(MatKey.HazardStripes), Vector3.zero, null, false);
            }
            return root;
        }

        /// <summary>Thin trim band on the side faces just below the top edge (top at +4 mm to avoid z-fighting).</summary>
        static void TrimBand(Transform t, Vector3 size, MatKey mat, float h)
        {
            var key = EnvMeshes.Key("env_trimband", size.x, size.z, h);
            var mesh = EnvMeshes.Cached(key, p =>
            {
                var c = Color.white; const float pr = 0.025f, top = 0.004f;
                float y = top - h * 0.5f;
                p.Box(new Vector3(0f, y, size.z * 0.5f), new Vector3(size.x + pr * 2f, h, pr * 2f), c, 0.008f);
                p.Box(new Vector3(0f, y, -size.z * 0.5f), new Vector3(size.x + pr * 2f, h, pr * 2f), c, 0.008f);
                p.Box(new Vector3(size.x * 0.5f, y, 0f), new Vector3(pr * 2f, h, size.z), c, 0.008f);
                p.Box(new Vector3(-size.x * 0.5f, y, 0f), new Vector3(pr * 2f, h, size.z), c, 0.008f);
            });
            EnvMeshes.Render(t, "Trim", mesh, M(mat), Vector3.zero);
        }

        // ================================================================== walls, pillars
        /// <summary>Wall from 'from' to 'to' (base line), rising 'height'. Kick plate, trim cap and an optional emissive
        /// accent strip. Collider: BoxCollider on child "Body".</summary>
        public static GameObject Wall(Transform parent, Vector3 from, Vector3 to, float height, float thickness = 0.4f, MatKey mat = MatKey.Wall, bool accentStrip = true)
        {
            var d = to - from; d.y = 0f;
            float len = Mathf.Max(0.1f, d.magnitude);
            var rot = d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d / len) : Quaternion.identity;
            var root = Kit.Make("Wall", parent, (from + new Vector3(to.x, from.y, to.z)) * 0.5f, rot);
            var t = root.transform;
            height = Mathf.Max(0.2f, height); thickness = Mathf.Max(0.05f, thickness);
            Kit.SolidBox(t, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(thickness, height, len), mat, 0.02f);
            Kit.BoxVisual(t, "Kick", new Vector3(0f, 0.11f, 0f), new Vector3(thickness + 0.06f, 0.22f, len - 0.002f), MatKey.MetalDark, 0.02f);
            Kit.BoxVisual(t, "Cap", new Vector3(0f, height, 0f), new Vector3(thickness + 0.1f, 0.1f, len + 0.02f), MatKey.Trim, 0.02f);
            if (accentStrip && height >= 2f)
                Kit.BoxVisual(t, "Accent", new Vector3(0f, Mathf.Min(1.3f, height * 0.45f), 0f), new Vector3(thickness + 0.03f, 0.06f, len - 0.2f), MatKey.TrimGlow, 0f, null, false);
            return root;
        }

        /// <summary>Square pillar with plinth, trim cap and glowing vertical strips. basePos = bottom center. Collider: body box.</summary>
        public static GameObject Pillar(Transform parent, Vector3 basePos, float height, float width = 0.9f)
        {
            var root = Kit.Make("Pillar", parent, basePos);
            var t = root.transform;
            height = Mathf.Max(0.3f, height); width = Mathf.Max(0.1f, width);
            Kit.SolidBox(t, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width), MatKey.WallAlt, 0.04f);
            Kit.BoxVisual(t, "Plinth", new Vector3(0f, 0.15f, 0f), new Vector3(width + 0.2f, 0.3f, width + 0.2f), MatKey.MetalDark, 0.04f);
            Kit.BoxVisual(t, "Cap", new Vector3(0f, height - 0.09f, 0f), new Vector3(width + 0.16f, 0.18f, width + 0.16f), MatKey.Trim, 0.03f);
            float sh = height * 0.6f;
            var key = EnvMeshes.Key("env_pillar_strips", width, sh);
            var strips = EnvMeshes.Cached(key, p =>
            {
                var c = Color.white;
                p.Box(new Vector3(0f, 0f, width * 0.5f + 0.01f), new Vector3(0.07f, sh, 0.03f), c, 0f);
                p.Box(new Vector3(0f, 0f, -width * 0.5f - 0.01f), new Vector3(0.07f, sh, 0.03f), c, 0f);
                p.Box(new Vector3(width * 0.5f + 0.01f, 0f, 0f), new Vector3(0.03f, sh, 0.07f), c, 0f);
                p.Box(new Vector3(-width * 0.5f - 0.01f, 0f, 0f), new Vector3(0.03f, sh, 0.07f), c, 0f);
            });
            EnvMeshes.Render(t, "Glow", strips, M(MatKey.TrimGlow), new Vector3(0f, height * 0.5f, 0f), null, false);
            return root;
        }

        // ================================================================== ramps & stairs
        /// <summary>Wedge ramp rising from 'bottom' to 'top' (points on the walking surface centerline). Keep it under
        /// ~35 degrees. Collider: MeshCollider (wedge).</summary>
        public static GameObject Ramp(Transform parent, Vector3 bottom, Vector3 top, float width = 2f, MatKey mat = MatKey.FloorAlt)
        {
            if (top.y < bottom.y) { var tmp = top; top = bottom; bottom = tmp; }
            var hd = top - bottom; hd.y = 0f;
            float len = Mathf.Max(0.2f, hd.magnitude), h = Mathf.Max(0.05f, top.y - bottom.y);
            var rot = hd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(hd / len) : Quaternion.identity;
            var root = Kit.Make("Ramp", parent, bottom, rot);
            var t = root.transform;
            var size = new Vector3(width, h, len);
            var mesh = ProcMesh.Cached(EnvMeshes.Key("env_ramp", size), () => ProcMesh.Wedge(size));
            var r = EnvMeshes.Render(t, "Slope", mesh, M(mat), new Vector3(0f, 0f, len * 0.5f));
            var mc = r.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            float slopeLen = Mathf.Sqrt(len * len + h * h), ang = Mathf.Atan2(h, len) * Mathf.Rad2Deg;
            var q = Quaternion.Euler(-ang, 0f, 0f);
            Kit.BoxVisual(t, "EdgeL", new Vector3(-width * 0.5f + 0.04f, h * 0.5f + 0.02f, len * 0.5f), new Vector3(0.09f, 0.05f, slopeLen), MatKey.Trim, 0.01f, q);
            Kit.BoxVisual(t, "EdgeR", new Vector3(width * 0.5f - 0.04f, h * 0.5f + 0.02f, len * 0.5f), new Vector3(0.09f, 0.05f, slopeLen), MatKey.Trim, 0.01f, q);
            return root;
        }

        /// <summary>Solid stairs. start = bottom front edge center (floor level), dir = horizontal climb direction.
        /// Colliders: one BoxCollider per step + a smooth slope BoxCollider through the step noses (no snagging).</summary>
        public static GameObject Stairs(Transform parent, Vector3 start, Vector3 dir, float width, float rise, float run, int steps, MatKey mat = MatKey.Concrete)
        {
            dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            steps = Mathf.Clamp(steps, 1, 64); rise = Mathf.Max(0.02f, rise); run = Mathf.Max(0.05f, run); width = Mathf.Max(0.2f, width);
            var root = Kit.Make("Stairs", parent, start, Quaternion.LookRotation(dir.normalized));
            var t = root.transform;
            var key = EnvMeshes.Key("env_stairs", width, rise, run, steps);
            var mesh = EnvMeshes.Cached(key, p =>
            {
                for (int i = 0; i < steps; i++)
                    p.Box(new Vector3(0f, rise * (i + 1) * 0.5f, run * (i + 0.5f)), new Vector3(width, rise * (i + 1), run), Color.white, 0.015f);
            });
            EnvMeshes.Render(t, "Steps", mesh, M(mat), Vector3.zero);
            var nkey = EnvMeshes.Key("env_stair_nose", width, rise, run, steps);
            var nose = EnvMeshes.Cached(nkey, p =>
            {
                for (int i = 0; i < steps; i++)
                    p.Box(new Vector3(0f, rise * (i + 1) + 0.006f, run * i + 0.05f), new Vector3(width - 0.04f, 0.014f, 0.1f), Color.white, 0f);
            });
            EnvMeshes.Render(t, "Nosing", nose, M(MatKey.HazardYellow), Vector3.zero, null, false);
            for (int i = 0; i < steps; i++)
            {
                var bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, rise * (i + 1) * 0.5f, run * (i + 0.5f));
                bc.size = new Vector3(width, rise * (i + 1), run);
            }
            if (steps >= 2)
            {
                var a = new Vector3(0f, rise, 0f);
                var b = new Vector3(0f, rise * steps, run * (steps - 1));
                float ang = Mathf.Atan2(rise, run);
                var n = new Vector3(0f, Mathf.Cos(ang), -Mathf.Sin(ang));
                var slope = Kit.Make("SlopeCollider", t, (a + b) * 0.5f - n * 0.05f, Quaternion.Euler(-ang * Mathf.Rad2Deg, 0f, 0f));
                var sc = slope.AddComponent<BoxCollider>();
                sc.size = new Vector3(width, 0.1f, (b - a).magnitude);
            }
            return root;
        }

        // ================================================================== railings, pipes, cables
        /// <summary>Safety railing along the base line from-&gt;to (posts, yellow top rail, mid rail). Collider: one thin box.</summary>
        public static GameObject Railing(Transform parent, Vector3 from, Vector3 to, float height = 1.05f)
        {
            var d = to - from; d.y = 0f;
            float len = Mathf.Max(0.2f, d.magnitude);
            var root = Kit.Make("Railing", parent, from, d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d / len) : Quaternion.identity);
            var key = EnvMeshes.Key("env_railing", Kit.Theme, len, height);
            var mesh = EnvMeshes.Cached(key, p =>
            {
                var post = C(MatKey.MetalLight); var rail = C(MatKey.HazardYellow);
                int posts = Mathf.Max(2, Mathf.CeilToInt(len / 1.5f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float z = len * i / (posts - 1);
                    p.Cyl(new Vector3(0f, height * 0.5f, z), 0.035f, height, post, 8);
                    p.Cyl(new Vector3(0f, 0.02f, z), 0.07f, 0.04f, post, 8, null, false);
                }
                p.Rod(new Vector3(0f, height, 0f), new Vector3(0f, height, len), 0.045f, rail, 10);
                p.Rod(new Vector3(0f, height * 0.52f, 0f), new Vector3(0f, height * 0.52f, len), 0.025f, post, 8);
            });
            EnvMeshes.Render(root.transform, "Rails", mesh, M(MatKey.White), Vector3.zero);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, height * 0.5f, len * 0.5f);
            bc.size = new Vector3(0.12f, height, len);
            return root;
        }

        /// <summary>Pipe run through world points with joints and flanges. Collider: capsules per segment if collide.</summary>
        public static GameObject Pipe(Transform parent, Vector3[] points, float radius = 0.12f, bool collide = false, MatKey mat = MatKey.Pipe)
        {
            if (points == null || points.Length < 2) return Kit.Make("Pipe", parent, points != null && points.Length > 0 ? points[0] : Vector3.zero);
            var root = Kit.Make("Pipe", parent, points[0]);
            var rel = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) rel[i] = points[i] - points[0];
            var key = "env_pipe_" + EnvMeshes.Hash(rel) + "_" + rel.Length + "_" + radius.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var mesh = EnvMeshes.Cached(key, p =>
            {
                var c = Color.white; var fl = new Color(0.72f, 0.72f, 0.75f);
                for (int i = 0; i < rel.Length - 1; i++)
                {
                    var a = rel[i]; var b = rel[i + 1]; var seg = b - a; float L = seg.magnitude;
                    if (L < 1e-3f) continue;
                    p.Rod(a, b, radius, c, 14);
                    var q = Quaternion.FromToRotation(Vector3.up, seg / L);
                    int rings = Mathf.FloorToInt(L / 2.5f);
                    for (int k = 1; k <= rings; k++) p.Cyl(a + seg * (k / (float)(rings + 1)), radius * 1.22f, 0.07f, fl, 14, q);
                    if (i == 0) p.Cyl(a + seg / L * 0.04f, radius * 1.4f, 0.08f, fl, 14, q);
                    if (i == rel.Length - 2) p.Cyl(b - seg / L * 0.04f, radius * 1.4f, 0.08f, fl, 14, q);
                }
                for (int i = 1; i < rel.Length - 1; i++) p.Sphere(rel[i], radius * 1.18f, fl, null, 14, 8);
            });
            EnvMeshes.Render(root.transform, "Mesh", mesh, M(mat), Vector3.zero);
            if (collide)
            {
                for (int i = 0; i < rel.Length - 1; i++)
                {
                    var seg = rel[i + 1] - rel[i]; float L = seg.magnitude; if (L < 1e-3f) continue;
                    var cgo = Kit.Make("Col" + i, root.transform, (rel[i] + rel[i + 1]) * 0.5f, Quaternion.FromToRotation(Vector3.up, seg / L));
                    var cc = cgo.AddComponent<CapsuleCollider>(); cc.direction = 1; cc.radius = radius; cc.height = L + radius * 2f;
                }
            }
            return root;
        }

        /// <summary>Cable tray (U channel with colored cables and hangers) from-&gt;to. Decor, no collider.</summary>
        public static GameObject CableTray(Transform parent, Vector3 from, Vector3 to, float width = 0.45f)
        {
            var d = to - from; float len = Mathf.Max(0.2f, d.magnitude);
            var root = Kit.Make("CableTray", parent, from, Quaternion.LookRotation(d / len, Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up));
            var key = EnvMeshes.Key("env_cabletray", Kit.Theme, len, width);
            var mesh = EnvMeshes.Cached(key, p =>
            {
                var tray = C(MatKey.MetalLight); var dark = C(MatKey.MetalDark);
                p.Box(new Vector3(0f, 0f, len * 0.5f), new Vector3(width, 0.02f, len), tray, 0.005f);
                p.Box(new Vector3(width * 0.5f, 0.04f, len * 0.5f), new Vector3(0.02f, 0.09f, len), tray, 0.005f);
                p.Box(new Vector3(-width * 0.5f, 0.04f, len * 0.5f), new Vector3(0.02f, 0.09f, len), tray, 0.005f);
                Color[] cc = { UIColors.Hex(0xE8394B), UIColors.Hex(0x3C7DD9), UIColors.Hex(0xFFC93C), UIColors.Hex(0x22252B), UIColors.Hex(0x3DDC97) };
                int n = Mathf.Clamp(Mathf.RoundToInt(width / 0.09f), 2, 5);
                for (int i = 0; i < n; i++)
                {
                    float x = Mathf.Lerp(-width * 0.36f, width * 0.36f, n == 1 ? 0.5f : i / (float)(n - 1));
                    p.Rod(new Vector3(x, 0.04f, 0.02f), new Vector3(x, 0.04f, len - 0.02f), 0.028f, cc[i % cc.Length], 8);
                }
                int hangers = Mathf.Max(1, Mathf.FloorToInt(len / 2f));
                for (int i = 0; i <= hangers; i++)
                {
                    float z = Mathf.Lerp(0.1f, len - 0.1f, i / (float)hangers);
                    p.Box(new Vector3(0f, -0.02f, z), new Vector3(width + 0.08f, 0.03f, 0.05f), dark, 0f);
                    p.Box(new Vector3(width * 0.5f + 0.04f, 0.25f, z), new Vector3(0.025f, 0.5f, 0.025f), dark, 0f);
                    p.Box(new Vector3(-width * 0.5f - 0.04f, 0.25f, z), new Vector3(0.025f, 0.5f, 0.025f), dark, 0f);
                }
            });
            EnvMeshes.Render(root.transform, "Tray", mesh, M(MatKey.White), Vector3.zero, null, false);
            return root;
        }

        // ================================================================== lights & markings
        /// <summary>Emissive light panel (frame + glowing face) with an optional point light (no shadows) 0.6 m in front of
        /// the face. Mounted-piece convention: rot*forward points into the wall, the lit face looks back at the viewer
        /// (a panel lying on the floor facing up: Quaternion.Euler(90,0,0)). Returns the root; the Light is on child "Light".
        /// withPointLight defaults to FALSE (WebGL/Mobile: keep real-time point lights to a handful per area; the emissive
        /// face + bloom already reads as a light). The point light carries a DecorLight marker: on WebGL / the Mobile quality
        /// level the LightBudget pass switches it off at scene start (the glowing face stays).</summary>
        public static GameObject LightPanel(Transform parent, Vector3 pos, Quaternion rot, Vector2 size, Color color, bool withPointLight = false, float intensity = 2.5f, float range = 0f)
        {
            var root = Kit.Make("LightPanel", parent, pos, ViewToFace(rot));
            var t = root.transform;
            size = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
            Kit.BoxVisual(t, "Frame", Vector3.zero, new Vector3(size.x + 0.1f, size.y + 0.1f, 0.08f), MatKey.MetalDark, 0.02f);
            var face = EnvMeshes.Render(t, "Face", ProcMesh.CachedBox(new Vector3(size.x, size.y, 0.03f), 0.01f), MaterialLibrary.Colored(color, 0.2f, 2.2f), new Vector3(0f, 0f, 0.035f), null, false);
            face.name = "Face";
            if (withPointLight)
            {
                var lgo = Kit.Make("Light", t, new Vector3(0f, 0f, 0.6f));
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.color = color; l.intensity = intensity; l.shadows = LightShadows.None;
                l.range = range > 0f ? range : Mathf.Max(size.x, size.y) * 3f + 4f;
                DecorLight.Mark(l);
            }
            return root;
        }

        /// <summary>Yellow/black diagonal stripe slab (box center = center). Visual only.</summary>
        public static GameObject HazardStripe(Transform parent, Vector3 center, Vector3 size)
        {
            var r = Kit.BoxVisual(parent, "HazardStripe", center, size, MatKey.HazardStripes, Mathf.Min(0.01f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.2f));
            return r.gameObject;
        }

        /// <summary>Adds a Lab-style flicker to a LightPanel (or any object with a Light child / emissive "Face").</summary>
        public static LightFlicker AddFlicker(GameObject panel, float amount = 0.7f)
        {
            if (panel == null) return null;
            var f = panel.GetComponent<LightFlicker>();
            if (f == null) f = panel.AddComponent<LightFlicker>();
            f.Target = panel.GetComponentInChildren<Light>();
            var face = panel.transform.Find("Face");
            f.Glow = face != null ? face.GetComponent<Renderer>() : null;
            f.Amount = amount;
            return f;
        }
    }
}
