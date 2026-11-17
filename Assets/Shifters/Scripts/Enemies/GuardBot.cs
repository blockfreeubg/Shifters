// =====================================================================================
//  SHIFTERS - GuardBot.cs   [owned by the ENEMIES agent]
//  Bank guard variant of the BONK-BOT (CH05/CH07/O1): cap, hologram floor vision cone that shows its real
//  SightRange/Fov (white patrol -> orange investigating -> red chasing; off while stunned/blind/frozen/
//  on break), and an optional COFFEE BREAK: from CoffeeBreakAt s after start, every CoffeeEvery s it stands
//  still for CoffeeSeconds seeing nothing ("COFFEE BREAK"). Captures go to its HoldingCell like any bot.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class GuardBot : MonoBehaviour
    {
        public SecurityBot Bot;
        public EnemyConeVisual Cone;
        public Transform Cap;
        [Tooltip("Seconds after start of the first coffee break (< 0 = never).")] public float CoffeeBreakAt = -1f;
        [Tooltip("Repeat period of the coffee break (<= 0 = only once).")] public float CoffeeEvery = 20f;
        public float CoffeeSeconds = 6f;

        public bool OnCoffeeBreak => Bot != null && Bot.OnBreak;
        float _t, _manual;
        bool _was;

        /// <summary>Send the guard on a break now for seconds (levels: a distraction).</summary>
        public void StartCoffeeBreak(float seconds) { _manual = Mathf.Max(_manual, seconds); }

        /// <summary>Seconds until the next scheduled break starts (0 = on break; infinity = never).</summary>
        public float SecondsUntilBreak()
        {
            if (OnCoffeeBreak) return 0f;
            if (CoffeeBreakAt < 0f) return float.PositiveInfinity;
            float ph = _t - CoffeeBreakAt;
            if (ph < 0f) return -ph;
            if (CoffeeEvery <= 0f) return float.PositiveInfinity;
            return CoffeeEvery - Mathf.Repeat(ph, CoffeeEvery);
        }

        void Update()
        {
            if (Bot == null) return;
            float dt = Time.deltaTime;
            _t += dt;
            if (_manual > 0f) _manual -= dt;
            bool sched = false;
            if (CoffeeBreakAt >= 0f)
            {
                float ph = _t - CoffeeBreakAt;
                if (ph >= 0f) sched = CoffeeEvery > 0f ? Mathf.Repeat(ph, CoffeeEvery) < CoffeeSeconds : ph < CoffeeSeconds;
            }
            bool calm = Bot.State == BotState.Patrol || Bot.State == BotState.Return;
            bool brk = (sched || _manual > 0f) && calm;
            Bot.OnBreak = brk;
            if (brk != _was)
            {
                _was = brk;
                Vector3 at = Bot.EyePosition + Vector3.up * 1.0f;
                if (brk) { Fx.FloatingText(at, "COFFEE BREAK", UIColors.AccentYellow, 1.3f); Sfx.At(SfxId.Gurgle, at, 0.6f); }
                else Fx.FloatingText(at, "BREAK'S OVER", UIColors.TextMuted, 1f);
            }
            if (Cone == null) return;
            bool off = Bot.IsStunned || Bot.IsBlind || Bot.IsFrozen || brk || Bot.IsTiny;
            Cone.SetVisible(!off);
            if (off) return;
            Color c;
            switch (Bot.State)
            {
                case BotState.Chase:
                case BotState.Capture: c = EnemyConeVisual.Alert; break;
                case BotState.Investigate: c = EnemyConeVisual.Spotting; break;
                default: c = EnemyConeVisual.Idle; break;
            }
            Cone.SetColor(c);
        }
    }
}
