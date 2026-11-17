// =====================================================================================
//  SHIFTERS - CharacterFactory.cs   [CONTRACT API - owned by the CORE agent]
//  Builds a complete playable character GameObject from a profile at runtime.
//  Component order matters: ShifterCharacter.Initialize resolves the others.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class CharacterFactory
    {
        public static ShifterCharacter Create(CharacterProfile profile, int slot, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var go = new GameObject("Character_" + slot);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<Rigidbody>();
            go.AddComponent<CapsuleCollider>();
            var c = go.AddComponent<ShifterCharacter>();
            go.AddComponent<CharacterMotor>();
            go.AddComponent<CharacterVisual>();
            go.AddComponent<Grabbable>();
            go.AddComponent<CharacterGrabber>();
            go.AddComponent<CharacterBridge>();
            go.AddComponent<CharacterFlop>();
            go.AddComponent<WeaponHolder>();
            go.AddComponent<WeightBody>();
            go.AddComponent<InteractionSensor>();
            go.AddComponent<CharacterAudio>();
            var brain = go.AddComponent<PlayerBrain>();
            var ai = go.AddComponent<CharacterAI>();
            brain.enabled = false;
            ai.enabled = true;
            c.Initialize(profile, slot);
            // frictionless capsule: no wall sticking, motor drives all grounded motion
            c.Capsule.sharedMaterial = CharacterMotor.FrictionlessMaterial;
            // Late initialization hooks for modules that need the profile.
            foreach (var hook in go.GetComponents<ICharacterModule>()) hook.OnCharacterInitialized(c);
            return c;
        }
    }

    /// <summary>Implemented by character components that need setup after ShifterCharacter.Initialize
    /// (e.g. CharacterVisual builds the model, WeaponHolder equips Profile.Weapon).</summary>
    public interface ICharacterModule
    {
        void OnCharacterInitialized(ShifterCharacter character);
    }
}
