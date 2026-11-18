// =====================================================================================
//  SHIFTERS - UIFactory.cs   (UI agent)
//  Code-only uGUI construction helpers in the SHIFTERS style: rects, panels with soft
//  shadows/glows, TMP labels, buttons, key caps, icons, chips, canvases, navigation links.
//  Reference resolution 1920x1080 (CanvasScaler match 0.5).
// =====================================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public enum UIButtonStyle { Menu, Primary, Secondary, Danger, Ghost }

    /// <summary>References to the parts of a factory panel.</summary>
    public class UIPanel : MonoBehaviour
    {
        public RectTransform Rect;
        public Image Bg, Shadow, Border, Glow;
        public CanvasGroup Group;

        /// <summary>Show/hide the colored glow halo (fades).</summary>
        public void SetGlow(bool on, Color color, float alpha = 0.85f)
        {
            if (Glow == null) return;
            var c = color; c.a = on ? alpha : 0f;
            UITween.Color(Glow, c, 0.18f);
        }
    }

    public static class UIFactory
    {
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        // ------------------------------------------------------------------ canvases / rects
        public static Canvas Canvas(string name, Transform parent, int sortOrder, bool raycaster)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            c.pixelPerfect = false;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = Reference;
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            s.referencePixelsPerUnit = 100f;
            if (raycaster) go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // built-in "UI" layer
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Rect anchored at anchor (0..1), pivot defaults to the anchor.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretch to fill the parent with insets (left, right, top, bottom).</summary>
        public static RectTransform Stretch(RectTransform rt, float l = 0f, float r = 0f, float t = 0f, float b = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Expand(RectTransform rt, float by, Vector2 offset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-by, -by) + offset; rt.offsetMax = new Vector2(by, by) + offset;
            return rt;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        // ------------------------------------------------------------------ graphics
        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        /// <summary>Full-parent image (stretched).</summary>
        public static Image Fill(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var img = Image(parent, name, sprite, color, raycast);
            Stretch(img.rectTransform);
            return img;
        }

        public static Image Icon(Transform parent, UIIcon icon, Color color, float size, Vector2? pos = null)
        {
            var img = Image(parent, "Icon_" + icon, UIIcons.Get(icon), color);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            if (pos.HasValue) img.rectTransform.anchoredPosition = pos.Value;
            return img;
        }

        /// <summary>Rounded panel with soft drop shadow (+ optional border and glow halo). Add content to the returned Rect.</summary>
        public static UIPanel Panel(Transform parent, string name, Vector2 size, Color color, int radius = 16, bool shadow = true, Color? border = null, bool glow = false)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;
            var p = rt.gameObject.AddComponent<UIPanel>();
            p.Rect = rt;
            if (glow)
            {
                p.Glow = Image(rt, "Glow", UISprites.Shadow(28, radius), new Color(1f, 0.54f, 0.12f, 0f));
                Expand(p.Glow.rectTransform, 28f, Vector2.zero);
            }
            if (shadow)
            {
                p.Shadow = Image(rt, "Shadow", UISprites.Shadow(22, radius), new Color(0f, 0f, 0f, 0.55f));
                Expand(p.Shadow.rectTransform, 22f, new Vector2(0f, -7f));
            }
            p.Bg = Fill(rt, "Bg", UISprites.Rounded(radius), color);
            if (border.HasValue) p.Border = Fill(rt, "Border", UISprites.RoundedOutline(radius, 3), border.Value);
            return p;
        }

        // ------------------------------------------------------------------ text
        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool wrap = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text ?? "";
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            // Overflow (never Ellipsis/Truncate): TMP truncates a single line to NOTHING when the rect is
            // shorter than the font's line height. Width-critical texts use AutoSize or measured rects.
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        // ------------------------------------------------------------------ measuring
        static TextMeshProUGUI _measure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMeasure() => _measure = null;

        /// <summary>Preferred size of text rendered with t's font/size/spacing/style (width = wrap width, 0 = no wrap).
        /// Works even while t is inactive (screens are built before they are shown), where TMP's own
        /// GetPreferredValues returns garbage because the component has not been initialized yet.</summary>
        public static Vector2 Preferred(TMP_Text t, string text, float width = 0f)
        {
            if (t == null) return Vector2.zero;
            text = text ?? "";
            if (t.isActiveAndEnabled) return width > 0f ? t.GetPreferredValues(text, width, 0f) : t.GetPreferredValues(text);
            var m = Measurer();
            if (m == null) return Vector2.zero;
            m.font = t.font;
            m.fontSize = t.fontSize;
            m.fontStyle = t.fontStyle;
            m.characterSpacing = t.characterSpacing;
            m.wordSpacing = t.wordSpacing;
            m.lineSpacing = t.lineSpacing;
            m.enableAutoSizing = false;
            m.textWrappingMode = width > 0f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            return width > 0f ? m.GetPreferredValues(text, width, 0f) : m.GetPreferredValues(text);
        }

        static TextMeshProUGUI Measurer()
        {
            if (_measure != null) return _measure;
            if (!Application.isPlaying) return null;
            var go = new GameObject("[UIMeasure]", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var cv = go.AddComponent<UnityEngine.Canvas>();
            cv.enabled = false;   // never renders; the GameObject stays active so TMP is initialized
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            _measure = tgo.AddComponent<TextMeshProUGUI>();
            _measure.raycastTarget = false;
            _measure.richText = true;
            return _measure;
        }

        /// <summary>Text placed at an anchored position with a fixed size.</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            Vector2 anchor, Vector2 pos, Vector2 rectSize, TextAlignmentOptions align = TextAlignmentOptions.Left, bool wrap = false, Vector2? pivot = null)
        {
            var t = Text(parent, name, text, font, size, color, align, wrap);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? anchor; rt.anchoredPosition = pos; rt.sizeDelta = rectSize;
            return t;
        }

        /// <summary>Auto-size a single line between min and max point sizes.</summary>
        public static TextMeshProUGUI AutoSize(TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true; t.fontSizeMin = min; t.fontSizeMax = max;
            return t;
        }

        // ------------------------------------------------------------------ widgets
        /// <summary>Keyboard key cap ("E", "RShift", "1"...). Width grows with the text. Pivot center. Static (never changes
        /// with the device): for a cap that follows the player's device use <see cref="ActionCap"/>.</summary>
        public static RectTransform KeyCap(Transform parent, string key, float height = 40f, Vector2? pos = null)
            => DeviceStyledCap(parent, key, false, height, pos);

        /// <summary>Static GAMEPAD-styled cap for a pad label ("A" round green, "RB" dark labelled cap...).</summary>
        public static RectTransform PadCap(Transform parent, string padLabel, float height = 40f, Vector2? pos = null)
            => DeviceStyledCap(parent, padLabel, true, height, pos);

        /// <summary>Live cap for an ACTION ("Jump", "SkipTutorial"...) or a legacy keyboard key name ("Space", "LShift", "TAB"):
        /// shows <paramref name="keyboardText"/> (default: the legacy key name as written, else the action's keyboard label)
        /// as the classic key cap on keyboard/mouse and the action's gamepad button on a pad, restyling live
        /// (<see cref="DeviceKeyCap"/>; listen to its Resized when caps are laid out side by side).</summary>
        public static RectTransform ActionCap(Transform parent, string action, float height = 40f, string keyboardText = null, Vector2? pos = null)
        {
            string act = KeyLabels.ActionForKey(action);
            string kb = keyboardText ?? (KeyLabels.IsKeyName(action) || act == null ? action : KeyLabels.Keyboard(act));
            var root = DeviceStyledCap(parent, kb, false, height, pos);
            var cap = root.gameObject.AddComponent<DeviceKeyCap>();
            cap.Init(act, kb, height);
            return root;
        }

        static RectTransform DeviceStyledCap(Transform parent, string key, bool gamepad, float height, Vector2? pos)
        {
            string name = string.IsNullOrEmpty(key) ? "?" : key.ToUpperInvariant();
            var root = Rect("Key_" + name, parent);
            var label = Text(root, "Label", name, FontLibrary.Label, height * 0.56f, UIColors.Hex(0x161B22), TextAlignmentOptions.Center);
            root.sizeDelta = new Vector2(height, height);
            if (pos.HasValue) root.anchoredPosition = pos.Value;
            var depth = Fill(root, "Depth", UISprites.Rounded(8), UIColors.Hex(0x7D8796));
            depth.rectTransform.offsetMin = new Vector2(0f, -4f); depth.rectTransform.offsetMax = new Vector2(0f, -2f);
            var face = Fill(root, "Face", UISprites.Rounded(8), UIColors.Hex(0xEEF1F5));
            face.rectTransform.offsetMin = new Vector2(0f, 2f);
            label.transform.SetAsLastSibling();
            Stretch(label.rectTransform, 2f, 2f, 0f, 2f);
            StyleCap(root, string.IsNullOrEmpty(key) ? "?" : key, gamepad, height);
            return root;
        }

        /// <summary>(Re)style a cap built by KeyCap/PadCap/ActionCap: keyboard = light key cap (classic), gamepad = Xbox-coloured
        /// round face button (A/B/X/Y) or dark labelled cap. Empty text hides the cap graphics. Updates the width.</summary>
        public static void StyleCap(RectTransform root, string text, bool gamepad, float height)
        {
            if (root == null) return;
            var label = root.Find("Label")?.GetComponent<TMP_Text>();
            var depth = root.Find("Depth")?.GetComponent<Image>();
            var face = root.Find("Face")?.GetComponent<Image>();
            if (label == null || depth == null || face == null) return;
            bool show = !string.IsNullOrEmpty(text);
            label.enabled = depth.enabled = face.enabled = show;
            if (!show) return;
            string t = text.ToUpperInvariant();
            label.text = t;
            float w;
            if (!gamepad)
            {
                depth.sprite = UISprites.Rounded(8); depth.type = UnityEngine.UI.Image.Type.Sliced; depth.color = UIColors.Hex(0x7D8796);
                face.sprite = UISprites.Rounded(8); face.type = UnityEngine.UI.Image.Type.Sliced; face.color = UIColors.Hex(0xEEF1F5);
                label.color = UIColors.Hex(0x161B22);
                label.fontSize = height * 0.56f;
                w = Mathf.Max(height, Preferred(label, t).x + height * 0.55f);
            }
            else if (InputPrompts.IsFaceButton(t, out var fc))
            {
                depth.sprite = UISprites.Circle; depth.type = UnityEngine.UI.Image.Type.Simple; depth.color = Color.Lerp(fc, Color.black, 0.5f);
                face.sprite = UISprites.Circle; face.type = UnityEngine.UI.Image.Type.Simple; face.color = fc;
                label.color = t == "Y" ? UIColors.Hex(0x161B22) : Color.white;
                label.fontSize = height * 0.6f;
                w = height;
            }
            else
            {
                int r = Mathf.Clamp(Mathf.RoundToInt(height * 0.4f), 6, 20);
                depth.sprite = UISprites.Rounded(r); depth.type = UnityEngine.UI.Image.Type.Sliced; depth.color = UIColors.Hex(0x12161C);
                face.sprite = UISprites.Rounded(r); face.type = UnityEngine.UI.Image.Type.Sliced; face.color = UIColors.Hex(0x4A5465);
                label.color = UIColors.Hex(0xF2F4F8);
                label.fontSize = height * 0.5f;
                w = Mathf.Max(height * 1.15f, Preferred(label, t).x + height * 0.6f);
            }
            root.sizeDelta = new Vector2(w, height);
            root.name = "Key_" + t;
        }

        /// <summary>Pill chip with optional icon + text. filled = solid color bg with dark text, else dark bg with colored border.</summary>
        public static RectTransform Chip(Transform parent, UIIcon? icon, string text, Color color, float height = 30f, bool filled = false)
        {
            var root = Rect("Chip_" + text, parent);
            int radius = Mathf.Clamp(Mathf.RoundToInt(height * 0.5f), 4, 32);
            Fill(root, "Bg", UISprites.Rounded(radius), filled ? color : new Color(0.06f, 0.07f, 0.09f, 0.88f));
            if (!filled) Fill(root, "Border", UISprites.RoundedOutline(radius, 2), new Color(color.r, color.g, color.b, 0.9f));
            Color fg = filled ? UIColors.Hex(0x12161C) : color;
            float x = height * 0.42f;
            if (icon.HasValue)
            {
                var ic = Icon(root, icon.Value, fg, height * 0.68f);
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(x - 2f, 0f);
                x += height * 0.68f + 4f;
            }
            var label = Text(root, "Label", text, FontLibrary.Label, height * 0.62f, fg, TextAlignmentOptions.Left);
            label.characterSpacing = 4f;
            float tw = Preferred(label, text).x;
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f); lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(x, 0f); lrt.sizeDelta = new Vector2(tw + 4f, height);
            root.sizeDelta = new Vector2(x + tw + height * 0.45f, height);
            return root;
        }

        /// <summary>Animated, sound-enabled, keyboard-navigable button.</summary>
        public static UIButton Button(Transform parent, string name, string label, Action onClick, Vector2 size, UIButtonStyle style = UIButtonStyle.Secondary, float fontSize = 0f)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var sel = rt.gameObject.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;
            sel.targetGraphic = hit;
            var b = rt.gameObject.AddComponent<UIButton>();
            b.Build(label, style, fontSize > 0f ? fontSize : Mathf.Clamp(size.y * 0.5f, 18f, 38f));
            b.OnClick = onClick;
            return b;
        }

        // ------------------------------------------------------------------ navigation
        public static void LinkVertical(IList<Selectable> list, bool wrap = true)
        {
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                if (list[i] == null) continue;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = i > 0 ? list[i - 1] : (wrap ? list[n - 1] : null);
                nav.selectOnDown = i < n - 1 ? list[i + 1] : (wrap ? list[0] : null);
                list[i].navigation = nav;
            }
        }

        public static void LinkHorizontal(IList<Selectable> list, bool wrap = true, Selectable down = null, Selectable up = null)
        {
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                if (list[i] == null) continue;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnLeft = i > 0 ? list[i - 1] : (wrap ? list[n - 1] : null);
                nav.selectOnRight = i < n - 1 ? list[i + 1] : (wrap ? list[0] : null);
                nav.selectOnDown = down; nav.selectOnUp = up;
                list[i].navigation = nav;
            }
        }

        // ------------------------------------------------------------------ misc
        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>Readable text color (dark or light) for a given background.</summary>
        public static Color ContrastText(Color bg) => (bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f) > 0.55f ? UIColors.Hex(0x12161C) : UIColors.Text;

        public static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Split(new[] { ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return name.Substring(0, 1).ToUpperInvariant();
            if (parts.Length == 1) return parts[0].Substring(0, Mathf.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds - m * 60f;
            return m.ToString("00") + ":" + s.ToString("00.0");
        }
    }
}
