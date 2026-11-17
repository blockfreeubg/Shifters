// =====================================================================================
//  SHIFTERS - ScriptTrigger.cs   [owned by STORY_KIT]   (StoryKit.ScriptTrigger)
//  Collider-free box trigger: polls Alive team positions at 10 Hz against a box (local to this transform; Size = full
//  extents) and raises GameEvents.StoryBeat(Id) (+ the C# event Fired). StoryPlayer plays the script with that id.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public enum StoryTriggerWho { AnyIntern, ActiveIntern, WholeTeam }

    public sealed class ScriptTrigger : MonoBehaviour
    {
        public string Id;
        public Vector3 Size = new Vector3(4f, 3f, 4f);
        public bool Once = true;
        public StoryTriggerWho Who = StoryTriggerWho.AnyIntern;
        /// <summary>Re-fire delay for Once = false (seconds after everyone left).</summary>
        public float RearmSeconds = 1f;

        public event Action<ScriptTrigger> Fired;
        public bool HasFired { get; private set; }
        public int FireCount { get; private set; }

        float _poll, _emptyFor;
        bool _inside;

        public bool Contains(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            var h = Size * 0.5f;
            return Mathf.Abs(l.x) <= h.x && Mathf.Abs(l.y) <= h.y + 0.5f && Mathf.Abs(l.z) <= h.z;
        }

        /// <summary>True when the configured team condition holds right now.</summary>
        public bool ConditionMet()
        {
            var cm = CharacterManager.Instance;
            if (cm == null) return false;
            if (Who == StoryTriggerWho.ActiveIntern) return cm.Active != null && cm.Active.IsAlive && Contains(cm.Active.transform.position);
            var team = cm.Team;
            if (team == null) return false;
            int alive = 0, inside = 0;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || !c.IsAlive) continue;
                alive++;
                if (Contains(c.transform.position)) inside++;
            }
            return Who == StoryTriggerWho.WholeTeam ? alive > 0 && inside == alive : inside > 0;
        }

        /// <summary>Raise the beat now (ignores the box; respects Once).</summary>
        public void Fire()
        {
            if (Once && HasFired) return;
            HasFired = true;
            FireCount++;
            if (!string.IsNullOrEmpty(Id)) GameEvents.RaiseStoryBeat(Id);
            Fired?.Invoke(this);
        }

        public void Rearm() { HasFired = false; _inside = false; }

        void Update()
        {
            if (Once && HasFired) return;
            _poll -= Time.deltaTime;
            if (_poll > 0f) return;
            _poll = 0.1f;
            bool now = ConditionMet();
            if (now && !_inside) { _inside = true; Fire(); }
            else if (!now && _inside) { _emptyFor += 0.1f; if (_emptyFor >= RearmSeconds) { _inside = false; _emptyFor = 0f; } }
            else _emptyFor = 0f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Size);
        }
    }
}
