// =====================================================================================
//  SHIFTERS - L14Envelope.cs   [LEVEL 14 - Max's gold BRIBE ENVELOPE on a lectern (optional gag)]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L14Envelope : MonoBehaviour, IInteractable
    {
        public int Lectern;          // 1..3
        public bool Available;       // slides in on ch14.vote; one per lectern per attempt
        public bool Used { get; private set; }
        public Vector3 Home;

        // same priority as the AYE key, but the point sits ON the envelope (lectern top, 0.45 m west of the key): from the
        // voter plate's stand the AYE key always wins; you take the bribe only by stepping up to the envelope and facing it
        public int InteractPriority => 1;
        public Vector3 InteractPoint => transform.position + new Vector3(0f, -0.2f, -0.3f);

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || !who.CanAct || !Available || Used) return false;
            return L14Hearing.Instance != null && L14Hearing.Instance.Phase == L14Hearing.HearingPhase.Vote;
        }

        public string GetPrompt(ShifterCharacter who) => "Open envelope (\"CONFIDENTIAL\")";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) return;
            Used = true;
            L14Hearing.Instance?.Bribe(who, this);
        }

        public void ResetState() { Used = false; Available = false; transform.position = Home; gameObject.SetActive(false); }
    }
}
