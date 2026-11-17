// =====================================================================================
//  SHIFTERS - L11Builder.cs   [SHIFT 10 - THE ME-MORIAL PARK, LevelIndex 10, scene L11_MeMorialPark]
//  A foggy corporate cemetery under moonlight. Sections along +Z (spec: LEVELS2.md "SHIFT 10"):
//    S0 THE BACK GATE        x -8..8,   z -10..4   spawn (0,0,-5), executive elevator back doors, gate arch, dummy
//    S1 "STAND TOGETHER"     z 4..22                three 40 kg plates -> Latch(AND) huddle gate (0,0,15) -> C1 (0,0,19)
//    S2 THE GRAVES           x -2.5..2.5, z 22..57  3 open graves (1.6 m, crumbling FRESH SOIL, foam bottom), ghost
//                                                    handshake hands, the 5.0 m GHOST BRIDGE (WholeTeam zone) -> C2 (0,0,55)
//    S3 THE MAUSOLEUM        x -6..6,   z 57..71   RECENTLY REPRINTED tombstones, mausoleum (C09 trust fall), EXIT
//  Deviation from the spec coordinates (documented in the report): the walk-in mausoleum's front is at z 65 and the
//  exit cab at (0,0,71) so the 6-slot tombstone row (1.5 m spacing = 7.5 m) fits beside the plaza.
//  Night rules (G10): no Lost hazards, every pit is <= 2.5 m deep with a foam bottom, walkable edges glow (TrimGlow),
//  one real pool light (S1 lamp post) + flashlight + checkpoint lamps.
//  Section files: L11Builder.cs (shared + S0/S1), L11Builder.S2S3.cs.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L11Builder : ILevelBuilder
    {
        public int LevelIndex => 10;

        // ---- shared metrics (the solution and the checks read these)
        public const float PathHalf = 2.5f;
        public const float PitDepth = 2.5f;
        public const float GraveLen = 1.6f;
        public static readonly float[] GraveZ = { 26f, 31f, 36f };
        public const float GapNear = 46f, GapFar = 51f;          // ghost bridge gap (5.0 m)
        public const float ZoneZ0 = 40f, ZoneZ1 = 57f;           // ghost watch zone (whole team)
        public const float HandsZ0 = 40f, HandsZ1 = 45f;         // handshake strips (x +-2..2.5)
        public const float PlazaZ0 = 57f, MausoleumZ = 65f, MausoleumDepth = 6.3f, ExitZ = 71f;
        public const float FenceH = 3f;
        public static readonly float[] LaneX = { -1.5f, 0f, 1.5f };     // L C R grave lanes
        public static readonly float[] BridgeLaneX = { -1.2f, 0f, 1.2f };
        public static readonly string[] Lane = { "L", "C", "R" };

        LevelBuildContext _ctx;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            BuildS0S1(ctx);
            BuildS2(ctx);
            BuildS3(ctx);
            BuildBackdrop(ctx);
            FlashlightRig.Ensure(ctx.Lights);
        }

        // ------------------------------------------------------------------ helpers
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);
        /// <summary>Viewer looks +X (reads a piece on an east wall / east side): EnvKit convention rot = viewer's look.</summary>
        static Quaternion LookEast => EnvKit.ViewerLooking(Vector3.right);
        static Quaternion LookWest => EnvKit.ViewerLooking(Vector3.left);

        BoxCollider Slab(Transform p, float x0, float x1, float z0, float z1, float top, float thick, MatKey mat, bool trim, string name)
            => _ctx.Platform(p, new Vector3((x0 + x1) * 0.5f, top, (z0 + z1) * 0.5f), new Vector3(x1 - x0, thick, z1 - z0), mat, trim, name);

        void Fence(Transform p, float x0, float z0, float x1, float z1, float h = FenceH)
            => EnvKit.Decor.Night.IronFenceLine(p, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), h, true);

        /// <summary>Glowing edge strip across X at z (walkable-edge glow, G10).</summary>
        void EdgeGlowX(Transform p, float x0, float x1, float z, MatKey mat = MatKey.TrimGlow)
            => Kit.BoxVisual(p, "EdgeGlow", new Vector3((x0 + x1) * 0.5f, 0.012f, z), new Vector3(x1 - x0, 0.025f, 0.12f), mat, 0f, null, false);

        void EdgeGlowZ(Transform p, float x, float z0, float z1, MatKey mat = MatKey.TrimGlow)
            => Kit.BoxVisual(p, "EdgeGlow", new Vector3(x, 0.012f, (z0 + z1) * 0.5f), new Vector3(0.12f, 0.025f, z1 - z0), mat, 0f, null, false);

        GameObject Grave(Transform p, Vector3 pos, Quaternion rot, bool collider = false)
            => EnvKit.Decor.Night.Gravestone(p, pos, rot, null, -1, 1f, collider);

        // ================================================================== S0 + S1
        void BuildS0S1(LevelBuildContext ctx)
        {
            var s0 = ctx.Section("S0_BackGate", Vector3.zero);
            var s1 = ctx.Section("S1_StandTogether", Vector3.zero);

            // ground: courtyard + the inner yard behind the gate (C1)
            Slab(s0, -8f, 8f, -10.5f, 15f, 0f, 0.6f, MatKey.Concrete, true, "CourtyardFloor");
            Slab(s1, -4f, 4f, 15f, 22f, 0f, 0.6f, MatKey.Concrete, true, "InnerYardFloor");
            Kit.BoxVisual(s0, "GravelPath", new Vector3(0f, 0.008f, 4f), new Vector3(3.2f, 0.016f, 21f), MatKey.FloorAlt, 0f, null, false);

            // perimeter fences (3 m: not stack-climbable)
            Fence(s0, -8f, -10.5f, -8f, 15f); Fence(s0, 8f, -10.5f, 8f, 15f);
            Fence(s0, -8f, -10.5f, -2.7f, -10.5f); Fence(s0, 2.7f, -10.5f, 8f, -10.5f);
            Fence(s1, -8f, 15f, -2.6f, 15f); Fence(s1, 2.6f, 15f, 8f, 15f);
            Fence(s1, -4f, 15f, -4f, 22f); Fence(s1, 4f, 15f, 4f, 22f);
            Fence(s1, -4f, 22f, -PathHalf - 0.1f, 22f); Fence(s1, PathHalf + 0.1f, 22f, 4f, 22f);

            // the executive elevator's back doors (we came out the back)
            var el = Kit.Make("ExecElevatorBack", s0, new Vector3(0f, 0f, -10.5f), Quaternion.identity).transform;
            Kit.SolidBox(el, "Back", new Vector3(0f, 2.3f, -2.2f), new Vector3(5.6f, 4.6f, 0.3f), MatKey.MetalDark, 0.04f);
            Kit.SolidBox(el, "SideL", new Vector3(-2.75f, 2.3f, -1.1f), new Vector3(0.3f, 4.6f, 2.2f), MatKey.MetalDark, 0.04f);
            Kit.SolidBox(el, "SideR", new Vector3(2.75f, 2.3f, -1.1f), new Vector3(0.3f, 4.6f, 2.2f), MatKey.MetalDark, 0.04f);
            Kit.SolidBox(el, "Roof", new Vector3(0f, 4.75f, -1.1f), new Vector3(5.8f, 0.3f, 2.6f), MatKey.MetalDark, 0.04f);
            Kit.BoxVisual(el, "Floor", new Vector3(0f, 0.02f, -1.1f), new Vector3(5.2f, 0.04f, 2.1f), MatKey.Gold, 0f);
            Kit.BoxVisual(el, "DoorL", new Vector3(-2.2f, 2.1f, 0.05f), new Vector3(1.0f, 4.1f, 0.1f), MatKey.Gold, 0.02f);
            Kit.BoxVisual(el, "DoorR", new Vector3(2.2f, 2.1f, 0.05f), new Vector3(1.0f, 4.1f, 0.1f), MatKey.Gold, 0.02f);
            Kit.BoxVisual(el, "CeilGlow", new Vector3(0f, 4.55f, -1.1f), new Vector3(3.6f, 0.04f, 1.2f), MatKey.LightOn, 0f, null, false);
            EnvKit.Sign(el, new Vector3(0f, 5.4f, 0.1f), Quaternion.identity, "EXECUTIVE ELEVATOR - BACK DOOR\nAUTHORIZED PERSONNEL: ME", new Vector2(5.2f, 0.9f), SignStyle.Info);

            var spawns = ctx.SetSpawn(new Vector3(0f, 0f, -5f), 0f);
            var startTrig = StoryKit.ScriptTrigger(s0, new Vector3(0f, 1.5f, -5f), new Vector3(10f, 3f, 6f), "ch10.start", true);
            ctx.Mark("s0.start", startTrig.gameObject);

            // gate arch + signs + ME-TV + poster + the watching dummy
            L11Kit.Arch(s0, new Vector3(0f, 0f, 3f), 4.6f, 4.8f, "ME-MORIAL PARK\n<size=60%>FOR TEAMS THAT WERE LET GO</size>", new Vector2(6.4f, 1.5f));
            EnvKit.Sign(s0, new Vector3(7.85f, 2.1f, -3f), LookEast, "VISITING HOURS: NEVER\n<size=70%>(SHIFT CHANGE IN: NEVER)</size>", new Vector2(3.2f, 1.1f), SignStyle.Info);
            var tv = StoryKit.MeTvScreen(s0, new Vector3(5.4f, 3.4f, 1.5f), EnvKit.FacingViewerAt(new Vector3(5.4f, 3.4f, 1.5f), new Vector3(0f, 1.7f, -4.5f)), 3.2f);
            ctx.Mark("story.metv", tv);
            StoryKit.HoloPoster(s0, new Vector3(-6.4f, 0f, 4.9f), LookWest, "TRUST NO ONE. ESPECIALLY ON TRUST FALLS.", 3);
            var dummyRot = Yaw(250f);   // DummyModel faces local -Z: this turns it toward the spawn path
            var dummy = DummyModel.Create(s0, new Vector3(-5.6f, 0f, -0.5f), dummyRot, false);
            var dummyMild = DummyModel.Create(s0, new Vector3(-5.6f, 0f, -0.5f), dummyRot, true);
            var watcher = Kit.Make("L11DummyWatcher", s0, new Vector3(-5.6f, 0f, -0.5f)).AddComponent<L11DummyWatcher>();
            watcher.Dummy = dummy; watcher.MildDummy = dummyMild;
            if (dummyMild != null) dummyMild.gameObject.SetActive(false);
            Kit.BoxVisual(s0, "DummyPlinth", new Vector3(-5.6f, 0.06f, -0.5f), new Vector3(1.1f, 0.12f, 1.1f), MatKey.Concrete, 0.02f);
            ctx.Label(s0, new Vector3(-5.0f, 0.3f, -0.3f), EnvKit.FacingViewerAt(new Vector3(-5.0f, 0.3f, -0.3f), new Vector3(0f, 1.6f, -4f)), "GREETER\n<size=60%>(HAS NOT BLINKED SINCE 1999)</size>", 0.16f, new Color(0.85f, 0.88f, 0.95f), 1.2f);
            for (int s = -1; s <= 1; s += 2) Grave(s0, new Vector3(s * 6.4f, 0f, -7.5f), s < 0 ? LookWest : LookEast, true);

            // ---------------- S1: the huddle gate
            var plateA = PuzzleKit.PressurePlate(ctx.Puzzles, new Vector3(-2f, 0f, 8f), Quaternion.identity, 40f, false, "");
            var plateB = PuzzleKit.PressurePlate(ctx.Puzzles, new Vector3(2f, 0f, 8f), Quaternion.identity, 40f, false, "");
            var plateC = PuzzleKit.PressurePlate(ctx.Puzzles, new Vector3(0f, 0f, 11.5f), Quaternion.identity, 40f, false, "");
            ctx.Mark("s1.A", plateA.gameObject); ctx.Mark("s1.B", plateB.gameObject); ctx.Mark("s1.C", plateC.gameObject);
            var gate = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 15f), Quaternion.identity, DoorMode.SlideUp, 4f, 3.6f, plateA, plateB, plateC);
            gate.Logic = SignalLogic.Latch;
            ctx.Mark("s1.gate", gate.gameObject);
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.SolidBox(s1, "GatePillar", new Vector3(s * 2.75f, 2.1f, 15f), new Vector3(0.6f, 4.2f, 0.7f), MatKey.WallAlt, 0.05f);
                Kit.BoxVisual(s1, "PillarCap", new Vector3(s * 2.75f, 4.3f, 15f), new Vector3(0.8f, 0.2f, 0.9f), MatKey.Concrete, 0.03f);
                PuzzleKit.Cable(ctx.Puzzles, new Vector3(s * 2f, 0f, 8.9f), new Vector3(s * 2.4f, 0f, 14.6f), s < 0 ? (SignalSource)plateA : plateB);
            }
            PuzzleKit.Cable(ctx.Puzzles, new Vector3(0.2f, 0f, 12.4f), new Vector3(0.2f, 0f, 14.6f), plateC);
            var gateBeat = gate.gameObject.AddComponent<L11BeatOnPowered>();
            gateBeat.Receiver = gate; gateBeat.BeatId = "ch10.gate"; gateBeat.FloatText = "HUDDLE DETECTED.";
            EnvKit.Sign(s1, new Vector3(-5.3f, 2.2f, 14.8f), Quaternion.identity, "PLEASE DO NOT DISTURB\nTHE DISBANDED", new Vector2(3.6f, 1.1f), SignStyle.Info);
            EnvKit.Sign(s1, new Vector3(5.3f, 2.2f, 14.8f), Quaternion.identity, "HUDDLE GATE\n<size=70%>ONE INTERN PER PLATE. ALL AT ONCE.</size>", new Vector2(3.6f, 1.1f), SignStyle.Warning);
            ctx.Label(s1, new Vector3(0f, 0.03f, 9.6f), EnvKit.ViewerLooking(Vector3.down, Vector3.forward), "STAND TOGETHER", 0.42f, L11Kit.GhostBlue, 5f);

            // the one real light: a lamp-post pool over the plates
            L11Kit.LampPost(s1, new Vector3(3.6f, 0f, 11f));
            L11Kit.LampPost(s1, new Vector3(-3.6f, 0f, 11f));
            PuzzleKit.LightPool(ctx.Lights, new Vector3(0f, 0f, 9.3f), 3.2f, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Grave(s1, new Vector3(s * 6.6f, 0f, 6.5f), s < 0 ? LookWest : LookEast, true);
                Grave(s1, new Vector3(s * 6.6f, 0f, 9.8f), s < 0 ? LookWest : LookEast, true);
                Grave(s1, new Vector3(s * 6.6f, 0f, 13f), s < 0 ? LookWest : LookEast, true);
            }

            ctx.Tutorial(new Vector3(0f, 1.5f, 4.8f), new Vector3(15f, 3f, 4f), "ch10_huddle", "STAND TOGETHER",
                "Some doors only open for a team. Stand together.", null, false);
            ctx.Hint(new Vector3(0f, 2f, 9f), new Vector3(15f, 4f, 11f), "One intern on each plate.", "Huddle up.");
            ctx.Checkpoint(new Vector3(0f, 0f, 19f), 0f);
            ctx.MarkPoint("s1.gateFar", new Vector3(0f, 0f, 16.8f));
        }
    }
}
