using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// A source that combines other sources with a SignalLogic (All=AND, Any=OR, Xor, Toggle, Latch) and an optional
    /// Not (inverts the result: NAND / NOR / NOT). Small visible logic box with its symbol and input/output lamps.
    /// Use SetInputs() when wiring at runtime (it re-subscribes).
    /// </summary>
    public class LogicGate : SignalSource, IResettable
    {
        public SignalSource[] Inputs = new SignalSource[0];
        public SignalLogic Logic = SignalLogic.All;
        public bool Not;

        [Header("Parts")]
        public PuzzleLamp OutputLamp;
        public PuzzleLamp[] InputLamps = new PuzzleLamp[0];
        public TextMeshPro Symbol;
        [Tooltip("Optional second symbol on the back face (readable from both sides).")] public TextMeshPro SymbolBack;
        public Revealable Reveal;

        bool _toggle, _latched, _subscribed;

        void OnEnable() { Subscribe(); Evaluate(); }
        void OnDisable() { Unsubscribe(); }

        void Start() { if (Reveal != null) Reveal.DynamicLabel = v => "LOGIC: " + SymbolFor(Logic, Not, Inputs.Length) + "\n" + OnCount() + "/" + Inputs.Length + " INPUTS ON"; }

        /// <summary>Replace the inputs (safe at runtime and in edit mode).</summary>
        public void SetInputs(params SignalSource[] inputs)
        {
            Unsubscribe();
            Inputs = inputs ?? new SignalSource[0];
            if (Symbol != null) Symbol.text = SymbolFor(Logic, Not, Inputs.Length);
            if (SymbolBack != null) SymbolBack.text = SymbolFor(Logic, Not, Inputs.Length);
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

        void OnInput(SignalSource s, bool on)
        {
            if (Logic == SignalLogic.Toggle && on) _toggle = !_toggle;
            Evaluate();
        }

        int OnCount() { int n = 0; foreach (var s in Inputs) if (s != null && s.IsOn) n++; return n; }

        void Evaluate()
        {
            int count = 0, on = 0;
            foreach (var s in Inputs) { if (s == null) continue; count++; if (s.IsOn) on++; }
            bool r;
            switch (Logic)
            {
                case SignalLogic.Any: r = on > 0; break;
                case SignalLogic.Xor: r = on == 1; break;
                case SignalLogic.Toggle: r = _toggle; break;
                case SignalLogic.Latch: if (count > 0 && on == count) _latched = true; r = _latched; break;
                default: r = count > 0 && on == count; break;
            }
            if (Not) r = !r;
            SetOn(r);
            if (OutputLamp != null) OutputLamp.Set(r);
            for (int i = 0; i < InputLamps.Length; i++)
                if (InputLamps[i] != null) InputLamps[i].Set(i < Inputs.Length && Inputs[i] != null && Inputs[i].IsOn ? PuzzleLampColor.Blue : PuzzleLampColor.Off);
        }

        public static string SymbolFor(SignalLogic logic, bool not, int inputs)
        {
            string s;
            switch (logic)
            {
                case SignalLogic.Any: s = "OR"; break;
                case SignalLogic.Xor: s = "XOR"; break;
                case SignalLogic.Toggle: s = "FLIP"; break;
                case SignalLogic.Latch: s = "LATCH"; break;
                default: s = "AND"; break;
            }
            if (!not) return s;
            return inputs <= 1 ? "NOT" : "N" + s;
        }

        public void ResetState() { _toggle = false; _latched = false; Evaluate(); }
    }
}
