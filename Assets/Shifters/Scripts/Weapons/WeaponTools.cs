// =====================================================================================
//  SHIFTERS - WeaponTools.cs   [owned by the WEAPONS agent]
//  Behaviour of the campaign hitscan kinds (CAMPAIGN 13.2), called from WeaponHolder.TryFire:
//    LEAF BLOWER (tick of the continuous cone -> LeafBlowerStream), SAY CHEESE (flash cone),
//    BIT-SHIFTER (IResizable cycle), SWAP-O-MATIC (shooter <-> target), CAPS-LOCK (IFreezable 6 s).
//  W3: every centre ray calls IShootable.OnShot on what it hits (Notify). W7: NoToolZone fizzle.
//  Projectile kinds (T-SHIRT, PAINT, PARTY POPPER) live in WeaponProjectile / PartyFlare / PaintSplats.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class WeaponTools
    {
        // ---- tunables (CAMPAIGN 13.2)
        public const float BlowerConeLength = 8f, BlowerHalfAngle = 25f, BlowerMaxPropKg = 60f, BlowerNudgeMax = 2f;
        public const float BlowerBotShove = 3f, BlowerBotStun = 1f, BlowerBotStunEvery = 2f, BlowerLedgeDrop = 1.5f, BlowerLedgeStun = 3f;
        public const float CheeseConeLength = 10f, CheeseHalfAngle = 35f, CheeseFlashSeconds = 0.12f;
        public const float SwapMaxKg = 200f, SwapNoiseRadius = 6f, SwapBotStun = 1.5f;
        public const float CapsLockSeconds = 6f;
        public const float BotTinyFallbackSeconds = 8f;
        /// <summary>Continuous fire: at most one OnShot per target per this many seconds.</summary>
        public const float ContinuousHitInterval = 0.25f;

        public const string InternNotBitAddressable = "ERROR: INTERNS ARE NOT BIT-ADDRESSABLE";
        public const string NotShiftable = "SYNTAX ERROR: NOT SHIFTABLE";
        public const string Overflow = "OVERFLOW ERROR";
        public const string NoRoom = "NO ROOM TO SWAP";
        public const string CantSwap = "CAN'T SWAP WITH THAT";
        public const string TooHeavySwap = "TOO HEAVY TO SWAP";
        public const string Busy = "BUSY - TRY LATER";
        public const string CapsProof = "CAPS-PROOF";
        public const string AlreadyCaps = "THEY ALREADY TYPE IN ALL CAPS";

        /// <summary>Last refusal / fizzle text of any tool (tests, HUD).</summary>
        public static string LastRefusal { get; private set; }
        public static float LastRefusalTime { get; private set; } = -999f;
        /// <summary>Last successful swap (tests): frame number.</summary>
        public static int LastSwapFrame { get; private set; } = -1;

        static readonly RaycastHit[] _ray = new RaycastHit[24];
        static readonly Collider[] _overlap = new Collider[512];
        static readonly List<Object> _seen = new List<Object>(16);
        static readonly Dictionary<int, float> _toastAt = new Dictionary<int, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { LastRefusal = null; LastRefusalTime = -999f; LastSwapFrame = -1; _seen.Clear(); _toastAt.Clear(); }

        // ================================================================== entry
        /// <summary>Hitscan part of a campaign kind's trigger pull (WeaponHolder.TryFire). Projectile kinds never come here.</summary>
        public static void Fire(WeaponHolder h, WeaponKind kind, WeaponData d, Vector3 pivot, Vector3 dir, Vector3 muzzle, float muzzleDist)
        {
            switch (kind)
            {
                case WeaponKind.LeafBlower: LeafBlowerStream.For(h).Tick(d, pivot, dir, muzzle, muzzleDist); break;
                case WeaponKind.SayCheese: FireCheese(h, d, pivot, dir, muzzle, muzzleDist); break;
                case WeaponKind.BitShifter: FireBitShifter(h, d, pivot, dir, muzzle, muzzleDist); break;
                case WeaponKind.SwapOMatic: FireSwap(h, d, pivot, dir, muzzle, muzzleDist); break;
                case WeaponKind.CapsLock: FireCapsLock(h, d, pivot, dir, muzzle, muzzleDist); break;
            }
        }

        // ================================================================== shared helpers
        /// <summary>W3 for tools: IShootable.OnShot only (no knockback, no friendly fire). True if something received it.</summary>
        public static bool Notify(WeaponHolder h, WeaponData d, in RaycastHit hit, Vector3 dir)
        {
            if (hit.collider == null) return false;
            var shot = new ShotInfo
            {
                Point = hit.point, Normal = hit.normal, Direction = dir, Damage = d != null ? d.Damage : 0f, Knockback = 0f,
                Weapon = d != null ? d.Type : WeaponType.Experimental, Variant = d != null ? d.Variant : ExperimentalVariant.RubberChickenLauncher,
                Shooter = h != null ? h.Character : null, Collider = hit.collider,
            };
            return WeaponBallistics.NotifyShootable(shot);
        }

        static bool Trace(WeaponHolder h, Vector3 pivot, Vector3 dir, float range, out RaycastHit hit)
            => WeaponBallistics.Raycast(pivot, dir, range, h != null ? h.transform : null, out hit);

        /// <summary>Floating text at the point + a (rate-limited) toast for the active shooter.</summary>
        public static void Refuse(WeaponHolder h, Vector3 at, string text, bool fizzle = false)
        {
            LastRefusal = text; LastRefusalTime = Time.time;
            Fx.FloatingText(at + Vector3.up * 0.4f, text, fizzle ? UIColors.Hex(0xC05CFF) : UIColors.Danger, 0.9f);
            if (fizzle) { Fx.Puff(at, 0.5f, UIColors.Hex(0xC05CFF)); Sfx.At(SfxId.Sizzle, at, 0.8f); }
            else Sfx.At(SfxId.Denied, at, 0.6f);
            var c = h != null ? h.Character : null;
            if (c != null && c.IsActive)
            {
                int key = text.GetHashCode();
                if (!_toastAt.TryGetValue(key, out float last) || Time.time - last > 1.2f)
                {
                    _toastAt[key] = Time.time;
                    GameEvents.RaiseToast(text, fizzle ? UIColors.Hex(0xC05CFF) : UIColors.Danger);
                }
            }
        }

        static bool ZoneFizzle(WeaponHolder h, WeaponKind kind, Vector3 at)
        {
            var m = WeaponStyle.ToolMaskOf(kind);
            if (m == ToolMask.None) return false;
            bool blocked = ToolZones.Blocks(at, m) || (h != null && h.Character != null && ToolZones.Blocks(h.Character.Center, m));
            if (blocked) Refuse(h, at, ToolZones.Refusal, true);
            return blocked;
        }

        static void Beam(WeaponKind kind, Vector3 muzzle, Vector3 end)
            => Fx.Tracer(muzzle, end, WeaponStyle.TracerColor(kind), WeaponStyle.TracerWidth(kind), WeaponStyle.TracerDuration(kind));

        static bool InCone(Vector3 origin, Vector3 fwd, Vector3 p, float length, float halfAngle)
        {
            Vector3 to = p - origin;
            float d = to.magnitude;
            if (d > length || d < 0.01f) return d <= length;
            return Vector3.Angle(fwd, to) <= halfAngle;
        }

        /// <summary>Clear line from a to b except colliders under 'allowRoot' / 'ignoreRoot' (triggers ignored).</summary>
        /// <summary>Name of the collider that last blocked ClearLine (diagnostics).</summary>
        public static string LastBlocker = "";
        public static bool ClearLine(Vector3 a, Vector3 b, Transform allowRoot, Transform ignoreRoot)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(a, d / len, _ray, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _ray[i].collider;
                if (c == null) continue;
                if (ignoreRoot != null && c.transform.IsChildOf(ignoreRoot)) continue;
                if (allowRoot != null && c.transform.IsChildOf(allowRoot)) continue;
                LastBlocker = c.name;
                return false;
            }
            return true;
        }

        // ================================================================== SAY CHEESE
        static void FireCheese(WeaponHolder h, WeaponData d, Vector3 pivot, Vector3 dir, Vector3 muzzle, float muzzleDist)
        {
            var shooter = h.Character;
            float range = d.Range + muzzleDist;
            Object centre = null;
            if (Trace(h, pivot, dir, range, out var hit))
            {
                Notify(h, d, hit, dir);
                centre = (Object)(hit.collider.GetComponentInParent<IShootable>() as Component) ?? hit.collider;
                Fx.Sparks(hit.point, hit.normal, 8, Color.white);
            }
            // the flash: screen white-out only for the active player; a bright puff in the world for everyone
            if (shooter != null && shooter.IsActive) PostFx.Flash(Color.white, CheeseFlashSeconds);
            Fx.Puff(muzzle + dir * 0.3f, 0.9f, Color.white);
            Fx.Sparks(muzzle, dir, 10, Color.white);

            // cone: enemies only (bots, security cameras, stalkers, the Boogeyman) - never puzzle targets
            _seen.Clear();
            if (centre != null) _seen.Add(centre);
            // security bots straight from their registry (never lost to a full overlap buffer / child triggers)
            var bots = SecurityBot.All;
            for (int i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null || _seen.Contains(bot)) continue;
                _seen.Add(bot);
                Vector3 bp = bot.transform.position + Vector3.up * 0.9f;
                if (!InCone(muzzle, dir, bp, CheeseConeLength, CheeseHalfAngle)) continue;
                if (!ClearLine(muzzle, bp, bot.transform, h.transform)) continue;
                var bc = bot.GetComponentInChildren<Collider>();
                var bs = new ShotInfo
                {
                    Point = bp, Normal = -dir, Direction = (bp - muzzle).normalized, Damage = 0f, Knockback = 0f,
                    Weapon = d.Type, Variant = d.Variant, Shooter = shooter, Collider = bc,
                };
                if (bc != null) WeaponBallistics.NotifyShootable(bs); else bot.OnShot(bs);
            }
            int n = Physics.OverlapSphereNonAlloc(muzzle, CheeseConeLength, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var sh = col.GetComponentInParent<IShootable>();
                var comp = sh as Component;
                if (comp == null || _seen.Contains(comp) || !IsFlashReactive(comp)) continue;
                _seen.Add(comp);
                Vector3 p = col.bounds.center;
                if (!InCone(muzzle, dir, p, CheeseConeLength, CheeseHalfAngle)) continue;
                if (!ClearLine(muzzle, p, comp.transform, h.transform)) continue;
                var shot = new ShotInfo
                {
                    Point = p, Normal = -dir, Direction = (p - muzzle).normalized, Damage = 0f, Knockback = 0f,
                    Weapon = d.Type, Variant = d.Variant, Shooter = shooter, Collider = col,
                };
                WeaponBallistics.NotifyShootable(shot);
            }
            // teammates in the cone blink
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == shooter || !c.IsAlive) continue;
                if (InCone(muzzle, dir, c.HeadTop - Vector3.up * 0.2f, CheeseConeLength * 0.6f, CheeseHalfAngle * 0.6f) && Random.value < 0.5f) c.Say("MY EYES!");
            }
        }

        /// <summary>Things the SAY CHEESE cone affects besides its centre ray: enemies, never puzzle parts.</summary>
        static bool IsFlashReactive(Component c)
        {
            if (c is SecurityBot) return true;
            string n = c.GetType().Name;
            return n.Contains("Camera") || n.Contains("Stalker") || n.Contains("Boogey") || n.Contains("Guard");
        }

        // ================================================================== BIT-SHIFTER
        static void FireBitShifter(WeaponHolder h, WeaponData d, Vector3 pivot, Vector3 dir, Vector3 muzzle, float muzzleDist)
        {
            float range = d.Range + muzzleDist;
            bool got = Trace(h, pivot, dir, range, out var hit);
            Vector3 end = got ? hit.point : pivot + dir * range;
            Beam(WeaponKind.BitShifter, muzzle, end);
            BinaryTrail(muzzle, end);
            if (!got) return;
            Notify(h, d, hit, dir);
            if (ZoneFizzle(h, WeaponKind.BitShifter, hit.point)) return;

            var ch = hit.collider.GetComponentInParent<ShifterCharacter>();
            if (ch != null) { Refuse(h, hit.point, InternNotBitAddressable); if (ch != h.Character) ch.Say("Rude."); return; }
            var r = hit.collider.GetComponentInParent<IResizable>();
            if (r != null)
            {
                string reason = null;
                bool ok;
                try { ok = r.TryCycleSize(h.Character, out reason); }
                catch (System.Exception e) { Debug.LogException(e); ok = false; }
                if (!ok) { Refuse(h, hit.point, string.IsNullOrEmpty(reason) ? Overflow : reason, reason == ToolZones.Refusal); return; }
                int sc = r.SizeClass;
                Fx.Poof(hit.point, WeaponStyle.Tint(WeaponKind.BitShifter), sc > 0 ? 1.4f : 0.7f);
                Fx.FloatingText(hit.point + Vector3.up * 0.6f, sc > 0 ? "<< HUGE" : sc < 0 ? ">> TINY" : "NORMAL", WeaponStyle.Tint(WeaponKind.BitShifter), 1.1f);
                Sfx.At(SfxId.BitShift, hit.point, 1f, sc > 0 ? 0.8f : sc < 0 ? 1.3f : 1f);
                return;
            }
            var bot = hit.collider.GetComponentInParent<SecurityBot>();
            if (bot != null) { bot.Stun(BotTinyFallbackSeconds, "BONK-BOT 3"); return; } // fallback until SecurityBot implements IResizable
            Refuse(h, hit.point, NotShiftable);
        }

        static readonly string[] Bits = { "1", "0", "<<", ">>", "01", "10" };
        static int _bit;
        static void BinaryTrail(Vector3 a, Vector3 b)
        {
            for (int i = 1; i <= 3; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / 4f) + Random.insideUnitSphere * 0.15f;
                Fx.FloatingText(p, Bits[_bit++ % Bits.Length], WeaponStyle.Tint(WeaponKind.BitShifter), 0.45f);
            }
        }

        // ================================================================== CAPS-LOCK
        static void FireCapsLock(WeaponHolder h, WeaponData d, Vector3 pivot, Vector3 dir, Vector3 muzzle, float muzzleDist)
        {
            float range = d.Range + muzzleDist;
            bool got = Trace(h, pivot, dir, range, out var hit);
            Vector3 end = got ? hit.point : pivot + dir * range;
            Beam(WeaponKind.CapsLock, muzzle, end);
            if (!got) return;
            Notify(h, d, hit, dir);
            if (ZoneFizzle(h, WeaponKind.CapsLock, hit.point)) return;
            var ch = hit.collider.GetComponentInParent<ShifterCharacter>();
            if (ch != null) { Refuse(h, hit.point, AlreadyCaps); return; }
            var f = hit.collider.GetComponentInParent<IFreezable>();
            if (f != null)
            {
                try { f.Freeze(CapsLockSeconds); } catch (System.Exception e) { Debug.LogException(e); }
                var fc = f as Component;
                CapsLockFreeze.Badge(fc != null ? fc.transform : hit.collider.transform, hit.point, CapsLockSeconds);
                Locked(hit.point);
                return;
            }
            var bot = hit.collider.GetComponentInParent<SecurityBot>();
            if (bot != null) { bot.Stun(CapsLockSeconds, "CAPS LOCK!"); CapsLockFreeze.Badge(bot.transform, hit.point, CapsLockSeconds); Locked(hit.point); return; }
            var rb = hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic && rb.GetComponentInParent<WeaponProjectile>() == null)
            {
                var g = rb.GetComponent<Grabbable>();
                if (g != null && g.IsHeld) { Refuse(h, hit.point, Busy); return; }
                CapsLockFreeze.Lock(rb, CapsLockSeconds);
                Locked(hit.point);
                return;
            }
            Refuse(h, hit.point, CapsProof);
        }

        static void Locked(Vector3 p)
        {
            Fx.FloatingText(p + Vector3.up * 0.5f, "LOCKED", WeaponStyle.Tint(WeaponKind.CapsLock), 1f);
            Fx.Sparks(p, Vector3.up, 10, WeaponStyle.Tint(WeaponKind.CapsLock));
        }

        // ================================================================== SWAP-O-MATIC
        /// <summary>Why a swap with this collider would be refused (null = allowed). Also returns the target's swap point.</summary>
        public static string SwapCheck(ShifterCharacter shooter, Collider col, out Vector3 targetPoint)
        {
            targetPoint = col != null ? col.bounds.center : Vector3.zero;
            if (shooter == null || col == null) return CantSwap;
            if (!shooter.IsAlive || shooter.Condition != CharacterCondition.Normal) return Busy;
            var sw = col.GetComponentInParent<ISwappable>();
            if (sw != null)
            {
                targetPoint = sw.SwapPoint;
                string reason = null;
                bool ok;
                try { ok = sw.CanSwap(shooter, out reason); } catch (System.Exception e) { Debug.LogException(e); ok = false; }
                return ok ? null : (string.IsNullOrEmpty(reason) ? CantSwap : reason);
            }
            var ch = col.GetComponentInParent<ShifterCharacter>();
            if (ch != null)
            {
                if (ch == shooter) return CantSwap;
                targetPoint = ch.Feet;
                if (ch.State == CharacterState.Captured) return "LOCKED UP - FREE THEM FIRST";
                if (ch.State == CharacterState.Lost) return CantSwap;
                if (ch.Grabbable != null && ch.Grabbable.IsHeld) return Busy;
                var cond = ch.Condition;
                if (ch.State == CharacterState.Downed) return cond == CharacterCondition.Carried || cond == CharacterCondition.Seated || cond == CharacterCondition.Bridging ? Busy : null;
                return cond == CharacterCondition.Normal || cond == CharacterCondition.Flopped || cond == CharacterCondition.Stunned ? null : Busy;
            }
            var bot = col.GetComponentInParent<SecurityBot>();
            if (bot != null)
            {
                targetPoint = bot.transform.position;
                return bot.State == BotState.Capture ? Busy : null;
            }
            var rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) return CantSwap;
            if (rb.GetComponentInParent<WeaponProjectile>() != null) return CantSwap;
            if (rb.mass > SwapMaxKg) return TooHeavySwap;
            var g = rb.GetComponent<Grabbable>();
            if (g != null && g.IsHeld) return Busy;
            targetPoint = BottomOf(rb);
            return null;
        }

        static void FireSwap(WeaponHolder h, WeaponData d, Vector3 pivot, Vector3 dir, Vector3 muzzle, float muzzleDist)
        {
            var shooter = h.Character;
            float range = d.Range + muzzleDist;
            bool got = Trace(h, pivot, dir, range, out var hit);
            Vector3 end = got ? hit.point : pivot + dir * range;
            Beam(WeaponKind.SwapOMatic, muzzle, end);
            if (!got) return;
            Notify(h, d, hit, dir);
            TrySwap(h, hit.collider, hit.point);
        }

        /// <summary>Full SWAP-O-MATIC resolution against a collider (also used by tests). True if the swap happened.</summary>
        public static bool TrySwap(WeaponHolder h, Collider col, Vector3 hitPoint)
        {
            var shooter = h != null ? h.Character : null;
            if (shooter == null || col == null) return false;
            string why = SwapCheck(shooter, col, out Vector3 B);
            if (why != null) { Refuse(h, hitPoint, why, why == ToolZones.Refusal); return false; }
            Vector3 A = shooter.Feet;
            if (ToolZones.BlocksSegment(A + Vector3.up * 0.5f, B + Vector3.up * 0.5f, ToolMask.SwapOMatic)) { Refuse(h, hitPoint, ToolZones.Refusal, true); return false; }

            var sw = col.GetComponentInParent<ISwappable>();
            var ch = sw == null ? col.GetComponentInParent<ShifterCharacter>() : null;
            var bot = sw == null && ch == null ? col.GetComponentInParent<SecurityBot>() : null;
            var rb = sw == null && ch == null && bot == null ? col.attachedRigidbody : null;
            Transform targetRoot = sw is Component swc ? swc.transform : ch != null ? ch.transform : bot != null ? bot.transform : rb != null ? rb.transform : col.transform;

            // clearance at both ends
            if (!CharacterFits(shooter, B, shooter.transform, targetRoot)) { Refuse(h, hitPoint, NoRoom); return false; }
            bool targetFits;
            if (ch != null) targetFits = CharacterFits(ch, A, shooter.transform, ch.transform);
            else targetFits = BoundsFit(targetRoot, B, A, shooter.transform);
            if (!targetFits) { Refuse(h, hitPoint, NoRoom); return false; }

            // swap (facing kept, velocity zeroed)
            if (sw != null)
            {
                try { sw.SwapTo(A, targetRoot.rotation); } catch (System.Exception e) { Debug.LogException(e); return false; }
            }
            else if (ch != null)
            {
                if (ch.Motor != null) ch.Motor.Teleport(A, ch.transform.rotation);
                else ch.transform.position = A;
                if (ch.Body != null && !ch.Body.isKinematic) { ch.Body.linearVelocity = Vector3.zero; ch.Body.angularVelocity = Vector3.zero; }
                ch.Say("...wait, where am I?");
            }
            else if (bot != null)
            {
                Vector3 off = bot.transform.position - B;
                if (bot.Body != null) { bot.Body.position = A + off; if (!bot.Body.isKinematic) { bot.Body.linearVelocity = Vector3.zero; bot.Body.angularVelocity = Vector3.zero; } }
                bot.transform.position = A + off;
                bot.Stun(SwapBotStun, "WHERE AM I?");
            }
            else if (rb != null)
            {
                Vector3 off = rb.position - B;
                rb.position = A + off;
                rb.transform.position = A + off;
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            }
            if (shooter.Motor != null) shooter.Motor.Teleport(B, shooter.transform.rotation);
            else shooter.transform.position = B;
            Physics.SyncTransforms();
            LastSwapFrame = Time.frameCount;

            // 0.2 s glitch + quiet noise
            Color c = WeaponStyle.Tint(WeaponKind.SwapOMatic);
            Fx.Poof(A + Vector3.up * 0.9f, c, 1.1f); Fx.Poof(B + Vector3.up * 0.9f, c, 1.1f);
            Fx.Sparks(A + Vector3.up, Vector3.up, 12, c); Fx.Sparks(B + Vector3.up, Vector3.up, 12, c);
            Fx.FloatingText(B + Vector3.up * 2.1f, "SWAP!", c, 1.2f);
            Sfx.At(SfxId.SwapZap, A, 0.9f); Sfx.At(SfxId.SwapZap, B, 0.9f, 1.15f);
            if (shooter.IsActive) PostFx.Burst(0.35f, 0.2f);
            GameEvents.RaiseNoise(B, SwapNoiseRadius, shooter, 0.5f);
            return true;
        }

        static readonly Collider[] _fit = new Collider[32];

        /// <summary>Would this character's capsule fit with its feet at 'feet'? Colliders under ignoreA / ignoreB don't count.</summary>
        public static bool CharacterFits(ShifterCharacter c, Vector3 feet, Transform ignoreA, Transform ignoreB)
        {
            float r = Mathf.Max(0.15f, c.Radius * 0.9f);
            float hgt = Mathf.Max(r * 2f + 0.1f, c.Height);
            Vector3 p0 = feet + Vector3.up * (r + 0.08f), p1 = feet + Vector3.up * (hgt - r);
            int n = Physics.OverlapCapsuleNonAlloc(p0, p1, r, _fit, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(_fit[i], ignoreA, ignoreB)) return false;
            return true;
        }

        /// <summary>Would 'root' (its collider bounds, base at basePoint) fit with its base at 'dest'?</summary>
        static bool BoundsFit(Transform root, Vector3 basePoint, Vector3 dest, Transform ignore)
        {
            var cols = root.GetComponentsInChildren<Collider>();
            bool any = false;
            Bounds b = default;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || cols[i].isTrigger || !cols[i].enabled) continue;
                if (!any) { b = cols[i].bounds; any = true; } else b.Encapsulate(cols[i].bounds);
            }
            if (!any) return true;
            Vector3 center = dest + (b.center - basePoint) + Vector3.up * 0.06f;
            Vector3 half = Vector3.Max(b.extents * 0.92f - Vector3.one * 0.02f, Vector3.one * 0.05f);
            int n = Physics.OverlapBoxNonAlloc(center, half, _fit, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(_fit[i], ignore, root)) return false;
            return true;
        }

        static bool Ignored(Collider c, Transform a, Transform b)
        {
            if (c == null) return true;
            var t = c.transform;
            return (a != null && t.IsChildOf(a)) || (b != null && t.IsChildOf(b));
        }

        /// <summary>Base centre of a body (bottom of its collider bounds under the centre of mass).</summary>
        public static Vector3 BottomOf(Rigidbody rb)
        {
            var cols = rb.GetComponentsInChildren<Collider>();
            bool any = false; Bounds b = default;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || cols[i].isTrigger) continue;
                if (!any) { b = cols[i].bounds; any = true; } else b.Encapsulate(cols[i].bounds);
            }
            if (!any) return rb.position;
            return new Vector3(b.center.x, b.min.y, b.center.z);
        }
    }
}
