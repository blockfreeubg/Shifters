// =====================================================================================
//  SHIFTERS - L0_Sandbox.cs   (BUILDER module - example builder + bot test course)
//  Scene Assets/Shifters/Scenes/L0_Sandbox.unity (NOT in Build Settings). A straight course along +Z with one
//  station per ability, every useful point marked for TeamBot:
//    spawn floor (crate) -> 2.0 m gap -> checkpoint 1 -> 2.5 m STACK ledge (right) + 3.5 m THROW ledge (left)
//    -> 5.2 m BRIDGE gap -> heavy block + 150 kg plate (left), IQ 7 terminal (right), shoot target (far right)
//    -> exit. A catch floor 3.5 m below every gap with a ramp back up to the spawn (no deaths in the sandbox).
//  Marks: spawn, checkpoint.1, exit, gap2.takeoff/landing, stack.base/ledge/block, throw.spot/ledge/block,
//         bridge.near/far, push.block/plate/start, hack.terminal/spot, shoot.target/spot, spawn.crate, exit.approach
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L0_Sandbox : ILevelBuilder, ILevelSceneInfo
    {
        /// <summary>Builder index used by LevelRuntimeBuilder.BuildInCurrentScene(L0_Sandbox.Index).</summary>
        public const int Index = 100;
        public const string SceneName = "L0_Sandbox";

        public int LevelIndex => Index;

        public LevelInfo SceneInfo => new LevelInfo(Index, SceneName, "SANDBOX", "Builder test course",
            "One station per ability, for bots and humans.", LevelTheme.Training, MusicId.Training, "Try every station. Reach the exit.");

        // course metrics (top surfaces)
        public const float Gap2Start = 7f, Gap2End = 9f;          // 2.0 m gap
        public const float BridgeStart = 21f, BridgeEnd = 26.2f;  // 5.2 m gap
        public const float StackHeight = 2.5f, ThrowHeight = 3.5f, CatchY = -3.5f;

        public void Build(LevelBuildContext ctx)
        {
            var g = ctx.Geometry;
            // ---------------------------------------------------------------- structure (exact metrics)
            ctx.Platform(g, new Vector3(0f, 0f, 0f), new Vector3(14f, 1f, 14f), MatKey.Floor, true, "P0_Spawn");
            ctx.Platform(g, new Vector3(0f, 0f, 15f), new Vector3(14f, 1f, 12f), MatKey.Floor, true, "P1_Stations");
            ctx.Platform(g, new Vector3(0f, 0f, (BridgeEnd + 54f) * 0.5f), new Vector3(14f, 1f, 54f - BridgeEnd), MatKey.Floor, true, "P2_Puzzles");
            var stack = ctx.Platform(g, new Vector3(4.5f, StackHeight, 18f), new Vector3(5f, StackHeight, 6f), MatKey.WallAlt, true, "StackLedge_2.5m");
            var thr = ctx.Platform(g, new Vector3(-5f, ThrowHeight, 18f), new Vector3(4f, ThrowHeight, 6f), MatKey.WallAlt, true, "ThrowLedge_3.5m");
            ctx.Platform(g, new Vector3(0f, CatchY, 20f), new Vector3(28f, 1f, 72f), MatKey.FloorAlt, false, "CatchFloor");
            ctx.Ramp(g, new Vector3(0f, CatchY, -15f), new Vector3(0f, 0f, -7f), 5f, 0.5f, MatKey.FloorAlt, "ReturnRamp");
            // low curbs along the long sides of the puzzle floor keep props from sliding off
            ctx.Block(g, new Vector3(-7.15f, 0.25f, (BridgeEnd + 54f) * 0.5f), new Vector3(0.3f, 0.5f, 54f - BridgeEnd), MatKey.Trim, "CurbW");
            ctx.Block(g, new Vector3(7.15f, 0.25f, (BridgeEnd + 54f) * 0.5f), new Vector3(0.3f, 0.5f, 54f - BridgeEnd), MatKey.Trim, "CurbE");

            // ---------------------------------------------------------------- gameplay
            ctx.SetSpawn(new Vector3(0f, 0f, -3f), 0f);
            ctx.Checkpoint(new Vector3(0f, 0f, 12f), 0f);
            ctx.Exit(new Vector3(0f, 0f, 50f), 0f);
            ctx.Tutorial(new Vector3(0f, 1.5f, -2f), new Vector3(10f, 3f, 8f), "sandbox_welcome", "SANDBOX",
                "Every station tests one ability. Switch characters with 1 / 2 / 3.", new[] { "1", "2", "3" }, false);
            ctx.Hint(new Vector3(0f, 2f, 22f), new Vector3(16f, 8f, 70f),
                "Stations: 2 m gap (anyone), 2.5 m stack, 3.5 m throw, 5.2 m bridge, push, hack, shoot.",
                "Each sign tells you what to try.");

            // ---------------------------------------------------------------- kit content (optional modules)
            var crate = ctx.CallKit("PuzzleKit", "Crate", ctx.Props, new Vector3(4.5f, 0f, -3f), Quaternion.identity);
            if (crate != null) ctx.Mark("spawn.crate", crate);
            var block = ctx.CallKit("PuzzleKit", "HeavyBlock", ctx.Props, new Vector3(-3f, 0f, 30f), Quaternion.identity);
            if (block != null) ctx.Mark("push.block", block);
            var plate = ctx.CallKit("PuzzleKit", "PressurePlate", ctx.Puzzles, new Vector3(-3f, 0f, 37f), Quaternion.identity, 150f);
            if (plate != null) ctx.Mark("push.plate", plate);
            var term = ctx.CallKit("PuzzleKit", "Terminal", ctx.Puzzles, new Vector3(3f, 0f, 33f), Quaternion.Euler(0f, 180f, 0f));
            if (term != null) ctx.Mark("hack.terminal", term);
            var target = ctx.CallKit("PuzzleKit", "ShootTarget", ctx.Puzzles, new Vector3(5f, 2.5f, 46f), Quaternion.Euler(0f, 180f, 0f));
            if (target != null) ctx.Mark("shoot.target", target);
            ctx.CallKit("PerspectiveKit", "Clue", ctx.Puzzles, new Vector3(3f, 2.4f, 34.2f), Quaternion.identity, "IQ CLUE: 42", 0.5f);
            ctx.CallKit("PerspectiveKit", "SpeedRoute", ctx.Gameplay, new[] { new Vector3(0f, 0f, 13f), new Vector3(0f, 0f, BridgeStart - 0.3f), new Vector3(0f, 0f, BridgeEnd + 1.5f) });

            // ---------------------------------------------------------------- marks for bots
            ctx.Mark("stack.block", stack.gameObject);
            ctx.Mark("throw.block", thr.gameObject);
            ctx.MarkPoint("gap2.takeoff", new Vector3(0f, 0f, Gap2Start - 0.5f));
            ctx.MarkPoint("gap2.landing", new Vector3(0f, 0f, Gap2End + 0.8f));
            ctx.MarkPoint("stack.base", new Vector3(1.3f, 0f, 18f), 90f);
            ctx.MarkPoint("stack.ledge", new Vector3(4.5f, StackHeight, 18f));
            ctx.MarkPoint("throw.spot", new Vector3(-0.6f, 0f, 18f), -90f);
            ctx.MarkPoint("throw.ledge", new Vector3(-5f, ThrowHeight, 18f));
            ctx.MarkPoint("bridge.near", new Vector3(0f, 0f, BridgeStart - 0.4f));
            ctx.MarkPoint("bridge.far", new Vector3(0f, 0f, BridgeEnd + 0.8f));
            ctx.MarkPoint("push.start", new Vector3(-3f, 0f, 28.2f));
            ctx.MarkPoint("hack.spot", new Vector3(3f, 0f, 31.6f));
            ctx.MarkPoint("shoot.spot", new Vector3(3f, 0f, 34.5f));
            ctx.MarkPoint("exit.approach", new Vector3(0f, 0f, 46f));

            // ---------------------------------------------------------------- signage
            ctx.Sign(ctx.Decor, new Vector3(0f, 3.4f, Gap2Start - 0.2f), 0f, "SHIFTERS SANDBOX: ONE STATION PER ABILITY", 0.42f);
            ctx.Sign(ctx.Decor, new Vector3(-5.2f, 1.3f, Gap2Start - 0.3f), 0f, "2 M GAP: EVERYONE", 0.32f);
            ctx.Sign(ctx.Decor, new Vector3(4.5f, 1.5f, 14.93f), 0f, "STACK 2.5 M", 0.36f);
            ctx.Sign(ctx.Decor, new Vector3(-5f, 2.0f, 14.93f), 0f, "THROW 3.5 M", 0.36f);
            ctx.Sign(ctx.Decor, new Vector3(-5.2f, 1.3f, BridgeStart - 0.3f), 0f, "BRIDGE 5.2 M", 0.32f);
            ctx.Sign(ctx.Decor, new Vector3(-3f, 2.6f, 27.4f), 0f, "PUSH 160 KG ONTO 150 KG", 0.3f);
            ctx.Sign(ctx.Decor, new Vector3(3f, 2.6f, 27.4f), 0f, "HACK: IQ 7", 0.3f);
            ctx.Sign(ctx.Decor, new Vector3(5f, 4.1f, 46.3f), 0f, "SHOOT", 0.3f);
        }
    }
}
