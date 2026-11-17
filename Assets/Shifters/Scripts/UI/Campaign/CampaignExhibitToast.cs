// =====================================================================================
//  SHIFTERS - CampaignExhibitToast.cs   (ui_hud agent)
//  "EXHIBIT B ACQUIRED: THE BOOKS" evidence-folder toast (GameEvents.ExhibitAcquired).
//  Exhibit art is drawn from UI shapes (the fonts lack ☐ ★) and exposed as
//  BuildIcon(...) so ui's CASE FILE panel can reuse the same sprites.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class CampaignExhibitToast : MonoBehaviour
    {
        public const float W = 680f, H = 156f, Seconds = 3.4f;
        public static readonly Vector2 HomePos = new Vector2(0f, -180f);

        public bool Shown { get; private set; }
        public string CurrentId { get; private set; }
        public string HeadlineText => _headline != null ? _headline.text : "";
        public RectTransform Rt { get; private set; }

        CanvasGroup _group;
        TextMeshProUGUI _headline, _title;
        RectTransform _iconHolder, _stamp;
        float _hideAt = -1f;

        /// <summary>Exhibit display name ("A" -> "THE GREY MEMO", "B" -> "THE BOOKS", "C" -> "THE BEDSHEET"; else the id).</summary>
        public static string ExhibitName(string id)
        {
            switch (Key(id))
            {
                case "A": return "THE GREY MEMO";
                case "B": return "THE BOOKS";
                case "C": return "THE BEDSHEET";
                default: return string.IsNullOrEmpty(id) ? "EVIDENCE" : id.ToUpperInvariant();
            }
        }

        /// <summary>Normalizes "A", "a", "EXHIBIT_A", "exhibit.b" ... to "A"/"B"/"C" (else upper id).</summary>
        public static string Key(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string u = id.Trim().ToUpperInvariant();
            if (u.Length == 1) return u;
            char last = u[u.Length - 1];
            if ((last == 'A' || last == 'B' || last == 'C') && u.StartsWith("EXHIBIT")) return last.ToString();
            return u;
        }

        public static CampaignExhibitToast Create(Transform parent)
        {
            var rt = UIFactory.Rect("ExhibitToast", parent, new Vector2(0.5f, 0.5f), HomePos, new Vector2(W, H), new Vector2(0.5f, 0.5f));
            var t = rt.gameObject.AddComponent<CampaignExhibitToast>();
            t.Rt = rt;
            t.Build();
            return t;
        }

        void Build()
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            // folder tab + manila folder
            var tab = UIFactory.Image(Rt, "Tab", UISprites.Rounded(10), UIColors.Hex(0xD9B978));
            var trt = tab.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 0f);
            trt.anchoredPosition = new Vector2(34f, -14f); trt.sizeDelta = new Vector2(190f, 42f);
            var tabText = UIFactory.Text(tab.transform, "TabText", "EVIDENCE", FontLibrary.Label, 18f, UIColors.Hex(0x5A4420), TextAlignmentOptions.Center);
            tabText.characterSpacing = 8f;
            UIFactory.Stretch(tabText.rectTransform, 0f, 0f, 2f, 14f);
            var panel = UIFactory.Panel(Rt, "Folder", new Vector2(W, H), CampaignHudStyle.Manila, 14, true, UIColors.Hex(0xB8975A));
            UIFactory.Stretch(panel.Rect);
            _iconHolder = UIFactory.Rect("Icon", Rt, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(118f, 118f), new Vector2(0f, 0.5f));
            _headline = UIFactory.Text(Rt, "Headline", "", FontLibrary.Heavy, 28f, CampaignHudStyle.Ink, new Vector2(0f, 1f), new Vector2(170f, -26f), new Vector2(480f, 34f), TextAlignmentOptions.Left);
            _headline.characterSpacing = 4f;
            UIFactory.AutoSize(_headline, 18f, 28f);
            _title = UIFactory.Text(Rt, "Title", "", FontLibrary.Display, 44f, UIColors.Hex(0xC22D3C), new Vector2(0f, 1f), new Vector2(170f, -62f), new Vector2(480f, 56f), TextAlignmentOptions.Left);
            UIFactory.AutoSize(_title, 26f, 44f);
            var memo = UIFactory.Text(Rt, "Memo", "ADDED TO THE CASE FILE", FontLibrary.Label, 16f, UIColors.Hex(0x6B5530), new Vector2(0f, 0f), new Vector2(172f, 14f), new Vector2(400f, 22f), TextAlignmentOptions.Left);
            memo.characterSpacing = 5f;
            TextMeshProUGUI st;
            _stamp = CampaignHudStyle.Stamp(Rt, "Stamp", "FILED", CampaignHudStyle.StampInk, 30f, 12f, out st);
            _stamp.anchorMin = _stamp.anchorMax = new Vector2(1f, 0.5f); _stamp.anchoredPosition = new Vector2(-70f, -24f);
        }

        public void Show(string id)
        {
            CurrentId = id;
            string key = Key(id);
            _headline.text = key.Length == 1 ? "EXHIBIT " + key + " ACQUIRED" : "EVIDENCE ACQUIRED";
            _title.text = ExhibitName(id);
            for (int i = _iconHolder.childCount - 1; i >= 0; i--) Destroy(_iconHolder.GetChild(i).gameObject);
            BuildIcon(_iconHolder, id, 118f);
            Shown = true;
            _hideAt = UITime.Now + Seconds;
            UITween.Kill(_group); UITween.Kill(Rt);
            _group.alpha = 0f;
            UITween.Fade(_group, 1f, 0.2f);
            UITween.Move(Rt, HomePos, 0.45f, Ease.OutBack, 0f, HomePos + new Vector2(0f, -140f));
            _stamp.gameObject.SetActive(false);
            UITween.Delay(0.55f, () =>
            {
                if (_stamp == null || !Shown) return;
                _stamp.gameObject.SetActive(true);
                UITween.Scale(_stamp, 1f, 0.22f, Ease.OutBack, 0f, 2.6f);
                UITween.Shake(Rt, 8f, 0.2f);
                Sfx.Play(SfxId.Stamp, 0.8f);
            }, this);
            Sfx.Play(SfxId.Cymbal, 0.35f, 1.2f);
        }

        public void Hide(bool instant = false)
        {
            Shown = false; _hideAt = -1f;
            UITween.Kill(_group);
            if (instant) _group.alpha = 0f; else UITween.Fade(_group, 0f, 0.3f);
        }

        void Update()
        {
            if (Shown && _hideAt > 0f && UITime.Now >= _hideAt) Hide();
        }

        /// <summary>Builds the exhibit's picture (memo / ledger / bedsheet ghost / folder) into 'parent' at 'size' (square). Returns its root.</summary>
        public static RectTransform BuildIcon(Transform parent, string id, float size)
        {
            var root = UIFactory.Rect("Exhibit_" + Key(id), parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(size, size);
            root.anchoredPosition = Vector2.zero;
            var disc = UIFactory.Fill(root, "Disc", UISprites.Circle, UIColors.Hex(0x2A2016, 0.18f));
            disc.raycastTarget = false;
            switch (Key(id))
            {
                case "A": // THE GREY MEMO: a sheet with lines and a gold signature
                {
                    var sheet = UIFactory.Image(root, "Sheet", UISprites.Rounded(6), UIColors.Hex(0xF4F4F0));
                    CampaignHudStyle.Place(sheet.rectTransform, 0f, 0f, 0.6f, 0.78f);
                    sheet.rectTransform.localRotation = Quaternion.Euler(0, 0, -6f);
                    for (int i = 0; i < 5; i++)
                    {
                        var ln = UIFactory.Image(sheet.transform, "Line" + i, UISprites.Rounded(2), UIColors.Hex(0x9AA0A8));
                        CampaignHudStyle.Place(ln.rectTransform, -0.05f + (i == 4 ? -0.1f : 0f), 0.3f - i * 0.12f, i == 4 ? 0.5f : 0.75f, 0.04f);
                    }
                    var sig = UIFactory.Image(sheet.transform, "Signature", UISprites.Rounded(3), CampaignHudStyle.MaxGold);
                    CampaignHudStyle.Place(sig.rectTransform, 0.15f, -0.36f, 0.45f, 0.06f);
                    sig.rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
                    break;
                }
                case "B": // THE BOOKS: a green ledger with a gold spine and coins
                {
                    var book = UIFactory.Image(root, "Ledger", UISprites.Rounded(8), UIColors.Hex(0x2E7D4F));
                    CampaignHudStyle.Place(book.rectTransform, 0f, 0.04f, 0.62f, 0.72f);
                    book.rectTransform.localRotation = Quaternion.Euler(0, 0, 5f);
                    var spine = UIFactory.Image(book.transform, "Spine", UISprites.Rounded(4), CampaignHudStyle.MaxGoldDeep);
                    CampaignHudStyle.Place(spine.rectTransform, -0.4f, 0f, 0.14f, 1f);
                    var label = UIFactory.Image(book.transform, "Label", UISprites.Rounded(4), UIColors.Hex(0xF4E6C0));
                    CampaignHudStyle.Place(label.rectTransform, 0.08f, 0.18f, 0.6f, 0.22f);
                    for (int i = 0; i < 3; i++)
                    {
                        var coin = UIFactory.Image(root, "Coin" + i, UISprites.Circle, CampaignHudStyle.MaxGold);
                        CampaignHudStyle.Place(coin.rectTransform, -0.26f + i * 0.2f, -0.36f + (i == 1 ? 0.04f : 0f), 0.18f, 0.18f);
                    }
                    break;
                }
                case "C": // THE BEDSHEET: a bedsheet ghost with eye holes
                {
                    var sheet = UIFactory.Image(root, "Sheet", UISprites.Rounded(40), UIColors.Hex(0xF2F4F8));
                    CampaignHudStyle.Place(sheet.rectTransform, 0f, 0.02f, 0.6f, 0.8f);
                    var hem = UIFactory.Image(root, "Hem", UISprites.Rounded(10), UIColors.Hex(0xDDE2EA));
                    CampaignHudStyle.Place(hem.rectTransform, 0f, -0.33f, 0.66f, 0.14f);
                    var e1 = UIFactory.Image(root, "EyeL", UISprites.Circle, CampaignHudStyle.Ink); CampaignHudStyle.Place(e1.rectTransform, -0.1f, 0.12f, 0.1f, 0.13f);
                    var e2 = UIFactory.Image(root, "EyeR", UISprites.Circle, CampaignHudStyle.Ink); CampaignHudStyle.Place(e2.rectTransform, 0.1f, 0.12f, 0.1f, 0.13f);
                    var tag = UIFactory.Image(root, "Tag", UISprites.Rounded(3), UIColors.Hex(0xFF8A1F));
                    CampaignHudStyle.Place(tag.rectTransform, 0.24f, -0.2f, 0.16f, 0.1f);
                    break;
                }
                default: // generic evidence folder
                {
                    var folder = UIFactory.Image(root, "Folder", UISprites.Rounded(8), UIColors.Hex(0xD9B978));
                    CampaignHudStyle.Place(folder.rectTransform, 0f, 0f, 0.7f, 0.56f);
                    var q = UIFactory.Text(root, "Q", "?", FontLibrary.Display, size * 0.4f, CampaignHudStyle.Ink, TextAlignmentOptions.Center);
                    UIFactory.Stretch(q.rectTransform);
                    break;
                }
            }
            return root;
        }
    }
}
