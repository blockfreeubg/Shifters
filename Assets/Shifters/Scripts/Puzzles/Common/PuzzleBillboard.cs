using UnityEngine;

namespace Shifters
{
    /// <summary>Yaw-only (default) billboard toward the main camera. Used by floating puzzle displays.</summary>
    public class PuzzleBillboard : MonoBehaviour
    {
        public bool YawOnly = true;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var d = transform.position - cam.transform.position;
            if (YawOnly) d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
