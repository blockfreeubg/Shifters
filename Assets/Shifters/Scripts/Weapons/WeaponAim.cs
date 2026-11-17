// =====================================================================================
//  SHIFTERS - WeaponAim.cs   [owned by the WEAPONS agent]
//  WHERE SHOTS COME FROM. Every shot (hip-fire, aimed, bot FireAt, the first shot of a burst)
//  travels along the line from a STABLE fire pivot to the target. The pivot sits inside the
//  body at 0.8 x height (never below shoulder height) on the gun side of the aim direction, so it
//  depends only on the body measurements + target direction, never on the animation: a gun
//  hanging at the hip can no longer fire into the floor, and railings / ledge lips / low walls
//  between shooter and target don't eat shots. The gun's muzzle lies ON that line in front of the
//  body (WeaponFireGeometry.MuzzleDistance from the pivot).
//  WeaponArmPose then raises the gun arm onto that line (upper-body twist + 2-bone IK + hand
//  orientation) after the CharacterAnimator wrote the bones, so tracers / muzzle flashes leave
//  the visible gun. No allocations.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Animation-independent fire geometry of one armed character (body measurements + equipped gun model).
    /// Aim frame: forward = planar direction feet -> target, right = Cross(up, forward).</summary>
    public struct WeaponFireGeometry
    {
        /// <summary>Height of the fire pivot above the feet (m). Shots leave along pivot -> target.</summary>
        public float PivotHeight;
        /// <summary>Pivot offset to the right of the body axis in the aim frame (m, inside the capsule).</summary>
        public float PivotSide;
        /// <summary>Pivot -> gun grip (weapon socket) distance along the shot line (m).</summary>
        public float GripDistance;
        /// <summary>Pivot -> muzzle distance along the shot line (m): where projectiles spawn and tracers start.</summary>
        public float MuzzleDistance;
        /// <summary>Muzzle position in weapon-socket space (x right, y = gun top, z = barrel).</summary>
        public Vector3 MuzzleInSocket;
        /// <summary>The geometry was measured from a real body (false = generic fallback numbers).</summary>
        public bool FromBody;
    }

    public static class WeaponAim
    {
        /// <summary>Fire pivot height as a fraction of body height (shoulder height for normal bodies, eye level for the
        /// big-headed 1.5 m Brainiac). Level rule of thumb: Brute ~1.48 m, Sprinter ~1.57 m, Brainiac ~1.2 m.</summary>
        public const float PivotHeightFraction = 0.8f;
        /// <summary>The gun shoulder may reach this far toward an out-of-reach grip (short arms, big heads).</summary>
        public const float MaxShoulderReach = 0.08f;
        /// <summary>Max upper-body twist toward the aim direction when the body faces elsewhere (degrees).</summary>
        public const float MaxTwist = 75f;

        /// <summary>Default muzzle offset (socket space) when no gun model is known yet.</summary>
        public static readonly Vector3 DefaultMuzzleInSocket = new Vector3(0f, 0.07f, 0.45f);

        /// <summary>Measure the fire geometry of a character holding a gun whose muzzle sits at muzzleInSocket.</summary>
        public static WeaponFireGeometry Measure(ShifterCharacter c, Vector3 muzzleInSocket)
        {
            float height = c != null ? Mathf.Max(0.6f, c.Height) : 1.8f;
            float radius = c != null ? Mathf.Max(0.15f, c.Radius) : 0.4f;
            var g = new WeaponFireGeometry
            {
                MuzzleInSocket = muzzleInSocket,
                PivotHeight = PivotHeightFraction * height,
                PivotSide = Mathf.Min(0.15f, radius * 0.5f),
                GripDistance = 0.4f,
            };
            var profile = c != null ? c.Profile : null;
            if (profile != null)
            {
                RigDims d = c.Visual != null && c.Visual.Rig != null ? c.Visual.Rig.D : CharacterModelBuilder.Measure(profile);
                Solve(d, height, radius, muzzleInSocket, ref g);
                g.FromBody = true;
            }
            g.MuzzleDistance = g.GripDistance + Mathf.Max(0.08f, muzzleInSocket.z);
            return g;
        }

        /// <summary>Pivot height (never below the shoulder), side offset (clear of the head) and a comfortable grip
        /// distance the gun arm can reach.</summary>
        static void Solve(in RigDims d, float height, float radius, Vector3 muzzle, ref WeaponFireGeometry g)
        {
            float shoulderY = d.WaistY + d.ShoulderY;
            float h = Mathf.Clamp(Mathf.Max(PivotHeightFraction * height, shoulderY + 0.06f), 0.4f, height - 0.08f);
            // shoulder -> grip, elbow slightly bent (gorilla arms bend more: the gun stays ~1.1 m in front of the body)
            float arm = Mathf.Min((d.UpperLen + d.ForeLen + d.HandR * 0.77f) * 0.92f, 0.75f);
            float maxSide = Mathf.Max(0.05f, radius - 0.02f);                 // keep the pivot inside the capsule
            float side = Mathf.Min(maxSide, d.ShoulderX * 0.6f);
            float gripY = h - muzzle.y;                                       // the barrel sits above the grip
            var hc = new Vector3(0f, d.WaistY + d.NeckY + d.HeadCenterY, d.HeadZ);
            var hh = d.HeadHalf + Vector3.one * 0.07f;                        // + gun bulk
            float gz = 0.2f;
            for (int i = 0; i < 3; i++)
            {
                float dx = side - d.ShoulderX, dy = gripY - shoulderY;
                float g2 = arm * arm - dx * dx - dy * dy;
                if (g2 < 0.04f)
                {
                    float reach = arm + MaxShoulderReach;                     // the shoulder may follow the arm a little
                    g2 = Mathf.Max(0.0225f, reach * reach - dx * dx - dy * dy);
                }
                gz = Mathf.Sqrt(g2);
                // big heads (Brainiac): the gun goes beside the cheek, not inside the face
                float ey = (gripY - hc.y) / hh.y, ez = (gz - hc.z) / hh.z, ex = side / hh.x;
                if (ex * ex + ey * ey + ez * ez >= 1f) break;
                float rest = 1f - ey * ey - ez * ez;
                float need = hh.x * Mathf.Sqrt(Mathf.Max(0f, rest)) + 0.01f;
                if (need <= side + 0.005f || side >= maxSide) break;
                side = Mathf.Min(maxSide, need);
            }
            g.PivotHeight = h;
            g.PivotSide = side;
            g.GripDistance = Mathf.Clamp(gz, 0.15f, 1.2f);
        }

        /// <summary>Aim frame for a shot from a body at feet toward target (planar forward + right).</summary>
        public static void AimFrame(Vector3 feet, Vector3 bodyForward, Vector3 target, out Vector3 forward, out Vector3 right)
        {
            Vector3 f = target - feet; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) { f = bodyForward; f.y = 0f; }
            if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
            forward = f.normalized;
            right = new Vector3(forward.z, 0f, -forward.x);   // Cross(up, forward)
        }

        /// <summary>World fire pivot for a body at feet in the given aim frame.</summary>
        public static Vector3 Pivot(in WeaponFireGeometry g, Vector3 feet, Vector3 right) => feet + Vector3.up * g.PivotHeight + right * g.PivotSide;

        /// <summary>Unit shot direction pivot -> target (falls back to forward for degenerate targets).</summary>
        public static Vector3 Direction(Vector3 pivot, Vector3 target, Vector3 forward)
        {
            Vector3 d = target - pivot;
            return d.sqrMagnitude > 1e-4f ? d.normalized : forward;
        }

        /// <summary>Weapon socket pose that puts the gun's muzzle on the shot line at MuzzleDistance, barrel along dir.</summary>
        public static void SocketPose(in WeaponFireGeometry g, Vector3 pivot, Vector3 dir, out Vector3 socketPos, out Quaternion socketRot)
        {
            socketRot = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.98f ? Vector3.forward : Vector3.up);
            socketPos = pivot + dir * g.MuzzleDistance - socketRot * g.MuzzleInSocket;
        }
    }

    /// <summary>Raises the gun arm of a CharacterRig onto the shot line. Call AFTER the CharacterAnimator wrote the
    /// bones this frame (WeaponHolder runs late - DefaultExecutionOrder). Never accumulates: every bone it touches is
    /// rewritten by the animator each frame.</summary>
    public static class WeaponArmPose
    {
        /// <summary>Blend (weight 0..1) the upper body toward aimForward and the right arm so the weapon socket reaches
        /// socketPos with socketRot. Returns false when the rig lacks the needed bones.</summary>
        public static bool Apply(CharacterRig rig, Vector3 bodyForward, Vector3 aimForward, Vector3 aimRight, Vector3 socketPos, Quaternion socketRot, float weight)
        {
            if (rig == null || rig.Spine == null || rig.ShoulderR == null || rig.ElbowR == null || rig.HandR == null || rig.WeaponSocket == null) return false;
            float w = Mathf.Clamp01(weight);
            if (w <= 0.001f) return true;

            // 1) upper-body twist toward the aim direction (legs keep walking where they walk)
            Vector3 bf = bodyForward; bf.y = 0f;
            if (bf.sqrMagnitude > 1e-4f)
            {
                float yaw = Mathf.Clamp(Vector3.SignedAngle(bf, aimForward, Vector3.up), -WeaponAim.MaxTwist, WeaponAim.MaxTwist);
                if (Mathf.Abs(yaw) > 0.05f) rig.Spine.rotation = Quaternion.AngleAxis(yaw * w, Vector3.up) * rig.Spine.rotation;
            }

            // 2) hand orientation: barrel (socket +Z) along the shot line, gun top up; wrist target from the socket offset
            Transform sh = rig.ShoulderR, el = rig.ElbowR, hand = rig.HandR, sock = rig.WeaponSocket;
            Quaternion handRot = socketRot * Quaternion.Inverse(sock.localRotation);
            Vector3 wrist = socketPos - handRot * Vector3.Scale(sock.localPosition, hand.lossyScale);

            // 3) short arms / big heads: the shoulder follows the arm a little toward an out-of-reach grip
            float upper = (el.position - sh.position).magnitude, lower = (hand.position - el.position).magnitude;
            Vector3 toWrist = wrist - sh.position;
            float excess = toWrist.magnitude - (upper + lower) * 0.985f;
            if (excess > 0f) sh.position += toWrist.normalized * (Mathf.Min(excess, WeaponAim.MaxShoulderReach) * w);

            // 4) two-bone IK shoulder -> elbow -> wrist; the elbow bends down and out
            Vector3 pole = Vector3.down * 0.8f + aimRight * 0.6f;
            TwoBone(sh, el, hand, wrist, pole, handRot, w);
            return true;
        }

        static void TwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, Quaternion endRot, float w)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            Vector3 ab = b - a, bc = c - b;
            float lab = ab.magnitude, lbc = bc.magnitude;
            if (lab < 1e-4f || lbc < 1e-4f) return;
            Vector3 at = target - a;
            float dist = at.magnitude;
            Vector3 dirT = dist > 1e-5f ? at / dist : ab / lab;
            float lat = Mathf.Clamp(dist, Mathf.Abs(lab - lbc) + 1e-3f, lab + lbc - 1e-3f);
            Vector3 bend = pole - Vector3.Dot(pole, dirT) * dirT;
            if (bend.sqrMagnitude < 1e-6f) bend = ab - Vector3.Dot(ab, dirT) * dirT;
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dirT, Vector3.right);
            bend.Normalize();
            float cosA = Mathf.Clamp((lab * lab + lat * lat - lbc * lbc) / (2f * lab * lat), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 elbowT = a + (dirT * cosA + bend * sinA) * lab;
            Vector3 wristT = a + dirT * lat;

            Quaternion rUpper = Quaternion.FromToRotation(ab, elbowT - a);
            Quaternion upperW = rUpper * upper.rotation;
            Quaternion rLower = Quaternion.FromToRotation(rUpper * bc, wristT - elbowT);
            Quaternion lowerW = rLower * rUpper * lower.rotation;
            // blend every bone in its (IK) parent space: exact at w = 1, smooth in between
            Quaternion parent = upper.parent != null ? upper.parent.rotation : Quaternion.identity;
            upper.localRotation = Quaternion.Slerp(upper.localRotation, Quaternion.Inverse(parent) * upperW, w);
            lower.localRotation = Quaternion.Slerp(lower.localRotation, Quaternion.Inverse(upperW) * lowerW, w);
            end.localRotation = Quaternion.Slerp(end.localRotation, Quaternion.Inverse(lowerW) * endRot, w);
        }
    }
}
