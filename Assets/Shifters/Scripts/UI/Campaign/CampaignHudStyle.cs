// =====================================================================================
//  SHIFTERS - CampaignHudStyle.cs   (ui_hud agent)
//  Shared look for the in-chapter campaign UI (CAMPAIGN §15.2): speaker colors, rubber
//  stamps, procedural textures (TV static, scanlines) and the drawn fallback faces used
//  by the ME-TV PiP / chapter card when VillainStudio's render texture is unavailable.
//  Static helpers only (no MonoBehaviour in this file).
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public static class CampaignHudStyle
    {
        public static readonly Color MaxGold = UIColors.Hex(0xFFC93C);
        public static readonly Color MaxGoldDeep = UIColors.Hex(0xC9932A);
        public static readonly Color PercyBrown = UIColors.Hex(0xD49A62);
        public static readonly Color PercyBody = UIColors.Hex(0x3B2A1F);
        public static readonly Color GhostBlue = UIColors.Hex(0xBFD8FF);
        public static readonly Color StampRed = UIColors.Hex(0xFF4D5E);
        public static readonly Color Ink = UIColors.Hex(0x12161C);
        public static readonly Color Manila = UIColors.Hex(0xE9CF95);
        public static readonly Color ScaredPurple = UIColors.Hex(0xB07CFF);
        public static readonly Color TvBody = UIColors.Hex(0x1A1F27);
        public static readonly Color StampInk = UIColors.Hex(0xD62839);

        public static bool IsMax(string id) => !string.IsNullOrEmpty(id) && (id.StartsWith("MAX", System.StringComparison.OrdinalIgnoreCase) || id.Equals("ME", System.StringComparison.OrdinalIgnoreCase));
        public static bool IsPercy(string id) => !string.IsNullOrEmpty(id) && id.StartsWith("PERCY", System.StringComparison.OrdinalIgnoreCase);
        public static bool IsGhost(string id) => !string.IsNullOrEmpty(id) && id.StartsWith("GHOST", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Speaker color: the line's own color when set (alpha &gt; 0), else Max gold / Percy coffee / ghost blue /
        /// the matching intern's primary color / white.</summary>
        public static Color SpeakerColor(StoryLine l)
        {
            if (l.SpeakerColor.a > 0.01f) { var c = l.SpeakerColor; c.a = 1f; return c; }
            return ColorForId(l.SpeakerId);
        }

        public static Color ColorForId(string id)
        {
            if (IsMax(id)) return MaxGold;
            if (IsPercy(id)) return PercyBrown;
            if (IsGhost(id)) return GhostBlue;
            var c = FindIntern(id);
            if (c != null) return c.Color;
            return UIColors.Text;
        }

        /// <summary>Team member for "BRUTE"/"SPRINTER"/"BRAINIAC" (archetype tokens) or a display name; null otherwise.</summary>
        public static ShifterCharacter FindIntern(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return null;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c == null) continue;
                if (string.Equals(c.Archetype.ToString(), id, System.StringComparison.OrdinalIgnoreCase)) return c;
                if (string.Equals(c.DisplayName, id, System.StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
        }

        /// <summary>Voice-blip pitch per speaker (Max squeaks high when pompous, Percy gurgles low).</summary>
        public static float BlipPitch(string id)
        {
            if (IsMax(id)) return 1.3f;
            if (IsPercy(id)) return 0.78f;
            if (IsGhost(id)) return 0.6f;
            var c = FindIntern(id);
            if (c != null)
            {
                switch (c.Archetype)
                {
                    case Archetype.Brute: return 0.72f;
                    case Archetype.Sprinter: return 1.35f;
                    default: return 1.05f;
                }
            }
            return 1f;
        }

        // ------------------------------------------------------------------ rubber stamp
        /// <summary>Red-ink rubber stamp: outlined rounded box + spaced Heavy text, rotated. Pivot centre. Returns root.</summary>
        public static RectTransform Stamp(Transform parent, string name, string text, Color color, float fontSize, float angle, out TextMeshProUGUI label, bool paper = false)
        {
            var root = UIFactory.Rect(name, parent);
            label = UIFactory.Text(root, "Label", text, FontLibrary.Heavy, fontSize, color, TextAlignmentOptions.Center);
            label.characterSpacing = 6f;
            var size = UIFactory.Preferred(label, text);
            float w = size.x + fontSize * 1.1f, h = Mathf.Max(fontSize * 1.55f, size.y + fontSize * 0.5f);
            root.sizeDelta = new Vector2(w, h);
            var fill = UIFactory.Fill(root, "Wash", UISprites.Rounded(6), paper ? new Color(0.96f, 0.93f, 0.86f, 0.94f) : new Color(color.r, color.g, color.b, 0.12f));
            fill.transform.SetAsFirstSibling();
            UIFactory.Fill(root, "Ink", UISprites.RoundedOutline(6, 3), new Color(color.r, color.g, color.b, 0.95f));
            label.transform.SetAsLastSibling();
            UIFactory.Stretch(label.rectTransform, 4f, 4f, 0f, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, angle);
            return root;
        }

        /// <summary>Resize a stamp built by <see cref="Stamp"/> after its text changed.</summary>
        public static void RefitStamp(RectTransform root, TextMeshProUGUI label, string text)
        {
            if (root == null || label == null) return;
            label.text = text;
            var size = UIFactory.Preferred(label, text);
            root.sizeDelta = new Vector2(size.x + label.fontSize * 1.1f, Mathf.Max(label.fontSize * 1.55f, size.y + label.fontSize * 0.5f));
        }

        // ------------------------------------------------------------------ procedural textures
        static Texture2D _noise, _scan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _noise = null; _scan = null; }

        /// <summary>96x72 grey TV static (point filtered, repeat). Animate with RawImage.uvRect offsets.</summary>
        public static Texture2D Noise
        {
            get
            {
                if (_noise != null) return _noise;
                const int w = 96, h = 72;
                _noise = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave, name = "CampaignTvNoise" };
                var px = new Color32[w * h];
                uint s = 0x9E3779B9u;
                for (int i = 0; i < px.Length; i++)
                {
                    s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                    byte v = (byte)(40 + (s % 216));
                    px[i] = new Color32(v, v, v, 255);
                }
                _noise.SetPixels32(px);
                _noise.Apply(false, false);
                return _noise;
            }
        }

        /// <summary>1x4 scanline texture (repeat): use uvRect height = lines/4.</summary>
        public static Texture2D Scanlines
        {
            get
            {
                if (_scan != null) return _scan;
                _scan = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave, name = "CampaignScanlines" };
                _scan.SetPixels32(new[] { new Color32(0, 0, 0, 120), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 60) });
                _scan.Apply(false, false);
                return _scan;
            }
        }

        // ------------------------------------------------------------------ drawn fallback faces
        /// <summary>Parts of a drawn face (mouth animates while talking).</summary>
        public struct DrawnFace
        {
            public RectTransform Root, Mouth, BrowL, BrowR;
        }

        /// <summary>Tiny, pompous Max (grey face, gold crown, monocle, mustache) drawn from UI shapes, filling 'parent'.
        /// Used when VillainStudio's render texture is not available.</summary>
        public static DrawnFace DrawMax(RectTransform parent)
        {
            var f = new DrawnFace();
            var root = UIFactory.Rect("MaxDrawn", parent);
            UIFactory.Stretch(root);
            f.Root = root;
            var bg = UIFactory.Fill(root, "Studio", UISprites.GradientV, UIColors.Hex(0x3A2C5C));
            bg.color = UIColors.Hex(0x4A3A70);
            var glow = UIFactory.Image(root, "Spot", UISprites.SoftCircle, new Color(1f, 0.85f, 0.4f, 0.35f));
            Place(glow.rectTransform, 0f, -0.05f, 1.1f, 1.1f);
            var body = UIFactory.Image(root, "Suit", UISprites.Rounded(30), UIColors.Hex(0x2B2F38));
            Place(body.rectTransform, 0f, -0.52f, 0.62f, 0.42f);
            var tie = UIFactory.Image(root, "Tie", UISprites.Triangle, MaxGold);
            Place(tie.rectTransform, 0f, -0.42f, 0.08f, 0.16f); tie.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            var head = UIFactory.Image(root, "Head", UISprites.Circle, UIColors.Hex(0xB9BEC8));
            Place(head.rectTransform, 0f, -0.02f, 0.44f, 0.58f);
            for (int i = -1; i <= 1; i++)
            {
                var spike = UIFactory.Image(root, "Crown" + i, UISprites.Triangle, MaxGold);
                Place(spike.rectTransform, i * 0.1f, 0.33f + (i == 0 ? 0.03f : 0f), 0.12f, 0.16f);
            }
            var band = UIFactory.Image(root, "CrownBand", UISprites.Rounded(4), MaxGoldDeep);
            Place(band.rectTransform, 0f, 0.255f, 0.34f, 0.06f);
            var eyeL = UIFactory.Image(root, "EyeL", UISprites.Circle, Ink); Place(eyeL.rectTransform, -0.08f, 0.04f, 0.045f, 0.06f);
            var eyeR = UIFactory.Image(root, "EyeR", UISprites.Circle, Ink); Place(eyeR.rectTransform, 0.08f, 0.04f, 0.045f, 0.06f);
            var mono = UIFactory.Image(root, "Monocle", UISprites.Ring(10), MaxGold); Place(mono.rectTransform, 0.08f, 0.04f, 0.12f, 0.16f);
            var chain = UIFactory.Image(root, "Chain", UISprites.Rounded(2), MaxGold); Place(chain.rectTransform, 0.15f, -0.06f, 0.012f, 0.16f);
            var bl = UIFactory.Image(root, "BrowL", UISprites.Rounded(3), Ink); Place(bl.rectTransform, -0.08f, 0.13f, 0.09f, 0.022f);
            var br = UIFactory.Image(root, "BrowR", UISprites.Rounded(3), Ink); Place(br.rectTransform, 0.08f, 0.13f, 0.09f, 0.022f);
            bl.rectTransform.localRotation = Quaternion.Euler(0, 0, -14f); br.rectTransform.localRotation = Quaternion.Euler(0, 0, 14f);
            f.BrowL = bl.rectTransform; f.BrowR = br.rectTransform;
            var mouth = UIFactory.Image(root, "Mouth", UISprites.Rounded(6), UIColors.Hex(0x3A1E22)); Place(mouth.rectTransform, 0f, -0.15f, 0.1f, 0.05f);
            f.Mouth = mouth.rectTransform;
            var mL = UIFactory.Image(root, "MustacheL", UISprites.Rounded(8), UIColors.Hex(0x2A2A30)); Place(mL.rectTransform, -0.055f, -0.095f, 0.11f, 0.04f);
            var mR = UIFactory.Image(root, "MustacheR", UISprites.Rounded(8), UIColors.Hex(0x2A2A30)); Place(mR.rectTransform, 0.055f, -0.095f, 0.11f, 0.04f);
            mL.rectTransform.localRotation = Quaternion.Euler(0, 0, 12f); mR.rectTransform.localRotation = Quaternion.Euler(0, 0, -12f);
            return f;
        }

        /// <summary>Percy the coffee machine (brown body, screen face) drawn from UI shapes, filling 'parent'.</summary>
        public static DrawnFace DrawPercy(RectTransform parent)
        {
            var f = new DrawnFace();
            var root = UIFactory.Rect("PercyDrawn", parent);
            UIFactory.Stretch(root);
            f.Root = root;
            UIFactory.Fill(root, "Room", UISprites.GradientV, UIColors.Hex(0x2A1C14));
            var body = UIFactory.Image(root, "Machine", UISprites.Rounded(18), UIColors.Hex(0x6B4A33));
            Place(body.rectTransform, 0f, -0.08f, 0.56f, 0.86f);
            var screen = UIFactory.Image(root, "Screen", UISprites.Rounded(10), UIColors.Hex(0x0F2A22));
            Place(screen.rectTransform, 0f, 0.1f, 0.42f, 0.34f);
            var eyeL = UIFactory.Image(root, "EyeL", UISprites.Circle, UIColors.Hex(0x7CFFB2)); Place(eyeL.rectTransform, -0.08f, 0.15f, 0.06f, 0.08f);
            var eyeR = UIFactory.Image(root, "EyeR", UISprites.Circle, UIColors.Hex(0x7CFFB2)); Place(eyeR.rectTransform, 0.08f, 0.15f, 0.06f, 0.08f);
            var mouth = UIFactory.Image(root, "Mouth", UISprites.Rounded(4), UIColors.Hex(0x7CFFB2)); Place(mouth.rectTransform, 0f, 0.02f, 0.14f, 0.03f);
            f.Mouth = mouth.rectTransform;
            var spout = UIFactory.Image(root, "Spout", UISprites.Rounded(4), UIColors.Hex(0x2A2A2A)); Place(spout.rectTransform, 0f, -0.2f, 0.1f, 0.08f);
            var cup = UIFactory.Image(root, "Cup", UISprites.Rounded(6), UIColors.Hex(0xF2F4F8)); Place(cup.rectTransform, 0f, -0.36f, 0.14f, 0.14f);
            return f;
        }

        /// <summary>Place a child in normalized parent space: centre (cx, cy) in -0.5..0.5, size (w, h) as fractions.</summary>
        public static void Place(RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f + cx - w * 0.5f, 0.5f + cy - h * 0.5f);
            rt.anchorMax = new Vector2(0.5f + cx + w * 0.5f, 0.5f + cy + h * 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Animate a drawn mouth (talk 0..1 = how open).</summary>
        public static void Talk(DrawnFace f, float open01)
        {
            if (f.Mouth == null) return;
            f.Mouth.localScale = new Vector3(1f + open01 * 0.15f, 0.6f + open01 * 2.2f, 1f);
        }

        /// <summary>Angry brows for Angry/Pout faces, raised for Shocked/Laugh.</summary>
        public static void SetFace(DrawnFace f, Expression e)
        {
            if (f.BrowL == null || f.BrowR == null) return;
            float a = 14f;
            if (e == Expression.Angry || e == Expression.Pout) a = -22f;
            else if (e == Expression.Shocked || e == Expression.Surprised || e == Expression.Laugh) a = 4f;
            else if (e == Expression.Smug) a = 22f;
            f.BrowL.localRotation = Quaternion.Euler(0, 0, -a);
            f.BrowR.localRotation = Quaternion.Euler(0, 0, a);
        }
    }
}
