using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Pressure plate that needs a weight inside [RequiredKg, MaxKg] ("200-270 KG"). Too much weight blinks the lamp,
    /// shows "TOO HEAVY" and plays a rate-limited denied buzz + toast.
    /// </summary>
    public class WeightRangePlate : PressurePlate
    {
        public float MaxKg = 270f;
        float _lastToast;

        protected override bool Evaluate(float kg) => kg >= RequiredKg - 0.5f && kg <= MaxKg + 0.5f;
        public override string RequirementString() => Mathf.RoundToInt(RequiredKg) + "–" + Mathf.RoundToInt(MaxKg) + " KG";

        protected override int LoadMode(float kg) => kg > MaxKg + 0.5f ? 2 : base.LoadMode(kg);

        protected override void OnLoadModeShown(int mode)
        {
            if (mode != 2 || !Application.isPlaying) return;
            if (!PuzzleUtil.RateLimit(ref _lastToast, 3f)) return;
            Sfx.At(SfxId.Denied, transform.position);
            Fx.FloatingText(transform.position + Vector3.up * 2f, "TOO HEAVY!", UIColors.Accent);
            GameEvents.RaiseToast("Too heavy for this plate (max " + Mathf.RoundToInt(MaxKg) + " KG)", UIColors.Accent);
        }
    }
}
