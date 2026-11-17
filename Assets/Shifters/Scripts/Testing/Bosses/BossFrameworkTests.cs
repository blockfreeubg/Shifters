// =====================================================================================
//  SHIFTERS - BossFrameworkTests.cs   [BOSSES module - CAMPAIGN 11.1 B-rules + 18.3, proven on TestBoss]
//  One Unity run (no scene file needed; everything is code-built):
//    devplay.sh <clone> bosses_all -devSetup Shifters.BossFrameworkTests.All -devSeconds 1
//  Parts (also callable alone): Static, Fight, Idle, Chase, Flee.
//    Static  B1/B3 spec validation (+ negatives), B6 ValidateSafeZones (+ negatives), B5 ValidateNoKillZones
//            (+ negative), B2 headless timeline hash (seeded UnityEngine.Random 1/2/3 must not matter),
//            B3 one attack at a time, B12 <= 10 projectiles.
//    Fight   seeds 1/2/3: TeamBot defeats MEGA BONK-BOT 9000 with the BossBot helpers (bait in a break area,
//            specialist waits INACTIVE next to the weak point, SHIFT on the window). Phase checkpoints 1/2/3
//            activate from script. Seed 1 also: telegraph screenshots + 3-channel check, mercy after 2 wipes,
//            and a simulated Restart Checkpoint (rebuild arena, RestoreIndex) -> RestorePhase(1).
//    Idle    120 s with the team parked in break areas through all three phases: nobody Downed/Captured/Lost,
//            nobody even scratched, the boss kept attacking every kind.
//    Chase   ChaseTrack: all aboard -> moving -> station (checkpoint) -> deliberate BUSTED -> 2 s -> restore at
//            the station (gap = StartGap) -> clear -> end of line; GameEvents.ChaseChanged raised.
//    Flee    FleeBrain (SOLO POD): flees, monologue window (~3 s, every 7 s), dizzy 3 s, argue on taunt, shot
//            stun, carried by the Brute.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class BossFrameworkTests
    {
        sealed class Rig { public Transform Root; public BossArena Arena; public MegaBonkBot Boss; public Checkpoint[] Cps; }

        static Rig s_rig;
        static Transform s_teamRoot;
        static BossTestMonitor s_mon;
        static int s_seed, s_firstSeed;
        static readonly Dictionary<int, uint> s_liveHash = new Dictionary<int, uint>();
        static Camera s_cam;
        // chase
        static ChaseTrack s_chase; static BossTestSignal s_clear; static Transform s_chaseRoot; static int s_restarts; static float s_bustAt, s_restartAt;
        // flee
        static FleeBrain s_pod; static Transform s_fleeRoot;

        static readonly Vector3 BossPos = new Vector3(0f, 0f, 6f);
        static readonly Vector3 ZoneL = new Vector3(-9f, 0f, -8f), ZoneR = new Vector3(9f, 0f, -8f), ZoneC = new Vector3(0f, 0f, -11.5f);
        static readonly Vector3 GunSpot = new Vector3(-4f, 0f, -1f);       // 7 m from the heart, outside the break areas
        static readonly Vector3 PunchSpot = new Vector3(0f, 0f, 3.6f);     // in front of the belly plate (world z 4.88)
        static readonly Vector3 PunchWait = new Vector3(-1.5f, 0f, 2.4f);  // the Brute waits here (inactive) for the window
        static readonly Vector3 HackSpot = new Vector3(-2.6f, 0f, 2.6f);   // in front of the hack port (world -2.6, 4.4)
        static readonly Vector3 ChaseOrigin = new Vector3(100f, 0f, 0f), FleeOrigin = new Vector3(-100f, 0f, 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_rig = null; s_teamRoot = null; s_mon = null; s_liveHash.Clear(); s_cam = null; s_chase = null; s_clear = null; s_chaseRoot = null; s_pod = null; s_fleeRoot = null; }

        // ================================================================== entry points
        public static IEnumerator All(DevPlayContext ctx)
        {
            yield return Static(ctx);
            yield return Fight(ctx);
            yield return Idle(ctx);
            yield return Chase(ctx);
            yield return Flee(ctx);
            Cleanup();
            ctx.Log("[BOSS] all parts done");
        }

        /// <summary>Part A (Static + Fight) / Part B (Idle + Chase + Flee): the same suite split for the 900 s run limit.</summary>
        public static IEnumerator PartA(DevPlayContext ctx) { yield return Static(ctx); yield return Fight(ctx); Cleanup(); }
        public static IEnumerator PartB(DevPlayContext ctx) { yield return Idle(ctx); yield return Chase(ctx); yield return Flee(ctx); Cleanup(); }

        // ================================================================== static (no team)
        public static IEnumerator Static(DevPlayContext ctx)
        {
            Env();
            BossBase.ClearMercyMemory();
            var rig = BuildArena();
            yield return null;
            var boss = rig.Boss;
            ctx.Check(boss.State == BossState.Dormant, "[BOSS] TestBoss is Dormant without an active intern in the arena");
            ctx.Check(boss.ValidateSpecs(out string e1), "[BOSS] B1/B3 ValidateSpecs(MEGA BONK-BOT) " + e1);
            // explicit per-spec telegraph listing (B3: >= 1.5 s in phase 0, >= 1.2 s after)
            var sb = new System.Text.StringBuilder(); bool tellOk = true;
            for (int p = 0; p < boss.PhaseCount; p++)
                foreach (var s in boss.Phases[p].Pattern)
                {
                    float min = p == 0 ? Tuning.BossTelegraphFirst : Tuning.BossTelegraphMin;
                    sb.Append("P").Append(p).Append(' ').Append(s.Kind).Append('=').Append(s.Windup.ToString("0.0#")).Append("s ");
                    if (s.Windup < min - 1e-4f) tellOk = false;
                }
            ctx.Check(tellOk, "[BOSS] every attack spec telegraph >= Tuning minimum: " + sb);
            bool hitsOk = true; for (int p = 0; p < boss.PhaseCount; p++) if (boss.Phases[p].HitsRequired < 1 || boss.Phases[p].HitsRequired > 3) hitsOk = false;
            ctx.Check(hitsOk, "[BOSS] B1 every phase needs 1..3 discrete hits");
            // negatives: short windup, 4 hits
            var keep = boss.Phases[0].Pattern[0].Windup;
            boss.Phases[0].Pattern[0].Windup = 1.0f;
            ctx.Check(!boss.ValidateSpecs(out string e2), "[BOSS] negative: a 1.0 s phase-1 windup is rejected (" + e2.Trim() + ")");
            boss.Phases[0].Pattern[0].Windup = keep;
            boss.Phases[1].HitsRequired = 4;
            ctx.Check(!boss.ValidateSpecs(out string e3), "[BOSS] negative: 4 hits in a phase is rejected (" + e3.Trim() + ")");
            boss.Phases[1].HitsRequired = 1;

            // B6 safe zones
            ctx.Check(rig.Arena.Zones.Count == 3, "[BOSS] arena has 3 break areas (" + rig.Arena.Zones.Count + ")");
            ctx.Check(rig.Arena.ValidateSafeZones(out string e4), "[BOSS] B6 ValidateSafeZones (attack footprints never cover a break area) " + e4);
            var keepRange = boss.Phases[0].Pattern[1].Range;
            boss.Phases[0].Pattern[1].Range = 30f;
            ctx.Check(!rig.Arena.ValidateSafeZones(out string e5), "[BOSS] negative: a 30 m shockwave covering the break areas is rejected (" + Trunc(e5) + ")");
            boss.Phases[0].Pattern[1].Range = keepRange;
            var small = BossKit.BreakArea(rig.Arena.transform, new Vector3(10f, 0f, 10f), new Vector2(5f, 4f));
            ctx.Check(!rig.Arena.ValidateSafeZones(out string e6), "[BOSS] negative: a 5 x 4 m break area is rejected (" + Trunc(e6) + ")");
            UnityEngine.Object.DestroyImmediate(small);
            rig.Arena.RefreshZones();
            ctx.Check(rig.Arena.ValidateSafeZones(out string e7) && rig.Arena.Zones.Count == 3, "[BOSS] safe zones valid again after removing it " + e7);

            // B5 no kill zones
            ctx.Check(rig.Arena.ValidateNoKillZones(out string e8), "[BOSS] B5 ValidateNoKillZones " + e8);
            var kz = Kit.Trigger(rig.Root, "TestPit", new Vector3(13f, -1f, 13f), new Vector3(2f, 2f, 2f));
            kz.gameObject.AddComponent<KillZone>();
            ctx.Check(!rig.Arena.ValidateNoKillZones(out string e9), "[BOSS] negative: a Void KillZone inside the arena is rejected (" + e9.Trim() + ")");
            UnityEngine.Object.DestroyImmediate(kz.gameObject);
            ctx.Check(rig.Arena.ValidateNoKillZones(out _), "[BOSS] no kill zones again after removing it");

            // B2 determinism: headless timeline hash with UnityEngine.Random seeded 1/2/3
            uint h1 = HeadlessHash(ctx, 1, true), h2 = HeadlessHash(ctx, 2, false), h3 = HeadlessHash(ctx, 3, false);
            ctx.Check(h1 == h2 && h2 == h3 && h1 != 2166136261u, "[BOSS] B2 attack timeline hash identical across seeds 1/2/3 (" + h1.ToString("X8") + " " + h2.ToString("X8") + " " + h3.ToString("X8") + ")");

            // hit filtering (state/phase/kind) on the dormant boss: nothing counts
            ctx.Check(!boss.TryHit(BossHitKind.Shoot, boss.transform.position, null) && boss.Hits == 0, "[BOSS] Dormant boss ignores hits");
            KillRig(rig);
            yield return null;
        }

        /// <summary>Tick a detached TestBoss's runner through all phases (60 s each, fixed 0.02 s steps) with Random seeded.</summary>
        static uint HeadlessHash(DevPlayContext ctx, int seed, bool report)
        {
            UnityEngine.Random.InitState(seed * 7919 + 13);
            var root = new GameObject("BossHashRig").transform;
            var b = (MegaBonkBot)BossKit.TestBoss(root, new Vector3(300f, 0f, 300f));
            b.AutoStart = false; b.AutoBeginOnRestore = false;
            var r = b.Attacks;
            r.ResetHash();
            int maxActive = 0, maxProj = 0, starts = 0, shortT = 0;
            Action<BossTelegraph> onStart = t => { starts++; if (t.WindupSeconds < BossAttackRunner.MinTelegraph(t.Phase) - 1e-3f) shortT++; };
            r.Started += onStart;
            for (int p = 0; p < b.PhaseCount; p++)
            {
                r.SetPattern(b.Phases[p].Pattern, p);
                for (int i = 0; i < 3000; i++)
                {
                    r.Tick(0.02f);
                    maxActive = Mathf.Max(maxActive, r.Active.Count);
                    maxProj = Mathf.Max(maxProj, r.LiveProjectiles);
                }
            }
            r.Started -= onStart;
            r.Cancel();
            uint h = r.TimelineHash;
            if (report)
            {
                ctx.Check(starts >= 30, "[BOSS] headless runner started " + starts + " attacks in 3 x 60 s");
                ctx.Check(shortT == 0, "[BOSS] B3 no telegraph shorter than the minimum at runtime (" + shortT + " short)");
                ctx.Check(maxActive <= 1, "[BOSS] B3 never more than one attack active at once (max " + maxActive + ")");
                ctx.Check(maxProj <= BossAttackRunner.MaxProjectiles && maxProj > 0, "[BOSS] B12 volley projectiles pooled, max live " + maxProj + " <= 10");
            }
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return h;
        }

        // ================================================================== fight (seeds 1/2/3)
        public static IEnumerator Fight(DevPlayContext ctx)
        {
            Env();
            int[] seeds = BotArgs.Seeds(1, 2, 3);
            s_firstSeed = seeds.Length > 0 ? seeds[0] : 1;
            s_liveHash.Clear();
            int first = TeamBot.Results.Count;
            yield return TeamBot.RunSeeds(ctx, seeds, FightSolution, PrepareArena, new BotOptions { RunTimeout = 420f }, "BossFight");
            for (int i = first; i < TeamBot.Results.Count; i++)
            {
                var res = TeamBot.Results[i];
                ctx.Check(res.Passed, "[BOSS] TeamBot defeats MEGA BONK-BOT 9000 " + res.Label + " (" + res.Seconds.ToString("0.0") + " s) " + res.FailReason);
            }
            TeardownAll();
            yield return null;
        }

        static IEnumerator PrepareArena(int seed)
        {
            TeardownAll();
            yield return null;
            yield return null;
            s_seed = seed;
            BossBase.ClearMercyMemory();
            TeamBot.ForceTeam(seed);
            var old = CheckpointManager.Instance;
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            CheckpointManager.Ensure();
            s_rig = BuildArena();
            SpawnTeam(new[] { ZoneL + new Vector3(-1.6f, 0f, 0f), ZoneL, ZoneL + new Vector3(1.6f, 0f, 0f) });
            var mg = new GameObject("BossTestMonitor");
            s_mon = mg.AddComponent<BossTestMonitor>();
            s_mon.Boss = s_rig.Boss;
            yield return null;
        }

        static IEnumerator FightSolution(TeamBot bot)
        {
            var ctx = bot.Ctx;
            var boss = s_rig.Boss;
            bool firstSeed = s_seed == s_firstSeed;
            s_liveHash[s_seed] = HeadlessHash(ctx, s_seed, false);
            yield return bot.WaitBoss(boss, BossState.Fighting, 15f);
            if (bot.Failed) yield break;
            ctx.Check(boss.Phase == 0 && CheckpointManager.Instance != null && CheckpointManager.Instance.CurrentIndex == 1,
                "[BOSS] seed " + s_seed + ": fight began in phase 1 and its checkpoint (1) activated (" + (CheckpointManager.Instance != null ? CheckpointManager.Instance.CurrentIndex : -9) + ")");
            yield return bot.ParkAll(boss);
            var gun = bot.GunOwner; var brute = bot.Brute; var brain = bot.Brainiac; var spr = bot.Sprinter;
            if (gun == null || brute == null || brain == null || spr == null) { bot.Fail("team lacks an archetype / gun owner"); yield break; }
            ShifterCharacter Bait(ShifterCharacter spec) => spr != spec ? spr : (brute != spec ? brute : brain);

            if (firstSeed) yield return TelegraphShots(bot, boss, "p1");

            // ---------------- phase 1: GUN shoots the heart twice (each window takes one hit)
            yield return bot.Switch(Bait(gun));
            yield return bot.GoSafe(Bait(gun), boss);
            yield return bot.WalkTo(gun, GunSpot, true);
            for (int tries = 0; tries < 6 && InPhase(boss, 0) && !bot.Failed; tries++)
            {
                var b = boss;
                bot.Log("shoot try " + tries + ": hits " + b.Hits + " rejected " + b.RejectedHits);
                yield return bot.BaitAndPunish(Bait(gun), gun, b, () => { int h0 = b.Hits; return bot.ShootAt(gun, b.Heart, () => b.Hits != h0 || b.Phase != 0 || !b.IsVulnerable, 6, 6f); }, 60f);
            }
            yield return bot.ExpectBossPhase(boss, 1, 10f);
            if (bot.Failed) yield break;
            ctx.Check(CheckpointManager.Instance.CurrentIndex == 2, "[BOSS] seed " + s_seed + ": B7 phase 2 start force-activated checkpoint 2 (" + CheckpointManager.Instance.CurrentIndex + ")");

            if (firstSeed)
            {
                yield return RestoreAndMercy(bot);
                if (bot.Failed) yield break;
                boss = s_rig.Boss;
                yield return TelegraphShots(bot, boss, "p2");
            }

            // ---------------- phase 2: STR punches the belly plate during the dizzy window
            yield return bot.WalkTo(brute, PunchWait, true);
            for (int tries = 0; tries < 5 && InPhase(boss, 1) && !bot.Failed; tries++)
            {
                var b = boss;
                yield return bot.BaitAndPunish(Bait(brute), brute, b, () => bot.Interact(brute, b.PunchPlate, (BotTarget)PunchSpot), 60f);
                bot.Log("punch try " + tries + ": phase " + b.Phase + " hits " + b.Hits + " state " + b.State + " rejected " + b.RejectedHits + " plate attempts " + b.PunchPlate.Attempts + " landed " + b.PunchPlate.HitsLanded);
                if (InPhase(b, 1) && !bot.Failed) yield return bot.WalkTo(brute, PunchWait, true);
            }
            yield return bot.ExpectBossPhase(boss, 2, 10f);
            if (bot.Failed) yield break;
            ctx.Check(CheckpointManager.Instance.CurrentIndex == 3, "[BOSS] seed " + s_seed + ": phase 3 start force-activated checkpoint 3 (" + CheckpointManager.Instance.CurrentIndex + ")");
            if (firstSeed) yield return TelegraphShots(bot, boss, "p3");

            // ---------------- phase 3: IQ hacks the port during the reboot window
            yield return bot.WalkTo(brain, HackSpot, true);
            for (int tries = 0; tries < 5 && InPhase(boss, 2) && !bot.Failed; tries++)
            {
                var b = boss;
                yield return bot.BaitAndPunish(Bait(brain), brain, b, () => bot.Hack(brain, b.HackPort.Source, (BotTarget)HackSpot), 60f);
                bot.Log("hack try " + tries + ": phase " + b.Phase + " state " + b.State + " rejected " + b.RejectedHits + " port landed " + b.HackPort.HitsLanded);
                if (InPhase(b, 2) && !bot.Failed) yield return bot.WalkTo(brain, HackSpot, true);
            }
            yield return bot.ExpectBossDefeated(boss, 10f);
            if (bot.Failed) yield break;
            ctx.Check(boss.IsOn, "[BOSS] seed " + s_seed + ": B8 defeated boss is a SignalSource that is ON");
            ctx.Check(!boss.TryHit(BossHitKind.Hack, boss.transform.position, null), "[BOSS] defeated boss ignores further hits");
            ctx.Check(s_mon.InactiveDamaged == 0, "[BOSS] seed " + s_seed + ": B4 ME-VISION: inactive interns never damaged (" + s_mon.InactiveDamaged + " " + s_mon.LastInactiveDamage + ")");
            ctx.Check(s_mon.MaxActive <= 1 && s_mon.MaxProjectiles <= 10, "[BOSS] seed " + s_seed + ": B3/B12 live: max " + s_mon.MaxActive + " attack active, max " + s_mon.MaxProjectiles + " projectiles");
            ctx.Check(s_mon.ShortTelegraphs == 0 && s_mon.Telegraphs > 0, "[BOSS] seed " + s_seed + ": live telegraphs " + s_mon.Telegraphs + ", none short (min " + s_mon.MinWindup.ToString("0.00") + " s)");
            ctx.Check(s_mon.Lost == 0, "[BOSS] seed " + s_seed + ": B5 nobody Lost (downed " + s_mon.Downed + ")");
            ctx.Check(s_rig.Arena.ValidateNoKillZones(out string kzErr), "[BOSS] arena still free of kill zones " + kzErr);
            if (firstSeed) ctx.Shot("boss_defeated", ShotCam(new Vector3(0f, 14f, -16f), BossPos));
            if (s_liveHash.Count == 3)
            {
                uint a = 0; bool same = true; foreach (var kv in s_liveHash) { if (a == 0) a = kv.Value; else if (kv.Value != a) same = false; }
                ctx.Check(same, "[BOSS] B2 hash identical in all three team seeds' scenes (" + a.ToString("X8") + ")");
            }
        }

        /// <summary>Seed 1, phase 2: two wipes -> mercy; then a simulated Restart Checkpoint (arena + boss rebuilt,
        /// CheckpointManager.RestoreIndex) must resume in phase 2 with mercy kept and longer telegraphs.</summary>
        static IEnumerator RestoreAndMercy(TeamBot bot)
        {
            var ctx = bot.Ctx;
            var mgr = CheckpointManager.Instance;
            var old = s_rig.Boss;
            int idx = mgr.CurrentIndex;
            old.RegisterWipe();
            ctx.Check(old.MercyLevel == 0 && old.WipesThisPhase == 1, "[BOSS] B10 one wipe: no mercy yet");
            old.RegisterWipe();
            ctx.Check(old.MercyLevel == 1 && Mathf.Abs(old.TellScale - 1.3f) < 1e-3f && Mathf.Abs(old.DamageScale - 0.7f) < 1e-3f,
                "[BOSS] B10 two wipes in the same phase: mercy (tells x" + old.TellScale + ", damage x" + old.DamageScale + ")");

            // simulated Restart Checkpoint: the level reloads (code-built arena rebuilt), the snapshot restores the index
            var oldRoot = s_rig.Root;
            oldRoot.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(oldRoot.gameObject);
            s_rig = BuildArena();
            mgr.RestoreIndex(idx);
            var nb = s_rig.Boss;
            s_mon.Boss = nb;
            yield return bot.ExpectBossPhase(nb, 1, 12f);
            if (bot.Failed) yield break;
            ctx.Check(nb.Phase == 1 && nb.Hits == 0 && nb.State == BossState.Fighting, "[BOSS] B7 RestorePhase from CheckpointManager.CurrentIndex=" + idx + " -> " + nb.PhaseLabel(nb.Phase) + " (hits " + nb.Hits + ")");
            ctx.Check(nb.MercyLevel == 1 && nb.WipesThisPhase == 2, "[BOSS] B10 mercy + wipe count survive the restart (mercy " + nb.MercyLevel + ", wipes " + nb.WipesThisPhase + ")");
            yield return bot.WaitUntil(() => nb.Attacks.Current != null && nb.Attacks.Current.Stage == BossTelegraphStage.Windup, 15f, "first telegraph after restore");
            var t = nb.Attacks.Current;
            if (t != null) ctx.Check(t.WindupSeconds >= Tuning.BossTelegraphMin * 1.3f - 1e-3f, "[BOSS] mercy lengthens telegraphs: " + t.Kind + " " + t.WindupSeconds.ToString("0.00") + " s >= " + (Tuning.BossTelegraphMin * 1.3f).ToString("0.00") + " s");
        }

        /// <summary>Screenshot every attack kind of the current phase mid-windup + check the three tell channels.</summary>
        static IEnumerator TelegraphShots(TeamBot bot, BossBase boss, string tag)
        {
            var ctx = bot.Ctx;
            var pat = boss.CurrentPhase.Pattern;
            var done = new HashSet<BossAttackKind>();
            int phase = boss.Phase;
            foreach (var s in pat)
            {
                if (done.Contains(s.Kind)) continue;
                done.Add(s.Kind);
                var k = s.Kind;
                yield return bot.WaitUntil(() => boss.Phase != phase || (boss.Attacks.Current != null && boss.Attacks.Current.Kind == k && boss.Attacks.Current.Stage == BossTelegraphStage.Windup && boss.Attacks.Current.Fill01 >= 0.55f), 30f, "telegraph " + k, false);
                var t = boss.Attacks.Current;
                if (t == null || t.Kind != k || t.Stage != BossTelegraphStage.Windup) { bot.Warn("no " + k + " telegraph caught"); continue; }
                var view = UnityEngine.Object.FindAnyObjectByType<BossTelegraphView>();
                bool decal = view != null && view.gameObject.activeInHierarchy && view.Telegraph == t;
                string chip = t.Chip ?? "";
                var sfx = boss.TellSfx(k);
                ctx.Check(decal && chip.Length > 0 && sfx != SfxId.None, "[BOSS] B3 three tell channels for " + tag + " " + k + ": decal=" + decal + " chip='" + chip + "' sfx=" + sfx + " fill=" + t.Fill01.ToString("0.00"));
                ctx.Shot("boss_tele_" + tag + "_" + k, ShotCam(new Vector3(0f, 17f, -19f), new Vector3(0f, 0f, 1f)));
            }
        }

        // ================================================================== idle survival (120 s)
        public static IEnumerator Idle(DevPlayContext ctx)
        {
            Env();
            int first = TeamBot.Results.Count;
            yield return TeamBot.RunSeeds(ctx, new[] { BotArgs.Seeds(1, 2, 3)[0] }, IdleSolution, PrepareArena, new BotOptions { RunTimeout = 300f }, "BossIdle");
            for (int i = first; i < TeamBot.Results.Count; i++)
                ctx.Check(TeamBot.Results[i].Passed, "[BOSS] 120 s idle survival run " + TeamBot.Results[i].Label + " " + TeamBot.Results[i].FailReason);
            TeardownAll();
            yield return null;
        }

        static IEnumerator IdleSolution(TeamBot bot)
        {
            var ctx = bot.Ctx;
            var boss = s_rig.Boss;
            yield return bot.WaitBoss(boss, BossState.Fighting, 15f);
            if (bot.Failed) yield break;
            yield return bot.ParkAll(boss);
            yield return bot.Wait(0.5f);
            s_mon.ResetCounts();
            var team = bot.Team;
            float t0 = bot.Now;
            int stage = 0, switches = 0, threatened = 0, outside = 0, samples = 0;
            while (bot.Now - t0 < 120f && !bot.Failed)
            {
                float el = bot.Now - t0;
                if (stage == 0 && el > 40f) { stage = 1; boss.RestorePhase(1); }
                if (stage == 1 && el > 80f) { stage = 2; boss.RestorePhase(2); }
                if (el > (switches + 1) * 17f) { switches++; yield return bot.Switch(team[switches % team.Count]); }
                for (int i = 0; i < team.Count; i++)
                {
                    var c = team[i];
                    samples++;
                    if (boss.Attacks.IsThreatened(c.Feet, 3f)) threatened++;
                    if (!boss.Arena.InSafeZone(c.Feet)) outside++;
                }
                yield return bot.Wait(0.25f);
            }
            ctx.Check(s_mon.Downed == 0 && s_mon.Captured == 0 && s_mon.Lost == 0, "[BOSS] 120 s parked in break areas: nobody Downed/Captured/Lost (" + s_mon.Downed + "/" + s_mon.Captured + "/" + s_mon.Lost + ")");
            ctx.Check(s_mon.MinHealth >= Tuning.MaxHealth - 0.5f, "[BOSS] B6 nobody in a break area was ever hit (min health " + s_mon.MinHealth.ToString("0") + ")");
            ctx.Check(threatened == 0 && outside == 0, "[BOSS] break-area spots never threatened (" + threatened + "/" + samples + ", outside " + outside + ")");
            ctx.Check(s_mon.Telegraphs >= 15 && s_mon.KindsSeen.Count >= 5, "[BOSS] the boss kept attacking: " + s_mon.Telegraphs + " telegraphs, kinds " + string.Join(",", s_mon.KindsSeen));
            ctx.Check(s_mon.ShortTelegraphs == 0 && s_mon.MaxActive <= 1, "[BOSS] idle: no short telegraph, max active " + s_mon.MaxActive);
        }

        // ================================================================== chase
        public static IEnumerator Chase(DevPlayContext ctx)
        {
            Env();
            int first = TeamBot.Results.Count;
            yield return TeamBot.RunSeeds(ctx, new[] { BotArgs.Seeds(1, 2, 3)[0] }, ChaseSolution, PrepareChase, new BotOptions { RunTimeout = 200f }, "ChaseTrack");
            for (int i = first; i < TeamBot.Results.Count; i++)
                ctx.Check(TeamBot.Results[i].Passed, "[BOSS] ChaseTrack scenario " + TeamBot.Results[i].Label + " " + TeamBot.Results[i].FailReason);
            TeardownAll();
            yield return null;
        }

        static IEnumerator PrepareChase(int seed)
        {
            TeardownAll();
            yield return null;
            yield return null;
            TeamBot.ForceTeam(seed);
            var old = CheckpointManager.Instance;
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            CheckpointManager.Ensure();
            s_restarts = 0;
            var root = new GameObject("BossChaseTest").transform;
            s_chaseRoot = root;
            Kit.SolidBox(root, "Ground", ChaseOrigin + new Vector3(0f, -0.5f, 55f), new Vector3(16f, 1f, 130f), MatKey.Floor);
            LevelKit.Checkpoint(root, ChaseOrigin + new Vector3(6f, 0f, 50f), Quaternion.identity, 5);
            s_clear = new GameObject("StationClear").AddComponent<BossTestSignal>();
            s_clear.transform.SetParent(root, false);
            var track = new[] { ChaseOrigin + new Vector3(0f, 0.3f, 0f), ChaseOrigin + new Vector3(0f, 0.3f, 50f), ChaseOrigin + new Vector3(0f, 0.3f, 100f) };
            s_chase = BossKit.Chase(root, track, new[] { new ChaseStation(1, s_clear, 5, "MID STATION") });
            s_chase.RestartOverride = t =>
            {
                s_restarts++; s_restartAt = Time.time;
                var m = CheckpointManager.Instance;
                t.RestoreAtStation(t.StationForCheckpoint(m != null ? m.CurrentIndex : -1));
            };
            s_chase.BustedEvent += t => s_bustAt = Time.time;
            var deck = track[0] + new Vector3(0f, 0.25f, 0f);
            SpawnTeam(new[] { deck + new Vector3(-1.2f, 0f, -1f), deck + new Vector3(0f, 0f, 1f), deck + new Vector3(1.2f, 0f, -1f) });
            yield return null;
        }

        static IEnumerator ChaseSolution(TeamBot bot)
        {
            var ctx = bot.Ctx;
            var ct = s_chase;
            int events = 0;
            Action<ChaseStatus> onCh = s => events++;
            GameEvents.ChaseChanged += onCh;
            try
            {
                yield return bot.WaitChase(ct, t => t.AllAboard, 10f, "all aboard");
                yield return bot.WaitChase(ct, t => t.Started && t.Moving, 5f, "tram moving");
                if (bot.Failed) yield break;
                ctx.Check(Mathf.Abs(ct.Gap - ct.StartGap) < 3f, "[BOSS] chase starts with gap ~StartGap (" + ct.Gap.ToString("0.0") + " / " + ct.StartGap + ")");
                yield return bot.Wait(2f);
                ctx.Check(ct.AllAboard && ct.TramDistance > 5f, "[BOSS] team rides the moving tram (all aboard, " + ct.TramDistance.ToString("0.0") + " m)");
                ctx.Shot("chase_ride", ShotCam(ct.Tram.Body.transform.position + new Vector3(9f, 7f, -10f), ct.Tram.Body.transform.position));
                for (int k = 0; k < 20 && !(ct.StationIndex == 0 && ct.Docked); k++)
                {
                    bot.Log("chase: dist " + ct.TramDistance.ToString("0.0") + "/" + ct.TrackLength.ToString("0.0") + " station " + ct.StationIndex + " docked " + ct.Docked + " moving " + ct.Moving
                            + " aboard " + ct.AllAboard + " waitClear " + ct.WaitingForClear + " gap " + ct.Gap.ToString("0.0") + " finished " + ct.Finished + " busted " + ct.Busted + " stations " + ct.Stations.Length
                            + " tram z " + ct.Tram.Body.transform.position.z.ToString("0.0") + " st0 pi " + ct.Stations[0].PointIndex + " ck " + ct.Stations[0].CheckpointIndex + " clear " + (ct.Stations[0].Clear != null) + " pts " + ct.TrackPoints.Length + " p1 " + ct.PointAt(50f) + " sd0 " + ct.StationDistance(0) + " next " + ct.NextStop);
                    yield return bot.Wait(1f);
                }
                yield return bot.WaitChase(ct, t => t.StationIndex == 0 && t.Docked, 2f, "docked at the station");
                if (bot.Failed) yield break;
                ctx.Check(ct.WaitingForClear && ct.Gap <= ct.MaxGap + 0.01f, "[BOSS] docked at station 1, waiting for its Clear signal (gap " + ct.Gap.ToString("0.0") + " <= " + ct.MaxGap + ")");
                ctx.Check(CheckpointManager.Instance.CurrentIndex == 5, "[BOSS] the station's checkpoint (5) activated on arrival (" + CheckpointManager.Instance.CurrentIndex + ")");
                ctx.Check(ct.AllAboard, "[BOSS] still all aboard after the ride");
                // deliberate BUSTED: never clear the station
                yield return bot.WaitChase(ct, t => t.Busted, 45f, "BUSTED");
                if (bot.Failed) yield break;
                ctx.Check(ct.BustCount == 1 && ct.Gap <= 0.01f, "[BOSS] pursuer closed the gap: BUSTED (count " + ct.BustCount + ")");
                ctx.Shot("chase_busted", ShotCam(ct.Tram.Body.transform.position + new Vector3(10f, 8f, -14f), ct.Tram.Body.transform.position));
                yield return bot.WaitUntil(() => s_restarts > 0, 5f, "restart after BUSTED");
                if (bot.Failed) yield break;
                float delay = s_restartAt - s_bustAt;
                ctx.Check(delay > 1.7f && delay < 2.5f, "[BOSS] BUSTED -> " + delay.ToString("0.00") + " s -> Restart Checkpoint");
                ctx.Check(!ct.Busted && ct.Docked && ct.StationIndex == 0 && Mathf.Abs(ct.Gap - ct.StartGap) < 0.05f,
                    "[BOSS] RestoreAtStation(StationForCheckpoint(5)) = station " + ct.StationIndex + ", gap " + ct.Gap.ToString("0.0") + " = StartGap");
                s_clear.Set(true);
                yield return bot.WaitChase(ct, t => t.Moving, 6f, "moving again after the station is cleared");
                yield return bot.WaitChase(ct, t => t.Finished, 25f, "end of the line");
                if (bot.Failed) yield break;
                ctx.Check(ct.Finished && !ct.Busted, "[BOSS] tram reached the end of the line");
                ctx.Check(events >= 10, "[BOSS] GameEvents.ChaseChanged raised (" + events + " times)");
            }
            finally { GameEvents.ChaseChanged -= onCh; }
        }

        // ================================================================== flee (SOLO POD)
        public static IEnumerator Flee(DevPlayContext ctx)
        {
            Env();
            int first = TeamBot.Results.Count;
            yield return TeamBot.RunSeeds(ctx, new[] { BotArgs.Seeds(1, 2, 3)[0] }, FleeSolution, PrepareFlee, new BotOptions { RunTimeout = 200f }, "FleeBrain");
            for (int i = first; i < TeamBot.Results.Count; i++)
                ctx.Check(TeamBot.Results[i].Passed, "[BOSS] FleeBrain scenario " + TeamBot.Results[i].Label + " " + TeamBot.Results[i].FailReason);
            TeardownAll();
            yield return null;
        }

        static IEnumerator PrepareFlee(int seed)
        {
            TeardownAll();
            yield return null;
            yield return null;
            TeamBot.ForceTeam(seed);
            var root = new GameObject("BossFleeTest").transform;
            s_fleeRoot = root;
            var o = FleeOrigin;
            Kit.SolidBox(root, "Ground", o + new Vector3(0f, -0.5f, 0f), new Vector3(34f, 1f, 34f), MatKey.Floor);
            var loop = new[] { o + new Vector3(-7f, 0f, -7f), o + new Vector3(-7f, 0f, 7f), o + new Vector3(7f, 0f, 7f), o + new Vector3(7f, 0f, -7f) };
            s_pod = BossKit.SoloPod(root, o + new Vector3(-7f, 0.6f, 0f), loop);
            SpawnTeam(new[] { o + new Vector3(-1.6f, 0f, -12f), o + new Vector3(0f, 0f, -12f), o + new Vector3(1.6f, 0f, -12f) });
            yield return null;
        }

        static IEnumerator FleeSolution(TeamBot bot)
        {
            var ctx = bot.Ctx;
            var pod = s_pod;
            var brute = bot.Brute; var spr = bot.Sprinter; var gun = bot.GunOwner;
            yield return bot.Wait(0.5f);
            // flee: chase it with the sprinter for a moment
            var p0 = pod.transform.position;
            yield return bot.Switch(spr);
            bool walking = true; float path = 0f, minDist = 999f; var last = pod.transform.position;
            IEnumerator Walk() { yield return bot.WalkTo(spr, new Vector3(p0.x, 0f, p0.z), false, 2.5f, 10f); walking = false; }   // walk: a sprint >= TagSpeed would TAG it
            IEnumerator Sample()
            {
                while (walking)
                {
                    path += Vector3.Distance(last, pod.transform.position); last = pod.transform.position;
                    if (!pod.IsMonologuing) minDist = Mathf.Min(minDist, BossFootprint.Planar(spr.Feet, pod.transform.position));
                    yield return null;
                }
            }
            yield return bot.Parallel(new[] { Walk(), Sample() });
            ctx.Check(path > 6f && minDist > 1.5f, "[BOSS] FleeBrain flees the nearest intern along its loop (flew " + path.ToString("0.0") + " m while the sprinter walked at it; closest " + minDist.ToString("0.0") + " m; monologues " + pod.MonologueCount + ")");
            // monologue window
            yield return bot.WaitUntil(() => pod.IsMonologuing, 9f, "monologue");
            if (bot.Failed) yield break;
            float ms = bot.Now; var mp = pod.transform.position;
            ctx.Shot("flee_monologue", ShotCam(pod.transform.position + new Vector3(5f, 4f, -6f), pod.transform.position));
            yield return bot.WaitUntil(() => !pod.IsMonologuing, 5f, "monologue ends");
            float mdur = bot.Now - ms;
            ctx.Check(mdur > 2.2f && mdur < 3.6f && Vector3.Distance(mp, pod.transform.position) < 0.5f, "[BOSS] monologue window " + mdur.ToString("0.0") + " s (~3 s), pod holds still");
            float ms2 = bot.Now;
            yield return bot.WaitUntil(() => pod.IsMonologuing, 9f, "second monologue");
            ctx.Check(pod.MonologueCount >= 2, "[BOSS] monologue repeats (count " + pod.MonologueCount + ", gap " + (bot.Now - ms2).ToString("0.0") + " s)");
            // dizzy
            pod.Stun(pod.DizzyFor);
            float ds = bot.Now;
            ctx.Check(pod.IsDizzy && !pod.IsFleeing, "[BOSS] Stun -> dizzy");
            yield return bot.Wait(0.4f);
            ctx.Shot("flee_dizzy", ShotCam(pod.transform.position + new Vector3(5f, 4f, -6f), pod.transform.position));
            yield return bot.WaitUntil(() => !pod.IsDizzy, 5f, "dizzy ends");
            float dd = bot.Now - ds;
            ctx.Check(dd > 2.6f && dd < 3.6f, "[BOSS] dizzy lasts " + dd.ToString("0.0") + " s (DizzyFor " + pod.DizzyFor + ")");
            // shot stun (any gun, B11)
            if (gun != null)
            {
                yield return bot.ShootAt(gun, pod, () => pod.IsDizzy, 6, 8f);
                ctx.Check(pod.IsDizzy && pod.LastStunCause == "shot", "[BOSS] a shot stuns the pod (" + pod.LastStunCause + ")");
                yield return bot.WaitUntil(() => !pod.IsDizzy, 5f, "dizzy ends (shot)");
            }
            // argue (TAUNT route): stun the pod, the Brute walks up to it, and taunts the moment the dizzy wears off
            yield return bot.Switch(brute);
            pod.Stun(pod.DizzyFor);
            yield return bot.WalkTo(brute, pod.transform.position, true, 4.5f, 6f);   // outside TouchRadius (touching = stun), inside TauntRadius
            yield return bot.WaitUntil(() => !pod.IsDizzy, 5f, "dizzy wears off");
            yield return bot.Taunt(brute);
            yield return bot.WaitUntil(() => pod.IsDistracted, 1f, "pod argues", false);
            ctx.Check(pod.IsDistracted && pod.Taunter == brute, "[BOSS] TAUNT -> the pod zooms over to argue (distracted " + pod.IsDistracted + ")");
            ctx.Shot("flee_argue", ShotCam(pod.transform.position + new Vector3(5f, 4f, -6f), pod.transform.position));
            ctx.Check(BossFootprint.Planar(pod.transform.position, brute.Feet) < pod.TauntRadius, "[BOSS] arguing pod hovers near the taunter (" + BossFootprint.Planar(pod.transform.position, brute.Feet).ToString("0.0") + " m)");
            // carry: walk up and grab it while it argues (touching stuns it, grabbing keeps it dizzy)
            yield return bot.Grab(brute, pod, 6f);
            ctx.Check(pod.IsCarried && pod.IsDizzy, "[BOSS] arguing pod grabbed + carried by the Brute (STR " + (brute.Stats != null ? brute.Stats.Strength.ToString() : "?") + ")");
            if (pod.IsCarried)
            {
                yield return bot.Wait(1f);
                ctx.Shot("flee_carried", ShotCam(brute.Feet + new Vector3(4f, 3.5f, -5f), brute.Feet + Vector3.up));
                yield return bot.Drop(brute);
            }
            yield return bot.WaitUntil(() => !pod.IsDizzy && !pod.IsCarried, 6f, "pod recovers after the drop");
            if (bot.Failed) yield break;
            // fix request (lead): ArgueArea -> a taunt from outside it is ignored
            var o = FleeOrigin;
            pod.ArgueArea = new Bounds(o + new Vector3(0f, 0f, 40f), new Vector3(4f, 10f, 4f));
            yield return bot.Wait(2.2f);                                                // taunt cooldown
            yield return bot.WaitUntil(() => !pod.IsDizzy && BossFootprint.Planar(pod.transform.position, brute.Feet) < pod.TauntRadius - 1f, 10f, "pod within taunt range (area test)", false);
            yield return bot.Taunt(brute);
            yield return bot.Wait(0.5f);
            ctx.Check(!pod.IsDistracted, "[BOSS] taunt from outside ArgueArea is ignored (NOT WORTH MY TIME.)");
            pod.ArgueArea = default;
            // fix request (lead): two interns 6 m apart on the loop -> at most one reversal per second
            var spots = new[] { o + new Vector3(-7f, 0f, -3f), o + new Vector3(-7f, 0f, 3f) };
            var others = bot.Others(brute);
            yield return bot.Parallel(new[] { bot.WalkTo(others[0], spots[0], true, 0.6f, 15f), bot.WalkTo(others[1], spots[1], true, 0.6f, 15f), bot.WalkTo(brute, o + new Vector3(10f, 0f, 10f), true, 0.8f, 15f) });
            int r0 = pod.Reversals; float t0r = bot.Now;
            yield return bot.Wait(8f);
            int rev = pod.Reversals - r0; float span = bot.Now - t0r;
            ctx.Check(rev <= Mathf.FloorToInt(span / pod.MinReverseInterval) + 1, "[BOSS] flanked by two interns 6 m apart: " + rev + " reversals in " + span.ToString("0.0") + " s (<= 1 per " + pod.MinReverseInterval + " s)");
        }

        // ================================================================== builders
        static Rig BuildArena()
        {
            var r = new Rig();
            r.Root = new GameObject("BossTestArena").transform;
            r.Arena = BossKit.Arena(r.Root, Vector3.zero, new Vector2(30f, 30f), 2.5f, true);
            var at = r.Arena.transform;
            BossKit.BreakArea(at, ZoneL, new Vector2(8f, 6f));
            BossKit.BreakArea(at, ZoneR, new Vector2(8f, 6f));
            BossKit.BreakArea(at, ZoneC, new Vector2(7f, 5f));
            r.Cps = new[]
            {
                LevelKit.Checkpoint(at, ZoneL, Quaternion.identity, 1),
                LevelKit.Checkpoint(at, ZoneR, Quaternion.identity, 2),
                LevelKit.Checkpoint(at, ZoneC, Quaternion.identity, 3),
            };
            r.Arena.PhaseCheckpoints = r.Cps;
            r.Boss = (MegaBonkBot)BossKit.TestBoss(at, BossPos);
            var rot = Quaternion.Euler(0f, 180f, 0f);
            r.Boss.transform.rotation = rot;
            r.Boss.Arena = r.Arena;
            r.Arena.Boss = r.Boss;
            r.Boss.SetHome(BossPos, rot);
            return r;
        }

        static void SpawnTeam(Vector3[] at)
        {
            var gs = GameSession.Instance;
            if (gs == null) { Debug.LogError("[BOSS] no GameSession"); return; }
            gs.EnsureTeam();
            s_teamRoot = new GameObject("BossTestTeam").transform;
            var cmGo = new GameObject("CharacterManager");
            cmGo.transform.SetParent(s_teamRoot, false);
            var cm = cmGo.AddComponent<CharacterManager>();
            cmGo.AddComponent<CharacterSwitcher>();
            var poses = new List<Pose>(3);
            for (int i = 0; i < 3; i++) poses.Add(new Pose(at[Mathf.Min(i, at.Length - 1)], Quaternion.identity));
            cm.SpawnTeam(gs.Team, poses, 0);
            var cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cg.AddComponent<Camera>();
            }
            var sc = cam.GetComponent<ShiftersCamera>();
            if (sc == null) sc = cam.gameObject.AddComponent<ShiftersCamera>();
            sc.SetTarget(cm.Active, true);
        }

        static void Env()
        {
            if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
            {
                var l = new GameObject("BossTestSun").AddComponent<Light>();
                l.type = LightType.Directional; l.intensity = 1.15f;
                l.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            }
        }

        static Camera ShotCam(Vector3 pos, Vector3 look)
        {
            if (s_cam == null)
            {
                var g = new GameObject("BossTestCam");
                s_cam = g.AddComponent<Camera>();
                s_cam.enabled = false;
                s_cam.fieldOfView = 55f; s_cam.nearClipPlane = 0.1f; s_cam.farClipPlane = 400f;
            }
            s_cam.transform.position = pos;
            s_cam.transform.LookAt(look);
            return s_cam;
        }

        static void KillRig(Rig r)
        {
            if (r == null || r.Root == null) return;
            r.Root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(r.Root.gameObject);
        }

        static void TeardownAll()
        {
            KillRig(s_rig); s_rig = null;
            if (s_mon != null) UnityEngine.Object.Destroy(s_mon.gameObject); s_mon = null;
            if (s_chaseRoot != null) { s_chaseRoot.gameObject.SetActive(false); UnityEngine.Object.Destroy(s_chaseRoot.gameObject); } s_chaseRoot = null; s_chase = null;
            if (s_fleeRoot != null) { s_fleeRoot.gameObject.SetActive(false); UnityEngine.Object.Destroy(s_fleeRoot.gameObject); } s_fleeRoot = null; s_pod = null;
            TeamBot.TearDownLevel();
            if (s_teamRoot != null) UnityEngine.Object.Destroy(s_teamRoot.gameObject); s_teamRoot = null;
        }

        static void Cleanup()
        {
            TeardownAll();
            if (s_cam != null) UnityEngine.Object.Destroy(s_cam.gameObject); s_cam = null;
        }

        static bool InPhase(BossBase b, int p) => b != null && b.Phase == p && !b.IsDefeated && b.State != BossState.PhaseTransition;

        static string Trunc(string s) => s == null ? "" : (s.Length > 160 ? s.Substring(0, 160) + "..." : s.Trim());
    }
}
