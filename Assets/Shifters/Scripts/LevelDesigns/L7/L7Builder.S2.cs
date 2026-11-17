// =====================================================================================
//  SHIFTERS - L7Builder.S2.cs   S2 THE TELLER LINE, S3 STAFF CORRIDOR, the director.
//  S2 solution (any team): in the statue's blind spot the top (Sprinter or Brute) stacks on the Brainiac,
//  climbs the glass-free CLIMB SECTION (2.5 m), drops into the teller area and presses STAFF GATE (8 s);
//  the other two walk in through the gate (its hall side lies in the blind spot). The Brainiac prints
//  the keycard (IQ 7). Then each intern passes the ONE-AT-A-TIME staff door alone while the others
//  wait spread out, west of T2's range.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public partial class L7Builder
    {
        Terminal _printer;
        Door _staffDoor;

        void BuildS2(LevelBuildContext ctx)
        {
            var g = _s2;
            Floor(g, -12f, 12f, 40f, 64f, 0f, MatKey.Marble);
            Wall(g, -12.2f, 40f, -12.2f, 64.2f);
            Wall(g, 12.2f, 40f, 12.2f, 64.2f);
            WallWithGap(g, 40f, -12.2f, 12.2f, -2f, 2f);
            WallWithGap(g, 64f, -12.2f, 12.2f, 0f, 4f);
            Kit.BoxVisual(_decor, "Beam", new Vector3(0f, WallH - 0.2f, 46f), new Vector3(24.4f, 0.4f, 0.5f), MatKey.Gold, 0.03f);
            EnvKit.Decor.Bank.Chandelier(_decor, new Vector3(3f, WallH - 0.4f, 46f), null, 1.3f, 1.2f, 2, false);
            Kit.BoxVisual(_decor, "Beam", new Vector3(0f, WallH - 0.2f, 58f), new Vector3(24.4f, 0.4f, 0.5f), MatKey.Gold, 0.03f);
            EnvKit.Decor.Bank.Chandelier(_decor, new Vector3(4f, WallH - 0.4f, 58f), null, 1.1f, 1.0f, 1, false);

            // ---- TELLER COUNTER (face z 52, 1.4 m deep): glass over x -6..12, CLIMB SECTION x -8.9..-6, TELLER GATE x -12..-9.4
            float cz = CounterFaceZ + CounterDepth * 0.5f;
            EnvKit.Decor.Bank.TellerCounter(g, new Vector3(3f, 0f, cz), null, 18f, CounterH, CounterDepth, 4.5f, 3f, true, null);
            EnvKit.Decor.Bank.TellerCounter(g, new Vector3(-7.45f, 0f, cz), null, 2.9f, CounterH, CounterDepth, 0f, 3f, true, "TELLER CLOSED\n(FOREVER)");
            ctx.Label(_decor, new Vector3(-7.45f, 1.35f, CounterFaceZ - 0.03f), Quaternion.identity, "TELLER CLOSED\n(FOREVER)", 0.24f, UIColors.AccentYellow, 2.6f);
            var gate = PuzzleKit.Door(ctx.Puzzles, new Vector3(-10.7f, 0f, cz), null, DoorMode.SlideUp, 2.6f, 4.2f);
            ctx.Mark("s2.tellerGate", gate.gameObject);
            var btn = PuzzleKit.PushButton(ctx.Puzzles, new Vector3(-11.2f, 0f, 56.6f), Quaternion.Euler(0f, 180f, 0f), GateButtonSeconds, "");
            PuzzleKit.Wire(gate, btn);
            ctx.Mark("s2.gateBtn", btn.gameObject);
            ctx.Label(_decor, new Vector3(-11.2f, 2.3f, 56.9f), Quaternion.identity, "STAFF GATE (8 S)", 0.32f, UIColors.AccentYellow, 3f);
            ctx.Label(_decor, new Vector3(-10.7f, 4.95f, CounterFaceZ - 0.02f), Quaternion.identity, "STAFF ONLY\n(OPENS FROM THE INSIDE)", 0.26f, UIColors.Text, 3.4f);

            // ---- 9 m GOLD MAX statue on a 3 x 3 x 1 m plinth: the cover that makes the BLIND SPOT
            ctx.Block(g, StatuePos + Vector3.up * (PlinthH * 0.5f), new Vector3(PlinthW, PlinthH, PlinthW), MatKey.Marble, "StatuePlinth");
            Kit.BoxVisual(_decor, "PlinthTrim", StatuePos + Vector3.up * (PlinthH + 0.03f), new Vector3(PlinthW + 0.1f, 0.06f, PlinthW + 0.1f), MatKey.Gold, 0.01f);
            var occ = Kit.Make("StatueOccluder", g, StatuePos + Vector3.up * (PlinthH + StatueBlockH * 0.5f));
            var oc = occ.AddComponent<BoxCollider>(); oc.size = new Vector3(StatueBlockW, StatueBlockH, StatueBlockW);
            var statue = StoryKit.VillainStatue(_decor, StatuePos + Vector3.up * PlinthH, null, 9.6f, 0);
            GoldStatue(statue);
            ctx.Mark("s2.statue", statue);
            ctx.Label(_decor, new Vector3(StatuePos.x, 0.55f, StatuePos.z - PlinthW * 0.5f - 0.02f), Quaternion.identity, "OUR FOUNDER, OUR FUTURE, OUR EVERYTHING", 0.2f, UIColors.AccentYellow, 2.9f);
            ctx.Label(_decor, new Vector3(StatuePos.x + PlinthW * 0.5f + 0.02f, 0.55f, StatuePos.z), EnvKit.WallMountFacing(Vector3.right), "SOLID GOLD. LIKE HIS HEART.", 0.2f, UIColors.AccentYellow, 2.9f);
            BlindSpotHatch(ctx, g);

            // ---- cameras: T1 on a post (south-east, facing north-west), T2 over the staff-door approach
            EnemyKit.CameraPost(g, new Vector3(10.1f, 0f, 41.1f), 4.5f);
            _t1 = EnemyKit.SecurityCamera(ctx.Enemies, T1Pos, Quaternion.LookRotation(new Vector3(-1f, 0f, 1f)), CameraDetect.Teamwork, 40f, 7f, T1Range, 40f, 0f);
            ctx.Mark("s2.T1", _t1.gameObject);
            EnemyKit.CameraPost(g, new Vector3(10.8f, 0f, 63.3f), 4.5f);
            _t2 = EnemyKit.SecurityCamera(ctx.Enemies, T2Pos, Quaternion.LookRotation(new Vector3(-8.7f, 0f, -2.3f)), CameraDetect.Teamwork, 25f, 5f, T2Range, 40f, 0f);
            ctx.Mark("s2.T2", _t2.gameObject);
            var r2 = new[] {
                ctx.MarkPoint("s2.return.0", new Vector3(-4f, 0f, 42f)),
                ctx.MarkPoint("s2.return.1", new Vector3(0f, 0f, 42f)),
                ctx.MarkPoint("s2.return.2", new Vector3(4f, 0f, 42f)) };
            _a2 = Latch(ctx, "s2.alarm", new Vector3(11.6f, 6.2f, 41.5f), r2, true, _t1);
            var r3 = new[] {
                ctx.MarkPoint("s2.returnB.0", new Vector3(-10f, 0f, 59f)),
                ctx.MarkPoint("s2.returnB.1", new Vector3(-6f, 0f, 59f)),
                ctx.MarkPoint("s2.returnB.2", new Vector3(-8f, 0f, 62.5f)) };
            _a3 = Latch(ctx, "s2.alarmB", new Vector3(11.6f, 6.2f, 63f), r3, true, _t2);
            SweepChip(g, "s2.chip", _t2, new Vector3(2f, 0f, 60f), "STAFF DOOR");

            // ---- teller area: keycard printer, staff door, teller-bots
            Floor(g, -12f, 12f, 53.4f, 64f, 0.01f, MatKey.FloorAlt);
            _printer = PuzzleKit.Terminal(ctx.Puzzles, new Vector3(-2f, 0f, 58f), Quaternion.Euler(0f, 180f, 0f), Tuning.TerminalIQ, true, 6f, "KEYCARD PRINTED");
            ctx.Mark("s2.printer", _printer.gameObject);
            ctx.Label(_decor, new Vector3(-2f, 2.6f, 58.3f), Quaternion.identity, "KEYCARD PRINTER\n(IQ 7: BRAINS ONLY)", 0.28f, UIColors.AccentYellow, 3f);
            var solo = PuzzleKit.OneAtATimeDoor(ctx.Puzzles, new Vector3(2f, 0f, 64f), null, new Vector3(3f, 3f, 3f), _printer);
            _staffDoor = solo.Door;
            ctx.Mark("s2.staffDoor", solo.Door.gameObject);
            ctx.Mark("s2.soloSensor", solo.gameObject);
            ctx.MarkPoint("s2.doorZone", new Vector3(2f, 0f, 62.4f));
            ctx.Tutorial(new Vector3(1f, 1.5f, 60f), new Vector3(9f, 3f, 6f), "ch04_onedoor", "STAFF DOOR",
                "STAFF DOOR: ONE (1) EMPLOYEE AT A TIME. It means it.", null, false, g);
            StoryKit.ScriptTrigger(g, new Vector3(2f, 1.5f, 62.4f), new Vector3(3f, 3f, 3f), "ch04.onedoor", true);
            for (int i = 0; i < 3; i++)
                TellerBot(g, new Vector3(0.5f + 4.5f * i, 0f, 54.9f));
            EnvKit.Decor.Bank.SafeDepositWall(_decor, new Vector3(-7f, 0f, 63.75f), Quaternion.identity, 8, 5, 0.45f, false, "SAFE DEPOSIT (MINE)");
            EnvKit.Decor.Bank.CoinPile(_decor, new Vector3(8.5f, 0f, 61f), null, 1.1f, 0.5f, 7);
            EnvKit.Decor.Bank.PenOnAChain(_decor, new Vector3(-1f, CounterH, CounterFaceZ + 0.4f), null, 3f);
            EnvKit.Sign(_decor, new Vector3(-11.95f, 3.6f, 46f), EnvKit.WallMountFacing(Vector3.right), "SMILE!\nYOU'RE ON ME-TV.", new Vector2(3f, 1.4f), SignStyle.Info);
            StoryKit.HoloPoster(_decor, new Vector3(10.5f, 0f, 48f), EnvKit.FacingViewer(new Vector3(-1f, 0f, -0.4f)), StoryKitPoster(3), 2);
            var tv = StoryKit.MeTvScreen(_decor, new Vector3(-2.5f, 5.8f, 63.7f), null, 3.4f);
            Headline(tv, "EMPLOYEE OF THE MONTH: ME (AGAIN)");

            // ---- bot marks (blind spot, gate, waits)
            ctx.MarkPoint("s2.hallIn", new Vector3(0f, 0f, 41.6f));
            ctx.MarkPoint("s2.westRun", new Vector3(-10.8f, 0f, 45f));
            ctx.MarkPoint("s2.stackSpot", StackSpot);
            ctx.MarkPoint("s2.stackRun", new Vector3(-9.3f, 0f, 49.6f));
            ctx.MarkPoint("s2.gateWait", new Vector3(-10.9f, 0f, 50.7f));
            ctx.MarkPoint("s2.counterTop", new Vector3(-7.45f, CounterH, CounterFaceZ + 0.75f));
            ctx.MarkPoint("s2.tellerDrop", new Vector3(-7.45f, 0f, 55.6f));
            ctx.MarkPoint("s2.gateOut", new Vector3(-10.5f, 0f, 51.1f));
            ctx.MarkPoint("s2.gateIn", new Vector3(-10.6f, 0f, 54.3f));
            ctx.MarkPoint("s2.inside.a", new Vector3(-6f, 0f, 57.5f));
            ctx.MarkPoint("s2.inside.b", new Vector3(-10.5f, 0f, 60.5f));
            ctx.MarkPoint("s2.brainRun", new Vector3(-9.2f, 0f, 47.2f));
            ctx.MarkPoint("s2.stackApproach", new Vector3(-8.6f, 0f, 50.4f));
            ctx.MarkPoint("s2.wait.0", new Vector3(-2.6f, 0f, 61.2f));
            ctx.MarkPoint("s2.wait.1", new Vector3(-3.8f, 0f, 57.9f));
            ctx.MarkPoint("s2.wait.2", new Vector3(-6.5f, 0f, 60.5f));
            ctx.MarkPoint("s2.alone", new Vector3(3f, 0f, 49.5f));

            ctx.Hint(new Vector3(-6f, 2f, 48f), new Vector3(12f, 4f, 10f),
                "Stack behind the statue where the camera can't see; the gate opens from the inside.",
                "The founder makes great cover.", g);
            ctx.Hint(new Vector3(1f, 2f, 59f), new Vector3(12f, 4f, 9f),
                "Print the keycard (Brainiac), then pass the staff door one by one. Wait far apart, out of the camera's reach.",
                "One. At. A. Time.", g);
        }

        // ================================================================== S3 STAFF CORRIDOR
        void BuildS3(LevelBuildContext ctx)
        {
            var g = _s3;
            Floor(g, -4f, 4f, 64f, 76f, 0f, MatKey.Concrete);
            Wall(g, -4.2f, 64f, -4.2f, 76.2f, 5f);
            Wall(g, 4.2f, 64f, 4.2f, 76.2f, 5f);
            Wall(g, -4.2f, 76f, 4.2f, 76f, 5f);
            EnvKit.Sign(_decor, new Vector3(-3.98f, 3.1f, 68.5f), EnvKit.WallMountFacing(Vector3.right), "NO CAMERAS BEYOND THIS POINT\n(PRIVACY IS FOR ME)", new Vector2(3.6f, 1.2f), SignStyle.Warning);
            EnvKit.Sign(_decor, new Vector3(0f, 3.6f, 75.78f), Quaternion.identity, "BASEMENT · OLD LAB\n(CLOSED SINCE 2004)", new Vector2(4.6f, 1.3f), SignStyle.Exit);
            EnvKit.LightPanel(_decor, new Vector3(3.98f, 3.2f, 70f), EnvKit.WallMountFacing(Vector3.left), new Vector2(1.6f, 0.4f), UIColors.Hex(0xFFD27A), false);
            ctx.MarkPoint("s3.doorIn", new Vector3(2f, 0f, 65.8f));
            for (int i = 0; i < 3; i++) ctx.MarkPoint("s3.corridor." + i, new Vector3(-2.2f + 2.2f * i, 0f, 67.5f));
            ctx.Checkpoint(new Vector3(0f, 0f, 67f), 0f);
            StoryKit.ScriptTrigger(g, new Vector3(0f, 1.5f, 66.4f), new Vector3(8f, 3f, 2.2f), "ch04.exit", true);
            ctx.Exit(new Vector3(0f, 0f, 72.5f), 0f);
        }

        // ================================================================== director + story
        void BuildDirector(LevelBuildContext ctx)
        {
            var go = Kit.Make("L7Director", ctx.Gameplay, Vector3.zero);
            var d = go.AddComponent<L7Director>();
            d.Lesson = _a0;
            d.Graded = new[] { _a1, _a2, _a3 };
            d.Printer = _printer;
            d.StaffDoor = _staffDoor;
            d.AloneCenter = new Vector3(3f, 0f, 48f);
            d.AloneSize = new Vector3(18f, 4f, 8f);
            ctx.Mark("story.director", go);
        }

        // ------------------------------------------------------------------ pieces
        void GoldStatue(GameObject statue)
        {
            if (statue == null) return;
            var t = statue.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name != "Figure") c.gameObject.SetActive(false);   // StoryKit's own plinth + plate (we have ours)
            }
            var fig = t.Find("Figure");
            if (fig == null) return;
            fig.localPosition = Vector3.zero;
            var gold = Kit.Mat(MatKey.Gold);
            foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = gold;
            var sg = fig.Find("Sunglasses");
            if (sg != null) sg.gameObject.SetActive(true);   // the founder wears shades indoors
        }

        void TellerBot(Transform p, Vector3 pos)
        {
            // a stool behind the counter so the bow-tied BONK-BOT's head shows through the glass
            _ctx.Block(p, pos + new Vector3(0f, 0.6f, 0.15f), new Vector3(1.1f, 1.2f, 1.1f), MatKey.Wood, "TellerStool");
            var holder = Kit.Make("TellerBot", _decor, pos + Vector3.up * 1.2f, Quaternion.Euler(0f, 180f, 0f));
            var model = EnemyModelBuilder.Build(holder.transform, UIColors.Hex(0xFFC845));
            if (model != null)
            {
                EnemyModelBuilder.AddBowTie(model, UIColors.Hex(0xD7263D));
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Kit.SafeDestroy(c);
            }
        }

        /// <summary>IQ view: hatched floor cells inside the study area that T1 can never see (its lens -> the
        /// intern's centre and head both hit the statue or plinth). Computed analytically (edit-mode safe).</summary>
        void BlindSpotHatch(LevelBuildContext ctx, Transform g)
        {
            var root = Kit.Make("BlindSpot", g, Vector3.zero);
            const float cell = 0.5f;
            int nx = Mathf.RoundToInt((BlindX1 - BlindX0) / cell), nz = Mathf.RoundToInt((BlindZ1 - BlindZ0) / cell);
            var sOcc = new Bounds(StatuePos + Vector3.up * (PlinthH + StatueBlockH * 0.5f), new Vector3(StatueBlockW, StatueBlockH, StatueBlockW));
            var sPl = new Bounds(StatuePos + Vector3.up * (PlinthH * 0.5f), new Vector3(PlinthW, PlinthH, PlinthW));
            Vector3 eye = T1Pos + Quaternion.LookRotation(new Vector3(-1f, 0f, 1f)) * new Vector3(0f, 0f, 0.5f);
            int quads = 0;
            for (int iz = 0; iz < nz; iz++)
            {
                float z = BlindZ0 + (iz + 0.5f) * cell;
                int runStart = -1;
                for (int ix = 0; ix <= nx; ix++)
                {
                    bool blind = false;
                    if (ix < nx)
                    {
                        float x = BlindX0 + (ix + 0.5f) * cell;
                        var f = new Vector3(x, 0f, z);
                        bool inPlinth = sPl.Contains(f + Vector3.up * 0.5f);
                        blind = !inPlinth && Hidden(eye, f + Vector3.up * 0.9f, sOcc, sPl) && Hidden(eye, f + Vector3.up * 1.7f, sOcc, sPl);
                    }
                    if (blind && runStart < 0) runStart = ix;
                    if (!blind && runStart >= 0)
                    {
                        float x0 = BlindX0 + runStart * cell, x1 = BlindX0 + ix * cell;
                        Kit.BoxVisual(root.transform, "Hatch", new Vector3((x0 + x1) * 0.5f, 0.02f, z), new Vector3(x1 - x0 - 0.04f, 0.012f, cell - 0.06f), MatKey.HazardStripes, 0f, null, false);
                        quads++;
                        runStart = -1;
                    }
                }
            }
            if (quads == 0) ctx.Warn("L7: blind-spot hatch is empty (statue/camera geometry changed?)");
            var rev = PerspectiveKit.Reveal(root, PerspectiveMask.Intellect, "BLIND SPOT\n(THE STATUE HIDES YOU)", true, true, new Vector3(-9f, 2.4f, 50.5f));
            rev.LabelAlwaysVisible = true;
            ctx.Mark("s2.blindSpot", root);
        }

        static bool Hidden(Vector3 eye, Vector3 p, Bounds a, Bounds b)
        {
            var ray = new Ray(eye, (p - eye).normalized);
            float d = Vector3.Distance(eye, p);
            return (a.IntersectRay(ray, out float t1) && t1 < d) || (b.IntersectRay(ray, out float t2) && t2 < d);
        }
    }
}
