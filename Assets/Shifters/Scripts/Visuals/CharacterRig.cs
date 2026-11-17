// =====================================================================================
//  SHIFTERS - CharacterRig.cs   (VISUALS module)
//  The built character hierarchy (pivots + rest data + dimensions). Produced by
//  CharacterModelBuilder, driven by CharacterAnimator (body) and FaceController (face).
//
//  Hierarchy (all pivots have IDENTITY rest rotation; +Z forward, +Y up, feet at the origin):
//   Model
//    Hips (hip-joint height)          -> Pelvis, LegL/LegR -> KneeL/R -> FootL/R -> Shoe
//     Spine (waist)                   -> TorsoMesh, Coat (Brainiac), BowTie
//      ShoulderL/R -> ElbowL/R -> HandL/R (HandR -> WeaponSocket, +Z = barrel)
//      Head (neck)                    -> HeadMesh, Face (eyes/brows/mouth/nose/ears), Hair, Accessory
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Body measurements of a built character (meters, model space unless noted).</summary>
    public struct RigDims
    {
        public float Height, HipY, ThighLen, ShinLen, AnkleY, LegR, HipX;
        public Vector3 Foot;                  // shoe size (w, h, len)
        public float WaistY;                  // spine pivot (model y)
        public float TorsoH, ChestW, WaistW, TorsoD;
        public float ShoulderY, ShoulderX;    // relative to the spine pivot
        public float UpperLen, ForeLen, ArmR, ForeR0, ForeR1, HandR, ArmSplay;
        public float NeckY;                   // head pivot, relative to the spine pivot
        public float HeadZ;                   // head pivot forward offset (Brute's hunched gorilla neck)
        public float HeadCenterY;             // head center, relative to the head pivot
        public Vector3 HeadHalf;              // head half extents
        public float EyeR;
        public float HairTop;                 // top of hair, relative to the head pivot
        public float ArmReach => UpperLen + ForeLen + HandR * 1.7f;
        public float LegLen => ThighLen + ShinLen;
    }

    public class CharacterRig
    {
        public CharacterProfile Profile;
        public RigDims D;
        public Transform Root, Hips, Spine, Head;
        public Transform ShoulderL, ShoulderR, ElbowL, ElbowR, HandL, HandR, WeaponSocket;
        public Transform LegL, LegR, KneeL, KneeR, FootL, FootR;
        public Transform TorsoMesh, HeadMesh;
        public Transform Propeller, AntennaPivot;   // optional (accessories)
        public Vector3 HipsRest, ShoulderLRest, ShoulderRRest, TorsoMeshScale = Vector3.one;
        public FaceController Face;
        public CharacterMaterials Mats;
        public Renderer[] Renderers = new Renderer[0];
        public int Triangles;
        /// <summary>(campaign) Solid gold statue look is on (CharacterGold.Apply / CharacterPuppet.SetGold).</summary>
        public bool IsGold { get; internal set; }
        internal Material[] SavedMats;
        internal UnityEngine.Rendering.ShadowCastingMode[] SavedShadows;

        /// <summary>World-space center of the head mesh.</summary>
        public Vector3 HeadCenterWorld => Head != null ? Head.TransformPoint(new Vector3(0f, D.HeadCenterY, 0f)) : Vector3.zero;

        public void SetVisible(bool on)
        {
            for (int i = 0; i < Renderers.Length; i++) if (Renderers[i] != null) Renderers[i].enabled = on;
            Face?.RefreshRendererState();
        }

        public void Dispose()
        {
            Mats?.Destroy();
            if (Root != null) Kit.SafeDestroy(Root.gameObject);
            Root = null;
        }
    }
}
