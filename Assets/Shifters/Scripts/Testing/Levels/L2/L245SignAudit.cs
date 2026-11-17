// =====================================================================================
//  SHIFTERS - L245SignAudit.cs   [LEVELS 2/4/5 - visual QA; lives with the L2 tests, self-contained]
//  Visual audit of the built scenes L2_WeightProblem, L4_IQTest and L5_TotalChaos.
//  Every world-space text under the level root (EnvKit signs / posters / holo panels / whiteboards, ctx labels,
//  IQ clues, kit readouts) is checked from the level ROUTE (walk-surface polylines below, sampled every metre at
//  eye and camera height):
//    BACKWARDS  only the back of the text can be seen from the route (faces away / into a wall)
//    UNSEEN     neither side can be seen from the route (buried, boxed in, or off-route)
//    WALL       something solid sits right in front of the readable face
//    CLIPS      the text rectangle intersects solid geometry
//    MIRRORED / TILTED (vertical text not upright) / FLOOR-INVERTED (floor text upside down from the route)
//  Texts under a PuzzleBillboard (camera-facing displays) are listed but not judged.
//  Output (shots dir): <tag>_texts_N.png = numbered close-ups (4x4 tiles, yellow number = log index '#'),
//                      <tag>_route_N.png = gameplay-camera views along the main route (2x2 tiles).
//    devplay.sh <proj> l245_audit -devScene Assets/Shifters/Scenes/L2_WeightProblem.unity
//               -devSetup Shifters.L245SignAudit.AuditL2L4L5 -devSeconds 1     (all three levels, one launch)
//    devplay.sh <proj> l2_audit -devScene <level scene> -devSetup Shifters.L245SignAudit.AuditActive
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class L245SignAudit
    {
        // ------------------------------------------------------------------ routes (walk surface; [0] = main route)
        static readonly Vector3[][] L2Routes =
        {
            new[]
            {
                V(0, 0, 0), V(0, 0, 8), V(-1.2f, 0, 12), V(-1.2f, 0, 20.2f), V(2, 0, 25.2f), V(2, 0, 29.2f), V(2, 0, 33),
                V(0, 0, 36.4f), V(0, 0, 39.4f), V(0, 6, 39.4f), V(-0.2f, 6, 41.3f), V(1.2f, 6, 45.2f), V(2.5f, 6, 46.8f),
                V(2.5f, 6, 53.2f), V(2.5f, 6, 58), V(2.5f, 6, 63.6f), V(0, 6, 67), V(-1.5f, 6, 74), V(-1.5f, 6, 79),
                V(-1.5f, 12, 79), V(-2.2f, 12, 82.6f), V(0, 12, 85), V(0, 12, 88.4f), V(0, 12, 93.6f), V(0, 12, 97),
                V(-3.5f, 12, 104), V(2.5f, 12, 107.5f), V(2.5f, 12, 112), V(2.5f, 12, 117), V(2.5f, 12, 122), V(2.5f, 12, 127)
            },
            new[] { V(-5, 0, -3), V(5, 0, -3) },
            new[] { V(4.5f, 6, 71), V(4.5f, 6, 75.6f) },
            new[] { V(-3.4f, 6, 65.2f), V(-6.5f, 6, 65), V(-6.5f, 6, 67.7f), V(-6.5f, 12, 76.7f), V(-6.5f, 12, 80.2f), V(-6.4f, 12, 86.2f) },
            new[] { V(4.6f, 12, 83.2f), V(4.6f, 12, 86.15f), V(4.6f, 14.5f, 88.1f), V(4.6f, 14.5f, 89f) },
            new[] { V(4.5f, -1.6f, 79), V(8, -1.6f, 80), V(9.75f, -1.6f, 84.5f), V(9.75f, 6, 73.25f), V(9.75f, 6, 69.5f) },
            new[] { V(-5.5f, 6, 44), V(-5.5f, 6, 51.4f), V(-5.5f, 6, 58.6f), V(-5.5f, 6, 61.5f), V(-3, 6, 63) },
        };

        static readonly Vector3[][] L4Routes =
        {
            new[]
            {
                V(0, 0, -3.5f), V(0, 0, 4), V(0, 0, 9), V(0, 0, 11.5f), V(0, 0, 17), V(0, 0, 20), V(0, 0, 24.6f), V(0, 0, 26),
                V(0, 0, 30), V(10, 0, 30), V(10, 0, 42), V(4, 0, 42), V(1.6f, 0, 42), V(-4, 0, 45), V(-4, 0, 49), V(-4, 0, 52),
                V(-4, 0, 58), V(-4, 0, 64), V(-4, 0, 68.5f), V(-4, 0, 71), V(-4, 0, 78.5f), V(-4, 0, 83.5f), V(-4, 0, 89),
                V(-4, 0, 94), V(-4, 0, 97.9f), V(-4, 0, 104), V(-4, 0, 106.5f), V(-4, 0, 111.5f), V(-4, 0, 115), V(-4, 0, 124),
                V(-4, 0, 132.2f), V(-4, 0, 145), V(-4, 0, 157.5f), V(-4, 0, 161.5f), V(-4, 0, 169.5f), V(-4, 0, 176), V(-4, 0, 178)
            },
            new[] { V(-6, 0, -1), V(6, 0, -1) },
            new[] { V(5.2f, 0, 2.7f), V(5, 0, 9.35f) },
            new[] { V(5, 2.5f, 11.4f), V(8.7f, 2.5f, 12.2f), V(3.6f, 2.5f, 11.6f) },
            new[] { V(-9.3f, 0, 52.5f), V(-9.3f, 0, 63.5f) },
            new[] { V(1.3f, 0, 52.5f), V(1.3f, 0, 63.5f) },
            new[] { V(1.5f, 0, 58), V(6.4f, 0, 58) },
            new[] { V(-9.3f, 0, 85), V(-9.3f, 0, 96) },
            new[] { V(1.3f, 0, 85), V(1.3f, 0, 96) },
            new[] { V(-6.9f, 0, 132.8f), V(-6.9f, 0, 140) },
            new[] { V(-4, 0, 160.2f), V(-1.2f, 0, 159.8f) },
            new[] { V(-4, 0, 163), V(-8.6f, 0, 165.2f) },
        };

        static readonly Vector3[][] L5Routes =
        {
            new[]
            {
                V(0, 0, 1), V(0, 0, 9.6f), V(0, 0, 13.2f), V(0, 0, 21.2f), V(0, 0, 26.4f), V(0, 0, 31.5f), V(0, 0, 39.5f),
                V(-3.5f, 0, 38.6f), V(-6.25f, 0, 38.6f), V(-6.25f, 0, 42.6f), V(-6.25f, 2, 50.6f), V(-3, 2, 57.5f), V(0, 2, 62),
                V(0, 2, 66.6f), V(0, 2, 82.6f), V(0, 2, 98.5f), V(-1, 2, 105.8f), V(-10.5f, 2, 110), V(-10.5f, 2, 114.5f),
                V(-10.5f, 2, 130.6f), V(-7.4f, 0.4f, 131.4f), V(-4, 0.4f, 138.6f), V(0, 0.4f, 141.5f), V(0, 0.4f, 149),
                V(0, 0.4f, 158), V(-3, 0.4f, 164.6f), V(-3, 0.4f, 168), V(-3, 3.4f, 168), V(-3, 3.4f, 172.4f), V(-4, 3.4f, 176.5f),
                V(-2, 3.4f, 184), V(-2, 3.4f, 188.6f), V(0, 3.4f, 196.5f), V(0, 3.4f, 201.5f)
            },
            new[] { V(-5, 0, 0), V(5, 0, 0) },
            new[] { V(7.5f, 2, 59.5f), V(7.5f, 2, 66.2f), V(7.5f, 2, 88.6f), V(7.5f, 2, 99.5f) },
            new[] { V(-7.4f, 0.4f, 131.4f), V(3, 0.4f, 129), V(8, 0.4f, 125), V(3, 0.4f, 120) },
            new[] { V(3, 0.4f, 161.4f), V(3, 0.4f, 166) },
            new[] { V(6.6f, -2.6f, 169.8f), V(7, -2.6f, 172.6f), V(7, 1.4f, 179.4f), V(7, 1.4f, 184.6f), V(7, 5.4f, 191.4f), V(7, 5.4f, 195.2f), V(6, 8.3f, 202.4f), V(0.3f, 8.3f, 202.4f) },
            new[] { V(-4, 3.4f, 176.5f), V(3.5f, 3.4f, 181) },
        };

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ------------------------------------------------------------------ entry points
        /// <summary>Audits L2 (the -devScene), then loads and audits L4 and L5 - one Unity launch for all three.</summary>
        public static IEnumerator AuditL2L4L5(DevPlayContext ctx)
        {
            var hold = BotDriver.Ensure();
            hold.HoldHarness(ctx);   // three levels can outlast -devSeconds + 120 s
            try
            {
                yield return AuditActive(ctx);
                yield return LoadLevel("L4_IQTest");
                yield return AuditActive(ctx);
                yield return LoadLevel("L5_TotalChaos");
                yield return AuditActive(ctx);
            }
            finally { hold.ReleaseHarness(ctx); }
        }

        /// <summary>Audits whichever of L2/L4/L5 is the active scene.</summary>
        public static IEnumerator AuditActive(DevPlayContext ctx)
        {
            string scene = SceneManager.GetActiveScene().name;
            Vector3[][] routes = scene.StartsWith("L2") ? L2Routes : scene.StartsWith("L4") ? L4Routes : scene.StartsWith("L5") ? L5Routes : null;
            if (routes == null) { ctx.Log("sign audit: no route for scene " + scene); yield break; }
            yield return Audit(ctx, scene.Substring(0, 2), scene, routes);
        }

#if UNITY_EDITOR
        /// <summary>Batch entry: rebuilds the level scenes listed in -devLevels (catalog indices, e.g. 1,3,4) with
        /// ShiftersBuilder.BuildLevelIndex, then hands over to DevPlayRunner.Play with the usual DevPlay arguments - one
        /// Unity launch for "build + audit" instead of four:
        ///   unity_batch.sh &lt;proj&gt; l245 -executeMethod Shifters.L245SignAudit.BuildThenPlay -devLevels 1,3,4
        ///       -devScene Assets/Shifters/Scenes/L2_WeightProblem.unity -devSetup Shifters.L245SignAudit.AuditL2L4L5
        ///       -devSeconds 1 -devShots &lt;dir&gt; -devResult &lt;dir&gt;/result.json</summary>
        public static void BuildThenPlay()
        {
            try
            {
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                var build = System.Type.GetType("Shifters.EditorTools.ShiftersBuilder, Assembly-CSharp-Editor")?.GetMethod("BuildLevelIndex", flags);
                var play = System.Type.GetType("Shifters.EditorTools.DevPlayRunner, Assembly-CSharp-Editor")?.GetMethod("Play", flags);
                if (build == null || play == null) throw new System.Exception("ShiftersBuilder.BuildLevelIndex or DevPlayRunner.Play not found");
                var args = System.Environment.GetCommandLineArgs();
                string levels = "";
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-devLevels") levels = args[i + 1];
                foreach (var s in levels.Split(','))
                {
                    if (!int.TryParse(s.Trim(), out int index)) continue;
                    bool ok = (bool)build.Invoke(null, new object[] { index });
                    Debug.Log("[L245] BuildLevelIndex(" + index + "): " + (ok ? "saved" : "FAILED"));
                    if (!ok) { UnityEditor.EditorApplication.Exit(3); return; }
                }
                play.Invoke(null, null);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                UnityEditor.EditorApplication.Exit(4);
            }
        }
#endif

        static IEnumerator LoadLevel(string sceneName)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Shifters/Scenes/" + sceneName + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(sceneName);
#endif
            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ audit
        sealed class Item
        {
            public TextMeshPro Tmp;
            public string Text, Path, WallHit, Clip, Skip;   // Skip = reason the text is listed but not judged
            public Vector3 C, N, Up;
            public float W, H;
            public bool Floor, Billboard, Mirrored, Tilted;
            public int Front, Back, Upright, Inverted;
            public bool Judged => !Billboard && Skip == null;
            public bool Bad => Judged && (Backwards || Unseen || WallHit != null || Mirrored || Tilted || FloorInverted);
            public bool Backwards => !Floor && Front == 0 && Back > 0;
            public bool Unseen => Front == 0 && Back == 0;
            public bool FloorInverted => Floor && Upright == 0 && Inverted > 0;
        }

        static IEnumerator Audit(DevPlayContext ctx, string tag, string scene, Vector3[][] routes)
        {
            yield return TeamBot.WaitForTeam(ctx);
            yield return ctx.Wait(6f);
            foreach (var cv in Object.FindObjectsByType<Canvas>()) cv.enabled = false;           // clean shots: no HUD/popups
            // reveal every perspective-only thing (IQ clues, speed routes, shoot hints). PerspectiveManager re-applies the
            // active character's mask on team/state events, so keep forcing it every frame until the pop-in has settled.
            for (int f = 0; f < 90; f++) { ForceReveal(); yield return null; }
            ForceReveal();

            var root = GameObject.Find(LevelBuildContext.RootName(LevelCatalog.Get(LevelCatalog.IndexOfScene(scene))));
            if (root == null) { ctx.Check(false, tag + " sign audit: level root found"); yield break; }
            var samples = Sample(routes, 1f);
            var items = Collect(root.transform, samples);

            int backwards = 0, unseen = 0, wall = 0, clip = 0, mirrored = 0, tilted = 0, floorInv = 0, notJudged = 0;
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.Judged) notJudged++;
                else
                {
                    if (it.Backwards) backwards++;
                    if (it.Unseen) unseen++;
                    if (it.WallHit != null) wall++;
                    if (it.Clip != null) clip++;
                    if (it.Mirrored) mirrored++;
                    if (it.Tilted) tilted++;
                    if (it.FloorInverted) floorInv++;
                }
                sb.Length = 0;
                sb.Append(tag).Append(" #").Append(i).Append(' ').Append(it.Bad ? "BAD " : "ok  ");
                if (it.Billboard) sb.Append("[billboard] ");
                if (it.Skip != null) sb.Append("[").Append(it.Skip).Append("] ");
                if (it.Backwards) sb.Append("BACKWARDS ");
                if (it.Unseen) sb.Append("UNSEEN ");
                if (it.WallHit != null) sb.Append("WALL(").Append(it.WallHit).Append(") ");
                if (it.Clip != null) sb.Append("CLIPS(").Append(it.Clip).Append(") ");
                if (it.Mirrored) sb.Append("MIRRORED ");
                if (it.Tilted) sb.Append("TILTED ");
                if (it.FloorInverted) sb.Append("FLOOR-INVERTED ");
                if (!it.Bad && it.Judged && !it.Floor && it.Back > 3 * it.Front && it.Front < 12) sb.Append("(mostly-back: review) ");
                sb.Append("'").Append(it.Text).Append("' at ").Append(F(it.C)).Append(" n=").Append(F(it.N))
                  .Append(it.Floor ? " floor up=" + F(it.Up) + " upright=" + it.Upright + " inverted=" + it.Inverted : "")
                  .Append(" front=").Append(it.Front).Append(" back=").Append(it.Back).Append(" | ").Append(it.Path);
                ctx.Log(sb.ToString());
            }
            ctx.Log(tag + " SIGN AUDIT: texts=" + items.Count + " not-judged=" + notJudged + " backwards=" + backwards + " unseen=" + unseen +
                    " wall=" + wall + " clips=" + clip + " mirrored=" + mirrored + " tilted=" + tilted + " floorInverted=" + floorInv);
            ctx.Check(backwards == 0, tag + ": no text is backwards from the route (" + backwards + ")");
            ctx.Check(wall == 0, tag + ": no text faces a wall (" + wall + ")");
            ctx.Check(mirrored == 0 && tilted == 0 && floorInv == 0, tag + ": no mirrored / tilted / upside-down text (" + (mirrored + tilted + floorInv) + ")");
            ctx.Check(unseen == 0, tag + ": every text is visible from the route (" + unseen + ")");

            yield return CloseUps(ctx, tag, items);
            yield return RouteViews(ctx, tag, routes[0], 9f);
        }

        static void ForceReveal()
        {
            var all = Revealable.All;
            for (int i = all.Count - 1; i >= 0; i--) if (all[i] != null) all[i].SetRevealed(true);
        }

        // ------------------------------------------------------------------ analysis
        /// <summary>Route samples: eye height on the route, plus where the follow camera sits for someone walking it
        /// (Tuning.CamDistance behind along the walking direction, ~3 m up).</summary>
        static List<Vector3> Sample(Vector3[][] routes, float step)
        {
            var list = new List<Vector3>();
            foreach (var poly in routes)
            {
                var dir = Vector3.forward;
                for (int i = 0; i + 1 < poly.Length; i++)
                {
                    var a = poly[i]; var b = poly[i + 1];
                    var flat = b - a; flat.y = 0f;
                    if (flat.sqrMagnitude > 0.01f) dir = flat.normalized;   // vertical rides keep the last heading
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / step));
                    for (int k = 0; k <= n; k++)
                    {
                        var p = Vector3.Lerp(a, b, k / (float)n);
                        list.Add(p + Vector3.up * 1.6f);                                        // eye
                        list.Add(p + Vector3.up * 3.0f - dir * (Tuning.CamDistance * 0.96f));    // follow camera
                    }
                }
            }
            return list;
        }

        static List<Item> Collect(Transform root, List<Vector3> samples)
        {
            var items = new List<Item>();
            foreach (var tmp in root.GetComponentsInChildren<TextMeshPro>(false))
            {
                var rend = tmp.GetComponent<Renderer>();
                if (!tmp.enabled || rend == null || !rend.enabled || string.IsNullOrWhiteSpace(tmp.text)) continue;
                tmp.ForceMeshUpdate();
                var t = tmp.transform;
                var tb = tmp.textBounds;
                var ls = t.lossyScale;
                bool hasBounds = tb.size.x > 0.001f && tb.size.y > 0.001f;
                var it = new Item
                {
                    Tmp = tmp,
                    Text = Clean(tmp.text),
                    Path = PathOf(t, root),
                    N = -t.forward,
                    Up = t.up,
                    C = hasBounds ? t.TransformPoint(tb.center) : rend.bounds.center,
                    W = Mathf.Max(0.15f, (hasBounds ? tb.size.x : tmp.rectTransform.rect.width) * Mathf.Abs(ls.x)),
                    H = Mathf.Max(0.1f, (hasBounds ? tb.size.y : tmp.rectTransform.rect.height) * Mathf.Abs(ls.y)),
                    Billboard = tmp.GetComponentInParent<PuzzleBillboard>() != null,
                    Mirrored = t.localToWorldMatrix.determinant < 0f,
                };
                it.Floor = it.N.y > 0.7f;
                // listed, not judged: labels printed on every face of a movable prop, the kit holding cell's interior
                // notice (read by captured interns), and the distant skyline signs of the void backdrop
                if (tmp.GetComponentInParent<Pushable>() != null) it.Skip = "prop label";
                else if (it.Path.Contains("InsideSign")) it.Skip = "cell interior";
                else if (NearestDistance(it.C, samples) > 40f) it.Skip = "distant backdrop";
                it.Tilted = !it.Floor && Mathf.Abs(it.N.y) < 0.7f && it.Up.y < 0.3f;
                Judge(it, samples);
                items.Add(it);
            }
            return items;
        }

        static void Judge(Item it, List<Vector3> samples)
        {
            // something solid directly in front of the readable face?
            if (Physics.Raycast(it.C + it.N * 0.01f, it.N, out var hit, 0.3f, ~0, QueryTriggerInteraction.Ignore) && Solid(hit.collider))
                it.WallHit = hit.collider.name + "@" + hit.distance.ToString("0.00");
            // text rectangle intersecting geometry?
            var half = new Vector3(it.W * 0.42f, it.H * 0.42f, 0.004f);
            foreach (var c in Physics.OverlapBox(it.C + it.N * 0.006f, half, it.Tmp.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (Solid(c)) { it.Clip = c.name; break; }

            var flatUp = new Vector3(it.Up.x, 0f, it.Up.z);
            foreach (var p in samples)
            {
                var d = p - it.C;
                float dist = d.magnitude;
                if (dist < 0.6f || dist > 30f) continue;
                var dir = d / dist;
                float cos = Vector3.Dot(it.N, dir);
                if (cos > -0.34f && cos < 0.34f) continue;           // too grazing to read either side
                if (Blocked(it.C + dir * 0.06f, p)) continue;
                if (cos > 0f) it.Front++; else it.Back++;
                if (it.Floor && cos > 0f && dist < 14f && flatUp.sqrMagnitude > 0.01f)
                {
                    var toText = it.C - p; toText.y = 0f;
                    if (toText.sqrMagnitude < 0.01f) continue;
                    float up = Vector3.Dot(flatUp.normalized, toText.normalized);
                    if (up > 0.3f) it.Upright++; else if (up < -0.3f) it.Inverted++;
                }
            }
        }

        static float NearestDistance(Vector3 p, List<Vector3> samples)
        {
            float best = float.MaxValue;
            foreach (var s in samples) best = Mathf.Min(best, (s - p).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        static bool Solid(Collider c)
        {
            if (c == null || c.isTrigger) return false;
            if (c.GetComponentInParent<ShifterCharacter>() != null) return false;
            var rb = c.attachedRigidbody;
            return rb == null || rb.isKinematic;
        }

        static bool Blocked(Vector3 from, Vector3 to)
        {
            var d = to - from;
            float len = d.magnitude;
            if (len < 1e-3f) return false;
            foreach (var h in Physics.RaycastAll(from, d / len, len, ~0, QueryTriggerInteraction.Ignore))
                if (Solid(h.collider)) return true;
            return false;
        }

        // ------------------------------------------------------------------ pictures
        const int TileW = 320, TileH = 180, Cols = 4, Rows = 4;

        static IEnumerator CloseUps(DevPlayContext ctx, string tag, List<Item> items)
        {
            var cam = MakeCam(40f);
            var rt = RenderTexture.GetTemporary(TileW, TileH, 24, RenderTextureFormat.ARGB32);
            Texture2D sheet = null;
            int sheetNo = 0, inSheet = 0;
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), aspect = TileW / (float)TileH;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (sheet == null) { sheet = new Texture2D(TileW * Cols, TileH * Rows, TextureFormat.RGB24, false); Fill(sheet, new Color(0.08f, 0.08f, 0.1f)); inSheet = 0; ForceReveal(); }
                float halfH = Mathf.Max(it.H * 0.5f, it.W * 0.5f / aspect) * 1.45f + 0.12f;
                float dist = Mathf.Clamp(halfH / tanHalf, 0.6f, 14f);
                Vector3 pos;
                if (it.Floor)
                {
                    var flatUp = new Vector3(it.Up.x, 0f, it.Up.z).normalized;
                    pos = it.C + Vector3.up * dist * 0.85f - flatUp * dist * 0.55f;
                }
                else pos = it.C + it.N * dist + Vector3.up * dist * 0.06f;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(it.C - pos, Vector3.up));
                int col = inSheet % Cols, row = inSheet / Cols;
                RenderTile(cam, rt, sheet, col * TileW, (Rows - 1 - row) * TileH);
                DrawNumber(sheet, col * TileW + 2, (Rows - row) * TileH - 3, i, 3);
                inSheet++;
                if (inSheet == Cols * Rows || i == items.Count - 1)
                {
                    sheet.Apply();
                    Save(ctx, sheet, tag + "_texts_" + sheetNo);
                    Object.Destroy(sheet);
                    sheet = null;
                    sheetNo++;
                    yield return null;
                }
            }
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(cam.gameObject);
        }

        /// <summary>Follow-camera views (Tuning.CamDistance behind, 15 deg pitch, shoulder offset) every 'spacing' metres.</summary>
        static IEnumerator RouteViews(DevPlayContext ctx, string tag, Vector3[] route, float spacing)
        {
            const int w = 640, h = 360;
            var cam = MakeCam(Tuning.CamDefaultFov);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            Texture2D sheet = null;
            int sheetNo = 0, inSheet = 0, view = 0;
            float carry = 0f;
            var points = new List<(Vector3 p, Vector3 f)>();
            for (int i = 0; i + 1 < route.Length; i++)
            {
                var a = route[i]; var b = route[i + 1];
                var f = b - a; f.y = 0f;
                if (f.sqrMagnitude < 0.01f) continue;            // vertical rides: no new view
                f.Normalize();
                float len = Vector3.Distance(a, b);
                for (float s = carry; s < len; s += spacing) points.Add((Vector3.Lerp(a, b, s / len), f));
                carry = Mathf.Repeat(carry - len, spacing);
            }
            foreach (var (p, f) in points)
            {
                if (sheet == null) { sheet = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false); inSheet = 0; ForceReveal(); }
                var right = Vector3.Cross(Vector3.up, f).normalized;
                var pivot = p + Vector3.up * 1.6f;
                var look = Quaternion.AngleAxis(15f, right) * f;
                var want = pivot - look * Tuning.CamDistance + right * 0.6f;
                var dir = want - pivot;
                if (Physics.SphereCast(pivot, 0.25f, dir.normalized, out var hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore) && Solid(hit.collider))
                    want = pivot + dir.normalized * Mathf.Max(0.5f, hit.distance - 0.1f);
                cam.transform.SetPositionAndRotation(want, Quaternion.LookRotation(look, Vector3.up));
                int col = inSheet % 2, row = inSheet / 2;
                RenderTile(cam, rt, sheet, col * w, (1 - row) * h);
                DrawNumber(sheet, col * w + 2, (2 - row) * h - 3, view, 3);
                ctx.Log(tag + " route view " + view + " at " + F(p) + " facing " + F(f));
                view++; inSheet++;
                if (inSheet == 4)
                {
                    sheet.Apply(); Save(ctx, sheet, tag + "_route_" + sheetNo); Object.Destroy(sheet); sheet = null; sheetNo++;
                    yield return null;
                }
            }
            if (sheet != null) { sheet.Apply(); Save(ctx, sheet, tag + "_route_" + sheetNo); Object.Destroy(sheet); }
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(cam.gameObject);
        }

        static Camera MakeCam(float fov)
        {
            var go = new GameObject("[L245AuditCam]");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) { cam.CopyFrom(main); cam.targetTexture = null; }
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            PostFx.EnsureCameraPost(cam);
            return cam;
        }

        static void RenderTile(Camera cam, RenderTexture rt, Texture2D sheet, int x, int y)
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, rt.width, rt.height), x, y);
            RenderTexture.active = prev;
        }

        static void Fill(Texture2D tex, Color c)
        {
            var px = new Color[tex.width * tex.height];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
        }

        // 3x5 digits, rows top to bottom
        static readonly string[] Glyphs =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
        };

        /// <summary>Yellow index number on a black box; (x, yTop) = top-left corner in texture pixels (y up).</summary>
        static void DrawNumber(Texture2D tex, int x, int yTop, int n, int s)
        {
            string str = n.ToString();
            int w = str.Length * 4 * s + s, h = 7 * s;
            for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) Px(tex, x + i, yTop - j, Color.black);
            for (int k = 0; k < str.Length; k++)
            {
                var g = Glyphs[str[k] - '0'];
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                        if (g[row * 3 + col] == '1')
                            for (int a = 0; a < s; a++)
                                for (int b = 0; b < s; b++)
                                    Px(tex, x + s + k * 4 * s + col * s + a, yTop - s - row * s - b, new Color(1f, 0.85f, 0.1f));
            }
        }

        static void Px(Texture2D t, int x, int y, Color c) { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c); }

        static void Save(DevPlayContext ctx, Texture2D tex, string name)
        {
#if UNITY_EDITOR
            if (ctx?.Config == null || string.IsNullOrEmpty(ctx.Config.shotsDir)) return;
            System.IO.Directory.CreateDirectory(ctx.Config.shotsDir);
            string path = System.IO.Path.Combine(ctx.Config.shotsDir, name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            ctx.Result?.shots.Add(path);
            ctx.Log("sheet " + path);
#endif
        }

        // ------------------------------------------------------------------ text helpers
        static string Clean(string s)
        {
            var sb = new StringBuilder();
            bool tag = false;
            foreach (char ch in s)
            {
                if (ch == '<') { tag = true; continue; }
                if (ch == '>' && tag) { tag = false; continue; }
                if (!tag) sb.Append(ch == '\n' ? '/' : ch);
            }
            var r = sb.ToString();
            return r.Length > 48 ? r.Substring(0, 48) + "..." : r;
        }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string F(Vector3 v) => "(" + v.x.ToString("0.0") + "," + v.y.ToString("0.0") + "," + v.z.ToString("0.0") + ")";
    }
}
