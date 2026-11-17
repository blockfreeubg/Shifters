// =====================================================================================
//  SHIFTERS - StoryLineSpot.cs   [owned by STORY_KIT]   (StoryKit.HologramTauntSpot / StoryKit.LineSpot runtime)
//  Proximity (active intern within Radius, polled at 5 Hz; no collider) or StoryBeat == TriggerBeat -> shows the line
//  once per level attempt (StoryAttemptMemory key "spot:" + scene + LineId). With a Hologram child: Max flickers in
//  (scale tween), turns to the intern (yaw only), says the line, fades out.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class StoryLineSpot : MonoBehaviour
    {
        public string LineId, Text;
        public float Radius = 5f;
        /// <summary>When set, the spot fires on this StoryBeat instead of by proximity.</summary>
        public string TriggerBeat;
        public GameObject Hologram;
        public bool SpeakerIsPercy;
        public bool Radio;
        public bool OncePerAttempt = true;

        public bool HasPlayed { get; private set; }
        public bool IsShowing => _state != 0;

        int _state, _token;   // 0 idle, 1 in, 2 hold, 3 out
        float _t, _poll, _hold;
        string _key;

        string Key => _key ?? (_key = "spot:" + gameObject.scene.name + ":" + (LineId ?? name) + ":" + transform.position.ToString("F0"));

        void OnEnable() { GameEvents.StoryBeat += OnBeat; }
        void OnDisable()
        {
            GameEvents.StoryBeat -= OnBeat;
            if (_token != 0) { StoryKitSay.Hide(_token); _token = 0; }
            if (Hologram != null) Hologram.SetActive(false);
            _state = 0;
        }

        void OnBeat(string id) { if (!string.IsNullOrEmpty(TriggerBeat) && id == TriggerBeat) Play(); }

        /// <summary>Show the line now (respects once-per-attempt).</summary>
        public void Play()
        {
            if (_state != 0) return;
            if (OncePerAttempt && (HasPlayed || StoryAttemptMemory.Seen(Key))) return;
            HasPlayed = true;
            StoryAttemptMemory.Mark(Key);
            string text = string.IsNullOrEmpty(Text) ? StoryKitLines.Resolve(LineId) : Text;
            _hold = StoryKitSay.AutoSeconds(text) + 0.4f;
            if (!string.IsNullOrEmpty(text))
                _token = SpeakerIsPercy ? StoryKitSay.Percy(text, _hold, Radio) : StoryKitSay.Max(text, _hold);
            if (Hologram != null)
            {
                Hologram.SetActive(true);
                Hologram.transform.localScale = new Vector3(1f, 0.02f, 1f);
                Sfx.Play(SfxId.TVStatic, 0.6f);
            }
            _state = 1; _t = 0f;
        }

        void FaceActive()
        {
            var a = StoryKit.Active;
            if (a == null || Hologram == null) return;
            var d = a.transform.position - Hologram.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) Hologram.transform.rotation = Quaternion.Slerp(Hologram.transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 0.2f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_state == 0)
            {
                if (!string.IsNullOrEmpty(TriggerBeat) || (OncePerAttempt && HasPlayed)) return;
                _poll -= dt;
                if (_poll > 0f) return;
                _poll = 0.2f;
                var a = StoryKit.Active;
                if (a != null && a.IsAlive && (a.transform.position - transform.position).sqrMagnitude < Radius * Radius) Play();
                return;
            }
            _t += dt;
            if (Hologram != null)
            {
                FaceActive();
                float s = _state == 1 ? Mathf.Clamp01(_t / 0.25f) : _state == 3 ? 1f - Mathf.Clamp01(_t / 0.3f) : 1f;
                float flick = _state == 1 && _t < 0.25f && ((int)(_t * 24f) % 2 == 0) ? 0.6f : 1f;
                Hologram.transform.localScale = new Vector3(1f, Mathf.Max(0.02f, s * flick), 1f);
            }
            if (_state == 1 && _t >= 0.25f) { _state = 2; _t = 0f; }
            else if (_state == 2 && _t >= _hold) { _state = 3; _t = 0f; if (_token != 0) { StoryKitSay.Hide(_token); _token = 0; } }
            else if (_state == 3 && _t >= 0.3f) { _state = 0; if (Hologram != null) Hologram.SetActive(false); }
        }
    }
}
