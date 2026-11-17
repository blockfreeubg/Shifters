// =====================================================================================
//  SHIFTERS - ConfirmScreen.cs   (UI agent)
//  Modal "are you sure?" memo: title, body, YES / NO. Opened with UIManager.AskConfirm(...).
//  Used by START OVER, "Continue the campaign from here?" and similar prompts. Esc = NO.
//  Default-focus guard (polish H4, same rule as PauseScreen's RESUME guard): until the player moves the mouse,
//  presses a new key or pushes the stick, the default answer stays selected. The memo pops up under the pointer
//  that just clicked a SHIFT SCHEDULE row, and that hover used to focus NOT TODAY (so Enter cancelled).
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class ConfirmScreen : UIScreen
    {
        TextMeshProUGUI _title, _body;
        RectTransform _panel;
        System.Action _yes, _no;
        bool _defaultYes = true;
        bool _guard, _guardNavNeutral;
        Vector2 _guardPointer;
        /// <summary>The default answer is held selected (player has not taken over yet).</summary>
        public bool DefaultGuardActive => _guard;
        /// <summary>Hover selections by a resting pointer that the guard undid (tests).</summary>
        public int DefaultGuardReverts { get; private set; }
        public UIButton YesButton { get; private set; }
        public UIButton NoButton { get; private set; }
        public string TitleText => _title != null ? _title.text : "";

        public void Setup(string title, string body, string yes, string no, System.Action onYes, System.Action onNo, bool defaultYes)
        {
            EnsureBuiltInactive();
            _title.text = title ?? "";
            _body.text = body ?? "";
            YesButton.SetText(string.IsNullOrEmpty(yes) ? "YES" : yes);
            NoButton.SetText(string.IsNullOrEmpty(no) ? "NO" : no);
            _yes = onYes; _no = onNo; _defaultYes = defaultYes;
            DefaultSelection = defaultYes ? YesButton.Selectable : NoButton.Selectable;
        }

        void EnsureBuiltInactive()
        {
            if (Built) return;
            bool was = gameObject.activeSelf;
            gameObject.SetActive(true);
            EnsureBuilt();
            if (!was) gameObject.SetActive(false);
        }

        protected override void Build()
        {
            Backdrop(0.7f, false);
            var p = UIFactory.Panel(Rect, "Memo", new Vector2(1000f, 460f), UIColors.Hex(0xF3EBDD, 0.99f), 18, true);
            _panel = p.Rect;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = new Vector2(0f, 20f);
            _panel.localEulerAngles = new Vector3(0f, 0f, -1.2f);
            var head = UIFactory.Text(_panel, "Memo", "INTEROFFICE MEMO", FontLibrary.Label, 22f, UIColors.Hex(0x6B5A48), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(900f, 28f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            head.characterSpacing = 12f;
            _title = UIFactory.Text(_panel, "Title", "", FontLibrary.Display, 54f, UIColors.Hex(0x2B2118), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(920f, 70f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(_title, 30f, 54f);
            var rule = UIFactory.Image(_panel, "Rule", null, UIColors.Hex(0x2B2118, 0.35f));
            rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -146f); rule.rectTransform.sizeDelta = new Vector2(840f, 2f);
            _body = UIFactory.Text(_panel, "Body", "", FontLibrary.Body, 32f, UIColors.Hex(0x2B2118), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(860f, 150f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(_body, 20f, 32f);
            NoButton = Btn("NO", () => Answer(false), new Vector2(-210f, 40f), UIButtonStyle.Ghost);
            YesButton = Btn("YES", () => Answer(true), new Vector2(210f, 40f), UIButtonStyle.Primary);
            UIFactory.LinkHorizontal(new List<Selectable> { NoButton.Selectable, YesButton.Selectable }, true);
            DefaultSelection = YesButton.Selectable;
        }

        UIButton Btn(string label, System.Action a, Vector2 pos, UIButtonStyle style)
        {
            var b = UIFactory.Button(_panel, "Btn_" + label, label, a, new Vector2(380f, 72f), style, 28f);
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0f); b.Rt.pivot = new Vector2(0.5f, 0f);
            b.Rt.anchoredPosition = pos;
            return b;
        }

        /// <summary>Answer the prompt (tests / bots). Pops this screen first, then runs the callback.</summary>
        public void Answer(bool yes)
        {
            var cb = yes ? _yes : _no;
            _yes = null; _no = null;
            if (Manager != null && Manager.Top == this) Manager.Pop();
            cb?.Invoke();
        }

        protected override void OnOpened()
        {
            DefaultSelection = _defaultYes ? YesButton.Selectable : NoButton.Selectable;
            UITween.Scale(_panel, 1f, 0.35f, Ease.OutBack, 0f, 0.7f);
            UITween.Rotate(_panel, -1.2f, 0.4f, Ease.OutBack, 0f, 5f);
            Sfx.Play(SfxId.UIPop, 0.8f);
            _guard = true;
            _guardPointer = ShiftersInput.PointerPosition;
            _guardNavNeutral = ShiftersInput.NavigateStick.sqrMagnitude < 0.04f;
        }

        protected override void OnClosed() => _guard = false;

        // after the EventSystem's Update of the same frame: undo a hover selection made by a pointer that has not moved
        void LateUpdate()
        {
            if (!_guard) return;
            if (!IsOpen || DefaultSelection == null || PlayerTookOver()) { _guard = false; return; }
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.currentSelectedGameObject != DefaultSelection.gameObject) { SelectDefault(); DefaultGuardReverts++; }
        }

        bool PlayerTookOver()
        {
            if ((ShiftersInput.PointerPosition - _guardPointer).sqrMagnitude > 9f) return true;   // the mouse really moved
            if (ShiftersInput.AnyKeyPressed) return true;                                          // a NEW key / button press
            float nav = ShiftersInput.NavigateStick.sqrMagnitude;                                  // arrows / sticks
            if (!_guardNavNeutral) { if (nav < 0.04f) _guardNavNeutral = true; return false; }     // held since before the memo
            return nav > 0.25f;
        }

        public override void OnBack() { Sfx.Play(SfxId.UIBack); Answer(false); }
    }
}
