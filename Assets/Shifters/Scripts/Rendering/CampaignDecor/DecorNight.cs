// =====================================================================================
//  SHIFTERS - DecorNight.cs   (RENDERING_DECOR)
//  EnvKit.Decor.Night: the GRAVEYARD SHIFT (CH09 old SHIFTERS INC. HQ, CH10 ME-MORIAL PARK, CH11 atrium, O5 break room).
//  No Light components unless asked for (night chapters have a tight light budget, §21): glows are emissive.
//  FRONT convention as in DecorBank.cs: 'rot' = the VIEWER'S LOOK; null = viewer looks +Z. root.forward = out of the front.
// =====================================================================================
using UnityEngine;
using static Shifters.CampaignDecorUtil;

namespace Shifters
{
    public static partial class EnvKit
    {
        public static partial class Decor
        {
            public static class Night
            {
                /// <summary>Tombstone lines (CAMPAIGN §24.5) used when Gravestone gets text == null.</summary>
                public static readonly string[] TombstoneLines =
                {
                    "HERE LIES THE MARKETING TEAM.\n1998-2003.\nGREAT SYNERGY.",
                    "HERE LIES CASUAL FRIDAY.\nNOW JUST \"FRIDAY\".",
                    "HERE LIES THE OFFICE BIRTHDAY CAKE.\nEATEN BY ONE MAN.",
                    "HERE LIES THE BOWLING LEAGUE.\nA GOOD RUN. AND A GUTTER BALL.",
                    "HERE LIES \"WE\".\n\"ME\" IS FINE, THANKS.",
                    "HERE LIES THE SUGGESTION BOX.\nNO SUGGESTIONS WERE TAKEN.",
                    "HERE LIES THE GROUP CHAT.\nLAST MESSAGE: \"LOL\".",
                    "HERE LIES THE BAND.\nNOW A ONE-MAN BAND.",
                    "HERE LIES THE TUG-OF-WAR TEAM.\nBOTH SIDES LOST.",
                    "HERE LIES THE BOARD OF DIRECTORS.\nFIRED BY THE CHAIRMAN,\nWHO WAS ALSO THE BOARD.",
                    "HERE LIES THE THREE-LEGGED RACE.\nIT FELL OVER.",
                    "HERE LIES THE HIGH-FIVE.\nLEFT HANGING.",
                };

                /// <summary>Abandoned office cubicle: three 1.8 m fabric partitions (open towards the viewer), a desk with a dead CRT,
                /// a rolling chair, a team photo with the faces scribbled out, papers. variant 0..5 changes the clutter (1 = the
                /// monitor still glows a screensaver, 2 = chair knocked over). lampOn = emissive desk-lamp bulb (no Light).
                /// pos = centre of the footprint; front convention. COLLIDERS: three partition boxes + the desk box.</summary>
                public static GameObject Cubicle(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 2.4f, float depth = 2.4f, float height = 1.8f, int variant = 0, bool collider = true, bool lampOn = false)
                {
                    width = Mathf.Max(1.2f, width); depth = Mathf.Max(1.2f, depth); height = Mathf.Max(1f, height);
                    var go = Root("Cubicle", parent, pos, rot, true); var t = go.transform;
                    float W = width, Dp = depth, H = height; int v = Mathf.Abs(variant) % 6;
                    float th = 0.08f, deskY = 0.74f, deskZ = -Dp * 0.5f + 0.42f;
                    Part(t, "Mesh", EnvMeshes.Key("rd_night_cubicle", W, Dp, H, v), p =>
                    {
                        var fab = Hx(0x56606C); var fab2 = Hx(0x4C555F); var cap = Hx(0x8B9097); var desk = Hx(0x8A7A66); var dark = Hx(0x2A2C31);
                        var beige = Hx(0xB9B29E); var paper = Hx(0xE6E4DA);
                        p.Box(new Vector3(0, H * 0.5f, -Dp * 0.5f), new Vector3(W, H, th), fab, 0.01f);
                        p.Box(new Vector3(-W * 0.5f, H * 0.5f, 0), new Vector3(th, H, Dp), fab2, 0.01f);
                        p.Box(new Vector3(W * 0.5f, H * 0.5f, 0), new Vector3(th, H, Dp), fab2, 0.01f);
                        p.Box(new Vector3(0, H + 0.015f, -Dp * 0.5f), new Vector3(W + 0.04f, 0.03f, th + 0.03f), cap, 0.005f);
                        p.Box(new Vector3(-W * 0.5f, H + 0.015f, 0), new Vector3(th + 0.03f, 0.03f, Dp), cap, 0.005f);
                        p.Box(new Vector3(W * 0.5f, H + 0.015f, 0), new Vector3(th + 0.03f, 0.03f, Dp), cap, 0.005f);
                        // desk + pedestal
                        p.Box(new Vector3(0, deskY, deskZ), new Vector3(W - th * 2f - 0.04f, 0.04f, 0.7f), desk, 0.01f);
                        p.Box(new Vector3(W * 0.5f - 0.35f, deskY * 0.5f, deskZ), new Vector3(0.45f, deskY - 0.02f, 0.6f), cap, 0.01f);
                        for (int i = 0; i < 3; i++) p.Box(new Vector3(W * 0.5f - 0.35f, 0.15f + i * 0.22f, deskZ + 0.31f), new Vector3(0.18f, 0.025f, 0.02f), dark, 0f);
                        // CRT
                        p.Box(new Vector3(-0.15f, deskY + 0.24f, deskZ - 0.05f), new Vector3(0.46f, 0.4f, 0.42f), beige, 0.04f);
                        p.Box(new Vector3(-0.15f, deskY + 0.25f, deskZ + 0.165f), new Vector3(0.36f, 0.3f, 0.02f), dark, 0.01f);
                        p.Box(new Vector3(-0.1f, deskY + 0.035f, deskZ + 0.25f), new Vector3(0.45f, 0.03f, 0.16f), beige, 0.01f);
                        // team photo, faces scribbled (pinned to the back partition)
                        var ph = new Vector3(W * 0.22f, H * 0.72f, -Dp * 0.5f + th * 0.5f + 0.012f);
                        p.Box(ph, new Vector3(0.42f, 0.3f, 0.01f), paper, 0f);
                        Color[] shirts = { Hx(0xFF8A1F), Hx(0x2EC4B6), Hx(0xB07CFF) };
                        for (int i = 0; i < 3; i++)
                        {
                            p.Box(ph + new Vector3(-0.12f + i * 0.12f, -0.07f, 0.006f), new Vector3(0.08f, 0.1f, 0.004f), shirts[(i + v) % 3], 0f);
                            p.Cyl(ph + new Vector3(-0.12f + i * 0.12f, 0.03f, 0.007f), 0.035f, 0.004f, dark, 10, Quaternion.Euler(90, 0, 0), false);
                        }
                        p.Box(ph + new Vector3(0, 0.12f, 0.007f), new Vector3(0.3f, 0.03f, 0.004f), Hx(0xE8394B), 0f);
                        // sticky notes + papers
                        for (int i = 0; i < 4; i++) p.Box(new Vector3(-W * 0.3f + i * 0.09f, H * 0.6f + (i % 2) * 0.08f, -Dp * 0.5f + th * 0.5f + 0.01f), new Vector3(0.07f, 0.07f, 0.004f), Hx(0xF2D45C), 0f);
                        for (int i = 0; i < 3 + v % 3; i++) p.Box(new Vector3(-W * 0.3f + i * 0.31f, deskY + 0.025f + i * 0.002f, deskZ + 0.1f - (i % 2) * 0.15f), new Vector3(0.21f, 0.004f, 0.29f), paper, 0f, Quaternion.Euler(0, i * 23f + v * 11f, 0));
                        if (v % 2 == 1) for (int i = 0; i < 4; i++) p.Box(new Vector3(-W * 0.2f + i * 0.4f, 0.003f, Dp * 0.1f + (i % 2) * 0.3f), new Vector3(0.21f, 0.004f, 0.29f), paper, 0f, Quaternion.Euler(0, i * 41f, 0));
                        // mug "WORLD'S OKAYEST TEAM"
                        p.Cyl(new Vector3(W * 0.5f - 0.6f, deskY + 0.07f, deskZ + 0.05f), 0.045f, 0.1f, Hx(0xE8E2D0), 10);
                        // desk lamp
                        var lb = new Vector3(-W * 0.5f + 0.35f, deskY + 0.02f, deskZ - 0.15f);
                        p.Cyl(lb, 0.08f, 0.03f, dark, 10);
                        p.Rod(lb, lb + new Vector3(0.05f, 0.35f, 0.05f), 0.012f, dark, 6);
                        p.Rod(lb + new Vector3(0.05f, 0.35f, 0.05f), lb + new Vector3(0.18f, 0.42f, 0.15f), 0.012f, dark, 6);
                        p.Cone(lb + new Vector3(0.2f, 0.36f, 0.17f), 0.08f, 0.1f, Hx(0x3A6B5A), 10);
                        // chair
                        var cb = new Vector3(0.15f, 0f, deskZ + 0.75f);
                        var cq = v == 2 ? Quaternion.Euler(0, 30f, 82f) : Quaternion.Euler(0, v * 19f, 0);
                        var co = v == 2 ? new Vector3(0, 0.25f, 0) : Vector3.zero;
                        for (int i = 0; i < 5; i++) { var q = cq * Quaternion.Euler(0, i * 72f, 0); p.Box(cb + co + cq * new Vector3(0, 0.05f, 0) + q * new Vector3(0, 0, 0.16f), new Vector3(0.05f, 0.04f, 0.32f), dark, 0.01f, q); }
                        p.Cyl(cb + co + cq * new Vector3(0, 0.25f, 0), 0.03f, 0.35f, dark, 8, cq);
                        p.Box(cb + co + cq * new Vector3(0, 0.46f, 0), new Vector3(0.48f, 0.08f, 0.46f), fab2, 0.03f, cq);
                        p.Box(cb + co + cq * new Vector3(0, 0.78f, 0.22f), new Vector3(0.44f, 0.55f, 0.06f), fab2, 0.03f, cq);
                    }, White);
                    if (v % 3 == 1) EnvMeshes.Render(t, "Screen", ProcMesh.CachedBox(new Vector3(0.34f, 0.28f, 0.01f), 0f), Glow(0x5BE3C8, 1.2f), new Vector3(-0.15f, deskY + 0.25f, deskZ + 0.178f), null, false);
                    if (lampOn) EnvMeshes.Render(t, "LampBulb", ProcMesh.Cached("rd_night_cubbulb", () => ProcMesh.Sphere(0.045f, 10, 6, true)), Glow(0xFFE0A0, 2.6f), new Vector3(-W * 0.5f + 0.55f, deskY + 0.33f, deskZ + 0.02f), null, false);
                    if (collider)
                    {
                        BoxCol(go, new Vector3(0, H * 0.5f, -Dp * 0.5f), new Vector3(W, H, th));
                        BoxCol(go, new Vector3(-W * 0.5f, H * 0.5f, 0), new Vector3(th, H, Dp));
                        BoxCol(go, new Vector3(W * 0.5f, H * 0.5f, 0), new Vector3(th, H, Dp));
                        BoxCol(go, new Vector3(0, deskY * 0.5f, deskZ), new Vector3(W - th * 2f, deskY + 0.02f, 0.7f));
                    }
                    return go;
                }

                /// <summary>Row of 1..4 old photocopiers (control panels to the viewer), paper reams, an "OUT OF ORDER (SINCE 1999)"
                /// note. Child "Flash" = the emissive scan bars of all copiers (toggle it / its renderer for the copier-flash scare).
                /// pos = centre of the row on the floor; front convention. COLLIDER: one BoxCollider over the row.</summary>
                public static GameObject CopierStack(Transform parent, Vector3 pos, Quaternion? rot = null, int copiers = 1, bool collider = true)
                {
                    copiers = Mathf.Clamp(copiers, 1, 4);
                    var go = Root("CopierStack", parent, pos, rot, true); var t = go.transform;
                    int n = copiers; float cw = 1.0f, gap = 0.25f, rowW = n * cw + (n - 1) * gap;
                    float X(int i) => -rowW * 0.5f + cw * 0.5f + i * (cw + gap);
                    Part(t, "Mesh", EnvMeshes.Key("rd_night_copier", n), p =>
                    {
                        var body = Hx(0xB7B9B4); var body2 = Hx(0x8F928E); var dark = Hx(0x2A2C31); var paper = Hx(0xEDEBE2);
                        for (int i = 0; i < n; i++)
                        {
                            float x = X(i);
                            p.Box(new Vector3(x, 0.08f, 0), new Vector3(cw, 0.16f, 0.7f), dark, 0.02f);
                            p.Box(new Vector3(x, 0.55f, 0), new Vector3(cw, 0.78f, 0.7f), body, 0.04f);
                            for (int k = 0; k < 2; k++) p.Box(new Vector3(x, 0.32f + k * 0.24f, 0.355f), new Vector3(cw * 0.86f, 0.18f, 0.02f), body2, 0.01f);
                            p.Box(new Vector3(x, 0.98f, -0.02f), new Vector3(cw * 0.96f, 0.08f, 0.62f), body2, 0.02f);
                            p.Box(new Vector3(x + cw * 0.28f, 0.97f, 0.3f), new Vector3(0.34f, 0.1f, 0.16f), dark, 0.02f, Quaternion.Euler(-15, 0, 0));
                            p.Box(new Vector3(x - cw * 0.62f, 0.62f, 0), new Vector3(0.24f, 0.03f, 0.4f), body2, 0.01f, Quaternion.Euler(0, 0, -12));
                            for (int k = 0; k < 6; k++) p.Box(new Vector3(x - cw * 0.62f, 0.64f + k * 0.006f, (k % 3) * 0.01f), new Vector3(0.21f, 0.005f, 0.29f), paper, 0f, Quaternion.Euler(0, k * 7f, -12));
                            for (int k = 0; k < 3; k++) p.Box(new Vector3(x - 0.2f + k * 0.2f, 0.1f + (k % 2) * 0.09f, 0.62f), new Vector3(0.21f, 0.18f, 0.28f), Hx(0xD9D2C0), 0.01f, Quaternion.Euler(0, k * 13f, 0));
                        }
                        for (int k = 0; k < 9; k++) p.Box(new Vector3(-rowW * 0.4f + k * rowW * 0.1f, 0.003f, 0.9f + (k % 3) * 0.25f), new Vector3(0.21f, 0.004f, 0.29f), paper, 0f, Quaternion.Euler(0, k * 37f, 0));
                        p.Box(new Vector3(X(0) - 0.2f, 0.62f, 0.368f), new Vector3(0.36f, 0.2f, 0.004f), Hx(0xF2D45C), 0f, Quaternion.Euler(0, 0, 4));
                    }, White);
                    var flash = Kit.Make("Flash", t, Vector3.zero);
                    Part(flash.transform, "Mesh", EnvMeshes.Key("rd_night_copierflash", n), p => { for (int i = 0; i < n; i++) p.Box(new Vector3(X(i), 1.025f, -0.02f), new Vector3(cw * 0.8f, 0.012f, 0.5f), Color.white, 0f); }, Glow(0x8CFFB8, 2.2f), false);
                    var note = Text(t, "Note", "OUT OF ORDER\n(SINCE 1999)", new Vector3(X(0) - 0.2f, 0.62f, 0.372f), new Vector2(0.32f, 0.17f), Hx(0x2A2C31), 0.05f, false);
                    note.transform.localRotation = Quaternion.Euler(0, 180f, 4f);
                    if (collider) BoxCol(go, new Vector3(0, 0.55f, 0), new Vector3(rowW, 1.1f, 0.72f));
                    return go;
                }

                /// <summary>Office water cooler (taps to the viewer). dead = empty jug, slime ring, cobweb; else full and blue.
                /// pos = floor point; front convention. COLLIDER: one BoxCollider (0.42 x 1.45 x 0.42) unless collider=false.</summary>
                public static GameObject WaterCooler(Transform parent, Vector3 pos, Quaternion? rot = null, bool dead = true, bool collider = true)
                {
                    var go = Root("WaterCooler", parent, pos, rot, true); var t = go.transform; bool d = dead;
                    Part(t, "Mesh", EnvMeshes.Key("rd_night_cooler", d), p =>
                    {
                        var body = Hx(0xD6D4CC); var dark = Hx(0x3A3D43);
                        p.Box(new Vector3(0, 0.5f, 0), new Vector3(0.36f, 1.0f, 0.36f), body, 0.03f);
                        p.Box(new Vector3(0, 0.62f, 0.17f), new Vector3(0.26f, 0.16f, 0.06f), dark, 0.01f);
                        p.Box(new Vector3(-0.06f, 0.66f, 0.21f), new Vector3(0.04f, 0.05f, 0.05f), Hx(0x3C7DD9), 0.005f);
                        p.Box(new Vector3(0.06f, 0.66f, 0.21f), new Vector3(0.04f, 0.05f, 0.05f), Hx(0xE8394B), 0.005f);
                        p.Box(new Vector3(0, 0.52f, 0.2f), new Vector3(0.22f, 0.02f, 0.08f), dark, 0.005f);
                        p.Cyl(new Vector3(0, 1.02f, 0), 0.12f, 0.04f, dark, 12);
                        p.Cyl(new Vector3(0.22f, 0.75f, 0), 0.04f, 0.3f, Hx(0xE8E6DA), 10);
                        if (d)
                        {
                            p.Cyl(new Vector3(0, 1.06f, 0), 0.155f, 0.03f, Hx(0x6E8A4A), 16);
                            for (int i = 0; i < 6; i++) { float a = i * 0.6f; p.Rod(new Vector3(0.17f, 1.4f, -0.05f), new Vector3(0.17f + Mathf.Cos(a) * 0.25f, 1.4f - Mathf.Sin(a) * 0.25f, -0.18f), 0.003f, Hx(0xD8DDE6), 3, false); }
                            p.Box(new Vector3(0.1f, 0.003f, 0.35f), new Vector3(0.07f, 0.006f, 0.07f), Hx(0xE8E6DA), 0f, Quaternion.Euler(0, 20, 80));
                        }
                    }, White);
                    EnvMeshes.Render(t, "Jug", ProcMesh.Cached("rd_night_coolerjug", () => ProcMesh.Cylinder(0.16f, 0.42f, 16, true)), MaterialLibrary.Transparent(new Color(0.6f, 0.82f, 1f, 0.35f)), new Vector3(0, 1.26f, 0), null, false);
                    if (!d) EnvMeshes.Render(t, "Water", ProcMesh.Cached("rd_night_coolerwater", () => ProcMesh.Cylinder(0.145f, 0.34f, 14, true)), Glow(0x3FA4FF, 0.25f, 0.8f), new Vector3(0, 1.23f, 0), null, false);
                    if (collider) BoxCol(go, new Vector3(0, 0.73f, 0), new Vector3(0.42f, 1.46f, 0.42f));
                    return go;
                }

                /// <summary>Corner cobweb with a dangling spider. pos = the corner point (e.g. wall/ceiling corner); the web fans out
                /// to the viewer's RIGHT and DOWN (mirror = to the LEFT and down). Front convention (the web faces the viewer).
                /// size = radius in m. NO COLLIDER.</summary>
                public static GameObject Cobweb(Transform parent, Vector3 pos, Quaternion? rot = null, float size = 1f, bool mirror = false)
                {
                    size = Mathf.Max(0.1f, size);
                    var go = Root("Cobweb", parent, pos, rot, true); float s = size; bool m = mirror;
                    Part(go.transform, "Mesh", EnvMeshes.Key("rd_night_cobweb", s, m), p =>
                    {
                        var c = Hx(0xD8DDE6); float sx = m ? 1f : -1f; float rr = 0.006f * Mathf.Max(1f, s);
                        const int spokes = 6;
                        Vector3 Pt(int k, float r) { float a = (k / (float)(spokes - 1)) * Mathf.PI * 0.5f; return new Vector3(sx * Mathf.Cos(a) * r, -Mathf.Sin(a) * r, 0); }
                        for (int k = 0; k < spokes; k++) p.Rod(Vector3.zero, Pt(k, s), rr, c, 3, false);
                        for (int ring = 1; ring <= 5; ring++)
                        {
                            float r = s * ring / 5.5f;
                            for (int k = 0; k < spokes - 1; k++)
                            {
                                var a = Pt(k, r); var b = Pt(k + 1, r);
                                var mid = (a + b) * 0.5f * 0.92f;
                                p.Rod(a, mid, rr, c, 3, false); p.Rod(mid, b, rr, c, 3, false);
                            }
                        }
                        var hang = Pt(2, s * 0.62f);
                        p.Rod(hang, hang + new Vector3(0, -s * 0.35f, 0.02f), rr * 0.7f, c, 3, false);
                        var sp = hang + new Vector3(0, -s * 0.35f - 0.03f * s, 0.02f); var blk = Hx(0x15161A);
                        p.Sphere(sp, 0.035f * s, blk, new Vector3(1, 0.8f, 1), 8, 5);
                        p.Sphere(sp + new Vector3(0, 0.035f * s, 0), 0.022f * s, blk, null, 6, 4);
                        for (int i = 0; i < 4; i++) { float x = (i - 1.5f) * 0.02f * s; p.Rod(sp + new Vector3(x, 0, 0), sp + new Vector3(x * 3f, -0.03f * s, 0.03f * s), 0.004f * s, blk, 3, false); }
                    }, White, false);
                    return go;
                }

                /// <summary>Tall arched window full of moonlight (night-blue emissive panes, the moon in one pane, iron mullions,
                /// optional cracked pane, a sill). pos = bottom centre of the opening; front convention (set it into a wall, the
                /// viewer inside). withLight = one shadowless cold spot light shining in (DecorLight; counts against §21).
                /// NO COLLIDER.</summary>
                public static GameObject MoonWindow(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 1.6f, float height = 2.8f, bool cracked = false, bool withLight = false)
                {
                    width = Mathf.Max(0.5f, width); height = Mathf.Max(width * 0.6f, height);
                    var go = Root("MoonWindow", parent, pos, rot, true); var t = go.transform;
                    float W = width, r = width * 0.5f, rectH = height - r; bool ck = cracked;
                    Part(t, "Panes", EnvMeshes.Key("rd_night_moonpanes", W, height), p =>
                    {
                        p.Box(new Vector3(0, rectH * 0.5f, 0), new Vector3(W, rectH, 0.02f), Color.white, 0f);
                        p.Cyl(new Vector3(0, rectH, 0), r, 0.02f, Color.white, 24, Quaternion.Euler(90, 0, 0), false);
                    }, Glow(0x2C4A80, 0.9f, 0.7f), false);
                    EnvMeshes.Render(t, "Moon", ProcMesh.Cached(EnvMeshes.Key("rd_night_moon", W), () => ProcMesh.Cylinder(W * 0.17f, 0.01f, 24, false)), Kit.Mat(MatKey.Moon), new Vector3(W * 0.18f, rectH + r * 0.2f, 0.015f), Quaternion.Euler(90, 0, 0), false);
                    Part(t, "Frame", EnvMeshes.Key("rd_night_moonframe", W, height, ck), p =>
                    {
                        var iron = Hx(0x1E2026); var stone = Hx(0x6A6E75); float fr = 0.07f;
                        p.Box(new Vector3(-r, rectH * 0.5f, 0.02f), new Vector3(fr, rectH, 0.08f), iron, 0.01f);
                        p.Box(new Vector3(r, rectH * 0.5f, 0.02f), new Vector3(fr, rectH, 0.08f), iron, 0.01f);
                        p.Torus(new Vector3(0, rectH, 0.02f), r, fr * 0.5f, iron, Quaternion.Euler(90, 0, 0), 24, 4);
                        p.Box(new Vector3(0, rectH, 0.02f), new Vector3(W, fr * 0.7f, 0.06f), iron, 0f);
                        p.Box(new Vector3(0, (rectH + r) * 0.5f, 0.025f), new Vector3(fr * 0.6f, rectH + r, 0.05f), iron, 0f);
                        for (int i = 1; i < 4; i++) p.Box(new Vector3(0, rectH * i / 4f, 0.025f), new Vector3(W, fr * 0.6f, 0.05f), iron, 0f);
                        p.Box(new Vector3(0, -0.06f, 0.12f), new Vector3(W + 0.3f, 0.12f, 0.3f), stone, 0.02f);
                        p.Box(new Vector3(0, height + 0.08f, 0.04f), new Vector3(0.3f, 0.24f, 0.12f), stone, 0.02f);
                        if (ck)
                        {
                            var o = new Vector3(-r * 0.5f, rectH * 0.37f, 0.016f);
                            for (int i = 0; i < 6; i++) { float a = i * 1.05f + 0.3f; p.Rod(o, o + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r * (0.35f + 0.1f * (i % 3)), 0.006f, Hx(0xC8D8F0), 3, false); }
                        }
                    }, White, false);
                    if (withLight)
                    {
                        var lgo = Kit.Make("MoonBeam", t, new Vector3(0, height * 0.6f, 0.3f), Quaternion.Euler(35f, 0, 0));
                        var l = lgo.AddComponent<Light>(); l.type = LightType.Spot; l.color = Hx(0x9FB8FF); l.range = 12f; l.spotAngle = 55f; l.intensity = 2.5f; l.shadows = LightShadows.None;
                        DecorLight.Mark(l);
                    }
                    return go;
                }

                /// <summary>Tombstone with an engraved inscription (text null = a §24.5 line picked by position), moss, a soil
                /// mound in front. shape: 0 rounded slab, 1 cross, 2 obelisk, 3 square slab (leaning), -1 = by position.
                /// pos = ground point; front convention (inscription to the viewer). Child "Inscription" (TMP).
                /// COLLIDER: one BoxCollider around the stone (not the mound) unless collider=false.</summary>
                public static GameObject Gravestone(Transform parent, Vector3 pos, Quaternion? rot = null, string text = null, int shape = -1, float scale = 1f, bool collider = true)
                {
                    scale = Mathf.Max(0.3f, scale);
                    int h = PosHash(pos);
                    if (shape < 0) shape = h % 4;
                    shape %= 4;
                    text ??= TombstoneLines[h % TombstoneLines.Length];
                    var go = Root("Gravestone", parent, pos, rot, true); var t = go.transform; float s = scale; int sh = shape;
                    float sw = sh == 2 ? 0.55f : 0.85f, sH = sh == 2 ? 2.0f : (sh == 1 ? 1.5f : 1.1f), tilt = sh == 3 ? 6f : 0f;
                    Part(t, "Mesh", EnvMeshes.Key("rd_night_grave", sh, s), p =>
                    {
                        var stone = Hx(0x7D8289); var stone2 = Hx(0x666B72); var moss = Hx(0x4E6B47); var soil = Hx(0x3A2E26);
                        var q = Quaternion.Euler(-tilt, 0, tilt * 0.5f);
                        p.Box(new Vector3(0, 0.08f, 0) * s, new Vector3(sw + 0.2f, 0.16f, 0.45f) * s, stone2, 0.02f);
                        switch (sh)
                        {
                            case 0:
                                p.Box(q * new Vector3(0, 0.16f + (sH - sw * 0.5f) * 0.5f, 0) * s, new Vector3(sw, sH - sw * 0.5f, 0.18f) * s, stone, 0.02f, q);
                                p.Cyl(q * new Vector3(0, 0.16f + sH - sw * 0.5f, 0) * s, sw * 0.5f * s, 0.18f * s, stone, 18, q * Quaternion.Euler(90, 0, 0));
                                break;
                            case 1:
                                p.Box(new Vector3(0, 0.16f + sH * 0.5f, 0) * s, new Vector3(0.22f, sH, 0.16f) * s, stone, 0.02f);
                                p.Box(new Vector3(0, 0.16f + sH * 0.68f, 0) * s, new Vector3(0.8f, 0.2f, 0.16f) * s, stone, 0.02f);
                                p.Box(new Vector3(0, 0.16f + 0.3f, 0.02f) * s, new Vector3(0.7f, 0.6f, 0.16f) * s, stone2, 0.02f);
                                break;
                            case 2:
                                p.Box(new Vector3(0, 0.16f + 0.2f, 0) * s, new Vector3(0.65f, 0.4f, 0.65f) * s, stone2, 0.02f);
                                p.Box(new Vector3(0, 0.56f + (sH - 0.75f) * 0.5f, 0) * s, new Vector3(0.45f, sH - 0.75f, 0.45f) * s, stone, 0.01f);
                                p.Cone(new Vector3(0, 0.56f + sH - 0.75f, 0) * s, 0.32f * s, 0.35f * s, stone, 4, Quaternion.Euler(0, 45, 0));
                                break;
                            default:
                                p.Box(q * new Vector3(0, 0.16f + sH * 0.5f, 0) * s, new Vector3(sw, sH, 0.16f) * s, stone, 0.01f, q);
                                p.Box(q * new Vector3(0, 0.16f + sH, 0) * s, new Vector3(sw + 0.08f, 0.06f, 0.2f) * s, stone2, 0.01f, q);
                                break;
                        }
                        p.Sphere(new Vector3(-sw * 0.35f, 0.17f, 0.12f) * s, 0.12f * s, moss, new Vector3(1.4f, 0.4f, 1f), 8, 5);
                        p.Sphere(new Vector3(sw * 0.3f, 0.16f + sH * 0.6f, -0.05f) * s, 0.08f * s, moss, new Vector3(1f, 1.3f, 1.6f), 6, 4);
                        p.Sphere(new Vector3(0, 0, 0.95f) * s, 0.75f * s, soil, new Vector3(0.6f, 0.12f, 1f), 12, 6);
                        for (int i = 0; i < 5; i++) p.Cone(new Vector3(-0.4f + i * 0.2f, 0.02f, 0.3f + (i % 2) * 0.12f) * s, 0.04f * s, 0.18f * s, moss, 4, Quaternion.Euler(i * 7f - 14f, i * 40f, 0));
                    }, White);
                    float ty = sh == 2 ? 0.36f : (sh == 1 ? 0.46f : 0.16f + sH * 0.45f);
                    float tw = sh == 2 ? 0.6f : (sh == 1 ? 0.62f : sw * 0.86f), thh = sh == 2 ? 0.34f : (sh == 1 ? 0.5f : sH * 0.62f);
                    float tz = sh == 2 ? 0.33f : (sh == 1 ? 0.105f : 0.095f);
                    var tx = Text(t, "Inscription", text, new Vector3(0, ty, tz) * s, new Vector2(tw, thh) * s, Hx(0xD4D9E0), 0.09f * s, false);
                    if (tilt != 0f) { tx.transform.localPosition = Quaternion.Euler(-tilt, 0, tilt * 0.5f) * tx.transform.localPosition; tx.transform.localRotation = Quaternion.Euler(-tilt, 0, tilt * 0.5f) * tx.transform.localRotation; }
                    if (collider) BoxCol(go, new Vector3(0, (0.16f + sH) * 0.5f, 0) * s, new Vector3(sh == 1 ? 0.8f : sw + 0.2f, 0.16f + sH, 0.45f) * s);
                    return go;
                }

                /// <summary>Wrought-iron fence segment running along the local X axis (centred on pos): posts with ball finials every
                /// ~2 m, spear-tipped bars, two rails. Not front-facing (rot = plain rotation). COLLIDER: one thin BoxCollider
                /// (length x height x 0.14) unless collider=false.</summary>
                public static GameObject IronFence(Transform parent, Vector3 pos, Quaternion? rot = null, float length = 4f, float height = 2f, bool collider = true)
                {
                    length = Mathf.Max(0.5f, length); height = Mathf.Max(0.5f, height);
                    var go = Root("IronFence", parent, pos, rot, false);
                    BuildFence(go.transform, -length * 0.5f, length, height);
                    if (collider) BoxCol(go, new Vector3(0, height * 0.5f, 0), new Vector3(length, height, 0.14f));
                    return go;
                }

                /// <summary>Iron fence from 'from' to 'to' (floor points local to parent), one mesh for the whole run.
                /// COLLIDER: one thin BoxCollider along it unless collider=false.</summary>
                public static GameObject IronFenceLine(Transform parent, Vector3 from, Vector3 to, float height = 2f, bool collider = true)
                {
                    var d = to - from; d.y = 0f; float len = Mathf.Max(0.5f, d.magnitude);
                    var dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
                    var go = Kit.Make("IronFenceLine", parent, from, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, -90f, 0));
                    height = Mathf.Max(0.5f, height);
                    BuildFence(go.transform, 0f, len, height);
                    if (collider) BoxCol(go, new Vector3(len * 0.5f, height * 0.5f, 0), new Vector3(len, height, 0.14f));
                    return go;
                }

                static void BuildFence(Transform t, float x0, float len, float h)
                {
                    Part(t, "Mesh", EnvMeshes.Key("rd_night_fence", x0, len, h), p =>
                    {
                        var iron = Hx(0x24262C); var iron2 = Hx(0x34373E);
                        int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
                        for (int i = 0; i <= posts; i++)
                        {
                            float x = x0 + len * i / posts;
                            p.Box(new Vector3(x, h * 0.5f + 0.05f, 0), new Vector3(0.12f, h + 0.1f, 0.12f), iron2, 0.01f);
                            p.Sphere(new Vector3(x, h + 0.16f, 0), 0.08f, iron2, null, 8, 5);
                        }
                        p.Box(new Vector3(x0 + len * 0.5f, h * 0.15f, 0), new Vector3(len, 0.05f, 0.05f), iron, 0f);
                        p.Box(new Vector3(x0 + len * 0.5f, h * 0.82f, 0), new Vector3(len, 0.05f, 0.05f), iron, 0f);
                        int bars = Mathf.Max(2, Mathf.RoundToInt(len / 0.16f));
                        for (int i = 1; i < bars; i++)
                        {
                            float x = x0 + len * i / bars;
                            float bh = h * (0.92f + 0.05f * Mathf.Sin(i * 0.7f));
                            p.Box(new Vector3(x, bh * 0.5f, 0), new Vector3(0.025f, bh, 0.025f), iron, 0f);
                            p.Cone(new Vector3(x, bh, 0), 0.03f, 0.1f, iron, 4);
                            if (i % 2 == 0) p.Torus(new Vector3(x + len / bars * 0.5f, h * 0.9f, 0), len / bars * 0.4f, 0.01f, iron, Quaternion.Euler(90, 0, 0), 8, 3);
                        }
                    }, White);
                }

                /// <summary>Small stone mausoleum: steps, two columns, a pediment with an inscription frieze (child "Inscription"),
                /// an open 2 x 2.8 m doorway with two iron door leaves (children "DoorL"/"DoorR", hinged at the jambs; doorOpenDegrees
                /// swings them in). pos = centre of the footprint on the ground; front convention (doorway to the viewer; the steps
                /// stick out ~0.9 m in front). COLLIDERS: back/side/front walls, roof slab and the three steps (walkable); the door
                /// leaves and the doorway have none (levels gate it). inscription null = "ME-MORIAL".</summary>
                public static GameObject Mausoleum(Transform parent, Vector3 pos, Quaternion? rot = null, float width = 6f, float depth = 6f, float height = 5f, string inscription = null, float doorOpenDegrees = 0f, bool collider = true)
                {
                    width = Mathf.Max(3.2f, width); depth = Mathf.Max(2.5f, depth); height = Mathf.Max(3.6f, height);
                    var go = Root("Mausoleum", parent, pos, rot, true); var t = go.transform;
                    float W = width, Dp = depth, H = height, wt = 0.4f, dw = 2f, dh = 2.8f, wallH = H - 0.9f, sy = 0.54f;
                    Part(t, "Mesh", EnvMeshes.Key("rd_night_mausoleum", W, Dp, H), p =>
                    {
                        var st = Hx(0x8A8E95); var st2 = Hx(0x6C7077); var dark = Hx(0x14151A); var moss = Hx(0x4E6B47);
                        p.Box(new Vector3(0, sy * 0.5f, 0), new Vector3(W + 0.3f, sy, Dp + 0.3f), st2, 0.03f);
                        for (int i = 0; i < 3; i++) p.Box(new Vector3(0, sy * (i + 1) / 3f * 0.5f, Dp * 0.5f + 0.15f + (3 - i) * 0.3f - 0.15f), new Vector3(dw + 1.6f, sy * (i + 1) / 3f, 0.3f), st2, 0.02f);
                        float y0 = sy;
                        p.Box(new Vector3(0, y0 + wallH * 0.5f, -Dp * 0.5f + wt * 0.5f), new Vector3(W, wallH, wt), st, 0.02f);
                        p.Box(new Vector3(-W * 0.5f + wt * 0.5f, y0 + wallH * 0.5f, 0), new Vector3(wt, wallH, Dp), st, 0.02f);
                        p.Box(new Vector3(W * 0.5f - wt * 0.5f, y0 + wallH * 0.5f, 0), new Vector3(wt, wallH, Dp), st, 0.02f);
                        float side = (W - dw) * 0.5f;
                        p.Box(new Vector3(-(dw + side) * 0.5f, y0 + wallH * 0.5f, Dp * 0.5f - wt * 0.5f), new Vector3(side, wallH, wt), st, 0.02f);
                        p.Box(new Vector3((dw + side) * 0.5f, y0 + wallH * 0.5f, Dp * 0.5f - wt * 0.5f), new Vector3(side, wallH, wt), st, 0.02f);
                        p.Box(new Vector3(0, y0 + dh + (wallH - dh) * 0.5f, Dp * 0.5f - wt * 0.5f), new Vector3(dw, wallH - dh, wt), st, 0.02f);
                        p.Box(new Vector3(0, y0 + dh * 0.5f, Dp * 0.5f - wt - 0.02f), new Vector3(dw, dh, 0.04f), dark, 0f);
                        p.Box(new Vector3(0, y0 + 0.02f, 0), new Vector3(W - wt * 2f, 0.04f, Dp - wt * 2f), dark, 0f);
                        // roof slab + pediment
                        p.Box(new Vector3(0, y0 + wallH + 0.12f, 0.2f), new Vector3(W + 0.5f, 0.24f, Dp + 0.9f), st2, 0.03f);
                        float ph = 0.9f, half = (W + 0.5f) * 0.5f, ang = Mathf.Atan2(ph, half) * Mathf.Rad2Deg, sl = Mathf.Sqrt(half * half + ph * ph);
                        p.Box(new Vector3(-half * 0.5f, y0 + wallH + 0.24f + ph * 0.5f, 0.2f), new Vector3(sl, 0.2f, Dp + 0.9f), st, 0.02f, Quaternion.Euler(0, 0, ang));
                        p.Box(new Vector3(half * 0.5f, y0 + wallH + 0.24f + ph * 0.5f, 0.2f), new Vector3(sl, 0.2f, Dp + 0.9f), st, 0.02f, Quaternion.Euler(0, 0, -ang));
                        p.Box(new Vector3(0, y0 + wallH + 0.24f + ph * 0.35f, Dp * 0.5f + 0.5f), new Vector3(W * 0.6f, ph * 0.6f, 0.1f), st2, 0.01f);
                        // columns
                        foreach (float x in new[] { -dw * 0.5f - 0.55f, dw * 0.5f + 0.55f })
                        {
                            p.Box(new Vector3(x, y0 + 0.1f, Dp * 0.5f + 0.3f), new Vector3(0.5f, 0.2f, 0.5f), st2, 0.02f);
                            p.Cyl(new Vector3(x, y0 + wallH * 0.5f, Dp * 0.5f + 0.3f), 0.2f, wallH - 0.2f, st, 14);
                            p.Box(new Vector3(x, y0 + wallH - 0.1f, Dp * 0.5f + 0.3f), new Vector3(0.5f, 0.2f, 0.5f), st2, 0.02f);
                        }
                        // moss + cracks
                        p.Sphere(new Vector3(-W * 0.45f, y0 + 0.15f, Dp * 0.5f), 0.35f, moss, new Vector3(1.5f, 0.4f, 0.6f), 8, 5);
                        p.Sphere(new Vector3(W * 0.3f, y0 + wallH + 0.25f, Dp * 0.5f + 0.6f), 0.3f, moss, new Vector3(2f, 0.3f, 0.5f), 8, 5);
                        p.Rod(new Vector3(W * 0.35f, y0 + 0.6f, Dp * 0.5f + 0.005f), new Vector3(W * 0.3f, y0 + 1.6f, Dp * 0.5f + 0.005f), 0.015f, dark, 3, false);
                    }, White);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var hinge = Kit.Make(sgn < 0 ? "DoorL" : "DoorR", t, new Vector3(sgn * dw * 0.5f, sy, Dp * 0.5f - wt * 0.5f), Quaternion.Euler(0, sgn * doorOpenDegrees, 0));
                        int sg = sgn;
                        Part(hinge.transform, "Mesh", EnvMeshes.Key("rd_night_mausdoor", sg), p =>
                        {
                            var iron = Hx(0x2A2C32); float lw = dw * 0.5f;
                            p.Box(new Vector3(-sg * lw * 0.5f, 0.05f, 0), new Vector3(lw, 0.08f, 0.06f), iron, 0f);
                            p.Box(new Vector3(-sg * lw * 0.5f, dh - 0.05f, 0), new Vector3(lw, 0.08f, 0.06f), iron, 0f);
                            p.Box(new Vector3(-sg * lw * 0.5f, dh * 0.55f, 0), new Vector3(lw, 0.06f, 0.05f), iron, 0f);
                            for (int i = 0; i < 6; i++) p.Box(new Vector3(-sg * (0.04f + i * (lw - 0.08f) / 5f), dh * 0.5f, 0), new Vector3(0.04f, dh, 0.04f), iron, 0f);
                            p.Torus(new Vector3(-sg * lw * 0.5f, dh * 0.75f, 0), lw * 0.3f, 0.02f, iron, Quaternion.Euler(90, 0, 0), 14, 3);
                        }, White);
                    }
                    Text(t, "Inscription", inscription ?? "ME-MORIAL", new Vector3(0, sy + wallH + 0.24f + 0.9f * 0.35f, Dp * 0.5f + 0.56f), new Vector2(W * 0.56f, 0.45f), Hx(0xD4D9E0), 0.36f);
                    if (collider)
                    {
                        BoxCol(go, new Vector3(0, sy * 0.5f, 0), new Vector3(W + 0.3f, sy, Dp + 0.3f));
                        for (int i = 0; i < 3; i++) BoxCol(go, new Vector3(0, sy * (i + 1) / 3f * 0.5f, Dp * 0.5f + 0.15f + (3 - i) * 0.3f - 0.15f), new Vector3(dw + 1.6f, sy * (i + 1) / 3f, 0.3f));
                        float y0 = sy, side = (W - dw) * 0.5f;
                        BoxCol(go, new Vector3(0, y0 + wallH * 0.5f, -Dp * 0.5f + wt * 0.5f), new Vector3(W, wallH, wt));
                        BoxCol(go, new Vector3(-W * 0.5f + wt * 0.5f, y0 + wallH * 0.5f, 0), new Vector3(wt, wallH, Dp));
                        BoxCol(go, new Vector3(W * 0.5f - wt * 0.5f, y0 + wallH * 0.5f, 0), new Vector3(wt, wallH, Dp));
                        BoxCol(go, new Vector3(-(dw + side) * 0.5f, y0 + wallH * 0.5f, Dp * 0.5f - wt * 0.5f), new Vector3(side, wallH, wt));
                        BoxCol(go, new Vector3((dw + side) * 0.5f, y0 + wallH * 0.5f, Dp * 0.5f - wt * 0.5f), new Vector3(side, wallH, wt));
                        BoxCol(go, new Vector3(0, y0 + dh + (wallH - dh) * 0.5f, Dp * 0.5f - wt * 0.5f), new Vector3(dw, wallH - dh, wt));
                        BoxCol(go, new Vector3(0, y0 + wallH + 0.12f, 0.2f), new Vector3(W + 0.5f, 0.24f, Dp + 0.9f));
                    }
                    return go;
                }
            }
        }
    }
}
