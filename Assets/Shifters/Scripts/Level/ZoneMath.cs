// =====================================================================================
//  SHIFTERS - ZoneMath.cs   [owned by the LEVEL agent]
//  Oriented-box containment used by every level zone (checkpoints, exit, tutorials, hints).
//  Zones are polled geometrically instead of relying on trigger enter/exit, so teleports,
//  carried bodies, disabled colliders and multi-collider characters never miscount.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class ZoneMath
    {
        /// <summary>True if worldPoint lies inside the box (center/size in t's local space).</summary>
        public static bool Contains(Transform t, Vector3 localCenter, Vector3 localSize, Vector3 worldPoint)
        {
            var p = t.InverseTransformPoint(worldPoint) - localCenter;
            return Mathf.Abs(p.x) <= localSize.x * 0.5f && Mathf.Abs(p.y) <= localSize.y * 0.5f && Mathf.Abs(p.z) <= localSize.z * 0.5f;
        }

        /// <summary>Character counts as inside when its body center or its feet (+0.3 m) are inside.</summary>
        public static bool ContainsCharacter(Transform t, Vector3 localCenter, Vector3 localSize, ShifterCharacter c)
        {
            if (c == null) return false;
            return Contains(t, localCenter, localSize, c.Center) || Contains(t, localCenter, localSize, c.Feet + Vector3.up * 0.3f);
        }

        public static void DrawGizmo(Transform t, Vector3 localCenter, Vector3 localSize, Color color)
        {
            Gizmos.matrix = t.localToWorldMatrix;
            Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
            Gizmos.DrawCube(localCenter, localSize);
            Gizmos.color = color;
            Gizmos.DrawWireCube(localCenter, localSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
