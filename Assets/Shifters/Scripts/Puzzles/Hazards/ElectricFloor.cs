using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Electrified floor tile: grid lines glow yellow while warning, blue while live. Anyone standing on it while live
    /// takes Damage with a comic hop (tumble). Cycling by default; can be signal-controlled like any hazard.
    /// </summary>
    public class ElectricFloor : PuzzleHazard
    {
        public Vector2 Size = new Vector2(4f, 4f);
        public float Damage = 25f;
        public float HopSpeed = 7f;

        [Header("Parts")]
        public Renderer[] Lines = new Renderer[0];
        public Material OffMaterial;
        public Material WarnMaterial;
        public Material LiveMaterial;
        public Revealable Reveal;

        public int Zaps { get; private set; }
        readonly PuzzleCooldown _cool = new PuzzleCooldown(0.6f);
        float _spark;

        void Start()
        {
            if (Reveal != null) Reveal.DynamicLabel = v => { float t = TimeToSwitch; return t < 0f ? (IsActive ? "LIVE FLOOR" : "FLOOR SAFE") : (IsActive ? "SAFE IN " : "LIVE IN ") + t.ToString("0.0") + " s"; };
        }

        protected override void OnHazardState(bool active, bool warning, bool silent)
        {
            SetLines(active ? LiveMaterial : warning ? WarnMaterial : OffMaterial);
            if (!silent && active) Sfx.At(SfxId.Sizzle, transform.position, 0.8f, 1.2f);
        }

        void SetLines(Material m)
        {
            if (m == null) return;
            for (int i = 0; i < Lines.Length; i++) if (Lines[i] != null) Lines[i].sharedMaterial = m;
        }

        protected override void HazardUpdate()
        {
            if (IsWarning) SetLines(Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? WarnMaterial : OffMaterial);
            if (!IsActive) return;
            _spark -= Time.deltaTime;
            if (_spark <= 0f)
            {
                _spark = 0.15f;
                var p = transform.TransformPoint(new Vector3(Random.Range(-0.5f, 0.5f) * Size.x, 0.08f, Random.Range(-0.5f, 0.5f) * Size.y));
                Fx.Sparks(p, Vector3.up, 5, UIColors.Info);
            }
            var all = ShifterCharacter.All;
            var center = new Vector3(0f, 0.45f, 0f);
            var half = new Vector3(Size.x * 0.5f, 0.45f, Size.y * 0.5f);
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Condition == CharacterCondition.Carried) continue;
                if (!PuzzleUtil.InBox(transform, center, half + new Vector3(c.Radius * 0.5f, 0f, c.Radius * 0.5f), c.Feet + Vector3.up * 0.05f)) continue;
                if (!_cool.Ready(c)) continue;
                Zaps++;
                c.Damage(Damage, Vector3.up * HopSpeed, "got electrocuted");
                Sfx.At(SfxId.LaserZap, c.Center, 1f, 0.7f);
                Sfx.At(SfxId.Sizzle, c.Center);
                Fx.Sparks(c.Feet, Vector3.up, 18, UIColors.Info);
                Fx.FloatingText(c.HeadTop, "BZZZT!", UIColors.Info);
            }
        }
    }
}
