// =====================================================================================
//  SHIFTERS - HoldLatch.cs   [PUZZLES - campaign wave; promoted copy of L3HoldLatch + L4SecurityLatch]
//  HoldThenLatch (L3): OFF until Input stayed ON for HoldSeconds without interruption, then ON forever.
//  OpenThenLatch (L4): passes Input straight through, and latches ON forever after HoldSeconds of continuous ON
//  (its SolvedMessage is announced at the latch, not at the first pass-through).
//  Display on a post: title, live "HOLD 2.1 S" / "ARMING 1.4 S" / "LATCHED", 5 lamps filling up.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class HoldLatch : SignalSource, IResettable
    {
        [Tooltip("Source that must stay on (usually a non-latching PressurePlate).")] public SignalSource Input;
        public float HoldSeconds = 1f;
        public HoldLatchMode Mode = HoldLatchMode.HoldThenLatch;
        public TextMeshPro Display;
        public PuzzleLamp[] Lamps = new PuzzleLamp[0];

        public bool IsLatched { get; private set; }
        /// <summary>Seconds the input has been held continuously.</summary>
        public float Held { get; private set; }
        public float Progress01 => IsLatched ? 1f : (HoldSeconds > 0f ? Mathf.Clamp01(Held / HoldSeconds) : 0f);

        float _tick;
        int _shownLit = -1, _shownKey = int.MinValue;
        string _latchMessage;
        bool _msgMoved;

        void Start()
        {
            if (Mode == HoldLatchMode.OpenThenLatch && !_msgMoved)
            {
                _msgMoved = true;
                _latchMessage = SolvedMessage;
                SolvedMessage = "";
            }
            Refresh(true);
        }

        void Update()
        {
            if (!IsLatched)
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
                    if (Held > 0.15f) Sfx.At(Mode == HoldLatchMode.OpenThenLatch ? SfxId.Denied : SfxId.PlateRelease, transform.position, 0.6f, 0.8f);
                    Held = 0f;
                    _tick = 0f;
                }
                if (!IsLatched && Mode == HoldLatchMode.OpenThenLatch) SetOn(held);
            }
            Refresh(false);
        }

        void Latch()
        {
            IsLatched = true;
            Held = HoldSeconds;
            bool wasOn = IsOn;
            SetOn(true);
            Sfx.At(SfxId.PuzzleSolved, transform.position, 0.8f);
            Sfx.At(SfxId.DoorSlam, transform.position, 0.5f, 1.4f);
            Fx.Confetti(transform.position + Vector3.up * 2.2f);
            Fx.FloatingText(transform.position + Vector3.up * 2.6f, "LATCHED!", UIColors.Success, 1.4f);
            if (wasOn && !string.IsNullOrEmpty(_latchMessage))
            {
                GameEvents.RaisePuzzleSolved(_latchMessage);
                GameEvents.RaisePuzzleSolvedAt(_latchMessage, transform.position);
                _latchMessage = null;
            }
            else if (Mode == HoldLatchMode.OpenThenLatch) GameEvents.RaiseToast("SECURITY LATCH ENGAGED. THE DOOR NOW TRUSTS YOU.", UIColors.Success);
        }

        void Refresh(bool force)
        {
            int n = Lamps != null ? Lamps.Length : 0;
            int lit = IsLatched ? n : Mathf.FloorToInt(Progress01 * n + 0.001f);
            if (force || lit != _shownLit)
            {
                for (int i = 0; i < n; i++)
                {
                    if (Lamps[i] == null) continue;
                    if (IsLatched) Lamps[i].Set(PuzzleLampColor.Green);
                    else Lamps[i].Set(i < lit ? PuzzleLampColor.Yellow : PuzzleLampColor.Off);
                }
                _shownLit = lit;
            }
            if (Display == null) return;
            int key = IsLatched ? -1 : Held > 0f ? Mathf.CeilToInt(Mathf.Max(0f, HoldSeconds - Held) * 10f) : -2;
            if (!force && key == _shownKey) return;
            _shownKey = key;
            bool open = Mode == HoldLatchMode.OpenThenLatch;
            if (key == -1) { Display.text = "LATCHED"; Display.color = UIColors.Success; }
            else if (key == -2) { Display.text = open ? "STANDBY" : "HOLD " + HoldSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " S"; Display.color = UIColors.Text; }
            else { Display.text = (open ? "ARMING " : "HOLD ") + (key / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " S"; Display.color = UIColors.AccentYellow; }
        }

        public void ResetState()
        {
            IsLatched = false;
            Held = 0f;
            SetOn(false);
            Refresh(true);
        }
    }
}
