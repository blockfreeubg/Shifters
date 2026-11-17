// =====================================================================================
//  SHIFTERS - CaseFileScreen.cs   (UI agent)
//  CASE FILE (Pause -> CASE FILE, §15.2): a manila folder with the goal ("Capture Max. Impeach Max.
//  Bring the color back."), where we are (act / shift), WORLD COLOR, exhibits A/B/C as sprite tags
//  (ACQUIRED / MISSING stamps), the LOST & REPRINTED log and TEAMWORK VIOLATIONS with HR's verdict.
//  Data: CampaignLink (director, else the campaign save JSON). BACK returns to Pause.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class CaseFileScreen : UIScreen
    {
        static readonly Color Manila = UIColors.Hex(0xE8D3A2);
        static readonly Color Ink = UIColors.Hex(0x2B2118);
        static readonly Color InkMuted = UIColors.Hex(0x6B5A48);
        static readonly string[] Exhibits = { "A", "B", "C" };

        RectTransform _folder;
        TextMeshProUGUI _now, _lost, _violations, _hr, _colorPct;
        RectTransform _colorFill;
        readonly Image[] _exCards = new Image[3];
        readonly TextMeshProUGUI[] _exStamp = new TextMeshProUGUI[3];
        public UIButton BackButton { get; private set; }
        /// <summary>Exhibit ids shown as ACQUIRED at the last open (tests).</summary>
        public readonly List<string> ShownExhibits = new List<string>();
        public string LostText => _lost != null ? _lost.text : "";
        public string ViolationsText => _violations != null ? _violations.text : "";

        /// <summary>HR's verdict for a violation count (§15.2).</summary>
        public static string HRLine(int v) => v <= 5 ? "Barely cooperative. Promising." : v <= 20 ? "Excessive cooperation. See me." : "This is a TEAM. I'm calling HR. I AM HR.";

        protected override void Build()
        {
            Backdrop(0.8f, false);
            var f = UIFactory.Panel(Rect, "Folder", new Vector2(1560f, 840f), Manila, 16, true);
            _folder = f.Rect;
            _folder.anchorMin = _folder.anchorMax = new Vector2(0.5f, 0.5f);
            _folder.anchoredPosition = new Vector2(0f, 10f);
            var tab = UIFactory.Image(_folder, "Tab", UISprites.Rounded(14), Manila);
            var trt = tab.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 0f);
            trt.anchoredPosition = new Vector2(40f, -14f); trt.sizeDelta = new Vector2(440f, 64f);
            var tabT = UIFactory.Text(trt, "T", "CASE FILE #001: MAX", FontLibrary.Display, 26f, Ink, new Vector2(0f, 0.5f), new Vector2(22f, 6f), new Vector2(420f, 36f), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            tabT.characterSpacing = 1f;
            var conf = UIFactory.Text(_folder, "Confidential", "CONFIDENTIAL", FontLibrary.Display, 44f, UIFactory.WithAlpha(UIColors.Danger, 0.55f), new Vector2(1f, 1f), new Vector2(-50f, -34f), new Vector2(460f, 56f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));
            conf.rectTransform.localEulerAngles = new Vector3(0f, 0f, 6f);

            var head = UIFactory.Text(_folder, "Head", "THE PEOPLE vs. MAX (SUPREME SOLE EXECUTIVE)", FontLibrary.Display, 38f, Ink, new Vector2(0f, 1f), new Vector2(50f, -36f), new Vector2(1100f, 48f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            UIFactory.AutoSize(head, 24f, 38f);
            var goal = UIFactory.Text(_folder, "Goal", "<b>GOAL:</b> Capture Max. Impeach Max. Bring the color back.", FontLibrary.Body, 30f, Ink, new Vector2(0f, 1f), new Vector2(50f, -94f), new Vector2(1400f, 40f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            goal.richText = true;
            _now = UIFactory.Text(_folder, "Now", "", FontLibrary.Label, 26f, InkMuted, new Vector2(0f, 1f), new Vector2(50f, -140f), new Vector2(1000f, 32f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            _now.characterSpacing = 4f;
            // world color bar
            var cl = UIFactory.Text(_folder, "ColorLabel", "WORLD COLOR", FontLibrary.Heavy, 22f, Ink, new Vector2(1f, 1f), new Vector2(-470f, -140f), new Vector2(200f, 30f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            cl.characterSpacing = 3f;
            var track = UIFactory.Image(_folder, "ColorTrack", UISprites.Rounded(8), UIFactory.WithAlpha(Ink, 0.25f));
            var crt = track.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(-300f, -146f); crt.sizeDelta = new Vector2(180f, 18f);
            var fill = UIFactory.Image(crt, "Fill", UISprites.Rounded(8), UIColors.AccentYellow);
            _colorFill = fill.rectTransform;
            _colorFill.anchorMin = Vector2.zero; _colorFill.anchorMax = new Vector2(0f, 1f); _colorFill.offsetMin = _colorFill.offsetMax = Vector2.zero;
            _colorPct = UIFactory.Text(_folder, "ColorPct", "0%", FontLibrary.Display, 26f, Ink, new Vector2(1f, 1f), new Vector2(-50f, -138f), new Vector2(100f, 32f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));

            // exhibits
            var exH = UIFactory.Text(_folder, "ExHead", "EXHIBITS", FontLibrary.Display, 30f, Ink, new Vector2(0f, 1f), new Vector2(50f, -200f), new Vector2(400f, 40f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            exH.characterSpacing = 3f;
            for (int i = 0; i < 3; i++)
            {
                var card = UIFactory.Image(_folder, "Exhibit" + Exhibits[i], UISprites.Rounded(14), Color.white);
                var r = card.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(50f + i * 235f, -250f); r.sizeDelta = new Vector2(215f, 270f);
                r.localEulerAngles = new Vector3(0f, 0f, (i - 1) * -2.5f);
                _exCards[i] = card;
                var letter = UIFactory.Text(r, "Letter", Exhibits[i], FontLibrary.Display, 110f, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(200f, 130f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
                letter.alpha = 0.85f;
                var nm = UIFactory.Text(r, "Name", CampaignLink.ExhibitName(Exhibits[i]), FontLibrary.Heavy, 22f, Ink, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(200f, 56f), TextAlignmentOptions.Center, true, new Vector2(0.5f, 0f));
                UIFactory.AutoSize(nm, 14f, 22f);
                _exStamp[i] = UIFactory.Text(r, "Stamp", "MISSING", FontLibrary.Display, 30f, UIColors.Danger, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(210f, 44f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f));
                _exStamp[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, 8f);
            }
            // violations
            var vH = UIFactory.Text(_folder, "VioHead", "TEAMWORK VIOLATIONS", FontLibrary.Display, 28f, Ink, new Vector2(0f, 0f), new Vector2(50f, 150f), new Vector2(600f, 40f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            vH.characterSpacing = 2f;
            _violations = UIFactory.Text(_folder, "Violations", "0", FontLibrary.Display, 64f, UIColors.Danger, new Vector2(0f, 0f), new Vector2(50f, 66f), new Vector2(200f, 80f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            _hr = UIFactory.Text(_folder, "HR", "", FontLibrary.Body, 26f, Ink, new Vector2(0f, 0f), new Vector2(240f, 70f), new Vector2(520f, 76f), TextAlignmentOptions.Left, true, new Vector2(0f, 0f));
            _hr.fontStyle = FontStyles.Italic;

            // lost log
            var paper = UIFactory.Panel(_folder, "LostLog", new Vector2(700f, 600f), UIColors.Hex(0xFBF7EE), 6, true);
            paper.Rect.anchorMin = paper.Rect.anchorMax = new Vector2(1f, 0f); paper.Rect.pivot = new Vector2(1f, 0f);
            paper.Rect.anchoredPosition = new Vector2(-50f, 40f);
            paper.Rect.localEulerAngles = new Vector3(0f, 0f, 1.2f);
            var lh = UIFactory.Text(paper.Rect, "Head", "LOST & REPRINTED", FontLibrary.Display, 30f, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(640f, 40f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            lh.characterSpacing = 2f;
            _lost = UIFactory.Text(paper.Rect, "Log", "", FontLibrary.Body, 23f, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(640f, 500f), TextAlignmentOptions.TopLeft, true, new Vector2(0.5f, 1f));
            _lost.lineSpacing = 6f;
            UIFactory.AutoSize(_lost, 15f, 23f);

            BackButton = UIFactory.Button(Rect, "Back", "BACK", () => OnBack(), new Vector2(260f, 60f), UIButtonStyle.Primary, 28f);
            BackButton.Rt.anchorMin = BackButton.Rt.anchorMax = new Vector2(0.5f, 0f); BackButton.Rt.pivot = new Vector2(0.5f, 0f);
            BackButton.Rt.anchoredPosition = new Vector2(0f, 22f);
            DefaultSelection = BackButton.Selectable;
        }

        protected override void OnOpened()
        {
            var cur = CampaignLink.Current;
            if (cur == null && GameSession.Instance != null && GameSession.Instance.LevelIndex >= 0) cur = CampaignLink.ForCatalogIndex(GameSession.Instance.LevelIndex);
            if (cur == null) cur = CampaignLink.SavedChapter;
            bool campaign = CampaignLink.IsCampaign || (GameSession.Instance == null || GameSession.Instance.LevelIndex < 0);
            _now.text = cur == null ? "NOW: NO OPEN SHIFT" : "NOW: " + (cur.IsOvertime ? "OVERTIME (UNPAID)" : CampaignLink.ActHeader(cur.Act)) + "   ·   " + cur.MenuLabel + (campaign ? "" : "   ·   OFF THE RECORD");
            float meter = Mathf.Clamp01(CampaignLink.Meter);
            _colorFill.anchorMax = new Vector2(0f, 1f);
            UITween.Value(0.8f, e => { if (_colorFill != null) _colorFill.anchorMax = new Vector2(meter * e, 1f); }, Ease.OutCubic, 0.2f, null, this);
            _colorFill.GetComponent<Image>().color = ShiftScheduleScreen.WorldColorTint(meter);
            _colorPct.text = Mathf.RoundToInt(meter * 100f) + "%";

            ShownExhibits.Clear();
            for (int i = 0; i < 3; i++)
            {
                bool has = CampaignLink.HasExhibit(Exhibits[i]);
                if (has) ShownExhibits.Add(Exhibits[i]);
                _exCards[i].color = has ? Color.white : UIColors.Hex(0xD9C48E);
                _exStamp[i].text = has ? "ACQUIRED" : "MISSING";
                _exStamp[i].color = has ? UIColors.Success : UIFactory.WithAlpha(UIColors.Danger, 0.75f);
                UITween.Scale(_exStamp[i].rectTransform, 1f, 0.3f, Ease.OutBack, 0.25f + 0.12f * i, 2.2f);
            }
            UITween.Delay(0.3f, () => Sfx.Play(SfxId.Stamp, 0.8f), this);

            int v = CampaignLink.TeamworkViolations;
            if (v == 0 && GameSession.Instance != null && GameSession.Instance.Stats != null) v = GameSession.Instance.Stats.TeamworkViolations;
            _violations.text = v.ToString();
            _hr.text = "HR (ALSO ME) SAYS: \"" + HRLine(v) + "\"";

            var lost = CampaignLink.LostLog;
            if (lost.Count == 0) _lost.text = "<i>Nobody lost. Suspicious.</i>\n\n<size=80%>(Lost interns are reprinted between shifts. The reprint is fine.)</size>";
            else
            {
                var sb = new System.Text.StringBuilder();
                int start = Mathf.Max(0, lost.Count - 10);
                if (start > 0) sb.Append("<size=80%>...and ").Append(start).Append(" earlier incidents.</size>\n");
                for (int i = start; i < lost.Count; i++)
                {
                    var l = lost[i];
                    var ch = CampaignLink.Find(l.ChapterId);
                    sb.Append("<b>").Append((l.Name ?? "").ToUpperInvariant()).Append("</b> - ").Append(string.IsNullOrEmpty(l.Cause) ? "misplaced" : l.Cause.ToLowerInvariant());
                    if (ch != null) sb.Append(", ").Append(ch.ShiftLabel.ToLowerInvariant());
                    if (!string.IsNullOrEmpty(l.CloneName)) sb.Append(". <color=#6B5A48>Reprinted as ").Append(l.CloneName.ToUpperInvariant()).Append(".</color>");
                    sb.Append('\n');
                }
                _lost.text = sb.ToString();
            }
            UITween.Scale(_folder, 1f, 0.4f, Ease.OutBack, 0f, 0.85f);
            UITween.Rotate(_folder, 0f, 0.45f, Ease.OutBack, 0f, -4f);
        }
    }
}
