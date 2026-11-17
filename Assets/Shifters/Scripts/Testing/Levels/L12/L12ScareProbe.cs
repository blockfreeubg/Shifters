// SHIFTERS - L12ScareProbe.cs   [SHIFT 11 test helper] samples the team at 10 Hz: longest continuous SCARED time, captures.
using UnityEngine;

namespace Shifters
{
    public sealed class L12ScareProbe : MonoBehaviour
    {
        public L12BoogeymanBoss Boss;
        public float MaxScaredFor { get; private set; }
        public string MaxScaredWho { get; private set; } = "";
        public int CapturedSamples { get; private set; }
        public bool TargetEverInBreakArea { get; private set; }
        float _t;

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < 0.1f) return;
            _t = 0f;
            if (Boss == null) return;
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null) continue;
                float s = Boss.ScaredFor(c);
                if (s > MaxScaredFor) { MaxScaredFor = s; MaxScaredWho = c.DisplayName + "@" + c.Feet.ToString("F1"); }
                if (c.State == CharacterState.Captured) CapturedSamples++;
            }
            var tg = Boss.Target;
            if (tg != null && Boss.Arena != null && Boss.Arena.InSafeZone(tg.Feet)) TargetEverInBreakArea = true;
        }
    }
}
