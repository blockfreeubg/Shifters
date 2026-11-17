// =====================================================================================
//  SHIFTERS - L8DevTests.cs   [L8 dev scenarios]
//  Shifters.L8DevTests.All        : runtime-build L8, checks + overview shots, the full solution, "after" shots
//                                   (-botSeed N, -l8Shots 0/1, -l8From 1..3 dev start section, -l8NoTool 1)
//  Shifters.L8DevTests.SceneShots : checks + shots on the generated scene (-devScene .../L8_ColorReserve.unity)
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L8DevTests
    {
        static readonly (string name, Vector3 pos, Vector3 look)[] Views =
        {
            ("01_antechamber", new Vector3(1.5f, 4.2f, -7.2f), new Vector3(-2.5f, 1.8f, 6f)),
            ("02_laser_gallery", new Vector3(0.4f, 3.0f, 11.2f), new Vector3(0f, 0.9f, 40f)),
            ("03_rotunda", new Vector3(1f, 13f, 55.5f), new Vector3(0f, 1.5f, 76f)),
            ("04_vault_door", new Vector3(-3.5f, 3.2f, 72.5f), new Vector3(0f, 4.6f, 85f)),
            ("05_tumblers", new Vector3(-3.5f, 4.5f, 60f), new Vector3(-10.5f, 0.5f, 70f)),
            ("06_keys_gallery", new Vector3(-6f, 6f, 64f), new Vector3(8f, 2.5f, 73f)),
            ("07_portrait", new Vector3(-7.5f, 3.6f, 71.5f), new Vector3(-15.5f, 3.9f, 74.5f)),
            ("08_reserve", new Vector3(0f, 6.5f, 93.2f), new Vector3(0f, 1.2f, 108f)),
            ("10_door_close", new Vector3(0.5f, 5.5f, 77.5f), new Vector3(0f, 6.2f, 85f)),
        };

        public static IEnumerator All(DevPlayContext ctx)
        {
            int seed = BotArgs.Int("-botSeed", 0);
            if (seed != 0) TeamBot.ForceTeam(seed);
            var lctx = LevelRuntimeBuilder.BuildInCurrentScene(7);
            ctx.Check(lctx != null && !lctx.Failed, "L8 runtime build");
            if (lctx != null) ctx.Log(lctx.Report());
            yield return ctx.Wait(1.5f);
            bool shots = BotArgs.Int("-l8Shots", 1) != 0;
            yield return Checks(ctx);
            if (shots) { yield return ctx.Wait(3.5f); yield return TakeViews(ctx, ""); }
            L8Solution.DevStartSection = BotArgs.Int("-l8From", 1);
            var opts = L8Solution.Options();
            opts.AllowDebugAssists = L8Solution.DevStartSection > 1;
            yield return TeamBot.Run(ctx, L8Solution.Solve, opts, "L8rt");
            L8Solution.DevStartSection = 1;
            if (shots) { yield return ctx.Wait(1.0f); yield return TakeViews(ctx, "after_", 3, 7, 8); }
        }

        public static IEnumerator SceneShots(DevPlayContext ctx)
        {
            yield return ctx.Wait(6.0f);
            yield return Checks(ctx);
            yield return TakeViews(ctx, "scene_");
            yield return SwapAlternative(ctx);
        }

        /// <summary>The S1 SWAP alternative, functionally: from the SWAP SPOT, SWAP-O-MATIC the deposit box -> the shooter lands
        /// beyond the last laser curtain (z > 40.5) and the box lands at the swap spot.</summary>
        static IEnumerator SwapAlternative(DevPlayContext ctx)
        {
            var box = LevelMark.Find("s1.depositBox");
            ShifterCharacter ch = null;
            foreach (var c in Object.FindObjectsByType<ShifterCharacter>()) { ch = c; break; }
            ctx.Check(box != null && ch != null, "swap test: deposit box + a character");
            if (box == null || ch == null) yield break;
            Vector3 spot = LevelMark.Position("s1.swapSpot") + Vector3.up * 0.1f;
            var rb = ch.GetComponent<Rigidbody>();
            ch.transform.position = spot;
            if (rb != null) { rb.position = spot; rb.linearVelocity = Vector3.zero; }
            Physics.SyncTransforms();
            yield return ctx.Wait(0.6f);
            var h = ch.GetComponentInChildren<WeaponHolder>();
            var col = box.GetComponentInChildren<Collider>();
            string why = WeaponTools.SwapCheck(ch, col, out _);
            ctx.Log("swap check from the SWAP SPOT: " + (why ?? "allowed"));
            bool ok = WeaponTools.TrySwap(h, col, box.transform.position);
            yield return ctx.Wait(0.8f);
            ctx.Check(ok, "SWAP-O-MATIC swap with the deposit box happened");
            ctx.Check(ch.Feet.z > L8Builder.CurtainZ[2] + 0.3f, "shooter landed beyond the last curtain (z " + ch.Feet.z.ToString("0.0") + ")");
            ctx.Check(box.transform.position.z < L8Builder.CurtainZ[0], "deposit box landed at the swap spot (z " + box.transform.position.z.ToString("0.0") + ")");
            var cam = Camera.main;
            if (cam != null) ctx.Shot("scene_11_after_swap", cam);
        }

        static IEnumerator Checks(DevPlayContext ctx)
        {
            string[] marks = { "spawn", "s0.locker", "s0.tipJar", "s1.timed", "s1.lever", "s1.laserToggle", "s1.depositBox", "s1.swapSpot",
                               "checkpoint.1", "s2.pallet", "s2.plateI", "s2.plateII", "s2.plateIII", "s2.crateA", "s2.barrel", "s2.lockA",
                               "s2.K1.key", "s2.K2.key", "s2.K3.key", "s2.K1.pad", "s2.lockB", "s2.lipEdge", "s2.galleryLanding", "s2.throwSpot",
                               "s2.podium", "s2.dial.CROWN", "s2.dial.MONOCLE", "s2.dial.MUSTACHE", "s2.dial.DRUM", "s2.sequence", "s2.lockC",
                               "s2.vaultDoor", "checkpoint.2", "s3.prism", "s3.pedestal", "s3.socket", "s3.valve", "s3.alarm", "s3.exitDoor",
                               "story.valve", "story.metv", "exit" };
            foreach (var m in marks) ctx.Check(LevelMark.Exists(m), "mark " + m);
            var tb = LevelMark.Find<TimedButton>("s1.timed");
            ctx.Check(tb != null && Mathf.Abs(tb.RouteLength - L8Builder.RouteLength) < 0.01f, "timed route 28 m");
            var c1 = LevelMark.Find<LaserBarrier>("s1.curtain.3");
            ctx.Check(c1 != null && c1.IsActive, "last laser curtain active before the run");
            var door = LevelMark.Find<L8VaultDoor>("s2.vaultDoor");
            ctx.Check(door != null && !door.Powered, "vault door closed at start");
            var exitDoor = LevelMark.Find<Door>("s3.exitDoor");
            ctx.Check(exitDoor != null, "exit door exists");
            // shot line podium -> dials (rule 4: visible from the shot pivot)
            foreach (var n in new[] { "CROWN", "MONOCLE", "MUSTACHE", "DRUM" })
            {
                var st = LevelMark.Find<ShootTarget>("s2.dial." + n);
                if (st == null) continue;
                Vector3 from = LevelMark.Position("s2.podium") + Vector3.up * 1.15f;
                Vector3 to = st.Disc != null ? st.Disc.position : st.transform.position;
                bool hit = Physics.Linecast(from, to, out var h, ~0, QueryTriggerInteraction.Ignore);
                bool ok = hit && h.collider != null && h.collider.GetComponentInParent<ShootTarget>() == st;
                ctx.Check(ok, "dial " + n + " visible from the podium (hit " + (hit && h.collider != null ? h.collider.name : "nothing") + ")");
            }
            // swap line of sight: swap spot -> deposit box
            var box = LevelMark.Find("s1.depositBox");
            if (box != null)
            {
                Vector3 from = LevelMark.Position("s1.swapSpot") + Vector3.up * 1.4f;
                bool hit = Physics.Linecast(from, box.transform.position, out var h, ~0, QueryTriggerInteraction.Ignore);
                ctx.Check(hit && h.rigidbody != null && h.rigidbody.gameObject == box, "deposit box visible from the SWAP SPOT (hit " + (hit && h.collider != null ? h.collider.name : "nothing") + ")");
            }
            int bad = 0, rends = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                rends++;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name.Contains("InternalErrorShader") || m.name.StartsWith("Default-Material") || m.name == "Lit")
                { if (bad < 5) ctx.Log("bad material on " + r.name); bad++; }
            }
            ctx.Check(bad == 0, "no default/magenta materials (" + bad + ")");
            int rbs = Object.FindObjectsByType<Rigidbody>().Length;
            int lights = 0; foreach (var l in Object.FindObjectsByType<Light>()) if (l.type != LightType.Directional && l.enabled) lights++;
            ctx.Log("L8 budget: renderers " + rends + ", rigidbodies " + rbs + ", non-sun lights " + lights);
            ctx.Check(rends <= 2200 && rbs <= 40, "within the hard caps");
            yield return null;
        }

        static IEnumerator TakeViews(DevPlayContext ctx, string prefix, params int[] only)
        {
            var go = new GameObject("L8ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 62f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 600f;
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            for (int i = 0; i < Views.Length; i++)
            {
                if (only != null && only.Length > 0 && System.Array.IndexOf(only, i) < 0) continue;
                var v = Views[i];
                go.transform.position = v.pos;
                go.transform.rotation = Quaternion.LookRotation(v.look - v.pos, Vector3.up);
                yield return null;
                ctx.Shot(prefix + v.name, cam);
            }
            Object.Destroy(go);
            if (main != null) ctx.Shot(prefix + "09_gameplay", main);
        }
    }
}
