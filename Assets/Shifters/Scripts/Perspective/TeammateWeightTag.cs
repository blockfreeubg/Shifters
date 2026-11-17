// =====================================================================================
//  SHIFTERS - TeammateWeightTag.cs   [owned by the PERSPECTIVE agent]
//  STRENGTH VIEW sees everybody's weight: "GARY · 112 KG" floating over each character
//  (+ what they carry). Added automatically to every ShifterCharacter by PerspectiveManager.
//  The viewer's own tag only shows while Focused (after a scan pulse) to keep the view clean.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class TeammateWeightTag : MonoBehaviour
    {
        public ShifterCharacter Character { get; private set; }
        public Revealable Reveal { get; private set; }

        int _kg = -1;
        string _name, _text = "";

        /// <summary>Make sure every character in the scene has a weight tag (cheap; called by the 0.25 s poll).</summary>
        public static void EnsureAll()
        {
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.Profile == null) continue;
                if (c.GetComponent<TeammateWeightTag>() == null) c.gameObject.AddComponent<TeammateWeightTag>();
            }
        }

        void Awake()
        {
            Character = GetComponent<ShifterCharacter>();
            Reveal = gameObject.AddComponent<Revealable>();
            Reveal.Category = PerspectiveMask.Strength;
            Reveal.Highlight = false;
            Reveal.ShowOnlyWhenRevealed = false;
            Reveal.PopIn = false;
            Reveal.Renderers = new Renderer[0];
            Reveal.LabelOffset = new Vector3(0f, (Character != null ? Character.Height : 1.8f) + 0.55f, 0f);
            Reveal.DynamicLabel = Compose;
        }

        string Compose(ShifterCharacter viewer)
        {
            var c = Character;
            if (c == null || c.State == CharacterState.Lost || c.State == CharacterState.Captured) return "";
            if (viewer == c && (PerspectiveManager.Instance == null || !PerspectiveManager.Instance.Focused)) return "";
            if (c.Condition == CharacterCondition.Carried) return ""; // the carrier's tag already includes them
            int kg = Mathf.RoundToInt(c.TotalMass);
            if (kg != _kg || _name == null)
            {
                _kg = kg;
                _name = c.DisplayName.ToUpperInvariant();
                _text = c.Grabber != null && c.Grabber.IsHolding ? _name + " · " + kg + " KG (LOADED)" : _name + " · " + kg + " KG";
            }
            return _text;
        }
    }
}
