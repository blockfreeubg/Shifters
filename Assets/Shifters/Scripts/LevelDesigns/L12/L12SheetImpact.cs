// SHIFTERS - L12SheetImpact.cs   [SHIFT 11] trigger volume on the machine: a THROWN intern (or a >= 50 kg prop at >= 4 m/s)
// while CHARGING unmasks. Walking / sprinting into it does nothing (an intern must be Condition.Thrown, like BossWeakPoint).
using UnityEngine;

namespace Shifters
{
    public sealed class L12SheetImpact : MonoBehaviour
    {
        public L12BoogeymanBoss Boss;
        public float MinKg = 50f, MinSpeed = 4f;
        public int Seen { get; private set; }          // diagnostics for the bot proof
        public string Last { get; private set; } = "";
        void OnTriggerEnter(Collider other) => Try(other);
        void OnTriggerStay(Collider other) { if (Boss != null && Boss.IsCharging) Try(other); }   // thrown before the pause, still tumbling inside
        public void Try(Collider other)
        {
            if (Boss == null || !Boss.IsCharging) return;
            var rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            var c = PuzzleUtil.CharacterFrom(other);
            Seen++; Last = (c != null ? c.DisplayName + " " + c.Condition : rb.name) + " v" + rb.linearVelocity.magnitude.ToString("0.0");
            if (c != null)
            {
                if (c.Condition != CharacterCondition.Thrown || c.TotalMass < MinKg) return;
            }
            else if (rb.mass < MinKg || rb.linearVelocity.magnitude < MinSpeed) return;
            Boss.Unmask(L12UnmaskKind.Impact, c);
        }
    }
}
