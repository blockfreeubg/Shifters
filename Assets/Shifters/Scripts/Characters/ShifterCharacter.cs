// =====================================================================================
//  SHIFTERS - ShifterCharacter.cs   [CONTRACT API - implementation owned by the CORE agent]
//  The hub component of every playable character. Every other module talks to a character
//  through this class. Public members below are the contract: you may ADD members, never
//  rename/remove/change semantics of existing ones.
//
//  Ownership rules for the Rigidbody (who may write velocity / isKinematic / parent):
//    Condition.Normal / Interacting -> CharacterMotor
//    Condition.Thrown / Flopped / Stunned -> CharacterMotor (tumble physics) - abilities only request
//    Condition.Carried -> CharacterGrabber of the CARRIER (kinematic + parented to its CarryPoint)
//    Condition.Bridging -> CharacterBridge (kinematic plank)
//    Condition.Seated -> the machine that seated it (kinematic)
//  Whoever sets a non-Normal condition is responsible for restoring physics when leaving it.
//
//  State semantics (implemented here):
//    Downed   -> limp tumble (no auto get-up) until Revive, KO pose/face, stars, still Grabbable.
//                A Downed character may be Carried/Thrown by teammates (SetCondition allows those two).
//    Captured -> no control, no motor writes; the security bot moves the body.
//    Lost     -> no control; after ~1.5 s a poof hides renderers, disables colliders and makes the body
//                kinematic (GameObject stays alive for UI). Fell below Tuning.LethalFallY -> just hidden.
//    Alive (Revive / ReleaseFromCapture) -> everything above undone, stands up, poof.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class ShifterCharacter : MonoBehaviour
    {
        /// <summary>All characters currently in the scene (registry; cleared on SubsystemRegistration).</summary>
        public static readonly List<ShifterCharacter> All = new List<ShifterCharacter>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); AnyScaredChanged = null; }

        // ------------------------------------------------------------------ scared (campaign seam, story_kit Bravery)
        /// <summary>Scared flag (night chapters: dark + close to a scare). Purely presentational: NO stat change.
        /// Set by Bravery at ~4 Hz; listeners (visuals cower, voice, HUD) use ScaredChanged / AnyScaredChanged.</summary>
        public bool IsScared { get; private set; }
        /// <summary>Time.time of the last IsScared change.</summary>
        public float ScaredChangedTime { get; private set; }
        public event Action<ShifterCharacter> ScaredChanged;
        /// <summary>Static twin of ScaredChanged for every character (reset at SubsystemRegistration).</summary>
        public static event Action<ShifterCharacter> AnyScaredChanged;

        /// <summary>Sets IsScared; raises ScaredChanged + AnyScaredChanged only on a change. Only Alive characters can be
        /// scared (setting true on a non-Alive one is ignored; false always works). Becoming scared plays VisualAction.Cower.</summary>
        public void SetScared(bool scared)
        {
            if (scared && State != CharacterState.Alive) return;
            if (IsScared == scared) return;
            IsScared = scared;
            ScaredChangedTime = Time.time;
            if (scared && Visual != null) Visual.Play(VisualAction.Cower);
            try { ScaredChanged?.Invoke(this); AnyScaredChanged?.Invoke(this); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ identity
        public CharacterProfile Profile { get; private set; }
        public int Slot { get; private set; } = -1;
        public string DisplayName => Profile != null ? Profile.DisplayName : name;
        public Color Color => Profile != null ? Profile.PrimaryColor : Color.white;
        public Archetype Archetype => Profile != null ? Profile.Archetype : Archetype.Brute;
        public CharacterStats Stats => Profile != null ? Profile.Stats : null;

        // ------------------------------------------------------------------ components (resolved in Initialize)
        public Rigidbody Body { get; private set; }
        public CapsuleCollider Capsule { get; private set; }
        public CharacterMotor Motor { get; private set; }
        public CharacterVisual Visual { get; private set; }
        public CharacterGrabber Grabber { get; private set; }
        public Grabbable Grabbable { get; private set; }
        public CharacterBridge Bridge { get; private set; }
        public CharacterFlop Flop { get; private set; }
        public WeaponHolder Weapon { get; private set; }
        public CharacterAI AI { get; private set; }
        public PlayerBrain Brain { get; private set; }
        public WeightBody Weight { get; private set; }
        public InteractionSensor Sensor { get; private set; }

        // ------------------------------------------------------------------ anchors (children created in Initialize)
        /// <summary>Camera orbit pivot (around eye height).</summary>
        public Transform CameraTarget { get; private set; }
        /// <summary>Where carried objects/characters are held (above the head).</summary>
        public Transform CarryPoint { get; private set; }
        /// <summary>Flat box collider on top of the head so teammates can stand on it (stacking).</summary>
        public BoxCollider HeadPlatform { get; private set; }

        public float Height => Profile != null ? Profile.Body.Height : 1.8f;
        public float Radius => Profile != null ? Profile.Body.Radius : 0.4f;
        public Vector3 Feet => transform.position;
        public Vector3 Center => transform.position + Vector3.up * (Height * 0.5f);
        public Vector3 HeadTop => transform.position + Vector3.up * Height;

        // ------------------------------------------------------------------ state
        public CharacterState State { get; private set; } = CharacterState.Alive;
        public CharacterCondition Condition { get; private set; } = CharacterCondition.Normal;
        public float Health { get; private set; } = Tuning.MaxHealth;
        public float MaxHealth => Tuning.MaxHealth;
        public bool IsActive { get; private set; }
        public bool IsAlive => State == CharacterState.Alive;
        public bool IsSelectable => State == CharacterState.Alive;
        /// <summary>True when alive and free to move/act (Normal condition).</summary>
        public bool CanAct => IsAlive && Condition == CharacterCondition.Normal;
        /// <summary>Test bots / cutscenes: when true PlayerBrain, CharacterAI and ability input handlers do nothing;
        /// the external driver calls Motor.SetCommand and ability methods directly.</summary>
        public bool ExternalControl { get; set; }
        public string CapturedCellId { get; set; } = "";
        public string LastCause { get; private set; } = "";

        public float Mass => Profile != null ? Profile.Mass : (Body != null ? Body.mass : 80f);
        /// <summary>Own mass plus whatever it is carrying (used by weight sensors).</summary>
        public float TotalMass => Mass + (Grabber != null ? Grabber.CarriedMass : 0f);

        /// <summary>Perspectives this character currently sees (perks + weapon if held right now).</summary>
        public PerspectiveMask Perspectives
        {
            get
            {
                if (Profile == null) return PerspectiveMask.None;
                var m = Profile.Perspectives & ~PerspectiveMask.Weapon;
                if (Weapon != null && Weapon.HasWeapon) m |= PerspectiveMask.Weapon;
                return m;
            }
        }

        public event Action<ShifterCharacter> StateChanged;
        public event Action<ShifterCharacter> ConditionChanged;

        // ------------------------------------------------------------------ added state (core)
        /// <summary>True when the body has been hidden (Lost): renderers + colliders off, kinematic.</summary>
        public bool IsHidden { get; private set; }
        /// <summary>Time.time of the last State change.</summary>
        public float StateChangedTime { get; private set; }
        /// <summary>State before the last change (for banners: Downed -> Alive = "back on their feet").</summary>
        public CharacterState PreviousState { get; private set; } = CharacterState.Alive;
        /// <summary>True while StateChanged is raised from RestoreState (checkpoint restore): listeners should not show banners.</summary>
        public bool IsRestoringState { get; private set; }

        float _lastDamageTime = -999f;
        float _nextStarsTime;
        Coroutine _vanish;
        readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        readonly List<Collider> _hiddenColliders = new List<Collider>();
        const float StarsDuration = 4f;
        const float LostVanishDelay = 1.5f;

        // ------------------------------------------------------------------ lifecycle
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Called once by CharacterFactory right after all components were added.</summary>
        public void Initialize(CharacterProfile profile, int slot)
        {
            Profile = profile;
            Slot = slot;
            name = "Character_" + slot + "_" + profile.Name;
            Body = GetComponent<Rigidbody>();
            Capsule = GetComponent<CapsuleCollider>();
            Motor = GetComponent<CharacterMotor>();
            Visual = GetComponent<CharacterVisual>();
            Grabber = GetComponent<CharacterGrabber>();
            Grabbable = GetComponent<Grabbable>();
            Bridge = GetComponent<CharacterBridge>();
            Flop = GetComponent<CharacterFlop>();
            Weapon = GetComponent<WeaponHolder>();
            AI = GetComponent<CharacterAI>();
            Brain = GetComponent<PlayerBrain>();
            Weight = GetComponent<WeightBody>();
            Sensor = GetComponent<InteractionSensor>();

            float h = profile.Body.Height, r = profile.Body.Radius;
            Body.mass = profile.Mass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Capsule.height = h;
            Capsule.radius = r;
            Capsule.center = new Vector3(0f, h * 0.5f, 0f);

            CameraTarget = MakeAnchor("CameraTarget", new Vector3(0f, h * 0.82f, 0f));
            CarryPoint = MakeAnchor("CarryPoint", new Vector3(0f, h + 0.35f, 0.05f));
            var hp = new GameObject("HeadPlatform");
            hp.transform.SetParent(transform, false);
            hp.transform.localPosition = new Vector3(0f, h - 0.05f, 0f);
            HeadPlatform = hp.AddComponent<BoxCollider>();
            HeadPlatform.size = new Vector3(r * 1.5f, 0.1f, r * 1.5f);

            Health = Tuning.MaxHealth;
            State = CharacterState.Alive;
            Condition = CharacterCondition.Normal;
        }

        Transform MakeAnchor(string n, Vector3 local)
        {
            var t = new GameObject(n).transform;
            t.SetParent(transform, false);
            t.localPosition = local;
            return t;
        }

        /// <summary>CharacterManager ONLY. Enables PlayerBrain (active) or CharacterAI (inactive).</summary>
        public void SetActive(bool active)
        {
            IsActive = active;
            if (Brain != null) Brain.enabled = active;
            if (AI != null) AI.enabled = !active;
        }

        /// <summary>Request a condition change. Returns false if not allowed (e.g. character not alive,
        /// or trying to leave Carried while still held). Raises ConditionChanged + GameEvents.
        /// Downed characters may additionally enter Carried / Thrown (teammates carry KO'd friends).</summary>
        public bool SetCondition(CharacterCondition c)
        {
            if (Condition == c) return true;
            if (!IsAlive && c != CharacterCondition.Normal)
            {
                bool downedCarry = State == CharacterState.Downed && (c == CharacterCondition.Carried || c == CharacterCondition.Thrown);
                if (!downedCarry) return false;
            }
            Condition = c;
            ConditionChanged?.Invoke(this);
            GameEvents.RaiseCharacterConditionChanged(this);
            return true;
        }

        // ------------------------------------------------------------------ health & status
        public void Damage(float amount, Vector3 knockback, string cause)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            _lastDamageTime = Time.time;
            LastCause = cause;
            if (knockback.sqrMagnitude > 0.01f && Motor != null) Motor.AddImpulse(knockback, knockback.magnitude > 6f, 0.4f);
            GameEvents.RaiseCharacterDamaged(this, amount);
            GameEvents.RaiseCharacterHealthChanged(this);
            if (Health <= 0f) KnockOut(cause);
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            GameEvents.RaiseCharacterHealthChanged(this);
        }

        void Update()
        {
            // passive regeneration
            if (IsAlive && Health < MaxHealth && Time.time - _lastDamageTime > Tuning.RegenDelay)
            {
                Health = Mathf.Min(MaxHealth, Health + Tuning.RegenPerSecond * Time.deltaTime);
            }
            float y = transform.position.y;
            if (IsAlive && y < Tuning.LethalFallY) Lose("fell into the void");
            else if (State == CharacterState.Lost && !IsHidden && y < Tuning.LethalFallY) HideBody();
            if (State == CharacterState.Downed && Time.time >= _nextStarsTime)
            {
                _nextStarsTime = Time.time + StarsDuration - 0.1f;
                Fx.Stars(CameraTarget != null ? CameraTarget : transform, StarsDuration);
            }
        }

        public void KnockOut(string cause) => ChangeState(CharacterState.Downed, cause);
        public void Capture(string cellId, string cause) { CapturedCellId = cellId ?? ""; ChangeState(CharacterState.Captured, cause); }
        public void ReleaseFromCapture() { if (State == CharacterState.Captured) { CapturedCellId = ""; Health = Mathf.Max(Health, 50f); ChangeState(CharacterState.Alive, "rescued"); } }
        public void Lose(string cause) => ChangeState(CharacterState.Lost, cause);

        public void Revive(Vector3? position = null, Quaternion? rotation = null)
        {
            if (State == CharacterState.Lost) return; // lost is permanent for this attempt
            if (position.HasValue && Motor != null) Motor.Teleport(position.Value, rotation ?? transform.rotation);
            Health = MaxHealth;
            CapturedCellId = "";
            ChangeState(CharacterState.Alive, "revived");
            GameEvents.RaiseCharacterHealthChanged(this);
        }

        /// <summary>Restores a state directly (checkpoint restore). No banners.</summary>
        public void RestoreState(CharacterState s, float health, string cellId)
        {
            var prev = State;
            PreviousState = prev;
            State = s;
            StateChangedTime = Time.time;
            Health = health;
            CapturedCellId = cellId ?? "";
            if (s != CharacterState.Alive) LeaveConditionFor(s);
            if (prev != s || s != CharacterState.Alive) ApplyStateEffects(s, instant: true);
            IsRestoringState = true;
            try
            {
                StateChanged?.Invoke(this);
                GameEvents.RaiseCharacterStateChanged(this);
            }
            finally { IsRestoringState = false; }
        }

        void ChangeState(CharacterState s, string cause)
        {
            if (State == s) return;
            if (State == CharacterState.Lost) return;
            LastCause = cause;
            PreviousState = State;
            State = s;
            StateChangedTime = Time.time;
            if (s != CharacterState.Alive) LeaveConditionFor(s);
            if (s != CharacterState.Alive && IsScared) SetScared(false);
            ApplyStateEffects(s, instant: false);
            StateChanged?.Invoke(this);
            GameEvents.RaiseCharacterStateChanged(this);
        }

        /// <summary>Hands the body back from whatever owned it (bridge, carrier...) when the character stops being Alive.</summary>
        void LeaveConditionFor(CharacterState s)
        {
            if (Condition == CharacterCondition.Normal) return;
            if (Condition == CharacterCondition.Carried && s == CharacterState.Downed) return; // carrier keeps holding the KO'd friend
            if (Condition == CharacterCondition.Bridging && Bridge != null && Bridge.IsBridging) Bridge.Collapse();
            if (Condition == CharacterCondition.Carried && Grabbable != null && Grabbable.Holder != null) Grabbable.Holder.ForceRelease();
            if (Condition != CharacterCondition.Normal)
            {
                Condition = CharacterCondition.Normal;
                ConditionChanged?.Invoke(this);
                GameEvents.RaiseCharacterConditionChanged(this);
            }
        }

        void ApplyStateEffects(CharacterState s, bool instant)
        {
            if (Motor != null) Motor.ClearCommand();
            if (s != CharacterState.Alive && Grabber != null && Grabber.IsHolding) Grabber.ForceRelease();
            switch (s)
            {
                case CharacterState.Downed:
                    CancelVanish();
                    if (IsHidden) ShowBody();
                    if (Condition != CharacterCondition.Carried)
                    {
                        EnsureDynamic();
                        if (Motor != null) Motor.BeginTumble(1f);
                    }
                    if (Visual != null) { Visual.SetPose(PoseOverride.KO); Visual.SetExpression(Expression.KO); Visual.SetDesaturated(true); }
                    _nextStarsTime = Time.time + StarsDuration - 0.1f;
                    Fx.Stars(CameraTarget != null ? CameraTarget : transform, StarsDuration);
                    break;
                case CharacterState.Captured:
                    CancelVanish();
                    if (IsHidden) ShowBody();
                    if (Motor != null && !Body.isKinematic && Motor.IsTumbling) Motor.StandUp();
                    if (Visual != null) { Visual.SetPose(PoseOverride.Captured); Visual.SetExpression(Expression.Scared); Visual.SetDesaturated(true); }
                    break;
                case CharacterState.Lost:
                    if (Visual != null) { Visual.SetExpression(Expression.Scared); Visual.SetDesaturated(true); }
                    if (instant || transform.position.y < Tuning.LethalFallY + 1f) HideBody();
                    else { CancelVanish(); _vanish = StartCoroutine(VanishAfter(LostVanishDelay)); }
                    break;
                default: // Alive
                    CancelVanish();
                    if (IsHidden) ShowBody();
                    if (Condition == CharacterCondition.Normal)
                    {
                        // a bot / cell may have parented us: go back under the team root
                        var home = CharacterManager.Instance != null ? CharacterManager.Instance.transform : null;
                        if (home != null && transform.parent != home) transform.SetParent(home, true);
                        EnsureDynamic();
                    }
                    if (Motor != null && Body != null && !Body.isKinematic) Motor.StandUp();
                    if (Visual != null) { Visual.SetPose(PoseOverride.None); Visual.SetDesaturated(false); Visual.SetExpression(Expression.Happy, 1.5f); }
                    if (!instant) Fx.Poof(Center, Color, 1f);
                    break;
            }
        }

        void EnsureDynamic()
        {
            if (Body == null) return;
            if (Body.isKinematic) Body.isKinematic = false;
        }

        IEnumerator VanishAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _vanish = null;
            if (State != CharacterState.Lost || IsHidden) yield break;
            Fx.Poof(transform.TransformPoint(Capsule != null ? Capsule.center : Vector3.up), Color, 1.2f);
            HideBody();
        }

        void CancelVanish()
        {
            if (_vanish != null) { StopCoroutine(_vanish); _vanish = null; }
        }

        /// <summary>Hide the body (Lost): renderers off, colliders off, kinematic. GameObject stays alive for UI.</summary>
        void HideBody()
        {
            if (IsHidden) return;
            IsHidden = true;
            _hiddenRenderers.Clear();
            _hiddenColliders.Clear();
            GetComponentsInChildren(false, _hiddenRenderers);
            for (int i = _hiddenRenderers.Count - 1; i >= 0; i--)
            {
                if (_hiddenRenderers[i].enabled) _hiddenRenderers[i].enabled = false; else _hiddenRenderers.RemoveAt(i);
            }
            GetComponentsInChildren(false, _hiddenColliders);
            for (int i = _hiddenColliders.Count - 1; i >= 0; i--)
            {
                if (_hiddenColliders[i].enabled) _hiddenColliders[i].enabled = false; else _hiddenColliders.RemoveAt(i);
            }
            if (Body != null)
            {
                if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
                Body.isKinematic = true;
            }
        }

        void ShowBody()
        {
            if (!IsHidden) return;
            IsHidden = false;
            foreach (var r in _hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var c in _hiddenColliders) if (c != null) c.enabled = true;
            _hiddenRenderers.Clear();
            _hiddenColliders.Clear();
            if (Body != null) Body.isKinematic = false;
        }

        /// <summary>Speech bubble + gibberish voice. UI/Audio listen to GameEvents.Speech.</summary>
        public void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            GameEvents.RaiseSpeech(this, text);
        }
    }
}
