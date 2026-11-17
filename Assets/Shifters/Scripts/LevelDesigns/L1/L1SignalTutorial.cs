// =====================================================================================
//  SHIFTERS - L1SignalTutorial.cs   (LEVEL 1)
//  Shows a tutorial popup the first time its inputs power it ("after the lever", "when the vent opens").
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Receiver that queues <see cref="Message"/> once, Delay seconds after it first becomes powered.</summary>
    public class L1SignalTutorial : SignalReceiver
    {
        public TutorialMessage Message = new TutorialMessage();
        public float Delay = 0.8f;

        public bool Fired { get; private set; }
        float _countdown = -1f;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (!powered || Fired || initial || _countdown >= 0f) return;
            _countdown = Delay;
        }

        protected override void Update()
        {
            base.Update();
            if (_countdown < 0f || Fired) return;
            _countdown -= Time.deltaTime;
            if (_countdown > 0f) return;
            Fired = true;
            _countdown = -1f;
            TutorialManager.Ensure().Show(Message);
        }
    }
}
