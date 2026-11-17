// =====================================================================================
//  SHIFTERS - WeightSensor.cs   [CONTRACT FILE - implemented by the architect]
//  Measures the total EffectiveMass resting on 'Surfaces' (directly or stacked, via the
//  WeightBody support graph). Used by PressurePlate, PulleyLift, WeightBalance, living bridge.
//  Put it on the plate/platform; Surfaces = the collider(s) you stand on (default: own colliders).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class WeightSensor : MonoBehaviour
    {
        [Tooltip("Colliders whose top surface is measured. Empty = non-trigger colliders on this GameObject and children.")]
        public Collider[] Surfaces = new Collider[0];
        [Tooltip("Mass changes smaller than this (kg) don't raise MassChanged.")] public float ChangeThreshold = 0.5f;

        public float CurrentMass { get; protected set; }
        public IReadOnlyList<WeightBody> Bodies => _bodies;
        protected readonly List<WeightBody> _bodies = new List<WeightBody>();
        public event Action<float> MassChanged;
        protected void InvokeMassChanged(float m) => MassChanged?.Invoke(m);

        readonly HashSet<Rigidbody> _bodySet = new HashSet<Rigidbody>();
        readonly HashSet<WeightBody> _members = new HashSet<WeightBody>();
        float _reportedMass;

        protected virtual void Awake()
        {
            if (Surfaces == null || Surfaces.Length == 0)
            {
                var list = new List<Collider>();
                foreach (var c in GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) list.Add(c);
                Surfaces = list.ToArray();
            }
        }

        /// <summary>Is this character currently counted on the sensor?</summary>
        public bool Contains(ShifterCharacter c)
        {
            if (c == null || c.Weight == null) return false;
            return _members.Contains(c.Weight);
        }

        protected virtual void FixedUpdate() => Measure();

        /// <summary>Recompute now (also called every FixedUpdate).</summary>
        public float Measure()
        {
            _bodies.Clear(); _members.Clear(); _bodySet.Clear();
            var all = WeightBody.All;
            // direct contacts
            for (int i = 0; i < all.Count; i++)
            {
                var wb = all[i];
                if (wb == null || !wb.Counts) continue;
                if (wb.RestsOn(Surfaces, _bodySet) && _members.Add(wb)) { _bodies.Add(wb); if (wb.Body != null) _bodySet.Add(wb.Body); }
            }
            // stacked bodies (support graph), bounded depth
            for (int depth = 0; depth < 6; depth++)
            {
                bool added = false;
                for (int i = 0; i < all.Count; i++)
                {
                    var wb = all[i];
                    if (wb == null || !wb.Counts || _members.Contains(wb)) continue;
                    var sb = wb.SupportBody;
                    if (sb != null && _bodySet.Contains(sb) && sb != wb.Body)
                    {
                        _members.Add(wb); _bodies.Add(wb); if (wb.Body != null) _bodySet.Add(wb.Body); added = true;
                    }
                }
                if (!added) break;
            }
            float m = 0f;
            for (int i = 0; i < _bodies.Count; i++) m += _bodies[i].EffectiveMass;
            CurrentMass = m;
            if (Mathf.Abs(m - _reportedMass) >= ChangeThreshold) { _reportedMass = m; MassChanged?.Invoke(m); }
            return m;
        }
    }
}
