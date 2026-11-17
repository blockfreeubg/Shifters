// =====================================================================================
//  SHIFTERS - L8Builder.cs   [SHIFT 07 - THE FEDERAL COLOR RESERVE (L8_ColorReserve, idx 7)]
//  Heist level (Bank): S0 vault antechamber (SWAP-O-MATIC locker, tip jar) -> S1 laser gallery
//  (turbo curtain / swap with the deposit box) -> C1 -> S2 vault door rotunda (Lock A tumblers,
//  Lock B three-key turn, Lock C shot code) -> C2 -> S3 the Reserve (the swap: pedestal + prism ->
//  master valve -> C06 color explosion) -> exit "GETAWAY TRAM - PLATFORM 1".
//  Spec: docs/campaign/LEVELS2.md "SHIFT 07". Deviations (documented in the report): rotunda wall
//  radius 16 (spec 15: the spec's pallet start sat on the wall), vault door centre y 3.4 (walkable
//  chord), K3 lip widened west for a turbo run-up, door swings into the vestibule.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public partial class L8Builder : ILevelBuilder
    {
        public int LevelIndex => 7;

        // ---- metrics (bots and tests read these)
        public const float RouteLength = 28f;
        public static readonly float[] CurtainZ = { 24f, 32f, 40.5f };
        public static readonly Vector3 RotundaCenter = new Vector3(0f, 0f, 70f);
        public const float RotundaR = 16f;
        public static readonly Vector3 ReserveCenter = new Vector3(0f, 0f, 104f);
        public const float ReserveR = 10f;
        public const float DoorZ = 85.2f, DoorCenterY = 3.4f, DoorDiameter = 8f;
        public const float KeySeconds = 5f;

        LevelBuildContext _ctx;
        int _lights;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            _lights = 0;
            BuildS0(ctx);
            BuildS1(ctx);
            BuildS2(ctx);
            BuildS3(ctx);
            EnvKit.VoidBackdrop(ctx.Decor, LevelTheme.Bank, new Bounds(new Vector3(0f, 0f, 56f), new Vector3(60f, 24f, 150f)), 77);
        }

        // ------------------------------------------------------------------ helpers
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);
        /// <summary>Rot for a piece read by a viewer standing in direction 'towardViewer' (EnvKit convention).</summary>
        static Quaternion Face(Vector3 towardViewer) => EnvKit.FacingViewer(towardViewer);
        static Quaternion FaceAt(Vector3 piece, Vector3 viewer) => EnvKit.FacingViewerAt(piece, viewer);

        BoxCollider Floor(Transform p, float x0, float x1, float z0, float z1, float top = 0f, MatKey mat = MatKey.Floor, bool trim = true, string name = "Floor")
            => _ctx.Platform(p, V((x0 + x1) * 0.5f, top, (z0 + z1) * 0.5f), V(x1 - x0, 0.6f, z1 - z0), mat, trim, name);

        BoxCollider Solid(Transform p, float x0, float x1, float z0, float z1, float top, float bottom = 0f, MatKey mat = MatKey.FloorAlt, string name = "Block")
            => _ctx.Block(p, V((x0 + x1) * 0.5f, (top + bottom) * 0.5f, (z0 + z1) * 0.5f), V(x1 - x0, top - bottom, z1 - z0), mat, name);

        GameObject Wall(Transform p, float x0, float z0, float x1, float z1, float h, MatKey mat = MatKey.Wall, bool accent = true)
            => EnvKit.Wall(p, V(x0, 0f, z0), V(x1, 0f, z1), h, 0.4f, mat, accent);

        /// <summary>Polygon ring wall (segment midpoints at -90 + offset + k*step degrees); skip(midDeg) removes segments.</summary>
        void RingWall(Transform p, Vector3 c, float r, int segs, float h, System.Func<float, bool> skip, MatKey mat = MatKey.Wall)
        {
            float step = 360f / segs;
            for (int k = 0; k < segs; k++)
            {
                float mid = -90f + step * 0.5f + k * step;
                float m = Mathf.Repeat(mid + 180f, 360f) - 180f;
                if (skip != null && skip(m)) continue;
                float a0 = (mid - step * 0.5f) * Mathf.Deg2Rad, a1 = (mid + step * 0.5f) * Mathf.Deg2Rad;
                var p0 = c + V(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r;
                var p1 = c + V(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
                var d = (p1 - p0).normalized * 0.06f;   // slight overlap so the corners close
                EnvKit.Wall(p, p0 - d, p1 + d, h, 0.4f, mat, true);
            }
        }

        static Vector3 OnCircle(Vector3 c, float r, float deg, float y = 0f)
            => c + V(Mathf.Cos(deg * Mathf.Deg2Rad) * r, y, Mathf.Sin(deg * Mathf.Deg2Rad) * r);

        GameObject Sign(Transform p, Vector3 pos, Vector3 towardViewer, string text, float w, float h, SignStyle style = SignStyle.Info)
            => EnvKit.Sign(p, pos, Face(towardViewer), text, new Vector2(w, h), style);

        GameObject Panel(Transform p, Vector3 pos, Vector3 towardViewer, Color c, bool wantLight = false, float intensity = 2.4f, float range = 14f, float w = 1.8f, float h = 0.45f)
        {
            bool light = wantLight && _lights < 4;
            if (light) _lights++;
            return EnvKit.LightPanel(p, pos, Face(towardViewer), new Vector2(w, h), c, light, intensity, range);
        }

        /// <summary>Flat floor label (reads for someone walking toward +Z unless yaw is given).</summary>
        TMPro.TextMeshPro FloorLabel(Transform p, Vector3 pos, string text, float height, Color color, float yaw = 0f, float maxWidth = 4f)
            => _ctx.Label(p, pos + Vector3.up * 0.02f, Quaternion.Euler(90f, yaw, 0f), text, height, color, maxWidth);

        void Disc(Transform p, Vector3 pos, float radius, MatKey mat, string name = "Disc")
            => PuzzleVisuals.Cyl(p, name, pos + Vector3.up * 0.008f, radius, 0.016f, mat, null, 24, false);

        L8Node Node(string id, SignalLogic logic, string beat, params SignalSource[] inputs)
        {
            var go = Kit.Make("L8Node_" + id, _ctx.Puzzles, Vector3.zero);
            var n = go.AddComponent<L8Node>();
            n.Logic = logic; n.BeatId = beat ?? "";
            n.SetInputs(inputs);
            return _ctx.Mark(id, n);
        }

        /// <summary>Re-skins a kit crate: hides its crate mesh and adds custom child visuals (no colliders).</summary>
        static Grabbable Reskin(Rigidbody rb, string display, Mesh mesh, Material mat, Vector3 localPos, Vector3? scale = null)
        {
            var mr = rb.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            ProcMesh.Part("Look", rb.transform, mesh, mat, localPos, null, scale, true);
            var g = rb.GetComponent<Grabbable>();
            if (g != null) g.DisplayName = display;
            return g;
        }

        static Color Gold => UIColors.Hex(0xFFC83D);
        static Color VaultRed => UIColors.Hex(0xFF4D5E);
        static Color Cream => UIColors.Hex(0xFFF1CF);
        static Color Teal => UIColors.Hex(0x4DE3D0);
    }
}
