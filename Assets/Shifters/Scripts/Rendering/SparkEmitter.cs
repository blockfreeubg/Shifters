// =====================================================================================
//  SHIFTERS - SparkEmitter.cs   (RENDERING)
//  Spits pooled Fx.Sparks at random intervals (Chaos sparking panels, broken cables). Own file named after the
//  class so scene/prefab-saved instances bind (see SlowSpin.cs). Play mode only, no per-frame allocations.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Spits sparks at random intervals (Chaos sparking panels, broken cables).</summary>
    public class SparkEmitter : MonoBehaviour
    {
        public Vector3 LocalPoint;
        public Vector3 LocalNormal = Vector3.forward;
        public float MinInterval = 0.4f, MaxInterval = 2.2f;
        public Color Color = new Color(1f, 0.8f, 0.35f, 1f);
        [Tooltip("Optional tiny light blinked with each burst (leave empty on WebGL-heavy scenes).")]
        public Light Flash;
        float _next, _flashEnd;

        void OnEnable() => _next = Time.time + Random.Range(MinInterval, MaxInterval);

        void Update()
        {
            if (Time.time >= _next)
            {
                _next = Time.time + Random.Range(MinInterval, MaxInterval);
                Fx.Sparks(transform.TransformPoint(LocalPoint), transform.TransformDirection(LocalNormal), Random.Range(6, 16), Color);
                if (Flash != null) { Flash.enabled = true; _flashEnd = Time.time + 0.06f; }
            }
            if (Flash != null && Flash.enabled && Time.time >= _flashEnd) Flash.enabled = false;
        }
    }
}
