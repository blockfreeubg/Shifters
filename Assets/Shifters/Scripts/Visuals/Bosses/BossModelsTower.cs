// =====================================================================================
//  SHIFTERS - BossModelsTower.cs   [owned by VISUALS_BOSSES]
//  BossModels.OneManBand (CH13: Max's 9 m gold ME-CHA) and BossModels.SoloPod (the 105 kg gold hover-chair).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class BossModels
    {
        /// <summary>THE ONE-MAN BAND: 9 m gold ME-CHA (antenna tip ~9.6 m). Root on the floor, front +Z. A bass-drum torso
        /// ("ME"), shoulder crash cymbals, knee cymbals, ArmR = giant gold Fist (hangs to ~1.3 m above the floor), ArmL =
        /// giant kazoo, rocket roller-skates (Flame children), a glass brain Dome with Max at the controls in the Cockpit and
        /// the Antenna on top. withMax: seat Maximilian Me (VillainModelBuilder, or a stand-in). No colliders.</summary>
        public static OneManBandModel OneManBand(Transform parent, Vector3 pos, Quaternion? rot = null, bool withMax = true)
        {
            var go = Root("OneManBand", parent, pos, rot);
            var t = go.transform;
            var m = go.AddComponent<OneManBandModel>();
            var gold = GoldMat;
            var steel = C(Steel, 0.6f);
            var dark = C(SteelDark, 0.45f);
            var skin = C(new Color(0.93f, 0.92f, 0.88f), 0.25f);
            var flame = Glow(new Color(1f, 0.55f, 0.15f), 3f);

            // ---- legs + rocket skates
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = Node(s < 0 ? "LegL" : "LegR", t, new Vector3(s * 1.15f, 3.5f, 0f));
                P("Leg", leg, "omb_leg", b =>
                {
                    b.AddCylinder(new Vector3(0f, -0.6f, 0f), 0.4f, 1.2f, Quaternion.identity, W, 14, true);
                    b.AddCylinder(new Vector3(0f, -1.9f, 0f), 0.34f, 1.2f, Quaternion.identity, W, 14, true);
                }, steel);
                P("Knee", leg, "omb_knee", b =>
                {
                    b.AddSphere(new Vector3(0f, -1.25f, 0f), 0.46f, Quaternion.identity, W, 12, 8, true, null);
                    b.AddCylinder(new Vector3(0f, -1.25f, 0.5f), 0.55f, 0.05f, AlongZ, W, 18, true);    // knee cymbal
                    b.AddSphere(new Vector3(0f, -1.25f, 0.53f), 0.1f, Quaternion.identity, W, 8, 5, true, null);
                }, gold);
                var skate = Node(s < 0 ? "SkateL" : "SkateR", leg, new Vector3(0f, -2.6f, 0f));
                P("Boot", skate, "omb_boot", b =>
                {
                    b.AddBox(new Vector3(0f, 0.15f, 0.12f), new Vector3(1.0f, 0.8f, 1.75f), Quaternion.identity, W, 0.2f);
                    b.AddBox(new Vector3(0f, -0.3f, 0.12f), new Vector3(0.9f, 0.14f, 1.9f), Quaternion.identity, W, 0.05f);   // plate
                }, gold);
                P("Wheels", skate, "omb_wheels", b =>
                {
                    for (int i = 0; i < 4; i++)
                        b.AddCylinder(new Vector3(0f, -0.6f, -0.62f + i * 0.45f), 0.3f, 0.8f, AlongX, W, 14, true);
                    b.AddCylinder(new Vector3(0f, -0.25f, 1.12f), 0.18f, 0.2f, AlongZ, W, 10, true);                      // toe stop
                }, dark);
                P("Booster", skate, "omb_booster", b =>
                {
                    b.AddCylinder(new Vector3(0f, 0.25f, -1.05f), 0.32f, 0.9f, AlongZ, W, 14, true);
                    b.AddCone(new Vector3(0f, 0.25f, -1.5f), 0.3f, 0.2f, ConeBack, W, 14);
                }, steel);
                var fl = Node(s < 0 ? "FlameL" : "FlameR", skate, new Vector3(0f, 0.25f, -1.55f));
                P("Fire", fl, "omb_flame", b =>
                {
                    b.AddCone(Vector3.zero, 0.26f, 1.0f, ConeBack, W, 12);
                }, flame, default, null, false);
                if (s < 0) { m.LegL = leg; m.SkateL = skate; m.FlameL = fl; } else { m.LegR = leg; m.SkateR = skate; m.FlameR = fl; }
            }

            // ---- pelvis + bass-drum torso
            var pelvis = Node("Pelvis", t, new Vector3(0f, 3.5f, 0f));
            P("Hips", pelvis, "omb_hips", b =>
            {
                b.AddBox(Vector3.zero, new Vector3(2.7f, 0.8f, 1.4f), Quaternion.identity, W, 0.25f);
                b.AddSphere(new Vector3(-1.15f, 0f, 0f), 0.5f, Quaternion.identity, W, 12, 8, true, null);
                b.AddSphere(new Vector3(1.15f, 0f, 0f), 0.5f, Quaternion.identity, W, 12, 8, true, null);
            }, gold);
            var torso = Node("Torso", t, new Vector3(0f, 3.9f, 0f));
            P("DrumShell", torso, "omb_drum", b =>
            {
                b.AddCylinder(new Vector3(0f, 1.75f, 0f), 1.7f, 1.5f, AlongZ, W, 28, true, false);
            }, gold);
            P("DrumSkins", torso, "omb_skins", b =>
            {
                b.AddCylinder(new Vector3(0f, 1.75f, 0.74f), 1.62f, 0.02f, AlongZ, W, 28, true);
                b.AddCylinder(new Vector3(0f, 1.75f, -0.74f), 1.62f, 0.02f, AlongZ, W, 28, true);
            }, skin);
            P("DrumHoops", torso, "omb_hoops", b =>
            {
                b.AddTorus(new Vector3(0f, 1.75f, 0.78f), 1.7f, 0.1f, AlongZ, W, 28, 6);
                b.AddTorus(new Vector3(0f, 1.75f, -0.78f), 1.7f, 0.1f, AlongZ, W, 28, 6);
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f;
                    b.AddBox(new Vector3(Mathf.Cos(a) * 1.74f, 1.75f + Mathf.Sin(a) * 1.74f, 0f), new Vector3(0.12f, 0.12f, 1.4f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), W, 0.03f);
                }
                b.AddCylinder(new Vector3(-2.05f, 2.7f, 0f), 0.08f, 1.1f, Quaternion.identity, W, 8, true);    // cymbal stands
                b.AddCylinder(new Vector3(2.05f, 2.7f, 0f), 0.08f, 1.1f, Quaternion.identity, W, 8, true);
            }, dark);
            P("Cymbals", torso, "omb_cymbals", b =>
            {
                b.AddCylinder(new Vector3(-2.05f, 3.3f, 0f), 0.85f, 0.05f, Quaternion.Euler(0f, 0f, 8f), W, 22, true);
                b.AddCylinder(new Vector3(2.05f, 3.3f, 0f), 0.85f, 0.05f, Quaternion.Euler(0f, 0f, -8f), W, 22, true);
                b.AddSphere(new Vector3(-2.05f, 3.34f, 0f), 0.14f, Quaternion.identity, W, 8, 5, true, null);
                b.AddSphere(new Vector3(2.05f, 3.34f, 0f), 0.14f, Quaternion.identity, W, 8, 5, true, null);
                b.AddSphere(new Vector3(-2.05f, 2.7f, 0f), 0.48f, Quaternion.identity, W, 12, 8, true, null);   // shoulders
                b.AddSphere(new Vector3(2.05f, 2.7f, 0f), 0.48f, Quaternion.identity, W, 12, 8, true, null);
                b.AddTorus(new Vector3(0f, 3.5f, 0f), 0.95f, 0.14f, Quaternion.identity, W, 20, 6);             // neck collar
            }, gold);
            Label(torso, "DrumME", "ME", new Vector3(0f, 1.85f, 0.76f), Face(Vector3.forward), new Vector2(2.6f, 1.6f), new Color(0.75f, 0.55f, 0.1f), 1.5f);
            Label(torso, "DrumBand", "THE ONE-MAN BAND", new Vector3(0f, 0.75f, 0.76f), Face(Vector3.forward), new Vector2(2.4f, 0.35f), SteelDark, 0.28f);
            Label(torso, "DrumBack", "NO OTHER MEMBERS", new Vector3(0f, 1.75f, -0.76f), Face(Vector3.back), new Vector2(2.4f, 0.4f), SteelDark, 0.32f);

            // ---- arms (pivot at the shoulders): ArmR -> Fist, ArmL -> Kazoo
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = Node(s < 0 ? "ArmL" : "ArmR", torso, new Vector3(s * 2.15f, 2.7f, 0f));
                P("ArmSteel", arm, "omb_arm", b =>
                {
                    b.AddCylinder(new Vector3(0f, -1.0f, 0f), 0.36f, 1.8f, Quaternion.identity, W, 14, true);
                    b.AddCylinder(new Vector3(0f, -2.9f, 0f), 0.4f, 1.6f, Quaternion.identity, W, 14, true);
                }, steel);
                P("ArmGold", arm, "omb_arm_gold", b =>
                {
                    b.AddSphere(new Vector3(0f, -2.0f, 0f), 0.48f, Quaternion.identity, W, 12, 8, true, null);
                    b.AddTorus(new Vector3(0f, -3.6f, 0f), 0.44f, 0.1f, Quaternion.identity, W, 14, 5);
                }, gold);
                if (s > 0)
                {
                    var fist = Node("Fist", arm, new Vector3(0f, -4.6f, 0f));
                    P("Knuckles", fist, "omb_fist", b =>
                    {
                        b.AddBox(new Vector3(0f, 0.05f, 0f), new Vector3(1.6f, 1.35f, 1.45f), Quaternion.identity, W, 0.35f);
                        for (int k = 0; k < 4; k++)
                            b.AddCylinder(new Vector3(-0.57f + k * 0.38f, -0.62f, 0.3f), 0.2f, 0.5f, AlongZ, W, 10, true);
                        b.AddCapsule(new Vector3(-0.85f, -0.05f, 0.45f), 0.22f, 0.8f, Quaternion.Euler(20f, 0f, 0f), W, 10, 3);   // thumb
                    }, gold);
                    P("Ring", fist, "omb_ring", b =>
                    {
                        b.AddBox(new Vector3(0.19f, -0.62f, 0.62f), new Vector3(0.34f, 0.3f, 0.12f), Quaternion.identity, W, 0.04f);
                    }, C(FiredRed, 0.8f, 0.6f));
                    m.Fist = fist; m.ArmR = arm;
                }
                else
                {
                    var kazoo = Node("Kazoo", arm, new Vector3(0f, -3.95f, 0.6f));
                    P("Horn", kazoo, "omb_kazoo", b =>
                    {
                        b.AddSphere(new Vector3(0f, 0f, -0.6f), 0.5f, Quaternion.identity, W, 12, 8, true, null);          // hand
                        b.AddCylinder(new Vector3(0f, 0f, 0.2f), 0.3f, 1.8f, AlongZ, W, 14, true);
                        b.AddCone(new Vector3(0f, 0f, 1.6f), 0.72f, 0.75f, ConeBack, W, 16);
                        b.AddCylinder(new Vector3(0f, 0.32f, 0.3f), 0.2f, 0.12f, Quaternion.identity, W, 12, true);         // membrane cap
                    }, gold);
                    m.Kazoo = kazoo; m.ArmL = arm;
                }
            }

            // ---- glass brain dome with the cockpit and Max
            var dome = Node("Dome", torso, new Vector3(0f, 3.5f, 0f));
            var domeGo = ProcMesh.Part("DomeGlass", dome, VisualMeshes.Dome(1.35f, 1.45f, 1.35f, 2f, 22), GlassMat, Vector3.zero, null, null, false);
            m.DomeGlass = domeGo.GetComponent<Renderer>();
            m.DomeClear = GlassMat;
            m.DomeShield = MaterialLibrary.Hologram(new Color(0.35f, 0.85f, 1f, 0.5f));
            var cockpit = Node("Cockpit", dome, new Vector3(0f, 0.05f, 0f));
            P("Floor", cockpit, "omb_cockpit_floor2", b =>
            {
                b.AddCylinder(new Vector3(0f, 0f, 0f), 1.25f, 0.1f, Quaternion.identity, W, 22, true);
                b.AddBox(new Vector3(0f, 0.2f, 0.92f), new Vector3(1.2f, 0.34f, 0.3f), Quaternion.Euler(-20f, 0f, 0f), W, 0.05f);   // console (low: Max reads over it)
            }, dark);
            P("Controls", cockpit, "omb_controls2", b =>
            {
                b.AddCylinder(new Vector3(-0.35f, 0.5f, 0.86f), 0.03f, 0.36f, Quaternion.Euler(-25f, 0f, 0f), W, 6, true);
                b.AddCylinder(new Vector3(0.35f, 0.5f, 0.86f), 0.03f, 0.36f, Quaternion.Euler(-25f, 0f, 0f), W, 6, true);
                b.AddSphere(new Vector3(-0.35f, 0.67f, 0.79f), 0.07f, Quaternion.identity, W, 8, 5, true, null);
                b.AddSphere(new Vector3(0.35f, 0.67f, 0.79f), 0.07f, Quaternion.identity, W, 8, 5, true, null);
                b.AddBox(new Vector3(0f, 0.05f, -0.12f), new Vector3(0.75f, 0.12f, 0.6f), Quaternion.identity, W, 0.04f);      // throne seat
                b.AddBox(new Vector3(0f, 0.55f, -0.42f), new Vector3(0.75f, 1.0f, 0.1f), Quaternion.identity, W, 0.04f);
            }, gold);
            P("Buttons", cockpit, "omb_buttons2", b =>
            {
                for (int i = 0; i < 5; i++) b.AddBox(new Vector3(-0.4f + i * 0.2f, 0.38f, 1.04f), new Vector3(0.1f, 0.05f, 0.08f), Quaternion.Euler(-20f, 0f, 0f), W, 0.01f);
            }, Glow(new Color(1f, 0.3f, 0.25f), 2f), default, null, false);
            var seat = Node("MaxSeat", cockpit, new Vector3(0f, 0.0f, 0.15f));   // forward + low console: Max reads from the floor
            if (withMax) m.Max = SeatMax(seat, Vector3.zero, 0.92f);
            P("DomeRing", dome, "omb_dome_ring", b => b.AddTorus(new Vector3(0f, 0.04f, 0f), 1.37f, 0.09f, Quaternion.identity, W, 24, 6), gold);

            // P3 FIREWALL: two hex half-shields over the dome (ShieldW = -X, ShieldE = +X), off until SetShieldHalf / SetDomeShield
            for (int s = -1; s <= 1; s += 2)
            {
                int side = s;
                var half = Node(side < 0 ? "ShieldW" : "ShieldE", dome, Vector3.zero);
                P("Hexes", half, side < 0 ? "omb_shield_w" : "omb_shield_e", b => ShieldHexes(b, side), m.DomeShield, default, null, false);
                half.gameObject.SetActive(false);
                if (side < 0) m.ShieldW = half; else m.ShieldE = half;
            }

            // P1 detached fist: 6 gold chain links the model stretches from the ArmR wrist to the Fist (SetFistDetached)
            var chain = Node("FistChain", t, Vector3.zero);
            m.ChainLinks = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                var link = Node("Link" + i, chain, Vector3.zero);
                P("Ring", link, "omb_chain_link", b => b.AddTorus(Vector3.zero, 0.2f, 0.065f, Quaternion.identity, W, 12, 5), gold, default, null, false);
                m.ChainLinks[i] = link;
            }
            m.FistChain = chain;
            chain.gameObject.SetActive(false);

            var antenna = Node("Antenna", dome, new Vector3(0f, 1.43f, 0f));
            P("Mast", antenna, "omb_mast", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.3f, 0f), 0.06f, 0.6f, Quaternion.identity, W, 8, true);
                b.AddCylinder(new Vector3(0f, 0.02f, 0f), 0.2f, 0.06f, Quaternion.identity, W, 12, true);
                b.AddBox(new Vector3(0.18f, 0.45f, 0f), new Vector3(0.3f, 0.18f, 0.02f), Quaternion.identity, W, 0.01f);   // ME flag
            }, gold);
            var tip = Node("AntennaTip", antenna, new Vector3(0f, 0.72f, 0f));
            P("Bulb", tip, "omb_antenna_tip", b => b.AddSphere(Vector3.zero, 0.2f, Quaternion.identity, W, 12, 8, true, null), Glow(new Color(1f, 0.2f, 0.15f), 3f), default, null, false);

            m.Pelvis = pelvis; m.Torso = torso; m.Dome = dome; m.Cockpit = cockpit; m.Antenna = antenna; m.AntennaTip = tip; m.MaxSeat = seat;
            m.ShoulderHeight = 3.9f + 2.7f; m.FistReach = 4.6f;
            m.Height = 3.9f + 3.5f + 1.43f + 0.92f;
            m.SetBoost(true);
            return Finish(m, "OneManBand");
        }

        /// <summary>Hex tiles over one half of the cockpit dome (side -1 = -X / west, +1 = +X / east), slightly outside the glass.</summary>
        static void ShieldHexes(ProcMesh.Builder b, int side)
        {
            float[] els = { 8f, 28f, 48f, 68f, 84f };
            int[] counts = { 9, 8, 6, 4, 1 };
            for (int r = 0; r < els.Length; r++)
            {
                float el = els[r] * Mathf.Deg2Rad;
                int n = counts[r];
                float spacing = Mathf.PI * 1.55f * Mathf.Cos(el) / n;
                float rad = Mathf.Clamp(spacing * 0.46f, 0.16f, 0.27f);
                for (int i = 0; i < n; i++)
                {
                    float phi = (side > 0 ? 0f : Mathf.PI) + (i + 0.5f) * Mathf.PI / n;
                    var dir = new Vector3(Mathf.Cos(el) * Mathf.Sin(phi), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(phi));
                    var pos = new Vector3(dir.x * 1.52f, dir.y * 1.62f, dir.z * 1.52f);
                    b.AddCylinder(pos, rad, 0.035f, Quaternion.FromToRotation(Vector3.up, dir), W, 6, true);
                }
            }
        }

        /// <summary>THE SOLO POD: Max's gold hover-chair (1.6 m wide incl. armrests, 1.95 m to the crest; gameplay mass 105 kg
        /// is set by whoever adds the Rigidbody/Grabbable). Root = floor contact centre, front +Z; visuals live under
        /// "Visual" (SetHover / SetSpin move them, the collider stays). collider: BoxCollider on the root.</summary>
        public static SoloPodModel SoloPod(Transform parent, Vector3 pos, Quaternion? rot = null, bool withMax = true, bool collider = true)
        {
            var go = Root("SoloPod", parent, pos, rot);
            var t = go.transform;
            var m = go.AddComponent<SoloPodModel>();
            var gold = GoldMat;
            var velvet = Kit.Mat(MatKey.Velvet);
            var visual = Node("Visual", t, Vector3.zero);

            var baseN = Node("Base", visual, Vector3.zero);
            ProcMesh.Part("Saucer", baseN, VisualMeshes.Lathe("vb_pod_saucer", new[]
            {
                new Vector4(0.1f, 0.0f, 0.0f, 0f), new Vector4(0.12f, 0.5f, 0.5f, 0f), new Vector4(0.2f, 0.78f, 0.78f, 0f),
                new Vector4(0.32f, 0.8f, 0.8f, 0f), new Vector4(0.42f, 0.66f, 0.66f, 0f), new Vector4(0.45f, 0f, 0f, 0f)
            }, 2f, 22), gold, Vector3.zero);
            Label(baseN, "BaseLabel", "SOLO POD - 1 VIP", new Vector3(0f, 0.27f, 0.81f), Face(Vector3.forward), new Vector2(0.9f, 0.14f), Ink, 0.11f);

            var jets = Node("Jets", visual, Vector3.zero);
            P("Nozzles", jets, "pod_nozzles", b =>
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i / 3f * Mathf.PI * 2f;
                    b.AddCylinder(new Vector3(Mathf.Sin(a) * 0.42f, 0.08f, Mathf.Cos(a) * 0.42f), 0.13f, 0.1f, Quaternion.identity, W, 12, true);
                }
            }, C(SteelDark, 0.5f));
            m.JetOn = Glow(new Color(0.45f, 0.9f, 1f), 3f);
            m.JetOff = C(new Color(0.25f, 0.28f, 0.32f), 0.5f);
            var jr = P("JetGlow", jets, "pod_jetglow", b =>
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i / 3f * Mathf.PI * 2f;
                    b.AddCylinder(new Vector3(Mathf.Sin(a) * 0.42f, 0.025f, Mathf.Cos(a) * 0.42f), 0.1f, 0.02f, Quaternion.identity, W, 12, true);
                }
            }, m.JetOn, default, null, false);
            m.JetRenderer = jr.GetComponent<Renderer>();

            var seatN = Node("Seat", visual, Vector3.zero);
            P("Frame", seatN, "pod_frame", b =>
            {
                b.AddBox(new Vector3(0f, 0.5f, 0.02f), new Vector3(1.05f, 0.12f, 0.9f), Quaternion.identity, W, 0.04f);
                b.AddCylinder(new Vector3(0f, 0.47f, 0f), 0.25f, 0.06f, Quaternion.identity, W, 12, true);
            }, gold);
            P("Cushion", seatN, "pod_cushion", b => b.AddBox(new Vector3(0f, 0.62f, 0.05f), new Vector3(0.95f, 0.2f, 0.8f), Quaternion.identity, W, 0.08f), velvet);
            var back = Node("Backrest", visual, new Vector3(0f, 0.6f, -0.42f), Quaternion.Euler(-8f, 0f, 0f));
            P("BackGold", back, "pod_back_gold", b => b.AddBox(new Vector3(0f, 0.58f, -0.06f), new Vector3(1.08f, 1.18f, 0.1f), Quaternion.identity, W, 0.05f), gold);
            P("BackVelvet", back, "pod_back_velvet", b => b.AddBox(new Vector3(0f, 0.56f, 0.03f), new Vector3(0.92f, 1.02f, 0.12f), Quaternion.identity, W, 0.06f), velvet);
            var crest = Node("Crest", back, new Vector3(0f, 1.3f, -0.04f));
            P("Medallion", crest, "pod_crest", b =>
            {
                b.AddCylinder(Vector3.zero, 0.24f, 0.08f, AlongZ, W, 18, true);
                for (int i = 0; i < 5; i++) b.AddCone(new Vector3(-0.2f + i * 0.1f, 0.2f, 0f), 0.05f, 0.14f, Quaternion.identity, W, 6);
            }, gold);
            Label(crest, "CrestME", "ME", new Vector3(0f, 0f, 0.045f), Face(Vector3.forward), new Vector2(0.36f, 0.22f), Ink, 0.2f);
            Label(back, "BackLabel", "RESERVED: ME", new Vector3(0f, 0.6f, -0.115f), Face(Vector3.back), new Vector2(0.9f, 0.2f), Ink, 0.15f);

            Transform eject = null;
            for (int s = -1; s <= 1; s += 2)
            {
                var ar = Node(s < 0 ? "ArmrestL" : "ArmrestR", visual, new Vector3(s * 0.58f, 0.86f, 0.05f));
                P("Rest", ar, "pod_armrest", b =>
                {
                    b.AddBox(Vector3.zero, new Vector3(0.16f, 0.1f, 0.78f), Quaternion.identity, W, 0.04f);
                    b.AddBox(new Vector3(0f, -0.17f, 0.25f), new Vector3(0.08f, 0.3f, 0.08f), Quaternion.identity, W, 0.02f);
                }, gold);
                if (s > 0)
                {
                    eject = Node("EjectButton", ar, new Vector3(0f, 0.07f, 0.28f));
                    P("Button", eject, "pod_button", b => b.AddCylinder(Vector3.zero, 0.05f, 0.04f, Quaternion.identity, W, 10, true), Glow(new Color(1f, 0.15f, 0.12f), 2.5f), default, null, false);
                }
            }

            var maxSeat = Node("MaxSeat", visual, new Vector3(0f, 0.72f, 0.0f));
            if (withMax) m.Max = SeatMax(maxSeat, Vector3.zero, 0.95f);

            if (collider)
            {
                m.Collider = go.AddComponent<BoxCollider>();
                m.Collider.center = new Vector3(0f, 0.95f, 0f);
                m.Collider.size = new Vector3(1.4f, 1.85f, 1.4f);
            }

            m.Base = baseN; m.Jets = jets; m.Seat = seatN; m.Backrest = back; m.Crest = crest; m.MaxSeat = maxSeat; m.Visual = visual;
            m.Height = 0.6f + 1.3f + 0.25f;
            m.SetJets(true);
            return Finish(m, "SoloPod");
        }
    }
}
