// =====================================================================================
//  SHIFTERS - MeTvPip.cs   (ui_hud agent)
//  CAMPAIGN §8.1/§15.2 picture-in-picture: a little ME-TV set (gold, "ME-TV LIVE" bug,
//  static, scanlines) showing VillainStudio's render texture while Max broadcasts, or the
//  coffee-brown PERCY RADIO skin (antenna, grille, waveform) for Percy. Falls back to a
//  drawn face when no render texture exists. Lives on its own canvas (sort order 35) so
//  Level Complete reaction lines show above that screen; hidden while paused.
//  Placement: top-left under the objective panel (the WebGL template's toast area owns
//  the top-right corner, the perspective chips/toasts sit under it).
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class MeTvPip : MonoBehaviour
    {
        public const float BodyW = 300f, BodyH = 236f, ScreenW = 272f, ScreenH = 176f;
        public static readonly Vector2 HomePos = new Vector2(26f, -132f);

        public bool Visible { get; private set; }
        public bool RadioSkin { get; private set; }
        public string SpeakerId { get; private set; }
        public RectTransform Rt { get; private set; }
        public bool HasLiveTexture => _raw != null && _raw.enabled;
        /// <summary>Full-screen ME-TV (StoryPlayer.Cue("tv", "MAX", channel) in C00/C16) is up.</summary>
        public bool FullScreen { get; private set; }
        /// <summary>Channel shown in full-screen mode ("ME NEWS"...).</summary>
        public string Channel { get; private set; }
        /// <summary>Scale of the set in full-screen mode (fits between the letterbox top bar and the DialogueBox).</summary>
        public float FullScale { get; private set; } = 1f;

        CanvasGroup _group;
        Image _body, _border, _dot, _antenna, _grille;
        RectTransform _screen, _content, _wave;
        RawImage _raw, _noise, _scan;
        Image _flash;
        TextMeshProUGUI _bug, _channel, _name, _caption;
        RectTransform _captionRt, _lower, _tickerRt;
        TextMeshProUGUI _lowerName, _ticker;
        CampaignHudStyle.DrawnFace _maxFace, _percyFace;
        readonly Image[] _bars = new Image[9];
        bool _broadcastOn, _lineUp, _talking, _studioBegun;
        float _offAt = -1f, _age, _staticBurst;

        public static MeTvPip Create(Transform parent)
        {
            var rt = UIFactory.Rect("MeTvPip", parent, new Vector2(0f, 1f), HomePos, new Vector2(BodyW, BodyH), new Vector2(0f, 1f));
            var p = rt.gameObject.AddComponent<MeTvPip>();
            p.Rt = rt;
            p.Build();
            return p;
        }

        void Build()
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;

            _antenna = UIFactory.Image(Rt, "Antenna", UISprites.Rounded(3), UIColors.Hex(0x8A8F99));
            var art = _antenna.rectTransform;
            // side-mounted telescopic antenna leaning out to the right. It must stay below the set's top edge:
            // the objective panel ends only ~17 px above the PiP (polish H3: it used to poke into the objective text)
            art.anchorMin = art.anchorMax = new Vector2(1f, 1f); art.pivot = new Vector2(0.5f, 0f);
            art.anchoredPosition = new Vector2(-10f, -66f); art.sizeDelta = new Vector2(6f, 58f); art.localRotation = Quaternion.Euler(0, 0, -34f);
            var knob = UIFactory.Image(_antenna.transform, "Knob", UISprites.Circle, UIColors.Hex(0xFF4D5E));
            var krt = knob.rectTransform; krt.anchorMin = krt.anchorMax = new Vector2(0.5f, 1f); krt.sizeDelta = new Vector2(14f, 14f); krt.anchoredPosition = Vector2.zero;

            var sh = UIFactory.Image(Rt, "Shadow", UISprites.Shadow(22, 22), new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Expand(sh.rectTransform, 22f, new Vector2(0f, -7f));
            _body = UIFactory.Fill(Rt, "Body", UISprites.Rounded(22), CampaignHudStyle.TvBody);
            _border = UIFactory.Fill(Rt, "Border", UISprites.RoundedOutline(22, 4), CampaignHudStyle.MaxGold);

            _screen = UIFactory.Rect("Screen", Rt, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(ScreenW, ScreenH), new Vector2(0.5f, 1f));
            var maskImg = UIFactory.Fill(_screen, "Mask", UISprites.Rounded(14), Color.black);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _content = UIFactory.Rect("Content", maskImg.transform); UIFactory.Stretch(_content);
            _maxFace = CampaignHudStyle.DrawMax(_content);
            _percyFace = CampaignHudStyle.DrawPercy(_content);
            var rawRt = UIFactory.Rect("Live", _content); UIFactory.Stretch(rawRt);
            _raw = rawRt.gameObject.AddComponent<RawImage>(); _raw.raycastTarget = false; _raw.enabled = false;
            _wave = UIFactory.Rect("Wave", _content, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(180f, 44f), new Vector2(0.5f, 0f));
            for (int i = 0; i < _bars.Length; i++)
            {
                var b = UIFactory.Image(_wave, "Bar" + i, UISprites.Rounded(3), UIColors.Hex(0x7CFFB2));
                var brt = b.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = new Vector2(10f + i * 20f, 0f); brt.sizeDelta = new Vector2(10f, 10f);
                _bars[i] = b;
            }
            var nRt = UIFactory.Rect("Static", _content); UIFactory.Stretch(nRt);
            _noise = nRt.gameObject.AddComponent<RawImage>(); _noise.raycastTarget = false; _noise.texture = CampaignHudStyle.Noise;
            _noise.color = new Color(1f, 1f, 1f, 0.12f);
            var sRt = UIFactory.Rect("Scanlines", _content); UIFactory.Stretch(sRt);
            _scan = sRt.gameObject.AddComponent<RawImage>(); _scan.raycastTarget = false; _scan.texture = CampaignHudStyle.Scanlines; _scan.uvRect = new Rect(0f, 0f, 1f, 44f);
            var vig = UIFactory.Fill(_content, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0f, 0.45f));
            vig.raycastTarget = false;
            _flash = UIFactory.Fill(_content, "Flash", null, Color.white);
            _flash.color = new Color(1f, 1f, 1f, 0f);

            // channel bug + channel number
            _dot = UIFactory.Image(_content, "LiveDot", UISprites.Circle, UIColors.Hex(0xFF3B4E));
            var drt = _dot.rectTransform; drt.anchorMin = drt.anchorMax = new Vector2(0f, 1f); drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = new Vector2(10f, -11f); drt.sizeDelta = new Vector2(12f, 12f);
            _bug = UIFactory.Text(_content, "Bug", "ME-TV LIVE", FontLibrary.Heavy, 17f, CampaignHudStyle.MaxGold, new Vector2(0f, 1f), new Vector2(27f, -6f), new Vector2(170f, 22f), TextAlignmentOptions.Left);
            _bug.characterSpacing = 3f;
            _channel = UIFactory.Text(_content, "Channel", "CH 1", FontLibrary.Label, 16f, new Color(1f, 1f, 1f, 0.8f), new Vector2(1f, 1f), new Vector2(-10f, -7f), new Vector2(80f, 20f), TextAlignmentOptions.Right);

            // lower third (full-screen broadcasts only): channel plate + scrolling ticker
            _lower = UIFactory.Rect("LowerThird", _content, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(ScreenW - 16f, 30f), new Vector2(0.5f, 0f));
            UIFactory.Fill(_lower, "Bg", UISprites.Rounded(4), new Color(0.05f, 0.05f, 0.08f, 0.88f));
            var plate = UIFactory.Image(_lower, "Plate", UISprites.Rounded(4), CampaignHudStyle.MaxGold);
            var prt = plate.rectTransform; prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(78f, 0f); prt.anchoredPosition = Vector2.zero;
            _lowerName = UIFactory.Text(plate.rectTransform, "Name", "ME NEWS", FontLibrary.Heavy, 13f, UIColors.Hex(0x12161C), TextAlignmentOptions.Center, false);
            UIFactory.Stretch(_lowerName.rectTransform, 3f, 3f, 2f, 2f);
            UIFactory.AutoSize(_lowerName, 8f, 13f);
            var tickMask = UIFactory.Rect("TickerMask", _lower, new Vector2(0f, 0f), Vector2.zero, Vector2.zero, new Vector2(0f, 0f));
            tickMask.anchorMin = new Vector2(0f, 0f); tickMask.anchorMax = new Vector2(1f, 1f); tickMask.offsetMin = new Vector2(84f, 0f); tickMask.offsetMax = new Vector2(-4f, 0f);
            tickMask.gameObject.AddComponent<RectMask2D>();
            _ticker = UIFactory.Text(tickMask, "Ticker", "", FontLibrary.Label, 13f, new Color(1f, 1f, 1f, 0.92f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1400f, 24f), TextAlignmentOptions.MidlineLeft);
            _tickerRt = _ticker.rectTransform; _tickerRt.pivot = new Vector2(0f, 0.5f);
            _lower.gameObject.SetActive(false);

            // grille + name under the screen
            _grille = UIFactory.Image(Rt, "Grille", UISprites.Stripes, new Color(1f, 1f, 1f, 0.12f));
            var grt = _grille.rectTransform; grt.anchorMin = grt.anchorMax = new Vector2(1f, 0f); grt.pivot = new Vector2(1f, 0f);
            grt.anchoredPosition = new Vector2(-16f, 12f); grt.sizeDelta = new Vector2(64f, 24f);
            _name = UIFactory.Text(Rt, "Name", "MAX", FontLibrary.Heavy, 21f, CampaignHudStyle.MaxGold, new Vector2(0f, 0f), new Vector2(18f, 12f), new Vector2(200f, 26f), TextAlignmentOptions.Left);
            _name.characterSpacing = 4f;

            // caption (only when the DialogueBox is covered by a menu, e.g. Level Complete reactions)
            _captionRt = UIFactory.Rect("Caption", Rt, new Vector2(0f, 0f), new Vector2(0f, -8f), new Vector2(BodyW + 160f, 70f), new Vector2(0f, 1f));
            UIFactory.Fill(_captionRt, "Bg", UISprites.Rounded(12), UIFactory.WithAlpha(UIColors.Background, 0.92f));
            _caption = UIFactory.Text(_captionRt, "Text", "", FontLibrary.Body, 22f, UIColors.Text, TextAlignmentOptions.MidlineLeft, true);
            UIFactory.Stretch(_caption.rectTransform, 14f, 14f, 6f, 6f);
            UIFactory.AutoSize(_caption, 16f, 22f);
            _captionRt.gameObject.SetActive(false);
            ApplySkin(false);
        }

        void ApplySkin(bool radio)
        {
            RadioSkin = radio;
            _body.color = radio ? CampaignHudStyle.PercyBody : CampaignHudStyle.TvBody;
            _border.color = radio ? CampaignHudStyle.PercyBrown : CampaignHudStyle.MaxGold;
            _antenna.gameObject.SetActive(radio);
            _bug.text = radio ? "PERCY RADIO" : "ME-TV LIVE";
            _bug.color = radio ? UIColors.Hex(0x7CFFB2) : CampaignHudStyle.MaxGold;
            _dot.color = radio ? UIColors.Hex(0x7CFFB2) : UIColors.Hex(0xFF3B4E);
            _channel.text = radio ? "CH 2" : "CH 1";
            _name.color = radio ? CampaignHudStyle.PercyBrown : CampaignHudStyle.MaxGold;
            _wave.gameObject.SetActive(radio);
            _maxFace.Root.gameObject.SetActive(!radio);
            _percyFace.Root.gameObject.SetActive(radio);
        }

        /// <summary>A broadcast line: turn on (if needed) with the speaker's skin and keep the line's caption.</summary>
        public void ShowLine(StoryLine l)
        {
            _lineUp = true;
            _talking = true;
            Open(l.SpeakerId, !string.IsNullOrEmpty(l.SpeakerName) ? l.SpeakerName : l.SpeakerId);
            if (!RadioSkin) CampaignHudStyle.SetFace(_maxFace, l.Face);
            if (_studioBegun) VillainStudio.Instance?.Say(l);
            _caption.text = l.Text ?? "";
            _staticBurst = 0.25f;
        }

        public void LineEnded()
        {
            _lineUp = false;
            _talking = false;
            if (!_broadcastOn) _offAt = UITime.Now + 0.6f;
        }

        /// <summary>GameEvents.BroadcastChanged: on keeps the set up between lines; off closes it.</summary>
        public void SetBroadcast(bool on, string speakerId)
        {
            _broadcastOn = on;
            if (on) Open(speakerId, speakerId);
            else if (!_lineUp) _offAt = UITime.Now + 0.4f;
        }

        void Open(string speakerId, string displayName)
        {
            SpeakerId = speakerId;
            bool radio = CampaignHudStyle.IsPercy(speakerId);
            if (radio != RadioSkin || !Visible) ApplySkin(radio);
            _name.text = string.IsNullOrEmpty(displayName) ? (radio ? "PERCY" : "MAX") : displayName.ToUpperInvariant();
            _offAt = -1f;
            // visuals' VillainStudio self-listens to the broadcast events; if it did not exist yet it missed this
            // one, so the PiP creates it and keeps it live manually (Begin/End) for this broadcast.
            bool fresh = VillainStudio.Instance == null;
            RefreshTexture(true);
            var studio = VillainStudio.Instance;
            if (fresh && studio != null && !_studioBegun) { studio.Begin(speakerId); _studioBegun = true; }
            if (Visible) return;
            Visible = true;
            Rt.localScale = Vector3.one;
            UITween.Kill(_group); UITween.Kill(_screen);
            _group.alpha = 1f;
            _screen.localScale = new Vector3(1f, 0.02f, 1f);
            UITween.Scale(_screen, Vector3.one, 0.22f, Ease.OutCubic, 0.05f);
            UITween.Move(Rt, HomePos, 0.3f, Ease.OutBack, 0f, HomePos + new Vector2(-BodyW - 40f, 0f));
            _flash.color = new Color(1f, 1f, 1f, 0.9f);
            UITween.Color(_flash, new Color(1f, 1f, 1f, 0f), 0.35f, Ease.OutQuad, 0.1f);
            Sfx.Play(SfxId.TVStatic, 0.35f, radio ? 0.8f : 1.1f);
        }

        public void Close(bool instant = false)
        {
            _offAt = -1f;
            _broadcastOn = false; _lineUp = false; _talking = false;
            if (_studioBegun) { _studioBegun = false; VillainStudio.Instance?.End(); }
            if (FullScreen || _fsAmt > 0f)
            {
                bool wasFull = FullScreen;
                FullScreen = false; _fsAmt = 0f; _lower.gameObject.SetActive(false);
                if (wasFull) ApplySkin(RadioSkin);
                Rt.localScale = Vector3.one; Rt.anchoredPosition = HomePos;
            }
            if (!Visible) return;
            Visible = false;
            UITween.Kill(_group); UITween.Kill(_screen);
            if (instant) { _group.alpha = 0f; return; }
            UITween.Scale(_screen, new Vector3(1f, 0.02f, 1f), 0.14f, Ease.InCubic, 0f, null, () =>
                UITween.Fade(_group, 0f, 0.16f));
        }

        /// <summary>Full-screen ME-TV takeover (CAMPAIGN D30, C00/C16): the set grows to the centre of the screen with the
        /// channel's lower third and ticker. on=false (Cue arg "off" / scene end) shrinks it back to the corner, or closes it
        /// when no broadcast / line keeps it up.</summary>
        public void SetFullScreen(bool on, string channel = null)
        {
            if (on)
            {
                Channel = string.IsNullOrEmpty(channel) ? "ME NEWS" : channel.Trim().ToUpperInvariant();
                if (!Visible) Open("MAX", "MAX ME");
                else if (RadioSkin) { ApplySkin(false); _name.text = "MAX ME"; }
                _offAt = -1f;
                FullScreen = true;
                _bug.text = "ME-TV LIVE";
                _channel.text = ChannelNumber(Channel);
                _lowerName.text = Channel;
                var tk = TickerFor(Channel); _ticker.text = tk + tk;
                _tickerX = 0f;
                _lower.gameObject.SetActive(true);
                _captionRt.gameObject.SetActive(false);
                UITween.Kill(Rt);
                _staticBurst = 0.45f;
                Sfx.Play(SfxId.TVStatic, 0.45f, 0.9f);
                return;
            }
            if (!FullScreen) return;
            FullScreen = false;
            _lower.gameObject.SetActive(false);
            ApplySkin(RadioSkin);
            if (!_broadcastOn && !_lineUp) { Close(); return; }
            _staticBurst = 0.3f;
        }

        float _tickerX, _fsAmt;

        /// <summary>Full-screen geometry for the current canvas size: the set fits between the letterbox top bar and the
        /// DialogueBox (+ speaker plate), centred. Called every frame while full screen (resolution changes) and by tests.</summary>
        public void Relayout(bool snap = false)
        {
            if (snap) _fsAmt = FullScreen ? 1f : 0f;
            if (_fsAmt <= 0f && !FullScreen)
            {
                if (Rt.localScale.x != 1f) { Rt.localScale = Vector3.one; Rt.anchoredPosition = HomePos; }
                return;
            }
            var parent = Rt.parent as RectTransform;
            float W = parent != null && parent.rect.width > 10f ? parent.rect.width : 1920f;
            float H = parent != null && parent.rect.height > 10f ? parent.rect.height : 1080f;
            float top = CampaignLetterbox.BarHeight + 30f, bottom = DialogueBox.BottomY + DialogueBox.Height + 56f;
            FullScale = Mathf.Max(1f, Mathf.Min((H - top - bottom) / BodyH, (W - 160f) / BodyW));
            var target = new Vector2((W - BodyW * FullScale) * 0.5f, -top);
            float e = Easing.Evaluate(Ease.InOutCubic, _fsAmt);
            Rt.anchoredPosition = Vector2.LerpUnclamped(HomePos, target, e);
            float sc = Mathf.LerpUnclamped(1f, FullScale, e);
            Rt.localScale = new Vector3(sc, sc, 1f);
        }

        static string ChannelNumber(string ch)
        {
            if (ch.Contains("WEATHER")) return "CH 3";
            if (ch.Contains("SPORT")) return "CH 4";
            if (ch.Contains("NEWS")) return "CH 1";
            return "CH 1";
        }

        static string TickerFor(string ch)
        {
            if (ch.Contains("WEATHER"))
                return "TODAY: GREY  +++  TOMORROW: GREY  +++  WEEKEND: ME  +++  COLOR WARNING LIFTED (COLOR CONFISCATED)  +++  ";
            if (ch.Contains("SPORT"))
                return "FINAL SCORE: ME 1, EVERYONE ELSE 0  +++  RELAY RACES BANNED  +++  TUG-OF-WAR NOW A ONE-MAN EVENT (HE WON)  +++  ";
            return "BREAKING: LOCAL MAN STILL IN CHARGE OF EVERYTHING  +++  TEAMWORK DECLARED A FIRE HAZARD  +++  GROUP PROJECTS NOW A CRIME  +++  ";
        }

        void RefreshTexture(bool ensure = false)
        {
            var tex = CampaignHudCompat.VillainTexture(ensure);
            _raw.texture = tex;
            _raw.enabled = tex != null;
        }

        /// <summary>Show the caption strip (true while a menu screen covers the DialogueBox).</summary>
        public void SetCaptionVisible(bool on)
        {
            bool want = on && Visible && !FullScreen && _lineUp && !string.IsNullOrEmpty(_caption.text);
            if (_captionRt.gameObject.activeSelf != want) _captionRt.gameObject.SetActive(want);
        }

        void Update()
        {
            if (!Visible) return;
            float dt = UITime.Delta;
            _age += dt;
            if (_offAt > 0f && UITime.Now >= _offAt) { Close(); return; }
            // static: jitter the noise uv every frame, bursts on line changes
            _staticBurst = Mathf.Max(0f, _staticBurst - dt);
            float a = (_raw.enabled ? 0.08f : 0.12f) + _staticBurst * 2f;
            _noise.color = new Color(1f, 1f, 1f, Mathf.Min(0.7f, a));
            _noise.uvRect = new Rect(Mathf.Repeat(_age * 7.31f, 1f), Mathf.Repeat(_age * 3.17f, 1f), 3f, 2.4f);
            _scan.uvRect = new Rect(0f, Mathf.Repeat(-_age * 0.6f, 1f), 1f, 44f);
            var dc = _dot.color; dc.a = Mathf.Repeat(_age, 1f) < 0.6f ? 1f : 0.25f; _dot.color = dc;
            float talk = _talking ? Mathf.Abs(Mathf.Sin(_age * 17f)) * (0.6f + 0.4f * Mathf.Sin(_age * 5.3f)) : 0f;
            if (RadioSkin)
            {
                CampaignHudStyle.Talk(_percyFace, talk);
                for (int i = 0; i < _bars.Length; i++)
                {
                    float h = _talking ? 8f + 34f * Mathf.Abs(Mathf.Sin(_age * (9f + i * 1.7f) + i)) : 6f;
                    _bars[i].rectTransform.sizeDelta = new Vector2(10f, h);
                }
            }
            else CampaignHudStyle.Talk(_maxFace, talk);
            if (FullScreen || _fsAmt > 0f)
            {
                _fsAmt = Mathf.MoveTowards(_fsAmt, FullScreen ? 1f : 0f, dt / 0.45f);
                Relayout();
                if (!FullScreen && _fsAmt <= 0f) { Rt.localScale = Vector3.one; Rt.anchoredPosition = HomePos; }
            }
            if (FullScreen)
            {
                _tickerX -= dt * 60f;
                float tw = _ticker.preferredWidth;
                if (tw > 10f && _tickerX < -tw * 0.5f) _tickerX += tw * 0.5f;
                _tickerRt.anchoredPosition = new Vector2(_tickerX, 0f);
            }
            if ((Time.frameCount & 31) == 0) RefreshTexture();
        }
    }
}
