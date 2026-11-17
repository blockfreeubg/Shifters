// =====================================================================================
//  SHIFTERS - L11Shots.cs   [SHIFT 10]  DevPlay: static checks + key-moment screenshots (debug teleports allowed
//  here: this is a picture/sanity scenario, NOT the proof run).
//  Run: $SP/tools/devplay.sh <proj> l11_shots -devSetup Shifters.L11Shots.Run -devScene Assets/Shifters/Scenes/L11_MeMorialPark.unity -devSeconds 1
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L11Shots
    {
        static readonly string[] Marks =
        {
            "spawn", "s1.A", "s1.B", "s1.C", "s1.gate", "checkpoint.1", "s2.start", "s2.bridgeNear", "s2.ghostBridge", "s2.ghostZone",
            "checkpoint.2", "s3.door", "s3.mausoleum", "s3.ownGrave", "s3.bell", "exit", "story.metv", "story.statue", "story.mausoleum",
            "tutorial.ch10_huddle", "tutorial.ch10_ghosts", "hint.1", "hint.2",
        };
        static readonly string[] Beats = { "ch10.start", "ch10.gate", "ch10.ghosts", "ch10.ghosts.alone", "ch10.ownGrave", "ch10.mausoleum" };

        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(1.0f);

            // ---------------- static checks (DoD 4)
            foreach (var m in Marks) ctx.Check(LevelMark.Exists(m), "mark " + m);
            for (int g = 1; g <= 3; g++)
                foreach (var l in L11Builder.Lane) { ctx.Check(LevelMark.Exists("s2.g" + g + ".takeoff." + l), "takeoff g" + g + l); ctx.Check(LevelMark.Exists("s2.g" + g + ".landing." + l), "landing g" + g + l); }
            foreach (var b in Beats) ctx.Check(StoryScripts.Exists(b), "story beat defined: " + b);
            ctx.Check(CheckpointManager.Find(1) != null && CheckpointManager.Find(2) != null && CheckpointManager.Find(3) == null, "exactly 2 checkpoints");
            int real = 0, lights = 0;
            var names = new System.Text.StringBuilder();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var li in Object.FindObjectsByType<Light>())
                if (li.type != LightType.Directional && li.enabled && li.gameObject.activeInHierarchy && li.gameObject.scene == scene && li.GetComponentInParent<PortraitStudio>() == null && (li.transform.parent == null || !li.transform.parent.name.StartsWith("PortraitSlot"))) { lights++; names.Append(li.name).Append('/').Append(li.transform.parent != null ? li.transform.parent.name : "-").Append(' '); }
            ctx.Log("scene lights: " + names);
            foreach (var lp in Object.FindObjectsByType<LightPool>()) if (lp.GetComponentInChildren<Light>(true) != null) real++;
            ctx.Log("non-sun lights enabled: " + lights + ", real pool lights: " + real);
            ctx.Check(real <= 1, "<= 1 real pool light");
            ctx.Check(lights <= 6, "<= 6 non-sun lights");
            ctx.Check(Object.FindAnyObjectByType<FlashlightRig>() != null, "flashlight rig present");
            int rb = Object.FindObjectsByType<Rigidbody>().Length;
            int rends = Object.FindObjectsByType<Renderer>().Length;
            ctx.Log("rigidbodies " + rb + ", renderers " + rends);
            ctx.Check(rends <= 1600, "renderers <= 1600");
            // GUN bonus bell visible from the C2 shot pivot and from the plaza
            var bell = LevelMark.Find("s3.bell");
            if (bell != null)
            {
                foreach (var from in new[] { new Vector3(0f, 1.2f, 55f), new Vector3(1f, 1.2f, 59f) })
                {
                    var col = bell.GetComponentInChildren<Collider>();
                    Vector3 to = col != null ? col.bounds.center : bell.transform.position;
                    bool hit = Physics.Raycast(from, (to - from).normalized, out var h, 40f, ~0, QueryTriggerInteraction.Ignore);
                    ctx.Check(hit && h.collider != null && h.collider.transform.IsChildOf(bell.transform), "bell visible from " + from + (hit ? " (hit " + h.collider.name + ")" : " (no hit)"));
                }
            }
            // crumbling soil falls under a walker: physics sanity of the grave tiles
            var tile = LevelMark.Find<CrumblingPlatform>("s2.grave1");
            ctx.Check(tile != null && tile.ShakeTime < 0.3f, "grave soil collapses fast (walking drops you in)");

            var cm = CharacterManager.Instance;
            var cam = new GameObject("L11ShotCam").AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f;
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }

            // 1 spawn / back gate
            Aim(cam, new Vector3(2.5f, 3.2f, -9.5f), new Vector3(0f, 1.8f, 4f));
            yield return ctx.Wait(0.3f);
            ctx.Shot("l11_s0_backgate", cam);

            // 2 huddle: team on the plates, gate opening
            if (cm != null && cm.Team.Count >= 3)
            {
                Tp(cm.Team[0], LevelMark.Position("s1.A")); Tp(cm.Team[1], LevelMark.Position("s1.B")); Tp(cm.Team[2], LevelMark.Position("s1.C"));
            }
            yield return ctx.Wait(2.0f);
            ctx.Check(TeamBot.IsPowered("s1.gate"), "huddle gate opens with three on the plates");
            Aim(cam, new Vector3(-5.5f, 4.5f, 2f), new Vector3(0f, 1.2f, 12f));
            ctx.Shot("l11_s1_huddle", cam);

            // 3 the graves (from above the path)
            Aim(cam, new Vector3(0f, 6.5f, 19f), new Vector3(0f, 0f, 33f));
            yield return ctx.Wait(0.2f);
            ctx.Shot("l11_s2_graves", cam);

            // 4 ghost bridge: whole team at the edge -> bridge out
            if (cm != null)
                for (int i = 0; i < cm.Team.Count; i++) Tp(cm.Team[i], new Vector3(L11Builder.BridgeLaneX[i % 3], 0f, 44.4f));
            yield return ctx.Wait(3.0f);
            ctx.Check(TeamBot.IsPowered("s2.ghostBridge"), "ghost bridge extends for the whole team");
            Aim(cam, new Vector3(1.2f, 5.2f, 38.8f), new Vector3(0f, 0f, 50f));
            ctx.Shot("l11_s2_ghostbridge", cam);
            // one wanders off -> bridge fades after the 1.5 s grace
            if (cm != null && cm.Team.Count >= 3) Tp(cm.Team[2], new Vector3(0f, 0f, 34f));
            yield return ctx.Wait(3.5f);
            ctx.Check(!TeamBot.IsPowered("s2.ghostBridge"), "ghost bridge fades when someone wanders off");

            // 5 the mausoleum plaza + 6 the trust-fall tableau (C09 camera mark)
            if (cm != null)
                for (int i = 0; i < cm.Team.Count; i++) Tp(cm.Team[i], new Vector3(-1.5f + 1.5f * i, 0f, 58.5f));
            yield return ctx.Wait(2.0f);
            Aim(cam, new Vector3(1.5f, 3.2f, 51.5f), new Vector3(0f, 3.2f, 64f));
            ctx.Shot("l11_s3_plaza", cam);
            Aim(cam, LevelMark.Position("story.mausoleum"), LevelMark.Position("story.statue"));
            cam.fieldOfView = 50f;
            ctx.Shot("l11_s3_trustfall", cam);

            // 7 Spooky MILD: sticker dummy
            ScareDirector.MildOverride = true;
            var w = Object.FindAnyObjectByType<L11DummyWatcher>();
            if (w != null) { if (w.Dummy != null) w.Dummy.gameObject.SetActive(false); if (w.MildDummy != null) w.MildDummy.gameObject.SetActive(true); }
            if (cm != null) Tp(cm.Team[0], new Vector3(-3f, 0f, -1f));
            cam.fieldOfView = 55f;
            Aim(cam, new Vector3(-2.5f, 2.2f, -3.5f), new Vector3(-5.6f, 1.2f, -0.5f));
            yield return ctx.Wait(1.0f);
            ctx.Shot("l11_s0_dummy_mild", cam);
            ScareDirector.MildOverride = null;

            // 8 overview
            Aim(cam, new Vector3(18f, 26f, -6f), new Vector3(0f, 0f, 34f));
            cam.fieldOfView = 60f;
            ctx.Shot("l11_overview", cam);
            Object.Destroy(cam.gameObject);
        }

        static void Aim(Camera cam, Vector3 from, Vector3 at) => cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));

        static void Tp(ShifterCharacter c, Vector3 p)
        {
            if (c == null) return;
            var rot = Quaternion.identity;
            if (c.Motor != null) c.Motor.Teleport(p + Vector3.up * 0.05f, rot); else c.transform.SetPositionAndRotation(p, rot);
        }
    }
}
