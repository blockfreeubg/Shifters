using UnityEngine;

namespace Shifters
{
    /// <summary>SHIFT 10 S3: the mausoleum doors creak open OUTWARD (1.4 s) when the first intern reaches the plaza. Visual
    /// only (the doors have no colliders), so they can never block anybody.</summary>
    public sealed class L11MausoleumDoors : MonoBehaviour
    {
        public Transform DoorL, DoorR;
        public float OpenZ = 57.5f;
        public float OpenDegrees = 95f;
        public float Seconds = 1.4f;
        public bool Opening { get; private set; }
        float _t;

        void Update()
        {
            if (!Opening)
            {
                var cm = CharacterManager.Instance;
                if (cm == null) return;
                foreach (var c in cm.Team)
                    if (c != null && c.IsAlive && c.transform.position.z >= OpenZ) { Opening = true; Sfx.At(SfxId.Creak, transform.position, 1f, 0.8f); break; }
                return;
            }
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.1f, Seconds));
            float a = OpenDegrees * Mathf.SmoothStep(0f, 1f, _t);
            if (DoorL != null) DoorL.localRotation = Quaternion.Euler(0f, a, 0f);
            if (DoorR != null) DoorR.localRotation = Quaternion.Euler(0f, -a, 0f);
        }
    }
}
