// =====================================================================================
//  SHIFTERS - TeamBot.Team.cs   [BOTS module]
//  Switching, AI modes, follow-the-leader, deterministic teams (ForceTeam / -botSeed) and
//  multi-seed runs (the same archetype-based solution must pass for every seed).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public sealed partial class TeamBot
    {
        // ------------------------------------------------------------------ switching
        /// <summary>Switch control through CharacterManager.SwitchTo and wait for the camera transition.</summary>
        public IEnumerator Switch(ShifterCharacter c, float timeout = 4f)
        {
            var s = Begin("Switch", c, null);
            if (!Ready(s, c)) yield break;
            yield return SwitchCore(c, timeout, s);
            End(s);
        }

        public IEnumerator Switch(int slot) => Switch(CharacterManager.Instance != null ? CharacterManager.Instance.Get(slot) : null);
        public IEnumerator Switch(Archetype a) => Switch(Get(a));

        IEnumerator SwitchCore(ShifterCharacter c, float timeout, BotStep s)
        {
            var cm = CharacterManager.Instance;
            if (cm == null) { FailStep(s, "no CharacterManager in the scene", c); yield break; }
            if (cm.Active == c) yield break;
            if (!cm.SwitchTo(c.Slot)) { FailStep(s, "SwitchTo(" + c.Slot + ") refused (state " + c.State + ")", c); yield break; }
            float t0 = Now;
            yield return null;
            while (Now - t0 < timeout && (cm.IsTransitioning || (ShiftersCamera.Instance != null && ShiftersCamera.Instance.IsTransitioning)))
                yield return null;
            if (cm.Active != c) { FailStep(s, "active is " + BotFmt.Name(cm.Active) + " after switching", c); yield break; }
            if (cm.IsTransitioning) Warn("switch transition still running after " + timeout + " s");
            TakeControl(c);
        }

        /// <summary>Set the inactive teammates' AI mode (CharacterManager.SetAIMode).</summary>
        public IEnumerator SetAIMode(TeamAIMode mode)
        {
            var s = Begin("SetAIMode", null, mode.ToString());
            if (s == null) yield break;
            var cm = CharacterManager.Instance;
            if (cm == null) { FailStep(s, "no CharacterManager"); yield break; }
            cm.SetAIMode(mode);
            yield return null;
            End(s, cm.AIMode == mode, cm.AIMode == mode ? null : "AIMode is " + cm.AIMode);
            if (cm.AIMode != mode) Fail("SetAIMode(" + mode + ") did not stick");
        }

        /// <summary>Real team AI test: switch to the leader, set Follow, hand the followers back to their AI,
        /// walk the leader along the path, then verify every follower arrived within arriveRadius.</summary>
        public IEnumerator FollowLeader(ShifterCharacter leader, IList<BotTarget> path, bool sprint = false, float arriveRadius = 4.5f, float timeout = 40f)
        {
            var s = Begin("FollowLeader", leader, path.Count + " pts");
            if (!Ready(s, leader)) yield break;
            yield return SwitchCore(leader, 4f, s);
            if (s.Ended) yield break;
            var cm = CharacterManager.Instance;
            cm.SetAIMode(TeamAIMode.Follow);
            var followers = new List<ShifterCharacter>();
            foreach (var c in Others(leader)) if (c.CanAct) { followers.Add(c); ReleaseControl(c); }
            try
            {
                for (int i = 0; i < path.Count; i++)
                {
                    yield return WalkCore(leader, path[i], sprint, i == path.Count - 1 ? 0.6f : 0.9f, 25f, i == path.Count - 1, s);
                    if (s.Ended) yield break;
                }
                float t0 = Now;
                string missing = "";
                while (Now - t0 < timeout)
                {
                    missing = "";
                    foreach (var f in followers)
                    {
                        if (f == null || !f.IsAlive) { missing += " " + (f != null ? f.DisplayName + "(" + f.State + ")" : "?"); continue; }
                        float d = Flat(f.Feet - leader.Feet).magnitude;
                        if (d > arriveRadius || Mathf.Abs(f.Feet.y - leader.Feet.y) > 1.5f) missing += " " + f.DisplayName + "@" + BotFmt.F(d) + "m";
                    }
                    if (missing.Length == 0) break;
                    yield return null;
                }
                if (missing.Length > 0) { FailStep(s, "followers did not arrive:" + missing, leader); yield break; }
                End(s, true, followers.Count + " follower(s) arrived");
            }
            finally
            {
                if (cm != null) cm.SetAIMode(TeamAIMode.Stay);
                foreach (var f in followers) TakeControl(f);
            }
        }

        // ------------------------------------------------------------------ deterministic teams
        /// <summary>Start a new run with this seed (GameSession.NewRun). Call BEFORE the level spawns the team
        /// (or use ReloadWithSeed / the -botSeed argument, applied automatically after scene load).</summary>
        /// <remarks>seed TeamBot.SeedMin / SeedMax (what BotArgs returns for MIN / MAX) builds the extreme team.</remarks>
        public static void ForceTeam(int seed)
        {
            if (seed == SeedMin) { ForceTeamExtreme(ExtremeTeam.Min); return; }
            if (seed == SeedMax) { ForceTeamExtreme(ExtremeTeam.Max); return; }
            var gs = GameSession.Instance;
            if (gs == null) { Debug.LogWarning("[DEVPLAY] [BOT] ForceTeam(" + seed + "): no GameSession"); return; }
            gs.NewRun(seed);
            ForcedExtreme = ExtremeTeam.None;
            Debug.Log("[DEVPLAY] [BOT] ForceTeam seed=" + seed + " -> " + DescribeTeam(gs.Team));
        }

        public static string DescribeTeam(TeamData t)
        {
            if (t == null || t.Members == null) return "(no team)";
            var parts = new List<string>();
            foreach (var m in t.Members)
                if (m != null) parts.Add(m.DisplayName + " " + m.Archetype + " S" + m.Stats.Strength + "/P" + m.Stats.Speed + "/I" + m.Stats.IQ + " " + m.Mass.ToString("0") + "kg" + (m.HasWeapon ? " GUN" : ""));
            return string.Join(" | ", parts);
        }

#if UNITY_EDITOR
        /// <summary>-botSeed N on the command line => ForceTeam(N) right after the first scene loaded (before Start).
        /// Editor only (bots run through DevPlay): player builds - WebGL included - never read command-line args here.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplySeedArgument()
        {
            int seed = BotArgs.TeamSeed("-botSeed", 0);
            if (seed == 0)
            {
                var list = BotArgs.Seeds();
                if (list != null && list.Length > 0) seed = list[0];   // first team of -botTeams / -botSeeds: no extra reload
            }
            if (seed != 0) ForceTeam(seed);
            if (BotArgs.Has("-botSeed") || BotArgs.Has("-botSeeds") || BotArgs.Has("-botTeams")) BotStory.ApplyCommandLine();
        }
#endif

        /// <summary>ForceTeam(seed) and reload the active scene (scene-based levels) so the level spawns that team.</summary>
        public static IEnumerator ReloadWithSeed(DevPlayContext ctx, int seed, float timeout = 20f)
        {
            ForceTeam(seed);
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[DEVPLAY] [BOT] ReloadWithSeed: active scene is unsaved - pass a prepare callback to RunSeeds instead");
                yield break;
            }
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
            yield return null;
            yield return null;
            yield return WaitForTeam(ctx, timeout);
        }

        /// <summary>Run the same solution for several seeds. prepare(seed) must (re)build the level with that team;
        /// default = ReloadWithSeed (scene-based levels). Logs a per-seed summary at the end.</summary>
        public static IEnumerator RunSeeds(DevPlayContext ctx, int[] seeds, Func<TeamBot, IEnumerator> solution, Func<int, IEnumerator> prepare = null, BotOptions options = null, string label = null)
        {
            ctx = ctx ?? DevPlayHarness.Context ?? LooseContext();
            var driver = BotDriver.Ensure();
            driver.HoldHarness(ctx);
            bool prefsPushed = options == null || options.GuardPrefs; if (prefsPushed) BotPrefsGuard.Push(ctx);
            BotStory.Apply(options ?? new BotOptions());
            int first = Results.Count;
            foreach (int seed in seeds)
            {
                ctx.Log("[BOT] ===== SEED " + TeamKey(seed) + " =====");
                if (prepare != null) yield return prepare(seed);
                else yield return ReloadWithSeed(ctx, seed);
                yield return Run(ctx, solution, options, (label ?? "solution") + "#" + TeamKey(seed));
            }
            int pass = 0;
            for (int i = first; i < Results.Count; i++)
            {
                var r = Results[i];
                if (r.Passed) pass++;
                ctx.Log("[BOT] SEEDS " + r.Label + " seed=" + r.Seed + " team=" + r.Team + (r.Passed ? " PASS " : " FAIL ") + r.Seconds.ToString("0.0") + "s"
                        + (r.UsedDebugAssists ? " (DEBUG ASSISTS)" : "") + (r.Passed ? "" : " - " + r.FailReason));
            }
            var tl = new System.Text.StringBuilder("[BOT] TIME TABLE " + (label ?? "solution") + ":");
            for (int i = first; i < Results.Count; i++) tl.Append(' ').Append(Results[i].Team).Append('=').Append(Results[i].Seconds.ToString("0.0")).Append(Results[i].Passed ? "s" : "s(FAIL)");
            ctx.Log(tl.ToString());
            ctx.Log("[BOT] SEEDS TOTAL " + pass + "/" + (Results.Count - first) + " passed");
            BotStory.Restore();
            if (prefsPushed) BotPrefsGuard.Pop(ctx);
            driver.ReleaseHarness(ctx);
        }

        // ------------------------------------------------------------------ debug assists
        /// <summary>DEVELOPMENT ONLY: teleport a character (skip a section). Requires Options.AllowDebugAssists;
        /// logged loudly and the run is flagged as not a valid solution run.</summary>
        public IEnumerator DebugTeleport(ShifterCharacter c, BotTarget position, float yaw = float.NaN)
        {
            var s = Begin("DEBUG_TELEPORT", c, position.Describe());
            if (!Ready(s, c)) yield break;
            if (!Options.AllowDebugAssists) { FailStep(s, "DebugTeleport used but Options.AllowDebugAssists is false (final solution runs must not teleport)", c); yield break; }
            UsedDebugAssists = true;
            Warn("!!! DEBUG ASSIST: teleporting " + c.DisplayName + " to " + position.Describe() + " - NOT A VALID SOLUTION RUN");
            var rot = float.IsNaN(yaw) ? c.transform.rotation : Quaternion.Euler(0f, yaw, 0f);
            if (c.Motor != null) c.Motor.Teleport(position.Position, rot); else c.transform.SetPositionAndRotation(position.Position, rot);
            Stop(c);
            yield return Wait(0.3f);
            End(s, true, "(assist)");
        }
    }
}
