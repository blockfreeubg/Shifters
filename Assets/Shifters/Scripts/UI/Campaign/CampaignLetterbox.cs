// =====================================================================================
//  SHIFTERS - CampaignLetterbox.cs   (ui_hud agent)
//  Cinematic bars for blocking story scenes (GameEvents.CutsceneChanged(true)).
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class CampaignLetterbox : MonoBehaviour
    {
        public const float BarHeight = 104f;

        public bool On { get; private set; }
        public float Amount01 { get; private set; }

        RectTransform _top, _bottom;
        Image _topImg, _botImg;

        public static CampaignLetterbox Create(Transform parent)
        {
            var rt = UIFactory.Rect("Letterbox", parent);
            UIFactory.Stretch(rt);
            var l = rt.gameObject.AddComponent<CampaignLetterbox>();
            l.Build(rt);
            return l;
        }

        void Build(RectTransform rt)
        {
            _topImg = UIFactory.Image(rt, "Top", null, Color.black);
            _top = _topImg.rectTransform;
            _top.anchorMin = new Vector2(0f, 1f); _top.anchorMax = new Vector2(1f, 1f); _top.pivot = new Vector2(0.5f, 1f);
            _top.sizeDelta = new Vector2(400f, 0f); _top.anchoredPosition = Vector2.zero;
            _botImg = UIFactory.Image(rt, "Bottom", null, Color.black);
            _bottom = _botImg.rectTransform;
            _bottom.anchorMin = new Vector2(0f, 0f); _bottom.anchorMax = new Vector2(1f, 0f); _bottom.pivot = new Vector2(0.5f, 0f);
            _bottom.sizeDelta = new Vector2(400f, 0f); _bottom.anchoredPosition = Vector2.zero;
            Apply(0f);
        }

        public void Set(bool on, bool instant = false)
        {
            On = on;
            if (instant) Apply(on ? 1f : 0f);
        }

        void Apply(float a)
        {
            Amount01 = a;
            float h = BarHeight * Easing.Evaluate(Ease.OutCubic, a);
            _top.sizeDelta = new Vector2(400f, h);
            _bottom.sizeDelta = new Vector2(400f, h);
            bool vis = h > 0.5f;
            if (_topImg.enabled != vis) { _topImg.enabled = vis; _botImg.enabled = vis; }
        }

        void Update()
        {
            float target = On ? 1f : 0f;
            if (Mathf.Approximately(Amount01, target)) return;
            Apply(Mathf.MoveTowards(Amount01, target, UITime.Delta / 0.35f));
        }
    }
}
