// =====================================================================================
//  SHIFTERS - SmokeSignSelfTest.cs   [smoke tests - proves the generic sign audit and the EnvKit facing helpers]
//    devplay.sh <proj> sign_selftest -devSetup Shifters.SmokeSignSelfTest.Run -devSeconds 1     (no -devScene)
//  Builds a small test room in the empty DevPlay scene with EnvKit pieces mounted RIGHT (EnvKit.WallMountFacing /
//  FacingViewer / FacingViewerAt) and deliberately WRONG (the classic "rot = the direction the face points" mistake,
//  upside-down / sideways / mirrored / buried texts, a backwards light panel and whiteboard), plus things the audit
//  must leave alone (a billboard, a spinning text, the back of a double-sided label, a label on a dynamic prop, a sign
//  behind a thin pipe). Checks that SceneAudit.SignProblems flags exactly the BAD_* groups with the expected kind, the
//  helper math, and the BrainJar "IQ 9000" plate (outward by default, plateFacing overload, and L4's
//  FaceBrainJarPlate still turning it towards the path). Screenshots: room, back side, jars, issue close-ups.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class SmokeSignSelfTest
    {
        // group name -> expected issue kind (BAD_*), no issue allowed for GOOD_* / SKIP_*
        static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
        {
            { "BAD_sign_classic_mistake", "FACES-WALL" },
            { "BAD_poster_backwards", "FACES-WALL" },
            { "BAD_lightpanel_backwards", "FACES-WALL" },
            { "BAD_whiteboard_backwards", "FACES-WALL" },
            { "BAD_east_sign_viewerlooking_out", "FACES-WALL" },
            { "BAD_label_upside_down", "UPSIDE-DOWN" },
            { "BAD_label_sideways", "SIDEWAYS" },
            { "BAD_label_mirrored", "MIRRORED" },
            { "BAD_label_buried_in_wall", "BURIED" },
            { "BAD_floor_text_under_floor", "BURIED" },
        };

        const float WallZ = 4f;     // north wall surface (room side), outward normal -Z
        const float EastX = 8f;     // east wall surface, outward normal -X

        public static IEnumerator Run(DevPlayContext ctx)
        {
            Kit.Theme = LevelTheme.Training;
            LevelLighting.Apply(LevelTheme.Training);
            var root = new GameObject("[SignSelfTest]").transform;
            var cam = Camera.main;
            var camPos = new Vector3(0f, 2.2f, -5.5f);
            if (cam != null) cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(new Vector3(0f, 2.1f, WallZ) - camPos));

            HelperMath(ctx);
            BuildRoom(root);
            var jars = BuildJars(ctx, root);
            BuildCases(root, camPos);
            yield return null;
            yield return null;   // billboards / spinners run their first updates

            var moving = new HashSet<Transform>();
            yield return SceneAudit.CollectMovingTexts(moving);
            var res = SceneAudit.SignProblems(moving, root);
            ctx.Log("[SIGNTEST] " + res.Summary);
            for (int i = 0; i < res.Issues.Count; i++) ctx.Log("[SIGNTEST]   #" + i + " " + res.Issues[i]);
            for (int i = 0; i < res.Warnings.Count; i++) ctx.Log("[SIGNTEST]   warn #" + i + " " + res.Warnings[i]);
            JudgeResult(ctx, res, root);
            JarChecks(ctx, jars);

            if (cam != null)
            {
                ctx.Shot("signtest_room", cam);
                Shoot(ctx, cam, "signtest_room_left", new Vector3(-4.5f, 2.4f, -1.2f), new Vector3(-4.5f, 2.2f, WallZ));
                Shoot(ctx, cam, "signtest_room_right", new Vector3(4.5f, 2.4f, -1.2f), new Vector3(4.5f, 2.2f, WallZ));
                Shoot(ctx, cam, "signtest_east", new Vector3(3f, 2.2f, -1.5f), new Vector3(EastX, 2.2f, -1.5f));
                Shoot(ctx, cam, "signtest_jars_from_north", new Vector3(-5.2f, 2.2f, -0.6f), new Vector3(-5.2f, 0.5f, -4f));
                Shoot(ctx, cam, "signtest_jar_plateFacing_back", new Vector3(-5.2f, 0.9f, -6.6f), new Vector3(-5.2f, 0.4f, -4f));
                Shoot(ctx, cam, "signtest_jar_l4fix_east", new Vector3(-0.6f, 0.9f, -4f), new Vector3(-3.2f, 0.4f, -4f));
            }
            yield return SceneAudit.SignIssueSheet(ctx, res.Issues, "signtest_issues");
        }

        static void Shoot(DevPlayContext ctx, Camera cam, string name, Vector3 pos, Vector3 look)
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            ctx.Shot(name, cam);
        }

        // ------------------------------------------------------------------ helper math
        static void HelperMath(DevPlayContext ctx)
        {
            ctx.Check(Quaternion.Angle(EnvKit.FacingViewer(Vector3.back), Quaternion.identity) < 0.01f, "[SIGNTEST] FacingViewer(back) == identity");
            ctx.Check(Quaternion.Angle(EnvKit.WallMountFacing(Vector3.back), Quaternion.identity) < 0.01f, "[SIGNTEST] WallMountFacing(back) == identity");
            ctx.Check(Quaternion.Angle(EnvKit.ViewerLooking(Vector3.forward), Quaternion.identity) < 0.01f, "[SIGNTEST] ViewerLooking(forward) == identity");
            ctx.Check(Quaternion.Angle(EnvKit.FacingViewer(Vector3.forward), Quaternion.Euler(0f, 180f, 0f)) < 0.01f, "[SIGNTEST] FacingViewer(forward) == Euler(0,180,0)");
            ctx.Check(Quaternion.Angle(EnvKit.FacingViewer(Vector3.up), Quaternion.Euler(90f, 0f, 0f)) < 0.01f, "[SIGNTEST] FacingViewer(up) == Euler(90,0,0) (the documented floor panel)");
            ctx.Check(Quaternion.Angle(EnvKit.ViewToFace(Quaternion.identity), Quaternion.Euler(0f, 180f, 0f)) < 0.01f, "[SIGNTEST] ViewToFace unchanged (Y180)");
            var dirs = new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.up, Vector3.down, new Vector3(1f, 0f, 1f).normalized, new Vector3(-0.3f, 0.2f, 0.9f).normalized };
            int ok = 0, upright = 0, horizontal = 0;
            foreach (var d in dirs)
            {
                var q = EnvKit.FacingViewer(d);
                if (Vector3.Dot(EnvKit.ReadableDirection(q), d) > 0.9999f) ok++;
                if (Mathf.Abs(d.y) < 0.5f) { horizontal++; if ((q * Vector3.up).y > 0.95f) upright++; }
            }
            ctx.Check(ok == dirs.Length, "[SIGNTEST] ReadableDirection(FacingViewer(d)) == d for " + ok + "/" + dirs.Length + " directions");
            ctx.Check(upright == horizontal, "[SIGNTEST] wall texts stay upright (" + upright + "/" + horizontal + ")");
            var at = EnvKit.ReadableDirection(EnvKit.FacingViewerAt(new Vector3(3f, 1f, 2f), new Vector3(3f, 9f, -8f)));
            ctx.Check(Vector3.Dot(at, Vector3.back) > 0.9999f, "[SIGNTEST] FacingViewerAt turns about Y only, towards the viewer " + SceneAudit.Fmt(at));
            var floorUp = EnvKit.FacingViewer(Vector3.up) * Vector3.up;
            ctx.Check(Vector3.Dot(floorUp, Vector3.forward) > 0.9999f, "[SIGNTEST] floor text top points +Z by default " + SceneAudit.Fmt(floorUp));
        }

        // ------------------------------------------------------------------ scene
        static Transform G(Transform root, string name) => Kit.Make(name, root, Vector3.zero).transform;

        static void BuildRoom(Transform root)
        {
            var room = G(root, "Room");
            EnvKit.Floor(room, new Vector3(0f, 0f, -1f), new Vector3(17f, 0.4f, 12f));
            EnvKit.Wall(room, new Vector3(-8.5f, 0f, WallZ + 0.2f), new Vector3(8.5f, 0f, WallZ + 0.2f), 4.5f, 0.4f);
            EnvKit.Wall(room, new Vector3(EastX + 0.2f, 0f, -7f), new Vector3(EastX + 0.2f, 0f, WallZ + 0.4f), 4.5f, 0.4f);
        }

        static TextMeshPro Label(Transform parent, Vector3 pos, Quaternion rot, string text, float scaleX = 1f)
        {
            var go = Kit.Make("Label", parent, pos, rot);
            if (scaleX < 0f) go.transform.localScale = new Vector3(scaleX, 1f, 1f);
            var tmp = go.AddComponent<TextMeshPro>();
            var f = ShiftersFonts.Display;
            if (f != null) tmp.font = f;
            tmp.text = text;
            tmp.fontSize = 3f;
            tmp.color = new Color(0.75f, 0.1f, 0.15f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            return tmp;
        }

        static void BuildCases(Transform root, Vector3 camPos)
        {
            float z = WallZ - 0.02f;   // mounting depth in front of the north wall
            var north = Vector3.back;  // its outward normal
            // ---- right
            EnvKit.Sign(G(root, "GOOD_sign_wallmount"), new Vector3(-6f, 3.2f, z), EnvKit.WallMountFacing(north), "GOOD: WALL MOUNT", new Vector2(2.6f, 0.6f));
            EnvKit.Poster(G(root, "GOOD_poster_facingviewer"), new Vector3(0.6f, 3.1f, z), EnvKit.FacingViewer(north), "GOOD POSTER", "FACING VIEWER");
            EnvKit.LightPanel(G(root, "GOOD_lightpanel"), new Vector3(5.5f, 3.75f, z), EnvKit.WallMountFacing(north), new Vector2(1.6f, 0.4f), new Color(1f, 0.9f, 0.6f));
            Label(G(root, "GOOD_label"), new Vector3(-6f, 1.6f, z - 0.01f), EnvKit.FacingViewer(north), "GOOD LABEL");
            EnvKit.Sign(G(root, "GOOD_east_sign"), new Vector3(EastX - 0.02f, 2.2f, 0f), EnvKit.WallMountFacing(Vector3.left), "GOOD: EAST WALL", new Vector2(2.6f, 0.6f));
            var free = new Vector3(-3f, 1.4f, -1f);
            EnvKit.Sign(G(root, "GOOD_freestanding_facing_camera"), free, EnvKit.FacingViewerAt(free, camPos), "FACING CAMERA", new Vector2(2.4f, 0.55f));
            Label(G(root, "GOOD_floor_text"), new Vector3(0f, 0.015f, -1.5f), EnvKit.FacingViewer(Vector3.up), "FLOOR TEXT");
            EnvKit.Decor.Whiteboard(G(root, "GOOD_whiteboard_free"), new Vector3(6f, 0f, -2.5f), EnvKit.FacingViewer(north));
            var pipe = G(root, "GOOD_sign_behind_thin_pipe");
            EnvKit.Sign(pipe, new Vector3(-6f, 2.4f, z), EnvKit.WallMountFacing(north), "PIPE IN FRONT", new Vector2(2.6f, 0.6f));
            EnvKit.Pipe(pipe, new[] { new Vector3(-7.6f, 2.4f, WallZ - 0.25f), new Vector3(-4.4f, 2.4f, WallZ - 0.25f) }, 0.06f, true);
            // ---- wrong
            EnvKit.Sign(G(root, "BAD_sign_classic_mistake"), new Vector3(-2.5f, 3.2f, z), Quaternion.LookRotation(north), "BAD: BACKWARDS", new Vector2(2.6f, 0.6f));
            EnvKit.Poster(G(root, "BAD_poster_backwards"), new Vector3(2.6f, 3.1f, z), Quaternion.Euler(0f, 180f, 0f), "BAD POSTER", "BACKWARDS");
            EnvKit.LightPanel(G(root, "BAD_lightpanel_backwards"), new Vector3(5.5f, 3.05f, z), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.6f, 0.4f), new Color(1f, 0.9f, 0.6f));
            EnvKit.Decor.Whiteboard(G(root, "BAD_whiteboard_backwards"), new Vector3(5.6f, 0f, WallZ - 0.3f), Quaternion.Euler(0f, 180f, 0f));   // legs clear the wall
            EnvKit.Sign(G(root, "BAD_east_sign_viewerlooking_out"), new Vector3(EastX - 0.02f, 2.2f, -3f), EnvKit.ViewerLooking(Vector3.left), "BAD: EAST WALL", new Vector2(2.6f, 0.6f));
            Label(G(root, "BAD_label_upside_down"), new Vector3(-3.4f, 1.6f, z - 0.01f), EnvKit.FacingViewer(north, Vector3.down), "UPSIDE DOWN");
            Label(G(root, "BAD_label_sideways"), new Vector3(-0.9f, 1.9f, z - 0.01f), EnvKit.FacingViewer(north, Vector3.right), "SIDEWAYS");
            Label(G(root, "BAD_label_mirrored"), new Vector3(0.9f, 1.6f, z - 0.01f), EnvKit.FacingViewer(north), "MIRRORED", -1f);
            Label(G(root, "BAD_label_buried_in_wall"), new Vector3(2.6f, 1.6f, WallZ + 0.2f), EnvKit.FacingViewer(north), "BURIED");
            Label(G(root, "BAD_floor_text_under_floor"), new Vector3(2.5f, -0.06f, -1.5f), EnvKit.FacingViewer(Vector3.up), "UNDER FLOOR");
            // ---- must not be judged
            var bb = G(root, "SKIP_billboard");
            var bbRoot = Kit.Make("Billboard", bb, new Vector3(-2.5f, 2.4f, WallZ - 0.35f));
            bbRoot.AddComponent<PuzzleBillboard>();
            Label(bbRoot.transform, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), "BILLBOARD");      // faces away from the camera
            var spin = Kit.Make("Spinner", G(root, "SKIP_spinning_text"), new Vector3(4f, 2.3f, WallZ - 0.45f));
            spin.AddComponent<SlowSpin>().DegreesPerSecond = 90f;
            Label(spin.transform, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), "SPIN");
            var ds = Kit.Make("DoubleSided", G(root, "SKIP_double_sided_back"), new Vector3(3.7f, 1.6f, WallZ - 0.12f));
            Label(ds.transform, new Vector3(0f, 0f, -0.01f), EnvKit.FacingViewer(north), "FRONT");
            Label(ds.transform, new Vector3(0f, 0f, 0.01f), EnvKit.FacingViewer(-north), "BACK");     // faces the wall
            // a long free-standing label whose ends run into two pillars: CLIPPED warning, not an issue
            var clip = G(root, "WARN_label_clipped_by_pillars");
            var longLabel = Label(clip, new Vector3(-1f, 1.5f, -6.3f), EnvKit.FacingViewer(Vector3.forward), "THIS LONG LABEL RUNS INTO TWO PILLARS");
            longLabel.ForceMeshUpdate(true);
            float halfW = Mathf.Max(1f, longLabel.textBounds.extents.x);                 // pillars across the outer audit columns (+-0.6 half width)
            EnvKit.Pillar(clip, new Vector3(-1f - halfW * 0.6f, 0f, -6.3f), 3f, 0.9f);
            EnvKit.Pillar(clip, new Vector3(-1f + halfW * 0.6f, 0f, -6.3f), 3f, 0.9f);
            var crate = Kit.Make("Crate", G(root, "SKIP_prop_label"), new Vector3(7.2f, 0.5f, WallZ - 0.7f));
            crate.AddComponent<BoxCollider>().size = Vector3.one;
            crate.AddComponent<Rigidbody>().mass = 25f;
            Kit.BoxVisual(crate.transform, "Box", Vector3.zero, Vector3.one, MatKey.Wood, 0.04f);
            Label(crate.transform, new Vector3(0f, 0f, 0.51f), EnvKit.FacingViewer(Vector3.forward), "FRAGILE");   // printed on the wall side
        }

        // ------------------------------------------------------------------ jars
        static GameObject[] BuildJars(DevPlayContext ctx, Transform root)
        {
            var g = G(root, "GOOD_brainjars");
            var a = EnvKit.Decor.BrainJar(g, new Vector3(-7.2f, 0f, -4f));                       // default
            var b = EnvKit.Decor.BrainJar(g, new Vector3(-5.2f, 0f, -4f), 1f, Vector3.back);     // plateFacing overload
            var c = EnvKit.Decor.BrainJar(g, new Vector3(-3.2f, 0f, -4f), 1.2f);                 // + L4's local fix
            var fix = System.Type.GetType("Shifters.L4Decor")?.GetMethod("FaceBrainJarPlate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (fix != null) fix.Invoke(null, new object[] { c, Vector3.right });
            else ctx.Log("[SIGNTEST] L4Decor.FaceBrainJarPlate not found - L4 compatibility not checked");
            ctx.Check(fix != null, "[SIGNTEST] L4Decor.FaceBrainJarPlate present (L4's local plate fix)");
            return new[] { a, b, c };
        }

        static void JarChecks(DevPlayContext ctx, GameObject[] jars)
        {
            var want = new[] { Vector3.forward, Vector3.back, Vector3.right };
            string[] names = { "default", "plateFacing(back)", "L4 FaceBrainJarPlate(right)" };
            for (int i = 0; i < jars.Length; i++)
            {
                var jar = jars[i];
                var tmp = jar != null ? jar.GetComponentInChildren<TextMeshPro>() : null;
                if (tmp == null) { ctx.Check(false, "[SIGNTEST] brain jar " + names[i] + " has its IQ 9000 plate"); continue; }
                var readable = -tmp.transform.forward;
                var outward = tmp.transform.position - jar.transform.position; outward.y = 0f; outward.Normalize();
                ctx.Check(Vector3.Dot(readable, outward) > 0.99f, "[SIGNTEST] brain jar " + names[i] + ": plate '" + tmp.text + "' reads OUTWARD (readable " +
                          SceneAudit.Fmt(readable) + ", outward " + SceneAudit.Fmt(outward) + ")");
                ctx.Check(Vector3.Dot(readable, want[i]) > 0.99f, "[SIGNTEST] brain jar " + names[i] + ": plate faces " + SceneAudit.Fmt(want[i]));
                var plate = tmp.transform.parent;
                ctx.Check(plate != null && plate.parent == jar.transform && Quaternion.Angle(plate.localRotation, Quaternion.identity) < 0.5f,
                          "[SIGNTEST] brain jar " + names[i] + ": plate is a direct child with identity local rotation (L4 fix compatible)");
            }
        }

        // ------------------------------------------------------------------ verdict
        static void JudgeResult(DevPlayContext ctx, SceneAudit.SignAuditResult res, Transform root)
        {
            var found = new Dictionary<string, List<SceneAudit.SignIssue>>();
            foreach (var issue in res.Issues)
            {
                string group = GroupOf(issue.Subject, root);
                if (!found.TryGetValue(group, out var l)) found[group] = l = new List<SceneAudit.SignIssue>();
                l.Add(issue);
            }
            foreach (var kv in Expected)
            {
                found.TryGetValue(kv.Key, out var l);
                bool ok = l != null && l.Count == 1 && l[0].Kind.Contains(kv.Value) && l[0].Path.Contains(kv.Key);
                ctx.Check(ok, "[SIGNTEST] " + kv.Key + " flagged " + kv.Value + " -> " + (l == null ? "NOT FLAGGED" : l.Count + " issue(s): " + l[0]));
            }
            int wrong = 0;
            foreach (var kv in found)
            {
                if (Expected.ContainsKey(kv.Key)) continue;
                wrong += kv.Value.Count;
                foreach (var i in kv.Value) ctx.Log("[SIGNTEST] UNEXPECTED issue in " + kv.Key + ": " + i);
            }
            ctx.Check(wrong == 0, "[SIGNTEST] no false positives on GOOD_* / SKIP_* items (" + wrong + ")");
            ctx.Check(res.Billboards >= 1, "[SIGNTEST] billboard text not judged (billboards=" + res.Billboards + ")");
            ctx.Check(res.Moving >= 1, "[SIGNTEST] spinning text not judged (moving=" + res.Moving + ")");
            ctx.Check(res.DoubleSidedBacks >= 1, "[SIGNTEST] back of a double-sided label excused (" + res.DoubleSidedBacks + ")");
            ctx.Check(res.PropLabels >= 1, "[SIGNTEST] label on a dynamic prop not judged (" + res.PropLabels + ")");
            ctx.Check(res.LightPanels == 2, "[SIGNTEST] both light panels judged (" + res.LightPanels + ")");
            int clipped = 0, otherWarnings = 0;
            foreach (var w in res.Warnings)
            {
                if (GroupOf(w.Subject, root) == "WARN_label_clipped_by_pillars" && w.Kind == "CLIPPED") clipped++;
                else { otherWarnings++; ctx.Log("[SIGNTEST] UNEXPECTED warning: " + w); }
            }
            ctx.Check(clipped == 1 && otherWarnings == 0, "[SIGNTEST] long label running into pillars = 1 CLIPPED warning (" + clipped + "), no other warnings (" + otherWarnings + ")");
        }

        static string GroupOf(Transform t, Transform root)
        {
            for (var c = t; c != null; c = c.parent) if (c.parent == root) return c.name;
            return "(outside)";
        }
    }
}
