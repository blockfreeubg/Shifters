// =====================================================================================
//  SHIFTERS - VisualsTests.cs   (VISUALS DevPlay scenarios)
//  Run: devplay.sh <proj> visuals_all -devSetup Shifters.VisualsTests.All -devSize 1920x1080
//  One batched scenario: line-up (9 chars), expression sheets (3 x 11), pose/animation sheet
//  (CharacterVisual + preview driver), bridge over a gap, aiming with every weapon, weapon
//  line-up, BONK-BOT views, portrait textures (+ state mirroring) saved as PNGs.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shifters
{
    public static class VisualsTests
    {
        static Camera Cam => Camera.main;

        static void Look(Vector3 pos, Vector3 target, float fov)
        {
            var c = Cam; if (c == null) return;
            c.transform.position = pos; c.transform.LookAt(target);
            c.fieldOfView = fov; c.nearClipPlane = 0.1f; c.farClipPlane = 400f;
        }

        static void Floor(string name, Vector3 topCenter, Vector3 size, MatKey mat = MatKey.Floor)
            => Kit.SolidBox(null, name, topCenter - new Vector3(0f, size.y * 0.5f, 0f), size, mat, 0.02f);

        static ShifterCharacter MakeChar(CharacterProfile p, int slot, Vector3 pos, float yaw)
        {
            var c = CharacterFactory.Create(p, slot, pos, Quaternion.Euler(0f, yaw, 0f));
            c.Body.isKinematic = true;
            c.ExternalControl = true;
            return c;
        }

        static CharacterProfile Pick(TeamData t, Archetype a) { foreach (var m in t.Members) if (m != null && m.Archetype == a) return m; return t.Members[0]; }

        static bool MaterialsOk(Renderer[] rs)
        {
            foreach (var r in rs)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name.Contains("InternalError") || m.name.StartsWith("Default-Material")) return false;
            }
            return true;
        }

        public static IEnumerator All(DevPlayContext ctx)
        {
            RenderSettings.ambientIntensity = 1.1f;
            yield return LineUp(ctx);
            yield return Expressions(ctx);
            yield return Poses(ctx);
            yield return MotorDriven(ctx);
            yield return WeaponsAndBots(ctx);
            yield return Portraits(ctx);
            ctx.Log("visuals scenario complete");
        }

        // ------------------------------------------------------------------ 1. line-up
        static IEnumerator LineUp(DevPlayContext ctx)
        {
            Floor("LineupFloor", Vector3.zero, new Vector3(40f, 1f, 20f));
            var root = new GameObject("LineUp").transform;
            int[] seeds = { 11, 42, 777 };
            var pups = new List<CharacterPuppet>();
            float x = -5.9f;
            foreach (int s in seeds)
            {
                var team = VisualsTestKit.Team(s);
                foreach (var m in team.Members) { pups.Add(CharacterPuppet.Create(m, root, new Vector3(x, 0f, 0f), Quaternion.identity)); x += 1.45f; }
                x += 0.25f;
            }
            yield return ctx.Wait(0.5f);
            int maxR = 0; bool heightsOk = true, matsOk = true;
            var stats = new Dictionary<Archetype, string>();
            foreach (var p in pups)
            {
                maxR = Mathf.Max(maxR, p.Rig.Renderers.Length);
                float top = p.Rig.HeadMesh.GetComponent<Renderer>().bounds.max.y - p.transform.position.y;
                if (Mathf.Abs(top - p.Profile.Body.Height) > 0.12f) { heightsOk = false; ctx.Log("height mismatch " + p.Profile.Name + " " + top + " vs " + p.Profile.Body.Height); }
                if (!MaterialsOk(p.Rig.Renderers)) matsOk = false;
                stats[p.Profile.Archetype] = p.Rig.Renderers.Length + " renderers / " + p.Rig.Triangles + " tris";
            }
            foreach (var kv in stats) ctx.Log("perf " + kv.Key + ": " + kv.Value);
            ctx.Check(maxR <= 35, "<= 35 renderers per character (max " + maxR + ")");
            ctx.Check(heightsOk, "head top matches Body.Height (+-0.12)");
            ctx.Check(matsOk, "no default/missing materials on characters");
            Look(new Vector3(0f, 1.35f, 10.5f), new Vector3(0f, 1.0f, 0f), 48f); ctx.Shot("lineup_front");
            Look(new Vector3(8.5f, 2.6f, 7.5f), new Vector3(-0.5f, 0.9f, 0f), 46f); ctx.Shot("lineup_34");
            Look(new Vector3(1.8f, 2.7f, 5.9f), new Vector3(0.3f, 1.05f, 0f), 60f); ctx.Shot("lineup_gameplay6m");
        }

        // ------------------------------------------------------------------ 2. expression sheets (choir risers)
        static readonly Expression[] AllExpr =
        {
            Expression.Neutral, Expression.Happy, Expression.Determined, Expression.Strain,
            Expression.Scared, Expression.Surprised, Expression.Sad, Expression.Angry,
            Expression.KO, Expression.Dizzy, Expression.Smug
        };

        static IEnumerator Expressions(DevPlayContext ctx)
        {
            var team = VisualsTestKit.Team(42);
            Archetype[] arch = { Archetype.Brute, Archetype.Sprinter, Archetype.Brainiac };
            var sheets = new List<(Archetype a, float x, List<CharacterPuppet> pups, Vector3 camPos, Vector3 camTarget)>();
            const float vfov = 30f;
            for (int ai = 0; ai < 3; ai++)
            {
                var prof = Pick(team, arch[ai]);
                var d = CharacterModelBuilder.Measure(prof);
                float bx = 100f + ai * 30f, sp = arch[ai] == Archetype.Brute ? 1.45f : arch[ai] == Archetype.Sprinter ? 0.8f : 0.95f;
                float h = prof.Body.Height, step = h * 0.45f;
                Floor("ExprFloor" + ai, new Vector3(bx, 0f, 0f), new Vector3(16f, 1f, 16f));
                // tight framing on the three rows of heads
                float headY = h - d.HeadHalf.y * 1.1f;
                float frameH = 2f * step + d.HeadHalf.y * 3.4f, frameW = 4f * sp + 0.4f;
                float t = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
                float dist = Mathf.Max(frameH * 0.5f / t, frameW * 0.5f / (t * 16f / 9f)) + 1.2f;
                var target = new Vector3(bx, headY + step, -1.2f);
                var camPos = target + new Vector3(0f, 0.15f, dist);
                var pups = new List<CharacterPuppet>();
                var root = new GameObject("Expr_" + arch[ai]).transform;
                for (int i = 0; i < AllExpr.Length; i++)
                {
                    int row = i / 4, col = i % 4, n = row < 2 ? 4 : 3;
                    float y = row * step, z = -row * 1.2f;
                    if (row > 0) Floor("Riser" + ai + row + col, new Vector3(bx + (col - (n - 1) * 0.5f) * sp, y, z), new Vector3(sp * 0.9f, y + 0.01f, 1.1f), MatKey.FloorAlt);
                    var p = CharacterPuppet.Create(prof, root, new Vector3(bx + (col - (n - 1) * 0.5f) * sp, y, z), Quaternion.identity);
                    p.SetExpression(AllExpr[i]);
                    p.LookTarget = camPos;
                    pups.Add(p);
                }
                sheets.Add((arch[ai], bx, pups, camPos, target));
            }
            yield return ctx.Wait(0.6f);
            foreach (var sh in sheets)
            {
                bool ok = true;
                for (int i = 0; i < sh.pups.Count; i++) if (sh.pups[i].CurrentExpression != AllExpr[i]) ok = false;
                ctx.Check(ok, sh.a + ": every expression applied");
                ctx.Check(sh.pups[8].Face.EyeMode == 1 && sh.pups[9].Face.EyeMode == 2, sh.a + ": KO shows X eyes, Dizzy shows spirals");
                ctx.Check(sh.pups[1].Face.CurrentMouth == MouthShape.Grin && sh.pups[5].Face.CurrentMouth == MouthShape.O, sh.a + ": mouths swap per expression");
                bool sane = true;
                foreach (var p in sh.pups) if (p.Face.Mouth.lossyScale.x > 2f || p.Face.Mouth.lossyScale.x < 0.2f) sane = false;
                ctx.Check(sane, sh.a + ": mouth springs stay bounded");
                Look(sh.camPos, sh.camTarget, vfov);
                ctx.Shot("expr_" + sh.a);
            }
        }

        // ------------------------------------------------------------------ 3. poses (CharacterVisual + preview driver)
        /// <summary>Front 3/4 shot of a group of characters standing in a row at z (x from x0 to x1).</summary>
        static void GroupShot(DevPlayContext ctx, string name, float x0, float x1, float z, float yLook = 0.95f)
        {
            float xc = (x0 + x1) * 0.5f, w = (x1 - x0) + 1.8f;
            float dist = Mathf.Max(5.5f, w * 0.5f / (Mathf.Tan(20f * Mathf.Deg2Rad) * 16f / 9f) + 1f);
            Look(new Vector3(xc + 1.4f, 2.1f, z + dist), new Vector3(xc, yLook, z), 40f);
            ctx.Shot(name);
        }

        static IEnumerator Poses(DevPlayContext ctx)
        {
            var t1 = VisualsTestKit.Team(42); var t2 = VisualsTestKit.Team(11); var t3 = VisualsTestKit.Team(777);
            var brute = Pick(t1, Archetype.Brute); var sprint = Pick(t1, Archetype.Sprinter); var brain = Pick(t1, Archetype.Brainiac);
            var sprint2 = Pick(t2, Archetype.Sprinter); var brute2 = Pick(t3, Archetype.Brute); var brain2 = Pick(t2, Archetype.Brainiac);
            const float X0 = 200f, S = 2.4f, zA = 0f, zB = -14f, zC = -28f;
            Floor("PoseFloor", new Vector3(X0 + 13f, 0f, -14f), new Vector3(40f, 1f, 44f));
            int slot = 0;
            // row A: locomotion
            var loco = new (CharacterProfile p, VisualsPreviewDriver.Mode m, float y)[]
            {
                (brute, VisualsPreviewDriver.Mode.Idle, 0f), (brute2, VisualsPreviewDriver.Mode.Walk, 0f), (sprint, VisualsPreviewDriver.Mode.Run, 0f),
                (sprint2, VisualsPreviewDriver.Mode.Sprint, 0f), (brain, VisualsPreviewDriver.Mode.JumpRise, 0.6f), (brute2, VisualsPreviewDriver.Mode.Fall, 0.9f),
                (brain2, VisualsPreviewDriver.Mode.Idle, 0f), (sprint2, VisualsPreviewDriver.Mode.Tumble, 1.0f)
            };
            CharacterVisual landing = null;
            for (int i = 0; i < loco.Length; i++)
            {
                var c = MakeChar(loco[i].p, slot++, new Vector3(X0 + i * S, loco[i].y, zA), 25f);
                VisualsPreviewDriver.Attach(c.Visual, loco[i].m);
                if (i == 6) landing = c.Visual;
            }
            // row B: actions
            int col = 0;
            ShifterCharacter RowB(CharacterProfile p, float yaw = 15f) => MakeChar(p, slot++, new Vector3(X0 + (col++) * S, 0f, zB), yaw);
            var carrier = RowB(brute); carrier.Visual.SetPose(PoseOverride.Carrying);
            var carried = MakeChar(sprint2, slot++, carrier.CarryPoint.position, 0f);
            foreach (var colz in carried.GetComponentsInChildren<Collider>()) colz.enabled = false;
            carried.transform.SetParent(carrier.CarryPoint, false); carried.transform.localPosition = Vector3.zero; carried.transform.localRotation = Quaternion.identity;
            carried.SetCondition(CharacterCondition.Carried);
            var hacker = RowB(brain); hacker.Visual.SetPose(PoseOverride.Hacking);
            var ko = RowB(sprint); ko.KnockOut("test");
            var celeb = RowB(brain2);
            var taunt = RowB(brute2, 0f);
            var seated = RowB(sprint2); seated.Visual.SetPose(PoseOverride.Seated);
            var captured = RowB(brain); captured.Capture("cell_test", "test");
            var flop = RowB(brute); flop.SetCondition(CharacterCondition.Flopped);
            var glow = RowB(sprint); glow.Visual.SetHighlight(UIColors.Speed, 0.6f);
            var grey = RowB(brain2); grey.Visual.SetDesaturated(true);
            var waver = RowB(brute2);
            var dancer = RowB(sprint);

            // row C: aiming with every weapon
            var weapons = new[]
            {
                VisualsTestKit.Weapon(WeaponType.Pistol, 0), VisualsTestKit.Weapon(WeaponType.Shotgun, 0), VisualsTestKit.Weapon(WeaponType.LaserGun, 0),
                VisualsTestKit.Weapon(WeaponType.Slingshot, 0), VisualsTestKit.Weapon(WeaponType.Experimental, ExperimentalVariant.RubberChickenLauncher),
                VisualsTestKit.Weapon(WeaponType.Experimental, ExperimentalVariant.GravityPlunger)
            };
            var aimers = new List<ShifterCharacter>();
            CharacterProfile[] aimProfiles = { brute, sprint, brain, sprint2, brute2, brain2 };
            for (int i = 0; i < weapons.Length; i++)
            {
                var c = MakeChar(aimProfiles[i], slot++, new Vector3(X0 + i * S, 0f, zC), -35f);
                c.Visual.SetPose(PoseOverride.Aiming);
                WeaponModelBuilder.Build(weapons[i], c.Visual.WeaponSocket);
                aimers.Add(c);
            }

            // bridge over a 5.5 m gap
            float bxp = X0 + 40f;
            Floor("BridgeNear", new Vector3(bxp, 0f, -3f), new Vector3(4f, 1f, 6f), MatKey.FloorAlt);
            Floor("BridgeFar", new Vector3(bxp, 0f, 8.5f), new Vector3(4f, 1f, 6f), MatKey.FloorAlt);
            Floor("BridgePit", new Vector3(bxp, -3f, 2.75f), new Vector3(4f, 1f, 5.5f), MatKey.HazardBlack);
            var bridge = MakeChar(sprint2, slot++, new Vector3(bxp, 0f, 0f), 0f);
            bridge.Visual.SetPose(PoseOverride.Bridge);
            bridge.Visual.SetBridgeStretch(Tuning.BridgeLength, 0.65f);
            var walkerRoot = new GameObject("BridgeWalker").transform;
            var walker = CharacterPuppet.Create(brain, walkerRoot, new Vector3(bxp, -0.12f, 3.0f), Quaternion.Euler(0f, 0f, 0f));
            walker.SetMotion(2f);

            yield return ctx.Wait(0.4f);
            celeb.Visual.Play(VisualAction.Celebrate);
            taunt.Visual.Play(VisualAction.Taunt);
            waver.Visual.Play(VisualAction.Wave);
            dancer.Visual.Play(VisualAction.Dance);
            yield return ctx.Wait(1.04f);
            landing.NotifyLanded(18f);
            yield return null; yield return null;

            // ---- checks
            ctx.Check(ko.Visual.CurrentExpression == Expression.KO && ko.Visual.Face.EyeMode == 1 && ko.Visual.EffectivePose == PoseOverride.KO, "KO state -> KO pose + X eyes");
            ctx.Check(captured.Visual.EffectivePose == PoseOverride.Captured && captured.Visual.CurrentExpression == Expression.Scared, "Captured state -> struggling + scared");
            ctx.Check(flop.Visual.EffectivePose == PoseOverride.Flop && flop.Visual.CurrentExpression == Expression.Dizzy, "Flopped condition -> flop + dizzy");
            ctx.Check(carried.Visual.EffectivePose == PoseOverride.Carried, "Carried condition -> carried pose");
            var carriedHead = carrier.CarryPoint.InverseTransformPoint(carried.Visual.Head.position);
            ctx.Check(Mathf.Abs(carriedHead.x) > 0.3f && Mathf.Abs(carriedHead.y) < 0.6f, "carried lies across the carrier's head along local X (head local " + carriedHead + ")");
            ctx.Check(landing.Animator.Squash < -0.05f, "landing squash (" + landing.Animator.Squash.ToString("0.00") + ")");
            ctx.Check(taunt.Visual.Animator.IsGesturing && celeb.Visual.Animator.IsGesturing && dancer.Visual.Animator.IsGesturing, "gestures playing");
            var gm = glow.Visual.Rig.Mats.Primary;
            ctx.Check(gm.IsKeywordEnabled("_EMISSION") || (gm.HasProperty("_RevealStrength") && gm.GetFloat("_RevealStrength") > 0.3f), "SetHighlight drives emission/_RevealStrength");
            Color.RGBToHSV(grey.Visual.Rig.Mats.Primary.GetColor("_BaseColor"), out _, out float sat, out _);
            ctx.Check(sat < 0.2f, "SetDesaturated greys materials (sat " + sat.ToString("0.00") + ")");
            bool aimOk = true;
            foreach (var a in aimers)
            {
                var wf = a.Visual.WeaponSocket.forward; var cf = a.transform.forward;
                if (Vector3.Dot(wf, cf) < 0.85f) { aimOk = false; ctx.Log("aim dir " + wf + " vs " + cf); }
            }
            ctx.Check(aimOk, "Aiming: WeaponSocket +Z points along the character's forward");
            var br = bridge.transform;
            var hl = br.InverseTransformPoint(bridge.Visual.HandL.position); var hr = br.InverseTransformPoint(bridge.Visual.HandR.position);
            var fl = br.InverseTransformPoint(bridge.Visual.Rig.FootL.position);
            var torsoTop = bridge.Visual.Rig.TorsoMesh.GetComponent<Renderer>().bounds.max.y - br.position.y;
            ctx.Log("bridge hands z " + hl.z.ToString("0.00") + "/" + hr.z.ToString("0.00") + " foot z " + fl.z.ToString("0.00") + " torso top y " + torsoTop.ToString("0.00"));
            ctx.Check(Mathf.Abs(hl.z - Tuning.BridgeLength) < 0.6f && Mathf.Abs(hr.z - Tuning.BridgeLength) < 0.6f, "bridge: hands at z ~ length");
            ctx.Check(Mathf.Abs(fl.z) < 0.45f, "bridge: feet at z ~ 0");
            ctx.Check(torsoTop > -0.35f && torsoTop < 0.25f, "bridge: top surface near local y=0");

            GroupShot(ctx, "poses_loco_a", X0, X0 + 3f * S, zA);
            GroupShot(ctx, "poses_loco_b", X0 + 4f * S, X0 + 7f * S, zA, 1.2f);
            GroupShot(ctx, "poses_actions_a", X0, X0 + 3f * S, zB, 1.2f);
            GroupShot(ctx, "poses_actions_b", X0 + 4f * S, X0 + 7f * S, zB);
            GroupShot(ctx, "poses_actions_c", X0 + 8f * S, X0 + 12f * S, zB);
            GroupShot(ctx, "poses_aiming_a", X0, X0 + 2f * S, zC, 1.2f);
            GroupShot(ctx, "poses_aiming_b", X0 + 3f * S, X0 + 5f * S, zC, 1.2f);
            Look(new Vector3(bxp + 7.5f, 1.8f, 3.2f), new Vector3(bxp, 0f, 3.2f), 55f); ctx.Shot("bridge_side");
            Look(new Vector3(bxp + 4.5f, 3.5f, -3.5f), new Vector3(bxp, 0f, 3.0f), 55f); ctx.Shot("bridge_34");
        }

        // ------------------------------------------------------------------ 3b. real CharacterMotor drives the visual
        static IEnumerator MotorDriven(DevPlayContext ctx)
        {
            const float X0 = 400f;
            Floor("MotorFloor", new Vector3(X0, 0f, 0f), new Vector3(16f, 1f, 30f));
            var team = VisualsTestKit.Team(42);
            var c = CharacterFactory.Create(Pick(team, Archetype.Sprinter), 0, new Vector3(X0, 0.05f, -10f), Quaternion.identity);
            c.ExternalControl = true;
            yield return ctx.Wait(0.3f);
            c.Motor.SetCommand(new CharacterMotor.Command { Move = Vector3.forward, Sprint = true, Face = Vector3.forward });
            float minLeg = 999f, maxLeg = -999f;
            for (int i = 0; i < 50; i++)
            {
                yield return null;
                float a = Mathf.DeltaAngle(0f, c.Visual.Rig.LegL.localEulerAngles.x);
                minLeg = Mathf.Min(minLeg, a); maxLeg = Mathf.Max(maxLeg, a);
            }
            ctx.Log("motor run: speed " + c.Motor.PlanarVelocity.magnitude.ToString("0.0") + " m/s, leg swing " + minLeg.ToString("0") + ".." + maxLeg.ToString("0") + " deg");
            ctx.Check(maxLeg - minLeg > 40f, "real motor speed drives the run cycle (leg swing " + (maxLeg - minLeg).ToString("0") + " deg)");
            Look(c.transform.position + new Vector3(4.5f, 1.4f, 1.5f), c.transform.position + new Vector3(0f, 0.9f, 0.6f), 45f);
            ctx.Shot("motor_run");
            c.Motor.SetCommand(new CharacterMotor.Command { Move = Vector3.forward, Sprint = true, Jump = true, JumpHeld = true, Face = Vector3.forward });
            float maxStretch = 0f;
            for (int i = 0; i < 12; i++) { yield return null; maxStretch = Mathf.Max(maxStretch, c.Visual.Animator.Squash); }
            ctx.Check(maxStretch > 0.03f, "jump event -> stretch (" + maxStretch.ToString("0.00") + ")");
            yield return ctx.Wait(0.15f);
            Look(c.transform.position + new Vector3(4.5f, 0.6f, 1.5f), c.transform.position + new Vector3(0f, 0.9f, 0.6f), 45f);
            ctx.Shot("motor_jump");
            c.Motor.SetCommand(default);
        }

        // ------------------------------------------------------------------ 4. weapons + bots
        static IEnumerator WeaponsAndBots(DevPlayContext ctx)
        {
            const float X0 = 300f;
            Floor("WpnFloor", new Vector3(X0 + 6f, 0f, 0f), new Vector3(30f, 1f, 14f));
            var weapons = new[]
            {
                VisualsTestKit.Weapon(WeaponType.Pistol, 0), VisualsTestKit.Weapon(WeaponType.Shotgun, 0), VisualsTestKit.Weapon(WeaponType.LaserGun, 0),
                VisualsTestKit.Weapon(WeaponType.Slingshot, 0), VisualsTestKit.Weapon(WeaponType.Experimental, ExperimentalVariant.RubberChickenLauncher),
                VisualsTestKit.Weapon(WeaponType.Experimental, ExperimentalVariant.GravityPlunger)
            };
            var holders = new List<Transform>();
            bool muzzOk = true, sizeOk = true;
            for (int i = 0; i < weapons.Length; i++)
            {
                var h = new GameObject("WpnHolder" + i).transform;
                h.position = new Vector3(X0 + (i % 3) * 1.0f, 1.15f - (i / 3) * 0.55f, 0f);
                h.rotation = Quaternion.Euler(0f, 90f, 0f);
                Kit.BoxVisual(null, "Pedestal" + i, new Vector3(h.position.x, h.position.y - 0.2f, 0.12f), new Vector3(0.5f, 0.06f, 0.3f), MatKey.MetalDark);
                var w = WeaponModelBuilder.Build(weapons[i], h);
                var mz = w.transform.Find("Muzzle");
                if (mz == null || mz.localPosition.z < 0.05f) { muzzOk = false; ctx.Log("muzzle " + weapons[i].Type + " " + (mz != null ? mz.localPosition.ToString() : "missing")); }
                var b = new Bounds(w.transform.position, Vector3.zero);
                foreach (var r in w.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                ctx.Log("weapon " + WeaponStyle.Label(WeaponStyle.KindOf(weapons[i])) + " size " + b.size.ToString("0.00") + " muzzle z " + (mz != null ? mz.localPosition.z.ToString("0.00") : "-"));
                if (big < 0.3f || big > 0.85f) sizeOk = false;
                if (!MaterialsOk(w.GetComponentsInChildren<Renderer>())) muzzOk = false;
                holders.Add(h);
            }
            ctx.Check(muzzOk, "every weapon has a Muzzle at the barrel tip (+Z) and valid materials");
            ctx.Check(sizeOk, "weapon sizes within 0.3-0.85 m");

            // bots: front, side, 3/4
            Color[] acc = { UIColors.Accent, UIColors.Info, UIColors.Success };
            float[] yaw = { 0f, 90f, 35f };
            bool namesOk = true, botH = true;
            string[] names = { "Body", "Eye", "Siren", "ArmL", "ArmR", "Wheel", "Antenna" };
            for (int i = 0; i < 3; i++)
            {
                var h = new GameObject("BotHolder" + i).transform;
                h.position = new Vector3(X0 + 6f + i * 2.0f, 0f, 0f); h.rotation = Quaternion.Euler(0f, yaw[i], 0f);
                var bot = EnemyModelBuilder.Build(h, acc[i]);
                foreach (var n in names) if (bot.transform.Find(n) == null) { namesOk = false; ctx.Log("bot missing " + n); }
                var b = new Bounds(bot.transform.position, Vector3.zero);
                foreach (var r in bot.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                if (b.size.y < 1.45f || b.size.y > 1.95f) { botH = false; ctx.Log("bot height " + b.size.y); }
                if (i == 2) EnemyModelBuilder.SetGlow(bot.transform.Find("Eye").GetComponent<Renderer>(), UIColors.Danger, 2.5f);
            }
            ctx.Check(namesOk, "bot has direct children Body/Eye/Siren/ArmL/ArmR/Wheel/Antenna");
            ctx.Check(botH, "bot ~1.6 m tall");
            yield return ctx.Wait(0.2f);
            Look(new Vector3(X0 + 1f, 1.0f, -2.6f), new Vector3(X0 + 1f, 0.85f, 0f), 42f); ctx.Shot("weapons_side");
            foreach (var h in holders) h.rotation = Quaternion.Euler(-10f, 140f, 0f);
            yield return null;
            Look(new Vector3(X0 + 1f, 1.3f, -2.4f), new Vector3(X0 + 1f, 0.85f, 0f), 42f); ctx.Shot("weapons_34");
            Look(new Vector3(X0 + 8f, 1.4f, 5.2f), new Vector3(X0 + 8f, 0.85f, 0f), 48f); ctx.Shot("bots");
        }

        // ------------------------------------------------------------------ 5. portraits
        static IEnumerator Portraits(DevPlayContext ctx)
        {
            var team = VisualsTestKit.Team(42);
            var studio = PortraitStudio.Ensure();
            studio.Build(team);
            yield return null; yield return null;
            var dir = ctx.Config.shotsDir;
            var texs = new Texture[3];
            bool ok = true;
            for (int i = 0; i < 3; i++)
            {
                texs[i] = studio.GetPortrait(i);
                ok &= texs[i] != null && texs[i].width == PortraitStudio.TextureSize;
                float sd = texs[i] != null ? VisualsTestKit.SaveRT(texs[i], Path.Combine(dir, "portrait_" + i + ".png")) : 0f;
                ctx.Log("portrait " + i + " luminance stddev " + sd.ToString("0.000"));
                ok &= sd > 0.03f;
            }
            ctx.Check(ok, "3 portrait RenderTextures (256x256) with content");
            VisualsTestKit.SaveStrip(texs, Path.Combine(dir, "portraits_strip.png"));

            // state mirroring through GameEvents.CharacterStateChanged
            var chars = new ShifterCharacter[3];
            for (int i = 0; i < 3; i++) chars[i] = MakeChar(team.Members[i], i, new Vector3(500f + i * 2f, 0f, 0f), 0f);
            chars[1].KnockOut("test"); chars[2].Lose("test");
            yield return null; yield return null; yield return null;
            ctx.Check(studio.GetPuppet(1).CurrentExpression == Expression.KO, "portrait mirrors Downed -> KO face");
            ctx.Check(studio.GetPuppet(2).Rig.Mats.Desaturated && studio.GetPuppet(2).CurrentExpression == Expression.Sad, "portrait mirrors Lost -> desaturated + sad");
            VisualsTestKit.SaveStrip(new[] { studio.GetPortrait(0), studio.GetPortrait(1), studio.GetPortrait(2) }, Path.Combine(dir, "portraits_states_strip.png"));
            studio.SetLive(true);
            yield return null;
            ctx.Check(studio.IsLive, "SetLive(true) enables live portrait cameras");
            studio.SetLive(false);
            var main = Cam;
            ctx.Check(main == null || main.transform.position.y > -900f, "main camera far from the studio");
        }
    }
}
