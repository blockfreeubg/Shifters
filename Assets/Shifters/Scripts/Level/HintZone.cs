// =====================================================================================
//  SHIFTERS - HintZone.cs   [owned by the LEVEL agent]
//  Volume around a puzzle holding a real hint (BIG BRAIN, IQ >= 7) and a vague hint (IQ 4-6).
//  IQ <= 3 gets a joke instead. Overlapping zones: higher Priority wins, then the smaller zone.
//  Build with LevelKit.HintZone.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class HintZone : MonoBehaviour
    {
        public static readonly List<HintZone> All = new List<HintZone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [TextArea] public string RealHint = "";
        [TextArea] public string VagueHint = "";
        public Vector3 ZoneCenter = Vector3.zero;
        public Vector3 ZoneSize = new Vector3(8f, 4f, 8f);
        public int Priority;

        public float Volume
        {
            get { var s = Vector3.Scale(ZoneSize, transform.lossyScale); return Mathf.Abs(s.x * s.y * s.z); }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Contains(Vector3 worldPoint) => ZoneMath.Contains(transform, ZoneCenter, ZoneSize, worldPoint);

        void OnDrawGizmos() => ZoneMath.DrawGizmo(transform, ZoneCenter, ZoneSize, UIColors.Intellect);
    }
}
