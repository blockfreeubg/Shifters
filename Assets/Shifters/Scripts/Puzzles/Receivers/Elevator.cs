using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Platform that rises Travel meters when powered and returns down when unpowered (guide rails + lamp).
    /// Campaign: every enabled elevator is listed in <see cref="All"/> (LightRegistry counts cars as lit).</summary>
    public class Elevator : SlideMover
    {
        public static readonly List<Elevator> All = new List<Elevator>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        protected override void OnEnable() { base.OnEnable(); if (!All.Contains(this)) All.Add(this); }
        protected override void OnDisable() { base.OnDisable(); All.Remove(this); }

        protected override void OnStartMoving(bool up) => Sfx.At(SfxId.ElevatorHum, transform.position, 0.8f, up ? 1f : 0.85f);
        protected override void OnArrived(bool top) => Sfx.At(SfxId.PlateRelease, Body != null ? Body.transform.position : transform.position, 0.7f, 0.8f);
    }
}
