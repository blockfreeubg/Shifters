// =====================================================================================
//  SHIFTERS - DownsizerModel.cs   [owned by VISUALS_BOSSES]
//  THE DOWNSIZER (CH03): 7 m hydraulic HR press. Built by BossModels.Downsizer. Root = plinth base centre on the floor.
//  Kinematic parts the brain animates: Arm (yaw about the column, the stamp hangs ArmRadius m out along the arm's +Z),
//  Stamp (height via SetStampBottom), Shutter (SetShutter 0 closed .. 1 open, slides into the plinth), Heart (glow).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class DownsizerModel : BossModelBase
    {
        public static readonly string[] PartNames = { "Plinth", "Column", "Arm", "Piston", "PistonRod", "Stamp", "Heart", "Shutter", "Sticker", "Counterweight" };
        public override string[] RequiredParts => PartNames;

        public Transform Plinth, Column, Arm, Piston, PistonRod, Stamp, Heart, Shutter, Sticker;
        public Renderer HeartLiquid;
        public Material HeartDim, HeartBright;
        public Light HeartLight;
        public BoxCollider PlinthCollider, StampCollider;

        /// <summary>Horizontal distance column axis -> stamp centre (LEVELS2: pads 6 m from the press).</summary>
        public float ArmRadius = 6f;
        /// <summary>Height of the arm pivot above the floor.</summary>
        public float PivotHeight = 6.6f;
        public float StampThickness = 0.5f;
        public float StampSize = 3.5f;
        /// <summary>Lowest point of the piston housing (the stamp top can't go above it).</summary>
        public float HousingBottom = 5.0f;
        /// <summary>Plinth top (heart sits on it at HeartCenter).</summary>
        public float PlinthHeight = 2f;
        public Vector3 HeartCenter = new Vector3(0f, 2.6f, 0f);
        public float ShutterDrop = 1.45f;

        public float StampBottom { get; private set; } = 4f;
        public float ArmYaw { get; private set; }
        public float ShutterOpen { get; private set; }
        public float MaxStampBottom => HousingBottom - StampThickness - 0.1f;

        /// <summary>Yaw of the arm in degrees, local to the model root (0 = stamp over +Z, 90 = over +X).</summary>
        public void SetArmYaw(float degrees)
        {
            ArmYaw = degrees;
            if (Arm != null) Arm.localRotation = Quaternion.Euler(0f, degrees, 0f);
        }

        /// <summary>Yaw that puts the stamp over a point given in the model root's local space.</summary>
        public float YawToward(Vector3 localPoint) => Mathf.Atan2(localPoint.x, localPoint.z) * Mathf.Rad2Deg;

        /// <summary>Stamp underside height above the floor (0 = slammed / REVIEW PLATFORM with its top at StampThickness).</summary>
        public void SetStampBottom(float bottom)
        {
            bottom = Mathf.Clamp(bottom, 0f, MaxStampBottom);
            StampBottom = bottom;
            if (Stamp != null) Stamp.localPosition = new Vector3(0f, bottom - PivotHeight, ArmRadius);
            if (PistonRod != null)
            {
                float top = HousingBottom - PivotHeight;              // local y (negative)
                float len = Mathf.Max(0.05f, HousingBottom - (bottom + StampThickness)) + 0.1f;
                PistonRod.localPosition = new Vector3(0f, top + 0.05f, ArmRadius);
                PistonRod.localScale = new Vector3(1f, len, 1f);
            }
        }

        /// <summary>World-space centre of the stamp underside.</summary>
        public Vector3 StampFootWorld => Stamp != null ? Stamp.position : transform.position;

        /// <summary>0 = heart covered, 1 = heart exposed (the shutter sleeve sinks into the plinth).</summary>
        public void SetShutter(float open01)
        {
            ShutterOpen = Mathf.Clamp01(open01);
            if (Shutter != null) Shutter.localPosition = new Vector3(0f, -ShutterDrop * ShutterOpen, 0f);
        }

        /// <summary>Brighter liquid (+ light) while the heart is a valid target.</summary>
        public void SetHeartExposed(bool exposed)
        {
            if (HeartLiquid != null && HeartDim != null && HeartBright != null) HeartLiquid.sharedMaterial = exposed ? HeartBright : HeartDim;
            if (HeartLight != null) HeartLight.intensity = exposed ? 2.4f : 1.1f;
        }
    }
}
