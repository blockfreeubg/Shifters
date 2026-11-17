// =====================================================================================
//  SHIFTERS - SfxTable.cs   [owned by the AUDIO agent]
//  Mixing metadata per SfxId: number of cached variations, base volume, importance (voice
//  stealing), anti-spam cooldown, max simultaneous instances, pitch jitter, loop/UI flags.
//  Immutable after the static constructor (no statics to reset).
// =====================================================================================
using System;

namespace Shifters
{
    public struct SfxInfo
    {
        public int Variants;       // cached variations (random pick, never the same twice in a row)
        public float Volume;       // base playback gain (clips themselves are normalized to -1 dBFS)
        public int Importance;     // 0 trivial .. 10 never stolen
        public float Cooldown;     // min seconds between two starts of this id (spam collapse)
        public int MaxInstances;   // simultaneous voices of this id
        public float Jitter;       // random pitch +/- fraction (0.05 = 5%)
        public bool Loopable;      // rendered as a seamless loop (use AudioManager.PlayLoop)
        public bool UI;            // interface sound: unaffected by slow-mo pitch / pause
        public SfxGroup Group;     // campaign group (Core = the classic ids)
    }

    /// <summary>Campaign SFX groups (CAMPAIGN 19 order). Boss, Heist and Scary are LAZY: they are not part of the boot
    /// pre-warm; AudioManager pre-warms them for scenes that need them (boss chapters / Bank / Night), when boss music
    /// starts, or on first use, and releases them when a scene does not need them (PCM budget).</summary>
    public enum SfxGroup { Core, Story, Boss, Heist, Scary, Weapons }

    public static class SfxTable
    {
        static readonly SfxInfo[] Info = Build();

        public static int Count => Info.Length;

        /// <summary>True for ids of a lazily pre-warmed group (Boss, Heist, Scary).</summary>
        public static bool IsLazy(SfxId id) { var g = Get(id).Group; return g == SfxGroup.Boss || g == SfxGroup.Heist || g == SfxGroup.Scary; }

        public static SfxInfo Get(SfxId id)
        {
            int i = (int)id;
            return i >= 0 && i < Info.Length ? Info[i] : Info[0];
        }

        static SfxInfo[] Build()
        {
            int max = 0;
            foreach (SfxId v in Enum.GetValues(typeof(SfxId))) if ((int)v > max) max = (int)v;
            var t = new SfxInfo[max + 1];
            for (int i = 0; i < t.Length; i++)
                t[i] = new SfxInfo { Variants = 1, Volume = 0.6f, Importance = 4, Cooldown = 0.03f, MaxInstances = 4, Jitter = 0.05f };

            void S(SfxId id, int variants, float vol, int imp, float cooldown = 0.03f, int maxInst = 4, float jitter = 0.05f)
            {
                int i = (int)id;
                if (i < 0 || i >= t.Length) return;
                t[i] = new SfxInfo { Variants = variants, Volume = vol, Importance = imp, Cooldown = cooldown, MaxInstances = maxInst, Jitter = jitter };
            }
            void Ui(SfxId id, int variants, float vol, int imp = 9, float cooldown = 0.03f, int maxInst = 2, float jitter = 0.01f)
            {
                S(id, variants, vol, imp, cooldown, maxInst, jitter);
                t[(int)id].UI = true;
            }
            void Loop(SfxId id, float vol, int imp)
            {
                S(id, 1, vol, imp, 0.05f, 6, 0f);
                t[(int)id].Loopable = true;
            }

            // characters
            S(SfxId.Footstep, 4, 0.32f, 1, 0.025f, 6, 0.06f);
            S(SfxId.FootstepHeavy, 4, 0.42f, 2, 0.025f, 6, 0.05f);
            S(SfxId.Jump, 3, 0.5f, 3, 0.05f, 3);
            S(SfxId.Land, 3, 0.45f, 3, 0.05f, 3);
            S(SfxId.LandHeavy, 2, 0.65f, 4, 0.08f, 3);
            S(SfxId.Tumble, 3, 0.45f, 4, 0.15f, 3);
            S(SfxId.Grab, 2, 0.45f, 4);
            S(SfxId.Throw, 2, 0.5f, 5);
            S(SfxId.Whoosh, 3, 0.5f, 4, 0.05f, 3);
            S(SfxId.Flop, 2, 0.5f, 4);
            S(SfxId.GetUp, 2, 0.3f, 2, 0.1f, 3);
            S(SfxId.KO, 2, 0.7f, 8, 0.2f, 3, 0.03f);
            S(SfxId.Lost, 1, 0.8f, 8, 0.2f, 3, 0.02f);
            S(SfxId.Revive, 1, 0.6f, 8, 0.2f, 3, 0.02f);
            S(SfxId.Captured, 1, 0.65f, 8, 0.2f, 3, 0.02f);
            S(SfxId.Hurt, 3, 0.55f, 5, 0.08f, 3);
            S(SfxId.BridgeForm, 1, 0.6f, 6, 0.2f, 2);
            S(SfxId.BridgeCreak, 3, 0.4f, 2, 0.25f, 2, 0.08f);
            S(SfxId.BridgeCollapse, 1, 0.75f, 7, 0.3f, 2);
            S(SfxId.BridgeRecover, 1, 0.55f, 6, 0.2f, 2);
            S(SfxId.Bonk, 3, 0.6f, 5, 0.05f, 3);
            // switching / perspective
            Ui(SfxId.SwitcherOpen, 1, 0.55f, 9, 0.1f, 1, 0f);
            Ui(SfxId.SwitcherClose, 1, 0.45f, 9, 0.1f, 1, 0f);
            Ui(SfxId.SwitcherTick, 1, 0.3f, 8, 0.03f, 2, 0.03f);
            Ui(SfxId.SwitchWhoosh, 1, 0.55f, 9, 0.2f, 1, 0.02f);
            Ui(SfxId.SwitchLand, 1, 0.55f, 9, 0.2f, 1, 0.02f);
            S(SfxId.ScanPulse, 1, 0.45f, 7, 0.3f, 2, 0f);
            Ui(SfxId.Denied, 1, 0.55f, 9, 0.12f, 1, 0f);
            // UI
            Ui(SfxId.UIHover, 2, 0.22f, 6, 0.04f, 2, 0.02f);
            Ui(SfxId.UIClick, 1, 0.4f);
            Ui(SfxId.UIBack, 1, 0.4f);
            Ui(SfxId.UIError, 1, 0.45f, 9, 0.12f, 1, 0f);
            Ui(SfxId.UIPop, 1, 0.4f, 8, 0.03f, 3, 0.04f);
            Ui(SfxId.Banner, 1, 0.5f, 9, 0.25f, 1, 0f);
            Ui(SfxId.TutorialPop, 1, 0.45f, 9, 0.25f, 1, 0f);
            // puzzles
            S(SfxId.PlatePress, 1, 0.55f, 5, 0.08f, 3);
            S(SfxId.PlateRelease, 1, 0.45f, 5, 0.08f, 3);
            S(SfxId.Lever, 1, 0.6f, 6, 0.15f, 2);
            S(SfxId.ButtonPress, 1, 0.5f, 6, 0.06f, 2);
            S(SfxId.TimerTick, 2, 0.35f, 6, 0.1f, 2, 0f);
            S(SfxId.TimerEnd, 1, 0.6f, 7, 0.3f, 1, 0f);
            S(SfxId.DoorOpen, 1, 0.6f, 6, 0.2f, 3, 0.04f);
            S(SfxId.DoorClose, 1, 0.6f, 6, 0.2f, 3, 0.04f);
            S(SfxId.DoorSlam, 1, 0.75f, 7, 0.2f, 2, 0.04f);
            S(SfxId.Terminal, 3, 0.4f, 5, 0.08f, 2, 0.02f);
            S(SfxId.HackSuccess, 1, 0.55f, 7, 0.3f, 1, 0f);
            S(SfxId.HackFail, 1, 0.55f, 7, 0.3f, 1, 0f);
            Ui(SfxId.PuzzleSolved, 1, 0.6f, 10, 1.0f, 1, 0f);      // long cooldowns: AudioManager auto-plays these on GameEvents,
            Ui(SfxId.Checkpoint, 1, 0.6f, 10, 1.0f, 1, 0f);        // so a second request from UI/puzzles a moment later
            Ui(SfxId.LevelComplete, 1, 0.7f, 10, 6f, 1, 0f);       // collapses into the one already playing (no double fanfare)
            Loop(SfxId.ElevatorHum, 0.35f, 4);
            S(SfxId.Steam, 2, 0.45f, 4, 0.15f, 3);
            S(SfxId.Crusher, 1, 0.75f, 6, 0.1f, 3);
            S(SfxId.LaserZap, 2, 0.5f, 5, 0.06f, 3);
            S(SfxId.Bounce, 2, 0.6f, 5, 0.06f, 3);
            Loop(SfxId.Conveyor, 0.3f, 3);
            S(SfxId.Break, 2, 0.65f, 6, 0.05f, 3);
            S(SfxId.Crumble, 2, 0.55f, 5, 0.1f, 3);
            S(SfxId.Wobble, 2, 0.45f, 3, 0.2f, 2);
            // weapons
            S(SfxId.Pistol, 3, 0.5f, 6, 0.03f, 4);
            S(SfxId.Shotgun, 2, 0.65f, 6, 0.06f, 3);
            S(SfxId.Laser, 2, 0.5f, 6, 0.03f, 4);
            S(SfxId.Slingshot, 2, 0.5f, 6, 0.05f, 3);
            S(SfxId.Chicken, 3, 0.6f, 6, 0.05f, 3, 0.08f);
            S(SfxId.Plunger, 2, 0.55f, 6, 0.05f, 3);
            S(SfxId.Impact, 3, 0.5f, 3, 0.05f, 3);
            S(SfxId.MetalHit, 3, 0.5f, 3, 0.05f, 3);
            S(SfxId.Ricochet, 3, 0.4f, 3, 0.06f, 3, 0.08f);
            S(SfxId.Pickup, 1, 0.55f, 7, 0.1f, 2, 0f);
            S(SfxId.Empty, 1, 0.45f, 5, 0.08f, 2);
            // enemies
            S(SfxId.EnemyBeep, 3, 0.4f, 4, 0.1f, 3);
            S(SfxId.EnemyAlert, 1, 0.6f, 7, 0.2f, 3, 0.03f);
            S(SfxId.EnemyStun, 1, 0.5f, 6, 0.2f, 3);
            Loop(SfxId.Siren, 0.45f, 6);
            S(SfxId.EnemyGrab, 1, 0.55f, 7, 0.15f, 3);
            // misc
            S(SfxId.Explosion, 2, 0.85f, 7, 0.08f, 3, 0.06f);
            S(SfxId.Squeak, 3, 0.45f, 3, 0.05f, 3, 0.08f);
            S(SfxId.Splash, 2, 0.55f, 5, 0.08f, 3);
            S(SfxId.Sizzle, 2, 0.4f, 4, 0.2f, 2);
            Loop(SfxId.Alarm, 0.45f, 6);
            // ---------------------------------------------------------------- campaign (CAMPAIGN 19)
            SfxGroup grp = SfxGroup.Story;
            void G(SfxId id, int variants, float vol, int imp, float cooldown = 0.05f, int maxInst = 3, float jitter = 0.04f, bool ui = false, bool loop = false)
            {
                int i = (int)id;
                if (i < 0 || i >= t.Length) return;
                t[i] = new SfxInfo { Variants = variants, Volume = vol, Importance = imp, Cooldown = cooldown, MaxInstances = maxInst, Jitter = jitter, UI = ui, Loopable = loop, Group = grp };
            }
            // story
            G(SfxId.Kazoo, 2, 0.55f, 7, 0.3f, 2, 0.02f);
            G(SfxId.Cymbal, 2, 0.5f, 6, 0.1f, 3, 0.04f);
            G(SfxId.Gavel, 2, 0.7f, 9, 0.2f, 2, 0.02f);
            G(SfxId.CrowdGasp, 1, 0.6f, 8, 0.6f, 1, 0.03f, true);
            G(SfxId.CrowdCheer, 1, 0.6f, 8, 1.0f, 1, 0.02f, true);
            G(SfxId.Thunder, 1, 0.6f, 7, 0.8f, 2, 0.05f);
            G(SfxId.Whisper, 2, 0.35f, 4, 0.5f, 2, 0.06f);
            G(SfxId.Creak, 2, 0.45f, 4, 0.25f, 2, 0.08f);
            G(SfxId.Klaxon, 1, 0.55f, 8, 0.5f, 2, 0f);
            G(SfxId.Gurgle, 2, 0.45f, 5, 0.25f, 2, 0.05f);
            G(SfxId.PrinterWhir, 1, 0.4f, 5, 0.5f, 2, 0.03f);
            G(SfxId.ColorBurst, 2, 0.6f, 8, 0.15f, 3, 0.03f);
            G(SfxId.SlideWhistleDown, 2, 0.55f, 8, 0.25f, 2, 0.03f);
            G(SfxId.Boo, 2, 0.55f, 8, 0.3f, 2, 0.04f);
            G(SfxId.TVStatic, 2, 0.3f, 5, 0.15f, 2, 0.03f);
            G(SfxId.DialogueBlip, 3, 0.2f, 5, 0.035f, 2, 0.03f, true);
            G(SfxId.ActCard, 1, 0.65f, 10, 2f, 1, 0f, true);
            // bosses
            grp = SfxGroup.Boss;
            G(SfxId.BossRoar, 2, 0.7f, 8, 0.5f, 2, 0.04f);
            G(SfxId.BossHit, 2, 0.7f, 9, 0.08f, 3, 0.03f);
            G(SfxId.BossStomp, 2, 0.75f, 7, 0.12f, 3, 0.05f);
            G(SfxId.BossCharge, 1, 0.55f, 8, 0.3f, 2, 0.02f);
            G(SfxId.BossDefeat, 1, 0.75f, 10, 3f, 1, 0f);
            G(SfxId.Oink, 3, 0.55f, 6, 0.2f, 2, 0.08f);
            G(SfxId.Stamp, 2, 0.6f, 7, 0.08f, 3, 0.04f);
            G(SfxId.DrumSlam, 2, 0.7f, 7, 0.1f, 3, 0.04f);
            G(SfxId.Shockwave, 1, 0.65f, 8, 0.15f, 3, 0.03f);
            G(SfxId.TramBell, 1, 0.55f, 6, 0.4f, 2, 0.01f);
            // heist (lazy)
            grp = SfxGroup.Heist;
            G(SfxId.CameraBeep, 2, 0.3f, 4, 0.2f, 4, 0.01f);
            G(SfxId.CameraSpotted, 1, 0.6f, 9, 0.4f, 2, 0f);
            G(SfxId.VaultSpin, 2, 0.5f, 6, 0.1f, 2, 0.03f);
            G(SfxId.VaultOpen, 1, 0.7f, 10, 2f, 1, 0f);
            G(SfxId.CoinShower, 1, 0.55f, 7, 0.2f, 3, 0.05f);
            // scary (lazy). ScareStinger stays at the normal SFX level (CAMPAIGN 10.1 rule 4: never louder than the SFX bus)
            grp = SfxGroup.Scary;
            G(SfxId.ScareStinger, 2, 0.5f, 9, 2f, 1, 0f);
            G(SfxId.CardboardCreak, 3, 0.45f, 5, 0.2f, 2, 0.08f);
            G(SfxId.VacuumHum, 1, 0.35f, 5, 0.05f, 3, 0f, false, true);
            G(SfxId.StaplerSnap, 2, 0.55f, 6, 0.1f, 3, 0.05f);
            G(SfxId.Heartbeat, 1, 0.45f, 6, 0.3f, 2, 0f, false, true);
            // weapons / tools
            grp = SfxGroup.Weapons;
            G(SfxId.LeafBlower, 2, 0.5f, 6, 0.05f, 3, 0.04f);
            G(SfxId.TShirtPop, 2, 0.6f, 6, 0.05f, 3, 0.05f);
            G(SfxId.CameraFlash, 2, 0.55f, 6, 0.08f, 3, 0.03f);
            G(SfxId.PaintSplat, 3, 0.5f, 4, 0.04f, 4, 0.08f);
            G(SfxId.BitShift, 2, 0.5f, 6, 0.05f, 3, 0.03f);
            G(SfxId.SwapZap, 2, 0.6f, 8, 0.1f, 2, 0.03f);
            G(SfxId.Popper, 2, 0.55f, 6, 0.05f, 3, 0.06f);
            G(SfxId.CapsLock, 2, 0.6f, 6, 0.05f, 3, 0.03f);
            return t;
        }
    }
}
