// =====================================================================================
//  SHIFTERS - L6ReviewScale.cs   [CH03 - P3 review scale on the press stamp]
//  A WeightSensor on the stamp's collider. While Armed (the stamp rests on pad S as the REVIEW PLATFORM)
//  a load >= RequiredKg held for HoldSeconds latches it ON ("OVERLOAD"). Carried teammates count
//  (WeightBody effective mass). Any two interns weigh <= 195 kg; all three 220-260 kg.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L6ReviewScale : SignalSource, IResettable
    {
        public WeightSensor Sensor;
        public float RequiredKg = 210f, HoldSeconds = 2f;
        public bool Armed;
        public float CurrentKg { get; private set; }
        public int CountOn { get; private set; }
        public float HeldFor { get; private set; }

        void FixedUpdate()
        {
            if (IsOn) return;
            if (!Armed || Sensor == null) { CurrentKg = 0f; CountOn = 0; HeldFor = 0f; return; }
            CurrentKg = Sensor.Measure();
            int n = 0;
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++) if (team[i] != null && Sensor.Contains(team[i])) n++;
            CountOn = n;
            if (CurrentKg >= RequiredKg) HeldFor += Time.fixedDeltaTime; else HeldFor = 0f;
            if (HeldFor >= HoldSeconds) SetOn(true);
        }

        public void ResetState() { if (IsOn) SetOn(false); HeldFor = 0f; Armed = false; }
    }
}
