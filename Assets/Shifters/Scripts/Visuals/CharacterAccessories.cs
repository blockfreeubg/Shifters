// =====================================================================================
//  SHIFTERS - CharacterAccessories.cs   (VISUALS module)
//  Hair styles (0 bald, 1 tuft, 2 mohawk, 3 side part / flat-top, 4 afro puff), the 11
//  AccessoryTypes and archetype extras (Sprinter sweatband, Brainiac glasses + lab coat).
//  All meshes are merged per material and cached by parameter keys.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class CharacterAccessories
    {
        static string F(float v) => VisualMeshes.F(v);

        /// <summary>Adds the hair (and mustache) mesh. Returns the hair top (head-pivot local y).</summary>
        public static float AddHair(CharacterRig rig, CharacterProfile p, Vector3 c, Vector3 R)
        {
            int style = Mathf.Clamp(p.Body.HairStyle, 0, 4);
            var acc = p.Body.Accessory;
            if (acc == AccessoryType.HardHat || acc == AccessoryType.Beanie || acc == AccessoryType.PropellerCap) style = 0; // hats replace the hair
            bool brute = p.Archetype == Archetype.Brute;
            bool mustache = p.Body.Accessory == AccessoryType.Mustache;
            float top = c.y + R.y + (style == 1 ? R.y * 0.3f : style == 2 ? R.y * 0.5f : style == 3 ? R.y * (brute ? 0.14f : 0.2f) : style == 4 ? R.y * 0.6f : 0f);
            if (style == 0 && !mustache) return top;
            float er = rig.D.EyeR;
            var face = rig.Face;
            Vector3 eyeL = face != null && face.SocketL != null ? face.SocketL.localPosition : c;
            Vector3 mouth = face != null && face.Mouth != null ? face.Mouth.localPosition : c;
            string key = "hair_" + p.Archetype + "_" + style + "_" + VisualMeshes.V(R) + (mustache ? "_m" + F(er) + "_" + VisualMeshes.V(mouth) : "");
            var mesh = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var w = Color.white;
                switch (style)
                {
                    case 1:
                        for (int i = 0; i < 3; i++)
                        {
                            var rot = Quaternion.Euler(-18f, 0f, -(i - 1) * 30f);
                            b.AddCone(c + new Vector3((i - 1) * R.x * 0.16f, R.y * 0.86f, R.z * 0.12f), R.x * 0.2f, R.y * (i == 1 ? 0.55f : 0.4f), rot, w, 7);
                        }
                        break;
                    case 2:
                        for (int i = 0; i < 6; i++)
                        {
                            float u = i / 5f, z = Mathf.Lerp(0.55f, -0.85f, u) * R.z;
                            float y = R.y * Mathf.Sqrt(Mathf.Max(0.05f, 1f - (z / R.z) * (z / R.z)));
                            var n = new Vector3(0f, y / R.y, z / R.z).normalized;
                            b.AddCone(c + new Vector3(0f, y * 0.9f, z * 0.9f), R.x * 0.17f, R.y * (0.62f - 0.3f * Mathf.Abs(u - 0.3f)), Quaternion.FromToRotation(Vector3.up, n), w, 6);
                        }
                        break;
                    case 3:
                        if (brute) b.AddBox(c + new Vector3(0f, R.y * 0.86f, -R.z * 0.02f), new Vector3(R.x * 1.96f, R.y * 0.36f, R.z * 1.88f), Quaternion.identity, w, R.y * 0.1f);
                        else
                        {
                            b.AddSphere(c + new Vector3(0f, R.y * 0.3f, -R.z * 0.15f), 1f, Quaternion.identity, w, 16, 10, true, new Vector3(R.x * 1.05f, R.y * 0.86f, R.z * 1.02f));
                            b.AddSphere(c + new Vector3(R.x * 0.3f, R.y * 0.82f, R.z * 0.4f), 1f, Quaternion.Euler(0f, 0f, -18f), w, 12, 8, true, new Vector3(R.x * 0.58f, R.y * 0.24f, R.z * 0.48f));
                        }
                        break;
                    case 4:
                        {
                            float s = R.x;
                            b.AddSphere(c + new Vector3(0f, R.y * 0.82f, -R.z * 0.1f), s * 0.68f, Quaternion.identity, w, 12, 8, true, Vector3.one);
                            b.AddSphere(c + new Vector3(-R.x * 0.62f, R.y * 0.5f, -R.z * 0.15f), s * 0.52f, Quaternion.identity, w, 12, 8, true, Vector3.one);
                            b.AddSphere(c + new Vector3(R.x * 0.62f, R.y * 0.5f, -R.z * 0.15f), s * 0.52f, Quaternion.identity, w, 12, 8, true, Vector3.one);
                            b.AddSphere(c + new Vector3(0f, R.y * 0.45f, -R.z * 0.6f), s * 0.6f, Quaternion.identity, w, 12, 8, true, Vector3.one);
                            b.AddSphere(c + new Vector3(0f, R.y * 0.8f, R.z * 0.35f), s * 0.46f, Quaternion.identity, w, 12, 8, true, Vector3.one);
                            break;
                        }
                }
                if (mustache)
                {
                    float my = Mathf.Lerp(mouth.y, eyeL.y, 0.36f);
                    float k = brute ? 1.3f : 1f;
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var pos = new Vector3(mouth.x + sgn * er * 0.72f * k, my, mouth.z + er * 0.18f);
                        b.AddSphere(pos, 1f, Quaternion.Euler(0f, sgn * 10f, -sgn * 16f), w, 10, 7, true, new Vector3(er * 0.9f * k, er * 0.34f * k, er * 0.34f * k));
                    }
                }
                return b.ToMesh("Hair");
            });
            ProcMesh.Part("Hair", rig.Head, mesh, rig.Mats.Hair, Vector3.zero);
            return top;
        }

        static bool CoversHead(AccessoryType a) =>
            a == AccessoryType.HardHat || a == AccessoryType.Headband || a == AccessoryType.PropellerCap || a == AccessoryType.Beanie ||
            a == AccessoryType.Visor || a == AccessoryType.Headphones || a == AccessoryType.Crown;

        static float RadiusAt(Vector3 R, float yRel, bool brute)
        {
            if (brute) return 0.98f;
            float t = Mathf.Clamp(yRel / R.y, -0.98f, 0.98f);
            return Mathf.Sqrt(1f - t * t);
        }

        static Mesh Band(Vector3 R, float yRel, float halfH, bool brute, float grow)
        {
            float k = RadiusAt(R, yRel, brute) * grow;
            string key = "band_" + VisualMeshes.V(R) + "_" + F(yRel) + "_" + F(halfH) + (brute ? "_b" : "") + F(grow);
            return VisualMeshes.Lathe(key, new[]
            {
                new Vector4(-halfH, 0f, 0f, 0f), new Vector4(-halfH, R.x * k, R.z * k, 0f), new Vector4(halfH, R.x * k, R.z * k, 0f), new Vector4(halfH, 0f, 0f, 0f)
            }, brute ? 3.2f : 2f, 18);
        }

        public static void AddAccessory(CharacterRig rig, CharacterProfile p, Vector3 c, Vector3 R, System.Func<float, float, float> surf)
        {
            var m = rig.Mats; var head = rig.Head; bool brute = p.Archetype == Archetype.Brute;
            float lift = Mathf.Max(0f, rig.D.HairTop - (c.y + R.y)) * 0.8f;
            string rk = VisualMeshes.V(R) + "_" + F(lift);
            switch (p.Body.Accessory)
            {
                case AccessoryType.HardHat:
                    {
                        var mesh = ProcMesh.Cached("acc_hardhat_" + rk + (brute ? "b" : ""), () =>
                        {
                            var b = new ProcMesh.Builder();
                            // the Brute's boxy (squircle) skull needs a lower, taller, squarer shell or it pokes through
                            float y0 = R.y * (brute ? 0.12f : 0.3f) + lift, hh = R.y * (brute ? 1.02f : 0.78f), sr = brute ? 1.16f : 1.12f;
                            b.AddMesh(VisualMeshes.Dome(R.x * sr, hh, R.z * (sr + 0.02f), brute ? 3f : 2f), Matrix4x4.Translate(c + new Vector3(0f, y0, 0f)), Color.white);
                            b.AddCylinder(c + new Vector3(0f, y0 + 0.012f, R.z * 0.12f), Mathf.Max(R.x, R.z) * 1.34f, 0.026f, Quaternion.identity, Color.white, 18);
                            b.AddBox(c + new Vector3(0f, y0 + hh * 0.95f, 0f), new Vector3(R.x * 0.28f, R.y * 0.14f, R.z * 1.7f), Quaternion.identity, Color.white, 0.01f);
                            return b.ToMesh("HardHat");
                        });
                        ProcMesh.Part("HardHat", head, mesh, m.Gold, Vector3.zero);
                        break;
                    }
                case AccessoryType.Headband:
                    {
                        float y = R.y * 0.44f + lift * 0.3f;
                        ProcMesh.Part("Headband", head, Band(R, y, R.y * 0.09f, brute, 1.07f), m.Accent, c + new Vector3(0f, y, 0f));
                        var tails = ProcMesh.Cached("acc_tails_" + rk, () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddBox(new Vector3(-0.03f, -0.06f, 0f), new Vector3(0.035f, 0.13f, 0.012f), Quaternion.Euler(0f, 0f, -22f), Color.white, 0.004f);
                            b.AddBox(new Vector3(0.03f, -0.07f, 0f), new Vector3(0.035f, 0.15f, 0.012f), Quaternion.Euler(0f, 0f, 18f), Color.white, 0.004f);
                            return b.ToMesh("HeadbandTails");
                        });
                        ProcMesh.Part("HeadbandTails", head, tails, m.Accent, c + new Vector3(0f, y, -R.z * 1.02f));
                        break;
                    }
                case AccessoryType.Glasses:
                    if (p.Archetype != Archetype.Brainiac) AddGlasses(rig, 1f);
                    break;
                case AccessoryType.PropellerCap:
                    {
                        float y0 = R.y * (brute ? 0.18f : 0.36f) + lift, h = R.y * (brute ? 0.92f : 0.62f);
                        var cap = ProcMesh.Cached("acc_pcap_" + rk + (brute ? "b" : ""), () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddMesh(VisualMeshes.Dome(R.x * (brute ? 1.14f : 1.06f), h, R.z * (brute ? 1.16f : 1.08f), brute ? 3f : 2f), Matrix4x4.Translate(c + new Vector3(0f, y0, 0f)), Color.white);
                            b.AddBox(c + new Vector3(0f, y0 + 0.01f, R.z * 1.02f), new Vector3(R.x * 1.0f, 0.022f, R.z * 0.55f), Quaternion.Euler(-8f, 0f, 0f), Color.white, 0.008f);
                            b.AddCylinder(c + new Vector3(0f, y0 + h + 0.03f, 0f), 0.012f, 0.07f, Quaternion.identity, Color.white, 6);
                            return b.ToMesh("PropCap");
                        });
                        ProcMesh.Part("PropellerCap", head, cap, m.Accent, Vector3.zero);
                        rig.Propeller = CharacterModelBuilder.Pivot("Propeller", head, c + new Vector3(0f, y0 + h + 0.065f, 0f));
                        float span = Mathf.Max(0.22f, R.x * 1.8f);
                        var prop = ProcMesh.Cached("acc_prop_" + F(span), () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddBox(new Vector3(span * 0.25f, 0f, 0f), new Vector3(span * 0.5f, 0.012f, 0.055f), Quaternion.Euler(10f, 0f, 0f), Color.white, 0.004f);
                            b.AddBox(new Vector3(-span * 0.25f, 0f, 0f), new Vector3(span * 0.5f, 0.012f, 0.055f), Quaternion.Euler(-10f, 0f, 0f), Color.white, 0.004f);
                            b.AddSphere(Vector3.zero, 0.022f, Quaternion.identity, Color.white, 8, 6, true, Vector3.one);
                            return b.ToMesh("Propeller");
                        });
                        ProcMesh.Part("Blades", rig.Propeller, prop, m.Gold, Vector3.zero);
                        break;
                    }
                case AccessoryType.Beanie:
                    {
                        float y0 = R.y * (brute ? 0.08f : 0.18f) + lift;
                        var mesh = ProcMesh.Cached("acc_beanie_" + rk + (brute ? "b" : ""), () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddMesh(VisualMeshes.Dome(R.x * (brute ? 1.15f : 1.1f), R.y * (brute ? 1.08f : 0.95f), R.z * (brute ? 1.17f : 1.12f), brute ? 3f : 2f), Matrix4x4.Translate(c + new Vector3(0f, y0, 0f)), Color.white);
                            b.AddMesh(Band(R, y0 + 0.02f, R.y * 0.1f, brute, 1.16f), Matrix4x4.Translate(c + new Vector3(0f, y0 + R.y * 0.08f, 0f)), Color.white);
                            b.AddSphere(c + new Vector3(0f, y0 + R.y * 0.98f, 0f), R.x * 0.26f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
                            return b.ToMesh("Beanie");
                        });
                        ProcMesh.Part("Beanie", head, mesh, m.Accent, Vector3.zero);
                        break;
                    }
                case AccessoryType.Visor:
                    {
                        float y = R.y * 0.42f + lift * 0.3f;
                        var mesh = ProcMesh.Cached("acc_visor_" + rk + (brute ? "b" : ""), () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddMesh(Band(R, y, R.y * 0.1f, brute, 1.07f), Matrix4x4.Translate(c + new Vector3(0f, y, 0f)), Color.white);
                            b.AddBox(c + new Vector3(0f, y + R.y * 0.04f, R.z * 1.12f), new Vector3(R.x * 1.45f, 0.026f, R.z * 0.8f), Quaternion.Euler(12f, 0f, 0f), Color.white, 0.01f);
                            return b.ToMesh("Visor");
                        });
                        ProcMesh.Part("Visor", head, mesh, m.Accent, Vector3.zero);
                        break;
                    }
                case AccessoryType.BowTie:
                    {
                        float s = p.Archetype == Archetype.Brute ? 1.4f : p.Archetype == Archetype.Sprinter ? 0.85f : 1f;
                        var mesh = ProcMesh.Cached("acc_bowtie_" + F(s), () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddCone(new Vector3(-0.085f * s, 0f, 0f), 0.05f * s, 0.08f * s, Quaternion.FromToRotation(Vector3.up, Vector3.right), Color.white, 8);
                            b.AddCone(new Vector3(0.085f * s, 0f, 0f), 0.05f * s, 0.08f * s, Quaternion.FromToRotation(Vector3.up, Vector3.left), Color.white, 8);
                            b.AddSphere(Vector3.zero, 0.026f * s, Quaternion.identity, Color.white, 8, 6, true, Vector3.one);
                            return b.ToMesh("BowTie");
                        });
                        var d = rig.D;
                        float y = brute ? d.TorsoH * 0.86f : d.TorsoH - 0.03f;
                        float z = brute ? d.TorsoD * 0.9f : d.TorsoD * 0.62f + 0.02f;
                        ProcMesh.Part("BowTie", rig.Spine, mesh, m.Accent, new Vector3(0f, y, z));
                        break;
                    }
                case AccessoryType.Headphones:
                    {
                        var band = ProcMesh.Cached("acc_hpband_" + rk, () =>
                        {
                            var b = new ProcMesh.Builder();
                            float ra = R.x * 1.1f, rb = R.y * 1.08f + lift;
                            Vector3 prev = Vector3.zero;
                            for (int i = 0; i <= 10; i++)
                            {
                                float a = Mathf.Lerp(8f, 172f, i / 10f) * Mathf.Deg2Rad;
                                var pt = c + new Vector3(Mathf.Cos(a) * ra, Mathf.Sin(a) * rb, 0f);
                                if (i > 0) { var dir = pt - prev; b.AddBox((pt + prev) * 0.5f, new Vector3(dir.magnitude + 0.012f, 0.028f, 0.05f), Quaternion.FromToRotation(Vector3.right, dir), Color.white, 0.006f); }
                                prev = pt;
                            }
                            return b.ToMesh("HeadphoneBand");
                        });
                        ProcMesh.Part("HeadphoneBand", head, band, m.Dark, Vector3.zero);
                        var cups = ProcMesh.Cached("acc_hpcups_" + rk, () =>
                        {
                            var b = new ProcMesh.Builder();
                            for (int sgn = -1; sgn <= 1; sgn += 2)
                                b.AddCylinder(c + new Vector3(sgn * R.x * 1.04f, R.y * 0.05f, 0f), R.y * 0.38f, 0.075f, Quaternion.Euler(0f, 0f, 90f), Color.white, 14, true);
                            return b.ToMesh("HeadphoneCups");
                        });
                        ProcMesh.Part("HeadphoneCups", head, cups, m.Accent, Vector3.zero);
                        break;
                    }
                case AccessoryType.Crown:
                    {
                        float y0 = brute ? R.y * 0.82f : R.y * 0.76f;
                        y0 += lift;
                        var mesh = ProcMesh.Cached("acc_crown_" + rk, () =>
                        {
                            var b = new ProcMesh.Builder();
                            float rr = R.x * (brute ? 0.75f : 0.62f), h = R.y * 0.24f;
                            b.AddCylinder(new Vector3(0f, h * 0.5f, 0f), rr, h, Quaternion.identity, Color.white, 14);
                            for (int i = 0; i < 5; i++)
                            {
                                float a = i / 5f * Mathf.PI * 2f + 0.3f;
                                b.AddCone(new Vector3(Mathf.Cos(a) * rr * 0.92f, h * 0.95f, Mathf.Sin(a) * rr * 0.92f), R.x * 0.12f, R.y * 0.3f, Quaternion.identity, Color.white, 6);
                            }
                            return b.ToMesh("Crown");
                        });
                        ProcMesh.Part("Crown", head, mesh, m.Gold, c + new Vector3(0f, y0, 0f), Quaternion.Euler(0f, 0f, -9f));
                        break;
                    }
                case AccessoryType.Antenna:
                    {
                        float len = p.Archetype == Archetype.Brainiac ? 0.3f : 0.24f;
                        rig.AntennaPivot = CharacterModelBuilder.Pivot("Antenna", head, new Vector3(0f, rig.D.HairTop - 0.02f, -R.z * 0.1f));
                        var stalk = ProcMesh.Cached("acc_antenna_" + F(len), () => new ProcMesh.Builder().AddCylinder(new Vector3(0f, len * 0.5f, 0f), 0.011f, len, Quaternion.identity, Color.white, 6).ToMesh("Stalk"));
                        ProcMesh.Part("Stalk", rig.AntennaPivot, stalk, m.Dark, Vector3.zero);
                        ProcMesh.Part("Bulb", rig.AntennaPivot, VisualMeshes.Sphere(0.045f, 10, 8), m.Glow, new Vector3(0f, len + 0.03f, 0f));
                        break;
                    }
            }
        }

        static void AddGlasses(CharacterRig rig, float scale)
        {
            var f = rig.Face; if (f == null || f.SocketL == null) return;
            float er = rig.D.EyeR;
            var eL = f.SocketL.localPosition; var eR = f.SocketR.localPosition;
            float major = er * 1.28f * scale, minor = er * 0.13f * Mathf.Sqrt(scale);
            string key = "glasses_" + VisualMeshes.V(eL) + "_" + F(er) + "_" + F(scale) + "_" + F(rig.D.HeadHalf.x);
            float hx = rig.D.HeadHalf.x;
            var mesh = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float z = eL.z + er * 0.82f;
                var rot = Quaternion.Euler(90f, 0f, 0f);
                b.AddTorus(new Vector3(eL.x, eL.y, z), major, minor, rot, Color.white, 18, 6);
                b.AddTorus(new Vector3(eR.x, eR.y, z), major, minor, rot, Color.white, 18, 6);
                float gap = Mathf.Max(0.015f, (eR.x - eL.x) - 2f * major + minor * 2f);
                b.AddBox(new Vector3(0f, eL.y + er * 0.3f, z), new Vector3(gap + minor * 2f, minor * 1.6f, minor * 1.6f), Quaternion.identity, Color.white, 0f);
                float tx = Mathf.Min(hx * 0.97f, Mathf.Abs(eL.x) + major);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    b.AddBox(new Vector3(sgn * tx, eL.y + er * 0.2f, z * 0.5f - 0.01f), new Vector3(minor * 1.5f, minor * 1.5f, z + 0.02f), Quaternion.identity, Color.white, 0f);
                return b.ToMesh("Glasses");
            });
            ProcMesh.Part("Glasses", rig.Head, mesh, rig.Mats.Dark, Vector3.zero, null, null, false);
        }

        public static void AddArchetypeExtras(CharacterRig rig, CharacterProfile p, Vector3 c, Vector3 R, System.Func<float, float, float> surf)
        {
            var m = rig.Mats; var d = rig.D;
            switch (p.Archetype)
            {
                case Archetype.Sprinter:
                    if (CoversHead(p.Body.Accessory))
                    {
                        float r = d.ForeR1 * 1.45f;
                        var band = VisualMeshes.Lathe("wrist_" + F(r), new[] { new Vector4(-0.035f, 0f, 0f, 0f), new Vector4(-0.035f, r, r, 0f), new Vector4(0.035f, r, r, 0f), new Vector4(0.035f, 0f, 0f, 0f) }, 2f, 12);
                        ProcMesh.Part("Sweatband", rig.ElbowR, band, m.Accent, new Vector3(0f, -d.ForeLen * 0.8f, 0f));
                    }
                    else
                    {
                        float y = R.y * 0.48f;
                        ProcMesh.Part("Sweatband", rig.Head, Band(R, y, R.y * 0.11f, false, 1.08f), m.Accent, c + new Vector3(0f, y, 0f));
                    }
                    break;
                case Archetype.Brainiac:
                    AddGlasses(rig, p.Body.Accessory == AccessoryType.Glasses ? 1.5f : 1.3f);
                    {
                        float yb = -(d.WaistY - 0.12f), ww = d.WaistW, t = d.TorsoD, h = d.TorsoH;
                        var coat = ProcMesh.Cached("labcoat_" + F(yb) + "_" + F(ww) + "_" + F(t) + "_" + F(h) + "_" + F(d.ChestW), () =>
                        {
                            var b = new ProcMesh.Builder();
                            b.AddMesh(VisualMeshes.BuildLathe(new[]
                            {
                                new Vector4(yb, 0f, 0f, 0f), new Vector4(yb, ww * 1.4f, t * 1.36f, -0.01f), new Vector4(yb + 0.035f, ww * 1.38f, t * 1.33f, -0.01f),
                                new Vector4(-0.03f, ww * 1.12f, t * 1.1f, 0f), new Vector4(0.07f, ww * 0.96f, t * 0.96f, 0f), new Vector4(0.07f, 0f, 0f, 0f)
                            }, 2f, 18, "Skirt"), Matrix4x4.identity, Color.white);
                            float cr = d.ChestW * 0.6f, cd = t * 0.62f;
                            b.AddMesh(VisualMeshes.BuildLathe(new[]
                            {
                                new Vector4(-0.035f, 0f, 0f, 0f), new Vector4(-0.035f, cr, cd, 0f), new Vector4(0.03f, cr * 1.1f, cd * 1.1f, 0f), new Vector4(0.03f, 0f, 0f, 0f)
                            }, 2f, 16, "Collar"), Matrix4x4.Translate(new Vector3(0f, h - 0.03f, 0f)), Color.white);
                            return b.ToMesh("LabCoat");
                        });
                        ProcMesh.Part("LabCoat", rig.Spine, coat, m.Coat, Vector3.zero);
                    }
                    break;
            }
        }
    }
}
