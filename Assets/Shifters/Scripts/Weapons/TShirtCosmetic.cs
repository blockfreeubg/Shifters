// =====================================================================================
//  SHIFTERS - TShirtCosmetic.cs   [owned by the WEAPONS agent]
//  T-SHIRT CANNON hit on a teammate: a cosmetic "I SURVIVED THE SHIFT" tee over the torso
//  (no damage, no physics). Stays for the level attempt. Visual only (no colliders).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class TShirtCosmetic : MonoBehaviour
    {
        public const string Slogan = "I SURVIVED THE SHIFT";
        static readonly Color[] ShirtColors = { UIColors.Hex(0xFF4D9A), UIColors.Hex(0x3DE0FF), UIColors.Hex(0xFFD23C), UIColors.Hex(0x7BD389) };
        static readonly string[] Lines = { "FREE MERCH!", "Is this... my size?", "I SURVIVED THE SHIFT!", "Swag!" };
        static int _n;

        public ShifterCharacter Character { get; private set; }
        public int Shirts { get; private set; }

        /// <summary>Dress the character (again: another color). Returns the cosmetic.</summary>
        public static TShirtCosmetic Give(ShifterCharacter c)
        {
            if (c == null) return null;
            var t = c.GetComponent<TShirtCosmetic>();
            if (t == null) { t = c.gameObject.AddComponent<TShirtCosmetic>(); t.Character = c; }
            t.Dress(ShirtColors[_n++ % ShirtColors.Length]);
            return t;
        }

        void Dress(Color color)
        {
            Shirts++;
            var old = transform.Find("WeaponsTShirt");
            if (old != null) Kit.SafeDestroy(old.gameObject);
            float h = Character != null ? Character.Height : 1.8f, r = Character != null ? Character.Radius : 0.4f;
            var root = new GameObject("WeaponsTShirt").transform;
            Transform anchor = transform;
            var v = Character != null ? Character.Visual : null;
            root.SetParent(anchor, false);
            root.localPosition = new Vector3(0f, h * 0.6f, 0f);
            var body = ProcMesh.Cached("tshirt_body", () => ProcMesh.Cylinder(0.5f, 1f, 12, true));
            var mat = MaterialLibrary.Colored(color, 0.15f);
            ProcMesh.Part("Tee", root, body, mat, Vector3.zero, null, new Vector3(r * 2.25f, h * 0.24f, r * 1.9f), false);
            var sleeve = ProcMesh.Cached("tshirt_sleeve", () => ProcMesh.Cylinder(0.5f, 1f, 8, true));
            ProcMesh.Part("SleeveL", root, sleeve, mat, new Vector3(-r * 1.15f, h * 0.07f, 0f), Quaternion.Euler(0f, 0f, 70f), new Vector3(r * 0.55f, r * 0.7f, r * 0.55f), false);
            ProcMesh.Part("SleeveR", root, sleeve, mat, new Vector3(r * 1.15f, h * 0.07f, 0f), Quaternion.Euler(0f, 0f, -70f), new Vector3(r * 0.55f, r * 0.7f, r * 0.55f), false);
            var logo = ProcMesh.Cached("tshirt_logo", () => ProcMesh.Box(new Vector3(1f, 1f, 0.02f)));
            ProcMesh.Part("Logo", root, logo, MaterialLibrary.Unlit(Color.white, 1f), new Vector3(0f, h * 0.02f, r * 0.96f), null, new Vector3(r * 0.9f, h * 0.07f, 1f), false);
            Fx.Poof(transform.position + Vector3.up * h * 0.6f, color, 0.6f);
            Fx.FloatingText(transform.position + Vector3.up * (h + 0.3f), Slogan, color, 0.8f);
            if (Character != null) Character.Say(Lines[(_n + Shirts) % Lines.Length]);
            if (v == null) { }
        }
    }
}
