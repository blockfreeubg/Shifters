// =====================================================================================
//  SHIFTERS - FxSystem.cs   (RENDERING)
//  Runtime backend of the static Fx API: one persistent [Fx] root (DontDestroyOnLoad) with shared
//  world-space ParticleSystems driven by Emit() (zero GameObjects per effect) plus small pools for
//  tracers (LineRenderer), floating text (TextMeshPro), KO stars, scan pulses, speed lines and
//  flash lights. Never exists in edit mode. No allocations per frame.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shifters
{
    public class FxSystem : MonoBehaviour
    {
        public static FxSystem Instance { get; private set; }
        static bool _failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; _failed = false; }

        /// <summary>Returns the live system, creating it on first use (play mode only). Null in edit mode.</summary>
        public static FxSystem Get()
        {
            if (Instance != null) return Instance;
            if (!Application.isPlaying || _failed) return null;
            try
            {
                var go = new GameObject("[Fx]");
                DontDestroyOnLoad(go);
                var fx = go.AddComponent<FxSystem>();
                fx.Init();
                Instance = fx;
            }
            catch (System.Exception e)
            {
                _failed = true;
                Debug.LogWarning("[Fx] init failed, effects disabled: " + e.Message);
            }
            return Instance;
        }

        // ------------------------------------------------------------------ shared particle systems
        internal ParticleSystem Dust, PuffPs, SparksPs, GlowPs, SparklePs, ConfettiPs, DropsPs, RipplePs, ShockPs, StreakPs;
        ParticleSystem.EmitParams _ep;
        Material _tracerMat, _pulseMat, _starMat;
        Mesh _quad, _pulseWall, _pulseRing, _starMesh;

        // ------------------------------------------------------------------ pools
        class TracerFx { public LineRenderer Lr; public float Age, Life, Width; public Color Color; }
        class TextFx { public TextMeshPro Tmp; public Transform T; public Vector3 Start, Drift; public float Age, Life, Scale; }
        class StarsFx { public GameObject Root; public Transform[] Stars; public Transform Follow; public float End, OffsetY, Phase; }
        class PulseFx { public Transform T; public MeshRenderer Wall, Ring; public MaterialPropertyBlock Mpb; public float Age, Life, Radius; public Color Color; }
        class SpeedFx { public Transform Follow; public Rigidbody Body; public float End, Acc; public Vector3 LastPos; }
        class LightFx { public Light L; public float Age, Life, Peak; }

        readonly List<TracerFx> _tracers = new List<TracerFx>(); int _tracerActive;
        readonly List<TextFx> _texts = new List<TextFx>(); int _textActive;
        readonly List<StarsFx> _stars = new List<StarsFx>(); int _starsActive;
        readonly List<PulseFx> _pulses = new List<PulseFx>(); int _pulseActive;
        readonly List<SpeedFx> _speed = new List<SpeedFx>(); int _speedActive;
        readonly List<LightFx> _lights = new List<LightFx>(); int _lightCursor;
        Material _textMat;

        const int MaxTracers = 48, MaxTexts = 32, MaxStars = 12, MaxPulses = 6, MaxSpeed = 8, MaxLights = 3;

        static readonly Color32[] ConfettiColors =
        {
            new Color32(255, 138, 31, 255), new Color32(255, 201, 60, 255), new Color32(46, 196, 182, 255), new Color32(176, 124, 255, 255),
            new Color32(61, 224, 255, 255), new Color32(255, 77, 94, 255), new Color32(61, 220, 151, 255), new Color32(255, 255, 255, 255)
        };

        void Init()
        {
            var shader = MaterialLibrary.ParticleShader;
            Material Mat(string n, Texture tex, bool additive, float intensity = 1f)
            {
                var m = new Material(shader) { name = n, hideFlags = HideFlags.DontSave };
                m.SetTexture(ShaderIds.BaseMap, tex);
                if (m.HasProperty(ShaderIds.SrcBlend))
                {
                    m.SetFloat(ShaderIds.SrcBlend, (float)BlendMode.SrcAlpha);
                    m.SetFloat(ShaderIds.DstBlend, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                }
                if (m.HasProperty(ShaderIds.Intensity)) m.SetFloat(ShaderIds.Intensity, intensity);
                m.renderQueue = (int)RenderQueue.Transparent;
                return m;
            }

            var softAlpha = Mat("fx_dust", FxTextures.SoftCircle, false);
            var puffAlpha = Mat("fx_puff", FxTextures.Puff, false);
            var sparkAdd = Mat("fx_spark", FxTextures.SoftCircle, true, 2.5f);
            var glowAdd = Mat("fx_glow", FxTextures.Glow, true, 2f);
            var starAdd = Mat("fx_sparkle", FxTextures.Star, true, 2.2f);
            var squareAlpha = Mat("fx_confetti", FxTextures.Square, false);
            var dropAlpha = Mat("fx_drop", FxTextures.SoftCircle, false, 1.1f);
            var ringAlpha = Mat("fx_ripple", FxTextures.Ring, false);
            var ringAdd = Mat("fx_shock", FxTextures.Ring, true, 2f);
            var streakAdd = Mat("fx_streak", FxTextures.SoftCircle, true, 1.4f);
            _tracerMat = Mat("fx_tracer", FxTextures.Streak, true, 2.6f);
            _pulseMat = Mat("fx_pulse", FxTextures.Gradient, true, 1.25f);
            _quad = BuildQuad();

            Dust = MakeSystem("Dust", softAlpha, ParticleSystemRenderMode.Billboard, -0.04f, 600, Curve(0f, 0.45f, 1f, 1.5f), Fade(0.7f), 1.2f, true);
            PuffPs = MakeSystem("Puff", puffAlpha, ParticleSystemRenderMode.Billboard, -0.02f, 500,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.18f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f)), Fade(0.85f), 4.5f, true);
            SparksPs = MakeSystem("Sparks", sparkAdd, ParticleSystemRenderMode.Stretch, 1.1f, 800, Curve(0f, 1f, 1f, 0.15f), Fade(0.5f), 0.8f, false);
            GlowPs = MakeSystem("Glow", glowAdd, ParticleSystemRenderMode.Billboard, 0f, 120, Curve(0f, 0.7f, 1f, 1.25f), Fade(0f), 0f, false);
            SparklePs = MakeSystem("Sparkle", starAdd, ParticleSystemRenderMode.Billboard, 0f, 200,
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)), Fade(0.6f), 2f, true);
            ConfettiPs = MakeSystem("Confetti", squareAlpha, ParticleSystemRenderMode.Mesh, 0.32f, 900, Curve(0f, 1f, 1f, 1f), Fade(0.8f), 2.6f, false);
            DropsPs = MakeSystem("Drops", dropAlpha, ParticleSystemRenderMode.Stretch, 1.4f, 400, Curve(0f, 1f, 1f, 0.5f), Fade(0.7f), 0.3f, false);
            RipplePs = MakeSystem("Ripple", ringAlpha, ParticleSystemRenderMode.HorizontalBillboard, 0f, 60, Curve(0f, 0.25f, 1f, 1f), Fade(0f), 0f, false);
            ShockPs = MakeSystem("Shock", ringAdd, ParticleSystemRenderMode.HorizontalBillboard, 0f, 30, Curve(0f, 0.2f, 1f, 1f), Fade(0f), 0f, false);
            StreakPs = MakeSystem("Streaks", streakAdd, ParticleSystemRenderMode.Stretch, 0f, 400, Curve(0f, 1f, 1f, 0.6f), Fade(0.3f), 0f, false);

            var sr = SparksPs.GetComponent<ParticleSystemRenderer>(); sr.velocityScale = 0.045f; sr.lengthScale = 1.2f;
            var dr = DropsPs.GetComponent<ParticleSystemRenderer>(); dr.velocityScale = 0.03f; dr.lengthScale = 1.1f;
            var tr = StreakPs.GetComponent<ParticleSystemRenderer>(); tr.velocityScale = 0.09f; tr.lengthScale = 1f;

            // confetti: flipping 3D quads with noise flutter
            var cr = ConfettiPs.GetComponent<ParticleSystemRenderer>(); cr.mesh = _quad; cr.alignment = ParticleSystemRenderSpace.World;
            var cm = ConfettiPs.main; cm.startRotation3D = true;
            var rol = ConfettiPs.rotationOverLifetime; rol.enabled = true; rol.separateAxes = true;
            rol.x = new ParticleSystem.MinMaxCurve(-9f, 9f); rol.y = new ParticleSystem.MinMaxCurve(-6f, 6f); rol.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            var noise = ConfettiPs.noise; noise.enabled = true; noise.strength = 1.2f; noise.frequency = 0.6f; noise.scrollSpeed = 0.4f; noise.quality = ParticleSystemNoiseQuality.Low;

            // pulse meshes
            _pulseWall = BuildPulseWall(48);
            _pulseRing = BuildPulseRing(48, 0.93f);

            // stars
            _starMesh = BuildStar(0.13f, 0.06f, 0.05f);
            _starMat = MaterialLibrary.Colored(UIColors.Hex(0xFFD84A), 0.4f, 1.6f);

            for (int i = 0; i < MaxLights; i++)
            {
                var lgo = new GameObject("FlashLight" + i);
                lgo.transform.SetParent(transform, false);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.shadows = LightShadows.None; l.range = 5f; l.intensity = 0f; l.enabled = false;
                _lights.Add(new LightFx { L = l, Age = 1f, Life = 0.1f });
            }
        }

        // ------------------------------------------------------------------ builders
        ParticleSystem MakeSystem(string n, Material mat, ParticleSystemRenderMode mode, float gravity, int max, AnimationCurve size, Gradient fade, float drag, bool spin)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 1f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(fade);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, size);
            if (drag > 0f)
            {
                var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 10000f; lv.dampen = 0f; lv.drag = drag; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;
            }
            if (spin) { var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-3f, 3f); }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.minParticleSize = 0f;
            r.maxParticleSize = 4f;
            ps.Play();
            return ps;
        }

        static AnimationCurve Curve(float t0, float v0, float t1, float v1) => AnimationCurve.EaseInOut(t0, v0, t1, v1);

        /// <summary>Alpha 1 until 'hold' (0..1 of life) then fades to 0.</summary>
        static Gradient Fade(float hold)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                hold <= 0f ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                           : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Clamp(hold, 0.01f, 0.98f)), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Mesh BuildQuad()
        {
            var m = new Mesh { name = "fx_quad", hideFlags = HideFlags.DontSave };
            m.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            m.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildPulseWall(int seg)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d); uv.Add(new Vector2(i / (float)seg, 0.02f));
                v.Add(d + Vector3.up); uv.Add(new Vector2(i / (float)seg, 1f));
            }
            for (int i = 0; i < seg; i++) { int b = i * 2; t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b + 1); t.Add(b + 3); t.Add(b + 2); }
            var m = new Mesh { name = "fx_pulse_wall", hideFlags = HideFlags.DontSave };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh BuildPulseRing(int seg, float inner)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * inner + Vector3.up * 0.03f); uv.Add(new Vector2(i / (float)seg, 0.35f));
                v.Add(d + Vector3.up * 0.03f); uv.Add(new Vector2(i / (float)seg, 0.04f));
            }
            for (int i = 0; i < seg; i++) { int b = i * 2; t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b + 1); t.Add(b + 2); t.Add(b + 3); }
            var m = new Mesh { name = "fx_pulse_ring", hideFlags = HideFlags.DontSave };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Extruded 5-point star (XY plane, thickness along Z).</summary>
        public static Mesh BuildStar(float outer, float inner, float thickness)
        {
            var b = new ProcMesh.Builder();
            var c = Color.white;
            var front = new Vector3(0, 0, -thickness * 0.5f);
            var back = new Vector3(0, 0, thickness * 0.5f);
            for (int i = 0; i < 10; i++)
            {
                float a0 = Mathf.PI * 0.5f + i * Mathf.PI / 5f, a1 = Mathf.PI * 0.5f + (i + 1) * Mathf.PI / 5f;
                float r0 = i % 2 == 0 ? outer : inner, r1 = i % 2 == 0 ? inner : outer;
                var p0 = new Vector3(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0, 0f);
                var p1 = new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1, 0f);
                b.Tri(front, p0 + front, p1 + front, c, Vector3.back);
                b.Tri(back, p0 + back, p1 + back, c, Vector3.forward);
                b.Quad(p0 + front, p1 + front, p1 + back, p0 + back, c, (p0 + p1).normalized);
            }
            var m = b.ToMesh("fx_star");
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        // ------------------------------------------------------------------ emit helpers
        internal void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color, float rotDeg = 0f)
        {
            if (ps == null) return;
            _ep.position = pos;
            _ep.velocity = vel;
            _ep.startSize = size;
            _ep.startLifetime = life;
            _ep.startColor = color;
            _ep.rotation = rotDeg;
            ps.Emit(_ep, 1);
        }

        internal void EmitConfetti(Vector3 pos, Vector3 vel, float size, float life, Color color)
        {
            if (ConfettiPs == null) return;
            var ep = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startSize = size, startLifetime = life, startColor = color,
                rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f)
            };
            ConfettiPs.Emit(ep, 1);
        }

        internal static Color32 RandomConfettiColor() => ConfettiColors[Random.Range(0, ConfettiColors.Length)];

        internal void FlashLight(Vector3 pos, Color color, float intensity, float range, float life)
        {
            if (_lights.Count == 0) return;
            var lf = _lights[_lightCursor];
            _lightCursor = (_lightCursor + 1) % _lights.Count;
            lf.L.transform.position = pos;
            lf.L.color = color;
            lf.L.range = range;
            lf.Peak = intensity;
            lf.L.intensity = intensity;
            lf.L.enabled = true;
            lf.Age = 0f;
            lf.Life = Mathf.Max(0.02f, life);
        }

        // ------------------------------------------------------------------ pooled effects
        internal void Tracer(Vector3 from, Vector3 to, Color color, float width, float duration)
        {
            int idx;
            if (_tracerActive < _tracers.Count) idx = _tracerActive++;
            else if (_tracers.Count < MaxTracers) { _tracers.Add(CreateTracer()); idx = _tracerActive++; }
            else idx = 0;
            var t = _tracers[idx];
            t.Lr.gameObject.SetActive(true);
            t.Lr.SetPosition(0, from);
            t.Lr.SetPosition(1, to);
            t.Age = 0f; t.Life = Mathf.Max(0.02f, duration); t.Width = Mathf.Max(0.005f, width); t.Color = color;
            UpdateTracer(t, 0f);
        }

        TracerFx CreateTracer()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.useWorldSpace = true; lr.textureMode = LineTextureMode.Stretch; lr.numCapVertices = 0;
            lr.alignment = LineAlignment.View; lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off; lr.reflectionProbeUsage = ReflectionProbeUsage.Off; lr.sharedMaterial = _tracerMat;
            return new TracerFx { Lr = lr };
        }

        void UpdateTracer(TracerFx t, float p)
        {
            float a = 1f - p * p;
            var c = t.Color; c.a = Mathf.Clamp01(t.Color.a) * a;
            t.Lr.startColor = c; t.Lr.endColor = c;
            t.Lr.widthMultiplier = t.Width * (1f - 0.6f * p);
        }

        internal void FloatingText(Vector3 pos, string text, Color color, float scale)
        {
            int idx;
            if (_textActive < _texts.Count) idx = _textActive++;
            else if (_texts.Count < MaxTexts) { _texts.Add(CreateText()); idx = _textActive++; }
            else idx = 0;
            var f = _texts[idx];
            f.T.gameObject.SetActive(true);
            f.Tmp.text = text;
            f.Tmp.color = color;
            f.Start = pos;
            f.Drift = new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
            f.Age = 0f; f.Life = 1.15f; f.Scale = Mathf.Max(0.2f, scale);
            UpdateText(f, 0f, Camera.main);
        }

        TextFx CreateText()
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            var font = ShiftersFonts.Display;
            if (font != null) tmp.font = font;
            if (_textMat == null && tmp.font != null)
            {
                _textMat = new Material(tmp.font.material) { name = "fx_text_outline", hideFlags = HideFlags.DontSave };
                _textMat.EnableKeyword("OUTLINE_ON");
                if (_textMat.HasProperty(ShaderUtilities.ID_OutlineWidth)) _textMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                if (_textMat.HasProperty(ShaderUtilities.ID_OutlineColor)) _textMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.06f, 0.09f, 1f));
                if (_textMat.HasProperty(ShaderUtilities.ID_FaceDilate)) _textMat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            }
            if (_textMat != null) tmp.fontSharedMaterial = _textMat;
            tmp.fontSize = 4.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.renderer.shadowCastingMode = ShadowCastingMode.Off;
            tmp.renderer.receiveShadows = false;
            return new TextFx { Tmp = tmp, T = go.transform };
        }

        void UpdateText(TextFx f, float p, Camera cam)
        {
            float rise = 1f - (1f - p) * (1f - p);
            f.T.position = f.Start + Vector3.up * (1.25f * rise) + f.Drift * p;
            float s = p < 0.1f ? Mathf.Lerp(0.3f, 1.3f, p / 0.1f) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((p - 0.1f) / 0.15f));
            f.T.localScale = Vector3.one * (s * f.Scale);
            if (cam != null) f.T.rotation = cam.transform.rotation;
            f.Tmp.alpha = p > 0.6f ? Mathf.Clamp01(1f - (p - 0.6f) / 0.4f) : 1f;
        }

        internal void Stars(Transform follow, float duration)
        {
            for (int i = 0; i < _starsActive; i++)
                if (_stars[i].Follow == follow) { _stars[i].End = Mathf.Max(_stars[i].End, Time.time + duration); return; }
            StarsFx s;
            if (_starsActive < _stars.Count) s = _stars[_starsActive];
            else if (_stars.Count < MaxStars) { s = CreateStars(); _stars.Add(s); }
            else return;
            _starsActive++;
            s.Follow = follow;
            s.End = Time.time + duration;
            s.Phase = Random.value * 6.28f;
            s.OffsetY = HeadOffset(follow);
            s.Root.SetActive(true);
        }

        StarsFx CreateStars()
        {
            var root = new GameObject("KOStars");
            root.transform.SetParent(transform, false);
            var s = new StarsFx { Root = root, Stars = new Transform[4] };
            for (int i = 0; i < s.Stars.Length; i++)
                s.Stars[i] = ProcMesh.Part("Star" + i, root.transform, _starMesh, _starMat, Vector3.zero, null, null, false).transform;
            return s;
        }

        static float HeadOffset(Transform follow)
        {
            var rs = follow.GetComponentsInChildren<Renderer>();
            float top = float.MinValue;
            for (int i = 0; i < rs.Length; i++) if (rs[i].enabled && !(rs[i] is ParticleSystemRenderer)) top = Mathf.Max(top, rs[i].bounds.max.y);
            if (top == float.MinValue) return 0.35f;
            return Mathf.Clamp(top - follow.position.y + 0.18f, 0.25f, 3.5f);
        }

        internal void ScanPulse(Vector3 origin, Color color, float radius, float duration)
        {
            int idx;
            if (_pulseActive < _pulses.Count) idx = _pulseActive++;
            else if (_pulses.Count < MaxPulses) { _pulses.Add(CreatePulse()); idx = _pulseActive++; }
            else idx = 0;
            var p = _pulses[idx];
            p.T.gameObject.SetActive(true);
            p.T.position = origin;
            p.Age = 0f; p.Life = Mathf.Max(0.1f, duration); p.Radius = Mathf.Max(1f, radius); p.Color = color;
            UpdatePulse(p, 0f);
        }

        PulseFx CreatePulse()
        {
            var go = new GameObject("ScanPulse");
            go.transform.SetParent(transform, false);
            var wall = ProcMesh.Part("Wall", go.transform, _pulseWall, _pulseMat, Vector3.zero, null, null, false).GetComponent<MeshRenderer>();
            var ring = ProcMesh.Part("Ring", go.transform, _pulseRing, _pulseMat, Vector3.zero, null, null, false).GetComponent<MeshRenderer>();
            return new PulseFx { T = go.transform, Wall = wall, Ring = ring, Mpb = new MaterialPropertyBlock() };
        }

        void UpdatePulse(PulseFx p, float t)
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            float r = Mathf.Max(0.3f, p.Radius * e);
            float h = Mathf.Lerp(1.6f, 0.4f, t);
            p.Wall.transform.localScale = new Vector3(r, h, r);
            p.Ring.transform.localScale = new Vector3(r, 1f, r);
            float a = Mathf.Pow(1f - t, 1.4f) * Mathf.Clamp01(t / 0.06f + 0.2f);
            var c = p.Color; c.a = Mathf.Clamp01(p.Color.a) * a;
            p.Mpb.SetColor(ShaderIds.BaseColor, c);
            p.Wall.SetPropertyBlock(p.Mpb);
            p.Ring.SetPropertyBlock(p.Mpb);
        }

        internal void SpeedLines(Transform follow, float duration)
        {
            for (int i = 0; i < _speedActive; i++)
                if (_speed[i].Follow == follow) { _speed[i].End = Mathf.Max(_speed[i].End, Time.time + duration); return; }
            SpeedFx s;
            if (_speedActive < _speed.Count) s = _speed[_speedActive];
            else if (_speed.Count < MaxSpeed) { s = new SpeedFx(); _speed.Add(s); }
            else return;
            _speedActive++;
            s.Follow = follow; s.End = Time.time + duration; s.Acc = 0f; s.LastPos = follow.position;
            s.Body = follow.GetComponentInParent<Rigidbody>();
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            float now = Time.time;
            var cam = Camera.main;

            for (int i = _tracerActive - 1; i >= 0; i--)
            {
                var t = _tracers[i];
                t.Age += dt;
                float p = t.Age / t.Life;
                if (p >= 1f) { t.Lr.gameObject.SetActive(false); _tracerActive--; Swap(_tracers, i, _tracerActive); continue; }
                UpdateTracer(t, p);
            }
            for (int i = _textActive - 1; i >= 0; i--)
            {
                var f = _texts[i];
                f.Age += dt;
                float p = f.Age / f.Life;
                if (p >= 1f) { f.T.gameObject.SetActive(false); _textActive--; Swap(_texts, i, _textActive); continue; }
                UpdateText(f, p, cam);
            }
            for (int i = _starsActive - 1; i >= 0; i--)
            {
                var s = _stars[i];
                if (s.Follow == null || !s.Follow.gameObject.activeInHierarchy || now >= s.End)
                {
                    s.Root.SetActive(false); s.Follow = null; _starsActive--; Swap(_stars, i, _starsActive); continue;
                }
                var c = s.Follow.position + Vector3.up * s.OffsetY;
                int n = s.Stars.Length;
                for (int k = 0; k < n; k++)
                {
                    float a = now * 4.2f + s.Phase + k * Mathf.PI * 2f / n;
                    var st = s.Stars[k];
                    st.position = c + new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a * 2f) * 0.06f, Mathf.Sin(a) * 0.42f);
                    st.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, Mathf.Sin(now * 9f + k) * 25f);
                }
            }
            for (int i = _pulseActive - 1; i >= 0; i--)
            {
                var p = _pulses[i];
                p.Age += dt;
                float t = p.Age / p.Life;
                if (t >= 1f) { p.T.gameObject.SetActive(false); _pulseActive--; Swap(_pulses, i, _pulseActive); continue; }
                UpdatePulse(p, t);
            }
            for (int i = _speedActive - 1; i >= 0; i--)
            {
                var s = _speed[i];
                if (s.Follow == null || !s.Follow.gameObject.activeInHierarchy || now >= s.End)
                {
                    s.Follow = null; s.Body = null; _speedActive--; Swap(_speed, i, _speedActive); continue;
                }
                EmitSpeedLines(s, dt);
            }
            for (int i = 0; i < _lights.Count; i++)
            {
                var l = _lights[i];
                if (!l.L.enabled) continue;
                l.Age += dt;
                float p = l.Age / l.Life;
                if (p >= 1f) { l.L.enabled = false; continue; }
                l.L.intensity = l.Peak * (1f - p) * (1f - p);
            }
        }

        void EmitSpeedLines(SpeedFx s, float dt)
        {
            var pos = s.Follow.position;
            Vector3 vel = s.Body != null ? s.Body.linearVelocity : (dt > 0f ? (pos - s.LastPos) / dt : Vector3.zero);
            s.LastPos = pos;
            vel.y *= 0.3f;
            var dir = vel.sqrMagnitude > 0.5f ? vel.normalized : s.Follow.forward;
            s.Acc += dt * 55f;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            while (s.Acc >= 1f)
            {
                s.Acc -= 1f;
                float ang = Random.value * Mathf.PI * 2f;
                var off = side * (Mathf.Cos(ang) * Random.Range(0.45f, 0.85f)) + Vector3.up * (0.9f + Mathf.Sin(ang) * Random.Range(0.4f, 0.75f));
                var p = pos + off + dir * Random.Range(-0.2f, 0.6f);
                Emit(StreakPs, p, -dir * Random.Range(7f, 11f) + vel * 0.6f, Random.Range(0.035f, 0.06f), Random.Range(0.12f, 0.2f), new Color(1f, 1f, 1f, 0.55f));
            }
        }

        static void Swap<T>(List<T> list, int a, int b)
        {
            if (a == b || a < 0 || b < 0) return;
            var tmp = list[a]; list[a] = list[b]; list[b] = tmp;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Active pooled counts (tests / debug HUD).</summary>
        public int ActiveTracers => _tracerActive;
        public int ActiveTexts => _textActive;
        public int ActiveStars => _starsActive;
        public int ActivePulses => _pulseActive;
        public int ActiveSpeedLines => _speedActive;
        public int ParticleCount
        {
            get
            {
                int n = 0;
                if (Dust != null) n += Dust.particleCount + PuffPs.particleCount + SparksPs.particleCount + GlowPs.particleCount + SparklePs.particleCount
                                     + ConfettiPs.particleCount + DropsPs.particleCount + RipplePs.particleCount + ShockPs.particleCount + StreakPs.particleCount;
                return n;
            }
        }
    }
}
