// =====================================================================================
//  SHIFTERS - WeaponModelBuilder.cs   [CONTRACT API - implementation owned by the VISUALS agent]
//  Cartoon low-poly weapon models built from ProcMesh primitives.
//  Convention: grip at the origin, barrel along +Z, top = +Y, child "Muzzle" at the barrel tip
//  (+Z = shot direction). Parts are merged per material (Tint body / dark details / accents).
//  Sizes ~0.4-0.7 m. Meshes are cached per type; materials are shared (MaterialLibrary.Colored).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class WeaponModelBuilder
    {
        static readonly Color Dark = new Color(0.16f, 0.17f, 0.2f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.12f);
        static readonly Color Wood = new Color(0.62f, 0.4f, 0.22f);
        static readonly Quaternion AlongZ = Quaternion.Euler(90f, 0f, 0f); // cylinder/cone Y axis -> +Z

        /// <summary>Builds the model as a child of parent. Barrel points +Z. Returns the root;
        /// the muzzle is a child named "Muzzle".</summary>
        public static GameObject Build(WeaponData data, Transform parent)
        {
            if (data == null) data = new WeaponData();
            var root = new GameObject("Weapon_" + data.Type);
            root.transform.SetParent(parent, false);
            var tint = data.Tint; tint.a = 1f;
            Vector3 muzzle;
            string id = data.Type == WeaponType.Experimental ? data.Variant.ToString() : data.Type.ToString();
            switch (data.Type)
            {
                case WeaponType.Shotgun: muzzle = Shotgun(root.transform, tint); break;
                case WeaponType.LaserGun: muzzle = Laser(root.transform, tint); break;
                case WeaponType.Slingshot: muzzle = Slingshot(root.transform, tint); break;
                case WeaponType.Experimental:
                    // campaign weapons/tools (W0): models by visuals_bosses (Visuals/Bosses/CampaignWeaponModels.cs)
                    if (CampaignWeaponModels.TryKind(data.Variant, out var campaignKind)) muzzle = CampaignWeaponModels.Build(campaignKind, root.transform, tint);
                    else muzzle = data.Variant == ExperimentalVariant.GravityPlunger ? Plunger(root.transform, tint) : Chicken(root.transform, tint);
                    break;
                default: muzzle = Pistol(root.transform, tint); break;
            }
            var m = new GameObject("Muzzle").transform;
            m.SetParent(root.transform, false);
            m.localPosition = muzzle;
            root.name = "Weapon_" + id;
            return root;
        }

        static float _scale = 1f;   // per-model uniform scale (the slingshot is drawn oversized so it reads in gameplay)

        static void Part(Transform root, string name, string key, System.Action<ProcMesh.Builder> build, Material mat)
        {
            var mesh = ProcMesh.Cached("wpn_" + key, () => { var b = new ProcMesh.Builder(); build(b); return b.ToMesh(key); });
            ProcMesh.Part(name, root, mesh, mat, Vector3.zero, null, Vector3.one * _scale);
        }

        static Material Mat(Color c, float smooth = 0.45f, float emission = 0f) => MaterialLibrary.Colored(c, smooth, emission);

        // ------------------------------------------------------------------ Pew-Pew blaster
        static Vector3 Pistol(Transform r, Color tint)
        {
            Part(r, "Body", "pistol_body", b =>
            {
                b.AddBox(new Vector3(0f, 0.075f, 0.05f), new Vector3(0.1f, 0.12f, 0.24f), Quaternion.identity, Color.white, 0.03f);
                b.AddSphere(new Vector3(0f, 0.14f, 0.0f), 0.045f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
                b.AddCylinder(new Vector3(0f, 0.075f, 0.24f), 0.038f, 0.2f, AlongZ, Color.white, 12, true);
            }, Mat(tint, 0.5f));
            Part(r, "Details", "pistol_dark", b =>
            {
                b.AddBox(new Vector3(0f, -0.04f, -0.035f), new Vector3(0.07f, 0.15f, 0.08f), Quaternion.Euler(14f, 0f, 0f), Color.white, 0.02f);
                b.AddTorus(new Vector3(0f, 0.075f, 0.2f), 0.048f, 0.014f, AlongZ, Color.white, 14, 6);
                b.AddTorus(new Vector3(0f, 0.075f, 0.27f), 0.048f, 0.014f, AlongZ, Color.white, 14, 6);
                b.AddBox(new Vector3(0f, 0.005f, 0.04f), new Vector3(0.025f, 0.05f, 0.07f), Quaternion.identity, Color.white, 0.008f);
            }, Mat(Dark, 0.4f));
            Part(r, "Tip", "pistol_tip", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.075f, 0.355f), 0.05f, 0.04f, AlongZ, Color.white, 12, true);
                b.AddCone(new Vector3(0f, 0.075f, 0.375f), 0.04f, 0.03f, AlongZ, Color.white, 12);
            }, Mat(Orange, 0.5f, 0.6f));
            return new Vector3(0f, 0.075f, 0.41f);
        }

        // ------------------------------------------------------------------ double-barrel shotgun
        static Vector3 Shotgun(Transform r, Color tint)
        {
            Part(r, "Barrels", "shotgun_barrels", b =>
            {
                b.AddCylinder(new Vector3(-0.032f, 0.07f, 0.32f), 0.031f, 0.44f, AlongZ, Color.white, 12, true);
                b.AddCylinder(new Vector3(0.032f, 0.07f, 0.32f), 0.031f, 0.44f, AlongZ, Color.white, 12, true);
                b.AddTorus(new Vector3(-0.032f, 0.07f, 0.535f), 0.031f, 0.008f, AlongZ, Color.white, 12, 5);
                b.AddTorus(new Vector3(0.032f, 0.07f, 0.535f), 0.031f, 0.008f, AlongZ, Color.white, 12, 5);
            }, Mat(Dark, 0.6f));
            Part(r, "Receiver", "shotgun_recv", b =>
            {
                b.AddBox(new Vector3(0f, 0.06f, 0.03f), new Vector3(0.11f, 0.1f, 0.17f), Quaternion.identity, Color.white, 0.025f);
                b.AddBox(new Vector3(0f, 0.115f, 0.05f), new Vector3(0.03f, 0.02f, 0.12f), Quaternion.identity, Color.white, 0.006f);
            }, Mat(tint, 0.45f));
            Part(r, "Wood", "shotgun_wood", b =>
            {
                b.AddBox(new Vector3(0f, 0.01f, -0.17f), new Vector3(0.075f, 0.11f, 0.26f), Quaternion.Euler(-10f, 0f, 0f), Color.white, 0.025f);
                b.AddBox(new Vector3(0f, -0.04f, -0.02f), new Vector3(0.055f, 0.11f, 0.06f), Quaternion.Euler(12f, 0f, 0f), Color.white, 0.015f);
                b.AddBox(new Vector3(0f, 0.025f, 0.3f), new Vector3(0.085f, 0.055f, 0.15f), Quaternion.identity, Color.white, 0.018f);
            }, Mat(Wood, 0.3f));
            return new Vector3(0f, 0.07f, 0.55f);
        }

        // ------------------------------------------------------------------ sci-fi laser gun
        static Vector3 Laser(Transform r, Color tint)
        {
            var glow = new Color(0.3f, 1f, 0.92f);
            Part(r, "Body", "laser_body", b =>
            {
                b.AddBox(new Vector3(0f, 0.08f, 0.06f), new Vector3(0.11f, 0.13f, 0.3f), Quaternion.identity, Color.white, 0.035f);
                b.AddBox(new Vector3(0f, 0.16f, 0.02f), new Vector3(0.025f, 0.06f, 0.16f), Quaternion.Euler(-8f, 0f, 0f), Color.white, 0.01f);
                b.AddBox(new Vector3(0.065f, 0.09f, 0.0f), new Vector3(0.02f, 0.05f, 0.12f), Quaternion.identity, Color.white, 0.008f);
                b.AddBox(new Vector3(-0.065f, 0.09f, 0.0f), new Vector3(0.02f, 0.05f, 0.12f), Quaternion.identity, Color.white, 0.008f);
            }, Mat(tint, 0.6f));
            Part(r, "Details", "laser_dark", b =>
            {
                b.AddBox(new Vector3(0f, -0.04f, -0.03f), new Vector3(0.065f, 0.14f, 0.075f), Quaternion.Euler(12f, 0f, 0f), Color.white, 0.02f);
                b.AddCylinder(new Vector3(0f, 0.08f, 0.32f), 0.03f, 0.24f, AlongZ, Color.white, 12, true);
                b.AddCylinder(new Vector3(0f, 0.08f, 0.45f), 0.045f, 0.03f, AlongZ, Color.white, 12, true);
            }, Mat(Dark, 0.5f));
            Part(r, "Coil", "laser_coil", b =>
            {
                for (int i = 0; i < 4; i++) b.AddTorus(new Vector3(0f, 0.08f, 0.24f + i * 0.045f), 0.042f, 0.011f, AlongZ, Color.white, 14, 6);
                b.AddSphere(new Vector3(0f, 0.08f, 0.47f), 0.028f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
                b.AddBox(new Vector3(0f, 0.08f, 0.212f), new Vector3(0.115f, 0.02f, 0.012f), Quaternion.identity, Color.white, 0f);
            }, Mat(glow, 0.7f, 2.2f));
            return new Vector3(0f, 0.08f, 0.5f);
        }

        // ------------------------------------------------------------------ Y-fork slingshot
        static Vector3 Slingshot(Transform r, Color tint)
        {
            _scale = 1.4f;
            try { return SlingshotParts(r, tint) * _scale; }
            finally { _scale = 1f; }
        }

        static Vector3 SlingshotParts(Transform r, Color tint)
        {
            Part(r, "Fork", "sling_fork", b =>
            {
                b.AddCylinder(new Vector3(0f, -0.02f, 0f), 0.025f, 0.16f, Quaternion.identity, Color.white, 10, true);
                b.AddSphere(new Vector3(0f, 0.065f, 0f), 0.032f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
                b.AddCylinder(new Vector3(-0.055f, 0.14f, 0.02f), 0.019f, 0.17f, Quaternion.Euler(10f, 0f, 33f), Color.white, 8, true);
                b.AddCylinder(new Vector3(0.055f, 0.14f, 0.02f), 0.019f, 0.17f, Quaternion.Euler(10f, 0f, -33f), Color.white, 8, true);
                b.AddSphere(new Vector3(-0.1f, 0.21f, 0.035f), 0.022f, Quaternion.identity, Color.white, 8, 6, true, Vector3.one);
                b.AddSphere(new Vector3(0.1f, 0.21f, 0.035f), 0.022f, Quaternion.identity, Color.white, 8, 6, true, Vector3.one);
            }, Mat(tint, 0.35f));
            Part(r, "Grip", "sling_grip", b =>
            {
                b.AddCylinder(new Vector3(0f, -0.03f, 0f), 0.029f, 0.09f, Quaternion.identity, Color.white, 10, true);
                b.AddBox(new Vector3(0f, 0.2f, -0.09f), new Vector3(0.05f, 0.035f, 0.03f), Quaternion.identity, Color.white, 0.01f);
            }, Mat(Dark, 0.3f));
            Part(r, "Band", "sling_band", b =>
            {
                var l = new Vector3(-0.1f, 0.21f, 0.035f); var rr = new Vector3(0.1f, 0.21f, 0.035f); var pouch = new Vector3(0f, 0.2f, -0.09f);
                AddBeam(b, l, pouch, 0.012f); AddBeam(b, rr, pouch, 0.012f);
            }, Mat(new Color(0.95f, 0.22f, 0.2f), 0.3f));
            return new Vector3(0f, 0.2f, 0.08f);
        }

        // ------------------------------------------------------------------ rubber chicken launcher
        static Vector3 Chicken(Transform r, Color tint)
        {
            Part(r, "Tube", "chick_tube", b =>
            {
                b.AddCylinder(new Vector3(0f, 0.1f, 0.16f), 0.072f, 0.5f, AlongZ, Color.white, 14, true);
                b.AddBox(new Vector3(0f, 0.19f, 0.05f), new Vector3(0.03f, 0.05f, 0.12f), Quaternion.identity, Color.white, 0.01f);
            }, Mat(tint, 0.4f));
            Part(r, "Details", "chick_dark", b =>
            {
                b.AddTorus(new Vector3(0f, 0.1f, 0.41f), 0.072f, 0.016f, AlongZ, Color.white, 16, 6);
                b.AddTorus(new Vector3(0f, 0.1f, -0.085f), 0.072f, 0.016f, AlongZ, Color.white, 16, 6);
                b.AddBox(new Vector3(0f, -0.03f, -0.01f), new Vector3(0.06f, 0.14f, 0.07f), Quaternion.Euler(12f, 0f, 0f), Color.white, 0.02f);
                b.AddSphere(new Vector3(-0.022f, 0.15f, 0.47f), 0.009f, Quaternion.identity, Color.white, 6, 5, true, Vector3.one);
                b.AddSphere(new Vector3(0.022f, 0.15f, 0.47f), 0.009f, Quaternion.identity, Color.white, 6, 5, true, Vector3.one);
            }, Mat(Dark, 0.4f));
            Part(r, "Chicken", "chick_head", b =>
            {
                b.AddSphere(new Vector3(0f, 0.13f, 0.43f), 0.05f, Quaternion.identity, Color.white, 12, 8, true, new Vector3(1f, 1.15f, 1f));
                b.AddCylinder(new Vector3(0f, 0.09f, 0.41f), 0.03f, 0.06f, Quaternion.Euler(70f, 0f, 0f), Color.white, 8, true);
            }, Mat(new Color(1f, 0.88f, 0.3f), 0.4f));
            Part(r, "Beak", "chick_beak", b => b.AddCone(new Vector3(0f, 0.13f, 0.47f), 0.022f, 0.05f, AlongZ, Color.white, 8), Mat(Orange, 0.4f));
            Part(r, "Comb", "chick_comb", b =>
            {
                for (int i = 0; i < 3; i++) b.AddSphere(new Vector3(0f, 0.185f + (i == 1 ? 0.01f : 0f), 0.415f + i * 0.02f), 0.017f, Quaternion.identity, Color.white, 8, 6, true, Vector3.one);
                b.AddSphere(new Vector3(0f, 0.095f, 0.47f), 0.013f, Quaternion.identity, Color.white, 8, 6, true, new Vector3(1f, 1.6f, 1f));
            }, Mat(new Color(0.95f, 0.2f, 0.2f), 0.4f));
            return new Vector3(0f, 0.1f, 0.5f);
        }

        // ------------------------------------------------------------------ gravity plunger
        static Vector3 Plunger(Transform r, Color tint)
        {
            Part(r, "Body", "plunger_body", b =>
            {
                b.AddBox(new Vector3(0f, 0.07f, 0.0f), new Vector3(0.11f, 0.12f, 0.22f), Quaternion.identity, Color.white, 0.035f);
                b.AddCylinder(new Vector3(0f, 0.07f, 0.13f), 0.045f, 0.05f, AlongZ, Color.white, 12, true);
                b.AddSphere(new Vector3(0f, 0.14f, -0.04f), 0.035f, Quaternion.identity, Color.white, 10, 7, true, Vector3.one);
            }, Mat(tint, 0.5f));
            Part(r, "Details", "plunger_dark", b =>
            {
                b.AddBox(new Vector3(0f, -0.04f, -0.05f), new Vector3(0.065f, 0.14f, 0.075f), Quaternion.Euler(12f, 0f, 0f), Color.white, 0.02f);
                for (int i = 0; i < 4; i++) b.AddTorus(new Vector3(0f, 0.07f, 0.18f + i * 0.04f), 0.026f, 0.007f, AlongZ, Color.white, 10, 5);
            }, Mat(Dark, 0.5f));
            Part(r, "Stick", "plunger_stick", b => b.AddCylinder(new Vector3(0f, 0.07f, 0.33f), 0.018f, 0.38f, AlongZ, Color.white, 8, true), Mat(Wood, 0.3f));
            Part(r, "Cup", "plunger_cup", b =>
            {
                var dome = VisualMeshes.Dome(0.11f, 0.09f, 0.11f, 2f, 16);
                b.AddMesh(dome, Matrix4x4.TRS(new Vector3(0f, 0.07f, 0.6f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one), Color.white);
                b.AddTorus(new Vector3(0f, 0.07f, 0.6f), 0.105f, 0.014f, AlongZ, Color.white, 16, 6);
            }, Mat(new Color(0.9f, 0.12f, 0.15f), 0.55f));
            return new Vector3(0f, 0.07f, 0.62f);
        }

        static void AddBeam(ProcMesh.Builder b, Vector3 a, Vector3 c, float thick)
        {
            var d = c - a;
            b.AddBox((a + c) * 0.5f, new Vector3(thick, thick, d.magnitude), Quaternion.LookRotation(d.normalized, Vector3.up), Color.white, 0f);
        }
    }
}
