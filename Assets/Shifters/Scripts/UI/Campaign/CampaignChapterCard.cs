// =====================================================================================
//  SHIFTERS - CampaignChapterCard.cs   (ui_hud agent)
//  CAMPAIGN §15.1 chapter card (GameEvents.ChapterCard(act, title)): ≈ 3 s on the load
//  fade, zero extra time, skippable. "ACT II · SWING SHIFT" / "SHIFT 07 · THE FEDERAL
//  COLOR RESERVE" / one-line objective / a Max quip in a little TV frame. Own canvas at
//  sort order 101 (just above the ScreenFader's 100), so it rides on the fade's black.
//  Objective/quip: PendingObjective/PendingQuip (consumed), QuipProvider, or the
//  LevelCatalog entry whose title appears in the card title.
// =====================================================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class CampaignChapterCard : MonoBehaviour
    {
        public const float CardSeconds = 3f, MaxHoldSeconds = 8f;

        /// <summary>Set before raising GameEvents.ChapterCard to fill the objective line (consumed by the next card).</summary>
        public static string PendingObjective;
        /// <summary>Set before raising GameEvents.ChapterCard to fill Max's quip (consumed by the next card).</summary>
        public static string PendingQuip;
        /// <summary>Optional quip source (act, title) -> quip; used when PendingQuip is empty. Story/campaign may set it.</summary>
        public static Func<string, string, string> QuipProvider;
        /// <summary>Optional objective source (act, title) -> objective; used when PendingObjective is empty.</summary>
        public static Func<string, string, string> ObjectiveProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { PendingObjective = null; PendingQuip = null; QuipProvider = null; ObjectiveProvider = null; LoadingHold = true; }

        public bool Showing { get; private set; }
        public string ActText => _act != null ? _act.text : "";
        public string TitleText => _title != null ? _title.text : "";
        public string ObjectiveText => _objective != null ? _objective.text : "";
        public string QuipText => _quip != null ? _quip.text : "";

        CanvasGroup _group;
        Image _backdrop;
        TextMeshProUGUI _act, _title, _objective, _quip, _skipHint;
        /// <summary>Skip hint template (gp_ui): keyboard "PRESS ANY KEY TO SKIP" (unchanged), gamepad "PRESS A TO SKIP".</summary>
        public const string SkipHintTemplate = "PRESS {kb:ANY KEY}{pad:{CONFIRM}} TO SKIP";
        /// <summary>Skip hint as currently shown (tests).</summary>
        public string SkipHintText => _skipHint != null ? _skipHint.text : "";
        RectTransform _actRt, _tv, _quipRt, _lineL, _lineR;
        CampaignHudStyle.DrawnFace _face;
        RawImage _tvRaw;
        float _age, _closing = -1f;

        public static CampaignChapterCard Create(Transform canvas)
        {
            var rt = UIFactory.Rect("ChapterCard", canvas);
            UIFactory.Stretch(rt);
            var c = rt.gameObject.AddComponent<CampaignChapterCard>();
            c.Build(rt);
            return c;
        }

        void Build(RectTransform rt)
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            _backdrop = UIFactory.Fill(rt, "Backdrop", null, UIColors.Hex(0x07090C));
            var band = UIFactory.Image(rt, "Band", UISprites.GradientH, new Color(1f, 0.54f, 0.12f, 0.08f));
            var brt = band.rectTransform; brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.sizeDelta = new Vector2(0f, 300f); brt.anchoredPosition = new Vector2(0f, 40f);
            UIFactory.Fill(rt, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0f, 0.7f));

            var center = UIFactory.Rect("Center", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1700f, 400f));
            _actRt = UIFactory.Rect("Act", center, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1200f, 44f), new Vector2(0.5f, 1f));
            _act = UIFactory.Text(_actRt, "Text", "", FontLibrary.Label, 32f, UIColors.Accent, TextAlignmentOptions.Center);
            _act.characterSpacing = 14f;
            UIFactory.Stretch(_act.rectTransform);
            _lineL = UIFactory.Image(_actRt, "LineL", UISprites.Rounded(2), UIColors.Accent).rectTransform;
            _lineR = UIFactory.Image(_actRt, "LineR", UISprites.Rounded(2), UIColors.Accent).rectTransform;
            foreach (var l in new[] { _lineL, _lineR }) { l.anchorMin = l.anchorMax = new Vector2(0.5f, 0.5f); l.sizeDelta = new Vector2(160f, 4f); }

            _title = UIFactory.Text(center, "Title", "", FontLibrary.Display, 84f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(1680f, 120f), TextAlignmentOptions.Center);
            if (FontLibrary.DisplayShadow != null) _title.fontSharedMaterial = FontLibrary.DisplayShadow;
            _title.enableVertexGradient = true;
            _title.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Accent);
            UIFactory.AutoSize(_title, 44f, 84f);
            _objective = UIFactory.Text(center, "Objective", "", FontLibrary.Body, 32f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(1500f, 46f), TextAlignmentOptions.Center);
            UIFactory.AutoSize(_objective, 22f, 32f);

            // Max on a little TV with his quip
            var quipRow = UIFactory.Rect("QuipRow", rt, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1000f, 150f), new Vector2(0.5f, 0f));
            _tv = UIFactory.Rect("Tv", quipRow, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(196f, 148f), new Vector2(0f, 0.5f));
            UIFactory.Fill(_tv, "Body", UISprites.Rounded(18), CampaignHudStyle.TvBody);
            var scr = UIFactory.Rect("Screen", _tv); UIFactory.Stretch(scr, 12f, 12f, 12f, 12f);
            var mask = UIFactory.Fill(scr, "Mask", UISprites.Rounded(10), Color.black);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _face = CampaignHudStyle.DrawMax((RectTransform)mask.transform);
            var rawRt = UIFactory.Rect("Live", mask.transform); UIFactory.Stretch(rawRt);
            _tvRaw = rawRt.gameObject.AddComponent<RawImage>(); _tvRaw.raycastTarget = false; _tvRaw.enabled = false;
            var scan = UIFactory.Rect("Scan", mask.transform); UIFactory.Stretch(scan);
            var sraw = scan.gameObject.AddComponent<RawImage>(); sraw.raycastTarget = false; sraw.texture = CampaignHudStyle.Scanlines; sraw.uvRect = new Rect(0, 0, 1, 30);
            UIFactory.Fill(_tv, "Border", UISprites.RoundedOutline(18, 4), CampaignHudStyle.MaxGold);
            var bug = UIFactory.Text(_tv, "Bug", "ME-TV", FontLibrary.Heavy, 15f, CampaignHudStyle.MaxGold, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(100f, 18f), TextAlignmentOptions.Left);
            bug.characterSpacing = 3f;

            _quipRt = UIFactory.Rect("Quip", quipRow, new Vector2(0f, 0.5f), new Vector2(222f, 0f), new Vector2(760f, 110f), new Vector2(0f, 0.5f));
            var bubble = UIFactory.Fill(_quipRt, "Bubble", UISprites.Rounded(20), UIFactory.WithAlpha(UIColors.Panel, 0.95f));
            bubble.raycastTarget = false;
            UIFactory.Fill(_quipRt, "Border", UISprites.RoundedOutline(20, 3), UIFactory.WithAlpha(CampaignHudStyle.MaxGold, 0.8f));
            var tail = UIFactory.Image(_quipRt, "Tail", UISprites.Triangle, UIFactory.WithAlpha(CampaignHudStyle.MaxGold, 0.8f));
            var tr = tail.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f); tr.sizeDelta = new Vector2(26f, 20f);
            tr.anchoredPosition = new Vector2(-10f, 0f); tr.localRotation = Quaternion.Euler(0, 0, 90f);
            var who = UIFactory.Text(_quipRt, "Who", "MAX", FontLibrary.Heavy, 18f, CampaignHudStyle.MaxGold, new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(300f, 22f), TextAlignmentOptions.Left);
            who.characterSpacing = 5f;
            _quip = UIFactory.Text(_quipRt, "Text", "", FontLibrary.Body, 28f, UIColors.Text, TextAlignmentOptions.TopLeft, true);
            var qrt = _quip.rectTransform; qrt.anchorMin = Vector2.zero; qrt.anchorMax = Vector2.one; qrt.offsetMin = new Vector2(24f, 10f); qrt.offsetMax = new Vector2(-20f, -36f);
            UIFactory.AutoSize(_quip, 20f, 28f);

            _skipHint = UIFactory.Text(rt, "SkipHint", "PRESS ANY KEY TO SKIP", FontLibrary.Label, 18f, UIColors.TextMuted, new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(420f, 24f), TextAlignmentOptions.Right, false, new Vector2(1f, 0f));
            _skipHint.characterSpacing = 4f;
            DevicePromptText.Attach(_skipHint, SkipHintTemplate);   // pad: "PRESS A TO SKIP" (A, B and Start all skip)
        }

        public void Show(string act, string title, string objective = null, string quip = null)
        {
            if (string.IsNullOrEmpty(objective)) objective = PendingObjective;
            if (string.IsNullOrEmpty(quip)) quip = PendingQuip;
            PendingObjective = null; PendingQuip = null;
            if (string.IsNullOrEmpty(objective) && ObjectiveProvider != null) { try { objective = ObjectiveProvider(act, title); } catch (Exception e) { Debug.LogException(e); } }
            if (string.IsNullOrEmpty(quip) && QuipProvider != null) { try { quip = QuipProvider(act, title); } catch (Exception e) { Debug.LogException(e); } }
            string t = title ?? "";
            int nl = t.IndexOf('\n');
            if (nl >= 0) { if (string.IsNullOrEmpty(objective)) objective = t.Substring(nl + 1); t = t.Substring(0, nl); }
            if (string.IsNullOrEmpty(objective)) objective = CatalogObjective(t);

            _act.text = (act ?? "").ToUpperInvariant();
            _title.text = t.ToUpperInvariant();
            _objective.text = objective ?? "";
            _quip.text = quip ?? "";
            float aw = UIFactory.Preferred(_act, _act.text).x * 0.5f + 30f;
            _lineL.anchoredPosition = new Vector2(-aw - 80f, 0f); _lineR.anchoredPosition = new Vector2(aw + 80f, 0f);
            bool hasQuip = !string.IsNullOrEmpty(_quip.text);
            _tv.parent.gameObject.SetActive(hasQuip);
            var tex = CampaignHudCompat.VillainTexture();
            _tvRaw.texture = tex; _tvRaw.enabled = tex != null;
            CampaignHudStyle.SetFace(_face, Expression.Smug);

            Showing = true; _age = 0f; _closing = -1f;
            UITween.Kill(_group);
            // opaque on top of the fade; slightly see-through over live gameplay
            _backdrop.color = UIColors.Hex(0x07090C, ScreenFader.Alpha > 0.5f ? 1f : 0.9f);
            UITween.Fade(_group, 1f, ScreenFader.Alpha > 0.5f ? 0.12f : 0.3f);
            UITween.Move(_actRt, new Vector2(0f, -10f), 0.45f, Ease.OutCubic, 0.05f, new Vector2(-260f, -10f));
            UITween.Scale(_title.rectTransform, 1f, 0.5f, Ease.OutBack, 0.12f, 1.25f);
            UITween.Color(_objective, UIColors.Text, 0.4f, Ease.OutQuad, 0.45f, UIFactory.WithAlpha(UIColors.Text, 0f));
            if (hasQuip) UITween.Scale((RectTransform)_tv.parent, 1f, 0.35f, Ease.OutBack, 0.7f, 0.01f);
            Sfx.Play(SfxId.ActCard, 0.8f);
        }

        static string CatalogObjective(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                var info = LevelCatalog.Get(i);
                if (info == null || string.IsNullOrEmpty(info.Title)) continue;
                if (title.IndexOf(info.Title, StringComparison.OrdinalIgnoreCase) >= 0) return info.DefaultObjective;
            }
            return null;
        }

        public void Skip() { if (Showing) Close(); }

        public void Close(bool instant = false)
        {
            if (!Showing) return;
            Showing = false;
            UITween.Kill(_group);
            if (instant) { _group.alpha = 0f; return; }
            UITween.Fade(_group, 0f, 0.35f);
        }

        void Update()
        {
            if (!Showing) return;
            _age += UITime.Delta;
            if (ScreenFader.Alpha > 0.01f || ScreenFader.IsFading) { var bc = _backdrop.color; if (bc.a < 1f) { bc.a = 1f; _backdrop.color = bc; } }
            CampaignHudStyle.Talk(_face, _age > 0.8f && _age < 2.6f ? Mathf.Abs(Mathf.Sin(_age * 16f)) : 0f);
            if (_age > 0.35f && (CampaignHudCompat.AnyAdvancePressed() || CampaignHudCompat.SkipHeld01() > 0.25f)) { Close(); return; }
            // hold while the loader still has the screen black; never longer than MaxHoldSeconds
            bool loading = ScreenFader.Alpha > 0.98f && ScreenFader.IsFading == false && _age < MaxHoldSeconds && LoadingHold;
            if (_age >= CardSeconds && !loading) Close();
        }

        /// <summary>When true (default) the card stays up past CardSeconds while the fader is fully black
        /// (i.e. the next scene is still loading), up to MaxHoldSeconds.</summary>
        public static bool LoadingHold = true;
    }
}
