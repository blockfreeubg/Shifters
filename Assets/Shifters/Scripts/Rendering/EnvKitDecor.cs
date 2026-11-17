// =====================================================================================
//  SHIFTERS - EnvKitDecor.cs   (RENDERING)
//  EnvKit.Decor: themed decoration sets. Most props are ONE merged vertex-colored mesh (Kit.Mat(White))
//  + an optional emissive part, so they cost 1-2 draw calls. Small props have no colliders; large ones
//  (dummies, benches, machines, tanks, desks, crates, barrels, beams, jars, pillars) get simple colliders.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class EnvKit
    {
        public static partial class Decor   // partial (W0): rendering_decor adds Decor.Bank / Decor.Night / Decor.Tower in Rendering/CampaignDecor
        {
            static Material White => Kit.Mat(MatKey.White);
            static Color P(MatKey k) => EnvMeshes.C(k);
            static Color Hx(int rgb) => UIColors.Hex(rgb);

            static GameObject Prop(string name, Transform parent, Vector3 pos, Quaternion rot, string key, System.Action<EnvParts> build, bool shadows = true)
            {
                var root = Kit.Make(name, parent, pos, rot);
                EnvMeshes.Render(root.transform, "Mesh", EnvMeshes.Cached(key, build), White, Vector3.zero, null, shadows);
                return root;
            }

            static void BoxCol(GameObject go, Vector3 center, Vector3 size) { var b = go.AddComponent<BoxCollider>(); b.center = center; b.size = size; }

            // ============================================================ TRAINING
            /// <summary>Traffic cone (no collider).</summary>
            public static GameObject Cone(Transform parent, Vector3 pos)
                => Prop("Cone", parent, pos, Quaternion.identity, "decor_cone", p =>
                {
                    var o = Hx(0xFF6A1F);
                    p.Box(new Vector3(0, 0.025f, 0), new Vector3(0.42f, 0.05f, 0.42f), Hx(0x26282E), 0.015f);
                    p.Cone(new Vector3(0, 0.05f, 0), 0.17f, 0.62f, o, 14);
                    p.Cyl(new Vector3(0, 0.33f, 0), 0.105f, 0.08f, Color.white, 14, null, true);
                });

            /// <summary>Training dummy with a painted target (capsule collider).</summary>
            public static GameObject TrainingDummy(Transform parent, Vector3 pos, Quaternion rot)
            {
                var go = Prop("TrainingDummy", parent, pos, rot, "decor_dummy", p =>
                {
                    var tan = Hx(0xD9B36C); var dark = Hx(0x3A3F48); var red = Hx(0xE8394B);
                    p.Cyl(new Vector3(0, 0.06f, 0), 0.32f, 0.12f, dark, 16, null, false);
                    p.Cyl(new Vector3(0, 0.45f, 0), 0.05f, 0.7f, dark, 8);
                    p.Capsule(new Vector3(0, 1.15f, 0), 0.3f, 1.0f, tan, null, 14);
                    p.Sphere(new Vector3(0, 1.85f, 0), 0.2f, tan, null, 14, 10);
                    p.Rod(new Vector3(-0.55f, 1.35f, 0), new Vector3(0.55f, 1.35f, 0), 0.06f, tan, 8);
                    p.Cyl(new Vector3(0, 1.2f, 0.29f), 0.22f, 0.02f, Color.white, 20, Quaternion.Euler(90, 0, 0), false);
                    p.Cyl(new Vector3(0, 1.2f, 0.3f), 0.15f, 0.02f, red, 20, Quaternion.Euler(90, 0, 0), false);
                    p.Cyl(new Vector3(0, 1.2f, 0.31f), 0.07f, 0.02f, Color.white, 16, Quaternion.Euler(90, 0, 0), false);
                });
                var c = go.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, 1f, 0); c.radius = 0.32f; c.height = 2f;
                return go;
            }

            /// <summary>Park bench (box collider).</summary>
            public static GameObject Bench(Transform parent, Vector3 pos, Quaternion rot, float length = 1.8f)
            {
                var go = Prop("Bench", parent, pos, rot, EnvMeshes.Key("decor_bench", length), p =>
                {
                    var wood = Hx(0xC08A55); var metal = Hx(0x3D4552);
                    for (int i = 0; i < 3; i++) p.Box(new Vector3(0, 0.45f, -0.15f + i * 0.15f), new Vector3(length, 0.05f, 0.12f), wood, 0.012f);
                    p.Box(new Vector3(0, 0.75f, 0.27f), new Vector3(length, 0.18f, 0.05f), wood, 0.012f);
                    foreach (float x in new[] { -length * 0.4f, length * 0.4f })
                    {
                        p.Box(new Vector3(x, 0.22f, 0), new Vector3(0.06f, 0.44f, 0.45f), metal, 0.01f);
                        p.Box(new Vector3(x, 0.62f, 0.27f), new Vector3(0.05f, 0.35f, 0.05f), metal, 0.01f);
                    }
                });
                BoxCol(go, new Vector3(0, 0.4f, 0.05f), new Vector3(length, 0.8f, 0.6f));
                return go;
            }

            /// <summary>Potted office plant (no collider).</summary>
            public static GameObject Plant(Transform parent, Vector3 pos, int variant = 0)
                => Prop("Plant", parent, pos, Quaternion.Euler(0, variant * 47f, 0), EnvMeshes.Key("decor_plant", variant % 3), p =>
                {
                    var pot = Hx(0xD9764A); var soil = Hx(0x4A3426); var leaf = Hx(0x5DAA4A); var leaf2 = Hx(0x3F8A3A);
                    p.Cyl(new Vector3(0, 0.22f, 0), 0.22f, 0.44f, pot, 14, null, false);
                    p.Cyl(new Vector3(0, 0.45f, 0), 0.25f, 0.06f, Palettes.Shade(pot, 0.85f), 14, null, false);
                    p.Cyl(new Vector3(0, 0.47f, 0), 0.2f, 0.02f, soil, 12, null, false);
                    int leaves = 5 + variant % 3;
                    for (int i = 0; i < leaves; i++)
                    {
                        float a = i * 360f / leaves + variant * 13f;
                        var q = Quaternion.Euler(0, a, 0) * Quaternion.Euler(28f + (i % 2) * 18f, 0, 0);
                        p.Sphere(new Vector3(0, 0.5f, 0) + q * new Vector3(0, 0.38f, 0), 0.2f, i % 2 == 0 ? leaf : leaf2, new Vector3(0.55f, 1.6f, 0.25f), 8, 6, false, q);
                    }
                });

            /// <summary>Whiteboard on legs with marker text and a doodled chart (no collider). Readable by a viewer looking along
            /// rot*forward (mounted-piece convention).</summary>
            public static GameObject Whiteboard(Transform parent, Vector3 pos, Quaternion rot, string text = null)
            {
                var go = Prop("Whiteboard", parent, pos, ViewToFace(rot), "decor_whiteboard", p =>
                {
                    var frame = Hx(0xA7B1BE); var board = Hx(0xF7F8FA);
                    p.Box(new Vector3(0, 1.35f, 0), new Vector3(1.9f, 1.1f, 0.04f), frame, 0.01f);
                    p.Box(new Vector3(0, 1.35f, 0.012f), new Vector3(1.8f, 1.0f, 0.03f), board, 0f);
                    p.Box(new Vector3(0, 0.78f, 0.06f), new Vector3(1.6f, 0.03f, 0.1f), frame, 0.005f);
                    foreach (float x in new[] { -0.85f, 0.85f })
                    {
                        p.Rod(new Vector3(x, 0f, 0.25f), new Vector3(x, 1.9f, 0f), 0.022f, frame, 8);
                        p.Rod(new Vector3(x, 0f, -0.25f), new Vector3(x, 1.9f, 0f), 0.022f, frame, 8);
                    }
                    // doodled bar chart going down (funny)
                    Color[] bars = { Hx(0x3C7DD9), Hx(0x3DDC97), Hx(0xFFC93C), Hx(0xE8394B) };
                    for (int i = 0; i < 4; i++) p.Box(new Vector3(0.45f + i * 0.13f, 1.05f + (0.36f - i * 0.1f) * 0.5f, 0.03f), new Vector3(0.08f, 0.36f - i * 0.1f, 0.005f), bars[i], 0f);
                });
                AddText(go.transform, text ?? "PLAN:\n1. SHIFT\n2. ???\n3. PROFIT", new Vector3(-0.3f, 1.38f, 0.032f), new Vector2(1.05f, 0.85f), Hx(0x2A4FA8), ShiftersFonts.Label, 0.16f);
                return go;
            }

            /// <summary>Motivational corporate poster (index into the built-in funny lines; -1 = by position).</summary>
            public static GameObject MotivationalPoster(Transform parent, Vector3 pos, Quaternion rot, int index = -1)
            {
                if (index < 0) index = Mathf.Abs(Mathf.RoundToInt(pos.x * 7f + pos.z * 13f + pos.y * 3f));
                var l = PosterLines[index % PosterLines.Length];
                return Poster(parent, pos, rot, l[0], l[1]);
            }

            // ============================================================ FACTORY
            /// <summary>Yellow industrial machine with control panel lights (box collider).</summary>
            public static GameObject Machine(Transform parent, Vector3 pos, Quaternion rot, Vector3 size)
            {
                size = new Vector3(Mathf.Max(0.6f, size.x), Mathf.Max(0.6f, size.y), Mathf.Max(0.6f, size.z));
                var go = Prop("Machine", parent, pos, rot, EnvMeshes.Key("decor_machine", size), p =>
                {
                    var yel = Hx(0xF2B124); var dark = Hx(0x35312E); var steel = Hx(0x968B7E);
                    p.Box(new Vector3(0, 0.08f, 0), new Vector3(size.x + 0.1f, 0.16f, size.z + 0.1f), dark, 0.02f);
                    p.Box(new Vector3(0, size.y * 0.5f + 0.08f, 0), new Vector3(size.x, size.y - 0.16f, size.z), yel, 0.06f);
                    p.Box(new Vector3(0, size.y - 0.02f, 0), new Vector3(size.x * 0.7f, 0.12f, size.z * 0.7f), dark, 0.02f);
                    for (int i = 0; i < 4; i++) p.Box(new Vector3(-size.x * 0.25f, size.y * 0.35f + i * 0.09f, size.z * 0.5f + 0.01f), new Vector3(size.x * 0.35f, 0.04f, 0.02f), dark, 0f);
                    p.Box(new Vector3(size.x * 0.22f, size.y * 0.55f, size.z * 0.5f + 0.02f), new Vector3(size.x * 0.32f, size.y * 0.3f, 0.04f), steel, 0.01f);
                    p.Cyl(new Vector3(size.x * 0.32f, size.y + 0.25f, -size.z * 0.2f), 0.12f, 0.5f, steel, 10);
                });
                var lights = Kit.Make("Lights", go.transform, new Vector3(size.x * 0.22f, size.y * 0.55f, size.z * 0.5f + 0.045f));
                Kit.BoxVisual(lights.transform, "On", new Vector3(-size.x * 0.07f, 0.04f, 0f), new Vector3(0.07f, 0.07f, 0.02f), MatKey.LightOn, 0.005f, null, false);
                Kit.BoxVisual(lights.transform, "Red", new Vector3(size.x * 0.07f, 0.04f, 0f), new Vector3(0.07f, 0.07f, 0.02f), MatKey.LightRed, 0.005f, null, false);
                BoxCol(go, new Vector3(0, size.y * 0.5f, 0), new Vector3(size.x + 0.1f, size.y, size.z + 0.1f));
                return go;
            }

            /// <summary>Spinning gear (SlowSpin). Axis = rot*up. No collider.</summary>
            public static GameObject Gear(Transform parent, Vector3 pos, Quaternion rot, float radius = 0.8f, float speed = 25f)
            {
                var go = Prop("Gear", parent, pos, rot, EnvMeshes.Key("decor_gear", Kit.Theme, radius), p =>
                    p.Gear(Vector3.zero, radius, Mathf.Max(0.08f, radius * 0.18f), Mathf.Clamp(Mathf.RoundToInt(radius * 12f), 8, 24), P(MatKey.MetalLight)));
                var s = go.AddComponent<SlowSpin>(); s.DegreesPerSecond = speed;
                return go;
            }

            /// <summary>Conveyor-look strip from-&gt;to (rubber belt with chevrons, rollers, side rails). Box collider (top at from.y + 0.2).</summary>
            public static GameObject ConveyorStrip(Transform parent, Vector3 from, Vector3 to, float width = 1.4f)
            {
                var d = to - from; d.y = 0f; float len = Mathf.Max(0.5f, d.magnitude);
                var go = Prop("ConveyorStrip", parent, from, Quaternion.LookRotation(d.sqrMagnitude > 1e-6f ? d / len : Vector3.forward), EnvMeshes.Key("decor_conveyor", Kit.Theme, len, width), p =>
                {
                    var belt = Hx(0x2A2A2E); var rail = P(MatKey.MetalLight); var chev = Hx(0xFFC93C);
                    p.Box(new Vector3(0, 0.15f, len * 0.5f), new Vector3(width, 0.1f, len), belt, 0.02f);
                    p.Box(new Vector3(width * 0.5f + 0.05f, 0.14f, len * 0.5f), new Vector3(0.1f, 0.28f, len), rail, 0.02f);
                    p.Box(new Vector3(-width * 0.5f - 0.05f, 0.14f, len * 0.5f), new Vector3(0.1f, 0.28f, len), rail, 0.02f);
                    int n = Mathf.FloorToInt(len / 0.9f);
                    for (int i = 0; i < n; i++)
                    {
                        float z = 0.45f + i * 0.9f;
                        var ql = Quaternion.Euler(0, -35f, 0); var qr = Quaternion.Euler(0, 35f, 0);
                        p.Box(new Vector3(-width * 0.18f, 0.205f, z), new Vector3(0.08f, 0.012f, width * 0.45f), chev, 0f, ql);
                        p.Box(new Vector3(width * 0.18f, 0.205f, z), new Vector3(0.08f, 0.012f, width * 0.45f), chev, 0f, qr);
                    }
                    for (int i = 0; i <= Mathf.FloorToInt(len / 1.2f); i++)
                        p.Cyl(new Vector3(0, 0.07f, 0.3f + i * 1.2f), 0.07f, width + 0.1f, rail, 10, Quaternion.Euler(0, 0, 90));
                });
                BoxCol(go, new Vector3(0, 0.1f, len * 0.5f), new Vector3(width + 0.2f, 0.2f, len));
                return go;
            }

            /// <summary>Decor barrel (capsule collider). colorIndex 0 blue, 1 hazard yellow, 2 rust red.</summary>
            public static GameObject Barrel(Transform parent, Vector3 pos, int colorIndex = 0)
            {
                Color[] cs = { Hx(0x3C7DD9), Hx(0xF2B124), Hx(0xA4583A) };
                var c = cs[Mathf.Abs(colorIndex) % cs.Length];
                var go = Prop("Barrel", parent, pos, Quaternion.Euler(0, colorIndex * 61f, 0), EnvMeshes.Key("decor_barrel", c), p =>
                {
                    p.Cyl(new Vector3(0, 0.45f, 0), 0.3f, 0.9f, c, 16, null, true);
                    foreach (float y in new[] { 0.12f, 0.45f, 0.78f }) p.Cyl(new Vector3(0, y, 0), 0.315f, 0.04f, Palettes.Shade(c, 0.7f), 16, null, true);
                    p.Cyl(new Vector3(0.12f, 0.905f, 0.05f), 0.05f, 0.02f, Palettes.Shade(c, 0.6f), 8, null, false);
                });
                var col = go.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.45f, 0); col.radius = 0.31f; col.height = 0.9f;
                return go;
            }

            /// <summary>Static stack of wooden crates (box colliders per crate).</summary>
            public static GameObject CrateStack(Transform parent, Vector3 pos, Quaternion rot, int count = 3, int seed = 1)
            {
                var root = Kit.Make("CrateStack", parent, pos, rot);
                var rng = new System.Random(seed);
                count = Mathf.Clamp(count, 1, 8);
                var key = EnvMeshes.Key("decor_crate", 1f);
                var mesh = EnvMeshes.Cached(key, p =>
                {
                    var wood = Hx(0xC08A55); var edge = Hx(0x8A5A30);
                    p.Box(Vector3.zero, Vector3.one * 0.94f, wood, 0.03f);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            p.Box(new Vector3(sx * 0.46f, 0, sz * 0.46f), new Vector3(0.1f, 1f, 0.1f), edge, 0.01f);
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        p.Box(new Vector3(0, sy * 0.46f, 0.46f), new Vector3(1f, 0.1f, 0.1f), edge, 0.01f);
                        p.Box(new Vector3(0, sy * 0.46f, -0.46f), new Vector3(1f, 0.1f, 0.1f), edge, 0.01f);
                        p.Box(new Vector3(0.46f, sy * 0.46f, 0), new Vector3(0.1f, 0.1f, 1f), edge, 0.01f);
                        p.Box(new Vector3(-0.46f, sy * 0.46f, 0), new Vector3(0.1f, 0.1f, 1f), edge, 0.01f);
                    }
                    p.Box(new Vector3(0, 0, 0.475f), new Vector3(0.1f, 1.25f, 0.04f), edge, 0f, Quaternion.Euler(0, 0, 45));
                });
                int bottom = Mathf.Min(count, 3);
                for (int i = 0; i < count; i++)
                {
                    bool top = i >= bottom;
                    float x = top ? (i - bottom - (count - bottom - 1) * 0.5f) * 1.02f : (i - (bottom - 1) * 0.5f) * 1.04f;
                    var lp = new Vector3(x, top ? 1.5f : 0.5f, (float)rng.NextDouble() * 0.1f);
                    var lr = Quaternion.Euler(0, (float)rng.NextDouble() * 14f - 7f, 0);
                    var c = EnvMeshes.Render(root.transform, "Crate" + i, mesh, White, lp, lr);
                    c.gameObject.AddComponent<BoxCollider>().size = Vector3.one;
                }
                return root;
            }

            /// <summary>Hanging chain from 'top' down 'length' meters with a hook. No collider.</summary>
            public static GameObject Chain(Transform parent, Vector3 top, float length = 3f)
                => Prop("Chain", parent, top, Quaternion.identity, EnvMeshes.Key("decor_chain", Kit.Theme, length), p =>
                {
                    var c = P(MatKey.MetalDark);
                    int links = Mathf.Max(2, Mathf.RoundToInt(length / 0.14f));
                    for (int i = 0; i < links; i++)
                        p.Torus(new Vector3(0, -0.07f - i * 0.14f, 0), 0.06f, 0.016f, c, Quaternion.Euler(90f, i % 2 == 0 ? 0f : 90f, 0f), 10, 5);
                    float hy = -length - 0.1f;
                    p.Torus(new Vector3(0.06f, hy, 0), 0.1f, 0.025f, P(MatKey.HazardYellow), Quaternion.Euler(90f, 0f, 0f), 12, 6);
                }, false);

            /// <summary>Steam stack (chimney) puffing steam at runtime. No collider.</summary>
            public static GameObject SteamStack(Transform parent, Vector3 pos, float height = 3f)
            {
                var go = Prop("SteamStack", parent, pos, Quaternion.identity, EnvMeshes.Key("decor_steamstack", Kit.Theme, height), p =>
                {
                    var m = P(MatKey.MetalRust); var d = P(MatKey.MetalDark);
                    p.Cyl(new Vector3(0, height * 0.5f, 0), 0.32f, height, m, 14, null, true);
                    p.Cyl(new Vector3(0, height, 0), 0.4f, 0.16f, d, 14, null, false);
                    for (int i = 1; i < 4; i++) p.Cyl(new Vector3(0, height * i / 4f, 0), 0.35f, 0.06f, d, 14, null, true);
                });
                var s = go.AddComponent<SteamPuffer>(); s.LocalPoint = new Vector3(0, height + 0.15f, 0); s.Size = 1.1f;
                return go;
            }

            // ============================================================ LAB
            /// <summary>Specimen tank: metal base/cap, glass tube, glowing green liquid, a floating rubber duck. Capsule collider.</summary>
            public static GameObject Tank(Transform parent, Vector3 pos, float height = 2.2f, float radius = 0.55f)
            {
                var go = Prop("Tank", parent, pos, Quaternion.identity, EnvMeshes.Key("decor_tank", Kit.Theme, height, radius), p =>
                {
                    var d = P(MatKey.MetalDark); var l = P(MatKey.MetalLight);
                    p.Cyl(new Vector3(0, 0.15f, 0), radius + 0.12f, 0.3f, d, 18, null, false);
                    p.Cyl(new Vector3(0, height - 0.12f, 0), radius + 0.1f, 0.24f, d, 18, null, false);
                    p.Cyl(new Vector3(0, 0.34f, 0), radius + 0.05f, 0.08f, l, 18, null, false);
                    p.Rod(new Vector3(radius + 0.1f, height - 0.05f, 0), new Vector3(radius + 0.6f, height + 0.4f, 0), 0.05f, l, 8);
                    // rubber duck floating near the top of the liquid
                    var duck = Hx(0xFFD23C);
                    p.Sphere(new Vector3(0.05f, height * 0.78f, 0), 0.11f, duck, new Vector3(1.2f, 0.8f, 1f), 10, 8);
                    p.Sphere(new Vector3(0.14f, height * 0.78f + 0.11f, 0), 0.07f, duck, null, 10, 8);
                    p.Cone(new Vector3(0.21f, height * 0.78f + 0.1f, 0), 0.03f, 0.06f, Hx(0xFF8A1F), 8, Quaternion.Euler(0, 0, -90));
                });
                float liquidH = height * 0.82f - 0.38f;
                EnvMeshes.Render(go.transform, "Liquid", ProcMesh.Cached(EnvMeshes.Key("decor_tank_liquid", radius, liquidH), () => ProcMesh.Cylinder(radius * 0.94f, liquidH, 18, true)),
                    Kit.Mat(MatKey.Acid), new Vector3(0, 0.38f + liquidH * 0.5f, 0), null, false);
                EnvMeshes.Render(go.transform, "Glass", ProcMesh.Cached(EnvMeshes.Key("decor_tank_glass", radius, height), () => ProcMesh.Cylinder(radius, height - 0.5f, 18, true)),
                    Kit.Mat(MatKey.Glass), new Vector3(0, height * 0.5f, 0), null, false);
                var c = go.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, height * 0.5f, 0); c.radius = radius + 0.1f; c.height = height;
                return go;
            }

            /// <summary>Lab desk with two glowing monitors, keyboard and a mug. Box collider.</summary>
            public static GameObject DeskWithMonitors(Transform parent, Vector3 pos, Quaternion rot)
            {
                var go = Prop("Desk", parent, pos, rot, EnvMeshes.Key("decor_desk", Kit.Theme), p =>
                {
                    var top = P(MatKey.MetalLight); var leg = P(MatKey.MetalDark); var dark = Hx(0x22252B);
                    p.Box(new Vector3(0, 0.74f, 0), new Vector3(1.6f, 0.05f, 0.75f), top, 0.01f);
                    foreach (float x in new[] { -0.75f, 0.75f }) foreach (float z in new[] { -0.32f, 0.32f }) p.Box(new Vector3(x, 0.36f, z), new Vector3(0.05f, 0.72f, 0.05f), leg, 0f);
                    foreach (float x in new[] { -0.38f, 0.38f })
                    {
                        p.Box(new Vector3(x, 1.1f, 0.15f), new Vector3(0.62f, 0.4f, 0.04f), dark, 0.01f);
                        p.Box(new Vector3(x, 0.84f, 0.17f), new Vector3(0.05f, 0.16f, 0.05f), dark, 0f);
                        p.Box(new Vector3(x, 0.775f, 0.17f), new Vector3(0.2f, 0.02f, 0.14f), dark, 0f);
                    }
                    p.Box(new Vector3(0, 0.78f, -0.12f), new Vector3(0.5f, 0.02f, 0.16f), dark, 0.005f);
                    p.Cyl(new Vector3(0.62f, 0.82f, -0.15f), 0.045f, 0.1f, Hx(0xE8394B), 10, null, false);
                });
                foreach (float x in new[] { -0.38f, 0.38f })
                    EnvMeshes.Render(go.transform, "Screen", ProcMesh.CachedBox(new Vector3(0.56f, 0.34f, 0.01f), 0f), Kit.Mat(MatKey.Screen), new Vector3(x, 1.1f, 0.125f), null, false);
                BoxCol(go, new Vector3(0, 0.38f, 0), new Vector3(1.6f, 0.76f, 0.75f));
                return go;
            }

            /// <summary>Broken tiles scattered on the floor (top of floor = pos.y). Visual only.</summary>
            public static GameObject BrokenTiles(Transform parent, Vector3 center, Vector2 size, int seed = 3)
                => Prop("BrokenTiles", parent, center, Quaternion.identity, EnvMeshes.Key("decor_brokentiles", Kit.Theme, size, (float)seed), p =>
                {
                    var rng = new System.Random(seed);
                    var tile = P(MatKey.FloorAlt); var hole = Palettes.Shade(P(MatKey.MetalDark), 0.6f);
                    int n = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * 1.2f), 4, 40);
                    for (int i = 0; i < n; i++)
                    {
                        var lp = new Vector3(((float)rng.NextDouble() - 0.5f) * size.x, 0.012f, ((float)rng.NextDouble() - 0.5f) * size.y);
                        bool isHole = rng.NextDouble() < 0.3;
                        float s = 0.2f + (float)rng.NextDouble() * 0.45f;
                        var q = Quaternion.Euler(isHole ? 0f : (float)rng.NextDouble() * 10f - 5f, (float)rng.NextDouble() * 360f, isHole ? 0f : (float)rng.NextDouble() * 10f - 5f);
                        if (isHole) p.Box(lp - Vector3.up * 0.008f, new Vector3(s * 1.4f, 0.006f, s * 1.4f), hole, 0f, q);
                        else p.Box(lp + Vector3.up * 0.02f, new Vector3(s, 0.04f, s * 0.8f), Palettes.Shade(tile, 0.9f + (float)rng.NextDouble() * 0.2f), 0.01f, q);
                    }
                }, false);

            /// <summary>Sagging cable between two points (catenary-ish). Visual only.</summary>
            public static GameObject Cable(Transform parent, Vector3 from, Vector3 to, float sag = 0.6f, int colorIndex = 0)
            {
                Color[] cs = { Hx(0x22252B), Hx(0xE8394B), Hx(0x3C7DD9), Hx(0xFFC93C) };
                var rel = to - from;
                var key = "decor_cable_" + EnvMeshes.Hash(rel, new Vector3(sag, colorIndex, 0));
                return Prop("Cable", parent, from, Quaternion.identity, key, p =>
                {
                    const int seg = 12; var prev = Vector3.zero; var c = cs[Mathf.Abs(colorIndex) % cs.Length];
                    for (int i = 1; i <= seg; i++)
                    {
                        float t = i / (float)seg;
                        var pt = rel * t + Vector3.down * (sag * 4f * t * (1f - t));
                        p.Rod(prev, pt, 0.025f, c, 6);
                        prev = pt;
                    }
                }, false);
            }

            // ============================================================ RESEARCH
            /// <summary>Floating, slowly spinning geometric shape (0 ico-ball, 1 cube, 2 octahedron, 3 torus). No collider.</summary>
            public static GameObject FloatingShape(Transform parent, Vector3 pos, float size = 1f, int shape = 0)
            {
                shape = Mathf.Abs(shape) % 4;
                var go = Prop("FloatingShape", parent, pos, Quaternion.Euler(20f * shape, 30f * shape, 10f), EnvMeshes.Key("decor_shape", Kit.Theme, size, (float)shape), p =>
                {
                    var a = P(MatKey.WallAlt); var b = P(MatKey.Trim);
                    switch (shape)
                    {
                        case 0: p.Sphere(Vector3.zero, size * 0.5f, a, null, 8, 5, false); break;
                        case 1: p.Box(Vector3.zero, Vector3.one * size * 0.75f, a, size * 0.06f); break;
                        case 2: p.Cone(Vector3.zero, size * 0.5f, size * 0.6f, a, 4); p.Cone(Vector3.zero, size * 0.5f, size * 0.6f, b, 4, Quaternion.Euler(180, 0, 0)); break;
                        default: p.Torus(Vector3.zero, size * 0.4f, size * 0.12f, b, null, 18, 8); break;
                    }
                });
                var s = go.AddComponent<SlowSpin>(); s.Axis = new Vector3(0.3f, 1f, 0.2f).normalized; s.DegreesPerSecond = 12f + shape * 5f; s.BobAmplitude = 0.25f * size; s.Phase = shape * 0.37f;
                return go;
            }

            /// <summary>Holographic info panel (emitter base + hologram quad + glowing text), readable by a viewer looking along
            /// rot*forward (mounted-piece convention). pos = emitter base. No collider.</summary>
            public static GameObject HoloPanel(Transform parent, Vector3 pos, Quaternion rot, Vector2 size, string text)
            {
                var root = Kit.Make("HoloPanel", parent, pos, ViewToFace(rot));
                var t = root.transform;
                Kit.BoxVisual(t, "Emitter", new Vector3(0, 0.05f, 0), new Vector3(size.x * 0.6f, 0.1f, 0.25f), MatKey.MetalDark, 0.02f);
                Kit.BoxVisual(t, "EmitterGlow", new Vector3(0, 0.105f, 0), new Vector3(size.x * 0.5f, 0.012f, 0.12f), MatKey.TrimGlow, 0f, null, false);
                var holo = EnvMeshes.Render(t, "Holo", ProcMesh.CachedBox(new Vector3(size.x, size.y, 0.02f), 0f), Kit.Mat(MatKey.Hologram), new Vector3(0, 0.2f + size.y * 0.5f, 0), null, false);
                var tmp = AddText(t, text ?? "", new Vector3(0, 0.2f + size.y * 0.5f, 0.02f), new Vector2(size.x * 0.88f, size.y * 0.8f), UIColors.Hex(0xE6D6FF), ShiftersFonts.Label, size.y * 0.25f);
                tmp.fontStyle = TMPro.FontStyles.Bold;
                return root;
            }

            /// <summary>The giant brain jar (base, glass dome, pink lumpy brain, "IQ 9000" plate). Capsule collider.
            /// The plate is a direct child "Sign_Info" on the base's local +Z side and its readable face points OUTWARD
            /// (local +Z; world +Z with the default rotation). To aim the plate at the path use the plateFacing overload.</summary>
            public static GameObject BrainJar(Transform parent, Vector3 pos, float scale = 1f)
            {
                var go = Prop("BrainJar", parent, pos, Quaternion.identity, EnvMeshes.Key("decor_brainjar", Kit.Theme, scale), p =>
                {
                    var d = P(MatKey.MetalDark); var pink = Hx(0xF29BB5); var pink2 = Hx(0xE07C9C);
                    p.Cyl(new Vector3(0, 0.3f * scale, 0), 0.8f * scale, 0.6f * scale, d, 20, null, false);
                    p.Cyl(new Vector3(0, 0.62f * scale, 0), 0.7f * scale, 0.06f * scale, P(MatKey.Trim), 20, null, false);
                    var rng = new System.Random(42);
                    for (int i = 0; i < 22; i++)
                    {
                        var dir = new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.3f) * 0.8f, (float)rng.NextDouble() - 0.5f).normalized;
                        p.Sphere(new Vector3(0, 1.25f * scale, 0) + Vector3.Scale(dir, new Vector3(0.38f, 0.26f, 0.32f)) * scale, 0.2f * scale, i % 2 == 0 ? pink : pink2, null, 8, 6);
                    }
                    p.Box(new Vector3(0, 1.25f * scale, 0), new Vector3(0.03f, 0.5f, 0.75f) * scale, pink2, 0.01f);
                });
                EnvMeshes.Render(go.transform, "Dome", ProcMesh.Cached(EnvMeshes.Key("decor_jar_dome", scale), () => ProcMesh.Sphere(0.72f * scale, 20, 12, true)),
                    Kit.Mat(MatKey.Glass), new Vector3(0, 1.3f * scale, 0), null, false);
                // face space: the plate's readable face points along the jar's local +Z, away from the base (outward)
                SignFace(go.transform, new Vector3(0, 0.3f * scale, 0.81f * scale), Quaternion.identity, "IQ 9000", new Vector2(0.8f, 0.25f) * scale, SignStyle.Info);
                var c = go.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, 1f * scale, 0); c.radius = 0.8f * scale; c.height = 2.1f * scale;
                return go;
            }

            /// <summary>BrainJar turned about the vertical axis so its "IQ 9000" plate faces the world direction
            /// <paramref name="plateFacing"/> (XZ; e.g. towards the player's path). The jar is otherwise symmetric.</summary>
            public static GameObject BrainJar(Transform parent, Vector3 pos, float scale, Vector3 plateFacing)
            {
                var go = BrainJar(parent, pos, scale);
                plateFacing.y = 0f;
                if (plateFacing.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.LookRotation(plateFacing.normalized, Vector3.up);
                return go;
            }

            /// <summary>Tall pillar with a glowing grid (theme trim glow rings). Box collider.</summary>
            public static GameObject GridPillar(Transform parent, Vector3 basePos, float height = 4f)
            {
                var go = Pillar(parent, basePos, height, 0.8f);
                go.name = "GridPillar";
                var rings = EnvMeshes.Cached(EnvMeshes.Key("decor_gridpillar_rings", height), p =>
                {
                    for (float y = 0.8f; y < height - 0.4f; y += 0.8f) p.Box(new Vector3(0, y, 0), new Vector3(0.86f, 0.04f, 0.86f), Color.white, 0f);
                });
                EnvMeshes.Render(go.transform, "GridGlow", rings, Kit.Mat(MatKey.TrimGlow), Vector3.zero, null, false);
                return go;
            }

            // ============================================================ CHAOS
            /// <summary>Electrical panel with an open door that spits sparks at runtime (wall mounted; mounted-piece convention: rot*forward points into the wall).</summary>
            public static GameObject SparkingPanel(Transform parent, Vector3 pos, Quaternion rot)
            {
                var go = Prop("SparkingPanel", parent, pos, ViewToFace(rot), EnvMeshes.Key("decor_sparkpanel", Kit.Theme), p =>
                {
                    var box = P(MatKey.MetalLight); var dark = Hx(0x15161A);
                    p.Box(new Vector3(0, 0, 0.12f), new Vector3(0.7f, 0.9f, 0.24f), box, 0.02f);
                    p.Box(new Vector3(0, 0, 0.245f), new Vector3(0.6f, 0.8f, 0.01f), dark, 0f);
                    p.Box(new Vector3(-0.45f, 0, 0.42f), new Vector3(0.6f, 0.86f, 0.03f), box, 0.01f, Quaternion.Euler(0, -65f, 0));
                    Color[] wires = { Hx(0xE8394B), Hx(0xFFC93C), Hx(0x3C7DD9) };
                    for (int i = 0; i < 3; i++) p.Rod(new Vector3(-0.15f + i * 0.15f, 0.3f, 0.25f), new Vector3(-0.1f + i * 0.12f, -0.2f, 0.3f), 0.02f, wires[i], 6);
                });
                SignFace(go.transform, new Vector3(0.0f, 0.62f, 0.13f), Quaternion.identity, "HIGH VOLTAGE", new Vector2(0.7f, 0.22f), SignStyle.Warning);
                var se = go.AddComponent<SparkEmitter>(); se.LocalPoint = new Vector3(0, -0.15f, 0.3f); se.LocalNormal = new Vector3(0.2f, 0.4f, 1f);
                return go;
            }

            /// <summary>Broken I-beam lying/tilted (box collider).</summary>
            public static GameObject BrokenBeam(Transform parent, Vector3 pos, Quaternion rot, float length = 4f)
            {
                var go = Prop("BrokenBeam", parent, pos, rot, EnvMeshes.Key("decor_beam", Kit.Theme, length), p =>
                {
                    var c = P(MatKey.MetalRust);
                    p.Box(new Vector3(0, 0.03f, length * 0.5f), new Vector3(0.4f, 0.06f, length), c, 0.01f);
                    p.Box(new Vector3(0, 0.37f, length * 0.5f), new Vector3(0.4f, 0.06f, length), c, 0.01f);
                    p.Box(new Vector3(0, 0.2f, length * 0.5f), new Vector3(0.06f, 0.3f, length), c, 0.005f);
                    for (int i = 0; i < 4; i++) p.Box(new Vector3(((i % 2) - 0.5f) * 0.2f, 0.2f, length + 0.08f * i - 0.1f), new Vector3(0.1f, 0.35f, 0.12f), Palettes.Shade(c, 0.8f), 0f, Quaternion.Euler(i * 17f, i * 23f, i * 11f));
                });
                BoxCol(go, new Vector3(0, 0.2f, length * 0.5f), new Vector3(0.4f, 0.4f, length));
                return go;
            }

            /// <summary>Rotating red warning beacon (two spot lights, no shadows, marked DecorLight: switched off by the
            /// WebGL/Mobile light budget while the glowing dome keeps spinning). No collider.</summary>
            public static GameObject WarningBeacon(Transform parent, Vector3 pos, bool withLights = true)
            {
                var go = Prop("WarningBeacon", parent, pos, Quaternion.identity, EnvMeshes.Key("decor_beacon_base", Kit.Theme), p =>
                {
                    p.Cyl(new Vector3(0, 0.06f, 0), 0.2f, 0.12f, P(MatKey.MetalDark), 14, null, false);
                    p.Cyl(new Vector3(0, 0.14f, 0), 0.16f, 0.04f, P(MatKey.HazardYellow), 14, null, false);
                });
                var head = Kit.Make("Head", go.transform, new Vector3(0, 0.16f, 0));
                EnvMeshes.Render(head.transform, "Dome", ProcMesh.Cached("decor_beacon_dome", () => ProcMesh.Sphere(0.15f, 14, 8, true)), Kit.Mat(MatKey.LightRed), new Vector3(0, 0.08f, 0), null, false);
                if (withLights)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var lgo = Kit.Make("Spot" + i, head.transform, new Vector3(0, 0.1f, 0), Quaternion.Euler(10f, i * 180f, 0f));
                        var l = lgo.AddComponent<Light>();
                        l.type = LightType.Spot; l.color = new Color(1f, 0.15f, 0.1f); l.range = 9f; l.spotAngle = 50f; l.intensity = 6f; l.shadows = LightShadows.None;
                        DecorLight.Mark(l);
                    }
                }
                var s = head.AddComponent<SlowSpin>(); s.DegreesPerSecond = 220f;
                return go;
            }

            /// <summary>Rubble scattered around center (on the floor at center.y). Visual only.</summary>
            public static GameObject Debris(Transform parent, Vector3 center, float radius = 2f, int count = 14, int seed = 5)
                => Prop("Debris", parent, center, Quaternion.identity, EnvMeshes.Key("decor_debris", Kit.Theme, radius, (float)count, (float)seed), p =>
                {
                    var rng = new System.Random(seed);
                    var a = P(MatKey.Concrete); var b = P(MatKey.MetalRust); var c2 = P(MatKey.MetalDark);
                    for (int i = 0; i < count; i++)
                    {
                        float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                        float s = 0.12f + (float)rng.NextDouble() * 0.4f;
                        var col = i % 3 == 0 ? b : (i % 3 == 1 ? a : c2);
                        var q = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
                        p.Box(new Vector3(Mathf.Cos(ang) * r, s * 0.35f, Mathf.Sin(ang) * r), new Vector3(s, s * 0.6f, s * 0.8f), col, 0.02f, q);
                    }
                });

            // ============================================================ SCATTER
            /// <summary>Places 'count' theme-appropriate small decor props randomly inside a rectangle (center = floor top
            /// center, size = x/z extents). Deterministic per seed.</summary>
            public static GameObject Scatter(Transform parent, LevelTheme theme, Vector3 center, Vector2 size, int count = 8, int seed = 1)
            {
                var root = Kit.Make("DecorScatter", parent, center);
                var prev = Kit.Theme; Kit.Theme = theme;
                var rng = new System.Random(seed);
                for (int i = 0; i < count; i++)
                {
                    var lp = new Vector3(((float)rng.NextDouble() - 0.5f) * size.x, 0f, ((float)rng.NextDouble() - 0.5f) * size.y);
                    var rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    int k = rng.Next(4);
                    switch (theme)
                    {
                        case LevelTheme.Factory: if (k == 0) Barrel(root.transform, lp, rng.Next(3)); else if (k == 1) CrateStack(root.transform, lp, rot, 1 + rng.Next(3), rng.Next(100)); else if (k == 2) Cone(root.transform, lp); else Debris(root.transform, lp, 0.8f, 6, rng.Next(100)); break;
                        case LevelTheme.Lab: if (k == 0) BrokenTiles(root.transform, lp, new Vector2(1.5f, 1.5f), rng.Next(100)); else if (k == 1) Plant(root.transform, lp, rng.Next(3)); else if (k == 2) Barrel(root.transform, lp, 0); else Cone(root.transform, lp); break;
                        case LevelTheme.Research: FloatingShape(root.transform, lp + Vector3.up * (1.5f + (float)rng.NextDouble() * 2f), 0.5f + (float)rng.NextDouble() * 0.6f, rng.Next(4)); break;
                        case LevelTheme.Chaos: if (k < 2) Debris(root.transform, lp, 1f, 8, rng.Next(100)); else if (k == 2) Barrel(root.transform, lp, 2); else Cone(root.transform, lp); break;
                        default: if (k == 0) Cone(root.transform, lp); else if (k == 1) Plant(root.transform, lp, rng.Next(3)); else if (k == 2) Barrel(root.transform, lp, 0); else Cone(root.transform, lp); break;
                    }
                }
                Kit.Theme = prev;
                return root;
            }
        }
    }
}
