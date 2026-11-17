// =====================================================================================
//  SHIFTERS - WeaponArmory.cs   [owned by the WEAPONS agent]
//  CH13 ARMORY root (WeaponKit.Armory): a row of ArmorySlots, one tool each. The team may hold
//  ONE tool at a time (or its real gun); picking a slot returns any other armory tool.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class WeaponArmory : MonoBehaviour
    {
        public ArmorySlot[] Slots = new ArmorySlot[0];
        /// <summary>The slot whose tool is out (null = the team holds its real gun).</summary>
        public ArmorySlot Current
        {
            get { for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null && Slots[i].IsOut) return Slots[i]; return null; }
        }
        public int Changes { get; private set; }
        public event System.Action<WeaponArmory, ArmorySlot> Changed;

        /// <summary>Slot for a tool (null if the armory doesn't stock it).</summary>
        public ArmorySlot SlotOf(ExperimentalVariant tool)
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null && Slots[i].Tool == tool) return Slots[i];
            return null;
        }

        public void NotifyChanged(ArmorySlot s)
        {
            Changes++;
            try { Changed?.Invoke(this, s); } catch (System.Exception e) { Debug.LogException(e); }
        }
    }
}
