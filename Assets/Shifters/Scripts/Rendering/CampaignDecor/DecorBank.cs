// =====================================================================================
//  SHIFTERS - DecorBank.cs   (RENDERING_DECOR)
//  EnvKit.Decor.Bank: THE FIRST & ONLY BANK OF ME (CH04 lobby, CH07 vault, CH08 coin plant, O1).
//  Grey marble (MatKey.Marble), grey-immune gold (MatKey.Gold, its own renderer so the world-color grade skips it),
//  red velvet (MatKey.Velvet). Every prop = 1..4 merged meshes (+ TMP texts). Positions are local to 'parent'.
//  FRONT convention: pieces with a front/readable side take 'rot' = the VIEWER'S LOOK (like EnvKit.Sign):
//  pass EnvKit.FacingViewer(towardViewer) / WallMountFacing(wallNormal); null = viewer looks +Z (front faces -Z).
//  The returned root's transform.forward points out of the front.
// =====================================================================================
using UnityEngine;
using static Shifters.CampaignDecorUtil;

namespace Shifters
{
    public static partial class EnvKit
    {
        public static partial class Decor
        {
            public static class Bank
            {
                static Material MarbleM => Kit.Mat(MatKey.Marble);
                static Material GoldM => Kit.Mat(MatKey.Gold);
                static Material VelvetM => Kit.Mat(MatKey.Velvet);

                /// <summary>Marble column (square fluted, or round), gold bands. pos = base centre on the floor.
                /// COLLIDER: one BoxCollider width x height x width (usable as cover). Not front-facing (rot = plain rotation).</summary>
                public static GameObject MarbleColumn(Transform parent, Vector3 pos, Quaternion? rot = null, float height = 6f, float width = 1.2f, bool round = false, bool collider = true)
                {
                    height = Mathf.Max(1.5f, height); width = Mathf.Max(0.3f, width);
                    var go = Root("MarbleColumn", parent, pos, rot, false); var t = go.transform;
                    float h = height, w = width;
                    Part(t, "Marble", EnvMeshes.Key("rd_bank_col", h, w, round), p =>
                    {
                        var c = Color.white; var c2 = new Color(0.86f, 0.86f, 0.88f);
                        p.Box(new Vector3(0, 0.15f, 0), new Vector3(w + 0.3f, 0.3f, w + 0.3f), c2, 0.03f);
                        p.Box(new Vector3(0, 0.4f, 0), new Vector3(w + 0.14f, 0.2f, w + 0.14f), c, 0.03f);
                        float sh = h - 1.15f, sy = 0.5f + sh * 0.5f;
                        if (round)
                        {
                            p.Cyl(new Vector3(0, sy, 0), w * 0.42f, sh, c, 20);
                            for (int i = 0; i < 12; i++) { var q = Quaternion.Euler(0, i * 30f, 0); p.Box(new Vector3(0, sy, 0) + q * new Vector3(0, 0, w * 0.42f), new Vector3(0.05f, sh * 0.96f, 0.04f), c2, 0.01f, q); }
                        }
                        else
                        {
                            p.Box(new Vector3(0, sy, 0), new Vector3(w * 0.86f, sh, w * 0.86f), c, 0.02f);
                            for (int s = 0; s < 4; s++)
                            {
                                var q = Quaternion.Euler(0, s * 90f, 0);
                                for (int i = -1; i <= 1; i++) p.Box(new Vector3(0, sy, 0) + q * new Vector3(i * w * 0.22f, 0, w * 0.43f), new Vector3(0.07f, sh * 0.94f, 0.04f), c2, 0.01f, q);
                            }
                        }
                        p.Box(new Vector3(0, h - 0.45f, 0), new Vector3(w + 0.12f, 0.3f, w + 0.12f), c, 0.03f);
                        p.Box(new Vector3(0, h - 0.12f, 0), new Vector3(w + 0.34f, 0.24f, w + 0.34f), c2, 0.03f);
                    }, MarbleM);
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_colgold", h, w, round), p =>
                    {
                        var g = Color.white;
                        foreach (float y in new[] { 0.53f, h - 0.63f })
                        {
                            if (round) p.Torus(new Vector3(0, y, 0), w * 0.43f, 0.045f, g, null, 20, 6);
                            else p.Box(new Vector3(0, y, 0), new Vector3(w * 0.9f, 0.07f, w * 0.9f), g, 0.01f);
                        }
                        p.Box(new Vector3(0, h - 0.27f, 0), new Vector3(w + 0.2f, 0.05f, w + 0.2f), g, 0.01f);
                    }, GoldM, false);
                    if (collider) BoxCol(go, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, w));
                    return go;
                }

                /// <summary>Teller counter: marble body with panels, dark wood top, gold rail, optional teller glass with gold bars
                /// and window placards. pos = centre of the footprint on the floor; FRONT (customer side) per the front convention,
                /// the counter runs along the viewer's left-right. glassHeight &lt;= height = no glass.
                /// COLLIDERS: body BoxCollider (length x height x depth) and, with glass, a glass BoxCollider up to glassHeight.
                /// placard: text for every window (null = "TELLER 1..n"; at most 6 placards).</summary>
                public static GameObject TellerCounter(Transform parent, Vector3 pos, Quaternion? rot = null, float length = 6f, float height = 2.5f, float depth = 1.4f, float glassHeight = 4.5f, float windowSpacing = 2f, bool collider = true, string placard = null)
                {
                    length = Mathf.Max(1f, length); height = Mathf.Max(0.8f, height); depth = Mathf.Max(0.4f, depth); windowSpacing = Mathf.Max(0.8f, windowSpacing);
                    var go = Root("TellerCounter", parent, pos, rot, true); var t = go.transform;
                    float L = length, H = height, Dp = depth, ws = windowSpacing;
                    int windows = Mathf.Max(1, Mathf.FloorToInt(L / ws));
                    bool glass = glassHeight > H + 0.1f;
                    float gh = glass ? glassHeight - H : 0f;
                    Part(t, "Marble", EnvMeshes.Key("rd_bank_teller", L, H, Dp, ws), p =>
                    {
                        var c = Color.white; var c2 = new Color(0.8f, 0.8f, 0.83f);
                        p.Box(new Vector3(0, (H - 0.1f) * 0.5f, 0), new Vector3(L, H - 0.1f, Dp), c, 0.02f);
                        float pw = L / windows;
                        for (int i = 0; i < windows; i++)
                        {
                            float x = -L * 0.5f + pw * (i + 0.5f);
                            p.Box(new Vector3(x, H * 0.5f, Dp * 0.5f + 0.02f), new Vector3(pw * 0.72f, H * 0.55f, 0.04f), c2, 0.02f);
                            p.Box(new Vector3(x, H * 0.5f, Dp * 0.5f + 0.045f), new Vector3(pw * 0.6f, H * 0.42f, 0.02f), c, 0.01f);
                        }
                    }, MarbleM);
                    Part(t, "Mesh", EnvMeshes.Key("rd_bank_tellerwood", L, H, Dp), p =>
                    {
                        var wood = Hx(0x4A2E22); var kick = Hx(0x2A2A30);
                        p.Box(new Vector3(0, H - 0.05f, 0), new Vector3(L + 0.08f, 0.1f, Dp + 0.12f), wood, 0.02f);
                        p.Box(new Vector3(0, 0.08f, Dp * 0.5f + 0.01f), new Vector3(L, 0.16f, 0.06f), kick, 0.01f);
                    }, White);
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_tellergold", L, H, Dp, ws, gh), p =>
                    {
                        var g = Color.white;
                        p.Box(new Vector3(0, H - 0.16f, Dp * 0.5f + 0.03f), new Vector3(L, 0.06f, 0.04f), g, 0.01f);
                        p.Box(new Vector3(0, 0.2f, Dp * 0.5f + 0.045f), new Vector3(L, 0.04f, 0.03f), g, 0.01f);
                        if (gh > 0f)
                        {
                            p.Box(new Vector3(0, H + gh - 0.04f, 0), new Vector3(L, 0.08f, 0.1f), g, 0.01f);
                            p.Box(new Vector3(0, H + 0.03f, 0), new Vector3(L, 0.06f, 0.1f), g, 0.01f);
                            float pw = L / windows;
                            for (int i = 0; i <= windows; i++) p.Box(new Vector3(-L * 0.5f + pw * i, H + gh * 0.5f, 0), new Vector3(0.08f, gh, 0.1f), g, 0.01f);
                            for (int i = 0; i < windows; i++)
                            {
                                float x = -L * 0.5f + pw * (i + 0.5f);
                                for (int b = -2; b <= 2; b++) p.Box(new Vector3(x + b * pw * 0.09f, H + 0.85f, 0.06f), new Vector3(0.025f, 1.1f, 0.025f), g, 0f);
                                p.Torus(new Vector3(x, H + 0.85f, 0.065f), pw * 0.22f, 0.025f, g, Quaternion.Euler(90, 0, 0), 18, 4);
                                p.Box(new Vector3(x, H + Mathf.Min(gh - 0.4f, 1.85f), 0.06f), new Vector3(Mathf.Min(1.1f, pw * 0.6f), 0.28f, 0.03f), g, 0.01f);
                            }
                        }
                    }, GoldM, false);
                    if (glass)
                    {
                        EnvMeshes.Render(t, "Glass", ProcMesh.CachedBox(new Vector3(L, gh, 0.04f), 0f), Kit.Mat(MatKey.Glass), new Vector3(0, H + gh * 0.5f, 0), null, false);
                        float pw = L / windows;
                        for (int i = 0; i < Mathf.Min(6, windows); i++)
                        {
                            float x = L * 0.5f - pw * (i + 0.5f);
                            Text(t, "Placard" + i, placard ?? ("TELLER " + (i + 1)), new Vector3(x, H + Mathf.Min(gh - 0.4f, 1.85f), 0.08f), new Vector2(Mathf.Min(1.0f, pw * 0.55f), 0.22f), Hx(0x2A1E0E), 0.16f);
                        }
                    }
                    if (collider)
                    {
                        BoxCol(go, new Vector3(0, H * 0.5f, 0), new Vector3(L, H, Dp));
                        if (glass) BoxCol(go, new Vector3(0, H + gh * 0.5f, 0), new Vector3(L, gh, 0.12f));
                    }
                    return go;
                }

                /// <summary>Two gold stanchions with a sagging red velvet rope (rope along the viewer's left-right), optional little
                /// sign plate on the rope ("PLEASE WAIT ALONE"). pos = centre on the floor; front convention for the sign.
                /// NO COLLIDER (decor).</summary>
                public static GameObject VelvetRope(Transform parent, Vector3 pos, Quaternion? rot = null, float length = 2.5f, string sign = null)
                {
                    length = Mathf.Max(0.4f, length);
                    var go = Root("VelvetRope", parent, pos, rot, true); var t = go.transform;
                    float L = length;
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_ropeposts", L), p => { Stanchion(p, new Vector3(-L * 0.5f, 0, 0)); Stanchion(p, new Vector3(L * 0.5f, 0, 0)); }, GoldM, true);
                    Part(t, "Rope", EnvMeshes.Key("rd_bank_rope", L), p => Sag(p, new Vector3(-L * 0.5f + 0.06f, 0.88f, 0), new Vector3(L * 0.5f - 0.06f, 0.88f, 0), 0.16f, 0.028f, Color.white, 10), VelvetM, false);
                    if (!string.IsNullOrEmpty(sign)) RopeSign(t, new Vector3(0, 0.6f, 0.04f), sign);
                    return go;
                }

                /// <summary>A run of stanchions from 'from' to 'to' (local to parent, floor points) every ~spacing m with velvet
                /// between them: 2 renderers for the whole run (use it for queue mazes). Optional sign on the middle span
                /// (readable from the side the run's right-hand normal points to; set signTowards to pick the side).
                /// NO COLLIDER.</summary>
                public static GameObject VelvetRopeLine(Transform parent, Vector3 from, Vector3 to, float spacing = 2.5f, string sign = null, Vector3? signTowards = null)
                {
                    var d = to - from; d.y = 0f; float len = Mathf.Max(0.4f, d.magnitude);
                    var dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
                    var go = Kit.Make("VelvetRopeLine", parent, from, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, -90f, 0));
                    var t = go.transform;
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(0.5f, spacing)));
                    float seg = len / n;
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_ropelineposts", len, n), p => { for (int i = 0; i <= n; i++) Stanchion(p, new Vector3(i * seg, 0, 0)); }, GoldM, true);
                    Part(t, "Rope", EnvMeshes.Key("rd_bank_ropeline", len, n), p => { for (int i = 0; i < n; i++) Sag(p, new Vector3(i * seg + 0.06f, 0.88f, 0), new Vector3((i + 1) * seg - 0.06f, 0.88f, 0), 0.16f, 0.028f, Color.white, 8); }, VelvetM, false);
                    if (!string.IsNullOrEmpty(sign))
                    {
                        // local +Z is the run's left normal; flip when the reader is on the other side
                        bool flip = false;
                        if (signTowards.HasValue) { var tw = t.InverseTransformDirection(parent != null ? parent.TransformDirection(signTowards.Value) : signTowards.Value); flip = tw.z < 0f; }
                        var holder = Kit.Make("SignHolder", t, new Vector3((n / 2) * seg + seg * 0.5f, 0, 0), flip ? Quaternion.Euler(0, 180f, 0) : Quaternion.identity);
                        RopeSign(holder.transform, new Vector3(0, 0.6f, 0.04f), sign);
                    }
                    return go;
                }

                static void Stanchion(EnvParts p, Vector3 b)
                {
                    var g = Color.white;
                    p.Cyl(b + new Vector3(0, 0.03f, 0), 0.2f, 0.06f, g, 16);
                    p.Cyl(b + new Vector3(0, 0.08f, 0), 0.12f, 0.05f, g, 14);
                    p.Cyl(b + new Vector3(0, 0.5f, 0), 0.035f, 0.8f, g, 10);
                    p.Sphere(b + new Vector3(0, 0.95f, 0), 0.065f, g, null, 10, 6);
                }

                static void RopeSign(Transform t, Vector3 at, string text)
                {
                    Part(t, "SignPlate", "rd_bank_ropesign", p =>
                    {
                        p.Box(Vector3.zero, new Vector3(0.62f, 0.26f, 0.02f), Hx(0x2A1A14), 0.01f);
                        p.Box(new Vector3(0, 0, 0.011f), new Vector3(0.56f, 0.2f, 0.004f), Hx(0xF3E6C8), 0f);
                    }, White, false, at);
                    Text(t, "SignText", text, at + new Vector3(0, 0, 0.016f), new Vector2(0.52f, 0.17f), Hx(0x5A1020), 0.08f, false);
                }

                /// <summary>Round vault door (decor) in a riveted steel frame with a gold wheel and a nameplate. pos = door centre;
                /// front convention (the wall is behind it). Children: "Hinge" (at the door's RIGHT edge as seen by the viewer;
                /// rotate its localRotation about Y by -openDegrees to swing the door out), "Hinge/Door", "Hinge/Door/Wheel"
                /// (spin about its local Z). label null = "THE FEDERAL COLOR RESERVE", "" = none.
                /// COLLIDER: only with collider=true, a BoxCollider on "Hinge/Door" (moves with it).</summary>
                public static GameObject VaultDoorDecor(Transform parent, Vector3 pos, Quaternion? rot = null, float diameter = 8f, float openDegrees = 0f, bool collider = false, string label = null)
                {
                    diameter = Mathf.Max(1f, diameter);
                    var go = Root("VaultDoorDecor", parent, pos, rot, true); var t = go.transform;
                    float R = diameter * 0.5f;
                    Part(t, "Frame", EnvMeshes.Key("rd_bank_vaultframe", R), p =>
                    {
                        var dark = Hx(0x34373D); var steel = Hx(0x8C9098);
                        p.Box(new Vector3(0, 0, -0.35f), new Vector3(diameter + 1.8f, diameter + 1.8f, 0.2f), dark, 0.05f);
                        p.Torus(new Vector3(0, 0, -0.05f), R + 0.32f, 0.38f, steel, Quaternion.Euler(90, 0, 0), 36, 10);
                        for (int i = 0; i < 20; i++) { float a = i * Mathf.PI * 2f / 20f; p.Sphere(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (R + 0.32f) + new Vector3(0, 0, 0.32f), 0.09f, dark, null, 8, 5); }
                        // three lock-lamp sockets on top of the frame (levels may place PuzzleKit lamps in front of them)
                        for (int i = -1; i <= 1; i++) p.Cyl(new Vector3(i * R * 0.45f, R + 0.55f, 0.05f), 0.28f, 0.2f, dark, 16, Quaternion.Euler(90, 0, 0));
                    }, White);
                    var hinge = Kit.Make("Hinge", t, new Vector3(-R, 0, 0.18f), Quaternion.Euler(0, -openDegrees, 0));
                    var door = Kit.Make("Door", hinge.transform, new Vector3(R, 0, 0));
                    Part(door.transform, "Mesh", EnvMeshes.Key("rd_bank_vaultdoor", R), p =>
                    {
                        var steel = Hx(0xA9AEB6); var steel2 = Hx(0x7E838B); var dark = Hx(0x3A3D43);
                        p.Cyl(Vector3.zero, R * 0.97f, 0.5f, steel, 40, Quaternion.Euler(90, 0, 0));
                        p.Torus(new Vector3(0, 0, 0.26f), R * 0.78f, 0.07f, steel2, Quaternion.Euler(90, 0, 0), 32, 6);
                        p.Torus(new Vector3(0, 0, 0.26f), R * 0.5f, 0.06f, steel2, Quaternion.Euler(90, 0, 0), 28, 6);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                            p.Rod(d * R * 0.55f + new Vector3(0, 0, 0.27f), d * R * 0.75f + new Vector3(0, 0, 0.27f), 0.09f, steel2, 8);
                            p.Box(d * R * 0.9f + new Vector3(0, 0, 0.26f), new Vector3(0.3f, 0.3f, 0.06f), dark, 0.02f, Quaternion.Euler(0, 0, a * Mathf.Rad2Deg));
                        }
                        p.Box(new Vector3(-R * 0.97f, R * 0.45f, 0.1f), new Vector3(0.4f, R * 0.3f, 0.6f), dark, 0.04f);
                        p.Box(new Vector3(-R * 0.97f, -R * 0.45f, 0.1f), new Vector3(0.4f, R * 0.3f, 0.6f), dark, 0.04f);
                    }, White);
                    var wheel = Kit.Make("Wheel", door.transform, new Vector3(0, 0, 0.3f));
                    Part(wheel.transform, "Gold", EnvMeshes.Key("rd_bank_vaultwheel", R), p =>
                    {
                        var g = Color.white; float wr = R * 0.24f;
                        p.Torus(new Vector3(0, 0, 0.12f), wr, 0.07f * Mathf.Max(1f, R / 4f), g, Quaternion.Euler(90, 0, 0), 24, 6);
                        p.Cyl(new Vector3(0, 0, 0.06f), wr * 0.25f, 0.2f, g, 16, Quaternion.Euler(90, 0, 0));
                        for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); p.Rod(new Vector3(0, 0, 0.12f), d * wr + new Vector3(0, 0, 0.12f), 0.04f * Mathf.Max(1f, R / 4f), g, 6); p.Sphere(d * (wr + 0.15f) + new Vector3(0, 0, 0.12f), 0.08f * Mathf.Max(1f, R / 4f), g, null, 8, 5); }
                    }, GoldM);
                    if (collider) { var bc = door.AddComponent<BoxCollider>(); bc.size = new Vector3(R * 1.94f, R * 1.94f, 0.5f); }
                    string lab = label ?? "THE FEDERAL COLOR RESERVE";
                    if (lab.Length > 0)
                    {
                        Part(t, "Plate", EnvMeshes.Key("rd_bank_vaultplate", R), p => p.Box(Vector3.zero, new Vector3(Mathf.Min(diameter, 6f), 0.5f, 0.06f), Color.white, 0.02f), GoldM, false, new Vector3(0, -R - 0.62f, 0.1f));
                        Text(t, "Label", lab, new Vector3(0, -R - 0.62f, 0.14f), new Vector2(Mathf.Min(diameter, 6f) * 0.92f, 0.38f), Hx(0x2A1E0E), 0.3f);
                    }
                    return go;
                }

                /// <summary>Mound of gold coins with loose coins and a few stacks. pos = centre on the floor. NO COLLIDER.</summary>
                public static GameObject CoinPile(Transform parent, Vector3 pos, Quaternion? rot = null, float radius = 1f, float height = 0.5f, int seed = 0)
                {
                    radius = Mathf.Max(0.2f, radius); height = Mathf.Max(0.05f, height);
                    var go = Root("CoinPile", parent, pos, rot, false);
                    float r = radius, h = height; int sd = seed;
                    Part(go.transform, "Gold", EnvMeshes.Key("rd_bank_coins", r, h, sd), p =>
                    {
                        var g = Color.white; var g2 = new Color(0.86f, 0.84f, 0.8f);
                        var rng = new System.Random(1000 + sd);
                        float F() => (float)rng.NextDouble();
                        p.Sphere(Vector3.zero, r, g2, new Vector3(1f, h / r, 1f), 18, 8);
                        float cr = Mathf.Clamp(r * 0.09f, 0.04f, 0.09f);
                        int n = Mathf.Clamp(Mathf.RoundToInt(r * r * 40f), 12, 90);
                        for (int i = 0; i < n; i++)
                        {
                            float a = F() * Mathf.PI * 2f, d = Mathf.Sqrt(F()) * r * 1.1f;
                            float y = Mathf.Max(0.005f, h * Mathf.Sqrt(Mathf.Max(0f, 1f - (d * d) / (r * r))) - cr * 0.3f);
                            p.Cyl(new Vector3(Mathf.Cos(a) * d, y, Mathf.Sin(a) * d), cr, cr * 0.22f, i % 3 == 0 ? g2 : g, 10, Quaternion.Euler(F() * 50f - 25f, F() * 360f, F() * 50f - 25f), false);
                        }
                        int stacks = 2 + sd % 3;
                        for (int s = 0; s < stacks; s++)
                        {
                            float a = s * 2.4f + sd, d = r * (1.1f + 0.15f * s);
                            int k = 4 + (s * 3 + sd) % 6;
                            for (int j = 0; j < k; j++) p.Cyl(new Vector3(Mathf.Cos(a) * d, cr * 0.13f + j * cr * 0.24f, Mathf.Sin(a) * d), cr, cr * 0.22f, j % 2 == 0 ? g : g2, 10, Quaternion.Euler(0, j * 17f, 0), false);
                        }
                    }, GoldM, false);
                    return go;
                }

                /// <summary>Wall of safe-deposit boxes (steel doors, gold keyholes, a few hanging open) with a gold title plate.
                /// pos = bottom centre against the wall; front convention. COLLIDER: one BoxCollider (whole wall, 0.5 m deep,
                /// behind the front) unless collider=false. title null = "SAFE DEPOSIT BOXES (ALL OF THEM: ME)", "" = none.</summary>
                public static GameObject SafeDepositWall(Transform parent, Vector3 pos, Quaternion? rot = null, int columns = 8, int rows = 6, float cell = 0.45f, bool collider = true, string title = null)
                {
                    columns = Mathf.Clamp(columns, 1, 40); rows = Mathf.Clamp(rows, 1, 20); cell = Mathf.Max(0.15f, cell);
                    var go = Root("SafeDepositWall", parent, pos, rot, true); var t = go.transform;
                    int cols = columns, rws = rows; float c = cell;
                    float W = cols * c + 0.2f, H = rws * c + 0.2f;
                    bool Open(int i, int j) => ((i * 7 + j * 13) % 17) == 3;
                    Part(t, "Mesh", EnvMeshes.Key("rd_bank_safewall", cols, rws, c), p =>
                    {
                        var body = Hx(0x5C6068); var d1 = Hx(0xA4A9B0); var d2 = Hx(0x979CA4); var hole = Hx(0x15161A);
                        p.Box(new Vector3(0, H * 0.5f, -0.25f), new Vector3(W, H, 0.5f), body, 0.03f);
                        for (int i = 0; i < cols; i++)
                            for (int j = 0; j < rws; j++)
                            {
                                var ctr = new Vector3(-W * 0.5f + 0.1f + c * (i + 0.5f), 0.1f + c * (j + 0.5f), 0f);
                                if (Open(i, j))
                                {
                                    p.Box(ctr + new Vector3(0, 0, -0.01f), new Vector3(c - 0.06f, c - 0.06f, 0.02f), hole, 0f);
                                    var q = Quaternion.Euler(0, -70f, 0);
                                    p.Box(ctr + new Vector3(-c * 0.5f, 0, 0) + q * new Vector3(c * 0.47f, 0, 0.02f), new Vector3(c - 0.05f, c - 0.05f, 0.03f), d2, 0.006f, q);
                                }
                                else p.Box(ctr + new Vector3(0, 0, 0.015f), new Vector3(c - 0.04f, c - 0.04f, 0.03f), (i + j) % 2 == 0 ? d1 : d2, 0.006f);
                            }
                    }, White);
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_safewallgold", cols, rws, c), p =>
                    {
                        var g = Color.white;
                        p.Box(new Vector3(0, H + 0.04f, -0.2f), new Vector3(W + 0.1f, 0.08f, 0.6f), g, 0.01f);
                        for (int i = 0; i < cols; i++)
                            for (int j = 0; j < rws; j++)
                            {
                                if (Open(i, j)) continue;
                                var ctr = new Vector3(-W * 0.5f + 0.1f + c * (i + 0.5f), 0.1f + c * (j + 0.5f), 0.03f);
                                p.Cyl(ctr + new Vector3(c * 0.28f, 0, 0.01f), c * 0.06f, 0.03f, g, 8, Quaternion.Euler(90, 0, 0), false);
                                p.Box(ctr + new Vector3(-c * 0.1f, c * 0.3f, 0.005f), new Vector3(c * 0.4f, c * 0.1f, 0.01f), g, 0f);
                            }
                    }, GoldM, false);
                    string ti = title ?? "SAFE DEPOSIT BOXES (ALL OF THEM: ME)";
                    if (ti.Length > 0)
                    {
                        float pw = Mathf.Min(W, 4f);
                        Part(t, "Plate", EnvMeshes.Key("rd_bank_safeplate", pw), p => p.Box(Vector3.zero, new Vector3(pw, 0.34f, 0.04f), Color.white, 0.01f), GoldM, false, new Vector3(0, H + 0.3f, 0.02f));
                        Text(t, "Title", ti, new Vector3(0, H + 0.3f, 0.045f), new Vector2(pw * 0.94f, 0.26f), Hx(0x2A1E0E), 0.2f);
                    }
                    if (collider) BoxCol(go, new Vector3(0, H * 0.5f, -0.25f), new Vector3(W, H, 0.5f));
                    return go;
                }

                /// <summary>Gold chandelier with crystal drops and glowing candle bulbs. pos = the ceiling attachment point; it hangs
                /// 'drop' m of chain below it. withLight adds ONE shadowless warm point light (DecorLight-marked, counts against the
                /// §21 light budget: prefer emissive-only, the default). NO COLLIDER.</summary>
                public static GameObject Chandelier(Transform parent, Vector3 pos, Quaternion? rot = null, float radius = 1.2f, float drop = 1.2f, int tiers = 2, bool withLight = false)
                {
                    radius = Mathf.Max(0.3f, radius); drop = Mathf.Max(0f, drop); tiers = Mathf.Clamp(tiers, 1, 3);
                    var go = Root("Chandelier", parent, pos, rot, false); var t = go.transform;
                    float r = radius, dr = drop; int tr = tiers;
                    float Ty(int i) => -dr - 0.25f - i * r * 0.45f;
                    float Tr(int i) => r * (1f - i * 0.38f);
                    int Cn(int i) => Mathf.Max(5, Mathf.RoundToInt(Tr(i) * 8f));
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_chand", r, dr, tr), p =>
                    {
                        var g = Color.white;
                        if (dr > 0.05f) LinkChain(p, Vector3.zero, new Vector3(0, -dr, 0), 0.09f, g);
                        p.Cyl(new Vector3(0, -0.04f, 0), 0.18f, 0.08f, g, 14);
                        float bottom = Ty(tr - 1) - 0.3f;
                        p.Cyl(new Vector3(0, (-dr + bottom) * 0.5f, 0), 0.05f, -dr - bottom, g, 10);
                        p.Sphere(new Vector3(0, -dr - 0.1f, 0), 0.12f, g, null, 10, 6);
                        p.Sphere(new Vector3(0, bottom, 0), 0.14f, g, new Vector3(1, 1.4f, 1), 10, 6);
                        for (int i = 0; i < tr; i++)
                        {
                            float y = Ty(i), rr = Tr(i); int n = Cn(i);
                            p.Torus(new Vector3(0, y, 0), rr, 0.035f, g, null, 28, 5);
                            for (int k = 0; k < n; k++)
                            {
                                float a = (k + i * 0.5f) * Mathf.PI * 2f / n; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                                if (k % 2 == 0) p.Rod(new Vector3(0, y + 0.25f, 0), d * rr + new Vector3(0, y, 0), 0.02f, g, 5);
                                p.Cyl(d * rr + new Vector3(0, y + 0.05f, 0), 0.06f, 0.06f, g, 8);
                            }
                        }
                    }, GoldM, false);
                    Part(t, "Crystals", EnvMeshes.Key("rd_bank_chandcrys", r, dr, tr), p =>
                    {
                        for (int i = 0; i < tr; i++)
                        {
                            float y = Ty(i), rr = Tr(i); int n = Cn(i) * 2;
                            for (int k = 0; k < n; k++)
                            {
                                float a = k * Mathf.PI * 2f / n; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                                p.Sphere(d * rr + new Vector3(0, y - 0.14f - (k % 2) * 0.06f, 0), 0.045f, Color.white, new Vector3(1, 2f, 1), 6, 4, false);
                            }
                        }
                    }, Kit.Mat(MatKey.Glass), false);
                    Part(t, "Bulbs", EnvMeshes.Key("rd_bank_chandbulb", r, dr, tr), p =>
                    {
                        for (int i = 0; i < tr; i++)
                        {
                            float y = Ty(i), rr = Tr(i); int n = Cn(i);
                            for (int k = 0; k < n; k++) { float a = (k + i * 0.5f) * Mathf.PI * 2f / n; p.Sphere(new Vector3(Mathf.Cos(a) * rr, y + 0.15f, Mathf.Sin(a) * rr), 0.055f, Color.white, new Vector3(1, 1.5f, 1), 8, 5); }
                        }
                    }, ImmuneGlow(Hx(0xFFD98A), 2.4f), false);
                    if (withLight) DecorPoint(t, "Light", new Vector3(0, Ty(0) - 0.3f, 0), Hx(0xFFD7A0), r * 7f, 1.6f);
                    return go;
                }

                /// <summary>Bank pen on a chain in its desk holder (the pen is on a chain. Of course it is). pos = the desk/counter
                /// top; the pen lies towards the viewer's right. scale 3+ makes a joke-sized one. NO COLLIDER.</summary>
                public static GameObject PenOnAChain(Transform parent, Vector3 pos, Quaternion? rot = null, float scale = 1f)
                {
                    scale = Mathf.Max(0.2f, scale);
                    var go = Root("PenOnAChain", parent, pos, rot, true); var t = go.transform; float s = scale;
                    var penA = new Vector3(-0.06f, 0.012f, 0.03f) * s; var penB = new Vector3(-0.2f, 0.012f, 0.06f) * s;
                    Part(t, "Mesh", EnvMeshes.Key("rd_bank_pen", s), p =>
                    {
                        var dark = Hx(0x26282E); var blue = Hx(0x2F5FBF); var tip = Hx(0xC9CCD2);
                        p.Cyl(new Vector3(0.06f, 0.012f, 0) * s, 0.045f * s, 0.024f * s, dark, 14);
                        p.Cyl(new Vector3(0.06f, 0.045f, 0) * s, 0.014f * s, 0.05f * s, dark, 8);
                        p.Rod(penA, penB, 0.009f * s, blue, 8);
                        p.Cone(penB, 0.009f * s, 0.025f * s, tip, 8, Quaternion.FromToRotation(Vector3.up, (penB - penA).normalized));
                    }, White, false);
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_penchain", s), p => Sag(p, new Vector3(0.06f, 0.07f, 0) * s, penA, 0.025f * s, 0.003f * s, Color.white, 8, 4), GoldM, false);
                    return go;
                }

                /// <summary>Tall glass canister of the world's stored color: steel caps, gold rings, emissive GREY-IMMUNE liquid (child "Liquid",
                /// scaled in Y by the fill; see SetCanisterFill) and a gold label plate (child "Label" TMP) facing the viewer.
                /// pos = base centre on the floor; front convention for the label. COLLIDER: one CapsuleCollider (radius x height)
                /// unless collider=false.</summary>
                public static GameObject Canister(Transform parent, Vector3 pos, Quaternion? rot, Color color, string label = null, float height = 6f, float radius = 0.9f, float fill = 0.85f, bool collider = true)
                {
                    height = Mathf.Max(1f, height); radius = Mathf.Max(0.15f, radius);
                    var go = Root("Canister", parent, pos, rot, true); var t = go.transform;
                    float H = height, r = radius, glassH = H - 0.9f;
                    Part(t, "Mesh", EnvMeshes.Key("rd_bank_canister", H, r), p =>
                    {
                        var steel = Hx(0x60656D); var dark = Hx(0x2C2E33);
                        p.Cyl(new Vector3(0, 0.25f, 0), r + 0.12f, 0.5f, steel, 24);
                        p.Cyl(new Vector3(0, H - 0.2f, 0), r + 0.1f, 0.4f, steel, 24);
                        p.Cyl(new Vector3(0, H + 0.08f, 0), r * 0.3f, 0.16f, dark, 14);
                        p.Rod(new Vector3(0, H + 0.1f, 0), new Vector3(0, H + 0.6f, -r * 0.5f), r * 0.1f, dark, 8);
                        p.Cyl(new Vector3(0, 0.35f, r + 0.13f), 0.1f, 0.04f, Hx(0xE8E6DA), 12, Quaternion.Euler(90, 0, 0), false);
                        for (int i = 0; i < 4; i++) { var q = Quaternion.Euler(0, 45f + i * 90f, 0); p.Box(q * new Vector3(0, H * 0.5f, r + 0.02f), new Vector3(0.08f, glassH, 0.05f), dark, 0.01f, q); }
                    }, White);
                    Part(t, "Gold", EnvMeshes.Key("rd_bank_canistergold", H, r), p =>
                    {
                        var g = Color.white;
                        p.Torus(new Vector3(0, 0.5f, 0), r + 0.06f, 0.05f, g, null, 24, 5);
                        p.Torus(new Vector3(0, H - 0.4f, 0), r + 0.06f, 0.05f, g, null, 24, 5);
                        p.Box(new Vector3(0, 1.15f, r + 0.08f), new Vector3(Mathf.Min(1.4f, r * 1.5f), 0.4f, 0.04f), g, 0.01f);
                    }, GoldM, false);
                    var liquid = Kit.Make("Liquid", t, new Vector3(0, 0.5f, 0));
                    var lr = Part(liquid.transform, "Mesh", EnvMeshes.Key("rd_bank_canliquid", glassH, r), p =>
                    {
                        var c = Color.white; var c2 = new Color(1.15f, 1.15f, 1.15f);
                        p.Cyl(new Vector3(0, glassH * 0.5f, 0), r * 0.88f, glassH, c, 22);
                        for (int i = 0; i < 9; i++) p.Sphere(new Vector3(Mathf.Sin(i * 2.1f) * r * 0.6f, glassH * (0.1f + 0.09f * i), Mathf.Cos(i * 2.1f) * r * 0.6f), r * 0.07f, c2, null, 6, 4);
                    }, ImmuneGlow(new Color(color.r, color.g, color.b, 1f), 0.9f, 0.6f), false);
                    liquid.transform.localScale = new Vector3(1f, Mathf.Clamp01(fill), 1f);
                    lr.gameObject.SetActive(fill > 0.001f);
                    EnvMeshes.Render(t, "Glass", ProcMesh.Cached(EnvMeshes.Key("rd_bank_canglass", glassH, r), () => ProcMesh.Cylinder(r, glassH, 24, true)), MaterialLibrary.Transparent(new Color(0.85f, 0.95f, 1f, 0.12f)), new Vector3(0, 0.5f + glassH * 0.5f, 0), null, false);
                    if (!string.IsNullOrEmpty(label)) Text(t, "Label", label, new Vector3(0, 1.15f, r + 0.105f), new Vector2(Mathf.Min(1.3f, r * 1.4f), 0.32f), Hx(0x2A1E0E), 0.22f);
                    if (collider) { var cc = go.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, H * 0.5f, 0); cc.radius = r + 0.1f; cc.height = H; }
                    return go;
                }

                /// <summary>Sets a Canister's liquid level (0..1; 0 hides it). Null-safe.</summary>
                public static void SetCanisterFill(GameObject canister, float fill01)
                {
                    if (canister == null) return;
                    var l = canister.transform.Find("Liquid"); if (l == null) return;
                    l.localScale = new Vector3(1f, Mathf.Clamp01(fill01), 1f);
                    if (l.childCount > 0) l.GetChild(0).gameObject.SetActive(fill01 > 0.001f);
                }
            }
        }
    }
}
