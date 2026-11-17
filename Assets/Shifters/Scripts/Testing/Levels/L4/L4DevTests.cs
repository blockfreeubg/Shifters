// =====================================================================================
//  SHIFTERS - L4DevTests.cs   [owned by LEVEL 4]
//  Development scenarios for Level 4:
//    Shifters.L4DevTests.RuntimeSolve  - build the level at runtime, take overview + IQ-view shots,
//                                        then run L4Solution (pass -l4Seed N to force a team)
//    Shifters.L4DevTests.SceneViews    - on the BUILT scene: overview/section shots only
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L4DevTests
    {
        public static IEnumerator RuntimeSolve(DevPlayContext ctx)
        {
            int seed = BotArgs.Int("-l4Seed", 0);
            if (seed != 0) TeamBot.ForceTeam(seed);
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(3);
            ctx.Check(lctx != null && !lctx.Failed, "L4 runtime build ok");
            if (lctx != null)
            {
                ctx.Log(lctx.Report());
                ctx.Check(lctx.CheckpointCount == 4, "4 checkpoints (" + lctx.CheckpointCount + ")");
                ctx.Check(lctx.ExitCount == 1, "one exit");
                ctx.Check(lctx.HintCount >= 6, "a hint zone per puzzle (" + lctx.HintCount + ")");
            }
            yield return ctx.Wait(6.5f);
            if (BotArgs.Int("-l4Views", 1) != 0) yield return Views(ctx, true);
            int from = BotArgs.Int("-l4From", 1);
            var opts = new BotOptions { AllowDebugAssists = from > 1 };
            yield return TeamBot.Run(ctx, b => L4Solution.SolveFrom(b, from), opts, from > 1 ? "L4rt_from" + from : "L4rt");
            ctx.Check(TeamBot.LastResult != null && TeamBot.LastResult.Passed, "L4 solution passed on the runtime build");
            ctx.Shot("z_end");
        }

        /// <summary>Built-scene audit: no default/magenta materials, sane rigidbody count, then every section view.</summary>
        public static IEnumerator SceneViews(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            var problems = SceneAudit.MaterialProblems();
            ctx.Check(problems.Count == 0, "no default/magenta/missing materials (" + problems.Count + ")");
            foreach (var p in problems) ctx.Log("material problem: " + p);
            int bodies = SceneAudit.ActiveRigidbodies();
            ctx.Check(bodies < 80, "active rigidbodies < 80 (" + bodies + ")");
            ctx.Check(LevelMark.Exists("exit") && LevelMark.Exists("checkpoint.4"), "exit + 4 checkpoints marked");
            yield return ctx.Wait(6.5f);
            yield return Views(ctx, true);
        }

        /// <summary>Fixed camera views of every section (normal view, then IQ view with the Brainiac active).</summary>
        static IEnumerator Views(DevPlayContext ctx, bool iqToo)
        {
            var cam = Camera.main;
            if (cam == null) { ctx.Log("no main camera for views"); yield break; }
            ctx.Shot("a0_spawn_gameplay");
            var follow = cam.GetComponent<ShiftersCamera>();
            if (follow != null) follow.enabled = false;
            var views = new (string name, Vector3 pos, Vector3 look)[]
            {
                ("a1_overview", new Vector3(46f, 52f, 20f), new Vector3(-4f, 0f, 92f)),
                ("a2_s0_s1", new Vector3(-9f, 7f, -9f), new Vector3(3f, 1.5f, 11f)),
                ("a3_s1_ledge", new Vector3(1.5f, 4.2f, 3.5f), new Vector3(6f, 2.2f, 12.5f)),
                ("a4_s2_path", new Vector3(-6f, 9f, 18f), new Vector3(5f, 0f, 36f)),
                ("a5_s3_room", new Vector3(-4f, 9.5f, 45.5f), new Vector3(-4f, 1.5f, 60f)),
                ("a6_s4_logic", new Vector3(-4f, 10f, 78.5f), new Vector3(-4f, 0.5f, 94f)),
                ("a7_s5_stage1", new Vector3(-4f, 8f, 108f), new Vector3(-4f, 0.5f, 124f)),
                ("a8_s5_stage2", new Vector3(-9f, 7f, 131f), new Vector3(-3f, 0.5f, 152f)),
                ("a9_s5_final", new Vector3(-4f, 6.5f, 156f), new Vector3(-4f, 2.5f, 174f)),
            };
            foreach (var v in views) { cam.transform.SetPositionAndRotation(v.pos, Quaternion.LookRotation(v.look - v.pos)); yield return null; ctx.Shot(v.name); }
            if (iqToo && CharacterManager.Instance != null)
            {
                ShifterCharacter brain = null;
                foreach (var c in ShifterCharacter.All) if (c != null && c.Archetype == Archetype.Brainiac) brain = c;
                if (brain != null)
                {
                    CharacterManager.Instance.SwitchTo(brain.Slot, true);
                    yield return ctx.Wait(2.0f);
                    if (follow != null) follow.enabled = false;
                    foreach (var v in views)
                    {
                        if (v.name != "a2_s0_s1" && v.name != "a4_s2_path" && v.name != "a5_s3_room" && v.name != "a6_s4_logic") continue;
                        cam.transform.SetPositionAndRotation(v.pos, Quaternion.LookRotation(v.look - v.pos));
                        yield return null;
                        ctx.Shot("iq_" + v.name);
                    }
                    ctx.Check(HiddenPathTile.All.Count > 10, "hologram tiles exist (" + HiddenPathTile.All.Count + ")");
                    int visible = 0;
                    foreach (var t in HiddenPathTile.All) if (t.IsVisible) visible++;
                    ctx.Check(visible > 10, "hologram tiles visible in IQ view (" + visible + ")");
                }
            }
            if (follow != null) follow.enabled = true;
            yield return ctx.Wait(0.5f);
        }
    }
}
