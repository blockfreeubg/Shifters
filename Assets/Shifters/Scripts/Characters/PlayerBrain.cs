// =====================================================================================
//  SHIFTERS - PlayerBrain.cs   [owned by the CORE agent]
//  Enabled only on the active character. Converts ShiftersInput (camera-relative) into
//  CharacterMotor commands (move, sprint, jump, aim-facing). Abilities (grab, bridge, flop,
//  weapon, interact) read their own keys - see each ability component.
//  Does nothing while ExternalControl is set; clears the command when control is lost.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class PlayerBrain : MonoBehaviour
    {
        protected ShifterCharacter character;
        protected virtual void Awake() { character = GetComponent<ShifterCharacter>(); }

        public const float TauntCooldown = 2f;
        float _nextTaunt;

        protected virtual void OnDisable()
        {
            if (character != null && character.Motor != null && !character.ExternalControl) character.Motor.ClearCommand();
        }

        protected virtual void Update()
        {
            if (character == null || character.ExternalControl) return;
            var motor = character.Motor;
            if (motor == null) return;
            if (!character.IsActive || ShiftersInput.Context != InputContext.Gameplay || !character.CanAct)
            {
                motor.ClearCommand();
                return;
            }

            var cam = ShiftersCamera.Instance;
            Vector3 fwd = cam != null ? cam.FlatForward : Vector3.forward;
            Vector3 right = cam != null ? cam.FlatRight : Vector3.right;
            Vector2 mv = ShiftersInput.Move;
            Vector3 move = fwd * mv.y + right * mv.x;
            if (move.sqrMagnitude > 1f) move.Normalize();

            bool aiming = character.Weapon != null && character.Weapon.IsAiming;
            var cmd = new CharacterMotor.Command
            {
                Move = move,
                Sprint = ShiftersInput.SprintHeld,
                Jump = ShiftersInput.JumpPressed,
                JumpHeld = ShiftersInput.JumpHeld,
                Face = aiming ? fwd : Vector3.zero,
            };
            motor.SetCommand(cmd);

            if (ShiftersInput.TauntPressed) TryTaunt();
        }

        /// <summary>Dance/raspberry + noise (Tuning.NoiseTaunt) that draws security bots. 2 s cooldown.</summary>
        public bool TryTaunt()
        {
            if (character == null || !character.CanAct || Time.time < _nextTaunt) return false;
            _nextTaunt = Time.time + TauntCooldown;
            if (character.Visual != null) character.Visual.Play(VisualAction.Taunt);
            character.Say(CharacterQuips.Taunt());
            GameEvents.RaiseNoise(character.Center, Tuning.NoiseTaunt, character, 1.5f);
            return true;
        }
    }
}
