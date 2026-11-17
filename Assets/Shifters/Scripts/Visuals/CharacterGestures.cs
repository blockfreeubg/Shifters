// =====================================================================================
//  SHIFTERS - CharacterGestures.cs   (VISUALS module)
//  One-shot gestures (VisualAction) and idle fidgets as pose functions of time.
//  Upper-body gestures blend over locomotion; "full" gestures (Dance, Celebrate, Taunt, Strain)
//  also own hips/legs/root.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class CharacterGestures
    {
        public static float Duration(VisualAction a)
        {
            switch (a)
            {
                case VisualAction.Wave: return 1.5f;
                case VisualAction.FistPump: return 1.1f;
                case VisualAction.Punch: return 0.55f;
                case VisualAction.Interact: return 0.8f;
                case VisualAction.Shrug: return 1.2f;
                case VisualAction.Dance: return 3.2f;
                case VisualAction.Point: return 1.1f;
                case VisualAction.Facepalm: return 1.5f;
                case VisualAction.Strain: return 1.3f;
                case VisualAction.Celebrate: return 1.7f;
                case VisualAction.Taunt: return 2.0f;
                default: return 1f;
            }
        }

        public static bool IsFullBody(VisualAction a) =>
            a == VisualAction.Dance || a == VisualAction.Celebrate || a == VisualAction.Taunt || a == VisualAction.Strain;

        /// <summary>Face shown while the gesture plays.</summary>
        public static Expression Face(VisualAction a)
        {
            switch (a)
            {
                case VisualAction.Wave: case VisualAction.Dance: case VisualAction.Celebrate: case VisualAction.FistPump: return Expression.Happy;
                case VisualAction.Punch: return Expression.Angry;
                case VisualAction.Interact: case VisualAction.Point: return Expression.Determined;
                case VisualAction.Shrug: case VisualAction.Taunt: return Expression.Smug;
                case VisualAction.Facepalm: return Expression.Sad;
                case VisualAction.Strain: return Expression.Strain;
                default: return Expression.Neutral;
            }
        }

        /// <summary>Writes the gesture pose into g (g starts as the current base pose). t = seconds since start.</summary>
        public static void Pose(VisualAction a, float t, float legLen, ref BodyPose g, out float tremble)
        {
            tremble = 0f;
            float dur = Duration(a), u = Mathf.Clamp01(t / dur);
            const float TAU = Mathf.PI * 2f;
            switch (a)
            {
                case VisualAction.Wave:
                    g.ShR = new Vector3(-25f, 0f, 150f); g.ElR = new Vector3(-15f, 0f, 28f + 32f * Mathf.Sin(t * TAU * 3.2f)); g.HaR = Vector3.zero;
                    g.Head = new Vector3(g.Head.x, g.Head.y, 7f); g.Spine.z = -4f;
                    break;
                case VisualAction.FistPump:
                    {
                        float pump = Mathf.Abs(Mathf.Sin(t * TAU * 1.4f));
                        g.ShR = new Vector3(-55f - 115f * pump, 0f, 12f); g.ElR = new Vector3(-125f + 105f * pump, 0f, 0f);
                        g.ShL = new Vector3(5f, 0f, -12f); g.Spine.x = -6f * pump; g.Head.x = -10f * pump;
                        break;
                    }
                case VisualAction.Punch:
                    {
                        var wind = new Vector3(28f, 0f, 8f); var hit = new Vector3(-92f, 0f, 0f); var rest = new Vector3(-25f, 0f, 8f);
                        if (t < 0.18f) { float w = t / 0.18f; g.ShR = Vector3.Lerp(g.ShR, wind, w); g.ElR = new Vector3(-115f * w, 0f, 0f); g.Spine.y = -25f * w; }
                        else if (t < 0.32f) { float w = (t - 0.18f) / 0.14f; g.ShR = Vector3.Lerp(wind, hit, w); g.ElR = new Vector3(Mathf.Lerp(-115f, -4f, w), 0f, 0f); g.Spine.y = Mathf.Lerp(-25f, 28f, w); g.Spine.x += 12f * w; }
                        else { float w = Mathf.Clamp01((t - 0.32f) / 0.23f); g.ShR = Vector3.Lerp(hit, rest, w); g.ElR = new Vector3(Mathf.Lerp(-4f, -60f, w), 0f, 0f); g.Spine.y = Mathf.Lerp(28f, 0f, w); g.Spine.x += 12f * (1f - w); }
                        g.ShL = new Vector3(-35f, 0f, -12f); g.ElL = new Vector3(-95f, 0f, 0f);
                        break;
                    }
                case VisualAction.Interact:
                    {
                        float r = AnimMath.Bump(u, 0.3f, 0.7f);
                        g.ShR = new Vector3(-82f * r, 0f, 4f); g.ElR = new Vector3(-12f - 15f * r, 0f, 0f); g.Spine.x += 14f * r; g.Head.x += 10f * r;
                        break;
                    }
                case VisualAction.Shrug:
                    {
                        float r = AnimMath.Bump(u);
                        g.ShLPos = new Vector3(0f, 0.06f * r, 0f); g.ShRPos = g.ShLPos;
                        g.ShL = new Vector3(-18f * r, 0f, -30f * r); g.ShR = new Vector3(-18f * r, 0f, 30f * r);
                        g.ElL = new Vector3(-85f * r, 0f, 0f); g.ElR = g.ElL;
                        g.HaL = new Vector3(0f, 0f, -35f * r); g.HaR = new Vector3(0f, 0f, 35f * r);
                        g.Head = new Vector3(g.Head.x, g.Head.y, 14f * r);
                        break;
                    }
                case VisualAction.Dance:
                    {
                        float sway = Mathf.Sin(t * TAU), bob = Mathf.Abs(Mathf.Sin(t * TAU * 2f)), up = 0.5f + 0.5f * Mathf.Sin(t * TAU);
                        g.HipsPos = new Vector3(0.05f * sway * legLen, -0.07f * bob * legLen, 0f); g.HipsRot = new Vector3(0f, 10f * sway, 12f * sway);
                        g.KnL = new Vector3(24f * bob, 0f, 0f); g.KnR = g.KnL; g.LgL = new Vector3(-12f * bob, 0f, -4f); g.LgR = new Vector3(-12f * bob, 0f, 4f);
                        g.FtL = new Vector3(-12f * bob, 0f, 0f); g.FtR = g.FtL;
                        g.ShR = Vector3.Lerp(new Vector3(-35f, 0f, -25f), new Vector3(-165f, 0f, 22f), up); g.ElR = new Vector3(-15f, 0f, 0f);
                        g.ShL = new Vector3(20f, 0f, -38f); g.ElL = new Vector3(-110f, 0f, 0f);
                        g.Head = new Vector3(8f * Mathf.Sin(t * TAU * 2f), 15f * sway, 0f); g.Spine = new Vector3(2f, -6f * sway, -8f * sway);
                        break;
                    }
                case VisualAction.Point:
                    {
                        float r = AnimMath.Bump(u, 0.18f, 0.8f);
                        g.ShR = new Vector3(-100f * r, 0f, 4f * r); g.ElR = new Vector3(-3f, 0f, 0f); g.HaR = Vector3.zero; g.Spine.y = 8f * r;
                        break;
                    }
                case VisualAction.Facepalm:
                    {
                        float r = AnimMath.Bump(u, 0.2f, 0.8f);
                        g.ShR = new Vector3(-128f * r, 0f, -32f * r); g.ElR = new Vector3(-138f * r, 0f, 0f); g.Head.x += 20f * r; g.Spine.x += 8f * r;
                        break;
                    }
                case VisualAction.Strain:
                    g.ShL = new Vector3(-58f, 0f, 8f); g.ShR = new Vector3(-58f, 0f, -8f); g.ElL = new Vector3(-35f, 0f, 0f); g.ElR = g.ElL;
                    g.Spine = new Vector3(-12f, 0f, 0f); g.Head = new Vector3(-8f, 0f, 0f);
                    g.KnL = new Vector3(25f, 0f, 0f); g.KnR = g.KnL; g.LgL = new Vector3(-12f, 0f, -6f); g.LgR = new Vector3(-12f, 0f, 6f);
                    g.FtL = new Vector3(-12f, 0f, 0f); g.FtR = g.FtL; g.HipsPos = new Vector3(0f, -0.06f * legLen, 0f);
                    tremble = 2.5f;
                    break;
                case VisualAction.Celebrate:
                    {
                        float hop = Mathf.Max(0f, Mathf.Sin(t * TAU * 1.2f));
                        g.RootPos = new Vector3(0f, 0.32f * hop * Mathf.Max(0.6f, legLen), 0f);
                        g.KnL = new Vector3(35f * (1f - hop) + 20f * hop, 0f, 0f); g.KnR = g.KnL; g.LgL = new Vector3(-15f * hop, 0f, -5f); g.LgR = new Vector3(-15f * hop, 0f, 5f);
                        g.ShL = new Vector3(-165f + 12f * Mathf.Sin(t * 25f), 0f, -28f); g.ShR = new Vector3(-165f + 12f * Mathf.Sin(t * 25f + 1f), 0f, 28f);
                        g.ElL = new Vector3(-12f, 0f, 0f); g.ElR = g.ElL; g.Head = new Vector3(-12f, 0f, 0f); g.Spine = new Vector3(-4f, 0f, 0f);
                        break;
                    }
                case VisualAction.Taunt:
                    {
                        float turn = AnimMath.Smooth01(t / 0.25f) * (1f - AnimMath.Smooth01((t - (dur - 0.3f)) / 0.3f));
                        g.RootRot = Quaternion.Euler(0f, 180f * turn, 0f);
                        g.Spine = new Vector3(38f * turn, 0f, 0f);
                        g.HipsRot = new Vector3(0f, 0f, 18f * turn * Mathf.Sin(t * TAU * 3.5f)); g.HipsPos = new Vector3(0.03f * turn * Mathf.Sin(t * TAU * 3.5f), 0f, 0f);
                        g.ShL = new Vector3(-38f * turn, 0f, -6f); g.ShR = new Vector3(-38f * turn, 0f, 6f); g.ElL = new Vector3(-25f * turn, 0f, 0f); g.ElR = g.ElL;
                        g.KnL = new Vector3(18f * turn, 0f, 0f); g.KnR = g.KnL;
                        g.Head = new Vector3(-25f * turn, 70f * turn, 0f);
                        break;
                    }
            }
        }

        /// <summary>Idle fidgets (0 scratch head, 1 check watch, 2 stretch, 3 foot tap). w = blend weight.</summary>
        public static void Fidget(int kind, float t, ref BodyPose p, float w)
        {
            var g = p;
            switch (kind)
            {
                case 0: g.ShR = new Vector3(-150f, 0f, -15f); g.ElR = new Vector3(-125f + 12f * Mathf.Sin(t * 30f), 0f, 0f); g.Head = new Vector3(8f, 0f, -10f); break;
                case 1: g.ShL = new Vector3(-62f, 0f, 22f); g.ElL = new Vector3(-105f, 0f, 0f); g.Head = new Vector3(24f, -18f, 0f); break;
                case 2: g.ShL = new Vector3(-172f, 0f, -12f); g.ShR = new Vector3(-172f, 0f, 12f); g.ElL = new Vector3(-10f, 0f, 0f); g.ElR = g.ElL; g.Spine.x = -10f; g.Head.x = -15f; break;
                default:
                    g.LgR = new Vector3(-12f, 0f, 2f); g.KnR = new Vector3(10f, 0f, 0f); g.FtR = new Vector3(-20f * Mathf.Abs(Mathf.Sin(t * 14f)), 0f, 0f);
                    g.ShL = new Vector3(-35f, 0f, 22f); g.ShR = new Vector3(-35f, 0f, -22f); g.ElL = new Vector3(-100f, 0f, 0f); g.ElR = g.ElL;
                    break;
            }
            p.BlendUpper(g, w);
            if (kind == 3) { p.LgR = Vector3.Lerp(p.LgR, g.LgR, w); p.KnR = Vector3.Lerp(p.KnR, g.KnR, w); p.FtR = Vector3.Lerp(p.FtR, g.FtR, w); }
        }
    }
}
