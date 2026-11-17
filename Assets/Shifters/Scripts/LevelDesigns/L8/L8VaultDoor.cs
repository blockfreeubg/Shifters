// =====================================================================================
//  SHIFTERS - L8VaultDoor.cs   [L8 - the three-lock vault door]
//  Receiver: when powered (AND of the three lock latches) the decor vault door swings INTO the
//  vestibule (hinge +OpenDegrees, never closes again), the wheel spins, a gold glow turns on, coins
//  fly and the "AAAH" plays. Raises BeatId once.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L8VaultDoor : SignalReceiver
    {
        public Transform Hinge;
        public Transform Wheel;
        public float OpenDegrees = 92f;
        public float Seconds = 2.4f;
        public GameObject Glow;
        public string BeatId = "ch07.door";
        public bool IsOpen { get; private set; }
        public float Progress01 => _t;
        float _t;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (!powered || IsOpen || !Application.isPlaying) return;
            IsOpen = true;
            if (Glow != null) Glow.SetActive(true);
            Vector3 c = Hinge != null ? Hinge.position : transform.position;
            Sfx.At(SfxId.VaultSpin, transform.position, 1f);
            Sfx.At(SfxId.VaultOpen, transform.position, 1f);
            Fx.CoinShower(transform.position + Vector3.up * 2f, 60, 2f);
            Fx.FloatingText(transform.position + new Vector3(0f, 5f, -1f), "AAAAAAH!", UIColors.AccentYellow);
            if (!string.IsNullOrEmpty(BeatId)) GameEvents.RaiseStoryBeat(BeatId);
            GameEvents.RaisePuzzleSolvedAt("VAULT OPEN", transform.position);
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen || _t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.1f, Seconds));
            float e = _t < 0.25f ? 0f : Mathf.SmoothStep(0f, 1f, (_t - 0.25f) / 0.75f);   // wheel spins first, then the swing
            if (Wheel != null) Wheel.localRotation = Quaternion.Euler(0f, 0f, _t * 540f);
            if (Hinge != null) Hinge.localRotation = Quaternion.Euler(0f, OpenDegrees * e, 0f);
        }
    }
}
