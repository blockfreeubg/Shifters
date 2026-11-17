// =====================================================================================
//  SHIFTERS - BossSafeZone.cs   [BOSSES module]
//  Marks a BREAK AREA (B6): an oriented rectangle on the floor (Size in local X/Z, >= 7 x 5 m) that no
//  boss attack ever covers. Built by BossKit.BreakArea; BossArena.SafeZones lists the transforms.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class BossSafeZone : MonoBehaviour
    {
        [Tooltip("Width (local X) x depth (local Z) in meters.")] public Vector2 Size = new Vector2(7f, 5f);
        [Tooltip("Height of the protected volume above the floor.")] public float Height = 4f;

        public Vector3 Center => transform.position;
        public Quaternion Rotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        public bool Contains(Vector3 p, float margin = 0f)
        {
            float dy = p.y - transform.position.y;
            if (dy < -1.5f || dy > Height) return false;
            var l = Quaternion.Inverse(Rotation) * (p - transform.position);
            return Mathf.Abs(l.x) <= Size.x * 0.5f + margin && Mathf.Abs(l.z) <= Size.y * 0.5f + margin;
        }

        public float Distance(Vector3 p) => BossFootprint.RectDistance(p, Center, Rotation, Size);

        /// <summary>Point inside the zone for team slot i (three standing spots, 1.5 m apart).</summary>
        public Vector3 SpotFor(int slot)
        {
            float x = (Mathf.Clamp(slot, 0, 2) - 1) * Mathf.Min(1.5f, Size.x * 0.25f);
            return transform.position + Rotation * new Vector3(x, 0f, 0f);
        }
    }
}
