// =====================================================================================
//  SHIFTERS - SoloPodModel.cs   [owned by VISUALS_BOSSES]
//  THE SOLO POD (CH13 capture): Max's gold hover-chair (gameplay mass 105 kg: the level/bosses add the Rigidbody +
//  Grabbable). Built by BossModels.SoloPod. Root = floor contact centre (hover offset is the brain's), front +Z.
//  Optional BoxCollider on the root (grab / carry / swap target). Max sits at MaxSeat.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class SoloPodModel : BossModelBase
    {
        public static readonly string[] PartNames = { "Base", "Jets", "Seat", "Backrest", "Crest", "ArmrestL", "ArmrestR", "MaxSeat", "EjectButton" };
        public override string[] RequiredParts => PartNames;

        public Transform Base, Jets, Seat, Backrest, Crest, MaxSeat, Visual;
        public GameObject Max;
        public BoxCollider Collider;
        public Renderer JetRenderer;
        public Material JetOn, JetOff;
        public float Height = 1.85f;

        /// <summary>Hover jets glow (off while DIZZY on the floor / carried).</summary>
        public void SetJets(bool on)
        {
            if (JetRenderer != null && JetOn != null && JetOff != null) JetRenderer.sharedMaterial = on ? JetOn : JetOff;
        }

        /// <summary>Visual bob / tilt of the chair (the collider stays on the root): height offset m, tilt degrees.</summary>
        public void SetHover(float heightOffset, float tiltDeg = 0f)
        {
            if (Visual != null) { Visual.localPosition = new Vector3(0f, heightOffset, 0f); Visual.localRotation = Quaternion.Euler(tiltDeg, 0f, tiltDeg * 0.5f); }
        }

        /// <summary>DIZZY wobble (degrees) around the vertical axis.</summary>
        public void SetSpin(float degrees)
        {
            if (Visual != null) Visual.localRotation = Quaternion.Euler(0f, degrees, 0f);
        }
    }
}
