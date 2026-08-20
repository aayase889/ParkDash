using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Regression checks for the mobile-specific car asset and runtime rendering
/// optimizations. This class is editor-only and is not included in player
/// builds.
/// </summary>
public static class CarMobileOptimizationVerifier
{
    [MenuItem("Car Prototype/Verify Mobile Car Optimization")]
    public static void RunBatch()
    {
        VerifyModel(
            "Assets/Resources/CarModels/RedCar/redcar.fbx",
            3000);
        VerifyModel(
            "Assets/Resources/CarModels/GreenCar/greencar.fbx",
            12000);
        VerifyModel(
            "Assets/Resources/CarModels/PoliceCar/policecar_blend.obj",
            5000);
        VerifyModel(
            "Assets/Resources/CarModels/PurpleCar/purplecarrpro_7.obj",
            3000);
        VerifyModel(
            "Assets/Resources/CarModels/YellowCar/yellowcar_final.obj",
            5000);
        VerifyModel(
            "Assets/Resources/CarModels/BlueCar/bluecar.obj",
            5000);

        VerifyTexture(
            "Assets/Resources/CarModels/RedCar/Material-color-stylized.png",
            1024);
        VerifyTexture(
            "Assets/Resources/CarModels/RedCar/Material-metallic-smoothness.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/RedCar/Material-emission.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/GreenCar/Material-color.png",
            1024);
        VerifyTexture(
            "Assets/Resources/CarModels/GreenCar/Material-metallic.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/PoliceCar/carPolice.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/PoliceCar/Purple_Coated_Glitter-color.png",
            1024);
        VerifyTexture(
            "Assets/Resources/CarModels/PoliceCar/Material.004-color.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/PoliceCar/Material-color.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/PoliceCar/Material.005-color.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/PurpleCar/Purple_Coated_Glitter-color.png",
            1024);
        VerifyTexture(
            "Assets/Resources/CarModels/PurpleCar/Material.004-color.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/PurpleCar/Material-color.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/PurpleCar/Material.005-color.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-red.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-green.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-blue.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/YellowCar/Material-color-purple.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/BlueCar/Material-color.png",
            1024);
        VerifyTexture(
            "Assets/Resources/CarModels/BlueCar/Material-color-red-from-blue.png",
            1024);
        VerifyTexture(
            "Assets/Resources/CarModels/BlueCar/Material-color-green-from-yellow.png",
            1024);
        VerifyTexture(
            "Assets/Resources/CarModels/BlueCar/Material-metallic.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/BlueCar/Material-roughness.png",
            512);
        VerifyTexture(
            "Assets/Resources/CarModels/Limousines/redlimousine_ColorWithWindows.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/Limousines/greenlimousine_ColorWithWindows.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/Limousines/purplelimousine_ColorWithWindows.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/Limousines/yellowlimousine_ColorWithWindows.png",
            2048);
        VerifyTexture(
            "Assets/Resources/CarModels/Limousines/bluelimousine_ColorWithWindows.png",
            2048);
        TextureImporter yellowAtlasImporter = AssetImporter.GetAtPath(
            "Assets/Resources/CarModels/YellowCar/Material-color.png") as TextureImporter;
        Require(
            yellowAtlasImporter != null && !yellowAtlasImporter.mipmapEnabled,
            "The tightly packed yellow-car atlas must not use detail-erasing mipmaps.");
        string[] limousineAtlasPaths =
        {
            "Assets/Resources/CarModels/Limousines/redlimousine_ColorWithWindows.png",
            "Assets/Resources/CarModels/Limousines/greenlimousine_ColorWithWindows.png",
            "Assets/Resources/CarModels/Limousines/purplelimousine_ColorWithWindows.png",
            "Assets/Resources/CarModels/Limousines/yellowlimousine_ColorWithWindows.png",
            "Assets/Resources/CarModels/Limousines/bluelimousine_ColorWithWindows.png"
        };
        for (int index = 0; index < limousineAtlasPaths.Length; index++)
        {
            TextureImporter limousineAtlasImporter =
                AssetImporter.GetAtPath(limousineAtlasPaths[index]) as TextureImporter;
            Require(
                limousineAtlasImporter != null && !limousineAtlasImporter.mipmapEnabled,
                $"{limousineAtlasPaths[index]} must not use detail-erasing mipmaps.");
        }

        VerifyRuntimeCars();
        Debug.Log(
            "[Mobile Car Optimization Verification] PASS: mobile texture overrides, " +
            "static model imports, low-poly shared eyes, disabled detail shadows, and " +
            "shared runtime materials are active.");
    }

    private static void VerifyModel(string path, int maximumTriangles)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        Require(importer != null, $"Missing model importer: {path}");
        Require(!importer.importAnimation, $"{path} still imports animation.");
        Require(!importer.importCameras, $"{path} still imports cameras.");
        Require(!importer.importLights, $"{path} still imports lights.");
        Require(!importer.importBlendShapes, $"{path} still imports blend shapes.");
        Require(!importer.importVisibility, $"{path} still imports visibility curves.");
        Require(
            importer.meshCompression != ModelImporterMeshCompression.Off,
            $"{path} has mesh compression disabled.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(prefab != null, $"Could not load model prefab: {path}");
        int triangles = CountTriangles(prefab);
        Require(
            triangles <= maximumTriangles,
            $"{path} has {triangles} triangles; expected no more than {maximumTriangles}.");
    }

    private static void VerifyTexture(string path, int maximumSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Require(importer != null, $"Missing texture importer: {path}");
        Require(importer.maxTextureSize <= maximumSize, $"{path} exceeds {maximumSize}px.");
        Require(importer.textureType == TextureImporterType.Default, $"{path} is not a Default texture.");
        Require(!importer.isReadable, $"{path} is unnecessarily CPU-readable.");

        VerifyPlatformTexture(importer, path, "Android", maximumSize);
        VerifyPlatformTexture(importer, path, "iPhone", maximumSize);
    }

    private static void VerifyPlatformTexture(
        TextureImporter importer,
        string path,
        string platform,
        int maximumSize)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        Require(settings.overridden, $"{path} has no {platform} override.");
        Require(
            settings.maxTextureSize <= maximumSize,
            $"{path} exceeds {maximumSize}px for {platform}.");
        Require(
            settings.format == TextureImporterFormat.ASTC_6x6,
            $"{path} does not use ASTC 6x6 for {platform}.");
    }

    private static void VerifyRuntimeCars()
    {
        GameObject root = new GameObject("Mobile Car Optimization Verification");
        try
        {
            CarPuzzlePiece firstPurple = CreatePiece(
                root.transform, "First Purple Car", "Purple", new Vector3(-2f, 0f, 0f), 1);
            CarPuzzlePiece secondPurple = CreatePiece(
                root.transform, "Second Purple Car", "Purple", new Vector3(2f, 0f, 0f), 3);
            CarPuzzlePiece actualBlue = CreatePiece(
                root.transform, "Actual Blue Car", "Blue", new Vector3(4f, 0f, 0f), 0);
            CarPuzzlePiece redLimousine = CreatePiece(
                root.transform, "Red Limousine", "Red", new Vector3(6f, 0f, 0f), 0, 2);
            CarPuzzlePiece greenLimousine = CreatePiece(
                root.transform, "Green Limousine", "Green", new Vector3(7f, 0f, 0f), 0, 2);
            CarPuzzlePiece blueLimousine = CreatePiece(
                root.transform, "Blue Limousine", "Blue", new Vector3(8f, 0f, 0f), 0, 2);
            CarPuzzlePiece purpleLimousine = CreatePiece(
                root.transform, "Purple Limousine", "Purple", new Vector3(10f, 0f, 0f), 0, 2);
            CarPuzzlePiece yellowLimousine = CreatePiece(
                root.transform, "Yellow Limousine", "Yellow", new Vector3(12f, 0f, 0f), 0, 2);
            CarPuzzlePiece pinkLimousine = CreatePiece(
                root.transform, "Pink Limousine", "Pink", new Vector3(14f, 0f, 0f), 0, 2);
            CarPuzzlePiece firstYellow = CreatePiece(root.transform, "First Yellow Car", "Yellow", new Vector3(-2f, 0f, 2.5f));
            CarPuzzlePiece secondYellow = CreatePiece(root.transform, "Second Yellow Car", "Yellow", new Vector3(2f, 0f, 2.5f));
            CarPuzzlePiece red = CreatePiece(root.transform, "Red Car", "Red", new Vector3(0f, 0f, 2.5f));
            CarPuzzlePiece green = CreatePiece(root.transform, "Green Car", "Green", new Vector3(0f, 0f, -2.5f));
            CarPuzzlePiece police = CreatePiece(root.transform, "Police Car", "Trash", new Vector3(0f, 0f, -5f));

            Transform eyeRoot = FindDeepChild(firstPurple.transform, "Integrated Windshield Eye Rig");
            Require(eyeRoot != null, "The optimized eye rig was not created.");
            int eyeTriangles = CountTriangles(eyeRoot.gameObject);
            Renderer[] eyeRenderers = eyeRoot.GetComponentsInChildren<Renderer>(true);
            Require(eyeTriangles <= 100, $"The eye rig uses {eyeTriangles} triangles; expected at most 100.");
            Require(eyeRenderers.Length <= 5, $"The eye rig uses {eyeRenderers.Length} renderers; expected at most 5.");
            for (int index = 0; index < eyeRenderers.Length; index++)
            {
                Require(
                    eyeRenderers[index].shadowCastingMode == ShadowCastingMode.Off,
                    $"{eyeRenderers[index].name} still casts a shadow.");
                Require(!eyeRenderers[index].receiveShadows, $"{eyeRenderers[index].name} still receives shadows.");
            }

            MeshFilter firstPupil = FindDeepChild(firstPurple.transform, "Left Pupil").GetComponent<MeshFilter>();
            MeshFilter secondPupil = FindDeepChild(secondPurple.transform, "Left Pupil").GetComponent<MeshFilter>();
            Require(
                firstPupil.sharedMesh == secondPupil.sharedMesh,
                "Cars do not share the optimized pupil mesh.");

            VerifyImportedMaterialsUseInstancing(red);
            VerifyImportedMaterialsUseInstancing(green);
            VerifyImportedMaterialsUseInstancing(firstPurple);
            VerifyImportedMaterialsUseInstancing(actualBlue);
            VerifyImportedMaterialsUseInstancing(redLimousine);
            VerifyImportedMaterialsUseInstancing(greenLimousine);
            VerifyImportedMaterialsUseInstancing(blueLimousine);
            VerifyImportedMaterialsUseInstancing(purpleLimousine);
            VerifyImportedMaterialsUseInstancing(yellowLimousine);
            VerifyImportedMaterialsUseInstancing(pinkLimousine);
            VerifyImportedMaterialsUseInstancing(firstYellow);
            VerifyImportedMaterialsUseInstancing(secondYellow);
            VerifyImportedMaterialsUseInstancing(police);
            VerifyStandardCarMaterial(red, "Red", "Material-color-red");
            VerifyStandardCarMaterial(green, "Green", "Material-color-green");
            VerifyStandardCarMaterial(firstPurple, "Purple", "Material-color-purple");
            VerifyStandardCarMaterial(actualBlue, "Blue", "Material-color-blue");
            VerifyStandardCarMaterial(firstYellow, "Yellow", "Material-color");
            VerifyLimousineMaterial(redLimousine, "Red");
            VerifyLimousineMaterial(greenLimousine, "Green");
            VerifyLimousineMaterial(blueLimousine, "Blue");
            VerifyLimousineMaterial(purpleLimousine, "Purple");
            VerifyLimousineMaterial(yellowLimousine, "Yellow");
            VerifyLimousineMaterial(pinkLimousine, "Pink");
            Require(
                FindDeepChild(pinkLimousine.transform, "Imported Pink Limousine Model") != null,
                "The pink limousine still uses the old procedural body instead of the authored limousine model.");
            VerifyPoliceMaterialsAndLights(police);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static CarPuzzlePiece CreatePiece(
        Transform parent,
        string objectName,
        string colorName,
        Vector3 position,
        int logicalColumn = 0,
        int cellLength = 1)
    {
        GameObject pieceObject = new GameObject(objectName);
        pieceObject.transform.SetParent(parent, false);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();
        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Type pieceColorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        Require(configure != null && pieceColorType != null && directionType != null, "Car configuration API is missing.");

        configure.Invoke(piece, new object[]
        {
            0,
            logicalColumn,
            Enum.Parse(pieceColorType, colorName),
            Enum.Parse(directionType, "Down"),
            position,
            1f,
            1f,
            cellLength,
            false,
            false
        });
        return piece;
    }

    private static void VerifyImportedMaterialsUseInstancing(CarPuzzlePiece piece)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        bool foundImportedMaterial = false;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null || !material.name.Contains("Runtime Material")) continue;
                foundImportedMaterial = true;
                Require(material.enableInstancing, $"{material.name} has instancing disabled.");
            }
        }
        Require(foundImportedMaterial, $"{piece.name} did not use its imported runtime material.");
    }

    private static void VerifyStandardCarMaterial(
        CarPuzzlePiece piece,
        string colorName,
        string expectedTextureName)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        bool foundPaintedAtlas = false;
        bool foundSecondaryMaterial = false;
        int headlightCount = 0;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null) continue;
                if (material.name == "Standard Car Headlight Runtime Material")
                {
                    headlightCount++;
                    Require(material.enableInstancing, "The standard headlight material has instancing disabled.");
                    Require(material.color == Color.white, "The standard headlight material is not pure white.");
                    continue;
                }
                if (!material.name.StartsWith(colorName + " Standard Car ")) continue;

                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null)
                {
                    foundPaintedAtlas = true;
                    Require(
                        texture.name == expectedTextureName,
                        $"{colorName} uses {texture.name} instead of {expectedTextureName}.");
                }
                else
                {
                    foundSecondaryMaterial = true;
                }
            }
        }

        Require(foundPaintedAtlas, $"The {colorName.ToLowerInvariant()} standard car has no painted atlas.");
        Require(foundSecondaryMaterial, $"The {colorName.ToLowerInvariant()} standard car lost its glass material.");
        Require(headlightCount == 2, $"The {colorName.ToLowerInvariant()} standard car must have two fitted headlights.");
    }

    private static void VerifyStylizedRedMaterial(CarPuzzlePiece piece)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        Material material = null;
        for (int rendererIndex = 0; rendererIndex < renderers.Length && material == null; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (materials[materialIndex] == null ||
                    !materials[materialIndex].name.Contains("Blue Car Red Paint Runtime Material"))
                    continue;
                material = materials[materialIndex];
                break;
            }
        }

        Require(material != null, "The red car did not use its stylized runtime material.");
        Require(material.GetTexture("_BaseMap") != null, "The red car has no stylized base-color map.");
        Require(material.GetTexture("_MetallicGlossMap") == null, "The red car still uses the iOS-darkening metallic map.");
        Require(material.GetTexture("_OcclusionMap") == null, "The red car still uses the overly dark baked AO map.");
        Require(material.GetTexture("_EmissionMap") == null, "The red car still uses the mismatched emission map.");
        Require(!material.IsKeywordEnabled("_EMISSION"), "The red car emission keyword should be disabled.");
        Require(material.GetFloat("_Metallic") <= 0.20f, "The red car is too metallic for consistent iPhone rendering.");
    }

    private static void VerifyLimousineMaterial(CarPuzzlePiece piece, string colorName)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null || !material.name.Contains(colorName + " Limousine")) continue;
                found = true;
                Require(
                    material.GetTexture("_BaseMap") != null,
                    $"{material.name} has no baked {colorName.ToLowerInvariant()}-limousine texture.");
            }
        }
        Require(found, $"The {colorName.ToLowerInvariant()} limousine did not use its imported runtime materials.");
    }

    private static void VerifyPurpleMaterial(CarPuzzlePiece piece)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        bool foundBody = false;
        bool foundGlass = false;
        bool foundTrim = false;
        bool foundDetails = false;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null || !material.name.Contains("Purple Car")) continue;
                Require(material.GetTexture("_BaseMap") != null, $"{material.name} has no painted texture.");

                if (material.name.Contains("Purple_Coated_Glitter")) foundBody = true;
                else if (material.name.Contains("004")) foundGlass = true;
                else if (material.name.Contains("005")) foundDetails = true;
                else foundTrim = true;
            }
        }

        Require(foundBody, "The replacement purple car has no painted body material.");
        Require(foundGlass, "The replacement purple car has no blue glass material.");
        Require(foundTrim, "The replacement purple car has no wheel/trim material.");
        Require(foundDetails, "The replacement purple car has no white-detail material.");
    }

    private static void VerifyYellowMaterial(CarPuzzlePiece piece)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        bool foundPaintedAtlas = false;
        bool foundSecondaryMaterial = false;
        int headlightCount = 0;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null && material.name == "Yellow Headlight Runtime Material")
                {
                    headlightCount++;
                    Require(
                        material.enableInstancing,
                        "The yellow headlight material has instancing disabled.");
                    continue;
                }
                if (material == null || !material.name.StartsWith("Yellow Car ")) continue;
                if (material.GetTexture("_BaseMap") != null)
                {
                    foundPaintedAtlas = true;
                }
                else
                {
                    foundSecondaryMaterial = true;
                }
            }
        }

        Require(foundPaintedAtlas, "The replacement yellow car has no painted texture atlas.");
        Require(foundSecondaryMaterial, "The replacement yellow car lost its secondary material.");
        Require(headlightCount == 2, "The yellow car must have two fitted headlights.");
    }

    private static void VerifyBlueMaterial(CarPuzzlePiece piece)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        Material blueMaterial = null;
        for (int rendererIndex = 0; rendererIndex < renderers.Length && blueMaterial == null; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null && material.name == "Blue Car Runtime Material")
                {
                    blueMaterial = material;
                    break;
                }
            }
        }

        Require(blueMaterial != null, "The separate blue car was not instantiated.");
        Require(blueMaterial.GetTexture("_BaseMap") != null, "The blue car has no painted texture.");
    }

    private static void VerifyPoliceMaterialsAndLights(CarPuzzlePiece piece)
    {
        HashSet<Material> policeMaterials = new HashSet<Material>();
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null && material.name.StartsWith("Police "))
                    policeMaterials.Add(material);
            }
        }

        int texturedMaterials = 0;
        foreach (Material material in policeMaterials)
            if (material.GetTexture("_BaseMap") != null) texturedMaterials++;
        Require(policeMaterials.Count >= 4, "The replacement police car lost its separate materials.");
        Require(texturedMaterials >= 4, "The replacement police car is missing painted textures.");

        Material redLight = null;
        Material blueLight = null;
        foreach (Material material in policeMaterials)
        {
            if (material.name == "Police Red Roof Light") redLight = material;
            if (material.name == "Police Blue Roof Light") blueLight = material;
        }
        Require(redLight != null, "The OBJ's red roof lamp is not connected to the flasher.");
        Require(blueLight != null, "The OBJ's blue roof lamp is not connected to the flasher.");
        Require(redLight.IsKeywordEnabled("_EMISSION"), "The red police light cannot glow.");
        Require(blueLight.IsKeywordEnabled("_EMISSION"), "The blue police light cannot glow.");
        Require(
            FindDeepChild(piece.transform, "Police Red Light Emitter") == null &&
            FindDeepChild(piece.transform, "Police Blue Light Emitter") == null,
            "Separate police-light cubes are still being created.");
    }

    private static int CountTriangles(GameObject root)
    {
        int triangleCount = 0;
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
        {
            Mesh mesh = filters[filterIndex].sharedMesh;
            if (mesh == null) continue;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                triangleCount += (int)mesh.GetIndexCount(subMesh) / 3;
        }

        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int rendererIndex = 0; rendererIndex < skinned.Length; rendererIndex++)
        {
            Mesh mesh = skinned[rendererIndex].sharedMesh;
            if (mesh == null) continue;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                triangleCount += (int)mesh.GetIndexCount(subMesh) / 3;
        }
        return triangleCount;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < children.Length; index++)
            if (children[index].name == objectName)
                return children[index];
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
