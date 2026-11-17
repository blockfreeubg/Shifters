// =====================================================================================
//  SHIFTERS - BossBot.cs   [BOSSES module - TeamBot helpers for boss fights, CAMPAIGN §18.3]
//  Extension methods on TeamBot (level solutions: `yield return bot.WaitBoss(boss, BossState.Vulnerable)`):
//    WaitBoss(boss, state, timeout)          wait until boss.State == state (fails the run on timeout)
//    WaitVulnerable(boss, timeout)           shorthand for WaitBoss(..., Vulnerable)
//    WaitSafe(boss, point, horizon)          wait until no attack threatens point within horizon seconds
//    GoSafe(c, boss)                         walk c to its spot in the nearest BREAK AREA (never attacked, B6)
//    ParkAll(boss)                           everyone into break areas (parallel)
//    ExpectBossPhase(boss, phase, within)    phase reached and fighting (or later)
//    ExpectBossDefeated(boss, within)        boss Defeated (exit doors open)
//    WaitChase(track, predicate, timeout)    ChaseTrack condition (e.g. t => t.StationIndex == 1)
//    BaitAndPunish(bait, specialist, boss, act)  the core boss tactic: the bait stays active in a break area
//                                            while the specialist waits INACTIVE (ME-VISION never targets it);
//                                            on the window: SHIFT to the specialist, act, SHIFT back.
//  Boss time is scaled game time; all waits use bot.Now (driver time) for timeouts.
// =====================================================================================
using System;
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class BossBot
    {
        public static IEnumerator WaitBoss(this TeamBot bot, BossBase boss, BossState state, float timeout = 40f)
        {
            if (boss == null) { bot.Fail("WaitBoss: no boss"); yield break; }
            yield return bot.WaitUntil(() => boss == null || boss.State == state, timeout, "boss " + boss.BossId + " " + state);
        }

        public static IEnumerator WaitVulnerable(this TeamBot bot, BossBase boss, float timeout = 40f) => bot.WaitBoss(boss, BossState.Vulnerable, timeout);

        /// <summary>Wait until boss.Attacks says point is not threatened within horizon seconds (or the boss is not fighting).</summary>
        public static IEnumerator WaitSafe(this TeamBot bot, BossBase boss, Vector3 point, float horizon = 2f, float timeout = 30f)
        {
            if (boss == null) yield break;
            yield return bot.WaitUntil(() => boss == null || !boss.IsFighting || !boss.Attacks.IsThreatened(point, horizon), timeout, "safe at " + point);
        }

        /// <summary>The BREAK AREA spot for c (nearest zone; slot-based spot so three interns fit), or c's feet if none.</summary>
        public static Vector3 SafeSpot(BossBase boss, ShifterCharacter c)
        {
            var arena = boss != null ? boss.Arena : null;
            var z = arena != null && c != null ? arena.NearestSafeZone(c.Feet) : null;
            return z != null ? z.SpotFor(c.Slot) : (c != null ? c.Feet : Vector3.zero);
        }

        public static IEnumerator GoSafe(this TeamBot bot, ShifterCharacter c, BossBase boss, bool sprint = true)
        {
            if (c == null || boss == null) yield break;
            if (boss.Arena != null && boss.Arena.InSafeZone(c.Feet, -0.4f)) yield break;
            yield return bot.WalkTo(c, SafeSpot(boss, c), sprint, 0.6f, 25f);
            if (boss.Arena != null && !boss.Arena.InSafeZone(c.Feet)) bot.Warn("GoSafe: " + c.DisplayName + " is not inside a break area");
        }

        public static IEnumerator ParkAll(this TeamBot bot, BossBase boss)
        {
            var t = bot.Team;
            var r = new IEnumerator[t.Count];
            for (int i = 0; i < t.Count; i++) r[i] = bot.GoSafe(t[i], boss);
            yield return bot.Parallel(r);
        }

        public static IEnumerator ExpectBossPhase(this TeamBot bot, BossBase boss, int phase, float within = 30f) =>
            bot.Expect(() => boss != null && (boss.State == BossState.Defeated || (boss.Phase >= phase && boss.State != BossState.Intro && boss.State != BossState.Dormant && boss.State != BossState.PhaseTransition)),
                "boss " + (boss != null ? boss.BossId : "?") + " in phase " + phase, within);

        public static IEnumerator ExpectBossDefeated(this TeamBot bot, BossBase boss, float within = 10f) =>
            bot.Expect(() => boss != null && boss.State == BossState.Defeated && boss.IsOn, "boss " + (boss != null ? boss.BossId : "?") + " defeated (signal ON)", within);

        public static IEnumerator WaitChase(this TeamBot bot, ChaseTrack track, Func<ChaseTrack, bool> predicate, float timeout = 60f, string label = "chase condition")
        {
            if (track == null) { bot.Fail("WaitChase: no track"); yield break; }
            yield return bot.WaitUntil(() => track == null || predicate(track), timeout, label);
        }

        /// <summary>One discrete hit by the core tactic: bait active + safe, specialist inactive near the weak point; on
        /// the vulnerable window SHIFT to the specialist, run act (e.g. bot.Interact(brute, plate)), SHIFT back to the bait.
        /// Ends when the boss's hit counter / phase changed or the window closed.</summary>
        public static IEnumerator BaitAndPunish(this TeamBot bot, ShifterCharacter bait, ShifterCharacter specialist, BossBase boss, Func<IEnumerator> act, float timeout = 60f)
        {
            if (boss == null || specialist == null) { bot.Fail("BaitAndPunish: missing boss/specialist"); yield break; }
            if (bait != null && bait != specialist && CharacterManager.Instance != null && CharacterManager.Instance.Active != bait) yield return bot.Switch(bait);
            yield return bot.WaitVulnerable(boss, timeout);
            if (bot.Failed) yield break;
            int phase = boss.Phase, hits = boss.Hits;
            if (CharacterManager.Instance != null && CharacterManager.Instance.Active != specialist) yield return bot.Switch(specialist);
            yield return act();
            float t0 = bot.Now;
            while (bot.Now - t0 < 1.5f && boss.Phase == phase && boss.Hits == hits && boss.State == BossState.Vulnerable) yield return null;
            if (bait != null && bait != specialist && boss.State != BossState.Defeated) yield return bot.Switch(bait);
        }
    }
}
