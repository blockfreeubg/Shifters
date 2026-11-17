// =====================================================================================
//  SHIFTERS - L12Extras.cs   [SHIFT 11 - UNMASKED]  boss extras (LEVELS2 "Boss extras", G13). No debug teleports.
//  Run (one launch, ~6 min sim):
//    devplay.sh <proj> l12_extras -devScene Assets/Shifters/Scenes/L12_Unmasked.unity -devSetup Shifters.L12Extras.Run -devSeconds 900
//  A  S0 -> C1 -> crank (first light) -> (e) FIZZLE: the Brute (active) alone at s1.darkSpot faces the eyes with the
//     flashlight -> (d) deliberate SPOOK: the Brainiac alone at s1.darkSpot is dragged into the BROOM CLOSET, the Brute
//     climbs the E stair and pulls the closet lever -> released + ch11.rescue / {RESCUED}.
//  (a) Restart Checkpoint at C1 -> lights 0/3, closet empty, P1 -> crank + race + fuse (steps 2-3) -> P2 reached (C2)
//     -> Restart Checkpoint at C2 -> floods ON, machine with its sheet -> THROW route (Brute throws the Sprinter from
//     s2.throwE into the CHARGING machine, StrictPlayerThrows) -> hack -> walk out -> level complete.
//  (b) 120 s idle: 60 s in P1 (Brute + Brainiac in break area W, Sprinter on the balcony), steps 2-3, 60 s in P2
//     (Brute + Brainiac in break area E, Sprinter where the race ended) -> nobody harmed, no eyes opened on a parked
//     intern, no BOO landed.
//  (c) timeline hash (boss.TimelineHash: phase, slot, kind, start FightClock 0.01 s) of the first two P1 slots and the
//     first P2 pad pause (RestorePhase(1)) on seeds 1,2,3,MIN,MAX -> identical.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L12Extras
    {
        static L12BoogeymanBoss Boss => Object.FindAnyObjectByType<L12BoogeymanBoss>();
        static int CI => CheckpointManager.Instance != null ? CheckpointManager.Instance.CurrentIndex : -1;
        static Vector3 P(string m) => LevelMark.Position(m);
        static readonly List<string> s_beats = new List<string>();
        static readonly List<string> s_hashes = new List<string>();
        static void OnBeat(string id) { if (s_beats.Count < 200) s_beats.Add(id); }

        static readonly Vector3 BAW = new Vector3(-11.5f, 0f, 3.5f), BAE = new Vector3(11.5f, 0f, 3.5f);
        static readonly Vector3 ViaW = new Vector3(-9f, 0f, 11.5f), ViaE = new Vector3(9f, 0f, 11.5f);

        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            GameEvents.StoryBeat -= OnBeat; GameEvents.StoryBeat += OnBeat; s_beats.Clear();
            var opts = L12Solution.Options();

            // ---------------- A: fizzle + deliberate spook + rescue (after the first light)
            yield return TeamBot.RunSeeds(ctx, new[] { 3 }, StageA, null, opts, "L12xA");   // team 3 (spec: the throw route runs on seed 3)
            var before = Boss;
            ctx.Check(s_beats.Contains("ch11.rescue"), "story beat ch11.rescue raised on the closet rescue");
            ctx.Check(s_beats.Contains("ch11.fizzle") && s_beats.Contains("ch11.spook"), "ch11.fizzle + ch11.spook raised");
            ctx.Check(before != null && before.Lights != null && before.Lights.LitCount >= 1 && CI == 1, "before the C1 restart: >= 1 light on, CI 1");

            // ---------------- (a) Restart Checkpoint at C1
            yield return Restart(ctx, before);
            var boss = Boss;
            ctx.Check(boss != null && boss != before && boss.Phase == 0 && CI == 1, "C1 restore: P1 again (CI " + CI + ")");
            ctx.Check(boss != null && boss.Lights != null && boss.Lights.LitCount == 0, "C1 restore: lights back to 0/3");
            ctx.Check(boss != null && boss.Closet != null && boss.Closet.PrisonerCount == 0 && boss.SpooksLanded == 0, "C1 restore: closet empty, no spook counted");
            yield return TeamBot.Run(ctx, StageP1, opts, "L12xP1");
            for (float w = 0f; w < 12f; w += Time.deltaTime) { var bb = Boss; if (bb != null && bb.Phase == 1 && bb.IsFighting && CI == 2) break; yield return null; }
            yield return ctx.Wait(1f);
            before = Boss;
            ctx.Check(before != null && before.Phase == 1 && before.IsFighting && CI == 2, "P2 reached, C2 active before the restart");

            // ---------------- (a) Restart Checkpoint at C2 -> throw route
            yield return Restart(ctx, before);
            boss = Boss;
            ctx.Check(boss != null && boss != before && boss.Phase == 1 && CI == 2, "C2 restore: PHASE 2 resumes (CI " + CI + ")");
            ctx.Check(boss != null && boss.Lights != null && boss.Lights.LitCount == 3 && boss.Unmasks == 0 && !boss.IsDizzy, "C2 restore: floods ON, machine still sheeted");
            yield return TeamBot.Run(ctx, StageThrow, opts, "L12xThrow");

            // ---------------- (b) idle, (c) hash
            yield return TeamBot.RunSeeds(ctx, new[] { 1 }, Idle, null, opts, "L12idle");
            s_hashes.Clear();
            yield return TeamBot.RunSeeds(ctx, new[] { 1, 2, 3, TeamBot.SeedMin, TeamBot.SeedMax }, Hash, null, opts, "L12hash");
            for (int p = 0; p < 2; p++)
            {
                string first = null; bool same = true; int n = 0; var sb = new System.Text.StringBuilder();
                foreach (var h in s_hashes)
                {
                    if (!h.StartsWith("P" + p + "=")) continue;
                    n++; sb.Append(h).Append(' ');
                    if (first == null) first = h; else if (h != first) same = false;
                }
                ctx.Check(first != null && n == 5 && same, "timeline hash P" + (p + 1) + " identical on 5 teams: " + sb);
            }
            GameEvents.StoryBeat -= OnBeat;
        }

        static IEnumerator Restart(DevPlayContext ctx, L12BoogeymanBoss before)
        {
            GameSession.Instance.RestartFromCheckpoint();
            yield return ctx.Wait(1.5f);
            for (float t = 0f; t < 20f; t += Time.deltaTime)
            {
                var b = Boss;
                if (b != null && b != before && (b.IsFighting || b.IsDefeated)) break;
                yield return null;
            }
            yield return ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ A
        static IEnumerator StageA(TeamBot bot)
        {
            var boss = Boss; var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            if (boss == null) { bot.Fail("no boss"); yield break; }
            yield return L12Solution.StartToC1(bot, boss, false); if (bot.Failed) yield break;
            yield return L12Solution.Crank(bot); if (bot.Failed) yield break;

            // (e) FIZZLE: the Brute (active, carries the flashlight) alone in the dark, the others in break area W
            yield return bot.Switch(brute); if (bot.Failed) yield break;
            int f0 = boss.Fizzles;
            yield return bot.Parallel(bot.WalkTo(brute, "s1.darkSpot", false, 0.6f), bot.WalkTo(spr, BAW, false, 0.8f), bot.WalkTo(brain, BAW + new Vector3(1.2f, 0f, 0f), false, 0.8f));
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => (boss.Target == brute && boss.EyesOpen) || boss.Fizzles > f0 || brute.State != CharacterState.Alive, 30f, "a TELL on the lonely Brute"); if (bot.Failed) yield break;
            if (boss.Fizzles == f0 && boss.EyesOpen) { yield return bot.FaceAndHold(brute, boss.EyesPoint, 1.6f, () => boss.Fizzles > f0); if (bot.Failed) yield break; }
            yield return bot.Expect(() => boss.Fizzles > f0 && brute.State == CharacterState.Alive, "(e) flashlight on the eyes FIZZLES the spook (fizzles " + boss.Fizzles + ", Brute " + brute.State + ")", 1f); if (bot.Failed) yield break;

            // (d) deliberate SPOOK: the Brainiac alone at the dark spot, the others in break area E
            int s0 = boss.SpooksLanded;
            yield return bot.Parallel(bot.WalkTo(brain, "s1.darkSpot", false, 0.6f), bot.WalkPath(brute, false, ViaE, BAE), bot.WalkPath(spr, false, ViaW, ViaE, BAE + new Vector3(1.2f, 0f, 0f)));
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => brain.State == CharacterState.Captured, 30f, "(d) the lonely Brainiac is spooked into the closet"); if (bot.Failed) yield break;
            yield return bot.Expect(() => boss.SpooksLanded > s0 && boss.Closet != null && boss.Closet.PrisonerCount == 1, "spook landed, the Brainiac is in the BROOM CLOSET", 2f); if (bot.Failed) yield break;
            yield return bot.WalkPath(brute, false, ViaE, "s1.stairE.bottom", "s1.stairE.top", "s0.leverStand"); if (bot.Failed) yield break;
            yield return bot.Interact(brute, "s1.closetLever"); if (bot.Failed) yield break;
            yield return bot.Expect(() => brain.State == CharacterState.Alive && boss.Rescues >= 1, "the closet lever rescues the Brainiac (rescues " + boss.Rescues + ")", 6f); if (bot.Failed) yield break;
            yield return bot.Expect(() => s_beats.Contains("ch11.rescue") && StoryText.Resolve("{RESCUED}").ToUpperInvariant().Contains(brain.DisplayName.ToUpperInvariant()), "ch11.rescue with {RESCUED} = " + brain.DisplayName, 1f);
        }

        // ------------------------------------------------------------------ after the C1 restore: steps 2-3
        static IEnumerator StageP1(TeamBot bot)
        {
            var boss = Boss;
            yield return bot.WaitUntil(() => boss.State != BossState.Dormant || boss.IsFighting, 6f, "boss resumed"); if (bot.Failed) yield break;
            yield return L12Solution.Crank(bot); if (bot.Failed) yield break;
            yield return L12Solution.RaceAndFuse(bot, boss);
        }

        // ------------------------------------------------------------------ after the C2 restore: throw route
        static IEnumerator StageThrow(TeamBot bot)
        {
            var boss = Boss;
            yield return L12Solution.Unmask(bot, boss, L12Solution.Route.Throw); if (bot.Failed) yield break;
            yield return L12Solution.HackAndOut(bot, boss);
        }

        // ------------------------------------------------------------------ (b) 120 s idle
        static IEnumerator Idle(TeamBot bot)
        {
            var boss = Boss; var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            if (boss == null) { bot.Fail("no boss"); yield break; }
            yield return L12Solution.StartToC1(bot, boss, false); if (bot.Failed) yield break;
            yield return bot.Switch(spr); if (bot.Failed) yield break;
            // the same lanes as L12Solution.Crank (Brute -0.9, Brainiac +0.9), then on to break area W
            Vector3 deckB = new Vector3(-1.6f, L12Builder.DeckY, 3.9f), deckR = deckB + new Vector3(-1.0f, 0f, 0.3f);
            Vector3 lB = new Vector3(-0.9f, 0f, 0f), lR = new Vector3(0.9f, 0f, 0f);
            Vector3 topB = P("s1.stairW.top") + lB, topR = P("s1.stairW.top") + lR, botB = P("s1.stairW.bottom") + lB, botR = P("s1.stairW.bottom") + lR;
            yield return bot.WalkTo(spr, "boss.safe.1", false, 0.6f); if (bot.Failed) yield break;
            yield return Legs(bot, brute, brain, new[] { deckB, topB, botB, ViaW, BAW }, new[] { deckR, topR, botR, ViaW + lR, BAW + new Vector3(1.2f, 0f, 0f) }); if (bot.Failed) yield break;
            yield return Park(bot, boss, 60f, "P1"); if (bot.Failed) yield break;
            // regroup on the balcony (the same lanes back up), then steps 2-3
            yield return Legs(bot, brute, brain, new[] { ViaW, botB, topB, deckB }, new[] { ViaW + lR, botR, topR, deckR }); if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, deckB + new Vector3(1.0f, 0f, -0.4f), false, 0.6f); if (bot.Failed) yield break;
            yield return L12Solution.Crank(bot); if (bot.Failed) yield break;
            yield return L12Solution.RaceAndFuse(bot, boss); if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.Phase == 1 && boss.IsFighting, 8f, "P2 fighting"); if (bot.Failed) yield break;
            yield return bot.Parallel(bot.WalkTo(brute, BAE + new Vector3(0f, 0f, 1f), false, 0.8f), bot.WalkTo(brain, BAE + new Vector3(1.2f, 0f, 0f), false, 0.8f));
            if (bot.Failed) yield break;
            yield return Park(bot, boss, 60f, "P2");
        }

        /// <summary>Per point a Parallel(WalkTo a, WalkTo b) leg (each stops at the point, no corner cutting at the glass screens).</summary>
        static IEnumerator Legs(TeamBot bot, ShifterCharacter a, ShifterCharacter b, Vector3[] pa, Vector3[] pb)
        {
            for (int i = 0; i < pa.Length; i++)
            {
                yield return bot.Parallel(bot.WalkTo(a, pa[i], false, 0.5f), bot.WalkTo(b, pb[i], false, 0.5f));
                if (bot.Failed) yield break;
            }
        }

        static IEnumerator Park(TeamBot bot, L12BoogeymanBoss boss, float seconds, string label)
        {
            int spooks0 = boss.SpooksLanded, boos0 = boss.Boos, eyesOnParked = 0, slots0 = boss.TimelineEvents;
            bool wasOpen = false;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                bool open = boss.EyesOpen;
                if (open && !wasOpen && boss.Target != null) eyesOnParked++;
                wasOpen = open;
                yield return null;
            }
            int slots = boss.TimelineEvents - slots0;
            bot.Log("IDLE " + label + ": " + seconds + " s, " + slots + " timeline events, whispers " + boss.Whispers + ", boos " + (boss.Boos - boos0));
            foreach (var c in bot.Team)
                yield return bot.Expect(() => c.State == CharacterState.Alive && c.Condition == CharacterCondition.Normal, c.DisplayName + " unharmed after " + seconds + " s idle in " + label, 0f);
            yield return bot.Expect(() => boss.SpooksLanded == spooks0 && boss.Boos == boos0 && eyesOnParked == 0,
                label + " idle: no spook / BOO landed, no eyes opened on a parked intern (eyes " + eyesOnParked + ", slots " + slots + ")", 0f);
        }

        // ------------------------------------------------------------------ (c) timeline hash
        static IEnumerator Hash(TeamBot bot)
        {
            var boss = Boss;
            if (boss == null) { bot.Fail("no boss"); yield break; }
            yield return L12Solution.StartToC1(bot, boss, false); if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.Phase == 0 && boss.IsFighting, 8f, "P1 fighting"); if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.FightClock >= 14f, 20f, "P1 clock 14 s"); if (bot.Failed) yield break;
            s_hashes.Add("P0=" + boss.TimelineHash.ToString("X8"));
            bot.Log("HASH P1 team " + TeamBot.CurrentTeamKey + " = " + boss.TimelineHash.ToString("X8") + " (" + boss.TimelineLog + ")");
            boss.RestorePhase(1);
            boss.ResetTimeline();
            yield return bot.WaitUntil(() => boss.TimelineEvents >= 1, 15f, "first P2 pad pause"); if (bot.Failed) yield break;
            s_hashes.Add("P1=" + boss.TimelineHash.ToString("X8"));
            bot.Log("HASH P2 team " + TeamBot.CurrentTeamKey + " = " + boss.TimelineHash.ToString("X8") + " (" + boss.TimelineLog + ")");
        }
    }
}
