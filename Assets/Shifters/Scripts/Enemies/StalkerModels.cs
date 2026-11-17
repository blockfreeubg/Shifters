// =====================================================================================
//  SHIFTERS - StalkerModels.cs   [owned by the ENEMIES agent]
//  Placeholder (but readable) models for the stalkers and the guard-bot cap, built from cached
//  ProcMesh parts. Front = local +Z. Visuals' dedicated models can replace these later.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class StalkerModels
    {
        /// <summary>Builds the skin under root (feet at 0, facing +Z). Returns the model height.</summary>
        public static float Build(Transform root, StalkerSkin skin)
        {
            return skin == StalkerSkin.CardboardMax ? CardboardMax(root) : Dummy(root);
        }

        /// <summary>Team-Building Dummy: 2.0 m wooden mannequin, painted smile, party hat, name tag.</summary>
        static float Dummy(Transform root)
        {
            var wood = ProcMesh.Cached("stalker_dummy_wood", () =>
            {
                var b = new ProcMesh.Builder();
                Color w = Color.white;
                for (int s = -1; s <= 1; s += 2)
                {
                    b.AddCapsule(new Vector3(s * 0.13f, 0.5f, 0f), 0.1f, 1.0f, Quaternion.identity, w, 10, 3);          // legs
                    b.AddCapsule(new Vector3(s * 0.36f, 1.2f, 0.02f), 0.07f, 0.78f, Quaternion.Euler(0f, 0f, s * 7f), w, 8, 3); // arms
                    b.AddSphere(new Vector3(s * 0.4f, 0.8f, 0.04f), 0.08f, Quaternion.identity, w, 8, 6);              // hands
                    b.AddSphere(new Vector3(s * 0.13f, 0.98f, 0f), 0.09f, Quaternion.identity, w, 8, 6);               // knees... hips
                }
                b.AddBox(new Vector3(0f, 1.0f, 0f), new Vector3(0.42f, 0.18f, 0.24f), Quaternion.identity, w, 0.06f);   // hips
                b.AddCapsule(new Vector3(0f, 1.32f, 0f), 0.23f, 0.62f, Quaternion.identity, w, 12, 4);                  // torso
                b.AddCylinder(new Vector3(0f, 1.66f, 0f), 0.06f, 0.14f, Quaternion.identity, w, 8, true);              // neck
                b.AddSphere(new Vector3(0f, 1.82f, 0f), 0.17f, Quaternion.identity, w, 14, 10, true, new Vector3(1f, 1.12f, 1f)); // head
                b.AddCylinder(new Vector3(0f, 0.02f, 0f), 0.3f, 0.04f, Quaternion.identity, w, 16, true);              // stand disc
                return b.ToMesh("stalker_dummy_wood");
            });
            var face = ProcMesh.Cached("stalker_dummy_face", () =>
            {
                var b = new ProcMesh.Builder();
                Color k = Color.white;
                b.AddBox(new Vector3(-0.06f, 1.87f, 0.165f), new Vector3(0.035f, 0.05f, 0.02f), Quaternion.identity, k);   // eyes
                b.AddBox(new Vector3(0.06f, 1.87f, 0.165f), new Vector3(0.035f, 0.05f, 0.02f), Quaternion.identity, k);
                for (int i = -3; i <= 3; i++)                                                                             // painted smile
                {
                    float x = i * 0.025f, y = 1.76f + (i * i) * 0.004f;
                    b.AddBox(new Vector3(x, y, 0.16f - Mathf.Abs(i) * 0.006f), new Vector3(0.028f, 0.016f, 0.02f), Quaternion.Euler(0f, 0f, i * 8f), k);
                }
                return b.ToMesh("stalker_dummy_face");
            });
            var hat = ProcMesh.Cached("stalker_dummy_hat", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCone(new Vector3(0f, 1.95f, 0f), 0.11f, 0.3f, Quaternion.Euler(-8f, 0f, 6f), Color.white, 12);
                return b.ToMesh("stalker_dummy_hat");
            });
            ProcMesh.Part("Wood", root, wood, Kit.Mat(MatKey.Wood), Vector3.zero);
            ProcMesh.Part("Face", root, face, Kit.Mat(MatKey.Black), Vector3.zero, null, null, false);
            ProcMesh.Part("PartyHat", root, hat, Kit.Mat(MatKey.SafetyOrange), Vector3.zero);
            ProcMesh.Part("Pompom", root, ProcMesh.Sphere(0.04f, 8, 6), Kit.Mat(MatKey.HazardYellow), new Vector3(0.03f, 2.25f, -0.04f), null, null, false);
            Kit.BoxVisual(root, "NameTag", new Vector3(0f, 1.42f, 0.235f), new Vector3(0.24f, 0.14f, 0.012f), MatKey.SignWhite, 0f, null, false);
            Label(root, "NameText", "HELLO\nI'M TEAM", new Vector3(0f, 1.42f, 0.243f), 0.55f, new Vector2(0.24f, 0.14f), UIColors.Hex(0xD03030));
            return 2.0f;
        }

        /// <summary>Cardboard Max: a 1.95 m flat cutout of the villain (suit, crown, smug grin) on a cardboard strut.</summary>
        static float CardboardMax(Transform root)
        {
            var board = ProcMesh.Cached("stalker_cardboard_board", () =>
            {
                var b = new ProcMesh.Builder();
                Color w = Color.white;
                b.AddBox(new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.9f, 0.04f), Quaternion.identity, w, 0.01f);        // torso
                b.AddBox(new Vector3(0f, 0.27f, 0f), new Vector3(0.5f, 0.5f, 0.04f), Quaternion.identity, w, 0.01f);         // legs
                b.AddCylinder(new Vector3(0f, 1.62f, 0f), 0.24f, 0.04f, Quaternion.Euler(90f, 0f, 0f), w, 18, true);         // head disc
                for (int s = -1; s <= 1; s += 2) b.AddBox(new Vector3(s * 0.43f, 1.05f, 0f), new Vector3(0.18f, 0.7f, 0.04f), Quaternion.Euler(0f, 0f, -s * 12f), w, 0.01f); // arms
                b.AddWedge(new Vector3(0f, 0f, -0.32f), new Vector3(0.05f, 1.1f, 0.6f), Quaternion.identity, w);           // back strut
                return b.ToMesh("stalker_cardboard_board");
            });
            var suit = ProcMesh.Cached("stalker_cardboard_suit", () =>
            {
                var b = new ProcMesh.Builder();
                Color w = Color.white;
                b.AddBox(new Vector3(0f, 0.97f, 0.025f), new Vector3(0.64f, 0.82f, 0.01f), Quaternion.identity, w);
                b.AddBox(new Vector3(0f, 0.28f, 0.025f), new Vector3(0.44f, 0.46f, 0.01f), Quaternion.identity, w);
                b.AddBox(new Vector3(-0.045f, 1.62f, 0.03f), new Vector3(0.03f, 0.04f, 0.01f), Quaternion.identity, w);      // eyes
                b.AddBox(new Vector3(0.075f, 1.62f, 0.03f), new Vector3(0.03f, 0.04f, 0.01f), Quaternion.identity, w);
                b.AddBox(new Vector3(0.02f, 1.52f, 0.03f), new Vector3(0.16f, 0.025f, 0.01f), Quaternion.Euler(0f, 0f, 10f), w); // smug grin
                b.AddBox(new Vector3(0.015f, 1.56f, 0.03f), new Vector3(0.14f, 0.03f, 0.01f), Quaternion.identity, w);          // mustache
                return b.ToMesh("stalker_cardboard_suit");
            });
            var crown = ProcMesh.Cached("stalker_cardboard_crown", () =>
            {
                var b = new ProcMesh.Builder();
                Color w = Color.white;
                b.AddBox(new Vector3(0f, 1.88f, 0.03f), new Vector3(0.36f, 0.08f, 0.012f), Quaternion.identity, w);
                for (int i = -2; i <= 2; i++) b.AddBox(new Vector3(i * 0.08f, 1.96f, 0.03f), new Vector3(0.05f, 0.1f, 0.012f), Quaternion.Euler(0f, 0f, 45f), w);
                b.AddBox(new Vector3(0f, 1.13f, 0.034f), new Vector3(0.05f, 0.3f, 0.01f), Quaternion.identity, w);             // tie
                return b.ToMesh("stalker_cardboard_crown");
            });
            ProcMesh.Part("Board", root, board, Kit.Mat(MatKey.Cardboard), Vector3.zero);
            ProcMesh.Part("Suit", root, suit, Kit.Mat(MatKey.Black), Vector3.zero, null, null, false);
            ProcMesh.Part("Crown", root, crown, Kit.Mat(MatKey.Gold), Vector3.zero, null, null, false);
            Label(root, "Caption", "YOUR CEO\n(LIFE SIZE)", new Vector3(0f, 0.3f, 0.04f), 0.7f, new Vector2(0.44f, 0.3f), UIColors.Hex(0xF2C14E));
            return 1.95f;
        }

        /// <summary>Guard cap (navy crown, black visor, gold badge) on a bot's HeadAnchor.</summary>
        public static Transform GuardCap(Transform headAnchor)
        {
            var cap = Kit.Make("GuardCap", headAnchor, new Vector3(0f, -0.08f, 0f));
            var mesh = ProcMesh.Cached("guard_cap", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(new Vector3(0f, 0.1f, 0f), 0.4f, 0.2f, Quaternion.identity, Color.white, 16, true);
                b.AddCylinder(new Vector3(0f, 0.24f, -0.03f), 0.46f, 0.08f, Quaternion.Euler(-6f, 0f, 0f), Color.white, 16, true);
                return b.ToMesh("guard_cap");
            });
            ProcMesh.Part("Crown", cap.transform, mesh, Kit.Mat(MatKey.HazardBlack), Vector3.zero);
            Kit.BoxVisual(cap.transform, "Visor", new Vector3(0f, 0.02f, 0.42f), new Vector3(0.55f, 0.04f, 0.3f), MatKey.Black, 0.02f, Quaternion.Euler(12f, 0f, 0f));
            Kit.BoxVisual(cap.transform, "Badge", new Vector3(0f, 0.16f, 0.41f), new Vector3(0.14f, 0.12f, 0.03f), MatKey.Gold, 0.01f, null, false);
            Kit.BoxVisual(cap.transform, "Band", new Vector3(0f, 0.05f, 0f), new Vector3(0.83f, 0.05f, 0.83f), MatKey.Gold, 0.02f, null, false);
            return cap.transform;
        }

        static void Label(Transform root, string name, string text, Vector3 pos, float size, Vector2 box, Color color)
        {
            var go = Kit.Make(name, root, pos, Quaternion.Euler(0f, 180f, 0f));   // readable from +Z (the front)
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = box;
        }
    }
}
