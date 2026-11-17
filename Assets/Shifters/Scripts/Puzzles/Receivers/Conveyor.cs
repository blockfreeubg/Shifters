using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Conveyor belt running along local +Z (Direction = -1 reverses) while powered. Characters in control get the
    /// belt speed through ISurfaceVelocity (CharacterMotor adds it); props and limp characters (flopped, KO'd, thrown)
    /// are dragged by setting their planar rigidbody velocity in FixedUpdate. Slats scroll so you can see it move.
    /// </summary>
    public class Conveyor : SignalReceiver, ISurfaceVelocity
    {
        public float Speed = 2.5f;
        [Tooltip("+1 = local +Z, -1 = local -Z.")] public float Direction = 1f;
        public float Length = 6f;
        [Tooltip("How fast props reach the belt speed (m/s^2).")] public float Grip = 30f;

        [Header("Parts")]
        public Transform[] Slats = new Transform[0];
        public float SlatSpacing = 0.5f;
        public PuzzleLamp Lamp;

        public bool Running => Powered;
        public Vector3 BeltVelocity => Running ? transform.forward * (Speed * Mathf.Sign(Direction == 0f ? 1f : Direction)) : Vector3.zero;

        readonly HashSet<Rigidbody> _contacts = new HashSet<Rigidbody>();
        readonly List<Rigidbody> _work = new List<Rigidbody>();
        float _scroll, _hum;

        public Vector3 GetSurfaceVelocity(Vector3 worldPoint) => BeltVelocity;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (Lamp != null) Lamp.Set(powered ? PuzzleLampColor.Blue : PuzzleLampColor.Red);
            if (!initial) Sfx.At(SfxId.Conveyor, transform.position, 0.6f, powered ? 1f : 0.7f);
        }

        void OnCollisionStay(Collision c)
        {
            var rb = c.rigidbody;
            if (rb == null || rb.isKinematic) return;
            if (rb.TryGetComponent(out ShifterCharacter ch) && (ch.Condition == CharacterCondition.Normal || ch.Condition == CharacterCondition.Interacting) && ch.IsAlive) return;
            _contacts.Add(rb);
        }

        void FixedUpdate()
        {
            if (_contacts.Count == 0) return;
            _work.Clear();
            foreach (var rb in _contacts) _work.Add(rb);
            _contacts.Clear();
            if (!Running) return;
            var target = BeltVelocity;
            float step = Grip * Time.fixedDeltaTime;
            for (int i = 0; i < _work.Count; i++)
            {
                var rb = _work[i];
                if (rb == null || rb.isKinematic) continue;
                var v = rb.linearVelocity;
                var planar = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), new Vector3(target.x, 0f, target.z), step);
                rb.linearVelocity = new Vector3(planar.x, v.y, planar.z);
                rb.WakeUp();
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!Running || Slats.Length == 0) return;
            float dir = Direction < 0f ? -1f : 1f;
            _scroll = Mathf.Repeat(_scroll + Speed * dir * Time.deltaTime, SlatSpacing);
            float start = -Length * 0.5f;
            for (int i = 0; i < Slats.Length; i++)
            {
                var s = Slats[i];
                if (s == null) continue;
                float z = Mathf.Repeat(i * SlatSpacing + _scroll, Slats.Length * SlatSpacing);
                var p = s.localPosition;
                p.z = start + z;
                s.localPosition = p;
            }
            _hum -= Time.deltaTime;
            if (_hum <= 0f) { _hum = 0.9f; Sfx.At(SfxId.Conveyor, transform.position, 0.25f); }
        }
    }
}
