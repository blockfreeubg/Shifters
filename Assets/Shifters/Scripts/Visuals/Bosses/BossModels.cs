// =====================================================================================
//  SHIFTERS - BossModels.cs   [owned by VISUALS_BOSSES]
//  Factories for the campaign boss and set-piece models (CAMPAIGN §11.2 / §12, LEVELS2 SHIFT 03 / 08 / 11 / 13):
//    BossModels.Downsizer   (CH03)  7 m hydraulic HR press            -> DownsizerModel
//    BossModels.Porkchop    (CH08)  3.5 m armored robo-piggy-bank      -> PorkchopModel
//    BossModels.GetawayTram (CH08)  tram car dressing for the deck     -> GetawayTramModel
//    BossModels.Boogeyman   (CH11)  6 m shadow with door-sized eyes    -> BoogeymanModel
//    BossModels.ScareOMatic (CH11)  floor buffer in a bedsheet         -> ScareOMaticModel
//    BossModels.OneManBand  (CH13)  9 m gold ME-CHA                    -> OneManBandModel
//    BossModels.SoloPod     (CH13)  105 kg gold hover-chair            -> SoloPodModel
//  Kit conventions: (parent, pos, rot?, ...) with pos/rot LOCAL to parent, everything built inside the factory (edit-mode
//  safe), ProcMesh.Cached keys "vb_*" (fixed geometry per key), shared cached materials, no rigidbodies.
//  Facing: every model faces +Z (its front / face / snout / windshield) with its root on the floor.
// =====================================================================================
using System;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static partial class BossModels
    {
        internal static readonly Quaternion AlongZ = Quaternion.Euler(90f, 0f, 0f);   // cylinder Y axis -> +Z
        internal static readonly Quaternion AlongX = Quaternion.Euler(0f, 0f, 90f);   // cylinder Y axis -> X
        internal static readonly Quaternion ConeBack = Quaternion.Euler(-90f, 0f, 0f); // cone +Y -> -Z
        internal static readonly Color W = Color.white;

        // ------------------------------------------------------------------ palette (grey world; gold is grey-immune)
        internal static readonly Color Steel = new Color(0.42f, 0.45f, 0.5f);
        internal static readonly Color SteelDark = new Color(0.2f, 0.22f, 0.26f);
        internal static readonly Color Ink = new Color(0.1f, 0.1f, 0.12f);
        internal static readonly Color HazardY = new Color(1f, 0.78f, 0.1f);
        internal static readonly Color FiredRed = new Color(0.86f, 0.12f, 0.1f);
        internal static readonly Color Paper = new Color(0.95f, 0.94f, 0.9f);

        // ------------------------------------------------------------------ helpers
        internal static Mesh M(string key, Action<ProcMesh.Builder> build)
            => ProcMesh.Cached("vb_" + key, () => { var b = new ProcMesh.Builder(); build(b); return b.ToMesh("vb_" + key); });

        internal static GameObject P(string name, Transform parent, string key, Action<ProcMesh.Builder> build, Material mat,
            Vector3 pos = default, Quaternion? rot = null, bool shadows = true)
            => ProcMesh.Part(name, parent, M(key, build), mat, pos, rot, null, shadows);

        internal static Transform Node(string name, Transform parent, Vector3 pos, Quaternion? rot = null)
            => Kit.Make(name, parent, pos, rot ?? Quaternion.identity).transform;

        internal static Material C(Color c, float smooth = 0.35f, float emission = 0f) => MaterialLibrary.Colored(c, smooth, emission);
        internal static Material Glow(Color c, float glow = 2f) => MaterialLibrary.Unlit(c, glow);
        internal static Material GoldMat => Kit.Mat(MatKey.Gold);
        internal static Material GlassMat => MaterialLibrary.Transparent(new Color(0.72f, 0.88f, 1f, 0.26f));

        /// <summary>Rotation whose +Z is the outward normal n (the readable side of a Label).</summary>
        internal static Quaternion Face(Vector3 n, Vector3? up = null)
            => Quaternion.LookRotation(n, up ?? (Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up));

        /// <summary>World-space TMP text whose readable side faces faceRot*+Z (a node is created at pos).</summary>
        internal static TextMeshPro Label(Transform parent, string name, string text, Vector3 pos, Quaternion faceRot, Vector2 area,
            Color color, float maxHeight, bool display = true)
        {
            var t = Node(name, parent, pos, faceRot);
            return EnvKit.AddText(t, text, Vector3.zero, area, color, display ? ShiftersFonts.Display : ShiftersFonts.Label, maxHeight);
        }

        internal static T Finish<T>(T model, string id) where T : BossModelBase
        {
            model.ModelId = id;
            model.CollectParts();
            return model;
        }

        internal static GameObject Root(string name, Transform parent, Vector3 pos, Quaternion? rot)
            => Kit.Make(name, parent, pos, rot ?? Quaternion.identity);

        // ------------------------------------------------------------------ Max in the cockpit / on the pod seat
        /// <summary>Puts Maximilian Me at seat (local pos, facing seat +Z, uniform scale). Uses VillainModelBuilder (module
        /// visuals) when it exists (resolved by reflection: any public static Build* whose first parameter is a Transform);
        /// otherwise builds a grey-and-gold stand-in. The returned object is named "Max".</summary>
        public static GameObject SeatMax(Transform seat, Vector3 localPos, float scale = 1f)
        {
            if (seat == null) return null;
            var holder = Node("Max", seat, localPos);
            holder.localScale = Vector3.one * scale;
            GameObject built = null;
            try { built = BuildVillainByReflection(holder); } catch (Exception e) { Debug.LogWarning("[BossModels] VillainModelBuilder failed: " + e.Message); built = null; }
            if (built == null) MaxStandIn(holder, Vector3.zero, 1f);
            return holder.gameObject;
        }

        static GameObject BuildVillainByReflection(Transform holder)
        {
            var type = typeof(BossModels).Assembly.GetType("Shifters.VillainModelBuilder");
            if (type == null) return null;
            MethodInfo best = null;
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!m.Name.StartsWith("Build")) continue;
                var ps = m.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(Transform)) continue;
                if (best == null || m.Name == "Build" && best.Name != "Build" || (m.Name == best.Name && ps.Length < best.GetParameters().Length)) best = m;
            }
            if (best == null) return null;
            var pars = best.GetParameters();
            var args = new object[pars.Length];
            args[0] = holder;
            for (int i = 1; i < pars.Length; i++)
            {
                var pt = pars[i].ParameterType;
                if (pt == typeof(Vector3)) args[i] = Vector3.zero;
                else if (pt == typeof(Quaternion)) args[i] = Quaternion.identity;
                else if (pt == typeof(Quaternion?)) args[i] = (Quaternion?)Quaternion.identity;
                else if (pars[i].HasDefaultValue) args[i] = pars[i].DefaultValue;
                else args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
            int before = holder.childCount;
            var res = best.Invoke(null, args);
            if (res is GameObject g) return g;
            if (res is Component c) return c.gameObject;
            return holder.childCount > before ? holder.GetChild(holder.childCount - 1).gameObject : null;
        }

        /// <summary>Stand-in Maximilian Me (1.2 m, 1.45 m with crown): pear torso in a grey suit, big head, monocle, crown
        /// two sizes too big, gold buttons. Used until VillainModelBuilder exists. Faces +Z.</summary>
        public static GameObject MaxStandIn(Transform parent, Vector3 pos, float scale = 1f)
        {
            var root = Node("MaxStandIn", parent, pos);
            root.localScale = Vector3.one * scale;
            var suit = C(new Color(0.43f, 0.45f, 0.48f), 0.3f);
            var skin = C(new Color(0.96f, 0.78f, 0.64f), 0.3f);
            P("Suit", root, "max_suit", b =>
            {
                b.AddSphere(new Vector3(0f, 0.42f, 0f), 0.3f, Quaternion.identity, W, 14, 9, true, new Vector3(1f, 1.05f, 0.85f));
                b.AddCapsule(new Vector3(-0.13f, 0.12f, 0f), 0.08f, 0.26f, Quaternion.identity, W, 8, 3);
                b.AddCapsule(new Vector3(0.13f, 0.12f, 0f), 0.08f, 0.26f, Quaternion.identity, W, 8, 3);
                b.AddCapsule(new Vector3(-0.31f, 0.5f, 0.05f), 0.065f, 0.34f, Quaternion.Euler(20f, 0f, 25f), W, 8, 3);
                b.AddCapsule(new Vector3(0.31f, 0.5f, 0.05f), 0.065f, 0.34f, Quaternion.Euler(20f, 0f, -25f), W, 8, 3);
            }, suit);
            P("Head", root, "max_head", b =>
            {
                b.AddSphere(new Vector3(0f, 0.92f, 0.02f), 0.23f, Quaternion.identity, W, 14, 9, true, new Vector3(1f, 1f, 0.95f));
                b.AddSphere(new Vector3(0f, 0.8f, 0.14f), 0.13f, Quaternion.identity, W, 10, 7, true, new Vector3(1.1f, 0.8f, 0.9f)); // chin
                b.AddSphere(new Vector3(0f, 0.92f, 0.24f), 0.045f, Quaternion.identity, W, 8, 6, true, null); // nose
            }, skin);
            P("Details", root, "max_details", b =>
            {
                b.AddBox(new Vector3(-0.08f, 1.0f, 0.21f), new Vector3(0.1f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, -12f), W, 0.01f); // brows
                b.AddBox(new Vector3(0.08f, 1.0f, 0.21f), new Vector3(0.1f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, 12f), W, 0.01f);
                b.AddSphere(new Vector3(-0.07f, 0.95f, 0.215f), 0.022f, Quaternion.identity, W, 6, 4, true, null);
                b.AddSphere(new Vector3(0.07f, 0.95f, 0.215f), 0.022f, Quaternion.identity, W, 6, 4, true, null);
                b.AddCapsule(new Vector3(-0.07f, 0.87f, 0.24f), 0.025f, 0.13f, Quaternion.Euler(0f, 0f, 70f), W, 6, 2); // mustache
                b.AddCapsule(new Vector3(0.07f, 0.87f, 0.24f), 0.025f, 0.13f, Quaternion.Euler(0f, 0f, -70f), W, 6, 2);
                b.AddBox(new Vector3(0f, 0.6f, 0.24f), new Vector3(0.05f, 0.2f, 0.02f), Quaternion.identity, W, 0.005f); // tie
            }, C(new Color(0.35f, 0.36f, 0.38f), 0.2f));
            P("Gold", root, "max_gold", b =>
            {
                b.AddCylinder(new Vector3(0f, 1.18f, 0f), 0.21f, 0.16f, Quaternion.identity, W, 14, true);                       // crown band
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f;
                    b.AddCone(new Vector3(Mathf.Sin(a) * 0.17f, 1.26f, Mathf.Cos(a) * 0.17f), 0.06f, 0.15f, Quaternion.identity, W, 6);
                }
                b.AddTorus(new Vector3(0.075f, 0.95f, 0.225f), 0.04f, 0.008f, AlongZ, W, 12, 4);                                // monocle
                for (int i = 0; i < 3; i++) b.AddSphere(new Vector3(0f, 0.32f + i * 0.11f, 0.27f - i * 0.005f), 0.022f, Quaternion.identity, W, 6, 4, true, null);
            }, GoldMat);
            return root.gameObject;
        }
    }
}
