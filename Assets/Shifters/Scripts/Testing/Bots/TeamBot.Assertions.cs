// =====================================================================================
//  SHIFTERS - TeamBot.Assertions.cs   [BOTS module]
//  Expectations used by solutions. Each records a ctx.Check; a failed expectation fails the run
//  (state dump + diagnostic screenshots). Optional 'within' seconds = keep checking until true.
// =====================================================================================
using System;
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public sealed partial class TeamBot
    {
        IEnumerator ExpectCore(string what, Func<bool> cond, Func<string> detail, float within, ShifterCharacter focus)
        {
            if (Failed && !Options.ContinueAfterFailure) yield break;
            float t0 = Now;
            bool ok = false;
            while (true)
            {
                try { ok = cond(); } catch (Exception e) { ok = false; Warn("expectation threw: " + e.Message); }
                if (ok || Now - t0 >= within) break;
                yield return null;
            }
            string d = detail != null ? detail() : "";
            if (ok) { Ctx?.Check(true, "[BOT] " + what + (string.IsNullOrEmpty(d) ? "" : " (" + d + ")")); Steps.Add(new BotStep { Action = "Expect", Detail = what, Start = t0, Seconds = Now - t0, Outcome = "OK" }); }
            else
            {
                Steps.Add(new BotStep { Action = "Expect", Detail = what, Start = t0, Seconds = Now - t0, Outcome = "FAIL", Note = d });
                Fail("EXPECT " + what + " FAILED" + (within > 0f ? " within " + within + " s" : "") + (string.IsNullOrEmpty(d) ? "" : ": " + d), focus);
            }
        }

        /// <summary>Generic expectation.</summary>
        public IEnumerator Expect(Func<bool> cond, string what, float within = 0f, ShifterCharacter focus = null) => ExpectCore(what, cond, null, within, focus);

        public IEnumerator ExpectState(ShifterCharacter c, CharacterState state, float within = 0f) =>
            ExpectCore(BotFmt.Name(c) + " state " + state, () => c != null && c.State == state, () => c != null ? "is " + c.State + " " + c.LastCause : "null", within, c);

        public IEnumerator ExpectCondition(ShifterCharacter c, CharacterCondition condition, float within = 0f) =>
            ExpectCore(BotFmt.Name(c) + " condition " + condition, () => c != null && c.Condition == condition, () => c != null ? "is " + c.Condition : "null", within, c);

        /// <summary>Character within radius (planar) and maxDy (vertical) of the target.</summary>
        public IEnumerator ExpectNear(ShifterCharacter c, BotTarget target, float radius = 1.2f, float within = 0f, float maxDy = 0.8f) =>
            ExpectCore(BotFmt.Name(c) + " near " + target.Describe(),
                () => c != null && Flat(c.Feet - target.Position).magnitude <= radius && Mathf.Abs(c.Feet.y - target.Position.y) <= maxDy,
                () => c != null ? "feet " + BotFmt.V(c.Feet) + " target " + BotFmt.V(target.Position) : "null", within, c);

        /// <summary>A receiver is Powered / a source IsOn (or a bool Powered/IsOn/IsOpen/IsSolved/IsComplete member).</summary>
        public IEnumerator ExpectPowered(BotTarget receiverOrSource, bool powered = true, float within = 1f) =>
            ExpectCore(receiverOrSource.Describe() + (powered ? " powered" : " NOT powered"),
                () => BotTargets.ReadPowered(receiverOrSource) == powered,
                () => { var v = BotTargets.ReadPowered(receiverOrSource); return v.HasValue ? "is " + (v.Value ? "on" : "off") : "no signal component found"; },
                within, null);

        /// <summary>Non-yielding read for WaitUntil conditions.</summary>
        public static bool IsPowered(BotTarget receiverOrSource) => BotTargets.ReadPowered(receiverOrSource) == true;

        /// <summary>Wait for GameEvents.LevelCompleted (LevelExit) or a LevelExit-like component reporting completion.</summary>
        public IEnumerator ExpectLevelComplete(float timeout = 10f) =>
            ExpectCore("level complete", () => LevelCompleted || ExitReportsComplete(),
                () => LevelCompleted ? "survivors " + CompletedResult.Survivors + ", time " + CompletedResult.Time.ToString("0.0") + " s"
                                     : "no LevelCompleted event; exit " + ExitCount(FindExit(), out _), timeout, null);

        static bool ExitReportsComplete()
        {
            var exit = BotReflect.FindFirst("LevelExit");
            if (exit == null) return false;
            return (BotReflect.TryGet<bool>(exit, "IsCompleted", out var c) && c)
                || (BotReflect.TryGet<bool>(exit, "IsComplete", out var a) && a) || (BotReflect.TryGet<bool>(exit, "Completed", out var b) && b);
        }
    }
}
