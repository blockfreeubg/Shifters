// =====================================================================================
//  SHIFTERS - L13BumpRun.cs   [LEVEL 13 - P2 BUMP RUN]
//  BUMPS = Latch(AND(window, G2, G3)), PRESSER-ONLY: a gate counts only the character who pressed the
//  TimedButton, crossing its plane (x = G2X / G3X, z in ZMin..ZMax) eastward while the window runs.
//  (Stops the SHIFT-relay and pre-standing cheeses; the NO-SWAP band stops swap relays.)
//  ON = bumps raised (0.3 s). Lower() at P3 start. The presser = L13BumpButton.Presser (recorded in
//  OnPressed(who)); only a script press without a character falls back to the closest alive intern.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L13BumpRun : SignalSource, IResettable
    {
        public TimedButton Button;
        public Transform[] Bumps = new Transform[0];
        public float G2X = 0f, G3X = 13f, ZMin = 13.5f, ZMax = 17.5f;
        public float UpY = 0.25f, DownY = -0.26f, RiseSeconds = 0.3f;

        public ShifterCharacter Presser { get; private set; }
        public bool PassedG2 { get; private set; }
        public bool PassedG3 { get; private set; }
        public int Presses { get; private set; }
        public int LateRuns { get; private set; }

        bool _wasOn;
        float _prevX, _lift;
        int _pressCount;

        void Update()
        {
            bool on = Button != null && Button.IsOn;
            if (on && !_wasOn && !IsOn)
            {
                Presses++;
                var bb = Button as L13BumpButton;
                Presser = bb != null && bb.Presser != null ? bb.Presser : NearestTo(Button.transform.position, 3.5f);
                _pressCount = bb != null ? bb.PressCount : 0;
                PassedG2 = PassedG3 = false;
                _prevX = Presser != null ? Presser.Feet.x : -99f;
            }
            if (on && !IsOn && Button is L13BumpButton nb && nb.PressCount != _pressCount && nb.Presser != null)
            {   // re-pressed while running: the window restarted, the new presser owns the run
                _pressCount = nb.PressCount; Presser = nb.Presser; PassedG2 = PassedG3 = false; _prevX = Presser.Feet.x;
            }
            if (on && Presser != null && !IsOn)
            {
                var f = Presser.Feet;
                bool inBand = f.z >= ZMin && f.z <= ZMax;
                if (inBand && !PassedG2 && _prevX < G2X && f.x >= G2X) PassedG2 = true;
                if (inBand && PassedG2 && !PassedG3 && _prevX < G3X && f.x >= G3X)
                {
                    PassedG3 = true;
                    SetOn(true);
                    GameEvents.RaiseStoryBeat("ch13.bumps");
                    GameEvents.RaiseToast("SPEED BUMPS DEPLOYED. PLEASE SKATE RESPONSIBLY.", UIColors.AccentYellow);
                    Sfx.At(SfxId.PuzzleSolved, transform.position, 1f);
                    for (int i = 0; i < Bumps.Length; i++) if (Bumps[i] != null) Fx.Puff(Bumps[i].position + Vector3.up * 0.4f, 1.2f, UIColors.AccentYellow);
                }
                _prevX = f.x;
            }
            if (!on && _wasOn && !IsOn && Presser != null)
            {
                LateRuns++;
                GameEvents.RaiseToast("BUMPS: TOO SLOW. TRY THE FAST ONE.", UIColors.Danger);
                Presser = null;
            }
            _wasOn = on;
            float target = IsOn ? 1f : 0f;
            _lift = Mathf.MoveTowards(_lift, target, Time.deltaTime / Mathf.Max(0.05f, RiseSeconds));
            for (int i = 0; i < Bumps.Length; i++)
            {
                var b = Bumps[i];
                if (b == null) continue;
                var p = b.position; float y = Mathf.Lerp(DownY, UpY, _lift);
                if (Mathf.Abs(p.y - y) > 1e-4f) { p.y = y; var rb = b.GetComponent<Rigidbody>(); if (rb != null) rb.MovePosition(p); else b.position = p; }
            }
        }

        static ShifterCharacter NearestTo(Vector3 p, float radius)
        {
            ShifterCharacter best = null; float bd = radius;
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive) continue;
                float d = BossFootprint.Planar(c.Feet, p) - (c.IsActive ? 0.5f : 0f);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Bumps down, window/gates reset (P3 start, checkpoint restore).</summary>
        public void Lower() { SetOn(false); Presser = null; PassedG2 = PassedG3 = false; _wasOn = Button != null && Button.IsOn; }

        public void ResetState() { Lower(); if (Button != null) Button.ResetState(); }
    }
}
