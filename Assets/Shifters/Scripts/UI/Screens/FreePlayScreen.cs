// =====================================================================================
//  SHIFTERS - FreePlayScreen.cs   (UI agent)
//  FREE PLAY (§15.1): the Level Select extended to a grid of every BUILT level (main chapters in story order,
//  then OVERTIME), no story, full color. Card = shift tag + number, title, theme, best time, completed check.
//  A card: CampaignDirector.StartFreePlay(catalogIndex), else GameSession.LoadLevel (classic).
//  Grid navigation is explicit (arrows / stick in 2D), BACK below the grid.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class FreePlayScreen : UIScreen
    {
        public readonly List<UIButton> Cards = new List<UIButton>();
        /// <summary>Catalog index per card (same order as Cards).</summary>
        public readonly List<int> CardLevels = new List<int>();
        readonly List<GameObject> _done = new List<GameObject>();
        readonly List<TextMeshProUGUI> _best = new List<TextMeshProUGUI>();
        readonly List<Vector2> _home = new List<Vector2>();
        public UIButton BackButton { get; private set; }
        TextMeshProUGUI _empty;
        public const int Columns = 6;
        const float CardW = 274f, CardH = 186f, GapX = 16f, GapY = 14f;

        protected override void Build()
        {
            Backdrop(0.84f, true);
            Title("FREE PLAY", new Vector2(0.5f, 1f), new Vector2(0f, -30f), 70f).rectTransform.pivot = new Vector2(0.5f, 1f);
            var sub = UIFactory.Text(Rect, "Sub", "NO STORY. FULL COLOR. NO SUPERVISION. (MAX IS FURIOUS.)", FontLibrary.Label, 26f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1500f, 32f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            sub.characterSpacing = 6f;

            // order: main path (story order) first, then OVERTIME, then any other built catalog entry
            var order = new List<int>();
            foreach (var c in CampaignLink.Chapters) if (c.CatalogIndex >= 0 && !order.Contains(c.CatalogIndex)) order.Add(c.CatalogIndex);
            foreach (var c in CampaignLink.Overtime) if (c.CatalogIndex >= 0 && !order.Contains(c.CatalogIndex)) order.Add(c.CatalogIndex);
            for (int i = 0; i < LevelCatalog.Count; i++) if (!order.Contains(i)) order.Add(i);

            var built = new List<int>();
            foreach (int idx in order) if (LevelCatalog.HasScene(idx)) built.Add(idx);
            int rows = Mathf.Max(1, (built.Count + Columns - 1) / Columns);
            float gridH = rows * CardH + (rows - 1) * GapY;
            float top = Mathf.Min(gridH * 0.5f, 380f) - 20f;
            for (int k = 0; k < built.Count; k++)
            {
                int idx = built[k];
                int col = k % Columns, row = k / Columns;
                int inRow = Mathf.Min(Columns, built.Count - row * Columns);
                float x = (col - (inRow - 1) * 0.5f) * (CardW + GapX);
                float y = top - CardH * 0.5f - row * (CardH + GapY);
                var b = UIFactory.Button(Rect, "Level" + idx, "", () => Play(idx), new Vector2(CardW, CardH), UIButtonStyle.Secondary);
                b.HoverScale = 1.07f;
                b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                b.Rt.anchoredPosition = new Vector2(x, y);
                _home.Add(b.Rt.anchoredPosition);
                BuildCard(b, idx);
                Cards.Add(b);
                CardLevels.Add(idx);
            }
            _empty = UIFactory.Text(Rect, "Empty", built.Count == 0 ? "NO LEVELS BUILT. HR IS LOOKING INTO IT." : "", FontLibrary.Display, 40f, UIColors.TextMuted, TextAlignmentOptions.Center);
            BackButton = UIFactory.Button(Rect, "Back", "BACK", () => OnBack(), new Vector2(260f, 60f), UIButtonStyle.Ghost, 28f);
            BackButton.Rt.anchorMin = BackButton.Rt.anchorMax = new Vector2(0.5f, 0f); BackButton.Rt.pivot = new Vector2(0.5f, 0f);
            BackButton.Rt.anchoredPosition = new Vector2(0f, 34f);
            LinkGrid();
            DefaultSelection = Cards.Count > 0 ? Cards[0].Selectable : BackButton.Selectable;
        }

        void LinkGrid()
        {
            int n = Cards.Count;
            for (int k = 0; k < n; k++)
            {
                int col = k % Columns, row = k / Columns;
                Selectable At(int r, int c)
                {
                    if (r < 0) return null;
                    int i = r * Columns + c;
                    if (i >= n) { i = n - 1; if (i / Columns != r) return null; }
                    return Cards[i].Selectable;
                }
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnLeft = col > 0 ? Cards[k - 1].Selectable : (k + Columns - 1 < n ? Cards[k + Columns - 1].Selectable : Cards[n - 1].Selectable);
                nav.selectOnRight = col < Columns - 1 && k + 1 < n ? Cards[k + 1].Selectable : Cards[row * Columns].Selectable;
                nav.selectOnUp = row > 0 ? At(row - 1, col) : BackButton.Selectable;
                nav.selectOnDown = (row + 1) * Columns < n ? At(row + 1, col) : BackButton.Selectable;
                Cards[k].Selectable.navigation = nav;
            }
            BackButton.Selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = n > 0 ? Cards[((n - 1) / Columns) * Columns].Selectable : null, selectOnDown = n > 0 ? Cards[0].Selectable : null };
        }

        void BuildCard(UIButton b, int idx)
        {
            var info = LevelCatalog.Get(idx);
            var ch = CampaignLink.ForCatalogIndex(idx);
            var color = LevelSelectScreen.ThemeColor(info != null ? info.Theme : LevelTheme.Training);
            var rt = b.Rt;
            var band = UIFactory.Image(rt, "Band", UISprites.Rounded(12), color);
            var brt = band.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(0f, -62f); brt.offsetMax = Vector2.zero;
            var st = UIFactory.Image(brt, "Stripes", UISprites.Stripes, new Color(0f, 0f, 0f, 0.12f));
            st.type = Image.Type.Tiled;
            UIFactory.Stretch(st.rectTransform);
            string tag = ch == null ? "LEVEL" : ch.IsOvertime ? "OVERTIME" : "SHIFT";
            string num = ch == null ? (idx + 1).ToString("00") : ch.IsOvertime ? ch.Id : ch.Number.ToString("00");
            var tagT = UIFactory.Text(brt, "Tag", tag, FontLibrary.Label, 20f, UIColors.Hex(0x12161C), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(140f, 30f), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            tagT.characterSpacing = 8f;
            UIFactory.Text(brt, "Num", num, FontLibrary.Display, 44f, UIColors.Hex(0x12161C), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(130f, 56f), TextAlignmentOptions.Right, false, new Vector2(1f, 0.5f));
            string titleText = ch != null ? ch.Title : info != null ? info.Title : "LEVEL " + (idx + 1);
            var title = UIFactory.Text(rt, "Title", (titleText ?? "").ToUpperInvariant(), FontLibrary.Display, 24f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(CardW - 24f, 60f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(title, 15f, 24f);
            var theme = UIFactory.Text(rt, "Theme", info != null ? info.Theme.ToString().ToUpperInvariant() + (ch != null && ch.IsBoss ? "  ·  BOSS" : "") : "", FontLibrary.Label, 17f, color, new Vector2(0f, 0f), new Vector2(14f, 12f), new Vector2(160f, 22f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            theme.characterSpacing = 3f;
            var best = UIFactory.Text(rt, "Best", "", FontLibrary.Heavy, 19f, UIColors.TextMuted, new Vector2(1f, 0f), new Vector2(-14f, 12f), new Vector2(110f, 22f), TextAlignmentOptions.Right, false, new Vector2(1f, 0f));
            _best.Add(best);
            var done = UIFactory.Image(rt, "Done", UISprites.Circle, UIColors.Success);
            var drt = done.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(1f, 1f); drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(-8f, -8f); drt.sizeDelta = new Vector2(34f, 34f);
            UIFactory.Icon(drt, UIIcon.Check, UIColors.Hex(0x12161C), 22f);
            _done.Add(done.gameObject);
            b.Label.transform.SetAsLastSibling();
        }

        void Play(int index) => SessionActions.FreePlay(index);

        protected override void OnOpened()
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                int idx = CardLevels[i];
                _done[i].SetActive(SessionActions.IsLevelCompleted(idx));
                float best = SessionPrefs.BestTime(idx);
                _best[i].text = best > 0f ? UIFactory.FormatTime(best) : "";
                UITween.Move(Cards[i].Rt, _home[i], 0.4f, Ease.OutBack, 0.025f * i, _home[i] + new Vector2(0f, -120f));
            }
        }
    }
}
