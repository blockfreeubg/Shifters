// =====================================================================================
//  SHIFTERS - L7Solution.cs   [SHIFT 04 - ACT NATURAL]  TeamBot solution (no debug assists)
//  Characters by ARCHETYPE only. The bot plays "like strangers": nobody is ever within 3 m of a
//  teammate while both are inside a camera cone, except in the statue's blind spot.
//    S0  three parallel lanes 4 m apart (x -4 / 0 / 4) under the practice camera Q0
//    S1  Brainiac holds P1 alone; Sprinter, then Brute pass G1 one at a time; Sprinter pulls L1; Brainiac follows
//    S2  Brute waits by the teller gate, Brainiac on the stack spot, Sprinter stacks + climbs the CLIMB SECTION,
//        drops in, presses STAFF GATE; Brute + Brainiac walk in; Brainiac prints the keycard; one at a time
//        through the STAFF DOOR while the others wait far apart (west of T2's range)
//    S3  regroup at C2, exit
//  Story-ON run (BOT_STORY=1): first walks Brute + Sprinter into Q1's cone together (deliberate detection,
//  expects the escort), then continues; checks that every chapter beat id fired.
//  Run: $SP/tools/run_level_test.sh <project> 7 1,2,3,MIN,MAX     (BOT_STORY=1 ... 7 1 for the story run)
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L7Solution
    {
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, Options(), "L7");

        public static BotOptions Options() => new BotOptions { StrictPlayerThrows = true };

        static bool StoryRun => BotArgs.Int("-botStory", 0) != 0;

        public static readonly string[] BeatIds = { "ch04.disguise", "ch04.detected", "ch04.alone", "ch04.keycard", "ch04.onedoor", "ch04.exit" };
        static readonly HashSet<string> _beats = new HashSet<string>();
        static void OnBeat(string id) { if (id != null) _beats.Add(id); }

        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bot.Log("team: " + bot.TeamSummary() + (StoryRun ? "  [STORY RUN: deliberate detection in S1]" : ""));
            _beats.Clear();
            GameEvents.StoryBeat -= OnBeat;
            GameEvents.StoryBeat += OnBeat;
            try
            {
                yield return bot.SetAIMode(TeamAIMode.Stay); if (bot.Failed) yield break;
                yield return S0(bot, brute, spr, brain); if (bot.Failed) yield break;
                if (StoryRun) { yield return Detection(bot, brute, spr); if (bot.Failed) yield break; }
                yield return S1(bot, brute, spr, brain); if (bot.Failed) yield break;
                yield return S2(bot, brute, spr, brain); if (bot.Failed) yield break;
                yield return S3(bot, brute, spr, brain); if (bot.Failed) yield break;
                var dir = LevelMark.Find<L7Director>("story.director");
                int det = dir != null ? dir.Detections : -1;
                bot.Log("detections (graded): " + det + " lesson: " + (dir != null ? dir.LessonDetections : -1) + " grade " + LevelCompleteExtras.StealthGrade);
                yield return bot.Expect(() => dir != null && dir.Detections == (StoryRun ? 1 : 0), "stealth: exactly " + (StoryRun ? 1 : 0) + " graded detection(s)");
                if (StoryRun)
                    foreach (var id in BeatIds)
                    {
                        string b = id;
                        yield return bot.Expect(() => _beats.Contains(b), "story beat fired: " + b);
                    }
                yield return bot.EnterExit();
            }
            finally { GameEvents.StoryBeat -= OnBeat; }
        }

        static IEnumerator ExpectCheckpoint(TeamBot bot, int index)
        {
            var cp = LevelMark.Find<Checkpoint>("checkpoint." + index);
            yield return bot.Expect(() => cp != null && cp.IsActivated, "checkpoint " + index + " activated", 6f);
        }

        static IEnumerator Seq(params IEnumerator[] steps) { foreach (var s in steps) yield return s; }

        // ================================================================== S0
        static IEnumerator S0(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // lanes: Brainiac x -4, Sprinter x 0, Brute x 4 (pairwise 4 m) - first spread out behind Q0's range
            yield return bot.Parallel(bot.WalkTo(brain, new Vector3(-4f, 0f, -8.5f)), bot.WalkTo(spr, new Vector3(0f, 0f, -9f)), bot.WalkTo(brute, new Vector3(4f, 0f, -8.5f)));
            if (bot.Failed) yield break;
            yield return bot.Parallel(bot.WalkTo(brain, "s1.entry.0", true), bot.WalkTo(spr, "s1.entry.1", true), bot.WalkTo(brute, "s1.entry.2", true));
        }

        // ================================================================== story-ON: one deliberate detection
        static IEnumerator Detection(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr)
        {
            var esc = LevelMark.Find<EscortBack>("s1.alarm.escort");
            if (esc == null) { bot.Fail("s1.alarm.escort missing"); yield break; }
            int before = esc.EscortCount;
            yield return bot.Parallel(bot.WalkTo(brute, "s1.togetherA"), bot.WalkTo(spr, "s1.togetherB"));
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => esc.EscortCount > before, 15f, "deliberate detection -> escorted back");
            yield return bot.Wait(0.5f);
        }

        // ================================================================== S1
        static IEnumerator S1(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            var g1 = LevelMark.Find<SignalReceiver>("s1.G1");
            // Brainiac to P1 (west lanes), Sprinter up the middle (waits for the gate), Brute stays in its own lane
            // until the Sprinter is through: never two of them within 3 m in a cone.
            var brainRoute = bot.WalkPath(brain, true, "s1.plateRoute", "s1.P1");
            var sprRoute = Seq(bot.WalkTo(spr, "s1.approach", true),
                               bot.WaitUntil(() => g1 != null && g1.Powered, 20f, "G1 open (P1 held)"),
                               bot.WalkPath(spr, true, "s1.gate", "s1.through", "s1.beyond"));
            var bruteRoute = Seq(bot.WalkTo(brute, new Vector3(4f, 0f, 20f), true),
                                 bot.WaitUntil(() => spr.transform.position.z > 33f, 25f, "sprinter through G1"),
                                 bot.WalkPath(brute, true, "s1.approach", "s1.gate", "s1.through", "s1.beyond2"));
            yield return bot.Parallel(brainRoute, sprRoute, bruteRoute); if (bot.Failed) yield break;
            yield return bot.Interact(spr, "s1.L1"); if (bot.Failed) yield break;
            var l1 = LevelMark.Find<Lever>("s1.L1");
            yield return bot.Expect(() => l1 != null && l1.IsOn, "L1 latched", 2f); if (bot.Failed) yield break;
            yield return bot.WalkPath(brain, true, "s1.alcoveOut", "s1.approach", "s1.gate", "s1.through", "s1.beyond3"); if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.1"); if (bot.Failed) yield break;
            yield return ExpectCheckpoint(bot, 1);
        }

        // ================================================================== S2
        static IEnumerator S2(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            // staggered: Brute to the teller gate (blind spot), the Brainiac to the stack spot once the Brute is west,
            // the Sprinter once the Brainiac is behind the statue (all three end up hidden from T1)
            var bruteRoute = bot.WalkPath(brute, true, "s2.hallIn", "s2.westRun", "s2.gateWait");
            var brainRoute = Seq(bot.WaitUntil(() => brute.transform.position.x < -6f, 20f, "brute west"),
                                 bot.WalkPath(brain, true, "s2.hallIn", "s2.brainRun", "s2.stackApproach"),
                                 bot.WalkTo(brain, "s2.stackSpot", false, 0.2f),
                                 bot.Face(brain, Vector3.forward));
            var sprRoute = Seq(bot.WaitUntil(() => brain.transform.position.z > 47f && brain.transform.position.x < -7f, 25f, "brainiac behind the statue"),
                               bot.WalkPath(spr, true, "s2.hallIn", "s2.westRun"),
                               bot.WalkTo(spr, "s2.stackRun", false, 0.3f));
            yield return bot.Parallel(bruteRoute, brainRoute, sprRoute); if (bot.Failed) yield break;
            yield return bot.Face(brain, Vector3.forward);
            yield return bot.StackOnto(spr, brain); if (bot.Failed) yield break;
            bot.Snapshot("l7_stacked", spr);
            yield return bot.ClimbOnto(spr, "s2.counterTop"); if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "s2.tellerDrop"); if (bot.Failed) yield break;
            yield return bot.WaitSettled(spr);
            yield return bot.Interact(spr, "s2.gateBtn"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s2.tellerGate", true, 2f); if (bot.Failed) yield break;
            yield return bot.Parallel(
                bot.WalkTo(spr, "s2.wait.0", true),
                Seq(bot.Wait(0.3f), bot.WalkPath(brute, false, "s2.gateIn", "s2.wait.2")),
                Seq(bot.Wait(1.0f), bot.WalkPath(brain, false, "s2.gateOut", "s2.gateIn", "s2.inside.a")));
            if (bot.Failed) yield break;
            yield return bot.Hack(brain, "s2.printer"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s2.printer", true, 2f); if (bot.Failed) yield break;
            yield return bot.WalkTo(brain, "s2.wait.1"); if (bot.Failed) yield break;
            // the staff door, one at a time (waits lie west of T2's 12 m range, >= 3 m apart)
            var order = new[] { spr, brain, brute };
            for (int i = 0; i < order.Length; i++)
            {
                var c = order[i];
                yield return bot.WalkTo(c, "s2.doorZone", true, 0.5f); if (bot.Failed) yield break;
                yield return bot.ExpectPowered("s2.staffDoor", true, 3f); if (bot.Failed) yield break;
                yield return bot.WalkPath(c, true, "s3.doorIn", "s3.corridor." + i); if (bot.Failed) yield break;
            }
        }

        // ================================================================== S3
        static IEnumerator S3(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.GatherAt("checkpoint.2"); if (bot.Failed) yield break;
            yield return ExpectCheckpoint(bot, 2);
        }
    }
}
