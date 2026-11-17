// =====================================================================================
//  SHIFTERS - BotFallbackModel.cs   [owned by the WEAPONS_ENEMIES agent]
//  Chunky stand-in BONK-BOT model used ONLY when EnemyModelBuilder (VISUALS) produced no
//  renderers. Same child names the animator drives: Body, Eye, Siren, ArmL, ArmR, Wheel, Antenna.
//  Model faces +Z, feet at y = 0, ~1.8 m tall.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class BotFallbackModel
    {
        public static void Build(Transform root, Color accent)
        {
            var shell = MaterialLibrary.Colored(UIColors.Hex(0xE4E7EC), 0.45f);
            var dark = MaterialLibrary.Colored(UIColors.Hex(0x2A2F38), 0.35f);
            var trim = MaterialLibrary.Colored(accent, 0.35f);
            var rubber = Kit.Mat(MatKey.Rubber);
            var eyeMat = MaterialLibrary.Colored(UIColors.Success, 0.2f, 2.5f);
            var sirenMat = MaterialLibrary.Colored(UIColors.Danger, 0.2f, 2f);

            // wheel + fork
            var wheelMesh = ProcMesh.Cached("bot_fb_wheel_x", () =>
                new ProcMesh.Builder().AddCylinder(Vector3.zero, 0.3f, 0.26f, Quaternion.Euler(0f, 0f, 90f), Color.white, 16, true).ToMesh("bot_fb_wheel_x"));
            var wheel = Part(root, "Wheel", wheelMesh, rubber, new Vector3(0f, 0.3f, 0f), Quaternion.identity);
            var hub = ProcMesh.Cached("bot_fb_hub", () => ProcMesh.Cylinder(0.14f, 0.28f, 10));
            Part(wheel.transform, "Hub", hub, trim, Vector3.zero, Quaternion.Euler(0f, 0f, 90f));
            Part(root, "Fork", ProcMesh.CachedBox(new Vector3(0.42f, 0.4f, 0.22f), 0.06f), dark, new Vector3(0f, 0.62f, 0f), Quaternion.identity);

            // body (egg-ish), belly band, visor
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localPosition = Vector3.zero;
            Part(body, "Shell", ProcMesh.Cached("bot_fb_shell", () => ProcMesh.Sphere(0.6f, 14, 10, true)), shell, new Vector3(0f, 1.15f, 0f), Quaternion.identity, new Vector3(1f, 0.92f, 0.95f));
            Part(body, "Band", ProcMesh.Cached("bot_fb_band", () => ProcMesh.Torus(0.6f, 0.07f, 20, 6)), trim, new Vector3(0f, 1.02f, 0f), Quaternion.identity);
            Part(body, "Visor", ProcMesh.CachedBox(new Vector3(0.62f, 0.3f, 0.2f), 0.08f), dark, new Vector3(0f, 1.28f, 0.47f), Quaternion.Euler(-8f, 0f, 0f));
            Part(body, "Badge", ProcMesh.CachedBox(new Vector3(0.22f, 0.12f, 0.04f), 0.02f), Kit.Mat(MatKey.HazardYellow), new Vector3(0f, 0.86f, 0.53f), Quaternion.Euler(18f, 0f, 0f));

            var eye = Part(body, "Eye", ProcMesh.Cached("bot_fb_eye", () => ProcMesh.Sphere(0.13f, 12, 8, true)), eyeMat, new Vector3(0f, 1.29f, 0.58f), Quaternion.identity, new Vector3(1.25f, 1f, 0.6f));
            eye.name = "Eye";

            // siren
            var siren = new GameObject("Siren").transform;
            siren.SetParent(body, false);
            siren.localPosition = new Vector3(0f, 1.68f, 0f);
            Part(siren, "Base", ProcMesh.Cached("bot_fb_siren_base", () => ProcMesh.Cylinder(0.14f, 0.06f, 12)), dark, Vector3.zero, Quaternion.identity);
            Part(siren, "Dome", ProcMesh.Cached("bot_fb_siren_dome", () => ProcMesh.Sphere(0.12f, 10, 6, true)), sirenMat, new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(1f, 1.2f, 1f));
            Part(siren, "Flap", ProcMesh.CachedBox(new Vector3(0.04f, 0.14f, 0.22f), 0.01f), dark, new Vector3(0f, 0.1f, 0f), Quaternion.identity);

            // arms (pivot at shoulder, hanging down -Y)
            BuildArm(body, "ArmL", new Vector3(-0.62f, 1.12f, 0.05f), shell, dark, trim);
            BuildArm(body, "ArmR", new Vector3(0.62f, 1.12f, 0.05f), shell, dark, trim);

            // antenna
            var ant = new GameObject("Antenna").transform;
            ant.SetParent(body, false);
            ant.localPosition = new Vector3(0.22f, 1.62f, -0.12f);
            Part(ant, "Stick", ProcMesh.Cached("bot_fb_ant", () => ProcMesh.Cylinder(0.018f, 0.45f, 6)), dark, new Vector3(0f, 0.22f, 0f), Quaternion.identity);
            Part(ant, "Ball", ProcMesh.Cached("bot_fb_ant_ball", () => ProcMesh.Sphere(0.055f, 8, 6, true)), MaterialLibrary.Colored(accent, 0.2f, 1.5f), new Vector3(0f, 0.47f, 0f), Quaternion.identity);
        }

        static void BuildArm(Transform body, string name, Vector3 shoulder, Material shell, Material dark, Material trim)
        {
            var arm = new GameObject(name).transform;
            arm.SetParent(body, false);
            arm.localPosition = shoulder;
            float side = Mathf.Sign(shoulder.x);
            Part(arm, "Joint", ProcMesh.Cached("bot_fb_joint", () => ProcMesh.Sphere(0.11f, 8, 6, true)), trim, Vector3.zero, Quaternion.identity);
            Part(arm, "Limb", ProcMesh.Cached("bot_fb_limb", () => ProcMesh.Capsule(0.07f, 0.62f, 8, 3)), dark, new Vector3(0.04f * side, -0.32f, 0f), Quaternion.Euler(0f, 0f, 6f * side));
            Part(arm, "Claw", ProcMesh.CachedBox(new Vector3(0.16f, 0.12f, 0.2f), 0.03f), shell, new Vector3(0.07f * side, -0.68f, 0.03f), Quaternion.identity);
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3? scale = null)
            => ProcMesh.Part(name, parent, mesh, mat, pos, rot, scale, true);
    }
}
