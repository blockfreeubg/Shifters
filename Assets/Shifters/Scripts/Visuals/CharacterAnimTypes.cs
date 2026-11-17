// =====================================================================================
//  SHIFTERS - CharacterAnimTypes.cs   (VISUALS module)
//  Plain data used by the procedural animator: per-frame motion input and a full-body pose
//  (Euler offsets per joint, model-root transform and bridge stretch scales).
//  Angle conventions (model space, rest = arms/legs hanging straight down):
//    shoulder/leg X: negative = swing FORWARD, positive = back.  elbow X negative = bend forward.
//    knee X positive = bend (shin back).  spine X positive = lean forward.  head X positive = look down.
//    shoulder Z: positive moves the hand toward +X (right arm raise outward = +Z, left arm = -Z).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>What the animator needs to know about the body this frame.</summary>
    public struct AnimInput
    {
        public float Speed;            // planar m/s relative to the ground
        public float Forward;          // +1 moving forward, -1 moving backwards
        public float VerticalSpeed;    // m/s
        public bool Grounded, Sprinting, Turbo, Tumbling, Stunned;
        public float AirTime;
        public Vector3 AngularVelocity; // local rad/s (tumble wobble)
        public float Upright;          // dot(physics root up, world up); 1 = standing
        public float TurnRate;         // yaw deg/s (lean into turns)
        public PoseOverride Pose;
        public float Strain01;         // carry load / bridge load
        public float AimPitch;         // degrees, + = up

        public static AnimInput Idle => new AnimInput { Grounded = true, Forward = 1f, Upright = 1f };
    }

    /// <summary>Full-body pose: Euler offsets per joint (degrees) + root transform + bridge scales.</summary>
    public struct BodyPose
    {
        public Vector3 RootPos; public Quaternion RootRot;
        public Vector3 HipsPos, HipsRot, Spine, Head;
        public Vector3 ShL, ShR, ElL, ElR, HaL, HaR, ShLPos, ShRPos;
        public Vector3 LgL, LgR, KnL, KnR, FtL, FtR;
        public float LegScale, SpineScale, ArmScale;

        public static BodyPose Rest => new BodyPose { RootRot = Quaternion.identity, LegScale = 1f, SpineScale = 1f, ArmScale = 1f };

        public static BodyPose Lerp(in BodyPose a, in BodyPose b, float t)
        {
            var p = a;
            p.BlendLower(b, t);
            p.BlendUpper(b, t);
            p.LegScale = Mathf.Lerp(a.LegScale, b.LegScale, t);
            p.SpineScale = Mathf.Lerp(a.SpineScale, b.SpineScale, t);
            p.ArmScale = Mathf.Lerp(a.ArmScale, b.ArmScale, t);
            return p;
        }

        /// <summary>Blend arms, spine and head toward g.</summary>
        public void BlendUpper(in BodyPose g, float w)
        {
            Spine = Vector3.LerpUnclamped(Spine, g.Spine, w); Head = Vector3.LerpUnclamped(Head, g.Head, w);
            ShL = Vector3.LerpUnclamped(ShL, g.ShL, w); ShR = Vector3.LerpUnclamped(ShR, g.ShR, w);
            ElL = Vector3.LerpUnclamped(ElL, g.ElL, w); ElR = Vector3.LerpUnclamped(ElR, g.ElR, w);
            HaL = Vector3.LerpUnclamped(HaL, g.HaL, w); HaR = Vector3.LerpUnclamped(HaR, g.HaR, w);
            ShLPos = Vector3.LerpUnclamped(ShLPos, g.ShLPos, w); ShRPos = Vector3.LerpUnclamped(ShRPos, g.ShRPos, w);
        }

        /// <summary>Blend root, hips and legs toward g.</summary>
        public void BlendLower(in BodyPose g, float w)
        {
            RootPos = Vector3.LerpUnclamped(RootPos, g.RootPos, w); RootRot = Quaternion.Slerp(RootRot, g.RootRot, w);
            HipsPos = Vector3.LerpUnclamped(HipsPos, g.HipsPos, w); HipsRot = Vector3.LerpUnclamped(HipsRot, g.HipsRot, w);
            LgL = Vector3.LerpUnclamped(LgL, g.LgL, w); LgR = Vector3.LerpUnclamped(LgR, g.LgR, w);
            KnL = Vector3.LerpUnclamped(KnL, g.KnL, w); KnR = Vector3.LerpUnclamped(KnR, g.KnR, w);
            FtL = Vector3.LerpUnclamped(FtL, g.FtL, w); FtR = Vector3.LerpUnclamped(FtR, g.FtR, w);
        }
    }

    static class AnimMath
    {
        public static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        public static float Bump(float u, float a = 0.22f, float b = 0.78f) => Smooth01(u / a) * (1f - Smooth01((u - b) / (1f - b)));
        public static float EaseOutBack(float x) { x = Mathf.Clamp01(x); const float c1 = 1.9f, c3 = c1 + 1f; float t = x - 1f; return 1f + c3 * t * t * t + c1 * t * t; }
        public static float Damp(float k, float dt) => 1f - Mathf.Exp(-k * dt);

        /// <summary>Integration step for springs: stiff springs explode with long frames, so they are sub-stepped.</summary>
        public const float MaxStep = 1f / 90f;

        public static int Steps(float dt, out float h)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / MaxStep - 0.001f), 1, 12);
            h = dt / n;
            return n;
        }

        /// <summary>Damped spring toward target (stiffness k, damping c), stable for any dt.</summary>
        public static void Spring(ref float x, ref float v, float target, float k, float c, float dt)
        {
            if (dt <= 0f) return;
            int n = Steps(dt, out float h);
            for (int i = 0; i < n; i++) { v += ((target - x) * k - v * c) * h; x += v * h; }
        }

        public static void Spring(ref Vector2 x, ref Vector2 v, Vector2 target, float k, float c, float dt)
        {
            if (dt <= 0f) return;
            int n = Steps(dt, out float h);
            for (int i = 0; i < n; i++) { v += ((target - x) * k - v * c) * h; x += v * h; }
        }
    }
}
