// =====================================================================================
//  SHIFTERS - TeamBot.Levels.cs   [BOTS module]
//  Level-run helpers:
//    RunLevel        standard L<n>Solution.Run body for a BUILT scene (-botSeed N / -botSeeds a,b,c)
//    RunRuntimeLevel the same solution on a level built at RUNTIME by LevelRuntimeBuilder (the editor
//                    builder's code path, no scene file needed) - fast iteration for level designers
//    EnterExit       walks the whole team into the LevelExit cab (lanes through the door, slots clear
//                    of the console) and verifies the exit counts everybody inside
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed partial class TeamBot
    {
        // ------------------------------------------------------------------ level runs
        /// <summary>Standard body of <c>L&lt;n&gt;Solution.Run</c> on a built scene: with <c>-botSeeds a,b,c</c> the solution
        /// runs once per seed (scene reloaded with each team), otherwise once (with <c>-botSeed N</c> applied).</summary>
        public static IEnumerator RunLevel(DevPlayContext ctx, Func<TeamBot, IEnumerator> solution, string label, BotOptions options = null)
        {
            if (!string.IsNullOrEmpty(BotArgs.Str("-botSeeds", "")) || !string.IsNullOrEmpty(BotArgs.Str("-botTeams", "")))
                yield return RunSeeds(ctx, BotArgs.Seeds(), solution, null, options, label);
            else yield return Run(ctx, solution, options, label);
        }

        /// <summary>Run a solution on a level built at RUNTIME, once per seed: tear down the previous level + team,
        /// ForceTeam(seed), <c>LevelRuntimeBuilder.BuildInCurrentScene(levelIndex)</c>, wait for the LevelManager to spawn
        /// the team, run. levelIndex = LevelCatalog index (0 = L1 ... 4 = L5) or L0_Sandbox.Index. Use it from a DevPlay
        /// setup WITHOUT -devScene. seeds: default -botSeeds / -botSeed, else 11, 22, 33.</summary>
        public static IEnumerator RunRuntimeLevel(DevPlayContext ctx, int levelIndex, Func<TeamBot, IEnumerator> solution, string label, int[] seeds = null, BotOptions options = null)
        {
            seeds = seeds ?? BotArgs.Seeds(11, 22, 33);
            yield return RunSeeds(ctx, seeds, solution, seed => BuildRuntimeLevel(ctx, levelIndex, seed), options, label);
        }

        /// <summary>Tear down any level + team, ForceTeam(seed), build the level at runtime and wait for the team.</summary>
        public static IEnumerator BuildRuntimeLevel(DevPlayContext ctx, int levelIndex, int seed)
        {
            TearDownLevel();
            yield return null;
            yield return null;
            ForceTeam(seed);
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(levelIndex);
            if (lctx == null) { ctx?.Check(false, "[BOT] no ILevelBuilder with LevelIndex " + levelIndex); yield break; }
            if (lctx.Failed) ctx?.Check(false, "[BOT] runtime build of level " + levelIndex + " failed:\n" + lctx.Report());
            yield return WaitForTeam(ctx);
        }

        /// <summary>Destroy the LevelManager (it owns CharacterManager + team), a fallback bootstrap and any stray
        /// characters, so the next build spawns a fresh team.</summary>
        public static void TearDownLevel()
        {
            var lm = BotReflect.FindFirst("LevelManager");
            if (lm != null) UnityEngine.Object.Destroy(lm.gameObject);
            var fb = BotReflect.FindFirst("LevelBootstrapFallback");
            if (fb != null) UnityEngine.Object.Destroy(fb.gameObject);
            if (CharacterManager.Instance != null) UnityEngine.Object.Destroy(CharacterManager.Instance.gameObject);
            for (int i = 0; i < ShifterCharacter.All.Count; i++)
                if (ShifterCharacter.All[i] != null) UnityEngine.Object.Destroy(ShifterCharacter.All[i].gameObject);
        }

        // ------------------------------------------------------------------ exit
        /// <summary>The LevelExit on / under / above the target; with no target the first LevelExit in the scene.</summary>
        public static MonoBehaviour FindExit(BotTarget exit = default)
        {
            var type = BotReflect.FindType("LevelExit");
            if (type == null) return null;
            var t = exit.Transform;
            if (t != null)
            {
                var c = t.GetComponentInChildren(type, true);
                if (c == null) c = t.GetComponentInParent(type, true);
                if (c is MonoBehaviour mb) return mb;
            }
            return BotReflect.FindFirst("LevelExit");
        }

        static bool TargetIsExit(BotTarget t)
        {
            var type = BotReflect.FindType("LevelExit");
            var tr = t.Transform;
            return type != null && tr != null && (tr.GetComponentInChildren(type, true) != null || tr.GetComponentInParent(type, true) != null);
        }

        static bool ExitBusy(MonoBehaviour ex) =>
            ex != null && ((BotReflect.TryGet<bool>(ex, "IsCompleting", out var a) && a) || (BotReflect.TryGet<bool>(ex, "IsCompleted", out var b) && b));

        static string ExitCount(MonoBehaviour ex, out bool all)
        {
            all = false;
            if (ex == null) return "no exit";
            if (!BotReflect.TryGet<int>(ex, "AlivePresent", out int present) || !BotReflect.TryGet<int>(ex, "AliveTotal", out int total)) return "count unknown";
            all = total > 0 && present >= total;
            string missing = "";
            if (!all && BotReflect.TryCall(ex, "MissingAliveNames", out var r) && r is List<string> names && names.Count > 0) missing = " missing: " + string.Join(", ", names);
            return present + "/" + total + " inside" + missing;
        }

        /// <summary>Walk every alive character into the LevelExit cab (LevelKit.Exit: 3.2 m door, console on the right
        /// wall): each takes a lane through the door, ordered by its current side so nobody crosses, and stops in a slot
        /// inside the exit zone. Then waits until the exit counts everybody (it completes ~0.35 s later; follow with
        /// ExpectLevelComplete). Walks straight lines: bring the team near the exit first if props are in the way.
        /// exit = mark/object of the LevelExit (default: the first LevelExit in the scene).</summary>
        public IEnumerator EnterExit(BotTarget exit = default, bool sprint = false, float timeout = 30f)
        {
            var s = Begin("EnterExit", null, exit.IsValid ? exit.Describe() : "LevelExit");
            if (s == null) yield break;
            var ex = FindExit(exit);
            if (ex == null) { FailStep(s, "no LevelExit in the scene" + (exit.IsValid ? " (target " + exit.Describe() + ")" : "")); yield break; }
            var et = ex.transform;
            Vector3 zc = BotReflect.TryGet<Vector3>(ex, "ZoneCenter", out var zcv) ? zcv : new Vector3(0f, 2f, 2.2f);
            Vector3 zs = BotReflect.TryGet<Vector3>(ex, "ZoneSize", out var zsv) ? zsv : new Vector3(4.2f, 4f, 4f);
            var alive = Others();
            alive.Sort((p, q) => et.InverseTransformPoint(p.Feet).x.CompareTo(et.InverseTransformPoint(q.Feet).x));
            int n = alive.Count;
            float spacing = n > 1 ? Mathf.Min(0.95f, (zs.x - 1.2f) / (n - 1)) : 0f;
            float door = zc.z - zs.z * 0.5f;
            var lanes = new IEnumerator[n];
            for (int i = 0; i < n; i++)
            {
                float lx = (i - (n - 1) * 0.5f) * spacing;
                bool middle = (n % 2 == 1) && i == n / 2;
                float lz = middle ? zc.z : zc.z + zs.z * 0.22f;   // outer slots deeper: clear of the side console
                Vector3 approach = et.TransformPoint(new Vector3(lx, 0f, door - 1.8f));
                Vector3 slot = et.TransformPoint(new Vector3(lx, 0f, lz));
                lanes[i] = ExitLane(alive[i], et, door, approach, slot, sprint, timeout, s);
            }
            yield return Parallel(lanes);
            if (s.Ended) yield break;
            float t0 = Now;
            bool all = false;
            string count = "";
            while (Now - t0 < 3f)
            {
                count = ExitCount(ex, out all);
                if (all || ExitBusy(ex)) break;
                yield return null;
            }
            if (!all && !ExitBusy(ex)) { FailStep(s, "exit zone counts " + count); yield break; }
            End(s, true, ExitBusy(ex) ? "exit completing" : count);
        }

        IEnumerator ExitLane(ShifterCharacter c, Transform et, float door, Vector3 approach, Vector3 slot, bool sprint, float timeout, BotStep s)
        {
            // the approach point in front of the door is skipped only when already in the doorway or inside
            var local = et.InverseTransformPoint(c.Feet);
            bool inDoorway = local.z > door - 1.2f && Mathf.Abs(local.x) < 1.5f;
            if (!inDoorway)
            {
                yield return WalkCore(c, approach, sprint, 0.6f, timeout, false, s);
                if (s.Ended) yield break;
            }
            yield return WalkCore(c, slot, false, 0.35f, timeout, true, s);
        }
    }
}
