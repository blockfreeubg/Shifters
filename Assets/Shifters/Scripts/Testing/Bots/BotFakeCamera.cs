// =====================================================================================
//  SHIFTERS - BotFakeCamera.cs   [BOTS module - test double for WaitCameraClear]
//  Same probe shape as enemies' SecurityCamera.CouldSee(Vector3, float): "sees" everything within Range until
//  ClearAt (BotDriver clock), then nothing. Test-only; never placed by a level.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class BotFakeCamera : MonoBehaviour
    {
        public float ClearAt = 2f;
        public float Range = 12f;

        public bool CouldSee(Vector3 p, float horizon)
        {
            var d = BotDriver.Ensure();
            return d.Now + 0f < ClearAt && (p - transform.position).magnitude <= Range;
        }
    }
}
