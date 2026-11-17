// =====================================================================================
//  SHIFTERS - CampaignObjectiveMemo.cs   (ui_hud agent)
//  Mid-chapter objective updates: a "NEW ORDERS" rubber stamp slammed onto the corner of
//  ui's objective panel (top-left) when GameEvents.ObjectiveChanged fires after the level
//  has been running for a moment (the level-start objective is not stamped).
// =====================================================================================
using UnityEngine;
using TMPro;

namespace Shifters
{
    public class CampaignObjectiveMemo : MonoBehaviour
    {
        public const float StartGrace = 2f, Seconds = 2.6f;
        public static readonly Vector2 HomePos = new Vector2(590f, -66f);

        public bool Shown { get; private set; }
        public string StampText = "NEW ORDERS";
        public RectTransform Rt { get; private set; }

        CanvasGroup _group;
        TextMeshProUGUI _text;
        string _last;
        float _levelStart = -100f, _hideAt = -1f;

        public static CampaignObjectiveMemo Create(Transform parent)
        {
            var holder = UIFactory.Rect("ObjectiveMemo", parent, new Vector2(0f, 1f), HomePos, new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));
            var m = holder.gameObject.AddComponent<CampaignObjectiveMemo>();
            m.Build(holder);
            return m;
        }

        void Build(RectTransform holder)
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            Rt = CampaignHudStyle.Stamp(holder, "Stamp", StampText, CampaignHudStyle.StampInk, 24f, -9f, out _text, true);
            Rt.anchorMin = Rt.anchorMax = new Vector2(0.5f, 0.5f);
            Rt.anchoredPosition = Vector2.zero;
        }

        public void OnLevelStarted() { _levelStart = UITime.Now; _last = null; Hide(true); }

        public void OnObjective(string text, bool allowed)
        {
            bool fresh = !string.IsNullOrEmpty(text) && text != _last;
            bool early = UITime.Now - _levelStart < StartGrace;
            _last = text;
            if (!fresh || early || !allowed) return;
            Show();
        }

        public void Show()
        {
            CampaignHudStyle.RefitStamp(Rt, _text, StampText);
            Shown = true;
            _hideAt = UITime.Now + Seconds;
            UITween.Kill(_group); UITween.Kill(Rt);
            _group.alpha = 1f;
            UITween.Scale(Rt, 1f, 0.22f, Ease.OutBack, 0f, 2.4f);
            Sfx.Play(SfxId.Stamp, 0.7f);
        }

        public void Hide(bool instant = false)
        {
            Shown = false; _hideAt = -1f;
            UITween.Kill(_group);
            if (instant) _group.alpha = 0f; else UITween.Fade(_group, 0f, 0.3f);
        }

        void Update()
        {
            if (Shown && _hideAt > 0f && UITime.Now >= _hideAt) Hide();
        }
    }
}
