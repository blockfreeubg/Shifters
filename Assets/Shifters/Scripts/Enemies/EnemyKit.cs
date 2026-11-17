// =====================================================================================
//  SHIFTERS - EnemyKit.cs   [owned by the ENEMIES agent; campaign factories in EnemyKitCampaign.cs]
//  Runtime/edit-mode factories (Core/Kit.cs conventions) for security bots and holding cells.
//  All positions/waypoints/arena centers are in the PARENT's local space (world if parent null).
// =====================================================================================
using System.Globalization;
using UnityEngine;

namespace Shifters
{
    public static partial class EnemyKit
    {
        // ---- holding cell dimensions (local; the door faces +Z)
        public const float CellWidth = 3.6f;
        public const float CellDepth = 3.6f;
        public const float CellHeight = 3.2f;
        public const float CellDoorWidth = 2.6f;

        /// <summary>A BONK-BOT 3000. waypoints: patrol loop (empty = guard its spawn). cellId: HoldingCell it delivers to.
        /// arenaCenter/arenaRadius: circle it never leaves while hunting (radius &lt;= 0 = unlimited).</summary>
        public static SecurityBot SecurityBot(Transform parent, Vector3 pos, Quaternion rot, Vector3[] waypoints, string cellId, Vector3 arenaCenter, float arenaRadius)
        {
            var go = Kit.Make("SecurityBot", parent, pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Shifters.SecurityBot.Mass;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0f;
            rb.angularDamping = 5f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.6f;
            col.height = 1.6f;
            col.center = new Vector3(0f, 0.8f, 0f);

            Color accent = UIColors.Accent;
            var model = Kit.Make("Model", go.transform, Vector3.zero);
            GameObject built = null;
            try { built = EnemyModelBuilder.Build(model.transform, accent); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (model.GetComponentInChildren<Renderer>(true) == null) BotFallbackModel.Build(built != null ? built.transform : model.transform, accent);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Kit.SafeDestroy(c); // visual only

            var grab = Kit.Make("GrabPoint", go.transform, new Vector3(0f, 1.55f, 0.95f));
            var eye = Kit.Make("EyePoint", go.transform, new Vector3(0f, 1.3f, 0.5f));
            var head = Kit.Make("HeadAnchor", go.transform, new Vector3(0f, 1.95f, 0f));

            var bot = go.AddComponent<Shifters.SecurityBot>();
            bot.Waypoints = waypoints != null ? (Vector3[])waypoints.Clone() : new Vector3[0];
            bot.CellId = cellId ?? "";
            bot.ArenaCenter = arenaCenter;
            bot.ArenaRadius = arenaRadius;
            bot.GrabPoint = grab.transform;
            bot.EyePoint = eye.transform;
            bot.HeadAnchor = head.transform;
            bot.ModelRoot = model.transform;
            bot.Accent = accent;
            go.AddComponent<BotAnimator>();

            var rev = go.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Weapon;
            rev.Highlight = true;
            rev.Label = "SHOOT TO STUN";
            rev.LabelOffset = new Vector3(0f, 2.4f, 0f);
            return bot;
        }

        /// <summary>A detention cage (bars, roof, sliding door, "DETENTION" sign, status lights + lamp).
        /// The door faces local +Z; bots hand prisoners over at IntakePoint ~1.9 m in front of it.
        /// Wire a lever/button to it with cell.SetInputs(...) (or Inputs before the scene is saved).</summary>
        public static HoldingCell HoldingCell(Transform parent, Vector3 pos, Quaternion rot, string id)
        {
            float W = CellWidth, D = CellDepth, H = CellHeight, dw = CellDoorWidth;
            var go = Kit.Make("HoldingCell_" + id, parent, pos, rot);
            var t = go.transform;

            // floor, back wall
            Kit.SolidBox(t, "Floor", new Vector3(0f, 0.06f, 0.1f), new Vector3(W + 0.5f, 0.12f, D + 0.7f), MatKey.MetalDark, 0.03f);
            Kit.SolidBox(t, "BackWall", new Vector3(0f, H * 0.5f, -D * 0.5f - 0.125f), new Vector3(W + 0.5f, H, 0.25f), MatKey.MetalDark, 0.04f);
            Kit.BoxVisual(t, "BackStripe", new Vector3(0f, 0.5f, -D * 0.5f + 0.01f), new Vector3(W, 0.12f, 0.04f), MatKey.HazardYellow, 0.01f);

            // side bars + roof bars (invisible solid colliders behind the bars)
            var barsMat = Kit.Mat(MatKey.MetalLight);
            for (int s = -1; s <= 1; s += 2)
            {
                var side = Kit.Make(s < 0 ? "BarsLeft" : "BarsRight", t, new Vector3(s * (W * 0.5f + 0.06f), 0.12f, 0f), Quaternion.Euler(0f, 90f, 0f));
                ProcMesh.AddRenderer(side, BarsPanel(D + 0.2f, H - 0.12f, 0.32f, 0.035f), barsMat);
                var sc = Kit.Make("Collider", side.transform, Vector3.zero).AddComponent<BoxCollider>();
                sc.center = new Vector3(0f, (H - 0.12f) * 0.5f, 0f);
                sc.size = new Vector3(D + 0.25f, H - 0.12f, 0.14f);
            }
            var roof = Kit.Make("RoofBars", t, new Vector3(0f, H, -D * 0.5f - 0.1f), Quaternion.Euler(90f, 0f, 0f));
            ProcMesh.AddRenderer(roof, BarsPanel(W + 0.3f, D + 0.25f, 0.4f, 0.03f), barsMat);
            var rc = Kit.Make("RoofCollider", t, new Vector3(0f, H + 0.05f, 0f)).AddComponent<BoxCollider>();
            rc.size = new Vector3(W + 0.45f, 0.1f, D + 0.45f);

            // front: side panels, lintel with sign, status lights
            float panelW = (W + 0.5f - dw) * 0.5f;
            float frontZ = D * 0.5f + 0.06f;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.SolidBox(t, s < 0 ? "PillarLeft" : "PillarRight", new Vector3(s * (dw * 0.5f + panelW * 0.5f), H * 0.5f, frontZ), new Vector3(panelW, H, 0.24f), MatKey.MetalDark, 0.04f);
                Kit.BoxVisual(t, s < 0 ? "StripeLeft" : "StripeRight", new Vector3(s * (dw * 0.5f + 0.05f), H * 0.5f, frontZ + 0.13f), new Vector3(0.1f, H, 0.03f), MatKey.HazardYellow, 0.01f);
            }
            Kit.SolidBox(t, "Lintel", new Vector3(0f, H + 0.3f, frontZ), new Vector3(W + 0.5f, 0.6f, 0.3f), MatKey.MetalDark, 0.05f);
            Kit.BoxVisual(t, "LintelStripe", new Vector3(0f, H + 0.03f, frontZ + 0.01f), new Vector3(W + 0.52f, 0.07f, 0.33f), MatKey.HazardYellow, 0.01f);
            Sign(t, "Sign", "DETENTION", new Vector3(0f, H + 0.31f, frontZ + 0.16f), 4.2f, UIColors.AccentYellow, new Vector2(W, 0.6f));
            Sign(t, "InsideSign", "THINK ABOUT WHAT YOU DID", new Vector3(0f, 2.3f, -D * 0.5f + 0.02f), 2f, UIColors.Hex(0xE4E7EC), new Vector2(W - 0.2f, 0.5f));
            var lightL = Kit.BoxVisual(t, "StatusLightL", new Vector3(-(W * 0.5f - 0.05f), H + 0.3f, frontZ + 0.17f), new Vector3(0.2f, 0.2f, 0.06f), MatKey.LightRed, 0.03f, null, false);
            var lightR = Kit.BoxVisual(t, "StatusLightR", new Vector3(W * 0.5f - 0.05f, H + 0.3f, frontZ + 0.17f), new Vector3(0.2f, 0.2f, 0.06f), MatKey.LightRed, 0.03f, null, false);

            var lampGo = Kit.Make("Lamp", t, new Vector3(0f, H + 1.0f, frontZ + 0.8f));
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 5.5f;
            lamp.intensity = 1.6f;
            lamp.color = UIColors.Danger;
            lamp.shadows = LightShadows.None;

            // door (kinematic body so the moving collider behaves)
            var door = Kit.Make("Door", t, new Vector3(0f, 0f, frontZ));
            var bars = Kit.Make("Bars", door.transform, new Vector3(0f, 0.12f, 0f));
            ProcMesh.AddRenderer(bars, BarsPanel(dw, H - 0.12f, 0.26f, 0.045f), Kit.Mat(MatKey.MetalDark));
            Kit.BoxVisual(door.transform, "FrameL", new Vector3(-dw * 0.5f + 0.05f, 0.12f + (H - 0.12f) * 0.5f, 0f), new Vector3(0.1f, H - 0.12f, 0.14f), MatKey.PuzzleBody, 0.02f);
            Kit.BoxVisual(door.transform, "FrameR", new Vector3(dw * 0.5f - 0.05f, 0.12f + (H - 0.12f) * 0.5f, 0f), new Vector3(0.1f, H - 0.12f, 0.14f), MatKey.PuzzleBody, 0.02f);
            Kit.BoxVisual(door.transform, "Lock", new Vector3(dw * 0.5f - 0.3f, 1.2f, 0.09f), new Vector3(0.22f, 0.3f, 0.08f), MatKey.PuzzleAccent, 0.02f);
            var dc = door.AddComponent<BoxCollider>();
            dc.center = new Vector3(0f, 0.12f + (H - 0.12f) * 0.5f, 0f);
            dc.size = new Vector3(dw, H - 0.12f, 0.14f);
            var drb = door.AddComponent<Rigidbody>();
            drb.isKinematic = true;
            drb.interpolation = RigidbodyInterpolation.Interpolate;

            // anchors
            var drop = Kit.Make("DropPoint", t, new Vector3(0f, 0.14f, -0.3f));
            var intake = Kit.Make("IntakePoint", t, new Vector3(0f, 0f, D * 0.5f + 1.9f));
            Kit.BoxVisual(t, "IntakeMark", new Vector3(0f, 0.01f, D * 0.5f + 1.9f), new Vector3(1.8f, 0.02f, 1.4f), MatKey.HazardYellow, 0f, null, false);
            Kit.BoxVisual(t, "IntakeMarkInner", new Vector3(0f, 0.02f, D * 0.5f + 1.9f), new Vector3(1.5f, 0.02f, 1.1f), MatKey.HazardBlack, 0f, null, false);

            var cell = go.AddComponent<Shifters.HoldingCell>();
            cell.Id = id;
            cell.Door = door.transform;
            cell.DoorClosedLocal = door.transform.localPosition;
            cell.DoorOpenHeight = H - 0.1f;
            cell.DropPoint = drop.transform;
            cell.IntakePoint = intake.transform;
            cell.InteriorHalfExtents = new Vector3(W * 0.5f, H * 0.5f, D * 0.5f);
            cell.StatusLights = new Renderer[] { lightL, lightR };
            cell.Lamp = lamp;
            return cell;
        }

        /// <summary>Vertical bars panel in the local XY plane: x centered, y from 0 to height. Cached per size.</summary>
        static Mesh BarsPanel(float width, float height, float spacing, float radius)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "cell_bars_{0:0.##}x{1:0.##}_s{2:0.##}_r{3:0.###}", width, height, spacing, radius);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                int n = Mathf.Max(2, Mathf.RoundToInt(width / spacing));
                for (int i = 0; i <= n; i++)
                {
                    float x = -width * 0.5f + width * i / n;
                    b.AddCylinder(new Vector3(x, height * 0.5f, 0f), radius, height, Quaternion.identity, Color.white, 6, true);
                }
                b.AddBox(new Vector3(0f, 0.05f, 0f), new Vector3(width + 0.08f, 0.1f, 0.1f), Quaternion.identity, Color.white, 0.02f);
                b.AddBox(new Vector3(0f, height - 0.05f, 0f), new Vector3(width + 0.08f, 0.1f, 0.1f), Quaternion.identity, Color.white, 0.02f);
                b.AddBox(new Vector3(0f, height * 0.55f, 0f), new Vector3(width + 0.04f, 0.07f, 0.08f), Quaternion.identity, Color.white, 0.02f);
                return b.ToMesh(key);
            });
        }

        /// <summary>World-space TextMeshPro sign readable from the local +Z side.</summary>
        static void Sign(Transform parent, string name, string text, Vector3 pos, float size, Color color, Vector2 box)
        {
            var go = Kit.Make(name, parent, pos, Quaternion.Euler(0f, 180f, 0f));
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = box;
        }
    }
}
