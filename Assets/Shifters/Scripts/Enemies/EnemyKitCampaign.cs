// =====================================================================================
//  SHIFTERS - EnemyKitCampaign.cs   [owned by the ENEMIES agent]
//  Campaign factories (CAMPAIGN §18.4): SecurityCamera (+ CameraPost), AlarmLatch, EscortBack, GuardBot,
//  Stalker, ChargingDock. Core/Kit.cs conventions: everything built inside the factory, edit-mode safe,
//  positions LOCAL to parent (world if parent null), cached meshes.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class EnemyKit
    {
        /// <summary>A sweeping "ME-CAM" at pos (the lens; mount it on a wall/post/ceiling at ~4.5 m). rot's yaw = the
        /// sweep centre (the camera looks along rot*forward). sweepDeg = AMPLITUDE (yaw = centre +- sweepDeg),
        /// period s, range m (planar, on the floor), fov = full cone angle, phase = fraction of the period.
        /// The floor wedge is placed on the first floor below at Start.</summary>
        public static SecurityCamera SecurityCamera(Transform parent, Vector3 pos, Quaternion? rot, CameraDetect mode, float sweepDeg = 35f,
            float period = 6f, float range = 12f, float fov = 40f, float phase = 0f)
        {
            Quaternion r = rot ?? Quaternion.identity;
            Vector3 f = r * Vector3.forward; f.y = 0f;
            Quaternion yawRot = f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity;
            var go = Kit.Make("SecurityCamera_" + mode, parent, pos, yawRot);
            var t = go.transform;

            // wall/ceiling bracket (visual) behind the head
            Kit.BoxVisual(t, "BracketPlate", new Vector3(0f, 0.32f, -0.42f), new Vector3(0.3f, 0.3f, 0.05f), MatKey.MetalDark, 0.02f);
            Kit.BoxVisual(t, "BracketArm", new Vector3(0f, 0.32f, -0.22f), new Vector3(0.07f, 0.07f, 0.4f), MatKey.MetalDark, 0.01f);
            Kit.BoxVisual(t, "BracketDrop", new Vector3(0f, 0.17f, 0f), new Vector3(0.07f, 0.3f, 0.07f), MatKey.MetalDark, 0.01f);

            var head = Kit.Make("Head", t, Vector3.zero);
            var body = Kit.Make("Body", head.transform, Vector3.zero, Quaternion.Euler(24f, 0f, 0f));
            var bt = body.transform;
            Kit.BoxVisual(bt, "Housing", new Vector3(0f, 0f, 0.08f), new Vector3(0.34f, 0.3f, 0.62f), MatKey.SignWhite, 0.06f);
            Kit.BoxVisual(bt, "Hood", new Vector3(0f, 0.17f, 0.16f), new Vector3(0.4f, 0.04f, 0.72f), MatKey.MetalDark, 0.015f);
            Kit.BoxVisual(bt, "Stripe", new Vector3(0f, 0f, 0.08f), new Vector3(0.355f, 0.06f, 0.5f), MatKey.Gold, 0.01f, null, false);
            ProcMesh.Part("LensRing", bt, ProcMesh.Cylinder(0.12f, 0.08f, 16, true), Kit.Mat(MatKey.Black), new Vector3(0f, 0f, 0.42f), Quaternion.Euler(90f, 0f, 0f), null, false);
            ProcMesh.Part("LensGlass", bt, ProcMesh.Cylinder(0.085f, 0.02f, 16, true), Kit.Mat(MatKey.LightBlue), new Vector3(0f, 0f, 0.465f), Quaternion.Euler(90f, 0f, 0f), null, false);
            var led = Kit.BoxVisual(bt, "Led", new Vector3(0.11f, 0.08f, 0.395f), new Vector3(0.05f, 0.05f, 0.02f), MatKey.LightRed, 0f, null, false);
            var lens = Kit.Make("Lens", bt, new Vector3(0f, 0f, 0.5f));
            var col = body.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0f, 0.1f);
            col.size = new Vector3(0.42f, 0.38f, 0.8f);

            var cone = EnemyConeVisual.Create(t, "FloorCone", new Vector3(0f, -pos.y + 0.03f, 0f), range, fov * 0.5f, EnemyConeVisual.Idle);

            var cam = go.AddComponent<Shifters.SecurityCamera>();
            cam.Mode = mode; cam.SweepDeg = sweepDeg; cam.Period = period; cam.Range = range; cam.Fov = fov; cam.Phase = phase;
            cam.Head = head.transform; cam.Lens = lens.transform; cam.Cone = cone; cam.Led = led;
            cam.SolvedMessage = "";

            var rev = go.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Weapon;
            rev.Highlight = true;
            rev.Label = "SHOOT: OFFLINE 5 S";
            rev.LabelOffset = new Vector3(0f, 0.8f, 0f);
            rev.Renderers = body.GetComponentsInChildren<Renderer>(true);
            return cam;
        }

        /// <summary>A marble-and-gold camera post: floorPos = its foot (local), height = top (put the camera at
        /// floorPos + up*height). Solid collider.</summary>
        public static Transform CameraPost(Transform parent, Vector3 floorPos, float height = 4.5f)
        {
            var go = Kit.Make("CameraPost", parent, floorPos);
            var t = go.transform;
            Kit.SolidBox(t, "Post", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.24f, height, 0.24f), MatKey.MetalDark, 0.03f);
            Kit.BoxVisual(t, "Foot", new Vector3(0f, 0.08f, 0f), new Vector3(0.6f, 0.16f, 0.6f), MatKey.MetalDark, 0.04f);
            Kit.BoxVisual(t, "Band", new Vector3(0f, height - 0.35f, 0f), new Vector3(0.28f, 0.08f, 0.28f), MatKey.Gold, 0.01f, null, false);
            Kit.BoxVisual(t, "Cap", new Vector3(0f, height + 0.45f, 0f), new Vector3(0.34f, 0.06f, 0.34f), MatKey.MetalDark, 0.02f);
            return t;
        }

        /// <summary>Alarm latch (SignalSource) with a red beacon at pos: ON when any of cams detects, auto-clears
        /// after resetSeconds (or ResetAlarm()). Counts RunStats.TeamworkViolations.</summary>
        public static AlarmLatch AlarmLatch(Transform parent, Vector3 pos, SecurityCamera[] cams, float resetSeconds = 8f)
        {
            var go = Kit.Make("AlarmLatch", parent, pos);
            var t = go.transform;
            Kit.BoxVisual(t, "BeaconBase", new Vector3(0f, 0.06f, 0f), new Vector3(0.3f, 0.12f, 0.3f), MatKey.MetalDark, 0.02f);
            ProcMesh.Part("BeaconDome", t, ProcMesh.Sphere(0.14f, 12, 8, true), Kit.Mat(MatKey.LightOff), new Vector3(0f, 0.18f, 0f), null, null, false);
            var lit = ProcMesh.Part("BeaconLit", t, ProcMesh.Sphere(0.15f, 12, 8, true), Kit.Mat(MatKey.LightRed), new Vector3(0f, 0.18f, 0f), null, null, false);
            var mr = lit.GetComponent<Renderer>();
            mr.enabled = false;
            var a = go.AddComponent<Shifters.AlarmLatch>();
            a.Cameras = cams != null ? (SecurityCamera[])cams.Clone() : new SecurityCamera[0];
            a.ResetSeconds = resetSeconds;
            a.Beacon = mr;
            return a;
        }

        /// <summary>CH04 escort: on alarm trip fade -> teleport the team to returnPoints -> cameras pause 3 s -> alarm clears.</summary>
        public static EscortBack EscortBack(Transform parent, AlarmLatch alarm, Transform[] returnPoints)
        {
            var go = Kit.Make("EscortBack", parent, Vector3.zero);
            var e = go.AddComponent<Shifters.EscortBack>();
            e.Alarm = alarm;
            e.ReturnPoints = returnPoints != null ? (Transform[])returnPoints.Clone() : new Transform[0];
            return e;
        }

        /// <summary>A bank GUARD bot: SecurityBot with a cap, a floor vision cone (sightRange/fov) and an optional coffee
        /// break (coffeeBreakAt s after start, then every 20 s for 6 s; &lt; 0 = never).</summary>
        public static GuardBot GuardBot(Transform parent, Vector3 pos, Quaternion rot, Vector3[] waypoints, string cellId, Vector3 arenaCenter,
            float arenaRadius, float sightRange = 10f, float fov = 90f, float coffeeBreakAt = -1f)
        {
            var bot = SecurityBot(parent, pos, rot, waypoints, cellId, arenaCenter, arenaRadius);
            bot.gameObject.name = "GuardBot";
            bot.SightRange = sightRange;
            bot.Fov = fov;
            Transform cap = null;
            var botModel = bot.transform.Find("Model");
            if (botModel != null && botModel.childCount > 0 && botModel.GetChild(0).Find("Body") != null)
            {
                try { var cg = EnemyModelBuilder.AddGuardCap(botModel.GetChild(0).gameObject); if (cg != null) cap = cg.transform; } catch { cap = null; }
            }
            if (cap == null && bot.HeadAnchor != null) cap = StalkerModels.GuardCap(bot.HeadAnchor);
            var cone = EnemyConeVisual.Create(bot.transform, "VisionCone", new Vector3(0f, 0.05f, 0f), sightRange, fov * 0.5f, EnemyConeVisual.Idle);
            var g = bot.gameObject.AddComponent<Shifters.GuardBot>();
            g.Bot = bot; g.Cone = cone; g.Cap = cap; g.CoffeeBreakAt = coffeeBreakAt;
            var rev = bot.GetComponent<Revealable>();
            if (rev != null) rev.Label = "GUARD: SHOOT TO STUN";
            return g;
        }

        /// <summary>A stalker (Team-Building Dummy / cardboard Max) at pos facing rot. cellId: HoldingCell for hugs
        /// (empty = KO). Never leaves arenaCenter/arenaRadius (local); dormant while no intern is near it.</summary>
        public static StalkerBrain Stalker(Transform parent, Vector3 pos, Quaternion rot, string cellId, Vector3 arenaCenter, float arenaRadius, StalkerSkin skin)
        {
            var go = Kit.Make(skin == StalkerSkin.CardboardMax ? "Stalker_CardboardMax" : "Stalker_Dummy", parent, pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = StalkerBrain.Mass;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = skin == StalkerSkin.CardboardMax ? 0.4f : 0.33f;
            col.height = 1.95f;
            col.center = new Vector3(0f, 0.975f, 0f);
            var model = Kit.Make("Model", go.transform, Vector3.zero);
            // visuals' DummyModel / CardboardMaxModel (animated, freezes when lit); our placeholder if unavailable
            IStalkerVisual vis = null;
            float h;
            try
            {
                bool mild = false;
                try { mild = SessionActions.Settings != null && SessionActions.Settings.SpookyMild; } catch { }
                vis = StalkerSkins.Build(skin, model.transform, mild);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Kit.SafeDestroy(c);
            }
            catch (System.Exception e) { Debug.LogWarning("[Enemies] visuals stalker model failed, placeholder: " + e.Message); vis = null; }
            if (vis != null) h = skin == StalkerSkin.CardboardMax ? CardboardMaxModel.Height + 0.1f : DummyModel.Height + 0.1f;
            else h = StalkerModels.Build(model.transform, skin);
            var head = Kit.Make("HeadAnchor", go.transform, new Vector3(0f, h, 0f));
            var s = go.AddComponent<StalkerBrain>();
            s.CellId = cellId ?? "";
            s.ArenaCenter = arenaCenter;
            s.ArenaRadius = arenaRadius;
            s.Skin = skin;
            s.ModelRoot = model.transform;
            s.Visual = vis as Component;
            s.HeadAnchor = head.transform;
            s.ModelHeight = h;
            var rev = go.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Intellect;
            rev.Highlight = true;
            rev.Label = "THREAT: MOVES IN THE DARK";
            rev.LabelOffset = new Vector3(0f, h + 0.4f, 0f);
            return s;
        }

        /// <summary>Charging dock (promoted L5ChargingDock): pad + charger post facing rot*forward; when its inputs power
        /// it, bot glides onto the pad and sleeps.</summary>
        public static ChargingDock ChargingDock(Transform parent, Vector3 pos, Quaternion? rot, SecurityBot bot, params SignalSource[] inputs)
        {
            var go = Kit.Make("ChargingDock", parent, pos, rot ?? Quaternion.identity);
            var dt = go.transform;
            Kit.BoxVisual(dt, "Pad", new Vector3(0f, 0.04f, 0f), new Vector3(2.2f, 0.08f, 2.2f), MatKey.MetalDark, 0.02f);
            Kit.BoxVisual(dt, "PadGlow", new Vector3(0f, 0.085f, 0f), new Vector3(1.6f, 0.01f, 1.6f), MatKey.LightBlue, 0f, null, false);
            Kit.SolidBox(dt, "ChargerPost", new Vector3(0f, 1.3f, -1.25f), new Vector3(1.6f, 2.6f, 0.35f), MatKey.PuzzleBody, 0.04f);
            Kit.BoxVisual(dt, "Bolt", new Vector3(0f, 1.9f, -1.06f), new Vector3(0.5f, 0.7f, 0.04f), MatKey.HazardYellow, 0.01f, null, false);
            var lamp = PuzzleVisuals.Lamp(dt, "DockLamp", new Vector3(0f, 2.75f, -1.25f), Quaternion.Euler(-90f, 0f, 0f), 0.2f);
            var text = PuzzleVisuals.Text(dt, "DockStatus", new Vector3(0f, 1.15f, -1.06f), PuzzleVisuals.Facing(Vector3.forward), "DOCK: NO POWER", 2.2f, UIColors.Text, 1.5f, 0.4f);
            var point = Kit.Make("DockPoint", dt, new Vector3(0f, 0.08f, 0f), Quaternion.identity).transform;
            var dock = go.AddComponent<Shifters.ChargingDock>();
            dock.Bot = bot; dock.DockPoint = point; dock.Status = text; dock.Lamp = lamp;
            if (inputs != null && inputs.Length > 0) PuzzleKit.Wire(dock, inputs);
            return dock;
        }
    }
}
