using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Imports the bundled Lilita One typeface as a TMP font and assigns it to both
/// Colorarrows HUD layouts. It is safe to run again from the Tools menu.
/// </summary>
public static class LilitaOneTmpFontInstaller
{
    // Kept as an editor-only installer so font creation is reproducible.
    private const string SourceFontPath = "Assets/Fonts/LilitaOne/LilitaOne-Regular.ttf";
    private const string FontAssetPath = "Assets/Fonts/TMP/Lilita One SDF.asset";

    [InitializeOnLoadMethod]
    private static void InstallAfterImport()
    {
        EditorApplication.delayCall += Install;
    }

    [MenuItem("Tools/Color Sort/Install Lilita One Game Font")]
    public static void Install()
    {
        if (!File.Exists(SourceFontPath)) return;

        Directory.CreateDirectory("Assets/Fonts/TMP");
        AssetDatabase.Refresh();

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null) return;

            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogError("Could not create the Lilita One TMP font asset.");
                return;
            }

            fontAsset.name = "Lilita One SDF";
            fontAsset.material.name = "Lilita One SDF Material";
            fontAsset.atlasTextures[0].name = "Lilita One SDF Atlas";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        CarPrototypeHudLayout carLayout = AssetDatabase.LoadAssetAtPath<CarPrototypeHudLayout>("Assets/Resources/CarPrototypeHudLayout.asset");
        if (carLayout != null && carLayout.fontOverride != fontAsset)
        {
            carLayout.fontOverride = fontAsset;
            EditorUtility.SetDirty(carLayout);
        }

        ColorSortHudLayout colorSortLayout = AssetDatabase.LoadAssetAtPath<ColorSortHudLayout>("Assets/Resources/ColorSortHudLayout.asset");
        if (colorSortLayout != null && colorSortLayout.hudFont != fontAsset)
        {
            colorSortLayout.hudFont = fontAsset;
            EditorUtility.SetDirty(colorSortLayout);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("Lilita One font installed for all Colorarrows UI text.");
    }
}
