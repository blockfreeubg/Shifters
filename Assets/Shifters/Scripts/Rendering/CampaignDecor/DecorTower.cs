// =====================================================================================
//  SHIFTERS - DecorTower.cs   (RENDERING_DECOR)
//  EnvKit.Decor.Tower: ME TOWER and THE HALL OF ONE (CH13 Hostile Takeover, CH14 All In Favor?, O7/O8).
//  Black glass (MatKey.BlackGlass) + grey-immune gold (MatKey.Gold) + velvet. FRONT convention as in DecorBank.cs.
// =====================================================================================
using TMPro;
using UnityEngine;
using static Shifters.CampaignDecorUtil;

namespace Shifters
{
    public static partial class EnvKit
    {
        public static partial class Decor
        {
            public static class Tower
            {
                static Material GoldM => Kit.Mat(MatKey.Gold);
                static Material GlassM => Kit.Mat(MatKey.BlackGlass);

                /// <summary>Black glass wall panel with a gold mullion grid and a black base. pos = bottom centre; front convention.
                /// COLLIDER: only with collider=true (width x height x 0.12).</summary>
                public static GameObject BlackGlassPanel(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 3f, float height = 4f, int columns = 3, int rows = 4, bool collider = false)
                {
                    width = Mathf.Max(0.3f, width); height = Mathf.Max(0.3f, height); columns = Mathf.Clamp(columns, 1, 30); rows = Mathf.Clamp(rows, 1, 30);
                    var go = Root("BlackGlassPanel", parent, pos, rot, true); var t = go.transform;
                    float W = width, H = height; int c = columns, r = rows;
                    EnvMeshes.Render(t, "Glass", ProcMesh.CachedBox(new Vector3(W, H, 0.08f), 0f), GlassM, new Vector3(0, H * 0.5f, 0), null, true);
                    Part(t, "Gold", EnvMeshes.Key("rd_tower_glassgrid", W, H, c, r), p =>
                    {
                        var g = Color.white;
                        for (int i = 0; i <= c; i++) p.Box(new Vector3(-W * 0.5f + W * i / c, H * 0.5f, 0.05f), new Vector3(i == 0 || i == c ? 0.07f : 0.04f, H, 0.04f), g, 0f);
                        for (int j = 0; j <= r; j++) p.Box(new Vector3(0, H * j / r, 0.05f), new Vector3(W, j == 0 || j == r ? 0.07f : 0.03f, 0.04f), g, 0f);
                    }, GoldM, false);
                    if (collider) BoxCol(go, new Vector3(0, H * 0.5f, 0), new Vector3(W, H, 0.12f));
                    return go;
                }

                /// <summary>ME-TV propaganda screen: black frame with gold trim, a deep-red emissive screen, a gold crown icon, the
                /// slogan (child "Text", TMP; change it freely) and a "ME-TV LIVE" corner bug. pos = screen centre (stand=true:
                /// adds a pole down to pos.y - height/2 - standHeight, i.e. put pos standHeight + height/2 above the floor).
                /// Front convention. NO COLLIDER.</summary>
                public static GameObject PropagandaScreen(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 4f, float height = 2.4f, string text = null, bool stand = false, float standHeight = 2f)
                {
                    width = Mathf.Max(0.8f, width); height = Mathf.Max(0.5f, height);
                    var go = Root("PropagandaScreen", parent, pos, rot, true); var t = go.transform;
                    float W = width, H = height, sh = stand ? Mathf.Max(0.2f, standHeight) : 0f;
                    Part(t, "Mesh", EnvMeshes.Key("rd_tower_pscreen", W, H, sh), p =>
                    {
                        var blk = Hx(0x141519);
                        p.Box(new Vector3(0, 0, -0.06f), new Vector3(W + 0.3f, H + 0.3f, 0.16f), blk, 0.04f);
                        if (sh > 0f)
                        {
                            p.Box(new Vector3(0, -H * 0.5f - sh * 0.5f, -0.1f), new Vector3(0.22f, sh, 0.22f), blk, 0.02f);
                            p.Box(new Vector3(0, -H * 0.5f - sh + 0.05f, -0.1f), new Vector3(1.4f, 0.1f, 0.8f), blk, 0.02f);
                        }
                    }, White);
                    Part(t, "Gold", EnvMeshes.Key("rd_tower_pscreengold", W, H), p =>
                    {
                        var g = Color.white;
                        p.Box(new Vector3(0, H * 0.5f + 0.1f, 0.03f), new Vector3(W + 0.2f, 0.05f, 0.03f), g, 0f);
                        p.Box(new Vector3(0, -H * 0.5f - 0.1f, 0.03f), new Vector3(W + 0.2f, 0.05f, 0.03f), g, 0f);
                        p.Box(new Vector3(-W * 0.5f - 0.1f, 0, 0.03f), new Vector3(0.05f, H + 0.2f, 0.03f), g, 0f);
                        p.Box(new Vector3(W * 0.5f + 0.1f, 0, 0.03f), new Vector3(0.05f, H + 0.2f, 0.03f), g, 0f);
                        float cs = H * 0.16f; var cc = new Vector3(0, H * 0.27f, 0.04f);
                        p.Box(cc, new Vector3(cs * 2.2f, cs * 0.45f, 0.02f), g, 0f);
                        for (int i = -2; i <= 2; i++) p.Cone(cc + new Vector3(i * cs * 0.5f, cs * 0.2f, 0), cs * 0.2f, cs * (i == 0 ? 0.9f : 0.6f), g, 4, Quaternion.Euler(0, 45, 0));
                    }, GoldM, false);
                    EnvMeshes.Render(t, "Screen", ProcMesh.CachedBox(new Vector3(W, H, 0.02f), 0f), Glow(0x5C0814, 0.75f, 0.75f), new Vector3(0, 0, 0.02f), null, false);
                    Text(t, "Text", text ?? "APPROVAL RATING: 100%\n(MARGIN OF ERROR: ME)", new Vector3(0, -H * 0.12f, 0.04f), new Vector2(W * 0.88f, H * 0.5f), Hx(0xFFE7A8), H * 0.14f);
                    Text(t, "Bug", "ME-TV LIVE", new Vector3(W * 0.5f - W * 0.13f, H * 0.5f - H * 0.08f, 0.04f), new Vector2(W * 0.22f, H * 0.1f), Hx(0xFFFFFF), H * 0.06f);
                    return go;
                }

                /// <summary>The dais of THE HALL OF ONE: concentric black-marble steps with gold nosings, a velvet runner up the front,
                /// and (throne=true) the golden THRONE OF ONE on top (child "Throne": its own renderers + BoxCollider, so a story
                /// beat can hide/fold it). pos = centre of the bottom step on the floor; front convention (the throne faces the
                /// viewer). COLLIDERS: one BoxCollider per step (walkable, stepHeight each) + the throne's.</summary>
                public static GameObject ThroneDais(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 6f, float depth = 5f, int steps = 3, float stepHeight = 0.3f, bool throne = true, bool collider = true)
                {
                    steps = Mathf.Clamp(steps, 1, 8); stepHeight = Mathf.Clamp(stepHeight, 0.1f, 0.5f);
                    width = Mathf.Max(steps * 0.9f + 1.5f, width); depth = Mathf.Max(steps * 0.9f + 1.5f, depth);
                    var go = Root("ThroneDais", parent, pos, rot, true); var t = go.transform;
                    float W = width, Dp = depth, sh = stepHeight; int n = steps; const float inset = 0.45f;
                    Vector3 StepC(int i) => new Vector3(0, sh * i + sh * 0.5f, -i * inset * 0.5f);
                    Vector3 StepS(int i) => new Vector3(W - i * inset * 2f, sh, Dp - i * inset * 1.5f);
                    Part(t, "Mesh", EnvMeshes.Key("rd_tower_dais", W, Dp, n, sh), p =>
                    {
                        var m1 = Hx(0x1C1D23); var m2 = Hx(0x26272F);
                        for (int i = 0; i < n; i++) p.Box(StepC(i), StepS(i), i % 2 == 0 ? m1 : m2, 0.02f);
                    }, White);
                    Part(t, "Gold", EnvMeshes.Key("rd_tower_daisgold", W, Dp, n, sh), p =>
                    {
                        var g = Color.white;
                        for (int i = 0; i < n; i++) { var c = StepC(i); var s = StepS(i); p.Box(new Vector3(0, c.y + sh * 0.5f - 0.02f, c.z + s.z * 0.5f), new Vector3(s.x, 0.04f, 0.04f), g, 0f); }
                    }, GoldM, false);
                    Part(t, "Runner", EnvMeshes.Key("rd_tower_daisrunner", W, Dp, n, sh), p =>
                    {
                        for (int i = 0; i < n; i++)
                        {
                            var c = StepC(i); var s = StepS(i); float front = c.z + s.z * 0.5f, back = i + 1 < n ? StepC(i + 1).z + StepS(i + 1).z * 0.5f : front - 1.2f;
                            p.Box(new Vector3(0, c.y + sh * 0.5f + 0.008f, (front + back) * 0.5f), new Vector3(1.4f, 0.016f, front - back), Color.white, 0f);
                            p.Box(new Vector3(0, c.y, front + 0.006f), new Vector3(1.4f, sh, 0.012f), Color.white, 0f);
                        }
                    }, Kit.Mat(MatKey.Velvet), false);
                    if (collider) for (int i = 0; i < n; i++) BoxCol(go, StepC(i), StepS(i));
                    if (throne)
                    {
                        var top = StepC(n - 1); var ts = StepS(n - 1);
                        var th = Kit.Make("Throne", t, new Vector3(0, sh * n, top.z - ts.z * 0.5f + 0.9f));
                        Part(th.transform, "Gold", "rd_tower_throne", p =>
                        {
                            var g = Color.white; var g2 = new Color(0.85f, 0.82f, 0.75f);
                            p.Box(new Vector3(0, 0.25f, 0), new Vector3(1.3f, 0.5f, 1.0f), g2, 0.05f);
                            p.Box(new Vector3(0, 1.6f, -0.42f), new Vector3(1.3f, 2.2f, 0.2f), g, 0.06f);
                            for (int i = -2; i <= 2; i++) { p.Cone(new Vector3(i * 0.28f, 2.7f, -0.42f), 0.12f, i == 0 ? 0.7f : 0.45f, g, 4, Quaternion.Euler(0, 45, 0)); p.Sphere(new Vector3(i * 0.28f, 2.7f + (i == 0 ? 0.74f : 0.48f), -0.42f), 0.07f, g, null, 8, 5); }
                            foreach (float x in new[] { -0.6f, 0.6f })
                            {
                                p.Box(new Vector3(x, 0.8f, 0.05f), new Vector3(0.16f, 0.12f, 0.95f), g, 0.03f);
                                p.Box(new Vector3(x, 0.62f, 0.45f), new Vector3(0.14f, 0.36f, 0.12f), g, 0.02f);
                                p.Sphere(new Vector3(x, 0.88f, 0.52f), 0.1f, g, null, 8, 5);
                            }
                            p.Torus(new Vector3(0, 1.75f, -0.31f), 0.32f, 0.05f, g2, Quaternion.Euler(90, 0, 0), 20, 5);
                        }, GoldM);
                        Part(th.transform, "Cushion", "rd_tower_thronecush", p =>
                        {
                            p.Box(new Vector3(0, 0.56f, 0.02f), new Vector3(1.04f, 0.14f, 0.86f), Color.white, 0.05f);
                            p.Box(new Vector3(0, 1.35f, -0.3f), new Vector3(0.9f, 1.2f, 0.08f), Color.white, 0.04f);
                        }, Kit.Mat(MatKey.Velvet), false);
                        Text(th.transform, "Monogram", "ME", new Vector3(0, 1.75f, -0.29f), new Vector2(0.5f, 0.4f), Hx(0xFFD45A), 0.32f);
                        if (collider) BoxCol(th, new Vector3(0, 1.1f, -0.05f), new Vector3(1.4f, 2.2f, 1.0f));
                    }
                    return go;
                }

                /// <summary>ME-TROPOLIS skyline backdrop: three layers of grey towers (only the front layer has lit windows), and
                /// (meTower=true) ME TOWER in the middle - black glass, gold crown, sign "SHIFT CHANGE IN: NEVER" (child
                /// "MeTower/Sign", TMP: the finale can set "SHIFT CHANGE: NOW"). It spreads 'width' along the viewer's left-right
                /// and ~40 m away from the viewer behind pos. pos = ground centre of the front layer; front convention. Heights
                /// scale with 'scale'. 3-5 renderers. NO COLLIDER (put it beyond the playable area / in the void).</summary>
                public static GameObject CitySkyline(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 120f, int seed = 0, bool meTower = true, float scale = 1f, string towerSign = null)
                {
                    width = Mathf.Clamp(width, 10f, 600f); scale = Mathf.Max(0.1f, scale);
                    var go = Root("CitySkyline", parent, pos, rot, true); var t = go.transform;
                    float Wd = width, sc = scale; int sd = seed; bool mt = meTower;
                    float towerHalf = mt ? 9f * sc : 0f;
                    Part(t, "Buildings", EnvMeshes.Key("rd_tower_sky", Wd, sd, sc, mt), p => SkylineLayer(p, null, Wd, sd, sc, towerHalf), White, false);
                    Part(t, "Windows", EnvMeshes.Key("rd_tower_skywin", Wd, sd, sc, mt), p => SkylineLayer(null, p, Wd, sd, sc, towerHalf), Glow(0xFFD9A0, 1.3f, 0.2f), false);
                    if (mt)
                    {
                        var tw = Kit.Make("MeTower", t, new Vector3(0, 0, -8f * sc));
                        float th = 120f * sc, tw2 = 7f * sc;
                        EnvMeshes.Render(tw.transform, "Glass", ProcMesh.Cached(EnvMeshes.Key("rd_tower_metower", sc), () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddBox(new Vector3(0, th * 0.5f, 0), new Vector3(tw2 * 2f, th, tw2 * 2f), Quaternion.identity, Color.white, 0f);
                            b.AddBox(new Vector3(0, th + 6f * sc, 0), new Vector3(tw2 * 1.4f, 12f * sc, tw2 * 1.4f), Quaternion.identity, Color.white, 0f);
                            b.AddBox(new Vector3(0, th * 0.25f, 0), new Vector3(tw2 * 2.6f, th * 0.5f, tw2 * 2.6f), Quaternion.identity, Color.white, 0f);
                            return b.ToMesh("MeTower");
                        }), GlassM, Vector3.zero, null, false);
                        Part(tw.transform, "Gold", EnvMeshes.Key("rd_tower_metowergold", sc), p =>
                        {
                            var g = Color.white;
                            for (int i = 1; i < 12; i++) { float y = th * i / 12f; float hw = y < th * 0.5f ? tw2 * 1.3f : tw2; p.Box(new Vector3(0, y, hw + 0.05f * sc), new Vector3(hw * 2f, 0.35f * sc, 0.1f * sc), g, 0f); }
                            foreach (float x in new[] { -tw2, tw2 }) p.Box(new Vector3(x, th * 0.75f, tw2 + 0.06f * sc), new Vector3(0.4f * sc, th * 0.5f, 0.12f * sc), g, 0f);
                            float cy = th + 12f * sc;
                            p.Box(new Vector3(0, cy + 1f * sc, 0), new Vector3(tw2 * 1.6f, 2f * sc, tw2 * 1.6f), g, 0f);
                            for (int i = -2; i <= 2; i++) p.Cone(new Vector3(i * tw2 * 0.35f, cy + 2f * sc, tw2 * 0.8f), 0.9f * sc, (i == 0 ? 7f : 4.5f) * sc, g, 4, Quaternion.Euler(0, 45, 0));
                            p.Box(new Vector3(0, th * 0.86f, tw2 + 0.12f * sc), new Vector3(tw2 * 1.9f, 6.5f * sc, 0.12f * sc), new Color(0.35f, 0.3f, 0.2f), 0f);
                        }, GoldM, false);
                        var sign = Text(tw.transform, "Sign", towerSign ?? "SHIFT CHANGE IN:\nNEVER", new Vector3(0, th * 0.86f, tw2 + 0.25f * sc), new Vector2(tw2 * 1.8f, 6f * sc), Hx(0xFFD45A), 2.6f * sc);
                        sign.fontStyle = FontStyles.Bold;
                    }
                    return go;
                }

                static void SkylineLayer(EnvParts body, EnvParts win, float width, int seed, float sc, float towerHalf)
                {
                    var rng = new System.Random(7700 + seed);
                    float F() => (float)rng.NextDouble();
                    for (int layer = 2; layer >= 0; layer--)
                    {
                        float z = -layer * 16f * sc, x = -width * 0.5f;
                        float shade = 0.42f + layer * 0.08f;
                        while (x < width * 0.5f)
                        {
                            float bw = (5f + F() * 7f) * sc, bh = (12f + F() * (layer == 0 ? 26f : 40f) + layer * 6f) * sc, bd = (6f + F() * 6f) * sc;
                            float cx = x + bw * 0.5f; x += bw + (0.6f + F() * 2.5f) * sc;
                            if (layer == 0 && Mathf.Abs(cx) < towerHalf + bw * 0.5f) continue;
                            var col = new Color(shade * 0.92f, shade * 0.95f, shade * 1.05f) * (0.9f + F() * 0.2f); col.a = 1f;
                            body?.Box(new Vector3(cx, bh * 0.5f, z - bd * 0.5f), new Vector3(bw, bh, bd), col, 0f);
                            if (F() < 0.3f) body?.Box(new Vector3(cx, bh + 1.2f * sc, z - bd * 0.5f), new Vector3(bw * 0.4f, 2.4f * sc, bd * 0.4f), col * 0.85f, 0f);
                            if (F() < 0.2f) body?.Rod(new Vector3(cx + bw * 0.2f, bh, z - bd * 0.5f), new Vector3(cx + bw * 0.2f, bh + 6f * sc, z - bd * 0.5f), 0.12f * sc, col * 0.7f, 4, false);
                            if (layer == 0)
                            {
                                // identical loop in both passes keeps the RNG stream in sync (bodies and windows are two meshes)
                                float wy = 2.5f * sc;
                                for (float y = 2f * sc; y < bh - 2f * sc; y += wy)
                                    for (float xx = cx - bw * 0.5f + 1.2f * sc; xx < cx + bw * 0.5f - 0.8f * sc; xx += 1.6f * sc)
                                    {
                                        bool lit = F() < 0.3f;
                                        if (lit) win?.Box(new Vector3(xx, y, z + 0.05f * sc), new Vector3(0.8f * sc, 1.1f * sc, 0.05f * sc), Color.white, 0f);
                                    }
                            }
                        }
                    }
                }

                /// <summary>The impeachment tally board: black board in a gold frame, the motion (child "Title"), AYE (green) and NAY
                /// (red) columns (AYE on the viewer's left) with big counts (children "Aye", "Nay", TMP) and lamp rows (3 AYE lamps "AyeLamp0..2", 1 NAY lamp
                /// "NayLamp0"). pos = board centre; front convention. Update with SetTally. NO COLLIDER.</summary>
                public static GameObject VoteBoard(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 5f, float height = 3f, string title = null, int aye = 0, int nay = 1)
                {
                    width = Mathf.Max(1.5f, width); height = Mathf.Max(1f, height);
                    var go = Root("VoteBoard", parent, pos, rot, true); var t = go.transform;
                    float W = width, H = height;
                    Part(t, "Mesh", EnvMeshes.Key("rd_tower_vote", W, H), p =>
                    {
                        p.Box(new Vector3(0, 0, -0.06f), new Vector3(W, H, 0.12f), Hx(0x121318), 0.03f);
                        p.Box(new Vector3(0, H * 0.5f - H * 0.17f, 0.005f), new Vector3(W * 0.94f, 0.02f, 0.01f), Hx(0x3A3B44), 0f);
                        p.Box(new Vector3(0, -H * 0.12f, 0.005f), new Vector3(0.02f, H * 0.6f, 0.01f), Hx(0x3A3B44), 0f);
                    }, White);
                    Part(t, "Gold", EnvMeshes.Key("rd_tower_votegold", W, H), p =>
                    {
                        var g = Color.white;
                        p.Box(new Vector3(0, H * 0.5f + 0.06f, -0.03f), new Vector3(W + 0.24f, 0.12f, 0.16f), g, 0.02f);
                        p.Box(new Vector3(0, -H * 0.5f - 0.06f, -0.03f), new Vector3(W + 0.24f, 0.12f, 0.16f), g, 0.02f);
                        p.Box(new Vector3(-W * 0.5f - 0.06f, 0, -0.03f), new Vector3(0.12f, H, 0.16f), g, 0.02f);
                        p.Box(new Vector3(W * 0.5f + 0.06f, 0, -0.03f), new Vector3(0.12f, H, 0.16f), g, 0.02f);
                    }, GoldM, false);
                    EnvMeshes.Render(t, "AyePanel", ProcMesh.CachedBox(new Vector3(W * 0.44f, H * 0.56f, 0.01f), 0f), Glow(0x0F3A22, 0.6f, 0.6f), new Vector3(W * 0.24f, -H * 0.12f, 0.002f), null, false);
                    EnvMeshes.Render(t, "NayPanel", ProcMesh.CachedBox(new Vector3(W * 0.44f, H * 0.56f, 0.01f), 0f), Glow(0x3A0F14, 0.6f, 0.6f), new Vector3(-W * 0.24f, -H * 0.12f, 0.002f), null, false);
                    var lampMesh = ProcMesh.Cached(EnvMeshes.Key("rd_tower_votelamp", H), () => ProcMesh.Cylinder(H * 0.045f, 0.03f, 14, false));
                    for (int i = 0; i < 3; i++) EnvMeshes.Render(t, "AyeLamp" + i, lampMesh, Kit.Mat(MatKey.LightOff), new Vector3(W * 0.24f + (i - 1) * H * 0.13f, -H * 0.34f, 0.02f), Quaternion.Euler(90, 0, 0), false);
                    EnvMeshes.Render(t, "NayLamp0", lampMesh, Kit.Mat(MatKey.LightOff), new Vector3(-W * 0.24f, -H * 0.34f, 0.02f), Quaternion.Euler(90, 0, 0), false);
                    Text(t, "Title", title ?? "THE MOTION: IMPEACH MAXIMILIAN ME", new Vector3(0, H * 0.5f - H * 0.09f, 0.02f), new Vector2(W * 0.9f, H * 0.13f), Hx(0xFFE7A8), H * 0.08f);
                    Text(t, "Aye", "", new Vector3(W * 0.24f, -H * 0.06f, 0.02f), new Vector2(W * 0.4f, H * 0.36f), Hx(0x7CFFB0), H * 0.15f);
                    Text(t, "Nay", "", new Vector3(-W * 0.24f, -H * 0.06f, 0.02f), new Vector2(W * 0.4f, H * 0.36f), Hx(0xFF7A86), H * 0.15f);
                    SetTally(go, aye, nay);
                    return go;
                }

                /// <summary>Updates a VoteBoard: texts "AYE n" / "NAY n" (+ nayNote under NAY when nay &gt; 0, default "(ME)"), and lights
                /// the first min(aye,3) AYE lamps (green-ish LightOn) and the NAY lamp when nay &gt; 0 (LightRed). Null-safe, no
                /// allocations beyond the two strings.</summary>
                public static void SetTally(GameObject board, int aye, int nay, string nayNote = "(ME)")
                {
                    if (board == null) return;
                    var t = board.transform;
                    var a = t.Find("Aye")?.GetComponent<TextMeshPro>(); if (a != null) a.text = "AYE\n" + Mathf.Max(0, aye);
                    var n = t.Find("Nay")?.GetComponent<TextMeshPro>(); if (n != null) n.text = "NAY\n" + Mathf.Max(0, nay) + (nay > 0 && !string.IsNullOrEmpty(nayNote) ? " " + nayNote : "");
                    for (int i = 0; i < 3; i++) { var r = t.Find("AyeLamp" + i)?.GetComponent<MeshRenderer>(); if (r != null) r.sharedMaterial = Kit.Mat(i < aye ? MatKey.LightOn : MatKey.LightOff); }
                    var nr = t.Find("NayLamp0")?.GetComponent<MeshRenderer>(); if (nr != null) nr.sharedMaterial = Kit.Mat(nay > 0 ? MatKey.LightRed : MatKey.LightOff);
                }
            }
        }
    }
}
