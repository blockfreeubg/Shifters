using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>DevPlay for the campaign enemies: teamwork camera geometry + timing, CouldSee/CouldSeePair, shot disable /
    /// blind, alarm latch + reset, escort back, guard patrol / blind / coffee break / capture / cell release / freeze /
    /// tiny / swap, stalker (moves only unlit; pool + flashlight cone freeze it; buddy = not scared; freeze; hug -> cell;
    /// never enters a pool; dormant), the Bravery rule. Screenshots: cameras, dummies, guard.
    /// Run: devplay.sh &lt;clone&gt; enemies -devSetup Shifters.EnemyTests.Run -devSeconds 150</summary>
    public static class EnemyTests
    {
        static readonly Vector3 Parking = new Vector3(-34f, 0f, 34f);
        static ShifterCharacter _a, _b, _c;

        static void Put(ShifterCharacter c, Vector3 p, float yaw = 0f) { c.Motor.Teleport(p, Quaternion.Euler(0f, yaw, 0f)); }
        static void ParkAll() { Put(_a, Parking); Put(_b, Parking + new Vector3(4f, 0f, 0f)); Put(_c, Parking + new Vector3(8f, 0f, 0f)); }
        static ShotInfo Shot(ExperimentalVariant v, Vector3 at, WeaponType w = WeaponType.Experimental)
            => new ShotInfo { Point = at, Normal = Vector3.back, Direction = Vector3.forward, Damage = 10f, Weapon = w, Variant = v };

        public static IEnumerator Run(DevPlayContext ctx)
        {
            WETestKit.Ground(100f);
            _a = WETestKit.Character(Archetype.Brute, Parking, 0f, 0);
            _b = WETestKit.Character(Archetype.Sprinter, Parking + new Vector3(4f, 0f, 0f), 0f, 1);
            _c = WETestKit.Character(Archetype.Brainiac, Parking + new Vector3(8f, 0f, 0f), 0f, 2);
            yield return ctx.Wait(0.3f);
            yield return Cameras(ctx);
            ParkAll();
            yield return Guards(ctx);
            ParkAll();
            yield return Stalkers(ctx);
            ParkAll();
            yield return BraveryRule(ctx);
        }

        /// <summary>Stalker phase only (quick re-run): devplay.sh &lt;clone&gt; enemies_stalk -devSetup Shifters.EnemyTests.RunStalkers</summary>
        public static IEnumerator RunStalkers(DevPlayContext ctx)
        {
            WETestKit.Ground(100f);
            _a = WETestKit.Character(Archetype.Brute, Parking, 0f, 0);
            _b = WETestKit.Character(Archetype.Sprinter, Parking + new Vector3(4f, 0f, 0f), 0f, 1);
            _c = WETestKit.Character(Archetype.Brainiac, Parking + new Vector3(8f, 0f, 0f), 0f, 2);
            yield return ctx.Wait(0.3f);
            yield return Stalkers(ctx);
        }

        static string Diag(StalkerBrain s)
            => "state " + s.State + " target " + (s.Target != null ? s.Target.name : "-") + " lit " + s.IsLit + " moving " + s.IsMoving
               + " frozen " + s.IsFrozen + " dormant " + s.Dormant + " pos " + s.transform.position + " | a " + _a.State + "/" + _a.Condition
               + " feet " + _a.Feet + " scared " + Bravery.IsScared(_a) + " aLit " + Bravery.IsLit(_a) + " kin " + (s.Body != null && s.Body.isKinematic) + " vel " + (s.Body != null ? s.Body.linearVelocity.ToString("0.00") : "-")
               + " rot " + s.transform.eulerAngles.y.ToString("0") + " near [" + Near(s) + "]";

        static string Near(StalkerBrain s)
        {
            var hits = Physics.OverlapSphere(s.transform.position + Vector3.up * 1f, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            var sb = new System.Text.StringBuilder();
            foreach (var h in hits) { if (h.attachedRigidbody == s.Body && h.transform == s.transform) continue; sb.Append(h.name).Append('<').Append(h.transform.parent != null ? h.transform.parent.name : "").Append("> "); }
            var cc = s.GetComponent<CapsuleCollider>();
            if (cc != null) sb.Append(" mat=").Append(cc.sharedMaterial != null ? cc.sharedMaterial.name : "none");
            return sb.ToString();
        }

        static string Unused() => "";

        // =========================================================================== cameras
        static IEnumerator Cameras(DevPlayContext ctx)
        {
            var cam = EnemyKit.SecurityCamera(null, new Vector3(0f, 4.5f, 0f), Quaternion.identity, CameraDetect.Teamwork, 0f, 6f, 12f, 40f, 0f);
            var sweep = EnemyKit.SecurityCamera(null, new Vector3(20f, 4.5f, -30f), Quaternion.identity, CameraDetect.Teamwork, 30f, 4f, 12f, 40f, 0f);
            var any = EnemyKit.SecurityCamera(null, new Vector3(-14f, 4.5f, 14f), Quaternion.identity, CameraDetect.AnyIntern, 0f, 6f, 12f, 40f, 0f);
            EnemyKit.CameraPost(null, new Vector3(-14f, 0f, 13.6f), 4.0f);
            yield return ctx.Wait(0.2f);
            ctx.Check(Mathf.Abs(cam.FloorPoint.y) < 0.05f, "camera found its floor (" + cam.FloorPoint + ")");

            // single intern inside the cone: always fine
            Put(_a, new Vector3(0f, 0f, 6f));
            yield return ctx.Wait(1.0f);
            ctx.Check(cam.SeesCharacter(_a) && !cam.Detecting && cam.Meter01 == 0f, "teamwork cam: one intern alone in the cone is fine (sees " + cam.SeesCharacter(_a) + ", meter " + cam.Meter01 + ")");
            // pair inside radius + cone -> detected after ~0.5 s
            float t0 = Time.time;
            Put(_b, new Vector3(1.6f, 0f, 6f));
            yield return ctx.Wait(0.2f);
            ctx.Check(!cam.Detecting && cam.Meter01 > 0f, "pair in cone: meter filling, not yet detected after 0.2 s (" + cam.Meter01.ToString("0.00") + ")");
            WETestKit.Cam(new Vector3(-6f, 8f, -6f), new Vector3(0f, 0f, 6f));
            ctx.Shot("cam_spotting");
            yield return WETestKit.WaitUntil(() => cam.Detecting, 2f);
            float dt = Time.time - t0;
            ctx.Check(cam.Detecting && dt >= 0.4f && dt <= 1.0f, "pair within 3 m inside the cone -> TEAMWORK detected after " + dt.ToString("0.00") + " s");
            ctx.Check(cam.IsOn && cam.DetectionCount == 1 && (cam.SpottedA == _a || cam.SpottedB == _a), "camera SignalSource ON while detecting, count 1");
            yield return ctx.Wait(0.1f);
            ctx.Shot("cam_detected");
            // pair split beyond the radius (both still in the cone) -> clears
            Put(_b, new Vector3(-2.2f, 0f, 9f)); Put(_a, new Vector3(1.6f, 0f, 9f));
            yield return ctx.Wait(1.0f);
            ctx.Check(cam.SeesCharacter(_a) && cam.SeesCharacter(_b) && !cam.Detecting && cam.Meter01 == 0f, "pair 3.8 m apart in the cone -> not teamwork");
            // pair within radius but one outside the cone
            Put(_a, new Vector3(2.4f, 0f, 8f)); Put(_b, new Vector3(4.6f, 0f, 8f));
            yield return ctx.Wait(1.0f);
            ctx.Check(cam.SeesCharacter(_a) && !cam.SeesCharacter(_b) && !cam.Detecting, "pair 2.2 m apart, one outside the cone -> not teamwork");
            // pair behind a wall
            var wall = Kit.SolidBox(null, "Wall", new Vector3(-3f, 1.6f, 7f), new Vector3(3f, 3.2f, 0.3f), MatKey.Wall);
            Put(_a, new Vector3(-3.2f, 0f, 9.5f)); Put(_b, new Vector3(-2.2f, 0f, 9.7f));
            yield return ctx.Wait(1.0f);
            ctx.Check(cam.InWedge(_a.Feet, cam.SweepYaw) && !cam.SeesCharacter(_a) && !cam.Detecting, "pair in the wedge behind a 3.2 m wall -> not seen");
            Object.Destroy(wall.gameObject);

            // shot: disabled 5 s
            Put(_a, new Vector3(0f, 0f, 6f)); Put(_b, new Vector3(1.2f, 0f, 6f));
            yield return WETestKit.WaitUntil(() => cam.Detecting, 2f);
            ctx.Check(cam.Detecting, "pair back in the cone -> detected again");
            cam.OnShot(Shot(ExperimentalVariant.RubberChickenLauncher, cam.EyePosition, WeaponType.Pistol));
            ctx.Check(cam.IsDisabled && !cam.Detecting && cam.InactiveRemaining > 4.8f && cam.InactiveRemaining <= 5.01f, "shot -> disabled 5 s (" + cam.InactiveRemaining.ToString("0.00") + "), detection cleared");
            yield return ctx.Wait(1.0f);
            ctx.Check(!cam.Detecting && cam.Meter01 == 0f, "disabled camera sees nothing (pair still in the cone)");
            ctx.Shot("cam_disabled");
            cam.ResetState();
            // T-shirt / say cheese blind
            cam.OnShot(Shot(ExperimentalVariant.TShirtCannon, cam.EyePosition));
            ctx.Check(cam.IsBlind && cam.InactiveRemaining > 3.8f && cam.InactiveRemaining <= 4.01f, "T-SHIRT -> camera WRAPPED 4 s");
            cam.ResetState();
            cam.OnShot(Shot(ExperimentalVariant.SayCheese, cam.EyePosition));
            ctx.Check(cam.IsBlind && cam.InactiveRemaining > 4.8f, "SAY CHEESE -> camera blinded 5 s");
            cam.ResetState();
            yield return WETestKit.WaitUntil(() => cam.Detecting, 2f);
            ctx.Check(cam.Detecting, "after ResetState the camera works again");

            // AnyIntern camera
            Put(_c, new Vector3(-14f, 0f, 20f));
            yield return WETestKit.WaitUntil(() => any.Detecting, 2f);
            ctx.Check(any.Detecting && any.SpottedA == _c, "AnyIntern camera detects a single intern");
            Put(_c, Parking + new Vector3(8f, 0f, 0f));

            // sweep prediction (geometry only)
            Vector3 apex = sweep.FloorPoint;
            Vector3 p25 = apex + Quaternion.Euler(0f, 25f, 0f) * Vector3.forward * 8f;
            Vector3 p60 = apex + Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * 8f;
            Vector3 pBack = apex + Vector3.back * 6f;
            ctx.Check(sweep.CouldSee(p25, 4f), "sweeping cam (+-30, 4 s): CouldSee(25 deg, 4 s) = true");
            ctx.Check(!sweep.CouldSee(p60, 4f) && !sweep.CouldSee(pBack, 8f), "CouldSee(60 deg) and behind = false");
            Vector3 q = apex + Quaternion.Euler(0f, 25f, 0f) * Vector3.forward * 8.5f;
            ctx.Check(sweep.CouldSeePair(p25, q, 4f), "CouldSeePair(two points 0.5 m apart at 25 deg) = true");
            ctx.Check(!sweep.CouldSeePair(p25, apex + Quaternion.Euler(0f, -25f, 0f) * Vector3.forward * 8f, 4f), "CouldSeePair(points 6.8 m apart) = false (not teamwork)");
            float ymin = 999f, ymax = -999f;
            for (int i = 0; i < 150; i++) { float y = Mathf.DeltaAngle(0f, sweep.SweepYaw); ymin = Mathf.Min(ymin, y); ymax = Mathf.Max(ymax, y); yield return null; }
            ctx.Check(ymax - ymin > 20f && ymin >= -30.5f && ymax <= 30.5f, "sweep yaw moves within +-30 (" + ymin.ToString("0") + " .. " + ymax.ToString("0") + ")");
            float tClear = sweep.SecondsUntilSees(p25, 6f);
            ctx.Check(tClear >= 0f && tClear < 4f, "SecondsUntilSees(25 deg) = " + tClear.ToString("0.00"));
            sweep.Freeze(1f);
            float yf = sweep.SweepYaw;
            yield return ctx.Wait(0.5f);
            ctx.Check(sweep.IsFrozen && Mathf.Abs(Mathf.DeltaAngle(yf, sweep.SweepYaw)) < 0.01f, "CAPS-LOCK: sweep frozen");

            // ------------------------------------------------ alarm latch
            var alarm = EnemyKit.AlarmLatch(null, new Vector3(0f, 0f, -2f), new[] { cam }, 1.5f);
            alarm.TripBeat = "test.detected";
            string beat = null;
            System.Action<string> onBeat = id => beat = id;
            GameEvents.StoryBeat += onBeat;
            yield return WETestKit.WaitUntil(() => alarm.IsOn, 2f);
            ctx.Check(alarm.IsOn && alarm.TripCount == 1 && alarm.TrippedBy == cam && beat == "test.detected", "alarm latch trips on detection (+ story beat)");
            ParkAll();
            yield return ctx.Wait(0.6f);
            ctx.Check(!cam.Detecting && alarm.IsOn, "alarm stays latched after the camera clears");
            yield return WETestKit.WaitUntil(() => !alarm.IsOn, 2f);
            ctx.Check(!alarm.IsOn && alarm.TripCount == 1, "alarm latch auto-resets after resetSeconds");
            GameEvents.StoryBeat -= onBeat;
            Object.Destroy(alarm.gameObject);

            // ------------------------------------------------ escort back
            var marks = new Transform[3];
            for (int i = 0; i < 3; i++) marks[i] = Kit.Make("ReturnMark" + i, null, new Vector3(-4f + i * 3f, 0f, -8f), Quaternion.identity).transform;
            var alarm2 = EnemyKit.AlarmLatch(null, new Vector3(0f, 0f, -2f), new[] { cam }, 8f);
            var escort = EnemyKit.EscortBack(null, alarm2, marks);
            Put(_a, new Vector3(0f, 0f, 6f)); Put(_b, new Vector3(1.4f, 0f, 6.5f));
            yield return WETestKit.WaitUntil(() => alarm2.IsOn, 2f);
            ctx.Check(alarm2.IsOn && escort.IsEscorting, "escort starts on the alarm");
            yield return WETestKit.WaitUntil(() => escort.EscortCount == 1, 4f);
            ctx.Check(escort.EscortCount == 1 && !escort.IsEscorting, "escort finished");
            ctx.Check(WETestKit.Planar(_a.Feet, marks[0].position) < 0.3f && WETestKit.Planar(_b.Feet, marks[1].position) < 0.3f && WETestKit.Planar(_c.Feet, marks[2].position) < 0.3f,
                "team teleported to return marks (slot i -> mark i)");
            ctx.Check(!alarm2.IsOn && cam.IsPaused, "alarm cleared and cameras paused after the escort");
            ctx.Check(ScreenFader.Alpha < 0.05f, "screen faded back in (alpha " + ScreenFader.Alpha.ToString("0.00") + ")");
            Object.Destroy(escort.gameObject); Object.Destroy(alarm2.gameObject);
            ParkAll();
            Object.Destroy(cam.gameObject); Object.Destroy(sweep.gameObject); Object.Destroy(any.gameObject);
            yield return null;
        }

        // =========================================================================== guards
        static IEnumerator Guards(DevPlayContext ctx)
        {
            var cell = EnemyKit.HoldingCell(null, new Vector3(-10f, 0f, -22f), Quaternion.Euler(0f, -90f, 0f), "guardCell");
            var g = EnemyKit.GuardBot(null, new Vector3(-26f, 0f, -24f), Quaternion.identity,
                new[] { new Vector3(-26f, 0f, -24f), new Vector3(-26f, 0f, -18f) }, "guardCell", new Vector3(-22f, 0f, -21f), 12f, 10f, 90f, -1f);
            var gb = EnemyKit.GuardBot(null, new Vector3(-40f, 0f, -40f), Quaternion.identity, new Vector3[0], "", new Vector3(-40f, 0f, -38f), 8f, 10f, 90f, 0f);
            ctx.Check(g.Bot != null && g.Cone != null && g.Cap != null && Mathf.Approximately(g.Bot.SightRange, 10f) && Mathf.Approximately(g.Bot.Fov, 90f), "GuardBot: bot + cone + cap, per-bot SightRange/Fov");
            Vector3 start = g.Bot.transform.position;
            WETestKit.Cam(new Vector3(-31f, 6f, -29f), new Vector3(-25f, 0.5f, -20f));
            yield return ctx.Wait(1.5f);
            ctx.Shot("guard_patrol");
            yield return ctx.Wait(3.5f);
            ctx.Check(g.Bot.State == BotState.Patrol && (g.Bot.WaypointsReached >= 1 || WETestKit.Planar(g.Bot.transform.position, start) > 1.5f), "guard patrols its waypoints (" + g.Bot.WaypointsReached + ")");

            // coffee break (gb: break from t=0 for 6 s) - an intern right in front is ignored
            ctx.Check(gb.OnCoffeeBreak && !gb.Cone.Visible, "coffee break: on break, cone hidden");
            Put(_b, gb.Bot.transform.position + gb.Bot.transform.forward * 3f, 180f);
            yield return ctx.Wait(0.8f);
            ctx.Check(gb.Bot.State == BotState.Patrol, "guard on a coffee break does not spot an intern 3 m in front (" + gb.Bot.State + ")");
            Put(_b, Parking + new Vector3(4f, 0f, 0f));
            // T-shirt wrap
            gb.Bot.OnShot(Shot(ExperimentalVariant.TShirtCannon, gb.Bot.EyePosition));
            ctx.Check(gb.Bot.IsBlind && gb.Bot.BlindRemaining > 3.8f && !gb.Bot.CanSee(_b), "T-SHIRT: guard WRAPPED 4 s (blind)");
            // freeze
            gb.Bot.Freeze(1f);
            yield return ctx.Wait(0.3f);
            ctx.Check(gb.Bot.IsFrozen && gb.Bot.Body.isKinematic, "CAPS-LOCK: bot frozen (kinematic)");
            yield return ctx.Wait(1.0f);
            ctx.Check(!gb.Bot.IsFrozen && !gb.Bot.Body.isKinematic, "freeze ends, physics restored");
            // tiny
            string reason;
            bool ok = gb.Bot.TryCycleSize(_a, out reason);
            ctx.Check(ok && gb.Bot.IsTiny && gb.Bot.SizeClass == -1 && Mathf.Approximately(gb.Bot.Body.mass, EnemyReact.TinyMass) && gb.Bot.transform.localScale.x < 0.7f, "BIT-SHIFTER: bot TINY (15 kg, x0.6)");
            gb.Bot.SetTiny(0f);
            ctx.Check(!gb.Bot.IsTiny && gb.Bot.transform.localScale.x > 0.99f && gb.Bot.Body.mass > 100f, "tiny restored");
            // swap
            yield return ctx.Wait(0.2f);
            ctx.Check(gb.Bot.CanSwap(_a, out reason), "guard is swappable");
            Vector3 swapTo = new Vector3(-36f, 0f, -40f);
            gb.Bot.SwapTo(swapTo, Quaternion.identity);
            yield return ctx.Wait(0.1f);
            ctx.Check(WETestKit.Planar(gb.Bot.transform.position, swapTo) < 0.4f && gb.Bot.IsStunned, "SWAP-O-MATIC: bot moved + stunned (WHERE AM I?)");
            // leaf blower: shove + 1 s stun, rate-limited
            yield return ctx.Wait(1.8f);
            gb.Bot.OnShot(Shot(ExperimentalVariant.LeafBlower, gb.Bot.EyePosition));
            ctx.Check(!gb.Bot.IsStunned, "LEAF BLOWER OnShot: no stun from enemies (weapons' stream shoves + stuns itself)");
            SecurityBot.BlowerShovesItself = false;
            gb.Bot.OnShot(Shot(ExperimentalVariant.LeafBlower, gb.Bot.EyePosition));
            SecurityBot.BlowerShovesItself = true;
            ctx.Check(gb.Bot.IsStunned && gb.Bot.StunRemaining <= 1.01f && gb.Bot.StunRemaining > 0.8f, "LEAF BLOWER (BlowerShovesItself=false): 1 s stun");

            // capture -> carried to the cell -> imprisoned -> released
            Put(_c, g.Bot.transform.position + g.Bot.transform.forward * 3f, 180f);
            yield return WETestKit.WaitUntil(() => _c.State == CharacterState.Captured, 6f);
            ctx.Check(_c.State == CharacterState.Captured, "guard captures an intern in front of it");
            yield return WETestKit.WaitUntil(() => cell.PrisonerCount > 0, 15f);
            ctx.Check(cell.PrisonerCount == 1 && cell.Contains(_c.Feet), "guard delivered the prisoner into its holding cell");
            ctx.Shot("guard_cell");
            cell.ReleaseAll();
            yield return ctx.Wait(0.3f);
            ctx.Check(_c.IsAlive && cell.PrisonerCount == 0, "cell release frees the intern");
            ParkAll();
            yield return ctx.Wait(0.2f);
            Object.Destroy(g.gameObject); Object.Destroy(gb.gameObject); Object.Destroy(cell.gameObject);
            yield return null;
        }

        // =========================================================================== stalkers
        static IEnumerator Stalkers(DevPlayContext ctx)
        {
            Bravery.Override = true;
            var cell = EnemyKit.HoldingCell(null, new Vector3(44f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), "trustRoom");
            var s = EnemyKit.Stalker(null, new Vector3(25f, 0f, 0f), Quaternion.identity, "trustRoom", new Vector3(25f, 0f, 2f), 12f, StalkerSkin.TeamBuildingDummy);
            var max = EnemyKit.Stalker(null, new Vector3(23f, 0f, -1.5f), Quaternion.identity, "", new Vector3(23f, 0f, -1.5f), 6f, StalkerSkin.CardboardMax);
            max.SetDormant(true);
            yield return ctx.Wait(0.3f);
            WETestKit.Cam(new Vector3(24f, 1.8f, 4.5f), new Vector3(24f, 1.1f, -1f));
            yield return ctx.Wait(0.2f);
            ctx.Shot("stalker_models");
            ctx.Check(s.State == StalkerState.Dormant, "stalker dormant while nobody is in its section (" + s.State + ")");

            // a lone intern in the dark -> SCARED -> hunted
            Put(_a, new Vector3(25f, 0f, 9f), 180f);
            ctx.Check(Bravery.IsScared(_a), "lone intern in the dark is SCARED");
            yield return WETestKit.WaitUntil(() => s.IsMoving, 2f);
            ctx.Check(s.IsMoving && s.Target == _a && s.State == StalkerState.Hunting, "stalker hunts the scared intern (" + s.State + ")");
            yield return ctx.Wait(0.4f);
            // pool on the stalker -> lit -> frozen
            var pool = EnemyTestLight.Pool(s.transform.position, 3f);
            yield return ctx.Wait(0.3f);
            float d0 = s.DistanceMoved + s.DistanceMovedLit;
            yield return ctx.Wait(1.0f);
            float moved = s.DistanceMoved + s.DistanceMovedLit - d0;
            ctx.Check(s.IsLit && s.State == StalkerState.Lit && moved < 0.15f, "light pool: stalker lit and does not move (" + moved.ToString("0.00") + " m)");
            pool.On = false;
            yield return WETestKit.WaitUntil(() => s.IsMoving, 1.5f);
            ctx.Check(!s.IsLit && s.IsMoving, "pool off -> it moves again");
            // flashlight cone on it -> frozen
            var cone = EnemyTestLight.Cone(s.transform.position + new Vector3(0f, 1.5f, 7f), s.Center);
            yield return ctx.Wait(0.3f);
            d0 = s.DistanceMoved + s.DistanceMovedLit;
            yield return ctx.Wait(1.0f);
            moved = s.DistanceMoved + s.DistanceMovedLit - d0;
            ctx.Check(s.IsLit && moved < 0.15f, "flashlight cone (25 deg / 14 m): stalker lit and frozen (" + moved.ToString("0.00") + " m)");
            // cone pointing away -> unlit
            cone.transform.rotation = Quaternion.LookRotation(Vector3.left);
            yield return WETestKit.WaitUntil(() => s.IsMoving, 1.5f);
            ctx.Check(!s.IsLit && s.IsMoving, "flashlight pointed away -> moves");
            // flashlight blocked by a wall
            cone.transform.rotation = Quaternion.LookRotation(s.Center - cone.transform.position);
            cone.On = false;
            // buddy -> not scared -> no target
            Put(_b, _a.Feet + new Vector3(3f, 0f, 0f));
            yield return ctx.Wait(0.4f);
            ctx.Check(!Bravery.IsScared(_a) && s.Target == null && !s.IsMoving, "a buddy within 6 m -> not scared -> the stalker stops (" + s.State + ")");
            ctx.Log("buddy: " + Diag(s));
            Put(_b, Parking + new Vector3(4f, 0f, 0f));
            yield return ctx.Wait(0.3f);
            // Freeze
            s.Freeze(1f);
            yield return ctx.Wait(0.15f);
            d0 = s.DistanceMoved;
            yield return ctx.Wait(0.6f);
            ctx.Check(s.IsFrozen && s.DistanceMoved - d0 < 0.1f, "Freeze(1): no movement");
            // say cheese -> frozen 3 s
            s.OnShot(Shot(ExperimentalVariant.SayCheese, s.Center));
            ctx.Check(s.IsFrozen && s.FrozenRemaining > 2.8f, "SAY CHEESE: dummy frozen 3 s");
            s.ResetState();
            yield return ctx.Wait(0.2f);

            // the hug -> trust exercise room
            WETestKit.Cam(new Vector3(30f, 5f, 12f), new Vector3(25f, 0.5f, 5f));
            ctx.Log("hug start: " + Diag(s));
            for (int k = 0; k < 16 && _a.State != CharacterState.Captured; k++) { yield return ctx.Wait(0.5f); if (k % 4 == 0) ctx.Log("hug +" + (k * 0.5f) + "s: " + Diag(s)); }
            ctx.Check(_a.State == CharacterState.Captured && s.CaptureCount == 1, "stalker captures the scared intern (TEAM-BUILDING HUG)");
            ctx.Check(cell.PrisonerCount == 1 && cell.Contains(_a.Feet), "hugged intern is in the trust exercise room");
            ctx.Check(WETestKit.Planar(s.transform.position, s.Home) < 0.5f && s.RestRemaining > 3f, "stalker poofed home and rests (" + s.RestRemaining.ToString("0.0") + " s, " + s.State + ")");
            cell.ReleaseAll();
            yield return ctx.Wait(0.3f);
            ctx.Check(_a.IsAlive, "trust room release frees the intern");

            // never enters a pool (HuntAll: targets the lit intern but stops at the edge)
            var pool2 = EnemyTestLight.Pool(new Vector3(25f, 0f, 8f), 3f);
            Put(_a, new Vector3(25f, 0f, 8f));
            s.HuntAll = true;
            s.ResetState();
            yield return ctx.Wait(4.5f);
            float edge = WETestKit.Planar(s.transform.position, pool2.transform.position);
            ctx.Check(_a.IsAlive && edge > 2.4f && !s.IsLit, "HuntAll: stalker waits at the pool edge (" + edge.ToString("0.00") + " m from the centre), intern safe");
            ctx.Check(!Bravery.IsScared(_a), "intern alone in a pool is not scared");
            WETestKit.Cam(new Vector3(29f, 4f, 0f), new Vector3(25f, 0.8f, 6f));
            ctx.Shot("stalker_pool_edge");
            s.HuntAll = false;
            pool2.On = false;
            // dormant again
            ParkAll();
            yield return ctx.Wait(0.4f);
            ctx.Check(s.State == StalkerState.Dormant && !s.IsMoving, "everyone left the section -> dormant");
            ctx.Check(s.DistanceMovedLit < 0.5f, "total distance moved while lit " + s.DistanceMovedLit.ToString("0.00") + " m (stop-on-light)");
            Object.Destroy(s.gameObject); Object.Destroy(max.gameObject); Object.Destroy(cell.gameObject);
            Object.Destroy(pool.gameObject); Object.Destroy(pool2.gameObject); Object.Destroy(cone.gameObject);
            yield return null;
        }

        // =========================================================================== bravery
        static IEnumerator BraveryRule(DevPlayContext ctx)
        {
            Bravery.Override = true;
            Put(_a, new Vector3(0f, 0f, 30f)); Put(_b, new Vector3(4f, 0f, 30f)); Put(_c, new Vector3(20f, 0f, 30f));
            yield return ctx.Wait(0.3f);
            ctx.Check(!Bravery.IsScared(_a) && !Bravery.IsScared(_b), "pair 4 m apart in the dark: not scared");
            ctx.Check(Bravery.IsScared(_c), "intern 16 m from everyone in the dark: SCARED");
            Put(_b, new Vector3(7f, 0f, 30f));
            yield return ctx.Wait(0.2f);
            ctx.Check(Bravery.IsScared(_a) && Bravery.IsScared(_b), "pair 7 m apart (> 6 m): both scared");
            var pool = EnemyTestLight.Pool(new Vector3(0f, 0f, 30f), 3f);
            ctx.Check(!Bravery.IsScared(_a) && Bravery.IsLit(_a), "lit by a pool: not scared");
            pool.On = false;
            ctx.Check(Bravery.IsScared(_a), "pool off: scared again");
            _b.KnockOut("test");
            yield return null;
            ctx.Check(!Bravery.IsScared(_b), "a Downed intern is never scared");
            Put(_c, new Vector3(5f, 0f, 30f));
            yield return null;
            ctx.Check(Bravery.IsScared(_a) == false, "buddy = any Alive teammate within 6 m (brainiac at 5 m)");
            _b.Revive(new Vector3(7f, 0f, 30f), Quaternion.identity);
            ctx.Log("Bravery: SetScared seam present = " + EnemyBridge.HasSetScared + ", LightRegistry present = " + LightQuery.HasRegistry + ", Active = " + Bravery.Active);
            Bravery.Override = null;
            Object.Destroy(pool.gameObject);
        }
    }
}
