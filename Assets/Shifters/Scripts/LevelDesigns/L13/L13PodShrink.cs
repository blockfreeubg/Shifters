// =====================================================================================
//  SHIFTERS - L13PodShrink.cs   [LEVEL 13 - P4 tool shortcut]
//  BIT-SHIFTER on the SOLO POD: OnShot (FleeBrain) stuns it, then TryCycleSize: TINY (scale 0.6, mass x 0.25 =
//  26 kg) for 8 s, anyone can carry it; HUGE is refused ("OVERFLOW ERROR: EGO TOO LARGE"). At the revert a
//  non-Brute holder drops it (DIZZY 1.5 s via FleeBrain's release rule).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L13PodShrink : MonoBehaviour, IResizable
    {
        public float TinySeconds = 8f, TinyScale = 0.6f, TinyMassMul = 0.25f;
        public int SizeClass { get; private set; }
        public bool IsTiny => SizeClass < 0;
        public float TinyLeft => Mathf.Max(0f, _left);

        float _left;
        Vector3 _baseScale = Vector3.one;
        Rigidbody _rb;
        FleeBrain _pod;

        void Awake() { _rb = GetComponent<Rigidbody>(); _pod = GetComponent<FleeBrain>(); _baseScale = transform.localScale; }

        public bool TryCycleSize(ShifterCharacter by, out string reason)
        {
            reason = null;
            if (_pod != null && (_pod.IsCaptured || !_pod.enabled)) { reason = "HR SAYS NO."; return false; }
            if (SizeClass != 0) { reason = "OVERFLOW ERROR: EGO TOO LARGE"; return false; }
            SizeClass = -1;
            _left = TinySeconds;
            transform.localScale = _baseScale * TinyScale;
            if (_rb != null) _rb.mass = Tuning.SoloPodKg * TinyMassMul;
            Fx.FloatingText(transform.position + Vector3.up * 1.6f, "TINY TYRANT!", UIColors.AccentYellow, 1.2f);
            Sfx.At(SfxId.BitShift, transform.position, 1f, 1.4f);
            return true;
        }

        void Update()
        {
            if (SizeClass == 0) return;
            _left -= Time.deltaTime;
            if (_left > 0f && (_pod == null || !_pod.IsCaptured)) return;
            Revert();
        }

        public void Revert()
        {
            if (SizeClass == 0) return;
            SizeClass = 0;
            transform.localScale = _baseScale;
            if (_rb != null) _rb.mass = Tuning.SoloPodKg;
            var g = _pod != null ? _pod.Grab : null;
            if (g != null && g.Holder != null)
            {
                var who = g.Holder.GetComponent<ShifterCharacter>();
                if (who == null || who.Stats == null || who.Stats.Strength * 12 + 30 < Tuning.SoloPodKg) g.Holder.ForceRelease();
            }
            Fx.FloatingText(transform.position + Vector3.up * 1.8f, "EGO RESTORED (105 KG)", UIColors.Danger, 1.1f);
        }
    }
}
