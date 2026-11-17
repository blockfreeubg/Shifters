// =====================================================================================
//  SHIFTERS - L8TipJar.cs   [L8 S0 - the SWAP-O-MATIC practice target]
//  Watches the TIP JAR; when it jumps more than 1.5 m in one frame right after a SWAP, toasts
//  "SWAP SUCCESSFUL. YOU ARE NOW A TIP." (rate-limited).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L8TipJar : MonoBehaviour
    {
        public Rigidbody Body;
        public string Line = "SWAP SUCCESSFUL. YOU ARE NOW A TIP.";
        public int Swaps { get; private set; }
        Vector3 _last;
        float _next;

        void Start() { if (Body != null) _last = Body.position; }

        void LateUpdate()
        {
            if (Body == null) return;
            Vector3 p = Body.position;
            if ((p - _last).sqrMagnitude > 2.25f && Time.frameCount - WeaponTools.LastSwapFrame <= 3)
            {
                Swaps++;
                if (Time.time >= _next) { _next = Time.time + 6f; GameEvents.RaiseToast(Line, UIColors.AccentYellow); }
            }
            _last = p;
        }
    }
}
