using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Runs every PUZZLES DevPlay block in one Unity session, isolating exceptions per block so one broken block
    /// doesn't hide the results of the others.
    /// Run: devplay.sh &lt;proj&gt; puzzles_everything -devSetup Shifters.PuzzleTestRunner.Everything -devSeconds 30
    /// </summary>
    public static class PuzzleTestRunner
    {
        public static IEnumerator Everything(DevPlayContext ctx)
        {
            yield return Guard(ctx, "Logic", PuzzleTests.Logic(ctx));
            yield return Guard(ctx, "Characters", PuzzleTests.Characters(ctx));
            yield return Guard(ctx, "Motor", PuzzleMotorTests.Run(ctx));
            yield return Guard(ctx, "Extra", PuzzleExtraTests.Run(ctx));
            yield return Guard(ctx, "Zoo", PuzzleZoo.Run(ctx));
            yield return Guard(ctx, "Campaign", PuzzleCampaignTests.Run(ctx));
            yield return Guard(ctx, "CampaignGallery", PuzzleCampaignTests.Gallery(ctx));
        }

        /// <summary>Behaviour only (no zoo screenshots).</summary>
        public static IEnumerator Behaviour(DevPlayContext ctx)
        {
            yield return Guard(ctx, "Logic", PuzzleTests.Logic(ctx));
            yield return Guard(ctx, "Characters", PuzzleTests.Characters(ctx));
            yield return Guard(ctx, "Motor", PuzzleMotorTests.Run(ctx));
            yield return Guard(ctx, "Extra", PuzzleExtraTests.Run(ctx));
            yield return Guard(ctx, "Campaign", PuzzleCampaignTests.Run(ctx));
        }

        /// <summary>Steps a routine (flattening nested IEnumerators) and turns an exception into a failed check.</summary>
        public static IEnumerator Guard(DevPlayContext ctx, string name, IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            ctx.Log("BLOCK " + name + " start (t=" + Time.time.ToString("0.0") + ")");
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                object current;
                try
                {
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    current = top.Current;
                }
                catch (Exception e)
                {
                    ctx.Check(false, "BLOCK " + name + " threw " + e.GetType().Name + ": " + e.Message);
                    Debug.LogException(e);
                    yield break;
                }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            ctx.Log("BLOCK " + name + " end (t=" + Time.time.ToString("0.0") + ")");
        }
    }
}
