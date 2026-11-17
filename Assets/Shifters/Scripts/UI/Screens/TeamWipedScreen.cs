// =====================================================================================
//  SHIFTERS - TeamWipedScreen.cs   (UI agent)
//  Opened on GameEvents.TeamWiped: "TEAM WIPED" on a hazard band + funny subtitle,
//  RESTART CHECKPOINT / RESTART LEVEL / MAIN MENU.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class TeamWipedScreen : UIScreen
    {
        TextMeshProUGUI _sub;
        RectTransform _title, _band;
        public UIButton CheckpointButton { get; private set; }
        public UIButton LevelButton { get; private set; }
        public UIButton MenuButton { get; private set; }
        public string Subtitle => _sub != null ? _sub.text : "";

        static readonly string[] Subs =
        {
            "All three interns are currently 'unavailable'.",
            "HR has been notified. HR is laughing.",
            "Somehow, everyone. At once. Impressive.",
            "The void sends its regards. And its thanks.",
            "Have you tried not doing that?",
            "Teamwork makes the dream work. This was not the dream.",
        };

        protected override void Build()
        {
            var bg = Backdrop(0.82f, false);
            bg.color = new Color(0.12f, 0.02f, 0.03f, 0.86f);
            _band = UIFactory.Rect("Band", Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(2600f, 230f));
            var stripes = UIFactory.Image(_band, "Stripes", UISprites.Stripes, UIColors.AccentYellow);
            stripes.type = Image.Type.Tiled;
            UIFactory.Stretch(stripes.rectTransform);
            var inner = UIFactory.Image(_band, "Inner", null, UIColors.Hex(0x141015));
            UIFactory.Stretch(inner.rectTransform, 0f, 0f, 22f, 22f);
            _band.localEulerAngles = new Vector3(0f, 0f, -4f);
            var t = UIFactory.Text(Rect, "Title", "TEAM WIPED", FontLibrary.Display, 150f, UIColors.Danger, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1800f, 180f), TextAlignmentOptions.Center);
            if (FontLibrary.LogoMaterial != null) t.fontSharedMaterial = FontLibrary.LogoMaterial;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(UIColors.Hex(0xFF7A85), UIColors.Hex(0xFF7A85), UIColors.Danger, UIColors.Hex(0xC92A3C));
            _title = t.rectTransform;
            _title.localEulerAngles = new Vector3(0f, 0f, -4f);
            _sub = UIFactory.Text(Rect, "Sub", "", FontLibrary.Heavy, 36f, UIColors.Text, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1500f, 50f), TextAlignmentOptions.Center);

            CheckpointButton = Btn("RESTART CHECKPOINT", () => SessionActions.RestartCheckpoint(), new Vector2(0f, -200f), new Vector2(520f, 76f), UIButtonStyle.Primary);
            LevelButton = Btn("RESTART LEVEL", () => SessionActions.RestartLevel(), new Vector2(-300f, -300f), new Vector2(400f, 64f), UIButtonStyle.Secondary);
            MenuButton = Btn("MAIN MENU", () => SessionActions.MainMenu(), new Vector2(300f, -300f), new Vector2(400f, 64f), UIButtonStyle.Ghost);
            var n = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = LevelButton.Selectable, selectOnUp = LevelButton.Selectable };
            CheckpointButton.Selectable.navigation = n;
            UIFactory.LinkHorizontal(new List<Selectable> { LevelButton.Selectable, MenuButton.Selectable }, true, CheckpointButton.Selectable, CheckpointButton.Selectable);
            DefaultSelection = CheckpointButton.Selectable;
        }

        UIButton Btn(string label, System.Action a, Vector2 pos, Vector2 size, UIButtonStyle style)
        {
            var b = UIFactory.Button(Rect, "Btn_" + label, label, a, size, style, 28f);
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0.5f);
            b.Rt.anchoredPosition = pos;
            return b;
        }

        protected override void OnOpened()
        {
            _sub.text = Subs[Random.Range(0, Subs.Length)];
            UITween.Scale(_title, 1f, 0.6f, Ease.OutElastic, 0f, 2.4f);
            UITween.Shake(_title, 26f, 0.5f);
            UITween.Scale(_band, new Vector3(1f, 1f, 1f), 0.35f, Ease.OutBack, 0f, new Vector3(1f, 0f, 1f));
            Sfx.Play(SfxId.Alarm, 0.7f);
            PostFx.Flash(new Color(1f, 0.1f, 0.15f, 1f), 0.35f);
        }

        public override void OnBack() { }
    }
}
