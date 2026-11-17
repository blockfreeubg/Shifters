// =====================================================================================
//  SHIFTERS - CheckpointManager.cs   [owned by the LEVEL agent]
//  Scene singleton (created by LevelManager, or lazily by the first Checkpoint). Activates
//  checkpoints in ascending index order, revives Downed teammates at the checkpoint ("HR retrieved
//  <name>"), saves the CheckpointSnapshot to GameSession, and notifies other modules via the
//  static CheckpointActivated event (weapons: respawn point for a gun lost in the void).
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        /// <summary>Raised after a checkpoint activated and the snapshot was saved (not raised on restore).</summary>
        public static event Action<Checkpoint> CheckpointActivated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; CheckpointActivated = null; }

        public int CurrentIndex { get; private set; } = -1;
        public Checkpoint Current { get; private set; }
        public int ActivationCount { get; private set; }

        static readonly string[] Cheers =
        {
            "Progress! Probably.", "Saved. Unlike my dignity.", "Ooh, a flag!", "Checkpoint! Nobody panic.", "I'm basically a hero now.",
        };
        static readonly string[] BannerQuips =
        {
            "Progress saved. Feelings not saved.", "Your coworkers have been backed up.", "HR is watching. Proudly.",
            "Shift progress saved. Overtime not paid.", "Clocked in. Again.",
            "This counts as a performance review.", "Respawn insurance activated.",
        };
        static readonly string[] RevivedLines =
        {
            "I saw a bright light. It was a fluorescent tube.", "Did I miss anything?", "Five more minutes...", "Who moved the floor?",
        };

        protected virtual void Awake() { Instance = this; }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        public static CheckpointManager Ensure() => Instance != null ? Instance : new GameObject("CheckpointManager").AddComponent<CheckpointManager>();

        public static Checkpoint Find(int index)
        {
            var all = Checkpoint.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].Index == index) return all[i];
            return null;
        }

        /// <summary>Where a lost item (e.g. the gun) should reappear: the current checkpoint's middle spawn, else null.</summary>
        public static Vector3? RespawnPoint => Instance != null && Instance.Current != null ? Instance.Current.GetSpawnPose(1).position + Vector3.up : (Vector3?)null;

        /// <summary>Ground point where the weapons module respawns a gun lost in the void (WeaponPickup.FallbackSpawn).
        /// Set on level start (start spawn) and on every checkpoint activation / restore.</summary>
        public static void SetItemFallback(Vector3 groundPoint) => WeaponPickup.FallbackSpawn = groundPoint;

        /// <summary>Level start from a snapshot: mark the index as reached without effects.</summary>
        public void RestoreIndex(int index)
        {
            CurrentIndex = index;
            Current = Find(index);
            if (Current != null) SetItemFallback(Current.GetSpawnPose(1).position);
            var all = Checkpoint.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null) { all[i].SetVisualState(all[i].Index <= index, false); all[i].SetLampLit(all[i] == Current); }
        }

        /// <summary>Activate cp if its index is higher than the current one. Returns true when it activated.</summary>
        public bool TryActivate(Checkpoint cp, ShifterCharacter by)
        {
            if (cp == null || cp.Index <= CurrentIndex) return false;
            if (LevelManager.Instance != null && LevelManager.Instance.IsCompleted) return false;
            CurrentIndex = cp.Index;
            Current = cp;
            ActivationCount++;
            var all = Checkpoint.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                if (all[i].Index <= CurrentIndex) all[i].SetVisualState(true, all[i] == cp);
                all[i].SetLampLit(all[i] == cp);
            }

            ReviveDowned(cp);
            SetItemFallback(cp.GetSpawnPose(1).position);
            var snap = BuildSnapshot(cp.Index);
            var session = GameSession.Instance;
            if (session != null)
            {
                session.SaveCheckpoint(snap);
                if (session.Stats != null) session.Stats.Checkpoints++;
            }
            try { CheckpointActivated?.Invoke(cp); } catch (Exception e) { Debug.LogException(e); }
            GameEvents.RaiseBanner("CHECKPOINT " + cp.DisplayNumber, BannerQuips[UnityEngine.Random.Range(0, BannerQuips.Length)], BannerStyle.Checkpoint);
            GameEvents.RaiseCheckpointReached(cp.Index);
            PostFx.Flash(new Color(0.24f, 0.86f, 0.59f, 0.35f), 0.3f);
            if (by != null) by.Say(Cheers[UnityEngine.Random.Range(0, Cheers.Length)]);
            return true;
        }

        /// <summary>Force-activates checkpoint <paramref name="index"/> from code (boss phases, story beats, campaign).
        /// Higher than the current one: a normal activation (banner, revive Downed, snapshot, CheckpointActivated).
        /// Equal to the current one: quietly revives Downed teammates there and re-saves the snapshot (no banner).
        /// Lower, missing, or after the level completed: does nothing and returns false.</summary>
        public static bool ActivateFromScript(int index)
        {
            var cp = Find(index);
            if (cp == null) { Debug.LogWarning("[Checkpoint] ActivateFromScript: no checkpoint with index " + index); return false; }
            var mgr = Ensure();
            if (LevelManager.Instance != null && LevelManager.Instance.IsCompleted) return false;
            if (index > mgr.CurrentIndex) return mgr.TryActivate(cp, null);
            if (index < mgr.CurrentIndex) return false;
            mgr.Current = cp;
            mgr.ReviveDowned(cp);
            SetItemFallback(cp.GetSpawnPose(1).position);
            var session = GameSession.Instance;
            if (session != null) session.SaveCheckpoint(mgr.BuildSnapshot(cp.Index));
            return true;
        }

        void ReviveDowned(Checkpoint cp)
        {
            var cm = CharacterManager.Instance;
            if (cm == null) return;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c != null && c.State == CharacterState.Downed) ReviveAt(cp, c);
            }
        }

        /// <summary>Revive a Downed character at cp's spawn point for its slot (also used when a KO'd teammate is carried in).</summary>
        public void ReviveAt(Checkpoint cp, ShifterCharacter c)
        {
            if (cp == null || c == null || c.State != CharacterState.Downed) return;
            if (c.Grabbable != null && c.Grabbable.IsHeld && c.Grabbable.Holder != null) c.Grabbable.Holder.ForceRelease();
            var pose = cp.GetSpawnPose(c.Slot);
            Fx.Poof(c.Center, c.Color, 1f);
            c.Revive(pose.position + Vector3.up * 0.05f, pose.rotation);
            Fx.Poof(pose.position + Vector3.up * 0.8f, c.Color, 1.3f);
            Sfx.At(SfxId.Revive, pose.position);
            CountRevive();
            GameEvents.RaiseToast("HR retrieved " + c.DisplayName + ". They're fine. Probably.", UIColors.Success);
            c.Say(RevivedLines[UnityEngine.Random.Range(0, RevivedLines.Length)]);
        }

        /// <summary>RunStats.Revives++ (checkpoint and exit revives).</summary>
        public static void CountRevive()
        {
            var s = GameSession.Instance;
            if (s != null && s.Stats != null) s.Stats.Revives++;
        }

        /// <summary>Snapshot of the current team state (called right after reviving).</summary>
        public CheckpointSnapshot BuildSnapshot(int index)
        {
            var snap = new CheckpointSnapshot { CheckpointIndex = index };
            var cm = CharacterManager.Instance;
            if (cm == null) return snap;
            snap.ActiveSlot = Mathf.Max(0, cm.ActiveIndex);
            snap.WeaponOwnerSlot = -1;
            for (int i = 0; i < cm.Team.Count && i < 3; i++)
            {
                var c = cm.Team[i];
                if (c == null) continue;
                snap.States[i] = c.State;
                snap.Health[i] = c.State == CharacterState.Alive ? Mathf.Max(1f, c.Health) : 0f;
                snap.CapturedCellIds[i] = c.State == CharacterState.Captured ? (c.CapturedCellId ?? "") : "";
                bool hasGun = c.Weapon != null ? c.Weapon.HasWeapon : (c.Profile != null && c.Profile.HasWeapon);
                if (hasGun && snap.WeaponOwnerSlot < 0) snap.WeaponOwnerSlot = i;
            }
            return snap;
        }
    }
}
