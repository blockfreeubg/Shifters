// SHIFTERS - DevPlay: boss / set-piece models + campaign weapon models (visuals_bosses).
//   devplay.sh <clone> vbosses -devSetup Shifters.VisualsBossesTests.RunAll -devSize 1280x720
//   (Models / Weapons / Mechanics can be run alone.)
// Checks: every factory builds, all named parts exist, no rigidbodies, renderer caps, sizes vs. the specs, Downsizer
// kinematics (arm yaw / stamp / shutter), weapon Muzzle child in front of the grip; turntable screenshots of everything.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class VisualsBossesTests
    {
        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            yield return Models(ctx);
            yield return Mechanics(ctx);
            yield return Weapons(ctx);
        }

        static Camera Cam()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("TestCam") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            return cam;
        }

        static GameObject Stage(LevelTheme theme)
        {
            Kit.Theme = theme;
            LevelLighting.Apply(theme);
            PostFx.SetTheme(theme);
            var root = new GameObject("VB_Stage");
            EnvKit.Floor(root.transform, new Vector3(0f, 0f, 0f), new Vector3(40f, 1f, 40f));
            return root;
        }

        // tram: the cab is deliberately low (~1.9 m above the deck) so players on the deck see over it.
        // solopod: built without its grab collider here - in the level it carries Rigidbody + Grabbable, which makes its
        // labels prop labels (SceneAudit does not judge them); with the bare root box the audit calls them BURIED.
        delegate BossModelBase Factory(Transform parent);

        struct Spec { public string Name; public Factory Make; public float MinH, MaxH; public int MaxRenderers; public LevelTheme Theme; }

        static Spec[] Specs() => new[]
        {
            new Spec { Name = "downsizer", Make = p => BossModels.Downsizer(p, Vector3.zero), MinH = 6.8f, MaxH = 7.8f, MaxRenderers = 40, Theme = LevelTheme.Factory },
            new Spec { Name = "porkchop", Make = p => BossModels.Porkchop(p, Vector3.zero), MinH = 3.3f, MaxH = 4.0f, MaxRenderers = 40, Theme = LevelTheme.Bank },
            new Spec { Name = "tram", Make = p => BossModels.GetawayTram(p, new Vector3(0f, 0.6f, 0f)), MinH = 1.6f, MaxH = 3.2f, MaxRenderers = 30, Theme = LevelTheme.Bank },
            new Spec { Name = "boogeyman", Make = p => BossModels.Boogeyman(p, Vector3.zero), MinH = 5.8f, MaxH = 7.2f, MaxRenderers = 20, Theme = LevelTheme.Factory },
            new Spec { Name = "scareomatic", Make = p => BossModels.ScareOMatic(p, Vector3.zero), MinH = 1.5f, MaxH = 1.8f, MaxRenderers = 20, Theme = LevelTheme.Factory },
            new Spec { Name = "onemanband", Make = p => BossModels.OneManBand(p, Vector3.zero), MinH = 8.8f, MaxH = 10.2f, MaxRenderers = 90, Theme = LevelTheme.Tower },
            new Spec { Name = "solopod", Make = p => BossModels.SoloPod(p, Vector3.zero, null, true, false), MinH = 1.7f, MaxH = 2.4f, MaxRenderers = 60, Theme = LevelTheme.Tower },
        };

        public static IEnumerator Models(DevPlayContext ctx)
        {
            var cam = Cam();
            foreach (var spec in Specs())
            {
                var stage = Stage(spec.Theme);
                BossModelBase model = null;
                try { model = spec.Make(stage.transform); }
                catch (System.Exception e) { ctx.Check(false, spec.Name + ": factory threw " + e); }
                if (model == null) { Object.Destroy(stage); continue; }
                ctx.Check(model.HasAllParts(out var missing), spec.Name + ": all named parts present" + (missing.Length > 0 ? " (missing " + missing + ")" : ""));
                ctx.Check(model.GetComponentsInChildren<Rigidbody>(true).Length == 0, spec.Name + ": no rigidbodies (kinematic hierarchy)");
                int rc = model.RendererCount;
                ctx.Check(rc <= spec.MaxRenderers, spec.Name + ": renderers " + rc + " <= " + spec.MaxRenderers);
                var bb = model.RenderBounds();
                float h = bb.max.y - model.transform.position.y;
                ctx.Check(h >= spec.MinH && h <= spec.MaxH, spec.Name + ": height " + h.ToString("0.00") + " in [" + spec.MinH + ", " + spec.MaxH + "] (size " + bb.size.ToString("0.0") + ")");
                // no missing materials
                int nullMats = 0;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) if (r.sharedMaterial == null) nullMats++;
                ctx.Check(nullMats == 0, spec.Name + ": every renderer has a material (" + nullMats + " missing)");
                // sign audit on the model's texts
                var audit = SceneAudit.SignProblems(null, model.transform);
                foreach (var iss in audit.Issues) ctx.Log(spec.Name + " sign issue: " + iss.Kind + " '" + iss.Text + "' " + iss.What);
                ctx.Check(audit.Issues.Count == 0, spec.Name + ": sign audit clean (" + audit.Texts + " texts, " + audit.Issues.Count + " issues)");

                // turntable: 4 views around the model + one close-up of its front
                var c = bb.center;
                float dist = Mathf.Max(bb.size.x, bb.size.y, bb.size.z) * 1.35f + 2f;
                float[] yaws = { 20f, 110f, 200f, 290f };
                for (int i = 0; i < yaws.Length; i++)
                {
                    var dir = Quaternion.Euler(0f, yaws[i], 0f) * Vector3.forward;
                    RenderTestUtil.Place(cam, c + dir * dist + Vector3.up * bb.size.y * 0.25f, c, 50f);
                    yield return ctx.Wait(0.15f);
                    var shot = ctx.Shot(spec.Name + "_turn" + i);
                    if (i == 0) ctx.Check(RenderTestUtil.MagentaFraction(shot) < 0.001f, spec.Name + ": no magenta (shader errors)");
                }
                Object.Destroy(stage);
                yield return null;
            }
        }

        public static IEnumerator Mechanics(DevPlayContext ctx)
        {
            var cam = Cam();
            // ---- Downsizer kinematics: arm yaw 90 -> stamp over +X at 6 m; slam -> review platform top 0.5 m; shutter open
            var stage = Stage(LevelTheme.Factory);
            var d = BossModels.Downsizer(stage.transform, Vector3.zero);
            d.SetArmYaw(90f);
            d.SetStampBottom(0f);
            d.SetShutter(1f);
            d.SetHeartExposed(true);
            yield return null;
            var sp = d.Stamp.position;
            ctx.Check(Mathf.Abs(sp.x - 6f) < 0.05f && Mathf.Abs(sp.z) < 0.05f && Mathf.Abs(sp.y) < 0.02f, "downsizer: yaw 90 + slam puts the stamp foot at (6,0,0) (got " + sp.ToString("0.00") + ")");
            Physics.SyncTransforms();
            ctx.Check(d.StampCollider != null && Mathf.Abs(d.StampCollider.bounds.max.y - 0.5f) < 0.03f && d.StampCollider.bounds.size.x > 3.4f, "downsizer: slammed stamp collider = 3.5 m review platform, top 0.5 m");
            ctx.Check(Physics.Raycast(new Vector3(6f, 3f, 0f), Vector3.down, out var hit, 5f) && hit.collider == d.StampCollider && Mathf.Abs(hit.point.y - 0.5f) < 0.03f, "downsizer: ray down onto the review platform hits the stamp at 0.5 m");
            float shutterTop = float.MinValue;
            foreach (var r in d.Shutter.GetComponentsInChildren<Renderer>()) shutterTop = Mathf.Max(shutterTop, r.bounds.max.y);
            ctx.Check(shutterTop <= 2.02f, "downsizer: open shutter sinks below the plinth top (top " + shutterTop.ToString("0.00") + ")");
            var heartR = d.HeartLiquid.bounds;
            ctx.Check(heartR.center.y > 2.2f && heartR.center.y < 3.0f, "downsizer: heart at ~2.6 m");
            // shot-pivot visibility of the exposed heart from 8 m (Brainiac pivot 1.2 m): nothing but the heart's glass in between
            var from = new Vector3(0f, 1.2f, -8f);
            var to = d.Heart.position;
            bool blocked = Physics.Raycast(from, (to - from).normalized, (to - from).magnitude - 0.7f);
            ctx.Check(!blocked, "downsizer: exposed heart visible from 8 m at 1.2 m eye height");
            d.SetStampBottom(d.MaxStampBottom);
            ctx.Check(d.PistonRod.localScale.y > 0.05f && d.Stamp.localPosition.y < 0f, "downsizer: raised stamp below the piston housing");
            d.SetStampBottom(0f);
            RenderTestUtil.Place(cam, new Vector3(-9f, 5f, -11f), new Vector3(1.5f, 2.2f, 0f), 50f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("downsizer_slam_open");
            RenderTestUtil.Place(cam, new Vector3(0f, 2.2f, -7.5f), new Vector3(0f, 2.3f, 0f), 45f);
            d.SetArmYaw(0f); d.SetStampBottom(4f); d.SetShutter(0f); d.SetHeartExposed(false);
            yield return ctx.Wait(0.15f);
            ctx.Shot("downsizer_front_closed");
            d.SetShutter(1f); d.SetHeartExposed(true);
            yield return ctx.Wait(0.15f);
            ctx.Shot("downsizer_front_open");
            Object.Destroy(stage);
            yield return null;

            // ---- Porkchop: belly plug 1.2 m up on -X, red eyes, gait
            stage = Stage(LevelTheme.Bank);
            var pc = BossModels.Porkchop(stage.transform, Vector3.zero);
            ctx.Check(Mathf.Abs(pc.BellyPlug.position.y - 1.2f) < 0.05f && pc.BellyPlug.position.x < -1f, "porkchop: belly plug on the -X flank, 1.2 m up");
            ctx.Check(pc.EarPort.position.y > 3f, "porkchop: ear port on the ear (y " + pc.EarPort.position.y.ToString("0.00") + ")");
            pc.SetEyesRed(true); pc.SetScrape(0.5f);
            RenderTestUtil.Place(cam, new Vector3(-3.5f, 2.6f, 7.5f), new Vector3(0f, 2f, 0.5f), 50f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("porkchop_charge_tell");
            pc.SetEyesRed(false); pc.SetGait(1.2f, 25f);
            RenderTestUtil.Place(cam, new Vector3(-8f, 1.6f, 0.5f), new Vector3(0f, 1.6f, 0.3f), 45f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("porkchop_flank_plug");
            Object.Destroy(stage);
            yield return null;

            // ---- Tram: 6 rail colliders, the console corner (front-left) free of cab parts
            stage = Stage(LevelTheme.Bank);
            var deck = Kit.SolidBox(stage.transform, "Deck", new Vector3(0f, 0.45f, 0f), new Vector3(4f, 0.3f, 7f), MatKey.MetalLight);
            var tram = BossModels.GetawayTram(deck.transform, new Vector3(0f, 0.15f, 0f));
            ctx.Check(tram.RailColliders.Length == 6, "tram: 6 railing colliders (" + tram.RailColliders.Length + ")");
            float cabMinX = float.MaxValue;
            foreach (var r in tram.Cab.GetComponentsInChildren<Renderer>()) cabMinX = Mathf.Min(cabMinX, r.bounds.min.x);
            ctx.Check(cabMinX > -1.0f, "tram: front-left corner free for the CONSOLE (cab min x " + cabMinX.ToString("0.00") + ")");
            RenderTestUtil.Place(cam, new Vector3(-6f, 4.5f, -7f), new Vector3(0f, 0.8f, 0.5f), 50f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("tram_deck_view");
            Object.Destroy(stage);
            yield return null;

            // ---- Boogeyman night mood + tells; Scare-o-matic unmasked
            stage = Stage(LevelTheme.Night);
            var bg = BossModels.Boogeyman(stage.transform, Vector3.zero);
            var som = BossModels.ScareOMatic(stage.transform, new Vector3(3f, 0f, 2f), Quaternion.Euler(0f, -20f, 0f));
            float eyeH = bg.EyeL.GetComponentInChildren<Renderer>().bounds.size.y;
            ctx.Check(eyeH > 1.6f && eyeH < 2.2f, "boogeyman: door-sized eyes (" + eyeH.ToString("0.00") + " m tall)");
            bg.SetAngry(true); bg.SetPose(0.6f, 4f);
            RenderTestUtil.Place(cam, new Vector3(2f, 1.6f, 11f), new Vector3(0.5f, 3.4f, 0f), 55f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("boogeyman_night_spook");
            bg.SetEyesOpen(0.3f); bg.SetAngry(false); bg.SetPose(0f, 0f);
            som.SetSheet(false);
            RenderTestUtil.Place(cam, new Vector3(4.5f, 1.4f, 5f), new Vector3(3f, 0.6f, 2f), 45f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("scareomatic_unmasked");
            som.SetSheet(true);
            yield return ctx.Wait(0.1f);
            ctx.Shot("scareomatic_sheet");
            Object.Destroy(stage);
            yield return null;

            // ---- One-Man Band: Max in the cockpit, fist slam pose, shield; SOLO POD next to it
            stage = Stage(LevelTheme.Tower);
            var omb = BossModels.OneManBand(stage.transform, Vector3.zero);
            ctx.Check(omb.Max != null && omb.Max.GetComponentsInChildren<Renderer>().Length > 0, "one-man band: Max visible in the cockpit (" + (omb.Max != null ? omb.Max.GetComponentsInChildren<Renderer>().Length : 0) + " renderers)");
            var fistB = omb.Fist.GetComponentInChildren<Renderer>().bounds;
            ctx.Check(fistB.min.y > 0.8f && fistB.min.y < 2f, "one-man band: hanging fist bottom " + fistB.min.y.ToString("0.00") + " m");
            ctx.Check(omb.AntennaTip.position.y > 8.8f && omb.AntennaTip.position.y < 10f, "one-man band: antenna tip at " + omb.AntennaTip.position.y.ToString("0.00") + " m");
            var pod = BossModels.SoloPod(stage.transform, new Vector3(4f, 0f, 3f), Quaternion.Euler(0f, -30f, 0f));
            ctx.Check(pod.Collider != null && pod.Max != null, "solo pod: grab collider + Max seated");
            RenderTestUtil.Place(cam, new Vector3(1.5f, 8.2f, 4.2f), new Vector3(0f, 7.9f, 0f), 50f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("onemanband_cockpit_closeup");
            if (omb.Max != null && omb.DomeGlass != null)
            {
                var mb = new Bounds(omb.Max.transform.position, Vector3.zero);
                foreach (var r in omb.Max.GetComponentsInChildren<Renderer>()) mb.Encapsulate(r.bounds);
                var db = omb.DomeGlass.bounds;
                ctx.Check(mb.max.y < db.max.y - 0.02f && mb.size.y > 0.9f, "one-man band: Max (" + mb.size.y.ToString("0.00") + " m) fits under the dome (top " + mb.max.y.ToString("0.00") + " < " + db.max.y.ToString("0.00") + ")");
            }
            // what a player sees from the floor 12 m in front: Max must read above the console
            RenderTestUtil.Place(cam, new Vector3(0f, 1.8f, 12f), new Vector3(0f, 7.6f, 0f), 40f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("onemanband_cockpit_from_floor");
            omb.SetFistArm(-75f); omb.SetKazooArm(-60f, 20f); omb.SetStride(0.8f); omb.SetDomeShield(true);
            RenderTestUtil.Place(cam, new Vector3(9f, 4.5f, 14f), new Vector3(1.5f, 4.2f, 1f), 55f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("onemanband_attack_pose");
            ctx.Check(omb.ShieldHalfOn(-1) && omb.ShieldHalfOn(1), "one-man band: SetDomeShield(true) raises both half-shields");
            // P3 FIREWALL W down: only the east half stays (shatter confetti on the west side)
            omb.SetShieldHalf(-1, false);
            ctx.Check(!omb.ShieldHalfOn(-1) && omb.ShieldHalfOn(1) && omb.ShieldW.name == "ShieldW", "one-man band: SetShieldHalf(-1, false) drops only the west half");
            omb.SetFistArm(0f); omb.SetKazooArm(0f, 0f); omb.SetStride(0f, 0f);
            RenderTestUtil.Place(cam, new Vector3(-8f, 9.5f, 9f), new Vector3(0f, 8f, 0f), 45f);
            yield return ctx.Wait(0.1f);
            ctx.Shot("onemanband_shield_east_only");
            // P1 detached fist: the level moves Fist; the model draws the chain from the ArmR wrist
            omb.SetFistDetached(true);
            omb.Fist.position = new Vector3(4.5f, 0.3f, 6f);
            omb.Fist.rotation = Quaternion.identity;
            yield return null;
            yield return null;
            var wa = omb.WristPoint; var wb = omb.Fist.TransformPoint(0f, 0.7f, 0f);
            var l0 = omb.ChainLinks[0].position; var l5 = omb.ChainLinks[5].position;
            ctx.Check(omb.FistChain.gameObject.activeInHierarchy && Vector3.Distance(l0, Vector3.Lerp(wa, wb, 0.5f / 6f)) < 0.05f && Vector3.Distance(l5, Vector3.Lerp(wa, wb, 5.5f / 6f)) < 0.05f,
                "one-man band: detached-fist chain spans wrist -> fist (" + Vector3.Distance(wa, wb).ToString("0.0") + " m)");
            RenderTestUtil.Place(cam, new Vector3(9f, 4f, 13f), new Vector3(2.5f, 3f, 3f), 55f);
            yield return ctx.Wait(0.1f);
            ctx.Shot("onemanband_fist_chain");
            omb.SetFistDetached(false);
            ctx.Check(!omb.FistChain.gameObject.activeSelf, "one-man band: SetFistDetached(false) hides the chain");
            RenderTestUtil.Place(cam, new Vector3(6.5f, 1.7f, 6.2f), new Vector3(4f, 0.9f, 3f), 45f);
            pod.SetHover(0.3f, 6f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("solopod_closeup");
            Object.Destroy(stage);
            yield return null;
        }

        static readonly ExperimentalVariant[] Variants =
        {
            ExperimentalVariant.LeafBlower, ExperimentalVariant.TShirtCannon, ExperimentalVariant.SayCheese, ExperimentalVariant.PaintBlaster,
            ExperimentalVariant.BitShifter, ExperimentalVariant.SwapOMatic, ExperimentalVariant.PartyPopper, ExperimentalVariant.CapsLock
        };

        public static IEnumerator Weapons(DevPlayContext ctx)
        {
            var cam = Cam();
            var stage = Stage(LevelTheme.Factory);
            var grid = new GameObject("WeaponGrid").transform;
            grid.SetParent(stage.transform, false);
            var tints = new[] { new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 0.95f), new Color(0.95f, 0.75f, 0.2f), new Color(0.45f, 0.85f, 0.4f) };
            for (int i = 0; i < Variants.Length; i++)
            {
                var v = Variants[i];
                var holder = new GameObject("Slot_" + v).transform;
                holder.SetParent(grid, false);
                holder.localPosition = new Vector3((i % 4) * 2.4f - 3.6f, 1.2f + (i < 4 ? 1.6f : 0f), 0f);
                holder.localRotation = Quaternion.Euler(0f, 90f, 0f);   // barrel +Z -> world +X (side view from -Z)
                holder.localScale = Vector3.one * 3f;
                var data = new WeaponData { Type = WeaponType.Experimental, Variant = v, Tint = tints[i % tints.Length] };
                var model = WeaponModelBuilder.Build(data, holder);
                ctx.Check(CampaignWeaponModels.TryKind(v, out var kind) && CampaignWeaponModels.Handles(kind), v + ": campaign kind " + kind);
                var muzzle = model.transform.Find("Muzzle");
                var grip = model.transform.Find("Grip");
                var body = model.transform.Find("Body");
                ctx.Check(muzzle != null && grip != null && body != null, v + ": Muzzle + Grip + Body children");
                if (muzzle != null && grip != null)
                {
                    var gb = grip.GetComponent<Renderer>().bounds;
                    float gripZ = model.transform.InverseTransformPoint(gb.center).z;
                    ctx.Check(muzzle.localPosition.z > 0.2f && muzzle.localPosition.z > gripZ + 0.15f, v + ": muzzle z " + muzzle.localPosition.z.ToString("0.00") + " in front of the grip (" + gripZ.ToString("0.00") + ")");
                    // muzzle at the front of the silhouette (within 6 cm of the max z of all parts)
                    float maxZ = float.MinValue;
                    foreach (var r in model.GetComponentsInChildren<Renderer>())
                    {
                        var b = r.bounds;
                        for (int k = 0; k < 8; k++)
                        {
                            var corner = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                            maxZ = Mathf.Max(maxZ, model.transform.InverseTransformPoint(corner).z);
                        }
                    }
                    ctx.Check(Mathf.Abs(maxZ - muzzle.localPosition.z) < 0.08f, v + ": muzzle at the barrel tip (model front " + maxZ.ToString("0.00") + ")");
                    ctx.Check(maxZ > 0.3f && maxZ < 0.85f, v + ": readable size (length to tip " + maxZ.ToString("0.00") + " m)");
                }
                int rc = model.GetComponentsInChildren<Renderer>().Length;
                ctx.Check(rc <= 8, v + ": renderers " + rc + " <= 8");
            }
            RenderTestUtil.Place(cam, new Vector3(0f, 2.1f, -9.5f), new Vector3(0f, 2.0f, 0f), 50f);
            yield return ctx.Wait(0.2f);
            var shot = ctx.Shot("weapons_side");
            ctx.Check(RenderTestUtil.MagentaFraction(shot) < 0.001f, "weapons: no magenta");
            RenderTestUtil.Place(cam, new Vector3(5f, 4f, -7f), new Vector3(0f, 2.0f, 0f), 50f);
            yield return ctx.Wait(0.15f);
            ctx.Shot("weapons_three_quarter");
            // close-ups, two at a time
            for (int i = 0; i < 8; i += 2)
            {
                var a = grid.GetChild(i).position; var b2 = grid.GetChild(i + 1).position;
                var mid = (a + b2) * 0.5f;
                RenderTestUtil.Place(cam, mid + new Vector3(-0.8f, 1.0f, -3.6f), mid, 45f);
                yield return ctx.Wait(0.12f);
                ctx.Shot("weapons_pair" + (i / 2));
            }
            Object.Destroy(stage);
            yield return null;
        }
    }
}
