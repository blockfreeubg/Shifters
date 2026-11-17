// =====================================================================================
//  SHIFTERS - EndToEnd.cs   [INTEGRATION - the definitive merged-build test]
//  devplay.sh <proj> e2e -devSetup Shifters.EndToEnd.Run -devSeconds 1
//  Builds L0_Sandbox at runtime (LevelRuntimeBuilder) + runtime annexes, spawns a real team through
//  LevelManager, then exercises every module together through ExternalControl + public APIs, with a
//  screenshot (UI included) per step. PlayerPrefs (shared with the real project's Editor) are restored.
// =====================================================================================
using System.Collections;

namespace Shifters
{
    public static class EndToEnd
    {
        public const int Seed = 4242;

        public static IEnumerator Run(DevPlayContext ctx)
        {
            E2E.Ctx = ctx;
            var prefs = E2EPrefsBackup.Capture();
            E2E.Subscribe();
            try
            {
                yield return EndToEndCourse.Build(ctx);
                yield return EndToEndPhases.Spawn(ctx);
                var mgr = CharacterManager.Instance;
                if (mgr == null || mgr.Team.Count != 3) { ctx.Check(false, "team spawned - aborting the end-to-end run"); yield break; }
                yield return Phase(ctx, "presentation", EndToEndPhases.Presentation(ctx));
                yield return Phase(ctx, "movement+checkpoint", EndToEndPhases.Movement(ctx));
                yield return Phase(ctx, "switching", EndToEndPhases.Switching(ctx));
                yield return Phase(ctx, "time+contexts", EndToEndPhases.TimeAndContexts(ctx));
                yield return Phase(ctx, "grab+throw", EndToEndPhases.GrabThrow(ctx));
                yield return Phase(ctx, "stacking", EndToEndPhases.Stacking(ctx));
                yield return Phase(ctx, "bridge", EndToEndPhases.Bridge(ctx));
                yield return Phase(ctx, "flop", EndToEndPhases.Flop(ctx));
                yield return Phase(ctx, "push block onto plate", EndToEndPhases.PushBlock(ctx));
                yield return Phase(ctx, "plate+door", EndToEndPhases.PlateDoor(ctx));
                yield return Phase(ctx, "terminal", EndToEndPhases.TerminalHack(ctx));
                yield return Phase(ctx, "shoot", EndToEndPhases.Shoot(ctx));
                yield return Phase(ctx, "bot+cell", EndToEndPhases.BotCapture(ctx));
                yield return Phase(ctx, "exit", EndToEndPhases.Exit(ctx));
                ctx.Log("END-TO-END finished: switched=" + E2E.Switched + " banners=" + E2E.Banners + " toasts=" + E2E.Toasts
                        + " prompts=" + E2E.Prompts + " perspective=" + E2E.PerspectiveChanges + " noises=" + E2E.Noises
                        + " checks passed=" + ctx.Result.checksPassed + " failed=" + ctx.Result.checksFailed);
            }
            finally
            {
                E2E.Unsubscribe();
                prefs.Restore();
            }
        }

        static IEnumerator Phase(DevPlayContext ctx, string name, IEnumerator body)
        {
            int failedBefore = ctx.Result.checksFailed;
            ctx.Log("---- PHASE " + name);
            yield return body;
            E2E.TakeControl();
            ctx.Log("---- PHASE " + name + (ctx.Result.checksFailed == failedBefore ? " OK" : " FAILED (" + (ctx.Result.checksFailed - failedBefore) + ")"));
        }
    }
}
