// =====================================================================================
//  SHIFTERS - L8Node.cs   [L8 THE FEDERAL COLOR RESERVE - hidden logic node]
//  A collider-free SignalSource that combines inputs (All / Any / Latch / Xor), drives optional
//  PuzzleLamps (red -> green) and raises a story beat (once, or on every rising edge).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L8Node : SignalSource, IResettable
    {
        public SignalSource[] Inputs = new SignalSource[0];
        public SignalLogic Logic = SignalLogic.All;
        public string BeatId = "";
        public bool BeatEveryRise;
        public string Toast = "";
        public PuzzleLamp[] Lamps = new PuzzleLamp[0];
        bool _latched, _fired, _subscribed, _evaluated;

        void OnEnable() { Subscribe(); Evaluate(); }
        void OnDisable() { Unsubscribe(); }

        public void SetInputs(params SignalSource[] inputs)
        {
            Unsubscribe();
            Inputs = inputs ?? new SignalSource[0];
            if (isActiveAndEnabled) { Subscribe(); Evaluate(); }
        }

        void Subscribe()
        {
            if (_subscribed) return;
            foreach (var s in Inputs) if (s != null) s.Changed += OnInput;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            foreach (var s in Inputs) if (s != null) s.Changed -= OnInput;
            _subscribed = false;
        }

        void OnInput(SignalSource s, bool on) { Evaluate(); }

        public int OnCount() { int n = 0; foreach (var s in Inputs) if (s != null && s.IsOn) n++; return n; }

        void Evaluate()
        {
            int count = 0, on = 0;
            foreach (var s in Inputs) { if (s == null) continue; count++; if (s.IsOn) on++; }
            bool r;
            switch (Logic)
            {
                case SignalLogic.Any: r = on > 0; break;
                case SignalLogic.Xor: r = on == 1; break;
                case SignalLogic.Latch: if (count > 0 && on == count) _latched = true; r = _latched; break;
                default: r = count > 0 && on == count; break;
            }
            bool was = IsOn;
            SetOn(r);
            foreach (var l in Lamps) if (l != null) l.Set(r);
            if (r && !was && _evaluated && Application.isPlaying)
            {
                if (!_fired || BeatEveryRise)
                {
                    _fired = true;
                    if (!string.IsNullOrEmpty(BeatId)) GameEvents.RaiseStoryBeat(BeatId);
                    if (!string.IsNullOrEmpty(Toast)) GameEvents.RaiseToast(Toast, UIColors.AccentYellow);
                }
            }
            _evaluated = true;
        }

        public void ResetState() { _latched = false; Evaluate(); }
    }
}
