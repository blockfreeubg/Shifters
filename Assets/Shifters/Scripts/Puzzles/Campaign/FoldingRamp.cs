// =====================================================================================
//  SHIFTERS - FoldingRamp.cs   [PUZZLES - campaign wave; promoted copy of L1FoldingRamp]
//  Hinged "drawbridge" ramp / folding stairs. A kinematic slab stands upright against a ledge face
//  (Raised) and swings down into a walkable slope (Lowered) when powered. Wire it with Latch logic for a
//  one-way unfold ("lowers a ramp for everyone"). Build with FoldingRamp.Build (edit-mode safe).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Hinged ramp receiver: Raised (pitch -90, flat against the ledge face) until powered, then it swings
    /// down to <see cref="LoweredPitch"/> with a heavy landing thump. The walking surface passes through the hinge.</summary>
    public class FoldingRamp : SignalReceiver
    {
        public MoverBody Pivot;
        [Tooltip("Pitch (deg, about the hinge's local X) when unpowered. -90 = standing straight up.")] public float RaisedPitch = -90f;
        [Tooltip("Pitch when powered: atan(height / run).")] public float LoweredPitch = 21f;
        public float Duration = 1.3f;
        [Tooltip("Ramp length along the slope (for the landing puff).")] public float Length = 6.5f;
        public PuzzleLamp Lamp;

        /// <summary>0 = raised, 1 = lowered.</summary>
        public float Position01 => _t;
        public bool IsLowered => _t >= 0.999f;

        float _t;
        bool _target;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            _target = powered;
            if (Lamp != null) Lamp.Set(powered ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            if (initial)
            {
                _t = powered ? 1f : 0f;
                Snap();
                return;
            }
            Sfx.At(SfxId.Lever, transform.position, 0.8f, 0.7f);
            Sfx.At(SfxId.ElevatorHum, transform.position, 0.6f, 0.8f);
        }

        void FixedUpdate()
        {
            float goal = _target ? 1f : 0f;
            if (Pivot == null || Mathf.Approximately(_t, goal)) return;
            _t = Mathf.MoveTowards(_t, goal, Time.fixedDeltaTime / Mathf.Max(0.1f, Duration));
            Pivot.RotateTo(RotationAt(_t));
            if (Mathf.Approximately(_t, goal) && goal > 0.5f) Landed();
        }

        void Landed()
        {
            var end = transform.position + RotationAt(1f) * new Vector3(0f, 0f, Length);
            Sfx.At(SfxId.LandHeavy, end, 1f, 0.7f);
            Sfx.At(SfxId.DoorSlam, end, 0.7f, 1.1f);
            Fx.Puff(end, 2.2f);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.Shake(0.25f, 0.3f);
        }

        /// <summary>World rotation of the pivot at progress t (eased: accelerating fall plus a small rebound).</summary>
        public Quaternion RotationAt(float t)
        {
            float e = t < 0.82f ? (t / 0.82f) * (t / 0.82f) : 1f - 0.07f * Mathf.Sin((t - 0.82f) / 0.18f * Mathf.PI);
            return transform.rotation * Quaternion.Euler(Mathf.LerpUnclamped(RaisedPitch, LoweredPitch, e), 0f, 0f);
        }

        void Snap()
        {
            if (Pivot == null) return;
            var rot = RotationAt(_t);
            Pivot.transform.rotation = rot;
            var rb = Pivot.Body != null ? Pivot.Body : Pivot.GetComponent<Rigidbody>();
            if (rb != null) rb.rotation = rot;
        }

        // ------------------------------------------------------------------ factory
        /// <summary>Builds a folding ramp. hinge = top edge center of the ledge (local to parent); yaw = direction the ramp
        /// runs DOWN toward (0 = +Z); width/run/height in meters. stairs = draw yellow step nosings.</summary>
        public static FoldingRamp Build(Transform parent, Vector3 hinge, float yaw, float width, float run, float height, bool stairs, string foldedLabel, params SignalSource[] inputs)
        {
            float pitch = Mathf.Atan2(height, run) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(run * run + height * height) + 0.25f;
            const float thick = 0.32f;
            var root = Kit.Make(stairs ? "FoldingStairs" : "FoldingRamp", parent, hinge, Quaternion.Euler(0f, yaw, 0f));
            var pivotGo = Kit.Make("Pivot", root.transform, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
            var mover = pivotGo.AddComponent<MoverBody>();
            mover.Ensure();
            var p = pivotGo.transform;
            Kit.SolidBox(p, "Slab", new Vector3(0f, -thick * 0.5f, len * 0.5f), new Vector3(width, thick, len), MatKey.FloorAlt, 0.04f);
            for (int s = -1; s <= 1; s += 2)
                Kit.BoxVisual(p, "Rail", new Vector3(s * (width * 0.5f - 0.07f), 0.07f, len * 0.5f), new Vector3(0.14f, 0.14f, len - 0.1f), MatKey.SafetyOrange, 0.02f);
            if (stairs)
            {
                for (float z = 0.45f; z < len - 0.2f; z += 0.55f)
                    Kit.BoxVisual(p, "Step", new Vector3(0f, 0.012f, z), new Vector3(width - 0.32f, 0.025f, 0.12f), MatKey.HazardYellow, 0.005f, null, false);
            }
            else
            {
                for (float z = 0.8f; z < len - 0.4f; z += 1.6f)
                    Kit.BoxVisual(p, "Chevron", new Vector3(0f, 0.012f, z), new Vector3(width * 0.55f, 0.025f, 0.18f), MatKey.HazardYellow, 0.005f, null, false);
            }
            // label on the underside: faces the players while folded up, hidden under the slope once lowered
            if (!string.IsNullOrEmpty(foldedLabel))
            {
                var label = PuzzleVisuals.Text(p, "FoldedLabel", new Vector3(0f, -thick - 0.14f, len * 0.42f), Quaternion.LookRotation(Vector3.up, Vector3.forward),
                    foldedLabel, 3.2f, UIColors.AccentYellow, width - 0.3f, len * 0.5f);
                // shrink to the slab instead of spilling over its edges
                label.enableAutoSizing = true;
                label.fontSizeMin = 1.2f;
                label.fontSizeMax = 3.2f;
            }
            // underside ribs so the raised slab reads as a mechanism
            Kit.BoxVisual(p, "Rib", new Vector3(0f, -thick - 0.06f, len * 0.5f), new Vector3(0.3f, 0.12f, len - 0.4f), MatKey.MetalDark, 0.02f);
            // hinge barrel + housings
            Kit.BoxVisual(root.transform, "HingeBar", new Vector3(0f, -0.12f, 0.05f), new Vector3(width + 0.3f, 0.22f, 0.22f), MatKey.MetalDark, 0.05f);
            Renderer lampR = null;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.BoxVisual(root.transform, "Housing", new Vector3(s * (width * 0.5f + 0.3f), 0.15f, 0.35f), new Vector3(0.36f, 0.3f, 0.5f), MatKey.PuzzleBody, 0.04f);
                var l = Kit.BoxVisual(root.transform, "Lamp", new Vector3(s * (width * 0.5f + 0.3f), 0.33f, 0.35f), new Vector3(0.18f, 0.06f, 0.18f), PuzzleLampColorMat(), 0.01f, null, false);
                if (lampR == null) lampR = l;
            }
            var ramp = root.AddComponent<FoldingRamp>();
            ramp.Pivot = mover;
            ramp.RaisedPitch = -90f;
            ramp.LoweredPitch = pitch;
            ramp.Length = len;
            ramp.Logic = SignalLogic.Latch;
            ramp.Lamp = lampR != null ? PuzzleVisuals.LampFrom(Kit.Make("Lamps", root.transform, Vector3.zero), PuzzleLampColor.Red, lampR) : null;
            return PuzzleKit.Wire(ramp, inputs);
        }

        static MatKey PuzzleLampColorMat() => MatKey.LightRed;
    }
}
