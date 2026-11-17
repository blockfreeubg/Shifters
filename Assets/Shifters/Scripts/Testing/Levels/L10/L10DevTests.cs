// =====================================================================================
//  SHIFTERS - L10DevTests.cs   [LEVEL 10 - DevPlay: static checks + one seeded solution run with screenshots]
//  devplay.sh <proj> l10dev -devSetup Shifters.L10DevTests.Run -devSeconds 5
//    (runtime build of L10, no scene file needed; add -devScene .../L10_LightsOut.unity to use the built scene)
//  Mild:  ... -devSetup Shifters.L10DevTests.RunMild
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L10DevTests
    {
        static readonly string[] Marks =
        {
            "spawn", "checkpoint.1", "checkpoint.2", "checkpoint.3", "exit", "s0.locker", "s0.lockerStand", "s1.pool.b",
            "s1a.battery", "s1.breaker1", "s1a.plate.center", "s1b.timed", "s1b.stand", "s1b.lever", "s1b.leverStand",
            "s1c.terminal", "s1c.termStand", "s1c.uv.1", "s1c.uv.2", "s1c.door", "s1.lobbyDoor", "s1.dummyW", "s1.dummyE",
            "s1.cellLever", "s2.phone", "s2.shelf", "s3.call", "s3.callStand", "s3.flood", "s3.cabDoor", "s3.corridorW",
            "s3.guardW", "s3.dummyW", "s3.dummyE", "story.metv", "s3.cellLever", "s3.cellLeverStand",
        };

        static readonly string[] Beats = { "ch09.garage", "ch09.dummy", "ch09.copier", "ch09.phone", "ch09.elevator", "ch09.reveal" };

        public static IEnumerator Run(DevPlayContext ctx) => RunCore(ctx, false);
        public static IEnumerator RunMild(DevPlayContext ctx) => RunCore(ctx, true);

        static IEnumerator RunCore(DevPlayContext ctx, bool mild)
        {
            ScareDirector.MildOverride = mild;
            string pre = mild ? "l10mild_" : "l10_";
            var fired = new HashSet<string>();
            System.Action<string> onBeat = id => fired.Add(id);
            GameEvents.StoryBeat += onBeat;
            bool first = true;
            L10Solution.SectionDone = name =>
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.Hide();   // clean screenshots (players dismiss with Backspace)
                ctx.Shot(pre + name);
                if (name == "s0") Scenic(ctx, pre);
                if (name == "s3reveal") { ctx.Shot(pre + "s3reveal_top", TopCam(new Vector3(0f, 4.6f, 65.5f), new Vector3(0f, 1.2f, 80f))); DestroyTopCam(); }
            };
            int labelsAtC2 = -1, labelsAtReveal = -1, hiddenAtReveal = -1; bool camAtReveal = false, allInCab = false;
            L10Solution.HugOverride = !mild;     // the main run also proves capture -> rescue -> continue
            var shotHook = L10Solution.SectionDone;
            L10Solution.SectionDone = name =>
            {
                var dir = L10Director.Instance;
                if (name == "s1" && dir != null) labelsAtC2 = dir.ThreatLabelsShown;
                if (name == "s3reveal" && dir != null)
                {
                    labelsAtReveal = CountShownLabels();
                    camAtReveal = dir.RevealCamActive;
                    var cm = CharacterManager.Instance;
                    allInCab = true;
                    if (cm != null) foreach (var c in cm.Team) if (c != null && c.IsAlive && c.transform.position.z < 79.2f) allInCab = false;
                }
                if (name == "s3hug") { ctx.Shot(pre + "s3hug_cell", TopCam(new Vector3(-0.6f, 3.4f, 58.6f), new Vector3(-5.6f, 1f, 52.8f))); DestroyTopCam(); }
                shotHook?.Invoke(name);
            };
            System.Func<TeamBot, IEnumerator> solve = bot => Wrapped(ctx, bot, pre, first ? StaticChecks(ctx) : null);
            yield return TeamBot.RunRuntimeLevel(ctx, 9, solve, "L10dev", new[] { 1 }, L10Solution.Options());
            L10Solution.SectionDone = null;
            L10Solution.HugOverride = false;
            GameEvents.StoryBeat -= onBeat;
            ctx.Check(labelsAtC2 == 0, "no ThreatLabel allowed at checkpoint 2 (S1 labels off, S3 asleep): " + labelsAtC2);
            ctx.Check(labelsAtReveal == 0, "no ThreatLabel shown during the reveal: " + labelsAtReveal);
            ctx.Check(camAtReveal, "reveal cinematic owns the camera at the reveal shot");
            ctx.Check(allInCab, "reveal fired with every alive intern in the cab");
            if (!mild)
            {
                ctx.Check(L10Solution.HugCaptured, "hug proof: intern held in the S3 TIME-OUT cage");
                ctx.Check(L10Solution.HugRescued, "hug proof: released by the lever, run continued");
            }
            foreach (var b in Beats) ctx.Check(fired.Contains(b), "beat fired: " + b);
            ScareDirector.MildOverride = null;
        }

        static IEnumerator Wrapped(DevPlayContext ctx, TeamBot bot, string pre, IEnumerator checks)
        {
            if (checks != null) yield return checks;
            ctx.Shot(pre + "spawn");
            yield return L10Solution.Solve(bot);
        }

        /// <summary>Scenic shots with the HUD hidden (taken once the garage is dark).</summary>
        static void Scenic(DevPlayContext ctx, string pre)
        {
            var canvases = new List<Canvas>();
            foreach (var c in Object.FindObjectsByType<Canvas>()) if (c.enabled && c.renderMode != RenderMode.WorldSpace) { canvases.Add(c); c.enabled = false; }
            ctx.Shot(pre + "ov_farm", TopCam(new Vector3(0f, 34f, 4f), new Vector3(0f, 0f, 30f)));
            ctx.Shot(pre + "ov_s3", TopCam(new Vector3(0f, 22f, 58f), new Vector3(0f, 0f, 70f)));
            ctx.Shot(pre + "garage_lit", TopCam(new Vector3(0f, 3.2f, -9f), new Vector3(0f, 1.6f, 6f)));
            ctx.Shot(pre + "mailroom", TopCam(new Vector3(-10.5f, 3.4f, 21.5f), new Vector3(-14f, 0.8f, 30f)));
            ctx.Shot(pre + "copyroom", TopCam(new Vector3(0f, 3.4f, 51.5f), new Vector3(0f, 1f, 62f)));
            ctx.Shot(pre + "lobby", TopCam(new Vector3(0f, 3.2f, 67f), new Vector3(0f, 1.2f, 79f)));
            DestroyTopCam();
            foreach (var c in canvases) if (c != null) c.enabled = true;
        }

        static IEnumerator StaticChecks(DevPlayContext ctx)
        {
            yield return null;
            var lctx = LevelRuntimeBuilder.LastContext;
            if (lctx != null)
            {
                ctx.Check(!lctx.Failed, "L10 builds at runtime");
                ctx.Check(lctx.Warnings.Count == 0, "builder warnings: " + lctx.Warnings.Count + (lctx.Warnings.Count > 0 ? " first: " + lctx.Warnings[0] : ""));
                ctx.Log(lctx.Report());
            }
            foreach (var m in Marks) ctx.Check(LevelMark.Exists(m), "mark " + m);
            int lights = 0, pools = 0;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.enabled && l.type != LightType.Directional && l.gameObject.activeInHierarchy) lights++;
            foreach (var p in Object.FindObjectsByType<LightPool>()) if (p.RealLight != null) pools++;
            ctx.Check(pools <= 3, "real pool lights <= 3: " + pools);
            ctx.Log("non-sun lights enabled at start: " + lights);
            ctx.Check(L10Director.Instance != null, "director present");
            ctx.Check(FlashlightRig.Instance != null, "flashlight rig present");
            ctx.Check(StalkerBrain.All.Count == 4, "4 dummies: " + StalkerBrain.All.Count);
            ctx.Check(HoldingCell.Find(L10Builder.CellId) != null, "trust exercise room cell");
            ctx.Check(HoldingCell.Find(L10Builder.CellS3Id) != null, "S3 TIME-OUT cell");
            foreach (var s in StalkerBrain.All)
                if (s != null && s.transform.position.z > 70f) ctx.Check(s.CellId == L10Builder.CellS3Id, "S3 dummy delivers to the TIME-OUT cell: " + s.CellId);
            // bot snag probes: what solid colliders sit near the two spots where the bot hopped (evidence for the report)
            Probe(ctx, new Vector3(-3.5f, 0.9f, 21.7f), 1.1f, "pool B approach");
            Probe(ctx, new Vector3(14f, 0.9f, 42f), 0.9f, "hall lever");
            int rbs = Object.FindObjectsByType<Rigidbody>().Length;
            int rends = Object.FindObjectsByType<Renderer>().Length;
            ctx.Log("rigidbodies " + rbs + ", renderers " + rends);
            ctx.Check(rbs <= 25, "rigidbodies <= 25: " + rbs);
            ctx.Check(rends <= 1600, "renderers <= 1600: " + rends);
        }

        static int CountShownLabels()
        {
            int n = 0;
            foreach (var m in Object.FindObjectsByType<ThreatMarker>()) if (m != null && m.isActiveAndEnabled && m.IsShown) n++;
            return n;
        }

        static void Probe(DevPlayContext ctx, Vector3 p, float r, string what)
        {
            var hits = Physics.OverlapSphere(p, r, ~0, QueryTriggerInteraction.Ignore);
            var sb = new System.Text.StringBuilder();
            foreach (var h in hits) sb.Append(h.name).Append('@').Append(h.transform.parent != null ? h.transform.parent.name : "-").Append(' ');
            ctx.Log("probe " + what + " " + p + " r " + r + ": " + hits.Length + " colliders: " + sb);
        }

        static Camera _top;
        static Camera TopCam(Vector3 pos, Vector3 look)
        {
            if (_top == null)
            {
                var go = new GameObject("L10TopCam");
                _top = go.AddComponent<Camera>();
                _top.fieldOfView = 60f;
                _top.nearClipPlane = 0.1f; _top.farClipPlane = 300f;
                _top.enabled = false;
            }
            _top.transform.position = pos;
            _top.transform.LookAt(look);
            return _top;
        }
        static void DestroyTopCam() { if (_top != null) Object.Destroy(_top.gameObject); _top = null; }
    }
}
