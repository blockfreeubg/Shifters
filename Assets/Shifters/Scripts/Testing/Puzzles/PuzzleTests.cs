using System.Collections;
using UnityEngine;
using static Shifters.PuzzleTestUtil;

namespace Shifters
{
    /// <summary>
    /// PUZZLES DevPlay scenarios. Run: devplay.sh &lt;proj&gt; puzzles_all -devSetup Shifters.PuzzleTests.All
    /// Logic (props only), Characters (works with the stub motor too) and the puzzle zoo screenshots.
    /// </summary>
    public static class PuzzleTests
    {
        public static IEnumerator All(DevPlayContext ctx)
        {
            yield return Logic(ctx);
            yield return Characters(ctx);
            yield return PuzzleZoo.Run(ctx);
        }

        // ================================================================== logic / props
        public static IEnumerator Logic(DevPlayContext ctx)
        {
            var o = new Vector3(300f, 0f, 0f);
            Floor(o + new Vector3(20f, 0f, 20f), new Vector2(90f, 90f));
            Look(o + new Vector3(0f, 12f, -14f), o);

            // --- plate + door with crates (100 kg plate)
            var plate = PuzzleKit.PressurePlate(null, o, null, Tuning.PlateMedium);
            var door = PuzzleKit.Door(null, o + new Vector3(0f, 0f, 6f), null, plate);
            PuzzleKit.CableBetween(null, plate, door.transform);
            yield return ctx.Wait(0.3f);
            ctx.Check(!plate.IsOn && door.IsClosed, "empty 100kg plate: off, door closed");
            var c1 = PuzzleKit.Crate(null, o + Vector3.up * 0.3f, null, true);
            var c2 = PuzzleKit.Crate(null, o + new Vector3(0f, 1.6f, 0f));
            yield return ctx.Wait(1.5f);
            ctx.Check(!plate.IsOn, "85 kg < 100: still off (" + plate.CurrentKg.ToString("0") + " kg)");
            var c3 = PuzzleKit.Crate(null, o + new Vector3(0f, 2.6f, 0f));
            yield return WaitFor(() => door.IsOpen, 4f);
            ctx.Check(plate.IsOn && door.IsOpen, "stacked 110 kg opens the door (" + plate.CurrentKg.ToString("0") + " kg, open " + door.Position01.ToString("0.00") + ")");
            Move(c1, o + new Vector3(8f, 1f, -3f)); Move(c2, o + new Vector3(10f, 1f, -3f)); Move(c3, o + new Vector3(12f, 1f, -3f));
            yield return WaitFor(() => door.IsClosed, 5f);
            ctx.Check(!plate.IsOn && door.IsClosed, "door closes when the weight leaves");

            // --- heavy block on a 150 plate
            var p150 = PuzzleKit.PressurePlate(null, o + new Vector3(6f, 0f, 0f), null, Tuning.PlateHeavy);
            PuzzleKit.HeavyBlock(null, o + new Vector3(6f, 0.3f, 0f));
            yield return WaitFor(() => p150.IsOn, 3f);
            ctx.Check(p150.IsOn, "160 kg heavy block satisfies 150 kg plate (" + p150.CurrentKg.ToString("0") + ")");

            // --- door never closes on a prop
            var lever = PuzzleKit.Lever(null, o + new Vector3(14f, 0f, 0f));
            var door2 = PuzzleKit.Door(null, o + new Vector3(14f, 0f, 6f), null, lever);
            lever.Flip(true);
            yield return WaitFor(() => door2.IsOpen, 4f);
            var blocker = PuzzleKit.Crate(null, o + new Vector3(14f, 0f, 6f), null, true);
            yield return ctx.Wait(0.8f);
            lever.Flip(false);
            yield return ctx.Wait(4f);
            ctx.Check(door2.BlockedCount > 0 && door2.Position01 > 0.2f, "door pauses on a crate in the doorway (blocked " + door2.BlockedCount + ", open " + door2.Position01.ToString("0.00") + ")");
            Move(blocker, o + new Vector3(18f, 1f, -3f));
            yield return WaitFor(() => door2.IsClosed, 5f);
            ctx.Check(door2.IsClosed, "door closes after the crate is removed");

            // --- sequence lock B, A, C
            var bA = PuzzleKit.PushButton(null, o + new Vector3(0f, 0f, -8f), null, 0.4f);
            var bB = PuzzleKit.PushButton(null, o + new Vector3(2f, 0f, -8f), null, 0.4f);
            var bC = PuzzleKit.PushButton(null, o + new Vector3(4f, 0f, -8f), null, 0.4f);
            var seq = PuzzleKit.SequenceLock(null, o + new Vector3(2f, 0f, -11f), null, new SignalSource[] { bB, bA, bC });
            bA.Press(); yield return ctx.Wait(0.6f);
            ctx.Check(!seq.IsOn && seq.Mistakes == 1 && seq.Progress == 0, "sequence: wrong first input resets (+buzz/toast)");
            bB.Press(); yield return ctx.Wait(0.6f);
            bA.Press(); yield return ctx.Wait(0.6f);
            ctx.Check(seq.Progress == 2 && !seq.IsOn, "sequence: progress 2/3");
            bC.Press(); yield return ctx.Wait(0.3f);
            ctx.Check(seq.IsOn, "sequence: correct order unlocks");

            // --- logic gates
            var l1 = PuzzleKit.Lever(null, o + new Vector3(8f, 0f, -8f));
            var l2 = PuzzleKit.Lever(null, o + new Vector3(10f, 0f, -8f));
            var and = PuzzleKit.LogicGate(null, o + new Vector3(9f, 0f, -11f), null, SignalLogic.All, false, l1, l2);
            var not = PuzzleKit.LogicGate(null, o + new Vector3(12f, 0f, -11f), null, SignalLogic.All, true, l1);
            var xor = PuzzleKit.LogicGate(null, o + new Vector3(14f, 0f, -11f), null, SignalLogic.Xor, false, l1, l2);
            yield return null;
            ctx.Check(!and.IsOn && not.IsOn && !xor.IsOn, "gates idle: AND off, NOT on, XOR off");
            l1.Flip(true); yield return null;
            ctx.Check(!and.IsOn && !not.IsOn && xor.IsOn, "one lever: AND off, NOT off, XOR on");
            l2.Flip(true); yield return null;
            ctx.Check(and.IsOn && !xor.IsOn, "both levers: AND on, XOR off");

            // --- timed button window + door closes after it
            var tb = PuzzleKit.TimedButton(null, o + new Vector3(20f, 0f, -8f), null, 24f, new[] { o + new Vector3(20f, 0f, -7f), o + new Vector3(20f, 0f, 5f), o + new Vector3(26f, 0f, 5f) });
            var tdoor = PuzzleKit.Door(null, o + new Vector3(26f, 0f, -2f), null, tb);
            float expected = Tuning.TimedWindow(24f, 7f);
            ctx.Check(Mathf.Abs(tb.Window - expected) < 0.01f, "timed window matches Tuning formula (" + tb.Window.ToString("0.00") + " s)");
            ctx.Check(tdoor.Timer == tb && tdoor.RingLamps.Length == 12, "door got a 12-lamp countdown ring");
            tb.Press();
            yield return WaitFor(() => tdoor.IsOpen, 3f);
            ctx.Check(tdoor.IsOpen && tb.IsOn, "timed door opens on press");
            yield return ctx.Wait(expected + 0.2f);
            ctx.Check(!tb.IsOn, "timer expired after the window");
            yield return WaitFor(() => tdoor.IsClosed, 4f);
            ctx.Check(tdoor.IsClosed, "timed door closes after the window");

            // --- shoot target, glass
            var st = PuzzleKit.ShootTarget(null, o + new Vector3(30f, 0f, -8f));
            Shoot(st, st.transform.position + Vector3.up * 1.75f);
            yield return null;
            ctx.Check(st.IsOn && st.Hits == 1, "shoot target latches on OnShot");
            var glass = PuzzleKit.GlassPane(null, o + new Vector3(34f, 0f, -8f));
            Shoot(glass, glass.transform.position + Vector3.up * 1.5f);
            yield return ctx.Wait(0.2f);
            ctx.Check(glass.IsBroken && !glass.Solid.enabled, "glass breaks on OnShot (collider off, source on)");
            var wall = PuzzleKit.CrackedWall(null, o + new Vector3(38f, 0f, -8f));
            for (int i = 0; i < 3; i++) Shoot(wall, wall.transform.position + Vector3.up);
            ctx.Check(!wall.IsBroken, "cracked wall survives 3 shots");
            Shoot(wall, wall.transform.position + Vector3.up);
            ctx.Check(wall.IsBroken, "cracked wall breaks on the 4th shot");

            // --- respawnable crate + kill zone (acid) for props
            var rc = PuzzleKit.Crate(null, o + new Vector3(0f, 0f, 14f));
            yield return ctx.Wait(0.5f);
            var home = rc.position;
            Move(rc, new Vector3(o.x, -70f, 14f));
            yield return ctx.Wait(1.6f);
            ctx.Check((rc.position - home).magnitude < 0.3f && rc.GetComponent<Respawnable>().RespawnCount == 1, "respawnable crate returns home after the void");
            PuzzleKit.KillZone(null, o + new Vector3(5f, 0f, 14f), null, new Vector3(3f, 3f, 3f), KillZoneType.Acid);
            var ac = PuzzleKit.Crate(null, o + new Vector3(8.5f, 0f, 14f));
            yield return ctx.Wait(0.4f);
            var acHome = ac.position;
            Move(ac, o + new Vector3(5f, -1f, 14f));
            yield return ctx.Wait(1.6f);
            ctx.Check((ac.position - acHome).magnitude < 0.3f, "crate dropped in acid respawns home");

            // --- bounce pad launches a crate >= 4 m
            var pad = PuzzleKit.BouncePad(null, o + new Vector3(12f, 0f, 14f), null, 5f);
            var bc = PuzzleKit.Crate(null, o + new Vector3(12f, 1.5f, 14f));
            float maxY = 0f;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime) { maxY = Mathf.Max(maxY, bc.position.y); yield return null; }
            ctx.Check(maxY - 0.45f - pad.transform.position.y >= 4f && pad.Launches > 0, "bounce pad launch height >= 4 m (" + (maxY - 0.45f).ToString("0.0") + ")");

            // --- pulley: heavier side goes down
            var pulley = PuzzleKit.PulleyLift(null, o + new Vector3(24f, 0f, 14f), null, 6f, 3f);
            ctx.Check(Mathf.Approximately(pulley.Offset, 1.5f) || pulley.Offset == 0f, "pulley starts A up");
            yield return ctx.Wait(0.3f);
            var pa = pulley.PlatformA.transform.position;
            PuzzleKit.Crate(null, pa + Vector3.up * 0.3f, null, true);
            yield return WaitFor(() => pulley.Offset <= -1.49f, 6f);
            ctx.Check(pulley.Offset <= -1.49f, "pulley: loaded side A went down (offset " + pulley.Offset.ToString("0.00") + ", A " + pulley.MassA.ToString("0") + " kg)");

            // --- conveyor moves a crate
            var conv = PuzzleKit.Conveyor(null, o + new Vector3(34f, 0.4f, 14f), null, 8f, 2f, 2.5f);
            var cc = PuzzleKit.Crate(null, o + new Vector3(34f, 0.45f, 11f));
            yield return ctx.Wait(0.5f);
            float z0 = cc.position.z;
            yield return ctx.Wait(1.5f);
            ctx.Check(cc.position.z - z0 > 1.5f, "conveyor drags a crate (" + (cc.position.z - z0).ToString("0.0") + " m)");
            ctx.Check(conv.GetSurfaceVelocity(Vector3.zero).z > 2f, "conveyor surface velocity for characters");

            // --- moving platform: kinematic velocity probe + carried crate
            var mp = PuzzleKit.MovingPlatform(null, o + new Vector3(0f, 1f, 26f), null, new[] { Vector3.zero, new Vector3(0f, 0f, 8f) }, new Vector3(3f, 0.4f, 3f), 2.5f, 0.5f);
            var rider = PuzzleKit.Crate(null, o + new Vector3(0f, 1.05f, 26f));
            yield return ctx.Wait(1.2f);
            var kv = mp.Body.Body.linearVelocity;
            ctx.Log("PROBE kinematic MovePosition velocity reported by PhysX: " + kv.ToString("0.00") + " (mover " + mp.Body.Velocity.ToString("0.00") + ")");
            float drift0 = (rider.position - mp.Body.transform.position).magnitude;
            yield return ctx.Wait(6f);
            float drift1 = (rider.position - mp.Body.transform.position).magnitude;
            ctx.Check(Mathf.Abs(drift1 - drift0) < 0.3f && rider.position.y > 1f, "crate rides the moving platform (drift " + Mathf.Abs(drift1 - drift0).ToString("0.00") + ")");

            // --- balance
            var bal = PuzzleKit.WeightBalance(null, o + new Vector3(12f, 0f, 26f), null, 5f, 10f, 20f);
            PuzzleKit.Crate(null, o + new Vector3(9.5f, 0.3f, 26f));
            PuzzleKit.Crate(null, o + new Vector3(14.5f, 0.3f, 26f));
            yield return WaitFor(() => bal.IsOn, 3f);
            ctx.Check(bal.IsOn, "balance: 25 vs 25 is balanced (" + bal.MassA.ToString("0") + "/" + bal.MassB.ToString("0") + ")");
            PuzzleKit.Crate(null, o + new Vector3(14.5f, 1.5f, 26f), null, true);
            yield return WaitFor(() => !bal.IsOn, 3f);
            ctx.Check(!bal.IsOn, "balance: 25 vs 85 is not");

            // --- wobbly tilts under an off-center load
            var wob = PuzzleKit.WobblyPlatform(null, o + new Vector3(22f, 1.6f, 26f));
            PuzzleKit.Crate(null, o + new Vector3(22f, 1.9f, 27.6f), null, true);
            yield return ctx.Wait(2f);
            ctx.Check(wob.Angle > 5f, "wobbly platform tilts toward the load (" + wob.Angle.ToString("0.0") + " deg)");

            // --- hazards cycle
            var laser = PuzzleKit.LaserBarrier(null, o + new Vector3(30f, 0f, 26f), null, 3f, 2, 1f, 1f);
            var crusher = PuzzleKit.Crusher(null, o + new Vector3(36f, 0f, 26f), null, null, 2.6f, 1f);
            bool sawOn = false, sawOff = false; float maxExt = 0f;
            for (float t = 0f; t < 3f; t += Time.deltaTime) { if (laser.IsActive) sawOn = true; else sawOff = true; maxExt = Mathf.Max(maxExt, crusher.Extension); yield return null; }
            ctx.Check(sawOn && sawOff, "laser cycles on/off");
            ctx.Check(maxExt > 0.99f, "crusher slams down");
            ctx.Log("logic block done");
        }

        // ================================================================== characters (stub motor is enough)
        public static IEnumerator Characters(DevPlayContext ctx)
        {
            var o = new Vector3(500f, 0f, 0f);
            Floor(o + new Vector3(20f, 0f, 15f), new Vector2(90f, 70f));
            Look(o + new Vector3(0f, 10f, -12f), o);

            // --- plate with Brute + another on 150; stacked Sprinter on Brainiac on 100
            var p150 = PuzzleKit.PressurePlate(null, o, null, Tuning.PlateHeavy);
            var door = PuzzleKit.Door(null, o + new Vector3(0f, 0f, 5f), null, p150);
            var brute = Brute(o + new Vector3(-0.4f, 0.4f, 0f));
            yield return ctx.Wait(1.2f);
            ctx.Check(!p150.IsOn, "Brute alone (110) < 150 (" + p150.CurrentKg.ToString("0") + ")");
            var buddy = Brainiac(o + new Vector3(0.5f, 0.4f, 0.3f));
            yield return WaitFor(() => door.IsOpen, 4f);
            ctx.Check(p150.IsOn && door.IsOpen, "Brute + Brainiac (180) open the 150 door (" + p150.CurrentKg.ToString("0") + ")");
            brute.Motor.Teleport(o + new Vector3(-4f, 0.1f, -2f), Quaternion.identity);
            buddy.Motor.Teleport(o + new Vector3(-6f, 0.1f, -2f), Quaternion.identity);
            yield return WaitFor(() => door.IsClosed, 5f);
            ctx.Check(!p150.IsOn && door.IsClosed, "door closes when they leave");

            var p100 = PuzzleKit.PressurePlate(null, o + new Vector3(6f, 0f, 0f), null, Tuning.PlateMedium);
            var bottom = Brainiac(o + new Vector3(6f, 0.2f, 0f));
            yield return ctx.Wait(0.8f);
            var top = Sprinter(bottom.HeadTop + Vector3.up * 0.15f);
            yield return WaitFor(() => p100.IsOn, 3f);
            ctx.Check(p100.IsOn, "stacked Sprinter on Brainiac counts on the 100 plate (" + p100.CurrentKg.ToString("0") + " kg)");

            // --- terminal: IQ 8 hacks within HackDuration, IQ 3 denied
            var term = PuzzleKit.Terminal(null, o + new Vector3(12f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            var dumb = Spawn("Kevin", Archetype.Brute, 3, 8, 3, 105f, o + new Vector3(12f, 0.1f, -1f));
            yield return ctx.Wait(0.3f);
            term.Interact(dumb);
            yield return ctx.Wait(0.2f);
            ctx.Check(!term.IsOn && term.DeniedCount == 1 && !term.IsHacking, "IQ 3 is denied (joke)");
            dumb.Motor.Teleport(o + new Vector3(15f, 0.1f, -3f), Quaternion.identity);
            var smart = Brainiac(o + new Vector3(12f, 0.1f, -1f), 8);
            yield return ctx.Wait(0.3f);
            term.Interact(smart);
            ctx.Check(term.IsHacking && smart.Condition == CharacterCondition.Interacting, "IQ 8 starts hacking (Interacting)");
            float hd = Tuning.HackDuration(8);
            yield return ctx.Wait(hd + 0.2f);
            ctx.Check(term.IsOn && !term.IsHacking && smart.Condition == CharacterCondition.Normal, "hacked within HackDuration (" + hd.ToString("0.0") + " s)");

            // --- rusty lever: STR 3 blocked, STR 8 pulls
            var rusty = PuzzleKit.Lever(null, o + new Vector3(18f, 0f, 0f), null, Tuning.RustyLeverStrength);
            ctx.Check(!rusty.CanInteract(smart, out var reason) && reason != null && reason.Contains("STR 7"), "rusty lever blocks weaklings: " + reason);
            ctx.Check(rusty.CanInteract(brute, out _), "rusty lever ok for STR 8");
            rusty.Interact(brute);
            ctx.Check(rusty.IsOn, "rusty lever pulled by the Brute");

            // --- laser damages + knocks back
            var laser = PuzzleKit.LaserBarrier(null, o + new Vector3(24f, 0f, 0f), null, 3f, 2);
            var victim = Sprinter(o + new Vector3(24f, 0.1f, -0.05f));
            var start = victim.transform.position;
            yield return ctx.Wait(0.8f);
            ctx.Check(laser.HitCount > 0 && victim.Health < Tuning.MaxHealth - 30f, "laser deals 35 (health " + victim.Health.ToString("0") + ")");
            ctx.Check((victim.transform.position - start).magnitude > 0.6f, "laser knocks back (" + (victim.transform.position - start).magnitude.ToString("0.0") + " m)");

            // --- kill zone (void) loses a character, foam pit respawns
            PuzzleKit.KillZone(null, o + new Vector3(30f, 0f, 0f), null, new Vector3(3f, 4f, 3f), KillZoneType.Void);
            var doomed = Sprinter(o + new Vector3(30f, -1f, 0f));
            yield return ctx.Wait(0.4f);
            ctx.Check(doomed.State == CharacterState.Lost && doomed.LastCause == "fell into the void", "void kill zone: Lost (" + doomed.LastCause + ")");
            var spawnPt = o + new Vector3(36f, 0.1f, -4f);
            PuzzleKit.FoamPit(null, o + new Vector3(36f, 0f, 0f), null, new Vector3(3f, 3f, 3f), spawnPt);
            var lucky = Brainiac(o + new Vector3(36f, -1f, 0f));
            yield return ctx.Wait(0.5f);
            ctx.Check(lucky.IsAlive && (lucky.transform.position - spawnPt).magnitude < 1.5f, "foam pit bounces the intern back to the respawn point");

            // --- bounce pad launches a character >= 4 m
            var pad = PuzzleKit.BouncePad(null, o + new Vector3(42f, 0f, 0f), null, 5f);
            var jumper = Sprinter(o + new Vector3(42f, 1.2f, 0f));
            float maxY = 0f;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime) { maxY = Mathf.Max(maxY, jumper.transform.position.y); yield return null; }
            ctx.Check(maxY - pad.transform.position.y >= 4f, "bounce pad launches a character >= 4 m (" + maxY.ToString("0.0") + ")");

            // --- crumbling platform falls and respawns
            var crumble = PuzzleKit.CrumblingPlatform(null, o + new Vector3(48f, 3f, 0f));
            var walker = Brainiac(o + new Vector3(48f, 3.3f, 0f));
            yield return WaitFor(() => crumble.State == CrumblingPlatform.CrumbleState.Falling, 2.5f);
            ctx.Check(crumble.State == CrumblingPlatform.CrumbleState.Falling, "crumbling platform falls after a character touches it");
            yield return WaitFor(() => crumble.State == CrumblingPlatform.CrumbleState.Idle, 5f);
            ctx.Check(crumble.State == CrumblingPlatform.CrumbleState.Idle && Mathf.Abs(crumble.transform.position.y - 2.75f) < 0.05f, "crumbling platform respawns at home");

            // --- crusher KOs, electric floor + spikes hurt, zone trigger counts
            var crusher = PuzzleKit.Crusher(null, o + new Vector3(0f, 0f, 14f), null, null, 2.6f, 0.8f);
            var squished = Brute(o + new Vector3(0f, 0.1f, 14f));
            yield return WaitFor(() => squished.State == CharacterState.Downed, 3f);
            ctx.Check(squished.State == CharacterState.Downed && crusher.Crushed > 0, "crusher knocks out whoever is under it");
            var ef = PuzzleKit.ElectricFloor(null, o + new Vector3(8f, 0f, 14f), null, null, 1f, 1f);
            var zapped = Sprinter(o + new Vector3(8f, 0.1f, 14f));
            yield return WaitFor(() => ef.Zaps > 0, 3f);
            ctx.Check(ef.Zaps > 0 && zapped.Health < Tuning.MaxHealth, "electric floor zaps (health " + zapped.Health.ToString("0") + ")");
            var spikes = PuzzleKit.Spikes(null, o + new Vector3(14f, 0f, 14f));
            var ouch = Brainiac(o + new Vector3(14f, 0.6f, 14f));
            yield return WaitFor(() => spikes.Hits > 0, 2f);
            ctx.Check(spikes.Hits > 0 && ouch.Health < Tuning.MaxHealth, "spikes hurt");
            var zone = PuzzleKit.ZoneTrigger(null, o + new Vector3(20f, 0f, 14f), null, new Vector3(3f, 3f, 3f), ZoneFilter.Archetype, 1);
            zone.RequiredArchetype = Archetype.Brainiac;
            var zoner = Brute(o + new Vector3(20f, 0.1f, 14f));
            yield return ctx.Wait(0.3f);
            ctx.Check(!zone.IsOn, "zone ignores the wrong archetype");
            Brainiac(o + new Vector3(20.6f, 0.1f, 14.8f));
            yield return ctx.Wait(0.4f);
            ctx.Check(zone.IsOn && zone.Count == 1, "zone turns on for a Brainiac");
            var sweeper = PuzzleKit.SweeperArm(null, o + new Vector3(30f, 0f, 14f), null, 6f, 120f);
            Sprinter(o + new Vector3(32f, 0.1f, 14f));
            yield return WaitFor(() => sweeper.Hits > 0, 4f);
            ctx.Check(sweeper.Hits > 0, "sweeper bowls a character over");
            ctx.Log("character block done");
        }
    }
}
