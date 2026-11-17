// =====================================================================================
//  SHIFTERS - CharacterAudio.cs   [owned by the AUDIO agent]
//  Per-character sound hooks: subscribes to CharacterMotor events (footsteps, jump, land,
//  tumble, get-up), state changes (KO, lost, captured, revive), condition changes (thrown
//  "wheee", bridge strain), damage ("oof"), speech bubbles (gibberish chatter), becoming the
//  active character (Selected voice), level complete ("yahoo!") and teammates' misfortune
//  (an occasional laugh). Voices follow the character while they play.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class CharacterAudio : MonoBehaviour, ICharacterModule
    {
        protected ShifterCharacter character;

        static float s_lastLaugh = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_lastLaugh = -99f;

        CharacterMotor _motor;
        bool _subscribed;
        CharacterState _lastState = CharacterState.Alive;
        float _lastJumpVoice = -99f, _lastHurt = -99f, _lastSpeech = -99f, _lastScream = -99f;
        float _selectedAt = -1f, _selectedDeadline = -1f, _victoryAt = -1f, _laughAt = -1f, _greetAt = -1f;
        bool _transitioning;
        AudioRng _rng = new AudioRng(99);

        /// <summary>Number of sounds/voices this component requested (tests).</summary>
        public int Requests { get; private set; }

        public virtual void OnCharacterInitialized(ShifterCharacter c)
        {
            character = c;
            _lastState = c != null ? c.State : CharacterState.Alive;
            _rng = new AudioRng(c != null ? c.Slot * 7919 + 17 : 5);
            if (isActiveAndEnabled) Subscribe();
            var am = AudioManager.Instance;
            if (am != null && am.Voices != null && c != null && c.Profile != null) am.Voices.Prewarm(c.Profile);
        }

        void OnEnable() { if (character != null) Subscribe(); }
        void OnDisable() => Unsubscribe();

        void Subscribe()
        {
            if (_subscribed || character == null) return;
            _motor = character.Motor;
            if (_motor != null)
            {
                _motor.Footstep += OnFootstep;
                _motor.Jumped += OnJumped;
                _motor.Landed += OnLanded;
                _motor.Tumbled += OnTumbled;
                _motor.GotUp += OnGotUp;
            }
            character.StateChanged += OnStateChanged;
            character.ConditionChanged += OnConditionChanged;
            GameEvents.Speech += OnSpeech;
            GameEvents.CharacterSwitched += OnSwitched;
            GameEvents.SwitchTransitionChanged += OnTransition;
            GameEvents.CharacterDamaged += OnDamaged;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.CharacterStateChanged += OnAnyStateChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_motor != null)
            {
                _motor.Footstep -= OnFootstep;
                _motor.Jumped -= OnJumped;
                _motor.Landed -= OnLanded;
                _motor.Tumbled -= OnTumbled;
                _motor.GotUp -= OnGotUp;
            }
            if (character != null)
            {
                character.StateChanged -= OnStateChanged;
                character.ConditionChanged -= OnConditionChanged;
            }
            GameEvents.Speech -= OnSpeech;
            GameEvents.CharacterSwitched -= OnSwitched;
            GameEvents.SwitchTransitionChanged -= OnTransition;
            GameEvents.CharacterDamaged -= OnDamaged;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.CharacterStateChanged -= OnAnyStateChanged;
            _subscribed = false;
        }

        // ------------------------------------------------------------------ helpers
        float Now => Time.unscaledTime;
        bool Heavy => character.Archetype == Archetype.Brute || character.Mass >= 95f;
        float ArchetypePitch => character.Archetype == Archetype.Sprinter ? 1.12f : character.Archetype == Archetype.Brainiac ? 1.04f : 0.94f;
        Vector3 MouthOffset => Vector3.up * (character.Height * 0.85f);

        void Voice(VoiceLine line, float volume = 1f)
        {
            var am = AudioManager.Instance;
            if (am == null || character == null || character.Profile == null) return;
            am.PlayVoiceAt(character.Profile, line, character.transform.position + MouthOffset, character.transform, MouthOffset, volume);
            Requests++;
        }

        void At(SfxId id, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            Sfx.At(id, pos, vol, pitch);
            Requests++;
        }

        // ------------------------------------------------------------------ motor
        void OnFootstep(int foot)
        {
            if (character == null || !character.IsAlive) return;
            float speed = _motor != null ? _motor.Speed01 : 0.5f;
            float vol = Mathf.Lerp(0.55f, 1f, speed) * (character.IsActive ? 1f : 0.8f);
            At(Heavy ? SfxId.FootstepHeavy : SfxId.Footstep, character.Feet, vol, ArchetypePitch * (foot == 0 ? 1f : 1.03f));
        }

        void OnJumped()
        {
            if (character == null) return;
            At(SfxId.Jump, character.Feet, 0.8f, ArchetypePitch);
            if (Now - _lastJumpVoice > 2.5f && _rng.Chance(0.3f)) { _lastJumpVoice = Now; Voice(VoiceLine.Jump, 0.8f); }
        }

        void OnLanded(float impact)
        {
            if (character == null || impact < 2.5f) return;
            bool heavyHit = impact >= 11f || (Heavy && impact >= 7f);
            if (heavyHit) At(SfxId.LandHeavy, character.Feet, Mathf.Clamp(impact / 16f, 0.5f, 1.2f), ArchetypePitch);
            else At(SfxId.Land, character.Feet, Mathf.Lerp(0.35f, 1f, (impact - 2.5f) / 8f), ArchetypePitch);
        }

        void OnTumbled()
        {
            if (character == null) return;
            var cond = character.Condition;
            if (cond == CharacterCondition.Flopped) return;                    // CharacterFlop plays its own flop sound
            At(SfxId.Tumble, character.Center, 0.9f);
            if (cond == CharacterCondition.Thrown) return;                     // "wheee" comes from the condition change
            if (character.IsAlive && Now - _lastScream > 3f && _rng.Chance(0.5f)) { _lastScream = Now; Voice(VoiceLine.Scream, 0.9f); }
        }

        void OnGotUp()
        {
            if (character != null && character.IsAlive) At(SfxId.GetUp, character.Center, 0.6f, ArchetypePitch);
        }

        // ------------------------------------------------------------------ state / condition
        void OnStateChanged(ShifterCharacter c)
        {
            if (c != character) return;
            var prev = _lastState;
            var s = c.State;
            _lastState = s;
            if (s == prev) return;
            switch (s)
            {
                case CharacterState.Downed:
                    At(SfxId.KO, c.Center);
                    Voice(VoiceLine.KO);
                    break;
                case CharacterState.Lost:
                    {
                        var am = AudioManager.Instance;
                        if (am != null) { am.PlayAttached(SfxId.Lost, c.transform, 1f, 1f, Vector3.up * (c.Height * 0.5f)); Requests++; }
                        Voice(VoiceLine.Lost);
                        break;
                    }
                case CharacterState.Captured:
                    At(SfxId.Captured, c.Center);
                    Voice(VoiceLine.Denied);
                    break;
                case CharacterState.Alive:
                    if (prev == CharacterState.Downed || prev == CharacterState.Captured)
                    {
                        At(SfxId.Revive, c.Center);
                        _greetAt = Now + 0.35f;
                    }
                    break;
            }
        }

        void OnConditionChanged(ShifterCharacter c)
        {
            if (c != character || !c.IsAlive) return;
            if (c.Condition == CharacterCondition.Thrown) Voice(VoiceLine.Thrown);
            else if (c.Condition == CharacterCondition.Bridging) Voice(VoiceLine.Strain);
        }

        void OnDamaged(ShifterCharacter c, float amount)
        {
            if (c != character || amount < 5f || Now - _lastHurt < 0.6f) return;
            _lastHurt = Now;
            At(SfxId.Hurt, c.Center, Mathf.Clamp(amount / 40f, 0.4f, 1f));
            if (c.IsAlive && c.Health > 0f) Voice(VoiceLine.Hurt);
        }

        void OnAnyStateChanged(ShifterCharacter other)
        {
            if (other == null || other == character || character == null || !character.IsAlive) return;
            if (other.State != CharacterState.Downed && other.State != CharacterState.Lost) return;
            if (Now - s_lastLaugh < 4f || !_rng.Chance(0.35f)) return;
            s_lastLaugh = Now;              // only one teammate laughs
            _laughAt = Now + 0.8f;
        }

        // ------------------------------------------------------------------ speech / switching / level
        void OnSpeech(ShifterCharacter c, string text)
        {
            if (c != character || character.Profile == null) return;
            var am = AudioManager.Instance;
            if (am == null) return;
            _lastSpeech = Now;
            _selectedAt = -1f;
            am.PlayChatter(character.Profile, text, character.transform.position + MouthOffset, character.transform, MouthOffset);
            Requests++;
        }

        void OnSwitched(ShifterCharacter prev, ShifterCharacter next)
        {
            // The "Selected" line is only a fallback: after a camera transition CharacterManager says a quip on
            // arrival (GameEvents.Speech -> chatter), which cancels it. Instant switches (no quip) get the line.
            if (next == character && character != null && character.IsAlive) { _selectedAt = Now + 0.12f; _selectedDeadline = Now + 2.5f; }
        }

        void OnTransition(bool running) => _transitioning = running;

        void OnLevelCompleted(LevelResult r)
        {
            if (character != null && character.IsAlive) _victoryAt = Now + 0.5f + Mathf.Max(0, character.Slot) * 0.3f;
        }

        void Update()
        {
            if (character == null) return;
            float now = Now;
            if (_selectedAt > 0f && now >= _selectedAt)
            {
                if (_transitioning && now < _selectedDeadline) _selectedAt = now + 0.05f;   // wait for the arrival quip
                else
                {
                    _selectedAt = -1f;
                    if (now - _lastSpeech > 0.6f && character.IsAlive) Voice(VoiceLine.Selected);
                }
            }
            if (_greetAt > 0f && now >= _greetAt) { _greetAt = -1f; if (character.IsAlive) Voice(VoiceLine.Greeting); }
            if (_victoryAt > 0f && now >= _victoryAt) { _victoryAt = -1f; if (character.IsAlive) Voice(VoiceLine.Victory); }
            if (_laughAt > 0f && now >= _laughAt) { _laughAt = -1f; if (character.IsAlive) Voice(VoiceLine.Laugh, 0.9f); }
        }
    }
}
