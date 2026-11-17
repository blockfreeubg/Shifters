using System.Collections;
using UnityEngine;
using static Shifters.PuzzleTestUtil;

namespace Shifters
{
    /// <summary>"Puzzle zoo": every PuzzleKit element lined up, screenshotted OFF, then switched ON and shot again.
    /// Run alone: devplay.sh &lt;proj&gt; puzzles_zoo -devSetup Shifters.PuzzleZoo.Run</summary>
    public static class PuzzleZoo
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            Floor(new Vector3(22f, 0f, 18f), new Vector2(64f, 52f));
            // ---------------- row 1: sources
            var plate = PuzzleKit.PressurePlate(null, new Vector3(0f, 0f, 0f), null, Tuning.PlateHeavy, false, "PLATE PUZZLE SOLVED");
            var range = PuzzleKit.WeightRangePlate(null, new Vector3(4.5f, 0f, 0f), null, 200f, 270f);
            var lever = PuzzleKit.Lever(null, new Vector3(8.5f, 0f, 0f));
            var rusty = PuzzleKit.Lever(null, new Vector3(11f, 0f, 0f), null, Tuning.RustyLeverStrength);
            var button = PuzzleKit.PushButton(null, new Vector3(13.5f, 0f, 0f), null, 60f);
            var timed = PuzzleKit.TimedButton(null, new Vector3(16f, 0f, 0f), null, 24f, new[] { new Vector3(16f, 0f, 1.2f), new Vector3(16f, 0f, 6f), new Vector3(20f, 0f, 6f) });
            var target = PuzzleKit.ShootTarget(null, new Vector3(19f, 0f, 0f));
            var term = PuzzleKit.Terminal(null, new Vector3(22f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            var gate = PuzzleKit.LogicGate(null, new Vector3(25f, 0f, 0f), null, SignalLogic.All, false, lever, rusty);
            var seqB1 = PuzzleKit.PushButton(null, new Vector3(27.5f, 0f, -1.5f), null, 0.5f);
            var seqB2 = PuzzleKit.PushButton(null, new Vector3(29f, 0f, -1.5f), null, 0.5f);
            var seqB3 = PuzzleKit.PushButton(null, new Vector3(30.5f, 0f, -1.5f), null, 0.5f);
            var seq = PuzzleKit.SequenceLock(null, new Vector3(29f, 0f, 0.8f), null, new SignalSource[] { seqB3, seqB1, seqB2 });
            PuzzleKit.ZoneTrigger(null, new Vector3(34f, 0f, 0f), null, new Vector3(3f, 3f, 3f), ZoneFilter.AnyCharacter, 2);
            var bal = PuzzleKit.WeightBalance(null, new Vector3(41f, 0f, 0f), null, 5f, 10f, 20f);

            // ---------------- row 2: receivers
            var door = PuzzleKit.Door(null, new Vector3(0f, 0f, 12f), null, plate);
            PuzzleKit.CableBetween(null, plate, door.transform);
            var sideDoor = PuzzleKit.Door(null, new Vector3(6.5f, 0f, 12f), null, DoorMode.SlideSide, 3f, 4.2f, lever);
            PuzzleKit.CableBetween(null, lever, sideDoor.transform);
            var tdoor = PuzzleKit.Door(null, new Vector3(13f, 0f, 12f), null, timed);
            PuzzleKit.CableBetween(null, timed, tdoor.transform);
            PuzzleKit.MovingPlatform(null, new Vector3(19f, 1.2f, 12f), null, new[] { Vector3.zero, new Vector3(0f, 1.5f, 0f) }, new Vector3(2.5f, 0.4f, 2.5f), 1f, 0.5f);
            var pulley = PuzzleKit.PulleyLift(null, new Vector3(26f, 0f, 12f), null, 5f, 3f, new Vector2(2.2f, 2.2f));
            var elev = PuzzleKit.Elevator(null, new Vector3(33f, 0f, 12f), null, 3f, new Vector2(2.5f, 2.5f), rusty);
            PuzzleKit.CableBetween(null, rusty, elev.transform);
            var bridge = PuzzleKit.RetractableBridge(null, new Vector3(38f, 0.6f, 10.5f), null, 4f, 2f, button);
            PuzzleKit.Conveyor(null, new Vector3(43f, 0.4f, 12f), null, 6f, 2f, 2f);
            var il1 = PuzzleKit.IndicatorLight(null, new Vector3(47f, 0f, 10f), null, false, gate);
            PuzzleKit.CableBetween(null, gate, il1.transform);
            PuzzleKit.IndicatorLight(null, new Vector3(47f, 0f, 13f), null, false, seq);

            // ---------------- row 3: props + hazards
            PuzzleKit.Crate(null, new Vector3(0f, 0f, 24f));
            PuzzleKit.Crate(null, new Vector3(1.6f, 0f, 24f), null, true);
            PuzzleKit.Barrel(null, new Vector3(3.3f, 0f, 24f));
            PuzzleKit.HeavyBlock(null, new Vector3(5.5f, 0f, 24f));
            var glass = PuzzleKit.GlassPane(null, new Vector3(9f, 0f, 24f), null, new Vector2(2.6f, 2.6f));
            var wall = PuzzleKit.CrackedWall(null, new Vector3(13f, 0f, 24f), null, new Vector3(3f, 3.2f, 0.6f));
            PuzzleKit.LaserBarrier(null, new Vector3(18f, 0f, 24f), null, 3f, 3, 3f, 2f);
            PuzzleKit.Crusher(null, new Vector3(23f, 0f, 24f), null, null, 2.4f, 2f);
            PuzzleKit.SweeperArm(null, new Vector3(29f, 0f, 24f), null, 5f, 60f);
            PuzzleKit.BouncePad(null, new Vector3(34f, 0f, 24f));
            PuzzleKit.SteamVent(null, new Vector3(37f, 0f, 24f), null, 4.5f, 30f, 1f);
            PuzzleKit.Spikes(null, new Vector3(40.5f, 0f, 24f));
            PuzzleKit.ElectricFloor(null, new Vector3(45f, 0.01f, 24f), null, new Vector2(3f, 3f), 30f, 1f);

            // ---------------- row 4: pits + platforms
            Pool(new Vector3(2f, 0f, 34f), KillZoneType.Acid);
            Pool(new Vector3(7f, 0f, 34f), KillZoneType.Incinerator);
            Pool(new Vector3(12f, 0f, 34f), KillZoneType.FoamPit);
            PuzzleKit.CrumblingPlatform(null, new Vector3(17f, 1.2f, 34f));
            PuzzleKit.WobblyPlatform(null, new Vector3(22f, 1.6f, 34f));

            yield return ctx.Wait(0.6f);
            yield return ShotAt(ctx, new Vector3(22f, 30f, -20f), new Vector3(22f, 0f, 16f), 50f, "zoo_overview_off");
            yield return ShotAt(ctx, new Vector3(8f, 4.5f, -9f), new Vector3(8f, 1.2f, 1f), 55f, "zoo_row1a_off");
            yield return ShotAt(ctx, new Vector3(31f, 4.5f, -9f), new Vector3(31f, 1.2f, 1f), 55f, "zoo_row1b_off");
            yield return ShotAt(ctx, new Vector3(10f, 5f, 2f), new Vector3(10f, 2f, 12f), 60f, "zoo_row2a_off");

            // ---------------- switch everything ON
            PuzzleKit.HeavyBlock(null, new Vector3(0f, 0.3f, 0f));
            PuzzleKit.HeavyBlock(null, new Vector3(4.5f, 0.3f, 0f));
            PuzzleKit.Crate(null, new Vector3(4.5f, 2.2f, 0f), null, true);
            lever.Flip(true); rusty.Flip(true);
            button.Press(); timed.Press();
            Shoot(target, target.transform.position + Vector3.up * 1.75f);
            seqB3.Press(); yield return ctx.Wait(0.7f); seqB1.Press(); yield return ctx.Wait(0.7f); seqB2.Press();
            var hacker = Brainiac(new Vector3(22f, 0.1f, -1f));
            term.Interact(hacker);
            PuzzleKit.Crate(null, new Vector3(38.5f, 0.3f, 0f));
            PuzzleKit.Crate(null, new Vector3(43.5f, 0.3f, 0f));
            PuzzleKit.Crate(null, pulley.PlatformA.transform.position + Vector3.up * 0.3f, null, true);
            Shoot(glass, glass.transform.position + Vector3.up);
            yield return ctx.Wait(2.2f);
            hacker.Motor.Teleport(new Vector3(23.5f, 0.1f, -2.5f), Quaternion.Euler(0f, 200f, 0f));
            yield return ctx.Wait(0.3f);
            ctx.Check(plate.IsOn && range.IsOn && gate.IsOn && seq.IsOn && term.IsOn && target.IsOn && door.IsOpen, "zoo: everything switched on");
            yield return ShotAt(ctx, new Vector3(8f, 4.5f, -9f), new Vector3(8f, 1.2f, 1f), 55f, "zoo_row1a_on");
            yield return ShotAt(ctx, new Vector3(31f, 4.5f, -9f), new Vector3(31f, 1.2f, 1f), 55f, "zoo_row1b_on");
            yield return ShotAt(ctx, new Vector3(10f, 5f, 2f), new Vector3(10f, 2f, 12f), 60f, "zoo_row2a_on");
            yield return ShotAt(ctx, new Vector3(37f, 6f, 1f), new Vector3(37f, 1.5f, 12f), 60f, "zoo_row2b_on");
            yield return ShotAt(ctx, new Vector3(12f, 5f, 15f), new Vector3(12f, 1.2f, 24f), 60f, "zoo_row3a");
            yield return ShotAt(ctx, new Vector3(36f, 5f, 15f), new Vector3(36f, 1.2f, 24f), 60f, "zoo_row3b");
            yield return ShotAt(ctx, new Vector3(12f, 6f, 26f), new Vector3(12f, 0.5f, 34f), 60f, "zoo_row4");
            yield return ShotAt(ctx, new Vector3(22f, 1.8f, -2.2f), new Vector3(22f, 1.1f, 0f), 40f, "zoo_terminal_close");
            yield return ShotAt(ctx, new Vector3(0.5f, 3f, -4.5f), new Vector3(0f, 1.6f, 0.5f), 50f, "zoo_plate_close");
            ctx.Log("zoo done");
        }

        /// <summary>Moves the camera, waits one frame (billboards/labels face the new camera), then captures.</summary>
        public static IEnumerator ShotAt(DevPlayContext ctx, Vector3 from, Vector3 at, float fov, string name)
        {
            Look(from, at, fov);
            yield return null;
            ctx.Shot(name);
        }

        static void Pool(Vector3 pos, KillZoneType type)
        {
            const float w = 3.4f, h = 0.6f;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.SolidBox(null, "PoolWall", pos + new Vector3(s * (w * 0.5f + 0.15f), h * 0.5f, 0f), new Vector3(0.3f, h, w + 0.6f), MatKey.WallAlt);
                Kit.SolidBox(null, "PoolWall", pos + new Vector3(0f, h * 0.5f, s * (w * 0.5f + 0.15f)), new Vector3(w, h, 0.3f), MatKey.WallAlt);
            }
            PuzzleKit.KillZone(null, pos + Vector3.up * (h + 0.05f), null, new Vector3(w, h + 0.05f, w), type, pos + new Vector3(0f, 0.2f, -4f));
        }
    }
}
