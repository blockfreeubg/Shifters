// =====================================================================================
//  SHIFTERS - WindowGate.cs   [PUZZLES - campaign wave, LEVELS2 G2]
//  Run-through gate of a timed route: ON from the moment an Alive intern enters its box while the TimedButton
//  window is running, until that window ends. Wire a payoff as Latch(AND(button, gate A, gate B ...)) so only a
//  runner who passes every gate in time wins. Frame lamps: red idle, yellow while the window runs, green once
//  passed. Box = local (0, Size.y/2, 0) +- Size/2 (pos = bottom center, passage along local Z).
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class WindowGate : SignalSource, IResettable
    {
        public TimedButton Window;
        public Vector3 Size = new Vector3(3f, 3f, 1f);
        public PuzzleLamp Lamp;
        public TextMeshPro[] Labels = new TextMeshPro[0];

        /// <summary>Who passed during the current window (null otherwise).</summary>
        public ShifterCharacter Passer { get; private set; }
        public int Passes { get; private set; }
        public bool WindowRunning => Window != null && Window.IsOn;

        int _shown = -1;

        void FixedUpdate()
        {
            if (!WindowRunning)
            {
                if (IsOn) SetOn(false);
                Passer = null;
                return;
            }
            if (IsOn) return;
            var all = ShifterCharacter.All;
            var center = new Vector3(0f, Size.y * 0.5f, 0f);
            var half = Size * 0.5f;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || !PuzzleUtil.CharacterInBox(transform, center, half, c)) continue;
                Passer = c;
                Passes++;
                Sfx.At(SfxId.PlatePress, transform.position + Vector3.up * Size.y, 0.8f, 1.5f);
                Fx.Sparks(transform.position + Vector3.up * Size.y, Vector3.down, 10, UIColors.Success);
                SetOn(true);
                break;
            }
        }

        void Update()
        {
            int state = IsOn ? 2 : WindowRunning ? 1 : 0;
            if (Lamp != null)
            {
                if (state == 1) Lamp.Blink(PuzzleLampColor.Yellow, PuzzleLampColor.Off, 6f);
                else if (state != _shown) Lamp.Set(state == 2 ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            }
            if (state == _shown) return;
            _shown = state;
            string txt = state == 2 ? "PASSED!" : state == 1 ? "GO GO GO" : "WINDOW GATE";
            Color col = state == 2 ? UIColors.Success : state == 1 ? UIColors.AccentYellow : UIColors.Text;
            if (Labels != null) foreach (var l in Labels) if (l != null) { l.text = txt; l.color = col; }
        }

        public void ResetState() { Passer = null; SetOn(false); _shown = -1; }
    }
}
