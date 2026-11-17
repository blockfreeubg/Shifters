using System.Collections;
using UnityEngine;
using static Shifters.PuzzleTestUtil;

namespace Shifters
{
    /// <summary>Puzzle behaviour that needs CORE's real CharacterMotor (pushing, riding, conveyors, hack cancel).
    /// Run: devplay.sh &lt;proj&gt; puzzles_motor -devSetup Shifters.PuzzleMotorTests.Run</summary>
    public static class PuzzleMotorTests
    {
        static void Drive(ShifterCharacter c, Vector3 dir, bool sprint = false)
            => c.Motor.SetCommand(new CharacterMotor.Command { Move = dir, Sprint = sprint });

        public static IEnumerator Run(DevPlayContext ctx)
        {
            var o = new Vector3(700f, 0f, 0f);
            Floor(o + new Vector3(20f, 0f, 10f), new Vector2(80f, 50f));
            Look(o + new Vector3(4f, 9f, -10f), o + new Vector3(4f, 0f, 4f));

            // --- heavy block: Brute (STR 8) pushes >= 2 m, Brainiac (STR 2) can't
            var block = PuzzleKit.HeavyBlock(null, o + new Vector3(0f, 0f, 4f));
            var brute = Brute(o + new Vector3(0f, 0.1f, 1.6f));
            yield return ctx.Wait(0.5f);
            var b0 = block.transform.position;
            Drive(brute, Vector3.forward);
            yield return ctx.Wait(3f);
            Drive(brute, Vector3.zero);
            yield return ctx.Wait(0.3f);
            float pushed = (block.transform.position - b0).z;
            ctx.Check(pushed >= 2f, "Brute pushes the 160 kg block >= 2 m (" + pushed.ToString("0.0") + " m)");
            var after = block.transform.position;
            yield return ctx.Wait(0.6f);
            ctx.Check((block.transform.position - after).magnitude < 0.05f, "block stops dead when the push ends");
            yield return PuzzleZoo.ShotAt(ctx, block.transform.position + new Vector3(3.5f, 3.2f, -5.5f), block.transform.position + Vector3.up * 0.6f, 50f, "motor_push");

            var block2 = PuzzleKit.HeavyBlock(null, o + new Vector3(8f, 0f, 4f));
            var nerd = Brainiac(o + new Vector3(8f, 0.1f, 1.6f));
            yield return ctx.Wait(0.5f);
            var n0 = block2.transform.position;
            Drive(nerd, Vector3.forward);
            yield return ctx.Wait(2.5f);
            Drive(nerd, Vector3.zero);
            ctx.Check((block2.transform.position - n0).magnitude < 0.1f, "Brainiac (STR 2) can't move it (" + (block2.transform.position - n0).magnitude.ToString("0.00") + " m)");

            // --- moving platform carries a standing character (drift < 0.3 m over a loop)
            var mp = PuzzleKit.MovingPlatform(null, o + new Vector3(18f, 1f, 0f), null, new[] { Vector3.zero, new Vector3(6f, 0f, 0f) }, new Vector3(3f, 0.4f, 3f), 2.5f, 0.5f);
            var rider = Sprinter(o + new Vector3(18f, 1.1f, 0f));
            yield return ctx.Wait(0.8f);
            var rel0 = rider.transform.position - mp.Body.transform.position;
            yield return ctx.Wait(5.8f);
            var rel1 = rider.transform.position - mp.Body.transform.position;
            float drift = Vector3.ProjectOnPlane(rel1 - rel0, Vector3.up).magnitude;
            ctx.Check(drift < 0.3f && rider.transform.position.y > 0.9f, "character rides the moving platform (drift " + drift.ToString("0.00") + " m over a loop)");

            // --- conveyor moves a standing character
            var conv = PuzzleKit.Conveyor(null, o + new Vector3(30f, 0.4f, 0f), null, 8f, 2f, 2.5f);
            var belted = Brainiac(o + new Vector3(30f, 0.55f, -2.5f));
            yield return ctx.Wait(0.6f);
            float cz = belted.transform.position.z;
            yield return ctx.Wait(1.5f);
            ctx.Check(belted.transform.position.z - cz > 2f, "conveyor carries a character (" + (belted.transform.position.z - cz).ToString("0.0") + " m)");

            // --- elevator lifts a character
            var lever = PuzzleKit.Lever(null, o + new Vector3(36f, 0f, -3f));
            var elev = PuzzleKit.Elevator(null, o + new Vector3(40f, 0f, 0f), null, 3f, new Vector2(3f, 3f), lever);
            var passenger = Brute(o + new Vector3(40f, 0.1f, 0f));
            yield return ctx.Wait(0.6f);
            lever.Flip(true);
            yield return ctx.Wait(3f);
            ctx.Check(passenger.transform.position.y > 2.8f && passenger.Motor.IsGrounded, "elevator lifts the Brute (y " + passenger.transform.position.y.ToString("0.0") + ")");

            // --- hacking is cancelled when the hacker walks away
            var term = PuzzleKit.Terminal(null, o + new Vector3(48f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            var hacker = Brainiac(o + new Vector3(48f, 0.1f, -1f), 8);
            yield return ctx.Wait(0.4f);
            term.Interact(hacker);
            yield return ctx.Wait(0.5f);
            Drive(hacker, Vector3.back);
            yield return ctx.Wait(0.4f);
            Drive(hacker, Vector3.zero);
            ctx.Check(!term.IsHacking && !term.IsOn && hacker.Condition == CharacterCondition.Normal, "walking away cancels the hack");

            // --- a character in the doorway stops the door
            var dl = PuzzleKit.Lever(null, o + new Vector3(54f, 0f, -3f));
            var door = PuzzleKit.Door(null, o + new Vector3(56f, 0f, 2f), null, dl);
            dl.Flip(true);
            yield return WaitFor(() => door.IsOpen, 3f);
            var stander = Sprinter(o + new Vector3(56f, 0.1f, 2f));
            yield return ctx.Wait(0.5f);
            dl.Flip(false);
            yield return ctx.Wait(3f);
            ctx.Check(door.BlockedCount > 0 && !door.IsClosed && stander.IsAlive, "door won't close on a character (blocked " + door.BlockedCount + ")");
            yield return PuzzleZoo.ShotAt(ctx, o + new Vector3(56f, 4f, -7f), o + new Vector3(56f, 2f, 2f), 55f, "motor_door_blocked");
            ctx.Log("motor block done");
        }
    }
}
