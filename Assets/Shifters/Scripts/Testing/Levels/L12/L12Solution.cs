// =====================================================================================
//  SHIFTERS - L12Solution.cs   [SHIFT 11 - UNMASKED]  TeamBot solution (no debug assists, StrictPlayerThrows).
//  Mirrors LEVELS2 "TeamBot proof": locker (not seed 1 = the tool-less fallback) -> C1 (intro) -> trio legs to the
//  W crank (Brute) -> Sprinter runs the 28 m BREAKER RACE while the Brute escorts the Brainiac to the E fuse ->
//  P2: Brute + Brainiac pair at PAD E, the gun/tool owner shoots the sheet (seed 1: the Brute punches it while
//  CHARGING) -> Brainiac hacks the projector -> lobby -> exit. Asserts nobody was ever SCARED >= 1 s, no spook landed.
//  Run: $SP/tools/run_level_test.sh <project> 12 1,2,3,MIN,MAX     (BOT_STORY=1 for the story-ON pass)
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L12Solution
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            var seeds = BotArgs.Seeds();
            if (seeds != null && seeds.Length > 1) return TeamBot.RunSeeds(ctx, seeds, Solve, null, Options(), "L12");
            return TeamBot.Run(ctx, Solve, Options(), "L12");
        }

        public static IEnumerator RunRuntime(DevPlayContext ctx) => TeamBot.RunRuntimeLevel(ctx, 11, Solve, "L12rt", BotArgs.Seeds(), Options());

        public static BotOptions Options() => new BotOptions { DiagnosticShots = true, Verbose = true, RunTimeout = 300f, StrictPlayerThrows = true };

        public static Vector3 P(string mark) => LevelMark.Position(mark);

        public static IEnumerator Solve(TeamBot bot)
        {
            var boss = LevelMark.Find<L12BoogeymanBoss>("boss");
            string key = TeamBot.CurrentTeamKey;
            bool toolless = key == "1";
            bot.Log("L12 team " + key + ": " + bot.TeamSummary() + (toolless ? " (tool-less fallback)" : ""));
            if (boss == null) { bot.Fail("no boss"); yield break; }
            var probe = boss.gameObject.AddComponent<L12ScareProbe>(); probe.Boss = boss;
            yield return StartToC1(bot, boss, !toolless); if (bot.Failed) yield break;
            yield return Crank(bot); if (bot.Failed) yield break;
            yield return RaceAndFuse(bot, boss); if (bot.Failed) yield break;
            yield return bot.Expect(() => boss.SpooksLanded == 0 && probe.MaxScaredFor < 1.0f,
                "P1 pairs: no spook landed, nobody SCARED >= 1 s (max " + probe.MaxScaredFor.ToString("0.00") + " s " + probe.MaxScaredWho + ")"); if (bot.Failed) yield break;
            bot.Log("P1 done at boss clock; whispers " + boss.Whispers + ", fizzles " + boss.Fizzles + ", max scared " + probe.MaxScaredFor.ToString("0.00") + " s");
            yield return Unmask(bot, boss, toolless ? Route.Punch : Route.Shot); if (bot.Failed) yield break;
            yield return HackAndOut(bot, boss);
        }

        public enum Route { Shot, Punch, Throw }

        /// <summary>S0: the PARTY POPPER locker (locker = true), then C1 (fires ch11.intro) and the boss intro.</summary>
        public static IEnumerator StartToC1(TeamBot bot, L12BoogeymanBoss boss, bool locker)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            if (locker) { yield return bot.Interact(bot.GunOwner, "s0.locker"); if (bot.Failed) yield break; }
            yield return bot.Parallel(bot.WalkTo(brute, new Vector3(-0.8f, L12Builder.DeckY, -1.5f)), bot.WalkTo(spr, new Vector3(0f, L12Builder.DeckY, -2.2f)), bot.WalkTo(brain, new Vector3(0.8f, L12Builder.DeckY, -1.5f))); if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.1"); if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.State != BossState.Dormant, 6f, "boss intro started");
        }

        /// <summary>P1 step 2: trio legs (lanes, walking) from the balcony (C1) to the W crank, the Brute turns it.</summary>
        public static IEnumerator Crank(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            var off3 = new[] { new Vector3(-0.9f, 0f, 0f), Vector3.zero, new Vector3(0.9f, 0f, 0f) };
            var offS = new[] { Vector3.zero, new Vector3(1.0f, 0f, -0.4f), new Vector3(-1.0f, 0f, 0.3f) };
            yield return Trio(bot, brute, spr, brain, new Vector3(-1.6f, L12Builder.DeckY, 3.9f), offS); if (bot.Failed) yield break;   // along the screen, clear of the C1 beacon pole
            yield return Trio(bot, brute, spr, brain, P("s1.stairW.top"), off3); if (bot.Failed) yield break;
            yield return Trio(bot, brute, spr, brain, P("s1.stairW.bottom"), off3); if (bot.Failed) yield break;
            var offZ = new[] { Vector3.zero, new Vector3(0f, 0f, 1.6f), new Vector3(0f, 0f, -1.6f) };
            yield return Trio(bot, brute, spr, brain, P("s1.legW"), offZ); if (bot.Failed) yield break;
            yield return bot.Parallel(bot.WalkTo(brute, "s1.crankW.stand"), bot.WalkTo(spr, L12Builder.CrankStand + new Vector3(0.8f, 0f, 1.6f)),
                                      bot.WalkTo(brain, L12Builder.CrankStand + new Vector3(0.8f, 0f, -1.6f)));
            if (bot.Failed) yield break;
            yield return bot.Interact(brute, "s1.crankW"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1.floodW", true, 2f);
        }

        /// <summary>P1 step 3: the race (Sprinter) || the pair to the fuse, the hack, then the P1 -> P2 transition.</summary>
        public static IEnumerator RaceAndFuse(TeamBot bot, L12BoogeymanBoss boss)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            yield return bot.Parallel(Race(bot, spr), PairToFuse(bot, brain, brute));
            if (bot.Failed) yield break;
            yield return bot.Hack(brain, "s1.fuseE"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1.floodE", true, 2f); if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.Phase == 1 || boss.State == BossState.PhaseTransition, 6f, "P1 -> P2 transition");
        }

        /// <summary>P2 step 4: knock the sheet off by route (shot / Brute punch while CHARGING / thrown Sprinter from s2.throwE).</summary>
        public static IEnumerator Unmask(TeamBot bot, L12BoogeymanBoss boss, Route route)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            if (route == Route.Throw)
            {
                // from wherever the team stands (C2 = break area E): around the E fuse to the THROW pad, the Brainiac to the pair spot
                var via = new Vector3(11.5f, 0f, 17f);
                yield return bot.Parallel(bot.WalkPath(brute, false, via, "s2.throwE"), bot.WalkPath(spr, false, via + new Vector3(0.8f, 0f, 1.2f), P("s2.throwE") + new Vector3(0f, 0f, -1.3f)),
                                          bot.WalkPath(brain, false, new Vector3(11.5f, 0f, 13f), new Vector3(11f, 0f, 20.5f), new Vector3(9.6f, 0f, 22.8f)));   // 4.5 m from the Brute, just outside the BOO ring
                if (bot.Failed) yield break;
                yield return bot.Grab(brute, spr); if (bot.Failed) yield break;
                // start the throw as the machine parks on PAD E (aim + settle ~1.5 s, flight ~1 s -> it arrives while CHARGING);
                // ~1 s before the machine parks on PAD E (aim + settle ~3.4 s, flight ~1.1 s -> it lands in the CHARGING window)
                float last = 99f;
                yield return bot.WaitUntil(() =>
                {
                    if (boss.Body == null || boss.Phase != 1) return false;
                    float d = BossFootprint.Planar(boss.Body.position, L12BoogeymanBoss.PadE);
                    bool ok = boss.CurrentPad == -1 && boss.IsFighting && d < 3.6f && d < last - 1e-4f;
                    last = d; return ok;
                }, 30f, "machine about to park on PAD E (throw)"); if (bot.Failed) yield break;
                int u0 = boss.Unmasks;
                // aim at the PARKED machine (pad centre, 0.7 m up; not the moving mark), no slide allowance: the lob goes into the 2 m body
                float slide = bot.Options.ThrowSlideAllowance; bot.Options.ThrowSlideAllowance = 0f;
                float tThrow = boss.FightClock;
                var imp = LevelMark.Find<L12SheetImpact>("boss.weak.impact");
                bool unmaskedEarly = false;
                yield return bot.Parallel(bot.ThrowTo(brute, L12BoogeymanBoss.PadE + Vector3.up * 0.7f, "s2.throwE", 5f),
                                          WatchUnmask(boss, u0, () => unmaskedEarly = true));
                bot.Options.ThrowSlideAllowance = slide;
                bot.Log("throw: started at clock " + tThrow.ToString("0.00") + ", now " + boss.FightClock.ToString("0.00") + "; impact volume saw " + (imp != null ? imp.Seen + " (last: " + imp.Last + ")" : "?")
                        + ", charging " + boss.IsCharging + ", pad " + boss.CurrentPad + ", dizzy " + boss.IsDizzy + ", unmasks " + boss.Unmasks + (unmaskedEarly ? " (unmasked in flight)" : ""));
                if (bot.Failed) yield break;
                yield return bot.Expect(() => boss.IsDizzy && boss.Unmasks > u0, "thrown Sprinter unmasks it (DIZZY)", 2f);
                yield break;
            }
            yield return bot.Parallel(bot.WalkTo(brute, "s2.padE.punchStand", false, 0.4f), bot.WalkTo(brain, "s2.padE.pairSpot", false, 0.5f), bot.WalkTo(spr, "s2.shootE", false, 0.6f));
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.Phase == 1 && boss.IsCharging && boss.CurrentPad == 1, 26f, "machine CHARGING on PAD E"); if (bot.Failed) yield break;
            if (route == Route.Punch)
            {
                yield return bot.Interact(brute, "boss.weak.punch", null, true, 3f); if (bot.Failed) yield break;
                yield return bot.Expect(() => boss.IsDizzy, "Brute punch unmasks it (DIZZY)", 1f);
            }
            else
            {
                var shooter = bot.GunOwner;
                yield return bot.ShootAt(shooter, "boss.weak.sheet", () => boss.IsDizzy, 8, 4f); if (bot.Failed) yield break;
                yield return bot.Expect(() => boss.IsDizzy, "shot on the sheet unmasks it (DIZZY)", 1f);
            }
        }

        /// <summary>P2 hack (walk around the body to the projector on its BACK) -> defeated -> step 5 walk out + exit.</summary>
        public static IEnumerator HackAndOut(TeamBot bot, L12BoogeymanBoss boss)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            // The projector is on the machine's BACK (the machine turns to its drive direction): walk around the body
            // on the Brainiac's side, then hack from 1.1 m behind the port, exactly what a human has to do.
            {
                var port = LevelMark.Find("boss.weak.projector");
                Vector3 body = boss.Body != null ? boss.Body.position : L12BoogeymanBoss.PadE; body.y = 0f;
                Vector3 outDir = port != null ? port.transform.forward : Vector3.left; outDir.y = 0f; outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : Vector3.left;
                Vector3 stand = body + outDir * 2.15f;
                Vector3 rel = brain.transform.position - body; rel.y = 0f;
                if (Vector3.Dot(rel, outDir) < 0.8f)
                {
                    Vector3 perp = Vector3.Cross(Vector3.up, outDir);
                    if (Vector3.Dot(rel, perp) < 0f) perp = -perp;
                    yield return bot.WalkTo(brain, body + perp * 2.1f, false, 0.5f); if (bot.Failed) yield break;
                    yield return bot.WalkTo(brain, body + perp * 2.1f + outDir * 2.15f, false, 0.5f); if (bot.Failed) yield break;
                }
                yield return bot.Hack(brain, "boss.weak.projector", stand); if (bot.Failed) yield break;
            }
            yield return bot.ExpectBossDefeated(boss, 6f); if (bot.Failed) yield break;
            bot.Log("P2 done: boos " + boss.Boos + ", unmasks " + boss.Unmasks);
            yield return bot.Expect(() => boss.Boos == 0, "no BOO landed on the pair"); if (bot.Failed) yield break;

            // ---------------- walk out (C10 plays here in story-ON runs)
            yield return bot.ExpectPowered("s3.door", true, 3f); if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => StoryPlayer.Instance == null || !StoryPlayer.Instance.IsBlocking, 60f, "C10 finished (story ON)"); if (bot.Failed) yield break;
            // line up in front of the 4 m north door first (a diagonal from the E pad clips the door frame)
            yield return bot.Parallel(bot.WalkTo(spr, new Vector3(-1.1f, 0f, 27.6f), false, 0.5f), bot.WalkTo(brain, new Vector3(0f, 0f, 27f), false, 0.5f), bot.WalkTo(brute, new Vector3(1.1f, 0f, 27.6f), false, 0.5f));
            if (bot.Failed) yield break;
            yield return bot.GatherAt("s3.lobby"); if (bot.Failed) yield break;
            yield return bot.EnterExit("exit", true, 30f);
        }

        static IEnumerator WatchUnmask(L12BoogeymanBoss boss, int u0, System.Action hit)
        {
            for (float t = 0f; t < 8f; t += Time.deltaTime) { if (boss.Unmasks > u0) { hit(); yield break; } yield return null; }
        }

        public static IEnumerator Trio(TeamBot bot, ShifterCharacter a, ShifterCharacter b, ShifterCharacter c, Vector3 p, Vector3[] off)
            => bot.Parallel(bot.WalkTo(a, p + off[0], false, 0.5f), bot.WalkTo(b, p + off[1], false, 0.5f), bot.WalkTo(c, p + off[2], false, 0.5f));

        static IEnumerator Race(TeamBot bot, ShifterCharacter spr)
        {
            yield return bot.WalkTo(spr, "s1.race.stand", false, 0.4f); if (bot.Failed) yield break;
            yield return bot.Interact(spr, "s1.race.button"); if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "s1.race.leverStand", true, 0.6f); if (bot.Failed) yield break;
            yield return bot.Interact(spr, "s1.race.lever"); if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1.floodN", true, 2f);
        }

        /// <summary>PairLegs(brain, brute, lane.1..3, fuse stand): walking, legs <= 7 m, the Brute 1.6 m to the south.</summary>
        static IEnumerator PairToFuse(TeamBot bot, ShifterCharacter brain, ShifterCharacter brute)
        {
            var lane = new Vector3(0f, 0f, -1.6f);
            foreach (var m in new[] { "s1.lane.1", "s1.lane.2", "s1.lane.3" })
            {
                yield return bot.Parallel(bot.WalkTo(brain, P(m), false, 0.5f), bot.WalkTo(brute, P(m) + lane, false, 0.5f));
                if (bot.Failed) yield break;
            }
            yield return bot.Parallel(bot.WalkTo(brain, "s1.fuseE.stand", false, 0.4f), bot.WalkTo(brute, L12Builder.FuseStand + new Vector3(-0.6f, 0f, -1.6f), false, 0.5f));
        }
    }
}
