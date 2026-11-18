// =====================================================================================
//  SHIFTERS - HudCharacterCard.cs   (UI agent)
//  One bottom-left team card: portrait, slot key, name, archetype title, smooth health,
//  state stamp (KO'D / CAPTURED / LOST), held weapon icon, perk icons, AI mode icon.
//  Active card: full size, orange frame + glow. Inactive: smaller and dimmer.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudCharacterCard : MonoBehaviour
    {
        public const float Width = 340f, Height = 100f, InactiveScale = 0.82f;

        public RectTransform Rt { get; private set; }
        public ShifterCharacter Character { get; private set; }
        public bool IsActive { get; private set; }

        UIPanel _panel;
        Image _strip, _weaponIcon, _aiIcon;
        PortraitView _portrait;
        TextMeshProUGUI _name, _title;
        UIHealthBar _health;
        RectTransform _stamp, _perkRow, _content;
        TextMeshProUGUI _stampText;
        CanvasGroup _contentGroup;
        readonly Image[] _perkIcons = new Image[3];
        TextMeshProUGUI _keyLabel;
        DeviceKeyCap _keyCap;   // kb: slot number (1/2/3 switch directly); hidden on pad (D-pad = team/hint/taunt in gameplay)

        CharacterState _state = (CharacterState)(-1);
        bool _hasWeapon, _first = true;
        TeamAIMode _aiMode = (TeamAIMode)(-1);

        public static HudCharacterCard Create(Transform parent)
        {
            var rt = UIFactory.Rect("TeamCard", parent);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(Width, Height);
            var c = rt.gameObject.AddComponent<HudCharacterCard>();
            c.Rt = rt;
            c.Build();
            return c;
        }

        void Build()
        {
            _panel = UIFactory.Panel(Rt, "Panel", new Vector2(Width, Height), UIFactory.WithAlpha(UIColors.Panel, 0.92f), 16, true, UIColors.Accent, true);
            UIFactory.Stretch(_panel.Rect);
            _panel.Border.color = UIFactory.WithAlpha(UIColors.Accent, 0f);
            _content = UIFactory.Rect("Content", Rt);
            UIFactory.Stretch(_content);
            _contentGroup = UIFactory.Group(_content.gameObject);

            _strip = UIFactory.Image(_content, "Strip", UISprites.Rounded(4), Color.white);
            var srt = _strip.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 0.5f);
            srt.offsetMin = new Vector2(8f, 12f); srt.offsetMax = new Vector2(16f, -12f);

            _portrait = PortraitView.Create(_content, 74f, true);
            var prt = (RectTransform)_portrait.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f); prt.pivot = new Vector2(0f, 0.5f);
            prt.anchoredPosition = new Vector2(26f, 0f);

            var key = UIFactory.ActionCap(_content, "Select1", 26f, "1");
            _keyCap = key.GetComponent<DeviceKeyCap>();
            _keyCap.Init(null, "1", 26f);
            key.anchorMin = key.anchorMax = new Vector2(0f, 0f); key.pivot = new Vector2(0.5f, 0.5f);
            key.anchoredPosition = new Vector2(30f, 16f);
            _keyLabel = key.GetComponentInChildren<TextMeshProUGUI>();

            _name = UIFactory.Text(_content, "Name", "NAME", FontLibrary.Heavy, 27f, UIColors.Text, new Vector2(0f, 1f), new Vector2(112f, -10f), new Vector2(150f, 32f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            UIFactory.AutoSize(_name, 18f, 27f);
            _title = UIFactory.Text(_content, "Title", "THE BRUTE", FontLibrary.Label, 18f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(112f, -42f), new Vector2(150f, 22f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            UIFactory.AutoSize(_title, 11f, 18f);
            _title.characterSpacing = 3f;

            _health = UIHealthBar.Create(_content, new Vector2(Width - 112f - 16f, 10f));
            var hrt = (RectTransform)_health.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 0f); hrt.pivot = new Vector2(0f, 0f);
            hrt.anchoredPosition = new Vector2(112f, 16f);

            _perkRow = UIFactory.Rect("Perks", _content, new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(100f, 26f), new Vector2(1f, 1f));
            for (int i = 0; i < 3; i++)
            {
                _perkIcons[i] = UIFactory.Icon(_perkRow, UIIcon.Strength, Color.white, 24f);
                var irt = _perkIcons[i].rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(1f, 0.5f); irt.pivot = new Vector2(1f, 0.5f);
                irt.anchoredPosition = new Vector2(-i * 28f, 0f);
            }
            _weaponIcon = UIFactory.Icon(_content, UIIcon.Pistol, UIColors.Weapon, 30f);
            var wrt = _weaponIcon.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(1f, 0f); wrt.pivot = new Vector2(1f, 0f);
            wrt.anchoredPosition = new Vector2(-12f, 30f);
            _aiIcon = UIFactory.Icon(_content, UIIcon.Stay, UIColors.TextMuted, 22f);
            var art = _aiIcon.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(1f, 0f); art.pivot = new Vector2(1f, 0f);
            art.anchoredPosition = new Vector2(-48f, 34f);

            _stamp = UIFactory.Rect("Stamp", Rt, new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(210f, 54f), new Vector2(0.5f, 0.5f));
            UIFactory.Fill(_stamp, "Bg", UISprites.Rounded(8), new Color(0.1f, 0.02f, 0.03f, 0.75f));
            UIFactory.Fill(_stamp, "Border", UISprites.RoundedOutline(8, 4), UIColors.Danger);
            _stampText = UIFactory.Text(_stamp, "Text", "KO'D", FontLibrary.Display, 32f, UIColors.Danger, TextAlignmentOptions.Center);
            UIFactory.Stretch(_stampText.rectTransform);
            _stamp.localEulerAngles = new Vector3(0f, 0f, -8f);
            _stamp.gameObject.SetActive(false);
        }

        public void Bind(ShifterCharacter c)
        {
            Character = c;
            _first = true;
            _state = (CharacterState)(-1);
            var p = c != null ? c.Profile : null;
            if (p == null) return;
            _name.text = p.DisplayName.ToUpperInvariant();
            _title.text = p.Title;
            _title.color = p.PrimaryColor;
            _strip.color = p.PrimaryColor;
            if (_keyCap != null) _keyCap.Init(null, (c.Slot + 1).ToString(), 26f); else _keyLabel.text = (c.Slot + 1).ToString();
            _portrait.Bind(c.Slot, p);
            int n = 0;
            if (p.HasStrengthPerk) SetPerk(n++, UIIcon.Strength, UIColors.Strength);
            if (p.HasSpeedPerk) SetPerk(n++, UIIcon.Speed, UIColors.Speed);
            if (p.HasIQPerk) SetPerk(n++, UIIcon.Brain, UIColors.Intellect);
            for (int i = n; i < 3; i++) _perkIcons[i].enabled = false;
            _health.Set(c.Health / c.MaxHealth, true);
            Tick(c, false, TeamAIMode.Stay);
        }

        void SetPerk(int i, UIIcon icon, Color color)
        {
            _perkIcons[i].enabled = true;
            _perkIcons[i].sprite = UIIcons.Get(icon);
            _perkIcons[i].color = color;
        }

        /// <summary>Per-frame refresh (cheap; only touches visuals on change).</summary>
        public void Tick(ShifterCharacter c, bool active, TeamAIMode mode)
        {
            if (c == null) return;
            _health.Set(c.Health / Mathf.Max(1f, c.MaxHealth));
            bool hasW = c.Weapon != null && c.Weapon.HasWeapon;
            if (hasW != _hasWeapon || _first)
            {
                _hasWeapon = hasW;
                _weaponIcon.enabled = hasW;
                if (hasW) _weaponIcon.sprite = UIIcons.Get(UIIcons.ForWeapon(c.Weapon.Data));
            }
            if (c.State != _state)
            {
                bool wasAlive = _state == CharacterState.Alive;
                _state = c.State;
                bool down = _state != CharacterState.Alive;
                _stamp.gameObject.SetActive(down);
                _contentGroup.alpha = down ? 0.5f : 1f;
                _portrait.SetGrey(down);
                if (down)
                {
                    _stampText.text = _state == CharacterState.Downed ? "KO'D" : _state == CharacterState.Captured ? "CAPTURED" : "LOST";
                    if (!_first) { UITween.Scale(_stamp, 1f, 0.35f, Ease.OutBack, 0f, 2.2f); UITween.Shake(Rt, 12f, 0.4f); }
                }
                else if (!wasAlive && !_first) UITween.Punch(_contentGroup.transform as RectTransform, 0.12f, 0.35f);
            }
            if (active != IsActive || _first)
            {
                IsActive = active;
                _panel.SetGlow(active, UIColors.Accent, 0.6f);
                if (_panel.Border != null) UITween.Color(_panel.Border, UIFactory.WithAlpha(UIColors.Accent, active ? 1f : 0f), 0.2f);
                UITween.Scale(Rt, active ? 1f : InactiveScale, _first ? 0.01f : 0.3f, Ease.OutBack);
                _name.color = active ? UIColors.Text : UIFactory.WithAlpha(UIColors.Text, 0.8f);
            }
            if (mode != _aiMode || _first)
            {
                _aiMode = mode;
                _aiIcon.sprite = UIIcons.Get(mode == TeamAIMode.Follow ? UIIcon.Follow : UIIcon.Stay);
                _aiIcon.color = mode == TeamAIMode.Follow ? UIColors.Info : UIColors.TextMuted;
            }
            _aiIcon.enabled = !active && c.IsAlive;
            _first = false;
        }

        public float VisualHeight => Height * (IsActive ? 1f : InactiveScale);
    }
}
