// SHIFTERS - L12ChargePunch.cs   [SHIFT 11] Brute punch (STR 7) on the machine, offered only while it is CHARGING on a pad.
using UnityEngine;

namespace Shifters
{
    public sealed class L12ChargePunch : MonoBehaviour, IInteractable
    {
        public L12BoogeymanBoss Boss;
        public int RequiredStrength = 7;
        public int InteractPriority => 5;
        public Vector3 InteractPoint => transform.position;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (Boss == null || Boss.Phase != 1 || Boss.State != BossState.Fighting) return false;
            if (!Boss.IsCharging) { blockedReason = "IT'S NOT CHARGING"; return false; }
            int str = who != null && who.Stats != null ? who.Stats.Strength : 0;
            if (str < RequiredStrength) { blockedReason = "PUNCH NEEDS STR " + RequiredStrength + " - " + (who != null ? who.DisplayName : "?") + " has " + str; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => "Punch the sheet off";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) return;
            Sfx.At(SfxId.Bonk, transform.position, 1f, 0.8f);
            Fx.FloatingText(transform.position + Vector3.up * 1.2f, "POW!", UIColors.Strength, 0.9f);
            Boss.Unmask(L12UnmaskKind.Punch, who);
        }
    }
}
