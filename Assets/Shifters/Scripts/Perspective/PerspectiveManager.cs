// =====================================================================================
//  SHIFTERS - PerspectiveManager.cs   [CONTRACT API - implementation owned by the PERSPECTIVE agent]
//  SHIFT PERSPECTIVE: when the active character changes, the world is re-read through that
//  character's perks (+weapon). Revealables matching the mask light up / appear / get labels,
//  a scan pulse sweeps out, and the screen takes the perspective's tint. R re-pulses and shows
//  all labels for a few seconds. Persistent (created by GameBootstrap).
//
//  Refresh triggers: GameEvents.CharacterSwitched, the active character's state changes, and a
//  cheap 0.25 s poll (weapon picked up / dropped, Revealables created later, teammates spawned).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class PerspectiveManager : MonoBehaviour
    {
        public static PerspectiveManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public PerspectiveMask Current { get; protected set; }
        public ShifterCharacter Viewer { get; protected set; }
        /// <summary>True for a few seconds after a pulse (labels fully shown).</summary>
        public bool Focused { get; protected set; }

        public const float PulseCooldown = 1.5f;
        public const float FocusDuration = 5f;
        public const float PollInterval = 0.25f;
        public const float TintWeight = 0.35f;
        public const float PulseRadius = 30f, PulseDuration = 0.9f;

        /// <summary>Total scan pulses fired (switches + R).</summary>
        public int PulseCount { get; private set; }
        /// <summary>Number of Revealables currently revealed.</summary>
        public int RevealedCount { get; private set; }
        /// <summary>Seconds left on the R cooldown.</summary>
        public float CooldownLeft { get; private set; }
        public PerspectiveLabels Labels { get; private set; }

        float _focusLeft, _pollLeft, _tintLeft;
        ShifterCharacter _stateSub;

        protected virtual void Awake()
        {
            Instance = this;
            var labels = new GameObject("PerspectiveLabels");
            labels.transform.SetParent(transform, false);
            Labels = labels.AddComponent<PerspectiveLabels>();
        }

        protected virtual void OnEnable()
        {
            GameEvents.CharacterSwitched += OnSwitched;
            GameEvents.TeamSpawned += OnTeamSpawned;
        }

        protected virtual void OnDisable()
        {
            GameEvents.CharacterSwitched -= OnSwitched;
            GameEvents.TeamSpawned -= OnTeamSpawned;
            WatchState(null);
        }

        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        // ------------------------------------------------------------------ public API
        /// <summary>Fire a scan pulse from the viewer now (no cooldown): ring + sound + 5 s of focused labels.</summary>
        public virtual void Pulse()
        {
            PulseCount++;
            _focusLeft = FocusDuration;
            Focused = true;
            var v = Viewer;
            Vector3 origin = v != null ? v.Feet : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            int n = PerspectiveStyle.Count(Current);
            if (n == 0) Fx.ScanPulse(origin, UIColors.TextMuted, PulseRadius * 0.5f, PulseDuration);
            for (int i = 0; i < n && i < 2; i++)
                Fx.ScanPulse(origin, PerspectiveStyle.ColorOf(PerspectiveStyle.Nth(Current, i)), PulseRadius * (1f - 0.25f * i), PulseDuration * (1f - 0.12f * i));
            Sfx.Play(SfxId.ScanPulse, 0.9f, n > 1 ? 1.08f : 1f);
            Refresh(false);
        }

        /// <summary>R key path: pulse unless on cooldown. Returns true if it pulsed.</summary>
        public bool TryScan()
        {
            if (CooldownLeft > 0f) { Sfx.Play(SfxId.Denied, 0.35f, 1.3f); return false; }
            CooldownLeft = PulseCooldown;
            Pulse();
            return true;
        }

        /// <summary>Recompute the mask from the active character and apply it to every Revealable.</summary>
        public void Refresh(bool raiseIfChanged = true)
        {
            var active = CharacterManager.Instance != null ? CharacterManager.Instance.Active : null;
            if (active != null && !active.isActiveAndEnabled) active = null;
            var mask = active != null && active.IsAlive ? active.Perspectives : PerspectiveMask.None;
            bool changed = mask != Current || active != Viewer;
            Viewer = active;
            Current = mask;
            WatchState(active);
            ApplyMask();
            if (changed)
            {
                ApplyTint(true);
                if (raiseIfChanged) GameEvents.RaisePerspectiveChanged(Current, Viewer);
            }
        }

        public static bool Sees(PerspectiveMask category) => Instance != null && (Instance.Current & category) != 0;

        // ------------------------------------------------------------------ internals
        void OnSwitched(ShifterCharacter prev, ShifterCharacter next)
        {
            Refresh(true);
            Pulse();
        }

        void OnTeamSpawned()
        {
            TeammateWeightTag.EnsureAll();
            Refresh(true);
        }

        void WatchState(ShifterCharacter c)
        {
            if (_stateSub == c) return;
            if (_stateSub != null) _stateSub.StateChanged -= OnActiveStateChanged;
            _stateSub = c;
            if (_stateSub != null) _stateSub.StateChanged += OnActiveStateChanged;
        }

        void OnActiveStateChanged(ShifterCharacter c) => Refresh(true);

        void ApplyMask()
        {
            int revealed = 0;
            var all = Revealable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var r = all[i];
                if (r == null) { all.RemoveAt(i); continue; }
                bool on = (Current & r.Category) != 0;
                r.SetRevealed(on);
                if (on) revealed++;
            }
            RevealedCount = revealed;
        }

        void ApplyTint(bool force)
        {
            if (Current == PerspectiveMask.None) { if (force) PostFx.SetPerspectiveTint(Color.white, 0f); return; }
            if (!force && PerspectiveStyle.Count(Current) < 2) return;
            PostFx.SetPerspectiveTint(PerspectiveStyle.Tint(Current, Time.unscaledTime), TintWeight);
        }

        protected virtual void Update()
        {
            float dt = PerspectiveStyle.UiDelta;
            if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
            if (_focusLeft > 0f) { _focusLeft -= dt; Focused = _focusLeft > 0f; }
            else Focused = false;

            if (ShiftersInput.ScanPressed && Viewer != null) TryScan();

            _pollLeft -= dt;
            if (_pollLeft <= 0f)
            {
                _pollLeft = PollInterval;
                TeammateWeightTag.EnsureAll();
                Refresh(true); // weapon pickups/drops, new Revealables, active lost...
            }

            _tintLeft -= dt;
            if (_tintLeft <= 0f) { _tintLeft = 0.1f; ApplyTint(false); } // multi-perk viewers alternate colors
        }
    }
}
