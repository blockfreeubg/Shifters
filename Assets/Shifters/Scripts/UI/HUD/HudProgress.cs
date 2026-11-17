// =====================================================================================
//  SHIFTERS - HudProgress.cs   (UI agent)
//  Progress bar below the screen center (terminal hacking...). Listens: Progress(t01, label);
//  t01 < 0 hides. Completes with a green flash.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudProgress : MonoBehaviour
    {
        RectTransform _rt, _fill;
        Image _fillImg;
        TextMeshProUGUI _label, _pct;
        CanvasGroup _group;
        bool _visible;
        float _value;

        public bool Visible => _visible;
        public float Value => _value;

        public static HudProgress Create(Transform parent)
        {
            var rt = UIFactory.Rect("Progress", parent, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(480f, 70f));
            var p = rt.gameObject.AddComponent<HudProgress>();
            p._rt = rt;
            p._group = UIFactory.Group(rt.gameObject);
            p._label = UIFactory.Text(rt, "Label", "HACKING...", FontLibrary.Label, 22f, UIColors.Text, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(380f, 28f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            p._label.characterSpacing = 5f;
            p._pct = UIFactory.Text(rt, "Pct", "0%", FontLibrary.Heavy, 24f, UIColors.AccentYellow, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(100f, 28f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));
            var track = UIFactory.Rect("Track", rt, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(480f, 22f), new Vector2(0.5f, 0f));
            var tsh = UIFactory.Image(track, "Shadow", UISprites.Shadow(14, 11), new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Expand(tsh.rectTransform, 14f, new Vector2(0f, -3f));
            UIFactory.Fill(track, "Bg", UISprites.Rounded(11), new Color(0.05f, 0.06f, 0.08f, 0.9f));
            var fillRoot = UIFactory.Rect("FillRoot", track);
            UIFactory.Stretch(fillRoot, 4f, 4f, 4f, 4f);
            p._fillImg = UIFactory.Fill(fillRoot, "Fill", UISprites.Rounded(7), UIColors.Accent);
            p._fill = p._fillImg.rectTransform;
            var stripes = UIFactory.Fill(p._fill, "Stripes", UISprites.Stripes, new Color(1f, 1f, 1f, 0.16f));
            stripes.type = Image.Type.Tiled;
            p._group.alpha = 0f;
            return p;
        }

        void OnEnable() => GameEvents.Progress += Set;
        void OnDisable() => GameEvents.Progress -= Set;

        public void Clear() => Set(-1f, null);

        public void Set(float t01, string label)
        {
            if (t01 < 0f)
            {
                if (_visible) UITween.Fade(_group, 0f, 0.25f, Ease.OutQuad, _value >= 0.999f ? 0.35f : 0f);
                _visible = false;
                return;
            }
            bool wasDone = _value >= 0.999f;
            _value = Mathf.Clamp01(t01);
            if (!_visible)
            {
                _visible = true;
                UITween.Fade(_group, 1f, 0.15f);
                UITween.Scale(_rt, 1f, 0.3f, Ease.OutBack, 0f, 0.8f);
            }
            if (!string.IsNullOrEmpty(label)) _label.text = label.ToUpperInvariant();
            _fill.anchorMax = new Vector2(Mathf.Max(0.001f, _value), 1f);
            _pct.SetText("{0}%", Mathf.RoundToInt(_value * 100f));
            bool done = _value >= 0.999f;
            _fillImg.color = done ? UIColors.Success : UIColors.Accent;
            if (done && !wasDone) UITween.Punch(_rt, 0.08f, 0.3f);
        }
    }
}
