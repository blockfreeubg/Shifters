// =====================================================================================
//  SHIFTERS - TeamRevealScreen.cs   (UI agent)
//  "MEET YOUR INTERNS" (classic) / "YOUR NEW HIRES" (campaign, §15.1): three dossier cards flip in
//  (portrait, stats, perks, weapon). REROLL = new random team, BACK = main menu, START =
//  classic LoadLevel(0) / CampaignDirector.StartNew(seed) / StartAt(chapter) (SHIFT SCHEDULE without a team).
//  Campaign modes add Max's ME-TV PiP, the TEAM CODE (= seed) with an ENTER CODE field (§15.4) and,
//  when the coop module exists, the PARTY toggle seam (Settings.PartyPlayers, §17).
//  Portraits: PortraitStudio (created in the menu scene if missing and built for this team).
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class TeamRevealScreen : UIScreen
    {
        readonly ProfileCardView[] _cards = new ProfileCardView[3];
        TextMeshProUGUI _sub;
        bool _fresh = true;
        PortraitStudio _ownStudio;
        public UIButton StartButton { get; private set; }
        public UIButton RerollButton { get; private set; }
        public UIButton BackButton { get; private set; }
        public ProfileCardView Card(int i) => _cards[i];

        public enum Mode { Classic, Campaign, Chapter }
        public Mode CurrentMode { get; private set; } = Mode.Classic;
        /// <summary>SHIFT SCHEDULE start without a team: the chapter START begins.</summary>
        public ChapterView StartChapter { get; private set; }
        public TextMeshProUGUI TitleText { get; private set; }
        public TextMeshProUGUI TeamCodeText { get; private set; }
        public TMP_InputField CodeInput { get; private set; }
        public UIButton EnterCodeButton { get; private set; }
        public UISettingRow PartyRow { get; private set; }
        RectTransform _codeBlock;
        MeTvFrame _tv;
        static readonly string[] PartyOptions = { "OFF", "2 PLAYERS", "3 PLAYERS" };

        /// <summary>Set the mode before pushing the screen (MainMenu CAMPAIGN / SHIFT SCHEDULE / classic PLAY).</summary>
        public void Configure(Mode mode, ChapterView chapter)
        {
            CurrentMode = mode;
            StartChapter = mode == Mode.Chapter ? chapter : null;
        }

        /// <summary>True when the coop module (PartySession) is in the build: the PARTY toggle is shown.</summary>
        public static bool CoopInstalled => System.Type.GetType("Shifters.PartySession") != null;

        /// <summary>TEAM CODE entry: a positive number becomes the team seed (same seed = same doomed team).</summary>
        public bool ApplyCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            code = code.Trim();
            if (!int.TryParse(code, out int seed) || seed <= 0)
            {
                Sfx.Play(SfxId.UIError);
                GameEvents.RaiseToast("HR REJECTED THAT CODE. NUMBERS ONLY.", UIColors.Danger);
                return false;
            }
            SessionActions.NewRun(seed);
            _fresh = true;
            Refresh();
            MenuStage.Ensure(SessionActions.Team);
            Sfx.Play(SfxId.SwitchWhoosh, 0.6f);
            GameEvents.RaiseToast("TEAM CODE " + seed + " ACCEPTED. SAME TEAM. SAME PROBLEMS.", UIColors.AccentYellow);
            return true;
        }

        /// <summary>Next open plays the full flip-in (new team).</summary>
        public void MarkFresh() => _fresh = true;

        protected override void Build()
        {
            Backdrop(0.8f, true);
            TitleText = Title("MEET YOUR INTERNS", new Vector2(0.5f, 1f), new Vector2(0f, -40f), 76f);
            TitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _sub = UIFactory.Text(Rect, "Sub", "", FontLibrary.Label, 28f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(1400f, 36f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            _sub.characterSpacing = 8f;
            for (int i = 0; i < 3; i++)
            {
                _cards[i] = ProfileCardView.Create(Rect);
                _cards[i].Rt.anchoredPosition = new Vector2((i - 1) * 560f, -22f);
            }
            BackButton = Btn("BACK", () => OnBack(), new Vector2(-560f, 66f), new Vector2(260f, 66f), UIButtonStyle.Ghost);
            RerollButton = Btn("REROLL", Reroll, new Vector2(0f, 66f), new Vector2(320f, 70f), UIButtonStyle.Secondary);
            StartButton = Btn("START SHIFT", OnStart, new Vector2(560f, 66f), new Vector2(380f, 78f), UIButtonStyle.Primary);
            UIFactory.LinkHorizontal(new List<Selectable> { BackButton.Selectable, RerollButton.Selectable, StartButton.Selectable }, false);
            DefaultSelection = StartButton.Selectable;
            BuildCodeBlock();
            _tv = MeTvFrame.Create(Rect, new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(380f, 170f), "MAX, WATCHING", 22f);
            var s = SessionActions.Settings;
            PartyRow = UISettingRow.CreateOption(Rect, "PARTY", PartyOptions, Mathf.Clamp(s.PartyPlayers - 1, 0, 2), i => { SessionActions.Settings.PartyPlayers = i + 1; SessionActions.SaveSettings(); });
            var prt = (RectTransform)PartyRow.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f); prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-40f, -200f);
            prt.localScale = new Vector3(0.62f, 0.62f, 1f);
        }

        void BuildCodeBlock()
        {
            _codeBlock = UIFactory.Rect("TeamCode", Rect, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(420f, 170f), new Vector2(0f, 1f));
            var lab = UIFactory.Text(_codeBlock, "Label", "TEAM CODE", FontLibrary.Label, 22f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(300f, 28f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            lab.characterSpacing = 8f;
            TeamCodeText = UIFactory.Text(_codeBlock, "Code", "", FontLibrary.Display, 46f, UIColors.AccentYellow, new Vector2(0f, 1f), new Vector2(0f, -26f), new Vector2(420f, 58f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            TeamCodeText.characterSpacing = 6f;
            // input (mouse click or ENTER CODE button; kept out of the arrow-key chain so arrows never get trapped in it)
            var root = UIFactory.Rect("CodeInput", _codeBlock, new Vector2(0f, 1f), new Vector2(0f, -94f), new Vector2(210f, 50f), new Vector2(0f, 1f));
            root.gameObject.SetActive(false);
            var bg = UIFactory.Fill(root, "Bg", UISprites.Rounded(10), new Color(0f, 0f, 0f, 0.55f), true);
            var area = UIFactory.Rect("TextArea", root);
            UIFactory.Stretch(area, 14f, 14f, 6f, 6f);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = UIFactory.Text(area, "Placeholder", "123456", FontLibrary.Heavy, 26f, UIFactory.WithAlpha(UIColors.TextMuted, 0.5f), TextAlignmentOptions.Left);
            UIFactory.Stretch(ph.rectTransform);
            var txt = UIFactory.Text(area, "Text", "", FontLibrary.Heavy, 26f, UIColors.Text, TextAlignmentOptions.Left);
            UIFactory.Stretch(txt.rectTransform);
            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = txt; input.placeholder = ph; input.targetGraphic = bg;
            input.characterLimit = 9;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            input.caretColor = UIColors.AccentYellow;
            input.onSubmit.AddListener(v => { if (ApplyCode(v)) CodeInput.text = ""; SelectDefault(); });
            root.gameObject.SetActive(true);
            CodeInput = input;
            EnterCodeButton = UIFactory.Button(_codeBlock, "Btn_ENTER CODE", "ENTER CODE", FocusCode, new Vector2(190f, 50f), UIButtonStyle.Ghost, 22f);
            EnterCodeButton.Rt.anchorMin = EnterCodeButton.Rt.anchorMax = new Vector2(0f, 1f); EnterCodeButton.Rt.pivot = new Vector2(0f, 1f);
            EnterCodeButton.Rt.anchoredPosition = new Vector2(222f, -94f);
        }

        void FocusCode()
        {
            if (CodeInput == null) return;
            if (!string.IsNullOrEmpty(CodeInput.text)) { if (ApplyCode(CodeInput.text)) CodeInput.text = ""; return; }
            CodeInput.Select();
            CodeInput.ActivateInputField();
        }

        void Relink()
        {
            bool campaign = CurrentMode != Mode.Classic;
            bool party = campaign && CoopInstalled;
            var row = new List<Selectable> { BackButton.Selectable, RerollButton.Selectable, StartButton.Selectable };
            Selectable up = campaign ? EnterCodeButton.Selectable : null;
            UIFactory.LinkHorizontal(row, false, null, up);
            if (campaign)
                EnterCodeButton.Selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = StartButton.Selectable, selectOnRight = party ? PartyRow.Selectable : null };
            if (party)
                PartyRow.Selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = StartButton.Selectable, selectOnLeft = EnterCodeButton.Selectable };
        }

        UIButton Btn(string label, System.Action a, Vector2 pos, Vector2 size, UIButtonStyle style)
        {
            var b = UIFactory.Button(Rect, "Btn_" + label, label, a, size, style, 30f);
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0f); b.Rt.pivot = new Vector2(0.5f, 0f);
            b.Rt.anchoredPosition = pos;
            return b;
        }

        void Reroll()
        {
            SessionActions.NewRun();
            _fresh = true;
            Refresh();
            MenuStage.Ensure(SessionActions.Team);
            Sfx.Play(SfxId.SwitchWhoosh, 0.6f);
        }

        void OnStart()
        {
            var team = SessionActions.Team;
            switch (CurrentMode)
            {
                case Mode.Campaign: SessionActions.CampaignNew(team != null ? team.Seed : 0); break;
                case Mode.Chapter: SessionActions.CampaignAt(StartChapter); break;
                default: SessionActions.LoadLevel(0); break;
            }
        }

        protected override void OnOpened()
        {
            SessionActions.EnsureTeam();
            bool campaign = CurrentMode != Mode.Classic;
            TitleText.text = campaign ? "YOUR NEW HIRES" : "MEET YOUR INTERNS";
            StartButton.SetText(CurrentMode == Mode.Chapter && StartChapter != null ? "START " + StartChapter.ShiftLabel : "START SHIFT");
            _codeBlock.gameObject.SetActive(campaign);
            _tv.Root.gameObject.SetActive(campaign);
            PartyRow.gameObject.SetActive(campaign && CoopInstalled);
            if (PartyRow.gameObject.activeSelf) { PartyRow.SetIndex(Mathf.Clamp(SessionActions.Settings.PartyPlayers - 1, 0, 2)); PartyRow.RefreshText(); }
            if (campaign) _tv.Say("Three of them. Ugh. I'll learn zero names.", 0.5f);
            Relink();
            Refresh();
        }

        protected override void OnClosed()
        {
            // TEAM CODE accepted/rejected toasts are feedback for this screen only: don't let them sit over the next one
            if (UIManager.Instance != null && UIManager.Instance.Toasts != null) UIManager.Instance.Toasts.Clear();
            if (_ownStudio != null && GameSession.Instance != null && GameSession.Instance.LevelIndex >= 0) { Destroy(_ownStudio.gameObject); _ownStudio = null; }
        }

        void Refresh()
        {
            var team = SessionActions.Team;
            if (team == null) return;
            _sub.text = CurrentMode == Mode.Classic ? "YOUR SHIFT STARTS NOW   ·   TEAM #" + team.Seed + "   ·   NO REFUNDS"
                : CurrentMode == Mode.Chapter && StartChapter != null ? "CLOCKING IN AT " + StartChapter.MenuLabel + "   ·   NO REFUNDS"
                : "THEY START TODAY   ·   NO TRAINING   ·   NO REFUNDS";
            if (TeamCodeText != null) TeamCodeText.text = team.Seed.ToString();
            BuildPortraits(team);
            bool animate = _fresh;
            _fresh = false;
            for (int i = 0; i < 3; i++)
            {
                var p = team.Members != null && i < team.Members.Length ? team.Members[i] : null;
                _cards[i].gameObject.SetActive(p != null);
                if (p == null) continue;
                _cards[i].Bind(p, i, animate);
                if (animate)
                {
                    var rt = _cards[i].Rt;
                    rt.localScale = new Vector3(0f, 1f, 1f);
                    UITween.Scale(rt, Vector3.one, 0.45f, Ease.OutBack, 0.08f + i * 0.14f, new Vector3(0.02f, 1.04f, 1f));
                    UITween.Rotate(rt, 0f, 0.5f, Ease.OutBack, 0.08f + i * 0.14f, i == 1 ? 6f : -6f);
                    UITween.Delay(0.1f + i * 0.14f, () => Sfx.Play(SfxId.UIPop, 0.8f, 0.9f + 0.1f * Random.value), this);
                }
            }
        }

        void BuildPortraits(TeamData team)
        {
            if (PortraitStudio.Instance == null)
            {
                var go = new GameObject("PortraitStudio (menu)");
                _ownStudio = go.AddComponent<PortraitStudio>();
            }
            PortraitStudio.Instance?.Build(team);
            PortraitStudio.Instance?.Refresh();
        }

        public override void OnBack()
        {
            if (CodeInput != null && CodeInput.isFocused) { CodeInput.DeactivateInputField(); SelectDefault(); return; }
            Sfx.Play(SfxId.UIBack); Manager?.Pop();
        }
    }
}
