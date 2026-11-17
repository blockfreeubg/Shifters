// =====================================================================================
//  SHIFTERS - CreditsScreen.cs   (UI agent)
//  Auto-scrolling credits inside a masked viewport (loops). BACK returns.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class CreditsScreen : UIScreen
    {
        const float Speed = 70f;
        RectTransform _content, _viewport;
        float _contentH;
        public UIButton BackButton { get; private set; }

        static readonly string[] Lines =
        {
            "#SHIFTERS",
            "A game about terrible teamwork",
            "",
            "#THE GREAT SHIFT",
            "A story campaign in fourteen shifts and one impeachment",
            "",
            "@STARRING",
            "The Interns, as whoever survived",
            "Max, Supreme Sole Executive, as himself (all roles)",
            "Percy, the coffee machine, as the voice of reason",
            "",
            "@ALSO STARRING",
            "The Downsizer  ·  Porkchop  ·  The Boogeyman  ·  The One-Man Band",
            "(all built by Max, all defeated by teamwork)",
            "",
            "@CROWD SCENES",
            "The citizens of the world, in grey, then in color",
            "",
            "@CASE FILE EVIDENCE",
            "Exhibit A: the Grey Memo  ·  Exhibit B: the Books  ·  Exhibit C: the Bedsheet",
            "",
            "@HUMAN RESOURCES",
            "Also Max (until the vote)",
            "",
            "@MADE FOR",
            "Game Off 2026 — theme: SHIFT",
            "",
            "@BUILT WITH",
            "Unity 6 + URP",
            "",
            "@ART, AUDIO & MUSIC",
            "All models, sounds and music generated procedurally",
            "(no interns were harmed in the making of the polygons)",
            "",
            "@FONTS",
            "Bungee by David Jonathan Ross",
            "Barlow by Jeremy Tribby",
            "SIL Open Font License 1.1",
            "",
            "@QUALITY ASSURANCE",
            "Gary (lost to the void, re-cloned, lost again)",
            "",
            "@HUMAN RESOURCES (AFTER THE VOTE)",
            "Currently unavailable",
            "",
            "@SPECIAL THANKS",
            "Every intern who stood on a pressure plate so others could live",
            "The floor, employee of the month",
            "",
            "@NO VILLAINS WERE HARMED",
            "Max was impeached, not hurt. He is fine. He is furious.",
            "",
            "#THANK YOU FOR PLAYING",
            "Please return your hard hat at the front desk.",
        };

        protected override void Build()
        {
            Backdrop(0.9f, true);
            Title("CREDITS", new Vector2(0.5f, 1f), new Vector2(0f, -40f), 76f).rectTransform.pivot = new Vector2(0.5f, 1f);
            _viewport = UIFactory.Rect("Viewport", Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1300f, 720f));
            _viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 80);
            _content = UIFactory.Rect("Content", _viewport, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1300f, 100f), new Vector2(0.5f, 1f));
            float y = 0f;
            foreach (var line in Lines)
            {
                if (string.IsNullOrEmpty(line)) { y -= 34f; continue; }
                TextMeshProUGUI t;
                if (line[0] == '#')
                {
                    t = UIFactory.Text(_content, "H", line.Substring(1), FontLibrary.Display, 64f, UIColors.Text, TextAlignmentOptions.Center);
                    t.enableVertexGradient = true;
                    t.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Accent);
                }
                else if (line[0] == '@')
                {
                    t = UIFactory.Text(_content, "S", line.Substring(1), FontLibrary.Label, 26f, UIColors.Accent, TextAlignmentOptions.Center);
                    t.characterSpacing = 10f;
                }
                else t = UIFactory.Text(_content, "L", line, FontLibrary.Body, 32f, UIColors.Text, TextAlignmentOptions.Center, true);
                float h = UIFactory.Preferred(t, t.text, 1200f).y + 8f;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(1200f, h);
                rt.anchoredPosition = new Vector2(0f, y);
                y -= h;
            }
            _contentH = -y;
            _content.sizeDelta = new Vector2(1300f, _contentH);
            BackButton = UIFactory.Button(Rect, "Back", "BACK", () => OnBack(), new Vector2(300f, 64f), UIButtonStyle.Primary, 28f);
            BackButton.Rt.anchorMin = BackButton.Rt.anchorMax = new Vector2(0.5f, 0f); BackButton.Rt.pivot = new Vector2(0.5f, 0f);
            BackButton.Rt.anchoredPosition = new Vector2(0f, 34f);
            DefaultSelection = BackButton.Selectable;
        }

        /// <summary>Invoked once when the credits close (campaign end -> C16 post-credits), then cleared.</summary>
        public System.Action OnceClosed;

        protected override void OnClosed()
        {
            var a = OnceClosed;
            OnceClosed = null;
            a?.Invoke();
        }

        protected override void OnOpened() => _content.anchoredPosition = new Vector2(0f, -_viewport.rect.height * 0.35f);

        /// <summary>Scroll offset (tests).</summary>
        public float Scroll => _content != null ? _content.anchoredPosition.y : 0f;

        void Update()
        {
            if (!IsOpen || _content == null) return;
            var p = _content.anchoredPosition;
            p.y += Speed * UITime.Delta;
            if (p.y > _contentH + 40f) p.y = -_viewport.rect.height;
            _content.anchoredPosition = p;
        }
    }
}
