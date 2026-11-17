// =====================================================================================
//  SHIFTERS - CampaignEndScreen.cs   (UI agent)
//  "THE GREAT SHIFT - COMPLETE" + the HR FILE (§15.4): campaign time, interns printed, highest clone,
//  teamwork violations, captures, most-thrown intern, world color, president of the day, TEAM CODE and
//  the grade ("TEAMWORK: A+ - PAPERWORK: F"; "HR handled it: 2x. HR would like a word." with assists).
//  Placeholder = the R0 end card "MAX REMAINS IN POWER... FOR NOW.". CREDITS / MAIN MENU.
//  Opened by UIManager.ShowCampaignEnd / RequestCampaignEnd (queued for the next main menu from a level).
//  Optional director members read by name: CampaignTime (float), MostThrown (string), President (string).
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class CampaignEndScreen : UIScreen
    {
        /// <summary>R0 card instead of the full ending (set by ShowCampaignEnd).</summary>
        public bool Placeholder;
        bool _postCredits;   // the full ending still owes the C16 post-credits scene
        RectTransform _rays, _title, _file;
        TextMeshProUGUI _pre, _titleT, _sub, _left, _right, _grade, _hrNote;
        MeTvFrame _tv;
        public UIButton CreditsButton { get; private set; }
        public UIButton MenuButton { get; private set; }
        public string TitleText => _titleT != null ? _titleT.text : "";
        public string GradeText => _grade != null ? _grade.text : "";
        public string FileText => (_left != null ? _left.text : "") + "\n" + (_right != null ? _right.text : "");

        protected override void Build()
        {
            Backdrop(0.82f, true);
            var rays = UIFactory.Image(Rect, "Rays", UISprites.RadialLines(32), UIFactory.WithAlpha(UIColors.AccentYellow, 0.16f));
            _rays = rays.rectTransform;
            _rays.anchorMin = _rays.anchorMax = new Vector2(0.5f, 1f);
            _rays.sizeDelta = new Vector2(2400f, 2400f);
            _rays.anchoredPosition = new Vector2(0f, -220f);
            _pre = UIFactory.Text(Rect, "Pre", "", FontLibrary.Label, 32f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(1700f, 40f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            _pre.characterSpacing = 6f;
            _titleT = UIFactory.Text(Rect, "Title", "", FontLibrary.Display, 112f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1800f, 140f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(_titleT, 60f, 112f);
            if (FontLibrary.LogoMaterial != null) _titleT.fontSharedMaterial = FontLibrary.LogoMaterial;
            _titleT.enableVertexGradient = true;
            _title = _titleT.rectTransform;
            _sub = UIFactory.Text(Rect, "Sub", "", FontLibrary.Body, 30f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -248f), new Vector2(1600f, 40f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            _sub.fontStyle = FontStyles.Italic;

            // HR FILE (paper)
            var p = UIFactory.Panel(Rect, "HRFile", new Vector2(1240f, 560f), UIColors.Hex(0xF3EBDD, 0.99f), 16, true);
            _file = p.Rect;
            _file.anchorMin = _file.anchorMax = new Vector2(0.5f, 0.5f);
            _file.anchoredPosition = new Vector2(-150f, -110f);
            _file.localEulerAngles = new Vector3(0f, 0f, -1f);
            var ink = UIColors.Hex(0x2B2118);
            var hh = UIFactory.Text(_file, "Head", "HR FILE", FontLibrary.Display, 40f, ink, new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(400f, 50f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            hh.characterSpacing = 3f;
            var hs = UIFactory.Text(_file, "HeadSub", "SUBJECT: THE INTERNS   ·   RE: THE WHOLE WORLD", FontLibrary.Label, 20f, UIColors.Hex(0x6B5A48), new Vector2(1f, 1f), new Vector2(-40f, -38f), new Vector2(700f, 26f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));
            hs.characterSpacing = 5f;
            var rule = UIFactory.Image(_file, "Rule", null, UIColors.Hex(0x2B2118, 0.35f));
            rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -86f); rule.rectTransform.sizeDelta = new Vector2(1160f, 2f);
            _left = UIFactory.Text(_file, "Left", "", FontLibrary.Body, 26f, ink, new Vector2(0f, 1f), new Vector2(40f, -104f), new Vector2(570f, 330f), TextAlignmentOptions.TopLeft, true, new Vector2(0f, 1f));
            _left.lineSpacing = 10f;
            UIFactory.AutoSize(_left, 16f, 26f);
            _right = UIFactory.Text(_file, "Right", "", FontLibrary.Body, 26f, ink, new Vector2(0f, 1f), new Vector2(640f, -104f), new Vector2(570f, 330f), TextAlignmentOptions.TopLeft, true, new Vector2(0f, 1f));
            _right.lineSpacing = 10f;
            UIFactory.AutoSize(_right, 16f, 26f);
            _grade = UIFactory.Text(_file, "Grade", "", FontLibrary.Display, 40f, UIColors.Danger, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(1160f, 54f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f));
            UIFactory.AutoSize(_grade, 24f, 40f);
            _grade.rectTransform.localEulerAngles = new Vector3(0f, 0f, 1.5f);
            _hrNote = UIFactory.Text(_file, "HRNote", "", FontLibrary.Body, 24f, UIColors.Hex(0x6B5A48), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(1160f, 34f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f));
            _hrNote.fontStyle = FontStyles.Italic;

            _tv = MeTvFrame.Create(Rect, new Vector2(1f, 0.5f), new Vector2(-60f, -60f), new Vector2(420f, 230f), "EX-SUPREME SOLE EXECUTIVE", 26f);

            CreditsButton = UIFactory.Button(Rect, "Credits", "CREDITS", OpenCredits, new Vector2(320f, 70f), UIButtonStyle.Secondary, 30f);
            CreditsButton.Rt.anchorMin = CreditsButton.Rt.anchorMax = new Vector2(0.5f, 0f); CreditsButton.Rt.pivot = new Vector2(0.5f, 0f);
            CreditsButton.Rt.anchoredPosition = new Vector2(-190f, 40f);
            MenuButton = UIFactory.Button(Rect, "Menu", "MAIN MENU", () => OnBack(), new Vector2(320f, 70f), UIButtonStyle.Primary, 30f);
            MenuButton.Rt.anchorMin = MenuButton.Rt.anchorMax = new Vector2(0.5f, 0f); MenuButton.Rt.pivot = new Vector2(0.5f, 0f);
            MenuButton.Rt.anchoredPosition = new Vector2(190f, 40f);
            UIFactory.LinkHorizontal(new List<Selectable> { CreditsButton.Selectable, MenuButton.Selectable }, true);
            DefaultSelection = MenuButton.Selectable;
        }

        void OpenCredits()
        {
            var m = Manager;
            if (m == null) return;
            if (_postCredits) { _postCredits = false; m.Credits.OnceClosed = () => CampaignLink.PlayPostCredits(null); }
            IsReopen = true;
            m.OpenCredits();
        }

        protected override void OnOpened()
        {
            if (!IsReopen) _postCredits = !Placeholder;
            IsReopen = false;
            if (Placeholder) OpenPlaceholder(); else OpenFull();
            UITween.Scale(_title, 1f, 0.7f, Ease.OutElastic, 0.1f, 0.2f);
            UITween.Move(_file, _file.anchoredPosition, 0.6f, Ease.OutBack, 0.35f, _file.anchoredPosition + new Vector2(0f, -700f));
            Sfx.Play(SfxId.LevelComplete);
        }

        void OpenPlaceholder()
        {
            _pre.text = "THE GREAT SHIFT  ·  TO BE CONTINUED";
            _titleT.text = "MAX REMAINS IN POWER... FOR NOW.";
            _titleT.colorGradient = new VertexGradient(UIColors.Hex(0xC9CED6), UIColors.Hex(0xC9CED6), UIColors.Hex(0x7A7F87), UIColors.Hex(0x7A7F87));
            _sub.text = "The interns will be back. With paperwork.";
            _rays.gameObject.SetActive(false);
            FillFile();
            _tv.Say("Ha! Nobody impeaches ME. Not today. Not ever. ...Probably.", 0.8f);
            AudioManager.Instance?.PlayMusic(MusicId.Villain);
        }

        void OpenFull()
        {
            _pre.text = "THE WORLD IS BRIGHT AGAIN. (THE INTERNS ARGUE A LOT.)";
            _titleT.text = "THE GREAT SHIFT  ·  COMPLETE";
            _titleT.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.Hex(0x3DE0FF), UIColors.Accent, UIColors.Hex(0xB07CFF));
            _sub.text = "Max was impeached. Unanimously. He voted against. Alone.";
            _rays.gameObject.SetActive(true);
            FillFile();
            _tv.Say("Fine. FINE. ...Same time next shift?", 1.0f);
            AudioManager.Instance?.PlayMusic(MusicId.Finale);
        }

        void FillFile()
        {
            var gs = GameSession.Instance;
            var st = gs != null ? gs.Stats : null;
            var team = SessionActions.Team;
            var save = CampaignLink.Save;
            float time = DirectorFloat("CampaignTime");
            if (time <= 0f && save != null) time = save.time;
            if (time <= 0f && st != null) time = st.PlayTime;
            int printed = 0, highestGen = 0;
            string highest = "NOBODY";
            if (team != null && team.Members != null)
                foreach (var m in team.Members)
                {
                    if (m == null) continue;
                    printed += Mathf.Max(1, m.CloneGeneration);
                    if (m.CloneGeneration > highestGen) { highestGen = m.CloneGeneration; highest = m.DisplayName.ToUpperInvariant(); }
                }
            int violations = Mathf.Max(CampaignLink.TeamworkViolations, st != null ? st.TeamworkViolations : 0);
            int captures = st != null ? st.Captures : 0;
            int hr = Mathf.Max(st != null ? st.HRInterventions : 0, save != null ? save.hr : 0);
            string mostThrown = DirectorString("MostThrown");
            if (string.IsNullOrEmpty(mostThrown)) mostThrown = team != null && team.Members != null && team.Members.Length > 0 && team.Members[team.Members.Length - 1] != null
                ? team.Members[team.Members.Length - 1].DisplayName.ToUpperInvariant() + " (PROBABLY)" : "EVERYONE, EQUALLY";
            string president = DirectorString("President");
            if (string.IsNullOrEmpty(president) && team != null && team.Members != null && team.Members.Length > 0)
            {
                var pm = team.Members[Mathf.Abs(team.Seed + System.DateTime.Now.DayOfYear) % team.Members.Length];
                president = pm != null ? pm.DisplayName.ToUpperInvariant() : "";
            }
            if (string.IsNullOrEmpty(president)) president = "PERCY (ACTING)";
            float color = Placeholder ? Mathf.Clamp01(CampaignLink.Meter) : 1f;
            const string K = "<color=#6B5A48>";
            _left.text =
                K + "CAMPAIGN TIME</color>  " + UIFactory.FormatTime(time) + "\n" +
                K + "INTERNS PRINTED</color>  " + printed + "\n" +
                K + "HIGHEST CLONE</color>  " + highest + "\n" +
                K + "TEAMWORK VIOLATIONS</color>  " + violations + "\n" +
                K + "CAPTURES</color>  " + captures;
            _right.text =
                K + "MOST-THROWN INTERN</color>  " + mostThrown + "\n" +
                K + "WORLD COLOR</color>  " + Mathf.RoundToInt(color * 100f) + "%\n" +
                K + "PRESIDENT OF THE DAY</color>  " + (Placeholder ? "STILL MAX (UGH)" : president) + "\n" +
                K + "TEAM CODE</color>  " + (team != null ? team.Seed.ToString() : "-") + "\n" +
                K + "EXHIBITS</color>  " + ExhibitCount() + " / 3";
            string teamwork = violations >= 21 ? "A+" : violations >= 6 ? "A" : "B-";
            string paperwork = st != null && st.Lost == 0 ? "C-" : "F";
            _grade.text = "TEAMWORK: " + teamwork + "   ·   PAPERWORK: " + paperwork;
            _hrNote.text = hr > 0 ? "HR handled it: " + hr + "x. HR would like a word." : "HR never had to step in. HR is suspicious.";
        }

        static int ExhibitCount()
        {
            int n = 0;
            foreach (var id in new[] { "A", "B", "C" }) if (CampaignLink.HasExhibit(id)) n++;
            return n;
        }

        static float DirectorFloat(string member)
        {
            var d = CampaignLink.Director;
            if (d == null) return 0f;
            try
            {
                var p = d.GetType().GetProperty(member);
                if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(d);
                var f = d.GetType().GetField(member);
                if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(d);
            }
            catch (System.Exception) { }
            return 0f;
        }

        static string DirectorString(string member)
        {
            var d = CampaignLink.Director;
            if (d == null) return null;
            try
            {
                var p = d.GetType().GetProperty(member);
                if (p != null) return p.GetValue(d) as string;
                var f = d.GetType().GetField(member);
                if (f != null) return f.GetValue(d) as string;
            }
            catch (System.Exception) { }
            return null;
        }

        // reopened after the credits pop back to it: keep the post-credits bookkeeping
        bool IsReopen;

        public override void OnBack()
        {
            Sfx.Play(SfxId.UIBack);
            AudioManager.Instance?.PlayMusic(MusicId.Menu);
            Manager?.Pop();
            if (_postCredits) { _postCredits = false; CampaignLink.PlayPostCredits(null); }
        }

        void Update()
        {
            if (!IsOpen || _rays == null || !_rays.gameObject.activeSelf) return;
            _rays.localEulerAngles = new Vector3(0f, 0f, _rays.localEulerAngles.z + UITime.Delta * 8f);
        }
    }
}
