// =====================================================================================
//  SHIFTERS - WeaponProjectile.cs   [owned by the WEAPONS agent]
//  Pooled physical projectiles: Slingshot pebble (gravity, dies on first hit) and the
//  Rubber Chicken (bouncy, squeaks on every bounce, 3 bounces, despawns after 4 s).
//  Campaign: T-SHIRT (rolled tee; bot hit = WRAPPED, teammate hit = cosmetic shirt, no damage),
//  PAINT BLOB (splat decal via PaintSplats), PARTY POPPER flare (lands -> PartyFlare; 1 active).
//  Hits resolve exactly like hitscan via WeaponBallistics.ApplyHit.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class WeaponProjectile : MonoBehaviour
    {
        public const int ChickenMaxBounces = 3;
        public const float ChickenLifetime = 4f;
        public const float PebbleLifetime = 3f;
        public const float ChickenRadius = 0.2f, PebbleRadius = 0.09f;
        public const float ShirtRadius = 0.16f, PaintRadius = 0.11f, PopperRadius = 0.1f;

        /// <summary>Collider radius of a projectile kind (spawn clearance in front of the shooter).</summary>
        public static float RadiusOf(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Chicken: return ChickenRadius;
                case WeaponKind.TShirtCannon: return ShirtRadius;
                case WeaponKind.PaintBlaster: return PaintRadius;
                case WeaponKind.PartyPopper: return PopperRadius;
                default: return PebbleRadius;
            }
        }

        /// <summary>Projectiles created so far (pool growth; tests check that firing reuses them).</summary>
        public static int CreatedCount { get; private set; }

        /// <summary>Projectiles currently in flight (registry; do not modify).</summary>
        public static readonly List<WeaponProjectile> Active = new List<WeaponProjectile>();
        static readonly List<WeaponProjectile> _free = new List<WeaponProjectile>();
        static Transform _root;
        static PhysicsMaterial _pebbleMat, _chickenMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active.Clear(); _free.Clear(); _root = null; _pebbleMat = null; _chickenMat = null; CreatedCount = 0; _paintIndex = 0; }
        static int _paintIndex;

        public WeaponKind Kind { get; private set; }
        public int Bounces { get; private set; }
        public int TargetsHit { get; private set; }
        public Vector3 LaunchPosition { get; private set; }
        /// <summary>Highest world Y reached this flight (tests: arcs).</summary>
        public float ApexY { get; private set; }
        public bool InFlight { get; private set; }
        public Rigidbody Body { get; private set; }

        SphereCollider _col;
        Renderer _paintRenderer;
        /// <summary>Paint color of this blob (PAINT BLASTER cycles the 7 Reserve colors per shot).</summary>
        public Color PaintColor { get; private set; }
        WeaponData _data;
        ShifterCharacter _shooter;
        float _spawnTime, _lastBounceTime, _launchSpeed;
        Vector3 _lastVelocity;
        readonly List<Collider> _ignored = new List<Collider>(4);
        readonly List<Object> _hitKeys = new List<Object>(8);
        static readonly List<Collider> _shooterCols = new List<Collider>(16);

        // ------------------------------------------------------------------ spawning / pooling
        /// <summary>Fire a pooled projectile. Returns it (in flight) or null if kind is not a projectile type.</summary>
        public static WeaponProjectile Launch(WeaponKind kind, Vector3 position, Vector3 velocity, WeaponData data, ShifterCharacter shooter)
        {
            if (!WeaponStyle.IsProjectile(kind)) return null;
            for (int i = Active.Count - 1; i >= 0; i--) if (Active[i] == null) Active.RemoveAt(i); // scene reload leftovers
            WeaponProjectile p = null;
            for (int i = _free.Count - 1; i >= 0; i--)
            {
                var f = _free[i];
                if (f == null) { _free.RemoveAt(i); continue; }
                if (f.Kind == kind) { p = f; _free.RemoveAt(i); break; }
            }
            if (kind == WeaponKind.PartyPopper) // one flare in flight per shooter (the landed light pool itself is global: PartyFlare.Current)
                for (int i = Active.Count - 1; i >= 0; i--) if (Active[i] != null && Active[i].Kind == WeaponKind.PartyPopper && Active[i]._shooter == shooter) Active[i].Despawn(false);
            if (p == null) p = Create(kind);
            p.Begin(position, velocity, data, shooter);
            return p;
        }

        static WeaponProjectile Create(WeaponKind kind)
        {
            if (_root == null) _root = new GameObject("[WeaponProjectiles]").transform;
            CreatedCount++;
            var go = new GameObject(kind == WeaponKind.Chicken ? "RubberChicken" : kind == WeaponKind.Slingshot ? "Pebble" : "Proj_" + kind);
            go.transform.SetParent(_root, false);
            go.SetActive(false);
            var p = go.AddComponent<WeaponProjectile>();
            p.Kind = kind;
            p.Body = go.AddComponent<Rigidbody>();
            p.Body.interpolation = RigidbodyInterpolation.Interpolate;
            p.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            p._col = go.AddComponent<SphereCollider>();
            if (kind == WeaponKind.Chicken)
            {
                p.Body.mass = 0.6f;
                p.Body.angularDamping = 0.4f;
                p._col.radius = ChickenRadius;
                if (_chickenMat == null)
                    _chickenMat = new PhysicsMaterial("RubberChicken") { bounciness = 0.82f, bounceCombine = PhysicsMaterialCombine.Maximum, dynamicFriction = 0.25f, staticFriction = 0.25f, frictionCombine = PhysicsMaterialCombine.Minimum };
                p._col.sharedMaterial = _chickenMat;
                BuildChicken(go.transform);
            }
            else if (kind == WeaponKind.TShirtCannon || kind == WeaponKind.PaintBlaster || kind == WeaponKind.PartyPopper)
            {
                p.Body.mass = kind == WeaponKind.TShirtCannon ? 0.4f : 0.2f;
                p._col.radius = RadiusOf(kind);
                if (_pebbleMat == null)
                    _pebbleMat = new PhysicsMaterial("Pebble") { bounciness = 0.2f, dynamicFriction = 0.5f, staticFriction = 0.5f };
                p._col.sharedMaterial = _pebbleMat;
                if (kind == WeaponKind.TShirtCannon) BuildShirt(go.transform);
                else if (kind == WeaponKind.PaintBlaster) p._paintRenderer = ProcMesh.Part("Blob", go.transform, ProcMesh.Cached("proj_paint", () => ProcMesh.Sphere(0.12f, 9, 6, true)),
                        MaterialLibrary.Unlit(PaintSplats.Colors[0], 1.2f), Vector3.zero, null, new Vector3(1f, 0.85f, 1.25f)).GetComponent<Renderer>();
                else BuildPopper(go.transform);
            }
            else
            {
                p.Body.mass = 0.25f;
                p._col.radius = PebbleRadius;
                if (_pebbleMat == null)
                    _pebbleMat = new PhysicsMaterial("Pebble") { bounciness = 0.2f, dynamicFriction = 0.5f, staticFriction = 0.5f };
                p._col.sharedMaterial = _pebbleMat;
                var mesh = ProcMesh.Cached("proj_pebble", () => ProcMesh.Sphere(0.1f, 7, 5, false));
                ProcMesh.Part("Stone", go.transform, mesh, MaterialLibrary.Colored(UIColors.Hex(0x8A8F98), 0.1f), Vector3.zero, null, new Vector3(1.1f, 0.85f, 1f));
            }
            return p;
        }

        static void BuildShirt(Transform t)
        {
            var roll = ProcMesh.Cached("proj_shirt_roll", () => ProcMesh.Cylinder(0.15f, 0.34f, 10, true));
            var band = ProcMesh.Cached("proj_shirt_band", () => ProcMesh.Cylinder(0.155f, 0.05f, 10, true));
            var pink = MaterialLibrary.Colored(UIColors.Hex(0xFF4D9A), 0.2f);
            var white = MaterialLibrary.Colored(UIColors.Hex(0xF4F4F4), 0.2f);
            ProcMesh.Part("Roll", t, roll, pink, Vector3.zero, Quaternion.Euler(0f, 0f, 90f));
            ProcMesh.Part("BandL", t, band, white, new Vector3(-0.1f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));
            ProcMesh.Part("BandR", t, band, white, new Vector3(0.1f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));
        }

        static void BuildPopper(Transform t)
        {
            var cone = ProcMesh.Cached("proj_popper_cone", () => ProcMesh.Cone(0.08f, 0.2f, 8));
            var ball = ProcMesh.Cached("proj_popper_ball", () => ProcMesh.Sphere(0.07f, 8, 6, true));
            ProcMesh.Part("Cone", t, cone, MaterialLibrary.Colored(UIColors.Hex(0xFFD23C), 0.3f), new Vector3(0f, 0f, -0.06f), Quaternion.Euler(-90f, 0f, 0f));
            ProcMesh.Part("Glow", t, ball, MaterialLibrary.Unlit(UIColors.Hex(0xFF7AC8), 2f), new Vector3(0f, 0f, 0.08f), null, null, false);
        }

        static void BuildChicken(Transform t)
        {
            var body = ProcMesh.Cached("proj_chicken_body", () => ProcMesh.Sphere(0.17f, 10, 7, true));
            var head = ProcMesh.Cached("proj_chicken_head", () => ProcMesh.Sphere(0.085f, 8, 6, true));
            var beak = ProcMesh.Cached("proj_chicken_beak", () => ProcMesh.Cone(0.035f, 0.09f, 6));
            var comb = ProcMesh.Cached("proj_chicken_comb", () => ProcMesh.Box(new Vector3(0.025f, 0.06f, 0.09f), 0.01f));
            var leg = ProcMesh.Cached("proj_chicken_leg", () => ProcMesh.Cylinder(0.014f, 0.14f, 5));
            var white = MaterialLibrary.Colored(UIColors.Hex(0xFFE45C), 0.35f);
            var orange = MaterialLibrary.Colored(UIColors.Hex(0xFF8A1F), 0.3f);
            var red = MaterialLibrary.Colored(UIColors.Hex(0xE8394B), 0.3f);
            var black = MaterialLibrary.Colored(UIColors.Hex(0x15161A), 0.6f);
            ProcMesh.Part("Body", t, body, white, Vector3.zero, null, new Vector3(0.85f, 0.8f, 1.15f));
            ProcMesh.Part("Head", t, head, white, new Vector3(0f, 0.13f, 0.15f));
            ProcMesh.Part("Beak", t, beak, orange, new Vector3(0f, 0.12f, 0.23f), Quaternion.Euler(90f, 0f, 0f));
            ProcMesh.Part("Comb", t, comb, red, new Vector3(0f, 0.22f, 0.15f));
            var eye = ProcMesh.Cached("proj_chicken_eye", () => ProcMesh.Sphere(0.018f, 6, 4, true));
            ProcMesh.Part("EyeL", t, eye, black, new Vector3(-0.05f, 0.15f, 0.21f));
            ProcMesh.Part("EyeR", t, eye, black, new Vector3(0.05f, 0.15f, 0.21f));
            ProcMesh.Part("LegL", t, leg, orange, new Vector3(-0.05f, -0.15f, -0.02f), Quaternion.Euler(25f, 0f, 0f));
            ProcMesh.Part("LegR", t, leg, orange, new Vector3(0.05f, -0.15f, -0.02f), Quaternion.Euler(-15f, 0f, 0f));
        }

        void Begin(Vector3 position, Vector3 velocity, WeaponData data, ShifterCharacter shooter)
        {
            _data = data;
            _shooter = shooter;
            Bounces = 0;
            TargetsHit = 0;
            _hitKeys.Clear();
            _spawnTime = Time.time;
            _lastBounceTime = -1f;
            _launchSpeed = Mathf.Max(1f, velocity.magnitude);
            LaunchPosition = position;
            ApexY = position.y;
            transform.SetPositionAndRotation(position, velocity.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            gameObject.SetActive(true);
            IgnoreShooter(true);
            Body.position = position;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Kind == WeaponKind.Chicken ? new Vector3(Random.Range(-9f, 9f), Random.Range(-4f, 4f), Random.Range(-9f, 9f)) : Random.insideUnitSphere * 20f;
            _lastVelocity = velocity;
            if (Kind == WeaponKind.PaintBlaster)
            {
                PaintColor = PaintSplats.Colors[_paintIndex++ % PaintSplats.Colors.Length];
                if (_paintRenderer != null) _paintRenderer.sharedMaterial = MaterialLibrary.Unlit(PaintColor, 1.2f);
            }
            InFlight = true;
            if (!Active.Contains(this)) Active.Add(this);
        }

        void IgnoreShooter(bool ignore)
        {
            if (ignore)
            {
                _ignored.Clear();
                if (_shooter == null) return;
                _shooter.GetComponentsInChildren(false, _shooterCols); // no per-shot array allocation
                for (int i = 0; i < _shooterCols.Count; i++) { var c = _shooterCols[i]; if (c == null || c.isTrigger) continue; Physics.IgnoreCollision(_col, c, true); _ignored.Add(c); }
                _shooterCols.Clear();
            }
            else
            {
                for (int i = 0; i < _ignored.Count; i++) if (_ignored[i] != null && _col != null) Physics.IgnoreCollision(_col, _ignored[i], false);
                _ignored.Clear();
            }
        }

        /// <summary>Return to the pool (with a little poof).</summary>
        public void Despawn(bool poof = true)
        {
            if (!InFlight) return;
            InFlight = false;
            if (poof)
            {
                if (Kind == WeaponKind.Chicken) { Fx.Puff(transform.position, 0.6f, Color.white); Fx.Poof(transform.position, UIColors.Hex(0xFFE45C), 0.5f); }
                else Fx.Puff(transform.position, 0.25f, UIColors.Hex(0x8A8F98));
            }
            IgnoreShooter(false);
            Active.Remove(this);
            gameObject.SetActive(false);
            if (!_free.Contains(this)) _free.Add(this);
        }

        // ------------------------------------------------------------------ flight
        void FixedUpdate()
        {
            if (!InFlight) return;
            _lastVelocity = Body.linearVelocity;
            if (transform.position.y > ApexY) ApexY = transform.position.y;
        }

        void Update()
        {
            if (!InFlight) return;
            float life = Kind == WeaponKind.Chicken ? ChickenLifetime : Kind == WeaponKind.PartyPopper ? 4f : PebbleLifetime;
            if (Time.time - _spawnTime > life || transform.position.y < Tuning.LethalFallY) Despawn();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!InFlight) return;
            var other = collision.collider;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : -_lastVelocity.normalized;
            Vector3 dir = _lastVelocity.sqrMagnitude > 0.01f ? _lastVelocity.normalized : transform.forward;

            if (other != null && other.GetComponentInParent<WeaponProjectile>() == null)
            {
                Object key = HitKey(other);
                var hitChar = other.GetComponentInParent<ShifterCharacter>();
                if ((Kind == WeaponKind.TShirtCannon || Kind == WeaponKind.PartyPopper) && hitChar != null)
                {
                    // no friendly fire from merch or confetti: a free shirt / a party
                    if (hitChar != _shooter && !_hitKeys.Contains(hitChar))
                    {
                        _hitKeys.Add(hitChar); TargetsHit++;
                        if (Kind == WeaponKind.TShirtCannon) TShirtCosmetic.Give(hitChar);
                        else hitChar.Say("PARTY!");
                    }
                    key = null;
                }
                if (key != null && !_hitKeys.Contains(key))
                {
                    _hitKeys.Add(key);
                    TargetsHit++;
                    float speedFrac = Mathf.Clamp(_lastVelocity.magnitude / _launchSpeed, 0.4f, 1f);
                    var shot = new ShotInfo
                    {
                        Point = point, Normal = normal, Direction = dir,
                        Damage = _data != null ? _data.Damage : 10f,
                        Knockback = (_data != null ? _data.Knockback : 10f) * speedFrac,
                        Weapon = _data != null ? _data.Type : WeaponStyle.TypeOf(Kind),
                        Variant = _data != null ? _data.Variant : WeaponStyle.VariantOf(Kind),
                        Shooter = _shooter, Collider = other,
                    };
                    WeaponBallistics.ApplyHit(shot, false, 1f, _data != null ? _data.FireRate : 1.5f);
                    if (Kind == WeaponKind.TShirtCannon)
                    {
                        var bot = other.GetComponentInParent<SecurityBot>();
                        if (bot != null) TShirtWrap.Wrap(bot.transform, bot.HeadAnchor, TShirtWrap.WrapSeconds);
                    }
                }
            }

            if (Kind == WeaponKind.TShirtCannon || Kind == WeaponKind.PaintBlaster || Kind == WeaponKind.PartyPopper)
            {
                if (Kind == WeaponKind.PaintBlaster)
                {
                    PaintSplats.Splat(point, normal, other, PaintColor);
                    Fx.Splash(point, PaintColor);
                    Sfx.At(SfxId.PaintSplat, point, 0.8f, Random.Range(0.9f, 1.15f));
                    Despawn(false);
                }
                else if (Kind == WeaponKind.PartyPopper)
                {
                    PartyFlare.Land(point, normal, _shooter);
                    Despawn(false);
                }
                else
                {
                    Fx.Puff(point, 0.6f, UIColors.Hex(0xFF4D9A));
                    Sfx.At(SfxId.Bonk, point, 0.6f, 1.3f);
                    Despawn(false);
                }
                return;
            }

            if (Kind == WeaponKind.Slingshot)
            {
                Fx.Impact(point, normal, UIColors.Hex(0xC8CCD2));
                Sfx.At(SfxId.Impact, point, 0.6f, Random.Range(1.1f, 1.3f));
                Despawn();
                return;
            }

            // rubber chicken: squeak on every bounce, give up after a few
            if (Time.time - _lastBounceTime < 0.06f) return;
            _lastBounceTime = Time.time;
            Bounces++;
            Sfx.At(SfxId.Squeak, point, 0.9f, 0.9f + 0.12f * Bounces);
            Fx.Puff(point, 0.3f, Color.white);
            if (Bounces > ChickenMaxBounces) Despawn();
        }

        /// <summary>What counts as "one target" (a chicken hits each thing once per flight). Null = static scenery.</summary>
        static Object HitKey(Collider c)
        {
            var shootable = c.GetComponentInParent<IShootable>();
            if (shootable is Object so) return so;
            var ch = c.GetComponentInParent<ShifterCharacter>();
            if (ch != null) return ch;
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return rb;
            return null;
        }
    }
}
