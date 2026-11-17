using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>DevPlay: generator ranges, every weapon kind firing at dummies/crates/heavy blocks, projectile arcs &
    /// bounces, friendly fire, drop on KO -> pickup -> teammate picks it up, void respawn at FallbackSpawn.
    /// Run: devplay.sh &lt;clone&gt; we_weapons -devSetup Shifters.WeaponTests.Run</summary>
    public static class WeaponTests
    {
        static readonly WeaponKind[] Kinds = { WeaponKind.Pistol, WeaponKind.Shotgun, WeaponKind.Laser, WeaponKind.Slingshot, WeaponKind.Chicken, WeaponKind.Plunger };

        public static IEnumerator Run(DevPlayContext ctx)
        {
            GeneratorChecks(ctx);
            WETestKit.Ground();

            // ---------------------------------------------------------------- lineup screenshot
            var rng = new System.Random(99);
            var lineup = new List<ShifterCharacter>();
            for (int i = 0; i < Kinds.Length; i++)
            {
                var a = (Archetype)(i % 3);
                lineup.Add(WETestKit.Character(a, new Vector3(-20f + i * 1.6f, 0f, 20f), 180f, i, WeaponGenerator.Generate(Kinds[i], rng)));
            }
            var pickupShow = WeaponKit.Pickup(null, new Vector3(-12f, 0f, 20f), WeaponGenerator.Generate(WeaponKind.Chicken, rng));
            WETestKit.Cam(new Vector3(-15.5f, 1.6f, 15.2f), new Vector3(-15.5f, 1.0f, 20f));
            yield return ctx.Wait(0.6f);
            int armed = 0;
            foreach (var c in lineup) if (c.Weapon != null && c.Weapon.HasWeapon && c.Weapon.Model != null && c.Profile.Weapon == c.Weapon.Data) armed++;
            ctx.Check(armed == Kinds.Length, "lineup: all 6 characters armed, model built, Profile.Weapon synced (" + armed + ")");
            ctx.Check(pickupShow != null && pickupShow.GetComponentsInChildren<Renderer>().Length > 2, "standalone WeaponKit.Pickup has a visible model + ring");
            ctx.Shot("weapon_lineup");
            foreach (var c in lineup) Object.Destroy(c.gameObject);
            Object.Destroy(pickupShow.gameObject);

            // ---------------------------------------------------------------- firing lanes
            var shooter = WETestKit.Character(Archetype.Brute, Vector3.zero, 0f, 0);
            WETestKit.Cam(new Vector3(-4.5f, 3.2f, -3.5f), new Vector3(0.5f, 0.8f, 6f));
            foreach (var kind in Kinds)
            {
                yield return FireLane(ctx, shooter, kind, rng);
            }

            // ---------------------------------------------------------------- friendly fire
            shooter.Weapon.Equip(WeaponGenerator.Generate(WeaponKind.Pistol, rng));
            var buddy = WETestKit.Character(Archetype.Sprinter, new Vector3(0f, 0f, 4f), 180f, 1);
            yield return ctx.Wait(0.4f);
            float hp = buddy.Health;
            shooter.Weapon.FireAt(buddy.Center);
            yield return ctx.Wait(0.1f);
            ctx.Check(buddy.Health < hp && hp - buddy.Health <= WeaponBallistics.FriendlyFireDamage + 0.01f && buddy.IsAlive,
                "friendly fire: small damage (" + (hp - buddy.Health).ToString("0.0") + ") and still alive");

            // ---------------------------------------------------------------- drop on KO -> pickup -> teammate
            var data = shooter.Weapon.Data;
            yield return ctx.Wait(0.6f);
            shooter.KnockOut("test KO");
            ctx.Check(!shooter.Weapon.HasWeapon && shooter.Profile.Weapon == null, "KO drops the weapon (HasWeapon false, Profile.Weapon null)");
            ctx.Check(WeaponPickup.All.Count == 1 && WeaponPickup.All[0].Data == data, "a WeaponPickup with the same data appeared");
            yield return ctx.Wait(1.0f);
            var pickup = WeaponPickup.All.Count > 0 ? WeaponPickup.All[0] : null;
            ctx.Check(pickup != null && WETestKit.Planar(pickup.transform.position, shooter.Feet) < 2.5f && pickup.transform.position.y > 0.3f && pickup.transform.position.y < 1.6f,
                "pickup rests near the KO'd owner, hovering (" + (pickup != null ? pickup.transform.position.ToString() : "none") + ")");
            WETestKit.Cam(new Vector3(2.5f, 2.2f, -2.5f), pickup != null ? pickup.transform.position : Vector3.zero);
            ctx.Shot("weapon_pickup_dropped");
            string why;
            ctx.Check(pickup != null && pickup.CanInteract(buddy, out why), "teammate can interact with the pickup");
            ctx.Check(pickup != null && pickup.GetPrompt(buddy).Contains(data.Name), "prompt names the weapon: " + (pickup != null ? pickup.GetPrompt(buddy) : ""));
            ctx.Check(pickup != null && !pickup.CanInteract(shooter, out why), "the KO'd owner cannot pick it up");
            if (pickup != null) pickup.Interact(buddy);
            yield return null;
            ctx.Check(buddy.Weapon.HasWeapon && buddy.Weapon.Data == data && buddy.Profile.Weapon == data && !shooter.Weapon.HasWeapon, "HasWeapon moved to the teammate");
            yield return null;
            ctx.Check(WeaponPickup.All.Count == 0, "pickup consumed");
            ctx.Check(WeaponHolder.CurrentOwner == buddy.Weapon, "WeaponHolder.CurrentOwner is the new owner");

            // ---------------------------------------------------------------- lost to the void -> FallbackSpawn
            WeaponPickup.FallbackSpawn = new Vector3(-6f, 0f, -3f);
            buddy.Lose("test void");
            yield return ctx.Wait(1.0f);
            var lostPickup = WeaponPickup.All.Count > 0 ? WeaponPickup.All[0] : null;
            ctx.Check(lostPickup != null && WETestKit.Planar(lostPickup.transform.position, WeaponPickup.FallbackSpawn.Value) < 1.0f,
                "gun lost with its owner reappears at FallbackSpawn (" + (lostPickup != null ? lostPickup.transform.position.ToString() : "none") + ")");
            // a pickup knocked into the void respawns too
            if (lostPickup != null)
            {
                lostPickup.transform.position = new Vector3(40f, -10f, 40f); // off the ground: falls
                yield return ctx.Wait(3.0f);
                ctx.Check(WETestKit.Planar(lostPickup.transform.position, WeaponPickup.FallbackSpawn.Value) < 1.0f && lostPickup.transform.position.y > -1f,
                    "pickup that fell below LethalFallY respawned at FallbackSpawn (" + lostPickup.transform.position + ")");
            }
            // ---------------------------------------------------------------- custody: a respawned team gets its gun back
            WeaponPickup.FallbackSpawn = null;
            foreach (var p in WeaponPickup.All.ToArray()) Object.Destroy(p.gameObject);
            var mgr = new GameObject("CharacterManager").AddComponent<CharacterManager>();
            var team = new TeamData { Seed = 5 };
            for (int i = 0; i < 3; i++)
            {
                var a = (Archetype)i;
                var pr = new CharacterProfile { Name = "Temp" + i, Archetype = a, PrimaryColor = Color.HSVToRGB(i / 3f, 0.6f, 0.9f) };
                pr.Body.Height = Tuning.BaseHeight(a); pr.Body.Radius = Tuning.CapsuleRadius(a);
                team.Members[i] = pr;
            }
            team.Members[1].Weapon = WeaponGenerator.Generate(WeaponKind.Slingshot, rng);
            var spawns = new List<Pose> { new Pose(new Vector3(-10f, 0f, -10f), Quaternion.identity), new Pose(new Vector3(-8f, 0f, -10f), Quaternion.identity), new Pose(new Vector3(-6f, 0f, -10f), Quaternion.identity) };
            yield return null;
            mgr.SpawnTeam(team, spawns, 0);
            yield return null;
            var gun = mgr.Get(1) != null && mgr.Get(1).Weapon != null ? mgr.Get(1).Weapon.Data : null;
            ctx.Check(gun != null, "CharacterManager team: slot 1 spawned armed");
            if (gun != null) mgr.Get(1).Weapon.TakeWeapon(); // gun vanished (e.g. dropped, then the scene reloaded)
            ctx.Check(team.Members[1].Weapon == null, "profiles no longer carry the gun");
            mgr.SpawnTeam(team, spawns, 0);
            yield return null;
            yield return null;
            ShifterCharacter armedNow = null;
            for (int i = 0; i < mgr.Team.Count; i++) if (mgr.Team[i] != null && mgr.Team[i].Weapon != null && mgr.Team[i].Weapon.HasWeapon) armedNow = mgr.Team[i];
            ctx.Check(armedNow != null && armedNow.Weapon.Data == gun, "WeaponCustody: respawned team got its gun back (" + (armedNow != null ? armedNow.DisplayName : "nobody") + ")");
            ctx.Log("weapons scenario done");
        }

        static IEnumerator FireLane(DevPlayContext ctx, ShifterCharacter shooter, WeaponKind kind, System.Random rng)
        {
            var data = WeaponGenerator.Generate(kind, rng);
            shooter.Weapon.Equip(data);
            var dummy = WETestKit.Target(new Vector3(-2.5f, 1f, 8f));
            var crate = WETestKit.Prop("Crate30", new Vector3(0f, 0.45f, kind == WeaponKind.Plunger ? 10f : 8f), Vector3.one * 0.9f, 30f, MatKey.Wood);
            var block = WETestKit.Prop("Block160", new Vector3(2.8f, 0.6f, 8f), Vector3.one * 1.2f, 160f, MatKey.CrateHeavy);
            yield return ctx.Wait(0.5f);
            string tag = "[" + kind + " '" + data.Name + "'] ";

            // 1) the IShootable dummy
            yield return HoldFire(shooter, dummy.transform.position, 0.25f, true);
            yield return ctx.Wait(WeaponStyle.IsProjectile(kind) ? 0.8f : 0.1f);
            ctx.Check(dummy.Hits > 0, tag + "IShootable.OnShot received (" + dummy.Hits + ")");

            // 2) the 30 kg crate gets knocked (plunger: pulled closer)
            Vector3 c0 = crate.position;
            float d0 = WETestKit.Planar(c0, shooter.Feet);
            yield return HoldFire(shooter, crate.position, 1.0f, false, kind != WeaponKind.Plunger, crate);
            yield return ctx.Wait(0.7f);
            if (kind == WeaponKind.Plunger)
            {
                float d1 = WETestKit.Planar(crate.position, shooter.Feet);
                ctx.Check(d1 < d0 - 1.5f, tag + "plunger pulled the crate closer (" + d0.ToString("0.0") + " -> " + d1.ToString("0.0") + " m)");
            }
            else
            {
                float moved = Vector3.Distance(crate.position, c0);
                ctx.Check(moved > 0.5f, tag + "30 kg crate knocked (" + moved.ToString("0.00") + " m)");
            }

            // 3) the 160 kg block barely budges
            Vector3 b0 = block.position;
            if (!WeaponStyle.IsProjectile(kind))
            {
                yield return WETestKit.WaitUntil(() => shooter.Weapon.IsReady, 2f);
                WETestKit.Cam(new Vector3(-3.2f, 2.4f, -2.2f), new Vector3(1.5f, 0.9f, 7f));
                shooter.Weapon.FireAt(block.position);
                yield return null;        // that frame rendered: gun raised onto the shot line, tracer + muzzle flash alive
                ctx.Shot("fire_" + kind);
            }
            yield return HoldFire(shooter, block.position, 1.0f, false);
            yield return ctx.Wait(0.6f);
            float bm = Vector3.Distance(block.position, b0);
            ctx.Check(bm < 0.35f, tag + "160 kg block barely moved (" + bm.ToString("0.000") + " m)");

            // 4) projectiles arc (and the chicken bounces)
            if (WeaponStyle.IsProjectile(kind))
            {
                yield return WETestKit.WaitUntil(() => shooter.Weapon.IsReady, 2f);
                int before = WeaponProjectile.Active.Count;
                Vector3 floorAim = new Vector3(-1.5f, 0f, 18f);
                shooter.Weapon.FireAt(floorAim);
                WeaponProjectile p = WeaponProjectile.Active.Count > before ? WeaponProjectile.Active[WeaponProjectile.Active.Count - 1] : null;
                ctx.Check(p != null, tag + "physical projectile launched from the pool");
                float apex = 0f, arc = 0f; int bounces = 0;
                yield return ctx.Wait(0.35f);
                ctx.Shot("fire_" + kind);
                for (int i = 0; i < 330 && p != null && p.InFlight; i++)
                {
                    apex = Mathf.Max(apex, p.ApexY - p.LaunchPosition.y);
                    bounces = Mathf.Max(bounces, p.Bounces);
                    // height above the straight launch -> aim line (shots now leave at shoulder height, so a floor target
                    // is below the launch point: the arc is measured against the chord, not the launch height)
                    if (bounces == 0)
                    {
                        Vector3 a0 = p.LaunchPosition, chord = floorAim - a0, pos = p.transform.position;
                        float along = Mathf.Clamp01(Vector3.Dot(pos - a0, chord) / Mathf.Max(0.01f, chord.sqrMagnitude));
                        arc = Mathf.Max(arc, pos.y - Mathf.Lerp(a0.y, floorAim.y, along));
                    }
                    yield return null;
                }
                ctx.Check(arc > 0.5f, tag + "projectile flew a visible arc (+" + arc.ToString("0.00") + " m above the straight line, apex +" + apex.ToString("0.00") + " m over the launch)");
                if (kind == WeaponKind.Chicken) ctx.Check(bounces >= 2, tag + "rubber chicken bounced (" + bounces + " squeaky bounces)");
                ctx.Check(p == null || !p.InFlight, tag + "projectile despawned back to the pool");
            }
            Object.Destroy(dummy.gameObject);
            Object.Destroy(crate.gameObject);
            Object.Destroy(block.gameObject);
            yield return null;
        }

        /// <summary>FireAt every frame for 'seconds' (FireAt respects the fire rate). trackBody re-aims at a moving target.</summary>
        static IEnumerator HoldFire(ShifterCharacter shooter, Vector3 target, float seconds, bool untilFirstShot, bool track = false, Rigidbody trackBody = null)
        {
            int frames = Mathf.CeilToInt(seconds * 60f);
            int shots = shooter.Weapon.ShotsFired;
            for (int i = 0; i < frames * 4; i++)
            {
                Vector3 aim = track && trackBody != null ? trackBody.position : target;
                shooter.Weapon.FireAt(aim);
                if (untilFirstShot && shooter.Weapon.ShotsFired > shots) yield break;
                if (!untilFirstShot && i >= frames) yield break;
                yield return null;
            }
        }

        static void GeneratorChecks(DevPlayContext ctx)
        {
            var rng = new System.Random(2026);
            var counts = new int[6];
            var names = new HashSet<string>();
            string bad = null;
            float minRange = 999f;
            for (int i = 0; i < 1000; i++)
            {
                var d = WeaponGenerator.Generate(rng);
                var k = WeaponStyle.KindOf(d);
                counts[(int)k]++;
                names.Add(d.Name);
                minRange = Mathf.Min(minRange, d.Range);
                string r = RangeProblem(d, k);
                if (r != null && bad == null) bad = "#" + i + " " + k + ": " + r;
                if (string.IsNullOrEmpty(d.Name) && bad == null) bad = "#" + i + " empty name";
            }
            ctx.Check(bad == null, "1000 generated weapons all inside their per-type ranges" + (bad != null ? " FIRST BAD: " + bad : ""));
            ctx.Check(minRange >= 20f, "range always >= 20 m (min " + minRange + ")");
            bool spread = true;
            for (int i = 0; i < 6; i++) if (counts[i] < 60) spread = false;
            ctx.Check(spread, "all 6 kinds generated (P/S/L/Sl/Ch/Pl = " + string.Join("/", counts) + "), Experimental split in 2 variants");
            ctx.Check(counts[0] > 150 && counts[0] < 250 && counts[4] + counts[5] > 150 && counts[4] + counts[5] < 250, "types roughly uniform over the 5 WeaponTypes");
            ctx.Check(names.Count > 120, "funny names are varied (" + names.Count + " distinct)");
            var sample = new System.Text.StringBuilder();
            var srng = new System.Random(5);
            for (int i = 0; i < 8; i++) { var d = WeaponGenerator.Generate(srng); sample.Append(d.Name).Append(" | "); }
            ctx.Log("sample names: " + sample);
            var a = WeaponGenerator.Generate(new System.Random(77));
            var b = WeaponGenerator.Generate(new System.Random(77));
            ctx.Check(a.Name == b.Name && a.Type == b.Type && Mathf.Approximately(a.Damage, b.Damage), "deterministic per seed");
        }

        static string RangeProblem(WeaponData d, WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Pistol: return Out(d.Damage, 15, 25, "dmg") ?? Out(d.FireRate, 2.5f, 4, "rate") ?? Out(d.Accuracy, 0.75f, 0.95f, "acc") ?? Out(d.Range, 35, 50, "range") ?? Out(d.Knockback, 6, 12, "kb");
                case WeaponKind.Shotgun: return Out(d.Damage, 8, 12, "dmg") ?? Out(d.Pellets, 6, 8, "pellets") ?? Out(d.FireRate, 0.8f, 1.3f, "rate") ?? Out(d.Accuracy, 0.4f, 0.6f, "acc") ?? Out(d.Range, 20, 28, "range") ?? Out(d.Knockback, 20, 35, "kb");
                case WeaponKind.Laser: return Out(d.Damage, 8, 14, "dmg") ?? Out(d.FireRate, 6, 10, "rate") ?? Out(d.Accuracy, 0.95f, 1, "acc") ?? Out(d.Range, 45, 60, "range") ?? Out(d.Knockback, 2, 5, "kb");
                case WeaponKind.Slingshot: return Out(d.Damage, 20, 35, "dmg") ?? Out(d.FireRate, 1, 1.6f, "rate") ?? Out(d.Accuracy, 0.8f, 0.95f, "acc") ?? Out(d.Range, 25, 40, "range") ?? Out(d.Knockback, 15, 25, "kb");
                case WeaponKind.Chicken: return Out(d.Damage, 10, 15, "dmg") ?? Out(d.FireRate, 1.2f, 2, "rate") ?? Out(d.Range, 25, 35, "range") ?? Out(d.Knockback, 18, 30, "kb");
                default: return Out(d.FireRate, 0.8f, 1.2f, "rate") ?? Out(d.Range, 20, 30, "range") ?? Out(d.Knockback, 12, 20, "pull");
            }
        }

        static string Out(float v, float min, float max, string what) => v < min - 1e-4f || v > max + 1e-4f ? what + "=" + v + " not in " + min + ".." + max : null;
    }
}
