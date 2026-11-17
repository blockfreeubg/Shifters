// =====================================================================================
//  SHIFTERS - MeTvFrame.cs   (UI agent)
//  Small gold "ME-TV" set used by menus for Max's one-liners (team reveal, level complete reaction,
//  campaign end). Static builder, no MonoBehaviour: callers set Line.text and animate Root.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public sealed class MeTvFrame
    {
        public RectTransform Root;
        public TextMeshProUGUI Line, Caption;
        public static readonly Color Gold = UIColors.Hex(0xFFD54A);

        /// <summary>Builds a TV of size at anchor/pos (pivot = anchor). caption = small text under the screen.</summary>
        public static MeTvFrame Create(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string caption = "MAX, LIVE ON ME-TV", float lineSize = 26f)
        {
            var f = new MeTvFrame();
            var rt = UIFactory.Rect("MeTv", parent, anchor, pos, size, anchor);
            f.Root = rt;
            var sh = UIFactory.Image(rt, "Shadow", UISprites.Shadow(16, 20), new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Expand(sh.rectTransform, 16f, new Vector2(0f, -6f));
            UIFactory.Fill(rt, "Case", UISprites.Rounded(20), UIColors.Hex(0x2A2418));
            var bezel = UIFactory.Image(rt, "Bezel", UISprites.Rounded(16), UIColors.Hex(0xC9A227));
            UIFactory.Stretch(bezel.rectTransform, 8f, 8f, 8f, 34f);
            var screen = UIFactory.Image(rt, "Screen", UISprites.Rounded(12), UIColors.Hex(0x0B0E0A));
            UIFactory.Stretch(screen.rectTransform, 14f, 14f, 14f, 40f);
            var scan = UIFactory.Image(screen.rectTransform, "Scan", UISprites.Stripes, new Color(1f, 1f, 1f, 0.035f));
            scan.type = Image.Type.Tiled;
            UIFactory.Stretch(scan.rectTransform);
            var bug = UIFactory.Text(screen.rectTransform, "Bug", "ME-TV", FontLibrary.Display, 18f, Gold, new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(120f, 22f), TextAlignmentOptions.Left, false, new Vector2(0f, 1f));
            bug.characterSpacing = 2f;
            var live = UIFactory.Text(screen.rectTransform, "Live", "LIVE", FontLibrary.Heavy, 15f, UIColors.Danger, new Vector2(1f, 1f), new Vector2(-10f, -8f), new Vector2(80f, 20f), TextAlignmentOptions.Right, false, new Vector2(1f, 1f));
            live.characterSpacing = 3f;
            f.Line = UIFactory.Text(screen.rectTransform, "Line", "", FontLibrary.Display, lineSize, Gold, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(f.Line.rectTransform, 14f, 14f, 26f, 8f);
            UIFactory.AutoSize(f.Line, Mathf.Min(14f, lineSize), lineSize);
            f.Caption = UIFactory.Text(rt, "Caption", caption, FontLibrary.Label, 15f, UIColors.Hex(0xC9A227), new Vector2(0f, 0f), new Vector2(18f, 9f), new Vector2(size.x - 36f, 20f), TextAlignmentOptions.Left, false, new Vector2(0f, 0f));
            f.Caption.characterSpacing = 4f;
            return f;
        }

        /// <summary>Pop-in animation + line.</summary>
        public void Say(string text, float delay = 0f, float tilt = -2f)
        {
            Line.text = "\"" + text + "\"";
            UITween.Scale(Root, 1f, 0.45f, Ease.OutBack, delay, 0.6f);
            UITween.Rotate(Root, tilt, 0.6f, Ease.OutElastic, delay, tilt + 8f);
        }
    }
}
