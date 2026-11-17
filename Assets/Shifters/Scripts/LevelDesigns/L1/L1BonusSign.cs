// =====================================================================================
//  SHIFTERS - L1BonusSign.cs   (LEVEL 1)
//  Bonus "NICE SHOT" sign: dark until powered, then lights up with confetti, a sound and a toast.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>Receiver that switches a sign from unlit to lit (once) and celebrates.</summary>
    public class L1BonusSign : SignalReceiver
    {
        public TextMeshPro Text;
        public GameObject LitParts;
        public Color UnlitColor = new Color(0.35f, 0.38f, 0.45f);
        public Color LitColor = new Color(1f, 0.79f, 0.24f);
        public string Toast = "NICE SHOT! HR has added a gold star to your file.";

        public bool Lit { get; private set; }

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (!powered) { if (initial) Apply(false); return; }
            bool celebrate = !Lit && !initial;
            Apply(true);
            if (!celebrate) return;
            var p = Text != null ? Text.transform.position : transform.position;
            Fx.Confetti(p);
            Sfx.At(SfxId.PuzzleSolved, p);
            GameEvents.RaiseToast(Toast, UIColors.AccentYellow);
        }

        void Apply(bool lit)
        {
            Lit = lit;
            if (Text != null) Text.color = lit ? LitColor : UnlitColor;
            if (LitParts != null) LitParts.SetActive(lit);
        }
    }
}
