// =====================================================================================
//  SHIFTERS - L14BenchSeat.cs   [LEVEL 14 - one seat of THE BENCH FOR THREE]
//  E to sit (only while Open). Holds its sitter until Release() or until the seat is closed (Open = false releases).
//  ON while occupied. Level-local (PuzzleKit.Seat is the L3 volunteer chair with its own banners and timers).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L14BenchSeat : SignalSource, IInteractable
    {
        public Transform Seat;
        public Transform ExitPoint;
        public bool Open;
        public string ClosedReason = "THE THRONE IS STILL A THRONE. (FOR NOW.)";
        public ShifterCharacter Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;

        static readonly string[] SitLines = { "Comfy. Suspiciously comfy.", "This bench has great posture.", "Is this... power?", "Scoot over. No, the other way." };
        static int _line;

        public int InteractPriority => 3;
        public Vector3 InteractPoint => (Seat != null ? Seat.position : transform.position) + Vector3.up * 0.6f;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || !who.CanAct) return false;
            if (!Open) { blockedReason = ClosedReason; return false; }
            if (IsOccupied) { blockedReason = Occupant == who ? null : "NO SAVING SEATS. " + Occupant.DisplayName + " IS SITTING THERE."; return false; }
            if (who.Grabber != null && who.Grabber.IsHolding) { blockedReason = "Hands full. Put it down first."; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => Open ? "Take a seat" : "Bench (adjourned)";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out var reason))
            {
                if (reason != null) { Sfx.At(SfxId.Denied, transform.position); GameEvents.RaiseToast(reason, UIColors.AccentYellow); }
                return;
            }
            SeatCharacter(who);
        }

        public bool SeatCharacter(ShifterCharacter c)
        {
            if (c == null || IsOccupied || !c.IsAlive) return false;
            if (c.Grabber != null && c.Grabber.IsHolding) c.Grabber.ForceRelease();
            if (!c.SetCondition(CharacterCondition.Seated)) return false;
            if (c.Body != null)
            {
                if (!c.Body.isKinematic) { c.Body.linearVelocity = Vector3.zero; c.Body.angularVelocity = Vector3.zero; }
                c.Body.isKinematic = true;
            }
            Occupant = c;
            Snap();
            if (c.Visual != null) { c.Visual.SetPose(PoseOverride.Seated); c.Visual.SetExpression(Expression.Happy, 2.5f); }
            c.Say(SitLines[_line++ % SitLines.Length]);
            Sfx.At(SfxId.Squeak, transform.position, 0.8f, 0.9f + 0.1f * (_line % 3));
            Fx.Sparks(transform.position + Vector3.up * 1.4f, Vector3.up, 10, VillainLook.Gold);
            SetOn(true);
            return true;
        }

        public void Release()
        {
            var c = Occupant;
            if (c == null) return;
            Occupant = null;
            var exit = ExitPoint != null ? ExitPoint : transform;
            if (c.Body != null) c.Body.isKinematic = false;
            if (c.Motor != null) c.Motor.Teleport(exit.position, exit.rotation); else c.transform.SetPositionAndRotation(exit.position, exit.rotation);
            c.SetCondition(CharacterCondition.Normal);
            if (c.Visual != null) { c.Visual.SetPose(PoseOverride.None); c.Visual.SetExpression(Expression.Happy, 2f); }
            SetOn(false);
        }

        void Snap()
        {
            var c = Occupant;
            if (c == null || Seat == null) return;
            c.transform.SetPositionAndRotation(Seat.position, Seat.rotation);
            if (c.Body != null) { c.Body.position = Seat.position; c.Body.rotation = Seat.rotation; }
        }

        void FixedUpdate()
        {
            var c = Occupant;
            if (c == null) return;
            if (!c.IsAlive || c.Condition != CharacterCondition.Seated) { Occupant = null; SetOn(false); return; }
            if (!Open) { Release(); return; }   // a closed bench never keeps a sitter (no seated softlock, whatever seated them)
            Snap();
        }
    }
}
