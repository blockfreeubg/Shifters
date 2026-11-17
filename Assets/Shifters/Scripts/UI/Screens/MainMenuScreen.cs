// =====================================================================================
//  SHIFTERS - MainMenuScreen.cs   (UI agent)
//  Animated logo, subtitle, CONTINUE (only with a campaign save: "SHIFT 07 - THE FEDERAL COLOR RESERVE") /
//  CAMPAIGN (default) / SHIFT SCHEDULE / FREE PLAY / SETTINGS / CREDITS / QUIT (hidden on WebGL), "GAME OFF 2026"
//  jam badge, version + current team chips, and a ME-TV set that heckles ("Press PLAY. Alone."). The 3D stage
//  behind it is MenuStage. Without the campaign module CAMPAIGN reads PLAY and runs the classic
//  PLAY -> team reveal -> L1 flow; CONTINUE then follows the classic saved run.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class MainMenuScreen : UIScreen
    {
        public const string Version = "v1.0 JAM BUILD";
        public LogoAnimator Logo { get; private set; }
        public UIButton ContinueButton { get; private set; }
        public UIButton CampaignButton { get; private set; }
        public UIButton ScheduleButton { get; private set; }
        public UIButton FreePlayButton { get; private set; }
        public UIButton SettingsButton { get; private set; }
        public UIButton CreditsButton { get; private set; }
        public UIButton QuitButton { get; private set; }
        public readonly List<UIButton> Buttons = new List<UIButton>();
        /// <summary>The CONTINUE sub-line ("SHIFT 07 - THE FEDERAL COLOR RESERVE"), empty when hidden.</summary>
        public string ContinueLabel => _continueSub != null && _continueSub.gameObject.activeSelf ? _continueSub.text : "";
        /// <summary>The ME-TV line currently on air (tests).</summary>
        public string HeckleText => _tvLine;
        TextMeshProUGUI _version, _continueSub, _tvText, _tvLive;
        RectTransform _teamRow, _subtitle, _badge, _tv;
        string _tvLine = "";
        int _tvIndex = -1, _tvShown = -1;
        float _tvStart, _tvNext;

        const float ButtonTop = -430f, ButtonStep = 70f;

        static readonly string[] Heckles =
        {
            "Press PLAY. Alone.",
            "Teamwork is cheating. Report all cooperation to: ME.",
            "Three interns? Ugh. One is plenty. Zero is ideal.",
            "Today's forecast: GREY. Tomorrow: also grey.",
            "Employee of the month: ME. Again.",
            "Don't press CAMPAIGN. It ends badly. For me.",
            "Settings won't save you. Nothing will.",
            "Free Play? Nothing is free. I own everything.",
            "Smile! You're on ME-TV.",
            "Two's a crowd. Three's a crime.",
        };

        protected override void Build()
        {
            var tvLayer = UIFactory.Rect("TvLayer", Rect);
            UIFactory.Stretch(tvLayer);
            var shade = UIFactory.Image(Rect, "LeftShade", UISprites.GradientH, new Color(0.03f, 0.035f, 0.05f, 0.92f));
            var srt = shade.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(1250f, 0f);
            var vig = UIFactory.Fill(Rect, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0f, 0.45f));
            vig.raycastTarget = false;

            var stripes = UIFactory.Image(Rect, "Stripes", UISprites.Stripes, UIFactory.WithAlpha(UIColors.AccentYellow, 0.85f));
            stripes.type = Image.Type.Tiled;
            var strt = stripes.rectTransform;
            strt.anchorMin = strt.anchorMax = new Vector2(0f, 1f); strt.pivot = new Vector2(0f, 1f);
            strt.anchoredPosition = new Vector2(0f, -318f); strt.sizeDelta = new Vector2(760f, 14f);

            Logo = LogoAnimator.Create(Rect, 172f);
            var lrt = (RectTransform)Logo.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f); lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = new Vector2(100f, -90f);

            var sub = UIFactory.Text(Rect, "Subtitle", "THREE INTERNS. ONE SHIFT. TRY NOT TO LOSE THEM.", FontLibrary.Label, 34f, UIColors.Text, new Vector2(0f, 1f), new Vector2(112f, -336f), new Vector2(1000f, 44f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            sub.characterSpacing = 6f;
            _subtitle = sub.rectTransform;

            float y = ButtonTop;
            var sels = new List<Selectable>();
            ContinueButton = AddButton("CONTINUE", OnContinue, ref y, sels);
            CampaignButton = AddButton("CAMPAIGN", OnCampaign, ref y, sels);
            ScheduleButton = AddButton("SHIFT SCHEDULE", () => Manager?.OpenShiftSchedule(), ref y, sels);
            FreePlayButton = AddButton("FREE PLAY", () => Manager?.OpenFreePlay(), ref y, sels);
            SettingsButton = AddButton("SETTINGS", () => Manager?.OpenSettings(), ref y, sels);
            CreditsButton = AddButton("CREDITS", () => Manager?.OpenCredits(), ref y, sels);
            QuitButton = AddButton("QUIT", () => SessionActions.Quit(), ref y, sels);
            UIFactory.LinkVertical(sels);
            _continueSub = UIFactory.Text(ContinueButton.Rt, "Sub", "", FontLibrary.Label, 22f, UIColors.AccentYellow, new Vector2(1f, 0.5f), new Vector2(22f, 0f), new Vector2(560f, 30f), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            _continueSub.characterSpacing = 4f;
            BuildTv(tvLayer);

            // jam badge
            _badge = UIFactory.Rect("JamBadge", Rect, new Vector2(0f, 0f), new Vector2(112f, 60f), new Vector2(330f, 74f), new Vector2(0f, 0f));
            var bsh = UIFactory.Image(_badge, "Shadow", UISprites.Shadow(16, 14), new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Expand(bsh.rectTransform, 16f, new Vector2(0f, -5f));
            UIFactory.Fill(_badge, "Bg", UISprites.Rounded(14), UIColors.AccentYellow);
            var bstr = UIFactory.Fill(_badge, "Stripes", UISprites.Stripes, new Color(0f, 0f, 0f, 0.12f));
            bstr.type = Image.Type.Tiled;
            var bt = UIFactory.Text(_badge, "Text", "GAME OFF 2026", FontLibrary.Display, 30f, UIColors.Hex(0x12161C), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(320f, 40f), TextAlignmentOptions.Center);
            var bs = UIFactory.Text(_badge, "Sub", "OFFICIAL JAM ENTRY", FontLibrary.Label, 17f, UIColors.Hex(0x12161C), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(320f, 22f), TextAlignmentOptions.Center);
            bs.characterSpacing = 8f;
            _badge.localEulerAngles = new Vector3(0f, 0f, 3f);

            _version = UIFactory.Text(Rect, "Version", Version, FontLibrary.Label, 20f, UIColors.TextMuted, new Vector2(1f, 0f), new Vector2(-34f, 26f), new Vector2(700f, 26f), TextAlignmentOptions.Right, false, new Vector2(1f, 0f));
            _version.characterSpacing = 4f;
            _teamRow = UIFactory.Rect("Team", Rect, new Vector2(1f, 0f), new Vector2(-34f, 62f), new Vector2(900f, 34f), new Vector2(1f, 0f));
            DefaultSelection = sels.Count > 1 ? sels[1] : null;
        }

        UIButton AddButton(string label, System.Action action, ref float y, List<Selectable> sels)
        {
            var b = UIFactory.Button(Rect, "Btn_" + label, label, action, new Vector2(470f, 60f), UIButtonStyle.Menu, 30f);
            var rt = b.Rt;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(104f, y);
            y -= ButtonStep;
            Buttons.Add(b);
            sels.Add(b.Selectable);
            return b;
        }

        /// <summary>The CampaignLink.Present value the menu last laid itself out for. The menu is first opened from
        /// UIManager.OnSceneLoaded, before CampaignDirector attaches (AfterSceneLoad), so Update re-applies the state
        /// (PLAY/CAMPAIGN label, CONTINUE + subline from the right save) the moment Present flips.</summary>
        bool _presentShown;
        /// <summary>Tests: how many times the menu re-laid itself out because CampaignLink.Present changed while open.</summary>
        public int PresenceRefreshes { get; private set; }

        protected override void OnOpened()
        {
            ApplyState(false);
            _tvIndex = -1; _tvNext = UITime.Now + 0.6f; _tvLine = ""; _tvText.text = "";
            UITween.Rotate(_tv, -2.5f, 0.7f, Ease.OutElastic, 0.35f, 8f);

            int seed = SessionActions.Team != null ? SessionActions.Team.Seed : 0;
            _version.text = Version + (seed != 0 ? "   ·   TEAM #" + seed : "");
            BuildTeamRow();

            // entrance animation
            int i = 0;
            foreach (var b in Buttons)
            {
                if (!b.gameObject.activeSelf) continue;
                var target = b.Rt.anchoredPosition;
                UITween.Move(b.Rt, target, 0.4f, Ease.OutBack, 0.05f + i * 0.05f, target + new Vector2(-520f, 0f));
                i++;
            }
            var lrt = (RectTransform)Logo.transform;
            UITween.Scale(lrt, 1f, 0.55f, Ease.OutBack, 0f, 0.85f);
            UITween.Move(_subtitle, _subtitle.anchoredPosition, 0.5f, Ease.OutCubic, 0.15f, _subtitle.anchoredPosition + new Vector2(0f, -30f));
            UITween.Rotate(_badge, 3f, 0.8f, Ease.OutElastic, 0.3f, -14f);
        }

        /// <summary>Label, CONTINUE visibility/subline, QUIT, button flow and navigation for the current campaign state.
        /// live = re-applied while open (buttons glide to their new slots; selection kept unless it became hidden).</summary>
        void ApplyState(bool live)
        {
            bool campaign = CampaignLink.Present;
            _presentShown = campaign;
            bool canContinue = ContinueVisible;
            ContinueButton.gameObject.SetActive(canContinue);
            _continueSub.text = canContinue ? ContinueSubline() : "";
            _continueSub.gameObject.SetActive(_continueSub.text.Length > 0);
            QuitButton.gameObject.SetActive(!IsWebGL);   // browsers can't quit: no dead buttons
            CampaignButton.SetText(campaign ? "CAMPAIGN" : "PLAY");
            // re-flow so there is no hole when CONTINUE is hidden
            float y = ButtonTop;
            var sels = new List<Selectable>();
            foreach (var b in Buttons)
            {
                if (!b.gameObject.activeSelf) continue;
                var slot = new Vector2(104f, y);
                if (live) UITween.Move(b.Rt, slot, 0.22f, Ease.OutCubic);   // replaces any running entrance tween
                else b.Rt.anchoredPosition = slot;
                sels.Add(b.Selectable);
                y -= ButtonStep;
            }
            UIFactory.LinkVertical(sels);
            DefaultSelection = CampaignButton.Selectable;   // §15.1: CAMPAIGN is the default selection
            if (live)
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var cur = es != null ? es.currentSelectedGameObject : null;
                if (cur == null || !cur.activeInHierarchy) SelectDefault();
            }
        }

        void BuildTeamRow()
        {
            for (int i = _teamRow.childCount - 1; i >= 0; i--) Destroy(_teamRow.GetChild(i).gameObject);
            var team = SessionActions.Team;
            if (team == null || team.Members == null) return;
            float x = 0f;
            for (int i = team.Members.Length - 1; i >= 0; i--)
            {
                var m = team.Members[i];
                if (m == null) continue;
                var chip = UIFactory.Chip(_teamRow, UIIcon.Person, m.DisplayName.ToUpperInvariant(), m.PrimaryColor, 30f);
                chip.anchorMin = chip.anchorMax = new Vector2(1f, 0.5f); chip.pivot = new Vector2(1f, 0.5f);
                chip.anchoredPosition = new Vector2(-x, 0f);
                x += chip.sizeDelta.x + 8f;
            }
            var label = UIFactory.Text(_teamRow, "Label", "YOUR INTERNS", FontLibrary.Label, 19f, UIColors.TextMuted, new Vector2(1f, 0.5f), new Vector2(-x - 6f, 0f), new Vector2(200f, 26f), TextAlignmentOptions.Right, false, new Vector2(1f, 0.5f));
            label.characterSpacing = 5f;
        }

        /// <summary>CONTINUE is visible: a campaign save (campaign module present) or the classic saved run (module absent).</summary>
        public static bool ContinueVisible => CampaignLink.Present ? CampaignLink.HasSave : SessionActions.CanContinue;

        static string ContinueSubline()
        {
            if (CampaignLink.Present)
            {
                var c = CampaignLink.SavedChapter;
                return c != null ? c.MenuLabel : "";
            }
            int i = SessionActions.FirstIncompleteLevel();
            var info = LevelCatalog.Get(i);
            return info != null ? CampaignLink.ShiftLabelFor(i) + " - " + info.Title.ToUpperInvariant() : "";
        }

        void OnContinue()
        {
            if (CampaignLink.Present && CampaignLink.HasSave) SessionActions.CampaignContinue();
            else SessionActions.Continue();
        }

        /// <summary>CAMPAIGN: (START OVER? if a campaign save exists) -> YOUR NEW HIRES -> CampaignDirector.StartNew. Classic PLAY without the module.</summary>
        void OnCampaign()
        {
            var m = Manager;
            if (m == null) return;
            if (!CampaignLink.Present)
            {
                SessionActions.NewRun();
                m.TeamReveal.Configure(TeamRevealScreen.Mode.Classic, null);
                m.TeamReveal.MarkFresh();
                m.Push(m.TeamReveal);
                return;
            }
            if (CampaignLink.HasSave)
            {
                m.AskConfirm("START OVER?", "Your old interns will be... let go.\n<size=70%>(The saved campaign is replaced when the new one starts.)</size>", "START OVER", "KEEP THEM",
                    () => OpenNewHires(m), null, false);
                return;
            }
            OpenNewHires(m);
        }

        static void OpenNewHires(UIManager m)
        {
            SessionActions.NewRun();
            m.TeamReveal.Configure(TeamRevealScreen.Mode.Campaign, null);
            m.TeamReveal.MarkFresh();
            m.Push(m.TeamReveal);
        }

        // ------------------------------------------------------------------ ME-TV heckle set
        void BuildTv(RectTransform layer)
        {
            _tv = UIFactory.Rect("MeTv", layer, new Vector2(1f, 1f), new Vector2(-150f, -70f), new Vector2(520f, 330f), new Vector2(1f, 1f));
            var sh = UIFactory.Image(_tv, "Shadow", UISprites.Shadow(20, 26), new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Expand(sh.rectTransform, 22f, new Vector2(0f, -8f));
            UIFactory.Fill(_tv, "Case", UISprites.Rounded(26), UIColors.Hex(0x2A2418));
            var bezel = UIFactory.Image(_tv, "Bezel", UISprites.Rounded(22), UIColors.Hex(0xC9A227));
            UIFactory.Stretch(bezel.rectTransform, 10f, 10f, 10f, 46f);
            var screen = UIFactory.Image(_tv, "Screen", UISprites.Rounded(18), UIColors.Hex(0x0B0E0A));
            UIFactory.Stretch(screen.rectTransform, 18f, 18f, 18f, 54f);
            var scan = UIFactory.Image(screen.rectTransform, "Scan", UISprites.Stripes, new Color(1f, 1f, 1f, 0.035f));
            scan.type = Image.Type.Tiled;
            UIFactory.Stretch(scan.rectTransform);
            var glow = UIFactory.Image(screen.rectTransform, "Glow", UISprites.SoftCircle, UIFactory.WithAlpha(UIColors.Hex(0xFFD54A), 0.10f));
            UIFactory.Stretch(glow.rectTransform, -60f, -60f, -40f, -40f);
            var bug = UIFactory.Text(screen.rectTransform, "Bug", "ME-TV", FontLibrary.Display, 26f, UIColors.Hex(0xFFD54A), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(200f, 32f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            bug.characterSpacing = 3f;
            _tvLive = UIFactory.Text(screen.rectTransform, "Live", "LIVE", FontLibrary.Heavy, 20f, UIColors.Danger, new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(120f, 26f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));
            var who = UIFactory.Text(screen.rectTransform, "Who", "MAX, SUPREME SOLE EXECUTIVE", FontLibrary.Label, 17f, UIColors.TextMuted, new Vector2(0f, 0f), new Vector2(16f, 10f), new Vector2(440f, 24f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            who.characterSpacing = 4f;
            _tvText = UIFactory.Text(screen.rectTransform, "Line", "", FontLibrary.Display, 30f, UIColors.Hex(0xFFD54A), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(430f, 150f), TextAlignmentOptions.Center, true);
            UIFactory.AutoSize(_tvText, 20f, 30f);
            var knobL = UIFactory.Image(_tv, "Knob1", UISprites.Circle, UIColors.Hex(0xC9A227));
            knobL.rectTransform.anchorMin = knobL.rectTransform.anchorMax = new Vector2(1f, 0f);
            knobL.rectTransform.sizeDelta = new Vector2(24f, 24f); knobL.rectTransform.anchoredPosition = new Vector2(-40f, 24f);
            var knobR = UIFactory.Image(_tv, "Knob2", UISprites.Circle, UIColors.Hex(0xC9A227));
            knobR.rectTransform.anchorMin = knobR.rectTransform.anchorMax = new Vector2(1f, 0f);
            knobR.rectTransform.sizeDelta = new Vector2(24f, 24f); knobR.rectTransform.anchoredPosition = new Vector2(-76f, 24f);
            var brand = UIFactory.Text(_tv, "Brand", "PROPERTY OF ME", FontLibrary.Label, 16f, UIColors.Hex(0xC9A227), new Vector2(0f, 0f), new Vector2(26f, 12f), new Vector2(300f, 24f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            brand.characterSpacing = 6f;
            _tv.localEulerAngles = new Vector3(0f, 0f, -2.5f);
        }

        void Update()
        {
            if (!IsOpen || _tvText == null) return;
            if (CampaignLink.Present != _presentShown) { ApplyState(true); PresenceRefreshes++; }
            float now = UITime.Now;
            bool offAir = SessionActions.Settings.Heckles >= 2;   // VILLAIN HECKLES: NEVER
            if (offAir)
            {
                if (_tvLine != "PLEASE STAND BY") { _tvLine = "PLEASE STAND BY"; _tvText.text = "<size=70%>OFF AIR</size>\n" + _tvLine; }
                _tvLive.text = "";
                return;
            }
            _tvLive.text = Mathf.Repeat(now, 1.2f) < 0.8f ? "LIVE" : "";
            if (now >= _tvNext)
            {
                _tvIndex = (_tvIndex + 1) % Heckles.Length;
                _tvLine = Heckles[_tvIndex];
                _tvStart = now;
                _tvShown = -1;
                _tvNext = now + (SessionActions.Settings.Heckles == 1 ? 9f : 6f);
                UITween.Punch(_tv, 0.04f, 0.25f);
            }
            int n = Mathf.Clamp(Mathf.FloorToInt((now - _tvStart) * 32f), 0, _tvLine.Length);
            if (n == _tvShown) return;   // only re-set the text when a character appears (no per-frame garbage)
            _tvShown = n;
            _tvText.text = "\"" + _tvLine.Substring(0, n) + (n < _tvLine.Length ? "_" : "\"");
        }

        public override void OnBack() { }

        /// <summary>True in the WebGL player (no QUIT there).</summary>
        public static bool IsWebGL => Application.platform == RuntimePlatform.WebGLPlayer;

        public UIButton Find(string label)
        {
            foreach (var b in Buttons) if (b.Label != null && b.Label.text == label) return b;
            return null;
        }
    }
}
