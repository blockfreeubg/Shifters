// =====================================================================================
//  SHIFTERS - L1BridgeTutorial.cs   (LEVEL 1)
//  Follow-up tutorial for the living bridge: fires once a teammate lies as a solid bridge inside the zone.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Polls the team (5 Hz) and queues <see cref="Message"/> once someone is a solid bridge inside the zone.</summary>
    public class L1BridgeTutorial : MonoBehaviour
    {
        public TutorialMessage Message = new TutorialMessage();
        public Vector3 ZoneCenter = Vector3.zero;
        public Vector3 ZoneSize = new Vector3(12f, 4f, 12f);

        public bool Fired { get; private set; }
        float _poll;

        void Update()
        {
            if (Fired) return;
            _poll -= Time.deltaTime;
            if (_poll > 0f) return;
            _poll = 0.2f;
            var cm = CharacterManager.Instance;
            if (cm == null) return;
            var team = cm.Team;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || c.Bridge == null || !c.Bridge.IsSolid) continue;
                if (!ZoneMath.Contains(transform, ZoneCenter, ZoneSize, c.transform.position)) continue;
                Fired = true;
                TutorialManager.Ensure().Show(Message);
                return;
            }
        }

        void OnDrawGizmos() => ZoneMath.DrawGizmo(transform, ZoneCenter, ZoneSize, UIColors.AccentYellow);
    }
}
