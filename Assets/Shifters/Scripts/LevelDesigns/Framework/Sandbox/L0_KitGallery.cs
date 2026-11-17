// =====================================================================================
//  SHIFTERS - L0_KitGallery.cs   (BUILDER module - visual catalog of every kit factory)
//  Scene Assets/Shifters/Scenes/L0_KitGallery.unity (NOT in Build Settings): one default instance of every
//  placeable factory found by KitCatalog.Discover() on a labeled grid (same instances the prefab generator saves).
//  Failing factories get a red "FAILED" label (and a build warning). Marks: "gallery.<Kit>.<Method>".
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L0_KitGallery : ILevelBuilder, ILevelSceneInfo
    {
        public const int Index = 101;
        public const string SceneName = "L0_KitGallery";
        public const int Columns = 6;
        public const float Cell = 10f;

        public int LevelIndex => Index;

        public LevelInfo SceneInfo => new LevelInfo(Index, SceneName, "KIT GALLERY", "Every factory, default settings",
            "Visual catalog of all kit factories.", LevelTheme.Training, MusicId.Training, "Look around.");

        public void Build(LevelBuildContext ctx)
        {
            var factories = KitCatalog.Discover();
            int rows = Mathf.Max(1, (factories.Count + Columns - 1) / Columns);
            float width = Columns * Cell, depth = rows * Cell;
            ctx.Platform(ctx.Geometry, new Vector3(0f, 0f, depth * 0.5f - 4f), new Vector3(width + 4f, 1f, depth + 12f), MatKey.Floor, true, "GalleryFloor");
            ctx.SetSpawn(new Vector3(0f, 0f, -6f), 0f);
            ctx.Sign(ctx.Decor, new Vector3(0f, 3.2f, -1.5f), 0f, factories.Count > 0 ? "SHIFTERS KIT GALLERY: " + factories.Count + " FACTORIES" : "KIT GALLERY: NO KITS PUBLISHED YET", 0.45f);

            for (int i = 0; i < factories.Count; i++)
            {
                var f = factories[i];
                int col = i % Columns, row = i / Columns;
                var pos = new Vector3((col - (Columns - 1) * 0.5f) * Cell, 0f, 4f + row * Cell);
                var slot = Kit.Make("Slot_" + f.Id, RootFor(ctx, f.Category), pos).transform;
                Kit.BoxVisual(slot, "Tile", new Vector3(0f, -0.005f, 0f), new Vector3(Cell - 1.2f, 0.05f, Cell - 1.2f), MatKey.FloorAlt, 0.02f, null, false);
                var holder = Kit.Make("Instance", slot, Vector3.zero).transform;
                KitCatalog.SpawnDefault(f, holder, out string error);
                bool failed = error != null;
                if (failed) ctx.Warn("gallery: " + f.Id + " failed: " + error);
                ctx.Label(slot, new Vector3(0f, 0.45f, -Cell * 0.5f + 0.7f), Quaternion.identity, f.Id + (failed ? "  FAILED" : ""), 0.32f,
                    failed ? new Color(1f, 0.35f, 0.4f) : new Color(1f, 0.92f, 0.8f), Cell - 1f);
                ctx.Mark("gallery." + f.Id, slot.gameObject);
                ctx.Count(failed ? "galleryFailed" : "galleryOk");
            }
        }

        static Transform RootFor(LevelBuildContext ctx, string category)
        {
            switch (category)
            {
                case "Puzzles": return ctx.Puzzles;
                case "Props": return ctx.Props;
                case "Hazards": return ctx.Hazards;
                case "Enemies": return ctx.Enemies;
                case "Environment": return ctx.Decor;
                default: return ctx.Gameplay;
            }
        }
    }
}
