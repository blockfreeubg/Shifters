using TMPro;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Two linked kinematic platforms hanging from an overhead pulley. When one side is heavier by more than
    /// DiffThresholdKg it goes down (and the other up) at Speed m/s until the travel limits. Weight is measured with a
    /// WeightSensor on each platform (stacked weight counts). Offset s: A = homeA + s, B = homeB - s, |s| &lt;= Travel/2.
    /// </summary>
    public class PulleyLift : MonoBehaviour, IResettable
    {
        public MoverBody PlatformA;
        public MoverBody PlatformB;
        public WeightSensor SensorA;
        public WeightSensor SensorB;
        public float Travel = 3f;
        public float Speed = 1.5f;
        public float DiffThresholdKg = 10f;
        [Tooltip("Initial offset: +Travel/2 = A up / B down.")] public float StartOffset = 1.5f;
        public Vector3 HomeA;
        public Vector3 HomeB;

        [Header("Visual parts")]
        public Transform RopeA;
        public Transform RopeB;
        public Transform Wheel;
        [Tooltip("Local Y of the overhead beam (rope top).")] public float RopeTopY = 6f;
        [Tooltip("Local Y offset of the rope bottom above each platform center.")] public float RopeAttachY = 2.4f;
        public TextMeshPro ReadoutA;
        public TextMeshPro ReadoutB;
        public Revealable Reveal;

        public float Offset => _s;
        public float MassA => SensorA != null ? SensorA.CurrentMass : 0f;
        public float MassB => SensorB != null ? SensorB.CurrentMass : 0f;
        public bool Moving { get; private set; }

        float _s, _creak;
        bool _init;
        int _shownA = -1, _shownB = -1;

        void Init()
        {
            if (_init) return;
            _init = true;
            _s = Mathf.Clamp(StartOffset, -Travel * 0.5f, Travel * 0.5f);
        }

        void Start() { if (Reveal != null) Reveal.DynamicLabel = v => "PULLEY LIFT\n" + Mathf.RoundToInt(MassA) + " KG vs " + Mathf.RoundToInt(MassB) + " KG"; }

        void FixedUpdate()
        {
            Init();
            float diff = MassA - MassB;
            float dir = diff > DiffThresholdKg ? -1f : diff < -DiffThresholdKg ? 1f : 0f;
            float half = Travel * 0.5f;
            float before = _s;
            _s = Mathf.Clamp(_s + dir * Speed * Time.fixedDeltaTime, -half, half);
            bool moving = Mathf.Abs(_s - before) > 1e-5f;
            if (moving && !Moving) Sfx.At(SfxId.BridgeCreak, transform.position + Vector3.up * RopeTopY, 0.8f, 0.9f);
            if (!moving && Moving) Sfx.At(SfxId.PlateRelease, transform.position, 0.6f, 0.7f);
            Moving = moving;
            if (moving)
            {
                _creak -= Time.fixedDeltaTime;
                if (_creak <= 0f) { _creak = 0.7f; Sfx.At(SfxId.BridgeCreak, transform.position + Vector3.up * RopeTopY, 0.5f, Random.Range(0.8f, 1.1f)); }
            }
            if (PlatformA != null) PlatformA.MoveTo(transform.TransformPoint(HomeA + Vector3.up * _s));
            if (PlatformB != null) PlatformB.MoveTo(transform.TransformPoint(HomeB - Vector3.up * _s));
        }

        void Update()
        {
            Init();
            UpdateRope(RopeA, HomeA.y + _s, HomeA);
            UpdateRope(RopeB, HomeB.y - _s, HomeB);
            if (Wheel != null && Moving) Wheel.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sign(MassB - MassA) * Speed * 80f * Time.deltaTime);
            int a = Mathf.RoundToInt(MassA), b = Mathf.RoundToInt(MassB);
            if (ReadoutA != null && a != _shownA) { _shownA = a; ReadoutA.text = a + " KG"; }
            if (ReadoutB != null && b != _shownB) { _shownB = b; ReadoutB.text = b + " KG"; }
        }

        void UpdateRope(Transform rope, float platformY, Vector3 home)
        {
            if (rope == null) return;
            float bottom = platformY + RopeAttachY;
            float len = Mathf.Max(0.05f, RopeTopY - bottom);
            rope.localPosition = new Vector3(home.x, bottom + len * 0.5f, home.z);
            rope.localScale = new Vector3(1f, len, 1f);
        }

        public void ResetState()
        {
            _init = false;
            Init();
            if (PlatformA != null) PlatformA.Snap(transform.TransformPoint(HomeA + Vector3.up * _s));
            if (PlatformB != null) PlatformB.Snap(transform.TransformPoint(HomeB - Vector3.up * _s));
        }
    }
}
