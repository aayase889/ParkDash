using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class MPlusRoundedTmpFontAssetBuilder
{
    private const string SourceFolder = "Assets/Fonts/MPLUSRounded1c";
    private const string OutputFolder = "Assets/Fonts/TMP";

    [InitializeOnLoadMethod]
    private static void AutoCreateMissingFonts()
    {
        EditorApplication.delayCall += CreateMissingFontAssets;
    }

    [MenuItem("Tools/Color Sort/Create M PLUS TMP Fonts")]
    public static void CreateMissingFontAssets()
    {
        if (!Directory.Exists(SourceFolder)) return;

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();

        bool createdAny = false;
        string[] guids = AssetDatabase.FindAssets("t:Font", new[] { SourceFolder });
        foreach (string guid in guids)
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(guid);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (sourceFont == null) continue;

            string baseName = Path.GetFileNameWithoutExtension(fontPath);
            string assetPath = $"{OutputFolder}/{baseName} SDF.asset";
            if (File.Exists(assetPath)) continue;

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogWarning($"Could not create TMP font asset for {sourceFont.name}.", sourceFont);
                continue;
            }

            fontAsset.name = $"{baseName} SDF";
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = $"{baseName} SDF Atlas";
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{baseName} SDF Material";
            }

            AssetDatabase.CreateAsset(fontAsset, assetPath);

            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }

            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            createdAny = true;
        }

        if (!createdAny) return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created M PLUS Rounded TMP font assets in Assets/Fonts/TMP.");
    }
}
