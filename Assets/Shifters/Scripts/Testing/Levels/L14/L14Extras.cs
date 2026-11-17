// =====================================================================================
//  SHIFTERS - L14Extras.cs   [SHIFT 14 - finale extras (LEVELS2 SHIFT 14 "Finale extras")]
//  RunExtras: static scan (no KillZone / hazard / enemy / boss anywhere) -> Restart Checkpoint at C3 (the vote is
//  restored: charges done, words landed, MUTE latched) and the team finishes the chapter from there -> a fresh
//  level, Restart Checkpoint at C2 (the defense restarts the filibuster), 25 s idle on the C2 ring while every word
//  falls (nobody Downed / Captured / hurt, the polite words never land on anyone), then the team finishes from C2.
//  The C3 run also idles 120 s with one intern seated alone on the bench (no timer, nothing breaks) and, after Done,
//  checks that the closed bench seats nobody (E and a scripted SeatCharacter: the old post-sunrise softlock).
//  Run: devplay.sh <proj> l14_extras -devScene Assets/Shifters/Scenes/L14_AllInFavor.unity
//       -devSetup Shifters.L14Extras.RunExtras -devSeconds 600
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L14Extras
    {
        static L14Hearing H => L14Hearing.Instance;
        static int CI => CheckpointManager.Instance != null ? CheckpointManager.Instance.CurrentIndex : -1;

        static readonly string[] Forbidden = { "KillZone", "HazardZone", "Hazard", "EnemyBase", "BossBase" };

        static IEnumerator WaitNewHearing(DevPlayContext ctx, L14Hearing before)
        {
            yield return ctx.Wait(1.5f);
            for (float t = 0f; t < 20f && (H == null || H == before); t += Time.unscaledDeltaTime) yield return null;
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(0.5f);
        }

        static IEnumerator RestartAt(DevPlayContext ctx, int cp)
        {
            var before = H;
            bool ok = CheckpointManager.ActivateFromScript(cp);
            ctx.Check(ok && CI == cp, "checkpoint C" + cp + " activated for the restore test (CI " + CI + ")");
            GameSession.Instance.RestartFromCheckpoint();
            yield return WaitNewHearing(ctx, before);
        }

        public static IEnumerator RunExtras(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            // ---- static scan
            int bad = 0; string names = "";
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null) continue;
                for (var t = mb.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                    if (System.Array.IndexOf(Forbidden, t.Name) >= 0) { bad++; if (names.Length < 200) names += mb.name + ":" + t.Name + " "; break; }
            }
            ctx.Check(bad == 0, "static scan: no KillZone / Hazard / Enemy / Boss in the finale (" + bad + " found " + names + ")");

            var opts = L14Solution.Options();
            // ---- C3 restore: the vote
            yield return RestartAt(ctx, 3);
            ctx.Check(H != null && H.Phase == L14Hearing.HearingPhase.Vote && H.Muted && H.Evidence == 3 && H.WordLanded("POPULAR") && CI == 3,
                "Restart Checkpoint C3 resumes THE VOTE (phase " + (H != null ? H.Phase.ToString() : "none") + ", evidence " + (H != null ? H.Evidence : -1) + ")");
            L14Solution.BenchIdleSeconds = 120f; L14Solution.ScriptedSeatCheck = true;
            yield return TeamBot.Run(ctx, b => L14Solution.Stages(b, 3), opts, "L14fromC3");
            L14Solution.BenchIdleSeconds = 0f; L14Solution.ScriptedSeatCheck = false;

            // ---- fresh level, C2 restore: the defense / filibuster
            var prev = H;
            GameSession.Instance.RestartLevel();
            yield return WaitNewHearing(ctx, prev);
            yield return RestartAt(ctx, 2);
            ctx.Check(H != null && (H.Phase == L14Hearing.HearingPhase.Defense || H.Phase == L14Hearing.HearingPhase.Filibuster) && H.Evidence == 3 && CI == 2,
                "Restart Checkpoint C2 resumes THE DEFENSE (phase " + (H != null ? H.Phase.ToString() : "none") + ")");
            yield return TeamBot.Run(ctx, Idle, opts, "L14idleC2");
            yield return TeamBot.Run(ctx, b => L14Solution.Stages(b, 2), opts, "L14fromC2");
        }

        /// <summary>25 s idle on the C2 ring while the filibuster drops every word: nobody hurt, nobody displaced.</summary>
        static IEnumerator Idle(TeamBot bot)
        {
            var start = new Vector3[bot.Team.Count];
            for (int i = 0; i < bot.Team.Count; i++) start[i] = bot.Team[i].transform.position;
            yield return bot.Wait(25f);
            yield return bot.Expect(() => H.WordLanded("AND2") && H.WordLanded("POPULAR"), "every stair word + POPULAR landed during the idle", 0f);
            for (int i = 0; i < bot.Team.Count; i++)
            {
                var c = bot.Team[i]; float moved = Vector3.Distance(start[i], c.transform.position);
                yield return bot.Expect(() => c.IsAlive && c.Condition == CharacterCondition.Normal && c.State == CharacterState.Alive && c.Health >= c.MaxHealth - 0.01f,
                    c.DisplayName + " unharmed after 25 s idle on C2 (moved " + moved.ToString("0.00") + " m)", 0f);
            }
        }
    }
}
