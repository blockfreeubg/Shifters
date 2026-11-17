using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Small, reusable construction helpers for Level 2 (THE WEIGHT PROBLEM): oversized/recessed weight plates,
    /// a counterweight pulley whose cargo basket starts in a loading pit, the freight elevator assembly and
    /// rectangle-based floor slabs. Everything is built from kit factories (PuzzleKit / EnvKit) and only configures
    /// their public fields, so it works identically at runtime and in the editor scene builder.
    /// </summary>
    public static class L2Parts
    {
        // ------------------------------------------------------------------ geometry
        /// <summary>Floor slab covering the XZ rectangle [x0,x1] x [z0,z1] with its top at y.</summary>
        public static GameObject Rect(Transform parent, float x0, float x1, float z0, float z1, float y, float thickness = 0.6f, MatKey mat = MatKey.Floor, bool trim = true)
        {
            var size = new Vector3(Mathf.Abs(x1 - x0), thickness, Mathf.Abs(z1 - z0));
            var center = new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
            return EnvKit.Floor(parent, center, size, mat, trim);
        }

        /// <summary>Solid block filling [x0,x1] x [y0,y1] x [z0,z1].</summary>
        public static BoxCollider Box(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1, MatKey mat)
        {
            var size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            var center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            return Kit.SolidBox(parent, name, center, size, mat, 0.04f);
        }

        // ------------------------------------------------------------------ plates
        /// <summary>Scales a kit plate's footprint (pad, rims, lamps) by 'scale' in X/Z while keeping its floating display
        /// undistorted. A 1.5x plate is 3 x 3 m: room for a 160 kg block that was pushed a bit off-center, or three interns.</summary>
        public static T Enlarge<T>(T plate, float scale) where T : PressurePlate
        {
            if (plate == null || scale <= 0f) return plate;
            var t = plate.transform;
            t.localScale = new Vector3(scale, 1f, scale);
            var display = t.Find("Display");
            if (display != null) display.localScale = new Vector3(1f / scale, 1f, 1f / scale);
            return plate;
        }

        /// <summary>Kit plate height above its root (pad top). Place the root this far (+2 cm) below a floor to sink the pad
        /// just under floor level: a pushed block then slides over the pad edge instead of snagging on it, and drops in.</summary>
        public const float PlateTop = 0.14f;

        // ------------------------------------------------------------------ pulley
        /// <summary>
        /// Counterweight pulley: PEOPLE platform A (local -X) starts flush with the deck at pos.y, CARGO basket B (local +X)
        /// starts 'cargoPitDepth' below the deck inside a loading pit, so a heavy block shoved off the pit edge drops into
        /// it while the pusher stays safely on the deck. When B outweighs A by more than 10 kg, A rises 'travel' meters
        /// (to the upper deck) and B sinks the same distance.
        /// </summary>
        public static PulleyLift CounterweightLift(Transform parent, Vector3 pos, float separation, float travel, float platformSize, float cargoPitDepth)
        {
            float half = travel * 0.5f;
            var lift = PuzzleKit.PulleyLift(parent, pos, Quaternion.identity, separation, travel, new Vector2(platformSize, platformSize), -half);
            if (lift == null) return null;
            // B = HomeB - s; with s = -half at the start we want B's top at -cargoPitDepth.
            lift.HomeB = new Vector3(separation * 0.5f, -cargoPitDepth - half, 0f);
            if (lift.PlatformB != null) lift.PlatformB.transform.localPosition = lift.HomeB + Vector3.up * half;
            if (lift.RopeB != null) lift.RopeB.localPosition = lift.HomeB + Vector3.up * half;
            return lift;
        }

        // ------------------------------------------------------------------ freight elevator
        /// <summary>Kit elevator car (size x size, top at pos.y when down) rising 'height' meters, with an L2FreightElevator
        /// control pedestal riding on the car (local corner offset), a car WeightSensor and a load display.</summary>
        public static L2FreightElevator FreightElevator(Transform parent, Vector3 pos, float height, float size, float maxKg, Vector3 panelLocal)
        {
            var elev = PuzzleKit.Elevator(parent, pos, Quaternion.identity, height, new Vector2(size, size));
            var car = elev.Body != null ? elev.Body.gameObject : elev.gameObject;
            var carCol = car.GetComponent<BoxCollider>();
            var sensor = car.AddComponent<WeightSensor>();
            sensor.Surfaces = carCol != null ? new Collider[] { carCol } : new Collider[0];

            // control pedestal on the car, facing -Z (riders stand on its -Z side)
            var panelGo = Kit.Make("ControlPanel", car.transform, panelLocal, Quaternion.Euler(0f, 180f, 0f));
            var pt = panelGo.transform;
            Kit.SolidBox(pt, "Pedestal", new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 1.1f, 0.45f), MatKey.PuzzleBody);
            Kit.BoxVisual(pt, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(0.7f, 0.08f, 0.6f), MatKey.MetalDark);
            Kit.BoxVisual(pt, "Face", new Vector3(0f, 1.0f, 0.2f), new Vector3(0.5f, 0.35f, 0.08f), MatKey.Screen, 0.02f, Quaternion.Euler(-25f, 0f, 0f), false);
            Kit.BoxVisual(pt, "BigButton", new Vector3(0f, 1.14f, 0.05f), new Vector3(0.22f, 0.08f, 0.22f), MatKey.WarningRed, 0.03f);
            var lamp = PuzzleVisuals.Lamp(pt, "Lamp", new Vector3(0f, 0.45f, 0.235f), Quaternion.identity, 0.14f);
            var label = PuzzleVisuals.Text(pt, "UpLabel", new Vector3(0f, 0.8f, 0.236f), Quaternion.Euler(0f, 180f, 0f), "UP", 2.2f, UIColors.AccentYellow, 0.45f, 0.25f);
            label.alignment = TextAlignmentOptions.Center;

            var fe = panelGo.AddComponent<L2FreightElevator>();
            fe.MaxKg = maxKg;
            fe.Car = elev;
            fe.Sensor = sensor;
            fe.Lamp = lamp;
            fe.SolvedMessage = "";
            PuzzleKit.Wire(elev, fe);
            return fe;
        }

        /// <summary>Big world-space label (TMP) readable from -Z when rot is identity.</summary>
        public static TextMeshPro Text(LevelBuildContext ctx, Transform parent, Vector3 pos, float yaw, string text, float height, Color color, float width = 12f)
        {
            var tmp = ctx.Label(parent, pos, Quaternion.Euler(0f, yaw, 0f), text, height, color, width);
            if (tmp != null) tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}
