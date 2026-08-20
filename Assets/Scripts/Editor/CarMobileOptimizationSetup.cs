using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies repeatable mobile import settings to the 3D car assets. Keeping this
/// as an editor command prevents future FBX or texture replacements from
/// silently reintroducing animations, cameras, CPU-readable textures, or large
/// uncompressed mobile texture formats.
/// </summary>
public static class CarMobileOptimizationSetup
{
    private const string AutomaticSetupSessionKey =
        "CarPrototype.MobileOptimizationAppliedThisEditorSession.v19";

    [InitializeOnLoadMethod]
    private static void ScheduleAutomaticSetup()
    {
        if (SessionState.GetBool(AutomaticSetupSessionKey, false)) return;
        SessionState.SetBool(AutomaticSetupSessionKey, true);
        EditorApplication.delayCall -= ApplyAutomatically;
        EditorApplication.delayCall += ApplyAutomatically;
    }

    private static void ApplyAutomatically()
    {
        EditorApplication.delayCall -= ApplyAutomatically;
        Apply();
        CarMobileOptimizationVerifier.RunBatch();
    }

    [MenuItem("Car Prototype/Apply Mobile Car Optimization")]
    public static void Apply()
    {
        ConfigureModel("Assets/Resources/CarModels/RedCar/redcar.fbx");
        ConfigureModel("Assets/Resources/CarModels/GreenCar/greencar.fbx");
        ConfigureModel("Assets/Resources/CarModels/PoliceCar/policecar_blend.obj");
        ConfigureModel("Assets/Resources/CarModels/PurpleCar/purplecarrpro_7.obj");
        ConfigureModel("Assets/Resources/CarModels/YellowCar/yellowcar_final.obj");
        ConfigureModel("Assets/Resources/CarModels/BlueCar/bluecar.obj");

        ConfigureTexture("Assets/Resources/CarModels/RedCar/Material-color-stylized.png", 1024, true);
        ConfigureTexture("Assets/Resources/CarModels/RedCar/Material-metallic-smoothness.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/RedCar/Material-emission.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/GreenCar/Material-color.png", 1024, true);
        ConfigureTexture("Assets/Resources/CarModels/GreenCar/Material-metallic.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/GreenCar/Material-roughness.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PoliceCar/carPolice.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/PoliceCar/Purple_Coated_Glitter-color.png", 1024, true);
        ConfigureTexture("Assets/Resources/CarModels/PoliceCar/Material.004-color.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/PoliceCar/Material-color.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/PoliceCar/Material.005-color.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Purple_Coated_Glitter-color.png", 1024, true);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material.004-color.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material-color.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material.005-color.png", 512, true);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Purple_Coated_Glitter-metallic.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Purple_Coated_Glitter-roughness.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material.004-metallic.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material.004-roughness.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material-metallic.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material-roughness.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material.005-metallic.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/PurpleCar/Material.005-roughness.png", 512, false);
        // This Blender atlas has tightly packed window and headlight islands.
        // Mipmaps blend those small blue/white islands into adjacent yellow
        // paint at gameplay size, making the details appear to vanish.
        ConfigureTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-red.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-green.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-blue.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-purple.png",
            2048,
            true,
            false);
        ConfigureTexture("Assets/Resources/CarModels/BlueCar/Material-color.png", 1024, true);
        ConfigureTexture("Assets/Resources/CarModels/BlueCar/Material-metallic.png", 512, false);
        ConfigureTexture("Assets/Resources/CarModels/BlueCar/Material-roughness.png", 512, false);
        // Keep the tightly packed limousine atlases at full resolution without
        // mipmaps so their small windows, lamps and tires do not bleed into
        // transparent gaps when the cars are viewed on the phone.
        ConfigureTexture(
            "Assets/Resources/CarModels/Limousines/redlimousine_ColorWithWindows.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/Limousines/greenlimousine_ColorWithWindows.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/Limousines/purplelimousine_ColorWithWindows.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/Limousines/yellowlimousine_ColorWithWindows.png",
            2048,
            true,
            false);
        ConfigureTexture(
            "Assets/Resources/CarModels/Limousines/bluelimousine_ColorWithWindows.png",
            2048,
            true,
            false);

        CarPrototypeVisualPipelineSetup.EnsureConfigured();
        AssetDatabase.SaveAssets();
        Debug.Log(
            "[Mobile Car Optimization] Applied static compressed model imports, " +
            "mipmapped ASTC textures, half-resolution SSAO, and compact shadows.");
    }

    private static void ConfigureModel(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Could not optimize missing model: {path}");
            return;
        }

        bool changed = false;
        if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
        if (importer.importCameras) { importer.importCameras = false; changed = true; }
        if (importer.importLights) { importer.importLights = false; changed = true; }
        if (importer.importBlendShapes) { importer.importBlendShapes = false; changed = true; }
        if (importer.importVisibility) { importer.importVisibility = false; changed = true; }
        if (importer.isReadable) { importer.isReadable = false; changed = true; }
        if (importer.meshCompression != ModelImporterMeshCompression.Medium)
        {
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            changed = true;
        }

        if (changed) importer.SaveAndReimport();
    }

    private static void ConfigureTexture(
        string path,
        int maximumSize,
        bool sRgb,
        bool useMipmaps = true)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Could not optimize missing texture: {path}");
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.None)
        {
            importer.spriteImportMode = SpriteImportMode.None;
            changed = true;
        }
        if (importer.isReadable)
        {
            importer.isReadable = false;
            changed = true;
        }
        if (importer.mipmapEnabled != useMipmaps)
        {
            importer.mipmapEnabled = useMipmaps;
            changed = true;
        }
        if (importer.streamingMipmaps)
        {
            importer.streamingMipmaps = false;
            changed = true;
        }
        if (importer.sRGBTexture != sRgb)
        {
            importer.sRGBTexture = sRgb;
            changed = true;
        }
        if (importer.maxTextureSize != maximumSize)
        {
            importer.maxTextureSize = maximumSize;
            changed = true;
        }
        if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }
        if (importer.anisoLevel != 1)
        {
            importer.anisoLevel = 1;
            changed = true;
        }
        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            changed = true;
        }

        changed |= SetPlatformTexture(importer, "Android", maximumSize);
        changed |= SetPlatformTexture(importer, "iPhone", maximumSize);
        if (changed) importer.SaveAndReimport();
    }

    private static bool SetPlatformTexture(
        TextureImporter importer,
        string platform,
        int maximumSize)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        if (settings.overridden &&
            settings.maxTextureSize == maximumSize &&
            settings.format == TextureImporterFormat.ASTC_6x6 &&
            settings.textureCompression == TextureImporterCompression.CompressedHQ &&
            settings.compressionQuality == 75)
            return false;

        settings.name = platform;
        settings.overridden = true;
        settings.maxTextureSize = maximumSize;
        settings.format = TextureImporterFormat.ASTC_6x6;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = 75;
        importer.SetPlatformTextureSettings(settings);
        return true;
    }

}
