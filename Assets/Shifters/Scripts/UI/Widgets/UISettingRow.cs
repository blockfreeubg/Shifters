// =====================================================================================
//  SHIFTERS - UISettingRow.cs   (UI agent)
//  A settings row: label + control (slider or < OPTION > stepper). Mouse hover selects,
//  keyboard Up/Down navigates, Left/Right changes the value, Enter/click steps options.
// =====================================================================================
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shifters
{
    public class UISettingRow : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IMoveHandler, IPointerClickHandler, ISubmitHandler
    {
        public Selectable Selectable { get; private set; }
        public Slider Slider { get; private set; }
        public TextMeshProUGUI ValueText { get; private set; }
        public string Label { get; private set; }

        Image _rowBg, _accent;
        TextMeshProUGUI _label;
        string[] _options;
        int _index;
        Action<int> _onOption;
        Action<float> _onSlider;
        Func<float, string> _format;

        public int OptionIndex => _index;

        const float RowW = 980f, RowH = 56f, CtrlW = 430f;

        static RectTransform MakeRow(Transform parent, string label, out Image bg, out Image accent, out TextMeshProUGUI text)
        {
            var rt = UIFactory.Rect("Row_" + label, parent);
            rt.sizeDelta = new Vector2(RowW, RowH);
            bg = UIFactory.Fill(rt, "Bg", UISprites.Rounded(10), new Color(1f, 1f, 1f, 0.03f));
            accent = UIFactory.Image(rt, "Accent", UISprites.Rounded(3), UIColors.Accent);
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 0.18f); art.anchorMax = new Vector2(0f, 0.82f); art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(5f, 0f);
            accent.enabled = false;
            text = UIFactory.Text(rt, "Label", label, FontLibrary.Heavy, 26f, UIColors.Text, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(480f, RowH), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            text.characterSpacing = 2f;
            return rt;
        }

        /// <summary>Slider row. format renders the value text (e.g. v => (v*100)+"%").</summary>
        public static UISettingRow CreateSlider(Transform parent, string label, float min, float max, float value, bool whole, Func<float, string> format, Action<float> onChange)
        {
            var rt = MakeRow(parent, label, out var bg, out var accent, out var text);
            var track = UIFactory.Rect("Track", rt, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(CtrlW - 110f, 12f), new Vector2(1f, 0.5f));
            var hit = UIFactory.Image(track, "Hit", null, new Color(0f, 0f, 0f, 0f), true);
            UIFactory.Expand(hit.rectTransform, 18f, Vector2.zero);
            var trackBg = UIFactory.Fill(track, "Bg", UISprites.Rounded(6), new Color(0f, 0f, 0f, 0.5f));
            trackBg.raycastTarget = true;
            var fillArea = UIFactory.Rect("FillArea", track);
            UIFactory.Stretch(fillArea);
            var fill = UIFactory.Image(fillArea, "Fill", UISprites.Rounded(6), UIColors.Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = UIFactory.Rect("HandleArea", track);
            UIFactory.Stretch(handleArea);
            var handle = UIFactory.Image(handleArea, "Handle", UISprites.Circle, UIColors.Text);
            handle.rectTransform.sizeDelta = new Vector2(30f, 18f);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = trackBg;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole;
            slider.SetValueWithoutNotify(value);
            var vt = UIFactory.Text(rt, "Value", "", FontLibrary.Heavy, 26f, UIColors.AccentYellow, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(90f, RowH), TextAlignmentOptions.Right, false, new Vector2(1f, 0.5f));
            var row = track.gameObject.AddComponent<UISettingRow>();
            row.Selectable = slider; row.Slider = slider; row.ValueText = vt; row.Label = label;
            row._rowBg = bg; row._accent = accent; row._label = text; row._format = format; row._onSlider = onChange;
            vt.text = format != null ? format(value) : value.ToString("0.0");
            slider.onValueChanged.AddListener(row.OnSliderChanged);
            return row;
        }

        /// <summary>Stepper row: "< OPTION >".</summary>
        public static UISettingRow CreateOption(Transform parent, string label, string[] options, int index, Action<int> onChange)
        {
            var rt = MakeRow(parent, label, out var bg, out var accent, out var text);
            bg.raycastTarget = true;
            var sel = rt.gameObject.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;
            sel.targetGraphic = bg;
            var vt = UIFactory.Text(rt, "Value", "", FontLibrary.Display, 26f, UIColors.AccentYellow, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(CtrlW - 130f, RowH), TextAlignmentOptions.Center, false, new Vector2(1f, 0.5f));
            MakeArrow(rt, "<", new Vector2(-(CtrlW - 50f), 0f));
            MakeArrow(rt, ">", new Vector2(-20f, 0f));
            var row = rt.gameObject.AddComponent<UISettingRow>();
            row.Selectable = sel; row.ValueText = vt; row.Label = label;
            row._rowBg = bg; row._accent = accent; row._label = text; row._options = options; row._onOption = onChange;
            row._index = Mathf.Clamp(index, 0, options.Length - 1);
            vt.text = options[row._index];
            return row;
        }

        static void MakeArrow(RectTransform row, string s, Vector2 pos)
        {
            var t = UIFactory.Text(row, "Arrow" + s, s, FontLibrary.Display, 30f, UIColors.TextMuted, new Vector2(1f, 0.5f), pos, new Vector2(40f, RowH), TextAlignmentOptions.Center, false, new Vector2(1f, 0.5f));
            t.raycastTarget = false;
        }

        void OnSliderChanged(float v)
        {
            if (ValueText != null) ValueText.text = _format != null ? _format(v) : v.ToString("0.0");
            _onSlider?.Invoke(v);
        }

        /// <summary>Step an option row by dir (wraps). Returns the new index.</summary>
        public int Step(int dir)
        {
            if (_options == null || _options.Length == 0) return _index;
            _index = (_index + dir + _options.Length) % _options.Length;
            ValueText.text = _options[_index];
            UITween.Scale(ValueText.rectTransform, 1f, 0.25f, Ease.OutBack, 0f, 1.25f);
            Sfx.Play(SfxId.UIClick, 0.7f);
            _onOption?.Invoke(_index);
            return _index;
        }

        public void RefreshText()
        {
            if (Slider != null && ValueText != null) ValueText.text = _format != null ? _format(Slider.value) : Slider.value.ToString("0.0");
        }

        public void SetIndex(int i) { if (_options == null) return; _index = Mathf.Clamp(i, 0, _options.Length - 1); ValueText.text = _options[_index]; }

        void Highlight(bool on)
        {
            if (_rowBg == null) return;
            UITween.Color(_rowBg, on ? new Color(1f, 0.54f, 0.12f, 0.16f) : new Color(1f, 1f, 1f, 0.03f), 0.12f);
            _accent.enabled = on;
            _label.color = on ? UIColors.AccentYellow : UIColors.Text;
        }

        public void OnSelect(BaseEventData e) { Highlight(true); if (Time.frameCount != UIButton.MuteFrame) Sfx.Play(SfxId.UIHover, 0.5f); }
        public void OnDeselect(BaseEventData e) => Highlight(false);

        public void OnPointerEnter(PointerEventData e)
        {
            var es = EventSystem.current;
            if (es != null && Selectable != null && !es.alreadySelecting) es.SetSelectedGameObject(Selectable.gameObject);
        }

        public void OnMove(AxisEventData e)
        {
            if (_options == null) return;
            if (e.moveDir == MoveDirection.Left) Step(-1);
            else if (e.moveDir == MoveDirection.Right) Step(1);
        }

        public void OnPointerClick(PointerEventData e) { if (_options != null) Step(1); }
        public void OnSubmit(BaseEventData e) { if (_options != null) Step(1); }

        void OnDisable() => Highlight(false);
    }
}
