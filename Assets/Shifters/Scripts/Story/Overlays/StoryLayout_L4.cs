// =====================================================================================
//  SHIFTERS - StoryLayout_L4.cs   [owned by STORY_KIT]   CAMPAIGN §8.3 row L4 (the IQ test)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class StoryLayout_L4
    {
        public static void Build(StoryLayoutUtil u)
        {
            var spawn = StoryLayoutUtil.Mark("spawn");
            Vector3 s0 = spawn != null ? spawn.position : Vector3.zero;
            var fwd = spawn != null ? StoryLayoutUtil.Flat(spawn.forward) : Vector3.forward;
            var path = StoryLayoutUtil.Mark("S2.Path");
            if (path != null) u.Billboard("metv.S2", path.position + new Vector3(0f, 10f, 8f), path.position - fwd * 8f + Vector3.up * 1.6f, 7f);
            var t1 = StoryLayoutUtil.Mark("S1.Terminal");
            u.TauntNear("taunt.S1", t1 != null ? t1.position : s0 + fwd * 8f, "L4.S1", s0);
            if (path != null) u.TauntNear("taunt.S2", path.position, "L4.S2", path.position - fwd * 8f);
            var latch = StoryLayoutUtil.Mark("S4.Latch");
            u.TauntAt("S4.Latch", "L4.S4.Latch", latch != null ? latch.position - fwd * 8f : s0);
            for (int i = 1; i <= 4; i++) u.PosterAtCheckpoint(i, StoryKitLines.Poster(i * 2 + 8), 1);
            // brain jars relabelled
            int jars = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                if (jars >= 6 || r == null || !r.gameObject.name.StartsWith("BrainJar")) continue;
                var jt = r.transform;
                var cam = s0;
                var lp = jt.position + Vector3.up * 1.55f;
                u.Add("brainjar." + jars, Label(u.Root, lp, cam, "MAX'S BACKUP BRAIN\n(DO NOT LICK)"));
                jars++;
            }
            if (jars == 0) u.Skip("brainjar(none found)");
            var exit = StoryLayoutUtil.Mark("exit");
            if (exit != null)
            {
                var ef = StoryLayoutUtil.Flat(exit.forward);
                var viewer = exit.position - ef * 6f + Vector3.up * 1.6f;
                u.Sign("sign.vault", exit.position + Vector3.up * 3.4f - ef * 0.6f, viewer, "VAULT LEVEL - DOWN");
            }
        }

        static GameObject Label(Transform root, Vector3 pos, Vector3 viewer, string text)
        {
            var rot = EnvKit.FacingViewerAt(pos, viewer);
            var go = Kit.Make("BrainJarLabel", root, pos, EnvKit.ViewToFace(rot));
            Kit.BoxVisual(go.transform, "Tag", Vector3.zero, new Vector3(1.1f, 0.34f, 0.02f), MatKey.SignWhite, 0.005f, null, false);
            EnvKit.AddText(go.transform, text, new Vector3(0f, 0f, 0.014f), new Vector2(1.02f, 0.3f), StoryKitMeshes.Hex(0xC8202E), ShiftersFonts.Display, 0.11f);
            return go;
        }
    }
}
