// =====================================================================================
//  SHIFTERS - BreadcrumbTrail.cs   [owned by the CORE agent]
//  Path recorded from the ACTIVE character (by CharacterManager) for Follow-mode AI:
//  a crumb every 0.6 m plus explicit jump markers. Ring buffer, no allocations.
//  Each crumb has a monotonically increasing Serial so followers can track progress.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public struct Breadcrumb
    {
        public Vector3 Position;   // leader's feet
        public bool Grounded;
        public bool Jump;          // leader jumped here (takeoff point)
        public bool Sprint;
        public int Serial;
    }

    public class BreadcrumbTrail
    {
        public const float Spacing = 0.6f;
        const int Capacity = 256;
        readonly Breadcrumb[] _crumbs = new Breadcrumb[Capacity];
        int _start, _count, _nextSerial;

        /// <summary>Changes whenever the trail is reset (new leader).</summary>
        public int Version { get; private set; }
        public ShifterCharacter Leader { get; private set; }
        public int Count => _count;
        public int OldestSerial => _count > 0 ? _crumbs[_start].Serial : _nextSerial;
        public int NewestSerial => _count > 0 ? _nextSerial - 1 : -1;

        public void Reset(ShifterCharacter leader)
        {
            Leader = leader;
            _start = 0; _count = 0;
            Version++;
            if (leader != null) Add(leader.Feet, leader.Motor == null || leader.Motor.IsGrounded, false, false);
        }

        /// <summary>Called every physics step by CharacterManager for the active character.</summary>
        public void Record(ShifterCharacter leader)
        {
            if (leader == null) return;
            if (leader != Leader) Reset(leader);
            var m = leader.Motor;
            bool grounded = m == null || m.IsGrounded;
            Vector3 p = leader.Feet;
            if (_count == 0) { Add(p, grounded, false, m != null && m.IsSprinting); return; }
            ref var last = ref _crumbs[(_start + _count - 1) % Capacity];
            if ((p - last.Position).sqrMagnitude >= Spacing * Spacing || grounded != last.Grounded)
                Add(p, grounded, false, m != null && m.IsSprinting);
        }

        /// <summary>Jump marker at the leader's takeoff point.</summary>
        public void MarkJump(ShifterCharacter leader)
        {
            if (leader == null || leader != Leader) return;
            Add(leader.Feet, true, true, leader.Motor != null && leader.Motor.IsSprinting);
        }

        void Add(Vector3 p, bool grounded, bool jump, bool sprint)
        {
            var c = new Breadcrumb { Position = p, Grounded = grounded, Jump = jump, Sprint = sprint, Serial = _nextSerial++ };
            if (_count < Capacity) { _crumbs[(_start + _count) % Capacity] = c; _count++; }
            else { _crumbs[_start] = c; _start = (_start + 1) % Capacity; }
        }

        public bool TryGet(int serial, out Breadcrumb crumb)
        {
            int idx = serial - OldestSerial;
            if (_count == 0 || idx < 0 || idx >= _count) { crumb = default; return false; }
            crumb = _crumbs[(_start + idx) % Capacity];
            return true;
        }

        /// <summary>Serial of the crumb nearest to pos within maxDist (prefers newer crumbs on ties), -1 if none.</summary>
        public int FindNearest(Vector3 pos, float maxDist)
        {
            int best = -1; float bestD = maxDist * maxDist;
            for (int i = 0; i < _count; i++)
            {
                var c = _crumbs[(_start + i) % Capacity];
                float d = (c.Position - pos).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = c.Serial; }
            }
            return best;
        }
    }
}
