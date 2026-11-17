// =====================================================================================
//  SHIFTERS - StoryLayout_L5.cs   [owned by STORY_KIT]   CAMPAIGN §8.3 row L5 (the tower demolition)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class StoryLayout_L5
    {
        public static void Build(StoryLayoutUtil u)
        {
            var spawn = StoryLayoutUtil.Mark("spawn");
            Vector3 s0 = spawn != null ? spawn.position : Vector3.zero;
            var fwd = spawn != null ? StoryLayoutUtil.Flat(spawn.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            u.Billboard("metv.S0", s0 + fwd * 16f + Vector3.up * 12f, s0 + Vector3.up * 1.6f, 8f);
            u.TauntNear("taunt.S0", s0 + fwd * 5f, "L5.S0", s0);
            var lever = StoryLayoutUtil.Mark("S2.Lever");
            u.TauntNear("taunt.S2", lever != null ? lever.position : s0 + fwd * 30f, "L5.S2", lever != null ? lever.position - fwd * 8f : s0);
            var lift = StoryLayoutUtil.Mark("S4.Lift");
            if (lift != null) u.TauntNear("taunt.S4", lift.position, "L5.S4", lift.position - fwd * 8f);
            for (int i = 1; i <= 4; i++) u.PosterAtCheckpoint(i, StoryKitLines.Poster(i * 3 + 11), 1);
            var pod = StoryLayoutUtil.Mark("S5.Pod");
            if (pod != null)
            {
                var pf = StoryLayoutUtil.Flat(pod.forward);
                u.Sign("sign.pod", pod.position + Vector3.up * 3.6f, pod.position - pf * 7f + Vector3.up * 1.6f, "EXECUTIVE ESCAPE POD\n1 VIP + EGO");
            }
            // toppled statue (stage 3) on free floor beside checkpoint 1, else in the void by S0
            var cp1 = StoryLayoutUtil.Mark("checkpoint.1");
            // the toppled figure lies up to ~5 m in front of its plinth (toward cp1): keep that footprint off the posters
            System.Func<Vector3, bool> clear = p =>
            {
                var d = StoryLayoutUtil.Flat(cp1.position - p);
                var fb = new Bounds(p + Vector3.up * 0.9f, new Vector3(2.2f, 1.8f, 2.2f));
                fb.Encapsulate(new Bounds(p + d * 5f + Vector3.up * 0.9f, new Vector3(2.2f, 1.8f, 2.2f)));
                return !u.OverlapsPlaced(fb);
            };
            if (cp1 != null && StoryLayoutUtil.FindSpot(cp1.position, new Vector3(1.4f, 0.8f, 2.4f), new[] { 6f, 7.5f, 9f, 10.5f }, cp1.position + right * 8f, out var g, null, 2.5f, clear))
                u.Statue("statue.toppled", g, cp1.position, 2.6f, 3);
            else u.Statue("statue.toppled", s0 - right * 16f + fwd * 8f + Vector3.down * 3f, s0 + Vector3.up * 1.6f, 4f, 3);
        }
    }
}
