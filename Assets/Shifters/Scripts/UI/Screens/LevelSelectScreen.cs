// =====================================================================================
//  SHIFTERS - LevelSelectScreen.cs   (UI agent)
//  Five test-chamber cards (number, title, subtitle, description, theme color, completed
//  badge). All playable: uses the existing run or creates one, then GameSession.LoadLevel(i).
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class LevelSelectScreen : UIScreen
    {
        public readonly List<UIButton> Cards = new List<UIButton>();
        readonly List<GameObject> _done = new List<GameObject>();
        readonly List<TextMeshProUGUI> _status = new List<TextMeshProUGUI>();
        public UIButton BackButton { get; private set; }

        public static Color ThemeColor(LevelTheme t)
        {
            switch (t)
            {
                case LevelTheme.Training: return UIColors.Hex(0x5AB0FF);
                case LevelTheme.Factory: return UIColors.Accent;
                case LevelTheme.Lab: return UIColors.Hex(0x8BE04E);
                case LevelTheme.Research: return UIColors.Intellect;
                case LevelTheme.Chaos: return UIColors.Danger;
                default: return UIColors.AccentYellow;
            }
        }

        protected override void Build()
        {
            Backdrop(0.84f, true);
            Title("LEVEL SELECT", new Vector2(0.5f, 1f), new Vector2(0f, -48f), 76f).rectTransform.pivot = new Vector2(0.5f, 1f);
            var sub = UIFactory.Text(Rect, "Sub", "PICK YOUR SHIFT. ALL CHAMBERS UNLOCKED. HR CANNOT STOP YOU.", FontLibrary.Label, 28f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(1400f, 36f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            sub.characterSpacing = 8f;
            var sels = new List<Selectable>();
            for (int i = 0; i < LevelCatalog.MainGameCount; i++)   // W0: the shipped 5 levels (FREE PLAY lists the rest)
            {
                var info = LevelCatalog.Get(i);
                int idx = i;
                var b = UIFactory.Button(Rect, "Level" + i, "", () => Play(idx), new Vector2(330f, 500f), UIButtonStyle.Secondary);
                b.HoverScale = 1.06f;
                b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                b.Rt.anchoredPosition = new Vector2((i - 2) * 352f, -10f);
                BuildCard(b, info);
                Cards.Add(b);
                sels.Add(b.Selectable);
            }
            BackButton = UIFactory.Button(Rect, "Back", "BACK", () => OnBack(), new Vector2(260f, 64f), UIButtonStyle.Ghost, 28f);
            BackButton.Rt.anchorMin = BackButton.Rt.anchorMax = new Vector2(0.5f, 0f); BackButton.Rt.pivot = new Vector2(0.5f, 0f);
            BackButton.Rt.anchoredPosition = new Vector2(0f, 54f);
            UIFactory.LinkHorizontal(sels, true, BackButton.Selectable, null);
            BackButton.Selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = sels[0] };
            DefaultSelection = sels[0];
        }

        void BuildCard(UIButton b, LevelInfo info)
        {
            var color = ThemeColor(info.Theme);
            var rt = b.Rt;
            var band = UIFactory.Image(rt, "Band", UISprites.Rounded(14), color);
            var brt = band.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(0f, -150f); brt.offsetMax = Vector2.zero;
            var st = UIFactory.Image(brt, "Stripes", UISprites.Stripes, new Color(0f, 0f, 0f, 0.12f));
            st.type = Image.Type.Tiled;
            UIFactory.Stretch(st.rectTransform);
            var num = UIFactory.Text(brt, "Num", (info.Index + 1).ToString("00"), FontLibrary.Display, 96f, UIColors.Hex(0x12161C), TextAlignmentOptions.Center);
            var shiftTag = UIFactory.Text(brt, "Shift", "SHIFT", FontLibrary.Label, 24f, UIColors.Hex(0x12161C), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(300f, 28f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            shiftTag.characterSpacing = 14f;
            UIFactory.Stretch(num.rectTransform, 0f, 0f, 30f, -4f);
            var title = UIFactory.Text(rt, "Title", info.Title, FontLibrary.Display, 30f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(300f, 80f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(title, 20f, 30f);
            var sub = UIFactory.Text(rt, "Sub", info.Subtitle.ToUpperInvariant(), FontLibrary.Label, 19f, color, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(300f, 48f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            sub.characterSpacing = 3f;
            var desc = UIFactory.Text(rt, "Desc", info.Description, FontLibrary.Body, 21f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -304f), new Vector2(290f, 120f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(desc, 15f, 21f);
            var done = UIFactory.Chip(rt, UIIcon.Check, "COMPLETED", UIColors.Success, 32f, true);
            done.anchorMin = done.anchorMax = new Vector2(0.5f, 0f); done.pivot = new Vector2(0.5f, 0f);
            done.anchoredPosition = new Vector2(0f, 22f);
            _done.Add(done.gameObject);
            var status = UIFactory.Text(rt, "Status", "PLAY", FontLibrary.Display, 24f, color, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 32f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f));
            _status.Add(status);
            b.Label.transform.SetAsLastSibling();
        }

        void Play(int index)
        {
            if (!SessionActions.HasRun) SessionActions.NewRun();
            SessionActions.LoadLevel(index);
        }

        protected override void OnOpened()
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                bool done = SessionActions.IsLevelCompleted(i);
                _done[i].SetActive(done);
                _status[i].gameObject.SetActive(!done);
                var rt = Cards[i].Rt;
                var target = new Vector2((i - 2) * 352f, -10f);
                UITween.Move(rt, target, 0.45f, Ease.OutBack, 0.04f * i, target + new Vector2(0f, -140f));
            }
        }
    }
}
