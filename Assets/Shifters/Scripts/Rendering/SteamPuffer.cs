// =====================================================================================
//  SHIFTERS - SteamPuffer.cs   (RENDERING)
//  Periodic rising pooled Fx.Steam puffs (factory steam stacks, acid fumes). Own file named after the class so
//  scene/prefab-saved instances bind (see SlowSpin.cs). Play mode only, no per-frame allocations.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Periodic rising puffs (factory steam stacks, acid fumes).</summary>
    public class SteamPuffer : MonoBehaviour
    {
        public Vector3 LocalPoint;
        public float Interval = 0.22f;
        public float Size = 1f;
        public Color Color = new Color(0.95f, 0.95f, 0.97f, 0.5f);
        float _next;

        void OnEnable() => _next = Time.time + Random.value * Interval;

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + Interval * Random.Range(0.7f, 1.3f);
            Fx.Steam(transform.TransformPoint(LocalPoint), transform.up, Size, Color);
        }
    }
}
