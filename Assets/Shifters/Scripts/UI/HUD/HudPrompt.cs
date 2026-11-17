// =====================================================================================
//  SHIFTERS - HudPrompt.cs   (UI agent)
//  Bottom-center interaction prompt: [KEY] text. Blocked prompts turn red and show the
//  reason line ("Requires STR 7 - yours: 3"). Listens: GameEvents.Prompt (null clears).
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudPrompt : MonoBehaviour
    {
        RectTransform _rt, _pill, _key;
        Transform _keyParent;
        TextMeshProUGUI _text, _reason;
        Image _bg, _border;
        CanvasGroup _group;
        string _lastKey, _lastText, _lastReason;
        bool _lastBlocked, _visible;

        public bool Visible => _visible;
        public bool Blocked => _lastBlocked;
        public string CurrentText => _lastText;

        public static HudPrompt Create(Transform parent)
        {
            var rt = UIFactory.Rect("Prompt", parent, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 110f), new Vector2(0.5f, 0f));
            var p = rt.gameObject.AddComponent<HudPrompt>();
            p._rt = rt;
            p._group = UIFactory.Group(rt.gameObject);
            p._pill = UIFactory.Rect("Pill", rt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 58f), new Vector2(0.5f, 1f));
            var sh = UIFactory.Image(p._pill, "Shadow", UISprites.Shadow(18, 29), new Color(0f, 0f, 0f, 0.5f));
            UIFactory.Expand(sh.rectTransform, 18f, new Vector2(0f, -5f));
            p._bg = UIFactory.Fill(p._pill, "Bg", UISprites.Rounded(29), new Color(0.06f, 0.07f, 0.09f, 0.88f));
            p._border = UIFactory.Fill(p._pill, "Border", UISprites.RoundedOutline(29, 3), UIFactory.WithAlpha(UIColors.Accent, 0.9f));
            p._keyParent = p._pill;
            p._text = UIFactory.Text(p._pill, "Text", "", FontLibrary.Heavy, 28f, UIColors.Text, TextAlignmentOptions.Left);
            p._text.characterSpacing = 1f;
            p._reason = UIFactory.Text(rt, "Reason", "", FontLibrary.Body, 22f, UIColors.Danger, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(900f, 30f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            p._group.alpha = 0f;
            return p;
        }

        void OnEnable() => GameEvents.Prompt += OnPrompt;
        void OnDisable() => GameEvents.Prompt -= OnPrompt;

        public void Clear() => OnPrompt(null);

        void OnPrompt(PromptInfo? info)
        {
            if (!info.HasValue || string.IsNullOrEmpty(info.Value.Text))
            {
                if (_visible) UITween.Fade(_group, 0f, 0.15f);
                _visible = false;
                _lastText = null;
                return;
            }
            var p = info.Value;
            string key = string.IsNullOrEmpty(p.Key) ? "E" : p.Key;
            bool same = _visible && key == _lastKey && p.Text == _lastText && p.Blocked == _lastBlocked && p.BlockedReason == _lastReason;
            if (same) return;
            bool wasVisible = _visible;
            _lastKey = key; _lastText = p.Text; _lastBlocked = p.Blocked; _lastReason = p.BlockedReason;
            _visible = true;

            if (_key != null) Destroy(_key.gameObject);
            _key = UIFactory.KeyCap(_keyParent, key, 38f);
            _key.anchorMin = _key.anchorMax = new Vector2(0f, 0.5f); _key.pivot = new Vector2(0f, 0.5f);
            _key.anchoredPosition = new Vector2(12f, 1f);
            float kw = _key.sizeDelta.x;
            _text.text = p.Text;
            float tw = Mathf.Min(UIFactory.Preferred(_text, p.Text).x, 720f);
            var trt = _text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f); trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(12f + kw + 14f, 0f); trt.sizeDelta = new Vector2(tw + 4f, 50f);
            _pill.sizeDelta = new Vector2(12f + kw + 14f + tw + 26f, 58f);

            Color accent = p.Blocked ? UIColors.Danger : UIColors.Accent;
            _border.color = UIFactory.WithAlpha(accent, 0.95f);
            _text.color = p.Blocked ? Color.Lerp(UIColors.Danger, Color.white, 0.25f) : UIColors.Text;
            _bg.color = p.Blocked ? new Color(0.16f, 0.04f, 0.06f, 0.9f) : new Color(0.06f, 0.07f, 0.09f, 0.88f);
            _reason.text = p.Blocked ? (p.BlockedReason ?? "") : "";
            _reason.gameObject.SetActive(p.Blocked && !string.IsNullOrEmpty(p.BlockedReason));

            UITween.Fade(_group, 1f, 0.15f);
            if (!wasVisible) UITween.Move(_rt, new Vector2(0f, 150f), 0.25f, Ease.OutBack, 0f, new Vector2(0f, 120f));
            UITween.Scale(_key, 1f, 0.3f, Ease.OutBack, 0f, 0.6f);
            if (p.Blocked) UITween.Shake(_pill, 8f, 0.3f);
        }
    }
}
