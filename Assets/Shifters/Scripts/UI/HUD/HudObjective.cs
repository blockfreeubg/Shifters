// =====================================================================================
//  SHIFTERS - HudObjective.cs   (UI agent)
//  Top-left: small level title + objective text. Slides/flashes when the objective changes.
//  Listens: ObjectiveChanged, LevelStarted.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudObjective : MonoBehaviour
    {
        RectTransform _rt;
        TextMeshProUGUI _level, _text;
        Image _flash;
        CanvasGroup _group;
        string _current = "";

        public string Text => _current;

        public static HudObjective Create(Transform parent)
        {
            var rt = UIFactory.Rect("Objective", parent, new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(640f, 92f), new Vector2(0f, 1f));
            var o = rt.gameObject.AddComponent<HudObjective>();
            o._rt = rt;
            o._group = UIFactory.Group(rt.gameObject);
            var panel = UIFactory.Panel(rt, "Panel", rt.sizeDelta, UIFactory.WithAlpha(UIColors.Panel, 0.8f), 12, true);
            UIFactory.Stretch(panel.Rect);
            var bar = UIFactory.Image(rt, "Bar", UISprites.Rounded(3), UIColors.Accent);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 0.5f);
            brt.offsetMin = new Vector2(10f, 12f); brt.offsetMax = new Vector2(16f, -12f);
            o._level = UIFactory.Text(rt, "Level", "", FontLibrary.Label, 19f, UIColors.Accent, new Vector2(0f, 1f), new Vector2(30f, -10f), new Vector2(590f, 24f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            o._level.characterSpacing = 6f;
            o._text = UIFactory.Text(rt, "Text", "", FontLibrary.Body, 25f, UIColors.Text, new Vector2(0f, 1f), new Vector2(30f, -36f), new Vector2(590f, 48f), TextAlignmentOptions.TopLeft, true, new Vector2(0f, 1f));
            UIFactory.AutoSize(o._text, 18f, 25f);
            o._flash = UIFactory.Fill(rt, "Flash", UISprites.Rounded(12), new Color(1f, 0.79f, 0.24f, 0f));
            o._group.alpha = 0f;
            return o;
        }

        void OnEnable()
        {
            GameEvents.ObjectiveChanged += OnObjective;
            GameEvents.LevelStarted += RefreshLevel;
        }

        void OnDisable()
        {
            GameEvents.ObjectiveChanged -= OnObjective;
            GameEvents.LevelStarted -= RefreshLevel;
        }

        /// <summary>Header text ("SHIFT 05  /  THE BACK WAY IN" in a campaign chapter). Read-only, for tests.</summary>
        public string LevelText => _level != null ? _level.text : "";

        public void Clear() { _current = ""; _text.text = ""; _group.alpha = 0f; }

        public void RefreshLevel()
        {
            int idx = GameSession.Instance != null ? GameSession.Instance.LevelIndex : -1;
            var sceneIdx = LevelCatalog.IndexOfScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (idx < 0) idx = sceneIdx;
            var info = LevelCatalog.Get(idx);
            ChapterView ch = null;
            try { ch = CampaignLink.IsCampaign ? CampaignLink.Current : null; } catch { ch = null; }
            _level.text = ch != null ? ch.ShiftLabel + "  /  " + (ch.Title ?? "").ToUpperInvariant()
                : info != null ? CampaignLink.ShiftLabelFor(idx) + "  /  " + info.Title : "OBJECTIVE";
            if (string.IsNullOrEmpty(_current) && info != null) OnObjective(info.DefaultObjective);
        }

        void OnObjective(string text)
        {
            if (string.IsNullOrEmpty(text)) { _group.alpha = 0f; _current = ""; return; }
            bool changed = text != _current;
            _current = text;
            _text.text = text;
            if (string.IsNullOrEmpty(_level.text)) RefreshLevel();
            UITween.Fade(_group, 1f, 0.2f);
            if (!changed) return;
            UITween.Move(_rt, new Vector2(26f, -24f), 0.4f, Ease.OutBack, 0f, new Vector2(-40f, -24f));
            UITween.Color(_flash, new Color(1f, 0.79f, 0.24f, 0f), 0.6f, Ease.OutQuad, 0f, new Color(1f, 0.79f, 0.24f, 0.45f));
            Sfx.Play(SfxId.UIPop, 0.7f);
        }
    }
}
