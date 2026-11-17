using UnityEngine;

namespace Shifters
{
    /// <summary>SHIFT 10 S3 "ch10.ownGrave": when a reprinted intern stands within 3 m of the RECENTLY REPRINTED tombstone
    /// that names it ("... REPRINTED AS {CLONE}."), the clone admires itself once (scare #8, via the ScareDirector).</summary>
    public sealed class L11OwnGrave : MonoBehaviour
    {
        public TombstoneRowView Row;
        public float Radius = 3f;
        public bool Fired { get; private set; }
        float _next;

        void Update()
        {
            if (Fired || Row == null || Row.Texts == null || Time.time < _next) return;
            _next = Time.time + 0.25f;
            var cm = CharacterManager.Instance;
            if (cm == null) return;
            foreach (var c in cm.Team)
            {
                if (c == null || !c.IsAlive || c.Profile == null) continue;
                string me = c.DisplayName;
                if (string.IsNullOrEmpty(me)) continue;
                me = me.ToUpperInvariant();
                foreach (var t in Row.Texts)
                {
                    if (t == null || string.IsNullOrEmpty(t.text)) continue;
                    if (t.text.ToUpperInvariant().IndexOf("AS " + me, System.StringComparison.Ordinal) < 0) continue;
                    Vector3 d = c.transform.position - t.transform.position; d.y = 0f;
                    if (d.magnitude > Radius) continue;
                    if (!ScareDirector.CanPlay("ch10.ownGrave")) return;
                    Fired = true;
                    ScareDirector.TryPlay("ch10.ownGrave");
                    GameEvents.RaiseStoryBeat("ch10.ownGrave");
                    return;
                }
            }
        }
    }
}
