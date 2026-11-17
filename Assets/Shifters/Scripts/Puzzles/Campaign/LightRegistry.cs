// =====================================================================================
//  SHIFTERS - LightRegistry.cs   [PUZZLES - campaign wave, CAMPAIGN §10.2]
//  The GEOMETRIC light rule (independent of rendering, so bots and tests can query it):
//    lit = inside an ON ILightSource (horizontal distance <= LightRadius, -PoolBelow..+PoolAbove m vertically)
//        | inside a registered lit volume, a Checkpoint zone, an Elevator car or a HoldingCell interior
//        | inside an ON ILightCone (<= HalfAngle off-axis, <= Range, clear line of sight).
//  LightPool registers itself; FlashlightRig (rendering) calls RegisterCone; party-popper flares call Register.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class LightRegistry
    {
        struct Volume { public Transform T; public Vector3 Center, Half; }

        static readonly List<ILightSource> _sources = new List<ILightSource>();
        static readonly List<ILightCone> _cones = new List<ILightCone>();
        static readonly List<Volume> _volumes = new List<Volume>();
        static readonly RaycastHit[] _hits = new RaycastHit[24];

        /// <summary>Vertical tolerance of pools: a point from PoolBelow m under to PoolAbove m over the source counts.</summary>
        public const float PoolBelow = 1.5f, PoolAbove = 4f;
        /// <summary>Checkpoint rings / elevator cars / holding cells always count as lit (CAMPAIGN §10.2). Tests may switch off.</summary>
        public static bool CountCheckpoints = true, CountElevators = true, CountCells = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _sources.Clear(); _cones.Clear(); _volumes.Clear();
            CountCheckpoints = CountElevators = CountCells = true;
        }

        public static int SourceCount => _sources.Count;
        public static int ConeCount => _cones.Count;

        // ------------------------------------------------------------------ registration
        public static void Register(ILightSource s) { if (s != null && !_sources.Contains(s)) _sources.Add(s); }
        public static void Unregister(ILightSource s) { if (s != null) _sources.Remove(s); }
        public static void RegisterCone(ILightCone c) { if (c != null && !_cones.Contains(c)) _cones.Add(c); }
        public static void UnregisterCone(ILightCone c) { if (c != null) _cones.Remove(c); }

        /// <summary>Always-lit box (local center/size in t's space): safe rooms, floods. Removed with UnregisterVolume(t)
        /// or automatically once t is destroyed.</summary>
        public static void RegisterVolume(Transform t, Vector3 localCenter, Vector3 size)
        {
            if (t == null) return;
            _volumes.Add(new Volume { T = t, Center = localCenter, Half = size * 0.5f });
        }

        public static void UnregisterVolume(Transform t)
        {
            for (int i = _volumes.Count - 1; i >= 0; i--) if (_volumes[i].T == t || _volumes[i].T == null) _volumes.RemoveAt(i);
        }

        // ------------------------------------------------------------------ queries
        /// <summary>Is this world point lit (pool, lit volume or flashlight cone)?</summary>
        public static bool IsLit(Vector3 p) => IsLit(p, null);

        /// <summary>As IsLit(p); colliders under 'ignore' (the asker's own body) never block a cone's line of sight.</summary>
        public static bool IsLit(Vector3 p, Transform ignore) => InPool(p) || InLitVolume(p) || InCone(p, ignore);

        /// <summary>Inside any ON light source (pools, flares).</summary>
        public static bool InPool(Vector3 p)
        {
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                if (Dead(s)) { _sources.RemoveAt(i); continue; }
                if (!s.LightOn) continue;
                var c = s.LightPosition;
                float dy = p.y - c.y;
                if (dy < -PoolBelow || dy > PoolAbove) continue;
                float dx = p.x - c.x, dz = p.z - c.z, r = s.LightRadius;
                if (dx * dx + dz * dz <= r * r) return true;
            }
            return false;
        }

        /// <summary>Inside a registered volume, an (enabled) Checkpoint zone, an Elevator car or a HoldingCell.</summary>
        public static bool InLitVolume(Vector3 p)
        {
            for (int i = _volumes.Count - 1; i >= 0; i--)
            {
                var v = _volumes[i];
                if (v.T == null) { _volumes.RemoveAt(i); continue; }
                if (v.T.gameObject.activeInHierarchy && PuzzleUtil.InBox(v.T, v.Center, v.Half, p)) return true;
            }
            if (CountCheckpoints)
            {
                var cps = Checkpoint.All;
                for (int i = 0; i < cps.Count; i++)
                {
                    var cp = cps[i];
                    if (cp != null && cp.isActiveAndEnabled && PuzzleUtil.InBox(cp.transform, cp.ZoneCenter, cp.ZoneSize * 0.5f + new Vector3(0.3f, 0.5f, 0.3f), p)) return true;
                }
            }
            if (CountCells)
            {
                var cells = HoldingCell.All;
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i] != null && cells[i].isActiveAndEnabled && cells[i].Contains(p)) return true;
            }
            if (CountElevators)
            {
                var els = Elevator.All;
                for (int i = 0; i < els.Count; i++)
                {
                    var e = els[i];
                    if (e == null || !e.isActiveAndEnabled || e.Body == null) continue;
                    var col = e.Body.GetComponent<BoxCollider>();
                    if (col == null) continue;
                    var t = col.transform;
                    var half = col.size * 0.5f;
                    var top = col.center + new Vector3(0f, half.y, 0f);
                    // the car floor footprint, from 0.5 m below its top to 3 m above
                    if (PuzzleUtil.InBox(t, top + new Vector3(0f, 1.25f, 0f), new Vector3(half.x + 0.2f, 1.75f, half.z + 0.2f), p)) return true;
                }
            }
            return false;
        }

        /// <summary>Inside any ON cone with line of sight (characters, triggers, held props and 'ignore' never block).</summary>
        public static bool InCone(Vector3 p, Transform ignore = null)
        {
            for (int i = _cones.Count - 1; i >= 0; i--)
            {
                var c = _cones[i];
                if (Dead(c)) { _cones.RemoveAt(i); continue; }
                if (!c.LightOn) continue;
                var o = c.Origin;
                var d = p - o;
                float dist = d.magnitude;
                if (dist < 0.05f) return true;
                if (dist > c.Range) continue;
                var fwd = c.Forward;
                if (fwd.sqrMagnitude < 1e-6f) continue;
                if (Vector3.Dot(d / dist, fwd.normalized) < Mathf.Cos(c.HalfAngle * Mathf.Deg2Rad)) continue;
                if (LineOfSight(o, p, dist, ignore)) return true;
            }
            return false;
        }

        static bool LineOfSight(Vector3 o, Vector3 p, float dist, Transform ignore)
        {
            int n = Physics.RaycastNonAlloc(o, (p - o) / dist, _hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.distance < 0.3f || h.distance > dist - 0.45f) continue;   // holder's own gear / the target's own surface
                var col = h.collider;
                if (col == null) continue;
                if (ignore != null && col.transform.IsChildOf(ignore)) continue;
                if (PuzzleUtil.CharacterFrom(col) != null) continue;
                var rb = col.attachedRigidbody;
                if (rb != null && rb.TryGetComponent(out Grabbable g) && g.IsHeld) continue;
                return false;
            }
            return true;
        }

        static bool Dead(object o) => o == null || (o is Object uo && uo == null);
    }
}
