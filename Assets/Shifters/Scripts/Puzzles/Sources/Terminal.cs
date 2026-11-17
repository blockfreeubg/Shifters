using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Hackable console (E). IQ &gt;= RequiredIQ: the character holds still (Condition.Interacting + Motor.Hold) for
    /// Tuning.HackDuration(IQ) with a HUD progress bar; moving, being switched away, damaged or knocked cancels.
    /// Success turns it on (latched, or for OnSeconds). Lower IQ: "ACCESS DENIED" + rotating joke lines.
    /// </summary>
    public class Terminal : SignalSource, IInteractable, IResettable
    {
        public int RequiredIQ = Tuning.TerminalIQ;
        public bool Latching = true;
        [Tooltip("When not latching: seconds to stay on after a successful hack.")] public float OnSeconds = 6f;

        [Header("Parts")]
        public TextMeshPro Screen;
        public PuzzleLamp Lamp;
        public Revealable Reveal;

        public static readonly string[] DeniedJokes =
        {
            "Password incorrect. Hint: it's not 'password'.",
            "ACCESS DENIED. Have you tried being smarter?",
            "Error 404: Brain not found.",
            "Please insert a larger brain.",
            "That's the keyboard. You're licking the keyboard.",
            "Nice try. The terminal is laughing at you.",
            "Incorrect. Also, the monitor was off.",
            "Authentication failed: excessive biceps detected."
        };

        public ShifterCharacter Hacker { get; private set; }
        public bool IsHacking => Hacker != null;
        public float Progress01 { get; private set; }
        public int DeniedCount { get; private set; }

        float _duration, _elapsed, _beep, _startHealth, _onRemaining, _screenFlash;
        Vector3 _hackStart;
        bool _wasActive;
        int _jokeIndex, _shownPct = -1;

        void Start()
        {
            if (Reveal != null) Reveal.DynamicLabel = v => v != null && v.Stats != null && v.Stats.IQ >= RequiredIQ
                ? "TERMINAL - HACK IT (" + Tuning.HackDuration(v.Stats.IQ).ToString("0.0") + " s)" : "TERMINAL - NEEDS IQ " + RequiredIQ;
            SetScreen(IsOn ? "ACCESS GRANTED" : "LOCKED\n<size=60%>IQ " + RequiredIQ + " REQUIRED</size>", IsOn ? UIColors.Success : UIColors.Info);
        }

        // ------------------------------------------------------------------ IInteractable
        public int InteractPriority => 2;
        public Vector3 InteractPoint => transform.TransformPoint(new Vector3(0f, 1.05f, 0.35f));

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null || IsHacking) return false;
            if (IsOn && Latching) return false;
            return true;
        }

        public string GetPrompt(ShifterCharacter who)
            => who != null && who.Stats != null && who.Stats.IQ >= RequiredIQ ? "Hack terminal" : "Hack terminal (IQ " + RequiredIQ + ")";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) return;
            if (who.Stats == null || who.Stats.IQ < RequiredIQ) Deny(who);
            else BeginHack(who);
        }

        // ------------------------------------------------------------------ hacking
        void BeginHack(ShifterCharacter who)
        {
            Hacker = who;
            _duration = Tuning.HackDuration(who.Stats.IQ);
            _elapsed = 0f;
            Progress01 = 0f;
            _hackStart = who.transform.position;
            _startHealth = who.Health;
            _wasActive = who.IsActive;
            who.SetCondition(CharacterCondition.Interacting);
            if (who.Motor != null) who.Motor.Hold(true);
            PuzzleUtil.Pose(who, PoseOverride.Hacking);
            Sfx.At(SfxId.Terminal, transform.position);
            GameEvents.RaiseProgress(0f, "HACKING...");
            _shownPct = -1;
        }

        void Update()
        {
            if (IsHacking) TickHack();
            else if (IsOn && !Latching)
            {
                _onRemaining -= Time.deltaTime;
                if (_onRemaining <= 0f) { SetOn(false); SetScreen("LOCKED\n<size=60%>IQ " + RequiredIQ + " REQUIRED</size>", UIColors.Info); }
            }
            if (_screenFlash > 0f) _screenFlash -= Time.deltaTime;
            if (Lamp != null)
            {
                if (IsHacking) Lamp.Blink(PuzzleLampColor.Blue, PuzzleLampColor.Off, 6f);
                else if (_screenFlash > 0f) Lamp.Blink(PuzzleLampColor.Red, PuzzleLampColor.Off, 8f);
                else Lamp.Set(IsOn);
            }
        }

        void TickHack()
        {
            var h = Hacker;
            if (ShouldCancel(h)) { EndHack(false); return; }
            _elapsed += Time.deltaTime;
            Progress01 = Mathf.Clamp01(_elapsed / _duration);
            GameEvents.RaiseProgress(Progress01, "HACKING...");
            _beep -= Time.deltaTime;
            if (_beep <= 0f) { _beep = 0.28f; Sfx.At(SfxId.Terminal, transform.position, 0.5f, Random.Range(0.9f, 1.4f)); }
            int pct = Mathf.FloorToInt(Progress01 * 10f) * 10;
            if (pct != _shownPct) { _shownPct = pct; SetScreen("HACKING...\n<size=70%>" + pct + "%</size>", UIColors.Intellect); }
            if (Progress01 >= 1f) EndHack(true);
        }

        bool ShouldCancel(ShifterCharacter h)
        {
            if (h == null || !h.IsAlive) return true;
            if (h.Condition != CharacterCondition.Interacting) return true;
            if (_wasActive && !h.IsActive) return true;
            if (h.Health < _startHealth - 0.5f) return true;
            var d = h.transform.position - _hackStart; d.y = 0f;
            if (d.sqrMagnitude > 0.5f * 0.5f) return true;
            if (_elapsed > 0.3f && h.Motor != null && h.Motor.CurrentCommand.Move.sqrMagnitude > 0.36f) return true;
            return false;
        }

        void EndHack(bool success)
        {
            var h = Hacker;
            Hacker = null;
            GameEvents.RaiseProgress(-1f);
            if (h != null)
            {
                if (h.Condition == CharacterCondition.Interacting) h.SetCondition(CharacterCondition.Normal);
                if (h.Motor != null) h.Motor.Hold(false);
                PuzzleUtil.Pose(h, PoseOverride.None);
            }
            if (success)
            {
                Progress01 = 1f;
                _onRemaining = OnSeconds;
                SetScreen("ACCESS\nGRANTED", UIColors.Success);
                Sfx.At(SfxId.HackSuccess, transform.position);
                Fx.Confetti(transform.position + Vector3.up * 1.5f);
                PuzzleUtil.Gesture(h, VisualAction.FistPump);
                if (h != null) h.Say("I'm in.");
                SetOn(true);
            }
            else
            {
                Progress01 = 0f;
                SetScreen("CONNECTION\nLOST", UIColors.Danger);
                _screenFlash = 1f;
                Sfx.At(SfxId.HackFail, transform.position, 0.6f);
            }
        }

        void Deny(ShifterCharacter who)
        {
            DeniedCount++;
            string joke = DeniedJokes[_jokeIndex % DeniedJokes.Length];
            _jokeIndex++;
            _screenFlash = 1.2f;
            SetScreen("ACCESS DENIED\n<size=45%>" + joke + "</size>", UIColors.Danger);
            Sfx.At(SfxId.HackFail, transform.position);
            Sfx.Voice(who, VoiceLine.Denied);
            PuzzleUtil.Gesture(who, VisualAction.Facepalm);
            GameEvents.RaiseToast(joke, UIColors.Intellect);
            Fx.FloatingText(transform.position + Vector3.up * 1.8f, "ACCESS DENIED", UIColors.Danger);
        }

        void SetScreen(string text, Color c)
        {
            if (Screen == null) return;
            Screen.text = text;
            Screen.color = c;
        }

        void OnDisable() { if (IsHacking) EndHack(false); }

        public void ResetState()
        {
            if (IsHacking) EndHack(false);
            SetOn(false);
            SetScreen("LOCKED\n<size=60%>IQ " + RequiredIQ + " REQUIRED</size>", UIColors.Info);
        }
    }
}
