// =====================================================================================
//  SHIFTERS - WeaponFallbackModel.cs   [owned by the WEAPONS agent]
//  Minimal chunky gun model used ONLY when WeaponModelBuilder (VISUALS) produced no renderers,
//  so the weapon is never invisible. Barrel points +Z, child "Muzzle" at the barrel tip.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class WeaponFallbackModel
    {
        /// <summary>Adds fallback parts under root (keeps an existing "Muzzle" child if present).</summary>
        public static void Build(WeaponData data, Transform root)
        {
            var kind = WeaponStyle.KindOf(data);
            Color tint = data != null ? data.Tint : WeaponStyle.Tint(kind);
            var main = MaterialLibrary.Colored(tint, 0.35f);
            var dark = MaterialLibrary.Colored(UIColors.Hex(0x2A2F38), 0.3f);
            var glow = MaterialLibrary.Colored(tint, 0.2f, 2f);
            var rx = Quaternion.Euler(90f, 0f, 0f);
            float muzzleZ;
            switch (kind)
            {
                case WeaponKind.Shotgun:
                    Part(root, "Stock", Box(0.07f, 0.1f, 0.22f), MaterialLibrary.Colored(UIColors.Hex(0xA0673A), 0.2f), new Vector3(0f, -0.02f, -0.12f));
                    Part(root, "Body", Box(0.1f, 0.12f, 0.2f), main, new Vector3(0f, 0f, 0.06f));
                    Part(root, "BarrelL", Cyl(0.03f, 0.5f), dark, new Vector3(-0.032f, 0.02f, 0.36f), rx);
                    Part(root, "BarrelR", Cyl(0.03f, 0.5f), dark, new Vector3(0.032f, 0.02f, 0.36f), rx);
                    muzzleZ = 0.62f;
                    break;
                case WeaponKind.Laser:
                    Part(root, "Body", Box(0.1f, 0.14f, 0.3f), dark, new Vector3(0f, 0f, 0.05f));
                    Part(root, "Coil1", Torus(0.065f, 0.018f), glow, new Vector3(0f, 0.01f, 0.14f), rx);
                    Part(root, "Coil2", Torus(0.055f, 0.016f), glow, new Vector3(0f, 0.01f, 0.22f), rx);
                    Part(root, "Emitter", Cyl(0.035f, 0.2f), main, new Vector3(0f, 0.01f, 0.27f), rx);
                    muzzleZ = 0.38f;
                    break;
                case WeaponKind.Slingshot:
                    Part(root, "Handle", Cyl(0.025f, 0.16f), MaterialLibrary.Colored(UIColors.Hex(0xA0673A), 0.2f), new Vector3(0f, -0.08f, 0f));
                    Part(root, "ForkL", Cyl(0.02f, 0.16f), MaterialLibrary.Colored(UIColors.Hex(0xA0673A), 0.2f), new Vector3(-0.05f, 0.05f, 0.03f), Quaternion.Euler(20f, 0f, 25f));
                    Part(root, "ForkR", Cyl(0.02f, 0.16f), MaterialLibrary.Colored(UIColors.Hex(0xA0673A), 0.2f), new Vector3(0.05f, 0.05f, 0.03f), Quaternion.Euler(20f, 0f, -25f));
                    Part(root, "Band", Box(0.15f, 0.02f, 0.02f), main, new Vector3(0f, 0.12f, 0.06f));
                    muzzleZ = 0.14f;
                    break;
                case WeaponKind.Chicken:
                    Part(root, "Tube", Cyl(0.08f, 0.5f), main, new Vector3(0f, 0.02f, 0.1f), rx);
                    Part(root, "Grip", Box(0.05f, 0.12f, 0.06f), dark, new Vector3(0f, -0.09f, -0.02f));
                    Part(root, "Beak", ProcMesh.Cached("wfb_cone", () => ProcMesh.Cone(0.04f, 0.08f, 6)), MaterialLibrary.Colored(UIColors.Hex(0xFF8A1F), 0.3f), new Vector3(0f, 0.02f, 0.36f), rx);
                    muzzleZ = 0.38f;
                    break;
                case WeaponKind.Plunger:
                    Part(root, "Body", Box(0.08f, 0.1f, 0.2f), main, new Vector3(0f, 0f, 0f));
                    Part(root, "Stick", Cyl(0.018f, 0.4f), MaterialLibrary.Colored(UIColors.Hex(0xC08A55), 0.2f), new Vector3(0f, 0.01f, 0.26f), rx);
                    Part(root, "Cup", ProcMesh.Cached("wfb_cup", () => ProcMesh.Cone(0.08f, 0.09f, 10)), MaterialLibrary.Colored(UIColors.Hex(0xE8394B), 0.5f), new Vector3(0f, 0.01f, 0.52f), Quaternion.Euler(-90f, 0f, 0f));
                    muzzleZ = 0.5f;
                    break;
                default:
                    Part(root, "Body", Box(0.08f, 0.11f, 0.24f), main, new Vector3(0f, 0.01f, 0.04f));
                    Part(root, "Grip", Box(0.06f, 0.14f, 0.07f), dark, new Vector3(0f, -0.09f, -0.04f), Quaternion.Euler(-15f, 0f, 0f));
                    Part(root, "Barrel", Cyl(0.028f, 0.2f), dark, new Vector3(0f, 0.025f, 0.2f), rx);
                    Part(root, "Fin", Box(0.02f, 0.05f, 0.12f), glow, new Vector3(0f, 0.08f, 0.04f));
                    muzzleZ = 0.31f;
                    break;
            }
            var muzzle = root.Find("Muzzle");
            if (muzzle == null) { muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(root, false); }
            muzzle.localPosition = new Vector3(0f, 0.02f, muzzleZ);
            muzzle.localRotation = Quaternion.identity;
        }

        static Mesh Box(float x, float y, float z) => ProcMesh.CachedBox(new Vector3(x, y, z), Mathf.Min(x, y, z) * 0.2f);
        static Mesh Cyl(float r, float h) => ProcMesh.Cached(string.Format(System.Globalization.CultureInfo.InvariantCulture, "wfb_cyl_{0:0.###}_{1:0.###}", r, h), () => ProcMesh.Cylinder(r, h, 10));
        static Mesh Torus(float a, float b) => ProcMesh.Cached(string.Format(System.Globalization.CultureInfo.InvariantCulture, "wfb_torus_{0:0.###}_{1:0.###}", a, b), () => ProcMesh.Torus(a, b, 12, 6));

        static void Part(Transform root, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion? rot = null)
            => ProcMesh.Part(name, root, mesh, mat, pos, rot, null, false);
    }
}
