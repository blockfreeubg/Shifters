// =====================================================================================
//  SHIFTERS - LevelSafetyNet.cs   [perspective_level kit, promoted by SEAMS from L1SafetyNet]
//  Invisible catch volume far below a section: characters entering it are returned to RespawnPoint
//  with a poof (like a FoamPit, without the foam). Copy of L1SafetyNet (the original stays for L1's
//  scene GUIDs). Build with LevelKit.SafetyNet.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Trigger box (on this GameObject) that teleports characters back to <see cref="RespawnPoint"/> (world).</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LevelSafetyNet : MonoBehaviour
    {
        public Vector3 RespawnPoint;
        public float RespawnYaw;
        [Tooltip("Toast on a catch ({0} = name). Empty = silent.")] public string CatchToast = "The safety net caught {0}. HR is mildly relieved.";
        public int Catches { get; private set; }

        readonly Dictionary<ShifterCharacter, float> _last = new Dictionary<ShifterCharacter, float>();

        void OnTriggerEnter(Collider other) => Handle(other);
        void OnTriggerStay(Collider other) => Handle(other);

        void Handle(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || !rb.TryGetComponent(out ShifterCharacter c)) return;
            if (c.State == CharacterState.Lost || c.State == CharacterState.Captured || c.Condition == CharacterCondition.Carried) return;
            if (_last.TryGetValue(c, out float t) && Time.time - t < 0.75f) return;
            _last[c] = Time.time;
            Catches++;
            var rot = Quaternion.Euler(0f, RespawnYaw, 0f);
            if (c.Bridge != null && c.Bridge.IsBridging) c.Bridge.Collapse();
            if (c.Motor != null) c.Motor.Teleport(RespawnPoint, rot);
            else c.transform.SetPositionAndRotation(RespawnPoint, rot);
            Fx.Poof(RespawnPoint + Vector3.up, c.Color, 1.3f);
            Sfx.At(SfxId.Bounce, RespawnPoint);
            if (!string.IsNullOrEmpty(CatchToast)) GameEvents.RaiseToast(string.Format(CatchToast, c.DisplayName), UIColors.Info);
        }
    }
}
