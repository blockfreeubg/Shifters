// =====================================================================================
//  SHIFTERS - EnemyReactions.cs   [owned by the ENEMIES agent]
//  The weapon/tool reaction surface of every campaign enemy (SecurityBot/GuardBot, StalkerBrain,
//  SecurityCamera). The weapons agent can either just call IShootable.OnShot(shot) - every enemy
//  dispatches the POOL-GUN variants itself (EnemyReact.Classify) - or call these methods directly.
//  Tools use their own contract interfaces: CAPS-LOCK -> IFreezable.Freeze(6), BIT-SHIFTER ->
//  IResizable.TryCycleSize, SWAP-O-MATIC -> ISwappable. OnShot for those three tool variants only
//  plays a spark (so calling both never doubles the effect).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Clean reaction methods for weapons/tools/levels. All are safe to call repeatedly (timers extend).</summary>
    public interface IEnemyReactions : IFreezable
    {
        /// <summary>Sight 0 for seconds (T-SHIRT "WRAPPED" 4 s, SAY CHEESE on cameras 5 s). Bots wander, cameras go dark.</summary>
        void Blind(float seconds, string label = null);
        bool IsBlind { get; }
        /// <summary>Stunned for seconds (leaf blower 1 s, popper dance 2 s, swap 1.5 s, cheese 2 s). Bots drop prisoners.</summary>
        void Stun(float seconds, string label = null);
        /// <summary>Add a velocity (m/s, world) and optionally stun (leaf blower: 3 m/s + 1 s).</summary>
        void Shove(Vector3 velocity, float stunSeconds = 0f);
        /// <summary>Investigate / walk to a noise point (party popper landing, taunt).</summary>
        void Lure(Vector3 point, float priority = 1f);
    }

    public enum EnemyHit { Generic, LeafBlower, TShirt, SayCheese, Paint, PartyPopper, ToolOnly }

    /// <summary>Reaction numbers (CAMPAIGN §6 weapon table) + the shot classifier shared by every enemy.</summary>
    public static class EnemyReact
    {
        public const float WrapBlindSeconds = 4f;          // T-SHIRT CANNON
        public const float BlowerStunSeconds = 1f;         // LEAF BLOWER (max 1 s per 2 s)
        public const float BlowerStunCooldown = 2f;
        public const float BlowerShoveSpeed = 3f;
        public const float BlowerLedgeDrop = 1.5f;         // blown off a >= 1.5 m ledge
        public const float BlowerLedgeStunSeconds = 3f;
        public const float PopperDanceSeconds = 2f;        // PARTY POPPER direct hit
        public const float SwapStunSeconds = 1.5f;         // SWAP-O-MATIC (ISwappable.SwapTo)
        public const float CheeseBotStunSeconds = 2f;      // SAY CHEESE
        public const float CheeseFreezeSeconds = 3f;       // SAY CHEESE on dummies (counts as lit)
        public const float CheeseCameraBlindSeconds = 5f;  // SAY CHEESE on security cameras
        public const float CameraShotDisableSeconds = 5f;  // any other hit on a security camera
        public const float CapsLockSeconds = 6f;           // CAPS-LOCK (IFreezable.Freeze)
        public const float TinySeconds = 8f;               // BIT-SHIFTER on bots (IResizable)
        public const float TinyMass = 15f;

        /// <summary>Which reaction a shot asks for. Pool guns are recognised by ShotInfo.Variant (any WeaponType).</summary>
        public static EnemyHit Classify(in ShotInfo shot)
        {
            switch (shot.Variant)
            {
                case ExperimentalVariant.LeafBlower: return EnemyHit.LeafBlower;
                case ExperimentalVariant.TShirtCannon: return EnemyHit.TShirt;
                case ExperimentalVariant.SayCheese: return EnemyHit.SayCheese;
                case ExperimentalVariant.PaintBlaster: return EnemyHit.Paint;
                case ExperimentalVariant.PartyPopper: return EnemyHit.PartyPopper;
                case ExperimentalVariant.BitShifter:
                case ExperimentalVariant.SwapOMatic:
                case ExperimentalVariant.CapsLock: return EnemyHit.ToolOnly;
                default: return EnemyHit.Generic;
            }
        }

        /// <summary>Planar push direction of a shot (falls back to the normal).</summary>
        public static Vector3 PushDir(in ShotInfo shot)
        {
            Vector3 d = shot.Direction; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) { d = -shot.Normal; d.y = 0f; }
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
        }
    }
}
