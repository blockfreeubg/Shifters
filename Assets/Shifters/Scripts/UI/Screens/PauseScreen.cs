// =====================================================================================
//  SHIFTERS - PauseScreen.cs   (UI agent)
//  Esc in gameplay. RESUME / RESTART CHECKPOINT / RESTART LEVEL / SETTINGS / RESTART TUTORIAL /
//  MAIN MENU, plus level title, seed, run stats and the team's mini cards. Campaign wave: LET HR HANDLE IT
//  (only while CampaignAssists.ShouldOfferHR, §16 -> GameSession.RestartAtNextCheckpoint) and CASE FILE
//  (campaign module present); the header shows the chapter ("SHIFT 07 / THE FEDERAL COLOR RESERVE") and act. Time is paused by
//  UIManager (TimeController.SetPaused) while this (or Settings opened from it) is up.
//  RESUME is always the preselected button: a mouse pointer that never moved (WebGL: the cursor reappears
//  where the pointer lock grabbed it, often over these buttons) must not hover-select another one while the
//  menu appears / slides in. The guard ends as soon as the player moves the mouse, presses a key/button or
//  navigates (see KeepResumeSelected).
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shifters
{
    public class PauseScreen : UIScreen
    {
        public readonly List<UIButton> Buttons = new List<UIButton>();
        public UIButton HRButton { get; private set; }
        public UIButton CaseFileButton { get; private set; }
        readonly List<Vector2> _home = new List<Vector2>();   // final button positions (slide-in targets)
        TextMeshProUGUI _level, _seed, _stats;
        RectTransform _team, _left;
        bool _guardResume;          // RESUME stays selected until the player takes over
        bool _guardNavNeutral;      // the navigate stick was released since the menu opened
        Vector2 _guardPointer;      // pointer position when the menu opened

        /// <summary>True while the RESUME preselection is protected from hover by a pointer that has not moved.</summary>
        public bool ResumeGuardActive => _guardResume;
        /// <summary>Bots / tests: hand navigation over now (as if the player pressed a new key), so synthetic moves stick.</summary>
        public void ReleaseResumeGuard() => _guardResume = false;
        /// <summary>How often the guard put RESUME back (diagnostics / tests).</summary>
        public int ResumeGuardReverts { get; private set; }

        protected override void Build()
        {
            Backdrop(0.72f, true);
            var shade = UIFactory.Image(Rect, "LeftShade", UISprites.GradientH, new Color(0.02f, 0.025f, 0.035f, 0.9f));
            var srt = shade.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(1000f, 0f);
            _left = UIFactory.Rect("Left", Rect, new Vector2(0f, 1f), new Vector2(110f, -80f), new Vector2(700f, 900f), new Vector2(0f, 1f));
            var title = UIFactory.Text(_left, "Title", "PAUSED", FontLibrary.Display, 110f, UIColors.Text, new Vector2(0f, 1f), Vector2.zero, new Vector2(700f, 130f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            if (FontLibrary.LogoMaterial != null) title.fontSharedMaterial = FontLibrary.LogoMaterial;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Accent);
            _level = UIFactory.Text(_left, "Level", "", FontLibrary.Label, 30f, UIColors.Accent, new Vector2(0f, 1f), new Vector2(6f, -138f), new Vector2(700f, 36f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            _level.characterSpacing = 6f;
            _seed = UIFactory.Text(_left, "Seed", "", FontLibrary.Label, 22f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(6f, -176f), new Vector2(700f, 28f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            _seed.characterSpacing = 5f;

            float y = -250f;
            var sels = new List<Selectable>();
            Add("RESUME", () => Manager?.ClosePause(), ref y, sels);
            Add("RESTART CHECKPOINT", () => Do(SessionActions.RestartCheckpoint), ref y, sels);
            HRButton = Add("LET HR HANDLE IT", () => Do(SessionActions.HRIntervention), ref y, sels);
            Add("RESTART LEVEL", () => Do(SessionActions.RestartLevel), ref y, sels);
            CaseFileButton = Add("CASE FILE", () => Manager?.OpenCaseFile(), ref y, sels);
            Add("SETTINGS", () => Manager?.OpenSettings(), ref y, sels);
            Add("RESTART TUTORIAL", () => Do(SessionActions.RestartTutorial), ref y, sels);
            Add("MAIN MENU", () => Do(SessionActions.MainMenu), ref y, sels);
            UIFactory.LinkVertical(sels);
            DefaultSelection = sels[0];

            var right = UIFactory.Panel(Rect, "Stats", new Vector2(760f, 440f), UIFactory.WithAlpha(UIColors.Panel, 0.92f), 22, true, UIColors.Border);
            right.Rect.anchorMin = right.Rect.anchorMax = new Vector2(1f, 1f); right.Rect.pivot = new Vector2(1f, 1f);
            right.Rect.anchoredPosition = new Vector2(-100f, -110f);
            var rh = UIFactory.Text(right.Rect, "Header", "RUN STATS", FontLibrary.Display, 30f, UIColors.AccentYellow, new Vector2(0f, 1f), new Vector2(32f, -24f), new Vector2(600f, 40f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            rh.characterSpacing = 2f;
            _stats = UIFactory.Text(right.Rect, "Body", "", FontLibrary.Body, 25f, UIColors.Text, new Vector2(0f, 1f), new Vector2(32f, -78f), new Vector2(700f, 340f), TextAlignmentOptions.TopLeft, true, new Vector2(0f, 1f));
            _stats.lineSpacing = 12f;

            _team = UIFactory.Rect("Team", Rect, new Vector2(1f, 1f), new Vector2(-100f, -580f), new Vector2(760f, 150f), new Vector2(1f, 1f));
            var th = UIFactory.Text(_team, "Header", "THE TEAM", FontLibrary.Display, 26f, UIColors.AccentYellow, new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(400f, 34f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            th.characterSpacing = 2f;
        }

        UIButton Add(string label, System.Action a, ref float y, List<Selectable> sels)
        {
            var b = UIFactory.Button(_left, "Btn_" + label, label, a, new Vector2(500f, 62f), UIButtonStyle.Menu, 28f);
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0f, 1f); b.Rt.pivot = new Vector2(0f, 0.5f);
            b.Rt.anchoredPosition = new Vector2(0f, y);
            _home.Add(b.Rt.anchoredPosition);
            y -= 74f;
            Buttons.Add(b);
            sels.Add(b.Selectable);
            return b;
        }

        /// <summary>Show/hide the optional buttons and re-flow the column (homes = slide-in targets).</summary>
        void Layout()
        {
            HRButton.gameObject.SetActive(CampaignLink.ShouldOfferHR);
            CaseFileButton.gameObject.SetActive(CampaignLink.Present);
            float y = -250f;
            var sels = new List<Selectable>();
            for (int i = 0; i < Buttons.Count; i++)
            {
                if (!Buttons[i].gameObject.activeSelf) { _home[i] = new Vector2(0f, y); continue; }
                _home[i] = new Vector2(0f, y);
                Buttons[i].Rt.anchoredPosition = _home[i];
                sels.Add(Buttons[i].Selectable);
                y -= 74f;
            }
            UIFactory.LinkVertical(sels);
        }

        void Do(System.Action sessionAction)
        {
            Manager?.ClosePause();
            sessionAction();
        }

        public override void OnBack() { Sfx.Play(SfxId.UIBack); Manager?.ClosePause(); }

        public UIButton Find(string label)
        {
            foreach (var b in Buttons) if (b.Label != null && b.Label.text == label) return b;
            return null;
        }

        /// <summary>RUN STATS progress cell: "SHIFTS done/14" from the campaign save in a campaign chapter, else classic "LEVELS n/5".</summary>
        static string ProgressStat(ChapterView ch, int levelsCompleted)
        {
            if (ch != null)
            {
                try
                {
                    var save = CampaignLink.Save;
                    int total = 0, done = 0;
                    foreach (var c in CampaignLink.Chapters)
                    {
                        if (c == null || c.IsOvertime) continue;
                        total++;
                        if (save != null && save.Done(c.Id)) done++;
                    }
                    if (total > 0) return "<color=#9AA4B2>SHIFTS</color>  " + done + "/" + total;
                }
                catch { }
            }
            return "<color=#9AA4B2>LEVELS</color>  " + levelsCompleted + "/" + LevelCatalog.MainGameCount;
        }

        /// <summary>RUN STATS body text (read-only, for tests).</summary>
        public string StatsText => _stats != null ? _stats.text : "";

        protected override void OnOpened()
        {
            var gs = GameSession.Instance;
            int idx = gs != null ? gs.LevelIndex : -1;
            if (idx < 0) idx = LevelCatalog.IndexOfScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            var info = LevelCatalog.Get(idx);
            var ch = CampaignLink.IsCampaign ? CampaignLink.Current : null;
            _level.text = ch != null ? ch.ShiftLabel + "  /  " + (ch.Title ?? "").ToUpperInvariant()
                : info != null ? CampaignLink.ShiftLabelFor(idx) + "  /  " + info.Title : "TEST CHAMBER";
            _seed.text = (ch != null ? (ch.IsOvertime ? "OVERTIME (UNPAID)" : CampaignLink.ActHeader(ch.Act)) + "   ·   TEAM CODE #" : "TEAM SEED #") + (gs != null ? gs.Seed : 0)
                + (gs != null && gs.CheckpointIndex >= 0 ? "   ·   CHECKPOINT " + Checkpoint.RankOf(gs.CheckpointIndex) : "");
            Layout();
            if (HRButton.gameObject.activeSelf) UITween.Punch(HRButton.Rt, 0.12f, 0.4f);
            var st = gs != null ? gs.Stats : null;
            if (st != null)
            {
                float levelTime = gs.LevelStartTime > 0f ? Mathf.Max(0f, Time.time - gs.LevelStartTime) : 0f;
                _stats.text =
                    "<color=#9AA4B2>PLAY TIME</color>  " + UIFactory.FormatTime(st.PlayTime > 0f ? st.PlayTime : levelTime) + "      " + ProgressStat(ch, st.LevelsCompleted) + "\n" +
                    "<color=#9AA4B2>SWITCHES</color>  " + st.Switches + "      <color=#9AA4B2>THROWS</color>  " + st.Throws + "      <color=#9AA4B2>BRIDGES</color>  " + st.Bridges + "\n" +
                    "<color=#9AA4B2>FLOPS</color>  " + st.Flops + "      <color=#9AA4B2>KO'S</color>  " + st.KnockOuts + "      <color=#9AA4B2>CAPTURES</color>  " + st.Captures + "\n" +
                    "<color=#9AA4B2>LOST TO THE VOID</color>  " + st.Lost + "      <color=#9AA4B2>REVIVES</color>  " + st.Revives + "\n" +
                    "<color=#9AA4B2>SHOTS FIRED</color>  " + st.ShotsFired + "      <color=#9AA4B2>RESTARTS</color>  " + st.Restarts;
            }
            else _stats.text = "No stats. No accountability.";
            BuildTeam();
            // slide in to the HOME positions: closing the menu mid-slide kills the tweens (UIButton.OnDisable), so the
            // current position is not a valid target (quick P-P left the buttons shifted left for good)
            for (int i = 0; i < Buttons.Count && i < _home.Count; i++)
                UITween.Move(Buttons[i].Rt, _home[i], 0.35f, Ease.OutBack, 0.03f * i, _home[i] + new Vector2(-300f, 0f));
            _guardResume = true;
            _guardPointer = ShiftersInput.PointerPosition;
            _guardNavNeutral = ShiftersInput.NavigateStick.sqrMagnitude < 0.04f;
        }

        protected override void OnClosed() => _guardResume = false;

        // After the EventSystem's Update (hover / navigation) of the same frame: undo a hover selection made by a pointer
        // that has not moved since the menu opened; keep anything the player chose.
        void LateUpdate()
        {
            if (_guardResume) KeepResumeSelected();
        }

        void KeepResumeSelected()
        {
            if (!IsOpen || DefaultSelection == null) { _guardResume = false; return; }
            if (PlayerTookOver()) { _guardResume = false; return; }
            // nothing the player did since the menu opened can have moved the selection: a stationary pointer hovered a
            // button that appeared / slid under it, or a key held from gameplay (W/S) repeated the UI navigation
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != DefaultSelection.gameObject) { SelectDefault(); ResumeGuardReverts++; }
        }

        bool PlayerTookOver()
        {
            if ((ShiftersInput.PointerPosition - _guardPointer).sqrMagnitude > 9f) return true;   // the mouse really moved
            if (ShiftersInput.AnyKeyPressed) return true;                                          // a NEW key / button / D-pad press
            float nav = ShiftersInput.NavigateStick.sqrMagnitude;                                  // arrows / sticks
            if (!_guardNavNeutral) { if (nav < 0.04f) _guardNavNeutral = true; return false; }     // held since before the menu
            return nav > 0.25f;
        }

        void BuildTeam()
        {
            for (int i = _team.childCount - 1; i >= 1; i--) Destroy(_team.GetChild(i).gameObject);
            var cm = CharacterManager.Instance;
            var team = SessionActions.Team;
            for (int i = 0; i < 3; i++)
            {
                var c = cm != null ? cm.Get(i) : null;
                var p = c != null ? c.Profile : (team != null && team.Members != null && i < team.Members.Length ? team.Members[i] : null);
                if (p == null) continue;
                var card = UIFactory.Panel(_team, "Mini" + i, new Vector2(244f, 96f), UIFactory.WithAlpha(UIColors.Panel, 0.92f), 14, true, UIFactory.WithAlpha(p.PrimaryColor, 0.7f));
                card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0f, 1f); card.Rect.pivot = new Vector2(0f, 1f);
                card.Rect.anchoredPosition = new Vector2(i * 258f, -44f);
                var pv = PortraitView.Create(card.Rect, 64f, true);
                var prt = (RectTransform)pv.transform;
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f); prt.pivot = new Vector2(0f, 0.5f); prt.anchoredPosition = new Vector2(14f, 0f);
                pv.Bind(i, p);
                var n = UIFactory.Text(card.Rect, "Name", p.DisplayName.ToUpperInvariant(), FontLibrary.Heavy, 22f, UIColors.Text, new Vector2(0f, 1f), new Vector2(88f, -14f), new Vector2(146f, 28f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
                UIFactory.AutoSize(n, 13f, 22f);
                UIFactory.AutoSize(n, 14f, 22f);
                var t = UIFactory.Text(card.Rect, "Title", p.Title, FontLibrary.Label, 16f, p.PrimaryColor, new Vector2(0f, 1f), new Vector2(88f, -42f), new Vector2(146f, 22f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
                UIFactory.AutoSize(t, 10f, 16f);
                t.characterSpacing = 2f;
                string state = c == null ? "READY" : c.State == CharacterState.Alive ? (c.IsActive ? "ACTIVE" : "ALIVE") : c.State == CharacterState.Downed ? "KO'D" : c.State.ToString().ToUpperInvariant();
                Color sc = c == null || c.IsAlive ? UIColors.Success : UIColors.Danger;
                var s = UIFactory.Text(card.Rect, "State", state, FontLibrary.Label, 17f, sc, new Vector2(0f, 0f), new Vector2(88f, 10f), new Vector2(146f, 22f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
                s.characterSpacing = 4f;
            }
        }
    }
}
