// =====================================================================================
//  SHIFTERS - WeaponPickup.cs   [owned by the WEAPONS agent]
//  A gun lying in the world: floating, bobbing, spinning model above a glowing ring.
//  "[E] Pick up <name>" for Alive characters. Never lost for good: if it falls below
//  Tuning.LethalFallY it reappears at WeaponPickup.FallbackSpawn (set by the level module on
//  checkpoints) or its first resting spot. Built by WeaponKit.Pickup (edit-mode safe).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class WeaponPickup : MonoBehaviour, IInteractable
    {
        /// <summary>All pickups in the scene (registry).</summary>
        public static readonly List<WeaponPickup> All = new List<WeaponPickup>();
        /// <summary>Set by the level module (checkpoints): where guns lost to the void reappear. Null = unknown.</summary>
        public static Vector3? FallbackSpawn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); FallbackSpawn = null; }

        public WeaponData Data;
        [Tooltip("Spinning/bobbing model root (child).")] public Transform Visual;
        [Tooltip("Glow ring on the floor (child).")] public Transform Ring;
        public float HoverHeight = 0.8f;
        public float SpinSpeed = 90f;

        Vector3 _vel;
        bool _falling, _popping;
        float _popT, _probeTimer, _age;
        Vector3 _popFrom, _popTo;
        Vector3? _home;
        static readonly RaycastHit[] _hits = new RaycastHit[12];

        // ------------------------------------------------------------------ IInteractable
        public int InteractPriority => 5;
        public Vector3 InteractPoint => transform.position;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || !who.IsAlive || Data == null || _popping) return false;
            if (who.Condition != CharacterCondition.Normal) return false;
            if (who.Weapon == null) { blockedReason = "Can't hold weapons"; return false; }
            if (who.Weapon.HasWeapon) { blockedReason = "Already holding the " + who.Weapon.Data.Name; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => "Pick up " + (Data != null ? Data.Name : "weapon");

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) { Sfx.At(SfxId.Denied, transform.position, 0.6f); return; }
            var d = Data;
            Data = null;              // Equip() removes pickups of the same data: make sure it is us only once
            who.Weapon.EquipFromPickup(d);
            Fx.Poof(transform.position, d.Tint, 0.7f);
            Kit.SafeDestroy(gameObject);
        }

        // ------------------------------------------------------------------ spawning
        /// <summary>Spawn a dropped gun: pops out of 'from' and lands on the ground below (or at 'forcedSpot', e.g. a
        /// checkpoint when the owner was lost to the void).</summary>
        public static WeaponPickup SpawnDropped(WeaponData data, Vector3 from, Vector3? forcedSpot = null)
        {
            if (data == null) return null;
            Vector3 start = forcedSpot ?? from;
            var p = WeaponKit.Pickup(null, start, data);
            if (forcedSpot.HasValue)
            {
                Fx.Poof(start + Vector3.up * p.HoverHeight, data.Tint, 1f);
                GameEvents.RaiseToast("The " + data.Name + " was returned to Lost & Found", UIColors.Weapon);
            }
            p.BeginPop(start, forcedSpot.HasValue);
            Sfx.At(SfxId.Pickup, start, 0.6f, 0.8f);
            return p;
        }

        /// <summary>Destroy every pickup carrying this exact WeaponData (it was equipped elsewhere).</summary>
        public static void RemovePickupsOf(WeaponData data)
        {
            if (data == null) return;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var p = All[i];
                if (p == null) { All.RemoveAt(i); continue; }
                if (p.Data == data) { p.Data = null; Kit.SafeDestroy(p.gameObject); }
            }
        }

        void BeginPop(Vector3 from, bool quiet)
        {
            _popping = true;
            _popT = 0f;
            _popFrom = from;
            Vector3 ground;
            if (FindGround(from + Vector3.up * 0.5f, 60f, out ground))
            {
                Vector3 side = quiet ? Vector3.zero : new Vector3(Mathf.Sin(Time.time * 7.3f), 0f, Mathf.Cos(Time.time * 7.3f)) * 0.6f;
                Vector3 target = ground + side;
                if (!quiet && FindGround(target + Vector3.up * 1.2f, 3f, out var g2)) target = g2; else target = ground;
                _popTo = target + Vector3.up * HoverHeight;
            }
            else { _popTo = from; _popping = false; _falling = true; }
            transform.position = from;
            if (Visual != null) Visual.localScale = Vector3.one * 0.2f;
        }

        // ------------------------------------------------------------------ lifecycle
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_popping)
            {
                _popT = Mathf.Min(1f, _popT + dt / 0.45f);
                Vector3 p = Vector3.Lerp(_popFrom, _popTo, _popT);
                p.y += Mathf.Sin(_popT * Mathf.PI) * 1.1f;
                transform.position = p;
                if (Visual != null) Visual.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(_popT));
                if (_popT >= 1f) { _popping = false; if (_home == null) _home = transform.position; }
            }
            else if (_falling)
            {
                _vel.y -= Tuning.Gravity * dt;
                Vector3 step = _vel * dt;
                if (FindGround(transform.position, -step.y + HoverHeight, out var g) && _vel.y < 0f)
                {
                    transform.position = g + Vector3.up * HoverHeight;
                    _falling = false; _vel = Vector3.zero;
                    if (_home == null) _home = transform.position;
                }
                else transform.position += step;
                if (transform.position.y < Tuning.LethalFallY) Respawn();
            }
            else
            {
                _probeTimer -= dt;
                if (_probeTimer <= 0f)
                {
                    _probeTimer = 0.3f;
                    if (Application.isPlaying && !FindGround(transform.position, HoverHeight + 0.35f, out var g))
                    { _falling = true; _vel = Vector3.zero; }
                    else if (Application.isPlaying && _home == null) _home = transform.position;
                }
            }
            Animate(dt);
        }

        void Animate(float dt)
        {
            if (Visual != null)
            {
                Visual.localPosition = new Vector3(0f, Mathf.Sin(_age * 2.4f) * 0.12f, 0f);
                Visual.localRotation = Quaternion.Euler(0f, _age * SpinSpeed, Mathf.Sin(_age * 1.3f) * 8f);
            }
            if (Ring != null)
            {
                float s = 1f + Mathf.Sin(_age * 3.1f) * 0.08f;
                Ring.localScale = new Vector3(s, 1f, s);
                bool grounded = !_falling && !_popping;
                Ring.gameObject.SetActive(grounded);
            }
        }

        void Respawn()
        {
            _falling = false; _vel = Vector3.zero;
            Vector3 spot = FallbackSpawn ?? (_home.HasValue ? _home.Value - Vector3.up * HoverHeight : new Vector3(0f, 2f, 0f));
            transform.position = spot + Vector3.up * HoverHeight;
            if (!FindGround(transform.position, HoverHeight + 0.35f, out _)) _falling = true; // will fall onto whatever is below
            if (transform.position.y < Tuning.LethalFallY + 5f) { transform.position = new Vector3(0f, 2f, 0f); _falling = false; }
            Fx.Poof(transform.position, Data != null ? Data.Tint : UIColors.Weapon, 1f);
            Sfx.At(SfxId.Pickup, transform.position, 0.7f, 1.2f);
            if (Data != null) GameEvents.RaiseToast("The " + Data.Name + " was returned to Lost & Found", UIColors.Weapon);
        }

        /// <summary>Ground point straight below origin within distance (ignores triggers, characters, bots, pickups).</summary>
        bool FindGround(Vector3 origin, float distance, out Vector3 point)
        {
            point = origin;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, Mathf.Max(0.05f, distance), ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<ShifterCharacter>() != null || h.collider.GetComponentInParent<SecurityBot>() != null) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb.mass < 20f) continue; // don't sit on pebbles/chickens
                if (h.distance < best) { best = h.distance; point = h.point; found = true; }
            }
            return found;
        }

        static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; float x = t - 1f; return 1f + c3 * x * x * x + c1 * x * x; }
    }
}
