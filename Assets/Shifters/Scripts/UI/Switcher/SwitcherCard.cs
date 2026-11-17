// =====================================================================================
//  SHIFTERS - SwitcherCard.cs   (UI agent)
//  One big tilted card of the switcher wheel: live portrait, name, title, SPD/STR/IQ bars
//  with numbers, health, perk + weapon chips, "YOU" / AI-mode tag, status stamp for
//  KO'D / CAPTURED / LOST (greyed, not selectable). Highlight = scale 1.12 + orange glow.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class SwitcherCard : MonoBehaviour
    {
        public const float W = 450f, H = 296f;

        public RectTransform Rt { get; private set; }
        public int Slot { get; private set; }
        public bool Available { get; private set; } = true;
        public bool Highlighted { get; private set; }
        public ShifterCharacter Character { get; private set; }
        public Vector2 HomePos;
        public float Tilt;

        UIPanel _panel;
        CanvasGroup _group, _content;
        Image _band;
        PortraitView _portrait;
        TextMeshProUGUI _name, _title, _key, _tagText, _stampText;
        UIStatBar _spd, _str, _iq;
        UIHealthBar _health;
        RectTransform _chips, _tag, _stamp;
        Image _tagBg, _tagIcon;
        readonly List<RectTransform> _chipList = new List<RectTransform>();
        CharacterState _state = (CharacterState)(-1);
        bool _wasActive;
        int _bindVersion;

        public static SwitcherCard Create(Transform parent, int slot)
        {
            var rt = UIFactory.Rect("Card" + slot, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var c = rt.gameObject.AddComponent<SwitcherCard>();
            c.Rt = rt;
            c.Slot = slot;
            c._group = UIFactory.Group(rt.gameObject);
            c.Build();
            return c;
        }

        void Build()
        {
            _panel = UIFactory.Panel(Rt, "Panel", new Vector2(W, H), UIFactory.WithAlpha(UIColors.Panel, 0.97f), 20, true, Color.white, true);
            UIFactory.Stretch(_panel.Rect);
            var contentRt = UIFactory.Rect("Content", Rt);
            UIFactory.Stretch(contentRt);
            _content = UIFactory.Group(contentRt.gameObject);
            var c = contentRt;

            _band = UIFactory.Image(c, "Band", UISprites.Rounded(6), Color.white);
            var brt = _band.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(16f, -12f); brt.offsetMax = new Vector2(-16f, -6f);

            _portrait = PortraitView.Create(c, 156f, false);
            var prt = (RectTransform)_portrait.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(20f, -26f);

            var key = UIFactory.KeyCap(Rt, "1", 40f);
            key.anchorMin = key.anchorMax = new Vector2(0f, 1f); key.pivot = new Vector2(0.5f, 0.5f);
            key.anchoredPosition = new Vector2(14f, -10f);
            _key = key.GetComponentInChildren<TextMeshProUGUI>();

            float x = 196f, colW = W - x - 20f;
            _name = UIFactory.Text(c, "Name", "", FontLibrary.Heavy, 36f, UIColors.Text, new Vector2(0f, 1f), new Vector2(x, -22f), new Vector2(colW, 42f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            UIFactory.AutoSize(_name, 22f, 36f);
            _title = UIFactory.Text(c, "Title", "", FontLibrary.Label, 21f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(x, -62f), new Vector2(colW, 26f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            UIFactory.AutoSize(_title, 13f, 21f);
            _title.characterSpacing = 4f;
            _spd = MakeStat(c, "SPD", UIColors.Speed, x, -106f, colW);
            _str = MakeStat(c, "STR", UIColors.Strength, x, -134f, colW);
            _iq = MakeStat(c, "IQ", UIColors.Intellect, x, -162f, colW);
            UIFactory.Text(c, "HP", "HP", FontLibrary.Label, 19f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(x, -186f), new Vector2(40f, 22f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            _health = UIHealthBar.Create(c, new Vector2(colW - 50f, 10f));
            var hrt = (RectTransform)_health.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f); hrt.pivot = new Vector2(0f, 1f);
            hrt.anchoredPosition = new Vector2(x + 48f, -193f);

            _chips = UIFactory.Rect("Chips", c, new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(W - 40f, 34f), new Vector2(0f, 0f));

            _tag = UIFactory.Rect("Tag", Rt, new Vector2(1f, 1f), new Vector2(-16f, 16f), new Vector2(120f, 34f), new Vector2(1f, 0.5f));
            _tagBg = UIFactory.Fill(_tag, "Bg", UISprites.Rounded(17), UIColors.Accent);
            _tagIcon = UIFactory.Icon(_tag, UIIcon.Person, UIColors.Hex(0x12161C), 22f);
            var tirt = _tagIcon.rectTransform;
            tirt.anchorMin = tirt.anchorMax = new Vector2(0f, 0.5f); tirt.pivot = new Vector2(0f, 0.5f); tirt.anchoredPosition = new Vector2(10f, 0f);
            _tagText = UIFactory.Text(_tag, "Text", "YOU", FontLibrary.Display, 20f, UIColors.Hex(0x12161C), TextAlignmentOptions.Center);
            UIFactory.Stretch(_tagText.rectTransform, 30f, 8f, 0f, 0f);

            _stamp = UIFactory.Rect("Stamp", Rt, new Vector2(0.5f, 0.5f), new Vector2(10f, 6f), new Vector2(330f, 100f));
            UIFactory.Fill(_stamp, "Bg", UISprites.Rounded(12), new Color(0.12f, 0.02f, 0.04f, 0.82f));
            UIFactory.Fill(_stamp, "Border", UISprites.RoundedOutline(12, 6), UIColors.Danger);
            var inner = UIFactory.Fill(_stamp, "Inner", UISprites.RoundedOutline(8, 2), UIFactory.WithAlpha(UIColors.Danger, 0.7f));
            UIFactory.Stretch(inner.rectTransform, 10f, 10f, 10f, 10f);
            _stampText = UIFactory.Text(_stamp, "Text", "KO'D", FontLibrary.Display, 56f, UIColors.Danger, TextAlignmentOptions.Center);
            UIFactory.Stretch(_stampText.rectTransform, 12f, 12f, 4f, 4f);
            UIFactory.AutoSize(_stampText, 30f, 56f);
            _stamp.localEulerAngles = new Vector3(0f, 0f, -13f);
            _stamp.gameObject.SetActive(false);
        }

        static UIStatBar MakeStat(Transform parent, string label, Color color, float x, float y, float w)
        {
            var sb = UIStatBar.Create(parent, label, color, w, 20f, 44f);
            var rt = (RectTransform)sb.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            return sb;
        }

        /// <summary>Fill the card from a character (animate = stat pips fill in).</summary>
        public void Bind(ShifterCharacter c, bool isActive, TeamAIMode mode, bool animate)
        {
            Character = c;
            _bindVersion++;
            gameObject.SetActive(c != null);
            if (c == null || c.Profile == null) return;
            var p = c.Profile;
            _key.text = (Slot + 1).ToString();
            _name.text = p.DisplayName.ToUpperInvariant();
            _title.text = p.Title;
            _title.color = p.PrimaryColor;
            _band.color = p.PrimaryColor;
            _portrait.Bind(Slot, p);
            _spd.Set(p.Stats.Speed, animate, 0.12f);
            _str.Set(p.Stats.Strength, animate, 0.16f);
            _iq.Set(p.Stats.IQ, animate, 0.2f);
            _health.Set(c.Health / Mathf.Max(1f, c.MaxHealth), true);
            BuildChips(c);
            _state = (CharacterState)(-1);
            _wasActive = !isActive;
            Refresh(isActive, mode);
        }

        void BuildChips(ShifterCharacter c)
        {
            foreach (var x in _chipList) if (x != null) Destroy(x.gameObject);
            _chipList.Clear();
            var p = c.Profile;
            if (p.HasStrengthPerk) _chipList.Add(UIFactory.Chip(_chips, UIIcon.Strength, "HEAVY LIFTER", UIColors.Strength, 32f));
            if (p.HasSpeedPerk) _chipList.Add(UIFactory.Chip(_chips, UIIcon.Speed, "TURBO LEGS", UIColors.Speed, 32f));
            if (p.HasIQPerk) _chipList.Add(UIFactory.Chip(_chips, UIIcon.Brain, "BIG BRAIN", UIColors.Intellect, 32f));
            var w = c.Weapon != null && c.Weapon.HasWeapon ? c.Weapon.Data : (p.HasWeapon ? p.Weapon : null);
            if (w != null) _chipList.Add(UIFactory.Chip(_chips, UIIcons.ForWeapon(w), w.Name.ToUpperInvariant(), UIColors.Weapon, 32f, true));
            float xPos = 0f;
            foreach (var chip in _chipList)
            {
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f); chip.pivot = new Vector2(0f, 0.5f);
                chip.anchoredPosition = new Vector2(xPos, 0f);
                xPos += chip.sizeDelta.x + 8f;
            }
            float avail = W - 40f;
            float s = xPos > avail ? avail / xPos : 1f;
            _chips.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Cheap refresh of dynamic state (health, status, YOU/AI tag).</summary>
        public void Refresh(bool isActive, TeamAIMode mode)
        {
            var c = Character;
            if (c == null) return;
            _health.Set(c.Health / Mathf.Max(1f, c.MaxHealth));
            if (c.State != _state)
            {
                _state = c.State;
                Available = c.IsSelectable;
                _stamp.gameObject.SetActive(!Available);
                _content.alpha = Available ? 1f : 0.42f;
                _portrait.SetGrey(!Available);
                if (!Available) _stampText.text = _state == CharacterState.Downed ? "KO'D" : _state == CharacterState.Captured ? "CAPTURED" : "LOST";
            }
            if (isActive != _wasActive)
            {
                _wasActive = isActive;
                _tag.gameObject.SetActive(true);
            }
            if (isActive)
            {
                SetTag("YOU");
                _tagBg.color = UIColors.Accent;
                _tagIcon.sprite = UIIcons.Get(UIIcon.Person);
                _tagText.color = _tagIcon.color = UIColors.Hex(0x12161C);
            }
            else if (Available)
            {
                bool follow = mode == TeamAIMode.Follow;
                SetTag(follow ? "FOLLOWING" : "HOLDING");
                _tagBg.color = UIColors.PanelLight;
                _tagIcon.sprite = UIIcons.Get(follow ? UIIcon.Follow : UIIcon.Stay);
                _tagIcon.color = follow ? UIColors.Info : UIColors.AccentYellow;
                _tagText.color = UIColors.Text;
            }
            else _tag.gameObject.SetActive(false);
            ApplyBorder();
        }

        /// <summary>Tag text + width fitted to it (measured only when the text changes).</summary>
        void SetTag(string text)
        {
            if (_tagText.text == text && _tag.sizeDelta.x > 1f) return;
            _tagText.text = text;
            float w = UIFactory.Preferred(_tagText, text).x;
            _tag.sizeDelta = new Vector2(30f + w + 16f, 34f);
        }

        public static float TargetScale(bool on, bool anyHighlighted) => on ? 1.12f : (anyHighlighted ? 0.94f : 1f);

        public void SetHighlighted(bool on, bool anyHighlighted, bool instant, bool applyScale = true)
        {
            Highlighted = on;
            float scale = TargetScale(on, anyHighlighted);
            float alpha = on || !anyHighlighted ? 1f : 0.62f;
            if (instant) { if (applyScale) Rt.localScale = Vector3.one * scale; _group.alpha = alpha; }
            else
            {
                if (applyScale) UITween.Scale(Rt, scale, 0.18f, on ? Ease.OutBack : Ease.OutCubic);
                UITween.Fade(_group, alpha, 0.15f);
            }
            if (on) Rt.SetAsLastSibling();
            _panel.SetGlow(on, Available ? UIColors.Accent : UIColors.Danger, 0.9f);
            ApplyBorder();
        }

        void ApplyBorder()
        {
            if (_panel.Border == null) return;
            Color b = Highlighted ? (Available ? UIColors.Accent : UIColors.Danger)
                : (Character != null && Character.Profile != null ? UIFactory.WithAlpha(Character.Profile.PrimaryColor, 0.45f) : UIColors.Border);
            _panel.Border.color = b;
        }

        /// <summary>Error feedback: shake + red flash (selecting an unavailable character).</summary>
        public void Deny()
        {
            UITween.Shake(Rt, 22f, 0.4f);
            if (_stamp.gameObject.activeSelf) UITween.Punch(_stamp, 0.18f, 0.3f);
            if (_panel.Glow != null) UITween.Color(_panel.Glow, UIFactory.WithAlpha(UIColors.Danger, 0f), 0.5f, Ease.OutQuad, 0f, UIFactory.WithAlpha(UIColors.Danger, 1f));
        }
    }
}
