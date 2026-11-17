// =====================================================================================
//  SHIFTERS - L11Solution.cs   [SHIFT 10 - THE ME-MORIAL PARK]  TeamBot solution (no debug assists)
//  Characters by ARCHETYPE only; StrictPlayerThrows (no throws are needed anyway). Mirrors LEVELS2 "TeamBot proof":
//    S1  Brute -> plate A, Sprinter -> plate B, Brainiac -> plate C (all at once) -> huddle gate latches -> C1
//    S2  three graves: everyone jumps in parallel lanes (x -1.5 / 0 / 1.5) over the crumbling FRESH SOIL;
//        gather at the bridge edge (whole team in the ghost watch zone) -> ghost bridge extends -> cross in lanes,
//        hold at the far bank until all are over -> C2
//    S3  walk to the mausoleum (C09 fires in the doorway; story-ON waits it out) -> exit cab
//  Run: $SP/tools/run_level_test.sh <project> 11 1,2,3,MIN,MAX     (BOT_STORY=1 for the story-ON pass)
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L11Solution
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            var seeds = BotArgs.Seeds();
            if (seeds != null && seeds.Length > 1) return TeamBot.RunSeeds(ctx, seeds, Solve, null, Options(), "L11");
            return TeamBot.Run(ctx, Solve, Options(), "L11");
        }

        /// <summary>Same solution on the level built at runtime by L11Builder (no scene needed).</summary>
        public static IEnumerator RunRuntime(DevPlayContext ctx) => TeamBot.RunRuntimeLevel(ctx, 10, Solve, "L11rt", BotArgs.Seeds(), Options());

        public static BotOptions Options() => new BotOptions { DiagnosticShots = true, Verbose = true, RunTimeout = 300f, StrictPlayerThrows = true };

        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("L11 team: " + bot.TeamSummary());
            var who = new[] { brute, spr, brain };
            s_beats.Clear();
            GameEvents.StoryBeat -= RecordBeat; GameEvents.StoryBeat += RecordBeat;

            // ---------------- S1 THE GATES: "STAND TOGETHER"
            yield return bot.Parallel(bot.WalkTo(brute, "s1.A", true, 0.45f), bot.WalkTo(spr, "s1.B", true, 0.45f), bot.WalkTo(brain, "s1.C", true, 0.45f));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1.gate", true, 3f); if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.1", true); if (bot.Failed) yield break;
            yield return bot.Expect(() => ActiveCheckpoint() >= 1, "checkpoint 1 active", 3f); if (bot.Failed) yield break;

            // ---------------- S2 THE GRAVES: three jumps in lanes
            for (int g = 1; g <= 3; g++)
            {
                yield return bot.Parallel(
                    bot.JumpGap(who[0], "s2.g" + g + ".takeoff.L", "s2.g" + g + ".landing.L", true, 1.5f),
                    bot.JumpGap(who[1], "s2.g" + g + ".takeoff.C", "s2.g" + g + ".landing.C", true, 1.5f),
                    bot.JumpGap(who[2], "s2.g" + g + ".takeoff.R", "s2.g" + g + ".landing.R", true, 1.5f));
                if (bot.Failed) yield break;
            }

            // ---------------- the ghost bridge
            yield return bot.GatherAt("s2.bridgeNear", true); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s2.ghostBridge", true, 4f); if (bot.Failed) yield break;
            var bridge = LevelMark.Find<RetractableBridge>("s2.ghostBridge");
            yield return bot.WaitUntil(() => bridge != null && bridge.Position01 > 0.98f, 6f, "ghost bridge fully extended"); if (bot.Failed) yield break;
            yield return bot.Parallel(
                bot.WalkPath(who[0], false, "s2.lane.L.near", "s2.farBank.L"),
                bot.WalkPath(who[1], false, "s2.lane.C.near", "s2.farBank.C"),
                bot.WalkPath(who[2], false, "s2.lane.R.near", "s2.farBank.R"));
            if (bot.Failed) yield break;
            yield return bot.Expect(() => AllBeyond(who, L11Builder.GapFar + 0.3f), "whole team on the far bank", 3f); if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.2", true); if (bot.Failed) yield break;
            yield return bot.Expect(() => ActiveCheckpoint() >= 2, "checkpoint 2 active", 3f); if (bot.Failed) yield break;

            // ---------------- S3 THE MAUSOLEUM -> exit
            yield return bot.GatherAt("s3.door", true); if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, new Vector3(0f, 0f, L11Builder.MausoleumZ + 1.4f), true, 0.6f); if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => StoryPlayer.Instance == null || !StoryPlayer.Instance.IsBlocking, 60f, "C09 finished (story ON)"); if (bot.Failed) yield break;
            yield return bot.EnterExit("exit", true, 30f);
            GameEvents.StoryBeat -= RecordBeat;
            if (bot.Failed) yield break;
            // DoD 3/4: the chapter's beats fired (ch10.start by the spawn trigger before Solve began; ch10.ownGrave needs a
            // campaign LostLog and ch10.ghosts.alone a split team: both proven in L11Shots / by code review)
            var st = LevelMark.Find<ScriptTrigger>("s0.start");
            yield return bot.Expect(() => st != null && st.HasFired, "beat ch10.start fired (spawn trigger)", 1f);
            foreach (var id in new[] { "ch10.gate", "ch10.ghosts", "ch10.mausoleum" })
            {
                string b = id;
                yield return bot.Expect(() => s_beats.Contains(b), "beat " + b + " fired", 1f);
            }
            bot.Log("beats fired: " + string.Join(", ", s_beats));
        }

        static readonly List<string> s_beats = new List<string>();
        static void RecordBeat(string id) { if (!string.IsNullOrEmpty(id) && id.StartsWith("ch10.") && !s_beats.Contains(id)) s_beats.Add(id); }

        static bool AllBeyond(ShifterCharacter[] who, float z)
        {
            foreach (var c in who) if (c != null && c.IsAlive && c.transform.position.z < z) return false;
            return true;
        }

        static int ActiveCheckpoint()
        {
            var cm = CheckpointManager.Instance;
            return cm != null ? cm.CurrentIndex : -1;
        }
    }
}
