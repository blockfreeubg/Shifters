// =====================================================================================
//  SHIFTERS - SwitcherWheelUI.cs   (UI agent)
//  Visuals of the character switch wheel (logic = CharacterSwitcher, CORE). Listens to
//  SwitcherOpenChanged / SwitcherSelectionChanged and reads CharacterManager. Draws a dimmed
//  backdrop, a radial ring with slowly turning rays, a hub with a needle pointing at the
//  highlighted card, and three tilted cards at CharacterSwitcher.SlotAngles that fly out with
//  stagger + overshoot. Unavailable cards are stamped and shake on selection attempts.
//  PostFx does the desaturation; this class never decides selection.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class SwitcherWheelUI : MonoBehaviour
    {
        public static SwitcherWheelUI Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        const float RadiusY = 300f, RadiusX = 405f;
        static readonly float[] Tilts = { -3f, 4.5f, -4.5f };

        RectTransform _rt, _plate, _rays, _needle, _hub;
        CanvasGroup _group;
        readonly SwitcherCard[] _cards = new SwitcherCard[3];
        TextMeshProUGUI _hubLabel, _hubSub;
        Image _hubRing;
        bool _open;
        int _highlight = -1;
        float _needleAngle, _needleTarget;

        public bool IsOpen => _open;
        public int Highlight => _highlight;
        public SwitcherCard Card(int slot) => slot >= 0 && slot < 3 ? _cards[slot] : null;
        /// <summary>Big hub text as drawn: the highlighted slot's key ("1".."3" on keyboard, the switcher pad
        /// binding e.g. "D-UP" on a gamepad = the card cap), or "SHIFT" with nothing highlighted (tests).</summary>
        public string HubLabelText => _hubLabel != null ? _hubLabel.text : "";
        public float HubLabelFontSize => _hubLabel != null ? _hubLabel.fontSize : 0f;

        public static SwitcherWheelUI Create(Transform parent)
        {
            var rt = UIFactory.Rect("SwitcherWheel", parent);
            UIFactory.Stretch(rt);
            var w = rt.gameObject.AddComponent<SwitcherWheelUI>();
            w._rt = rt;
            w._group = UIFactory.Group(rt.gameObject);
            w._group.blocksRaycasts = false;
            w.Build();
            Instance = w;
            var relay = parent.gameObject.AddComponent<SwitcherWheelRelay>();
            relay.Wheel = w;
            rt.gameObject.SetActive(false);
            return w;
        }

        void OnDestroy()
        {
            ShiftersInput.DeviceChanged -= OnDeviceChanged;
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            UIFactory.Fill(_rt, "Dim", null, new Color(0.02f, 0.03f, 0.05f, 0.42f));
            UIFactory.Fill(_rt, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0f, 0.75f));

            _plate = UIFactory.Rect("Plate", _rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 1000f));
            var glow = UIFactory.Image(_plate, "Glow", UISprites.SoftCircle, UIFactory.WithAlpha(UIColors.Accent, 0.12f));
            glow.rectTransform.sizeDelta = new Vector2(1100f, 1100f);
            var rays = UIFactory.Image(_plate, "Rays", UISprites.RadialLines(40), UIFactory.WithAlpha(UIColors.AccentYellow, 0.09f));
            _rays = rays.rectTransform;
            _rays.sizeDelta = new Vector2(1040f, 1040f);
            var outer = UIFactory.Image(_plate, "Outer", UISprites.Ring(4), UIFactory.WithAlpha(UIColors.Accent, 0.35f));
            outer.rectTransform.sizeDelta = new Vector2(760f, 760f);
            var mid = UIFactory.Image(_plate, "Mid", UISprites.Ring(3), UIFactory.WithAlpha(UIColors.Text, 0.12f));
            mid.rectTransform.sizeDelta = new Vector2(560f, 560f);

            // needle (behind cards)
            _needle = UIFactory.Rect("Needle", _rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(40f, 40f));
            var line = UIFactory.Image(_needle, "Line", UISprites.Rounded(3), UIFactory.WithAlpha(UIColors.Accent, 0.55f));
            var lrt = line.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f); lrt.pivot = new Vector2(0.5f, 0f);
            lrt.anchoredPosition = new Vector2(0f, 60f); lrt.sizeDelta = new Vector2(8f, 44f);
            var tip = UIFactory.Image(_needle, "Tip", UISprites.Triangle, UIColors.Accent);
            var trt = tip.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 92f); trt.sizeDelta = new Vector2(46f, 36f);

            for (int i = 0; i < 3; i++)
            {
                _cards[i] = SwitcherCard.Create(_rt, i);
                float a = CharacterSwitcher.SlotAngles[i] * Mathf.Deg2Rad;
                _cards[i].HomePos = new Vector2(Mathf.Sin(a) * RadiusX, Mathf.Cos(a) * RadiusY + 40f);
                _cards[i].Tilt = Tilts[i];
            }

            _hub = UIFactory.Rect("Hub", _rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(176f, 176f));
            var hsh = UIFactory.Image(_hub, "Shadow", UISprites.SoftCircle, new Color(0f, 0f, 0f, 0.7f));
            hsh.rectTransform.sizeDelta = new Vector2(240f, 240f);
            UIFactory.Image(_hub, "Disc", UISprites.Circle, UIColors.Hex(0x10141A)).rectTransform.sizeDelta = new Vector2(176f, 176f);
            _hubRing = UIFactory.Image(_hub, "Ring", UISprites.Ring(10), UIColors.Accent);
            _hubRing.rectTransform.sizeDelta = new Vector2(176f, 176f);
            _hubLabel = UIFactory.Text(_hub, "Label", "SHIFT", FontLibrary.Display, 34f, UIColors.AccentYellow, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(170f, 44f), TextAlignmentOptions.Center);
            _hubSub = UIFactory.Text(_hub, "Sub", "WHO'S NEXT?", FontLibrary.Label, 17f, UIColors.TextMuted, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(170f, 24f), TextAlignmentOptions.Center);
            _hubSub.characterSpacing = 3f;

            BuildFooter();
        }

        RectTransform _footer;
        bool _footerBuilt, _footerPad;

        /// <summary>True when the footer currently shows the gamepad controls (tests).</summary>
        public bool FooterShowsGamepad => _footerBuilt && _footerPad;

        // Subscribed for the wheel's whole life (not only while open) so the sleeping wheel never holds stale
        // keyboard hints (scene audits read inactive UI too); OnEnable re-checks in case an event was missed.
        void Awake() => ShiftersInput.DeviceChanged += OnDeviceChanged;

        void OnEnable() { if (_footerBuilt) BuildFooter(); ApplyHubKey(); }

        void OnDeviceChanged(bool gamepad) { BuildFooter(); ApplyHubKey(); }

        /// <summary>Control hints under the wheel, for the device the player uses (gp_ui). Keyboard (unchanged):
        /// [1][2][3] OR MOUSE SELECT · [ENTER]/[LMB] SWITCH · [ESC] CANCEL. Gamepad (switcher bindings):
        /// [D-LEFT][D-UP][D-RIGHT] OR STICK SELECT · [A] SWITCH · [B] CANCEL. Rebuilt on a device switch.</summary>
        void BuildFooter()
        {
            bool pad = ShiftersInput.UsingGamepad;
            if (_footerBuilt && pad == _footerPad && _footer != null) return;
            if (_footer != null) { _footer.gameObject.SetActive(false); Destroy(_footer.gameObject); }
            _footerBuilt = true; _footerPad = pad;
            var row = UIFactory.Rect("Footer", _rt, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1400f, 50f), new Vector2(0.5f, 0f));
            _footer = row;
            var bg = UIFactory.Image(row, "Bg", UISprites.Rounded(25), new Color(0.04f, 0.05f, 0.07f, 0.85f));
            UIFactory.Stretch(bg.rectTransform);
            float x = 24f;
            if (!pad)
            {
                x = Key(row, "1", x); x = Key(row, "2", x); x = Key(row, "3", x);
                x = Words(row, "OR MOUSE  SELECT", x);
                x = Sep(row, x);
                x = Key(row, "Enter", x); x = Words(row, "/", x - 4f); x = Key(row, "LMB", x - 4f);
                x = Words(row, "SWITCH", x);
                x = Sep(row, x);
                x = Key(row, "Esc", x);
                x = Words(row, "CANCEL", x);
            }
            else
            {
                x = Pad(row, "Select1", x); x = Pad(row, "Select2", x); x = Pad(row, "Select3", x);
                x = Words(row, "OR STICK  SELECT", x);
                x = Sep(row, x);
                x = Pad(row, "Confirm", x);
                x = Words(row, "SWITCH", x);
                x = Sep(row, x);
                x = Pad(row, "Cancel", x);
                x = Words(row, "CANCEL", x);
            }
            row.sizeDelta = new Vector2(x + 14f, 50f);
        }

        static float Pad(RectTransform row, string action, float x)
        {
            var cap = UIFactory.PadCap(row, KeyLabels.Gamepad(action), 32f);
            cap.anchorMin = cap.anchorMax = new Vector2(0f, 0.5f); cap.pivot = new Vector2(0f, 0.5f);
            cap.anchoredPosition = new Vector2(x, 1f);
            return x + cap.sizeDelta.x + 8f;
        }

        static float Key(RectTransform row, string k, float x)
        {
            var cap = UIFactory.KeyCap(row, k, 32f);
            cap.anchorMin = cap.anchorMax = new Vector2(0f, 0.5f); cap.pivot = new Vector2(0f, 0.5f);
            cap.anchoredPosition = new Vector2(x, 1f);
            return x + cap.sizeDelta.x + 8f;
        }

        static float Words(RectTransform row, string s, float x)
        {
            var t = UIFactory.Text(row, "Txt", s, FontLibrary.Label, 22f, UIColors.Text, TextAlignmentOptions.Left);
            t.characterSpacing = 3f;
            float w = UIFactory.Preferred(t, s).x + 4f;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x + 2f, 0f); rt.sizeDelta = new Vector2(w, 40f);
            return x + w + 10f;
        }

        static float Sep(RectTransform row, float x)
        {
            var dot = UIFactory.Image(row, "Sep", UISprites.Circle, UIColors.Accent);
            var rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x + 6f, 0f); rt.sizeDelta = new Vector2(8f, 8f);
            return x + 28f;
        }

        /// <summary>Hooked by UIManager-created relay (always active) so the wheel can sleep when closed.</summary>
        internal void HandleOpenChanged(bool open)
        {
            if (open) Open(); else Close();
        }

        internal void HandleSelection(int slot) => SetHighlight(slot, false, true);

        void Open()
        {
            var cm = CharacterManager.Instance;
            _rt.gameObject.SetActive(true);
            _open = true;
            UITween.Kill(_group);
            UITween.Fade(_group, 1f, 0.12f, Ease.OutQuad, 0f, 0f);
            UITween.Scale(_plate, 1f, 0.35f, Ease.OutBack, 0f, 0.75f);
            UITween.Scale(_hub, 1f, 0.35f, Ease.OutBack, 0.04f, 0.3f);
            int active = cm != null ? cm.ActiveIndex : -1;
            var mode = cm != null ? cm.AIMode : TeamAIMode.Stay;
            for (int i = 0; i < 3; i++) _cards[i].Bind(cm != null ? cm.Get(i) : null, i == active, mode, true);
            int h = CharacterSwitcher.Instance != null && CharacterSwitcher.Instance.IsOpen ? CharacterSwitcher.Instance.Highlighted : active;
            _highlight = -2;
            SetHighlight(h, true, false);
            bool any = h >= 0 && h < 3 && _cards[h].Character != null;
            for (int i = 0; i < 3; i++)
            {
                var card = _cards[i];
                if (card.Character == null) continue;
                card.Rt.anchoredPosition = new Vector2(0f, 40f);
                card.Rt.localScale = Vector3.one * 0.2f;
                card.Rt.localEulerAngles = Vector3.zero;
                float d = 0.03f + i * 0.05f;
                UITween.Move(card.Rt, card.HomePos, 0.34f, Ease.OutBack, d);
                UITween.Rotate(card.Rt, card.Tilt, 0.4f, Ease.OutBack, d);
                UITween.Scale(card.Rt, SwitcherCard.TargetScale(i == h, any), 0.34f, Ease.OutBack, d);
            }
            _needleAngle = _needleTarget;
            _needle.localEulerAngles = new Vector3(0f, 0f, -_needleAngle);
        }

        void Close()
        {
            if (!_open) return;
            _open = false;
            for (int i = 0; i < 3; i++)
            {
                var card = _cards[i];
                if (!card.gameObject.activeSelf) continue;
                UITween.Move(card.Rt, new Vector2(0f, 40f), 0.16f, Ease.InCubic);
                UITween.Scale(card.Rt, 0.3f, 0.16f, Ease.InCubic);
            }
            UITween.Scale(_plate, 0.85f, 0.16f, Ease.InCubic);
            UITween.Fade(_group, 0f, 0.18f, Ease.OutQuad, 0f, null, () => { if (this != null && !_open) _rt.gameObject.SetActive(false); });
        }

        /// <summary>Hide immediately (scene change, pause, level end).</summary>
        public void ForceClose()
        {
            _open = false;
            UITween.Kill(_group);
            _group.alpha = 0f;
            _rt.gameObject.SetActive(false);
        }

        void SetHighlight(int slot, bool instant, bool applyScale)
        {
            if (slot == _highlight) return;
            _highlight = slot;
            bool any = slot >= 0 && slot < 3 && _cards[slot].Character != null;
            for (int i = 0; i < 3; i++) if (_cards[i].Character != null) _cards[i].SetHighlighted(i == slot, any, instant, applyScale);
            _needle.gameObject.SetActive(any);
            if (any)
            {
                _needleTarget = CharacterSwitcher.SlotAngles[slot];
                var c = _cards[slot];
                ApplyHubKey();
                _hubLabel.color = c.Available ? UIColors.AccentYellow : UIColors.Danger;
                _hubSub.text = c.Available ? (c.Character.IsActive ? "STAY AS YOU" : "SHIFT TO") : "UNAVAILABLE";
                _hubSub.rectTransform.anchoredPosition = new Vector2(0f, -38f);
                _hubLabel.rectTransform.anchoredPosition = new Vector2(0f, 14f);
                _hubRing.color = c.Available ? UIColors.Accent : UIColors.Danger;
                if (!instant) UITween.Punch(_hub, 0.08f, 0.2f);
                if (!c.Available && !instant) c.Deny();
            }
            else
            {
                _hubLabel.text = "SHIFT";
                _hubLabel.fontSize = 34f;
                _hubLabel.rectTransform.anchoredPosition = new Vector2(0f, 10f);
                _hubSub.text = "WHO'S NEXT?";
                _hubSub.rectTransform.anchoredPosition = new Vector2(0f, -24f);
                _hubRing.color = UIColors.Accent;
            }
        }

        const float HubKeySize = 64f, HubKeyMinSize = 24f, HubKeyMaxW = 144f;   // inner chord of the 176 disc (ring 10) at y=14

        /// <summary>Hub key text for the highlighted slot, per device: keyboard keeps the slot number ("3");
        /// a gamepad shows that slot's switcher binding (KeyLabels Select1..3 = D-LEFT/D-UP/D-RIGHT, the same
        /// label as the card cap), on one line at one size for all three slots (the widest label fits the disc,
        /// so the hub does not jump in size while flicking). Re-run on DeviceChanged (open or asleep).</summary>
        void ApplyHubKey()
        {
            if (_hubLabel == null || _highlight < 0 || _highlight > 2 || _cards[_highlight] == null || _cards[_highlight].Character == null) return;
            string num = (_highlight + 1).ToString();
            string label = num;
            float size = HubKeySize;
            _hubLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _hubLabel.fontSize = HubKeySize;
            if (ShiftersInput.UsingGamepad)
            {
                string pad = KeyLabels.Gamepad("Select" + num);
                if (!string.IsNullOrEmpty(pad))
                {
                    label = pad.ToUpperInvariant();
                    for (int i = 1; i <= 3; i++)
                    {
                        string l = KeyLabels.Gamepad("Select" + i);
                        float w = string.IsNullOrEmpty(l) ? 0f : UIFactory.Preferred(_hubLabel, l.ToUpperInvariant()).x;
                        if (w > HubKeyMaxW) size = Mathf.Min(size, Mathf.Floor(HubKeySize * HubKeyMaxW / w));
                    }
                    size = Mathf.Max(HubKeyMinSize, size);
                }
            }
            _hubLabel.fontSize = size;
            _hubLabel.text = label;
        }

        /// <summary>Error feedback for an unavailable slot (CharacterSwitcher may call this on a refused confirm).</summary>
        public void Deny(int slot)
        {
            var c = Card(slot);
            if (c == null || !_open) return;
            c.Deny();
            UITween.Shake(_hub, 10f, 0.3f);
        }

        void Update()
        {
            if (!_open) return;
            float dt = UITime.Delta;
            _rays.localEulerAngles = new Vector3(0f, 0f, _rays.localEulerAngles.z - dt * 6f);
            _needleAngle = Mathf.LerpAngle(_needleAngle, _needleTarget, 1f - Mathf.Exp(-dt * 18f));
            _needle.localEulerAngles = new Vector3(0f, 0f, -_needleAngle);
            var cm = CharacterManager.Instance;
            int active = cm != null ? cm.ActiveIndex : -1;
            var mode = cm != null ? cm.AIMode : TeamAIMode.Stay;
            for (int i = 0; i < 3; i++)
            {
                var card = _cards[i];
                if (card.Character == null) continue;
                bool wasAvail = card.Available;
                card.Refresh(i == active, mode);
                if (wasAvail != card.Available && i == _highlight) { int h = _highlight; _highlight = -2; SetHighlight(h, true, true); }
            }
            if (_highlight >= 0 && _highlight < 3 && !_cards[_highlight].Available && (ShiftersInput.ConfirmPressed || ShiftersInput.LeftClickPressed)) Deny(_highlight);
        }
    }

    /// <summary>Always-active event relay for the (sleeping) switcher wheel.</summary>
    public class SwitcherWheelRelay : MonoBehaviour
    {
        public SwitcherWheelUI Wheel;
        void OnEnable()
        {
            GameEvents.SwitcherOpenChanged += OnOpen;
            GameEvents.SwitcherSelectionChanged += OnSel;
        }
        void OnDisable()
        {
            GameEvents.SwitcherOpenChanged -= OnOpen;
            GameEvents.SwitcherSelectionChanged -= OnSel;
        }
        void OnOpen(bool open) { if (Wheel != null) Wheel.HandleOpenChanged(open); }
        void OnSel(int slot) { if (Wheel != null && Wheel.IsOpen) Wheel.HandleSelection(slot); }
    }
}
