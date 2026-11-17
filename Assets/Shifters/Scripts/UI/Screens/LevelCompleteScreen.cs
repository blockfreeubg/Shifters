// =====================================================================================
//  SHIFTERS - LevelCompleteScreen.cs   (UI agent)
//  Opened on GameEvents.LevelCompleted: time, survivors x/3, left-behind names, switches,
//  restarts (counting up), a graded "HR PERFORMANCE REVIEW" joke, NEXT SHIFT / RETRY / MAIN MENU.
//  Campaign additions (§15.2): WORLD COLOR rainbow bar with the delta, TEAMWORK VIOLATIONS + "HR (ALSO ME)
//  SAYS:", EXHIBIT ACQUIRED stamp, stealth grade (LevelCompleteExtras.StealthGrade, CH04) and Max's live
//  ME-TV reaction (LevelCompleteExtras.MaxReaction or a line by grade). The screen opens after
//  LevelCompleteHold (<= 3 s). NEXT SHIFT -> GameSession.NextLevel (the campaign routes it).
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class LevelCompleteScreen : UIScreen
    {
        LevelResult _result;
        TextMeshProUGUI _level, _time, _survivors, _left, _switches, _restarts, _grade, _review;
        RectTransform _gradeRt, _header;
        public UIButton NextButton { get; private set; }
        public UIButton RetryButton { get; private set; }
        public UIButton MenuButton { get; private set; }
        public string Grade { get; private set; }
        public string Review => _review != null ? _review.text : "";
        public LevelResult Result => _result;
        RectTransform _strip, _colorFill, _exStamp;
        TextMeshProUGUI _colorText, _vioText, _hrText, _exText, _stealth;
        MeTvFrame _tv;
        /// <summary>True when the campaign strip was shown at the last open (tests).</summary>
        public bool CampaignStripShown => _strip != null && _strip.gameObject.activeSelf;
        public string ReactionText => _tv != null && _tv.Root.gameObject.activeSelf ? _tv.Line.text : "";
        public string StealthText => _stealth != null && _stealth.gameObject.activeSelf ? _stealth.text : "";
        public string ExhibitText => _exStamp != null && _exStamp.gameObject.activeSelf ? _exText.text : "";

        static readonly string[] Reactions =
        {
            "Flawless?! Who TRAINED you? Not me. I'd never.",
            "You all survived. Together. Disgusting.",
            "One fewer intern. See? Solo is the future.",
            "Most of you got lost. I'm almost proud.",
            "One intern left. Finally, someone who gets it.",
        };

        static readonly string[][] Lines =
        {
            new[] { "Flawless. Suspiciously flawless. We are auditing you.", "Zero casualties. HR is confused and slightly disappointed.", "Textbook teamwork. Please stop, you are making the rest of us look bad." },
            new[] { "Everyone made it. Adequate. Do not let it go to your head.", "All interns accounted for. A first for this department.", "Good job. Your reward is more work." },
            new[] { "One coworker short. We will print another one.", "Minor losses. The paperwork, however, is major.", "Acceptable casualties. Morale remains unacceptable." },
            new[] { "Most of the team is 'elsewhere'. Productivity unaffected.", "You left people behind. Bold leadership.", "Survival rate: questionable. Initiative: noted." },
            new[] { "Technically a success. Technically.", "You are the team now. Congratulations?", "The exit was reached. By someone. Eventually." },
        };

        public void SetResult(LevelResult r) => _result = r;

        protected override void Build()
        {
            Backdrop(0.8f, true);
            _header = UIFactory.Rect("Header", Rect, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1920f, 170f), new Vector2(0.5f, 1f));
            var band = UIFactory.Image(_header, "Band", null, UIColors.Success);
            band.rectTransform.sizeDelta = new Vector2(2400f, 140f);
            band.rectTransform.localEulerAngles = new Vector3(0f, 0f, -2f);
            var st = UIFactory.Image(band.rectTransform, "Stripes", UISprites.Stripes, new Color(0f, 0f, 0f, 0.1f));
            st.type = Image.Type.Tiled;
            UIFactory.Stretch(st.rectTransform);
            var t = UIFactory.Text(_header, "Title", "SHIFT COMPLETE", FontLibrary.Display, 100f, UIColors.Hex(0x12161C), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(1700f, 120f), TextAlignmentOptions.Center);
            t.rectTransform.localEulerAngles = new Vector3(0f, 0f, -2f);
            _level = UIFactory.Text(Rect, "Level", "", FontLibrary.Label, 32f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(1600f, 40f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            _level.characterSpacing = 8f;

            var stats = UIFactory.Panel(Rect, "Stats", new Vector2(820f, 440f), UIFactory.WithAlpha(UIColors.Panel, 0.95f), 22, true, UIColors.Border);
            stats.Rect.anchorMin = stats.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            stats.Rect.anchoredPosition = new Vector2(-440f, -40f);
            _time = Row(stats.Rect, "TIME", 0);
            _survivors = Row(stats.Rect, "SURVIVORS", 1);
            _left = Row(stats.Rect, "LEFT BEHIND", 2);
            _switches = Row(stats.Rect, "SWITCHES", 3);
            _restarts = Row(stats.Rect, "RESTARTS", 4);

            var hr = UIFactory.Panel(Rect, "HR", new Vector2(640f, 440f), UIColors.Hex(0xF3EBDD, 0.98f), 18, true);
            hr.Rect.anchorMin = hr.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            hr.Rect.anchoredPosition = new Vector2(330f, -40f);
            hr.Rect.localEulerAngles = new Vector3(0f, 0f, 1.5f);
            var hh = UIFactory.Text(hr.Rect, "Header", "HR PERFORMANCE REVIEW", FontLibrary.Display, 28f, UIColors.Hex(0x2B2118), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 36f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            hh.characterSpacing = 1f;
            var line = UIFactory.Image(hr.Rect, "Rule", null, UIColors.Hex(0x2B2118, 0.4f));
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(0f, -70f); line.rectTransform.sizeDelta = new Vector2(560f, 2f);
            _gradeRt = UIFactory.Rect("Grade", hr.Rect, new Vector2(0f, 1f), new Vector2(130f, -170f), new Vector2(170f, 170f), new Vector2(0.5f, 0.5f));
            UIFactory.Fill(_gradeRt, "Ring", UISprites.Ring(14), UIColors.Danger);
            _grade = UIFactory.Text(_gradeRt, "Text", "A", FontLibrary.Display, 84f, UIColors.Danger, TextAlignmentOptions.Center);
            UIFactory.Stretch(_grade.rectTransform);
            _gradeRt.localEulerAngles = new Vector3(0f, 0f, -12f);
            _review = UIFactory.Text(hr.Rect, "Review", "", FontLibrary.Body, 27f, UIColors.Hex(0x2B2118), new Vector2(0f, 1f), new Vector2(240f, -96f), new Vector2(370f, 220f), TextAlignmentOptions.TopLeft, true, new Vector2(0f, 1f));
            UIFactory.AutoSize(_review, 18f, 27f);
            var sig = UIFactory.Text(hr.Rect, "Sig", "- Management (probably)", FontLibrary.Label, 22f, UIColors.Hex(0x6B5A48), new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(400f, 30f), TextAlignmentOptions.Right, false, new Vector2(1f, 0f));
            sig.fontStyle = FontStyles.Italic;

            NextButton = Btn("NEXT SHIFT", () => SessionActions.NextLevel(), new Vector2(360f, 70f), new Vector2(380f, 76f), UIButtonStyle.Primary);
            RetryButton = Btn("RETRY", () => SessionActions.RestartLevel(), new Vector2(0f, 70f), new Vector2(280f, 66f), UIButtonStyle.Secondary);
            MenuButton = Btn("MAIN MENU", () => SessionActions.MainMenu(), new Vector2(-360f, 70f), new Vector2(300f, 66f), UIButtonStyle.Ghost);
            UIFactory.LinkHorizontal(new List<Selectable> { MenuButton.Selectable, RetryButton.Selectable, NextButton.Selectable }, false);
            DefaultSelection = NextButton.Selectable;
            BuildCampaignStrip();
            _tv = MeTvFrame.Create(Rect, new Vector2(1f, 0f), new Vector2(-22f, 168f), new Vector2(300f, 178f), "LIVE REACTION", 21f);
        }

        void BuildCampaignStrip()
        {
            _strip = UIFactory.Rect("CampaignStrip", Rect, new Vector2(0.5f, 0f), new Vector2(-150f, 166f), new Vector2(1300f, 100f), new Vector2(0.5f, 0f));
            var bg = UIFactory.Fill(_strip, "Bg", UISprites.Rounded(16), UIFactory.WithAlpha(UIColors.Panel, 0.95f));
            bg.raycastTarget = false;
            var cl = UIFactory.Text(_strip, "ColorLabel", "WORLD COLOR", FontLibrary.Label, 22f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(300f, 28f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            cl.characterSpacing = 6f;
            var track = UIFactory.Image(_strip, "Track", UISprites.Rounded(10), new Color(0f, 0f, 0f, 0.5f));
            var trt = track.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0f); trt.pivot = new Vector2(0f, 0f);
            trt.anchoredPosition = new Vector2(24f, 18f); trt.sizeDelta = new Vector2(380f, 24f);
            // rainbow fill: one segment per hue, revealed by the fill rect's mask
            var fillMask = UIFactory.Rect("FillMask", trt);
            fillMask.anchorMin = Vector2.zero; fillMask.anchorMax = new Vector2(0f, 1f); fillMask.offsetMin = fillMask.offsetMax = Vector2.zero;
            fillMask.gameObject.AddComponent<RectMask2D>();
            _colorFill = fillMask;
            for (int i = 0; i < 6; i++)
            {
                var seg = UIFactory.Image(fillMask, "Hue" + i, null, Color.HSVToRGB(i / 6f * 0.83f, 0.8f, 1f));
                var srt = seg.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f); srt.pivot = new Vector2(0f, 0.5f);
                srt.anchoredPosition = new Vector2(i * 380f / 6f, 0f); srt.sizeDelta = new Vector2(380f / 6f + 1f, 24f);
            }
            _colorText = UIFactory.Text(_strip, "ColorText", "", FontLibrary.Heavy, 26f, UIColors.Text, new Vector2(0f, 0f), new Vector2(416f, 14f), new Vector2(260f, 32f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            var vl = UIFactory.Text(_strip, "VioLabel", "TEAMWORK VIOLATIONS", FontLibrary.Label, 22f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(680f, -12f), new Vector2(300f, 28f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            vl.characterSpacing = 4f;
            _vioText = UIFactory.Text(_strip, "Vio", "0", FontLibrary.Display, 40f, UIColors.Danger, new Vector2(0f, 0f), new Vector2(680f, 10f), new Vector2(120f, 50f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            _hrText = UIFactory.Text(_strip, "HR", "", FontLibrary.Body, 21f, UIColors.Text, new Vector2(0f, 0f), new Vector2(780f, 12f), new Vector2(300f, 52f), TextAlignmentOptions.Left, true, new Vector2(0f, 0f));
            _hrText.fontStyle = FontStyles.Italic;
            UIFactory.AutoSize(_hrText, 14f, 21f);
            _exStamp = UIFactory.Rect("Exhibit", _strip, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(190f, 84f), new Vector2(1f, 0.5f));
            UIFactory.Fill(_exStamp, "Bg", UISprites.RoundedOutline(10, 4), UIColors.Success);
            _exText = UIFactory.Text(_exStamp, "T", "", FontLibrary.Display, 22f, UIColors.Success, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(_exText.rectTransform, 8f, 8f, 6f, 6f);
            UIFactory.AutoSize(_exText, 14f, 22f);
            _exStamp.localEulerAngles = new Vector3(0f, 0f, -6f);
            _stealth = UIFactory.Text(Rect, "Stealth", "", FontLibrary.Display, 34f, UIColors.Info, new Vector2(0.5f, 1f), new Vector2(0f, -298f), new Vector2(1200f, 42f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            _stealth.characterSpacing = 4f;
        }

        void OpenCampaignAdditions(LevelResult r, int grade)
        {
            var ch = CampaignLink.IsCampaign ? CampaignLink.Current : null;
            if (ch == null && CampaignLink.IsCampaign) ch = CampaignLink.ForCatalogIndex(r.LevelIndex);
            bool show = ch != null;
            _strip.gameObject.SetActive(show);
            string reaction = LevelCompleteExtras.MaxReaction;
            bool tv = show || !string.IsNullOrEmpty(reaction);
            _tv.Root.gameObject.SetActive(tv && SessionActions.Settings.Heckles < 2);
            if (_tv.Root.gameObject.activeSelf) _tv.Say(string.IsNullOrEmpty(reaction) ? Reactions[Mathf.Clamp(grade, 0, Reactions.Length - 1)] : reaction, 0.9f, 2f);
            string sg = LevelCompleteExtras.StealthGrade;
            if (string.IsNullOrEmpty(sg) && LevelCompleteExtras.Detections >= 0) sg = LevelCompleteExtras.GradeFor(LevelCompleteExtras.Detections);
            _stealth.gameObject.SetActive(!string.IsNullOrEmpty(sg));
            if (_stealth.gameObject.activeSelf)
            {
                _stealth.text = "STEALTH GRADE: " + sg + (LevelCompleteExtras.Detections >= 0 ? "   <size=70%>(" + LevelCompleteExtras.Detections + " DETECTIONS)</size>" : "");
                _stealth.color = sg == "GHOST" ? UIColors.Info : sg == "CASUAL" ? UIColors.AccentYellow : UIColors.Danger;
                UITween.Scale(_stealth.rectTransform, 1f, 0.4f, Ease.OutBack, 0.7f, 2f);
            }
            if (!show) return;
            _level.text = ch.ShiftLabel + "  /  " + (ch.Title ?? "").ToUpperInvariant();
            float lf = CampaignLink.LastMeterFrom, lt = CampaignLink.LastMeterTo;
            float from = Mathf.Clamp01(lf >= 0f ? lf : ch.MeterStart), to = Mathf.Clamp01(lt >= 0f ? lt : Mathf.Max(ch.MeterEnd, CampaignLink.Meter));
            if (to < from) to = from;
            _colorFill.anchorMax = new Vector2(from, 1f);
            UITween.Value(1.1f, e => { if (_colorFill != null) _colorFill.anchorMax = new Vector2(Mathf.Lerp(from, to, e), 1f); }, Ease.OutCubic, 0.5f, null, this);
            int dp = Mathf.RoundToInt((to - from) * 100f);
            _colorText.text = Mathf.RoundToInt(to * 100f) + "%" + (dp > 0 ? "  <color=#3DDC97>+" + dp + "%</color>" : "");
            int v = CampaignLink.TeamworkViolations;
            _vioText.text = v.ToString();
            _hrText.text = "HR (ALSO ME) SAYS: \"" + CaseFileScreen.HRLine(v) + "\"";
            string exId = CampaignLink.LastExhibit ?? (!string.IsNullOrEmpty(ch.ExhibitId) && CampaignLink.HasExhibit(ch.ExhibitId) ? ch.ExhibitId : null);
            bool ex = exId != null;
            _exStamp.gameObject.SetActive(ex);
            if (ex)
            {
                _exText.text = "EXHIBIT " + exId + " ACQUIRED\n<size=70%>" + CampaignLink.ExhibitName(exId) + "</size>";
                UITween.Scale(_exStamp, 1f, 0.35f, Ease.OutBack, 1.2f, 2.4f);
                UITween.Delay(1.25f, () => Sfx.Play(SfxId.Stamp), this);
            }
            var next = CampaignLink.Next(ch);
            NextButton.SetText(ch.IsOvertime ? "BACK TO WORK" : next == null ? "CLOCK OUT" : "NEXT SHIFT");
        }

        TextMeshProUGUI Row(RectTransform parent, string label, int i)
        {
            float y = -40f - i * 76f;
            var l = UIFactory.Text(parent, label, label, FontLibrary.Label, 28f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(300f, 50f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            l.characterSpacing = 6f;
            var v = UIFactory.Text(parent, label + "_V", "", FontLibrary.Heavy, 40f, UIColors.Text, new Vector2(1f, 1f), new Vector2(-40f, y + 4f), new Vector2(440f, 56f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));
            UIFactory.AutoSize(v, 20f, 40f);
            if (i < 4)
            {
                var rule = UIFactory.Image(parent, "Rule", null, new Color(1f, 1f, 1f, 0.06f));
                var rrt = rule.rectTransform;
                rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f); rrt.anchoredPosition = new Vector2(0f, y - 62f); rrt.sizeDelta = new Vector2(740f, 2f);
            }
            return v;
        }

        UIButton Btn(string label, System.Action a, Vector2 pos, Vector2 size, UIButtonStyle style)
        {
            var b = UIFactory.Button(Rect, "Btn_" + label, label, a, size, style, 30f);
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0f); b.Rt.pivot = new Vector2(0.5f, 0f);
            b.Rt.anchoredPosition = pos;
            return b;
        }

        public static int GradeIndex(LevelResult r)
        {
            int g = r.Survivors >= 3 ? (r.Restarts == 0 ? 0 : 1) : r.Survivors == 2 ? 2 : r.Survivors == 1 ? 3 : 4;
            if (r.Restarts >= 4 && g < 4) g++;
            return g;
        }

        protected override void OnOpened()
        {
            var r = _result;
            var info = LevelCatalog.Get(r.LevelIndex);
            _level.text = info != null ? CampaignLink.ShiftLabelFor(r.LevelIndex) + "  /  " + info.Title : "TEST CHAMBER CLEARED";
            bool last = LevelCatalog.ClassicNext(r.LevelIndex) < 0;
            NextButton.SetText(last ? "GET PROMOTED" : "NEXT SHIFT");
            CountUp(_time, r.Time, true, 0.2f);
            _survivors.text = Mathf.Clamp(r.Survivors, 0, 3) + " / 3";
            _survivors.color = r.Survivors >= 3 ? UIColors.Success : r.Survivors > 0 ? UIColors.AccentYellow : UIColors.Danger;
            bool anyLeft = r.LeftBehind != null && r.LeftBehind.Length > 0;
            _left.text = anyLeft ? string.Join(", ", r.LeftBehind).ToUpperInvariant() : "NOBODY (SUSPICIOUS)";
            _left.color = anyLeft ? UIColors.Danger : UIColors.Success;
            CountUp(_switches, r.Switches, false, 0.35f);
            CountUp(_restarts, r.Restarts, false, 0.45f);
            int g = GradeIndex(r);
            string[] grades = { "A+", "A", "B", "C", "D" };
            Grade = grades[g];
            _grade.text = Grade;
            var set = Lines[g];
            _review.text = set[Random.Range(0, set.Length)] + (r.Restarts >= 3 ? " Restarts: " + r.Restarts + ". Persistence noted. Competence pending." : "");
            UITween.Scale(_gradeRt, 1f, 0.5f, Ease.OutBack, 0.6f, 3f);
            UITween.Delay(0.65f, () => Sfx.Play(SfxId.Bonk), this);
            UITween.Move(_header, new Vector2(0f, -70f), 0.5f, Ease.OutBack, 0f, new Vector2(-1800f, -70f));
            OpenCampaignAdditions(r, g);
            Sfx.Play(SfxId.LevelComplete);
            BeginStoryMirror();
        }

        // ------------------------------------------------------------------ story lines while the screen is up
        // A chapter outro (or any story script) can still be talking when this screen opens (LevelCompleteHold caps at 3 s).
        // ui_hud's DialogueBox lives on the Campaign HUD canvas (order 15, under the Menu canvas), so its box + the skip chip
        // would peek through the button row and sit under MAIN MENU. While this screen is open we hide that canvas and show
        // the line in our own ME-TV box instead ("MAX ME - LIVE"), so nothing the story says is lost.
        Canvas _hiddenHud;
        bool _mirroring;
        /// <summary>Tests: the last story line mirrored into the ME-TV box while the screen was open (null = none).</summary>
        public string MirroredLine { get; private set; }
        /// <summary>Tests: true while the campaign HUD canvas (DialogueBox, skip chip) is hidden under this screen.</summary>
        public bool CampaignHudHidden => _hiddenHud != null;

        void BeginStoryMirror()
        {
            MirroredLine = null;
            if (_tv != null && _tv.Caption != null) _tv.Caption.text = "LIVE REACTION";
            var hud = CampaignHud.Instance;
            if (hud != null && hud.MainCanvas != null && hud.MainCanvas.enabled) { hud.MainCanvas.enabled = false; _hiddenHud = hud.MainCanvas; }
            if (!_mirroring) { GameEvents.StoryLineShown += OnStoryLine; _mirroring = true; }
            if (hud != null && hud.Dialogue != null && hud.Dialogue.Visible) Mirror(hud.Dialogue.Current);
        }

        void EndStoryMirror()
        {
            if (_mirroring) { GameEvents.StoryLineShown -= OnStoryLine; _mirroring = false; }
            if (_hiddenHud != null) _hiddenHud.enabled = true;
            _hiddenHud = null;
        }

        void OnStoryLine(StoryLine l) { if (IsOpen) Mirror(l); }

        void Mirror(StoryLine l)
        {
            if (_tv == null || string.IsNullOrEmpty(l.Text)) return;
            string name = !string.IsNullOrEmpty(l.SpeakerName) ? l.SpeakerName : (l.SpeakerId ?? "");
            _tv.Root.gameObject.SetActive(true);
            if (_tv.Caption != null) _tv.Caption.text = name.Length > 0 ? name.ToUpperInvariant() + " - LIVE" : "LIVE";
            _tv.Say(l.Text, 0f, 2f);
            MirroredLine = l.Text;
        }

        protected override void OnClosed() => EndStoryMirror();
        void OnDestroy() => EndStoryMirror();

        void CountUp(TextMeshProUGUI t, float target, bool time, float delay)
        {
            UITween.Value(0.8f, e =>
            {
                if (t == null) return;
                float v = target * e;
                t.text = time ? UIFactory.FormatTime(v) : Mathf.RoundToInt(v).ToString();
            }, Ease.OutCubic, delay, null, this);
            t.text = time ? UIFactory.FormatTime(0f) : "0";
        }

        public override void OnBack() { }
    }
}
