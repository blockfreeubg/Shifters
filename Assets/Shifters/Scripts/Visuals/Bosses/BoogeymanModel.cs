// =====================================================================================
//  SHIFTERS - BoogeymanModel.cs   [owned by VISUALS_BOSSES]
//  THE BOOGEYMAN (CH11): a 6 m shadow silhouette (unlit near-black) with two DOOR-SIZED glowing eyes (0.95 x 1.9 m).
//  Built by BossModels.Boogeyman. Root on the floor, face +Z. No colliders (it's a projection of the SCARE-O-MATIC).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class BoogeymanModel : BossModelBase
    {
        public static readonly string[] PartNames = { "Body", "Head", "ArmL", "ArmR", "EyeL", "EyeR", "Grin" };
        public override string[] RequiredParts => PartNames;

        public Transform Body, Head, ArmL, ArmR, EyeL, EyeR, Grin;
        public Renderer[] EyeRenderers = new Renderer[0];
        public Material EyeCalm, EyeAngry;
        public float Height = 6f;
        public float EyesOpen { get; private set; } = 1f;

        /// <summary>0 = closed (dark), 1 = wide open (the spook tell opens them 1.5 s before a spook).</summary>
        public void SetEyesOpen(float open01)
        {
            EyesOpen = Mathf.Clamp01(open01);
            float y = Mathf.Max(0.02f, EyesOpen);
            if (EyeL != null) { EyeL.localScale = new Vector3(1f, y, 1f); EyeL.gameObject.SetActive(EyesOpen > 0.01f); }
            if (EyeR != null) { EyeR.localScale = new Vector3(1f, y, 1f); EyeR.gameObject.SetActive(EyesOpen > 0.01f); }
        }

        /// <summary>Angry (red-orange) eyes for the BOO ring / spook.</summary>
        public void SetAngry(bool angry)
        {
            for (int i = 0; i < EyeRenderers.Length; i++) if (EyeRenderers[i] != null) EyeRenderers[i].sharedMaterial = angry ? EyeAngry : EyeCalm;
            if (Grin != null) Grin.gameObject.SetActive(angry);
        }

        /// <summary>Reach pose: both arms raised forward by t (0..1); sway = idle lean in degrees.</summary>
        public void SetPose(float reach01, float swayDeg = 0f)
        {
            float a = Mathf.Lerp(0f, -70f, Mathf.Clamp01(reach01));
            if (ArmL != null) ArmL.localRotation = Quaternion.Euler(a, 0f, -12f + swayDeg * 0.5f);
            if (ArmR != null) ArmR.localRotation = Quaternion.Euler(a, 0f, 12f + swayDeg * 0.5f);
            if (Body != null) Body.localRotation = Quaternion.Euler(0f, 0f, swayDeg);
        }
    }
}
