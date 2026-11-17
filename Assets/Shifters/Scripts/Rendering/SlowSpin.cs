// =====================================================================================
//  SHIFTERS - SlowSpin.cs   (RENDERING)
//  Slow rotation + optional vertical bob for EnvKit decor (floating research shapes, gears, beacon heads).
//  One MonoBehaviour per file, file named after the class: Unity binds a component saved in a scene/prefab to
//  the script asset whose FILE name matches the class (the old EnvComponents.cs held four classes and every
//  saved instance became a "missing script"). Configured through public fields by the factories; play mode
//  only, no per-frame allocations.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Slow rotation + optional vertical bob (floating research shapes, gears, beacons).</summary>
    public class SlowSpin : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;
        public float DegreesPerSecond = 20f;
        public float BobAmplitude;
        public float BobFrequency = 0.25f;
        public float Phase;
        Vector3 _base;

        void OnEnable() => _base = transform.localPosition;

        void Update()
        {
            transform.Rotate(Axis, DegreesPerSecond * Time.deltaTime, Space.Self);
            if (BobAmplitude > 0f)
                transform.localPosition = _base + Vector3.up * (Mathf.Sin((Time.time * BobFrequency + Phase) * Mathf.PI * 2f) * BobAmplitude);
        }
    }
}
