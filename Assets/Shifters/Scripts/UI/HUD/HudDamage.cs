// =====================================================================================
//  SHIFTERS - HudDamage.cs   (UI agent)
//  Red screen-edge vignette pulse when the ACTIVE character takes damage; a faint
//  heartbeat pulse while the active character is below 30% health.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudDamage : MonoBehaviour
    {
        Image _vignette;
        float _pulse;
        public float Intensity => _vignette != null ? _vignette.color.a : 0f;

        public static HudDamage Create(Transform parent)
        {
            var rt = UIFactory.Rect("Damage", parent);
            UIFactory.Stretch(rt);
            var d = rt.gameObject.AddComponent<HudDamage>();
            d._vignette = UIFactory.Fill(rt, "Vignette", UISprites.Vignette, new Color(0.9f, 0.08f, 0.12f, 0f));
            return d;
        }

        void OnEnable() => GameEvents.CharacterDamaged += OnDamaged;
        void OnDisable() => GameEvents.CharacterDamaged -= OnDamaged;

        public void Clear() { _pulse = 0f; Apply(0f); }

        void OnDamaged(ShifterCharacter c, float amount)
        {
            if (c == null || !c.IsActive) return;
            _pulse = Mathf.Max(_pulse, Mathf.Clamp(amount / 35f, 0.35f, 0.9f));
        }

        void Update()
        {
            float low = 0f;
            var cm = CharacterManager.Instance;
            var a = cm != null ? cm.Active : null;
            if (a != null && a.IsAlive && a.Health < a.MaxHealth * 0.3f)
                low = 0.12f + 0.1f * Mathf.Max(0f, Mathf.Sin(UITime.Now * 6f));
            if (_pulse > 0f) _pulse = Mathf.MoveTowards(_pulse, 0f, UITime.Delta * 1.4f);
            Apply(Mathf.Max(low, _pulse));
        }

        void Apply(float a)
        {
            if (_vignette == null) return;
            var c = _vignette.color;
            if (Mathf.Abs(c.a - a) < 0.002f) return;
            c.a = a;
            _vignette.color = c;
            _vignette.enabled = a > 0.003f;
        }
    }
}
