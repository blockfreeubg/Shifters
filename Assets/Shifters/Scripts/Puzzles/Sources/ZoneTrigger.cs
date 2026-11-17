using UnityEngine;

namespace Shifters
{
    public enum ZoneFilter { AnyCharacter, Archetype, Perk, WholeTeam }

    /// <summary>
    /// ON while enough qualifying characters are inside its box (local, centered at (0, Size.y/2, 0)).
    /// Filters: any character / a given archetype / a given perk (Strength, Speed, Intellect, Weapon) / the whole
    /// (alive) team. RequiredCount = how many qualifying characters are needed (count &gt;= N).
    /// Uses the character registry (no triggers), so teleports/disabling never leave it stuck.
    /// </summary>
    public class ZoneTrigger : SignalSource
    {
        public Vector3 Size = new Vector3(4f, 3f, 4f);
        public ZoneFilter Filter = ZoneFilter.AnyCharacter;
        public Archetype RequiredArchetype = Archetype.Brute;
        public PerspectiveMask RequiredPerk = PerspectiveMask.Strength;
        public int RequiredCount = 1;
        public bool IncludeDowned;
        public float ReleaseDelay = 0.15f;

        public int Count { get; private set; }
        float _off;

        public bool Qualifies(ShifterCharacter c)
        {
            if (c == null) return false;
            if (!(c.IsAlive || (IncludeDowned && c.State == CharacterState.Downed))) return false;
            switch (Filter)
            {
                case ZoneFilter.Archetype: return c.Archetype == RequiredArchetype;
                case ZoneFilter.Perk: return (c.Perspectives & RequiredPerk) != 0;
                default: return true;
            }
        }

        void FixedUpdate()
        {
            var all = ShifterCharacter.All;
            var center = new Vector3(0f, Size.y * 0.5f, 0f);
            var half = Size * 0.5f;
            int n = 0, alive = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (c.IsAlive) alive++;
                if (Qualifies(c) && PuzzleUtil.CharacterInBox(transform, center, half, c)) n++;
            }
            Count = n;
            bool on = Filter == ZoneFilter.WholeTeam ? (alive > 0 && n >= alive) : n >= Mathf.Max(1, RequiredCount);
            if (on) { _off = 0f; SetOn(true); }
            else if (IsOn) { _off += Time.fixedDeltaTime; if (_off >= ReleaseDelay) SetOn(false); }
        }
    }
}
