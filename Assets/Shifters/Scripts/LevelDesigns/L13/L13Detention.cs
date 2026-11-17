// =====================================================================================
//  SHIFTERS - L13Detention.cs   [LEVEL 13 - P4 DETENTION (VIP)]   SignalSource ON = DETAINED (latched).
//  Door open = OR(INTAKE terminal ON (non-latching, 8 s), inside button ON (3 s)); after the capture the
//  door also closes as soon as no intern is inside the alcove (nobody is ever locked in with Max).
//  DETAINED latches when the SOLO POD rests DROPPED in the DETAIN ZONE (dizzy, not carried, not arguing,
//  speed < 0.3 m/s for 0.5 s) -> pod.Capture(), the body slides 0.4 s to the seat, the VIP DOME drops 0.6 s.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L13Detention : SignalSource, IResettable
    {
        public Terminal Intake;
        public PushButton InsideButton;
        public L13Signal DoorSignal;
        public FleeBrain Pod;
        public Transform Dome;
        public Bounds Zone, Alcove;
        public Vector3 Seat = new Vector3(22f, 0f, 40f);
        public float DomeUpY = 2.8f, DomeDownY = 0.02f, RestSeconds = 0.5f;

        public float RestTime { get; private set; }
        public bool DoorOpen => DoorSignal != null && DoorSignal.IsOn;
        public int IntakeHacks { get; private set; }

        float _anim = -1f;
        Vector3 _slideFrom;
        bool _intakeWas;

        void Update()
        {
            bool intake = Intake != null && Intake.IsOn;
            if (intake && !_intakeWas)
            {
                IntakeHacks++;
                GameEvents.RaiseStoryBeat("ch13.intake");
                GameEvents.RaiseToast("INTAKE: AUTHORIZED: A TEAM OF THREE. ARTICLE 3.", UIColors.AccentYellow);
            }
            _intakeWas = intake;
            bool open = intake || (InsideButton != null && InsideButton.IsOn);
            if (IsOn && !AnyInternInside()) open = false;
            if (DoorSignal != null) DoorSignal.Set(open);

            if (!IsOn && Pod != null && Pod.enabled && !Pod.IsCaptured)
            {
                var p = Pod.transform.position;
                var rb = Pod.Body;
                bool rest = InZone(p) && Pod.IsDizzy && !Pod.IsCarried && !Pod.IsDistracted && (rb == null || rb.isKinematic || rb.linearVelocity.magnitude < 0.3f);
                RestTime = rest ? RestTime + Time.deltaTime : 0f;
                if (RestTime >= RestSeconds) Capture();
            }
            if (_anim >= 0f)
            {
                _anim += Time.deltaTime;
                if (Pod != null)
                {
                    float s = Mathf.Clamp01(_anim / 0.4f);
                    var to = new Vector3(Seat.x, _slideFrom.y, Seat.z);
                    Pod.transform.position = Vector3.Lerp(_slideFrom, to, PuzzleUtil.Ease(s));
                }
                if (Dome != null)
                {
                    float d = Mathf.Clamp01((_anim - 0.3f) / 0.6f);
                    var dp = Dome.position; dp.y = Mathf.Lerp(DomeUpY, DomeDownY, d * d); Dome.position = dp;
                    if (d >= 1f && _anim < 1.0f) { Sfx.At(SfxId.DoorSlam, Dome.position, 1f, 0.8f); Fx.Puff(Dome.position, 2f, UIColors.AccentYellow); }
                }
                if (_anim > 1.2f) _anim = -1f;
            }
        }

        public bool InZone(Vector3 p) => p.x >= Zone.min.x && p.x <= Zone.max.x && p.z >= Zone.min.z && p.z <= Zone.max.z && p.y < Zone.max.y + 1f;

        public bool AnyInternInside()
        {
            var team = BossTeam.Members;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || c.State == CharacterState.Lost) continue;
                var f = c.Feet;
                if (f.x >= Alcove.min.x - 0.3f && f.x <= Alcove.max.x && f.z >= Alcove.min.z && f.z <= Alcove.max.z) return true;
            }
            return false;
        }

        /// <summary>DETAINED: capture the pod, slide it onto the seat, drop the VIP dome.</summary>
        public void Capture()
        {
            if (IsOn) return;
            if (Pod != null)
            {
                Pod.Capture();
                if (Pod.Body != null) { Pod.Body.linearVelocity = Vector3.zero; Pod.Body.isKinematic = true; Pod.Body.useGravity = false; }
                _slideFrom = Pod.transform.position;
                Pod.enabled = false;
            }
            _anim = 0f;
            SetOn(true);
            Sfx.At(SfxId.Gavel, Seat, 1f);
            Fx.Confetti(Seat + Vector3.up * 2f);
            GameEvents.RaiseBanner("DETAINED", "MAX ME: DETENTION (VIP). NOBODY HAS EVER CAUGHT HIM. UNTIL NOW.", BannerStyle.Boss);
        }

        /// <summary>C5 restore: pod already domed on the seat.</summary>
        public void SnapCaptured()
        {
            if (Pod != null)
            {
                Pod.Capture();
                if (Pod.Body != null) { Pod.Body.isKinematic = true; Pod.Body.useGravity = false; }
                Pod.transform.position = Seat;
                Pod.enabled = false;
            }
            if (Dome != null) { var dp = Dome.position; dp.y = DomeDownY; Dome.position = dp; }
            _anim = -1f;
            SetOn(true);
        }

        public void ResetState()
        {
            SetOn(false); RestTime = 0f; _anim = -1f;
            if (Dome != null) { var dp = Dome.position; dp.y = DomeUpY; Dome.position = dp; }
        }
    }
}
