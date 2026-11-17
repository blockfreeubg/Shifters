// =====================================================================================
//  SHIFTERS - PerspectiveKit.cs   [owned by the PERSPECTIVE agent]
//  Runtime/edit-mode factories for perspective content (Core/Kit.cs conventions: everything is
//  built inside the factory, cached meshes, MaterialLibrary materials, no Start-time construction).
//  All positions are LOCAL to parent (parent may be null = world).
// =====================================================================================
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Shifters
{
    public static class PerspectiveKit
    {
        public const float TileThickness = 0.25f;

        // ------------------------------------------------------------------ generic
        /// <summary>Adds (or reconfigures) a Revealable on go. Use for puzzle parts: heavy block (Strength, "160 KG - PUSH"),
        /// shoot target (Weapon, "SHOOT"), terminal (Intellect, "TERMINAL - IQ 7"). labelOffset is in world space from go's pivot.</summary>
        public static Revealable Reveal(GameObject go, PerspectiveMask category, string label = "", bool highlight = true, bool showOnlyWhenRevealed = false, Vector3? labelOffset = null)
        {
            var r = go.GetComponent<Revealable>();
            if (r == null) r = go.AddComponent<Revealable>();
            r.Category = category;
            r.Label = label ?? "";
            r.Highlight = highlight;
            r.ShowOnlyWhenRevealed = showOnlyWhenRevealed;
            if (labelOffset.HasValue) r.LabelOffset = labelOffset.Value;
            return r;
        }

        // ------------------------------------------------------------------ IQ: hologram paths
        /// <summary>One invisible-but-solid hologram tile. center = box center (top surface = center.y + size.y/2).</summary>
        public static HiddenPathTile HologramTile(Transform parent, Vector3 center, Vector3 size) => HologramTile(parent, center, size, Quaternion.identity);

        public static HiddenPathTile HologramTile(Transform parent, Vector3 center, Vector3 size, Quaternion rot)
        {
            var go = Kit.Make("HoloTile", parent, center, rot);
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            var tile = go.AddComponent<HiddenPathTile>();
            tile.Size = size;
            var slab = Kit.Make("Holo", go.transform, Vector3.zero);
            var slabR = ProcMesh.AddRenderer(slab, ProcMesh.CachedBox(size * 0.98f, 0.03f), Kit.Mat(MatKey.Hologram), false);
            var frame = Kit.Make("Frame", go.transform, Vector3.zero);
            var frameR = ProcMesh.AddRenderer(frame, FrameMesh(size), MaterialLibrary.Colored(UIColors.Intellect, 0f, 1.8f), false);
            tile.Visuals = new Renderer[] { slabR, frameR };
            var rev = go.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Intellect;
            rev.Highlight = false;
            rev.ShowOnlyWhenRevealed = false; // HiddenPathTile drives visibility itself (footprints)
            rev.PopIn = false;
            rev.Renderers = tile.Visuals;
            rev.LabelOffset = new Vector3(0f, 1.1f, 0f);
            tile.Reveal = rev;
            return tile;
        }

        /// <summary>Hologram walkway along points (LOCAL positions of the TOP-surface centerline). Tiles ~tileLength long,
        /// width wide, linked as neighbours (footprints light up the tiles around the Brainiac). Slopes allowed.</summary>
        public static HiddenPathTile[] HologramPath(Transform parent, Vector3[] points, float width = 1.6f, float tileLength = 1.6f, float gap = 0.08f)
        {
            if (points == null || points.Length < 2) return new HiddenPathTile[0];
            var root = Kit.Make("HologramPath", parent, Vector3.zero);
            var tiles = new System.Collections.Generic.List<HiddenPathTile>();
            for (int s = 0; s < points.Length - 1; s++)
            {
                Vector3 a = points[s], b = points[s + 1], d = b - a;
                float len = d.magnitude;
                if (len < 0.05f) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(0.4f, tileLength)));
                float step = len / n;
                var rot = Quaternion.LookRotation(d / len, Vector3.up);
                var size = new Vector3(width, TileThickness, Mathf.Max(0.3f, step - gap));
                for (int k = 0; k < n; k++)
                {
                    Vector3 top = a + d / len * ((k + 0.5f) * step);
                    var t = HologramTile(root.transform, top - rot * Vector3.up * (TileThickness * 0.5f), size, rot);
                    t.name = "HoloTile_" + tiles.Count;
                    tiles.Add(t);
                }
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                if (i == 0) tiles[i].Neighbours = tiles.Count > 1 ? new[] { tiles[1] } : new HiddenPathTile[0];
                else if (i == tiles.Count - 1) tiles[i].Neighbours = new[] { tiles[i - 1] };
                else tiles[i].Neighbours = new[] { tiles[i - 1], tiles[i + 1] };
            }
            if (tiles.Count > 0) tiles[0].Reveal.Label = "HIDDEN PATH";
            return tiles.ToArray();
        }

        static Mesh FrameMesh(Vector3 size)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "pk_holoframe_{0:0.##}x{1:0.##}x{2:0.##}", size.x, size.y, size.z);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float t = 0.06f, y = size.y * 0.5f + 0.01f, hx = size.x * 0.5f - t * 0.5f, hz = size.z * 0.5f - t * 0.5f;
                b.AddBox(new Vector3(0f, y, hz), new Vector3(size.x, 0.03f, t), Quaternion.identity, Color.white);
                b.AddBox(new Vector3(0f, y, -hz), new Vector3(size.x, 0.03f, t), Quaternion.identity, Color.white);
                b.AddBox(new Vector3(hx, y, 0f), new Vector3(t, 0.03f, size.z - 2f * t), Quaternion.identity, Color.white);
                b.AddBox(new Vector3(-hx, y, 0f), new Vector3(t, 0.03f, size.z - 2f * t), Quaternion.identity, Color.white);
                return b.ToMesh(key);
            });
        }

        // ------------------------------------------------------------------ SPD: turbo routes
        /// <summary>Glowing chevrons along the floor path (LOCAL points at floor height), only visible in TURBO view.</summary>
        public static SpeedRoute SpeedRoute(Transform parent, Vector3[] points, float spacing = 1.1f, float width = 1.2f)
        {
            var origin = points != null && points.Length > 0 ? points[0] : Vector3.zero;
            var root = Kit.Make("SpeedRoute", parent, origin);
            var route = root.AddComponent<SpeedRoute>();
            var list = new System.Collections.Generic.List<Renderer>();
            float total = 0f;
            if (points != null && points.Length >= 2)
            {
                var mesh = ChevronMesh(width);
                var mat = MaterialLibrary.Colored(UIColors.Speed, 0f, 2f);
                float carry = spacing * 0.5f;
                for (int s = 0; s < points.Length - 1; s++)
                {
                    Vector3 a = points[s], b = points[s + 1], d = b - a;
                    float len = d.magnitude;
                    if (len < 0.01f) continue;
                    Vector3 flat = new Vector3(d.x, 0f, d.z);
                    var rot = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
                    float x = carry;
                    while (x <= len)
                    {
                        var p = a + d * (x / len) + Vector3.up * 0.03f - origin;
                        var c = Kit.Make("Chevron_" + list.Count, root.transform, p, rot);
                        var r = ProcMesh.AddRenderer(c, mesh, mat, false);
                        r.receiveShadows = false;
                        list.Add(r);
                        x += spacing;
                    }
                    carry = x - len;
                    total += len;
                }
            }
            route.Chevrons = list.ToArray();
            route.Length = total;
            route.GlowColor = UIColors.Speed;
            var rev = root.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Speed;
            rev.Highlight = false;
            rev.ShowOnlyWhenRevealed = true;
            rev.Label = "TURBO ROUTE";
            rev.LabelOffset = new Vector3(0f, 1.3f, 0f);
            route.Reveal = rev;
            return route;
        }

        static Mesh ChevronMesh(float width)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "pk_chevron_w{0:0.##}", width);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float w = width;
                var size = new Vector3(0.2f * Mathf.Max(0.6f, w), 0.03f, 0.64f * w);
                b.AddBox(new Vector3(0.225f * w, 0f, 0.025f * w), size, Quaternion.Euler(0f, -45f, 0f), Color.white);
                b.AddBox(new Vector3(-0.225f * w, 0f, 0.025f * w), size, Quaternion.Euler(0f, 45f, 0f), Color.white);
                return b.ToMesh(key);
            });
        }

        // ------------------------------------------------------------------ IQ: clues
        /// <summary>IQ-only painted clue (sequence digit, logic symbol, text). rot: +Z points INTO the surface, so the clue
        /// reads from the front (wall facing -Z: identity; floor: Euler(90,0,0)). size = text height (m).</summary>
        public static ClueMarker Clue(Transform parent, Vector3 pos, Quaternion rot, string text, float size = 0.8f)
        {
            var root = Kit.Make("Clue_" + text, parent, pos, rot);
            var clue = root.AddComponent<ClueMarker>();
            var tmp = PerspectiveFonts.MakeText(root.transform, "Text", text, size, UIColors.Hex(0xF1E8FF), new Vector3(0f, 0f, -0.035f), Quaternion.identity, true);
            tmp.fontStyle = TMPro.FontStyles.Bold;
            float w = Mathf.Max(size * 0.9f, tmp.GetPreferredValues(text).x + size * 0.35f);
            var plate = Kit.Make("Plate", root.transform, new Vector3(0f, 0f, -0.012f));
            ProcMesh.AddRenderer(plate, ProcMesh.CachedBox(new Vector3(w, size * 1.25f, 0.02f), 0.01f), Kit.Mat(MatKey.Hologram), false);
            clue.Text = tmp;
            var rev = root.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Intellect;
            rev.Highlight = false;
            rev.ShowOnlyWhenRevealed = true;
            clue.Reveal = rev;
            return clue;
        }

        // ------------------------------------------------------------------ GUN: shoot hints
        /// <summary>Crosshair marker visible only to the gun holder (put it at/in front of a ShootTarget).</summary>
        public static Revealable ShootHint(Transform parent, Vector3 pos, float radius = 0.55f)
        {
            var root = Kit.Make("ShootHint", parent, pos);
            var marker = root.AddComponent<ShootHintMarker>();
            var spinner = Kit.Make("Spinner", root.transform, Vector3.zero);
            var reticle = Kit.Make("Reticle", spinner.transform, Vector3.zero);
            var r = ProcMesh.AddRenderer(reticle, ReticleMesh(radius), MaterialLibrary.Colored(UIColors.Weapon, 0f, 2.2f), false);
            r.receiveShadows = false;
            marker.Spinner = spinner.transform;
            var rev = root.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Weapon;
            rev.Highlight = false;
            rev.ShowOnlyWhenRevealed = true;
            marker.Reveal = rev;
            return rev;
        }

        static Mesh ReticleMesh(float radius)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "pk_reticle_r{0:0.##}", radius);
            return ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                var axisZ = Quaternion.Euler(90f, 0f, 0f);
                b.AddTorus(Vector3.zero, radius, radius * 0.07f, axisZ, Color.white, 28, 6);
                float tl = radius * 0.45f, tw = radius * 0.09f;
                b.AddBox(new Vector3(0f, radius + tl * 0.3f, 0f), new Vector3(tw, tl, tw), Quaternion.identity, Color.white);
                b.AddBox(new Vector3(0f, -radius - tl * 0.3f, 0f), new Vector3(tw, tl, tw), Quaternion.identity, Color.white);
                b.AddBox(new Vector3(radius + tl * 0.3f, 0f, 0f), new Vector3(tl, tw, tw), Quaternion.identity, Color.white);
                b.AddBox(new Vector3(-radius - tl * 0.3f, 0f, 0f), new Vector3(tl, tw, tw), Quaternion.identity, Color.white);
                b.AddSphere(Vector3.zero, radius * 0.1f, Quaternion.identity, Color.white, 10, 6);
                return b.ToMesh(key);
            });
        }

        // ------------------------------------------------------------------ campaign wave (promoted by SEAMS)
        /// <summary>Hologram walkway along corner points (LOCAL top-surface centreline; copy of L4Decor.HoloWalkway).
        /// Unlike HologramPath it fills every corner with a square tile so nobody falls through the outer corner. Tiles
        /// are linked as neighbours in walking order (footprints light up the tiles around the Brainiac).</summary>
        public static HiddenPathTile[] HoloWalkway(Transform p, Vector3[] corners, float width = 1.6f, float tileLength = 1.6f)
        {
            var root = Kit.Make("HologramWalkway", p, Vector3.zero);
            var tiles = new List<HiddenPathTile>();
            if (corners == null || corners.Length < 2) return tiles.ToArray();
            float th = TileThickness;
            int n = corners.Length;
            for (int s = 0; s < n - 1; s++)
            {
                Vector3 a = corners[s], b = corners[s + 1];
                Vector3 d = b - a; d.y = 0f;
                float len = d.magnitude;
                if (len < 0.05f) continue;
                Vector3 dir = d / len;
                float s0 = s > 0 ? width * 0.5f : 0f;
                float e0 = s + 1 < n - 1 ? width * 0.5f : 0f;
                float usable = len - s0 - e0;
                if (usable > 0.2f)
                {
                    int k = Mathf.Max(1, Mathf.RoundToInt(usable / Mathf.Max(0.1f, tileLength)));
                    float step = usable / k;
                    var rot = Quaternion.LookRotation(dir, Vector3.up);
                    for (int i = 0; i < k; i++)
                    {
                        Vector3 top = a + dir * (s0 + (i + 0.5f) * step);
                        top.y = Mathf.Lerp(a.y, b.y, (s0 + (i + 0.5f) * step) / len);
                        var tile = HologramTile(root.transform, top + Vector3.down * (th * 0.5f), new Vector3(width, th, step), rot);
                        tile.name = "HoloTile_" + tiles.Count;
                        tiles.Add(tile);
                    }
                }
                if (s + 1 < n - 1)
                {
                    var c = HologramTile(root.transform, b + Vector3.down * (th * 0.5f), new Vector3(width, th, width), Quaternion.LookRotation(dir, Vector3.up));
                    c.name = "HoloCorner_" + tiles.Count;
                    tiles.Add(c);
                }
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                var nb = new List<HiddenPathTile>(2);
                if (i > 0) nb.Add(tiles[i - 1]);
                if (i < tiles.Count - 1) nb.Add(tiles[i + 1]);
                tiles[i].Neighbours = nb.ToArray();
            }
            if (tiles.Count > 0 && tiles[0].Reveal != null) tiles[0].Reveal.Label = "HIDDEN PATH - FOLLOW ME";
            return tiles.ToArray();
        }

        /// <summary>IQ-only threat tag above target ("THREAT: {m} M": {m} = meters to the active intern). Always visible
        /// while the active intern has the Intellect perspective, drawn through walls, billboarded, readable at range.
        /// Child of target (dies with it). Returns the ThreatMarker (set Template / Height / Mask / RequirePerspective).</summary>
        public static ThreatMarker ThreatLabel(Transform target, string text)
        {
            if (target == null) return null;
            var go = new GameObject("ThreatLabel");
            go.transform.SetParent(target, false);
            go.transform.localPosition = Vector3.up * 2.6f;
            var m = go.AddComponent<ThreatMarker>();
            m.Target = target;
            m.Template = string.IsNullOrEmpty(text) ? "THREAT: {m} M" : text;
            m.Text = PerspectiveFonts.MakeText(go.transform, "Text", m.Template.Replace("{m}", "?"), 0.42f, UIColors.Danger, Vector3.zero, Quaternion.identity, true);
            m.Text.fontStyle = TMPro.FontStyles.Bold;
            m.Text.enabled = false;
            return m;
        }
    }
}
