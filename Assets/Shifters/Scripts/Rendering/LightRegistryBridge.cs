// =====================================================================================
//  SHIFTERS - LightRegistryBridge.cs   (RENDERING)
//  Null-safe link from rendering's light cones (FlashlightRig) to puzzles' LightRegistry (CAMPAIGN 18.5).
//  puzzles published LightRegistry at v1, so this now calls it directly (kept as a seam for rendering callers).
// =====================================================================================
namespace Shifters
{
    public static class LightRegistryBridge
    {
        /// <summary>True when puzzles' LightRegistry is present (always, since puzzles v1).</summary>
        public static bool Available => true;

        public static void RegisterCone(ILightCone c) { if (c != null) LightRegistry.RegisterCone(c); }
        public static void UnregisterCone(ILightCone c) { if (c != null) LightRegistry.UnregisterCone(c); }
    }
}
