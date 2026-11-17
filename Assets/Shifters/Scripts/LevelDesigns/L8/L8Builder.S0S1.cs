// =====================================================================================
//  SHIFTERS - L8Builder.S0S1.cs   [S0 VAULT ANTECHAMBER, S1 THE LASER GALLERY, S1b END ROOM (C1)]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L8Builder
    {
        void BuildS0(LevelBuildContext ctx)
        {
            var g = ctx.Section("S0_Antechamber", Vector3.zero);
            var d = ctx.Decor;
            Floor(g, -8f, 8f, -8f, 10.5f, 0f, MatKey.Marble);
            Wall(g, -8.2f, -8.2f, -8.2f, 10.5f, 6f);
            Wall(g, 8.2f, -8.2f, 8.2f, 10.5f, 6f);
            Wall(g, -8.2f, -8.2f, 8.2f, -8.2f, 6f);
            Wall(g, -8.2f, 10.5f, -2.7f, 10.5f, 6f);
            Wall(g, 2.7f, 10.5f, 8.2f, 10.5f, 6f);
            ctx.Block(g, V(0f, 5.2f, 10.5f), V(5.8f, 1.6f, 0.4f), MatKey.Wall, "Lintel");

            ctx.SetSpawn(V(0f, 0f, -5f), 0f);

            // decor elevator behind the spawn
            Kit.SolidBox(d, "ElevatorFrame", V(0f, 2.1f, -7.75f), V(3.8f, 4.2f, 0.5f), MatKey.MetalDark, 0.04f);
            Kit.BoxVisual(d, "ElevatorDoorL", V(-0.78f, 1.6f, -7.47f), V(1.5f, 3.1f, 0.08f), MatKey.Gold, 0.02f);
            Kit.BoxVisual(d, "ElevatorDoorR", V(0.78f, 1.6f, -7.47f), V(1.5f, 3.1f, 0.08f), MatKey.Gold, 0.02f);
            Sign(d, V(0f, 4.75f, -7.9f), Vector3.forward, "VAULT LEVEL\n<size=60%>(DOWN FROM THE EXAM)</size>", 3.6f, 1.0f);
            ctx.Label(d, V(0f, 3.6f, -7.44f), Face(Vector3.forward), "B7  v", 0.42f, Gold, 2f);

            // TOOL LOCKER: SWAP-O-MATIC (west, faces east)
            var lockerPos = V(-6f, 0f, 2f);
            var locker = WeaponKit.ToolLocker(ctx.Gameplay, lockerPos, FaceAt(lockerPos, V(-2f, 0f, 2f)), ExperimentalVariant.SwapOMatic);
            ctx.Mark("s0.locker", locker);
            ctx.MarkPoint("s0.lockerStand", V(-4.9f, 0f, 2f), 90f);
            Sign(d, V(-7.95f, 2.0f, 5.0f), Vector3.right, "TRADES PLACES WITH ANYTHING\nUNDER 200 KG. NOT INTERNS IN A HURRY.", 2.6f, 0.7f, SignStyle.Warning);
            StoryKit.LineSpot(d, V(-5f, 0f, 2f), 3.2f, "The SWAP-O-MATIC trades places with anything under 200 kilos. Please don't swap with me. I'm a coffee machine.");

            // practice target: TIP JAR on a 3 m shelf with steps down (east)
            Solid(g, 4f, 7.95f, 5f, 7f, 3f, 0f, MatKey.Marble, "TipShelf");
            for (int i = 0; i < 8; i++)
            {
                float top = 3f - (i + 1) * (3f / 9f);
                Solid(g, 6.35f, 7.95f, 5f - 0.42f * (i + 1), 5f - 0.42f * i, top, 0f, MatKey.FloorAlt, "TipStep");
            }
            var jar = PuzzleKit.Crate(ctx.Props, V(5f, 3f, 6f));
            Reskin(jar, "tip jar", L8Art.Jar(), MaterialLibrary.Get(MatKey.Glass, LevelTheme.Bank), V(0f, 0.05f, 0f));
            ProcMesh.Part("Coins", jar.transform, L8Art.Coins(), Kit.Mat(MatKey.Gold), V(0f, 0.05f, 0f), null, null, false);
            ctx.Label(jar.transform, V(0f, 0.15f, -0.36f), Quaternion.identity, "TIPS\n<size=60%>(DO NOT SWAP)</size>", 0.16f, Gold, 0.7f);
            jar.gameObject.AddComponent<L8TipJar>().Body = jar;
            ctx.Mark("s0.tipJar", jar.gameObject);
            Sign(d, V(4.6f, 4.3f, 7.05f), Vector3.back, "PRACTICE TARGET\n<size=65%>SWAP WITH THE TIP JAR</size>", 2.8f, 0.9f);
            ctx.Tutorial(V(-2f, 1.5f, 2f), V(12f, 3f, 9f), "ch07_swap", "SWAP-O-MATIC",
                         "SWAP-O-MATIC: shoot something to trade places with it. Not people in a hurry.", new[] { "LMB" }, false);

            // dressing: deposit wall, columns, ropes, ME-TV, posters
            EnvKit.Decor.Bank.SafeDepositWall(d, V(-7.75f, 0f, -4.2f), Face(Vector3.right), 8, 6, 0.45f, true, "SAFE DEPOSIT (MINE)");
            foreach (var x in new[] { -6.9f, 6.9f })
                foreach (var z in new[] { -6.9f, 9.3f })
                    EnvKit.Decor.Bank.MarbleColumn(g, V(x, 0f, z), null, 6f, 1.0f, true, true);
            EnvKit.Decor.Bank.VelvetRopeLine(d, V(-3.2f, 0f, -2.2f), V(-3.2f, 0f, 6.5f), 2.2f, null, null);
            EnvKit.Decor.Bank.VelvetRopeLine(d, V(3.2f, 0f, -2.2f), V(3.2f, 0f, 3.6f), 2.2f, null, null);
            ctx.Mark("story.metv", StoryKit.MeTvScreen(d, V(-5.3f, 3.7f, 10.25f), Face(Vector3.back), 3.6f));
            StoryKit.HoloPoster(d, V(5.4f, 0f, 9.9f), Face(Vector3.back), "COLOR IS A LUXURY.\nLUXURY IS FOR ME.", 2);
            EnvKit.Decor.Bank.CoinPile(d, V(-6.2f, 0f, 7.6f), null, 1.0f, 0.5f, 3);
            Panel(d, V(0f, 5.6f, -7.95f), Vector3.forward, Cream, true, 2.6f, 16f, 3f, 0.4f);
            Panel(d, V(7.95f, 4.6f, 0f), Vector3.left, Gold, false);
            Panel(d, V(-7.95f, 4.6f, -1f), Vector3.right, Gold, false);
        }

        void BuildS1(LevelBuildContext ctx)
        {
            var g = ctx.Section("S1_LaserGallery", Vector3.zero);
            var d = ctx.Decor;
            Floor(g, -2.5f, 2.5f, 10.5f, 44.6f, 0f, MatKey.Marble);
            Wall(g, -2.7f, 10.5f, -2.7f, 44.6f, 4.2f);
            Wall(g, 2.7f, 10.5f, 2.7f, 44.6f, 4.2f);
            for (float z = 16f; z < 44f; z += 7f)
            {
                EnvKit.Decor.Bank.MarbleColumn(g, V(-2.3f, 0f, z), null, 4.2f, 0.5f, false, false);
                EnvKit.Decor.Bank.MarbleColumn(g, V(2.3f, 0f, z), null, 4.2f, 0.5f, false, false);
            }

            // timed button (stand z 12.5) + three full-width 3-beam LASER CURTAINS
            var button = PuzzleKit.TimedButton(ctx.Puzzles, V(0f, 0f, 13f), Yaw(180f), RouteLength,
                                               new[] { V(0.9f, 0f, 13.6f), V(0.4f, 0f, 40.5f), V(0f, 0f, 42f) }, "");
            ctx.Mark("s1.timed", button);
            ctx.MarkPoint("s1.timedStand", V(0f, 0f, 12.4f), 0f);
            var curtains = new Behaviour[CurtainZ.Length];
            for (int i = 0; i < CurtainZ.Length; i++)
            {
                var lb = PuzzleKit.LaserBarrier(ctx.Hazards, V(0f, 0f, CurtainZ[i]), Quaternion.identity, 5.0f, 3);
                curtains[i] = lb;
                ctx.Mark("s1.curtain." + (i + 1), lb);
            }
            var lever = PuzzleKit.Lever(ctx.Puzzles, V(0f, 0f, 43f), Yaw(180f), 0, true, false, "LASER POWER: OFF");
            ctx.Mark("s1.lever", lever);
            ctx.MarkPoint("s1.leverStand", V(0f, 0f, 42f), 0f);
            var toggle = PuzzleKit.HazardToggle(ctx.Puzzles, curtains, false, button, lever);
            toggle.Logic = SignalLogic.Any;
            ctx.Mark("s1.laserToggle", toggle);
            ctx.MarkPoint("s1.run.1", V(0.9f, 0f, 14.2f));
            ctx.MarkPoint("s1.run.2", V(0.3f, 0f, 40.8f));
            Sign(d, V(2.45f, 2.3f, 43f), Vector3.left, "LASER POWER\n<size=70%>PULL = OFF</size>", 1.6f, 0.8f, SignStyle.Warning);

            // SWAP alternative: deposit box behind the last curtain + SWAP SPOT 22.5 m away
            Solid(g, 0.9f, 2.5f, 43.6f, 45.2f, 1.4f, 0f, MatKey.Marble, "BoxShelf");
            var box = PuzzleKit.Crate(ctx.Props, V(1.7f, 1.4f, 44.4f));
            Reskin(box, "deposit box", ProcMesh.CachedBox(V(0.86f, 0.62f, 0.86f)), Kit.Mat(MatKey.Gold), V(0f, -0.14f, 0f));
            ctx.Label(box.transform, V(0f, -0.1f, -0.44f), Quaternion.identity, "DEPOSIT\nBOX", 0.16f, UIColors.Hex(0x2A1E0E), 0.8f);
            ctx.Mark("s1.depositBox", box.gameObject);
            Disc(d, V(0f, 0f, 22f), 0.75f, MatKey.HazardYellow, "SwapSpot");
            FloorLabel(d, V(0f, 0f, 20.9f), "SWAP SPOT", 0.4f, UIColors.Hex(0x1A1A1A), 0f, 2.2f);
            ctx.MarkPoint("s1.swapSpot", V(0f, 0f, 22f), 0f);

            // story + teaching
            StoryKit.ScriptTrigger(ctx.Gameplay, V(0f, 1.5f, 11.6f), V(5f, 3f, 2f), "ch07.curtain");
            Sign(d, V(0f, 5.2f, 10.27f), Vector3.back, "LASER HALL\n<size=60%>PLEASE DO NOT DANCE</size>", 3.0f, 1.0f, SignStyle.Danger);
            Sign(d, V(-2.45f, 2.6f, 18.5f), Vector3.right, "MIND YOUR MUSTACHE\n<size=60%>LASERS BURN DISGUISES</size>", 2.0f, 0.9f, SignStyle.Warning);
            Sign(d, V(2.45f, 2.6f, 27.5f), Vector3.left, "SMILE, YOU'RE\nON CAMERA", 1.8f, 0.8f);
            ctx.Hint(V(0f, 1.5f, 15f), V(5f, 3f, 8f), "Only TURBO LEGS beats the curtain. Or swap with the deposit box behind it.", "Fast or sneaky.");
            Panel(d, V(-2.45f, 3.6f, 30f), Vector3.right, VaultRed, true, 2.2f, 14f);
            Panel(d, V(2.45f, 3.6f, 20f), Vector3.left, VaultRed, false);
            Panel(d, V(2.45f, 3.6f, 38f), Vector3.left, VaultRed, false);

            // S1b END ROOM (C1) + NO-SWAP stripe
            var e = ctx.Section("S1b_EndRoom", Vector3.zero);
            Floor(e, -4.2f, 4.2f, 44.6f, 52.4f, 0f, MatKey.Marble);
            Wall(e, -4.4f, 44.6f, -4.4f, 52.4f, 5f);
            Wall(e, 4.4f, 44.6f, 4.4f, 52.4f, 5f);
            Wall(e, -4.4f, 44.6f, -2.7f, 44.6f, 5f);
            Wall(e, 2.7f, 44.6f, 4.4f, 44.6f, 5f);
            Wall(e, -4.4f, 52.4f, -2.2f, 52.4f, 5f);
            Wall(e, 2.2f, 52.4f, 4.4f, 52.4f, 5f);
            PuzzleKit.NoToolZone(ctx.Puzzles, V(0f, 2f, 45.8f), V(8.4f, 4f, 0.8f), ToolMask.SwapOMatic);
            PuzzleVisuals.StripedBox(d, "NoSwapStripe", V(0f, 0.01f, 45.8f), V(8.2f, 0.02f, 0.6f), 0, 0.35f, false);
            FloorLabel(d, V(0f, 0f, 46.6f), "NO SWAPPING PAST THIS LINE", 0.3f, UIColors.Hex(0xC08BFF), 0f, 6f);
            ctx.Checkpoint(V(0f, 0f, 48.8f), 0f);
            Sign(d, V(-4.15f, 2.8f, 49f), Vector3.right, "COLOR IS A LUXURY.\nLUXURY IS FOR ME.\n<size=55%>- MAX ME</size>", 2.2f, 1.2f);
            EnvKit.Decor.Bank.CoinPile(d, V(3.2f, 0f, 51.2f), null, 0.7f, 0.4f, 5);

            // corridor to the rotunda
            Floor(e, -2.2f, 2.2f, 52.4f, 54.0f, 0f, MatKey.Marble);
            Wall(e, -2.4f, 52.4f, -2.4f, 54.6f, 5f);
            Wall(e, 2.4f, 52.4f, 2.4f, 54.6f, 5f);
        }
    }
}
