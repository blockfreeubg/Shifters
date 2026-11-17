// =====================================================================================
//  SHIFTERS - ClueMarker.cs   [owned by the PERSPECTIVE agent]
//  BIG BRAIN VIEW clue painted on a surface (sequence numbers, logic labels, symbols).
//  Hidden unless an IQ-perk character is the viewer. Build with PerspectiveKit.Clue.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class ClueMarker : MonoBehaviour
    {
        public TextMeshPro Text;
        public Revealable Reveal;
        public string Value => Text != null ? Text.text : "";

        /// <summary>Change the painted text (e.g. a randomized sequence digit).</summary>
        public void SetText(string text) { if (Text != null) Text.text = text ?? ""; }
    }
}
