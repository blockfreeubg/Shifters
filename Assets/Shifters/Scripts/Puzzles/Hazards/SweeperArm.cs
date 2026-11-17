using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Rotating hazard-striped bar (kinematic MoveRotation around the hub). Characters it touches get a big tangential
    /// tumble impulse (+ a little damage) - jump it or get bowled off the platform.
    /// </summary>
    public class SweeperArm : PuzzleHazard
    {
        [Tooltip("Degrees per second (sign = direction).")] public float DegreesPerSecond = 90f;
        [Tooltip("Total bar length (centered on the hub).")] public float Length = 6f;
        public float BarHeight = 0.6f;
        public float BarThickness = 0.45f;
        public float Impulse = 13f;
        public float Damage = 10f;

        [Header("Parts")]
        public MoverBody Arm;

        public int Hits { get; private set; }
        float _angle;
        readonly PuzzleCooldown _cool = new PuzzleCooldown(0.8f);

        protected override void OnHazardState(bool active, bool warning, bool silent) { }

        void FixedUpdate()
        {
            if (Arm == null || IsFrozen) return;
            if (IsActive) _angle = Mathf.Repeat(_angle + DegreesPerSecond * Time.fixedDeltaTime, 360f);
            Arm.RotateTo(transform.rotation * Quaternion.Euler(0f, _angle, 0f));
            Arm.MoveTo(transform.position);
            if (IsActive) CheckHits();
        }

        void CheckHits()
        {
            var rot = transform.rotation * Quaternion.Euler(0f, _angle, 0f);
            var axis = rot * Vector3.right;
            var hub = transform.position;
            float barY = hub.y + BarHeight;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Condition == CharacterCondition.Carried) continue;
                if (c.Feet.y > barY + BarThickness * 0.5f + 0.05f || c.HeadTop.y < barY - BarThickness * 0.5f) continue;
                var d = c.transform.position - hub; d.y = 0f;
                float along = Vector3.Dot(d, axis);
                if (Mathf.Abs(along) > Length * 0.5f + c.Radius) continue;
                var perp = d - axis * along;
                if (perp.magnitude > BarThickness * 0.5f + c.Radius + 0.05f) continue;
                if (!_cool.Ready(c)) continue;
                Hits++;
                var tangent = Vector3.Cross(Vector3.up, axis) * Mathf.Sign(DegreesPerSecond) * Mathf.Sign(along == 0f ? 1f : along);
                var outward = axis * Mathf.Sign(along == 0f ? 1f : along);
                c.Damage(Damage, tangent * Impulse + outward * 4f + Vector3.up * 6f, "got swept away");
                Sfx.At(SfxId.Bonk, c.Center);
                Sfx.At(SfxId.Whoosh, c.Center, 0.8f);
                Fx.FloatingText(c.HeadTop, "BONK!", UIColors.AccentYellow);
            }
        }
    }
}
