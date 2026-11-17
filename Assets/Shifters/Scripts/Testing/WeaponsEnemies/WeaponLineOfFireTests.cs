using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Shifters
{
    /// <summary>DevPlay: LINE OF FIRE. Four shooters in parallel lanes - Brute, Sprinter, the 1.5 m Brainiac and a worst-case
    /// Brainiac (-4 % height, big head, short arms) - x every weapon kind x hip-fire (the FIRST shot of each burst leaves a
    /// relaxed, hanging gun) AND aimed (WeaponHolder.AimAt), at ShootTargets 8 / 12 / 16 m away behind a 1.05 m
    /// EnvKit.Railing 1.8 m in front of the shooter and at a target 2 m back on a 2.5 m ledge: every shot must clear the
    /// railing and hit. Also: worst-accuracy rolls never touch the railing, the visible gun is ON the shot line the frame
    /// the shot renders, FireAt with the body turned 60 deg away still clears, the pre-fix origin (the hanging gun's
    /// muzzle) would have been blocked (proves the scenario reproduces the bug), and the real player path
    /// (virtual mouse LMB hip-fire / RMB+LMB aimed through ShiftersCamera + PlayerBrain).
    /// Run: devplay.sh &lt;clone&gt; we_lof -devSetup Shifters.WeaponLineOfFireTests.Run -devSeconds 200
    ///   suite + this in ONE launch: -devSetup Shifters.WeaponLineOfFireTests.RunWithSuite -devSeconds 330</summary>
    public static class WeaponLineOfFireTests
    {
        static readonly WeaponKind[] ClassicKinds = { WeaponKind.Pistol, WeaponKind.Shotgun, WeaponKind.Laser, WeaponKind.Slingshot, WeaponKind.Chicken, WeaponKind.Plunger };
        /// <summary>Campaign kinds (CAMPAIGN 13; UPDATES_POST_SHIP rule 4): the 4 pool guns and the 4 locker tools.</summary>
        static readonly WeaponKind[] CampaignKinds = { WeaponKind.LeafBlower, WeaponKind.TShirtCannon, WeaponKind.SayCheese, WeaponKind.PaintBlaster,
                                                       WeaponKind.BitShifter, WeaponKind.SwapOMatic, WeaponKind.PartyPopper, WeaponKind.CapsLock };
        static readonly WeaponKind[] AllKinds = Concat(ClassicKinds, CampaignKinds);
        static WeaponKind[] Kinds = AllKinds;
        static WeaponKind[] Concat(WeaponKind[] a, WeaponKind[] b) { var r = new WeaponKind[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }
        static readonly string[] TargetNames = { "8m", "12m", "16m", "ledge" };
        const float RailZ = 1.8f, RailHeight = 1.05f, LedgeLipZ = -10f, LedgeTop = 2.5f;
        const int Ledge = 3;

        class Lane
        {
            public string Name;
            public ShifterCharacter Shooter;
            public Vector3 Spot;
            public WETestTarget Railing;
            public readonly ShootTarget[] Targets = new ShootTarget[4];
            public readonly float[] LastHit = { -9f, -9f, -9f, -9f };   // ShootTarget discs spin 720 deg (~0.63 s) after a hit
            public bool Done;
            public int Shots, Clean, LegacyBlocked, LegacyTotal, WorstShots, WorstRail;
            public float MinPivotY = 99f, MaxPivotY, MinMuzzleY = 99f, MaxGap, MaxFlashGap, LegacyMinY = 99f, LegacyMaxY = -99f;
            public readonly StringBuilder Fails = new StringBuilder();
        }

        static DevPlayContext _ctx;

        public static IEnumerator RunWithSuite(DevPlayContext ctx)
        {
            yield return WeaponsEnemiesSuite.Run(ctx);
            ClearScene();
            yield return null;
            yield return null;
            yield return Run(ctx);
        }

        /// <summary>Only the 8 campaign kinds (shorter run): -devSetup Shifters.WeaponLineOfFireTests.RunCampaignKinds -devSeconds 420</summary>
        public static IEnumerator RunCampaignKinds(DevPlayContext ctx) { Kinds = CampaignKinds; yield return Run(ctx); Kinds = AllKinds; }

        /// <summary>Only the 6 classic kinds (the pre-campaign scenario).</summary>
        public static IEnumerator RunClassicKinds(DevPlayContext ctx) { Kinds = ClassicKinds; yield return Run(ctx); Kinds = AllKinds; }

        public static IEnumerator Run(DevPlayContext ctx)
        {
            _ctx = ctx;
            float t0 = Time.time;
            WETestKit.Ground(150f);
            var lanes = new List<Lane>
            {
                BuildLane("Brute", Archetype.Brute, -54f, 0, 1f, 1f, 1f),
                BuildLane("Sprinter", Archetype.Sprinter, -36f, 1, 1f, 1f, 1f),
                BuildLane("Brainiac", Archetype.Brainiac, -18f, 2, 1f, 1f, 1f),
                BuildLane("Brainiac-min", Archetype.Brainiac, 0f, 3, 1f - Tuning.HeightJitter, 1.2f, 0.9f),
            };
            yield return ctx.Wait(0.6f);
            foreach (var L in lanes)
            {
                var g = L.Shooter.Weapon.FireGeometry;
                ctx.Log("LoF " + L.Name + " h " + L.Shooter.Height.ToString("0.00") + " m: pivot " + g.PivotHeight.ToString("0.00") + " m, side " + g.PivotSide.ToString("0.00")
                        + ", grip " + g.GripDistance.ToString("0.00") + ", muzzle " + g.MuzzleDistance.ToString("0.00") + " m along the shot line (pistol)");
            }
            WETestKit.Cam(new Vector3(-23.5f, 2.3f, -3.6f), new Vector3(-18f, 1.1f, 2.5f));
            ctx.Shot("lof_overview");

            var host = new GameObject("[WELoFHost]").AddComponent<WELoFHost>();
            for (int i = 0; i < lanes.Count; i++) host.StartCoroutine(RunLane(lanes[i], new System.Random(900 + i)));
            for (int f = 0; f < 60 * 1000; f++)
            {
                bool all = true;
                foreach (var L in lanes) all &= L.Done;
                if (all) break;
                yield return null;
            }

            foreach (var L in lanes)
            {
                string tag = "[LoF " + L.Name + "] ";
                ctx.Check(L.Done, tag + "lane finished");
                ctx.Check(L.Shots > 0 && L.Clean == L.Shots, tag + "EVERY shot (" + Kinds.Length + " kinds x hip+aimed x 8/12/16 m + ledge) cleared the railing and hit: "
                          + L.Clean + "/" + L.Shots + (L.Fails.Length > 0 ? " | " + L.Fails : ""));
                float wantPivot = Mathf.Max(WeaponAim.PivotHeightFraction * L.Shooter.Height - 0.01f, 1.1f);
                ctx.Check(L.MinPivotY >= wantPivot, tag + "shot pivot is stable at chest/shoulder height: " + L.MinPivotY.ToString("0.00") + ".." + L.MaxPivotY.ToString("0.00")
                          + " m (>= " + wantPivot.ToString("0.00") + "), muzzle point >= " + L.MinMuzzleY.ToString("0.00") + " m");
                ctx.Check(L.MaxGap < 0.15f, tag + "the visible gun sits ON the shot line the frame the shot renders (max socket gap " + L.MaxGap.ToString("0.000")
                          + " m, flash-to-gun-muzzle incl. recoil " + L.MaxFlashGap.ToString("0.00") + " m)");
                ctx.Check(L.WorstShots > 0 && L.WorstRail == 0, tag + "worst-accuracy rolls (pellet cones up to 5 deg) never touched the railing at 16 m: "
                          + L.WorstRail + " railing hits in " + L.WorstShots + " shots");
                ctx.Check(L.LegacyTotal > 0 && L.LegacyBlocked >= L.LegacyTotal * 0.9f, tag + "scenario reproduces the old bug: shots leaving the hanging gun's muzzle (y "
                          + L.LegacyMinY.ToString("0.00") + ".." + L.LegacyMaxY.ToString("0.00") + " m, the pre-fix origin) would hit the railing " + L.LegacyBlocked + "/" + L.LegacyTotal);
            }
            Object.Destroy(host.gameObject);

            yield return PlayerPath(ctx, 24f);
            ctx.Log("line-of-fire scenario done in " + (Time.time - t0).ToString("0") + " s game time");
        }

        // ------------------------------------------------------------------ lanes
        static Lane BuildLane(string name, Archetype a, float x, int slot, float heightMul, float headScale, float limbScale)
        {
            var L = new Lane { Name = name, Spot = new Vector3(x, 0f, 0f) };
            var root = new GameObject("LoF_" + name).transform;
            L.Railing = EnvKit.Railing(root, new Vector3(x - 6f, 0f, RailZ), new Vector3(x + 6f, 0f, RailZ), RailHeight).AddComponent<WETestTarget>();
            var face = Quaternion.Euler(0f, 180f, 0f);
            L.Targets[0] = PuzzleKit.ShootTarget(root, new Vector3(x - 2.5f, 0f, 8f), face, ShootTargetMode.Latch, 4f, true, "");
            L.Targets[1] = PuzzleKit.ShootTarget(root, new Vector3(x + 2.5f, 0f, 12f), face, ShootTargetMode.Latch, 4f, true, "");
            L.Targets[2] = PuzzleKit.ShootTarget(root, new Vector3(x, 0f, 16f), face, ShootTargetMode.Latch, 4f, true, "");
            // 2.5 m ledge behind the shooter: lip at z = -10, target board 2 m back, disc centre 0.5 m above the ledge top
            Kit.SolidBox(root, "Ledge", new Vector3(x, LedgeTop * 0.5f, LedgeLipZ - 2f), new Vector3(5f, LedgeTop, 4f), MatKey.Concrete);
            Kit.SolidBox(root, "TargetBoard", new Vector3(x, LedgeTop + 0.55f, LedgeLipZ - 2.25f), new Vector3(1.3f, 1.1f, 0.2f), MatKey.MetalDark);
            L.Targets[Ledge] = PuzzleKit.ShootTarget(root, new Vector3(x, LedgeTop + 0.5f, LedgeLipZ - 2f), Quaternion.identity, ShootTargetMode.Latch, 4f, false, "");

            var p = new CharacterProfile
            {
                Name = name, Archetype = a, PrimaryColor = Color.HSVToRGB(slot * 0.27f % 1f, 0.6f, 0.95f),
                Stats = new CharacterStats { Speed = a == Archetype.Sprinter ? 8 : 3, Strength = a == Archetype.Brute ? 8 : 3, IQ = a == Archetype.Brainiac ? 8 : 3 },
            };
            var mr = Tuning.MassRange(a);
            p.Mass = (mr.x + mr.y) * 0.5f;
            p.Body.Height = Tuning.BaseHeight(a) * heightMul;
            p.Body.Radius = Tuning.CapsuleRadius(a);
            p.Body.HeadScale = headScale;
            p.Body.LimbScale = limbScale;
            p.Weapon = WeaponGenerator.Generate(WeaponKind.Pistol, new System.Random(17));
            L.Shooter = CharacterFactory.Create(p, slot, L.Spot, Quaternion.identity);
            L.Shooter.ExternalControl = true;
            // no random idle fidgets (head scratch, stretch): the relaxed gun really hangs when the "old origin" is sampled
            if (L.Shooter.Visual != null && L.Shooter.Visual.Animator != null) L.Shooter.Visual.Animator.IdleStyle = 1;
            return L;
        }

        static IEnumerator RunLane(Lane L, System.Random rng)
        {
            var h = L.Shooter.Weapon;
            // 1) precision pass: Accuracy 1 (0.5 deg) so "every shot hits" measures the shot LINE, not spread luck
            foreach (var kind in Kinds)
            {
                var data = WeaponGenerator.Generate(kind, rng);
                data.Accuracy = 1f;
                h.Equip(data);
                yield return Frames(3);
                for (int mode = 0; mode < 2; mode++)
                    for (int ti = 0; ti < 4; ti++)
                        yield return Burst(L, kind, mode == 1, ti, 2, true, 0f);
            }
            // 2) the body turned 60 deg away (bots that don't face): the shot line is built in the AIM frame
            h.Equip(Precise(WeaponKind.Pistol, rng));
            yield return Frames(3);
            yield return Burst(L, WeaponKind.Pistol, false, 1, 1, true, 60f);
            yield return Burst(L, WeaponKind.Pistol, false, 2, 1, true, -60f);
            // 3) worst rolls (widest cones): nothing may touch the railing (hits are spread luck here, only logged)
            foreach (var kind in Kinds)
            {
                if (WeaponStyle.IsTool(kind) || kind == WeaponKind.LeafBlower || kind == WeaponKind.SayCheese) continue;   // fixed-stat tools / cone guns: no accuracy roll
                var data = WeaponGenerator.Generate(kind, rng);
                data.Accuracy = MinAccuracy(kind);
                h.Equip(data);
                yield return Frames(3);
                yield return Burst(L, kind, false, 2, 3, false, 0f);
            }
            h.AimAt(null);
            L.Done = true;
        }

        static WeaponData Precise(WeaponKind k, System.Random rng) { var d = WeaponGenerator.Generate(k, rng); d.Accuracy = 1f; return d; }

        static float MinAccuracy(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Pistol: return 0.75f;
                case WeaponKind.Shotgun: return 0.4f;
                case WeaponKind.Laser: return 0.95f;
                case WeaponKind.Slingshot: return 0.8f;
                case WeaponKind.Chicken: return 0.7f;
                case WeaponKind.TShirtCannon: return 0.8f;
                case WeaponKind.PaintBlaster: return 0.8f;
                default: return 0.9f;
            }
        }

        /// <summary>A burst of 'shots' FireAt shots at target ti. Hip: waits until the gun hangs relaxed first (the bug was
        /// the FIRST shot of a burst). Aimed: AimAt(target) first. bodyYaw != 0 turns the body away from the target.</summary>
        static IEnumerator Burst(Lane L, WeaponKind kind, bool aimed, int ti, int shots, bool precision, float bodyYaw)
        {
            var c = L.Shooter;
            var h = c.Weapon;
            var target = L.Targets[ti];
            Vector3 aim = target.Disc != null ? target.Disc.position : target.transform.position;
            Vector3 flat = aim - L.Spot; flat.y = 0f;
            c.Motor.Teleport(L.Spot, Quaternion.AngleAxis(bodyYaw, Vector3.up) * Quaternion.LookRotation(flat.normalized));
            yield return Frames(4);
            string tag = L.Name + "/" + kind + "/" + (aimed ? "aimed" : "hip") + "/" + TargetNames[ti] + (bodyYaw != 0f ? "/turned" + bodyYaw.ToString("0") : "");
            if (aimed) { h.AimAt(aim); yield return Seconds(0.35f); }
            else
            {
                h.AimAt(null);
                yield return WaitFor(() => !h.WantsAimPose && h.AimPoseWeight <= 0f, 3f);
                yield return Seconds(0.3f);   // the animator's crossfade back to the hanging arm
                // the pre-fix shot origin: the muzzle of the gun hanging straight down the relaxed arm (model at its base
                // pose in the hand socket - before the fix shots left from there; today's relaxed carry tilts it forward)
                var sock0 = c.Visual != null ? c.Visual.WeaponSocket : null;
                Vector3 legacy = sock0 != null ? sock0.position + sock0.rotation * h.FireGeometry.MuzzleInSocket : h.GunMuzzlePosition;
                {   // ...clamped exactly like the old SafeOrigin (centre -> muzzle ray): a muzzle poking into the floor fired from the floor
                    Vector3 to = legacy - c.Center; float len = to.magnitude;
                    if (len > 0.05f && WeaponBallistics.Raycast(c.Center, to / len, len, c.transform, out var so)) legacy = so.point - to / len * 0.05f;
                }
                float ly = legacy.y - c.Feet.y;
                L.LegacyMinY = Mathf.Min(L.LegacyMinY, ly); L.LegacyMaxY = Mathf.Max(L.LegacyMaxY, ly);
                if (ti != Ledge && precision && bodyYaw == 0f)
                {
                    L.LegacyTotal++;
                    if (WeaponBallistics.Raycast(legacy, (aim - legacy).normalized, Vector3.Distance(legacy, aim), c.transform, out var lh)
                        && lh.collider.GetComponentInParent<WETestTarget>() == L.Railing) L.LegacyBlocked++;
                }
            }

            for (int s = 0; s < shots; s++)
            {
                yield return WaitFor(() => h.IsReady && Time.time - L.LastHit[ti] > 0.7f, 3f);   // disc face-on again
                int rail0 = L.Railing.Hits, hits0 = target.Hits, active0 = WeaponProjectile.Active.Count;
                bool relaxed = !aimed && s == 0 && h.AimPoseWeight <= 0f;
                if (!h.FireAt(aim)) { Fail(L, tag + " #" + s + " FireAt refused"); continue; }
                WeaponProjectile proj = WeaponStyle.IsProjectile(kind) && WeaponProjectile.Active.Count > active0 ? WeaponProjectile.Active[WeaponProjectile.Active.Count - 1] : null;
                Vector3 launch = proj != null ? proj.LaunchPosition : Vector3.zero;   // pooled: another lane may reuse it later
                float pivotY = h.LastShotPivot.y - c.Feet.y, muzzleY = h.LastShotMuzzle.y - c.Feet.y;
                bool railHit = L.Railing.Hits > rail0;   // hitscan resolves inside FireAt: any pellet on the railing is a blocked shot
                bool shotMe = WantShot(L, kind, aimed, ti, s, bodyYaw, precision);

                yield return null;   // this frame rendered: the arm pose ran after the animator
                var sock = c.Visual != null ? c.Visual.WeaponSocket : null;
                if (sock != null && precision)
                {
                    Vector3 restMuzzle = sock.position + sock.rotation * h.FireGeometry.MuzzleInSocket;
                    L.MaxGap = Mathf.Max(L.MaxGap, Vector3.Distance(restMuzzle, h.LastShotMuzzle));
                    L.MaxFlashGap = Mathf.Max(L.MaxFlashGap, Vector3.Distance(h.GunMuzzlePosition, h.LastShotMuzzle));
                }
                if (shotMe) Snap(L, tag + (relaxed ? "_first" : ""), ti);

                if (proj != null)
                {
                    for (int f = 0; f < 200 && proj.InFlight && proj.LaunchPosition == launch && target.Hits == hits0; f++)
                    {
                        if (L.Railing.Hits > rail0) { railHit = true; break; }
                        yield return null;
                    }
                    if (L.Railing.Hits > rail0 && target.Hits == hits0) railHit = true;
                    // a chicken bouncing back off the target must not count as "blocked" later
                    if (proj.InFlight && proj.LaunchPosition == launch) proj.Despawn(false);
                }
                bool hit = target.Hits > hits0;
                if (hit) L.LastHit[ti] = Time.time;
                if (precision)
                {
                    L.Shots++;
                    L.MinPivotY = Mathf.Min(L.MinPivotY, pivotY); L.MaxPivotY = Mathf.Max(L.MaxPivotY, pivotY);
                    L.MinMuzzleY = Mathf.Min(L.MinMuzzleY, muzzleY);
                    if (hit && !railHit) L.Clean++;
                    else Fail(L, tag + " #" + s + (relaxed ? " (first, relaxed)" : "") + (railHit ? " RAILING" : "") + (!hit ? " MISS" : "")
                                 + " pivotY " + pivotY.ToString("0.00") + " muzzleY " + muzzleY.ToString("0.00"));
                }
                else
                {
                    L.WorstShots++;
                    if (railHit) { L.WorstRail++; Fail(L, tag + " worst-roll #" + s + " touched the railing"); }
                }
                if (s == 0 && precision && bodyYaw == 0f)
                    _ctx.Check(hit && !railHit, "[LoF " + tag + "] first shot" + (relaxed ? " (gun was hanging)" : "") + " cleared the railing + hit (pivot "
                               + pivotY.ToString("0.00") + " m, muzzle " + muzzleY.ToString("0.00") + " m)");
            }
            if (aimed) h.AimAt(null);
        }

        static void Fail(Lane L, string what) { if (L.Fails.Length < 1500) L.Fails.Append(what).Append("; "); }

        // ------------------------------------------------------------------ screenshots (a few representative frames)
        static bool WantShot(Lane L, WeaponKind kind, bool aimed, int ti, int s, float yaw, bool precision)
        {
            if (!precision || s != 0 || yaw != 0f) return false;
            if (L.Name == "Brainiac") return (kind == WeaponKind.Pistol && ti == 1) || (kind == WeaponKind.Shotgun && ti == 2 && aimed) || (kind == WeaponKind.Plunger && ti == Ledge && !aimed);
            if (L.Name == "Brute") return (kind == WeaponKind.Laser && ti == 0 && !aimed) || (WeaponStyle.IsTool(kind) && ti == 1 && aimed);
            if (L.Name == "Sprinter" && ti == 2 && !aimed && (kind == WeaponKind.LeafBlower || kind == WeaponKind.TShirtCannon || kind == WeaponKind.SayCheese || kind == WeaponKind.PaintBlaster)) return true;
            if (L.Name == "Sprinter") return kind == WeaponKind.Pistol && ti == 1 && !aimed;
            if (L.Name == "Brainiac-min") return kind == WeaponKind.Shotgun && ti == 2 && !aimed;
            return false;
        }

        static void Snap(Lane L, string name, int ti)
        {
            Vector3 p = L.Spot;
            if (ti == Ledge) WETestKit.Cam(p + new Vector3(4.2f, 2.4f, 2.6f), p + new Vector3(0f, 1.7f, -5f));
            else WETestKit.Cam(p + new Vector3(3.4f, 1.9f, -2.2f), p + new Vector3(0.2f, 1.15f, 2.6f));
            _ctx.Shot("lof_" + name.Replace('/', '_'));
        }

        // ------------------------------------------------------------------ the real player path (input -> camera -> holder)
        static IEnumerator PlayerPath(DevPlayContext ctx, float x)
        {
            var root = new GameObject("LoF_Player").transform;
            var railing = EnvKit.Railing(root, new Vector3(x - 6f, 0f, RailZ), new Vector3(x + 6f, 0f, RailZ), RailHeight).AddComponent<WETestTarget>();
            var t12 = PuzzleKit.ShootTarget(root, new Vector3(x + 2.5f, 0f, 12f), Quaternion.Euler(0f, 180f, 0f), ShootTargetMode.Latch, 4f, true, "");
            var cam = Camera.main;
            if (cam == null) { ctx.Check(false, "[LoF player] needs a main camera"); yield break; }
            var sc = cam.GetComponent<ShiftersCamera>();
            if (sc == null) sc = cam.gameObject.AddComponent<ShiftersCamera>();
            var mgr = CharacterManager.Instance;
            if (mgr == null) mgr = new GameObject("CharacterManager").AddComponent<CharacterManager>();
            var team = new TeamData { Seed = 77 };
            var arch = new[] { Archetype.Brainiac, Archetype.Brute, Archetype.Sprinter };
            for (int i = 0; i < 3; i++)
            {
                var a = arch[i];
                var pr = new CharacterProfile { Name = "Player" + i, Archetype = a, PrimaryColor = Color.HSVToRGB(0.1f + i * 0.3f, 0.6f, 0.95f) };
                pr.Body.Height = Tuning.BaseHeight(a); pr.Body.Radius = Tuning.CapsuleRadius(a);
                var mr = Tuning.MassRange(a); pr.Mass = (mr.x + mr.y) * 0.5f;
                team.Members[i] = pr;
            }
            var spot = new Vector3(x, 0f, 0f);
            var spawns = new List<Pose> { new Pose(spot, Quaternion.identity), new Pose(new Vector3(x - 4f, 0f, -14f), Quaternion.identity), new Pose(new Vector3(x + 4f, 0f, -14f), Quaternion.identity) };
            yield return null;
            mgr.SpawnTeam(team, spawns, 0);
            yield return null;
            yield return null;
            var c = mgr.Get(0);
            if (c == null || c.Weapon == null) { ctx.Check(false, "[LoF player] team spawned"); yield break; }
            for (int i = 1; i < mgr.Team.Count; i++) if (mgr.Team[i] != null) { mgr.Team[i].ExternalControl = true; if (mgr.Team[i].Weapon.HasWeapon) mgr.Team[i].Weapon.TakeWeapon(); }
            c.ExternalControl = false;
            ShiftersInput.Context = InputContext.Gameplay;
            ctx.Check(c.IsActive && ShiftersCamera.Instance == sc, "[LoF player] Brainiac is the active, player-controlled character with the gameplay camera");

            var settings = InputSystem.settings;
            var oldBg = settings.backgroundBehavior;
            var oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>("WELoFMouse");
            yield return null;

            var rng = new System.Random(55);
            Vector3 aim = t12.Disc.position;
            foreach (var kind in new[] { WeaponKind.Pistol, WeaponKind.Chicken, WeaponKind.Plunger })
            {
                c.Weapon.Equip(Precise(kind, rng));
                // ---- hip-fire: LMB click with the body turned 70 deg away, crosshair on the target
                float targetYaw = Mathf.Atan2(aim.x - spot.x, aim.z - spot.z) * Mathf.Rad2Deg;
                c.Motor.Teleport(spot, Quaternion.Euler(0f, targetYaw + 70f, 0f));
                yield return AimCamera(sc, aim, 40);
                yield return WaitFor(() => c.Weapon.IsReady && !c.Weapon.WantsAimPose, 3f);
                bool crosshairOk = Vector3.Distance(c.Weapon.AimPoint, aim) < 0.6f;
                int shots0 = c.Weapon.ShotsFired, hits0 = t12.Hits, rail0 = railing.Hits;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                bool fired = c.Weapon.ShotsFired > shots0;
                bool hipAim = c.Weapon.IsHipAiming && !c.Weapon.IsAiming && !sc.IsAiming;
                yield return Resolve(kind, t12, hits0, railing, rail0);
                yield return Seconds(0.25f);
                float turn = Vector3.Angle(Flat(c.transform.forward), Flat(aim - c.Feet));
                bool hipOk = fired && t12.Hits > hits0 && railing.Hits == rail0;
                ctx.Check(crosshairOk && hipOk && hipAim, "[LoF player] " + kind + " hip-fire (LMB, body 70 deg off): crosshair on target " + crosshairOk + ", fired " + fired
                          + ", hip-aim window (no camera zoom) " + hipAim + ", hit " + (t12.Hits > hits0) + ", railing untouched " + (railing.Hits == rail0));
                ctx.Check(turn < 25f, "[LoF player] " + kind + " hip-fire turns the body toward the shot (" + turn.ToString("0") + " deg off after 0.3 s)");

                // ---- aimed: hold RMB, crosshair on target, click LMB
                yield return WaitFor(() => !c.Weapon.WantsAimPose, 3f);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return Seconds(0.45f);
                yield return AimCamera(sc, aim, 30);
                bool aiming = c.Weapon.IsAiming && sc.IsAiming && c.Visual.EffectivePose == PoseOverride.Aiming;
                yield return WaitFor(() => c.Weapon.IsReady, 3f);
                shots0 = c.Weapon.ShotsFired; hits0 = t12.Hits; rail0 = railing.Hits;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                fired = c.Weapon.ShotsFired > shots0;
                if (kind == WeaponKind.Pistol) { yield return null; ctx.Shot("lof_player_aimed_pistol"); }
                yield return Resolve(kind, t12, hits0, railing, rail0);
                ctx.Check(aiming && fired && t12.Hits > hits0 && railing.Hits == rail0, "[LoF player] " + kind + " aimed (RMB held + LMB): aim camera + pose " + aiming
                          + ", fired " + fired + ", hit " + (t12.Hits > hits0) + ", railing untouched " + (railing.Hits == rail0));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return Seconds(0.3f);
                ctx.Check(!c.Weapon.IsAiming && !sc.IsAiming, "[LoF player] " + kind + " releasing RMB ends the aim (holder + camera)");
            }
            InputSystem.RemoveDevice(mouse);
            settings.backgroundBehavior = oldBg;
            settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
        }

        static IEnumerator Resolve(WeaponKind kind, ShootTarget target, int hits0, WETestTarget railing, int rail0)
        {
            if (!WeaponStyle.IsProjectile(kind)) { yield return null; yield break; }
            for (int f = 0; f < 200 && target.Hits == hits0 && railing.Hits == rail0; f++) yield return null;
            for (int i = WeaponProjectile.Active.Count - 1; i >= 0; i--) { var p = WeaponProjectile.Active[i]; if (p != null && p.InFlight) p.Despawn(false); }
        }

        /// <summary>Point the gameplay camera so its crosshair ray passes through 'point' (orbit cam: iterate).</summary>
        static IEnumerator AimCamera(ShiftersCamera sc, Vector3 point, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                Vector3 d = point - sc.transform.position;
                float planar = new Vector2(d.x, d.z).magnitude;
                sc.Yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                sc.Pitch = Mathf.Clamp(-Mathf.Atan2(d.y, planar) * Mathf.Rad2Deg, Tuning.CamPitchMin, Tuning.CamPitchMax);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ helpers
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) { int n = Mathf.Max(1, Mathf.RoundToInt(s * 60f)); for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> cond, float timeout)
        {
            int n = Mathf.CeilToInt(timeout * 60f);
            for (int i = 0; i < n && !cond(); i++) yield return null;
        }

        static void ClearScene()
        {
            WeaponPickup.FallbackSpawn = null;
            var cam = Camera.main;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (cam != null && go == cam.gameObject) continue;
                var light = go.GetComponent<Light>();
                if (light != null && light.type == LightType.Directional) continue;
                Object.Destroy(go);
            }
        }
    }

    /// <summary>Coroutine host for the parallel line-of-fire lanes (test only).</summary>
    public class WELoFHost : MonoBehaviour { }
}
