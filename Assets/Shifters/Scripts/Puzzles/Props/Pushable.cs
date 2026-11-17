using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Heavy pushable object (160 kg block). At rest it is locked in place (planar position + rotation frozen; it can
    /// still fall). A character with STR &gt;= RequiredStrength touching it while its motor command points into it
    /// unlocks motion along the dominant push axis (the block's local X or Z) at Tuning.PushSpeed(STR).
    /// Weak pushers strain, and get a rate-limited "TOO HEAVY" toast. Scraping sound while moving.
    /// If it is pushed past a ledge (center unsupported) it slides off and falls.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Pushable : MonoBehaviour
    {
        public int RequiredStrength = Tuning.HeavyPushStrength;
        [Tooltip("Push intent threshold: dot(move command, direction to block).")] public float IntentDot = 0.55f;
        public string DisplayName = "heavy block";

        public Rigidbody Body { get; private set; }
        public bool IsBeingPushed => Time.time - _lastPush < 0.15f;
        public ShifterCharacter Pusher { get; private set; }
        public float DistancePushed { get; private set; }

        Vector3 _pushDir;
        float _pushSpeed, _lastPush = -10f, _lastToast = -10f, _lastStrain = -10f, _scrape;
        bool _wasPushed;
        Vector3 _lastPos;
        float _halfHeight = 0.8f;
        BoxCollider _box;

        /// <summary>Highest step the block climbs while pushed (pressure-plate pads are ~0.14 m, curbs 0.25 m).</summary>
        public const float MaxStepUp = 0.3f;

        const RigidbodyConstraints Locked = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.constraints = Locked;
            _lastPos = transform.position;
            var bc = GetComponent<BoxCollider>();
            _box = bc;
            _halfHeight = bc != null ? bc.size.y * 0.5f * transform.lossyScale.y : 0.8f;
        }

        /// <summary>Can this character push it?</summary>
        public bool CanPush(ShifterCharacter c) => c != null && c.Stats != null && c.Stats.Strength >= RequiredStrength;

        void OnCollisionStay(Collision collision)
        {
            var rb = collision.rigidbody;
            if (rb == null || !rb.TryGetComponent(out ShifterCharacter c)) return;
            if (!c.CanAct || c.Motor == null) return;
            var move = c.Motor.CurrentCommand.Move;
            move.y = 0f;
            if (move.sqrMagnitude < 0.09f) return;
            var to = transform.position - c.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;
            var mdir = move.normalized;
            if (Vector3.Dot(mdir, to.normalized) < IntentDot) return;

            if (!CanPush(c))
            {
                if (Time.time - _lastStrain > 0.6f)
                {
                    _lastStrain = Time.time;
                    PuzzleUtil.Gesture(c, VisualAction.Strain);
                    PuzzleUtil.Express(c, Expression.Strain, 0.7f);
                }
                if (PuzzleUtil.RateLimit(ref _lastToast, 3f))
                {
                    Sfx.Voice(c, VoiceLine.Strain);
                    Fx.FloatingText(transform.position + Vector3.up * 1.4f, "TOO HEAVY!", UIColors.Accent);
                    GameEvents.RaiseToast("TOO HEAVY! Needs STR " + RequiredStrength + " - " + c.DisplayName + " has " + c.Stats.Strength, UIColors.Accent);
                }
                return;
            }

            // dominant local axis
            var fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            var right = transform.right; right.y = 0f; right.Normalize();
            float df = Vector3.Dot(mdir, fwd), dr = Vector3.Dot(mdir, right);
            _pushDir = Mathf.Abs(df) >= Mathf.Abs(dr) ? fwd * Mathf.Sign(df) : right * Mathf.Sign(dr);
            _pushSpeed = Tuning.PushSpeed(c.Stats.Strength) * Mathf.Clamp01(move.magnitude);
            _lastPush = Time.time;
            Pusher = c;
        }

        void FixedUpdate()
        {
            bool pushed = IsBeingPushed;
            bool supported = CenterSupported();
            if (pushed || !supported)
            {
                Body.constraints = RigidbodyConstraints.FreezeRotation;
                var v = Body.linearVelocity;
                Vector3 planar = pushed ? _pushDir * _pushSpeed : new Vector3(v.x, 0f, v.z);
                Body.linearVelocity = new Vector3(planar.x, Mathf.Min(v.y, 0.5f), planar.z);
                if (pushed) TryStepUp();
                if (pushed && !_wasPushed) PuzzleUtil.Express(Pusher, Expression.Determined, 1f);
            }
            else
            {
                if (!Body.isKinematic)
                {
                    var v = Body.linearVelocity;
                    Body.linearVelocity = new Vector3(0f, v.y, 0f);
                }
                Body.constraints = Locked;
                Pusher = null;
            }
            _wasPushed = pushed;
            var p = transform.position;
            var d = p - _lastPos; d.y = 0f;
            if (pushed) DistancePushed += d.magnitude;
            _lastPos = p;
        }

        void Update()
        {
            if (!IsBeingPushed) return;
            _scrape -= Time.deltaTime;
            if (_scrape <= 0f)
            {
                _scrape = 0.22f;
                Sfx.At(SfxId.Crumble, transform.position, 0.35f, Random.Range(0.55f, 0.7f));
                Fx.Puff(transform.position - _pushDir * 0.6f + Vector3.down * 0.7f, 0.35f);
            }
        }

        /// <summary>[integrator] While pushed, climb a low step in the push direction (e.g. a pressure-plate pad): the
        /// bottom of the leading face is blocked, the space MaxStepUp higher is free, and the step top is found below ->
        /// lift the block onto it. Without this the block stopped dead at every plate edge ("push 160 kg onto 150 kg").</summary>
        void TryStepUp()
        {
            if (Body.isKinematic || _box == null) return;
            Vector3 c = Body.position;
            Vector3 ext = Vector3.Scale(_box.size * 0.5f, transform.lossyScale);
            float halfDepth = Mathf.Abs(Vector3.Dot(transform.right * ext.x, _pushDir)) + Mathf.Abs(Vector3.Dot(transform.forward * ext.z, _pushDir));
            float bottom = c.y - _halfHeight;
            float reach = halfDepth + 0.15f;
            Vector3 low = new Vector3(c.x, bottom + 0.04f, c.z);
            if (!Physics.Raycast(low, _pushDir, out var hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (hit.rigidbody == Body || (hit.rigidbody != null && !hit.rigidbody.isKinematic)) return; // only static/kinematic steps
            if (hit.distance > halfDepth + 0.06f) return; // act only when touching: lifted early it would land level with the step corner
            if (Physics.Raycast(low + Vector3.up * MaxStepUp, _pushDir, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return; // a wall
            Vector3 probe = new Vector3(hit.point.x, bottom + MaxStepUp + 0.05f, hit.point.z) + _pushDir * 0.08f;
            if (!Physics.Raycast(probe, Vector3.down, out var top, MaxStepUp + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            float step = top.point.y - bottom;
            if (step < 0.02f || step > MaxStepUp) return;
            // clear the step edge with margin: the leading bottom edge must land ON the step top, not against its corner
            Body.position = c + Vector3.up * (step + 0.04f) + _pushDir * 0.1f;
        }

        bool CenterSupported()
        {
            if (Body.isKinematic) return true;
            int n = Physics.RaycastNonAlloc(transform.position, Vector3.down, _rayHits, _halfHeight + 0.25f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (_rayHits[i].rigidbody != Body) return true;
            return false;
        }

        static readonly RaycastHit[] _rayHits = new RaycastHit[8];
    }
}
