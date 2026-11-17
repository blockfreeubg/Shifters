// =====================================================================================
//  SHIFTERS - StoryLayout_L3.cs   [owned by STORY_KIT]   CAMPAIGN §8.3 row L3 (CH05 overlay, the volunteer chair)
//  + the §10.3 #1 flicker-statue scare at checkpoint.2 (a grey Max statue in the void is closer each flicker; on the
//  fourth flicker it wears sunglasses) and Exhibit A on the exit.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class StoryLayout_L3
    {
        public static void Build(StoryLayoutUtil u)
        {
            var spawn = StoryLayoutUtil.Mark("spawn");
            Vector3 s0 = spawn != null ? spawn.position : Vector3.zero;
            var fwd = spawn != null ? StoryLayoutUtil.Flat(spawn.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var bot = StoryLayoutUtil.Mark("S1.Bot");
            var s1 = bot != null ? bot.position : s0 + fwd * 15f;
            u.Billboard("metv.S1", s1 + new Vector3(0f, 10f, 4f), s1 - fwd * 10f + Vector3.up * 1.6f, 7f);
            var s1Plate = StoryLayoutUtil.Mark("S1.Plate");
            u.TauntNear("taunt.S1", s1Plate != null ? s1Plate.position : s1, "L3.S1", s0);
            var chair = StoryLayoutUtil.Mark("S3.Chair");
            if (chair != null) u.TauntNear("taunt.S3.Chair", chair.position, "ch05.chair", chair.position - fwd * 8f, 4f);
            var s4 = StoryLayoutUtil.Mark("S4.Plate");
            if (s4 != null) u.TauntNear("taunt.S4", s4.position, "L3.S4", s4.position - fwd * 8f);
            for (int i = 1; i <= 3; i++) u.PosterAtCheckpoint(i, StoryKitLines.Poster(i * 5 + 2), 1);
            // §10.3 #1 flicker statue at checkpoint 2
            var cp2 = StoryLayoutUtil.Mark("checkpoint.2");
            if (cp2 != null)
            {
                var f2 = StoryLayoutUtil.Flat(cp2.forward);
                var r2 = Vector3.Cross(Vector3.up, f2);
                // in the void on the side of the checkpoint, approaching the pad
                float side = PickVoidSide(cp2.position, r2);
                var far = cp2.position + r2 * side * 34f + f2 * 10f + Vector3.down * 3f;
                var near = cp2.position + r2 * side * 16f + f2 * 3f + Vector3.down * 2f;
                var positions = new Vector3[4];
                for (int i = 0; i < 4; i++) positions[i] = Vector3.Lerp(far, near, i / 3f);
                var statue = StoryKit.VillainStatue(null, Vector3.zero, EnvKit.FacingViewerAt(far, cp2.position), 4.5f, 0);
                var glasses = StoryKit.StatueSunglasses(statue);
                var reveal = ScareKit.FlickerReveal(u.Root, positions, 0.6f, "ch05.flickerStatue", statue, glasses);
                var view = reveal.GetComponent<FlickerRevealView>();
                if (view != null)
                {
                    view.ArmRadius = 5f; view.ArmCenter = cp2.position;
                    if (view.PunchText != null) { view.PunchText.text = "COOL."; view.PunchText.transform.position = near + Vector3.up * 8.6f; view.PunchText.transform.rotation = EnvKit.ViewToFace(EnvKit.FacingViewerAt(near, cp2.position)) * Quaternion.Euler(0f, 180f, 0f); }
                }
                u.Add("scare.flickerStatue", reveal);
            }
            else u.Skip("scare.flickerStatue(no cp2)");
            // Exhibit A rides the exit elevator
            var exit = StoryLayoutUtil.Mark("exit");
            if (exit != null && StoryLayoutUtil.FindSpot(exit.position, new Vector3(0.4f, 0.9f, 0.4f), new[] { 2f, 2.8f, 3.6f }, exit.position - StoryLayoutUtil.Flat(exit.forward) * 4f, out var g))
            {
                var ex = StoryKit.ExhibitProp(u.Root, g, "A");
                ex.transform.rotation = EnvKit.ViewToFace(EnvKit.FacingViewerAt(g, exit.position - StoryLayoutUtil.Flat(exit.forward) * 4f));
                u.Add("exhibit.A", ex);
            }
            else u.Skip("exhibit.A");
        }

        /// <summary>+1 or -1: the side of the checkpoint with no floor (the void) within 20 m.</summary>
        static float PickVoidSide(Vector3 cp, Vector3 right)
        {
            bool rightFloor = StoryLayoutUtil.Ground(cp + right * 16f, out _, 3f, 12f);
            bool leftFloor = StoryLayoutUtil.Ground(cp - right * 16f, out _, 3f, 12f);
            if (rightFloor && !leftFloor) return -1f;
            return 1f;
        }
    }
}
