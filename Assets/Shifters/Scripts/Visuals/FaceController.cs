// =====================================================================================
//  SHIFTERS - FaceController.cs   (VISUALS module)
//  Expressive cartoon face: big eyes (white + pupil + glint) that blink and look at a target,
//  eyebrows that angle/raise per expression, swappable low-poly mouth shapes with a little
//  pop, X-eyes for KO and spinning spiral eyes for Dizzy. No per-frame allocations.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public enum MouthShape { Smile, Grin, O, Flat, Wavy, Grit, Frown, Smug }

    public class FaceController
    {
        struct FaceParams
        {
            public float BrowAngle, BrowHeight, Asym, EyeOpen, Pupil, LookDown; public MouthShape Mouth; public Vector2 MouthScale; public int EyeMode;
            public FaceParams(float ba, float bh, float asym, float open, float pupil, MouthShape m, float mx, float my, int eyeMode = 0, float lookDown = 0f)
            { BrowAngle = ba; BrowHeight = bh; Asym = asym; EyeOpen = open; Pupil = pupil; Mouth = m; MouthScale = new Vector2(mx, my); EyeMode = eyeMode; LookDown = lookDown; }
        }

        static FaceParams For(Expression e)
        {
            switch (e)
            {
                case Expression.Happy: return new FaceParams(-6f, 0.25f, 0f, 0.92f, 1.05f, MouthShape.Grin, 0.95f, 0.9f);
                case Expression.Determined: return new FaceParams(16f, -0.12f, 0f, 0.78f, 0.95f, MouthShape.Flat, 0.75f, 1f);
                case Expression.Strain: return new FaceParams(24f, -0.25f, 0f, 0.3f, 0.9f, MouthShape.Grit, 1.1f, 1.2f);
                case Expression.Scared: return new FaceParams(-22f, 0.38f, 0f, 1.22f, 0.6f, MouthShape.Wavy, 0.9f, 1.15f);
                case Expression.Surprised: return new FaceParams(-6f, 0.55f, 0f, 1.28f, 0.62f, MouthShape.O, 0.9f, 0.95f);
                case Expression.Sad: return new FaceParams(-24f, 0.08f, 0f, 0.82f, 1.05f, MouthShape.Frown, 0.75f, 0.8f, 0, 14f);
                case Expression.Angry: return new FaceParams(30f, -0.3f, 0f, 0.72f, 0.85f, MouthShape.Frown, 0.95f, 1f);
                case Expression.KO: return new FaceParams(-8f, -0.05f, 0f, 1f, 1f, MouthShape.Wavy, 0.8f, 0.8f, 1);
                case Expression.Dizzy: return new FaceParams(-10f, 0.15f, 0.35f, 1.08f, 1f, MouthShape.Wavy, 1f, 1.25f, 2);
                case Expression.Smug: return new FaceParams(8f, 0f, 1f, 0.58f, 1f, MouthShape.Smug, 1f, 1f);
                // ---- campaign (villain faces; interns use them too)
                case Expression.Laugh: return new FaceParams(-12f, 0.34f, 0f, 0.16f, 1f, MouthShape.Grin, 1.2f, 1.3f);
                case Expression.Shocked: return new FaceParams(-4f, 0.8f, 0f, 1.45f, 0.42f, MouthShape.O, 1.25f, 1.45f);
                case Expression.Pout: return new FaceParams(20f, 0.05f, -0.6f, 0.66f, 1f, MouthShape.Frown, 0.5f, 1.15f, 0, 4f);
                case Expression.Defeated: return new FaceParams(-28f, -0.08f, 0f, 0.5f, 1f, MouthShape.Wavy, 0.8f, 0.7f, 0, 20f);
                default: return new FaceParams(0f, 0f, 0f, 1f, 1f, MouthShape.Smile, 0.72f, 0.6f);
            }
        }

        public Transform SocketL, SocketR, EyeL, EyeR, PupilL, PupilR, BrowL, BrowR, Mouth;
        MeshFilter _pupilFL, _pupilFR, _mouthF;
        Renderer _glintL, _glintR;
        Mesh _pupilMesh, _xMesh, _spiralMesh;
        readonly Mesh[] _mouths = new Mesh[8];
        Transform _head;
        Vector3 _browLRest, _browRRest, _eyeCenter;
        float _eyeR;
        bool _visible = true;

        public Expression Expression { get; private set; } = Expression.Neutral;
        public MouthShape CurrentMouth => _mouthShape;
        public int EyeMode => _eyeMode;
        /// <summary>Keep the eyes open (portrait snapshots).</summary>
        public bool SuppressBlink;
        /// <summary>(campaign) Flap the mouth like speech (broadcasts, puppets, villain monologues).</summary>
        public bool Talking;
        float _talkTimer; bool _talkOpen;

        float _browAngle, _browH, _asym, _eyeOpen = 1f, _pupil = 1f, _lookDown;
        Vector2 _mouthScale = new Vector2(0.72f, 0.6f);
        float _mouthPop = 1f, _mouthPopV;
        int _eyeMode;
        MouthShape _mouthShape = MouthShape.Smile;
        float _blinkTimer, _blinkT = 1f; bool _doubleBlink;
        float _yaw, _pitch, _spin;
        readonly System.Random _rng;

        FaceController(int seed) { _rng = new System.Random(seed); _blinkTimer = 1f + (float)_rng.NextDouble() * 3f; }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ build
        /// <summary>Creates the face under head. c = head center (head-local), R = head half extents,
        /// surfaceZ(x,y) = front surface depth relative to the head center.</summary>
        public static FaceController Build(Transform head, Vector3 c, Vector3 R, Archetype a, float eyeR, CharacterMaterials m,
                                           int mouthStyle, System.Func<float, float, float> surfaceZ, int seed)
        {
            var f = new FaceController(seed) { _head = head, _eyeR = eyeR };
            float ex = a == Archetype.Brute ? R.x * 0.43f : a == Archetype.Brainiac ? R.x * 0.34f : R.x * 0.37f;
            float ey = a == Archetype.Brute ? R.y * 0.06f : R.y * 0.1f;
            float ez = surfaceZ(ex, ey) - eyeR * 0.42f;
            string k = VisualMeshes.F(eyeR);

            f._pupilMesh = VisualMeshes.Ellipsoid(new Vector3(eyeR * 0.56f, eyeR * 0.56f, eyeR * 0.24f), 12, 8);
            f._xMesh = ProcMesh.Cached("v_xeye_" + k, () =>
            {
                var b = new ProcMesh.Builder();
                float L = eyeR * 0.78f;
                VisualMeshes.AddStroke(b, new[] { new Vector2(-L, -L), new Vector2(L, L) }, eyeR * 0.3f, eyeR * 0.2f, eyeR * 1.15f, new Vector3(0f, 0f, eyeR * 0.04f));
                VisualMeshes.AddStroke(b, new[] { new Vector2(-L, L), new Vector2(L, -L) }, eyeR * 0.3f, eyeR * 0.2f, eyeR * 1.15f, new Vector3(0f, 0f, eyeR * 0.04f));
                return b.ToMesh("XEye");
            });
            f._spiralMesh = ProcMesh.Cached("v_spiral_" + k, () =>
            {
                var b = new ProcMesh.Builder();
                var pts = new Vector2[44];
                float turns = 2.6f;
                for (int i = 0; i < pts.Length; i++)
                {
                    float t = i / (float)(pts.Length - 1);
                    float ang = t * turns * Mathf.PI * 2f;
                    float rr = eyeR * (0.05f + 0.68f * t);
                    pts[i] = new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr);
                }
                VisualMeshes.AddStroke(b, pts, eyeR * 0.14f, eyeR * 0.16f, eyeR * 1.15f, new Vector3(0f, 0f, eyeR * 0.02f));
                return b.ToMesh("SpiralEye");
            });

            f._eyeCenter = new Vector3(0f, c.y + ey, ez);
            f.MakeEye(head, new Vector3(c.x - ex, c.y + ey, c.z + ez), m, true);
            f.MakeEye(head, new Vector3(c.x + ex, c.y + ey, c.z + ez), m, false);

            // brows
            float bh = eyeR * (a == Archetype.Brute ? 0.42f : 0.3f), bw = eyeR * (a == Archetype.Brute ? 1.7f : 1.45f);
            var browMesh = VisualMeshes.Block(new Vector3(bw, bh, eyeR * 0.28f), bh * 0.35f);
            float by = ey + eyeR * (a == Archetype.Brute ? 1.3f : 1.45f);
            f._browLRest = new Vector3(c.x - ex, c.y + by, c.z + surfaceZ(ex, by) + eyeR * 0.06f);
            f._browRRest = new Vector3(c.x + ex, c.y + by, c.z + surfaceZ(ex, by) + eyeR * 0.06f);
            f.BrowL = ProcMesh.Part("BrowL", head, browMesh, m.Brow, f._browLRest, null, null, false).transform;
            f.BrowR = ProcMesh.Part("BrowR", head, browMesh, m.Brow, f._browRRest, null, null, false).transform;

            // mouth shapes
            float w = R.x * (a == Archetype.Brute ? 0.46f : 0.4f) * (mouthStyle == 1 ? 1.22f : mouthStyle == 2 ? 0.85f : 1f);
            float my = ey - eyeR * (a == Archetype.Brainiac ? 2.25f : a == Archetype.Brute ? 1.9f : 2.0f);
            float mx = mouthStyle == 2 ? R.x * 0.1f : 0f;
            float curv = a == Archetype.Brute ? R.x * 2.2f : R.x * 1.05f;
            float thick = eyeR * (a == Archetype.Brute ? 0.42f : 0.36f);
            for (int i = 0; i < f._mouths.Length; i++) f._mouths[i] = MouthMesh((MouthShape)i, w, thick, eyeR * 0.22f, curv);
            f.Mouth = ProcMesh.Part("Mouth", head, f._mouths[0], m.Mouth, new Vector3(c.x + mx, c.y + my, c.z + surfaceZ(mx, my) + eyeR * 0.02f), null, null, false).transform;
            f._mouthF = f.Mouth.GetComponent<MeshFilter>();
            return f;
        }

        void MakeEye(Transform head, Vector3 pos, CharacterMaterials m, bool left)
        {
            float r = _eyeR;
            var socket = new GameObject(left ? "EyeSocketL" : "EyeSocketR").transform;
            socket.SetParent(head, false); socket.localPosition = pos;
            var eye = ProcMesh.Part(left ? "EyeL" : "EyeR", socket, VisualMeshes.Sphere(r, 16, 12), m.EyeWhite, Vector3.zero, null, null, false).transform;
            var pupil = ProcMesh.Part(left ? "PupilL" : "PupilR", eye, _pupilMesh, m.Pupil, new Vector3(0f, 0f, r * 0.86f), null, null, false).transform;
            var glint = ProcMesh.Part("Glint", pupil, VisualMeshes.Sphere(r * 0.17f, 8, 6), m.Glint, new Vector3(r * 0.2f, r * 0.22f, r * 0.14f), null, null, false);
            if (left) { SocketL = socket; EyeL = eye; PupilL = pupil; _pupilFL = pupil.GetComponent<MeshFilter>(); _glintL = glint.GetComponent<Renderer>(); }
            else { SocketR = socket; EyeR = eye; PupilR = pupil; _pupilFR = pupil.GetComponent<MeshFilter>(); _glintR = glint.GetComponent<Renderer>(); }
        }

        static Mesh MouthMesh(MouthShape s, float w, float t, float depth, float curv)
        {
            string key = "v_mouth_" + s + "_" + VisualMeshes.F(w) + "_" + VisualMeshes.F(t) + "_" + VisualMeshes.F(curv);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                switch (s)
                {
                    case MouthShape.Smile: VisualMeshes.AddStroke(b, VisualMeshes.Arc(w * 1.1f, w * 0.72f, 212f, 328f, 10), t, depth, curv, Vector3.zero); break;
                    case MouthShape.Grin: VisualMeshes.AddFilled(b, VisualMeshes.Ellipse(w * 0.88f, w * 0.72f, w * 0.18f, 14, 180f, 360f, true), depth, curv, Vector3.zero); break;
                    case MouthShape.O: VisualMeshes.AddFilled(b, VisualMeshes.Ellipse(w * 0.36f, w * 0.46f, -w * 0.08f, 14), depth, curv, Vector3.zero); break;
                    case MouthShape.Flat: VisualMeshes.AddStroke(b, new[] { new Vector2(-w * 0.62f, 0f), new Vector2(0f, -w * 0.02f), new Vector2(w * 0.62f, 0f) }, t, depth, curv, Vector3.zero); break;
                    case MouthShape.Frown: VisualMeshes.AddStroke(b, VisualMeshes.Arc(w * 1.0f, -w * 0.95f, 52f, 128f, 8), t, depth, curv, Vector3.zero); break;
                    case MouthShape.Smug:
                        VisualMeshes.AddStroke(b, new[] { new Vector2(-w * 0.6f, -w * 0.02f), new Vector2(-w * 0.15f, -w * 0.06f), new Vector2(w * 0.25f, -w * 0.03f), new Vector2(w * 0.52f, w * 0.08f), new Vector2(w * 0.7f, w * 0.24f) }, t, depth, curv, Vector3.zero);
                        break;
                    case MouthShape.Wavy:
                    case MouthShape.Grit:
                        {
                            bool grit = s == MouthShape.Grit;
                            int n = grit ? 9 : 12;
                            var pts = new Vector2[n];
                            for (int i = 0; i < n; i++)
                            {
                                float u = i / (float)(n - 1);
                                float x = Mathf.Lerp(-w * (grit ? 0.85f : 0.72f), w * (grit ? 0.85f : 0.72f), u);
                                float y = grit ? ((i % 2 == 0) ? w * 0.09f : -w * 0.09f) : Mathf.Sin(u * Mathf.PI * 3f) * w * 0.12f;
                                pts[i] = new Vector2(x, y);
                            }
                            VisualMeshes.AddStroke(b, pts, t * (grit ? 0.9f : 1f), depth, curv, Vector3.zero);
                            if (grit) // clenched-teeth outline (top & bottom lips)
                            {
                                VisualMeshes.AddStroke(b, new[] { new Vector2(-w * 0.9f, w * 0.2f), new Vector2(w * 0.9f, w * 0.2f) }, t * 0.8f, depth, curv, Vector3.zero);
                                VisualMeshes.AddStroke(b, new[] { new Vector2(-w * 0.9f, -w * 0.2f), new Vector2(w * 0.9f, -w * 0.2f) }, t * 0.8f, depth, curv, Vector3.zero);
                            }
                            break;
                        }
                }
                return b.ToMesh("Mouth_" + s);
            });
        }

        // ------------------------------------------------------------------ runtime
        public void Set(Expression e) { Expression = e; }

        public void SetVisible(bool on) { _visible = on; RefreshRendererState(); }

        public void RefreshRendererState()
        {
            bool g = _visible && _eyeMode == 0;
            if (_glintL != null) _glintL.enabled = g;
            if (_glintR != null) _glintR.enabled = g;
        }

        /// <summary>Snap all smoothed values to the current expression (portraits, first frame).</summary>
        public void Snap() { ApplyParams(For(Expression), 1f); _mouthPop = 1f; _mouthPopV = 0f; _blinkT = 1f; }

        void ApplyParams(in FaceParams f, float k)
        {
            _browAngle = Mathf.Lerp(_browAngle, f.BrowAngle, k);
            _browH = Mathf.Lerp(_browH, f.BrowHeight, k);
            _asym = Mathf.Lerp(_asym, f.Asym, k);
            _eyeOpen = Mathf.Lerp(_eyeOpen, f.EyeOpen, k);
            _pupil = Mathf.Lerp(_pupil, f.Pupil, k);
            _lookDown = Mathf.Lerp(_lookDown, f.LookDown, k);
            _mouthScale = Vector2.Lerp(_mouthScale, f.MouthScale, k);
            if (f.Mouth != _mouthShape)
            {
                _mouthShape = f.Mouth;
                if (_mouthF != null) _mouthF.sharedMesh = _mouths[(int)f.Mouth];
                _mouthPop = 0.5f; _mouthPopV = 0f;
            }
            if (f.EyeMode != _eyeMode)
            {
                _eyeMode = f.EyeMode;
                var mesh = _eyeMode == 1 ? _xMesh : _eyeMode == 2 ? _spiralMesh : _pupilMesh;
                if (_pupilFL != null) _pupilFL.sharedMesh = mesh;
                if (_pupilFR != null) _pupilFR.sharedMesh = mesh;
                RefreshRendererState();
            }
        }

        /// <summary>Per-frame update. lookWorld = world point the pupils track (null = straight ahead).</summary>
        public void Tick(float dt, Vector3? lookWorld)
        {
            if (_head == null) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            var f = For(Expression);
            if (Talking && f.EyeMode == 0)
            {
                _talkTimer -= dt;
                if (_talkTimer <= 0f) { _talkOpen = !_talkOpen; _talkTimer = _talkOpen ? Rand(0.07f, 0.13f) : Rand(0.06f, 0.16f); }
                if (_talkOpen)
                {
                    f.Mouth = f.Mouth == MouthShape.O || f.Mouth == MouthShape.Grin ? MouthShape.Grin : MouthShape.O;
                    f.MouthScale = new Vector2(f.MouthScale.x * 0.9f, f.MouthScale.y * 0.85f);
                }
            }
            else _talkOpen = false;
            ApplyParams(f, 1f - Mathf.Exp(-16f * dt));

            // mouth pop spring
            AnimMath.Spring(ref _mouthPop, ref _mouthPopV, 1f, 320f, 13f, dt);
            _mouthPop = Mathf.Clamp(_mouthPop, 0.3f, 1.6f);
            if (Mouth != null) Mouth.localScale = new Vector3(_mouthScale.x * _mouthPop, _mouthScale.y * _mouthPop, 1f);

            // brows
            float hl = (_browH - 0.12f * _asym) * _eyeR, hr = (_browH + 0.32f * _asym) * _eyeR;
            if (BrowL != null) { BrowL.localPosition = _browLRest + new Vector3(0f, hl, 0f); BrowL.localRotation = Quaternion.Euler(0f, 0f, -(_browAngle + 14f * _asym)); }
            if (BrowR != null) { BrowR.localPosition = _browRRest + new Vector3(0f, hr, 0f); BrowR.localRotation = Quaternion.Euler(0f, 0f, _browAngle - 18f * _asym); }

            // blink
            float blink = 1f;
            if (_eyeMode == 0 && !SuppressBlink)
            {
                _blinkTimer -= dt;
                if (_blinkTimer <= 0f) { _blinkT = 0f; _doubleBlink = _rng.NextDouble() < 0.15; _blinkTimer = _doubleBlink ? 0.22f : Rand(2f, 5f); }
                if (_blinkT < 1f) { _blinkT = Mathf.Min(1f, _blinkT + dt / 0.13f); blink = 1f - Mathf.Sin(_blinkT * Mathf.PI) * 0.93f; }
            }
            else _blinkT = 1f;
            float open = _eyeMode == 0 ? Mathf.Max(0.06f, _eyeOpen * blink) : 1f;
            var sc = new Vector3(1f, open, 1f);
            if (SocketL != null) SocketL.localScale = sc;
            if (SocketR != null) SocketR.localScale = sc;

            // pupils: look / X / spiral
            if (_eyeMode == 0)
            {
                float ty = 0f, tp = -_lookDown;
                if (lookWorld.HasValue)
                {
                    var l = _head.InverseTransformPoint(lookWorld.Value) - _eyeCenter;
                    if (l.z < 0.05f) l.z = 0.05f;
                    ty = Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -30f, 30f);
                    tp = Mathf.Clamp(Mathf.Atan2(l.y, Mathf.Sqrt(l.x * l.x + l.z * l.z)) * Mathf.Rad2Deg - _lookDown, -22f, 22f);
                }
                float k = 1f - Mathf.Exp(-12f * dt);
                _yaw = Mathf.Lerp(_yaw, ty, k); _pitch = Mathf.Lerp(_pitch, tp, k);
                var rot = Quaternion.Euler(-_pitch, _yaw, 0f);
                if (EyeL != null) EyeL.localRotation = rot;
                if (EyeR != null) EyeR.localRotation = rot;
                var ps = Vector3.one * _pupil;
                if (PupilL != null) { PupilL.localScale = ps; PupilL.localRotation = Quaternion.identity; }
                if (PupilR != null) { PupilR.localScale = ps; PupilR.localRotation = Quaternion.identity; }
            }
            else
            {
                _yaw = _pitch = 0f;
                if (EyeL != null) EyeL.localRotation = Quaternion.identity;
                if (EyeR != null) EyeR.localRotation = Quaternion.identity;
                _spin += dt * 420f;
                var rl = _eyeMode == 2 ? Quaternion.Euler(0f, 0f, _spin) : Quaternion.identity;
                var rr = _eyeMode == 2 ? Quaternion.Euler(0f, 0f, -_spin) : Quaternion.identity;
                if (PupilL != null) { PupilL.localScale = Vector3.one; PupilL.localRotation = rl; }
                if (PupilR != null) { PupilR.localScale = Vector3.one; PupilR.localRotation = rr; }
            }
        }
    }
}
