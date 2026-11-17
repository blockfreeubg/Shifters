// =====================================================================================
//  SHIFTERS - HudTimer.cs   (UI agent)
//  Top-center countdown (timed doors): big digits, red + pulsing under 3 s.
//  Listens: TimerChanged(seconds) (<= 0 hides).
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class HudTimer : MonoBehaviour
    {
        RectTransform _rt, _digitsRt;
        TextMeshProUGUI _digits, _label;
        CanvasGroup _group;
        bool _visible;
        int _lastWhole = -1;
        float _seconds;

        public bool Visible => _visible;
        public float Seconds => _seconds;

        public static HudTimer Create(Transform parent)
        {
            var rt = UIFactory.Rect("Timer", parent, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(260f, 120f), new Vector2(0.5f, 1f));
            var t = rt.gameObject.AddComponent<HudTimer>();
            t._rt = rt;
            t._group = UIFactory.Group(rt.gameObject);
            var panel = UIFactory.Panel(rt, "Panel", rt.sizeDelta, UIFactory.WithAlpha(UIColors.Panel, 0.82f), 18, true);
            UIFactory.Stretch(panel.Rect);
            t._label = UIFactory.Text(rt, "Label", "TIMED DOOR", FontLibrary.Label, 19f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(240f, 24f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            t._label.characterSpacing = 6f;
            t._digits = UIFactory.Text(rt, "Digits", "0.0", FontLibrary.Heavy, 76f, UIColors.Text, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(250f, 88f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f));
            t._digitsRt = t._digits.rectTransform;
            t._group.alpha = 0f;
            return t;
        }

        void OnEnable() => GameEvents.TimerChanged += Set;
        void OnDisable() => GameEvents.TimerChanged -= Set;

        public void Clear() => Set(0f);

        public void Set(float seconds)
        {
            _seconds = seconds;
            if (seconds <= 0f)
            {
                if (_visible) { UITween.Fade(_group, 0f, 0.2f); UITween.Scale(_rt, 0.8f, 0.2f, Ease.InBack); }
                _visible = false;
                _lastWhole = -1;
                return;
            }
            if (!_visible)
            {
                _visible = true;
                UITween.Fade(_group, 1f, 0.15f);
                UITween.Scale(_rt, 1f, 0.35f, Ease.OutBack, 0f, 0.6f);
            }
            _digits.SetText("{0:1}", seconds);
            bool danger = seconds < 3f;
            _digits.color = danger ? UIColors.Danger : UIColors.Text;
            int whole = Mathf.CeilToInt(seconds);
            if (whole != _lastWhole)
            {
                if (_lastWhole >= 0) UITween.Scale(_digitsRt, 1f, 0.25f, Ease.OutBack, 0f, danger ? 1.3f : 1.1f);
                _lastWhole = whole;
            }
        }
    }
}
