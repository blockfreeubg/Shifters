// =====================================================================================
//  SHIFTERS - TeamBot.Abilities.cs   [BOTS module]
//  Grab / drop / throw (ballistic solve + landing check), living bridge, flop, interact (via
//  InteractionSensor), heavy-block pushing, shooting, taunting, volunteer chairs.
//  Everything goes through the abilities' public methods (contract APIs).
// =====================================================================================
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public sealed partial class TeamBot
    {
        // ------------------------------------------------------------------ grab / drop / throw
        /// <summary>Walk next to the target, face it and pick it up (CharacterGrabber.TryGrab). Retries once closer.</summary>
        public IEnumerator Grab(ShifterCharacter c, BotTarget target, float timeout = 15f)
        {
            var s = Begin("Grab", c, target.Describe());
            if (!Ready(s, c)) yield break;
            if (c.Grabber == null) { FailStep(s, "no CharacterGrabber", c); yield break; }
            var g = BotTargets.ResolveGrabbable(target);
            if (g == null) { FailStep(s, "no Grabbable at " + target.Describe(), c); yield break; }
            if (c.Grabber.Held == g) { End(s, true, "(already holding)"); yield break; }
            if (c.Grabber.IsHolding) { FailStep(s, "already holding " + c.Grabber.Held.name, c); yield break; }
            if (g.Mass > c.Grabber.Capacity) { FailStep(s, "TOO HEAVY: " + g.Mass.ToString("0") + " kg > capacity " + c.Grabber.Capacity.ToString("0") + " kg (STR " + (c.Stats != null ? c.Stats.Strength : 0) + ")", c); yield break; }
            if (g.Character != null) Stop(g.Character);
            float gap = 0.45f;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Vector3 gp = g.transform.position;
                float reach = c.Radius + BotTargets.RadiusOf(g) + gap;
                Vector3 approach = gp + FlatDir(c.Feet - gp, -c.transform.forward) * reach;
                yield return WalkCore(c, approach, false, 0.3f, timeout, true, s);
                if (s.Ended) yield break;
                yield return FaceCore(c, g.transform.position - c.Feet, 1f);
                bool ok = c.Grabber.TryGrab(g);
                if (!ok && attempt == 1) ok = c.Grabber.TryGrab();
                float t0 = Now;
                while (Now - t0 < 0.6f && c.Grabber.Held != g) yield return null;
                if (c.Grabber.Held == g) { End(s, true, "holding " + g.name + " " + g.Mass.ToString("0") + " kg"); yield break; }
                if (c.Grabber.IsHolding) { FailStep(s, "grabbed the wrong thing: " + c.Grabber.Held.name, c); yield break; }
                Warn("Grab attempt " + (attempt + 1) + " failed (TryGrab=" + ok + ", dist " + BotFmt.F(Flat(g.transform.position - c.Feet).magnitude) + " m) - retrying closer");
                gap = 0.15f;
            }
            FailStep(s, "TryGrab failed twice" + (BotReflect.IsStub(typeof(CharacterGrabber), "TryGrab", typeof(Grabbable)) ? " (CharacterGrabber is still a STUB)" : ""), c);
        }

        /// <summary>Put down what the character holds (CharacterGrabber.Drop).</summary>
        public IEnumerator Drop(ShifterCharacter c)
        {
            var s = Begin("Drop", c, null);
            if (!Ready(s, c)) yield break;
            if (c.Grabber == null || !c.Grabber.IsHolding) { End(s, true, "(nothing held)"); yield break; }
            var held = c.Grabber.Held;
            c.Grabber.Drop();
            float t0 = Now;
            while (Now - t0 < 1f && c.Grabber.IsHolding) yield return null;
            if (c.Grabber.IsHolding) { FailStep(s, "still holding after Drop()", c); yield break; }
            if (held != null && held.Character != null) yield return WaitSettled(held.Character, 3f);
            End(s, true, held != null ? "dropped " + held.name + " at " + BotFmt.V(held.transform.position) : null);
        }

        /// <summary>Throw what the character holds so it comes to rest on <paramref name="landing"/>. Without throwFrom the
        /// bot first walks (along the line from its position to the landing) to the spot from which a throw at
        /// Options.ThrowElevation touches down Options.ThrowSlideAllowance short of the landing (speed is fixed by STR and
        /// load, like a player). Stand on the line you want to throw along first. The elevation is solved inside the
        /// player's aim range (Tuning.ThrowBaseAngle..+25 = 38..63 deg) when possible; otherwise a wider lob is used with a
        /// WARN (or a failure with Options.StrictPlayerThrows) that says how far from the landing a player must stand.
        /// Verifies the rest position (planar within verifyRadius, height within 0.8 m). attempts &gt; 1: after a miss (thrown
        /// bodies tumble and can roll off) the thrower picks it up again (Grab) and re-throws, like a player would.</summary>
        public IEnumerator ThrowTo(ShifterCharacter c, BotTarget landing, BotTarget? throwFrom = null, float verifyRadius = 3f, int attempts = 1)
        {
            var s = Begin("ThrowTo", c, landing.Describe());
            if (!Ready(s, c)) yield break;
            if (c.Grabber == null || !c.Grabber.IsHolding) { FailStep(s, "not holding anything (Grab first)", c); yield break; }
            var held = c.Grabber.Held;
            for (int attempt = 1; ; attempt++)
            {
                _throwMiss = null; _throwNote = null;
                yield return ThrowAttempt(c, held, landing, throwFrom, verifyRadius, s);
                if (s.Ended) yield break;
                if (_throwMiss == null) { End(s, true, _throwNote + (attempt > 1 ? " (attempt " + attempt + ")" : "")); yield break; }
                if (attempt >= attempts) { FailStep(s, _throwMiss + (attempts > 1 ? " (" + attempts + " attempts)" : ""), held.Character != null ? held.Character : c); yield break; }
                // thrown bodies tumble chaotically: like a player, pick it up and throw again
                Warn("ThrowTo attempt " + attempt + "/" + attempts + " missed: " + _throwMiss + " - picking it up again");
                yield return Grab(c, held.Character != null ? (BotTarget)held.Character : (BotTarget)held);
                if (Failed) yield break;
                if (c.Grabber.Held != held) { FailStep(s, "could not pick up " + held.name + " again", c); yield break; }
            }
        }

        string _throwMiss, _throwNote;

        /// <summary>One throw (walk, solve, throw, wait, verify). Sets _throwMiss on a recoverable miss, _throwNote on success;
        /// hard problems (unreachable, Throw() ignored, thrown character lost) fail the step directly.</summary>
        IEnumerator ThrowAttempt(ShifterCharacter c, Grabbable held, BotTarget landing, BotTarget? throwFrom, float verifyRadius, BotStep s)
        {
            Vector3 target = landing.Position;
            // physical model (matches CharacterGrabber): the held body's CENTER flies; characters land lying down
            // (center ~radius above the surface) and get the motor's extra fall gravity.
            bool isChar = held.Character != null;
            float speed = ThrowSpeedOf(c, held) * Options.ThrowSpeedScale;
            float gUp = Mathf.Abs(Physics.gravity.y);
            float gDown = gUp * (Options.ThrowFallGravityMultiplier > 0f ? Options.ThrowFallGravityMultiplier : (isChar ? Tuning.FallGravityMultiplier : 1f));
            float centerUp = isChar ? held.Character.Height * 0.5f : HalfHeight(held);
            float slide = isChar ? Options.ThrowSlideAllowance : Options.ThrowSlideAllowance / 3f;
            Vector3 aimCenter = target - FlatDir(target - c.Feet, c.transform.forward) * slide
                                + Vector3.up * (isChar ? held.Character.Radius + 0.1f : HalfHeight(held));
            float releaseUp = held.transform.position.y + centerUp - c.Feet.y;
            float elev = Options.ThrowElevation;
            if (throwFrom.HasValue)
            {
                yield return WalkCore(c, throwFrom.Value, false, 0.3f, 20f, true, s);
            }
            else
            {
                float dh = aimCenter.y - (c.Feet.y + releaseUp);
                float d = BotBallistics.RangeForHeight(speed, elev, dh, gUp, gDown);
                if (float.IsNaN(d)) { FailStep(s, "target " + BotFmt.F(dh) + " m above the release point is out of reach (v=" + BotFmt.F(speed) + ")", c); yield break; }
                Vector3 spot = aimCenter - FlatDir(target - c.Feet, -c.transform.forward) * d;
                spot.y = c.Feet.y;
                Log("ThrowTo: v=" + BotFmt.F(speed) + " m/s elev " + elev + " deg -> throw spot " + BotFmt.V(spot) + " (" + BotFmt.F(d) + " m before target)");
                yield return WalkCore(c, spot, false, 0.25f, 25f, true, s);
            }
            if (s.Ended) yield break;
            yield return FaceCore(c, target - c.Feet, 1.2f);
            yield return WaitSettled(c, 0.6f);
            Vector3 release = held.transform.position + Vector3.up * centerUp;
            float dist = Flat(aimCenter - release).magnitude, rise = aimCenter.y - release.y;
            float pMin = Tuning.ThrowBaseAngle, pMax = Tuning.ThrowBaseAngle + 25f;
            // a given throw spot slightly out of this thrower's reach (weak MIN-team arm, heavy MAX-team passenger): step
            // along the throw line into the player-aimable band, like a player would (at most 1.5 m)
            if (throwFrom.HasValue && !BotBallistics.SolveElevation(speed, dist, rise, gUp, gDown, pMin, pMax, out elev, Mathf.Clamp(Options.ThrowElevation, pMin, pMax))
                && BotBallistics.RangeSpan(speed, pMin, pMax, rise, gUp, gDown, out float n0, out float f0) && f0 - n0 > 0.4f)
            {
                float want = Mathf.Clamp(dist, n0 + 0.2f, f0 - 0.2f), step = dist - want;
                if (Mathf.Abs(step) <= 1.5f)
                {
                    Vector3 spot = c.Feet + FlatDir(aimCenter - release, c.transform.forward) * step;
                    Log("ThrowTo: throw spot out of reach (" + BotFmt.F(dist) + " m, band " + BotFmt.F(n0) + ".." + BotFmt.F(f0) + ") - stepping " + BotFmt.F(step) + " m");
                    yield return WalkCore(c, spot, false, 0.15f, 8f, true, s);
                    if (s.Ended) yield break;
                    yield return FaceCore(c, target - c.Feet, 1.2f);
                    yield return WaitSettled(c, 0.6f);
                    release = held.transform.position + Vector3.up * centerUp;
                    dist = Flat(aimCenter - release).magnitude; rise = aimCenter.y - release.y;
                }
            }
            if (!BotBallistics.SolveElevation(speed, dist, rise, gUp, gDown, pMin, pMax, out elev, Mathf.Clamp(Options.ThrowElevation, pMin, pMax)))
            {
                string reach = BotBallistics.RangeSpan(speed, pMin, pMax, rise, gUp, gDown, out float near, out float far)
                    ? "a player's " + pMin.ToString("0") + ".." + pMax.ToString("0") + " deg throw (v " + BotFmt.F(speed) + ") touches down " + BotFmt.F(near) + ".." + BotFmt.F(far)
                      + " m away, but the aim point is " + BotFmt.F(dist) + " m away (stand " + BotFmt.F(near + slide) + ".." + BotFmt.F(far + slide) + " m from the landing)"
                    : "the landing is " + BotFmt.F(rise) + " m above the release point - out of a player's reach (v " + BotFmt.F(speed) + ")";
                if (Options.StrictPlayerThrows) { FailStep(s, "no player-feasible throw: " + reach, c); yield break; }
                if (!BotBallistics.SolveElevation(speed, dist, rise, gUp, gDown, 10f, 85f, out elev, Options.ThrowElevation))
                { FailStep(s, "no throw solution from " + BotFmt.V(release) + ": " + reach, c); yield break; }
                Warn("ThrowTo uses " + BotFmt.F(elev) + " deg - NOT aimable by a player: " + reach);
            }
            var dir = BotBallistics.Direction(aimCenter - release, elev);
            float flight = BotBallistics.FlightTime(speed, elev, rise, gUp, gDown);
            c.Grabber.Throw(dir);
            float t0 = Now;
            yield return null;
            yield return null;
            if (c.Grabber.Held == held) { FailStep(s, "Throw() did nothing" + (BotReflect.IsStub(typeof(CharacterGrabber), "Throw") ? " (CharacterGrabber is still a STUB)" : ""), c); yield break; }
            // wait for the landing
            while (Now - t0 < flight + 5f)
            {
                if (Now - t0 > flight * 0.8f + 0.2f)
                {
                    if (held.Character != null)
                    {
                        var hc = held.Character;
                        if (!hc.IsAlive || (hc.Condition == CharacterCondition.Normal && hc.Motor != null && hc.Motor.IsGrounded && !hc.Motor.IsTumbling)) break;
                    }
                    else if (held.Body == null || held.Body.IsSleeping() || held.Body.linearVelocity.magnitude < 0.25f) break;
                }
                yield return null;
            }
            Vector3 land = held.Character != null ? held.Character.Feet : held.transform.position;
            float err = Flat(land - target).magnitude, dy = land.y - target.y;
            string note = "elev " + BotFmt.F(elev) + " v " + BotFmt.F(speed) + " flight~" + BotFmt.F(flight) + "s landed " + BotFmt.V(land) + " err " + BotFmt.F(err) + " m dy " + BotFmt.F(dy);
            if (held.Character != null && !held.Character.IsAlive) { FailStep(s, held.Character.DisplayName + " is " + held.Character.State + " after the throw; " + note, held.Character); yield break; }
            if (err > verifyRadius || Mathf.Abs(dy) > 0.8f) { _throwMiss = "throw missed target " + BotFmt.V(target) + ": " + note; yield break; }
            _throwNote = note;
        }

        /// <summary>Launch speed the grabber will use (CharacterGrabber.ThrowSpeedFor when available: load-scaled).</summary>
        static float ThrowSpeedOf(ShifterCharacter c, Grabbable held)
        {
            if (BotReflect.TryCall(c.Grabber, "ThrowSpeedFor", out var r, held) && r is float f && f > 0f) return f;
            int str = c.Stats != null ? c.Stats.Strength : 5;
            float load = c.Grabber.Capacity > 0f ? Mathf.Clamp01(held.Mass / c.Grabber.Capacity) : 0f;
            return Tuning.ThrowSpeed(str) * Mathf.Lerp(1f, 0.8f, load);
        }

        static float HalfHeight(Component c)
        {
            var col = c != null ? c.GetComponentInChildren<Collider>() : null;
            return col != null ? col.bounds.extents.y : 0.4f;
        }

        /// <summary>Carry a teammate and put them down onto another teammate's head (for heads out of jump reach).</summary>
        public IEnumerator PlaceOnto(ShifterCharacter carrier, ShifterCharacter passenger, ShifterCharacter bottom)
        {
            var s = Begin("PlaceOnto", carrier, BotFmt.Name(passenger) + " -> " + BotFmt.Name(bottom));
            if (!Ready(s, carrier) || !Ready(s, passenger) || !Ready(s, bottom)) yield break;
            yield return Grab(carrier, passenger);
            if (Failed) yield break;
            Vector3 approach = bottom.Feet + FlatDir(carrier.Feet - bottom.Feet, -bottom.transform.forward) * (carrier.Radius + bottom.Radius + 0.35f);
            yield return WalkCore(carrier, approach, false, 0.25f, 15f, true, s);
            if (s.Ended) yield break;
            yield return FaceCore(carrier, bottom.Feet - carrier.Feet, 1f);
            carrier.Grabber.Drop();
            yield return Wait(1f);
            if (!IsStandingOn(passenger, bottom)) { FailStep(s, passenger.DisplayName + " did not end up on " + bottom.DisplayName + "'s head (feet " + BotFmt.V(passenger.Feet) + ")", passenger); yield break; }
            End(s);
        }

        // ------------------------------------------------------------------ bridge / flop
        /// <summary>Face the gap and become a living bridge (CharacterBridge.TryForm). Waits until the plank is in place.</summary>
        public IEnumerator FormBridge(ShifterCharacter c, Vector3 facingDir, float timeout = 2.5f)
        {
            var s = Begin("FormBridge", c, BotFmt.V(facingDir));
            if (!Ready(s, c)) yield break;
            if (c.Bridge == null) { FailStep(s, "no CharacterBridge", c); yield break; }
            yield return FaceCore(c, facingDir, 1.2f);
            Stop(c);
            yield return WaitSettled(c, 1f);
            if (!c.Bridge.CanForm(out string reason)) { FailStep(s, "CanForm=false: " + (reason ?? "(no reason given - CharacterBridge still a STUB?)"), c); yield break; }
            if (!c.Bridge.TryForm()) { FailStep(s, "TryForm returned false", c); yield break; }
            float t0 = Now;
            while (Now - t0 < timeout && !(c.Bridge.IsBridging && c.Condition == CharacterCondition.Bridging)) yield return null;
            yield return Wait(0.6f); // forming animation
            if (!c.Bridge.IsBridging) { FailStep(s, "did not become a bridge (condition " + c.Condition + ")", c); yield break; }
            End(s, true, "capacity " + c.Bridge.Capacity.ToString("0") + " kg");
        }

        /// <summary>Recover from bridge mode (snaps to the far end if there is ground). Waits until standing.</summary>
        public IEnumerator RecoverBridge(ShifterCharacter c, float timeout = 3f)
        {
            var s = Begin("RecoverBridge", c, null);
            if (s == null || c == null) { if (s != null) FailStep(s, "null character"); yield break; }
            if (c.Bridge == null || !c.Bridge.IsBridging) { End(s, true, "(not bridging)"); yield break; }
            Vector3 before = c.Feet;
            c.Bridge.Recover();
            float t0 = Now;
            while (Now - t0 < timeout && (c.Bridge.IsBridging || c.Condition == CharacterCondition.Bridging)) yield return null;
            if (c.Bridge.IsBridging) { FailStep(s, "still bridging after Recover()", c); yield break; }
            yield return WaitSettled(c, 2f);
            TakeControl(c);
            End(s, true, "now at " + BotFmt.V(c.Feet) + " (moved " + BotFmt.F(Flat(c.Feet - before).magnitude) + " m)");
        }

        /// <summary>Flop on/off (CharacterFlop.SetFlopped). Off waits until fully standing.</summary>
        public IEnumerator Flop(ShifterCharacter c, bool on, float timeout = 4f)
        {
            var s = Begin(on ? "Flop" : "Unflop", c, null);
            if (!Ready(s, c)) yield break;
            if (c.Flop == null) { FailStep(s, "no CharacterFlop", c); yield break; }
            Stop(c);
            c.Flop.SetFlopped(on);
            float t0 = Now;
            while (Now - t0 < timeout)
            {
                bool done = on ? c.Condition == CharacterCondition.Flopped
                               : c.Condition == CharacterCondition.Normal && (c.Motor == null || !c.Motor.IsTumbling);
                if (done) { End(s); yield break; }
                yield return null;
            }
            FailStep(s, "condition is " + c.Condition + " after SetFlopped(" + on + ")" + (BotReflect.IsStub(typeof(CharacterFlop), "SetFlopped") ? " (CharacterFlop still a STUB)" : ""), c);
        }

        // ------------------------------------------------------------------ interact
        /// <summary>Walk into range, face it and press E (InteractionSensor.TryInteract; falls back to IInteractable.Interact
        /// unless Options.StrictInteract). Waits while the character is Interacting (terminals: hack duration).</summary>
        public IEnumerator Interact(ShifterCharacter c, BotTarget target, BotTarget? standAt = null, bool waitWhileInteracting = true, float timeout = 20f)
        {
            var s = Begin("Interact", c, target.Describe());
            if (!Ready(s, c)) yield break;
            var it = BotTargets.ResolveInteractable(target);
            if (it == null) { FailStep(s, "no IInteractable at " + target.Describe(), c); yield break; }
            if (Options.AutoSwitchForInteract && CharacterManager.Instance != null && CharacterManager.Instance.Active != c)
            {
                yield return SwitchCore(c, 4f, s);
                if (s.Ended) yield break;
            }
            Vector3 ip = it.InteractPoint;
            Vector3 stand = standAt.HasValue ? standAt.Value.Position : ip + FlatDir(c.Feet - ip, -c.transform.forward) * Options.InteractDistance;
            yield return WalkCore(c, stand, false, 0.3f, timeout, true, s);
            if (s.Ended) yield break;
            yield return FaceCore(c, ip - c.Feet, 1f);
            if (!it.CanInteract(c, out string reason)) { FailStep(s, "CanInteract=false: " + (reason ?? "(prompt hidden)"), c); yield break; }
            string how = "sensor";
            bool done = false;
            if (c.Sensor != null)
            {
                float t0 = Now;
                while (Now - t0 < 0.5f && !ReferenceEquals(c.Sensor.Current, it)) yield return null;
                if (c.Sensor.Current != null && !ReferenceEquals(c.Sensor.Current, it)) Warn("InteractionSensor prefers " + BotFmt.Obj(c.Sensor.Current) + " over the target");
                if (ReferenceEquals(c.Sensor.Current, it)) done = c.Sensor.TryInteract();
            }
            if (!done)
            {
                if (Options.StrictInteract) { FailStep(s, "InteractionSensor did not interact with the target (StrictInteract)", c); yield break; }
                Warn("Interact fallback: IInteractable.Interact called directly (InteractionSensor " + (BotReflect.IsStub(typeof(InteractionSensor), "TryInteract") ? "is a STUB" : "did not pick the target") + ")");
                it.Interact(c);
                how = "direct";
            }
            if (waitWhileInteracting)
            {
                yield return WaitFrames(3);
                float t1 = Now, max = Tuning.HackDuration(c.Stats != null ? c.Stats.IQ : 5) + 4f;
                while (c.Condition == CharacterCondition.Interacting && Now - t1 < max) yield return null;
                if (c.Condition == CharacterCondition.Interacting) { FailStep(s, "still Interacting after " + BotFmt.F(max) + " s", c); yield break; }
            }
            End(s, true, "(" + how + ")");
        }

        /// <summary>Interact with a terminal (from standAt if given) and verify its signal turned on.</summary>
        public IEnumerator Hack(ShifterCharacter c, BotTarget terminal, BotTarget? standAt = null)
        {
            yield return Interact(c, terminal, standAt);
            if (Failed) yield break;
            yield return ExpectPowered(terminal, true, 1f);
        }

        /// <summary>Sit in a VolunteerChair-like IInteractable and verify Condition.Seated.</summary>
        public IEnumerator SitIn(ShifterCharacter c, BotTarget chair)
        {
            yield return Interact(c, chair, null, false);
            if (Failed) yield break;
            yield return ExpectCondition(c, CharacterCondition.Seated, 2f);
        }

        // ------------------------------------------------------------------ push
        /// <summary>Push a heavy Pushable block until its center reaches targetPoint (axis-aligned legs, dominant axis
        /// first). The bot walks around the block to the far side of each push axis.</summary>
        public IEnumerator PushBlock(ShifterCharacter c, BotTarget block, BotTarget targetPoint, float tolerance = 0.3f, float timeout = 60f)
        {
            var s = Begin("PushBlock", c, block.Describe() + " -> " + targetPoint.Describe());
            if (!Ready(s, c)) yield break;
            var tr = block.Transform;
            Rigidbody rb = null;
            if (tr != null) { rb = tr.GetComponentInParent<Rigidbody>(); if (rb == null) rb = tr.GetComponentInChildren<Rigidbody>(); }
            var col = rb != null ? rb.GetComponentInChildren<Collider>() : (tr != null ? tr.GetComponentInChildren<Collider>() : null);
            if (col == null) { FailStep(s, "block has no collider/rigidbody: " + block.Describe(), c); yield break; }
            Transform bt = rb != null ? rb.transform : col.transform;
            object pushable = BotReflect.FindComponent(bt.gameObject, "Pushable");
            int required = BotReflect.TryGet<int>(pushable, "RequiredStrength", out var rs) ? rs : 0;
            if (required > 0 && c.Stats != null && c.Stats.Strength < required) Warn(c.DisplayName + " has STR " + c.Stats.Strength + " < required " + required);
            Vector3 goal = targetPoint.Position;
            float t0 = Now;
            for (int leg = 0; leg < 4; leg++)
            {
                Vector3 delta = Flat(goal - bt.position);
                if (delta.magnitude <= tolerance) break;
                Vector3 axis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z) ? new Vector3(Mathf.Sign(delta.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(delta.z));
                if (Vector3.Dot(delta, axis) <= tolerance) break;
                yield return PushLeg(c, bt, col, axis, goal, tolerance, t0 + timeout, s);
                if (s.Ended) yield break;
            }
            float err = Flat(goal - bt.position).magnitude;
            if (err > tolerance + 0.4f) { FailStep(s, "block ended " + BotFmt.F(err) + " m from the goal at " + BotFmt.V(bt.position), c); yield break; }
            End(s, true, "block at " + BotFmt.V(bt.position) + " err " + BotFmt.F(err) + " m");
        }

        IEnumerator PushLeg(ShifterCharacter c, Transform bt, Collider col, Vector3 axis, Vector3 goal, float tolerance, float deadline, BotStep s)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, axis);
            Vector3 ext = col.bounds.extents;
            float extAlong = Mathf.Abs(Vector3.Dot(ext, new Vector3(Mathf.Abs(axis.x), 0f, Mathf.Abs(axis.z))));
            float extSide = Mathf.Abs(Vector3.Dot(ext, new Vector3(Mathf.Abs(perp.x), 0f, Mathf.Abs(perp.z))));
            // --- go around to the far side
            Vector3 bp = bt.position;
            Vector3 rel = Flat(c.Feet - bp);
            float along = Vector3.Dot(rel, axis), side = Vector3.Dot(rel, perp);
            float lane = (Mathf.Abs(side) < 0.05f ? 1f : Mathf.Sign(side)) * (extSide + c.Radius + 0.7f);
            float behind = extAlong + c.Radius + 0.3f;
            Vector3 Ground(Vector3 p) { p.y = c.Feet.y; return p; }
            if (along > -behind + 0.2f)
            {
                if (Mathf.Abs(side) < extSide + c.Radius + 0.3f)
                {
                    yield return WalkCore(c, Ground(bp + perp * lane + axis * Mathf.Clamp(along, -behind, extAlong + c.Radius + 0.8f)), false, 0.5f, 15f, false, s);
                    if (s.Ended) yield break;
                }
                yield return WalkCore(c, Ground(bp + perp * lane - axis * (behind + 0.4f)), false, 0.5f, 15f, false, s);
                if (s.Ended) yield break;
            }
            yield return WalkCore(c, Ground(bt.position - axis * behind), false, 0.25f, 15f, true, s);
            if (s.Ended) yield break;
            yield return FaceCore(c, axis, 1f);
            // --- push
            float goalAlong = Vector3.Dot(goal, axis);
            float lastAlong = Vector3.Dot(bt.position, axis), lastMove = Now;
            while (true)
            {
                var b = Blocker(c);
                if (b == WaitToken) { Stop(c); yield return null; continue; }
                if (b != null) { FailStep(s, b, c); yield break; }
                float cur = Vector3.Dot(bt.position, axis);
                float remain = goalAlong - cur;
                if (remain <= tolerance) break;
                if (cur > lastAlong + 0.05f) { lastAlong = cur; lastMove = Now; }
                else if (Now - lastMove > 2.5f)
                {
                    Stop(c);
                    FailStep(s, "block stopped moving at " + BotFmt.V(bt.position) + " (" + BotFmt.F(remain) + " m to go; STR " + (c.Stats != null ? c.Stats.Strength : 0) + ", pusher at " + BotFmt.V(c.Feet) + ")", c);
                    yield break;
                }
                if (Now > deadline) { Stop(c); FailStep(s, "push timeout", c); yield break; }
                float sideOff = Vector3.Dot(Flat(c.Feet - bt.position), perp);
                Vector3 move = axis + perp * Mathf.Clamp(-sideOff * 1.5f, -0.6f, 0.6f);
                Driver.SetMove(c, move.normalized * (remain < 0.6f ? 0.55f : 1f), false);
                yield return null;
            }
            Stop(c);
            yield return Wait(0.3f);
            yield return WalkCore(c, Ground(c.Feet - axis * 0.7f), false, 0.25f, 3f, true, s);
        }

        // ------------------------------------------------------------------ weapon / taunt
        /// <summary>Face the target and fire (WeaponHolder.FireAt, respects fire rate) until until() is true
        /// (default: the target's SignalSource is on, else one shot), up to maxShots. TOOL-AWARE (campaign wave):
        /// LEAF BLOWER = trigger held (FireAt every frame) until until() or for hold seconds (default 1.5 s, maxShots
        /// ignored); tools / T-SHIRT CANNON wait their cooldown between shots (ShotInterval) and the timeout grows to fit
        /// maxShots cooldowns; without until() a tool shot counts as done 0.6 s after it fired.</summary>
        public IEnumerator ShootAt(ShifterCharacter c, BotTarget target, Func<bool> until = null, int maxShots = 8, float timeout = 12f, float hold = 0f)
        {
            var s = Begin("ShootAt", c, target.Describe());
            if (!Ready(s, c)) yield break;
            if (c.Weapon == null || !c.Weapon.HasWeapon) { FailStep(s, c.DisplayName + " has no weapon (gun owner: " + BotFmt.Name(GunOwner) + ")", c); yield break; }
            Vector3 aim = BotTargets.AimPoint(target);
            if (until == null)
            {
                var src = BotTargets.ResolveSource(target.Transform);
                if (src != null) until = () => src.IsOn;
            }
            var data = c.Weapon.Data;
            bool continuous = IsContinuous(data), tool = IsTool(data);
            float interval = ShotInterval(data);
            if (!continuous) timeout = Mathf.Max(timeout, interval * maxShots + 2f);
            if (continuous && hold <= 0f && until == null) hold = 1.5f;
            float range = data != null ? data.Range : 20f;
            if (continuous) range = Mathf.Min(range, 8f);   // the blower's cone reaches 8 m (§13.2)
            if (Vector3.Distance(c.Center, aim) > range * 0.85f)
            {
                Vector3 closer = aim + FlatDir(c.Feet - aim, -c.transform.forward) * (range * 0.6f);
                closer.y = c.Feet.y;
                yield return WalkCore(c, closer, false, 0.8f, 20f, true, s);
                if (s.Ended) yield break;
            }
            yield return FaceCore(c, aim - c.Feet, 1f);
            int shots = 0;
            float t0 = Now, lastShot = -99f;
            float settle = tool ? 0.6f : 0.4f;
            while (true)
            {
                if (s.Ended) yield break;
                if (until != null && until()) break;
                if (continuous)
                {
                    if (hold > 0f && Now - t0 >= hold) break;
                    if (Now - t0 > timeout) { FailStep(s, "target not satisfied after holding the trigger " + timeout + " s", c); yield break; }
                    if (aim != BotTargets.AimPoint(target)) aim = BotTargets.AimPoint(target);
                    Driver.SetFace(c, aim - c.Feet);
                    if (c.Weapon.FireAt(aim)) shots++;
                    yield return null;
                    continue;
                }
                if (until == null && shots > 0 && Now - lastShot > settle) break;
                if (Now - t0 > timeout)
                {
                    FailStep(s, shots == 0 ? "FireAt never fired" + (BotReflect.IsStub(typeof(WeaponHolder), "FireAt") ? " (WeaponHolder still a STUB)" : "") : "target not satisfied after " + shots + " shot(s)", c);
                    yield break;
                }
                Driver.SetFace(c, aim - c.Feet);
                bool ready = !tool || Now - lastShot >= interval;
                if (shots < maxShots && ready && c.Weapon.FireAt(aim)) { shots++; lastShot = Now; }
                else if (shots >= maxShots && Now - lastShot > Mathf.Max(1.5f, interval + 0.5f)) { FailStep(s, "target not satisfied after " + shots + " shots", c); yield break; }
                yield return null;
            }
            Driver.SetFace(c, Vector3.zero);
            End(s, true, (continuous ? "held " + BotFmt.F(Now - t0) + "s (" + shots + " ticks)" : shots + " shot(s)") + (tool ? " [tool " + data.Variant + "]" : ""));
        }

        /// <summary>Taunt (distraction noise). Uses a Taunt()/TryTaunt() method on any character component when one
        /// exists, else plays the taunt gesture and raises the taunt noise directly (logged).</summary>
        public IEnumerator Taunt(ShifterCharacter c)
        {
            var s = Begin("Taunt", c, null);
            if (!Ready(s, c)) yield break;
            string how = null;
            foreach (var mb in c.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var m = mb.GetType().GetMethod("Taunt", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null)
                     ?? mb.GetType().GetMethod("TryTaunt", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (m == null) continue;
                var r = m.Invoke(mb, null);
                if (!(r is bool ok) || ok) { how = mb.GetType().Name + "." + m.Name; break; }
            }
            if (how == null)
            {
                Warn("no Taunt() method on " + c.DisplayName + " - raising the taunt noise directly");
                if (c.Visual != null) c.Visual.Play(VisualAction.Taunt);
                GameEvents.RaiseNoise(c.Center, Tuning.NoiseTaunt, c, 1f);
                how = "noise fallback";
            }
            yield return Wait(0.8f);
            End(s, true, "(" + how + ")");
        }
    }
}
