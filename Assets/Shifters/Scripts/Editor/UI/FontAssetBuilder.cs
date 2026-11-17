// =====================================================================================
//  SHIFTERS - FontAssetBuilder.cs   (UI agent, editor only)
//  Generates TextMeshPro SDF font assets from the OFL TTFs in Assets/Shifters/Fonts into
//  Assets/Shifters/Resources/Fonts:
//     <Name> SDF.asset           TMP_FontAsset (dynamic, ASCII + punctuation pre-populated)
//     <Name> SDF Atlas.asset     its atlas texture
//     <Name> SDF Material.mat    its default SDF material
//     Bungee-Regular SDF Logo.mat    outlined + underlay preset for the logo
//     Bungee-Regular SDF Shadow.mat  soft drop-shadow preset for big titles / banners
//  (presets are assets so their shader_feature variants ship in player builds)
//  Atlas + materials are written through ShiftersGeneratedAssets.SaveAsset and the font asset with the
//  same scheme (WriteFontAsset), so GUIDs (md5 of the path) and main fileIDs are identical in every
//  project copy: scenes built in different clones that reference these fonts stay valid after merging.
//  Idempotent: BuildAll() skips fonts already built in this layout ("SHIFTERS/Build Fonts" forces a rebuild).
//  Batchmode: -executeMethod Shifters.EditorTools.FontAssetBuilder.BuildAll -quit
// =====================================================================================
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Shifters.EditorTools
{
    public static class FontAssetBuilder
    {
        const string SourceFolder = "Assets/Shifters/Fonts";
        const string OutFolder = "Assets/Shifters/Resources/Fonts";
        static readonly string[] FontNames = { "Bungee-Regular", "Barlow-SemiBold", "Barlow-Black", "BarlowCondensed-Bold" };

        /// <summary>Characters pre-populated into every atlas (ASCII + common punctuation).</summary>
        public const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "·—–…×°’‘“”•©";

        public static string FontPath(string name) => OutFolder + "/" + name + " SDF.asset";
        public static string AtlasPath(string name) => OutFolder + "/" + name + " SDF Atlas.asset";
        public static string MaterialPath(string name) => OutFolder + "/" + name + " SDF Material.mat";
        public static string LogoPath(string name) => OutFolder + "/" + name + " SDF Logo.mat";
        public static string ShadowPath(string name) => OutFolder + "/" + name + " SDF Shadow.mat";

        /// <summary>Builds the fonts that are missing or not yet in the deterministic layout (builder entry point).</summary>
        public static void BuildAll() => Build(false);

        /// <summary>Rebuilds every font asset from scratch (GUIDs stay the same: they derive from the paths).</summary>
        [MenuItem("SHIFTERS/Build Fonts")]
        public static void RebuildAll() => Build(true);

        static void Build(bool force)
        {
            ShiftersGeneratedAssets.EnsureFolder(OutFolder);
            int built = 0, skipped = 0;
            foreach (var n in FontNames)
            {
                if (!force && IsUpToDate(n)) { skipped++; continue; }
                if (BuildFont(n)) built++;
            }
            ShiftersGeneratedAssets.Cleanup();
            AssetDatabase.SaveAssets();
            Debug.Log("[FontAssetBuilder] built " + built + ", up to date " + skipped + " of " + FontNames.Length + " font assets in " + OutFolder);
        }

        static bool HasDeterministicGuid(string path)
            => AssetDatabase.AssetPathToGUID(path) == ShiftersGeneratedAssets.DeterministicGuid(path);

        static bool IsUpToDate(string name)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath(name));
            if (fa == null || fa.material == null || fa.atlasTexture == null) return false;
            if (!HasDeterministicGuid(FontPath(name)) || !HasDeterministicGuid(AtlasPath(name)) || !HasDeterministicGuid(MaterialPath(name))) return false;
            if (AssetDatabase.GetAssetPath(fa.atlasTexture) != AtlasPath(name) || AssetDatabase.GetAssetPath(fa.material) != MaterialPath(name)) return false;
            if (name.StartsWith("Bungee"))
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(LogoPath(name)) == null || !HasDeterministicGuid(LogoPath(name))) return false;
                if (AssetDatabase.LoadAssetAtPath<Material>(ShadowPath(name)) == null || !HasDeterministicGuid(ShadowPath(name))) return false;
            }
            return fa.characterTable != null && fa.characterTable.Count >= 90;
        }

        static void DeleteIfPresent(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }

        static bool BuildFont(string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFolder + "/" + name + ".ttf");
            if (font == null) { Debug.LogError("[FontAssetBuilder] missing font " + SourceFolder + "/" + name + ".ttf"); return false; }
            // start clean: older layouts kept material/atlas as sub-assets with random GUIDs/fileIDs
            DeleteIfPresent(FontPath(name));
            DeleteIfPresent(AtlasPath(name));
            DeleteIfPresent(MaterialPath(name));
            if (name.StartsWith("Bungee")) { DeleteIfPresent(LogoPath(name)); DeleteIfPresent(ShadowPath(name)); }

            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null) { Debug.LogError("[FontAssetBuilder] CreateFontAsset failed for " + name); return false; }
            fa.name = name + " SDF";
            fa.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log("[FontAssetBuilder] " + name + " missing glyphs: " + missing);

            var memAtlas = fa.atlasTexture;
            var memMat = fa.material;
            try
            {
                if (memAtlas == null || memMat == null) { Debug.LogError("[FontAssetBuilder] " + name + ": no atlas/material created"); return false; }
                memAtlas.name = name + " SDF Atlas";
                var atlas = ShiftersGeneratedAssets.SaveAsset(memAtlas, AtlasPath(name));
                if (atlas == null) return false;

                memMat.name = name + " SDF Material";
                memMat.SetTexture(ShaderUtilities.ID_MainTex, atlas);
                var mat = ShiftersGeneratedAssets.SaveAsset(memMat, MaterialPath(name));
                if (mat == null) return false;

                fa.atlasTextures = new[] { atlas };
                fa.material = mat;
                // keep the pre-populated glyphs: TMP clears dynamic data on build when this flag is set
                var so = new SerializedObject(fa);
                var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
                if (clear != null) { clear.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                if (WriteFontAsset(fa, FontPath(name)) == null) return false;

                if (name.StartsWith("Bungee")) { BuildLogoMaterial(mat, name); BuildShadowMaterial(mat, name); }
            }
            finally
            {
                // hand the in-memory originals back, so TMP_FontAsset.OnDestroy (which destroys its atlas
                // textures and material) cleans up exactly those and never touches the saved assets
                fa.atlasTextures = new[] { memAtlas };
                fa.material = memMat;
                Object.DestroyImmediate(fa);
            }

            var check = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath(name));
            bool ok = check != null && check.atlasTexture != null && check.material != null && check.characterTable.Count >= 90;
            if (!ok) Debug.LogError("[FontAssetBuilder] " + name + " did not round-trip (font/atlas/material missing)");
            else Debug.Log("[FontAssetBuilder] " + name + ": " + check.characterTable.Count + " glyphs, guid " + AssetDatabase.AssetPathToGUID(FontPath(name)));
            return ok;
        }

        static readonly System.Text.RegularExpressions.Regex HeaderRx =
            new System.Text.RegularExpressions.Regex(@"^--- !u!114 &(-?\d+)", System.Text.RegularExpressions.RegexOptions.Multiline);

        /// <summary>Writes the font asset with the SAME deterministic scheme as ShiftersGeneratedAssets.SaveAsset
        /// (GUID = DeterministicGuid(path), canonical main fileID 11400000), but without SaveAsset's internal
        /// Instantiate + DestroyImmediate copy: TMP_FontAsset.OnDestroy would try to destroy the (already
        /// persistent) atlas and material and log "Destroying assets is not permitted" errors.</summary>
        static TMP_FontAsset WriteFontAsset(TMP_FontAsset fa, string path)
        {
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { fa }, path, true);
            if (!System.IO.File.Exists(path)) { Debug.LogError("[FontAssetBuilder] could not write " + path); return null; }
            string text = System.IO.File.ReadAllText(path);
            var m = HeaderRx.Match(text);
            if (m.Success && m.Groups[1].Value != "11400000")
            {
                string oldId = m.Groups[1].Value;
                text = text.Replace("--- !u!114 &" + oldId, "--- !u!114 &11400000").Replace("{fileID: " + oldId + "}", "{fileID: 11400000}");
                System.IO.File.WriteAllText(path, text);
            }
            System.IO.File.WriteAllText(path + ".meta",
                "fileFormatVersion: 2\nguid: " + ShiftersGeneratedAssets.DeterministicGuid(path) +
                "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (loaded == null) Debug.LogError("[FontAssetBuilder] " + path + " did not import");
            return loaded;
        }

        static void BuildLogoMaterial(Material baseMat, string name)
        {
            // same (mobile SDF) shader as the font's material: OUTLINE_ON / UNDERLAY_ON are its shader_features
            var mat = new Material(baseMat) { name = name + " SDF Logo" };
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.16f);
            mat.SetColor("_OutlineColor", new Color(0.09f, 0.05f, 0.02f, 1f));
            mat.SetFloat("_FaceDilate", 0.1f);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
            mat.SetFloat("_UnderlayOffsetX", 0.55f);
            mat.SetFloat("_UnderlayOffsetY", -0.65f);
            mat.SetFloat("_UnderlayDilate", 0.15f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);
            ShiftersGeneratedAssets.SaveAsset(mat, LogoPath(name));
            Object.DestroyImmediate(mat);
        }

        static void BuildShadowMaterial(Material baseMat, string name)
        {
            var mat = new Material(baseMat) { name = name + " SDF Shadow" };
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            mat.SetFloat("_UnderlayOffsetX", 0.4f);
            mat.SetFloat("_UnderlayOffsetY", -0.5f);
            mat.SetFloat("_UnderlaySoftness", 0.35f);
            ShiftersGeneratedAssets.SaveAsset(mat, ShadowPath(name));
            Object.DestroyImmediate(mat);
        }
    }
}
