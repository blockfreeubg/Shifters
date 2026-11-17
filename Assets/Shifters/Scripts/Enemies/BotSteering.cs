// =====================================================================================
//  SHIFTERS - BotSteering.cs   [owned by the WEAPONS_ENEMIES agent]
//  Velocity steering for the unicycle-ish BONK-BOT: it can only drive where it faces, turns
//  with a limited rate (clumsy -> dodgeable), accelerates/brakes with limits, avoids obstacles
//  with 3 sphere casts and wriggles free when stuck. No NavMesh.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class BotSteering
    {
        public float Acceleration = 12f;     // m/s^2
        public float Braking = 18f;          // m/s^2
        public float TurnRate = 170f;        // deg/s (patrol); chase uses ChaseTurnRate
        public float ChaseTurnRate = 230f;
        public float AvoidRadius = 0.38f;
        public float AvoidHeight = 0.95f;
        public float AvoidAngle = 35f;

        /// <summary>Seconds without meaningful progress while trying to move.</summary>
        public float StuckTime { get; private set; }

        readonly Rigidbody _rb;
        readonly Transform _self;
        static readonly RaycastHit[] _hits = new RaycastHit[12];
        Vector3 _progressPos;
        float _progressTimer, _detourTimer, _detourSign = 1f;

        public BotSteering(Rigidbody rb, Transform self) { _rb = rb; _self = self; _progressPos = self.position; }

        /// <summary>Drive toward goal at speed; slows down to stop at stopDistance. fast = chase turn rate.</summary>
        public void MoveTo(Vector3 goal, float speed, float stopDistance, bool fast, float dt)
        {
            Vector3 pos = _rb.position;
            Vector3 to = goal - pos; to.y = 0f;
            float dist = to.magnitude;
            Vector3 fwd = Flat(_self.forward);
            Vector3 desired = dist > 0.05f ? to / dist : fwd;
            if (dist > stopDistance + 0.1f) desired = Avoid(desired, speed);
            if (_detourTimer > 0f) { _detourTimer -= dt; desired = Quaternion.Euler(0f, 75f * _detourSign, 0f) * desired; }

            Turn(desired, fast ? ChaseTurnRate : TurnRate, dt);
            fwd = Flat(_self.forward);
            float align = Vector3.Dot(fwd, desired);
            float arrive = Mathf.Clamp01((dist - stopDistance) / 1.2f);
            float target = speed * arrive * Mathf.Clamp01((align - 0.25f) / 0.75f);
            Drive(fwd * target, dt);
            TrackProgress(target > 0.6f, dt);
        }

        /// <summary>Brake to a stop while turning to face a direction.</summary>
        public void Hold(Vector3 faceDirection, float dt, float turnRate = 140f)
        {
            Vector3 f = Flat(faceDirection);
            if (f.sqrMagnitude > 1e-4f) Turn(f, turnRate, dt);
            Drive(Vector3.zero, dt);
            StuckTime = 0f;
            _progressPos = _rb.position;
        }

        /// <summary>Kill planar motion immediately (stun).</summary>
        public void Stop()
        {
            var v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(v.x * 0.2f, v.y, v.z * 0.2f);
        }

        void Turn(Vector3 desired, float rate, float dt)
        {
            if (desired.sqrMagnitude < 1e-4f) return;
            Quaternion want = Quaternion.LookRotation(desired, Vector3.up);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, want, rate * dt));
        }

        void Drive(Vector3 wantPlanar, float dt)
        {
            var v = _rb.linearVelocity;
            Vector3 planar = new Vector3(v.x, 0f, v.z);
            float rate = wantPlanar.sqrMagnitude >= planar.sqrMagnitude ? Acceleration : Braking;
            planar = Vector3.MoveTowards(planar, wantPlanar, rate * dt);
            _rb.linearVelocity = new Vector3(planar.x, v.y, planar.z);
        }

        void TrackProgress(bool tryingToMove, float dt)
        {
            _progressTimer += dt;
            if (_progressTimer < 0.75f) return;
            Vector3 delta = _rb.position - _progressPos; delta.y = 0f;
            float moved = delta.magnitude;
            _progressPos = _rb.position;
            _progressTimer = 0f;
            if (tryingToMove && moved < 0.3f)
            {
                StuckTime += 0.75f;
                if (StuckTime >= 1.5f && _detourTimer <= 0f) { _detourTimer = 1.1f; _detourSign = -_detourSign; }
            }
            else StuckTime = 0f;
        }

        Vector3 Avoid(Vector3 desired, float speed)
        {
            float len = 1.1f + speed * 0.35f;
            float dC = Cast(desired, len);
            if (dC < len)
            {
                float dL = Cast(Quaternion.Euler(0f, -AvoidAngle * 1.6f, 0f) * desired, len);
                float dR = Cast(Quaternion.Euler(0f, AvoidAngle * 1.6f, 0f) * desired, len);
                float sign = dL > dR ? -1f : 1f;
                return Quaternion.Euler(0f, sign * Mathf.Lerp(85f, 30f, dC / len), 0f) * desired;
            }
            float l = Cast(Quaternion.Euler(0f, -AvoidAngle, 0f) * desired, len * 0.8f);
            if (l < len * 0.8f) return Quaternion.Euler(0f, 28f * (1f - l / (len * 0.8f)), 0f) * desired;
            float r = Cast(Quaternion.Euler(0f, AvoidAngle, 0f) * desired, len * 0.8f);
            if (r < len * 0.8f) return Quaternion.Euler(0f, -28f * (1f - r / (len * 0.8f)), 0f) * desired;
            return desired;
        }

        /// <summary>Distance to the nearest blocking obstacle along dir (len if free). Ignores characters, light props, triggers, self.</summary>
        float Cast(Vector3 dir, float len)
        {
            Vector3 origin = _rb.position + Vector3.up * AvoidHeight;
            int n = Physics.SphereCastNonAlloc(origin, AvoidRadius, dir, _hits, len, ~0, QueryTriggerInteraction.Ignore);
            float best = len;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || h.distance <= 0f) continue;
                if (h.collider.transform.IsChildOf(_self)) continue;
                var arb = h.collider.attachedRigidbody;
                if (arb != null && !arb.isKinematic && arb.mass < 40f) continue;              // plow through small props
                if (h.collider.GetComponentInParent<ShifterCharacter>() != null) continue;   // characters are targets, not walls
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
    }
}
