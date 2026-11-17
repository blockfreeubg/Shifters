// =====================================================================================
//  SHIFTERS - L5FollowBody.cs   [LEVEL 5]
//  Keeps a (non-rigidbody) root glued to a moving kinematic body every physics step, before the
//  hazards on it run (used to mount a SweeperArm on a MovingPlatform without nesting rigidbodies).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DefaultExecutionOrder(-200)]
    public class L5FollowBody : MonoBehaviour
    {
        public MoverBody Target;
        [Tooltip("Offset in the target's local space.")] public Vector3 LocalOffset;

        void FixedUpdate() => Snap();
        void LateUpdate() => Snap();

        void Snap()
        {
            if (Target == null) return;
            var t = Target.transform;
            var b = Target.Body;
            Vector3 p = b != null ? b.position : t.position;
            transform.position = p + t.rotation * LocalOffset;
        }
    }
}
