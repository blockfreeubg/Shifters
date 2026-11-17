// =====================================================================================
//  SHIFTERS - VictoryScreen.cs   (UI agent)
//  After Level 5 (GameSession.ShowVictory when the main menu loads): "YOU'RE PROMOTED TO
//  SENIOR INTERN", run summary, CREDITS / MAIN MENU. Plays the Victory music.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class VictoryScreen : UIScreen
    {
        RectTransform _rays, _title;
        TextMeshProUGUI _stats;
        public UIButton CreditsButton { get; private set; }
        public UIButton MenuButton { get; private set; }

        protected override void Build()
        {
            Backdrop(0.86f, true);
            var rays = UIFactory.Image(Rect, "Rays", UISprites.RadialLines(28), UIFactory.WithAlpha(UIColors.AccentYellow, 0.14f));
            _rays = rays.rectTransform;
            _rays.anchorMin = _rays.anchorMax = new Vector2(0.5f, 0.5f);
            _rays.sizeDelta = new Vector2(2200f, 2200f);
            _rays.anchoredPosition = new Vector2(0f, 200f);
            var pre = UIFactory.Text(Rect, "Pre", "ALL FIVE SHIFTS SURVIVED. MOSTLY.", FontLibrary.Label, 40f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1600f, 50f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            pre.characterSpacing = 8f;
            var t = UIFactory.Text(Rect, "Title", "END OF SHIFT", FontLibrary.Display, 150f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(1800f, 180f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            if (FontLibrary.LogoMaterial != null) t.fontSharedMaterial = FontLibrary.LogoMaterial;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Hex(0xF26A10));
            _title = t.rectTransform;
            var sub = UIFactory.Text(Rect, "Sub", "You're promoted to Senior Intern. Your new desk is a slightly bigger box.", FontLibrary.Body, 32f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(1600f, 44f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            sub.fontStyle = FontStyles.Italic;
            var panel = UIFactory.Panel(Rect, "Stats", new Vector2(1000f, 232f), UIFactory.WithAlpha(UIColors.Panel, 0.95f), 22, true, UIFactory.WithAlpha(UIColors.Accent, 0.7f));
            panel.Rect.anchorMin = panel.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            panel.Rect.anchoredPosition = new Vector2(0f, -90f);
            var hh = UIFactory.Text(panel.Rect, "H", "FINAL EMPLOYEE REPORT", FontLibrary.Display, 30f, UIColors.AccentYellow, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 40f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            hh.characterSpacing = 2f;
            _stats = UIFactory.Text(panel.Rect, "Body", "", FontLibrary.Body, 27f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(920f, 140f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            _stats.lineSpacing = 14f;
            CreditsButton = UIFactory.Button(Rect, "Credits", "CREDITS", () => Manager?.OpenCredits(), new Vector2(320f, 70f), UIButtonStyle.Secondary, 30f);
            CreditsButton.Rt.anchorMin = CreditsButton.Rt.anchorMax = new Vector2(0.5f, 0f); CreditsButton.Rt.pivot = new Vector2(0.5f, 0f);
            CreditsButton.Rt.anchoredPosition = new Vector2(-190f, 70f);
            MenuButton = UIFactory.Button(Rect, "Menu", "MAIN MENU", () => OnBack(), new Vector2(320f, 70f), UIButtonStyle.Primary, 30f);
            MenuButton.Rt.anchorMin = MenuButton.Rt.anchorMax = new Vector2(0.5f, 0f); MenuButton.Rt.pivot = new Vector2(0.5f, 0f);
            MenuButton.Rt.anchoredPosition = new Vector2(190f, 70f);
            UIFactory.LinkHorizontal(new List<Selectable> { CreditsButton.Selectable, MenuButton.Selectable }, true);
            DefaultSelection = MenuButton.Selectable;
        }

        protected override void OnOpened()
        {
            var st = GameSession.Instance != null ? GameSession.Instance.Stats : null;
            if (st != null)
                _stats.text =
                    "<color=#9AA4B2>TOTAL TIME</color>  " + UIFactory.FormatTime(st.PlayTime) + "     <color=#9AA4B2>LEVELS</color>  " + st.LevelsCompleted + "/" + LevelCatalog.MainGameCount + "\n" +
                    "<color=#9AA4B2>SWITCHES</color>  " + st.Switches + "     <color=#9AA4B2>THROWS</color>  " + st.Throws + "     <color=#9AA4B2>BRIDGES</color>  " + st.Bridges + "     <color=#9AA4B2>FLOPS</color>  " + st.Flops + "\n" +
                    "<color=#9AA4B2>COWORKERS LOST</color>  " + st.Lost + "     <color=#9AA4B2>CAPTURED</color>  " + st.Captures + "     <color=#9AA4B2>RESTARTS</color>  " + st.Restarts;
            else _stats.text = "Records were lost in a tragic shredder incident.";
            UITween.Scale(_title, 1f, 0.7f, Ease.OutElastic, 0.1f, 0.2f);
            AudioManager.Instance?.PlayMusic(MusicId.Victory);
            Sfx.Play(SfxId.LevelComplete);
        }

        protected override void OnClosed() => AudioManager.Instance?.PlayMusic(MusicId.Menu);

        void Update()
        {
            if (!IsOpen || _rays == null) return;
            _rays.localEulerAngles = new Vector3(0f, 0f, _rays.localEulerAngles.z + UITime.Delta * 8f);
        }
    }
}
