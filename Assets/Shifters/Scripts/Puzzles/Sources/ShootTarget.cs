using UnityEngine;

namespace Shifters
{
    public enum ShootTargetMode { Momentary, Latch, Toggle }

    /// <summary>Bullseye remote switch (IShootable). Momentary (on for MomentarySeconds), Latch or Toggle.
    /// Weapon perspective highlights it with a "SHOOT" label.</summary>
    public class ShootTarget : SignalSource, IShootable, IResettable
    {
        public ShootTargetMode Mode = ShootTargetMode.Latch;
        public float MomentarySeconds = 4f;

        [Header("Parts")]
        public Transform Disc;
        public PuzzleLamp Lamp;
        public Revealable Reveal;

        public int Hits { get; private set; }
        float _remaining, _spin;

        public void OnShot(in ShotInfo shot)
        {
            Hits++;
            Sfx.At(SfxId.MetalHit, shot.Point);
            Sfx.At(SfxId.Bonk, shot.Point, 0.6f, 1.4f);
            Fx.Sparks(shot.Point, shot.Normal, 12, UIColors.AccentYellow);
            _spin = 1f;
            switch (Mode)
            {
                case ShootTargetMode.Momentary: _remaining = MomentarySeconds; Hit(true); break;
                case ShootTargetMode.Latch: Hit(true); break;
                default: Hit(!IsOn); break;
            }
        }

        void Hit(bool on)
        {
            if (on && !IsOn) Fx.FloatingText(transform.position + Vector3.up * 1.1f, "BULLSEYE!", UIColors.AccentYellow);
            SetOn(on);
        }

        void Update()
        {
            if (IsOn && Mode == ShootTargetMode.Momentary)
            {
                _remaining -= Time.deltaTime;
                if (_remaining <= 0f) { SetOn(false); Sfx.At(SfxId.ButtonPress, transform.position, 0.6f, 0.7f); }
            }
            if (_spin > 0f)
            {
                _spin = Mathf.Max(0f, _spin - Time.deltaTime * 1.6f);
                if (Disc != null) Disc.localRotation = Quaternion.Euler(0f, PuzzleUtil.Ease(1f - _spin) * 720f, 0f);
            }
            if (Lamp != null) Lamp.Set(IsOn);
        }

        public void ResetState() { _remaining = 0f; Hits = 0; SetOn(false); }
    }
}
