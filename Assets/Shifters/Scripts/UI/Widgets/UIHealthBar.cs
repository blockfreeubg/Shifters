// =====================================================================================
//  SHIFTERS - UIHealthBar.cs   (UI agent)
//  Smooth health bar with a trailing "damage ghost" segment. Color shifts green -> yellow -> red.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class UIHealthBar : MonoBehaviour
    {
        RectTransform _fill, _ghost;
        Image _fillImg, _ghostImg;
        float _target = 1f, _shown = 1f, _ghostV = 1f, _ghostHold;

        public float Target => _target;

        public static UIHealthBar Create(Transform parent, Vector2 size)
        {
            var rt = UIFactory.Rect("Health", parent);
            rt.sizeDelta = size;
            var hb = rt.gameObject.AddComponent<UIHealthBar>();
            int r = Mathf.Clamp(Mathf.RoundToInt(size.y * 0.5f), 2, 12);
            UIFactory.Fill(rt, "Track", UISprites.Rounded(r), new Color(0f, 0f, 0f, 0.45f));
            hb._ghostImg = UIFactory.Fill(rt, "Ghost", UISprites.Rounded(r), new Color(1f, 0.95f, 0.85f, 0.85f));
            hb._ghost = hb._ghostImg.rectTransform;
            hb._fillImg = UIFactory.Fill(rt, "Fill", UISprites.Rounded(r), UIColors.Success);
            hb._fill = hb._fillImg.rectTransform;
            hb.Apply();
            return hb;
        }

        /// <summary>t01 = health / max.</summary>
        public void Set(float t01, bool instant = false)
        {
            t01 = Mathf.Clamp01(t01);
            if (t01 < _target - 0.001f) _ghostHold = 0.35f;
            _target = t01;
            if (instant) { _shown = _ghostV = t01; Apply(); }
        }

        void Update()
        {
            float dt = UITime.Delta;
            if (Mathf.Abs(_shown - _target) > 0.0005f || Mathf.Abs(_ghostV - _shown) > 0.0005f)
            {
                _shown = Mathf.MoveTowards(_shown, _target, dt * 1.6f);
                if (_ghostHold > 0f) _ghostHold -= dt;
                else _ghostV = Mathf.MoveTowards(_ghostV, _shown, dt * 0.9f);
                if (_ghostV < _shown) _ghostV = _shown;
                Apply();
            }
        }

        void Apply()
        {
            if (_fill == null) return;
            _fill.anchorMax = new Vector2(Mathf.Max(0.0001f, _shown), 1f);
            _ghost.anchorMax = new Vector2(Mathf.Max(0.0001f, _ghostV), 1f);
            _fillImg.enabled = _shown > 0.005f;
            _ghostImg.enabled = _ghostV > _shown + 0.003f;
            _fillImg.color = _shown > 0.5f ? UIColors.Success : _shown > 0.25f ? UIColors.AccentYellow : UIColors.Danger;
        }
    }
}
