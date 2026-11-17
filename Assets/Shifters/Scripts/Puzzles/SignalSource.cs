// =====================================================================================
//  SHIFTERS - SignalSource.cs   [CONTRACT FILE - puzzle wiring base classes]
//  Puzzle elements are wired as SOURCES (plates, levers, buttons, terminals, shoot targets,
//  logic gates, sequence locks, timers...) feeding RECEIVERS (doors, platforms, lifts, bridges,
//  hazards, lamps...). Receivers combine their inputs with a SignalLogic.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public abstract class SignalSource : MonoBehaviour
    {
        /// <summary>Optional banner text. On the first ON it raises GameEvents.PuzzleSolved(msg) and then
        /// GameEvents.PuzzleSolvedAt(msg, transform.position) (campaign wave).</summary>
        [Tooltip("Optional: shown in the 'PUZZLE SOLVED' banner when this source first turns on.")]
        public string SolvedMessage = "";
        public bool IsOn { get; private set; }
        public event Action<SignalSource, bool> Changed;
        bool _announced;

        protected void SetOn(bool on)
        {
            if (IsOn == on) return;
            IsOn = on;
            Changed?.Invoke(this, on);
            if (on && !_announced && !string.IsNullOrEmpty(SolvedMessage))
            {
                _announced = true;
                GameEvents.RaisePuzzleSolved(SolvedMessage);
                GameEvents.RaisePuzzleSolvedAt(SolvedMessage, transform.position);   // campaign: world-color burst here
            }
        }
    }

    public enum SignalLogic
    {
        All,     // powered when every input is on (AND)
        Any,     // powered when at least one input is on (OR)
        Toggle,  // each rising edge of any input flips power
        Latch,   // like All, but stays powered forever once powered
        Xor      // powered when exactly one input is on
    }

    public abstract class SignalReceiver : MonoBehaviour
    {
        public SignalSource[] Inputs = new SignalSource[0];
        public SignalLogic Logic = SignalLogic.All;
        [Tooltip("Invert the final result (e.g. a door that is open until a plate is pressed).")] public bool Invert;
        [Tooltip("Seconds to stay powered after the logic turns off.")] public float OffDelay;
        [Tooltip("Powered at start when there are no inputs (e.g. always-moving platforms).")] public bool PoweredWithoutInputs;

        public bool Powered { get; private set; }
        bool _latched, _toggle, _initialized;
        float _offTimer = -1f;

        protected virtual void OnEnable()
        {
            foreach (var s in Inputs) if (s != null) s.Changed += OnInputChanged;
            Evaluate(true);
        }

        protected virtual void OnDisable()
        {
            foreach (var s in Inputs) if (s != null) s.Changed -= OnInputChanged;
        }

        void OnInputChanged(SignalSource src, bool on)
        {
            if (Logic == SignalLogic.Toggle && on) _toggle = !_toggle;
            Evaluate(false);
        }

        protected virtual void Update()
        {
            if (_offTimer >= 0f)
            {
                _offTimer -= Time.deltaTime;
                if (_offTimer < 0f) SetPowered(ComputeLogic(), false);
            }
        }

        bool ComputeLogic()
        {
            bool result;
            int count = 0, on = 0;
            foreach (var s in Inputs) { if (s == null) continue; count++; if (s.IsOn) on++; }
            if (count == 0) result = PoweredWithoutInputs;
            else switch (Logic)
            {
                case SignalLogic.Any: result = on > 0; break;
                case SignalLogic.Toggle: result = _toggle; break;
                case SignalLogic.Xor: result = on == 1; break;
                case SignalLogic.Latch: if (on == count) _latched = true; result = _latched; break;
                default: result = on == count; break;
            }
            return Invert ? !result : result;
        }

        void Evaluate(bool initial)
        {
            bool target = ComputeLogic();
            if (!target && Powered && OffDelay > 0f && !initial)
            {
                if (_offTimer < 0f) _offTimer = OffDelay;
                return;
            }
            _offTimer = -1f;
            SetPowered(target, initial);
        }

        void SetPowered(bool p, bool initial)
        {
            if (_initialized && Powered == p) return;
            _initialized = true;
            Powered = p;
            OnPowerChanged(p, initial);
        }

        /// <summary>Force a power state (cutscenes, tests). Logic resumes on the next input change.</summary>
        public void ForcePower(bool p) { _offTimer = -1f; SetPowered(p, false); }

        /// <summary>initial = true during OnEnable (snap to state without animation/sound).</summary>
        protected abstract void OnPowerChanged(bool powered, bool initial);
    }
}
