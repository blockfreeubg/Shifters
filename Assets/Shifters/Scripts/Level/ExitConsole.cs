// =====================================================================================
//  SHIFTERS - ExitConsole.cs   [owned by the LEVEL agent]
//  Elevator control panel: "[E] Leave without them". Only offered while someone Alive is
//  missing from the cab. First press warns ("Press E again to leave Gary behind"), a second
//  press within 3 s confirms and the exit completes with whoever is inside.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class ExitConsole : MonoBehaviour, IInteractable
    {
        public LevelExit Exit;
        public const float ConfirmWindow = 3f;

        float _armedUntil = -1f;
        public bool IsArmed => Time.time < _armedUntil;

        public int InteractPriority => 5;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.1f;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (Exit == null || Exit.IsCompleting || who == null) return false;
            if (Exit.MissingAliveNames().Count == 0) return false; // everyone here: the doors close on their own
            if (!Exit.Contains(who)) { blockedReason = "Get in the elevator first"; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => IsArmed ? "CONFIRM: leave them behind" : "Leave without them";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) return;
            var missing = Exit.MissingAliveNames();
            if (!IsArmed)
            {
                _armedUntil = Time.time + ConfirmWindow;
                GameEvents.RaiseToast("Press E again to leave " + JoinNames(missing) + " behind", UIColors.Danger);
                Sfx.At(SfxId.Denied, InteractPoint, 0.8f);
                if (who != null) who.Say("...are we really doing this?");
                return;
            }
            _armedUntil = -1f;
            Sfx.At(SfxId.ButtonPress, InteractPoint);
            GameEvents.RaiseBanner("SHIFT'S OVER", JoinNames(missing) + " will be re-cloned. Probably.", BannerStyle.Warning);
            Exit.LeaveWithoutThem();
        }

        static string JoinNames(System.Collections.Generic.List<string> names)
        {
            if (names.Count == 0) return "nobody";
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return names[0] + " and " + names[1];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }
    }
}
