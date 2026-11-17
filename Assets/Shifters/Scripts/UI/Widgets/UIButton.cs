// =====================================================================================
//  SHIFTERS - UIButton.cs   (UI agent)
//  Button behaviour: hover == selection (mouse and keyboard/gamepad share one highlight),
//  scale 1.05 + orange accent bar/glow, UIHover/UIClick sounds, error shake when disabled.
//  Navigation comes from the sibling Selectable (explicit links via UIFactory.Link*).
// =====================================================================================
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shifters
{
    public class UIButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        /// <summary>Sounds are muted for selections made during this frame (programmatic default selection).</summary>
        public static int MuteFrame = -1;

        public RectTransform Rt { get; private set; }
        public Selectable Selectable { get; private set; }
        public Image Bg { get; private set; }
        public Image Accent { get; private set; }
        public Image Glow { get; private set; }
        public TextMeshProUGUI Label { get; private set; }
        public UIButtonStyle Style { get; private set; }
        public Action OnClick;
        public float HoverScale = 1.05f;
        public bool Highlighted { get; private set; }

        bool _interactable = true;
        Color _bgNormal, _bgHover, _textNormal, _textHover;

        /// <summary>Disabled buttons stay visible (dimmed) and shake + buzz when pressed.</summary>
        public bool Interactable
        {
            get => _interactable;
            set { _interactable = value; ApplyInstant(); }
        }

        public void SetText(string text) { if (Label != null) Label.text = text; }

        internal void Build(string text, UIButtonStyle style, float fontSize)
        {
            Rt = (RectTransform)transform;
            Selectable = GetComponent<Selectable>();
            Style = style;
            int radius = style == UIButtonStyle.Menu ? 10 : 14;
            Glow = UIFactory.Image(Rt, "Glow", UISprites.Shadow(24, radius), UIFactory.WithAlpha(UIColors.Accent, 0f));
            UIFactory.Expand(Glow.rectTransform, 24f, Vector2.zero);
            Bg = UIFactory.Fill(Rt, "Bg", UISprites.Rounded(radius), Color.white);
            Accent = UIFactory.Image(Rt, "Accent", UISprites.Rounded(3), UIColors.Accent);
            var art = Accent.rectTransform;
            switch (style)
            {
                case UIButtonStyle.Menu:
                    _bgNormal = new Color(0.08f, 0.1f, 0.13f, 0.82f); _bgHover = UIColors.Accent;
                    _textNormal = UIColors.Text; _textHover = UIColors.Hex(0x12161C);
                    art.anchorMin = new Vector2(0f, 0.15f); art.anchorMax = new Vector2(0f, 0.85f); art.pivot = new Vector2(0f, 0.5f);
                    art.sizeDelta = new Vector2(6f, 0f); art.anchoredPosition = new Vector2(0f, 0f);
                    Accent.color = UIColors.AccentYellow;
                    break;
                case UIButtonStyle.Primary:
                    _bgNormal = UIColors.Accent; _bgHover = UIColors.AccentYellow;
                    _textNormal = UIColors.Hex(0x12161C); _textHover = UIColors.Hex(0x12161C);
                    SetUnderline(art);
                    Accent.color = UIColors.Text;
                    break;
                case UIButtonStyle.Danger:
                    _bgNormal = UIColors.Hex(0x3A1A20, 0.95f); _bgHover = UIColors.Danger;
                    _textNormal = UIColors.Danger; _textHover = UIColors.Text;
                    SetUnderline(art);
                    break;
                case UIButtonStyle.Ghost:
                    _bgNormal = new Color(1f, 1f, 1f, 0f); _bgHover = new Color(1f, 1f, 1f, 0.08f);
                    _textNormal = UIColors.TextMuted; _textHover = UIColors.Accent;
                    SetUnderline(art);
                    break;
                default:
                    _bgNormal = UIColors.PanelLight; _bgHover = UIColors.Hex(0x2E3846);
                    _textNormal = UIColors.Text; _textHover = UIColors.AccentYellow;
                    SetUnderline(art);
                    break;
            }
            var font = style == UIButtonStyle.Menu ? FontLibrary.Display : FontLibrary.Heavy;
            Label = UIFactory.Text(Rt, "Label", text, font, fontSize, _textNormal,
                style == UIButtonStyle.Menu ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            Label.characterSpacing = style == UIButtonStyle.Menu ? 2f : 3f;
            UIFactory.Stretch(Label.rectTransform, style == UIButtonStyle.Menu ? 28f : 12f, 12f, 0f, 2f);
            ApplyInstant();
        }

        static void SetUnderline(RectTransform art)
        {
            art.anchorMin = new Vector2(0.5f, 0f); art.anchorMax = new Vector2(0.5f, 0f); art.pivot = new Vector2(0.5f, 0f);
            art.anchoredPosition = new Vector2(0f, 6f); art.sizeDelta = new Vector2(0f, 4f);
        }

        void ApplyInstant()
        {
            if (Bg == null) return;
            Bg.color = Highlighted ? _bgHover : _bgNormal;
            Label.color = Highlighted ? _textHover : _textNormal;
            float dim = _interactable ? 1f : 0.45f;
            Label.alpha = dim;
            if (!_interactable) Bg.color = UIFactory.WithAlpha(Bg.color, Bg.color.a * 0.6f);
            Rt.localScale = Vector3.one * (Highlighted ? HoverScale : 1f);
            UpdateAccent(Highlighted, true);
        }

        void UpdateAccent(bool on, bool instant)
        {
            if (Accent == null) return;
            var art = Accent.rectTransform;
            if (Style == UIButtonStyle.Menu)
            {
                float w = on ? 10f : 6f;
                art.sizeDelta = new Vector2(w, 0f);
                Accent.color = on ? UIColors.Hex(0x12161C) : UIColors.AccentYellow;
            }
            else
            {
                float target = on ? Mathf.Max(24f, Rt.sizeDelta.x * 0.55f) : 0f;
                if (instant) art.sizeDelta = new Vector2(target, 4f);
                else
                {
                    float from = art.sizeDelta.x;
                    UITween.Value(0.16f, e => { if (art != null) art.sizeDelta = new Vector2(Mathf.Lerp(from, target, e), 4f); }, Ease.OutCubic, 0f, null, art);
                }
            }
        }

        void SetHighlight(bool on, bool sound)
        {
            if (Highlighted == on || Bg == null) return;
            Highlighted = on;
            UITween.Scale(Rt, on ? HoverScale : 1f, 0.16f, on ? Ease.OutBack : Ease.OutCubic);
            Color bg = on ? _bgHover : _bgNormal;
            if (!_interactable) bg = UIFactory.WithAlpha(bg, bg.a * 0.6f);
            UITween.Color(Bg, bg, 0.12f);
            UITween.Color(Label, UIFactory.WithAlpha(on ? _textHover : _textNormal, _interactable ? 1f : 0.45f), 0.12f);
            if (Glow != null) UITween.Color(Glow, UIFactory.WithAlpha(UIColors.Accent, on && Style != UIButtonStyle.Ghost ? 0.55f : 0f), 0.18f);
            UpdateAccent(on, false);
            if (on && sound && Time.frameCount != MuteFrame) Sfx.Play(SfxId.UIHover, 0.55f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var es = EventSystem.current;
            if (es != null && Selectable != null && Selectable.IsActive()) es.SetSelectedGameObject(gameObject);
            else SetHighlight(true, true);
        }

        public void OnSelect(BaseEventData eventData) => SetHighlight(true, true);
        public void OnDeselect(BaseEventData eventData) => SetHighlight(false, false);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            Click();
        }

        public void OnSubmit(BaseEventData eventData) => Click();

        /// <summary>Press the button (also used by tests).</summary>
        public void Click()
        {
            if (!isActiveAndEnabled) return;
            if (!_interactable)
            {
                Sfx.Play(SfxId.UIError);
                UITween.Shake(Rt, 10f, 0.3f);
                return;
            }
            Sfx.Play(SfxId.UIClick);
            UITween.Scale(Rt, Highlighted ? HoverScale : 1f, 0.22f, Ease.OutBack, 0f, 0.93f);
            try { OnClick?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void OnDisable()
        {
            UITween.Kill(Rt);
            Highlighted = false;
            ApplyInstant();
        }
    }
}
