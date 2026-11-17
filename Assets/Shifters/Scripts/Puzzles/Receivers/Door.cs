using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public enum DoorMode { SlideUp, SlideSide }

    /// <summary>
    /// Heavy test-chamber door: dark steel panel with yellow chevrons, status light strip (red closed / green open).
    /// Opens when powered (slides up, or sideways along local +X). Never closes on characters or props: it pauses,
    /// backs off a little and retries. Wired to a TimedButton it shows a countdown ring of lamps on its header.
    /// </summary>
    public class Door : SlideMover
    {
        public DoorMode Mode = DoorMode.SlideUp;
        public float Width = 3f;
        public float Height = 4.2f;
        public float PanelThickness = 0.3f;

        [Header("Parts")]
        public PuzzleLamp Strip;
        public PuzzleLamp[] RingLamps = new PuzzleLamp[0];
        [Tooltip("Set automatically by PuzzleKit.Wire when an input is a TimedButton.")] public TimedButton Timer;

        public bool IsOpen => Position01 >= 0.999f;
        public bool IsClosed => Position01 <= 0.001f;
        public int BlockedCount { get; private set; }

        readonly List<Rigidbody> _overlap = new List<Rigidbody>();
        float _retryAt;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            base.OnPowerChanged(powered, initial);
            if (Strip != null) Strip.Set(powered);
            if (!initial) Sfx.At(powered ? SfxId.DoorOpen : SfxId.DoorClose, transform.position + Vector3.up * Height * 0.5f);
        }

        protected override bool AllowMove(float dir)
        {
            if (dir > 0f) return true;
            if (Time.time < _retryAt) return false;
            if (!Blocked()) return true;
            BlockedCount++;
            _retryAt = Time.time + 0.6f;
            Position01 = Mathf.Min(1f, Position01 + 0.08f);
            Sfx.At(SfxId.Bonk, transform.position + Vector3.up, 0.6f, 0.8f);
            return false;
        }

        bool Blocked()
        {
            float travel = Travel * PuzzleUtil.Ease(Position01);
            Vector3 c, half;
            if (Mode == DoorMode.SlideUp)
            {
                float bottom = travel;
                float lo = Mathf.Max(0.02f, bottom - 0.5f), hi = Mathf.Max(lo + 0.1f, bottom + 0.05f);
                c = new Vector3(0f, (lo + hi) * 0.5f, 0f);
                half = new Vector3(Width * 0.5f - 0.05f, (hi - lo) * 0.5f, PanelThickness * 0.5f + 0.15f);
            }
            else
            {
                float edge = -Width * 0.5f + travel;
                float lo = Mathf.Max(-Width * 0.5f, edge - 0.5f), hi = Mathf.Min(Width * 0.5f, edge + 0.05f);
                if (hi <= lo) return false;
                c = new Vector3((lo + hi) * 0.5f, Height * 0.5f, 0f);
                half = new Vector3((hi - lo) * 0.5f, Height * 0.5f - 0.05f, PanelThickness * 0.5f + 0.15f);
            }
            PuzzleUtil.OverlapBodies(transform.TransformPoint(c), half, transform.rotation, _overlap, Body != null ? Body.Body : null);
            return _overlap.Count > 0;
        }

        protected override void OnArrived(bool open)
        {
            if (!open)
            {
                Sfx.At(SfxId.DoorSlam, transform.position + Vector3.up * 0.3f);
                Fx.Puff(transform.position + Vector3.up * 0.1f, 1.2f);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (RingLamps.Length == 0) return;
            bool running = Timer != null && Timer.IsOn;
            float r = running ? Timer.Remaining01 : 0f;
            int lit = running ? Mathf.CeilToInt(r * RingLamps.Length) : 0;
            var col = r > 0.35f ? PuzzleLampColor.Yellow : PuzzleLampColor.Red;
            for (int i = 0; i < RingLamps.Length; i++)
                if (RingLamps[i] != null) RingLamps[i].Set(i < lit ? col : PuzzleLampColor.Off);
        }
    }
}
