// =====================================================================================
//  SHIFTERS - ProfileCardView.cs   (UI agent)
//  Tall "intern dossier" card used by Team Reveal: portrait, name, title, personality,
//  SPD/STR/IQ bars with numbers, perk badges, weapon icon + name + stats (or "UNARMED").
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class ProfileCardView : MonoBehaviour
    {
        public const float W = 520f, H = 700f;
        public RectTransform Rt { get; private set; }
        public CharacterProfile Profile { get; private set; }

        UIPanel _panel;
        Image _band;
        PortraitView _portrait;
        TextMeshProUGUI _name, _title, _personality, _mass, _weaponName, _weaponStats, _slot;
        UIStatBar _spd, _str, _iq;
        RectTransform _perks, _weaponBox;
        Image _weaponIcon;
        readonly List<RectTransform> _chips = new List<RectTransform>();

        public static ProfileCardView Create(Transform parent)
        {
            var rt = UIFactory.Rect("ProfileCard", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var v = rt.gameObject.AddComponent<ProfileCardView>();
            v.Rt = rt;
            v.Build();
            return v;
        }

        void Build()
        {
            _panel = UIFactory.Panel(Rt, "Panel", new Vector2(W, H), UIFactory.WithAlpha(UIColors.Panel, 0.97f), 24, true, Color.white, true);
            UIFactory.Stretch(_panel.Rect);
            _band = UIFactory.Image(Rt, "Band", UISprites.Rounded(24), Color.white);
            var brt = _band.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(0f, -150f); brt.offsetMax = Vector2.zero;
            var bstr = UIFactory.Image(_band.rectTransform, "Stripes", UISprites.Stripes, new Color(0f, 0f, 0f, 0.1f));
            bstr.type = Image.Type.Tiled;
            UIFactory.Stretch(bstr.rectTransform);
            _slot = UIFactory.Text(Rt, "Slot", "INTERN #1", FontLibrary.Label, 22f, UIColors.Hex(0x12161C), new Vector2(0f, 1f), new Vector2(26f, -16f), new Vector2(220f, 28f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            _slot.characterSpacing = 6f;
            _mass = UIFactory.Text(Rt, "Mass", "", FontLibrary.Label, 22f, UIColors.Hex(0x12161C), new Vector2(1f, 1f), new Vector2(-26f, -16f), new Vector2(240f, 28f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));
            _mass.characterSpacing = 4f;

            _portrait = PortraitView.Create(Rt, 230f, false);
            var prt = (RectTransform)_portrait.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -52f);

            _name = UIFactory.Text(Rt, "Name", "", FontLibrary.Heavy, 46f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(W - 40f, 54f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(_name, 28f, 46f);
            _title = UIFactory.Text(Rt, "Title", "", FontLibrary.Display, 24f, UIColors.Accent, new Vector2(0.5f, 1f), new Vector2(0f, -346f), new Vector2(W - 40f, 32f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(_title, 16f, 24f);
            _personality = UIFactory.Text(Rt, "Personality", "", FontLibrary.Body, 23f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -384f), new Vector2(W - 70f, 60f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            _personality.fontStyle = FontStyles.Italic;
            UIFactory.AutoSize(_personality, 16f, 23f);

            _spd = Stat("SPD", UIColors.Speed, -466f);
            _str = Stat("STR", UIColors.Strength, -500f);
            _iq = Stat("IQ", UIColors.Intellect, -534f);

            _perks = UIFactory.Rect("Perks", Rt, new Vector2(0.5f, 1f), new Vector2(0f, -572f), new Vector2(W - 40f, 34f), new Vector2(0.5f, 1f));

            _weaponBox = UIFactory.Rect("Weapon", Rt, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(W - 36f, 76f), new Vector2(0.5f, 0f));
            UIFactory.Fill(_weaponBox, "Bg", UISprites.Rounded(14), new Color(0f, 0f, 0f, 0.35f));
            _weaponIcon = UIFactory.Icon(_weaponBox, UIIcon.Pistol, UIColors.Weapon, 54f);
            var wirt = _weaponIcon.rectTransform;
            wirt.anchorMin = wirt.anchorMax = new Vector2(0f, 0.5f); wirt.pivot = new Vector2(0f, 0.5f); wirt.anchoredPosition = new Vector2(14f, 0f);
            _weaponName = UIFactory.Text(_weaponBox, "Name", "", FontLibrary.Heavy, 26f, UIColors.Text, new Vector2(0f, 1f), new Vector2(80f, -8f), new Vector2(W - 140f, 32f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            UIFactory.AutoSize(_weaponName, 16f, 26f);
            _weaponStats = UIFactory.Text(_weaponBox, "Stats", "", FontLibrary.Label, 19f, UIColors.TextMuted, new Vector2(0f, 0f), new Vector2(80f, 10f), new Vector2(W - 140f, 26f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            _weaponStats.characterSpacing = 2f;
        }

        UIStatBar Stat(string label, Color color, float y)
        {
            var sb = UIStatBar.Create(Rt, label, color, W - 80f, 24f, 52f);
            var rt = (RectTransform)sb.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(40f, y);
            return sb;
        }

        public void Bind(CharacterProfile p, int slot, bool animate)
        {
            Profile = p;
            if (p == null) return;
            _band.color = p.PrimaryColor;
            if (_panel.Border != null) _panel.Border.color = UIFactory.WithAlpha(p.PrimaryColor, 0.8f);
            _slot.text = "INTERN #" + (slot + 1);
            _mass.text = Mathf.RoundToInt(p.Mass) + " KG  ·  " + p.Body.Height.ToString("0.00") + " M";
            _portrait.Bind(slot, p);
            _name.text = p.DisplayName.ToUpperInvariant();
            _title.text = p.Title;
            _title.color = Color.Lerp(p.PrimaryColor, Color.white, 0.15f);
            _personality.text = "“" + p.Personality + "”";
            _spd.Set(p.Stats.Speed, animate, 0.25f);
            _str.Set(p.Stats.Strength, animate, 0.32f);
            _iq.Set(p.Stats.IQ, animate, 0.39f);

            foreach (var c in _chips) if (c != null) Destroy(c.gameObject);
            _chips.Clear();
            if (p.HasStrengthPerk) _chips.Add(UIFactory.Chip(_perks, UIIcon.Strength, "HEAVY LIFTER", UIColors.Strength, 32f, true));
            if (p.HasSpeedPerk) _chips.Add(UIFactory.Chip(_perks, UIIcon.Speed, "TURBO LEGS", UIColors.Speed, 32f, true));
            if (p.HasIQPerk) _chips.Add(UIFactory.Chip(_perks, UIIcon.Brain, "BIG BRAIN", UIColors.Intellect, 32f, true));
            if (p.HasWeapon) _chips.Add(UIFactory.Chip(_perks, UIIcon.Crosshair, "ARMED", UIColors.Weapon, 32f, true));
            float total = 0f;
            foreach (var c in _chips) total += c.sizeDelta.x + 8f;
            float x = -total * 0.5f + 4f;
            foreach (var c in _chips)
            {
                c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f); c.pivot = new Vector2(0f, 0.5f);
                c.anchoredPosition = new Vector2(x, 0f);
                x += c.sizeDelta.x + 8f;
            }

            if (p.HasWeapon)
            {
                var w = p.Weapon;
                _weaponIcon.sprite = UIIcons.Get(UIIcons.ForWeapon(w));
                _weaponIcon.color = UIColors.Weapon;
                _weaponName.text = w.Name.ToUpperInvariant();
                _weaponName.color = UIColors.Text;
                _weaponStats.text = "DMG " + Mathf.RoundToInt(w.Damage) + (w.Pellets > 1 ? "x" + w.Pellets : "") + "  ·  RATE " + w.FireRate.ToString("0.0") + "/S  ·  ACC " + Mathf.RoundToInt(w.Accuracy * 100f) + "%  ·  RANGE " + Mathf.RoundToInt(w.Range) + "M";
            }
            else
            {
                _weaponIcon.sprite = UIIcons.Get(UIIcon.Person);
                _weaponIcon.color = UIColors.TextMuted;
                _weaponName.text = "UNARMED";
                _weaponName.color = UIColors.TextMuted;
                _weaponStats.text = "EMOTIONALLY ARMED, THOUGH";
            }
        }
    }
}
