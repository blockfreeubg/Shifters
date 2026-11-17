// =====================================================================================
//  SHIFTERS - UIScreen.cs   (UI agent)
//  Base class of every full-screen menu (built lazily on first open, animated open/close,
//  default keyboard selection, Esc = OnBack). Screens live on UIManager's menu canvas and
//  are opened through UIManager.Push / ReplaceAll.
// =====================================================================================
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shifters
{
    public abstract class UIScreen : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        public CanvasGroup Group { get; private set; }
        public bool IsOpen { get; private set; }
        public bool Built { get; private set; }

        /// <summary>Selected when the screen opens (keyboard / gamepad navigation entry point).</summary>
        protected Selectable DefaultSelection;
        protected UIManager Manager => UIManager.Instance;

        /// <summary>Create a screen component on a new full-screen child of parent.</summary>
        public static T Create<T>(Transform parent, string name) where T : UIScreen
        {
            var rt = UIFactory.Rect(name, parent);
            UIFactory.Stretch(rt);
            var s = rt.gameObject.AddComponent<T>();
            s.Rect = rt;
            s.Group = UIFactory.Group(rt.gameObject);
            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>Build the visual tree (called once, lazily).</summary>
        protected abstract void Build();
        /// <summary>Called every time the screen is shown (refresh dynamic content, start animations).</summary>
        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }
        /// <summary>Esc / gamepad B. Default: back to the previous screen.</summary>
        public virtual void OnBack() { Sfx.Play(SfxId.UIBack); Manager?.Pop(); }

        public void EnsureBuilt()
        {
            if (Built) return;
            Built = true;
            Build();
        }

        public void Show()
        {
            gameObject.SetActive(true);   // active first: TMP texts initialize (and measure) correctly while building
            EnsureBuilt();
            transform.SetAsLastSibling();
            IsOpen = true;
            UITween.Kill(Group);
            Group.interactable = true;
            Group.blocksRaycasts = true;
            UITween.Fade(Group, 1f, 0.2f, Ease.OutQuad, 0f, 0f);
            OnOpened();
            SelectDefault();
        }

        public void Hide(bool instant = false)
        {
            if (!IsOpen && !gameObject.activeSelf) return;
            bool wasOpen = IsOpen;
            IsOpen = false;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            if (wasOpen) OnClosed();
            if (instant || !gameObject.activeInHierarchy) { UITween.Kill(Group); gameObject.SetActive(false); return; }
            UITween.Fade(Group, 0f, 0.14f, Ease.OutQuad, 0f, null, () => { if (this != null && !IsOpen) gameObject.SetActive(false); });
        }

        public void SelectDefault()
        {
            var es = EventSystem.current;
            if (es == null || DefaultSelection == null || !DefaultSelection.gameObject.activeInHierarchy) return;
            UIButton.MuteFrame = Time.frameCount;
            if (es.alreadySelecting) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(DefaultSelection.gameObject);
        }

        // ------------------------------------------------------------------ shared building blocks
        /// <summary>Big screen title (Bungee) with a yellow/orange underline bar.</summary>
        protected TMPro.TextMeshProUGUI Title(string text, Vector2 anchor, Vector2 pos, float size = 72f, TMPro.TextAlignmentOptions align = TMPro.TextAlignmentOptions.Center)
        {
            var t = UIFactory.Text(Rect, "Title", text, FontLibrary.Display, size, UIColors.Text, anchor, pos, new Vector2(1600f, size * 1.3f), align);
            if (FontLibrary.DisplayShadow != null) t.fontSharedMaterial = FontLibrary.DisplayShadow;
            t.enableVertexGradient = true;
            t.colorGradient = new TMPro.VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Accent);
            return t;
        }

        /// <summary>Full-screen darkening backdrop (optionally with hazard stripes along the bottom).</summary>
        protected Image Backdrop(float alpha = 0.82f, bool stripes = false)
        {
            var bg = UIFactory.Fill(Rect, "Backdrop", null, new Color(0.035f, 0.045f, 0.06f, alpha), true);
            var vig = UIFactory.Fill(Rect, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0f, 0.55f));
            vig.raycastTarget = false;
            if (stripes)
            {
                var band = UIFactory.Image(Rect, "Stripes", UISprites.Stripes, UIFactory.WithAlpha(UIColors.AccentYellow, 0.08f));
                band.type = Image.Type.Tiled;
                var brt = band.rectTransform;
                brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f); brt.pivot = new Vector2(0.5f, 0f);
                brt.sizeDelta = new Vector2(0f, 26f); brt.anchoredPosition = Vector2.zero;
            }
            return bg;
        }
    }
}
