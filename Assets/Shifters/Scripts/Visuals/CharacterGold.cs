// =====================================================================================
//  SHIFTERS - CharacterGold.cs   (VISUALS module, campaign wave)
//  Solid-gold statue look for any built character rig (founders' statue, trophies, Max's
//  statues). Swaps every renderer's shared material for the shared Gold material (pupils,
//  mouth and brows a darker gold so the face still reads) and restores the originals.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class CharacterGold
    {
        static Material _dark;

        /// <summary>The body gold (MatKey.Gold of the current Kit theme; grey-immune once rendering's palettes land).</summary>
        public static Material BodyGold => MaterialLibrary.Get(MatKey.Gold, Kit.Theme);

        public static Material DarkGold
        {
            get
            {
                if (_dark == null) _dark = MaterialLibrary.Colored(new Color(0.55f, 0.38f, 0.08f), 0.6f);
                return _dark;
            }
        }

        public static void Apply(CharacterRig rig, bool on)
        {
            if (rig == null || rig.Renderers == null || rig.IsGold == on) return;
            var rs = rig.Renderers;
            if (on)
            {
                rig.SavedMats = new Material[rs.Length];
                var body = BodyGold; var dark = DarkGold;
                for (int i = 0; i < rs.Length; i++)
                {
                    var r = rs[i]; if (r == null) continue;
                    rig.SavedMats[i] = r.sharedMaterial;
                    string n = r.gameObject.name;
                    r.sharedMaterial = n.StartsWith("Pupil") || n == "Mouth" || n.StartsWith("Brow") || n == "CrownME" ? dark : body;
                }
            }
            else if (rig.SavedMats != null)
            {
                for (int i = 0; i < rs.Length && i < rig.SavedMats.Length; i++)
                    if (rs[i] != null && rig.SavedMats[i] != null) rs[i].sharedMaterial = rig.SavedMats[i];
                rig.SavedMats = null;
            }
            rig.IsGold = on;
            rig.Face?.RefreshRendererState();
        }
    }
}
