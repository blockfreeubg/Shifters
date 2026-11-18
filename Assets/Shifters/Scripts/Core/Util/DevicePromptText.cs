// =====================================================================================
//  SHIFTERS - DevicePromptText.cs   (gp_core)
//  Live device-aware text for ANY TMP_Text (HUD TextMeshProUGUI or world-space TextMeshPro sign).
//  Stores a template ("STEP UP  ({JUMP})") and re-renders it through InputPrompts.Format whenever
//  ShiftersInput.DeviceChanged fires. Builders attach it at build time with DevicePromptText.Attach:
//  the template is SERIALIZED into the generated scene and the baked TMP text is the keyboard version,
//  so the scene looks exactly as before until a gamepad is used.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class DevicePromptText : MonoBehaviour
    {
        [SerializeField, TextArea(1, 4)] string _template = "";
        [Tooltip("TMP rich styling of gamepad labels (coloured face buttons, bold pad labels). Keyboard text is never styled.")]
        [SerializeField] bool _rich = true;
        [SerializeField] TMP_Text _text;
        int _renderedVersion = -1;

        /// <summary>The template (tokens per InputPrompts). Setting it re-renders immediately.</summary>
        public string Template { get => _template; set { _template = value ?? ""; Render(true); } }
        public bool Rich { get => _rich; set { _rich = value; Render(true); } }
        public TMP_Text Text => _text != null ? _text : (_text = GetComponent<TMP_Text>());

        /// <summary>Attach (or update) the live template on a TMP text and render it for the current device
        /// (at edit time / in builders: the keyboard text). Returns the component.</summary>
        public static DevicePromptText Attach(TMP_Text text, string template, bool rich = true)
        {
            if (text == null) return null;
            var d = text.GetComponent<DevicePromptText>();
            if (d == null) d = text.gameObject.AddComponent<DevicePromptText>();
            d._text = text;
            d._rich = rich;
            d._template = template ?? "";
            d.Render(true);
            return d;
        }

        /// <summary>Convenience for code that only has a GameObject / component holding a TMP_Text.</summary>
        public static DevicePromptText Attach(Component holder, string template, bool rich = true)
            => holder == null ? null : Attach(holder.GetComponentInChildren<TMP_Text>(true), template, rich);

        /// <summary>For multi-text props (poster title + subtitle, whiteboard, holo panel): finds the child TMP_Text whose
        /// text contains the template's KEYBOARD rendering and attaches the template there (any surrounding text the prop
        /// added, e.g. size tags, is kept). Build the prop with <c>InputPrompts.Format(template, false, false)</c> first.
        /// Returns null (and warns) when no child text matches.</summary>
        public static DevicePromptText AttachInChildren(GameObject root, string template, bool rich = true)
        {
            if (root == null || string.IsNullOrEmpty(template)) return null;
            string kb = InputPrompts.Format(template, false, false);
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string cur = texts[i].text ?? "";
                int at = cur.IndexOf(kb, System.StringComparison.Ordinal);
                if (at < 0) continue;
                return Attach(texts[i], cur.Substring(0, at) + template + cur.Substring(at + kb.Length), rich);
            }
            Debug.LogWarning("[DevicePromptText] AttachInChildren: no text '" + kb.Replace("\n", " / ") + "' under " + root.name);
            return null;
        }

        void OnEnable()
        {
            ShiftersInput.DeviceChanged += OnDeviceChanged;
            Render(false);
        }

        void OnDisable() => ShiftersInput.DeviceChanged -= OnDeviceChanged;

        void OnDeviceChanged(bool gamepad) => Render(true);

        /// <summary>Re-render for the current device (force = even if the device did not change since the last render).</summary>
        public void Render(bool force)
        {
            var t = Text;
            if (t == null || _template == null) return;
            int v = Application.isPlaying ? ShiftersInput.DeviceVersion : -2;
            if (!force && v == _renderedVersion) return;
            _renderedVersion = v;
            bool pad = Application.isPlaying && ShiftersInput.UsingGamepad;
            string s = InputPrompts.Format(_template, pad, _rich);
            if (t.text != s) t.text = s;
        }
    }
}
