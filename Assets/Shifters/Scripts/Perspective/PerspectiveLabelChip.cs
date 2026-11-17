// =====================================================================================
//  SHIFTERS - PerspectiveLabelChip.cs   [owned by the PERSPECTIVE agent]
//  One pooled world-space label: dark chip background, glowing perspective accent strip,
//  "<TAG>  text" in TextMeshPro. Billboarded, scaled, decluttered and pulled toward the camera
//  by PerspectiveLabels (so it is never hidden behind characters). Text is re-laid out only
//  when it actually changes.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class PerspectiveLabelChip : MonoBehaviour
    {
        /// <summary>Line height (m) at scale 1.</summary>
        public const float TextSize = 0.26f;
        /// <summary>Chip height (m) at scale 1.</summary>
        public const float ChipHeight = 0.42f;
        const float PadX = 0.14f;

        public TextMeshPro Text { get; private set; }
        public string CurrentText { get; private set; } = "";
        public float NextRefresh { get; set; }
        public float Alpha { get; private set; } = 1f;
        public bool InUse { get; private set; }
        /// <summary>Chip width (m) at scale 1.</summary>
        public float Width { get; private set; } = 1f;

        Transform _bg, _accent;
        MeshRenderer _accentR;
        PerspectiveMask _tag = (PerspectiveMask)(-1);

        public static PerspectiveLabelChip Create(Transform parent)
        {
            var go = new GameObject("LabelChip");
            go.transform.SetParent(parent, false);
            var chip = go.AddComponent<PerspectiveLabelChip>();
            chip.Build();
            go.SetActive(false);
            return chip;
        }

        void Build()
        {
            var unit = ProcMesh.CachedBox(new Vector3(1f, 1f, 0.02f), 0f);
            var bg = new GameObject("Bg");
            bg.transform.SetParent(transform, false);
            var bgR = ProcMesh.AddRenderer(bg, unit, MaterialLibrary.Colored(UIColors.Hex(0x0E1116), 0f, 0f), false);
            bgR.receiveShadows = false;
            _bg = bg.transform;
            var ac = new GameObject("Accent");
            ac.transform.SetParent(transform, false);
            _accentR = ProcMesh.AddRenderer(ac, unit, MaterialLibrary.Colored(UIColors.Accent, 0f, 1.6f), false);
            _accentR.receiveShadows = false;
            _accent = ac.transform;
            Text = PerspectiveFonts.MakeText(transform, "Text", "", TextSize, UIColors.Text, new Vector3(0f, 0f, -0.02f), Quaternion.identity);
            Layout(1f);
        }

        public void Acquire()
        {
            InUse = true;
            CurrentText = "";
            NextRefresh = 0f;
            Alpha = -1f;
        }

        public void Release()
        {
            InUse = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>Sets the label (no-op when unchanged). tag = perspective whose color/badge is shown. Empty text hides the chip.</summary>
        public void SetText(string text, PerspectiveMask tag)
        {
            text ??= "";
            if (_tag != tag)
            {
                _tag = tag;
                _accentR.sharedMaterial = MaterialLibrary.Colored(PerspectiveStyle.ColorOf(tag), 0f, 1.6f);
                CurrentText = null; // force re-compose with the new tag color
            }
            if (CurrentText != null && string.Equals(CurrentText, text)) return;
            CurrentText = text;
            bool show = text.Length > 0;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (!show) return;
            string t = PerspectiveStyle.Tag(tag);
            Text.text = t.Length > 0 ? "<color=#" + PerspectiveStyle.Hex(tag) + "><b>" + t + "</b></color>  " + text : text;
            Layout(Mathf.Max(0.4f, Text.GetPreferredValues(Text.text).x));
        }

        void Layout(float textWidth)
        {
            float w = textWidth + PadX * 2f + 0.06f;
            Width = w;
            _bg.localScale = new Vector3(w, ChipHeight, 1f);
            _bg.localPosition = new Vector3(0.03f, 0f, 0f);
            _accent.localScale = new Vector3(0.07f, ChipHeight, 1f);
            _accent.localPosition = new Vector3(-w * 0.5f + 0.03f, 0f, -0.012f);
            Text.rectTransform.sizeDelta = new Vector2(w, ChipHeight);
            Text.transform.localPosition = new Vector3(0.06f, 0f, -0.02f);
        }

        /// <summary>Fades the text (chips are opaque so they stay batched; PerspectiveLabels also shrinks faded chips).</summary>
        public void SetAlpha(float a)
        {
            if (Mathf.Abs(a - Alpha) < 0.01f) return;
            Alpha = a;
            var c = Text.color; c.a = a; Text.color = c;
        }
    }
}
