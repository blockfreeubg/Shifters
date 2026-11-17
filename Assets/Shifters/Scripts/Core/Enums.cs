// =====================================================================================
//  SHIFTERS - Enums.cs   [CONTRACT FILE - do not rename/remove members; append only]
//  Saved data stores enum values as ints: new values are ALWAYS appended at the end of an enum.
//  Campaign wave (W0, 2026-11-15): CAMPAIGN.md §19 appends + shared campaign enums (bottom of file).
// =====================================================================================
namespace Shifters
{
    public enum Archetype { Brute, Sprinter, Brainiac }

    /// <summary>Life status. Only Alive characters can be selected/controlled.</summary>
    public enum CharacterState
    {
        Alive,
        Downed,     // KO'd (health 0). Recoverable: carried to a checkpoint / revived when the next checkpoint activates.
        Captured,   // grabbed by a security bot and locked in a HoldingCell. Recoverable by opening the cell.
        Lost        // fell into the void / acid / incinerator. Permanent for this level attempt (re-cloned next level).
    }

    /// <summary>What the (alive) body is currently doing. Drives who owns the Rigidbody.</summary>
    public enum CharacterCondition
    {
        Normal,       // motor in control (player or AI)
        Interacting,  // standing still doing something (hacking a terminal). Motor holds position.
        Carried,      // held by another character's CharacterGrabber (kinematic, parented to CarryPoint)
        Thrown,       // airborne after a throw; tumbling until settled, then gets up -> Normal
        Bridging,     // living bridge plank (CharacterBridge owns the body)
        Flopped,      // voluntary ragdoll flop (X). Dead weight until the player gets up again
        Stunned,      // knocked back / dizzy for a while, then gets up -> Normal
        Seated        // locked into a machine (e.g. VolunteerChair). Kinematic at the seat.
    }

    public enum TeamAIMode { Stay, Follow }

    [System.Flags]
    public enum PerspectiveMask
    {
        None = 0,
        Strength = 1 << 0,   // heavy objects, weights, cracked walls, plates kg
        Speed = 1 << 1,      // speed routes, timed windows, crumbling platforms
        Intellect = 1 << 2,  // hidden clues, hologram paths, logic labels, sequences
        Weapon = 1 << 3,     // shootable targets, remote switches, breakables
        All = Strength | Speed | Intellect | Weapon
    }

    public enum WeaponType { Pistol, Shotgun, LaserGun, Slingshot, Experimental }

    /// <summary>Experimental weapon flavours (WeaponData.Variant, only meaningful when Type == Experimental).
    /// Campaign appends (CAMPAIGN §13): pool guns LeafBlower, TShirtCannon, SayCheese, PaintBlaster (the generator may roll
    /// them); TOOLS BitShifter, SwapOMatic, PartyPopper, CapsLock (lockers / CH13 armory only, fixed stats);
    /// ObjectionMegaphone and MeBucksBlaster are reserved for stretch weapons.</summary>
    public enum ExperimentalVariant
    {
        RubberChickenLauncher, GravityPlunger,
        // ---- campaign appends (W0)
        /// <summary>Pool gun: continuous push cone (props, nudge, bot shove).</summary>
        LeafBlower,
        /// <summary>Pool gun: lobbed T-shirt projectile; a bot hit is WRAPPED (blind 4 s).</summary>
        TShirtCannon,
        /// <summary>Pool gun (P1): camera flash; stuns bots, freezes dummies, blinds security cameras.</summary>
        SayCheese,
        /// <summary>Pool gun (P1): paint-blob projectile with pooled grey-immune splats.</summary>
        PaintBlaster,
        /// <summary>Tool: cycles a Shiftable prop NORMAL -> HUGE -> TINY -> NORMAL.</summary>
        BitShifter,
        /// <summary>Tool: shooter and target swap positions (ISwappable).</summary>
        SwapOMatic,
        /// <summary>Tool: confetti flare = noise lure + temporary 3 m light pool.</summary>
        PartyPopper,
        /// <summary>Tool (P1): locks a target for 6 s (IFreezable).</summary>
        CapsLock,
        /// <summary>Reserved (stretch).</summary>
        ObjectionMegaphone,
        /// <summary>Reserved (stretch).</summary>
        MeBucksBlaster
    }

    /// <summary>Faces. Laugh/Shocked/Pout/Defeated were appended for the villain puppet (CAMPAIGN §18.8); interns without a
    /// dedicated face for them fall back to the neutral face.</summary>
    public enum Expression
    {
        Neutral, Happy, Determined, Strain, Scared, Surprised, Sad, Angry, KO, Dizzy, Smug,
        // ---- campaign appends (W0, villain faces)
        Laugh, Shocked, Pout, Defeated
    }

    public enum AccessoryType { None, HardHat, Headband, Glasses, PropellerCap, Beanie, Mustache, Visor, BowTie, Headphones, Crown, Antenna }

    /// <summary>One-shot gestures played by CharacterVisual.</summary>
    public enum VisualAction
    {
        Wave, FistPump, Punch, Interact, Shrug, Dance, Point, Facepalm, Strain, Celebrate, Taunt,
        // ---- campaign appends (W0)
        /// <summary>The vote (CH14 "all in favor?").</summary>
        RaiseHand,
        /// <summary>Scared flinch (jump scares, SCARED state).</summary>
        Cower,
        /// <summary>Arms out to catch (CH14 catch, trust fall).</summary>
        Catch
    }

    /// <summary>Full-body pose overrides set by abilities (None = locomotion driven by the motor).</summary>
    public enum PoseOverride { None, Bridge, Carried, Carrying, Seated, Hacking, Aiming, Captured, Flop, KO }

    public enum VoiceLine
    {
        Greeting, Selected, Jump, Hurt, Scream, Strain, Thrown, KO, Victory, Taunt, Denied, Idle, Laugh, Lost,
        // ---- campaign appends (W0)
        /// <summary>Short frightened intake of breath (jump scares).</summary>
        Gasp
    }

    public enum BannerStyle
    {
        Info, Success, Warning, Danger, Checkpoint, Puzzle,
        // ---- campaign appends (W0)
        /// <summary>Boss intro / phase banners.</summary>
        Boss,
        /// <summary>Story / chapter banners.</summary>
        Story
    }

    public enum LevelTheme
    {
        Menu, Training, Factory, Lab, Research, Chaos,
        // ---- campaign appends (W0); rendering adds palettes / looks (unknown themes fall back to neutral defaults)
        /// <summary>Federal Color Reserve: grey marble, gold (grey-immune), velvet (Act II).</summary>
        Bank,
        /// <summary>Midnight blue, rain, fog; color visible only inside light (Act III).</summary>
        Night,
        /// <summary>ME TOWER: black glass and gold (Act IV / finale).</summary>
        Tower
    }

    public enum InputContext { Gameplay, Switcher, Menu, Cutscene }

    public enum SfxId
    {
        None,
        // characters
        Footstep, FootstepHeavy, Jump, Land, LandHeavy, Tumble, Grab, Throw, Whoosh, Flop, GetUp,
        KO, Lost, Revive, Captured, Hurt, BridgeForm, BridgeCreak, BridgeCollapse, BridgeRecover, Bonk,
        // switching / perspective
        SwitcherOpen, SwitcherClose, SwitcherTick, SwitchWhoosh, SwitchLand, ScanPulse, Denied,
        // UI
        UIHover, UIClick, UIBack, UIError, UIPop, Banner, TutorialPop,
        // puzzles
        PlatePress, PlateRelease, Lever, ButtonPress, TimerTick, TimerEnd, DoorOpen, DoorClose, DoorSlam,
        Terminal, HackSuccess, HackFail, PuzzleSolved, Checkpoint, LevelComplete, ElevatorHum,
        Steam, Crusher, LaserZap, Bounce, Conveyor, Break, Crumble, Wobble,
        // weapons
        Pistol, Shotgun, Laser, Slingshot, Chicken, Plunger, Impact, MetalHit, Ricochet, Pickup, Empty,
        // enemies
        EnemyBeep, EnemyAlert, EnemyStun, Siren, EnemyGrab,
        // misc
        Explosion, Squeak, Splash, Sizzle, Alarm,
        // ---- campaign appends (W0, CAMPAIGN §19). Until audio adds a recipe an id plays nothing (never throws).
        // story
        Kazoo, Cymbal, Gavel, CrowdGasp, CrowdCheer, Thunder, Whisper, Creak, Klaxon, Gurgle, PrinterWhir, ColorBurst,
        SlideWhistleDown, Boo, TVStatic, DialogueBlip, ActCard,
        // bosses
        BossRoar, BossHit, BossStomp, BossCharge, BossDefeat, Oink, Stamp, DrumSlam, Shockwave, TramBell,
        // heist
        CameraBeep, CameraSpotted, VaultSpin, VaultOpen, CoinShower,
        // scary
        ScareStinger, CardboardCreak, VacuumHum, StaplerSnap, Heartbeat,
        // weapons / tools
        LeafBlower, TShirtPop, CameraFlash, PaintSplat, BitShift, SwapZap, Popper, CapsLock
    }

    public enum MusicId
    {
        None, Menu, Training, Factory, Lab, Research, Chaos, Victory,
        // ---- campaign appends (W0); unknown ids render no music until audio adds them
        /// <summary>"HAIL TO ME" (104 BPM) - Max's theme.</summary>
        Villain,
        /// <summary>"SWING SHIFT" (138 BPM) - Act II bank heist.</summary>
        Heist,
        /// <summary>"NIGHT SHIFT WALTZ" (3/4) - Act III.</summary>
        Spooky,
        /// <summary>Boss fights.</summary>
        Boss,
        /// <summary>The One-Man Band (villain theme, rock orchestra).</summary>
        FinalBoss,
        /// <summary>"SHIFT CHANGE" (full band, key change) - finale / credits.</summary>
        Finale
    }

    // =====================================================================================
    //  Campaign wave shared enums (W0). Referenced by more than one module's CAMPAIGN §18 API, so they live here.
    //  Do NOT redeclare them in a module (duplicate type = compile error).
    // =====================================================================================

    /// <summary>Boss state machine (bosses' BossBase.State; BossStatus.State in GameEvents.BossChanged for the boss bar).</summary>
    public enum BossState { Dormant, Intro, Fighting, Vulnerable, PhaseTransition, Defeated }

    /// <summary>What a SecurityCamera reports (EnemyKit.SecurityCamera): Teamwork = two or more interns close together
    /// (Tuning.TeamworkRadius) inside the cone; AnyIntern = any intern inside the cone.</summary>
    public enum CameraDetect { Teamwork, AnyIntern }

    /// <summary>Look of an EnemyKit.Stalker (enemies' StalkerBrain; models from visuals).</summary>
    public enum StalkerSkin { TeamBuildingDummy, CardboardMax }

    /// <summary>Popper of a ScareKit.JumpScare (story_kit; props from visuals / StoryKit).</summary>
    public enum ScareKind { CardboardMax, GhostSheet, Dummy, Mannequin, FilingCabinet, MirrorFace }

    /// <summary>PuzzleKit.HoldLatch behaviour: HoldThenLatch = input must stay on for N s, then latches (L3HoldLatch);
    /// OpenThenLatch = opens while the input is on and latches after N s (L4SecurityLatch).</summary>
    public enum HoldLatchMode { HoldThenLatch, OpenThenLatch }

    /// <summary>Campaign TOOLS blocked by a PuzzleKit.NoToolZone (purple stripes, "HR SAYS NO."). Flags.
    /// Tool -> flag: SwapOMatic -> SwapOMatic (NO-SWAP), BitShifter -> BitShifter (NO-SIZE), CapsLock, PartyPopper.</summary>
    [System.Flags]
    public enum ToolMask
    {
        None = 0,
        SwapOMatic = 1 << 0,
        BitShifter = 1 << 1,
        CapsLock = 1 << 2,
        PartyPopper = 1 << 3,
        All = SwapOMatic | BitShifter | CapsLock | PartyPopper
    }

    /// <summary>Extreme test teams (bots: TeamBot.ForceTeamExtreme; run_level_test -botTeams MIN,MAX; CAMPAIGN §18.8).
    /// Min: Brute 100 kg STR 7, Sprinter 55 kg SPD 7, Brainiac 65 kg IQ 7 (gun on the Brute).
    /// Max: 120 / 65 / 75 kg, primaries 10 (gun on the Brainiac). None = normal random team.</summary>
    public enum ExtremeTeam { None, Min, Max }
}
