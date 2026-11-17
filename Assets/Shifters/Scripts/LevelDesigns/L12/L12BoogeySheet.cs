// SHIFTERS - L12BoogeySheet.cs   [SHIFT 11] the bedsheet collider: any shot (every gun / tool) unmasks; SAY CHEESE +2 s dizzy.
// A THROWN intern that hits the solid sheet while CHARGING also unmasks (same rule as L12SheetImpact; walking into it does not).
using UnityEngine;

namespace Shifters
{
    public sealed class L12BoogeySheet : MonoBehaviour, IShootable
    {
        public L12BoogeymanBoss Boss;
        public void OnShot(in ShotInfo shot)
        {
            if (Boss == null) return;
            Boss.Unmask(L12UnmaskKind.Shot, shot.Shooter, shot.Variant == ExperimentalVariant.SayCheese);
        }

        void OnCollisionEnter(Collision c)
        {
            if (Boss == null || !Boss.IsCharging || c.collider == null) return;
            var ch = PuzzleUtil.CharacterFrom(c.collider);
            if (ch != null && ch.Condition == CharacterCondition.Thrown) Boss.Unmask(L12UnmaskKind.Impact, ch);
        }
    }
}
