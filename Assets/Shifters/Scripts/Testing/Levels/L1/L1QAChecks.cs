// =====================================================================================
//  SHIFTERS - L1QAChecks.cs   (LEVEL 1 fairness probes - a DEVELOPMENT test, never the solution run)
//    devplay.sh <clone> l1_qa -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSetup Shifters.L1QAChecks.Run -devSeconds 1 [-botSeed N]
//  S5 WEAPON RANGE
//    * line of fire: every archetype, idle and aiming pose, several spots along the firing line, every target:
//      the gun's real ray (muzzle -> WeaponHolder.SafeOrigin -> target) + sampled spread cones; logs what blocks.
//    * live fire: every weapon kind at its WORST accuracy / shortest range, fired by every archetype from the
//      natural shooting spot; counts the shots needed and logs where missed projectiles stopped.
//  S4 THROW: human-style throws (CharacterGrabber.DirectionFrom(forward, 38 + look-up), exactly what ThrowAimed does)
//      from the THROW FROM HERE pad (back, middle, front) for the weakest and the strongest possible throw speed
//      (Brute STR 7 / STR 10) with both possible fliers, at 38 / 50 / 63 deg (no look-up / some / max).
//  Teleports and temporary stat / weapon overrides: diagnostics only.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public static class L1QAChecks
    {
        static readonly string[] TargetIds = { "S5.T1", "S5.T2", "S5.T3" };

        /// <summary>Worst-case generator roll per weapon kind: (kind, lowest accuracy, shortest range).</summary>
        static readonly (WeaponKind kind, float acc, float range)[] WorstGuns =
        {
            (WeaponKind.Pistol, 0.75f, 35f), (WeaponKind.Shotgun, 0.4f, 20f), (WeaponKind.Laser, 0.95f, 45f),
            (WeaponKind.Slingshot, 0.8f, 25f), (WeaponKind.Chicken, 0.7f, 25f), (WeaponKind.Plunger, 0.9f, 20f),
        };

        /// <summary>Review shots of the built scene (L1DevTests.ReviewShots), then the probes.</summary>
        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return ctx.Wait(2f);
            yield return L1DevTests.ReviewShots(ctx);
            var opt = new BotOptions { AllowDebugAssists = true, DiagnosticShots = false, Verbose = false, RunTimeout = 2400f };
            yield return TeamBot.Run(ctx, Probe, opt, "L1QA");
        }

        static IEnumerator Probe(TeamBot bot)
        {
            bot.Log("L1QA team: " + bot.TeamSummary());
            yield return LineOfFire(bot);
            yield return LiveFire(bot);
            yield return HumanThrows(bot);
            bot.Log("L1QA done");
        }

        // ================================================================== helpers
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static void Put(TeamBot bot, ShifterCharacter c, Vector3 feet, Vector3 face)
        {
            face = Flat(face);
            var rot = face.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(face.normalized, Vector3.up) : c.transform.rotation;
            if (c.Motor != null) c.Motor.Teleport(feet, rot); else c.transform.SetPositionAndRotation(feet, rot);
            bot.Driver.SetMove(c, Vector3.zero, false);
            bot.Driver.SetFace(c, face);
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Park everyone except 'keep' along the east wall of the hall at z (out of every firing lane).</summary>
        static void ParkOthers(TeamBot bot, ShifterCharacter keep, float z, float x = 5.2f)
        {
            int i = 0;
            foreach (var o in new[] { bot.Brute, bot.Sprinter, bot.Brainiac })
            {
                if (o == null || o == keep) continue;
                if (o.Grabber != null && o.Grabber.IsHolding) o.Grabber.ForceRelease();
                Put(bot, o, new Vector3(x, 0f, z + 1.6f * i++), Vector3.left);
            }
        }

        static void GiveGun(TeamBot bot, ShifterCharacter c, WeaponKind kind, float acc, float range)
        {
            foreach (var o in new[] { bot.Brute, bot.Sprinter, bot.Brainiac })
                if (o != null && o.Weapon != null && o.Weapon.HasWeapon) o.Weapon.TakeWeapon();
            var d = WeaponGenerator.Generate(kind, new System.Random(77));
            d.Accuracy = acc;
            d.Range = Mathf.Max(WeaponGenerator.MinRange, range);
            d.FireRate = Mathf.Max(d.FireRate, 4f); // timing only: geometry and ballistics are unchanged
            c.Weapon.Equip(d);
        }

        /// <summary>Same as WeaponHolder.SafeOrigin: a muzzle inside / behind a wall starts the shot at the wall.</summary>
        static Vector3 SafeOrigin(ShifterCharacter c, Vector3 muzzle)
        {
            Vector3 to = muzzle - c.Center;
            float len = to.magnitude;
            if (len < 0.05f) return muzzle;
            return WeaponBallistics.Raycast(c.Center, to / len, len, c.Weapon.transform, out var h) ? h.point - to / len * 0.05f : muzzle;
        }

        static string Name(Collider col)
        {
            if (col == null) return "nothing";
            var t = col.transform;
            return t.parent != null ? t.parent.name + "/" + t.name : t.name;
        }

        /// <summary>First hit of a gun ray. kind: 0 = target, 1 = blocked before the target, 2 = missed past it.</summary>
        static int Classify(Vector3 origin, Vector3 dir, Transform ignore, ShootTarget target, float targetDist, out string what)
        {
            bool hit = WeaponBallistics.Raycast(origin, dir, 40f, ignore, out var h);
            what = hit ? Name(h.collider) : "nothing";
            if (hit && h.collider.GetComponentInParent<ShootTarget>() == target) return 0;
            if (hit && h.distance < targetDist - 0.35f) return 1;
            return 2;
        }

        // ================================================================== S5: line of fire
        static IEnumerator LineOfFire(TeamBot bot)
        {
            var ctx = bot.Ctx;
            Vector3 spot = ((BotTarget)"S5.ShootSpot").Position;
            var spots = new[] { spot, new Vector3(spot.x - 1.2f, 0f, spot.z), new Vector3(-6.2f, 0f, 153.6f), new Vector3(-6.2f, 0f, 168.4f), new Vector3(-2.5f, 0f, spot.z) };
            var rng = new System.Random(5);
            int blockedCombos = 0, combos = 0;
            foreach (var c in new[] { bot.Brute, bot.Sprinter, bot.Brainiac })
            {
                if (c == null) continue;
                ParkOthers(bot, c, 147.5f);
                GiveGun(bot, c, WeaponKind.Pistol, 0.75f, 35f);
                yield return Frames(3);
                for (int si = 0; si < spots.Length; si++)
                {
                    foreach (var id in TargetIds)
                    {
                        var target = LevelMark.Find<ShootTarget>(id);
                        if (target == null) { ctx.Check(false, "[L1QA] target " + id + " exists"); continue; }
                        Vector3 aim = BotTargets.AimPoint(id);
                        Put(bot, c, spots[si], aim - spots[si]);
                        yield return Frames(10);
                        foreach (var pose in new[] { PoseOverride.None, PoseOverride.Aiming })
                        {
                            if (c.Visual != null) c.Visual.SetPose(pose);
                            yield return Frames(5);
                            Vector3 muzzle = c.Weapon.MuzzlePosition;
                            Vector3 origin = SafeOrigin(c, muzzle);
                            Vector3 dir = (aim - origin).normalized;
                            float dist = Vector3.Distance(origin, aim);
                            int center = Classify(origin, dir, c.Weapon.transform, target, dist, out string centerWhat);
                            var blockers = new Dictionary<string, int>();
                            int[] counts = new int[3];
                            int[] wide = new int[3];
                            const int N = 120;
                            for (int i = 0; i < N; i++)
                            {
                                int k = Classify(origin, WeaponBallistics.Spread(dir, 2.375f, rng), c.Weapon.transform, target, dist, out string w);
                                counts[k]++;
                                if (k == 1) blockers[w] = blockers.TryGetValue(w, out int n) ? n + 1 : 1;
                                int k2 = Classify(origin, WeaponBallistics.Spread(dir, 5f, rng), c.Weapon.transform, target, dist, out string w2);
                                wide[k2]++;
                                if (k2 == 1) blockers[w2] = blockers.TryGetValue(w2, out int n2) ? n2 + 1 : 1;
                            }
                            Vector3 eye = c.HeadTop - Vector3.up * 0.12f;
                            int eyeK = Classify(eye, (aim - eye).normalized, c.Weapon.transform, target, Vector3.Distance(eye, aim), out string eyeWhat);
                            var sb = new StringBuilder();
                            foreach (var kv in blockers) sb.Append(kv.Key).Append(" x").Append(kv.Value).Append("; ");
                            combos++;
                            bool clear = center == 0 && counts[1] == 0 && wide[1] == 0 && eyeK != 1;
                            if (!clear) blockedCombos++;
                            bot.Log("LOF " + c.Archetype + " h" + c.Height.ToString("0.00") + " spot" + si + " " + BotFmt.V(spots[si]) + " " + id + " pose=" + pose
                                    + " muzzleY " + (muzzle.y - c.Feet.y).ToString("0.00") + " originY " + (origin.y - c.Feet.y).ToString("0.00")
                                    + " | center -> " + (center == 0 ? "TARGET" : centerWhat) + " | pistol2.4deg hit " + counts[0] + "/" + N + " blocked " + counts[1]
                                    + " | shotgun5deg hit " + wide[0] + "/" + N + " blocked " + wide[1] + " | eye -> " + (eyeK == 0 ? "TARGET" : eyeWhat)
                                    + (sb.Length > 0 ? " | blockers: " + sb : ""));
                            ctx.Check(clear, "[L1QA] S5 line of fire clear: " + c.Archetype + " spot" + si + " " + id + " pose " + pose
                                             + (clear ? "" : " (center -> " + centerWhat + ", blocked " + counts[1] + "+" + wide[1] + "/" + (2 * N) + ")"));
                        }
                        if (c.Visual != null) c.Visual.SetPose(PoseOverride.None);
                    }
                }
            }
            bot.Log("LOF SUMMARY: " + (combos - blockedCombos) + "/" + combos + " archetype x spot x target x pose combos have a clear line of fire");
        }

        // ================================================================== S5: live fire
        static IEnumerator LiveFire(TeamBot bot)
        {
            var ctx = bot.Ctx;
            Vector3 spot = ((BotTarget)"S5.ShootSpot").Position;
            var inFlight = new HashSet<WeaponProjectile>();
            int total = 0, ok = 0;
            foreach (var c in new[] { bot.Brute, bot.Sprinter, bot.Brainiac })
            {
                if (c == null) continue;
                ParkOthers(bot, c, 147.5f);
                foreach (var g in WorstGuns)
                {
                    GiveGun(bot, c, g.kind, g.acc, g.range);
                    yield return Frames(12);
                    foreach (var id in TargetIds)
                    {
                        var target = LevelMark.Find<ShootTarget>(id);
                        if (target == null) continue;
                        target.ResetState();
                        Vector3 aim = BotTargets.AimPoint(id);
                        Put(bot, c, spot, aim - spot);
                        yield return Frames(10);
                        int shots = 0;
                        var misses = new StringBuilder();
                        float t0 = bot.Now;
                        while (shots < 8 && target.Hits == 0 && bot.Now - t0 < 14f)
                        {
                            inFlight.Clear();
                            foreach (var p in WeaponProjectile.Active) if (p != null && p.InFlight) inFlight.Add(p);
                            bot.Driver.SetFace(c, aim - c.Feet);
                            if (!c.Weapon.FireAt(aim)) { yield return null; continue; }
                            shots++;
                            WeaponProjectile mine = null;
                            foreach (var p in WeaponProjectile.Active) if (p != null && p.InFlight && !inFlight.Contains(p)) mine = p;
                            Vector3 last = mine != null ? mine.transform.position : Vector3.zero;
                            float tw = bot.Now;
                            while (bot.Now - tw < (mine != null ? 2.5f : 0.3f) && target.Hits == 0)
                            {
                                if (mine != null)
                                {
                                    if (!mine.InFlight || !mine.gameObject.activeInHierarchy) break;
                                    last = mine.transform.position;
                                }
                                yield return null;
                            }
                            if (mine != null && target.Hits == 0) misses.Append(BotFmt.V(last)).Append(mine.Bounces > 0 ? " (bounced " + mine.Bounces + ")" : "").Append("; ");
                        }
                        total++;
                        bool hit = target.Hits > 0;
                        if (hit) ok++;
                        bot.Log("FIRE " + c.Archetype + " " + g.kind + " acc " + g.acc.ToString("0.00") + " range " + g.range.ToString("0") + " -> " + id + ": "
                                + (hit ? "HIT after " + shots + " shot(s)" : "NO HIT in " + shots + " shots") + (misses.Length > 0 ? " | missed projectiles stopped at " + misses : ""));
                        ctx.Check(hit && shots <= 6, "[L1QA] S5 live fire: " + c.Archetype + " with a worst-roll " + g.kind + " hits " + id + " (" + shots + " shot(s))");
                    }
                }
            }
            foreach (var id in TargetIds) { var t = LevelMark.Find<ShootTarget>(id); if (t != null) t.ResetState(); }
            bot.Log("FIRE SUMMARY: " + ok + "/" + total + " archetype x weapon x target combos hit");
        }

        // ================================================================== S4: human-style throws
        static IEnumerator HumanThrows(TeamBot bot)
        {
            var ctx = bot.Ctx;
            var brute = bot.Brute;
            if (brute == null || brute.Grabber == null) { ctx.Check(false, "[L1QA] S4 throw: Brute with a grabber"); yield break; }
            foreach (var o in new[] { bot.Brute, bot.Sprinter, bot.Brainiac })
                if (o != null && o.Weapon != null && o.Weapon.HasWeapon) o.Weapon.TakeWeapon();
            var back = LevelMark.Find("S4.ThrowPadBack");
            var front = LevelMark.Find("S4.ThrowPadFront");
            float x = 4.6f;
            float z0 = back != null ? back.transform.position.z : 133.4f, z1 = front != null ? front.transform.position.z : 135.8f;
            if (back != null) x = back.transform.position.x;
            bot.Log("THROW pad z " + z0.ToString("0.00") + ".." + z1.ToString("0.00") + (back == null ? " (no pad marks: old pad)" : ""));
            var zs = new[] { z0 + 0.15f, (z0 + z1) * 0.5f, z1 - 0.15f };
            int origStr = brute.Stats.Strength;
            int total = 0, landed = 0;
            foreach (int str in new[] { 7, 10 })
            {
                brute.Stats.Strength = str;
                foreach (var flier in new[] { bot.Brainiac, bot.Sprinter })
                {
                    if (flier == null) continue;
                    foreach (float z in zs)
                    {
                        foreach (float ang in new[] { Tuning.ThrowBaseAngle, Tuning.ThrowBaseAngle + 12f, Tuning.ThrowBaseAngle + 25f })
                        {
                            ParkOthers(bot, flier, 124f, -4.5f);
                            if (!flier.IsAlive) flier.Revive();
                            flier.Heal(Tuning.MaxHealth);
                            if (brute.Grabber.IsHolding) brute.Grabber.ForceRelease();
                            Put(bot, brute, new Vector3(x, 0f, z - 1.4f), Vector3.forward);
                            Put(bot, flier, new Vector3(x, 0f, z - 0.15f), Vector3.back);
                            yield return Frames(20);
                            float tg = bot.Now;
                            while (bot.Now - tg < 3f && flier.Condition != CharacterCondition.Normal) yield return null;
                            bool grabbed = brute.Grabber.TryGrab(flier.Grabbable);
                            float tw = bot.Now;
                            while (bot.Now - tw < 1f && brute.Grabber.Held != flier.Grabbable) yield return null;
                            if (brute.Grabber.Held != flier.Grabbable) { ctx.Check(false, "[L1QA] S4 throw setup: grab " + flier.Archetype + " (TryGrab=" + grabbed + ")"); continue; }
                            Put(bot, brute, new Vector3(x, 0f, z), Vector3.forward);
                            yield return Frames(30);
                            float v = brute.Grabber.ThrowSpeedFor(flier.Grabbable);
                            Vector3 release = flier.Center;
                            brute.Grabber.Throw(CharacterGrabber.DirectionFrom(Vector3.forward, ang));
                            float tt = bot.Now;
                            yield return Frames(20);
                            while (bot.Now - tt < 6f)
                            {
                                bool settled = flier.Motor != null && flier.Motor.IsGrounded && !flier.Motor.IsTumbling && flier.Condition == CharacterCondition.Normal;
                                if (settled || !flier.IsAlive) break;
                                yield return null;
                            }
                            yield return Frames(15);
                            Vector3 f = flier.Feet;
                            bool onBalcony = flier.IsAlive && f.y > L1Builder.BalconyTop - 0.3f && f.z > L1Builder.BalconyZ0 - 0.1f && f.z < L1Builder.D4Z + 0.1f
                                             && f.x > L1Builder.BalconyX0 - 0.1f && f.x < L1Builder.BalconyX1 + 0.1f;
                            total++;
                            if (onBalcony) landed++;
                            bot.Log("THROW STR" + str + " v " + v.ToString("0.00") + " flier " + flier.Archetype + " " + flier.Mass.ToString("0") + "kg from z " + z.ToString("0.00")
                                    + " at " + ang.ToString("0") + " deg (look-up " + (ang - Tuning.ThrowBaseAngle).ToString("0") + "): release " + BotFmt.V(release)
                                    + " -> rest " + BotFmt.V(f) + " " + flier.State + "/" + flier.Condition + (onBalcony ? " ON BALCONY" : " MISSED"));
                            ctx.Check(onBalcony, "[L1QA] S4 human throw lands on the balcony: STR" + str + " " + flier.Archetype + " from z " + z.ToString("0.0") + " at " + ang.ToString("0") + " deg");
                        }
                    }
                }
            }
            brute.Stats.Strength = origStr;
            bot.Log("THROW SUMMARY: " + landed + "/" + total + " human-style throws from the pad landed on the balcony");
        }
    }
}
