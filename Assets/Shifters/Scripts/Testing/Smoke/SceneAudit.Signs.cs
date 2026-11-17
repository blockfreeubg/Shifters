// =====================================================================================
//  SHIFTERS - SceneAudit.Signs.cs   [smoke tests - GENERIC sign audit, run for every scene by SmokeTests.Run]
//  SceneAudit.SignProblems() judges every world-space text in the loaded scenes (TextMeshPro, and TMP UGUI on
//  world-space canvases - EnvKit signs / posters / whiteboards / holo panels, ctx labels, kit readouts, clues) and
//  every EnvKit LightPanel:
//    FACES-WALL   the readable / lit side runs into solid geometry within SignClearance (0.4 m): mounted backwards or
//                 facing a wall. The detail says whether the back side is open (then a 180 degree turn fixes it).
//    BURIED       the text itself sits inside solid geometry (its centre and most of it): invisible.
//    UPSIDE-DOWN / SIDEWAYS   text on a (near) vertical face whose top does not point up.
//    MIRRORED     negative scale: the text reads mirrored.
//  Warnings (listed, not issues): CLIPPED = part of the text (but not its centre) runs into geometry, e.g. a long
//  line whose ends disappear in the side walls.
//  A face counts as blocked when >= 5 of 9 rays (3x3 grid over the rendered text) hit within the clearance, so a pipe
//  or a railing post in front of a big sign does not. "Solid" = enabled non-trigger collider that is not part of the
//  judged piece (or the object a label is printed on), not a character, not a dynamic rigidbody, not a movable prop.
//  Not judged (counted in the summary): camera-facing billboards and animated texts (CollectMovingTexts moves the camera
//  aside for a few frames and watches which texts turn), labels on movable props (Pushable / Grabbable / dynamic
//  rigidbodies), texts on characters, and the back face of a double-sided item whose front face is fine.
//  Floor / ceiling texts: FACES-WALL / BURIED / MIRRORED only (from where a floor text is read is level knowledge).
//  Every issue carries the object path and the world position + facing of the readable face; SignIssueSheet renders a
//  numbered close-up sheet. The facing convention itself is documented in Rendering/EnvKitSigns.cs (header).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static partial class SceneAudit
    {
        /// <summary>Free space (m) required in front of a readable / lit face.</summary>
        public const float SignClearance = 0.4f;

        /// <summary>One text / light panel that fails the sign audit.</summary>
        public sealed class SignIssue
        {
            /// <summary>FACES-WALL, BURIED, UPSIDE-DOWN, SIDEWAYS, MIRRORED (several joined with '+').</summary>
            public string Kind = "";
            /// <summary>"text", or the EnvKit piece the text belongs to (Sign, Poster, LightPanel, Whiteboard, HoloPanel...).</summary>
            public string What = "";
            /// <summary>The text (tags stripped, line breaks as '/'); empty for a light panel.</summary>
            public string Text = "";
            /// <summary>Hierarchy path "scene:Root/.../Object" (the EnvKit piece root when the text belongs to one).</summary>
            public string Path = "";
            /// <summary>Why: rays blocked by which collider at what distance, text up vector, scale...</summary>
            public string Detail = "";
            /// <summary>World centre of the readable face.</summary>
            public Vector3 Position;
            /// <summary>World direction the readable face points (towards where a reader would have to stand).</summary>
            public Vector3 Facing;
            /// <summary>Text up vector (world) and half size of the judged face (m) - used for the close-up sheet.</summary>
            public Vector3 Up;
            public float HalfWidth, HalfHeight;
            /// <summary>Further failing texts of the same piece folded into this issue.</summary>
            public int FoldedTexts;
            public Transform Subject;

            public override string ToString()
            {
                var sb = new StringBuilder(200);
                sb.Append(Kind).Append(' ').Append(What);
                if (Text.Length > 0) sb.Append(" '").Append(Text).Append('\'');
                sb.Append(" at ").Append(Fmt(Position)).Append(" facing ").Append(Fmt(Facing)).Append(" | ").Append(Path);
                if (Detail.Length > 0) sb.Append(" | ").Append(Detail);
                if (FoldedTexts > 0) sb.Append(" (+").Append(FoldedTexts).Append(" more text(s) of this piece)");
                return sb.ToString();
            }
        }

        /// <summary>Result of SignProblems: what was looked at and every issue (scene order).</summary>
        public sealed class SignAuditResult
        {
            public int Texts, LightPanels, Judged, Billboards, Moving, PropLabels, OnCharacters, DoubleSidedBacks;
            public readonly List<SignIssue> Issues = new List<SignIssue>();
            /// <summary>CLIPPED texts: worth a look, not counted as issues.</summary>
            public readonly List<SignIssue> Warnings = new List<SignIssue>();

            /// <summary>Issues whose Kind contains 'kind'.</summary>
            public int Count(string kind)
            {
                int n = 0;
                foreach (var i in Issues) if (i.Kind.Contains(kind)) n++;
                return n;
            }

            public string Summary =>
                "texts=" + Texts + " lightPanels=" + LightPanels + " judged=" + Judged + " issues=" + Issues.Count +
                " (faces-wall=" + Count("FACES-WALL") + " buried=" + Count("BURIED") + " upside-down=" + Count("UPSIDE-DOWN") +
                " sideways=" + Count("SIDEWAYS") + " mirrored=" + Count("MIRRORED") + ") warnings=" + Warnings.Count +
                " (clipped) | not judged: billboards=" + Billboards +
                " moving=" + Moving + " propLabels=" + PropLabels + " onCharacters=" + OnCharacters + " doubleSidedBacks=" + DoubleSidedBacks;
        }

        sealed class SignSubject
        {
            public Transform T;            // text transform, or the light panel root
            public TMP_Text Tmp;           // null for light panels
            public Transform Piece;        // EnvKit piece root owning the text (null when none)
            public string PieceKind;       // "Sign", "Poster", ... (null when none)
            public Vector3 C, N, R, U;     // face centre, readable normal, right, up (world)
            public float HalfW, HalfH;
            public float Behind;           // light panels: rays start this far behind the face (root plane - 3 cm)
            public string Hit = "";
            public float HitDist = float.MaxValue;
            public readonly List<string> Kinds = new List<string>(2);
            public string Detail = "";
        }

        static readonly float[] GridSteps = { -0.6f, 0f, 0.6f };
        static readonly RaycastHit[] SignHits = new RaycastHit[32];
        static readonly Collider[] SignOverlaps = new Collider[16];

        // ------------------------------------------------------------------ collection
        /// <summary>World-space texts the sign audit considers: active, non-empty TextMeshPro objects and TMP UGUI texts on
        /// world-space canvases (screen-space UI is not world text).</summary>
        public static List<TMP_Text> WorldTexts()
        {
            var list = new List<TMP_Text>();
            foreach (var t in Object.FindObjectsByType<TMP_Text>())
            {
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(CleanText(t.text, 999))) continue;
                if (t is TextMeshProUGUI)
                {
                    var cv = t.canvas;
                    if (cv == null || cv.renderMode != RenderMode.WorldSpace) continue;
                }
                list.Add(t);
            }
            return list;
        }

        /// <summary>Finds texts whose orientation is driven at runtime: camera-facing billboards (they turn when the camera
        /// moves) and spinning / animated parts (they turn on their own). Moves the main camera far aside for 3 frames
        /// (ShiftersCamera paused), restores it, waits 3 more frames, and adds every text that turned to 'moving'.</summary>
        public static IEnumerator CollectMovingTexts(HashSet<Transform> moving)
        {
            var texts = WorldTexts();
            var r0 = new Quaternion[texts.Count];
            for (int i = 0; i < texts.Count; i++) r0[i] = texts[i].transform.rotation;
            var cam = Camera.main;
            var rig = cam != null ? cam.GetComponent<ShiftersCamera>() : null;
            bool rigWasOn = rig != null && rig.enabled;
            Vector3 p0 = cam != null ? cam.transform.position : Vector3.zero;
            Quaternion q0 = cam != null ? cam.transform.rotation : Quaternion.identity;
            if (cam != null)
            {
                if (rig != null) rig.enabled = false;
                cam.transform.SetPositionAndRotation(p0 + new Vector3(57f, 31f, -43f), Quaternion.Euler(35f, 230f, 0f));
            }
            for (int f = 0; f < 3; f++) yield return null;
            var r1 = new Quaternion[texts.Count];
            for (int i = 0; i < texts.Count; i++) r1[i] = texts[i] != null ? texts[i].transform.rotation : r0[i];
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(p0, q0);
                if (rig != null) rig.enabled = rigWasOn;
            }
            for (int f = 0; f < 3; f++) yield return null;
            for (int i = 0; i < texts.Count; i++)
            {
                if (texts[i] == null) continue;
                var r2 = texts[i].transform.rotation;
                if (Quaternion.Angle(r0[i], r1[i]) > 2f || Quaternion.Angle(r1[i], r2) > 2f) moving.Add(texts[i].transform);
            }
        }

        // ------------------------------------------------------------------ audit
        /// <summary>Generic sign audit of the loaded scenes (or only below 'onlyUnder'). 'moving' = texts left unjudged
        /// (from CollectMovingTexts; null = none). See the file header for what is checked.</summary>
        public static SignAuditResult SignProblems(ICollection<Transform> moving = null, Transform onlyUnder = null, float clearance = SignClearance)
        {
            Physics.SyncTransforms();
            var res = new SignAuditResult();
            var subjects = new List<SignSubject>();
            foreach (var tmp in WorldTexts())
            {
                var t = tmp.transform;
                if (onlyUnder != null && !t.IsChildOf(onlyUnder)) continue;
                res.Texts++;
                if (IsBillboard(t)) { res.Billboards++; continue; }
                if (moving != null && moving.Contains(t)) { res.Moving++; continue; }
                if (t.GetComponentInParent<ShifterCharacter>() != null) { res.OnCharacters++; continue; }
                if (IsOnMovableProp(t)) { res.PropLabels++; continue; }
                var s = TextSubject(tmp);
                JudgeText(s, clearance);
                subjects.Add(s);
            }
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>())
            {
                if (mr == null || mr.name != "Face" || !mr.gameObject.activeInHierarchy) continue;
                var root = mr.transform.parent;
                if (root == null || PieceKindOf(root) != "LightPanel") continue;
                if (onlyUnder != null && !root.IsChildOf(onlyUnder)) continue;
                res.LightPanels++;
                if (IsOnMovableProp(root)) { res.PropLabels++; continue; }
                var s = PanelSubject(root, mr);
                JudgePanel(s, clearance);
                subjects.Add(s);
            }
            res.Judged = subjects.Count;

            // the back face of a double-sided item (its twin on the same small owner points the other way and is fine)
            foreach (var s in subjects)
            {
                if (s.Tmp == null || s.Kinds.Count != 1 || (s.Kinds[0] != "FACES-WALL" && s.Kinds[0] != "BURIED")) continue;
                foreach (var o in subjects)
                {
                    if (o == s || o.Tmp == null || o.Kinds.Count > 0) continue;
                    if (Vector3.Dot(o.N, s.N) > -0.8f || (o.C - s.C).sqrMagnitude > 0.36f || !SameOwner(o, s)) continue;
                    s.Kinds.Clear();
                    res.DoubleSidedBacks++;
                    break;
                }
            }

            var byPiece = new Dictionary<Transform, SignIssue>();
            foreach (var s in subjects)
            {
                bool warnOnly = s.Kinds.Count == 1 && s.Kinds[0] == "CLIPPED";
                if (s.Kinds.Count == 0) continue;
                if (!warnOnly && s.Piece != null && byPiece.TryGetValue(s.Piece, out var same)) { same.FoldedTexts++; continue; }
                var issue = new SignIssue
                {
                    Kind = string.Join("+", s.Kinds),
                    What = s.PieceKind ?? "text",
                    Text = s.Piece != null && s.PieceKind != "LightPanel" ? PieceText(s.Piece) : s.Tmp != null ? CleanText(s.Tmp.text, 48) : "",
                    Path = FullPath(s.Piece != null ? s.Piece : s.T),
                    Detail = s.Detail,
                    Position = s.C,
                    Facing = s.N,
                    Up = s.U,
                    HalfWidth = s.HalfW,
                    HalfHeight = s.HalfH,
                    Subject = s.Piece != null ? s.Piece : s.T,
                };
                if (warnOnly) { res.Warnings.Add(issue); continue; }
                if (s.Piece != null) byPiece[s.Piece] = issue;
                res.Issues.Add(issue);
            }
            return res;
        }

        /// <summary>The texts of an EnvKit piece in hierarchy order (title / subtitle...), for the report.</summary>
        static string PieceText(Transform piece)
        {
            var sb = new StringBuilder();
            foreach (var t in piece.GetComponentsInChildren<TMP_Text>(true))
            {
                var c = CleanText(t.text, 40);
                if (c.Length == 0) continue;
                if (sb.Length > 0) sb.Append(" / ");
                sb.Append(c);
                if (sb.Length > 60) break;
            }
            return sb.ToString();
        }

        static SignSubject TextSubject(TMP_Text tmp)
        {
            var t = tmp.transform;
            var s = new SignSubject { T = t, Tmp = tmp, N = -t.forward, R = t.right, U = t.up };
            Bounds tb = default;
            bool ok = false;
            try { tmp.ForceMeshUpdate(true); tb = tmp.textBounds; ok = tb.size.x > 1e-4f && tb.size.y > 1e-4f; }
            catch (System.Exception) { ok = false; }
            if (ok)
            {
                var ls = t.lossyScale;
                s.C = t.TransformPoint(new Vector3(tb.center.x, tb.center.y, 0f));
                s.HalfW = Mathf.Min(tb.extents.x * Mathf.Abs(ls.x), 6f);
                s.HalfH = Mathf.Min(tb.extents.y * Mathf.Abs(ls.y), 4f);
            }
            else { s.C = t.position; s.HalfW = 0.15f; s.HalfH = 0.08f; }
            s.Piece = OwningPiece(t, out s.PieceKind);
            return s;
        }

        static SignSubject PanelSubject(Transform root, MeshRenderer face)
        {
            var s = new SignSubject { T = root, Piece = root, PieceKind = "LightPanel", N = root.forward, R = root.right, U = root.up };
            var mf = face.GetComponent<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.4f, 0.4f, 0.03f));
            var ft = face.transform;
            var fs = ft.lossyScale;
            s.HalfW = Mathf.Min(b.extents.x * Mathf.Abs(fs.x), 6f);
            s.HalfH = Mathf.Min(b.extents.y * Mathf.Abs(fs.y), 4f);
            s.C = ft.TransformPoint(b.center + new Vector3(0f, 0f, b.extents.z));      // front of the glowing face
            s.Behind = Mathf.Max(0f, Vector3.Dot(s.C - root.position, s.N)) + 0.03f;     // back to the root plane, + 3 cm
            return s;
        }

        static void JudgeText(SignSubject s, float clearance)
        {
            // per sample point: inside geometry 3 cm deep = buried; poking < 3 cm into geometry from its back side
            // (a backwards piece pushed against the wall) or a solid hit within the clearance = front blocked
            int rays = 0, blocked = 0, buried = 0, backBlocked = 0, insideAny = 0;
            bool centreInside = false;
            var own = s.Piece != null ? s.Piece : s.T;
            foreach (float a in GridSteps)
                foreach (float b in GridSteps)
                {
                    var p = s.C + s.R * (a * s.HalfW) + s.U * (b * s.HalfH);
                    rays++;
                    // probe 4 mm in front of the text plane: a text lying flush on a surface is not "inside" it
                    if (InsideSolid(p + s.N * 0.004f, own, out var inside))
                    {
                        Note(s, inside, 0f);
                        insideAny++;
                        if (a == 0f && b == 0f) centreInside = true;
                        var q = p - s.N * 0.03f;
                        if (InsideSolid(q, own, out _)) { buried++; backBlocked++; continue; }
                        blocked++;
                        if (CastSolid(q, -s.N, clearance, own, out _)) backBlocked++;
                        continue;
                    }
                    if (CastSolid(p + s.N * 0.002f, s.N, clearance, own, out var hit)) { blocked++; Note(s, hit.collider, hit.distance); }
                    if (CastSolid(p - s.N * 0.002f, -s.N, clearance, own, out _)) backBlocked++;
                }
            int need = Majority(rays);
            bool backOpen = backBlocked < need;
            string kind = null;
            if (buried >= need) kind = centreInside ? "BURIED" : "CLIPPED";                 // deep inside geometry
            else if (blocked + buried >= need) kind = backOpen ? "FACES-WALL" : (centreInside && buried > blocked ? "BURIED" : "FACES-WALL");
            else if (insideAny >= 2) kind = "CLIPPED";                                        // partly inside geometry
            if (kind != null)
            {
                s.Kinds.Add(kind);
                s.Detail = kind == "FACES-WALL"
                    ? (blocked + buried) + "/" + rays + " rays blocked by '" + s.Hit + "' at " + F2(s.HitDist) + " m" +
                      (backOpen ? "; back side open -> mounted backwards: turn it 180 deg (rot from EnvKit.WallMountFacing / FacingViewer)"
                                : "; back side blocked too (boxed in)")
                    : insideAny + "/" + rays + " points inside '" + s.Hit + "'" +
                      (kind == "BURIED" ? " - invisible: move it out of the geometry (in front of the surface)"
                                        : " (centre clear) - the text runs into geometry: shorten / shrink / move it");
            }
            if (Mathf.Abs(s.N.y) < 0.7f)
            {
                if (s.U.y < -0.3f) AddKind(s, "UPSIDE-DOWN", "text up " + Fmt(s.U));
                else if (s.U.y < 0.3f) AddKind(s, "SIDEWAYS", "text up " + Fmt(s.U));
            }
            if (s.T.localToWorldMatrix.determinant < 0f) AddKind(s, "MIRRORED", "negative scale " + Fmt(s.T.lossyScale));
        }

        static void JudgePanel(SignSubject s, float clearance)
        {
            int rays = 0, blocked = 0;
            float len = s.Behind + clearance;
            foreach (float a in GridSteps)
                foreach (float b in GridSteps)
                {
                    var p = s.C - s.N * s.Behind + s.R * (a * s.HalfW) + s.U * (b * s.HalfH);
                    rays++;
                    if (CastSolid(p, s.N, len, s.T, out var hit)) { blocked++; Note(s, hit.collider, Mathf.Max(0f, hit.distance - s.Behind)); }
                }
            if (blocked >= Majority(rays))
            {
                s.Kinds.Add("FACES-WALL");
                s.Detail = blocked + "/" + rays + " rays from the lit face blocked by '" + s.Hit + "' at " + F2(s.HitDist) +
                           " m -> turn it 180 deg (rot from EnvKit.WallMountFacing / FacingViewer)";
            }
        }

        static int Majority(int rays) => (rays * 5 + 8) / 9;   // 5 of 9

        static void AddKind(SignSubject s, string kind, string why)
        {
            s.Kinds.Add(kind);
            s.Detail = s.Detail.Length > 0 ? s.Detail + "; " + why : why;
        }

        static void Note(SignSubject s, Collider c, float dist)
        {
            if (c == null || dist >= s.HitDist) return;
            s.HitDist = dist;
            s.Hit = FullPath(c.transform, 4) + " (" + c.GetType().Name + ")";
        }

        // ------------------------------------------------------------------ physics helpers
        static bool CastSolid(Vector3 origin, Vector3 dir, float len, Transform own, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, SignHits, len, ~0, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var h = SignHits[i];
                if (h.distance >= bestD || !SolidFor(h.collider, own)) continue;
                best = h; bestD = h.distance; any = true;
            }
            return any;
        }

        static bool InsideSolid(Vector3 p, Transform own, out Collider found)
        {
            found = null;
            int n = Physics.OverlapSphereNonAlloc(p, 0.003f, SignOverlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (SolidFor(SignOverlaps[i], own)) { found = SignOverlaps[i]; return true; }
            return false;
        }

        /// <summary>Solid scenery for the sign audit: enabled non-trigger collider that is not part of 'own' (the judged
        /// piece / text) or the object a label is printed on (own's direct parent), not a character, not a dynamic
        /// rigidbody (props, bots, ragdolls) and not a movable prop.</summary>
        static bool SolidFor(Collider c, Transform own)
        {
            if (c == null || c.isTrigger || !c.enabled) return false;
            var ct = c.transform;
            if (own != null && (ct.IsChildOf(own) || ct == own.parent)) return false;
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return false;
            if (c.GetComponentInParent<ShifterCharacter>() != null) return false;
            if (c.GetComponentInParent<Pushable>() != null || c.GetComponentInParent<Grabbable>() != null) return false;
            return true;
        }

        static bool IsBillboard(Transform t) =>
            t.GetComponentInParent<PuzzleBillboard>() != null || t.GetComponentInParent<PerspectiveLabelChip>() != null ||
            t.GetComponentInParent<ShootHintMarker>() != null || t.GetComponentInParent<FxSystem>() != null;

        static bool IsOnMovableProp(Transform t)
        {
            if (t.GetComponentInParent<Pushable>() != null || t.GetComponentInParent<Grabbable>() != null) return true;
            var rb = t.GetComponentInParent<Rigidbody>();
            return rb != null && !rb.isKinematic;
        }

        /// <summary>Back-to-back twins belong together: the same EnvKit piece, or owners (piece root / text) that share a
        /// parent - a double-sided label, a LogicGate's SymbolBack, two signs mounted back to back.</summary>
        static bool SameOwner(SignSubject a, SignSubject b)
        {
            var oa = a.Piece != null ? a.Piece : a.T;
            var ob = b.Piece != null ? b.Piece : b.T;
            return oa == ob || (oa.parent != null && oa.parent == ob.parent);
        }

        static Transform OwningPiece(Transform t, out string kind)
        {
            kind = null;
            var p = t.parent;
            for (int i = 0; i < 4 && p != null; i++, p = p.parent)
            {
                kind = PieceKindOf(p);
                if (kind != null) return p;
            }
            return null;
        }

        /// <summary>EnvKit mounted-piece type of a root ("Sign", "Poster", "LightPanel", "Whiteboard", "HoloPanel",
        /// "SparkingPanel") recognised by name + structure, or null.</summary>
        public static string PieceKindOf(Transform t)
        {
            if (t == null) return null;
            string n = t.name;
            if (n.StartsWith("Sign_", System.StringComparison.Ordinal)) return t.Find("Panel") != null && t.Find("Frame") != null ? "Sign" : null;
            switch (n)
            {
                case "Poster": return t.Find("Paper") != null && t.Find("Backing") != null ? "Poster" : null;
                case "LightPanel": return t.Find("Face") != null && t.Find("Frame") != null ? "LightPanel" : null;
                case "Whiteboard": return t.Find("Mesh") != null ? "Whiteboard" : null;
                case "HoloPanel": return t.Find("Holo") != null ? "HoloPanel" : null;
                case "SparkingPanel": return "SparkingPanel";
            }
            return null;
        }

        // ------------------------------------------------------------------ text helpers
        /// <summary>Text without rich-text tags, line breaks as '/', cut to maxLen.</summary>
        public static string CleanText(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool tag = false;
            foreach (char ch in s)
            {
                if (ch == '<') { tag = true; continue; }
                if (ch == '>' && tag) { tag = false; continue; }
                if (!tag) sb.Append(ch == '\n' || ch == '\r' ? '/' : ch);
            }
            var r = sb.ToString().Trim();
            return r.Length > maxLen ? r.Substring(0, maxLen) + "..." : r;
        }

        /// <summary>"scene:Root/A/B/Object" (at most maxDepth names, '...' when cut).</summary>
        public static string FullPath(Transform t, int maxDepth = 14)
        {
            if (t == null) return "(null)";
            var parts = new List<string>();
            var c = t;
            for (; c != null && parts.Count < maxDepth; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            string p = string.Join("/", parts);
            if (c != null) p = ".../" + p;
            return (t.gameObject.scene.IsValid() ? t.gameObject.scene.name + ":" : "") + p;
        }

        static string F2(float f) => f.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        public static string Fmt(Vector3 v) =>
            "(" + v.x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")";

        // ------------------------------------------------------------------ close-up sheet
        const int SheetTileW = 320, SheetTileH = 180, SheetCols = 4, SheetRows = 4;

        /// <summary>Renders numbered close-ups (4x4 tiles per sheet, the yellow number = the issue index) of up to 'max'
        /// issues: FACES-WALL issues from their back (open) side - the blank back a player sees - the others from the front
        /// (a BURIED text then shows the geometry hiding it). Editor: saves
        /// &lt;shotsDir&gt;/&lt;name&gt;_N.png and adds it to the DevPlay result.</summary>
        public static IEnumerator SignIssueSheet(DevPlayContext ctx, List<SignIssue> issues, string name, int max = 32)
        {
            if (issues == null || issues.Count == 0) yield break;
            var go = new GameObject("[SignAuditCam]");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) { cam.CopyFrom(main); cam.targetTexture = null; }
            cam.enabled = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            PostFx.EnsureCameraPost(cam);
            var rt = RenderTexture.GetTemporary(SheetTileW, SheetTileH, 24, RenderTextureFormat.ARGB32);
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), aspect = SheetTileW / (float)SheetTileH;
            Texture2D sheet = null;
            int sheetNo = 0, inSheet = 0, count = Mathf.Min(max, issues.Count);
            for (int i = 0; i < count; i++)
            {
                var it = issues[i];
                if (sheet == null)
                {
                    sheet = new Texture2D(SheetTileW * SheetCols, SheetTileH * SheetRows, TextureFormat.RGB24, false);
                    var px = new Color32[sheet.width * sheet.height];
                    for (int k = 0; k < px.Length; k++) px[k] = new Color32(20, 20, 26, 255);
                    sheet.SetPixels32(px);
                    inSheet = 0;
                }
                float halfH = Mathf.Max(it.HalfHeight, it.HalfWidth / aspect) * 1.6f + 0.15f;
                float dist = Mathf.Clamp(halfH / tanHalf, 0.7f, 12f);
                var side = it.Kind.StartsWith("FACES-WALL") ? -it.Facing : it.Facing;   // a wall-facing piece: show its blank back
                var pos = it.Position + side * dist + Vector3.up * dist * 0.12f;
                var up = Mathf.Abs(Vector3.Dot(side, Vector3.up)) > 0.95f ? (it.Up.sqrMagnitude > 0.1f ? it.Up : Vector3.forward) : Vector3.up;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(it.Position - pos, up));
                int col = inSheet % SheetCols, row = inSheet / SheetCols;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, SheetTileW, SheetTileH), col * SheetTileW, (SheetRows - 1 - row) * SheetTileH);
                RenderTexture.active = prev;
                DrawIndex(sheet, col * SheetTileW + 2, (SheetRows - row) * SheetTileH - 3, i, 3);
                inSheet++;
                if (inSheet == SheetCols * SheetRows || i == count - 1)
                {
                    sheet.Apply();
                    SaveSheet(ctx, sheet, name + "_" + sheetNo);
                    Object.Destroy(sheet);
                    sheet = null;
                    sheetNo++;
                    yield return null;
                }
            }
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(go);
        }

        static void SaveSheet(DevPlayContext ctx, Texture2D tex, string name)
        {
#if UNITY_EDITOR
            if (ctx?.Config == null || string.IsNullOrEmpty(ctx.Config.shotsDir)) return;
            try
            {
                System.IO.Directory.CreateDirectory(ctx.Config.shotsDir);
                string path = System.IO.Path.Combine(ctx.Config.shotsDir, name + ".png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                ctx.Result?.shots.Add(path);
                ctx.Log("[SMOKE] sign issue sheet " + path);
            }
            catch (System.Exception e) { ctx.Log("[SMOKE] could not save sign sheet: " + e.Message); }
#endif
        }

        // 3x5 digit glyphs, rows top to bottom
        static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
        };

        /// <summary>Yellow index on a black box; (x, yTop) = top-left corner in texture pixels (y up).</summary>
        static void DrawIndex(Texture2D tex, int x, int yTop, int n, int s)
        {
            string str = n.ToString();
            int w = str.Length * 4 * s + s, h = 7 * s;
            for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) SetPx(tex, x + i, yTop - j, Color.black);
            for (int k = 0; k < str.Length; k++)
            {
                var g = Digits[str[k] - '0'];
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                        if (g[row * 3 + col] == '1')
                            for (int a = 0; a < s; a++)
                                for (int b = 0; b < s; b++)
                                    SetPx(tex, x + s + k * 4 * s + col * s + a, yTop - s - row * s - b, new Color(1f, 0.85f, 0.1f));
            }
        }

        static void SetPx(Texture2D t, int x, int y, Color c) { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c); }
    }
}
