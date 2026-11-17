using UnityEngine;

namespace Shifters
{
    /// <summary>Status lamp receiver: green when powered, red when not (optionally blinking while powered).</summary>
    public class IndicatorLight : SignalReceiver
    {
        public PuzzleLamp Lamp;
        public bool BlinkWhenPowered;
        public PuzzleLampColor OnColor = PuzzleLampColor.Green;
        public PuzzleLampColor OffColor = PuzzleLampColor.Red;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (Lamp != null) Lamp.Set(powered ? OnColor : OffColor);
        }

        protected override void Update()
        {
            base.Update();
            if (BlinkWhenPowered && Powered && Lamp != null) Lamp.Blink(OnColor, PuzzleLampColor.Off, 3f);
        }
    }
}
