// =====================================================================================
//  SHIFTERS - Interfaces.cs   [CONTRACT FILE]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Anything the active character can use with the Interact key (E): levers, buttons, terminals,
    /// weapon pickups, exit consoles, volunteer chairs, cell releases...
    /// Found by InteractionSensor (overlap sphere, radius Tuning.InteractRadius) on the active character.
    /// Implementers must be a Component with a Collider (trigger or solid) on the same GameObject or a child.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Higher wins when several are in range (default 0).</summary>
        int InteractPriority { get; }
        /// <summary>World position used for distance/facing checks and the prompt anchor.</summary>
        Vector3 InteractPoint { get; }
        /// <summary>false => prompt is shown blocked in red with <paramref name="blockedReason"/> ("Requires IQ 7 - Gary has 3").
        /// Return false with a null reason to hide the prompt entirely.</summary>
        bool CanInteract(ShifterCharacter who, out string blockedReason);
        /// <summary>Short verb phrase shown after the key cap, e.g. "Pull lever", "Hack terminal".</summary>
        string GetPrompt(ShifterCharacter who);
        void Interact(ShifterCharacter who);
    }

    /// <summary>Receives weapon hits (hitscan or projectile). Put on the collider's GameObject or a parent.</summary>
    public interface IShootable
    {
        void OnShot(in ShotInfo shot);
    }

    /// <summary>Optional: things that take damage from hazards/explosions besides characters.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 point, Vector3 force);
    }

    /// <summary>Surfaces that move what stands on them without moving themselves (conveyor belts).
    /// CharacterMotor adds this to the ground velocity when the ground collider (or a parent) implements it;
    /// props are pushed by the implementer itself.</summary>
    public interface ISurfaceVelocity
    {
        Vector3 GetSurfaceVelocity(Vector3 worldPoint);
    }

    /// <summary>Puzzle pieces that can be reset (checkpoint restore without reload, or respawn).</summary>
    public interface IResettable
    {
        void ResetState();
    }

    // =====================================================================================
    //  Campaign wave (W0, CAMPAIGN §19)
    // =====================================================================================

    /// <summary>Omnidirectional light area used by the geometric light rule (puzzles' LightRegistry.IsLit; CAMPAIGN §10.2):
    /// light pools, floods, party-popper flares, checkpoint rings. Independent of real Light components (bot-testable).</summary>
    public interface ILightSource
    {
        /// <summary>World-space center of the lit area.</summary>
        Vector3 LightPosition { get; }
        /// <summary>Lit radius in meters (Tuning.LightPoolRadius for pools).</summary>
        float LightRadius { get; }
        /// <summary>False while switched off (breaker down, flare expired).</summary>
        bool LightOn { get; }
    }

    /// <summary>Cone light (rendering's FlashlightRig on the active intern): a point is lit when it is within Range of Origin,
    /// at most HalfAngle degrees off Forward and in clear line of sight.</summary>
    public interface ILightCone
    {
        Vector3 Origin { get; }
        /// <summary>Normalized world direction.</summary>
        Vector3 Forward { get; }
        /// <summary>Meters (Tuning.FlashlightRange).</summary>
        float Range { get; }
        /// <summary>Degrees off-axis (Tuning.FlashlightHalfAngle).</summary>
        float HalfAngle { get; }
        bool LightOn { get; }
    }

    /// <summary>Things the SWAP-O-MATIC can trade places with (shooter and target swap positions; CAMPAIGN §13.2).
    /// Put it on the target's root (the Rigidbody's GameObject) or a parent of the hit collider.</summary>
    public interface ISwappable
    {
        /// <summary>World point the shooter's feet land on (the target's feet / base center).</summary>
        Vector3 SwapPoint { get; }
        /// <summary>false with a toast reason ("NO ROOM TO SWAP", "HR SAYS NO.") refuses the swap.</summary>
        bool CanSwap(ShifterCharacter by, out string reason);
        /// <summary>Teleport the target so its SwapPoint lands on pos (velocity zeroed, facing rot).</summary>
        void SwapTo(Vector3 pos, Quaternion rot);
    }

    /// <summary>Things the BIT-SHIFTER can resize (puzzles' Shiftable; cycle NORMAL -> HUGE -> TINY -> NORMAL).</summary>
    public interface IResizable
    {
        /// <summary>-1 = TINY (x Tuning.TinyScale size, x Tuning.TinyMassMul mass), 0 = NORMAL, 1 = HUGE (x Tuning.HugeScale, x Tuning.HugeMassMul).</summary>
        int SizeClass { get; }
        /// <summary>Advance to the next size class. false with a toast reason ("OVERFLOW ERROR", "HR SAYS NO.") if refused.</summary>
        bool TryCycleSize(ShifterCharacter by, out string reason);
    }

    /// <summary>Things the CAPS-LOCK CANNON can lock in place (movers, hazards, props, bots; CAMPAIGN §13.2).</summary>
    public interface IFreezable
    {
        /// <summary>Freeze for seconds (re-freezing extends to the later end time).</summary>
        void Freeze(float seconds);
        bool IsFrozen { get; }
    }
}
