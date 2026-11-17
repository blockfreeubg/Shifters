// =====================================================================================
//  SHIFTERS - CardboardMaxModel.cs   (VISUALS module, campaign wave)
//  Life-size cardboard cutout of Max (1.45 m incl. crown): a flat print of him (grey suit, pear
//  body, big chin, smug mouth, monocle, curly mustache, gold crown "ME") on a cardboard slab with
//  a kickstand. Used by the CardboardMax stalker skin, jump scares and decor. It hops/creaks
//  forward while moving, wobbles when hit or scared, freezes when lit. Feet at origin, faces +Z.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class CardboardMaxModel : MonoBehaviour, IStalkerVisual
    {
        public const float Height = 1.45f;
        public bool Moving { get; private set; }
        public bool Lit { get; private set; }
        public bool UseUnscaledTime;
        public Transform Root => transform;
        public Transform Board { get; private set; }

        float _t, _hopPh, _speed, _wob, _wobV, _lean;

        public static CardboardMaxModel Create(Transform parent, Vector3 localPosition, Quaternion? localRotation = null)
        {
            var go = new GameObject("CardboardMax");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var c = go.AddComponent<CardboardMaxModel>();
            c.Build();
            return c;
        }

        void Build()
        {
            Board = CharacterModelBuilder.Pivot("Board", transform, Vector3.zero);
            var card = MaterialLibrary.Get(MatKey.Cardboard, Kit.Theme);
            var suit = MaterialLibrary.Colored(VillainLook.SuitGrey, 0.15f);
            var skin = MaterialLibrary.Colored(new Color(0.88f, 0.83f, 0.78f), 0.15f);
            var gold = MaterialLibrary.Colored(VillainLook.Gold, 0.5f);
            var ink = MaterialLibrary.Colored(new Color(0.14f, 0.13f, 0.14f), 0.2f);
            const float z = 0.012f;   // print layer on the front face
            // cardboard slab (outline shape, slightly bigger than the print) + kickstand
            ProcMesh.Part("Cardboard", Board, ProcMesh.Cached("cbmax_card", () =>
            {
                var b = new ProcMesh.Builder();
                VisualMeshes.AddFilled(b, VisualMeshes.Ellipse(0.32f, 0.33f, 0.42f, 18), 0.02f, 0f, Vector3.zero);    // body
                VisualMeshes.AddFilled(b, VisualMeshes.Ellipse(0.23f, 0.27f, 0.98f, 18), 0.02f, 0f, Vector3.zero);    // head + chin
                b.AddBox(new Vector3(0f, 1.27f, 0f), new Vector3(0.5f, 0.24f, 0.02f), Quaternion.identity, Color.white); // crown block
                b.AddBox(new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.16f, 0.02f), Quaternion.identity, Color.white); // legs block
                b.AddBox(new Vector3(0f, 0.45f, -0.16f), new Vector3(0.12f, 0.9f, 0.012f), Quaternion.Euler(-21f, 0f, 0f), Color.white); // kickstand
                return b.ToMesh("Cardboard");
            }), card, Vector3.zero);
            ProcMesh.Part("PrintSuit", Board, ProcMesh.Cached("cbmax_suit", () =>
            {
                var b = new ProcMesh.Builder();
                VisualMeshes.AddFilled(b, VisualMeshes.Ellipse(0.29f, 0.3f, 0.42f, 18), 0.004f, 0f, new Vector3(0f, 0f, z));
                b.AddBox(new Vector3(-0.08f, 0.09f, z), new Vector3(0.1f, 0.16f, 0.004f), Quaternion.identity, Color.white);
                b.AddBox(new Vector3(0.08f, 0.09f, z), new Vector3(0.1f, 0.16f, 0.004f), Quaternion.identity, Color.white);
                return b.ToMesh("PrintSuit");
            }), suit, Vector3.zero, null, null, false);
            ProcMesh.Part("PrintSkin", Board, ProcMesh.Cached("cbmax_skin", () =>
            {
                var b = new ProcMesh.Builder();
                VisualMeshes.AddFilled(b, VisualMeshes.Ellipse(0.2f, 0.2f, 1.02f, 18), 0.004f, 0f, new Vector3(0f, 0f, z + 0.002f));
                VisualMeshes.AddFilled(b, VisualMeshes.Ellipse(0.15f, 0.1f, 0.84f, 14), 0.004f, 0f, new Vector3(0f, 0f, z + 0.003f));   // huge chin
                b.AddSphere(new Vector3(-0.27f, 0.36f, z + 0.002f), 1f, Quaternion.identity, Color.white, 8, 5, true, new Vector3(0.05f, 0.06f, 0.004f)); // gloves
                b.AddSphere(new Vector3(0.27f, 0.36f, z + 0.002f), 1f, Quaternion.identity, Color.white, 8, 5, true, new Vector3(0.05f, 0.06f, 0.004f));
                return b.ToMesh("PrintSkin");
            }), skin, Vector3.zero, null, null, false);
            ProcMesh.Part("PrintGold", Board, ProcMesh.Cached("cbmax_gold", () =>
            {
                var b = new ProcMesh.Builder();
                var crown = new System.Collections.Generic.List<Vector2>
                {
                    new Vector2(-0.23f, 1.17f), new Vector2(0.23f, 1.17f), new Vector2(0.25f, 1.38f), new Vector2(-0.25f, 1.38f)
                };
                VisualMeshes.AddFilled(b, crown, 0.004f, 0f, new Vector3(0f, 0f, z + 0.004f));
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.22f + i * 0.11f;
                    VisualMeshes.AddFilled(b, new[] { new Vector2(x - 0.04f, 1.37f), new Vector2(x + 0.04f, 1.37f), new Vector2(x, 1.45f) }, 0.004f, 0f, new Vector3(0f, 0f, z + 0.004f));
                }
                for (int i = 0; i < 3; i++) b.AddSphere(new Vector3(0f, 0.3f + i * 0.09f, z + 0.004f), 0.02f, Quaternion.identity, Color.white, 8, 5, true, new Vector3(1f, 1f, 0.3f));
                b.AddTorus(new Vector3(0.075f, 1.06f, z + 0.006f), 0.042f, 0.007f, Quaternion.Euler(90f, 0f, 0f), Color.white, 16, 4);   // monocle
                return b.ToMesh("PrintGold");
            }), gold, Vector3.zero, null, null, false);
            ProcMesh.Part("PrintInk", Board, ProcMesh.Cached("cbmax_ink", () =>
            {
                var b = new ProcMesh.Builder();
                float zz = z + 0.007f;
                // small eyes + heavy brows (smug: right brow raised)
                b.AddSphere(new Vector3(-0.075f, 1.06f, zz), 0.017f, Quaternion.identity, Color.white, 8, 5, true, new Vector3(1f, 1f, 0.3f));
                b.AddSphere(new Vector3(0.075f, 1.06f, zz), 0.017f, Quaternion.identity, Color.white, 8, 5, true, new Vector3(1f, 1f, 0.3f));
                b.AddBox(new Vector3(-0.08f, 1.12f, zz), new Vector3(0.1f, 0.028f, 0.004f), Quaternion.Euler(0f, 0f, -10f), Color.white);
                b.AddBox(new Vector3(0.08f, 1.14f, zz), new Vector3(0.1f, 0.028f, 0.004f), Quaternion.Euler(0f, 0f, 14f), Color.white);
                // curly mustache
                for (int s = -1; s <= 1; s += 2)
                {
                    b.AddSphere(new Vector3(s * 0.045f, 0.955f, zz), 1f, Quaternion.Euler(0f, 0f, -s * 10f), Color.white, 10, 5, true, new Vector3(0.055f, 0.02f, 0.003f));
                    b.AddTorus(new Vector3(s * 0.11f, 0.975f, zz), 0.018f, 0.007f, Quaternion.Euler(90f, 0f, 0f), Color.white, 10, 4);
                }
                // smug smirk
                VisualMeshes.AddStroke(b, new[] { new Vector2(-0.06f, 0.9f), new Vector2(0f, 0.895f), new Vector2(0.05f, 0.905f), new Vector2(0.08f, 0.925f) }, 0.012f, 0.003f, 0f, new Vector3(0f, 0f, zz));
                // "ME" on the crown (reads from the front, +Z)
                VillainModelBuilder.Letters(b, Matrix4x4.TRS(new Vector3(0f, 1.27f, zz), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.05f), 0.2f);
                // lapels
                VisualMeshes.AddStroke(b, new[] { new Vector2(-0.1f, 0.7f), new Vector2(-0.01f, 0.5f) }, 0.018f, 0.003f, 0f, new Vector3(0f, 0f, zz));
                VisualMeshes.AddStroke(b, new[] { new Vector2(0.1f, 0.7f), new Vector2(0.01f, 0.5f) }, 0.018f, 0.003f, 0f, new Vector3(0f, 0f, zz));
                return b.ToMesh("PrintInk");
            }), ink, Vector3.zero, null, null, false);
        }

        // ------------------------------------------------------------------ API
        public void SetMoving(bool moving, float speed = 0f) { Moving = moving && !Lit; _speed = speed > 0f ? speed : Tuning.StalkerSpeed; }
        public void SetLit(bool lit) { if (lit && !Lit) Wobble(0.6f); Lit = lit; if (lit) Moving = false; }
        public void SetFrozen(bool frozen) { SetLit(frozen); }
        /// <summary>Cardboard wobble (hit, scare reveal). amount ~0.3..1.5.</summary>
        public void Wobble(float amount = 1f) { _wobV += amount * 9f; }

        void LateUpdate()
        {
            float dt = UseUnscaledTime ? TimeController.RealDeltaTime : Time.deltaTime;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _t += dt;
            AnimMath.Spring(ref _wob, ref _wobV, 0f, 120f, 4f, dt);
            float hop = 0f;
            if (Moving)
            {
                _hopPh += dt * Mathf.Clamp(_speed * 0.9f, 1f, 5f);
                hop = Mathf.Abs(Mathf.Sin(_hopPh * Mathf.PI)) * 0.06f;
                _lean = Mathf.Lerp(_lean, 7f, AnimMath.Damp(6f, dt));
            }
            else _lean = Mathf.Lerp(_lean, 0f, AnimMath.Damp(4f, dt));
            if (Board != null)
            {
                Board.localPosition = new Vector3(0f, hop, 0f);
                Board.localRotation = Quaternion.Euler(_lean + _wob * 6f, 0f, _wob * 4f + (Moving ? 3f * Mathf.Sin(_hopPh * Mathf.PI * 2f) : 0f));
            }
        }
    }
}
