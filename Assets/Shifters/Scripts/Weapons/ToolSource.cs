// =====================================================================================
//  SHIFTERS - ToolSource.cs   [owned by the WEAPONS agent]
//  Base of the W5 tool givers (ToolLocker, ArmorySlot). One gun per team, always:
//    E on a source whose tool is IN: the team's current gun (in anyone's hand or lying as a
//      pickup) is collected - a real gun goes into WeaponCustody's stash, another source's tool
//      goes back to that source - and the presser gets this tool.
//    E on a source whose tool is OUT: the tool comes back (from whoever holds it / the floor) and
//      the presser gets the stashed original gun.
//  Bots use it exactly like players (TeamBot interacts with bot.GunOwner or anyone).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public abstract class ToolSource : MonoBehaviour, IInteractable
    {
        /// <summary>Every enabled source in the loaded scenes.</summary>
        public static readonly List<ToolSource> All = new List<ToolSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [Tooltip("The tool this source hands out (fixed stats via WeaponGenerator.Tool).")] public ExperimentalVariant Tool = ExperimentalVariant.BitShifter;

        WeaponData _toolData;
        /// <summary>The tool's WeaponData (one instance per source, fixed stats).</summary>
        public WeaponData ToolData { get { if (_toolData == null || _toolData.Variant != Tool) _toolData = WeaponGenerator.Tool(Tool); return _toolData; } }
        public WeaponKind ToolKind => WeaponStyle.KindOf(Tool);
        /// <summary>True while the tool is in the team's hands (or lying around), i.e. NOT in this source.</summary>
        public bool IsOut { get; private set; }
        public int Uses { get; private set; }
        public float LastUseTime { get; private set; } = -999f;

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        /// <summary>The source whose tool this WeaponData is (null for real guns).</summary>
        public static ToolSource SourceOf(WeaponData d)
        {
            if (d == null) return null;
            for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i]._toolData == d) return All[i];
            return null;
        }

        // ------------------------------------------------------------------ IInteractable
        public virtual int InteractPriority => 6;
        public virtual Vector3 InteractPoint => transform.position + Vector3.up * 1.1f;

        public virtual bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || !who.IsAlive || who.Weapon == null) return false;
            if (Time.time - LastUseTime < 0.4f) { blockedReason = "..."; return false; }
            if (who.Grabber != null && who.Grabber.IsHolding) { blockedReason = "Hands full - drop it first"; return false; }
            return true;
        }

        public virtual string GetPrompt(ShifterCharacter who)
        {
            string tool = WeaponStyle.Label(ToolKind);
            if (!IsOut)
            {
                var gun = CurrentTeamGun();
                return gun != null && SourceOf(gun) == null ? "Take " + tool + " (stash " + gun.Name + ")" : "Take " + tool;
            }
            var orig = WeaponCustody.Original ?? WeaponCustody.LastRealGun;
            return orig != null ? "Return " + tool + " (get " + orig.Name + ")" : "Return " + tool;
        }

        public virtual void Interact(ShifterCharacter who) => Exchange(who);

        /// <summary>The swap itself (players via E, bots / tests directly). True if something changed hands.</summary>
        public bool Exchange(ShifterCharacter who)
        {
            if (who == null || who.Weapon == null) return false;
            LastUseTime = Time.time;
            Uses++;
            var gun = WeaponCustody.CollectTeamGun();
            if (!IsOut)
            {
                if (gun != null)
                {
                    var src = SourceOf(gun);
                    if (src != null && src != this) src.ResetOut();       // the other tool goes home
                    else if (src == null) WeaponCustody.Stash(gun);        // the real gun waits in custody
                }
                IsOut = true;
                who.Weapon.EquipFromPickup(ToolData);
                Sfx.At(SfxId.DoorOpen, InteractPoint, 0.6f, 1.3f);
                OnStateChanged();
                return true;
            }
            // return the tool, hand out the original
            if (gun != null && gun != ToolData)
            {
                var src = SourceOf(gun);
                if (src != null) src.ResetOut();
                else if (!WeaponStyle.IsTool(gun)) { WeaponCustody.Stash(gun); } // odd case: a real gun was in play
            }
            IsOut = false;
            var orig = WeaponCustody.Unstash();
            if (orig != null) who.Weapon.EquipFromPickup(orig);
            Sfx.At(SfxId.DoorClose, InteractPoint, 0.6f, 1.2f);
            OnStateChanged();
            return true;
        }

        /// <summary>The tool is back in this source (custody restore, another source took it home). No hand-over.</summary>
        public void ResetOut()
        {
            if (!IsOut) return;
            IsOut = false;
            OnStateChanged();
        }

        static WeaponData CurrentTeamGun()
        {
            var h = WeaponHolder.CurrentOwner;
            if (h != null) return h.Data;
            for (int i = 0; i < WeaponPickup.All.Count; i++) if (WeaponPickup.All[i] != null) return WeaponPickup.All[i].Data;
            return null;
        }

        /// <summary>Visual refresh after IN/OUT changed.</summary>
        protected virtual void OnStateChanged() { }

        /// <summary>Level exit flips LevelManager.IsCompleted before the save: hand the original back right then (W5).</summary>
        protected virtual void Update()
        {
            if (!IsOut) return;
            var lm = LevelManager.Instance;
            if (lm != null && lm.IsCompleted) WeaponCustody.RestoreOriginal();
        }

        /// <summary>Build (or rebuild) a hovering gun model under 'parent' (visual only, no colliders). Null data clears.</summary>
        protected static GameObject BuildModel(WeaponData data, Transform parent, float scale)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Kit.SafeDestroy(parent.GetChild(i).gameObject);
            if (data == null) return null;
            GameObject model = null;
            try { model = WeaponModelBuilder.Build(data, parent); } catch (System.Exception e) { Debug.LogException(e); }
            if (model == null) { model = new GameObject("Weapon_" + data.Type); model.transform.SetParent(parent, false); }
            if (model.GetComponentInChildren<Renderer>() == null) WeaponFallbackModel.Build(data, model.transform);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Kit.SafeDestroy(c);
            model.transform.localScale = Vector3.one * scale;
            var muzzle = WeaponHolder.FindDeep(model.transform, "Muzzle");
            if (muzzle != null) model.transform.localPosition = new Vector3(0f, 0f, -muzzle.localPosition.z * 0.5f * scale);
            return model;
        }
    }
}
