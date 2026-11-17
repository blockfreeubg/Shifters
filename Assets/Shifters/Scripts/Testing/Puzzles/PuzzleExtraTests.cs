using System.Collections;
using UnityEngine;
using static Shifters.PuzzleTestUtil;

namespace Shifters
{
    /// <summary>Second batch of PUZZLES checks: range-plate limits, latching, target modes, one-shot levers,
    /// bridges, hazard toggles, steam vents, punching walls and the HUD timer event.
    /// Run alone: devplay.sh &lt;proj&gt; puzzles_extra -devSetup Shifters.PuzzleExtraTests.Run</summary>
    public static class PuzzleExtraTests
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            var o = new Vector3(900f, 0f, 0f);
            Floor(o + new Vector3(20f, 0f, 10f), new Vector2(80f, 50f));
            Look(o + new Vector3(10f, 14f, -16f), o + new Vector3(10f, 0f, 4f));

            // --- weight range plate 100-150: below / inside / too heavy
            var range = PuzzleKit.WeightRangePlate(null, o, null, 100f, 150f);
            var a = Brainiac(o + new Vector3(-0.45f, 0.2f, -0.45f));
            yield return ctx.Wait(1f);
            ctx.Check(!range.IsOn, "range 100-150: 70 kg is below (" + range.CurrentKg.ToString("0") + ")");
            var b = Sprinter(o + new Vector3(0.5f, 0.2f, -0.45f));
            yield return WaitFor(() => range.IsOn, 3f);
            ctx.Check(range.IsOn, "range 100-150: 130 kg is inside (" + range.CurrentKg.ToString("0") + ")");
            int toasts = 0;
            System.Action<string, Color> onToast = (s, c) => { if (s != null && s.Contains("Too heavy")) toasts++; };
            GameEvents.Toast += onToast;
            var c3 = Brute(o + new Vector3(0f, 0.2f, 0.5f));
            yield return WaitFor(() => !range.IsOn, 3f);
            yield return ctx.Wait(0.3f);
            GameEvents.Toast -= onToast;
            ctx.Check(!range.IsOn && toasts > 0, "range 100-150: 240 kg is too heavy (+toast x" + toasts + ")");
            foreach (var ch in new[] { a, b, c3 }) ch.Motor.Teleport(o + new Vector3(-6f, 0.1f, -6f + ch.Slot), Quaternion.identity);

            // --- latching plate stays on
            var latch = PuzzleKit.PressurePlate(null, o + new Vector3(5f, 0f, 0f), null, Tuning.PlateMedium, true);
            var m1 = PuzzleKit.Crate(null, o + new Vector3(5f, 0.3f, 0f), null, true);
            var m2 = PuzzleKit.Crate(null, o + new Vector3(5f, 1.7f, 0f), null, true);
            yield return WaitFor(() => latch.IsOn, 3f);
            Move(m1, o + new Vector3(5f, 1f, -6f)); Move(m2, o + new Vector3(7f, 1f, -6f));
            yield return ctx.Wait(1f);
            ctx.Check(latch.IsOn && latch.CurrentKg < 1f, "latching plate stays on after the weight leaves");

            // --- shoot target modes
            var mom = PuzzleKit.ShootTarget(null, o + new Vector3(10f, 0f, 0f), null, ShootTargetMode.Momentary, 1f);
            var tog = PuzzleKit.ShootTarget(null, o + new Vector3(12f, 0f, 0f), null, ShootTargetMode.Toggle);
            Shoot(mom, mom.transform.position + Vector3.up * 1.75f);
            Shoot(tog, tog.transform.position + Vector3.up * 1.75f);
            yield return null;
            ctx.Check(mom.IsOn && tog.IsOn, "momentary + toggle targets turn on when shot");
            yield return ctx.Wait(1.3f);
            Shoot(tog, tog.transform.position + Vector3.up * 1.75f);
            yield return null;
            ctx.Check(!mom.IsOn && !tog.IsOn, "momentary target times out, toggle target flips off on the 2nd shot");

            // --- one-shot lever
            var one = PuzzleKit.Lever(null, o + new Vector3(14f, 0f, 0f), null, 0, true);
            var puller = Sprinter(o + new Vector3(14f, 0.1f, 1f));
            yield return ctx.Wait(0.2f);
            one.Interact(puller);
            one.Interact(puller);
            ctx.Check(one.IsOn && one.Used && !one.CanInteract(puller, out _), "one-shot lever stays on and locks");

            // --- retractable bridge on a 3 s button (4 m at 3 m/s needs ~1.4 s to extend)
            var btn = PuzzleKit.PushButton(null, o + new Vector3(18f, 0f, 0f), null, 3f);
            var bridge = PuzzleKit.RetractableBridge(null, o + new Vector3(20f, 0.6f, 2f), null, 4f, 2f, btn);
            ctx.Check(bridge.Position01 < 0.01f, "bridge starts retracted");
            btn.Press();
            yield return WaitFor(() => bridge.Position01 > 0.99f, 3f);
            ctx.Check(bridge.Position01 > 0.99f, "bridge extends while the button is on");
            yield return WaitFor(() => bridge.Position01 < 0.01f, 6f);
            ctx.Check(bridge.Position01 < 0.01f && !btn.IsOn, "bridge retracts when the button times out");

            // --- hazard toggle: lever kills the laser
            var laser = PuzzleKit.LaserBarrier(null, o + new Vector3(26f, 0f, 0f), null, 3f, 2);
            var kill = PuzzleKit.Lever(null, o + new Vector3(26f, 0f, -3f));
            PuzzleKit.HazardToggle(null, new Behaviour[] { laser }, false, kill);
            yield return ctx.Wait(0.2f);
            ctx.Check(laser.IsActive, "laser active before the lever");
            kill.Flip(true);
            yield return ctx.Wait(0.3f);
            ctx.Check(!laser.IsActive, "hazard toggle switches the laser off");
            kill.Flip(false);
            yield return ctx.Wait(0.3f);
            ctx.Check(laser.IsActive, "laser back on when the lever is reset");

            // --- steam vent launches a crate
            var vent = PuzzleKit.SteamVent(null, o + new Vector3(32f, 0f, 0f), null, 4.5f, 1f, 1f);
            var vc = PuzzleKit.Crate(null, o + new Vector3(32f, 0.2f, 0f));
            float maxY = 0f;
            for (float t = 0f; t < 3f; t += Time.deltaTime) { maxY = Mathf.Max(maxY, vc.position.y); yield return null; }
            ctx.Check(vent.Launches > 0 && maxY > 2.5f, "steam vent launches a crate (" + maxY.ToString("0.0") + " m)");

            // --- cracked wall: weakling denied, Brute punches through
            var wall = PuzzleKit.CrackedWall(null, o + new Vector3(38f, 0f, 2f));
            var nerd = Brainiac(o + new Vector3(38f, 0.1f, 0.6f));
            var brute = Brute(o + new Vector3(36.6f, 0.1f, 0.6f));
            yield return ctx.Wait(0.3f);
            ctx.Check(!wall.CanInteract(nerd, out var why) && why != null && why.Contains("STR 7"), "weak punch refused: " + why);
            ctx.Check(wall.CanInteract(brute, out _), "Brute may punch the wall");
            wall.Interact(brute);
            ctx.Check(wall.IsBroken && !wall.Solid.enabled, "Brute punches through the cracked wall");

            // --- timed button raises the HUD countdown
            float lastTimer = -1f; int timerEvents = 0;
            System.Action<float> onTimer = s => { lastTimer = s; timerEvents++; };
            GameEvents.TimerChanged += onTimer;
            var tb = PuzzleKit.TimedButton(null, o + new Vector3(44f, 0f, 0f), null, 10f);
            tb.Press();
            yield return ctx.Wait(0.5f);
            bool counting = timerEvents > 3 && lastTimer > 0f && lastTimer < tb.Window;
            yield return WaitFor(() => !tb.IsOn, tb.Window + 1f);
            yield return ctx.Wait(0.2f);
            GameEvents.TimerChanged -= onTimer;
            ctx.Check(counting && lastTimer <= 0f, "timed button drives GameEvents.TimerChanged and hides it at the end (" + timerEvents + " events)");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(20f, 7f, -10f), o + new Vector3(20f, 0.5f, 2f), 60f, "extra_overview");
            ctx.Log("extra block done");
        }
    }
}
