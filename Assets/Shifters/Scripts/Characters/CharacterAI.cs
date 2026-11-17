// =====================================================================================
//  SHIFTERS - CharacterAI.cs   [owned by the CORE agent]
//  Enabled on inactive characters. Behaviour = CharacterManager.AIMode:
//   Stay   - hold where you were left (never wander; essential for holding plates)
//   Follow - follow the active character along its breadcrumb trail; refuse unsafe jumps
//  Never teleports. Shows speech bubbles when stuck ("I can't jump that far!").
//
//  Follow details: walks the CharacterManager.Trail crumb by crumb, stops ~2.5 m from the
//  leader, jumps at the leader's jump markers only if the gap fits its OWN max jump
//  (estimated from Tuning speeds), hops up blocking steps <= 1.2 m, probes ahead and never
//  walks into a drop > 2.5 m on its own, gives up when > 25 m behind.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class CharacterAI : MonoBehaviour
    {
        protected ShifterCharacter character;
        protected virtual void Awake() { character = GetComponent<ShifterCharacter>(); }

        public const float FollowDistance = 2.5f;
        public const float GiveUpDistance = 25f;
        public const float SafeDrop = 2.5f;
        public const float MaxStepJump = 1.2f;
        const float ReachRadius = 0.7f;
        const float RefuseCooldown = 6f;

        /// <summary>Why the follower is currently waiting (for debugging/UI). Empty when moving or idle.</summary>
        public string WaitReason { get; private set; } = "";
        /// <summary>True when the follower refused a jump/drop and is waiting at an edge.</summary>
        public bool IsRefusing { get; private set; }

        int _trailVersion = -1;
        int _targetSerial = -1;
        int _jumpedSerial = -1;
        bool _gaveUp;
        float _nextRefuseSay = -999f, _nextStuckSay = -999f, _nextQuip, _lookTimer, _lookFor;
        float _stuckTimer, _jumpHold;
        Vector3 _lookDir;
        CharacterMotor.Command _cmd;

        protected virtual void OnEnable()
        {
            _trailVersion = -1;
            _targetSerial = -1;
            _gaveUp = false;
            _nextQuip = Time.time + Random.Range(20f, 40f);
            _lookTimer = Random.Range(1.5f, 4f);
        }

        protected virtual void OnDisable()
        {
            if (character != null && character.Motor != null && !character.ExternalControl) character.Motor.ClearCommand();
        }

        protected virtual void Update()
        {
            if (character == null || character.ExternalControl) return;
            var motor = character.Motor;
            if (motor == null) return;
            var mgr = CharacterManager.Instance;
            if (character.IsActive || !character.CanAct || mgr == null) { motor.ClearCommand(); IsRefusing = false; return; }

            var leader = mgr.Active;
            _cmd = default;
            bool follow = mgr.AIMode == TeamAIMode.Follow && leader != null && leader != character && leader.IsAlive && !mgr.IsTransitioning;
            if (follow) Follow(leader, mgr.Trail, motor);
            else { IsRefusing = false; WaitReason = ""; _trailVersion = -1; Stay(leader); }

            if (_jumpHold > 0f) { _jumpHold -= Time.deltaTime; _cmd.JumpHeld = true; }
            IdleChatter(leader);
            motor.SetCommand(_cmd);
        }

        // ---------------------------------------------------------------- Stay
        void Stay(ShifterCharacter leader)
        {
            if (leader == null || leader == character) return;
            _lookTimer -= Time.deltaTime;
            if (_lookTimer <= 0f)
            {
                _lookTimer = Random.Range(3f, 7f);
                Vector3 to = leader.Feet - character.Feet; to.y = 0f;
                if (to.sqrMagnitude < 14f * 14f && to.sqrMagnitude > 0.25f) { _lookDir = to.normalized; _lookFor = 1.2f; }
            }
            if (_lookFor > 0f) { _lookFor -= Time.deltaTime; _cmd.Face = _lookDir; }
        }

        // ---------------------------------------------------------------- Follow
        void Follow(ShifterCharacter leader, BreadcrumbTrail trail, CharacterMotor motor)
        {
            Vector3 pos = character.Feet;
            Vector3 toLeader = leader.Feet - pos;
            float dist = toLeader.magnitude;
            if (dist > GiveUpDistance)
            {
                if (!_gaveUp) { _gaveUp = true; character.Say(CharacterQuips.GiveUpLine()); }
                WaitReason = "too far behind";
                return;
            }
            if (_gaveUp && dist < GiveUpDistance - 5f) _gaveUp = false;
            if (_gaveUp) return;

            Vector3 flatToLeader = new Vector3(toLeader.x, 0f, toLeader.z);
            if (flatToLeader.magnitude < FollowDistance && Mathf.Abs(toLeader.y) < 1.2f)
            {
                IsRefusing = false; WaitReason = "";
                _targetSerial = Mathf.Max(_targetSerial, trail.NewestSerial);
                return;
            }

            if (trail.Version != _trailVersion || !trail.TryGet(_targetSerial, out _))
            {
                _trailVersion = trail.Version;
                _targetSerial = trail.FindNearest(pos, 6f);
                _jumpedSerial = -1;
            }

            Vector3 goal = leader.Feet;
            Breadcrumb crumb = default;
            bool haveCrumb = _targetSerial >= 0 && trail.TryGet(_targetSerial, out crumb);
            // advance past reached crumbs (stop at jump markers - handled below)
            for (int guard = 0; haveCrumb && guard < 8; guard++)
            {
                Vector3 d = crumb.Position - pos;
                float planar = new Vector2(d.x, d.z).magnitude;
                if (crumb.Jump && _jumpedSerial != crumb.Serial) break;
                if (planar < ReachRadius && d.y < 0.9f && d.y > -SafeDrop)
                {
                    if (!trail.TryGet(_targetSerial + 1, out var nextCrumb)) break;
                    _targetSerial++;
                    crumb = nextCrumb;
                }
                else break;
            }
            if (haveCrumb) goal = crumb.Position;
            else if (dist > 8f) { WaitReason = "no trail"; return; }

            Vector3 dir = goal - pos; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0025f) return;
            dir.Normalize();
            bool sprint = (haveCrumb && crumb.Sprint) || dist > 7f;

            // jump markers
            bool jumpHere = false;
            if (haveCrumb && crumb.Jump && _jumpedSerial != crumb.Serial)
            {
                Vector3 d = crumb.Position - pos;
                if (new Vector2(d.x, d.z).magnitude < 0.9f)
                {
                    if (!FindLanding(trail, crumb.Serial, out var landing)) { WaitReason = "waiting for leader to land"; return; }
                    Vector3 gap = landing.Position - crumb.Position;
                    float gapPlanar = new Vector2(gap.x, gap.z).magnitude;
                    float reach = motor.EstimateJumpDistance(gap.y, motor.SprintSpeed * Mathf.Max(0f, motor.SpeedMultiplier)) * 0.9f;
                    if (gapPlanar > reach) { Refuse(); return; }
                    jumpHere = true;
                    _jumpedSerial = crumb.Serial;
                    sprint = gapPlanar > motor.EstimateJumpDistance(gap.y, motor.WalkSpeed) * 0.85f;
                    // aim at the landing, keep momentum
                    var ld = landing.Position - pos; ld.y = 0f;
                    if (ld.sqrMagnitude > 0.01f) dir = ld.normalized;
                    if (trail.TryGet(crumb.Serial + 1, out _)) _targetSerial = crumb.Serial + 1;
                }
            }

            // never walk into a big drop unless we are taking the leader's jump
            if (!jumpHere && motor.IsGrounded && DropAhead(pos, dir))
            {
                bool leaderJumpedNear = haveCrumb && crumb.Jump;
                if (!leaderJumpedNear) { Refuse(); return; }
            }

            // blocked by a step: hop it if it is low enough
            if (!jumpHere && motor.IsGrounded)
            {
                float speed = motor.LocalPlanarVelocity.magnitude;
                _stuckTimer = speed < 0.4f ? _stuckTimer + Time.deltaTime : 0f;
                if (_stuckTimer > 0.25f)
                {
                    if (StepAhead(pos, dir, out float h) && h <= MaxStepJump) { jumpHere = true; _stuckTimer = 0f; }
                    else if (_stuckTimer > 2.5f && Time.time > _nextStuckSay) { _nextStuckSay = Time.time + RefuseCooldown; character.Say(CharacterQuips.StuckLine()); }
                }
            }

            IsRefusing = false;
            WaitReason = "";
            _cmd.Move = dir;
            _cmd.Sprint = sprint;
            if (jumpHere) { _cmd.Jump = true; _cmd.JumpHeld = true; _jumpHold = 0.45f; }
        }

        void Refuse()
        {
            IsRefusing = true;
            WaitReason = CharacterQuips.CantJump;
            if (Time.time >= _nextRefuseSay)
            {
                _nextRefuseSay = Time.time + RefuseCooldown;
                character.Say(CharacterQuips.CantJump);
            }
        }

        /// <summary>First grounded crumb after the jump marker (where the leader landed).</summary>
        static bool FindLanding(BreadcrumbTrail trail, int jumpSerial, out Breadcrumb landing)
        {
            for (int s = jumpSerial + 1; s <= trail.NewestSerial; s++)
            {
                if (!trail.TryGet(s, out var c)) break;
                if (c.Grounded && !c.Jump) { landing = c; return true; }
            }
            landing = default;
            return false;
        }

        bool DropAhead(Vector3 pos, Vector3 dir)
        {
            Vector3 probe = pos + dir * (character.Radius + 0.45f) + Vector3.up * 0.5f;
            if (!Physics.Raycast(probe, Vector3.down, out var hit, 0.5f + SafeDrop, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.attachedRigidbody == character.Body;
        }

        bool StepAhead(Vector3 pos, Vector3 dir, out float height)
        {
            height = 0f;
            float r = character.Radius;
            if (!Physics.Raycast(pos + Vector3.up * 0.4f, dir, out var wall, r + 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (wall.collider.attachedRigidbody == character.Body) return false;
            Vector3 top = wall.point + dir * 0.15f; top.y = pos.y + MaxStepJump + 0.15f;
            if (!Physics.Raycast(top, Vector3.down, out var hit, MaxStepJump + 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Angle(hit.normal, Vector3.up) > CharacterMotor.SlopeLimit) return false;
            height = hit.point.y - pos.y;
            return height > 0.05f;
        }

        // ---------------------------------------------------------------- chatter
        void IdleChatter(ShifterCharacter leader)
        {
            if (Time.time < _nextQuip) return;
            _nextQuip = Time.time + Random.Range(20f, 40f);
            if (leader == null || (leader.Feet - character.Feet).sqrMagnitude > 20f * 20f) return;
            if (character.Motor.LocalPlanarVelocity.sqrMagnitude > 0.5f) return;
            character.Say(CharacterQuips.IdleLine());
        }
    }
}
