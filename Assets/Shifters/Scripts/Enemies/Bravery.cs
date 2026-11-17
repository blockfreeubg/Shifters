// =====================================================================================
//  SHIFTERS - Bravery.cs   [owned by the ENEMIES agent]
//  CAMPAIGN §10.2 SCARED rule: IsScared(c) = Alive AND not lit (LightQuery: pools, flashlight cone with
//  line of sight, LightRegistry) AND no Alive teammate within Tuning.ScaredRadius (6 m).
//  No speed/stat penalty. While Active, BraveryTicker calls ShifterCharacter.SetScared(bool) at 4 Hz
//  (seams; bridged by EnemyBridge, so nothing happens if the seam is missing).
//  Active = Override ?? (current level theme is Night OR any StalkerBrain exists).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class Bravery
    {
        public const float TickHz = 4f;
        /// <summary>Force the SCARED system on/off (null = automatic: Night theme or stalkers present).</summary>
        public static bool? Override;
        /// <summary>Extra lit test point height above the feet.</summary>
        public const float ProbeHeight = 1.0f;

        static readonly bool[] _scared = new bool[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Override = null; System.Array.Clear(_scared, 0, _scared.Length); }

        /// <summary>Is the SCARED system running in this level?</summary>
        public static bool Active
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                if (StalkerBrain.All.Count > 0) return true;
                var s = GameSession.Instance;
                if (s != null && s.LevelIndex >= 0)
                {
                    var info = s.CurrentLevel;
                    if (info != null && info.Theme == LevelTheme.Night) return true;
                }
                return false;
            }
        }

        /// <summary>Lit = feet+1 m or the head inside light.</summary>
        public static bool IsLit(ShifterCharacter c)
        {
            if (c == null) return false;
            return LightQuery.IsLit(c.Feet + Vector3.up * ProbeHeight) || LightQuery.IsLit(c.HeadTop - Vector3.up * 0.1f);
        }

        /// <summary>Has an Alive teammate within radius (default Tuning.ScaredRadius)?</summary>
        public static bool HasBuddy(ShifterCharacter c, float radius = -1f)
        {
            if (c == null) return false;
            if (radius < 0f) radius = Tuning.ScaredRadius;
            float r2 = radius * radius;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == null || o == c || !o.isActiveAndEnabled || !o.IsAlive) continue;
                if ((o.Feet - c.Feet).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>The SCARED rule (pure; works whether or not the system is Active).</summary>
        public static bool IsScared(ShifterCharacter c)
        {
            if (c == null || !c.isActiveAndEnabled || !c.IsAlive) return false;
            if (HasBuddy(c)) return false;
            return !IsLit(c);
        }

        /// <summary>Last value pushed by the ticker for a slot (0..7).</summary>
        public static bool LastPushed(ShifterCharacter c) => c != null && c.Slot >= 0 && c.Slot < _scared.Length && _scared[c.Slot];

        /// <summary>One 4 Hz tick: push SetScared for every intern (false for everyone when inactive).</summary>
        public static void Tick()
        {
            bool active = Active;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null) continue;
                bool s = active && IsScared(c);
                int slot = c.Slot;
                if (slot >= 0 && slot < _scared.Length)
                {
                    if (_scared[slot] == s && !active) continue;   // nothing to clear
                    _scared[slot] = s;
                }
                EnemyBridge.SetScared(c, s);
            }
        }
    }
}
