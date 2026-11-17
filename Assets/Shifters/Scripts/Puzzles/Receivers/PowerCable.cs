using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Floor conduit from a source to whatever it powers. Its core strip glows (LightBlue) while the source is on and a
    /// bright pulse bead runs along the path toward the receiver - readability: you SEE what is connected to what.
    /// Built by PuzzleKit.Cable(parent, from, to, source).
    /// </summary>
    public class PowerCable : MonoBehaviour
    {
        public SignalSource Source;
        public Renderer[] Cores = new Renderer[0];
        public Material OnMaterial;
        public Material OffMaterial;
        [Tooltip("Path points local to this transform (for the pulse).")] public Vector3[] Path = new Vector3[0];
        public Transform Pulse;
        public float PulseSpeed = 5f;

        public bool Lit { get; private set; }
        float _dist, _length;
        bool _subscribed, _applied;

        void OnEnable() { Subscribe(); Apply(Source != null && Source.IsOn); }
        void OnDisable() { Unsubscribe(); }

        /// <summary>Change the source (re-subscribes).</summary>
        public void SetSource(SignalSource s)
        {
            Unsubscribe();
            Source = s;
            if (isActiveAndEnabled) Subscribe();
            Apply(Source != null && Source.IsOn);
        }

        void Subscribe() { if (_subscribed || Source == null) return; Source.Changed += OnChanged; _subscribed = true; }
        void Unsubscribe() { if (!_subscribed) return; if (Source != null) Source.Changed -= OnChanged; _subscribed = false; }
        void OnChanged(SignalSource s, bool on) => Apply(on);

        public void Apply(bool on)
        {
            if (_applied && on == Lit) return;
            _applied = true;
            Lit = on;
            var m = on ? OnMaterial : OffMaterial;
            if (m != null) for (int i = 0; i < Cores.Length; i++) if (Cores[i] != null) Cores[i].sharedMaterial = m;
            if (Pulse != null) Pulse.gameObject.SetActive(on && Application.isPlaying);
            _dist = 0f;
        }

        void Update()
        {
            if (!Lit || Pulse == null || Path.Length < 2) return;
            if (_length <= 0f) for (int i = 1; i < Path.Length; i++) _length += Vector3.Distance(Path[i - 1], Path[i]);
            if (_length <= 0f) return;
            _dist = Mathf.Repeat(_dist + PulseSpeed * Time.deltaTime, _length);
            float d = _dist;
            for (int i = 1; i < Path.Length; i++)
            {
                float seg = Vector3.Distance(Path[i - 1], Path[i]);
                if (d <= seg || i == Path.Length - 1) { Pulse.localPosition = Vector3.Lerp(Path[i - 1], Path[i], seg > 0f ? Mathf.Clamp01(d / seg) : 1f); break; }
                d -= seg;
            }
        }
    }
}
