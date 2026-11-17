using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Two emitter posts with glowing beams between them (along local X). Active beams deal Damage + knockback away
    /// from the beam plane (enough to tumble) and zap props. Flickers WarningTime seconds before turning on.
    /// Always on, cycling (CycleOn/CycleOff/Phase) or signal-controlled (Inputs; Invert = "plate turns lasers off").
    /// The trigger collider must be on this GameObject.
    /// </summary>
    public class LaserBarrier : PuzzleHazard
    {
        public float Damage = 35f;
        public float Knockback = 9f;
        public float Width = 3f;

        [Header("Parts")]
        public Renderer[] Beams = new Renderer[0];
        public Collider Zone;
        public PuzzleLamp[] EmitterLamps = new PuzzleLamp[0];
        public Revealable Reveal;

        public int HitCount { get; private set; }
        readonly PuzzleCooldown _cool = new PuzzleCooldown(0.5f);
        float _hum;

        void Start()
        {
            if (Reveal != null) Reveal.DynamicLabel = v =>
            {
                float t = TimeToSwitch;
                return t < 0f ? (IsActive ? "LASER - DON'T" : "LASER OFF") : (IsActive ? "OFF IN " : "ON IN ") + t.ToString("0.0") + " s";
            };
        }

        protected override void OnHazardState(bool active, bool warning, bool silent)
        {
            SetBeams(active, 1f);
            if (Zone != null) Zone.enabled = active;
            foreach (var l in EmitterLamps) if (l != null) l.Set(active ? PuzzleLampColor.Red : warning ? PuzzleLampColor.Yellow : PuzzleLampColor.Off);
            if (!silent && active) Sfx.At(SfxId.LaserZap, transform.position + Vector3.up, 0.5f, 1.5f);
        }

        void SetBeams(bool on, float thickness)
        {
            for (int i = 0; i < Beams.Length; i++)
            {
                var b = Beams[i];
                if (b == null) continue;
                b.enabled = on;
                var s = b.transform.localScale;
                b.transform.localScale = new Vector3(thickness, s.y, thickness);
            }
        }

        protected override void HazardUpdate()
        {
            if (IsWarning) SetBeams(Mathf.Repeat(Time.time * 14f, 1f) < 0.45f, 0.35f);
            if (IsActive)
            {
                _hum -= Time.deltaTime;
                if (_hum <= 0f) { _hum = 1.2f; Sfx.At(SfxId.LaserZap, transform.position + Vector3.up, 0.12f, 0.6f); }
            }
        }

        void OnTriggerStay(Collider other)
        {
            if (!IsActive) return;
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var side = Vector3.Dot(rb.position - transform.position, transform.forward) >= 0f ? transform.forward : -transform.forward;
            if (rb.TryGetComponent(out ShifterCharacter c))
            {
                if (!c.IsAlive || c.Condition == CharacterCondition.Carried || !_cool.Ready(c)) return;
                HitCount++;
                c.Damage(Damage, side * Knockback + Vector3.up * 4f, "got lasered");
                Sfx.At(SfxId.LaserZap, c.Center);
                Fx.Sparks(c.Center, side, 16, MaterialLibrary.DefaultColor(MatKey.Laser));
                Fx.FloatingText(c.HeadTop + Vector3.up * 0.3f, "ZAP! -" + Mathf.RoundToInt(Damage), UIColors.Danger);
            }
            else if (!rb.isKinematic && _cool.Ready(rb))
            {
                rb.AddForce(side * 4f + Vector3.up * 2f, ForceMode.VelocityChange);
                Fx.Sparks(other.ClosestPoint(transform.position), side, 8, MaterialLibrary.DefaultColor(MatKey.Laser));
                Sfx.At(SfxId.Sizzle, rb.position, 0.5f);
            }
        }
    }
}
