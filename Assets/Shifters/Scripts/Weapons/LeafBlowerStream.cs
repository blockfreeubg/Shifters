// =====================================================================================
//  SHIFTERS - LeafBlowerStream.cs   [owned by the WEAPONS agent]
//  LEAF BLOWER continuous cone (8 m x 25 deg half-angle) while the trigger is held. WeaponHolder
//  fires a tick FireRate (4) times per second; each tick keeps the stream alive for 0.3 s and casts
//  the centre ray (IShootable.OnShot, at most once per 0.25 s per target; a Toggle ShootTarget only
//  on first contact). Between ticks FixedUpdate pushes:
//    props <= 60 kg toward the push speed (Knockback, 4.0-6.4 m/s); heavier props don't budge;
//    teammates nudged to <= 2 m/s along the stream, never tumbled (W2);
//    security bots pushed back toward 3 m/s while in the cone (beats their braking) + Stun(1 s) at most once per 2 s,
//    blown off a >= 1.5 m ledge within 2 s: Stun(3 s); OnShot(Variant LeafBlower) <= once per 0.25 s (SecurityBot does not
//    stun on it while SecurityBot.BlowerShovesItself, the default: fixes/enemies.md #1).
//    The cone follows the direction of the last trigger tick (cached), so it goes where the gun fired.
//  Added at runtime to the shooter (GetComponent/AddComponent via For).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class LeafBlowerStream : MonoBehaviour
    {
        public WeaponHolder Holder { get; private set; }
        public bool Blowing => Time.time < _until && Holder != null && Holder.HasWeapon && Holder.Kind == WeaponKind.LeafBlower;
        /// <summary>Bodies pushed during the last physics step (tests).</summary>
        public int PushedLastStep { get; private set; }
        public int Ticks { get; private set; }
        /// <summary>Diagnostics (tests): last stream origin/direction and totals of bodies seen / inside the cone / pushed.</summary>
        public Vector3 LastOrigin { get; private set; }
        public Vector3 LastDir { get; private set; }
        public int SeenTotal, InConeTotal, PushTotal, BlockedTotal, Saturated, PropPushes;
        public string LastSkip = "";

        float _until, _push = 5f, _range = 20f, _fxTimer;
        WeaponData _data;
        readonly Dictionary<Object, float> _lastHit = new Dictionary<Object, float>();
        Object _lastCentre;
        float _lastCentreTime = -999f;
        readonly Dictionary<SecurityBot, float> _lastShove = new Dictionary<SecurityBot, float>();
        Vector3 _dir = Vector3.forward, _muzzleLocal;
        readonly Dictionary<SecurityBot, float> _lastStun = new Dictionary<SecurityBot, float>();
        struct Drop { public SecurityBot Bot; public float Y0, Until; }
        readonly List<Drop> _drops = new List<Drop>(4);
        readonly List<Rigidbody> _done = new List<Rigidbody>(24);
        static readonly Collider[] _cols = new Collider[512];   // big: a full buffer silently drops bodies (level geometry is many colliders)

        public static LeafBlowerStream For(WeaponHolder h)
        {
            var s = h.GetComponent<LeafBlowerStream>();
            if (s == null) s = h.gameObject.AddComponent<LeafBlowerStream>();
            s.Holder = h;
            return s;
        }

        /// <summary>One trigger tick (from WeaponHolder.TryFire via WeaponTools.Fire).</summary>
        public void Tick(WeaponData d, Vector3 pivot, Vector3 dir, Vector3 muzzle, float muzzleDist)
        {
            _data = d;
            _push = Mathf.Max(0.5f, d.Knockback);
            _range = d.Range;
            _until = Time.time + 0.3f;
            _dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : transform.forward;
            _muzzleLocal = transform.InverseTransformPoint(muzzle);
            Ticks++;
            // centre ray: W3
            if (WeaponBallistics.Raycast(pivot, dir, d.Range + muzzleDist, Holder.transform, out var hit))
            {
                var sh = hit.collider.GetComponentInParent<IShootable>();
                Object key = sh as Object;
                if (key != null && CanHit(key)) WeaponTools.Notify(Holder, d, hit, dir);
                _lastCentre = key; _lastCentreTime = Time.time;
            }
            else { _lastCentre = null; }
            Fx.Steam(muzzle, dir, 0.7f, UIColors.Hex(0xE6F7D9));
            if (Random.value < 0.5f) Fx.Puff(muzzle + dir * Random.Range(2f, 6f) + Random.insideUnitSphere * 0.5f, 0.35f, UIColors.Hex(0x9BE15D));
        }

        bool CanHit(Object key)
        {
            float now = Time.time;
            if (_lastHit.TryGetValue(key, out float last) && now - last < WeaponTools.ContinuousHitInterval) return false;
            // a Toggle target flips only on first contact (stream must leave it first)
            var st = key as ShootTarget;
            if (st != null && st.Mode == ShootTargetMode.Toggle && key == _lastCentre && now - _lastCentreTime < 0.35f) return false;
            _lastHit[key] = now;
            return true;
        }

        void FixedUpdate()
        {
            PushedLastStep = 0;
            TickDrops();
            if (!Blowing) return;
            var c = Holder.Character;
            if (c == null || !c.IsAlive) return;
            Vector3 origin = transform.TransformPoint(_muzzleLocal);
            Vector3 dir = _dir;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : transform.forward;
            Vector3 pushDir = (flat + Vector3.up * Mathf.Clamp(dir.y, 0f, 0.5f)).normalized;
            float dt = Time.fixedDeltaTime;
            float len = Mathf.Min(WeaponTools.BlowerConeLength, _range);

            LastOrigin = origin; LastDir = dir;
            _done.Clear();
            int n = Physics.OverlapSphereNonAlloc(origin, len, _cols, ~0, QueryTriggerInteraction.Ignore);
            if (n == _cols.Length) Saturated++;
            for (int i = 0; i < n; i++)
            {
                var col = _cols[i];
                if (col == null) continue;
                var rb = col.attachedRigidbody;
                if (rb == null || _done.Contains(rb)) continue;
                _done.Add(rb);
                if (rb.transform.IsChildOf(transform)) continue;
                Vector3 p = rb.worldCenterOfMass;
                Vector3 to = p - origin;
                float dist = to.magnitude;
                SeenTotal++;
                if (dist > len || (dist > 0.3f && Vector3.Angle(dir, to) > WeaponTools.BlowerHalfAngle)) continue;
                InConeTotal++;
                float fall = Mathf.Clamp01(1.15f - dist / len); // stronger up close

                var ch = rb.GetComponent<ShifterCharacter>();
                if (ch != null)
                {
                    if (ch == c || !ch.IsAlive || ch.Motor == null || ch.Condition == CharacterCondition.Carried) continue;
                    Vector3 v = rb.linearVelocity; v.y = 0f;
                    float along = Vector3.Dot(v, flat);
                    float want = WeaponTools.BlowerNudgeMax - along;
                    if (want > 0.01f) { ch.Motor.AddImpulse(flat * Mathf.Min(want, 40f * dt * fall + 0.05f), false, 0f); PushedLastStep++; PushTotal++; }
                    continue;
                }
                var bot = rb.GetComponent<SecurityBot>();
                if (bot != null) { Shove(bot, rb, col, flat, p, dir, dt, fall); continue; }
                if (rb.isKinematic || rb.GetComponentInParent<WeaponProjectile>() != null) { LastSkip = rb.name + ":kinematic"; continue; }
                if (rb.mass > WeaponTools.BlowerMaxPropKg + 0.01f) { if (rb.mass < 150f) LastSkip = rb.name + ":mass " + rb.mass.ToString("0"); continue; }
                var g = rb.GetComponent<Grabbable>();
                if (g != null && g.IsHeld) { LastSkip = rb.name + ":held"; continue; }
                if (!WeaponTools.ClearLine(origin, p, rb.transform, transform)) { BlockedTotal++; LastSkip = rb.name + ":blocked by " + WeaponTools.LastBlocker; continue; }
                float alongP = Vector3.Dot(rb.linearVelocity, pushDir);
                // accel = friction compensation (grippy floors, mu ~1) + a falloff term; capped at the push speed so nothing rockets
                float dv = Mathf.Clamp(_push - alongP, 0f, (11f + 22f * fall) * dt * Mathf.Lerp(1.3f, 0.8f, rb.mass / WeaponTools.BlowerMaxPropKg));
                if (dv > 0f) { rb.AddForce(pushDir * dv, ForceMode.VelocityChange); rb.WakeUp(); PushedLastStep++; PushTotal++; PropPushes++; }
            }

            _fxTimer -= dt;
            if (_fxTimer <= 0f)
            {
                _fxTimer = 0.12f;
                Vector3 side = Vector3.Cross(Vector3.up, flat);
                Fx.Puff(origin + dir * Random.Range(1f, len) + side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-0.4f, 0.4f), 0.25f, UIColors.Hex(0xD8F7C0));
            }
        }

        void Shove(SecurityBot bot, Rigidbody rb, Collider col, Vector3 flat, Vector3 p, Vector3 dir, float dt, float fall)
        {
            if (!WeaponTools.ClearLine(transform.TransformPoint(_muzzleLocal), p, rb.transform, transform)) return;
            if (!rb.isKinematic)
            {
                // sustained push: out-accelerates the bot's own braking (18 m/s^2) up to the shove speed
                float along = Vector3.Dot(new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z), flat);
                float dv = Mathf.Clamp(WeaponTools.BlowerBotShove - along, 0f, 34f * dt * Mathf.Max(0.5f, fall));
                if (dv > 0f) { rb.AddForce(flat * dv, ForceMode.VelocityChange); PushedLastStep++; PushTotal++; }
            }
            float now = Time.time;
            if (!_lastStun.TryGetValue(bot, out float ls) || now - ls >= WeaponTools.BlowerBotStunEvery)
            {
                _lastStun[bot] = now;
                bot.Stun(WeaponTools.BlowerBotStun, "WHOOSH!");
                _drops.Add(new Drop { Bot = bot, Y0 = bot.transform.position.y, Until = now + 2.5f });
            }
            // OnShot for everything else that listens (SecurityBot itself does not stun on it: BlowerShovesItself)
            if (_lastShove.TryGetValue(bot, out float last) && now - last < WeaponTools.ContinuousHitInterval) return;
            _lastShove[bot] = now;
            if (_lastHit.TryGetValue(bot, out float lh) && now - lh < WeaponTools.ContinuousHitInterval) return;  // centre ray just did
            _lastHit[bot] = now;
            var shot = new ShotInfo
            {
                Point = p, Normal = -dir, Direction = flat, Damage = 0f, Knockback = 0f,
                Weapon = _data != null ? _data.Type : WeaponType.Experimental, Variant = ExperimentalVariant.LeafBlower,
                Shooter = Holder != null ? Holder.Character : null, Collider = col,
            };
            WeaponBallistics.NotifyShootable(shot);
        }

        void TickDrops()
        {
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (d.Bot == null || Time.time > d.Until) { _drops.RemoveAt(i); continue; }
                if (d.Bot.transform.position.y < d.Y0 - WeaponTools.BlowerLedgeDrop)
                {
                    d.Bot.Stun(WeaponTools.BlowerLedgeStun, "WHEEE!");
                    _drops.RemoveAt(i);
                }
            }
        }
    }
}
