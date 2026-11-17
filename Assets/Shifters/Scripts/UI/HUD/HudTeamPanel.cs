// =====================================================================================
//  SHIFTERS - HudTeamPanel.cs   (UI agent)
//  Bottom-left stack of the three team cards (slot 1 top .. slot 3 bottom) + the team AI
//  mode chip "TEAM: HOLD [G]" / "TEAM: FOLLOW [G]". Polls CharacterManager (cheap, robust
//  even if a module forgets an event) and animates layout changes on switch.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudTeamPanel : MonoBehaviour
    {
        const float Gap = 10f;
        readonly HudCharacterCard[] _cards = new HudCharacterCard[3];
        readonly ShifterCharacter[] _bound = new ShifterCharacter[3];
        RectTransform _rt, _aiChip;
        Image _aiIcon;
        TextMeshProUGUI _aiText;
        CanvasGroup _group;
        int _activeSlot = -2;
        TeamAIMode _mode = (TeamAIMode)(-1);

        public HudCharacterCard Card(int slot) => slot >= 0 && slot < 3 ? _cards[slot] : null;

        public static HudTeamPanel Create(Transform parent)
        {
            var rt = UIFactory.Rect("TeamPanel", parent, Vector2.zero, new Vector2(26f, 26f), new Vector2(HudCharacterCard.Width, 420f), Vector2.zero);
            var p = rt.gameObject.AddComponent<HudTeamPanel>();
            p._rt = rt;
            p._group = UIFactory.Group(rt.gameObject);
            for (int i = 0; i < 3; i++) p._cards[i] = HudCharacterCard.Create(rt);
            p._aiChip = UIFactory.Rect("AIMode", rt, Vector2.zero, Vector2.zero, new Vector2(300f, 34f), Vector2.zero);
            UIFactory.Fill(p._aiChip, "Bg", UISprites.Rounded(17), new Color(0.06f, 0.07f, 0.09f, 0.85f));
            p._aiIcon = UIFactory.Icon(p._aiChip, UIIcon.Stay, UIColors.TextMuted, 22f);
            var irt = p._aiIcon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0f, 0.5f); irt.anchoredPosition = new Vector2(12f, 0f);
            p._aiText = UIFactory.Text(p._aiChip, "Text", "TEAM: HOLD", FontLibrary.Label, 21f, UIColors.Text, new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(190f, 30f), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            p._aiText.characterSpacing = 4f;
            var key = UIFactory.KeyCap(p._aiChip, "G", 24f);
            key.anchorMin = key.anchorMax = new Vector2(1f, 0.5f); key.pivot = new Vector2(1f, 0.5f); key.anchoredPosition = new Vector2(-10f, 1f);
            p.SetVisible(false);
            return p;
        }

        void SetVisible(bool v)
        {
            for (int i = 0; i < 3; i++) if (_cards[i] != null) _cards[i].gameObject.SetActive(v && _bound[i] != null);
            _aiChip.gameObject.SetActive(v);
        }

        public void Clear()
        {
            for (int i = 0; i < 3; i++) _bound[i] = null;
            _activeSlot = -2;
            SetVisible(false);
        }

        public void SetDim(bool dim) => UITween.Fade(_group, dim ? 0.3f : 1f, 0.15f);

        void Update()
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null || cm.Team.Count == 0)
            {
                if (_aiChip.gameObject.activeSelf) Clear();
                return;
            }
            bool changed = false;
            for (int i = 0; i < 3; i++)
            {
                var c = cm.Get(i);
                if (c != _bound[i])
                {
                    _bound[i] = c;
                    if (c != null) { _cards[i].gameObject.SetActive(true); _cards[i].Bind(c); }
                    else _cards[i].gameObject.SetActive(false);
                    changed = true;
                }
            }
            if (!_aiChip.gameObject.activeSelf) { _aiChip.gameObject.SetActive(true); changed = true; }
            int active = cm.ActiveIndex;
            var mode = cm.AIMode;
            for (int i = 0; i < 3; i++) if (_bound[i] != null) _cards[i].Tick(_bound[i], i == active, mode);
            if (active != _activeSlot) { changed = true; _activeSlot = active; }
            if (mode != _mode)
            {
                bool first = (int)_mode < 0;
                _mode = mode;
                bool follow = mode == TeamAIMode.Follow;
                _aiText.text = follow ? "TEAM: FOLLOW" : "TEAM: HOLD";
                _aiIcon.sprite = UIIcons.Get(follow ? UIIcon.Follow : UIIcon.Stay);
                _aiIcon.color = follow ? UIColors.Info : UIColors.AccentYellow;
                if (!first) UITween.Punch(_aiChip, 0.15f, 0.3f);
            }
            if (changed) Layout(!(_activeSlot < -1));
        }

        void Layout(bool animate)
        {
            float y = 0f;
            for (int i = 2; i >= 0; i--)
            {
                if (_bound[i] == null) continue;
                bool act = i == _activeSlot;
                var target = new Vector2(0f, y);
                if (animate) UITween.Move(_cards[i].Rt, target, 0.25f, Ease.OutCubic); else _cards[i].Rt.anchoredPosition = target;
                y += HudCharacterCard.Height * (act ? 1f : HudCharacterCard.InactiveScale) + Gap;
            }
            var chipPos = new Vector2(0f, y + 4f);
            if (animate) UITween.Move(_aiChip, chipPos, 0.25f, Ease.OutCubic); else _aiChip.anchoredPosition = chipPos;
        }
    }
}
