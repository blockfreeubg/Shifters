using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Timed door button: ON for Tuning.TimedWindow(RouteLength, fastest Alive sprint speed). While running it drives
    /// the HUD countdown (GameEvents.RaiseTimer), accelerating ticks, and any Door ring wired to it.
    /// Speed perspective shows "TURBO RUN: 3.4 s". Routes shorter than Tuning.TimedRouteMin can't discriminate turbo.
    /// </summary>
    public class TimedButton : PushButton
    {
        [Tooltip("Length of the run (m) from this button to the door.")] public float RouteLength = 24f;
        [Tooltip("Optional: also drive the HUD countdown (disable for decorative timers).")] public bool ShowHudTimer = true;

        float _tick;

        /// <summary>Current window in seconds for the team's fastest sprinter.</summary>
        public float Window
        {
            get
            {
                var cm = CharacterManager.Instance;
                float fastest = cm != null ? cm.FastestSprintSpeed : 7f;
                return Tuning.TimedWindow(RouteLength, fastest);
            }
        }

        void Start() { if (Reveal != null) Reveal.DynamicLabel = v => "TURBO RUN: " + Window.ToString("0.0") + " s\n" + Mathf.RoundToInt(RouteLength) + " M"; }

        public override string GetPrompt(ShifterCharacter who) => "Start timer (" + Window.ToString("0.0") + " s)";

        protected override float WindowSeconds() => Window;

        protected override void OnPressed(ShifterCharacter who)
        {
            _tick = 0f;
            Sfx.At(SfxId.TimerTick, transform.position, 1f, 1.2f);
            if (ShowHudTimer) GameEvents.RaiseTimer(Remaining);
            if (who != null) who.Say("GO GO GO!");
        }

        protected override void OnRunning(float remaining)
        {
            if (ShowHudTimer) GameEvents.RaiseTimer(remaining);
            float r01 = Remaining01;
            _tick -= Time.deltaTime;
            if (_tick <= 0f)
            {
                _tick = Mathf.Lerp(0.12f, 0.8f, r01);
                Sfx.Play(SfxId.TimerTick, 0.7f, Mathf.Lerp(1.5f, 1f, r01));
            }
        }

        protected override void OnExpired()
        {
            if (ShowHudTimer) GameEvents.RaiseTimer(0f);
            Sfx.Play(SfxId.TimerEnd);
            Sfx.At(SfxId.ButtonPress, transform.position, 0.7f, 0.75f);
        }

        public override void ResetState()
        {
            bool wasOn = IsOn;
            base.ResetState();
            if (wasOn && ShowHudTimer) GameEvents.RaiseTimer(0f);
        }

        void OnDisable() { if (IsOn && ShowHudTimer) GameEvents.RaiseTimer(0f); }
    }
}
