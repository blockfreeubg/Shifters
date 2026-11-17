// =====================================================================================
//  SHIFTERS - WeaponCustody.cs   [owned by the WEAPONS agent]
//  The team can never lose its gun for good: remembers the team's WeaponData and, when a
//  team (re)spawns (scene reload / checkpoint restart / next level) with nobody holding it and
//  no pickup lying around, hands it back to the checkpoint's WeaponOwnerSlot (else the first
//  Alive member). Needed because Drop() nulls the (shared) session profile's Weapon.
//
//  Campaign W5 (tool lockers / armory): while a TOOL is out, the team's ORIGINAL gun is stashed
//  here (Original). RestoreOriginal() puts it back on whoever holds the tool (else into the
//  session profiles) - at LevelExit, on level completion and when the scene unloads (restart),
//  so saves always keep exactly one real gun per team and tools never leave their chapter.
// =====================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class WeaponCustody
    {
        /// <summary>The team's gun as last seen in someone's hands (null before any character was armed).</summary>
        public static WeaponData TeamWeapon { get; private set; }
        /// <summary>The last NON-tool gun the team held (custody fallback when a tool would otherwise be kept).</summary>
        public static WeaponData LastRealGun { get; private set; }
        /// <summary>The team's original gun, stashed while a locker / armory tool is out (null = no tool out).</summary>
        public static WeaponData Original { get; private set; }
        public static bool HasStash => Original != null;
        /// <summary>RestoreOriginal calls that actually restored something (tests).</summary>
        public static int Restores { get; private set; }
        static int _lastCheckFrame = -1;
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { TeamWeapon = null; LastRealGun = null; Original = null; Restores = 0; _lastCheckFrame = -1; _hooked = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            GameEvents.LevelCompleted += _ => RestoreOriginal();
            SceneManager.sceneUnloaded += _ => RestoreOriginal();
        }

        /// <summary>Called by WeaponHolder.Equip.</summary>
        public static void Remember(WeaponData d)
        {
            if (d == null) return;
            TeamWeapon = d;
            if (!WeaponStyle.IsTool(d)) LastRealGun = d;
        }

        /// <summary>Stash the team's original gun (a tool went out).</summary>
        public static void Stash(WeaponData original) { if (original != null && !WeaponStyle.IsTool(original)) { Original = original; LastRealGun = original; } }

        /// <summary>Take the stash back (the tool returned). Falls back to the last real gun.</summary>
        public static WeaponData Unstash()
        {
            var d = Original ?? LastRealGun;
            Original = null;
            return d;
        }

        /// <summary>Remove the team's current gun from the world WITHOUT spawning anything: from whoever holds it, else the
        /// pickup lying around. Returns it (null if the team has no gun at all).</summary>
        public static WeaponData CollectTeamGun()
        {
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.Weapon != null && c.Weapon.HasWeapon) return c.Weapon.TakeWeapon();
            }
            for (int i = WeaponPickup.All.Count - 1; i >= 0; i--)
            {
                var p = WeaponPickup.All[i];
                if (p == null || p.Data == null) continue;
                var d = p.Data;
                WeaponPickup.RemovePickupsOf(d);
                return d;
            }
            return null;
        }

        /// <summary>W5: put the original gun back (LevelExit / level complete / scene unload). Safe to call any time; does
        /// nothing when no tool is out. The tool goes back to its locker (or vanishes with the scene).</summary>
        public static void RestoreOriginal()
        {
            var orig = Original;
            bool toolInPlay = AnyToolInPlay();
            if (orig == null && !toolInPlay) return;
            if (orig == null) orig = LastRealGun;
            Original = null;
            for (int i = 0; i < ToolSource.All.Count; i++) if (ToolSource.All[i] != null) ToolSource.All[i].ResetOut();
            if (orig == null) return;
            Restores++;

            // live world: the tool holder (or the first armed / owner-slot member) gets the original
            ShifterCharacter holder = null;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.Weapon != null && c.Weapon.HasWeapon) { holder = c; break; }
            }
            for (int i = WeaponPickup.All.Count - 1; i >= 0; i--)
            {
                var p = WeaponPickup.All[i];
                if (p != null && WeaponStyle.IsTool(p.Data)) WeaponPickup.RemovePickupsOf(p.Data);
            }
            TeamWeapon = orig;
            if (holder != null)
            {
                if (WeaponStyle.IsTool(holder.Weapon.Data)) holder.Weapon.Equip(orig);
                FixProfiles(orig, holder.Profile);
                return;
            }
            // nobody holds a gun: profiles (shared with the session) carry it to the next scene
            FixProfiles(orig, null);
        }

        /// <summary>Replace any tool in the session profiles by 'orig'; if no profile is armed, arm the owner slot.</summary>
        static void FixProfiles(WeaponData orig, CharacterProfile keep)
        {
            var session = GameSession.Instance;
            var members = session != null && session.Team != null ? session.Team.Members : null;
            if (members == null) return;
            bool armed = false;
            for (int i = 0; i < members.Length; i++)
            {
                var m = members[i];
                if (m == null || m.Weapon == null) continue;
                if (keep != null && m != keep) { if (WeaponStyle.IsTool(m.Weapon) || m.Weapon == orig) m.Weapon = null; continue; }
                if (WeaponStyle.IsTool(m.Weapon)) m.Weapon = armed ? null : orig;
                if (m.Weapon != null) armed = true;
            }
            if (keep != null) return;
            if (armed) return;
            int slot = session.Snapshot != null ? session.Snapshot.WeaponOwnerSlot : -1;
            if (slot < 0 || slot >= members.Length || members[slot] == null) slot = 0;
            if (members[slot] != null) members[slot].Weapon = orig;
        }

        static bool AnyToolInPlay()
        {
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++) { var c = all[i]; if (c != null && c.Weapon != null && WeaponStyle.IsTool(c.Weapon.Data)) return true; }
            for (int i = 0; i < WeaponPickup.All.Count; i++) { var p = WeaponPickup.All[i]; if (p != null && WeaponStyle.IsTool(p.Data)) return true; }
            var session = GameSession.Instance;
            var members = session != null && session.Team != null ? session.Team.Members : null;
            if (members != null) for (int i = 0; i < members.Length; i++) if (members[i] != null && WeaponStyle.IsTool(members[i].Weapon)) return true;
            return false;
        }

        /// <summary>A profile gun at character init: a tool without an active source in this scene becomes the real gun.</summary>
        public static WeaponData SanitizeProfileGun(WeaponData d)
        {
            if (d == null || !WeaponStyle.IsTool(d)) return d;
            for (int i = 0; i < ToolSource.All.Count; i++) { var s = ToolSource.All[i]; if (s != null && s.IsOut && s.ToolData == d) return d; }
            var real = Original ?? LastRealGun;
            if (real == null) return d;
            Original = null;
            return real;
        }

        /// <summary>If the CharacterManager's team has no gun and none lies in the world, re-equip the remembered one.
        /// Returns true if it handed the gun back. Safe to call any time (once per frame).</summary>
        public static bool EnsureTeamArmed()
        {
            if (_lastCheckFrame == Time.frameCount) return false;
            _lastCheckFrame = Time.frameCount;
            var mgr = CharacterManager.Instance;
            if (mgr == null || TeamWeapon == null) return false;
            var team = mgr.Team;
            if (team == null || team.Count == 0) return false;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c != null && c.Weapon != null && c.Weapon.HasWeapon) { TeamWeapon = c.Weapon.Data; return false; }
            }
            for (int i = 0; i < WeaponPickup.All.Count; i++) if (WeaponPickup.All[i] != null) return false;

            var give = SanitizeProfileGun(TeamWeapon);
            ShifterCharacter target = null;
            var session = GameSession.Instance;
            int slot = session != null && session.Snapshot != null ? session.Snapshot.WeaponOwnerSlot : -1;
            if (slot >= 0 && slot < team.Count && team[slot] != null && team[slot].IsAlive && team[slot].Weapon != null) target = team[slot];
            for (int i = 0; target == null && i < team.Count; i++)
                if (team[i] != null && team[i].IsAlive && team[i].Weapon != null) target = team[i];
            if (target != null) { target.Weapon.Equip(give); return true; }

            // nobody alive: leave it on the floor where the level can find it
            var anyone = team[0];
            Vector3 at = WeaponPickup.FallbackSpawn ?? (anyone != null ? anyone.Feet : Vector3.zero);
            WeaponPickup.SpawnDropped(give, at + Vector3.up, at);
            return true;
        }
    }
}
