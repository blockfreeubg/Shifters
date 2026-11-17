// =====================================================================================
//  SHIFTERS - EndToEndCourse.cs   [INTEGRATION - runtime-built course]
//  The builder's L0_Sandbox (through LevelRuntimeBuilder = the real level pipeline: lighting, LevelManager,
//  camera, kits, marks) plus two runtime annexes far away from the sandbox (bots never see the sandbox):
//    WEST  annex: 90 kg pressure plate wired to a sliding door (Brute opens it, the Sprinter alone does not)
//    EAST  annex: BONK-BOT 3000 guard arena + holding cell whose door is wired to a lever
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class EndToEndCourse
    {
        public const string CellId = "e2e_cell";
        public const float PlateKg = 90f;
        public static readonly Vector3 PlateArea = new Vector3(-45f, 0f, 30f);
        public static readonly Vector3 Arena = new Vector3(50f, 0f, 30f);

        public static LevelBuildContext Level { get; private set; }
        public static Transform Annex { get; private set; }
        public static PressurePlate Plate { get; private set; }
        public static Door PlateDoor { get; private set; }
        public static SecurityBot Bot { get; private set; }
        public static HoldingCell Cell { get; private set; }
        public static Lever CellLever { get; private set; }

        public static Vector3 PlateSpot => PlateArea + new Vector3(0f, 0f, -2f);
        public const float ArenaRadius = 10f;
        /// <summary>Outside the bot's arena (radius 10): the bot never chases the rescuer standing there.</summary>
        public static Vector3 LeverPos => Arena + new Vector3(15f, 0f, 2f);
        public static Vector3 BotHome => Arena + new Vector3(0f, 0f, -6f);

        public static IEnumerator Build(DevPlayContext ctx)
        {
            var gs = GameSession.Instance;
            ctx.Check(gs != null, "GameSession bootstrapped ([SHIFTERS] root)");
            if (gs != null) gs.NewRun(EndToEnd.Seed);
            ctx.Check(gs != null && gs.Team != null && gs.Team.Members != null && gs.Team.Members.Length == 3, "NewRun(" + EndToEnd.Seed + ") generated a 3-person team");

            Level = LevelRuntimeBuilder.BuildInCurrentScene(L0_Sandbox.Index);
            ctx.Check(Level != null && !Level.Failed, "L0_Sandbox built through LevelRuntimeBuilder");
            if (Level != null)
            {
                ctx.Log("sandbox build: " + Level.Report().Replace("\n", " | "));
                ctx.Check(Level.Warnings.Count == 0, "sandbox build has no warnings (" + (Level.Warnings.Count > 0 ? Level.Warnings[0] : "none") + ")");
            }
            ctx.Check(LevelManager.Instance != null, "LevelManager configured by the scene assembler");
            foreach (var id in new[] { "spawn", "spawn.crate", "checkpoint.1", "exit", "push.block", "push.plate", "hack.terminal", "shoot.target", "stack.base", "bridge.near" })
                ctx.Check(LevelMark.Exists(id), "sandbox mark '" + id + "' exists");

            Annex = new GameObject("[E2E Annex]").transform;
            BuildPlateArea();
            BuildBotArena();
            yield return null;
            ctx.Log("course built (sandbox + annexes)");
        }

        static void BuildPlateArea()
        {
            E2E.Box(Annex, "PlateFloor", PlateArea + new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 16f), MatKey.Floor);
            Plate = PuzzleKit.PressurePlate(Annex, PlateSpot, Quaternion.identity, PlateKg, false, "DOOR OPEN");
            Vector3 doorPos = PlateArea + new Vector3(0f, 0f, 4f);
            PlateDoor = PuzzleKit.Door(Annex, doorPos, Quaternion.identity, Plate);
            E2E.Box(Annex, "DoorWallW", doorPos + new Vector3(-4.3f, 2.1f, 0f), new Vector3(5.4f, 4.2f, 0.4f), MatKey.Wall);
            E2E.Box(Annex, "DoorWallE", doorPos + new Vector3(4.3f, 2.1f, 0f), new Vector3(5.4f, 4.2f, 0.4f), MatKey.Wall);
        }

        static void BuildBotArena()
        {
            E2E.Box(Annex, "ArenaFloor", Arena + new Vector3(3f, -0.5f, 0f), new Vector3(36f, 1f, 30f), MatKey.Floor);
            // the cell's door faces local +Z: turn it toward the arena (world -Z)
            Cell = EnemyKit.HoldingCell(Annex, Arena + new Vector3(0f, 0f, 9f), Quaternion.Euler(0f, 180f, 0f), CellId);
            CellLever = PuzzleKit.Lever(Annex, LeverPos, Quaternion.Euler(0f, -90f, 0f));
            if (Cell != null) Cell.SetInputs(CellLever);
            Bot = EnemyKit.SecurityBot(Annex, BotHome, Quaternion.identity, new Vector3[0], CellId, Arena, ArenaRadius);
        }
    }
}
