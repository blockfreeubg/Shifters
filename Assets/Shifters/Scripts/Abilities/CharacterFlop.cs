// =====================================================================================
//  SHIFTERS - CharacterFlop.cs   [CONTRACT API - implementation owned by the ABILITIES agent]
//  X: go limp (Condition.Flopped) - a floppy dead-weight ragdoll that teammates can carry/throw
//  and that still counts on pressure plates. X again (when active) gets up (Motor.GetUp: 0.35 s blend,
//  Condition stays Flopped until the blend ends, then the motor sets Normal).
//  Flop: SetCondition(Flopped) + Motor.BeginTumble(9999) (motor never auto-recovers Flopped) + a small
//  sideways topple impulse. Inactive flopped characters stay flopped (plate weights / bait).
//  Being carried keeps the flop: CharacterGrabber restores Flopped after a drop/throw.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public class CharacterFlop : MonoBehaviour
    {
        public ShifterCharacter Character { get; protected set; }
        public bool IsFlopped => Character != null && Character.Condition == CharacterCondition.Flopped;
        /// <summary>(character, flopped) after a voluntary flop / get-up.</summary>
        public event Action<ShifterCharacter, bool> FlopChanged;

        static readonly string[] FlopLines = { "I live here now.", "Wake me when it's solved.", "Dead weight, reporting for duty!", "*dramatic flop*", "Ugh. Mondays.", "I'm a rug now." };
        static readonly string[] UpLines = { "I'm up! I'm up!", "Okay. Back to work.", "Was I asleep?", "Nap complete." };

        protected virtual void Awake() { Character = GetComponent<ShifterCharacter>(); }

        public virtual void SetFlopped(bool flopped)
        {
            var c = Character;
            if (c == null || !c.IsAlive) return;
            if (flopped)
            {
                if (IsFlopped) return;
                var cond = c.Condition;
                if (cond != CharacterCondition.Normal && cond != CharacterCondition.Thrown && cond != CharacterCondition.Stunned) return;
                if (c.Grabber != null) c.Grabber.ForceRelease(); // whatever you held tumbles off your head
                if (!c.SetCondition(CharacterCondition.Flopped)) return;
                if (c.Motor != null) c.Motor.BeginTumble(9999f);
                var body = c.Body;
                if (body != null && !body.isKinematic)
                {
                    float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                    body.AddForce(transform.right * (1.8f * side) + Vector3.up * 1.6f + transform.forward * 0.4f, ForceMode.VelocityChange);
                    body.AddTorque(transform.forward * (-6f * side), ForceMode.VelocityChange);
                }
                if (c.Visual != null) { c.Visual.SetPose(PoseOverride.Flop); c.Visual.SetExpression(Expression.Dizzy); }
                Sfx.At(SfxId.Flop, c.Center);
                Sfx.Voice(c, VoiceLine.Laugh);
                if (UnityEngine.Random.value < 0.6f) c.Say(FlopLines[UnityEngine.Random.Range(0, FlopLines.Length)]);
                var s = GameSession.Instance != null ? GameSession.Instance.Stats : null;
                if (s != null) s.Flops++;
                FlopChanged?.Invoke(c, true);
            }
            else
            {
                if (!IsFlopped) return;
                // blended 0.35 s get-up; the motor returns Flopped -> Normal at the end of the blend
                if (c.Motor != null) c.Motor.GetUp();
                else c.SetCondition(CharacterCondition.Normal);
                if (c.Visual != null) { c.Visual.SetPose(PoseOverride.None); c.Visual.SetExpression(Expression.Neutral); }
                Sfx.At(SfxId.GetUp, c.Center);
                Fx.Puff(c.Feet, 0.6f);
                if (UnityEngine.Random.value < 0.4f) c.Say(UpLines[UnityEngine.Random.Range(0, UpLines.Length)]);
                FlopChanged?.Invoke(c, false);
            }
        }

        /// <summary>Flop / get up.</summary>
        public void Toggle() => SetFlopped(!IsFlopped);

        protected virtual void Update()
        {
            if (!AbilityInput.HasControl(Character) || !ShiftersInput.FlopPressed) return;
            Toggle();
        }
    }
}
