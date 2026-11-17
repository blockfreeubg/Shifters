// =====================================================================================
//  SHIFTERS - StoryLayout_L2.cs   [owned by STORY_KIT]   CAMPAIGN §8.3 row L2 (SHIFT 02 THE GREY PAINT FACTORY)
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class StoryLayout_L2
    {
        public static void Build(StoryLayoutUtil u)
        {
            var spawn = StoryLayoutUtil.Mark("spawn");
            Vector3 s0 = spawn != null ? spawn.position : Vector3.zero;
            var fwd = spawn != null ? StoryLayoutUtil.Flat(spawn.forward) : Vector3.forward;
            var scale = StoryLayoutUtil.Mark("S5.Scale");
            if (scale != null) u.Billboard("metv.S5", scale.position + new Vector3(0f, 11f, 6f), scale.position - fwd * 10f + Vector3.up * 1.6f, 7f);
            var panel = StoryLayoutUtil.Mark("S2.Panel");
            u.TauntAt("S2.Panel", "L2.S2.Panel", panel != null ? panel.position - fwd * 8f : s0);
            var pulley = StoryLayoutUtil.Mark("S4.Pulley");
            u.TauntAt("S4.Pulley", "L2.S4.Pulley", pulley != null ? pulley.position - fwd * 8f : s0);
            for (int i = 1; i <= 3; i++) u.PosterAtCheckpoint(i, StoryKitLines.Poster(i * 4 + 1), 1);
            var cp1 = StoryLayoutUtil.Mark("checkpoint.1");
            if (cp1 != null)
                u.PercyNear("percy.cp1", cp1.position + StoryLayoutUtil.Flat(cp1.forward) * 1.5f, cp1.position - StoryLayoutUtil.Flat(cp1.forward) * 2f,
                    "You're the new interns? Grab a coffee. Not me. Well... me.", "THE COFFEE MACHINE IS NOT YOUR FRIEND? Rude.", "percy.L1.cp5");
            // grey-paint barrels by the conveyor (decor)
            var conv = StoryLayoutUtil.Mark("S1.Conveyor");
            if (conv != null && StoryLayoutUtil.FindSpot(conv.position, new Vector3(0.9f, 0.6f, 0.5f), new[] { 3.5f, 4.5f, 5.5f }, s0, out var g))
                u.Add("barrels.S1", GreyPaintBarrels(u.Root, g, EnvKit.FacingViewerAt(g, s0)));
            else u.Skip("barrels.S1");
        }

        static GameObject GreyPaintBarrels(Transform p, Vector3 pos, Quaternion rot)
        {
            var root = Kit.Make("GreyPaintBarrels", p, pos, EnvKit.ViewToFace(rot));
            var grey = MaterialLibrary.Colored(StoryKitMeshes.Hex(0x7E838A), 0.5f);
            var mesh = ProcMesh.Cached("story_barrel", () => ProcMesh.Cylinder(0.3f, 0.9f, 16, true));
            var lid = ProcMesh.Cached("story_barrel_lid", () => ProcMesh.Cylinder(0.31f, 0.04f, 16, true));
            for (int i = 0; i < 3; i++)
            {
                var b = ProcMesh.Part("Barrel" + i, root.transform, mesh, Kit.Mat(MatKey.Barrel), new Vector3((i - 1) * 0.66f, 0.45f, i == 1 ? 0.12f : 0f));
                ProcMesh.Part("Paint", b.transform, lid, grey, new Vector3(0f, 0.46f, 0f));
                EnvKit.AddText(b.transform, "GREY\n#5A5F66", new Vector3(0f, 0f, 0.305f), new Vector2(0.42f, 0.3f), StoryKitMeshes.Hex(0x1E2430), ShiftersFonts.Display, 0.11f);
            }
            return root;
        }
    }
}
