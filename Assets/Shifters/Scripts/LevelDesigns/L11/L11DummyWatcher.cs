using UnityEngine;

namespace Shifters
{
    /// <summary>SHIFT 10 S0: a stationary decor dummy whose head slowly follows the nearest intern (creepy-cute).
    /// Spooky MILD shows the sticker-face variant instead.</summary>
    public sealed class L11DummyWatcher : MonoBehaviour
    {
        public DummyModel Dummy;
        public DummyModel MildDummy;
        public float Range = 12f;
        float _next;

        void Start()
        {
            bool mild = ScareDirector.Mild;
            if (Dummy != null) Dummy.gameObject.SetActive(!mild || MildDummy == null);
            if (MildDummy != null) MildDummy.gameObject.SetActive(mild);
        }

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.1f;
            var d = Dummy != null && Dummy.gameObject.activeInHierarchy ? Dummy : MildDummy;
            if (d == null) return;
            var cm = CharacterManager.Instance;
            Vector3? best = null; float bestD = Range * Range;
            if (cm != null)
                foreach (var c in cm.Team)
                {
                    if (c == null || !c.IsAlive) continue;
                    float dd = (c.transform.position - d.transform.position).sqrMagnitude;
                    if (dd < bestD) { bestD = dd; best = c.transform.position + Vector3.up * 1.4f; }
                }
            d.LookTarget = best;
        }
    }
}
