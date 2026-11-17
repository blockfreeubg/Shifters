// =====================================================================================
//  SHIFTERS - L8Art.cs   [L8 - procedural meshes: vault dial symbols, vault ring, prism, jar, box]
//  All meshes go through ProcMesh.Cached with keys that encode every parameter. Symbols are flat
//  extrusions in the local XY plane (readable from +Z and -Z), no glyphs (L4 SymbolMesh approach).
// =====================================================================================
using System.Globalization;
using UnityEngine;

namespace Shifters
{
    public enum L8Symbol { Drum, Crown, Mustache, Monocle }

    public static class L8Art
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static Quaternion Z(float deg) => Quaternion.Euler(0f, 0f, deg);
        static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);   // cylinder/torus axis along Z

        public static string Name(L8Symbol s) => s == L8Symbol.Drum ? "DRUM" : s == L8Symbol.Crown ? "CROWN" : s == L8Symbol.Mustache ? "MUSTACHE" : "MONOCLE";

        /// <summary>Symbol of about 'size' metres, thickness t along Z, centred on the origin.</summary>
        public static Mesh Symbol(L8Symbol s, float size, float t = 0.1f)
        {
            string key = string.Format(Inv, "l8_symbol_{0}_{1:0.##}_{2:0.###}", (int)s, size, t);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float r = size * 0.5f;
                var c = Color.white;
                switch (s)
                {
                    case L8Symbol.Crown:
                        b.AddBox(new Vector3(0f, -r * 0.55f, 0f), new Vector3(size * 0.9f, r * 0.45f, t), Quaternion.identity, c, 0.01f);
                        for (int i = -1; i <= 1; i++)
                        {
                            float h = i == 0 ? r * 1.15f : r * 0.9f;
                            var baseC = new Vector3(i * r * 0.6f, -r * 0.35f, 0f);
                            b.AddBox(baseC + new Vector3(-r * 0.12f, h * 0.45f, 0f), new Vector3(r * 0.16f, h, t), Z(-14f), c, 0f);
                            b.AddBox(baseC + new Vector3(r * 0.12f, h * 0.45f, 0f), new Vector3(r * 0.16f, h, t), Z(14f), c, 0f);
                            b.AddSphere(baseC + new Vector3(0f, h * 0.95f, 0f), r * 0.13f, Quaternion.identity, c, 10, 6);
                        }
                        break;
                    case L8Symbol.Monocle:
                        b.AddTorus(new Vector3(-r * 0.15f, r * 0.15f, 0f), r * 0.6f, r * 0.12f, Flat, c, 24, 6);
                        for (int i = 0; i < 4; i++)
                            b.AddBox(new Vector3(r * (0.42f + i * 0.13f), -r * (0.4f + i * 0.16f), 0f), new Vector3(r * 0.1f, r * 0.17f, t * 0.8f), Z(30f), c, 0f);
                        break;
                    case L8Symbol.Mustache:
                        for (int sd = -1; sd <= 1; sd += 2)
                        {
                            b.AddSphere(new Vector3(sd * r * 0.42f, 0f, 0f), r * 0.5f, Z(sd * -12f), c, 14, 8, true, new Vector3(1f, 0.42f, t / r));
                            b.AddTorus(new Vector3(sd * r * 0.86f, r * 0.12f, 0f), r * 0.16f, r * 0.07f, Flat, c, 14, 5);
                        }
                        break;
                    default: // Drum
                        b.AddBox(new Vector3(0f, -r * 0.15f, 0f), new Vector3(size * 0.8f, r * 1.0f, t), Quaternion.identity, c, 0.01f);
                        b.AddBox(new Vector3(0f, r * 0.38f, 0f), new Vector3(size * 0.86f, r * 0.12f, t * 1.2f), Quaternion.identity, c, 0f);
                        b.AddBox(new Vector3(0f, -r * 0.68f, 0f), new Vector3(size * 0.86f, r * 0.12f, t * 1.2f), Quaternion.identity, c, 0f);
                        for (int sd = -1; sd <= 1; sd += 2)
                        {
                            b.AddBox(new Vector3(sd * r * 0.28f, r * 0.72f, 0f), new Vector3(r * 0.08f, r * 0.9f, t), Z(sd * 35f), c, 0f);
                            b.AddSphere(new Vector3(sd * r * 0.02f + sd * r * 0.02f, r * 1.08f, 0f) + new Vector3(sd * -r * 0.0f, 0f, 0f), r * 0.1f, Quaternion.identity, c, 8, 5);
                        }
                        break;
                }
                return b.ToMesh(key);
            });
        }

        /// <summary>Flat gold ring (axis Z) used as the vault door frame.</summary>
        public static Mesh Ring(float major, float minor)
        {
            string key = string.Format(Inv, "l8_ring_{0:0.##}_{1:0.##}", major, minor);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                b.AddTorus(Vector3.zero, major, minor, Flat, Color.white, 40, 8);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16f;
                    b.AddSphere(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * major + new Vector3(0f, 0f, minor * 0.8f), minor * 0.35f, Quaternion.identity, new Color(0.8f, 0.8f, 0.8f), 8, 5);
                }
                return b.ToMesh(key);
            });
        }

        /// <summary>Floor inlay ring (axis Y), lying flat.</summary>
        public static Mesh Inlay(float major, float minor)
        {
            string key = string.Format(Inv, "l8_inlay_{0:0.##}_{1:0.###}", major, minor);
            return ProcMesh.Cached(key, () => new ProcMesh.Builder().AddTorus(Vector3.zero, major, minor, Quaternion.identity, Color.white, 48, 4).ToMesh(key));
        }

        /// <summary>The MASTER PRISM: a tall faceted gem on a small base (about 0.8 x 1.2 x 0.8, centre at origin).</summary>
        public static Mesh Prism()
        {
            const string key = "l8_master_prism_v1";
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(new Vector3(0f, -0.52f, 0f), 0.36f, 0.16f, Quaternion.identity, new Color(0.75f, 0.75f, 0.75f), 6);
                b.AddCylinder(new Vector3(0f, -0.08f, 0f), 0.32f, 0.72f, Quaternion.identity, Color.white, 6);
                b.AddCone(new Vector3(0f, 0.28f, 0f), 0.32f, 0.34f, Quaternion.identity, Color.white, 6);
                return b.ToMesh(key);
            });
        }

        /// <summary>Glass tip jar (about 0.7 m tall) with a coin layer.</summary>
        public static Mesh Jar()
        {
            const string key = "l8_tip_jar_v1";
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(new Vector3(0f, -0.05f, 0f), 0.34f, 0.7f, Quaternion.identity, Color.white, 16, true);
                b.AddCylinder(new Vector3(0f, 0.34f, 0f), 0.24f, 0.1f, Quaternion.identity, new Color(0.8f, 0.8f, 0.8f), 16, true);
                return b.ToMesh(key);
            });
        }

        public static Mesh Coins()
        {
            const string key = "l8_tip_coins_v1";
            return ProcMesh.Cached(key, () => new ProcMesh.Builder().AddCylinder(new Vector3(0f, -0.3f, 0f), 0.3f, 0.2f, Quaternion.identity, Color.white, 16, true).ToMesh(key));
        }
    }
}
