// =====================================================================================
//  SHIFTERS - BotProbes.cs   [BOTS module - TEST DOUBLES]
//  Minimal stand-ins used ONLY by the bots' own mini course (BotMiniCourse) when the real
//  puzzle/level module is not published yet. They implement the same contracts the real parts
//  do (SignalSource, IInteractable, IShootable, GameEvents.LevelCompleted), so TeamBot exercises
//  identical code paths. Never placed in real levels.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Terminal double: IQ-gated hack taking Tuning.HackDuration(IQ) (Condition.Interacting + Motor.Hold).</summary>
    public class BotProbeTerminal : SignalSource, IInteractable
    {
        public int RequiredIQ = Tuning.TerminalIQ;
        ShifterCharacter _hacker;
        float _t, _duration;

        public int InteractPriority => 1;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.0f;
        public string GetPrompt(ShifterCharacter who) => "Hack terminal";

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (IsOn || _hacker != null || who == null || who.Stats == null) return false;
            if (who.Stats.IQ < RequiredIQ) { blockedReason = "Requires IQ " + RequiredIQ + " - " + who.DisplayName + " has " + who.Stats.IQ; return false; }
            return true;
        }

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) return;
            _hacker = who;
            _t = 0f;
            _duration = Tuning.HackDuration(who.Stats.IQ);
            who.SetCondition(CharacterCondition.Interacting);
            if (who.Motor != null) who.Motor.Hold(true);
        }

        void Update()
        {
            if (_hacker == null) return;
            _t += Time.deltaTime;
            GameEvents.RaiseProgress(_t / _duration, "HACKING...");
            if (_t < _duration) return;
            var h = _hacker;
            _hacker = null;
            if (h.Motor != null) h.Motor.Hold(false);
            h.SetCondition(CharacterCondition.Normal);
            GameEvents.RaiseProgress(-1f);
            SetOn(true);
        }
    }

    /// <summary>Shoot target double: latches on the first hit.</summary>
    public class BotProbeTarget : SignalSource, IShootable
    {
        public void OnShot(in ShotInfo shot) => SetOn(true);
    }

    /// <summary>Exit double: raises GameEvents.LevelCompleted once every Alive character is inside the box.</summary>
    public class BotProbeExit : MonoBehaviour
    {
        public Vector3 Size = new Vector3(7f, 3f, 6f);
        bool _done;

        void Update()
        {
            if (_done) return;
            var b = new Bounds(transform.position + Vector3.up * (Size.y * 0.5f), Size);
            int alive = 0, inside = 0;
            foreach (var c in ShifterCharacter.All)
            {
                if (c == null || !c.IsAlive) continue;
                alive++;
                if (b.Contains(c.Center)) inside++;
            }
            if (alive == 0 || inside < alive) return;
            _done = true;
            GameEvents.RaiseLevelCompleted(new LevelResult { LevelIndex = -1, Survivors = inside, LeftBehind = new string[0], Time = Time.timeSinceLevelLoad });
        }
    }
}
