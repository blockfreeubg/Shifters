// =====================================================================================
//  SHIFTERS - EnemyModelBuilder.cs   [CONTRACT API - implementation owned by the VISUALS agent]
//  "BONK-BOT 3000" security robot: round body on a single wheel, big glowing eye, siren light,
//  grabby arms, wobbly antenna. Children named for animation: "Body","Eye","Siren","ArmL","ArmR","Wheel","Antenna".
//
//  All seven named parts are DIRECT children of the returned root (Transform.Find works):
//    Wheel   pivot at the axle (0,0.26,0): spin around local X.
//    Body    pivot just above the axle: shell + accent stripe + face plate + angry brow (squash it for bonks).
//    Eye     renderer with a per-bot emissive material instance (use EnemyModelBuilder.SetGlow to recolor).
//    Siren   dome renderer with a per-bot emissive material instance (flash it with SetGlow).
//    ArmL/R  shoulder pivots; rest = arms hang down; rotate local X negative to raise forward, Z to spread.
//    Antenna pivot at its base: wobble with small rotations.
//  Feet/ground at y=0, faces +Z, ~1.6 m tall (siren top), ~0.9 m wide.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class EnemyModelBuilder
    {
        static readonly Quaternion AxisX = Quaternion.Euler(0f, 0f, 90f);   // cylinder Y axis -> X
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static GameObject Build(Transform parent, Color accent)
        {
            accent.a = 1f;
            var root = new GameObject("BotModel");
            root.transform.SetParent(parent, false);
            var t = root.transform;
            var shell = MaterialLibrary.Colored(new Color(0.93f, 0.94f, 0.96f), 0.55f);
            var dark = MaterialLibrary.Colored(new Color(0.17f, 0.18f, 0.22f), 0.5f);
            var rubber = MaterialLibrary.Colored(new Color(0.1f, 0.1f, 0.12f), 0.2f);
            var acc = MaterialLibrary.Colored(accent, 0.45f);

            // ---- Wheel (spins around X)
            var wheel = Pivot("Wheel", t, new Vector3(0f, 0.26f, 0f));
            ProcMesh.AddRenderer(wheel.gameObject, Cached("bot_tire", b =>
            {
                b.AddCylinder(Vector3.zero, 0.25f, 0.17f, AxisX, Color.white, 18, true);
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    b.AddBox(new Vector3(0f, Mathf.Sin(a) * 0.252f, Mathf.Cos(a) * 0.252f), new Vector3(0.18f, 0.035f, 0.06f), Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f), Color.white, 0.008f);
                }
            }), rubber);
            ProcMesh.Part("Hub", wheel, Cached("bot_hub", b =>
            {
                b.AddCylinder(Vector3.zero, 0.13f, 0.19f, AxisX, Color.white, 14, true);
                b.AddBox(Vector3.zero, new Vector3(0.2f, 0.22f, 0.04f), Quaternion.identity, Color.white, 0.01f);
                b.AddBox(Vector3.zero, new Vector3(0.2f, 0.04f, 0.22f), Quaternion.identity, Color.white, 0.01f);
            }), acc, Vector3.zero);

            // ---- Body (shell + stripe + face plate + brow + fork)
            var body = Pivot("Body", t, new Vector3(0f, 0.3f, 0f));
            ProcMesh.AddRenderer(body.gameObject, VisualMeshes.Lathe("bot_shell", new[]
            {
                new Vector4(0.1f, 0f, 0f, 0f), new Vector4(0.1f, 0.2f, 0.2f, 0f), new Vector4(0.2f, 0.33f, 0.33f, 0f), new Vector4(0.4f, 0.42f, 0.41f, 0f),
                new Vector4(0.65f, 0.44f, 0.43f, 0f), new Vector4(0.9f, 0.4f, 0.39f, 0f), new Vector4(1.05f, 0.3f, 0.3f, 0f), new Vector4(1.14f, 0.16f, 0.16f, 0f), new Vector4(1.17f, 0f, 0f, 0f)
            }, 2.2f, 20), shell);
            ProcMesh.Part("Stripe", body, VisualMeshes.Lathe("bot_stripe", new[]
            {
                new Vector4(0.3f, 0f, 0f, 0f), new Vector4(0.3f, 0.395f, 0.385f, 0f), new Vector4(0.4f, 0.435f, 0.425f, 0f), new Vector4(0.48f, 0.448f, 0.438f, 0f), new Vector4(0.48f, 0f, 0f, 0f)
            }, 2.2f, 20), acc, Vector3.zero);
            ProcMesh.Part("FacePlate", body, Cached("bot_face", b =>
            {
                b.AddSphere(new Vector3(0f, 0.78f, 0.33f), 1f, Quaternion.identity, Color.white, 16, 10, true, new Vector3(0.27f, 0.21f, 0.14f));
                // angry V brow + chin grill
                b.AddBox(new Vector3(-0.09f, 0.98f, 0.37f), new Vector3(0.17f, 0.045f, 0.06f), Quaternion.Euler(0f, 0f, -18f), Color.white, 0.012f);
                b.AddBox(new Vector3(0.09f, 0.98f, 0.37f), new Vector3(0.17f, 0.045f, 0.06f), Quaternion.Euler(0f, 0f, 18f), Color.white, 0.012f);
                for (int i = 0; i < 3; i++) b.AddBox(new Vector3(0f, 0.6f - i * 0.035f, 0.45f - i * 0.006f), new Vector3(0.16f - i * 0.03f, 0.014f, 0.03f), Quaternion.identity, Color.white, 0.004f);
                // wheel fork
                b.AddBox(new Vector3(-0.12f, 0.0f, 0f), new Vector3(0.04f, 0.26f, 0.08f), Quaternion.identity, Color.white, 0.01f);
                b.AddBox(new Vector3(0.12f, 0.0f, 0f), new Vector3(0.04f, 0.26f, 0.08f), Quaternion.identity, Color.white, 0.01f);
                b.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.28f, 0.05f, 0.1f), Quaternion.identity, Color.white, 0.01f);
                // shoulder bolts
                b.AddSphere(new Vector3(-0.44f, 0.65f, 0.02f), 0.06f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
                b.AddSphere(new Vector3(0.44f, 0.65f, 0.02f), 0.06f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
            }), dark, Vector3.zero);

            // ---- Eye (per-bot emissive instance) + pupil slit
            var eyeMat = MaterialLibrary.Instance(Color.Lerp(accent, Color.white, 0.25f), 0.8f);
            SetGlow(eyeMat, Color.Lerp(accent, Color.white, 0.25f), 2.2f);
            ProcMesh.Part("Eye", t, VisualMeshes.Ellipsoid(new Vector3(0.14f, 0.13f, 0.07f), 16, 10), eyeMat, new Vector3(0f, 1.08f, 0.43f), null, null, false);
            // sibling (not child) of "Eye": the enemy animator recolors every renderer below "Eye"
            ProcMesh.Part("EyePupil", t, VisualMeshes.Ellipsoid(new Vector3(0.035f, 0.075f, 0.02f), 10, 8), dark, new Vector3(0f, 1.08f, 0.492f), null, null, false);

            // ---- Siren dome (per-bot emissive instance)
            var sirenMat = MaterialLibrary.Instance(new Color(1f, 0.25f, 0.25f), 0.8f);
            SetGlow(sirenMat, new Color(1f, 0.2f, 0.2f), 1.2f);
            ProcMesh.Part("Siren", t, VisualMeshes.Dome(0.11f, 0.13f, 0.11f, 2f, 14), sirenMat, new Vector3(0f, 1.44f, 0f), null, null, false);
            ProcMesh.Part("SirenBase", t, Cached("bot_sirenbase", b => b.AddCylinder(new Vector3(0f, 0.0f, 0f), 0.13f, 0.04f, Quaternion.identity, Color.white, 14, true)), dark, new Vector3(0f, 1.44f, 0f));

            // ---- Arms (shoulder pivots) with claws
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0; float sx = left ? -1f : 1f;
                var arm = Pivot(left ? "ArmL" : "ArmR", t, new Vector3(sx * 0.46f, 0.95f, 0.02f));
                var seg = new GameObject("Segment").transform;
                seg.SetParent(arm, false);
                seg.localRotation = Quaternion.Euler(-18f, 0f, sx * 14f);
                ProcMesh.Part("Upper", seg, VisualMeshes.Limb(0.045f, 0.34f), dark, Vector3.zero);
                var claw = new GameObject("Claw").transform;
                claw.SetParent(seg, false); claw.localPosition = new Vector3(0f, -0.38f, 0f);
                ProcMesh.Part("Pincers", claw, Cached("bot_claw", b =>
                {
                    b.AddSphere(Vector3.zero, 0.06f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
                    b.AddBox(new Vector3(-0.045f, -0.07f, 0.02f), new Vector3(0.03f, 0.11f, 0.05f), Quaternion.Euler(0f, 0f, -20f), Color.white, 0.01f);
                    b.AddBox(new Vector3(0.045f, -0.07f, 0.02f), new Vector3(0.03f, 0.11f, 0.05f), Quaternion.Euler(0f, 0f, 20f), Color.white, 0.01f);
                }), acc, Vector3.zero);
            }

            // ---- Antenna (wobbly)
            var ant = Pivot("Antenna", t, new Vector3(0.15f, 1.36f, -0.12f));
            ProcMesh.Part("Stalk", ant, Cached("bot_stalk", b => b.AddCylinder(new Vector3(0f, 0.15f, 0f), 0.012f, 0.3f, Quaternion.identity, Color.white, 6)), dark, Vector3.zero);
            ProcMesh.Part("Bulb", ant, VisualMeshes.Sphere(0.04f, 10, 8), MaterialLibrary.Colored(accent, 0.6f, 1.6f), new Vector3(0f, 0.32f, 0f), null, null, false);
            return root;
        }

        // ------------------------------------------------------------------ campaign re-hats (BONK-BOT outfits)
        /// <summary>Security-guard cap (CH04-CH08 guard bots): dark peaked cap with a gold badge and a black brim,
        /// the siren poking out of the top. botModel = the root returned by Build. Returns the cap.</summary>
        public static GameObject AddGuardCap(GameObject botModel)
        {
            if (botModel == null) return null;
            var t = botModel.transform;
            var old = t.Find("GuardCap"); if (old != null) return old.gameObject;
            var cap = Pivot("GuardCap", t, new Vector3(0f, 1.34f, 0.02f));
            ProcMesh.Part("Crown", cap, Cached("bot_guardcap", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.05f, 0f), 0.25f, 0.1f, Quaternion.identity, Color.white, 18, true);
                b.AddCylinder(new Vector3(0f, 0.115f, 0.015f), 0.29f, 0.03f, Quaternion.Euler(-6f, 0f, 0f), Color.white, 18, true);
            }), MaterialLibrary.Colored(new Color(0.16f, 0.2f, 0.3f), 0.4f), Vector3.zero);
            ProcMesh.Part("Brim", cap, Cached("bot_guardbrim", b =>
            {
                b.AddBox(new Vector3(0f, 0.01f, 0.25f), new Vector3(0.34f, 0.02f, 0.16f), Quaternion.Euler(14f, 0f, 0f), Color.white, 0.008f);
                b.AddBox(new Vector3(0f, 0.035f, 0.247f), new Vector3(0.46f, 0.018f, 0.02f), Quaternion.identity, Color.white, 0.005f);
            }), MaterialLibrary.Colored(new Color(0.06f, 0.06f, 0.07f), 0.85f), Vector3.zero);
            ProcMesh.Part("Badge", cap, Cached("bot_guardbadge", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.065f, 0.25f), 0.04f, 0.012f, Quaternion.Euler(90f, 0f, 0f), Color.white, 6, false);
            }), MaterialLibrary.Colored(VillainLook.Gold, 0.8f), Vector3.zero, null, null, false);
            return cap.gameObject;
        }

        /// <summary>Teller-bot / bank bow tie on the chest (CH04 TELLER-BOTS).</summary>
        public static GameObject AddBowTie(GameObject botModel, Color? color = null)
        {
            if (botModel == null) return null;
            var t = botModel.transform;
            var old = t.Find("BotBowTie"); if (old != null) return old.gameObject;
            var c = color ?? new Color(0.12f, 0.12f, 0.14f);
            return ProcMesh.Part("BotBowTie", t, Cached("bot_bowtie", b =>
            {
                b.AddSphere(new Vector3(-0.08f, 0f, 0f), 1f, Quaternion.Euler(0f, 0f, 90f), Color.white, 10, 6, true, new Vector3(0.05f, 0.08f, 0.03f));
                b.AddSphere(new Vector3(0.08f, 0f, 0f), 1f, Quaternion.Euler(0f, 0f, 90f), Color.white, 10, 6, true, new Vector3(0.05f, 0.08f, 0.03f));
                b.AddSphere(Vector3.zero, 0.035f, Quaternion.identity, Color.white, 8, 6);
            }), MaterialLibrary.Colored(c, 0.4f), new Vector3(0f, 0.84f, 0.42f), null, null, false);
        }

        /// <summary>Act III "night mode": red eye glow (true) or back to the accent (false = pass the accent).</summary>
        public static void SetEyeColor(GameObject botModel, Color c, float intensity = 2.2f)
        {
            if (botModel == null) return;
            var eye = botModel.transform.Find("Eye");
            var r = eye != null ? eye.GetComponent<Renderer>() : null;
            if (r != null) SetGlow(r.sharedMaterial, c, intensity);
        }

        /// <summary>Set a material's base + emission color (works for Shifters/StylizedLit and URP/Lit).</summary>
        public static void SetGlow(Material m, Color c, float intensity)
        {
            if (m == null) return;
            c.a = 1f;
            m.SetColor(BaseColorId, c);
            if (m.HasProperty(EmissionId)) { m.EnableKeyword("_EMISSION"); m.SetColor(EmissionId, c * intensity); }
        }

        /// <summary>Recolor a renderer's (instance) material, e.g. SetGlow(root.transform.Find("Eye").GetComponent&lt;Renderer&gt;(), Color.red, 2.5f).</summary>
        public static void SetGlow(Renderer r, Color c, float intensity) { if (r != null) SetGlow(r.material, c, intensity); }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pos;
            return p;
        }

        static Mesh Cached(string key, System.Action<ProcMesh.Builder> build)
            => ProcMesh.Cached(key, () => { var b = new ProcMesh.Builder(); build(b); return b.ToMesh(key); });
    }
}
