// =====================================================================================
//  SHIFTERS - EnvKitSigns.cs   (RENDERING)
//  Signage: 3D TextMeshPro signs (Info / Warning / Danger / Exit / Arrow), funny corporate posters, and the
//  facing helpers shared by every mounted piece. Visual only (no colliders).
//
//  FACING CONVENTION of every mounted piece - EnvKit.Sign, Poster, LightPanel, Decor.Whiteboard,
//  Decor.MotivationalPoster, Decor.HoloPanel, Decor.SparkingPanel (and, identically, LevelBuildContext.Sign/Label,
//  PuzzleVisuals.Text and a raw TextMeshPro transform):
//    * 'rot' is the rotation of the VIEWER'S LOOK: rot * Vector3.forward points FROM the reader INTO the wall.
//    * The readable / lit face points the opposite way, along rot * Vector3.back (out of the wall, towards the reader).
//    * The root the factory returns is built in face space: root.transform.forward = outward normal of the readable face.
//    * Quaternion.identity = readable from the -Z side (a player walking +Z reads it); Euler(0, 180, 0) = readable from +Z.
//    * Passing "the direction the text should face" as rot mounts the piece BACKWARDS (blank back towards the player).
//      That mistake hit L1-L5 (all levels compensate locally: MountYaw = yaw + 180 / Face() = rot * Y180). Don't repeat it:
//  New code should get 'rot' from the explicit helpers below:
//    EnvKit.WallMountFacing(wallNormal)        wallNormal = OUTWARD normal of the wall surface (pointing into the room)
//    EnvKit.FacingViewer(towardViewer)         direction FROM the piece TOWARDS where the reader stands
//    EnvKit.FacingViewerAt(piecePos, viewerPos) readable side turned (about Y only, text upright) towards a point
//    EnvKit.ViewerLooking(lookDirection)       the raw convention spelled out (direction the reader looks)
//    EnvKit.ReadableDirection(rot)             where the readable face of a piece built with 'rot' points
//  Example - a sign on the EAST wall of a room (wall surface at x = 9.8, the room on its -X side, so the wall's outward
//  normal is -X):   EnvKit.Sign(parent, new Vector3(9.78f, 2.2f, 4f), EnvKit.WallMountFacing(Vector3.left), "EXIT ->");
//  The smoke tests (SceneAudit.SignProblems, run for every scene) flag any text or panel whose readable side faces a
//  wall, is upside down or mirrored.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public enum SignStyle { Info, Warning, Danger, Exit, Arrow }

    public static partial class EnvKit
    {
        static readonly string[][] PosterLines =
        {
            new[] { "EMPLOYEE OF THE MONTH", "THE FLOOR. ALWAYS THERE FOR YOU." },
            new[] { "FAILURE IS A LEARNING OPPORTUNITY", "(FOR US.)" },
            new[] { "QUOTA: 0 / 1000", "KEEP SHIFTING!" },
            new[] { "TEAMWORK", "BECAUSE BLAME IS EASIER TO SHARE" },
            new[] { "SAFETY FIRST", "(SAFETY RESULTS NOT GUARANTEED)" },
            new[] { "DO NOT FEED THE ROBOTS", "THEY BITE. LITIGIOUSLY." },
            new[] { "HAVE YOU TRIED", "BEING SMARTER?" },
            new[] { "GRAVITY", "IT'S NOT JUST A GOOD IDEA. IT'S THE LAW." },
            new[] { "DAYS WITHOUT INCIDENT: 0", "PERSONAL BEST: 0" },
            new[] { "YOUR CLONE IS VALUED", "YOUR ORIGINAL WAS ALSO VALUED" },
        };

        /// <summary>Number of built-in funny poster lines (Decor.MotivationalPoster picks by index).</summary>
        public static int PosterLineCount => PosterLines.Length;

        // ------------------------------------------------------------------ facing helpers (see the header)
        /// <summary>The 'rot' to pass to a mounted piece (Sign, Poster, LightPanel, Decor.Whiteboard / MotivationalPoster /
        /// HoloPanel / SparkingPanel, LevelBuildContext.Sign / Label, a raw TextMeshPro) so that its readable / lit face
        /// points along <paramref name="towardViewer"/> = the direction FROM the piece TOWARDS where the reader stands.
        /// <paramref name="textUp"/> = where the top of the text points (default: world up; when towardViewer is (nearly)
        /// vertical - a piece lying on the floor or stuck to a ceiling - the default is world +Z, i.e. the text reads for
        /// someone facing +Z).
        /// <code>
        /// EnvKit.Sign(p, pos, EnvKit.FacingViewer(Vector3.back), "HI");    // readable from -Z (= Quaternion.identity)
        /// EnvKit.Poster(p, pos, EnvKit.FacingViewer(Vector3.right), "A", "B"); // print faces +X (read by someone at +X)
        /// EnvKit.LightPanel(p, pos, EnvKit.FacingViewer(Vector3.down), size, c); // ceiling panel shining down
        /// </code></summary>
        public static Quaternion FacingViewer(Vector3 towardViewer, Vector3? textUp = null)
        {
            if (towardViewer.sqrMagnitude < 1e-10f) return Quaternion.identity;
            var n = towardViewer.normalized;
            var fallbackUp = Mathf.Abs(n.y) > 0.99f ? Vector3.forward : Vector3.up;
            var up = textUp ?? fallbackUp;
            if (Vector3.Cross(n, up).sqrMagnitude < 1e-6f) up = fallbackUp;   // degenerate hint (parallel to the normal)
            return Quaternion.LookRotation(-n, up);
        }

        /// <summary>The 'rot' for a mounted piece at <paramref name="piecePosition"/> whose readable side turns towards
        /// <paramref name="viewerPosition"/> about the vertical axis only (the text stays upright). Example: a free-standing
        /// sign readable from the level spawn: EnvKit.Sign(p, pos, EnvKit.FacingViewerAt(pos, spawnPos), "WELCOME").</summary>
        public static Quaternion FacingViewerAt(Vector3 piecePosition, Vector3 viewerPosition)
        {
            var d = viewerPosition - piecePosition;
            d.y = 0f;
            return FacingViewer(d.sqrMagnitude > 1e-8f ? d : Vector3.back);
        }

        /// <summary>The 'rot' for a piece mounted flat on a wall, given the wall surface's OUTWARD normal = the direction
        /// pointing out of the wall into the space where the reader stands (for a wall built with EnvKit.Wall from 'a' to
        /// 'b' that is +/- Vector3.Cross(Vector3.up, b - a).normalized, the sign picking the room side). The readable face
        /// then points along wallNormal and the text stays upright. Same result as FacingViewer(wallNormal).
        /// <code>
        /// // room z = 0..20 with its north wall surface at z = 19.8: outward normal = -Z (back into the room)
        /// EnvKit.Sign(p, new Vector3(0f, 2.5f, 19.78f), EnvKit.WallMountFacing(Vector3.back), "TEST CHAMBER 1");
        /// // east wall surface at x = 9.8 (room on its -X side): outward normal = -X
        /// EnvKit.Poster(p, new Vector3(9.78f, 1.8f, 6f), EnvKit.WallMountFacing(Vector3.left), "SAFETY THIRD", "(PROBABLY)");
        /// </code></summary>
        public static Quaternion WallMountFacing(Vector3 wallNormal) => FacingViewer(wallNormal);

        /// <summary>The raw convention spelled out: the 'rot' for a reader who LOOKS along <paramref name="lookDirection"/>
        /// (towards the wall the piece hangs on). ViewerLooking(Vector3.forward) = Quaternion.identity.</summary>
        public static Quaternion ViewerLooking(Vector3 lookDirection, Vector3? textUp = null) => FacingViewer(-lookDirection, textUp);

        /// <summary>World direction the readable / lit face of a mounted piece built with 'rot' points (= rot * Vector3.back,
        /// towards the reader). For an already built EnvKit piece use its root's transform.forward; for a TextMeshPro text
        /// its -transform.forward.</summary>
        public static Vector3 ReadableDirection(Quaternion rot) => rot * Vector3.back;

        /// <summary>Converts a mounted piece's 'rot' (the viewer's look rotation) into the rotation of the piece ROOT the
        /// kit builds (face space: root.forward = outward normal of the readable face). It is a 180 degree turn about the
        /// local Y axis and therefore its own inverse - which is all the levels' local Face() / MountYaw helpers do.
        /// Level code should not need it: use FacingViewer / WallMountFacing / FacingViewerAt.</summary>
        public static Quaternion ViewToFace(Quaternion viewRot) => viewRot * Quaternion.Euler(0f, 180f, 0f);

        /// <summary>Panel sign with 3D TMP text. rot = the VIEWER's look rotation (rot*forward points INTO the wall, the
        /// readable face points along rot*back) - get it from WallMountFacing(wallNormal) / FacingViewer(towardViewer).
        /// The returned root's transform.forward points out of the readable face. size = panel width/height (m).
        /// Arrow style draws an arrow pointing to the viewer's right (prefix the text with '&lt;' to point left).</summary>
        public static GameObject Sign(Transform parent, Vector3 pos, Quaternion rot, string text, Vector2 size, SignStyle style = SignStyle.Info)
            => SignFace(parent, pos, ViewToFace(rot), text, size, style);

        /// <summary>Auto-sized sign (letter height ~0.42 m, panel fitted to the text) with the style guessed from the
        /// text: '&lt;'/'&gt;' prefix = Arrow, EXIT = Exit, DANGER/DO NOT/VOID/ACID/LASER = Danger, WARNING/CAUTION/MIND/
        /// HIGH VOLTAGE = Warning, else Info. Same facing convention (rot = the viewer's look; see WallMountFacing). This is
        /// the overload LevelBuildContext.Sign(parent, pos, yaw, text) picks: its 'yaw' is the direction the viewer LOOKS.</summary>
        public static GameObject Sign(Transform parent, Vector3 pos, Quaternion rot, string text)
            => Sign(parent, pos, rot, text, AutoSignSize(text, 0.42f), GuessSignStyle(text));

        /// <summary>Panel size that fits 'text' (multi-line aware) at the given letter height.</summary>
        public static Vector2 AutoSignSize(string text, float letterHeight = 0.42f)
        {
            text = (text ?? "").Trim('<', '>', ' ');
            int longest = 1, lines = 1, cur = 0;
            foreach (char ch in text) { if (ch == '\n') { lines++; cur = 0; } else if (++cur > longest) longest = cur; }
            return new Vector2(Mathf.Max(1.2f, longest * letterHeight * 0.66f + letterHeight * 1.6f), letterHeight * (1.25f * lines + 1.1f));
        }

        /// <summary>Sign style guessed from the text (see the 4-argument Sign overload).</summary>
        public static SignStyle GuessSignStyle(string text)
        {
            var u = (text ?? "").ToUpperInvariant();
            if (u.StartsWith("<") || u.StartsWith(">") || u.EndsWith(">") || u.Contains("->") || u.Contains("<-")) return SignStyle.Arrow;
            if (u.Contains("EXIT")) return SignStyle.Exit;
            if (u.Contains("DANGER") || u.Contains("DO NOT") || u.Contains("VOID") || u.Contains("ACID") || u.Contains("LASER") || u.Contains("DEATH")) return SignStyle.Danger;
            if (u.Contains("WARNING") || u.Contains("CAUTION") || u.Contains("MIND THE") || u.Contains("VOLTAGE") || u.Contains("HEAVY")) return SignStyle.Warning;
            return SignStyle.Info;
        }

        /// <summary>Sign builder in FACE space: the readable face points along faceRot*forward (= the root's +Z). Internal;
        /// the public overloads take the viewer-look 'rot' and convert it with ViewToFace.</summary>
        static GameObject SignFace(Transform parent, Vector3 pos, Quaternion faceRot, string text, Vector2 size, SignStyle style)
        {
            text = text ?? "";
            size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.15f, size.y));
            var root = Kit.Make("Sign_" + style, parent, pos, faceRot);
            var t = root.transform;
            MatKey bg; MatKey frame; Color txt;
            switch (style)
            {
                case SignStyle.Warning: bg = MatKey.HazardYellow; frame = MatKey.HazardStripes; txt = UIColors.Hex(0x15161A); break;
                case SignStyle.Danger: bg = MatKey.WarningRed; frame = MatKey.SignWhite; txt = Color.white; break;
                case SignStyle.Exit: bg = MatKey.SafetyOrange; frame = MatKey.HazardYellow; txt = Color.white; break;
                case SignStyle.Arrow: bg = MatKey.MetalDark; frame = MatKey.HazardYellow; txt = UIColors.Hex(0xFFC93C); break;
                default: bg = MatKey.SignWhite; frame = MatKey.MetalDark; txt = UIColors.Hex(0x1E2430); break;
            }
            const float depth = 0.06f;
            Kit.BoxVisual(t, "Panel", Vector3.zero, new Vector3(size.x, size.y, depth), bg, 0.01f);
            float fw = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.08f, 0.03f, style == SignStyle.Warning ? 0.14f : 0.08f);
            var fkey = EnvMeshes.Key("env_signframe", size, fw);
            var fmesh = EnvMeshes.Cached(fkey, p =>
            {
                var c = Color.white; float z = 0.006f;
                p.Box(new Vector3(0f, size.y * 0.5f - fw * 0.5f, z), new Vector3(size.x, fw, depth), c, 0f);
                p.Box(new Vector3(0f, -size.y * 0.5f + fw * 0.5f, z), new Vector3(size.x, fw, depth), c, 0f);
                p.Box(new Vector3(size.x * 0.5f - fw * 0.5f, 0f, z), new Vector3(fw, size.y - fw * 2f, depth), c, 0f);
                p.Box(new Vector3(-size.x * 0.5f + fw * 0.5f, 0f, z), new Vector3(fw, size.y - fw * 2f, depth), c, 0f);
            });
            EnvMeshes.Render(t, "Frame", fmesh, M(frame), Vector3.zero, null, false);

            float textW = size.x - fw * 2.6f, textX = 0f;
            if (style == SignStyle.Arrow)
            {
                bool left = text.StartsWith("<") || text.StartsWith("<-");
                text = text.Replace("->", "").Replace("<-", "").Trim('<', '>', ' ');
                float aw = Mathf.Min(size.y * 0.9f, size.x * 0.3f);
                // the reader looks along -Z, so the reader's right is local -X
                float ax = (size.x * 0.5f - fw - aw * 0.6f) * (left ? 1f : -1f);
                var amesh = EnvMeshes.Cached(EnvMeshes.Key("env_arrow", aw), p => p.Arrow(Vector3.zero, aw, aw * 0.75f, 0.03f, Color.white));
                EnvMeshes.Render(t, "Arrow", amesh, M(MatKey.HazardYellow), new Vector3(ax, 0f, depth * 0.5f + 0.015f), left ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f), false);
                textW -= aw * 1.2f;
                textX = left ? -aw * 0.6f : aw * 0.6f;
            }
            AddText(t, text, new Vector3(textX, 0f, depth * 0.5f + 0.004f), new Vector2(textW, size.y - fw * 2.4f), txt,
                text.Length > 22 ? ShiftersFonts.Label : ShiftersFonts.Display, size.y * 0.55f);
            return root;
        }

        /// <summary>Funny corporate poster (frame, paper, colored header band, icon, title + subtitle, HR footer). Mounted-
        /// piece convention: rot = the VIEWER's look rotation (rot*forward points INTO the wall, the print faces rot*back) -
        /// get it from WallMountFacing(wallNormal) / FacingViewer(towardViewer). The returned root's transform.forward points
        /// out of the print. Default 1.1 x 1.5 m. Visual only.</summary>
        public static GameObject Poster(Transform parent, Vector3 pos, Quaternion rot, string title, string subtitle, Vector2? size = null)
        {
            var s = size ?? new Vector2(1.1f, 1.5f);
            title = title ?? ""; subtitle = subtitle ?? "";
            var root = Kit.Make("Poster", parent, pos, ViewToFace(rot));
            var t = root.transform;
            Color[] accents = { UIColors.Hex(0xFF8A1F), UIColors.Hex(0x2EC4B6), UIColors.Hex(0xB07CFF), UIColors.Hex(0xE8394B), UIColors.Hex(0x3C7DD9), UIColors.Hex(0x3DDC97) };
            int h = 0; foreach (char ch in title) h = h * 31 + ch;
            var accent = accents[Mathf.Abs(h) % accents.Length];
            Kit.BoxVisual(t, "Backing", Vector3.zero, new Vector3(s.x + 0.08f, s.y + 0.08f, 0.03f), MatKey.MetalDark, 0.01f);
            Kit.BoxVisual(t, "Paper", new Vector3(0f, 0f, 0.02f), new Vector3(s.x, s.y, 0.01f), MatKey.Poster, 0f);
            float band = s.y * 0.2f;
            EnvMeshes.Render(t, "Band", ProcMesh.CachedBox(new Vector3(s.x, band, 0.012f), 0f), MaterialLibrary.Colored(accent, 0.3f), new Vector3(0f, s.y * 0.5f - band * 0.5f, 0.024f), null, false);
            // icon: a big friendly circle with a "!" made of two boxes
            float ir = Mathf.Min(s.x, s.y) * 0.17f;
            var icon = EnvMeshes.Cached(EnvMeshes.Key("env_poster_icon", ir), p =>
            {
                p.Cyl(Vector3.zero, ir, 0.01f, Color.white, 24, Quaternion.Euler(90f, 0f, 0f), false);
                p.Box(new Vector3(0f, ir * 0.18f, 0.008f), new Vector3(ir * 0.28f, ir * 0.9f, 0.006f), new Color(0.12f, 0.12f, 0.14f), 0f);
                p.Box(new Vector3(0f, -ir * 0.55f, 0.008f), new Vector3(ir * 0.28f, ir * 0.25f, 0.006f), new Color(0.12f, 0.12f, 0.14f), 0f);
            });
            EnvMeshes.Render(t, "Icon", icon, MaterialLibrary.Colored(accent, 0.3f), new Vector3(0f, s.y * 0.5f - band - ir * 1.25f, 0.026f), null, false);
            float textTop = s.y * 0.5f - band - ir * 2.5f;
            AddText(t, title, new Vector3(0f, textTop - s.y * 0.11f, 0.028f), new Vector2(s.x * 0.86f, s.y * 0.2f), UIColors.Hex(0x1E2430), ShiftersFonts.Display, s.y * 0.1f);
            AddText(t, subtitle, new Vector3(0f, textTop - s.y * 0.29f, 0.028f), new Vector2(s.x * 0.84f, s.y * 0.15f), UIColors.Hex(0x4A5363), ShiftersFonts.Label, s.y * 0.07f);
            AddText(t, "SHIFTERS INC. HR", new Vector3(0f, s.y * 0.5f - band * 0.5f, 0.032f), new Vector2(s.x * 0.9f, band * 0.7f), Color.white, ShiftersFonts.Display, band * 0.45f);
            return root;
        }

        /// <summary>World-space TMP text block whose readable side faces the PARENT's +Z (viewer on the parent's +Z side,
        /// looking -Z) - i.e. the face-space convention of a mounted piece's root, not the viewer-look 'rot' of the public
        /// factories. Auto-sized into 'area' (m), wraps, no shadows.</summary>
        public static TextMeshPro AddText(Transform parent, string text, Vector3 localPos, Vector2 area, Color color, TMP_FontAsset font, float maxHeight)
        {
            var go = Kit.Make("Text", parent, localPos, Quaternion.Euler(0f, 180f, 0f));
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.2f;
            tmp.fontSizeMax = Mathf.Max(0.3f, maxHeight * 10f);
            tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.05f, area.x), Mathf.Max(0.05f, area.y));
            tmp.margin = Vector4.zero;
            var r = tmp.GetComponent<MeshRenderer>();
            if (r != null) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            return tmp;
        }
    }
}
