// =====================================================================================
//  SHIFTERS - IStalkerVisual.cs   (VISUALS module, campaign wave)
//  Common visual interface of the stalker skins (DummyModel, CardboardMaxModel) and the
//  factory used by EnemyKit.Stalker: StalkerSkins.Build(skin, parent).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public interface IStalkerVisual
    {
        Transform Root { get; }
        bool Moving { get; }
        bool Lit { get; }
        /// <summary>Animate the creep (speed m/s; 0 = Tuning.StalkerSpeed).</summary>
        void SetMoving(bool moving, float speed = 0f);
        /// <summary>Lit = freezes on the spot (they only move unlit).</summary>
        void SetLit(bool lit);
        void SetFrozen(bool frozen);
    }

    public static class StalkerSkins
    {
        /// <summary>Visual of a stalker: TeamBuildingDummy (SPOOKY: MILD sticker when mild) or CardboardMax.
        /// Built under parent at local origin, facing parent +Z. Visual only (no colliders).</summary>
        public static IStalkerVisual Build(StalkerSkin skin, Transform parent, bool mild = false)
        {
            if (skin == StalkerSkin.CardboardMax) return CardboardMaxModel.Create(parent, Vector3.zero);
            return DummyModel.Create(parent, Vector3.zero, null, mild);
        }

        /// <summary>Prop for ScareKit.JumpScare poppers (CardboardMax, GhostSheet, Dummy, Mannequin). null for kinds
        /// that are not visuals' (FilingCabinet, MirrorFace: story_kit builds those).</summary>
        public static GameObject BuildScareProp(ScareKind kind, Transform parent, Vector3 localPos, Quaternion? rot = null, bool mild = false)
        {
            switch (kind)
            {
                case ScareKind.CardboardMax: return CardboardMaxModel.Create(parent, localPos, rot).gameObject;
                case ScareKind.GhostSheet: return GhostSheet.Create(parent, localPos, rot, "BOO?").gameObject;
                case ScareKind.Dummy: { var d = DummyModel.Create(parent, localPos, rot, mild); d.SetPose(DummyPose.Reach, 0f); return d.gameObject; }
                case ScareKind.Mannequin: { var d = DummyModel.Create(parent, localPos, rot, mild); d.SetPose(DummyPose.Slump, 0f); return d.gameObject; }
                default: return null;
            }
        }
    }
}
