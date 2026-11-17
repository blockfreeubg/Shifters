using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    /// <summary>Test IFreezable / IResizable stand-ins (puzzles' Shiftable is used too when present).</summary>
    public class CWTestFreezable : MonoBehaviour, IFreezable, IShootable
    {
        public float FrozenFor; public int Shots;
        public void Freeze(float seconds) { FrozenFor = seconds; }
        public bool IsFrozen => FrozenFor > 0f;
        public void OnShot(in ShotInfo shot) { Shots++; }
    }

    /// <summary>DevPlay: the campaign weapons (CAMPAIGN 13). Generator W4, every new kind fires/hits/OnShot/range/rate,
    /// pooling, LEAF BLOWER push rules, T-SHIRT wrap/shirt, PAINT splats, BIT-SHIFTER cycle, SWAP exclusions/clearance,
    /// PARTY POPPER flare light/noise, SAY CHEESE cone, CAPS-LOCK, NoToolZone fizzle, lockers + armory + custody.
    /// Run: devplay.sh &lt;clone&gt; cw_all -devSetup Shifters.CampaignWeaponTests.Run -devSeconds 400</summary>
    public static class CampaignWeaponTests
    {
        static readonly WeaponKind[] NewKinds = { WeaponKind.LeafBlower, WeaponKind.TShirtCannon, WeaponKind.SayCheese, WeaponKind.PaintBlaster,
                                                  WeaponKind.BitShifter, WeaponKind.SwapOMatic, WeaponKind.PartyPopper, WeaponKind.CapsLock };

        /// <summary>Only the LEAF BLOWER section: -devSetup Shifters.CampaignWeaponTests.RunBlower -devSeconds 120</summary>
        public static IEnumerator RunBlower(DevPlayContext ctx) { Clear(); yield return null; WETestKit.Ground(); yield return Blower(ctx); }

        public static IEnumerator Run(DevPlayContext ctx)
        {
            GeneratorChecks(ctx);
            Clear(); yield return null;
            WETestKit.Ground();
            yield return FireAll(ctx);
            Clear(); yield return null; WETestKit.Ground();
            yield return Blower(ctx);
            Clear(); yield return null; WETestKit.Ground();
            yield return ShirtPaint(ctx);
            Clear(); yield return null; WETestKit.Ground();
            yield return BitShifter(ctx);
            Clear(); yield return null; WETestKit.Ground();
            yield return Swap(ctx);
            Clear(); yield return null; WETestKit.Ground();
            yield return PopperCheeseCaps(ctx);
            Clear(); yield return null; WETestKit.Ground();
            yield return Lockers(ctx);
            Clear();
            ToolZones.PointQuery = null; ToolZones.SegmentQuery = null;
            WeaponGenerator.ForcedPoolKind = null;
        }

        // ================================================================== generator (W4)
        static void GeneratorChecks(DevPlayContext ctx)
        {
            var pool = WeaponGenerator.PoolKinds;
            bool noTools = true;
            foreach (var k in pool) if (WeaponStyle.IsTool(k)) noTools = false;
            ctx.Check(pool.Length == 10 && noTools, "PoolKinds: 10 kinds, no tools (" + pool.Length + ")");
            var count = new int[WeaponStyle.KindCount];
            var rng = new System.Random(5);
            const int N = 6000;
            for (int i = 0; i < N; i++) count[(int)WeaponStyle.KindOf(WeaponGenerator.Generate(rng))]++;
            bool uniform = true; string dist = "";
            foreach (var k in pool) { float f = count[(int)k] / (float)N; dist += k + "=" + f.ToString("0.000") + " "; if (f < 0.085f || f > 0.115f) uniform = false; }
            foreach (var k in NewKinds) if (WeaponStyle.IsTool(k) && count[(int)k] > 0) uniform = false;
            ctx.Check(uniform, "Generate(rng) uniform over PoolKinds, never a tool: " + dist);

            // stream-length rule: exactly two draws before the stats
            bool stream = true;
            for (int s = 1; s <= 40; s++)
            {
                var a = new System.Random(s); var da = WeaponGenerator.Generate(a);
                var b = new System.Random(s); int idx = b.Next(pool.Length); b.Next(2); var db = WeaponGenerator.Generate(pool[idx], b);
                if (a.Next() != b.Next() || da.Name != db.Name || WeaponStyle.KindOf(da) != pool[idx]) stream = false;
            }
            ctx.Check(stream, "stream rule: kind index + one Next(2), then the stats (seed-stable)");

            // a seed keeps its team; only the gun may change (forced kind changes nothing else)
            bool team = true;
            for (int s = 1; s <= 5; s++)
            {
                WeaponGenerator.ForcedPoolKind = null;
                var t0 = CharacterGenerator.GenerateTeam(s);
                WeaponGenerator.ForcedPoolKind = WeaponKind.LeafBlower;
                var t1 = CharacterGenerator.GenerateTeam(s);
                for (int i = 0; i < 3; i++)
                {
                    var m0 = t0.Members[i]; var m1 = t1.Members[i];
                    if (m0.Name != m1.Name || m0.Archetype != m1.Archetype || Mathf.Abs(m0.Mass - m1.Mass) > 0.001f || m0.HasWeapon != m1.HasWeapon) team = false;
                    if (m1.HasWeapon && WeaponStyle.KindOf(m1.Weapon) != WeaponKind.LeafBlower) team = false;
                }
            }
            WeaponGenerator.ForcedPoolKind = null;
            ctx.Check(team, "seeds 1-5: forcing the gun kind leaves names/archetypes/masses/armed slot unchanged");

            // ranges
            var r = new System.Random(77);
            bool ranges = true; string bad = "";
            for (int i = 0; i < 300; i++)
            {
                var d = WeaponGenerator.Generate(WeaponKind.LeafBlower, r);
                if (d.Damage != 0f || d.FireRate != 4f || d.Range < 20f || d.Range > 24f || d.Knockback < 4f || d.Knockback > 6.4f) { ranges = false; bad = "blower"; }
                d = WeaponGenerator.Generate(WeaponKind.TShirtCannon, r);
                if (d.Damage < 5f || d.Damage > 10f || d.FireRate < 1f || d.FireRate > 1.4f || d.Accuracy < 0.8f || d.Accuracy > 0.9f || d.Range < 25f || d.Range > 32f || d.Knockback < 18f || d.Knockback > 28f) { ranges = false; bad = "shirt"; }
                d = WeaponGenerator.Generate(WeaponKind.SayCheese, r);
                if (d.Damage != 0f || d.FireRate < 0.6f || d.FireRate > 0.8f || d.Range < 20f || d.Range > 25f) { ranges = false; bad = "cheese"; }
                d = WeaponGenerator.Generate(WeaponKind.PaintBlaster, r);
                if (d.Damage < 10f || d.Damage > 16f || d.FireRate < 1.5f || d.FireRate > 2.5f || d.Accuracy < 0.8f || d.Accuracy > 0.95f || d.Range < 25f || d.Range > 35f || d.Knockback < 8f || d.Knockback > 14f) { ranges = false; bad = "paint"; }
                if (d.Name.Length > 34) { ranges = false; bad = "name " + d.Name; }
            }
            ctx.Check(ranges, "pool gun stat ranges (CAMPAIGN 13.2) " + bad);
            var bs = WeaponGenerator.Tool(ExperimentalVariant.BitShifter); var bs2 = WeaponGenerator.Tool(ExperimentalVariant.BitShifter);
            var sw = WeaponGenerator.Tool(ExperimentalVariant.SwapOMatic); var pp = WeaponGenerator.Tool(ExperimentalVariant.PartyPopper); var cl = WeaponGenerator.Tool(ExperimentalVariant.CapsLock);
            ctx.Check(bs.FireRate == 1.5f && bs.Range == 25f && bs.Name == bs2.Name && bs != bs2 && WeaponStyle.IsTool(bs), "Tool(BitShifter): fixed 1.5/s r25 (" + bs.Name + ")");
            ctx.Check(Mathf.Abs(1f / sw.FireRate - 1.5f) < 0.01f && sw.Range == 25f && Mathf.Abs(1f / pp.FireRate - 2f) < 0.01f && pp.Range == 20f && Mathf.Abs(1f / cl.FireRate - 2f) < 0.01f && cl.Range == 25f,
                "Tool(): SWAP cooldown 1.5 s r25, POPPER reload 2 s r20, CAPS cooldown 2 s r25");
            bool map = true;
            foreach (var k in NewKinds) if (WeaponStyle.KindOf(WeaponStyle.TypeOf(k), WeaponStyle.VariantOf(k)) != k) map = false;
            ctx.Check(map, "KindOf(TypeOf, VariantOf) round-trips for every campaign kind");
        }

        static WeaponData Data(WeaponKind k, int seed = 3)
        {
            var d = WeaponGenerator.Generate(k, new System.Random(seed));
            d.Accuracy = 1f;
            return d;
        }

        // ================================================================== every kind: fire, hit, OnShot, range, rate
        static IEnumerator FireAll(DevPlayContext ctx)
        {
            var shooter = WETestKit.Character(Archetype.Brute, Vector3.zero, 0f, 0);
            int lane = 0;
            foreach (var kind in NewKinds)
            {
                float x = lane * 4f; lane++;
                var d = Data(kind);
                shooter.Weapon.Equip(d);
                if (shooter.Motor != null) shooter.Motor.Teleport(new Vector3(x, 0f, 0f), Quaternion.identity);
                yield return ctx.Wait(0.3f);
                // near target (6 m) and far target (19 m, i.e. >= the 20 m rule after the muzzle)
                var near = WETestKit.Target(new Vector3(x, 1.4f, 6f));
                WETestKit.Cam(new Vector3(x - 3.2f, 2.4f, -2.8f), new Vector3(x, 1.2f, 4f));
                bool fired = shooter.Weapon.FireAt(near.transform.position);
                bool again = shooter.Weapon.FireAt(near.transform.position);
                yield return null;
                ctx.Shot("cw_fire_" + kind);
                yield return WETestKit.WaitUntil(() => near.Hits > 0, 2.5f);
                ctx.Check(fired && !again, kind + ": fires, second pull inside the cooldown refused");
                ctx.Check(near.Hits > 0 && near.Last.Variant == WeaponStyle.VariantOf(kind) && near.Last.Weapon == WeaponType.Experimental,
                    kind + ": OnShot on the hit target with Variant " + near.Last.Variant + " (hits " + near.Hits + ")");
                Object.Destroy(near.gameObject);
                yield return ctx.Wait(1f / Mathf.Max(0.1f, d.FireRate) + 0.1f);
                var far = WETestKit.Target(new Vector3(x, 1.4f, 19f));
                if (WeaponStyle.IsProjectile(kind)) far.transform.position = new Vector3(x, 0.6f, 18.5f);
                yield return null;
                bool ok = shooter.Weapon.FireAt(far.transform.position);
                yield return WETestKit.WaitUntil(() => far.Hits > 0, 3.5f);
                ctx.Check(ok && far.Hits > 0, kind + ": reaches a target " + Vector3.Distance(shooter.Feet, far.transform.position).ToString("0.0") + " m away (range " + d.Range + ")");
                Object.Destroy(far.gameObject);
                yield return ctx.Wait(1f / Mathf.Max(0.1f, d.FireRate) + 0.05f);
                if (kind == WeaponKind.PartyPopper && PartyFlare.Current != null) PartyFlare.Current.Extinguish();
            }

            // pooling: projectiles are reused; no per-launch allocations after warm-up
            shooter.Weapon.Equip(Data(WeaponKind.TShirtCannon));
            if (shooter.Motor != null) shooter.Motor.Teleport(Vector3.zero, Quaternion.identity);
            yield return ctx.Wait(0.3f);
            int created0 = WeaponProjectile.CreatedCount;
            for (int i = 0; i < 8; i++) { shooter.Weapon.FireAt(new Vector3(0f, 0.5f, 10f)); yield return ctx.Wait(1f); }
            ctx.Check(WeaponProjectile.CreatedCount - created0 <= 1, "T-SHIRT projectiles pooled (" + (WeaponProjectile.CreatedCount - created0) + " new for 8 shots)");
            var dd = Data(WeaponKind.PaintBlaster);
            WeaponProjectile.Launch(WeaponKind.PaintBlaster, new Vector3(0f, 1f, 1f), Vector3.forward * 5f, dd, shooter); yield return ctx.Wait(1.2f);
            long a0 = System.GC.GetAllocatedBytesForCurrentThread();
            var pr = WeaponProjectile.Launch(WeaponKind.PaintBlaster, new Vector3(0f, 1f, 1f), Vector3.forward * 5f, dd, shooter);
            long a1 = System.GC.GetAllocatedBytesForCurrentThread();
            ctx.Check(a1 - a0 < 256, "pooled launch allocates nothing (" + (a1 - a0) + " bytes)");
            yield return ctx.Wait(1f);
            Object.Destroy(shooter.gameObject);
        }

        // ================================================================== LEAF BLOWER
        static IEnumerator Blower(DevPlayContext ctx)
        {
            var shooter = WETestKit.Character(Archetype.Sprinter, Vector3.zero, 0f, 0);
            var d = Data(WeaponKind.LeafBlower); d.Knockback = 5f;
            shooter.Weapon.Equip(d);
            var light = WETestKit.Prop("Crate30", new Vector3(-1.0f, 0.4f, 4.5f), Vector3.one * 0.8f, 30f, MatKey.Wood);
            var heavy = WETestKit.Prop("Block160", new Vector3(1.2f, 0.5f, 4.5f), Vector3.one * 1f, 160f, MatKey.CrateHeavy);
            var buddy = WETestKit.Character(Archetype.Brainiac, new Vector3(0.3f, 0f, 2.5f), 180f, 1);   // not shielding the crate
            yield return ctx.Wait(0.5f);
            Vector3 l0 = light.position, h0 = heavy.position, b0 = buddy.Feet;
            float maxBuddy = 0f, maxLight = 0f; bool tumbled = false;
            WETestKit.Cam(new Vector3(-4f, 3f, -2f), new Vector3(0f, 0.8f, 4f));
            float tEnd = Time.time + 2.5f; bool shot = false;
            for (int f = 0; Time.time < tEnd && f < 2000; f++)
            {
                shooter.Weapon.FireAt(new Vector3(0f, 0.6f, 6f));
                var v = buddy.Body.linearVelocity; v.y = 0f; maxBuddy = Mathf.Max(maxBuddy, v.magnitude);
                maxLight = Mathf.Max(maxLight, light.linearVelocity.magnitude);
                if (buddy.Condition != CharacterCondition.Normal) tumbled = true;
                if (!shot && Time.time > tEnd - 1.3f) { shot = true; ctx.Shot("cw_blower_cone"); }
                yield return null;
            }
            ctx.Check(Vector3.Distance(light.position, l0) > 1f && maxLight <= d.Knockback + 1f, "blower: 30 kg crate pushed " + Vector3.Distance(light.position, l0).ToString("0.0") + " m, peak " + maxLight.ToString("0.0") + " m/s (push " + d.Knockback + ")");
            ctx.Check(Vector3.Distance(heavy.position, h0) < 0.25f, "blower: 160 kg block stays (" + Vector3.Distance(heavy.position, h0).ToString("0.00") + " m)");
            ctx.Check(maxBuddy <= 2.3f && !tumbled && WETestKit.Planar(buddy.Feet, b0) > 0.05f, "blower: teammate nudged <= 2 m/s (peak " + maxBuddy.ToString("0.00") + "), no tumble, moved " + WETestKit.Planar(buddy.Feet, b0).ToString("0.00") + " m");
            var stream = shooter.GetComponent<LeafBlowerStream>();
            if (stream != null)
            {
                var sb = new System.Text.StringBuilder("blower overlap:");
                foreach (var oc in Physics.OverlapSphere(stream.LastOrigin, 8f, ~0, QueryTriggerInteraction.Ignore))
                    sb.Append(' ').Append(oc.name).Append('[').Append(oc.attachedRigidbody != null ? oc.attachedRigidbody.name + "/" + oc.attachedRigidbody.mass.ToString("0") : "-").Append(" L").Append(oc.gameObject.layer).Append(']');
                var lc = light.GetComponent<Collider>();
                sb.Append(" | crate collider: ").Append(lc != null ? lc.enabled + " L" + light.gameObject.layer + " active " + light.gameObject.activeInHierarchy + " bounds " + lc.bounds : "none");
                ctx.Log(sb.ToString());
            }
            if (stream != null) ctx.Log("blower diag: origin " + stream.LastOrigin + " dir " + stream.LastDir + " seen " + stream.SeenTotal + " inCone " + stream.InConeTotal + " pushed " + stream.PushTotal + " blocked " + stream.BlockedTotal + " saturated " + stream.Saturated + " propPushes " + stream.PropPushes + " crateMass " + light.mass + " kin " + light.isKinematic + " lastSkip " + stream.LastSkip
                + " | shooter " + shooter.transform.position + " buddy " + buddy.Center + " crate " + light.position + " ticks " + stream.Ticks + " rate " + d.FireRate);
            ctx.Check(stream != null && stream.Ticks >= Mathf.FloorToInt(2.5f * d.FireRate * 0.7f), "blower: continuous ticks over 2.5 s at " + d.FireRate.ToString("0.0") + "/s (" + (stream != null ? stream.Ticks : 0) + ")");
            Object.Destroy(buddy.gameObject); Object.Destroy(light.gameObject); Object.Destroy(heavy.gameObject);
            yield return ctx.Wait(1.5f);

            // bot shove + 1 s stun, at most once per 2 s
            var bot = EnemyKit.SecurityBot(null, new Vector3(0f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f), new Vector3[0], "noCell", new Vector3(0f, 0f, 4f), 12f);
            yield return ctx.Wait(1.0f);
            Vector3 bp = bot.transform.position;
            int stuns = 0; bool wasStunned = false; float firstStun = -1f;
            float tBot = Time.time + 2.5f;
            for (int f = 0; Time.time < tBot && f < 3000; f++)
            {
                shooter.Weapon.FireAt(bot.transform.position + Vector3.up * 0.8f);
                if (bot.IsStunned && !wasStunned) { stuns++; if (firstStun < 0f) firstStun = bot.StunRemaining; }
                wasStunned = bot.IsStunned;
                yield return null;
            }
            ctx.Check(stuns >= 1 && stuns <= 2 && WETestKit.Planar(bot.transform.position, bp) > 0.3f,
                "blower: bot shoved " + WETestKit.Planar(bot.transform.position, bp).ToString("0.0") + " m, stunned " + stuns + "x in 2.5 s (first stun " + firstStun.ToString("0.0") + " s; enemies may override via Variant)");
            Object.Destroy(bot.gameObject); Object.Destroy(shooter.gameObject);
        }

        // ================================================================== T-SHIRT + PAINT
        static IEnumerator ShirtPaint(DevPlayContext ctx)
        {
            var shooter = WETestKit.Character(Archetype.Brute, Vector3.zero, 0f, 0);
            shooter.Weapon.Equip(Data(WeaponKind.TShirtCannon));
            var buddy = WETestKit.Character(Archetype.Sprinter, new Vector3(0f, 0f, 6f), 180f, 1);
            yield return ctx.Wait(0.5f);
            float hp = buddy.Health;
            shooter.Weapon.FireAt(buddy.Center);
            yield return WETestKit.WaitUntil(() => buddy.GetComponent<TShirtCosmetic>() != null, 2f);
            WETestKit.Cam(new Vector3(1.8f, 1.8f, 4f), buddy.Center);
            yield return ctx.Wait(0.2f);
            ctx.Shot("cw_tshirt_teammate");
            ctx.Check(buddy.GetComponent<TShirtCosmetic>() != null && Mathf.Approximately(buddy.Health, hp), "T-SHIRT: teammate gets the cosmetic shirt, no damage");
            Object.Destroy(buddy.gameObject);
            var bot = EnemyKit.SecurityBot(null, new Vector3(0f, 0f, 7f), Quaternion.Euler(0f, 180f, 0f), new Vector3[0], "noCell", new Vector3(0f, 0f, 7f), 12f);
            yield return ctx.Wait(1.2f);
            shooter.Weapon.FireAt(bot.transform.position + Vector3.up * 1.1f);
            yield return WETestKit.WaitUntil(() => bot.GetComponent<TShirtWrap>() != null, 2f);
            var wrap = bot.GetComponent<TShirtWrap>();
            WETestKit.Cam(new Vector3(2f, 2f, 5f), bot.transform.position + Vector3.up);
            yield return ctx.Wait(0.2f);
            ctx.Shot("cw_tshirt_wrapped_bot");
            ctx.Check(wrap != null && wrap.IsWrapped && wrap.Until - Time.time > 3f, "T-SHIRT: bot WRAPPED (cosmetic 4 s; blindness = enemies' Variant reaction)");
            Object.Destroy(bot.gameObject);

            // PAINT: splats cycle colors, pool <= 24
            shooter.Weapon.Equip(Data(WeaponKind.PaintBlaster));
            Kit.SolidBox(null, "PaintWall", new Vector3(0f, 2f, 9f), new Vector3(8f, 4f, 0.4f), MatKey.Wall);
            yield return ctx.Wait(0.3f);
            var cols = new HashSet<Color>();
            for (int i = 0; i < 30; i++)
            {
                shooter.Weapon.FireAt(new Vector3(-3f + (i % 7), 0.8f + (i / 7) * 0.7f, 8.8f));
                yield return ctx.Wait(0.68f);
            }
            yield return ctx.Wait(1f);
            foreach (var t in Object.FindObjectsByType<Transform>()) if (t.name == "PaintSplat") { var r = t.GetComponent<Renderer>(); if (r != null) cols.Add(r.sharedMaterial.color); }
            WETestKit.Cam(new Vector3(0f, 2.2f, 2f), new Vector3(0f, 2f, 9f));
            yield return null;
            ctx.Shot("cw_paint_wall");
            ctx.Check(PaintSplats.Count == PaintSplats.MaxSplats, "PAINT: splat pool capped at 24 (" + PaintSplats.Count + ") after 30 shots");
            ctx.Check(cols.Count >= 6, "PAINT: splats cycle the Reserve colors (" + cols.Count + " colors)");
            Object.Destroy(shooter.gameObject);
        }

        // ================================================================== BIT-SHIFTER
        static IEnumerator BitShifter(DevPlayContext ctx)
        {
            var shooter = WETestKit.Character(Archetype.Brainiac, Vector3.zero, 0f, 0);
            var d = WeaponGenerator.Tool(ExperimentalVariant.BitShifter);
            shooter.Weapon.Equip(d);
            var crate = WETestKit.Prop("Crate25", new Vector3(0f, 0.4f, 6f), Vector3.one * 0.8f, 25f, MatKey.Wood);
            bool real = MakeShiftable(crate);
            var buddy = WETestKit.Character(Archetype.Brute, new Vector3(3f, 0f, 6f), 180f, 1);
            yield return ctx.Wait(0.8f);
            WETestKit.Cam(new Vector3(-3f, 2.4f, 1f), new Vector3(0f, 0.8f, 6f));
            var r = crate.GetComponentInParent<IResizable>();
            if (r == null) { ctx.Log("BIT-SHIFTER: puzzles' Shiftable not available yet - size-cycle checks skipped"); }
            else
            {
                float[] want = { 100f, 6.25f, 25f };
                int[] cls = { 1, -1, 0 };
                for (int i = 0; i < 3; i++)
                {
                    shooter.Weapon.FireAt(crate.worldCenterOfMass);
                    yield return ctx.Wait(0.2f);
                    if (i == 0) ctx.Shot("cw_bitshifter_huge");
                    ctx.Check(r.SizeClass == cls[i] && Mathf.Abs(crate.mass - want[i]) < 0.6f, "BIT-SHIFTER shot " + (i + 1) + ": size class " + r.SizeClass + ", mass " + crate.mass.ToString("0.00") + " (want " + want[i] + ")");
                    yield return ctx.Wait(0.8f);
                }
                // NoToolZone fizzle
                ToolZones.PointQuery = (p, m) => (m & ToolMask.BitShifter) != 0 && Mathf.Abs(p.z - 6f) < 1.5f;
                shooter.Weapon.FireAt(crate.worldCenterOfMass);
                yield return ctx.Wait(0.2f);
                ctx.Check(r.SizeClass == 0 && WeaponTools.LastRefusal == ToolZones.Refusal, "BIT-SHIFTER in a NO-SIZE zone fizzles: " + WeaponTools.LastRefusal);
                ToolZones.PointQuery = null;
                yield return ctx.Wait(0.8f);
            }
            shooter.Weapon.FireAt(buddy.Center);
            yield return ctx.Wait(0.2f);
            ctx.Check(WeaponTools.LastRefusal == WeaponTools.InternNotBitAddressable && Mathf.Approximately(buddy.transform.localScale.x, 1f), "BIT-SHIFTER on an intern: " + WeaponTools.LastRefusal);
            ctx.Shot("cw_bitshifter_intern");
            yield return ctx.Wait(0.8f);
            var wall = Kit.SolidBox(null, "Wall", new Vector3(-4f, 1f, 6f), new Vector3(1f, 2f, 1f), MatKey.Wall);
            shooter.Weapon.FireAt(wall.transform.position);
            yield return ctx.Wait(0.2f);
            ctx.Check(WeaponTools.LastRefusal == WeaponTools.NotShiftable, "BIT-SHIFTER on scenery: " + WeaponTools.LastRefusal);
            ctx.Log("BIT-SHIFTER used " + (real ? "puzzles' PuzzleKit.MakeShiftable" : "no Shiftable"));
            Object.Destroy(shooter.gameObject); Object.Destroy(buddy.gameObject);
        }

        /// <summary>puzzles' PuzzleKit.MakeShiftable when published (CAMPAIGN 18.5).</summary>
        static bool MakeShiftable(Rigidbody rb)
        {
            try { return CWShiftableBridge.Make(rb); } catch (System.Exception e) { Debug.LogException(e); return false; }
        }

        // ================================================================== SWAP-O-MATIC
        static IEnumerator Swap(DevPlayContext ctx)
        {
            var shooter = WETestKit.Character(Archetype.Sprinter, Vector3.zero, 0f, 0);
            var d = WeaponGenerator.Tool(ExperimentalVariant.SwapOMatic);
            shooter.Weapon.Equip(d);
            var crate = WETestKit.Prop("Crate50", new Vector3(0f, 0.5f, 8f), Vector3.one * 1f, 50f, MatKey.Wood);
            yield return ctx.Wait(0.8f);
            WETestKit.Cam(new Vector3(-5f, 3f, 2f), new Vector3(0f, 0.8f, 5f));
            Vector3 s0 = shooter.Feet, c0 = crate.position;
            shooter.Weapon.FireAt(crate.worldCenterOfMass);
            yield return null;
            ctx.Shot("cw_swap_fire");
            yield return ctx.Wait(0.3f);
            ctx.Check(WETestKit.Planar(shooter.Feet, c0) < 0.3f && WETestKit.Planar(crate.position, s0) < 0.3f && Mathf.Abs(shooter.transform.eulerAngles.y) < 1f,
                "SWAP: shooter and 50 kg crate traded places, facing kept (shooter " + shooter.Feet + ", crate " + crate.position + ")");
            yield return ctx.Wait(1.6f);

            System.Func<Collider, string> why = col => WeaponTools.SwapCheck(shooter, col, out _);
            var heavy = WETestKit.Prop("Heavy300", new Vector3(4f, 0.5f, 3f), Vector3.one, 300f, MatKey.CrateHeavy);
            var kin = WETestKit.Prop("Mover", new Vector3(-4f, 0.5f, 3f), Vector3.one, 20f, MatKey.MetalLight); kin.isKinematic = true;
            var scenery = Kit.SolidBox(null, "Pillar", new Vector3(6f, 1f, 6f), new Vector3(1f, 2f, 1f), MatKey.Wall);
            var mate = WETestKit.Character(Archetype.Brute, new Vector3(-3f, 0f, 10f), 0f, 1);
            var mate2 = WETestKit.Character(Archetype.Brainiac, new Vector3(3f, 0f, 10f), 0f, 2);
            yield return ctx.Wait(0.5f);
            ctx.Check(why(heavy.GetComponent<Collider>()) == WeaponTools.TooHeavySwap, "SWAP excludes > 200 kg: " + why(heavy.GetComponent<Collider>()));
            ctx.Check(why(kin.GetComponent<Collider>()) == WeaponTools.CantSwap, "SWAP excludes kinematic movers: " + why(kin.GetComponent<Collider>()));
            ctx.Check(why(scenery) == WeaponTools.CantSwap, "SWAP excludes static scenery");
            ctx.Check(why(mate.Capsule) == null, "SWAP allows a Normal teammate");
            mate2.SetCondition(CharacterCondition.Interacting);
            ctx.Check(why(mate2.Capsule) == WeaponTools.Busy, "SWAP excludes an Interacting teammate: " + why(mate2.Capsule));
            mate2.SetCondition(CharacterCondition.Normal);
            mate2.Capture("noCell", "test");
            ctx.Check(why(mate2.Capsule) != null, "SWAP excludes a Captured teammate: " + why(mate2.Capsule));
            mate2.ReleaseFromCapture();
            mate2.KnockOut("test");
            ctx.Check(why(mate2.Capsule) == null, "SWAP allows a Downed teammate");
            mate2.Revive();

            // teammate swap
            Vector3 m0 = mate.Feet; s0 = shooter.Feet;
            bool swapped = WeaponTools.TrySwap(shooter.Weapon, mate.Capsule, mate.Center);
            yield return ctx.Wait(0.2f);
            ctx.Check(swapped && WETestKit.Planar(shooter.Feet, m0) < 0.3f && WETestKit.Planar(mate.Feet, s0) < 0.3f, "SWAP with a teammate");

            // clearance: crate under a low ceiling - the shooter can't fit there
            var low = WETestKit.Prop("CrateLow", new Vector3(10f, 0.3f, 4f), Vector3.one * 0.6f, 20f, MatKey.Wood);
            Kit.SolidBox(null, "LowCeiling", new Vector3(10f, 1.0f, 4f), new Vector3(3f, 0.2f, 3f), MatKey.Concrete);
            yield return ctx.Wait(0.5f);
            Vector3 before = shooter.Feet;
            bool ok = WeaponTools.TrySwap(shooter.Weapon, low.GetComponent<Collider>(), low.position);
            ctx.Check(!ok && WeaponTools.LastRefusal == WeaponTools.NoRoom && WETestKit.Planar(shooter.Feet, before) < 0.05f, "SWAP clearance: " + WeaponTools.LastRefusal);

            // NO-SWAP zone between the two ends
            ToolZones.SegmentQuery = (a, b, m) => (m & ToolMask.SwapOMatic) != 0;
            ok = WeaponTools.TrySwap(shooter.Weapon, mate.Capsule, mate.Center);
            ctx.Check(!ok && WeaponTools.LastRefusal == ToolZones.Refusal, "SWAP across a NO-SWAP zone fizzles: " + WeaponTools.LastRefusal);
            ToolZones.SegmentQuery = null;

            // guard bot: swap + stun 1.5 s
            var bot = EnemyKit.SecurityBot(null, new Vector3(-8f, 0f, 2f), Quaternion.identity, new Vector3[0], "noCell", new Vector3(-8f, 0f, 2f), 14f);
            yield return ctx.Wait(1.2f);
            Vector3 bp = bot.transform.position; before = shooter.Feet;
            ok = WeaponTools.TrySwap(shooter.Weapon, bot.GetComponentInChildren<Collider>(), bot.transform.position + Vector3.up);
            yield return ctx.Wait(0.1f);
            ctx.Check(ok && bot.IsStunned && WETestKit.Planar(bot.transform.position, before) < 0.5f && WETestKit.Planar(shooter.Feet, bp) < 0.5f, "SWAP with a guard bot: swapped + stunned (" + bot.StunRemaining.ToString("0.0") + " s)");
            Object.Destroy(bot.gameObject); Object.Destroy(shooter.gameObject); Object.Destroy(mate.gameObject); Object.Destroy(mate2.gameObject);
        }

        // ================================================================== PARTY POPPER, SAY CHEESE, CAPS-LOCK
        static IEnumerator PopperCheeseCaps(DevPlayContext ctx)
        {
            var shooter = WETestKit.Character(Archetype.Brute, Vector3.zero, 0f, 0);
            shooter.Weapon.Equip(WeaponGenerator.Tool(ExperimentalVariant.PartyPopper));
            float noiseR = 0f, noiseP = 0f;
            System.Action<NoiseEvent> onNoise = e => { if (e.Radius > noiseR) { noiseR = e.Radius; noiseP = e.Priority; } };
            GameEvents.Noise += onNoise;
            yield return ctx.Wait(0.4f);
            int lands = PartyFlare.LandCount;
            shooter.Weapon.FireAt(new Vector3(0f, 0f, 10f));
            yield return WETestKit.WaitUntil(() => PartyFlare.LandCount > lands, 3f);
            var f = PartyFlare.Current;
            WETestKit.Cam(new Vector3(-3f, 3f, 5f), f != null ? f.transform.position : new Vector3(0f, 0f, 10f));
            yield return ctx.Wait(0.3f);
            ctx.Shot("cw_partypopper_pool");
            ctx.Check(f != null && f.LightOn && Mathf.Approximately(f.LightRadius, 3f) && f.PointLight != null && f.PointLight.shadows == LightShadows.None && f.Remaining > 7f,
                "POPPER: lands, light pool r 3 m, one unshadowed point light, 8 s (" + (f != null ? f.Remaining.ToString("0.0") : "-") + " s left)");
            ctx.Check(Mathf.Approximately(noiseR, 14f) && Mathf.Approximately(noiseP, 2f), "POPPER: noise 14 m priority 2 (" + noiseR + "/" + noiseP + ")");
            ctx.Check(f != null && f is ILightSource && Vector3.Distance(f.LightPosition, new Vector3(0f, 0f, 10f)) < 2.5f, "POPPER: flare is an ILightSource at the landing point");
            int lights = 0; foreach (var l in Object.FindObjectsByType<Light>()) if (l.type == LightType.Point && l.enabled) lights++;
            yield return ctx.Wait(2.1f);
            ToolZones.PointQuery = (p, m) => (m & ToolMask.PartyPopper) != 0;
            lands = PartyFlare.LandCount;
            shooter.Weapon.FireAt(new Vector3(3f, 0f, 9f));
            yield return ctx.Wait(2.5f);
            ctx.Check(PartyFlare.LandCount == lands && WeaponTools.LastRefusal == ToolZones.Refusal, "POPPER landing in a no-tool zone fizzles");
            ToolZones.PointQuery = null;
            GameEvents.Noise -= onNoise;
            ctx.Check(lights <= 1, "POPPER: exactly one point light in the scene (" + lights + ")");
            if (PartyFlare.Current != null) PartyFlare.Current.Extinguish();

            // SAY CHEESE: a bot inside the cone (not on the centre ray) gets OnShot -> stunned
            shooter.Weapon.Equip(Data(WeaponKind.SayCheese));
            // facing away (+z) so it doesn't spot the shooter and walk out of the cone before the flash
            var bot = EnemyKit.SecurityBot(null, new Vector3(3f, 0f, 6f), Quaternion.identity, new Vector3[0], "noCell", new Vector3(3f, 0f, 6f), 12f);
            var dummy = WETestKit.Target(new Vector3(0f, 1.4f, 8f));
            yield return ctx.Wait(0.6f);
            ctx.Log("SAY CHEESE setup: bot at " + bot.transform.position + " state " + bot.State);
            shooter.Weapon.FireAt(dummy.transform.position);
            yield return null;
            WETestKit.Cam(new Vector3(-2f, 2.5f, -2f), new Vector3(1.5f, 1f, 6f));
            ctx.Shot("cw_saycheese_flash");
            yield return ctx.Wait(0.1f);
            ctx.Check(dummy.Hits == 1 && bot.IsStunned, "SAY CHEESE: centre target OnShot + cone bot stunned (hits " + dummy.Hits + ", stunned " + bot.IsStunned + ")");
            ctx.Check(dummy.Last.Damage == 0f, "SAY CHEESE: no damage");
            Object.Destroy(bot.gameObject); Object.Destroy(dummy.gameObject);

            // CAPS-LOCK: falling crate freezes mid-air, IFreezable gets Freeze(6), interns / scenery refuse
            shooter.Weapon.Equip(WeaponGenerator.Tool(ExperimentalVariant.CapsLock));
            var crate = WETestKit.Prop("FallingCrate", new Vector3(0f, 4f, 7f), Vector3.one * 0.8f, 25f, MatKey.Wood);
            yield return ctx.Wait(0.25f);
            shooter.Weapon.FireAt(crate.worldCenterOfMass);
            float y = crate.position.y;
            yield return ctx.Wait(1.5f);
            WETestKit.Cam(new Vector3(-2.5f, 2.5f, 3f), crate.position);
            ctx.Shot("cw_capslock_crate");
            ctx.Check(crate.isKinematic && Mathf.Abs(crate.position.y - y) < 0.05f && y > 1.5f, "CAPS-LOCK: falling crate locked mid-air at y " + y.ToString("0.00"));
            yield return ctx.Wait(5f);
            ctx.Check(!crate.isKinematic, "CAPS-LOCK: released after 6 s");
            var fz = new GameObject("Freezable").AddComponent<CWTestFreezable>();
            fz.transform.position = new Vector3(2f, 1.3f, 7f);
            fz.gameObject.AddComponent<BoxCollider>();
            yield return null;
            shooter.Weapon.FireAt(fz.transform.position);
            yield return ctx.Wait(0.1f);
            ctx.Check(Mathf.Approximately(fz.FrozenFor, 6f) && fz.Shots == 1, "CAPS-LOCK: IFreezable.Freeze(6) + OnShot");
            yield return ctx.Wait(2.1f);
            var buddy = WETestKit.Character(Archetype.Sprinter, new Vector3(-2f, 0f, 6f), 180f, 1);
            yield return ctx.Wait(0.3f);
            shooter.Weapon.FireAt(buddy.Center);
            yield return ctx.Wait(0.1f);
            ctx.Check(WeaponTools.LastRefusal == WeaponTools.AlreadyCaps, "CAPS-LOCK on an intern: " + WeaponTools.LastRefusal);
            yield return ctx.Wait(2.1f);
            var door = Kit.SolidBox(null, "Door", new Vector3(-5f, 1f, 7f), new Vector3(1.5f, 2f, 0.2f), MatKey.PuzzleBody);
            shooter.Weapon.FireAt(door.transform.position);
            yield return ctx.Wait(0.1f);
            ctx.Check(WeaponTools.LastRefusal == WeaponTools.CapsProof, "CAPS-LOCK on scenery: " + WeaponTools.LastRefusal);
            ToolZones.PointQuery = (p, m) => (m & ToolMask.CapsLock) != 0;
            yield return ctx.Wait(2.1f);
            var crate2 = WETestKit.Prop("Crate2", new Vector3(2f, 0.4f, 5f), Vector3.one * 0.8f, 25f, MatKey.Wood);
            yield return ctx.Wait(0.4f);
            shooter.Weapon.FireAt(crate2.worldCenterOfMass);
            yield return ctx.Wait(0.1f);
            ctx.Check(!crate2.isKinematic && WeaponTools.LastRefusal == ToolZones.Refusal, "CAPS-LOCK in a no-tool zone fizzles");
            ToolZones.PointQuery = null;
            Object.Destroy(buddy.gameObject); Object.Destroy(shooter.gameObject);
        }

        // ================================================================== lockers, armory, custody
        static IEnumerator Lockers(DevPlayContext ctx)
        {
            var a = WETestKit.Character(Archetype.Brute, new Vector3(0f, 0f, 0f), 0f, 0, Data(WeaponKind.Pistol));
            var b = WETestKit.Character(Archetype.Sprinter, new Vector3(1.5f, 0f, 0f), 0f, 1);
            var gun = a.Weapon.Data;
            var locker = WeaponKit.ToolLocker(null, new Vector3(0f, 0f, 3f), EnvKit.FacingViewerAt(new Vector3(0f, 0f, 3f), Vector3.zero), ExperimentalVariant.BitShifter);
            var locker2 = WeaponKit.ToolLocker(null, new Vector3(3f, 0f, 3f), EnvKit.FacingViewerAt(new Vector3(3f, 0f, 3f), new Vector3(3f, 0f, 0f)), ExperimentalVariant.SwapOMatic);
            yield return ctx.Wait(0.5f);
            WETestKit.Cam(new Vector3(1.5f, 2.2f, -3.5f), new Vector3(1.5f, 1.2f, 3f));
            ctx.Shot("cw_lockers");
            string reason;
            ctx.Check(locker.CanInteract(b, out reason) && locker.GetPrompt(b).Contains("BIT-SHIFTER") && locker.GetPrompt(b).Contains(gun.Name), "locker prompt: " + locker.GetPrompt(b));
            locker.Interact(b);
            ctx.Check(b.Weapon.HasWeapon && b.Weapon.Kind == WeaponKind.BitShifter && !a.Weapon.HasWeapon && WeaponCustody.Original == gun && locker.IsOut,
                "locker: B gets BIT-SHIFTER, A's gun stashed (one gun per team)");
            yield return ctx.Wait(0.6f);
            ctx.Shot("cw_locker_out");
            locker2.Interact(a);
            ctx.Check(a.Weapon.Kind == WeaponKind.SwapOMatic && !b.Weapon.HasWeapon && !locker.IsOut && locker2.IsOut && WeaponCustody.Original == gun,
                "second locker: BIT-SHIFTER goes home, A gets SWAP, original still stashed");
            yield return ctx.Wait(0.6f);
            locker2.Interact(b);
            ctx.Check(b.Weapon.Data == gun && !a.Weapon.HasWeapon && !locker2.IsOut && WeaponCustody.Original == null, "E again: tool back, B gets the original gun");
            yield return ctx.Wait(0.6f);
            // tool lying as a pickup (holder KO'd) is collected by the locker
            locker.Interact(a);
            a.KnockOut("test");
            yield return ctx.Wait(0.3f);
            ctx.Check(WeaponPickup.All.Count == 1 && WeaponStyle.IsTool(WeaponPickup.All[0].Data), "tool follows the drop rules (pickup on KO)");
            locker.Interact(b);
            ctx.Check(b.Weapon.Data == gun && WeaponPickup.All.Count == 0 && !locker.IsOut, "locker collects the dropped tool and returns the gun");
            a.Revive();
            yield return ctx.Wait(0.6f);
            // custody restore at level exit
            locker.Interact(a);
            ctx.Check(a.Weapon.Kind == WeaponKind.BitShifter && WeaponCustody.HasStash, "tool out before exit");
            WeaponCustody.RestoreOriginal();
            ctx.Check(a.Weapon.Data == gun && a.Profile.Weapon == gun && !b.Weapon.HasWeapon && !locker.IsOut && !WeaponCustody.HasStash, "RestoreOriginal: A holds the original again, profile synced, locker refilled");
            Object.Destroy(locker.gameObject); Object.Destroy(locker2.gameObject);
            yield return ctx.Wait(0.3f);

            // armory
            var armory = WeaponKit.Armory(null, new Vector3(0f, 0f, 5f), EnvKit.FacingViewerAt(new Vector3(0f, 0f, 5f), Vector3.zero),
                new[] { ExperimentalVariant.BitShifter, ExperimentalVariant.SwapOMatic, ExperimentalVariant.PartyPopper, ExperimentalVariant.CapsLock });
            yield return ctx.Wait(0.5f);
            WETestKit.Cam(new Vector3(0f, 2.4f, -0.5f), new Vector3(0f, 1.2f, 5f));
            ctx.Shot("cw_armory");
            armory.SlotOf(ExperimentalVariant.PartyPopper).Interact(b);
            ctx.Check(b.Weapon.Kind == WeaponKind.PartyPopper && !a.Weapon.HasWeapon && armory.Current == armory.SlotOf(ExperimentalVariant.PartyPopper), "armory: B picks the PARTY POPPER");
            yield return ctx.Wait(0.5f);
            armory.SlotOf(ExperimentalVariant.CapsLock).Interact(b);
            ctx.Check(b.Weapon.Kind == WeaponKind.CapsLock && armory.Current == armory.SlotOf(ExperimentalVariant.CapsLock) && !armory.SlotOf(ExperimentalVariant.PartyPopper).IsOut, "armory: one tool per team (popper returned)");
            yield return ctx.Wait(0.5f);
            armory.SlotOf(ExperimentalVariant.CapsLock).Interact(a);
            ctx.Check(a.Weapon.Data == gun && !b.Weapon.HasWeapon && armory.Current == null, "armory: returning the tool gives the original back");
            Object.Destroy(a.gameObject); Object.Destroy(b.gameObject); Object.Destroy(armory.gameObject);
        }

        static void Clear()
        {
            WeaponPickup.FallbackSpawn = null;
            ToolZones.PointQuery = null; ToolZones.SegmentQuery = null;
            if (PartyFlare.Current != null) PartyFlare.Current.Extinguish();
            PaintSplats.ClearAll();
            var cam = Camera.main;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (cam != null && go == cam.gameObject) continue;
                if (go.name == "[WeaponProjectiles]" || go.name == "PartyFlare" || go.name == "[PaintSplats]") continue;
                var l = go.GetComponent<Light>();
                if (l != null && l.type == LightType.Directional) continue;
                if (go.GetComponent<DevPlayHarness>() != null) continue;
                Object.Destroy(go);
            }
        }
    }
}
