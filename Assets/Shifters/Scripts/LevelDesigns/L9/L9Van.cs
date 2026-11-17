// SHIFTERS - L9Van.cs   [LEVEL 9 - THE GETAWAY]  PERCY'S PERCOLATED DELIVERIES backs up to the north door (C07 "c07.van").
using UnityEngine;

namespace Shifters
{
    public sealed class L9Van : MonoBehaviour
    {
        public Vector3 From, To;
        public float Seconds = 2.6f;
        public string Beat = "c07.van";
        public bool Arrived => _t >= Seconds;
        float _t = -1f, _beep;

        void OnEnable() { GameEvents.StoryBeat += OnBeat; }
        void OnDisable() { GameEvents.StoryBeat -= OnBeat; }
        void Start() { transform.localPosition = From; }
        void OnBeat(string id) { if (id == Beat) Go(); }

        /// <summary>Reverse into the dock (idempotent).</summary>
        public void Go()
        {
            if (_t >= 0f) return;
            _t = 0f;
            GameEvents.RaiseToast("BEEP. BEEP. BEEP. (GETAWAY VAN REVERSING)", UIColors.AccentYellow);
        }

        void Update()
        {
            if (_t < 0f || _t >= Seconds) return;
            _t += Time.deltaTime;
            _beep -= Time.deltaTime;
            if (_beep <= 0f) { _beep = 0.5f; Sfx.At(SfxId.CameraBeep, transform.position, 0.8f, 0.7f); }
            transform.localPosition = Vector3.Lerp(From, To, PuzzleUtil.Ease(Mathf.Clamp01(_t / Seconds)));
            if (_t >= Seconds) { Sfx.At(SfxId.Impact, transform.position, 0.8f); Fx.Poof(transform.position + Vector3.up, Color.white, 1.4f); }
        }
    }
}
