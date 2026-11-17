using System.Collections;
using UnityEngine;
using static Shifters.PuzzleTestUtil;

namespace Shifters
{
    /// <summary>Campaign-wave PUZZLES checks (CAMPAIGN §18.5): light rule (pools, volumes, cone), BIT-SHIFTER sizes,
    /// NO-TOOL zones, window gates, one-at-a-time door, hold latches, heist pedestal, socket, call elevator,
    /// seat, folding ramp, signal, CAPS-LOCK freezes. Plus a gallery of the new parts.
    /// Run: devplay.sh &lt;proj&gt; puzzles_campaign -devSetup Shifters.PuzzleCampaignTests.All -devSeconds 120</summary>
    public static class PuzzleCampaignTests
    {
        class TestCone : ILightCone
        {
            public Vector3 Origin { get; set; }
            public Vector3 Forward { get; set; } = Vector3.forward;
            public float Range { get; set; } = Tuning.FlashlightRange;
            public float HalfAngle { get; set; } = Tuning.FlashlightHalfAngle;
            public bool LightOn { get; set; } = true;
        }

        public static IEnumerator All(DevPlayContext ctx)
        {
            yield return PuzzleTestRunner.Guard(ctx, "CampaignBehaviour", Run(ctx));
            yield return PuzzleTestRunner.Guard(ctx, "CampaignGallery", Gallery(ctx));
        }

        public static IEnumerator Run(DevPlayContext ctx)
        {
            var o = new Vector3(1400f, 0f, 0f);
            Floor(o + new Vector3(30f, 0f, 30f), new Vector2(110f, 110f));
            Look(o + new Vector3(10f, 16f, -16f), o + new Vector3(10f, 0f, 6f));

            // ================================================================ light rule
            var pool = PuzzleKit.LightPool(null, o, 3f);
            var sig = PuzzleKit.Signal(null, "PoolSwitch");
            var pool2 = PuzzleKit.LightPool(null, o + new Vector3(10f, 0f, 0f), 3f, false, sig);
            yield return null;
            ctx.Check(LightRegistry.IsLit(o + new Vector3(1.5f, 1f, 1.5f)), "light: point 2.1 m from a pool centre is lit");
            ctx.Check(!LightRegistry.IsLit(o + new Vector3(3.4f, 1f, 0f)), "light: 3.4 m from a 3 m pool is dark");
            ctx.Check(!LightRegistry.IsLit(o + new Vector3(0f, 5f, 0f)), "light: 5 m above the pool is dark");
            ctx.Check(!LightRegistry.IsLit(o + new Vector3(10f, 1f, 0f)), "light: pool wired to an OFF signal is dark");
            sig.Set(true);
            yield return null;
            ctx.Check(LightRegistry.IsLit(o + new Vector3(10f, 1f, 0f)) && pool2.LightOn, "light: pool turns lit when its input goes ON");
            sig.Set(false);
            yield return null;
            ctx.Check(!LightRegistry.IsLit(o + new Vector3(10f, 1f, 0f)), "light: ... and dark again when it goes OFF");

            // cone
            var co = o + new Vector3(0f, 1.5f, 10f);
            var cone = new TestCone { Origin = co, Forward = Vector3.forward };
            LightRegistry.RegisterCone(cone);
            LightRegistry.RegisterCone(cone); // idempotent
            ctx.Check(LightRegistry.ConeCount >= 1, "light: cone registered");
            ctx.Check(LightRegistry.IsLit(co + new Vector3(0f, 0f, 10f)), "cone: 10 m on-axis is lit");
            ctx.Check(LightRegistry.IsLit(co + Quaternion.Euler(0f, 20f, 0f) * new Vector3(0f, 0f, 10f)), "cone: 20 deg off-axis is lit");
            ctx.Check(!LightRegistry.IsLit(co + Quaternion.Euler(0f, 30f, 0f) * new Vector3(0f, 0f, 10f)), "cone: 30 deg off-axis is dark");
            ctx.Check(!LightRegistry.IsLit(co + new Vector3(0f, 0f, 15f)), "cone: 15 m (> 14 m range) is dark");
            var bystander = Sprinter(co + new Vector3(0f, -1.4f, 5f));
            yield return ctx.Wait(0.3f);
            ctx.Check(LightRegistry.IsLit(co + new Vector3(0f, 0f, 10f)), "cone: an intern in the beam does not block it");
            bystander.Motor.Teleport(o + new Vector3(-8f, 0.1f, -8f), Quaternion.identity);
            Kit.SolidBox(null, "ConeWall", co + new Vector3(0f, 0f, 6f), new Vector3(3f, 3f, 0.3f), MatKey.Concrete);
            yield return new WaitForFixedUpdate();
            ctx.Check(!LightRegistry.IsLit(co + new Vector3(0f, 0f, 10f)), "cone: a wall in between blocks the line of sight");
            ctx.Check(LightRegistry.IsLit(co + new Vector3(0f, 0f, 4f)), "cone: in front of the wall is still lit");
            cone.LightOn = false;
            ctx.Check(!LightRegistry.IsLit(co + new Vector3(0f, 0f, 4f)), "cone: LightOn=false = dark");
            LightRegistry.UnregisterCone(cone);

            // lit volumes: registered box, elevator car, holding cell
            var safe = Kit.Make("SafeRoom", null, o + new Vector3(20f, 0f, 0f));
            LightRegistry.RegisterVolume(safe.transform, new Vector3(0f, 1.5f, 0f), new Vector3(4f, 3f, 4f));
            ctx.Check(LightRegistry.IsLit(o + new Vector3(21f, 1f, 1f)) && !LightRegistry.IsLit(o + new Vector3(23f, 1f, 0f)), "light: registered volume lit inside, dark outside");
            var elev = PuzzleKit.Elevator(null, o + new Vector3(30f, 0.35f, 0f), null, 4f, new Vector2(3f, 3f));
            yield return null;
            ctx.Check(Elevator.All.Contains(elev) && LightRegistry.IsLit(o + new Vector3(30.5f, 1.2f, 0.5f)), "light: an elevator car counts as lit");
            var cell = EnemyKit.HoldingCell(null, o + new Vector3(40f, 0f, 0f), Quaternion.identity, "lit_test_cell");
            yield return null;
            var cellIn = cell.DropPoint != null ? cell.DropPoint.position + Vector3.up : cell.transform.position + Vector3.up;
            ctx.Check(LightRegistry.IsLit(cellIn), "light: a holding cell interior counts as lit");

            // ================================================================ BIT-SHIFTER (Shiftable)
            var so = o + new Vector3(0f, 0f, 30f);
            var crate = PuzzleKit.Crate(null, so);
            var sh = PuzzleKit.MakeShiftable(crate);
            yield return ctx.Wait(0.5f);
            string reason;
            float h0 = crate.transform.localScale.y;
            ctx.Check(sh.SizeClass == 0 && Mathf.Approximately(crate.mass, 25f), "shift: crate starts NORMAL 25 kg");
            bool ok = sh.TryCycleSize(null, out reason);
            yield return ctx.Wait(0.6f);
            ctx.Check(ok && sh.SizeClass == 1 && Mathf.Abs(crate.mass - 100f) < 0.01f && Mathf.Abs(crate.transform.localScale.y - h0 * Tuning.HugeScale) < 0.01f,
                "shift: HUGE = 100 kg, x1.6 (" + crate.mass + " kg, scale " + crate.transform.localScale.y.ToString("0.00") + ")");
            ctx.Check(crate.position.y > so.y + 0.6f, "shift: HUGE crate kept its bottom on the floor (centre y " + (crate.position.y - so.y).ToString("0.00") + ")");
            ok = sh.TryCycleSize(null, out reason);
            yield return ctx.Wait(0.6f);
            ctx.Check(ok && sh.SizeClass == -1 && Mathf.Abs(crate.mass - 6.25f) < 0.01f, "shift: TINY = 6.25 kg (" + crate.mass + ")");
            ok = sh.TryCycleSize(null, out reason);
            yield return ctx.Wait(0.6f);
            ctx.Check(ok && sh.SizeClass == 0 && Mathf.Abs(crate.mass - 25f) < 0.01f, "shift: back to NORMAL 25 kg");
            // clearance: a low ceiling over a second crate
            var c2 = PuzzleKit.Crate(null, so + new Vector3(5f, 0f, 0f));
            var s2 = PuzzleKit.MakeShiftable(c2);
            Kit.SolidBox(null, "LowCeiling", so + new Vector3(5f, 1.25f, 0f), new Vector3(3f, 0.3f, 3f), MatKey.Concrete);
            yield return ctx.Wait(0.5f);
            ok = s2.TryCycleSize(null, out reason);
            ctx.Check(!ok && reason == Shiftable.OverflowMessage && s2.SizeClass == 0, "shift: HUGE under a low ceiling refused: " + reason);
            // heavy block: HUGE unpushable 640 kg, TINY 40 kg carryable
            var block = PuzzleKit.HeavyBlock(null, so + new Vector3(10f, 0f, 0f));
            var sb = PuzzleKit.MakeShiftable(block.Body);
            yield return ctx.Wait(0.4f);
            sb.TryCycleSize(null, out reason);
            yield return ctx.Wait(0.5f);
            ctx.Check(sb.SizeClass == 1 && Mathf.Abs(block.Body.mass - 640f) < 0.1f && block.RequiredStrength == Shiftable.UnpushableStrength, "shift: HUGE heavy block = 640 kg PERMANENT FIXTURE (STR " + block.RequiredStrength + ")");
            sb.TryCycleSize(null, out reason);
            yield return ctx.Wait(0.5f);
            var bg = block.GetComponent<Grabbable>();
            ctx.Check(sb.SizeClass == -1 && Mathf.Abs(block.Body.mass - 40f) < 0.1f && bg != null && bg.CanBeGrabbed && !block.enabled, "shift: TINY heavy block = 40 kg, grabbable");
            var lifter = Brainiac(so + new Vector3(10f, 0.1f, -1.3f));
            yield return ctx.Wait(0.5f);
            bool grabbed = lifter.Grabber != null && lifter.Grabber.TryGrab(bg);
            yield return ctx.Wait(0.3f);
            ctx.Check(grabbed && bg.IsHeld, "shift: a STR-2 intern lifts the TINY heavy block");
            if (lifter.Grabber != null) lifter.Grabber.ForceRelease();
            lifter.Motor.Teleport(o + new Vector3(-6f, 0.1f, -10f), Quaternion.identity);
            yield return ctx.Wait(0.5f);
            sb.TryCycleSize(null, out reason);
            yield return ctx.Wait(0.5f);
            ctx.Check(sb.SizeClass == 0 && block.enabled && (bg == null || !bg.CanBeGrabbed) && Mathf.Abs(block.Body.mass - 160f) < 0.1f, "shift: heavy block back to NORMAL 160 kg pushable");
            // respawn resets to NORMAL
            sh.TryCycleSize(null, out reason);
            yield return ctx.Wait(0.5f);
            var resp = crate.GetComponent<Respawnable>();
            resp.Respawn();
            yield return ctx.Wait(1.6f);
            ctx.Check(sh.SizeClass == 0 && Mathf.Abs(crate.mass - 25f) < 0.01f && Mathf.Abs(crate.transform.localScale.y - h0) < 0.01f, "shift: respawn resets to NORMAL");

            // ================================================================ NO-TOOL zone
            var zo = o + new Vector3(30f, 0f, 30f);
            var zone = PuzzleKit.NoToolZone(null, zo + new Vector3(0f, 2f, 0f), new Vector3(6f, 4f, 6f), ToolMask.SwapOMatic | ToolMask.BitShifter);
            yield return null;
            ctx.Check(NoToolZone.Blocks(zo + Vector3.up, ToolMask.SwapOMatic) && NoToolZone.Blocks(zo + Vector3.up, ToolMask.BitShifter), "notool: inside blocks SWAP and SIZE");
            ctx.Check(!NoToolZone.Blocks(zo + Vector3.up, ToolMask.CapsLock), "notool: CAPS-LOCK not in the mask = allowed");
            ctx.Check(!NoToolZone.Blocks(zo + new Vector3(4f, 1f, 0f), ToolMask.SwapOMatic), "notool: outside = allowed");
            ctx.Check(NoToolZone.BlocksSegment(zo + new Vector3(-6f, 1f, 0f), zo + new Vector3(6f, 1f, 0f), ToolMask.SwapOMatic), "notool: a swap path crossing the zone is blocked");
            ctx.Check(!NoToolZone.BlocksSegment(zo + new Vector3(-6f, 1f, 5f), zo + new Vector3(6f, 1f, 5f), ToolMask.SwapOMatic), "notool: a path beside the zone is allowed");
            var zc = PuzzleKit.Crate(null, zo);
            var zs = PuzzleKit.MakeShiftable(zc);
            yield return ctx.Wait(0.4f);
            ok = zs.TryCycleSize(null, out reason);
            ctx.Check(!ok && reason == NoToolZone.Message && zs.SizeClass == 0, "notool: BIT-SHIFTER refused inside a NO-SIZE zone: " + reason);
            NoToolZone.Fizzle(zo + Vector3.up);
            ctx.Check(zone.Fizzles == 1, "notool: Fizzle counted on the zone");

            // ================================================================ WINDOW GATE
            var go = o + new Vector3(0f, 0f, 55f);
            var tb = PuzzleKit.TimedButton(null, go, null, 26f);
            var gate = PuzzleKit.WindowGate(null, go + new Vector3(0f, 0f, 10f), null, new Vector3(3f, 3f, 1f), tb);
            var gate2 = PuzzleKit.WindowGate(null, go + new Vector3(6f, 0f, 10f), null, new Vector3(3f, 3f, 1f), tb);
            var runner = Sprinter(go + new Vector3(0f, 0.1f, -3f));
            var late = Brute(go + new Vector3(6f, 0.1f, -3f));
            yield return ctx.Wait(0.4f);
            runner.Motor.Teleport(go + new Vector3(0f, 0.1f, 10f), Quaternion.identity);
            yield return ctx.Wait(0.4f);
            ctx.Check(!gate.IsOn, "gate: entering while no window runs does nothing");
            runner.Motor.Teleport(go + new Vector3(0f, 0.1f, -3f), Quaternion.identity);
            yield return ctx.Wait(0.3f);
            tb.Press(runner);
            float window = tb.CurrentWindow;
            yield return ctx.Wait(0.5f);
            ctx.Check(!gate.IsOn, "gate: window running, nobody in the gate = OFF");
            runner.Motor.Teleport(go + new Vector3(0f, 0.1f, 10f), Quaternion.identity);
            yield return ctx.Wait(0.3f);
            runner.Motor.Teleport(go + new Vector3(0f, 0.1f, 14f), Quaternion.identity);
            yield return ctx.Wait(0.3f);
            ctx.Check(gate.IsOn && gate.Passer == runner, "gate: run-through during the window = ON (and stays ON after leaving)");
            yield return WaitFor(() => !tb.IsOn, window + 1f);
            yield return ctx.Wait(0.1f);
            ctx.Check(!gate.IsOn, "gate: OFF when the window ends (window " + window.ToString("0.00") + " s)");
            late.Motor.Teleport(go + new Vector3(6f, 0.1f, 10f), Quaternion.identity);
            yield return ctx.Wait(0.4f);
            ctx.Check(!gate2.IsOn, "gate: a late runner after the window never turns it on");
            runner.Motor.Teleport(o + new Vector3(-10f, 0.1f, -10f), Quaternion.identity);
            late.Motor.Teleport(o + new Vector3(-12f, 0.1f, -10f), Quaternion.identity);

            // ================================================================ ONE AT A TIME DOOR
            var dO = o + new Vector3(20f, 0f, 55f);
            var allow = PuzzleKit.Signal(null, "DoorInput");
            allow.Set(true);
            var solo = PuzzleKit.OneAtATimeDoor(null, dO, null, new Vector3(3f, 3f, 3f), allow);
            var zc0 = dO + new Vector3(0f, 0.1f, solo.ZoneCenter.z);
            var a1 = Sprinter(zc0 + new Vector3(-0.6f, 0f, 0f));
            yield return ctx.Wait(0.5f);
            ctx.Check(solo.Count == 1 && solo.IsOn && solo.Door.Powered, "solo door: exactly 1 intern = opens");
            var a2 = Brainiac(zc0 + new Vector3(0.6f, 0f, 0f));
            yield return ctx.Wait(0.5f);
            ctx.Check(solo.Count == 2 && !solo.IsOn && !solo.Door.Powered && solo.Crowded >= 1, "solo door: 2 interns = closed (" + solo.Count + ")");
            a2.Motor.Teleport(o + new Vector3(-14f, 0.1f, -10f), Quaternion.identity);
            yield return ctx.Wait(0.5f);
            allow.Set(false);
            yield return ctx.Wait(0.2f);
            ctx.Check(solo.IsOn && !solo.Door.Powered, "solo door: 1 intern but input OFF = closed");
            a1.Motor.Teleport(o + new Vector3(-16f, 0.1f, -10f), Quaternion.identity);

            // ================================================================ HOLD LATCH (both modes)
            var lo = o + new Vector3(40f, 0f, 55f);
            var inA = PuzzleKit.Signal(null, "LatchInA");
            var inB = PuzzleKit.Signal(null, "LatchInB");
            var hold = PuzzleKit.HoldLatch(null, lo, null, inA, 1f, "HOLD TEST");
            var open = PuzzleKit.HoldLatch(null, lo + new Vector3(4f, 0f, 0f), null, inB, 1f, "OPEN TEST", HoldLatchMode.OpenThenLatch);
            yield return null;
            inA.Set(true); inB.Set(true);
            yield return ctx.Wait(0.4f);
            ctx.Check(!hold.IsOn && open.IsOn, "latch: after 0.4 s HoldThenLatch OFF, OpenThenLatch passes through ON");
            inA.Set(false); inB.Set(false);
            yield return ctx.Wait(0.2f);
            ctx.Check(!hold.IsOn && !open.IsOn && !open.IsLatched && hold.Held == 0f, "latch: released early = both OFF, timers reset");
            inA.Set(true); inB.Set(true);
            yield return ctx.Wait(1.3f);
            inA.Set(false); inB.Set(false);
            yield return ctx.Wait(0.3f);
            ctx.Check(hold.IsOn && hold.IsLatched && open.IsOn && open.IsLatched, "latch: held 1.3 s = both latched ON for good");

            // ================================================================ MIN WEIGHT ALARM + PROP SOCKET
            var ho = o + new Vector3(0f, 0f, 80f);
            var prism = PuzzleKit.Crate(null, ho + new Vector3(0f, 0.6f, 0f), null, true);   // 60 kg
            var pg = prism.GetComponent<Grabbable>();
            var alarm = PuzzleKit.MinWeightAlarm(null, ho, null, 50f, pg);
            var socket = PuzzleKit.PropSocket(null, ho + new Vector3(0f, 0f, 8f), null, pg);
            var wrong = PuzzleKit.Crate(null, ho + new Vector3(5f, 0f, 8f));
            var brute = Brute(ho + new Vector3(0f, 0.1f, -1.7f));
            yield return ctx.Wait(1f);
            ctx.Check(alarm.State == MinWeightAlarm.AlarmState.Resting && alarm.PropOnPedestal && !alarm.IsOn, "alarm: prism resting on the pedestal = calm");
            bool g1 = brute.Grabber.TryGrab(pg);
            yield return ctx.Wait(0.6f);
            ctx.Check(g1 && alarm.AlarmCount == 1 && alarm.IsOn, "alarm: Brute lifts it with nobody on the pedestal = ALARM (grab " + g1 + ")");
            ctx.Check(!pg.IsHeld, "alarm: prop force-released");
            yield return ctx.Wait(4.5f);
            ctx.Check(!alarm.IsOn && alarm.PropOnPedestal, "alarm: resets after 4 s, prism back on the pedestal");
            var guard = Sprinter(ho + new Vector3(0.5f, 0.75f, 0.5f));
            yield return ctx.Wait(1f);
            ctx.Check(alarm.LoadKg >= 50f, "alarm: Sprinter on the pedestal weighs " + alarm.LoadKg.ToString("0") + " kg");
            bool g2 = brute.Grabber.TryGrab(pg);
            yield return ctx.Wait(0.8f);
            ctx.Check(g2 && pg.IsHeld && alarm.AlarmCount == 1 && alarm.State == MinWeightAlarm.AlarmState.Swapped, "alarm: lift with a teammate on the pedestal = SWAPPED, no alarm (" + alarm.State + ")");
            guard.Motor.Teleport(ho + new Vector3(3f, 0.1f, -3f), Quaternion.identity);
            yield return ctx.Wait(0.5f);
            ctx.Check(alarm.AlarmCount == 1, "alarm: stepping off after the swap is fine");
            // socket: wrong prop does nothing, the prism snaps in
            Move(wrong, ho + new Vector3(0.2f, 1.2f, 8f));
            yield return ctx.Wait(0.8f);
            ctx.Check(!socket.IsOn, "socket: wrong prop = OFF");
            Move(wrong, ho + new Vector3(5f, 0.6f, 8f));
            brute.Grabber.ForceRelease();
            Move(prism, ho + new Vector3(0.3f, 1.4f, 8.2f));
            yield return ctx.Wait(0.8f);
            ctx.Check(socket.IsOn && socket.Seated == pg, "socket: the prism snaps in = ON");
            Move(prism, ho + new Vector3(3f, 1f, 4f));
            yield return ctx.Wait(0.4f);
            ctx.Check(!socket.IsOn, "socket: removed = OFF");
            foreach (var ch in new[] { brute, guard }) ch.Motor.Teleport(o + new Vector3(-18f - ch.Slot, 0.1f, -12f), Quaternion.identity);

            // ================================================================ CALL ELEVATOR (one passenger)
            var eo = o + new Vector3(25f, 0.35f, 80f);
            var ce = PuzzleKit.CallElevator(null, eo, 4f, new Vector2(3f, 3f), 200f, 1);
            var p1 = Sprinter(eo + new Vector3(-0.6f, 0.2f, -0.6f));
            var p2 = Brainiac(eo + new Vector3(0.6f, 0.2f, -0.6f));
            yield return ctx.Wait(1f);
            ce.Interact(p1);
            yield return null;
            ctx.Check(!ce.IsOn && ce.CountRefusals == 1, "elevator: 2 riders on a ONE (1) PASSENGER car = refused (riders " + ce.Riders + ")");
            p2.Motor.Teleport(o + new Vector3(-20f, 0.1f, -14f), Quaternion.identity);
            yield return ctx.Wait(0.8f);
            ce.Interact(p1);
            yield return ctx.Wait(3f);
            ctx.Check(ce.IsOn && ce.AtTop, "elevator: 1 rider = goes up (" + ce.Car.Position01.ToString("0.00") + ")");
            p1.Motor.Teleport(o + new Vector3(-22f, 0.1f, -14f), Quaternion.identity);
            yield return ctx.Wait(1f);
            yield return WaitFor(() => ce.AtBottom, 6f);
            ctx.Check(!ce.IsOn && ce.AtBottom, "elevator: empty car returns down");

            // ================================================================ SEAT + SIGNAL + FOLDING RAMP
            var to = o + new Vector3(45f, 0f, 80f);
            var lever = PuzzleKit.Signal(null, "SeatRelease");
            var seat = PuzzleKit.Seat(null, to, null, "SIT TO\nPOWER", lever, 10f);
            var sitter = Brute(to + new Vector3(0f, 0.2f, 1.6f));
            var helper = Sprinter(to + new Vector3(3f, 0.1f, 3f));
            yield return ctx.Wait(0.5f);
            ctx.Check(seat.SeatCharacter(sitter) && seat.IsOn && seat.IsOccupied, "seat: sitting powers it");
            yield return ctx.Wait(0.5f);
            lever.Set(true);
            yield return ctx.Wait(0.2f);
            ctx.Check(!seat.IsOccupied && seat.IsOn && seat.GraceRemaining > 0f && seat.ReleasedSignal != null && seat.ReleasedSignal.IsOn, "seat: release keeps it ON for the grace window, ReleasedSignal latched");
            yield return WaitFor(() => !seat.IsOn, 6f);
            ctx.Check(!seat.IsOn, "seat: OFF after the grace window");
            var rampSig = PuzzleKit.Signal(null, "RampSig");
            var ledge = Kit.SolidBox(null, "Ledge", to + new Vector3(10f, 1f, 0f), new Vector3(4f, 2f, 4f), MatKey.Concrete);
            var ramp = PuzzleKit.FoldingRamp(null, to + new Vector3(10f, 2f, -2f), 180f, 3f, 5f, 2f, false, "RAMP", rampSig);
            yield return ctx.Wait(0.3f);
            ctx.Check(!ramp.IsLowered, "ramp: raised while unpowered");
            rampSig.Set(true);
            yield return ctx.Wait(0.2f);
            rampSig.Set(false);
            yield return ctx.Wait(2f);
            ctx.Check(ramp.IsLowered, "ramp: latches lowered after one pulse");
            foreach (var ch in new[] { sitter, helper }) ch.Motor.Teleport(o + new Vector3(-24f - ch.Slot, 0.1f, -16f), Quaternion.identity);
            if (ledge == null) ctx.Log("ledge missing");

            // ================================================================ CAPS-LOCK (IFreezable)
            var fo = o + new Vector3(0f, 0f, 100f);
            var sweeper = PuzzleKit.SweeperArm(null, fo, null, 4f, 90f);
            var plat = PuzzleKit.MovingPlatform(null, fo + new Vector3(12f, 1f, 0f), null, new[] { Vector3.zero, new Vector3(6f, 0f, 0f) }, new Vector3(2f, 0.4f, 2f), 2f, 0f);
            var laser = PuzzleKit.LaserBarrier(null, fo + new Vector3(24f, 0f, 0f), null, 3f, 2, 1f, 1f);
            var fdoor = PuzzleKit.Door(null, fo + new Vector3(32f, 0f, 0f), null);
            yield return ctx.Wait(0.6f);
            IFreezable fz = sweeper.Arm.GetComponentInParent<IFreezable>();
            fz.Freeze(2.2f);
            yield return ctx.Wait(0.15f);   // let the last queued MoveRotation / interpolation settle
            float ang0 = sweeper.Arm.transform.eulerAngles.y;
            yield return ctx.Wait(1f);
            float ang1 = sweeper.Arm.transform.eulerAngles.y;
            ctx.Check(sweeper.IsFrozen && Mathf.Abs(Mathf.DeltaAngle(ang0, ang1)) < 0.5f, "caps: frozen sweeper arm stays put (via its MoverBody)");
            yield return ctx.Wait(1.3f);
            float ang2 = sweeper.Arm.transform.eulerAngles.y;
            ctx.Check(!sweeper.IsFrozen && Mathf.Abs(Mathf.DeltaAngle(ang1, ang2)) > 5f, "caps: sweeper resumes after the freeze");
            plat.Body.Freeze(1.5f);
            yield return ctx.Wait(0.15f);
            var pp0 = plat.Body.transform.position;
            yield return ctx.Wait(1f);
            ctx.Check(plat.Body.IsFrozen && (plat.Body.transform.position - pp0).magnitude < 0.02f, "caps: frozen moving platform pauses");
            bool laserState = laser.IsActive;
            laser.Freeze(3f);
            yield return ctx.Wait(2.2f);
            ctx.Check(laser.IsActive == laserState, "caps: frozen cycling laser keeps its state for 2.2 s");
            var dm = fdoor.Body;
            dm.Freeze(3f);
            ctx.Check(!dm.IsFrozen && dm.CapsProof, "caps: door mover is CAPS-PROOF");
        }

        // ================================================================ gallery
        public static IEnumerator Gallery(DevPlayContext ctx)
        {
            var o = new Vector3(1700f, 0f, 0f);
            Floor(o + new Vector3(16f, 0f, 8f), new Vector2(60f, 40f));
            var pool = PuzzleKit.LightPool(null, o, 3f, true);
            var offSig = PuzzleKit.Signal(null, "Off");
            PuzzleKit.LightPool(null, o + new Vector3(7.5f, 0f, 0f), 3f, false, offSig);
            var tb = PuzzleKit.TimedButton(null, o + new Vector3(14f, 0f, -3f), null, 26f);
            var gate = PuzzleKit.WindowGate(null, o + new Vector3(14f, 0f, 2f), null, new Vector3(3f, 3f, 1f), tb);
            var inS = PuzzleKit.Signal(null, "In");
            PuzzleKit.HoldLatch(null, o + new Vector3(19f, 0f, 0f), null, inS, 2f, "SECURITY LATCH");
            PuzzleKit.HoldLatch(null, o + new Vector3(21.5f, 0f, 0f), null, inS, 2f, "LOGIC LATCH", HoldLatchMode.OpenThenLatch);
            var solo = PuzzleKit.OneAtATimeDoor(null, o + new Vector3(28f, 0f, 3f), null, new Vector3(3f, 3f, 3f));
            var prism = PuzzleKit.Crate(null, o + new Vector3(0f, 0.6f, 9f), null, true);
            PuzzleKit.MinWeightAlarm(null, o + new Vector3(0f, 0f, 9f), null, 50f, prism.GetComponent<Grabbable>());
            PuzzleKit.PropSocket(null, o + new Vector3(5f, 0f, 9f), null, prism.GetComponent<Grabbable>());
            PuzzleKit.NoToolZone(null, o + new Vector3(11f, 2f, 10f), new Vector3(5f, 4f, 5f), ToolMask.SwapOMatic | ToolMask.BitShifter);
            var cN = PuzzleKit.Crate(null, o + new Vector3(16f, 0f, 9f));
            var cH = PuzzleKit.Crate(null, o + new Vector3(18f, 0f, 9f));
            var cT = PuzzleKit.Crate(null, o + new Vector3(20.5f, 0f, 9f));
            PuzzleKit.MakeShiftable(cN);
            var sH = PuzzleKit.MakeShiftable(cH);
            var sT = PuzzleKit.MakeShiftable(cT);
            PuzzleKit.CallElevator(null, o + new Vector3(26f, 0.35f, 11f), 4f, new Vector2(3f, 3f), 105f, 1);
            PuzzleKit.Seat(null, o + new Vector3(33f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f), "INSERT INTERN\nTO CONTINUE");
            var c1 = Sprinter(o + new Vector3(1f, 0.1f, -1f));
            var c2 = Brute(o + new Vector3(28f, 0.1f, 1.2f));
            yield return ctx.Wait(0.8f);
            sH.SetSize(1, false);
            sT.SetSize(-1, false);
            tb.Press(null);
            yield return ctx.Wait(0.5f);
            inS.Set(true);
            yield return ctx.Wait(0.8f);
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(16f, 13f, -14f), o + new Vector3(16f, 0f, 5f), 60f, "campaign_gallery_overview");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(3.5f, 4.5f, -8f), o + new Vector3(3.5f, 0.5f, 1f), 55f, "campaign_gallery_pools");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(18f, 3.5f, -6.5f), o + new Vector3(18f, 1.8f, 1f), 55f, "campaign_gallery_gate_latches");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(28f, 3f, -5f), o + new Vector3(28f, 2.2f, 2f), 55f, "campaign_gallery_solo_door");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(3f, 4f, 2f), o + new Vector3(3f, 0.6f, 9f), 55f, "campaign_gallery_heist");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(15.5f, 4.5f, 2.5f), o + new Vector3(15.5f, 0.8f, 9.5f), 60f, "campaign_gallery_notool_shift");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(30f, 5f, 3.8f), o + new Vector3(30f, 1f, 11f), 62f, "campaign_gallery_elevator_seat");
            if (pool == null || gate == null || solo == null || c1 == null || c2 == null) ctx.Log("gallery: missing part");
        }
    }
}
