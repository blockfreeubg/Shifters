// =====================================================================================
//  SHIFTERS - StoryLayoutUtil.cs   [owned by STORY_KIT]
//  Placement helpers for StoryLayout_L1..L5: mark lookup, ground probes, free-space checks (Physics.CheckBox against
//  the level's solid colliders, triggers ignored) and candidate searches, so overlay pieces never sit inside walls,
//  never float over the void (except billboards / void statues) and face the player's approach. All pieces go under
//  the overlay root (world-aligned at the origin, so local == world).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class StoryLayoutUtil
    {
        public readonly Transform Root;
        public int Placed, Skipped;
        public string SkipLog = "";
        /// <summary>Every placed piece with a label (tests screenshot each one).</summary>
        public readonly List<KeyValuePair<string, GameObject>> Pieces = new List<KeyValuePair<string, GameObject>>();
        public static readonly List<KeyValuePair<string, GameObject>> LastPieces = new List<KeyValuePair<string, GameObject>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => LastPieces.Clear();

        const int Solid = ~(1 << 2);   // everything but Ignore Raycast

        public StoryLayoutUtil(Transform root) { Root = root; LastPieces.Clear(); }

        public GameObject Add(string label, GameObject go)
        {
            if (go == null) { Skip(label); return null; }
            Placed++;
            var kv = new KeyValuePair<string, GameObject>(label, go);
            Pieces.Add(kv); LastPieces.Add(kv);
            return go;
        }

        public void Skip(string label) { Skipped++; SkipLog += (SkipLog.Length > 0 ? ", " : "") + label; }

        public static Transform Mark(string id)
        {
            var go = LevelMark.Find(id);
            return go != null ? go.transform : null;
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }

        /// <summary>Ground point below p (ray from p + up 'up' down to 'down' below p), ignoring triggers.</summary>
        public static bool Ground(Vector3 p, out Vector3 g, float up = 2.5f, float down = 4f)
        {
            if (Physics.Raycast(p + Vector3.up * up, Vector3.down, out var hit, up + down, Solid, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f && hit.collider.GetComponentInParent<ShifterCharacter>() == null)
            { g = hit.point; return true; }
            g = p; return false;
        }

        public static bool Free(Vector3 center, Vector3 half, Quaternion rot) =>
            !Physics.CheckBox(center, half, rot, Solid, QueryTriggerInteraction.Ignore);

        /// <summary>First free floor spot near 'anchor' for a piece of footprint half-extents 'half' (height = 2*half.y),
        /// trying rings of radius 'radii' (8 angles each), preferring spots close to 'prefer'. Needs a clear line of sight
        /// from 'viewFrom' (if given) to the piece's center.</summary>
        public static bool FindSpot(Vector3 anchor, Vector3 half, float[] radii, Vector3 prefer, out Vector3 spot, Vector3? viewFrom = null, float maxDrop = 2.5f)
            => FindSpot(anchor, half, radii, prefer, out spot, viewFrom, maxDrop, null);

        /// <summary>FindSpot with an extra filter on the ground point (e.g. "does not overlap an overlay piece").</summary>
        public static bool FindSpot(Vector3 anchor, Vector3 half, float[] radii, Vector3 prefer, out Vector3 spot, Vector3? viewFrom, float maxDrop, System.Func<Vector3, bool> accept)
        {
            spot = anchor;
            float best = float.MaxValue;
            bool found = false;
            foreach (var r in radii)
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    var p = anchor + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    if (!Ground(p, out var g, 2.5f, maxDrop)) continue;
                    if (Mathf.Abs(g.y - anchor.y) > maxDrop) continue;
                    var c = g + Vector3.up * (half.y + 0.05f);
                    if (!Free(c, half, Quaternion.identity)) continue;
                    if (viewFrom.HasValue)
                    {
                        var d = c - viewFrom.Value;
                        if (Physics.Raycast(viewFrom.Value, d.normalized, d.magnitude - half.magnitude, Solid, QueryTriggerInteraction.Ignore)) continue;
                    }
                    if (accept != null && !accept(g)) continue;
                    float score = (g - prefer).sqrMagnitude;
                    if (score < best) { best = score; spot = g; found = true; }
                }
                if (found) return true;
            }
            return found;
        }

        /// <summary>True when 'b' (expanded by 'margin') intersects the renderer bounds of a piece placed so far
        /// (overlay pieces are collider-free, so Free() cannot see them).</summary>
        public bool OverlapsPlaced(Bounds b, float margin = 0.3f)
        {
            b.Expand(margin * 2f);
            foreach (var kv in Pieces)
            {
                if (kv.Value == null) continue;
                foreach (var r in kv.Value.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled && r.bounds.Intersects(b)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- piece helpers
        /// <summary>Holo-poster beside checkpoint.i, readable from the checkpoint pad / its approach.</summary>
        public GameObject PosterAtCheckpoint(int i, string text, int act = 1)
        {
            string label = "poster.cp" + i;
            var cp = Mark("checkpoint." + i);
            if (cp == null) { Skip(label + "(no mark)"); return null; }
            var fwd = Flat(cp.forward);
            var right = Vector3.Cross(Vector3.up, fwd);
            var viewer = cp.position - fwd * 2.5f + Vector3.up * 1.5f;
            Vector3[] offs = { right * 3.8f + fwd * 0.8f, -right * 3.8f + fwd * 0.8f, right * 4.6f - fwd * 1.5f, -right * 4.6f - fwd * 1.5f,
                               right * 3.2f + fwd * 3.2f, -right * 3.2f + fwd * 3.2f, right * 2.6f - fwd * 3.5f, -right * 2.6f - fwd * 3.5f };
            var half = new Vector3(0.72f, 1.25f, 0.3f);
            foreach (var o in offs)
            {
                if (!Ground(cp.position + o + Vector3.up * 0.3f, out var g, 2f, 1.5f)) continue;
                var rot = EnvKit.FacingViewerAt(g, viewer);
                var c = g + Vector3.up * 1.32f;
                if (!Free(c, half, EnvKit.ViewToFace(rot))) continue;
                if (OverlapsPlaced(new Bounds(c, new Vector3(1.6f, 2.5f, 1.6f)))) continue;
                var toV = viewer - c;
                if (Physics.Raycast(c, toV.normalized, toV.magnitude, Solid, QueryTriggerInteraction.Ignore)) continue;
                // readable side must not be up against anything within 1.5 m
                if (Physics.Raycast(c, EnvKit.ReadableDirection(rot), 1.5f, Solid, QueryTriggerInteraction.Ignore)) continue;
                return Add(label, StoryKit.HoloPoster(Root, g, rot, text, act));
            }
            Skip(label + "(no space)");
            return null;
        }

        /// <summary>ME-TV billboard at world 'pos' (raised in 2 m steps until clear), facing 'lookFrom'.</summary>
        public GameObject Billboard(string label, Vector3 pos, Vector3 lookFrom, float width = 7f)
        {
            float h = width * 0.5625f;
            for (int step = 0; step < 7; step++)
            {
                var p = pos + Vector3.up * (step * 2f);
                var rot = EnvKit.FacingViewerAt(p, lookFrom);
                if (!Free(p, new Vector3(width * 0.5f + 0.6f, h * 0.5f + 1.2f, 0.6f), EnvKit.ViewToFace(rot))) continue;
                return Add(label, StoryKit.MeTvScreen(Root, p, rot, width));
            }
            Skip(label + "(blocked)");
            return null;
        }

        /// <summary>Hologram taunt spot on free floor 2.5-4.5 m from mark 'markId', on the side nearest 'prefer'.</summary>
        public GameObject TauntAt(string markId, string lineId, Vector3 prefer, float radius = 5f, string triggerBeat = null)
        {
            string label = "taunt." + markId;
            var m = Mark(markId);
            if (m == null) { Skip(label + "(no mark)"); return null; }
            return TauntNear(label, m.position, lineId, prefer, radius, triggerBeat);
        }

        public GameObject TauntNear(string label, Vector3 anchor, string lineId, Vector3 prefer, float radius = 5f, string triggerBeat = null)
        {
            if (!FindSpot(anchor, new Vector3(0.55f, 1.05f, 0.55f), new[] { 2.8f, 3.8f, 5f, 2f }, prefer, out var g, null, 2.5f, p => !OverlapsPlaced(new Bounds(p + Vector3.up * 1.05f, new Vector3(1.1f, 2.1f, 1.1f))))) { Skip(label + "(no space)"); return null; }
            var go = StoryKit.HologramTauntSpot(Root, g, EnvKit.FacingViewerAt(g, prefer), lineId);
            var spot = go.GetComponent<StoryLineSpot>();
            if (spot != null) { spot.Radius = radius; spot.TriggerBeat = triggerBeat; }
            return Add(label, go);
        }

        /// <summary>Percy on free floor near 'anchor', facing 'faceToward'. firstLine: line id / text for the first approach.</summary>
        public GameObject PercyNear(string label, Vector3 anchor, Vector3 faceToward, string firstLine = "percy.wink", params string[] lines)
        {
            if (!FindSpot(anchor, new Vector3(0.48f, 0.7f, 0.4f), new[] { 3.5f, 4.5f, 2.5f, 5.5f }, faceToward, out var g, faceToward + Vector3.up * 1.4f, 2.5f, p => !OverlapsPlaced(new Bounds(p + Vector3.up * 0.7f, new Vector3(1f, 1.4f, 1f))))) { Skip(label + "(no space)"); return null; }
            var go = StoryKit.PercyMachine(Root, g, EnvKit.FacingViewerAt(g, faceToward));
            var v = go.GetComponent<PercyMachineView>();
            if (v != null) { v.FirstLine = firstLine; if (lines != null && lines.Length > 0) v.Lines = lines; }
            return Add(label, go);
        }

        public GameObject Statue(string label, Vector3 floor, Vector3 faceToward, float height, int stage) =>
            Add(label, StoryKit.VillainStatue(Root, floor, EnvKit.FacingViewerAt(floor, faceToward), height, stage));

        /// <summary>A plain EnvKit sign at world pos readable from 'viewer'.</summary>
        public GameObject Sign(string label, Vector3 pos, Vector3 viewer, string text) =>
            Add(label, EnvKit.Sign(Root, pos, EnvKit.FacingViewerAt(pos, viewer), text));
    }
}
