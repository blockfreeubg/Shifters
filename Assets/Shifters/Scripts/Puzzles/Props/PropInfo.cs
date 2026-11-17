using UnityEngine;

namespace Shifters
{
    /// <summary>Strength-perspective weight label for props ("25 KG - YOU CAN CARRY IT", "160 KG - YOU CAN PUSH" /
    /// "TOO HEAVY (STR 7)"). Assigns the Revealable's runtime DynamicLabel.</summary>
    public class PropInfo : MonoBehaviour
    {
        public Revealable Reveal;
        public Rigidbody Body;
        public Pushable Push;

        void Start()
        {
            if (Reveal != null) Reveal.DynamicLabel = Label;
        }

        string Label(ShifterCharacter viewer)
        {
            float kg = Body != null ? Body.mass : 0f;
            string w = Mathf.RoundToInt(kg) + " KG";
            if (viewer == null || viewer.Stats == null) return w;
            if (Push != null) return Push.CanPush(viewer) ? w + " - YOU CAN PUSH" : w + " - TOO HEAVY (STR " + Push.RequiredStrength + ")";
            return kg <= Tuning.CarryCapacityKg(viewer.Stats.Strength) ? w + " - YOU CAN CARRY IT" : w + " - TOO HEAVY TO LIFT";
        }
    }
}
