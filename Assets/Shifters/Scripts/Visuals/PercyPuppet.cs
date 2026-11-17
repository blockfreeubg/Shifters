// =====================================================================================
//  SHIFTERS - PercyPuppet.cs   (VISUALS module, campaign wave)
//  PERCY THE PERCOLATOR (CAMPAIGN 2.2): chunky retro break-room coffee machine (0.8 x 1.3 x 0.6 m),
//  chrome trim, glass carafe, plate "PERCOLATOR P-3RCY", a small screen face with TMP emoticons
//  (o_o) (^_^) (T_T) (>_<) (-_-)zzz ;) , red BREW lamp, steam puff on every line. Visual only
//  (no collider): StoryKit.PercyMachine wraps it with a talk zone. Feet at the origin, facing +Z.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class PercyPuppet : MonoBehaviour
    {
        public const float Width = 0.8f, Height = 1.3f, Depth = 0.6f;
        public PercyFace Face { get; private set; } = PercyFace.Neutral;
        /// <summary>Speech: the face bounces, the BREW lamp blinks, a steam puff every ~1.3 s.</summary>
        public bool Talking;
        /// <summary>Steam puffs (Fx) while talking / on Puff(). Off in the VillainStudio when it is not live.</summary>
        public bool SteamFx = true;
        /// <summary>Use unscaled time (studio / cutscenes). Default true.</summary>
        public bool UseUnscaledTime = true;
        public TextMeshPro FaceText { get; private set; }
        public Transform Body { get; private set; }
        public Transform Screen { get; private set; }
        /// <summary>World center of the screen face (portrait framing, speech bubbles).</summary>
        public Vector3 FaceCenter => Screen != null ? Screen.position : transform.position + Vector3.up * 1f;
        public Renderer[] Renderers { get; private set; } = new Renderer[0];

        PercyFace _base = PercyFace.Neutral, _temp; float _tempUntil = -1f;
        float _t, _blinkTimer = 3f, _blinkT = -1f, _steamTimer = 0.4f, _bounce, _bounceV, _shakeT = 9f;
        Renderer _lamp; Material _lampMat; Transform _wig, _coffee;
        string _shown;
        static readonly Color ScreenText = new Color(1f, 0.78f, 0.35f);
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static PercyPuppet Create(Transform parent, Vector3 localPosition, Quaternion? localRotation = null)
        {
            var go = new GameObject("Percy");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var p = go.AddComponent<PercyPuppet>();
            p.Build();
            return p;
        }

        void Build()
        {
            var cream = MaterialLibrary.Colored(new Color(0.86f, 0.79f, 0.66f), 0.35f);
            var brown = MaterialLibrary.Colored(VillainLook.PercyBrown, 0.3f);
            var dark = MaterialLibrary.Colored(new Color(0.2f, 0.13f, 0.08f), 0.3f);
            var chrome = MaterialLibrary.Colored(new Color(0.82f, 0.84f, 0.87f), 0.9f);
            var screen = MaterialLibrary.Colored(new Color(0.06f, 0.07f, 0.06f), 0.85f);
            var glass = MaterialLibrary.Transparent(new Color(0.8f, 0.9f, 0.95f, 0.32f));
            var coffee = MaterialLibrary.Colored(new Color(0.22f, 0.12f, 0.05f), 0.6f);
            Body = CharacterModelBuilder.Pivot("Body", transform, Vector3.zero);
            var b = Body;
            // shell: base plinth, back column, top head (brown band), chrome trims
            ProcMesh.Part("Shell", b, ProcMesh.Cached("percy_shell", () =>
            {
                var m = new ProcMesh.Builder();
                m.AddBox(new Vector3(0f, 0.47f, -0.15f), new Vector3(0.8f, 0.7f, 0.3f), Quaternion.identity, Color.white, 0.03f);   // back column
                m.AddBox(new Vector3(-0.31f, 0.47f, 0.12f), new Vector3(0.18f, 0.7f, 0.3f), Quaternion.identity, Color.white, 0.03f); // side pillars
                m.AddBox(new Vector3(0.31f, 0.47f, 0.12f), new Vector3(0.18f, 0.7f, 0.3f), Quaternion.identity, Color.white, 0.03f);
                m.AddBox(new Vector3(0f, 1.03f, 0f), new Vector3(0.8f, 0.46f, 0.6f), Quaternion.identity, Color.white, 0.05f);       // head
                return m.ToMesh("PercyShell");
            }), cream, Vector3.zero);
            ProcMesh.Part("Trim", b, ProcMesh.Cached("percy_trim", () =>
            {
                var m = new ProcMesh.Builder();
                m.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.84f, 0.12f, 0.64f), Quaternion.identity, Color.white, 0.03f);     // plinth
                m.AddBox(new Vector3(0f, 0.15f, 0.1f), new Vector3(0.44f, 0.03f, 0.36f), Quaternion.identity, Color.white, 0.01f);   // drip tray
                m.AddBox(new Vector3(0f, 0.8f, 0.01f), new Vector3(0.84f, 0.04f, 0.64f), Quaternion.identity, Color.white, 0.015f);  // waist trim
                m.AddBox(new Vector3(0f, 1.27f, 0f), new Vector3(0.84f, 0.04f, 0.64f), Quaternion.identity, Color.white, 0.015f);    // lid trim
                m.AddCylinder(new Vector3(0f, 0.77f, 0.12f), 0.05f, 0.05f, Quaternion.identity, Color.white, 10, true);             // spout
                m.AddCylinder(new Vector3(0.28f, 1.3f, -0.12f), 0.035f, 0.06f, Quaternion.identity, Color.white, 8, true);          // whistle
                m.AddCone(new Vector3(0.28f, 1.33f, -0.12f), 0.035f, 0.05f, Quaternion.identity, Color.white, 8);
                // screen bezel
                m.AddBox(new Vector3(0f, 1.05f, 0.3f), new Vector3(0.5f, 0.3f, 0.03f), Quaternion.identity, Color.white, 0.02f);
                return m.ToMesh("PercyTrim");
            }), chrome, Vector3.zero);
            ProcMesh.Part("Band", b, ProcMesh.Cached("percy_band", () =>
            {
                var m = new ProcMesh.Builder();
                m.AddBox(new Vector3(0f, 0.86f, 0.005f), new Vector3(0.81f, 0.08f, 0.61f), Quaternion.identity, Color.white, 0.01f);
                m.AddBox(new Vector3(0f, 0.86f, 0.305f), new Vector3(0.6f, 0.06f, 0.012f), Quaternion.identity, Color.white, 0.004f); // plate
                return m.ToMesh("PercyBand");
            }), brown, Vector3.zero);
            // screen face
            Screen = CharacterModelBuilder.Pivot("Screen", b, new Vector3(0f, 1.05f, 0.318f));
            ProcMesh.Part("ScreenGlass", Screen, ProcMesh.CachedBox(new Vector3(0.44f, 0.24f, 0.01f), 0.004f), screen, Vector3.zero, null, null, false);
            FaceText = EnvKit.AddText(Screen, "(o_o)", new Vector3(0f, 0f, 0.008f), new Vector2(0.42f, 0.2f), ScreenText, ShiftersFonts.Numbers, 0.15f);
            FaceText.enableAutoSizing = false; FaceText.fontSize = 1.05f; FaceText.textWrappingMode = TextWrappingModes.NoWrap;
            FaceText.overflowMode = TextOverflowModes.Overflow;
            var plate = EnvKit.AddText(b, "PERCOLATOR P-3RCY", new Vector3(0f, 0.86f, 0.313f), new Vector2(0.56f, 0.05f), new Color(0.95f, 0.88f, 0.7f), ShiftersFonts.Label, 0.05f);
            plate.enableAutoSizing = false; plate.fontSize = 0.32f;
            // carafe in the front recess
            ProcMesh.Part("Carafe", b, VisualMeshes.Lathe("percy_carafe", new[]
            {
                new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0.1f, 0.1f, 0f), new Vector4(0.12f, 0.13f, 0.13f, 0f),
                new Vector4(0.25f, 0.1f, 0.1f, 0f), new Vector4(0.32f, 0.07f, 0.07f, 0f), new Vector4(0.32f, 0f, 0f, 0f)
            }, 2f, 16), glass, new Vector3(0f, 0.165f, 0.1f), null, null, false);
            _coffee = ProcMesh.Part("Coffee", b, VisualMeshes.Lathe("percy_coffee", new[]
            {
                new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0.09f, 0.09f, 0f), new Vector4(0.11f, 0.12f, 0.12f, 0f), new Vector4(0.15f, 0.115f, 0.115f, 0f), new Vector4(0.15f, 0f, 0f, 0f)
            }, 2f, 14), coffee, new Vector3(0f, 0.17f, 0.1f), null, null, false).transform;
            ProcMesh.Part("Handle", b, ProcMesh.Cached("percy_handle", () =>
            {
                var m = new ProcMesh.Builder();
                m.AddTorus(Vector3.zero, 0.07f, 0.014f, Quaternion.Euler(0f, 0f, 90f), Color.white, 12, 5);
                return m.ToMesh("Handle");
            }), dark, new Vector3(0.14f, 0.33f, 0.1f), null, null, false);
            // BREW lamp
            _lampMat = MaterialLibrary.Instance(new Color(0.85f, 0.1f, 0.08f), 0.8f);
            _lamp = ProcMesh.Part("BrewLamp", b, VisualMeshes.Sphere(0.03f, 10, 7), _lampMat, new Vector3(0.3f, 0.71f, 0.27f), null, null, false).GetComponent<Renderer>();
            var lbl = EnvKit.AddText(b, "BREW", new Vector3(0.3f, 0.66f, 0.274f), new Vector2(0.12f, 0.03f), new Color(0.3f, 0.2f, 0.12f), ShiftersFonts.Label, 0.03f);
            lbl.enableAutoSizing = false; lbl.fontSize = 0.22f;
            Renderers = GetComponentsInChildren<Renderer>(true);
            Show(PercyFace.Neutral, true);
            SetLamp(0.4f);
        }

        // ------------------------------------------------------------------ API
        public void SetFace(PercyFace f, float duration = 0f)
        {
            if (duration > 0f) { _temp = f; _tempUntil = _t + duration; }
            else { _base = f; _tempUntil = -1f; }
            if (f == PercyFace.Upset) _shakeT = 0f;
            Kick(0.6f);
        }

        /// <summary>Dialogue expressions (StoryLine.Face) mapped onto the screen.</summary>
        public void SetExpression(Expression e, float duration = 0f) => SetFace(VillainLook.PercyFor(e), duration);

        /// <summary>Steam puff from the whistle (every line).</summary>
        public void Puff()
        {
            Kick(1f);
            if (SteamFx && Application.isPlaying) Fx.Steam(transform.TransformPoint(new Vector3(0.28f, 1.38f, -0.12f)), transform.up, 0.6f);
        }

        /// <summary>Tiny judge wig (CH14 clerk of the court).</summary>
        public void SetWig(bool on)
        {
            if (on == (_wig != null)) return;
            if (!on) { Kit.SafeDestroy(_wig.gameObject); _wig = null; Renderers = GetComponentsInChildren<Renderer>(true); return; }
            _wig = ProcMesh.Part("JudgeWig", Body, ProcMesh.Cached("percy_wig", () =>
            {
                var m = new ProcMesh.Builder();
                m.AddSphere(new Vector3(0f, 0.04f, 0f), 1f, Quaternion.identity, Color.white, 12, 7, true, new Vector3(0.2f, 0.07f, 0.16f));
                for (int s = -1; s <= 1; s += 2)
                    for (int i = 0; i < 3; i++) m.AddSphere(new Vector3(s * 0.17f, -0.01f - i * 0.05f, 0f), 0.035f, Quaternion.identity, Color.white, 8, 5);
                return m.ToMesh("PercyWig");
            }), MaterialLibrary.Colored(new Color(0.95f, 0.95f, 0.93f), 0.2f), new Vector3(0f, 1.31f, 0f)).transform;
            Renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>Squash-and-stretch hop (positive) or settle.</summary>
        public void Kick(float amount) { _bounceV += amount * 2.2f; }

        public PercyFace CurrentFace => (_tempUntil >= 0f && _t < _tempUntil) ? _temp : _base;

        // ------------------------------------------------------------------ tick
        void LateUpdate() { Tick(UseUnscaledTime ? TimeController.RealDeltaTime : Time.deltaTime); }

        public void Tick(float dt)
        {
            if (Body == null) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _t += dt;
            var f = CurrentFace;
            // blink
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f) { _blinkT = 0.13f; _blinkTimer = 2.5f + (Mathf.Sin(_t * 7.3f) * 0.5f + 0.5f) * 3f; }
            if (_blinkT > 0f) _blinkT -= dt;
            bool blink = _blinkT > 0f && (f == PercyFace.Neutral || f == PercyFace.Happy || f == PercyFace.Shocked);
            if (f == PercyFace.Static) Show(f, false, Glitch());
            else Show(f, false, blink ? "(-_-)" : null);
            // talking: bounce text, lamp blink, steam
            float talk = Talking ? Mathf.Abs(Mathf.Sin(_t * 13f)) : 0f;
            if (FaceText != null) FaceText.transform.localScale = new Vector3(1f + 0.04f * talk, 1f + 0.12f * talk, 1f);
            SetLamp(Talking ? (Mathf.Repeat(_t, 0.5f) < 0.25f ? 1f : 0.3f) : 0.45f + 0.1f * Mathf.Sin(_t * 2f));
            if (Talking)
            {
                _steamTimer -= dt;
                if (_steamTimer <= 0f) { _steamTimer = 1.3f; Puff(); }
            }
            // body: breathing + bounce spring + upset rattle
            AnimMath.Spring(ref _bounce, ref _bounceV, 0f, 240f, 10f, dt);
            float sy = 1f + Mathf.Clamp(_bounce, -0.12f, 0.15f) * 0.6f + 0.008f * Mathf.Sin(_t * 1.8f) + 0.02f * talk;
            Body.localScale = new Vector3(1f / Mathf.Sqrt(sy), sy, 1f / Mathf.Sqrt(sy));
            _shakeT += dt;
            float shake = _shakeT < 0.6f ? Mathf.Sin(_shakeT * 60f) * 2.5f * (1f - _shakeT / 0.6f) : 0f;
            if (f == PercyFace.Upset) shake += Mathf.Sin(_t * 41f) * 0.6f;
            Body.localRotation = Quaternion.Euler(0f, 0f, shake);
            if (_coffee != null) _coffee.localScale = new Vector3(1f, 1f + 0.06f * Mathf.Sin(_t * 3.1f) + 0.1f * talk, 1f);
        }

        string Glitch()
        {
            const string g = "#%&@!?*$";
            int k = Mathf.FloorToInt(_t * 14f);
            var c = new char[5];
            for (int i = 0; i < 5; i++) c[i] = g[(k * 7 + i * 3) % g.Length];
            return new string(c);
        }

        void Show(PercyFace f, bool force, string overrideText = null)
        {
            if (FaceText == null) return;
            string s = overrideText ?? VillainLook.PercyText(f);
            if (!force && s == _shown) return;
            _shown = s;
            FaceText.text = s;
            FaceText.fontSize = s.Length > 6 ? 0.85f : 1.05f;
        }

        void SetLamp(float k)
        {
            if (_lampMat == null) return;
            _lampMat.EnableKeyword("_EMISSION");
            _lampMat.SetColor(EmissionId, new Color(1f, 0.12f, 0.08f) * (0.3f + 2.2f * k));
        }

        void OnDestroy() { if (_lampMat != null) Kit.SafeDestroy(_lampMat); }
    }
}
