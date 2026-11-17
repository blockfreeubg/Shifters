// =====================================================================================
//  SHIFTERS - UITween.cs   (UI agent)
//  Minimal allocation-free tween engine for UI: one runner component, pooled tween
//  records, unscaled time (works while paused / in switcher slow-mo).
//  UITween.Scale/Move/Rotate/Fade/Color/Value/Shake/Punch/Delay, UITween.Kill(target).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public enum Ease { Linear, OutQuad, OutCubic, InCubic, InOutSine, InOutCubic, OutBack, InBack, OutElastic, OutBounce }

    public static class Easing
    {
        public static float Evaluate(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.InCubic: return t * t * t;
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
                case Ease.InBack: { const float c1 = 1.70158f, c3 = c1 + 1f; return c3 * t * t * t - c1 * t * t; }
                case Ease.OutElastic:
                    if (t <= 0f || t >= 1f) return t;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
                case Ease.OutBounce:
                {
                    const float n1 = 7.5625f, d1 = 2.75f;
                    if (t < 1f / d1) return n1 * t * t;
                    if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                    if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                    t -= 2.625f / d1; return n1 * t * t + 0.984375f;
                }
                default: return t;
            }
        }
    }

    public static class UITween
    {
        internal enum Kind { Scale, Move, Rotate, Fade, Color, Value, Shake, Punch, Delay }

        internal sealed class Tween
        {
            public Kind Kind;
            public UnityEngine.Object Target;
            public RectTransform Rt;
            public CanvasGroup Cg;
            public Graphic G;
            public Vector3 From, To;
            public Color CFrom, CTo;
            public float Duration, Delay, Elapsed, Amplitude;
            public Ease Ease;
            public Action<float> OnValue;
            public Action OnDone;
            public bool FromSet;
        }

        static readonly List<Tween> _active = new List<Tween>(128);
        static readonly Stack<Tween> _pool = new Stack<Tween>(128);
        static UITweenRunner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _active.Clear(); _pool.Clear(); _runner = null; }

        public static int ActiveCount => _active.Count;

        static Tween Start(Kind k, UnityEngine.Object target, float duration, float delay, Ease ease, Action onDone)
        {
            EnsureRunner();
            if (target != null && k != Kind.Delay && k != Kind.Value) KillKind(target, k);
            var t = _pool.Count > 0 ? _pool.Pop() : new Tween();
            t.Kind = k; t.Target = target; t.Duration = Mathf.Max(0.0001f, duration); t.Delay = delay; t.Elapsed = 0f;
            t.Ease = ease; t.OnDone = onDone; t.OnValue = null; t.Rt = null; t.Cg = null; t.G = null; t.FromSet = false; t.Amplitude = 0f;
            _active.Add(t);
            return t;
        }

        static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("[UITween]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<UITweenRunner>();
        }

        /// <summary>Tween localScale.</summary>
        public static void Scale(RectTransform rt, Vector3 to, float duration, Ease ease = Ease.OutBack, float delay = 0f, Vector3? from = null, Action onDone = null)
        {
            if (rt == null) return;
            var t = Start(Kind.Scale, rt, duration, delay, ease, onDone);
            t.Rt = rt; t.To = to;
            if (from.HasValue) { t.From = from.Value; t.FromSet = true; if (delay <= 0f) rt.localScale = from.Value; }
        }

        public static void Scale(RectTransform rt, float to, float duration, Ease ease = Ease.OutBack, float delay = 0f, float? from = null, Action onDone = null)
            => Scale(rt, Vector3.one * to, duration, ease, delay, from.HasValue ? Vector3.one * from.Value : (Vector3?)null, onDone);

        /// <summary>Tween anchoredPosition.</summary>
        public static void Move(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, float delay = 0f, Vector2? from = null, Action onDone = null)
        {
            if (rt == null) return;
            var t = Start(Kind.Move, rt, duration, delay, ease, onDone);
            t.Rt = rt; t.To = to;
            if (from.HasValue) { t.From = from.Value; t.FromSet = true; if (delay <= 0f) rt.anchoredPosition = from.Value; }
        }

        /// <summary>Tween local Z rotation (degrees).</summary>
        public static void Rotate(RectTransform rt, float toZ, float duration, Ease ease = Ease.OutCubic, float delay = 0f, float? fromZ = null, Action onDone = null)
        {
            if (rt == null) return;
            var t = Start(Kind.Rotate, rt, duration, delay, ease, onDone);
            t.Rt = rt; t.To = new Vector3(0f, 0f, toZ);
            if (fromZ.HasValue) { t.From = new Vector3(0f, 0f, fromZ.Value); t.FromSet = true; if (delay <= 0f) rt.localEulerAngles = t.From; }
        }

        public static void Fade(CanvasGroup cg, float to, float duration, Ease ease = Ease.OutQuad, float delay = 0f, float? from = null, Action onDone = null)
        {
            if (cg == null) return;
            var t = Start(Kind.Fade, cg, duration, delay, ease, onDone);
            t.Cg = cg; t.To = new Vector3(to, 0f, 0f);
            if (from.HasValue) { t.From = new Vector3(from.Value, 0f, 0f); t.FromSet = true; if (delay <= 0f) cg.alpha = from.Value; }
        }

        public static void Color(Graphic g, Color to, float duration, Ease ease = Ease.OutQuad, float delay = 0f, Color? from = null, Action onDone = null)
        {
            if (g == null) return;
            var t = Start(Kind.Color, g, duration, delay, ease, onDone);
            t.G = g; t.CTo = to;
            if (from.HasValue) { t.CFrom = from.Value; t.FromSet = true; if (delay <= 0f) g.color = from.Value; }
        }

        /// <summary>Generic 0..1 driven value tween. owner (optional) lets Kill(owner) stop it.</summary>
        public static void Value(float duration, Action<float> onValue, Ease ease = Ease.Linear, float delay = 0f, Action onDone = null, UnityEngine.Object owner = null)
        {
            var t = Start(Kind.Value, owner, duration, delay, ease, onDone);
            t.OnValue = onValue;
        }

        /// <summary>Positional shake around the current anchoredPosition (restored at the end).</summary>
        public static void Shake(RectTransform rt, float amplitude = 14f, float duration = 0.35f)
        {
            if (rt == null) return;
            var prev = Find(rt, Kind.Shake);
            Vector3 basePos = prev != null ? prev.From : (Vector3)rt.anchoredPosition;
            var t = Start(Kind.Shake, rt, duration, 0f, Ease.Linear, null);
            t.Rt = rt; t.From = basePos; t.Amplitude = amplitude; t.FromSet = true;
        }

        /// <summary>Scale punch: briefly grows by amount then settles back to the current scale.</summary>
        public static void Punch(RectTransform rt, float amount = 0.12f, float duration = 0.3f)
        {
            if (rt == null) return;
            var prev = Find(rt, Kind.Punch);
            Vector3 baseScale = prev != null ? prev.From : rt.localScale;
            var t = Start(Kind.Punch, rt, duration, 0f, Ease.Linear, null);
            t.Rt = rt; t.From = baseScale; t.Amplitude = amount; t.FromSet = true;
        }

        public static void Delay(float seconds, Action action, UnityEngine.Object owner = null)
        {
            Start(Kind.Delay, owner, seconds, 0f, Ease.Linear, action);
        }

        /// <summary>Stop every tween on target (no completion callbacks). Shakes/punches snap back.</summary>
        public static void Kill(UnityEngine.Object target)
        {
            if (target == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i].Target == target) Recycle(i, true);
        }

        static void KillKind(UnityEngine.Object target, Kind k)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                if (t.Target == target && t.Kind == k) Recycle(i, k != Kind.Shake && k != Kind.Punch);
            }
        }

        static Tween Find(UnityEngine.Object target, Kind k)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].Target == target && _active[i].Kind == k) return _active[i];
            return null;
        }

        static void Recycle(int index, bool restore)
        {
            var t = _active[index];
            if (restore && t.Rt != null)
            {
                if (t.Kind == Kind.Shake) t.Rt.anchoredPosition = t.From;
                else if (t.Kind == Kind.Punch) t.Rt.localScale = t.From;
            }
            _active.RemoveAt(index);
            t.Target = null; t.Rt = null; t.Cg = null; t.G = null; t.OnValue = null; t.OnDone = null;
            _pool.Push(t);
        }

        internal static void Tick(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (i >= _active.Count) continue;
                var t = _active[i];
                // destroyed target => drop silently
                if (!ReferenceEquals(t.Target, null) && t.Target == null) { Recycle(i, false); continue; }
                if (t.Delay > 0f) { t.Delay -= dt; if (t.Delay > 0f) continue; }
                if (!t.FromSet) CaptureFrom(t);
                t.Elapsed += dt;
                float n = Mathf.Clamp01(t.Elapsed / t.Duration);
                float e = Easing.Evaluate(t.Ease, n);
                Apply(t, n, e);
                if (n >= 1f)
                {
                    var done = t.OnDone;
                    if (t.Kind == Kind.Shake || t.Kind == Kind.Punch) Recycle(i, true); else Recycle(i, false);
                    if (done != null)
                    {
                        try { done(); } catch (Exception ex) { Debug.LogException(ex); }
                    }
                }
            }
        }

        static void CaptureFrom(Tween t)
        {
            t.FromSet = true;
            switch (t.Kind)
            {
                case Kind.Scale: t.From = t.Rt.localScale; break;
                case Kind.Move: t.From = t.Rt.anchoredPosition; break;
                case Kind.Rotate: t.From = new Vector3(0f, 0f, t.Rt.localEulerAngles.z); break;
                case Kind.Fade: t.From = new Vector3(t.Cg.alpha, 0f, 0f); break;
                case Kind.Color: t.CFrom = t.G.color; break;
            }
        }

        static void Apply(Tween t, float n, float e)
        {
            switch (t.Kind)
            {
                case Kind.Scale: t.Rt.localScale = Vector3.LerpUnclamped(t.From, t.To, e); break;
                case Kind.Move: t.Rt.anchoredPosition = Vector2.LerpUnclamped(t.From, t.To, e); break;
                case Kind.Rotate:
                {
                    float z = t.From.z + Mathf.DeltaAngle(t.From.z, t.To.z) * e;
                    t.Rt.localEulerAngles = new Vector3(0f, 0f, z);
                    break;
                }
                case Kind.Fade: t.Cg.alpha = Mathf.LerpUnclamped(t.From.x, t.To.x, e); break;
                case Kind.Color: t.G.color = UnityEngine.Color.LerpUnclamped(t.CFrom, t.CTo, e); break;
                case Kind.Value:
                    if (t.OnValue != null)
                    {
                        try { t.OnValue(e); } catch (Exception ex) { Debug.LogException(ex); t.OnValue = null; }
                    }
                    break;
                case Kind.Shake:
                {
                    float damp = 1f - n;
                    float x = Mathf.Sin(n * 47f) * t.Amplitude * damp;
                    float y = Mathf.Sin(n * 31f + 1.3f) * t.Amplitude * 0.35f * damp;
                    t.Rt.anchoredPosition = new Vector2(t.From.x + x, t.From.y + y);
                    break;
                }
                case Kind.Punch:
                {
                    float k = Mathf.Sin(n * Mathf.PI) * (1f - n * 0.3f);
                    t.Rt.localScale = t.From * (1f + t.Amplitude * k);
                    break;
                }
            }
        }
    }

    /// <summary>Drives UITween on unscaled time. Created automatically.</summary>
    public sealed class UITweenRunner : MonoBehaviour
    {
        void Update() => UITween.Tick(UITime.Delta);
    }
}
