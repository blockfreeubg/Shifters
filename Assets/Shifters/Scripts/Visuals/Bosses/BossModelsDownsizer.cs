// =====================================================================================
//  SHIFTERS - BossModelsDownsizer.cs   [owned by VISUALS_BOSSES]
//  BossModels.Downsizer: THE DOWNSIZER (CH03, LEVELS2 SHIFT 03). Root = floor under the plinth centre, front = -Z
//  (the side the team enters from: sticker + badge face -Z), pads N/E/S/W at (0,6) (6,0) (0,-6) (-6,0) local.
//  Plinth 4 x 4 x 2 m (BoxCollider), column to 7 m, arm pivot 6.6 m, stamp 3.5 x 3.5 x 0.5 m (BoxCollider = the
//  REVIEW PLATFORM when slammed: top 0.5 m), hydraulic heart (1.2 m tank) at (0, 2.6, 0) behind the shutter sleeve.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class BossModels
    {
        /// <summary>The Downsizer press. colliders: plinth + stamp BoxColliders (the stamp is the P3 review platform);
        /// heartLight: one unshadowed orange point light (DecorLight-marked; LEVELS2 budget "heart glow").</summary>
        public static DownsizerModel Downsizer(Transform parent, Vector3 pos, Quaternion? rot = null, bool colliders = true, bool heartLight = true)
        {
            var go = Root("Downsizer", parent, pos, rot);
            var t = go.transform;
            var m = go.AddComponent<DownsizerModel>();
            var steel = C(Steel, 0.45f);
            var dark = C(SteelDark, 0.4f);
            var hazard = C(HazardY, 0.35f);
            var red = C(FiredRed, 0.35f);

            // ---- plinth (4 x 4 x 2) with hazard band and foot trim
            var plinth = P("Plinth", t, "dz_plinth", b =>
            {
                b.AddBox(new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 4f), Quaternion.identity, W, 0.08f);
            }, steel).transform;
            P("PlinthTrim", plinth, "dz_plinth_trim", b =>
            {
                b.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(4.12f, 0.24f, 4.12f), Quaternion.identity, W, 0.04f);
                b.AddBox(new Vector3(0f, 1.98f, 0f), new Vector3(4.1f, 0.12f, 4.1f), Quaternion.identity, W, 0.03f);
                // bolts on the four faces
                for (int s = 0; s < 4; s++)
                {
                    var q = Quaternion.Euler(0f, s * 90f, 0f);
                    for (int i = -1; i <= 1; i += 2)
                        b.AddCylinder(q * new Vector3(i * 1.6f, 1.6f, 2.02f), 0.09f, 0.06f, q * AlongZ, W, 8, true);
                }
            }, dark);
            P("PlinthStripes", plinth, "dz_plinth_stripes", b =>
            {
                // diagonal hazard chevrons on the 4 faces (yellow bars on the dark trim band)
                for (int s = 0; s < 4; s++)
                {
                    var q = Quaternion.Euler(0f, s * 90f, 0f);
                    for (int i = 0; i < 8; i++)
                        b.AddBox(q * new Vector3(-1.75f + i * 0.5f, 0.75f, 2.015f), new Vector3(0.18f, 0.5f, 0.03f), q * Quaternion.Euler(0f, 0f, 35f), W, 0f);
                }
            }, hazard);
            if (colliders)
            {
                m.PlinthCollider = plinth.gameObject.AddComponent<BoxCollider>();
                m.PlinthCollider.center = new Vector3(0f, 1f, 0f);
                m.PlinthCollider.size = new Vector3(4f, 2f, 4f);
            }

            // ---- sticker on the front (-Z) face
            var sticker = Node("Sticker", t, new Vector3(0f, 1.25f, -2.03f), Face(Vector3.back));
            P("StickerPaper", sticker, "dz_sticker", b => b.AddBox(Vector3.zero, new Vector3(2.4f, 0.62f, 0.02f), Quaternion.identity, W, 0.01f), C(Paper, 0.2f), new Vector3(0f, 0f, -0.01f));
            EnvKit.AddText(sticker, "RATED FOR ONE (1) EMPLOYEE\n<size=70%>MAX LOAD 150 KG</size>", new Vector3(0f, 0f, 0.012f), new Vector2(2.25f, 0.52f), Ink, ShiftersFonts.Display, 0.26f);

            // ---- the hydraulic heart (glass tank + glowing liquid) on the plinth top
            var heart = Node("Heart", t, m.HeartCenter);
            P("HeartGlass", heart, "dz_heart_glass", b =>
            {
                b.AddCylinder(Vector3.zero, 0.6f, 1.1f, Quaternion.identity, W, 18, true, false);
            }, GlassMat, default, null, false);
            var liquidGo = P("HeartLiquid", heart, "dz_heart_liquid", b =>
            {
                b.AddCylinder(new Vector3(0f, -0.08f, 0f), 0.5f, 0.86f, Quaternion.identity, W, 16, true);
                b.AddSphere(new Vector3(0.15f, 0.2f, 0.25f), 0.12f, Quaternion.identity, W, 8, 6, true, null);   // bubbles
                b.AddSphere(new Vector3(-0.2f, -0.15f, 0.22f), 0.09f, Quaternion.identity, W, 8, 6, true, null);
            }, Glow(new Color(1f, 0.42f, 0.12f), 1.4f), default, null, false);
            m.HeartDim = Glow(new Color(1f, 0.42f, 0.12f), 1.4f);
            m.HeartBright = Glow(new Color(1f, 0.62f, 0.2f), 3.2f);
            m.HeartLiquid = liquidGo.GetComponent<Renderer>();
            m.HeartLiquid.sharedMaterial = m.HeartDim;
            P("HeartCaps", heart, "dz_heart_caps", b =>
            {
                b.AddCylinder(new Vector3(0f, -0.6f, 0f), 0.68f, 0.12f, Quaternion.identity, W, 18, true);
                b.AddCylinder(new Vector3(0f, 0.6f, 0f), 0.68f, 0.12f, Quaternion.identity, W, 18, true);
                for (int i = 0; i < 4; i++)
                {
                    float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                    b.AddCylinder(new Vector3(Mathf.Sin(a) * 0.62f, 0f, Mathf.Cos(a) * 0.62f), 0.04f, 1.2f, Quaternion.identity, W, 6, true);
                }
            }, dark);
            if (heartLight)
            {
                var lgo = Node("HeartLight", heart, new Vector3(0f, 0.2f, 0f));
                var l = lgo.gameObject.AddComponent<Light>();
                l.type = LightType.Point; l.range = 5f; l.intensity = 1.1f; l.color = new Color(1f, 0.55f, 0.2f);
                l.shadows = LightShadows.None;
                m.HeartLight = l;
                DecorLight.Mark(l);
            }

            // ---- shutter sleeve (4 armored panels with hazard stripes); open = sinks into the plinth
            var shutter = Node("Shutter", t, Vector3.zero);
            P("ShutterPanels", shutter, "dz_shutter", b =>
            {
                for (int i = 0; i < 8; i++)
                {
                    var q = Quaternion.Euler(0f, i * 45f, 0f);
                    b.AddBox(new Vector3(0f, 2.66f, 0f) + q * new Vector3(0f, 0f, 0.74f), new Vector3(0.6f, 1.32f, 0.1f), q, W, 0.03f);
                }
            }, steel);
            P("ShutterStripes", shutter, "dz_shutter_stripes", b =>
            {
                for (int i = 0; i < 8; i++)
                {
                    var q = Quaternion.Euler(0f, i * 45f, 0f);
                    b.AddBox(new Vector3(0f, 2.66f, 0f) + q * new Vector3(0f, 0f, 0.80f), new Vector3(0.12f, 1.2f, 0.02f), q * Quaternion.Euler(0f, 0f, 30f), W, 0f);
                }
            }, hazard);

            // ---- column: struts from the plinth corners to a collar, the hydraulic column, gold ME badge
            var column = P("Column", t, "dz_column", b =>
            {
                b.AddCylinder(new Vector3(0f, 5.25f, 0f), 0.55f, 3.5f, Quaternion.identity, W, 16, true);
                b.AddCylinder(new Vector3(0f, 3.55f, 0f), 0.95f, 0.22f, Quaternion.identity, W, 16, true);   // collar
                for (int i = 0; i < 4; i++)
                {
                    float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                    var lo = new Vector3(Mathf.Sin(a) * 2.25f, 2.05f, Mathf.Cos(a) * 2.25f);
                    var hi = new Vector3(Mathf.Sin(a) * 0.85f, 3.5f, Mathf.Cos(a) * 0.85f);
                    var d = hi - lo;
                    b.AddCylinder((lo + hi) * 0.5f, 0.13f, d.magnitude, Quaternion.FromToRotation(Vector3.up, d), W, 8, true);
                }
            }, dark).transform;
            P("ColumnRings", column, "dz_column_rings", b =>
            {
                for (int i = 0; i < 4; i++) b.AddTorus(new Vector3(0f, 4.1f + i * 0.7f, 0f), 0.57f, 0.06f, Quaternion.identity, W, 18, 5);
            }, GoldMat);
            var badge = Node("Badge", column, new Vector3(0f, 4.45f, -0.6f), Face(Vector3.back));
            EnvKit.AddText(badge, "ME", Vector3.zero, new Vector2(0.9f, 0.5f), new Color(0.95f, 0.72f, 0.2f), ShiftersFonts.Display, 0.5f);

            // ---- arm (pivot on the column top): hub, beam to the stamp, counterweight, hoses
            var arm = Node("Arm", t, new Vector3(0f, m.PivotHeight, 0f));
            P("ArmBeam", arm, "dz_arm", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.1f, 0f), 0.9f, 0.9f, Quaternion.identity, W, 18, true);                 // hub
                b.AddBox(new Vector3(0f, 0.15f, 3.1f), new Vector3(0.8f, 0.75f, 6.4f), Quaternion.identity, W, 0.08f); // beam
                b.AddBox(new Vector3(0f, 0.15f, 6f), new Vector3(1.3f, 0.95f, 1.3f), Quaternion.identity, W, 0.1f);    // head
            }, steel);
            P("ArmStripes", arm, "dz_arm_stripes", b =>
            {
                for (int i = 0; i < 9; i++)
                {
                    b.AddBox(new Vector3(0.41f, 0.15f, 1.2f + i * 0.5f), new Vector3(0.02f, 0.7f, 0.18f), Quaternion.Euler(35f, 0f, 0f), W, 0f);
                    b.AddBox(new Vector3(-0.41f, 0.15f, 1.2f + i * 0.5f), new Vector3(0.02f, 0.7f, 0.18f), Quaternion.Euler(35f, 0f, 0f), W, 0f);
                }
            }, hazard);
            P("Counterweight", arm, "dz_counter", b =>
            {
                b.AddBox(new Vector3(0f, 0.15f, -1.6f), new Vector3(1.4f, 1.1f, 1.2f), Quaternion.identity, W, 0.1f);
                b.AddBox(new Vector3(0f, 0.15f, -0.9f), new Vector3(0.6f, 0.5f, 0.6f), Quaternion.identity, W, 0.05f);
            }, dark);
            P("Hoses", arm, "dz_hoses", b =>
            {
                for (int i = 0; i < 6; i++)
                {
                    float z = 0.9f + i * 0.85f;
                    b.AddCylinder(new Vector3(0.48f, -0.25f + 0.05f * Mathf.Sin(i), z), 0.07f, 0.95f, AlongZ, W, 8, true);
                    b.AddCylinder(new Vector3(-0.48f, -0.25f + 0.05f * Mathf.Cos(i), z), 0.07f, 0.95f, AlongZ, W, 8, true);
                }
            }, C(new Color(0.08f, 0.08f, 0.09f), 0.3f));
            // piston housing hangs under the arm head; the rod is a unit cylinder scaled in Y (pivot at its top)
            var piston = P("Piston", arm, "dz_piston", b =>
            {
                b.AddCylinder(new Vector3(0f, -0.8f, 0f), 0.42f, 1.6f, Quaternion.identity, W, 14, true);
                b.AddTorus(new Vector3(0f, -1.55f, 0f), 0.44f, 0.07f, Quaternion.identity, W, 14, 5);
            }, dark, new Vector3(0f, m.HousingBottom - m.PivotHeight + 1.6f, m.ArmRadius)).transform;
            var rod = P("PistonRod", arm, "dz_rod", b =>
            {
                b.AddCylinder(new Vector3(0f, -0.5f, 0f), 0.26f, 1f, Quaternion.identity, W, 14, true);
            }, C(new Color(0.78f, 0.8f, 0.84f), 0.8f)).transform;

            // ---- stamp (3.5 x 3.5 x 0.5): steel block, red rubber face, hazard edge, YOU'RE FIRED on all sides + underside
            var stamp = Node("Stamp", arm, Vector3.zero);
            float s = m.StampSize, th = m.StampThickness;
            P("StampBody", stamp, "dz_stamp", b =>
            {
                b.AddBox(new Vector3(0f, th * 0.62f, 0f), new Vector3(s, th * 0.76f, s), Quaternion.identity, W, 0.06f);
                b.AddBox(new Vector3(0f, th + 0.12f, 0f), new Vector3(1.2f, 0.3f, 1.2f), Quaternion.identity, W, 0.05f);  // rod socket
            }, steel);
            P("StampFace", stamp, "dz_stamp_face", b =>
            {
                b.AddBox(new Vector3(0f, th * 0.12f, 0f), new Vector3(s - 0.1f, th * 0.24f, s - 0.1f), Quaternion.identity, W, 0.03f);
            }, red);
            P("StampEdge", stamp, "dz_stamp_edge", b =>
            {
                for (int side = 0; side < 4; side++)
                {
                    var q = Quaternion.Euler(0f, side * 90f, 0f);
                    for (int i = 0; i < 7; i++)
                        b.AddBox(q * new Vector3(-1.5f + i * 0.5f, th * 0.86f, s * 0.5f + 0.01f), new Vector3(0.16f, th * 0.26f, 0.02f), q * Quaternion.Euler(0f, 0f, 35f), W, 0f);
                }
            }, hazard);
            for (int side = 0; side < 4; side++)
            {
                var n = Quaternion.Euler(0f, side * 90f, 0f) * Vector3.forward;
                Label(stamp, "Fired" + side, "YOU'RE FIRED", n * (s * 0.5f + 0.02f) + new Vector3(0f, th * 0.5f, 0f), Face(n), new Vector2(s * 0.86f, th * 0.42f), Paper, th * 0.4f);
            }
            Label(stamp, "FiredUnder", "YOU'RE\nFIRED", new Vector3(0f, -0.005f, 0f), Face(Vector3.down, Vector3.forward), new Vector2(s * 0.8f, s * 0.6f), Paper, 0.9f);
            if (colliders)
            {
                m.StampCollider = stamp.gameObject.AddComponent<BoxCollider>();
                m.StampCollider.center = new Vector3(0f, th * 0.5f, 0f);
                m.StampCollider.size = new Vector3(s, th, s);
            }

            m.Plinth = plinth; m.Column = column; m.Arm = arm; m.Piston = piston; m.PistonRod = rod; m.Stamp = stamp;
            m.Heart = heart; m.Shutter = shutter; m.Sticker = sticker;
            m.SetArmYaw(0f);
            m.SetStampBottom(4f);
            m.SetShutter(0f);
            return Finish(m, "Downsizer");
        }
    }
}
