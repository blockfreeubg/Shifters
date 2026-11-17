// =====================================================================================
//  SHIFTERS - CallElevatorButton.cs   [PUZZLES - campaign wave]
//  Landing call button (E) for a CallElevator: brings the EMPTY car to its landing (Top = upper landing).
//  Built by PuzzleKit.ElevatorCallButton.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class CallElevatorButton : MonoBehaviour, IInteractable
    {
        public CallElevator Elevator;
        public bool Top;
        public PuzzleLamp Lamp;

        public int InteractPriority => 1;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.05f;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || Elevator == null) return false;
            if (Elevator.Car != null && Elevator.Car.Moving) { blockedReason = "The elevator is on its way. Probably."; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => "Call elevator";

        public void Interact(ShifterCharacter who)
        {
            if (Elevator == null) return;
            Sfx.At(SfxId.ButtonPress, transform.position);
            PuzzleUtil.Gesture(who, VisualAction.Interact);
            Elevator.Call(Top, who);
        }

        void Update()
        {
            if (Lamp == null || Elevator == null) return;
            bool here = Top ? Elevator.AtTop : Elevator.AtBottom;
            Lamp.Set(here ? PuzzleLampColor.Green : (Elevator.Car != null && Elevator.Car.Moving) ? PuzzleLampColor.Yellow : PuzzleLampColor.Red);
        }
    }
}
