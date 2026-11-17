using UnityEngine;

namespace Shifters
{
    /// <summary>SHIFT 10 ghost bridge director: the Ghosts of Teams Past bow and say "WE ONLY HELP TEAMS." the first time
    /// the whole team stands in the watch zone (bridge extends), and sulk "...oh. You're a solo act." when the team
    /// splits while somebody still has to cross (bridge fades after its OffDelay grace).</summary>
    public sealed class L11GhostWatch : MonoBehaviour
    {
        public ZoneTrigger Zone;
        public RetractableBridge Bridge;
        public GhostTrio[] Trios = new GhostTrio[0];
        [Tooltip("World z of the near edge of the gap: interns below it still have to cross.")] public float NearEdgeZ = 46f;
        public float AloneCooldown = 15f;

        bool _wasOn, _helped;
        float _nextAlone;
        public int AloneCount { get; private set; }

        void Update()
        {
            if (Zone == null) return;
            bool on = Zone.IsOn;
            if (on && !_wasOn)
            {
                if (!_helped) { _helped = true; GameEvents.RaiseStoryBeat("ch10.ghosts"); }
                foreach (var t in Trios) if (t != null) t.Bow();
                Sfx.At(SfxId.BridgeForm, Bridge != null ? Bridge.transform.position : transform.position);
            }
            else if (!on && _wasOn && Time.time >= _nextAlone && SomeoneStillToCross())
            {
                _nextAlone = Time.time + AloneCooldown;
                AloneCount++;
                GameEvents.RaiseStoryBeat("ch10.ghosts.alone");
                foreach (var t in Trios) if (t != null) t.Wave();
            }
            _wasOn = on;
        }

        bool SomeoneStillToCross()
        {
            var cm = CharacterManager.Instance;
            if (cm == null) return false;
            foreach (var c in cm.Team)
                if (c != null && c.IsAlive && c.transform.position.z < NearEdgeZ && c.transform.position.z > NearEdgeZ - 30f) return true;
            return false;
        }
    }
}
