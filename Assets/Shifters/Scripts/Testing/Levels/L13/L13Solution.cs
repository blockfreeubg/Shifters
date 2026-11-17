// =====================================================================================
//  SHIFTERS - L13Solution.cs   [LEVEL 13 - HOSTILE TAKEOVER]  TeamBot solution (Run A "TEAM", no debug assists,
//  no armory = the tool-less fallback). Characters by archetype; the gun by bot.GunOwner.
//    S0  gather at C1; Sprinter parks in break area SW, Brainiac at the W stairs foot, the Brute baits.
//    P1  x3: the fist tells at the Brute -> he sprints 4 m aside -> the fist sticks -> he punches a knuckle.
//    P2  the Sprinter presses the BUMP RUN button and TURBOs east through G2 + G3 -> bumps -> the mech trips.
//    P3  a parker P (Sprinter, or the Brute if the Sprinter has the gun) stays active in a break area while
//        everyone else walks; the Brainiac hacks W, crosses (inactive), hacks E; the GunOwner shoots the
//        antenna from break area N (the kazoo holds there).
//    P4  the Brute TAUNTs the SOLO POD from the hub, grabs the arguing pod, carries it to DETENTION while the
//        Brainiac hacks the INTAKE; drop inside -> DETAINED -> exit through the north door.
//  Run: $SP/tools/run_level_test.sh <project> 13 1,2,3,MIN,MAX     (BOT_STORY=1 for the story-ON run)
//  Seed 1 also takes the key-moment screenshots (l13_*.png).
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L13Solution
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            StaticChecks(ctx);
            bool story = BotStory.CommandLineStoryOn;
            if (story)
            {
                StoryProbe.Install();
                // C12 is the chapter's IntroScript (CampaignCatalog): the campaign plays it on a campaign start; a direct
                // scene load (this test) does not, so Run D plays it here once, exactly as the director would.
                yield return ctx.Wait(1f);
                if (!StoryProbe.Started("C12") && StoryPlayer.Instance != null)
                {
                    ctx.Log("STORY: direct scene load -> playing the IntroScript C12 explicitly");
                    StoryPlayer.Instance.Play("C12");
                    for (float t = 0f; t < 30f && StoryPlayer.Instance.IsPlaying; t += Time.unscaledDeltaTime) yield return null;
                }
            }
            yield return TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, Options(), "L13");
            if (story)
            {
                for (float t = 0f; t < 20f && StoryPlayer.Instance != null && StoryPlayer.Instance.IsPlaying; t += Time.unscaledDeltaTime) yield return null;
                StoryProbe.Report(ctx);
            }
        }

        /// <summary>Run D "STORY" (-botStory 1): records scripts, beats, lines and cutscene toggles, then checks G13.</summary>
        static class StoryProbe
        {
            static readonly System.Collections.Generic.List<string> s_started = new System.Collections.Generic.List<string>();
            static readonly System.Collections.Generic.List<string> s_beats = new System.Collections.Generic.List<string>();
            static int s_lines, s_cutOn, s_cutOff;
            public static void Install()
            {
                s_started.Clear(); s_beats.Clear(); s_lines = s_cutOn = s_cutOff = 0;
                StoryPlayer.ScriptStarted += id => s_started.Add(id);
                GameEvents.StoryBeat += id => s_beats.Add(id);
                GameEvents.StoryLineShown += l => s_lines++;
                GameEvents.CutsceneChanged += on => { if (on) s_cutOn++; else s_cutOff++; };
            }
            public static bool Started(string id) => s_started.Contains(id);
            static int Count(System.Collections.Generic.List<string> l, string id) { int n = 0; foreach (var x in l) if (x == id) n++; return n; }
            public static void Report(DevPlayContext ctx)
            {
                ctx.Log("STORY scripts: " + string.Join(", ", s_started));
                ctx.Log("STORY beats: " + string.Join(", ", s_beats));
                ctx.Log("STORY lines shown " + s_lines + ", cutscene on " + s_cutOn + " / off " + s_cutOff);
                ctx.Check(Count(s_started, "C12") + Count(s_started, "ch13.start") >= 1, "story: C12 played at chapter start");
                ctx.Check(Count(s_beats, "scare.ch13.cutout") == 1, "story: ch13.cutout scare once");
                ctx.Check(Count(s_beats, "scare.ch13.wreck") == 1, "story: ch13.wreck scare once");
                ctx.Check(Count(s_started, "C13") + Count(s_started, "ch13.caught") >= 1, "story: C13 played on capture");
                foreach (var id in new[] { "ch13.p1", "ch13.stuck", "ch13.punch", "ch13.arm", "ch13.p2", "ch13.bumps", "ch13.legs", "ch13.p3", "ch13.firewall", "ch13.eject", "ch13.chase", "ch13.argue", "ch13.carried", "ch13.intake", "ch13.caught" })
                    ctx.Check(Count(s_beats, id) >= 1, "story: beat " + id + " fired");
                ctx.Check(s_lines >= 15, "story: " + s_lines + " lines shown (>= 15)");
                ctx.Check(s_cutOn == s_cutOff, "story: every blocking cutscene ended (on " + s_cutOn + " / off " + s_cutOff + ")");
                var sp = StoryPlayer.Instance;
                ctx.Check(sp == null || !sp.IsPlaying, "story: every script finished within 20 s of the exit (" + (sp != null ? sp.CurrentId : "-") + ")");
                ctx.Check(Mathf.Approximately(Time.timeScale, 1f) && Camera.main != null && Camera.main.enabled, "story: timescale + main camera restored");
            }
        }

        public static BotOptions Options() => new BotOptions();

        static bool ShotsOn => TeamBot.CurrentTeamKey == "1";
        static void Shot(TeamBot bot, string name) { if (ShotsOn && bot.Ctx != null) bot.Ctx.Shot(name); }

        /// <summary>Seed 1 only: a still from a free camera (no HUD) framing a key moment.</summary>
        static void Overview(TeamBot bot, string name, Vector3 pos, Vector3 look)
        {
            if (!ShotsOn || bot.Ctx == null) return;
            var go = new GameObject("L13ShotCam");
            var cam = go.AddComponent<Camera>();
            go.transform.position = pos; go.transform.LookAt(look);
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 400f; cam.enabled = false;
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            bot.Ctx.Shot(name, cam);
            Object.Destroy(go);
        }

        /// <summary>Static checks on the built scene (spec + boss rules); logged into the result.</summary>
        public static void StaticChecks(DevPlayContext ctx)
        {
            var boss = Object.FindAnyObjectByType<L13OneManBandBoss>();
            ctx.Check(boss != null, "L13: OneManBand brain present");
            if (boss == null) return;
            ctx.Check(boss.ValidateSpecs(out string e1), "L13: boss specs valid (B1/B3) " + e1);
            if (boss.Arena != null)
            {
                ctx.Check(boss.Arena.ValidateNoKillZones(out string e2), "L13: no KillZone in the arena " + e2);
                ctx.Check(boss.Arena.Zones.Count == 5, "L13: 5 break areas (" + boss.Arena.Zones.Count + ")");
                // P2 lane hitbox (r 10.5 +- 2) vs break areas: >= 2.4 m everywhere
                float minD = 99f;
                for (int i = 0; i < 128; i++)
                {
                    var p = L13Builder.LanePoint(i * Mathf.PI * 2f / 128f);
                    foreach (var z in boss.Arena.Zones) minD = Mathf.Min(minD, z.Distance(p));
                }
                ctx.Check(minD - 2f >= 2.4f, "L13: skate hitbox clears every break area by " + (minD - 2f).ToString("0.00") + " m (>= 2.4)");
                // P1 clamp: fist circles never within 0.5 m of a break area for any target outside the HOLD zone
                int bad = 0, n = 0;
                for (float x = -18.5f; x <= 18.5f; x += 0.5f)
                    for (float z = 8.5f; z <= 51.5f; z += 0.5f)
                    {
                        var p = new Vector3(x, 0f, z);
                        if (boss.InHold(p)) continue;
                        n++;
                        var aim = ClampProbe(boss, p);
                        foreach (var zz in boss.Arena.Zones) if (zz.Distance(aim) < 2.5f + 0.5f - 0.01f) { bad++; break; }
                    }
                ctx.Check(bad == 0, "L13: P1 fist clamp keeps every circle >= 0.5 m off the break areas (" + n + " targets, " + bad + " bad)");
            }
            ctx.Check(boss.Phases.Length == 4 && boss.Phases[0].Pattern[0].Windup >= Tuning.BossTelegraphFirst && boss.Phases[2].Pattern[0].Windup >= Tuning.BossTelegraphMin,
                "L13: telegraphs P1 1.5 / P2 1.2 / P3 1.2 s");
            ctx.Check(boss.Pod != null && boss.Detention != null && boss.BumpRun != null, "L13: pod, DETENTION and bump run wired");
            var budget = Object.FindObjectsByType<Rigidbody>().Length;
            ctx.Log("L13 rigidbodies in scene: " + budget + " (G8 <= 25), renderers: " + Object.FindObjectsByType<Renderer>().Length);
        }

        static Vector3 ClampProbe(L13OneManBandBoss boss, Vector3 p)
        {
            var m = typeof(L13OneManBandBoss).GetMethod("ClampFist", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return m != null ? (Vector3)m.Invoke(boss, new object[] { p }) : p;
        }

        // ================================================================== solution
        public static IEnumerator Solve(TeamBot bot) => Stages(bot, 0, 5);

        /// <summary>Stages 0 S0, 1 P1, 2 P2, 3 P3, 4 P4, 5 exit (from..to). tag = P4 by the Sprinter TAG instead of the
        /// TAUNT; stopAfterPunches > 0 ends P1 after that many punches (Restart Checkpoint test).</summary>
        public static IEnumerator Stages(TeamBot bot, int from, int to, bool tag = false, int stopAfterPunches = 0)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            var gun = bot.GunOwner;
            var boss = Object.FindAnyObjectByType<L13OneManBandBoss>();
            bot.Log("team: " + bot.TeamSummary() + " gun=" + (gun != null ? gun.Archetype.ToString() : "none"));
            if (boss == null) { bot.Fail("no L13OneManBandBoss in the scene"); yield break; }
            var cm = CheckpointManager.Instance;

            // ---------------- S0
            if (from <= 0 && to >= 0)
            {
            if (BotStory.CommandLineStoryOn)
            {
                // Run D: the Sprinter peeks into the supply closet -> the cardboard-Max cutout scare (ch13.cutout)
                yield return bot.Switch(spr);
                yield return bot.WalkTo(spr, new Vector3(4.6f, 0f, -5.5f), false, 0.6f, 8f);
                yield return bot.Wait(4f);
            }
            yield return bot.GatherAt("checkpoint.1");
            yield return bot.Expect(() => CheckpointManager.Instance != null && CheckpointManager.Instance.CurrentIndex == 1, "C1 active", 3f);
            Shot(bot, "l13_s0_landing");
            Overview(bot, "l13_ov_s0", new Vector3(0f, 6.5f, -9.5f), new Vector3(0f, 1.5f, 4f));
            if (bot.Failed) yield break;
            yield return bot.Switch(brute);
            var portalW = new Vector3(-4f, 0f, 10.5f);
            yield return bot.Parallel(bot.WalkPath(spr, false, portalW, "boss.safe.1"), bot.WalkTo(brute, "s1.bait", true));
            if (bot.Failed) yield break;

            }
            // ---------------- P1 THE FIST x3 (the Brainiac walks to the W stairs meanwhile, inactive)
            if (from <= 1 && to >= 1)
            {
            var foot = new Vector3(-17.75f, 0f, 18.5f);
            var brainWalk = brain.Feet.z < 8f ? bot.WalkPath(brain, false, new Vector3(-4f, 0f, 10.5f), "s3.stairsW.foot")
                          : BossFootprint.Planar(brain.Feet, foot) > 1.5f ? bot.WalkTo(brain, "s3.stairsW.foot") : bot.Wait(0.1f);
            yield return bot.Parallel(brainWalk, P1Loop(bot, boss, brute, stopAfterPunches));
            if (bot.Failed) yield break;
            if (stopAfterPunches > 0 && boss.Phase == 0) yield break;
            yield return bot.ExpectBossPhase(boss, 1, 8f);
            yield return bot.Expect(() => cm != null && CheckpointManager.Instance.CurrentIndex == 2, "C2 active at P2", 2f);
            if (bot.Failed) yield break;

            }
            // ---------------- P2 THE SKATES: BUMP RUN
            if (from <= 2 && to >= 2)
            {
            for (int attempt = 0; attempt < 3 && !boss.BumpsUp && boss.Phase == 1; attempt++)
            {
                yield return bot.Interact(spr, "s2.button", (BotTarget)"s2.stand");
                if (bot.Failed) yield break;
                yield return bot.WalkTo(spr, "s2.runout", true, 0.8f, 6f);
                if (bot.Failed) yield break;
                if (!boss.BumpsUp) { bot.Log("bump run late (attempt " + (attempt + 1) + ")"); yield return bot.WalkTo(spr, "s2.stand", true); }
            }
            yield return bot.ExpectPowered("s2.bumps", true, 1f);
            if (bot.Failed) yield break;
            yield return bot.WaitUntil(() => boss.Tripping || boss.State == BossState.PhaseTransition || boss.Phase > 1, 12f, "mech trips over the bumps", false);
            Shot(bot, "l13_p2_trip");
            Overview(bot, "l13_ov_skates", new Vector3(0f, 17f, 6f), new Vector3(0f, 0f, 31f));
            yield return bot.WaitBoss(boss, BossState.PhaseTransition, 8f);
            if (bot.Failed) yield break;

            }
            // ---------------- P3 THE BRAIN DOME
            if (from <= 3 && to >= 3)
            {
            yield return bot.ExpectBossPhase(boss, 2, 8f);
            yield return bot.Expect(() => CheckpointManager.Instance.CurrentIndex == 3, "C3 active at P3", 2f);
            if (bot.Failed) yield break;
            var parker = gun == spr ? brute : spr;
            yield return bot.WalkTo(spr, "boss.safe.2", true);
            if (parker == brute) yield return bot.WalkTo(brute, "boss.safe.1", true);
            yield return bot.Switch(parker);
            yield return bot.WalkPath(brain, false, "s3.stairsW.foot", "s3.stairsW.top", "s3.balconyW.stand");
            yield return bot.Hack(brain, "s3.terminalW", (BotTarget)"s3.balconyW.stand");
            if (bot.Failed) yield break;
            Shot(bot, "l13_p3_firewall_w");
            Overview(bot, "l13_ov_dome", new Vector3(-6f, 8f, 24f), new Vector3(0f, 5f, 42.5f));
            yield return bot.Switch(parker);
            var cross = bot.WalkPath(brain, true, "s3.stairsW.top", "s3.stairsW.foot", "s3.cross.2", "s3.cross.1", "s3.stairsE.foot", "s3.stairsE.top", "s3.balconyE.stand");
            IEnumerator gunWalk = null, bruteWalk = null;
            if (gun == brute) gunWalk = brute.Feet.x > 10f ? bot.WalkPath(brute, false, "s3.cross.1", "s3.east.1", "s5.path.1", "s3.shootSpot") : bot.WalkPath(brute, false, "s5.path.1", "s3.shootSpot");
            else if (gun == spr) gunWalk = bot.WalkPath(spr, false, "s3.cross.1", "s3.east.1", "s5.path.1", "s3.shootSpot");
            if (gun != brute && parker != brute) bruteWalk = bot.WalkTo(brute, "s4.hub");
            yield return bot.Parallel(cross, gunWalk ?? bot.Wait(0.1f), bruteWalk ?? bot.Wait(0.1f));
            if (bot.Failed) yield break;
            yield return bot.Hack(brain, "s3.terminalE", (BotTarget)"s3.balconyE.stand");
            yield return bot.Expect(() => boss.Hits == 2, "both firewalls hacked (2/3)", 1.5f);
            yield return bot.ExpectPowered("s3.firewall", true, 2f);
            if (bot.Failed) yield break;
            if (gun == brain)
            {
                yield return bot.Switch(parker);
                yield return bot.WalkPath(brain, true, "s3.stairsE.top", "s3.stairsE.foot", "s3.cross.1", "s3.east.1", "s5.path.1", "s3.shootSpot"); // sprint: MAX time margin (critic)
            }
            yield return bot.Switch(gun);
            if (boss.Antenna != null && ShotsOn)
            {   // key moment: the exposed antenna, framed from above/north-east (not the active camera mid-switch)
                var ap = boss.Antenna.transform.position;
                Debug.Log("[L13SHOT] antenna " + ap + " active=" + boss.Antenna.gameObject.activeInHierarchy + " gun " + gun.name + " feet " + gun.Feet + " boss " + boss.transform.position);
                Overview(bot, "l13_p3_antenna", ap + new Vector3(6f, 2.5f, 8f), ap + Vector3.down * 1.5f);
            }
            yield return bot.ShootAt(gun, boss.Antenna, () => boss.Phase >= 3 || boss.State == BossState.PhaseTransition, 10, 15f);
            if (bot.Failed) yield break;

            }
            // ---------------- P4 THE CAPTURE (TAUNT route)
            if (from <= 4 && to >= 4)
            {
            yield return bot.ExpectBossPhase(boss, 3, 8f);
            yield return bot.Expect(() => CheckpointManager.Instance.CurrentIndex == 4, "C4 active at P4", 2f);
            if (bot.Failed) yield break;
            var pod = boss.Pod;
            bool brainUp = Vector3.Distance(brain.Feet, new Vector3(17.5f, 2.5f, 28f)) < 3f;
            var brainToIntake = brainUp
                ? bot.WalkPath(brain, false, "s3.stairsE.top", "s3.stairsE.foot", "s3.cross.1", "s3.east.1", "s4.intake.stand")
                : bot.WalkPath(brain, false, "s5.path.1", "s4.intake.stand");
            var bruteToHub = brute.Feet.z > 39f ? bot.WalkPath(brute, true, new Vector3(5.5f, 0f, 46f), new Vector3(4f, 0f, 37.5f)) : bot.WalkTo(brute, "s4.hub", true);
            var sprAside = spr != gun ? bot.WalkTo(spr, "s5.path.1", true) : bot.Wait(0.1f);
            yield return bot.Parallel(bruteToHub, brainToIntake, sprAside);
            if (bot.Failed) yield break;
            yield return bot.Switch(brute);
            yield return bot.WaitUntil(() => pod.enabled, 4f, "pod launched");
            if (tag)
            {
                yield return TagRoute(bot, spr, brute, pod);
                if (bot.Failed) yield break;
            }
            for (int tries = 0; tries < 4 && !pod.IsCarried; tries++)
            {
                yield return bot.WaitUntil(() => Vector3.Distance(brute.Feet, pod.transform.position) <= 8.5f || pod.IsMonologuing || pod.IsDizzy, 20f, "pod within taunt range", false);
                if (!pod.IsDizzy && Vector3.Distance(brute.Feet, pod.transform.position) > 8.5f)
                    yield return bot.WalkTo(brute, pod, true, 7f, 6f);
                if (!pod.IsDizzy)
                {
                    yield return bot.Taunt(brute);
                    yield return bot.WaitUntil(() => pod.IsDistracted || pod.IsDizzy, 1.5f, "pod argues", false);
                }
                if (pod.IsDistracted || pod.IsDizzy)
                {
                    yield return bot.Grab(brute, pod, 4f);
                    if (bot.Failed) yield break;
                }
            }
            yield return bot.Expect(() => pod.IsCarried, "the Brute carries the SOLO POD", 1f);
            if (bot.Failed) yield break;
            Shot(bot, "l13_p4_carry");
            Overview(bot, "l13_ov_carry", brute.Feet + new Vector3(-6f, 6f, -7f), brute.Feet + Vector3.up * 1.5f);
            var front = new Vector3(17f, 0f, 40f);
            yield return bot.Parallel(bot.WalkTo(brute, "s4.cellFront", true, 0.6f, 25f), HackWhenClose(bot, brain, brute, front, boss));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s4.cellDoor", true, 4f);
            if (bot.Failed) yield break;
            yield return bot.Wait(0.6f);
            yield return bot.WalkTo(brute, "s4.cell.drop", false, 0.5f, 6f);
            yield return bot.Drop(brute);
            yield return bot.WalkTo(brute, "s4.cellFront", false, 0.6f, 6f);
            yield return bot.ExpectPowered("s4.detained", true, 4f);
            Shot(bot, "l13_p4_detained");
            Overview(bot, "l13_ov_detained", new Vector3(13.5f, 4.2f, 35.5f), new Vector3(22f, 1.2f, 40f));
            yield return bot.ExpectBossDefeated(boss, 5f);
            yield return bot.Expect(() => CheckpointManager.Instance.CurrentIndex == 5, "C5 active after DETAINED", 3f);
            if (bot.Failed) yield break;

            }
            // ---------------- exit
            if (from <= 5 && to >= 5)
            {
            var team = bot.Team;
            var walks = new IEnumerator[team.Count];
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                walks[i] = c == null || !c.IsAlive ? bot.Wait(0.1f) : c.Feet.z > 44f && c.Feet.x < 13f ? bot.WalkTo(c, "s5.door", true) : bot.WalkPath(c, true, "s5.path.1", "s5.door");
            }
            yield return bot.Parallel(walks);
            if (bot.Failed) yield break;
            yield return bot.GatherAt("s5.lobby");
            Shot(bot, "l13_s5_lobby");
            yield return bot.EnterExit();
            yield return bot.ExpectLevelComplete();
            }
        }

        static IEnumerator P1Loop(TeamBot bot, L13OneManBandBoss boss, ShifterCharacter brute, int stopAfterPunches)
        {
            yield return bot.WaitBoss(boss, BossState.Fighting, 12f);
            yield return bot.ExpectBossPhase(boss, 0, 2f);
            if (bot.Failed) yield break;
            float t0 = Time.time;
            bool shotTell = false, shotStuck = false;
            while (boss.Phase == 0 && boss.State != BossState.PhaseTransition && boss.State != BossState.Defeated && Time.time - t0 < 90f)
            {
                yield return bot.WaitUntil(() => boss.FistStuck || boss.Phase != 0 || boss.State == BossState.PhaseTransition || FistWindup(boss), 14f, "fist tell", false);
                if (boss.Phase != 0 || boss.State == BossState.PhaseTransition) break;
                if (FistWindup(boss))
                {
                    var c = boss.FistCentre;
                    float dx = c.x > -4f ? -4.2f : 4.2f;
                    if (!shotTell) { shotTell = true; Shot(bot, "l13_p1_fist_tell"); Overview(bot, "l13_ov_fist_tell", new Vector3(c.x + 9f, 8f, c.z - 9f), c + Vector3.up * 2f); }
                    yield return bot.WalkTo(brute, new Vector3(c.x + dx, 0f, c.z), true, 0.35f, 6f);
                    if (bot.Failed) yield break;
                    yield return bot.WaitUntil(() => boss.FistStuck || !FistWindup(boss) && boss.Attacks.Current == null, 3f, "fist landed", false);
                }
                if (boss.FistStuck)
                {
                    int before = boss.Hits;
                    if (!shotStuck) { shotStuck = true; Shot(bot, "l13_p1_fist_stuck"); Overview(bot, "l13_ov_fist_stuck", boss.FistCentre + new Vector3(5f, 4.5f, -6f), boss.FistCentre + Vector3.up * 1.2f); }
                    var k = NearestKnuckle(boss, brute.Feet);
                    if (k != null) yield return bot.Interact(brute, k);
                    if (bot.Failed) yield break;
                    yield return bot.Expect(() => boss.Hits > before || boss.Phase > 0 || boss.State == BossState.PhaseTransition, "punch landed (" + (before + 1) + "/3)", 1.5f);
                    if (bot.Failed) yield break;
                    if (stopAfterPunches > 0 && boss.Phase == 0 && boss.Hits >= stopAfterPunches) yield break;
                }
            }
        }

        /// <summary>Run B: the Sprinter TURBO-tags the fleeing pod (>= 8 m/s), the Brute shadows and grabs it while DIZZY.</summary>
        static IEnumerator TagRoute(TeamBot bot, ShifterCharacter spr, ShifterCharacter brute, FleeBrain pod)
        {
            for (int tries = 0; tries < 4 && !pod.IsCarried; tries++)
            {
                yield return bot.Switch(spr);
                yield return bot.Chase(spr, pod, true, 1.3f, 15f, () => pod.IsDizzy);
                if (bot.Failed) yield break;
                yield return bot.WaitUntil(() => pod.IsDizzy, 1f, "pod dizzy after the tag", false);
                if (!pod.IsDizzy) continue;
                bot.Log("pod stunned by '" + pod.LastStunCause + "' (try " + (tries + 1) + ")");
                yield return bot.Expect(() => pod.LastStunCause == "tag", "the stun was a Sprinter TAG", 0.5f);
                if (bot.Failed) yield break;
                // the Brute closes in while the Sprinter re-tags whenever the pod wakes up
                float t0 = Time.time;
                while (!pod.IsCarried && Time.time - t0 < 12f)
                {
                    if (pod.IsDizzy && Vector3.Distance(brute.Feet, pod.transform.position) < 2.6f) { yield return bot.Grab(brute, pod, 3f); if (bot.Failed) yield break; break; }
                    if (pod.IsDizzy) yield return bot.WalkTo(brute, pod.transform.position, true, 1.6f, 6f);
                    else { yield return bot.Switch(spr); yield return bot.Chase(spr, pod, true, 1.3f, 10f, () => pod.IsDizzy); }
                    if (bot.Failed) yield break;
                }
            }
            yield return bot.Switch(brute);
        }

        static bool FistWindup(L13OneManBandBoss boss)
        {
            var c = boss.Attacks.Current;
            return boss.Phase == 0 && c != null && c.Kind == BossAttackKind.SlamZone && c.Stage == BossTelegraphStage.Windup;
        }

        static BossWeakPoint NearestKnuckle(L13OneManBandBoss boss, Vector3 p)
        {
            BossWeakPoint best = null; float bd = float.MaxValue;
            foreach (var k in boss.Knuckles)
            {
                if (k == null) continue;
                float d = (k.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        }

        static IEnumerator HackWhenClose(TeamBot bot, ShifterCharacter brain, ShifterCharacter brute, Vector3 front, L13OneManBandBoss boss)
        {
            yield return bot.WaitUntil(() => BossFootprint.Planar(brute.Feet, front) <= 8f, 25f, "Brute close to DETENTION");
            if (bot.Failed) yield break;
            yield return bot.Hack(brain, "s4.intake", (BotTarget)"s4.intake.stand");
        }
    }
}
