// =====================================================================================
//  SHIFTERS - VillainModelBuilder.cs   (VISUALS module, campaign wave)
//  MAXIMILIAN ME ("Max Me"), Supreme Sole Executive of Earth, on the shared character rig
//  (CharacterAnimator + FaceController drive him like an intern). Build sheet (CAMPAIGN 2.1):
//  1.20 m (1.45 with crown), pear torso, grey pinstripe suit + 3 gold buttons, x1.3 head with a
//  huge chin, small eyes, heavy brows, curly grey mustache, gold monocle on a chain, a gold crown
//  two sizes too big ("ME"), grey cape with a gold collar, a one-man band on his back (bass drum
//  "ME", knee cymbals, kazoo on a neck holder). Grey + gold only. Hologram variant swaps every
//  material for the gold hologram. Feet at the origin, facing +Z. <= 35 renderers (crown look).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Max's rig: the shared CharacterRig + his props.</summary>
    public class VillainRig : CharacterRig
    {
        public Transform CrownPivot, Crown, Cape, Drum, CymbalL, CymbalR, Kazoo, Mustache, HatHead, HatBody, HatHand, Monocle;
        public Vector3 CrownRestPos; public Quaternion CrownRestRot;
        public Vector3 HeadC;            // head center (head-local)
        public Vector3 HeadR;            // head half extents
        public float FrontZ;             // head front surface z at the eyes (head-local, from HeadC)
        public VillainHat Hat = VillainHat.Crown;
        public bool Hologram { get; internal set; }
        public bool Gold => IsGold;
        internal readonly Dictionary<Renderer, Material> Original = new Dictionary<Renderer, Material>();
        /// <summary>World position of the kazoo tip (fanfare puffs).</summary>
        public Vector3 KazooTip => Kazoo != null ? Kazoo.TransformPoint(new Vector3(0f, 0f, 0.07f)) : HeadCenterWorld;
    }

    public static class VillainModelBuilder
    {
        public const float Height = 1.2f, HeightWithCrown = 1.45f, Mass = 45f;
        static readonly List<Renderer> _tmpR = new List<Renderer>(48);
        static readonly List<MeshFilter> _tmpF = new List<MeshFilter>(48);

        /// <summary>Max as a CharacterProfile (materials + animator defaults). Name "MAX ME".</summary>
        public static CharacterProfile Profile()
        {
            var p = new CharacterProfile
            {
                Name = "MAX ME", Archetype = Archetype.Brute, Title = "SUPREME SOLE EXECUTIVE",
                Personality = "Does 400 jobs. Alone.", Catchphrase = "There's no TEAM in 'ME'.",
                PrimaryColor = VillainLook.SuitGrey, SecondaryColor = VillainLook.SuitDark, Mass = Mass, VoicePitch = 0.62f
            };
            p.Body.Height = Height; p.Body.HairStyle = 0; p.Body.Accessory = AccessoryType.None;
            p.Body.SkinTone = new Color(0.86f, 0.82f, 0.78f);   // pale grey-beige: grey world, still a face
            p.Body.HairColor = new Color(0.62f, 0.63f, 0.66f);
            return p;
        }

        public static RigDims Measure()
        {
            var d = new RigDims { Height = Height };
            d.HipY = 0.31f; d.AnkleY = 0.06f; d.LegR = 0.055f; d.Foot = new Vector3(0.11f, 0.075f, 0.2f);
            d.WaistY = 0.35f;
            d.TorsoH = 0.47f; d.ChestW = 0.165f; d.WaistW = 0.27f; d.TorsoD = 0.235f;
            d.ThighLen = d.ShinLen = (d.HipY - d.AnkleY) * 0.5f;
            d.HipX = 0.1f;
            d.ShoulderY = d.TorsoH - 0.08f; d.ShoulderX = d.ChestW + 0.03f;
            d.UpperLen = 0.2f; d.ForeLen = 0.18f; d.ArmR = 0.045f; d.ForeR0 = 0.043f; d.ForeR1 = 0.04f; d.HandR = 0.058f; d.ArmSplay = 24f;
            d.NeckY = d.TorsoH + 0.0f; d.HeadZ = 0.025f;
            d.HeadHalf = new Vector3(0.2f, 0.2f, 0.185f);
            d.HeadCenterY = 0.19f;
            d.EyeR = 0.04f;
            d.HairTop = d.HeadCenterY + d.HeadHalf.y + 0.26f;   // crown top
            return d;
        }

        // ------------------------------------------------------------------ build
        /// <summary>Build Max under parent (child "Model"). hologram = gold hologram look.</summary>
        public static VillainRig Build(Transform parent, bool hologram = false, int seed = 0)
        {
            var p = Profile();
            var d = Measure();
            var m = new CharacterMaterials(p);
            m.Shoe = m.Extra(new Color(0.12f, 0.12f, 0.14f), 0.85f);
            var stripe = m.Extra(new Color(0.33f, 0.35f, 0.38f), 0.25f);
            var drumHead = m.Extra(new Color(0.84f, 0.85f, 0.87f), 0.4f);
            var glove = m.Coat;
            var rig = new VillainRig { Profile = p, D = d, Mats = m };
            var root = new GameObject("Model").transform;
            root.SetParent(parent, false);
            rig.Root = root;

            // ---------------- hips + legs
            rig.Hips = CharacterModelBuilder.Pivot("Hips", root, new Vector3(0f, d.HipY, 0f));
            rig.HipsRest = rig.Hips.localPosition;
            ProcMesh.Part("Pelvis", rig.Hips, VisualMeshes.Lathe("vil_pelvis", new[]
            {
                new Vector4(-0.09f, 0f, 0f, 0f), new Vector4(-0.09f, 0.17f, 0.15f, 0f), new Vector4(-0.03f, 0.25f, 0.21f, 0f),
                new Vector4(0.06f, 0.26f, 0.22f, 0f), new Vector4(0.06f, 0f, 0f, 0f)
            }, 2.3f, 14), m.Secondary, Vector3.zero);
            rig.LegL = CharacterModelBuilder.Leg(rig, true, d, m, out rig.KneeL, out rig.FootL);
            rig.LegR = CharacterModelBuilder.Leg(rig, false, d, m, out rig.KneeR, out rig.FootR);
            // knee cymbals (inner side of each knee)
            var cym = ProcMesh.Cached("vil_cymbal", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(Vector3.zero, 0.065f, 0.008f, Quaternion.Euler(0f, 0f, 90f), Color.white, 14);
                b.AddSphere(new Vector3(0.006f, 0f, 0f), 0.022f, Quaternion.identity, Color.white, 8, 5, true, new Vector3(0.5f, 1f, 1f));
                return b.ToMesh("Cymbal");
            });
            rig.CymbalL = ProcMesh.Part("CymbalL", rig.KneeL, cym, m.Gold, new Vector3(0.07f, -0.005f, 0.01f), Quaternion.Euler(0f, 180f, 0f)).transform;
            rig.CymbalR = ProcMesh.Part("CymbalR", rig.KneeR, cym, m.Gold, new Vector3(-0.07f, -0.005f, 0.01f)).transform;

            // ---------------- spine + pear torso
            rig.Spine = CharacterModelBuilder.Pivot("Spine", rig.Hips, new Vector3(0f, d.WaistY - d.HipY, 0f));
            float h = d.TorsoH, ww = d.WaistW, cw = d.ChestW, t = d.TorsoD;
            var rings = new[]
            {
                new Vector4(-0.06f, 0f, 0f, 0f), new Vector4(-0.06f, ww * 0.9f, t * 0.86f, 0f), new Vector4(0.14f * h, ww * 1.06f, t * 1.04f, 0.02f),
                new Vector4(0.36f * h, ww * 1.02f, t * 1.02f, 0.026f), new Vector4(0.6f * h, cw * 1.3f, t * 0.86f, 0.012f),
                new Vector4(0.84f * h, cw * 1.08f, t * 0.68f, 0f), new Vector4(h, cw * 0.62f, t * 0.44f, 0f), new Vector4(h, 0f, 0f, 0f)
            };
            rig.TorsoMesh = ProcMesh.Part("Torso", rig.Spine, VisualMeshes.Lathe("vil_torso", rings, 2.3f, 18), m.Primary, Vector3.zero).transform;
            // pinstripes: thin darker ribbons hugging the belly (one merged mesh)
            ProcMesh.Part("Pinstripes", rig.Spine, ProcMesh.Cached("vil_stripes", () =>
            {
                var b = new ProcMesh.Builder();
                for (int i = -3; i <= 3; i++)
                {
                    if (i == 0) continue;
                    float ang = i * 17f * Mathf.Deg2Rad;
                    for (int s = 0; s < 6; s++)
                    {
                        float y0 = Mathf.Lerp(0.02f, 0.74f * h, s / 6f), y1 = Mathf.Lerp(0.02f, 0.74f * h, (s + 1) / 6f);
                        Vector3 P(float y) { RingAt(rings, y, out float rx, out float rz, out float zo); return new Vector3(Mathf.Sin(ang) * rx * 1.01f, y, Mathf.Cos(ang) * rz * 1.01f + zo); }
                        var a = P(y0); var c2 = P(y1);
                        var mid = (a + c2) * 0.5f; var dir = c2 - a;
                        b.AddBox(mid, new Vector3(0.006f, dir.magnitude + 0.004f, 0.004f), Quaternion.LookRotation(new Vector3(mid.x, 0f, mid.z).normalized, dir.normalized) * Quaternion.Euler(0f, 0f, 0f), Color.white);
                    }
                }
                return b.ToMesh("Pinstripes");
            }), stripe, Vector3.zero, null, null, false);

            // gold details on the spine: 3 buttons, stand-up collar
            ProcMesh.Part("SuitGold", rig.Spine, ProcMesh.Cached("vil_suitgold", () =>
            {
                var b = new ProcMesh.Builder();
                for (int i = 0; i < 3; i++)
                {
                    float y = h * (0.2f + 0.16f * i);
                    RingAt(rings, y, out _, out float rz, out float zo);
                    b.AddSphere(new Vector3(0f, y, rz + zo - 0.004f), 0.022f, Quaternion.identity, Color.white, 10, 6, true, new Vector3(1f, 1f, 0.55f));
                }
                // stand-up collar behind the head: open arc band, flaring outward
                const int seg = 14;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.Lerp(-135f, 135f, i / (float)seg) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-135f, 135f, (i + 1) / (float)seg) * Mathf.Deg2Rad;
                    Vector3 Q(float a, float yy, float r) => new Vector3(Mathf.Sin(a) * r, yy, -Mathf.Cos(a) * r * 0.9f + 0.01f);
                    float y0 = h - 0.04f, y1 = h + 0.16f;
                    var p0 = Q(a0, y0, 0.15f); var p1 = Q(a1, y0, 0.15f); var p2 = Q(a1, y1, 0.22f); var p3 = Q(a0, y1, 0.22f);
                    b.Quad(p0, p1, p2, p3, Color.white); b.Quad(p3, p2, p1, p0, Color.white);
                }
                return b.ToMesh("SuitGold");
            }), m.Gold, Vector3.zero);

            // cape (one ribbon, spring sway on the "Cape" pivot)
            rig.Cape = CharacterModelBuilder.Pivot("Cape", rig.Spine, new Vector3(0f, h - 0.02f, -t * 0.55f));
            ProcMesh.Part("CapeMesh", rig.Cape, ProcMesh.Cached("vil_cape", () =>
            {
                var b = new ProcMesh.Builder();
                const int nx = 8, ny = 4; float len = 0.34f;
                Vector3 V(int ix, int iy)
                {
                    float u = ix / (float)nx * 2f - 1f, v = iy / (float)ny;
                    float w = Mathf.Lerp(0.2f, 0.34f, v);
                    float z = -0.02f - (1f - u * u) * Mathf.Lerp(0.03f, 0.07f, v) + u * u * Mathf.Lerp(0.06f, 0.16f, v) + Mathf.Sin(u * 9f) * 0.01f * v;
                    return new Vector3(u * w, -v * len, z);
                }
                for (int ix = 0; ix < nx; ix++)
                    for (int iy = 0; iy < ny; iy++)
                    {
                        var a = V(ix, iy); var c = V(ix + 1, iy); var e = V(ix + 1, iy + 1); var f = V(ix, iy + 1);
                        b.Quad(a, c, e, f, Color.white, new Vector3(0f, 0f, -1f));
                        b.Quad(f, e, c, a, Color.white, new Vector3(0f, 0f, 1f));
                    }
                return b.ToMesh("Cape");
            }), m.Secondary, Vector3.zero);

            // one-man band: bass drum on his back (axis X, 0.7 m)
            rig.Drum = CharacterModelBuilder.Pivot("Drum", rig.Spine, new Vector3(0f, h * 0.6f, -(t * 0.95f + 0.19f)));
            ProcMesh.Part("DrumBody", rig.Drum, ProcMesh.Cached("vil_drum", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(Vector3.zero, 0.35f, 0.26f, Quaternion.Euler(90f, 0f, 0f), Color.white, 22, true);
                return b.ToMesh("Drum");
            }), drumHead, Vector3.zero);
            ProcMesh.Part("DrumGold", rig.Drum, ProcMesh.Cached("vil_drumgold", () =>
            {
                var b = new ProcMesh.Builder();
                var ax = Quaternion.Euler(90f, 0f, 0f);
                b.AddTorus(new Vector3(0f, 0f, 0.13f), 0.352f, 0.024f, ax, Color.white, 24, 6);    // front rim: a gold halo behind him
                b.AddTorus(new Vector3(0f, 0f, -0.13f), 0.352f, 0.024f, ax, Color.white, 24, 6);
                b.AddCylinder(Vector3.zero, 0.356f, 0.1f, ax, Color.white, 24, true, false);        // gold shell band
                // "ME" on the back drum head (identity frame reads from -Z = from behind him)
                Letters(b, Matrix4x4.TRS(new Vector3(0.02f, 0f, -0.135f), Quaternion.identity, Vector3.one * 0.12f), 0.17f);
                // beater on a little arm over his right shoulder
                b.AddCylinder(new Vector3(0.3f, 0.3f, 0.1f), 0.012f, 0.24f, Quaternion.Euler(0f, 0f, -35f), Color.white, 6);
                b.AddSphere(new Vector3(0.37f, 0.4f, 0.1f), 0.042f, Quaternion.identity, Color.white, 8, 6);
                return b.ToMesh("DrumGold");
            }), m.Gold, Vector3.zero);

            // ---------------- arms (suit sleeves, white gloves)
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0; float sx = left ? -1f : 1f;
                var sh = CharacterModelBuilder.Pivot(left ? "ShoulderL" : "ShoulderR", rig.Spine, new Vector3(sx * d.ShoulderX, d.ShoulderY, 0f));
                ProcMesh.Part("UpperArm", sh, VisualMeshes.Limb(d.ArmR, d.UpperLen), m.Primary, Vector3.zero);
                var el = CharacterModelBuilder.Pivot(left ? "ElbowL" : "ElbowR", sh, new Vector3(0f, -d.UpperLen, 0f));
                ProcMesh.Part("Forearm", el, VisualMeshes.TaperLimb(d.ForeR0, d.ForeR1, d.ForeLen), m.Primary, Vector3.zero);
                var hand = CharacterModelBuilder.Pivot(left ? "HandL" : "HandR", el, new Vector3(0f, -d.ForeLen, 0f));
                ProcMesh.Part("Hand", hand, ProcMesh.Cached("vil_glove", () =>
                {
                    var b = new ProcMesh.Builder();
                    b.AddSphere(new Vector3(0f, -0.04f, 0f), 1f, Quaternion.identity, Color.white, 12, 8, true, new Vector3(0.05f, 0.058f, 0.045f));
                    b.AddCylinder(new Vector3(0f, 0.004f, 0f), 0.052f, 0.03f, Quaternion.identity, Color.white, 12, true);   // cuff
                    b.AddSphere(new Vector3(0.045f, -0.035f, 0.02f), 1f, Quaternion.Euler(0f, 0f, 30f), Color.white, 8, 5, true, new Vector3(0.018f, 0.032f, 0.018f)); // thumb
                    return b.ToMesh("Glove");
                }), glove, Vector3.zero);
                if (left) { rig.ShoulderL = sh; rig.ElbowL = el; rig.HandL = hand; rig.ShoulderLRest = sh.localPosition; }
                else
                {
                    rig.ShoulderR = sh; rig.ElbowR = el; rig.HandR = hand; rig.ShoulderRRest = sh.localPosition;
                    rig.WeaponSocket = CharacterModelBuilder.Pivot("WeaponSocket", hand, new Vector3(0f, -d.HandR * 0.75f, d.HandR * 0.15f));
                    rig.WeaponSocket.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            // ---------------- head (x1.3, huge chin, small eyes, heavy brows)
            rig.Head = CharacterModelBuilder.Pivot("Head", rig.Spine, new Vector3(0f, d.NeckY, d.HeadZ));
            var c = new Vector3(0f, d.HeadCenterY, 0f);
            var R = d.HeadHalf;
            var chinC = new Vector3(0f, -R.y * 0.78f, R.z * 0.32f); var chinR = new Vector3(R.x * 0.74f, R.y * 0.46f, R.z * 0.66f);
            float HeadSurf(float x, float y) => R.z * Mathf.Sqrt(Mathf.Max(0.02f, 1f - (x / R.x) * (x / R.x) - (y / R.y) * (y / R.y)));
            float ChinSurf(float x, float y)
            {
                float ux = x / chinR.x, uy = (y - chinC.y) / chinR.y, k = 1f - ux * ux - uy * uy;
                return k <= 0f ? -1f : chinC.z + chinR.z * Mathf.Sqrt(k);
            }
            System.Func<float, float, float> surf = (x, y) => Mathf.Max(HeadSurf(x, y), ChinSurf(x, y));
            rig.HeadC = c; rig.HeadR = R;
            rig.HeadMesh = ProcMesh.Part("HeadMesh", rig.Head, VisualMeshes.Ellipsoid(R, 18, 12), m.Skin, c).transform;
            rig.Face = FaceController.Build(rig.Head, c, R, Archetype.Brute, d.EyeR, m, 0, surf, seed ^ 0x3A5);
            rig.FrontZ = surf(0f, 0f);
            // heavy brows, mouth lowered onto the chin
            if (rig.Face.BrowL != null) rig.Face.BrowL.localScale = new Vector3(1.35f, 1.9f, 1.4f);
            if (rig.Face.BrowR != null) rig.Face.BrowR.localScale = new Vector3(1.35f, 1.9f, 1.4f);
            float mouthY = -R.y * 0.58f;
            if (rig.Face.Mouth != null) rig.Face.Mouth.localPosition = c + new Vector3(0f, mouthY, surf(0f, mouthY) + 0.004f);

            // chin + bulb nose + ears (one skin mesh)
            float noseY = -R.y * 0.14f;
            ProcMesh.Part("HeadExtras", rig.Head, ProcMesh.Cached("vil_headx", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddSphere(chinC, 1f, Quaternion.identity, Color.white, 16, 10, true, chinR);
                b.AddSphere(new Vector3(0f, noseY, HeadSurf(0f, noseY) + 0.012f), 1f, Quaternion.identity, Color.white, 10, 7, true, new Vector3(0.042f, 0.036f, 0.04f));
                b.AddSphere(new Vector3(-R.x * 0.98f, -R.y * 0.05f, -R.z * 0.05f), 1f, Quaternion.identity, Color.white, 8, 6, true, new Vector3(0.03f, 0.055f, 0.04f));
                b.AddSphere(new Vector3(R.x * 0.98f, -R.y * 0.05f, -R.z * 0.05f), 1f, Quaternion.identity, Color.white, 8, 6, true, new Vector3(0.03f, 0.055f, 0.04f));
                return b.ToMesh("HeadExtras");
            }), m.Skin, c, null, null, false);

            // curly grey mustache (x1.4) between nose and mouth
            float mY = (noseY + mouthY) * 0.5f - 0.004f;
            float mZ = surf(0f, mY) + 0.012f;
            rig.Mustache = ProcMesh.Part("Mustache", rig.Head, ProcMesh.Cached("vil_mustache", () =>
            {
                var b = new ProcMesh.Builder();
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    b.AddSphere(new Vector3(sgn * 0.045f, 0f, 0f), 1f, Quaternion.Euler(0f, -sgn * 14f, -sgn * 12f), Color.white, 10, 7, true, new Vector3(0.058f, 0.022f, 0.022f));
                    b.AddSphere(new Vector3(sgn * 0.095f, 0.012f, -0.012f), 1f, Quaternion.Euler(0f, -sgn * 30f, -sgn * 40f), Color.white, 8, 6, true, new Vector3(0.032f, 0.014f, 0.014f));
                    b.AddTorus(new Vector3(sgn * 0.118f, 0.034f, -0.022f), 0.018f, 0.0075f, Quaternion.Euler(0f, -sgn * 30f, 0f) * Quaternion.Euler(90f, 0f, 0f), Color.white, 10, 5);
                }
                return b.ToMesh("Mustache");
            }), m.Hair, c + new Vector3(0f, mY, mZ), null, null, false).transform;

            // monocle + chain + kazoo holder (one gold mesh on the head)
            var eyeRP = rig.Face.SocketR != null ? rig.Face.SocketR.localPosition : c + new Vector3(R.x * 0.43f, 0f, rig.FrontZ);
            var kazooPos = c + new Vector3(-0.07f, mouthY - 0.005f, surf(-0.07f, mouthY) + 0.035f);
            rig.Monocle = ProcMesh.Part("HeadGold", rig.Head, ProcMesh.Cached("vil_headgold", () =>
            {
                var b = new ProcMesh.Builder();
                var mono = eyeRP + new Vector3(0f, 0f, d.EyeR * 0.75f);
                b.AddTorus(mono, d.EyeR * 1.55f, 0.008f, Quaternion.Euler(90f, 0f, 0f), Color.white, 18, 6);
                // chain: monocle -> down past the jaw
                Vector3 prev = mono + new Vector3(d.EyeR * 1.1f, -d.EyeR * 1.1f, 0f);
                for (int i = 1; i <= 9; i++)
                {
                    float u = i / 9f;
                    var q = mono + new Vector3(d.EyeR * 1.1f + u * 0.08f, -d.EyeR * 1.1f - u * 0.2f, -u * u * 0.09f + Mathf.Sin(u * 3.1f) * 0.012f);
                    b.AddSphere(q, 0.0075f, Quaternion.identity, Color.white, 6, 4);
                    prev = q;
                }
                // kazoo neck holder wire (collar -> kazoo)
                var w0 = kazooPos + new Vector3(-0.015f, -0.02f, -0.02f); var w1 = new Vector3(-0.12f, -0.24f, 0.04f) + c;
                b.AddCylinder((w0 + w1) * 0.5f, 0.006f, (w1 - w0).magnitude, Quaternion.FromToRotation(Vector3.up, w1 - w0), Color.white, 6);
                return b.ToMesh("HeadGold");
            }), m.Gold, Vector3.zero, null, null, false).transform;
            rig.Kazoo = CharacterModelBuilder.Pivot("Kazoo", rig.Head, kazooPos);
            rig.Kazoo.localRotation = Quaternion.Euler(4f, -28f, 0f);
            ProcMesh.Part("KazooMesh", rig.Kazoo, ProcMesh.Cached("vil_kazoo", () =>
            {
                var b = new ProcMesh.Builder();
                var ax = Quaternion.Euler(90f, 0f, 0f);
                b.AddCylinder(new Vector3(0f, 0f, 0.02f), 0.011f, 0.05f, ax, Color.white, 8, true);
                b.AddCone(new Vector3(0f, 0f, 0.045f), 0.017f, 0.035f, ax, Color.white, 8);
                b.AddCylinder(new Vector3(0f, 0.012f, 0.025f), 0.007f, 0.01f, Quaternion.identity, Color.white, 6);
                return b.ToMesh("Kazoo");
            }), m.Gold, Vector3.zero, null, null, false);

            // crown: two sizes too big, "ME" on the front band, jaunty tilt
            rig.CrownPivot = CharacterModelBuilder.Pivot("CrownPivot", rig.Head, c + new Vector3(0f, R.y * 0.66f, -0.005f));
            rig.CrownRestPos = rig.CrownPivot.localPosition;
            rig.CrownRestRot = Quaternion.Euler(-4f, 0f, -8f);
            rig.CrownPivot.localRotation = rig.CrownRestRot;
            rig.Crown = ProcMesh.Part("Crown", rig.CrownPivot, ProcMesh.Cached("vil_crown", () =>
            {
                var b = new ProcMesh.Builder();
                float rr = R.x * 1.06f, bh = 0.085f;
                b.AddCylinder(new Vector3(0f, bh * 0.5f, 0f), rr, bh, Quaternion.identity, Color.white, 20, true, false);
                b.AddCylinder(new Vector3(0f, bh * 0.5f, 0f), rr * 0.96f, bh, Quaternion.identity, Color.white, 20, true, false);
                b.AddTorus(new Vector3(0f, 0.004f, 0f), rr, 0.012f, Quaternion.identity, Color.white, 20, 5);
                for (int i = 0; i < 5; i++)
                {
                    float a = (i / 5f) * Mathf.PI * 2f;
                    var bp = new Vector3(Mathf.Sin(a) * rr * 0.98f, bh, Mathf.Cos(a) * rr * 0.98f);
                    b.AddCone(bp, 0.045f, 0.12f, Quaternion.identity, Color.white, 6);
                    b.AddSphere(bp + new Vector3(0f, 0.125f, 0f), 0.02f, Quaternion.identity, Color.white, 8, 5);
                }
                return b.ToMesh("Crown");
            }), m.Gold, Vector3.zero).transform;
            ProcMesh.Part("CrownME", rig.CrownPivot, ProcMesh.Cached("vil_crownme", () =>
            {
                var b = new ProcMesh.Builder();
                Letters(b, Matrix4x4.TRS(new Vector3(0f, 0.043f, R.x * 1.06f + 0.004f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.03f), 0.22f);
                return b.ToMesh("CrownME");
            }), m.Dark, Vector3.zero, null, null, false);

            // hat sockets (channel hats are built on demand)
            rig.HatHead = CharacterModelBuilder.Pivot("HatHead", rig.Head, c + new Vector3(0f, R.y * 0.62f, 0f));
            rig.HatBody = CharacterModelBuilder.Pivot("HatBody", rig.Spine, Vector3.zero);
            rig.HatHand = CharacterModelBuilder.Pivot("HatHand", rig.HandL, new Vector3(0f, -0.05f, 0f));

            Collect(rig);
            rig.Face.Snap();
            if (hologram) SetHologram(rig, true);
            return rig;
        }

        /// <summary>Refresh the renderer list + triangle count (after hats change).</summary>
        public static void Collect(CharacterRig rig)
        {
            if (rig?.Root == null) return;
            rig.Root.GetComponentsInChildren(true, _tmpR);
            rig.Renderers = _tmpR.ToArray();
            rig.Root.GetComponentsInChildren(true, _tmpF);
            int tris = 0;
            for (int i = 0; i < _tmpF.Count; i++) if (_tmpF[i].sharedMesh != null && _tmpF[i].gameObject.activeInHierarchy) tris += (int)_tmpF[i].sharedMesh.GetIndexCount(0) / 3;
            rig.Triangles = tris;
            _tmpR.Clear(); _tmpF.Clear();
        }

        /// <summary>Number of active renderers (budget checks).</summary>
        public static int ActiveRenderers(CharacterRig rig)
        {
            int n = 0;
            if (rig?.Renderers == null) return 0;
            for (int i = 0; i < rig.Renderers.Length; i++) if (rig.Renderers[i] != null && rig.Renderers[i].gameObject.activeInHierarchy) n++;
            return n;
        }

        // ------------------------------------------------------------------ hologram / gold
        static Material _holoBody, _holoDark, _holoLight;
        static readonly Color HoloBody = new Color(0.91f, 0.72f, 0.23f, 0.62f);

        /// <summary>Gold hologram look (MaterialLibrary.Hologram #E8B83A); features stay readable (darker, opaque).</summary>
        public static void SetHologram(VillainRig rig, bool on)
        {
            if (rig == null || rig.Hologram == on) return;
            rig.Hologram = on;
            if (on && rig.Gold) SetGoldLook(rig, false);
            if (on)
            {
                if (_holoBody == null) _holoBody = MaterialLibrary.Hologram(HoloBody);
                if (_holoDark == null) _holoDark = MaterialLibrary.Hologram(new Color(0.45f, 0.24f, 0.02f, 0.97f));
                if (_holoLight == null) _holoLight = MaterialLibrary.Hologram(new Color(1f, 0.95f, 0.75f, 0.9f));
            }
            Swap(rig, on, r =>
            {
                string n = r.gameObject.name;
                if (n.StartsWith("Pupil") || n == "Mouth" || n.StartsWith("Brow") || n == "CrownME") return _holoDark;
                if (n.StartsWith("Eye") || n == "Glint") return _holoLight;
                return _holoBody;
            });
            SetShadows(rig, !on);
        }

        /// <summary>Solid gold statue look (founders' statue, trophies).</summary>
        public static void SetGoldLook(CharacterRig rig, bool on)
        {
            if (rig == null) return;
            var vr = rig as VillainRig;
            if (vr != null && on && vr.Hologram) SetHologram(vr, false);
            CharacterGold.Apply(rig, on);
        }

        static void Swap(VillainRig rig, bool on, System.Func<Renderer, Material> pick)
        {
            Collect(rig);
            for (int i = 0; i < rig.Renderers.Length; i++)
            {
                var r = rig.Renderers[i]; if (r == null || r is ParticleSystemRenderer) continue;
                if (on)
                {
                    if (!rig.Original.ContainsKey(r)) rig.Original[r] = r.sharedMaterial;
                    r.sharedMaterial = pick(r);
                }
                else if (rig.Original.TryGetValue(r, out var m)) r.sharedMaterial = m;
            }
            if (!on) rig.Original.Clear();
        }

        static void SetShadows(CharacterRig rig, bool on)
        {
            for (int i = 0; i < rig.Renderers.Length; i++)
                if (rig.Renderers[i] != null) rig.Renderers[i].shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ channel hats
        /// <summary>Swap the channel hat (destroys the previous one). Crown hidden for hats that replace it.</summary>
        public static void SetHat(VillainRig rig, VillainHat hat)
        {
            if (rig == null || rig.HatHead == null) return;
            bool holo = rig.Hologram, gold = rig.Gold;
            if (holo) SetHologram(rig, false);
            if (gold) SetGoldLook(rig, false);
            Clear(rig.HatHead); Clear(rig.HatBody); Clear(rig.HatHand);
            rig.Hat = hat;
            var m = rig.Mats; var R = rig.HeadR;
            float crownLift = 0f; bool crown = true;
            switch (hat)
            {
                case VillainHat.President:
                    crownLift = 0.27f;
                    ProcMesh.Part("TopHat", rig.HatHead, ProcMesh.Cached("vil_tophat", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddCylinder(new Vector3(0f, 0.012f, 0f), R.x * 1.15f, 0.024f, Quaternion.identity, Color.white, 18, true);
                        b.AddCylinder(new Vector3(0f, 0.15f, 0f), R.x * 0.7f, 0.27f, Quaternion.identity, Color.white, 18, true);
                        return b.ToMesh("TopHat");
                    }), m.Dark, Vector3.zero);
                    ProcMesh.Part("HatBand", rig.HatHead, ProcMesh.Cached("vil_tophatband", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddCylinder(new Vector3(0f, 0.06f, 0f), R.x * 0.71f, 0.04f, Quaternion.identity, Color.white, 18, true, false);
                        return b.ToMesh("HatBand");
                    }), m.Gold, Vector3.zero);
                    Sash(rig, m.Gold);
                    break;
                case VillainHat.CEO:
                    ProcMesh.Part("Headset", rig.HatHead, ProcMesh.Cached("vil_headset", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddTorus(new Vector3(0f, -0.06f, -0.01f), R.x * 1.02f, 0.012f, Quaternion.Euler(0f, 0f, 90f), Color.white, 18, 5);
                        b.AddCylinder(new Vector3(R.x * 1.02f, -R.y * 0.62f - 0.0f, 0f), 0.045f, 0.04f, Quaternion.Euler(0f, 0f, 90f), Color.white, 12, true);
                        b.AddCylinder(new Vector3(-R.x * 1.02f, -R.y * 0.62f, 0f), 0.045f, 0.04f, Quaternion.Euler(0f, 0f, 90f), Color.white, 12, true);
                        var m0 = new Vector3(R.x * 1.0f, -R.y * 0.7f, 0.02f); var m1 = new Vector3(R.x * 0.35f, -R.y * 1.15f, R.z * 1.05f);
                        b.AddCylinder((m0 + m1) * 0.5f, 0.008f, (m1 - m0).magnitude, Quaternion.FromToRotation(Vector3.up, m1 - m0), Color.white, 6);
                        b.AddSphere(m1, 0.02f, Quaternion.identity, Color.white, 8, 6);
                        return b.ToMesh("Headset");
                    }), m.Dark, Vector3.zero);
                    Tie(rig, m.Gold);
                    break;
                case VillainHat.Banker:
                    crown = false;
                    ProcMesh.Part("Eyeshade", rig.HatHead, ProcMesh.Cached("vil_eyeshade", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddTorus(new Vector3(0f, -0.07f, 0f), R.x * 0.98f, 0.014f, Quaternion.identity, Color.white, 20, 5);
                        // visor brim: a fan of quads in front
                        for (int i = 0; i < 10; i++)
                        {
                            float a0 = Mathf.Lerp(-70f, 70f, i / 10f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-70f, 70f, (i + 1) / 10f) * Mathf.Deg2Rad;
                            Vector3 P(float a, float r, float y) => new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
                            var p0 = P(a0, R.x * 0.98f, -0.07f); var p1 = P(a1, R.x * 0.98f, -0.07f); var p2 = P(a1, R.x * 1.45f, -0.11f); var p3 = P(a0, R.x * 1.45f, -0.11f);
                            b.Quad(p0, p1, p2, p3, Color.white); b.Quad(p3, p2, p1, p0, Color.white);
                        }
                        return b.ToMesh("Eyeshade");
                    }), m.Gold, Vector3.zero);
                    break;
                case VillainHat.Judge:
                    crownLift = 0.09f;
                    ProcMesh.Part("Wig", rig.HatHead, ProcMesh.Cached("vil_wig", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddSphere(new Vector3(0f, 0.02f, -0.01f), 1f, Quaternion.identity, Color.white, 14, 8, true, new Vector3(R.x * 1.05f, 0.1f, R.z * 1.05f));
                        for (int sgn = -1; sgn <= 1; sgn += 2)
                            for (int i = 0; i < 4; i++)
                                b.AddSphere(new Vector3(sgn * R.x * 1.02f, -0.06f - i * 0.075f, -0.03f - i * 0.01f), 0.048f, Quaternion.identity, Color.white, 10, 6);
                        for (int i = 0; i < 3; i++) b.AddSphere(new Vector3((i - 1) * 0.08f, -0.1f, -R.z * 0.95f), 0.05f, Quaternion.identity, Color.white, 10, 6);
                        return b.ToMesh("Wig");
                    }), m.Coat, Vector3.zero);
                    ProcMesh.Part("Robe", rig.HatBody, VisualMeshes.Lathe("vil_robe", new[]
                    {
                        new Vector4(-0.1f, 0f, 0f, 0f), new Vector4(-0.1f, 0.33f, 0.3f, 0.01f), new Vector4(0.2f, 0.3f, 0.27f, 0.02f),
                        new Vector4(0.36f, 0.2f, 0.2f, 0f), new Vector4(0.47f, 0.12f, 0.1f, 0f), new Vector4(0.47f, 0f, 0f, 0f)
                    }, 2.2f, 16), m.Dark, Vector3.zero);
                    break;
                case VillainHat.Weatherman:
                    crown = false;
                    ProcMesh.Part("UmbrellaHat", rig.HatHead, ProcMesh.Cached("vil_umbrella", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddTorus(new Vector3(0f, -0.05f, 0f), R.x * 0.98f, 0.012f, Quaternion.identity, Color.white, 18, 5);
                        b.AddCylinder(new Vector3(0f, 0.06f, 0f), 0.008f, 0.22f, Quaternion.identity, Color.white, 6);
                        b.AddSphere(new Vector3(0f, 0.2f, 0f), 0.015f, Quaternion.identity, Color.white, 6, 4);
                        return b.ToMesh("UmbrellaStem");
                    }), m.Dark, Vector3.zero);
                    ProcMesh.Part("UmbrellaCanopy", rig.HatHead, ProcMesh.Cached("vil_canopy", () =>
                    {
                        var b = new ProcMesh.Builder();
                        const int n = 8; float r = 0.36f, top = 0.19f, rim = 0.09f;
                        for (int i = 0; i < n; i++)
                        {
                            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
                            var p0 = new Vector3(Mathf.Sin(a0) * r, rim, Mathf.Cos(a0) * r); var p1 = new Vector3(Mathf.Sin(a1) * r, rim, Mathf.Cos(a1) * r);
                            var tp = new Vector3(0f, top, 0f);
                            b.Tri(tp, p1, p0, Color.white); b.Tri(tp, p0, p1, Color.white);
                        }
                        return b.ToMesh("Canopy");
                    }), m.Gold, Vector3.zero);
                    break;
                case VillainHat.Janitor:
                    crown = false;
                    ProcMesh.Part("Cap", rig.HatHead, ProcMesh.Cached("vil_jcap", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddSphere(new Vector3(0f, -0.06f, 0f), 1f, Quaternion.identity, Color.white, 16, 8, true, new Vector3(R.x * 1.02f, 0.13f, R.z * 1.02f));
                        b.AddBox(new Vector3(0f, -0.06f, R.z * 1.12f), new Vector3(R.x * 1.3f, 0.016f, 0.14f), Quaternion.Euler(-12f, 0f, 0f), Color.white, 0.006f);
                        return b.ToMesh("JanitorCap");
                    }), m.Secondary, Vector3.zero);
                    ProcMesh.Part("Mop", rig.HatHand, ProcMesh.Cached("vil_mop", () =>
                    {
                        var b = new ProcMesh.Builder();
                        b.AddCylinder(new Vector3(0f, 0.15f, 0f), 0.012f, 1.0f, Quaternion.identity, Color.white, 6);
                        for (int i = 0; i < 9; i++)
                        {
                            float a = i / 9f * Mathf.PI * 2f;
                            b.AddCylinder(new Vector3(Mathf.Sin(a) * 0.035f, -0.4f, Mathf.Cos(a) * 0.035f), 0.014f, 0.16f, Quaternion.Euler(Mathf.Cos(a) * 14f, 0f, -Mathf.Sin(a) * 14f), Color.white, 5);
                        }
                        return b.ToMesh("Mop");
                    }), m.Coat, Vector3.zero);
                    break;
                case VillainHat.EmployeeOfTheMonth:
                    ProcMesh.Part("Rosette", rig.HatBody, ProcMesh.Cached("vil_rosette", () =>
                    {
                        var b = new ProcMesh.Builder();
                        var ctr = new Vector3(-0.1f, rig.D.TorsoH * 0.66f, rig.D.TorsoD * 0.92f);
                        for (int i = 0; i < 10; i++)
                        {
                            float a = i / 10f * Mathf.PI * 2f;
                            b.AddSphere(ctr + new Vector3(Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f, 0f), 0.028f, Quaternion.identity, Color.white, 6, 4);
                        }
                        b.AddCylinder(ctr + new Vector3(0f, 0f, 0.01f), 0.05f, 0.02f, Quaternion.Euler(90f, 0f, 0f), Color.white, 14, true);
                        b.AddBox(ctr + new Vector3(-0.025f, -0.1f, -0.005f), new Vector3(0.035f, 0.12f, 0.006f), Quaternion.Euler(0f, 0f, -10f), Color.white);
                        b.AddBox(ctr + new Vector3(0.025f, -0.1f, -0.005f), new Vector3(0.035f, 0.12f, 0.006f), Quaternion.Euler(0f, 0f, 10f), Color.white);
                        return b.ToMesh("Rosette");
                    }), m.Gold, Vector3.zero);
                    break;
            }
            if (rig.CrownPivot != null)
            {
                rig.CrownPivot.gameObject.SetActive(crown);
                rig.CrownRestPos = new Vector3(rig.HeadC.x, rig.HeadC.y + R.y * 0.66f + crownLift, -0.005f);
                rig.CrownPivot.localPosition = rig.CrownRestPos;
            }
            Collect(rig);
            if (holo) SetHologram(rig, true);
            if (gold) SetGoldLook(rig, true);
        }

        static void Sash(VillainRig rig, Material mat)
        {
            var d = rig.D;
            ProcMesh.Part("Sash", rig.HatBody, ProcMesh.Cached("vil_sash", () =>
            {
                var b = new ProcMesh.Builder();
                for (int i = 0; i < 8; i++)
                {
                    float u0 = i / 8f, u1 = (i + 1) / 8f;
                    Vector3 P(float u)
                    {
                        float y = Mathf.Lerp(d.TorsoH * 0.92f, d.TorsoH * 0.08f, u), x = Mathf.Lerp(-0.14f, 0.2f, u);
                        RingAt(TorsoRings(d), y, out float rx, out float rz, out float zo);
                        float k = Mathf.Clamp(x / rx, -0.95f, 0.95f);
                        return new Vector3(x, y, rz * Mathf.Sqrt(1f - k * k) + zo + 0.006f);
                    }
                    var a = P(u0); var c = P(u1);
                    b.AddBox((a + c) * 0.5f, new Vector3(0.075f, (c - a).magnitude + 0.01f, 0.008f), Quaternion.LookRotation(Vector3.forward, (c - a).normalized), Color.white);
                }
                return b.ToMesh("Sash");
            }), mat, Vector3.zero);
        }

        static void Tie(VillainRig rig, Material mat)
        {
            var d = rig.D;
            ProcMesh.Part("Tie", rig.HatBody, ProcMesh.Cached("vil_tie", () =>
            {
                var b = new ProcMesh.Builder();
                RingAt(TorsoRings(d), d.TorsoH * 0.9f, out _, out float rz0, out float z0);
                RingAt(TorsoRings(d), d.TorsoH * 0.62f, out _, out float rz1, out float z1);
                b.AddBox(new Vector3(0f, d.TorsoH * 0.93f, rz0 + z0 + 0.01f), new Vector3(0.045f, 0.035f, 0.02f), Quaternion.identity, Color.white, 0.005f);
                b.AddBox(new Vector3(0f, d.TorsoH * 0.74f, (rz0 + z0 + rz1 + z1) * 0.5f + 0.012f), new Vector3(0.05f, 0.17f, 0.008f), Quaternion.Euler(-14f, 0f, 0f), Color.white);
                return b.ToMesh("Tie");
            }), mat, Vector3.zero);
        }

        static Vector4[] _torsoRings;
        static Vector4[] TorsoRings(RigDims d)
        {
            if (_torsoRings != null) return _torsoRings;
            float h = d.TorsoH, ww = d.WaistW, cw = d.ChestW, t = d.TorsoD;
            return _torsoRings = new[]
            {
                new Vector4(-0.06f, 0f, 0f, 0f), new Vector4(-0.06f, ww * 0.9f, t * 0.86f, 0f), new Vector4(0.14f * h, ww * 1.06f, t * 1.04f, 0.02f),
                new Vector4(0.36f * h, ww * 1.02f, t * 1.02f, 0.026f), new Vector4(0.6f * h, cw * 1.3f, t * 0.86f, 0.012f),
                new Vector4(0.84f * h, cw * 1.08f, t * 0.68f, 0f), new Vector4(h, cw * 0.62f, t * 0.44f, 0f), new Vector4(h, 0f, 0f, 0f)
            };
        }

        static void Clear(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--) Kit.SafeDestroy(t.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Lathe ring radii at height y (piecewise linear over the non-degenerate rings).</summary>
        internal static void RingAt(Vector4[] rings, float y, out float rx, out float rz, out float zOff)
        {
            rx = rz = zOff = 0f;
            for (int i = 1; i < rings.Length - 2; i++)
            {
                var a = rings[i]; var b = rings[i + 1];
                if (y >= a.x && y <= b.x)
                {
                    float u = Mathf.InverseLerp(a.x, b.x, y);
                    rx = Mathf.Lerp(a.y, b.y, u); rz = Mathf.Lerp(a.z, b.z, u); zOff = Mathf.Lerp(a.w, b.w, u);
                    return;
                }
            }
            var l = rings[rings.Length - 2]; rx = l.y; rz = l.z; zOff = l.w;
        }

        /// <summary>Raised stroke letters "ME" in the XY plane of frame (unit = letter half-height), facing +Z of frame.</summary>
        internal static void Letters(ProcMesh.Builder b, Matrix4x4 frame, float thick)
        {
            // M (x -1.75..-0.25), E (x 0.35..1.55); height -1..1
            Seg(b, frame, -1.7f, -1f, -1.7f, 1f, thick); Seg(b, frame, -1.7f, 1f, -1f, -0.1f, thick);
            Seg(b, frame, -1f, -0.1f, -0.3f, 1f, thick); Seg(b, frame, -0.3f, 1f, -0.3f, -1f, thick);
            Seg(b, frame, 0.4f, -1f, 0.4f, 1f, thick); Seg(b, frame, 0.4f, 1f, 1.5f, 1f, thick);
            Seg(b, frame, 0.4f, 0f, 1.25f, 0f, thick); Seg(b, frame, 0.4f, -1f, 1.5f, -1f, thick);
        }

        static void Seg(ProcMesh.Builder b, Matrix4x4 frame, float x0, float y0, float x1, float y1, float thick)
        {
            var p0 = new Vector2(x0, y0); var p1 = new Vector2(x1, y1); var dd = p1 - p0;
            float len = dd.magnitude, ang = Mathf.Atan2(dd.y, dd.x) * Mathf.Rad2Deg;
            float s = frame.lossyScale.x;
            var ctr = frame.MultiplyPoint3x4(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f));
            b.AddBox(ctr, new Vector3((len + thick * 2f) * s, thick * 2f * s, thick * 1.2f * s), frame.rotation * Quaternion.Euler(0f, 0f, ang), Color.white);
        }
    }
}
