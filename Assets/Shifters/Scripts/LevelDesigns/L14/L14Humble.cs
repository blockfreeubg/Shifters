// =====================================================================================
//  SHIFTERS - L14Humble.cs   [LEVEL 14 - "HUMBLE" on the railing post: any shot tips it off ("OBJECTION? (SHOOT)")]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L14Humble : MonoBehaviour, IShootable
    {
        public void OnShot(in ShotInfo shot) { L14Hearing.Instance?.ShootHumble(); }
    }
}
