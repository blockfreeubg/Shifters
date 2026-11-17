// =====================================================================================
//  SHIFTERS - UIStatBar.cs   (UI agent)
//  "SPD ▮▮▮▮▮▮▮▯▯▯ 7" : label + 10 segmented pips + number. Perk values (>= 7) glow.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class UIStatBar : MonoBehaviour
    {
        Image[] _pips;
        TextMeshProUGUI _label, _value;
        Color _color;
        int _target;
        static readonly Color Empty = new Color(1f, 1f, 1f, 0.09f);

        public int Value => _target;

        /// <summary>Create a stat bar (pivot left-center). width includes label and number.</summary>
        public static UIStatBar Create(Transform parent, string label, Color color, float width, float height, float labelWidth = 46f)
        {
            var rt = UIFactory.Rect("Stat_" + label, parent);
            rt.sizeDelta = new Vector2(width, height);
            rt.pivot = new Vector2(0f, 0.5f);
            var sb = rt.gameObject.AddComponent<UIStatBar>();
            sb._color = color;
            sb._label = UIFactory.Text(rt, "Label", label, FontLibrary.Label, height * 0.95f, UIColors.TextMuted, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(labelWidth, height), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            sb._label.characterSpacing = 3f;
            float numW = height * 1.5f;
            sb._value = UIFactory.Text(rt, "Value", "0", FontLibrary.Heavy, height * 1.1f, UIColors.Text, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(numW, height * 1.3f), TextAlignmentOptions.Right, false, new Vector2(1f, 0.5f));
            float x0 = labelWidth + 4f, x1 = width - numW - 6f;
            float gap = Mathf.Max(2f, height * 0.18f);
            float pw = (x1 - x0 - gap * 9f) / 10f;
            sb._pips = new Image[10];
            var sprite = UISprites.Rounded(3);
            for (int i = 0; i < 10; i++)
            {
                var p = UIFactory.Image(rt, "Pip" + i, sprite, Empty);
                var prt = p.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f); prt.pivot = new Vector2(0f, 0.5f);
                prt.anchoredPosition = new Vector2(x0 + i * (pw + gap), 0f);
                prt.sizeDelta = new Vector2(pw, height * (i >= Tuning.PerkThreshold - 1 ? 0.8f : 0.62f));
                sb._pips[i] = p;
            }
            return sb;
        }

        /// <summary>Set the value (1..10). animate = pips fill one by one.</summary>
        public void Set(int value, bool animate = true, float delay = 0f)
        {
            _target = Mathf.Clamp(value, 0, 10);
            _value.text = _target.ToString();
            _value.color = Tuning.HasPerk(_target) ? _color : UIColors.Text;
            if (!animate || !isActiveAndEnabled) { Paint(_target); return; }
            Paint(0);
            int target = _target;
            UITween.Value(0.06f * target + 0.12f, e => Paint(Mathf.RoundToInt(e * target)), Ease.OutQuad, delay, null, this);
        }

        void Paint(int lit)
        {
            if (_pips == null) return;
            for (int i = 0; i < _pips.Length; i++)
            {
                if (_pips[i] == null) continue;
                bool on = i < lit;
                Color c = on ? _color : Empty;
                if (on && i < Tuning.PerkThreshold - 1 && !Tuning.HasPerk(_target)) c = Color.Lerp(_color, Color.white, 0.15f) * new Color(1f, 1f, 1f, 0.85f);
                _pips[i].color = c;
            }
        }
    }
}
