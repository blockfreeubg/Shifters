// =====================================================================================
//  SHIFTERS - FlickerRevealView.cs   [owned by STORY_KIT]   (ScareKit.FlickerReveal runtime)
//  Start: StoryBeat == CueId, Play(), or the active intern within ArmRadius of ArmCenter (local; ArmRadius 0 = off).
//  Each step: a short blackout (<= 0.22 s; MILD: half as many steps, shorter darkness) during which the figure moves to
//  the next position; steps are >= 0.34 s apart (<= 3 Hz) and squeezed so the punchline lands < 3 s after the start.
//  Last flicker: FinalReveal shown (figure stays) or the figure vanishes; then the punchline sign + slide whistle.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public sealed class FlickerRevealView : MonoBehaviour
    {
        public string CueId;
        public Vector3[] Positions;
        public float Interval = 0.6f;
        public Transform Figure;
        public GameObject FinalReveal;
        public TextMeshPro PunchText;
        public float ArmRadius;
        public Vector3 ArmCenter;
        public float PunchHold = 3f;

        public int State { get; private set; }   // 0 armed, 1 playing, 2 punchline, 3 done
        public int StepsPlayed { get; private set; }
        public float StartedAt { get; private set; } = -1f;
        public float PunchlineAt { get; private set; } = -1f;

        int[] _plan;
        int _step;
        float _next, _darkEnd, _poll, _interval, _dark;
        bool _moved;

        void OnEnable() { GameEvents.StoryBeat += OnBeat; }
        void OnDisable() { GameEvents.StoryBeat -= OnBeat; }
        void OnBeat(string id) { if (id == CueId) Play(); }

        public bool Play()
        {
            if (State != 0 || Positions == null || Positions.Length == 0) return false;
            if (ScareDirector.HasSeen(CueId)) { State = 3; return false; }
            if (!ScareDirector.TryPlay(CueId)) return false;
            bool mild = ScareDirector.Mild;
            int n = Positions.Length;
            // plan: indices of the positions shown after each flicker (MILD halves the flickers, always ends at the last)
            var plan = new System.Collections.Generic.List<int>();
            for (int i = 1; i < n; i++) if (!mild || (n - 1 - i) % 2 == 0) plan.Add(i);
            plan.Add(-1);   // final flicker: reveal / vanish
            _plan = plan.ToArray();
            _interval = Mathf.Clamp(Interval, 0.34f, Mathf.Max(0.34f, 2.6f / _plan.Length));
            _dark = Mathf.Min(0.22f, _interval * 0.45f) * (mild ? 0.6f : 1f);
            _step = 0; _next = Time.time + 0.05f; State = 1; StartedAt = Time.time; StepsPlayed = 0;
            if (Figure != null) { Figure.gameObject.SetActive(true); Figure.localPosition = Positions[0]; }
            return true;
        }

        void Update()
        {
            float now = Time.time;
            if (State == 0)
            {
                if (ArmRadius <= 0f) return;
                _poll -= Time.deltaTime;
                if (_poll > 0f) return;
                _poll = 0.2f;
                var a = StoryKit.Active;
                if (a != null && a.IsAlive && (a.transform.position - transform.TransformPoint(ArmCenter)).sqrMagnitude < ArmRadius * ArmRadius) Play();
                return;
            }
            if (State == 1)
            {
                if (now >= _next && _step < _plan.Length)
                {
                    ScareKit.Blackout(_dark, true);
                    Sfx.Play(SfxId.Creak, 0.5f, 1f + _step * 0.06f);
                    _darkEnd = now + _dark * 0.5f; _moved = false;
                    _next = now + _interval;
                }
                if (!_moved && _darkEnd > 0f && now >= _darkEnd)
                {
                    _moved = true;
                    int idx = _plan[_step];
                    if (idx >= 0) { if (Figure != null) Figure.localPosition = Positions[idx]; }
                    else
                    {
                        if (FinalReveal != null) FinalReveal.SetActive(true);
                        else if (Figure != null) Figure.gameObject.SetActive(false);
                    }
                    _step++; StepsPlayed = _step;
                    if (_step >= _plan.Length) { State = 2; PunchlineAt = now + _dark; _next = PunchlineAt; }
                }
                return;
            }
            if (State == 2)
            {
                if (PunchText != null && !PunchText.gameObject.activeSelf && now >= _next)
                {
                    PunchText.gameObject.SetActive(true);
                    Sfx.Play(SfxId.SlideWhistleDown, 0.8f);
                    PunchlineAt = now;
                }
                if (now >= _next + PunchHold) { if (PunchText != null) PunchText.gameObject.SetActive(false); State = 3; }
            }
        }
    }
}
