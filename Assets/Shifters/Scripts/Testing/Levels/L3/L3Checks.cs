// =====================================================================================
//  SHIFTERS - L3Checks.cs   [LEVEL 3]  scene sanity checks + overview screenshots + DevPlay entry points
//    Runtime build + shots + solution:  devplay.sh <proj> l3_rt -devSetup Shifters.L3Checks.RuntimeRun -devSeconds 1
//    Shots only (runtime build):         devplay.sh <proj> l3_shots -devSetup Shifters.L3Checks.RuntimeShots
//    Built scene shots:                  devplay.sh <proj> l3_scene -devScene Assets/Shifters/Scenes/L3_SacrificeZone.unity -devSetup Shifters.L3Checks.SceneShots
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L3Checks
    {
        /// <summary>No missing/default/magenta materials; every gameplay anchor exists.</summary>
        public static void SceneSanity(DevPlayContext ctx)
        {
            int bad = 0, total = 0;
            string first = null;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.GetComponentInParent<ShifterCharacter>() != null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    total++;
                    bool broken = m == null || m.shader == null || m.name.StartsWith("Default-Material") || m.shader.name == "Hidden/InternalErrorShader";
                    if (!broken) continue;
                    bad++;
                    if (first == null) first = r.name + " (" + (m == null ? "null" : m.name + "/" + (m.shader != null ? m.shader.name : "no shader")) + ")";
                }
            }
            ctx?.Check(bad == 0, "L3 materials: " + bad + " broken of " + total + (first != null ? " first: " + first : ""));
            foreach (var id in new[] { "spawn", "checkpoint.1", "checkpoint.2", "checkpoint.3", "exit", "S1.Bot", "S1.Cell", "S1.CellLever", "S1.Door",
                                       "S2.Button", "S2.Door", "S2.Lever", "S3.Chair", "S3.Bridge", "S3.Release", "S3.TunnelDoor", "S3.Gate", "S3.Bot", "S3.Cell",
                                       "S4.BotA", "S4.BotB", "S4.Plate", "S4.Door", "S4.Hatch", "S4.Cell", "S4.CellLever" })
                ctx?.Check(LevelMark.Exists(id), "L3 mark " + id);
            foreach (var id in new[] { "S1.CellExit", "S1.CellStage", "S1.WallS", "S1.WallN", "S3.CellExit", "S4.CellExit", "S4.CellStage",
                                       "S4.LaneS", "S4.LaneMid", "S4.LaneN", "S4.PlateApproach", "S4.NorthOfPlate" })
                ctx?.Check(LevelMark.Exists(id), "L3 mark " + id);
            CellsOutsideArenas(ctx);
            TextsFaceOut(ctx);
            ctx?.Check(Checkpoint.All.Count == 3, "L3 has 3 checkpoints (" + Checkpoint.All.Count + ")");
            ctx?.Check(SecurityBot.All.Count == 4, "L3 has 4 security bots (" + SecurityBot.All.Count + ")");
            ctx?.Check(HoldingCell.All.Count == 3, "L3 has 3 holding cells (" + HoldingCell.All.Count + ")");
        }

        /// <summary>Design invariants that keep captures recoverable: every intern inside a holding cell (and every square
        /// metre of the S4 plate) is out of reach of the bots that guard it (they only chase targets within arena radius + 0.5),
        /// so a bot handing over a prisoner can never re-grab the one it just released through the open door.</summary>
        static void CellsOutsideArenas(DevPlayContext ctx)
        {
            foreach (var b in SecurityBot.All)
            {
                var cell = b != null ? b.Cell : null;
                if (cell == null) { ctx?.Check(false, "L3 bot " + (b != null ? b.name : "null") + " has no holding cell"); continue; }
                Vector3 d = cell.DropPosition; Vector3 r = cell.transform.right, back = -cell.transform.forward;
                bool inside = b.InArena(d, 0.6f) || b.InArena(d + r + back * 0.4f, 0.6f) || b.InArena(d - r + back * 0.4f, 0.6f);
                ctx?.Check(!inside, "L3 cell " + cell.Id + " is outside the reach of " + b.name + " (arena r " + b.ArenaRadius + ")");
            }
            var plate = LevelMark.Find("S4.Plate");
            var botA = LevelMark.Find<SecurityBot>("S4.BotA");
            if (plate != null && botA != null)
            {
                bool any = false;
                for (int i = -1; i <= 1; i++)
                    for (int k = -1; k <= 1; k++)
                        any |= botA.InArena(plate.transform.position + new Vector3(i, 0f, k), 0.6f);
                ctx?.Check(!any, "L3 S4 plate (all of it) is outside the bots' reach");
            }
        }

        /// <summary>Every world-space text (signs, posters, labels, kit readouts) must show its READABLE side to open space:
        /// a text whose readable side (TMP: -forward) runs into solid geometry within 0.35 m is mounted backwards.</summary>
        static void TextsFaceOut(DevPlayContext ctx)
        {
            int total = 0, bad = 0;
            string first = "";
            foreach (var t in Object.FindObjectsByType<TMPro.TextMeshPro>())
            {
                if (t == null || !t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text) || t.GetComponentInParent<ShifterCharacter>() != null) continue;
                total++;
                Vector3 n = -t.transform.forward;
                if (!Physics.Raycast(t.transform.position + n * 0.01f, n, out var hit, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<ShifterCharacter>() != null || hit.collider.GetComponentInParent<SecurityBot>() != null) continue;
                bad++;
                if (bad <= 6) first += " '" + t.text.Replace("\n", " ") + "'@" + BotFmt.V(t.transform.position) + "->" + hit.collider.name;
            }
            ctx?.Check(bad == 0, "L3 texts readable from open space: " + (total - bad) + "/" + total + (bad > 0 ? " BACKWARDS:" + first : ""));
        }

        static readonly (string name, Vector3 pos, Vector3 look)[] Views =
        {
            ("v01_spawn", new Vector3(0f, 3.2f, -8f), new Vector3(0f, 1.2f, 6f)),
            ("v02_bothall", new Vector3(-6f, 9f, 12f), new Vector3(1f, 0f, 32f)),
            ("v03_gallery_pulpit", new Vector3(-6f, 5f, 22f), new Vector3(6f, 1.5f, 28f)),
            ("v04_bothall_far", new Vector3(4f, 8f, 26f), new Vector3(-3f, 0.5f, 45f)),
            ("v05_timed_walkway", new Vector3(5f, 7f, 50f), new Vector3(0f, 0f, 72f)),
            ("v06_controlroom", new Vector3(-6f, 6f, 86f), new Vector3(2f, 0.5f, 94f)),
            ("v07_chair", new Vector3(-5f, 5f, 92f), new Vector3(3f, 1f, 100f)),
            ("v08_acidpit_far", new Vector3(5f, 7f, 100f), new Vector3(-1f, 0f, 124f)),
            ("v09_tunnel", new Vector3(-11.5f, 7f, 104f), new Vector3(-14f, 0.5f, 118f)),
            ("v10_tunnel_botroom", new Vector3(-12f, 8f, 116f), new Vector3(-14.5f, 0.5f, 130f)),
            ("v11_gauntlet", new Vector3(7f, 9f, 140f), new Vector3(-1f, 0f, 165f)),
            ("v12_gauntlet_far", new Vector3(-3f, 7f, 150f), new Vector3(1f, 1f, 176f)),
            ("v13_exit", new Vector3(-6f, 6f, 176f), new Vector3(3f, 1.5f, 184f)),
            ("v14_overview", new Vector3(32f, 60f, 60f), new Vector3(0f, 0f, 95f)),
            ("v15_s1_prow", new Vector3(-2.5f, 4.2f, 20.5f), new Vector3(4.2f, 1.6f, 27f)),
            ("v16_s1_cell", new Vector3(1.5f, 4.5f, 13f), new Vector3(-7f, 1.2f, 17f)),
            ("v17_s4_prow", new Vector3(2.5f, 4.5f, 152.5f), new Vector3(-4.2f, 1.6f, 159.5f)),
            ("v18_s4_cell_dock", new Vector3(-1.5f, 5f, 141f), new Vector3(7.5f, 1.2f, 154f)),
            ("v19_s4_plate", new Vector3(-3f, 5f, 164f), new Vector3(3f, 0.5f, 172.5f)),
        };

        /// <summary>Takes over the main camera (ShiftersCamera disabled) for a set of overview shots.</summary>
        public static IEnumerator Overview(DevPlayContext ctx)
        {
            var cam = Camera.main;
            if (cam == null) { ctx.Log("no main camera"); yield break; }
            var sc = cam.GetComponent<ShiftersCamera>();
            if (sc != null) sc.enabled = false;
            var p0 = cam.transform.position; var r0 = cam.transform.rotation;
            var canvases = Object.FindObjectsByType<Canvas>();
            var was = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++) { was[i] = canvases[i].enabled; canvases[i].enabled = false; }
            foreach (var v in Views)
            {
                cam.transform.position = v.pos;
                cam.transform.rotation = Quaternion.LookRotation(v.look - v.pos);
                yield return null;
                yield return null;
                ctx.Shot(v.name, cam);
            }
            for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = was[i];
            cam.transform.SetPositionAndRotation(p0, r0);
            if (sc != null) sc.enabled = true;
            yield return null;
        }

        public static IEnumerator RuntimeShots(DevPlayContext ctx)
        {
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(2);
            ctx.Check(lctx != null && !lctx.Failed, "L3 built at runtime");
            if (lctx != null) ctx.Log(lctx.Report());
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(1f);
            SceneSanity(ctx);
            yield return Overview(ctx);
        }

        public static IEnumerator RuntimeRun(DevPlayContext ctx)
        {
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(2);
            ctx.Check(lctx != null && !lctx.Failed, "L3 built at runtime");
            if (lctx != null) ctx.Log(lctx.Report());
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(1f);
            SceneSanity(ctx);
            if (BotArgs.Int("-l3Shots", 0) != 0) yield return Overview(ctx);
            yield return TeamBot.Run(ctx, L3Solution.Solve, null, "L3rt");
        }

        public static IEnumerator SceneShots(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(3f);
            SceneSanity(ctx);
            yield return Overview(ctx);
        }
    }
}
