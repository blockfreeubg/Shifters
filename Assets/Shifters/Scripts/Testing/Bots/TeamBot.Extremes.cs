// =====================================================================================
//  SHIFTERS - TeamBot.Extremes.cs   [BOTS module - campaign wave]
//  Extreme teams (CAMPAIGN §18.8 / §22): every chapter proof runs seeds 1,2,3 + MIN + MAX in ONE launch.
//    MIN: Brute 100 kg STR 7, Sprinter 55 kg SPD 7, Brainiac 65 kg IQ 7 - the GUN on the Brute
//    MAX: Brute 120 kg STR 10, Sprinter 65 kg SPD 10, Brainiac 75 kg IQ 10 - the GUN on the Brainiac
//  Secondary stats are the extreme LEGAL values of CharacterGenerator's total band (12..17): MIN gives the lowest
//  pair (primary 7 + 3 + 2 = 12), MAX the highest (primary 10 + 4 + 3 = 17), so the team is one a player can roll.
//  Base looks/names/gun kind come from a fixed seed (9001 MIN / 9002 MAX).
//  Command line: -botSeed MIN, -botSeeds 1,2,3,MIN,MAX or -botTeams 1,2,3,MIN,MAX (BotArgs.Seeds returns SeedMin/SeedMax).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed partial class TeamBot
    {
        /// <summary>Seed sentinel for the MIN extreme team (BotArgs "MIN"). Negative: never a real explicit seed.</summary>
        public const int SeedMin = -101;
        /// <summary>Seed sentinel for the MAX extreme team (BotArgs "MAX").</summary>
        public const int SeedMax = -102;
        public const int ExtremeBaseSeedMin = 9001, ExtremeBaseSeedMax = 9002;

        /// <summary>The extreme team forced last (None after a normal ForceTeam).</summary>
        public static ExtremeTeam ForcedExtreme { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetExtremeStatics() => ForcedExtreme = ExtremeTeam.None;

        /// <summary>Seed sentinel for an extreme team (SeedMin / SeedMax; 0 for None).</summary>
        public static int SeedOf(ExtremeTeam e) => e == ExtremeTeam.Min ? SeedMin : e == ExtremeTeam.Max ? SeedMax : 0;

        /// <summary>"MIN" / "MAX" for the sentinels, else the seed number.</summary>
        public static string TeamKey(int seed) => seed == SeedMin ? "MIN" : seed == SeedMax ? "MAX" : seed.ToString();

        /// <summary>Key of the team GameSession holds now ("MIN"/"MAX" when that extreme team is in play).</summary>
        public static string CurrentTeamKey
        {
            get
            {
                var gs = GameSession.Instance;
                int s = gs != null ? gs.Seed : 0;
                if (ForcedExtreme == ExtremeTeam.Min && s == ExtremeBaseSeedMin) return "MIN";
                if (ForcedExtreme == ExtremeTeam.Max && s == ExtremeBaseSeedMax) return "MAX";
                return s.ToString();
            }
        }

        /// <summary>True if GameSession already holds the team of this seed / sentinel.</summary>
        public static bool IsCurrentTeam(int seed) => CurrentTeamKey == TeamKey(seed);

        /// <summary>Start a new run with the extreme team (call before the level spawns, or use ReloadWithSeed(SeedOf(e))).</summary>
        public static void ForceTeamExtreme(ExtremeTeam e)
        {
            if (e == ExtremeTeam.None) { ForceTeam(1); return; }
            var gs = GameSession.Instance;
            if (gs == null) { Debug.LogWarning("[DEVPLAY] [BOT] ForceTeamExtreme(" + e + "): no GameSession"); return; }
            gs.NewRun(e == ExtremeTeam.Min ? ExtremeBaseSeedMin : ExtremeBaseSeedMax);
            ApplyExtreme(gs.Team, e);
            ForcedExtreme = e;
            Debug.Log("[DEVPLAY] [BOT] ForceTeamExtreme " + e + " -> " + DescribeTeam(gs.Team));
        }

        /// <summary>Rewrite a generated team in place to the extreme stats / masses / gun owner (exposed for tests).</summary>
        public static void ApplyExtreme(TeamData team, ExtremeTeam e)
        {
            if (team == null || team.Members == null || e == ExtremeTeam.None) return;
            bool max = e == ExtremeTeam.Max;
            WeaponData gun = null;
            foreach (var m in team.Members) if (m != null && m.HasWeapon) { gun = m.Weapon; break; }
            if (gun == null) gun = new WeaponData();
            var gunArchetype = max ? Archetype.Brainiac : Archetype.Brute;
            foreach (var m in team.Members)
            {
                if (m == null) continue;
                int primary = max ? 10 : 7, hi = max ? 4 : 3, lo = max ? 3 : 2;
                var st = new CharacterStats();
                switch (m.Archetype)
                {
                    case Archetype.Brute: st.Strength = primary; st.Speed = hi; st.IQ = lo; m.Mass = max ? 120f : 100f; break;
                    case Archetype.Sprinter: st.Speed = primary; st.Strength = hi; st.IQ = lo; m.Mass = max ? 65f : 55f; break;
                    default: st.IQ = primary; st.Strength = hi; st.Speed = lo; m.Mass = max ? 75f : 65f; break;
                }
                m.Stats = st;
                m.Weapon = m.Archetype == gunArchetype ? gun : null;
            }
        }
    }
}
