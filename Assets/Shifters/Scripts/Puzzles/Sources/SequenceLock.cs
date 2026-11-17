using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// ON (latched) once its Order sources produce rising edges in exactly that order. A wrong input resets progress
    /// with a buzz and the toast "INCORRECT. -2 IQ". Progress lamps show how far you got; IQ perspective reveals the
    /// sequence numbers floating over each input (clues built by PuzzleKit.SequenceLock).
    /// </summary>
    public class SequenceLock : SignalSource, IResettable
    {
        public SignalSource[] Order = new SignalSource[0];
        public string WrongToast = "INCORRECT. -2 IQ";

        [Header("Parts")]
        public PuzzleLamp[] ProgressLamps = new PuzzleLamp[0];
        public Revealable Reveal;

        public int Progress { get; private set; }
        public int Mistakes { get; private set; }

        readonly List<SignalSource> _subscribed = new List<SignalSource>();
        float _wrongFlash;

        void OnEnable() { Subscribe(); RefreshLamps(); }
        void OnDisable() { Unsubscribe(); }

        void Start() { if (Reveal != null) Reveal.DynamicLabel = v => "SEQUENCE LOCK\n" + Progress + "/" + Order.Length; }

        /// <summary>Replace the order (re-subscribes).</summary>
        public void SetOrder(params SignalSource[] order)
        {
            Unsubscribe();
            Order = order ?? new SignalSource[0];
            Progress = 0;
            if (isActiveAndEnabled) Subscribe();
            RefreshLamps();
        }

        void Subscribe()
        {
            foreach (var s in Order)
                if (s != null && !_subscribed.Contains(s)) { s.Changed += OnInput; _subscribed.Add(s); }
        }

        void Unsubscribe()
        {
            foreach (var s in _subscribed) if (s != null) s.Changed -= OnInput;
            _subscribed.Clear();
        }

        void OnInput(SignalSource s, bool on)
        {
            if (!on || IsOn || Order.Length == 0) return;
            if (Progress < Order.Length && Order[Progress] == s)
            {
                Progress++;
                Sfx.At(SfxId.ButtonPress, transform.position, 0.8f, 1f + 0.15f * Progress);
                if (Progress >= Order.Length)
                {
                    Sfx.At(SfxId.PuzzleSolved, transform.position);
                    Fx.Confetti(transform.position + Vector3.up * 1.5f);
                    SetOn(true);
                }
            }
            else Wrong();
            RefreshLamps();
        }

        void Wrong()
        {
            Progress = 0;
            Mistakes++;
            _wrongFlash = 0.8f;
            Sfx.At(SfxId.Denied, transform.position);
            Sfx.At(SfxId.HackFail, transform.position, 0.7f);
            GameEvents.RaiseToast(WrongToast, UIColors.Danger);
            Fx.FloatingText(transform.position + Vector3.up * 1.8f, "WRONG ORDER!", UIColors.Danger);
        }

        void Update()
        {
            if (_wrongFlash > 0f)
            {
                _wrongFlash -= Time.deltaTime;
                for (int i = 0; i < ProgressLamps.Length; i++) if (ProgressLamps[i] != null) ProgressLamps[i].Blink(PuzzleLampColor.Red, PuzzleLampColor.Off, 8f);
                if (_wrongFlash <= 0f) RefreshLamps();
            }
        }

        void RefreshLamps()
        {
            for (int i = 0; i < ProgressLamps.Length; i++)
                if (ProgressLamps[i] != null) ProgressLamps[i].Set(IsOn ? PuzzleLampColor.Green : i < Progress ? PuzzleLampColor.Blue : PuzzleLampColor.Off);
        }

        public void ResetState() { Progress = 0; SetOn(false); RefreshLamps(); }
    }
}
