// =====================================================================================
//  SHIFTERS - HudHint.cs   (UI agent)
//  Bottom-right hint box (bulb icon, "HINT", text) for GameEvents.Hint. Shows 6 s.
//  The hint text itself conveys quality (IQ-tiered hints are written by the level module).
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class HudHint : MonoBehaviour
    {
        public const float ShowSeconds = 6f;
        RectTransform _rt, _bulb;
        TextMeshProUGUI _text;
        CanvasGroup _group;
        float _hideAt = -1f;
        public bool Visible => _hideAt > 0f;
        public string Text => _text != null ? _text.text : "";

        public static HudHint Create(Transform parent)
        {
            var rt = UIFactory.Rect("Hint", parent, new Vector2(1f, 0f), new Vector2(-26f, 150f), new Vector2(540f, 120f), new Vector2(1f, 0f));
            var h = rt.gameObject.AddComponent<HudHint>();
            h._rt = rt;
            h._group = UIFactory.Group(rt.gameObject);
            var panel = UIFactory.Panel(rt, "Panel", rt.sizeDelta, UIFactory.WithAlpha(UIColors.Panel, 0.92f), 16, true, UIFactory.WithAlpha(UIColors.AccentYellow, 0.7f));
            UIFactory.Stretch(panel.Rect);
            var bulb = UIFactory.Icon(rt, UIIcon.Bulb, UIColors.AccentYellow, 54f);
            h._bulb = bulb.rectTransform;
            h._bulb.anchorMin = h._bulb.anchorMax = new Vector2(0f, 1f); h._bulb.pivot = new Vector2(0.5f, 0.5f);
            h._bulb.anchoredPosition = new Vector2(46f, -46f);
            var title = UIFactory.Text(rt, "Title", "HINT", FontLibrary.Display, 24f, UIColors.AccentYellow, new Vector2(0f, 1f), new Vector2(86f, -14f), new Vector2(300f, 30f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            title.characterSpacing = 4f;
            h._text = UIFactory.Text(rt, "Text", "", FontLibrary.Body, 24f, UIColors.Text, new Vector2(0f, 1f), new Vector2(86f, -46f), new Vector2(430f, 66f), TextAlignmentOptions.TopLeft, true, new Vector2(0f, 1f));
            UIFactory.AutoSize(h._text, 16f, 24f);
            h._group.alpha = 0f;
            return h;
        }

        void OnEnable() => GameEvents.Hint += Show;
        void OnDisable() => GameEvents.Hint -= Show;

        public void Clear() { _hideAt = -1f; _group.alpha = 0f; }

        public void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _text.text = text;
            _hideAt = UITime.Now + ShowSeconds;
            UITween.Fade(_group, 1f, 0.2f);
            UITween.Move(_rt, new Vector2(-26f, 150f), 0.35f, Ease.OutBack, 0f, new Vector2(80f, 150f));
            UITween.Rotate(_bulb, 0f, 0.6f, Ease.OutElastic, 0.1f, -25f);
            Sfx.Play(SfxId.UIPop);
        }

        void Update()
        {
            if (_hideAt > 0f && UITime.Now > _hideAt)
            {
                _hideAt = -1f;
                UITween.Fade(_group, 0f, 0.35f);
            }
        }
    }
}
