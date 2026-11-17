// =====================================================================================
//  SHIFTERS - Fx.cs   [CONTRACT API - implementation owned by the RENDERING agent]
//  Fire-and-forget pooled visual effects. All calls must be safe to spam and safe in batchmode.
//  Edit mode: every call is a no-op. Backend: FxSystem (persistent [Fx] root, shared particle systems).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static partial class Fx
    {
        static readonly Color DustColor = new Color(0.86f, 0.83f, 0.78f, 0.7f);
        static readonly Color SparkColor = new Color(1f, 0.78f, 0.32f, 1f);

        static FxSystem S => FxSystem.Get();

        static Vector3 RandXZ(float min, float max)
        {
            float a = Random.value * Mathf.PI * 2f, r = Random.Range(min, max);
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        public static void Puff(Vector3 pos, float size = 1f, Color? color = null)                  // dust (landing, footsteps, poofs)
        {
            var s = S; if (s == null) return;
            size = Mathf.Max(0.05f, size);
            var c = color ?? DustColor;
            int n = size < 0.6f ? 4 : 7;
            for (int i = 0; i < n; i++)
            {
                var d = RandXZ(0.6f, 1.6f) * size;
                s.Emit(s.Dust, pos + d * 0.12f + Vector3.up * 0.06f, d + Vector3.up * Random.Range(0.15f, 0.5f) * size,
                    Random.Range(0.35f, 0.6f) * size, Random.Range(0.45f, 0.8f), c, Random.value * 360f);
            }
        }

        public static void Sparks(Vector3 pos, Vector3 normal, int count = 12, Color? color = null)  // metal hits, lasers
        {
            var s = S; if (s == null) return;
            var c = color ?? SparkColor;
            var n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            count = Mathf.Clamp(count, 1, 80);
            for (int i = 0; i < count; i++)
            {
                var d = (n + Random.insideUnitSphere * 0.85f).normalized;
                s.Emit(s.SparksPs, pos + n * 0.02f, d * Random.Range(3f, 8.5f), Random.Range(0.04f, 0.08f), Random.Range(0.22f, 0.55f), c);
            }
            s.Emit(s.GlowPs, pos + n * 0.05f, Vector3.zero, 0.55f, 0.09f, c);
        }

        public static void Impact(Vector3 pos, Vector3 normal, Color color)                          // bullet impact
        {
            var s = S; if (s == null) return;
            var n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            for (int i = 0; i < 7; i++)
            {
                var d = (n * 1.2f + Random.insideUnitSphere).normalized;
                s.Emit(s.SparksPs, pos + n * 0.02f, d * Random.Range(2.5f, 6f), Random.Range(0.035f, 0.06f), Random.Range(0.15f, 0.35f), color);
            }
            s.Emit(s.GlowPs, pos + n * 0.04f, Vector3.zero, 0.7f, 0.08f, color);
            s.Emit(s.SparklePs, pos + n * 0.05f, Vector3.zero, 0.45f, 0.12f, color, Random.value * 360f);
            for (int i = 0; i < 2; i++)
                s.Emit(s.Dust, pos + n * 0.08f, (n + Random.insideUnitSphere * 0.5f) * 0.8f, Random.Range(0.18f, 0.3f), Random.Range(0.35f, 0.55f), DustColor);
        }

        public static void Poof(Vector3 pos, Color color, float size = 1f)                          // (re)spawn / vanish
        {
            var s = S; if (s == null) return;
            size = Mathf.Max(0.1f, size);
            var smoke = Color.Lerp(Color.white, color, 0.3f); smoke.a = 1f;
            for (int i = 0; i < 12; i++)
            {
                var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.7f + 0.1f;
                s.Emit(s.PuffPs, pos + d * 0.2f * size, d * Random.Range(2.2f, 3.8f) * size, Random.Range(0.5f, 0.9f) * size, Random.Range(0.5f, 0.8f), smoke, Random.value * 360f);
            }
            for (int i = 0; i < 6; i++)
                s.Emit(s.SparklePs, pos + Random.insideUnitSphere * 0.6f * size + Vector3.up * 0.3f * size, Random.onUnitSphere * 1.2f, Random.Range(0.25f, 0.4f) * size, Random.Range(0.4f, 0.6f), color, Random.value * 360f);
            s.Emit(s.GlowPs, pos + Vector3.up * 0.2f * size, Vector3.zero, 1.8f * size, 0.16f, new Color(color.r, color.g, color.b, 0.6f));
        }

        public static void Confetti(Vector3 pos)                                                    // puzzle solved, level complete
        {
            var s = S; if (s == null) return;
            for (int i = 0; i < 70; i++)
            {
                var v = Vector3.up * Random.Range(5f, 9.5f) + RandXZ(0.5f, 3.2f);
                s.EmitConfetti(pos + Random.insideUnitSphere * 0.3f, v, Random.Range(0.1f, 0.17f), Random.Range(2.2f, 3.2f), FxSystem.RandomConfettiColor());
            }
            for (int i = 0; i < 8; i++)
                s.Emit(s.SparklePs, pos + Random.insideUnitSphere * 0.8f + Vector3.up * 0.8f, Random.onUnitSphere * 2f, Random.Range(0.3f, 0.5f), Random.Range(0.5f, 0.8f), FxSystem.RandomConfettiColor(), Random.value * 360f);
            s.Emit(s.GlowPs, pos + Vector3.up * 0.5f, Vector3.zero, 2.2f, 0.2f, new Color(1f, 0.85f, 0.5f, 0.7f));
        }

        public static void Stars(Transform follow, float duration)                                  // dizzy / KO stars orbiting the head
        {
            if (follow == null || duration <= 0f) return;
            var s = S; if (s == null) return;
            s.Stars(follow, duration);
        }

        public static void Splash(Vector3 pos, Color color)                                         // acid / water
        {
            var s = S; if (s == null) return;
            var c = color; c.a = Mathf.Max(0.6f, c.a);
            for (int i = 0; i < 18; i++)
            {
                var v = Vector3.up * Random.Range(2.5f, 5.5f) + RandXZ(1f, 3f);
                s.Emit(s.DropsPs, pos + Vector3.up * 0.05f, v, Random.Range(0.08f, 0.16f), Random.Range(0.5f, 0.85f), c);
            }
            s.Emit(s.RipplePs, pos + Vector3.up * 0.03f, Vector3.zero, 1.8f, 0.6f, c);
            s.Emit(s.RipplePs, pos + Vector3.up * 0.03f, Vector3.zero, 1.1f, 0.45f, c);
            for (int i = 0; i < 3; i++)
                s.Emit(s.Dust, pos + Vector3.up * 0.2f, RandXZ(0.3f, 0.8f) + Vector3.up * 0.6f, Random.Range(0.35f, 0.55f), Random.Range(0.4f, 0.6f), new Color(c.r, c.g, c.b, 0.45f));
        }

        public static void Explosion(Vector3 pos, float radius)
        {
            var s = S; if (s == null) return;
            float r = Mathf.Clamp(radius, 0.4f, 12f);
            s.Emit(s.GlowPs, pos, Vector3.zero, 3.2f * r, 0.22f, new Color(1f, 0.82f, 0.5f, 1f));
            for (int i = 0; i < 12; i++)
            {
                var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.8f + 0.1f;
                var fire = Color.Lerp(new Color(1f, 0.5f, 0.15f, 1f), new Color(1f, 0.85f, 0.3f, 1f), Random.value);
                s.Emit(s.PuffPs, pos + d * 0.25f * r, d * Random.Range(3f, 6f) * r * 0.6f, Random.Range(0.8f, 1.3f) * r, Random.Range(0.4f, 0.65f), fire, Random.value * 360f);
            }
            for (int i = 0; i < 9; i++)
            {
                var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y);
                s.Emit(s.Dust, pos + d * 0.4f * r, d * Random.Range(1f, 2.5f) * r + Vector3.up * 1.2f, Random.Range(1.1f, 1.7f) * r, Random.Range(1f, 1.6f), new Color(0.25f, 0.24f, 0.27f, 0.75f), Random.value * 360f);
            }
            for (int i = 0; i < 26; i++)
            {
                var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.9f + 0.05f;
                s.Emit(s.SparksPs, pos, d * Random.Range(6f, 14f) * Mathf.Sqrt(r), Random.Range(0.05f, 0.1f), Random.Range(0.4f, 0.9f), SparkColor);
            }
            for (int i = 0; i < 10; i++)
            {
                var v = Vector3.up * Random.Range(4f, 8f) + RandXZ(1.5f, 4f);
                s.EmitConfetti(pos, v * Mathf.Sqrt(r), Random.Range(0.12f, 0.22f) * Mathf.Sqrt(r), Random.Range(1.2f, 2f), new Color(0.2f, 0.2f, 0.22f, 1f));
            }
            s.Emit(s.ShockPs, pos + Vector3.up * 0.1f, Vector3.zero, 5f * r, 0.38f, new Color(1f, 0.7f, 0.4f, 0.9f));
            s.FlashLight(pos + Vector3.up * 0.5f, new Color(1f, 0.65f, 0.3f), 12f, 7f * r, 0.3f);
        }

        public static void ScanPulse(Vector3 origin, Color color, float radius = 30f, float duration = 0.9f) // perspective shift wave
        {
            var s = S; if (s == null) return;
            s.ScanPulse(origin, color, radius, duration);
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.06f, float duration = 0.08f)
        {
            var s = S; if (s == null) return;
            s.Tracer(from, to, color, width, duration);
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir, Color color)
        {
            var s = S; if (s == null) return;
            var d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            s.Emit(s.GlowPs, pos + d * 0.08f, d * 0.5f, 0.75f, 0.07f, color);
            s.Emit(s.SparklePs, pos + d * 0.12f, Vector3.zero, 0.55f, 0.06f, Color.Lerp(color, Color.white, 0.5f), Random.value * 360f);
            for (int i = 0; i < 4; i++)
                s.Emit(s.SparksPs, pos, (d + Random.insideUnitSphere * 0.35f).normalized * Random.Range(6f, 11f), 0.04f, Random.Range(0.08f, 0.14f), color);
            s.Emit(s.Dust, pos + d * 0.2f, d * 1.2f, 0.25f, 0.35f, new Color(0.9f, 0.9f, 0.9f, 0.35f));
            s.FlashLight(pos + d * 0.15f, color, 4f, 5f, 0.07f);
        }

        public static void FloatingText(Vector3 pos, string text, Color color, float scale = 1f)    // "OOF", "-35", "TOO HEAVY"
        {
            if (string.IsNullOrEmpty(text)) return;
            var s = S; if (s == null) return;
            s.FloatingText(pos, text, color, scale);
        }

        /// <summary>Rising steam / fumes puff (steam stacks, vents, acid fumes). dir = rise direction.</summary>
        public static void Steam(Vector3 pos, Vector3 dir, float size = 1f, Color? color = null)
        {
            var s = S; if (s == null) return;
            var c = color ?? new Color(0.95f, 0.95f, 0.97f, 0.5f);
            var d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            for (int i = 0; i < 2; i++)
                s.Emit(s.Dust, pos + Random.insideUnitSphere * 0.1f * size, (d * Random.Range(1.6f, 2.6f) + Random.insideUnitSphere * 0.35f) * size,
                    Random.Range(0.35f, 0.55f) * size, Random.Range(0.9f, 1.4f), c, Random.value * 360f);
        }

        public static void SpeedLines(Transform follow, float duration)                            // turbo sprint
        {
            if (follow == null || duration <= 0f) return;
            var s = S; if (s == null) return;
            s.SpeedLines(follow, duration);
        }
    }
}
