// =====================================================================================
//  SHIFTERS - CharacterGrabber.cs   [CONTRACT API - implementation owned by the ABILITIES agent]
//  F: pick up the best Grabbable in front (if mass <= Tuning.CarryCapacityKg(STR)), F again: put down,
//  Q: throw (Tuning.ThrowSpeed(STR), camera-pitch aimed). Carried teammates -> Condition.Carried.
//
//  Physics while holding (see Grabbable header): target kinematic + colliders disabled + parented to
//  CarryPoint. On release the target's colliders ignore the carrier's colliders for 0.2 s.
//  Carrier: Visual pose Carrying, Motor.SpeedMultiplier = lerp(1, 0.6, mass/capacity),
//  Motor.JumpMultiplier = Tuning.CarryJumpMultiplier when mass/capacity > 0.5.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class CharacterGrabber : MonoBehaviour
    {
        public ShifterCharacter Character { get; protected set; }
        public Grabbable Held { get; protected set; }
        public bool IsHolding => Held != null;
        public float CarriedMass => Held != null ? Held.Mass : 0f;
        public float Capacity => Character != null && Character.Stats != null ? Tuning.CarryCapacityKg(Character.Stats.Strength) : 60f;

        /// <summary>0..1 load fraction of the current carry.</summary>
        public float Load01 => Capacity > 0f ? Mathf.Clamp01(CarriedMass / Capacity) : 0f;
        /// <summary>Seconds after release during which the released body ignores collisions with the carrier.</summary>
        public const float ReleaseIgnoreTime = 0.2f;
        public const float LiftTime = 0.2f;

        public event Action<Grabbable> PickedUp;
        /// <summary>(object, thrown) after it was released.</summary>
        public event Action<Grabbable, bool> Released;
        /// <summary>A grab attempt failed because the target is heavier than Capacity.</summary>
        public event Action<Grabbable> GrabDenied;

        readonly Collider[] _overlap = new Collider[48];
        readonly List<Collider> _ownColliders = new List<Collider>();
        struct IgnorePair { public Collider A, B; public float Until; }
        readonly List<IgnorePair> _ignored = new List<IgnorePair>();

        Vector3 _liftFromLocal, _carryLocal;
        Quaternion _liftFromRot;
        float _liftT;
        bool _heldWasFlopped;
        PoseOverride _heldSavedPose;
        bool _heldWasAlive;
        bool _poseApplied;

        static readonly string[] CarriedLines = { "Wheee!", "I can see my house!", "This is undignified.", "Careful, I'm fragile!", "Up we go!", "Do NOT drop me." };

        protected virtual void Awake() { Character = GetComponent<ShifterCharacter>(); }

        // ================================================================== public API
        /// <summary>Pick up the best candidate in range. Returns false (and shows feedback) if none/too heavy.</summary>
        public virtual bool TryGrab()
        {
            if (Held != null || !CanGrabNow()) return false;
            var best = FindBestCandidate(out var tooHeavy);
            if (best != null) return DoGrab(best);
            if (tooHeavy != null) { DenyTooHeavy(tooHeavy); return false; }
            return false;
        }

        public virtual bool TryGrab(Grabbable target)
        {
            if (Held != null || target == null || !CanGrabNow() || !IsCandidate(target)) return false;
            if (DistanceTo(target) > Tuning.GrabRadius * 1.5f) return false;
            if (target.Mass > Capacity + 0.01f) { DenyTooHeavy(target); return false; }
            return DoGrab(target);
        }

        /// <summary>Gently put down in front.</summary>
        public virtual void Drop()
        {
            if (Held == null) return;
            if (!IsUnityAlive(Held)) { ClearHeld(); return; }
            var g = Held;
            ComputeDropPose(g, out var pos, out var rot);
            Release(pos, rot, Vector3.zero, Vector3.zero, false);
            Sfx.At(SfxId.Land, pos, 0.6f);
            Fx.Puff(pos, 0.6f);
        }

        /// <summary>Throw along direction (normalized; launch speed from STR).</summary>
        public virtual void Throw(Vector3 direction)
        {
            if (Held == null) return;
            if (!IsUnityAlive(Held)) { ClearHeld(); return; }
            var g = Held;
            if (direction.sqrMagnitude < 1e-6f) direction = DirectionFrom(transform.forward, Tuning.ThrowBaseAngle);
            direction.Normalize();
            float speed = ThrowSpeedFor(g);
            Vector3 inherit = Character != null && Character.Motor != null ? Character.Motor.PlanarVelocity * 0.5f : Vector3.zero;
            Vector3 v = direction * speed + inherit;
            Vector3 pos = g.transform.position;
            Quaternion rot = g.transform.rotation;
            if (g.Character == null) pos += new Vector3(direction.x, 0f, direction.z).normalized * 0.15f;
            Vector3 spin = g.Character == null ? Vector3.Cross(Vector3.up, direction) * UnityEngine.Random.Range(4f, 8f) : Vector3.zero;
            Release(pos, rot, v, spin, true);
            g.BeginFlight(this);
            var s = GameSession.Instance != null ? GameSession.Instance.Stats : null;
            if (s != null) s.Throws++;
            Sfx.At(SfxId.Throw, pos);
            Sfx.At(SfxId.Whoosh, pos, 0.7f);
            if (Character != null && Character.Visual != null) Character.Visual.Play(VisualAction.Punch);
        }

        /// <summary>Throw along the camera (or facing) with Tuning.ThrowBaseAngle + up to 25 deg of camera look-up.</summary>
        public void ThrowAimed() => Throw(AimedThrowDirection());

        /// <summary>Solve the throw toward a world point (low arc, falls back to the high arc / base angle). Returns
        /// true if the target was reachable with this character's throw speed.</summary>
        public bool ThrowAt(Vector3 target, bool highArc = false)
        {
            if (Held == null) return false;
            bool ok = SolveThrow(target, out var dir, highArc);
            Throw(dir);
            return ok;
        }

        /// <summary>Immediately release without effects (holder KO'd, teleport...).</summary>
        public virtual void ForceRelease()
        {
            if (Held == null) return;
            if (!IsUnityAlive(Held)) { ClearHeld(); return; }
            var t = Held.transform;
            Vector3 v = Character != null && Character.Body != null && !Character.Body.isKinematic ? Character.Body.linearVelocity : Vector3.zero;
            Release(t.position, t.rotation, v, Vector3.zero, false);
        }

        // ================================================================== queries (sensor/bots)
        /// <summary>Best liftable candidate in front (or null). tooHeavy = best candidate that exceeds Capacity.</summary>
        public Grabbable FindBestCandidate(out Grabbable tooHeavy)
        {
            tooHeavy = null;
            if (Character == null) return null;
            Grabbable best = null;
            float bestScore = float.MaxValue, heavyScore = float.MaxValue;
            Vector3 origin = GrabOrigin;
            Vector3 fwd = transform.forward;
            int n = Physics.OverlapSphereNonAlloc(origin + fwd * 0.4f, Tuning.GrabRadius + 0.6f, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var rb = col.attachedRigidbody;
                if (rb == null || rb == Character.Body) continue;
                var g = rb.GetComponent<Grabbable>();
                if (g == null || !IsCandidate(g)) continue;
                Vector3 cp = col.bounds.ClosestPoint(origin);
                Vector3 d = cp - origin;
                float dist = d.magnitude;
                if (dist > Tuning.GrabRadius) continue;
                Vector3 flat = new Vector3(d.x, 0f, d.z);
                float facing = flat.sqrMagnitude > 1e-4f ? Vector3.Dot(fwd, flat.normalized) : 1f;
                if (facing < -0.25f) continue;
                float score = dist + (1f - facing) * 0.8f;
                if (g.Mass > Capacity + 0.01f) { if (score < heavyScore) { heavyScore = score; tooHeavy = g; } }
                else if (score < bestScore) { bestScore = score; best = g; }
            }
            for (int i = 0; i < n; i++) _overlap[i] = null;
            return best;
        }

        /// <summary>Is this a valid target at all (ignores mass and distance)?</summary>
        public bool IsCandidate(Grabbable g)
        {
            if (g == null || !g.isActiveAndEnabled || !g.CanBeGrabbed || g.IsHeld || g.Body == null) return false;
            if (Character != null && g == Character.Grabbable) return false;
            var c = g.Character;
            if (c != null)
            {
                if (c == Character) return false;
                if (c.State == CharacterState.Lost || c.State == CharacterState.Captured) return false;
                var cond = c.Condition;
                if (cond == CharacterCondition.Bridging || cond == CharacterCondition.Seated || cond == CharacterCondition.Carried || cond == CharacterCondition.Interacting) return false;
                if (c.Bridge != null && c.Bridge.IsBridging) return false;
                if (c.Grabber != null && Character != null && c.Grabber.Held == Character.Grabbable) return false;
                return true;
            }
            return !g.Body.isKinematic;
        }

        /// <summary>Smallest STR whose capacity lifts this mass (11 = nobody).</summary>
        public static int RequiredStrength(float mass)
        {
            for (int s = Tuning.StatMin; s <= Tuning.StatMax; s++) if (Tuning.CarryCapacityKg(s) >= mass) return s;
            return Tuning.StatMax + 1;
        }

        /// <summary>"Needs STR 7 — Gary has STR 3" style reason, or null if liftable.</summary>
        public string TooHeavyReason(Grabbable g)
        {
            if (g == null || g.Mass <= Capacity + 0.01f) return null;
            int need = RequiredStrength(g.Mass);
            int has = Character != null && Character.Stats != null ? Character.Stats.Strength : 0;
            string who = Character != null ? Character.DisplayName : "You";
            return need > Tuning.StatMax ? "Nobody can lift that. Try pushing it." : "Needs STR " + need + " — " + who + " has STR " + has;
        }

        public float ThrowSpeedFor(Grabbable g)
        {
            int str = Character != null && Character.Stats != null ? Character.Stats.Strength : 5;
            float load = g != null && Capacity > 0f ? Mathf.Clamp01(g.Mass / Capacity) : 0f;
            float mult = g != null ? Mathf.Max(0.1f, g.ThrowSpeedMultiplier) : 1f;
            return Tuning.ThrowSpeed(str) * Mathf.Lerp(1f, 0.8f, load) * mult;
        }

        /// <summary>Camera-aimed launch direction: camera forward flattened, elevation = base angle + clamp(look-up, 0..25).</summary>
        public Vector3 AimedThrowDirection()
        {
            var cam = ShiftersCamera.Instance;
            Vector3 flat = transform.forward;
            float lookUp = 0f;
            if (cam != null && Character != null && Character.IsActive && !Character.ExternalControl)
            {
                var f = cam.transform.forward;
                flat = new Vector3(f.x, 0f, f.z);
                if (flat.sqrMagnitude < 1e-4f) flat = transform.forward;
                lookUp = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
            return DirectionFrom(flat, Tuning.ThrowBaseAngle + Mathf.Clamp(lookUp, 0f, 25f));
        }

        public static Vector3 DirectionFrom(Vector3 flatForward, float elevationDegrees)
        {
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 1e-6f) flatForward = Vector3.forward;
            flatForward.Normalize();
            float r = elevationDegrees * Mathf.Deg2Rad;
            return (flatForward * Mathf.Cos(r) + Vector3.up * Mathf.Sin(r)).normalized;
        }

        /// <summary>Ballistic solve toward target (world point the object should come to rest on) with this character's
        /// throw speed. Models the motor's extra fall gravity for thrown characters and aims the centre of mass.
        /// Default: target above the launch point -> steep lob (clears ledge edges), otherwise the flattest arc >= 25 deg.
        /// highArc forces the lob. Returns false (and the max-range direction) if out of reach.</summary>
        public bool SolveThrow(Vector3 target, out Vector3 direction, bool highArc = false)
        {
            var held = Held;
            Vector3 from = held != null ? held.transform.position : (Character != null && Character.CarryPoint != null ? Character.CarryPoint.position : transform.position);
            if (held != null && held.Character != null)
            {
                from += Vector3.up * (held.Character.Height * 0.5f);
                target += Vector3.up * (held.Character.Radius + 0.1f);
            }
            else if (held != null)
            {
                var b = held.LocalBounds;
                from += Vector3.up * (b.center.y - b.min.y) * held.transform.localScale.y;
                target += Vector3.up * (b.extents.y * held.transform.localScale.y);
            }
            float v = ThrowSpeedFor(held);
            float g = Mathf.Max(0.1f, Physics.gravity.magnitude);
            float gDown = held != null && held.Character != null ? g * Tuning.FallGravityMultiplier : g;
            Vector3 d = target - from;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float x = flat.magnitude, y = d.y;
            if (x < 0.05f) { direction = Vector3.up; return true; }
            bool lob = highArc || y > 0.5f;
            float bestAngle = -1f, bestErr = float.MaxValue, farAngle = 45f, farX = 0f;
            for (float a = 15f; a <= 82f; a += 0.25f)
            {
                float r = a * Mathf.Deg2Rad;
                float vx = v * Mathf.Cos(r), vy = v * Mathf.Sin(r);
                float apex = vy * vy / (2f * g);
                if (apex < y) continue;
                float t = vy / g + Mathf.Sqrt(2f * (apex - y) / gDown);
                float lx = vx * t;
                if (lx > farX) { farX = lx; farAngle = a; }
                float err = Mathf.Abs(lx - x);
                if (err > 0.25f) continue;
                if (bestAngle < 0f) { bestAngle = a; bestErr = err; if (!lob && a >= 25f) break; continue; }
                if (lob) { bestAngle = a; bestErr = err; } // keep the steepest hit
                else if (err < bestErr && bestAngle < 25f) { bestAngle = a; bestErr = err; }
            }
            if (bestAngle < 0f) { direction = DirectionFrom(flat, farAngle); return false; }
            direction = DirectionFrom(flat, bestAngle);
            return true;
        }

        void DenyTooHeavy(Grabbable g)
        {
            if (Character == null || g == null) return;
            Fx.FloatingText(g.transform.position + Vector3.up * 1.2f, "TOO HEAVY!", UIColors.Danger, 1.1f);
            if (Character.Visual != null) { Character.Visual.Play(VisualAction.Strain); Character.Visual.SetExpression(Expression.Strain, 1f); }
            Sfx.Play(SfxId.Denied);
            Sfx.Voice(Character, VoiceLine.Strain);
            AbilityFeedback.Toast(TooHeavyReason(g), UIColors.Danger, 1.5f);
            GrabDenied?.Invoke(g);
        }

        Vector3 GrabOrigin => Character.Feet + Vector3.up * (Character.Height * 0.45f);

        float DistanceTo(Grabbable g)
        {
            Vector3 o = GrabOrigin;
            float best = Vector3.Distance(o, g.transform.position);
            var cols = g.Character != null ? null : g.GetComponentsInChildren<Collider>();
            if (g.Character != null && g.Character.Capsule != null && g.Character.Capsule.enabled) best = Vector3.Distance(o, g.Character.Capsule.bounds.ClosestPoint(o));
            if (cols != null) for (int i = 0; i < cols.Length; i++) if (cols[i].enabled && !cols[i].isTrigger) best = Mathf.Min(best, Vector3.Distance(o, cols[i].bounds.ClosestPoint(o)));
            return best;
        }

        bool CanGrabNow()
        {
            if (Character == null || !Character.IsAlive) return false;
            var c = Character.Condition;
            if (c != CharacterCondition.Normal && c != CharacterCondition.Interacting) return false;
            if (Character.Grabbable != null && Character.Grabbable.IsHeld) return false;
            return true;
        }

        // ================================================================== grab / release internals
        bool DoGrab(Grabbable g)
        {
            var tc = g.Character;
            if (tc != null)
            {
                if (tc.Grabber != null) tc.Grabber.ForceRelease();
                _heldWasAlive = tc.IsAlive;
                _heldWasFlopped = tc.Condition == CharacterCondition.Flopped;
                _heldSavedPose = tc.Visual != null ? tc.Visual.CurrentPose : PoseOverride.None;
                // core allows Downed -> Carried too; with an older core that refuses it the KO'd body is still held
                if (!tc.SetCondition(CharacterCondition.Carried) && tc.IsAlive) return false;
            }
            g.BeginHoldPhysics();
            var t = g.transform;
            t.SetParent(Character.CarryPoint, true);
            _liftFromLocal = t.localPosition;
            _liftFromRot = t.localRotation;
            _carryLocal = tc != null ? Vector3.zero : -g.BottomOffset;
            _liftT = 0f;
            Held = g;
            g.NotifyGrabbed(this);
            ApplyCarrierEffects(true);
            if (tc != null)
            {
                if (tc.Visual != null) { tc.Visual.SetPose(PoseOverride.Carried); tc.Visual.SetExpression(Expression.Surprised, 1.2f); }
                if (tc.IsAlive && UnityEngine.Random.value < 0.7f) tc.Say(CarriedLines[UnityEngine.Random.Range(0, CarriedLines.Length)]);
            }
            Sfx.At(SfxId.Grab, t.position);
            Fx.Puff(Character.Feet, 0.4f);
            if (Load01 > 0.6f && Character.Visual != null) { Character.Visual.Play(VisualAction.Strain); Sfx.Voice(Character, VoiceLine.Strain); }
            PickedUp?.Invoke(g);
            return true;
        }

        void Release(Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angular, bool thrown)
        {
            var g = Held;
            Held = null;
            ApplyCarrierEffects(false);
            if (g == null) return;
            var tc = g.Character;
            if (tc != null) rot = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
            g.EndHoldPhysics(pos, rot, velocity, angular);
            IgnoreCarrier(g, ReleaseIgnoreTime);
            g.NotifyReleased(velocity, thrown); // Holder = null BEFORE condition changes (core refuses leaving Carried while held)
            if (tc != null) RestoreCharacter(tc, thrown, velocity, pos, rot);
            Released?.Invoke(g, thrown);
        }

        void RestoreCharacter(ShifterCharacter tc, bool thrown, Vector3 velocity, Vector3 pos, Quaternion rot)
        {
            var m = tc.Motor;
            if (tc.IsAlive)
            {
                if (_heldWasFlopped)
                {
                    tc.SetCondition(CharacterCondition.Flopped);
                    if (m != null) m.BeginTumble(9999f);
                    if (tc.Visual != null) tc.Visual.SetPose(PoseOverride.Flop);
                }
                else if (thrown)
                {
                    tc.SetCondition(CharacterCondition.Thrown);
                    if (m != null) m.BeginTumble(0.6f);
                    if (tc.Visual != null) { tc.Visual.SetPose(PoseOverride.None); tc.Visual.SetExpression(Expression.Scared, 1.2f); }
                }
                else
                {
                    if (m != null) { m.Teleport(pos, rot); m.StandUp(); }
                    tc.SetCondition(CharacterCondition.Normal);
                    if (tc.Visual != null) tc.Visual.SetPose(PoseOverride.None);
                }
            }
            else
            {
                // Downed: stays limp (core allows Downed -> Thrown; the motor returns it to Normal when settled)
                if (!thrown || !tc.SetCondition(CharacterCondition.Thrown)) tc.SetCondition(CharacterCondition.Normal);
                if (m != null) m.BeginTumble(0.6f);
                if (tc.Visual != null) tc.Visual.SetPose(_heldSavedPose == PoseOverride.Carried ? PoseOverride.KO : _heldSavedPose);
            }
            // BeginTumble/Teleport may reset velocities: apply the launch last.
            if (tc.Body != null && !tc.Body.isKinematic && (thrown || velocity.sqrMagnitude > 0.01f)) tc.Body.linearVelocity = velocity;
        }

        void ApplyCarrierEffects(bool holding)
        {
            if (Character == null) return;
            var m = Character.Motor;
            if (holding)
            {
                float a = Load01;
                if (m != null) { m.SpeedMultiplier = Mathf.Lerp(1f, 0.6f, a); m.JumpMultiplier = a > 0.5f ? Tuning.CarryJumpMultiplier : 1f; }
                if (Character.Visual != null) Character.Visual.SetPose(PoseOverride.Carrying);
                _poseApplied = true;
            }
            else
            {
                if (m != null) { m.SpeedMultiplier = 1f; m.JumpMultiplier = 1f; }
                if (_poseApplied && Character.Visual != null && Character.Visual.CurrentPose == PoseOverride.Carrying) Character.Visual.SetPose(PoseOverride.None);
                _poseApplied = false;
            }
        }

        void ClearHeld()
        {
            Held = null;
            ApplyCarrierEffects(false);
        }

        void IgnoreCarrier(Grabbable g, float seconds)
        {
            if (Character == null) return;
            _ownColliders.Clear();
            GetComponentsInChildren(false, _ownColliders);
            float until = Time.time + seconds;
            var held = g.DisabledColliders;
            for (int i = 0; i < _ownColliders.Count; i++)
            {
                var a = _ownColliders[i];
                if (a == null || !a.enabled || a.isTrigger) continue;
                for (int j = 0; j < held.Count; j++)
                {
                    var b = held[j];
                    if (b == null || !b.enabled || b.isTrigger) continue;
                    Physics.IgnoreCollision(a, b, true);
                    _ignored.Add(new IgnorePair { A = a, B = b, Until = until });
                }
            }
            _ownColliders.Clear();
        }

        void UpdateIgnoredPairs()
        {
            if (_ignored.Count == 0) return;
            float now = Time.time;
            for (int i = _ignored.Count - 1; i >= 0; i--)
            {
                var p = _ignored[i];
                if (now < p.Until) continue;
                if (p.A != null && p.B != null) Physics.IgnoreCollision(p.A, p.B, false);
                _ignored.RemoveAt(i);
            }
        }

        void ComputeDropPose(Grabbable g, out Vector3 pos, out Quaternion rot)
        {
            Vector3 fwd = transform.forward;
            float yaw = transform.eulerAngles.y;
            rot = Quaternion.Euler(0f, yaw, 0f);
            float foot = g.FootprintRadius;
            float reach = Character.Radius + foot + 0.2f;
            Vector3 chest = Character.Feet + Vector3.up * Mathf.Min(1.0f, Character.Height * 0.5f);
            Vector3 dir = fwd;
            if (AbilityPhysics.SphereCast(chest, Mathf.Clamp(foot * 0.6f, 0.1f, 0.35f), fwd, reach, out var wall, Character.Body, true))
            {
                // blocked in front: try behind, else drop on the spot
                if (!AbilityPhysics.SphereCast(chest, Mathf.Clamp(foot * 0.6f, 0.1f, 0.35f), -fwd, reach, out _, Character.Body, true)) dir = -fwd;
                else dir = Vector3.zero;
            }
            Vector3 xz = Character.Feet + dir * reach;
            float y = Character.Feet.y;
            if (AbilityPhysics.GroundBelow(xz, Character.Height + 0.6f, 3f, out var ground, Character.Body)) y = ground.point.y;
            if (dir == Vector3.zero) y = g.transform.position.y; // nowhere to put it: let it fall from overhead
            if (g.Character != null) pos = new Vector3(xz.x, y + 0.03f, xz.z);
            else pos = new Vector3(xz.x, y + 0.02f, xz.z) - rot * g.BottomOffset;
        }

        // ================================================================== per-frame
        protected virtual void Update()
        {
            UpdateIgnoredPairs();
            if (Held != null) MaintainHold();
            HandleInput();
        }

        void HandleInput()
        {
            if (!AbilityInput.HasControl(Character)) return;
            if (ShiftersInput.GrabPressed)
            {
                if (Held != null) Drop();
                else TryGrab();
            }
            else if (ShiftersInput.ThrowPressed && Held != null) ThrowAimed();
        }

        void MaintainHold()
        {
            if (!IsUnityAlive(Held)) { ClearHeld(); return; }
            // carrier can no longer hold
            if (Character == null || !Character.IsAlive) { ForceRelease(); return; }
            var cond = Character.Condition;
            if (cond != CharacterCondition.Normal && cond != CharacterCondition.Interacting) { ForceRelease(); return; }
            var tc = Held.Character;
            if (tc != null)
            {
                if (tc.State == CharacterState.Lost || tc.State == CharacterState.Captured) { ForceRelease(); return; }
                if (_heldWasAlive != tc.IsAlive)
                {
                    // revived (or KO'd) while carried: revive teleports -> let go where they are
                    if (tc.IsAlive) { ForceRelease(); return; }
                    _heldWasAlive = false;
                }
                if (tc.IsAlive && tc.Condition != CharacterCondition.Carried) { ForceRelease(); return; }
            }
            if (Held.transform.parent != Character.CarryPoint) { ReleaseBookkeepingOnly(); return; }
            if (Held.Body != null && !Held.Body.isKinematic) Held.Body.isKinematic = true;
        }

        void ReleaseBookkeepingOnly()
        {
            // someone else (bot, respawner) took the object: forget it without touching its transform.
            var g = Held;
            Held = null;
            ApplyCarrierEffects(false);
            if (g != null) g.NotifyReleased(Vector3.zero, false);
        }

        protected virtual void LateUpdate()
        {
            if (Held == null) return;
            if (!IsUnityAlive(Held)) { ClearHeld(); return; }
            var t = Held.transform;
            if (t.parent != Character.CarryPoint) return;
            if (_liftT < 1f)
            {
                _liftT = Mathf.Min(1f, _liftT + Time.deltaTime / LiftTime);
                t.localPosition = Vector3.LerpUnclamped(_liftFromLocal, _carryLocal, AbilityPhysics.EaseOutBack(_liftT));
                t.localRotation = Quaternion.Slerp(_liftFromRot, Quaternion.identity, AbilityPhysics.EaseOutCubic(_liftT));
            }
            else
            {
                t.localPosition = _carryLocal;
                t.localRotation = Quaternion.identity;
            }
        }

        protected virtual void OnDisable()
        {
            if (Held != null) ForceRelease();
            for (int i = 0; i < _ignored.Count; i++) if (_ignored[i].A != null && _ignored[i].B != null) Physics.IgnoreCollision(_ignored[i].A, _ignored[i].B, false);
            _ignored.Clear();
        }

        static bool IsUnityAlive(Grabbable g) => g != null && g.gameObject != null;
    }
}
