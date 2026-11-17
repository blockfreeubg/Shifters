// =====================================================================================
//  SHIFTERS - UIIcons.cs   (UI agent)
//  Procedural, cached icon sprites (white silhouettes, tint with Image.color): weapons,
//  perks, character states, AI modes, hint bulb, warning, check, star, lock...
//  Each icon is a small list of signed-distance shapes evaluated once into a 128px texture.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public enum UIIcon
    {
        Pistol, Shotgun, Laser, Slingshot, Chicken, Plunger,
        Strength, Speed, Brain, Crosshair,
        KO, Captured, Lost,
        Stay, Follow,
        Bulb, Warning, Check, Star, Lock, Flag, Clock, Person
    }

    public static class UIIcons
    {
        const int Size = 128;
        static readonly Dictionary<UIIcon, Sprite> _cache = new Dictionary<UIIcon, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cache.Clear();

        public static Sprite Get(UIIcon icon)
        {
            if (_cache.TryGetValue(icon, out var s) && s != null) return s;
            var shapes = Define(icon);
            var tex = UISprites.Make(Size, Size, true, (x, y) =>
            {
                float d = UIShapes.Evaluate(shapes, x / Size, y / Size);
                return Mathf.Clamp01(0.5f - d * Size);
            });
            tex.name = "UIIcon_" + icon;
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = tex.name;
            _cache[icon] = s;
            return s;
        }

        /// <summary>Icon for a weapon (type + experimental variant).</summary>
        public static UIIcon ForWeapon(WeaponData w)
        {
            if (w == null) return UIIcon.Pistol;
            switch (w.Type)
            {
                case WeaponType.Shotgun: return UIIcon.Shotgun;
                case WeaponType.LaserGun: return UIIcon.Laser;
                case WeaponType.Slingshot: return UIIcon.Slingshot;
                case WeaponType.Experimental: return w.Variant == ExperimentalVariant.GravityPlunger ? UIIcon.Plunger : UIIcon.Chicken;
                default: return UIIcon.Pistol;
            }
        }

        /// <summary>Icon for a single perspective bit.</summary>
        public static UIIcon ForPerspective(PerspectiveMask bit)
        {
            if ((bit & PerspectiveMask.Strength) != 0) return UIIcon.Strength;
            if ((bit & PerspectiveMask.Speed) != 0) return UIIcon.Speed;
            if ((bit & PerspectiveMask.Intellect) != 0) return UIIcon.Brain;
            return UIIcon.Crosshair;
        }

        public static UIIcon ForState(CharacterState s) => s == CharacterState.Captured ? UIIcon.Captured : s == CharacterState.Lost ? UIIcon.Lost : UIIcon.KO;

        // ------------------------------------------------------------------ icon definitions (unit square, y up)
        static List<UIShapes.Shape> Define(UIIcon icon)
        {
            var L = new List<UIShapes.Shape>();
            switch (icon)
            {
                case UIIcon.Pistol:
                    L.Add(UIShapes.Box(0.55f, 0.64f, 0.33f, 0.095f, 0f, 0.03f));
                    L.Add(UIShapes.Box(0.33f, 0.4f, 0.085f, 0.2f, -16f, 0.035f));
                    L.Add(UIShapes.Ring(0.5f, 0.47f, 0.07f, 0.022f));
                    L.Add(UIShapes.Box(0.83f, 0.76f, 0.03f, 0.035f, 0f, 0.01f));
                    L.Add(UIShapes.Box(0.55f, 0.64f, 0.26f, 0.018f, 0f, 0.01f, true));
                    break;
                case UIIcon.Shotgun:
                    L.Add(UIShapes.Box(0.6f, 0.6f, 0.36f, 0.055f, 0f, 0.02f));
                    L.Add(UIShapes.Box(0.62f, 0.5f, 0.3f, 0.04f, 0f, 0.02f));
                    L.Add(UIShapes.Box(0.18f, 0.5f, 0.15f, 0.085f, -18f, 0.04f));
                    L.Add(UIShapes.Box(0.62f, 0.44f, 0.09f, 0.06f, 0f, 0.03f));
                    L.Add(UIShapes.Box(0.33f, 0.42f, 0.04f, 0.12f, -10f, 0.02f));
                    break;
                case UIIcon.Laser:
                    L.Add(UIShapes.Box(0.42f, 0.6f, 0.26f, 0.14f, 0f, 0.09f));
                    L.Add(UIShapes.Circle(0.76f, 0.6f, 0.1f));
                    L.Add(UIShapes.Ring(0.76f, 0.6f, 0.17f, 0.025f));
                    L.Add(UIShapes.Box(0.33f, 0.36f, 0.075f, 0.16f, -15f, 0.035f));
                    L.Add(UIShapes.Circle(0.36f, 0.62f, 0.045f, true));
                    L.Add(UIShapes.Circle(0.5f, 0.62f, 0.045f, true));
                    break;
                case UIIcon.Slingshot:
                    L.Add(UIShapes.Capsule(0.5f, 0.1f, 0.5f, 0.48f, 0.065f));
                    L.Add(UIShapes.Capsule(0.5f, 0.48f, 0.27f, 0.84f, 0.06f));
                    L.Add(UIShapes.Capsule(0.5f, 0.48f, 0.73f, 0.84f, 0.06f));
                    L.Add(UIShapes.Capsule(0.27f, 0.82f, 0.5f, 0.64f, 0.022f));
                    L.Add(UIShapes.Capsule(0.73f, 0.82f, 0.5f, 0.64f, 0.022f));
                    L.Add(UIShapes.Circle(0.5f, 0.63f, 0.065f));
                    break;
                case UIIcon.Chicken:
                    L.Add(UIShapes.Circle(0.42f, 0.44f, 0.21f));
                    L.Add(UIShapes.Capsule(0.52f, 0.5f, 0.66f, 0.74f, 0.075f));
                    L.Add(UIShapes.Circle(0.68f, 0.78f, 0.1f));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.75f, 0.83f), new Vector2(0.95f, 0.77f), new Vector2(0.75f, 0.72f) }));
                    L.Add(UIShapes.Circle(0.63f, 0.9f, 0.045f));
                    L.Add(UIShapes.Circle(0.7f, 0.92f, 0.045f));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.24f, 0.52f), new Vector2(0.06f, 0.66f), new Vector2(0.12f, 0.42f) }));
                    L.Add(UIShapes.Capsule(0.38f, 0.26f, 0.33f, 0.07f, 0.028f));
                    L.Add(UIShapes.Capsule(0.5f, 0.26f, 0.53f, 0.07f, 0.028f));
                    L.Add(UIShapes.Circle(0.71f, 0.8f, 0.022f, true));
                    break;
                case UIIcon.Plunger:
                    L.Add(UIShapes.Capsule(0.5f, 0.38f, 0.5f, 0.93f, 0.045f));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.12f), new Vector2(0.68f, 0.42f), new Vector2(0.32f, 0.42f) }, 0.04f));
                    L.Add(UIShapes.Box(0.5f, 0.12f, 0.33f, 0.035f, 0f, 0.03f));
                    break;
                case UIIcon.Strength:
                    L.Add(UIShapes.Capsule(0.16f, 0.32f, 0.56f, 0.32f, 0.11f));
                    L.Add(UIShapes.Capsule(0.58f, 0.32f, 0.68f, 0.68f, 0.1f));
                    L.Add(UIShapes.Circle(0.69f, 0.76f, 0.13f));
                    L.Add(UIShapes.Circle(0.4f, 0.45f, 0.155f));
                    break;
                case UIIcon.Speed:
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.62f, 0.96f), new Vector2(0.22f, 0.46f), new Vector2(0.47f, 0.46f),
                        new Vector2(0.36f, 0.04f), new Vector2(0.8f, 0.57f), new Vector2(0.55f, 0.57f), new Vector2(0.7f, 0.96f) }));
                    break;
                case UIIcon.Brain:
                    L.Add(UIShapes.Circle(0.36f, 0.56f, 0.23f));
                    L.Add(UIShapes.Circle(0.64f, 0.56f, 0.23f));
                    L.Add(UIShapes.Circle(0.5f, 0.68f, 0.2f));
                    L.Add(UIShapes.Circle(0.5f, 0.44f, 0.2f));
                    L.Add(UIShapes.Capsule(0.56f, 0.3f, 0.62f, 0.14f, 0.065f));
                    L.Add(UIShapes.Capsule(0.5f, 0.86f, 0.5f, 0.32f, 0.022f, true));
                    L.Add(UIShapes.Capsule(0.22f, 0.56f, 0.36f, 0.62f, 0.02f, true));
                    L.Add(UIShapes.Capsule(0.64f, 0.62f, 0.78f, 0.52f, 0.02f, true));
                    L.Add(UIShapes.Capsule(0.32f, 0.38f, 0.42f, 0.46f, 0.02f, true));
                    L.Add(UIShapes.Capsule(0.58f, 0.74f, 0.68f, 0.8f, 0.02f, true));
                    break;
                case UIIcon.Crosshair:
                    L.Add(UIShapes.Ring(0.5f, 0.5f, 0.3f, 0.05f));
                    L.Add(UIShapes.Box(0.5f, 0.86f, 0.035f, 0.11f, 0f, 0.01f));
                    L.Add(UIShapes.Box(0.5f, 0.14f, 0.035f, 0.11f, 0f, 0.01f));
                    L.Add(UIShapes.Box(0.14f, 0.5f, 0.11f, 0.035f, 0f, 0.01f));
                    L.Add(UIShapes.Box(0.86f, 0.5f, 0.11f, 0.035f, 0f, 0.01f));
                    L.Add(UIShapes.Circle(0.5f, 0.5f, 0.06f));
                    break;
                case UIIcon.KO:
                    L.Add(UIShapes.Circle(0.5f, 0.5f, 0.44f));
                    L.Add(UIShapes.Box(0.5f, 0.5f, 0.27f, 0.065f, 45f, 0.03f, true));
                    L.Add(UIShapes.Box(0.5f, 0.5f, 0.27f, 0.065f, -45f, 0.03f, true));
                    break;
                case UIIcon.Captured:
                case UIIcon.Lock:
                    L.Add(UIShapes.Ring(0.5f, 0.62f, 0.19f, 0.055f));
                    L.Add(UIShapes.Box(0.5f, 0.36f, 0.3f, 0.23f, 0f, 0.06f));
                    L.Add(UIShapes.Circle(0.5f, 0.4f, 0.065f, true));
                    L.Add(UIShapes.Box(0.5f, 0.3f, 0.028f, 0.08f, 0f, 0.01f, true));
                    break;
                case UIIcon.Lost:
                    L.Add(UIShapes.Circle(0.5f, 0.58f, 0.33f));
                    L.Add(UIShapes.Box(0.5f, 0.28f, 0.19f, 0.13f, 0f, 0.05f));
                    L.Add(UIShapes.Circle(0.37f, 0.57f, 0.09f, true));
                    L.Add(UIShapes.Circle(0.63f, 0.57f, 0.09f, true));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.5f, 0.47f), new Vector2(0.55f, 0.38f), new Vector2(0.45f, 0.38f) }, 0f, true));
                    L.Add(UIShapes.Box(0.44f, 0.2f, 0.014f, 0.07f, 0f, 0f, true));
                    L.Add(UIShapes.Box(0.56f, 0.2f, 0.014f, 0.07f, 0f, 0f, true));
                    break;
                case UIIcon.Stay:
                    L.Add(UIShapes.Ring(0.5f, 0.44f, 0.3f, 0.045f));
                    L.Add(UIShapes.Box(0.5f, 0.7f, 0.42f, 0.26f, 0f, 0f, true));
                    L.Add(UIShapes.Capsule(0.5f, 0.78f, 0.5f, 0.16f, 0.05f));
                    L.Add(UIShapes.Capsule(0.32f, 0.64f, 0.68f, 0.64f, 0.04f));
                    L.Add(UIShapes.Ring(0.5f, 0.86f, 0.075f, 0.03f));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.1f, 0.5f), new Vector2(0.3f, 0.5f), new Vector2(0.2f, 0.36f) }));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.7f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.8f, 0.36f) }));
                    break;
                case UIIcon.Follow:
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.1f, 0.86f), new Vector2(0.28f, 0.86f), new Vector2(0.56f, 0.5f), new Vector2(0.28f, 0.14f), new Vector2(0.1f, 0.14f), new Vector2(0.38f, 0.5f) }));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.42f, 0.86f), new Vector2(0.6f, 0.86f), new Vector2(0.88f, 0.5f), new Vector2(0.6f, 0.14f), new Vector2(0.42f, 0.14f), new Vector2(0.7f, 0.5f) }));
                    break;
                case UIIcon.Bulb:
                    L.Add(UIShapes.Circle(0.5f, 0.58f, 0.22f));
                    L.Add(UIShapes.Box(0.5f, 0.33f, 0.11f, 0.08f, 0f, 0.02f));
                    L.Add(UIShapes.Box(0.5f, 0.2f, 0.09f, 0.035f, 0f, 0.02f));
                    L.Add(UIShapes.Box(0.5f, 0.12f, 0.06f, 0.025f, 0f, 0.02f));
                    L.Add(UIShapes.Capsule(0.5f, 0.86f, 0.5f, 0.95f, 0.025f));
                    L.Add(UIShapes.Capsule(0.2f, 0.74f, 0.13f, 0.8f, 0.025f));
                    L.Add(UIShapes.Capsule(0.8f, 0.74f, 0.87f, 0.8f, 0.025f));
                    L.Add(UIShapes.Capsule(0.17f, 0.56f, 0.08f, 0.56f, 0.025f));
                    L.Add(UIShapes.Capsule(0.83f, 0.56f, 0.92f, 0.56f, 0.025f));
                    L.Add(UIShapes.Capsule(0.44f, 0.5f, 0.5f, 0.62f, 0.018f, true));
                    L.Add(UIShapes.Capsule(0.56f, 0.5f, 0.5f, 0.62f, 0.018f, true));
                    break;
                case UIIcon.Warning:
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.5f, 0.9f), new Vector2(0.92f, 0.14f), new Vector2(0.08f, 0.14f) }, 0.04f));
                    L.Add(UIShapes.Box(0.5f, 0.52f, 0.04f, 0.15f, 0f, 0.03f, true));
                    L.Add(UIShapes.Circle(0.5f, 0.27f, 0.05f, true));
                    break;
                case UIIcon.Check:
                    L.Add(UIShapes.Capsule(0.18f, 0.5f, 0.41f, 0.26f, 0.075f));
                    L.Add(UIShapes.Capsule(0.41f, 0.26f, 0.83f, 0.76f, 0.075f));
                    break;
                case UIIcon.Star:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                        float r = i % 2 == 0 ? 0.46f : 0.2f;
                        pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.47f + Mathf.Sin(a) * r);
                    }
                    L.Add(UIShapes.Poly(pts, 0.02f));
                    break;
                }
                case UIIcon.Flag:
                    L.Add(UIShapes.Capsule(0.24f, 0.08f, 0.24f, 0.92f, 0.04f));
                    L.Add(UIShapes.Poly(new[] { new Vector2(0.27f, 0.9f), new Vector2(0.86f, 0.74f), new Vector2(0.27f, 0.54f) }, 0.02f));
                    break;
                case UIIcon.Clock:
                    L.Add(UIShapes.Ring(0.5f, 0.5f, 0.38f, 0.06f));
                    L.Add(UIShapes.Capsule(0.5f, 0.5f, 0.5f, 0.74f, 0.04f));
                    L.Add(UIShapes.Capsule(0.5f, 0.5f, 0.67f, 0.42f, 0.04f));
                    break;
                case UIIcon.Person:
                    L.Add(UIShapes.Circle(0.5f, 0.7f, 0.17f));
                    L.Add(UIShapes.Box(0.5f, 0.24f, 0.3f, 0.2f, 0f, 0.16f));
                    break;
            }
            return L;
        }
    }

    /// <summary>Tiny signed-distance shape toolkit (unit coordinates) used to rasterize icons.</summary>
    public static class UIShapes
    {
        public enum Kind { Circle, Box, Capsule, Ring, Poly }

        public struct Shape
        {
            public Kind Kind;
            public Vector2 A, B;     // circle/ring/box center = A; capsule = A..B; box half-extents = B
            public float R, T, Rot;  // radius / thickness / rotation (deg)
            public Vector2[] Pts;
            public bool Subtract;
        }

        public static Shape Circle(float x, float y, float r, bool sub = false) => new Shape { Kind = Kind.Circle, A = new Vector2(x, y), R = r, Subtract = sub };
        public static Shape Ring(float x, float y, float r, float t, bool sub = false) => new Shape { Kind = Kind.Ring, A = new Vector2(x, y), R = r, T = t, Subtract = sub };
        public static Shape Box(float x, float y, float hx, float hy, float rotDeg = 0f, float round = 0f, bool sub = false) =>
            new Shape { Kind = Kind.Box, A = new Vector2(x, y), B = new Vector2(hx, hy), Rot = rotDeg, R = round, Subtract = sub };
        public static Shape Capsule(float ax, float ay, float bx, float by, float r, bool sub = false) =>
            new Shape { Kind = Kind.Capsule, A = new Vector2(ax, ay), B = new Vector2(bx, by), R = r, Subtract = sub };
        public static Shape Poly(Vector2[] pts, float round = 0f, bool sub = false) => new Shape { Kind = Kind.Poly, Pts = pts, R = round, Subtract = sub };

        /// <summary>Sequentially combine shapes (union, or subtraction when Subtract is set).</summary>
        public static float Evaluate(List<Shape> shapes, float x, float y)
        {
            float d = 10f;
            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                float sd = Distance(s, x, y);
                d = s.Subtract ? Mathf.Max(d, -sd) : Mathf.Min(d, sd);
            }
            return d;
        }

        public static float Distance(Shape s, float x, float y)
        {
            switch (s.Kind)
            {
                case Kind.Circle: return Len(x - s.A.x, y - s.A.y) - s.R;
                case Kind.Ring: return Mathf.Abs(Len(x - s.A.x, y - s.A.y) - s.R) - s.T;
                case Kind.Capsule:
                {
                    float pax = x - s.A.x, pay = y - s.A.y, bax = s.B.x - s.A.x, bay = s.B.y - s.A.y;
                    float h = Mathf.Clamp01((pax * bax + pay * bay) / Mathf.Max(1e-6f, bax * bax + bay * bay));
                    return Len(pax - bax * h, pay - bay * h) - s.R;
                }
                case Kind.Box:
                {
                    float px = x - s.A.x, py = y - s.A.y;
                    if (s.Rot != 0f)
                    {
                        float a = -s.Rot * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
                        float rx = px * c - py * sn, ry = px * sn + py * c;
                        px = rx; py = ry;
                    }
                    float qx = Mathf.Abs(px) - s.B.x + s.R, qy = Mathf.Abs(py) - s.B.y + s.R;
                    return Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - s.R;
                }
                case Kind.Poly: return Polygon(x, y, s.Pts) - s.R;
            }
            return 10f;
        }

        /// <summary>Signed distance to a simple polygon (negative inside).</summary>
        public static float Polygon(float x, float y, Vector2[] v)
        {
            float dx = x - v[0].x, dy = y - v[0].y;
            float d = dx * dx + dy * dy;
            float sign = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = x - v[i].x, wy = y - v[i].y;
                float h = Mathf.Clamp01((wx * ex + wy * ey) / Mathf.Max(1e-8f, ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }
            return sign * Mathf.Sqrt(d);
        }

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);
    }
}
