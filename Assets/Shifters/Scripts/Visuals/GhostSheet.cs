// =====================================================================================
//  SHIFTERS - GhostSheet.cs   (VISUALS module, campaign wave)
//  One friendly bedsheet ghost of a TEAM PAST (CAMPAIGN 2.4): a glowing sheet with eye holes,
//  a little "o" mouth, stubby sheet arms and a name tag ("HELLO, WE WERE: MARKETING"). Floats,
//  bobs, ripples its hem. Boo() = scare pop, Bow() = polite punchline, Wave(). Not dead - just
//  disbanded. Visual only. Origin = floor point under it; it hovers ~0.35 m up.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class GhostSheet : MonoBehaviour
    {
        public const float Height = 1.5f;
        public string Tag { get; private set; }
        public float Phase;
        public float HoverHeight = 0.35f;
        public bool UseUnscaledTime;
        public Transform Body { get; private set; }
        TextMeshPro _tagText;
        Transform _armL, _armR;
        float _t, _booT = 9f, _bowT = 9f, _waveT = 9f;

        public static GhostSheet Create(Transform parent, Vector3 localPosition, Quaternion? localRotation = null, string tag = null)
        {
            var go = new GameObject("Ghost_" + (string.IsNullOrEmpty(tag) ? "Team" : tag));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var g = go.AddComponent<GhostSheet>();
            g.Build(tag);
            return g;
        }

        void Build(string tag)
        {
            Tag = tag ?? "";
            var sheet = MaterialLibrary.Get(MatKey.GhostGlow, Kit.Theme);
            var holes = MaterialLibrary.Unlit(new Color(0.05f, 0.06f, 0.1f), 1f);
            Body = CharacterModelBuilder.Pivot("Body", transform, new Vector3(0f, HoverHeight, 0f));
            ProcMesh.Part("Sheet", Body, ProcMesh.Cached("ghost_sheet", () =>
            {
                // dome head + flared wavy hem (lathe-like, built by hand so the hem can wave)
                var b = new ProcMesh.Builder();
                const int seg = 20, rings = 9;
                Vector3 V(int i, int r)
                {
                    float a = i / (float)seg * Mathf.PI * 2f, v = r / (float)(rings - 1);
                    float y = Mathf.Lerp(1.15f, 0f, v);
                    float rad = v < 0.35f ? Mathf.Sin(Mathf.Lerp(0.15f, Mathf.PI * 0.5f, v / 0.35f)) * 0.3f : Mathf.Lerp(0.3f, 0.46f, (v - 0.35f) / 0.65f);
                    if (r == rings - 1) y += Mathf.Sin(a * 6f) * 0.05f;
                    return new Vector3(Mathf.Sin(a) * rad, y, Mathf.Cos(a) * rad * 0.9f);
                }
                for (int r = 0; r < rings - 1; r++)
                    for (int i = 0; i < seg; i++)
                    {
                        var p0 = V(i, r); var p1 = V(i + 1, r); var p2 = V(i + 1, r + 1); var p3 = V(i, r + 1);
                        b.Quad(p0, p1, p2, p3, Color.white); b.Quad(p3, p2, p1, p0, Color.white);
                    }
                b.AddSphere(new Vector3(0f, 1.15f, 0f), 0.05f, Quaternion.identity, Color.white, 8, 5);
                return b.ToMesh("GhostSheet");
            }), sheet, Vector3.zero, null, null, false);
            ProcMesh.Part("Holes", Body, ProcMesh.Cached("ghost_holes", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddSphere(new Vector3(-0.09f, 0.88f, 0.255f), 1f, Quaternion.identity, Color.white, 10, 6, true, new Vector3(0.045f, 0.065f, 0.02f));
                b.AddSphere(new Vector3(0.09f, 0.88f, 0.255f), 1f, Quaternion.identity, Color.white, 10, 6, true, new Vector3(0.045f, 0.065f, 0.02f));
                b.AddSphere(new Vector3(0f, 0.74f, 0.27f), 1f, Quaternion.identity, Color.white, 8, 6, true, new Vector3(0.03f, 0.035f, 0.015f));
                return b.ToMesh("GhostHoles");
            }), holes, Vector3.zero, null, null, false);
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                var arm = CharacterModelBuilder.Pivot(s == 0 ? "ArmL" : "ArmR", Body, new Vector3(sx * 0.3f, 0.62f, 0.02f));
                ProcMesh.Part("Arm", arm, VisualMeshes.Ellipsoid(new Vector3(0.06f, 0.16f, 0.06f), 8, 6), sheet, new Vector3(sx * 0.03f, -0.12f, 0f), Quaternion.Euler(0f, 0f, sx * 25f), null, false);
                if (s == 0) _armL = arm; else _armR = arm;
            }
            if (!string.IsNullOrEmpty(Tag))
            {
                ProcMesh.Part("TagCard", Body, ProcMesh.CachedBox(new Vector3(0.3f, 0.15f, 0.01f), 0.01f), MaterialLibrary.Colored(new Color(0.96f, 0.96f, 0.94f), 0.3f), new Vector3(0f, 0.48f, 0.33f), Quaternion.Euler(-8f, 0f, 0f), null, false);
                var hello = EnvKit.AddText(Body, "HELLO, WE WERE", new Vector3(0f, 0.53f, 0.34f), new Vector2(0.27f, 0.03f), new Color(0.8f, 0.2f, 0.2f), ShiftersFonts.Label, 0.03f);
                hello.enableAutoSizing = false; hello.fontSize = 0.2f;
                _tagText = EnvKit.AddText(Body, Tag.ToUpperInvariant(), new Vector3(0f, 0.465f, 0.342f), new Vector2(0.27f, 0.08f), new Color(0.12f, 0.12f, 0.15f), ShiftersFonts.Label, 0.07f);
                _tagText.fontSizeMax = 0.5f;
            }
        }

        public void Boo() { _booT = 0f; }
        public void Bow() { _bowT = 0f; }
        public void Wave() { _waveT = 0f; }

        void LateUpdate()
        {
            float dt = UseUnscaledTime ? TimeController.RealDeltaTime : Time.deltaTime;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _t += dt; _booT += dt; _bowT += dt; _waveT += dt;
            if (Body == null) return;
            float t = _t + Phase;
            float boo = _booT < 0.6f ? AnimMath.Bump(_booT / 0.6f, 0.15f, 0.6f) : 0f;
            float bow = _bowT < 1.2f ? AnimMath.Bump(_bowT / 1.2f, 0.3f, 0.7f) : 0f;
            Body.localPosition = new Vector3(0f, HoverHeight + 0.08f * Mathf.Sin(t * 1.7f) + 0.15f * boo, 0.25f * boo);
            float sq = 1f + 0.03f * Mathf.Sin(t * 3.4f) + 0.25f * boo;
            Body.localScale = new Vector3(1f / Mathf.Sqrt(sq) + 0.1f * boo, sq, 1f / Mathf.Sqrt(sq));
            Body.localRotation = Quaternion.Euler(4f * Mathf.Sin(t * 1.1f) + 38f * bow, 0f, 5f * Mathf.Sin(t * 0.8f + 1f));
            float armUp = 70f * boo;
            float wave = _waveT < 1.6f ? Mathf.Sin(_waveT * 12f) * 30f + 110f : 0f;
            if (_armL != null) _armL.localRotation = Quaternion.Euler(-armUp, 0f, -armUp * 0.8f + 8f * Mathf.Sin(t * 2.3f));
            if (_armR != null) _armR.localRotation = Quaternion.Euler(-armUp, 0f, armUp * 0.8f + wave - 8f * Mathf.Sin(t * 2.1f));
        }
    }
}
