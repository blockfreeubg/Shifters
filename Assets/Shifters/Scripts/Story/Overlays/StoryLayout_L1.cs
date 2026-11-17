// =====================================================================================
//  SHIFTERS - StoryLayout_L1.cs   [owned by STORY_KIT]   CAMPAIGN §8.3 row L1 (SHIFT 01 ORIENTATION DAY)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class StoryLayout_L1
    {
        public static void Build(StoryLayoutUtil u)
        {
            var spawn = StoryLayoutUtil.Mark("spawn");
            Vector3 s0 = spawn != null ? spawn.position : Vector3.zero;
            var fwd = spawn != null ? StoryLayoutUtil.Flat(spawn.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            // ME-TV billboards in the void
            u.Billboard("metv.spawn", s0 + new Vector3(0f, 16f, 60f), s0 + Vector3.up * 1.6f, 9f);
            var d2 = StoryLayoutUtil.Mark("S2.D2");
            if (d2 != null) u.Billboard("metv.S2", d2.position + new Vector3(0f, 12f, 10f), d2.position - fwd * 8f + Vector3.up * 1.6f, 7f);
            // hologram taunts
            u.TauntAt("S1.D1", "L1.S1.D1", s0);
            u.TauntAt("S2.D2", "L1.S2.D2", d2 != null ? d2.position - fwd * 10f : s0);
            var d6 = StoryLayoutUtil.Mark("S6.D6");
            u.TauntAt("S6.D6", "L1.S6.D6", d6 != null ? d6.position - fwd * 10f : s0);
            var gate = StoryLayoutUtil.Mark("S7.Gate");
            u.TauntAt("S7.Gate", "L1.S7.Gate", gate != null ? gate.position - fwd * 10f : s0);
            // posters beside checkpoints
            for (int i = 1; i <= 5; i++) u.PosterAtCheckpoint(i, StoryKitLines.Poster(i * 3 - 2), 1);
            // Percy in the S0 corner (cold-open wink) + his first real line at checkpoint 5 (radio)
            u.PercyNear("percy.S0", s0 + fwd * 4f + right * 4f, s0 + Vector3.up * 0.2f, "percy.wink", "percy.L1.cp5");
            var cp5 = StoryLayoutUtil.Mark("checkpoint.5");
            if (cp5 != null) u.Add("line.cp5", StoryKit.LineSpot(u.Root, cp5.position, 4f, "percy.L1.cp5", true, true).gameObject);
            // 6 m grey Max statue in the void beside S0 (stage 0)
            u.Statue("statue.S0", s0 - right * 17f + fwd * 10f + Vector3.down * 4f, s0 + Vector3.up * 1.6f, 6f, 0);
        }
    }
}
