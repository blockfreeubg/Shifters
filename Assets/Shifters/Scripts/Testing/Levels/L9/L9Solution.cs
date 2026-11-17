// =====================================================================================
//  SHIFTERS - L9Solution.cs   [SHIFT 08 - THE GETAWAY]  TeamBot solution (no debug assists; archetypes + bot.GunOwner)
//    S0   everyone boards the tram (wings + back gap) -> ALL ABOARD -> the chase starts
//    ST1  the Brute hops onto the side platform and punches the VAULT BRICKS, walks back aboard
//    ST2  the Brainiac hacks the deck CONSOLE (live only at the toll)          -> C2
//    ST3  the Sprinter TURBO-jumps 5.6 m onto the pylon, pulls the lever, walks back over the catwalk  -> C3
//    DOCK -> arena. Per phase: the specialist (P1 gun owner, P2 Brute, P3 Brainiac) is active and stands on the
//         BAIT spot behind the phase's pillar; when the HAM CHARGE locks on it steps aside; Porkchop bonks the pillar
//         (DIZZY) -> shoot the belly plug / punch the snout / hack the ear port. Then EXIT (the van).
//  Extras (flags, seed 1): -l9Busted 1 (wait at ST2 until BUSTED -> restore at C2 -> pass), -l9Restore 1 (Restart
//    Checkpoint at the P2 and P3 checkpoints -> resume), -l9Idle 1 (120 s on the loading bays: nobody hurt),
//    -l9Shots 1 (key-moment screenshots).
//  Run: $SP/tools/run_level_test.sh <project> 9 1,2,3,MIN,MAX     (story ON: BOT_STORY=1 ... 9 1 -l9Busted 1 ...)
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L9Solution
    {
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, Options(), "L9");

        /// <summary>Same solution on the level built at RUNTIME by L9Builder (fast iteration; no scene reloads -> no extras).</summary>
        public static IEnumerator RunRuntime(DevPlayContext ctx) => TeamBot.RunRuntimeLevel(ctx, 8, Solve, "L9rt", BotArgs.Seeds(1, 2, 3), Options());

        public static BotOptions Options() => new BotOptions { StrictPlayerThrows = true, StrictInteract = true, RunTimeout = 600f };

        static bool Flag(string k) => BotArgs.Int(k, 0) != 0;
        static readonly HashSet<string> s_beats = new HashSet<string>();
        static bool s_cutscene, s_hooked;
        static void OnBeat(string id) { if (!string.IsNullOrEmpty(id)) s_beats.Add(id); }
        static void OnCut(bool on) { s_cutscene = on; }

        static ChaseTrack Track => LevelMark.Find<ChaseTrack>("chase");
        static L9PorkchopBoss Boss => LevelMark.Find<L9PorkchopBoss>("boss");
        static int Ck => CheckpointManager.Instance != null ? CheckpointManager.Instance.CurrentIndex : 0;

        public static IEnumerator Solve(TeamBot bot)
        {
            if (!s_hooked) { s_hooked = true; GameEvents.StoryBeat += OnBeat; GameEvents.CutsceneChanged += OnCut; }
            s_beats.Clear();
            bot.Log("team: " + bot.TeamSummary() + "  gun: " + (bot.GunOwner != null ? bot.GunOwner.Archetype.ToString() : "NONE"));
            yield return StaticChecks(bot); if (bot.Failed) yield break;
            yield return bot.SetAIMode(TeamAIMode.Stay);
            yield return WaitStory(bot);
            yield return Chase(bot); if (bot.Failed) yield break;
            yield return Fight(bot); if (bot.Failed) yield break;
            yield return Exit(bot);
        }

        // ================================================================== checks (DoD 4)
        static IEnumerator StaticChecks(TeamBot bot)
        {
            string[] marks = { "spawn", "checkpoint.1", "checkpoint.2", "checkpoint.3", "checkpoint.4", "checkpoint.5", "checkpoint.6", "exit",
                "chase", "tram", "s0.deck", "deck.slot.0", "deck.slot.1", "deck.slot.2", "deck.edgeRight", "deck.console", "st1.wall", "st1.hop", "st1.punch",
                "st2.gate", "st3.lever", "st3.pylonTop", "st3.catwalk", "st3.bridgeA", "boss", "boss.arena", "boss.weak.plug", "boss.weak.snout", "boss.weak.ear",
                "boss.safe.0", "boss.safe.1", "boss.safe.2", "boss.safe.3", "p1.bait", "p1.side", "p2.bait", "p2.side", "p3.bait", "p3.side",
                "park.0", "park.1", "park.2", "park.3", "arena.door", "exhibit.b", "story.metv", "tutorial.ch08_chase", "tutorial.ch08_bait",
                "chase.station.0", "chase.station.1", "chase.station.2" };
            int missing = 0;
            foreach (var m in marks) if (!LevelMark.Exists(m)) { missing++; bot.Log("MISSING MARK " + m); }
            yield return bot.Expect(() => missing == 0, "all " + marks.Length + " marks resolve");
            string[] beats = { "ch08.board", "ch08.st1", "ch08.st2", "ch08.st3", "ch08.busted", "ch08.boss", "ch08.p1", "ch08.p2", "ch08.p3", "ch08.outro" };
            int undefined = 0;
            foreach (var b in beats) if (!StoryScripts.Exists(b)) { undefined++; bot.Log("UNDEFINED BEAT " + b); }
            yield return bot.Expect(() => undefined == 0, "all " + beats.Length + " ch08 beat ids are defined");
            var boss = Boss;
            string err = null;
            yield return bot.Expect(() => boss != null && boss.ValidateSpecs(out err), "boss specs valid (telegraphs >= 1.5/1.2 s) " + err);
            yield return bot.Expect(() => boss != null && boss.Arena != null && boss.Arena.ValidateSafeZones(out err), "safe zones never covered " + err);
            yield return bot.Expect(() => boss != null && boss.Arena.ValidateNoKillZones(out err), "no kill zones in the arena " + err);
            if (TeamBot.CurrentTeamKey != "1" || s_audited) yield break;
            s_audited = true;
            var mats = SceneAudit.MaterialProblems(20);
            foreach (var m in mats) bot.Log("MATERIAL " + m);
            yield return bot.Expect(() => mats.Count == 0, "no default/magenta/missing materials");
            var bud = SceneAudit.MeasureBudgets("L9_TheGetaway");
            bot.Log("BUDGET " + bud.Line());
            yield return bot.Expect(() => bud.Ok, "SceneAudit budgets within the hard caps");
            var moving = new HashSet<Transform>();
            yield return SceneAudit.CollectMovingTexts(moving);
            var signs = SceneAudit.SignProblems(moving);
            foreach (var i in signs.Issues) bot.Log("SIGN ISSUE " + i.Kind + " " + i.What + " '" + i.Text + "' " + i.Path + " " + i.Detail);
            foreach (var w in signs.Warnings) bot.Log("SIGN WARN " + w.Kind + " " + w.What + " '" + w.Text + "' " + w.Path);
            bot.Log("signs: texts " + signs.Texts + " judged " + signs.Judged + " issues " + signs.Issues.Count + " warnings " + signs.Warnings.Count);
            yield return bot.Expect(() => signs.Issues.Count == 0, "sign audit clean");
        }
        static bool s_audited;

        static IEnumerator WaitStory(TeamBot bot)
        {
            float t = 0f;
            while (s_cutscene && t < 60f) { t += Time.deltaTime; yield return null; }
        }

        static IEnumerator Shot(TeamBot bot, string name)
        {
            if (!Flag("-l9Shots") || bot.Ctx == null) yield break;
            if (name == "01_spawn") yield return bot.Wait(4.5f);
            yield return null;
            bot.Ctx.Shot("l9_" + name);
        }

        // ================================================================== the chase
        /// <summary>A point on the deck in tram-local coordinates (follows the tram).</summary>
        static Vector3 Deck(float x, float z) { var t = Track; return t.Tram.Body.transform.TransformPoint(new Vector3(x, 0f, z)); }

        /// <summary>A point that RIDES with the tram (a child of the tram body): safe when the tram starts mid-walk.</summary>
        static Transform DeckT(float x, float z)
        {
            var body = Track.Tram.Body.transform;
            string n = "BotDeckPt_" + x.ToString("0.00") + "_" + z.ToString("0.00");
            var t = body.Find(n);
            if (t == null) { t = new GameObject(n).transform; t.SetParent(body, false); t.localPosition = new Vector3(x, 0f, z); }
            return t;
        }

        /// <summary>Everyone walks aboard through the side gaps (wings) / the back gap: never across a railing.</summary>
        static IEnumerator Board(TeamBot bot)
        {
            var b = bot.Brute; var s = bot.Sprinter; var i = bot.Brainiac;
            yield return bot.Parallel(
                bot.WalkPath(b, false, Deck(-3.1f, 0f), DeckT(-1.3f, 0f), "deck.slot.0"),
                bot.WalkPath(s, false, Deck(3.1f, 0.55f), DeckT(1.3f, 0.55f), "deck.slot.1"),
                bot.WalkPath(i, false, Deck(3.4f, -0.4f), DeckT(1.3f, -0.4f), "deck.slot.2"));
            yield return bot.Expect(() => Track.AllAboard, "ALL ABOARD", 3f);
        }

        /// <summary>Board from a LEFT station platform (after a restore): through the left side gap, one lane each.</summary>
        static IEnumerator BoardFromLeft(TeamBot bot)
        {
            var b = bot.Brute; var s = bot.Sprinter; var i = bot.Brainiac;
            yield return bot.Parallel(
                bot.WalkPath(b, false, Deck(-3.2f, -0.6f), DeckT(-1.3f, -0.6f), "deck.slot.0"),
                bot.WalkPath(s, false, Deck(-3.6f, 0.6f), DeckT(-1.3f, 0.6f), DeckT(0.6f, 0.2f), "deck.slot.1"),
                bot.WalkPath(i, false, Deck(-4.2f, 0f), DeckT(-1.3f, 0f), "deck.slot.2"));
            yield return bot.Expect(() => Track.AllAboard, "ALL ABOARD (restored)", 3f);
        }

        static IEnumerator Ride(TeamBot bot, System.Func<bool> until, float timeout = 25f)
        {
            var t = Track;
            yield return bot.RideUntil(t.Tram, until, timeout, bot.Team, 3.3f);
        }

        static IEnumerator Chase(TeamBot bot)
        {
            var track = Track;
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            yield return Shot(bot, "01_spawn");
            if (track.StationIndex < 0)
            {
                yield return Board(bot);
                yield return bot.WaitChase(track, t => t.Started, 6f, "ALL ABOARD -> chase started");
                yield return Shot(bot, "02_ride");
                yield return Ride(bot, () => track.Docked && track.StationIndex == 0);
                // ---- ST1: the Brute punches the VAULT BRICKS from the side platform
                var wall = LevelMark.Find<Breakable>("st1.wall");
                yield return bot.WalkPath(brute, false, Deck(-1.3f, 0f), "st1.hop", "st1.punch");
                yield return bot.Interact(brute, "st1.wall", "st1.punch");
                yield return bot.Expect(() => wall.IsBroken, "ST1 vault bricks broken", 3f);
                yield return bot.WalkPath(brute, false, "st1.hop", Deck(-1.3f, 0f), "deck.slot.0");
                yield return Ride(bot, () => track.Docked && track.StationIndex == 1);
            }
            if (track.StationIndex == 1)
            {
                yield return bot.Expect(() => Ck >= 2, "C2 active at the toll gate", 2f);
                if (Flag("-l9Busted") && track.BustCount == 0 && TeamBot.CurrentTeamKey == "1")
                {
                    bot.Log("EXTRA: waiting at ST2 for BUSTED (gap " + track.Gap.ToString("0.0") + ")");
                    yield return bot.WaitUntil(() => track == null || track.Busted, 40f, "BUSTED at ST2");
                    yield return Shot(bot, "x_busted");
                    yield return WaitReload(bot);
                    track = Track;
                    yield return bot.Expect(() => track != null && track.StationIndex == 1 && track.Docked && !track.Busted, "restored at ST2 after BUSTED (C2)", 3f);
                    yield return bot.Expect(() => track.Gap >= 39f, "gap reset to 40 after the restore", 1f);
                    yield return bot.SetAIMode(TeamAIMode.Stay);
                    brute = bot.Brute; spr = bot.Sprinter; brain = bot.Brainiac;
                    // the restored team spawns on the station platform: board again
                    yield return BoardFromLeft(bot);
                }
                // ---- ST2: the deck CONSOLE (IQ)
                var console = LevelMark.Find<Terminal>("deck.console");
                yield return bot.Hack(brain, "deck.console");
                yield return bot.Expect(() => console.IsOn, "ST2 toll overridden", 3f);
                yield return bot.WalkTo(brain, "deck.slot.2");
                yield return Ride(bot, () => track.Docked && track.StationIndex == 2);
            }
            if (track.StationIndex == 2)
            {
                yield return bot.Expect(() => Ck >= 3, "C3 active at the drawbridge", 2f);
                yield return Shot(bot, "03_pylon");
                // ---- ST3: turbo jump to the pylon, lever, catwalk back
                yield return bot.JumpGap(spr, "deck.edgeRight", "st3.pylonTop", true, 3.4f);
                yield return bot.Expect(() => spr.Feet.x > 7.4f && spr.Feet.y > 0.3f, "Sprinter landed on the pylon", 2f);
                yield return bot.Interact(spr, "st3.lever");
                yield return bot.ExpectPowered("st3.catwalk", true, 3f);
                yield return bot.Wait(1.2f);
                yield return Shot(bot, "04_drawbridge");
                yield return bot.WalkPath(spr, false, new Vector3(7.0f, 0.5f, 87f), new Vector3(2.6f, 0.5f, 87f), Deck(1.3f, 0f), "deck.slot.1");
                yield return Ride(bot, () => track.Finished);
            }
            bot.Log("CHASE DONE: busts=" + track.BustCount + " gap=" + track.Gap.ToString("0.0") + " checkpoint=" + Ck);
        }

        static float St(int i) => i == 0 ? L9Builder.St1Z : i == 1 ? L9Builder.St2Z : L9Builder.St3Z;

        static IEnumerator WaitReload(TeamBot bot)
        {
            var oldBoss = Boss; var oldTrack = Track;
            float t = 0f;
            while (t < 30f && ((oldBoss != null && oldBoss) || (oldTrack != null && oldTrack))) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < 30f && (LevelManager.Instance == null || !LevelManager.Instance.IsStarted || bot.Team.Count < 3)) { t += Time.unscaledDeltaTime; yield return null; }
            yield return bot.Wait(0.6f);
            bot.Log("scene reloaded (checkpoint " + Ck + ")");
        }

        // ================================================================== Porkchop
        static ShifterCharacter Specialist(TeamBot bot, int p) => p == 0 ? (bot.GunOwner ?? bot.Brute) : p == 1 ? bot.Brute : bot.Brainiac;

        static IEnumerator Fight(TeamBot bot)
        {
            var boss = Boss;
            if (boss.State == BossState.Dormant)
            {
                // off the deck: the gun owner goes to the P1 bait spot, the others park by their phase's bay
                var gun = Specialist(bot, 0);
                yield return bot.Switch(gun);
                var routines = new List<IEnumerator>();
                foreach (var c in bot.Team)
                {
                    bool left = c == gun || c.Archetype == Archetype.Sprinter;
                    float lane = c == gun ? 0f : c.Archetype == Archetype.Brute ? -0.7f : 0.7f;
                    var gap = Deck(left ? -1.3f : 1.3f, lane);
                    var wing = Deck(left ? -3.4f : 3.4f, lane);
                    var inArena = new Vector3(left ? -3.5f : 3.5f, 0f, 124.5f + lane);
                    // routes around the pillars and the raised bays
                    BotTarget[] route;
                    if (c == gun) route = new BotTarget[] { gap, wing, inArena, new Vector3(-6.0f, 0f, 128.0f), "p1.bait" };
                    else if (c.Archetype == Archetype.Brute) route = new BotTarget[] { gap, wing, inArena, new Vector3(4.5f, 0f, 132.5f), "park.1" };
                    else if (c.Archetype == Archetype.Brainiac) route = new BotTarget[] { gap, wing, inArena, new Vector3(3.5f, 0f, 137f), "park.2" };
                    else route = new BotTarget[] { gap, wing, inArena, new Vector3(-3.5f, 0f, 137f), "park.3" };
                    routines.Add(bot.WalkPath(c, c == gun, route));
                }
                yield return bot.Parallel(routines.ToArray());
                yield return bot.WaitUntil(() => boss.IsFighting, 8f, "PORKCHOP fight started");
                yield return bot.Expect(() => Ck >= 4, "C4 active (P1)", 2f);
            }
            if (Flag("-l9Idle") && TeamBot.CurrentTeamKey == "1" && boss.Phase == 0) yield return IdleCheck(bot, boss);
            for (int p = Mathf.Max(0, boss.Phase); p < 3; p++)
            {
                boss = Boss;
                yield return PhaseRun(bot, boss, p);
                if (bot.Failed) yield break;
                if (p < 2)
                {
                    yield return bot.ExpectBossPhase(boss, p + 1, 12f);
                    yield return bot.Expect(() => Ck >= 5 + p, "C" + (5 + p) + " active (P" + (p + 2) + ")", 6f);
                    if (Flag("-l9Restore") && TeamBot.CurrentTeamKey == "1")
                    {
                        bot.Log("EXTRA: Restart Checkpoint during P" + (p + 2));
                        yield return bot.WaitUntil(() => boss.State == BossState.Fighting, 6f, "P" + (p + 2) + " fighting");
                        GameSession.Instance.RestartFromCheckpoint();
                        yield return WaitReload(bot);
                        yield return bot.SetAIMode(TeamAIMode.Stay);
                        var nb = Boss;
                        yield return bot.WaitUntil(() => nb != null && nb.IsFighting, 8f, "boss auto-began after the restore");
                        yield return bot.Expect(() => nb.Phase == p + 1, "restored at P" + (p + 2) + " (checkpoint " + Ck + ")", 1f);
                    }
                }
            }
            boss = Boss;
            yield return bot.ExpectBossDefeated(boss, 10f);
            yield return Shot(bot, "08_defeated");
        }

        /// <summary>Per phase: a point from which the bait spot is reached without brushing its pillar or bay.</summary>
        static readonly Vector3[] Approach = { new Vector3(-6.0f, 0f, 128.0f), new Vector3(6.0f, 0f, 128.0f), new Vector3(9.3f, 0f, 141.8f) };
        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        static IEnumerator PhaseRun(TeamBot bot, L9PorkchopBoss boss, int p)
        {
            var s = Specialist(bot, p);
            string bait = "p" + (p + 1) + ".bait", side = "p" + (p + 1) + ".side";
            int tries = 0;
            while (boss.Phase == p && !boss.IsDefeated && boss.State != BossState.PhaseTransition && tries < 6)
            {
                tries++;
                if (bot.Active != s) yield return bot.Switch(s);
                var bp = LevelMark.Position(bait);
                if (Flat(s.Feet - bp) > 5f) yield return bot.WalkPath(s, true, Approach[p], bait);
                else yield return bot.WalkTo(s, bait, true, 0.5f, 12f);
                if (boss.ChargeLocked && !boss.IsVulnerable && boss.Phase == p)
                {
                    // a charge locked on our old spot while we walked here: let it pass, then bait the next one
                    int c0 = boss.Charges;
                    bot.Log("P" + (p + 1) + " try " + tries + ": charge already locked on arrival - waiting for the next one");
                    yield return bot.WaitUntil(() => boss.Charges > c0 || boss.IsVulnerable || boss.Phase != p, 7f, "P" + (p + 1) + " next charge", false);
                }
                yield return bot.WaitUntil(() => boss.ChargeLocked || boss.IsVulnerable || boss.Phase != p, 9f, "P" + (p + 1) + " charge locked", false);
                if (boss.Phase != p) break;
                if (!boss.IsVulnerable) yield return StepAside(bot, s, p, bait, side);
                if (tries == 1 && p == 0) yield return Shot(bot, "05_charge");
                yield return bot.WaitUntil(() => boss.IsVulnerable || boss.Phase != p, 4f, "P" + (p + 1) + " dizzy", false);
                if (!boss.IsVulnerable) { bot.Log("P" + (p + 1) + " try " + tries + ": no dizzy - retry"); continue; }
                if (tries == 1 && p == 0) yield return Shot(bot, "06_dizzy");
                if (p < 2 && Flat(s.Feet - LevelMark.Position(bait)) < 2.5f) yield return StepAside(bot, s, p, bait, side);   // out of the pillar's shadow first
                if (bot.Failed) yield break;
                if (p == 0)
                {
                    // the plug is on Porkchop's LEFT flank: step out to where it faces, then shoot
                    var body = boss.Body != null ? boss.Body : boss.transform;
                    var n = PlugNormal(boss);
                    var stand = PlugStand(boss);
                    var rel = s.Feet - boss.PlugPoint; rel.y = 0f;
                    float facing = rel.sqrMagnitude > 0.01f ? Vector3.Dot(rel.normalized, n) : 0f;
                    bot.Log("P1 try " + tries + ": body " + body.position.ToString("F1") + " plug " + boss.PlugPoint.ToString("F1") + " normal " + n.ToString("F2")
                        + " shooter " + s.Feet.ToString("F1") + " facing " + facing.ToString("0.00") + " visible " + PlugVisible(s, boss) + " attempts " + boss.Plug.Attempts);
                    if (facing < 0.75f || !PlugVisible(s, boss))
                    {
                        var rb = s.Feet - body.position; rb.y = 0f;
                        if (Vector3.Dot(rb, n) < 0.5f)
                        {
                            // not on the plug's side: round the snout end or the rump end, whichever is nearer
                            var f = body.forward; f.y = 0f; f.Normalize();
                            float end = Vector3.Dot(rb, f) >= 0f ? 2.2f : -3.0f;
                            var w = body.position + n * 3.2f + f * end; w.y = 0f;
                            w.x = Mathf.Clamp(w.x, -14.2f, 14.2f); w.z = Mathf.Clamp(w.z, 123.2f, 151.2f);
                            yield return bot.WalkPath(s, new BotTarget[] { w, stand }, true, 0.7f, 4f);
                        }
                        else yield return bot.WalkTo(s, stand, true, 0.7f, 4.5f);
                        if (bot.Failed) yield break;
                    }
                    yield return bot.ShootAt(s, boss.Plug, () => boss.Phase != 0 || !boss.IsVulnerable, 8, 5f);
                    bot.Log("P1 try " + tries + ": plug attempts " + boss.Plug.Attempts + " landed " + boss.Plug.HitsLanded + " phase " + boss.Phase);
                }
                else if (p == 1)
                {
                    bot.Log("P2 try " + tries + ": snout " + boss.Snout.transform.position.ToString("F1") + " stand " + boss.PunchStandFor(s.Feet).ToString("F1"));
                    yield return bot.Interact(s, boss.Snout, (BotTarget)boss.PunchStandFor(s.Feet), true, 5f);
                }
                else
                {
                    if (tries == 1) yield return Shot(bot, "07_earhack");
                    var term = boss.EarTerminal != null ? boss.EarTerminal.GetComponentInChildren<Terminal>() : null;
                    bot.Log("P3 try " + tries + ": ear terminal " + (boss.EarTerminal != null ? boss.EarTerminal.position.ToString("F1") : "-") + " stand " + boss.HackStandPoint.ToString("F1") + " brainiac " + s.Feet.ToString("F1"));
                    yield return bot.Hack(s, term, (BotTarget)boss.HackStandPoint);
                }
                yield return bot.WaitUntil(() => boss.Phase != p || boss.State == BossState.PhaseTransition || boss.IsDefeated || !boss.IsVulnerable, 6f, "P" + (p + 1) + " hit", false);
            }
            bot.Log("P" + (p + 1) + " done after " + tries + " tries; pillar hits " + boss.PillarHits + ", charges " + boss.Charges + "; hp " + HpLine(bot));
        }

        /// <summary>Bait spot -> side spot. The south bait spots sit between their pillar and the bay: pass south of the pillar.</summary>
        static IEnumerator StepAside(TeamBot bot, ShifterCharacter s, int p, string bait, string side)
        {
            if (p < 2)
            {
                float sx = Mathf.Sign(LevelMark.Position(bait).x);
                yield return bot.WalkPath(s, new BotTarget[] { new Vector3(sx * 8.4f, 0f, 127.9f), side }, true, 0.6f, 4f);
            }
            else yield return bot.WalkTo(s, side, true, 0.6f, 6f);
        }

        /// <summary>3 m out along the belly plug's outward direction (from the body centre), clamped inside the arena.</summary>
        static Vector3 PlugNormal(L9PorkchopBoss boss)
        {
            var body = boss.Body != null ? boss.Body : boss.transform;
            var n = boss.PlugPoint - body.position; n.y = 0f;
            if (n.sqrMagnitude < 0.01f) n = -body.right;
            return n.normalized;
        }

        /// <summary>A clear line from the shooter's chest to the plug (nothing but the plug in the way)?</summary>
        static bool PlugVisible(ShifterCharacter s, L9PorkchopBoss boss)
        {
            if (boss.Plug == null) return true;
            var from = s.Center + Vector3.up * 0.3f;
            if (!Physics.Linecast(from, boss.PlugPoint, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider != null && (hit.collider.transform == boss.Plug.transform || hit.collider.transform.IsChildOf(boss.Plug.transform));
        }

        static Vector3 PlugStand(L9PorkchopBoss boss)
        {
            var n = PlugNormal(boss);
            var p = boss.PlugPoint + n * 2.4f;
            p.x = Mathf.Clamp(p.x, -14.2f, 14.2f); p.z = Mathf.Clamp(p.z, 123.2f, 151.2f); p.y = 0f;
            return p;
        }

        static string HpLine(TeamBot bot)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in bot.Team) sb.Append(c.Archetype.ToString()[0]).Append('=').Append(c.State).Append(' ');
            return sb.ToString();
        }

        /// <summary>Boss extra: 120 s with everyone on the loading bays (the active one too): nobody hurt.</summary>
        static IEnumerator IdleCheck(TeamBot bot, L9PorkchopBoss boss)
        {
            bot.Log("EXTRA: 120 s idle on the loading bays");
            // everyone climbs the bay nearest to where they wait: gun -> SW (0), Brute -> SE (1), Brainiac -> NE (2), Sprinter -> NW (3)
            var gun = Specialist(bot, 0);
            var routines = new List<IEnumerator>();
            foreach (var c in bot.Team)
            {
                int bay = c == gun ? 0 : c.Archetype == Archetype.Brute ? 1 : c.Archetype == Archetype.Brainiac ? 2 : 3;
                float sx = (bay == 0 || bay == 3) ? -13.25f : 13.25f;
                bool south = bay < 2;
                routines.Add(bot.WalkPath(c, new BotTarget[] { new Vector3(sx, 0f, south ? 131.2f : 142.8f), new Vector3(sx, 1.6f, south ? 126.2f : 147.8f), "boss.safe." + bay }, true, 0.8f, 10f));
            }
            yield return bot.Parallel(routines.ToArray());
            int charges = boss.Charges;
            float t = 0f; int bad = 0;
            while (t < 120f)
            {
                t += Time.deltaTime;
                foreach (var c in bot.Team) if (c.State == CharacterState.Downed || c.State == CharacterState.Captured || c.State == CharacterState.Lost) bad++;
                yield return null;
            }
            yield return bot.Expect(() => bad == 0, "120 s on the bays: nobody Downed/Captured/Lost");
            bot.Log("idle: charges during the idle " + (boss.Charges - charges));
        }

        // ================================================================== exit
        /// <summary>Walk everyone to the north lanes between the tipped-over pig / exhibit (x ~ 0) and the north pillars (x = +-7),
        /// so the straight exit walk does not snag on them.</summary>
        static IEnumerator ToExitLanes(TeamBot bot)
        {
            var routines = new List<IEnumerator>();
            foreach (var c in bot.Team)
            {
                if (c == null || !c.IsAlive) continue;
                var f = c.Feet;
                if (f.z > 148.5f && Mathf.Abs(f.x) < 4f) continue;
                float sx = f.x >= 0f ? 1f : -1f;
                var pts = new List<BotTarget>();
                if (f.z < 140f && Mathf.Abs(f.x) < 4.2f) pts.Add(new Vector3(sx * 4.6f, 0f, Mathf.Clamp(f.z, 126f, 140f)));
                if (f.y > 1f) pts.Add(new Vector3(sx * 13.25f, 0f, f.z < 137f ? 131.2f : 142.8f));     // down the bay stairs first
                pts.Add(new Vector3(sx * 4.4f, 0f, 140.6f));
                pts.Add(new Vector3(sx * 3.9f, 0f, 149.2f));
                routines.Add(bot.WalkPath(c, pts, false, 0.8f, 8f));
            }
            yield return bot.Parallel(routines.ToArray());
        }

        static IEnumerator Exit(TeamBot bot)
        {
            yield return bot.Wait(0.5f);
            float t = 0f;
            while (t < 2.5f && !s_cutscene) { t += Time.deltaTime; yield return null; }    // C07 may start (story ON)
            yield return WaitStory(bot);
            yield return bot.ExpectPowered("arena.door", true, 4f);
            yield return Shot(bot, "09_van");
            yield return ToExitLanes(bot);
            yield return bot.EnterExit("exit", false, 40f);
            yield return bot.ExpectLevelComplete(12f);
            string[] want = { "ch08.board", "ch08.st1", "ch08.st2", "ch08.st3", "ch08.boss", "ch08.p1", "ch08.p2", "ch08.p3", "ch08.outro" };
            var miss = new List<string>();
            foreach (var w in want) if (!s_beats.Contains(w)) miss.Add(w);
            if (Flag("-l9Busted") && TeamBot.CurrentTeamKey == "1" && !s_beats.Contains("ch08.busted")) miss.Add("ch08.busted");
            yield return bot.Expect(() => miss.Count == 0, "every ch08 beat fired" + (miss.Count > 0 ? " (missing " + string.Join(",", miss) + ")" : ""));
        }
    }
}
