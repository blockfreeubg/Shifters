using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Launch pad: anything landing on it is launched so its apex is LaunchHeight above the pad (v = sqrt(2 g h)),
    /// plus ForwardSpeed along local +Z. Characters via Motor.AddImpulse, props via velocity. Trigger on this GameObject.
    /// </summary>
    public class BouncePad : MonoBehaviour
    {
        public float LaunchHeight = 5f;
        public float ForwardSpeed = 0f;

        [Header("Parts")]
        public Transform Pad;

        public int Launches { get; private set; }
        public float LaunchSpeed => Mathf.Sqrt(2f * Tuning.Gravity * Mathf.Max(0.1f, LaunchHeight));

        readonly PuzzleCooldown _cool = new PuzzleCooldown(0.35f);
        float _squash;
        Vector3 _padScale = Vector3.one;
        bool _scaleInit;

        void OnTriggerEnter(Collider other) => TryLaunch(other);
        void OnTriggerStay(Collider other) => TryLaunch(other);

        void TryLaunch(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            float target = LaunchSpeed;
            var v = rb.linearVelocity;
            if (v.y > target * 0.5f) return;
            if (!_cool.Ready(rb)) return;
            var fwd = transform.forward * ForwardSpeed;
            if (rb.TryGetComponent(out ShifterCharacter c))
            {
                if (c.Condition == CharacterCondition.Carried) return;
                var dv = new Vector3(fwd.x, target - v.y, fwd.z);
                if (c.Motor != null) c.Motor.AddImpulse(dv, false, 0f);
                else rb.AddForce(dv, ForceMode.VelocityChange);
                Sfx.Voice(c, VoiceLine.Thrown);
            }
            else rb.linearVelocity = new Vector3(v.x + fwd.x, target, v.z + fwd.z);
            Launches++;
            _squash = 1f;
            Sfx.At(SfxId.Bounce, transform.position);
            Fx.Puff(transform.position + Vector3.up * 0.2f, 0.8f);
        }

        void Update()
        {
            if (Pad == null) return;
            if (!_scaleInit) { _padScale = Pad.localScale; _scaleInit = true; }
            if (_squash <= 0f) return;
            _squash = Mathf.Max(0f, _squash - Time.deltaTime * 4f);
            float s = Mathf.Sin(_squash * Mathf.PI * 2f) * _squash * 0.35f;
            Pad.localScale = new Vector3(_padScale.x * (1f - s * 0.4f), _padScale.y * (1f + s), _padScale.z * (1f - s * 0.4f));
        }
    }
}
