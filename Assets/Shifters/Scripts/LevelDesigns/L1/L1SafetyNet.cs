// =====================================================================================
//  SHIFTERS - L1SafetyNet.cs   (LEVEL 1)
//  Invisible catch volume far below a section: in the tutorial nobody is ever lost to the void.
//  Characters entering it are returned to RespawnPoint with a poof (like a FoamPit, without the foam).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Trigger box (on this GameObject) that teleports characters back to <see cref="RespawnPoint"/>.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class L1SafetyNet : MonoBehaviour
    {
        public Vector3 RespawnPoint;
        public float RespawnYaw;
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
            GameEvents.RaiseToast("The safety net caught " + c.DisplayName + ". HR is mildly relieved.", UIColors.Info);
        }

        /// <summary>Builds a catch volume. center = box center (parent space), respawn = world point.</summary>
        public static L1SafetyNet Build(Transform parent, Vector3 center, Vector3 size, Vector3 respawn, float yaw = 0f)
        {
            var go = Kit.Make("L1SafetyNet", parent, center);
            go.layer = 2;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            var net = go.AddComponent<L1SafetyNet>();
            net.RespawnPoint = respawn;
            net.RespawnYaw = yaw;
            return net;
        }
    }
}
