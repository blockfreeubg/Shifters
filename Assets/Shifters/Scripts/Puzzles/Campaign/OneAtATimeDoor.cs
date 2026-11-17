// =====================================================================================
//  SHIFTERS - OneAtATimeDoor.cs   [PUZZLES - campaign wave, LEVELS2 CH04 STAFF DOOR]
//  Sensor + door: this SignalSource is ON while EXACTLY ONE Alive intern stands in its approach zone (local box
//  ZoneCenter +- ZoneSize/2, by default in front of the door on local -Z). The Door is wired All(inputs..., this),
//  so it opens only for a single intern (and closes again behind them). Two or more: "ONE (1) AT A TIME".
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class OneAtATimeDoor : SignalSource
    {
        public Door Door;
        public Vector3 ZoneCenter = new Vector3(0f, 1.5f, -1.7f);
        public Vector3 ZoneSize = new Vector3(3f, 3f, 3f);
        public float ReleaseDelay = 0.15f;
        public TextMeshPro Sign;
        public PuzzleLamp Lamp;

        /// <summary>Alive interns currently in the zone.</summary>
        public int Count { get; private set; }
        public int Crowded { get; private set; }

        float _off, _lastToast = -10f;
        int _shown = -1;

        bool InputsPowered()
        {
            if (Door == null || Door.Inputs == null) return true;
            foreach (var s in Door.Inputs) if (s != null && s != this && !s.IsOn) return false;
            return true;
        }

        void FixedUpdate()
        {
            var all = ShifterCharacter.All;
            var half = ZoneSize * 0.5f;
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.IsAlive && PuzzleUtil.CharacterInBox(transform, ZoneCenter, half, c)) n++;
            }
            if (n >= 2 && Count < 2 && InputsPowered())
            {
                Crowded++;
                Sfx.At(SfxId.Denied, transform.position + Vector3.up * 2f);
                if (PuzzleUtil.RateLimit(ref _lastToast, 3f)) GameEvents.RaiseToast("ONE (1) INTERN AT A TIME. THIS DOOR DOES NOT DO TEAMWORK.", UIColors.Danger);
            }
            Count = n;
            if (n == 1) { _off = 0f; SetOn(true); }
            else if (IsOn) { _off += Time.fixedDeltaTime; if (_off >= ReleaseDelay) SetOn(false); }
        }

        void Update()
        {
            int state = Count >= 2 ? 2 : IsOn ? (InputsPowered() ? 1 : 3) : 0;
            if (state == _shown) return;
            _shown = state;
            if (Lamp != null) Lamp.Set(state == 1 ? PuzzleLampColor.Green : state == 2 ? PuzzleLampColor.Red : PuzzleLampColor.Yellow);
            if (Sign != null)
            {
                Sign.text = state == 2 ? "ONE (1) AT A TIME!\n<size=60%>" + Count + " DETECTED</size>"
                          : state == 1 ? "ONE (1) AT A TIME\n<size=60%>WELCOME, SOLO EMPLOYEE</size>"
                          : state == 3 ? "ONE (1) AT A TIME\n<size=60%>ACCESS PENDING</size>"
                          : "ONE (1) AT A TIME\n<size=60%>STAND HERE ALONE</size>";
                Sign.color = state == 2 ? UIColors.Danger : state == 1 ? UIColors.Success : UIColors.AccentYellow;
            }
        }
    }
}
