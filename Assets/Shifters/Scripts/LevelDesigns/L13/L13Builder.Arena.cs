// =====================================================================================
//  SHIFTERS - L13Builder.Arena.cs   [LEVEL 13 - HOSTILE TAKEOVER]
//  The ME-CHA arena (x -19..19, z 8..52): break areas, the boss, the skate lane + bump run,
//  balconies + firewall terminals, the figure-8 SOLO POD rail, Percy's cage and DETENTION (VIP).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public partial class L13Builder
    {
        static Vector3 AL(Vector3 world) => world - ArenaCenter;   // arena-local

        /// <summary>Skate lane (CCW from above, starting north): world point at angle t (rad).</summary>
        public static Vector3 LanePoint(float t, float r = LaneR) => new Vector3(-r * Mathf.Sin(t), 0f, LaneCenter.z + r * Mathf.Cos(t));

        /// <summary>The SOLO POD figure-8 (closed, world, sampled every ~1 m): crossing (0,31), lobes r 5 at (+-7, 31).</summary>
        public static Vector3[] FigureEight()
        {
            var pts = new List<Vector3>();
            const float r = 5f, cz = 31f;
            float tanA = 135.58f * Mathf.Deg2Rad;                  // tangent point on the east lobe (3.43, 34.5)
            Vector3 cross = new Vector3(0f, 0f, cz);
            Vector3 eTop = new Vector3(7f + r * Mathf.Cos(tanA), 0f, cz + r * Mathf.Sin(tanA));
            Vector3 eBot = new Vector3(eTop.x, 0f, cz - (eTop.z - cz));
            Vector3 wTop = new Vector3(-eTop.x, 0f, eTop.z), wBot = new Vector3(-eTop.x, 0f, eBot.z);
            void Line(Vector3 a, Vector3 b) { int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b))); for (int i = 0; i < n; i++) pts.Add(Vector3.Lerp(a, b, i / (float)n)); }
            void Arc(float cx, float a0, float a1)
            {
                float len = Mathf.Abs(a1 - a0) * r; int n = Mathf.Max(2, Mathf.RoundToInt(len));
                for (int i = 0; i < n; i++) { float a = Mathf.Lerp(a0, a1, i / (float)n); pts.Add(new Vector3(cx + r * Mathf.Cos(a), 0f, cz + r * Mathf.Sin(a))); }
            }
            Line(cross, eTop);
            Arc(7f, tanA, tanA - 271.16f * Mathf.Deg2Rad);         // clockwise round the east lobe -> eBot
            Line(eBot, cross);
            Line(cross, wTop);
            float wa0 = Mathf.PI - tanA;                           // 44.42 deg
            Arc(-7f, wa0, wa0 + 271.16f * Mathf.Deg2Rad);          // counter-clockwise round the west lobe -> wBot
            Line(wBot, cross);
            return pts.ToArray();
        }

        void BuildArena(LevelBuildContext ctx)
        {
            var deco = ctx.Section("Arena_Decor", Vector3.zero);
            var arena = BossKit.Arena(ctx.Gameplay, ArenaCenter, new Vector2(38f, 44f), 0f, false);
            ctx.Mark("boss.arena", arena.gameObject);

            // ---------------- break areas (7 x 5) + phase checkpoints C2 / C3 / C4
            Vector3[] ba = { new Vector3(-15.5f, 0f, 10.5f), new Vector3(15.5f, 0f, 10.5f), new Vector3(-15.5f, 0f, 49.5f), new Vector3(15.5f, 0f, 49.5f), new Vector3(0f, 0f, 49.5f) };
            for (int i = 0; i < ba.Length; i++) ctx.Mark("boss.safe." + (i + 1), BossKit.BreakArea(arena.transform, AL(ba[i]), new Vector2(7f, 5f)));
            ctx.Checkpoint(ba[0], 0f);   // C2 (P2)
            ctx.Checkpoint(ba[1], 0f);   // C3 (P3)
            ctx.Checkpoint(ba[3], 180f); // C4 (P4)
            FloorText(deco, new Vector3(0f, 0f, 51.1f), 0f, "ANTENNA IN RANGE - ANY GUN", 0.36f, Gold, 6.5f);
            ctx.MarkPoint("s3.shootSpot", new Vector3(0f, 0f, 50.5f));
            Sign(deco, new Vector3(-15.5f, 3.4f, 8.35f), 180f, "BREAK AREA\n<size=55%>EVEN MECHS RESPECT UNIONS</size>", 4.2f, 1.1f, SignStyle.Warning);
            Sign(deco, new Vector3(15.5f, 3.4f, 8.35f), 180f, "BREAK AREA\n<size=55%>EVEN MECHS RESPECT UNIONS</size>", 4.2f, 1.1f, SignStyle.Warning);

            // ---------------- the ME-CHA
            var root = Kit.Make("OneManBand", ctx.Gameplay, Home, Yaw(180f));
            var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var model = BossModels.OneManBand(root.transform, Vector3.zero, Quaternion.identity, true);
            var block = Kit.SolidBox(root.transform, "FeetBlock", new Vector3(0f, 2f, 0f), new Vector3(4.4f, 4f, 3.6f), MatKey.Shadow, 0f);
            var br = block != null ? block.GetComponent<Renderer>() : null; if (br != null) br.enabled = false;
            var boss = root.AddComponent<L13OneManBandBoss>();
            boss.Arena = arena; boss.Model = model; boss.BodyBlock = block;
            boss.Configure();
            ctx.Mark("boss", root);

            // knuckles (P1): four WeakPointPunch plates on the detached fist (inactive until it is STUCK)
            var knuckles = new BossWeakPoint[4];
            string[] kn = { "N", "E", "S", "W" };
            Vector3[] nrm = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            var kroot = Kit.Make("Knuckles", ctx.Gameplay, Home + Vector3.up * 0.6f).transform;
            for (int i = 0; i < 4; i++)
            {
                var wp = BossKit.WeakPointPunch(kroot, nrm[i] * 1.05f, boss, 1, 7);
                wp.transform.localRotation = Quaternion.LookRotation(nrm[i]);
                wp.Prompt = "PUNCH!";
                // polish 11-17: the kit plate floats its "PUNCH HERE" text above the plate (seen mirrored through the fist
                // from the far side) and each plate carries its own STR label (four identical labels stacked on the fist).
                // Text goes ON the plate face (the plate hides it from behind); one STR label, centred over the fist.
                var txt = wp.transform.Find("Label");
                var tmp = txt != null ? txt.GetComponent<TMPro.TextMeshPro>() : null;
                if (tmp != null)
                {
                    txt.localPosition = new Vector3(0f, 0f, 0.14f);
                    tmp.text = "PUNCH\nHERE";
                    tmp.color = new Color32(21, 22, 26, 255);
                    tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.5f; tmp.fontSizeMax = 2.6f;
                    tmp.lineSpacing = -18f;
                    tmp.rectTransform.sizeDelta = new Vector2(0.9f, 0.7f);
                }
                var rv = wp.GetComponent<Revealable>();
                if (rv != null)
                {
                    if (i == 0) rv.LabelOffset = -nrm[i] * 1.05f + Vector3.up * 1.15f;
                    else rv.Label = "";
                }
                ctx.Mark("boss.weak.knuckle." + kn[i], wp.gameObject);
                knuckles[i] = wp;
            }
            ctx.Label(kroot, new Vector3(0f, 2.3f, 0f), Quaternion.identity, "CRACKED KNUCKLE\n<size=60%>PUNCH ME (STR 7)</size>", 0.42f, Gold, 4f);
            kroot.gameObject.SetActive(false);
            boss.KnuckleRoot = kroot; boss.Knuckles = knuckles;

            // antenna (P3): any gun / tool, inactive until both firewalls are down
            var ant = BossKit.WeakPointShoot(ctx.Gameplay, new Vector3(0f, 7.35f, 42.5f), 0.6f, boss, 1 << 2);
            ctx.Label(ant.transform, new Vector3(0f, 1.2f, -0.1f), Quaternion.identity, "EGO ANTENNA\n<size=60%>DO NOT SHOOT. SERIOUSLY.</size>", 0.32f, Gold, 4f);
            ant.gameObject.SetActive(false);
            boss.Antenna = ant;
            ctx.Mark("boss.weak.antenna", ant.gameObject);

            // ---------------- P1 field
            ctx.MarkPoint("s1.bait", new Vector3(-6f, 0f, 17.5f));
            ctx.MarkPoint("s1.dodge", new Vector3(-2.5f, 0f, 17.5f));
            ctx.MarkPoint("boss.hashTarget", new Vector3(-6f, 0f, 17.5f));
            BossKit.Mark(arena, "boss.hashTarget", AL(new Vector3(-6f, 0f, 17.5f)));
            FloorText(deco, new Vector3(-6f, 0f, 26.5f), 0f, "PREMIUM SOFT MARBLE.\nPLEASE DO NOT PUNCH.", 0.42f, Ink, 7f);

            // ---------------- P2 skate lane (annulus r 8..13) + bumps + bump run
            var lane = Kit.Make("SkateLane", deco, new Vector3(0f, 0.012f, LaneCenter.z)).transform;
            ProcMesh.AddRenderer(Kit.Make("Surface", lane, Vector3.zero), ProcMesh.Cached("l13_lane", () => L13Kit.Annulus(LaneIn, LaneOut, 72)), Kit.Mat(MatKey.BlackGlass), false);
            ProcMesh.AddRenderer(Kit.Make("EdgeIn", lane, Vector3.up * 0.004f), ProcMesh.Cached("l13_lane_ei", () => L13Kit.Annulus(LaneIn, LaneIn + 0.22f, 72)), Kit.Mat(MatKey.Gold), false);
            ProcMesh.AddRenderer(Kit.Make("EdgeOut", lane, Vector3.up * 0.004f), ProcMesh.Cached("l13_lane_eo", () => L13Kit.Annulus(LaneOut - 0.22f, LaneOut, 72)), Kit.Mat(MatKey.Gold), false);
            for (int i = 0; i < 12; i++)
            {
                float t = (i + 0.5f) * Mathf.PI * 2f / 12f;
                var p = LanePoint(t); var tan = new Vector3(-Mathf.Cos(t), 0f, -Mathf.Sin(t));
                var ch = Kit.Make("Chevron", deco, p + Vector3.up * 0.02f, Quaternion.LookRotation(tan)).transform;
                Kit.BoxVisual(ch, "L", new Vector3(-0.45f, 0f, 0f), new Vector3(0.25f, 0.012f, 1.4f), MatKey.Gold, 0f, Yaw(35f), false);
                Kit.BoxVisual(ch, "R", new Vector3(0.45f, 0f, 0f), new Vector3(0.25f, 0.012f, 1.4f), MatKey.Gold, 0f, Yaw(-35f), false);
            }
            FloorText(deco, new Vector3(10.5f, 0f, 32f), 0f, "SKATE LANE\nEXECUTIVES ONLY", 0.5f, Gold, 4.6f);
            FloorText(deco, new Vector3(-10.5f, 0f, 32f), 0f, "SPEED BUMPS: BANNED\n(I DON'T SLOW DOWN)", 0.4f, Gold, 4.6f);
            var loop = new Vector3[64];
            for (int i = 0; i < loop.Length; i++) loop[i] = AL(LanePoint(i * Mathf.PI * 2f / loop.Length));
            BossKit.LoopMarks(arena, "s2.loop", loop);
            // INCOMING strip (P2 only)
            var lg = BossKit.HazardGroup(arena, "s2.lane");
            var incoming = new Transform[6];
            for (int i = 0; i < incoming.Length; i++)
            {
                var s = Kit.Make("Incoming" + i, lg, Vector3.zero).transform;
                Kit.BoxVisual(s, "Strip", Vector3.zero, new Vector3(4.6f, 0.02f, 1.6f), MatKey.WarningRed, 0f, null, false);
                incoming[i] = s;
            }
            boss.Incoming = incoming;
            // 3 speed bumps (kinematic, 5 m radial x 1 m x 0.5 m; flush when down)
            Vector3[] bp = { new Vector3(-6.02f, 0f, 23.40f), new Vector3(0f, 0f, 21.50f), new Vector3(6.02f, 0f, 23.40f) };
            var bumps = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var radial = (bp[i] - LaneCenter); radial.y = 0f;
                var go = Kit.Make("SpeedBump" + (i + 1), ctx.Puzzles, bp[i] + Vector3.down * 0.26f, Quaternion.LookRotation(radial.normalized));
                var brb = go.AddComponent<Rigidbody>(); brb.isKinematic = true; brb.useGravity = false;
                Kit.SolidBox(go.transform, "Hump", Vector3.zero, new Vector3(1f, 0.5f, 5f), MatKey.HazardStripes, 0.08f);
                ctx.Label(go.transform, new Vector3(0f, 0.27f, 0f), Quaternion.Euler(90f, 90f, 0f), "BUMP", 0.4f, UIColors.Hex(0x141414), 3f);
                ctx.Mark("s2.bump." + (i + 1), go);
                bumps[i] = go.transform;
            }
            boss.Bumps = bumps;
            // BUMP RUN along z 15.5: button (-14, 15.5) facing +X, gates G2 x 0, G3 x 13
            var button = MakeBumpButton(PuzzleKit.TimedButton(ctx.Puzzles, new Vector3(-14f, 0f, 15.5f), Yaw(90f), RouteLength, null, ""));
            ctx.Mark("s2.button", button.gameObject);
            ctx.MarkPoint("s2.stand", new Vector3(-13f, 0f, 15.5f));
            // polish 11-17: Yaw(90) read mirrored from the button side (+X)
            ctx.Label(deco, new Vector3(-14.7f, 2.7f, 15.5f), Yaw(-90f), "RAISE SPEED BUMPS\n<size=60%>(STRICTLY FORBIDDEN)</size>", 0.34f, Gold, 3.6f);
            var run = Kit.Make("BumpRun", ctx.Puzzles, new Vector3(0f, 0f, 15.5f)).AddComponent<L13BumpRun>();
            run.Button = button; run.Bumps = bumps; run.G2X = 0f; run.G3X = 13f; run.ZMin = 13.5f; run.ZMax = 17.5f;
            ctx.Mark("s2.bumps", run.gameObject);
            boss.BumpRun = run;
            for (int g = 0; g < 2; g++)
            {
                float x = g == 0 ? 0f : 13f;
                var gate = Kit.Make(g == 0 ? "GateG2" : "GateG3", deco, new Vector3(x, 0f, 15.5f)).transform;
                PuzzleVisuals.Cyl(gate, "PostS", new Vector3(0f, 1.5f, -1.5f), 0.12f, 3f, MatKey.Gold, null, 12, false);
                PuzzleVisuals.Cyl(gate, "PostN", new Vector3(0f, 1.5f, 1.5f), 0.12f, 3f, MatKey.Gold, null, 12, false);
                Kit.BoxVisual(gate, "Beam", new Vector3(0f, 3.05f, 0f), new Vector3(0.2f, 0.18f, 3.2f), MatKey.Gold, 0.02f, null, false);
                // polish 11-17: the G3 tag floats over the beam and read mirrored from the arena side (sign sweep); both turn to the camera
                var gl = ctx.Label(gate, new Vector3(-0.12f, 3.5f, 0f), Yaw(90f), g == 0 ? "G2" : "G3", 0.5f, Gold, 2f);
                if (gl != null) gl.gameObject.AddComponent<PuzzleBillboard>();
                ctx.Mark(g == 0 ? "s2.g2" : "s2.g3", gate.gameObject);
            }
            for (int i = 0; i < 13; i++)
            {
                var ch = Kit.Make("RunChevron", deco, new Vector3(-11f + i * 2f, 0.02f, 15.5f), Yaw(90f)).transform;
                Kit.BoxVisual(ch, "L", new Vector3(-0.3f, 0f, 0f), new Vector3(0.18f, 0.012f, 0.9f), MatKey.HazardYellow, 0f, Yaw(35f), false);
                Kit.BoxVisual(ch, "R", new Vector3(0.3f, 0f, 0f), new Vector3(0.18f, 0.012f, 0.9f), MatKey.HazardYellow, 0f, Yaw(-35f), false);
            }
            FloorText(deco, new Vector3(-8f, 0f, 13.9f), 0f, "BUMP RUN 26 M - TURBO", 0.42f, UIColors.AccentYellow, 6f);
            FloorText(deco, new Vector3(6.5f, 0f, 13.9f), 0f, "HR SAYS NO. SPEED BUMPS ARE A SPRINT EVENT.", 0.3f, Ink, 8f);
            PuzzleKit.NoToolZone(ctx.Puzzles, new Vector3(0f, 2f, 15.5f), new Vector3(29f, 4f, 4f), ToolMask.SwapOMatic);
            ctx.MarkPoint("s2.runout", new Vector3(15.5f, 0f, 15.5f));

            // ---------------- P3 balconies, ramps ("stairs"), firewall terminals, cables
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                string tag = side < 0 ? "W" : "E";
                ctx.Platform(ctx.Geometry, new Vector3(17f * s, 2.5f, 28f), new Vector3(4f, 2.5f, 6f), MatKey.FloorAlt, true, "Balcony" + tag);
                ctx.Ramp(ctx.Geometry, new Vector3(17.75f * s, 0f, 19f), new Vector3(17.75f * s, 2.5f, 25f), 2.5f, 0.4f, MatKey.FloorAlt);
                EnvKit.Railing(ctx.Geometry, new Vector3(15f * s, 2.5f, 25f), new Vector3(15f * s, 2.5f, 31f));
                EnvKit.Railing(ctx.Geometry, new Vector3(19f * s, 2.5f, 31f), new Vector3(15f * s, 2.5f, 31f));
                EnvKit.Railing(ctx.Geometry, new Vector3(16.5f * s, 2.5f, 25f), new Vector3(15f * s, 2.5f, 25f));
                ctx.MarkPoint("s3.stairs" + tag + ".foot", new Vector3(17.75f * s, 0f, 18.5f));
                ctx.MarkPoint("s3.stairs" + tag + ".top", new Vector3(17.75f * s, 2.5f, 25.5f));
                ctx.MarkPoint("s3.balcony" + tag + ".stand", new Vector3(17.5f * s, 2.5f, 28f));
                var term = PuzzleKit.Terminal(ctx.Puzzles, new Vector3(18.5f * s, 2.5f, 28f), Yaw(side < 0 ? 90f : -90f), 7, true, 6f, ""); // the boss raises the OFFLINE toast (DropShield)
                ctx.Mark("s3.terminal" + tag, term.gameObject);
                PerspectiveKit.Reveal(term.gameObject, PerspectiveMask.Intellect, "FIREWALL " + tag + " (IQ 7)", true, false, new Vector3(0f, 2.2f, 0f));
                if (side < 0) boss.TerminalW = term; else boss.TerminalE = term;
                Sign(deco, new Vector3(18.7f * s, 4.6f, 28f), side < 0 ? -90f : 90f, "FIREWALL\n<size=60%>(ALSO ME)</size>", 2.6f, 1.0f, SignStyle.Danger);
            }
            var cables = BossKit.HazardGroup(arena, "s3.cables");
            boss.CableW = L13Kit.Beam(cables, new Vector3(-18.3f, 3.6f, 28f), new Vector3(-1.4f, 5.4f, 42.2f), 0.14f, MatKey.Glow, "CableW");
            boss.CableE = L13Kit.Beam(cables, new Vector3(18.3f, 3.6f, 28f), new Vector3(1.4f, 5.4f, 42.2f), 0.14f, MatKey.Glow, "CableE");
            ctx.MarkPoint("s3.cross.1", new Vector3(14f, 0f, 18.5f));
            ctx.MarkPoint("s3.cross.2", new Vector3(-14f, 0f, 18.5f));
            ctx.MarkPoint("s3.east.1", new Vector3(14f, 0f, 33f));
            var fw = Kit.Make("FirewallDown", ctx.Puzzles, new Vector3(0f, 0f, 42.5f)).AddComponent<L13Signal>();
            ctx.Mark("s3.firewall", fw.gameObject);
            boss.FirewallDown = fw;
            var domeLight = GoldLight(ctx.Lights, new Vector3(0f, 6.2f, 40.5f), UIColors.Hex(0x7FE8FF), 2.4f, 9f);
            if (domeLight != null) { domeLight.SetActive(false); boss.DomeLight = domeLight; }

            // ---------------- P4 figure-8 rail, the SOLO POD
            var fig = FigureEight();
            var rail = BossKit.HazardGroup(arena, "s4.rail");
            for (int i = 0; i < fig.Length; i += 2)
            {
                var a = fig[i]; var b = fig[(i + 2) % fig.Length];
                var d = b - a; if (d.sqrMagnitude < 0.01f) continue;
                Kit.BoxVisual(rail, "Rail", (a + b) * 0.5f - ArenaCenter + Vector3.up * 0.02f, new Vector3(1.5f, 0.02f, d.magnitude + 0.08f), MatKey.Gold, 0f, Quaternion.LookRotation(d.normalized), false);
            }
            ctx.Label(rail, new Vector3(0f, 0.05f, 37.6f) - ArenaCenter, FloorRot(), "SOLO POD EXPRESS\n<size=60%>EXECUTIVES ONLY</size>", 0.5f, Gold, 6f);
            var pod = BossKit.SoloPod(ctx.Gameplay, new Vector3(0f, 1f, 46.5f), fig);
            pod.name = "SoloPod";
            foreach (var n in new[] { "Seat", "Back", "Cushion", "ArmL", "ArmR", "Thruster", "Plate" })
            {
                var c = pod.transform.Find(n);
                if (c != null) Kit.SafeDestroy(c.gameObject);
            }
            BossModels.SoloPod(pod.transform, Vector3.zero, Quaternion.identity, true, false);
            pod.ArgueArea = new Bounds(new Vector3(0f, 2f, 28f), new Vector3(29f, 10f, 38f));
            pod.MonologueLines = new[] { "I RULE ALONE!", "TEAMWORK IS A SCAM!", "ONE MAN. ONE VOTE. MINE.", "BEHOLD: ME!", "I DELEGATE TO MYSELF!", "APPROVAL RATING: ME%!" };
            pod.ArgueLines = new[] { "EXCUSE ME?!", "HOW DARE YOU!", "SAY THAT TO MY FACE!", "I'LL HAVE YOU REVIEWED!" };
            pod.gameObject.AddComponent<L13PodShrink>();
            var tagLbl = Kit.Make("TagLabel", pod.transform, Vector3.zero);
            PerspectiveKit.Reveal(tagLbl, PerspectiveMask.Speed, "TAG AT >= 8 M/S", false, true, new Vector3(0f, 2.4f, 0f));
            var shootLbl = Kit.Make("ShootLabel", pod.transform, Vector3.zero);
            PerspectiveKit.Reveal(shootLbl, PerspectiveMask.Weapon, "SHOOT HIM: DIZZY 3 s", false, true, new Vector3(0f, 2.8f, 0f));
            var clue = Kit.Make("ClueLabel", pod.transform, Vector3.zero);
            PerspectiveKit.Reveal(clue, PerspectiveMask.Intellect, "HE CAN'T RESIST AN ARGUMENT. (T)", false, true, new Vector3(0f, 3.2f, 0f));
            pod.enabled = false;
            boss.Pod = pod;
            ctx.Mark("s4.pod", pod.gameObject);
            ctx.MarkPoint("s4.pod.home", PodHome);
            ctx.MarkPoint("s4.hub", new Vector3(0f, 0f, 25f));
            ctx.MarkPoint("s4.tinyWait", new Vector3(8f, 0f, 37f));
            ctx.MarkPoint("s4.swapLure", new Vector3(15f, 0f, 40f));

            // ---------------- Percy's gold birdcage on a 1 m pedestal (x 15..17, z 44..46)
            ctx.Platform(ctx.Geometry, new Vector3(16f, 1f, 45f), new Vector3(2f, 1f, 2f), MatKey.Marble, true, "PercyPedestal");
            var cage = Kit.Make("PercyCage", deco, new Vector3(16f, 1f, 45f)).transform;
            var bars = Kit.Make("Bars", cage, Vector3.zero).transform;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                PuzzleVisuals.Cyl(bars, "Bar", new Vector3(Mathf.Cos(a) * 0.85f, 1.1f, Mathf.Sin(a) * 0.85f), 0.04f, 2.2f, MatKey.Gold, null, 6, false);
            }
            ProcMesh.AddRenderer(Kit.Make("Top", bars, new Vector3(0f, 2.2f, 0f)), ProcMesh.Cached("l13_cagetop", () => ProcMesh.Cylinder(0.95f, 0.12f, 18, true)), Kit.Mat(MatKey.Gold), false);
            var percy = StoryKit.PercyMachine(cage, new Vector3(0f, 0f, 0f), Yaw(90f));
            if (percy != null) ctx.Mark("story.percy", percy);
            boss.CageBars = bars;
            ctx.Label(deco, new Vector3(14.96f, 0.55f, 45f), Yaw(90f), "COFFEE MACHINE\n<size=60%>DETAINED FOR HELPING</size>", 0.2f, Ink, 1.9f);
            Sign(deco, new Vector3(15.5f, 3.2f, 51.65f), 0f, "ME-CHA MAINTENANCE\n<size=50%>FIST HATES SOFT FLOORS · SKATES HATE BUMPS · BRAIN RUNS ON PERCY</size>", 6.2f, 1.5f, SignStyle.Info);

            // ---------------- arena ME-TV + poster + throne
            var tv = StoryKit.MeTvScreen(deco, new Vector3(-8f, 3.5f, 51.6f), Yaw(0f), 5f);
            if (tv != null) { ctx.Mark("story.metv.arena", tv); boss.ArenaTv = tv.GetComponentInChildren<MeTvScreenView>(); }
            EnvKit.Poster(deco, new Vector3(-18.7f, 2.6f, 40f), Yaw(-90f), "ME-CHA", "I'M THE STRONG ONE, THE FAST ONE\nAND THE SMART ONE.", new Vector2(1.6f, 2.2f));
            EnvKit.Decor.Tower.BlackGlassPanel(deco, new Vector3(-11f, 0f, 51.6f), Yaw(0f), 3f, 4f, 3, 4, false);

            // ---------------- DETENTION (VIP): door, INTAKE, inside button, dome, lamp
            var doorSig = Kit.Make("CellDoorSignal", ctx.Puzzles, new Vector3(19f, 0f, 40f)).AddComponent<L13Signal>();
            ctx.Mark("s4.cellDoor", doorSig.gameObject);
            var door = PuzzleKit.Door(ctx.Puzzles, new Vector3(19f, 0f, 40f), Yaw(90f), DoorMode.SlideUp, 4f, 4.5f, doorSig);
            if (door != null) ctx.Mark("s4.door", door.gameObject);
            Sign(deco, new Vector3(18.55f, 5.35f, 40f), 90f, "DETENTION (VIP)\n<size=55%>NOW ACCEPTING MANAGEMENT</size>", 5f, 1.1f, SignStyle.Danger);
            var intake = PuzzleKit.Terminal(ctx.Puzzles, new Vector3(18.5f, 0f, 35.5f), Yaw(-90f), 7, false, 8f, "");
            ctx.Mark("s4.intake", intake.gameObject);
            ctx.MarkPoint("s4.intake.stand", new Vector3(17.6f, 0f, 35.5f));
            PerspectiveKit.Reveal(intake.gameObject, PerspectiveMask.Intellect, "INTAKE (IQ 7, 8 s)", true, false, new Vector3(0f, 2.2f, 0f));
            ctx.Label(deco, new Vector3(18.7f, 2.9f, 35.5f), Yaw(90f), "INTAKE\n<size=60%>VIP DETENTION</size>", 0.34f, Gold, 2.4f);
            var inside = PuzzleKit.PushButton(ctx.Puzzles, new Vector3(23.5f, 0f, 37.5f), Yaw(0f), 3f, "");
            ctx.Mark("s4.cell.button", inside.gameObject);
            ctx.Label(deco, new Vector3(23.5f, 2.4f, 37.3f), Yaw(180f), "LET ME OUT\n<size=60%>(I'M NOT A VIP)</size>", 0.26f, Ink, 2.2f);
            var dome = Kit.Make("VipDome", deco, new Vector3(22f, 2.8f, 40f)).transform;
            ProcMesh.AddRenderer(Kit.Make("Glass", dome, new Vector3(0f, 1.1f, 0f)), ProcMesh.Cached("l13_vipdome", () => ProcMesh.Cylinder(1.3f, 2.2f, 22, true)), Kit.Mat(MatKey.Glass), false);
            ProcMesh.AddRenderer(Kit.Make("Rim", dome, new Vector3(0f, 0.05f, 0f), Quaternion.Euler(90f, 0f, 0f)), ProcMesh.Cached("l13_vipdome_rim", () => ProcMesh.Torus(1.32f, 0.08f, 24, 6)), Kit.Mat(MatKey.Gold), false);
            ProcMesh.AddRenderer(Kit.Make("Cap", dome, new Vector3(0f, 2.22f, 0f)), ProcMesh.Cached("l13_vipdome_cap", () => ProcMesh.Cylinder(1.34f, 0.1f, 22, true)), Kit.Mat(MatKey.Gold), false);
            GoldLight(ctx.Lights, new Vector3(22f, 4.3f, 40f), Gold, 2.2f, 8f);
            EnvKit.Poster(deco, new Vector3(24.7f, 2.4f, 40f), Yaw(90f), "TRUST NO ONE.", "ESPECIALLY ON TRUST FALLS.", new Vector2(1.4f, 1.9f));
            var det = Kit.Make("Detention", ctx.Puzzles, CellSeat).AddComponent<L13Detention>();
            det.Intake = intake; det.InsideButton = inside; det.DoorSignal = doorSig; det.Pod = pod; det.Dome = dome;
            det.Zone = new Bounds(new Vector3(22.5f, 1.5f, 40f), new Vector3(5f, 3f, 6f));
            det.Alcove = new Bounds(new Vector3(22f, 1.5f, 40f), new Vector3(6.2f, 3f, 6f));
            ctx.Mark("s4.detained", det.gameObject);
            boss.Detention = det;
            ctx.MarkPoint("s4.cellFront", new Vector3(17f, 0f, 40f));
            ctx.MarkPoint("s4.cell.drop", new Vector3(21.5f, 0f, 40f));
            ctx.MarkPoint("s4.cell.seat", new Vector3(22.5f, 0f, 40f));

            // ---------------- north door (opens on defeat), exit path, wreck scare
            var north = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 52f), Yaw(0f), DoorMode.SlideUp, 4f, 4.5f, boss);
            if (north != null) ctx.Mark("s5.northDoor", north.gameObject);
            ctx.MarkPoint("s5.path.1", new Vector3(12f, 0f, 45.5f));
            ctx.MarkPoint("s5.door", new Vector3(0f, 0f, 50.5f));
            var wreck = StoryKit.ScriptTrigger(ctx.Gameplay, new Vector3(0f, 2f, 47.5f), new Vector3(12f, 4f, 5f), "ch13.wreck", true);
            if (wreck != null) { wreck.Id = ""; wreck.enabled = false; boss.WreckTrigger = wreck; }   // the boss raises ch13.wreck after ScareDirector.TryPlay

            // ---------------- staging (C13), tutorials, hints
            ctx.MarkPoint("story.c13.cam", new Vector3(8f, 4f, 36f));
            ctx.MarkPoint("story.c13.brute", new Vector3(16f, 0f, 39f));
            ctx.MarkPoint("story.c13.spr", new Vector3(16f, 0f, 41f));
            ctx.MarkPoint("story.c13.brain", new Vector3(16f, 0f, 37f));
            ctx.Tutorial(new Vector3(0f, 1.5f, 19f), new Vector3(24f, 3f, 12f), "ch13_fist", "THE FIST",
                "Dodge the shadow. A missed fist gets STUCK. Only the Brute can punch it (E).", new[] { "E" }, true);
            ctx.Tutorial(new Vector3(-13.5f, 1.5f, 15.5f), new Vector3(4f, 3f, 4f), "ch13_bumps", "THE BUMP RUN",
                "Press, then TURBO through both gates before the timer ends. Only the one who pressed counts.", new[] { "E", "LShift" }, false);
            boss.TutFirewall = ctx.Tutorial(new Vector3(0f, 2f, 30f), new Vector3(38f, 4f, 44f), "ch13_firewall", "THE BRAIN DOME",
                "The smart one hacks both balcony terminals. Then anyone with a gun shoots the antenna.", new[] { "E", "LMB" }, false);
            boss.TutCapture = ctx.Tutorial(new Vector3(0f, 2f, 30f), new Vector3(38f, 4f, 44f), "ch13_capture", "THE CAPTURE",
                "Stun him: bump him mid-speech, shoot him, TAUNT him (T) or TURBO-tag him. Then the Brute grabs the pod (F).", new[] { "T", "F" }, true);
            if (boss.TutFirewall != null) boss.TutFirewall.SetActive(false);
            if (boss.TutCapture != null) boss.TutCapture.SetActive(false);
            ctx.Tutorial(new Vector3(16.5f, 1.5f, 38f), new Vector3(4f, 3f, 7f), "ch13_detention", "DETENTION",
                "DETENTION opens only while the smart one holds the INTAKE (8 s). Drop him inside.", new[] { "E" }, false);
            ctx.Hint(new Vector3(0f, 2f, 21f), new Vector3(18f, 4f, 10f),
                "Let the fist miss: step out of its shadow. While it's stuck, the Brute punches it (E). Three times.", "Big fists get stuck. Punch back.");
            ctx.Hint(new Vector3(-13f, 2f, 15.5f), new Vector3(6f, 4f, 4f),
                "Press the bump button by the south-west break area, then TURBO through both gates. His skates do the rest.", "He hates speed bumps. Be fast.");
            ctx.Hint(new Vector3(-17f, 4f, 28f), new Vector3(4f, 3f, 6f),
                "Smart one: hack both balcony terminals. Then shoot the antenna from the break area behind him.", "Firewalls first. He's drowsy.");
            ctx.Hint(new Vector3(16f, 2f, 38f), new Vector3(5f, 4f, 7f),
                "Bump him while he monologues, TAUNT within 9 m, TURBO-tag or shoot him. The Brute carries the pod; the smart one opens DETENTION.", "He can't resist an argument. Or a cell.");
        }

        /// <summary>Swaps the kit TimedButton component for an L13BumpButton (records the presser), keeping its parts.</summary>
        static L13BumpButton MakeBumpButton(TimedButton t)
        {
            var go = t.gameObject;
            float len = t.RouteLength; var cap = t.Cap; var lamp = t.Lamp; var rev = t.Reveal; string msg = t.SolvedMessage; bool hud = t.ShowHudTimer;
            Object.DestroyImmediate(t);
            var b = go.AddComponent<L13BumpButton>();
            b.RouteLength = len; b.Cap = cap; b.Lamp = lamp; b.Reveal = rev; b.SolvedMessage = msg; b.ShowHudTimer = hud;
            return b;
        }
    }
}
