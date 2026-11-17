using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Cracked slab: ShakeTime seconds after a character touches it, it falls (becomes dynamic) and RespawnTime
    /// seconds later pops back at home. Speed perspective shows the timers. Collider + kinematic body on this object.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CrumblingPlatform : MonoBehaviour, IResettable, IFreezable
    {
        public enum CrumbleState { Idle, Shaking, Falling }

        public float ShakeTime = 0.6f;
        public float RespawnTime = 4f;

        [Header("Parts")]
        public Transform Visual;
        public Revealable Reveal;

        public CrumbleState State { get; private set; }
        public int Falls { get; private set; }

        Rigidbody _rb;
        Collider _col;
        Vector3 _homePos, _visualHome;
        Quaternion _homeRot;
        float _t, _crumbleSfx;
        bool _touched;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
            _rb.isKinematic = true;
            _rb.useGravity = true;
            _homePos = transform.position;
            _homeRot = transform.rotation;
            if (Visual != null) _visualHome = Visual.localPosition;
        }

        void Start()
        {
            if (Reveal != null) Reveal.DynamicLabel = v =>
                State == CrumbleState.Idle ? "CRUMBLES " + ShakeTime.ToString("0.0") + " s AFTER YOU STEP"
                : State == CrumbleState.Shaking ? "FALLING IN " + Mathf.Max(0f, ShakeTime - _t).ToString("0.0") + " s"
                : "BACK IN " + Mathf.Max(0f, RespawnTime - _t).ToString("0.0") + " s";
        }

        void OnCollisionStay(Collision c)
        {
            if (State != CrumbleState.Idle || c.rigidbody == null || !c.rigidbody.TryGetComponent(out ShifterCharacter _)) return;
            _touched = true;
        }

        // ------------------------------------------------------------------ IFreezable (CAPS-LOCK: crumbling stops)
        float _frozenUntil = -1f;
        public bool IsFrozen => Time.time < _frozenUntil;
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;
            if (!IsFrozen) { Fx.FloatingText(transform.position + Vector3.up * 1f, "CAPS LOCK", UIColors.Info, 1.1f); Sfx.At(SfxId.CapsLock, transform.position); }
            _frozenUntil = Mathf.Max(_frozenUntil, Time.time + seconds);
        }

        void FixedUpdate()
        {
            if (IsFrozen) return;
            if (State != CrumbleState.Idle) return;
            if (!_touched)
            {
                var all = ShifterCharacter.All;
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && all[i].Motor != null && all[i].Motor.IsGrounded && all[i].Motor.GroundCollider == _col) { _touched = true; break; }
            }
            if (_touched) BeginShake();
        }

        void BeginShake()
        {
            _touched = false;
            State = CrumbleState.Shaking;
            _t = 0f;
            Sfx.At(SfxId.Crumble, transform.position, 0.8f, 1.2f);
            Fx.Puff(transform.position, 0.8f, MaterialLibrary.DefaultColor(MatKey.Concrete));
        }

        void Update()
        {
            if (State == CrumbleState.Idle || IsFrozen) return;
            _t += Time.deltaTime;
            if (State == CrumbleState.Shaking)
            {
                float a = 0.03f + 0.05f * (_t / ShakeTime);
                if (Visual != null) Visual.localPosition = _visualHome + new Vector3(Random.Range(-a, a), Random.Range(-a, a) * 0.5f, Random.Range(-a, a));
                _crumbleSfx -= Time.deltaTime;
                if (_crumbleSfx <= 0f) { _crumbleSfx = 0.2f; Sfx.At(SfxId.Crumble, transform.position, 0.4f, Random.Range(0.9f, 1.3f)); }
                if (_t >= ShakeTime) Fall();
            }
            else if (_t >= RespawnTime) Restore();
        }

        void Fall()
        {
            State = CrumbleState.Falling;
            Falls++;
            _t = 0f;
            if (Visual != null) Visual.localPosition = _visualHome;
            _rb.isKinematic = false;
            _rb.AddTorque(new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 2f, ForceMode.VelocityChange);
            Sfx.At(SfxId.Crumble, transform.position, 1f, 0.7f);
            Fx.Puff(transform.position, 1.4f, MaterialLibrary.DefaultColor(MatKey.Concrete));
        }

        void Restore()
        {
            State = CrumbleState.Idle;
            _t = 0f;
            if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            _rb.isKinematic = true;
            _rb.position = _homePos;
            _rb.rotation = _homeRot;
            transform.SetPositionAndRotation(_homePos, _homeRot);
            Fx.Poof(_homePos + Vector3.up * 0.3f, MaterialLibrary.DefaultColor(MatKey.Concrete), 1.2f);
            Sfx.At(SfxId.UIPop, _homePos, 0.7f);
        }

        public void ResetState() { if (State != CrumbleState.Idle) Restore(); }
    }
}
