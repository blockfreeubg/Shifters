// =====================================================================================
//  SHIFTERS - GhostTrioView.cs   [owned by STORY_KIT]   (StoryKit.GhostTrio runtime): gentle bob + sway, out of phase.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class GhostTrioView : MonoBehaviour
    {
        public Transform[] Ghosts;
        public float BobHeight = 0.12f, BobSpeed = 1.6f, SwayDegrees = 6f;
        Vector3[] _base;
        Quaternion[] _baseRot;

        void OnEnable()
        {
            if (Ghosts == null) return;
            _base = new Vector3[Ghosts.Length];
            _baseRot = new Quaternion[Ghosts.Length];
            for (int i = 0; i < Ghosts.Length; i++) if (Ghosts[i] != null) { _base[i] = Ghosts[i].localPosition; _baseRot[i] = Ghosts[i].localRotation; }
        }

        void OnDisable()
        {
            if (Ghosts == null || _base == null) return;
            for (int i = 0; i < Ghosts.Length; i++) if (Ghosts[i] != null) { Ghosts[i].localPosition = _base[i]; Ghosts[i].localRotation = _baseRot[i]; }
        }

        void Update()
        {
            if (Ghosts == null || _base == null) return;
            float t = Time.time;
            for (int i = 0; i < Ghosts.Length; i++)
            {
                var g = Ghosts[i];
                if (g == null) continue;
                float ph = t * BobSpeed + i * 2.1f;
                g.localPosition = _base[i] + new Vector3(0f, Mathf.Sin(ph) * BobHeight, 0f);
                g.localRotation = _baseRot[i] * Quaternion.Euler(0f, 0f, Mathf.Sin(ph * 0.7f) * SwayDegrees);
            }
        }
    }
}
