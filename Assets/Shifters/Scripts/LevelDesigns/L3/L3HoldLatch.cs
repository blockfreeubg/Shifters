// =====================================================================================
//  SHIFTERS - L3HoldLatch.cs   [LEVEL 3 - Sacrifice Zone]
//  "SECURITY LATCH": a signal source that turns ON for good once its Input has stayed ON
//  for HoldSeconds without interruption (plate held 1 s / 3 s). Shows a live countdown on a
//  small display and fills a row of lamps, ticking faster as it gets close.
//  Built by L3Kit.HoldLatch (edit-mode safe: everything is wired through public fields).
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class L3HoldLatch : SignalSource, IResettable
    {
        [Tooltip("Source that must stay on (usually a non-latching PressurePlate).")] public SignalSource Input;
        public float HoldSeconds = 1f;
        [Tooltip("Optional live display ('HOLD 2.1 S' / 'LATCHED').")] public TextMeshPro Display;
        [Tooltip("Lamps that fill up while the input is held.")] public PuzzleLamp[] Lamps = new PuzzleLamp[0];
        public string IdleText = "HOLD 1 S";

        /// <summary>Seconds the input has been held continuously (resets when it lets go before latching).</summary>
        public float Held { get; private set; }
        public float Progress01 => IsOn ? 1f : (HoldSeconds > 0f ? Mathf.Clamp01(Held / HoldSeconds) : 0f);

        float _tick;
        int _shownLit = -1;
        string _shownText;

        void Start() => Refresh(true);

        void Update()
        {
            if (!IsOn)
            {
                bool held = Input != null && Input.IsOn;
                if (held)
                {
                    Held += Time.deltaTime;
                    _tick -= Time.deltaTime;
                    if (_tick <= 0f)
                    {
                        _tick = Mathf.Lerp(0.45f, 0.12f, Progress01);
                        Sfx.At(SfxId.TimerTick, transform.position, 0.6f, Mathf.Lerp(1f, 1.6f, Progress01));
                    }
                    if (Held >= HoldSeconds) Latch();
                }
                else if (Held > 0f)
                {
                    Held = 0f;
                    _tick = 0f;
                    Sfx.At(SfxId.PlateRelease, transform.position, 0.6f, 0.8f);
                }
            }
            Refresh(false);
        }

        void Latch()
        {
            Held = HoldSeconds;
            Sfx.At(SfxId.PuzzleSolved, transform.position, 0.8f);
            Sfx.At(SfxId.DoorSlam, transform.position, 0.5f, 1.4f);
            Fx.Confetti(transform.position + Vector3.up * 2.2f);
            Fx.FloatingText(transform.position + Vector3.up * 2.6f, "LATCHED!", UIColors.Success, 1.4f);
            SetOn(true);
        }

        void Refresh(bool force)
        {
            int n = Lamps != null ? Lamps.Length : 0;
            int lit = IsOn ? n : Mathf.FloorToInt(Progress01 * n + 0.001f);
            if (force || lit != _shownLit || IsOn)
            {
                for (int i = 0; i < n; i++)
                {
                    if (Lamps[i] == null) continue;
                    if (IsOn) Lamps[i].Set(PuzzleLampColor.Green);
                    else Lamps[i].Set(i < lit ? PuzzleLampColor.Yellow : PuzzleLampColor.Off);
                }
                _shownLit = lit;
            }
            if (Display == null) return;
            string txt = IsOn ? "LATCHED" : Held > 0f ? "HOLD " + Mathf.Max(0f, HoldSeconds - Held).ToString("0.0") + " S" : IdleText;
            if (force || txt != _shownText)
            {
                _shownText = txt;
                Display.text = txt;
                Display.color = IsOn ? UIColors.Success : Held > 0f ? UIColors.AccentYellow : UIColors.Text;
            }
        }

        public void ResetState()
        {
            Held = 0f;
            SetOn(false);
            Refresh(true);
        }
    }
}
