using UnityEngine;

namespace Shifters
{
    public enum PlatformLoop { PingPong, Loop }

    /// <summary>
    /// Kinematic platform moving along Points (local to this transform; Points[0] is usually zero) with MovePosition in
    /// FixedUpdate. Moves only while powered (no inputs + PoweredWithoutInputs = always). Waits WaitTime at each point.
    /// Riders inherit its motion through the motor's GroundBody (kinematic velocity) - see MoverBody.
    /// </summary>
    public class MovingPlatform : SignalReceiver, IResettable
    {
        public Vector3[] Points = { Vector3.zero, new Vector3(0f, 0f, 6f) };
        public float Speed = 2.5f;
        public float WaitTime = 1f;
        public PlatformLoop Loop = PlatformLoop.PingPong;

        [Header("Parts")]
        public MoverBody Body;
        public PuzzleLamp Lamp;

        public int TargetIndex => _to;
        public bool Waiting => _wait > 0f;

        Vector3 _local;
        int _to = 1, _dir = 1;
        float _wait;
        bool _init;

        void Init()
        {
            if (_init) return;
            _init = true;
            _local = Points != null && Points.Length > 0 ? Points[0] : Vector3.zero;
            _to = Points != null && Points.Length > 1 ? 1 : 0;
        }

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (Lamp != null) Lamp.Set(powered);
            if (!initial && powered) Sfx.At(SfxId.ElevatorHum, transform.position, 0.6f, 1.1f);
        }

        void FixedUpdate()
        {
            if (Body == null) return;
            Init();
            if (Body.IsFrozen) return;   // CAPS-LOCK: pause the route where it is
            float dt = Time.fixedDeltaTime;
            if (Powered && Points != null && Points.Length > 1)
            {
                if (_wait > 0f) _wait -= dt;
                else
                {
                    var target = Points[_to];
                    _local = Vector3.MoveTowards(_local, target, Speed * dt);
                    if ((_local - target).sqrMagnitude < 1e-6f) Advance();
                }
            }
            Body.MoveTo(transform.TransformPoint(_local));
        }

        void Advance()
        {
            _wait = WaitTime;
            int n = Points.Length;
            if (Loop == PlatformLoop.Loop) _to = (_to + 1) % n;
            else
            {
                if (_to + _dir >= n || _to + _dir < 0) _dir = -_dir;
                _to += _dir;
            }
        }

        public void ResetState()
        {
            _init = false;
            Init();
            _wait = 0f;
            _dir = 1;
            if (Body != null) Body.Snap(transform.TransformPoint(_local));
        }
    }
}
