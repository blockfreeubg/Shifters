using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Test-only IInteractable (counts Interact calls, can be blocked).</summary>
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        public int Count;
        public bool Blocked;
        public int InteractPriority => 0;
        public Vector3 InteractPoint => transform.position;
        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = Blocked ? "Requires IQ 11 - nobody has that" : null;
            return !Blocked;
        }
        public string GetPrompt(ShifterCharacter who) => "Press the dummy";
        public void Interact(ShifterCharacter who) { Count++; }
    }

    /// <summary>DevPlay scenarios for the ABILITIES module (grab/throw, living bridge, flop, prompts).
    /// Run: devplay.sh &lt;proj&gt; abilities_all -devSetup Shifters.AbilitiesTests.RunAll -devSeconds 1</summary>
    public static class AbilitiesTests
    {
        static ShifterCharacter s_brute, s_sprinter, s_brainiac;
        static bool s_realMotor;

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            yield return Setup(ctx);
            yield return GrabThrow(ctx);
            yield return BridgeTests(ctx);
            yield return PlateAndPromptTests(ctx);
            ctx.Log("abilities scenario finished");
        }

        public static IEnumerator OnlyBridge(DevPlayContext ctx) { yield return Setup(ctx); yield return BridgeTests(ctx); }
        public static IEnumerator OnlyPlates(DevPlayContext ctx) { yield return Setup(ctx); yield return PlateAndPromptTests(ctx); }

        // ------------------------------------------------------------------ helpers
        static IEnumerator Setup(DevPlayContext ctx)
        {
            // area A (grab/throw) floor
            Kit.SolidBox(null, "FloorA", new Vector3(0, -0.5f, 4), new Vector3(30, 1, 40), MatKey.Floor);
            var mgr = new GameObject("[Team]").AddComponent<CharacterManager>();
            var team = CharacterGenerator.GenerateTeam(4242);
            var spawns = new List<Pose> { new Pose(new Vector3(-4, 0.05f, -6), Quaternion.identity), new Pose(new Vector3(0, 0.05f, -6), Quaternion.identity), new Pose(new Vector3(4, 0.05f, -6), Quaternion.identity) };
            mgr.SpawnTeam(team, spawns, 0);
            foreach (var c in mgr.Team)
            {
                c.ExternalControl = true;
                if (c.Archetype == Archetype.Brute) s_brute = c;
                else if (c.Archetype == Archetype.Sprinter) s_sprinter = c;
                else s_brainiac = c;
            }
            ctx.Check(s_brute != null && s_sprinter != null && s_brainiac != null, "team spawned (one of each archetype)");
            yield return ctx.Wait(0.6f);
            s_realMotor = s_brute.Motor.IsGrounded;
            ctx.Log("motor: " + (s_realMotor ? "REAL core motor" : "STUB motor (walking checks skipped)") +
                    " | brute " + s_brute.DisplayName + " " + s_brute.Mass.ToString("0") + "kg STR" + s_brute.Stats.Strength +
                    " | sprinter " + s_sprinter.Mass.ToString("0") + "kg | brainiac " + s_brainiac.Mass.ToString("0") + "kg STR" + s_brainiac.Stats.Strength);
        }

        static void Place(ShifterCharacter c, Vector3 p, float yaw = 0f)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            if (c.Motor != null) c.Motor.Teleport(p, rot); else c.transform.SetPositionAndRotation(p, rot);
        }

        static void Cam(Vector3 pos, Vector3 look)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = pos;
            cam.transform.LookAt(look);
        }

        static Rigidbody MakeCrate(Vector3 pos, float mass, float size = 0.8f)
        {
            var go = new GameObject("Crate" + mass);
            go.transform.position = pos;
            ProcMesh.AddRenderer(go, ProcMesh.CachedBox(Vector3.one * size, 0.05f), Kit.Mat(MatKey.Wood));
            go.AddComponent<BoxCollider>().size = Vector3.one * size;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var g = go.AddComponent<Grabbable>();
            g.DisplayName = "crate";
            go.AddComponent<WeightBody>();
            return rb;
        }

        static WeightSensor MakePlate(Vector3 pos)
        {
            var plateGo = Kit.Make("TestPlate", null, pos);
            var top = Kit.SolidBox(plateGo.transform, "Top", Vector3.zero, new Vector3(2.4f, 0.1f, 2.4f), MatKey.PuzzleBody);
            var s = plateGo.AddComponent<WeightSensor>();
            s.Surfaces = new Collider[] { top };
            return s;
        }

        static void Walk(ShifterCharacter c, Vector3 dir) { if (c.Motor != null) c.Motor.SetCommand(new CharacterMotor.Command { Move = dir }); }
        static void Stop(ShifterCharacter c) { if (c.Motor != null) c.Motor.SetCommand(default(CharacterMotor.Command)); }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        static void DebugPlankVisual(CharacterBridge b)
        {
            // stub CharacterVisual has no bridge pose: make the plank visible in screenshots (test only)
            var v = b.Character.Visual;
            if (b.Plank == null || v == null || v.ModelRoot == null || v.ModelRoot.childCount > 4) return;
            if (b.Plank.transform.Find("DebugPlank") != null) return;
            var r = Kit.BoxVisual(b.Plank.transform, "DebugPlank", b.Plank.center, b.Plank.size, MatKey.Wood, 0.04f);
            r.sharedMaterial = MaterialLibrary.Colored(b.Character.Color);
            r.gameObject.AddComponent<DebugPlankFollower>().Plank = b.Plank;
        }

        /// <summary>Test-only: hides the debug plank renderer whenever the plank collider is off.</summary>
        class DebugPlankFollower : MonoBehaviour
        {
            public BoxCollider Plank;
            Renderer _r;
            void LateUpdate() { if (_r == null) _r = GetComponent<Renderer>(); if (_r != null && Plank != null) _r.enabled = Plank.enabled; }
        }

        // ------------------------------------------------------------------ 1) grab & throw
        static IEnumerator GrabThrow(DevPlayContext ctx)
        {
            var brute = s_brute; var sprinter = s_sprinter; var brainiac = s_brainiac;
            Place(brute, new Vector3(0, 0.02f, 0), 0);
            Place(sprinter, new Vector3(-6, 0.02f, 0), 0);
            Place(brainiac, new Vector3(6, 0.02f, 0), 0);
            var crate = MakeCrate(new Vector3(0, 0.45f, 1.25f), Tuning.CrateSmallKg);
            var crateG = crate.GetComponent<Grabbable>();
            yield return ctx.Wait(0.5f);

            bool grabbed = brute.Grabber.TryGrab();
            ctx.Check(grabbed && brute.Grabber.Held == crateG, "Brute grabs the 25 kg crate (TryGrab candidate search)");
            ctx.Check(crate.isKinematic && crate.transform.parent == brute.CarryPoint, "held crate kinematic + parented to CarryPoint");
            ctx.Check(brute.Motor.SpeedMultiplier < 0.999f && brute.Motor.SpeedMultiplier >= 0.6f, "carrying slows the carrier (x" + brute.Motor.SpeedMultiplier.ToString("0.00") + ")");
            yield return ctx.Wait(0.4f);
            var rend = crate.GetComponent<Renderer>();
            ctx.Check(Mathf.Abs(rend.bounds.min.y - brute.CarryPoint.position.y) < 0.08f, "crate bottom sits on the carry point (" + rend.bounds.min.y.ToString("0.00") + " vs " + brute.CarryPoint.position.y.ToString("0.00") + ")");
            Vector3 landPos = Vector3.zero; bool landed = false;
            crateG.Landed += (g, p, s) => { landPos = p; landed = true; };
            Vector3 start = crate.position;
            Cam(new Vector3(-9, 4, 3), new Vector3(0, 2.5f, 6));
            brute.Grabber.Throw(CharacterGrabber.DirectionFrom(brute.transform.forward, Tuning.ThrowBaseAngle));
            ctx.Check(!crate.isKinematic && brute.Grabber.Held == null && Mathf.Approximately(brute.Motor.SpeedMultiplier, 1f), "throw releases the crate and restores the carrier");
            yield return ctx.Wait(0.3f);
            ctx.Shot("01_crate_mid_throw");
            for (int i = 0; i < 240 && !landed; i++) yield return null;
            float dist = Flat(start, landPos);
            ctx.Check(landed && dist >= 6f, "Brute throws the 25 kg crate " + dist.ToString("0.0") + " m (>= 6)");

            // --- Brainiac can't lift the Brute
            Place(brainiac, new Vector3(0, 0.02f, -1.35f), 0);
            Place(brute, new Vector3(0, 0.02f, 0), 0);
            string lastToast = null; int strToasts = 0;
            System.Action<string, Color> onToast = (t, c) => { lastToast = t; if (t != null && t.Contains("STR")) strToasts++; };
            GameEvents.Toast += onToast;
            bool denied = false;
            brainiac.Grabber.GrabDenied += g => denied = true;
            yield return ctx.Wait(0.4f);
            bool liftBrute = brainiac.Grabber.TryGrab(brute.Grabbable);
            bool liftAny = brainiac.Grabber.TryGrab();
            ctx.Check(!liftBrute && !liftAny && brainiac.Grabber.Held == null && brute.Condition == CharacterCondition.Normal,
                "Brainiac (STR " + brainiac.Stats.Strength + ", cap " + brainiac.Grabber.Capacity.ToString("0") + ") cannot lift the Brute (" + brute.Mass.ToString("0") + " kg)");
            ctx.Check(denied && strToasts > 0, "too-heavy feedback fired (toast: '" + lastToast + "')");
            GameEvents.Toast -= onToast;

            // --- Brute carries the Sprinter
            Place(brainiac, new Vector3(8, 0.02f, -4), 0);
            Place(sprinter, new Vector3(0, 0.02f, 1.3f), 180);
            Place(brute, new Vector3(0, 0.02f, 0), 0);
            yield return ctx.Wait(0.5f);
            bool carry = brute.Grabber.TryGrab(sprinter.Grabbable);
            ctx.Check(carry && sprinter.Condition == CharacterCondition.Carried && sprinter.Body.isKinematic && sprinter.transform.parent == brute.CarryPoint,
                "Brute picks up the Sprinter (Carried, kinematic, parented)");
            ctx.Check(Mathf.Abs(brute.TotalMass - (brute.Mass + sprinter.Mass)) < 0.01f, "carrier TotalMass includes the carried teammate");
            ctx.Check(!sprinter.Capsule.enabled, "carried teammate's capsule disabled while held");
            yield return ctx.Wait(0.4f);
            Cam(brute.Feet + new Vector3(4.5f, 3.2f, -4.5f), brute.Feet + Vector3.up * 2.2f);
            ctx.Shot("02_carry_overhead");
            if (s_realMotor)
            {
                Vector3 p0 = brute.Feet;
                for (int i = 0; i < 360 && Flat(p0, brute.Feet) < 5f; i++) { Walk(brute, Vector3.forward); yield return null; }
                Stop(brute);
                ctx.Check(Flat(p0, brute.Feet) >= 5f && sprinter.Grabbable.Holder == brute.Grabber, "Brute walked " + Flat(p0, brute.Feet).ToString("0.0") + " m carrying the Sprinter");
                yield return ctx.Wait(0.5f);
            }
            else ctx.Log("SKIP walk-carry (stub motor)");

            // --- throw onto a 3.5 m ledge 4 m away
            Vector3 bf = brute.Feet; Vector3 fwd = brute.transform.forward; fwd.y = 0; fwd.Normalize();
            var ledgeRot = Quaternion.LookRotation(fwd);
            Kit.SolidBox(null, "Ledge", bf + fwd * 7f + Vector3.up * 1.75f, new Vector3(7, 3.5f, 6), MatKey.Concrete, 0.04f, ledgeRot);
            bool noise = false;
            System.Action<NoiseEvent> onNoise = e => { if (e.Source == sprinter) noise = true; };
            GameEvents.Noise += onNoise;
            bool sprLanded = false;
            System.Action<Grabbable, Vector3, float> onLand = (g, p, s) => sprLanded = true;
            sprinter.Grabbable.Landed += onLand;
            yield return ctx.Wait(0.3f);
            Vector3 target = bf + fwd * 6.8f + Vector3.up * 3.5f;
            bool solvable = brute.Grabber.ThrowAt(target);
            ctx.Check(solvable && sprinter.Condition == CharacterCondition.Thrown && !sprinter.Body.isKinematic && sprinter.transform.parent != brute.CarryPoint, "teammate thrown (Condition.Thrown, physics restored)");
            Cam(bf + Vector3.Cross(Vector3.up, fwd) * -10f + fwd * 3f + Vector3.up * 3.5f, bf + fwd * 4f + Vector3.up * 3f);
            yield return ctx.Wait(0.25f);
            ctx.Shot("03_teammate_mid_throw");
            for (int i = 0; i < 240 && !sprLanded; i++) yield return null;
            ctx.Check(noise, "thrown character emitted a Noise event on landing");
            if (s_realMotor)
            {
                for (int i = 0; i < 420 && sprinter.Condition != CharacterCondition.Normal; i++) yield return null;
                yield return ctx.Wait(0.3f);
            }
            else yield return ctx.Wait(1.5f);
            Vector3 local = Quaternion.Inverse(ledgeRot) * (sprinter.Feet - (bf + fwd * 7f));
            ctx.Check(sprinter.Feet.y > 3.3f && Mathf.Abs(local.z) < 3.1f && Mathf.Abs(local.x) < 3.6f, "Sprinter landed ON the 3.5 m ledge (feet y=" + sprinter.Feet.y.ToString("0.00") + ")");
            if (s_realMotor) ctx.Check(sprinter.Condition == CharacterCondition.Normal, "thrown Sprinter got up (Condition Normal)");
            Cam(bf + Vector3.Cross(Vector3.up, fwd) * -8f + fwd * 4f + Vector3.up * 6f, sprinter.Center);
            ctx.Shot("03b_landed_on_ledge");
            GameEvents.Noise -= onNoise;
            sprinter.Grabbable.Landed -= onLand;

            // --- carried teammate wriggles free; drop places in front on the ground
            Place(brute, new Vector3(-8, 0.02f, -6), 0);
            Place(brainiac, new Vector3(-8, 0.02f, -4.7f), 0);
            yield return ctx.Wait(0.4f);
            bool g2 = brute.Grabber.TryGrab(brainiac.Grabbable);
            ctx.Check(g2 && brainiac.Condition == CharacterCondition.Carried, "Brute picks up the Brainiac");
            yield return ctx.Wait(0.3f);
            brainiac.Grabbable.Wriggle(); brainiac.Grabbable.Wriggle();
            ctx.Check(brute.Grabber.Held == brainiac.Grabbable, "two wriggles: still carried");
            brainiac.Grabbable.Wriggle();
            ctx.Check(brute.Grabber.Held == null && brainiac.Condition == CharacterCondition.Normal && !brainiac.Body.isKinematic && brainiac.Capsule.enabled, "third wriggle breaks free (put down, Normal, physics back)");
            float ahead = Vector3.Dot(brainiac.Feet - brute.Feet, brute.transform.forward);
            ctx.Check(ahead > brute.Radius && Mathf.Abs(brainiac.Feet.y) < 0.2f, "put down in front on the ground (ahead " + ahead.ToString("0.00") + " m, y " + brainiac.Feet.y.ToString("0.00") + ")");
            yield return ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ 2) living bridge
        static IEnumerator BridgeTests(DevPlayContext ctx)
        {
            var brute = s_brute; var sprinter = s_sprinter; var brainiac = s_brainiac;
            const float X = 40f, X2 = 56f, gap = 5.4f;
            foreach (float x in new[] { X, X2 })
            {
                Kit.SolidBox(null, "NearLedge", new Vector3(x, -0.5f, -6f), new Vector3(6, 1, 12), MatKey.Floor);
                Kit.SolidBox(null, "FarLedge", new Vector3(x, -0.5f, gap + 6f), new Vector3(6, 1, 12), MatKey.FloorAlt);
                Kit.SolidBox(null, "PitFloor", new Vector3(x, -3.5f, 2.7f), new Vector3(10, 1, 30), MatKey.MetalDark);
            }
            Place(brainiac, new Vector3(X, 0.02f, -(brainiac.Radius + 0.05f)), 0);
            Place(sprinter, new Vector3(X, 0.02f, -5f), 0);
            Place(brute, new Vector3(X + 2.2f, 0.02f, -8f), 0);
            PromptInfo? lastPrompt = null;
            System.Action<PromptInfo?> onPrompt = p => lastPrompt = p;
            GameEvents.Prompt += onPrompt;
            CharacterManager.Instance.SwitchTo(brainiac.Slot, true);
            brainiac.ExternalControl = true;
            yield return ctx.Wait(0.6f);
            ctx.Check(lastPrompt != null && lastPrompt.Value.Text == "Become a bridge", "bridge hint prompt at a ledge facing the gap ('" + (lastPrompt != null ? lastPrompt.Value.Text : "null") + "')");

            bool formed = brainiac.Bridge.TryForm();
            ctx.Check(formed && brainiac.Condition == CharacterCondition.Bridging && brainiac.Body.isKinematic && !brainiac.Capsule.enabled, "Brainiac starts forming a bridge (Bridging, kinematic, capsule off)");
            yield return ctx.Wait(0.7f);
            var br = brainiac.Bridge;
            DebugPlankVisual(br);
            ctx.Check(br.IsSolid && br.Plank != null && br.Plank.enabled, "plank solid after the 0.5 s TIMBER fall");
            ctx.Check(Mathf.Abs(br.Plank.bounds.max.y - 0f) < 0.05f && br.Plank.bounds.max.z > gap + 0.3f, "plank top flush with the floor (" + br.Plank.bounds.max.y.ToString("0.000") + ") and reaches the far side (z " + br.Plank.bounds.max.z.ToString("0.0") + ")");
            ctx.Check(lastPrompt != null && lastPrompt.Value.Text.StartsWith("Get up"), "bridging prompt shows [B] Get up");

            float maxLoad = 0f; bool shot = false;
            if (s_realMotor)
            {
                for (int i = 0; i < 480 && sprinter.Feet.z < 8f; i++)
                {
                    Walk(sprinter, Vector3.forward);
                    if (sprinter.Feet.z > 0.6f && sprinter.Feet.z < gap - 0.3f) maxLoad = Mathf.Max(maxLoad, br.CurrentLoad);
                    if (!shot && sprinter.Feet.z > 2.0f)
                    {
                        shot = true;
                        Cam(new Vector3(X + 7.5f, 3.2f, -2.5f), new Vector3(X, -0.2f, 2.7f));
                        ctx.Shot("04_bridge_with_walker");
                    }
                    yield return null;
                }
                Stop(sprinter);
                ctx.Check(sprinter.Feet.z > gap + 0.4f && sprinter.Feet.y > -0.3f, "Sprinter walked across the 5.4 m gap on the bridge (z " + sprinter.Feet.z.ToString("0.0") + ", y " + sprinter.Feet.y.ToString("0.00") + ")");
                ctx.Check(Mathf.Abs(maxLoad - sprinter.Mass) < 2.5f, "plank load while walking ~ Sprinter mass (" + maxLoad.ToString("0.0") + " vs " + sprinter.Mass.ToString("0.0") + ")");
            }
            else ctx.Log("SKIP bridge walk (stub motor)");

            yield return ctx.Wait(0.3f);
            bool recovered = false;
            br.Recovered += b => recovered = true;
            br.Recover();
            yield return ctx.Wait(0.6f);
            ctx.Check(recovered && !br.IsBridging && brainiac.Condition == CharacterCondition.Normal && brainiac.Capsule.enabled && !brainiac.Body.isKinematic, "bridge recovered (Normal, capsule on, dynamic)");
            ctx.Check(brainiac.Feet.z > gap && Mathf.Abs(brainiac.Feet.y) < 0.2f, "recover snapped to the FAR side (z " + brainiac.Feet.z.ToString("0.00") + ")");

            // --- weak bridge collapses under two walkers
            int savedStr = brainiac.Stats.Strength;
            brainiac.Stats.Strength = 1;
            Place(sprinter, new Vector3(X2 - 2f, 0.02f, -4f), 0);
            Place(brute, new Vector3(X2 + 2f, 0.02f, -4f), 0);
            Place(brainiac, new Vector3(X2, 0.02f, -(brainiac.Radius + 0.05f)), 0);
            yield return ctx.Wait(0.5f);
            bool collapsed = false;
            br.Collapsed += b => collapsed = true;
            string banner = null;
            System.Action<string, string, BannerStyle> onBanner = (t, s, st) => banner = t;
            GameEvents.Banner += onBanner;
            ctx.Check(br.TryForm(), "weak (STR 1, cap " + br.Capacity.ToString("0") + ") bridge forms");
            yield return ctx.Wait(0.7f);
            DebugPlankVisual(br);
            Place(sprinter, new Vector3(X2, 0.06f, 1.6f), 0);
            Place(brute, new Vector3(X2, 0.06f, 3.8f), 180);
            Cam(new Vector3(X2 + 8f, 3f, -3f), new Vector3(X2, -0.5f, 2.7f));
            for (int i = 0; i < 20; i++) yield return null;
            ctx.Shot("05a_overloaded_bridge");
            float loadSeen = 0f;
            for (int i = 0; i < 150 && !collapsed; i++) { loadSeen = Mathf.Max(loadSeen, br.CurrentLoad); yield return null; }
            ctx.Check(collapsed, "two walkers (" + loadSeen.ToString("0") + " kg > " + br.Capacity.ToString("0") + ") collapse the bridge");
            ctx.Check(banner == "BRIDGE COLLAPSE!", "collapse banner raised");
            for (int i = 0; i < 9; i++) yield return null;
            ctx.Shot("05b_collapse");
            yield return ctx.Wait(1.2f);
            ctx.Check(sprinter.Feet.y < -1f && brute.Feet.y < -1f, "walkers fell into the pit (y " + sprinter.Feet.y.ToString("0.0") + ", " + brute.Feet.y.ToString("0.0") + ")");
            ctx.Check(!br.IsBridging && brainiac.Capsule.enabled && !brainiac.Body.isKinematic && br.Plank != null && !br.Plank.enabled, "collapsed bridger is a physics body again, plank gone");
            if (s_realMotor)
            {
                for (int i = 0; i < 600 && brainiac.Condition != CharacterCondition.Normal; i++)
                {
                    if (i % 60 == 0) ctx.Log("bridger t+" + (i / 60) + "s cond=" + brainiac.Condition + " tumbling=" + brainiac.Motor.IsTumbling + " ts=" + Time.timeScale.ToString("0.00") +
                        " pos=" + brainiac.transform.position.ToString("F2") + " v=" + brainiac.Body.linearVelocity.ToString("F2") + " w=" + brainiac.Body.angularVelocity.magnitude.ToString("0.00") +
                        " kin=" + brainiac.Body.isKinematic + " com=" + brainiac.Body.centerOfMass.ToString("F2") + " capsule=" + brainiac.Capsule.enabled);
                    yield return null;
                }
                ctx.Check(brainiac.Condition == CharacterCondition.Normal && brainiac.Feet.y > -0.3f, "bridger tumbled on the near ledge and got up (y " + brainiac.Feet.y.ToString("0.00") + ", cond " + brainiac.Condition + ")");
            }
            brainiac.Stats.Strength = savedStr;
            GameEvents.Banner -= onBanner;
            GameEvents.Prompt -= onPrompt;
        }

        // ------------------------------------------------------------------ 3) plates, flop, prompts
        static IEnumerator PlateAndPromptTests(DevPlayContext ctx)
        {
            var brute = s_brute; var sprinter = s_sprinter; var brainiac = s_brainiac;
            const float X = -40f;
            Kit.SolidBox(null, "FloorC", new Vector3(X, -0.5f, 0), new Vector3(24, 1, 24), MatKey.Floor);
            var plate = MakePlate(new Vector3(X, 0.05f, 0));
            var plate2 = MakePlate(new Vector3(X + 6f, 0.05f, 0));
            Place(sprinter, new Vector3(X, 0.15f, 0), 0);
            Place(brute, new Vector3(X + 6f, 0.15f, -4f), 0);
            Place(brainiac, new Vector3(X + 6f, 0.15f, -2.7f), 0);
            yield return ctx.Wait(0.6f);
            sprinter.Flop.SetFlopped(true);
            ctx.Check(sprinter.Flop.IsFlopped && sprinter.Condition == CharacterCondition.Flopped, "Sprinter flops (Condition.Flopped)");
            yield return ctx.Wait(2.0f);
            ctx.Check(sprinter.Condition == CharacterCondition.Flopped, "flopped character stays flopped (no auto get-up)");
            ctx.Check(Mathf.Abs(plate.CurrentMass - sprinter.Mass) < 1f, "flopped Sprinter on a plate counts its mass (" + plate.CurrentMass.ToString("0.0") + " vs " + sprinter.Mass.ToString("0.0") + ")");
            Cam(new Vector3(X - 3.5f, 2.2f, -3.5f), sprinter.Center);
            ctx.Shot("06_flop_on_plate");
            sprinter.Flop.SetFlopped(false);
            yield return ctx.Wait(0.6f);
            ctx.Check(sprinter.Condition == CharacterCondition.Normal && !sprinter.Flop.IsFlopped, "X again: flopped Sprinter gets up (Normal)");

            // carrying on a plate counts both
            bool g = brute.Grabber.TryGrab(brainiac.Grabbable);
            ctx.Check(g, "Brute grabs the Brainiac for the plate test");
            yield return ctx.Wait(0.3f);
            if (s_realMotor)
            {
                for (int i = 0; i < 240 && brute.Feet.z < -0.1f; i++) { Walk(brute, Vector3.forward); yield return null; }
                Stop(brute);
            }
            else Place(brute, new Vector3(X + 6f, 0.15f, 0f), 0);
            yield return ctx.Wait(0.8f);
            float expect = brute.Mass + brainiac.Mass;
            ctx.Check(Mathf.Abs(plate2.CurrentMass - expect) < 1.5f, "character carrying another on a plate counts both (" + plate2.CurrentMass.ToString("0.0") + " vs " + expect.ToString("0.0") + ")");
            brute.Grabber.Drop();
            yield return ctx.Wait(0.5f);

            // flopped teammate can be carried and stays flopped after the drop
            Place(sprinter, new Vector3(X - 6f, 0.15f, 1.2f), 0);
            Place(brute, new Vector3(X - 6f, 0.15f, 0f), 0);
            yield return ctx.Wait(0.4f);
            sprinter.Flop.SetFlopped(true);
            yield return ctx.Wait(1.0f);
            bool gf = brute.Grabber.TryGrab(sprinter.Grabbable);
            ctx.Check(gf && sprinter.Condition == CharacterCondition.Carried, "flopped teammate can be carried");
            yield return ctx.Wait(0.3f);
            brute.Grabber.Drop();
            ctx.Check(sprinter.Condition == CharacterCondition.Flopped, "dropped flopped teammate stays flopped");
            sprinter.Flop.SetFlopped(false);
            yield return ctx.Wait(0.6f);

            // prompts + interact
            var dummyGo = Kit.Make("Dummy", null, new Vector3(X, 1f, 7f));
            ProcMesh.AddRenderer(dummyGo, ProcMesh.CachedBox(new Vector3(0.6f, 0.6f, 0.6f), 0.05f), Kit.Mat(MatKey.PuzzleBody));
            dummyGo.AddComponent<BoxCollider>().size = new Vector3(0.6f, 0.6f, 0.6f);
            var dummy = dummyGo.AddComponent<TestInteractable>();
            PromptInfo? last = null; int nullPrompts = 0;
            System.Action<PromptInfo?> onPrompt = p => { last = p; if (p == null) nullPrompts++; };
            GameEvents.Prompt += onPrompt;
            string toast = null;
            System.Action<string, Color> onToast = (t, c) => toast = t;
            GameEvents.Toast += onToast;
            Place(brute, new Vector3(X, 0.02f, 5.9f), 0);
            CharacterManager.Instance.SwitchTo(brute.Slot, true);
            brute.ExternalControl = true;
            yield return ctx.Wait(0.4f);
            ctx.Check(last != null && last.Value.Text == "Press the dummy" && !last.Value.Blocked, "prompt raised for a dummy IInteractable ('" + (last != null ? last.Value.Key + "] " + last.Value.Text : "null") + "')");
            bool used = brute.Sensor.TryInteract();
            ctx.Check(used && dummy.Count == 1, "TryInteract() calls Interact() (count " + dummy.Count + ")");
            dummy.Blocked = true;
            yield return ctx.Wait(0.3f);
            ctx.Check(last != null && last.Value.Blocked && !string.IsNullOrEmpty(last.Value.BlockedReason), "blocked interactable shows a blocked prompt with the reason");
            bool used2 = brute.Sensor.TryInteract();
            ctx.Check(!used2 && dummy.Count == 1 && toast != null && toast.Contains("IQ"), "blocked TryInteract refuses + toasts the reason ('" + toast + "')");
            // grab prompt
            Place(brute, new Vector3(X + 3f, 0.02f, 5.9f), 0);
            MakeCrate(new Vector3(X + 3f, 0.45f, 7.1f), Tuning.CrateSmallKg);
            yield return ctx.Wait(0.5f);
            ctx.Check(last != null && last.Value.Text == "Pick up crate", "grab prompt '[F] Pick up crate' ('" + (last != null ? last.Value.Text : "null") + "')");
            brute.Grabber.TryGrab();
            yield return ctx.Wait(0.3f);
            ctx.Check(last != null && last.Value.Text.StartsWith("Throw crate"), "holding prompt '[Q] Throw crate  [F] Put down' ('" + (last != null ? last.Value.Text : "null") + "')");
            brute.Grabber.Drop();
            // switching away clears the prompt
            int before = nullPrompts;
            CharacterManager.Instance.SwitchTo(sprinter.Slot, true);
            sprinter.ExternalControl = true;
            yield return ctx.Wait(0.2f);
            ctx.Check(nullPrompts > before || last == null, "switching away clears the prompt");
            GameEvents.Prompt -= onPrompt;
            GameEvents.Toast -= onToast;
        }
    }
}
