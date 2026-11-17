// =====================================================================================
//  SHIFTERS - ShiftScheduleScreen.cs   (UI agent)
//  SHIFT SCHEDULE (chapter select, §15.1): a punch-card timesheet. Rows grouped by act like days on a
//  timecard (act header + one-line STORY SO FAR), columns SHIFT # / TITLE / PUNCHED / BEST TIME / EXHIBIT /
//  WORLD COLOR, then the OVERTIME side shifts (pay column "UNPAID"). Everything is unlocked; chapters
//  whose scene is not built yet show "NOT BUILT" and are skipped by keyboard navigation.
//  A row: with a team -> "CONTINUE THE CAMPAIGN FROM HERE?" -> CampaignDirector.StartAt(id); without
//  one -> YOUR NEW HIRES (Chapter mode) -> StartAt. Without the campaign module rows load the level.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shifters
{
    public class ShiftScheduleScreen : UIScreen
    {
        public sealed class Row
        {
            public ChapterView Chapter;
            public UIButton Button;
            public TextMeshProUGUI Punched, Best, Title;
            public Image Exhibit, ColorFill;
            public TextMeshProUGUI ExhibitText, ColorText;
            public float Y;   // top of the row inside the content (negative)
        }

        public readonly List<Row> Rows = new List<Row>();
        public UIButton BackButton { get; private set; }
        RectTransform _viewport, _content, _cursor;
        float _contentH, _scroll, _scrollTarget;
        GameObject _lastSel;

        const float SheetW = 1500f, RowH = 54f, RowGap = 6f, ActH = 78f, ViewH = 650f;
        static readonly float[] ColX = { 22f, 190f, 800f, 990f, 1160f, 1290f };
        static readonly string[] ColNames = { "SHIFT #", "TITLE", "PUNCHED", "BEST TIME", "EXHIBIT", "WORLD COLOR" };
        static readonly Color Paper = UIColors.Hex(0xF3EBDD);
        static readonly Color Ink = UIColors.Hex(0x2B2118);

        public Row Find(string id)
        {
            foreach (var r in Rows) if (r.Chapter.Id == id) return r;
            return null;
        }

        protected override void Build()
        {
            Backdrop(0.86f, true);
            Title("SHIFT SCHEDULE", new Vector2(0.5f, 1f), new Vector2(0f, -30f), 70f).rectTransform.pivot = new Vector2(0.5f, 1f);
            var sub = UIFactory.Text(Rect, "Sub", "TIMESHEET  ·  EVERY SHIFT UNLOCKED  ·  MAX: \"I DIDN'T THINK ANYONE WOULD GET THIS FAR.\"", FontLibrary.Label, 24f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1700f, 30f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            sub.characterSpacing = 5f;

            var sheet = UIFactory.Panel(Rect, "Sheet", new Vector2(SheetW + 40f, ViewH + 100f), UIColors.Hex(0x161B22, 0.97f), 20, true, UIFactory.WithAlpha(UIColors.AccentYellow, 0.6f));
            sheet.Rect.anchorMin = sheet.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            sheet.Rect.anchoredPosition = new Vector2(0f, -40f);
            // header band (time-card yellow)
            var band = UIFactory.Image(sheet.Rect, "HeaderBand", UISprites.Rounded(12), UIColors.AccentYellow);
            var brt = band.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -16f); brt.sizeDelta = new Vector2(SheetW, 52f);
            for (int i = 0; i < ColNames.Length; i++)
            {
                var h = UIFactory.Text(brt, "Col" + i, ColNames[i], FontLibrary.Heavy, 22f, Ink, new Vector2(0f, 0.5f), new Vector2(ColX[i], 0f), new Vector2(220f, 40f), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
                h.characterSpacing = 4f;
            }
            _viewport = UIFactory.Rect("Viewport", sheet.Rect, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(SheetW, ViewH), new Vector2(0.5f, 1f));
            _viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 24);
            _content = UIFactory.Rect("Content", _viewport, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(SheetW, 100f), new Vector2(0.5f, 1f));

            float y = -6f;
            string act = null;
            var sels = new List<Selectable>();
            foreach (var c in CampaignLink.Chapters)
            {
                if (c.Act != act) { act = c.Act; y = ActHeader(act, y); }
                AddRow(c, ref y, sels);
            }
            y = ActHeader("OVERTIME", y - 8f);
            foreach (var c in CampaignLink.Overtime) AddRow(c, ref y, sels);
            _contentH = -y + 10f;
            _content.sizeDelta = new Vector2(SheetW, _contentH);

            _cursor = UIFactory.Image(_content, "Cursor", UISprites.Triangle, UIColors.AccentYellow).rectTransform;
            _cursor.anchorMin = _cursor.anchorMax = new Vector2(0f, 1f); _cursor.pivot = new Vector2(0.5f, 0.5f);
            _cursor.sizeDelta = new Vector2(22f, 22f); _cursor.localEulerAngles = new Vector3(0f, 0f, -90f);

            BackButton = UIFactory.Button(Rect, "Back", "BACK", () => OnBack(), new Vector2(260f, 60f), UIButtonStyle.Ghost, 28f);
            BackButton.Rt.anchorMin = BackButton.Rt.anchorMax = new Vector2(0.5f, 0f); BackButton.Rt.pivot = new Vector2(0.5f, 0f);
            BackButton.Rt.anchoredPosition = new Vector2(0f, 22f);
            sels.Add(BackButton.Selectable);
            UIFactory.LinkVertical(sels, false);
            DefaultSelection = sels[0];
        }

        float ActHeader(string act, float y)
        {
            y -= 8f;
            var t = UIFactory.Text(_content, "Act_" + act, CampaignLink.ActHeader(act), FontLibrary.Display, 28f, UIColors.AccentYellow, new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(700f, 36f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            t.characterSpacing = 2f;
            var so = UIFactory.Text(_content, "SoFar_" + act, (act == "OVERTIME" ? "" : "STORY SO FAR: ") + CampaignLink.StorySoFar(act), FontLibrary.Body, 22f, UIColors.TextMuted, new Vector2(0f, 1f), new Vector2(560f, y - 6f), new Vector2(SheetW - 580f, 30f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            so.fontStyle = FontStyles.Italic;
            var rule = UIFactory.Image(_content, "Rule_" + act, null, UIFactory.WithAlpha(UIColors.AccentYellow, 0.35f));
            var rr = rule.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0f, 1f); rr.pivot = new Vector2(0f, 1f);
            rr.anchoredPosition = new Vector2(16f, y - 40f); rr.sizeDelta = new Vector2(SheetW - 32f, 2f);
            return y - (ActH - 28f);
        }

        void AddRow(ChapterView c, ref float y, List<Selectable> sels)
        {
            var b = UIFactory.Button(_content, "Row_" + c.Id, "", () => OnRow(c), new Vector2(SheetW - 40f, RowH), UIButtonStyle.Secondary, 24f);
            b.HoverScale = 1.01f;
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0f, 1f); b.Rt.pivot = new Vector2(0f, 1f);
            b.Rt.anchoredPosition = new Vector2(20f, y);
            var r = new Row { Chapter = c, Button = b, Y = y };
            bool built = c.Playable;
            var rt = b.Rt;
            Col(rt, 0, c.IsOvertime ? c.Id : c.Number.ToString("00"), FontLibrary.Display, 28f, c.IsBoss ? UIColors.Danger : UIColors.AccentYellow);
            r.Title = Col(rt, 1, (c.Title ?? "").ToUpperInvariant() + (c.IsBoss ? "  <size=70%><color=#FF4D5E>BOSS</color></size>" : c.Kind == "Finale" ? "  <size=70%><color=#FFC93C>FINALE</color></size>" : ""), FontLibrary.Heavy, 26f, built ? UIColors.Text : UIColors.TextMuted);
            UIFactory.AutoSize(r.Title, 16f, 26f);
            r.Title.rectTransform.sizeDelta = new Vector2(590f, RowH);
            r.Punched = Col(rt, 2, "", FontLibrary.Display, 22f, UIColors.Danger);
            r.Best = Col(rt, 3, "--:--", FontLibrary.Heavy, 24f, UIColors.TextMuted);
            // exhibit sprite (rounded tag + letter; fonts lack ☐ ★)
            if (!string.IsNullOrEmpty(c.ExhibitId))
            {
                r.Exhibit = UIFactory.Image(rt, "Exhibit", UISprites.Rounded(8), UIColors.AccentYellow);
                var ert = r.Exhibit.rectTransform;
                ert.anchorMin = ert.anchorMax = new Vector2(0f, 0.5f); ert.pivot = new Vector2(0f, 0.5f);
                ert.anchoredPosition = new Vector2(ColX[4] + 10f, 0f); ert.sizeDelta = new Vector2(40f, 40f);
                r.ExhibitText = UIFactory.Text(ert, "L", c.ExhibitId, FontLibrary.Display, 26f, Ink, TextAlignmentOptions.Center);
                UIFactory.Stretch(r.ExhibitText.rectTransform);
            }
            if (c.IsOvertime)
            {
                r.ColorText = Col(rt, 5, "UNPAID", FontLibrary.Display, 24f, UIColors.Danger);
                r.ColorText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 4f);
            }
            else
            {
                var track = UIFactory.Image(rt, "ColorTrack", UISprites.Rounded(5), new Color(0f, 0f, 0f, 0.45f));
                var trt = track.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f); trt.pivot = new Vector2(0f, 0.5f);
                trt.anchoredPosition = new Vector2(ColX[5], 0f); trt.sizeDelta = new Vector2(110f, 12f);
                r.ColorFill = UIFactory.Image(trt, "Fill", UISprites.Rounded(5), WorldColorTint(c.MeterEnd));
                var frt = r.ColorFill.rectTransform;
                frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(Mathf.Clamp01(c.MeterEnd), 1f); frt.offsetMin = frt.offsetMax = Vector2.zero;
                r.ColorText = Col(rt, 5, Mathf.RoundToInt(Mathf.Clamp01(c.MeterEnd) * 100f) + "%", FontLibrary.Heavy, 20f, UIColors.TextMuted);
                r.ColorText.rectTransform.anchoredPosition = new Vector2(ColX[5] + 118f, 0f);
            }
            if (!built) { b.Interactable = false; r.Punched.text = "<size=80%><color=#9AA4B2>NOT BUILT</color></size>"; }
            else sels.Add(b.Selectable);
            b.Label.transform.SetAsLastSibling();
            Rows.Add(r);
            y -= RowH + RowGap;
        }

        TextMeshProUGUI Col(RectTransform row, int col, string text, TMP_FontAsset font, float size, Color color)
        {
            var t = UIFactory.Text(row, "C" + col, text, font, size, color, new Vector2(0f, 0.5f), new Vector2(ColX[col], 0f), new Vector2(200f, RowH), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            return t;
        }

        /// <summary>Grey -> warm color with the meter (the world color after this shift).</summary>
        public static Color WorldColorTint(float m)
        {
            m = Mathf.Clamp01(m);
            var hue = Color.HSVToRGB(Mathf.Lerp(0.02f, 0.48f, m), 0.85f, 1f);
            return Color.Lerp(UIColors.Hex(0x7A7F87), hue, Mathf.Clamp01(m * 1.4f));
        }

        void OnRow(ChapterView c)
        {
            var m = Manager;
            if (m == null || c == null) return;
            if (!c.Playable) { Sfx.Play(SfxId.UIError); GameEvents.RaiseToast("THAT SHIFT IS STILL UNDER CONSTRUCTION.", UIColors.Danger); return; }
            if (!CampaignLink.Present) { SessionActions.CampaignAt(c); return; }   // classic: just load the level
            if (SessionActions.HasRun || SessionActions.CanContinue)
            {
                string body = c.MenuLabel + "\n<size=75%>" + (c.IsOvertime ? "Overtime is UNPAID. Your current interns clock in anyway." : "Your current interns clock in. The campaign continues from this shift.") + "</size>";
                m.AskConfirm(c.IsOvertime ? "WORK OVERTIME?" : "CONTINUE THE CAMPAIGN FROM HERE?", body, "CLOCK IN", "NOT TODAY", () => SessionActions.CampaignAt(c));
                return;
            }
            SessionActions.NewRun();
            m.TeamReveal.Configure(TeamRevealScreen.Mode.Chapter, c);
            m.TeamReveal.MarkFresh();
            m.Push(m.TeamReveal);
        }

        protected override void OnOpened()
        {
            var save = CampaignLink.Save;
            Selectable first = null, saved = null;
            string savedId = save != null ? save.chapter : null;
            foreach (var r in Rows)
            {
                var c = r.Chapter;
                if (c.Playable)
                {
                    bool punched = CampaignLink.IsPunched(c.Id) || (!CampaignLink.Present && c.CatalogIndex >= 0 && SessionActions.IsLevelCompleted(c.CatalogIndex));
                    r.Punched.text = punched ? "PUNCHED" : c.Id == savedId ? "<color=#FFC93C>ON SHIFT</color>" : "";
                    r.Punched.rectTransform.localEulerAngles = new Vector3(0f, 0f, punched ? -6f : 0f);
                    if (punched) UITween.Scale(r.Punched.rectTransform, 1f, 0.35f, Ease.OutBack, 0.2f, 1.8f);
                    if (first == null) first = r.Button.Selectable;
                    if (c.Id == savedId) saved = r.Button.Selectable;
                }
                float best = c.CatalogIndex >= 0 ? SessionPrefs.BestTime(c.CatalogIndex) : -1f;
                r.Best.text = best > 0f ? UIFactory.FormatTime(best) : "--:--";
                r.Best.color = best > 0f ? UIColors.Text : UIColors.TextMuted;
                if (r.Exhibit != null)
                {
                    bool has = CampaignLink.HasExhibit(c.ExhibitId);
                    r.Exhibit.color = has ? UIColors.AccentYellow : UIFactory.WithAlpha(UIColors.TextMuted, 0.35f);
                }
            }
            DefaultSelection = saved ?? first ?? BackButton.Selectable;
            _scroll = _scrollTarget = 0f;
            _content.anchoredPosition = Vector2.zero;
            _lastSel = null;
            _scroll = -120f;   // slides up into place
        }

        /// <summary>Content scroll offset (tests).</summary>
        public float Scroll => _scroll;
        public float ContentHeight => _contentH;

        void Update()
        {
            if (!IsOpen || _content == null) return;
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel != _lastSel)
            {
                _lastSel = sel;
                foreach (var r in Rows)
                {
                    if (r.Button.gameObject != sel) continue;
                    // keep the selected row inside the viewport (with one row of context)
                    float top = -r.Y - RowH - 10f, bottom = -r.Y + RowH * 2f + 10f;
                    if (top < _scrollTarget) _scrollTarget = top;
                    if (bottom > _scrollTarget + ViewH) _scrollTarget = bottom - ViewH;
                    _cursor.anchoredPosition = new Vector2(8f, r.Y - RowH * 0.5f);
                    _cursor.gameObject.SetActive(true);
                    UITween.Punch(_cursor, 0.3f, 0.2f);
                    break;
                }
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) { float wheel = mouse.scroll.ReadValue().y; if (Mathf.Abs(wheel) > 0.01f) _scrollTarget -= Mathf.Sign(wheel) * 90f; }
            _scrollTarget = Mathf.Clamp(_scrollTarget, 0f, Mathf.Max(0f, _contentH - ViewH));
            if (Mathf.Approximately(_scroll, _scrollTarget)) return;
            _scroll = Mathf.Lerp(_scroll, _scrollTarget, 1f - Mathf.Exp(-14f * UITime.Delta));
            if (Mathf.Abs(_scroll - _scrollTarget) < 0.5f) _scroll = _scrollTarget;
            _content.anchoredPosition = new Vector2(0f, _scroll);
        }
    }
}
