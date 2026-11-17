// =====================================================================================
//  SHIFTERS - L14NayButton.cs   [LEVEL 14 - Max's NAY button: honks on every AYE; a shot breaks it ("OVERRULE")]
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L14NayButton : MonoBehaviour, IShootable
    {
        public bool Broken { get; private set; }
        public void OnShot(in ShotInfo shot)
        {
            if (Broken) { Sfx.At(SfxId.Squeak, transform.position); return; }
            Broken = true;
            Fx.Sparks(transform.position, Vector3.up, 14, UIColors.Danger);
            Sfx.At(SfxId.Squeak, transform.position, 1f, 1.4f);
            GameEvents.RaiseToast("NAY BUTTON: OUT OF ORDER", UIColors.AccentYellow);
        }
        public void Honk() { Sfx.At(Broken ? SfxId.Squeak : SfxId.Klaxon, transform.position, 0.7f, Broken ? 1.5f : 1.2f); transform.localScale = new Vector3(1f, 0.6f, 1f); _t = 0.25f; }
        float _t;
        void Update() { if (_t > 0f) { _t -= Time.deltaTime; if (_t <= 0f) transform.localScale = Vector3.one; } }
    }
}
