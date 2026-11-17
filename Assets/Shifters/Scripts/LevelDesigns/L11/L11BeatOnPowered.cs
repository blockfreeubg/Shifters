using UnityEngine;

namespace Shifters
{
    /// <summary>SHIFT 10: raises a story beat (once per level load) the first time a receiver is Powered or a source
    /// turns ON (e.g. the huddle gate -> "ch10.gate"), with an optional sfx + floating text at the object.</summary>
    public sealed class L11BeatOnPowered : MonoBehaviour
    {
        public SignalReceiver Receiver;
        public SignalSource Source;
        public string BeatId = "";
        public string FloatText = "";
        public bool PlayChime = true;
        public bool Fired { get; private set; }

        void Update()
        {
            if (Fired) return;
            bool on = (Receiver != null && Receiver.Powered) || (Source != null && Source.IsOn);
            if (!on) return;
            Fired = true;
            if (!string.IsNullOrEmpty(BeatId)) GameEvents.RaiseStoryBeat(BeatId);
            if (PlayChime) Sfx.At(SfxId.Creak, transform.position);
            if (!string.IsNullOrEmpty(FloatText)) Fx.FloatingText(transform.position + Vector3.up * 3.2f, FloatText, L11Kit.GhostBlue, 1.2f);
        }
    }
}
