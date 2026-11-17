// =====================================================================================
//  SHIFTERS - Tuning.cs   [CONTRACT FILE - owned by the architect, do not rename members]
//  Single source of truth for every gameplay number. Level builders rely on the
//  "Level metrics" section: every puzzle is designed so it is ALWAYS solvable by the
//  archetype guarantees below, no matter what the random stats roll.
//
//  Archetype guarantees (enforced by CharacterGenerator):
//    * Every team has exactly one Brute, one Sprinter and one Brainiac.
//    * The archetype's primary stat rolls 7..10 (=> it ALWAYS has the perk),
//      the two secondary stats roll 1..5 (=> they NEVER have the perk).
//    * Perks (stat >= 7): STR = HEAVY LIFTER, SPD = TURBO LEGS, IQ = BIG BRAIN.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class Tuning
    {
        // ------------------------------------------------------------------ Physics
        public const float Gravity = 25f;                 // Physics.gravity magnitude (m/s^2)
        public const float FixedTimestep = 1f / 60f;      // physics rate
        public const float FallGravityMultiplier = 1.6f;  // extra gravity on characters while falling
        public const float JumpApexHeight = 1.75f;        // how high a standing jump lifts the feet
        public static float JumpVelocity => Mathf.Sqrt(2f * Gravity * JumpApexHeight); // ~9.35 m/s
        public const float CoyoteTime = 0.12f;
        public const float JumpBufferTime = 0.15f;
        public const float AirControl = 0.55f;            // fraction of ground acceleration usable in air
        public const float CarryJumpMultiplier = 0.75f;   // jump height multiplier while carrying something heavy

        // ------------------------------------------------------------------ Stats
        public const int StatMin = 1;
        public const int StatMax = 10;
        public const int PerkThreshold = 7;
        public const int PrimaryStatMin = 7;
        public const int SecondaryStatMax = 5;
        public static bool HasPerk(int stat) => stat >= PerkThreshold;
        public static float Stat01(int stat) => Mathf.Clamp01((stat - StatMin) / (float)(StatMax - StatMin));

        // SPEED
        public static float WalkSpeed(int spd) => 3.2f + 0.4f * spd;                 // SPD1 3.6 .. SPD5 5.2 .. SPD7 6.0 .. SPD10 7.2
        public static float SprintMultiplier(int spd) => HasPerk(spd) ? 1.7f : 1.3f;  // TURBO LEGS perk
        public static float SprintSpeed(int spd) => WalkSpeed(spd) * SprintMultiplier(spd); // non-turbo max 6.76, turbo min 10.2
        public static float Acceleration(int spd) => 38f + 3f * spd;                  // m/s^2 on ground
        public static float TurnSpeedDegrees(int spd) => 720f + 40f * spd;

        // STRENGTH
        public static float CarryCapacityKg(int str) => 30f + 12f * str;              // STR1 42kg .. STR5 90 .. STR7 114 .. STR10 150
        public static float ThrowSpeed(int str) => Mathf.Lerp(12f, 19f, Stat01(str)); // m/s launch speed
        public const float ThrowBaseAngle = 38f;                                      // degrees above horizontal (camera pitch adds)
        public static float PushSpeed(int str) => Mathf.Lerp(1.6f, 3.2f, Stat01(str)); // m/s when pushing a heavy Pushable
        public static float BridgeCapacityKg(int str) => 125f + 12f * str;            // any single walker (<=120kg) is ALWAYS fine

        // INTELLIGENCE
        public static float HackDuration(int iq) => Mathf.Lerp(4.0f, 1.2f, Stat01(iq)); // seconds to operate a terminal
        public const int HintRealIQ = 7;     // IQ >= 7: a real hint
        public const int HintVagueIQ = 4;    // IQ 4..6: a vague hint, IQ <= 3: a useless joke hint

        // ------------------------------------------------------------------ Bodies (meters, kg)
        public static float BaseHeight(Archetype a) => a == Archetype.Brute ? 1.85f : a == Archetype.Sprinter ? 1.95f : 1.5f;
        public static float CapsuleRadius(Archetype a) => a == Archetype.Brute ? 0.5f : a == Archetype.Sprinter ? 0.33f : 0.42f;
        public static Vector2 MassRange(Archetype a) => a == Archetype.Brute ? new Vector2(100f, 120f)
                                                     : a == Archetype.Sprinter ? new Vector2(55f, 65f)
                                                     : new Vector2(65f, 75f);
        public const float HeightJitter = 0.04f;          // +-4% random height

        // ------------------------------------------------------------------ Health / damage
        public const float MaxHealth = 100f;
        public const float RegenDelay = 4f;
        public const float RegenPerSecond = 6f;
        public const float FallDamageMinSpeed = 17f;      // landing faster than this hurts
        public const float FallDamagePerMps = 9f;
        public const float LethalFallY = -60f;            // below this world Y a character is Lost (safety net)

        // ------------------------------------------------------------------ Living bridge
        public const float BridgeLength = 6.5f;
        public const float BridgeWidth = 1.1f;
        public const float BridgeThickness = 0.35f;
        public const float BridgeCollapseDelay = 0.75f;   // seconds overloaded before collapsing

        // ------------------------------------------------------------------ Level metrics (for level builders)
        // Derived from the numbers above: standing jump air-time ~0.67s.
        public const float GapEveryoneMax = 2.0f;         // any character can jump it walking
        public const float GapBridgeMin = 5.0f;           // non-turbo characters can NOT jump (max ~4.5m)...
        public const float GapBridgeMax = 5.6f;           // ...but a 6.5m living bridge spans it
        public const float GapTurboMin = 5.4f;            // only TURBO LEGS (min ~6.8m sprint jump) can clear
        public const float GapTurboMax = 6.0f;
        public const float LedgeJumpMax = 1.2f;           // anyone can jump up
        public const float LedgeStackMin = 2.4f;          // can't be jumped alone...
        public const float LedgeStackMax = 2.7f;          // ...but reachable standing on ANY teammate's head
        public const float LedgeThrowMax = 4.0f;          // a Brute can throw a teammate up this high (ledge depth >= 3m)
        public const float DoorMinHeight = 4.0f;          // openings must allow carrying a teammate overhead
        public const float DoorMinWidth = 2.6f;

        // Pressure plate tiers (kg). Brute 100-120, Sprinter 55-65, Brainiac 65-75.
        public const float PlateLight = 40f;              // anyone, or a small crate+barrel
        public const float PlateMedium = 100f;            // Brute alone, or Sprinter+Brainiac together/stacked, or heavy block
        public const float PlateHeavy = 150f;             // Brute + anyone, or heavy block
        public const float PlateTeam = 210f;              // all three (or crates)

        // Props (kg)
        public const float CrateSmallKg = 25f;            // anyone can carry/throw
        public const float BarrelKg = 30f;                // anyone can carry/throw
        public const float CrateMediumKg = 60f;           // pushable by anyone (not reliably liftable)
        public const float BlockHeavyKg = 160f;           // needs STR >= 7 to push, nobody can lift
        public const int HeavyPushStrength = 7;
        public const int TerminalIQ = 7;
        public const int RustyLeverStrength = 7;

        // Timed doors: window computed at runtime from the team's fastest sprint speed.
        public const float TimedRouteMin = 20f;           // routes shorter than this can't discriminate turbo vs non-turbo
        public static float TimedWindow(float routeLength, float fastestSprintSpeed)
            => routeLength / Mathf.Max(1f, fastestSprintSpeed) * 1.2f + 0.5f;

        // ------------------------------------------------------------------ Enemies (security bots)
        public const float BotPatrolSpeed = 2.5f;
        public const float BotChaseSpeed = 6.0f;          // faster than slow walkers, slower than any TURBO sprint
        public const float BotSightRange = 14f;
        public const float BotFov = 110f;
        public const float BotCaptureRadius = 1.1f;
        public const float BotStunTime = 3f;
        public const float BotLoseInterestTime = 3f;

        // ------------------------------------------------------------------ Camera
        public const float CamDistance = 5.5f;
        public const float CamMinDistance = 2.5f;
        public const float CamMaxDistance = 9f;
        public const float CamAimDistance = 2.6f;
        public const float CamPitchMin = -35f;
        public const float CamPitchMax = 70f;
        public const float CamDefaultFov = 60f;
        public const float SwitchLongDistance = 10f;      // beyond this the switch uses the GTA-style sky transition

        // ------------------------------------------------------------------ Switcher
        public const float SwitcherTimeScale = 0.08f;     // slow-mo while the wheel is open
        public const float SwitcherRampTime = 0.18f;      // unscaled seconds to ramp time in/out

        // ------------------------------------------------------------------ Misc
        public const float InteractRadius = 1.9f;
        public const float GrabRadius = 2.0f;
        public const float NoiseThrowLanding = 10f;       // noise radius when a thrown body lands
        public const float NoiseGunshot = 12f;
        public const float NoiseTaunt = 14f;

        // ------------------------------------------------------------------ Campaign wave (W0, CAMPAIGN §19)
        // Bosses (§11.1): every attack is telegraphed; the first telegraph of a fight is the longest.
        public const float BossTelegraphFirst = 1.5f;     // s, first attack of a phase
        public const float BossTelegraphMin = 1.2f;       // s, never shorter (mercy may lengthen it)
        public const float BossHitDamage = 30f;           // damage of a landed boss attack (MaxHealth 100 -> 4 hits KO)
        public const float BossSafeZoneMin = 7f;          // m, a safe zone is at least 7 x 5 m (width) ...
        public const float BossSafeZoneMinDepth = 5f;     // m, ... (depth) - BossArena.ValidateSafeZones
        // Heist (§9): security cameras detect TEAMWORK (two interns within TeamworkRadius for TeamworkSpotSeconds).
        public const float TeamworkRadius = 3f;           // m
        public const float TeamworkSpotSeconds = 0.5f;    // s inside a cone before the alarm
        public const float CameraRange = 12f;             // m, SecurityCamera default range
        public const float CameraFov = 40f;               // deg, SecurityCamera default full cone angle
        public const float GuardSightRange = 10f;         // m, GuardBot / SecurityBot.SightRange default
        public const float GuardFov = 90f;                // deg, GuardBot / SecurityBot.Fov default
        // Night (§10.2): color exists only inside light; stalkers move only when unlit.
        public const float FlashlightRange = 14f;         // m, FlashlightRig cone
        public const float FlashlightHalfAngle = 25f;     // deg
        public const float LightPoolRadius = 3f;          // m, PuzzleKit.LightPool / party-popper flare
        public const float ScaredRadius = 6f;             // m, SCARED = Alive, unlit and no Alive teammate within this radius
        public const float StalkerSpeed = 4.5f;           // m/s
        public const float StalkerGraceAfterSwitch = 1.5f; // s stalkers wait after a character switch
        // Chase (§11.1 CH08 / O7): tram vs pursuer.
        public const float TramSpeed = 6f;                // m/s
        public const float PursuitSpeed = 2.5f;           // m/s
        public const float PursuitStartGap = 40f;         // m
        public const float PursuitMaxGap = 45f;           // m
        // CH13 SOLO POD + BIT-SHIFTER size classes (§13.2).
        public const float SoloPodKg = 105f;              // kg the pod weighs (carry = Brute only)
        public const float SoloPodSpeed = 7f;             // m/s FleeBrain loop speed
        public const float HugeMassMul = 4f;              // HUGE: mass x4
        public const float TinyMassMul = 0.25f;           // TINY: mass x0.25
        public const float HugeScale = 1.6f;              // HUGE: scale x1.6
        public const float TinyScale = 0.6f;              // TINY: scale x0.6
        public const float TimedRouteNewMin = 26f;        // m, timed routes in NEW chapters are at least this long (LEVELS2 §0)
    }
}
