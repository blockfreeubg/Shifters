using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Base receiver that slides a kinematic MoverBody between a home position (unpowered) and
    /// home + Axis * Travel (powered), with eased motion. Used by Door, Elevator and RetractableBridge.
    /// </summary>
    public abstract class SlideMover : SignalReceiver, IResettable
    {
        public MoverBody Body;
        [Tooltip("Local direction of travel when powered.")] public Vector3 Axis = Vector3.up;
        public float Travel = 4f;
        [Tooltip("m/s toward the powered end.")] public float SpeedOn = 2.5f;
        [Tooltip("m/s back home.")] public float SpeedOff = 2.5f;
        [Tooltip("Body position (local to this transform) when unpowered.")] public Vector3 HomeLocal;
        public PuzzleLamp Lamp;

        /// <summary>0 = home, 1 = fully extended/open (linear progress; motion is eased).</summary>
        public float Position01 { get; protected set; }
        public bool Moving { get; private set; }
        protected float Target01;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            Target01 = powered ? 1f : 0f;
            if (Lamp != null) Lamp.Set(powered);
            if (initial)
            {
                Position01 = Target01;
                if (Body != null) Body.Snap(WorldAt(Position01));
            }
            else OnStartMoving(powered);
        }

        protected virtual void FixedUpdate()
        {
            if (Body == null) return;
            bool wasMoving = Moving;
            Moving = false;
            if (!Mathf.Approximately(Position01, Target01))
            {
                float dir = Mathf.Sign(Target01 - Position01);
                if (AllowMove(dir))
                {
                    float speed = (dir > 0f ? SpeedOn : SpeedOff) / Mathf.Max(0.01f, Travel);
                    Position01 = Mathf.MoveTowards(Position01, Target01, speed * Time.fixedDeltaTime);
                    Moving = true;
                    if (Mathf.Approximately(Position01, Target01)) { Position01 = Target01; Moving = false; OnArrived(Target01 > 0.5f); }
                }
            }
            Body.MoveTo(WorldAt(Position01));
            if (wasMoving != Moving) OnMovingChanged(Moving);
        }

        public Vector3 WorldAt(float t01) => transform.TransformPoint(HomeLocal + Axis.normalized * (Travel * PuzzleUtil.Ease(t01)));

        /// <summary>Return false to pause movement this step (e.g. a door blocked by a character).</summary>
        protected virtual bool AllowMove(float dir) => true;
        protected virtual void OnStartMoving(bool towardPowered) { }
        protected virtual void OnArrived(bool atPoweredEnd) { }
        protected virtual void OnMovingChanged(bool moving) { }

        public virtual void ResetState()
        {
            Position01 = Powered ? 1f : 0f;
            Target01 = Position01;
            if (Body != null) Body.Snap(WorldAt(Position01));
        }
    }
}
