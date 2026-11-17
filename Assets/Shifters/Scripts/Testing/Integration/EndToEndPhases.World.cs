// =====================================================================================
//  SHIFTERS - EndToEndPhases.World.cs   [INTEGRATION - phases 10-14: plate -> door, terminal hack,
//  shot at a target, bot capture + cell rescue, level exit -> level complete UI]
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static partial class EndToEndPhases
    {
        // ------------------------------------------------------------------ 9b. push the 160 kg block onto the 150 kg plate
        public static IEnumerator PushBlock(DevPlayContext ctx)
        {
            var block = LevelMark.Find<Pushable>("push.block");
            var plate = LevelMark.Find<PressurePlate>("push.plate");
            ctx.Check(block != null && plate != null, "sandbox heavy block + 150 kg plate exist");
            if (block == null || plate == null) yield break;
            var brute = E2E.Brute;
            E2E.Place(brute, LevelMark.Position("push.start"), 0f);
            yield return E2E.SwitchTo(brute, true);
            E2E.Frame(35f, 20f);
            yield return ctx.Wait(0.4f);
            bool shot = false;
            for (int i = 0; i < 60 * 14 && !plate.IsOn; i++)
            {
                // push straight along +Z and keep the pusher centred behind the block (like a player / TeamBot.PushBlock)
                float sideOff = brute.Feet.x - block.transform.position.x;
                E2E.Drive(brute, (Vector3.forward + Vector3.right * Mathf.Clamp(-sideOff * 1.5f, -0.6f, 0.6f)).normalized);
                if (!shot && block.IsBeingPushed && i > 90) { shot = true; ctx.Shot("17a_pushing_block"); }
                yield return null;
            }
            E2E.Stop(brute);
            ctx.Check(block.DistancePushed > 3f, "Brute (STR " + brute.Stats.Strength + ") pushed the 160 kg block " + block.DistancePushed.ToString("0.0") + " m");
            ctx.Check(plate.IsOn, "block pushed ONTO the 150 kg plate: plate ON (" + plate.CurrentKg.ToString("0") + " kg, block at " + block.transform.position.ToString("0.00") + ")");
            yield return ctx.Wait(0.5f);
            ctx.Shot("17b_block_on_plate");
            E2E.Place(brute, LevelMark.Position("push.start") + new Vector3(3f, 0f, 0f), 0f);
        }

        // ------------------------------------------------------------------ 10. pressure plate -> door
        public static IEnumerator PlateDoor(DevPlayContext ctx)
        {
            var plate = EndToEndCourse.Plate;
            var door = EndToEndCourse.PlateDoor;
            ctx.Check(plate != null && door != null, "annex plate + door built (PuzzleKit)");
            if (plate == null || door == null) yield break;
            var light = E2E.Sprinter;
            var heavy = E2E.Brute;
            E2E.Place(light, EndToEndCourse.PlateSpot, 0f);
            yield return E2E.SwitchTo(light, true);
            E2E.Frame(0f, 14f);
            yield return ctx.Wait(1.2f);
            ctx.Check(Mathf.Abs(plate.CurrentKg - light.Mass) < 3f, "plate reads the Sprinter's mass (" + plate.CurrentKg.ToString("0") + " / " + light.Mass.ToString("0") + " kg)");
            ctx.Check(light.Mass < EndToEndCourse.PlateKg ? (!plate.IsOn && door.IsClosed) : plate.IsOn, "Sprinter alone (" + light.Mass.ToString("0") + " kg) vs " + EndToEndCourse.PlateKg + " kg plate: door state correct");
            ctx.Shot("17_plate_too_light");
            E2E.Place(light, EndToEndCourse.PlateSpot + new Vector3(-3f, 0f, -2f), 0f);
            E2E.Place(heavy, EndToEndCourse.PlateSpot, 0f);
            yield return E2E.Until(() => plate.IsOn, 2f);
            ctx.Check(plate.IsOn, "Brute (" + heavy.Mass.ToString("0") + " kg) presses the plate");
            yield return E2E.Until(() => door.IsOpen, 5f);
            ctx.Check(door.IsOpen && door.Powered, "wired door opened");
            ctx.Shot("18_plate_door_open");
            E2E.Place(heavy, EndToEndCourse.PlateSpot + new Vector3(3f, 0f, -2f), 0f);
            yield return E2E.Until(() => !plate.IsOn, 2f);
            ctx.Check(!plate.IsOn, "plate released when the Brute steps off");
            yield return E2E.Until(() => door.IsClosed, 6f);
            ctx.Check(door.IsClosed, "door closed again (non-latching plate)");
        }

        // ------------------------------------------------------------------ 11. terminal hack (IQ)
        public static IEnumerator TerminalHack(DevPlayContext ctx)
        {
            var term = LevelMark.Find<Terminal>("hack.terminal");
            ctx.Check(term != null, "sandbox terminal exists");
            if (term == null) yield break;
            var brain = E2E.Brainiac;
            var spot = LevelMark.Position("hack.spot");
            E2E.Place(brain, spot, E2E.YawTo(spot, term.transform.position));
            yield return E2E.SwitchTo(brain, true);
            E2E.Frame(E2E.YawTo(spot, term.transform.position) - 25f, 15f);
            yield return ctx.Wait(0.5f);
            ctx.Check(brain.Sensor != null && brain.Sensor.Current != null, "InteractionSensor found the terminal (" + (brain.Sensor != null && brain.Sensor.Current != null ? E2E.TypeName(brain.Sensor.Current) : "none") + ")");
            int prompts = E2E.Prompts;
            bool ok = brain.Sensor.TryInteract();
            ctx.Check(ok, "TryInteract accepted (IQ " + brain.Stats.IQ + ")");
            yield return ctx.Wait(0.5f);
            ctx.Check(term.IsHacking || term.IsOn, "hack in progress (" + term.Progress01.ToString("0.00") + ")");
            ctx.Shot("19_hacking");
            yield return E2E.Until(() => term.IsOn, 8f);
            ctx.Check(term.IsOn, "terminal hacked (IsOn)");
            yield return ctx.Wait(0.4f);
            ctx.Check(brain.Condition == CharacterCondition.Normal, "hacker released from Interacting");
            ctx.Shot("20_terminal_on");
        }

        // ------------------------------------------------------------------ 12. shoot a target
        public static IEnumerator Shoot(DevPlayContext ctx)
        {
            var st = LevelMark.Find<ShootTarget>("shoot.target");
            var gunner = E2E.GunOwner;
            ctx.Check(st != null && gunner != null, "shoot target + gun owner present");
            if (st == null || gunner == null) yield break;
            var spot = LevelMark.Position("shoot.spot");
            Vector3 aim = st.Disc != null ? st.Disc.position : st.transform.position + Vector3.up * 1.75f;
            float yaw = E2E.YawTo(spot, aim);
            E2E.Place(gunner, spot, yaw);
            yield return E2E.SwitchTo(gunner, true);
            E2E.Frame(yaw, 8f);
            yield return ctx.Wait(0.5f);
            int noises = E2E.Noises;
            int fired = 0;
            for (int attempt = 0; attempt < 6 && !st.IsOn; attempt++)
            {
                if (gunner.Weapon.FireAt(aim)) fired++;
                if (attempt == 0) { yield return ctx.Wait(0.05f); ctx.Shot("21_firing"); }
                yield return E2E.Until(() => st.IsOn, 1.5f);
            }
            ctx.Check(fired > 0, gunner.Weapon.Data.Name + " fired " + fired + " shot(s)");
            ctx.Check(st.IsOn && st.Hits > 0, "shoot target hit and ON (" + st.Hits + " hits)");
            ctx.Check(E2E.Noises > noises, "gunshot raised a Noise event");
            yield return ctx.Wait(0.3f);
            ctx.Shot("22_target_hit");
        }

        // ------------------------------------------------------------------ 13. bot capture -> holding cell -> lever rescue
        public static IEnumerator BotCapture(DevPlayContext ctx)
        {
            var bot = EndToEndCourse.Bot;
            var cell = EndToEndCourse.Cell;
            var lever = EndToEndCourse.CellLever;
            ctx.Check(bot != null && cell != null && lever != null, "annex bot + cell + lever built (EnemyKit/PuzzleKit)");
            if (bot == null || cell == null || lever == null) yield break;
            var victim = E2E.Sprinter;
            var rescuer = E2E.Brainiac;
            System.Action<SecurityBot, BotState> logState = (b, st) => ctx.Log("bot state -> " + st + " (carrying " + E2E.Who(b.Carried) + ", t=" + Time.time.ToString("0.00") + ")");
            bot.StateChanged += logState;
            // rescuer parked by the lever (outside the bot's view cone) - the camera follows them
            E2E.Place(rescuer, EndToEndCourse.LeverPos + new Vector3(-1.3f, 0f, 0f), 90f);
            yield return E2E.SwitchTo(rescuer, true);
            yield return ctx.Wait(0.6f);
            Vector3 fwd = bot.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 spot = bot.transform.position + fwd * 4.5f; spot.y = 0f;
            E2E.Place(victim, spot, E2E.YawTo(spot, bot.transform.position));
            E2E.Frame(E2E.YawTo(rescuer.Feet, spot), 20f);
            yield return E2E.Until(() => bot.State == BotState.Chase || bot.State == BotState.Capture, 3f);
            ctx.Check(bot.State == BotState.Chase || bot.State == BotState.Capture, "bot spotted the Sprinter and chases (" + bot.State + ")");
            yield return E2E.Until(() => victim.State == CharacterState.Captured, 6f);
            ctx.Check(victim.State == CharacterState.Captured && victim.CapturedCellId == EndToEndCourse.CellId, "bot captured the Sprinter (State Captured)");
            ctx.Check(E2E.BannerContains("CAPTURED"), "CAPTURED banner raised");
            yield return ctx.Wait(0.6f);
            ctx.Shot("23_bot_carrying");
            yield return E2E.Until(() => cell.PrisonerCount > 0, 15f);
            ctx.Check(cell.PrisonerCount == 1 && cell.Contains(victim.Feet), "prisoner delivered into the holding cell");
            ctx.Check(!victim.Body.isKinematic && victim.Capsule.enabled, "prisoner physics restored inside the cell");
            bot.Stun(30f);
            E2E.Frame(E2E.YawTo(rescuer.Feet, cell.transform.position), 18f);
            yield return ctx.Wait(0.8f);
            ctx.Shot("24_in_holding_cell");
            // rescue: pull the lever through the InteractionSensor
            E2E.Place(rescuer, EndToEndCourse.LeverPos + new Vector3(-1.1f, 0f, 0f), 90f);
            yield return ctx.Wait(0.4f);
            bool pulled = rescuer.Sensor.TryInteract();
            if (!pulled || !lever.IsOn) { ctx.Log("sensor did not pick the lever (" + E2E.TypeName(rescuer.Sensor.Current) + ") - flipping directly"); lever.Flip(true, rescuer); }
            ctx.Check(pulled && lever.IsOn, "rescuer pulled the cell lever via InteractionSensor");
            yield return E2E.Until(() => victim.State == CharacterState.Alive, 3f);
            ctx.Check(victim.State == CharacterState.Alive && cell.PrisonerCount == 0 && cell.IsOpen, "cell opened -> Sprinter released (Alive)");
            ctx.Check(E2E.BannerContains("RESCUED"), "RESCUED banner raised");
            yield return ctx.Wait(1.2f);
            ctx.Shot("25_cell_open_rescued");
            // checkpoint-restore path: LevelManager.CaptureRestoreHook (assigned by the enemies module) re-imprisons a captive
            lever.Flip(false, rescuer);
            yield return E2E.Until(() => !cell.IsOpen, 1f);
            ctx.Check(LevelManager.CaptureRestoreHook != null, "LevelManager.CaptureRestoreHook assigned (enemies module)");
            bool restored = LevelManager.CaptureRestoreHook != null && LevelManager.CaptureRestoreHook(victim, EndToEndCourse.CellId);
            yield return ctx.Wait(0.3f);
            ctx.Check(restored && victim.State == CharacterState.Captured && cell.Contains(victim.Feet), "restore hook locks the captive back in the cell");
            lever.Flip(true, rescuer);
            yield return E2E.Until(() => victim.State == CharacterState.Alive, 2f);
            ctx.Check(victim.State == CharacterState.Alive && cell.PrisonerCount == 0, "re-opened cell releases the restored captive");
            bot.StateChanged -= logState;
            bot.gameObject.SetActive(false); // keep the rest of the run deterministic
        }

        // ------------------------------------------------------------------ 14. level exit -> level complete
        public static IEnumerator Exit(DevPlayContext ctx)
        {
            var exit = LevelMark.Find<LevelExit>("exit");
            ctx.Check(exit != null, "sandbox LevelExit exists");
            if (exit == null) yield break;
            var mgr = CharacterManager.Instance;
            foreach (var c in mgr.Team) ctx.Check(c.IsAlive && c.Condition == CharacterCondition.Normal, E2E.Who(c) + " alive + normal before the exit (" + c.State + "/" + c.Condition + ")");
            Vector3 zone = exit.transform.TransformPoint(new Vector3(exit.ZoneCenter.x, 0f, exit.ZoneCenter.z));
            int k = 0;
            foreach (var c in mgr.Team) { E2E.Place(c, zone + exit.transform.right * ((k - 1) * 1.2f), exit.transform.eulerAngles.y + 180f); k++; }
            E2E.Frame(exit.transform.eulerAngles.y, 16f);
            yield return E2E.Until(() => E2E.LevelsCompleted > 0, 6f);
            ctx.Check(E2E.LevelsCompleted == 1, "LevelCompleted raised once (" + E2E.LevelsCompleted + ")");
            ctx.Check(E2E.LastResult.Survivors == 3 && (E2E.LastResult.LeftBehind == null || E2E.LastResult.LeftBehind.Length == 0),
                "result: 3 survivors, nobody left behind");
            ctx.Check(E2E.LastResult.Switches > 0 && E2E.LastResult.Time > 1f, "result has switches (" + E2E.LastResult.Switches + ") and time (" + E2E.LastResult.Time.ToString("0.0") + " s)");
            ctx.Check(LevelManager.Instance != null && LevelManager.Instance.IsCompleted, "LevelManager marked completed");
            ctx.Shot("26_exit_celebration");
            var ui = UIManager.Instance;
            yield return E2E.Until(() => ui != null && E2E.TypeName(E2E.Member(ui, "Top")) == "LevelCompleteScreen", 4f);
            ctx.Check(ui != null && E2E.TypeName(E2E.Member(ui, "Top")) == "LevelCompleteScreen", "level complete screen shown (" + E2E.TypeName(E2E.Member(ui, "Top")) + ")");
            ctx.Check(ShiftersInput.Context == InputContext.Menu, "context Menu on the level complete screen");
            yield return ctx.Wait(1.5f);
            ctx.Shot("27_level_complete_ui");
        }
    }
}
