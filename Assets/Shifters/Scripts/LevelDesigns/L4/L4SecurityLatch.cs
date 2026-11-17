// =====================================================================================
//  SHIFTERS - L4SecurityLatch.cs   [owned by LEVEL 4]
//  "SECURITY LATCH": a SignalSource that passes its Input straight through, and latches ON
//  forever once the Input has stayed on for HoldSeconds without interruption. Used by the
//  Logic Gates room: the door opens while (A AND B) AND NOT C holds and stays open after 2 s.
//  Shows a small readout (STANDBY / ARMING 1.4 S / LATCHED) and a lamp.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>Pass-through source that latches after its input stayed on for HoldSeconds.</summary>
    public class L4SecurityLatch : SignalSource, IResettable
    {
        public SignalSource Input;
        public float HoldSeconds = 2f;

        [Header("Parts")]
        public TextMeshPro Readout;
        public PuzzleLamp Lamp;
        public Transform ProgressBar;

        /// <summary>True once the latch engaged (stays on).</summary>
        public bool IsLatched { get; private set; }
        /// <summary>0..1 progress while arming.</summary>
        public float Arming01 => HoldSeconds > 0f ? Mathf.Clamp01(_held / HoldSeconds) : 1f;

        float _held;
        int _shown = -2;
        static string[] _armingText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _armingText = null;

        void OnEnable() { _shown = -2; Refresh(true); }

        void Update()
        {
            if (!IsLatched)
            {
                bool inOn = Input != null && Input.IsOn;
                if (inOn)
                {
                    _held += Time.deltaTime;
                    if (_held >= HoldSeconds) Engage();
                }
                else if (_held > 0f)
                {
                    if (_held > 0.15f) Sfx.At(SfxId.Denied, transform.position, 0.5f, 0.8f);
                    _held = 0f;
                }
                SetOn(inOn || IsLatched);
            }
            Refresh(false);
        }

        void Engage()
        {
            IsLatched = true;
            _held = HoldSeconds;
            SetOn(true);
            Sfx.At(SfxId.PuzzleSolved, transform.position);
            Fx.Confetti(transform.position + Vector3.up * 0.6f);
            GameEvents.RaiseToast("SECURITY LATCH ENGAGED. THE DOOR NOW TRUSTS YOU.", UIColors.Success);
        }

        void Refresh(bool force)
        {
            int state = IsLatched ? -1 : (_held > 0f ? Mathf.Clamp(Mathf.CeilToInt((HoldSeconds - _held) * 10f), 0, 99) : 100);
            if (!force && state == _shown) return;
            _shown = state;
            if (Readout != null)
            {
                if (state == -1) Readout.text = "SECURITY LATCH\n<color=#3DDC97>LATCHED</color>";
                else if (state == 100) Readout.text = "SECURITY LATCH\n<color=#9AA4B2>STANDBY</color>";
                else Readout.text = ArmingText(state);
            }
            if (Lamp != null) Lamp.Set(state == -1 ? PuzzleLampColor.Green : state == 100 ? PuzzleLampColor.Red : PuzzleLampColor.Yellow);
            if (ProgressBar != null) ProgressBar.localScale = new Vector3(Mathf.Max(0.001f, IsLatched ? 1f : Arming01), 1f, 1f);
        }

        static string ArmingText(int tenths)
        {
            if (_armingText == null)
            {
                _armingText = new string[100];
                for (int i = 0; i < _armingText.Length; i++)
                    _armingText[i] = "SECURITY LATCH\n<color=#FFC93C>ARMING " + (i / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " S</color>";
            }
            return _armingText[Mathf.Clamp(tenths, 0, _armingText.Length - 1)];
        }

        public void ResetState() { IsLatched = false; _held = 0f; SetOn(false); Refresh(true); }

        // ------------------------------------------------------------------ factory
        /// <summary>Wall/stand-mounted latch box (display faces local +Z) watching 'input'.</summary>
        public static L4SecurityLatch Create(Transform parent, Vector3 pos, Quaternion rot, SignalSource input, float holdSeconds = 2f)
        {
            var root = Kit.Make("SecurityLatch", parent, pos, rot);
            var t = root.transform;
            Kit.BoxVisual(t, "Housing", Vector3.zero, new Vector3(2.1f, 0.95f, 0.2f), MatKey.PuzzleBody, 0.04f);
            Kit.BoxVisual(t, "Screen", new Vector3(0f, 0.08f, 0.1f), new Vector3(1.85f, 0.58f, 0.02f), MatKey.Screen, 0.01f, null, false);
            var text = PuzzleVisuals.Text(t, "Readout", new Vector3(0f, 0.08f, 0.115f), PuzzleVisuals.Facing(Vector3.forward), "SECURITY LATCH\nSTANDBY", 1.6f, UIColors.Text, 1.8f, 0.56f);
            text.alignment = TextAlignmentOptions.Center;
            Kit.BoxVisual(t, "BarBack", new Vector3(0f, -0.33f, 0.1f), new Vector3(1.7f, 0.1f, 0.02f), MatKey.Black, 0.005f, null, false);
            var barPivot = Kit.Make("BarPivot", t, new Vector3(-0.84f, -0.33f, 0.115f)).transform;
            ProcMesh.Part("Bar", barPivot, ProcMesh.CachedBox(new Vector3(1.68f, 0.07f, 0.02f), 0.005f), MaterialLibrary.Colored(UIColors.AccentYellow, 0.2f, 1.8f), new Vector3(0.84f, 0f, 0f), null, null, false);
            var lamp = PuzzleVisuals.Lamp(t, "Lamp", new Vector3(1.2f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), 0.16f);
            var latch = root.AddComponent<L4SecurityLatch>();
            latch.Input = input;
            latch.HoldSeconds = holdSeconds;
            latch.Readout = text;
            latch.Lamp = lamp;
            latch.ProgressBar = barPivot;
            barPivot.localScale = new Vector3(0.001f, 1f, 1f);
            PerspectiveKit.Reveal(root, PerspectiveMask.Intellect, "SECURITY LATCH: OPEN FOR " + holdSeconds.ToString("0") + " S = STAYS OPEN", true, false, new Vector3(0f, 0.9f, 0f));
            return latch;
        }
    }
}
