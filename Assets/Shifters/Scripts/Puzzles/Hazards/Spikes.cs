using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Spike bed. Static (always out) or cycling/retractable (spikes sink into the base when inactive, poke up as a
    /// warning). Extended spikes deal Damage and bounce victims up and away.
    /// </summary>
    public class Spikes : PuzzleHazard
    {
        public Vector2 Size = new Vector2(2f, 2f);
        public float Damage = 40f;
        public float SpikeHeight = 0.45f;

        [Header("Parts")]
        public Transform SpikeRoot;
        public Revealable Reveal;

        public int Hits { get; private set; }
        readonly PuzzleCooldown _cool = new PuzzleCooldown(0.8f);
        float _extend, _target;

        void Start()
        {
            if (Reveal != null && Cycles) Reveal.DynamicLabel = v => { float t = TimeToSwitch; return (IsActive ? "SPIKES DOWN IN " : "SPIKES UP IN ") + Mathf.Max(0f, t).ToString("0.0") + " s"; };
        }

        protected override void OnHazardState(bool active, bool warning, bool silent)
        {
            _target = active ? 1f : warning ? 0.25f : 0f;
            if (silent && !warning) _extend = _target;
            if (!silent && active) Sfx.At(SfxId.MetalHit, transform.position, 0.8f, 1.3f);
        }

        protected override void HazardUpdate()
        {
            _extend = Mathf.MoveTowards(_extend, _target, Time.deltaTime * (_target > _extend ? 10f : 3f));
            if (SpikeRoot != null) SpikeRoot.localPosition = new Vector3(0f, (_extend - 1f) * SpikeHeight, 0f);
            if (_extend < 0.6f) return;
            var all = ShifterCharacter.All;
            var center = new Vector3(0f, 0.1f + SpikeHeight * 0.5f, 0f);
            var half = new Vector3(Size.x * 0.5f, SpikeHeight * 0.5f + 0.1f, Size.y * 0.5f);
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Condition == CharacterCondition.Carried) continue;
                if (!PuzzleUtil.InBox(transform, center, half + new Vector3(c.Radius * 0.4f, 0f, c.Radius * 0.4f), c.Feet + Vector3.up * 0.05f)) continue;
                if (!_cool.Ready(c)) continue;
                Hits++;
                var away = c.transform.position - transform.position; away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
                c.Damage(Damage, away * 4f + Vector3.up * 8f, "sat on the spikes");
                Sfx.At(SfxId.Hurt, c.Center);
                Fx.FloatingText(c.HeadTop, "OUCH! -" + Mathf.RoundToInt(Damage), UIColors.Danger);
            }
        }
    }
}
