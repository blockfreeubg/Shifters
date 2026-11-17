// =====================================================================================
//  SHIFTERS - VisualsPreviewDriver.cs   (VISUALS tests only)
//  Fakes motor values on a CharacterVisual (the stub motor never moves) so every
//  locomotion state can be screenshotted.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Test-only: drives a CharacterVisual with fake motor values so every animation state can be previewed.</summary>
    public class VisualsPreviewDriver : MonoBehaviour
    {
        public enum Mode { Idle, Walk, Run, Sprint, JumpRise, Fall, Tumble }
        public Mode Kind;
        public float Speed;
        public Vector3 Spin = new Vector3(5f, 0f, 3f);
        CharacterVisual _v;
        float _t;

        public static VisualsPreviewDriver Attach(CharacterVisual v, Mode kind, float speed = 0f)
        {
            var d = v.gameObject.AddComponent<VisualsPreviewDriver>();
            d._v = v; d.Kind = kind; d.Speed = speed;
            return d;
        }

        void Update()
        {
            if (_v == null) return;
            _t += Time.deltaTime;
            var f = transform.forward;
            var m = new CharacterVisual.VisualMotion { Grounded = true };
            switch (Kind)
            {
                case Mode.Walk: m.Velocity = f * (Speed > 0f ? Speed : 2.2f); break;
                case Mode.Run: m.Velocity = f * (Speed > 0f ? Speed : 5.5f); break;
                case Mode.Sprint: m.Velocity = f * (Speed > 0f ? Speed : 11f); m.Sprinting = true; break;
                case Mode.JumpRise: m.Grounded = false; m.Velocity = f * 2f + Vector3.up * 6f; m.AirTime = 0.15f; break;
                case Mode.Fall: m.Grounded = false; m.Velocity = Vector3.down * 11f; m.AirTime = 0.9f + _t; break;
                case Mode.Tumble:
                    m.Grounded = false; m.Tumbling = true; m.AirTime = 0.5f; m.AngularVelocity = Spin;
                    transform.Rotate(Spin * Mathf.Rad2Deg * Time.deltaTime * 0.35f, Space.Self);
                    break;
            }
            _v.MotionOverride = m;
        }
    }
}
