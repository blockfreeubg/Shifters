// =====================================================================================
//  SHIFTERS - InteractionSensor.cs   [CONTRACT API - implementation owned by the ABILITIES agent]
//  On the ACTIVE character only: finds the best IInteractable within Tuning.InteractRadius (facing
//  bias + priority), publishes the prompt via GameEvents.RaisePrompt, E interacts. Also publishes
//  grab/throw/bridge prompts ("[F] Pick up Brenda", "[Q] Throw", "[B] Become a bridge").
//
//  Prompt priority: state prompts (bridging "[B] Get up", flopped "[X] Get up", carried "[Space] Wriggle free")
//  > IInteractable (blocked ones shown red with the reason) > holding "[Q] Throw x  [F] Put down"
//  > grab candidate "[F] Pick up x" (too-heavy PROPS -> blocked with "Needs STR 7 ...") > bridge hint
//  "[B] Become a bridge" (only at a ledge facing a 3..6 m gap). Raised only when it changes; cleared
//  (null) when the character stops being active. Scans at ~10 Hz.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class InteractionSensor : MonoBehaviour
    {
        public ShifterCharacter Character { get; protected set; }
        public IInteractable Current { get; protected set; }
        /// <summary>The prompt this sensor last published (null = none).</summary>
        public PromptInfo? CurrentPrompt => _last;
        public float ScanInterval = 0.1f;

        static InteractionSensor s_owner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_owner = null;

        readonly Collider[] _hits = new Collider[40];
        PromptInfo? _last;
        float _timer;
        bool _currentCan;
        string _currentReason;
        // cached non-interactable prompt (avoid string allocs while nothing changes)
        int _cacheKind = -1;
        Object _cacheTarget;
        PromptInfo _cachePrompt;

        protected virtual void Awake() { Character = GetComponent<ShifterCharacter>(); }
        protected virtual void OnEnable() { GameEvents.CharacterSwitched += OnSwitched; }
        protected virtual void OnDisable() { GameEvents.CharacterSwitched -= OnSwitched; Current = null; ClearPrompt(); }

        void OnSwitched(ShifterCharacter prev, ShifterCharacter next)
        {
            if (prev == Character && next != Character) { Current = null; ClearPrompt(); }
            if (next == Character) _timer = 0f;
        }

        protected virtual void Update()
        {
            if (Character == null) return;
            if (!Character.IsActive || !Character.IsAlive)
            {
                if (_last != null || s_owner == this) ClearPrompt();
                Current = null;
                return;
            }
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = ScanInterval;
                Scan();
                Publish(BuildPrompt());
            }
            if (AbilityInput.HasControl(Character) && ShiftersInput.InteractPressed) TryInteract();
        }

        /// <summary>Interact with the current best target. Returns true if something happened.</summary>
        public virtual bool TryInteract()
        {
            var c = Character;
            if (c == null || !c.IsAlive) return false;
            if (c.Condition != CharacterCondition.Normal && c.Condition != CharacterCondition.Interacting) return false;
            Scan();
            var it = Current;
            if (it == null) return false;
            if (!it.CanInteract(c, out var reason))
            {
                Sfx.Play(SfxId.Denied);
                if (c.Visual != null) c.Visual.Play(VisualAction.Shrug);
                if (!string.IsNullOrEmpty(reason)) AbilityFeedback.Toast(reason, UIColors.Danger, 0.5f);
                return false;
            }
            it.Interact(c);
            if (c.Visual != null) c.Visual.Play(VisualAction.Interact);
            _timer = 0f; // refresh the prompt next frame (state probably changed)
            return true;
        }

        /// <summary>Refresh Current now (normally ~10 Hz while active).</summary>
        public void Scan()
        {
            Current = null;
            _currentCan = false;
            _currentReason = null;
            var c = Character;
            if (c == null) return;
            Vector3 origin = c.Center;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            int n = Physics.OverlapSphereNonAlloc(origin, Tuning.InteractRadius, _hits, Physics.AllLayers, QueryTriggerInteraction.Collide);
            float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                _hits[i] = null;
                if (col == null) continue;
                if (c.Body != null && col.attachedRigidbody == c.Body) continue;
                var it = col.GetComponentInParent<IInteractable>();
                if (it == null) continue;
                if (it is Component comp && comp == null) continue;
                bool can = it.CanInteract(c, out var reason);
                if (!can && reason == null) continue; // hidden
                Vector3 d = it.InteractPoint - origin;
                Vector3 flat = new Vector3(d.x, 0f, d.z);
                float angle = flat.sqrMagnitude > 1e-4f ? Vector3.Angle(fwd, flat) : 0f;
                float score = d.magnitude + angle / 90f - it.InteractPriority * 1.5f;
                if (score < bestScore) { bestScore = score; Current = it; _currentCan = can; _currentReason = reason; }
            }
        }

        PromptInfo? BuildPrompt()
        {
            var c = Character;
            // 1) state prompts
            if (c.Bridge != null && c.Bridge.IsSolid)
                return Hit(1, null) ? _cachePrompt : Store(PromptInfo.Make(AbilityInput.Key("Bridge", "B"), "Get up (hop off the gap)"));
            if (c.Condition == CharacterCondition.Flopped)
                return Hit(2, null) ? _cachePrompt : Store(PromptInfo.Make(AbilityInput.Key("Flop", "X"), "Get up"));
            if (c.Grabbable != null && c.Grabbable.IsHeld)
                return Hit(3, null) ? _cachePrompt : Store(PromptInfo.Make(AbilityInput.Key("Jump", "Space"), "Wriggle free"));
            if (!c.CanAct && c.Condition != CharacterCondition.Interacting) { _cacheKind = -1; return null; }
            // 2) interactables
            if (Current != null)
            {
                _cacheKind = -1;
                string verb = Current.GetPrompt(c);
                string key = AbilityInput.Key("Interact", "E");
                return _currentCan ? PromptInfo.Make(key, verb) : PromptInfo.MakeBlocked(key, verb, _currentReason);
            }
            // 3) holding / grab
            var gr = c.Grabber;
            if (gr != null && gr.IsHolding)
            {
                var held = gr.Held;
                return Hit(4, held) ? _cachePrompt
                    : Store(PromptInfo.Make(AbilityInput.Key("Throw", "Q"), "Throw " + held.PromptName + "    [" + AbilityInput.Key("Grab", "F") + "] Put down"));
            }
            if (gr != null)
            {
                var best = gr.FindBestCandidate(out var heavy);
                if (best != null) return Hit(5, best) ? _cachePrompt : Store(PromptInfo.Make(AbilityInput.Key("Grab", "F"), "Pick up " + best.PromptName));
                // passive "too heavy" prompt only for props (standing next to the Brute shouldn't nag); pressing F still explains
                if (heavy != null && heavy.Character == null) return Hit(6, heavy) ? _cachePrompt : Store(PromptInfo.MakeBlocked(AbilityInput.Key("Grab", "F"), "Pick up " + heavy.PromptName, gr.TooHeavyReason(heavy)));
            }
            // 4) bridge hint
            var br = c.Bridge;
            if (br != null && br.ProbeGap(out _) && br.CanForm(out _))
                return Hit(7, null) ? _cachePrompt : Store(PromptInfo.Make(AbilityInput.Key("Bridge", "B"), "Become a bridge"));
            _cacheKind = -1;
            return null;
        }

        int _pendingKind;
        Object _pendingTarget;

        bool Hit(int kind, Object target)
        {
            if (kind == _cacheKind && target == _cacheTarget) return true;
            _pendingKind = kind;
            _pendingTarget = target;
            return false;
        }

        PromptInfo Store(PromptInfo p)
        {
            _cacheKind = _pendingKind;
            _cacheTarget = _pendingTarget;
            _cachePrompt = p;
            return p;
        }

        void Publish(PromptInfo? p)
        {
            if (Same(p, _last) && (p == null || s_owner == this)) return;
            _last = p;
            if (p != null) s_owner = this;
            if (p != null || s_owner == this) GameEvents.RaisePrompt(p);
            if (p == null && s_owner == this) s_owner = null;
        }

        void ClearPrompt()
        {
            _cacheKind = -1;
            bool had = _last != null;
            _last = null;
            if (s_owner == this)
            {
                s_owner = null;
                if (had) GameEvents.RaisePrompt(null);
            }
        }

        static bool Same(PromptInfo? a, PromptInfo? b)
        {
            if (a == null || b == null) return a == null && b == null;
            var x = a.Value; var y = b.Value;
            return x.Key == y.Key && x.Text == y.Text && x.Blocked == y.Blocked && x.BlockedReason == y.BlockedReason;
        }
    }
}
