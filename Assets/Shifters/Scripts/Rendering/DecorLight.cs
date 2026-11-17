// =====================================================================================
//  SHIFTERS - DecorLight.cs   (RENDERING)
//  Marker for purely decorative real-time lights (EnvKit light panels, warning-beacon spots...). The light budget
//  (LightBudget, run by LevelLighting at scene start) switches marked lights off on WebGL / the Mobile quality level /
//  the Low graphics setting; their emissive faces + bloom keep the look. Level code may add it to its own mood lights.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Marks the Light on this GameObject as decoration: the WebGL/Mobile light budget may switch it off.
    /// EnvKit.LightPanel and EnvKit.Decor.WarningBeacon add it automatically.</summary>
    [DisallowMultipleComponent]
    public class DecorLight : MonoBehaviour
    {
        [Tooltip("Keep this light on even under the WebGL/Mobile light budget (use for at most one or two hero lights).")]
        public bool KeepOnLowEnd;

        /// <summary>Adds (or returns) the marker on a light's GameObject. Null-safe.</summary>
        public static DecorLight Mark(Light light, bool keepOnLowEnd = false)
        {
            if (light == null) return null;
            var d = light.GetComponent<DecorLight>();
            if (d == null) d = light.gameObject.AddComponent<DecorLight>();
            d.KeepOnLowEnd = keepOnLowEnd;
            return d;
        }
    }
}
