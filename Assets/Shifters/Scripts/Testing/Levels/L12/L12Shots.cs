// =====================================================================================
//  SHIFTERS - L12Shots.cs   [SHIFT 11]  DevPlay: static checks + key-moment screenshots (debug teleports allowed here:
//  this is a picture/sanity scenario, NOT the proof run). Also exercises the deliberate spook + rescue (boss extra d)
//  and the fizzle-by-flashlight idea through the real boss.
//  Run: $SP/tools/devplay.sh <proj> l12_shots -devSetup Shifters.L12Shots.Run -devScene Assets/Shifters/Scenes/L12_Unmasked.unity -devSeconds 1
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L12Shots
    {
        static readonly string[] Marks =
        {
            "spawn", "checkpoint.1", "checkpoint.2", "s0.locker", "s0.leverStand", "s1.closet", "s1.closetLever", "s1.stairW.top", "s1.stairW.bottom",
            "s1.stairE.top", "s1.stairE.bottom", "s1.crankW", "s1.crankW.stand", "s1.fuseE", "s1.fuseE.stand", "s1.race.button", "s1.race.stand",
            "s1.race.curtain", "s1.race.lever", "s1.race.leverStand", "s1.floodW", "s1.floodN", "s1.floodE", "s1.legW", "s1.lane.1", "s1.lane.2",
            "s1.lane.3", "s1.darkSpot", "s2.padW", "s2.padE", "s2.padE.punchStand", "s2.padE.pairSpot", "s2.shootE", "s2.shootW", "s2.throwE",
            "s2.throwW", "boss.safe.1", "boss.safe.2", "boss.safe.3", "boss.weak.sheet", "boss.weak.impact", "boss.weak.punch", "boss.weak.projector",
            "s3.door", "s3.lobby", "exit", "story.metv", "story.taunt.1", "tutorial.ch11_lonely", "tutorial.ch11_eyes", "tutorial.ch11_closet",
            "tutorial.ch11_flare", "tutorial.ch11_unmask", "hint.1", "hint.2", "hint.3", "boss",
        };
        static readonly string[] Beats = { "ch11.intro", "ch11.p1", "ch11.tell", "ch11.fizzle", "ch11.spook", "ch11.rescue", "ch11.lightW", "ch11.lightN", "ch11.lightE", "ch11.p2", "ch11.boo", "ch11.unmasked", "ch11.outro" };

        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(1.0f);
            foreach (var m in Marks) ctx.Check(LevelMark.Exists(m), "mark " + m);
            foreach (var b in Beats) ctx.Check(StoryScripts.Exists(b), "story beat defined: " + b);
            { string cps = ""; foreach (var m in LevelMark.FindAll("checkpoint")) cps += m.name + "=" + m.Id + " "; ctx.Log("checkpoint marks: " + cps); }
            ctx.Check(LevelMark.Exists("checkpoint.1") && LevelMark.Exists("checkpoint.2") && !LevelMark.Exists("checkpoint.3"), "exactly 2 checkpoints");
            ctx.Check(Object.FindObjectsByType<KillZone>(FindObjectsSortMode.None).Length == 0, "no KillZone anywhere");
            int lights = 0; string names = ""; foreach (var li in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (li.type != LightType.Directional && li.enabled && li.GetComponentInParent<Canvas>() == null && (li.transform.parent == null || !li.transform.parent.name.StartsWith("PortraitSlot"))) { lights++; names += li.name + "<" + (li.transform.parent != null ? li.transform.parent.name : "") + "> "; }
            ctx.Log("lights: " + names);
            int rb = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length, rends = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            ctx.Log("non-sun lights " + lights + ", rigidbodies " + rb + ", renderers " + rends);
            ctx.Check(rb <= 25 && rends <= 1600, "budgets: rigidbodies <= 25, renderers <= 1600");
            ctx.Check(lights <= 6, "non-sun lights <= 6 (" + lights + ")");
            var boss = LevelMark.Find<L12BoogeymanBoss>("boss");
            ctx.Check(boss != null, "boss present");
            if (boss == null) yield break;
            ctx.Check(boss.ValidateSpecs(out var err), "boss specs valid " + err);
            ctx.Check(boss.Arena != null && boss.Arena.ValidateNoKillZones(out var kz), "arena: no kill zones");
            // lit islands at the start
            ctx.Check(LightRegistry.IsLit(new Vector3(0f, 3.5f, 2.5f)) && LightRegistry.IsLit(new Vector3(-11.5f, 1f, 3.5f)) && LightRegistry.IsLit(new Vector3(11.5f, 1f, 3.5f)), "balcony + break areas lit");
            ctx.Check(!LightRegistry.IsLit(new Vector3(0f, 1f, 20f)), "atrium centre dark at the start");

            var cm = CharacterManager.Instance;
            var cam = new GameObject("L12ShotCam").AddComponent<Camera>();
            cam.fieldOfView = 58f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 300f;
            var main = Camera.main; if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }

            // 1 from the crypt stairs onto the balcony
            Aim(cam, new Vector3(1.2f, 4.6f, -8.5f), new Vector3(0f, 2.0f, 12f));
            yield return ctx.Wait(0.3f);
            ctx.Shot("l12_s0_balcony", cam);

            // 2 intro: step onto the balcony -> the eyes open across the dark atrium
            Tp(cm.Team[0], new Vector3(-1.5f, L12Builder.DeckY, 3f)); Tp(cm.Team[1], new Vector3(0f, L12Builder.DeckY, 3f)); Tp(cm.Team[2], new Vector3(1.5f, L12Builder.DeckY, 3f));
            yield return ctx.Wait(1.3f);
            ctx.Check(boss.State != BossState.Dormant, "boss begins when the team steps onto the balcony");
            Aim(cam, new Vector3(0f, 5.2f, 1.5f), new Vector3(0f, 2.8f, 29f));
            ctx.Shot("l12_intro_eyes", cam);

            // 3 deliberate spook (boss extra d): Brainiac alone at the dark spot, the others in BA-E
            ShifterCharacter brain = null, brute = null, spr = null;
            foreach (var c in cm.Team) { if (c.Archetype == Archetype.Brainiac) brain = c; else if (c.Archetype == Archetype.Brute) brute = c; else spr = c; }
            Tp(brute, new Vector3(10.5f, 0f, 3.5f)); Tp(spr, new Vector3(12.5f, 0f, 3.5f)); Tp(brain, LevelMark.Position("s1.darkSpot"));
            yield return WaitFor(ctx, () => boss.EyesOpen, 20f);
            ctx.Check(boss.EyesOpen && boss.Target == brain, "TELL targets the lonely Brainiac in the dark");
            var e = boss.EyesPoint;
            Aim(cam, brain.Feet + (brain.Feet - e).normalized * 3.5f + Vector3.up * 2.4f, e + Vector3.down * 0.6f);
            yield return ctx.Wait(0.8f);
            ctx.Shot("l12_tell_eyes", cam);
            yield return WaitFor(ctx, () => brain.State == CharacterState.Captured, 6f);
            ctx.Check(brain.State == CharacterState.Captured && boss.SpooksLanded == 1, "spook lands -> Brainiac in the broom closet");
            yield return ctx.Wait(0.6f);
            Aim(cam, new Vector3(2.6f, 2.6f, 13.5f), new Vector3(0f, 1.4f, 7.2f));
            ctx.Shot("l12_closet", cam);
            // rescue: the Brute presses the release lever on the balcony
            Tp(brute, LevelMark.Position("s0.leverStand"));
            yield return ctx.Wait(0.4f);
            var lever = LevelMark.Find<PushButton>("s1.closetLever");
            if (lever != null) lever.Interact(brute);
            yield return WaitFor(ctx, () => brain.State == CharacterState.Alive, 4f);
            ctx.Check(brain.State == CharacterState.Alive, "release lever frees the closet");

            // 4 the three lights (forced: picture scenario) -> transition reveal
            var lights3 = Object.FindAnyObjectByType<L12AtriumLights>();
            Tp(brute, new Vector3(-11f, 0f, 3.5f)); Tp(spr, new Vector3(-12f, 0f, 3.5f)); Tp(brain, new Vector3(-11.5f, 0f, 4.5f));
            lights3.FloodW.Set(true); yield return ctx.Wait(0.8f);
            lights3.FloodN.Set(true); yield return ctx.Wait(0.8f);
            Aim(cam, new Vector3(13f, 5f, 2f), new Vector3(-4f, 0.5f, 20f));
            ctx.Shot("l12_two_lights", cam);
            lights3.FloodE.Set(true);
            yield return WaitFor(ctx, () => boss.State == BossState.PhaseTransition, 4f);
            yield return ctx.Wait(1.6f);
            Aim(cam, new Vector3(4f, 3.4f, 11f), new Vector3(0f, 1.6f, 19f));
            ctx.Shot("l12_reveal", cam);
            yield return WaitFor(ctx, () => boss.Phase == 1 && boss.State == BossState.Fighting, 6f);
            ctx.Check(boss.Phase == 1, "three lights -> phase 2");

            // 5 P2: BOO tell on a lonely intern at PAD E, then unmask + dizzy
            Tp(spr, new Vector3(8f, 0f, 20.5f));
            yield return WaitFor(ctx, () => boss.BooTelling && boss.CurrentPad == 1, 10f);
            yield return ctx.Wait(0.7f);
            Aim(cam, new Vector3(12.5f, 4.2f, 12.5f), new Vector3(6f, 0.6f, 19f));
            ctx.Shot("l12_boo_tell", cam);
            yield return WaitFor(ctx, () => boss.IsCharging, 3f);
            ctx.Check(boss.Boos >= 1, "BOO lands on the lonely Sprinter");
            Aim(cam, new Vector3(9.6f, 2.3f, 15.2f), new Vector3(6f, 1.2f, 19f));
            ctx.Shot("l12_sheet", cam);
            {   // MAJOR fix: a walking / sprinting (not Thrown) intern entering the impact volume while CHARGING does nothing
                var imp = LevelMark.Find<L12SheetImpact>("boss.weak.impact");
                var bcol = brute.GetComponentInChildren<Collider>();
                int u0 = boss.Unmasks;
                if (imp != null && bcol != null) imp.SendMessage("OnTriggerEnter", bcol, SendMessageOptions.DontRequireReceiver);
                ctx.Check(imp != null && bcol != null && boss.IsCharging && boss.Unmasks == u0 && !boss.IsDizzy, "a walking (not Thrown) Brute entering the CHARGING machine does NOT unmask it");
            }
            // shot QA (WeaponHolder.TraceShot) to the sheet on PAD E from s2.shootE, the pair spot and the balcony deck
            yield return Trace(ctx, boss, LevelMark.Position("s2.shootE"), "s2.shootE -> PAD E");
            yield return Trace(ctx, boss, LevelMark.Position("s2.padE.pairSpot"), "pair spot -> PAD E");
            yield return Trace(ctx, boss, LevelMark.Position("s1.stairE.bottom"), "E stair foot (below the glass-screened balcony) -> PAD E");
            ctx.Check(boss.IsCharging, "still CHARGING after the traces");
            boss.Unmask(L12UnmaskKind.Shot, brain);
            yield return ctx.Wait(0.5f);
            ctx.Check(boss.IsDizzy, "unmask -> DIZZY");
            Aim(cam, new Vector3(9.5f, 2.6f, 15f), new Vector3(6f, 1.0f, 19f));
            ctx.Shot("l12_unmasked", cam);

            // shot QA on PAD W (after DIZZY + re-sheet the machine drives W)
            yield return WaitFor(ctx, () => boss.CurrentPad == 0 && !boss.IsDizzy && boss.Phase == 1, 24f);
            ctx.Check(boss.CurrentPad == 0, "machine re-sheeted and parked on PAD W");
            yield return Trace(ctx, boss, LevelMark.Position("s2.shootW"), "s2.shootW -> PAD W");
            yield return Trace(ctx, boss, new Vector3(-9.5f, 0f, 21f), "W pair spot -> PAD W");
            yield return Trace(ctx, boss, LevelMark.Position("s1.stairW.bottom"), "W stair foot -> PAD W");

            // 6 Spooky MILD eyes
            ScareDirector.MildOverride = true;
            if (boss.Eyes != null) { boss.Eyes.OpenAt(new Vector3(-8f, 2.8f, 24f), new Vector3(-8f, 2.8f, 18f), 0.5f); }
            yield return ctx.Wait(0.8f);
            Aim(cam, new Vector3(-8f, 2.6f, 18.5f), new Vector3(-8f, 2.8f, 24f));
            ctx.Shot("l12_eyes_mild", cam);
            ScareDirector.MildOverride = null;

            // 7 overview
            cam.fieldOfView = 62f;
            Aim(cam, new Vector3(-12f, 11f, 3f), new Vector3(3f, 0f, 22f));
            ctx.Shot("l12_overview", cam);
            Object.Destroy(cam.gameObject);
        }

        static IEnumerator WaitFor(DevPlayContext ctx, System.Func<bool> cond, float timeout)
        {
            float t = 0f;
            while (t < timeout && !cond()) { t += Time.deltaTime; yield return null; }
        }

        static IEnumerator Trace(DevPlayContext ctx, L12BoogeymanBoss boss, Vector3 from, string label)
        {
            ShifterCharacter who = null;
            foreach (var c in Object.FindObjectsByType<ShifterCharacter>(FindObjectsInactive.Exclude)) if (c != null && c.Weapon != null && c.IsAlive && c.Condition == CharacterCondition.Normal) { who = c; if (c.Weapon.HasWeapon) break; }
            var sheet = LevelMark.Find("boss.weak.sheet");
            if (who == null || sheet == null) { ctx.Check(false, "trace " + label + ": no shooter / sheet mark"); yield break; }
            int k = 0;
            foreach (var o in Object.FindObjectsByType<ShifterCharacter>(FindObjectsInactive.Exclude)) if (o != null && o != who) Tp(o, new Vector3(-12.5f + 1.3f * k++, 0f, 3.5f));   // bystanders out of the line (break area W)
            Tp(who, from);
            Vector3 d = sheet.transform.position - from; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) who.transform.rotation = Quaternion.LookRotation(d.normalized);
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            bool hit = who.Weapon.TraceShot(sheet.transform.position, out var h);
            bool ok = hit && h.collider != null && h.collider.GetComponent<L12BoogeySheet>() != null;
            ctx.Check(ok, "shot QA " + label + ": clear line to the sheet (hit " + (hit && h.collider != null ? h.collider.name : "nothing") + ")");
        }

        static void Aim(Camera cam, Vector3 from, Vector3 at) => cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));

        static void Tp(ShifterCharacter c, Vector3 p)
        {
            if (c == null) return;
            if (c.Motor != null) c.Motor.Teleport(p + Vector3.up * 0.05f, Quaternion.identity); else c.transform.position = p;
        }
    }
}
