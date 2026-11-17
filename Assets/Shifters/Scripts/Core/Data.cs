// =====================================================================================
//  SHIFTERS - Data.cs   [CONTRACT FILE - plain serializable data shared by all modules]
//  Append fields if needed; never rename/remove existing ones.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    [Serializable]
    public class CharacterStats
    {
        public int Speed = 5;
        public int Strength = 5;
        public int IQ = 5;

        public int Get(PerspectiveMask stat) =>
            stat == PerspectiveMask.Speed ? Speed : stat == PerspectiveMask.Strength ? Strength : stat == PerspectiveMask.Intellect ? IQ : 0;

        public CharacterStats Clone() => new CharacterStats { Speed = Speed, Strength = Strength, IQ = IQ };
    }

    /// <summary>Randomized body proportions. Multipliers are relative to the archetype's base model.</summary>
    [Serializable]
    public class BodyShape
    {
        public float Height = 1.8f;        // meters (total, feet to top of head) - also the capsule height
        public float Radius = 0.4f;        // capsule radius (meters)
        public float WidthScale = 1f;      // torso width multiplier (0.85..1.2)
        public float HeadScale = 1f;       // head size multiplier (0.85..1.2)
        public float LimbScale = 1f;       // arm/leg length multiplier (0.9..1.1)
        public float BellyScale = 1f;      // torso roundness (0.9..1.3)
        public AccessoryType Accessory = AccessoryType.None;
        public int HairStyle = 0;          // 0 = bald, 1..4 = styles (interpreted by CharacterModelBuilder)
        public Color SkinTone = new Color(1f, 0.82f, 0.66f);
        public Color HairColor = new Color(0.25f, 0.16f, 0.1f);
        public float EyeScale = 1f;        // 0.85..1.25
        public int MouthStyle = 0;         // 0..2 variations
    }

    [Serializable]
    public class WeaponData
    {
        public WeaponType Type;
        public ExperimentalVariant Variant;   // only meaningful when Type == Experimental
        public string Name = "Pew-Pew 3000";
        public float Damage = 20f;            // per hit (per pellet for shotgun)
        public float FireRate = 3f;           // shots per second
        public float Accuracy = 0.85f;        // 0..1 (spread = Lerp(8deg, 0.5deg, Accuracy))
        public float Range = 40f;             // meters (always >= 20 so puzzle targets within 16m are reachable)
        public float Knockback = 10f;         // impulse (velocity change for light bodies, scaled by mass)
        public int Pellets = 1;               // shotgun > 1
        public Color Tint = new Color(1f, 0.55f, 0.1f);

        public float SpreadDegrees => Mathf.Lerp(8f, 0.5f, Mathf.Clamp01(Accuracy));
        public WeaponData Clone() => (WeaponData)MemberwiseClone();
    }

    [Serializable]
    public class CharacterProfile
    {
        public string Name = "Gary";
        public Archetype Archetype;
        public string Title = "THE BRUTE";            // archetype flavour title shown in UI
        public string Personality = "Lifts. Mostly sandwiches.";
        public string Catchphrase = "Let's do this!";
        public Color PrimaryColor = new Color(1f, 0.42f, 0.21f);
        public Color SecondaryColor = new Color(0.15f, 0.15f, 0.2f);
        public CharacterStats Stats = new CharacterStats();
        public float Mass = 80f;                      // kg (used for Rigidbody mass and plates)
        public BodyShape Body = new BodyShape();
        public float VoicePitch = 1f;                 // gibberish voice pitch multiplier
        public int CloneGeneration = 1;               // 1 = original. 2 => "Gary II" (re-cloned after being lost)
        public WeaponData Weapon;                     // null unless this character owns the gun

        public bool HasWeapon => Weapon != null && !string.IsNullOrEmpty(Weapon.Name);
        public bool HasStrengthPerk => Tuning.HasPerk(Stats.Strength);
        public bool HasSpeedPerk => Tuning.HasPerk(Stats.Speed);
        public bool HasIQPerk => Tuning.HasPerk(Stats.IQ);

        public string DisplayName => CloneGeneration <= 1 ? Name : Name + " " + ToRoman(CloneGeneration);

        public PerspectiveMask Perspectives
        {
            get
            {
                var m = PerspectiveMask.None;
                if (HasStrengthPerk) m |= PerspectiveMask.Strength;
                if (HasSpeedPerk) m |= PerspectiveMask.Speed;
                if (HasIQPerk) m |= PerspectiveMask.Intellect;
                if (HasWeapon) m |= PerspectiveMask.Weapon;
                return m;
            }
        }

        public static string ToRoman(int n)
        {
            string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return n >= 0 && n < r.Length ? r[n] : n.ToString();
        }

        public CharacterProfile Clone()
        {
            var c = (CharacterProfile)MemberwiseClone();
            c.Stats = Stats.Clone();
            c.Body = JsonUtility.FromJson<BodyShape>(JsonUtility.ToJson(Body));
            c.Weapon = Weapon?.Clone();
            return c;
        }
    }

    [Serializable]
    public class TeamData
    {
        public int Seed;
        public CharacterProfile[] Members = new CharacterProfile[3];
    }

    [Serializable]
    public class SettingsData
    {
        public float MasterVolume = 0.8f;
        public float MusicVolume = 0.55f;
        public float SfxVolume = 0.8f;
        public float MouseSensitivity = 1f;   // 0.2 .. 3
        public bool InvertY = false;
        public float Fov = 60f;               // 50 .. 90
        public bool CinematicSwitch = true;   // GTA-style sky transition (false = quick glide)
        public bool Tutorials = true;
        public int Quality = 1;               // 0 = Low (no post/shadows soft), 1 = High
        public bool Fullscreen = true;
        public bool ScreenShake = true;

        // ---- campaign appends (W0, CAMPAIGN §15.3). Old saves without these keys keep the defaults below
        //      (SessionPrefs.LoadSettings uses JsonUtility.FromJsonOverwrite on a fresh SettingsData).
        /// <summary>STORY: 0 = FULL, 1 = SHORT, 2 = OFF.</summary>
        public int StoryMode = 0;
        /// <summary>VILLAIN HECKLES: 0 = OFTEN, 1 = SOMETIMES, 2 = NEVER.</summary>
        public int Heckles = 0;
        /// <summary>SPOOKY: false = FULL, true = MILD (CAMPAIGN §10.1 rule 7).</summary>
        public bool SpookyMild = false;
        /// <summary>COLOR SHIFT EFFECT (grey-to-color world, CAMPAIGN §14). false = always full color.</summary>
        public bool ColorShiftFx = true;
        /// <summary>HR ASSISTS (HR INTERVENTION, BOSS MERCY, reprint kiosk; CAMPAIGN §16).</summary>
        public bool HRAssists = true;
        /// <summary>SUBTITLE SPEED multiplier (typewriter / auto-advance), 0.5 .. 2.</summary>
        public float SubtitleSpeed = 1f;
        /// <summary>SHIFT PARTY hot-seat players (1 = solo), 1 .. 3.</summary>
        public int PartyPlayers = 1;
        /// <summary>SHOW FPS: the small performance overlay (FPS / avg / 1% low / ms) for playtest reports (BenchOverlay; also ?bench=1). Off by default.</summary>
        public bool ShowFps = false;
    }

    /// <summary>Team state captured when a checkpoint activates. Restored on "Restart Checkpoint".</summary>
    [Serializable]
    public class CheckpointSnapshot
    {
        public int CheckpointIndex = -1;
        public CharacterState[] States = { CharacterState.Alive, CharacterState.Alive, CharacterState.Alive };
        public string[] CapturedCellIds = { "", "", "" };   // HoldingCell id when Captured
        public float[] Health = { Tuning.MaxHealth, Tuning.MaxHealth, Tuning.MaxHealth };
        public int ActiveSlot = 0;
        public int WeaponOwnerSlot = -1;                     // -1 = nobody / weapon dropped
    }

    [Serializable]
    public class RunStats
    {
        public int Switches, Throws, Bridges, Flops, KnockOuts, Captures, Lost, Revives, ShotsFired, Checkpoints, Restarts;
        public float PlayTime;
        public int LevelsCompleted;
        // ---- campaign appends (W0); old saved runs load them as 0
        /// <summary>Campaign stat (CAMPAIGN §15.2): carries, throws, stacks, bridge crossings, rescues, revives, all-three plates.</summary>
        public int TeamworkViolations;
        /// <summary>Interns re-printed at an INTERN PRINTER kiosk (CharacterManager.Reprint).</summary>
        public int Reprints;
        /// <summary>Times "LET HR HANDLE IT" was used (GameSession.RestartAtNextCheckpoint from the Pause screen).</summary>
        public int HRInterventions;
    }

    [Serializable]
    public struct LevelResult
    {
        public int LevelIndex;
        public float Time;
        public int Survivors;          // characters that reached the exit
        public string[] LeftBehind;    // display names of characters that did not make it
        public int Switches;
        public int Restarts;
    }

    /// <summary>Interaction prompt shown at the bottom of the screen. Key = "E", "F", "LMB"...</summary>
    public struct PromptInfo
    {
        public string Key;
        public string Text;
        public bool Blocked;           // shown red with BlockedReason (e.g. "Requires STR 7 - yours: 3")
        public string BlockedReason;
        public static PromptInfo Make(string key, string text) => new PromptInfo { Key = key, Text = text };
        public static PromptInfo MakeBlocked(string key, string text, string reason) => new PromptInfo { Key = key, Text = text, Blocked = true, BlockedReason = reason };
    }

    [Serializable]
    public class TutorialMessage
    {
        public string Id = "welcome";          // shown once per level attempt (TutorialManager tracks)
        public string Title = "WELCOME TO SHIFTERS";
        public string Body = "";
        public string[] Keys = new string[0];  // key caps to render, e.g. { "W", "A", "S", "D" }
        public float Duration = 7f;            // auto-hide after (unscaled) seconds; 0 = until dismissed/replaced
        public bool Important = false;         // stays until dismissed
    }

    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Radius;
        public ShifterCharacter Source;   // may be null (e.g. a gunshot impact or a crate)
        public float Priority;            // higher = more interesting to enemies (thrown characters 2, gunshots 1)
    }

    public struct ShotInfo
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Direction;
        public float Damage;
        public float Knockback;
        public WeaponType Weapon;
        public ExperimentalVariant Variant;
        public ShifterCharacter Shooter;
        public Collider Collider;
    }

    [Serializable]
    public class LevelInfo
    {
        public int Index;
        public string SceneName;
        public string Title;
        public string Subtitle;
        public string Description;
        public LevelTheme Theme;
        public MusicId Music;
        public string DefaultObjective;

        public LevelInfo(int index, string scene, string title, string subtitle, string description, LevelTheme theme, MusicId music, string objective)
        {
            Index = index; SceneName = scene; Title = title; Subtitle = subtitle; Description = description;
            Theme = theme; Music = music; DefaultObjective = objective;
        }
    }

    // =====================================================================================
    //  Campaign wave (W0, CAMPAIGN §19): event payloads for GameEvents.StoryLineShown / BossChanged / ChaseChanged.
    // =====================================================================================

    /// <summary>One subtitle / dialogue line (GameEvents.StoryLineShown). Raised by the story module, drawn by ui_hud's DialogueBox
    /// (Broadcast = ME-TV / radio picture-in-picture instead of the bottom subtitle bar).</summary>
    [Serializable]
    public struct StoryLine
    {
        /// <summary>Stable speaker token: "MAX", "PERCY", "BRUTE"/"SPRINTER"/"BRAINIAC" (current team member), "GHOST"...</summary>
        public string SpeakerId;
        /// <summary>Display name on the speaker plate (already resolved: "MAX ME", the intern's DisplayName...).</summary>
        public string SpeakerName;
        public string Text;
        public Color SpeakerColor;
        /// <summary>Seconds the line stays up (0 = the DialogueBox decides from the text length).</summary>
        public float Duration;
        /// <summary>true = shown in the ME-TV / radio PiP (non-blocking broadcast).</summary>
        public bool Broadcast;
        public Expression Face;
    }

    /// <summary>Boss bar state (GameEvents.BossChanged). Raised by BossBase on every change; Visible = false hides the bar.</summary>
    [Serializable]
    public struct BossStatus
    {
        public string BossId, Name, Title, BarLabel;
        /// <summary>Current phase (0-based) and phase count.</summary>
        public int Phase, PhaseCount;
        /// <summary>Hits landed in the current phase / hits the phase requires.</summary>
        public int Hits, HitsRequired;
        /// <summary>BossState (contract enum in Enums.cs; CAMPAIGN §19 said "BossState-as-int" before BossState became a contract type).</summary>
        public BossState State;
        public bool Visible;
    }

    /// <summary>Chase meter state (GameEvents.ChaseChanged), e.g. "PORKCHOP: 40 M". Visible = false hides the meter.</summary>
    [Serializable]
    public struct ChaseStatus
    {
        /// <summary>Current pursuer gap and the gap at which the meter is full (meters).</summary>
        public float Gap, MaxGap;
        /// <summary>Current station (0-based) and station count.</summary>
        public int Station, StationCount;
        public bool Busted;
        public bool Visible;
    }
}
