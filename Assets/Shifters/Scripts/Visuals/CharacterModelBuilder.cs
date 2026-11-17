// =====================================================================================
//  SHIFTERS - CharacterModelBuilder.cs   (VISUALS module)
//  Builds the procedural low-poly character from a profile: archetype silhouette
//  (Brute barrel / Sprinter noodle / Brainiac big-head) x random proportions, outfit colors,
//  hair, accessory and the expressive face. Feet at the origin, facing +Z, head top = Body.Height.
//  Meshes are cached (ProcMesh.Cached) by their dimensions; materials are per-character instances.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class CharacterModelBuilder
    {
        static readonly List<Renderer> _tmpRenderers = new List<Renderer>(48);
        static readonly List<MeshFilter> _tmpFilters = new List<MeshFilter>(48);

        /// <summary>Compute the body measurements for a profile (pure function).</summary>
        public static RigDims Measure(CharacterProfile p)
        {
            var b = p.Body;
            float H = Mathf.Max(0.8f, b.Height);
            float W = Mathf.Clamp(b.WidthScale, 0.7f, 1.4f), Hs = Mathf.Clamp(b.HeadScale, 0.7f, 1.4f);
            float L = Mathf.Clamp(b.LimbScale, 0.75f, 1.3f), B = Mathf.Clamp(b.BellyScale, 0.75f, 1.5f), E = Mathf.Clamp(b.EyeScale, 0.7f, 1.4f);
            var d = new RigDims { Height = H };
            float neckModel, torsoTop;
            switch (p.Archetype)
            {
                case Archetype.Brute:
                    d.HeadHalf = new Vector3(0.205f, 0.19f, 0.19f) * Hs;
                    d.HipY = 0.56f * L * (H / 1.85f);
                    d.AnkleY = 0.1f; d.LegR = 0.135f * (0.85f + 0.15f * W);
                    d.Foot = new Vector3(0.25f, 0.16f, 0.38f);
                    d.HeadCenterY = d.HeadHalf.y * 0.75f;
                    neckModel = H - d.HeadCenterY - d.HeadHalf.y;
                    torsoTop = neckModel - 0.01f;          // shoulders hug the jaw, but the whole face stays visible
                    d.HeadZ = 0.065f + 0.05f * (B - 1f);  // hunched: head pokes out in front of the barrel chest
                    d.WaistY = d.HipY + 0.05f;
                    d.ChestW = 0.5f * W; d.WaistW = 0.4f * W; d.TorsoD = 0.31f * B;
                    d.UpperLen = 0.46f * L; d.ForeLen = 0.42f * L; d.ArmR = 0.115f * (0.85f + 0.15f * W);
                    d.ForeR0 = 0.12f; d.ForeR1 = 0.145f; d.HandR = 0.165f * (0.9f + 0.1f * W); d.ArmSplay = 9f;
                    d.EyeR = 0.068f * E;
                    break;
                case Archetype.Sprinter:
                    d.HeadHalf = new Vector3(0.165f, 0.175f, 0.165f) * Hs;
                    d.HipY = 1.0f * L * (H / 1.95f);
                    d.AnkleY = 0.09f; d.LegR = 0.062f;
                    d.Foot = new Vector3(0.17f, 0.15f, 0.41f);
                    d.HeadCenterY = 0.06f + d.HeadHalf.y;
                    neckModel = H - d.HeadCenterY - d.HeadHalf.y;
                    torsoTop = neckModel + 0.03f;
                    d.WaistY = d.HipY + 0.05f;
                    d.ChestW = 0.2f * W; d.WaistW = 0.15f * W; d.TorsoD = 0.13f * B;
                    d.UpperLen = 0.34f * L; d.ForeLen = 0.31f * L; d.ArmR = 0.048f;
                    d.ForeR0 = 0.045f; d.ForeR1 = 0.042f; d.HandR = 0.068f; d.ArmSplay = 4f;
                    d.EyeR = 0.052f * E;
                    break;
                default: // Brainiac
                    d.HeadHalf = new Vector3(0.33f, 0.315f, 0.31f) * Hs;
                    d.HipY = 0.34f * L * (H / 1.5f);
                    d.AnkleY = 0.07f; d.LegR = 0.072f;
                    d.Foot = new Vector3(0.14f, 0.11f, 0.24f);
                    d.HeadCenterY = d.HeadHalf.y * 0.9f;
                    neckModel = H - d.HeadCenterY - d.HeadHalf.y;
                    torsoTop = neckModel + 0.04f;
                    d.WaistY = d.HipY + 0.04f;
                    d.ChestW = 0.23f * W; d.WaistW = 0.25f * W; d.TorsoD = 0.2f * B;
                    d.UpperLen = 0.21f * L; d.ForeLen = 0.2f * L; d.ArmR = 0.055f;
                    d.ForeR0 = 0.052f; d.ForeR1 = 0.05f; d.HandR = 0.068f; d.ArmSplay = 10f;
                    d.EyeR = 0.085f * E;
                    break;
            }
            // keep a sane torso even for extreme rolls (head top stays at H)
            float minTorso = p.Archetype == Archetype.Sprinter ? 0.36f : 0.32f;
            if (torsoTop - d.WaistY < minTorso)
            {
                float excess = minTorso - (torsoTop - d.WaistY);
                d.HipY -= excess; d.WaistY -= excess;
            }
            d.TorsoH = torsoTop - d.WaistY;
            d.NeckY = neckModel - d.WaistY;
            d.ThighLen = d.ShinLen = (d.HipY - d.AnkleY) * 0.5f;
            d.HipX = d.WaistW * (p.Archetype == Archetype.Brute ? 0.5f : 0.55f);
            d.ShoulderY = d.TorsoH - (p.Archetype == Archetype.Brute ? 0.2f : p.Archetype == Archetype.Sprinter ? 0.06f : 0.1f);
            d.ShoulderX = p.Archetype == Archetype.Brute ? d.ChestW * 0.93f : d.ChestW + d.ArmR * 0.6f;
            return d;
        }

        /// <summary>Build the full model under parent (child "Model"). Returns the rig.</summary>
        public static CharacterRig Build(CharacterProfile p, Transform parent, int seed = 0)
        {
            if (p == null) p = new CharacterProfile();
            var d = Measure(p);
            var m = new CharacterMaterials(p);
            var rig = new CharacterRig { Profile = p, D = d, Mats = m };
            string a = p.Archetype.ToString();
            string F(float v) => VisualMeshes.F(v);

            var root = new GameObject("Model").transform;
            root.SetParent(parent, false);
            rig.Root = root;

            // ---------------- hips + legs
            rig.Hips = Pivot("Hips", root, new Vector3(0f, d.HipY, 0f));
            rig.HipsRest = rig.Hips.localPosition;
            float pelvisH = p.Archetype == Archetype.Brute ? 0.2f : 0.14f;
            var pelvis = VisualMeshes.Lathe(a + "_pelvis_" + F(d.WaistW) + "_" + F(d.TorsoD) + "_" + F(pelvisH), new[]
            {
                new Vector4(-pelvisH * 0.6f, 0f, 0f, 0f), new Vector4(-pelvisH * 0.6f, d.WaistW * 0.8f, d.TorsoD * 0.72f, 0f),
                new Vector4(-pelvisH * 0.2f, d.WaistW * 0.98f, d.TorsoD * 0.86f, 0f), new Vector4(pelvisH * 0.45f, d.WaistW * 0.96f, d.TorsoD * 0.84f, 0f),
                new Vector4(pelvisH * 0.45f, 0f, 0f, 0f)
            }, 2.4f, 14);
            ProcMesh.Part("Pelvis", rig.Hips, pelvis, m.Secondary, Vector3.zero);

            rig.LegL = Leg(rig, true, d, m, out rig.KneeL, out rig.FootL);
            rig.LegR = Leg(rig, false, d, m, out rig.KneeR, out rig.FootR);

            // ---------------- spine + torso
            rig.Spine = Pivot("Spine", rig.Hips, new Vector3(0f, d.WaistY - d.HipY, 0f));
            var torso = TorsoMesh(p.Archetype, d, p.Body.BellyScale);
            rig.TorsoMesh = ProcMesh.Part("Torso", rig.Spine, torso, m.Primary, Vector3.zero).transform;

            // ---------------- arms
            bool bareUpper = p.Archetype == Archetype.Brute;
            bool bareFore = p.Archetype != Archetype.Brainiac;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0; float sx = left ? -1f : 1f;
                var sh = Pivot(left ? "ShoulderL" : "ShoulderR", rig.Spine, new Vector3(sx * d.ShoulderX, d.ShoulderY, 0f));
                ProcMesh.Part("UpperArm", sh, VisualMeshes.Limb(d.ArmR, d.UpperLen), bareUpper ? m.Skin : m.Primary, Vector3.zero);
                var el = Pivot(left ? "ElbowL" : "ElbowR", sh, new Vector3(0f, -d.UpperLen, 0f));
                ProcMesh.Part("Forearm", el, VisualMeshes.TaperLimb(d.ForeR0, d.ForeR1, d.ForeLen), bareFore ? m.Skin : m.Primary, Vector3.zero);
                var hand = Pivot(left ? "HandL" : "HandR", el, new Vector3(0f, -d.ForeLen, 0f));
                var handMesh = p.Archetype == Archetype.Brute
                    ? VisualMeshes.Lathe("fist_" + F(d.HandR), new[]
                      {
                          new Vector4(-d.HandR, 0f, 0f, 0f), new Vector4(-d.HandR, d.HandR * 0.7f, d.HandR * 0.75f, 0f),
                          new Vector4(-d.HandR * 0.5f, d.HandR, d.HandR * 1.02f, 0.01f), new Vector4(d.HandR * 0.55f, d.HandR * 0.95f, d.HandR, 0.01f),
                          new Vector4(d.HandR * 0.9f, d.HandR * 0.6f, d.HandR * 0.65f, 0f), new Vector4(d.HandR * 0.9f, 0f, 0f, 0f)
                      }, 3f, 14)
                    : VisualMeshes.Ellipsoid(new Vector3(d.HandR * 0.9f, d.HandR, d.HandR * 0.95f), 12, 8);
                ProcMesh.Part("Hand", hand, handMesh, m.Skin, new Vector3(0f, -d.HandR * 0.7f, 0f));
                if (left) { rig.ShoulderL = sh; rig.ElbowL = el; rig.HandL = hand; rig.ShoulderLRest = sh.localPosition; }
                else
                {
                    rig.ShoulderR = sh; rig.ElbowR = el; rig.HandR = hand; rig.ShoulderRRest = sh.localPosition;
                    rig.WeaponSocket = Pivot("WeaponSocket", hand, new Vector3(0f, -d.HandR * 0.75f, d.HandR * 0.15f));
                    rig.WeaponSocket.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            // ---------------- head
            rig.Head = Pivot("Head", rig.Spine, new Vector3(0f, d.NeckY, d.HeadZ));
            var c = new Vector3(0f, d.HeadCenterY, 0f);
            var R = d.HeadHalf;
            System.Func<float, float, float> surf;
            Mesh headMesh;
            if (p.Archetype == Archetype.Brute)
            {
                headMesh = VisualMeshes.Lathe("brutehead_" + VisualMeshes.V(R), new[]
                {
                    new Vector4(-R.y, 0f, 0f, 0f), new Vector4(-R.y, R.x * 0.84f, R.z * 0.8f, 0f), new Vector4(-R.y * 0.6f, R.x, R.z * 0.97f, 0.01f),
                    new Vector4(R.y * 0.15f, R.x, R.z, 0f), new Vector4(R.y * 0.62f, R.x * 0.95f, R.z * 0.94f, 0f), new Vector4(R.y * 0.9f, R.x * 0.75f, R.z * 0.74f, 0f),
                    new Vector4(R.y, R.x * 0.42f, R.z * 0.42f, 0f), new Vector4(R.y, 0f, 0f, 0f)
                }, 3.2f, 16);
                surf = (x, y) => R.z * Mathf.Pow(Mathf.Max(0.02f, 1f - Mathf.Pow(Mathf.Abs(x / R.x), 3.2f) - Mathf.Pow(Mathf.Abs(y / R.y), 4f) * 0.5f), 1f / 3.2f);
            }
            else
            {
                headMesh = VisualMeshes.Ellipsoid(R, p.Archetype == Archetype.Brainiac ? 20 : 16, p.Archetype == Archetype.Brainiac ? 14 : 12);
                surf = (x, y) => R.z * Mathf.Sqrt(Mathf.Max(0.02f, 1f - (x / R.x) * (x / R.x) - (y / R.y) * (y / R.y)));
            }
            rig.HeadMesh = ProcMesh.Part("HeadMesh", rig.Head, headMesh, m.Skin, c).transform;
            if (p.Archetype == Archetype.Sprinter)
                ProcMesh.Part("Neck", rig.Head, VisualMeshes.Limb(0.045f, 0.08f), m.Skin, new Vector3(0f, 0.1f, -0.01f));

            rig.Face = FaceController.Build(rig.Head, c, R, p.Archetype, d.EyeR, m, Mathf.Clamp(p.Body.MouthStyle, 0, 2), surf, seed ^ (p.Name ?? "").GetHashCode());
            // nose + ears
            float ny = R.y * 0.1f - d.EyeR * 0.95f;
            var noseR = p.Archetype == Archetype.Brute ? new Vector3(0.05f, 0.045f, 0.045f) : p.Archetype == Archetype.Sprinter ? new Vector3(0.024f, 0.028f, 0.045f) : new Vector3(0.038f, 0.034f, 0.036f);
            ProcMesh.Part("Nose", rig.Head, VisualMeshes.Ellipsoid(noseR, 10, 7), m.Skin, c + new Vector3(0f, ny, surf(0f, ny) + noseR.z * 0.15f), null, null, false);
            var earR = p.Archetype == Archetype.Brainiac ? new Vector3(0.05f, 0.09f, 0.06f) : p.Archetype == Archetype.Brute ? new Vector3(0.035f, 0.055f, 0.045f) : new Vector3(0.03f, 0.05f, 0.035f);
            var earMesh = VisualMeshes.Ellipsoid(earR, 10, 7);
            ProcMesh.Part("EarL", rig.Head, earMesh, m.Skin, c + new Vector3(-R.x * 0.98f, -R.y * 0.04f, -R.z * 0.05f), null, null, false);
            ProcMesh.Part("EarR", rig.Head, earMesh, m.Skin, c + new Vector3(R.x * 0.98f, -R.y * 0.04f, -R.z * 0.05f), null, null, false);

            d.HairTop = CharacterAccessories.AddHair(rig, p, c, R);
            rig.D = d;
            CharacterAccessories.AddAccessory(rig, p, c, R, surf);
            CharacterAccessories.AddArchetypeExtras(rig, p, c, R, surf);

            // ---------------- collect
            root.GetComponentsInChildren(true, _tmpRenderers);
            rig.Renderers = _tmpRenderers.ToArray();
            root.GetComponentsInChildren(true, _tmpFilters);
            int tris = 0;
            for (int i = 0; i < _tmpFilters.Count; i++) if (_tmpFilters[i].sharedMesh != null) tris += (int)_tmpFilters[i].sharedMesh.GetIndexCount(0) / 3;
            rig.Triangles = tris;
            _tmpRenderers.Clear(); _tmpFilters.Clear();
            rig.Face.Snap();
            return rig;
        }

        internal static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        internal static Transform Leg(CharacterRig rig, bool left, RigDims d, CharacterMaterials m, out Transform knee, out Transform foot)
        {
            float sx = left ? -1f : 1f;
            var leg = Pivot(left ? "LegL" : "LegR", rig.Hips, new Vector3(sx * d.HipX, 0f, 0f));
            ProcMesh.Part("Thigh", leg, VisualMeshes.Limb(d.LegR, d.ThighLen), m.Secondary, Vector3.zero);
            knee = Pivot(left ? "KneeL" : "KneeR", leg, new Vector3(0f, -d.ThighLen, 0f));
            ProcMesh.Part("Shin", knee, VisualMeshes.Limb(d.LegR * 0.92f, d.ShinLen), m.Secondary, Vector3.zero);
            foot = Pivot(left ? "FootL" : "FootR", knee, new Vector3(0f, -d.ShinLen, 0f));
            var shoe = VisualMeshes.Block(d.Foot, Mathf.Min(d.Foot.x, d.Foot.y) * 0.38f);
            ProcMesh.Part("Shoe", foot, shoe, m.Shoe, new Vector3(0f, -d.AnkleY + d.Foot.y * 0.5f, d.Foot.z * 0.24f));
            return leg;
        }

        static Mesh TorsoMesh(Archetype a, RigDims d, float belly)
        {
            float h = d.TorsoH, cw = d.ChestW, ww = d.WaistW, t = d.TorsoD, bz = 0.04f * Mathf.Max(0f, belly - 0.8f) * 2f;
            string key = a + "_torso_" + VisualMeshes.F(h) + "_" + VisualMeshes.F(cw) + "_" + VisualMeshes.F(ww) + "_" + VisualMeshes.F(t) + "_" + VisualMeshes.F(bz);
            switch (a)
            {
                case Archetype.Brute:
                    return VisualMeshes.Lathe(key, new[]
                    {
                        new Vector4(-0.06f, 0f, 0f, 0f), new Vector4(-0.06f, ww * 0.95f, t * 0.85f, 0f),
                        new Vector4(0.12f * h, ww * 1.1f, t * 1.03f, bz), new Vector4(0.4f * h, cw * 0.95f, t * 1.08f, bz * 0.8f),
                        new Vector4(0.72f * h, cw, t, 0f), new Vector4(0.9f * h, cw * 0.9f, t * 0.86f, 0f),
                        new Vector4(h, cw * 0.55f, t * 0.56f, 0f), new Vector4(h, 0f, 0f, 0f)
                    }, 2.8f, 18);
                case Archetype.Sprinter:
                    return VisualMeshes.Lathe(key, new[]
                    {
                        new Vector4(-0.04f, 0f, 0f, 0f), new Vector4(-0.04f, ww * 0.95f, t * 0.85f, 0f),
                        new Vector4(0.3f * h, ww * 1.02f, t * 0.95f, bz * 0.5f), new Vector4(0.7f * h, cw, t, 0f),
                        new Vector4(0.92f * h, cw * 0.95f, t * 0.86f, 0f), new Vector4(h, cw * 0.55f, t * 0.6f, 0f), new Vector4(h, 0f, 0f, 0f)
                    }, 2.3f, 14);
                default:
                    return VisualMeshes.Lathe(key, new[]
                    {
                        new Vector4(-0.06f, 0f, 0f, 0f), new Vector4(-0.06f, ww * 0.92f, t * 0.9f, 0f),
                        new Vector4(0.25f * h, ww * 1.05f, t * 1.05f, bz), new Vector4(0.6f * h, (ww + cw) * 0.5f, t * 0.95f, bz * 0.5f),
                        new Vector4(0.88f * h, cw * 0.85f, t * 0.78f, 0f), new Vector4(h, cw * 0.5f, t * 0.5f, 0f), new Vector4(h, 0f, 0f, 0f)
                    }, 2f, 16);
            }
        }
    }
}
