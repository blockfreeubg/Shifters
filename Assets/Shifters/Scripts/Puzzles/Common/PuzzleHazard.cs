using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Base for hazards (lasers, electric floors, steam vents, spikes, crushers, sweepers).
    /// A hazard is ACTIVE when it is powered (no inputs + PoweredWithoutInputs = always; or via Inputs/Logic/Invert
    /// like any receiver) AND not disabled by a HazardToggle, AND - if CycleOn/CycleOff are both > 0 - inside the
    /// "on" part of its cycle. IsWarning is true during the last WarningTime seconds before turning on.
    /// </summary>
    public abstract class PuzzleHazard : SignalReceiver, IFreezable
    {
        [Header("Hazard cycle (both > 0 to cycle)")]
        public float CycleOn = 0f;
        public float CycleOff = 0f;
        [Tooltip("Seconds into the cycle at start (stagger several hazards).")] public float Phase = 0f;
        public float WarningTime = 0.6f;

        public bool IsActive { get; private set; }
        public bool IsWarning { get; private set; }
        public bool Cycles => CycleOn > 0f && CycleOff > 0f;
        /// <summary>Seconds until the next on/off change (cycling hazards), else -1.</summary>
        public float TimeToSwitch
        {
            get
            {
                if (!Cycles || !_powered || !_toggleEnabled) return -1f;
                float t = Mathf.Repeat(_t, CycleOn + CycleOff);
                return t < CycleOn ? CycleOn - t : CycleOn + CycleOff - t;
            }
        }

        bool _powered, _toggleEnabled = true, _stateInit;
        float _t;

        protected virtual void Reset() { PoweredWithoutInputs = true; }

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            _powered = powered;
            if (powered) _t = Phase;
            Refresh(initial);
        }

        /// <summary>Called by HazardToggle (false = forced off regardless of power).</summary>
        public void SetToggleEnabled(bool on)
        {
            _toggleEnabled = on;
            Refresh(false);
        }

        protected override void Update()
        {
            base.Update();
            if (IsFrozen) { HazardUpdate(); return; }   // CAPS-LOCK: the cycle and the state stay put; damage still applies
            if (_powered && _toggleEnabled && Cycles) _t += Time.deltaTime;
            Refresh(false);
            HazardUpdate();
        }

        // ------------------------------------------------------------------ IFreezable (CAPS-LOCK cannon)
        float _frozenUntil = -1f;
        public bool IsFrozen => Time.time < _frozenUntil;
        public int FreezeCount { get; private set; }

        /// <summary>Lock the hazard for 'seconds' (re-freezing extends to the later end). Cycles pause in their current
        /// state; moving hazards (crusher, sweeper) stop where they are.</summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;
            bool was = IsFrozen;
            _frozenUntil = Mathf.Max(_frozenUntil, Time.time + seconds);
            FreezeCount++;
            if (!was)
            {
                Fx.FloatingText(transform.position + Vector3.up * 2f, "CAPS LOCK", UIColors.Info, 1.2f);
                Sfx.At(SfxId.CapsLock, transform.position);
            }
        }

        void Refresh(bool initial)
        {
            bool en = _powered && _toggleEnabled;
            bool active = en, warn = false;
            if (en && Cycles)
            {
                float period = CycleOn + CycleOff;
                float t = Mathf.Repeat(_t, period);
                active = t < CycleOn;
                warn = !active && t > period - WarningTime;
            }
            if (_stateInit && active == IsActive && warn == IsWarning) return;
            bool changed = !_stateInit || active != IsActive;
            IsActive = active;
            IsWarning = warn;
            _stateInit = true;
            OnHazardState(active, warn, initial || !changed);
        }

        /// <summary>State changed. silent = initial snap or only the warning flag changed.</summary>
        protected abstract void OnHazardState(bool active, bool warning, bool silent);
        /// <summary>Per-frame hazard logic (after the state refresh).</summary>
        protected virtual void HazardUpdate() { }
    }
}
