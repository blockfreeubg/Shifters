// =====================================================================================
//  SHIFTERS - PhoneCallView.cs   [owned by STORY_KIT]   (ScareKit.PhoneCall runtime)
//  Idle -> Ringing (active intern within RingRadius, or StoryBeat CueId+".ring" / Ring()) -> E "Answer phone" ->
//  ScareDirector.TryPlay(CueId): heavy breathing (FULL only) -> Percy punchline at 1.8 s. If the director refuses
//  (cooldown / blocked) the call is just Percy's line (no scare).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class PhoneCallView : MonoBehaviour, IInteractable
    {
        public string CueId;
        public Transform Handset;
        public MeshRenderer Lamp;
        public BoxCollider Zone;
        public float RingRadius = 10f;
        public string Punchline = "Sorry. Coffee machines don't have lungs. I'm just steaming.";

        public int State { get; private set; }   // 0 idle, 1 ringing, 2 answered, 3 done
        public bool WasScare { get; private set; }
        float _t, _poll, _ringT;
        Vector3 _handRest;
        int _token;

        bool _handSet;
        // captured lazily: the factory assigns Handset AFTER AddComponent (Awake would see null)
        void CaptureRest() { if (_handSet || Handset == null) return; _handRest = Handset.localPosition; _handSet = true; }
        void Start() { CaptureRest(); }
        void OnEnable() { GameEvents.StoryBeat += OnBeat; }
        void OnDisable() { GameEvents.StoryBeat -= OnBeat; if (_token != 0) { StoryKitSay.Hide(_token); _token = 0; } }
        void OnBeat(string id) { if (id == CueId + ".ring") Ring(); }

        public void Ring() { if (State == 0) { State = 1; _ringT = 0f; } }

        public int InteractPriority => 2;
        public Vector3 InteractPoint => transform.position + Vector3.up * 0.95f;
        public bool CanInteract(ShifterCharacter who, out string blockedReason) { blockedReason = null; return State == 1; }
        public string GetPrompt(ShifterCharacter who) => "Answer phone";
        public void Interact(ShifterCharacter who) => Answer();

        public void Answer()
        {
            if (State != 1) return;
            State = 2; _t = 0f;
            WasScare = ScareDirector.TryPlay(CueId);
            if (Handset != null) Handset.localPosition = _handRest + new Vector3(0f, 0.25f, 0.1f);
            if (WasScare && !ScareDirector.Mild)
            {
                _token = StoryKitSay.Say("PHONE", "UNKNOWN CALLER", new Color(0.6f, 0.6f, 0.66f), "*heavy breathing*", 1.7f, Expression.Neutral, true);
                Sfx.Play(SfxId.Whisper, 0.7f, 0.7f);
            }
            else _t = 1.8f;   // MILD / not a scare: straight to the punchline
        }

        void Update()
        {
            if (State == 0)
            {
                _poll -= Time.deltaTime;
                if (_poll > 0f) return;
                _poll = 0.25f;
                var a = StoryKit.Active;
                if (a != null && a.IsAlive && (a.transform.position - transform.position).sqrMagnitude < RingRadius * RingRadius) Ring();
                return;
            }
            if (State == 1)
            {
                _ringT += Time.deltaTime;
                float cyc = _ringT % 2f;
                bool ringing = cyc < 1f;
                if (Handset != null) Handset.localPosition = _handRest + (ringing ? new Vector3(0f, Mathf.Abs(Mathf.Sin(_ringT * 40f)) * 0.02f, 0f) : Vector3.zero);
                if (Lamp != null) Lamp.enabled = ringing;
                if (ringing && Mathf.Repeat(_ringT, 0.25f) < Time.deltaTime) Sfx.Play(SfxId.CameraBeep, 0.45f, 1.7f);
                return;
            }
            if (State == 2)
            {
                _t += Time.deltaTime;
                if (_t >= 1.8f)
                {
                    if (_token != 0) StoryKitSay.Hide(_token);
                    _token = StoryKitSay.Percy(Punchline, 3.5f, true);
                    Sfx.Play(SfxId.Gurgle, 0.8f);
                    State = 3; _t = 0f;
                    if (Lamp != null) Lamp.enabled = false;
                    if (Zone != null) Zone.enabled = false;
                }
            }
        }
    }
}
