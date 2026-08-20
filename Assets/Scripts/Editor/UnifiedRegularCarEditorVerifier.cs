using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that all five standard one-cell colors use the exact Yellow Car
/// mesh, retain their shared gameplay components, and use Yellow-atlas color
/// variants. It also produces an editor render for visual inspection.
/// </summary>
public static class UnifiedRegularCarEditorVerifier
{
    private const int PreviewLayer = 31;
    private const string StandardModelAssetPath =
        "Assets/Resources/CarModels/YellowCar/yellowcar_final.obj";
    private const string PreviewPath = "/private/tmp/unified_yellow_body_cars_unity.png";
    private const string SidePreviewPath = "/private/tmp/unified_yellow_body_cars_side_unity.png";

    [MenuItem("Car Prototype/Verify Unified Regular Cars")]
    public static void RunBatch()
    {
        GameObject root = null;
        RenderTexture renderTexture = null;
        Texture2D screenshot = null;
        Texture2D sideScreenshot = null;
        try
        {
            root = new GameObject("Unified Regular Car Verification");
            CarPuzzlePiece red = CreatePiece(root.transform, "Red", new Vector3(-4.20f, 0f, 0f));
            CarPuzzlePiece green = CreatePiece(root.transform, "Green", new Vector3(-2.10f, 0f, 0f));
            CarPuzzlePiece yellow = CreatePiece(root.transform, "Yellow", Vector3.zero);
            CarPuzzlePiece blue = CreatePiece(root.transform, "Blue", new Vector3(2.10f, 0f, 0f));
            CarPuzzlePiece purple = CreatePiece(root.transform, "Purple", new Vector3(4.20f, 0f, 0f));
            CarPuzzlePiece redLimousine = CreatePiece(root.transform, "Red", new Vector3(6.50f, 0f, 0f), 2);

            Transform redModel = RequireChild(red.transform, "Yellow Geometry Red Car Model");
            Transform greenModel = RequireChild(green.transform, "Yellow Geometry Green Car Model");
            Transform yellowModel = RequireChild(yellow.transform, "Imported Yellow Car Model");
            Transform blueModel = RequireChild(blue.transform, "Yellow Geometry Blue Car Model");
            Transform purpleModel = RequireChild(purple.transform, "Yellow Geometry Purple Car Model");
            VerifySameMesh(redModel, yellowModel, "red");
            VerifySameMesh(greenModel, yellowModel, "green");
            VerifySameMesh(blueModel, yellowModel, "blue");
            VerifySameMesh(purpleModel, yellowModel, "purple");
            VerifyCorrectedFrontSideWindowNormals();
            VerifyPaintTexture(redModel, "Material-color-red");
            VerifyPaintTexture(greenModel, "Material-color-green");
            VerifyPaintTexture(yellowModel, "Material-color");
            VerifyPaintTexture(blueModel, "Material-color-blue");
            VerifyPaintTexture(purpleModel, "Material-color-purple");
            VerifyFittedWhiteHeadlights(redModel, "Red");
            VerifyFittedWhiteHeadlights(greenModel, "Green");
            VerifyFittedWhiteHeadlights(yellowModel, "Yellow");
            VerifyFittedWhiteHeadlights(blueModel, "Blue");
            VerifyFittedWhiteHeadlights(purpleModel, "Purple");
            VerifySideWindowFrames(redModel);
            VerifySideWindowFrames(greenModel);
            VerifySideWindowFrames(yellowModel);
            VerifySideWindowFrames(blueModel);
            VerifySideWindowFrames(purpleModel);
            VerifyGameplayVisuals(red);
            VerifyGameplayVisuals(green);
            VerifyGameplayVisuals(yellow);
            VerifyGameplayVisuals(blue);
            VerifyGameplayVisuals(purple);
            VerifyGameplayVisuals(redLimousine);
            Require(
                FindDeepChild(redLimousine.transform, "Imported Red Limousine Model") != null,
                "The limousine eye check did not build the imported red limousine.");

            CreateFloor(root.transform);
            CreateLighting(root.transform);
            Camera camera = CreateCamera(root.transform);
            SetLayerRecursively(root, PreviewLayer);
            renderTexture = new RenderTexture(1400, 760, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            screenshot = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(PreviewPath, screenshot.EncodeToPNG());
            RenderTexture.active = previous;

            red.gameObject.SetActive(false);
            green.gameObject.SetActive(false);
            yellow.gameObject.SetActive(false);
            purple.gameObject.SetActive(false);
            redLimousine.gameObject.SetActive(false);
            camera.orthographicSize = 1.65f;
            camera.transform.position = blue.transform.position + new Vector3(3.8f, 3.0f, -1.0f);
            camera.transform.LookAt(blue.transform.position + new Vector3(0f, 0.30f, -0.45f));
            camera.Render();

            RenderTexture.active = renderTexture;
            sideScreenshot = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                TextureFormat.RGBA32,
                false);
            sideScreenshot.ReadPixels(
                new Rect(0f, 0f, renderTexture.width, renderTexture.height),
                0,
                0);
            sideScreenshot.Apply();
            File.WriteAllBytes(SidePreviewPath, sideScreenshot.EncodeToPNG());
            RenderTexture.active = previous;

            camera.targetTexture = null;

            Debug.Log(
                "[Unified Regular Car Verification] PASS: red, green, yellow, blue, and purple use " +
                $"the exact Yellow Car meshes; requested textures, headlights, arrows, eyes, and root colliders are present. " +
                $"previews={PreviewPath},{SidePreviewPath}");
        }
        finally
        {
            if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
            if (sideScreenshot != null) UnityEngine.Object.DestroyImmediate(sideScreenshot);
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static CarPuzzlePiece CreatePiece(
        Transform parent,
        string colorName,
        Vector3 position,
        int cellLength = 1)
    {
        GameObject pieceObject = new GameObject(colorName + " Unified Car");
        pieceObject.transform.SetParent(parent, false);
        CarPuzzlePiece piece = pieceObject.AddComponent<CarPuzzlePiece>();
        MethodInfo configure = typeof(CarPuzzlePiece).GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Type colorType = typeof(CarPrototype3D).GetNestedType("PieceColor", BindingFlags.NonPublic);
        Type directionType = typeof(CarPrototype3D).GetNestedType("ExitDirection", BindingFlags.NonPublic);
        Require(configure != null && colorType != null && directionType != null, "Car configuration API is missing.");
        configure.Invoke(piece, new object[]
        {
            0,
            0,
            Enum.Parse(colorType, colorName),
            Enum.Parse(directionType, "Down"),
            position,
            1f,
            1f,
            cellLength,
            false,
            -1,
            -1,
            false,
            false
        });
        return piece;
    }

    private static void VerifySameMesh(Transform variant, Transform yellow, string colorName)
    {
        MeshFilter[] variantFilters = variant.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter[] yellowFilters = yellow.GetComponentsInChildren<MeshFilter>(true);
        Require(variantFilters.Length == yellowFilters.Length, $"The {colorName} car mesh count differs from Yellow Car.");
        for (int index = 0; index < yellowFilters.Length; index++)
        {
            Mesh expected = yellowFilters[index].sharedMesh;
            Mesh actual = variantFilters[index].sharedMesh;
            Require(actual == expected, $"The {colorName} car does not share Yellow Car mesh {index}.");
            string sourcePath = AssetDatabase.GetAssetPath(actual);
            if (sourcePath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
            {
                Require(
                    sourcePath == StandardModelAssetPath,
                    $"The {colorName} car mesh {index} is not sourced from {StandardModelAssetPath}.");
            }
        }
        Require(variant.localRotation == yellow.localRotation, $"The {colorName} car orientation differs from Yellow Car.");
        Require(
            Vector3.Distance(variant.localScale, yellow.localScale) < 0.0001f,
            $"The {colorName} car scale differs from Yellow Car.");
    }

    private static void VerifyCorrectedFrontSideWindowNormals()
    {
        string source = File.ReadAllText(StandardModelAssetPath);
        Require(
            source.Contains("vn -0.9174 0.3979 0.0105") &&
            source.Contains("vn -0.9174 0.3979 0.0104") &&
            source.Contains("vn 0.9174 0.3979 0.0105") &&
            source.Contains("vn 0.9173 0.3980 0.0104"),
            "The front side-window normals do not face outward and upward on both sides.");
    }

    private static void VerifyPaintTexture(Transform model, string expectedTextureName)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Texture texture = materials[materialIndex] != null
                    ? materials[materialIndex].mainTexture
                    : null;
                if (texture != null && texture.name == expectedTextureName)
                    return;
            }
        }
        throw new InvalidOperationException($"{model.name} does not use {expectedTextureName}.");
    }

    private static void VerifyFittedWhiteHeadlights(Transform model, string colorName)
    {
        Transform left = RequireChild(model, colorName + " Left Headlight");
        Transform right = RequireChild(model, colorName + " Right Headlight");
        Vector3 leftNormal = new Vector3(-0.1381f, 0.7297f, 0.6697f).normalized;
        Vector3 rightNormal = new Vector3(0.1381f, 0.7297f, 0.6697f).normalized;
        const float lensSurfaceOffset = 0.002f;
        VerifyFittedWhiteHeadlight(
            left,
            new Vector3(-0.7130f, 0.2091f, 0.6592f) + leftNormal * lensSurfaceOffset,
            leftNormal);
        VerifyFittedWhiteHeadlight(
            right,
            new Vector3(0.7130f, 0.2091f, 0.6592f) + rightNormal * lensSurfaceOffset,
            rightNormal);
    }

    private static void VerifyFittedWhiteHeadlight(
        Transform headlight,
        Vector3 expectedPosition,
        Vector3 expectedNormal)
    {
        Require(
            Vector3.Distance(headlight.localPosition, expectedPosition) < 0.0001f,
            $"{headlight.name} is not centered in its authored circular recess.");
        Require(
            Vector3.Distance(headlight.localScale, new Vector3(0.195f, 1f, 0.185f)) < 0.0001f,
            $"{headlight.name} does not match the circular recess diameter.");
        MeshFilter filter = headlight.GetComponent<MeshFilter>();
        Require(
            filter != null &&
            filter.sharedMesh != null &&
            filter.sharedMesh.name == "Standard Car Flush Headlight Lens Mesh" &&
            Mathf.Abs(filter.sharedMesh.bounds.size.y) < 0.0001f,
            $"{headlight.name} still uses raised geometry instead of a flush lens.");
        Require(
            Vector3.Angle(headlight.localRotation * Vector3.up, expectedNormal) < 0.01f,
            $"{headlight.name} is not aligned flush to the hood surface.");

        Renderer renderer = headlight.GetComponent<Renderer>();
        Require(
            renderer != null &&
            renderer.sharedMaterial != null &&
            renderer.sharedMaterial.name == "Standard Car Headlight Runtime Material" &&
            renderer.sharedMaterial.color == Color.white,
            $"{headlight.name} does not use the shared pure-white headlight material.");
    }

    private static void VerifySideWindowFrames(Transform model)
    {
        Transform frames = RequireChild(model, "Standard Car Side Window Frames");
        MeshFilter filter = frames.GetComponent<MeshFilter>();
        Renderer renderer = frames.GetComponent<Renderer>();
        Require(
            filter != null &&
            filter.sharedMesh != null &&
            filter.sharedMesh.name == "Standard Car Side Window Frame Mesh" &&
            filter.sharedMesh.vertexCount == 64 &&
            filter.sharedMesh.triangles.Length == 96,
            $"{model.name} does not have four complete side-window frames.");
        Require(
            renderer != null &&
            renderer.sharedMaterial != null &&
            renderer.sharedMaterial.name == "Standard Car Side Window Frame Runtime Material" &&
            renderer.sharedMaterial.enableInstancing,
            $"{model.name} does not use the shared dark side-window frame material.");
    }

    private static void VerifyGameplayVisuals(CarPuzzlePiece piece)
    {
        Require(piece.GetComponent<BoxCollider>() != null, $"{piece.name} lost its gameplay collider.");
        Require(piece.GetComponent<CarEyeController>() != null, $"{piece.name} lost its animated eyes.");
        Transform eyeSurface = RequireChild(piece.transform, "Windshield Eye Surface");
        Renderer eyeSurfaceRenderer = eyeSurface.GetComponent<Renderer>();
        Require(
            eyeSurfaceRenderer != null &&
            eyeSurfaceRenderer.sharedMaterial != null &&
            eyeSurfaceRenderer.sharedMaterial.name == "Runtime Car White Windshield Face" &&
            eyeSurfaceRenderer.sharedMaterial.color == Color.white,
            $"{piece.name} does not use the required neutral-white windshield face.");
        Require(
            Mathf.Abs(Mathf.DeltaAngle(eyeSurface.localEulerAngles.y, 180f)) < 0.1f,
            $"{piece.name} has the windshield face taper inverted toward the roof.");

        Transform leftEye = RequireChild(piece.transform, "Left Pupil");
        Renderer leftEyeRenderer = leftEye.GetComponent<Renderer>();
        Require(
            leftEyeRenderer != null &&
            leftEyeRenderer.sharedMaterial != null &&
            leftEyeRenderer.sharedMaterial.mainTexture != null &&
            leftEyeRenderer.sharedMaterial.mainTexture.name == "amber_eye_gradient",
            $"{piece.name} does not use the approved brown-and-amber eye texture.");
        Require(FindDeepChild(leftEye, "Left Pupil Highlight") != null, $"{piece.name} lost its left eye catchlight.");
        Require(FindDeepChild(piece.transform, "Right Pupil Highlight") != null, $"{piece.name} lost its right eye catchlight.");
        Require(FindDeepChild(piece.transform, "Movement Direction Arrow") != null, $"{piece.name} lost its direction arrow.");
    }

    private static void VerifyRedRearWindow(CarPuzzlePiece red)
    {
        Transform frame = RequireChild(red.transform, "Red Car Rear Window Frame");
        Transform glass = RequireChild(red.transform, "Red Car Rear Window Glass");
        MeshFilter frameFilter = frame.GetComponent<MeshFilter>();
        MeshFilter glassFilter = glass.GetComponent<MeshFilter>();
        Renderer frameRenderer = frame.GetComponent<Renderer>();
        Renderer glassRenderer = glass.GetComponent<Renderer>();

        Require(frameFilter != null && frameFilter.sharedMesh != null,
            "The red rear-window frame has no fitted mesh.");
        Require(glassFilter != null && glassFilter.sharedMesh != null,
            "The red rear-window glass has no fitted mesh.");
        Require(frameRenderer != null && frameRenderer.sharedMaterial != null,
            "The red rear-window frame has no material.");
        Require(glassRenderer != null && glassRenderer.sharedMaterial != null,
            "The red rear-window glass has no material.");
        Require(glassRenderer.sharedMaterial.name == "Red Car Rear Window Glass Material",
            "The red rear window does not use its blue-glass material.");

        Bounds frameBounds = frameFilter.sharedMesh.bounds;
        Bounds glassBounds = glassFilter.sharedMesh.bounds;
        Require(frameBounds.center.z < -0.80f && glassBounds.center.z < -0.80f,
            "The red rear window is not attached to the back of the car.");
        Require(glassBounds.size.x < frameBounds.size.x && glassBounds.size.z < frameBounds.size.z,
            "The rear-window glass does not remain inside its frame.");
        Require(glassBounds.max.y < 0.82f,
            "The red rear-window glass is raised above the measured hatch surface.");
    }

    private static void CreateFloor(Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Preview Asphalt";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, -0.235f, 0f);
        floor.transform.localScale = new Vector3(0.85f, 1f, 0.48f);
        Renderer renderer = floor.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "Unified Car Preview Asphalt",
            color = new Color(0.12f, 0.17f, 0.22f)
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.12f, 0.17f, 0.22f));
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.16f);
        renderer.sharedMaterial = material;
    }

    private static void CreateLighting(Transform parent)
    {
        GameObject keyObject = new GameObject("Preview Key Light");
        keyObject.transform.SetParent(parent, false);
        Light key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.35f;
        key.cullingMask = 1 << PreviewLayer;
        key.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

        GameObject fillObject = new GameObject("Preview Fill Light");
        fillObject.transform.SetParent(parent, false);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.48f;
        fill.cullingMask = 1 << PreviewLayer;
        fill.transform.rotation = Quaternion.Euler(68f, 145f, 0f);
    }

    private static Camera CreateCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetParent(parent, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.85f;
        camera.cullingMask = 1 << PreviewLayer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.075f, 0.095f, 0.125f);
        camera.transform.position = new Vector3(0f, 8.4f, -5.2f);
        camera.transform.LookAt(new Vector3(0f, 0.32f, 0.15f));
        return camera;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
            transforms[index].gameObject.layer = layer;
    }

    private static Transform RequireChild(Transform parent, string objectName)
    {
        Transform child = FindDeepChild(parent, objectName);
        Require(child != null, $"{parent.name} is missing {objectName}.");
        return child;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < children.Length; index++)
            if (children[index].name == objectName) return children[index];
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
