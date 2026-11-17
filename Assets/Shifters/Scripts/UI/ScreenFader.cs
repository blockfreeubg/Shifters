// =====================================================================================
//  SHIFTERS - ScreenFader.cs   [CONTRACT API - implementation owned by the UI agent]
//  Full-screen fade used by GameSession around scene loads. Uses unscaled time.
//  Implementation must work even if UIManager does not exist yet (create its own overlay canvas).
//
//  Own DontDestroyOnLoad overlay canvas (sort order 100): black plate + optional caption.
//  Caption: "TITLE" or "TITLE\nSubtitle". A caption equal to a LevelCatalog title gets the
//  level's subtitle automatically ("THE WEIGHT PROBLEM" + "Level 2 - Industrial Factory").
// =====================================================================================
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public static class ScreenFader
    {
        public static bool IsFading { get; private set; }
        /// <summary>Current opacity (0 = clear).</summary>
        public static float Alpha => _group != null ? _group.alpha : 0f;

        static GameObject _root;
        static CanvasGroup _group, _captionGroup;
        static TextMeshProUGUI _title, _subtitle;
        static RectTransform _bar;
        static int _token;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { IsFading = false; _root = null; _group = null; _captionGroup = null; _title = null; _subtitle = null; _bar = null; _token = 0; }

        /// <summary>Fade to opaque (optionally showing a caption such as the level title).</summary>
        public static IEnumerator FadeOut(float seconds = 0.35f, string caption = null)
        {
            Ensure();
            int token = ++_token;
            SetCaption(caption);
            IsFading = true;
            _group.blocksRaycasts = true;
            float start = _group.alpha;
            float t = 0f;
            seconds = Mathf.Max(0.0001f, seconds);
            while (t < 1f)
            {
                if (token != _token) yield break;
                t += UITime.Delta / seconds;
                float e = Easing.Evaluate(Ease.InOutSine, Mathf.Clamp01(t));
                _group.alpha = Mathf.Lerp(start, 1f, e);
                if (_captionGroup != null && _captionGroup.gameObject.activeSelf)
                {
                    _captionGroup.alpha = Mathf.Clamp01((t - 0.35f) / 0.65f);
                    if (_bar != null) _bar.sizeDelta = new Vector2(Mathf.Lerp(0f, 220f, Easing.Evaluate(Ease.OutCubic, t)), 6f);
                }
                yield return null;
            }
            if (token == _token)
            {
                _group.alpha = 1f;
                if (_captionGroup != null) _captionGroup.alpha = 1f;
                IsFading = false;
            }
        }

        public static IEnumerator FadeIn(float seconds = 0.35f)
        {
            Ensure();
            int token = ++_token;
            IsFading = true;
            float start = _group.alpha;
            float t = 0f;
            seconds = Mathf.Max(0.0001f, seconds);
            while (t < 1f)
            {
                if (token != _token) yield break;
                t += UITime.Delta / seconds;
                _group.alpha = Mathf.Lerp(start, 0f, Easing.Evaluate(Ease.InOutSine, Mathf.Clamp01(t)));
                yield return null;
            }
            if (token == _token)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                IsFading = false;
            }
        }

        /// <summary>Instantly set opacity (0 = clear).</summary>
        public static void Set(float alpha)
        {
            Ensure();
            _token++;
            IsFading = false;
            _group.alpha = Mathf.Clamp01(alpha);
            _group.blocksRaycasts = alpha > 0.5f;
            if (alpha <= 0f && _captionGroup != null) _captionGroup.gameObject.SetActive(false);
        }

        static void SetCaption(string caption)
        {
            if (_captionGroup == null) return;
            if (string.IsNullOrEmpty(caption)) { _captionGroup.gameObject.SetActive(false); return; }
            string title = caption, sub = null;
            int nl = caption.IndexOf('\n');
            if (nl >= 0) { title = caption.Substring(0, nl); sub = caption.Substring(nl + 1); }
            else
            {
                for (int i = 0; i < LevelCatalog.Count; i++)
                {
                    var info = LevelCatalog.Get(i);
                    if (info != null && string.Equals(info.Title, caption, System.StringComparison.OrdinalIgnoreCase)) { sub = info.Subtitle; break; }
                }
            }
            _title.text = title;
            _subtitle.text = sub ?? "";
            _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            _captionGroup.gameObject.SetActive(true);
            _captionGroup.alpha = 0f;
            if (_bar != null) _bar.sizeDelta = new Vector2(0f, 6f);
        }

        static void Ensure()
        {
            if (_root != null) return;
            _root = new GameObject("[ScreenFader]");
            Object.DontDestroyOnLoad(_root);
            var canvas = UIFactory.Canvas("Fader Canvas", _root.transform, 100, true);
            _group = UIFactory.Group(canvas.gameObject);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            var plate = UIFactory.Fill(canvas.transform, "Plate", null, UIColors.Hex(0x07090C), true);
            plate.raycastTarget = true;
            var vig = UIFactory.Fill(canvas.transform, "Vignette", UISprites.Vignette, new Color(0f, 0f, 0f, 0.6f));
            vig.raycastTarget = false;

            var cap = UIFactory.Rect("Caption", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1600f, 260f));
            _captionGroup = UIFactory.Group(cap.gameObject);
            _title = UIFactory.Text(cap, "Title", "", FontLibrary.Display, 84f, UIColors.Text, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1600f, 110f), TextAlignmentOptions.Center);
            if (FontLibrary.DisplayShadow != null) _title.fontSharedMaterial = FontLibrary.DisplayShadow;
            _title.enableVertexGradient = true;
            _title.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Accent);
            UIFactory.AutoSize(_title, 40f, 84f);
            var bar = UIFactory.Image(cap, "Bar", UISprites.Rounded(3), UIColors.Accent);
            _bar = bar.rectTransform;
            _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 0.5f);
            _bar.anchoredPosition = new Vector2(0f, -22f);
            _bar.sizeDelta = new Vector2(220f, 6f);
            _subtitle = UIFactory.Text(cap, "Subtitle", "", FontLibrary.Label, 34f, UIColors.TextMuted, new Vector2(0.5f, 0.5f), new Vector2(0f, -64f), new Vector2(1400f, 50f), TextAlignmentOptions.Center);
            _subtitle.characterSpacing = 8f;
            cap.gameObject.SetActive(false);
        }
    }
}
