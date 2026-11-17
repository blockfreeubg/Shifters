using UnityEngine;

namespace Shifters
{
    /// <summary>Impact sounds/dust for physics props (wood thunk, barrel bonk, heavy clang). Rate limited.</summary>
    public class PropImpact : MonoBehaviour
    {
        public SfxId Sound = SfxId.Bonk;
        public float MinSpeed = 3.5f;
        public float Pitch = 1f;
        public bool Noise = true;
        float _last;

        void OnCollisionEnter(Collision c)
        {
            float s = c.relativeVelocity.magnitude;
            if (s < MinSpeed || Time.time - _last < 0.15f || c.contactCount == 0) return;
            _last = Time.time;
            var p = c.GetContact(0).point;
            Sfx.At(Sound, p, Mathf.Clamp01(s / 12f), Pitch * Random.Range(0.92f, 1.08f));
            if (s > 8f)
            {
                Fx.Puff(p, 0.7f);
                if (Noise) GameEvents.RaiseNoise(p, 6f, null, 0.5f);
            }
        }
    }
}
