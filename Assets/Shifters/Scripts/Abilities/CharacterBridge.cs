// =====================================================================================
//  SHIFTERS - CharacterBridge.cs   [CONTRACT API - implementation owned by the ABILITIES agent]
//  B: become a living bridge plank (Tuning.BridgeLength) across a gap in the facing direction.
//  Persists while you switch away. Capacity Tuning.BridgeCapacityKg(STR); overloaded for
//  BridgeCollapseDelay -> collapse (comedy). B again (when active) recovers: the character snaps
//  to the FAR end (where its hands were) if there's ground there, otherwise the near end.
//
//  Physics while bridging (Condition.Bridging, this component owns the body):
//    * root placed at the feet (exact floor height from a ground ray), yaw = facing snapped to the
//      nearest 90 deg when within 12 deg, else to 15 deg; Rigidbody kinematic;
//    * CapsuleCollider + HeadPlatform disabled;
//    * child "BridgePlank" BoxCollider (non-trigger) centre (0, -t/2, L/2), size (W, t, L): TOP = feet
//      height, part of the character's (kinematic) compound body -> motors treat it as ground;
//    * a WeightSensor on the plank measures the load (CurrentLoad).
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public class CharacterBridge : MonoBehaviour
    {
        public ShifterCharacter Character { get; protected set; }
        public bool IsBridging { get; protected set; }
        public float Capacity => Character != null && Character.Stats != null ? Tuning.BridgeCapacityKg(Character.Stats.Strength) : 150f;
        public float CurrentLoad { get; protected set; }

        public const float FormTime = 0.5f;
        public const float RecoverTime = 0.35f;

        /// <summary>Plank collider (created on first use; disabled when not bridging).</summary>
        public BoxCollider Plank { get; protected set; }
        public WeightSensor LoadSensor { get; protected set; }
        /// <summary>True during the 0.5 s "TIMBER!" fall (plank not solid yet).</summary>
        public bool IsForming => _phase == Phase.Forming;
        public bool IsRecovering => _phase == Phase.Recovering;
        /// <summary>Fully formed and walkable.</summary>
        public bool IsSolid => _phase == Phase.Solid;
        public float Load01 => Capacity > 0f ? CurrentLoad / Capacity : 0f;
        public float OverloadTime { get; protected set; }
        /// <summary>Bridge start (feet / plank top at the near end).</summary>
        public Vector3 Origin => _origin;
        public Vector3 Direction => _rotation * Vector3.forward;
        public Vector3 FarEnd => _origin + Direction * Tuning.BridgeLength;

        public event Action<CharacterBridge> Formed;
        public event Action<CharacterBridge> Collapsed;
        public event Action<CharacterBridge> Recovered;

        enum Phase { None, Forming, Solid, Recovering }
        Phase _phase;
        float _t, _sag, _creakTimer;
        bool _warned;
        Vector3 _origin, _recoverFrom, _recoverTo;
        Quaternion _rotation = Quaternion.identity;
        bool _capsuleWasEnabled, _headWasEnabled;

        static readonly string[] FormLines = { "TIMBER!", "I am the bridge now.", "Walk on me. Gently.", "Bridge mode: ENGAGED", "Mind the gap. I AM the gap." };
        static readonly string[] OverLines = { "Too... many...!", "My SPINE!", "Who ate all the donuts?!", "Abort! ABORT!" };

        protected virtual void Awake() { Character = GetComponent<ShifterCharacter>(); }

        // ================================================================== contract
        public virtual bool CanForm(out string reason)
        {
            reason = null;
            var c = Character;
            if (c == null) { reason = "No body to bridge with."; return false; }
            if (!c.IsAlive) { reason = c.DisplayName + " is in no state to be a bridge."; return false; }
            if (_phase != Phase.None) { reason = "Already a bridge."; return false; }
            if (c.Condition != CharacterCondition.Normal) { reason = "Can't become a bridge right now."; return false; }
            if (c.Grabber != null && c.Grabber.IsHolding) { reason = "Put that down first."; return false; }
            if (c.Grabbable != null && c.Grabbable.IsHeld) { reason = "Can't bridge while being carried."; return false; }
            if (!IsGrounded()) { reason = "Need solid ground under your feet."; return false; }
            if (c.Motor != null && c.Motor.StandingOn != null) { reason = "Can't be a bridge on top of " + c.Motor.StandingOn.DisplayName + "."; return false; }
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o != null && o != c && o.Motor != null && o.Motor.StandingOn == c) { reason = o.DisplayName + " is standing on your head."; return false; }
            }
            return true;
        }

        public virtual bool TryForm()
        {
            if (!CanForm(out var reason))
            {
                AbilityFeedback.Denied(Character, "NOPE", reason);
                return false;
            }
            BeginForm();
            return true;
        }

        /// <summary>Bots/tests: face a direction (flattened) and form.</summary>
        public bool TryForm(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 1e-4f && Character != null && CanForm(out _))
            {
                var rot = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = rot;
                if (Character.Body != null) Character.Body.rotation = rot;
            }
            return TryForm();
        }

        /// <summary>Recover from bridge mode (snap to far end if possible).</summary>
        public virtual void Recover()
        {
            if (_phase != Phase.Solid && _phase != Phase.Forming) return;
            SetPlankSolid(false);
            _recoverFrom = transform.position;
            _recoverTo = FindFarStand(out var far) ? far : _origin;
            _phase = Phase.Recovering;
            _t = 0f;
            Sfx.At(SfxId.BridgeRecover, _origin + Direction * (Tuning.BridgeLength * 0.5f));
            if (Character != null && Character.Visual != null) Character.Visual.SetExpression(Expression.Happy, 1f);
        }

        /// <summary>Overload collapse (character falls, walkers drop).</summary>
        public virtual void Collapse()
        {
            if (_phase != Phase.Solid && _phase != Phase.Forming) return;
            var c = Character;
            Vector3 mid = _origin + Direction * (Tuning.BridgeLength * 0.5f);
            SetPlankSolid(false);
            RestoreBody();
            _phase = Phase.None;
            IsBridging = false;
            CurrentLoad = 0f;
            OverloadTime = 0f;
            if (c != null && c.IsAlive)
            {
                c.SetCondition(CharacterCondition.Stunned);
                if (c.Motor != null) c.Motor.BeginTumble(1.2f);
                if (c.Body != null && !c.Body.isKinematic) c.Body.linearVelocity = -Direction * 2.5f + Vector3.up * 5f;
                if (c.Visual != null) { c.Visual.SetPose(PoseOverride.None); c.Visual.SetExpression(Expression.Dizzy, 2.5f); }
                Fx.Stars(c.Visual != null && c.Visual.Head != null ? c.Visual.Head : c.transform, 2.5f);
                Sfx.Voice(c, VoiceLine.Scream);
            }
            for (int i = 0; i < 4; i++) Fx.Puff(_origin + Direction * (Tuning.BridgeLength * (0.15f + 0.25f * i)), 1.3f);
            Sfx.At(SfxId.BridgeCollapse, mid);
            GameEvents.RaiseBanner("BRIDGE COLLAPSE!", c != null ? c.DisplayName + " could not take the weight" : null, BannerStyle.Danger);
            TimeController.HitStop(0.3f, 0.25f);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.4f, 0.4f);
            Collapsed?.Invoke(this);
        }

        // ================================================================== queries
        /// <summary>Gap probe for the "[B] Become a bridge" hint: true when standing at a ledge facing a gap of
        /// 3 m .. (BridgeLength - 0.4) with ground on the far side at about the same height.</summary>
        public bool ProbeGap(out float gapLength)
        {
            gapLength = 0f;
            var c = Character;
            if (c == null) return false;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return false;
            fwd.Normalize();
            Vector3 feet = c.Feet;
            float r = c.Radius;
            if (GroundNear(feet + fwd * (r + 0.45f), feet.y)) return false; // still floor in front: not at a ledge
            for (float d = r + 1.0f; d <= Tuning.BridgeLength - 0.2f; d += 0.3f)
            {
                if (GroundNear(feet + fwd * d, feet.y))
                {
                    gapLength = d - r;
                    return gapLength >= 3f;
                }
            }
            gapLength = float.PositiveInfinity;
            return false;
        }

        bool GroundNear(Vector3 p, float refY)
        {
            if (!AbilityPhysics.GroundBelow(new Vector3(p.x, refY, p.z), 0.6f, 0.8f, out var hit, Character != null ? Character.Body : null)) return false;
            return Mathf.Abs(hit.point.y - refY) < 0.6f;
        }

        bool IsGrounded()
        {
            var c = Character;
            if (c.Motor != null && c.Motor.IsGrounded) return true;
            return AbilityPhysics.GroundBelow(c.Feet, 0.25f, 0.3f, out _, c.Body);
        }

        bool FindFarStand(out Vector3 pos)
        {
            pos = default;
            var c = Character;
            if (c == null) return false;
            Vector3 p = _origin + Direction * (Tuning.BridgeLength - 0.4f);
            if (!AbilityPhysics.GroundBelow(p, 1.2f, 2.5f, out var hit, c.Body)) return false;
            float r = c.Radius, h = c.Height;
            Vector3 a = hit.point + Vector3.up * (r + 0.06f), b = hit.point + Vector3.up * Mathf.Max(r + 0.07f, h - r);
            if (Physics.CheckCapsule(a, b, r * 0.9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            pos = hit.point;
            // step one body radius further onto the far ledge when there is room (don't stand on the very edge)
            Vector3 q = pos + Direction * (r + 0.1f);
            if (AbilityPhysics.GroundBelow(q, 0.6f, 0.6f, out var hit2, c.Body) && Mathf.Abs(hit2.point.y - pos.y) < 0.3f)
            {
                Vector3 a2 = hit2.point + Vector3.up * (r + 0.06f), b2 = hit2.point + Vector3.up * Mathf.Max(r + 0.07f, h - r);
                if (!Physics.CheckCapsule(a2, b2, r * 0.9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) pos = hit2.point;
            }
            return true;
        }

        // ================================================================== internals
        void BeginForm()
        {
            var c = Character;
            Vector3 feet = c.Feet;
            if (AbilityPhysics.GroundBelow(feet, 0.4f, 0.5f, out var g, c.Body)) feet.y = g.point.y;
            _origin = feet;
            _rotation = Quaternion.Euler(0f, SnapYaw(transform.eulerAngles.y), 0f);
            if (c.Grabber != null) c.Grabber.ForceRelease();
            if (!c.SetCondition(CharacterCondition.Bridging)) return;
            var body = c.Body;
            if (body != null)
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
            }
            _capsuleWasEnabled = c.Capsule != null && c.Capsule.enabled;
            _headWasEnabled = c.HeadPlatform != null && c.HeadPlatform.enabled;
            if (c.Capsule != null) c.Capsule.enabled = false;
            if (c.HeadPlatform != null) c.HeadPlatform.enabled = false;
            transform.SetPositionAndRotation(_origin, _rotation);
            if (body != null) { body.position = _origin; body.rotation = _rotation; }
            EnsurePlank();
            SetPlankSolid(false);
            IsBridging = true;
            _phase = Phase.Forming;
            _t = 0f;
            _sag = 0f;
            _warned = false;
            OverloadTime = 0f;
            CurrentLoad = 0f;
            if (c.Visual != null) { c.Visual.SetPose(PoseOverride.Bridge); c.Visual.SetBridgeStretch(c.Height, 0f); c.Visual.SetExpression(Expression.Determined, 1f); }
            Sfx.At(SfxId.BridgeForm, _origin);
            Sfx.Voice(c, VoiceLine.Strain);
            c.Say(FormLines[UnityEngine.Random.Range(0, FormLines.Length)]);
            Fx.Puff(_origin, 1f);
            var s = GameSession.Instance != null ? GameSession.Instance.Stats : null;
            if (s != null) s.Bridges++;
        }

        void FinishForm()
        {
            _phase = Phase.Solid;
            SetPlankSolid(true);
            Vector3 far = FarEnd;
            Fx.Puff(far, 1.2f);
            Sfx.At(SfxId.LandHeavy, far, 0.9f, 0.8f);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.15f, 0.2f);
            Formed?.Invoke(this);
        }

        void FinishRecover()
        {
            var c = Character;
            _phase = Phase.None;
            IsBridging = false;
            CurrentLoad = 0f;
            OverloadTime = 0f;
            Vector3 fwd = Direction;
            RestoreBody();
            if (c != null)
            {
                var rot = Quaternion.LookRotation(fwd, Vector3.up);
                if (c.Motor != null) { c.Motor.Teleport(_recoverTo, rot); c.Motor.StandUp(); }
                else { transform.SetPositionAndRotation(_recoverTo, rot); }
                c.SetCondition(CharacterCondition.Normal);
                if (c.Visual != null) { c.Visual.SetPose(PoseOverride.None); c.Visual.Play(VisualAction.FistPump); }
            }
            Fx.Puff(_recoverTo, 0.9f);
            Recovered?.Invoke(this);
        }

        /// <summary>Hard reset without effects (KO'd by a hazard, teleported, disabled, level restart).</summary>
        void Cleanup()
        {
            if (_phase == Phase.None && !IsBridging) return;
            SetPlankSolid(false);
            RestoreBody();
            _phase = Phase.None;
            IsBridging = false;
            CurrentLoad = 0f;
            OverloadTime = 0f;
            var c = Character;
            if (c != null)
            {
                if (c.Condition == CharacterCondition.Bridging) c.SetCondition(CharacterCondition.Normal);
                if (c.Visual != null && c.Visual.CurrentPose == PoseOverride.Bridge) c.Visual.SetPose(PoseOverride.None);
            }
        }

        void RestoreBody()
        {
            var c = Character;
            if (c == null) return;
            if (c.Capsule != null) c.Capsule.enabled = _capsuleWasEnabled || c.Capsule.enabled;
            if (c.HeadPlatform != null) c.HeadPlatform.enabled = _headWasEnabled || c.HeadPlatform.enabled;
            _capsuleWasEnabled = _headWasEnabled = false;
            if (c.Body != null && c.State != CharacterState.Lost && c.State != CharacterState.Captured) c.Body.isKinematic = false;
        }

        void EnsurePlank()
        {
            if (Plank != null) return;
            var go = new GameObject("BridgePlank");
            go.transform.SetParent(transform, false);
            float L = Tuning.BridgeLength, t = Tuning.BridgeThickness;
            Plank = go.AddComponent<BoxCollider>();
            Plank.center = new Vector3(0f, -t * 0.5f, L * 0.5f);
            Plank.size = new Vector3(Tuning.BridgeWidth, t, L);
            Plank.enabled = false;
            LoadSensor = go.AddComponent<WeightSensor>();
            LoadSensor.Surfaces = new Collider[] { Plank };
            LoadSensor.enabled = false;
        }

        void SetPlankSolid(bool on)
        {
            if (Plank != null)
            {
                Plank.transform.localPosition = Vector3.zero;
                Plank.transform.localRotation = Quaternion.identity;
                Plank.enabled = on;
            }
            if (LoadSensor != null) LoadSensor.enabled = on;
        }

        static float SnapYaw(float yaw)
        {
            float axis = Mathf.Round(yaw / 90f) * 90f;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, axis)) <= 12f) return axis;
            return Mathf.Round(yaw / 15f) * 15f;
        }

        // ================================================================== per-frame
        protected virtual void Update()
        {
            if (_phase == Phase.None)
            {
                if (IsBridging) Cleanup();
                if (AbilityInput.HasControl(Character) && ShiftersInput.BridgePressed && Character.Condition == CharacterCondition.Normal) TryForm();
                return;
            }
            var c = Character;
            // someone else changed our state (KO by hazard, capture, lost, teleport, restore)
            if (c == null || !c.IsAlive || c.Condition != CharacterCondition.Bridging || (transform.position - (_phase == Phase.Recovering ? transform.position : _origin)).sqrMagnitude > 0.25f)
            {
                Cleanup();
                return;
            }
            if (AbilityInput.HasControl(c) && ShiftersInput.BridgePressed && _phase == Phase.Solid) { Recover(); }

            float L = Tuning.BridgeLength;
            switch (_phase)
            {
                case Phase.Forming:
                    _t += Time.deltaTime / FormTime;
                    if (c.Visual != null) c.Visual.SetBridgeStretch(Mathf.Lerp(c.Height, L, AbilityPhysics.EaseInQuad(_t)), 0f);
                    if (_t >= 1f) FinishForm();
                    break;
                case Phase.Solid:
                    UpdateLoad(c);
                    break;
                case Phase.Recovering:
                    _t += Time.deltaTime / RecoverTime;
                    float e = AbilityPhysics.EaseOutBack(_t);
                    Vector3 p = Vector3.LerpUnclamped(_recoverFrom, _recoverTo, e) + Vector3.up * (Mathf.Sin(Mathf.Clamp01(_t) * Mathf.PI) * 0.6f);
                    transform.position = p;
                    if (c.Body != null) c.Body.position = p;
                    if (c.Visual != null) c.Visual.SetBridgeStretch(Mathf.Lerp(L, c.Height, Mathf.Clamp01(_t)), 0f);
                    if (_t >= 1f) FinishRecover();
                    break;
            }
        }

        void UpdateLoad(ShifterCharacter c)
        {
            CurrentLoad = LoadSensor != null ? LoadSensor.CurrentMass : 0f;
            float cap = Mathf.Max(1f, Capacity);
            float target = Mathf.Clamp01(CurrentLoad / cap);
            _sag = Mathf.MoveTowards(_sag, target, Time.deltaTime * 3f);
            if (c.Visual != null)
            {
                c.Visual.SetBridgeStretch(Tuning.BridgeLength, _sag);
                if (CurrentLoad > 1f && c.Visual.CurrentExpression != Expression.Strain && c.Visual.CurrentExpression != Expression.Scared)
                    c.Visual.SetExpression(CurrentLoad > cap ? Expression.Scared : Expression.Strain, 0.6f);
            }
            if (CurrentLoad > 1f)
            {
                _creakTimer -= Time.deltaTime;
                if (_creakTimer <= 0f)
                {
                    _creakTimer = Mathf.Lerp(1.4f, 0.3f, target) * UnityEngine.Random.Range(0.8f, 1.2f);
                    Sfx.At(SfxId.BridgeCreak, _origin + Direction * (Tuning.BridgeLength * 0.5f), 0.35f + 0.65f * target, UnityEngine.Random.Range(0.85f, 1.15f));
                }
            }
            if (CurrentLoad > cap + 0.5f)
            {
                OverloadTime += Time.deltaTime;
                if (!_warned)
                {
                    _warned = true;
                    Fx.FloatingText(_origin + Direction * (Tuning.BridgeLength * 0.5f) + Vector3.up * 0.8f, "CREAK...", UIColors.AccentYellow, 1.1f);
                    c.Say(OverLines[UnityEngine.Random.Range(0, OverLines.Length)]);
                    Sfx.Voice(c, VoiceLine.Strain);
                }
                if (OverloadTime >= Tuning.BridgeCollapseDelay) Collapse();
            }
            else
            {
                OverloadTime = Mathf.Max(0f, OverloadTime - Time.deltaTime * 2f);
                if (OverloadTime <= 0f) _warned = false;
            }
        }

        protected virtual void OnDisable() => Cleanup();
    }
}
