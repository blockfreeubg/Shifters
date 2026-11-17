// =====================================================================================
//  SHIFTERS - ScareOMaticModel.cs   [owned by VISUALS_BOSSES]
//  SCARE-O-MATIC 3000 (CH11 unmask): a floor buffer in a bedsheet with a projector nose (it projects the Boogeyman).
//  Built by BossModels.ScareOMatic. Root on the floor, projector +Z. Sheet can be pulled off (SetSheet(false) or animate).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class ScareOMaticModel : BossModelBase
    {
        public static readonly string[] PartNames = { "Buffer", "Brush", "Handle", "Projector", "Sheet", "EyeHoles", "Nameplate" };
        public override string[] RequiredParts => PartNames;

        public Transform Buffer, Brush, Handle, Projector, Sheet, Nameplate;
        public Renderer ProjectorLens;
        public Material LensOff, LensOn;
        public bool SheetOn { get; private set; } = true;

        public void SetSheet(bool on)
        {
            SheetOn = on;
            if (Sheet != null) Sheet.gameObject.SetActive(on);
        }

        public void SetProjector(bool on)
        {
            if (ProjectorLens != null && LensOn != null && LensOff != null) ProjectorLens.sharedMaterial = on ? LensOn : LensOff;
        }

        /// <summary>Spinning buffer pad angle (degrees).</summary>
        public void SetBrushAngle(float degrees)
        {
            if (Brush != null) Brush.localRotation = Quaternion.Euler(0f, degrees, 0f);
        }
    }
}
