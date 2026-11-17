// =====================================================================================
//  SHIFTERS - TutorialTrigger.cs   [owned by the LEVEL agent]
//  Zone that queues a TutorialMessage when the ACTIVE character enters (once). Optional
//  conditions: the active character must have / must lack a perk, the team AI mode must match,
//  and an extra Delay after entering. Build with LevelKit.Tutorial.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class TutorialTrigger : MonoBehaviour
    {
        public TutorialMessage Message = new TutorialMessage();
        public Vector3 ZoneCenter = Vector3.zero;
        public Vector3 ZoneSize = new Vector3(4f, 3f, 4f);

        [Header("Conditions")]
        [Tooltip("Active character must see ALL of these perspectives (None = no requirement).")] public PerspectiveMask RequireActivePerk = PerspectiveMask.None;
        [Tooltip("Active character must see NONE of these perspectives.")] public PerspectiveMask RequireActiveLacksPerk = PerspectiveMask.None;
        public bool CheckAIMode;
        public TeamAIMode RequireAIMode = TeamAIMode.Follow;
        [Tooltip("Seconds after entering (conditions met) before the message is queued.")] public float Delay;

        public bool Fired { get; private set; }

        float _poll, _countdown = -1f;

        public bool Contains(ShifterCharacter c) => ZoneMath.ContainsCharacter(transform, ZoneCenter, ZoneSize, c);

        public bool ConditionsMet(ShifterCharacter active)
        {
            if (active == null || !active.IsAlive) return false;
            var p = active.Perspectives;
            if (RequireActivePerk != PerspectiveMask.None && (p & RequireActivePerk) != RequireActivePerk) return false;
            if (RequireActiveLacksPerk != PerspectiveMask.None && (p & RequireActiveLacksPerk) != 0) return false;
            if (CheckAIMode && (CharacterManager.Instance == null || CharacterManager.Instance.AIMode != RequireAIMode)) return false;
            return true;
        }

        void Update()
        {
            if (Fired) return;
            float dt = Time.deltaTime;
            if (_countdown >= 0f)
            {
                _countdown -= dt;
                if (_countdown <= 0f) Fire();
                return;
            }
            _poll -= dt;
            if (_poll > 0f) return;
            _poll = 0.1f;
            var active = CharacterManager.Instance != null ? CharacterManager.Instance.Active : null;
            if (active == null || !Contains(active) || !ConditionsMet(active)) return;
            if (Delay > 0f) _countdown = Delay; else Fire();
        }

        /// <summary>Queue the message now (also usable from scripted events).</summary>
        public void Fire()
        {
            if (Fired) return;
            Fired = true;
            _countdown = -1f;
            TutorialManager.Ensure().Show(Message);
        }

        void OnDrawGizmos() => ZoneMath.DrawGizmo(transform, ZoneCenter, ZoneSize, UIColors.AccentYellow);
    }
}
