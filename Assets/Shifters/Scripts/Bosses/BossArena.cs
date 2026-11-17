// =====================================================================================
//  SHIFTERS - BossArena.cs   [BOSSES module - CAMPAIGN §11.1 B5/B6/B7]
//  The fight's space: world Bounds, BREAK AREAS (SafeZones, never covered by an attack), phase
//  checkpoints, named marks (TargetMark lookups) and hazard groups. Static validators:
//    ValidateSafeZones  - every break area >= 7 x 5 m inside the bounds; fixed-footprint attacks
//                         (Shockwave/Sweep/SkateLoop) never reach one; aimed attacks (Slam/Spook/Charge)
//                         clamp out of every zone for every aim sample on a 1.5 m grid.
//    ValidateNoKillZones - no Lost-causing KillZone (FoamPit is allowed) intersects the bounds.
// =====================================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public sealed class BossArena : MonoBehaviour
    {
        [Tooltip("World-space fight volume.")] public Bounds Bounds = new Bounds(Vector3.zero, new Vector3(30f, 12f, 30f));
        [Tooltip("Break areas (BossSafeZone on each).")] public Transform[] SafeZones = new Transform[0];
        [Tooltip("Optional: checkpoints of the phases (BossPhaseSpec.CheckpointIndex is what the boss uses).")] public Checkpoint[] PhaseCheckpoints = new Checkpoint[0];
        [Tooltip("The boss fighting here (set by BossBase).")] public BossBase Boss;

        readonly Dictionary<string, Transform> _marks = new Dictionary<string, Transform>();
        readonly List<BossSafeZone> _zones = new List<BossSafeZone>();
        bool _zonesCached;

        public float FloorY => Bounds.min.y;

        public IReadOnlyList<BossSafeZone> Zones { get { CacheZones(); return _zones; } }

        void CacheZones()
        {
            if (_zonesCached && Application.isPlaying) return;
            _zonesCached = true;
            _zones.Clear();
            if (SafeZones == null) return;
            foreach (var t in SafeZones)
            {
                if (t == null) continue;
                var z = t.GetComponent<BossSafeZone>();
                if (z != null) _zones.Add(z);
            }
        }

        /// <summary>Call after adding/removing SafeZones at runtime.</summary>
        public void RefreshZones() { _zonesCached = false; CacheZones(); }

        public void AddSafeZone(Transform t)
        {
            if (t == null) return;
            var list = new List<Transform>(SafeZones ?? new Transform[0]);
            if (!list.Contains(t)) list.Add(t);
            SafeZones = list.ToArray();
            RefreshZones();
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            var b = Bounds; b.Expand(margin * 2f);
            return b.Contains(p);
        }

        public bool InSafeZone(Vector3 p, float margin = 0f)
        {
            CacheZones();
            for (int i = 0; i < _zones.Count; i++) if (_zones[i] != null && _zones[i].Contains(p, margin)) return true;
            return false;
        }

        public BossSafeZone SafeZoneAt(Vector3 p, float margin = 0f)
        {
            CacheZones();
            for (int i = 0; i < _zones.Count; i++) if (_zones[i] != null && _zones[i].Contains(p, margin)) return _zones[i];
            return null;
        }

        public BossSafeZone NearestSafeZone(Vector3 p)
        {
            CacheZones();
            BossSafeZone best = null; float bd = float.PositiveInfinity;
            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i] == null) continue;
                float d = _zones[i].Distance(p);
                if (d < bd) { bd = d; best = _zones[i]; }
            }
            return best;
        }

        /// <summary>Minimal planar distance from p to any break area (inf without zones).</summary>
        public float SafeZoneDistance(Vector3 p)
        {
            CacheZones();
            float bd = float.PositiveInfinity;
            for (int i = 0; i < _zones.Count; i++) if (_zones[i] != null) bd = Mathf.Min(bd, _zones[i].Distance(p));
            return bd;
        }

        /// <summary>Moves p so a circle of radius r around it touches no break area; then keeps it inside the bounds.</summary>
        public Vector3 PushOutOfSafeZones(Vector3 p, float r)
        {
            CacheZones();
            for (int iter = 0; iter < 4; iter++)
            {
                bool moved = false;
                for (int i = 0; i < _zones.Count; i++)
                {
                    var z = _zones[i];
                    if (z == null) continue;
                    var rot = z.Rotation;
                    var l = Quaternion.Inverse(rot) * (p - z.Center);
                    float hx = z.Size.x * 0.5f + r + 0.05f, hz = z.Size.y * 0.5f + r + 0.05f;
                    if (Mathf.Abs(l.x) >= hx || Mathf.Abs(l.z) >= hz) continue;
                    float px = hx - Mathf.Abs(l.x), pz = hz - Mathf.Abs(l.z);
                    // push out through the side facing the arena centre when both are close (keeps attacks inside)
                    if (px < pz) l.x = Mathf.Sign(l.x == 0f ? 1f : l.x) * hx; else l.z = Mathf.Sign(l.z == 0f ? 1f : l.z) * hz;
                    var np = z.Center + rot * l; np.y = p.y; p = np;
                    moved = true;
                }
                if (!moved) break;
            }
            return p;
        }

        public Vector3 ClampToBounds(Vector3 p, float inset = 0.5f)
        {
            var min = Bounds.min; var max = Bounds.max;
            p.x = Mathf.Clamp(p.x, min.x + inset, max.x - inset);
            p.z = Mathf.Clamp(p.z, min.z + inset, max.z - inset);
            return p;
        }

        /// <summary>Named transform under the arena (or under the boss) - TargetMark lookups. Cached.</summary>
        public Transform Mark(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_marks.TryGetValue(name, out var t) && t != null) return t;
            t = FindDeep(transform, name);
            if (t == null && Boss != null) t = FindDeep(Boss.transform, name);
            if (t != null) _marks[name] = t;
            return t;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { var r = FindDeep(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        /// <summary>Children of "HazardGroups/&lt;name&gt;" are switched by the boss per phase (BossPhaseSpec.HazardGroupsOn).</summary>
        public Transform HazardGroup(string name)
        {
            var root = transform.Find("HazardGroups");
            return root != null ? root.Find(name) : null;
        }

        public IEnumerable<Transform> HazardGroups()
        {
            var root = transform.Find("HazardGroups");
            if (root == null) yield break;
            for (int i = 0; i < root.childCount; i++) yield return root.GetChild(i);
        }

        // ------------------------------------------------------------------ validation (B5, B6)
        public bool ValidateSafeZones(out string error)
        {
            RefreshZones();
            var sb = new StringBuilder();
            if (_zones.Count == 0) sb.Append("no break areas; ");
            foreach (var z in _zones)
            {
                if (z == null) continue;
                float a = Mathf.Max(z.Size.x, z.Size.y), b = Mathf.Min(z.Size.x, z.Size.y);
                if (a < Tuning.BossSafeZoneMin - 0.01f || b < Tuning.BossSafeZoneMinDepth - 0.01f) sb.Append(z.name + " is " + z.Size.x + "x" + z.Size.y + " m (< " + Tuning.BossSafeZoneMin + "x" + Tuning.BossSafeZoneMinDepth + "); ");
                var c = z.Center; c.y = Bounds.center.y;
                if (!Bounds.Contains(c)) sb.Append(z.name + " lies outside the arena bounds; ");
            }
            var bosses = new List<BossBase>();
            if (Boss != null) bosses.Add(Boss);
            foreach (var bb in FindObjectsByType<BossBase>(FindObjectsInactive.Include)) if (bb != null && bb.Arena == this && !bosses.Contains(bb)) bosses.Add(bb);
            foreach (var boss in bosses) ValidateBoss(boss, sb);
            error = sb.ToString();
            return error.Length == 0;
        }

        void ValidateBoss(BossBase boss, StringBuilder sb)
        {
            if (boss.Phases == null) return;
            for (int p = 0; p < boss.Phases.Length; p++)
            {
                var pat = boss.Phases[p].Pattern;
                if (pat == null) continue;
                for (int i = 0; i < pat.Length; i++)
                {
                    var s = pat[i];
                    string tag = boss.BossId + " P" + p + "#" + i + " " + s.Kind;
                    float r = BossAttackDefaults.Radius(s), range = BossAttackDefaults.Range(s);
                    Vector3 origin = BossAttackRunner.StaticOrigin(boss, s);
                    switch (s.Kind)
                    {
                        case BossAttackKind.Shockwave:
                        case BossAttackKind.Sweep:
                            foreach (var z in _zones) if (z != null && z.Distance(origin) <= range + 0.5f)
                                sb.Append(tag + " (reach " + range + " m from " + Fmt(origin) + ") covers " + z.name + "; ");
                            break;
                        case BossAttackKind.SkateLoop:
                        {
                            var path = BossAttackRunner.LoopPath(boss, s);
                            if (path == null || path.Length < 2) { sb.Append(tag + " has no loop mark '" + s.TargetMark + "'; "); break; }
                            foreach (var z in _zones)
                            {
                                if (z == null) continue;
                                for (int k = 0; k < path.Length; k++)
                                    if (BossFootprint.SegmentRectDistance(path[k], path[(k + 1) % path.Length], z.Center, z.Rotation, z.Size) <= r + 0.5f)
                                    { sb.Append(tag + " loop crosses " + z.name + "; "); break; }
                            }
                            break;
                        }
                        case BossAttackKind.Charge:
                        case BossAttackKind.Volley:
                            foreach (var z in _zones) if (z != null && z.Distance(origin) <= r + 0.2f) sb.Append(tag + " starts inside " + z.name + "; ");
                            if (s.Kind == BossAttackKind.Charge) CheckAimSamples(tag, r, sb, origin, true);
                            break;
                        case BossAttackKind.SlamZone:
                        case BossAttackKind.Spook:
                            CheckAimSamples(tag, s.Kind == BossAttackKind.Spook ? 1.5f : r, sb, origin, false);
                            break;
                    }
                }
            }
        }

        void CheckAimSamples(string tag, float r, StringBuilder sb, Vector3 origin, bool charge)
        {
            var min = Bounds.min; var max = Bounds.max;
            int bad = 0;
            for (float x = min.x; x <= max.x; x += 1.5f)
                for (float z = min.z; z <= max.z; z += 1.5f)
                {
                    var aim = new Vector3(x, FloorY, z);
                    if (charge)
                    {
                        var end = BossAttackRunner.ClampCharge(this, origin, aim, r, 999f);
                        foreach (var zz in _zones) if (zz != null && BossFootprint.SegmentRectDistance(origin, end, zz.Center, zz.Rotation, zz.Size) < r - 0.01f) bad++;
                    }
                    else
                    {
                        var p = PushOutOfSafeZones(aim, r);
                        foreach (var zz in _zones) if (zz != null && zz.Distance(p) < r - 0.01f) bad++;
                    }
                }
            if (bad > 0) sb.Append(tag + ": " + bad + " aim samples still overlap a break area after clamping; ");
        }

        public bool ValidateNoKillZones(out string error)
        {
            var sb = new StringBuilder();
            foreach (var k in FindObjectsByType<KillZone>(FindObjectsInactive.Include))
            {
                if (k == null || k.Type == KillZoneType.FoamPit) continue;
                var col = k.GetComponent<Collider>();
                var kb = col != null ? col.bounds : new Bounds(k.transform.position, Vector3.one);
                if (col != null && !col.enabled) { var bc = col as BoxCollider; if (bc != null) kb = new Bounds(k.transform.TransformPoint(bc.center), Vector3.Scale(bc.size, k.transform.lossyScale)); }
                if (kb.Intersects(Bounds)) sb.Append(k.name + " (" + k.Type + ") intersects the arena; ");
            }
            if (Bounds.min.y < Tuning.LethalFallY + 1f) sb.Append("arena floor is below the lethal fall height; ");
            error = sb.ToString();
            return error.Length == 0;
        }

        static string Fmt(Vector3 v) => "(" + v.x.ToString("0.#") + ", " + v.z.ToString("0.#") + ")";

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(Bounds.center, Bounds.size);
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
            foreach (var z in Zones) if (z != null) { Gizmos.matrix = Matrix4x4.TRS(z.Center, z.Rotation, Vector3.one); Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(z.Size.x, 0.2f, z.Size.y)); }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
