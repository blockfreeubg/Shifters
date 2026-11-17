// =====================================================================================
//  SHIFTERS - BossModelsNight.cs   [owned by VISUALS_BOSSES]
//  BossModels.Boogeyman (CH11: 6 m shadow, door-sized eyes) and BossModels.ScareOMatic (the floor buffer in a bedsheet).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class BossModels
    {
        /// <summary>THE BOOGEYMAN: 6 m shadow silhouette (unlit near-black, no shadows cast, no colliders) with two door-sized
        /// glowing eyes (0.95 x 1.9 m each). Root on the floor, face +Z. SetEyesOpen / SetAngry / SetPose for the tells.</summary>
        public static BoogeymanModel Boogeyman(Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            var go = Root("Boogeyman", parent, pos, rot);
            var t = go.transform;
            var m = go.AddComponent<BoogeymanModel>();
            var shadow = MaterialLibrary.Unlit(new Color(0.018f, 0.016f, 0.03f), 1f);

            var body = Node("Body", t, Vector3.zero);
            ProcMesh.Part("Robe", body, VisualMeshes.Lathe("vb_boogey_robe", new[]
            {
                new Vector4(0.35f, 1.65f, 1.25f, 0f), new Vector4(0.9f, 1.42f, 1.05f, 0f), new Vector4(1.9f, 1.12f, 0.82f, 0f),
                new Vector4(2.9f, 1.02f, 0.76f, 0f), new Vector4(3.5f, 1.3f, 0.88f, 0f), new Vector4(3.85f, 1.0f, 0.72f, 0f),
                new Vector4(4.05f, 0.55f, 0.45f, 0f), new Vector4(4.15f, 0f, 0f, 0f)
            }, 2f, 22), shadow, Vector3.zero, null, null, false);
            P("Tatters", body, "boogey_tatters", b =>
            {
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f;
                    float len = 0.35f + 0.25f * ((i * 7) % 3);
                    b.AddCone(new Vector3(Mathf.Sin(a) * 1.5f, 0.38f, Mathf.Cos(a) * 1.12f), 0.32f, len, Quaternion.Euler(180f, 0f, 0f), W, 6);
                }
            }, shadow, default, null, false);

            var head = Node("Head", body, new Vector3(0f, 4.75f, 0f));
            P("Skull", head, "boogey_head", b =>
            {
                b.AddSphere(Vector3.zero, 1f, Quaternion.identity, W, 20, 12, true, new Vector3(1.5f, 1.4f, 1.1f));
                b.AddCone(new Vector3(-0.95f, 0.95f, 0f), 0.32f, 0.9f, Quaternion.Euler(0f, 0f, 28f), W, 8);   // horn-ears
                b.AddCone(new Vector3(0.95f, 0.95f, 0f), 0.32f, 0.9f, Quaternion.Euler(0f, 0f, -28f), W, 8);
            }, shadow, default, null, false);

            m.EyeCalm = Glow(new Color(1f, 0.93f, 0.55f), 2.6f);
            m.EyeAngry = Glow(new Color(1f, 0.34f, 0.14f), 3.2f);
            var pupil = MaterialLibrary.Unlit(new Color(0.02f, 0.02f, 0.03f), 1f);
            var eyeRs = new Renderer[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Node(s < 0 ? "EyeL" : "EyeR", head, new Vector3(s * 0.74f, 0.22f, 0.93f), Quaternion.Euler(0f, s * 14f, -s * 10f));
                var er = P("Glow", eye, "boogey_eye", b => b.AddSphere(Vector3.zero, 1f, Quaternion.identity, W, 16, 10, true, new Vector3(0.475f, 0.95f, 0.16f)), m.EyeCalm, default, null, false);
                P("Pupil", eye, "boogey_pupil", b => b.AddSphere(new Vector3(-s * 0.06f, -0.12f, 0.1f), 1f, Quaternion.identity, W, 10, 8, true, new Vector3(0.11f, 0.55f, 0.08f)), pupil, default, null, false);
                eyeRs[s < 0 ? 0 : 1] = er.GetComponent<Renderer>();
                if (s < 0) m.EyeL = eye; else m.EyeR = eye;
            }
            m.EyeRenderers = eyeRs;

            var grin = Node("Grin", head, new Vector3(0f, -0.95f, 0.72f));
            P("Teeth", grin, "boogey_grin", b =>
            {
                for (int i = 0; i < 11; i++)
                {
                    float u = (i - 5) / 5f;
                    var p = new Vector3(u * 0.95f, 0.18f * u * u, 0.1f - 0.25f * u * u);
                    b.AddCone(p + new Vector3(0f, 0.05f, 0f), 0.08f, 0.22f, Quaternion.Euler(180f, 0f, 0f), W, 5);
                    if (i % 2 == 1) b.AddCone(p + new Vector3(0f, -0.2f, 0f), 0.07f, 0.18f, Quaternion.identity, W, 5);
                }
            }, Glow(new Color(0.95f, 0.95f, 0.85f), 1.6f), default, null, false);

            for (int s = -1; s <= 1; s += 2)
            {
                var arm = Node(s < 0 ? "ArmL" : "ArmR", body, new Vector3(s * 1.2f, 3.55f, 0f));
                P("Limb", arm, "boogey_arm", b =>
                {
                    b.AddCapsule(new Vector3(0f, -1.4f, 0f), 0.22f, 2.9f, Quaternion.identity, W, 10, 3);
                    b.AddSphere(new Vector3(0f, -2.95f, 0.05f), 0.3f, Quaternion.identity, W, 10, 6, true, new Vector3(1f, 0.8f, 0.7f));
                    for (int f = 0; f < 4; f++)
                    {
                        float fx = (f - 1.5f) * 0.13f;
                        b.AddCone(new Vector3(fx, -3.1f, 0.08f), 0.06f, 0.75f, Quaternion.Euler(180f + (f - 1.5f) * 4f, 0f, (f - 1.5f) * 8f), W, 5);
                    }
                }, shadow, default, null, false);
                if (s < 0) m.ArmL = arm; else m.ArmR = arm;
            }

            m.Body = body; m.Head = head; m.Grin = grin;
            m.SetEyesOpen(1f);
            m.SetAngry(false);
            m.SetPose(0f, 0f);
            return Finish(m, "Boogeyman");
        }

        /// <summary>SCARE-O-MATIC 3000: an industrial floor buffer (pad r 0.45 m) wearing a 1.6 m ghost bedsheet with eye
        /// holes; its projector lens pokes out of the sheet's front (+Z). No colliders.</summary>
        public static ScareOMaticModel ScareOMatic(Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            var go = Root("ScareOMatic", parent, pos, rot);
            var t = go.transform;
            var m = go.AddComponent<ScareOMaticModel>();
            var housing = C(new Color(0.5f, 0.55f, 0.62f), 0.5f);
            var dark = C(SteelDark, 0.4f);

            var buffer = Node("Buffer", t, Vector3.zero);
            P("Housing", buffer, "scare_housing", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.2f, 0f), 0.43f, 0.26f, Quaternion.identity, W, 20, true);
                b.AddSphere(new Vector3(0f, 0.36f, -0.05f), 0.28f, Quaternion.identity, W, 14, 8, true, new Vector3(1f, 0.6f, 1f));
            }, housing);
            P("Bumper", buffer, "scare_bumper", b =>
            {
                b.AddTorus(new Vector3(0f, 0.1f, 0f), 0.44f, 0.04f, Quaternion.identity, W, 20, 5);
                b.AddCylinder(new Vector3(-0.3f, 0.09f, -0.4f), 0.08f, 0.06f, AlongX, W, 10, true);   // wheels
                b.AddCylinder(new Vector3(0.3f, 0.09f, -0.4f), 0.08f, 0.06f, AlongX, W, 10, true);
            }, dark);
            var brush = Node("Brush", buffer, new Vector3(0f, 0.035f, 0f));
            P("Pad", brush, "scare_pad", b => b.AddCylinder(Vector3.zero, 0.45f, 0.06f, Quaternion.identity, W, 20, true), C(new Color(0.15f, 0.25f, 0.6f), 0.2f));
            P("PadMark", brush, "scare_padmark", b => b.AddBox(new Vector3(0.3f, 0.0f, 0f), new Vector3(0.24f, 0.065f, 0.06f), Quaternion.identity, W, 0f), C(HazardY, 0.3f));
            var plate = Node("Nameplate", buffer, new Vector3(0f, 0.2f, 0.435f), Face(Vector3.forward));
            EnvKit.AddText(plate, "SCARE-O-MATIC 3000", Vector3.zero, new Vector2(0.6f, 0.12f), Paper, ShiftersFonts.Display, 0.1f);

            var handle = Node("Handle", t, new Vector3(0f, 0.35f, -0.2f));
            P("Pole", handle, "scare_pole", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.48f, 0f), 0.03f, 0.96f, Quaternion.identity, W, 8, true);
                b.AddCylinder(new Vector3(0f, 0.95f, 0f), 0.025f, 0.5f, AlongX, W, 8, true);
            }, dark);

            var proj = Node("Projector", t, new Vector3(0f, 0.62f, 0.32f));
            P("ProjBody", proj, "scare_proj", b =>
            {
                b.AddBox(Vector3.zero, new Vector3(0.3f, 0.22f, 0.3f), Quaternion.identity, W, 0.03f);
                b.AddCylinder(new Vector3(0f, 0f, 0.24f), 0.1f, 0.2f, AlongZ, W, 14, true);
            }, dark);
            m.LensOff = C(new Color(0.3f, 0.35f, 0.4f), 0.9f);
            m.LensOn = Glow(new Color(0.85f, 0.95f, 1f), 3f);
            var lens = P("Lens", proj, "scare_lens", b => b.AddCylinder(new Vector3(0f, 0f, 0.345f), 0.08f, 0.012f, AlongZ, W, 14, true), m.LensOn, default, null, false);
            m.ProjectorLens = lens.GetComponent<Renderer>();

            var sheet = Node("Sheet", t, Vector3.zero);
            ProcMesh.Part("Bedsheet", sheet, VisualMeshes.Lathe("vb_scare_sheet", new[]
            {
                new Vector4(0.36f, 0.64f, 0.62f, 0f), new Vector4(0.6f, 0.57f, 0.55f, 0f), new Vector4(0.95f, 0.49f, 0.47f, 0f),
                new Vector4(1.25f, 0.42f, 0.4f, 0f), new Vector4(1.47f, 0.3f, 0.29f, 0f), new Vector4(1.6f, 0.13f, 0.13f, 0f),
                new Vector4(1.63f, 0f, 0f, 0f)
            }, 2f, 20), C(new Color(0.96f, 0.96f, 0.94f), 0.15f), Vector3.zero);
            P("Folds", sheet, "scare_folds", b =>
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                    b.AddCapsule(new Vector3(Mathf.Sin(a) * 0.55f, 0.55f, Mathf.Cos(a) * 0.53f), 0.06f, 0.5f, Quaternion.Euler(Mathf.Cos(a) * 10f, 0f, -Mathf.Sin(a) * 10f), W, 6, 2);
                }
            }, C(new Color(0.9f, 0.9f, 0.88f), 0.15f));
            var holes = Node("EyeHoles", sheet, new Vector3(0f, 1.22f, 0f));
            P("Holes", holes, "scare_holes", b =>
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * 0.13f, z = Mathf.Sqrt(0.41f * 0.41f - x * x);
                    b.AddSphere(new Vector3(x, 0f, z), 1f, Quaternion.Euler(0f, s * 18f, 0f), W, 10, 6, true, new Vector3(0.065f, 0.1f, 0.03f));
                }
            }, MaterialLibrary.Unlit(new Color(0.02f, 0.02f, 0.03f), 1f), default, null, false);

            m.Buffer = buffer; m.Brush = brush; m.Handle = handle; m.Projector = proj; m.Sheet = sheet; m.Nameplate = plate;
            return Finish(m, "ScareOMatic");
        }
    }
}
