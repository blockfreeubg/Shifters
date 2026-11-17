using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Slab on a pivot that tilts under off-center weight and springs back (spring-damper on the weight torque).
    /// Kinematic MoveRotation (much more stable with the character motor than a HingeJoint). Tilts around local X:
    /// weight toward local +Z tips that side down. Weight comes from a WeightSensor on the slab (stacks count).
    /// </summary>
    public class WobblyPlatform : MonoBehaviour, IResettable
    {
        public float MaxTilt = 22f;
        [Tooltip("Degrees of target tilt per kg*m of torque.")] public float TiltPerKgM = 0.18f;
        public float Stiffness = 40f;
        public float Damping = 7f;

        [Header("Parts")]
        public MoverBody Slab;
        public WeightSensor Sensor;

        public float Angle => _angle;
        float _angle, _vel, _lastWobble;

        void FixedUpdate()
        {
            if (Slab == null || Slab.IsFrozen) return;
            float dt = Time.fixedDeltaTime;
            float torque = 0f;
            if (Sensor != null)
            {
                var bodies = Sensor.Bodies;
                var fwd = transform.forward;
                var pivot = transform.position;
                for (int i = 0; i < bodies.Count; i++)
                {
                    var b = bodies[i];
                    if (b == null) continue;
                    torque += b.EffectiveMass * Vector3.Dot(b.transform.position - pivot, fwd);
                }
            }
            float target = Mathf.Clamp(torque * TiltPerKgM, -MaxTilt, MaxTilt);
            _vel += ((target - _angle) * Stiffness - _vel * Damping) * dt;
            _angle = Mathf.Clamp(_angle + _vel * dt, -MaxTilt, MaxTilt);
            if (Mathf.Abs(_vel) > 30f && Time.time - _lastWobble > 0.8f) { _lastWobble = Time.time; Sfx.At(SfxId.Wobble, transform.position, 0.7f); }
            Slab.RotateTo(transform.rotation * Quaternion.Euler(_angle, 0f, 0f));
            Slab.MoveTo(transform.position);
        }

        public void ResetState() { _angle = 0f; _vel = 0f; if (Slab != null) Slab.Snap(transform.position, transform.rotation); }
    }
}
