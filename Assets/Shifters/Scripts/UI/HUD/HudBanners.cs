// =====================================================================================
//  SHIFTERS - HudBanners.cs   (UI agent)
//  Big center banners: Bungee title on a colored, angled band (by BannerStyle) that slides
//  in from the left and out to the right; queued. Lives on the popup canvas (sort 40).
//  Also turns GameEvents.PuzzleSolved into a small toast (see HudToasts).
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudBanners : MonoBehaviour
    {
        struct Item { public string Title, Subtitle; public BannerStyle Style; }

        RectTransform _rt, _band, _shadowBand, _titleRt, _subRt;
        Image _bandImg, _stripes;
        TextMeshProUGUI _title, _sub;
        CanvasGroup _group;
        readonly Queue<Item> _queue = new Queue<Item>();
        bool _showing;
        float _hideAt;
        int _phase; // 0 idle, 1 shown (waiting), 2 leaving

        public bool Showing => _showing;
        public string CurrentTitle => _showing ? _title.text : null;
        public int Queued => _queue.Count;

        public static HudBanners Create(Transform parent)
        {
            var rt = UIFactory.Rect("Banners", parent, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(1920f, 220f));
            var b = rt.gameObject.AddComponent<HudBanners>();
            b._rt = rt;
            b._group = UIFactory.Group(rt.gameObject);
            var shadow = UIFactory.Image(rt, "BandShadow", null, new Color(0f, 0f, 0f, 0.45f));
            b._shadowBand = shadow.rectTransform;
            b._shadowBand.sizeDelta = new Vector2(2600f, 150f);
            b._shadowBand.anchoredPosition = new Vector2(14f, -14f);
            b._shadowBand.localEulerAngles = new Vector3(0f, 0f, -3f);
            b._bandImg = UIFactory.Image(rt, "Band", null, UIColors.Accent);
            b._band = b._bandImg.rectTransform;
            b._band.sizeDelta = new Vector2(2600f, 150f);
            b._band.localEulerAngles = new Vector3(0f, 0f, -3f);
            b._stripes = UIFactory.Image(b._band, "Stripes", UISprites.Stripes, new Color(0f, 0f, 0f, 0.1f));
            b._stripes.type = Image.Type.Tiled;
            var st = b._stripes.rectTransform;
            st.anchorMin = new Vector2(0f, 0f); st.anchorMax = new Vector2(1f, 0f); st.pivot = new Vector2(0.5f, 0f);
            st.sizeDelta = new Vector2(0f, 16f); st.anchoredPosition = new Vector2(0f, 0f);
            var st2 = UIFactory.Image(b._band, "StripesTop", UISprites.Stripes, new Color(0f, 0f, 0f, 0.1f));
            st2.type = Image.Type.Tiled;
            var st2r = st2.rectTransform;
            st2r.anchorMin = new Vector2(0f, 1f); st2r.anchorMax = new Vector2(1f, 1f); st2r.pivot = new Vector2(0.5f, 1f);
            st2r.sizeDelta = new Vector2(0f, 16f); st2r.anchoredPosition = Vector2.zero;

            b._title = UIFactory.Text(rt, "Title", "", FontLibrary.Display, 80f, UIColors.Hex(0x12161C), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(1700f, 100f), TextAlignmentOptions.Center);
            UIFactory.AutoSize(b._title, 40f, 80f);
            b._titleRt = b._title.rectTransform;
            b._titleRt.localEulerAngles = new Vector3(0f, 0f, -3f);
            b._sub = UIFactory.Text(rt, "Subtitle", "", FontLibrary.Heavy, 30f, UIColors.Hex(0x12161C), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(1500f, 40f), TextAlignmentOptions.Center);
            UIFactory.AutoSize(b._sub, 20f, 30f);
            b._subRt = b._sub.rectTransform;
            b._subRt.localEulerAngles = new Vector3(0f, 0f, -3f);
            b._group.alpha = 0f;
            rt.gameObject.SetActive(true);
            return b;
        }

        void OnEnable() => GameEvents.Banner += Enqueue;
        void OnDisable() => GameEvents.Banner -= Enqueue;

        public void Clear()
        {
            _queue.Clear();
            _showing = false;
            _phase = 0;
            // stop the show/hide tweens too: a fade-in still running would raise the alpha again and leave the last
            // banner frozen on screen with nothing to hide it (polish 1b: CampaignHud holds banners under a card)
            UITween.Kill(_group); UITween.Kill(_band); UITween.Kill(_shadowBand); UITween.Kill(_titleRt); UITween.Kill(_subRt);
            _group.alpha = 0f;
        }

        public static Color StyleColor(BannerStyle s)
        {
            switch (s)
            {
                case BannerStyle.Success: return UIColors.Success;
                case BannerStyle.Warning: return UIColors.AccentYellow;
                case BannerStyle.Danger: return UIColors.Danger;
                case BannerStyle.Checkpoint: return UIColors.Hex(0x4BE38A);
                case BannerStyle.Puzzle: return UIColors.Accent;
                default: return UIColors.Info;
            }
        }

        public void Enqueue(string title, string subtitle, BannerStyle style)
        {
            if (string.IsNullOrEmpty(title)) return;
            if (_queue.Count >= 4) _queue.Dequeue();
            _queue.Enqueue(new Item { Title = title, Subtitle = subtitle, Style = style });
            if (!_showing) Next();
        }

        void Next()
        {
            if (_queue.Count == 0) { _showing = false; _phase = 0; return; }
            var it = _queue.Dequeue();
            _showing = true;
            _phase = 1;
            Color c = StyleColor(it.Style);
            _bandImg.color = c;
            Color fg = UIFactory.ContrastText(c);
            _title.color = fg;
            _sub.color = UIFactory.WithAlpha(fg, 0.85f);
            _title.text = it.Title.ToUpperInvariant();
            _sub.text = it.Subtitle ?? "";
            _sub.gameObject.SetActive(!string.IsNullOrEmpty(it.Subtitle));
            float hold = 1.7f + Mathf.Min(1.2f, ((it.Subtitle?.Length ?? 0) + it.Title.Length) * 0.02f);
            if (_queue.Count > 0) hold *= 0.7f;
            _hideAt = UITime.Now + 0.3f + hold;
            UITween.Fade(_group, 1f, 0.1f, Ease.OutQuad, 0f, 0f);
            UITween.Move(_band, Vector2.zero, 0.32f, Ease.OutCubic, 0f, new Vector2(-2700f, 0f));
            UITween.Move(_shadowBand, new Vector2(14f, -14f), 0.36f, Ease.OutCubic, 0f, new Vector2(-2700f, -14f));
            UITween.Move(_titleRt, new Vector2(0f, 14f), 0.4f, Ease.OutBack, 0.06f, new Vector2(1400f, 14f));
            UITween.Move(_subRt, new Vector2(0f, -46f), 0.4f, Ease.OutBack, 0.12f, new Vector2(1600f, -46f));
            Sfx.Play(it.Style == BannerStyle.Danger ? SfxId.UIError : SfxId.Banner);
        }

        void Update()
        {
            if (!_showing || _phase != 1 || UITime.Now < _hideAt) return;
            _phase = 2;
            UITween.Move(_band, new Vector2(2700f, 0f), 0.28f, Ease.InCubic);
            UITween.Move(_shadowBand, new Vector2(2700f, -14f), 0.3f, Ease.InCubic);
            UITween.Move(_titleRt, new Vector2(-1500f, 14f), 0.26f, Ease.InBack);
            UITween.Move(_subRt, new Vector2(-1600f, -46f), 0.26f, Ease.InBack);
            UITween.Fade(_group, 0f, 0.12f, Ease.OutQuad, 0.2f, null, () => { if (this != null) Next(); });
        }
    }
}
