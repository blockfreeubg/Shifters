using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>DevPlay: BONK-BOT patrol, wall blocks sight, seen -> chase -> capture -> KO when no cell, noise lure,
    /// capture -> carry -> imprison (State Captured, inside the cell), cell opened -> released, shooting a carrying bot
    /// -> prisoner dropped & released, stun recovery.
    /// Run: devplay.sh &lt;clone&gt; we_bots -devSetup Shifters.BotTests.Run</summary>
    public static class BotTests
    {
        static readonly Vector3 Parking = new Vector3(-28f, 0f, 28f); // far outside every arena

        public static IEnumerator Run(DevPlayContext ctx)
        {
            WETestKit.Ground(80f);
            var cell = EnemyKit.HoldingCell(null, new Vector3(14f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), "cellA");
            ctx.Check(HoldingCell.Find("cellA") == cell, "HoldingCell.Find(id) finds the cell");
            var bot = EnemyKit.SecurityBot(null, new Vector3(-4f, 0f, 6f), Quaternion.Euler(0f, 90f, 0f),
                new[] { new Vector3(-5f, 0f, 6f), new Vector3(5f, 0f, 6f) }, "cellA", new Vector3(0f, 0f, 2f), 13f);
            var guard = EnemyKit.SecurityBot(null, new Vector3(-15f, 0f, -12f), Quaternion.identity, new Vector3[0], "noSuchCell", new Vector3(-15f, 0f, -10f), 9f);

            var brute = WETestKit.Character(Archetype.Brute, Parking, 0f, 0);
            var sprinter = WETestKit.Character(Archetype.Sprinter, Parking + new Vector3(3f, 0f, 0f), 0f, 1);
            var brain = WETestKit.Character(Archetype.Brainiac, Parking + new Vector3(6f, 0f, 0f), 0f, 2,
                WeaponGenerator.Generate(WeaponKind.Laser, new System.Random(3)));

            // ---------------------------------------------------------------- patrol
            WETestKit.Cam(new Vector3(0f, 7f, -7f), new Vector3(0f, 0.5f, 5f));
            Vector3 start = bot.transform.position;
            yield return ctx.Wait(1.5f);
            ctx.Shot("bot_patrol");
            yield return ctx.Wait(5.5f);
            ctx.Check(bot.State == BotState.Patrol, "bot patrols (" + bot.State + ")");
            ctx.Check(bot.WaypointsReached >= 1 && WETestKit.Planar(bot.transform.position, start) > 1.5f,
                "bot walked its waypoint loop (reached " + bot.WaypointsReached + ", now " + bot.transform.position + ")");
            ctx.Check(Mathf.Abs(bot.transform.position.z - 6f) < 1.0f, "bot stays on its patrol line");

            // ---------------------------------------------------------------- wall blocks sight (guard bot, no waypoints)
            var wall = Kit.SolidBox(null, "Wall", new Vector3(-15f, 1.5f, -7f), new Vector3(5f, 3f, 0.4f), MatKey.Wall);
            brute.Motor.Teleport(new Vector3(-15f, 0f, -4f), Quaternion.Euler(0f, 180f, 0f));
            yield return ctx.Wait(1.2f);
            ctx.Check(!guard.CanSee(brute) && guard.State != BotState.Chase, "character behind a wall is NOT seen (" + guard.State + ")");
            Object.Destroy(wall.gameObject);
            // the guard scans +-60 deg (FOV 110): allow one scan sweep to bring the character into view
            yield return WETestKit.WaitUntil(() => guard.CanSee(brute) || guard.State == BotState.Chase || guard.State == BotState.Capture, 3f);
            ctx.Check(guard.CanSee(brute) || guard.State == BotState.Chase || guard.State == BotState.Capture, "same character in the open IS seen (" + guard.State + ")");
            yield return WETestKit.WaitUntil(() => guard.State == BotState.Chase || guard.State == BotState.Capture, 1.5f);
            ctx.Check(guard.State == BotState.Chase || guard.State == BotState.Capture, "guard bot chases (" + guard.State + ")");
            // no cell => capture becomes a KO at the bot's home
            yield return WETestKit.WaitUntil(() => brute.State == CharacterState.Downed, 12f);
            ctx.Check(brute.State == CharacterState.Downed, "no holding cell: the capture became a KO (" + brute.State + ")");
            ctx.Check(WETestKit.Planar(brute.Feet, guard.Home) < 3f, "KO'd character dropped at the bot's home");
            brute.Revive(Parking, Quaternion.identity);
            guard.gameObject.SetActive(false);
            yield return ctx.Wait(0.3f);
            ctx.Check(brute.IsAlive && !CaptiveBody.IsSeized(brute) && !brute.Body.isKinematic && brute.Capsule.enabled, "KO'd character physics fully restored");

            // ---------------------------------------------------------------- noise lure
            yield return WETestKit.WaitUntil(() => bot.State == BotState.Patrol, 6f);
            Vector3 noise = new Vector3(-1f, 0f, -3.5f);
            float before = WETestKit.Planar(bot.transform.position, noise);
            GameEvents.RaiseNoise(noise, Tuning.NoiseTaunt, sprinter, 2f);
            yield return null;
            ctx.Check(bot.State == BotState.Investigate, "taunt noise -> bot investigates (" + bot.State + ")");
            yield return ctx.Wait(2.8f);
            float after = WETestKit.Planar(bot.transform.position, noise);
            ctx.Check(after < before - 2f && bot.transform.position.z < 4.5f, "noise lured the bot off its path (" + before.ToString("0.0") + " -> " + after.ToString("0.0") + " m)");

            // ---------------------------------------------------------------- seen -> chase -> capture -> carry -> cell
            Vector3 fwd = bot.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 spot = bot.transform.position + fwd * 4.5f; spot.y = 0f;
            sprinter.Motor.Teleport(spot, Quaternion.LookRotation(-fwd));
            yield return WETestKit.WaitUntil(() => bot.State == BotState.Chase, 1.0f);
            ctx.Check(bot.State == BotState.Chase && bot.Target == sprinter, "character in front -> CHASE (" + bot.State + ")");
            WETestKit.Cam(bot.transform.position + new Vector3(-4f, 4f, -6f), (bot.transform.position + spot) * 0.5f + Vector3.up);
            yield return ctx.Wait(0.4f);
            ctx.Shot("bot_chase");
            yield return WETestKit.WaitUntil(() => sprinter.State == CharacterState.Captured, 4f);
            ctx.Check(sprinter.State == CharacterState.Captured && sprinter.CapturedCellId == "cellA", "bot captured the character (State Captured, cell id)");
            ctx.Check(bot.State == BotState.Capture && bot.Carried == sprinter, "bot is carrying the prisoner");
            ctx.Check(sprinter.Body.isKinematic && !sprinter.Capsule.enabled && sprinter.transform.IsChildOf(bot.transform), "captive body kinematic, colliders off, parented to the bot");
            yield return ctx.Wait(0.8f);
            WETestKit.Cam(bot.transform.position + new Vector3(-3.5f, 3f, -5f), bot.transform.position + Vector3.up * 1.2f);
            ctx.Shot("bot_carrying");
            yield return WETestKit.WaitUntil(() => cell.PrisonerCount > 0, 10f);
            ctx.Check(cell.PrisonerCount == 1 && sprinter.State == CharacterState.Captured, "prisoner delivered and imprisoned (still Captured)");
            ctx.Check(!sprinter.transform.IsChildOf(bot.transform) && !sprinter.Body.isKinematic && sprinter.Capsule.enabled, "prisoner physics restored inside the cell");
            yield return ctx.Wait(1.5f);
            ctx.Check(cell.Contains(sprinter.Feet), "prisoner stays inside the cell (" + sprinter.Feet + ")");
            ctx.Check(bot.State == BotState.Return || bot.State == BotState.Patrol, "bot goes back to work (" + bot.State + ")");
            WETestKit.Cam(new Vector3(7.5f, 3.2f, 3.5f), new Vector3(14f, 1.4f, 0f));
            ctx.Shot("holding_cell");

            // ---------------------------------------------------------------- open the cell -> released
            cell.ForcePower(true);
            yield return null;
            ctx.Check(sprinter.State == CharacterState.Alive && cell.PrisonerCount == 0, "cell opened -> prisoner released (Alive)");
            yield return ctx.Wait(1.2f);
            ctx.Check(cell.Door.localPosition.y > 2f, "cell door slid open (" + cell.Door.localPosition.y.ToString("0.0") + ")");
            ctx.Shot("holding_cell_open");
            cell.ForcePower(false);
            sprinter.Motor.Teleport(Parking + new Vector3(3f, 0f, 0f), Quaternion.identity);

            // ---------------------------------------------------------------- shoot the carrying bot -> prisoner rescued
            yield return WETestKit.WaitUntil(() => bot.State == BotState.Patrol || bot.State == BotState.Return, 6f);
            fwd = bot.transform.forward; fwd.y = 0f; fwd.Normalize();
            spot = bot.transform.position + fwd * 4f; spot.y = 0f;
            brute.Motor.Teleport(spot, Quaternion.LookRotation(-fwd));
            yield return WETestKit.WaitUntil(() => brute.State == CharacterState.Captured, 5f);
            ctx.Check(brute.State == CharacterState.Captured && bot.Carried == brute, "second capture: bot carrying the Brute");
            yield return ctx.Wait(0.5f);
            Vector3 side = Vector3.Cross(Vector3.up, bot.transform.forward).normalized;
            brain.Motor.Teleport(bot.transform.position + side * 6f, Quaternion.LookRotation(-side));
            yield return ctx.Wait(0.2f);
            bool fired = brain.Weapon.FireAt(bot.transform.position + Vector3.up * 1.0f);
            yield return null;
            ctx.Check(fired && bot.IsStunned, "laser hit -> bot STUNNED (" + bot.State + ")");
            ctx.Check(bot.Carried == null && brute.State == CharacterState.Alive && !brute.Body.isKinematic && brute.Capsule.enabled && !brute.transform.IsChildOf(bot.transform),
                "stunned bot dropped the prisoner -> released with physics restored");
            WETestKit.Cam(bot.transform.position + new Vector3(-3f, 2.6f, -4.5f), bot.transform.position + Vector3.up);
            yield return ctx.Wait(0.3f);
            ctx.Shot("bot_stunned");
            brute.Motor.Teleport(Parking, Quaternion.identity);
            brain.Motor.Teleport(Parking + new Vector3(6f, 0f, 0f), Quaternion.identity);
            yield return ctx.Wait(3.2f);
            ctx.Check(!bot.IsStunned, "bot recovers after ~3 s (" + bot.State + ")");

            // ---------------------------------------------------------------- unreachable ledge -> waits below
            var ledge = Kit.SolidBox(null, "Ledge", new Vector3(bot.transform.position.x, 1.25f, bot.transform.position.z) + bot.transform.forward * 4f, new Vector3(2f, 2.5f, 2f), MatKey.Concrete);
            Vector3 top = ledge.transform.position + Vector3.up * 1.3f;
            sprinter.Motor.Teleport(top, Quaternion.identity);
            yield return WETestKit.WaitUntil(() => bot.IsWaitingBelow, 4f);
            ctx.Check(bot.IsWaitingBelow && sprinter.IsAlive, "character on an unreachable ledge: bot waits below beeping angrily");
            ctx.Log("bots scenario done");
        }
    }
}
