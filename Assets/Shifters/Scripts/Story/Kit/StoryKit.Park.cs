// =====================================================================================
//  SHIFTERS - StoryKit.Park.cs   [owned by STORY_KIT]
//  ME-morial Park props (CH10): Tombstone, TombstoneRow (filled at runtime from CampaignDirector.LostLog), GhostTrio.
//  Facing / pos conventions: see StoryKit.cs header. Collider-free.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static partial class StoryKit
    {
        public const float TombWidth = 0.8f, TombHeight = 1.25f, TombSpacing = 1.5f;

        /// <summary>Rounded grey tombstone (0.8 x 1.25 m incl. a mossy base) with engraved text on the front
        /// (§24.5 texts in StoryKitLines.Tombstones). pos = floor point. Collider-free. The text is child "Text".</summary>
        public static GameObject Tombstone(Transform p, Vector3 pos, Quaternion? rot, string text)
        {
            var root = Root("Tombstone", p, pos, rot);
            BuildTomb(root.transform, text);
            return root;
        }

        static TextMeshPro BuildTomb(Transform t, string text)
        {
            Part(t, "Stone", StoryKitMeshes.Tombstone(TombWidth, TombHeight), Painted, Vector3.zero);
            Text(t, "R.I.P.", new Vector3(0f, TombHeight - 0.2f, 0.115f), new Vector2(0.5f, 0.12f), StoryKitMeshes.Hex(0x3A3D42), ShiftersFonts.Display, 0.1f);
            var tmp = Text(t, text ?? "", new Vector3(0f, 0.62f, 0.115f), new Vector2(TombWidth * 0.84f, 0.7f), StoryKitMeshes.Hex(0x2A2D33), ShiftersFonts.Label, 0.085f);
            return tmp;
        }

        /// <summary>A row of 'slots' tombstones 1.5 m apart along the row's local X (centered on pos). At runtime
        /// TombstoneRowView fills them from CampaignDirector.LostLog ("HERE LIES {NAME}. {CAUSE}. SHIFT {NN}. REPRINTED AS
        /// {CLONE}."); without Lost interns slot 0 reads "HERE LIES NOBODY. GOOD JOB. - PERCY", the rest "RESERVED FOR YOU".
        /// TombstoneRowView.Fill(texts) sets them explicitly. Collider-free.</summary>
        public static GameObject TombstoneRow(Transform p, Vector3 pos, Quaternion? rot, int slots)
        {
            slots = Mathf.Clamp(slots, 1, 24);
            var root = Root("TombstoneRow", p, pos, rot);
            var texts = new TextMeshPro[slots];
            for (int i = 0; i < slots; i++)
            {
                // the reader looks along -Z of the root (front = +Z), so the reader's left->right runs along -X
                float x = ((slots - 1) * 0.5f - i) * TombSpacing;
                var slot = Kit.Make("Slot_" + i, root.transform, new Vector3(x, 0f, 0f), Quaternion.Euler(0f, (i % 3 - 1) * 3f, (i % 2 == 0 ? 1.5f : -1f)));
                texts[i] = BuildTomb(slot.transform, i == 0 ? StoryKitLines.TombNobody : StoryKitLines.TombReserved);
            }
            var v = root.AddComponent<TombstoneRowView>();
            v.Texts = texts;
            return root;
        }

        /// <summary>Three bobbing bedsheet ghosts (hologram), 1.2 m apart, each holding a little placard with tags[i]
        /// (e.g. "MARKETING", "BOWLING LEAGUE", "THE BAND"). pos = floor point of the middle ghost. Collider-free.</summary>
        public static GameObject GhostTrio(Transform p, Vector3 pos, Quaternion? rot, string[] tags)
        {
            var root = Root("GhostTrio", p, pos, rot);
            global::Shifters.GhostTrio vis = null;
            try { vis = global::Shifters.GhostTrio.Create(root.transform, Vector3.zero, Quaternion.identity, tags); } catch (System.Exception e) { Debug.LogWarning("[StoryKit] GhostTrio: " + e.Message); }
            if (vis != null && vis.Ghosts != null && vis.Ghosts.Length > 0) return root;
            if (vis != null) Kit.SafeDestroy(vis.gameObject);
            var mat = HoloMaterial(new Color(0.78f, 0.95f, 1f, 0.7f));
            var ghosts = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var g = Kit.Make("Ghost_" + i, root.transform, new Vector3((1 - i) * 1.2f, 0.25f, 0f), Quaternion.Euler(0f, (1 - i) * 12f, 0f));
                Part(g.transform, "Sheet", StoryKitMeshes.Ghost(Color.white), mat, Vector3.zero, null, Vector3.one * (i == 1 ? 1f : 0.9f), false);
                string tag = tags != null && i < tags.Length ? tags[i] : null;
                if (!string.IsNullOrEmpty(tag))
                {
                    Kit.BoxVisual(g.transform, "Placard", new Vector3(0f, 0.62f, 0.4f), new Vector3(0.62f, 0.26f, 0.02f), MatKey.Cardboard, 0.005f, null, false);
                    Text(g.transform, tag.ToUpperInvariant(), new Vector3(0f, 0.62f, 0.412f), new Vector2(0.56f, 0.2f), StoryKitMeshes.Hex(0x2A2D33), ShiftersFonts.Display, 0.09f);
                }
                ghosts[i] = g.transform;
            }
            var v = root.AddComponent<GhostTrioView>();
            v.Ghosts = ghosts;
            return root;
        }
    }
}
