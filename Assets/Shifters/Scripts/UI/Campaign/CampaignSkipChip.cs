// =====================================================================================
//  SHIFTERS - CampaignSkipChip.cs   (ui_hud agent)
//  "HOLD [BACKSPACE] TO SKIP" chip with a hold-progress fill (ShiftersInput.SkipHeld01 via
//  CampaignHudCompat). Bottom-right, inside the letterbox bar; visible in blocking scenes.
//  The key is a live ActionCap (gp_core): BACKSPACE on keyboard/mouse, the pad's BACK button on a gamepad;
//  the row re-centres when the cap width changes.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class CampaignSkipChip : MonoBehaviour
    {
        public const float W = 360f, H = 50f;

        public bool Shown { get; private set; }
        public float Progress01 { get; private set; }
        public RectTransform Rt { get; private set; }

        CanvasGroup _group;
        RectTransform _fill;
        Image _fillImg;
        float _shownFor;
        TextMeshProUGUI _hold, _skip;
        RectTransform _key;
        /// <summary>Key cap text as displayed ("BACKSPACE" / "BACK").</summary>
        public string KeyText => _key != null ? _key.GetComponent<DeviceKeyCap>().Text : "";

        public static CampaignSkipChip Create(Transform parent)
        {
            var rt = UIFactory.Rect("SkipChip", parent, new Vector2(1f, 0f), new Vector2(-30f, 28f), new Vector2(W, H), new Vector2(1f, 0f));
            var c = rt.gameObject.AddComponent<CampaignSkipChip>();
            c.Rt = rt;
            c.Build();
            return c;
        }

        void Build()
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            UIFactory.Fill(Rt, "Bg", UISprites.Rounded(25), new Color(0.06f, 0.07f, 0.09f, 0.85f));
            var track = UIFactory.Rect("FillMask", Rt); UIFactory.Stretch(track);
            _fillImg = UIFactory.Image(track, "Fill", UISprites.Rounded(25), UIFactory.WithAlpha(UIColors.Accent, 0.55f));
            _fill = _fillImg.rectTransform;
            _fill.anchorMin = new Vector2(0f, 0f); _fill.anchorMax = new Vector2(0f, 1f); _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = Vector2.zero; _fill.offsetMax = Vector2.zero;
            UIFactory.Fill(Rt, "Border", UISprites.RoundedOutline(25, 2), new Color(1f, 1f, 1f, 0.35f));
            _hold = UIFactory.Text(Rt, "Hold", "HOLD", FontLibrary.Label, 22f, UIColors.Text, TextAlignmentOptions.Right);
            _hold.characterSpacing = 4f;
            _key = UIFactory.ActionCap(Rt, "SkipTutorial", 34f, "BACKSPACE");
            _key.GetComponent<DeviceKeyCap>().Resized += _ => Layout();
            _skip = UIFactory.Text(Rt, "Skip", "TO SKIP", FontLibrary.Label, 22f, UIColors.Text, TextAlignmentOptions.Left);
            _skip.characterSpacing = 4f;
            Layout();
        }

        void Layout()
        {
            var hold = _hold; var skip = _skip; var key = _key;
            if (hold == null || skip == null || key == null) return;
            float hw = UIFactory.Preferred(hold, "HOLD").x + 4f, sw = UIFactory.Preferred(skip, "TO SKIP").x + 4f, kw = key.sizeDelta.x;
            float total = hw + 12f + kw + 12f + sw;
            float x = (W - total) * 0.5f;
            Place(hold.rectTransform, x, hw); x += hw + 12f;
            key.anchorMin = key.anchorMax = new Vector2(0f, 0.5f); key.pivot = new Vector2(0f, 0.5f); key.anchoredPosition = new Vector2(x, 2f); x += kw + 12f;
            Place(skip.rectTransform, x, sw);
        }

        static void Place(RectTransform rt, float x, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, H);
        }

        public void Set(bool shown)
        {
            if (shown == Shown) return;
            Shown = shown;
            _shownFor = 0f;
            if (!shown) { UITween.Kill(_group); UITween.Fade(_group, 0f, 0.15f); }
        }

        void Update()
        {
            float p = Shown ? CampaignHudCompat.SkipHeld01() : 0f;
            Progress01 = p;
            if (!Shown) return;
            _shownFor += UITime.Delta;
            // fade in after 0.6 s so very short scenes are not cluttered; full opacity while holding
            float target = p > 0.001f ? 1f : (_shownFor > 0.6f ? 0.72f : 0f);
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, UITime.Delta * 4f);
            _fill.anchorMax = new Vector2(Mathf.Max(0.0001f, p), 1f);
            _fillImg.enabled = p > 0.001f;
        }
    }
}
