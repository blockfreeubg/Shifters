// =====================================================================================
//  SHIFTERS - L1Builder.Dressing.cs   (LEVEL 1: void backdrop, floating billboards, safety nets)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L1Builder
    {
        void BuildDressing()
        {
            var ctx = _ctx;
            var d = ctx.Decor;
            var bounds = new Bounds(V(6f, 0f, 122f), V(80f, 20f, 280f));
            EnvKit.VoidBackdrop(d, LevelTheme.Training, bounds, 11);

            // floating corporate billboards in the void (visible from several sections)
            Billboard(d, V(-30f, 9f, 20f), 75f, "SHIFTERS INC.\nSHIFT HAPPENS.");
            Billboard(d, V(32f, 11f, 95f), -70f, "NOW HIRING: ALL SHIFTS\n(AGAIN)");
            Billboard(d, V(-40f, 12f, 160f), 80f, "REMEMBER: THE FOAM PIT IS NOT A BREAK ROOM");
            Billboard(d, V(30f, 10f, 225f), -75f, "ALMOST THERE.\nPROBABLY.");

            // party bunting + lane color coding (friendly first impression)
            Bunting(d, V(-8.8f, 5f, 4.6f), V(8.8f, 5f, 4.6f), 0.8f);
            Bunting(d, V(-8.8f, 4.6f, 2.5f), V(8.8f, 4.6f, 2.5f), 0.9f);
            Bunting(d, V(-8.8f, 5.2f, 53f), V(8.8f, 5.2f, 53f), 1f);
            Bunting(d, V(-11.8f, 5.6f, 226f), V(11.8f, 5.6f, 226f), 1.2f);
            Bunting(d, V(-11.8f, 5.6f, 234f), V(11.8f, 5.6f, 234f), 1.2f);
            Bunting(d, V(-11.8f, 5.6f, 242f), V(11.8f, 5.6f, 242f), 1.1f);
            FloorDisc(d, V(-6f, 0f, LaneZ), 1.6f, UIColors.Strength, "STR", -90f);
            FloorDisc(d, V(-6f, 0f, IqZ), 1.6f, UIColors.Intellect, "IQ", -90f);
            FloorDisc(d, V(6f, 0f, LaneZ), 1.6f, UIColors.Speed, "SPD", 90f);

            // safety nets: nobody is ever lost in the tutorial (each net returns to its section start)
            const float netY = -9f;
            L1SafetyNet.Build(ctx.Hazards, V(0f, netY, 8f), V(60f, 6f, 54f), V(0f, 0.3f, -2f));
            L1SafetyNet.Build(ctx.Hazards, V(0f, netY, 43f), V(60f, 6f, 16f), V(0f, 0f, 36f));
            L1SafetyNet.Build(ctx.Hazards, V(6f, netY, 63f), V(120f, 6f, 26f), V(0f, 0f, 56f));
            L1SafetyNet.Build(ctx.Hazards, V(0f, netY, 90f), V(60f, 6f, 28f), V(0f, 0f, 80f));
            L1SafetyNet.Build(ctx.Hazards, V(0f, netY, 127f), V(60f, 6f, 46f), V(0f, 0f, 104f));
            L1SafetyNet.Build(ctx.Hazards, V(-6f, netY, 163f), V(80f, 6f, 26f), V(0f, 0f, 150f));
            L1SafetyNet.Build(ctx.Hazards, V(-6f, netY, 199f), V(80f, 6f, 46f), V(0f, 0f, 182f));
            L1SafetyNet.Build(ctx.Hazards, V(0f, netY, 241f), V(70f, 6f, 38f), V(0f, 0f, 224f));
        }

        static readonly Color[] BuntingColors =
        {
            new Color(1f, 0.54f, 0.12f), new Color(0.24f, 0.88f, 1f), new Color(1f, 0.79f, 0.24f),
            new Color(0.69f, 0.49f, 1f), new Color(0.24f, 0.86f, 0.59f), new Color(1f, 0.3f, 0.37f)
        };

        /// <summary>Party bunting: a sagging string of colorful pennants from a to b (visual only, no shadows).</summary>
        void Bunting(Transform p, Vector3 a, Vector3 b, float sag = 0.7f)
        {
            var root = Kit.Make("Bunting", p, Vector3.zero);
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.75f));
            var dir = (b - a) / len;
            var face = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                var pos = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                if (i < n)
                {
                    var next = Vector3.Lerp(a, b, (i + 1f) / n) + Vector3.down * (sag * 4f * ((i + 1f) / n) * (1f - (i + 1f) / n));
                    var seg = next - pos;
                    Kit.BoxVisual(root.transform, "String", (pos + next) * 0.5f, V(0.02f, 0.02f, seg.magnitude), MatKey.MetalDark, 0f, Quaternion.LookRotation(seg.normalized, Vector3.up), false);
                }
                if (i == 0 || i == n) continue;
                var mat = MaterialLibrary.Colored(BuntingColors[i % BuntingColors.Length], 0.2f);
                var flag = Kit.Make("Pennant", root.transform, pos + Vector3.down * 0.2f, face * Quaternion.Euler(0f, 0f, 45f));
                ProcMesh.AddRenderer(flag, ProcMesh.CachedBox(V(0.32f, 0.32f, 0.015f), 0f), mat, false);
            }
        }

        /// <summary>Colored floor disc with a label (lane / zone color coding, visual only).</summary>
        void FloorDisc(Transform p, Vector3 center, float radius, Color color, string label, float yaw)
        {
            var disc = Kit.Make("FloorDisc", p, center + Vector3.up * 0.008f);
            ProcMesh.AddRenderer(disc, ProcMesh.Cached("l1_disc_" + radius.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), () => ProcMesh.Cylinder(radius, 0.012f, 32)), MaterialLibrary.Colored(color, 0.3f, 0.15f), false);
            if (!string.IsNullOrEmpty(label)) FloorText(p, center + Vector3.up * 0.01f, yaw, label, 0.42f, Color.white, radius * 1.8f);
        }

        /// <summary>Huge floating ad panel in the void (no collider).</summary>
        void Billboard(Transform d, Vector3 pos, float yaw, string text)
        {
            var root = Kit.Make("Billboard", d, pos, Yaw(yaw));
            Kit.BoxVisual(root.transform, "Post", V(0f, -6f, 0.3f), V(0.6f, 9f, 0.6f), MatKey.MetalDark, 0.05f);
            Sign(root.transform, Vector3.zero, 0f, text, 14f, 4f, SignStyle.Info);
            Kit.BoxVisual(root.transform, "Trim", V(0f, -2.15f, 0.02f), V(14.2f, 0.25f, 0.12f), MatKey.SafetyOrange, 0.02f);
        }
    }
}
