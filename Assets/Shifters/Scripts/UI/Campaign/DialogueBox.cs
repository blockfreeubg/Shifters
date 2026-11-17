// =====================================================================================
//  SHIFTERS - DialogueBox.cs   (ui_hud agent)
//  CAMPAIGN §15.2 subtitle bar: bottom centre, speaker plate in the speaker color,
//  portrait (interns: PortraitStudio via PortraitView; Max/Percy: VillainStudio RT or a
//  drawn face), typewriter 45 chars/s (x SettingsData.SubtitleSpeed) with voice blips,
//  auto-hide after max(2.5 s, chars/15 s) when the line has no Duration and no
//  StoryLineHidden arrives. Driven by GameEvents.StoryLineShown / StoryLineHidden.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class DialogueBox : MonoBehaviour
    {
        public const float Width = 1040f, Height = 112f, BottomY = 30f;
        public const float CharsPerSecond = 45f;

        /// <summary>Play SfxId.DialogueBlip while typing (story may turn this off if it plays VoiceSynth itself).</summary>
        public static bool Blips = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Blips = true; }

        public bool Visible { get; private set; }
        public bool Typing => Visible && _shown < _total;
        public StoryLine Current { get; private set; }
        public string CurrentText => _text != null ? _text.text : "";
        public RectTransform Rt { get; private set; }

        CanvasGroup _group;
        UIPanel _panel;
        Image _strip, _plateBg, _next;
        TextMeshProUGUI _text, _plateText;
        RectTransform _plate, _portraitHolder;
        PortraitView _portrait;
        RawImage _tvRaw;
        RectTransform _tvRoot;
        CampaignHudStyle.DrawnFace _maxFace, _percyFace;
        float _shown, _total, _age, _hideAt, _lastBlip;
        int _lastVisible;
        bool _villain;

        public static DialogueBox Create(Transform parent)
        {
            var rt = UIFactory.Rect("DialogueBox", parent, new Vector2(0.5f, 0f), new Vector2(0f, BottomY), new Vector2(Width, Height), new Vector2(0.5f, 0f));
            var d = rt.gameObject.AddComponent<DialogueBox>();
            d.Rt = rt;
            d.Build();
            return d;
        }

        void Build()
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            _panel = UIFactory.Panel(Rt, "Panel", new Vector2(Width, Height), UIFactory.WithAlpha(UIColors.Background, 0.93f), 18, true, UIColors.Accent, false);
            UIFactory.Stretch(_panel.Rect);
            _strip = UIFactory.Image(Rt, "Strip", UISprites.Rounded(4), UIColors.Accent);
            var srt = _strip.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 0.5f);
            srt.offsetMin = new Vector2(8f, 12f); srt.offsetMax = new Vector2(14f, -12f);

            // portrait slot (intern portrait or a little TV for Max/Percy)
            _portraitHolder = UIFactory.Rect("PortraitSlot", Rt, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(88f, 88f), new Vector2(0f, 0.5f));
            _portrait = PortraitView.Create(_portraitHolder, 88f, false);
            UIFactory.Stretch((RectTransform)_portrait.transform);
            _tvRoot = UIFactory.Rect("Tv", _portraitHolder);
            UIFactory.Stretch(_tvRoot);
            var tvBg = UIFactory.Fill(_tvRoot, "Mask", UISprites.Rounded(12), Color.white);
            var mask = tvBg.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var inner = (RectTransform)tvBg.transform;
            _maxFace = CampaignHudStyle.DrawMax(inner);
            _percyFace = CampaignHudStyle.DrawPercy(inner);
            var rawRt = UIFactory.Rect("Raw", inner); UIFactory.Stretch(rawRt);
            _tvRaw = rawRt.gameObject.AddComponent<RawImage>(); _tvRaw.raycastTarget = false; _tvRaw.enabled = false;
            var scan = UIFactory.Rect("Scan", inner); UIFactory.Stretch(scan);
            var sraw = scan.gameObject.AddComponent<RawImage>(); sraw.raycastTarget = false; sraw.texture = CampaignHudStyle.Scanlines; sraw.uvRect = new Rect(0f, 0f, 1f, 22f);
            UIFactory.Fill(_tvRoot, "Frame", UISprites.RoundedOutline(12, 3), CampaignHudStyle.MaxGold);
            _tvRoot.gameObject.SetActive(false);

            // speaker plate straddling the top edge
            _plate = UIFactory.Rect("Plate", Rt, new Vector2(0f, 1f), new Vector2(126f, 0f), new Vector2(200f, 36f), new Vector2(0f, 0.5f));
            var psh = UIFactory.Image(_plate, "Shadow", UISprites.Shadow(14, 18), new Color(0f, 0f, 0f, 0.5f));
            UIFactory.Expand(psh.rectTransform, 12f, new Vector2(0f, -4f));
            _plateBg = UIFactory.Fill(_plate, "Bg", UISprites.Rounded(18), UIColors.Accent);
            _plateText = UIFactory.Text(_plate, "Name", "", FontLibrary.Heavy, 22f, CampaignHudStyle.Ink, TextAlignmentOptions.Center);
            _plateText.characterSpacing = 4f;
            UIFactory.Stretch(_plateText.rectTransform, 14f, 14f, 0f, 2f);

            _text = UIFactory.Text(Rt, "Text", "", FontLibrary.Body, 30f, UIColors.Text, TextAlignmentOptions.Left, true);
            var trt = _text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(128f, 12f); trt.offsetMax = new Vector2(-46f, -22f);
            _text.alignment = TextAlignmentOptions.MidlineLeft;
            UIFactory.AutoSize(_text, 22f, 30f);
            _text.lineSpacing = -6f;

            _next = UIFactory.Image(Rt, "Next", UISprites.Triangle, UIColors.AccentYellow);
            var nrt = _next.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0f); nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(-24f, 22f); nrt.sizeDelta = new Vector2(16f, 13f);
            nrt.localRotation = Quaternion.Euler(0f, 0f, 180f);
            _next.enabled = false;
        }

        /// <summary>Seconds a line stays up when the story gives no Duration: max(2.5, chars/15) / SubtitleSpeed.</summary>
        public static float DurationFor(StoryLine l)
        {
            if (l.Duration > 0f) return l.Duration;
            int n = string.IsNullOrEmpty(l.Text) ? 0 : l.Text.Length;
            return Mathf.Max(2.5f, n / 15f) / SubtitleSpeed;
        }

        public static float SubtitleSpeed
        {
            get
            {
                var s = GameSession.Instance != null ? GameSession.Instance.Settings : null;
                return s != null ? Mathf.Clamp(s.SubtitleSpeed, 0.5f, 2f) : 1f;
            }
        }

        public void Show(StoryLine l)
        {
            Current = l;
            Color sc = CampaignHudStyle.SpeakerColor(l);
            string name = !string.IsNullOrEmpty(l.SpeakerName) ? l.SpeakerName : (l.SpeakerId ?? "");
            name = name.ToUpperInvariant();
            _plateText.text = name;
            float pw = Mathf.Max(120f, UIFactory.Preferred(_plateText, name).x + 36f);
            _plate.sizeDelta = new Vector2(pw, 36f);
            _plate.gameObject.SetActive(name.Length > 0);
            _plateBg.color = sc;
            _plateText.color = UIFactory.ContrastText(sc);
            _strip.color = sc;
            if (_panel.Border != null) _panel.Border.color = UIFactory.WithAlpha(sc, 0.85f);

            // portrait
            _villain = CampaignHudStyle.IsMax(l.SpeakerId) || CampaignHudStyle.IsPercy(l.SpeakerId);
            var intern = _villain ? null : CampaignHudStyle.FindIntern(l.SpeakerId);
            bool hasPortrait = _villain || intern != null;
            _portraitHolder.gameObject.SetActive(hasPortrait);
            _tvRoot.gameObject.SetActive(_villain);
            _portrait.gameObject.SetActive(!_villain && intern != null);
            if (intern != null) _portrait.Bind(intern.Slot, intern.Profile);
            if (_villain)
            {
                bool percy = CampaignHudStyle.IsPercy(l.SpeakerId);
                _maxFace.Root.gameObject.SetActive(!percy);
                _percyFace.Root.gameObject.SetActive(percy);
                CampaignHudStyle.SetFace(_maxFace, l.Face);
                var tex = CampaignHudCompat.VillainTexture();
                _tvRaw.texture = tex; _tvRaw.enabled = tex != null;
                var frame = _tvRoot.Find("Frame") as RectTransform;
                if (frame != null) frame.GetComponent<Image>().color = sc;
            }
            var trt = _text.rectTransform;
            trt.offsetMin = new Vector2(hasPortrait ? 128f : 34f, 12f);
            _plate.anchoredPosition = new Vector2(hasPortrait ? 126f : 30f, 0f);

            _text.text = l.Text ?? "";
            _text.maxVisibleCharacters = 0;
            _text.ForceMeshUpdate();
            _total = _text.textInfo != null ? _text.textInfo.characterCount : _text.text.Length;
            _shown = 0f; _lastVisible = 0; _age = 0f;
            _hideAt = _total / (CharsPerSecond * SubtitleSpeed) + DurationFor(l) + (l.Duration > 0f ? 1.5f : 0f);
            _next.enabled = false;

            if (!Visible)
            {
                Visible = true;
                UITween.Kill(_group);
                UITween.Fade(_group, 1f, 0.18f);
                UITween.Move(Rt, new Vector2(0f, BottomY), 0.28f, Ease.OutBack, 0f, new Vector2(0f, BottomY - 26f));
            }
            else UITween.Punch(_plate, 0.08f, 0.2f);
        }

        /// <summary>Reveal the whole line now (first "advance" press while typing).</summary>
        public void CompleteTypewriter()
        {
            if (!Visible) return;
            _shown = _total;
            _text.maxVisibleCharacters = 99999;
        }

        public void Hide(bool instant = false)
        {
            if (!Visible && !instant) return;
            Visible = false;
            _next.enabled = false;
            UITween.Kill(_group);
            if (instant) { _group.alpha = 0f; return; }
            UITween.Fade(_group, 0f, 0.2f);
        }

        void Update()
        {
            if (!Visible) return;
            float dt = UITime.Delta;
            _age += dt;
            if (_shown < _total)
            {
                _shown = Mathf.Min(_total, _shown + dt * CharsPerSecond * SubtitleSpeed);
                int vis = Mathf.FloorToInt(_shown);
                if (vis != _lastVisible)
                {
                    _text.maxVisibleCharacters = vis;
                    if (Blips && vis - _lastVisible >= 1 && UITime.Now - _lastBlip > 0.07f && !IsSpace(vis - 1))
                    {
                        _lastBlip = UITime.Now;
                        float p = CampaignHudStyle.BlipPitch(Current.SpeakerId) * (0.92f + 0.16f * Mathf.PerlinNoise(vis * 0.37f, 0.5f));
                        Sfx.Play(SfxId.DialogueBlip, 0.32f, p);
                    }
                    _lastVisible = vis;
                }
                if (_villain) CampaignHudStyle.Talk(CampaignHudStyle.IsPercy(Current.SpeakerId) ? _percyFace : _maxFace, Mathf.Abs(Mathf.Sin(_age * 18f)));
            }
            else
            {
                if (!_next.enabled) { _next.enabled = true; if (_villain) { CampaignHudStyle.Talk(_maxFace, 0f); CampaignHudStyle.Talk(_percyFace, 0f); } }
                var c = _next.color; c.a = 0.55f + 0.45f * Mathf.Sin(_age * 6f); _next.color = c;
            }
            if (_age >= _hideAt) Hide();
        }

        bool IsSpace(int index)
        {
            var s = _text.text;
            return index >= 0 && index < s.Length && char.IsWhiteSpace(s[index]);
        }
    }
}
