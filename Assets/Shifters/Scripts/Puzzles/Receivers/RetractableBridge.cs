using UnityEngine;

namespace Shifters
{
    /// <summary>Walkway that extends Travel meters along local +Z when powered and retracts into its housing when not.</summary>
    public class RetractableBridge : SlideMover
    {
        protected override void OnStartMoving(bool extending) => Sfx.At(extending ? SfxId.DoorOpen : SfxId.DoorClose, transform.position, 0.7f, 1.3f);
        protected override void OnArrived(bool extended) { if (extended) Sfx.At(SfxId.PlatePress, WorldAt(1f), 0.6f, 0.8f); }
    }
}
