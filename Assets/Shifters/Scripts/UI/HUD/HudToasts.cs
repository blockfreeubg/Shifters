// =====================================================================================
//  SHIFTERS - HudToasts.cs   (UI agent)
//  Right-side stack of short notifications (GameEvents.Toast, + PuzzleSolved as a green
//  toast with a check icon). Slide in, live 3.2 s, fade; max 5 (oldest dropped). Pooled.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudToasts : MonoBehaviour
    {
        const int Max = 5;
        const float Life = 3.2f, ItemH = 54f, Gap = 8f, Width = 440f;

        class Toast
        {
            public RectTransform Rt;
            public CanvasGroup Group;
            public TextMeshProUGUI Text;
            public Image Bar, Icon;
            public float Until;
            public bool Active, Leaving;
        }

        RectTransform _rt;
        readonly List<Toast> _pool = new List<Toast>();
        readonly List<Toast> _order = new List<Toast>();

        public int ActiveCount => _order.Count;
        public string Newest => _order.Count > 0 ? _order[_order.Count - 1].Text.text : null;

        public static HudToasts Create(Transform parent)
        {
            var rt = UIFactory.Rect("Toasts", parent, new Vector2(1f, 1f), new Vector2(-26f, -190f), new Vector2(Width, 400f), new Vector2(1f, 1f));
            var t = rt.gameObject.AddComponent<HudToasts>();
            t._rt = rt;
            for (int i = 0; i < Max + 1; i++) t._pool.Add(t.Make(i));
            return t;
        }

        Toast Make(int i)
        {
            var t = new Toast();
            t.Rt = UIFactory.Rect("Toast" + i, _rt, new Vector2(1f, 1f), Vector2.zero, new Vector2(Width, ItemH), new Vector2(1f, 1f));
            t.Group = UIFactory.Group(t.Rt.gameObject);
            var sh = UIFactory.Image(t.Rt, "Shadow", UISprites.Shadow(14, 10), new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Expand(sh.rectTransform, 14f, new Vector2(0f, -4f));
            UIFactory.Fill(t.Rt, "Bg", UISprites.Rounded(10), UIFactory.WithAlpha(UIColors.Panel, 0.94f));
            t.Bar = UIFactory.Image(t.Rt, "Bar", UISprites.Rounded(3), UIColors.Accent);
            var brt = t.Bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 0.5f);
            brt.offsetMin = new Vector2(6f, 8f); brt.offsetMax = new Vector2(12f, -8f);
            t.Icon = UIFactory.Icon(t.Rt, UIIcon.Check, UIColors.Success, 26f);
            var irt = t.Icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0f, 0.5f); irt.anchoredPosition = new Vector2(22f, 0f);
            t.Text = UIFactory.Text(t.Rt, "Text", "", FontLibrary.Heavy, 23f, UIColors.Text, TextAlignmentOptions.Left);
            UIFactory.Stretch(t.Text.rectTransform, 24f, 14f, 4f, 4f);
            t.Text.textWrappingMode = TextWrappingModes.Normal;   // long lines (TEAM CODE ...) wrap to 2 lines instead of running off-screen
            UIFactory.AutoSize(t.Text, 15f, 23f);
            t.Rt.gameObject.SetActive(false);
            return t;
        }

        void OnEnable()
        {
            GameEvents.Toast += Show;
            GameEvents.PuzzleSolved += OnPuzzleSolved;
        }

        void OnDisable()
        {
            GameEvents.Toast -= Show;
            GameEvents.PuzzleSolved -= OnPuzzleSolved;
        }

        void OnPuzzleSolved(string text) => Show(string.IsNullOrEmpty(text) ? "PUZZLE SOLVED" : text.ToUpperInvariant(), UIColors.Success, UIIcon.Check);

        public void Clear()
        {
            foreach (var t in _pool) { t.Active = false; t.Rt.gameObject.SetActive(false); }
            _order.Clear();
        }

        public void Show(string text, Color color) => Show(text, color, null);

        public void Show(string text, Color color, UIIcon? icon)
        {
            if (string.IsNullOrEmpty(text)) return;
            Toast t = null;
            foreach (var x in _pool) if (!x.Active) { t = x; break; }
            if (t == null || _order.Count >= Max)
            {
                var old = _order[0];
                _order.RemoveAt(0);
                old.Active = false;
                old.Rt.gameObject.SetActive(false);
                if (t == null) t = old;
            }
            t.Active = true;
            t.Leaving = false;
            t.Until = UITime.Now + Life;
            t.Text.text = text;
            Color barColor = color.a < 0.05f ? UIColors.Accent : color;
            bool neutral = Mathf.Abs(color.r - UIColors.Text.r) < 0.02f && Mathf.Abs(color.g - UIColors.Text.g) < 0.02f;
            t.Bar.color = neutral ? UIColors.Accent : barColor;
            t.Text.color = neutral ? UIColors.Text : Color.Lerp(barColor, Color.white, 0.35f);
            t.Icon.enabled = icon.HasValue;
            if (icon.HasValue) { t.Icon.sprite = UIIcons.Get(icon.Value); t.Icon.color = barColor; }
            UIFactory.Stretch(t.Text.rectTransform, icon.HasValue ? 58f : 24f, 14f, 4f, 4f);
            t.Rt.gameObject.SetActive(true);
            _order.Add(t);
            float y = -(_order.Count - 1) * (ItemH + Gap);
            UITween.Fade(t.Group, 1f, 0.15f, Ease.OutQuad, 0f, 0f);
            UITween.Move(t.Rt, new Vector2(0f, y), 0.3f, Ease.OutBack, 0f, new Vector2(Width + 60f, y));
            Sfx.Play(SfxId.UIPop, 0.6f);
        }

        void Update()
        {
            bool relayout = false;
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                var t = _order[i];
                if (!t.Leaving && UITime.Now > t.Until)
                {
                    t.Leaving = true;
                    UITween.Fade(t.Group, 0f, 0.3f);
                }
                if (t.Leaving && t.Group.alpha <= 0.01f)
                {
                    t.Active = false;
                    t.Rt.gameObject.SetActive(false);
                    _order.RemoveAt(i);
                    relayout = true;
                }
            }
            if (!relayout) return;
            for (int i = 0; i < _order.Count; i++) UITween.Move(_order[i].Rt, new Vector2(0f, -i * (ItemH + Gap)), 0.25f, Ease.OutCubic);
        }
    }
}
