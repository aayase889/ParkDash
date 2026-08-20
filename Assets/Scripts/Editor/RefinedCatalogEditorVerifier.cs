#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class RefinedCatalogEditorVerifier
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("Tools/Car Prototype/Verify Refined Levels 21-100")]
    public static void VerifyFromMenu()
    {
        VerifyInternal();
        Debug.Log("Refined catalog verification passed for Levels 21-100.");
    }

    public static void RunBatch()
    {
        try
        {
            VerifyInternal();
            Debug.Log("REFINED CATALOG BATCH VERIFICATION PASSED: Levels 21-100.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void VerifyInternal()
    {
        Type gameType = typeof(CarPrototype3D);
        GameObject root = new GameObject("Refined Catalog Verification");
        try
        {
            CarPrototype3D game = root.AddComponent<CarPrototype3D>();
            IList levels = GetField<IList>(gameType, game, "levels");
            levels.Clear();
            Invoke(gameType, game, "BuildTutorialLevels", new object[] { null });
            Invoke(gameType, game, "BuildRefinedCatalogLevels", null);
            if (levels.Count != 100)
                throw new InvalidOperationException($"Expected 100 campaign levels; found {levels.Count}.");

            for (int listIndex = 20; listIndex < levels.Count; listIndex++)
                VerifyLevel(gameType, levels[listIndex], listIndex + 1);

            VerifyArtwork("CarPrototype/Mechanics/MysteryBox");
            VerifyArtwork("CarPrototype/Mechanics/GarageOpen");
            VerifyArtwork("CarPrototype/Mechanics/GarageClosed");
            VerifyMechanicMaterial();
            VerifyRuntimeArtworkMaterials();
            VerifyArtworkPixelRendering();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyLevel(Type gameType, object level, int expectedBoardNumber)
    {
        Type levelType = level.GetType();
        int boardNumber = GetField<int>(levelType, level, "boardNumber");
        int boardSize = GetField<int>(levelType, level, "boardSize");
        int capacity = GetField<int>(levelType, level, "matchTarget");
        Array specifications = GetField<Array>(levelType, level, "customSpecifications");
        Array garages = GetField<Array>(levelType, level, "garageSpecifications");
        object difficulty = GetField<object>(levelType, level, "difficulty");

        if (boardNumber != expectedBoardNumber)
            throw new InvalidOperationException(
                $"Catalog index {expectedBoardNumber} contains Board {boardNumber}.");
        string expectedDifficulty = ExpectedDifficulty(boardNumber);
        if (!string.Equals(difficulty.ToString(), expectedDifficulty, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Level {boardNumber} should be {expectedDifficulty}, found {difficulty}.");
        if (boardSize < 3 || boardSize > 5)
            throw new InvalidOperationException($"Level {boardNumber} has unsupported {boardSize}x{boardSize} grid.");
        if (capacity < 2 || capacity > 5)
            throw new InvalidOperationException($"Level {boardNumber} has invalid tray capacity {capacity}.");

        MethodInfo geometryMethod = gameType.GetMethod(
            "MeetsRefinedDifficultyGeometry",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (geometryMethod == null)
            throw new MissingMethodException(gameType.FullName, "MeetsRefinedDifficultyGeometry");
        object[] geometryArguments =
        {
            boardSize,
            specifications,
            garages,
            difficulty,
            0,
            0
        };
        bool meetsDifficultyGeometry = (bool)geometryMethod.Invoke(null, geometryArguments);
        if ((int)geometryArguments[4] <= 0)
            throw new InvalidOperationException(
                $"Level {boardNumber} has no unboxed opening move and would fail immediately.");
        if (!string.Equals(difficulty.ToString(), "Normal", StringComparison.Ordinal))
        {
            if (!meetsDifficultyGeometry)
                throw new InvalidOperationException(
                    $"Level {boardNumber} is labelled {difficulty} but exposes "
                    + $"{geometryArguments[4]} opening moves with only "
                    + $"{geometryArguments[5]} blocker depth.");
        }

        bool shouldHaveBoxes = boardNumber >= 30;
        int boxCount = 0;
        int[] colorCounts = new int[7];
        var occupied = new bool[boardSize, boardSize];
        for (int index = 0; index < specifications.Length; index++)
        {
            object specification = specifications.GetValue(index);
            Type specificationType = specification.GetType();
            int row = GetField<int>(specificationType, specification, "row");
            int col = GetField<int>(specificationType, specification, "col");
            int length = GetField<int>(specificationType, specification, "cellLength");
            object color = GetField<object>(specificationType, specification, "color");
            object direction = GetField<object>(specificationType, specification, "direction");
            bool boxed = GetField<bool>(specificationType, specification, "isRevealBox");
            int colorIndex = Convert.ToInt32(color);
            if (colorIndex != 5) colorCounts[colorIndex]++;
            if (boxed)
            {
                boxCount++;
                if (colorIndex == 5 || length != 1)
                    throw new InvalidOperationException(
                        $"Level {boardNumber} has a box over an unsupported car.");
                if (!HasEarlierAdjacentPiece(specifications, index))
                    throw new InvalidOperationException(
                        $"Level {boardNumber} has a box that cannot be revealed "
                        + "by an earlier car in its proven removal order.");
            }

            Vector2Int step = DirectionStep(direction.ToString());
            for (int offset = 0; offset < length; offset++)
            {
                int cellRow = row - step.y * offset;
                int cellCol = col - step.x * offset;
                if (cellRow < 0 || cellRow >= boardSize || cellCol < 0 || cellCol >= boardSize
                    || occupied[cellRow, cellCol])
                    throw new InvalidOperationException(
                        $"Level {boardNumber} has overlapping/out-of-grid piece at {cellRow},{cellCol}.");
                occupied[cellRow, cellCol] = true;
            }
        }

        bool hasGarage = garages.Length > 0;
        if (hasGarage != (boardNumber >= 50))
            throw new InvalidOperationException(
                $"Level {boardNumber} garage start rule is wrong (garage count {garages.Length}).");
        for (int garageIndex = 0; garageIndex < garages.Length; garageIndex++)
        {
            object garage = garages.GetValue(garageIndex);
            Type garageType = garage.GetType();
            int row = GetField<int>(garageType, garage, "row");
            int col = GetField<int>(garageType, garage, "col");
            Array queue = GetField<Array>(garageType, garage, "carQueue");
            if (row < 0 || row >= boardSize - 1)
                throw new InvalidOperationException(
                    $"Level {boardNumber} garage {garageIndex + 1} is on the bottom row.");
            if (occupied[row, col])
                throw new InvalidOperationException(
                    $"Level {boardNumber} garage {garageIndex + 1} does not own a unique grid cell.");
            occupied[row, col] = true;
            if (occupied[row + 1, col])
                throw new InvalidOperationException(
                    $"Level {boardNumber} garage {garageIndex + 1} front overlaps another piece.");
            occupied[row + 1, col] = true;
            var queueColors = new HashSet<int>();
            for (int queueIndex = 0; queueIndex < queue.Length; queueIndex++)
            {
                int color = Convert.ToInt32(queue.GetValue(queueIndex));
                if (color < 0 || color > 6 || color == 5)
                    throw new InvalidOperationException(
                        $"Level {boardNumber} garage queue contains a non-playable car.");
                colorCounts[color]++;
                queueColors.Add(color);
            }
            if (queueColors.Count < 2)
                throw new InvalidOperationException(
                    $"Level {boardNumber} garage {garageIndex + 1} does not mix colors.");
        }

        for (int row = 0; row < boardSize; row++)
        for (int col = 0; col < boardSize; col++)
            if (!occupied[row, col])
                throw new InvalidOperationException($"Level {boardNumber} leaves grid cell {row},{col} empty.");

        if (shouldHaveBoxes && boxCount == 0)
            throw new InvalidOperationException($"Level {boardNumber} should contain boxes from Level 30 onward.");
        if (!shouldHaveBoxes && boxCount != 0)
            throw new InvalidOperationException($"Level {boardNumber} contains a box before Level 30.");

        for (int color = 0; color < colorCounts.Length; color++)
        {
            if (color == 5 || colorCounts[color] == 0) continue;
            bool valid = hasGarage
                ? colorCounts[color] % capacity == 0
                : colorCounts[color] == capacity;
            if (!valid)
                throw new InvalidOperationException(
                    $"Level {boardNumber} violates color-set arithmetic: color {color} has "
                    + $"{colorCounts[color]} cars with capacity {capacity}.");
        }
    }

    private static bool HasEarlierAdjacentPiece(Array specifications, int centerIndex)
    {
        object center = specifications.GetValue(centerIndex);
        Type centerType = center.GetType();
        int centerRow = GetField<int>(centerType, center, "row");
        int centerCol = GetField<int>(centerType, center, "col");
        for (int index = 0; index < centerIndex; index++)
        {
            object candidate = specifications.GetValue(index);
            Type candidateType = candidate.GetType();
            int row = GetField<int>(candidateType, candidate, "row");
            int col = GetField<int>(candidateType, candidate, "col");
            int length = GetField<int>(candidateType, candidate, "cellLength");
            object direction = GetField<object>(candidateType, candidate, "direction");
            Vector2Int step = DirectionStep(direction.ToString());
            for (int offset = 0; offset < length; offset++)
            {
                int occupiedRow = row - step.y * offset;
                int occupiedCol = col - step.x * offset;
                int distance = Mathf.Abs(occupiedRow - centerRow)
                    + Mathf.Abs(occupiedCol - centerCol);
                if (distance == 1) return true;
            }
        }
        return false;
    }

    private static string ExpectedDifficulty(int boardNumber)
    {
        int position = (boardNumber - 21) % 5;
        if (position == 3) return "Hard";
        if (position == 4) return "SuperHard";
        return "Normal";
    }

    private static Vector2Int DirectionStep(string direction)
    {
        if (direction == "Up") return new Vector2Int(0, -1);
        if (direction == "Down") return new Vector2Int(0, 1);
        if (direction == "Left") return new Vector2Int(-1, 0);
        return new Vector2Int(1, 0);
    }

    private static void VerifyArtwork(string resourcePath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            throw new InvalidOperationException($"Missing approved raster art Resources/{resourcePath}.");
        if (texture.width < 1024 || texture.height < 1024)
            throw new InvalidOperationException($"Approved raster art {resourcePath} is unexpectedly low-resolution.");

        string assetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"{resourcePath} was not imported as a texture.");
        if (!importer.alphaIsTransparency || importer.mipmapEnabled)
            throw new InvalidOperationException(
                $"{resourcePath} must preserve transparency and disable mipmaps for clean grid artwork.");
    }

    private static void VerifyRuntimeArtworkMaterials()
    {
        string[] resourcePaths =
        {
            "CarPrototype/Mechanics/MysteryBox",
            "CarPrototype/Mechanics/GarageOpen",
            "CarPrototype/Mechanics/GarageClosed"
        };
        MethodInfo method = typeof(CarPrototype3D).GetMethod(
            "GetApprovedMechanicMaterial",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null)
            throw new MissingMethodException(
                typeof(CarPrototype3D).FullName,
                "GetApprovedMechanicMaterial");
        for (int index = 0; index < resourcePaths.Length; index++)
        {
            Material material = (Material)method.Invoke(null, new object[] { resourcePaths[index] });
            if (material == null || material.mainTexture == null)
                throw new InvalidOperationException(
                    $"Runtime mechanic material {resourcePaths[index]} has no texture.");
            if (material.shader == null || !material.shader.isSupported
                || material.shader.name != "Universal Render Pipeline/Unlit")
                throw new InvalidOperationException(
                    $"Runtime mechanic material {resourcePaths[index]} has an unsupported shader.");
            if (material.renderQueue != (int)RenderQueue.Transparent)
                throw new InvalidOperationException(
                    $"Runtime mechanic material {resourcePaths[index]} is not transparent.");
        }
    }

    private static void VerifyArtworkPixelRendering()
    {
        string[] resourcePaths =
        {
            "CarPrototype/Mechanics/MysteryBox",
            "CarPrototype/Mechanics/GarageOpen",
            "CarPrototype/Mechanics/GarageClosed"
        };
        MethodInfo materialMethod = typeof(CarPrototype3D).GetMethod(
            "GetApprovedMechanicMaterial",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        for (int pathIndex = 0; pathIndex < resourcePaths.Length; pathIndex++)
        {
            GameObject cameraObject = null;
            GameObject quad = null;
            RenderTexture target = null;
            Texture2D readback = null;
            try
            {
                Material material = (Material)materialMethod.Invoke(
                    null,
                    new object[] { resourcePaths[pathIndex] });
                cameraObject = new GameObject("Mechanic Artwork Pixel Test Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 0.55f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.07f, 0.11f, 1f);
                camera.transform.position = new Vector3(0f, 0f, -2f);

                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
                target.Create();
                if (!target.IsCreated())
                    throw new InvalidOperationException(
                        "Unity could not create the artwork verification RenderTexture.");
                camera.targetTexture = target;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                readback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;

                Color[] pixels = readback.GetPixels();
                int artworkPixels = 0;
                int magentaPixels = 0;
                Color background = camera.backgroundColor;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color pixel = pixels[index];
                    float backgroundDifference = Mathf.Abs(pixel.r - background.r)
                        + Mathf.Abs(pixel.g - background.g)
                        + Mathf.Abs(pixel.b - background.b);
                    if (backgroundDifference > 0.18f) artworkPixels++;
                    if (pixel.r > 0.82f && pixel.g < 0.22f && pixel.b > 0.82f)
                        magentaPixels++;
                }
                if (artworkPixels < pixels.Length / 20)
                    throw new InvalidOperationException(
                        $"{resourcePaths[pathIndex]} rendered blank ({artworkPixels} visible pixels). ");
                if (magentaPixels > pixels.Length / 100)
                    throw new InvalidOperationException(
                        $"{resourcePaths[pathIndex]} rendered Unity error magenta "
                        + $"({magentaPixels} pixels). ");
            }
            finally
            {
                if (cameraObject != null)
                {
                    Camera camera = cameraObject.GetComponent<Camera>();
                    if (camera != null) camera.targetTexture = null;
                }
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (quad != null) UnityEngine.Object.DestroyImmediate(quad);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }

    private static void VerifyMechanicMaterial()
    {
        Material material = Resources.Load<Material>(
            "CarPrototype/Mechanics/MechanicArtworkMaterial");
        if (material == null || material.shader == null)
            throw new InvalidOperationException(
                "The approved mechanic artwork URP material is missing.");
        if (!material.shader.isSupported
            || material.shader.name != "Universal Render Pipeline/Unlit")
            throw new InvalidOperationException(
                $"Mechanic artwork must use the supported URP Unlit shader; found "
                + $"{material.shader.name} (supported={material.shader.isSupported}).");
    }

    private static T GetField<T>(Type type, object instance, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, AnyInstance);
        if (field == null) throw new MissingFieldException(type.FullName, fieldName);
        return (T)field.GetValue(instance);
    }

    private static object Invoke(Type type, object instance, string methodName, object[] arguments)
    {
        MethodInfo method = type.GetMethod(methodName, InstancePrivate);
        if (method == null) throw new MissingMethodException(type.FullName, methodName);
        return method.Invoke(instance, arguments);
    }
}
#endif
