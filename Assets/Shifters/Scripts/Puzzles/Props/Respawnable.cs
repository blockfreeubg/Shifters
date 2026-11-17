using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Anti-softlock for props: when it falls below Tuning.LethalFallY, enters a KillZone, or ends up farther than
    /// MaxDistance from home, it poofs away and reappears at its home pose after RespawnDelay with zero velocity.
    /// Home = pose at Awake (scene placement); call SetHome() to move it.
    /// </summary>
    [DisallowMultipleComponent]
    public class Respawnable : MonoBehaviour, IResettable
    {
        public float RespawnDelay = 1f;
        public float MaxDistance = 150f;
        public Color PoofColor = new Color(1f, 0.85f, 0.6f);

        public Vector3 HomePosition { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool IsRespawning => _timer > 0f;
        public int RespawnCount { get; private set; }

        Renderer[] _renderers;
        Collider[] _colliders;
        Grabbable _grab;
        float _timer = -1f, _check;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _grab = GetComponent<Grabbable>();
            SetHome();
        }

        public void SetHome()
        {
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
        }

        void Update()
        {
            if (_timer > 0f)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f) Finish();
                return;
            }
            _check -= Time.deltaTime;
            if (_check > 0f) return;
            _check = 0.25f;
            if (_grab != null && _grab.IsHeld) return;
            var p = transform.position;
            if (p.y < Tuning.LethalFallY || (p - HomePosition).sqrMagnitude > MaxDistance * MaxDistance) Respawn();
        }

        /// <summary>Poof away now and come back home after RespawnDelay. Ignored while held or already respawning.</summary>
        public void Respawn()
        {
            if (IsRespawning || (_grab != null && _grab.IsHeld)) return;
            Fx.Poof(transform.position, PoofColor, 1f);
            if (_renderers == null) { _renderers = GetComponentsInChildren<Renderer>(); _colliders = GetComponentsInChildren<Collider>(); }
            SetVisible(false);
            if (Body != null)
            {
                if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
                Body.isKinematic = true;
            }
            _timer = Mathf.Max(0.05f, RespawnDelay);
        }

        void Finish()
        {
            _timer = -1f;
            RespawnCount++;
            transform.SetPositionAndRotation(HomePosition, HomeRotation);
            if (Body != null)
            {
                Body.position = HomePosition;
                Body.rotation = HomeRotation;
                Body.isKinematic = false;
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            SetVisible(true);
            Fx.Poof(HomePosition + Vector3.up * 0.5f, PoofColor, 1.2f);
            Sfx.At(SfxId.UIPop, HomePosition, 0.8f);
        }

        void SetVisible(bool v)
        {
            if (_renderers != null) foreach (var r in _renderers) if (r != null) r.enabled = v;
            if (_colliders != null) foreach (var c in _colliders) if (c != null) c.enabled = v;
        }

        public void ResetState()
        {
            if (Body != null && !Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            if (Body != null) Body.isKinematic = true;
            _timer = 0.0001f;
            if (_renderers == null) { _renderers = GetComponentsInChildren<Renderer>(); _colliders = GetComponentsInChildren<Collider>(); }
        }
    }
}
