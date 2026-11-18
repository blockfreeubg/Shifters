// =====================================================================================
//  SHIFTERS - SettingsScreen.cs   (UI agent)
//  Two tabs. GAME: volumes, mouse sensitivity, invert Y, FOV, switch camera (Cinematic/Fast), tutorials,
//  quality, fullscreen, screen shake, SHOW FPS (BenchOverlay, polish H5). STORY & ASSISTS (§15.3): STORY FULL/SHORT/OFF, VILLAIN HECKLES
//  OFTEN/SOMETIMES/NEVER, SPOOKY FULL/MILD, COLOR SHIFT EFFECT, HR ASSISTS, SUBTITLE SPEED.
//  Every change writes GameSession.Settings and calls ApplySettings() (live preview); leaving the screen
//  calls SaveSettings(). Keyboard: Up from the first row reaches the tabs; Left/Right on a tab switches it.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class SettingsScreen : UIScreen
    {
        public readonly List<UISettingRow> Rows = new List<UISettingRow>();
        public UIButton BackButton { get; private set; }
        public UIButton GameTab { get; private set; }
        public UIButton StoryTab { get; private set; }
        /// <summary>0 = GAME, 1 = STORY & ASSISTS.</summary>
        public int Tab { get; private set; }
        static readonly string[] OnOff = { "OFF", "ON" };
        public static readonly string[] StoryOptions = { "FULL", "SHORT", "OFF" };
        public static readonly string[] HeckleOptions = { "OFTEN", "SOMETIMES", "NEVER" };
        public static readonly string[] SpookyOptions = { "FULL", "MILD" };
        readonly List<UISettingRow>[] _tabRows = { new List<UISettingRow>(), new List<UISettingRow>() };
        RectTransform[] _panels = new RectTransform[2];
        TextMeshProUGUI _note;
        RectTransform _tabMark;
        float _rowStep = 64f;

        protected override void Build()
        {
            Backdrop(0.86f, true);
            Title("SETTINGS", new Vector2(0.5f, 1f), new Vector2(0f, -30f), 70f).rectTransform.pivot = new Vector2(0.5f, 1f);
            GameTab = TabButton("GAME", new Vector2(-190f, -126f), 0);
            StoryTab = TabButton("STORY & ASSISTS", new Vector2(190f, -126f), 1);
            _tabMark = UIFactory.Image(Rect, "TabMark", UISprites.Rounded(3), UIColors.AccentYellow).rectTransform;
            _tabMark.anchorMin = _tabMark.anchorMax = new Vector2(0.5f, 1f); _tabMark.pivot = new Vector2(0.5f, 1f);
            _tabMark.sizeDelta = new Vector2(300f, 6f);
            _tabMark.anchoredPosition = GameTab.Rt.anchoredPosition + new Vector2(0f, -62f);

            var s = SessionActions.Settings;
            var p0 = MakePanel("Panel_Game", 0);
            float y = 336f; _rowStep = 60f;   // 12 rows fit the 760 px panel
            Add(p0, 0, UISettingRow.CreateSlider(p0, "MASTER VOLUME", 0f, 1f, s.MasterVolume, false, Pct, v => { SessionActions.Settings.MasterVolume = v; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateSlider(p0, "MUSIC VOLUME", 0f, 1f, s.MusicVolume, false, Pct, v => { SessionActions.Settings.MusicVolume = v; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateSlider(p0, "SFX VOLUME", 0f, 1f, s.SfxVolume, false, Pct, v => { SessionActions.Settings.SfxVolume = v; Apply(); Sfx.Play(SfxId.UIHover, 0.8f); }), ref y);
            var sens = UISettingRow.CreateSlider(p0, "MOUSE SENSITIVITY", 0.2f, 3f, s.MouseSensitivity, false, v => v.ToString("0.0") + "x", v => { SessionActions.Settings.MouseSensitivity = v; Apply(); });
            DevicePromptText.AttachInChildren(sens.transform.parent.gameObject, "{kb:MOUSE}{pad:LOOK} SENSITIVITY", false);   // slider rows: component on the track, label on its parent. Scales mouse AND right-stick look; Label stays the row key
            Add(p0, 0, sens, ref y);
            Add(p0, 0, UISettingRow.CreateOption(p0, "INVERT Y", OnOff, s.InvertY ? 1 : 0, i => { SessionActions.Settings.InvertY = i == 1; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateSlider(p0, "FIELD OF VIEW", 50f, 90f, s.Fov, true, v => Mathf.RoundToInt(v) + "°", v => { SessionActions.Settings.Fov = v; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateOption(p0, "SWITCH CAMERA", new[] { "FAST", "CINEMATIC" }, s.CinematicSwitch ? 1 : 0, i => { SessionActions.Settings.CinematicSwitch = i == 1; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateOption(p0, "TUTORIALS", OnOff, s.Tutorials ? 1 : 0, i => { SessionActions.Settings.Tutorials = i == 1; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateOption(p0, "QUALITY", new[] { "LOW", "HIGH" }, Mathf.Clamp(s.Quality, 0, 1), i => { SessionActions.Settings.Quality = i; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateOption(p0, "FULLSCREEN", OnOff, s.Fullscreen ? 1 : 0, i => { SessionActions.Settings.Fullscreen = i == 1; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateOption(p0, "SCREEN SHAKE", OnOff, s.ScreenShake ? 1 : 0, i => { SessionActions.Settings.ScreenShake = i == 1; Apply(); }), ref y);
            Add(p0, 0, UISettingRow.CreateOption(p0, "SHOW FPS", OnOff, s.ShowFps ? 1 : 0, i => { SessionActions.Settings.ShowFps = i == 1; Apply(); }), ref y);

            var p1 = MakePanel("Panel_Story", 1);
            y = 330f; _rowStep = 64f;
            Add(p1, 1, UISettingRow.CreateOption(p1, "STORY", StoryOptions, Mathf.Clamp(s.StoryMode, 0, 2), i => { SessionActions.Settings.StoryMode = i; Apply(); }), ref y);
            Add(p1, 1, UISettingRow.CreateOption(p1, "VILLAIN HECKLES", HeckleOptions, Mathf.Clamp(s.Heckles, 0, 2), i => { SessionActions.Settings.Heckles = i; Apply(); }), ref y);
            Add(p1, 1, UISettingRow.CreateOption(p1, "SPOOKY", SpookyOptions, s.SpookyMild ? 1 : 0, i => { SessionActions.Settings.SpookyMild = i == 1; Apply(); }), ref y);
            Add(p1, 1, UISettingRow.CreateOption(p1, "COLOR SHIFT EFFECT", OnOff, s.ColorShiftFx ? 1 : 0, i => { SessionActions.Settings.ColorShiftFx = i == 1; Apply(); }), ref y);
            Add(p1, 1, UISettingRow.CreateOption(p1, "HR ASSISTS", OnOff, s.HRAssists ? 1 : 0, i => { SessionActions.Settings.HRAssists = i == 1; Apply(); }), ref y);
            Add(p1, 1, UISettingRow.CreateSlider(p1, "SUBTITLE SPEED", 0.5f, 2f, Mathf.Clamp(s.SubtitleSpeed, 0.5f, 2f), false, v => v.ToString("0.0") + "x", v => { SessionActions.Settings.SubtitleSpeed = v; Apply(); }), ref y);
            _note = UIFactory.Text(p1, "Note", "", FontLibrary.Body, 26f, UIColors.TextMuted, new Vector2(0.5f, 0.5f), new Vector2(0f, y - 70f), new Vector2(900f, 150f), TextAlignmentOptions.Top, true);
            _note.fontStyle = FontStyles.Italic;

            BackButton = UIFactory.Button(Rect, "Back", "BACK", () => OnBack(), new Vector2(300f, 64f), UIButtonStyle.Primary, 28f);
            BackButton.Rt.anchorMin = BackButton.Rt.anchorMax = new Vector2(0.5f, 0f); BackButton.Rt.pivot = new Vector2(0.5f, 0f);
            BackButton.Rt.anchoredPosition = new Vector2(0f, 30f);
            ShowTab(0, false);
        }

        UIButton TabButton(string label, Vector2 pos, int tab)
        {
            var b = UIFactory.Button(Rect, "Tab_" + label, label, () => ShowTab(tab, true), new Vector2(360f, 56f), UIButtonStyle.Secondary, 26f);
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 1f); b.Rt.pivot = new Vector2(0.5f, 1f);
            b.Rt.anchoredPosition = pos;
            return b;
        }

        RectTransform MakePanel(string name, int tab)
        {
            var panel = UIFactory.Panel(Rect, name, new Vector2(1060f, 760f), UIFactory.WithAlpha(UIColors.Panel, 0.96f), 24, true, UIFactory.WithAlpha(UIColors.Border, 1f));
            panel.Rect.anchorMin = panel.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            panel.Rect.anchoredPosition = new Vector2(0f, -56f);
            _panels[tab] = panel.Rect;
            return panel.Rect;
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        void Add(RectTransform panel, int tab, UISettingRow row, ref float y)
        {
            var rt = (RectTransform)(row.Slider != null ? row.transform.parent : row.transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= _rowStep;
            Rows.Add(row);
            _tabRows[tab].Add(row);
        }

        /// <summary>Switch tab (tests / keyboard). select = move the selection to the tab's first row.</summary>
        public void ShowTab(int tab, bool select)
        {
            Tab = Mathf.Clamp(tab, 0, 1);
            for (int i = 0; i < 2; i++) _panels[i].gameObject.SetActive(i == Tab);
            var tabRt = (Tab == 0 ? GameTab : StoryTab).Rt;
            UITween.Move(_tabMark, tabRt.anchoredPosition + new Vector2(0f, -62f), 0.18f, Ease.OutCubic);
            GameTab.Label.fontStyle = Tab == 0 ? FontStyles.Underline : FontStyles.Normal;
            StoryTab.Label.fontStyle = Tab == 1 ? FontStyles.Underline : FontStyles.Normal;
            var rows = _tabRows[Tab];
            var sels = new List<Selectable>();
            foreach (var r in rows) sels.Add(r.Selectable);
            sels.Add(BackButton.Selectable);
            UIFactory.LinkVertical(sels, false);
            var activeTab = Tab == 0 ? GameTab : StoryTab;
            // first row: Up -> active tab; tabs: Left/Right between them, Down -> first row; Back: Down wraps to the tab
            var n0 = sels[0].navigation; n0.selectOnUp = activeTab.Selectable; sels[0].navigation = n0;
            var nb = BackButton.Selectable.navigation; nb.selectOnDown = activeTab.Selectable; BackButton.Selectable.navigation = nb;
            GameTab.Selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = StoryTab.Selectable, selectOnLeft = StoryTab.Selectable, selectOnDown = sels[0], selectOnUp = BackButton.Selectable };
            StoryTab.Selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = GameTab.Selectable, selectOnRight = GameTab.Selectable, selectOnDown = sels[0], selectOnUp = BackButton.Selectable };
            DefaultSelection = sels[0];
            RefreshNote();
            if (select && IsOpen)
            {
                Sfx.Play(SfxId.UIClick, 0.7f);
                UITween.Punch(_panels[Tab], 0.03f, 0.2f);
                SelectDefault();
            }
        }

        void RefreshNote()
        {
            if (_note == null) return;
            var s = SessionActions.Settings;
            _note.text = (s.StoryMode == 0 ? "Story FULL: seen scenes shorten themselves automatically." : s.StoryMode == 1 ? "Story SHORT: the gist, and the jokes." : "Story OFF: just puzzles. Max is devastated.")
                + "\n" + (s.HRAssists ? "HR ASSISTS: after a few restarts, Pause offers LET HR HANDLE IT." : "HR ASSISTS off. HR is relieved.");
        }

        void Apply() { SessionActions.ApplySettings(); RefreshNote(); }

        void Update()
        {
            // Left/Right while a tab is selected switch the visible panel (selection follows the navigation)
            if (!IsOpen) return;
            var es = UnityEngine.EventSystems.EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return;
            if (sel == GameTab.gameObject && Tab != 0) ShowTab(0, false);
            else if (sel == StoryTab.gameObject && Tab != 1) ShowTab(1, false);
        }

        protected override void OnOpened()
        {
            // reflect current settings (they may have changed elsewhere)
            var s = SessionActions.Settings;
            foreach (var r in Rows)
            {
                switch (r.Label)
                {
                    case "MASTER VOLUME": r.Slider.SetValueWithoutNotify(s.MasterVolume); break;
                    case "MUSIC VOLUME": r.Slider.SetValueWithoutNotify(s.MusicVolume); break;
                    case "SFX VOLUME": r.Slider.SetValueWithoutNotify(s.SfxVolume); break;
                    case "MOUSE SENSITIVITY": r.Slider.SetValueWithoutNotify(s.MouseSensitivity); break;
                    case "FIELD OF VIEW": r.Slider.SetValueWithoutNotify(s.Fov); break;
                    case "INVERT Y": r.SetIndex(s.InvertY ? 1 : 0); break;
                    case "SWITCH CAMERA": r.SetIndex(s.CinematicSwitch ? 1 : 0); break;
                    case "TUTORIALS": r.SetIndex(s.Tutorials ? 1 : 0); break;
                    case "QUALITY": r.SetIndex(Mathf.Clamp(s.Quality, 0, 1)); break;
                    case "FULLSCREEN": r.SetIndex(s.Fullscreen ? 1 : 0); break;
                    case "SCREEN SHAKE": r.SetIndex(s.ScreenShake ? 1 : 0); break;
                    case "SHOW FPS": r.SetIndex(s.ShowFps ? 1 : 0); break;
                    case "STORY": r.SetIndex(Mathf.Clamp(s.StoryMode, 0, 2)); break;
                    case "VILLAIN HECKLES": r.SetIndex(Mathf.Clamp(s.Heckles, 0, 2)); break;
                    case "SPOOKY": r.SetIndex(s.SpookyMild ? 1 : 0); break;
                    case "COLOR SHIFT EFFECT": r.SetIndex(s.ColorShiftFx ? 1 : 0); break;
                    case "HR ASSISTS": r.SetIndex(s.HRAssists ? 1 : 0); break;
                    case "SUBTITLE SPEED": r.Slider.SetValueWithoutNotify(Mathf.Clamp(s.SubtitleSpeed, 0.5f, 2f)); break;
                }
                r.RefreshText();
            }
            ShowTab(0, false);
        }

        protected override void OnClosed() => SessionActions.SaveSettings();

        public UISettingRow Row(string label)
        {
            foreach (var r in Rows) if (r.Label == label) return r;
            return null;
        }
    }
}
