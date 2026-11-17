// =====================================================================================
//  SHIFTERS - TeamBot.Movement.cs   [BOTS module]
//  Locomotion through motor commands only: direction recomputed every frame, braking near the
//  goal, stuck detection (hop once, then fail with a dump), tumble recovery waits.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed partial class TeamBot
    {
        const string WaitToken = "~wait";

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Vector3 FlatDir(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Flat(fallback).normalized;
        }

        /// <summary>null = free to drive; WaitToken = recovering (tumble/thrown/stunned); else a failure reason.</summary>
        string Blocker(ShifterCharacter c)
        {
            if (c == null) return "character destroyed";
            if (!c.IsAlive) return c.DisplayName + " is " + c.State + (string.IsNullOrEmpty(c.LastCause) ? "" : " (" + c.LastCause + ")");
            switch (c.Condition)
            {
                case CharacterCondition.Normal: return c.Motor != null && c.Motor.IsTumbling ? WaitToken : null;
                case CharacterCondition.Thrown:
                case CharacterCondition.Stunned: return WaitToken;
                default: return c.DisplayName + " cannot move while " + c.Condition;
            }
        }

        void Stop(ShifterCharacter c) => Driver.Stop(c);

        // ------------------------------------------------------------------ waiting
        /// <summary>Wait simulated seconds (all bot characters hold still unless another action drives them).</summary>
        public IEnumerator Wait(float seconds)
        {
            float t0 = Now;
            while (Now - t0 < seconds) yield return null;
        }

        IEnumerator WaitFrames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Wait until cond() is true. Fails the run on timeout unless failOnTimeout is false.</summary>
        public IEnumerator WaitUntil(Func<bool> cond, float timeout, string label, bool failOnTimeout = true)
        {
            var s = Begin("WaitUntil", null, label);
            if (s == null) yield break;
            float t0 = Now;
            while (true)
            {
                bool ok;
                try { ok = cond(); }
                catch (Exception e) { FailStep(s, "condition threw " + e.GetType().Name + ": " + e.Message); yield break; }
                if (ok) break;
                if (Now - t0 > timeout)
                {
                    if (failOnTimeout) FailStep(s, "timed out after " + timeout + " s");
                    else End(s, true, "(timed out, not required)");
                    yield break;
                }
                yield return null;
            }
            End(s);
        }

        /// <summary>Wait until the character is up, grounded and nearly still (no failure on timeout).</summary>
        public IEnumerator WaitSettled(ShifterCharacter c, float timeout = 3f)
        {
            float t0 = Now;
            while (Now - t0 < timeout && c != null)
            {
                bool settled = c.IsAlive && c.Condition == CharacterCondition.Normal && c.Motor != null && !c.Motor.IsTumbling
                               && c.Motor.IsGrounded && c.Motor.PlanarVelocity.magnitude < 0.35f;
                if (settled) break;
                yield return null;
            }
        }

        /// <summary>Run several actions at the same time (e.g. two characters walking). Finishes when all finish.</summary>
        public IEnumerator Parallel(params IEnumerator[] routines)
        {
            var list = new List<BotRoutine>();
            foreach (var r in routines) if (r != null) list.Add(new BotRoutine(r, () => Failed && !Options.ContinueAfterFailure));
            while (true)
            {
                bool any = false;
                foreach (var r in list)
                {
                    if (r.Done) continue;
                    if (r.Step()) any = true;
                    if (r.Error != null) { Debug.LogException(r.Error); Fail("EXCEPTION in parallel action: " + r.Error.Message); }
                }
                if (Failed && !Options.ContinueAfterFailure) { foreach (var r in list) r.Abort(); yield break; }
                if (!any) yield break;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ facing
        /// <summary>Turn in place toward a world direction (Command.Face).</summary>
        public IEnumerator Face(ShifterCharacter c, Vector3 direction, float timeout = 1.2f)
        {
            var s = Begin("Face", c, BotFmt.V(direction));
            if (!Ready(s, c)) yield break;
            yield return FaceCore(c, direction, timeout);
            End(s);
        }

        /// <summary>Turn toward a target.</summary>
        public IEnumerator FaceTarget(ShifterCharacter c, BotTarget target, float timeout = 1.2f)
        {
            var s = Begin("Face", c, target.Describe());
            if (!Ready(s, c)) yield break;
            yield return FaceCore(c, target.Position - c.Feet, timeout);
            End(s);
        }

        IEnumerator FaceCore(ShifterCharacter c, Vector3 dir, float timeout)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f || c == null) yield break;
            dir.Normalize();
            float t0 = Now;
            Driver.SetMove(c, Vector3.zero, false);
            Driver.SetFace(c, dir);
            while (Now - t0 < timeout && Vector3.Angle(Flat(c.transform.forward), dir) > 4f) yield return null;
            if (Vector3.Angle(Flat(c.transform.forward), dir) > 4f && c.Condition == CharacterCondition.Normal)
            {
                Warn("Face: " + c.DisplayName + " did not turn within " + timeout + " s (motor ignores Command.Face?) - setting yaw directly");
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                if (c.Body != null && !c.Body.isKinematic) c.Body.rotation = rot;
                c.transform.rotation = rot;
                yield return null;
            }
            Driver.SetFace(c, Vector3.zero);
        }

        // ------------------------------------------------------------------ walking
        /// <summary>Walk (or sprint) until within tolerance (planar) of the target. Stuck => one hop, then failure.</summary>
        public IEnumerator WalkTo(ShifterCharacter c, BotTarget target, bool sprint = false, float tolerance = 0.6f, float timeout = 20f)
        {
            var s = Begin(sprint ? "SprintTo" : "WalkTo", c, target.Describe());
            if (!Ready(s, c)) yield break;
            if (!target.IsValid) { FailStep(s, "target not found: " + target.Describe(), c); yield break; }
            yield return WalkCore(c, target, sprint, tolerance, timeout, true, s);
            End(s, true, "at " + BotFmt.V(c.Feet));
        }

        /// <summary>Walk through waypoints without stopping in between.</summary>
        public IEnumerator WalkPath(ShifterCharacter c, IList<BotTarget> points, bool sprint = false, float tolerance = 0.6f, float timeoutPerPoint = 20f)
        {
            var s = Begin(sprint ? "SprintPath" : "WalkPath", c, points.Count + " pts");
            if (!Ready(s, c)) yield break;
            for (int i = 0; i < points.Count; i++)
            {
                bool last = i == points.Count - 1;
                if (!points[i].IsValid) { FailStep(s, "waypoint " + i + " not found: " + points[i].Describe(), c); yield break; }
                yield return WalkCore(c, points[i], sprint, last ? tolerance : Mathf.Max(0.8f, tolerance), timeoutPerPoint, last, s);
                if (s.Ended) yield break;
            }
            End(s, true, "at " + BotFmt.V(c.Feet));
        }

        public IEnumerator WalkPath(ShifterCharacter c, bool sprint, params BotTarget[] points) => WalkPath(c, points, sprint);

        /// <summary>Everyone alive walks to a spot (side by side, 1.3 m apart) at the same time. A LevelExit target
        /// (e.g. the "exit" mark) is delegated to EnterExit (lanes into the cab).</summary>
        public IEnumerator GatherAt(BotTarget center, bool sprint = false, float spacing = 1.3f, float timeout = 30f)
        {
            if (TargetIsExit(center)) { yield return EnterExit(center, sprint, timeout); yield break; }
            var s = Begin("GatherAt", null, center.Describe());
            if (s == null) yield break;
            var alive = Others();
            var routines = new List<IEnumerator>();
            Vector3 c0 = center.Position;
            Vector3 approach = alive.Count > 0 ? FlatDir(c0 - alive[0].Feet, Vector3.forward) : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, approach);
            for (int i = 0; i < alive.Count; i++)
            {
                float k = (i - (alive.Count - 1) * 0.5f) * spacing;
                routines.Add(WalkCore(alive[i], center.Offset(side * k), sprint, 0.5f, timeout, true, s));
            }
            yield return Parallel(routines.ToArray());
            End(s);
        }

        /// <summary>Core walking loop shared by every movement action. Calls FailStep on failure.</summary>
        IEnumerator WalkCore(ShifterCharacter c, BotTarget target, bool sprint, float tolerance, float timeout, bool stopAtEnd, BotStep s)
        {
            float t0 = Now, lastProgress = Now, best = float.MaxValue, recovering = 0f;
            int hops = 0;
            while (true)
            {
                if (s != null && s.Ended) yield break;
                var b = Blocker(c);
                if (b == WaitToken)
                {
                    Stop(c);
                    recovering += 1f / 60f;
                    if (recovering > 6f) { FailStep(s, c.DisplayName + " did not recover from " + c.Condition + " in 6 s", c); yield break; }
                    lastProgress = Now;
                    yield return null;
                    continue;
                }
                if (b != null) { Stop(c); FailStep(s, b, c); yield break; }
                recovering = 0f;

                Vector3 to = Flat(target.Position - c.Feet);
                float d = to.magnitude;
                if (d <= tolerance) break;
                if (d < best - 0.25f) { best = d; lastProgress = Now; }
                else if (Now - lastProgress > Options.StuckSeconds)
                {
                    if (hops < 1)
                    {
                        hops++;
                        Warn(c.DisplayName + " stuck " + BotFmt.F(d) + " m from " + target.Describe() + " - hopping");
                        Driver.PressJump(c);
                        lastProgress = Now;
                    }
                    else
                    {
                        Stop(c);
                        FailStep(s, "STUCK " + BotFmt.F(d) + " m from " + target.Describe() + " at " + BotFmt.V(c.Feet)
                                    + " grounded=" + (c.Motor != null && c.Motor.IsGrounded) + " ground=" + GroundName(c)
                                    + " v=" + BotFmt.V(c.Motor != null ? c.Motor.Velocity : Vector3.zero), c);
                        yield break;
                    }
                }
                if (Now - t0 > timeout)
                {
                    Stop(c);
                    FailStep(s, "timeout " + timeout + " s, still " + BotFmt.F(d) + " m from " + target.Describe() + " at " + BotFmt.V(c.Feet), c);
                    yield break;
                }
                float mag = Mathf.Clamp(d / 1.2f, 0.35f, 1f);
                Driver.SetMove(c, to / d * mag, sprint && d > 2.5f);
                Driver.SetJumpHeld(c, hops > 0 && Now - lastProgress < 0.4f);
                yield return null;
            }
            if (stopAtEnd)
            {
                Stop(c);
                float t1 = Now;
                while (Now - t1 < 0.5f && c.Motor != null && c.Motor.PlanarVelocity.magnitude > 0.4f) yield return null;
            }
        }

        static string GroundName(ShifterCharacter c) =>
            c != null && c.Motor != null && c.Motor.GroundCollider != null ? c.Motor.GroundCollider.name : "-";

        // ------------------------------------------------------------------ jumping
        /// <summary>Jump in place (or while moving: keeps the current move). Holds jump until the apex, waits to land.</summary>
        public IEnumerator Jump(ShifterCharacter c, Vector3 moveDuringJump = default, float timeout = 3f)
        {
            var s = Begin("Jump", c, null);
            if (!Ready(s, c)) yield break;
            yield return JumpCore(c, moveDuringJump, () => moveDuringJump, false, timeout, s);
            End(s, true, "landed " + BotFmt.V(c.Feet));
        }

        /// <summary>Air steering that arrives over target() exactly when the feet come down to its height: desired planar
        /// velocity = remaining offset / time-to-land (gravity up, x Tuning.FallGravityMultiplier down), as a move input.
        /// Never returns a zero input: with no input the motor keeps all air momentum (no braking).</summary>
        Func<Vector3> LandOnSteer(ShifterCharacter c, Func<Vector3> target, bool sprint)
        {
            return () =>
            {
                Vector3 p = target();
                Vector3 to = Flat(p - c.Feet);
                var m = c.Motor;
                float vy = m != null ? m.Velocity.y : 0f;
                float gUp = Mathf.Abs(Physics.gravity.y), gDown = gUp * Tuning.FallGravityMultiplier;
                float h = c.Feet.y - p.y;
                if (vy <= 0.5f && h < 0f && m != null && m.IsGrounded) vy = Tuning.JumpVelocity * Mathf.Max(0f, m.JumpMultiplier); // take-off frame
                float t = vy > 0f
                    ? vy / gUp + Mathf.Sqrt(2f * Mathf.Max(0f, h + vy * vy / (2f * gUp)) / gDown)
                    : (vy + Mathf.Sqrt(Mathf.Max(0f, vy * vy + 2f * gDown * Mathf.Max(0f, h)))) / gDown;
                t = Mathf.Max(t, 0.06f);
                float maxSpeed = m != null ? (sprint ? m.SprintSpeed : m.WalkSpeed) * Mathf.Max(0.05f, m.SpeedMultiplier) : 5f;
                Vector3 desired = to / t;
                Vector3 dir = desired.sqrMagnitude > 1e-6f ? desired.normalized
                            : (m != null && m.PlanarVelocity.sqrMagnitude > 1e-4f ? -m.PlanarVelocity.normalized : c.transform.forward);
                return dir * Mathf.Clamp(desired.magnitude / maxSpeed, 0.025f, 1f);
            };
        }

        /// <summary>Press jump, hold to the apex, steer with steer() while airborne, wait for the landing.</summary>
        IEnumerator JumpCore(ShifterCharacter c, Vector3 move, Func<Vector3> steer, bool sprint, float timeout, BotStep s)
        {
            Driver.SetMove(c, move, sprint);
            Driver.PressJump(c);
            float t0 = Now;
            bool airborne = false;
            while (true)
            {
                if (s != null && s.Ended) yield break;
                var b = Blocker(c);
                if (b != null && b != WaitToken) { FailStep(s, b, c); yield break; }
                var m = c.Motor;
                if (m == null) { FailStep(s, "no motor", c); yield break; }
                if (!airborne && (!m.IsGrounded || m.Velocity.y > 1.5f)) airborne = true;
                if (!airborne && Now - t0 > 0.4f)
                {
                    FailStep(s, "jump did not trigger (grounded=" + m.IsGrounded + " jumpedEvent=" + Driver.JumpSeen(c) + " v=" + BotFmt.V(m.Velocity) + ")", c);
                    yield break;
                }
                if (airborne && m.IsGrounded && m.Velocity.y <= 0.5f && Now - t0 > 0.15f) break;
                if (Now - t0 > timeout) { FailStep(s, "still airborne after " + timeout + " s at " + BotFmt.V(c.Feet), c); yield break; }
                Driver.SetJumpHeld(c, m.Velocity.y > 0.2f || !airborne);
                Vector3 st = steer != null ? steer() : move;
                Driver.SetMove(c, st, sprint);
                yield return null;
            }
            Driver.SetJumpHeld(c, false);
        }

        /// <summary>Run-up from behind the takeoff point, jump at the edge, steer to the landing point.
        /// Verifies the landing (height within 0.6 m, planar within 1.2 m).</summary>
        public IEnumerator JumpGap(ShifterCharacter c, BotTarget takeoff, BotTarget landing, bool sprint = true, float runUp = 3.5f)
        {
            var s = Begin("JumpGap", c, takeoff.Describe() + " -> " + landing.Describe());
            if (!Ready(s, c)) yield break;
            if (!takeoff.IsValid || !landing.IsValid) { FailStep(s, "takeoff/landing not found", c); yield break; }
            Vector3 to = takeoff.Position, la = landing.Position;
            Vector3 dir = FlatDir(la - to, c.transform.forward);
            Vector3 start = to - dir * runUp;
            yield return WalkCore(c, start, false, 0.35f, 20f, true, s);
            if (s.Ended) yield break;
            yield return FaceCore(c, dir, 1f);
            // run toward the edge
            float t0 = Now;
            while (true)
            {
                var b = Blocker(c);
                if (b != null && b != WaitToken) { FailStep(s, b, c); yield break; }
                float along = Vector3.Dot(c.Feet - to, dir);
                if (along >= -0.12f) break;
                if (Now - t0 > 6f) { FailStep(s, "never reached the takeoff point (" + BotFmt.F(-along) + " m short)", c); yield break; }
                Vector3 aim = FlatDir(la - c.Feet, dir);
                Driver.SetMove(c, aim, sprint);
                yield return null;
            }
            yield return JumpCore(c, FlatDir(la - c.Feet, dir), () => FlatDir(la - c.Feet, dir) * Mathf.Clamp01(Flat(la - c.Feet).magnitude / 0.6f), sprint, 4f, s);
            if (s.Ended) yield break;
            if (Mathf.Abs(c.Feet.y - la.y) > 0.6f || Flat(c.Feet - la).magnitude > 2.5f)
            {
                Stop(c);
                FailStep(s, "missed the landing: feet " + BotFmt.V(c.Feet) + " vs landing " + BotFmt.V(la), c);
                yield break;
            }
            yield return WalkCore(c, la, false, 0.6f, 5f, true, s);
            if (s.Ended) yield break;
            End(s, true, "landed " + BotFmt.V(c.Feet));
        }

        /// <summary>Walk toward a ledge top (<= Tuning.LedgeJumpMax high) and jump onto it when the wall is in front
        /// (or immediately when standing on a teammate). Up to 3 attempts.</summary>
        public IEnumerator ClimbOnto(ShifterCharacter c, BotTarget ledgeTop, float timeout = 15f)
        {
            var s = Begin("ClimbOnto", c, ledgeTop.Describe());
            if (!Ready(s, c)) yield break;
            Vector3 top = ledgeTop.Position;
            float t0 = Now;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool jumped = false;
                float lastProgress = Now, best = float.MaxValue;
                while (true)
                {
                    if (s.Ended) yield break;
                    var b = Blocker(c);
                    if (b == WaitToken) { Stop(c); yield return null; continue; }
                    if (b != null) { FailStep(s, b, c); yield break; }
                    Vector3 to = Flat(top - c.Feet);
                    float d = to.magnitude;
                    bool onTop = c.Motor.IsGrounded && c.Feet.y > top.y - 0.25f;
                    if (onTop) { yield return WalkCore(c, top, false, 0.6f, 6f, true, s); if (!s.Ended) End(s, true, "on top at " + BotFmt.V(c.Feet)); yield break; }
                    if (Now - t0 > timeout) { FailStep(s, "timeout climbing (feet " + BotFmt.V(c.Feet) + ", ledge top y " + BotFmt.F(top.y) + ")", c); yield break; }
                    Vector3 dir = d > 1e-3f ? to / d : c.transform.forward;
                    if (!jumped && c.Motor.IsGrounded && (c.Motor.StandingOn != null || WallAhead(c, dir, 0.55f)))
                    {
                        jumped = true;
                        yield return JumpCore(c, dir, () => FlatDir(top - c.Feet, dir), false, 3f, s);
                        if (s.Ended) yield break;
                        continue;
                    }
                    if (d < best - 0.2f) { best = d; lastProgress = Now; }
                    if (jumped || Now - lastProgress > Options.StuckSeconds) break; // landed below or stuck: retry
                    Driver.SetMove(c, dir, false);
                    yield return null;
                }
                if (c.Motor.IsGrounded && c.Feet.y > top.y - 0.25f) continue;
                Warn("ClimbOnto attempt " + (attempt + 1) + " failed at " + BotFmt.V(c.Feet) + " - backing off");
                Vector3 back = c.Feet - FlatDir(top - c.Feet, c.transform.forward) * 1.4f;
                if (c.Motor.StandingOn == null) yield return WalkCore(c, back, false, 0.3f, 4f, true, s);
                if (s.Ended) yield break;
            }
            if (!s.Ended) FailStep(s, "could not climb after 3 attempts (feet " + BotFmt.V(c.Feet) + ", ledge top y " + BotFmt.F(top.y) + ")", c);
        }

        bool WallAhead(ShifterCharacter c, Vector3 dir, float extra)
        {
            Vector3 o = c.Feet + Vector3.up * 0.3f;
            var hits = Physics.RaycastAll(o, dir, c.Radius + extra, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.transform.IsChildOf(c.transform)) continue;
                if (h.collider.GetComponentInParent<ShifterCharacter>() != null) continue;
                if (h.normal.y < 0.6f) return true;
            }
            return false;
        }

        /// <summary>top jumps onto bottom's head platform and settles (verified via Motor.StandingOn or feet height).
        /// NOTE: the head must be within jump reach (apex Tuning.JumpApexHeight above top's feet): the 1.5 m
        /// Brainiac is always reachable; 1.85-1.95 m heads may need PlaceOnto (carry + put down).</summary>
        public IEnumerator StackOnto(ShifterCharacter top, ShifterCharacter bottom, float timeout = 20f)
        {
            var s = Begin("StackOnto", top, "-> " + BotFmt.Name(bottom));
            if (!Ready(s, top) || !Ready(s, bottom)) yield break;
            Stop(bottom);
            float headY = bottom.HeadTop.y;
            float reach = top.Feet.y + Tuning.JumpApexHeight * (top.Motor != null ? top.Motor.JumpMultiplier : 1f);
            if (headY > reach + 0.1f) Warn("StackOnto: " + bottom.DisplayName + "'s head is " + BotFmt.F(headY - top.Feet.y) + " m up, jump apex " + BotFmt.F(Tuning.JumpApexHeight) + " m - may be unreachable");
            float t0 = Now;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Vector3 side = FlatDir(top.Feet - bottom.Feet, -bottom.transform.forward);
                Vector3 start = bottom.Feet + side * (Options.StackJumpDistance + 1.0f);
                yield return WalkCore(top, start, false, 0.3f, 10f, true, s);
                if (s.Ended) yield break;
                yield return FaceCore(top, bottom.Feet - top.Feet, 1f);
                // short run-up, jump at StackJumpDistance
                float tr = Now;
                while (Flat(bottom.Feet - top.Feet).magnitude > Options.StackJumpDistance)
                {
                    if (Now - tr > 3f) break;
                    Driver.SetMove(top, FlatDir(bottom.Feet - top.Feet, top.transform.forward) * 0.8f, false);
                    yield return null;
                }
                yield return JumpCore(top, FlatDir(bottom.Feet - top.Feet, top.transform.forward),
                    LandOnSteer(top, () => bottom.HeadTop, false), false, 3f, s);
                if (s.Ended) yield break;
                Stop(top);
                yield return Wait(0.4f);
                if (IsStandingOn(top, bottom)) { End(s, true, "head at y " + BotFmt.F(headY)); yield break; }
                if (Now - t0 > timeout) break;
                Warn("StackOnto attempt " + (attempt + 1) + " missed (feet " + BotFmt.V(top.Feet) + ", head " + BotFmt.F(headY) + ")");
            }
            FailStep(s, "could not land on " + bottom.DisplayName + "'s head (feet " + BotFmt.V(top.Feet) + ", head top y " + BotFmt.F(bottom.HeadTop.y) + ")", top);
        }

        /// <summary>Is top standing on bottom (motor StandingOn, or feet at head height right above it)?</summary>
        public static bool IsStandingOn(ShifterCharacter top, ShifterCharacter bottom)
        {
            if (top == null || bottom == null) return false;
            if (top.Motor != null && top.Motor.StandingOn == bottom) return true;
            return Mathf.Abs(top.Feet.y - bottom.HeadTop.y) < 0.25f && Flat(top.Feet - bottom.Feet).magnitude < bottom.Radius + 0.15f;
        }
    }
}
