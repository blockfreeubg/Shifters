// =====================================================================================
//  SHIFTERS - L7Builder.cs   [SHIFT 04 - ACT NATURAL, LevelIndex 6, scene L7_ActNatural]
//  The First & Only Bank of ME during business hours. Teamwork-detecting cameras (SecurityCamera
//  mode Teamwork + AlarmLatch + EscortBack): two or more interns within 3 m inside one cone for
//  0.5 s = TEAMWORK DETECTED -> fade, the team is escorted back to the section's return marks.
//  Sections along +Z (LEVELS2 "SHIFT 04"):
//    S0  The Street        x -10..10, z -10..4   spawn, facade, practice camera Q0, tutorial
//    S1  The Queue (lobby) x -12..12, z 6..32    pillars, Q1 (post) + Q2 (back wall), P1 / gate G1 / lever L1
//    S1b Security vestibule x -6..6, z 32..40    C1 (no cameras)
//    S2  The Teller Line   x -12..12, z 40..64   counter, 9 m gold Max statue (blind spot), T1 / T2,
//                                                stack + climb, STAFF GATE, keycard printer, ONE-AT-A-TIME staff door
//    S3  Staff corridor    x -4..4, z 64..76     C2, exit "BASEMENT - OLD LAB"
//  Section files: L7Builder.cs (S0, S1), L7Builder.S2.cs (S2, S3, story dressing).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public partial class L7Builder : ILevelBuilder
    {
        public int LevelIndex => 6;

        public const float WallH = 7.5f;
        public const float FloorT = 0.6f;

        // ---- shared metrics (solution + checks read these)
        public static readonly Vector3 SpawnPos = new Vector3(0f, 0f, -7f);
        public static readonly Vector3 Q0Pos = new Vector3(0f, 4.5f, 3.4f);
        public static readonly Vector3 Q1Pos = new Vector3(-10.5f, 4.5f, 22f);
        public static readonly Vector3 Q2Pos = new Vector3(0f, 5.4f, 31.3f);
        public static readonly Vector3 T1Pos = new Vector3(9.5f, 4.5f, 41.5f);
        public static readonly Vector3 T2Pos = new Vector3(10.2f, 4.5f, 62.8f);
        public const float Q0Range = 8.5f, Q1Range = 22f, Q2Range = 14f, T1Range = 22f, T2Range = 12f;
        public const float CounterFaceZ = 52f, CounterDepth = 1.4f, CounterH = 2.5f;
        public static readonly Vector3 StatuePos = new Vector3(-6f, 0f, 49f);
        public const float PlinthH = 1f, PlinthW = 3f, StatueBlockW = 2.8f, StatueBlockH = 8f;
        public static readonly Vector3 StackSpot = new Vector3(-7.25f, 0f, 51.3f);
        public const float GateButtonSeconds = 8f;
        /// <summary>Blind-spot study area (the IQ hatching is computed inside it).</summary>
        public const float BlindX0 = -12f, BlindX1 = -4f, BlindZ0 = 44f, BlindZ1 = 52f;

        LevelBuildContext _ctx;
        Transform _s0, _s1, _s2, _s3, _decor;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            _decor = ctx.Decor;
            _s0 = ctx.Section("S0_TheStreet", Vector3.zero);
            _s1 = ctx.Section("S1_TheQueue", Vector3.zero);
            _s2 = ctx.Section("S2_TheTellerLine", Vector3.zero);
            _s3 = ctx.Section("S3_StaffCorridor", Vector3.zero);
            BuildS0(ctx);
            BuildS1(ctx);
            BuildS2(ctx);
            BuildS3(ctx);
            BuildDirector(ctx);
            EnvKit.VoidBackdrop(ctx.Decor, LevelTheme.Bank, new Bounds(new Vector3(0f, 0f, 33f), new Vector3(40f, 20f, 92f)), 44);
        }

        // ================================================================== S0 THE STREET
        SecurityCamera _q0, _q1, _q2, _t1, _t2;
        AlarmLatch _a0, _a1, _a2, _a3;

        void BuildS0(LevelBuildContext ctx)
        {
            var g = _s0;
            // street + sidewalk + marble apron ("entrance steps" z -2..4)
            Floor(g, -10f, 10f, -10.5f, -2f, 0f, MatKey.Concrete);
            Floor(g, -10f, 10f, -2f, 4f, 0f, MatKey.Marble);
            Floor(g, -6f, 6f, 4f, 6f, 0f, MatKey.Marble);   // entrance threshold under the facade
            for (int i = 0; i < 3; i++)   // painted gold step edges on the apron
                Kit.BoxVisual(_decor, "StepEdge", new Vector3(0f, 0.012f, -1.6f + i * 1.9f), new Vector3(14f, 0.02f, 0.14f), MatKey.Gold, 0f, null, false);
            // street enclosure: building fronts (west, east, south)
            Wall(g, -10.2f, -10.7f, -10.2f, 4f, 6f);
            Wall(g, 10.2f, -10.7f, 10.2f, 4f, 6f);
            Wall(g, -10.2f, -10.7f, 10.2f, -10.7f, 6f);
            // bank facade (z 4..6) with the 12 m entrance x -6..6, header y 5..8
            ctx.Block(g, new Vector3(-9f, 4f, 5f), new Vector3(6f, 8f, 2f), MatKey.Marble, "FacadeW");
            ctx.Block(g, new Vector3(9f, 4f, 5f), new Vector3(6f, 8f, 2f), MatKey.Marble, "FacadeE");
            ctx.Block(g, new Vector3(0f, 6.5f, 5f), new Vector3(12f, 3f, 2f), MatKey.Marble, "FacadeHeader");
            Kit.BoxVisual(_decor, "FacadeCornice", new Vector3(0f, 8.1f, 4.9f), new Vector3(24.4f, 0.3f, 2.4f), MatKey.Gold, 0.03f);
            for (int s = -1; s <= 1; s += 2)
                EnvKit.Decor.Bank.MarbleColumn(_decor, new Vector3(s * 6.6f, 0f, 3.55f), null, 5f, 0.9f, true, false);
            EnvKit.Sign(_decor, new Vector3(0f, 6.6f, 3.94f), Quaternion.identity, "FIRST & ONLY BANK OF ME", new Vector2(10.5f, 1.5f), SignStyle.Info);
            EnvKit.Sign(_decor, new Vector3(0f, 5.45f, 3.94f), Quaternion.identity, "VALUED CUSTOMERS: NO TEAMWORK ON THE PREMISES", new Vector2(9f, 0.5f), SignStyle.Warning);
            EnvKit.Sign(_decor, new Vector3(8.4f, 4.75f, 3.94f), Quaternion.identity, "NO LOITERING.\nNO TEAMWORK.\nNO EXCEPTIONS.", new Vector2(3.2f, 1.7f), SignStyle.Danger);
            RevolvingDoor(_decor, new Vector3(-8f, 0f, 3.1f));
            RevolvingDoor(_decor, new Vector3(8f, 0f, 3.1f));
            ctx.Label(_decor, new Vector3(-8f, 3.05f, 3.92f), Quaternion.identity, "REVOLVING DOOR\n(SPINS ALONE)", 0.24f, UIColors.AccentYellow, 3f);
            // ME-TV ad + posters
            var tv = StoryKit.MeTvScreen(_decor, new Vector3(-8f, 5.4f, 3.75f), null, 3.4f);
            Headline(tv, "YOUR MONEY IS SAFE WITH ME.\nBECAUSE IT'S MY MONEY NOW.");
            ctx.Mark("story.metv", tv);
            StoryKit.HoloPoster(_decor, new Vector3(6.5f, 0f, -6f), EnvKit.FacingViewer(Vector3.left), "OPEN A SOLO ACCOUNT TODAY", 2);
            StoryKit.HoloPoster(_decor, new Vector3(-7.5f, 0f, -8.5f), EnvKit.FacingViewer(new Vector3(1f, 0f, 1f)), StoryKitPoster(1), 2);
            EnvKit.Poster(_decor, new Vector3(9.98f, 2.2f, -4f), EnvKit.WallMountFacing(Vector3.left), "INTEREST RATE: 0%", "(FOR YOU)", new Vector2(1.4f, 1.9f));
            EnvKit.Poster(_decor, new Vector3(-9.98f, 2.2f, -1f), EnvKit.WallMountFacing(Vector3.right), "SMILE!", "YOU'RE ON ME-TV.", new Vector2(1.4f, 1.9f));

            ctx.SetSpawn(SpawnPos, 0f);
            var r0 = new[] {
                ctx.MarkPoint("s0.return.0", new Vector3(-2f, 0f, -7f)),
                ctx.MarkPoint("s0.return.1", new Vector3(0f, 0f, -7.6f)),
                ctx.MarkPoint("s0.return.2", new Vector3(2f, 0f, -7f)) };
            for (int i = 0; i < 3; i++)
            {
                float x = -4f + 4f * i;
                ctx.MarkPoint("s0.lane." + i, new Vector3(x, 0f, -3f));
                ctx.MarkPoint("s1.entry." + i, new Vector3(x, 0f, 8.5f));
            }

            // practice camera Q0 under the header, watching the steps
            _q0 = EnemyKit.SecurityCamera(ctx.Enemies, Q0Pos, Quaternion.LookRotation(Vector3.back), CameraDetect.Teamwork, 30f, 6f, Q0Range, 40f, 0f);
            ctx.Mark("s0.Q0", _q0.gameObject);
            _a0 = Latch(ctx, "s0.alarm", new Vector3(0f, 7.6f, 3.9f), r0, false, _q0);
            SweepChip(g, "s0.chip", _q0, new Vector3(0f, 0f, 0.5f), "ENTRANCE");

            ctx.Tutorial(new Vector3(0f, 1.5f, -6f), new Vector3(18f, 3f, 8f), "ch04_actnatural", "ACT NATURAL",
                "Cameras flag TEAMWORK: two or more interns within 3 m inside a cone. One intern alone is fine. " +
                "Smart eyes see blind spots. Fast eyes see sweep timers.", new[] { "TAB", "Q", "E" }, true, g);
            StoryKit.ScriptTrigger(g, new Vector3(0f, 1.5f, -4.2f), new Vector3(20f, 3f, 2.5f), "ch04.disguise", true);
        }

        // ================================================================== S1 THE QUEUE
        void BuildS1(LevelBuildContext ctx)
        {
            var g = _s1;
            Floor(g, -12f, 12f, 6f, 32f, 0f, MatKey.Marble);
            Wall(g, -12.2f, 6f, -12.2f, 32.2f);
            Wall(g, 12.2f, 6f, 12.2f, 32.2f);
            WallWithGap(g, 32f, -12.2f, 12.2f, -2f, 2f);
            // ceiling beams + emissive chandeliers (no Light components)
            for (int i = 0; i < 2; i++)
            {
                float z = 13f + i * 11f;
                Kit.BoxVisual(_decor, "Beam", new Vector3(0f, WallH - 0.2f, z), new Vector3(24.4f, 0.4f, 0.5f), MatKey.Gold, 0.03f);
                EnvKit.Decor.Bank.Chandelier(_decor, new Vector3(-4f, WallH - 0.4f, z), null, 1.1f, 1.1f, 2, false);
                EnvKit.Decor.Bank.Chandelier(_decor, new Vector3(4f, WallH - 0.4f, z), null, 1.1f, 1.1f, 2, false);
            }
            // PILLAR ROW (cover) at x 8
            for (int i = 0; i < 4; i++)
                EnvKit.Decor.Bank.MarbleColumn(g, new Vector3(8f, 0f, 10f + 5f * i), null, 6f, 1.2f, false, true);
            // velvet-rope queue maze (collider-free)
            EnvKit.Decor.Bank.VelvetRopeLine(_decor, new Vector3(-8f, 0f, 12f), new Vector3(4f, 0f, 12f), 2.5f, "PLEASE WAIT ALONE", new Vector3(0f, 0f, -1f));
            EnvKit.Decor.Bank.VelvetRopeLine(_decor, new Vector3(-4f, 0f, 17f), new Vector3(6f, 0f, 17f), 2.5f, null, null);
            EnvKit.Decor.Bank.VelvetRopeLine(_decor, new Vector3(-8f, 0f, 22f), new Vector3(4f, 0f, 22f), 2.5f, "ONE (1) CUSTOMER PER CUSTOMER", new Vector3(0f, 0f, -1f));
            // signs
            EnvKit.Sign(_decor, new Vector3(-11.95f, 3.4f, 12f), EnvKit.WallMountFacing(Vector3.right), "INTEREST RATE:\n0% (FOR YOU)", new Vector2(3.4f, 1.5f), SignStyle.Info);
            EnvKit.Sign(_decor, new Vector3(11.95f, 3.4f, 13f), EnvKit.WallMountFacing(Vector3.left), "PLEASE WAIT\nTO BE ROBBED.", new Vector2(3.4f, 1.5f), SignStyle.Info);
            EnvKit.Sign(_decor, new Vector3(-7f, 4.6f, 31.78f), Quaternion.identity, "NO TEAMWORK BEYOND THIS ROPE.\n(THERE IS NO ROPE. NO TEAMWORK ANYWAY.)", new Vector2(6.5f, 1.1f), SignStyle.Warning);
            StoryKit.HoloPoster(_decor, new Vector3(-10.5f, 0f, 9f), EnvKit.FacingViewer(new Vector3(1f, 0f, -0.3f)), StoryKitPoster(2), 2);
            var tv = StoryKit.MeTvScreen(_decor, new Vector3(7f, 4.9f, 31.65f), null, 3.2f);
            Headline(tv, "SMILE! YOU'RE ON ME-TV.");

            // P1 alcove (40 kg, not latching)
            ctx.Block(g, new Vector3(-7.9f, 1.2f, 30.2f), new Vector3(0.3f, 2.4f, 3.2f), MatKey.Marble, "AlcoveW");
            ctx.Block(g, new Vector3(-2.6f, 1.2f, 30.2f), new Vector3(0.3f, 2.4f, 3.2f), MatKey.Marble, "AlcoveE");
            var p1 = PuzzleKit.PressurePlate(ctx.Puzzles, new Vector3(-5f, 0f, 30f), null, 40f, false, "");
            ctx.Mark("s1.P1", p1.gameObject);
            EnvKit.Sign(_decor, new Vector3(-5.25f, 3.1f, 31.78f), Quaternion.identity, "PLEASE STAND HERE.\nALONE. FOREVER.", new Vector2(3.6f, 0.9f), SignStyle.Info);

            // L1 one-shot latching lever behind the wall (vestibule), facing -Z
            var l1 = PuzzleKit.Lever(ctx.Puzzles, new Vector3(4f, 0f, 34.5f), Quaternion.Euler(0f, 180f, 0f), 0, true, false, "MAINTENANCE OVERRIDE: GATE LATCHED OPEN");
            ctx.Mark("s1.L1", l1.gameObject);
            ctx.Label(_decor, new Vector3(4f, 2.6f, 34.9f), Quaternion.identity, "MAINTENANCE OVERRIDE\n(FOR STAFF)\n(YOU ARE STAFF NOW)", 0.26f, UIColors.AccentYellow, 3.4f);

            // GATE G1 = Any(P1, L1)
            var g1 = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 32f), null, DoorMode.SlideUp, 3f, 4.2f, p1, l1);
            g1.Logic = SignalLogic.Any;
            ctx.Mark("s1.G1", g1.gameObject);
            ctx.MarkPoint("s1.gate", new Vector3(0f, 0f, 30.2f));
            ctx.MarkPoint("s1.through", new Vector3(0f, 0f, 33.6f));
            ctx.MarkPoint("s1.approach", new Vector3(0f, 0f, 27f));
            ctx.MarkPoint("s1.beyond", new Vector3(-4.8f, 0f, 35.2f));
            ctx.MarkPoint("s1.beyond2", new Vector3(1f, 0f, 39.4f));
            ctx.MarkPoint("s1.beyond3", new Vector3(4.8f, 0f, 38.6f));
            ctx.MarkPoint("s1.alcoveOut", new Vector3(-5f, 0f, 27.2f));
            ctx.MarkPoint("s1.plateRoute", new Vector3(-5f, 0f, 25f));
            ctx.MarkPoint("s1.brutePath", new Vector3(3f, 0f, 24f));
            ctx.MarkPoint("s1.togetherA", new Vector3(-3f, 0f, 15f));   // story-ON run: walk two interns into Q1 together
            ctx.MarkPoint("s1.togetherB", new Vector3(-1.6f, 0f, 15.4f));

            // cameras Q1 (post, west wall, facing +X) and Q2 (back wall over the gate, facing -Z)
            EnemyKit.CameraPost(g, new Vector3(-11.1f, 0f, 22f), 4.5f);
            _q1 = EnemyKit.SecurityCamera(ctx.Enemies, Q1Pos, Quaternion.LookRotation(Vector3.right), CameraDetect.Teamwork, 35f, 6f, Q1Range, 40f, 0f);
            _q2 = EnemyKit.SecurityCamera(ctx.Enemies, Q2Pos, Quaternion.LookRotation(Vector3.back), CameraDetect.Teamwork, 30f, 6f, Q2Range, 40f, 0.5f);
            ctx.Mark("s1.Q1", _q1.gameObject);
            ctx.Mark("s1.Q2", _q2.gameObject);
            var r1 = new[] {
                ctx.MarkPoint("s1.return.0", new Vector3(-4f, 0f, 7.2f)),
                ctx.MarkPoint("s1.return.1", new Vector3(0f, 0f, 7.2f)),
                ctx.MarkPoint("s1.return.2", new Vector3(4f, 0f, 7.2f)) };
            _a1 = Latch(ctx, "s1.alarm", new Vector3(-11.6f, 6.2f, 22f), r1, true, _q1, _q2);
            SweepChip(g, "s1.chip", _q2, new Vector3(0f, 0f, 28.5f), "GATE");

            ctx.Hint(new Vector3(0f, 2f, 20f), new Vector3(24f, 4f, 26f),
                "One holds the plate alone. The others pass one at a time when the camera looks away. Don't walk as a group.",
                "Act like strangers.", g);
            ctx.Checkpoint(new Vector3(0f, 0f, 36.5f), 0f);

            // S1b vestibule (no cameras)
            Floor(g, -6f, 6f, 32f, 40f, 0f, MatKey.Marble);
            Wall(g, -6.2f, 32f, -6.2f, 40f);
            Wall(g, 6.2f, 32f, 6.2f, 40f);
            EnvKit.Sign(_decor, new Vector3(-6f, 3.2f, 36.5f), EnvKit.WallMountFacing(Vector3.right), "SECURITY VESTIBULE\n(CAMERAS ON BREAK)", new Vector2(3.2f, 1.2f), SignStyle.Info);
        }

        // ================================================================== helpers
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);

        GameObject Floor(Transform p, float x0, float x1, float z0, float z1, float y = 0f, MatKey mat = MatKey.Floor, bool trim = false)
            => EnvKit.Floor(p, new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(x1 - x0, FloorT, z1 - z0), mat, trim);

        GameObject Wall(Transform p, float x0, float z0, float x1, float z1, float h = WallH)
            => EnvKit.Wall(p, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), h, 0.4f, MatKey.Wall, true);

        void WallWithGap(Transform p, float z, float x0, float x1, float gx0, float gx1, float h = WallH)
        {
            if (gx0 - x0 > 0.05f) Wall(p, x0, z, gx0, z, h);
            if (x1 - gx1 > 0.05f) Wall(p, gx1, z, x1, z, h);
            // header above the gap
            _ctx.Block(p, new Vector3((gx0 + gx1) * 0.5f, (h + 5.2f) * 0.5f, z), new Vector3(gx1 - gx0, h - 5.2f, 0.4f), MatKey.Wall, "GapHeader");
        }

        AlarmLatch Latch(LevelBuildContext ctx, string id, Vector3 pos, Transform[] returns, bool countViolations, params SecurityCamera[] cams)
        {
            var a = EnemyKit.AlarmLatch(ctx.Gameplay, pos, cams, 8f);
            a.TripBeat = "ch04.detected";
            a.TripToast = "TEAMWORK DETECTED! PLEASE WAIT ALONE.";
            a.CountViolations = countViolations;
            ctx.Mark(id, a.gameObject);
            var e = EnemyKit.EscortBack(ctx.Gameplay, a, returns);
            e.CameraPause = 3f;
            ctx.Mark(id + ".escort", e.gameObject);
            return a;
        }

        void SweepChip(Transform p, string id, SecurityCamera cam, Vector3 floorPoint, string where)
        {
            var go = Kit.Make("SweepChip_" + where, p, floorPoint);
            var chip = go.AddComponent<L7SweepChip>();
            chip.Camera = cam;
            chip.Where = where;
            Kit.BoxVisual(go.transform, "ChipDisc", new Vector3(0f, 0.015f, 0f), new Vector3(1.4f, 0.02f, 1.4f), MatKey.Hologram, 0f, null, false);
            var rev = PerspectiveKit.Reveal(go, PerspectiveMask.Speed, "SWEEP TIMER", true, true, new Vector3(0f, 2.2f, 0f));
            rev.LabelAlwaysVisible = true;
            chip.Reveal = rev;
            _ctx.Mark(id, go);
        }

        static void Headline(GameObject tv, string text)
        {
            var v = tv != null ? tv.GetComponentInChildren<MeTvScreenView>(true) : null;
            if (v != null) v.SetHeadline(text);
        }

        static string StoryKitPoster(int i)
        {
            var arr = StoryKitLines.Posters;
            if (arr == null || arr.Length == 0) return "ME. ME. ME.";
            return arr[Mathf.Abs(i) % arr.Length];
        }

        void RevolvingDoor(Transform p, Vector3 pos)
        {
            var root = Kit.Make("RevolvingDoor", p, pos);
            var t = root.transform;
            Kit.BoxVisual(t, "Top", new Vector3(0f, 3.0f, 0f), new Vector3(2.5f, 0.2f, 1.6f), MatKey.Gold, 0.04f);
            Kit.BoxVisual(t, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(2.5f, 0.06f, 1.6f), MatKey.Gold, 0.01f, null, false);
            var spin = Kit.Make("Vanes", t, Vector3.zero);
            for (int i = 0; i < 4; i++)
                Kit.BoxVisual(spin.transform, "Vane", new Vector3(0f, 1.45f, 0f), new Vector3(0.06f, 2.8f, 1.4f), MatKey.Glass, 0.01f, Yaw(i * 45f), false);
            var s = spin.AddComponent<SlowSpin>();
            s.Axis = Vector3.up; s.DegreesPerSecond = 40f;
            var c = root.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, 1.5f, 0f); c.size = new Vector3(2.5f, 3f, 1.6f);
        }
    }
}
