// =====================================================================================
//  SHIFTERS - WeaponBallistics.cs   [owned by the WEAPONS agent]
//  Shared hit resolution for hitscan rays and physical projectiles:
//    IShootable.OnShot -> characters (friendly-fire comedy) -> rigidbody knockback / plunger pull.
//  Knockback is mass-aware: a 30 kg barrel flies, a 160 kg block barely budges.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class WeaponBallistics
    {
        /// <summary>Bodies of this mass receive the weapon's full knockback as velocity change (m/s).</summary>
        public const float ReferenceMass = 30f;
        /// <summary>Very light bodies get at most this multiple of the knockback.</summary>
        public const float MaxLightBoost = 1.25f;
        /// <summary>Friendly-fire damage per trigger pull (split across shotgun pellets, scaled down for fast guns).</summary>
        public const float FriendlyFireDamage = 5f;
        /// <summary>Hard cap on the velocity change a character receives from one hit.</summary>
        public const float MaxCharacterKnock = 9f;

        static readonly RaycastHit[] _hits = new RaycastHit[32];
        static readonly Dictionary<ShifterCharacter, float> _lastOuch = new Dictionary<ShifterCharacter, float>();
        static readonly string[] OuchLines = { "OW!", "HEY!", "Watch it!", "OUCH!", "Friendly fire!", "Was that on purpose?", "I'm telling HR!" };
        static readonly string[] PullLines = { "WHOA!", "Not the plunger!", "HEY!", "Unhand me!" };
        static int _ouchIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _lastOuch.Clear(); _ouchIndex = 0; }

        /// <summary>Mass factor: (ReferenceMass/mass)^2, capped. 30 kg -> 1, 60 kg -> 0.25, 160 kg -> 0.035.</summary>
        public static float MassFactor(float mass)
        {
            float f = ReferenceMass / Mathf.Max(0.5f, mass);
            return Mathf.Min(MaxLightBoost, f * f);
        }

        /// <summary>Velocity change (m/s) a body of this mass gets from a knockback value.</summary>
        public static float VelocityChangeFor(float knockback, float mass) => knockback * MassFactor(mass);

        /// <summary>Nearest non-trigger hit along the ray, skipping colliders under ignoreRoot (own body, carried prop, weapon)
        /// and anything closer than minDistance.</summary>
        public static bool Raycast(Vector3 origin, Vector3 dir, float range, Transform ignoreRoot, out RaycastHit best, float minDistance = 0f)
        {
            best = default;
            if (range <= 0f || dir.sqrMagnitude < 1e-6f) return false;
            int n = Physics.RaycastNonAlloc(origin, dir.normalized, _hits, range, ~0, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || h.distance < minDistance) continue;
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
            }
            return found;
        }

        /// <summary>W3 only: IShootable.OnShot on the owner of shot.Collider (no knockback, no friendly fire). True if delivered.</summary>
        public static bool NotifyShootable(in ShotInfo shot)
        {
            var col = shot.Collider;
            if (col == null) return false;
            var shootable = col.GetComponentInParent<IShootable>();
            if (shootable == null) return false;
            try { shootable.OnShot(shot); }
            catch (System.Exception e) { Debug.LogException(e); }
            return true;
        }

        /// <summary>Applies a weapon hit to whatever owns shot.Collider.
        /// pull = plunger (drag toward the shooter); share = fraction of one trigger pull (1/pellets);
        /// fireRate scales friendly-fire so fast guns are not instant teammate-killers.</summary>
        public static void ApplyHit(in ShotInfo shot, bool pull, float share = 1f, float fireRate = 2f)
        {
            var col = shot.Collider;
            if (col == null) return;

            var shootable = col.GetComponentInParent<IShootable>();
            if (shootable != null)
            {
                try { shootable.OnShot(shot); }
                catch (System.Exception e) { Debug.LogException(e); }
            }

            var ch = col.GetComponentInParent<ShifterCharacter>();
            if (ch != null)
            {
                if (ch != shot.Shooter) HitCharacter(ch, shot, pull, share, fireRate);
                return; // character bodies belong to their motor: never push them with raw forces
            }

            var rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            float factor = MassFactor(rb.mass);
            if (pull)
            {
                Vector3 shooterPos = shot.Shooter != null ? shot.Shooter.Center : shot.Point - shot.Direction * 5f;
                Vector3 to = shooterPos - shot.Point;
                float dist = new Vector2(to.x, to.z).magnitude;
                to.y = 0f;
                to = to.sqrMagnitude > 1e-4f ? to.normalized : -shot.Direction;
                float speed = Mathf.Clamp(dist * 1.15f, 3f, Mathf.Max(3f, shot.Knockback));
                rb.AddForce((to * speed + Vector3.up * 3.5f) * factor, ForceMode.VelocityChange);
            }
            else
            {
                Vector3 dir = (shot.Direction.normalized + Vector3.up * 0.2f).normalized;
                rb.AddForceAtPosition(dir * (shot.Knockback * factor * rb.mass), shot.Point, ForceMode.Impulse);
            }
        }

        static void HitCharacter(ShifterCharacter ch, in ShotInfo shot, bool pull, float share, float fireRate)
        {
            if (!ch.IsAlive) return;
            Vector3 dir;
            float kb;
            if (pull && shot.Shooter != null)
            {
                Vector3 to = shot.Shooter.Center - ch.Center;
                float dist = new Vector2(to.x, to.z).magnitude;
                to.y = 0f;
                dir = (to.normalized + Vector3.up * 0.35f).normalized;
                kb = Mathf.Min(Mathf.Clamp(dist * 1.1f, 3f, Mathf.Max(3f, shot.Knockback)) * Mathf.Sqrt(MassFactor(ch.Mass)) * 1.6f, MaxCharacterKnock);
            }
            else
            {
                dir = shot.Direction; dir.y = 0f;
                dir = (dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward) + Vector3.up * 0.3f;
                kb = Mathf.Min(shot.Knockback * Mathf.Sqrt(MassFactor(ch.Mass)) * 0.8f, MaxCharacterKnock);
            }
            Vector3 knock = dir.normalized * kb;
            float dmg = pull ? 0f : FriendlyFireDamage * Mathf.Clamp01(share) * Mathf.Min(1f, 3f / Mathf.Max(0.1f, fireRate));
            string who = shot.Shooter != null ? shot.Shooter.DisplayName : "a coworker";
            if (dmg > 0f) ch.Damage(dmg, knock, "was shot by " + who);
            else if (ch.Motor != null) ch.Motor.AddImpulse(knock, kb > 6f, 0.25f);

            float now = Time.time;
            if (!_lastOuch.TryGetValue(ch, out float last) || now - last > 1.4f)
            {
                _lastOuch[ch] = now;
                var lines = pull ? PullLines : OuchLines;
                ch.Say(lines[_ouchIndex++ % lines.Length]);
                Sfx.Voice(ch, VoiceLine.Hurt);
            }
        }

        /// <summary>Launch velocity (low arc) so a projectile of 'speed' lands on 'to'. Falls back to 45 degrees when out of reach.</summary>
        public static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float speed, float gravity)
        {
            Vector3 d = to - from;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float x = flat.magnitude;
            if (x < 0.05f || gravity <= 0f) return d.sqrMagnitude > 1e-6f ? d.normalized * speed : Vector3.forward * speed;
            float y = d.y, v2 = speed * speed;
            float disc = v2 * v2 - gravity * (gravity * x * x + 2f * y * v2);
            float angle = disc < 0f ? 45f * Mathf.Deg2Rad : Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (gravity * x));
            Vector3 dirFlat = flat / x;
            return (dirFlat * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * speed;
        }

        /// <summary>Random direction inside a cone of half-angle 'degrees' around dir (uniform over the disc).</summary>
        public static Vector3 Spread(Vector3 dir, float degrees, System.Random rng)
        {
            if (degrees <= 0.01f || rng == null) return dir;
            Vector3 n = dir.normalized;
            Vector3 perp = Vector3.Cross(n, Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            float around = (float)rng.NextDouble() * 360f;
            float off = Mathf.Sqrt((float)rng.NextDouble()) * degrees;
            Vector3 axis = Quaternion.AngleAxis(around, n) * perp;
            return Quaternion.AngleAxis(off, axis) * n;
        }

        /// <summary>Impact FX + sound for a hit (metal for bots/heavy bodies).</summary>
        public static void ImpactFx(Vector3 point, Vector3 normal, Collider col, WeaponKind kind, Color tint)
        {
            bool metal = col != null && (col.GetComponentInParent<SecurityBot>() != null || (col.attachedRigidbody != null && col.attachedRigidbody.mass >= 100f));
            if (kind == WeaponKind.Plunger)
            {
                Fx.Puff(point, 0.35f, UIColors.Hex(0xD9A066));
                Sfx.At(SfxId.Bonk, point, 0.6f, 1.3f);
                return;
            }
            if (kind == WeaponKind.TShirtCannon) { Fx.Puff(point, 0.5f, tint); Sfx.At(SfxId.Bonk, point, 0.7f, 1.2f); return; }
            if (kind == WeaponKind.PaintBlaster) { Fx.Splash(point, tint); Sfx.At(SfxId.PaintSplat, point, 0.8f, Random.Range(0.9f, 1.15f)); return; }
            if (kind == WeaponKind.PartyPopper) { Fx.Sparks(point, normal, 8, tint); return; }
            if (kind == WeaponKind.Laser) Fx.Sparks(point, normal, 6, tint);
            else Fx.Impact(point, normal, tint);
            if (metal) { Fx.Sparks(point, normal, 10, UIColors.AccentYellow); Sfx.At(SfxId.MetalHit, point, 0.8f); }
            else Sfx.At(kind == WeaponKind.Pistol && Random.value < 0.3f ? SfxId.Ricochet : SfxId.Impact, point, 0.7f, Random.Range(0.9f, 1.1f));
        }
    }
}
