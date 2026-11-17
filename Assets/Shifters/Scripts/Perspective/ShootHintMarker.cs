// =====================================================================================
//  SHIFTERS - ShootHintMarker.cs   [owned by the PERSPECTIVE agent]
//  TRIGGER VIEW crosshair marker floating at a shootable target: billboards to the camera,
//  slowly spins and breathes. Only visible to the gun holder (Revealable Weapon + ShowOnly).
//  Build with PerspectiveKit.ShootHint.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class ShootHintMarker : MonoBehaviour
    {
        public Transform Spinner;
        public Revealable Reveal;
        public float SpinSpeed = 45f;

        void Start() { if (Reveal == null) Reveal = GetComponent<Revealable>(); }

        void LateUpdate()
        {
            if (Reveal != null && !Reveal.IsRevealed && Reveal.RevealAmount <= 0f) return;
            var cam = ShiftersCamera.Instance != null && ShiftersCamera.Instance.Cam != null ? ShiftersCamera.Instance.Cam : Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            if (Spinner != null)
            {
                float t = Time.time;
                Spinner.localRotation = Quaternion.Euler(0f, 0f, t * SpinSpeed);
                float s = 1f + 0.08f * Mathf.Sin(t * 5f);
                Spinner.localScale = new Vector3(s, s, s);
            }
        }
    }
}
