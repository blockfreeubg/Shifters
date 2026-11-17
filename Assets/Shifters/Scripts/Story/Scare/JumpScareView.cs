// =====================================================================================
//  SHIFTERS - JumpScareView.cs   [owned by STORY_KIT]   (ScareKit.JumpScare runtime)
//  Polls Alive interns against the trigger box (10 Hz). On entry: ScareDirector.TryPlay(CueId) -> popper springs up
//  (0.18 s) + ScareStinger (normal SFX bus level) -> PUNCHLINE at 1.1 s (slide whistle, sign, confetti, popper slumps)
//  -> retracts at 3.6 s. Refused cues (cooldown / blocked) re-arm and retry while an intern stays inside; seen cues never.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public sealed class JumpScareView : MonoBehaviour
    {
        public string CueId;
        public ScareKind Kind;
        public Vector3 TriggerCenter, TriggerSize = new Vector3(3f, 3f, 3f);
        public Transform Popper;
        public Vector3 PopperRest;
        public TextMeshPro PunchText;
        public float PunchAt = 1.1f, RetractAt = 3.6f;

        public int State { get; private set; }   // 0 armed, 1 playing, 2 done
        public float PunchlineDelay => PunchAt;
        float _t, _poll;
        Quaternion _rest = Quaternion.identity;
        bool _restSet;

        // captured lazily: the factory assigns Popper AFTER AddComponent (Awake would see null)
        void CaptureRest() { if (_restSet || Popper == null) return; _rest = Popper.localRotation; _restSet = true; }
        void Start() { CaptureRest(); }

        /// <summary>Play now if the ScareDirector allows it. Returns true if it started.</summary>
        public bool Trigger()
        {
            if (State != 0) return false;
            if (ScareDirector.HasSeen(CueId)) { State = 2; return false; }
            if (!ScareDirector.TryPlay(CueId)) return false;
            State = 1; _t = 0f;
            if (Popper != null)
            {
                Popper.gameObject.SetActive(true);
                CaptureRest();
                Popper.localRotation = _rest;
                Popper.localPosition = PopperRest - new Vector3(0f, 1.2f, 0f);
                Popper.localScale = Vector3.one * 0.4f;
            }
            Sfx.Play(SfxId.ScareStinger, 0.85f);
            Fx.Puff(transform.TransformPoint(PopperRest), 1.2f);
            return true;
        }

        void Update()
        {
            if (State == 2) return;
            if (State == 0)
            {
                _poll -= Time.deltaTime;
                if (_poll > 0f) return;
                _poll = 0.1f;
                if (ScareKit.AnyAliveInBox(transform, TriggerCenter, TriggerSize)) Trigger();
                return;
            }
            _t += Time.deltaTime;
            if (Popper != null)
            {
                if (_t < 0.18f)
                {
                    float k = _t / 0.18f;
                    float over = 1f + Mathf.Sin(k * Mathf.PI) * 0.15f;
                    Popper.localPosition = Vector3.Lerp(PopperRest - new Vector3(0f, 1.2f, 0f), PopperRest, k);
                    Popper.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, k) * over;
                }
                else if (_t < RetractAt) { Popper.localPosition = PopperRest; Popper.localScale = Vector3.one; }
                if (_t >= PunchAt && _t < RetractAt) Popper.localRotation = Quaternion.Slerp(Popper.localRotation, _rest * Quaternion.Euler(0f, 0f, 14f), Time.deltaTime * 6f);
            }
            if (_t >= PunchAt && PunchText != null && !PunchText.gameObject.activeSelf)
            {
                PunchText.gameObject.SetActive(true);
                Sfx.Play(SfxId.SlideWhistleDown, 0.8f);
                Fx.Confetti(transform.TransformPoint(PopperRest + Vector3.up * 1.8f));
            }
            if (_t >= RetractAt)
            {
                float k = Mathf.Clamp01((_t - RetractAt) / 0.3f);
                if (Popper != null) Popper.localScale = Vector3.one * (1f - k);
                if (k >= 1f)
                {
                    if (Popper != null) { Fx.Poof(Popper.position + Vector3.up * 0.8f, Color.white, 0.8f); Popper.gameObject.SetActive(false); }
                    if (PunchText != null) PunchText.gameObject.SetActive(false);
                    State = 2;
                }
            }
        }
    }
}
