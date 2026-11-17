// =====================================================================================
//  SHIFTERS - BossTypes.cs   [BOSSES module - CAMPAIGN §11.1 / §18.3]
//  Shared boss data: targeting rule, hit kinds, attack kinds, attack + phase specs, the live
//  telegraph record, chase stations, footprint math (used by the runtime AND the static
//  validators, so "never covers a break area" is checked with the same code that aims), and
//  small reflection bridges to seams APIs that may not be published yet.
//  BossState is a contract enum (Core/Enums.cs).
// =====================================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    /// <summary>B4: ActiveOnly = ME-VISION (only the ACTIVE intern is targeted/damaged). Loneliest = most isolated SCARED intern.</summary>
    public enum BossTargetRule { ActiveOnly, Loneliest }

    [Flags] public enum BossHitKind { None = 0, Shoot = 1, Punch = 2, Hack = 4, Impact = 8, Plate = 16, TeamZone = 32, Swap = 64, Custom = 128, Any = 255 }

    public enum BossAttackKind { SlamZone, Shockwave, Charge, Sweep, Volley, Spook, SkateLoop, Summon }

    public enum BossTelegraphStage { Windup, Active, Recover }

    /// <summary>One attack of a phase's fixed loop. Zero fields fall back to the per-kind defaults (BossAttackDefaults).
    /// Windup = telegraph seconds (B3: &gt;= Tuning.BossTelegraphFirst in phase 0, &gt;= BossTelegraphMin after).
    /// TargetMark = arena mark name (BossArena.Mark): the aim point when TargetActive is false or there is no target;
    /// for Shockwave/Sweep/Volley/Charge with TargetActive == false it is also the ORIGIN. SkateLoop: the mark is a
    /// parent whose children (in order) are the loop points. Damage 0 = Tuning.BossHitDamage (30, B9).</summary>
    [Serializable]
    public struct BossAttackSpec
    {
        public BossAttackKind Kind;
        public float Windup, Active, Recover;
        public string TargetMark;
        public bool TargetActive;
        public float Radius, Range, Speed, Damage;

        public static BossAttackSpec Make(BossAttackKind kind, float windup, bool targetActive = true, string mark = null,
            float radius = 0f, float range = 0f, float speed = 0f, float active = 0f, float recover = 0f, float damage = 0f)
            => new BossAttackSpec { Kind = kind, Windup = windup, TargetActive = targetActive, TargetMark = mark, Radius = radius,
                                    Range = range, Speed = speed, Active = active, Recover = recover, Damage = damage };
    }

    /// <summary>One boss phase. CheckpointIndex &lt;= 0 = no phase checkpoint. HitsRequired &lt;= 0 = 1 (B1: max 3).
    /// HazardGroupsOn: names of arena children (BossArena "HazardGroups/&lt;name&gt;") switched on for this phase only.</summary>
    [Serializable]
    public struct BossPhaseSpec
    {
        public string Name;
        public int HitsRequired;
        public BossHitKind Accepts;
        public bool HitsOnlyWhenVulnerable;
        public float VulnerableSeconds;
        public BossAttackSpec[] Pattern;
        public int CheckpointIndex;
        public string IntroBeat;
        public string[] HazardGroupsOn;
    }

    /// <summary>Live record of the attack the runner is executing (BossAttackRunner.Active). Read-only for UI/bots.</summary>
    public sealed class BossTelegraph
    {
        public BossAttackKind Kind;
        public BossAttackSpec Spec;
        public int Phase, PatternIndex, Serial;
        public BossTelegraphStage Stage;
        public Vector3 Origin, Aim, End;
        public float Radius, Range, Speed, Damage;
        public float WindupSeconds, ActiveSeconds, RecoverSeconds;
        /// <summary>Seconds since the windup started (scaled, fixed-step).</summary>
        public float Elapsed;
        public ShifterCharacter Target;
        /// <summary>SkateLoop path (world). Null for other kinds.</summary>
        public Vector3[] Path;
        public float TimeToActive => Mathf.Max(0f, WindupSeconds - Elapsed);
        public float ActiveElapsed => Mathf.Max(0f, Elapsed - WindupSeconds);
        public float Fill01 => WindupSeconds > 0f ? Mathf.Clamp01(Elapsed / WindupSeconds) : 1f;
        public float Remaining => Mathf.Max(0f, WindupSeconds + ActiveSeconds + RecoverSeconds - Elapsed);
        public bool IsDangerous => Stage == BossTelegraphStage.Active;
        /// <summary>SPD-view chip text, e.g. "SLAM IN 1.2 s" (cached strings, no per-frame allocation).</summary>
        public string Chip => Stage == BossTelegraphStage.Windup ? BossText.Chip(Kind, TimeToActive) : BossText.Now(Kind);
        public bool Covers(Vector3 p, float margin = 0.5f) => BossFootprint.Covers(this, p, margin);
    }

    /// <summary>ChaseTrack stop: the tram docks at TrackPoints[PointIndex] until Clear is ON (null = no obstacle).
    /// CheckpointIndex = the checkpoint whose restore puts the tram back here (&lt;= 0 none).</summary>
    [Serializable]
    public class ChaseStation
    {
        public int PointIndex;
        public SignalSource Clear;
        public int CheckpointIndex;
        public string Name = "";
        public ChaseStation() { }
        public ChaseStation(int pointIndex, SignalSource clear, int checkpointIndex, string name = "") { PointIndex = pointIndex; Clear = clear; CheckpointIndex = checkpointIndex; Name = name ?? ""; }
    }

    /// <summary>Per-kind defaults for zero spec fields.</summary>
    public static class BossAttackDefaults
    {
        public static float Radius(in BossAttackSpec s)
        {
            if (s.Radius > 0f) return s.Radius;
            switch (s.Kind)
            {
                case BossAttackKind.SlamZone: return 3f;
                case BossAttackKind.Charge: return 1.4f;
                case BossAttackKind.Sweep: return 0.6f;
                case BossAttackKind.Volley: return 0.35f;
                case BossAttackKind.Spook: return Tuning.TeamworkRadius;
                case BossAttackKind.SkateLoop: return 1.2f;
                case BossAttackKind.Shockwave: return 0.6f;
                default: return 1f;
            }
        }
        public static float Range(in BossAttackSpec s)
        {
            if (s.Range > 0f) return s.Range;
            switch (s.Kind)
            {
                case BossAttackKind.Shockwave: return 12f;
                case BossAttackKind.Charge: return 16f;
                case BossAttackKind.Sweep: return 8f;
                case BossAttackKind.Volley: return 24f;
                case BossAttackKind.Spook: return 40f;
                default: return 0f;
            }
        }
        public static float Speed(in BossAttackSpec s)
        {
            if (s.Speed > 0f) return s.Speed;
            switch (s.Kind)
            {
                case BossAttackKind.Shockwave: return 8f;
                case BossAttackKind.Charge: return 14f;
                case BossAttackKind.Sweep: return 120f;      // degrees per second
                case BossAttackKind.Volley: return 12f;
                case BossAttackKind.SkateLoop: return 8f;
                default: return 0f;
            }
        }
        public static float Recover(in BossAttackSpec s) => s.Recover > 0f ? s.Recover : 0.8f;
        public static float Damage(in BossAttackSpec s) => s.Damage > 0f ? s.Damage : (s.Kind == BossAttackKind.Spook || s.Kind == BossAttackKind.Summon ? 0f : Tuning.BossHitDamage);
        /// <summary>Attacks whose footprint does not depend on the target (validated statically against the break areas).</summary>
        public static bool FixedFootprint(BossAttackKind k) => k == BossAttackKind.Shockwave || k == BossAttackKind.Sweep || k == BossAttackKind.SkateLoop;
        /// <summary>Attacks that originate at the boss / a mark (not at the target).</summary>
        public static bool FromOrigin(BossAttackKind k) => k == BossAttackKind.Shockwave || k == BossAttackKind.Sweep || k == BossAttackKind.Volley || k == BossAttackKind.Charge;
    }

    /// <summary>Footprint geometry shared by the runner (hits, threat queries) and the validators.</summary>
    public static class BossFootprint
    {
        public static float Planar(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        public static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = 0f; a.y = 0f; b.y = 0f;
            var ab = b - a; float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        public static float PathDistance(Vector3 p, Vector3[] path)
        {
            if (path == null || path.Length == 0) return float.PositiveInfinity;
            if (path.Length == 1) return Planar(p, path[0]);
            float best = float.PositiveInfinity;
            for (int i = 0; i < path.Length; i++) best = Mathf.Min(best, SegmentDistance(p, path[i], path[(i + 1) % path.Length]));
            return best;
        }

        public static bool Covers(BossTelegraph t, Vector3 p, float margin)
        {
            if (t == null) return false;
            switch (t.Kind)
            {
                case BossAttackKind.SlamZone: return Planar(p, t.Aim) <= t.Radius + margin;
                case BossAttackKind.Shockwave:
                case BossAttackKind.Sweep: return Planar(p, t.Origin) <= t.Range + margin;
                case BossAttackKind.Charge: return SegmentDistance(p, t.Origin, t.End) <= t.Radius + margin;
                case BossAttackKind.Volley: return SegmentDistance(p, t.Origin, t.End) <= t.Radius + 0.6f + margin;
                case BossAttackKind.Spook: return t.Target != null ? Planar(p, t.Target.Feet) <= 1.5f + margin : Planar(p, t.Aim) <= 1.5f + margin;
                case BossAttackKind.SkateLoop: return PathDistance(p, t.Path) <= t.Radius + margin;
                default: return false;
            }
        }

        /// <summary>Planar distance from p to an oriented rectangle (0 inside).</summary>
        public static float RectDistance(Vector3 p, Vector3 center, Quaternion rot, Vector2 size)
        {
            var l = Quaternion.Inverse(rot) * (p - center);
            float dx = Mathf.Max(0f, Mathf.Abs(l.x) - size.x * 0.5f), dz = Mathf.Max(0f, Mathf.Abs(l.z) - size.y * 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Minimal planar distance between segment a-b and a rectangle (sampled every 0.25 m).</summary>
        public static float SegmentRectDistance(Vector3 a, Vector3 b, Vector3 center, Quaternion rot, Vector2 size)
        {
            float len = Planar(a, b); int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
            float best = float.PositiveInfinity;
            for (int i = 0; i <= n; i++) best = Mathf.Min(best, RectDistance(Vector3.Lerp(a, b, i / (float)n), center, rot, size));
            return best;
        }
    }

    /// <summary>Cached HUD/chip strings (no per-frame allocations).</summary>
    public static class BossText
    {
        static readonly string[] Names = { "SLAM", "SHOCKWAVE", "CHARGE", "SWEEP", "VOLLEY", "BOO", "SKATES", "MINIONS" };
        static string[][] s_chips;
        static string[] s_now;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_chips = null; s_now = null; }

        public static string Name(BossAttackKind k) { int i = (int)k; return i >= 0 && i < Names.Length ? Names[i] : k.ToString().ToUpperInvariant(); }

        public static string Chip(BossAttackKind k, float seconds)
        {
            if (s_chips == null) s_chips = new string[Names.Length][];
            int i = Mathf.Clamp((int)k, 0, Names.Length - 1);
            if (s_chips[i] == null) s_chips[i] = new string[100];
            int tenth = Mathf.Clamp(Mathf.CeilToInt(seconds * 10f), 0, 99);
            return s_chips[i][tenth] ??= Names[i] + " IN " + (tenth / 10f).ToString("0.0") + " s";
        }

        public static string Now(BossAttackKind k)
        {
            if (s_now == null) s_now = new string[Names.Length];
            int i = Mathf.Clamp((int)k, 0, Names.Length - 1);
            return s_now[i] ??= Names[i] + "!";
        }
    }

    /// <summary>Bridges to seams APIs that may land after this module (CheckpointManager.ActivateFromScript,
    /// ShifterCharacter.IsScared). Reflection is looked up once; fallbacks keep everything working without them.</summary>
    public static class BossCompat
    {
        static bool s_lookedActivate, s_lookedScared;
        static MethodInfo s_activate;
        static PropertyInfo s_scared;
        static readonly object[] s_args = new object[1];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_lookedActivate = s_lookedScared = false; s_activate = null; s_scared = null; }

        /// <summary>B7: force-activate checkpoint <paramref name="index"/> (revives Downed teammates, saves the snapshot).
        /// Uses CheckpointManager.ActivateFromScript(int) when seams published it, else TryActivate.</summary>
        public static bool ActivateCheckpoint(int index)
        {
            if (index <= 0) return false;
            var mgr = CheckpointManager.Ensure();
            if (index <= mgr.CurrentIndex) return false;
            var cp = CheckpointManager.Find(index);
            if (cp != null && !cp.enabled) cp.enabled = true;
            if (!s_lookedActivate)
            {
                s_lookedActivate = true;
                s_activate = typeof(CheckpointManager).GetMethod("ActivateFromScript", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance, null, new[] { typeof(int) }, null);
            }
            if (s_activate != null)
            {
                try { s_args[0] = index; s_activate.Invoke(s_activate.IsStatic ? null : mgr, s_args); return mgr.CurrentIndex >= index; }
                catch (Exception e) { Debug.LogWarning("[BOSS] ActivateFromScript(" + index + ") threw: " + e.Message); }
            }
            return cp != null && mgr.TryActivate(cp, null);
        }

        /// <summary>ShifterCharacter.IsScared when core_character published it; true otherwise (everyone counts as scared).</summary>
        public static bool IsScared(ShifterCharacter c)
        {
            if (c == null) return false;
            if (!s_lookedScared)
            {
                s_lookedScared = true;
                s_scared = typeof(ShifterCharacter).GetProperty("IsScared", BindingFlags.Public | BindingFlags.Instance);
                if (s_scared != null && s_scared.PropertyType != typeof(bool)) s_scared = null;
            }
            if (s_scared == null) return true;
            try { return (bool)s_scared.GetValue(c); } catch { return true; }
        }
    }

    /// <summary>Team iteration helpers (no allocations).</summary>
    public static class BossTeam
    {
        public static IReadOnlyList<ShifterCharacter> Members => CharacterManager.Instance != null ? CharacterManager.Instance.Team : (IReadOnlyList<ShifterCharacter>)ShifterCharacter.All;
        public static ShifterCharacter Active => CharacterManager.Instance != null ? CharacterManager.Instance.Active : null;
    }
}
