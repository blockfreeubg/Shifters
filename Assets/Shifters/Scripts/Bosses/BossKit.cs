// =====================================================================================
//  SHIFTERS - BossKit.cs   [BOSSES module - §18.3 factories, Core/Kit.cs conventions]
//  Built entirely inside the factory, edit-mode safe, positions local to parent, cached meshes.
//    Arena / BreakArea / WeakPointShoot / WeakPointPunch / WeakPointHack / WeakPointImpact /
//    JamSensor / TelegraphDecal / ShockwaveRing / TestBoss ("MEGA BONK-BOT 9000")
//  Plus helpers: Mark (named arena point), LoopMarks (SkateLoop path), HazardGroup, Projectile.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class BossKit
    {
        static Quaternion Rot(Quaternion? r) => r ?? Quaternion.identity;

        // ------------------------------------------------------------------ arena
        /// <summary>Boss arena root: BossArena (Bounds = size.x x 12 x size.y from the floor at center), optional
        /// walls (wallHeight &gt; 0; no KillZones ever), optional floor, "Marks" (+ "Center"), "HazardGroups".</summary>
        public static BossArena Arena(Transform p, Vector3 center, Vector2 size, float wallHeight, bool floor = false)
        {
            var root = Kit.Make("BossArena", p, center);
            var a = root.AddComponent<BossArena>();
            var t = root.transform;
            a.Bounds = new Bounds(t.position + Vector3.up * 6f, new Vector3(size.x, 12f, size.y));
            if (floor) Kit.SolidBox(t, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(size.x + 1.2f, 1f, size.y + 1.2f), MatKey.Floor, 0f);
            if (wallHeight > 0f)
            {
                var walls = Kit.Make("Walls", t, Vector3.zero).transform;
                float hx = size.x * 0.5f + 0.3f, hz = size.y * 0.5f + 0.3f, h = wallHeight;
                Kit.SolidBox(walls, "WallN", new Vector3(0f, h * 0.5f, hz), new Vector3(size.x + 1.2f, h, 0.6f), MatKey.Wall, 0.05f);
                Kit.SolidBox(walls, "WallS", new Vector3(0f, h * 0.5f, -hz), new Vector3(size.x + 1.2f, h, 0.6f), MatKey.Wall, 0.05f);
                Kit.SolidBox(walls, "WallE", new Vector3(hx, h * 0.5f, 0f), new Vector3(0.6f, h, size.y), MatKey.WallAlt, 0.05f);
                Kit.SolidBox(walls, "WallW", new Vector3(-hx, h * 0.5f, 0f), new Vector3(0.6f, h, size.y), MatKey.WallAlt, 0.05f);
                Kit.BoxVisual(walls, "TrimN", new Vector3(0f, h + 0.06f, hz), new Vector3(size.x + 1.3f, 0.12f, 0.7f), MatKey.HazardStripes, 0f);
                Kit.BoxVisual(walls, "TrimS", new Vector3(0f, h + 0.06f, -hz), new Vector3(size.x + 1.3f, 0.12f, 0.7f), MatKey.HazardStripes, 0f);
            }
            var marks = Kit.Make("Marks", t, Vector3.zero).transform;
            Kit.Make("Center", marks, Vector3.zero);
            Kit.Make("HazardGroups", t, Vector3.zero);
            return a;
        }

        /// <summary>Named point under the arena's "Marks" (TargetMark lookups). pos local to the arena.</summary>
        public static Transform Mark(BossArena arena, string name, Vector3 localPos)
        {
            var marks = arena.transform.Find("Marks") ?? Kit.Make("Marks", arena.transform, Vector3.zero).transform;
            return Kit.Make(name, marks, localPos).transform;
        }

        /// <summary>SkateLoop path: a mark whose children (in order) are the loop points (local to the arena).</summary>
        public static Transform LoopMarks(BossArena arena, string name, params Vector3[] localPoints)
        {
            var root = Mark(arena, name, Vector3.zero);
            for (int i = 0; i < localPoints.Length; i++) Kit.Make(name + "_" + i, root, localPoints[i]);
            return root;
        }

        /// <summary>Empty group under "HazardGroups" switched on only in the phases listing its name.</summary>
        public static Transform HazardGroup(BossArena arena, string name)
        {
            var groups = arena.transform.Find("HazardGroups") ?? Kit.Make("HazardGroups", arena.transform, Vector3.zero).transform;
            var g = groups.Find(name);
            return g != null ? g : Kit.Make(name, groups, Vector3.zero).transform;
        }

        /// <summary>BREAK AREA (B6): yellow/black border decal, "BREAK AREA" floor text, BossSafeZone. Registers itself
        /// with a BossArena found in p's parents. size &gt;= 7 x 5 m (Tuning.BossSafeZoneMin x BossSafeZoneMinDepth).</summary>
        public static GameObject BreakArea(Transform p, Vector3 pos, Vector2 size)
        {
            var root = Kit.Make("BreakArea", p, pos);
            var t = root.transform;
            var z = root.AddComponent<BossSafeZone>();
            z.Size = size;
            float hx = size.x * 0.5f, hz = size.y * 0.5f, w = 0.35f;
            Kit.BoxVisual(t, "EdgeN", new Vector3(0f, 0.015f, hz - w * 0.5f), new Vector3(size.x, 0.03f, w), MatKey.HazardStripes, 0f, null, false);
            Kit.BoxVisual(t, "EdgeS", new Vector3(0f, 0.015f, -hz + w * 0.5f), new Vector3(size.x, 0.03f, w), MatKey.HazardStripes, 0f, null, false);
            Kit.BoxVisual(t, "EdgeE", new Vector3(hx - w * 0.5f, 0.015f, 0f), new Vector3(w, 0.03f, size.y - 2f * w), MatKey.HazardStripes, 0f, null, false);
            Kit.BoxVisual(t, "EdgeW", new Vector3(-hx + w * 0.5f, 0.015f, 0f), new Vector3(w, 0.03f, size.y - 2f * w), MatKey.HazardStripes, 0f, null, false);
            Kit.BoxVisual(t, "Tint", new Vector3(0f, 0.008f, 0f), new Vector3(size.x - 2f * w, 0.016f, size.y - 2f * w), MatKey.FloorAlt, 0f, null, false);
            var up = Quaternion.Euler(90f, 0f, 0f);
            PuzzleVisuals.Text(t, "Label", new Vector3(0f, 0.04f, -hz + 0.9f), up, "BREAK AREA", 5f, UIColors.AccentYellow, size.x - 1f, 1f);
            PuzzleVisuals.Text(t, "Sub", new Vector3(0f, 0.04f, -hz + 1.5f), up, "NO BOSSING ALLOWED", 2.6f, UIColors.Text, size.x - 1f, 0.6f);
            var arena = p != null ? p.GetComponentInParent<BossArena>() : null;
            if (arena != null) arena.AddSafeZone(t);
            return root;
        }

        // ------------------------------------------------------------------ weak points
        /// <summary>Glowing target sphere (any gun or tool, B11). Mount it &gt;= 1.2 m high (shot-pivot rule).</summary>
        public static BossWeakPoint WeakPointShoot(Transform p, Vector3 pos, float radius, BossBase boss, int phaseMask)
        {
            var root = Kit.Make("WeakPoint_Shoot", p, pos);
            var t = root.transform;
            var core = ProcMesh.AddRenderer(Kit.Make("Core", t, Vector3.zero), ProcMesh.Cached("bk_wp_core_" + radius.ToString("0.00"), () => ProcMesh.Sphere(radius, 16, 10)), Kit.Mat(MatKey.LightRed), false);
            ProcMesh.AddRenderer(Kit.Make("Rim", t, Vector3.zero, Quaternion.Euler(90f, 0f, 0f)), ProcMesh.Cached("bk_wp_rim_" + radius.ToString("0.00"), () => ProcMesh.Torus(radius * 1.25f, radius * 0.12f, 24, 6)), Kit.Mat(MatKey.HazardYellow), false);
            var col = root.AddComponent<SphereCollider>();
            col.radius = radius * 1.1f;
            var wp = root.AddComponent<BossWeakPoint>();
            wp.Kind = BossHitKind.Shoot; wp.Boss = boss; wp.PhaseMask = phaseMask;
            wp.Glow = new Renderer[] { core };
            PerspectiveKit.Reveal(root, PerspectiveMask.Weapon, "SHOOT HERE", true, false, new Vector3(0f, radius + 0.6f, 0f));
            return wp;
        }

        /// <summary>"PUNCH HERE" plate (E-interact by STR &gt;= strength).</summary>
        public static BossWeakPoint WeakPointPunch(Transform p, Vector3 pos, BossBase boss, int phaseMask, int strength = 7)
        {
            var root = Kit.Make("WeakPoint_Punch", p, pos);
            var t = root.transform;
            var plate = Kit.BoxVisual(t, "Plate", Vector3.zero, new Vector3(1f, 0.8f, 0.25f), MatKey.LightRed, 0.06f, null, false);
            Kit.BoxVisual(t, "Frame", new Vector3(0f, 0f, -0.02f), new Vector3(1.2f, 1f, 0.2f), MatKey.HazardStripes, 0.04f);
            PuzzleVisuals.Text(t, "Label", new Vector3(0f, 0.65f, 0.14f), Quaternion.Euler(0f, 180f, 0f), "PUNCH HERE", 2.6f, UIColors.AccentYellow, 1.6f, 0.4f);
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1f, 0.4f);
            var wp = root.AddComponent<BossWeakPoint>();
            wp.Kind = BossHitKind.Punch; wp.Boss = boss; wp.PhaseMask = phaseMask; wp.RequiredStrength = strength;
            wp.Glow = new Renderer[] { plate };
            PerspectiveKit.Reveal(root, PerspectiveMask.Strength, "PUNCH (STR " + strength + ")", true, false, new Vector3(0f, 1.1f, 0f));
            return wp;
        }

        /// <summary>Hack port: a non-latching Terminal (IQ 7, on 1.5 s) whose successful hack lands one Hack hit.</summary>
        public static BossWeakPoint WeakPointHack(Transform p, Vector3 pos, Quaternion? rot, BossBase boss, int phaseMask)
        {
            var root = Kit.Make("WeakPoint_Hack", p, pos, Rot(rot));
            var term = PuzzleKit.Terminal(root.transform, Vector3.zero, Quaternion.identity, Tuning.TerminalIQ, false, 1.5f);
            var wp = root.AddComponent<BossWeakPoint>();
            wp.Kind = BossHitKind.Hack; wp.Boss = boss; wp.PhaseMask = phaseMask;
            wp.SetSource(term);
            var rs = term != null ? term.GetComponentsInChildren<Renderer>() : new Renderer[0];
            wp.Glow = rs.Length > 0 ? new[] { rs[0] } : new Renderer[0];
            return wp;
        }

        /// <summary>Impact zone: a trigger box; a rigidbody of &gt;= minMassKg (crate, thrown intern) arriving at speed lands a hit.</summary>
        public static BossWeakPoint WeakPointImpact(Transform p, Vector3 pos, Vector3 size, BossBase boss, int phaseMask, float minMassKg)
        {
            var trig = Kit.Trigger(p, "WeakPoint_Impact", pos, size);
            var t = trig.transform;
            var outline = Kit.BoxVisual(t, "Outline", new Vector3(0f, -size.y * 0.5f + 0.02f, 0f), new Vector3(size.x, 0.04f, size.z), MatKey.LightRed, 0f, null, false);
            PuzzleVisuals.Text(t, "Label", new Vector3(0f, -size.y * 0.5f + 0.06f, 0f), Quaternion.Euler(90f, 0f, 0f), "IMPACT >= " + Mathf.RoundToInt(minMassKg) + " KG", 3f, UIColors.AccentYellow, size.x, 0.8f);
            var wp = trig.gameObject.AddComponent<BossWeakPoint>();
            wp.Kind = BossHitKind.Impact; wp.Boss = boss; wp.PhaseMask = phaseMask; wp.MinMassKg = minMassKg;
            wp.Glow = new Renderer[] { outline };
            return wp;
        }

        /// <summary>"JAMS AT &gt;= N KG" pad sensor: a PressurePlate (ON while load &gt;= minKg) framed to size, STR-view label.</summary>
        public static GameObject JamSensor(Transform p, Vector3 pos, Vector2 size, float minKg)
        {
            var plate = PuzzleKit.PressurePlate(p, pos, null, minKg);
            var go = plate.gameObject;
            go.name = "JamSensor";
            if (plate.RequirementText != null) plate.RequirementText.text = "JAMS AT " + Mathf.RoundToInt(minKg) + " KG";
            if (size.x > 2.1f || size.y > 2.1f)
            {
                var t = go.transform;
                float w = 0.25f, hx = size.x * 0.5f, hz = size.y * 0.5f;
                Kit.BoxVisual(t, "FrameN", new Vector3(0f, 0.01f, hz), new Vector3(size.x, 0.02f, w), MatKey.HazardStripes, 0f, null, false);
                Kit.BoxVisual(t, "FrameS", new Vector3(0f, 0.01f, -hz), new Vector3(size.x, 0.02f, w), MatKey.HazardStripes, 0f, null, false);
                Kit.BoxVisual(t, "FrameE", new Vector3(hx, 0.01f, 0f), new Vector3(w, 0.02f, size.y), MatKey.HazardStripes, 0f, null, false);
                Kit.BoxVisual(t, "FrameW", new Vector3(-hx, 0.01f, 0f), new Vector3(w, 0.02f, size.y), MatKey.HazardStripes, 0f, null, false);
            }
            if (plate.Reveal != null) plate.Reveal.Label = "JAMS AT >= " + Mathf.RoundToInt(minKg) + " KG";
            else PerspectiveKit.Reveal(go, PerspectiveMask.Strength, "JAMS AT >= " + Mathf.RoundToInt(minKg) + " KG", true, false, new Vector3(0f, 1.2f, 0f));
            return go;
        }

        // ------------------------------------------------------------------ telegraph visuals
        /// <summary>Floor telegraph: outline ring + growing fill disc (circle) and a lane (charge/volley) + SPD chip.
        /// The runner pools one per boss; radius sets the initial size.</summary>
        public static GameObject TelegraphDecal(Transform p, Vector3 pos, float radius)
        {
            var root = Kit.Make("BossTelegraph", p, pos);
            var t = root.transform;
            var disc = Kit.Make("Fill", t, new Vector3(0f, 0.01f, 0f));
            ProcMesh.AddRenderer(disc, ProcMesh.Cached("bk_tel_disc", () => ProcMesh.Cylinder(1f, 0.02f, 40)), Kit.Mat(MatKey.LightRed), false);
            var ring = Kit.Make("Ring", t, new Vector3(0f, 0.02f, 0f));
            ProcMesh.AddRenderer(ring, ProcMesh.Cached("bk_tel_ring", () => ProcMesh.Torus(1f, 0.035f, 48, 4)), Kit.Mat(MatKey.WarningRed), false);
            var lane = Kit.Make("Lane", t, Vector3.zero);
            ProcMesh.AddRenderer(lane, ProcMesh.CachedBox(new Vector3(1f, 0.02f, 1f), 0f), Kit.Mat(MatKey.WarningRed), false);
            var laneFill = Kit.Make("LaneFill", t, new Vector3(0f, 0.01f, 0f));
            ProcMesh.AddRenderer(laneFill, ProcMesh.CachedBox(new Vector3(1f, 0.02f, 1f), 0f), Kit.Mat(MatKey.LightRed), false);
            disc.transform.localScale = ring.transform.localScale = new Vector3(radius, 1f, radius);
            var view = root.AddComponent<BossTelegraphView>();
            view.Disc = disc.transform; view.Ring = ring.transform; view.Lane = lane.transform; view.LaneFill = laneFill.transform;
            var rev = root.AddComponent<Revealable>();
            rev.Category = PerspectiveMask.Speed;
            rev.Highlight = true;
            rev.LabelAlwaysVisible = true;
            rev.Label = "INCOMING";
            rev.LabelOffset = new Vector3(0f, 1.4f, 0f);
            view.Chip = rev;
            return root;
        }

        /// <summary>Expanding shockwave ring (torus radius 1, scaled by the runner).</summary>
        public static GameObject ShockwaveRing(Transform p, Vector3 pos)
        {
            var root = Kit.Make("BossShockwave", p, pos);
            ProcMesh.AddRenderer(Kit.Make("Ring", root.transform, Vector3.zero), ProcMesh.Cached("bk_shock_ring", () => ProcMesh.Torus(1f, 0.06f, 56, 6)), Kit.Mat(MatKey.Glow), false);
            ProcMesh.AddRenderer(Kit.Make("Inner", root.transform, new Vector3(0f, 0.1f, 0f)), ProcMesh.Cached("bk_shock_inner", () => ProcMesh.Torus(0.96f, 0.03f, 56, 4)), Kit.Mat(MatKey.WarningRed), false);
            return root;
        }

        /// <summary>Pooled volley projectile visual (no collider, no rigidbody: raycast-stepped by the runner).</summary>
        public static GameObject Projectile(Transform p, float radius)
        {
            var go = Kit.Make("BossProjectile", p, Vector3.zero);
            ProcMesh.AddRenderer(go, ProcMesh.Cached("bk_proj_" + radius.ToString("0.00"), () => ProcMesh.Sphere(Mathf.Max(0.1f, radius), 10, 7)), Kit.Mat(MatKey.LightRed), false);
            go.SetActive(false);
            return go;
        }

        // ------------------------------------------------------------------ set pieces
        /// <summary>Placeholder SOLO POD (105 kg gold hover-chair) with FleeBrain + Grabbable + WeightBody. loop: points LOCAL to p.
        /// visuals_bosses / level13 may swap the model (keep the root components).</summary>
        public static FleeBrain SoloPod(Transform p, Vector3 pos, Vector3[] loop)
        {
            var root = Kit.Make("SoloPod", p, pos);
            var t = root.transform;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = Tuning.SoloPodKg; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0f); col.size = new Vector3(1.2f, 1.1f, 1.2f);
            Kit.BoxVisual(t, "Seat", new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.4f, 1.2f), MatKey.Gold, 0.12f);
            Kit.BoxVisual(t, "Back", new Vector3(0f, 0.95f, -0.5f), new Vector3(1.2f, 1.0f, 0.2f), MatKey.Gold, 0.08f);
            Kit.BoxVisual(t, "Cushion", new Vector3(0f, 0.6f, 0.05f), new Vector3(1.0f, 0.12f, 0.95f), MatKey.Velvet, 0.05f);
            Kit.BoxVisual(t, "ArmL", new Vector3(-0.6f, 0.7f, 0f), new Vector3(0.15f, 0.3f, 1.0f), MatKey.Gold, 0.04f);
            Kit.BoxVisual(t, "ArmR", new Vector3(0.6f, 0.7f, 0f), new Vector3(0.15f, 0.3f, 1.0f), MatKey.Gold, 0.04f);
            ProcMesh.AddRenderer(Kit.Make("Thruster", t, new Vector3(0f, 0.05f, 0f)), ProcMesh.Cached("bk_pod_thruster", () => ProcMesh.Cylinder(0.45f, 0.1f, 20)), Kit.Mat(MatKey.Glow), false);
            PuzzleVisuals.Text(t, "Plate", new Vector3(0f, 1.25f, -0.39f), Quaternion.Euler(0f, 180f, 0f), "SOLO", 2.4f, UIColors.Background, 1f, 0.3f);
            var g = root.AddComponent<Grabbable>();
            g.DisplayName = "SOLO POD";
            root.AddComponent<WeightBody>();
            var fb = root.AddComponent<FleeBrain>();
            fb.Loop = loop ?? new Vector3[0];
            fb.Body = rb; fb.Grab = g;
            PerspectiveKit.Reveal(root, PerspectiveMask.Strength, Mathf.RoundToInt(Tuning.SoloPodKg) + " KG - HEAVY LIFTERS ONLY", true, false, new Vector3(0f, 1.8f, 0f));
            return fb;
        }

        /// <summary>Placeholder chase: a 4 x 7 m tram deck (kit MovingPlatform, driven by ChaseTrack) on track (LOCAL to p), a pursuer
        /// block and stations (PointIndex, Clear, CheckpointIndex).</summary>
        public static ChaseTrack Chase(Transform p, Vector3[] track, ChaseStation[] stations, Transform pursuer = null)
        {
            var root = Kit.Make("ChaseTrack", p, Vector3.zero);
            var tram = PuzzleKit.MovingPlatform(root.transform, track != null && track.Length > 0 ? track[0] : Vector3.zero, null,
                new[] { Vector3.zero, Vector3.forward }, new Vector3(4f, 0.4f, 7f), 0f, 0f);
            tram.name = "Tram";
            var ct = root.AddComponent<ChaseTrack>();
            ct.Tram = tram;
            ct.TrackPoints = track ?? new Vector3[0];
            ct.Stations = stations ?? new ChaseStation[0];
            ct.TramSpeed = Tuning.TramSpeed; ct.PursuerSpeed = Tuning.PursuitSpeed; ct.StartGap = Tuning.PursuitStartGap; ct.MaxGap = Tuning.PursuitMaxGap;
            if (pursuer == null)
            {
                var pu = Kit.Make("Pursuer", root.transform, Vector3.zero);
                Kit.BoxVisual(pu.transform, "Body", new Vector3(0f, 1.3f, 0f), new Vector3(2.6f, 2.2f, 3.4f), MatKey.SafetyOrange, 0.3f);
                Kit.BoxVisual(pu.transform, "Snout", new Vector3(0f, 1.4f, 1.85f), new Vector3(1.0f, 0.8f, 0.4f), MatKey.WarningRed, 0.15f);
                pursuer = pu.transform;
            }
            ct.Pursuer = pursuer;
            return ct;
        }

        // ------------------------------------------------------------------ the framework test boss
        /// <summary>"MEGA BONK-BOT 9000": a 3-phase placeholder boss (Shoot x2 / Punch / Hack) for framework tests and as
        /// a template for brains. Phase checkpoints 1/2/3 (set Phases[i].CheckpointIndex to your level's). Needs Arena set
        /// (and a "Center" mark) for safe-zone clamping; works without.</summary>
        public static BossBase TestBoss(Transform p, Vector3 pos)
        {
            var root = Kit.Make("MegaBonkBot9000", p, pos);
            var t = root.transform;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            var boss = root.AddComponent<MegaBonkBot>();
            // ---- placeholder model (visuals_bosses may replace it): treads, body, head, arms, eyes
            Kit.SolidBox(t, "Treads", new Vector3(0f, 0.35f, 0f), new Vector3(2.8f, 0.7f, 2.2f), MatKey.Rubber, 0.08f);
            Kit.SolidBox(t, "Body", new Vector3(0f, 1.9f, 0f), new Vector3(2.4f, 2.4f, 1.8f), MatKey.MetalDark, 0.12f);
            Kit.BoxVisual(t, "Belly", new Vector3(0f, 1.6f, 0.91f), new Vector3(1.9f, 1.4f, 0.06f), MatKey.HazardStripes, 0.02f);
            Kit.SolidBox(t, "Head", new Vector3(0f, 3.55f, 0f), new Vector3(1.6f, 0.9f, 1.4f), MatKey.MetalLight, 0.1f);
            Kit.BoxVisual(t, "EyeL", new Vector3(-0.38f, 3.62f, 0.71f), new Vector3(0.36f, 0.2f, 0.06f), MatKey.LightRed, 0.02f, null, false);
            Kit.BoxVisual(t, "EyeR", new Vector3(0.38f, 3.62f, 0.71f), new Vector3(0.36f, 0.2f, 0.06f), MatKey.LightRed, 0.02f, null, false);
            Kit.BoxVisual(t, "Antenna", new Vector3(0f, 4.3f, 0f), new Vector3(0.08f, 0.6f, 0.08f), MatKey.MetalLight, 0.01f);
            Kit.BoxVisual(t, "AntennaTip", new Vector3(0f, 4.65f, 0f), new Vector3(0.2f, 0.2f, 0.2f), MatKey.WarningRed, 0.05f, null, false);
            Kit.SolidBox(t, "ArmL", new Vector3(-1.6f, 2.1f, 0.3f), new Vector3(0.6f, 2.2f, 0.6f), MatKey.SafetyOrange, 0.08f);
            Kit.SolidBox(t, "ArmR", new Vector3(1.6f, 2.1f, 0.3f), new Vector3(0.6f, 2.2f, 0.6f), MatKey.SafetyOrange, 0.08f);
            Kit.SolidBox(t, "FistL", new Vector3(-1.6f, 0.75f, 0.45f), new Vector3(0.9f, 0.8f, 0.9f), MatKey.MetalLight, 0.12f);
            Kit.SolidBox(t, "FistR", new Vector3(1.6f, 0.75f, 0.45f), new Vector3(0.9f, 0.8f, 0.9f), MatKey.MetalLight, 0.12f);
            PuzzleVisuals.Text(t, "Name", new Vector3(0f, 3.05f, 0.93f), Quaternion.Euler(0f, 180f, 0f), "MEGA BONK-BOT 9000", 2.2f, UIColors.AccentYellow, 2.3f, 0.4f);
            // ---- weak points: P0 heart (shoot), P1 belly plate (punch), P2 hack port (terminal beside it)
            boss.Heart = WeakPointShoot(t, new Vector3(0f, 2.45f, 1.0f), 0.42f, boss, 1 << 0);
            boss.PunchPlate = WeakPointPunch(t, new Vector3(0f, 1.25f, 1.12f), boss, 1 << 1, 7);
            boss.HackPort = WeakPointHack(t, new Vector3(2.6f, 0f, 1.6f), Quaternion.identity, boss, 1 << 2);
            boss.Configure();
            return boss;
        }
    }
}
