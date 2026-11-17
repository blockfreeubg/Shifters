// =====================================================================================
//  SHIFTERS - HudCrosshair.cs   (UI agent)
//  Center crosshair (4 ticks + dot, dark outline) shown on GameEvents.CrosshairChanged.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudCrosshair : MonoBehaviour
    {
        RectTransform _rt;
        CanvasGroup _group;
        bool _visible;
        public bool Visible => _visible;

        public static HudCrosshair Create(Transform parent)
        {
            var rt = UIFactory.Rect("Crosshair", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            var c = rt.gameObject.AddComponent<HudCrosshair>();
            c._rt = rt;
            c._group = UIFactory.Group(rt.gameObject);
            for (int pass = 0; pass < 2; pass++)
            {
                bool outline = pass == 0;
                float grow = outline ? 4f : 0f;
                Color col = outline ? new Color(0f, 0f, 0f, 0.6f) : UIColors.Text;
                Tick(rt, new Vector2(0f, 15f), new Vector2(4f + grow, 14f + grow), col);
                Tick(rt, new Vector2(0f, -15f), new Vector2(4f + grow, 14f + grow), col);
                Tick(rt, new Vector2(15f, 0f), new Vector2(14f + grow, 4f + grow), col);
                Tick(rt, new Vector2(-15f, 0f), new Vector2(14f + grow, 4f + grow), col);
                var dot = UIFactory.Image(rt, "Dot", UISprites.Circle, outline ? col : UIColors.Accent);
                dot.rectTransform.sizeDelta = Vector2.one * (6f + grow);
            }
            c._group.alpha = 0f;
            return c;
        }

        static void Tick(RectTransform parent, Vector2 pos, Vector2 size, Color c)
        {
            var img = UIFactory.Image(parent, "Tick", UISprites.Rounded(2), c);
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
        }

        void OnEnable() => GameEvents.CrosshairChanged += Set;
        void OnDisable() => GameEvents.CrosshairChanged -= Set;

        public void Set(bool visible)
        {
            if (visible == _visible) return;
            _visible = visible;
            UITween.Fade(_group, visible ? 1f : 0f, 0.12f);
            if (visible) UITween.Scale(_rt, 1f, 0.25f, Ease.OutBack, 0f, 1.6f);
        }
    }
}
