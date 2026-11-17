// =====================================================================================
//  SHIFTERS - TeamBot.Verbs.cs   [BOTS module - campaign wave verbs, CAMPAIGN §18.8]
//    FaceAndHold(c, target, s)            turn toward a (moving) target and keep facing it for s seconds
//    Guard(c, stalkers, until)            flashlight duty: c becomes the ACTIVE intern (the flashlight rides on the
//                                         active one) and keeps facing the nearest stalker within 12 m until until()
//    WithGuard(main, c, stalkers)         run main while c guards; the guard stops when main ends
//    Chase(c, target, sprint)             moving-target TAG: steer at the target's (predicted) position until within
//                                         tagRadius (or until() is true)
//    WaitCameraClear(cam, center, r, s)   wait until SecurityCamera cam cannot see the disc (center, r) for the next s
//                                         seconds (SecurityCamera.CouldSee(p, horizon); geometric fallback)
//    RideUntil(platform, predicate)       riders stay on a moving platform (re-centre when they drift) until predicate
//    Race(a, b, ...)                      run routines together, finish when the FIRST finishes (others aborted)
//  Cross-module types (SecurityCamera, StalkerBrain, MovingPlatform) are taken as Component and used by reflection,
//  so this compiles and runs while those modules are absent.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed partial class TeamBot
    {
        // ------------------------------------------------------------------ FaceAndHold
        /// <summary>Turn toward the target and keep facing it (tracking a moving target) for seconds, or until until().
        /// aim = also point the gun arm at it (WeaponHolder.AimAt) while holding.</summary>
        public IEnumerator FaceAndHold(ShifterCharacter c, BotTarget target, float seconds, Func<bool> until = null, bool aim = false)
        {
            var s = Begin("FaceAndHold", c, target.Describe() + " " + BotFmt.F(seconds) + "s");
            if (!Ready(s, c)) yield break;
            if (!target.IsValid) { FailStep(s, "target not found: " + target.Describe(), c); yield break; }
            float t0 = Now;
            Driver.SetMove(c, Vector3.zero, false);
            try
            {
                while (Now - t0 < seconds)
                {
                    if (until != null && until()) break;
                    var b = Blocker(c);
                    if (b != null && b != WaitToken) { FailStep(s, b, c); yield break; }
                    Vector3 p = target.Position;
                    Driver.SetFace(c, p - c.Feet);
                    if (aim && c.Weapon != null && c.Weapon.HasWeapon) c.Weapon.AimAt(p);
                    yield return null;
                }
            }
            finally
            {
                Driver.SetFace(c, Vector3.zero);
                if (aim && c != null && c.Weapon != null) c.Weapon.AimAt(null);
            }
            End(s, true, "held " + BotFmt.F(Now - t0) + "s");
        }

        // ------------------------------------------------------------------ Guard (flashlight duty)
        /// <summary>All StalkerBrain components in the scene (empty while the enemies module has none).</summary>
        public static List<Component> FindStalkers()
        {
            var list = new List<Component>();
            foreach (var mb in BotReflect.FindAll("StalkerBrain")) if (mb != null) list.Add(mb);
            return list;
        }

        /// <summary>Flashlight duty (run it in Parallel / WithGuard): c becomes the ACTIVE intern (the FlashlightRig rides
        /// on the active one) and, standing still, keeps facing the nearest stalker within range (default 12 m;
        /// stalkers null = every StalkerBrain). Ends when until() is true (until null: never - use WithGuard / Race).</summary>
        public IEnumerator Guard(ShifterCharacter c, IEnumerable<Component> stalkers = null, Func<bool> until = null, float range = 12f, bool makeActive = true)
        {
            var s = Begin("Guard", c, "range " + BotFmt.F(range));
            if (!Ready(s, c)) yield break;
            if (makeActive) { yield return SwitchCore(c, 4f, s); if (s.Ended) yield break; }
            var list = new List<Component>();
            if (stalkers != null) foreach (var st in stalkers) if (st != null) list.Add(st);
            bool auto = stalkers == null;
            float t0 = Now, lastScan = -99f;
            int switches = 0;
            Component lastFocus = null;
            Driver.SetMove(c, Vector3.zero, false);
            try
            {
                while (until == null || !until())
                {
                    var b = Blocker(c);
                    if (b != null && b != WaitToken) { FailStep(s, "guard down: " + b, c); yield break; }
                    if (auto && Now - lastScan > 1f) { list = FindStalkers(); lastScan = Now; }
                    Component best = null;
                    float bestD = range;
                    foreach (var st in list)
                    {
                        if (st == null || !st.gameObject.activeInHierarchy) continue;
                        float d = Vector3.Distance(st.transform.position, c.Center);
                        if (d < bestD) { bestD = d; best = st; }
                    }
                    if (best != null)
                    {
                        if (best != lastFocus) { switches++; lastFocus = best; }
                        Driver.SetFace(c, best.transform.position - c.Feet);
                    }
                    yield return null;
                }
            }
            finally { if (c != null) Driver.SetFace(c, Vector3.zero); }
            End(s, true, BotFmt.F(Now - t0) + "s, " + switches + " target change(s)");
        }

        /// <summary>Run main while c guards (Guard ends when main ends).</summary>
        public IEnumerator WithGuard(IEnumerator main, ShifterCharacter c, IEnumerable<Component> stalkers = null, float range = 12f)
        {
            bool done = false;
            yield return Parallel(Then(main, () => done = true), Guard(c, stalkers, () => done, range));
        }

        static IEnumerator Then(IEnumerator routine, Action after)
        {
            if (routine != null) yield return routine;
            after?.Invoke();
        }

        /// <summary>Run routines together; finish as soon as the FIRST one finishes (the others are aborted; their
        /// characters are stopped).</summary>
        public IEnumerator Race(params IEnumerator[] routines)
        {
            var list = new List<BotRoutine>();
            foreach (var r in routines) if (r != null) list.Add(new BotRoutine(r, () => Failed && !Options.ContinueAfterFailure));
            while (list.Count > 0)
            {
                bool finished = false;
                foreach (var r in list)
                {
                    if (r.Done) { finished = true; continue; }
                    if (!r.Step()) finished = true;
                    if (r.Error != null) { Debug.LogException(r.Error); Fail("EXCEPTION in race action: " + r.Error.Message); }
                }
                if (Failed && !Options.ContinueAfterFailure) finished = true;
                if (finished) { foreach (var r in list) if (!r.Done) r.Abort(); break; }
                yield return null;
            }
            foreach (var c in Team) if (c != null) Driver.SetMove(c, Vector3.zero, false);
        }

        // ------------------------------------------------------------------ Chase (moving-target TAG)
        /// <summary>Moving-target TAG: steer at the target's position (led by its velocity) until within tagRadius
        /// (planar, |dy| &lt; 1.5 m) or until() is true. Fails on timeout or when c goes down.</summary>
        public IEnumerator Chase(ShifterCharacter c, BotTarget target, bool sprint = true, float tagRadius = 1.3f, float timeout = 30f, Func<bool> until = null)
        {
            var s = Begin("Chase", c, target.Describe());
            if (!Ready(s, c)) yield break;
            if (!target.IsValid) { FailStep(s, "target not found: " + target.Describe(), c); yield break; }
            float t0 = Now, closest = float.MaxValue;
            Vector3 prev = target.Position;
            Vector3 vel = Vector3.zero;
            while (true)
            {
                var b = Blocker(c);
                if (b == WaitToken) { Stop(c); yield return null; continue; }
                if (b != null) { Stop(c); FailStep(s, b, c); yield break; }
                Vector3 p = target.Position;
                float dt = Mathf.Max(Time.deltaTime, 1e-3f);
                vel = Vector3.Lerp(vel, (p - prev) / dt, 0.2f);
                prev = p;
                Vector3 to = Flat(p - c.Feet);
                float d = to.magnitude;
                closest = Mathf.Min(closest, d);
                if ((until != null && until()) || (until == null && d <= tagRadius && Mathf.Abs(p.y - c.Feet.y) < 1.5f)) break;
                if (Now - t0 > timeout) { Stop(c); FailStep(s, "no tag in " + timeout + " s (closest " + BotFmt.F(closest) + " m, now " + BotFmt.F(d) + " m)", c); yield break; }
                float speed = c.Motor != null ? Mathf.Max(1f, c.Motor.PlanarVelocity.magnitude) : 5f;
                float lead = Mathf.Clamp(d / Mathf.Max(speed, 3f), 0f, 1.2f);
                Vector3 aimP = p + Flat(vel) * lead;
                Driver.SetMove(c, FlatDir(aimP - c.Feet, to), sprint);
                Driver.SetFace(c, Vector3.zero);
                yield return null;
            }
            Stop(c);
            End(s, true, "tag at " + BotFmt.F(Flat(target.Position - c.Feet).magnitude) + " m after " + BotFmt.F(Now - t0) + "s");
        }

        // ------------------------------------------------------------------ WaitCameraClear
        static readonly object[] _couldSeeArgs = new object[2];

        /// <summary>Could the security camera see world point p within the next horizon seconds? Uses
        /// SecurityCamera.CouldSee(Vector3, float) when present, else a geometric cone test on the camera's current
        /// forward (range Tuning.CameraRange, fov Tuning.CameraFov, with a margin for the horizon).</summary>
        public static bool CameraCouldSee(Component cam, Vector3 p, float horizon)
        {
            if (cam == null || !cam.gameObject.activeInHierarchy) return false;
            var m = cam.GetType().GetMethod("CouldSee", new[] { typeof(Vector3), typeof(float) });
            if (m != null)
            {
                _couldSeeArgs[0] = p; _couldSeeArgs[1] = horizon;
                try { return (bool)m.Invoke(cam, _couldSeeArgs); } catch (Exception) { }
            }
            float range = BotReflect.TryGet(cam, "Range", out float r) ? r : Tuning.CameraRange;
            float fov = BotReflect.TryGet(cam, "Fov", out float f) ? f : Tuning.CameraFov;
            Vector3 to = p - cam.transform.position;
            if (to.magnitude > range) return false;
            return Vector3.Angle(Flat(cam.transform.forward), Flat(to)) < fov * 0.5f + 20f * Mathf.Clamp01(horizon / 2f);
        }

        /// <summary>Wait until the camera cannot see the disc (center, radius: centre + 8 rim points) for the next
        /// seconds. Fails after timeout.</summary>
        public IEnumerator WaitCameraClear(Component cam, BotTarget center, float radius, float seconds, float timeout = 30f)
        {
            var s = Begin("WaitCameraClear", null, BotFmt.Obj(cam) + " @" + center.Describe() + " r" + BotFmt.F(radius) + " " + BotFmt.F(seconds) + "s");
            if (s == null) yield break;
            if (cam == null) { End(s, true, "(no camera - clear)"); yield break; }
            float t0 = Now;
            while (true)
            {
                if (!DiscVisible(cam, center.Position, radius, seconds)) break;
                if (Now - t0 > timeout) { FailStep(s, "camera never clear in " + timeout + " s"); yield break; }
                yield return null;
            }
            End(s, true, "clear after " + BotFmt.F(Now - t0) + "s");
        }

        /// <summary>Same for several cameras (all must be clear at once).</summary>
        public IEnumerator WaitCamerasClear(IEnumerable<Component> cams, BotTarget center, float radius, float seconds, float timeout = 30f)
        {
            var list = new List<Component>(cams ?? new Component[0]);
            var s = Begin("WaitCameraClear", null, list.Count + " cams @" + center.Describe());
            if (s == null) yield break;
            float t0 = Now;
            while (true)
            {
                bool any = false;
                foreach (var cam in list) if (DiscVisible(cam, center.Position, radius, seconds)) { any = true; break; }
                if (!any) break;
                if (Now - t0 > timeout) { FailStep(s, "cameras never clear in " + timeout + " s"); yield break; }
                yield return null;
            }
            End(s, true, "clear after " + BotFmt.F(Now - t0) + "s");
        }

        static bool DiscVisible(Component cam, Vector3 c, float radius, float horizon)
        {
            if (CameraCouldSee(cam, c, horizon)) return true;
            if (radius <= 0.01f) return false;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                if (CameraCouldSee(cam, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, horizon)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ RideUntil
        /// <summary>Ride a moving platform (MovingPlatform / any mover) until predicate() is true: every rider (default:
        /// the alive team standing on it) holds still and walks back toward the platform's centre when it drifts more
        /// than keepRadius (default: 70% of the smaller deck half-size). Fails on timeout or when a rider falls off.</summary>
        public IEnumerator RideUntil(Component platform, Func<bool> predicate, float timeout = 60f, IList<ShifterCharacter> riders = null, float keepRadius = -1f)
        {
            var s = Begin("RideUntil", null, BotFmt.Obj(platform));
            if (s == null) yield break;
            if (platform == null) { FailStep(s, "platform is null"); yield break; }
            if (predicate == null) { FailStep(s, "predicate is null"); yield break; }
            var list = new List<ShifterCharacter>();
            if (riders != null) { foreach (var r in riders) if (r != null) list.Add(r); }
            else foreach (var c in Team) if (c.IsAlive && OnDeck(platform, c)) list.Add(c);
            if (list.Count == 0) { FailStep(s, "nobody is on " + BotFmt.Obj(platform)); yield break; }
            float t0 = Now;
            while (true)
            {
                bool ok;
                try { ok = predicate(); }
                catch (Exception e) { FailStep(s, "predicate threw " + e.Message); yield break; }
                if (ok) break;
                if (Now - t0 > timeout) { FailStep(s, "timed out after " + timeout + " s"); yield break; }
                var b = DeckBounds(platform);
                float keep = keepRadius > 0f ? keepRadius : Mathf.Max(0.4f, Mathf.Min(b.extents.x, b.extents.z) * 0.7f);
                foreach (var c in list)
                {
                    if (c == null) continue;
                    if (!c.IsAlive) { FailStep(s, c.DisplayName + " went " + c.State + " while riding", c); yield break; }
                    if (c.Feet.y < b.max.y - 2.5f) { FailStep(s, c.DisplayName + " fell off the platform at " + BotFmt.V(c.Feet), c); yield break; }
                    Vector3 to = Flat(b.center - c.Feet);
                    Driver.SetMove(c, to.magnitude > keep ? to.normalized * 0.6f : Vector3.zero, false);
                }
                yield return null;
            }
            foreach (var c in list) if (c != null) Stop(c);
            End(s, true, list.Count + " rider(s), " + BotFmt.F(Now - t0) + "s");
        }

        static Bounds DeckBounds(Component platform)
        {
            var cols = platform.GetComponentsInChildren<Collider>();
            bool any = false;
            var b = new Bounds(platform.transform.position, Vector3.one);
            foreach (var col in cols)
            {
                if (col == null || col.isTrigger || !col.enabled) continue;
                if (!any) { b = col.bounds; any = true; } else b.Encapsulate(col.bounds);
            }
            return b;
        }

        /// <summary>Is c standing on (or within the footprint just above) the platform?</summary>
        public static bool OnDeck(Component platform, ShifterCharacter c)
        {
            if (platform == null || c == null) return false;
            var b = DeckBounds(platform);
            Vector3 f = c.Feet;
            return f.x > b.min.x - 0.2f && f.x < b.max.x + 0.2f && f.z > b.min.z - 0.2f && f.z < b.max.z + 0.2f && Mathf.Abs(f.y - b.max.y) < 0.8f;
        }

        // ------------------------------------------------------------------ tools (ShootAt helpers)
        /// <summary>The holder's weapon is a TOOL (BIT-SHIFTER, SWAP-O-MATIC, PARTY POPPER, CAPS-LOCK) - §13.1 W1.</summary>
        public static bool IsTool(WeaponData d) =>
            d != null && d.Type == WeaponType.Experimental && (d.Variant == ExperimentalVariant.BitShifter || d.Variant == ExperimentalVariant.SwapOMatic
                                                              || d.Variant == ExperimentalVariant.PartyPopper || d.Variant == ExperimentalVariant.CapsLock);

        /// <summary>Continuous-fire weapon (LEAF BLOWER: hold the trigger).</summary>
        public static bool IsContinuous(WeaponData d) => d != null && d.Type == WeaponType.Experimental && d.Variant == ExperimentalVariant.LeafBlower;

        /// <summary>Seconds between shots the bot waits for (tool cooldowns of §13.2 are longer than FireRate says).</summary>
        public static float ShotInterval(WeaponData d)
        {
            if (d == null) return 0.4f;
            if (d.Type == WeaponType.Experimental)
                switch (d.Variant)
                {
                    case ExperimentalVariant.SwapOMatic: return 1.6f;
                    case ExperimentalVariant.CapsLock: return 2.1f;
                    case ExperimentalVariant.PartyPopper: return 2.1f;
                    case ExperimentalVariant.BitShifter: return 0.75f;
                    case ExperimentalVariant.TShirtCannon: return 1.1f;
                }
            return d.FireRate > 0.01f ? 1f / d.FireRate : 0.4f;
        }

        /// <summary>The current gun owner shoots (ShootAt with bot.GunOwner) - for tools taken from a ToolLocker.</summary>
        public IEnumerator ShootWithGun(BotTarget target, Func<bool> until = null, int maxShots = 8, float timeout = 12f) =>
            ShootAt(GunOwner, target, until, maxShots, timeout);
    }
}
